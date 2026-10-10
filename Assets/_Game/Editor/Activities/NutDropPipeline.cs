using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Vfx;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
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
    /// T40 « Pluie de noisettes » set pipeline (doc 01 Contenu S-ACT-003; doc « Mini-jeux &amp; activités », sections 1 and
    /// 6; collision rule): the import settings of agent NB's models (Art/Activities/NutDrop: ND_Props.fbx with the basket and
    /// the falling items, ND_HazelTree.fbx with the canopy and the trunk), NB's palette rows and the set's materials, the
    /// basket measured (its rim, its foot, the inside of its weave), the seat of the basket on every species (the bind pose:
    /// its underside over the head, the ears and the tail within its footprint, at its widest), the spawn line measured under
    /// the canopy, the set prefab PF_Activity_NutDrop_Set (T38's backdrop GR_SetBackdrop, the Prairie kit's grass and
    /// flowers, the hazel tree, the pooled items, leaves and effects, its camera and warm sun) and the Addressables group
    /// Activity_NutDrop. Missing models are listed and skipped, never invented. Idempotent. Run after the animals (Measure
    /// Bodies), the VFX (the shared particle material), the Prairie kit and Build Grooming (T38).
    /// </summary>
    public static class NutDropPipeline
    {
        public const string ModelsFolder = "Assets/_Game/Art/Activities/NutDrop";
        public const string PrefabPath = "Assets/_Game/Prefabs/Activities/" + NutDropLayout.SetPrefabName + ".prefab";
        public const string PalettesPath = "Assets/_Game/Content/Palettes/DEF_Palettes_NutDrop.asset";
        public const string SetMaterialPath = "Assets/_Game/Art/Materials/MAT_NutDrop_Set.mat";
        public const string PropsMaterialPath = "Assets/_Game/Art/Materials/MAT_NutDrop_Props.mat";
        public const string GoldMaterialPath = "Assets/_Game/Art/Materials/MAT_NutDrop_Gold.mat";
        public const string KitFolder = "Assets/_Game/Art/Environment/Grassland/Baked/Kit";
        public const string IconsFolder = "Assets/_Game/Art/UI/Icons";

        /// <summary>T38's set model (its backdrop is shared): an explicit entry of this group, so both sets depend on it, neither copies it.</summary>
        public const string SharedGroup = "Activity_Shared";

        public const string SharedSetAddress = "activity.shared.gr_set_prairie";

        public const string PropsModel = "ND_Props";
        public const string TreeModel = "ND_HazelTree";
        public const string Basket = "ND_Basket";
        public const string Canopy = "ND_HazelCanopy";
        public const string Trunk = "ND_HazelTrunk";
        public const string BackdropName = "GR_SetBackdrop";

        /// <summary>The mesh of each NutDropItemKind (index = kind; the specials borrow: the wide basket NB's basket).</summary>
        public static readonly string[] KindModels =
        {
            null, "ND_Hazelnut", "ND_Acorn", "ND_Berry", "ND_GoldenNut", "ND_Pinecone", Basket, "ND_Feather", "ND_GoldenNut",
        };

        // Agent NB's rows (pw_nutdrop.ND_PALETTES): (base, R, G, B, A, occlusion, accent), the regions it painted.
        public const string PaletteBasket = "palette.nut_drop.basket";
        public const string PaletteHazelnut = "palette.nut_drop.hazelnut";
        public const string PaletteAcorn = "palette.nut_drop.acorn";
        public const string PaletteBerry = "palette.nut_drop.berry";
        public const string PaletteGolden = "palette.nut_drop.golden_nut";
        public const string PalettePinecone = "palette.nut_drop.pinecone";
        public const string PaletteFeather = "palette.nut_drop.feather";
        public const string PaletteCanopy = "palette.nut_drop.hazel_canopy";
        public const string PaletteTrunk = "palette.nut_drop.hazel_trunk";

        public static readonly string[] KindPalettes =
        {
            null, PaletteHazelnut, PaletteAcorn, PaletteBerry, PaletteGolden, PalettePinecone, PaletteBasket, PaletteFeather, PaletteGolden,
        };

        /// <summary>The bottom grass and flowers: the Prairie kit's baked meshes (explicit entries of Biome_Grassland_Kit) and their rows.</summary>
        private static readonly (string mesh, string palette)[] GrassKit =
        {
            ("GL_GrassTuft_M", "palette.grassland.grass"), ("GL_GrassTuft_L", "palette.grassland.grass"), ("GL_Flower_A", "palette.grassland.flower_white"),
            ("GL_GrassTuft_S", "palette.grassland.grass"), ("GL_Clover_A", "palette.grassland.clover"), ("GL_GrassTuft_M", "palette.grassland.grass"),
            ("GL_Flower_B", "palette.grassland.flower_yellow"), ("GL_GrassTuft_L", "palette.grassland.grass"), ("GL_GrassTuft_S", "palette.grassland.grass"),
            ("GL_Flower_C", "palette.grassland.flower_pink"), ("GL_GrassTuft_M", "palette.grassland.grass"), ("GL_Clover_A", "palette.grassland.clover"),
        };

        public const string LeafKit = "GL_Leaf_A";
        public const string LeafPalette = "palette.grassland.leaf";

        /// <summary>The leaves of a bonk on the set (m).</summary>
        public const float LeafRadius = 0.035f;

        /// <summary>The basket's widest (d1 1.20 × 1.5 while wide) and narrowest (challenge 4: 0.70) catch widths, in units.</summary>
        public static readonly float[] SeatWidths = { 0.7f, 0.85f, 1f, 1.2f, 1.5f, 1.8f };

        /// <summary>Forward offsets (m, the animal's root frame) tried for the basket's centre, from the head socket over the brow.</summary>
        public static readonly float[] SeatForwards = { 0f, 0.02f, 0.04f, 0.06f, 0.08f, 0.10f, 0.12f };

        [MenuItem("Pocket Wilds/Activities/Build Nut Drop (T40)", priority = 242)]
        public static void BuildMenu() => Debug.Log("[NutDropPipeline] " + BuildAll());

        public static string BuildAll()
        {
            var report = new List<string>();
            var missing = new List<string>();
            int configured = ConfigureImporters(missing);
            report.Add($"{configured} models set up" + (missing.Count > 0 ? ", missing: " + string.Join(", ", missing) + " (agent NB's export)" : ""));
            EnsurePalettes();
            // The Prairie's own look (MAT_Environment_Common's values, one palette row per renderer), in the activity's materials
            // so its bundle never copies the kit's; the props a touch more rim (they read while they fall); gold stays matte.
            var set = EnsureEnvironmentMaterial(SetMaterialPath, "MAT_NutDrop_Set", -1f, default, 0f, -1f);
            var props = EnsureEnvironmentMaterial(PropsMaterialPath, "MAT_NutDrop_Props", 0.16f, new Color(1f, 0.97f, 0.9f), 3f, -1f);
            var gold = EnsureEnvironmentMaterial(GoldMaterialPath, "MAT_NutDrop_Gold", 0.24f, new Color(1f, 0.9f, 0.62f), 2.6f, -1f);
            var basket = MeasureBasket(report);
            var seats = MeasureSeats(basket, report);
            var prefab = BuildSetPrefab(set, props, gold, basket, seats, report);
            if (prefab != null)
                report.Add(EnsureAddressables(prefab, report));
            AssetDatabase.SaveAssets();
            return string.Join("; ", report);
        }

        // ---------------------------------------------------------------- models

        public static string ModelPath(string name) => $"{ModelsFolder}/{name}.fbx";

        /// <summary>Static prop settings (like AssetImportRules' Prop_Static), set here because the folder is the activity's.</summary>
        public static int ConfigureImporters(List<string> missing)
        {
            int count = 0;
            foreach (var name in new[] { PropsModel, TreeModel })
            {
                var importer = AssetImporter.GetAtPath(ModelPath(name)) as ModelImporter;
                if (importer == null)
                {
                    missing.Add(name + ".fbx");
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

        /// <summary>A model's meshes by object name, with their matrices in the model's space.</summary>
        public static List<(Mesh mesh, Matrix4x4 matrix, string name)> ModelMeshes(string path)
        {
            var result = new List<(Mesh, Matrix4x4, string)>();
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null)
                return result;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    result.Add((filter.sharedMesh, root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix, filter.name));
            return result;
        }

        private static (Mesh mesh, Matrix4x4 matrix) Find(List<(Mesh mesh, Matrix4x4 matrix, string name)> meshes, string name)
        {
            foreach (var m in meshes)
                if (m.name == name || m.mesh.name == name)
                    return (m.mesh, m.matrix);
            return (null, Matrix4x4.identity);
        }

        private static Mesh Kit(string name) => AssetDatabase.LoadAssetAtPath<Mesh>($"{KitFolder}/{name}.asset");

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
            // pw_nutdrop.ND_PALETTES, row for row.
            Add(set, PaletteBasket, "D3A05E", "EBC98C", "E4BB7C", "B9884D", "DC8C7E", "6F5048", "FFFFFF");
            Add(set, PaletteHazelnut, "B0703F", "E8CFA2", "93582F", "B0703F", "B0703F", "5E3B2E", "FFFFFF");
            Add(set, PaletteAcorn, "C48D50", "8E6B4A", "B39370", "6E4E38", "9C7A3E", "5A4234", "FFFFFF");
            Add(set, PaletteBerry, "D8453F", "5A2E2C", "8A8A4E", "6FAE57", "E9705A", "7A2A3A", "FFFFFF");
            Add(set, PaletteGolden, "E2A637", "F3D27C", "FFF0BA", "C27E26", "E2A637", "8A5A2A", "FFFFFF");
            Add(set, PalettePinecone, "A8714A", "D2A372", "5E4030", "7A5A3E", "A8714A", "4F3427", "FFFFFF");
            Add(set, PaletteFeather, "FBF7EE", "F1DEBE", "E7CDA2", "D7B78A", "FBF7EE", "A39CB2", "FFFFFF");
            Add(set, PaletteCanopy, "A86A3C", "7DB960", "A9D57C", "578F4B", "C9DC86", "3B6338", "FFFFFF");
            Add(set, PaletteTrunk, "8F6A52", "B3967C", "8DAA5C", "8F6A52", "6E4C3A", "4E3B38", "FFFFFF");
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

        public static uint PaletteId(string id) => string.IsNullOrEmpty(id) ? 0u : ContentId.FromSource(id).Value;

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.magenta;

        /// <summary>An SH_EnvironmentOpaque material with MAT_Environment_Common's values; a negative rim or wrap keeps them.</summary>
        private static Material EnsureEnvironmentMaterial(string path, string name, float rim, Color rimColor, float rimPower, float wrap)
        {
            var shader = Shader.Find(GroomingPipeline.EnvironmentShader)
                         ?? throw new InvalidOperationException("Shader not found: " + GroomingPipeline.EnvironmentShader);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            var common = AssetDatabase.LoadAssetAtPath<Material>(GroomingPipeline.EnvironmentCommonPath);
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
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------------------------------------------------------------- the basket, measured

        /// <summary>ND_Basket in its own space (pivot at the foot's center): rim, foot, the inside's floor, the underside.</summary>
        public sealed class BasketShape
        {
            public Mesh mesh;
            public float rimRadius = 0.155f;
            public float rimTop = 0.165f;
            public float bottom;
            public float innerRadius = 0.134f;
            public float[] inner = new float[9];

            /// <summary>The underside's height at 17 radii from the axis to the rim (the lowest point of the basket there).</summary>
            public float[] under = new float[17];

            /// <summary>The underside at a distance from the axis (mesh units): the lower of its two neighbours (conservative).</summary>
            public float Under(float rho)
            {
                if (rho >= rimRadius)
                    return float.PositiveInfinity;
                float t = rho / rimRadius * (under.Length - 1);
                int i = Mathf.Min(under.Length - 2, (int)t);
                return Mathf.Min(under[i], under[i + 1]);
            }
        }

        public static BasketShape MeasureBasket(List<string> report)
        {
            var shape = new BasketShape();
            var (mesh, matrix) = Find(ModelMeshes(ModelPath(PropsModel)), Basket);
            if (mesh == null)
            {
                report.Add("WARNING: " + Basket + " missing: default basket numbers (31 cm wide, 16.5 cm deep)");
                for (int i = 0; i < shape.inner.Length; i++)
                    shape.inner[i] = Mathf.Lerp(0.028f, 0.146f, (i / 8f) * (i / 8f));
                for (int i = 0; i < shape.under.Length; i++)
                    shape.under[i] = 0f;
                return shape;
            }
            shape.mesh = mesh;
            // The basket's own space: its pivot is its object's (the item renderers use the mesh as is).
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            float rimRadius = 0f, top = float.MinValue, bottom = float.MaxValue;
            foreach (var v in vertices)
            {
                rimRadius = Mathf.Max(rimRadius, new Vector2(v.x, v.z).magnitude);
                top = Mathf.Max(top, v.y);
                bottom = Mathf.Min(bottom, v.y);
            }
            float innerRadius = rimRadius;
            foreach (var v in vertices)
                if (v.y >= top - 0.02f * (top - bottom) / 0.165f)
                    innerRadius = Mathf.Min(innerRadius, new Vector2(v.x, v.z).magnitude);
            shape.rimRadius = rimRadius;
            shape.rimTop = top;
            shape.bottom = bottom;
            shape.innerRadius = Mathf.Max(0.001f, innerRadius);
            // The inside's floor: the highest upward-facing point within each radius (it rises toward the rim: an upper bound).
            float bin = shape.innerRadius / 8f;
            for (int i = 0; i < shape.inner.Length; i++)
            {
                float rho = i * bin;
                float h = float.MinValue;
                for (int v = 0; v < vertices.Length; v++)
                {
                    if (normals.Length > v && normals[v].y < 0.2f)
                        continue;
                    if (new Vector2(vertices[v].x, vertices[v].z).magnitude <= rho + bin)
                        h = Mathf.Max(h, vertices[v].y);
                }
                shape.inner[i] = h == float.MinValue ? bottom : h;
            }
            for (int i = 1; i < shape.inner.Length; i++)
                shape.inner[i] = Mathf.Max(shape.inner[i], shape.inner[i - 1]);
            // The underside: the lowest point at each radius (whatever faces down there).
            float ubin = rimRadius / (shape.under.Length - 1);
            for (int i = 0; i < shape.under.Length; i++)
            {
                float rho = i * ubin;
                float h = float.MaxValue;
                foreach (var v in vertices)
                    if (Mathf.Abs(new Vector2(v.x, v.z).magnitude - rho) <= ubin)
                        h = Mathf.Min(h, v.y);
                shape.under[i] = h == float.MaxValue ? bottom : h;
            }
            report.Add($"basket {rimRadius * 200f:F1} cm wide, rim {top * 100f:F1} cm, foot {bottom * 100f:F1} cm, inside {shape.innerRadius * 200f:F1} cm, floor {shape.inner[0] * 100f:F1} cm");
            return shape;
        }

        // ---------------------------------------------------------------- the seats (the basket on each species' head)

        /// <summary>
        /// Every species of the visual catalog at its bind pose (LOD0, scale 1): SOCK_HeadTop, the body, and the basket's lift
        /// over the socket so its underside clears every point of the animal within its footprint at each catch width (the
        /// rabbit's ears, the squirrel's tufts and tail, the hedgehog's spines).
        /// </summary>
        public static List<NutDropSeat> MeasureSeats(BasketShape basket, List<string> report)
        {
            var seats = new List<NutDropSeat>();
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var lines = new List<string>();
            if (catalog != null)
            {
                foreach (var entry in catalog.entries)
                {
                    if (entry == null || entry.prefab == null)
                        continue;
                    var seat = MeasureSeat(entry.visualKey, entry.prefab, basket);
                    if (seat == null)
                        continue;
                    seats.Add(seat);
                    lines.Add($"{Short(entry.visualKey)} socket {seat.socketY * 100f:F1} cm, forward {seat.socketZ * 100f:F1} cm, basket lifted {seat.seat * 100f:F1} cm (over {seat.above * 100f:F1} cm), body ±{seat.halfWidth * 100f:F0} × {seat.height * 100f:F0} cm");
                }
            }
            report.Add("seats: " + (lines.Count > 0 ? string.Join(", ", lines) : "none (no visual catalog)"));
            return seats;
        }

        public static NutDropSeat MeasureSeat(string visualKey, AnimalView prefab, BasketShape basket)
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
                var socket = FindDeep(instance.transform, "SOCK_HeadTop") ?? view.SocketHeadTop;
                var skin = Lod0(instance);
                if (socket == null || skin == null || skin.sharedMesh == null)
                    return null;
                var points = SkinnedPoints(skin);
                var s = socket.position;
                var seat = new NutDropSeat { visualKey = visualKey, socketY = s.y, socketZ = s.z };
                float halfWidth = 0f, front = float.MinValue, back = float.MaxValue, height = 0f, above = 0f;
                foreach (var p in points)
                {
                    halfWidth = Mathf.Max(halfWidth, Mathf.Abs(p.x));
                    front = Mathf.Max(front, p.z);
                    back = Mathf.Min(back, p.z);
                    height = Mathf.Max(height, p.y);
                }
                float vs = NutDropLayout.FieldScale * 0.5f / Mathf.Max(1e-4f, basket.rimRadius);
                // The basket slides forward over the brow until its underside rests lowest: centred on the head socket it would
                // ride on the ear tips (or the spines). The smallest forward offset within 5 mm of the lowest seat wins.
                float bestLift = float.MaxValue, bestAbove = 0f, bestForward = 0f;
                foreach (float forward in SeatForwards)
                {
                    float lift = float.MinValue, aboveHere = 0f;
                    foreach (float width in SeatWidths)
                    {
                        float hs = NutDropLayout.FieldScale * width * 0.5f / Mathf.Max(1e-4f, basket.rimRadius);
                        float reach = basket.rimRadius * hs;
                        foreach (var p in points)
                        {
                            float rho = new Vector2(p.x - s.x, p.z - (s.z + forward)).magnitude;
                            if (rho >= reach)
                                continue;
                            aboveHere = Mathf.Max(aboveHere, p.y - s.y);
                            // The pivot so high that the underside right over this point stays above it.
                            lift = Mathf.Max(lift, p.y - s.y - basket.Under(rho / hs) * vs);
                        }
                    }
                    if (lift == float.MinValue)
                        lift = -basket.bottom * vs;
                    if (lift < bestLift - 0.005f)
                    {
                        bestLift = lift;
                        bestAbove = aboveHere;
                        bestForward = forward;
                    }
                }
                above = bestAbove;
                seat.socketZ = s.z + bestForward;
                seat.seat = bestLift;
                seat.above = above;
                seat.halfWidth = halfWidth;
                seat.front = front;
                seat.back = back;
                seat.height = height;
                return seat;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static Vector3[] SkinnedPoints(SkinnedMeshRenderer skin)
        {
            var mesh = skin.sharedMesh;
            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;
            var bindposes = mesh.bindposes;
            var bones = skin.bones;
            bool skinned = bones != null && bones.Length == bindposes.Length && bones.Length > 0 && bones.All(b => b != null)
                           && weights.Length == vertices.Length;
            var matrix = skin.transform.localToWorldMatrix;
            var boneMatrices = skinned ? bones.Select((b, i) => b.localToWorldMatrix * bindposes[i]).ToArray() : null;
            var points = new Vector3[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                if (!skinned)
                {
                    points[v] = matrix.MultiplyPoint3x4(vertices[v]);
                    continue;
                }
                var w = weights[v];
                points[v] = Blend(boneMatrices, w.boneIndex0, w.weight0, vertices[v]) + Blend(boneMatrices, w.boneIndex1, w.weight1, vertices[v])
                            + Blend(boneMatrices, w.boneIndex2, w.weight2, vertices[v]) + Blend(boneMatrices, w.boneIndex3, w.weight3, vertices[v]);
            }
            return points;
        }

        private static Vector3 Blend(Matrix4x4[] matrices, int bone, float weight, Vector3 v) =>
            weight <= 0f ? Vector3.zero : weight * matrices[bone].MultiplyPoint3x4(v);

        private static SkinnedMeshRenderer Lod0(GameObject root)
        {
            var group = root.GetComponentInChildren<LODGroup>(true);
            if (group != null && group.lodCount > 0)
                foreach (var r in group.GetLODs()[0].renderers)
                    if (r is SkinnedMeshRenderer skin)
                        return skin;
            return root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(r => r.name, StringComparer.Ordinal).FirstOrDefault();
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

        private static string Short(string visualKey) => visualKey.StartsWith("PF_Animal_", StringComparison.Ordinal) ? visualKey.Substring(10) : visualKey;

        // ---------------------------------------------------------------- the set prefab

        public static GameObject BuildSetPrefab(Material setMaterial, Material props, Material gold, BasketShape basket, List<NutDropSeat> seats,
            List<string> report)
        {
            var root = new GameObject(NutDropLayout.SetPrefabName);
            try
            {
                var view = root.AddComponent<NutDropSetView>();
                var data = new NutDropSetData { seats = seats.ToArray() };
                var bindings = new List<WorldChunk.PaletteBinding>();
                var origin = new GameObject("Origin").transform;
                origin.SetParent(root.transform, false);
                data.origin = origin;
                var decor = new GameObject("Set").transform;
                decor.SetParent(root.transform, false);
                float fieldHalf = NutDropRules.HalfWidth * NutDropLayout.FieldScale;

                // T38's backdrop (the meadow floor and the round hills), bigger, its floor at the set's origin; its center
                // toward the camera so its inner ring (the grooming mound's place) stays under the bottom of the screen. GB's
                // camera side is Unity +Z: the backdrop turns half a turn, like on the grooming set.
                var backdrop = Find(GroomingPipeline.ModelMeshes(GroomingPipeline.SetModel), BackdropName);
                if (backdrop.mesh != null)
                {
                    var holder = new GameObject("Backdrop").transform;
                    holder.SetParent(decor, false);
                    holder.localPosition = new Vector3(0f, -NutDropLayout.BackdropFloor * NutDropLayout.BackdropScale, NutDropLayout.BackdropZ);
                    holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    holder.localScale = Vector3.one * NutDropLayout.BackdropScale;
                    var renderer = AddRenderer(BackdropName, holder, backdrop.mesh, setMaterial, ShadowCastingMode.Off, true);
                    Place(renderer.transform, backdrop.matrix);
                    bindings.Add(Binding(renderer, GroomingPipeline.PaletteBackdrop));
                }
                else
                {
                    report.Add("WARNING: " + BackdropName + " missing (" + GroomingPipeline.SetModel + ".fbx): the set has no floor nor hills");
                }

                // The hazel tree: its trunk at the left of the field, behind the animal's walk; its crown over the field.
                var tree = ModelMeshes(ModelPath(TreeModel));
                var treePosition = new Vector3(-fieldHalf - 0.2f, 0f, NutDropLayout.TreeMinZ);
                var treeRotation = Quaternion.Euler(0f, 180f, 0f);
                var trunk = Find(tree, Trunk);
                if (trunk.mesh != null)
                {
                    // Its lower part behind the walking animal (its back at most ~0.4 m behind the field's plane).
                    float front = MinZ(trunk.mesh, Matrix4x4.TRS(Vector3.zero, treeRotation, Vector3.one) * trunk.matrix, 1.2f);
                    treePosition.z = Mathf.Max(NutDropLayout.TreeMinZ, 0.5f - front);
                }
                var treeMatrix = Matrix4x4.TRS(treePosition, treeRotation, Vector3.one);
                var treeRoot = new GameObject("HazelTree").transform;
                treeRoot.SetParent(decor, false);
                treeRoot.SetLocalPositionAndRotation(treePosition, treeRotation);
                float canopyBottom = float.MaxValue, canopyAny = float.MaxValue, crownLeft = float.MaxValue, crownRight = float.MinValue;
                float slab = 0.12f + 0.25f;
                foreach (var (mesh, matrix, name) in tree)
                {
                    bool isTrunk = name.IndexOf("Trunk", StringComparison.OrdinalIgnoreCase) >= 0;
                    var renderer = AddRenderer(name, treeRoot, mesh, setMaterial, ShadowCastingMode.Off, true);
                    Place(renderer.transform, matrix);
                    bindings.Add(Binding(renderer, isTrunk ? PaletteTrunk : PaletteCanopy));
                    if (isTrunk)
                        continue;
                    var world = treeMatrix * matrix;
                    foreach (var v in mesh.vertices)
                    {
                        var p = world.MultiplyPoint3x4(v);
                        crownLeft = Mathf.Min(crownLeft, p.x);
                        crownRight = Mathf.Max(crownRight, p.x);
                        if (Mathf.Abs(p.x) > fieldHalf + 0.2f)
                            continue;
                        canopyAny = Mathf.Min(canopyAny, p.y);
                        if (Mathf.Abs(p.z) <= slab)
                            canopyBottom = Mathf.Min(canopyBottom, p.y);
                    }
                }
                if (canopyBottom == float.MaxValue)
                    canopyBottom = canopyAny;
                float maxItem = MaxItemRadius();
                if (canopyBottom == float.MaxValue)
                {
                    report.Add("WARNING: " + TreeModel + ".fbx missing: no canopy, the spawn line at its default " + NutDropLayout.DefaultSpawnY + " m");
                    data.spawnY = NutDropLayout.DefaultSpawnY;
                    data.canopyBottom = NutDropLayout.DefaultSpawnY + maxItem;
                }
                else
                {
                    data.canopyBottom = canopyBottom;
                    data.spawnY = canopyBottom - maxItem - 0.03f;
                    report.Add($"hazel tree at x {treePosition.x:F2} z {treePosition.z:F2}: leaves at {canopyBottom:F2} m over the field, spawn line {data.spawnY:F2} m, crown {crownLeft:F2} to {crownRight:F2} m"
                               + (crownLeft > -fieldHalf || crownRight < fieldHalf ? " (THE CROWN DOES NOT COVER THE FIELD)" : "")
                               + TrunkReport(trunk, treeMatrix, fieldHalf, canopyBottom));
                }

                // The bottom grass and flowers (the Prairie kit), along a line in front of the animal's walk, never in each other.
                report.Add(PlaceGrass(decor, setMaterial, bindings));

                data.bindings = bindings.ToArray();
                data.camera = AddCamera(root.transform);
                data.light = AddLight(root.transform);

                // Pooled props, built once (doc Mini-jeux section 1: nothing instantiated during a game).
                var propsRoot = new GameObject("Props").transform;
                propsRoot.SetParent(root.transform, false);
                var meshes = ModelMeshes(ModelPath(PropsModel));
                data.propsMaterial = props;
                data.goldMaterial = gold;
                if (basket.mesh != null)
                {
                    data.basket = AddRenderer("Basket", propsRoot, basket.mesh, props, ShadowCastingMode.On, false);
                    data.basketPalette = PaletteId(PaletteBasket);
                }
                data.basketRimRadius = basket.rimRadius;
                data.basketRimTop = basket.rimTop;
                data.basketBottom = basket.bottom;
                data.basketInnerRadius = basket.innerRadius;
                data.basketInner = basket.inner.ToArray();
                data.kindMeshes = new Mesh[NutDropLayout.KindCount];
                data.kindRadii = new float[NutDropLayout.KindCount];
                data.kindPalettes = new uint[NutDropLayout.KindCount];
                data.kindGold = new bool[NutDropLayout.KindCount];
                var missingItems = new List<string>();
                for (int k = 1; k < NutDropLayout.KindCount; k++)
                {
                    var (mesh, _) = Find(meshes, KindModels[k]);
                    if (mesh == null)
                    {
                        missingItems.Add(KindModels[k]);
                        continue;
                    }
                    data.kindMeshes[k] = mesh;
                    data.kindRadii[k] = Radius(mesh);
                    data.kindPalettes[k] = PaletteId(KindPalettes[k]);
                    data.kindGold[k] = k == (int)NutDropItemKind.GoldenNut || k == (int)NutDropItemKind.GoldenShower;
                }
                if (missingItems.Count > 0)
                    report.Add("WARNING: items without a model (hidden in game): " + string.Join(", ", missingItems.Distinct()));
                else
                    report.Add("items: " + string.Join(", ", Enumerable.Range(1, NutDropLayout.KindCount - 1)
                        .Select(k => $"{(NutDropItemKind)k} {data.kindRadii[k] * NutDropLayout.ItemScale((NutDropItemKind)k) * 200f:F0} cm")));
                var first = data.kindMeshes[(int)NutDropItemKind.Hazelnut];
                data.items = new MeshRenderer[NutDropLayout.ItemPool];
                for (int i = 0; i < data.items.Length; i++)
                    data.items[i] = AddRenderer("Item_" + i.ToString("00"), propsRoot, first, props, ShadowCastingMode.Off, false);

                // H-VFX-ACT-003-03: the bonk's leaves, the kit's leaf.
                var leaf = Kit(LeafKit);
                data.leafPalette = PaletteId(LeafPalette);
                data.leafRadius = LeafRadius;
                data.leafScale = leaf != null ? LeafRadius / Mathf.Max(0.001f, Mathf.Max(leaf.bounds.max.magnitude, leaf.bounds.min.magnitude)) : 1f;
                data.leafLift = leaf != null ? Mathf.Max(0.002f, -leaf.bounds.min.y * data.leafScale) : 0.004f;
                data.leaves = new MeshRenderer[leaf != null ? NutDropLayout.LeafCount : 0];
                for (int i = 0; i < data.leaves.Length; i++)
                    data.leaves[i] = AddRenderer("Leaf_" + i, propsRoot, leaf, props, ShadowCastingMode.Off, false);
                if (leaf == null)
                    report.Add("WARNING: " + LeafKit + " missing in " + KitFolder + ": the bonk drops no leaves");

                // The effects: round glints, soft dots, a soft ring, hearts (the feedback atlas; never a four-point sparkle).
                var fx = new GameObject("Fx").transform;
                fx.SetParent(root.transform, false);
                var vfx = AssetDatabase.LoadAssetAtPath<Material>(VfxPipeline.MaterialPath);
                if (vfx == null)
                    report.Add("WARNING: " + VfxPipeline.MaterialPath + " missing (run the VFX pipeline): no effects");
                data.glints = new ParticleSystem[NutDropLayout.GlintSystems];
                for (int i = 0; i < data.glints.Length; i++)
                    data.glints[i] = AddParticles(fx, "Glints_" + i, vfx, VfxPipeline.TileGlint, ParticleSystemSimulationSpace.World, 32,
                        new ParticleSystem.MinMaxCurve(0.4f, 0.55f), new ParticleSystem.MinMaxCurve(0.03f, 0.042f),
                        new Color(1f, 0.95f, 0.8f), new Color(1f, 0.84f, 0.5f), -0.02f, 0.3f, 1.1f, 0.6f);
                data.trail = AddParticles(fx, "GoldenTrail", vfx, VfxPipeline.TileDot, ParticleSystemSimulationSpace.World, 96,
                    new ParticleSystem.MinMaxCurve(0.27f), new ParticleSystem.MinMaxCurve(0.042f),
                    new Color(1f, 0.9f, 0.55f, 0.85f), new Color(1f, 0.82f, 0.42f, 0.85f), 0f, 1f, 0.85f, 0.35f);
                data.ring = AddParticles(fx, "SpecialRing", vfx, VfxPipeline.TileRing, ParticleSystemSimulationSpace.Local, 4,
                    new ParticleSystem.MinMaxCurve(0.75f), new ParticleSystem.MinMaxCurve(0.5f),
                    new Color(1f, 0.96f, 0.82f, 0.9f), new Color(1f, 0.9f, 0.7f, 0.9f), 0f, 0.75f, 1.05f, 1.15f);
                data.hearts = AddParticles(fx, "Hearts", vfx, VfxPipeline.TileHeart, ParticleSystemSimulationSpace.World, 8,
                    new ParticleSystem.MinMaxCurve(1.1f), new ParticleSystem.MinMaxCurve(0.07f, 0.085f),
                    new Color(1f, 0.62f, 0.72f), new Color(1f, 0.7f, 0.78f), -0.04f, 0.4f, 1.1f, 0.9f);

                // The boost icons are borrowed from the HUD at run time (no UI atlas in this bundle).
                var icons = new[] { NutDropStage.WideBasketIconName, NutDropStage.FeatherIconName, NutDropStage.GoldenShowerIconName };
                var noIcon = icons.Where(i => AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsFolder}/{i}.png") == null).ToList();
                if (noIcon.Count > 0)
                    report.Add("WARNING: boost icons missing in " + IconsFolder + ": " + string.Join(", ", noIcon) + " (agent NA)");
                ApplyLightingData(data);

                view.SetAuthoring(data);
                foreach (Transform child in propsRoot)
                    child.gameObject.SetActive(false);
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success)
                    throw new InvalidOperationException("Could not save " + PrefabPath);
                report.Add($"{PrefabPath}: {saved.GetComponentsInChildren<Renderer>(true).Length} renderers, set triangles {CountTriangles(saved)}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The largest item's radius on the set (m): the spawn line keeps it under the leaves.</summary>
        private static float MaxItemRadius()
        {
            var meshes = ModelMeshes(ModelPath(PropsModel));
            float max = 0.1f;
            for (int k = 1; k < NutDropLayout.KindCount; k++)
            {
                var (mesh, _) = Find(meshes, KindModels[k]);
                if (mesh != null)
                    max = Mathf.Max(max, Radius(mesh) * NutDropLayout.ItemScale((NutDropItemKind)k));
            }
            return max;
        }

        /// <summary>The farthest vertex from the mesh's pivot (the item spins around it).</summary>
        private static float Radius(Mesh mesh)
        {
            float r = 0f;
            foreach (var v in mesh.vertices)
                r = Mathf.Max(r, v.magnitude);
            return r;
        }

        /// <summary>The nearest point to the camera (lowest z) under a height.</summary>
        private static float MinZ(Mesh mesh, Matrix4x4 matrix, float below)
        {
            float z = float.MaxValue;
            foreach (var v in mesh.vertices)
            {
                var p = matrix.MultiplyPoint3x4(v);
                if (p.y < below)
                    z = Mathf.Min(z, p.z);
            }
            return z == float.MaxValue ? 0f : z;
        }

        /// <summary>The trunk keeps out of the field's plane under the leaves, out of the animal's walk and out of the near hills.</summary>
        private static string TrunkReport((Mesh mesh, Matrix4x4 matrix) trunk, Matrix4x4 treeMatrix, float fieldHalf, float canopyBottom)
        {
            if (trunk.mesh == null)
                return ", no trunk";
            var world = treeMatrix * trunk.matrix;
            float front = float.MaxValue, hills = 0f;
            bool inField = false;
            var center = new Vector2(0f, NutDropLayout.BackdropZ);
            float hillFoot = 2.25f * NutDropLayout.BackdropScale;
            foreach (var v in trunk.mesh.vertices)
            {
                var p = world.MultiplyPoint3x4(v);
                front = Mathf.Min(front, p.z);
                if (Mathf.Abs(p.z) < 0.2f && Mathf.Abs(p.x) < fieldHalf + 0.15f && p.y < canopyBottom)
                    inField = true;
                if (p.y < 1.8f)
                    hills = Mathf.Max(hills, (new Vector2(p.x, p.z) - center).magnitude);
            }
            return $", trunk front {front:F2} m behind the field" + (inField ? " (THE TRUNK CROSSES THE FIELD)" : "")
                   + (hills > hillFoot - 0.05f ? " (THE TRUNK TOUCHES THE NEAR HILLS)" : "");
        }

        /// <summary>
        /// The kit's grass and flowers in a row along the bottom of the screen: each footprint clear of the others (the kit's
        /// shared meshes, explicit entries of Biome_Grassland_Kit: the set's bundle depends on them, never copies them).
        /// </summary>
        private static string PlaceGrass(Transform decor, Material material, List<WorldChunk.PaletteBinding> bindings)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            var row = new GameObject("BottomGrass").transform;
            row.SetParent(decor, false);
            var placed = new List<(Vector2 at, float r)>();
            var skipped = new HashSet<string>();
            float x = -1.15f;
            uint seed = 0x47524153u;
            int count = 0;
            for (int i = 0; count < NutDropLayout.GrassRow && x < 1.15f && i < 64; i++)
            {
                var (meshName, palette) = GrassKit[i % GrassKit.Length];
                var mesh = Kit(meshName);
                if (mesh == null || !Explicit(settings, AssetDatabase.GetAssetPath(mesh)))
                {
                    skipped.Add(meshName);
                    if (skipped.Count >= GrassKit.Select(g => g.mesh).Distinct().Count())
                        break;
                    continue;
                }
                seed = seed * 1664525u + 1013904223u;
                float scale = 1f + 0.25f * ((seed >> 8) & 255u) / 255f;
                // Any yaw: the farthest corner of its bounds from its pivot.
                var b = mesh.bounds;
                float r = new Vector2(Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z))).magnitude * scale;
                float z = NutDropLayout.GrassZ + 0.14f * (((seed >> 16) & 255u) / 255f - 0.5f);
                var at = new Vector2(x + r, z);
                // Clear of every piece already placed (a decor never in another).
                foreach (var (other, rr) in placed)
                    if ((other - at).magnitude < r + rr)
                        at.x = Mathf.Max(at.x, other.x + Mathf.Sqrt(Mathf.Max(0f, (r + rr) * (r + rr) - (other.y - at.y) * (other.y - at.y))) + 0.002f);
                var renderer = AddRenderer(meshName + "_" + count, row, mesh, material, ShadowCastingMode.Off, true);
                renderer.transform.SetLocalPositionAndRotation(new Vector3(at.x, -mesh.bounds.min.y * scale, at.y),
                    Quaternion.Euler(0f, (seed >> 4) % 360u, 0f));
                renderer.transform.localScale = Vector3.one * scale;
                bindings.Add(Binding(renderer, palette));
                placed.Add((at, r));
                x = at.x + r;
                count++;
            }
            return $"bottom grass: {count} kit pieces" + (skipped.Count > 0 ? " (WARNING: not explicit kit entries, skipped: " + string.Join(", ", skipped) + ")" : "");
        }

        private static bool Explicit(AddressableAssetSettings settings, string path) =>
            settings != null && !string.IsNullOrEmpty(path) && settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)) != null;

        private static WorldChunk.PaletteBinding Binding(MeshRenderer renderer, string palette) => new WorldChunk.PaletteBinding
        {
            renderer = renderer,
            paletteId = PaletteId(palette),
            brightness = WorldChunk.NeutralBrightness,
        };

        private static void Place(Transform target, Matrix4x4 matrix)
        {
            target.localPosition = matrix.GetColumn(3);
            target.localRotation = matrix.rotation;
            target.localScale = matrix.lossyScale;
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
            go.transform.localRotation = NutDropLayout.CameraRotation;
            go.transform.localPosition = new Vector3(0f, 1.9f, -7f);
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = NutDropLayout.CameraFov;
            camera.nearClipPlane = NutDropLayout.CameraNear;
            camera.farClipPlane = NutDropLayout.CameraFar;
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

        /// <summary>The set's warm sun: the Prairie day a touch more golden (an afternoon under the hazel), from the camera's right.</summary>
        private static Light AddLight(Transform parent)
        {
            var profile = AssetDatabase.LoadAssetAtPath<LightingProfile>("Assets/_Game/Art/Lighting/Grassland/LP_Grassland_Day.asset");
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(42f, -34f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            var sun = profile != null ? profile.sunColor : new Color(1f, 0.945f, 0.863f);
            light.color = Color.Lerp(sun, new Color(1f, 0.84f, 0.64f), 0.35f);
            light.intensity = (profile != null ? profile.sunIntensity : 1f) * 1.05f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = profile != null ? Mathf.Min(0.55f, profile.shadowStrength) : 0.5f;
            light.enabled = false;
            return light;
        }

        /// <summary>The warm Prairie day of the set: the profile's ambient, a peach horizon, a golden sun glow.</summary>
        private static void ApplyLightingData(NutDropSetData data)
        {
            var profile = AssetDatabase.LoadAssetAtPath<LightingProfile>("Assets/_Game/Art/Lighting/Grassland/LP_Grassland_Day.asset");
            var warm = new Color(1f, 0.88f, 0.72f);
            if (profile != null)
            {
                data.ambientTop = Color.Lerp(profile.ambientTop, warm, 0.14f);
                data.ambientEquator = Color.Lerp(profile.ambientEquator, warm, 0.25f);
                data.ambientBottom = Color.Lerp(profile.ambientBottom, warm, 0.12f);
                data.skyTop = Color.Lerp(profile.skyTop, new Color(0.62f, 0.78f, 0.95f), 0.5f);
                data.cloudLight = profile.cloudLight;
            }
            data.skyHorizon = new Color(1f, 0.88f, 0.74f);
            data.skyBottom = new Color(0.95f, 0.86f, 0.74f);
            data.sunGlowTint = new Color(1f, 0.84f, 0.6f);
            data.sunGlow = 0.42f;
            data.cloudShade = new Color(0.9f, 0.83f, 0.85f);
            data.cloudCoverage = 0.28f;
        }

        /// <summary>
        /// A particle system the stage emits into (Emit with its own positions: no shape, no rate), on the shared additive
        /// material and one tile of the feedback atlas.
        /// </summary>
        private static ParticleSystem AddParticles(Transform parent, string name, Material material, int tile, ParticleSystemSimulationSpace space,
            int maxParticles, ParticleSystem.MinMaxCurve lifetime, ParticleSystem.MinMaxCurve size, Color from, Color to, float gravity,
            float sizeStart, float sizePeak, float sizeEnd)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = space;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = new ParticleSystem.MinMaxGradient(from, to);
            main.gravityModifier = gravity;
            main.maxParticles = maxParticles;
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = VfxPipeline.TileCount;
            sheet.numTilesY = 1;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve((tile + 0.25f) / VfxPipeline.TileCount);
            sheet.cycleCount = 1;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.8f, 0.55f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var sizeOver = ps.sizeOverLifetime;
            sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, sizeStart), new Keyframe(0.25f, sizePeak), new Keyframe(1f, sizeEnd)));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.maxParticleSize = 0.5f;
            return ps;
        }

        private static int CountTriangles(GameObject prefab)
        {
            int count = 0;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.name.StartsWith("Item_", StringComparison.Ordinal) || filter.name.StartsWith("Leaf_", StringComparison.Ordinal))
                    continue;
                var mesh = filter.sharedMesh;
                for (int s = 0; s < mesh.subMeshCount; s++)
                    count += (int)mesh.GetIndexCount(s) / 3;
            }
            return count;
        }

        // ---------------------------------------------------------------- Addressables

        /// <summary>
        /// Group Activity_NutDrop (doc Mini-jeux, section 1): the set and what only it uses, loaded at the first game. What it
        /// shares stays in one place: the VFX material in CommonAnimals (as T38 did), T38's set model in Activity_Shared, the
        /// Prairie kit's grass in Biome_Grassland_Kit (CommonContentTests: nothing duplicated between bundles).
        /// </summary>
        public static string EnsureAddressables(GameObject prefab, List<string> report)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = EnsureGroup(settings, NutDropLayout.Group);
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)), group, false, false);
            entry.address = NutDropLayout.SetAddress;
            var common = settings.FindGroup("CommonAnimals");
            var vfxGuid = AssetDatabase.AssetPathToGUID(VfxPipeline.MaterialPath);
            if (common != null && !string.IsNullOrEmpty(vfxGuid))
            {
                var vfx = settings.CreateOrMoveEntry(vfxGuid, common, false, false);
                vfx.address = "vfx.additive_lite";
                EditorUtility.SetDirty(common);
            }
            var setGuid = AssetDatabase.AssetPathToGUID(GroomingPipeline.ModelPath(GroomingPipeline.SetModel));
            if (!string.IsNullOrEmpty(setGuid))
            {
                var shared = EnsureGroup(settings, SharedGroup);
                var model = settings.CreateOrMoveEntry(setGuid, shared, false, false);
                model.address = SharedSetAddress;
                EditorUtility.SetDirty(shared);
            }
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            return $"Addressables {NutDropLayout.Group}: {NutDropLayout.SetAddress}; {SharedGroup}: {SharedSetAddress}";
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name) ?? settings.CreateGroup(name, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            EditorUtility.SetDirty(group);
            return group;
        }
    }
}
