using System;
using System.Collections.Generic;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Editor.Animals;
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
    /// that no fit solves are excluded. Follows the grooming bow (GroomingPipeline.MeasureBow). Idempotent.
    /// </summary>
    public static class AccessoryFitPipeline
    {
        /// <summary>The accessories were modeled on the companion: its widths are the size reference.</summary>
        public const string ReferenceSpecies = "species.fieldhamster";

        [MenuItem("Pocket Wilds/Cosmetics/Fit Accessories (T47)", priority = 193)]
        public static void BuildAllMenu() => Debug.Log("[AccessoryFitPipeline] " + BuildAll());

        public static string BuildAll()
        {
            var shop = AssetDatabase.LoadAssetAtPath<ShopCatalogAsset>(ShopPipeline.CatalogPath);
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var visuals = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var cosmetics = AssetDatabase.LoadAssetAtPath<CosmeticVisualCatalog>(AccessoryVisualPipeline.CatalogPath);
            if (shop == null || database == null || visuals == null || cosmetics == null)
                return "missing data: run CosmeticPipeline and AccessoryVisualPipeline first";
            var report = new List<string>();
            foreach (var accessory in shop.accessories)
            {
                var entry = cosmetics.Find(accessory.sourceId);
                if (entry == null || entry.prefab == null)
                {
                    report.Add(accessory.sourceId + ": no prefab");
                    continue;
                }
                var shape = AccessoryFitMeasure.LoadAccessory(entry.prefab);
                string socket = AccessoryFitMeasure.SocketName(accessory.socket);
                var candidates = AccessoryFitMeasure.CompatibleSpecies(database, visuals, accessory);
                var surfaces = new Dictionary<string, AccessoryFitMeasure.Surface>();
                foreach (var (id, prefab) in candidates)
                    surfaces[id] = AccessoryFitMeasure.Bake(prefab, socket);
                float reference = surfaces.TryGetValue(ReferenceSpecies, out var refSurface) && refSurface != null
                    ? AccessoryFitMeasure.Width(refSurface, accessory.socket) : 0f;
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
                    float width = AccessoryFitMeasure.Width(surface, accessory.socket);
                    float target = reference > 1e-4f && width > 1e-4f ? Mathf.Clamp(width / reference, AccessoryFitMeasure.MinScale, AccessoryFitMeasure.MaxScale) : 1f;
                    var result = AccessoryFitMeasure.Fit(shape, surface, target);
                    float clearance = result.Ok ? AccessoryFitMeasure.MinClearance(shape, surface, result.Fit, out _) : -1f;
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
                    lines.Add($"{id} scale {fit.LocalScale.x:0.00} lift {fit.LocalPosition.y * 1000f:0.0} mm z {fit.LocalPosition.z * 1000f:0} mm, " +
                              $"{surface.Poses.Count} poses, clearance {clearance * 1000f:0.0} mm");
                }
                accessory.speciesFits = fits;
                accessory.excludedSpecies = excluded;
                report.Add(accessory.sourceId + ": " + string.Join(", ", lines));
            }
            EditorUtility.SetDirty(shop);
            AssetDatabase.SaveAssets();
            return string.Join(" | ", report);
        }
    }

    /// <summary>The measuring behind <see cref="AccessoryFitPipeline"/>, shared with AccessoryFitTests.</summary>
    public static class AccessoryFitMeasure
    {
        /// <summary>The rule: at least 2 mm between an accessory and the skin, in every pose.</summary>
        public const float Clearance = 0.002f;

        /// <summary>Fitted with a margin, so a smaller individual (scale 0.8) keeps the rule.</summary>
        public const float FitClearance = 0.0025f;

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
        }

        /// <summary>The skin of one species in socket space: one vertex array per pose, the shared triangles.</summary>
        public sealed class Surface
        {
            public readonly List<Vector3[]> Poses = new List<Vector3[]>();
            public readonly List<string> Names = new List<string>();
            public int[] Triangles;
        }

        public struct Result
        {
            public bool Ok;
            public AccessoryFit Fit;
            public float MeanGap;
            public string Reason;
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
            // The underside: the lowest sample of each 2 mm column.
            var lowest = new Dictionary<long, int>();
            for (int i = 0; i < points.Count; i++)
            {
                long key = ((long)Mathf.FloorToInt(points[i].x / 0.002f) << 32) ^ (uint)Mathf.FloorToInt(points[i].z / 0.002f);
                if (!lowest.TryGetValue(key, out int at) || points[i].y < points[at].y)
                    lowest[key] = i;
            }
            var bottom = new bool[points.Count];
            foreach (var i in lowest.Values)
                bottom[i] = true;
            return new Shape
            {
                Vertices = vertices.ToArray(), Triangles = triangles.ToArray(), Points = points.ToArray(), Bottom = bottom,
                Reach = points.Count > 0 ? points.Max(p => Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z))) : 0f,
                Radius = points.Count > 0 ? points.Max(p => p.magnitude) : 0f,
            };
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
                var surface = new Surface { Triangles = skin.sharedMesh.triangles };
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

            public float At(float x, float z)
            {
                int ix = Mathf.RoundToInt((x + Reach) / Cell), iz = Mathf.RoundToInt((z + Reach) / Cell);
                if ((uint)ix >= (uint)Size || (uint)iz >= (uint)Size)
                    return float.NegativeInfinity;
                return Height[iz * Size + ix];
            }
        }

        /// <summary>The highest skin over every pose under each 1 mm cell around the socket, widened by the clearance.</summary>
        private static Field Envelope(Surface surface, float reach, float window, float widen)
        {
            int size = Mathf.CeilToInt(2f * reach / Cell) + 1;
            var height = new float[size * size];
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
                    samples.Clear();
                    Sample(a, b, c, SkinStep, samples);
                    foreach (var p in samples)
                    {
                        int ix = Mathf.RoundToInt((p.x + reach) / Cell), iz = Mathf.RoundToInt((p.z + reach) / Cell);
                        if ((uint)ix >= (uint)size || (uint)iz >= (uint)size)
                            continue;
                        int k = iz * size + ix;
                        if (p.y > height[k])
                            height[k] = p.y;
                    }
                }
            }
            // Widen: each cell takes the highest skin within the clearance around it (square, a superset of the disc).
            int r = Mathf.CeilToInt(widen / Cell) + 1;
            var row = new float[size * size];
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++)
                {
                    float best = float.NegativeInfinity;
                    for (int d = Math.Max(0, x - r); d <= Math.Min(size - 1, x + r); d++)
                        best = Mathf.Max(best, height[z * size + d]);
                    row[z * size + x] = best;
                }
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++)
                {
                    float best = float.NegativeInfinity;
                    for (int d = Math.Max(0, z - r); d <= Math.Min(size - 1, z + r); d++)
                        best = Mathf.Max(best, row[d * size + x]);
                    height[z * size + x] = best;
                }
            return new Field { Reach = reach, Size = size, Height = height };
        }

        /// <summary>The resting fit: for each scale and offset the lift that clears the envelope, the one resting closest near the target size.</summary>
        public static Result Fit(Shape shape, Surface surface, float targetScale)
        {
            float reach = shape.Reach * MaxScale + MaxOffset + 0.008f;
            float window = shape.Radius * MaxScale + MaxOffset + MaxLift + 0.01f;
            var field = Envelope(surface, reach, window, FitClearance);
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
                    int count = 0;
                    for (int i = 0; i < points.Length; i++)
                    {
                        if (!shape.Bottom[i])
                            continue;
                        float h = field.At(s * points[i].x, s * points[i].z + dz);
                        if (float.IsNegativeInfinity(h))
                            continue;
                        gap += s * points[i].y + lift - h - FitClearance;
                        count++;
                    }
                    if (count == 0)
                        continue;
                    float meanGap = (float)(gap / count);
                    if (meanGap > MaxMeanGap * s)
                        continue;
                    float cost = meanGap / 0.004f + Mathf.Abs(s - targetScale) / 0.1f + Mathf.Abs(dz) / 0.01f;
                    if (cost >= bestCost)
                        continue;
                    bestCost = cost;
                    best = new Result
                    {
                        Ok = true, MeanGap = meanGap,
                        Fit = new AccessoryFit(new Vector3(0f, lift, dz), Vector3.zero, Vector3.one * (float)Math.Round(s, 2)),
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
