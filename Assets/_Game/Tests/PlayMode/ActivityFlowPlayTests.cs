using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T37 « Cadre des activités », queue criterion: a dummy activity goes through the whole cycle in the real game (doc
    /// « Mini-jeux &amp; activités » sections 1 and 2, doc 01 Contenu section 12.0). The Play button appears, the hub opens
    /// on the activity, a rewarded game spends a ticket, runs on its stage with the activity HUD, ends on its rule, shows
    /// its result card and pays its rewards; a replay reveals at once; a game quit in its first seconds gives the ticket
    /// back; practice is free and pays nothing; with no ticket left the « no ticket » card offers practice and a pack; the
    /// pause card pauses the game; the hub closes back to the refuge, with the refuge HUD back.
    /// </summary>
    public class ActivityFlowPlayTests
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

        [UnityTest]
        public IEnumerator DummyActivity_GoesThroughTheWholeCycle()
        {
#if UNITY_EDITOR
            var clock = new FakeGameClock(new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero));
            GameBootstrap bootstrap = null;
            yield return Boot(clock, b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var presenter = entry.Activities;
            Assert.IsNotNull(presenter, "The scene runs the activities.");
            var services = context.Activities;
            var state = context.State;
            var hud = Object.FindAnyObjectByType<RefugeHud>();

            // A new profile: Grooming (T38) and Hide and Seek (T39) exist but open at refuge level 3 with 5 animals and level 4
            // with 8: no Play button yet.
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(services.AnyShipped, "Grooming ships with T38.");
            Assert.IsFalse(presenter.PlayAvailable, "No activity open yet: no Play button.");

            // The test registers the dummy activity (rules and stage), as an activity task registers its own.
            var stage = new DummyStage();
            services.Rules.Register(DummyRules());
            presenter.Stages = Registry(stage);
            services.RefreshUnlocks();
            yield return WaitFor(() => presenter.PlayAvailable && hud.PlayVisible, 3f, "The Play button appears once an activity is open.");

            // The hub opens on the activity, with a full ticket reserve.
            presenter.OpenActivities();
            yield return null;
            Assert.IsTrue(presenter.HubOpen, "Play opens the hub.");
            Assert.AreEqual(3, presenter.CardCount, "The hub lists the activities of this build: the test one, Grooming and Hide and Seek still closed.");
            Assert.AreEqual(ActivityId.Test, presenter.Card(0).Activity, "The open ones first.");
            Assert.AreEqual(ActivityId.Grooming, presenter.Card(1).Activity);
            Assert.AreEqual(ActivityCardState.Locked, presenter.Card(1).State);
            Assert.AreEqual(ActivityId.HideAndSeek, presenter.Card(2).Activity, "T39: Hide and Seek ships, closed until level 4 and 8 animals.");
            Assert.AreEqual(ActivityCardState.Locked, presenter.Card(2).State);
            Assert.IsFalse(string.IsNullOrEmpty(presenter.Card(1).UnlockHint), "A closed card says what opens it.");
            Assert.AreEqual(ActivityCardState.New, presenter.Card(0).State, "Never played: « Nouveau ».");
            Assert.AreEqual(5, presenter.Tickets.Count, "The first use fills the reserve (doc 01 A15).");
            Assert.AreEqual(ActivityCatalog.ChallengesPerActivity, presenter.ChallengeCount);
            Assert.IsFalse(presenter.Challenge(0).Locked, "The first challenge is open.");
            Assert.IsTrue(presenter.Challenge(1).Locked, "The next one waits for a star on the first.");
            Assert.IsTrue(entry.Refuge.ModalOpen, "The hub covers the refuge: world touches wait.");

            // A rewarded game: a ticket spent, the stage and the activity HUD, the refuge HUD away.
            presenter.Play();
            yield return null;
            Assert.IsTrue(presenter.Playing, "The game runs.");
            Assert.IsFalse(presenter.HubOpen, "The hub steps aside.");
            Assert.AreEqual(1, stage.Enters, "The stage shows the game.");
            Assert.AreEqual(4, services.Tickets.Count, "A rewarded game costs one ticket.");
            var activityHud = Object.FindAnyObjectByType<ActivityHudScreen>();
            Assert.IsTrue(activityHud == null || activityHud.IsShown, "The activity HUD shows.");
            Assert.IsTrue(hud.PlayingMode, "The refuge HUD steps aside during a game.");

            // The game ends on its rule with three stars.
            stage.Game.ScoreNow = 320;
            yield return WaitFor(() => presenter.InGame && !presenter.Playing, 2f, "Its score reached, the game ends.");
            Assert.AreEqual(1, stage.Ends);
            yield return WaitFor(() => ResultOpen(), 3f, "The result card shows.");
            var result = services.Runner.LastResult;
            Assert.AreEqual(3, result.Stars);
            Assert.IsTrue(result.Rewarded);
            Assert.AreEqual(20.0, result.Rewards.LightMinutes, "3 stars: 20 minutes of production (doc Mini-jeux, section 2).");
            Assert.IsTrue(result.Rewards.Light > BigValue.Zero, "Its Light is paid.");
            Assert.AreEqual(320, services.Record(ActivityId.Test).Best, "The record is kept.");
            Assert.AreEqual(1, services.Record(ActivityId.Test).GamesPlayed);

            // Replay: the same activity, one more ticket.
            presenter.PlayAgain();
            yield return null;
            Assert.IsTrue(presenter.Playing, "« Rejouer » starts the next game.");
            Assert.AreEqual(3, services.Tickets.Count);
            Assert.AreEqual(2, stage.Enters);

            // Pause, then quit in the first seconds: the ticket comes back, no reward, back to the hub.
            presenter.PauseActivity();
            Assert.IsTrue(services.Runner.IsPaused);
            Assert.AreEqual(1, stage.Pauses);
            presenter.ResumeActivity();
            Assert.IsFalse(services.Runner.IsPaused);
            presenter.PauseActivity();
            presenter.QuitActivity();
            yield return null;
            Assert.IsFalse(presenter.InGame, "Quitting ends the game.");
            Assert.IsTrue(presenter.HubOpen, "Back to the hub.");
            Assert.AreEqual(4, services.Tickets.Count, "A game quit in its first seconds gives its ticket back.");
            Assert.IsTrue(services.Runner.LastResult.Abandoned && services.Runner.LastResult.Refunded);
            Assert.AreEqual(1, services.Record(ActivityId.Test).GamesPlayed, "An abandoned game is not counted.");

            // Practice: free and unlimited, no reward.
            presenter.SetPractice(true);
            presenter.Play();
            yield return null;
            Assert.IsTrue(presenter.Playing);
            Assert.AreEqual(4, services.Tickets.Count, "Practice spends no ticket.");
            stage.Game.ScoreNow = 400;
            yield return WaitFor(() => ResultOpen(), 3f, "Practice shows its result too.");
            Assert.IsFalse(services.Runner.LastResult.Rewarded);
            Assert.IsFalse(services.Runner.LastResult.Rewards.Rewarded, "Practice pays nothing.");
            Assert.AreEqual(0.0, services.Runner.LastResult.Rewards.LightMinutes);
            Assert.AreEqual(320, services.Record(ActivityId.Test).Best, "Practice never sets a record.");
            presenter.BackToHub();
            yield return null;
            Assert.IsTrue(presenter.HubOpen);
            presenter.SetPractice(false);

            // No ticket left: the « no ticket » card first, then practice from it.
            state.Activities.FreeTickets = 0;
            state.Activities.PaidTickets = 0;
            state.Wallet.Gems = 0;
            presenter.Play();
            yield return null;
            Assert.IsFalse(presenter.InGame, "No ticket: no rewarded game.");
            Assert.IsTrue(presenter.EmptyOpen, "The « no ticket » card opens instead.");
            presenter.BuyTickets();
            Assert.AreEqual(0, services.Tickets.Count, "Without Gems the pack is not bought.");
            presenter.PlayPractice();
            yield return null;
            Assert.IsTrue(presenter.Playing, "Practice is always open.");
            Assert.AreEqual(ActivityMode.Practice, services.Runner.Setup.Mode);
            presenter.QuitActivity();
            yield return null;

            // A pack for Gems from the card starts the game the player wanted.
            state.Wallet.Gems = 100;
            presenter.Play();
            yield return null;
            Assert.IsTrue(presenter.EmptyOpen);
            presenter.BuyTickets();
            yield return null;
            Assert.IsTrue(presenter.Playing, "The pack bought, the game starts.");
            Assert.AreEqual(70, state.Wallet.Gems, "3 tickets for 30 Gems (doc 02 Gemmes).");
            Assert.AreEqual(2, services.Tickets.Count, "One of the 3 tickets is spent on the game.");
            presenter.QuitActivity();
            yield return null;

            // The hub closes back to the refuge, its HUD back.
            presenter.CloseActivities();
            yield return null;
            Assert.IsFalse(presenter.IsOpen);
            Assert.IsFalse(entry.Refuge.ModalOpen, "The refuge is free again.");
            Assert.IsFalse(hud.PlayingMode, "The refuge HUD is back.");
            Assert.AreEqual(5, stage.Exits, "Each game left its stage.");
#else
            yield break;
#endif
        }

        private static bool ResultOpen()
        {
            var result = Object.FindAnyObjectByType<ActivityResultScreen>();
            return result != null && result.IsOpen;
        }

        private static ActivityRules DummyRules()
        {
            var challenges = new ChallengeRule[ActivityCatalog.ChallengesPerActivity];
            for (int i = 0; i < challenges.Length; i++)
                challenges[i] = new ChallengeRule(new StarThresholds(50, 100, 150), level: i == 0 ? 1 : 5);
            return new ActivityRules(ActivityId.Test, new StarThresholds(100, 200, 300),
                new[] { new DifficultyParameter("speed", 1.0, 2.0, 4.0, 1) }, challenges);
        }

        private static ActivityStageRegistry Registry(DummyStage stage)
        {
            var registry = new ActivityStageRegistry();
            registry.Register(ActivityId.Test, _ => stage);
            return registry;
        }

        /// <summary>The dummy activity's game: its score is set by the test, it ends on its own rule once a score is set.</summary>
        private sealed class DummyGame : IActivityController
        {
            public int ScoreNow;

            public int Score { get; private set; }
            public int ChallengeScore => Score;
            public int Streak { get; private set; }
            public bool Finished { get; private set; }

            public void Begin(in ActivitySetup setup)
            {
                ScoreNow = 0;
                Score = 0;
                Streak = 0;
                Finished = false;
            }

            public void Tick(float deltaSeconds)
            {
                Score = ScoreNow;
                Finished = ScoreNow > 0;
            }

            public void Pause()
            {
            }

            public void Resume()
            {
            }
        }

        /// <summary>The dummy activity's stage: nothing to show, it counts the calls.</summary>
        private sealed class DummyStage : IActivityStage
        {
            public readonly DummyGame Game = new DummyGame();
            public int Enters, Ends, Exits, Pauses;

            public ActivityId Activity => ActivityId.Test;
            public IActivityController Controller => Game;
            public bool Ready => true;

            public void Enter(in ActivitySetup setup, ActivityStageHost host) => Enters++;

            public void Tick(float deltaTime, float unscaledDeltaTime)
            {
            }

            public void SetPaused(bool paused)
            {
                if (paused)
                    Pauses++;
            }

            public void End() => Ends++;

            public void Exit() => Exits++;
        }

        private IEnumerator Boot(IGameClock clock, Action<GameBootstrap> ready)
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
                ClockOverride = clock,
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
