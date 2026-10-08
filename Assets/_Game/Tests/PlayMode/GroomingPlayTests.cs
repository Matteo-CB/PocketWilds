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
    /// T38 « Toilettage » in the real game (doc « Mini-jeux &amp; activités » section 6: « un test PlayMode joue une partie
    /// complète »; doc 01 Contenu S-ACT-001 and A15). The free first game with its tutorial: the set loads from its
    /// Addressables group, the refuge stops rendering, the played animal stands on its cushion with its measured zones given to
    /// the game before it began; strokes brush every dirty zone (the dust array reaches 0), taps take every leaf and burr, a
    /// soap bubble pops, the golden brush is taken when offered, a bow lands on the head socket; the score, the stars, the
    /// result card and the rewards; no allocation per frame in play; back to the refuge, the set released with the hub.
    /// </summary>
    public class GroomingPlayTests
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
        public IEnumerator Grooming_PlaysAWholeGame_OnItsSet()
        {
#if UNITY_EDITOR
            SeedFiveAnimals();
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var services = context.Activities;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var presenter = entry.Activities;
            Assert.IsNotNull(presenter, "The scene runs the activities.");
            if (presenter.Stages is ActivityStageRegistry registry && !registry.Has(ActivityId.Grooming))
                GroomingStage.Register(registry);
            Assert.IsTrue(presenter.Stages.Has(ActivityId.Grooming), "Grooming's stage is registered.");

            // Doc 01 A15: Grooming opens at refuge level 3 with 5 animals; the first game is free, with its tutorial.
            services.Activities.Records[ActivityCatalog.SlotOf(ActivityId.Grooming)].Unlocked = true;
            services.RefreshUnlocks();
            // A15: the button arrives when no screen covers the refuge: the boot's cards (offline return, a refuge level) go first.
            yield return ClearScreens(entry);
            yield return WaitFor(() => presenter.PlayAvailable, 3f, () => "The Play button shows once Grooming is open (available "
                + services.IsAvailable(ActivityId.Grooming) + ", modal " + entry.Refuge.ModalOpen + ", offline " + entry.Refuge.OfflineScreenOpen
                + ", progression " + entry.Refuge.ProgressionOpen + ", daily " + entry.Refuge.DailyOpen + ", hatching " + entry.Refuge.Hatching + ").");
            presenter.OpenActivities();
            yield return null;
            Assert.IsTrue(presenter.HubOpen);
            int tickets = services.Tickets.Count;
            presenter.Play();
            yield return WaitFor(() => presenter.Playing, 5f, "The set loads in a moment (doc Mini-jeux section 1: under a second on device).");
            Assert.AreEqual(tickets, services.Tickets.Count, "The first game is free (doc 01 A15).");

            var stage = presenter.Stages.Acquire(ActivityId.Grooming, null) as GroomingStage;
            Assert.IsNotNull(stage, "The grooming stage plays the game.");
            stage.PollInput = false;
            var game = stage.Game;
            var controller = (GroomingController)stage.Controller;
            Assert.IsTrue(stage.SetLoaded, "The set comes from its Addressables group.");
            Assert.IsTrue(stage.Shown);
            Assert.IsNotNull(stage.Animal, "The played animal stands on the set.");
            var refugeCamera = entry.CameraRig.Camera;
            Assert.IsFalse(refugeCamera.enabled, "The refuge does not render while the set shows.");
            Assert.IsTrue(stage.Camera.enabled, "The set renders with its own camera.");

            // The zones measured on the model were given to the game before it began (the presenter's Prepare).
            var body = stage.Animal.Body;
            Assert.AreEqual(body.zoneCount, game.ZoneCount, "The game plays the measured coat.");
            Assert.AreEqual(body.zonePoints[0].x, controller.ZoneX(0), 1e-5f, "SetBody came before Begin.");
            Assert.AreEqual(GroomingLayout.ZoneCount(RigFamily.SmallQuadruped), body.zoneCount, "SmallQuadruped: 40 zones (doc 01).");
            Assert.IsTrue(stage.Animal.Dusty, "The fur shows the dust (measured groom mesh).");

            // A15: the swipe paw over the dusty coat until the first pass.
            yield return null;
            yield return null;
            var hud = Object.FindAnyObjectByType<ActivityHudScreen>();
            Assert.AreEqual(1, stage.TutorialStep, "The free first game shows its tutorial.");
            if (hud != null)
                Assert.IsTrue(hud.GestureShown, "The brush paw shows (UI_Gesture_Swipe).");
            CheckOnCushion(stage);

            // Brush every dirty zone but a few (the bubble and the golden brush need dust left).
            int dirty = 0;
            for (int z = 0; z < game.ZoneCount; z++)
                if (game.IsDirty(z))
                    dirty++;
            Assert.Greater(dirty, 8, "The game dirtied its zones.");
            int keep = 6;
            for (int z = 0; z < game.ZoneCount && dirty > keep; z++)
            {
                if (!game.IsDirty(z))
                    continue;
                yield return BrushZone(stage, game, z);
                Assert.IsFalse(game.IsDirty(z), $"Zone {z} is clean after its passes.");
                dirty--;
            }
            Assert.AreNotEqual(1, stage.TutorialStep, "The first pass ends the brush paw.");
            Assert.Greater(stage.FoamAlive + controller.ZonesCleaned, 0, "Foam on the brushed zones.");
            CheckOnCushion(stage);

            // Every leaf and burr: one tap, two for a burr (doc 01: +15, +25).
            for (int i = 0; i < game.DebrisCount; i++)
            {
                if (game.DebrisActive(i))
                    CheckOnCoat(stage, game, i);
                for (int tries = 0; tries < 4 && game.DebrisActive(i); tries++)
                {
                    Assert.IsTrue(stage.DebrisShown(i), $"Debris {i} lies on the coat.");
                    stage.TapAt(stage.DebrisScreen(i));
                    yield return null;
                }
                Assert.IsFalse(game.DebrisActive(i), $"Debris {i} taken off.");
            }
            Assert.AreEqual(game.DebrisCount, game.DebrisRemoved, "All debris and burrs removed.");
            Assert.GreaterOrEqual(stage.TutorialStep, 3, "The leaf paw went with the first leaf.");

            // No allocation per frame in play: the game, the stage, strokes and their events, after the warm-up.
            yield return null;
            int zone = FirstDirty(game);
            Assert.GreaterOrEqual(zone, 0);
            var center = stage.Animal.Screen[zone];
            float r = stage.Animal.ScreenRadii[zone];
            stage.PointerDown(center - new Vector2(0.7f * r, 0f));
            stage.PointerMove(center + new Vector2(0.7f * r, 0f));
            stage.Tick(1f / 60f, 1f / 60f);
            stage.LateTick();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 90; frame++)
            {
                controller.Tick(1f / 120f);
                // Back and forth across the zone while it is dirty (a pass each move: foam, sounds, its glints when clean),
                // then the finger rests on it (no rebrush).
                if (game.IsDirty(zone))
                    stage.PointerMove(center + new Vector2((frame & 1) == 0 ? -0.7f * r : 0.7f * r, 0f));
                stage.Tick(1f / 60f, 1f / 60f);
                stage.LateTick();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsFalse(game.IsDirty(zone), "The measured strokes cleaned their zone.");
            stage.PointerUp();
            Assert.AreEqual(0, allocated, "The grooming allocates nothing per frame in play.");
            var gcRecorder = UnityEngine.Profiling.Recorder.Get("GC.Alloc");
            gcRecorder.FilterToCurrentThread();
            gcRecorder.enabled = true;

            // A soap bubble at 15 s: a tap cleans the 3 dirty zones nearest to it.
            Time.timeScale = 4f;
            yield return WaitFor(() => game.BubbleActive(0) || game.Phase != GroomingPhase.Brushing, 20f, "The first bubble comes at 15 s.");
            Time.timeScale = 1f;
            if (game.BubbleActive(0))
            {
                yield return null;
                Assert.IsTrue(stage.BubbleShown(0), "The bubble rests on the coat.");
                int left = controller.DirtyLeft;
                stage.TapAt(stage.BubbleScreen(0));
                yield return null;
                Assert.IsFalse(game.BubbleActive(0), "A tap pops the bubble.");
                Assert.Less(controller.DirtyLeft, left, "The bubble cleaned the zones near it.");
            }
            int frames = Time.frameCount;
            yield return null;
            yield return null;
            Debug.Log($"[GroomingPlayTests] GC.Alloc samples in the last frame (whole editor frame): {gcRecorder.sampleBlockCount} ({Time.frameCount - frames} frames)");
            gcRecorder.enabled = false;

            // The golden brush at 20 s when the streak is at × 1.5 (taken: 5 s of double passes, the brush in gold).
            Time.timeScale = 4f;
            yield return WaitFor(() => game.GoldenBrushZone >= 0 || game.Elapsed > 21f || game.Phase != GroomingPhase.Brushing, 20f, "20 s.");
            Time.timeScale = 1f;
            if (game.GoldenBrushZone >= 0)
            {
                yield return null;
                yield return null;
                Assert.IsTrue(stage.GoldenShown, "The golden brush waits on the coat.");
                stage.TapAt(stage.GoldenScreen);
                yield return null;
                Assert.IsTrue(game.GoldenBrushActive, "Taken with a tap.");
                Assert.IsTrue(stage.BrushGold, "The brush turns gold.");
            }

            // The last zones: all clean, the bow phase and its picker.
            for (int z = 0; z < game.ZoneCount; z++)
                if (game.IsDirty(z))
                    yield return BrushZone(stage, game, z);
            yield return WaitFor(() => game.Phase == GroomingPhase.Bow, 3f, "All clean: the bows come.");
            yield return null;
            Assert.IsTrue(stage.PickerShown, "The three bows show in the HUD.");
            if (hud != null)
                Assert.IsTrue(hud.PickerShown);
            // The dust fades in about a third of a second, the clean glint in 0.6 s.
            yield return new WaitForSecondsRealtime(0.9f);
            for (int z = 0; z < game.ZoneCount; z++)
                Assert.Less(stage.Animal.ShaderValue(z), 0.01f, $"Zone {z}: no dust left on the fur.");

            // The bow: it flies onto the head and stays there; the game ends, the result card shows.
            int score = controller.Score;
            stage.Pick((int)GroomingBow.SkyBlue);
            yield return WaitFor(() => stage.BowOnHead, 2f, "The bow lands on SOCK_HeadTop.");
            Assert.AreEqual((int)GroomingBow.SkyBlue, stage.BowColor);
            Assert.Greater(stage.BowTransform.position.y, stage.Animal.HeadTop.position.y, "The bow sits on the head, never in it.");
            Assert.GreaterOrEqual(stage.BowTransform.localPosition.y, body.bowLift - 1e-5f);
            yield return WaitFor(() => !presenter.Playing, 2f, "The bow ends the game.");
            yield return WaitFor(() => ResultOpen(), 3f, "The result card shows over the set.");
            Assert.IsTrue(stage.Shown, "The set stays under the result card.");
            Assert.IsFalse(refugeCamera.enabled);
            var result = services.Runner.LastResult;
            Assert.IsTrue(result.Rewarded && result.FreeGame, "The free first game is rewarded.");
            Assert.Greater(result.Score, score, "The bow's +50 (and the seconds left).");
            Assert.AreEqual(controller.Score, result.Score);
            Assert.IsTrue(services.Rules.TryGet(ActivityId.Grooming, out var rules), "Grooming's rules ship (GS).");
            var thresholds = rules.Free;
            int stars = result.Score >= thresholds.Three ? 3 : result.Score >= thresholds.Two ? 2 : result.Score >= thresholds.One ? 1 : 0;
            Assert.AreEqual(stars, result.Stars, "Stars from the free-mode thresholds (200, 380, 520).");
            if (stars > 0)
                Assert.Greater(result.Rewards.LightMinutes, 0.0, "Its rewards are paid.");

            // Back to the hub: the refuge renders again; closing the hub releases the set.
            presenter.BackToHub();
            yield return null;
            Assert.IsTrue(refugeCamera.enabled, "The refuge is back.");
            Assert.IsFalse(stage.Shown);
            presenter.CloseActivities();
            yield return null;
            Assert.IsFalse(stage.SetLoaded, "Leaving the activities releases Activity_Grooming.");
            Assert.IsNull(stage.Set);
#else
            yield break;
#endif
        }

        /// <summary>Scrubs across a zone's screen disk, back and forth inside it, until it is clean (one press per frame).</summary>
        private static IEnumerator BrushZone(GroomingStage stage, IGroomingGame game, int zone)
        {
            for (int tries = 0; tries < 6 && game.IsDirty(zone); tries++)
            {
                var c = stage.Animal.Screen[zone];
                float r = stage.Animal.ScreenRadii[zone];
                stage.PointerDown(c - new Vector2(0.75f * r, 0f));
                for (int k = 0; k < 4; k++)
                    stage.PointerMove(c + new Vector2((k & 1) == 0 ? 0.75f * r : -0.75f * r, 0f));
                stage.PointerUp();
                yield return null;
            }
        }

        private static void CheckOnCushion(GroomingStage stage)
        {
            var anchor = stage.Set.Data.animalAnchor.position;
            var root = stage.Animal.View.transform.position;
            Assert.GreaterOrEqual(root.y, anchor.y - 0.002f, "The paws stand on the cushion, never in it.");
            Assert.LessOrEqual(Mathf.Abs(stage.SwayOffset), stage.SwayLimit + 1e-4f, "The sway stays over the cushion's flat top.");
        }

        private static void CheckOnCoat(GroomingStage stage, IGroomingGame game, int debris)
        {
            if (!stage.DebrisShown(debris))
                return;
            int zone = game.DebrisZone(debris);
            var offset = stage.DebrisPosition(debris) - stage.Animal.Points[zone];
            Assert.GreaterOrEqual(Vector3.Dot(offset, stage.Animal.Normals[zone]), 0f, "A debris lies on the fur, never in it.");
        }

        private static int FirstDirty(IGroomingGame game)
        {
            for (int z = 0; z < game.ZoneCount; z++)
                if (game.IsDirty(z))
                    return z;
            return -1;
        }

        private static bool ResultOpen()
        {
            var result = Object.FindAnyObjectByType<ActivityResultScreen>();
            return result != null && result.IsOpen;
        }

        /// <summary>Doc 01 A15's threshold: five animals of the three Prairie species.</summary>
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
                    // Like a player: the card's opening ends, then « Récolter » once.
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

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, Func<string> message)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until)
                    Assert.Fail(message());
                yield return null;
            }
        }

        private void SeedFiveAnimals()
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            var animals = new[]
            {
                ("species.rabbit", "variant.rabbit.brown"), ("species.squirrel", "variant.squirrel.red"), ("species.hedgehog", "variant.hedgehog.brown"),
                ("species.rabbit", "variant.rabbit.white"), ("species.squirrel", "variant.squirrel.grey"),
            };
            for (int i = 0; i < animals.Length; i++)
            {
                var species = ContentId.FromSource(animals[i].Item1);
                state.Collection.Add(new AnimalInstance
                {
                    Species = species, Variant = ContentId.FromSource(animals[i].Item2), Rarity = RarityTier.Common,
                    Seed = SeedHash.Mix((uint)i + 1u, 0x47524F4Du),
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
    }
}
