using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T23 and T24 in the real scene: rabbits, squirrels and hedgehogs share the meadow, the visible budget
    /// and the scheduler; every species of the egg is pooled ahead of time so no hatch instantiates.
    /// </summary>
    public class SpeciesMeadowPlayTests
    {
        private static readonly ContentId Rabbit = ContentId.FromSource("species.rabbit");
        private static readonly ContentId Squirrel = ContentId.FromSource("species.squirrel");
        private static readonly ContentId Hedgehog = ContentId.FromSource("species.hedgehog");
        private static readonly ContentId Basic = ContentId.FromSource("egg.grassland.basic");
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "PocketWildsPlayTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var bootstrap in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(bootstrap.gameObject);
            try
            {
                Directory.Delete(_saveDir, true);
            }
            catch (IOException)
            {
            }
        }

        private void PrepareSave()
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            var animals = new[]
            {
                (Rabbit, "variant.rabbit.brown", RarityTier.Common), (Squirrel, "variant.squirrel.red", RarityTier.Common),
                (Hedgehog, "variant.hedgehog.brown", RarityTier.Common), (Rabbit, "variant.rabbit.white", RarityTier.Common),
                (Squirrel, "variant.squirrel.autumn", RarityTier.Epic), (Hedgehog, "variant.hedgehog.mushroom", RarityTier.Epic),
            };
            for (int i = 0; i < animals.Length; i++)
            {
                var (species, variant, rarity) = animals[i];
                state.Collection.Add(new AnimalInstance
                {
                    Species = species, Variant = ContentId.FromSource(variant), Rarity = rarity,
                    Seed = SeedHash.Mix((uint)i + 1u, 0x5C0122E1u),
                });
                state.Collection.DiscoveredSpecies.Add(species);
                state.Collection.DiscoveredVariants.Add(ContentId.FromSource(variant));
            }
            state.Codex.SeenSpecies.UnionWith(state.Collection.DiscoveredSpecies);
            state.Codex.SeenVariants.UnionWith(state.Collection.DiscoveredVariants);
            Assert.IsTrue(service.FlushNow());
            service.Dispose();
        }

        private static int CountVisible(ContentId species)
        {
            int count = 0;
            foreach (var view in Object.FindObjectsByType<AnimalView>(FindObjectsSortMode.None))
                if (view.gameObject.activeInHierarchy && view.Species == species)
                    count++;
            return count;
        }

        [UnityTest]
        public IEnumerator Meadow_ShowsEverySpecies_AndHatchesComeFromThePool()
        {
#if UNITY_EDITOR
            PrepareSave();
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = _saveDir,
                ContentOverride = database.CreateBuilder().Build(),
                VisualCatalogOverride = catalog,
                LocalizationOverride = database.localization.ToArray(),
                LoadTargetScene = true,
            };
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            // The refuge scene and its Addressables load cold when this is the run's first boot (as ActivityFlowPlayTests).
            float timeout = Time.realtimeSinceStartup + 120f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            yield return null;
            var context = bootstrap.Context;
            var refuge = Object.FindAnyObjectByType<VerticalSliceEntry>().Refuge;
            Assert.AreEqual(6, refuge.VisibleAnimals);
            Assert.AreEqual(2, CountVisible(Rabbit));
            Assert.AreEqual(2, CountVisible(Squirrel), "Saved squirrels come back with their own prefab.");
            Assert.AreEqual(2, CountVisible(Hedgehog), "Saved hedgehogs come back with their own prefab.");

            // Every species of the egg is warmed side by side: budget 50 + 1 minus 6 visible each.
            for (int i = 0; i < 120 && refuge.PooledSpares < 45; i++)
                yield return null;
            foreach (var species in new[] { Rabbit, Squirrel, Hedgehog })
                Assert.GreaterOrEqual(refuge.PooledSparesOf(species), 45, species.ToString());
            // The warmup tasks make their last check on the next frame: let them end first.
            yield return null;
            yield return null;
            int viewsBefore = RefugeViewCount();

            // Hatch until a squirrel and a hedgehog came out of the grassland egg.
            bool squirrel = false, hedgehog = false;
            var log = new System.Text.StringBuilder();
            for (int hatch = 0; hatch < 30 && !(squirrel && hedgehog); hatch++)
            {
                context.Economy.Grant(CurrencyId.Light, context.Hatch.GetCost(Basic), EconomyReason.Reward);
                int before = context.State.Collection.Count;
                refuge.RequestHatch();
                refuge.RequestHatch(); // skip the staging
                var hatched = context.State.Collection[context.State.Collection.Count - 1].Species;
                log.Append(context.State.Collection.Count > before ? hatched.ToString() : $"(no hatch, modal {refuge.ModalOpen})").Append(' ');
                squirrel |= hatched == Squirrel;
                hedgehog |= hatched == Hedgehog;
                float until = Time.realtimeSinceStartup + 6f;
                while (refuge.Hatch.IsPlaying || refuge.WaitingEgg == null)
                {
                    Assert.Less(Time.realtimeSinceStartup, until, "Hatch or next egg stuck.");
                    yield return null;
                }
            }
            Assert.IsTrue(squirrel && hedgehog, "Both new species hatch from the grassland egg: " + log);
            int viewsAfter = RefugeViewCount();
            Assert.AreEqual(viewsBefore, viewsAfter, "Hatching reused pooled views: no Instantiate.");
            Assert.AreEqual(context.State.Collection.Count, refuge.VisibleAnimals);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        /// <summary>The refuge's animal views: the journal's portrait stage (T44C) makes its own view once per species.</summary>
        private static int RefugeViewCount()
        {
            int count = 0;
            foreach (var view in Object.FindObjectsByType<AnimalView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!InPortraitStage(view.transform))
                    count++;
            return count;
        }

        private static bool InPortraitStage(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name == "JournalPortraitStage")
                    return true;
            return false;
        }
    }
}
