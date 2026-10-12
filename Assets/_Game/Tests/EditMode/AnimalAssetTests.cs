using System;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Import;
using PocketWilds.Editor.Validation;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>Gate checks that run in CI: every animal prefab passes the AssetValidator, content builds cleanly.</summary>
    public class AnimalAssetTests
    {
        [Test]
        public void AllAnimalPrefabs_PassAssetValidator()
        {
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { AssetValidator.AnimalPrefabFolder });
            Assert.IsNotEmpty(prefabs, "No animal prefab found.");
            var errors = AssetValidator.ValidateAllAnimals().Where(i => i.Severity == IssueSeverity.Error).ToList();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void ContentDatabase_BuildsWithoutErrors_AndRabbitHasSixVariants()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            Assert.IsNotNull(database);
            var builder = database.CreateBuilder();
            Assert.IsFalse(builder.HasErrors, string.Join("; ", builder.Issues));
            var registry = builder.Build();
            var rabbit = ContentId.FromSource("species.rabbit");
            Assert.IsTrue(registry.Species.TryGet(rabbit, out var species));
            Assert.AreEqual("PF_Animal_Rabbit", species.VisualKey);
            int variants = 0;
            for (int i = 0; i < registry.Variants.Count; i++)
                if (registry.Variants[i].Species == rabbit && !registry.Variants[i].IsSecret) variants++;
            Assert.AreEqual(6, variants, "the six roster colors (visitor and mastery colors are secret)");
        }

        [Test]
        public void VisualCatalog_ResolvesEveryShippedSpecies()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            Assert.IsNotNull(catalog);
            foreach (var species in database.species)
            {
                Assert.IsTrue(catalog.TryGet(species.visualKey, out var entry), species.visualKey);
                Assert.IsNotNull(entry.prefab, species.visualKey);
            }
        }

        /// <summary>The family table of AnimalPipeline: every family controller has these states.</summary>
        private static readonly string[] FamilyStates = { "Idle", "IdleLook", "Move", "Happy", "React", "SleepEnter", "Sleep", "Wake", "Eat", "Social", "Inspect" };

        /// <summary>
        /// T52 « famille DONE »: a library imported with its loops and compression, and a controller playing the family's own
        /// clips; the heavy family walks (a waddle), the birds hop on the ground.
        /// </summary>
        [TestCase("SmallQuadruped", "Locomotion_Hop")]
        [TestCase("MediumQuadruped", "Locomotion_Walk")]
        [TestCase("HeavyQuadruped", "Locomotion_Walk")]
        [TestCase("Bird", "Locomotion_Hop")]
        public void FamilyController_PlaysItsOwnLibrary(string family, string move)
        {
            CollectionAssert.Contains(AnimalPipeline.Families, family);
            Assert.AreEqual(move, AnimalPipeline.MoveClip(family));
            var importer = AssetImporter.GetAtPath(AnimalPipeline.ClipsPath(family)) as ModelImporter;
            Assert.IsNotNull(importer, AnimalPipeline.ClipsPath(family) + ": export the family library (pw_export.export_animation_library).");
            Assert.AreEqual(ModelImporterAnimationCompression.Optimal, importer.animationCompression);
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType);
            Assert.IsNotEmpty(importer.clipAnimations);
            foreach (var clip in importer.clipAnimations)
            {
                StringAssert.StartsWith($"AN_{family}_", clip.name, "a library holds its family's clips only");
                Assert.AreEqual(AssetImportRules.IsLooping(clip.name), clip.loopTime, clip.name + ": loops and one-shots (AssetImportRules)");
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimalPipeline.ControllerPath(family));
            Assert.IsNotNull(controller, AnimalPipeline.ControllerPath(family) + ": run a species build of the family.");
            var machine = controller.layers[0].stateMachine;
            Assert.AreEqual("Idle", machine.defaultState.name);
            var states = machine.states.Select(s => s.state).ToDictionary(s => s.name);
            foreach (var name in FamilyStates)
            {
                Assert.IsTrue(states.TryGetValue(name, out var state), family + ": state " + name);
                Assert.IsNotNull(state.motion, family + " " + name);
                StringAssert.StartsWith($"AN_{family}_", state.motion.name, family + " " + name);
            }
            Assert.AreEqual($"AN_{family}_{move}", states["Move"].motion.name);
            Assert.AreEqual($"AN_{family}_Sleep_Loop", states["Sleep"].motion.name);
        }

        /// <summary>A pose plays for a family exactly when the family library has its clip (T52: PeekOut is optional).</summary>
        [TestCaseSource(typeof(AnimalPipeline), nameof(AnimalPipeline.Families))]
        public void PoseClips_ListAFamily_OnlyWhenItsLibraryHasTheClip(string family)
        {
            var rig = (RigFamily)Enum.Parse(typeof(RigFamily), family);
            var clips = AssetDatabase.LoadAllAssetsAtPath(AnimalPipeline.ClipsPath(family)).OfType<AnimationClip>().Select(c => c.name).ToList();
            Assert.IsNotEmpty(clips, AnimalPipeline.ClipsPath(family));
            foreach (var pose in PoseClips.All)
                Assert.AreEqual(clips.Contains(PoseClips.ClipName(pose, rig)), PoseClips.TryGet(pose.Key, rig, out _),
                    $"{pose.Key} on {family}: PoseClips.All lists the families whose library has {PoseClips.ClipName(pose, rig)}");
        }

        /// <summary>
        /// Accessories follow the sockets only when the import exposes them (optimized hierarchy): every species model has each
        /// socket path of AssetImportRules.FamilySockets for its skeleton, except the sockets it hides (the owl's ears).
        /// </summary>
        [Test]
        public void SpeciesModels_ExposeTheirFamilySockets()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            foreach (var species in database.species.Where(s => s != null))
            {
                string name = species.visualKey.Substring("PF_Animal_".Length);
                var model = AssetImporter.GetAtPath(AnimalPipeline.ModelPath(name)) as ModelImporter;
                Assert.IsNotNull(model, AnimalPipeline.ModelPath(name));
                string root = "SK_" + species.rigFamily;
                Assert.IsTrue(AssetImportRules.FamilySockets.TryGetValue(root, out var sockets), root + ": AssetImportRules.FamilySockets row");
                var paths = model.transformPaths;
                foreach (var path in sockets)
                {
                    string socketName = path.Substring(path.LastIndexOf('/') + 1);
                    var socket = (AccessorySocket)Enum.Parse(typeof(AccessorySocket), socketName.Substring("SOCK_".Length).Replace("_", ""));
                    if (species.hiddenSockets.Contains(socket))
                        continue;
                    CollectionAssert.Contains(paths, path, name + ": socket path (pw_rig socket parent)");
                    CollectionAssert.Contains(model.extraExposedTransformPaths, path, name + ": exposed at import");
                }
            }
        }

        [TestCase(0, PatternChannel.None, 0, 0)]
        [TestCase(1023, PatternChannel.MaskD, 15, 15)]
        [TestCase(37, PatternChannel.MaskA, 9, 3)]
        public void VisualPacking_RoundTrips(int row, PatternChannel pattern, int strength, int rare)
        {
            uint packed = AnimalVisualPacking.Pack(row, pattern, strength, rare);
            Assert.AreEqual(row, AnimalVisualPacking.PaletteRow(packed));
            Assert.AreEqual(pattern, AnimalVisualPacking.Pattern(packed));
            Assert.AreEqual(strength, AnimalVisualPacking.PatternStrength(packed));
            Assert.AreEqual(rare, AnimalVisualPacking.RareIntensity(packed));
        }

        [Test]
        public void VariantWithWrongPaletteSize_IsRejected()
        {
            var builder = new ContentRegistryBuilder();
            TestUtil.AddSpecies(builder, "species.rabbit", 1.0);
            builder.AddVariant(new VariantDefinition(ContentId.FromSource("variant.rabbit.bad"), ContentId.FromSource("species.rabbit"),
                "x", RarityTier.Common, new Color32[3], PatternChannel.None, 0), "variant.rabbit.bad");
            Assert.IsTrue(builder.HasErrors);
        }
    }
}
