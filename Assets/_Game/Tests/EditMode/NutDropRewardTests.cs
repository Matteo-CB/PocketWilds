using System.Linq;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// The activity rig with the rendez-vous and the nut drop hook (T40): the shipped rules registered as at boot (refuge
    /// level 10 and 15 animals, Nut Drop open), the real <see cref="NutDropController"/> played by a scripted catcher through
    /// the runner.
    /// </summary>
    internal sealed class NutDropRig
    {
        public ActivityTestRig A;
        public DailyVisitServices Daily;
        public NutDropHooks Hooks;
        public NutDropController Game = new NutDropController();

        public static NutDropRig Create(int level = 10, int animals = 15, bool activitiesInBuild = true, bool bakery = false)
        {
            var config = new ActivityConfig { BakeryAvailable = bakery };
            var rig = new NutDropRig { A = ActivityTestRig.Create(animals: animals, level: level, config: config) };
            ActivityRegistry.RegisterShipped(rig.A.S);
            rig.A.S.RefreshUnlocks();
            rig.Daily = new DailyVisitServices(rig.A.State, rig.A.Registry, rig.A.Economy, rig.A.Hatch, rig.A.SpeciesMastery, null, rig.A.P,
                rig.A.Clock, null, new DailyVisitConfig { ActivitiesAvailable = activitiesInBuild });
            rig.Hooks = new NutDropHooks(rig.A.S, rig.Daily);
            return rig;
        }

        public ActivityRequest Request(ActivityMode mode, int challenge = -1)
        {
            var request = new ActivityRequest { Activity = ActivityId.NutRain, Mode = mode, Challenge = challenge };
            request.SetAnimals(A.AnimalId(0));
            return request;
        }

        /// <summary>A whole nut drop game through the runner, played by <paramref name="skill"/>.</summary>
        public ActivityGameResult Play(ActivityMode mode, CatcherSkill skill, int challenge = -1)
        {
            var request = Request(mode, challenge);
            Assert.AreEqual(ActivityStartResult.Started, A.S.Runner.TryStart(in request, Game), "Start nut drop " + mode);
            ScriptedCatcher.Play(Game, skill, A.S.Runner.Setup.Seed, dt => A.S.Runner.Tick(dt), () => A.S.Runner.IsRunning);
            Assert.IsFalse(A.S.Runner.IsRunning, "The game ended.");
            return A.S.Runner.LastResult;
        }

        public int Treats(ContentId treat) => A.State.Progression.Treats.TryGetValue(treat, out int n) ? n : 0;

        public int Items(ContentId item) => A.State.Inventory.Count(item);

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

        /// <summary>A journal of this week holding JRN-017 with this target.</summary>
        public void NutsGoal(int target)
        {
            var visit = A.State.DailyVisit;
            visit.Goals[0] = new JournalGoalRecord { Goal = (ushort)JournalGoalId.NutsCaught, Slot = (byte)JournalSlot.Activities, Target = target };
            visit.GoalCount = 1;
            visit.JournalStartKey = Daily.TodayKey;
            visit.JournalEndKey = DailyDay.AddDays(Daily.TodayKey, 7);
            visit.FirstJournalDone = true;
        }

        public int GoalProgress => A.State.DailyVisit.Goals[0].Progress;
    }

    /// <summary>
    /// T40 « Pluie de noisettes » in the framework (doc 01 Contenu, S-ACT-003, sections 2.3 and 5.2; doc Mini-jeux, section
    /// 6): shipped and registered, opening at refuge level 10 on day 4 or with 15 animals, a whole cycle through
    /// <see cref="ActivityServices"/> (a ticket, stars, the record, rewards), the 3-star Toasted Hazelnut, the Hazelnut and
    /// Strawberry ingredients counted from the game (the reward service's T40 counted items), the wishes WISH-054 to 056 and
    /// the journal's JRN-017 through <see cref="NutDropHooks"/>, the SYS-11 draws, and allocation-free hooks.
    /// </summary>
    public sealed class NutDropRewardTests
    {
        private sealed class Weekend : IActivityEventHooks
        {
            public bool EventRunning => false;
            public bool IsFeatured(ActivityId activity) => false;
            public bool ActivitiesWeekend => true;
            public void GrantEventCurrency(int amount, ActivityId activity) { }
        }

        [Test]
        public void T40ShipsNutDrop_ItsRules_ItsTreat_AndItsIngredients()
        {
            var definition = ActivityCatalog.Get(ActivityId.NutRain);
            Assert.IsTrue(definition.ShippedInThisBuild, "T40 ships Nut Drop.");
            Assert.AreEqual("nut_drop", definition.Key);
            Assert.IsFalse(definition.FirstGameFree, "Only Grooming's first game is offered (A15).");
            Assert.IsTrue(definition.UsesTickets, "1 ticket.");
            Assert.AreEqual(60, definition.Seconds);
            Assert.AreEqual(ActivityMusic.Lively, definition.Music, "« Pattes pressées ».");
            var rig = NutDropRig.Create();
            var services = rig.A.S;
            Assert.IsTrue(services.Rules.Has(ActivityId.NutRain));
            Assert.IsTrue(services.IsShipped(ActivityId.NutRain));
            Assert.IsTrue(services.IsAvailable(ActivityId.NutRain), "Level 10 and 15 animals: open.");
            Assert.IsTrue(services.IsAvailable(ActivityId.HideAndSeek), "Hide and Seek stays.");
            Assert.AreEqual(NutDropRules.ThreeStarTreat, services.Rewards.ItemReward(ActivityId.NutRain, out int count, out int stars));
            Assert.AreEqual(ContentId.FromSource("treat.toasted_hazelnut"), NutDropRules.ThreeStarTreat);
            Assert.AreEqual(1, count);
            Assert.AreEqual(3, stars);

            Assert.AreEqual(ContentId.FromSource("ingredient.hazelnut"),
                services.Rewards.CountReward(ActivityId.NutRain, 0, out int per, out int max, out bool bakery));
            Assert.AreEqual(10, per, "1 Hazelnut per 10 hazelnuts caught…");
            Assert.AreEqual(3, max, "… 3 at most.");
            Assert.IsFalse(bakery);
            Assert.AreEqual(ContentId.FromSource("ingredient.strawberry"),
                services.Rewards.CountReward(ActivityId.NutRain, 1, out per, out max, out bakery));
            Assert.AreEqual(10, per, "1 Strawberry per 10 berries…");
            Assert.AreEqual(2, max, "… 2 at most…");
            Assert.IsTrue(bakery, "… the Pâtisserie open.");
            Assert.AreEqual(ContentId.None, services.Rewards.CountReward(ActivityId.HideAndSeek, 0, out _, out _, out _));
            Assert.AreEqual(ContentId.None, services.Rewards.CountReward(ActivityId.NutRain, 2, out _, out _, out _));

            Assert.IsFalse(NutDropRig.Create(level: 9).A.S.IsAvailable(ActivityId.NutRain), "Level 9: not open yet.");
            Assert.IsFalse(NutDropRig.Create(animals: 14).A.S.IsAvailable(ActivityId.NutRain), "Day 1 and 14 animals: not open yet.");
        }

        [Test]
        public void AWholeCycle_ATicket_3Stars_TheToastedHazelnut_TheHazelnuts_ThenACasualGame()
        {
            var rig = NutDropRig.Create();
            var toasted = NutDropRules.ThreeStarTreat;
            var hazelnut = NutDropRules.HazelnutIngredient;
            var strawberry = NutDropRules.StrawberryIngredient;
            int treats = rig.Treats(toasted);
            Assert.AreEqual(5, rig.A.S.Tickets.Count);
            Assert.IsFalse(rig.A.S.FreeGameAvailable(ActivityId.NutRain));

            var first = rig.Play(ActivityMode.Free, CatcherSkill.Strong());
            Assert.IsTrue(first.Rewarded);
            Assert.AreEqual(4, rig.A.S.Tickets.Count, "1 ticket.");
            Assert.AreEqual(AdaptiveDifficulty.Start, first.Difficulty);
            Assert.AreEqual(3, first.Stars, "A strong player at d3: " + first.Score);
            Assert.AreEqual(rig.Game.Score, first.Score);
            Assert.AreEqual(60.0, first.Seconds, 0.05);
            Assert.AreEqual(toasted, first.Rewards.ItemTreat, "« 1 Noisette grillée à 3 étoiles ».");
            Assert.AreEqual(1, first.Rewards.ItemTreats);
            int stickerTreats = first.Rewards.Treat == toasted ? first.Rewards.Treats : 0;
            Assert.AreEqual(treats + 1 + stickerTreats, rig.Treats(toasted), "The Toasted Hazelnut reaches the treat stock.");
            int expected = System.Math.Min(3, rig.Game.HazelnutsCaught / 10);
            Assert.Greater(rig.Game.HazelnutsCaught, 10);
            Assert.AreEqual(hazelnut, first.Rewards.CountItem0);
            Assert.AreEqual(expected, first.Rewards.CountItems0, "1 Hazelnut per 10 hazelnuts caught, 3 at most: " + rig.Game.HazelnutsCaught);
            Assert.AreEqual(expected, rig.Items(hazelnut), "The Hazelnuts reach the inventory.");
            Assert.Greater(rig.Game.BerriesCaught, 10);
            Assert.AreEqual(0, first.Rewards.CountItems1, "No Strawberry before the Pâtisserie.");
            Assert.AreEqual(ContentId.None, first.Rewards.CountItem1);
            Assert.AreEqual(0, rig.Items(strawberry));
            Assert.AreEqual(20.0, first.Rewards.LightMinutes, 1e-9);
            Assert.AreEqual(first.Score, rig.A.Record(ActivityId.NutRain).Best);

            treats = rig.Treats(toasted);
            int hazelnuts = rig.Items(hazelnut);
            var second = rig.Play(ActivityMode.Free, CatcherSkill.Casual());
            Assert.AreEqual(3, rig.A.S.Tickets.Count);
            Assert.That(second.Stars, NUnit.Framework.Is.InRange(1, 2), "A casual player: " + second.Score);
            Assert.AreEqual(0, second.Rewards.ItemTreats, "No Toasted Hazelnut under 3 stars.");
            stickerTreats = second.Rewards.Treat == toasted ? second.Rewards.Treats : 0;
            Assert.AreEqual(treats + stickerTreats, rig.Treats(toasted));
            expected = System.Math.Min(3, rig.Game.HazelnutsCaught / 10);
            Assert.AreEqual(expected, second.Rewards.CountItems0, "The Hazelnuts at any star.");
            Assert.AreEqual(hazelnuts + expected, rig.Items(hazelnut));
            Assert.AreEqual(2, rig.A.Record(ActivityId.NutRain).GamesPlayed);
        }

        [Test]
        public void TheStrawberries_ComeWithThePatisserie()
        {
            var rig = NutDropRig.Create(bakery: true);
            Assert.IsTrue(rig.A.S.Context.BakeryOpen);
            var result = rig.Play(ActivityMode.Free, CatcherSkill.Strong());
            int berries = rig.Game.BerriesCaught;
            Assert.Greater(berries, 10);
            Assert.AreEqual(NutDropRules.StrawberryIngredient, result.Rewards.CountItem1);
            Assert.AreEqual(System.Math.Min(2, berries / 10), result.Rewards.CountItems1, "1 Strawberry per 10 berries, 2 at most: " + berries);
            Assert.AreEqual(result.Rewards.CountItems1, rig.Items(NutDropRules.StrawberryIngredient));
        }

        [Test]
        public void TheCountedItems_PerTheirCount_Capped_NeverDoubled_NothingAt0Stars()
        {
            var rig = NutDropRig.Create(bakery: true);
            var rewards = rig.A.S.Rewards;
            var request = new ActivityRewardRequest { Activity = ActivityId.NutRain, Mode = ActivityMode.Free, Challenge = -1, Stars = 2, Count0 = 29, Count1 = 19 };
            rewards.Compute(in request, out var outcome);
            Assert.AreEqual(2, outcome.CountItems0, "29 hazelnuts: 2.");
            Assert.AreEqual(1, outcome.CountItems1, "19 berries: 1.");
            request.Count0 = 95;
            request.Count1 = 64;
            rewards.Compute(in request, out outcome);
            Assert.AreEqual(3, outcome.CountItems0, "3 at most.");
            Assert.AreEqual(2, outcome.CountItems1, "2 at most.");
            request.Count0 = 9;
            request.Count1 = 0;
            rewards.Compute(in request, out outcome);
            Assert.AreEqual(0, outcome.CountItems0);
            Assert.AreEqual(ContentId.None, outcome.CountItem0);

            // The weekend doubles rewards, never items.
            rig.A.S.Events = new Weekend();
            request.Count0 = 30;
            request.Count1 = 20;
            rewards.Compute(in request, out outcome);
            Assert.IsTrue(outcome.Weekend);
            Assert.AreEqual(3, outcome.CountItems0);
            Assert.AreEqual(2, outcome.CountItems1);

            // 0 stars and practice: nothing.
            request.Stars = 0;
            rewards.Compute(in request, out outcome);
            Assert.AreEqual(0, outcome.CountItems0);
            request.Stars = 3;
            request.Mode = ActivityMode.Practice;
            rewards.Compute(in request, out outcome);
            Assert.AreEqual(0, outcome.CountItems0);

            // Another activity has no counted items.
            request = new ActivityRewardRequest { Activity = ActivityId.HideAndSeek, Mode = ActivityMode.Free, Challenge = -1, Stars = 3, Count0 = 50 };
            rewards.Compute(in request, out outcome);
            Assert.AreEqual(0, outcome.CountItems0);

            // Granted: into the inventory.
            request = new ActivityRewardRequest { Activity = ActivityId.NutRain, Mode = ActivityMode.Free, Challenge = -1, Stars = 1, Count0 = 20, Count1 = 10 };
            rig.A.S.Events = null;
            rewards.Grant(in request, out outcome);
            Assert.AreEqual(2, rig.Items(NutDropRules.HazelnutIngredient));
            Assert.AreEqual(1, rig.Items(NutDropRules.StrawberryIngredient));
        }

        [Test]
        public void PracticeGivesNothing_AChallengeAt3StarsGivesTheTreat()
        {
            var rig = NutDropRig.Create();
            var toasted = NutDropRules.ThreeStarTreat;
            int treats = rig.Treats(toasted);
            var practice = rig.Play(ActivityMode.Practice, CatcherSkill.Strong());
            Assert.IsFalse(practice.Rewarded);
            Assert.AreEqual(3, practice.Stars, "Stars are shown.");
            Assert.AreEqual(0, practice.Rewards.ItemTreats);
            Assert.AreEqual(0, practice.Rewards.CountItems0);
            Assert.AreEqual(treats, rig.Treats(toasted), "Practice gives nothing.");
            Assert.AreEqual(0, rig.Items(NutDropRules.HazelnutIngredient));
            Assert.AreEqual(5, rig.A.S.Tickets.Count, "Practice is free.");

            var challenge = rig.Play(ActivityMode.Challenge, CatcherSkill.Strong(), challenge: 0);
            Assert.AreEqual(3, challenge.Stars, "Challenge 1: " + challenge.Score);
            Assert.AreEqual(1, challenge.Rewards.ItemTreats, "A challenge at 3 stars gives it too.");
            Assert.Greater(challenge.Rewards.CountItems0, 0, "And its Hazelnuts.");
            Assert.AreEqual(3, rig.A.S.ChallengeStars(ActivityId.NutRain, 0));
            Assert.IsTrue(rig.A.S.IsChallengeOpen(ActivityId.NutRain, 1));
            Assert.IsFalse(rig.A.S.IsChallengeOpen(ActivityId.NutRain, 2));

            // Challenge 6 through the runner (the five before it cleared): its stars measure the longest streak.
            int slot = ActivityCatalog.SlotOf(ActivityId.NutRain);
            for (int c = 0; c < NutDropRules.StreakChallenge; c++)
                rig.A.Activities.ChallengeStars[ActivityState.ChallengeIndex(slot, c)] = 1;
            var streak = rig.Play(ActivityMode.Challenge, CatcherSkill.Strong(), NutDropRules.StreakChallenge);
            Assert.AreEqual(3, streak.Stars, "50 in a row: " + rig.Game.BestStreak);
            Assert.AreEqual(rig.Game.Score, streak.Score, "The result card shows the points.");

            // Challenge 10 lasts 90 s.
            for (int c = 0; c < NutDropRules.HarvestChallenge; c++)
                rig.A.Activities.ChallengeStars[ActivityState.ChallengeIndex(slot, c)] = 1;
            var harvest = rig.Play(ActivityMode.Practice, CatcherSkill.Strong(), NutDropRules.HarvestChallenge);
            Assert.AreEqual(90.0, harvest.Seconds, 0.05);
        }

        // ---------------- wishes and journal (doc 01, sections 2.3 and 5.2) ----------------

        [Test]
        public void TheHook_FeedsWishes054To056_AndJournal017()
        {
            var rig = NutDropRig.Create();
            int play = rig.AddWish(WishTemplateId.PlayNutDrop, 1);
            int catches = rig.AddWish(WishTemplateId.NutDropCatch, 50);
            int streak = rig.AddWish(WishTemplateId.NutDropStreak, 30);
            rig.NutsGoal(150);

            // A practice game left early: its nuts count, the game does not.
            var request = rig.Request(ActivityMode.Practice);
            Assert.AreEqual(ActivityStartResult.Started, rig.A.S.Runner.TryStart(in request, rig.Game));
            for (int f = 0; f < 60 * 15; f++)
            {
                int next = NutDropTests.Next(rig.Game, pinecones: false);
                if (next >= 0)
                    rig.Game.SetBasketTarget(rig.Game.ItemRimX(next));
                rig.A.S.Runner.Tick(NutDropTests.Frame);
            }
            int early = rig.Game.Caught;
            Assert.Greater(early, 5);
            rig.A.S.Runner.Quit();
            Assert.AreEqual(1, rig.Hooks.Games);
            Assert.IsFalse(rig.Hooks.Last.Finished);
            Assert.AreEqual(early, rig.Hooks.Last.Caught);
            Assert.AreEqual(0, rig.Wish(play).Progress, "WISH-054: a game left is not a game played.");
            Assert.AreEqual(early, rig.Wish(catches).Progress, "WISH-055: the best game so far.");
            Assert.AreEqual(rig.Game.BestStreak, rig.Wish(streak).Progress, "WISH-056: the best streak so far.");
            Assert.AreEqual(early, rig.GoalProgress, "JRN-017: the nuts caught.");

            // A practice game played to its end by a strong player.
            rig.Play(ActivityMode.Practice, CatcherSkill.Strong());
            var last = rig.Hooks.Last;
            Assert.AreEqual(2, rig.Hooks.Games);
            Assert.IsTrue(last.Finished);
            Assert.IsFalse(last.Rewarded);
            Assert.AreEqual(-1, last.Challenge);
            Assert.AreEqual(rig.Game.Caught, last.Caught);
            Assert.AreEqual(rig.Game.HazelnutsCaught, last.HazelnutsCaught);
            Assert.AreEqual(rig.Game.BerriesCaught, last.BerriesCaught);
            Assert.AreEqual(rig.Game.PineconesCaught, last.PineconesCaught);
            Assert.AreEqual(rig.Game.BestStreak, last.BestStreak);
            Assert.IsTrue(rig.Wish(play).IsDone, "WISH-054: a game played, practice included.");
            Assert.IsTrue(rig.Wish(catches).IsDone, "WISH-055: 50 nuts in one game: " + last.Caught);
            Assert.IsTrue(rig.Wish(streak).IsDone, "WISH-056: a streak of 30: " + last.BestStreak);
            Assert.AreEqual(System.Math.Min(150, early + last.Caught), rig.GoalProgress, "JRN-017: cumulated.");

            // A score wish is one game: two small games never add up.
            int one = rig.AddWish(WishTemplateId.NutDropCatch, 1000);
            var summary = new NutDropGameSummary { Finished = true, Caught = 40, BestStreak = 12 };
            rig.Daily.OnNutDropGame(in summary);
            rig.Daily.OnNutDropGame(in summary);
            Assert.AreEqual(40, rig.Wish(one).Progress, "Not 80: the best single game.");

            // Another activity's game is not a nut drop; after Dispose nothing counts.
            int progress = rig.GoalProgress;
            rig.A.Play(ActivityMode.Practice, 100);
            Assert.AreEqual(2, rig.Hooks.Games, "The test activity's game is not counted.");
            rig.Hooks.Dispose();
            rig.Hooks.Dispose();
            rig.Play(ActivityMode.Practice, CatcherSkill.Strong());
            Assert.AreEqual(progress, rig.GoalProgress);
        }

        [Test]
        public void TheSummary_OfAnotherController_StillCountsTheGame()
        {
            var result = new ActivityGameResult { Activity = ActivityId.NutRain, Mode = ActivityMode.Free, Stars = 2, Score = 640, Rewarded = true, Challenge = -1 };
            var summary = NutDropHooks.Summarize(in result, new DummyActivityController());
            Assert.IsTrue(summary.Finished);
            Assert.IsTrue(summary.Rewarded);
            Assert.AreEqual(2, summary.Stars);
            Assert.AreEqual(640, summary.Score);
            Assert.AreEqual(0, summary.Caught);
            result.Abandoned = true;
            summary = NutDropHooks.Summarize(in result, null);
            Assert.IsFalse(summary.Finished);
            Assert.IsFalse(summary.Rewarded);
            Assert.AreEqual(0, summary.Stars);
        }

        [Test]
        public void Sys11_OpensWithNutDrop_InABuildWithActivities()
        {
            Assert.IsTrue(WishCatalog.IsShipped(WishSystem.NutRain));
            var ids = new[] { WishTemplateId.PlayNutDrop, WishTemplateId.NutDropCatch, WishTemplateId.NutDropStreak };
            foreach (var id in ids)
            {
                Assert.IsTrue(WishCatalog.TryGet(id, out var template));
                Assert.IsTrue(template.ShippedInThisBuild, id.ToString());
                Assert.AreEqual(WishSystem.NutRain, template.System);
                Assert.AreEqual(WishFamily.Games, template.Family);
            }
            Assert.IsTrue(WishCatalog.TryGet(WishTemplateId.NutDropCatch, out var catchWish));
            Assert.IsTrue(catchWish.Score);
            Assert.AreEqual(new[] { 30, 40, 50, 60 }, new[] { catchWish.P1, catchWish.P2, catchWish.P3, catchWish.P4 });
            Assert.IsTrue(WishCatalog.TryGet(WishTemplateId.NutDropStreak, out var streakWish));
            Assert.AreEqual(new[] { 15, 20, 25, 30 }, new[] { streakWish.P1, streakWish.P2, streakWish.P3, streakWish.P4 });
            Assert.IsTrue(WeeklyJournalCatalog.TryGet(JournalGoalId.NutsCaught, out var goal));
            Assert.IsTrue(goal.ShippedInThisBuild, "JRN-017 ships with Nut Drop.");
            Assert.AreEqual(JournalSlot.Activities, goal.Slot);
            Assert.AreEqual(new[] { 150, 250, 350, 450 }, new[] { goal.P1, goal.P2, goal.P3, goal.P4 });

            // Level 9: closed. Level 10 and 15 animals: open, and the draws bring nut drop wishes.
            var rig = WishTestRig.Create(animals: 15, config: new DailyVisitConfig { ActivitiesAvailable = true });
            rig.State.Progression.Refuge.Level = 9;
            rig.Wishes.RefreshSystemUnlocks();
            Assert.IsFalse(rig.Wishes.IsSystemOpen(WishSystem.NutRain));
            var journal = new WeeklyJournalService(rig.Ctx);
            Assert.AreEqual(0.0, journal.DrawWeight(JournalGoalId.NutsCaught, false));

            rig.State.Progression.Refuge.Level = 10;
            rig.Wishes.RefreshSystemUnlocks();
            Assert.IsTrue(rig.Wishes.IsSystemOpen(WishSystem.NutRain));
            Assert.Greater(journal.DrawWeight(JournalGoalId.NutsCaught, false), 0.0, "JRN-017 in the Activities slot.");
            int nutDrop = 0;
            for (int day = 2; day <= 14; day++)
            {
                rig.Wishes.DrawForDay(rig.SetDay(day));
                nutDrop += rig.Stock().Count(w => WishTestRig.SystemOf(w) == WishSystem.NutRain);
                var visit = rig.State.DailyVisit;
                for (int i = 0; i < visit.WishCount; i++)
                {
                    visit.Wishes[i].Progress = visit.Wishes[i].Target;
                    visit.Wishes[i].Flags |= WishRecord.FlagDone;
                }
                rig.Wishes.ClaimAll(WishClaimPath.ClaimAll);
            }
            Assert.Greater(nutDrop, 0, "Nut drop wishes are drawn.");

            // A build without activities never opens SYS-11.
            var closed = WishTestRig.Create(animals: 15);
            closed.State.Progression.Refuge.Level = 10;
            closed.Wishes.RefreshSystemUnlocks();
            Assert.IsFalse(closed.Wishes.IsSystemOpen(WishSystem.NutRain));
            Assert.AreEqual(0.0, new WeeklyJournalService(closed.Ctx).DrawWeight(JournalGoalId.NutsCaught, false));
        }

        [Test]
        public void TheNutDropGame_AndItsHook_DoNotAllocate()
        {
            var rig = NutDropRig.Create(bakery: true);
            rig.AddWish(WishTemplateId.PlayNutDrop, 1000);
            rig.AddWish(WishTemplateId.NutDropCatch, 1000);
            rig.AddWish(WishTemplateId.NutDropStreak, 1000);
            rig.NutsGoal(1000);
            var summary = new NutDropGameSummary { Finished = true, Rewarded = true, Stars = 2, Caught = 40, BestStreak = 12 };
            rig.Daily.OnNutDropGame(in summary);
            summary.Caught = 41;
            summary.BestStreak = 13;
            Assert.That(() => rig.Daily.OnNutDropGame(in summary), Is.Not.AllocatingGCMemory());

            var request = rig.Request(ActivityMode.Practice);
            Assert.AreEqual(ActivityStartResult.Started, rig.A.S.Runner.TryStart(in request, rig.Game));
            rig.A.S.Runner.Tick(0.016f);
            Assert.That(() => rig.A.S.Runner.Tick(0.016f), Is.Not.AllocatingGCMemory(), "The runner ticking the nut drop game.");

            var reward = new ActivityRewardRequest { Activity = ActivityId.NutRain, Mode = ActivityMode.Free, Challenge = -1, Stars = 3, Count0 = 30, Count1 = 20 };
            rig.A.S.Rewards.Compute(in reward, out _);
            Assert.That(() => rig.A.S.Rewards.Compute(in reward, out _), Is.Not.AllocatingGCMemory(), "The counted items.");
        }
    }
}
