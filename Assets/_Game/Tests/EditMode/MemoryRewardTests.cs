using System;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// The activity rig with the rendez-vous and the memory hook (T41): the shipped rules registered as at boot (refuge
    /// level 11 on day 5, Pawprint Memory open), the real <see cref="MemoryController"/> played by a scripted player through
    /// the runner.
    /// </summary>
    internal sealed class MemoryRig
    {
        public ActivityTestRig A;
        public DailyVisitServices Daily;
        public MemoryHooks Hooks;
        public MemoryController Game = new MemoryController();

        public static MemoryRig Create(int level = 11, int day = 5, bool activitiesInBuild = true, bool garden = false)
        {
            var config = new ActivityConfig { GardenAvailable = garden };
            var rig = new MemoryRig { A = ActivityTestRig.Create(animals: 8, level: level, config: config) };
            rig.A.Clock.Set(TestUtil.T0 + TimeSpan.FromDays(day - 1));
            rig.A.S.Context.CheckDay();
            ActivityRegistry.RegisterShipped(rig.A.S);
            rig.A.S.RefreshUnlocks();
            rig.Daily = new DailyVisitServices(rig.A.State, rig.A.Registry, rig.A.Economy, rig.A.Hatch, rig.A.SpeciesMastery, null, rig.A.P,
                rig.A.Clock, null, new DailyVisitConfig { ActivitiesAvailable = activitiesInBuild });
            rig.Hooks = new MemoryHooks(rig.A.S, rig.Daily);
            return rig;
        }

        public ActivityRequest Request(ActivityMode mode, int challenge = -1)
        {
            var request = new ActivityRequest { Activity = ActivityId.PawMemory, Mode = mode, Challenge = challenge };
            request.SetAnimals(A.AnimalId(0));
            return request;
        }

        /// <summary>A whole memory game through the runner, played by <paramref name="skill"/>.</summary>
        public ActivityGameResult Play(ActivityMode mode, MemorySkill skill, int challenge = -1)
        {
            var request = Request(mode, challenge);
            Assert.AreEqual(ActivityStartResult.Started, A.S.Runner.TryStart(in request, Game), "Start memory " + mode);
            ScriptedMemoryPlayer.Play(Game, skill, A.S.Runner.Setup.Seed, dt => A.S.Runner.Tick(dt), () => A.S.Runner.IsRunning);
            Assert.IsFalse(A.S.Runner.IsRunning, "The game ended.");
            return A.S.Runner.LastResult;
        }

        public int Treats(ContentId treat) => A.State.Progression.Treats.TryGetValue(treat, out int n) ? n : 0;

        public int Items(ContentId item) => A.State.Inventory.Count(item);

        public ref ActivityRecord Record => ref A.Activities.Records[ActivityCatalog.SlotOf(ActivityId.PawMemory)];

        public int AddWish(WishTemplateId template, int target)
        {
            var visit = A.State.DailyVisit;
            int slot = visit.WishCount++;
            visit.Wishes[slot] = new WishRecord
            {
                Template = (ushort)template,
                Animal = A.AnimalId(slot % 8),
                Target = target,
                DrawnDay = Daily.TodayKey,
                ParamKind = WishCatalog.TryGet(template, out var t) ? (byte)t.ParamKind : (byte)0,
            };
            return slot;
        }

        public WishRecord Wish(int slot) => A.State.DailyVisit.Wishes[slot];

        /// <summary>A journal of this week holding JRN-052 with this target.</summary>
        public void PairsGoal(int target)
        {
            var visit = A.State.DailyVisit;
            visit.Goals[0] = new JournalGoalRecord { Goal = (ushort)JournalGoalId.MemoryPairs, Slot = (byte)JournalSlot.Activities, Target = target };
            visit.GoalCount = 1;
            visit.JournalStartKey = Daily.TodayKey;
            visit.JournalEndKey = DailyDay.AddDays(Daily.TodayKey, 7);
            visit.FirstJournalDone = true;
        }

        public int GoalProgress => A.State.DailyVisit.Goals[0].Progress;
    }

    /// <summary>
    /// T41 « Memory des empreintes » in the framework (doc 01 Contenu, S-ACT-004, sections 2.3 and 5.2; doc Mini-jeux, section
    /// 6): shipped and registered, opening at refuge level 11 on day 5, the free mode's thresholds and length per grid read by
    /// the runner at the game's d (the T41 framework extension), a whole cycle through <see cref="ActivityServices"/> (a
    /// ticket, stars, the record, rewards), the 3-star Seed Cookie, the 3-star seed behind the Garden, the wishes WISH-057 to
    /// 059 and the journal's JRN-052 through <see cref="MemoryHooks"/>, the SYS-12 draws, and allocation-free hooks.
    /// </summary>
    public sealed class MemoryRewardTests
    {
        // ---------------- shipping and the framework extension ----------------

        [Test]
        public void T41ShipsPawMemory_ItsRules_ItsCookie_AndItsSeed()
        {
            var definition = ActivityCatalog.Get(ActivityId.PawMemory);
            Assert.IsTrue(definition.ShippedInThisBuild, "T41 ships Pawprint Memory.");
            Assert.AreEqual("memory", definition.Key);
            Assert.IsFalse(definition.FirstGameFree);
            Assert.IsTrue(definition.UsesTickets, "1 ticket.");
            Assert.AreEqual(45, definition.MinSeconds);
            Assert.AreEqual(90, definition.MaxSeconds);
            Assert.AreEqual(ActivityMusic.Calm, definition.Music, "« Petite partie ».");
            var rig = MemoryRig.Create();
            var services = rig.A.S;
            Assert.IsTrue(services.Rules.Has(ActivityId.PawMemory));
            Assert.IsTrue(services.IsShipped(ActivityId.PawMemory));
            Assert.IsTrue(services.IsAvailable(ActivityId.PawMemory), "Level 11 on day 5: open.");
            Assert.IsTrue(services.IsAvailable(ActivityId.NutRain), "Nut Drop stays.");

            Assert.AreEqual(ContentId.FromSource("treat.seed_cookie"), services.Rewards.ItemReward(ActivityId.PawMemory, out int count, out int stars));
            Assert.AreEqual(1, count, "« 1 Biscuit aux graines »…");
            Assert.AreEqual(3, stars, "… à 3 étoiles.");

            Assert.AreEqual(ContentId.FromSource("plant.sweet_clover"),
                services.Rewards.CountReward(ActivityId.PawMemory, MemoryRules.SeedCount, out int per, out int max, out bool bakery),
                "« 1 graine »: Trèfle doux, the Prairie's seed.");
            Assert.AreEqual(1, per);
            Assert.AreEqual(1, max);
            Assert.IsFalse(bakery);
            Assert.IsTrue(services.Rewards.CountRewardGates(ActivityId.PawMemory, MemoryRules.SeedCount, out bool garden, out int seedStars));
            Assert.IsTrue(garden, "« Jardin ouvert ».");
            Assert.AreEqual(3, seedStars);
            Assert.AreEqual(ContentId.None, services.Rewards.CountReward(ActivityId.PawMemory, 1, out _, out _, out _));
            Assert.IsTrue(services.Rewards.CountRewardGates(ActivityId.NutRain, 1, out garden, out seedStars));
            Assert.IsFalse(garden, "Nut Drop's Strawberries wait for the Pâtisserie, not the Garden.");
            Assert.AreEqual(1, seedStars, "Nut Drop's counted items come at any star.");
            Assert.IsFalse(services.Context.GardenOpen, "No Garden before T43.");

            Assert.IsFalse(MemoryRig.Create(level: 10).A.S.IsAvailable(ActivityId.PawMemory), "Level 10: not open yet.");
            Assert.IsFalse(MemoryRig.Create(day: 4).A.S.IsAvailable(ActivityId.PawMemory), "Day 4: not open yet.");
        }

        [Test]
        public void TheRules_GiveTheThresholdsAndTheLengthPerGrid_OtherActivitiesUnchanged()
        {
            var rules = MemoryRules.Create();
            Assert.AreEqual(MemoryRules.ParameterCount, rules.ParameterCount);
            for (int d = 1; d <= 10; d++)
            {
                int grid = d <= 3 ? 0 : d <= 7 ? 1 : 2;
                Assert.AreEqual(grid, MemoryRules.GridAt(d));
                Assert.AreEqual(grid, (int)rules.Value(MemoryRules.Grid, d), "d" + d);
                Assert.AreEqual(d >= 6 ? 1.0 : 0.0, rules.Value(MemoryRules.Pawprints, d), "Pawprints from d6.");
                Assert.AreEqual(d <= 2 ? 2.0 : d <= 4 ? 1.0 : 0.0, rules.Value(MemoryRules.Preview, d), "Preview 2 / 1 / 0 s.");
                Assert.AreEqual(d >= 4 ? 1.0 : 0.0, rules.Value(MemoryRules.DoublePair, d), "The double pair from d4.");
                Assert.AreEqual(1.0, rules.Value(MemoryRules.GlowCard, d));
                Assert.AreEqual(20.0, rules.Value(MemoryRules.MistakePenalty, d));
                var free = rules.FreeAt(d);
                var expected = grid == 0 ? (500, 700, 980) : grid == 1 ? (700, 950, 1150) : (900, 1250, 1500);
                Assert.AreEqual(expected, (free.One, free.Two, free.Three), "d" + d + " thresholds.");
                Assert.AreEqual(grid == 0 ? 45f : grid == 1 ? 60f : 90f, rules.SecondsAt(d, 0f), "d" + d + " length.");
                Assert.AreEqual(free.Three, rules.Thresholds(-1, d).Three);
            }
            Assert.AreEqual(980, rules.Free.Three, "Free: the starting d's (d3, 4 × 3).");
            Assert.AreEqual(850, rules.Thresholds(MemoryRules.SmallGridChallenge, 9).Three, "A challenge keeps its own at any d.");

            // A stepped parameter holds one value per d; the others still draw their straight lines.
            var steps = DifficultyParameter.Steps("cards", 1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
            Assert.AreEqual(4.0, steps.At(4));
            Assert.AreEqual(1.0, steps.At(-3), "Clamped to d1.");
            Assert.AreEqual(10.0, steps.At(12));
            Assert.AreEqual(1.0, steps.At1);
            Assert.AreEqual(5.0, steps.At5);
            Assert.AreEqual(10.0, steps.At10);
            Assert.Throws<ArgumentException>(() => DifficultyParameter.Steps("short", 1, 2, 3));
            Assert.AreEqual(12.0, new DifficultyParameter("debris", 4, 8, 12).At(10));

            var nut = NutDropRules.Create();
            Assert.IsNull(nut.FreeByLevel);
            Assert.IsNull(nut.SecondsByLevel);
            for (int d = 1; d <= 10; d++)
            {
                Assert.AreEqual(nut.Free.Three, nut.FreeAt(d).Three, "Nut Drop's free thresholds never change with d.");
                Assert.AreEqual(60f, nut.SecondsAt(d, 60f), "Nut Drop keeps its own length.");
            }
            Assert.Throws<ArgumentException>(() => new ActivityRules(ActivityId.PawMemory, rules.Free, rules.Parameters, rules.Challenges,
                new StarThresholds[3]));
        }

        [Test]
        public void TheRunner_ReadsTheThresholdsAndTheLength_AtTheGamesD()
        {
            var rig = MemoryRig.Create();
            var runner = rig.A.S.Runner;
            foreach (var (d, seconds, three) in new[] { (3, 45f, 980), (5, 60f, 1150), (9, 90f, 1500) })
            {
                rig.Record.Difficulty = (byte)d;
                var request = rig.Request(ActivityMode.Practice);
                Assert.AreEqual(ActivityStartResult.Started, runner.TryStart(in request, rig.Game));
                Assert.AreEqual(d, runner.Setup.Difficulty);
                Assert.AreEqual(seconds, runner.Setup.Seconds, "d" + d + ": the grid's length.");
                Assert.AreEqual(three, runner.Setup.Thresholds.Three, "d" + d + ": the grid's thresholds (the HUD's marks).");
                Assert.AreEqual(seconds, rig.Game.Seconds);
                runner.Quit();
            }
            rig.Record.Difficulty = 3;
            int slot = ActivityCatalog.SlotOf(ActivityId.PawMemory);
            for (int c = 0; c < 10; c++)
                rig.A.Activities.ChallengeStars[ActivityState.ChallengeIndex(slot, c)] = 1;
            foreach (var (challenge, seconds) in new[] { (MemoryRules.TightClockChallenge, 45f), (MemoryRules.GrandChallenge, 75f),
                         (MemoryRules.BigGridChallenge, 90f), (MemoryRules.MediumGridChallenge, 60f), (MemoryRules.FlawlessChallenge, 45f) })
            {
                var request = rig.Request(ActivityMode.Practice, challenge);
                Assert.AreEqual(ActivityStartResult.Started, runner.TryStart(in request, rig.Game));
                Assert.AreEqual(seconds, runner.Setup.Seconds, "Challenge " + (challenge + 1));
                Assert.AreEqual(MemoryRules.Create().Thresholds(challenge).Three, runner.Setup.Thresholds.Three);
                runner.Quit();
            }
            rig.A.S.SlowerActivities = true;
            var slow = rig.Request(ActivityMode.Practice);
            Assert.AreEqual(ActivityStartResult.Started, runner.TryStart(in slow, rig.Game));
            Assert.AreEqual(45f * 1.25f, runner.Setup.Seconds, 1e-4f, "« Activités plus lentes »: × 1.25.");
            Assert.AreEqual(980, runner.Setup.Thresholds.Three, "Thresholds unchanged.");
            runner.Quit();
        }

        // ---------------- a whole cycle and the rewards ----------------

        [Test]
        public void AWholeCycle_ATicket_3Stars_TheSeedCookie_ThenACasualGame()
        {
            var rig = MemoryRig.Create();
            var cookie = MemoryRules.ThreeStarTreat;
            var seed = MemoryRules.ThreeStarSeed;
            int treats = rig.Treats(cookie);
            Assert.AreEqual(5, rig.A.S.Tickets.Count);

            var first = rig.Play(ActivityMode.Free, MemorySkill.Strong());
            Assert.IsTrue(first.Rewarded);
            Assert.AreEqual(4, rig.A.S.Tickets.Count, "1 ticket.");
            Assert.AreEqual(AdaptiveDifficulty.Start, first.Difficulty);
            Assert.AreEqual(3, first.Stars, "A strong player at d3: " + first.Score);
            Assert.AreEqual(rig.Game.Score, first.Score);
            Assert.IsTrue(rig.Game.AllMatched);
            Assert.Less(first.Seconds, 45.0, "Every pair found before the time.");
            Assert.AreEqual(cookie, first.Rewards.ItemTreat, "« 1 Biscuit aux graines à 3 étoiles ».");
            Assert.AreEqual(1, first.Rewards.ItemTreats);
            int stickerTreats = first.Rewards.Treat == cookie ? first.Rewards.Treats : 0;
            Assert.AreEqual(treats + 1 + stickerTreats, rig.Treats(cookie), "The Seed Cookie reaches the treat stock.");
            Assert.AreEqual(0, first.Rewards.CountItems0, "No seed before the Garden.");
            Assert.AreEqual(ContentId.None, first.Rewards.CountItem0);
            Assert.AreEqual(0, rig.Items(seed));
            Assert.AreEqual(20.0, first.Rewards.LightMinutes, 1e-9);
            Assert.AreEqual(first.Score, rig.Record.Best);

            treats = rig.Treats(cookie);
            var second = rig.Play(ActivityMode.Free, MemorySkill.Casual());
            Assert.AreEqual(3, rig.A.S.Tickets.Count);
            Assert.That(second.Stars, NUnit.Framework.Is.InRange(1, 2), "A casual player: " + second.Score);
            Assert.AreEqual(0, second.Rewards.ItemTreats, "No Seed Cookie under 3 stars.");
            stickerTreats = second.Rewards.Treat == cookie ? second.Rewards.Treats : 0;
            Assert.AreEqual(treats + stickerTreats, rig.Treats(cookie));
            Assert.AreEqual(2, rig.Record.GamesPlayed);
        }

        [Test]
        public void TheSeed_ComesAt3Stars_OnceTheGardenIsOpen()
        {
            var rig = MemoryRig.Create(garden: true);
            Assert.IsTrue(rig.A.S.Context.GardenOpen);
            var result = rig.Play(ActivityMode.Free, MemorySkill.Strong());
            Assert.AreEqual(3, result.Stars);
            Assert.AreEqual(MemoryRules.ThreeStarSeed, result.Rewards.CountItem0, "« 1 graine (Jardin ouvert) ».");
            Assert.AreEqual(1, result.Rewards.CountItems0);
            Assert.AreEqual(1, rig.Items(MemoryRules.ThreeStarSeed), "The seed reaches the inventory.");

            var rewards = rig.A.S.Rewards;
            var request = new ActivityRewardRequest { Activity = ActivityId.PawMemory, Mode = ActivityMode.Free, Challenge = -1, Stars = 2, Count0 = 1 };
            rewards.Compute(in request, out var outcome);
            Assert.AreEqual(0, outcome.CountItems0, "2 stars: no seed.");
            request.Stars = 3;
            rewards.Compute(in request, out outcome);
            Assert.AreEqual(1, outcome.CountItems0);
            request.Count0 = 5;
            rewards.Compute(in request, out outcome);
            Assert.AreEqual(1, outcome.CountItems0, "1 at most.");
            request.Mode = ActivityMode.Practice;
            rewards.Compute(in request, out outcome);
            Assert.AreEqual(0, outcome.CountItems0, "Practice: nothing.");

            // The Garden built but its refuge function not reached yet: no seed.
            var early = MemoryRig.Create(garden: true);
            early.A.State.Progression.Refuge.Level = 7;
            Assert.IsFalse(early.A.S.Context.GardenOpen);
        }

        [Test]
        public void PracticeGivesNothing_AChallengeAt3StarsGivesTheCookie()
        {
            var rig = MemoryRig.Create(garden: true);
            var cookie = MemoryRules.ThreeStarTreat;
            int treats = rig.Treats(cookie);
            var practice = rig.Play(ActivityMode.Practice, MemorySkill.Strong());
            Assert.IsFalse(practice.Rewarded);
            Assert.AreEqual(3, practice.Stars, "Stars are shown.");
            Assert.AreEqual(0, practice.Rewards.ItemTreats);
            Assert.AreEqual(0, practice.Rewards.CountItems0);
            Assert.AreEqual(treats, rig.Treats(cookie), "Practice gives nothing.");
            Assert.AreEqual(0, rig.Items(MemoryRules.ThreeStarSeed));
            Assert.AreEqual(5, rig.A.S.Tickets.Count, "Practice is free.");

            var challenge = rig.Play(ActivityMode.Challenge, MemorySkill.Strong(), challenge: 0);
            Assert.AreEqual(3, challenge.Stars, "Challenge 1: " + challenge.Score);
            Assert.AreEqual(1, challenge.Rewards.ItemTreats, "A challenge at 3 stars gives it too.");
            Assert.AreEqual(1, challenge.Rewards.CountItems0, "And the seed, the Garden open.");
            Assert.AreEqual(3, rig.A.S.ChallengeStars(ActivityId.PawMemory, 0));
            Assert.IsTrue(rig.A.S.IsChallengeOpen(ActivityId.PawMemory, 1));
            Assert.IsFalse(rig.A.S.IsChallengeOpen(ActivityId.PawMemory, 2));

            // Challenge 10 through the runner: 5 × 4, pawprints, 75 s.
            int slot = ActivityCatalog.SlotOf(ActivityId.PawMemory);
            for (int c = 0; c < MemoryRules.GrandChallenge; c++)
                rig.A.Activities.ChallengeStars[ActivityState.ChallengeIndex(slot, c)] = 1;
            var grand = rig.Play(ActivityMode.Challenge, MemorySkill.Strong(), MemoryRules.GrandChallenge);
            Assert.AreEqual(75f, rig.A.S.Runner.Setup.Seconds);
            Assert.IsTrue(rig.Game.Pawprints);
            Assert.AreEqual(20, rig.Game.CardCount);
            Assert.AreEqual(3, grand.Stars, "Challenge 10: " + grand.Score);
        }

        [Test]
        public void TheTimeUp_EndsTheGameThroughTheRunner()
        {
            var rig = MemoryRig.Create();
            var request = rig.Request(ActivityMode.Free);
            Assert.AreEqual(ActivityStartResult.Started, rig.A.S.Runner.TryStart(in request, rig.Game));
            for (int f = 0; f < 60 * 50 && rig.A.S.Runner.IsRunning; f++)
                rig.A.S.Runner.Tick(MemoryTests.Frame);
            var result = rig.A.S.Runner.LastResult;
            Assert.IsFalse(rig.A.S.Runner.IsRunning);
            Assert.IsFalse(result.Abandoned);
            Assert.AreEqual(45.0, result.Seconds, 0.05);
            Assert.AreEqual(0, result.Score);
            Assert.AreEqual(0, result.Stars, "No pair found.");
            Assert.IsTrue(rig.Game.Finished);
        }

        // ---------------- wishes and journal (doc 01, sections 2.3 and 5.2) ----------------

        [Test]
        public void TheHook_FeedsWishes057To059_AndJournal052()
        {
            var rig = MemoryRig.Create();
            int play = rig.AddWish(WishTemplateId.PlayPawMemory, 1);
            int pairs = rig.AddWish(WishTemplateId.PawMemoryPairs, 10);
            int moves = rig.AddWish(WishTemplateId.PawMemoryMoves, 18);
            rig.PairsGoal(30);

            // A practice game left early: its pairs count, the game does not.
            var request = rig.Request(ActivityMode.Practice);
            Assert.AreEqual(ActivityStartResult.Started, rig.A.S.Runner.TryStart(in request, rig.Game));
            for (int f = 0; f < 90; f++)
                rig.A.S.Runner.Tick(MemoryTests.Frame);
            MemoryTests.MatchOne(rig.Game);
            MemoryTests.MatchOne(rig.Game);
            rig.A.S.Runner.Quit();
            Assert.AreEqual(1, rig.Hooks.Games);
            Assert.IsFalse(rig.Hooks.Last.Finished);
            Assert.AreEqual(2, rig.Hooks.Last.PairsFound);
            Assert.AreEqual(0, rig.Wish(play).Progress, "WISH-057: a game left is not a game played.");
            Assert.AreEqual(2, rig.Wish(pairs).Progress, "WISH-058: the pairs found count.");
            Assert.AreEqual(0, rig.Wish(moves).Progress, "WISH-059: not finished.");
            Assert.AreEqual(2, rig.GoalProgress, "JRN-052: the pairs found.");

            // A practice game played to its end by a strong player.
            rig.Play(ActivityMode.Practice, MemorySkill.Strong());
            var last = rig.Hooks.Last;
            Assert.AreEqual(2, rig.Hooks.Games);
            Assert.IsTrue(last.Finished);
            Assert.IsFalse(last.Rewarded);
            Assert.AreEqual(-1, last.Challenge);
            Assert.IsTrue(last.AllMatched);
            Assert.AreEqual(6, last.Pairs);
            Assert.AreEqual(6, last.PairsFound);
            Assert.AreEqual(rig.Game.Tries, last.Tries);
            Assert.AreEqual(rig.Game.Moves, last.Moves);
            Assert.AreEqual(2 * last.Tries, last.Moves);
            Assert.AreEqual(18, last.MovesTarget, "4 × 3: 2 × 6 + 6.");
            Assert.IsTrue(last.WithinMoves, last.Tries + " tries.");
            Assert.IsTrue(rig.Wish(play).IsDone, "WISH-057: a game played, practice included.");
            Assert.AreEqual(8, rig.Wish(pairs).Progress, "WISH-058: cumulated.");
            Assert.IsTrue(rig.Wish(moves).IsDone, "WISH-059: every pair in 18 tries or fewer.");
            Assert.AreEqual(8, rig.GoalProgress, "JRN-052: cumulated.");

            // WISH-059 asks for every pair, within the game's own n.
            int strict = rig.AddWish(WishTemplateId.PawMemoryMoves, 18);
            var summary = new MemoryGameSummary { Finished = true, AllMatched = false, Pairs = 6, PairsFound = 5, Tries = 9, MovesTarget = 18 };
            rig.Daily.OnMemoryGame(in summary);
            Assert.AreEqual(0, rig.Wish(strict).Progress, "Time up before the last pair.");
            summary = new MemoryGameSummary { Finished = true, AllMatched = true, Pairs = 6, PairsFound = 6, Tries = 19, MovesTarget = 18 };
            rig.Daily.OnMemoryGame(in summary);
            Assert.AreEqual(0, rig.Wish(strict).Progress, "19 tries: one too many.");
            summary = new MemoryGameSummary { Finished = true, AllMatched = true, Pairs = 10, PairsFound = 10, Tries = 24, MovesTarget = 26 };
            rig.Daily.OnMemoryGame(in summary);
            Assert.IsTrue(rig.Wish(strict).IsDone, "« Grille de la partie »: a 5 × 4 game in 24 tries (n = 26).");

            // Another activity's game is not a memory game; after Dispose nothing counts.
            int progress = rig.GoalProgress;
            rig.A.Play(ActivityMode.Practice, 100);
            Assert.AreEqual(2, rig.Hooks.Games, "The test activity's game is not counted.");
            rig.Hooks.Dispose();
            rig.Hooks.Dispose();
            rig.Play(ActivityMode.Practice, MemorySkill.Strong());
            Assert.AreEqual(progress, rig.GoalProgress);
        }

        [Test]
        public void TheSummary_OfAnotherController_StillCountsTheGame()
        {
            var result = new ActivityGameResult { Activity = ActivityId.PawMemory, Mode = ActivityMode.Free, Stars = 2, Score = 640, Rewarded = true, Challenge = -1 };
            var summary = MemoryHooks.Summarize(in result, new DummyActivityController());
            Assert.IsTrue(summary.Finished);
            Assert.IsTrue(summary.Rewarded);
            Assert.AreEqual(2, summary.Stars);
            Assert.AreEqual(640, summary.Score);
            Assert.AreEqual(0, summary.PairsFound);
            Assert.IsFalse(summary.WithinMoves, "No grid known: never within its moves.");
            result.Abandoned = true;
            summary = MemoryHooks.Summarize(in result, null);
            Assert.IsFalse(summary.Finished);
            Assert.IsFalse(summary.Rewarded);
            Assert.AreEqual(0, summary.Stars);
        }

        [Test]
        public void Sys12_OpensWithMemory_InABuildWithActivities_AndWish059DrawsTheGridsN()
        {
            Assert.IsTrue(WishCatalog.IsShipped(WishSystem.PawMemory));
            var ids = new[] { WishTemplateId.PlayPawMemory, WishTemplateId.PawMemoryPairs, WishTemplateId.PawMemoryMoves };
            foreach (var id in ids)
            {
                Assert.IsTrue(WishCatalog.TryGet(id, out var template));
                Assert.IsTrue(template.ShippedInThisBuild, id.ToString());
                Assert.AreEqual(WishSystem.PawMemory, template.System);
                Assert.AreEqual(WishFamily.Games, template.Family);
            }
            Assert.IsTrue(WishCatalog.TryGet(WishTemplateId.PawMemoryPairs, out var pairsWish));
            Assert.IsFalse(pairsWish.Score, "Cumulated.");
            Assert.AreEqual(new[] { 6, 8, 10, 12 }, new[] { pairsWish.P1, pairsWish.P2, pairsWish.P3, pairsWish.P4 });
            Assert.IsTrue(WeeklyJournalCatalog.TryGet(JournalGoalId.MemoryPairs, out var goal));
            Assert.IsTrue(goal.ShippedInThisBuild, "JRN-052 ships with Pawprint Memory.");
            Assert.AreEqual(JournalSlot.Activities, goal.Slot);
            Assert.AreEqual(new[] { 30, 40, 50, 60 }, new[] { goal.P1, goal.P2, goal.P3, goal.P4 });

            // Level 11 on day 4: closed. Day 5: open, and the draws bring memory wishes.
            var rig = WishTestRig.Create(animals: 15, config: new DailyVisitConfig { ActivitiesAvailable = true });
            rig.State.Progression.Refuge.Level = 11;
            rig.SetDay(4);
            rig.Wishes.RefreshSystemUnlocks();
            Assert.IsFalse(rig.Wishes.IsSystemOpen(WishSystem.PawMemory));
            var journal = new WeeklyJournalService(rig.Ctx);
            Assert.AreEqual(0.0, journal.DrawWeight(JournalGoalId.MemoryPairs, false));

            rig.SetDay(5);
            rig.Wishes.RefreshSystemUnlocks();
            Assert.IsTrue(rig.Wishes.IsSystemOpen(WishSystem.PawMemory));
            Assert.Greater(journal.DrawWeight(JournalGoalId.MemoryPairs, false), 0.0, "JRN-052 in the Activities slot.");
            int memory = 0, movesWishes = 0;
            for (int day = 5; day <= 30; day++)
            {
                // From day 18 the player's Memory d is 9 (5 × 4): n = 2 × 10 + 6.
                if (day == 18)
                    rig.State.Activities.Records[ActivityCatalog.SlotOf(ActivityId.PawMemory)].Difficulty = 9;
                rig.Wishes.DrawForDay(rig.SetDay(day));
                foreach (var wish in rig.Stock().Where(w => WishTestRig.SystemOf(w) == WishSystem.PawMemory))
                {
                    memory++;
                    if ((WishTemplateId)wish.Template == WishTemplateId.PawMemoryMoves && wish.DrawnDay == rig.Ctx.TodayKey)
                    {
                        movesWishes++;
                        Assert.AreEqual(day >= 18 ? 26 : 18, wish.Target, "WISH-059's n: the player's grid at the draw.");
                    }
                }
                var visit = rig.State.DailyVisit;
                for (int i = 0; i < visit.WishCount; i++)
                {
                    visit.Wishes[i].Progress = visit.Wishes[i].Target;
                    visit.Wishes[i].Flags |= WishRecord.FlagDone;
                }
                rig.Wishes.ClaimAll(WishClaimPath.ClaimAll);
            }
            Assert.Greater(memory, 0, "Memory wishes are drawn.");
            Assert.Greater(movesWishes, 0, "WISH-059 is drawn.");

            // A build without activities never opens SYS-12.
            var closed = WishTestRig.Create(animals: 15);
            closed.State.Progression.Refuge.Level = 11;
            closed.SetDay(5);
            closed.Wishes.RefreshSystemUnlocks();
            Assert.IsFalse(closed.Wishes.IsSystemOpen(WishSystem.PawMemory));
            Assert.AreEqual(0.0, new WeeklyJournalService(closed.Ctx).DrawWeight(JournalGoalId.MemoryPairs, false));
        }

        [Test]
        public void TheMemoryGame_AndItsHook_DoNotAllocate()
        {
            var rig = MemoryRig.Create(garden: true);
            rig.AddWish(WishTemplateId.PlayPawMemory, 1000);
            rig.AddWish(WishTemplateId.PawMemoryPairs, 1000);
            rig.AddWish(WishTemplateId.PawMemoryMoves, 18);
            rig.PairsGoal(1000);
            var summary = new MemoryGameSummary { Finished = true, Rewarded = true, Stars = 2, PairsFound = 4, Pairs = 6, Tries = 20, MovesTarget = 18 };
            rig.Daily.OnMemoryGame(in summary);
            summary.PairsFound = 5;
            Assert.That(() => rig.Daily.OnMemoryGame(in summary), Is.Not.AllocatingGCMemory());

            var request = rig.Request(ActivityMode.Practice);
            Assert.AreEqual(ActivityStartResult.Started, rig.A.S.Runner.TryStart(in request, rig.Game));
            rig.A.S.Runner.Tick(0.016f);
            Assert.That(() => rig.A.S.Runner.Tick(0.016f), Is.Not.AllocatingGCMemory(), "The runner ticking the memory game.");

            var reward = new ActivityRewardRequest { Activity = ActivityId.PawMemory, Mode = ActivityMode.Free, Challenge = -1, Stars = 3, Count0 = 1 };
            rig.A.S.Rewards.Compute(in reward, out _);
            Assert.That(() => rig.A.S.Rewards.Compute(in reward, out _), Is.Not.AllocatingGCMemory(), "The seed.");
        }
    }
}
