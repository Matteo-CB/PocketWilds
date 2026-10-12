using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Lighting;
using PocketWilds.Editor.Production;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Environment
{
    /// <summary>
    /// T50 Forêt Magnifique, the editor side: turns the Forest art (FO_Vegetation, FO_Props, FO_Scatter, FO_Grounds and FO_Water,
    /// made by pw_forest.py and pw_forest_ground.py) into standalone kit meshes (Baked/Kit, the Grassland's KitMesh), kit prefabs
    /// on the shared materials only (MAT_Environment_Common, MAT_Foliage_Grassland for the ferns' sway, MAT_Ground_Grassland,
    /// MAT_Water_Grassland: palettes by vertex regions, no new texture), DEF_ScatterKit_Forest, DEF_AmbientLife_Forest (canopy
    /// birds, the Clearing's butterflies; the fireflies of every Forest evening are the season looks' species at the
    /// VFX_Butterfly anchors), DEF_Weather_Forest (the Grassland's showers, sun shafts from Équilibrée in the Entry and the
    /// Clearing), DEF_ZoneKit_Forest (hedges of Forest bushes, the wooden sign) and the seven chunk prefabs of the strip
    /// (ForestPipeline.Chunks.cs), then runs the Biome Wizard to fill DEF_ChunkSet_Forest. Same rules as the Grassland strip:
    /// props settled until nothing enters anything (the ForestEdge's props included at x 105), scatter outside the props,
    /// collision bands, ground heights, three animal spawns per chunk. Idempotent: run again after a re-export.
    /// </summary>
    public static partial class ForestPipeline
    {
        public const string ModelsFolder = "Assets/_Game/Art/Environment/Forest/Models";
        public const string PrefabsFolder = "Assets/_Game/Prefabs/Environment/Forest";
        public const string BakedFolder = "Assets/_Game/Art/Environment/Forest/Baked";
        public const string KitMeshFolder = BakedFolder + "/Kit";
        public const string MergedFolder = BakedFolder + "/Merged";
        public const string ScatterKitPath = GrasslandPipeline.ScatterFolder + "/DEF_ScatterKit_Forest.asset";

        /// <summary>The Biome Wizard's places for the Forest (DEF_Palettes_Forest, kits, chunk prefabs).</summary>
        public static readonly BiomeWizard.BiomePaths Paths = new BiomeWizard.BiomePaths("Forest", BiomeWizard.DefaultRoot);
        public static string PalettesPath => Paths.Palettes;
        public static string LifeKitPath => Paths.AmbientLife;
        public static string WeatherKitPath => Paths.Weather;
        public static string ZoneKitPath => Paths.ZoneKit;
        public static string ChunkFolder => Paths.ChunkFolder;

        public const string PaletteTree = "palette.forest.tree";
        public const string PaletteBirch = "palette.forest.birch";
        public const string PaletteBush = "palette.forest.bush";
        public const string PaletteGround = "palette.forest.ground";
        public const string PaletteRock = "palette.forest.rock";
        public const string PaletteLog = "palette.forest.log";
        public const string PaletteMushroom = "palette.forest.mushroom";
        public const string PaletteFern = "palette.forest.fern";
        public const string PaletteWater = "palette.forest.water";
        public const string PaletteLifeBird = "palette.forest.life_bird";
        public const string PaletteButterflyAmber = "palette.forest.life_butterfly_amber";
        public const string PaletteButterflyCream = "palette.forest.life_butterfly_cream";

        /// <summary>
        /// Firefly spots of every Forest chunk: the season looks' fireflies live at the anchors starting with VFX_Butterfly
        /// (SeasonLookPipeline), and the Forest lights them every evening (VerticalSliceEntry.UpdateRegion).
        /// </summary>
        public const string FireflyAnchorPrefix = "VFX_Butterfly_Dusk";

        /// <summary>The Clearing's butterflies only (DEF_AmbientLife_Forest): no other Forest chunk has these anchors.</summary>
        public const string GladeButterflyPrefix = "VFX_ButterflyGlade";

        public const string BirdAnchorPrefix = "VFX_Birds";

        /// <summary>Landmarks stay detailed longer than the kit trees (doc 07: long LOD distance for monuments).</summary>
        private static readonly float[] LandmarkLods = { 0.22f, 0.09f, 0.01f };

        private static readonly string[] Trees = { "FO_Tree_A", "FO_Tree_B", "FO_Tree_C", "FO_Tree_D" };
        private static readonly string[] Landmarks = { "FO_Landmark_AncientTree", "FO_Landmark_GiantStump" };
        private static readonly string[] Bushes = { "FO_Bush_A", "FO_Bush_B", "FO_Bush_C" };
        private static readonly string[] Rocks = { "FO_MossRock_S", "FO_MossRock_M", "FO_MossRock_L", "FO_MossRock_Hero" };
        private static readonly string[] Props = { "FO_Stump", "FO_HollowLog", "FO_RootArch", "FO_TinyBridge", "FO_SteppingStones" };

        /// <summary>Shape-only pieces (a mesh filter, no renderer): their look is merged per chunk (ferns sway on the foliage material).</summary>
        private static readonly string[] Shapes = { "FO_Fern_A", "FO_Fern_B", "FO_Mushroom_A", "FO_Mushroom_B", "FO_Mushroom_C" };

        public static bool IsFern(string prop) => prop != null && prop.StartsWith("FO_Fern", StringComparison.Ordinal);
        public static bool IsMushroom(string prop) => prop != null && prop.StartsWith("FO_Mushroom", StringComparison.Ordinal);

        [MenuItem("Pocket Wilds/Environment/Build Forest Magnifique (T50)", priority = 178)]
        public static void BuildForestMenu() => Debug.Log("[ForestPipeline] " + BuildForest());

        /// <summary>
        /// Everything of the Forest in order: kit meshes and prefabs, scatter, life, weather and zone kits, the seven chunks, the
        /// scatter pruned along the whole strip, the Biome Wizard (palettes, moods, chunk set, Addressables entries, world profile)
        /// and the morning mist. Run after the Grassland strip (its ForestEdge opens east and the Entry settles against it).
        /// </summary>
        public static string BuildForest()
        {
            foreach (var folder in new[] { PrefabsFolder, KitMeshFolder, MergedFolder, ChunkFolder, GrasslandPipeline.ScatterFolder })
                BiomeWizard.EnsureFolder(folder);
            var meshes = ModelMeshes();
            var env = AssetDatabase.LoadAssetAtPath<Material>(GrasslandPipeline.EnvironmentMaterialPath)
                      ?? throw new InvalidOperationException("Build the Grassland kit first: " + GrasslandPipeline.EnvironmentMaterialPath);
            var ground = AssetDatabase.LoadAssetAtPath<Material>(GrasslandPipeline.GroundMaterialPath)
                         ?? throw new InvalidOperationException("Build the Grassland kit first: " + GrasslandPipeline.GroundMaterialPath);
            var water = GrasslandPipeline.EnsureWaterMaterial();
            var foliage = GrasslandPipeline.EnsureFoliageMaterial();
            CollisionProxies.ClearCache();
            _radii.Clear();
            var prefabs = BuildPrefabs(meshes, env, ground);
            var kit = EnsureScatterKit(meshes, foliage);
            var life = EnsureLifeKit();
            var weather = EnsureWeatherKit();
            var zones = EnsureZoneKit(meshes, env);
            AssetDatabase.SaveAssets();

            // The Grassland's prefabs too: the Entry settles against the ForestEdge's props at the seam.
            var all = new Dictionary<string, GameObject>(prefabs);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { GrasslandPipeline.PrefabsFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.name.StartsWith("PF_", StringComparison.Ordinal))
                    all[prefab.name.Substring(3)] = prefab;
            }
            SeedNeighbours(all);

            var reports = new List<string> { $"{prefabs.Count} kit prefabs, {kit.kinds.Length} scatter kinds, life {life.species.Length} species, weather rays from {weather.sunRaysFrom}, zones {zones.bushes.Length} hedge meshes" };
            foreach (var chunk in Chunks)
                reports.Add(BuildChunk(chunk, all, kit, water, meshes));
            AssetDatabase.SaveAssets();
            reports.Add(GrasslandPipeline.PruneScatterInsideProps());
            reports.Add(BiomeWizard.CreateOrUpdate(BiomeWizard.ForestSpec()));
            reports.Add(TunePalettes());
            reports.Add(TuneMoods());
            AssetDatabase.SaveAssets();
            return string.Join("; ", reports);
        }

        // ---------------- models and kit meshes ----------------

        private static Dictionary<string, Mesh> ModelMeshes()
        {
            var meshes = new Dictionary<string, Mesh>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                {
                    if (meshes.TryGetValue(mesh.name, out var other))
                        throw new InvalidOperationException($"Mesh {mesh.name} is exported twice ({path} and {AssetDatabase.GetAssetPath(other)}).");
                    meshes[mesh.name] = mesh;
                }
            }
            var missing = new[] { "FO_Tree_A_LOD0", "FO_Landmark_AncientTree_LOD0", "FO_Bush_A", "FO_Fern_A", "FO_Mushroom_A", "FO_MossRock_Hero",
                    "FO_TinyBridge", "FO_Leaf_A", "FO_MossTuft", "FO_Brook_Water" }
                .Concat(Chunks.Select(c => c.Ground)).Where(n => !meshes.ContainsKey(n)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException("Forest models missing (export them from the Forest blends): " + string.Join(", ", missing));
            return meshes;
        }

        /// <summary>A standalone copy in the Forest's kit folder: the FBX never enters a bundle (its model prefab drags URP Lit).</summary>
        private static Mesh Kit(Mesh source) => GrasslandPipeline.KitMesh(source, KitMeshFolder);

        // ---------------- prefabs ----------------

        private static Dictionary<string, GameObject> BuildPrefabs(Dictionary<string, Mesh> meshes, Material env, Material ground)
        {
            foreach (var tree in Trees)
                SaveLods(meshes, env, tree, GrasslandPipeline.TreeLods);
            foreach (var landmark in Landmarks)
                SaveLods(meshes, env, landmark, LandmarkLods);
            foreach (var bush in Bushes)
                SaveSingle(Kit(meshes[bush]), env, ShadowCastingMode.On, 0.008f);
            foreach (var rock in Rocks)
                SaveSingle(Kit(meshes[rock]), env, ShadowCastingMode.On, rock == "FO_MossRock_S" ? GrasslandPipeline.SmallPropCull : rock == "FO_MossRock_Hero" ? 0.004f : 0.006f);
            foreach (var prop in Props)
                SaveSingle(Kit(meshes[prop]), env, prop == "FO_SteppingStones" ? ShadowCastingMode.Off : ShadowCastingMode.On, 0.008f);
            foreach (var shape in Shapes)
                SaveShape(Kit(meshes[shape]));
            foreach (var chunk in Chunks)
                SaveSingle(Kit(meshes[chunk.Ground]), ground, ShadowCastingMode.Off, 0f);

            var prefabs = new Dictionary<string, GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.name.StartsWith("PF_FO_", StringComparison.Ordinal))
                    prefabs[prefab.name.Substring(3)] = prefab; // PF_FO_Bush_A -> FO_Bush_A
            }
            return prefabs;
        }

        private static GameObject SaveSingle(Mesh mesh, Material material, ShadowCastingMode shadows, float cull)
        {
            var go = new GameObject("PF_" + mesh.name);
            try
            {
                var renderer = GrasslandPipeline.AddRenderer(go, mesh, material, shadows);
                if (cull > 0f)
                {
                    var lod = go.AddComponent<LODGroup>();
                    lod.SetLODs(new[] { new LOD(cull, new Renderer[] { renderer }) });
                    lod.RecalculateBounds();
                }
                return PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsFolder}/PF_{mesh.name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Trees and landmarks: three LOD renderers (shadows on LOD0 and LOD1), no cross-fade.</summary>
        private static GameObject SaveLods(Dictionary<string, Mesh> meshes, Material material, string name, float[] heights)
        {
            var go = new GameObject("PF_" + name);
            try
            {
                var lods = new LOD[3];
                for (int lod = 0; lod < 3; lod++)
                {
                    var child = new GameObject($"{name}_LOD{lod}");
                    child.transform.SetParent(go.transform, false);
                    var renderer = GrasslandPipeline.AddRenderer(child, Kit(meshes[$"{name}_LOD{lod}"]), material,
                        lod < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off);
                    lods[lod] = new LOD(heights[lod], new Renderer[] { renderer });
                }
                var group = go.AddComponent<LODGroup>();
                group.SetLODs(lods);
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();
                return PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsFolder}/PF_{name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>PF_&lt;mesh&gt;: a mesh filter only, the piece's shape in the layout (measured volume, obstacle).</summary>
        private static GameObject SaveShape(Mesh mesh)
        {
            var go = new GameObject("PF_" + mesh.name);
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                return PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsFolder}/PF_{mesh.name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------------- measured footprints ----------------

        private static readonly Dictionary<string, float> _radii = new Dictionary<string, float>();

        /// <summary>
        /// Footprint radius of a Forest piece at scale 1 (m): the reach of its measured ground outline from its pivot
        /// (CollisionProxies). Props stand on the lowest ground under it, scatter details ring it, animals stop at it.
        /// </summary>
        public static float PropRadius(string prop)
        {
            if (_radii.TryGetValue(prop, out float radius))
                return radius;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/PF_{prop}.prefab");
            radius = 0.3f;
            var ground = prefab != null ? CollisionProxies.Measure(prefab).Ground : null;
            if (ground != null && ground.Hull.Length > 0)
                radius = ground.Hull.Max(p => p.magnitude);
            _radii[prop] = radius;
            return radius;
        }

        /// <summary>The palette row of a Forest piece (BiomeWizard.ForestSpec rows; the regions are painted for them).</summary>
        public static string PaletteFor(string prop) => prop switch
        {
            "FO_Tree_D" => PaletteBirch,
            _ when prop.StartsWith("FO_Tree", StringComparison.Ordinal) || prop.StartsWith("FO_Landmark_AncientTree", StringComparison.Ordinal) => PaletteTree,
            _ when prop.StartsWith("FO_Bush", StringComparison.Ordinal) => PaletteBush,
            _ when IsFern(prop) => PaletteFern,
            _ when IsMushroom(prop) => PaletteMushroom,
            _ when prop.StartsWith("FO_MossRock", StringComparison.Ordinal) || prop == "FO_SteppingStones" => PaletteRock,
            _ when prop.StartsWith("FO_Ground", StringComparison.Ordinal) => PaletteGround,
            // FO_Landmark_GiantStump, FO_Stump, FO_HollowLog, FO_RootArch, FO_TinyBridge.
            _ => PaletteLog,
        };

        private static uint Id(string source) => ContentId.FromSource(source).Value;

        // ---------------- scatter kit ----------------

        /// <summary>
        /// The Forest's ground details on MAT_Foliage_Grassland (instanced): the Prairie's own grass and low flowers where the
        /// ground is meadow (same meshes and rows: the meadow meets the ForestEdge's with the same colors), moss cushions, clover,
        /// fallen leaves, twigs, acorns, pine cones and pebbles of the Forest rows.
        /// </summary>
        private static ScatterKit EnsureScatterKit(Dictionary<string, Mesh> meshes, Material foliage)
        {
            var grass = AssetDatabase.LoadAssetAtPath<ScatterKit>(GrasslandPipeline.ScatterKitPath)
                        ?? throw new InvalidOperationException("Build the Grassland scatter kit first: " + GrasslandPipeline.ScatterKitPath);
            Mesh Shared(string id)
            {
                int at = grass.IndexOf(id);
                if (at < 0 || grass.kinds[at].mesh == null)
                    throw new InvalidOperationException($"{GrasslandPipeline.ScatterKitPath} has no {id}.");
                return grass.kinds[at].mesh;
            }
            var kinds = new (string id, Mesh mesh, string palette, float wind, float minDensity)[]
            {
                ("grass.tuft_s", Shared("grass.tuft_s"), GrasslandPipeline.PaletteGrass, 1.0f, 0f),
                ("grass.tuft_m", Shared("grass.tuft_m"), GrasslandPipeline.PaletteGrass, 1.0f, 0f),
                ("grass.tuft_l", Shared("grass.tuft_l"), GrasslandPipeline.PaletteGrass, 1.0f, 0f),
                ("grass.wisp_m", Shared("grass.wisp_m"), GrasslandPipeline.PaletteGrass, 1.2f, 0f),
                ("flower.daisy_white", Shared("flower.daisy_white"), GrasslandPipeline.FlowerPalettes[0], 0.7f, 0.55f),
                ("flower.bell_blue", Shared("flower.bell_blue"), GrasslandPipeline.FlowerPalettes[3], 0.7f, 0.55f),
                ("flower.buttercup", Shared("flower.buttercup"), GrasslandPipeline.FlowerPalettes[1], 0.6f, 0.55f),
                ("moss.tuft", Kit(meshes["FO_MossTuft"]), PaletteFern, 0.25f, 0f),
                ("detail.clover", Kit(meshes["FO_Clover"]), PaletteFern, 0.3f, 0f),
                ("detail.leaf_a", Kit(meshes["FO_Leaf_A"]), PaletteTree, 0.1f, 0f),
                ("detail.leaf_b", Kit(meshes["FO_Leaf_B"]), PaletteTree, 0.1f, 0f),
                ("detail.twig", Kit(meshes["FO_Twig"]), PaletteLog, 0f, 0.3f),
                ("detail.acorn", Kit(meshes["FO_Acorn"]), PaletteLog, 0f, 0.4f),
                ("detail.pinecone", Kit(meshes["FO_PineCone"]), PaletteLog, 0f, 0.4f),
                ("detail.pebble_a", Shared("detail.pebble_a"), PaletteRock, 0f, 0.3f),
            };
            var kit = AssetDatabase.LoadAssetAtPath<ScatterKit>(ScatterKitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<ScatterKit>();
                AssetDatabase.CreateAsset(kit, ScatterKitPath);
            }
            kit.material = foliage;
            kit.kinds = kinds.Select(k => new ScatterKit.Kind
            {
                id = k.id, mesh = k.mesh, paletteId = Id(k.palette), windScale = k.wind, minDensity = k.minDensity,
            }).ToArray();
            EditorUtility.SetDirty(kit);
            return kit;
        }

        /// <summary>Scatter kinds that are solid: they never overlap each other (Collision_Rule.md, ground details).</summary>
        public static bool IsSolidScatter(string id) =>
            id.StartsWith("flower.", StringComparison.Ordinal) || id.StartsWith("detail.pebble", StringComparison.Ordinal)
            || id == "detail.acorn" || id == "detail.pinecone" || id.StartsWith("detail.mushroom", StringComparison.Ordinal);

        // ---------------- life, weather, zones ----------------

        /// <summary>
        /// The Forest's daytime life on the Grassland's life material and meshes: small canopy birds (wrens, every bird anchor)
        /// and two butterfly colorways in the Clearing only. The fireflies come from the season looks every evening.
        /// </summary>
        private static AmbientLifeKit EnsureLifeKit()
        {
            var grass = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(GrasslandPipeline.LifeKitPath)
                        ?? throw new InvalidOperationException("Build the Grassland life kit first: " + GrasslandPipeline.LifeKitPath);
            AmbientLifeKit.Species Copy(string id)
            {
                foreach (var species in grass.species)
                    if (species.id == id)
                        return species;
                throw new InvalidOperationException($"{GrasslandPipeline.LifeKitPath} has no {id}.");
            }
            var wren = Copy("life.robin");
            wren.id = "life.wren";
            wren.paletteId = Id(PaletteLifeBird);
            wren.anchorPrefix = BirdAnchorPrefix;
            wren.perAnchor = 2;
            wren.scale = 1.25f;
            var amber = Copy("life.butterfly_yellow");
            amber.id = "life.butterfly_amber";
            amber.paletteId = Id(PaletteButterflyAmber);
            amber.anchorPrefix = GladeButterflyPrefix;
            amber.perAnchor = 2;
            var cream = Copy("life.butterfly_blue");
            cream.id = "life.butterfly_cream";
            cream.paletteId = Id(PaletteButterflyCream);
            cream.anchorPrefix = GladeButterflyPrefix;
            cream.perAnchor = 1;
            cream.spawnChance = 0.7f;

            var kit = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(LifeKitPath);
            if (kit == null)
            {
                BiomeWizard.EnsureFolder(Path.GetDirectoryName(LifeKitPath)?.Replace('\\', '/'));
                kit = ScriptableObject.CreateInstance<AmbientLifeKit>();
                AssetDatabase.CreateAsset(kit, LifeKitPath);
            }
            kit.material = grass.material;
            kit.species = new[] { wren, amber, cream };
            EditorUtility.SetDirty(kit);
            return kit;
        }

        /// <summary>
        /// Where the Forest's sun shafts stand past the camera focus (m; the Prairie's at 3.5). Additive cards drawn in front of
        /// a trunk wash it out: the Clearing's ancient tree read half transparent under the shaft that stood at 3.5 m. Standing
        /// deeper, the bright low part of each shaft (1 to 3.7 m up, slanted toward the sun) stays behind the hero's trunk and
        /// the front of its crown, and falls between the back rim's trunks.
        /// </summary>
        public const float RayDepth = 4.8f;

        /// <summary>The Grassland's weather (shared meshes and materials), with the Forest's sun shafts (Entry and Clearing, from Équilibrée).</summary>
        private static WeatherKit EnsureWeatherKit()
        {
            var source = AssetDatabase.LoadAssetAtPath<WeatherKit>(GrasslandPipeline.WeatherKitPath)
                         ?? throw new InvalidOperationException("Build the Grassland weather kit first: " + GrasslandPipeline.WeatherKitPath);
            var kit = AssetDatabase.LoadAssetAtPath<WeatherKit>(WeatherKitPath);
            if (kit == null)
            {
                BiomeWizard.EnsureFolder(Path.GetDirectoryName(WeatherKitPath)?.Replace('\\', '/'));
                kit = Object.Instantiate(source);
                AssetDatabase.CreateAsset(kit, WeatherKitPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, kit);
            }
            kit.name = Path.GetFileNameWithoutExtension(WeatherKitPath);
            var spec = BiomeWizard.ForestSpec();
            kit.sunRaysFrom = spec.SunRaysFrom;
            kit.sunRayAreas = BiomeWizard.SunRayAreas(spec);
            kit.rayDepth = RayDepth;
            EditorUtility.SetDirty(kit);
            return kit;
        }

        /// <summary>
        /// DEF_ZoneKit_Forest (zones.forest, the wizard registers it): the hedge closing the next Forest zone is a row of the
        /// Forest's round bushes (FO_Bush_A and FO_Bush_C, palette.forest.bush), the sign is the Prairie's wooden sign in the
        /// Forest's wood row. No gate: the Lake's comes with the Lake.
        /// </summary>
        private static ZoneKit EnsureZoneKit(Dictionary<string, Mesh> meshes, Material env)
        {
            var grass = AssetDatabase.LoadAssetAtPath<ZoneKit>(ZonePipeline.KitPath);
            var kit = AssetDatabase.LoadAssetAtPath<ZoneKit>(ZoneKitPath);
            if (kit == null)
            {
                BiomeWizard.EnsureFolder(Path.GetDirectoryName(ZoneKitPath)?.Replace('\\', '/'));
                kit = ScriptableObject.CreateInstance<ZoneKit>();
                AssetDatabase.CreateAsset(kit, ZoneKitPath);
            }
            kit.bushes = new[] { Kit(meshes["FO_Bush_A"]), Kit(meshes["FO_Bush_C"]) };
            kit.material = env;
            kit.paletteId = Id(PaletteBush);
            kit.brightness = WorldChunk.NeutralBrightness;
            kit.bushHeight = kit.bushes.Max(m => m.bounds.max.y);
            kit.bushRadius = kit.bushes.Max(MeshRadius);
            kit.spacing = 2f * kit.bushRadius;
            kit.signMesh = grass != null ? grass.signMesh : null;
            if (kit.signMesh != null)
            {
                kit.signRadius = MeshRadius(kit.signMesh);
                kit.signHeight = kit.signMesh.bounds.max.y;
                kit.signPaletteId = Id(PaletteLog);
                kit.signBrightness = WorldChunk.NeutralBrightness;
            }
            kit.gateMesh = null;
            kit.gateMaterial = null;
            EditorUtility.SetDirty(kit);
            return kit;
        }

        private static float MeshRadius(Mesh mesh)
        {
            var b = mesh.bounds;
            return Mathf.Max(Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z)));
        }

        // ---------------- palettes and moods ----------------

        /// <summary>
        /// The Forest's rows of BiomeWizard.ForestSpec written over DEF_Palettes_Forest (the wizard only adds missing rows): the
        /// T50 art pass made the floor a light moss with warm litter and a light tan path, the leaves, ferns and bushes lighter, the
        /// occlusion soft. SeasonLookPipeline.BuildAll derives the season rows from these afterwards.
        /// </summary>
        private static string TunePalettes()
        {
            var set = AssetDatabase.LoadAssetAtPath<PaletteSetAsset>(PalettesPath);
            if (set == null)
                return "palettes: " + PalettesPath + " missing";
            int written = 0;
            foreach (var (row, hex) in BiomeWizard.ForestSpec().Palettes)
            {
                var entry = set.palettes.FirstOrDefault(p => p != null && p.sourceId == row);
                if (entry == null)
                    continue;
                entry.baseColor = Hex(hex[0]);
                entry.regionR = Hex(hex[1]);
                entry.regionG = Hex(hex[2]);
                entry.regionB = Hex(hex[3]);
                entry.regionA = Hex(hex[4]);
                entry.occlusion = Hex(hex[5]);
                entry.accent = Hex(hex[6]);
                written++;
            }
            EditorUtility.SetDirty(set);
            return $"{written} Forest palette rows written";
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.magenta;

        /// <summary>
        /// The Forest's four moods, made again from the Prairie's on every build (the wizard makes LP_Forest_* only once): pulled
        /// toward the Forest mood (a warm amber sun, a light moss ambient, a sage sky, a light warm fog), a touch more sun, softer
        /// crown shadows, a weaker and lighter fog, so the Forest at noon is only a little deeper than the Prairie, never gloomy.
        /// Then the morning mist, low and gentle (the tiers keep their height fog factor: none on Fluide). The sky follows the day.
        /// </summary>
        private static string TuneMoods()
        {
            var spec = BiomeWizard.ForestSpec();
            int tuned = 0;
            LightingProfile day = null;
            foreach (string mood in BiomeWizard.Moods)
            {
                string path = Paths.Lighting(mood);
                var profile = AssetDatabase.LoadAssetAtPath<LightingProfile>(path);
                var seed = AssetDatabase.LoadAssetAtPath<LightingProfile>($"{LightingPipeline.LightingFolder}/LP_Grassland_{mood}.asset");
                if (profile == null || seed == null)
                    continue;
                var sky = profile.sky;
                EditorUtility.CopySerialized(seed, profile);
                profile.name = Path.GetFileNameWithoutExtension(path);
                profile.sky = sky;
                bool night = mood == "Night";
                BiomeWizard.Tint(profile, spec.Mood, night ? spec.Mood.Strength * 0.5f : spec.Mood.Strength);
                if (!night)
                    profile.sunIntensity *= 1.05f;
                profile.shadowStrength *= 0.88f;
                profile.fogColor = Color.Lerp(profile.fogColor, Color.white, 0.2f);
                profile.fogStart += 4f;
                profile.fogEnd += 10f;
                profile.heightFogStrength *= 0.7f;
                if (mood == "Morning")
                {
                    profile.heightFogStrength = 0.44f;
                    profile.heightFogBase = 0f;
                    profile.heightFogFalloff = 1.0f;
                    profile.heightFogStart = 11f;
                    profile.heightFogRange = 22f;
                }
                EditorUtility.SetDirty(profile);
                if (mood == "Day")
                    day = profile;
                tuned++;
            }
            if (day != null && day.sky != null)
                LightingPipeline.WriteSky(day);
            return $"{tuned} Forest moods tuned (morning mist on LP_Forest_Morning)";
        }

        // ---------------- neighbours ----------------

        /// <summary>
        /// The ForestEdge's props as built (when the Grassland strip was not rebuilt in this session), so the Entry settles
        /// against them; the Forest's own entries are cleared (each build settles west to east, like the first one).
        /// </summary>
        private static void SeedNeighbours(Dictionary<string, GameObject> prefabs)
        {
            foreach (var chunk in Chunks)
                GrasslandPipeline.BuiltProps.Remove(chunk.SettleName);
            const string edge = "ForestEdge";
            if (GrasslandPipeline.BuiltProps.ContainsKey(edge))
                return;
            var entry = GrasslandPipeline.StripChunkEntries.FirstOrDefault(e => e.address == "chunk.grassland.forestedge");
            var prefab = string.IsNullOrEmpty(entry.path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(entry.path);
            var props = prefab != null ? prefab.transform.Find("Props") : null;
            if (props == null)
                return;
            var list = new List<(string prop, Vector3 position, float yaw, float scale)>();
            foreach (Transform group in props)
            {
                foreach (Transform instance in group)
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject);
                    if (source == null || !source.name.StartsWith("PF_", StringComparison.Ordinal) || !prefabs.ContainsKey(source.name.Substring(3)))
                        continue;
                    list.Add((source.name.Substring(3), entry.origin + instance.localPosition, instance.localEulerAngles.y, instance.localScale.x));
                }
            }
            GrasslandPipeline.BuiltProps[edge] = list;
        }
    }
}
