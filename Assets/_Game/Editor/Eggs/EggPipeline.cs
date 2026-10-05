using System;
using System.IO;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Editor.Animals;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Eggs
{
    /// <summary>
    /// T14 egg pipeline: MAT_Egg_Master, PF_Egg_Base, the shared VFX atlas and hatch VFX prefabs,
    /// DEF_HatchPresentation and the Grassland egg definitions. Idempotent: safe to run again after
    /// re-exporting the egg from BL_Egg_Family.blend.
    /// </summary>
    public static class EggPipeline
    {
        public const string EggShaderName = "PocketWilds/EggMaster";
        public const string VfxShaderName = "PocketWilds/AdditiveVFXLite";
        public const string EggMaterialPath = "Assets/_Game/Art/Materials/MAT_Egg_Master.mat";
        public const string VfxMaterialPath = "Assets/_Game/Art/Materials/MAT_VFX_AdditiveLite.mat";
        public const string VfxAtlasPath = "Assets/_Game/Art/VFX/Textures/T_VFX_SparkAtlas.png";
        public const string VfxFolder = "Assets/_Game/Art/VFX";
        public const string EggPrefabPath = "Assets/_Game/Prefabs/Eggs/PF_Egg_Base.prefab";
        public const string HatchConfigPath = "Assets/_Game/Content/Visuals/DEF_HatchPresentation.asset";
        public const string EggDefinitionsFolder = "Assets/_Game/Content/Eggs";
        public const string EggMaskPath = "Assets/_Game/Art/Eggs/Textures/T_EggMask_Common.png";
        public const string EggBaseModelPath = "Assets/_Game/Art/Eggs/Models/SM_Egg_Base.fbx";
        public const string EggFragmentsModelPath = "Assets/_Game/Art/Eggs/Models/SM_Egg_Fragments.fbx";

        /// <summary>Egg LOD switch points (screen height). The egg is ~6% of the screen at gameplay distance.</summary>
        public static readonly float[] LodTransitions = { 0.035f, 0.008f };

        private const int AtlasTile = 64;

        [MenuItem("Pocket Wilds/Eggs/Build Egg Family (T14)", priority = 160)]
        public static void BuildAll()
        {
            var eggMaterial = EnsureEggMaterial();
            var vfxMaterial = EnsureVfxMaterial();
            var crack = BuildVfx("VFX_Crack", vfxMaterial, ConfigureCrack);
            var common = BuildVfx("VFX_HatchCommon", vfxMaterial, ConfigureHatchCommon);
            var rare = BuildVfx("VFX_HatchRare", vfxMaterial, ConfigureHatchRare);
            var egg = BuildEggPrefab(eggMaterial);
            EnsureHatchConfig(egg, crack, common, rare);
            EnsureEggDefinitions();
            AssetDatabase.SaveAssets();
            Debug.Log("[EggPipeline] Egg material, prefab, VFX, hatch config and Grassland egg definitions up to date.");
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

        public static Material EnsureVfxMaterial()
        {
            EnsureVfxAtlas();
            var shader = Shader.Find(VfxShaderName) ?? throw new InvalidOperationException("Shader not found: " + VfxShaderName);
            var material = LoadOrCreateMaterial(VfxMaterialPath, shader, "MAT_VFX_AdditiveLite");
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(VfxAtlasPath));
            material.SetFloat("_Intensity", 1.4f);
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

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

        /// <summary>Generated grayscale atlas: tile 0 soft dot, tile 1 four-point star. Source is this method.</summary>
        public static void EnsureVfxAtlas()
        {
            if (File.Exists(VfxAtlasPath))
                return;
            var texture = new Texture2D(AtlasTile * 2, AtlasTile, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[AtlasTile * 2 * AtlasTile];
            for (int y = 0; y < AtlasTile; y++)
            {
                for (int x = 0; x < AtlasTile; x++)
                {
                    float u = (x + 0.5f) / AtlasTile * 2f - 1f;
                    float v = (y + 0.5f) / AtlasTile * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float dot = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                    float core = Mathf.Exp(-r * r * 40f);
                    float rays = Mathf.Exp(-Mathf.Abs(u) * 26f) * Mathf.Exp(-Mathf.Abs(v) * 3.2f)
                                 + Mathf.Exp(-Mathf.Abs(v) * 26f) * Mathf.Exp(-Mathf.Abs(u) * 3.2f);
                    float star = Mathf.Clamp01(core + rays * 0.85f) * Mathf.Clamp01(1.15f - r);
                    pixels[y * AtlasTile * 2 + x] = Gray(dot);
                    pixels[y * AtlasTile * 2 + AtlasTile + x] = Gray(star);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(VfxAtlasPath));
            File.WriteAllBytes(VfxAtlasPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(VfxAtlasPath, ImportAssetOptions.ForceUpdate);
        }

        private static Color32 Gray(float value)
        {
            byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
            return new Color32(b, b, b, 255);
        }

        // ---------------- VFX ----------------

        private static ParticleSystem BuildVfx(string name, Material material, Action<GameObject, Material> configure)
        {
            string path = $"{VfxFolder}/{name}.prefab";
            Directory.CreateDirectory(VfxFolder);
            var root = new GameObject(name);
            try
            {
                configure(root, material);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                return prefab.GetComponent<ParticleSystem>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ParticleSystem AddSystem(GameObject root, string name, Material material, bool isRoot)
        {
            var go = isRoot ? root : new GameObject(name);
            if (!isRoot)
                go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.None;
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingFudge = 0f;
            return ps;
        }

        private static void Tile(ParticleSystem ps, int tile)
        {
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 2;
            sheet.numTilesY = 1;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(tile * 0.5f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f);
            sheet.cycleCount = 1;
        }

        private static void Burst(ParticleSystem ps, int count, float time = 0f)
        {
            var emission = ps.emission;
            emission.enabled = true;
            emission.SetBursts(new[] { new ParticleSystem.Burst(time, (short)count) });
        }

        private static void Fade(ParticleSystem ps, Color start, Color end, float growFrom, float growTo)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.85f, 0.55f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, growFrom, 1f, growTo));
        }

        private static void ConfigureCrack(GameObject root, Material material)
        {
            var puff = AddSystem(root, "Puff", material, true);
            var main = puff.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            main.startColor = new Color(1f, 0.96f, 0.86f, 0.55f);
            main.gravityModifier = -0.05f;
            main.maxParticles = 12;
            var shape = puff.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.08f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Burst(puff, 7);
            Tile(puff, 0);
            Fade(puff, Color.white, new Color(1f, 0.9f, 0.8f), 0.7f, 1.4f);

            var glints = AddSystem(root, "Glints", material, false);
            main = glints.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.34f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.06f);
            main.startColor = new Color(1f, 0.93f, 0.7f, 0.9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            main.maxParticles = 6;
            shape = glints.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            Burst(glints, 4);
            Tile(glints, 1);
            Fade(glints, Color.white, new Color(1f, 0.85f, 0.6f), 1f, 0.3f);
        }

        private static void ConfigureHatchCommon(GameObject root, Material material)
        {
            var flash = AddSystem(root, "Flash", material, true);
            var main = flash.main;
            main.startLifetime = 0.26f;
            main.startSpeed = 0f;
            main.startSize = 1.0f;
            main.startColor = new Color(1f, 0.95f, 0.82f, 0.9f);
            main.maxParticles = 1;
            var shape = flash.shape;
            shape.enabled = false;
            Burst(flash, 1);
            Tile(flash, 0);
            Fade(flash, Color.white, new Color(1f, 0.9f, 0.75f), 0.55f, 1.35f);

            var sparkles = AddSystem(root, "Sparkles", material, false);
            main = sparkles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.62f, 1f), new Color(1f, 0.78f, 0.86f, 1f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            main.gravityModifier = 0.18f;
            main.maxParticles = 20;
            shape = sparkles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            Burst(sparkles, 16);
            Tile(sparkles, 1);
            Fade(sparkles, Color.white, new Color(1f, 0.85f, 0.9f), 1f, 0.25f);

            var motes = AddSystem(root, "Motes", material, false);
            main = motes.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.05f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.075f);
            main.startColor = new Color(0.86f, 1f, 0.78f, 0.7f);
            main.gravityModifier = -0.08f;
            main.maxParticles = 14;
            shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.18f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Burst(motes, 12);
            Tile(motes, 0);
            Fade(motes, Color.white, new Color(1f, 1f, 0.85f), 0.8f, 0.4f);
        }

        private static void ConfigureHatchRare(GameObject root, Material material)
        {
            var glow = AddSystem(root, "GoldGlow", material, true);
            var main = glow.main;
            main.startLifetime = 0.45f;
            main.startSpeed = 0f;
            main.startSize = 1.35f;
            main.startColor = new Color(1f, 0.82f, 0.38f, 0.65f);
            main.maxParticles = 1;
            var shape = glow.shape;
            shape.enabled = false;
            Burst(glow, 1);
            Tile(glow, 0);
            Fade(glow, Color.white, new Color(1f, 0.8f, 0.5f), 0.4f, 1.3f);

            var ring = AddSystem(root, "StarRing", material, false);
            main = ring.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
            main.startColor = new Color(1f, 0.86f, 0.45f, 1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            main.maxParticles = 20;
            shape = ring.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Burst(ring, 18);
            Tile(ring, 1);
            Fade(ring, Color.white, new Color(1f, 0.75f, 0.4f), 1f, 0.35f);

            var rising = AddSystem(root, "Rising", material, false);
            main = rising.main;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
            main.startColor = new Color(1f, 0.92f, 0.6f, 0.9f);
            main.gravityModifier = -0.12f;
            main.maxParticles = 12;
            shape = rising.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var emission = rising.emission;
            emission.enabled = true;
            emission.rateOverTime = 16f;
            Tile(rising, 1);
            Fade(rising, Color.white, new Color(1f, 0.85f, 0.55f), 1f, 0.2f);
        }

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

        private static void EnsureHatchConfig(EggView egg, ParticleSystem crack, ParticleSystem common, ParticleSystem rare)
        {
            var config = AssetDatabase.LoadAssetAtPath<HatchPresentationConfig>(HatchConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(HatchConfigPath));
                config = ScriptableObject.CreateInstance<HatchPresentationConfig>();
                AssetDatabase.CreateAsset(config, HatchConfigPath);
            }
            config.eggPrefab = egg;
            config.crackVfx = crack;
            config.hatchCommonVfx = common;
            config.hatchRareVfx = rare;
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
                // Doc 02: Focus costs 3 to 5x a Basic for a smaller pool. Same tier odds until a rule says otherwise.
                egg.baseCost = 40.0;
                egg.costGrowth = 1.18;
                egg.rarityWeights = new[] { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 };
            });

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath)
                           ?? throw new InvalidOperationException("Missing " + AnimalPipeline.ContentDatabasePath);
            foreach (var egg in new[] { basic, focus })
                if (!database.eggs.Contains(egg))
                    database.eggs.Add(egg);
            EditorUtility.SetDirty(database);
        }

        private static EggDefinitionAsset EnsureEgg(string assetName, string sourceId, EggTier tier, Action<EggDefinitionAsset> fill)
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
            asset.biomeId = "biome.grassland";
            asset.tier = tier;
            asset.visualKey = "PF_Egg_Base";
            // Grassland pool: species of the biome. Squirrel and Hedgehog join when integrated (T23, T24).
            asset.pool = new System.Collections.Generic.List<EggDefinitionAsset.PoolEntry>
            {
                new EggDefinitionAsset.PoolEntry { speciesId = "species.rabbit", weight = 1f },
            };
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
