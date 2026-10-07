using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace PocketWilds.Editor.Environment
{
    /// <summary>
    /// T33 Prairie Magnifique: the instanced scatter vocabulary (DEF_ScatterKit_Grassland on MAT_Foliage_Grassland),
    /// the richer prop kit (bushes, trees B/C/D, hero rocks, log, stump, fence, hay bale, bird bath), and both
    /// grassland chunks recomposed: framing props as plain renderers, every ground detail as ScatterData drawn by the
    /// ScatterRenderer (no merged grass meshes, no GameObject per plant). Deterministic and idempotent.
    /// Source: SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Magnifique.blend (pw_grassland_t33.py).
    /// </summary>
    public static partial class GrasslandPipeline
    {
        public const string FoliageShader = "PocketWilds/FoliageWind";
        public const string FoliageMaterialPath = "Assets/_Game/Art/Materials/MAT_Foliage_Grassland.mat";
        public const string ScatterFolder = "Assets/_Game/Content/World/Scatter";
        public const string ScatterKitPath = ScatterFolder + "/DEF_ScatterKit_Grassland.asset";
        public const string WorldQualityPath = "Assets/_Game/Content/Quality/DEF_WorldQuality.asset";
        public const string StartScatterPath = ScatterFolder + "/SCT_Chunk_Grassland_Start.asset";
        public const string MeadowScatterPath = ScatterFolder + "/SCT_Chunk_Grassland_Meadow.asset";
        public const string LifeShader = "PocketWilds/AmbientLife";
        public const string LifeMaterialPath = "Assets/_Game/Art/Materials/MAT_AmbientLife_Grassland.mat";
        public const string LifeKitPath = "Assets/_Game/Content/World/DEF_AmbientLife_Grassland.asset";

        public const string PaletteFlowerRed = "palette.grassland.flower_red";
        public const string PaletteFlowerLavender = "palette.grassland.flower_lavender";
        public const string PalettePebble = "palette.grassland.pebble";
        public const string PaletteLeaf = "palette.grassland.leaf";
        public const string PaletteClover = "palette.grassland.clover";
        public const string PaletteMushroom = "palette.grassland.mushroom";
        public const string PaletteBush = "palette.grassland.bush";
        public const string PaletteBushBlossom = "palette.grassland.bush_blossom";
        public const string PaletteBirch = "palette.grassland.birch";
        public const string PaletteFruitTree = "palette.grassland.fruit_tree";
        public const string PaletteWood = "palette.grassland.wood";
        public const string PaletteHay = "palette.grassland.hay";
        public const string PaletteStoneWater = "palette.grassland.stone_water";
        public const string PaletteButterflyYellow = "palette.grassland.life_butterfly_yellow";
        public const string PaletteButterflyBlue = "palette.grassland.life_butterfly_blue";
        public const string PaletteButterflyGolden = "palette.grassland.life_butterfly_golden";
        public const string PaletteBee = "palette.grassland.life_bee";
        public const string PaletteBird = "palette.grassland.life_bird";

        private static void AddMagnifiquePalettes(PaletteSetAsset set)
        {
            Add(set, PaletteFlowerRed, "6FAF55", "EF6F5E", "3D3546", "6FAF55", "6FAF55", "4A7340", "F49A6A");
            Add(set, PaletteFlowerLavender, "7FA36A", "A68AE0", "A68AE0", "7FA36A", "7FA36A", "4E6E44", "C8B0F2");
            Add(set, PalettePebble, "DCD0B8", "E9E1CF", "AFC994", "DCD0B8", "DCD0B8", "A09276", "EFE7D6");
            Add(set, PaletteLeaf, "9DC160", "E0BC62", "7FA650", "9DC160", "9DC160", "6C8A48", "D19A58");
            Add(set, PaletteClover, "5C9A4A", "86C466", "D7EBC4", "5C9A4A", "5C9A4A", "3F6A36", "73B35A");
            Add(set, PaletteMushroom, "F3EAD6", "DB6A5A", "FFF7EA", "F3EAD6", "F3EAD6", "A08A72", "C98D5E");
            Add(set, PaletteBush, "6A4E3C", "6FAE57", "9ED07A", "4C8644", "6FAE57", "355E33", "FFFFFF");
            Add(set, PaletteBushBlossom, "6A4E3C", "73B25A", "A2D27E", "4F8A46", "F5C2D6", "355E33", "FFFFFF");
            Add(set, PaletteBirch, "EEE7D8", "92CC72", "BDE39A", "62A055", "4A4038", "3E6638", "FFFFFF");
            Add(set, PaletteFruitTree, "8E6650", "76B35C", "A3D27C", "538C49", "E25B4C", "3A6337", "FFFFFF");
            Add(set, PaletteWood, "A07A55", "D9B98E", "9CC77A", "6E5038", "7A5A40", "4F3A2C", "FFFFFF");
            Add(set, PaletteHay, "E2C677", "F2DC97", "D6B65F", "C7A452", "E2C677", "9C8452", "FFFFFF");
            Add(set, PaletteStoneWater, "E5DAC2", "E5DAC2", "9CC77A", "8FCBEA", "E5DAC2", "A39478", "FFFFFF");
            // Life: column 6 is the second variant every other individual wears on region R (wings, breast).
            Add(set, PaletteButterflyYellow, "3E3540", "F6C85F", "F08A4B", "3E3540", "3E3540", "2E2830", "FFF6E4");
            Add(set, PaletteButterflyBlue, "3E3540", "9CC8F2", "5C7FD0", "3E3540", "3E3540", "2E2830", "D3B8F2");
            Add(set, PaletteButterflyGolden, "4A3A2A", "FFCF3A", "FFF0A8", "4A3A2A", "4A3A2A", "35291E", "FFB52E");
            Add(set, PaletteBee, "3A3030", "EEF4F8", "F2C14E", "3A3030", "3A3030", "2A2424", "EEF4F8");
            Add(set, PaletteBird, "8C6A52", "F2A06A", "6E5242", "E8B64E", "8C6A52", "5A4636", "EADBC4");
            AddStripPalettes(set);
        }

        [MenuItem("Pocket Wilds/Environment/Build Grassland Magnifique (T33)", priority = 175)]
        public static void BuildMagnifiqueMenu() => Debug.Log("[GrasslandPipeline] " + BuildMagnifique());

        public static string BuildMagnifique()
        {
            EnsurePalettes();
            var env = EnsureMaterial(EnvironmentMaterialPath, EnvironmentShader, "MAT_Environment_Common");
            var foliage = EnsureFoliageMaterial();
            var kit = EnsureScatterKit(foliage);
            EnsureLifeKit(EnsureLifeMaterial());
            EnsureWeatherKit();
            EnsureWaterMaterial();
            var prefabs = BuildMagnifiquePrefabs(env);
            EnsureWorldQuality();
            string start = BuildStartChunkT33(prefabs, kit);
            string meadow = BuildMeadowChunkT33(prefabs, kit);
            AssetDatabase.SaveAssets();
            return $"{kit.kinds.Length} scatter kinds; {start}; {meadow}";
        }

        // ---------------- materials, kit, quality ----------------

        private static Material EnsureFoliageMaterial()
        {
            var shader = Shader.Find(FoliageShader) ?? throw new InvalidOperationException("Shader not found: " + FoliageShader);
            var material = AssetDatabase.LoadAssetAtPath<Material>(FoliageMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "MAT_Foliage_Grassland" };
                AssetDatabase.CreateAsset(material, FoliageMaterialPath);
            }
            material.shader = shader;
            // Drawn only by the ScatterRenderer (Graphics.RenderMeshInstanced): instancing, palette row per draw.
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Dictionary<string, Mesh> ModelMeshes()
        {
            var meshes = new Dictionary<string, Mesh>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                {
                    // A mesh exported into two FBX files would make the kit pick a stale copy at random.
                    if (meshes.TryGetValue(mesh.name, out var other))
                        throw new InvalidOperationException($"Mesh {mesh.name} is exported twice ({path} and {AssetDatabase.GetAssetPath(other)}).");
                    meshes[mesh.name] = mesh;
                }
            }
            if (!meshes.ContainsKey("GL_GrassWisp_M") || !meshes.ContainsKey("GL_Bush_A"))
                throw new InvalidOperationException("T33 models missing: export GL_Scatter.fbx and GL_Props_T33.fbx from BL_Biome_Grassland_Magnifique.blend.");
            return meshes;
        }

        private static readonly (string id, string mesh, string palette, float wind, float minDensity)[] ScatterKinds =
        {
            ("grass.tuft_s", "GL_GrassTuft_S", PaletteGrass, 1.0f, 0f),
            ("grass.tuft_m", "GL_GrassTuft_M", PaletteGrass, 1.0f, 0f),
            ("grass.tuft_l", "GL_GrassTuft_L", PaletteGrass, 1.0f, 0f),
            ("grass.wisp_s", "GL_GrassWisp_S", PaletteGrass, 1.2f, 0f),
            ("grass.wisp_m", "GL_GrassWisp_M", PaletteGrass, 1.2f, 0f),
            ("grass.wisp_l", "GL_GrassWisp_L", PaletteGrass, 1.25f, 0f),
            ("flower.daisy_white", "GL_Flower_A", "palette.grassland.flower_white", 0.7f, 0.55f),
            ("flower.daisy_yellow", "GL_Flower_A", "palette.grassland.flower_yellow", 0.7f, 0.55f),
            ("flower.bell_blue", "GL_Flower_B", "palette.grassland.flower_blue", 0.7f, 0.55f),
            ("flower.bell_lilac", "GL_Flower_B", "palette.grassland.flower_lilac", 0.7f, 0.55f),
            ("flower.clover_pink", "GL_Flower_C", "palette.grassland.flower_pink", 0.6f, 0.55f),
            ("flower.poppy", "GL_Flower_D", PaletteFlowerRed, 0.8f, 0.55f),
            ("flower.lavender", "GL_Flower_E", PaletteFlowerLavender, 0.8f, 0.55f),
            ("flower.buttercup", "GL_Flower_F", "palette.grassland.flower_yellow", 0.6f, 0.55f),
            ("detail.pebble_a", "GL_Pebble_A", PalettePebble, 0f, 0.3f),
            ("detail.pebble_b", "GL_Pebble_B", PalettePebble, 0f, 0.3f),
            ("detail.pebble_c", "GL_Pebble_C", PalettePebble, 0f, 0.3f),
            ("detail.leaf", "GL_Leaf_A", PaletteLeaf, 0.12f, 0f),
            ("detail.petals_pink", "GL_Petals_A", "palette.grassland.flower_pink", 0f, 0f),
            ("detail.petals_white", "GL_Petals_A", "palette.grassland.flower_white", 0f, 0f),
            ("detail.clover", "GL_Clover_A", PaletteClover, 0.3f, 0f),
            ("detail.mushroom", "GL_Mushroom_Tiny", PaletteMushroom, 0f, 0.5f),
        };

        private static ScatterKit EnsureScatterKit(Material foliage)
        {
            var meshes = ModelMeshes();
            Directory.CreateDirectory(ScatterFolder);
            var kit = AssetDatabase.LoadAssetAtPath<ScatterKit>(ScatterKitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<ScatterKit>();
                AssetDatabase.CreateAsset(kit, ScatterKitPath);
            }
            kit.material = foliage;
            kit.kinds = ScatterKinds.Select(k => new ScatterKit.Kind
            {
                id = k.id,
                // Standalone copies: the FBX never enters a bundle (its model prefab drags the URP Lit material).
                mesh = KitMesh(meshes[k.mesh]),
                paletteId = ContentId.FromSource(k.palette).Value,
                windScale = k.wind,
                minDensity = k.minDensity,
            }).ToArray();
            EditorUtility.SetDirty(kit);
            return kit;
        }

        private static Material EnsureLifeMaterial()
        {
            var shader = Shader.Find(LifeShader) ?? throw new InvalidOperationException("Shader not found: " + LifeShader);
            var material = AssetDatabase.LoadAssetAtPath<Material>(LifeMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "MAT_AmbientLife_Grassland" };
                AssetDatabase.CreateAsset(material, LifeMaterialPath);
            }
            material.shader = shader;
            // Drawn only by AmbientLife (Graphics.RenderMeshInstanced): palette row and wing flap per draw.
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Grassland life: two butterfly colorways (yellow/cream, blue/lilac) at VFX_Butterfly anchors, bees at
        /// VFX_Bees, robins at VFX_Birds. Sizes are enlarged a little so they read from the gameplay camera.
        /// </summary>
        public static AmbientLifeKit EnsureLifeKit(Material material)
        {
            var meshes = ModelMeshes();
            if (!meshes.ContainsKey("GL_Life_Bird"))
                throw new InvalidOperationException("Export GL_Life.fbx from BL_Biome_Grassland_Magnifique.blend first.");
            var kit = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(LifeKitPath);
            if (kit == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LifeKitPath));
                kit = ScriptableObject.CreateInstance<AmbientLifeKit>();
                AssetDatabase.CreateAsset(kit, LifeKitPath);
            }
            kit.material = material;
            var butterfly = KitMesh(meshes["GL_Life_Butterfly"]);
            kit.species = new[]
            {
                new AmbientLifeKit.Species
                {
                    id = "life.butterfly_yellow", mesh = butterfly, paletteId = ContentId.FromSource(PaletteButterflyYellow).Value,
                    behaviour = AmbientLifeBehaviour.Butterfly, anchorPrefix = "VFX_Butterfly", perAnchor = 2, spawnChance = 1f, scale = 2.3f,
                    flap = new Vector3(15f, 0.95f, 0.35f), flapGrounded = new Vector3(15f, 0.95f, 0.35f), shoulder = new Vector2(0.004f, 0f),
                },
                new AmbientLifeKit.Species
                {
                    id = "life.butterfly_blue", mesh = butterfly, paletteId = ContentId.FromSource(PaletteButterflyBlue).Value,
                    behaviour = AmbientLifeBehaviour.Butterfly, anchorPrefix = "VFX_Butterfly", perAnchor = 1, spawnChance = 1f, scale = 2.2f,
                    flap = new Vector3(13f, 0.9f, 0.3f), flapGrounded = new Vector3(13f, 0.9f, 0.3f), shoulder = new Vector2(0.004f, 0f),
                },
                new AmbientLifeKit.Species
                {
                    // Doc 04: the golden butterfly is the rare one, living at about a third of the butterfly anchors.
                    id = "life.butterfly_golden", mesh = butterfly, paletteId = ContentId.FromSource(PaletteButterflyGolden).Value,
                    behaviour = AmbientLifeBehaviour.Butterfly, anchorPrefix = "VFX_Butterfly", perAnchor = 1, spawnChance = 0.35f, scale = 2.4f,
                    flap = new Vector3(12f, 0.9f, 0.3f), flapGrounded = new Vector3(12f, 0.9f, 0.3f), shoulder = new Vector2(0.004f, 0f),
                },
                new AmbientLifeKit.Species
                {
                    id = "life.bee", mesh = KitMesh(meshes["GL_Life_Bee"]), paletteId = ContentId.FromSource(PaletteBee).Value,
                    behaviour = AmbientLifeBehaviour.Bee, anchorPrefix = "VFX_Bees", perAnchor = 3, spawnChance = 1f, scale = 2.0f,
                    flap = new Vector3(70f, 0.5f, 0.25f), flapGrounded = new Vector3(70f, 0.5f, 0.25f), shoulder = new Vector2(0.006f, 0.009f),
                },
                new AmbientLifeKit.Species
                {
                    id = "life.robin", mesh = KitMesh(meshes["GL_Life_Bird"]), paletteId = ContentId.FromSource(PaletteBird).Value,
                    behaviour = AmbientLifeBehaviour.Bird, anchorPrefix = "VFX_Birds", perAnchor = 2, spawnChance = 1f, scale = 1.35f,
                    // Folded: the wings swing down inside the body; in flight they beat about three times a second.
                    flap = new Vector3(20f, 1.0f, 0.1f), flapGrounded = new Vector3(0f, 0f, -1.85f), shoulder = new Vector2(0.034f, 0.082f),
                },
            };
            EditorUtility.SetDirty(kit);
            return kit;
        }

        private static void EnsureWorldQuality()
        {
            var quality = AssetDatabase.LoadAssetAtPath<WorldQualityProfile>(WorldQualityPath);
            if (quality == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(WorldQualityPath));
                quality = ScriptableObject.CreateInstance<WorldQualityProfile>();
                AssetDatabase.CreateAsset(quality, WorldQualityPath);
            }
            // Reference values live in code (doc 07 table): rerunning the pipeline restores them, new fields included.
            quality.low = WorldQualityProfile.Default(QualityTier.Low);
            quality.medium = WorldQualityProfile.Default(QualityTier.Medium);
            quality.high = WorldQualityProfile.Default(QualityTier.High);
            EditorUtility.SetDirty(quality);
        }

        // ---------------- prop prefabs ----------------

        private static Dictionary<string, GameObject> BuildMagnifiquePrefabs(Material env)
        {
            var meshes = ModelMeshes();
            Directory.CreateDirectory(PrefabsFolder);
            foreach (var kind in new[] { "A", "B", "C" })
                SaveSingle(KitMesh(meshes["GL_Bush_" + kind]), env, ShadowCastingMode.On, 0.008f);
            foreach (var kind in new[] { "B", "C", "D" })
                SaveTreeKind(meshes, env, kind);
            foreach (var kind in new[] { "A", "B" })
                SaveSingle(KitMesh(meshes["GL_RockHero_" + kind]), env, ShadowCastingMode.On, 0.004f);
            foreach (var name in new[] { "GL_Log", "GL_Stump", "GL_Fence_A", "GL_HayBale", "GL_BirdBath" })
                SaveSingle(KitMesh(meshes[name]), env, ShadowCastingMode.On, 0.008f);

            var prefabs = new Dictionary<string, GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                prefabs[prefab.name.Substring(3)] = prefab; // PF_GL_Bush_A -> GL_Bush_A
            }
            return prefabs;
        }

        private static GameObject SaveTreeKind(Dictionary<string, Mesh> meshes, Material material, string kind)
        {
            var go = new GameObject("PF_GL_Tree_" + kind);
            try
            {
                var renderers = new Renderer[3];
                for (int lod = 0; lod < 3; lod++)
                {
                    var child = new GameObject($"GL_Tree_{kind}_LOD{lod}");
                    child.transform.SetParent(go.transform, false);
                    renderers[lod] = AddRenderer(child, KitMesh(meshes[$"GL_Tree_{kind}_LOD{lod}"]), material,
                        lod < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off);
                }
                var group = go.AddComponent<LODGroup>();
                group.SetLODs(new[]
                {
                    new LOD(TreeLods[0], new[] { renderers[0] }),
                    new LOD(TreeLods[1], new[] { renderers[1] }),
                    new LOD(TreeLods[2], new[] { renderers[2] }),
                });
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();
                return PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsFolder}/PF_GL_Tree_{kind}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------------- layouts ----------------

        private static string PaletteFor(string prop) => prop switch
        {
            _ when prop.StartsWith("GL_Landmark_OldTree", StringComparison.Ordinal) => PaletteOldTree,
            _ when prop.StartsWith("GL_Landmark_Windmill", StringComparison.Ordinal) => PaletteWindmill,
            "GL_Fence_B" => PaletteFenceWhite,
            "GL_WellTiny" => PaletteWell,
            "GL_Edge_B" => PaletteEdgeRoots,
            "GL_StreamStones" => PaletteStreamStone,
            "GL_Tree_C" => PaletteBirch,
            "GL_Tree_D" => PaletteFruitTree,
            "GL_Bush_B" => PaletteBushBlossom,
            "GL_Log" or "GL_Stump" or "GL_Fence_A" => PaletteWood,
            "GL_HayBale" => PaletteHay,
            "GL_BirdBath" => PaletteStoneWater,
            _ when prop.StartsWith("GL_Tree", StringComparison.Ordinal) => PaletteTree,
            _ when prop.StartsWith("GL_Bush", StringComparison.Ordinal) => PaletteBush,
            _ => PaletteRock,
        };

        private static Placement Prop(string prop, float x, float z, float yaw, float scale, bool secondary = false) => new Placement
        {
            Prop = prop, Position = new Vector3(x, 0f, z), Yaw = yaw, Scale = scale, Palette = PaletteFor(prop), Secondary = secondary,
        };

        /// <summary>
        /// Start chunk framing (portrait camera looks from -Z toward +Z): trees and the taller bushes stand behind and
        /// on the far sides, low props (log, stump, bird bath) may sit near the camera, the play area stays open.
        /// </summary>
        private static List<Placement> StartLayoutT33()
        {
            return new List<Placement>
            {
                Prop("GL_Tree_A", -1.8f, 4.5f, 30f, 1.0f), Prop("GL_Tree_D", 2.3f, 6.0f, 200f, 0.95f),
                Prop("GL_Tree_B", -4.7f, 1.2f, 120f, 0.95f), Prop("GL_Tree_C", 4.5f, 2.4f, 300f, 1.0f),
                Prop("GL_Tree_C", -3.4f, 6.0f, 80f, 0.85f), Prop("GL_Tree_A", 5.1f, -2.6f, 10f, 0.85f),
                Prop("GL_Tree_B", -5.1f, -3.1f, 170f, 0.9f),
                Prop("GL_Bush_A", -2.9f, 3.1f, 20f, 1.0f), Prop("GL_Bush_B", 3.2f, 3.7f, 140f, 1.0f),
                Prop("GL_Bush_C", -5.7f, -1.0f, 60f, 1.0f), Prop("GL_Bush_B", 0.9f, 5.7f, 250f, 0.9f, secondary: true),
                Prop("GL_Bush_A", 5.6f, 0.6f, 300f, 0.9f), Prop("GL_Bush_C", -0.7f, 6.4f, 10f, 0.85f, secondary: true),
                Prop("GL_Rock_L", 2.0f, 1.7f, 40f, 1f), Prop("GL_Rock_S", 2.6f, 1.15f, 0f, 1f), Prop("GL_Rock_M", -2.0f, -0.4f, 100f, 1f),
                Prop("GL_Rock_S", -1.55f, 2.8f, 210f, 0.9f), Prop("GL_Rock_M", 1.1f, 4.9f, 10f, 0.9f),
                Prop("GL_RockHero_A", -4.3f, -1.6f, 200f, 0.8f), Prop("GL_Rock_S", -3.6f, -0.6f, 60f, 1.1f),
                Prop("GL_Rock_M", 4.6f, -0.7f, 250f, 1f), Prop("GL_Rock_S", 0.7f, -3.7f, 120f, 1f),
                Prop("GL_RockHero_B", -5.4f, 3.7f, 330f, 0.75f), Prop("GL_Rock_S", 3.4f, 4.4f, 15f, 1f),
                Prop("GL_Rock_L", 3.9f, -4.2f, 75f, 0.8f),
                Prop("GL_Log", 2.8f, -3.0f, 30f, 1.0f), Prop("GL_Stump", -3.0f, -3.7f, 0f, 1.0f),
                Prop("GL_BirdBath", 2.5f, 3.0f, 0f, 1.0f), Prop("GL_HayBale", 4.0f, 5.5f, 70f, 1.0f, secondary: true),
                Prop("GL_Fence_A", 0.6f, 6.65f, 0f, 1.0f), Prop("GL_Fence_A", 1.9f, 6.6f, 4f, 1.0f, secondary: true),
            };
        }

        /// <summary>Meadow chunk: trees framing the far sides, a fence and hay along the path, open grass in the middle.</summary>
        private static List<Placement> MeadowLayoutT33()
        {
            return new List<Placement>
            {
                Prop("GL_Tree_B", -6.4f, 6.1f, 40f, 0.95f), Prop("GL_Tree_C", -2.4f, 5.4f, 200f, 0.9f),
                Prop("GL_Tree_D", 2.6f, 5.8f, 120f, 1.0f), Prop("GL_Tree_A", 5.6f, 3.9f, 300f, 0.9f),
                Prop("GL_Tree_C", 5.4f, -4.2f, 15f, 0.85f), Prop("GL_Tree_B", 1.6f, -5.6f, 250f, 0.8f),
                Prop("GL_Bush_B", -4.3f, 4.7f, 20f, 1.0f), Prop("GL_Bush_A", 0.2f, 6.1f, 90f, 0.95f),
                Prop("GL_Bush_C", 4.2f, 5.6f, 0f, 0.9f, secondary: true), Prop("GL_Bush_A", 6.2f, -1.6f, 210f, 0.9f),
                Prop("GL_Bush_B", -1.4f, -5.2f, 300f, 0.85f, secondary: true),
                Prop("GL_RockHero_A", -6.4f, -5.9f, 80f, 0.85f), Prop("GL_Rock_M", -5.6f, -4.9f, 10f, 0.9f),
                Prop("GL_Rock_S", 3.3f, 1.6f, 140f, 1f), Prop("GL_Rock_M", -3.2f, -2.6f, 300f, 1f), Prop("GL_Rock_S", 4.4f, -1.9f, 60f, 1.1f),
                Prop("GL_Rock_L", 6.1f, 1.2f, 220f, 0.9f), Prop("GL_Rock_S", -4.1f, 3.4f, 20f, 1f),
                Prop("GL_Fence_A", -1.2f, 1.25f, 4f, 1.0f), Prop("GL_Fence_A", 0.1f, 1.3f, 0f, 1.0f),
                Prop("GL_Fence_A", 1.4f, 1.22f, 358f, 1.0f, secondary: true),
                Prop("GL_HayBale", 2.9f, 2.6f, 20f, 1.0f), Prop("GL_HayBale", 3.7f, 3.1f, 110f, 0.9f, secondary: true),
                Prop("GL_Log", -2.7f, -4.2f, 160f, 1.0f), Prop("GL_Stump", 2.2f, -3.4f, 0f, 1.0f),
            };
        }

        // ---------------- chunk composition ----------------

        private static string BuildStartChunkT33(Dictionary<string, GameObject> prefabs, ScatterKit kit)
        {
            var groundMesh = prefabs["GL_Ground_Core_A"].GetComponent<MeshFilter>().sharedMesh;
            var egg = ClearingCenter(groundMesh);
            var layout = StartLayoutT33();
            var moves = SettleProps("Start", Vector3.zero, layout, prefabs);
            var area = StartWalkArea;
            var rules = new ScatterRules
            {
                // The east side is joined to the meadow chunk (square, flat): scatter runs up to the seam there.
                Seed = 3301, Ground = groundMesh, Worn = WornMask(groundMesh), Solids = PlaceLayout(layout, prefabs),
                Inside = p => p.x > 0f ? p.x < 6.95f && Mathf.Abs(p.z) < 6.6f : InsideIsland(p, 0.35f),
                Clear = egg, ClearRadius = 0.95f, Lawn = area.Center, LawnRadius = 3.0f, FlowerPatches = 22, RimPatches = 8, FlowerSingles = 30,
            };
            var centers = new List<Vector3>();
            var scatter = SaveScatter(StartScatterPath, kit, GenerateScatter(kit, layout, rules, centers));

            var root = new GameObject("PF_Chunk_Grassland_Start");
            try
            {
                var chunk = root.AddComponent<WorldChunk>();
                var bindings = new List<WorldChunk.PaletteBinding>();
                var ground = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["GL_Ground_Core_A"], root.transform);
                ground.name = "Ground";
                bindings.Add(new WorldChunk.PaletteBinding
                {
                    renderer = ground.GetComponent<Renderer>(), paletteId = ContentId.FromSource(PaletteGround).Value, brightness = WorldChunk.NeutralBrightness,
                });
                ComposePropsT33(root.transform, layout, prefabs, new Random(4242), bindings);

                var spawns = Child(root.transform, "SpawnPoints");
                var eggSpawn = Point(spawns, "EggSpawn", egg);
                var animals = new[]
                {
                    Point(spawns, "AnimalSpawn_01", egg + new Vector3(0.95f, 0f, 0.55f)),
                    Point(spawns, "AnimalSpawn_02", egg + new Vector3(-0.95f, 0f, 0.75f)),
                    Point(spawns, "AnimalSpawn_03", egg + new Vector3(0.15f, 0f, 1.6f)),
                };
                var anchors = Child(root.transform, "VFXAnchors");
                var vfxAnchors = new[]
                {
                    Point(anchors, "VFX_Hatch", egg),
                    Point(anchors, "VFX_Butterfly_01", OnGround(groundMesh, -2.2f, 0.6f, 2.0f)),
                    Point(anchors, "VFX_Butterfly_02", OnGround(groundMesh, 2.6f, 0.6f, 3.2f)),
                    Point(anchors, "VFX_Bees_01", OnGround(groundMesh, 3.2f, 0.45f, 3.7f)),
                    Point(anchors, "VFX_Birds_01", OnGround(groundMesh, -1.4f, 0f, -2.6f)),
                };
                var linksRoot = Child(root.transform, "ChunkLinks");
                var links = new[]
                {
                    Point(linksRoot, "Link_N", new Vector3(0f, 0f, 7f)), Point(linksRoot, "Link_E", new Vector3(7f, 0f, 0f)),
                    Point(linksRoot, "Link_S", new Vector3(0f, 0f, -7f)), Point(linksRoot, "Link_W", new Vector3(-7f, 0f, 0f)),
                };
                chunk.SetAuthoring(bindings.ToArray(), area, eggSpawn, animals, InterestsT33(layout, centers, area), vfxAnchors, links);
                chunk.SetScatter(scatter);
                var (obstacles, groundField) = BuildCollision(layout, prefabs, groundMesh, null);
                chunk.SetCollision(obstacles, groundField);
                ClearSpawns(animals, obstacles, groundField);
                PrefabUtility.SaveAsPrefabAsset(root, ChunkPath);
                return $"Start: {layout.Count} props ({SettleSummary(moves)}), {obstacles.Length} obstacles, {scatter.Count} scatter instances, {bindings.Count} renderers";
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static string BuildMeadowChunkT33(Dictionary<string, GameObject> prefabs, ScatterKit kit)
        {
            var groundMesh = AssetDatabase.LoadAllAssetsAtPath($"{ModelsFolder}/GL_Ground_Core_B.fbx").OfType<Mesh>().First();
            var layout = MeadowLayoutT33();
            var moves = SettleProps("Meadow", MeadowOffset, layout, prefabs);
            var area = new WanderArea(Vector3.zero, 3.5f);
            var rules = new ScatterRules
            {
                Seed = 3302, Ground = groundMesh, Worn = WornMask(groundMesh), Solids = PlaceLayout(layout, prefabs),
                // T33B: joined on both sides (start to the west, StreamBend to the east): scatter runs up to both seams.
                Inside = p => Mathf.Abs(p.x) < 6.95f && Mathf.Abs(p.z) < 6.6f,
                Clear = new Vector3(0f, 0f, -100f), ClearRadius = 0f, Lawn = area.Center, LawnRadius = 2.2f,
                FlowerPatches = 34, RimPatches = 5, FlowerSingles = 40,
            };
            var centers = new List<Vector3>();
            var scatter = SaveScatter(MeadowScatterPath, kit, GenerateScatter(kit, layout, rules, centers));

            var root = new GameObject("PF_Chunk_Grassland_Meadow");
            try
            {
                var chunk = root.AddComponent<WorldChunk>();
                var bindings = new List<WorldChunk.PaletteBinding>();
                var ground = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["GL_Ground_Core_B"], root.transform);
                ground.name = "Ground";
                bindings.Add(new WorldChunk.PaletteBinding
                {
                    renderer = ground.GetComponent<Renderer>(), paletteId = ContentId.FromSource(PaletteGround).Value, brightness = WorldChunk.NeutralBrightness,
                });
                ComposePropsT33(root.transform, layout, prefabs, new Random(9157), bindings);
                var spawns = Child(root.transform, "SpawnPoints");
                var animals = new[]
                {
                    Point(spawns, "AnimalSpawn_01", new Vector3(-1.2f, 0f, -0.6f)),
                    Point(spawns, "AnimalSpawn_02", new Vector3(1.4f, 0f, -1.2f)),
                    Point(spawns, "AnimalSpawn_03", new Vector3(0.2f, 0f, 2.6f)),
                };
                var anchors = Child(root.transform, "VFXAnchors");
                var vfxAnchors = new[]
                {
                    Point(anchors, "VFX_Butterfly_01", OnGround(groundMesh, 1.8f, 0.6f, 2.6f)),
                    Point(anchors, "VFX_Butterfly_02", OnGround(groundMesh, -3.0f, 0.6f, -1.8f)),
                    Point(anchors, "VFX_Bees_01", OnGround(groundMesh, -4.3f, 0.45f, 4.6f)),
                    Point(anchors, "VFX_Birds_01", OnGround(groundMesh, 0.8f, 0f, -2.8f)),
                };
                var linksRoot = Child(root.transform, "ChunkLinks");
                var links = new[] { Point(linksRoot, "Link_W", new Vector3(-7f, 0f, 0f)), Point(linksRoot, "Link_E", new Vector3(7f, 0f, 0f)) };
                chunk.SetAuthoring(bindings.ToArray(), area, null, animals, InterestsT33(layout, centers, area), vfxAnchors, links);
                chunk.SetScatter(scatter);
                var (obstacles, groundField) = BuildCollision(layout, prefabs, groundMesh, null);
                chunk.SetCollision(obstacles, groundField);
                ClearSpawns(animals, obstacles, groundField);
                PrefabUtility.SaveAsPrefabAsset(root, MeadowChunkPath);
                return $"Meadow: {layout.Count} props ({SettleSummary(moves)}), {obstacles.Length} obstacles, {scatter.Count} scatter instances, {bindings.Count} renderers";
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ComposePropsT33(Transform root, List<Placement> layout, Dictionary<string, GameObject> prefabs, Random rng,
            List<WorldChunk.PaletteBinding> bindings)
        {
            var props = Child(root, "Props");
            var groups = new (string prefix, Transform group)[]
            {
                ("GL_Landmark", Child(props, "Landmarks")), ("GL_Tree", Child(props, "Trees")), ("GL_Bush", Child(props, "Bushes")),
                ("GL_Rock", Child(props, "Rocks")), ("GL_", Child(props, "Decor")),
            };
            foreach (var placement in layout)
            {
                var group = groups.First(g => placement.Prop.StartsWith(g.prefix, StringComparison.Ordinal)).group;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[placement.Prop], group);
                instance.transform.SetLocalPositionAndRotation(placement.Position, Quaternion.Euler(0f, placement.Yaw, 0f));
                instance.transform.localScale = Vector3.one * placement.Scale;
                byte brightness = (byte)(placement.Prop.StartsWith("GL_Tree", StringComparison.Ordinal) ? 6 + rng.Next(5) : 5 + rng.Next(7));
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    // Windmill sails: their own palette (canvas and wood) and the rotor flag (they turn in the shader).
                    bool sails = renderer.name.Contains("_Sails_");
                    bindings.Add(new WorldChunk.PaletteBinding
                    {
                        renderer = renderer, paletteId = ContentId.FromSource(sails ? PaletteWindmillSails : placement.Palette).Value,
                        brightness = brightness, secondary = placement.Secondary, rotor = sails,
                    });
                }
            }
        }

        private static InterestPoint[] InterestsT33(List<Placement> layout, List<Vector3> flowerCenters, WanderArea area)
        {
            var result = new List<InterestPoint>();
            void Add(Vector3 position, float standOff, InterestKind kind)
            {
                if (Vector3.Distance(new Vector3(position.x, 0f, position.z), new Vector3(area.Center.x, 0f, area.Center.z)) <= area.Radius + standOff + 0.2f)
                    result.Add(new InterestPoint(position, standOff, kind));
            }
            foreach (var p in layout)
            {
                float standOff = PropRadius(p.Prop) * p.Scale;
                if (p.Prop.StartsWith("GL_Fence", StringComparison.Ordinal))
                    continue;
                Add(p.Position, standOff, InterestKind.Inspect);
            }
            foreach (var c in flowerCenters)
                Add(c, 0.16f, InterestKind.Eat);
            return result.ToArray();
        }

        /// <summary>Footprint radius of a prop (m at scale 1): scatter keeps out, animals stop at this distance.</summary>
        private static float PropRadius(string prop) => prop switch
        {
            _ when prop.StartsWith("GL_Tree", StringComparison.Ordinal) => 0.42f,
            "GL_Bush_A" => 0.5f,
            "GL_Bush_B" => 0.55f,
            "GL_Bush_C" => 0.32f,
            "GL_RockHero_A" => 0.85f,
            "GL_RockHero_B" => 0.6f,
            "GL_Rock_L" => 0.62f,
            "GL_Rock_M" => 0.45f,
            "GL_Rock_S" => 0.32f,
            "GL_Log" => 0.55f,
            "GL_Stump" => 0.32f,
            "GL_Fence_A" => 0.62f,
            "GL_HayBale" => 0.38f,
            "GL_BirdBath" => 0.32f,
            "GL_Landmark_OldTree" => 0.95f,
            "GL_Landmark_Windmill" => 1.3f,
            "GL_Fence_B" => 0.72f,
            "GL_WellTiny" => 0.58f,
            "GL_Edge_A" => 0.4f,
            "GL_Edge_B" => 0.2f,
            "GL_StreamStones" => 0.0f,
            _ => 0.3f,
        };

        // ---------------- scatter generation ----------------

        private sealed class ScatterRules
        {
            public int Seed;
            public Mesh Ground;
            public Func<Vector3, bool> Inside;
            public Func<Vector3, float> Worn;
            public Vector3 Clear;
            public float ClearRadius;
            public Vector3 Lawn;
            public float LawnRadius;
            public int FlowerPatches;
            /// <summary>Patches placed first on the lawn rim, inside the gameplay frame.</summary>
            public int RimPatches;
            public int FlowerSingles;
            /// <summary>T33B: nothing grows here (stream and pond water), given the plant's radius.</summary>
            public Func<Vector3, float, bool> Excluded;
            /// <summary>T33C: the props' measured volumes; no plant, flower or pebble ever enters one.</summary>
            public List<CollisionProxies.Placed> Solids;
        }

        private struct ScatterPoint
        {
            public int Kind;
            public Vector3 Position;
            public float Yaw;
            public float Scale;
            public double Rank;
        }

        private static readonly (string id, float weight)[] FlowerSpecies =
        {
            ("flower.daisy_white", 0.17f), ("flower.daisy_yellow", 0.09f), ("flower.bell_blue", 0.1f), ("flower.bell_lilac", 0.08f),
            ("flower.clover_pink", 0.12f), ("flower.poppy", 0.15f), ("flower.lavender", 0.13f), ("flower.buttercup", 0.16f),
        };

        /// <summary>
        /// Rule-based scatter for one chunk: a grass carpet in soft patches (short lawn where animals play, tall wisps
        /// hugging rocks and trees), single-species flower patches, then details where they belong (pebbles at rock
        /// feet and path edges, leaves and mushrooms under trees, petals at flower feet, clover in the open meadow).
        /// </summary>
        private static List<ScatterPoint> GenerateScatter(ScatterKit kit, List<Placement> props, ScatterRules rules, List<Vector3> flowerCenters)
        {
            var rng = new Random(rules.Seed);
            var sampler = new GroundSampler(rules.Ground);
            var points = new List<ScatterPoint>();
            int Kind(string id) => kit.IndexOf(id);

            // T33C: each plant's own footprint (mesh extent seen from above) and the props' measured outlines at ground level.
            var kindRadius = kit.kinds.Select(k =>
            {
                if (k.mesh == null)
                    return 0.05f;
                var b = k.mesh.bounds;
                return Mathf.Max(Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z)));
            }).ToArray();
            var solidKind = kit.kinds.Select(k => k.id.StartsWith("flower.", StringComparison.Ordinal)
                || k.id.StartsWith("detail.pebble", StringComparison.Ordinal) || k.id == "detail.mushroom").ToArray();
            var outlines = (rules.Solids ?? throw new InvalidOperationException("ScatterRules.Solids: the props' volumes are required (T33C).")).Select(s => s.Ground).Where(b => b != null)
                .Select(b =>
                {
                    var c = Vector2.zero;
                    foreach (var v in b.Hull)
                        c += v;
                    c /= b.Hull.Length;
                    return (hull: b.Hull, center: c, reach: b.Hull.Max(v => (v - c).magnitude));
                }).ToList();
            var solids = new Dictionary<(int, int), List<(Vector2 p, float r)>>();

            float Clearance(Vector3 p)
            {
                var q = new Vector2(p.x, p.z);
                float best = float.MaxValue;
                foreach (var (hull, center, reach) in outlines)
                {
                    float far = (q - center).magnitude - reach;
                    if (far >= best)
                        continue;
                    best = Mathf.Min(best, CollisionProxies.HullDistance(hull, q));
                }
                return best;
            }

            float maxSolidRadius = 0.05f;
            bool SolidFree(Vector2 q, float r)
            {
                // Search every cell a neighbour's footprint may reach from.
                int range = Mathf.CeilToInt((r + maxSolidRadius) / 0.25f);
                int cx = Mathf.FloorToInt(q.x / 0.25f), cz = Mathf.FloorToInt(q.y / 0.25f);
                for (int x = cx - range; x <= cx + range; x++)
                    for (int z = cz - range; z <= cz + range; z++)
                        if (solids.TryGetValue((x, z), out var list))
                            foreach (var (other, radius) in list)
                                if ((other - q).sqrMagnitude < (radius + r) * (radius + r))
                                    return false;
                return true;
            }

            bool Place(int kind, Vector3 p, float scale, double rank, float minClearance = 0.02f)
            {
                if (kind < 0 || !rules.Inside(p))
                    return false;
                float radius = kindRadius[kind] * scale;
                var q = new Vector2(p.x, p.z);
                if (Clearance(p) < Mathf.Max(minClearance, radius) || (rules.Excluded != null && rules.Excluded(p, radius)))
                    return false;
                if (solidKind[kind] && !SolidFree(q, radius))
                    return false;
                if (rules.ClearRadius > 0f && Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(rules.Clear.x, 0f, rules.Clear.z)) < rules.ClearRadius)
                    return false;
                if (!sampler.TrySample(p.x, p.z, out float height, out var normal) || normal.y < 0.8f)
                    return false;
                points.Add(new ScatterPoint
                {
                    Kind = kind, Position = new Vector3(p.x, height - 0.01f, p.z), Yaw = (float)(rng.NextDouble() * Math.PI * 2.0),
                    Scale = scale, Rank = rank,
                });
                if (solidKind[kind])
                {
                    var cell = (Mathf.FloorToInt(q.x / 0.25f), Mathf.FloorToInt(q.y / 0.25f));
                    if (!solids.TryGetValue(cell, out var list))
                        solids[cell] = list = new List<(Vector2, float)>();
                    list.Add((q, radius));
                    maxSolidRadius = Mathf.Max(maxSolidRadius, radius);
                }
                return true;
            }

            float Noise(float x, float z, float scale, float offset) => Mathf.PerlinNoise(x * scale + offset, z * scale + offset * 1.7f);

            // Grass carpet: jittered grid, soft density patches, lawn in the play area.
            const float Spacing = 0.21f;
            int[] tufts = { Kind("grass.tuft_s"), Kind("grass.tuft_m"), Kind("grass.tuft_l") };
            int[] wisps = { Kind("grass.wisp_s"), Kind("grass.wisp_m"), Kind("grass.wisp_l") };
            for (float x = -7f; x <= 7f; x += Spacing)
            {
                for (float z = -7f; z <= 7f; z += Spacing)
                {
                    var p = new Vector3(x + (float)(rng.NextDouble() - 0.5) * Spacing * 0.9f, 0f, z + (float)(rng.NextDouble() - 0.5) * Spacing * 0.9f);
                    float lawn = rules.LawnRadius > 0f ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rules.LawnRadius * 0.6f, rules.LawnRadius, Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(rules.Lawn.x, 0f, rules.Lawn.z)))) : 0f;
                    float patch = Mathf.Lerp(0.42f, 1f, Noise(p.x, p.z, 0.32f, rules.Seed * 0.013f));
                    float worn = rules.Worn(p);
                    float density = patch * Mathf.Clamp01(1f - worn * 1.6f) * Mathf.Lerp(1f, 0.7f, lawn);
                    if (rng.NextDouble() > density)
                        continue;
                    float clearance = Clearance(p);
                    bool hug = clearance < 0.55f;
                    float tall = Noise(p.x, p.z, 0.5f, 40f + rules.Seed * 0.01f);
                    int kind;
                    float scale;
                    if (lawn > 0.5f || worn > 0.3f)
                    {
                        kind = tufts[0];
                        scale = 0.72f + (float)rng.NextDouble() * 0.22f;
                    }
                    else if (hug || tall > 0.62f)
                    {
                        double r = rng.NextDouble();
                        kind = r < 0.28 ? wisps[1 + rng.Next(2)] : r < 0.68 ? tufts[2] : tufts[1];
                        scale = 0.95f + (float)rng.NextDouble() * 0.35f;
                    }
                    else
                    {
                        double r = rng.NextDouble();
                        kind = r < 0.34 ? tufts[1] : r < 0.6 ? tufts[0] : r < 0.76 ? tufts[2] : r < 0.88 ? wisps[0] : r < 0.96 ? wisps[1] : wisps[2];
                        scale = 0.85f + (float)rng.NextDouble() * 0.35f;
                    }
                    Place(kind, p, scale, rng.NextDouble(), 0.04f);
                }
            }

            // Flower patches: one species each, a soft cluster, kept out of the lawn.
            string PickSpecies()
            {
                double r = rng.NextDouble() * FlowerSpecies.Sum(f => f.weight);
                foreach (var (id, weight) in FlowerSpecies)
                {
                    if ((r -= weight) <= 0)
                        return id;
                }
                return FlowerSpecies[0].id;
            }
            for (int attempt = 0; attempt < 900 && flowerCenters.Count < rules.FlowerPatches; attempt++)
            {
                // The first patches frame the lawn (they are what the gameplay camera sees), the rest fill the chunk.
                bool rim = flowerCenters.Count < rules.RimPatches && attempt < 300;
                Vector3 center;
                if (rim)
                {
                    float a = (float)(rng.NextDouble() * Math.PI * 2.0);
                    float d = rules.LawnRadius * (0.82f + (float)rng.NextDouble() * 0.38f);
                    center = new Vector3(rules.Lawn.x + Mathf.Cos(a) * d, 0f, rules.Lawn.z + Mathf.Sin(a) * d);
                }
                else
                {
                    center = new Vector3((float)(rng.NextDouble() * 12.6 - 6.3), 0f, (float)(rng.NextDouble() * 12.6 - 6.3));
                }
                if (!rules.Inside(center) || rules.Worn(center) > 0.2f || Clearance(center) < 0.25f
                    || Vector3.Distance(center, new Vector3(rules.Clear.x, 0f, rules.Clear.z)) < rules.ClearRadius + 0.9f
                    || (!rim && Vector3.Distance(new Vector3(center.x, 0f, center.z), new Vector3(rules.Lawn.x, 0f, rules.Lawn.z)) < rules.LawnRadius * 0.8f)
                    || flowerCenters.Any(c => Vector3.Distance(c, center) < (rim ? 1.4f : 1.1f)))
                    continue;
                string species = PickSpecies();
                int kind = Kind(species);
                int count = 7 + rng.Next(9);
                int placed = 0;
                for (int i = 0; i < count * 2 && placed < count; i++)
                {
                    float a = (float)(rng.NextDouble() * Math.PI * 2.0);
                    float d = (float)Math.Sqrt(rng.NextDouble()) * 0.5f;
                    var p = center + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                    if (Place(kind, p, 1.2f + (float)rng.NextDouble() * 0.35f, rng.NextDouble(), 0.06f))
                        placed++;
                }
                if (placed == 0)
                    continue;
                flowerCenters.Add(center);
                // Fallen petals at the foot of white and pink patches.
                string petals = species == "flower.daisy_white" ? "detail.petals_white" : species == "flower.clover_pink" ? "detail.petals_pink" : null;
                if (petals != null)
                {
                    for (int i = 0; i < 3; i++)
                        Place(Kind(petals), center + new Vector3((float)(rng.NextDouble() - 0.5) * 0.9f, 0f, (float)(rng.NextDouble() - 0.5) * 0.9f), 1f, rng.NextDouble());
                }
            }
            for (int i = 0, tries = 0; i < rules.FlowerSingles && tries < 400; tries++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 13.0 - 6.5), 0f, (float)(rng.NextDouble() * 13.0 - 6.5));
                if (Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(rules.Lawn.x, 0f, rules.Lawn.z)) < rules.LawnRadius * 0.7f || rules.Worn(p) > 0.2f)
                    continue;
                if (Place(Kind(PickSpecies()), p, 1.15f + (float)rng.NextDouble() * 0.3f, rng.NextDouble(), 0.08f))
                    i++;
            }

            // Details.
            string[] pebbles = { "detail.pebble_a", "detail.pebble_b", "detail.pebble_c" };
            foreach (var prop in props)
            {
                var c = prop.Position;
                float r = PropRadius(prop.Prop) * prop.Scale;
                if (prop.Prop.StartsWith("GL_Rock", StringComparison.Ordinal))
                {
                    int n = 3 + rng.Next(4);
                    for (int i = 0; i < n; i++)
                    {
                        float a = (float)(rng.NextDouble() * Math.PI * 2.0);
                        float d = r + 0.08f + (float)rng.NextDouble() * 0.5f;
                        Place(Kind(pebbles[rng.Next(3)]), c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), 0.8f + (float)rng.NextDouble() * 0.6f, rng.NextDouble());
                    }
                }
                else if (prop.Prop.StartsWith("GL_Tree", StringComparison.Ordinal))
                {
                    int leaves = 10 + rng.Next(7);
                    for (int i = 0; i < leaves; i++)
                    {
                        float a = (float)(rng.NextDouble() * Math.PI * 2.0);
                        float d = 0.35f + (float)rng.NextDouble() * 1.1f * prop.Scale;
                        Place(Kind("detail.leaf"), c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), 0.8f + (float)rng.NextDouble() * 0.5f, rng.NextDouble());
                    }
                    // Mushrooms on the shady (+Z) side of the trunk.
                    if (rng.NextDouble() < 0.7)
                        Place(Kind("detail.mushroom"), c + new Vector3((float)(rng.NextDouble() - 0.5) * 0.6f, 0f, 0.5f + (float)rng.NextDouble() * 0.3f), 0.9f + (float)rng.NextDouble() * 0.4f, rng.NextDouble());
                }
                else if (prop.Prop == "GL_Stump" || prop.Prop == "GL_Log")
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float a = (float)(rng.NextDouble() * Math.PI * 2.0);
                        Place(Kind("detail.mushroom"), c + new Vector3(Mathf.Cos(a) * (r + 0.1f), 0f, Mathf.Sin(a) * (r + 0.1f)), 0.8f + (float)rng.NextDouble() * 0.5f, rng.NextDouble());
                    }
                }
            }
            for (int i = 0, tries = 0; i < 60 && tries < 900; tries++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 13.0 - 6.5), 0f, (float)(rng.NextDouble() * 13.0 - 6.5));
                float worn = rules.Worn(p);
                // Pebbles gather along the path edges, a few in the grass.
                if (worn < 0.12f && rng.NextDouble() > 0.25)
                    continue;
                if (Place(Kind(pebbles[rng.Next(3)]), p, 0.7f + (float)rng.NextDouble() * 0.6f, rng.NextDouble()))
                    i++;
            }
            for (int i = 0, tries = 0; i < 45 && tries < 600; tries++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 12.6 - 6.3), 0f, (float)(rng.NextDouble() * 12.6 - 6.3));
                if (rules.Worn(p) > 0.2f || Noise(p.x, p.z, 0.6f, 7f) < 0.5f)
                    continue;
                if (Place(Kind("detail.clover"), p, 0.9f + (float)rng.NextDouble() * 0.5f, rng.NextDouble(), 0.05f))
                    i++;
            }
            return points;
        }

        private static ScatterData SaveScatter(string path, ScatterKit kit, List<ScatterPoint> points)
        {
            var data = AssetDatabase.LoadAssetAtPath<ScatterData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ScatterData>();
                AssetDatabase.CreateAsset(data, path);
            }
            var ordered = points.OrderBy(p => p.Kind).ThenBy(p => p.Rank).ToList();
            var groups = new List<ScatterData.Group>();
            for (int i = 0; i < ordered.Count;)
            {
                int kind = ordered[i].Kind;
                int start = i;
                while (i < ordered.Count && ordered[i].Kind == kind)
                    i++;
                groups.Add(new ScatterData.Group { kind = kind, start = start, count = i - start });
            }
            data.kit = kit;
            data.groups = groups.ToArray();
            data.placements = ordered.Select(p => new Vector4(p.Position.x, p.Position.y, p.Position.z, p.Yaw)).ToArray();
            data.scales = ordered.Select(p => p.Scale).ToArray();
            var bounds = new Bounds(ordered.Count > 0 ? ordered[0].Position : Vector3.zero, Vector3.zero);
            foreach (var p in ordered)
            {
                bounds.Encapsulate(p.Position);
                bounds.Encapsulate(p.Position + Vector3.up * 0.7f * p.Scale);
            }
            // A little room for blade widths and rounding (the renderer adds the wind sway on top).
            bounds.Expand(0.1f);
            data.bounds = bounds;
            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>A point above the ground mesh: life anchors sit on the grass (birds) or at a flight height.</summary>
        private static Vector3 OnGround(Mesh ground, float x, float height, float z)
        {
            float y = new GroundSampler(ground).TrySample(x, z, out float h, out _) ? h : 0f;
            return new Vector3(x, y + height, z);
        }

        /// <summary>Height and normal of a ground mesh under a point (chunk space), brute force over a coarse grid.</summary>
        private sealed class GroundSampler
        {
            private readonly Vector3[] _vertices;
            private readonly int[] _triangles;
            private readonly List<int>[,] _cells;
            private const float CellSize = 1f;
            private const int Cells = 16;

            public GroundSampler(Mesh mesh)
            {
                _vertices = mesh.vertices;
                _triangles = mesh.triangles;
                _cells = new List<int>[Cells, Cells];
                for (int t = 0; t < _triangles.Length; t += 3)
                {
                    var a = _vertices[_triangles[t]];
                    var b = _vertices[_triangles[t + 1]];
                    var c = _vertices[_triangles[t + 2]];
                    if (Vector3.Cross(b - a, c - a).y <= 0f)
                        continue; // walls and the underside never carry scatter
                    int x0 = Cell(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Cell(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
                    int z0 = Cell(Mathf.Min(a.z, Mathf.Min(b.z, c.z))), z1 = Cell(Mathf.Max(a.z, Mathf.Max(b.z, c.z)));
                    for (int x = x0; x <= x1; x++)
                        for (int z = z0; z <= z1; z++)
                            (_cells[x, z] ??= new List<int>()).Add(t);
                }
            }

            private static int Cell(float v) => Mathf.Clamp(Mathf.FloorToInt((v + Cells * CellSize * 0.5f) / CellSize), 0, Cells - 1);

            public bool TrySample(float x, float z, out float height, out Vector3 normal)
            {
                height = 0f;
                normal = Vector3.up;
                var list = _cells[Cell(x), Cell(z)];
                if (list == null)
                    return false;
                foreach (int t in list)
                {
                    var a = _vertices[_triangles[t]];
                    var b = _vertices[_triangles[t + 1]];
                    var c = _vertices[_triangles[t + 2]];
                    float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(d) < 1e-8f)
                        continue;
                    float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                    float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                    float w = 1f - u - v;
                    if (u < -1e-4f || v < -1e-4f || w < -1e-4f)
                        continue;
                    height = u * a.y + v * b.y + w * c.y;
                    normal = Vector3.Cross(b - a, c - a).normalized;
                    if (normal.y < 0f)
                        normal = -normal;
                    return true;
                }
                return false;
            }
        }
    }
}
