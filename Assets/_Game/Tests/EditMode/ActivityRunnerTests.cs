using System;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>A bond sink that counts without allocating.</summary>
    internal sealed class CountingBondSink : IBondSink
    {
        public int Calls;
        public int Points;
        public uint LastAnimal;
        public BondSource LastSource;

        public int AddBond(uint animal, int points, BondSource source)
        {
            Calls++;
            Points += points;
            LastAnimal = animal;
            LastSource = source;
            return points;
        }
    }

    /// <summary>
    /// The framework's dummy activity: its score is set by the test, it ends on its own after <see cref="EndAfter"/> seconds
    /// (0: never; the runner's timer ends it). Counts what the runner calls.
    /// </summary>
    internal sealed class DummyActivityController : IActivityController
    {
        public ActivitySetup Setup;
        public int Begins;
        public int Ticks;
        public int Pauses;
        public int Resumes;
        public float Elapsed;
        public float EndAfter;
        public int NextScore;
        public int NextChallengeScore = -1;

        public int Score { get; private set; }
        public int ChallengeScore => NextChallengeScore >= 0 ? NextChallengeScore : Score;
        public int Streak { get; private set; }
        public bool Finished { get; private set; }

        public void Begin(in ActivitySetup setup)
        {
            Setup = setup;
            Begins++;
            Elapsed = 0f;
            Score = 0;
            Finished = false;
        }

        public void Tick(float deltaSeconds)
        {
            Ticks++;
            Elapsed += deltaSeconds;
            Score = NextScore;
            Streak = Ticks;
            if (EndAfter > 0f && Elapsed >= EndAfter)
                Finished = true;
        }

        public void Pause() => Pauses++;

        public void Resume() => Resumes++;
    }

    /// <summary>
    /// A small refuge for the activity tests (the T36 wish rig's content: five Prairie species and a Forest one), with real
    /// economy, hatch, species and biome mastery, progression services, a UTC calendar, a counting bond sink, and the
    /// test activity's rules registered (only tests register them).
    /// </summary>
    internal sealed class ActivityTestRig
    {
        public static readonly ContentId Grassland = WishTestRig.Grassland;
        public static readonly ContentId CloverCrunch = WishTestRig.CloverCrunch;

        public PlayerState State;
        public ActivityState Activities;
        public FakeGameClock Clock;
        public ContentRegistry Registry;
        public EconomyService Economy;
        public MasteryService SpeciesMastery;
        public HatchService Hatch;
        public ProgressionServices P;
        public CountingBondSink Bond;
        public ActivityConfig Config;
        public ActivityServices S;
        public DummyActivityController Controller = new DummyActivityController();

        public static ActivityTestRig Create(int animals = 6, int level = 3, ActivityConfig config = null, int profile = 37,
            long gems = 0, Action<PlayerState> beforeServices = null)
        {
            var rig = new ActivityTestRig
            {
                Registry = WishTestRig.BuildContent(),
                State = PlayerState.CreateNew(TestUtil.T0),
                Activities = new ActivityState(),
                Clock = new FakeGameClock(TestUtil.T0),
                Bond = new CountingBondSink(),
                Config = config ?? new ActivityConfig(),
            };
            rig.State.Meta.ProfileId = new Guid(profile, 7, 11, 1, 2, 3, 4, 5, 6, 7, 8);
            var progressionConfig = new ProgressionConfig { TimeZone = TimeZoneInfo.Utc };
            rig.Economy = new EconomyService(rig.State, rig.Registry, EconomyConfig.Default, null);
            rig.SpeciesMastery = new MasteryService(rig.State, rig.Economy.Config, rig.Economy);
            rig.Hatch = new HatchService(rig.State, rig.Registry, rig.Economy, rig.Clock, null, rig.SpeciesMastery);
            rig.P = new ProgressionServices(rig.State, rig.Registry, rig.Economy, rig.Hatch, rig.Clock, null, progressionConfig,
                rig.SpeciesMastery);
            rig.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(1e12), EconomyReason.Debug);
            for (int i = 0; i < animals; i++)
                Assert.IsTrue(rig.Hatch.Hatch(WishTestRig.BasicEgg).Succeeded, "Rig hatch " + i);
            rig.State.Progression.Refuge.Level = level;
            if (gems > 0)
                rig.Economy.Grant(CurrencyId.Gems, BigValue.FromDouble(gems), EconomyReason.Debug);
            beforeServices?.Invoke(rig.State);
            rig.S = new ActivityServices(rig.State, rig.Activities, rig.Registry, rig.Economy, rig.Hatch, rig.SpeciesMastery, rig.P, rig.Bond,
                rig.Clock, null, rig.Config);
            rig.S.Rules.Register(TestRules(ActivityId.Test));
            rig.S.OnResume();
            return rig;
        }

        /// <summary>The test activity's rules: free mode 100 / 200 / 300, two parameters, 10 challenges at 50 / 100 / 150.</summary>
        public static ActivityRules TestRules(ActivityId activity)
        {
            var challenges = new ChallengeRule[ActivityCatalog.ChallengesPerActivity];
            for (int i = 0; i < challenges.Length; i++)
                challenges[i] = new ChallengeRule(new StarThresholds(50, 100, 150), level: i == 0 ? 1 : 5,
                    overrides: i == 1 ? new[] { 9.5, double.NaN } : null, seconds: i == 2 ? 20f : 0f);
            return new ActivityRules(activity, new StarThresholds(100, 200, 300),
                new[] { new DifficultyParameter("speed", 1.0, 2.0, 4.0, 1), new DifficultyParameter("items", 10, 20, 40) },
                challenges);
        }

        public uint AnimalId(int index) => State.Collection[index].InstanceId;

        public ActivityRequest Request(ActivityMode mode, int challenge = -1, ActivityId activity = ActivityId.Test)
        {
            var request = new ActivityRequest { Activity = activity, Mode = mode, Challenge = challenge };
            request.SetAnimals(AnimalId(0));
            return request;
        }

        /// <summary>Plays one whole game: start, ticks of 0.5 s until the controller ends after 2 s, the result.</summary>
        public ActivityGameResult Play(ActivityMode mode, int score, int challenge = -1, ActivityId activity = ActivityId.Test)
        {
            var request = Request(mode, challenge, activity);
            Assert.AreEqual(ActivityStartResult.Started, S.Runner.TryStart(in request, Controller), "Start " + activity + " " + mode);
            Controller.NextScore = score;
            Controller.EndAfter = 2f;
            for (int i = 0; i < 10 && S.Runner.IsRunning; i++)
                S.Runner.Tick(0.5f);
            Assert.IsFalse(S.Runner.IsRunning, "The game ended.");
            return S.Runner.LastResult;
        }

        public ref ActivityRecord Record(ActivityId activity = ActivityId.Test) =>
            ref Activities.Records[ActivityCatalog.SlotOf(activity)];

        public long Gems => (long)Economy.Balance(CurrencyId.Gems).ToDoubleClamped();
    }

    /// <summary>
    /// T37: the whole cycle of a game through the runner with a dummy controller (start → ticks → finish → outcome →
    /// saved values → next game easier or harder), practice, quitting, the free first game, challenges, pause, time, the
    /// slower mode, seeds, analytics fields, and a tick that never allocates.
    /// </summary>
    public sealed class ActivityRunnerTests
    {
        [Test]
        public void AWholeCycle_SpendsATicket_RewardsTheStars_SavesTheRecord_AndAdaptsTheNextGame()
        {
            var rig = ActivityTestRig.Create();
            int started = 0, ended = 0;
            ActivityStartInfo startInfo = default;
            rig.S.Runner.Started += (in ActivityStartInfo info) =>
            {
                started++;
                startInfo = info;
            };
            rig.S.Runner.Ended += (in ActivityGameResult result) => ended++;
            Assert.AreEqual(5, rig.S.Tickets.Count, "The pool starts full (doc 01 A15).");
            double rate = rig.Economy.GetLightPerSecond().ToDoubleClamped();
            double lightBefore = rig.Economy.Light.ToDoubleClamped();

            var result = rig.Play(ActivityMode.Free, 250);

            Assert.AreEqual(1, started);
            Assert.AreEqual(1, ended);
            Assert.AreEqual(1, startInfo.AnalyticsMode, "Rewarded.");
            Assert.AreEqual(AdaptiveDifficulty.Start, startInfo.Difficulty);
            Assert.AreEqual(4, startInfo.Tickets, "The ticket is spent at start.");
            Assert.AreEqual(4, rig.S.Tickets.Count);
            Assert.AreEqual(1, rig.Controller.Begins);
            Assert.AreEqual(4, rig.Controller.Ticks, "Ticked until it finished (2 s in 0.5 s steps).");

            Assert.AreEqual(2, result.Stars, "250 is between 200 and 300.");
            Assert.AreEqual(250, result.Score);
            Assert.IsTrue(result.Rewarded);
            Assert.IsFalse(result.NewRecord, "The first score beats nothing.");
            Assert.IsTrue(result.FirstScore);
            Assert.AreEqual(10.0, result.Rewards.LightMinutes, 1e-9);
            Assert.AreEqual(rate * 600.0, result.Rewards.Light.ToDoubleClamped(), rate * 1e-6);
            Assert.AreEqual(lightBefore + rate * 600.0, rig.Economy.Light.ToDoubleClamped(), lightBefore * 1e-9 + 1.0);
            Assert.AreEqual(4, rig.Bond.Points, "+4 bond to the animal played.");
            Assert.AreEqual(BondSource.Activity, rig.Bond.LastSource);
            Assert.AreEqual(2.0, result.Seconds, 1e-6);

            ref var record = ref rig.Record();
            Assert.AreEqual(250, record.Best);
            Assert.AreEqual(2, record.BestStars);
            Assert.AreEqual(1, record.GamesPlayed);
            Assert.AreEqual(1, record.RewardedGames);
            Assert.AreEqual(1, rig.Activities.GamesStarted);

            // A better score is a record: a guaranteed sticker (3 treats before the albums).
            var second = rig.Play(ActivityMode.Free, 320);
            Assert.IsTrue(second.NewRecord);
            Assert.AreEqual(3, second.Stars);
            Assert.IsTrue(second.Rewards.RecordSticker);
            Assert.GreaterOrEqual(rig.State.Progression.Treats[ActivityTestRig.CloverCrunch], 3);
            Assert.AreEqual(1, second.AnalyticsFlags & 1);

            // Two games in a row at 3 stars: the next one is a little harder.
            var third = rig.Play(ActivityMode.Free, 400);
            Assert.AreEqual(3, third.Stars);
            Assert.AreEqual(AdaptiveDifficulty.Start + 1, third.DifficultyNext);
            Assert.AreEqual(AdaptiveDifficulty.Start + 1, rig.S.Difficulty(ActivityId.Test));

            // Two at 0 stars: back down.
            rig.Play(ActivityMode.Free, 10);
            var fifth = rig.Play(ActivityMode.Free, 0);
            Assert.AreEqual(0, fifth.Stars);
            Assert.IsFalse(fifth.Rewards.Light.ToDoubleClamped() > 0.0, "0 stars: nothing.");
            Assert.AreEqual(AdaptiveDifficulty.Start, fifth.DifficultyNext);
            Assert.AreEqual(5, rig.Record().GamesPlayed);
            Assert.AreEqual(0, rig.S.Tickets.Count, "Five rewarded games, five tickets.");
        }

        [Test]
        public void Practice_GivesNothing_SpendsNothing_KeepsNoRecord_ButCountsForTheDifficulty()
        {
            var rig = ActivityTestRig.Create();
            double light = rig.Economy.Light.ToDoubleClamped();
            var first = rig.Play(ActivityMode.Practice, 500);
            var second = rig.Play(ActivityMode.Practice, 500);

            Assert.AreEqual(5, rig.S.Tickets.Count, "Practice is free and unlimited.");
            Assert.IsFalse(first.Rewarded);
            Assert.AreEqual(3, first.Stars, "Stars are still shown.");
            Assert.AreEqual(2, first.AnalyticsMode);
            Assert.AreEqual(light, rig.Economy.Light.ToDoubleClamped(), 1e-6);
            Assert.AreEqual(0, rig.Bond.Calls);
            Assert.AreEqual(0, rig.Record().Best, "Practice never sets records.");
            Assert.AreEqual(0, rig.Record().RewardedGames);
            Assert.AreEqual(2, rig.Record().GamesPlayed);
            Assert.AreEqual(AdaptiveDifficulty.Start + 1, second.DifficultyNext, "Practice games count for d (doc 01, section 12.0).");
        }

        [Test]
        public void NoTicket_RefusesARewardedGame_ButPracticeStaysOpen()
        {
            var rig = ActivityTestRig.Create();
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(rig.S.Tickets.TrySpend(out _));
            var free = rig.Request(ActivityMode.Free);
            Assert.AreEqual(ActivityStartResult.NoTicket, rig.S.Runner.CanStart(in free));
            Assert.AreEqual(ActivityStartResult.NoTicket, rig.S.Runner.TryStart(in free, rig.Controller));
            Assert.IsFalse(rig.S.Runner.IsRunning);
            Assert.AreEqual(0, rig.Activities.GamesStarted, "Nothing started.");
            var practice = rig.Request(ActivityMode.Practice);
            Assert.AreEqual(ActivityStartResult.Started, rig.S.Runner.CanStart(in practice));
            Assert.AreEqual(ActivityStartResult.Started, rig.S.Runner.TryStart(in practice, rig.Controller));
        }

        [Test]
        public void QuitWithinThreeSeconds_GivesTheTicketBack_Later_Not_AndNeverRewards()
        {
            var rig = ActivityTestRig.Create();
            int ended = 0;
            ActivityGameResult last = default;
            rig.S.Runner.Ended += (in ActivityGameResult result) =>
            {
                ended++;
                last = result;
            };
            var request = rig.Request(ActivityMode.Free);
            Assert.AreEqual(ActivityStartResult.Started, rig.S.Runner.TryStart(in request, rig.Controller));
            Assert.AreEqual(4, rig.S.Tickets.Count);
            rig.Controller.NextScore = 999;
            rig.S.Runner.Tick(2.5f);
            Assert.IsTrue(rig.S.Runner.Quit());
            Assert.AreEqual(5, rig.S.Tickets.Count, "Quit at 2.5 s: the ticket comes back.");
            Assert.IsTrue(last.Abandoned);
            Assert.IsTrue(last.Refunded);
            Assert.AreEqual(2, last.AnalyticsFlags & 2);
            Assert.IsFalse(last.Rewarded);

            Assert.AreEqual(ActivityStartResult.Started, rig.S.Runner.TryStart(in request, rig.Controller));
            rig.S.Runner.Tick(1.5f);
            rig.S.Runner.Tick(1.6f);
            Assert.IsTrue(rig.S.Runner.Quit());
            Assert.AreEqual(4, rig.S.Tickets.Count, "Quit at 3.1 s: the ticket is used.");
            Assert.IsFalse(last.Refunded);
            Assert.AreEqual(2, ended);
            Assert.AreEqual(0, rig.Record().Best, "An abandon records nothing.");
            Assert.AreEqual(0, rig.Record().GamesPlayed);
            Assert.AreEqual(0, rig.Bond.Calls);
            Assert.IsFalse(rig.S.Runner.Quit(), "Nothing runs any more.");
        }

        [Test]
        public void TheFirstGroomingGame_IsOffered_WithFullRewards_ThenTicketsAgain()
        {
            var rig = ActivityTestRig.Create(animals: 6, level: 3);
            rig.S.Rules.Register(ActivityTestRig.TestRules(ActivityId.Grooming));
            Assert.IsTrue(rig.S.IsUnlocked(ActivityId.Grooming), "Level 3 and 5 animals.");
            Assert.IsTrue(rig.S.FreeGameAvailable(ActivityId.Grooming));

            var first = rig.Play(ActivityMode.Free, 150, activity: ActivityId.Grooming);
            Assert.IsTrue(first.FreeGame);
            Assert.IsTrue(first.Rewarded);
            Assert.AreEqual(5, rig.S.Tickets.Count, "No ticket for the offered game.");
            Assert.AreEqual(5.0, first.Rewards.LightMinutes, 1e-9);
            Assert.IsFalse(rig.S.FreeGameAvailable(ActivityId.Grooming));

            var second = rig.Play(ActivityMode.Free, 150, activity: ActivityId.Grooming);
            Assert.IsFalse(second.FreeGame);
            Assert.AreEqual(4, rig.S.Tickets.Count);
        }

        [Test]
        public void AQuitFreeGame_StaysFree()
        {
            var rig = ActivityTestRig.Create();
            rig.S.Rules.Register(ActivityTestRig.TestRules(ActivityId.Grooming));
            var request = rig.Request(ActivityMode.Free, activity: ActivityId.Grooming);
            Assert.AreEqual(ActivityStartResult.Started, rig.S.Runner.TryStart(in request, rig.Controller));
            rig.S.Runner.Tick(10f);
            rig.S.Runner.Quit();
            Assert.IsTrue(rig.S.FreeGameAvailable(ActivityId.Grooming));
            Assert.AreEqual(5, rig.S.Tickets.Count);
        }

        [Test]
        public void Challenges_OpenInOrder_KeepTheirStars_NeverAdapt_AndPayOnce()
        {
            var rig = ActivityTestRig.Create();
            int improvedCalls = 0, improvedAttempts = 0;
            rig.S.Runner.ChallengeImproved += (activity, challenge, stars, attempts) =>
            {
                improvedCalls++;
                improvedAttempts = attempts;
            };
            var second = rig.Request(ActivityMode.Challenge, 1);
            Assert.AreEqual(ActivityStartResult.ChallengeLocked, rig.S.Runner.TryStart(in second, rig.Controller));
            Assert.AreEqual(5, rig.S.Tickets.Count, "A locked challenge spends nothing.");

            var low = rig.Play(ActivityMode.Challenge, 60, challenge: 0);
            Assert.AreEqual(1, low.Stars);
            Assert.IsTrue(low.ChallengeImproved);
            Assert.AreEqual(3, low.AnalyticsMode);
            Assert.AreEqual(1, rig.Controller.Setup.Difficulty, "Challenge 1 is at d1, fixed.");
            Assert.IsTrue(rig.S.IsChallengeOpen(ActivityId.Test, 1), "One star opens the next.");
            Assert.AreEqual(AdaptiveDifficulty.Start, rig.S.Difficulty(ActivityId.Test), "Challenges never move d.");

            long gems = rig.Gems;
            var top = rig.Play(ActivityMode.Challenge, 160, challenge: 0);
            Assert.AreEqual(3, top.Stars);
            Assert.AreEqual(5, top.Rewards.ChallengeGems, "A challenge's first 3 stars: 5 Gems.");
            Assert.AreEqual(2, top.Rewards.DailyGems, "And the first 3 stars of the day.");
            Assert.AreEqual(gems + 7, rig.Gems);
            Assert.AreEqual(2, improvedCalls);
            Assert.AreEqual(2, improvedAttempts);
            Assert.AreEqual(1, rig.Activities.ChallengesThreeStars);
            Assert.AreEqual(1, rig.S.ChallengesDone(ActivityId.Test));

            var again = rig.Play(ActivityMode.Challenge, 160, challenge: 0);
            Assert.AreEqual(0, again.Rewards.ChallengeGems, "Once only.");
            Assert.IsFalse(again.ChallengeImproved);
            Assert.AreEqual(2, improvedCalls);

            // A practiced challenge records nothing.
            var practiced = rig.Play(ActivityMode.Practice, 160, challenge: 1);
            Assert.AreEqual(3, practiced.Stars);
            Assert.AreEqual(0, rig.S.ChallengeStars(ActivityId.Test, 1));
            Assert.AreEqual(9.5, rig.Controller.Setup.Param(0), 1e-9, "Challenge 2 fixes its speed.");
            Assert.AreEqual(20, rig.Controller.Setup.ParamInt(1), "Its other parameter stays at its level (d5).");
        }

        [Test]
        public void Pause_StopsTheTicksAndTheTime()
        {
            var rig = ActivityTestRig.Create();
            var request = rig.Request(ActivityMode.Practice);
            rig.Controller.EndAfter = 0f;
            Assert.AreEqual(ActivityStartResult.Started, rig.S.Runner.TryStart(in request, rig.Controller));
            rig.S.Runner.Tick(1f);
            rig.S.Runner.Pause();
            Assert.IsTrue(rig.S.Runner.IsPaused);
            Assert.AreEqual(1, rig.Controller.Pauses);
            rig.S.Runner.Tick(100f);
            Assert.AreEqual(1, rig.Controller.Ticks);
            Assert.AreEqual(1.0, rig.S.Runner.Elapsed, 1e-9);
            Assert.AreEqual(29f, rig.S.Runner.TimeLeft, 1e-4f);
            rig.S.Runner.Resume();
            Assert.AreEqual(1, rig.Controller.Resumes);
            rig.S.Runner.Tick(1f);
            Assert.AreEqual(2, rig.Controller.Ticks);
        }

        [Test]
        public void TheTimer_EndsAGame_ThatDoesNotEndItself()
        {
            var rig = ActivityTestRig.Create();
            var request = rig.Request(ActivityMode.Free);
            Assert.AreEqual(ActivityStartResult.Started, rig.S.Runner.TryStart(in request, rig.Controller));
            rig.Controller.EndAfter = 0f;
            rig.Controller.NextScore = 120;
            for (int i = 0; i < 29; i++)
                rig.S.Runner.Tick(1f);
            Assert.IsTrue(rig.S.Runner.IsRunning);
            rig.S.Runner.Tick(1f);
            Assert.IsFalse(rig.S.Runner.IsRunning, "30 s: the test activity's time.");
            Assert.AreEqual(1, rig.S.Runner.LastResult.Stars);
        }

        [Test]
        public void SlowerActivities_StretchTheTime_AndSlowTheSpeeds_WithTheSameThresholds()
        {
            var rig = ActivityTestRig.Create();
            rig.S.SlowerActivities = true;
            var request = rig.Request(ActivityMode.Practice);
            Assert.AreEqual(ActivityStartResult.Started, rig.S.Runner.TryStart(in request, rig.Controller));
            Assert.AreEqual(37.5f, rig.Controller.Setup.Seconds, 1e-4f);
            Assert.AreEqual(0.8f, rig.Controller.Setup.SpeedMultiplier, 1e-6f);
            Assert.AreEqual(100, rig.Controller.Setup.Thresholds.One);
            rig.S.Runner.Quit();

            var challenge = rig.Request(ActivityMode.Practice, 2);
            rig.Activities.ChallengeStars[ActivityState.ChallengeIndex(ActivityCatalog.TestSlot, 1)] = 1;
            Assert.AreEqual(ActivityStartResult.Started, rig.S.Runner.TryStart(in challenge, rig.Controller));
            Assert.AreEqual(25f, rig.Controller.Setup.Seconds, 1e-4f, "Challenge 3 lasts 20 s, × 1.25.");
        }

        [Test]
        public void EachGame_HasADeterministicSeed()
        {
            var a = ActivityTestRig.Create(profile: 5);
            var b = ActivityTestRig.Create(profile: 5);
            var c = ActivityTestRig.Create(profile: 6);
            a.Play(ActivityMode.Practice, 0);
            b.Play(ActivityMode.Practice, 0);
            c.Play(ActivityMode.Practice, 0);
            uint first = a.Controller.Setup.Seed;
            Assert.AreEqual(first, b.Controller.Setup.Seed, "Same profile, same game: same seed.");
            Assert.AreNotEqual(first, c.Controller.Setup.Seed);
            a.Play(ActivityMode.Practice, 0);
            Assert.AreNotEqual(first, a.Controller.Setup.Seed, "The next game has its own seed.");
            Assert.AreEqual(a.S.Context.GameSeed(1, ActivityId.Test), a.Controller.Setup.Seed);
        }

        [Test]
        public void TheTheme_DefaultsToTheBaseBiome_AndTheLastChoiceIsKept()
        {
            var rig = ActivityTestRig.Create();
            Assert.AreEqual(ActivityTestRig.Grassland, rig.S.Theme(ActivityId.Test));
            var forest = WishTestRig.Forest;
            var request = rig.Request(ActivityMode.Practice);
            request.Theme = forest;
            Assert.AreEqual(ActivityStartResult.Started, rig.S.Runner.TryStart(in request, rig.Controller));
            Assert.AreEqual(forest, rig.Controller.Setup.Theme);
            rig.S.Runner.Quit();
            Assert.AreEqual(forest, rig.S.Theme(ActivityId.Test));
            var plain = rig.Request(ActivityMode.Practice);
            Assert.AreEqual(ActivityStartResult.Started, rig.S.Runner.TryStart(in plain, rig.Controller));
            Assert.AreEqual(forest, rig.Controller.Setup.Theme, "The last choice is kept.");
        }

        [Test]
        public void ARefusedStart_SaysWhy()
        {
            var rig = ActivityTestRig.Create();
            var busy = rig.Request(ActivityMode.Practice);
            Assert.AreEqual(ActivityStartResult.Started, rig.S.Runner.TryStart(in busy, rig.Controller));
            Assert.AreEqual(ActivityStartResult.Busy, rig.S.Runner.TryStart(in busy, rig.Controller));
            rig.S.Runner.Quit();

            var noRules = rig.Request(ActivityMode.Practice, activity: ActivityId.Grooming);
            Assert.AreEqual(ActivityStartResult.NoRules, rig.S.Runner.TryStart(in noRules, rig.Controller));
            var meta = rig.Request(ActivityMode.Free, activity: ActivityId.Garden);
            Assert.AreEqual(ActivityStartResult.Meta, rig.S.Runner.TryStart(in meta, rig.Controller));
            var locked = rig.Request(ActivityMode.Practice, activity: ActivityId.HideAndSeek);
            rig.S.Rules.Register(ActivityTestRig.TestRules(ActivityId.HideAndSeek));
            Assert.AreEqual(ActivityStartResult.Locked, rig.S.Runner.TryStart(in locked, rig.Controller), "Level 3: Hide and Seek waits.");

            rig.Config.TryOverrideFlag("flags.activities", false);
            Assert.AreEqual(ActivityStartResult.Disabled, rig.S.Runner.TryStart(in busy, rig.Controller));
            rig.Config.TryOverrideFlag("flags.activities", true);
            var badChallenge = rig.Request(ActivityMode.Challenge, 10);
            Assert.AreEqual(ActivityStartResult.Unknown, rig.S.Runner.TryStart(in badChallenge, rig.Controller));
        }

        [Test]
        public void TheGameTick_DoesNotAllocate()
        {
            var rig = ActivityTestRig.Create();
            var request = rig.Request(ActivityMode.Free);
            Assert.AreEqual(ActivityStartResult.Started, rig.S.Runner.TryStart(in request, rig.Controller));
            rig.Controller.EndAfter = 0f;
            rig.Controller.NextScore = 10;
            rig.S.Runner.Tick(0.016f);
            Assert.That(() => rig.S.Runner.Tick(0.016f), Is.Not.AllocatingGCMemory());
            int score = 0;
            float left = 0f;
            Assert.That(new TestDelegate(() =>
            {
                score = rig.S.Runner.Score;
                left = rig.S.Runner.TimeLeft;
            }), Is.Not.AllocatingGCMemory());
            Assert.AreEqual(10, score);
            Assert.Greater(left, 0f);
        }

        [Test]
        public void TheServicesTick_DoesNotAllocate()
        {
            var rig = ActivityTestRig.Create();
            Assert.IsTrue(rig.S.Tickets.TrySpend(out _));
            rig.S.Tick(1.0);
            Assert.That(() => rig.S.Tick(0.016), Is.Not.AllocatingGCMemory());
            Assert.That(() => rig.S.Tick(1.0), Is.Not.AllocatingGCMemory(), "A refill check without a boundary.");
            int minutes = 0;
            Assert.That(new TestDelegate(() => { minutes = rig.S.Tickets.MinutesToNext; }), Is.Not.AllocatingGCMemory());
            Assert.Greater(minutes, 0);
        }

        [Test]
        public void Dispose_StopsListening()
        {
            var rig = ActivityTestRig.Create();
            rig.S.Dispose();
            Assert.DoesNotThrow(() => rig.Hatch.Hatch(WishTestRig.BasicEgg));
        }
    }
}
