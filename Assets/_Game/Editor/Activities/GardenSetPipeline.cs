using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Capture;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Activities
{
    /// <summary>
    /// T43 « Jardin », le potager du refuge (doc 01 Contenu, section 8; collision rule): the import settings of agent GP's
    /// models (Art/Activities/Garden: GD_GardenSet.fbx with the meadow floor, the raised bed, its cover, the stepping
    /// stones, the fence, the watering can and the harvest basket; GD_GardenPlants.fbx with the 13 launch plants in 3
    /// stages), their palette rows and materials, the set prefab <see cref="GardenLayout.SetPrefabName"/> with its
    /// <see cref="GardenSetView"/> (five beds along the path, plot 0 at the front, a gentle zigzag, covers on plots 3 and 4,
    /// T38's backdrop hills behind the fence, the Prairie kit's grass and daisies on the edges, a portrait camera, a sun),
    /// the 39 stage prefabs and <see cref="GardenPlantVisuals"/>, the Addressables entries in
    /// <see cref="GardenLayout.Group"/>, and two edit-mode captures. Everything is measured: nothing enters anything (beds,
    /// stones, fence, can, basket, kit pieces, covers; every ripe plant inside its bed's soil), and every bed with its
    /// tallest ripe plant shows between the overlay's top bar and bottom sheet. Palettes: each renderer's row is bound by a
    /// <see cref="WorldChunk"/> on the set's root and on each plant prefab's root (call its Bind once after instantiating).
    /// Idempotent. Entry for the main session: <see cref="BuildAll"/> (execute_code), returns a short report.
    /// </summary>
    public static class GardenSetPipeline
    {
        public const string ModelsFolder = "Assets/_Game/Art/Activities/Garden";
        public const string SetModel = "GD_GardenSet";
        public const string PlantsModel = "GD_GardenPlants";
        public const string PalettesPath = "Assets/_Game/Content/Palettes/DEF_Palettes_Garden.asset";
        public const string SetMaterialPath = "Assets/_Game/Art/Materials/MAT_Garden_Set.mat";
        public const string PropsMaterialPath = "Assets/_Game/Art/Materials/MAT_Garden_Props.mat";
        public const string PlantPrefabFolder = "Assets/_Game/Prefabs/Activities/Garden";
        public const string CaptureSet = CaptureUtility.DefaultFolder + "/T43_garden_set.png";
        public const string CapturePlants = CaptureUtility.DefaultFolder + "/T43_garden_plants.png";

        public const string Ground = "GD_Ground";
        public const string Bed = "GD_Bed";
        public const string Cover = "GD_BedCover";
        public const string Path = "GD_Path";
        public const string Fence = "GD_Fence";
        public const string Can = "GD_WateringCan";
        public const string Basket = "GD_Basket";

        public static readonly string[] SetObjects = { Ground, Bed, Cover, Path, Fence, Can, Basket };

        /// <summary>The launch plants (pw_garden.GD_PLANTS): runtime key without « plant. », and the model's stem.</summary>
        public static readonly (string key, string stem)[] Plants =
        {
            ("sweet_clover", "SweetClover"), ("wild_strawberry", "WildStrawberry"), ("daisy", "Daisy"), ("round_carrot", "RoundCarrot"),
            ("poppy", "Poppy"), ("blueberry", "Blueberry"), ("lavender", "Lavender"), ("golden_wheat", "GoldenWheat"),
            ("meadow_indigo", "MeadowIndigo"), ("sunflower", "Sunflower"), ("dwarf_apple", "DwarfApple"), ("round_melon", "RoundMelon"),
            ("sweet_pea", "SweetPea"),
        };

        public static string PlantKey(int index) => "plant." + Plants[index].key;

        public static string PlantObject(string stem, int stage) => $"GD_Plant_{stem}_{stage}";

        public static string PlantPrefabPath(string stem, int stage) => $"{PlantPrefabFolder}/PF_GardenPlant_{stem}_{stage}.prefab";

        public static string PlantPalette(string key) => "palette.garden." + key;

        // ---------------- the layout (set space: x to the right of the screen, z away from the camera, y up)

        /// <summary>A bed's outer half size (m) and its soil's half size inside the planks.</summary>
        public static readonly Vector2 BedHalf = new Vector2(0.52f, 0.44f);

        public static readonly Vector2 SoilHalf = new Vector2(0.46f, 0.38f);

        /// <summary>The soil's top at a plant's root (the bed's center), and the planks' top.</summary>
        public const float SoilTop = 0.24f;

        public const float PlankTop = 0.258f;

        /// <summary>Beds alternate left and right of the path; plot i stands BedDz × i behind plot 0.</summary>
        public const float BedX = 0.76f;

        public const float BedDz = 0.78f;

        /// <summary>Each bed's touch area: its top face, 3 cm larger all round.</summary>
        public static readonly Vector2 TouchSize = new Vector2(1.1f, 0.94f);

        public static Vector3 BedPosition(int plot) => new Vector3(plot % 2 == 0 ? -BedX : BedX, 0f, plot * BedDz);

        /// <summary>The locked plots wear their cover by default (the Gems plot and the Club plot).</summary>
        public static bool CoveredByDefault(int plot) => plot >= 3;

        public static readonly Vector3 CanAt = new Vector3(1.16f, 0f, -0.06f);
        public const float CanYaw = 150f;
        public static readonly Vector3 BasketAt = new Vector3(0.6f, 0f, -0.08f);
        public const float BasketYaw = 192f;

        /// <summary>T38's backdrop: twice its size, centred under the garden's middle, its floor 2 cm under the meadow.</summary>
        public const float BackdropScale = 2f;

        public const float BackdropZ = 1.6f;
        public const float BackdropSink = 0.02f;
        public const float BackdropFloor = -0.30f;

        /// <summary>The portrait camera (1080 × 2340): all five beds with their tallest ripe plant between the bars.</summary>
        public static readonly Vector3 CameraAt = new Vector3(0f, 8.462f, -7.652f);

        public static readonly Vector3 CameraTarget = new Vector3(0f, 0.3f, 0.8f);
        public const float CameraFov = 30f;
        public const int ScreenWidth = 1080;
        public const int ScreenHeight = 2340;

        /// <summary>The overlay's top bar and bottom sheet (px of the 2340 px screen) a bed must stay clear of.</summary>
        public const float TopBar = 300f;

        public const float BottomSheet = 700f;

        /// <summary>Gaps kept between props (m), and between a ripe plant and its planks.</summary>
        public const float PropGap = 0.02f;

        public const float SoilMargin = 0.005f;

        /// <summary>Budgets (triangles): every plant stage, and the set's models together (each once).</summary>
        public const int PlantMinTriangles = 150;

        public const int PlantMaxTriangles = 900;
        public const int SetPropsBudget = 6000;

        /// <summary>The Prairie kit's pieces dressing the garden's edges (explicit entries of Biome_Grassland_Kit only).</summary>
        private static readonly (string mesh, string palette)[] KitPieces =
        {
            ("GL_GrassTuft_M", "palette.grassland.grass"), ("GL_Flower_A", "palette.grassland.flower_white"), ("GL_GrassTuft_L", "palette.grassland.grass"),
            ("GL_Clover_A", "palette.grassland.clover"), ("GL_GrassTuft_S", "palette.grassland.grass"), ("GL_Flower_C", "palette.grassland.flower_pink"),
            ("GL_GrassTuft_M", "palette.grassland.grass"), ("GL_Flower_B", "palette.grassland.flower_yellow"),
        };

        /// <summary>Where the kit pieces may stand: along the fence's foot and outside the beds on both sides (x, z).</summary>
        private static readonly Vector2[] KitSpots =
        {
            new Vector2(-1.75f, 3.7f), new Vector2(-1.2f, 3.82f), new Vector2(-0.45f, 3.95f), new Vector2(0.4f, 3.95f), new Vector2(1.15f, 3.82f),
            new Vector2(1.75f, 3.68f), new Vector2(-1.55f, 2.3f), new Vector2(-1.55f, 0.75f), new Vector2(1.6f, 1.55f), new Vector2(1.6f, 3.0f),
            new Vector2(0.55f, -0.72f), new Vector2(-1.5f, -0.35f),
        };

        [MenuItem("Pocket Wilds/Activities/Build Garden Set (T43)", priority = 247)]
        public static void BuildMenu() => Debug.Log("[GardenSetPipeline] " + BuildAll());

        /// <summary>Everything, in order; <paramref name="capture"/> false skips the captures (they open the world scene).</summary>
        public static string BuildAll(bool capture = true)
        {
            var report = new List<string>();
            if (!ConfigureImporters(report))
                return string.Join("; ", report);
            EnsurePalettes();
            var setMaterial = EnsureEnvironmentMaterial(SetMaterialPath, "MAT_Garden_Set", -1f, default, 0f);
            var propsMaterial = EnsureEnvironmentMaterial(PropsMaterialPath, "MAT_Garden_Props", 0.16f, new Color(1f, 0.97f, 0.9f), 3f);
            var visuals = BuildPlants(propsMaterial, report);
            var prefab = BuildSetPrefab(setMaterial, propsMaterial, visuals, report);
            if (prefab != null && visuals != null)
            {
                report.Add(EnsureAddressables(prefab, visuals));
                if (capture)
                    report.Add(Capture(prefab, visuals));
            }
            AssetDatabase.SaveAssets();
            return string.Join("; ", report);
        }

        // ---------------------------------------------------------------- models

        public static string ModelPath(string name) => $"{ModelsFolder}/{name}.fbx";

        /// <summary>Static prop settings (like AssetImportRules' Prop_Static), set here because the folder is the activity's.</summary>
        public static bool ConfigureImporters(List<string> report)
        {
            bool ok = true;
            foreach (var name in new[] { SetModel, PlantsModel })
            {
                var importer = AssetImporter.GetAtPath(ModelPath(name)) as ModelImporter;
                if (importer == null)
                {
                    report.Add("WARNING: " + ModelPath(name) + " missing (agent GP's export): nothing built");
                    ok = false;
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
            }
            return ok;
        }

        public static List<(Mesh mesh, Matrix4x4 matrix, string name)> ModelMeshes(string model) => NutDropPipeline.ModelMeshes(ModelPath(model));

        public static (Mesh mesh, Matrix4x4 matrix) Find(List<(Mesh mesh, Matrix4x4 matrix, string name)> meshes, string name)
        {
            foreach (var m in meshes)
                if (m.name == name || m.mesh.name == name)
                    return (m.mesh, m.matrix);
            return (null, Matrix4x4.identity);
        }

        /// <summary>A model's holder: Blender's front view becomes the set camera's (half a turn), then its own yaw.</summary>
        public static Matrix4x4 Holder(Vector3 position, float yaw, float scale = 1f) =>
            Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);

        public static List<Vector3> Points((Mesh mesh, Matrix4x4 matrix) model, Matrix4x4 holder)
        {
            var points = new List<Vector3>();
            if (model.mesh == null)
                return points;
            var matrix = holder * model.matrix;
            foreach (var v in model.mesh.vertices)
                points.Add(matrix.MultiplyPoint3x4(v));
            return points;
        }

        public static int Triangles(Mesh mesh)
        {
            if (mesh == null)
                return 0;
            int count = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
                count += (int)mesh.GetIndexCount(s) / 3;
            return count;
        }

        // ---------------------------------------------------------------- palettes and materials

        public static void EnsurePalettes()
        {
            var set = AssetDatabase.LoadAssetAtPath<PaletteSetAsset>(PalettesPath);
            if (set == null)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PalettesPath));
                set = ScriptableObject.CreateInstance<PaletteSetAsset>();
                AssetDatabase.CreateAsset(set, PalettesPath);
            }
            set.palettes.Clear();
            // pw_garden.GD_PALETTES, row for row: (base, R, G, B, A, occlusion, accent).
            Add(set, "palette.garden.bed", "BF8758", "DDB07E", "6A4B3B", "8C6A52", "A8734C", "5A4446", "FFFFFF");
            Add(set, "palette.garden.cover", "E6D2AC", "9FBF88", "C9955F", "F3DDB0", "A9764B", "857892", "FFFFFF");
            Add(set, "palette.garden.path", "CDBFA6", "DCD0B8", "9FBF74", "CDBFA9", "E6DCC8", "6E6A7E", "FFFFFF");
            Add(set, "palette.garden.fence", "D4A874", "E9C796", "C49460", "8DAA5C", "D4A874", "6A5250", "FFFFFF");
            Add(set, "palette.garden.watering_can", "7FBFC4", "B4DEDD", "E3BE62", "5E9FA8", "35585E", "4B5C7A", "FFFFFF");
            Add(set, "palette.garden.basket", "D3A05E", "EBC98C", "E4BB7C", "B9884D", "DC8C7E", "6F5048", "FFFFFF");
            Add(set, "palette.garden.ground", "8DC166", "A6D07A", "7DB55C", "B9A27C", "8DC166", "4E6D5D", "FFFFFF");
            Add(set, "palette.garden.sweet_clover", "5F9A48", "79B95A", "EE8FB4", "FFD3E3", "A5D483", "3F6346", "FFFFFF");
            Add(set, "palette.garden.wild_strawberry", "5E9646", "74B055", "FFFCF4", "E2403D", "F5C84C", "4A3D48", "FFFFFF");
            Add(set, "palette.garden.daisy", "5F9C49", "79B65A", "FFFDF7", "F2C24F", "F4E6C6", "4E6D5D", "FFFFFF");
            Add(set, "palette.garden.round_carrot", "5C9C44", "86C463", "EE8630", "FFB45E", "D86E25", "5A4A40", "FFFFFF");
            Add(set, "palette.garden.poppy", "7FA864", "8DBA72", "E8463B", "3B2C35", "B9D58E", "4E3E48", "FFFFFF");
            Add(set, "palette.garden.blueberry", "7A5E4C", "6CA758", "F4B5C6", "4C69C6", "97ADE6", "3D4A5E", "FFFFFF");
            Add(set, "palette.garden.lavender", "8CA488", "AEBFAA", "98BE6C", "9474D2", "C8B3EE", "4F4A66", "FFFFFF");
            Add(set, "palette.garden.golden_wheat", "7DB35A", "9CCB71", "E8BE55", "F5DC8E", "C99A42", "6B5A3E", "FFFFFF");
            Add(set, "palette.garden.meadow_indigo", "5E9050", "77AE62", "3D52B8", "A7C0EA", "6B85D8", "34395E", "FFFFFF");
            Add(set, "palette.garden.sunflower", "6AA14A", "7BB759", "F8C531", "7A4A2B", "FFE27E", "5A4632", "FFFFFF");
            Add(set, "palette.garden.dwarf_apple", "8E6650", "74B25A", "E04B42", "FFF6F2", "F4A6BB", "3F5A40", "FFFFFF");
            Add(set, "palette.garden.round_melon", "5E9A46", "79B55A", "F4CF46", "C8D98A", "7FAF5A", "4A5E3E", "FFFFFF");
            Add(set, "palette.garden.sweet_pea", "6CA552", "86BE68", "F5A3C6", "B69BE0", "D9BE84", "4E4A62", "FFFFFF");
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

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.magenta;

        public static uint PaletteId(string id) => string.IsNullOrEmpty(id) ? 0u : ContentId.FromSource(id).Value;

        public static string SetPalette(string objectName)
        {
            switch (objectName)
            {
                case Ground: return "palette.garden.ground";
                case Bed: return "palette.garden.bed";
                case Cover: return "palette.garden.cover";
                case Path: return "palette.garden.path";
                case Fence: return "palette.garden.fence";
                case Can: return "palette.garden.watering_can";
                case Basket: return "palette.garden.basket";
                default: return null;
            }
        }

        /// <summary>An SH_EnvironmentOpaque material with MAT_Environment_Common's values; a negative rim keeps them.</summary>
        private static Material EnsureEnvironmentMaterial(string path, string name, float rim, Color rimColor, float rimPower)
        {
            var shader = Shader.Find(GroomingPipeline.EnvironmentShader)
                         ?? throw new InvalidOperationException("Shader not found: " + GroomingPipeline.EnvironmentShader);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
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
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static MeshRenderer AddRenderer(GameObject go, Mesh mesh, Material material, ShadowCastingMode shadows)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
            return renderer;
        }

        private static WorldChunk.PaletteBinding Binding(MeshRenderer renderer, string palette) => new WorldChunk.PaletteBinding
        {
            renderer = renderer,
            paletteId = PaletteId(palette),
            brightness = WorldChunk.NeutralBrightness,
        };

        /// <summary>A model under <paramref name="parent"/> at <paramref name="holder"/> (local), with its renderer and binding.</summary>
        private static MeshRenderer Place(Transform parent, (Mesh mesh, Matrix4x4 matrix) model, string name, Matrix4x4 holder, Material material,
            string palette, ShadowCastingMode shadows, List<WorldChunk.PaletteBinding> bindings)
        {
            if (model.mesh == null)
                return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var world = holder * model.matrix;
            go.transform.SetLocalPositionAndRotation(world.GetColumn(3), world.rotation);
            go.transform.localScale = world.lossyScale;
            var renderer = AddRenderer(go, model.mesh, material, shadows);
            bindings.Add(Binding(renderer, palette));
            return renderer;
        }

        private static void SetBindings(GameObject root, List<WorldChunk.PaletteBinding> bindings)
        {
            var chunk = root.GetComponent<WorldChunk>() ?? root.AddComponent<WorldChunk>();
            chunk.SetAuthoring(bindings.ToArray(), new WanderArea(Vector3.zero, 0.5f), null, Array.Empty<Transform>(), null,
                Array.Empty<Transform>(), Array.Empty<Transform>());
        }

        // ---------------------------------------------------------------- the plants

        /// <summary>A plant stage's footprint radius (m, every point above 1 cm under the soil) and height, root space.</summary>
        public static (float radius, float height) Measure(List<Vector3> points)
        {
            float r = 0f, h = 0f;
            foreach (var p in points)
            {
                if (p.y > -0.01f)
                    r = Mathf.Max(r, new Vector2(p.x, p.z).magnitude);
                h = Mathf.Max(h, p.y);
            }
            return (r, h);
        }

        /// <summary>
        /// The 39 stage prefabs (root at the soil, a WorldChunk binding the plant's palette row; the model under it, turned
        /// half a turn like every set model) and DEF_GardenPlantVisuals with each ripe plant's measured radius and height.
        /// </summary>
        public static GardenPlantVisuals BuildPlants(Material material, List<string> report)
        {
            var meshes = ModelMeshes(PlantsModel);
            var entries = new List<GardenPlantVisuals.Entry>();
            var missing = new List<string>();
            var budget = new List<string>();
            Directory.CreateDirectory(PlantPrefabFolder);
            float widest = 0f, tallest = 0f;
            for (int i = 0; i < Plants.Length; i++)
            {
                var (key, stem) = Plants[i];
                var entry = new GardenPlantVisuals.Entry { plantKey = PlantKey(i) };
                for (int stage = 1; stage <= 3; stage++)
                {
                    string name = PlantObject(stem, stage);
                    var model = Find(meshes, name);
                    if (model.mesh == null)
                    {
                        missing.Add(name);
                        continue;
                    }
                    int tris = Triangles(model.mesh);
                    if (tris < PlantMinTriangles || tris > PlantMaxTriangles)
                        budget.Add($"{name} {tris}");
                    var prefab = BuildPlantPrefab(stem, stage, model, material, PlantPalette(key));
                    if (stage == 1)
                        entry.stage1 = prefab;
                    else if (stage == 2)
                        entry.stage2 = prefab;
                    else
                    {
                        entry.stage3 = prefab;
                        var (radius, height) = Measure(Points(model, Holder(Vector3.zero, 180f)));
                        entry.radius = radius;
                        entry.height = height;
                        widest = Mathf.Max(widest, radius);
                        tallest = Mathf.Max(tallest, height);
                    }
                }
                entries.Add(entry);
            }
            if (missing.Count == Plants.Length * 3)
            {
                report.Add("WARNING: no plant in " + PlantsModel + ".fbx");
                return null;
            }
            var visuals = AssetDatabase.LoadAssetAtPath<GardenPlantVisuals>(GardenLayout.PlantsAssetPath);
            if (visuals == null)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(GardenLayout.PlantsAssetPath));
                visuals = ScriptableObject.CreateInstance<GardenPlantVisuals>();
                AssetDatabase.CreateAsset(visuals, GardenLayout.PlantsAssetPath);
            }
            visuals.entries = entries.ToArray();
            EditorUtility.SetDirty(visuals);
            report.Add($"plants: {entries.Count} x 3 stages, widest ripe radius {widest * 100f:F1} cm, tallest {tallest * 100f:F0} cm"
                       + (missing.Count > 0 ? " (WARNING: missing " + string.Join(", ", missing) + ")" : "")
                       + (budget.Count > 0 ? " (WARNING: outside 150 to 900 triangles: " + string.Join(", ", budget) + ")" : ", every stage within 150 to 900 triangles"));
            return visuals;
        }

        private static GameObject BuildPlantPrefab(string stem, int stage, (Mesh mesh, Matrix4x4 matrix) model, Material material, string palette)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(PlantPrefabPath(stem, stage)));
            try
            {
                var bindings = new List<WorldChunk.PaletteBinding>();
                Place(root.transform, model, "Model", Holder(Vector3.zero, 180f), material, palette, ShadowCastingMode.On, bindings);
                SetBindings(root, bindings);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PlantPrefabPath(stem, stage), out bool success);
                if (!success)
                    throw new InvalidOperationException("Could not save " + PlantPrefabPath(stem, stage));
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------------------------------------------------------- the set prefab

        public static GameObject BuildSetPrefab(Material setMaterial, Material propsMaterial, GardenPlantVisuals visuals, List<string> report)
        {
            var meshes = ModelMeshes(SetModel);
            var missing = SetObjects.Where(n => Find(meshes, n).mesh == null).ToList();
            if (missing.Count > 0)
                report.Add("WARNING: missing in " + SetModel + ".fbx: " + string.Join(", ", missing) + " (skipped)");
            if (Find(meshes, Bed).mesh == null)
                return null;
            int setTris = SetObjects.Sum(n => Triangles(Find(meshes, n).mesh));
            var root = new GameObject(GardenLayout.SetPrefabName);
            try
            {
                var view = root.AddComponent<GardenSetView>();
                var bindings = new List<WorldChunk.PaletteBinding>();
                var footprints = new List<(string name, List<Vector2> hull)>();
                var decor = new GameObject("Set").transform;
                decor.SetParent(root.transform, false);

                // The meadow floor, the stepping stones and the fence at the origin.
                Place(decor, Find(meshes, Ground), Ground, Holder(Vector3.zero, 180f), setMaterial, SetPalette(Ground), ShadowCastingMode.Off, bindings);
                Place(decor, Find(meshes, Path), Path, Holder(Vector3.zero, 180f), setMaterial, SetPalette(Path), ShadowCastingMode.Off, bindings);
                foreach (var stone in Components(Find(meshes, Path), Holder(Vector3.zero, 180f), -0.004f))
                    footprints.Add(("stone", Hull(stone)));
                Place(decor, Find(meshes, Fence), Fence, Holder(Vector3.zero, 180f), setMaterial, SetPalette(Fence), ShadowCastingMode.On, bindings);
                var fence = Points(Find(meshes, Fence), Holder(Vector3.zero, 180f));
                foreach (var part in fence.GroupBy(p => Mathf.FloorToInt(p.x / 0.5f)))
                    footprints.Add(("fence", Hull(part.ToList())));

                // The five beds: plot 0 at the front, alternating sides of the path; anchors at the soil; covers on 3 and 4.
                var bedsRoot = new GameObject("Beds").transform;
                bedsRoot.SetParent(root.transform, false);
                var bedModel = Find(meshes, Bed);
                var coverModel = Find(meshes, Cover);
                view.beds = new Transform[GardenLayout.PlotCount];
                view.plantAnchors = new Transform[GardenLayout.PlotCount];
                view.lockedCovers = new GameObject[GardenLayout.PlotCount];
                view.bedSizes = new Vector2[GardenLayout.PlotCount];
                var bedOutline = Points(bedModel, Holder(Vector3.zero, 180f));
                for (int i = 0; i < GardenLayout.PlotCount; i++)
                {
                    var bed = new GameObject("Bed_" + i).transform;
                    bed.SetParent(bedsRoot, false);
                    bed.localPosition = BedPosition(i);
                    Place(bed, bedModel, Bed, Holder(Vector3.zero, 180f), propsMaterial, SetPalette(Bed), ShadowCastingMode.On, bindings);
                    var anchor = new GameObject("PlantAnchor").transform;
                    anchor.SetParent(bed, false);
                    anchor.localPosition = new Vector3(0f, SoilTop, 0f);
                    view.beds[i] = bed;
                    view.plantAnchors[i] = anchor;
                    view.bedSizes[i] = TouchSize;
                    footprints.Add(("bed " + i, Hull(bedOutline.Select(p => p + BedPosition(i)).ToList())));
                    if (CoveredByDefault(i) && coverModel.mesh != null)
                    {
                        var cover = new GameObject("Cover").transform;
                        cover.SetParent(bed, false);
                        Place(cover, coverModel, Cover, Holder(Vector3.zero, 180f), propsMaterial, SetPalette(Cover), ShadowCastingMode.On, bindings);
                        view.lockedCovers[i] = cover.gameObject;
                        footprints.Add(("cover " + i, Hull(Points(coverModel, Holder(BedPosition(i), 180f)))));
                    }
                }

                // The watering can and the basket at the side of the path near the front, on the meadow.
                var can = new GameObject("WateringCan").transform;
                can.SetParent(root.transform, false);
                can.localPosition = CanAt;
                Place(can, Find(meshes, Can), Can, Holder(Vector3.zero, CanYaw), propsMaterial, SetPalette(Can), ShadowCastingMode.On, bindings);
                view.wateringCan = can;
                footprints.Add(("can", Hull(Points(Find(meshes, Can), Holder(CanAt, CanYaw)))));
                var basket = new GameObject("Basket").transform;
                basket.SetParent(root.transform, false);
                basket.localPosition = BasketAt;
                Place(basket, Find(meshes, Basket), Basket, Holder(Vector3.zero, BasketYaw), propsMaterial, SetPalette(Basket), ShadowCastingMode.On, bindings);
                view.basket = basket;
                footprints.Add(("basket", Hull(Points(Find(meshes, Basket), Holder(BasketAt, BasketYaw)))));

                // T38's backdrop hills behind the fence, then the kit's grass and daisies where they fit.
                report.Add(PlaceBackdrop(decor, setMaterial, bindings, footprints));
                report.Add(PlaceKit(decor, setMaterial, bindings, footprints));

                // Nothing enters anything: every pair of footprints keeps PropGap (convex hulls seen from above).
                report.Add(CheckFootprints(footprints));
                if (visuals != null)
                    report.Add(CheckPlantsInBeds(visuals));

                view.setCamera = AddCamera(root.transform);
                view.sun = AddLight(root.transform);
                ApplyAmbient(view);
                report.Add(CheckFraming(visuals));
                SetBindings(root, bindings);

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(GardenLayout.SetPrefabPath));
                var saved = PrefabUtility.SaveAsPrefabAsset(root, GardenLayout.SetPrefabPath, out bool success);
                if (!success)
                    throw new InvalidOperationException("Could not save " + GardenLayout.SetPrefabPath);
                report.Add($"{GardenLayout.SetPrefabPath}: {saved.GetComponentsInChildren<Renderer>(true).Length} renderers, "
                           + $"{saved.GetComponentsInChildren<MeshFilter>(true).Sum(f => Triangles(f.sharedMesh))} triangles in the prefab, "
                           + $"set models {setTris} triangles (budget {SetPropsBudget})" + (setTris > SetPropsBudget ? " (WARNING: over budget)" : ""));
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// GR_SetBackdrop (T38's meadow and round hills, shared through Activity_Shared), twice its size, centred under the
        /// garden, its floor 2 cm under the meadow: its hills rise behind the fence; its little daisies count as props.
        /// </summary>
        private static string PlaceBackdrop(Transform decor, Material material, List<WorldChunk.PaletteBinding> bindings,
            List<(string name, List<Vector2> hull)> footprints)
        {
            var backdrop = GroomingPipeline.ModelMeshes(GroomingPipeline.SetModel)
                .FirstOrDefault(m => m.name == NutDropPipeline.BackdropName || m.mesh.name == NutDropPipeline.BackdropName);
            if (backdrop.mesh == null)
                return "WARNING: " + NutDropPipeline.BackdropName + " missing (" + GroomingPipeline.SetModel + ".fbx): no hills behind";
            var position = new Vector3(0f, -BackdropFloor * BackdropScale - BackdropSink, BackdropZ);
            var holder = new GameObject("Backdrop").transform;
            holder.SetParent(decor, false);
            holder.localPosition = position;
            holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
            holder.localScale = Vector3.one * BackdropScale;
            var go = new GameObject(NutDropPipeline.BackdropName);
            go.transform.SetParent(holder, false);
            go.transform.SetLocalPositionAndRotation(backdrop.matrix.GetColumn(3), backdrop.matrix.rotation);
            go.transform.localScale = backdrop.matrix.lossyScale;
            var renderer = AddRenderer(go, backdrop.mesh, material, ShadowCastingMode.Off);
            bindings.Add(Binding(renderer, GroomingPipeline.PaletteBackdrop));
            // Whatever of it rises over the meadow inside the garden (its daisies, its near hills' feet) must clear the props.
            var matrix = Holder(position, 180f, BackdropScale) * backdrop.matrix;
            var above = new List<Vector3>();
            foreach (var v in backdrop.mesh.vertices)
            {
                var p = matrix.MultiplyPoint3x4(v);
                if (p.y > 0.004f && Mathf.Abs(p.x) < 3f && p.z < 4.6f && p.z > -1.6f)
                    above.Add(p);
            }
            foreach (var part in Clusters(above, 0.12f))
                footprints.Add(("backdrop", Hull(part)));
            return $"backdrop: {above.Count} points over the meadow inside the garden";
        }

        /// <summary>The kit's grass and daisies at <see cref="KitSpots"/>, each footprint clear of everything placed.</summary>
        private static string PlaceKit(Transform decor, Material material, List<WorldChunk.PaletteBinding> bindings,
            List<(string name, List<Vector2> hull)> footprints)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            var row = new GameObject("Kit").transform;
            row.SetParent(decor, false);
            uint seed = 0x47415244u;
            int placed = 0, left = 0;
            var skipped = new HashSet<string>();
            for (int i = 0; i < KitSpots.Length; i++)
            {
                var (meshName, palette) = KitPieces[i % KitPieces.Length];
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{NutDropPipeline.KitFolder}/{meshName}.asset");
                string path = mesh != null ? AssetDatabase.GetAssetPath(mesh) : null;
                if (mesh == null || settings == null || settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)) == null)
                {
                    skipped.Add(meshName);
                    continue;
                }
                seed = seed * 1664525u + 1013904223u;
                float scale = 1f + 0.2f * ((seed >> 8) & 255u) / 255f;
                float yaw = (seed >> 4) % 360u;
                var matrix = Matrix4x4.TRS(new Vector3(KitSpots[i].x, -mesh.bounds.min.y * scale, KitSpots[i].y), Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
                var points = mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToList();
                var hull = Hull(points);
                if (footprints.Any(f => Gap(hull, f.hull) < PropGap))
                {
                    left++;
                    continue;
                }
                var go = new GameObject(meshName + "_" + placed);
                go.transform.SetParent(row, false);
                go.transform.SetLocalPositionAndRotation(matrix.GetColumn(3), matrix.rotation);
                go.transform.localScale = Vector3.one * scale;
                var renderer = AddRenderer(go, mesh, material, ShadowCastingMode.Off);
                bindings.Add(Binding(renderer, palette));
                footprints.Add(("kit " + meshName, hull));
                placed++;
            }
            return $"kit: {placed} grass and flower pieces, {left} left out (no room)"
                   + (skipped.Count > 0 ? " (WARNING: not explicit kit entries, skipped: " + string.Join(", ", skipped) + ")" : "");
        }

        // ---------------------------------------------------------------- measures

        /// <summary>
        /// The connected pieces of a mesh (vertices joined by its triangles), each piece's points over <paramref name="minY"/>
        /// in the holder's space: the path's stones, one by one.
        /// </summary>
        public static List<List<Vector3>> Components((Mesh mesh, Matrix4x4 matrix) model, Matrix4x4 holder, float minY)
        {
            var result = new List<List<Vector3>>();
            if (model.mesh == null)
                return result;
            var vertices = model.mesh.vertices;
            var triangles = model.mesh.triangles;
            var parent = new int[vertices.Length];
            for (int i = 0; i < parent.Length; i++)
                parent[i] = i;
            int Root(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }
                return i;
            }
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = Root(triangles[t]);
                for (int k = 1; k < 3; k++)
                {
                    int b = Root(triangles[t + k]);
                    if (a != b)
                        parent[b] = a;
                }
            }
            // Split vertices (same position, other normals) belong to the piece of any vertex at their place.
            var byPlace = new Dictionary<Vector3, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (byPlace.TryGetValue(vertices[i], out int other))
                {
                    int a = Root(other), b = Root(i);
                    if (a != b)
                        parent[b] = a;
                }
                else
                    byPlace[vertices[i]] = i;
            }
            var matrix = holder * model.matrix;
            var groups = new Dictionary<int, List<Vector3>>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = matrix.MultiplyPoint3x4(vertices[i]);
                if (p.y <= minY)
                    continue;
                int r = Root(i);
                if (!groups.TryGetValue(r, out var list))
                    groups[r] = list = new List<Vector3>();
                list.Add(p);
            }
            result.AddRange(groups.Values);
            return result;
        }

        /// <summary>Greedy clusters of points seen from above (a point joins a cluster when within <paramref name="reach"/> of one of its points).</summary>
        public static List<List<Vector3>> Clusters(List<Vector3> points, float reach)
        {
            var clusters = new List<List<Vector3>>();
            float r2 = reach * reach;
            foreach (var p in points)
            {
                List<Vector3> home = null;
                for (int c = clusters.Count - 1; c >= 0; c--)
                {
                    var cluster = clusters[c];
                    bool near = false;
                    foreach (var q in cluster)
                    {
                        float dx = p.x - q.x, dz = p.z - q.z;
                        if (dx * dx + dz * dz <= r2)
                        {
                            near = true;
                            break;
                        }
                    }
                    if (!near)
                        continue;
                    if (home == null)
                    {
                        home = cluster;
                        home.Add(p);
                    }
                    else
                    {
                        home.AddRange(cluster);
                        clusters.RemoveAt(c);
                    }
                }
                if (home == null)
                    clusters.Add(new List<Vector3> { p });
            }
            return clusters;
        }

        /// <summary>The convex hull of points seen from above (x, z), counterclockwise (monotone chain).</summary>
        public static List<Vector2> Hull(List<Vector3> points)
        {
            var pts = points.Select(p => new Vector2(p.x, p.z)).Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            if (pts.Count < 3)
                return pts;
            var hull = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                foreach (var p in pts)
                {
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f)
                        hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1);
                pts.Reverse();
            }
            return hull;
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        /// <summary>
        /// A lower bound of the gap between two convex footprints (m): the largest separation along the edge normals of
        /// both; negative when they overlap.
        /// </summary>
        public static float Gap(List<Vector2> a, List<Vector2> b)
        {
            if (a.Count == 0 || b.Count == 0)
                return float.MaxValue;
            float best = float.MinValue;
            foreach (var poly in new[] { a, b })
            {
                int n = poly.Count;
                if (n < 2)
                    continue;
                for (int i = 0; i < n; i++)
                {
                    var e = poly[(i + 1) % n] - poly[i];
                    if (e.sqrMagnitude < 1e-12f)
                        continue;
                    var normal = new Vector2(e.y, -e.x).normalized;
                    float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue;
                    foreach (var p in a)
                    {
                        float d = Vector2.Dot(p, normal);
                        minA = Mathf.Min(minA, d);
                        maxA = Mathf.Max(maxA, d);
                    }
                    foreach (var p in b)
                    {
                        float d = Vector2.Dot(p, normal);
                        minB = Mathf.Min(minB, d);
                        maxB = Mathf.Max(maxB, d);
                    }
                    best = Mathf.Max(best, Mathf.Max(minB - maxA, minA - maxB));
                }
            }
            return best;
        }

        /// <summary>Every pair of footprints apart by at least <see cref="PropGap"/> (a cover and its own bed excepted: it lies over it).</summary>
        public static string CheckFootprints(List<(string name, List<Vector2> hull)> footprints)
        {
            var problems = new List<string>();
            float smallest = float.MaxValue;
            for (int i = 0; i < footprints.Count; i++)
            {
                for (int j = i + 1; j < footprints.Count; j++)
                {
                    var (a, ha) = footprints[i];
                    var (b, hb) = footprints[j];
                    if (OwnCover(a, b) || (a == "fence" && b == "fence") || (a == "backdrop" && b == "backdrop"))
                        continue;
                    float gap = Gap(ha, hb);
                    smallest = Mathf.Min(smallest, gap);
                    if (gap < PropGap)
                        problems.Add($"{a} / {b} {gap * 100f:F1} cm");
                }
            }
            return $"footprints: {footprints.Count} checked, smallest gap {smallest * 100f:F1} cm"
                   + (problems.Count > 0 ? " (WARNING: " + string.Join(", ", problems.Distinct().Take(8)) + ")" : ", nothing in anything");
        }

        private static bool OwnCover(string a, string b)
        {
            if (a.StartsWith("bed ") && b.StartsWith("cover "))
                return a.Substring(4) == b.Substring(6);
            if (b.StartsWith("bed ") && a.StartsWith("cover "))
                return b.Substring(4) == a.Substring(6);
            return false;
        }

        /// <summary>
        /// Every ripe plant at a bed's anchor: every point above the soil inside the soil's rectangle with
        /// <see cref="SoilMargin"/> to spare (its rooted feet are in the soil by design).
        /// </summary>
        public static string CheckPlantsInBeds(GardenPlantVisuals visuals)
        {
            var problems = new List<string>();
            float tightest = float.MaxValue;
            foreach (var entry in visuals.entries)
            {
                if (entry.stage3 == null)
                    continue;
                float slack = SoilSlack(entry.stage3);
                tightest = Mathf.Min(tightest, slack);
                if (slack < SoilMargin)
                    problems.Add($"{entry.plantKey} {slack * 100f:F1} cm");
            }
            return $"ripe plants in their beds: tightest {tightest * 100f:F1} cm from the planks"
                   + (problems.Count > 0 ? " (WARNING: " + string.Join(", ", problems) + ")" : "");
        }

        /// <summary>The smallest distance (m) from a plant's points above the soil to the soil's rectangle edge (negative outside).</summary>
        public static float SoilSlack(GameObject plant)
        {
            float slack = float.MaxValue;
            foreach (var filter in plant.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                var matrix = plant.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (var v in filter.sharedMesh.vertices)
                {
                    var p = matrix.MultiplyPoint3x4(v);
                    if (p.y < -0.004f)
                        continue;
                    slack = Mathf.Min(slack, Mathf.Min(SoilHalf.x - Mathf.Abs(p.x), SoilHalf.y - Mathf.Abs(p.z)));
                }
            }
            return slack;
        }

        /// <summary>A point of the set on the 1080 × 2340 screen through the set camera (px, y up from the bottom).</summary>
        public static Vector2 ToScreen(Vector3 point)
        {
            var rotation = Quaternion.LookRotation(CameraTarget - CameraAt, Vector3.up);
            var c = Quaternion.Inverse(rotation) * (point - CameraAt);
            float t = Mathf.Tan(CameraFov * 0.5f * Mathf.Deg2Rad);
            float depth = Mathf.Max(0.05f, c.z);
            float aspect = ScreenWidth / (float)ScreenHeight;
            var ndc = new Vector2(c.x / (depth * t * aspect), c.y / (depth * t));
            return new Vector2((ndc.x + 1f) * 0.5f * ScreenWidth, (ndc.y + 1f) * 0.5f * ScreenHeight);
        }

        /// <summary>
        /// Every bed (its eight corners, its cover) and the tallest ripe plant standing in it, on the screen: between the
        /// overlay's bottom sheet and its top bar, inside the screen's width. Returns the margins left (px).
        /// </summary>
        public static (float bottom, float top, float side) FramingMargins(float tallest, float widest)
        {
            float bottom = float.MaxValue, top = float.MaxValue, side = float.MaxValue;
            for (int i = 0; i < GardenLayout.PlotCount; i++)
            {
                var c = BedPosition(i);
                for (int k = 0; k < 8; k++)
                {
                    var p = c + new Vector3((k & 1) == 0 ? -BedHalf.x : BedHalf.x, (k & 2) == 0 ? 0f : PlankTop + 0.08f, (k & 4) == 0 ? -BedHalf.y : BedHalf.y);
                    var s = ToScreen(p);
                    bottom = Mathf.Min(bottom, s.y - BottomSheet);
                    top = Mathf.Min(top, ScreenHeight - TopBar - s.y);
                    side = Mathf.Min(side, Mathf.Min(s.x, ScreenWidth - s.x));
                }
                // The tallest ripe plant's top, its widest reach toward the camera.
                for (int k = 0; k < 4; k++)
                {
                    var p = c + new Vector3((k & 1) == 0 ? -widest : widest, SoilTop + tallest, (k & 2) == 0 ? -widest : widest);
                    var s = ToScreen(p);
                    top = Mathf.Min(top, ScreenHeight - TopBar - s.y);
                    side = Mathf.Min(side, Mathf.Min(s.x, ScreenWidth - s.x));
                }
            }
            foreach (var p in new[] { CanAt, BasketAt })
            {
                var s = ToScreen(p + new Vector3(0f, 0f, -0.2f));
                bottom = Mathf.Min(bottom, s.y - BottomSheet);
            }
            return (bottom, top, side);
        }

        private static string CheckFraming(GardenPlantVisuals visuals)
        {
            float tallest = 0f, widest = 0f;
            if (visuals != null)
                foreach (var e in visuals.entries)
                {
                    tallest = Mathf.Max(tallest, e.height);
                    widest = Mathf.Max(widest, e.radius);
                }
            var (bottom, top, side) = FramingMargins(tallest, widest);
            bool ok = bottom >= 0f && top >= 0f && side >= 0f;
            return $"framing: beds and the tallest ripe plant ({tallest * 100f:F0} cm) {bottom:F0} px over the bottom sheet, {top:F0} px under the top bar, "
                   + $"{side:F0} px from the sides" + (ok ? "" : " (WARNING: something under the overlay)");
        }

        // ---------------------------------------------------------------- camera, light, ambient

        private static Camera AddCamera(Transform parent)
        {
            var go = new GameObject("Camera");
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(CameraAt, Quaternion.LookRotation(CameraTarget - CameraAt, Vector3.up));
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = CameraFov;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 40f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            var urp = go.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            urp.renderShadows = true;
            urp.requiresDepthOption = CameraOverrideOption.Off;
            urp.requiresColorOption = CameraOverrideOption.Off;
            return camera;
        }

        private const string DayProfile = "Assets/_Game/Art/Lighting/Grassland/LP_Grassland_Day.asset";

        /// <summary>The Prairie's day, from the top left of the screen and a little behind the camera (charte, section 5).</summary>
        private static Light AddLight(Transform parent)
        {
            var profile = AssetDatabase.LoadAssetAtPath<LightingProfile>(DayProfile);
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(50f, 28f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = profile != null ? profile.sunColor : new Color(1f, 0.945f, 0.863f);
            light.intensity = profile != null ? profile.sunIntensity : 1.05f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = profile != null ? Mathf.Min(0.55f, profile.shadowStrength) : 0.55f;
            light.enabled = false;
            return light;
        }

        private static void ApplyAmbient(GardenSetView view)
        {
            var profile = AssetDatabase.LoadAssetAtPath<LightingProfile>(DayProfile);
            if (profile == null)
                return;
            var warm = new Color(1f, 0.93f, 0.8f);
            view.ambientSky = Color.Lerp(profile.ambientTop, warm, 0.1f);
            view.ambientEquator = Color.Lerp(profile.ambientEquator, warm, 0.15f);
            view.ambientGround = Color.Lerp(profile.ambientBottom, warm, 0.08f);
        }

        // ---------------------------------------------------------------- Addressables

        /// <summary>
        /// The set and the plants' visuals in Activity_Garden (the stage prefabs and both models come along as their
        /// dependencies, in the same bundle); T38's set model stays an explicit entry of Activity_Shared.
        /// </summary>
        public static string EnsureAddressables(GameObject prefab, GardenPlantVisuals visuals)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = EnsureGroup(settings, GardenLayout.Group);
            var set = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)), group, false, false);
            set.address = GardenLayout.SetAddress;
            var plants = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(GardenLayout.PlantsAssetPath), group, false, false);
            plants.address = GardenLayout.PlantsAddress;
            var setGuid = AssetDatabase.AssetPathToGUID(GroomingPipeline.ModelPath(GroomingPipeline.SetModel));
            if (!string.IsNullOrEmpty(setGuid))
            {
                var shared = EnsureGroup(settings, NutDropPipeline.SharedGroup);
                var model = settings.CreateOrMoveEntry(setGuid, shared, false, false);
                model.address = NutDropPipeline.SharedSetAddress;
                EditorUtility.SetDirty(shared);
            }
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            return $"Addressables {GardenLayout.Group}: {set.address}, {plants.address}";
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name) ?? settings.CreateGroup(name, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            var update = group.GetSchema<ContentUpdateGroupSchema>() ?? group.AddSchema<ContentUpdateGroupSchema>();
            update.StaticContent = true;
            EditorUtility.SetDirty(group);
            return group;
        }

        // ---------------------------------------------------------------- captures

        /// <summary>The ripe plants shown in plots 0 to 4 by the set capture (the tallest at the back, under the top bar).</summary>
        public static readonly string[] CapturePlantsInBeds = { "plant.poppy", "plant.wild_strawberry", "plant.dwarf_apple", "plant.daisy", "plant.sunflower" };

        /// <summary>
        /// T43_garden_set.png (1080 × 2340 through the set camera: a ripe plant in each bed, the covers off) and
        /// T43_garden_plants.png (all 39 stages, a row per stage, the plants in catalogue order), in the world scene with the
        /// game shaders, palettes and the set's own light; nothing saved, the world scene reopened afterwards.
        /// </summary>
        public static string Capture(GameObject prefab, GardenPlantVisuals visuals)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != WorldCapture.ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "WARNING: captures skipped (save or discard the changes in " + active.path + ")";
            EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            if (database == null)
                return "WARNING: captures skipped (no content database)";
            var registry = database.CreateBuilder().Build();
            var spawned = new List<GameObject>();
            var lights = new List<Light>();
            var savedMode = RenderSettings.ambientMode;
            var savedSky = RenderSettings.ambientSkyColor;
            var savedEquator = RenderSettings.ambientEquatorColor;
            var savedGround = RenderSettings.ambientGroundColor;
            var savedSun = RenderSettings.sun;
            int missing = 0;
            try
            {
                using (var palettes = new PaletteTable(registry))
                {
                    var set = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    set.hideFlags = HideFlags.DontSave;
                    set.transform.SetPositionAndRotation(GardenLayout.SetOrigin, Quaternion.identity);
                    spawned.Add(set);
                    var view = set.GetComponent<GardenSetView>();
                    missing += set.GetComponent<WorldChunk>().Bind(palettes);
                    foreach (var cover in view.lockedCovers)
                        if (cover != null)
                            cover.SetActive(false);
                    for (int i = 0; i < GardenLayout.PlotCount; i++)
                        if (visuals.TryGet(CapturePlantsInBeds[i], out var entry) && entry.stage3 != null)
                            missing += Spawn(entry.stage3, view.plantAnchors[i].position, palettes, spawned);
                    foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                        if (light.enabled && light != view.sun)
                        {
                            lights.Add(light);
                            light.enabled = false;
                        }
                    view.sun.enabled = true;
                    RenderSettings.sun = view.sun;
                    RenderSettings.ambientMode = AmbientMode.Trilight;
                    RenderSettings.ambientSkyColor = view.ambientSky;
                    RenderSettings.ambientEquatorColor = view.ambientEquator;
                    RenderSettings.ambientGroundColor = view.ambientGround;
                    var camera = view.setCamera;
                    camera.aspect = ScreenWidth / (float)ScreenHeight;
                    CaptureUtility.Render(camera, ScreenWidth, ScreenHeight, CaptureSet);

                    // Every stage: a row per stage (1 at the front), the plants side by side, seen from the set camera's angle.
                    var origin = GardenLayout.SetOrigin + new Vector3(0f, 0f, 40f);
                    const float Step = 0.82f;
                    for (int p = 0; p < visuals.entries.Length; p++)
                    {
                        var e = visuals.entries[p];
                        var stages = new[] { e.stage1, e.stage2, e.stage3 };
                        for (int s = 0; s < 3; s++)
                            if (stages[s] != null)
                                missing += Spawn(stages[s], origin + new Vector3((p - (visuals.entries.Length - 1) * 0.5f) * Step, 0f, s * 1.6f), palettes, spawned);
                    }
                    var rowCamera = Object.Instantiate(camera.gameObject).GetComponent<Camera>();
                    rowCamera.gameObject.hideFlags = HideFlags.DontSave;
                    spawned.Add(rowCamera.gameObject);
                    var center = origin + new Vector3(0f, 0.35f, 1.6f);
                    var direction = (CameraTarget - CameraAt).normalized;
                    rowCamera.transform.SetPositionAndRotation(center - direction * 21f, Quaternion.LookRotation(direction, Vector3.up));
                    rowCamera.clearFlags = CameraClearFlags.SolidColor;
                    rowCamera.backgroundColor = new Color(0.93f, 0.95f, 0.9f);
                    rowCamera.fieldOfView = 15f;
                    rowCamera.farClipPlane = 60f;
                    rowCamera.aspect = 2400f / 900f;
                    CaptureUtility.Render(rowCamera, 2400, 900, CapturePlants);
                }
                return $"captures: {CaptureSet}, {CapturePlants}" + (missing > 0 ? $" (WARNING: {missing} palettes unknown)" : "");
            }
            finally
            {
                foreach (var light in lights)
                    if (light != null)
                        light.enabled = true;
                RenderSettings.sun = savedSun;
                RenderSettings.ambientMode = savedMode;
                RenderSettings.ambientSkyColor = savedSky;
                RenderSettings.ambientEquatorColor = savedEquator;
                RenderSettings.ambientGroundColor = savedGround;
                foreach (var go in spawned)
                    if (go != null)
                        Object.DestroyImmediate(go);
                EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
            }
        }

        private static int Spawn(GameObject prefab, Vector3 position, PaletteTable palettes, List<GameObject> spawned)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            spawned.Add(go);
            var chunk = go.GetComponent<WorldChunk>();
            return chunk != null ? chunk.Bind(palettes) : 0;
        }
    }
}
