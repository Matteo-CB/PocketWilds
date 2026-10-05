using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketWilds.Editor.Animals
{
    /// <summary>
    /// Unity side of the animal pipeline: shared material, family animator controller, species prefab,
    /// definitions and visual catalog. Idempotent: rebuilding keeps asset GUIDs.
    /// </summary>
    public static class AnimalPipeline
    {
        public const string ShaderName = "PocketWilds/AnimalMaster";
        public const string MaterialPath = "Assets/_Game/Art/Materials/MAT_Animal_Master.mat";
        public const string ContentDatabasePath = "Assets/_Game/Content/DEF_ContentDatabase.asset";
        public const string VisualCatalogPath = "Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset";

        public static string ModelPath(string species) => $"Assets/_Game/Art/Animals/{species}/Models/SKM_{species}.fbx";
        public static string PrefabPath(string species) => $"Assets/_Game/Prefabs/Animals/PF_Animal_{species}.prefab";
        public static string DefinitionPath(string species) => $"Assets/_Game/Content/Species/DEF_Species_{species}.asset";
        public static string ClipsPath(string family) => $"Assets/_Game/Art/Animations/{family}/AN_{family}.fbx";
        public static string ControllerPath(string family) => $"Assets/_Game/Art/Animations/{family}/AC_{family}.controller";

        /// <summary>LOD switch points as screen-relative height. Tuned on device in T22.</summary>
        public static readonly float[] LodTransitions = { 0.07f, 0.035f, 0.008f };

        /// <summary>Animator state name -> clip suffix. One-shots return to Idle by exit time.</summary>
        private static readonly (string State, string Clip, bool ReturnsToIdle, string Next)[] FamilyStates =
        {
            ("Idle", "Idle_A", false, null),
            ("IdleLook", "Idle_B", true, null),
            ("Move", "Locomotion_Hop", false, null),
            ("Happy", "Happy", true, null),
            ("React", "ReactTouch", true, null),
            ("SleepEnter", "Sleep_Enter", false, "Sleep"),
            ("Sleep", "Sleep_Loop", false, null),
            ("Wake", "Sleep_Exit", true, null),
            ("Eat", "Eat_Loop", false, null),
            ("Social", "Social_Nose", true, null),
            ("Inspect", "Inspect", true, null),
        };

        [MenuItem("Pocket Wilds/Animals/Build Rabbit (T10)", priority = 150)]
        public static void BuildRabbit()
        {
            BuildSpecies("Rabbit", "SmallQuadruped", EnsureRabbitDefinition);
        }

        public static void BuildRabbitBatch()
        {
            try
            {
                BuildRabbit();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Pocket Wilds/Animals/Build Squirrel (T23)", priority = 151)]
        public static void BuildSquirrel()
        {
            BuildSpecies("Squirrel", "SmallQuadruped", EnsureSquirrelDefinition);
        }

        public static void BuildSpecies(string species, string family, Func<SpeciesDefinitionAsset> ensureDefinition)
        {
            var material = EnsureAnimalMaterial();
            var controller = EnsureFamilyController(family);
            var prefab = BuildPrefab(species, material, controller);
            var definition = ensureDefinition();
            EnsureContentDatabase(definition);
            EnsureVisualCatalog(definition.visualKey, prefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimalPipeline] {species}: prefab, controller, material, definition and catalog up to date.");
        }

        // ---------------- Material ----------------

        public static Material EnsureAnimalMaterial()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
                throw new InvalidOperationException("Shader not found: " + ShaderName);

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
                material = new Material(shader) { name = "MAT_Animal_Master" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.enableInstancing = false; // Skinned meshes cannot instance; SRP Batcher does the work.
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------------- Animator ----------------

        public static AnimatorController EnsureFamilyController(string family)
        {
            string clipsPath = ClipsPath(family);
            var clips = AssetDatabase.LoadAllAssetsAtPath(clipsPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToDictionary(c => c.name, c => c);
            if (clips.Count == 0)
                throw new InvalidOperationException("No clips imported from " + clipsPath);

            string path = ControllerPath(family);
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            // Every species of the family shares this controller: rebuild only when it drifted from the
            // family table, so adding a species does not rewrite it.
            if (existing != null && IsUpToDate(existing, family, clips))
                return existing;
            var controller = existing ?? AnimatorController.CreateAnimatorControllerAtPath(path);

            var layer = controller.layers[0];
            var machine = layer.stateMachine;
            foreach (var child in machine.states.ToArray())
                machine.RemoveState(child.state);
            foreach (var t in machine.anyStateTransitions.ToArray())
                machine.RemoveAnyStateTransition(t);
            foreach (var p in controller.parameters.ToArray())
                controller.RemoveParameter(p);

            var states = new Dictionary<string, AnimatorState>();
            int i = 0;
            foreach (var (stateName, clipSuffix, _, _) in FamilyStates)
            {
                string clipName = $"AN_{family}_{clipSuffix}";
                if (!clips.TryGetValue(clipName, out var clip))
                    throw new InvalidOperationException("Missing clip " + clipName);
                var state = machine.AddState(stateName, new Vector3(300, 60 * i++, 0));
                state.motion = clip;
                state.writeDefaultValues = true;
                states[stateName] = state;
            }
            machine.defaultState = states["Idle"];

            foreach (var (stateName, _, returnsToIdle, next) in FamilyStates)
            {
                string target = next ?? (returnsToIdle ? "Idle" : null);
                if (target == null)
                    continue;
                var transition = states[stateName].AddTransition(states[target]);
                transition.hasExitTime = true;
                transition.exitTime = 0.92f;
                transition.hasFixedDuration = true;
                transition.duration = 0.12f;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static bool IsUpToDate(AnimatorController controller, string family, Dictionary<string, AnimationClip> clips)
        {
            if (controller.layers.Length == 0 || controller.parameters.Length != 0)
                return false;
            var machine = controller.layers[0].stateMachine;
            var states = machine.states.ToDictionary(s => s.state.name, s => s.state);
            if (states.Count != FamilyStates.Length || machine.defaultState == null || machine.defaultState.name != "Idle")
                return false;
            foreach (var (stateName, clipSuffix, returnsToIdle, next) in FamilyStates)
            {
                if (!states.TryGetValue(stateName, out var state))
                    return false;
                if (!clips.TryGetValue($"AN_{family}_{clipSuffix}", out var clip) || state.motion != clip)
                    return false;
                string target = next ?? (returnsToIdle ? "Idle" : null);
                if (target == null ? state.transitions.Length != 0
                        : state.transitions.Length != 1 || state.transitions[0].destinationState == null
                          || state.transitions[0].destinationState.name != target)
                    return false;
            }
            return true;
        }

        // ---------------- Prefab ----------------

        public static AnimalView BuildPrefab(string species, Material material, AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(species));
            if (model == null)
                throw new InvalidOperationException("Model not imported: " + ModelPath(species));

            var root = new GameObject("PF_Animal_" + species);
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "VisualRoot";

                var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullCompletely;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.keepAnimatorStateOnDisable = true;

                var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .OrderBy(r => r.name, StringComparer.Ordinal).ToArray();
                if (renderers.Length != 3)
                    throw new InvalidOperationException($"{species}: expected 3 LOD renderers, found {renderers.Length}.");
                for (int lod = 0; lod < renderers.Length; lod++)
                {
                    var r = renderers[lod];
                    r.sharedMaterials = new[] { material };
                    r.shadowCastingMode = lod < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    r.receiveShadows = true;
                    r.lightProbeUsage = LightProbeUsage.Off;
                    r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    r.skinnedMotionVectors = false;
                    r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    r.updateWhenOffscreen = false;
                    r.quality = SkinQuality.Auto;
                }

                var lodGroup = visual.GetComponent<LODGroup>() ?? visual.AddComponent<LODGroup>();
                var lods = new LOD[renderers.Length];
                for (int lod = 0; lod < renderers.Length; lod++)
                    lods[lod] = new LOD(LodTransitions[lod], new Renderer[] { renderers[lod] });
                lodGroup.SetLODs(lods);
                lodGroup.fadeMode = LODFadeMode.None;
                lodGroup.animateCrossFading = false;
                lodGroup.RecalculateBounds();

                // Low quality has no shadow map: a soft disc sized to the LOD0 footprint grounds the animal.
                var footprint = renderers[0].bounds.size;
                var blob = PocketWilds.Editor.Lighting.LightingPipeline.AddBlobShadow(root.transform, new Vector2(footprint.x * 1.35f, footprint.z * 1.2f));

                var view = root.AddComponent<AnimalView>();
                view.SetReferences(renderers.Cast<Renderer>().ToArray(), animator, lodGroup,
                    FindDeep(visual.transform, "SOCK_HeadTop"), FindDeep(visual.transform, "SOCK_Neck"),
                    FindDeep(visual.transform, "SOCK_Back"), FindDeep(visual.transform, "SOCK_TailBase"), blob);

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath(species)));
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(species), out bool success);
                if (!success)
                    throw new InvalidOperationException("Could not save " + PrefabPath(species));
                return saved.GetComponent<AnimalView>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static Transform FindDeep(Transform parent, string name)
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

        // ---------------- Data ----------------

        private static SpeciesDefinitionAsset EnsureRabbitDefinition()
        {
            var asset = LoadOrCreate<SpeciesDefinitionAsset>(DefinitionPath("Rabbit"));
            asset.sourceId = "species.rabbit";
            asset.displayKey = "species.rabbit.name";
            asset.rigFamily = RigFamily.SmallQuadruped;
            asset.biomeId = "biome.grassland";
            asset.baseProduction = 1.0;
            asset.visualKey = "PF_Animal_Rabbit";

            // Rabbit Production Spec palette baseline. Inner ears use "secondary", nose uses "detail".
            asset.variants = new List<SpeciesDefinitionAsset.VariantEntry>
            {
                Variant("variant.rabbit.brown", RarityTier.Common, Hex("B07A52"), Hex("F2B8B5"), Hex("F3E3CC"), Hex("E98A93"), Hex("1E1414")),
                Variant("variant.rabbit.white", RarityTier.Common, Hex("F6EEE2"), Hex("F4BFC0"), Hex("FFF8EE"), Hex("EE8E98"), Hex("2A1A1C")),
                Variant("variant.rabbit.grey", RarityTier.Uncommon, Hex("A9A6A8"), Hex("EDB9BA"), Hex("E9E4E0"), Hex("E48E96"), Hex("1C1718")),
                Variant("variant.rabbit.black", RarityTier.Uncommon, Hex("3B302E"), Hex("C99A9C"), Hex("6B5B57"), Hex("D98C93"), Hex("0E0A0A")),
                Variant("variant.rabbit.spotted", RarityTier.Rare, Hex("F4EBDD"), Hex("F2BCBC"), Hex("FFF8EE"), Hex("E98A93"), Hex("1E1414"),
                    pattern: Hex("C9A27E"), channel: PatternChannel.MaskA, strength: 15),
                Variant("variant.rabbit.sakura", RarityTier.Epic, Hex("F6C9D4"), Hex("FBE3E8"), Hex("FFF1F4"), Hex("E77D95"), Hex("2B1820")),
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static SpeciesDefinitionAsset EnsureSquirrelDefinition()
        {
            var asset = LoadOrCreate<SpeciesDefinitionAsset>(DefinitionPath("Squirrel"));
            asset.sourceId = "species.squirrel";
            asset.displayKey = "species.squirrel.name";
            asset.rigFamily = RigFamily.SmallQuadruped;
            asset.biomeId = "biome.grassland";
            // Same base as Rabbit: inside a biome, rarity and mastery carry the production curve.
            asset.baseProduction = 1.0;
            asset.visualKey = "PF_Animal_Squirrel";

            // Squirrel regions (pw_masks.squirrel_masks): secondary = tail, belly = chest, chin and muzzle,
            // detail = nose, mask A = tail tip and ear tufts. Autumn = warm orange/red palette (A-SP-002).
            asset.variants = new List<SpeciesDefinitionAsset.VariantEntry>
            {
                Variant("variant.squirrel.red", RarityTier.Common, Hex("C9693A"), Hex("B4552E"), Hex("F4E3CB"), Hex("5E3329"), Hex("1A1210")),
                Variant("variant.squirrel.brown", RarityTier.Common, Hex("8A5F43"), Hex("6E4835"), Hex("EEDDC4"), Hex("4E3128"), Hex("160F0D")),
                Variant("variant.squirrel.grey", RarityTier.Uncommon, Hex("9C9896"), Hex("C4C0BC"), Hex("F2EEE8"), Hex("6B5A58"), Hex("141012")),
                Variant("variant.squirrel.cream", RarityTier.Uncommon, Hex("EAD6B6"), Hex("F6E9D3"), Hex("FFF9EE"), Hex("C48D80"), Hex("2A1C1A")),
                Variant("variant.squirrel.black", RarityTier.Rare, Hex("443833"), Hex("332A27"), Hex("9C8C82"), Hex("5E4A46"), Hex("0B0808")),
                Variant("variant.squirrel.autumn", RarityTier.Epic, Hex("E58A35"), Hex("C8442E"), Hex("FBE5C2"), Hex("8E3A2C"), Hex("1E1210"),
                    pattern: Hex("F4C24C"), channel: PatternChannel.MaskA, strength: 15),
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static SpeciesDefinitionAsset.VariantEntry Variant(string id, RarityTier rarity, Color primary, Color secondary,
            Color belly, Color detail, Color eye, Color? pattern = null, PatternChannel channel = PatternChannel.None, int strength = 0)
        {
            return new SpeciesDefinitionAsset.VariantEntry
            {
                sourceId = id,
                displayKey = id + ".name",
                rarity = rarity,
                primary = primary,
                secondary = secondary,
                belly = belly,
                detail = detail,
                eye = eye,
                pattern = pattern ?? primary,
                patternChannel = channel,
                patternStrength = strength,
            };
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }

        private static void EnsureContentDatabase(SpeciesDefinitionAsset definition)
        {
            var database = LoadOrCreate<ContentDatabaseAsset>(ContentDatabasePath);
            database.species.RemoveAll(s => s == null);
            if (!database.species.Contains(definition))
                database.species.Add(definition);
            EditorUtility.SetDirty(database);

            var builder = database.CreateBuilder();
            if (builder.HasErrors)
                throw new InvalidOperationException("Content errors: " + string.Join("; ", builder.Issues));
        }

        private static void EnsureVisualCatalog(string visualKey, AnimalView prefab)
        {
            var catalog = LoadOrCreate<AnimalVisualCatalog>(VisualCatalogPath);
            catalog.entries.RemoveAll(e => e == null || e.visualKey == visualKey);
            catalog.entries.Add(new AnimalVisualCatalog.Entry { visualKey = visualKey, prefab = prefab, softCapacity = 24, hardCapacity = 48 });
            EditorUtility.SetDirty(catalog);
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
