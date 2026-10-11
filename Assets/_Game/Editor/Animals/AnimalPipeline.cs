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
        public static string OverridePath(string family, string species) => $"Assets/_Game/Art/Animations/{family}/AOC_{family}_{species}.overrideController";

        /// <summary>
        /// Low-bodied species (doc RIG-001 and A-SP-003): the family Walk replaces the Hop and Happy_Bounce
        /// replaces the high Happy. Data only: an override of the family controller, same states.
        /// </summary>
        public static readonly (string Family, string Species)[] LowBodyClips =
        {
            ("Locomotion_Hop", "Locomotion_Walk"),
            ("Happy", "Happy_Bounce"),
        };

        /// <summary>LOD switch points as screen-relative height. Tuned on device in T22.</summary>
        public static readonly float[] LodTransitions = { 0.07f, 0.035f, 0.008f };

        /// <summary>Animator state name -> clip suffix. One-shots return to Idle by exit time.</summary>
        private static readonly (string State, string Clip, bool ReturnsToIdle, string Next)[] FamilyStates =
        {
            ("Idle", "Idle_A", false, null),
            ("IdleLook", "Idle_B", true, null),
            ("Move", "Locomotion_Hop", false, null), // the Move clip is per family: see ClipFor
            ("Happy", "Happy", true, null),
            ("React", "ReactTouch", true, null),
            ("SleepEnter", "Sleep_Enter", false, "Sleep"),
            ("Sleep", "Sleep_Loop", false, null),
            ("Wake", "Sleep_Exit", true, null),
            ("Eat", "Eat_Loop", false, null),
            ("Social", "Social_Nose", true, null),
            ("Inspect", "Inspect", true, null),
        };

        /// <summary>Clip suffix of the Move state: the small family hops, the medium family walks (T46).</summary>
        public static string MoveClip(string family) => family == "MediumQuadruped" ? "Locomotion_Walk" : "Locomotion_Hop";

        private static string ClipFor(string family, string state, string clipSuffix) => state == "Move" ? MoveClip(family) : clipSuffix;

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

        [MenuItem("Pocket Wilds/Animals/Build Hedgehog (T24)", priority = 152)]
        public static void BuildHedgehog()
        {
            // Short legs and a scurry: a little slower than the hopping species. The longer snout comes from the measured
            // body: noses meet a bit farther apart (T33C).
            BuildSpecies("Hedgehog", "SmallQuadruped", EnsureHedgehogDefinition, LowBodyClips, behavior => behavior.moveSpeed = 0.28f);
        }

        [MenuItem("Pocket Wilds/Animals/Build Field Mouse (T45)", priority = 153)]
        public static void BuildFieldMouse()
        {
            // Hops quickly with the default hop clip.
            BuildSpecies("FieldMouse", "SmallQuadruped", EnsureFieldMouseDefinition, null, b => b.moveSpeed = 0.36f);
        }

        [MenuItem("Pocket Wilds/Animals/Build Guinea Pig (T45)", priority = 154)]
        public static void BuildGuineaPig()
        {
            BuildSpecies("GuineaPig", "SmallQuadruped", EnsureGuineaPigDefinition, LowBodyClips, b => b.moveSpeed = 0.26f);
        }

        [MenuItem("Pocket Wilds/Animals/Build Field Mouse and Guinea Pig (T45)", priority = 155)]
        public static void BuildT45()
        {
            BuildFieldMouse();
            BuildGuineaPig();
        }

        [MenuItem("Pocket Wilds/Animals/Build Fawn (T46)", priority = 156)]
        public static void BuildFawn()
        {
            // Fawns greet fawns with their own clip (social group 1); a calm walk.
            BuildSpecies("Fawn", "MediumQuadruped", EnsureFawnDefinition, null, b => { b.moveSpeed = 0.32f; b.socialGroup = 1; });
        }

        public static void BuildSpecies(string species, string family, Func<SpeciesDefinitionAsset> ensureDefinition,
            (string Family, string Species)[] clipOverrides = null, Action<BehaviorProfile> behavior = null)
        {
            var material = EnsureAnimalMaterial();
            var controller = EnsureFamilyController(family);
            RuntimeAnimatorController runtime = controller;
            if (clipOverrides != null && clipOverrides.Length > 0)
                runtime = EnsureSpeciesOverride(family, species, controller, clipOverrides);
            var prefab = BuildPrefab(species, material, runtime);
            var definition = ensureDefinition();
            EnsureContentDatabase(definition);
            EnsureVisualCatalog(definition.visualKey, prefab, behavior);
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
                string clipName = $"AN_{family}_{ClipFor(family, stateName, clipSuffix)}";
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

        /// <summary>Family controller with a few family clips swapped for one species. Never a species-only clip.</summary>
        public static AnimatorOverrideController EnsureSpeciesOverride(string family, string species, AnimatorController controller,
            (string Family, string Species)[] clipOverrides)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(ClipsPath(family)).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToDictionary(c => c.name, c => c);
            string path = OverridePath(family, species);
            var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (aoc == null)
            {
                aoc = new AnimatorOverrideController(controller);
                AssetDatabase.CreateAsset(aoc, path);
            }
            aoc.runtimeAnimatorController = controller;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            aoc.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++)
            {
                AnimationClip replacement = null;
                foreach (var (from, to) in clipOverrides)
                {
                    if (pairs[i].Key.name != $"AN_{family}_{from}")
                        continue;
                    if (!clips.TryGetValue($"AN_{family}_{to}", out replacement))
                        throw new InvalidOperationException($"Missing family clip AN_{family}_{to}");
                }
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, replacement);
            }
            aoc.ApplyOverrides(pairs);
            EditorUtility.SetDirty(aoc);
            return aoc;
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
                if (!clips.TryGetValue($"AN_{family}_{ClipFor(family, stateName, clipSuffix)}", out var clip) || state.motion != clip)
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

        public static AnimalView BuildPrefab(string species, Material material, RuntimeAnimatorController controller)
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
                // T44C visitor variants (doc Rétention, rare visitors): palettes only, secret so they are never in the egg
                // odds, the pities or the genetics (HatchService skips IsSecret; doc 02 Oeufs: « jamais de visiteur »).
                Variant("variant.rabbit.meadow_visitor", RarityTier.Rare, Hex("B9CBA6"), Hex("F4C3D2"), Hex("FBF3DE"), Hex("E98A93"), Hex("2A1E1C"), secret: true, visitor: true),
                Variant("variant.rabbit.caramel", RarityTier.Rare, Hex("D69A55"), Hex("F4C4A8"), Hex("F7EBD0"), Hex("E89A9E"), Hex("2A1E1C"), secret: true, mastery: 10),
                Variant("variant.rabbit.moon", RarityTier.Epic, Hex("E3E6EC"), Hex("F3C9CF"), Hex("FFFFFF"), Hex("E3A0AA"), Hex("1E1820"),
                    pattern: Hex("34416B"), channel: PatternChannel.MaskB, strength: 16, secret: true, mastery: 20),
                Variant("variant.rabbit.peach", RarityTier.Legendary, Hex("F6C3A0"), Hex("F7CDB8"), Hex("FBEFDF"), Hex("E8A49A"), Hex("2A1E1C"),
                    pattern: Hex("EE9E6E"), channel: PatternChannel.MaskA, strength: 14, secret: true, mastery: 30),
                Variant("variant.rabbit.snow_visitor", RarityTier.Rare, Hex("F8FBFF"), Hex("CFE3F5"), Hex("E3EFFA"), Hex("F2B6C2"), Hex("22303F"), secret: true, visitor: true),
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
                Variant("variant.squirrel.copper", RarityTier.Rare, Hex("C46A35"), Hex("B65E2E"), Hex("F3D0AE"), Hex("3A2622"), Hex("1E1414"), secret: true, mastery: 10),
                Variant("variant.squirrel.snow", RarityTier.Epic, Hex("F7F7F5"), Hex("D9DCE0"), Hex("FFFFFF"), Hex("4A3A3A"), Hex("1E1414"),
                    pattern: Hex("C9CED6"), channel: PatternChannel.MaskA, strength: 12, secret: true, mastery: 20),
                Variant("variant.squirrel.goldenhazel", RarityTier.Legendary, Hex("C99653"), Hex("B58542"), Hex("F3E3C3"), Hex("3A2A22"), Hex("1E1414"),
                    pattern: Hex("F2DFB0"), channel: PatternChannel.MaskB, strength: 14, secret: true, mastery: 30),
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static SpeciesDefinitionAsset EnsureHedgehogDefinition()
        {
            var asset = LoadOrCreate<SpeciesDefinitionAsset>(DefinitionPath("Hedgehog"));
            asset.sourceId = "species.hedgehog";
            asset.displayKey = "species.hedgehog.name";
            asset.rigFamily = RigFamily.SmallQuadruped;
            asset.biomeId = "biome.grassland";
            asset.baseProduction = 1.0;
            asset.visualKey = "PF_Animal_Hedgehog";

            // Hedgehog regions (pw_masks.hedgehog_masks): secondary = spine shell, belly = snout and underside,
            // detail = nose, mask A = mushroom spots on every third lobe, mask B = light lobe tips.
            // Mushroom changes the palette only (A-SP-003): red shell, white spots.
            asset.variants = new List<SpeciesDefinitionAsset.VariantEntry>
            {
                Variant("variant.hedgehog.brown", RarityTier.Common, Hex("B48A66"), Hex("6E4B36"), Hex("EEDCC4"), Hex("2A1E1C"), Hex("140E0D"),
                    pattern: Hex("E8D3B4"), channel: PatternChannel.MaskB, strength: 12),
                Variant("variant.hedgehog.cream", RarityTier.Common, Hex("EFE0CA"), Hex("D8C09E"), Hex("FFF6EA"), Hex("4A3532"), Hex("1C1414"),
                    pattern: Hex("FFF4E2"), channel: PatternChannel.MaskB, strength: 10),
                Variant("variant.hedgehog.grey", RarityTier.Uncommon, Hex("CFC6BE"), Hex("7F7873"), Hex("F0EBE6"), Hex("2A2424"), Hex("141012"),
                    pattern: Hex("E6E0DA"), channel: PatternChannel.MaskB, strength: 12),
                Variant("variant.hedgehog.black", RarityTier.Uncommon, Hex("9C8A7E"), Hex("3A312D"), Hex("D4C6BA"), Hex("1A1414"), Hex("0B0808"),
                    pattern: Hex("7A6D66"), channel: PatternChannel.MaskB, strength: 10),
                Variant("variant.hedgehog.white", RarityTier.Rare, Hex("F8F0E8"), Hex("EDE2D6"), Hex("FFFFFF"), Hex("E49AA0"), Hex("2A1A1C")),
                Variant("variant.hedgehog.mushroom", RarityTier.Epic, Hex("EAD6BD"), Hex("D2433A"), Hex("FFF4E6"), Hex("3A2A28"), Hex("1A1212"),
                    pattern: Hex("FFF8F0"), channel: PatternChannel.MaskA, strength: 15),
                Variant("variant.hedgehog.chestnut", RarityTier.Rare, Hex("B6764A"), Hex("7A4A2E"), Hex("F1E2CC"), Hex("2A1E1C"), Hex("140E0D"),
                    pattern: Hex("E2B48A"), channel: PatternChannel.MaskB, strength: 10, secret: true, mastery: 10),
                Variant("variant.hedgehog.lavender", RarityTier.Epic, Hex("BDB3C6"), Hex("9C87B0"), Hex("F6F1F8"), Hex("3A2E3E"), Hex("141012"),
                    pattern: Hex("D9CEE6"), channel: PatternChannel.MaskB, strength: 14, secret: true, mastery: 20),
                Variant("variant.hedgehog.moss", RarityTier.Legendary, Hex("EFE3CC"), Hex("6F8F4E"), Hex("F8F0E2"), Hex("2E2A20"), Hex("140E0D"),
                    pattern: Hex("A9C98A"), channel: PatternChannel.MaskA, strength: 14, secret: true, mastery: 30),
                // T44C visitor variant: warm cream spines tipped with soft blossom pink (light lobe tips, mask B). Secret: never in eggs.
                Variant("variant.hedgehog.flower_visitor", RarityTier.Rare, Hex("F2E3CE"), Hex("EBD9BC"), Hex("FFF6EA"), Hex("4A3532"), Hex("1C1414"),
                    pattern: Hex("F6BFD2"), channel: PatternChannel.MaskB, strength: 14, secret: true, visitor: true),
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static SpeciesDefinitionAsset EnsureFieldMouseDefinition()
        {
            var asset = LoadOrCreate<SpeciesDefinitionAsset>(DefinitionPath("FieldMouse"));
            asset.sourceId = "species.fieldmouse";
            asset.displayKey = "species.fieldmouse.name";
            asset.rigFamily = RigFamily.SmallQuadruped;
            asset.biomeId = "biome.grassland";
            asset.baseProduction = 1.0;
            asset.visualKey = "PF_Animal_FieldMouse";

            // Field mouse regions: secondary = inner ears, tail and paws, belly = chest, underside, chin and muzzle,
            // mask A = big patches, mask B = back stripe.
            // Secret rows are the mastery colors (T46, mastery 10/20/30) and one plain secret color: never in eggs, wishes or genetics.
            asset.variants = new List<SpeciesDefinitionAsset.VariantEntry>
            {
                Variant("variant.fieldmouse.warmbrown", RarityTier.Common, Hex("A8734E"), Hex("F2C2C0"), Hex("F3E6D0"), Hex("E8A0A6"), Hex("1E1414")),
                Variant("variant.fieldmouse.sand", RarityTier.Common, Hex("D9B47E"), Hex("F2C4C0"), Hex("F7EEDC"), Hex("E8A0A6"), Hex("1E1414")),
                Variant("variant.fieldmouse.grey", RarityTier.Uncommon, Hex("9C9799"), Hex("D9B3B5"), Hex("DAD6D4"), Hex("E3A2A8"), Hex("1A1414")),
                Variant("variant.fieldmouse.white", RarityTier.Uncommon, Hex("F7F2EA"), Hex("F4C6C8"), Hex("FFFFFF"), Hex("EE9AA6"), Hex("1E1414")),
                Variant("variant.fieldmouse.black", RarityTier.Rare, Hex("4A4446"), Hex("C9A1A5"), Hex("6B6466"), Hex("D898A0"), Hex("120C0C")),
                Variant("variant.fieldmouse.piebald", RarityTier.Epic, Hex("F7F2EA"), Hex("F2C4C2"), Hex("FFFFFF"), Hex("E8A0A6"), Hex("1E1414"),
                    pattern: Hex("A8734E"), channel: PatternChannel.MaskA, strength: 16),
                Variant("variant.fieldmouse.hazelnut", RarityTier.Rare, Hex("C9935F"), Hex("F5C9B4"), Hex("EEDCC2"), Hex("E8A0A0"), Hex("1E1414"), secret: true, mastery: 10),
                Variant("variant.fieldmouse.lilac", RarityTier.Epic, Hex("B7A9BE"), Hex("E9C3CF"), Hex("FBF8FA"), Hex("E6A2B2"), Hex("1E1414"),
                    pattern: Hex("8E6E9E"), channel: PatternChannel.MaskA, strength: 10, secret: true, mastery: 20),
                Variant("variant.fieldmouse.goldenwheat", RarityTier.Legendary, Hex("DDB46A"), Hex("F4CFB0"), Hex("F6E7C4"), Hex("E8A49A"), Hex("1E1414"),
                    pattern: Hex("8A6236"), channel: PatternChannel.MaskB, strength: 14, secret: true, mastery: 30),
                Variant("variant.fieldmouse.buttercup", RarityTier.Epic, Hex("F2D46B"), Hex("F6C6B4"), Hex("FBF1D6"), Hex("EBA39C"), Hex("1E1414"), secret: true),
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static SpeciesDefinitionAsset EnsureGuineaPigDefinition()
        {
            var asset = LoadOrCreate<SpeciesDefinitionAsset>(DefinitionPath("GuineaPig"));
            asset.sourceId = "species.guineapig";
            asset.displayKey = "species.guineapig.name";
            asset.rigFamily = RigFamily.SmallQuadruped;
            asset.biomeId = "biome.grassland";
            asset.baseProduction = 1.0;
            asset.visualKey = "PF_Animal_GuineaPig";

            // Guinea pig regions: secondary = ears and paws (a darker tone of the coat: pink tiny ears read as a patch), belly = chest, underside, chin and muzzle,
            // mask A = big patches, mask B = face blaze and middle belt.
            // Secret rows are the mastery colors (T46, mastery 10/20/30) and one plain secret color: never in eggs, wishes or genetics.
            asset.variants = new List<SpeciesDefinitionAsset.VariantEntry>
            {
                Variant("variant.guineapig.caramel", RarityTier.Common, Hex("C98A4E"), Hex("A86C3A"), Hex("F4E6CF"), Hex("B07A55"), Hex("1E1414")),
                Variant("variant.guineapig.cream", RarityTier.Common, Hex("F1DFC0"), Hex("D9C29C"), Hex("FFFDF7"), Hex("D6B894"), Hex("1E1414")),
                Variant("variant.guineapig.black", RarityTier.Uncommon, Hex("3E3836"), Hex("6B5A57"), Hex("5E5654"), Hex("6E5A52"), Hex("100A0A")),
                Variant("variant.guineapig.tricolor", RarityTier.Uncommon, Hex("C98A4E"), Hex("A86C3A"), Hex("FFFFFF"), Hex("B07A55"), Hex("1E1414"),
                    pattern: Hex("3E3836"), channel: PatternChannel.MaskA, strength: 16),
                Variant("variant.guineapig.greywhite", RarityTier.Rare, Hex("7D8288"), Hex("62666C"), Hex("FFFFFF"), Hex("D9A9A8"), Hex("1A1414"),
                    pattern: Hex("FBFAF7"), channel: PatternChannel.MaskB, strength: 16),
                Variant("variant.guineapig.golden", RarityTier.Epic, Hex("E3AE52"), Hex("C38E3A"), Hex("F7E3B5"), Hex("B97C45"), Hex("1E1414")),
                Variant("variant.guineapig.cinnamon", RarityTier.Rare, Hex("C07F5A"), Hex("A0623F"), Hex("F5E5CF"), Hex("E59A9E"), Hex("1E1414"), secret: true, mastery: 10),
                Variant("variant.guineapig.marshmallow", RarityTier.Epic, Hex("FBF1EF"), Hex("E9C9CC"), Hex("FFFFFF"), Hex("EEA2AE"), Hex("1E1414"),
                    pattern: Hex("F2BFCB"), channel: PatternChannel.MaskA, strength: 14, secret: true, mastery: 20),
                Variant("variant.guineapig.apricot", RarityTier.Legendary, Hex("EDB07A"), Hex("D08F5A"), Hex("FBEBD8"), Hex("D98C78"), Hex("1E1414"),
                    pattern: Hex("FFFBF5"), channel: PatternChannel.MaskB, strength: 16, secret: true, mastery: 30),
                Variant("variant.guineapig.milkcloud", RarityTier.Epic, Hex("FAF4E8"), Hex("D9C3A6"), Hex("FFFFFF"), Hex("EA9EA8"), Hex("1E1414"),
                    pattern: Hex("C9A27E"), channel: PatternChannel.MaskA, strength: 13, secret: true),
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static SpeciesDefinitionAsset EnsureFawnDefinition()
        {
            var asset = LoadOrCreate<SpeciesDefinitionAsset>(DefinitionPath("Fawn"));
            asset.sourceId = "species.fawn";
            asset.displayKey = "species.fawn.name";
            asset.rigFamily = RigFamily.MediumQuadruped;
            asset.biomeId = "biome.grassland";
            asset.baseProduction = 1.0;
            asset.visualKey = "PF_Animal_Fawn";

            // Fawn regions: secondary = inner ears, belly = chest, underside, inner legs and muzzle, detail = nose and hooves,
            // mask A = round spots, mask B = fern stripes along the back.
            // Secret rows are the mastery colors (mastery 10/20/30) and one plain secret color: never in eggs, wishes or genetics.
            asset.variants = new List<SpeciesDefinitionAsset.VariantEntry>
            {
                Variant("variant.fawn.chestnutspots", RarityTier.Common, Hex("A0663F"), Hex("E9C3B4"), Hex("F3E6D0"), Hex("4A3328"), Hex("1E1414"),
                    pattern: Hex("FFF8EE"), channel: PatternChannel.MaskA, strength: 16),
                Variant("variant.fawn.lightbrown", RarityTier.Common, Hex("C08A5E"), Hex("E9C6B6"), Hex("F4E8D4"), Hex("5A3D2C"), Hex("1E1414")),
                Variant("variant.fawn.cream", RarityTier.Uncommon, Hex("EFDDBE"), Hex("F3C9C4"), Hex("FFFDF8"), Hex("B5875A"), Hex("1E1414")),
                Variant("variant.fawn.darkbrown", RarityTier.Uncommon, Hex("6E4A33"), Hex("D9B9A6"), Hex("EADBC8"), Hex("2E211C"), Hex("120C0C")),
                Variant("variant.fawn.white", RarityTier.Rare, Hex("F8F5F0"), Hex("F3CACB"), Hex("FFFFFF"), Hex("B9B7B6"), Hex("1E1414")),
                Variant("variant.fawn.blossom", RarityTier.Epic, Hex("F4C9CF"), Hex("FBE9EA"), Hex("FFFFFF"), Hex("C98B8F"), Hex("1E1414"),
                    pattern: Hex("F09DB0"), channel: PatternChannel.MaskA, strength: 14),
                Variant("variant.fawn.honey", RarityTier.Rare, Hex("E0AE62"), Hex("F2CDB3"), Hex("F7EBD3"), Hex("8A6145"), Hex("1E1414"), secret: true, mastery: 10),
                Variant("variant.fawn.dew", RarityTier.Epic, Hex("AEB7C4"), Hex("E5C9CC"), Hex("FFFFFF"), Hex("5E6470"), Hex("1E1414"),
                    pattern: Hex("FFFFFF"), channel: PatternChannel.MaskA, strength: 14, secret: true, mastery: 20),
                Variant("variant.fawn.goldenfern", RarityTier.Legendary, Hex("C89A4E"), Hex("EFCBAA"), Hex("F4E5C6"), Hex("6B4A2C"), Hex("1E1414"),
                    pattern: Hex("F2DDA8"), channel: PatternChannel.MaskB, strength: 14, secret: true, mastery: 30),
                Variant("variant.fawn.daisy", RarityTier.Epic, Hex("B38A66"), Hex("EBC8B8"), Hex("F5EADA"), Hex("4F3A2C"), Hex("1E1414"),
                    pattern: Hex("FFFDF2"), channel: PatternChannel.MaskA, strength: 16, secret: true),
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static SpeciesDefinitionAsset.VariantEntry Variant(string id, RarityTier rarity, Color primary, Color secondary,
            Color belly, Color detail, Color eye, Color? pattern = null, PatternChannel channel = PatternChannel.None, int strength = 0, bool secret = false, bool visitor = false, int mastery = 0)
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
                // A plain palette still has a pattern color: a carried pattern (genetics, T26) shows tone on tone.
                pattern = pattern ?? CarriedPatternTint(primary),
                patternChannel = channel,
                patternStrength = strength,
                secret = secret,
                visitor = visitor,
                masteryLevel = mastery,
            };
        }

        private static Color CarriedPatternTint(Color primary) => Color.Lerp(primary, new Color(0.24f, 0.15f, 0.11f), 0.3f);

        /// <summary>Rewrites the species definitions only (palettes, variants), without rebuilding prefabs.</summary>
        [MenuItem("Pocket Wilds/Animals/Update Species Definitions", priority = 160)]
        public static void UpdateSpeciesDefinitions()
        {
            foreach (var ensure in new Func<SpeciesDefinitionAsset>[] { EnsureRabbitDefinition, EnsureSquirrelDefinition, EnsureHedgehogDefinition,
                EnsureFieldMouseDefinition, EnsureGuineaPigDefinition, EnsureFawnDefinition })
                EnsureContentDatabase(ensure());
            AssetDatabase.SaveAssets();
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

        private static void EnsureVisualCatalog(string visualKey, AnimalView prefab, Action<BehaviorProfile> behavior = null)
        {
            var catalog = LoadOrCreate<AnimalVisualCatalog>(VisualCatalogPath);
            catalog.entries.RemoveAll(e => e == null || e.visualKey == visualKey);
            var entry = new AnimalVisualCatalog.Entry { visualKey = visualKey, prefab = prefab, softCapacity = 24, hardCapacity = 48 };
            behavior?.Invoke(entry.behavior);
            ApplyBody(entry);
            catalog.entries.Add(entry);
            EditorUtility.SetDirty(catalog);
        }

        [MenuItem("Pocket Wilds/Animals/Measure Bodies (T33C)", priority = 160)]
        public static void MeasureBodiesMenu() => Debug.Log("[AnimalPipeline] " + MeasureBodies());

        /// <summary>T33C no-collision rule: every catalog species gets its footprint measured from its mesh.</summary>
        public static string MeasureBodies()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(VisualCatalogPath);
            var lines = new List<string>();
            foreach (var entry in catalog.entries)
            {
                ApplyBody(entry);
                var b = entry.behavior.body;
                lines.Add($"{entry.visualKey}: front {b.front:F3}, back {b.back:F3}, half width {b.halfWidth:F3}, height {b.height:F3}, gap {entry.behavior.socialGap:F3}");
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return string.Join("; ", lines);
        }

        /// <summary>The body capsule from the bind pose (nose, tail, flanks, ears) plus 1 cm for the moving fur and paws.</summary>
        private static void ApplyBody(AnimalVisualCatalog.Entry entry)
        {
            if (entry.prefab == null)
                return;
            entry.behavior.body = MeasureBody(entry.prefab.gameObject);
            // Greeting noses touch, never overlap (a longer snout meets farther away).
            entry.behavior.socialGap = 2f * entry.behavior.body.front + 0.01f;
        }

        public static BodyShape MeasureBody(GameObject prefab)
        {
            const float Margin = 0.01f;
            float front = 0f, back = 0f, side = 0f, top = 0f;
            var root = prefab.transform;
            // The skinned body only: the blob shadow and the sockets are not part of it.
            foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null)
                    continue;
                // LOD0 only: the lower LODs sit inside it.
                var lodGroup = prefab.GetComponent<LODGroup>();
                if (lodGroup != null && lodGroup.lodCount > 0 && Array.IndexOf(lodGroup.GetLODs()[0].renderers, renderer) < 0)
                    continue;
                var toRoot = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                foreach (var v in mesh.vertices)
                {
                    var p = toRoot.MultiplyPoint3x4(v);
                    front = Mathf.Max(front, p.z);
                    back = Mathf.Min(back, p.z);
                    side = Mathf.Max(side, Mathf.Abs(p.x));
                    top = Mathf.Max(top, p.y);
                }
            }
            return new BodyShape(front + Margin, back - Margin, side + Margin, top + Margin);
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
