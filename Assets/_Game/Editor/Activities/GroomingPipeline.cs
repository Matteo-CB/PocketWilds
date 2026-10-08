using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Vfx;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Activities
{
    /// <summary>
    /// T38 « Toilettage » set pipeline (doc 01 Contenu S-ACT-001; doc « Mini-jeux &amp; activités », sections 1 and 6;
    /// collision rule): the import settings of agent GB's models (Art/Activities/Grooming), the set's palettes and
    /// materials, the measured coat of every species (DEF_GroomingBodies and one groom mesh per species: zones by
    /// farthest-point sampling over the coat the camera sees, skinned to their bones; the bow's place on the head measured
    /// against the head and the ears), the set prefab PF_Activity_Grooming_Set with its pooled props and effects, and the
    /// Addressables group Activity_Grooming. Missing models are listed and skipped, never invented. Idempotent.
    /// Run after the animals (Measure Bodies) and the VFX (the shared particle material).
    /// </summary>
    public static class GroomingPipeline
    {
        public const string ModelsFolder = "Assets/_Game/Art/Activities/Grooming";
        public const string BodiesFolder = ModelsFolder + "/Bodies";
        public const string PrefabPath = "Assets/_Game/Prefabs/Activities/" + GroomingLayout.SetPrefabName + ".prefab";
        public const string BodiesPath = "Assets/_Game/Content/Activities/DEF_GroomingBodies.asset";
        public const string PalettesPath = "Assets/_Game/Content/Palettes/DEF_Palettes_Grooming.asset";
        public const string SetMaterialPath = "Assets/_Game/Art/Materials/MAT_Grooming_Set.mat";
        public const string PropsMaterialPath = "Assets/_Game/Art/Materials/MAT_Grooming_Props.mat";
        public const string GoldMaterialPath = "Assets/_Game/Art/Materials/MAT_Grooming_Gold.mat";
        public const string BubbleMaterialPath = "Assets/_Game/Art/Materials/MAT_Grooming_Bubble.mat";
        public const string EnvironmentShader = "PocketWilds/EnvironmentOpaque";
        public const string BubbleShader = "PocketWilds/SoapBubble";
        public const string EnvironmentCommonPath = "Assets/_Game/Art/Materials/MAT_Environment_Common.mat";
        public const string IconsFolder = "Assets/_Game/Art/UI/Icons";

        public const string SetModel = "GR_Set_Prairie";
        public const string BrushModel = "GR_Brush";
        public const string BurrModel = "GR_Burr";
        public const string BowModel = "GR_Bow";
        public const string BubbleModel = "GR_SoapBubble";

        /// <summary>GB exports every debris kind in one file (objects GR_Debris_*), each with its pivot at its contact point.</summary>
        public const string DebrisModel = "GR_Debris";

        /// <summary>Every debris GB makes; the Prairie theme uses the first four (doc 01: grass blades and daisy petals).</summary>
        public static readonly string[] DebrisModels =
        {
            "GR_Debris_GrassBlade", "GR_Debris_DaisyPetal", "GR_Debris_CloverLeaf", "GR_Debris_Seed", "GR_Debris_Leaf", "GR_Debris_Twig",
        };

        /// <summary>
        /// The Prairie's debris (doc 01: grass blades and daisy petals, with clover). The dandelion seed stays out: standing
        /// on its stalk in the fur it read as a pin stuck in the animal.
        /// </summary>
        public static readonly string[] PrairieDebris = { "GR_Debris_GrassBlade", "GR_Debris_DaisyPetal", "GR_Debris_CloverLeaf" };

        // Palettes (base, R, G, B, A, occlusion, accent): the models' vertex colors are region weights, like the Grassland kit.
        // Ids and colors are agent GB's (SourceArt/Blender/Tools/pw_grooming.py, GROOMING_PALETTES): the regions it painted.
        public const string PaletteMound = "palette.grooming.set_mound";
        public const string PaletteBackdrop = "palette.grooming.set_backdrop";
        public const string PaletteWood = "palette.grooming.brush";
        public const string PaletteGold = "palette.grooming.brush_gold";
        public const string PaletteDebris = "palette.grooming.debris";
        public const string PaletteBurr = "palette.grooming.burr";
        public static readonly string[] BowPalettes = { "palette.grooming.bow_pink", "palette.grooming.bow_sky", "palette.grooming.bow_cream" };

        /// <summary>The foam's sheens in turn (SH_SoapBubble's user value: 0 neutral, 1 pink, 2 sky).</summary>
        public static readonly uint[] FoamTints = { 0u, 1u, 2u };


        /// <summary>Minimum radius of curvature of a small animal's coat (m): debris are lifted by their sagitta over it.</summary>
        public const float CoatCurvature = 0.06f;

        [MenuItem("Pocket Wilds/Activities/Build Grooming (T38)", priority = 240)]
        public static void BuildMenu() => Debug.Log("[GroomingPipeline] " + BuildAll());

        public static string BuildAll()
        {
            var report = new List<string>();
            var missing = new List<string>();
            int configured = ConfigureImporters(missing);
            report.Add($"{configured} models set up" + (missing.Count > 0 ? ", missing: " + string.Join(", ", missing) : ""));
            EnsurePalettes();
            // The Prairie's own look (MAT_Environment_Common's values, one palette row per renderer), in the activity's
            // materials so its bundle never copies the kit's; the props a touch more rim (they read in the close-up).
            var set = EnsureEnvironmentMaterial(SetMaterialPath, "MAT_Grooming_Set", -1f, default, 0f, 0f);
            var props = EnsureEnvironmentMaterial(PropsMaterialPath, "MAT_Grooming_Props", 0.14f, new Color(1f, 0.97f, 0.9f), 3f, -1f);
            // Gold stays matte (GB): the gold palette and a warm rim only a little stronger than the wood's.
            var gold = EnsureEnvironmentMaterial(GoldMaterialPath, "MAT_Grooming_Gold", 0.22f, new Color(1f, 0.9f, 0.62f), 2.6f, -1f);
            // Soap bubbles and foam: the wish bubble's soft clear look in 3D (SH_SoapBubble, premultiplied alpha).
            var bubble = EnsureBubbleMaterial();
            var bodies = MeasureBodies(report);
            var prefab = BuildSetPrefab(set, props, gold, bubble, bodies, report);
            if (prefab != null)
                report.Add(EnsureAddressables(prefab));
            AssetDatabase.SaveAssets();
            return string.Join("; ", report);
        }

        // ---------------------------------------------------------------- models

        public static string ModelPath(string name) => $"{ModelsFolder}/{name}.fbx";

        /// <summary>Static prop settings (like AssetImportRules' Prop_Static), set here because the folder is the activity's.</summary>
        public static int ConfigureImporters(List<string> missing)
        {
            int count = 0;
            var names = new List<string> { SetModel, BrushModel, BurrModel, BowModel, BubbleModel, DebrisModel };
            foreach (var name in names)
            {
                string path = ModelPath(name);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    missing.Add(name);
                    continue;
                }
                bool changed = importer.globalScale != 1f || !importer.useFileScale || importer.isReadable || importer.importBlendShapes
                               || importer.importCameras || importer.importLights || importer.importAnimation || importer.addCollider
                               || importer.materialImportMode != ModelImporterMaterialImportMode.None
                               || importer.animationType != ModelImporterAnimationType.None
                               || importer.meshCompression != ModelImporterMeshCompression.Low
                               || importer.importNormals != ModelImporterNormals.Import || importer.importTangents != ModelImporterTangents.None;
                importer.globalScale = 1f;
                importer.useFileScale = true;
                importer.isReadable = false;
                importer.importBlendShapes = false;
                importer.importVisibility = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.None;
                importer.addCollider = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.meshCompression = ModelImporterMeshCompression.Low;
                if (changed)
                    importer.SaveAndReimport();
                count++;
            }
            return count;
        }

        /// <summary>The meshes of a model, by name (the Blender object names), in the model's hierarchy order.</summary>
        public static List<(Mesh mesh, Matrix4x4 matrix, string name)> ModelMeshes(string model)
        {
            var result = new List<(Mesh, Matrix4x4, string)>();
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(model));
            if (root == null)
                return result;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    result.Add((filter.sharedMesh, root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix, filter.name));
            return result;
        }

        public static Mesh FirstMesh(string model)
        {
            var meshes = ModelMeshes(model);
            return meshes.Count > 0 ? meshes[0].mesh : null;
        }

        // ---------------------------------------------------------------- palettes and materials

        public static void EnsurePalettes()
        {
            var set = AssetDatabase.LoadAssetAtPath<PaletteSetAsset>(PalettesPath);
            if (set == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PalettesPath));
                set = ScriptableObject.CreateInstance<PaletteSetAsset>();
                AssetDatabase.CreateAsset(set, PalettesPath);
            }
            set.palettes.Clear();
            // Agent GB's rows (pw_grooming.GROOMING_PALETTES): mound grass, R the knitted cushion (dusty rose), G its cream
            // stripes, B petals and clover heads, A daisy hearts; the backdrop's meadow, R sunny crests, G haze, B petals, A bark.
            Add(set, PaletteMound, "8DC166", "E3A3A9", "F3DCCB", "F5EFE2", "F2C94C", "4E6D5D", "B4D873");
            Add(set, PaletteBackdrop, "8DC166", "D5E08B", "A9C9C2", "F5EFE2", "8F6B50", "4E6D5D", "B4D873");
            // The brush: wood, R bristles, G the inlaid oval; the golden one tints the wood and the inlay only.
            Add(set, PaletteWood, "C69467", "EBDCBF", "E3C08F", "A87850", "C69467", "6F5A57", "FFFFFF");
            Add(set, PaletteGold, "E2B24A", "EBDCBF", "F7DC8E", "C2902F", "E2B24A", "8A6A4A", "FFFFFF");
            // Every debris kind in one row: leaf green, R light green, G petal cream, B bark, A warm yellow.
            Add(set, PaletteDebris, "86BD5E", "C3DF8A", "F6EFE0", "9C7352", "F0CB5A", "4E6650", "FFFFFF");
            Add(set, PaletteBurr, "8A7B48", "B8A764", "8A7B48", "8A7B48", "8A7B48", "4E4733", "FFFFFF");
            // Bows (GroomingBow order): the ribbon in base and R, the knot a shade darker in G.
            Add(set, BowPalettes[0], "F2AAC1", "F2AAC1", "DE8CA9", "F2AAC1", "F2AAC1", "9A6A88", "FFFFFF");
            Add(set, BowPalettes[1], "9EC8EE", "9EC8EE", "7FAEDD", "9EC8EE", "9EC8EE", "5F7AA0", "FFFFFF");
            Add(set, BowPalettes[2], "F5E7CA", "F5E7CA", "E5CFA6", "F5E7CA", "F5E7CA", "9C8A78", "FFFFFF");
            EditorUtility.SetDirty(set);

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            if (database != null && !database.paletteSets.Contains(set))
            {
                database.paletteSets.Add(set);
                EditorUtility.SetDirty(database);
            }
        }

        private static void Add(PaletteSetAsset set, string id, params string[] hex)
        {
            set.palettes.Add(new PaletteSetAsset.Entry
            {
                sourceId = id,
                baseColor = Hex(hex[0]),
                regionR = Hex(hex[1]),
                regionG = Hex(hex[2]),
                regionB = Hex(hex[3]),
                regionA = Hex(hex[4]),
                occlusion = Hex(hex[5]),
                accent = Hex(hex[6]),
            });
        }

        public static uint PaletteId(string id) => ContentId.FromSource(id).Value;

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.magenta;

        /// <summary>An SH_EnvironmentOpaque material with MAT_Environment_Common's values; a negative rim or wrap keeps them.</summary>
        private static Material EnsureEnvironmentMaterial(string path, string name, float rim, Color rimColor, float rimPower, float wrap)
        {
            var shader = Shader.Find(EnvironmentShader) ?? throw new InvalidOperationException("Shader not found: " + EnvironmentShader);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            var common = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentCommonPath);
            if (common != null && common.shader == shader)
                material.CopyPropertiesFromMaterial(common);
            if (rim >= 0f)
            {
                material.SetFloat("_RimStrength", rim);
                material.SetColor("_RimColor", rimColor);
                material.SetFloat("_RimPower", rimPower);
            }
            if (wrap >= 0f)
                material.SetFloat("_WrapDiffuse", wrap);
            // SRP Batcher; palettes travel in the Shader User Value.
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureBubbleMaterial()
        {
            var shader = Shader.Find(BubbleShader) ?? throw new InvalidOperationException("Shader not found: " + BubbleShader
                + " (Assets/_Game/Art/Shaders/SH_SoapBubble.shader).");
            var material = AssetDatabase.LoadAssetAtPath<Material>(BubbleMaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BubbleMaterialPath));
                material = new Material(shader) { name = "MAT_Grooming_Bubble" };
                AssetDatabase.CreateAsset(material, BubbleMaterialPath);
            }
            material.shader = shader;
            material.renderQueue = (int)RenderQueue.Transparent;
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------------------------------------------------------------- the coats (zones measured on the models)

        /// <summary>Every species of the visual catalog: its zones, its groom mesh, its bow place.</summary>
        public static GroomingBodyCatalog MeasureBodies(List<string> report)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var bodies = LoadOrCreate<GroomingBodyCatalog>(BodiesPath);
            var list = new List<GroomingBody>();
            var bowMesh = FirstMesh(BowModel);
            var lines = new List<string>();
            if (catalog != null)
            {
                foreach (var entry in catalog.entries)
                {
                    if (entry == null || entry.prefab == null)
                        continue;
                    var family = RigFamily.SmallQuadruped;
                    var species = database != null ? database.species.FirstOrDefault(s => s != null && s.visualKey == entry.visualKey) : null;
                    if (species != null)
                        family = species.rigFamily;
                    var body = MeasureBody(entry.visualKey, entry.prefab, GroomingLayout.ZoneCount(family), bowMesh);
                    if (body == null)
                        continue;
                    list.Add(body);
                    lines.Add($"{entry.visualKey} {body.zoneCount} zones (radius {body.zoneRadii.Average() * 100f:F1} cm), feet {body.footRadius * 100f:F1} cm, " +
                              $"bow lift {body.bowLift * 1000f:F0} mm × {body.bowScale:F2}" + (body.HasSkin ? "" : ", rigid (no bones)"));
                }
            }
            bodies.bodies = list.ToArray();
            EditorUtility.SetDirty(bodies);
            report.Add("coats: " + (lines.Count > 0 ? string.Join(", ", lines) : "none (no visual catalog)"));
            return bodies;
        }

        /// <summary>
        /// One species: its LOD0 at bind pose in the grooming view, the coat the camera sees (no eyes, nose, paws or inner
        /// ears; nothing hidden behind the body), N zones by farthest-point sampling, each skinned to its vertex's bones; every
        /// vertex's nearest zone written into the groom mesh (uv2) with its bind position (uv3); the bow's place on SOCK_HeadTop.
        /// </summary>
        public static GroomingBody MeasureBody(string visualKey, AnimalView prefab, int zoneCount, Mesh bowMesh)
        {
            var instance = Object.Instantiate(prefab.gameObject);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                var view = instance.GetComponent<AnimalView>();
                var animator = view.Animator != null ? view.Animator : instance.GetComponentInChildren<Animator>(true);
                if (animator != null)
                    AnimatorUtility.DeoptimizeTransformHierarchy(animator.gameObject);
                var skin = Lod0(instance);
                if (skin == null || skin.sharedMesh == null)
                    return null;
                var mesh = skin.sharedMesh;
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var colors = mesh.colors;
                var weights = mesh.boneWeights;
                var bindposes = mesh.bindposes;
                var triangles = mesh.triangles;
                var bones = skin.bones;
                bool skinned = bones != null && bones.Length == bindposes.Length && bones.Length > 0 && bones.All(b => b != null)
                               && weights.Length == vertices.Length;
                int n = vertices.Length;
                var points = new Vector3[n];
                var pointNormals = new Vector3[n];
                var dominant = new int[n];
                var skinMatrix = skin.transform.localToWorldMatrix;
                var boneMatrices = skinned ? bones.Select((b, i) => b.localToWorldMatrix * bindposes[i]).ToArray() : null;
                for (int v = 0; v < n; v++)
                {
                    if (skinned)
                    {
                        var w = weights[v];
                        points[v] = Blend(boneMatrices, w, vertices[v], true);
                        pointNormals[v] = Blend(boneMatrices, w, normals.Length > v ? normals[v] : Vector3.up, false).normalized;
                        dominant[v] = w.boneIndex0;
                    }
                    else
                    {
                        points[v] = skinMatrix.MultiplyPoint3x4(vertices[v]);
                        pointNormals[v] = skinMatrix.MultiplyVector(normals.Length > v ? normals[v] : Vector3.up).normalized;
                        dominant[v] = -1;
                    }
                }

                // The coat the camera sees in the grooming pose.
                var toCamera = GroomingLayout.ToCameraLocal;
                var candidates = new List<int>();
                for (int v = 0; v < n; v++)
                {
                    if (Vector3.Dot(pointNormals[v], toCamera) < 0.2f)
                        continue;
                    var c = colors.Length > v ? colors[v] : new Color(0f, 0f, 0f, 0f);
                    if (c.a > 0.3f || c.b > 0.5f)
                        continue;
                    if (c.r > 0.5f && skinned && bones[dominant[v]].name.StartsWith("Ear", StringComparison.Ordinal))
                        continue;
                    if (Occluded(points, triangles, v, points[v] + pointNormals[v] * 0.003f, toCamera))
                        continue;
                    candidates.Add(v);
                }
                if (candidates.Count == 0)
                    return null;

                // Farthest-point sampling, from the candidate nearest the middle of the visible coat.
                int count = Mathf.Min(zoneCount, Mathf.Min(candidates.Count, GroomingLayout.MaxZones));
                var middle = Vector3.zero;
                foreach (int v in candidates)
                    middle += points[v];
                middle /= candidates.Count;
                int first = candidates.OrderBy(v => (points[v] - middle).sqrMagnitude).First();
                var chosen = new List<int> { first };
                var nearest = candidates.Select(v => (points[v] - points[first]).sqrMagnitude).ToArray();
                while (chosen.Count < count)
                {
                    int best = 0;
                    for (int i = 1; i < nearest.Length; i++)
                        if (nearest[i] > nearest[best])
                            best = i;
                    int pick = candidates[best];
                    chosen.Add(pick);
                    for (int i = 0; i < nearest.Length; i++)
                        nearest[i] = Mathf.Min(nearest[i], (points[candidates[i]] - points[pick]).sqrMagnitude);
                }

                var body = new GroomingBody { visualKey = visualKey, zoneCount = count };
                body.zonePoints = chosen.Select(v => points[v]).ToArray();
                body.zoneNormals = chosen.Select(v => pointNormals[v]).ToArray();
                var neighbor = new float[count];
                for (int a = 0; a < count; a++)
                {
                    float best = float.MaxValue;
                    for (int b = 0; b < count; b++)
                        if (a != b)
                            best = Mathf.Min(best, (body.zonePoints[a] - body.zonePoints[b]).magnitude);
                    neighbor[a] = best < float.MaxValue ? best : 0.05f;
                }
                float mean = neighbor.Average();
                body.zoneRadii = neighbor.Select(d => 0.6f * Mathf.Clamp(d, 0.75f * mean, 1.35f * mean)).ToArray();
                // Where a prop could seem to float carries none: an ear, a paw, a zone seen edge-on at the silhouette (a burr on
                // an ear's tip or the tail's rim hangs in the air), or a coat thinner than a burr (the hedgehog's spines).
                body.zoneThin = chosen.Select(v =>
                {
                    string bone = skinned ? bones[dominant[v]].name : string.Empty;
                    bool edge = Vector3.Dot(pointNormals[v], toCamera) < EdgeOn;
                    bool part = bone.StartsWith("Ear", StringComparison.Ordinal) || bone.Contains("Paw");
                    return part || edge || Thickness(points, triangles, v, pointNormals[v]) < ThinCoat;
                }).ToArray();

                // Each zone skinned like its vertex (up to 4 bones), in each bone's bind space.
                body.zoneBones = new int[count * GroomingBody.Influences];
                body.zoneWeights = new float[count * GroomingBody.Influences];
                body.zoneLocalPoints = new Vector3[count * GroomingBody.Influences];
                body.zoneLocalNormals = new Vector3[count * GroomingBody.Influences];
                for (int z = 0; z < count; z++)
                {
                    int v = chosen[z];
                    for (int k = 0; k < GroomingBody.Influences; k++)
                    {
                        int slot = z * GroomingBody.Influences + k;
                        body.zoneBones[slot] = -1;
                        if (!skinned)
                            continue;
                        var w = weights[v];
                        int bone = k == 0 ? w.boneIndex0 : k == 1 ? w.boneIndex1 : k == 2 ? w.boneIndex2 : w.boneIndex3;
                        float weight = k == 0 ? w.weight0 : k == 1 ? w.weight1 : k == 2 ? w.weight2 : w.weight3;
                        if (weight <= 0f)
                            continue;
                        body.zoneBones[slot] = bone;
                        body.zoneWeights[slot] = weight;
                        body.zoneLocalPoints[slot] = bindposes[bone].MultiplyPoint3x4(vertices[v]);
                        body.zoneLocalNormals[slot] = bindposes[bone].MultiplyVector(normals.Length > v ? normals[v] : Vector3.up);
                    }
                }
                body.boneNames = skinned ? bones.Select(b => b.name).ToArray() : Array.Empty<string>();

                // Bounds (the framing) and the paws' reach (the cushion).
                var bounds = new Bounds(points[0], Vector3.zero);
                float feet = 0f;
                var paws = new List<Vector2>();
                for (int v = 0; v < n; v++)
                {
                    bounds.Encapsulate(points[v]);
                    if (points[v].y < 0.04f)
                    {
                        var p = new Vector2(points[v].x, points[v].z);
                        feet = Mathf.Max(feet, p.magnitude);
                        paws.Add(p);
                    }
                }
                body.bounds = bounds;
                body.footRadius = feet > 0f ? feet : Mathf.Max(bounds.extents.x, bounds.extents.z);
                body.footPoints = Hull(paws);

                body.groomMesh = SaveGroomMesh(visualKey, mesh, points, pointNormals, body);
                MeasureBow(instance.transform, points, skinned ? dominant.Select(d => bones[d].name).ToArray() : null, bowMesh, body);
                return body;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>Convex hull of points (monotone chain), counter-clockwise.</summary>
        public static Vector2[] Hull(List<Vector2> points)
        {
            var sorted = points.Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            if (sorted.Count < 3)
                return sorted.ToArray();
            float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            var hull = new List<Vector2>();
            foreach (var p in sorted)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            int lower = hull.Count + 1;
            for (int i = sorted.Count - 2; i >= 0; i--)
            {
                var p = sorted[i];
                while (hull.Count >= lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
            return hull.ToArray();
        }

        private static Vector3 Blend(Matrix4x4[] matrices, BoneWeight w, Vector3 v, bool point)
        {
            Vector3 Apply(int bone, float weight) => weight <= 0f ? Vector3.zero
                : weight * (point ? matrices[bone].MultiplyPoint3x4(v) : matrices[bone].MultiplyVector(v));
            return Apply(w.boneIndex0, w.weight0) + Apply(w.boneIndex1, w.weight1) + Apply(w.boneIndex2, w.weight2) + Apply(w.boneIndex3, w.weight3);
        }

        private static SkinnedMeshRenderer Lod0(GameObject root)
        {
            var group = root.GetComponentInChildren<LODGroup>(true);
            if (group != null && group.lodCount > 0)
                foreach (var r in group.GetLODs()[0].renderers)
                    if (r is SkinnedMeshRenderer skin)
                        return skin;
            return root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(r => r.name, StringComparer.Ordinal).FirstOrDefault();
        }

        /// <summary>Under this thickness (m) a zone carries no prop: about a burr's width, so nothing hangs over an edge.</summary>
        public const float ThinCoat = 0.045f;

        /// <summary>A zone whose normal turns this far from the camera (cosine) is seen edge-on: no prop on the silhouette.</summary>
        public const float EdgeOn = 0.5f;

        /// <summary>The body's thickness under a vertex: the distance from it, inward along its normal, to the far side.</summary>
        private static float Thickness(Vector3[] points, int[] triangles, int vertex, Vector3 normal)
        {
            var origin = points[vertex] - normal * 0.001f;
            var direction = -normal;
            float nearest = float.MaxValue;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (a == vertex || b == vertex || c == vertex)
                    continue;
                if (RayTriangle(origin, direction, points[a], points[b], points[c], out float distance) && distance > 0.0015f && distance < nearest)
                    nearest = distance;
            }
            return nearest;
        }

        /// <summary>A ray from the vertex toward the camera meets the body (a far leg behind the near one, the far cheek).</summary>
        private static bool Occluded(Vector3[] points, int[] triangles, int vertex, Vector3 origin, Vector3 direction)
        {
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (a == vertex || b == vertex || c == vertex)
                    continue;
                if (RayTriangle(origin, direction, points[a], points[b], points[c], out float distance) && distance > 0.0015f)
                    return true;
            }
            return false;
        }

        private static bool RayTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0f;
            var e1 = b - a;
            var e2 = c - a;
            var p = Vector3.Cross(direction, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-9f)
                return false;
            float inv = 1f / det;
            var s = origin - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f)
                return false;
            var q = Vector3.Cross(s, e1);
            float w = Vector3.Dot(direction, q) * inv;
            if (w < 0f || u + w > 1f)
                return false;
            distance = Vector3.Dot(e2, q) * inv;
            return distance > 0f;
        }

        /// <summary>The species' LOD0 copy carrying each vertex's nearest zone (uv2.x) and its bind position (uv3), saved once.</summary>
        private static Mesh SaveGroomMesh(string visualKey, Mesh source, Vector3[] points, Vector3[] normals, GroomingBody body)
        {
            int n = source.vertexCount;
            var vertices = source.vertices;
            var zones = new Vector2[n];
            for (int v = 0; v < n; v++)
            {
                int best = 0;
                float bestScore = float.MaxValue;
                for (int z = 0; z < body.zoneCount; z++)
                {
                    // Nearest zone, a little against the grain of the surface: a thin ear's back never takes its front's zone.
                    float score = (points[v] - body.zonePoints[z]).sqrMagnitude * (1.6f - 0.6f * Vector3.Dot(normals[v], body.zoneNormals[z]));
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = z;
                    }
                }
                zones[v] = new Vector2(best, 0f);
            }
            var copy = Object.Instantiate(source);
            copy.name = "ME_Groom_" + Short(visualKey);
            copy.SetUVs(2, zones);
            copy.SetUVs(3, vertices);
            Directory.CreateDirectory(BodiesFolder);
            string path = $"{BodiesFolder}/ME_Groom_{Short(visualKey)}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Mesh saved;
            if (existing != null)
            {
                EditorUtility.CopySerialized(copy, existing);
                existing.name = copy.name;
                Object.DestroyImmediate(copy);
                saved = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(copy, path);
                saved = copy;
            }
            // GPU only in the game, like the animals' own meshes (no CPU copy kept).
            var so = new SerializedObject(saved);
            var readable = so.FindProperty("m_IsReadable");
            if (readable != null)
            {
                readable.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(saved);
            return saved;
        }

        private static string Short(string visualKey) => visualKey.StartsWith("PF_Animal_", StringComparison.Ordinal) ? visualKey.Substring(10) : visualKey;

        /// <summary>
        /// The bow on SOCK_HeadTop (collision rule): the biggest size, from GB's own (1) down to 60 %, whose footprint rests on
        /// the fur under its knot without reaching an ear; it is lifted above every vertex under its footprint (head, spines,
        /// ears) plus 3 mm, so nothing of the animal is inside it at rest.
        /// </summary>
        private static void MeasureBow(Transform root, Vector3[] points, string[] dominantBones, Mesh bowMesh, GroomingBody body)
        {
            body.bowScale = 1f;
            body.bowLift = 0.004f;
            var socket = FindDeep(root, "SOCK_HeadTop");
            if (socket == null || bowMesh == null)
                return;
            var toSocket = socket.worldToLocalMatrix;
            var local = points.Select(p => toSocket.MultiplyPoint3x4(p)).ToArray();
            var bounds = bowMesh.bounds;
            float width = Mathf.Max(0.001f, bounds.size.x);
            float depth = Mathf.Max(0.001f, bounds.size.z);
            const float Margin = 0.004f;
            // The fur right under the knot (a hedgehog's spines rise above its socket): the bow rests there.
            float under = float.MinValue;
            for (int v = 0; v < local.Length; v++)
                if (Mathf.Abs(local[v].x) <= 0.008f && Mathf.Abs(local[v].z) <= 0.008f)
                    under = Mathf.Max(under, local[v].y);
            if (under == float.MinValue)
                under = 0f;
            // GB drew the bow for these heads (6.2 cm); it shrinks (to 60 %) only when its footprint would reach an ear.
            float bestScale = 0.6f, bestLift = under;
            for (float scale = 1f; scale >= 0.6f - 1e-4f; scale -= 0.05f)
            {
                float halfX = width * scale * 0.5f + Margin;
                float halfZ = depth * scale * 0.5f + Margin;
                float lift = under;
                for (int v = 0; v < local.Length; v++)
                    if (Mathf.Abs(local[v].x) <= halfX && Mathf.Abs(local[v].z) <= halfZ)
                        lift = Mathf.Max(lift, local[v].y);
                bestScale = scale;
                bestLift = lift;
                // Resting on the fur under it, not perched on an ear: taken.
                if (lift - under <= 0.008f)
                    break;
            }
            body.bowScale = bestScale;
            body.bowLift = Mathf.Max(0f, bestLift) + 0.003f;
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

        // ---------------------------------------------------------------- the set prefab

        public static GameObject BuildSetPrefab(Material setMaterial, Material props, Material gold, Material bubble, GroomingBodyCatalog bodies,
            List<string> report)
        {
            var root = new GameObject(GroomingLayout.SetPrefabName);
            try
            {
                var view = root.AddComponent<GroomingSetView>();
                var data = new GroomingSetData { bodies = bodies };
                var bindings = new List<WorldChunk.PaletteBinding>();

                // The mound, the cushion and the backdrop, as GB placed them (the cushion's top at the origin). GB's camera side
                // is Unity +Z (its hills arc around -Z); ours looks along +Z from -Z: the set turns half a turn (the mound is round).
                var setRoot = new GameObject("Set").transform;
                setRoot.SetParent(root.transform, false);
                setRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
                float topRadius = 0f;
                var setMeshes = ModelMeshes(SetModel);
                foreach (var (mesh, matrix, name) in setMeshes)
                {
                    var renderer = AddRenderer(name, setRoot, mesh, setMaterial, ShadowCastingMode.Off, true);
                    renderer.transform.localPosition = matrix.GetColumn(3);
                    renderer.transform.localRotation = matrix.rotation;
                    renderer.transform.localScale = matrix.lossyScale;
                    bool mound = name.IndexOf("Mound", StringComparison.OrdinalIgnoreCase) >= 0;
                    bindings.Add(new WorldChunk.PaletteBinding
                    {
                        renderer = renderer,
                        paletteId = PaletteId(mound ? PaletteMound : PaletteBackdrop),
                        brightness = WorldChunk.NeutralBrightness,
                    });
                    if (mound)
                        topRadius = Mathf.Max(topRadius, MeasureTop(mesh, matrix));
                }
                if (setMeshes.Count == 0)
                    report.Add("WARNING: " + SetModel + ".fbx missing: the set has no mound nor backdrop yet");
                data.moundTopRadius = topRadius > 0f ? topRadius : 0.45f;
                data.bindings = bindings.ToArray();
                report.Add($"cushion top radius {data.moundTopRadius * 100f:F1} cm" + (topRadius > 0f ? "" : " (default)") + SwayReport(bodies, data.moundTopRadius)
                           + BackdropReport(setMeshes, data.moundTopRadius));

                var anchor = new GameObject("AnimalAnchor").transform;
                anchor.SetParent(root.transform, false);
                data.animalAnchor = anchor;

                data.camera = AddCamera(root.transform);
                data.light = AddLight(root.transform, data);

                // Pooled props, built once (doc Mini-jeux section 1: nothing instantiated during a game).
                var propsRoot = new GameObject("Props").transform;
                propsRoot.SetParent(root.transform, false);
                data.props = propsRoot;
                var brushMesh = FirstMesh(BrushModel);
                data.brush = AddRenderer("Brush", propsRoot, brushMesh, props, ShadowCastingMode.Off, false);
                data.brushMaterial = props;
                data.goldMaterial = gold;
                data.brushPalette = PaletteId(PaletteWood);
                data.goldPalette = PaletteId(PaletteGold);
                // GR_Brush's pivot is the center of its bristle tips: it glides this little gap above the fur.
                data.brushHover = 0.006f;
                data.goldenBrush = AddRenderer("GoldenBrush", propsRoot, brushMesh, gold, ShadowCastingMode.Off, false);
                var bowMesh = FirstMesh(BowModel);
                data.bow = AddRenderer("Bow", propsRoot, bowMesh, props, ShadowCastingMode.Off, false);
                data.bowPalettes = BowPalettes.Select(PaletteId).ToArray();
                data.bowWidth = bowMesh != null ? bowMesh.bounds.size.x : 0.08f;
                var bubbleMesh = FirstMesh(BubbleModel);
                data.bubbleRadius = bubbleMesh != null ? Mathf.Max(0.0001f, bubbleMesh.bounds.extents.x) : 0.5f;
                data.bubbles = new MeshRenderer[GroomingLayout.MaxBubbles];
                for (int i = 0; i < data.bubbles.Length; i++)
                    data.bubbles[i] = AddRenderer("Bubble_" + i, propsRoot, bubbleMesh, bubble, ShadowCastingMode.Off, false);
                data.bubbleTint = 0u;
                data.foam = new MeshRenderer[GroomingLayout.FoamCount];
                for (int i = 0; i < data.foam.Length; i++)
                    data.foam[i] = AddRenderer("Foam_" + i.ToString("00"), propsRoot, bubbleMesh, bubble, ShadowCastingMode.Off, false);
                data.foamTints = FoamTints.ToArray();

                // Debris and burrs share the slots: a slot takes its mesh and palette when the game lays it.
                var burrMesh = FirstMesh(BurrModel);
                data.burrMesh = burrMesh;
                data.burrPalette = PaletteId(PaletteBurr);
                data.burrLift = burrMesh != null ? Sagitta(burrMesh) : 0.004f;
                var theme = new GroomingTheme { biome = "biome.grassland" };
                var themeMeshes = new List<Mesh>();
                var themePalettes = new List<uint>();
                var themeLift = new List<float>();
                var debrisMeshes = ModelMeshes(DebrisModel);
                foreach (var name in PrairieDebris)
                {
                    var mesh = debrisMeshes.Where(m => m.name == name || m.mesh.name == name).Select(m => m.mesh).FirstOrDefault();
                    if (mesh == null)
                        continue;
                    themeMeshes.Add(mesh);
                    themePalettes.Add(PaletteId(PaletteDebris));
                    themeLift.Add(Sagitta(mesh));
                }
                theme.debrisMeshes = themeMeshes.ToArray();
                theme.debrisPalettes = themePalettes.ToArray();
                theme.debrisLift = themeLift.ToArray();
                data.themes = new[] { theme };
                data.debris = new MeshRenderer[GroomingLayout.MaxDebris];
                for (int i = 0; i < data.debris.Length; i++)
                    data.debris[i] = AddRenderer("Debris_" + i.ToString("00"), propsRoot, themeMeshes.Count > 0 ? themeMeshes[0] : burrMesh, props,
                        ShadowCastingMode.Off, false);

                // H-VFX-ACT-001-03: round glints (the feedback atlas' round glint tile; never a four-point sparkle).
                var fx = new GameObject("Fx").transform;
                fx.SetParent(root.transform, false);
                data.glints = new ParticleSystem[GroomingLayout.GlintSystems];
                var vfxMaterial = AssetDatabase.LoadAssetAtPath<Material>(VfxPipeline.MaterialPath);
                for (int i = 0; i < data.glints.Length; i++)
                    data.glints[i] = AddGlints(fx, "Glints_" + i, vfxMaterial);

                // The bow and golden brush icons are borrowed from the HUD at run time (no UI atlas in this bundle).
                if (Icon("ICO_Bow_Pink") == null || Icon("ICO_Bow_SkyBlue") == null || Icon("ICO_Bow_Cream") == null || Icon("ICO_GoldenBrush") == null)
                    report.Add("WARNING: bow or golden brush icons missing in " + IconsFolder);
                ApplyLightingData(data);

                var missing = new List<string>();
                if (brushMesh == null) missing.Add(BrushModel);
                if (bowMesh == null) missing.Add(BowModel);
                if (bubbleMesh == null) missing.Add(BubbleModel);
                if (burrMesh == null) missing.Add(BurrModel);
                if (themeMeshes.Count == 0) missing.Add("Prairie debris");
                if (missing.Count > 0)
                    report.Add("WARNING: props without a model (hidden in game): " + string.Join(", ", missing));

                view.SetAuthoring(data);
                foreach (Transform child in propsRoot)
                    child.gameObject.SetActive(false);
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success)
                    throw new InvalidOperationException("Could not save " + PrefabPath);
                report.Add($"{PrefabPath}: {CountRenderers(saved)} renderers, set triangles {CountTriangles(setMeshes)}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Lying flat (pivot at its contact point) on a coat curved like a small animal's, a prop's ends would dip into the
        /// fur by its sagitta: it is lifted by that (plus anything under its pivot).
        /// </summary>
        public static float Sagitta(Mesh mesh)
        {
            var b = mesh.bounds;
            float half = 0.5f * Mathf.Max(b.size.x, b.size.z);
            return Mathf.Max(0f, -b.min.y) + half * half / (2f * CoatCurvature);
        }

        /// <summary>The cushion's flat top: the farthest point (from the axis) of the faces at the origin's height looking up.</summary>
        public static float MeasureTop(Mesh mesh, Matrix4x4 matrix)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            float radius = 0f;
            float above = float.MinValue;
            for (int v = 0; v < vertices.Length; v++)
            {
                var p = matrix.MultiplyPoint3x4(vertices[v]);
                var nn = normals.Length > v ? matrix.MultiplyVector(normals[v]).normalized : Vector3.up;
                if (Mathf.Abs(p.y) <= 0.0015f && nn.y > 0.95f)
                    radius = Mathf.Max(radius, new Vector2(p.x, p.z).magnitude);
            }
            // Nothing of the mound may rise above the top inside it (the paws stand on it, never in it).
            for (int v = 0; v < vertices.Length; v++)
            {
                var p = matrix.MultiplyPoint3x4(vertices[v]);
                if (new Vector2(p.x, p.z).magnitude < radius)
                    above = Mathf.Max(above, p.y);
            }
            if (above > 0.008f)
                Debug.LogWarning($"[GroomingPipeline] the mound rises {above * 1000f:F0} mm above its top inside it: check GR_SetMound.");
            return radius;
        }

        private static string SwayReport(GroomingBodyCatalog bodies, float top)
        {
            if (bodies == null || bodies.bodies == null || bodies.bodies.Length == 0)
                return "";
            var parts = new List<string>();
            var yaw = GroomingLayout.AnimalRotation;
            foreach (var body in bodies.bodies)
            {
                // As the stage holds it (scale 1): every paw point stays within the flat top less 1 cm.
                float radius = top - 0.01f;
                float sway = float.MaxValue;
                foreach (var point in body.footPoints)
                {
                    var q = yaw * new Vector3(point.x, 0f, point.y);
                    float room = radius * radius - q.z * q.z;
                    sway = room <= 0f ? -1f : Mathf.Min(sway, Mathf.Min(Mathf.Sqrt(room) - q.x, Mathf.Sqrt(room) + q.x));
                }
                if (sway == float.MaxValue)
                    sway = radius - body.footRadius;
                parts.Add($"{Short(body.visualKey)} sway ±{Mathf.Max(0f, sway) * 100f:F1} cm" + (sway < 0f ? " (PAWS OFF THE CUSHION)" : ""));
            }
            return " (" + string.Join(", ", parts) + ")";
        }

        private static string BackdropReport(List<(Mesh mesh, Matrix4x4 matrix, string name)> meshes, float top)
        {
            float nearest = float.MaxValue;
            foreach (var (mesh, matrix, name) in meshes)
            {
                if (name.IndexOf("Mound", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                foreach (var v in mesh.vertices)
                {
                    var p = matrix.MultiplyPoint3x4(v);
                    if (p.y < 1.2f)
                        nearest = Mathf.Min(nearest, new Vector2(p.x, p.z).magnitude);
                }
            }
            if (nearest == float.MaxValue)
                return "";
            return $", backdrop {nearest * 100f:F0} cm from the cushion's center" + (nearest < top + 0.2f ? " (TOO CLOSE)" : "");
        }

        private static MeshRenderer AddRenderer(string name, Transform parent, Mesh mesh, Material material, ShadowCastingMode shadows, bool receive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
            renderer.receiveShadows = receive;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
            return renderer;
        }

        private static Camera AddCamera(Transform parent)
        {
            var go = new GameObject("Camera");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = GroomingLayout.CameraRotation;
            go.transform.localPosition = new Vector3(0f, 0.6f, -2.4f);
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = GroomingLayout.CameraFov;
            camera.nearClipPlane = GroomingLayout.CameraNear;
            camera.farClipPlane = GroomingLayout.CameraFar;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = true;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            return camera;
        }

        /// <summary>The set's sun: the Prairie day's color a touch warmer, from behind the camera on its right (the face lit).</summary>
        private static Light AddLight(Transform parent, GroomingSetData data)
        {
            var profile = AssetDatabase.LoadAssetAtPath<LightingProfile>("Assets/_Game/Art/Lighting/Grassland/LP_Grassland_Day.asset");
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(44f, -38f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            var sun = profile != null ? profile.sunColor : new Color(1f, 0.945f, 0.863f);
            light.color = Color.Lerp(sun, new Color(1f, 0.88f, 0.72f), 0.25f);
            light.intensity = (profile != null ? profile.sunIntensity : 1f) * 1.05f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = profile != null ? Mathf.Min(0.6f, profile.shadowStrength) : 0.55f;
            light.enabled = false;
            return light;
        }

        /// <summary>The warm Prairie day of the set (doc 01: « ciel chaud »): the profile's ambient, a peach horizon.</summary>
        private static void ApplyLightingData(GroomingSetData data)
        {
            var profile = AssetDatabase.LoadAssetAtPath<LightingProfile>("Assets/_Game/Art/Lighting/Grassland/LP_Grassland_Day.asset");
            var warm = new Color(1f, 0.9f, 0.76f);
            if (profile != null)
            {
                data.ambientTop = Color.Lerp(profile.ambientTop, warm, 0.12f);
                data.ambientEquator = Color.Lerp(profile.ambientEquator, warm, 0.22f);
                data.ambientBottom = Color.Lerp(profile.ambientBottom, warm, 0.1f);
                data.skyTop = Color.Lerp(profile.skyTop, new Color(0.62f, 0.78f, 0.95f), 0.5f);
                data.cloudLight = profile.cloudLight;
            }
            data.skyHorizon = new Color(1f, 0.89f, 0.77f);
            data.skyBottom = new Color(0.95f, 0.87f, 0.76f);
            data.sunGlowTint = new Color(1f, 0.86f, 0.64f);
            data.sunGlow = 0.38f;
            data.cloudShade = new Color(0.9f, 0.83f, 0.85f);
            data.cloudCoverage = 0.3f;
        }

        private static ParticleSystem AddGlints(Transform parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.022f, 0.036f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.8f, 1f), new Color(1f, 0.84f, 0.5f, 1f));
            main.gravityModifier = -0.02f;
            main.maxParticles = 6;
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1), new ParticleSystem.Burst(0.07f, 1), new ParticleSystem.Burst(0.14f, 1) });
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.022f;
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = VfxPipeline.TileCount;
            sheet.numTilesY = 1;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve((VfxPipeline.TileGlint + 0.25f) / VfxPipeline.TileCount);
            sheet.cycleCount = 1;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.8f, 0.55f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.25f, 1.1f), new Keyframe(1f, 0.6f)));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.maxParticleSize = 0.08f;
            return ps;
        }

        private static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsFolder}/{name}.png");

        private static int CountRenderers(GameObject prefab) => prefab.GetComponentsInChildren<Renderer>(true).Length;

        private static int CountTriangles(List<(Mesh mesh, Matrix4x4 matrix, string name)> meshes)
        {
            int count = 0;
            foreach (var (mesh, _, _) in meshes)
                for (int s = 0; s < mesh.subMeshCount; s++)
                    count += (int)mesh.GetIndexCount(s) / 3;
            return count;
        }

        // ---------------------------------------------------------------- Addressables

        /// <summary>Group Activity_Grooming (doc Mini-jeux, section 1): the set and what only it uses, loaded at the first game.</summary>
        public static string EnsureAddressables(GameObject prefab)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(GroomingLayout.Group) ?? settings.CreateGroup(GroomingLayout.Group, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)), group, false, false);
            entry.address = GroomingLayout.SetAddress;
            // The feedback VFX material (its atlas and shader) is shared with the animals' bundle: one explicit entry there,
            // so this bundle depends on it instead of copying it (CommonContentTests: nothing duplicated between bundles).
            var common = settings.FindGroup("CommonAnimals");
            var vfxGuid = AssetDatabase.AssetPathToGUID(VfxPipeline.MaterialPath);
            if (common != null && !string.IsNullOrEmpty(vfxGuid))
            {
                var vfx = settings.CreateOrMoveEntry(vfxGuid, common, false, false);
                vfx.address = "vfx.additive_lite";
                EditorUtility.SetDirty(common);
            }
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            return $"Addressables {GroomingLayout.Group}: {GroomingLayout.SetAddress}";
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
