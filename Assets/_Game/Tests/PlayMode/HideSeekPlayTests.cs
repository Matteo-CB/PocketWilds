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
using PocketWilds.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T39 « Cache-cache » in the real game (doc « Mini-jeux &amp; activités » section 6: « un test PlayMode joue une partie
    /// complète »; doc 01 Contenu S-ACT-002). The first game, with its tutorial, plays in the refuge: the measured spots load
    /// from their Addressables group, the refuge camera glides to an open Prairie zone (the refuge still drawn, its own
    /// animals and egg hidden), the game's animals stand in the zone's spots without entering anything, as visible as the game
    /// asks; the paw shows the most visible one; the magnifier and the whistle; an empty spot's leaves; a tap on every
    /// occupied spot (« coucou », free landing points); the score, the stars, the result card and the rewards; no allocation
    /// per frame in play; back to the hub, the refuge as it was, the spots released with the hub.
    /// </summary>
    public class HideSeekPlayTests
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
            Time.timeScale = 1f;
            FoliageGusts.ClearAll();
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

        [UnityTest]
        public IEnumerator HideSeek_PlaysAWholeGame_InTheRefuge()
        {
#if UNITY_EDITOR
            SeedEightAnimals();
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var services = context.Activities;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var presenter = entry.Activities;
            Assert.IsNotNull(presenter, "The scene runs the activities.");
            if (presenter.Stages is ActivityStageRegistry registry && !registry.Has(ActivityId.HideAndSeek))
                HideSeekStage.Register(registry);
            Assert.IsTrue(presenter.Stages.Has(ActivityId.HideAndSeek), "Hide and Seek's stage is registered.");
            var refuge = entry.Refuge;
            var camera = entry.CameraRig.Camera;

            // The refuge's temperaments are the stage's (a Shy animal is the doc's Timid one).
            var scheduler = refuge.Scheduler;
            var collection = context.State.Collection;
            for (int i = 0; i < scheduler.Count; i++)
            {
                int handle = scheduler.GetActiveHandle(i);
                if (collection.TryGet(scheduler.GetInstanceId(handle), out var animal))
                    Assert.AreEqual(scheduler.GetTemperament(handle), HideSeekStage.TemperamentOf(animal.Seed), "Same temperament as the refuge's.");
            }

            // Doc 01: Hide and Seek opens at refuge level 4 with 8 animals.
            services.Activities.Records[ActivityCatalog.SlotOf(ActivityId.HideAndSeek)].Unlocked = true;
            services.RefreshUnlocks();
            yield return ClearScreens(entry);
            yield return WaitFor(() => presenter.PlayAvailable, 3f, "The Play button shows once Hide and Seek is open.");
            presenter.OpenActivities();
            yield return null;
            Assert.IsTrue(presenter.HubOpen);
            // The player's view once the rig has settled (a card closed at boot may still glide it): the view to come back to.
            yield return WaitStill(camera, 3f);
            var home = camera.transform.position;
            float homeFov = camera.fieldOfView;
            presenter.StartGame(ActivityRequest.Free(ActivityId.HideAndSeek));
            yield return WaitFor(() => presenter.Playing, 8f, "The spots load and the camera glides to its zone (under a second on device).");

            var stage = presenter.Stages.Acquire(ActivityId.HideAndSeek, null) as HideSeekStage;
            Assert.IsNotNull(stage, "The hide-and-seek stage plays the game.");
            stage.PollInput = false;
            var game = stage.Game;
            var controller = (HideSeekController)stage.Controller;
            Assert.IsTrue(stage.SpotsLoaded, "The spots come from their Addressables group (Activity_HideAndSeek).");
            Assert.IsTrue(stage.SpotsMeasured, "DEF_HideSpots_Grassland is measured (run the hide-spot pipeline).");
            Assert.IsTrue(stage.Shown);

            // In the refuge: still drawn, its own animals and egg aside, the camera on the zone's measured pose.
            Assert.IsTrue(camera.enabled, "The refuge keeps rendering: the game plays in it.");
            Assert.IsFalse(refuge.ActorsVisible, "The refuge's own animals and egg step aside for the game.");
            Assert.IsTrue(refuge.CameraHeld, "The game holds the refuge camera.");
            Assert.IsFalse(refuge.CameraGliding, "The glide is over when the game begins.");
            var zone = stage.Spots.GetZone(stage.Zone);
            Assert.Less(Vector3.Distance(camera.transform.position, stage.Origin + zone.cameraPosition), 0.01f, "The camera frames the zone.");
            Assert.AreEqual(ChunkStreamState.Active, entry.Streamer.GetState(entry.Streamer.IndexOf(zone.chunkId)), "The zone's chunk is drawn.");

            // The zone's spots were given to the game before it began (the presenter's Prepare).
            Assert.IsFalse(controller.UsesDefaultSpots, "The game plays the zone's spots.");
            Assert.AreEqual(stage.SpotCount, game.SpotCount);
            var first = stage.Spots.GetSpot(zone.firstSpot).position;
            Assert.AreEqual(first.x, controller.SpotX(0), 1e-5f, "SetSpots came before Begin.");
            Assert.AreEqual(game.HiddenCount, stage.HiddenShown, "Every hidden animal stands in its spot.");
            for (int i = 0; i < game.HiddenCount; i++)
            {
                Assert.IsTrue(stage.IsHidden(i), $"Animal {i} is hidden.");
                Assert.IsNotNull(stage.View(i), $"Animal {i} has its view.");
                CheckNothingEntered(stage, i, "hidden");
                // Doc 01: a Timid (the refuge's Shy) animal waits 2 s more, told to the game right after its Begin.
                Assert.IsTrue(collection.TryGet(stage.View(i).OwnerInstanceId, out var hidden), $"Animal {i} is one of the player's.");
                Assert.AreEqual(HideSeekStage.TemperamentOf(hidden.Seed) == Temperament.Shy, controller.IsTimid(i), $"Animal {i}'s temperament reached the game.");
            }

            // The visible share: a sample of the measured table at the game's visibility (doc 01: 30 % at d1, 8 % at d10).
            int measured = 0, near = 0;
            for (int i = 0; i < game.HiddenCount; i++)
            {
                float share = stage.ExpectedShare(i);
                if (share < 0f)
                    continue;
                measured++;
                if (Mathf.Abs(share - game.Visibility) <= 0.06f)
                    near++;
                Debug.Log($"[HideSeekPlayTests] animal {i} spot {game.AnimalSpot(i)}: visible {share:P0} for {game.Visibility:P0}");
            }
            Debug.Log($"[HideSeekPlayTests] zone {zone.chunkId}, {stage.SpotCount} spots, {game.HiddenCount} hidden, {stage.PlacementSteps} cm stepped back to keep bodies free");
            Assert.Greater(measured, 0, "The played species are measured.");
            Assert.GreaterOrEqual(near * 2, measured, "Most hidden animals show about the share the game asks for.");

            // Doc Mini-jeux section 1: after 2 s the paw taps the most visible animal, with its tip.
            var hud = Object.FindAnyObjectByType<ActivityHudScreen>();
            yield return WaitFor(() => stage.TutorialStep == 2, 4f, "The first game shows its paw after 2 s.");
            yield return null;
            if (hud != null)
                Assert.IsTrue(hud.GestureShown, "The paw (UI_Gesture_Tap) shows on a hidden animal.");
            Assert.IsTrue(stage.ItemsShown, "The magnifier and the whistle show in the HUD.");

            // Every spot's decor is its own tap target (no decor hides another one's), and a tap on a hidden animal's body (its
            // ear over a neighbour's decor, or the part behind its own) touches its spot.
            for (int k = 0; k < stage.SpotCount; k++)
                Assert.AreEqual(k, stage.HitDecor(stage.SpotScreen(k)), $"A tap on spot {k}'s decor touches spot {k}.");
            for (int i = 0; i < game.HiddenCount; i++)
            {
                var middle = camera.WorldToScreenPoint(stage.AnimalPosition(i) + Vector3.up * (0.5f * stage.AnimalBody(i).height * stage.AnimalScale(i)));
                Assert.AreEqual(game.AnimalSpot(i), stage.HitSpot(new Vector2(middle.x, middle.y)), $"A tap on animal {i} touches its spot.");
            }

            // An empty spot (a free one, else a decoy: both are empty to the touch): three leaves fly, no point lost. A small
            // zone may be full (as many animals as spots, no decoy): then there is nothing empty to touch.
            int empty = -1;
            for (int pass = 0; pass < 2 && empty < 0; pass++)
                for (int k = 0; k < stage.SpotCount && empty < 0; k++)
                    if (controller.AnimalAt(k) < 0 && (pass == 1 || !game.IsDecoy(k)) && stage.HitSpot(stage.SpotScreen(k)) == k)
                        empty = k;
            if (empty >= 0)
            {
                int scoreBefore = controller.Score;
                stage.TapAt(stage.SpotScreen(empty));
                Assert.AreEqual(1, game.EmptyTouches, "The empty touch counts.");
                Assert.AreEqual(scoreBefore, controller.Score, "No point lost.");
                Assert.Greater(stage.LeavesFlying, 0, "Leaves fly off the decor.");
            }
            else
            {
                Assert.AreEqual(stage.SpotCount, game.HiddenCount, "Only a full zone has no empty spot.");
                TestContext.WriteLine("The zone is full (" + stage.SpotCount + " spots): no empty touch to check.");
            }

            // No allocation per frame in play: the game, the stage, the hints, ripples, rings and leaves. The warm-up runs the
            // game to its first hint (its one-time tip and first rustle), so the measured frames really hold hints and ripples.
            for (int f = 0; f < 10; f++)
                yield return null;
            for (int f = 0; f < 60 * 25 && !AnyHinting(game); f++)
            {
                controller.Tick(1f / 60f);
                stage.Tick(1f / 60f, 1f / 60f);
            }
            Assert.IsTrue(AnyHinting(game), "An animal gives its hint (doc 01: after 6 to 15 s).");
            for (int f = 0; f < 6; f++)
            {
                controller.Tick(1f / 60f);
                stage.Tick(1f / 60f, 1f / 60f);
            }
            int second = -1;
            for (int k = 0; k < stage.SpotCount && second < 0; k++)
                if (k != empty && controller.AnimalAt(k) < 0 && stage.HitSpot(stage.SpotScreen(k)) == k)
                    second = k;
            bool hintRipple = false;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 120; frame++)
            {
                controller.Tick(1f / 60f);
                if (frame == 40 && second >= 0)
                    stage.TapAt(stage.SpotScreen(second));
                stage.Tick(1f / 60f, 1f / 60f);
                for (int i = 0; i < game.HiddenCount && !hintRipple; i++)
                    hintRipple = game.AnimalHinting(i) && stage.Rippling(game.AnimalSpot(i));
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "Hide and Seek allocates nothing per frame in play.");
            Assert.IsTrue(hintRipple, "The measured frames hold a hint's ripple (H-VFX-ACT-002-03).");

            // The items: the magnifier rings the nearest occupied spot, the whistle wiggles every animal still hidden.
            stage.UseItem(HideSeekStage.MagnifierItem);
            Assert.IsFalse(game.MagnifierAvailable, "The magnifier is used once.");
            Assert.GreaterOrEqual(game.MagnifierSpot, 0, "It rings an occupied spot.");
            yield return null;
            yield return null;
            Assert.IsTrue(stage.MagnifierRingShown, "A soft glass ring around the spot.");
            // The hints already ripple the hidden animals' decor: the whistle's own wiggles are counted.
            int stillHidden = 0;
            for (int i = 0; i < game.HiddenCount; i++)
                if (stage.IsHidden(i))
                    stillHidden++;
            int wiggles = stage.WhistleWiggles;
            stage.UseItem(HideSeekStage.WhistleItem);
            Assert.IsFalse(game.WhistleAvailable, "The whistle is used once.");
            yield return WaitFor(() => stage.WhistleWiggles >= wiggles + stillHidden, 3f,
                "Every animal still hidden wiggles once, its decor rippling (FoliageGusts).");

            // A tap on every occupied spot: « coucou », a heart, a ring, the hop out to free ground.
            bool firstFind = true;
            for (int i = 0; i < game.HiddenCount; i++)
            {
                int spot = game.AnimalSpot(i);
                if (spot < 0)
                    continue;
                for (int tries = 0; tries < 6 && !game.AnimalFound(i); tries++)
                {
                    stage.TapAt(stage.SpotScreen(spot));
                    // Three empty touches in 2 s rest the touches for 1 s: wait it out, like a player.
                    if (!game.AnimalFound(i) && game.TouchesPaused)
                        yield return new WaitForSecondsRealtime(1.05f);
                    // Another animal answered (its body under the finger): the same place again only after the double tap's
                    // guard (game time, so a little more).
                    else if (!game.AnimalFound(i))
                        yield return new WaitForSeconds(HideSeekLayout.FoundTapGuardSeconds + 0.2f);
                    yield return null;
                }
                Assert.IsTrue(game.AnimalFound(i) || game.AllFound, $"Animal {i} found with a tap on its decor.");
                if (firstFind)
                {
                    firstFind = false;
                    Assert.AreEqual(3, stage.TutorialStep, "The first find ends the paw.");
                    Assert.Greater(refuge.Vfx.PlayingCount, 0, "A heart above it (H-VFX-012).");
                    Assert.IsTrue(stage.RingShown(0) || stage.RingShown(1) || stage.RingShown(2), "A discovery ring where it lands.");
                    Assert.IsTrue(stage.IsHopping(i), "It hops out « coucou ».");
                    // Game time (a long editor frame counts 0.1 s at most): waited for, never assumed.
                    yield return WaitFor(() => stage.IsOut(i), 3f, "It landed.");
                    CheckNothingEntered(stage, i, "landed");
                }
            }

            yield return WaitFor(() => !presenter.Playing, 3f, "Every animal found ends the game.");
            Assert.IsTrue(game.AllFound, "All found (doc 01: +5 per second left).");
            for (int i = 0; i < game.HiddenCount; i++)
                if (stage.IsOut(i))
                    CheckNothingEntered(stage, i, "out");
            yield return WaitFor(() => ResultOpen(), 3f, "The result card shows over the refuge.");
            Assert.IsTrue(stage.Shown, "The game stays under the result card.");
            var result = services.Runner.LastResult;
            Assert.AreEqual(controller.Score, result.Score);
            Assert.IsTrue(services.Rules.TryGet(ActivityId.HideAndSeek, out var rules), "Hide and Seek's rules ship (HS).");
            var thresholds = rules.Free;
            int stars = result.Score >= thresholds.Three ? 3 : result.Score >= thresholds.Two ? 2 : result.Score >= thresholds.One ? 1 : 0;
            Assert.AreEqual(stars, result.Stars, "Stars from the free-mode thresholds (300, 600, 750).");
            Assert.IsTrue(result.Rewarded, "A ticket game is rewarded.");
            if (stars > 0)
                Assert.Greater(result.Rewards.LightMinutes, 0.0, "Its rewards are paid.");

            // Back to the hub: the refuge as it was (its animals, its egg, the player's view); closing releases the spots.
            presenter.BackToHub();
            yield return null;
            Assert.IsFalse(stage.Shown);
            Assert.IsTrue(refuge.ActorsVisible, "The refuge's animals and egg are back.");
            for (int i = 0; i < FoliageGusts.Slots; i++)
                Assert.AreEqual(0f, FoliageGusts.Strength(i), "No gust left on the foliage.");
            yield return WaitFor(() => !refuge.CameraHeld && !refuge.CameraGliding, 3f, "The camera glides back.");
            Assert.IsTrue(entry.CameraRig.enabled, "The rig has the camera again.");
            Assert.Less(Vector3.Distance(home, camera.transform.position), 0.05f, "The player's view, as it was.");
            Assert.AreEqual(homeFov, camera.fieldOfView, 0.01f);
            presenter.CloseActivities();
            yield return null;
            Assert.IsFalse(stage.SpotsLoaded, "Leaving the activities releases Activity_HideAndSeek.");
            Assert.IsNull(stage.Spots);
#else
            yield break;
#endif
        }

        /// <summary>Collision rule: the animal's body, at its own size, enters no volume of the zone nor another animal.</summary>
        private static void CheckNothingEntered(HideSeekStage stage, int animal, string where)
        {
            var body = stage.AnimalBody(animal);
            float scale = stage.AnimalScale(animal);
            var position = stage.AnimalPosition(animal);
            body.Segment(position, stage.AnimalYaw(animal), scale, out var a0, out var a1);
            float radius = body.halfWidth * scale;
            for (int j = 0; j < stage.ObstacleCount; j++)
            {
                var o = stage.GetObstacle(j);
                if (!o.BlocksWalker(position.y, body.height * scale))
                    continue;
                Assert.GreaterOrEqual(CollisionMath.Gap(a0, a1, radius, in o), -0.002f, $"Animal {animal} ({where}) enters a volume of the zone.");
            }
            Assert.GreaterOrEqual(stage.AnimalClearance(animal), -0.002f, $"Animal {animal} ({where}) enters something.");
        }

        private static bool ResultOpen()
        {
            var result = Object.FindAnyObjectByType<ActivityResultScreen>();
            return result != null && result.IsOpen;
        }

        /// <summary>Closes what a boot may open over the refuge: the offline return card, a refuge level's reward card, Today.</summary>
        private static IEnumerator ClearScreens(VerticalSliceEntry entry)
        {
            var refuge = entry.Refuge;
            float until = Time.realtimeSinceStartup + 8f;
            bool collected = false;
            while (refuge.ModalOpen && Time.realtimeSinceStartup < until)
            {
                var offline = Object.FindAnyObjectByType<OfflineReturnScreen>();
                if (refuge.OfflineScreenOpen && !collected && offline != null && offline.IsOpen)
                {
                    yield return new WaitForSecondsRealtime(0.35f);
                    offline.GetComponentInChildren<UnityEngine.UI.Button>(true).onClick.Invoke();
                    collected = true;
                }
                var progression = entry.ProgressionScreens;
                if (progression != null && progression.RewardWaiting && !progression.IsOpen)
                    progression.ShowPendingRewards();
                var card = Object.FindAnyObjectByType<LevelRewardScreen>();
                if (card != null && card.IsOpen)
                    card.ClaimButton.onClick.Invoke();
                if (entry.DailyVisit != null && entry.DailyVisit.ScreenOpen)
                    entry.DailyVisit.CloseToday();
                yield return null;
            }
        }

        private static bool AnyHinting(IHideSeekGame game)
        {
            for (int i = 0; i < game.HiddenCount; i++)
                if (game.AnimalHinting(i))
                    return true;
            return false;
        }

        /// <summary>Until the camera rests (a few frames without moving), at most this long: the view a game must give back.</summary>
        private static IEnumerator WaitStill(Camera camera, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            var last = camera.transform.position;
            int still = 0;
            while (still < 5 && Time.realtimeSinceStartup < until)
            {
                yield return null;
                var now = camera.transform.position;
                still = (now - last).sqrMagnitude < 1e-8f ? still + 1 : 0;
                last = now;
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string message)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until)
                    Assert.Fail(message);
                yield return null;
            }
        }

        /// <summary>Doc 01: eight animals of the three Prairie species (the activity opens with 8).</summary>
        private void SeedEightAnimals()
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            var animals = new[]
            {
                ("species.rabbit", "variant.rabbit.brown"), ("species.squirrel", "variant.squirrel.red"), ("species.hedgehog", "variant.hedgehog.brown"),
                ("species.rabbit", "variant.rabbit.white"), ("species.squirrel", "variant.squirrel.grey"), ("species.hedgehog", "variant.hedgehog.mushroom"),
                ("species.rabbit", "variant.rabbit.spotted"), ("species.squirrel", "variant.squirrel.autumn"),
            };
            for (int i = 0; i < animals.Length; i++)
            {
                var species = ContentId.FromSource(animals[i].Item1);
                state.Collection.Add(new AnimalInstance
                {
                    Species = species, Variant = ContentId.FromSource(animals[i].Item2), Rarity = RarityTier.Common,
                    Seed = SeedHash.Mix((uint)i + 1u, 0x48534B21u),
                });
                state.Collection.DiscoveredSpecies.Add(species);
            }
            Assert.IsTrue(service.FlushNow());
            service.Dispose();
        }

        private IEnumerator Boot(Action<GameBootstrap> ready)
        {
#if UNITY_EDITOR
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = _saveDir,
                ContentOverride = database.CreateBuilder().Build(),
                VisualCatalogOverride = catalog,
                LoadTargetScene = true,
                ClockOverride = new FakeGameClock(new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero)),
            };
#endif
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            // The first test of a run boots cold (assets, shaders): more time than the warm boots after it.
            float timeout = Time.realtimeSinceStartup + 60f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                if (Time.realtimeSinceStartup > timeout)
                    Assert.Fail("Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            ready(bootstrap);
        }
    }
}
