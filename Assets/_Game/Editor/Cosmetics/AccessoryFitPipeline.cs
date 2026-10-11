using System;
using System.Collections.Generic;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Events;
using PocketWilds.Editor.Shop;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Cosmetics
{
    /// <summary>
    /// T47 « tenues ajustées » (owner rule: nothing enters anything). For every accessory and every compatible species with a
    /// visual, the species' LOD0 skin (ears included) is baked at bind pose and at <see cref="AccessoryFitMeasure.FramesPerClip"/>
    /// frames of every clip its animator plays, in the socket's space. The highest skin of all those poses under each point (a
    /// height field, widened by the clearance) gives, for each uniform scale (0.6 to 1.4) and small forward or back offset, the
    /// lift along the socket's up that rests the accessory on the head (crown) or the back (basket) with the clearance; the chosen
    /// fit rests closest (smallest mean gap under it) near the species' own size, never perched on an ear. Each fit is then
    /// checked exactly (point to triangle) at every pose. Fits go into the shop catalog's accessory data (speciesFits); species
    /// that no fit solves are excluded. Follows the grooming bow (GroomingPipeline.MeasureBow). T49: every shop catalog
    /// (DEF_ShopCatalog and DEF_ShopCatalog_Burrows); each piece rests along its rest axis (<see cref="AccessoryFitMeasure.RestAxisOf"/>):
    /// Up as above (the scarf and the overalls' strap junction rest on the nape, on the Neck socket), or Forward for a piece
    /// worn on the chest: pushed along the socket's forward until it rests against the chest in front of it (the frontmost skin
    /// of every pose, widened by the clearance), with the same scale search, an offset up or down, the same mean gap and the
    /// same exact check, and never resting on the head skin (the face and chin follow the head bone, not the neck). No piece
    /// rests Forward since the chest pieces moved to the nape (the chin and front legs cross the chest in the eat, sleep and
    /// inspect poses of every species). The conform fit (AccessoryConformFit.cs) stays for later bands.
    /// Idempotent.
    /// </summary>
    public static class AccessoryFitPipeline
    {
        /// <summary>The accessories were modeled on the companion: its widths are the size reference.</summary>
        public const string ReferenceSpecies = "species.fieldhamster";

        [MenuItem("Pocket Wilds/Cosmetics/Fit Accessories (T47)", priority = 193)]
        public static void BuildAllMenu() => Debug.Log("[AccessoryFitPipeline] " + BuildAll());

        public static string BuildAll()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var visuals = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var cosmetics = AssetDatabase.LoadAssetAtPath<CosmeticVisualCatalog>(AccessoryVisualPipeline.CatalogPath);
            var catalogs = AccessoryFitMeasure.ShopCatalogs(database);
            if (catalogs.Count == 0 || database == null || visuals == null || cosmetics == null)
                return "missing data: run CosmeticPipeline, BurrowsEventPipeline and AccessoryVisualPipeline first";
            var report = new List<string>();
            var bakes = new Dictionary<(string, string), AccessoryFitMeasure.Surface>();
            AccessoryFitMeasure.Surface BakeOnce(string id, GameObject prefab, string socket)
            {
                if (!bakes.TryGetValue((id, socket), out var surface))
                    bakes[(id, socket)] = surface = AccessoryFitMeasure.Bake(prefab, socket);
                return surface;
            }
            foreach (var shop in catalogs)
            {
                foreach (var accessory in shop.accessories)
                {
                    if (accessory == null)
                        continue;
                    var entry = cosmetics.Find(accessory.sourceId);
                    if (entry == null || entry.prefab == null)
                    {
                        report.Add(accessory.sourceId + ": no prefab");
                        continue;
                    }
                    var shape = AccessoryFitMeasure.LoadAccessory(entry.prefab);
                    string socket = AccessoryFitMeasure.SocketName(accessory.socket);
                    bool conform = AccessoryFitMeasure.IsConform(accessory.sourceId);
                    var axis = AccessoryFitMeasure.RestAxisOf(accessory.sourceId);
                    var candidates = AccessoryFitMeasure.CompatibleSpecies(database, visuals, accessory);
                    var surfaces = new Dictionary<string, AccessoryFitMeasure.Surface>();
                    foreach (var (id, prefab) in candidates)
                        surfaces[id] = BakeOnce(id, prefab, socket);
                    var refPrefab = AccessoryFitMeasure.PrefabOf(database, visuals, ReferenceSpecies);
                    var refSurface = surfaces.TryGetValue(ReferenceSpecies, out var s0) ? s0
                        : refPrefab != null ? BakeOnce(ReferenceSpecies, refPrefab, socket) : null;
                    float reference = refSurface != null ? AccessoryFitMeasure.Width(refSurface, accessory.socket) : 0f;
                    // Conform bands: their inner side (lying on the reference skin) is what must hug every wearer.
                    var inner = conform ? AccessoryFitMeasure.InnerPoints(shape, refSurface) : null;
                    var fits = new List<ShopCatalogAsset.SpeciesFit>();
                    var excluded = new List<string>();
                    var lines = new List<string>();
                    foreach (var (id, _) in candidates)
                    {
                        var surface = surfaces[id];
                        if (surface == null)
                        {
                            excluded.Add(id);
                            lines.Add(id + " excluded (no socket or skin)");
                            continue;
                        }
                        AccessoryFitMeasure.Result result;
                        float clearance;
                        if (conform)
                        {
                            result = AccessoryFitMeasure.SolveConform(shape, surface, inner);
                            clearance = result.Clearance;
                        }
                        else
                        {
                            float width = AccessoryFitMeasure.Width(surface, accessory.socket);
                            float target = reference > 1e-4f && width > 1e-4f ? Mathf.Clamp(width / reference, AccessoryFitMeasure.MinScale, AccessoryFitMeasure.MaxScale) : 1f;
                            result = AccessoryFitMeasure.Fit(shape, surface, target, axis);
                            clearance = result.Ok ? AccessoryFitMeasure.MinClearance(shape, surface, result.Fit, out _) : -1f;
                        }
                        if (!result.Ok || clearance < AccessoryFitMeasure.Clearance)
                        {
                            excluded.Add(id);
                            lines.Add($"{id} excluded ({(result.Ok ? $"clearance {clearance * 1000f:0.0} mm" : result.Reason)})");
                            continue;
                        }
                        var fit = result.Fit;
                        fits.Add(new ShopCatalogAsset.SpeciesFit
                        {
                            speciesId = id, localPosition = fit.LocalPosition, localEuler = fit.LocalEuler, localScale = fit.LocalScale,
                        });
                        string scale = conform ? $"scale {fit.LocalScale.x:0.00}x{fit.LocalScale.y:0.00}x{fit.LocalScale.z:0.00}, hug {result.MeanGap * 1000f:0.0} mm (rest {result.RestGap * 1000f:0.0} mm)"
                            : $"scale {fit.LocalScale.x:0.00}, gap {result.MeanGap * 1000f:0.0} mm";
                        string rest = !conform && axis == AccessoryFitMeasure.RestAxis.Forward
                            ? $"push {fit.LocalPosition.z * 1000f:0.0} mm y {fit.LocalPosition.y * 1000f:0} mm"
                            : $"lift {fit.LocalPosition.y * 1000f:0.0} mm z {fit.LocalPosition.z * 1000f:0} mm";
                        lines.Add($"{id} {scale} {rest}, {surface.Poses.Count} poses, clearance {clearance * 1000f:0.0} mm");
                    }
                    accessory.speciesFits = fits;
                    accessory.excludedSpecies = excluded;
                    report.Add($"{accessory.sourceId} [{fits.Count} fitted]: " + string.Join(", ", lines));
                }
                EditorUtility.SetDirty(shop);
            }
            AssetDatabase.SaveAssets();
            return string.Join(" | ", report);
        }
    }

    /// <summary>The measuring behind <see cref="AccessoryFitPipeline"/>, shared with AccessoryFitTests (conform bands: AccessoryConformFit.cs).</summary>
    public static partial class AccessoryFitMeasure
    {
        /// <summary>Every shop catalog with accessories: the base one, the burrows event's, then any other the database lists.</summary>
        public static List<ShopCatalogAsset> ShopCatalogs(ContentDatabaseAsset database)
        {
            var list = new List<ShopCatalogAsset>();
            void Add(ShopCatalogAsset c)
            {
                if (c != null && !list.Contains(c))
                    list.Add(c);
            }
            Add(AssetDatabase.LoadAssetAtPath<ShopCatalogAsset>(ShopPipeline.CatalogPath));
            Add(AssetDatabase.LoadAssetAtPath<ShopCatalogAsset>(BurrowsEventPipeline.CatalogPath));
            if (database != null)
                foreach (var c in database.shopCatalogs)
                    Add(c);
            return list;
        }

        /// <summary>The rule: at least 2 mm between an accessory and the skin, in every pose.</summary>
        public const float Clearance = 0.002f;

        /// <summary>Fitted with a margin, so a smaller individual (scale 0.8) keeps the rule.</summary>
        public const float FitClearance = 0.0025f;

        /// <summary>How a rigid piece comes to rest on a wearer (pw_accessories.py fit « lift » or « push »).</summary>
        public enum RestAxis
        {
            /// <summary>Lifted along the socket's up until it rests on the skin under it (head top, back).</summary>
            Up,

            /// <summary>Pushed along the socket's forward until it rests against the chest in front of it (pieces worn on the chest).</summary>
            Forward,
        }

        /// <summary>
        /// The pieces worn on the chest, resting along <see cref="RestAxis.Forward"/>; every other piece rests along Up. Empty since
        /// T49: the gardener's scarf and the overalls' strap junction rest on the nape (Neck socket, rest axis Up).
        /// </summary>
        public static readonly HashSet<string> ForwardPieces = new HashSet<string>();

        public static RestAxis RestAxisOf(string accessoryId) => ForwardPieces.Contains(accessoryId) ? RestAxis.Forward : RestAxis.Up;

        /// <summary>A Forward piece never rests on the head skin (face, chin): at most this share of its resting samples.</summary>
        public const float MaxHeadShare = 0.2f;

        /// <summary>A Forward piece lies flat on the chest: its mean gap stays under this as well as under 12 mm x scale (never floating).</summary>
        public const float MaxForwardMeanGap = 0.008f;

        public const float MinScale = 0.6f;
        public const float MaxScale = 1.4f;
        public const int FramesPerClip = 6;

        private const float Cell = 0.001f;
        private const float SkinStep = 0.0007f;
        private const float PointStep = 0.001f;
        private const float ScaleStep = 0.05f;
        private const float MaxOffset = 0.02f;
        private const float OffsetStep = 0.005f;
        private const float MinLift = -0.03f;
        private const float MaxLift = 0.05f;
        private const float MaxMeanGap = 0.012f;

        /// <summary>An accessory in its prefab space (the attach point at the origin): triangles, surface samples, bottom samples.</summary>
        public sealed class Shape
        {
            public Vector3[] Vertices;
            public int[] Triangles;
            public Vector3[] Points;
            public bool[] Bottom;
            public float Reach;
            public float Radius;

            /// <summary>Vertices and face centers only (the conform search).</summary>
            public Vector3[] Coarse;
        }

        /// <summary>The skin of one species in socket space: one vertex array per pose, the shared triangles.</summary>
        public sealed class Surface
        {
            public readonly List<Vector3[]> Poses = new List<Vector3[]>();
            public readonly List<string> Names = new List<string>();
            public int[] Triangles;

            /// <summary>Per skin vertex: its strongest bone is the head or below it (ears). Null: unknown.</summary>
            public bool[] Head;

            /// <summary>Per skin vertex: its strongest bone is a leg (FrontLeg_*, HindLeg_* and below). Null: unknown.</summary>
            public bool[] Limb;
        }

        public struct Result
        {
            public bool Ok;
            public AccessoryFit Fit;
            public float MeanGap;
            public string Reason;

            /// <summary>Exact clearance of the chosen fit over every pose (conform fits).</summary>
            public float Clearance;

            /// <summary>Mean gap of a conform band's inner side at rest (MeanGap: the closest over the clip poses).</summary>
            public float RestGap;
        }

        public static string SocketName(AccessorySocket socket) => socket switch
        {
            AccessorySocket.HeadTop => "SOCK_HeadTop",
            AccessorySocket.Neck => "SOCK_Neck",
            AccessorySocket.Back => "SOCK_Back",
            AccessorySocket.TailBase => "SOCK_TailBase",
            AccessorySocket.EarL => "SOCK_Ear_L",
            _ => "SOCK_Ear_R",
        };

        /// <summary>Species of a compatible family, with a visual, not hiding the socket (source id, prefab).</summary>
        public static List<(string id, GameObject prefab)> CompatibleSpecies(ContentDatabaseAsset database, AnimalVisualCatalog visuals,
            ShopCatalogAsset.Accessory accessory)
        {
            var result = new List<(string, GameObject)>();
            foreach (var asset in database.species)
            {
                if (asset == null)
                    continue;
                var species = asset.ToRuntime();
                if (!accessory.families.Any(f => f.family == species.RigFamily) || species.HidesSocket(accessory.socket))
                    continue;
                if (!visuals.TryGet(species.VisualKey, out var visual) || visual.prefab == null)
                    continue;
                result.Add((asset.sourceId, visual.prefab.gameObject));
            }
            return result;
        }

        public static GameObject PrefabOf(ContentDatabaseAsset database, AnimalVisualCatalog visuals, string speciesId)
        {
            var asset = database.species.FirstOrDefault(s => s != null && s.sourceId == speciesId);
            if (asset == null)
                return null;
            return visuals.TryGet(asset.ToRuntime().VisualKey, out var visual) && visual.prefab != null ? visual.prefab.gameObject : null;
        }

        public static Shape LoadAccessory(GameObject prefab)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var toRoot = prefab.transform.worldToLocalMatrix;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;
                var matrix = toRoot * filter.transform.localToWorldMatrix;
                int start = vertices.Count;
                vertices.AddRange(mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)));
                triangles.AddRange(mesh.triangles.Select(t => start + t));
            }
            var points = new List<Vector3>(vertices);
            for (int t = 0; t + 2 < triangles.Count; t += 3)
                Sample(vertices[triangles[t]], vertices[triangles[t + 1]], vertices[triangles[t + 2]], PointStep, points);
            var coarse = new List<Vector3>(vertices);
            for (int t = 0; t + 2 < triangles.Count; t += 3)
                coarse.Add((vertices[triangles[t]] + vertices[triangles[t + 1]] + vertices[triangles[t + 2]]) / 3f);
            return Measured(vertices.ToArray(), triangles.ToArray(), points.ToArray(), coarse.ToArray());
        }

        private static Shape Measured(Vector3[] vertices, int[] triangles, Vector3[] points, Vector3[] coarse)
        {
            // The resting side: the lowest sample of each 2 mm column.
            var lowest = new Dictionary<long, int>();
            for (int i = 0; i < points.Length; i++)
            {
                long key = ((long)Mathf.FloorToInt(points[i].x / 0.002f) << 32) ^ (uint)Mathf.FloorToInt(points[i].z / 0.002f);
                if (!lowest.TryGetValue(key, out int at) || points[i].y < points[at].y)
                    lowest[key] = i;
            }
            var bottom = new bool[points.Length];
            foreach (var i in lowest.Values)
                bottom[i] = true;
            return new Shape
            {
                Vertices = vertices, Triangles = triangles, Points = points, Bottom = bottom, Coarse = coarse,
                Reach = points.Length > 0 ? points.Max(p => Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z))) : 0f,
                Radius = points.Length > 0 ? points.Max(p => p.magnitude) : 0f,
            };
        }

        /// <summary>Forward rest: up and forward swap (y and z), so the search's « lift » runs along the socket's forward and its offset up and down.</summary>
        private static Vector3 Swap(Vector3 v) => new Vector3(v.x, v.z, v.y);

        /// <summary>The shape as the search sees it along <paramref name="axis"/> (its resting side recomputed).</summary>
        public static Shape ForAxis(Shape shape, RestAxis axis) => axis == RestAxis.Up ? shape
            : Measured(shape.Vertices.Select(Swap).ToArray(), shape.Triangles, shape.Points.Select(Swap).ToArray(), shape.Coarse.Select(Swap).ToArray());

        private static Surface ForAxis(Surface surface, RestAxis axis)
        {
            if (axis == RestAxis.Up)
                return surface;
            var swapped = new Surface { Triangles = surface.Triangles, Head = surface.Head, Limb = surface.Limb };
            foreach (var pose in surface.Poses)
                swapped.Poses.Add(pose.Select(Swap).ToArray());
            swapped.Names.AddRange(surface.Names);
            return swapped;
        }

        /// <summary>Barycentric samples of a triangle, no farther apart than <paramref name="step"/>.</summary>
        private static void Sample(Vector3 a, Vector3 b, Vector3 c, float step, List<Vector3> into)
        {
            float longest = Mathf.Max((b - a).magnitude, Mathf.Max((c - a).magnitude, (c - b).magnitude));
            int n = Mathf.Clamp(Mathf.CeilToInt(longest / step), 1, 400);
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n - i; j++)
                    into.Add(a + (b - a) * (i / (float)n) + (c - a) * (j / (float)n));
        }

        /// <summary>The LOD0 skin in socket space at bind pose, then <see cref="FramesPerClip"/> frames of each clip of its animator.</summary>
        public static Surface Bake(GameObject animalPrefab, string socketName)
        {
            var instance = Object.Instantiate(animalPrefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            var baked = new Mesh();
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                var view = instance.GetComponent<AnimalView>();
                var animator = view != null && view.Animator != null ? view.Animator : instance.GetComponentInChildren<Animator>(true);
                if (animator != null)
                    AnimatorUtility.DeoptimizeTransformHierarchy(animator.gameObject);
                var socket = FindDeep(instance.transform, socketName);
                var skin = Lod0(instance);
                if (socket == null || skin == null || skin.sharedMesh == null)
                    return null;
                var surface = new Surface { Triangles = skin.sharedMesh.triangles, Head = BoneVertices(skin, "Head", false), Limb = BoneVertices(skin, "Leg", true) };
                AddPose(surface, skin, socket, baked, "bind");
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    var seen = new HashSet<AnimationClip>();
                    foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    {
                        if (clip == null || !seen.Add(clip))
                            continue;
                        for (int k = 0; k < FramesPerClip; k++)
                        {
                            float time = clip.length * k / (FramesPerClip - 1);
                            clip.SampleAnimation(animator.gameObject, time);
                            AddPose(surface, skin, socket, baked, $"{clip.name}@{time:0.00}");
                        }
                    }
                }
                return surface;
            }
            finally
            {
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(instance);
            }
        }

        private static void AddPose(Surface surface, SkinnedMeshRenderer skin, Transform socket, Mesh baked, string name)
        {
            skin.BakeMesh(baked, true);
            var vertices = baked.vertices;
            var matrix = socket.worldToLocalMatrix * Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            surface.Poses.Add(vertices);
            surface.Names.Add(name);
        }

        /// <summary>
        /// Per skin vertex: is its strongest bone named <paramref name="bone"/> (or containing it) or a bone under one (the head and
        /// its ears; the legs and their paws)?
        /// </summary>
        private static bool[] BoneVertices(SkinnedMeshRenderer skin, string bone, bool contains)
        {
            var mesh = skin.sharedMesh;
            var bones = skin.bones;
            var weights = mesh.boneWeights;
            var head = new bool[mesh.vertexCount];
            if (bones == null || weights == null || weights.Length != head.Length)
                return head;
            var inHead = new bool[bones.Length];
            for (int b = 0; b < bones.Length; b++)
                for (var t = bones[b]; t != null; t = t.parent)
                    if (contains ? t.name.Contains(bone) : t.name == bone)
                    {
                        inHead[b] = true;
                        break;
                    }
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                int best = w.boneIndex0;
                float strongest = w.weight0;
                if (w.weight1 > strongest)
                {
                    best = w.boneIndex1;
                    strongest = w.weight1;
                }
                if (w.weight2 > strongest)
                {
                    best = w.boneIndex2;
                    strongest = w.weight2;
                }
                if (w.weight3 > strongest)
                    best = w.boneIndex3;
                head[i] = best >= 0 && best < inHead.Length && inHead[best];
            }
            return head;
        }

        private static SkinnedMeshRenderer Lod0(GameObject instance)
        {
            var group = instance.GetComponentInChildren<LODGroup>(true);
            if (group != null && group.lodCount > 0)
            {
                var skin = group.GetLODs()[0].renderers.OfType<SkinnedMeshRenderer>().FirstOrDefault();
                if (skin != null)
                    return skin;
            }
            var all = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            return all.FirstOrDefault(s => s.name.Contains("LOD0")) ?? all.FirstOrDefault();
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;
                var found = FindDeep(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>Width of the skull (HeadTop) or the back (Back) just under the socket, at bind pose: the size the fit follows.</summary>
        public static float Width(Surface surface, AccessorySocket socket)
        {
            float halfDepth = socket == AccessorySocket.Back ? 0.02f : 0.012f;
            float below = socket == AccessorySocket.Back ? -0.03f : -0.025f;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var p in surface.Poses[0])
            {
                if (Mathf.Abs(p.z) > halfDepth || p.y < below || p.y > 0.004f)
                    continue;
                min = Mathf.Min(min, p.x);
                max = Mathf.Max(max, p.x);
            }
            return max > min ? max - min : 0f;
        }

        // ---------------------------------------------------------------- height field of the skin envelope

        private sealed class Field
        {
            public float Reach;
            public int Size;
            public float[] Height;

            /// <summary>Per cell: the highest skin there is head skin (tracked for the Forward rest only, else null).</summary>
            public bool[] Head;

            public int Index(float x, float z)
            {
                int ix = Mathf.RoundToInt((x + Reach) / Cell), iz = Mathf.RoundToInt((z + Reach) / Cell);
                return (uint)ix >= (uint)Size || (uint)iz >= (uint)Size ? -1 : iz * Size + ix;
            }

            public float At(float x, float z)
            {
                int k = Index(x, z);
                return k < 0 ? float.NegativeInfinity : Height[k];
            }
        }

        /// <summary>The highest skin over every pose under each 1 mm cell around the socket, widened by the clearance.</summary>
        private static Field Envelope(Surface surface, float reach, float window, float widen, bool trackHead = false)
        {
            int size = Mathf.CeilToInt(2f * reach / Cell) + 1;
            var height = new float[size * size];
            var headCell = trackHead && surface.Head != null ? new bool[size * size] : null;
            for (int i = 0; i < height.Length; i++)
                height[i] = float.NegativeInfinity;
            var tris = surface.Triangles;
            var samples = new List<Vector3>(512);
            foreach (var v in surface.Poses)
            {
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    Vector3 a = v[tris[t]], b = v[tris[t + 1]], c = v[tris[t + 2]];
                    var min = Vector3.Min(a, Vector3.Min(b, c));
                    var max = Vector3.Max(a, Vector3.Max(b, c));
                    if (min.x > reach || max.x < -reach || min.z > reach || max.z < -reach)
                        continue;
                    var nearest = Vector3.Max(min, Vector3.Min(Vector3.zero, max));
                    if (nearest.magnitude > window)
                        continue;
                    bool headTri = headCell != null
                        && (surface.Head[tris[t]] ? 1 : 0) + (surface.Head[tris[t + 1]] ? 1 : 0) + (surface.Head[tris[t + 2]] ? 1 : 0) >= 2;
                    // A chest piece (Forward rest) lies on the torso: the chin and the front legs reach further forward in some poses
                    // but never carry it (the exact check still refuses any touch with them).
                    if (headCell != null && (headTri || (surface.Limb != null
                        && (surface.Limb[tris[t]] ? 1 : 0) + (surface.Limb[tris[t + 1]] ? 1 : 0) + (surface.Limb[tris[t + 2]] ? 1 : 0) >= 2)))
                        continue;
                    samples.Clear();
                    Sample(a, b, c, SkinStep, samples);
                    foreach (var p in samples)
                    {
                        int ix = Mathf.RoundToInt((p.x + reach) / Cell), iz = Mathf.RoundToInt((p.z + reach) / Cell);
                        if ((uint)ix >= (uint)size || (uint)iz >= (uint)size)
                            continue;
                        int k = iz * size + ix;
                        if (p.y > height[k])
                        {
                            height[k] = p.y;
                            if (headCell != null)
                                headCell[k] = headTri;
                        }
                    }
                }
            }
            // Widen: each cell takes the highest skin within the clearance around it (square, a superset of the disc).
            int r = Mathf.CeilToInt(widen / Cell) + 1;
            // The head flag follows the cell whose skin wins.
            var row = new float[size * size];
            var rowHead = headCell != null ? new bool[size * size] : null;
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++)
                {
                    float best = float.NegativeInfinity;
                    int from = -1;
                    for (int d = Math.Max(0, x - r); d <= Math.Min(size - 1, x + r); d++)
                        if (height[z * size + d] > best)
                        {
                            best = height[z * size + d];
                            from = z * size + d;
                        }
                    row[z * size + x] = best;
                    if (rowHead != null && from >= 0)
                        rowHead[z * size + x] = headCell[from];
                }
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++)
                {
                    float best = float.NegativeInfinity;
                    int from = -1;
                    for (int d = Math.Max(0, z - r); d <= Math.Min(size - 1, z + r); d++)
                        if (row[d * size + x] > best)
                        {
                            best = row[d * size + x];
                            from = d * size + x;
                        }
                    height[z * size + x] = best;
                    if (headCell != null)
                        headCell[z * size + x] = from >= 0 && rowHead[from];
                }
            return new Field { Reach = reach, Size = size, Height = height, Head = headCell };
        }

        /// <summary>
        /// The resting fit: for each scale and offset the lift that clears the envelope, the one resting closest near the target size.
        /// Along <see cref="RestAxis.Forward"/> the « lift » is a push along the socket's forward and the offset runs up and down; a
        /// placement whose resting side lies on head skin for more than <see cref="MaxHeadShare"/> is refused.
        /// </summary>
        public static Result Fit(Shape shape, Surface surface, float targetScale, RestAxis axis = RestAxis.Up)
        {
            bool forward = axis == RestAxis.Forward;
            shape = ForAxis(shape, axis);
            float reach = shape.Reach * MaxScale + MaxOffset + 0.008f;
            float window = shape.Radius * MaxScale + MaxOffset + MaxLift + 0.01f;
            var field = Envelope(ForAxis(surface, axis), reach, window, FitClearance, forward);
            var best = new Result { Ok = false, Reason = "no resting fit" };
            float bestCost = float.MaxValue;
            var points = shape.Points;
            for (float s = MinScale; s <= MaxScale + 1e-4f; s += ScaleStep)
            {
                for (float dz = -MaxOffset; dz <= MaxOffset + 1e-4f; dz += OffsetStep)
                {
                    float lift = float.NegativeInfinity;
                    for (int i = 0; i < points.Length; i++)
                    {
                        float h = field.At(s * points[i].x, s * points[i].z + dz);
                        if (float.IsNegativeInfinity(h))
                            continue;
                        float need = h + FitClearance - s * points[i].y;
                        if (need > lift)
                            lift = need;
                    }
                    if (float.IsNegativeInfinity(lift) || lift < MinLift || lift > MaxLift)
                        continue;
                    double gap = 0;
                    int count = 0, onHead = 0;
                    for (int i = 0; i < points.Length; i++)
                    {
                        if (!shape.Bottom[i])
                            continue;
                        int k = field.Index(s * points[i].x, s * points[i].z + dz);
                        if (k < 0 || float.IsNegativeInfinity(field.Height[k]))
                            continue;
                        gap += s * points[i].y + lift - field.Height[k] - FitClearance;
                        count++;
                        if (field.Head != null && field.Head[k])
                            onHead++;
                    }
                    if (count == 0 || onHead > MaxHeadShare * count)
                        continue;
                    float meanGap = (float)(gap / count);
                    if (meanGap > MaxMeanGap * s || (forward && meanGap > MaxForwardMeanGap))
                        continue;
                    float cost = meanGap / 0.004f + Mathf.Abs(s - targetScale) / 0.1f + Mathf.Abs(dz) / 0.01f;
                    if (cost >= bestCost)
                        continue;
                    bestCost = cost;
                    best = new Result
                    {
                        Ok = true, MeanGap = meanGap,
                        Fit = new AccessoryFit(forward ? new Vector3(0f, dz, lift) : new Vector3(0f, lift, dz), Vector3.zero, Vector3.one * (float)Math.Round(s, 2)),
                    };
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- exact check

        /// <summary>
        /// Smallest distance between the fitted accessory (vertices and face centers) and the skin triangles, and between the skin
        /// vertices and the accessory triangles, over every pose; negative when an accessory point lies inside the skin (an odd
        /// number of skin crossings straight above it).
        /// </summary>
        public static float MinClearance(Shape shape, Surface surface, in AccessoryFit fit, out string worstPose)
        {
            var matrix = Matrix4x4.TRS(fit.LocalPosition, Quaternion.Euler(fit.LocalEuler), fit.LocalScale);
            var accVertices = shape.Vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray();
            var accTris = shape.Triangles;
            var points = new List<Vector3>(accVertices);
            for (int t = 0; t + 2 < accTris.Length; t += 3)
                points.Add((accVertices[accTris[t]] + accVertices[accTris[t + 1]] + accVertices[accTris[t + 2]]) / 3f);
            var accMin = points.Aggregate(Vector3.positiveInfinity, Vector3.Min);
            var accMax = points.Aggregate(Vector3.negativeInfinity, Vector3.Max);
            const float Near = 0.01f;
            float worst = float.MaxValue;
            worstPose = string.Empty;
            var tris = surface.Triangles;
            var near = new List<int>(512);
            var column = new List<int>(512);
            for (int pose = 0; pose < surface.Poses.Count; pose++)
            {
                var v = surface.Poses[pose];
                near.Clear();
                column.Clear();
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    Vector3 a = v[tris[t]], b = v[tris[t + 1]], c = v[tris[t + 2]];
                    var min = Vector3.Min(a, Vector3.Min(b, c));
                    var max = Vector3.Max(a, Vector3.Max(b, c));
                    bool overXz = min.x <= accMax.x && max.x >= accMin.x && min.z <= accMax.z && max.z >= accMin.z;
                    if (overXz && max.y >= accMin.y)
                        column.Add(t);
                    if (min.x <= accMax.x + Near && max.x >= accMin.x - Near && min.y <= accMax.y + Near && max.y >= accMin.y - Near
                        && min.z <= accMax.z + Near && max.z >= accMin.z - Near)
                        near.Add(t);
                }
                float poseWorst = float.MaxValue;
                foreach (var p in points)
                {
                    foreach (int t in near)
                        poseWorst = Mathf.Min(poseWorst, PointTriangleDistance(p, v[tris[t]], v[tris[t + 1]], v[tris[t + 2]]));
                    int crossings = 0;
                    foreach (int t in column)
                        if (CrossesAbove(p, v[tris[t]], v[tris[t + 1]], v[tris[t + 2]]))
                            crossings++;
                    if ((crossings & 1) == 1)
                        poseWorst = Mathf.Min(poseWorst, -0.001f);
                }
                for (int i = 0; i < v.Length; i++)
                {
                    var q = v[i];
                    if (q.x < accMin.x - Near || q.x > accMax.x + Near || q.y < accMin.y - Near || q.y > accMax.y + Near
                        || q.z < accMin.z - Near || q.z > accMax.z + Near)
                        continue;
                    for (int t = 0; t + 2 < accTris.Length; t += 3)
                        poseWorst = Mathf.Min(poseWorst, PointTriangleDistance(q, accVertices[accTris[t]], accVertices[accTris[t + 1]], accVertices[accTris[t + 2]]));
                }
                if (poseWorst < worst)
                {
                    worst = poseWorst;
                    worstPose = surface.Names[pose];
                }
            }
            return worst;
        }

        /// <summary>Does the vertical ray up from p cross the triangle (strictly above p)?</summary>
        private static bool CrossesAbove(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            float d = (b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z);
            if (Mathf.Abs(d) < 1e-12f)
                return false;
            float u = ((p.x - a.x) * (c.z - a.z) - (c.x - a.x) * (p.z - a.z)) / d;
            float w = ((b.x - a.x) * (p.z - a.z) - (p.x - a.x) * (b.z - a.z)) / d;
            if (u < 0f || w < 0f || u + w > 1f)
                return false;
            float y = a.y + u * (b.y - a.y) + w * (c.y - a.y);
            return y > p.y;
        }

        /// <summary>Distance from a point to a triangle (Ericson, Real-Time Collision Detection 5.1.5).</summary>
        public static float PointTriangleDistance(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var ab = b - a;
            var ac = c - a;
            var ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f)
                return ap.magnitude;
            var bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3)
                return bp.magnitude;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
                return (p - (a + ab * (d1 / (d1 - d3)))).magnitude;
            var cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6)
                return cp.magnitude;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
                return (p - (a + ac * (d2 / (d2 - d6)))).magnitude;
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
                return (p - (b + (c - b) * ((d4 - d3) / (d4 - d3 + (d5 - d6))))).magnitude;
            float denom = 1f / (va + vb + vc);
            return (p - (a + ab * (vb * denom) + ac * (vc * denom))).magnitude;
        }
    }
}
