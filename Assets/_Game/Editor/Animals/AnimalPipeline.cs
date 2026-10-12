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
        /// Their bellies already rest on the ground, so they sleep with the LowSleep clips: the family Sleep lowers the
        /// body 22 mm (fix 2026-10-12, « Aucune collision »).
        /// </summary>
        public static readonly (string Family, string Species)[] LowBodyClips =
        {
            ("Locomotion_Hop", "Locomotion_Walk"),
            ("Happy", "Happy_Bounce"),
            ("Sleep_Enter", "LowSleep_Enter"),
            ("Sleep_Loop", "LowSleep_Loop"),
            ("Sleep_Exit", "LowSleep_Exit"),
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

        /// <summary>
        /// T49 profile poses (Presentation PoseClips): one-shot states returning to Idle, added to a family controller only when
        /// its library has the clip (AN_&lt;Family&gt;_PeekOut in the small and medium libraries; floaters have none).
        /// </summary>
        public static readonly (string State, string Clip)[] PoseStates = { ("PeekOut", "PeekOut") };

        /// <summary>The family table plus the pose states this family's library can play.</summary>
        private static List<(string State, string Clip, bool ReturnsToIdle, string Next)> StatesFor(string family, Dictionary<string, AnimationClip> clips)
        {
            var states = new List<(string State, string Clip, bool ReturnsToIdle, string Next)>(FamilyStates);
            foreach (var (state, clip) in PoseStates)
                if (clips.ContainsKey($"AN_{family}_{clip}"))
                    states.Add((state, clip, true, null));
            return states;
        }

        /// <summary>
        /// Clip suffix of the Move state: the small family hops, the medium family walks (T46); T52 the heavy family waddles
        /// (Locomotion_Walk) and the birds hop on the ground (Locomotion_Hop).
        /// </summary>
        public static string MoveClip(string family) => WalkingFamilies.Contains(family) ? "Locomotion_Walk" : "Locomotion_Hop";

        private static readonly HashSet<string> WalkingFamilies = new HashSet<string> { MediumFamily, HeavyFamily };

        /// <summary>Every family with a library and a controller (AN_&lt;Family&gt;.fbx, AC_&lt;Family&gt;.controller).</summary>
        public static readonly string[] Families = { SmallFamily, MediumFamily, HeavyFamily, BirdFamily };

        /// <summary>One-shot states leave by exit time at this fraction of their clip, then fade (the behavior durations follow).</summary>
        public const float OneShotExitTime = 0.92f, OneShotFade = 0.12f;

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
            var prefab = BuildPrefab(species, material, runtime, ScaledLengths.TryGetValue(species, out float length) ? length : 0f);
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
            var table = StatesFor(family, clips);
            int i = 0;
            foreach (var (stateName, clipSuffix, _, _) in table)
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

            foreach (var (stateName, _, returnsToIdle, next) in table)
            {
                string target = next ?? (returnsToIdle ? "Idle" : null);
                if (target == null)
                    continue;
                var transition = states[stateName].AddTransition(states[target]);
                transition.hasExitTime = true;
                transition.exitTime = OneShotExitTime;
                transition.hasFixedDuration = true;
                transition.duration = OneShotFade;
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
            var table = StatesFor(family, clips);
            if (states.Count != table.Count || machine.defaultState == null || machine.defaultState.name != "Idle")
                return false;
            foreach (var (stateName, clipSuffix, returnsToIdle, next) in table)
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

        /// <summary>
        /// T51 species that ride a family skeleton at the family's scale (the bind poses stay identical, so the family clips fit)
        /// and are shown bigger: the prefab's VisualRoot is scaled uniformly so the LOD0 bind pose measures this length (meters,
        /// nose to tail tip, doc 03 « silhouette mesurée »). AnimalView scales the root at run time (hatch reveal, resting
        /// scale), never the VisualRoot, so this size stays; the measured body, sockets and accessories follow it.
        /// </summary>
        public static readonly Dictionary<string, float> ScaledLengths = new Dictionary<string, float>
        {
            // The bushy low tails (T51 polish) add length behind: the targets keep the bodies at the size validated in game.
            ["Raccoon"] = 0.67f,
            ["RedPanda"] = 0.71f,
        };

        /// <summary>A species scale stays within this range (a model far off the family scale is an export problem, not data).</summary>
        public const float MinSpeciesScale = 0.8f, MaxSpeciesScale = 2.2f;

        public static AnimalView BuildPrefab(string species, Material material, RuntimeAnimatorController controller, float targetLength = 0f)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(species));
            if (model == null)
                throw new InvalidOperationException("Model not imported: " + ModelPath(species));

            var root = new GameObject("PF_Animal_" + species);
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "VisualRoot";
                if (targetLength > 0f)
                {
                    float scale = UniformScaleFor(root.transform, visual, targetLength);
                    if (scale < MinSpeciesScale || scale > MaxSpeciesScale)
                        throw new InvalidOperationException($"{species}: scale {scale:F3} to reach {targetLength:F2} m is outside " +
                                                            $"{MinSpeciesScale}-{MaxSpeciesScale}; check the model's export scale.");
                    visual.transform.localScale = new Vector3(scale, scale, scale);
                    Debug.Log($"[AnimalPipeline] {species}: VisualRoot scaled {scale:F3} to measure {targetLength:F2} m.");
                }

                var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullCompletely;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.keepAnimatorStateOnDisable = true;

                var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .OrderBy(r => r.name, StringComparer.Ordinal).ToArray();
                // T49 variant modules (SKM_<Species>_MOD_<Name>_LOD<n>, e.g. the Star Nose rosette): extra skinned meshes kept
                // out of the body LOD list, shown only on the variant whose moduleSlot asks for them.
                var renderers = skins.Where(r => !IsModule(r.name)).ToArray();
                var modules = skins.Where(r => IsModule(r.name)).ToArray();
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
                var moduleNames = modules.Select(m => ModuleName(m.name)).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
                if (moduleNames.Count > 3)
                    throw new InvalidOperationException($"{species}: at most 3 variant modules, found {moduleNames.Count}.");
                foreach (var m in modules)
                {
                    m.sharedMaterials = new[] { material };
                    m.shadowCastingMode = ShadowCastingMode.Off;
                    m.receiveShadows = true;
                    m.lightProbeUsage = LightProbeUsage.Off;
                    m.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    m.skinnedMotionVectors = false;
                    m.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    m.updateWhenOffscreen = false;
                    m.quality = SkinQuality.Auto;
                    m.enabled = false; // AnimalView turns it on for its variant only
                }

                var lodGroup = visual.GetComponent<LODGroup>() ?? visual.AddComponent<LODGroup>();
                var lods = new LOD[renderers.Length];
                for (int lod = 0; lod < renderers.Length; lod++)
                    lods[lod] = new LOD(LodTransitions[lod], new Renderer[] { renderers[lod] }
                        .Concat(modules.Where(m => ModuleLod(m.name) == lod)).ToArray());
                lodGroup.SetLODs(lods);
                lodGroup.fadeMode = LODFadeMode.None;
                lodGroup.animateCrossFading = false;
                lodGroup.RecalculateBounds();

                // Low quality has no shadow map: a soft disc sized to the LOD0 footprint grounds the animal.
                var footprint = renderers[0].bounds.size;
                var blob = PocketWilds.Editor.Lighting.LightingPipeline.AddBlobShadow(root.transform, new Vector2(footprint.x * 1.35f, footprint.z * 1.2f));

                var view = root.AddComponent<AnimalView>();
                view.SetReferences(renderers.Concat(modules).Cast<Renderer>().ToArray(), animator, lodGroup,
                    FindDeep(visual.transform, "SOCK_HeadTop"), FindDeep(visual.transform, "SOCK_Neck"),
                    FindDeep(visual.transform, "SOCK_Back"), FindDeep(visual.transform, "SOCK_TailBase"), blob);
                view.SetModules(modules.Cast<Renderer>().ToArray(),
                    modules.Select(m => (byte)(moduleNames.IndexOf(ModuleName(m.name)) + 1)).ToArray());

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

        /// <summary>
        /// T51: the uniform VisualRoot scale that makes the LOD0 bind pose (the body skin, no module) measure
        /// <paramref name="targetLength"/> along the root's forward axis.
        /// </summary>
        public static float UniformScaleFor(Transform root, GameObject visual, float targetLength)
        {
            var lod0 = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => !IsModule(r.name) && r.sharedMesh != null)
                .OrderBy(r => r.name, StringComparer.Ordinal).FirstOrDefault();
            if (lod0 == null)
                throw new InvalidOperationException(visual.name + ": no LOD0 skin to measure.");
            var toRoot = root.worldToLocalMatrix * lod0.transform.localToWorldMatrix;
            float front = float.MinValue, back = float.MaxValue;
            foreach (var v in lod0.sharedMesh.vertices)
            {
                float z = toRoot.MultiplyPoint3x4(v).z;
                front = Mathf.Max(front, z);
                back = Mathf.Min(back, z);
            }
            float length = front - back;
            float current = visual.transform.localScale.x;
            return length > 1e-4f ? current * targetLength / length : 1f;
        }

        /// <summary>T51: length in seconds of a family library clip (AN_{family}_{suffix}), or the fallback while it is missing.</summary>
        public static float ClipSeconds(string family, string suffix, float fallback)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(ClipsPath(family)).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == $"AN_{family}_{suffix}");
            return clip != null && clip.length > 0.1f ? clip.length : fallback;
        }

        /// <summary>T49: a variant module mesh (SKM_Species_MOD_Name_LODn). Slots follow the module names in ordinal order.</summary>
        public static bool IsModule(string rendererName) => rendererName.Contains("_MOD_");

        public static string ModuleName(string rendererName)
        {
            int start = rendererName.IndexOf("_MOD_", StringComparison.Ordinal) + 5;
            int end = rendererName.LastIndexOf("_LOD", StringComparison.Ordinal);
            return end > start ? rendererName.Substring(start, end - start) : rendererName.Substring(start);
        }

        public static int ModuleLod(string rendererName)
        {
            int at = rendererName.LastIndexOf("_LOD", StringComparison.Ordinal);
            return at >= 0 && int.TryParse(rendererName.Substring(at + 4), out int lod) ? lod : 0;
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
                // T49 event variant (L'Éveil des Terriers, event egg only): pale dew blue-green coat, round glassy dew spots (mask A).
                Variant("variant.rabbit.dew", RarityTier.Rare, Hex("D6EBE6"), Hex("F4CDD3"), Hex("F7FCFA"), Hex("E3A0AA"), Hex("1E2024"),
                    pattern: Hex("EEF9FB"), channel: PatternChannel.MaskA, strength: 13, secret: true),
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
                // T48B visitor variant « Écureuil soleil » (season 2, Soleil): warm golden coat, sunny orange tail, honey tail tip and
                // ear tufts (mask A). Palette only, secret: never in eggs, pities or genetics.
                Variant("variant.squirrel.sun_visitor", RarityTier.Rare, Hex("E8B24E"), Hex("E2893A"), Hex("FBEFD2"), Hex("5A3A2E"), Hex("1E1414"),
                    pattern: Hex("F7DA7A"), channel: PatternChannel.MaskA, strength: 14, secret: true, visitor: true),
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
                // T49 event variants (event egg only, palette and pattern): clover green spines with round clover spots
                // (mask A), and soft moss spines with light tips (mask B). variant.hedgehog.moss is already the mastery 30
                // color, so the event's « Hérisson Mousse » is softmoss « Mousse tendre ».
                // Told apart at a glance: bright clover green with cream spots, against a muted olive moss with pale tips.
                Variant("variant.hedgehog.clover", RarityTier.Rare, Hex("F1E8D2"), Hex("58AE47"), Hex("FAF4E6"), Hex("3A2A28"), Hex("1A1212"),
                    pattern: Hex("FBFBEF"), channel: PatternChannel.MaskA, strength: 16, secret: true),
                Variant("variant.hedgehog.softmoss", RarityTier.Rare, Hex("E6D7BA"), Hex("7C8B55"), Hex("F3E9D6"), Hex("2E2A20"), Hex("140E0D"),
                    pattern: Hex("D3DCA6"), channel: PatternChannel.MaskB, strength: 15, secret: true),
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
                // T49 event variant (event egg only): fresh bud green coat, cream belly and cream patches (mask A).
                Variant("variant.fieldmouse.bud", RarityTier.Rare, Hex("B8D28E"), Hex("F3C8C2"), Hex("F8F1DC"), Hex("E8A0A6"), Hex("1E1414"),
                    pattern: Hex("F6EFD2"), channel: PatternChannel.MaskA, strength: 12, secret: true),
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

        /// <summary>
        /// T47: the Meadow Pack's companion walks like the guinea pig; its joy is its own greeting (played when the player
        /// comes back) and its second idle tucks a seed in its cheeks. Its belly rests on the ground: it sleeps with the
        /// LowSleep clips, like the low-bodied species.
        /// </summary>
        public static readonly (string Family, string Species)[] FieldHamsterClips =
        {
            ("Locomotion_Hop", "Locomotion_Walk"),
            ("Happy", "FieldHamsterGreet"),
            ("Idle_B", "FieldHamsterStash"),
            ("Sleep_Enter", "LowSleep_Enter"),
            ("Sleep_Loop", "LowSleep_Loop"),
            ("Sleep_Exit", "LowSleep_Exit"),
        };

        /// <summary>
        /// T48B the two season visitor variants (variant.fawn.white_visitor, variant.squirrel.sun_visitor): palette variants on the
        /// existing meshes, so only the Squirrel and Fawn definitions are rewritten (no model export, no prefab).
        /// </summary>
        [MenuItem("Pocket Wilds/Animals/Update Season Visitor Variants (T48B)", priority = 158)]
        public static void UpdateSeasonVisitorVariants()
        {
            EnsureSquirrelDefinition();
            EnsureFawnDefinition();
            AssetDatabase.SaveAssets();
            Debug.Log("[AnimalPipeline] Squirrel and Fawn definitions updated with their season visitor variants.");
        }

        [MenuItem("Pocket Wilds/Animals/Build Field Hamster (T47)", priority = 157)]
        public static void BuildFieldHamster()
        {
            BuildSpecies("FieldHamster", "SmallQuadruped", EnsureFieldHamsterDefinition, FieldHamsterClips, b => b.moveSpeed = 0.27f);
        }

        /// <summary>
        /// T49 Mole (A-SP-203, « corps bas »): its own low shuffle, low nibble, dig, sleep, startle and happy wiggle. The
        /// family Hop, Walk, Eat, Inspect, Sleep, ReactTouch, Happy and Happy_Bounce swing the big mitten hands or the low
        /// snout below the ground; the Mole clips are family clips checked frame by frame on the Mole (pw_anim_library).
        /// </summary>
        public static readonly (string Family, string Species)[] MoleClips =
        {
            ("Locomotion_Hop", "MoleWalk"),
            ("Happy", "MoleHappy"),
            ("ReactTouch", "MoleReact"),
            ("Sleep_Enter", "MoleSleep_Enter"),
            ("Sleep_Loop", "MoleSleep_Loop"),
            ("Sleep_Exit", "MoleSleep_Exit"),
            ("Eat_Loop", "MoleEat"),
            ("Inspect", "MoleDig"),
        };

        [MenuItem("Pocket Wilds/Animals/Build Mole (T49)", priority = 158)]
        public static void BuildMole()
        {
            BuildSpecies("Mole", "SmallQuadruped", EnsureMoleDefinition, MoleClips, b => b.moveSpeed = 0.22f);
        }

        // ---------------- T51 Forest species: Fox, Badger (MediumQuadruped), Raccoon, Red Panda (SmallQuadruped) ----------------
        // Rig reuse, no code exception: one species clip each, a family clip swapped through data like the Mole's. The clip
        // names are the family libraries' (pw_anim_medium FoxCurlSleep and BadgerDig, pw_anim_library RaccoonWash and
        // RedPandaStand); a state is moved by editing its row only.

        public const string MediumFamily = "MediumQuadruped";
        public const string SmallFamily = "SmallQuadruped";

        /// <summary>The fox sleeps curled up, its tail over its nose (A-SP-007 « sommeil enroulé »).</summary>
        public static readonly (string Family, string Species)[] FoxClips =
        {
            // Its curled sleep (tail toward the nose), and low versions of eat, inspect and peek out: the family clips fold a
            // short-necked head into the chest and sink the sleeping body into the ground (measured in Blender, T51).
            ("Sleep_Enter", "FoxCurlSleep_Enter"),
            ("Sleep_Loop", "FoxCurlSleep_Loop"),
            ("Sleep_Exit", "FoxCurlSleep_Exit"),
            ("Eat_Loop", "LowEat_Loop"),
            ("Inspect", "LowInspect"),
            ("PeekOut", "LowPeekOut"),
        };

        /// <summary>The badger's inspect is its little dig: both front paws scratch, then it sniffs (nothing goes below the ground).</summary>
        public static readonly (string Family, string Species)[] BadgerClips =
        {
            ("Inspect", "BadgerDig"),
            // A low body: its own sleep and the low eat and peek out (the family ones sink it into the ground, T51).
            ("Sleep_Enter", "BadgerSleep_Enter"),
            ("Sleep_Loop", "BadgerSleep_Loop"),
            ("Sleep_Exit", "BadgerSleep_Exit"),
            ("Eat_Loop", "LowEat_Loop"),
            ("PeekOut", "LowPeekOut"),
        };

        /// <summary>
        /// The raccoon walks (no hop) and bounces for joy like the low-bodied species; its inspect is the wash: it sits and rubs
        /// its front paws together (A-SP-008, « Inspect pondéré plus haut »).
        /// </summary>
        public static readonly (string Family, string Species)[] RaccoonClips =
        {
            ("Locomotion_Hop", "Locomotion_Walk"),
            ("Happy", "Happy_Bounce"),
            ("Inspect", "RaccoonWash"),
        };

        /// <summary>The red panda walks and bounces too; its second idle rises on the hind legs to look around, then settles.</summary>
        public static readonly (string Family, string Species)[] RedPandaClips =
        {
            ("Locomotion_Hop", "Locomotion_Walk"),
            ("Happy", "Happy_Bounce"),
            ("Idle_B", "RedPandaStand"),
        };

        /// <summary>Medium animals greet medium animals (shared Social_Nose clip); scaled small skeletons greet each other only.</summary>
        public const int MediumSocialGroup = 1, ScaledSmallSocialGroup = 2;

        [MenuItem("Pocket Wilds/Animals/Build Fox (T51)", priority = 159)]
        public static void BuildFox()
        {
            // Quick and playful: the brisk walk of the family.
            BuildSpecies("Fox", MediumFamily, EnsureFoxDefinition, FoxClips, b =>
            {
                b.moveSpeed = 0.34f;
                b.socialGroup = MediumSocialGroup;
            });
        }

        [MenuItem("Pocket Wilds/Animals/Build Badger (T51)", priority = 159)]
        public static void BuildBadger()
        {
            // Low, heavy and calm: slower than the fox and the fawn; the dig holds as long as its clip.
            BuildSpecies("Badger", MediumFamily, EnsureBadgerDefinition, BadgerClips, b =>
            {
                b.moveSpeed = 0.25f;
                b.socialGroup = MediumSocialGroup;
                b.inspectSeconds = ClipSeconds(MediumFamily, "BadgerDig", b.inspectSeconds);
            });
        }

        [MenuItem("Pocket Wilds/Animals/Build Raccoon (T51)", priority = 159)]
        public static void BuildRaccoon()
        {
            // Curious: inspects more often (doc 02), the wash holds as long as its clip. Scaled to 0.62 m (ScaledLengths).
            BuildSpecies("Raccoon", SmallFamily, EnsureRaccoonDefinition, RaccoonClips, b =>
            {
                b.moveSpeed = 0.28f;
                b.socialGroup = ScaledSmallSocialGroup;
                b.inspectWeight = 0.18f;
                b.wanderWeight = 0.26f;
                b.inspectSeconds = ClipSeconds(SmallFamily, "RaccoonWash", b.inspectSeconds);
            });
        }

        [MenuItem("Pocket Wilds/Animals/Build Red Panda (T51)", priority = 159)]
        public static void BuildRedPanda()
        {
            // Sleepy dreamer: dozes a little more; the stand holds as long as its clip. Scaled to 0.66 m (ScaledLengths).
            BuildSpecies("RedPanda", SmallFamily, EnsureRedPandaDefinition, RedPandaClips, b =>
            {
                b.moveSpeed = 0.26f;
                b.socialGroup = ScaledSmallSocialGroup;
                b.sleepWeight = 0.09f;
                b.wanderWeight = 0.29f;
                b.idleLookSeconds = ClipSeconds(SmallFamily, "RedPandaStand", b.idleLookSeconds);
            });
        }

        [MenuItem("Pocket Wilds/Animals/Build Forest Species (T51)", priority = 159)]
        public static void BuildT51()
        {
            BuildFox();
            BuildBadger();
            BuildRaccoon();
            BuildRedPanda();
        }

        // ---------------- T52 Forest species on their own families: Owl (Bird), Boar Piglet (HeavyQuadruped) ----------------
        // Two new skeletons (RIG-003 SK_HeavyQuadruped, RIG-004 SK_Bird) with minimal libraries named like the family table
        // (AN_<Family>_Idle_A ... _Inspect), so the family controller needs no override: the library is the species' own
        // behavior (the owl's slow head turn, feather fluff, wing flutter and bow; the piglet's waddle, bounce and snout push).
        // Heavy walks (Locomotion_Walk), the birds hop on the ground (Locomotion_Hop): see MoveClip. No flight, no perch yet.

        public const string HeavyFamily = "HeavyQuadruped";
        public const string BirdFamily = "Bird";

        /// <summary>T52: each new family greets its own family only (its own Social_Nose clip): a piglet never noses a rabbit.</summary>
        public const int HeavySocialGroup = 3, BirdSocialGroup = 4;

        [MenuItem("Pocket Wilds/Animals/Build Owl (T52)", priority = 159)]
        public static void BuildOwl()
        {
            // Sleepy (doc 03): dozes a little more and looks around more (its slow head turn); short ground hops.
            BuildSpecies("Owl", BirdFamily, EnsureOwlDefinition, null, b =>
            {
                ApplyFamilyTiming(BirdFamily, b);
                b.moveSpeed = 0.22f;
                b.socialGroup = BirdSocialGroup;
                b.sleepWeight = 0.10f;
                b.idleLookWeight = 0.24f;
                b.wanderWeight = 0.24f;
            });
        }

        [MenuItem("Pocket Wilds/Animals/Build Boar Piglet (T52)", priority = 159)]
        public static void BuildBoarPiglet()
        {
            // Brave, snout first (doc 03): eats and pushes the leaves more often (Eat then Inspect, its signature); a short
            // waddle on short legs.
            BuildSpecies("BoarPiglet", HeavyFamily, EnsureBoarPigletDefinition, null, b =>
            {
                ApplyFamilyTiming(HeavyFamily, b);
                b.moveSpeed = 0.27f;
                b.socialGroup = HeavySocialGroup;
                b.eatWeight = 0.17f;
                b.inspectWeight = 0.16f;
                b.wanderWeight = 0.30f;
            });
        }

        [MenuItem("Pocket Wilds/Animals/Build Owl and Boar Piglet (T52)", priority = 159)]
        public static void BuildT52()
        {
            BuildOwl();
            BuildBoarPiglet();
        }

        /// <summary>
        /// T52: the one-shot durations of a family's own library (clip length x exit time + fade), so the scheduler hands over
        /// when the Animator is back in Idle. The BehaviorProfile defaults are the SmallQuadruped library's.
        /// </summary>
        public static void ApplyFamilyTiming(string family, BehaviorProfile b)
        {
            b.idleLookSeconds = OneShotSeconds(family, "Idle_B", b.idleLookSeconds);
            b.inspectSeconds = OneShotSeconds(family, "Inspect", b.inspectSeconds);
            b.reactSeconds = OneShotSeconds(family, "ReactTouch", b.reactSeconds);
            b.happySeconds = OneShotSeconds(family, "Happy", b.happySeconds);
            b.wakeSeconds = OneShotSeconds(family, "Sleep_Exit", b.wakeSeconds);
            b.socialSeconds = OneShotSeconds(family, "Social_Nose", b.socialSeconds);
        }

        /// <summary>How long a one-shot family clip holds its state (exit time, then the fade), or the fallback while it is missing.</summary>
        public static float OneShotSeconds(string family, string suffix, float fallback)
        {
            float length = ClipSeconds(family, suffix, -1f);
            return length > 0f ? length * OneShotExitTime + OneShotFade : fallback;
        }

        /// <summary>
        /// T49 Mole, the featured species of L'Éveil des Terriers (event.burrows). Every color is secret: never in normal eggs,
        /// wishes or genetics; the event egg and track give the six event colors by id, mastery gives the last three.
        /// Regions (pw_masks mole_masks): primary = velvet coat, secondary = pink mitten hands, snout tip, hind feet and tail,
        /// belly = underside and chin, detail = nose bead and the Star Nose rosette; mask A = hands and nose, mask B = paler
        /// muzzle, mask C = round moss patches over the back. Star Nose shows module slot 1 (SKM_Mole_MOD_StarNose).
        /// </summary>
        internal static SpeciesDefinitionAsset EnsureMoleDefinition()
        {
            var asset = LoadOrCreate<SpeciesDefinitionAsset>(DefinitionPath("Mole"));
            asset.sourceId = "species.mole";
            asset.displayKey = "species.mole.name";
            asset.rigFamily = RigFamily.SmallQuadruped;
            asset.biomeId = "biome.grassland";
            asset.baseProduction = 1.0;
            asset.visualKey = "PF_Animal_Mole";
            asset.hiddenSockets = new List<AccessorySocket> { AccessorySocket.TailBase };
            asset.variants = new List<SpeciesDefinitionAsset.VariantEntry>
            {
                Variant("variant.mole.velvet", RarityTier.Rare, Hex("6F6B75"), Hex("F2B6C0"), Hex("8B8792"), Hex("EE9CAB"), Hex("141012"),
                    pattern: Hex("9A95A3"), channel: PatternChannel.MaskC, strength: 8, secret: true),
                Variant("variant.mole.cocoa", RarityTier.Rare, Hex("5E4336"), Hex("F0B4B6"), Hex("735649"), Hex("E996A4"), Hex("120C0C"),
                    pattern: Hex("9A7A64"), channel: PatternChannel.MaskB, strength: 10, secret: true),
                Variant("variant.mole.sand", RarityTier.Rare, Hex("CDB590"), Hex("F4C2C0"), Hex("E3D2B4"), Hex("EBA2AA"), Hex("1E1414"),
                    pattern: Hex("F1E6CF"), channel: PatternChannel.MaskB, strength: 12, secret: true),
                // Vivid pink hands and nose.
                Variant("variant.mole.mocha", RarityTier.Epic, Hex("7D5C4C"), Hex("FF93B2"), Hex("957462"), Hex("FF82A6"), Hex("140E0D"),
                    pattern: Hex("FF7AA6"), channel: PatternChannel.MaskA, strength: 15, secret: true),
                // Golden moss green, the back glowing like sunlit moss: a pale gold pattern at full strength on the back patches
                // (mask C), plus the Legendary rim glow. Soft, never a real light.
                Variant("variant.mole.goldenmoss", RarityTier.Legendary, Hex("7F8C4B"), Hex("F3C2A8"), Hex("9DA766"), Hex("EFA2A0"), Hex("141010"),
                    pattern: Hex("F7E59E"), channel: PatternChannel.MaskC, strength: 15, secret: true),
                // Deep velvet grey, the round pink lobed rosette (module slot 1) and a soft pink glow on hands, nose and rosette.
                Variant("variant.mole.starnose", RarityTier.Epic, Hex("4B4752"), Hex("FFB6CA"), Hex("5E5A64"), Hex("FFA0C0"), Hex("0E0C10"),
                    pattern: Hex("FFC4D8"), channel: PatternChannel.MaskA, strength: 15, secret: true, module: 1),
                Variant("variant.mole.gardensoil", RarityTier.Rare, Hex("4F3B2F"), Hex("EDB2AF"), Hex("645246"), Hex("E6969F"), Hex("120C0C"),
                    pattern: Hex("7C604A"), channel: PatternChannel.MaskC, strength: 10, secret: true, mastery: 10),
                Variant("variant.mole.tendersprout", RarityTier.Epic, Hex("A8C27C"), Hex("F6C8C0"), Hex("DAE6BC"), Hex("EDA4AA"), Hex("1E1414"),
                    pattern: Hex("EDF5CF"), channel: PatternChannel.MaskB, strength: 12, secret: true, mastery: 20),
                // Golden glow on hands and nose (mask A).
                Variant("variant.mole.goldenpaws", RarityTier.Legendary, Hex("5F5967"), Hex("F3D68C"), Hex("736D79"), Hex("F2C874"), Hex("120E14"),
                    pattern: Hex("FFE38E"), channel: PatternChannel.MaskA, strength: 15, secret: true, mastery: 30),
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
                // T48B visitor variant « Faon blanc » (season 1, Éveil): pearly white coat, blossom pink ears, soft pale blossom spots
                // (mask A), silver hooves. Palette only, secret: never in eggs, pities or genetics.
                Variant("variant.fawn.white_visitor", RarityTier.Rare, Hex("F7F6F1"), Hex("F5CFD6"), Hex("FFFFFF"), Hex("C7CCD6"), Hex("1E1820"),
                    pattern: Hex("F3E3EC"), channel: PatternChannel.MaskA, strength: 10, secret: true, visitor: true),
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// T47 Field Hamster, the Meadow Pack's companion (A-SP-101): never in eggs, never bred, out of the Codex completion
        /// (companion implies secret), no TailBase socket (hidden tail). Its three looks are Rare (« produit comme un animal Rare
        /// de son biome ») and secret: the base look, then the looks given at bond 5 and 10 (CompanionCatalog).
        /// </summary>
        internal static SpeciesDefinitionAsset EnsureFieldHamsterDefinition()
        {
            var asset = LoadOrCreate<SpeciesDefinitionAsset>(DefinitionPath("FieldHamster"));
            asset.sourceId = "species.fieldhamster";
            asset.displayKey = "species.fieldhamster.name";
            asset.rigFamily = RigFamily.SmallQuadruped;
            asset.biomeId = "biome.grassland";
            asset.baseProduction = 1.0;
            asset.visualKey = "PF_Animal_FieldHamster";
            asset.secret = true;
            asset.companion = true;
            asset.hiddenSockets = new List<AccessorySocket> { AccessorySocket.TailBase };

            // Field hamster regions: primary = golden back, secondary = round ears and paws, belly = white belly, chest and muzzle,
            // shared masks on one mesh (pw_masks fieldhamster_masks): cheek band = mask B, back spots = mask C, ear glow = mask A.
            asset.variants = new List<SpeciesDefinitionAsset.VariantEntry>
            {
                Variant("variant.fieldhamster.fieldgold", RarityTier.Rare, Hex("D9A257"), Hex("F2C3B2"), Hex("FCF6EA"), Hex("E8959E"), Hex("1E1414"),
                    pattern: Hex("F7E6C8"), channel: PatternChannel.MaskB, strength: 12, secret: true),
                Variant("variant.fieldhamster.wheatcream", RarityTier.Rare, Hex("EAD2A4"), Hex("F4CEC0"), Hex("FFFCF4"), Hex("E8A0A6"), Hex("1E1414"),
                    pattern: Hex("C99A5E"), channel: PatternChannel.MaskC, strength: 12, secret: true),
                Variant("variant.fieldhamster.fieldlavender", RarityTier.Rare, Hex("B8A5C8"), Hex("EBC7D5"), Hex("FBF8FC"), Hex("E6A2B2"), Hex("1E1414"),
                    pattern: Hex("F3DCF2"), channel: PatternChannel.MaskA, strength: 14, secret: true),
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // ---------------- T51 Forest species data (doc 03 Roster, A-SP-007 to A-SP-011) ----------------
        // Every Forest species: biome.forest, base production 8 (ProgressionConfig, Forest BiomeEconomy), every socket
        // (HeadTop, Neck, Back, TailBase, Ear_L/R). Rows: the six roster colors (the sixth is Epic, doc 03 « Forêt »), the mastery
        // colors 10/20/30 (secret: never in eggs, wishes or genetics), the secret colors (flag only: their conditions come with
        // their systems, like fawn.daisy), the season visitors (palette only, no mesh change). A variant has one pattern channel;
        // glows are a light pattern color at full strength (15) on a soft mask, never a light.

        /// <summary>Base production of every Forest species (decision 7; Grassland species stay at 1).</summary>
        public const double ForestBaseProduction = 8.0;

        private static SpeciesDefinitionAsset ForestSpecies(string name, string id, RigFamily family)
        {
            var asset = LoadOrCreate<SpeciesDefinitionAsset>(DefinitionPath(name));
            asset.sourceId = id;
            asset.displayKey = id + ".name";
            asset.rigFamily = family;
            asset.biomeId = "biome.forest";
            asset.baseProduction = ForestBaseProduction;
            asset.visualKey = "PF_Animal_" + name;
            asset.secret = false;
            asset.companion = false;
            asset.hiddenSockets = new List<AccessorySocket>();
            return asset;
        }

        /// <summary>
        /// A-SP-007 Fox. Regions: primary = coat, secondary = inner ears, belly = chest, belly, muzzle and cheeks, detail = legs,
        /// paws, ear tips and nose. Masks (pw_masks fox rule): A = tail tip and ear tips (the glow area), B = shoulder cross and
        /// the paler face mask. The white tail tip of the roster colors is a soft mask A pattern.
        /// </summary>
        internal static SpeciesDefinitionAsset EnsureFoxDefinition()
        {
            var asset = ForestSpecies("Fox", "species.fox", RigFamily.MediumQuadruped);
            asset.variants = new List<SpeciesDefinitionAsset.VariantEntry>
            {
                Variant("variant.fox.red", RarityTier.Common, Hex("D9783F"), Hex("F2C6B8"), Hex("FBF1E2"), Hex("4A3029"), Hex("1E1414"),
                    pattern: Hex("FFF8EE"), channel: PatternChannel.MaskA, strength: 13),
                Variant("variant.fox.silver", RarityTier.Common, Hex("A8AAB0"), Hex("D9C0C3"), Hex("F9F8F6"), Hex("4A4B51"), Hex("1A1618"),
                    pattern: Hex("F4F4F2"), channel: PatternChannel.MaskA, strength: 12),
                Variant("variant.fox.cream", RarityTier.Uncommon, Hex("EFCF98"), Hex("F6D3CF"), Hex("FFFDF8"), Hex("C08A52"), Hex("1E1414"),
                    pattern: Hex("FFF9EE"), channel: PatternChannel.MaskA, strength: 12),
                Variant("variant.fox.cross", RarityTier.Uncommon, Hex("D27A45"), Hex("F1D2BE"), Hex("FBF1E2"), Hex("4A3029"), Hex("1E1414"),
                    pattern: Hex("6A4434"), channel: PatternChannel.MaskB, strength: 14),
                Variant("variant.fox.white", RarityTier.Rare, Hex("F8F6F2"), Hex("F4CFCF"), Hex("FFFFFF"), Hex("CFC9C4"), Hex("1E1414"),
                    pattern: Hex("C9CCD2"), channel: PatternChannel.MaskA, strength: 13),
                // Night brown, soft green ears, a softly glowing yellow-green tail tip (palette and glow only, doc 02).
                Variant("variant.fox.firefly", RarityTier.Epic, Hex("4A3B3A"), Hex("B9D3A0"), Hex("F1E8D2"), Hex("2E2422"), Hex("141010"),
                    pattern: Hex("E4F27A"), channel: PatternChannel.MaskA, strength: 15),
                Variant("variant.fox.brick", RarityTier.Rare, Hex("B9573E"), Hex("F0C7B8"), Hex("F7E9D4"), Hex("4E3026"), Hex("1E1414"), secret: true, mastery: 10),
                // Pale blue grey, the lighter face mask (mask B).
                Variant("variant.fox.mist", RarityTier.Epic, Hex("B6C0CC"), Hex("E7CDD2"), Hex("FFFFFF"), Hex("6C7482"), Hex("1E1820"),
                    pattern: Hex("EEF2F6"), channel: PatternChannel.MaskB, strength: 14, secret: true, mastery: 20),
                // Golden russet, a golden glow on the tail tip (mask A), plus the Legendary rim glow.
                Variant("variant.fox.goldleaf", RarityTier.Legendary, Hex("D79A48"), Hex("F2CDB0"), Hex("F8EBD2"), Hex("5A3A26"), Hex("1E1414"),
                    pattern: Hex("FFE7A0"), channel: PatternChannel.MaskC, strength: 15, secret: true, mastery: 30),
                // Secret « Braise »: deep ember red, orange cream belly, a warm orange glow on the tail tip.
                Variant("variant.fox.ember", RarityTier.Epic, Hex("B0402A"), Hex("F0BFA8"), Hex("F6D8B8"), Hex("3E2620"), Hex("1E1414"),
                    pattern: Hex("FFA25A"), channel: PatternChannel.MaskA, strength: 15, secret: true),
                // Secret « Myrtille » (breeding Silver x Cross later): soft bilberry blue, lilac belly, a soft violet glow on the tips.
                Variant("variant.fox.bilberry", RarityTier.Epic, Hex("6F7FB8"), Hex("E3CFE6"), Hex("E6DDF0"), Hex("2E3358"), Hex("161420"),
                    pattern: Hex("D7B4F5"), channel: PatternChannel.MaskA, strength: 15, secret: true),
                // T51 season visitor « Renard doré » (season 2, Soleil; visitor.golden_fox): warm golden coat, honey paws, a pale gold
                // tail tip (mask A). Palette only, secret: never in eggs, pities or genetics.
                Variant("variant.fox.golden_visitor", RarityTier.Rare, Hex("E9B65C"), Hex("F4D2BC"), Hex("FCF3DE"), Hex("7A5232"), Hex("1E1414"),
                    pattern: Hex("FFE9A6"), channel: PatternChannel.MaskA, strength: 14, secret: true, visitor: true),
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// A-SP-009 Badger. Regions: primary = coat, secondary = inner ears, belly = underside, detail = the dark face stripes,
        /// legs and nose. Masks (pw_masks badger rule): A = paws and muzzle (the glow area), B = the pale face blaze between the
        /// stripes (the « bandes blanches » of the roster colors).
        /// </summary>
        internal static SpeciesDefinitionAsset EnsureBadgerDefinition()
        {
            var asset = ForestSpecies("Badger", "species.badger", RigFamily.MediumQuadruped);
            asset.variants = new List<SpeciesDefinitionAsset.VariantEntry>
            {
                Variant("variant.badger.classic", RarityTier.Common, Hex("9C9A98"), Hex("EAD9C2"), Hex("C9C6C2"), Hex("2E2A2A"), Hex("141010"),
                    pattern: Hex("FAF7F2"), channel: PatternChannel.MaskB, strength: 14),
                Variant("variant.badger.honey", RarityTier.Common, Hex("D4A15E"), Hex("F1DDBF"), Hex("F5E8CF"), Hex("8A5A2E"), Hex("1E1414"),
                    pattern: Hex("FBF4E6"), channel: PatternChannel.MaskB, strength: 14),
                Variant("variant.badger.grey", RarityTier.Uncommon, Hex("C5C2C0"), Hex("DCC4C4"), Hex("F2EEE8"), Hex("5C5858"), Hex("1A1416"),
                    pattern: Hex("F7F4F0"), channel: PatternChannel.MaskB, strength: 12),
                Variant("variant.badger.dark", RarityTier.Uncommon, Hex("5A5654"), Hex("CDB5B3"), Hex("8A8684"), Hex("1E1A1A"), Hex("0E0A0A"),
                    pattern: Hex("D9D6D2"), channel: PatternChannel.MaskB, strength: 14),
                Variant("variant.badger.cream", RarityTier.Rare, Hex("F1E2C8"), Hex("F4CFCB"), Hex("FFFDF8"), Hex("B98754"), Hex("1E1414"),
                    pattern: Hex("FFFBF2"), channel: PatternChannel.MaskB, strength: 12),
                Variant("variant.badger.moss", RarityTier.Epic, Hex("8FA675"), Hex("E3E8C8"), Hex("F3EDD6"), Hex("3E5A3C"), Hex("141410"),
                    pattern: Hex("F4F1DC"), channel: PatternChannel.MaskB, strength: 14),
                Variant("variant.badger.charcoal", RarityTier.Rare, Hex("4E4B4C"), Hex("C9B8B6"), Hex("8C8888"), Hex("2A2626"), Hex("0E0A0A"),
                    pattern: Hex("D3D0CC"), channel: PatternChannel.MaskB, strength: 12, secret: true, mastery: 10),
                // Pale sage green, wide cream blaze (mask B at full strength).
                Variant("variant.badger.sage", RarityTier.Epic, Hex("B5C3A2"), Hex("EEE6CF"), Hex("F3EFE0"), Hex("5E6B52"), Hex("161812"),
                    pattern: Hex("F6EED6"), channel: PatternChannel.MaskB, strength: 15, secret: true, mastery: 20),
                // Warm amber, an amber glow on paws and muzzle (mask A), plus the Legendary rim glow.
                Variant("variant.badger.amber", RarityTier.Legendary, Hex("D58E3C"), Hex("F3D3B0"), Hex("F6E3C4"), Hex("5A3420"), Hex("1E1414"),
                    pattern: Hex("FFC46A"), channel: PatternChannel.MaskA, strength: 15, secret: true, mastery: 30),
                // Secret « Réglisse » (breeding Dark x Cream later): soft deep black, warm grey belly, a soft pink glow on the blaze.
                Variant("variant.badger.licorice", RarityTier.Epic, Hex("2E2A2C"), Hex("D9B8BC"), Hex("8E827E"), Hex("1A1618"), Hex("0E0A0C"),
                    pattern: Hex("FFC9D6"), channel: PatternChannel.MaskB, strength: 15, secret: true),
                // T51 season visitor « Blaireau ambré » (season 3, Récolte; visitor.amber_badger): light honey amber coat, warm brown
                // stripes, cream blaze (mask B). Lighter than the mastery Amber, which glows. Palette only, secret.
                Variant("variant.badger.amber_visitor", RarityTier.Rare, Hex("E0A65A"), Hex("F4DAB8"), Hex("F7EAD2"), Hex("5E3B26"), Hex("1E1414"),
                    pattern: Hex("FBEFD8"), channel: PatternChannel.MaskB, strength: 14, secret: true, visitor: true),
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// A-SP-008 Raccoon (SmallQuadruped skeleton, prefab scaled to 0.62 m). Regions (pw_masks raccoon_masks): primary = coat,
        /// secondary = inner ears and little hands and feet, belly = brows, muzzle, lower cheeks, throat and underside, detail =
        /// the dark face mask and nose. Masks A and B = the tail rings and tip (one mesh, no extra material): every color draws its
        /// rings on mask B in the mask's color; the glowing rings are mask A at full strength.
        /// </summary>
        internal static SpeciesDefinitionAsset EnsureRaccoonDefinition()
        {
            var asset = ForestSpecies("Raccoon", "species.raccoon", RigFamily.SmallQuadruped);
            asset.variants = new List<SpeciesDefinitionAsset.VariantEntry>
            {
                Variant("variant.raccoon.classic", RarityTier.Common, Hex("8E8076"), Hex("EBC9BE"), Hex("D6D1CC"), Hex("3A3333"), Hex("120E0E"),
                    pattern: Hex("3A3333"), channel: PatternChannel.MaskB, strength: 15),
                Variant("variant.raccoon.dark", RarityTier.Common, Hex("55524F"), Hex("D2B2B0"), Hex("9E9A98"), Hex("1E1B1B"), Hex("0E0A0A"),
                    pattern: Hex("1E1B1B"), channel: PatternChannel.MaskB, strength: 15),
                Variant("variant.raccoon.cream", RarityTier.Uncommon, Hex("EEDFC6"), Hex("F4D0CC"), Hex("FFFDF8"), Hex("C08E5C"), Hex("1E1414"),
                    pattern: Hex("C08E5C"), channel: PatternChannel.MaskB, strength: 15),
                Variant("variant.raccoon.grey", RarityTier.Uncommon, Hex("C7C5C6"), Hex("DDC6C6"), Hex("F3F0EA"), Hex("5E6670"), Hex("161416"),
                    pattern: Hex("5E6670"), channel: PatternChannel.MaskB, strength: 15),
                Variant("variant.raccoon.cinnamon", RarityTier.Rare, Hex("B9774A"), Hex("F6CDB3"), Hex("F6E7D0"), Hex("7A3F27"), Hex("1E1414"),
                    pattern: Hex("7A3F27"), channel: PatternChannel.MaskB, strength: 15),
                // Night blue grey, lilac hands, a soft silver mask and softly glowing silver rings (mask B at full strength).
                Variant("variant.raccoon.moonmask", RarityTier.Epic, Hex("4E5468"), Hex("D3C3E3"), Hex("C9CDD8"), Hex("DDE5F0"), Hex("12121A"),
                    pattern: Hex("E6EEF8"), channel: PatternChannel.MaskB, strength: 15),
                Variant("variant.raccoon.slate", RarityTier.Rare, Hex("6F7783"), Hex("DCC6C8"), Hex("C8CCD2"), Hex("3C424C"), Hex("141418"),
                    pattern: Hex("3C424C"), channel: PatternChannel.MaskB, strength: 14, secret: true, mastery: 10),
                // Milk tea beige, wide light brown rings (mask B).
                Variant("variant.raccoon.milktea", RarityTier.Epic, Hex("D7C1A3"), Hex("F1D6CB"), Hex("F7EEDD"), Hex("A07A58"), Hex("1E1414"),
                    pattern: Hex("A8825E"), channel: PatternChannel.MaskB, strength: 15, secret: true, mastery: 20),
                // Warm brown, cream mask, golden glowing rings (mask A), plus the Legendary rim glow.
                Variant("variant.raccoon.goldenrings", RarityTier.Legendary, Hex("9A6A48"), Hex("F0CDB8"), Hex("EED9BF"), Hex("F1DFC2"), Hex("1E1414"),
                    pattern: Hex("FFD776"), channel: PatternChannel.MaskA, strength: 15, secret: true, mastery: 30),
                // Secret « Brume du matin » (hatched in the morning, later): very pale pearl, lilac mask, softly glowing lilac rings.
                Variant("variant.raccoon.morningmist", RarityTier.Epic, Hex("E3E2E6"), Hex("F0D6DE"), Hex("F6F5F8"), Hex("B9A6CC"), Hex("1E1820"),
                    pattern: Hex("D2B6F0"), channel: PatternChannel.MaskA, strength: 15, secret: true),
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// A-SP-011 Red Panda (SmallQuadruped skeleton, prefab scaled to 0.66 m). Regions (pw_masks redpanda_masks): primary = coat,
        /// secondary = the white face (muzzle, cheek patches, brow dots) and the ears' fronts (the roster's « visage »), belly = the
        /// dark legs, underside and chest, detail = nose. Masks: A = tail rings and ear tips (the glow area), B = tail rings
        /// (readable at LOD1; every color draws them), C = two small cheek dots.
        /// </summary>
        internal static SpeciesDefinitionAsset EnsureRedPandaDefinition()
        {
            var asset = ForestSpecies("RedPanda", "species.redpanda", RigFamily.SmallQuadruped);
            asset.variants = new List<SpeciesDefinitionAsset.VariantEntry>
            {
                Variant("variant.redpanda.classic", RarityTier.Common, Hex("C8613A"), Hex("FBF6EE"), Hex("3E2C26"), Hex("2A1E1C"), Hex("1A1210"),
                    pattern: Hex("EFD7B4"), channel: PatternChannel.MaskB, strength: 14),
                Variant("variant.redpanda.golden", RarityTier.Common, Hex("D98A3E"), Hex("FBF6EE"), Hex("5A3E2E"), Hex("2A1E1C"), Hex("1A1210"),
                    pattern: Hex("F2CB72"), channel: PatternChannel.MaskB, strength: 14),
                Variant("variant.redpanda.cream", RarityTier.Uncommon, Hex("EFDDBD"), Hex("FFFDF8"), Hex("A8784C"), Hex("3A2A24"), Hex("1E1414"),
                    pattern: Hex("B98652"), channel: PatternChannel.MaskB, strength: 14),
                Variant("variant.redpanda.dark", RarityTier.Uncommon, Hex("7E3A2C"), Hex("F1DEC6"), Hex("2A2020"), Hex("1A1414"), Hex("120C0C"),
                    pattern: Hex("8A5A40"), channel: PatternChannel.MaskB, strength: 14),
                Variant("variant.redpanda.snow", RarityTier.Rare, Hex("F6F0E6"), Hex("FBE6E4"), Hex("C9C6C4"), Hex("4A3A3A"), Hex("1E1414"),
                    pattern: Hex("D5D6DA"), channel: PatternChannel.MaskB, strength: 13),
                Variant("variant.redpanda.bamboo", RarityTier.Epic, Hex("9DB87A"), Hex("EEEED0"), Hex("4A5E3A"), Hex("2A2A20"), Hex("141410"),
                    pattern: Hex("E3EBC2"), channel: PatternChannel.MaskB, strength: 14),
                Variant("variant.redpanda.rust", RarityTier.Rare, Hex("B4552E"), Hex("F6E7D2"), Hex("5A3828"), Hex("2A1E1C"), Hex("1A1210"),
                    pattern: Hex("F0D9BC"), channel: PatternChannel.MaskB, strength: 13, secret: true, mastery: 10),
                // Dark plum, mauve rings (mask B).
                Variant("variant.redpanda.blackberry", RarityTier.Epic, Hex("5E3A52"), Hex("F1E4EC"), Hex("3A2634"), Hex("1E1418"), Hex("140E12"),
                    pattern: Hex("C49ACB"), channel: PatternChannel.MaskB, strength: 15, secret: true, mastery: 20),
                // Golden orange, a golden glow on the rings and ear tips (mask A), plus the Legendary rim glow.
                Variant("variant.redpanda.goldenlantern", RarityTier.Legendary, Hex("E28F3A"), Hex("FBF4E6"), Hex("5E3A26"), Hex("2A1E1C"), Hex("1A1210"),
                    pattern: Hex("FFDE85"), channel: PatternChannel.MaskA, strength: 15, secret: true, mastery: 30),
                // Secret « Cerise » (hatched in the Forest's sunbeams, later): soft cherry red, pinkish cream face, a pink glow on the rings.
                Variant("variant.redpanda.cherry", RarityTier.Epic, Hex("C9475A"), Hex("F8E1E1"), Hex("5A2A33"), Hex("2A1A1E"), Hex("1A1012"),
                    pattern: Hex("FFC2D2"), channel: PatternChannel.MaskA, strength: 15, secret: true),
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// A-SP-010 Owl (SK_Bird, modelled at its size). Regions (pw_masks owl rule): primary = plumage (head, back, wings),
        /// secondary = the sculpted face discs, belly = chest and belly, detail = beak and feet. Masks: A = wing tips and the rims
        /// of the face discs (the glow area), B = light bands across the wings, C (uv1.x) = small specks on the belly, and the
        /// Constellation's little round five-point stars (doc « G »). A bird has no ears: no ear socket (doc 03).
        /// </summary>
        internal static SpeciesDefinitionAsset EnsureOwlDefinition()
        {
            var asset = ForestSpecies("Owl", "species.owl", RigFamily.Bird);
            asset.hiddenSockets = new List<AccessorySocket> { AccessorySocket.EarL, AccessorySocket.EarR };
            asset.variants = new List<SpeciesDefinitionAsset.VariantEntry>
            {
                // The roster colors (doc « R + A »): a soft lighter edge on the wing tips.
                Variant("variant.owl.brown", RarityTier.Common, Hex("8A6446"), Hex("E3CDB0"), Hex("F3E7D3"), Hex("D29A4A"), Hex("1E1414"),
                    pattern: Hex("EAD6B8"), channel: PatternChannel.MaskA, strength: 10),
                Variant("variant.owl.tawny", RarityTier.Common, Hex("C27A44"), Hex("F3E3C8"), Hex("F6EAD4"), Hex("D9A048"), Hex("1E1414"),
                    pattern: Hex("F2DDBE"), channel: PatternChannel.MaskA, strength: 10),
                Variant("variant.owl.grey", RarityTier.Uncommon, Hex("B7B5B3"), Hex("F2EFE8"), Hex("FFFFFF"), Hex("8E8C8E"), Hex("1A1618"),
                    pattern: Hex("EEEDEA"), channel: PatternChannel.MaskA, strength: 10),
                Variant("variant.owl.cream", RarityTier.Uncommon, Hex("EEDFC4"), Hex("FFFDF8"), Hex("FFFFFF"), Hex("C08A52"), Hex("1E1414"),
                    pattern: Hex("FFF8EC"), channel: PatternChannel.MaskA, strength: 10),
                Variant("variant.owl.dark", RarityTier.Rare, Hex("4A3A32"), Hex("8A7E76"), Hex("D9C8AE"), Hex("8C8A8C"), Hex("120C0C"),
                    pattern: Hex("7A685C"), channel: PatternChannel.MaskA, strength: 10),
                // Night blue, lilac discs, blue grey belly, light points drawn as little ROUND five-point stars (mask C) softly
                // glowing at full strength.
                Variant("variant.owl.constellation", RarityTier.Epic, Hex("2E3A5E"), Hex("C9B6E4"), Hex("8E9AB4"), Hex("B8A9CC"), Hex("12121A"),
                    pattern: Hex("F4F0FF"), channel: PatternChannel.MaskD, strength: 15),
                Variant("variant.owl.cocoa", RarityTier.Rare, Hex("7A5544"), Hex("F3E6CF"), Hex("E6D3B5"), Hex("C99A5E"), Hex("1E1414"), secret: true, mastery: 10),
                // Periwinkle blue, lilac discs, a white speckled belly (mask C).
                Variant("variant.owl.periwinkle", RarityTier.Epic, Hex("8E9BD8"), Hex("D4C2EC"), Hex("F8F7FF"), Hex("9A8FB8"), Hex("161420"),
                    pattern: Hex("5E6AB8"), channel: PatternChannel.MaskC, strength: 13, secret: true, mastery: 20),
                // Honey brown, a golden glow on the wing tips (mask A), plus the Legendary rim glow.
                Variant("variant.owl.honeywings", RarityTier.Legendary, Hex("C8924A"), Hex("F4DDB4"), Hex("F6E8CC"), Hex("B8803E"), Hex("1E1414"),
                    pattern: Hex("FFD978"), channel: PatternChannel.MaskA, strength: 15, secret: true, mastery: 30),
                // Secret « Pleine lune » (hatched at night, later): pale cream, silver white discs, a soft silver glow (mask A).
                Variant("variant.owl.fullmoon", RarityTier.Epic, Hex("F1E8D6"), Hex("F4F6FA"), Hex("FBF8F0"), Hex("D8C6A0"), Hex("1E1820"),
                    pattern: Hex("E8EEF8"), channel: PatternChannel.MaskA, strength: 15, secret: true),
                // T52 season visitor « Chouette rousse » (season 3, Récolte; visitor.russet_owl): warm russet plumage, pale peach
                // discs, lighter russet wing tips (mask A). Palette only, secret: never in eggs, pities or genetics.
                Variant("variant.owl.russet_visitor", RarityTier.Rare, Hex("B8562E"), Hex("F0D2B4"), Hex("F4E2C8"), Hex("D49A50"), Hex("1E1414"),
                    pattern: Hex("F2C79A"), channel: PatternChannel.MaskA, strength: 12, secret: true, visitor: true),
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// A-SP-012 Boar Piglet (SK_HeavyQuadruped, modelled at its size; no tusks, no separate snout material). Regions (pw_masks
        /// boarpiglet rule): primary = coat, secondary = inner ears and the muzzle around the snout, belly = underside, detail =
        /// the snout disc and hooves. Masks: A = snout tip and tail tuft (the glow area), B = the light stripes along the back
        /// and flanks (doc « rayures par masque »), C (uv1.x) = little acorn-shaped spots on the back (doc « G »).
        /// </summary>
        internal static SpeciesDefinitionAsset EnsureBoarPigletDefinition()
        {
            var asset = ForestSpecies("BoarPiglet", "species.boarpiglet", RigFamily.HeavyQuadruped);
            asset.variants = new List<SpeciesDefinitionAsset.VariantEntry>
            {
                // The plain roster colors (doc « R + A »): a deeper pink snout tip and tail tuft.
                Variant("variant.boarpiglet.brown", RarityTier.Common, Hex("8A5E44"), Hex("E6C2B0"), Hex("E9D6BE"), Hex("C9707E"), Hex("1E1414"),
                    pattern: Hex("B9606E"), channel: PatternChannel.MaskA, strength: 9),
                Variant("variant.boarpiglet.chestnutstripe", RarityTier.Common, Hex("9A5A36"), Hex("E8C4B2"), Hex("F4E6CF"), Hex("D08690"), Hex("1E1414"),
                    pattern: Hex("F2E2C4"), channel: PatternChannel.MaskB, strength: 14),
                Variant("variant.boarpiglet.grey", RarityTier.Uncommon, Hex("9E9A98"), Hex("DCC2C2"), Hex("D6D3D0"), Hex("C48E98"), Hex("1A1618"),
                    pattern: Hex("B07E88"), channel: PatternChannel.MaskA, strength: 9),
                Variant("variant.boarpiglet.black", RarityTier.Uncommon, Hex("544C4C"), Hex("C9A3A6"), Hex("66605F"), Hex("B8949A"), Hex("0E0A0A"),
                    pattern: Hex("A08088"), channel: PatternChannel.MaskA, strength: 9),
                Variant("variant.boarpiglet.cream", RarityTier.Rare, Hex("F0E2CA"), Hex("F6D2D2"), Hex("FFFDF8"), Hex("EE9CAA"), Hex("1E1414"),
                    pattern: Hex("E58A9A"), channel: PatternChannel.MaskA, strength: 9),
                // Acorn brown, tender green ears and muzzle, little acorn-shaped spots (mask C).
                Variant("variant.boarpiglet.acorn", RarityTier.Epic, Hex("8E6238"), Hex("EED8B8"), Hex("F4E8D0"), Hex("D99098"), Hex("1E1414"),
                    pattern: Hex("E4EFC4"), channel: PatternChannel.MaskC, strength: 14),
                Variant("variant.boarpiglet.walnut", RarityTier.Rare, Hex("6E4A32"), Hex("E0B8A8"), Hex("E6D2B8"), Hex("B86A78"), Hex("1E1414"), secret: true, mastery: 10),
                // Powder pink, fine white stripes (mask B), pinkish cream belly.
                Variant("variant.boarpiglet.powderpink", RarityTier.Epic, Hex("EDC2C8"), Hex("F8DCDC"), Hex("FBEFEA"), Hex("E8939F"), Hex("1E1414"),
                    pattern: Hex("FFFFFF"), channel: PatternChannel.MaskB, strength: 13, secret: true, mastery: 20),
                // Golden brown, a golden glow on the snout and the tail (mask A), plus the Legendary rim glow.
                Variant("variant.boarpiglet.goldentruffle", RarityTier.Legendary, Hex("B98545"), Hex("F0D4B4"), Hex("F4E4C6"), Hex("E0A07E"), Hex("1E1414"),
                    pattern: Hex("FFD878"), channel: PatternChannel.MaskA, strength: 15, secret: true, mastery: 30),
                // Secret « Rayé menthe » (breeding Chestnut Stripe x Cream later): cream, pink snout, softly glowing mint stripes.
                Variant("variant.boarpiglet.mintstripe", RarityTier.Epic, Hex("F2E8D2"), Hex("F6D6D6"), Hex("FFFDF6"), Hex("EE9CAA"), Hex("1E1414"),
                    pattern: Hex("B4EBCF"), channel: PatternChannel.MaskB, strength: 15, secret: true),
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>T51 and T52: the Forest species definitions only (palettes, variants, visitors), without models or prefabs.</summary>
        [MenuItem("Pocket Wilds/Animals/Update Forest Species Definitions (T51)", priority = 159)]
        public static void UpdateForestDefinitions()
        {
            foreach (var ensure in new Func<SpeciesDefinitionAsset>[] { EnsureFoxDefinition, EnsureBadgerDefinition, EnsureRaccoonDefinition, EnsureRedPandaDefinition,
                         EnsureOwlDefinition, EnsureBoarPigletDefinition })
                EnsureContentDatabase(ensure());
            AssetDatabase.SaveAssets();
        }

        private static SpeciesDefinitionAsset.VariantEntry Variant(string id, RarityTier rarity, Color primary, Color secondary,
            Color belly, Color detail, Color eye, Color? pattern = null, PatternChannel channel = PatternChannel.None, int strength = 0, bool secret = false, bool visitor = false, int mastery = 0,
            int module = 0)
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
                moduleSlot = module,
            };
        }

        private static Color CarriedPatternTint(Color primary) => Color.Lerp(primary, new Color(0.24f, 0.15f, 0.11f), 0.3f);

        /// <summary>Rewrites the species definitions only (palettes, variants), without rebuilding prefabs.</summary>
        [MenuItem("Pocket Wilds/Animals/Update Species Definitions", priority = 160)]
        public static void UpdateSpeciesDefinitions()
        {
            foreach (var ensure in new Func<SpeciesDefinitionAsset>[] { EnsureRabbitDefinition, EnsureSquirrelDefinition, EnsureHedgehogDefinition,
                EnsureFieldMouseDefinition, EnsureGuineaPigDefinition, EnsureFawnDefinition, EnsureFieldHamsterDefinition, EnsureMoleDefinition,
                EnsureFoxDefinition, EnsureBadgerDefinition, EnsureRaccoonDefinition, EnsureRedPandaDefinition, EnsureOwlDefinition, EnsureBoarPigletDefinition })
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
