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
    /// T40 « Pluie de noisettes » in the real game (doc « Mini-jeux &amp; activités » section 6: « un test PlayMode joue une
    /// partie complète »; doc 01 Contenu S-ACT-003). The first game with its tutorial: the set loads from its Addressables
    /// group, the refuge stops rendering, the Squirrel plays (doc 01) with the basket on its head; the swipe paw and its tip
    /// until the first catch; a finger slides the basket under the items: catches, the score, the streak and its
    /// multiplier, the pile in the basket, a pinecone that resets the streak, a special with the HUD's boost chip; nothing
    /// enters anything (items, basket, animal, floor) on any frame; no allocation per frame in play; the result card, its
    /// stars and its rewards (the Hazelnuts counted from the game); back to the refuge, the set released with the hub.
    /// </summary>
    public class NutDropPlayTests
    {
        private static readonly ContentId Squirrel = ContentId.FromSource("species.squirrel");

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
        public IEnumerator NutDrop_PlaysAWholeGame_OnItsSet()
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
            if (presenter.Stages is ActivityStageRegistry registry && !registry.Has(ActivityId.NutRain))
                NutDropStage.Register(registry);
            Assert.IsTrue(presenter.Stages.Has(ActivityId.NutRain), "Nut Drop's stage is registered.");

            // Doc 01: Nut Drop opens at refuge level 10, on day 4 or with 15 animals.
            services.Activities.Records[ActivityCatalog.SlotOf(ActivityId.NutRain)].Unlocked = true;
            services.RefreshUnlocks();
            yield return ClearScreens(entry);
            yield return WaitFor(() => presenter.PlayAvailable, 3f, "The Play button shows once Nut Drop is open.");
            presenter.OpenActivities();
            yield return null;
            Assert.IsTrue(presenter.HubOpen);
            presenter.StartGame(ActivityRequest.Free(ActivityId.NutRain));
            yield return WaitFor(() => presenter.Playing, 5f, "The set loads in a moment (doc Mini-jeux section 1: under a second on device).");

            var stage = presenter.Stages.Acquire(ActivityId.NutRain, null) as NutDropStage;
            Assert.IsNotNull(stage, "The nut drop stage plays the game.");
            stage.PollInput = false;
            var game = stage.Game;
            var controller = (NutDropController)stage.Controller;
            Assert.IsTrue(stage.SetLoaded, "The set comes from its Addressables group (Activity_NutDrop).");
            Assert.IsTrue(stage.Shown);
            Assert.IsNotNull(stage.Animal, "The played animal stands on the set.");
            Assert.IsTrue(context.State.Collection.TryGet(stage.PlayedAnimal, out var played), "One of the player's animals plays.");
            Assert.AreEqual(Squirrel, played.Species, "Doc 01: the Squirrel when the player has one.");
            Assert.AreEqual(Squirrel, stage.Animal.Species);
            var refugeCamera = entry.CameraRig.Camera;
            Assert.IsFalse(refugeCamera.enabled, "The refuge does not render while the set shows.");
            Assert.IsTrue(stage.Camera.enabled, "The set renders with its own camera.");
            Assert.IsNotNull(stage.Basket, "The basket is on the set.");
            Assert.IsTrue(stage.Basket.gameObject.activeSelf, "The basket shows on the head.");
            Assert.AreEqual(game.BasketWidth * NutDropLayout.FieldScale * 0.5f, stage.BasketHalfWidth, 0.01f, "The basket is the catch width.");
            Assert.GreaterOrEqual(stage.BasketLift, stage.SeatLift - 1e-4f, "The basket sits on the head, never in it.");

            // The first game: after 1 s a paw slides left and right under the basket with « Glisse pour attraper ».
            var hud = Object.FindAnyObjectByType<ActivityHudScreen>();
            yield return WaitFor(() => stage.TutorialStep == 2, 4f, "The first game shows its paw after 1 s.");
            yield return null;
            if (hud != null)
            {
                Assert.IsTrue(hud.GestureShown, "The swipe paw shows (UI_Gesture_Swipe).");
                Assert.IsTrue(hud.TipShown, "The slide tip shows.");
            }

            // The finger slides the basket under the items: catches into the basket, the score, the streak.
            var pilot = new Pilot(stage, controller);
            int frames = 0;
            while (controller.Caught < 8 && frames++ < 60 * 40)
            {
                pilot.Steer(NutDropItemKind.None);
                yield return null;
                AssertClear(stage);
            }
            Assert.GreaterOrEqual(controller.Caught, 8, "The basket under the items catches them.");
            Assert.AreEqual(3, stage.TutorialStep, "The first catch ends the paw.");
            if (hud != null)
                Assert.IsFalse(hud.GestureShown, "The paw went with the first catch.");
            Assert.Greater(controller.Score, 0, "Catches score.");
            Assert.Greater(controller.BestStreak, 0, "Catches in a row make a streak.");
            Assert.Greater(stage.PileCount, 0, "Caught items settle in the basket.");
            Assert.LessOrEqual(stage.PileCount, NutDropLayout.PileMax, "Three at most in the pile (the top one swapped).");
            Assert.LessOrEqual(Mathf.Abs(stage.Basket.position.x - (stage.Set.Data.origin.position.x + game.BasketX * NutDropLayout.FieldScale)), 1e-3f,
                "The basket is where the game's basket is.");

            // A pinecone in the basket resets the streak (a soft bonk, never a scolding).
            int pinecones = controller.PineconesCaught;
            frames = 0;
            while (controller.PineconesCaught == pinecones && frames++ < 60 * 40)
            {
                pilot.Steer(NutDropItemKind.Pinecone);
                yield return null;
                AssertClear(stage);
            }
            Assert.Greater(controller.PineconesCaught, pinecones, "A pinecone caught.");
            Assert.AreEqual(0, controller.Streak, "A pinecone resets the streak.");
            Assert.IsTrue(stage.PineconeTipShown, "The first game tells « Évite les pommes de pin » at the first pinecone.");

            // No allocation per frame in play: the game, the stage, the finger, catches and their effects, after the warm-up.
            for (int f = 0; f < 10; f++)
            {
                pilot.Steer(NutDropItemKind.None);
                yield return null;
            }
            int caughtBefore = controller.Caught;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 240; frame++)
            {
                pilot.Steer(NutDropItemKind.None);
                controller.Tick(1f / 60f);
                stage.Tick(1f / 60f, 1f / 60f);
                stage.LateTick();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Greater(controller.Caught, caughtBefore, "The measured frames hold catches.");
            Assert.AreEqual(0, allocated, "The nut drop allocates nothing per frame in play.");

            // A special (each falls at most once, 50 % each, between 15 and 45 s): caught, its effect starts, the boost chip.
            Time.timeScale = 3f;
            bool special = controller.SpecialCount > 0;
            frames = 0;
            while (special && game.ActiveSpecial == NutDropItemKind.None && game.Elapsed < 50f && presenter.Playing && frames++ < 60 * 60)
            {
                pilot.Steer(SpecialFalling(controller));
                yield return null;
                AssertClear(stage);
            }
            Time.timeScale = 1f;
            if (special && game.ActiveSpecial != NutDropItemKind.None)
            {
                // The stage reads the game's events in its own Update, before or after the game's tick depending on the
                // scripts' order: the ring (0.75 s) shows within a few frames.
                for (int wait = 0; wait < 5 && !stage.RingShown; wait++)
                    yield return null;
                Assert.IsTrue(stage.RingShown, "A soft ring around the basket (H-VFX-ACT-003-04).");
                if (hud != null)
                    Assert.IsTrue(hud.BoostShown, "The HUD's boost chip shows the special.");
                if (game.ActiveSpecial == NutDropItemKind.WideBasket)
                {
                    yield return new WaitForSeconds(0.6f);
                    Assert.AreEqual(game.BasketWidth * NutDropLayout.FieldScale * 0.5f, stage.BasketHalfWidth, 0.01f, "The basket widens with the game's.");
                }
            }
            else
            {
                TestContext.WriteLine($"No special caught this game (planned {controller.SpecialCount}).");
            }

            // The rest of the game, quicker: the finger keeps catching; the time ends it.
            Time.timeScale = 4f;
            frames = 0;
            while (presenter.Playing && frames++ < 60 * 120)
            {
                pilot.Steer(NutDropItemKind.None);
                yield return null;
                AssertClear(stage);
            }
            Time.timeScale = 1f;
            Assert.IsFalse(presenter.Playing, "The time ends the game.");
            yield return WaitFor(() => ResultOpen(), 3f, "The result card shows over the set.");
            Assert.IsTrue(stage.Shown, "The set stays under the result card.");
            Assert.IsFalse(refugeCamera.enabled);
            var result = services.Runner.LastResult;
            Assert.AreEqual(controller.Score, result.Score);
            Assert.IsTrue(services.Rules.TryGet(ActivityId.NutRain, out var rules), "Nut Drop's rules ship (NS).");
            var thresholds = rules.Free;
            int stars = result.Score >= thresholds.Three ? 3 : result.Score >= thresholds.Two ? 2 : result.Score >= thresholds.One ? 1 : 0;
            Assert.AreEqual(stars, result.Stars, "Stars from the free-mode thresholds (500, 700, 900).");
            Assert.IsTrue(result.Rewarded, "A ticket game is rewarded.");
            if (stars > 0)
            {
                var rewards = result.Rewards;
                Assert.Greater(rewards.LightMinutes, 0.0, "Its rewards are paid.");
                int hazelnuts = Mathf.Min(NutDropRules.HazelnutIngredientMax, controller.HazelnutsCaught / NutDropRules.HazelnutsPerIngredient);
                Assert.AreEqual(hazelnuts, rewards.CountItem0 == NutDropRules.HazelnutIngredient ? rewards.CountItems0 : 0,
                    "1 Hazelnut per 10 hazelnuts caught, 3 at most (doc 01).");
                Assert.AreEqual(stars == 3 ? 1 : 0, rewards.ItemTreats, "1 Toasted Hazelnut at 3 stars (doc 01).");
            }
            Debug.Log($"[NutDropPlayTests] score {result.Score}, {result.Stars} stars, caught {controller.Caught} (hazelnuts {controller.HazelnutsCaught}, berries {controller.BerriesCaught}, pinecones {controller.PineconesCaught}), best streak {controller.BestStreak}");

            // Back to the hub: the refuge renders again; closing the hub releases the set.
            presenter.BackToHub();
            yield return null;
            Assert.IsTrue(refugeCamera.enabled, "The refuge is back.");
            Assert.IsFalse(stage.Shown);
            presenter.CloseActivities();
            yield return null;
            Assert.IsFalse(stage.SetLoaded, "Leaving the activities releases Activity_NutDrop.");
            Assert.IsNull(stage.Set);
#else
            yield break;
#endif
        }

        /// <summary>Collision rule on every frame: items, basket, animal and meadow never enter each other.</summary>
        private static void AssertClear(NutDropStage stage)
        {
            string overlap = stage.CheckOverlaps();
            Assert.IsNull(overlap, "Nothing enters anything: " + overlap);
            if (stage.Shown && stage.Basket != null && stage.Basket.gameObject.activeSelf)
                Assert.GreaterOrEqual(stage.BasketLift, stage.SeatLift - 1e-3f, "The basket never sinks into the head.");
            if (stage.Animal != null)
                Assert.GreaterOrEqual(stage.Animal.transform.position.y, stage.FloorY - 1e-3f, "The paws stand on the meadow, never in it.");
        }

        /// <summary>The special in the air, if any (the finger goes for it).</summary>
        private static NutDropItemKind SpecialFalling(NutDropController controller)
        {
            for (int s = 0; s < controller.ItemCapacity; s++)
            {
                if (!controller.ItemActive(s) || controller.ItemMissed(s))
                    continue;
                var kind = controller.ItemKind(s);
                if (kind == NutDropItemKind.WideBasket || kind == NutDropItemKind.Feather || kind == NutDropItemKind.GoldenShower)
                    return kind;
            }
            return NutDropItemKind.None;
        }

        /// <summary>A finger that slides the basket under the next item to catch (or the kind it is asked for); no allocation.</summary>
        private sealed class Pilot
        {
            private readonly NutDropStage _stage;
            private readonly NutDropController _game;
            private bool _down;

            public Pilot(NutDropStage stage, NutDropController game)
            {
                _stage = stage;
                _game = game;
            }

            public void Steer(NutDropItemKind chase)
            {
                int best = -1;
                float soonest = float.MaxValue;
                for (int s = 0; s < _game.ItemCapacity; s++)
                {
                    if (!_game.ItemActive(s) || _game.ItemMissed(s))
                        continue;
                    var kind = _game.ItemKind(s);
                    bool wanted = chase != NutDropItemKind.None ? kind == chase : kind != NutDropItemKind.Pinecone;
                    float t = _game.ItemRimSeconds(s);
                    if (!wanted || t <= 0f || t >= soonest)
                        continue;
                    soonest = t;
                    best = s;
                }
                if (best < 0)
                    return;
                var screen = new Vector2(_stage.ScreenX(_game.ItemRimX(best)), 300f);
                if (!_down)
                {
                    _stage.PointerDown(screen);
                    _down = true;
                }
                else
                {
                    _stage.PointerMove(screen);
                }
            }
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

        /// <summary>Five animals of the three Prairie species, two squirrels among them (the stage picks a squirrel).</summary>
        private void SeedFiveAnimals()
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            var animals = new[]
            {
                ("species.rabbit", "variant.rabbit.brown"), ("species.hedgehog", "variant.hedgehog.brown"), ("species.squirrel", "variant.squirrel.red"),
                ("species.rabbit", "variant.rabbit.white"), ("species.squirrel", "variant.squirrel.grey"),
            };
            for (int i = 0; i < animals.Length; i++)
            {
                var species = ContentId.FromSource(animals[i].Item1);
                state.Collection.Add(new AnimalInstance
                {
                    Species = species, Variant = ContentId.FromSource(animals[i].Item2), Rarity = RarityTier.Common,
                    Seed = SeedHash.Mix((uint)i + 1u, 0x4E555444u),
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
