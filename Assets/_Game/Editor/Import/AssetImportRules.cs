using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Import
{
    /// <summary>
    /// Import presets as code, chosen by folder (Asset Production Guide: Animal_Rigged, Animation_Library,
    /// Environment_Static, Prop_Static). Nothing depends on a hand-ticked importer checkbox.
    /// Bump <see cref="Version"/> when a rule changes so Unity reimports affected assets.
    /// </summary>
    public sealed class AssetImportRules : AssetPostprocessor
    {
        private const uint Version = 6;

        public const string AnimalsRoot = "Assets/_Game/Art/Animals/";
        public const string AnimationsRoot = "Assets/_Game/Art/Animations/";
        public const string EnvironmentRoot = "Assets/_Game/Art/Environment/";
        public const string PropsRoot = "Assets/_Game/Art/Props/";
        public const string EggsRoot = "Assets/_Game/Art/Eggs/";
        /// <summary>T49: an event's own props (decorations, monument): static props like the environment's.</summary>
        public const string EventsRoot = "Assets/_Game/Art/Events/";

        /// <summary>Sockets exposed when GameObjects are optimized, per rig family. Paths are below the model root.</summary>
        public static readonly Dictionary<string, string[]> FamilySockets = new Dictionary<string, string[]>
        {
            ["SK_SmallQuadruped"] = QuadrupedSockets("SK_SmallQuadruped"),
            // T46: same bone names and socket parents (pw_rig), another root.
            ["SK_MediumQuadruped"] = QuadrupedSockets("SK_MediumQuadruped"),
        };

        private static string[] QuadrupedSockets(string root) => new[]
        {
            root + "/Root/Pelvis/Spine/Chest/Neck/Head/SOCK_HeadTop",
            root + "/Root/Pelvis/Spine/Chest/Neck/SOCK_Neck",
            root + "/Root/Pelvis/Spine/SOCK_Back",
            root + "/Root/Pelvis/Tail_01/SOCK_TailBase",
            root + "/Root/Pelvis/Spine/Chest/Neck/Head/Ear_L_01/SOCK_Ear_L",
            root + "/Root/Pelvis/Spine/Chest/Neck/Head/Ear_R_01/SOCK_Ear_R",
        };

        // T49: the Mole's own Move and Eat clips loop like the family Locomotion and Eat_Loop they replace.
        public static readonly string[] LoopingClipMarkers = { "_Loop", "_Idle_A", "Locomotion_", "_MoleWalk", "_MoleEat" };

        public override uint GetVersion() => Version;

        private void OnPreprocessModel()
        {
            var importer = (ModelImporter)assetImporter;
            if (assetPath.StartsWith(AnimalsRoot, StringComparison.Ordinal) && assetPath.Contains("/Models/"))
                ApplyAnimalRigged(importer);
            else if (assetPath.StartsWith(AnimationsRoot, StringComparison.Ordinal))
                ApplyAnimationLibrary(importer);
            else if (assetPath.StartsWith(EnvironmentRoot, StringComparison.Ordinal) || assetPath.StartsWith(PropsRoot, StringComparison.Ordinal) || assetPath.StartsWith(EggsRoot, StringComparison.Ordinal)
                || assetPath.StartsWith(EventsRoot, StringComparison.Ordinal))
                ApplyStatic(importer);
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(AnimationsRoot, StringComparison.Ordinal))
                return;

            var importer = (ModelImporter)assetImporter;
            var defaults = importer.defaultClipAnimations;
            var clips = new List<ModelImporterClipAnimation>(defaults.Length);
            foreach (var source in defaults)
            {
                // Blender names takes "Armature|Action". Keep the action name only.
                string name = source.name;
                int bar = name.LastIndexOf('|');
                if (bar >= 0)
                    name = name.Substring(bar + 1);
                if (!name.StartsWith("AN_", StringComparison.Ordinal))
                    continue;

                source.name = name;
                source.loopTime = IsLooping(name);
                source.loopPose = false;
                source.lockRootRotation = false;
                source.lockRootHeightY = false;
                source.lockRootPositionXZ = false;
                source.keepOriginalOrientation = true;
                source.keepOriginalPositionY = true;
                source.keepOriginalPositionXZ = true;
                clips.Add(source);
            }
            importer.clipAnimations = clips.ToArray();
        }

        /// <summary>
        /// Family clips keep bone rotations only, plus Root position for the in-place bounce.
        /// Position and scale curves would force the reference species' bone offsets onto every
        /// other species of the family, so they are stripped at import. One exception (T47): the Head
        /// bone's scale where it leaves 1 (the Field Hamster's cheek squash); a scale is relative to the
        /// rest pose, so it fits every species, and curves that stay at 1 are dropped.
        /// </summary>
        private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!assetPath.StartsWith(AnimationsRoot, StringComparison.Ordinal))
                return;

            // The three axes of the Head's scale stay together: one axis left alone would not mean « at rest ».
            bool headScale = false;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type == typeof(Transform) && IsHeadScale(binding) && LeavesOne(AnimationUtility.GetEditorCurve(clip, binding)))
                    headScale = true;
            }

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform))
                    continue;
                bool isRoot = binding.path.EndsWith("/Root", StringComparison.Ordinal) || binding.path == "Root";
                bool isBone = binding.path.Contains("/");
                bool rotation = binding.propertyName.StartsWith("m_LocalRotation", StringComparison.Ordinal)
                                || binding.propertyName.StartsWith("localEuler", StringComparison.Ordinal);
                bool keep = isBone && (rotation || (isRoot && binding.propertyName.StartsWith("m_LocalPosition", StringComparison.Ordinal)));
                if (!keep && headScale && IsHeadScale(binding))
                    keep = true;
                if (!keep)
                    AnimationUtility.SetEditorCurve(clip, binding, null);
            }
        }

        private static bool IsHeadScale(EditorCurveBinding binding) =>
            binding.path.EndsWith("/Head", StringComparison.Ordinal) && binding.propertyName.StartsWith("m_LocalScale", StringComparison.Ordinal);

        private static bool LeavesOne(AnimationCurve curve)
        {
            if (curve == null)
                return false;
            foreach (var key in curve.keys)
            {
                if (Mathf.Abs(key.value - 1f) > 1e-4f)
                    return true;
            }
            return false;
        }

        public static bool IsLooping(string clipName)
        {
            foreach (var marker in LoopingClipMarkers)
            {
                if (clipName.Contains(marker))
                    return true;
            }
            return false;
        }

        private static void ApplyAnimalRigged(ModelImporter importer)
        {
            // Bone paths must match the animation library: never collapse the armature node.
            importer.preserveHierarchy = true;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.weldVertices = true;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.skinWeights = ModelImporterSkinWeights.Standard;
            importer.optimizeGameObjects = true;
            var exposed = new List<string>();
            foreach (var pair in FamilySockets)
                exposed.AddRange(pair.Value);
            importer.extraExposedTransformPaths = exposed.ToArray();
        }

        private static void ApplyAnimationLibrary(ModelImporter importer)
        {
            // The library FBX holds only the armature; keep it as a node so clip paths start with SK_<Family>.
            importer.preserveHierarchy = true;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.isReadable = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importConstraints = false;
            importer.importAnimatedCustomProperties = false;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.animationRotationError = 0.5f;
            importer.animationPositionError = 0.5f;
            importer.animationScaleError = 0.5f;
            importer.motionNodeName = string.Empty;
        }

        private static void ApplyStatic(ModelImporter importer)
        {
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
        }
    }
}
