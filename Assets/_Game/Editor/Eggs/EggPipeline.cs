using System;
using System.IO;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Vfx;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Eggs
{
    /// <summary>
    /// T14 egg pipeline: MAT_Egg_Master, PF_Egg_Base, DEF_HatchPresentation and the biome egg definitions (Grassland; T51 Forest).
    /// The VFX atlas, the particle material and every hatch effect are built by VfxPipeline (T34A). Idempotent: safe to run again after
    /// re-exporting the egg from BL_Egg_Family.blend.
    /// </summary>
    public static class EggPipeline
    {
        public const string EggShaderName = "PocketWilds/EggMaster";
        public const string VfxShaderName = VfxPipeline.ShaderName;
        public const string EggMaterialPath = "Assets/_Game/Art/Materials/MAT_Egg_Master.mat";
        public const string VfxMaterialPath = VfxPipeline.MaterialPath;
        public const string VfxAtlasPath = VfxPipeline.AtlasPath;
        public const string VfxFolder = VfxPipeline.Folder;
        public const string EggPrefabPath = "Assets/_Game/Prefabs/Eggs/PF_Egg_Base.prefab";
        public const string HatchConfigPath = "Assets/_Game/Content/Visuals/DEF_HatchPresentation.asset";
        public const string EggDefinitionsFolder = "Assets/_Game/Content/Eggs";
        public const string EggMaskPath = "Assets/_Game/Art/Eggs/Textures/T_EggMask_Common.png";
        public const string EggBaseModelPath = "Assets/_Game/Art/Eggs/Models/SM_Egg_Base.fbx";
        public const string EggFragmentsModelPath = "Assets/_Game/Art/Eggs/Models/SM_Egg_Fragments.fbx";

        /// <summary>Egg LOD switch points (screen height). The egg is ~6% of the screen at gameplay distance.</summary>
        public static readonly float[] LodTransitions = { 0.035f, 0.008f };

        [MenuItem("Pocket Wilds/Eggs/Build Egg Family (T14)", priority = 160)]
        public static void BuildAll()
        {
            var eggMaterial = EnsureEggMaterial();
            var egg = BuildEggPrefab(eggMaterial);
            EnsureHatchConfig(egg);
            VfxPipeline.BuildAll();
            EnsureEggDefinitions();
            AssetDatabase.SaveAssets();
            Debug.Log("[EggPipeline] Egg material, prefab, VFX, hatch config and biome egg definitions up to date.");
        }

        // ---------------- Materials ----------------

        public static Material EnsureEggMaterial()
        {
            var shader = Shader.Find(EggShaderName) ?? throw new InvalidOperationException("Shader not found: " + EggShaderName);
            var material = LoadOrCreateMaterial(EggMaterialPath, shader, "MAT_Egg_Master");
            material.SetTexture("_EggMask", AssetDatabase.LoadAssetAtPath<Texture2D>(EggMaskPath)
                                            ?? throw new InvalidOperationException("Missing " + EggMaskPath));
            material.enableInstancing = false; // SRP Batcher handles it; RSUV carries the palette.
            EditorUtility.SetDirty(material);
            return material;
        }

        public static Material EnsureVfxMaterial() => VfxPipeline.EnsureMaterial();

        private static Material LoadOrCreateMaterial(string path, Shader shader, string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            return material;
        }

        /// <summary>The shared VFX atlas (8 tiles since T34A), also read by the blob shadow.</summary>
        public static void EnsureVfxAtlas() => VfxPipeline.EnsureAtlas();

        // ---------------- Egg prefab ----------------

        public static EggView BuildEggPrefab(Material material)
        {
            var baseMeshes = AssetDatabase.LoadAllAssetsAtPath(EggBaseModelPath).OfType<Mesh>().ToDictionary(m => m.name);
            var fragmentMeshes = AssetDatabase.LoadAllAssetsAtPath(EggFragmentsModelPath).OfType<Mesh>().ToDictionary(m => m.name);
            if (baseMeshes.Count < 2 || fragmentMeshes.Count < 3)
                throw new InvalidOperationException("Egg models are not imported. Export them from BL_Egg_Family.blend first.");

            var root = new GameObject("PF_Egg_Base");
            try
            {
                var view = root.AddComponent<EggView>();
                var lodGroup = root.AddComponent<LODGroup>();

                var shell = new GameObject("Shell").transform;
                shell.SetParent(root.transform, false);
                var lod0 = AddRenderer(shell, baseMeshes["SM_Egg_Base_LOD0"], material, ShadowCastingMode.On);
                var lod1 = AddRenderer(shell, baseMeshes["SM_Egg_Base_LOD1"], material, ShadowCastingMode.Off);

                var fragmentsRoot = new GameObject("Fragments").transform;
                fragmentsRoot.SetParent(root.transform, false);
                var pivots = new Transform[EggView.FragmentCount];
                var fragmentRenderers = new Renderer[EggView.FragmentCount];
                string[] names = { "SM_Egg_Fragment_A", "SM_Egg_Fragment_B", "SM_Egg_Fragment_C" };
                for (int i = 0; i < names.Length; i++)
                {
                    var mesh = fragmentMeshes[names[i]];
                    // Pivot at the fragment center so it spins around itself, not around the egg base.
                    var pivot = new GameObject("Pivot_" + names[i].Substring(names[i].Length - 1)).transform;
                    pivot.SetParent(fragmentsRoot, false);
                    pivot.localPosition = mesh.bounds.center;
                    var renderer = AddRenderer(pivot, mesh, material, ShadowCastingMode.Off);
                    renderer.transform.localPosition = -mesh.bounds.center;
                    renderer.enabled = false;
                    pivots[i] = pivot;
                    fragmentRenderers[i] = renderer;
                }

                var vfxPoint = new GameObject("VfxPoint").transform;
                vfxPoint.SetParent(root.transform, false);
                vfxPoint.localPosition = new Vector3(0f, baseMeshes["SM_Egg_Base_LOD0"].bounds.center.y, 0f);
                var revealPoint = new GameObject("RevealPoint").transform;
                revealPoint.SetParent(root.transform, false);

                lodGroup.SetLODs(new[]
                {
                    new LOD(LodTransitions[0], new Renderer[] { lod0 }),
                    new LOD(LodTransitions[1], new Renderer[] { lod1 }),
                });
                lodGroup.fadeMode = LODFadeMode.None;
                lodGroup.RecalculateBounds();

                var blob = PocketWilds.Editor.Lighting.LightingPipeline.AddBlobShadow(root.transform, new Vector2(0.46f, 0.46f));
                view.SetReferences(shell, new Renderer[] { lod0, lod1 }, pivots, fragmentRenderers, vfxPoint, revealPoint, blob);
                Directory.CreateDirectory(Path.GetDirectoryName(EggPrefabPath));
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, EggPrefabPath);
                return prefab.GetComponent<EggView>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static MeshRenderer AddRenderer(Transform parent, Mesh mesh, Material material, ShadowCastingMode shadows)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
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

        // ---------------- Data ----------------

        private static void EnsureHatchConfig(EggView egg)
        {
            var config = AssetDatabase.LoadAssetAtPath<HatchPresentationConfig>(HatchConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(HatchConfigPath));
                config = ScriptableObject.CreateInstance<HatchPresentationConfig>();
                AssetDatabase.CreateAsset(config, HatchConfigPath);
            }
            config.eggPrefab = egg;
            EditorUtility.SetDirty(config);
        }

        /// <summary>Egg content only (economy, odds, pool): no prefab or VFX rebuild.</summary>
        [MenuItem("Pocket Wilds/Eggs/Update Egg Definitions", priority = 161)]
        public static void UpdateEggDefinitions()
        {
            EnsureEggDefinitions();
            AssetDatabase.SaveAssets();
        }

        private static void EnsureEggDefinitions()
        {
            var basic = EnsureEgg("DEF_Egg_Grassland_Basic", "egg.grassland.basic", EggTier.Basic, egg =>
            {
                egg.shell = Hex("F4EBD9");
                egg.spots = Hex("A8D08D");
                egg.band = Hex("7DB563");
                egg.flowers = Hex("F2A7BA");
                egg.crack = Hex("6B4A3A");
                egg.innerShell = Hex("FFF9EE");
                egg.accent = Hex("FFE9A8");
                egg.spotsStrength = 15;
                egg.bandStrength = 0;
                egg.flowersStrength = 15;
                // Doc 02: first egg 10 Light, Cost(n) = round(10 x 1.18^n), early Basic rarity baseline.
                egg.baseCost = 10.0;
                egg.costGrowth = 1.18;
                egg.rarityWeights = new[] { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 };
                // Vertical Slice: the tutorial egg gives the first rabbit.
                egg.starterSpeciesId = "species.rabbit";
            });
            var focus = EnsureEgg("DEF_Egg_Grassland_Focus", "egg.grassland.focus", EggTier.Focus, egg =>
            {
                egg.shell = Hex("EEF1D8");
                egg.spots = Hex("E3C46A");
                egg.band = Hex("6FAE58");
                egg.flowers = Hex("FFFFFF");
                egg.crack = Hex("5E4634");
                egg.innerShell = Hex("FFF9EE");
                egg.accent = Hex("FFD96A");
                egg.spotsStrength = 8;
                egg.bandStrength = 15;
                egg.flowersStrength = 0;
                // Doc 02 Œufs, section 2.2: 4 x B, the 3 species with the most colors left, a better rarity table, offered
                // from biome mastery 3.
                egg.baseCost = 40.0;
                egg.costGrowth = 1.18;
                egg.rarityWeights = new[] { 50.0, 30.0, 14.0, 4.5, 1.3, 0.2 };
                egg.unlockBiomeMastery = 3;
                egg.focusTargetCount = 3;
            });
            // C-EGG-003 (doc 02 Œufs, section 2.3): free, won as a reward, kept in a stock; a pearl shell with round gold
            // dots (no star motif: owner rule against the four-point star). Its table is the last rule's: Rare or better.
            var discovery = EnsureEgg("DEF_Egg_Discovery", "egg.discovery", EggTier.Discovery, egg =>
            {
                egg.biomeId = "";
                egg.pool.Clear();
                egg.shell = Hex("F6F1E7");
                egg.spots = Hex("E9C46A");
                egg.band = Hex("BFD9EE");
                egg.flowers = Hex("F4D58A");
                egg.crack = Hex("6A5340");
                egg.innerShell = Hex("FFF9EE");
                egg.accent = Hex("FFE7A0");
                egg.spotsStrength = 12;
                egg.bandStrength = 6;
                egg.flowersStrength = 10;
                egg.baseCost = 1.0;
                egg.costGrowth = 1.0;
                egg.rarityWeights = new[] { 0.0, 0.0, 70.0, 22.0, 7.0, 1.0 };
            });

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath)
                           ?? throw new InvalidOperationException("Missing " + AnimalPipeline.ContentDatabasePath);
            foreach (var egg in new[] { basic, focus, discovery })
                if (!database.eggs.Contains(egg))
                    database.eggs.Add(egg);
            foreach (var egg in EnsureForestEggs())
                if (!database.eggs.Contains(egg))
                    database.eggs.Add(egg);
            EditorUtility.SetDirty(database);
        }

        /// <summary>
        /// T51 the Forest eggs (doc 02 Œufs, C-EGG-004 and C-EGG-005), the Grassland rules at the Forest's B = 300: the Basic egg
        /// « Œuf de la Forêt » (300 × 1.18^n, the early Basic table) and the Focus egg « Œuf ciblé de la Forêt » (4 × B = 1 200 ×
        /// 1.18^n, 3 targets, the better table, from Forest mastery 3, sharing the Basic's counter like every biome). Brown and
        /// green palettes on the shared egg mask (no new mask). Written once a Forest species is in the database (an egg never
        /// has an empty pool); the pool grows with the Forest species (Owl and Boar Piglet in T52, the bear cub in T53).
        /// </summary>
        private static EggDefinitionAsset[] EnsureForestEggs()
        {
            if (PoolOf(ForestBiome).Count == 0)
            {
                Debug.Log("[EggPipeline] No Forest species in the database yet: the Forest eggs wait for them.");
                return Array.Empty<EggDefinitionAsset>();
            }
            var basic = EnsureEgg("DEF_Egg_Forest_Basic", "egg.forest.basic", EggTier.Basic, ForestBiome, egg =>
            {
                // Hazel brown shell, moss green spots, a thin bark band, cream little flowers.
                egg.shell = Hex("E3C9A0");
                egg.spots = Hex("7FA35E");
                egg.band = Hex("8A6A48");
                egg.flowers = Hex("F6EAD0");
                egg.crack = Hex("5A3E2C");
                egg.innerShell = Hex("FFF7EA");
                egg.accent = Hex("F2D48A");
                egg.spotsStrength = 15;
                egg.bandStrength = 6;
                egg.flowersStrength = 12;
                egg.baseCost = 300.0;
                egg.costGrowth = 1.18;
                egg.rarityWeights = new[] { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 };
            });
            var focus = EnsureEgg("DEF_Egg_Forest_Focus", "egg.forest.focus", EggTier.Focus, ForestBiome, egg =>
            {
                // Pale fern green shell, a deep forest green band, golden hazel dots.
                egg.shell = Hex("DCE6C4");
                egg.spots = Hex("C9A36A");
                egg.band = Hex("5E8C4E");
                egg.flowers = Hex("FFF8E6");
                egg.crack = Hex("4E3A2A");
                egg.innerShell = Hex("FFF9EE");
                egg.accent = Hex("FFD96A");
                egg.spotsStrength = 8;
                egg.bandStrength = 15;
                egg.flowersStrength = 0;
                egg.baseCost = 1200.0;
                egg.costGrowth = 1.18;
                egg.rarityWeights = new[] { 50.0, 30.0, 14.0, 4.5, 1.3, 0.2 };
                egg.unlockBiomeMastery = 3;
                egg.focusTargetCount = 3;
            });
            return new[] { basic, focus };
        }

        /// <summary>T47: the species definition of the shipped database says companion.</summary>
        private static bool IsCompanion(string speciesId)
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(PocketWilds.Editor.UI.UiPipeline.ContentDatabasePath);
            return database != null && database.species.Exists(s => s != null && s.sourceId == speciesId && s.companion);
        }

        public const string GrasslandBiome = "biome.grassland";
        public const string ForestBiome = "biome.forest";

        public static readonly string[] GrasslandSpecies = { "species.rabbit", "species.squirrel", "species.hedgehog", "species.fieldmouse", "species.guineapig", "species.fawn" };

        /// <summary>T51 the Forest's egg species, T52 the Owl and the Boar Piglet (T53 adds the bear cub).</summary>
        public static readonly string[] ForestSpecies = { "species.fox", "species.badger", "species.raccoon", "species.redpanda", "species.owl", "species.boarpiglet" };

        /// <summary>T51 the species of each biome's eggs (data: a new biome adds its row).</summary>
        public static readonly System.Collections.Generic.Dictionary<string, string[]> BiomeEggSpecies =
            new System.Collections.Generic.Dictionary<string, string[]>
            {
                [GrasslandBiome] = GrasslandSpecies,
                [ForestBiome] = ForestSpecies,
            };

        /// <summary>
        /// The pool of a biome's eggs: its egg species, equal weights (rarity comes from the tier roll, discovery from pity),
        /// without companions (they come from their pack only; the registry refuses them in a pool). T51: a Forest species
        /// joins once its definition is in the shipped database (the Grassland list is written as is).
        /// </summary>
        public static System.Collections.Generic.List<EggDefinitionAsset.PoolEntry> PoolOf(string biomeId)
        {
            var pool = new System.Collections.Generic.List<EggDefinitionAsset.PoolEntry>();
            if (!BiomeEggSpecies.TryGetValue(biomeId, out var species))
                return pool;
            foreach (var id in species)
            {
                if (IsCompanion(id) || (biomeId != GrasslandBiome && !InDatabase(id)))
                    continue;
                pool.Add(new EggDefinitionAsset.PoolEntry { speciesId = id, weight = 1f });
            }
            return pool;
        }

        private static bool InDatabase(string speciesId)
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            return database != null && database.species.Exists(s => s != null && s.sourceId == speciesId);
        }

        private static EggDefinitionAsset EnsureEgg(string assetName, string sourceId, EggTier tier, Action<EggDefinitionAsset> fill) =>
            EnsureEgg(assetName, sourceId, tier, GrasslandBiome, fill);

        private static EggDefinitionAsset EnsureEgg(string assetName, string sourceId, EggTier tier, string biomeId, Action<EggDefinitionAsset> fill)
        {
            string path = $"{EggDefinitionsFolder}/{assetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<EggDefinitionAsset>(path);
            if (asset == null)
            {
                Directory.CreateDirectory(EggDefinitionsFolder);
                asset = ScriptableObject.CreateInstance<EggDefinitionAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.sourceId = sourceId;
            asset.displayKey = sourceId + ".name";
            asset.biomeId = biomeId;
            asset.tier = tier;
            asset.visualKey = "PF_Egg_Base";
            // The biome's pool: integrated species of the biome, equal weights (rarity comes from the tier roll, discovery from pity).
            asset.pool = PoolOf(biomeId);
            asset.starterSpeciesId = "";
            asset.unlockBiomeMastery = 0;
            asset.focusTargetCount = 0;
            fill(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }
}
