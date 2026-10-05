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
    /// T23 in the real scene: rabbits and squirrels share the meadow, the visible budget and the scheduler;
    /// both species are pooled ahead of time so a squirrel hatch never instantiates.
    /// </summary>
    public class SquirrelPlayTests
    {
        private static readonly ContentId Rabbit = ContentId.FromSource("species.rabbit");
        private static readonly ContentId Squirrel = ContentId.FromSource("species.squirrel");
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
                (Rabbit, "variant.rabbit.white", RarityTier.Common), (Squirrel, "variant.squirrel.autumn", RarityTier.Epic),
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

        [UnityTest]
        public IEnumerator Meadow_ShowsBothSpecies_AndSquirrelHatchesComeFromThePool()
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
            float timeout = Time.realtimeSinceStartup + 20f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            yield return null;
            var context = bootstrap.Context;
            var sanctuary = Object.FindAnyObjectByType<VerticalSliceEntry>().Sanctuary;
            Assert.AreEqual(4, sanctuary.VisibleAnimals);

            int rabbits = 0, squirrels = 0;
            foreach (var view in Object.FindObjectsByType<AnimalView>(FindObjectsSortMode.None))
            {
                if (!view.gameObject.activeInHierarchy)
                    continue;
                if (view.Species == Rabbit) rabbits++;
                if (view.Species == Squirrel) squirrels++;
            }
            Assert.AreEqual(2, rabbits);
            Assert.AreEqual(2, squirrels, "Saved squirrels come back with their own prefab.");

            // Both species of the egg are warmed side by side.
            for (int i = 0; i < 90 && sanctuary.PooledSpares < 30; i++)
                yield return null;
            Assert.GreaterOrEqual(sanctuary.PooledSparesOf(Rabbit), 30);
            Assert.GreaterOrEqual(sanctuary.PooledSparesOf(Squirrel), 30);
            for (int i = 0; i < 120 && sanctuary.PooledSpares < 47; i++)
                yield return null;
            Assert.GreaterOrEqual(sanctuary.PooledSpares, 47, "Budget 50 + 1 minus 4 visible, for each species.");
            // The warmup tasks make their last check on the next frame: let them end first.
            yield return null;
            yield return null;
            int viewsBefore = Object.FindObjectsByType<AnimalView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            // Hatch until a squirrel comes out (two species, pity on nothing: a few eggs at most).
            int hatchedSquirrels = 0;
            for (int hatch = 0; hatch < 12 && hatchedSquirrels == 0; hatch++)
            {
                context.Economy.Grant(CurrencyId.Light, context.Hatch.GetCost(Basic), EconomyReason.Reward);
                sanctuary.RequestHatch();
                sanctuary.RequestHatch(); // skip the staging
                if (context.State.Collection[context.State.Collection.Count - 1].Species == Squirrel)
                    hatchedSquirrels++;
                float until = Time.realtimeSinceStartup + 6f;
                while (sanctuary.Hatch.IsPlaying || sanctuary.WaitingEgg == null)
                {
                    Assert.Less(Time.realtimeSinceStartup, until, "Hatch or next egg stuck.");
                    yield return null;
                }
            }
            Assert.AreEqual(1, hatchedSquirrels, "A squirrel hatched from the grassland egg.");
            int viewsAfter = Object.FindObjectsByType<AnimalView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Assert.AreEqual(viewsBefore, viewsAfter, "Hatching reused pooled views: no Instantiate.");
            Assert.AreEqual(context.State.Collection.Count, sanctuary.VisibleAnimals);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
