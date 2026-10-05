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
    /// T25 in the real scene: two animals greet nose to nose with the family Social clip on their own
    /// Animators, and the start chunk feeds its points of interest to the scheduler.
    /// </summary>
    public class SocialPlayTests
    {
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
            var animals = new[] { ("species.rabbit", "variant.rabbit.brown"), ("species.hedgehog", "variant.hedgehog.brown"),
                ("species.squirrel", "variant.squirrel.red"), ("species.rabbit", "variant.rabbit.white") };
            for (int i = 0; i < animals.Length; i++)
            {
                var species = ContentId.FromSource(animals[i].Item1);
                var variant = ContentId.FromSource(animals[i].Item2);
                state.Collection.Add(new AnimalInstance { Species = species, Variant = variant, Rarity = RarityTier.Common, Seed = SeedHash.Mix((uint)i + 7u, 0x50C1A1u) });
                state.Collection.DiscoveredSpecies.Add(species);
                state.Collection.DiscoveredVariants.Add(variant);
            }
            state.Codex.SeenSpecies.UnionWith(state.Collection.DiscoveredSpecies);
            state.Codex.SeenVariants.UnionWith(state.Collection.DiscoveredVariants);
            Assert.IsTrue(service.FlushNow());
            service.Dispose();
        }

        private static AnimalView ViewOf(AnimalScheduler scheduler, int handle)
        {
            uint id = scheduler.GetInstanceId(handle);
            foreach (var view in Object.FindObjectsByType<AnimalView>(FindObjectsSortMode.None))
                if (view.OwnerInstanceId == id)
                    return view;
            return null;
        }

        private static bool PlaysSocial(AnimalView view)
        {
            var animator = view.Animator;
            return animator.GetCurrentAnimatorStateInfo(0).shortNameHash == AnimalStateHashes.Social
                   || animator.GetNextAnimatorStateInfo(0).shortNameHash == AnimalStateHashes.Social;
        }

        [UnityTest]
        public IEnumerator TwoAnimals_GreetNoseToNose_WithTheFamilyClip()
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
            yield return null;
            yield return null;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var refuge = entry.Refuge;
            var scheduler = refuge.Scheduler;
            Assert.AreEqual(entry.StartChunk.InterestCount, scheduler.InterestCount, "The chunk's points of interest reach the scheduler.");
            Assert.Greater(scheduler.InterestCount, 0);

            // Animals are spread over the meadow: any of them that has a free neighbour within reach invites it.
            int a = -1;
            float searchUntil = Time.realtimeSinceStartup + 6f;
            while (a < 0)
            {
                for (int h = 0; h < scheduler.Capacity && a < 0; h++)
                    if (scheduler.IsValid(h) && scheduler.GetPartner(h) < 0 && scheduler.TryStartSocial(h, refuge.SimulationTime))
                        a = h;
                if (a >= 0)
                    break;
                Assert.Less(Time.realtimeSinceStartup, searchUntil, "No animal ever found a neighbour to greet.");
                yield return null;
            }
            int b = scheduler.GetPartner(a);
            Assert.GreaterOrEqual(b, 0);

            float until = Time.realtimeSinceStartup + 10f;
            while (scheduler.GetState(a) != AnimalBehaviorState.Social)
            {
                Assert.Less(Time.realtimeSinceStartup, until, "The pair never met.");
                yield return null;
            }
            Assert.AreEqual(AnimalBehaviorState.Social, scheduler.GetState(b));
            // Let the crossfade and the last few degrees of turning finish.
            for (int i = 0; i < 12; i++)
                yield return null;

            var viewA = ViewOf(scheduler, a);
            var viewB = ViewOf(scheduler, b);
            Assert.IsTrue(PlaysSocial(viewA) && PlaysSocial(viewB), "Both Animators play the family Social clip.");
            var toB = viewB.transform.position - viewA.transform.position;
            toB.y = 0f;
            Assert.Less(Vector3.Angle(viewA.transform.forward, toB), 10f, "Facing the partner.");
            Assert.Less(Vector3.Angle(viewB.transform.forward, -toB), 10f, "Facing the partner.");
            Assert.Less(toB.magnitude, 0.55f, "Noses close.");
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
