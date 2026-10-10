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
    /// T42 « Expéditions », the postcard set of Prés fleuris (doc 01 Contenu, section 7.1: « l'équipe pique-nique au pied
    /// d'une colline couverte de marguerites, papillons blancs autour »; collision rule): the import settings of agent EP's
    /// models (Art/Activities/Expeditions/PC_FlowerMeadows.fbx: the meadow floor, the gingham blanket, the picnic basket, the
    /// hill covered with daisies, two white butterflies), their palette rows and materials, the set prefab
    /// PF_Activity_Postcard_FlowerMeadows (T38's backdrop hills behind, the Prairie kit's grass and daisies in front, the
    /// butterflies in the air, a portrait camera for 512 × 768 and a warm sun), the team's seats measured on every species of
    /// the catalog so no animal enters another or a prop, the Addressables entry in Activity_Expeditions, and the
    /// destination card art EXP_flower_meadows.png (the set without animals, rendered with the game shaders). Missing models
    /// are listed and skipped, never invented. Idempotent. Run after the animals (Measure Bodies), the Prairie kit and Build
    /// Grooming (T38). Entry for the main session: <see cref="BuildAll"/> (execute_code), returns a short report.
    /// </summary>
    public static class PostcardSetPipeline
    {
        public const string Destination = PostcardSetLayout.FlowerMeadows;
        public const string ModelsFolder = "Assets/_Game/Art/Activities/Expeditions";
        public const string Model = "PC_FlowerMeadows";
        public const string PalettesPath = "Assets/_Game/Content/Palettes/DEF_Palettes_Postcards.asset";
        public const string SetMaterialPath = "Assets/_Game/Art/Materials/MAT_Postcard_Set.mat";
        public const string PropsMaterialPath = "Assets/_Game/Art/Materials/MAT_Postcard_Props.mat";

        public static string PrefabPath => "Assets/_Game/Prefabs/Activities/" + PostcardSetLayout.PrefabName(Destination) + ".prefab";

        public static string CardArtPath => PostcardLayout.CardArtFolder + "/" + PostcardLayout.CardArtName(Destination) + ".png";

        public const string Meadow = "PC_Meadow";
        public const string Blanket = "PC_Blanket";
        public const string Basket = "PC_Basket";
        public const string Hill = "PC_DaisyHill";
        public const string ButterflyOpen = "PC_Butterfly_Open";
        public const string ButterflyUp = "PC_Butterfly_Up";
        public const string Daisy = "PC_Daisy";
        public const string DaisyClump = "PC_Daisy_Clump";

        // Agent EP's rows (pw_postcards.PC_PALETTES): (base, R, G, B, A, occlusion, accent).
        public const string PaletteBlanket = "palette.postcard.blanket";
        public const string PaletteBasket = "palette.postcard.basket";
        public const string PaletteHill = "palette.postcard.daisy_hill";
        public const string PaletteButterfly = "palette.postcard.butterfly_white";
        public const string PaletteMeadow = "palette.postcard.meadow";
        public const string PaletteDaisy = "palette.postcard.daisy";

        /// <summary>T38's set model (its backdrop hills are shared): an explicit entry of Activity_Shared, so no set bundle copies it.</summary>
        public const string SharedGroup = NutDropPipeline.SharedGroup;

        public const string SharedSetAddress = NutDropPipeline.SharedSetAddress;

        // ---------------- the set's layout (origin frame: the blanket's center on the meadow, the camera looking along +Z) ----------------

        /// <summary>
        /// T38's backdrop: twice its size, centred under the daisy hill (its floor's middle hole and its near-hill ring,
        /// 4.5 m out, both clear of the hill), its floor 2 cm under the meadow (its hills rise through it), its little daisies
        /// behind and beside the hill.
        /// </summary>
        public const float BackdropScale = 2f;

        public const float BackdropZ = 2.15f;
        public const float BackdropSink = 0.02f;

        /// <summary>pw_grooming FLOOR_Z: the backdrop's floor under its origin.</summary>
        public const float BackdropFloor = -0.30f;

        /// <summary>The backdrop's yaws tried (degrees; 180 turns its hills behind the set) until none of its daisies grows in a prop.</summary>
        public static readonly float[] BackdropYaws = { 180f, 172f, 188f, 164f, 196f, 156f, 204f };

        public static readonly Vector3 HillAt = new Vector3(0.35f, 0f, 2.15f);
        public static readonly Vector3 BasketAt = new Vector3(0.70f, 0f, 0.10f);

        /// <summary>The basket's yaw on the blanket (180 shows its front, the cloth's flap, to the camera).</summary>
        public const float BasketYaw = 172f;

        /// <summary>
        /// The team's seats (x, z, yaw) for 1, 2 and 3 animals (<see cref="PostcardSetLayout.SeatIndex"/>): side by side on the
        /// blanket, the middle one a little back, the outer ones turned a little toward the middle, the basket at the right.
        /// </summary>
        public static readonly Vector3[] SeatPlan =
        {
            new Vector3(-0.12f, 0.05f, 180f),
            new Vector3(-0.40f, 0.05f, 172f), new Vector3(0.04f, 0.05f, 188f),
            new Vector3(-0.62f, 0f, 172f), new Vector3(-0.20f, 0.12f, 180f), new Vector3(0.22f, 0f, 188f),
        };

        /// <summary>
        /// The butterflies (model, position, turn of the body on the picture in degrees, scale). T42b: the first
        /// <see cref="NearButterflies"/> fly by the team at about head height, wings open and turned toward the camera (flat
        /// wings at eye height were seen edge-on, grey specks): about 0.19 m across, some 20 px in the 512 px card art; the
        /// renderer hides one that a bigger animal of the team would reach. The others flutter over the hill, smaller.
        /// </summary>
        public static readonly (string model, Vector3 at, float turn, float scale)[] Butterflies =
        {
            (ButterflyOpen, new Vector3(-0.66f, 0.92f, -0.05f), -22f, 1.8f),
            (ButterflyOpen, new Vector3(0.40f, 0.70f, -0.22f), 18f, 1.8f),
            (ButterflyOpen, new Vector3(-0.30f, 1.06f, 0.30f), 34f, 1.7f),
            (ButterflyOpen, new Vector3(0.15f, 1.62f, 1.55f), -10f, 2f),
            (ButterflyUp, new Vector3(-1.25f, 1.55f, 1.4f), 25f, 2f),
            (ButterflyOpen, new Vector3(0.95f, 1.30f, 1.0f), 40f, 2f),
        };

        public const int NearButterflies = 3;

        /// <summary>
        /// T42c: a little closer and lower than the first framing, so the team fills more of the card and the blanket's front
        /// edge sits at about 79 % of the postcard's height (the bottom band keeps the low grass and daisies).
        /// </summary>
        public static readonly Vector3 CameraAt = new Vector3(0.05f, 1.15f, -4.3f);
        public static readonly Vector3 CameraTarget = new Vector3(0.05f, 0.62f, 0.4f);

        /// <summary>The team's line where the framing is measured (z), and the tallest animal kept clear of the butterflies (m).</summary>
        public const float TeamDepth = 0.05f;

        public const float TallestAnimal = 0.66f * 1.38f;

        /// <summary>
        /// The grass and daisies along the bottom of the picture (in front of the blanket), each clear of the others: the
        /// kit's tufts and clover (own = false), and EP's daisies facing the camera (own = true, PC_FlowerMeadows.fbx). T42b:
        /// the kit's GL_Flower_A is made for the refuge's high camera; from the set camera its head was seen edge-on.
        /// </summary>
        private static readonly (string mesh, string palette, bool own)[] GrassKit =
        {
            ("GL_GrassTuft_M", "palette.grassland.grass", false), (Daisy, PaletteDaisy, true), ("GL_GrassTuft_L", "palette.grassland.grass", false),
            ("GL_GrassTuft_S", "palette.grassland.grass", false), ("GL_Clover_A", "palette.grassland.clover", false), (DaisyClump, PaletteDaisy, true),
            ("GL_GrassTuft_M", "palette.grassland.grass", false), ("GL_GrassTuft_L", "palette.grassland.grass", false), (Daisy, PaletteDaisy, true),
            ("GL_GrassTuft_S", "palette.grassland.grass", false), ("GL_Clover_A", "palette.grassland.clover", false), ("GL_GrassTuft_M", "palette.grassland.grass", false),
            (DaisyClump, PaletteDaisy, true),
        };

        /// <summary>EP's daisies face the camera (180), turned a little either way.</summary>
        public const float DaisyTurn = 18f;

        public const int GrassCount = 14;

        /// <summary>The foreground row's depth (m, toward the camera from the blanket): close pieces sit low in the picture.</summary>
        public const float GrassZ = -1.3f;

        /// <summary>Free space kept on the picture between a foreground piece and a seated animal or the basket (NDC units).</summary>
        public const float ScreenMargin = 0.012f;

        [MenuItem("Pocket Wilds/Activities/Build Postcard Sets (T42)", priority = 244)]
        public static void BuildMenu() => Debug.Log("[PostcardSetPipeline] " + BuildAll());

        /// <summary>Everything, in order; <paramref name="cardArt"/> false skips the card art (it opens the world scene).</summary>
        public static string BuildAll(bool cardArt = true)
        {
            var report = new List<string>();
            if (!ConfigureImporter(report))
                return string.Join("; ", report);
            EnsurePalettes();
            var set = EnsureEnvironmentMaterial(SetMaterialPath, "MAT_Postcard_Set", -1f, default, 0f);
            var props = EnsureEnvironmentMaterial(PropsMaterialPath, "MAT_Postcard_Props", 0.16f, new Color(1f, 0.97f, 0.9f), 3f);
            var prefab = BuildSetPrefab(set, props, report);
            if (prefab != null)
            {
                report.Add(EnsureAddressables(prefab));
                if (cardArt)
                    report.Add(RenderCardArt(prefab));
            }
            AssetDatabase.SaveAssets();
            return string.Join("; ", report);
        }

        // ---------------------------------------------------------------- model

        public static string ModelPath => $"{ModelsFolder}/{Model}.fbx";

        /// <summary>Static prop settings (like AssetImportRules' Prop_Static), set here because the folder is the activity's.</summary>
        public static bool ConfigureImporter(List<string> report)
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                report.Add("WARNING: " + ModelPath + " missing (agent EP's export): no set built");
                return false;
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
            // The blanket's checks keep the cloth's smooth normal (custom normals in the FBX).
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.meshCompression = ModelImporterMeshCompression.Low;
            if (changed)
                importer.SaveAndReimport();
            return true;
        }

        private static List<(Mesh mesh, Matrix4x4 matrix, string name)> ModelMeshes(string path)
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

        /// <summary>A prop's holder: Blender's front view becomes the set camera's (half a turn), then its own yaw.</summary>
        private static Matrix4x4 Holder(Vector3 position, float yaw, float scale) =>
            Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);

        private static List<Vector3> Points((Mesh mesh, Matrix4x4 matrix) model, Matrix4x4 holder)
        {
            var points = new List<Vector3>();
            if (model.mesh == null)
                return points;
            var matrix = holder * model.matrix;
            foreach (var v in model.mesh.vertices)
                points.Add(matrix.MultiplyPoint3x4(v));
            return points;
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
            // pw_postcards.PC_PALETTES, row for row.
            Add(set, PaletteBlanket, "FFFBF2", "B7D3EE", "7EA8DB", "6E99CF", "FFFBF2", "6D7096", "FFFFFF");
            Add(set, PaletteBasket, "D3A05E", "EBC98C", "E4BB7C", "F2A5AE", "FFF6EC", "6F5048", "FFFFFF");
            Add(set, PaletteHill, "8DC166", "BCD97C", "FAF6EC", "F2C24F", "76AA57", "4E6D5D", "FFFFFF");
            Add(set, PaletteButterfly, "8A7466", "FFFDF8", "F1DDBE", "8A7466", "8A7466", "A8917F", "FFFFFF");
            Add(set, PaletteDaisy, "6DA24F", "FFFDF7", "F2C24F", "7FB75C", "F4E6C6", "4E6D5D", "FFFFFF");
            Add(set, PaletteMeadow, "8DC166", "A6D07A", "7DB55C", "8DC166", "8DC166", "4E6D5D", "FFFFFF");
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

        /// <summary>An SH_EnvironmentOpaque material with MAT_Environment_Common's values; a negative rim keeps them.</summary>
        private static Material EnsureEnvironmentMaterial(string path, string name, float rim, Color rimColor, float rimPower)
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
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------------------------------------------------------------- the set prefab

        public static GameObject BuildSetPrefab(Material setMaterial, Material propsMaterial, List<string> report)
        {
            var meshes = ModelMeshes(ModelPath);
            var missing = new[] { Meadow, Blanket, Basket, Hill, ButterflyOpen, ButterflyUp }.Where(n => Find(meshes, n).mesh == null).ToList();
            if (missing.Count > 0)
                report.Add("WARNING: missing in " + Model + ".fbx: " + string.Join(", ", missing) + " (skipped)");
            var root = new GameObject(PostcardSetLayout.PrefabName(Destination));
            try
            {
                var view = root.AddComponent<PostcardSetView>();
                var data = new PostcardSetData();
                var bindings = new List<WorldChunk.PaletteBinding>();
                var obstacles = new List<Obstacle>();
                var origin = new GameObject("Origin").transform;
                origin.SetParent(root.transform, false);
                data.origin = origin;
                var decor = new GameObject("Set").transform;
                decor.SetParent(root.transform, false);

                // The meadow floor and the blanket at the origin.
                Place(decor, meshes, Meadow, Holder(Vector3.zero, 180f, 1f), setMaterial, PaletteMeadow, ShadowCastingMode.Off, bindings);
                var blanketMatrix = Holder(Vector3.zero, 180f, 1f);
                Place(decor, meshes, Blanket, blanketMatrix, propsMaterial, PaletteBlanket, ShadowCastingMode.Off, bindings);
                var blanket = Points(Find(meshes, Blanket), blanketMatrix);
                MeasureBlanket(blanket, data, report);

                // The hill covered with daisies behind the team, rooted in the meadow.
                var hillMatrix = Holder(HillAt, 180f, 1f);
                Place(decor, meshes, Hill, hillMatrix, setMaterial, PaletteHill, ShadowCastingMode.Off, bindings);
                var hill = Points(Find(meshes, Hill), hillMatrix);
                if (hill.Count > 0)
                    obstacles.Add(HillObstacle(hill));

                // The basket at the blanket's right end, standing on the cloth (the highest point under its foot), never in it.
                var basketModel = Find(meshes, Basket);
                var basketPoints = Points(basketModel, Holder(BasketAt, BasketYaw, 1f));
                float basketRadius = basketPoints.Count > 0 ? basketPoints.Max(p => new Vector2(p.x - BasketAt.x, p.z - BasketAt.z).magnitude) : 0.2f;
                float basketFoot = BlanketHeight(blanket, new Vector2(BasketAt.x, BasketAt.z), basketRadius) + 0.001f;
                var basketMatrix = Holder(new Vector3(BasketAt.x, basketFoot, BasketAt.z), BasketYaw, 1f);
                Place(decor, meshes, Basket, basketMatrix, propsMaterial, PaletteBasket, ShadowCastingMode.On, bindings);
                basketPoints = Points(basketModel, basketMatrix);
                float basketTop = basketPoints.Count > 0 ? basketPoints.Max(p => p.y) : 0.3f;
                if (basketPoints.Count > 0)
                    obstacles.Add(FlatObstacle(basketPoints, BasketYaw, basketFoot));

                // The kit's grass and daisies along the bottom of the picture.
                // The basket's box: the foreground keeps it in view too.
                var basketBox = new Bounds(BasketAt + Vector3.up * 0.15f, new Vector3(0.34f, 0.3f, 0.26f));
                if (basketPoints.Count > 0)
                {
                    basketBox = new Bounds(basketPoints[0], Vector3.zero);
                    foreach (var p in basketPoints)
                        basketBox.Encapsulate(p);
                }
                report.Add(PlaceGrass(decor, meshes, setMaterial, bindings, obstacles, data, basketBox));

                // T38's backdrop hills behind, turned until none of its daisies grows in a prop or under a seat.
                report.Add(PlaceBackdrop(decor, setMaterial, bindings, obstacles, hill, data));

                data.obstacles = obstacles.ToArray();
                report.Add(MeasureSeats(data, blanket));

                // The butterflies, clear of the hill, the basket, the team's heads and each other.
                report.Add(PlaceButterflies(root.transform, meshes, propsMaterial, bindings, data, hill, basketTop));

                data.bindings = bindings.ToArray();
                data.camera = AddCamera(root.transform, data);
                data.light = AddLight(root.transform);
                ApplyLightingData(data);
                view.SetAuthoring(data);

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success)
                    throw new InvalidOperationException("Could not save " + PrefabPath);
                report.Add($"{PrefabPath}: {saved.GetComponentsInChildren<Renderer>(true).Length} renderers, {CountTriangles(saved)} triangles");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static MeshRenderer Place(Transform parent, List<(Mesh mesh, Matrix4x4 matrix, string name)> meshes, string name, Matrix4x4 holder,
            Material material, string palette, ShadowCastingMode shadows, List<WorldChunk.PaletteBinding> bindings)
        {
            var (mesh, matrix) = Find(meshes, name);
            if (mesh == null)
                return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var world = holder * matrix;
            go.transform.localPosition = world.GetColumn(3);
            go.transform.localRotation = world.rotation;
            go.transform.localScale = world.lossyScale;
            var renderer = AddRenderer(go, mesh, material, shadows);
            bindings.Add(Binding(renderer, palette));
            return renderer;
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

        // ---------------------------------------------------------------- measures

        private static void MeasureBlanket(List<Vector3> blanket, PostcardSetData data, List<string> report)
        {
            if (blanket.Count == 0)
            {
                report.Add("WARNING: no blanket: the team stands on the meadow");
                data.blanketTop = 0.002f;
                return;
            }
            data.blanketTop = blanket.Max(p => p.y) + 0.002f;
            // The cloth's outline without the hem (its top: higher than 5 mm).
            var top = blanket.Where(p => p.y > 0.005f).ToList();
            data.blanketHalf = new Vector2(top.Max(p => Mathf.Abs(p.x)), top.Max(p => Mathf.Abs(p.z)));
            data.blanketPower = 7f;
            report.Add($"blanket {data.blanketHalf.x * 200f:F0} x {data.blanketHalf.y * 200f:F0} cm, top {data.blanketTop * 100f:F1} cm");
        }

        /// <summary>The blanket's highest point within a radius of a point (m; the meadow's 0 off the blanket).</summary>
        private static float BlanketHeight(List<Vector3> blanket, Vector2 at, float radius)
        {
            float h = 0f;
            foreach (var p in blanket)
                if (new Vector2(p.x - at.x, p.z - at.y).sqrMagnitude <= radius * radius)
                    h = Mathf.Max(h, p.y);
            return h;
        }

        /// <summary>The hill seen from above: a capsule along x holding its whole footprint (a superellipse: 10 % wider).</summary>
        private static Obstacle HillObstacle(List<Vector3> hill)
        {
            var above = hill.Where(p => p.y > 0.0f).ToList();
            float minX = above.Min(p => p.x), maxX = above.Max(p => p.x), minZ = above.Min(p => p.z), maxZ = above.Max(p => p.z);
            float radius = (maxZ - minZ) * 0.5f * 1.1f;
            float z = (minZ + maxZ) * 0.5f;
            var a = new Vector2(Mathf.Min(minX + radius, (minX + maxX) * 0.5f), z);
            var b = new Vector2(Mathf.Max(maxX - radius, (minX + maxX) * 0.5f), z);
            return new Obstacle(a, b, radius, 0f, -0.05f, above.Max(p => p.y), ObstacleKind.Solid);
        }

        /// <summary>A prop seen from above: a capsule along its own long axis holding every point.</summary>
        private static Obstacle FlatObstacle(List<Vector3> points, float yaw, float baseY)
        {
            var axis = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            var across = new Vector2(-axis.z, axis.x);
            var along = new Vector2(axis.x, axis.z);
            var center = new Vector2(points.Average(p => p.x), points.Average(p => p.z));
            float minA = float.MaxValue, maxA = float.MinValue, radius = 0f;
            foreach (var p in points)
            {
                var d = new Vector2(p.x, p.z) - center;
                float t = Vector2.Dot(d, along);
                minA = Mathf.Min(minA, t);
                maxA = Mathf.Max(maxA, t);
                radius = Mathf.Max(radius, Mathf.Abs(Vector2.Dot(d, across)));
            }
            float mid = (minA + maxA) * 0.5f;
            float half = Mathf.Max(0f, (maxA - minA) * 0.5f - radius);
            // Points near the ends may lie farther than the radius from the segment: widen to hold them all.
            var a = center + along * (mid - half);
            var b = center + along * (mid + half);
            foreach (var p in points)
                radius = Mathf.Max(radius, Mathf.Sqrt(CollisionMath.PointSegmentDistanceSq(new Vector2(p.x, p.z), a, b)));
            return new Obstacle(a, b, radius, baseY, -0.01f, points.Max(p => p.y) - baseY, ObstacleKind.Solid);
        }

        /// <summary>Every body of the catalog at its largest scale without the Giant trait (the seats hold any team).</summary>
        private static List<(string key, BodyShape body)> CatalogBodies()
        {
            var list = new List<(string, BodyShape)>();
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            if (catalog == null)
                return list;
            foreach (var entry in catalog.entries)
                if (entry != null && entry.prefab != null && entry.behavior != null)
                    list.Add((entry.visualKey, entry.behavior.body));
            return list;
        }

        /// <summary>
        /// The seats, their heights on the cloth, and the check: every team of the catalog's species (repeats included) at
        /// scale 1 and <see cref="PostcardSetLayout.SeatScale"/> sits on its seats with nobody in anybody nor in a prop.
        /// </summary>
        private static string MeasureSeats(PostcardSetData data, List<Vector3> blanket)
        {
            var bodies = CatalogBodies();
            float reach = bodies.Count > 0 ? bodies.Max(b => b.body.Reach) * PostcardSetLayout.SeatScale : 0.5f;
            data.seats = new Vector3[PostcardSetLayout.SeatCount];
            data.seatYaws = new float[PostcardSetLayout.SeatCount];
            for (int i = 0; i < PostcardSetLayout.SeatCount; i++)
            {
                // The root stands on the cloth's highest point under its paws (20 cm around), 2 mm over it.
                data.seats[i] = new Vector3(SeatPlan[i].x, BlanketHeight(blanket, new Vector2(SeatPlan[i].x, SeatPlan[i].y), 0.2f) + 0.002f, SeatPlan[i].y);
                data.seatYaws[i] = SeatPlan[i].z;
            }
            data.teamDepth = TeamDepth;
            if (bodies.Count == 0)
                return "WARNING: no visual catalog: the seats are not checked";
            var shapes = new BodyShape[PostcardSetLayout.MaxTeam];
            var scales = new float[PostcardSetLayout.MaxTeam];
            var positions = new Vector3[PostcardSetLayout.MaxTeam];
            var yaws = new float[PostcardSetLayout.MaxTeam];
            float smallest = float.MaxValue;
            int teams = 0, moved = 0, offBlanket = 0;
            string worst = "";
            foreach (float scale in new[] { 1f, PostcardSetLayout.SeatScale })
            {
                for (int size = 1; size <= PostcardSetLayout.MaxTeam; size++)
                {
                    int combos = (int)Math.Pow(bodies.Count, size);
                    for (int c = 0; c < combos; c++)
                    {
                        int code = c;
                        var names = new List<string>();
                        for (int i = 0; i < size; i++)
                        {
                            var b = bodies[code % bodies.Count];
                            code /= bodies.Count;
                            shapes[i] = b.body;
                            scales[i] = scale;
                            names.Add(b.key.Replace("PF_Animal_", ""));
                        }
                        float gap = PostcardTeamLayout.Solve(data, size, shapes, scales, positions, yaws);
                        teams++;
                        for (int i = 0; i < size; i++)
                        {
                            var seat = data.seats[PostcardSetLayout.SeatIndex(size, i)];
                            if (Mathf.Abs(positions[i].x - seat.x) > 0.005f)
                                moved++;
                            if (!PostcardTeamLayout.OnBlanket(data, positions[i]))
                                offBlanket++;
                        }
                        if (gap < smallest)
                        {
                            smallest = gap;
                            worst = string.Join("+", names) + (scale > 1f ? $" x{scale:F2}" : "");
                        }
                    }
                }
            }
            return $"seats: {teams} teams checked, smallest gap {smallest * 100f:F1} cm ({worst})"
                   + (smallest < 0f ? " (SOMETHING OVERLAPS)" : "")
                   + (moved > 0 ? $", {moved} animals pushed off their seats" : ", every animal on its seat")
                   + (offBlanket > 0 ? $", {offBlanket} off the blanket" : "")
                   + $", widest body reach {reach * 100f:F0} cm";
        }

        /// <summary>
        /// The grass and daisies in a loose row in front of the blanket (the kit's tufts and clover: explicit entries of
        /// Biome_Grassland_Kit, the set's bundle depends on them, never copies them; EP's daisies facing the camera), each
        /// footprint clear of the others and of the blanket's hem. T42c: never in front of a seat: each piece's picture
        /// (its bounds through the set camera) stays clear of every seat's picture (every team size, the widest and tallest
        /// body of the catalog at <see cref="PostcardSetLayout.SeatScale"/>) and of the basket's; a piece that cannot clear
        /// slides along the row, and a tall one that finds no room is left out (a smaller one may fit). The tall tufts end at
        /// the picture's edges, the low daisies along the bottom.
        /// </summary>
        private static string PlaceGrass(Transform decor, List<(Mesh mesh, Matrix4x4 matrix, string name)> meshes, Material material,
            List<WorldChunk.PaletteBinding> bindings, List<Obstacle> obstacles, PostcardSetData data, Bounds basket)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            var row = new GameObject("Foreground").transform;
            row.SetParent(decor, false);
            var keep = KeepClearRects(data, basket);
            var placed = new List<(Vector2 at, float r, Rect screen)>();
            var skipped = new HashSet<string>();
            float x = VisibleX(GrassZ, -1f) - 0.06f, end = VisibleX(GrassZ, 1f) + 0.06f;
            uint seed = 0x50435244u;
            int count = 0, daisies = 0, slid = 0, left = 0;
            float front = -(data.blanketHalf.y + 0.02f);
            for (int i = 0; count < GrassCount && x < end && i < 96; i++)
            {
                var (meshName, palette, own) = GrassKit[i % GrassKit.Length];
                (Mesh mesh, Matrix4x4 matrix) model = own
                    ? Find(meshes, meshName)
                    : (AssetDatabase.LoadAssetAtPath<Mesh>($"{NutDropPipeline.KitFolder}/{meshName}.asset"), Matrix4x4.identity);
                var mesh = model.mesh;
                bool usable = mesh != null && (own || (settings != null && settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mesh))) != null));
                if (!usable)
                {
                    skipped.Add(meshName);
                    if (skipped.Count >= GrassKit.Select(g => g.mesh).Distinct().Count())
                        break;
                    continue;
                }
                seed = seed * 1664525u + 1013904223u;
                float scale = own ? 1f + 0.12f * ((seed >> 8) & 255u) / 255f : 0.9f + 0.25f * ((seed >> 8) & 255u) / 255f;
                // The footprint and height in the set's frame (an own model carries the FBX's axes in its matrix).
                var shaped = Points(model, Matrix4x4.Scale(Vector3.one * scale));
                float r = 0f, low = float.MaxValue, high = float.MinValue;
                foreach (var p in shaped)
                {
                    r = Mathf.Max(r, new Vector2(p.x, p.z).magnitude);
                    low = Mathf.Min(low, p.y);
                    high = Mathf.Max(high, p.y);
                }
                float height = high - Mathf.Min(0f, low);
                float z = Mathf.Min(GrassZ + 0.3f * (((seed >> 16) & 255u) / 255f - 0.5f), front - r);
                var at = new Vector2(x + r, z);
                foreach (var (other, rr, _) in placed)
                    if ((other - at).magnitude < r + rr)
                        at.x = Mathf.Max(at.x, other.x + Mathf.Sqrt(Mathf.Max(0f, (r + rr) * (r + rr) - (other.y - at.y) * (other.y - at.y))) + 0.002f);
                // Never in front of a seat nor the basket: slide along the row until its picture is clear.
                var screen = PieceRect(at, r, height);
                bool moved = false;
                while (Hits(keep, screen) && at.x - r < end)
                {
                    at.x += 0.03f;
                    screen = PieceRect(at, r, height);
                    moved = true;
                }
                if (Hits(keep, screen) || at.x - r >= end)
                {
                    left++;
                    continue;
                }
                if (moved)
                    slid++;
                var go = new GameObject(meshName + "_" + count);
                go.transform.SetParent(row, false);
                if (own)
                {
                    // Rooted: its stems start under the meadow by design (the pivot is the foot on the ground).
                    float turn = 180f + DaisyTurn * ((((seed >> 4) & 255u) / 255f) * 2f - 1f);
                    var world = Holder(new Vector3(at.x, 0f, at.y), turn, scale) * model.matrix;
                    go.transform.SetLocalPositionAndRotation(world.GetColumn(3), world.rotation);
                    go.transform.localScale = world.lossyScale;
                    daisies++;
                }
                else
                {
                    go.transform.SetLocalPositionAndRotation(new Vector3(at.x, -low, at.y), Quaternion.Euler(0f, (seed >> 4) % 360u, 0f));
                    go.transform.localScale = Vector3.one * scale;
                }
                var renderer = AddRenderer(go, mesh, material, ShadowCastingMode.Off);
                bindings.Add(Binding(renderer, palette));
                obstacles.Add(new Obstacle(at, at, r, 0f, 0f, height, ObstacleKind.Solid));
                placed.Add((at, r, screen));
                x = at.x + r;
                count++;
            }
            // The check, piece by piece, against every seat's picture.
            int covering = placed.Count(p => Hits(keep, p.screen));
            return $"foreground: {count} pieces ({daisies} daisies facing the camera, {slid} slid aside, {left} left out)"
                   + (covering == 0 ? ", foreground clear of the team" : $" (WARNING: {covering} pieces in front of a seat)")
                   + (skipped.Count > 0 ? " (WARNING: skipped, missing or not explicit kit entries: " + string.Join(", ", skipped) + ")" : "");
        }

        /// <summary>The set camera's rotation (it looks from <see cref="CameraAt"/> at <see cref="CameraTarget"/>).</summary>
        private static Quaternion CameraRotation => Quaternion.LookRotation(CameraTarget - CameraAt, Vector3.up);

        /// <summary>A point of the set (origin frame) on the postcard: NDC, x and y in [-1, 1] across the picture.</summary>
        public static Vector2 ToScreen(Vector3 point)
        {
            var c = Quaternion.Inverse(CameraRotation) * (point - CameraAt);
            float t = Mathf.Tan(PostcardSetLayout.CameraFov * 0.5f * Mathf.Deg2Rad);
            float depth = Mathf.Max(0.05f, c.z);
            return new Vector2(c.x / (depth * t * (PostcardLayout.Width / (float)PostcardLayout.Height)), c.y / (depth * t));
        }

        /// <summary>The picture of a box: the smallest rect holding its eight corners.</summary>
        private static Rect BoxRect(Vector3 min, Vector3 max)
        {
            var lo = new Vector2(float.MaxValue, float.MaxValue);
            var hi = new Vector2(float.MinValue, float.MinValue);
            for (int k = 0; k < 8; k++)
            {
                var corner = new Vector3((k & 1) == 0 ? min.x : max.x, (k & 2) == 0 ? min.y : max.y, (k & 4) == 0 ? min.z : max.z);
                var p = ToScreen(corner);
                lo = Vector2.Min(lo, p);
                hi = Vector2.Max(hi, p);
            }
            return Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
        }

        private static Rect PieceRect(Vector2 at, float r, float height) =>
            BoxRect(new Vector3(at.x - r, 0f, at.y - r), new Vector3(at.x + r, height, at.y + r));

        /// <summary>
        /// What the foreground keeps clear of on the picture: each seat (all team sizes) with the widest, longest and tallest
        /// body of the catalog at <see cref="PostcardSetLayout.SeatScale"/> (and the outer seats' turn), and the basket.
        /// </summary>
        private static List<Rect> KeepClearRects(PostcardSetData data, Bounds basket)
        {
            var bodies = CatalogBodies();
            float s = PostcardSetLayout.SeatScale;
            float half = (bodies.Count > 0 ? bodies.Max(b => b.body.halfWidth) : 0.17f) * s;
            float front = (bodies.Count > 0 ? bodies.Max(b => b.body.front) : 0.26f) * s;
            float back = (bodies.Count > 0 ? bodies.Max(b => -b.body.back) : 0.41f) * s;
            float tall = (bodies.Count > 0 ? bodies.Max(b => b.body.height) : 0.66f) * s;
            var rects = new List<Rect>();
            foreach (var seat in SeatPlan)
            {
                // A turned body reaches sideways by its length times the turn's sine.
                float turn = Mathf.Abs(Mathf.Sin((seat.z - 180f) * Mathf.Deg2Rad)) * Mathf.Max(front, back);
                rects.Add(BoxRect(new Vector3(seat.x - half - turn, 0f, seat.y - front), new Vector3(seat.x + half + turn, data.blanketTop + tall, seat.y + back)));
            }
            rects.Add(BoxRect(basket.min, basket.max));
            return rects;
        }

        private static bool Hits(List<Rect> keep, Rect piece)
        {
            foreach (var k in keep)
                if (piece.xMin < k.xMax + ScreenMargin && piece.xMax > k.xMin - ScreenMargin && piece.yMin < k.yMax + ScreenMargin && piece.yMax > k.yMin - ScreenMargin)
                    return true;
            return false;
        }

        /// <summary>The x (origin frame) of the postcard's left (-1) or right (+1) edge at a depth on the meadow.</summary>
        private static float VisibleX(float z, float side)
        {
            var c = Quaternion.Inverse(CameraRotation) * (new Vector3(CameraAt.x, 0f, z) - CameraAt);
            float t = Mathf.Tan(PostcardSetLayout.CameraFov * 0.5f * Mathf.Deg2Rad) * (PostcardLayout.Width / (float)PostcardLayout.Height);
            return CameraAt.x + side * c.z * t;
        }

        /// <summary>
        /// GR_SetBackdrop (T38's meadow and round hills, shared through Activity_Shared), twice its size, its floor under the
        /// meadow; its own little daisies (any point over its flat floor) must stand clear of the hill, the blanket, the
        /// basket, the seats and the grass: the first yaw that keeps them all clear wins, and they join the props the team
        /// keeps clear of.
        /// </summary>
        private static string PlaceBackdrop(Transform decor, Material material, List<WorldChunk.PaletteBinding> bindings, List<Obstacle> obstacles,
            List<Vector3> hill, PostcardSetData data)
        {
            var backdrop = GroomingPipeline.ModelMeshes(GroomingPipeline.SetModel).FirstOrDefault(m => m.name == NutDropPipeline.BackdropName || m.mesh.name == NutDropPipeline.BackdropName);
            if (backdrop.mesh == null)
                return "WARNING: " + NutDropPipeline.BackdropName + " missing (" + GroomingPipeline.SetModel + ".fbx): no hills behind";
            // Its daisies, in its own space: over the flat floor, inside the floor's disc.
            var daisies = new List<Vector3>();
            foreach (var v in backdrop.mesh.vertices)
            {
                var p = backdrop.matrix.MultiplyPoint3x4(v);
                // Inside the near hills' foot (2.25 m in its own space): only its flowers rise over the floor there.
                if (p.y > BackdropFloor + 0.004f && new Vector2(p.x, p.z).magnitude < 2.2f)
                    daisies.Add(p);
            }
            var position = new Vector3(0f, -BackdropFloor * BackdropScale - BackdropSink, BackdropZ);
            var hillObstacle = hill.Count > 0 ? HillObstacle(hill) : default;
            float chosen = BackdropYaws[0];
            int bestConflicts = int.MaxValue;
            foreach (float yaw in BackdropYaws)
            {
                var holder = Holder(position, yaw, BackdropScale);
                int conflicts = 0;
                foreach (var d in daisies)
                {
                    var p = holder.MultiplyPoint3x4(d);
                    var flat = new Vector2(p.x, p.z);
                    if (hill.Count > 0 && CollisionMath.Gap(flat, flat, 0.09f, in hillObstacle) < 0f)
                        conflicts++;
                    else if (InsideBlanket(data, flat, 0.15f))
                        conflicts++;
                    else if (obstacles.Any(o => CollisionMath.Gap(flat, flat, 0.09f, in o) < 0f))
                        conflicts++;
                    else if (NearSeats(flat, 0.75f))
                        conflicts++;
                }
                if (conflicts < bestConflicts)
                {
                    bestConflicts = conflicts;
                    chosen = yaw;
                }
                if (conflicts == 0)
                    break;
            }
            var holderObject = new GameObject("Backdrop").transform;
            holderObject.SetParent(decor, false);
            holderObject.localPosition = position;
            holderObject.localRotation = Quaternion.Euler(0f, chosen, 0f);
            holderObject.localScale = Vector3.one * BackdropScale;
            var go = new GameObject(NutDropPipeline.BackdropName);
            go.transform.SetParent(holderObject, false);
            go.transform.localPosition = backdrop.matrix.GetColumn(3);
            go.transform.localRotation = backdrop.matrix.rotation;
            go.transform.localScale = backdrop.matrix.lossyScale;
            var renderer = AddRenderer(go, backdrop.mesh, material, ShadowCastingMode.Off);
            bindings.Add(Binding(renderer, GroomingPipeline.PaletteBackdrop));
            // Its daisies near the team join the props (one disc per little flower).
            var final = Holder(position, chosen, BackdropScale);
            var near = new List<(Vector2 c, float r, float top)>();
            foreach (var d in daisies)
            {
                var p = final.MultiplyPoint3x4(d);
                var flat = new Vector2(p.x, p.z);
                if (flat.magnitude > 2.5f)
                    continue;
                int k = near.FindIndex(n => (n.c - flat).magnitude < 0.12f);
                if (k < 0)
                    near.Add((flat, 0.09f, p.y));
                else
                    near[k] = (near[k].c, Mathf.Max(near[k].r, (near[k].c - flat).magnitude + 0.03f), Mathf.Max(near[k].top, p.y));
            }
            foreach (var n in near)
                obstacles.Add(new Obstacle(n.c, n.c, n.r, 0f, 0f, n.top, ObstacleKind.Solid));
            return $"backdrop yaw {chosen:F0}, {near.Count} of its daisies near the team" + (bestConflicts > 0 ? $" (WARNING: {bestConflicts} daisy points in a prop at every yaw)" : "");
        }

        private static bool InsideBlanket(PostcardSetData data, Vector2 p, float margin)
        {
            float u = Mathf.Abs(p.x) / (data.blanketHalf.x + margin), v = Mathf.Abs(p.y) / (data.blanketHalf.y + margin);
            return Mathf.Pow(u, data.blanketPower) + Mathf.Pow(v, data.blanketPower) <= 1f;
        }

        private static bool NearSeats(Vector2 p, float radius)
        {
            foreach (var s in SeatPlan)
                if ((new Vector2(s.x, s.y) - p).magnitude < radius)
                    return true;
            return false;
        }

        private static string PlaceButterflies(Transform root, List<(Mesh mesh, Matrix4x4 matrix, string name)> meshes, Material material,
            List<WorldChunk.PaletteBinding> bindings, PostcardSetData data, List<Vector3> hill, float basketTop)
        {
            var parent = new GameObject("Butterflies").transform;
            parent.SetParent(root, false);
            var placed = new List<(Vector3 at, float r)>();
            var transforms = new List<Transform>();
            var problems = new List<string>();
            var radii = new List<float>();
            int index = 0;
            foreach (var (model, at, turn, scale) in Butterflies)
            {
                bool near = index++ < NearButterflies;
                var (mesh, matrix) = Find(meshes, model);
                if (mesh == null)
                    continue;
                float r = 0f;
                foreach (var v in mesh.vertices)
                    r = Mathf.Max(r, matrix.MultiplyVector(v).magnitude);
                r *= scale;
                var name = model + "_" + transforms.Count;
                // Clear of the hill under it, the basket, the tallest head on the seats, and each other.
                float ground = 0f;
                foreach (var p in hill)
                    if (new Vector2(p.x - at.x, p.z - at.z).magnitude < r + 0.08f)
                        ground = Mathf.Max(ground, p.y);
                if (at.y - r < ground + 0.03f)
                    problems.Add(name + " in the hill");
                if (new Vector2(at.x - BasketAt.x, at.z - BasketAt.z).magnitude < 0.3f + r && at.y - r < basketTop + 0.03f)
                    problems.Add(name + " in the basket");
                // A near one may meet a big animal of some teams: the renderer hides it then (checked on the posed team).
                if (!near && NearSeats(new Vector2(at.x, at.z), 0.55f + r) && at.y - r < TallestAnimal + 0.02f)
                    problems.Add(name + " over a head");
                foreach (var (other, rr) in placed)
                    if ((other - at).magnitude < r + rr + 0.02f)
                        problems.Add(name + " in another butterfly");
                var holder = new GameObject(name).transform;
                holder.SetParent(parent, false);
                holder.SetLocalPositionAndRotation(at, FacingCamera(at, turn));
                holder.localScale = Vector3.one * scale;
                var go = new GameObject(model);
                go.transform.SetParent(holder, false);
                go.transform.localPosition = matrix.GetColumn(3);
                go.transform.localRotation = matrix.rotation;
                go.transform.localScale = matrix.lossyScale;
                var renderer = AddRenderer(go, mesh, material, ShadowCastingMode.Off);
                bindings.Add(Binding(renderer, PaletteButterfly));
                transforms.Add(holder);
                radii.Add(r);
                placed.Add((at, r));
            }
            data.butterflies = transforms.ToArray();
            data.butterflyRadii = radii.ToArray();
            data.nearButterflies = Mathf.Min(NearButterflies, transforms.Count);
            return $"butterflies: {transforms.Count}" + (problems.Count > 0 ? " (WARNING: " + string.Join(", ", problems) + ")" : ", each clear of everything");
        }

        /// <summary>
        /// A butterfly seen from above and in front, as a picture shows one: its wings' top turned toward the camera (and a
        /// little up), its body along the picture's up turned by <paramref name="turn"/> degrees. The model's head is
        /// Blender -Y and its wings' top Blender +Z: after the FBX's axes and the half turn, -Z and +Y.
        /// </summary>
        private static Quaternion FacingCamera(Vector3 at, float turn)
        {
            var toCamera = (CameraAt - at).normalized;
            var normal = (toCamera * 0.8f + Vector3.up * 0.45f).normalized;
            var head = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
            head = Quaternion.AngleAxis(turn, normal) * head;
            return Quaternion.LookRotation(-head, normal) * Quaternion.Euler(0f, 180f, 0f);
        }

        // ---------------------------------------------------------------- camera, light, sky

        private static Camera AddCamera(Transform parent, PostcardSetData data)
        {
            var go = new GameObject("Camera");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = CameraAt;
            go.transform.localRotation = Quaternion.LookRotation(CameraTarget - CameraAt, Vector3.up);
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = PostcardSetLayout.CameraFov;
            camera.nearClipPlane = PostcardSetLayout.CameraNear;
            camera.farClipPlane = PostcardSetLayout.CameraFar;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            var urp = go.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            // One frame per postcard: the soft edges of SMAA are worth it.
            urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            urp.antialiasingQuality = AntialiasingQuality.High;
            urp.renderShadows = true;
            urp.requiresDepthOption = CameraOverrideOption.Off;
            urp.requiresColorOption = CameraOverrideOption.Off;
            // The half width kept in view along the team's line (a wider team moves the camera back at run time).
            var forward = go.transform.localRotation * Vector3.forward;
            float distance = Vector3.Dot(new Vector3(CameraAt.x, 0.3f, TeamDepth) - CameraAt, forward);
            data.frameHalfWidth = distance * Mathf.Tan(PostcardSetLayout.CameraFov * 0.5f * Mathf.Deg2Rad) * (PostcardLayout.Width / (float)PostcardLayout.Height);
            return camera;
        }

        /// <summary>The Prairie day a touch more golden (an afternoon picnic), like the Nut Drop set's sun.</summary>
        private static Light AddLight(Transform parent)
        {
            var profile = AssetDatabase.LoadAssetAtPath<LightingProfile>("Assets/_Game/Art/Lighting/Grassland/LP_Grassland_Day.asset");
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(44f, -32f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            var sun = profile != null ? profile.sunColor : new Color(1f, 0.945f, 0.863f);
            light.color = Color.Lerp(sun, new Color(1f, 0.86f, 0.68f), 0.3f);
            light.intensity = (profile != null ? profile.sunIntensity : 1f) * 1.05f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = profile != null ? Mathf.Min(0.5f, profile.shadowStrength) : 0.5f;
            light.enabled = false;
            return light;
        }

        private static void ApplyLightingData(PostcardSetData data)
        {
            var profile = AssetDatabase.LoadAssetAtPath<LightingProfile>("Assets/_Game/Art/Lighting/Grassland/LP_Grassland_Day.asset");
            var warm = new Color(1f, 0.9f, 0.76f);
            if (profile != null)
            {
                data.ambientTop = Color.Lerp(profile.ambientTop, warm, 0.12f);
                data.ambientEquator = Color.Lerp(profile.ambientEquator, warm, 0.22f);
                data.ambientBottom = Color.Lerp(profile.ambientBottom, warm, 0.1f);
                data.skyTop = Color.Lerp(profile.skyTop, new Color(0.6f, 0.78f, 0.96f), 0.5f);
                data.cloudLight = profile.cloudLight;
            }
            data.skyHorizon = new Color(1f, 0.92f, 0.8f);
            data.skyBottom = new Color(0.95f, 0.88f, 0.76f);
            data.sunGlowTint = new Color(1f, 0.86f, 0.64f);
            data.sunGlow = 0.38f;
            data.cloudShade = new Color(0.9f, 0.85f, 0.88f);
            data.cloudCoverage = 0.3f;
        }

        private static int CountTriangles(GameObject prefab)
        {
            int count = 0;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                for (int s = 0; s < filter.sharedMesh.subMeshCount; s++)
                    count += (int)filter.sharedMesh.GetIndexCount(s) / 3;
            }
            return count;
        }

        // ---------------------------------------------------------------- Addressables

        /// <summary>
        /// The set in Activity_Expeditions (loaded at the first postcard, released with the screen); T38's set model stays an
        /// explicit entry of Activity_Shared and the kit's grass of Biome_Grassland_Kit (nothing duplicated between bundles).
        /// </summary>
        public static string EnsureAddressables(GameObject prefab)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = EnsureGroup(settings, PostcardLayout.Group);
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)), group, false, false);
            entry.address = PostcardSetLayout.SetAddress(Destination);
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
            return $"Addressables {PostcardLayout.Group}: {entry.address}";
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

        // ---------------------------------------------------------------- the destination card art

        /// <summary>
        /// EXP_flower_meadows.png: the set without animals, through its camera made landscape (the map's card), with the game
        /// shaders, its palettes, its light and sky, in the refuge scene (its sky). Refused while another scene has unsaved
        /// changes; the world scene is reopened afterwards (nothing saved).
        /// </summary>
        public static string RenderCardArt(GameObject prefab)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != WorldCapture.ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "WARNING: card art skipped (save or discard the changes in " + active.path + ")";
            EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            if (database == null)
                return "WARNING: card art skipped (no content database)";
            var registry = database.CreateBuilder().Build();
            GameObject instance = null;
            RenderTexture target = null;
            Texture2D picture = null;
            var lighting = new PostcardLighting();
            PostcardSetData data = null;
            try
            {
                using (var palettes = new PaletteTable(registry))
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.hideFlags = HideFlags.DontSave;
                    instance.transform.SetPositionAndRotation(PostcardSetLayout.SetPosition, Quaternion.identity);
                    var view = instance.GetComponent<PostcardSetView>();
                    data = view.Data;
                    int missing = view.Bind(palettes);
                    foreach (var butterfly in data.butterflies)
                        if (butterfly != null)
                            butterfly.gameObject.SetActive(true);
                    var camera = data.camera;
                    target = RenderTexture.GetTemporary(PostcardSetLayout.CardArtWidth, PostcardSetLayout.CardArtHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    lighting.Apply(data);
                    camera.targetTexture = target;
                    camera.aspect = PostcardSetLayout.CardArtWidth / (float)PostcardSetLayout.CardArtHeight;
                    camera.Render();
                    camera.targetTexture = null;
                    lighting.Restore(data);
                    var previous = RenderTexture.active;
                    RenderTexture.active = target;
                    picture = new Texture2D(PostcardSetLayout.CardArtWidth, PostcardSetLayout.CardArtHeight, TextureFormat.RGB24, false, false);
                    picture.ReadPixels(new Rect(0, 0, PostcardSetLayout.CardArtWidth, PostcardSetLayout.CardArtHeight), 0, 0, false);
                    picture.Apply(false, false);
                    RenderTexture.active = previous;
                    Directory.CreateDirectory(PostcardLayout.CardArtFolder);
                    File.WriteAllBytes(CardArtPath, picture.EncodeToPNG());
                    AssetDatabase.ImportAsset(CardArtPath, ImportAssetOptions.ForceUpdate);
                    ConfigureCardArt(CardArtPath);
                    return $"{CardArtPath} {PostcardSetLayout.CardArtWidth} x {PostcardSetLayout.CardArtHeight}" + (missing > 0 ? $" (WARNING: {missing} palettes unknown)" : "");
                }
            }
            finally
            {
                lighting.Restore(data);
                if (target != null)
                    RenderTexture.ReleaseTemporary(target);
                if (picture != null)
                    Object.DestroyImmediate(picture);
                if (instance != null)
                    Object.DestroyImmediate(instance);
                EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
            }
        }

        /// <summary>A UI sprite (TextureImportRules leaves EXP_ names alone): single, no mipmaps, clamped, never in an atlas.</summary>
        public static void ConfigureCardArt(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
