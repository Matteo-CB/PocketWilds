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
    /// The activity rig with the rendez-vous and the grooming hook (T38): the shipped rules registered as at boot, the real
    /// <see cref="GroomingController"/> played by a scripted player through the runner.
    /// </summary>
    internal sealed class GroomingRig
    {
        public ActivityTestRig A;
        public DailyVisitServices Daily;
        public GroomingHooks Hooks;
        public GroomingController Game = new GroomingController();

        public static GroomingRig Create(int level = 3, int animals = 6, bool activitiesInBuild = true)
        {
            var rig = new GroomingRig { A = ActivityTestRig.Create(animals: animals, level: level) };
            ActivityRegistry.RegisterShipped(rig.A.S);
            rig.Daily = new DailyVisitServices(rig.A.State, rig.A.Registry, rig.A.Economy, rig.A.Hatch, rig.A.SpeciesMastery, null, rig.A.P,
                rig.A.Clock, null, new DailyVisitConfig { ActivitiesAvailable = activitiesInBuild });
            rig.Hooks = new GroomingHooks(rig.A.S, rig.Daily);
            return rig;
        }

        public ActivityRequest Request(ActivityMode mode, int challenge = -1, uint animal = 0)
        {
            var request = new ActivityRequest { Activity = ActivityId.Grooming, Mode = mode, Challenge = challenge };
            request.SetAnimals(animal != 0 ? animal : A.AnimalId(0));
            return request;
        }

        /// <summary>A whole grooming game through the runner, played by <paramref name="skill"/>.</summary>
        public ActivityGameResult Play(ActivityMode mode, GroomerSkill skill, int challenge = -1, uint animal = 0)
        {
            var request = Request(mode, challenge, animal);
            Assert.AreEqual(ActivityStartResult.Started, A.S.Runner.TryStart(in request, Game), "Start grooming " + mode);
            ScriptedGroomer.Play(Game, skill, dt => A.S.Runner.Tick(dt), () => A.S.Runner.IsRunning);
            Assert.IsFalse(A.S.Runner.IsRunning, "The game ended.");
            return A.S.Runner.LastResult;
        }

        public int Treats(ContentId treat) => A.State.Progression.Treats.TryGetValue(treat, out int n) ? n : 0;

        public int AddWish(WishTemplateId template, uint animal, int target)
        {
            var visit = A.State.DailyVisit;
            int slot = visit.WishCount++;
            visit.Wishes[slot] = new WishRecord
            {
                Template = (ushort)template,
                Animal = animal,
                Target = target,
                DrawnDay = Daily.TodayKey,
                ParamKind = WishCatalog.TryGet(template, out var t) ? (byte)t.ParamKind : (byte)0,
            };
            return slot;
        }

        public WishRecord Wish(int slot) => A.State.DailyVisit.Wishes[slot];

        /// <summary>A journal of this week holding JRN-033 with this target.</summary>
        public void GroomingGoal(int target)
        {
            var visit = A.State.DailyVisit;
            visit.Goals[0] = new JournalGoalRecord { Goal = (ushort)JournalGoalId.Grooming, Slot = (byte)JournalSlot.Care, Target = target };
            visit.GoalCount = 1;
            visit.JournalStartKey = Daily.TodayKey;
            visit.JournalEndKey = DailyDay.AddDays(Daily.TodayKey, 7);
            visit.FirstJournalDone = true;
        }

        public int GoalProgress => A.State.DailyVisit.Goals[0].Progress;
    }

    /// <summary>
    /// T38 « Toilettage » in the framework (doc 01 Contenu, S-ACT-001, A15, sections 2.3 and 5.2; doc Mini-jeux, section 6):
    /// shipped and registered, a whole cycle through <see cref="ActivityServices"/> (the free first game, tickets, stars,
    /// records, rewards), the 3-star Clover Crunch through the runner and the reward service, the wishes WISH-019 to 022
    /// and the journal's JRN-033 through <see cref="GroomingHooks"/>, the SYS-09 draws, and allocation-free hooks.
    /// </summary>
    public sealed class GroomingRewardTests
    {
        private sealed class Weekend : IActivityEventHooks
        {
            public bool EventRunning => false;
            public bool IsFeatured(ActivityId activity) => false;
            public bool ActivitiesWeekend => true;
            public void GrantEventCurrency(int amount, ActivityId activity) { }
        }

        [Test]
        public void T38ShipsGrooming_ItsRules_AndItsCloverCrunch()
        {
            var definition = ActivityCatalog.Get(ActivityId.Grooming);
            Assert.IsTrue(definition.ShippedInThisBuild, "T38 ships Grooming.");
            var rig = GroomingRig.Create();
            var services = rig.A.S;
            Assert.IsTrue(services.Rules.Has(ActivityId.Grooming));
            Assert.IsTrue(services.IsShipped(ActivityId.Grooming));
            Assert.IsTrue(services.IsAvailable(ActivityId.Grooming), "Level 3 and 5 animals: open.");
            Assert.IsTrue(services.AnyShipped, "The Play button exists.");
            Assert.AreEqual(GroomingRules.ThreeStarTreat, services.Rewards.ItemReward(ActivityId.Grooming, out int count, out int stars));
            Assert.AreEqual(1, count);
            Assert.AreEqual(3, stars);
            Assert.AreEqual(ContentId.None, services.Rewards.ItemReward(ActivityId.Test, out count, out _));
            Assert.AreEqual(0, count);
            Assert.IsFalse(GroomingRig.Create(level: 2).A.S.IsAvailable(ActivityId.Grooming), "Level 2: not open yet.");
        }

        [Test]
        public void AWholeCycle_TheFreeFirstGame_3Stars_TheCloverCrunch_ThenATicket()
        {
            var rig = GroomingRig.Create();
            var clover = GroomingRules.ThreeStarTreat;
            int treats = rig.Treats(clover);
            Assert.IsTrue(rig.A.S.FreeGameAvailable(ActivityId.Grooming), "A15: the first game is offered.");

            var first = rig.Play(ActivityMode.Free, GroomerSkill.Strong());
            Assert.IsTrue(first.FreeGame);
            Assert.IsTrue(first.Rewarded);
            Assert.AreEqual(5, rig.A.S.Tickets.Count, "No ticket for the offered game.");
            Assert.AreEqual(3, first.Stars, "A strong player at d3: " + first.Score);
            Assert.AreEqual(rig.Game.Score, first.Score);
            Assert.IsTrue(rig.Game.BowPlaced);
            Assert.Less(first.Seconds, 45.0, "The bow ends the game early.");
            Assert.AreEqual(AdaptiveDifficulty.Start, first.Difficulty);
            Assert.AreEqual(clover, first.Rewards.ItemTreat, "« Objet gagné : 1 Trèfle croquant à 3 étoiles ».");
            Assert.AreEqual(1, first.Rewards.ItemTreats);
            int stickerTreats = first.Rewards.Treat == clover ? first.Rewards.Treats : 0;
            Assert.AreEqual(treats + 1 + stickerTreats, rig.Treats(clover), "The Clover Crunch reaches the treat stock.");
            Assert.AreEqual(20.0, first.Rewards.LightMinutes, 1e-9);
            Assert.AreEqual(6, rig.A.Bond.Points, "+6 bond to the groomed animal.");
            Assert.AreEqual(rig.A.AnimalId(0), rig.A.Bond.LastAnimal);
            Assert.AreEqual(first.Score, rig.A.Record(ActivityId.Grooming).Best);
            Assert.IsFalse(rig.A.S.FreeGameAvailable(ActivityId.Grooming));

            treats = rig.Treats(clover);
            var second = rig.Play(ActivityMode.Free, GroomerSkill.Casual());
            Assert.IsFalse(second.FreeGame);
            Assert.AreEqual(4, rig.A.S.Tickets.Count, "The second game takes a ticket.");
            Assert.AreEqual(2, second.Stars, "A casual player: " + second.Score);
            Assert.AreEqual(0, second.Rewards.ItemTreats, "No Clover Crunch under 3 stars.");
            Assert.AreEqual(ContentId.None, second.Rewards.ItemTreat);
            stickerTreats = second.Rewards.Treat == clover ? second.Rewards.Treats : 0;
            Assert.AreEqual(treats + stickerTreats, rig.Treats(clover));
            Assert.AreEqual(2, rig.A.Record(ActivityId.Grooming).GamesPlayed);
        }

        [Test]
        public void PracticeGivesNoTreat_AChallengeAt3StarsDoes_TheWeekendNeverDoublesIt()
        {
            var rig = GroomingRig.Create();
            var clover = GroomingRules.ThreeStarTreat;
            int treats = rig.Treats(clover);
            var practice = rig.Play(ActivityMode.Practice, GroomerSkill.Strong());
            Assert.IsFalse(practice.Rewarded);
            Assert.AreEqual(3, practice.Stars, "Stars are shown.");
            Assert.AreEqual(0, practice.Rewards.ItemTreats);
            Assert.AreEqual(treats, rig.Treats(clover), "Practice gives nothing.");

            var challenge = rig.Play(ActivityMode.Challenge, GroomerSkill.Strong(), challenge: 0);
            Assert.AreEqual(3, challenge.Stars);
            Assert.AreEqual(1, challenge.Rewards.ItemTreats, "A challenge at 3 stars gives it too.");
            Assert.AreEqual(3, rig.A.S.ChallengeStars(ActivityId.Grooming, 0));
            Assert.IsTrue(rig.A.S.IsChallengeOpen(ActivityId.Grooming, 1));

            rig.A.S.Events = new Weekend();
            var request = new ActivityRewardRequest
            {
                Activity = ActivityId.Grooming, Mode = ActivityMode.Free, Challenge = -1, Stars = 3, AnimalCount = 1, Animal0 = rig.A.AnimalId(0),
            };
            rig.A.S.Rewards.Compute(in request, out var outcome);
            Assert.IsTrue(outcome.Weekend);
            Assert.AreEqual(12, outcome.BondPerAnimal, "The weekend doubles the bond...");
            Assert.AreEqual(1, outcome.ItemTreats, "...never the item.");
        }

        [Test]
        public void TheItem_RidesApart_WhenTheStickerTurnsIntoAnotherTreat()
        {
            var rig = ActivityTestRig.Create();
            var cookie = ContentId.FromSource("treat.test_cookie");
            rig.S.Rewards.SetItemReward(ActivityId.Test, cookie, 2, 2);
            var request = new ActivityRewardRequest
            {
                Activity = ActivityId.Test, Mode = ActivityMode.Free, Challenge = -1, Stars = 2, NewRecord = true, AnimalCount = 1,
                Animal0 = rig.AnimalId(0), Seed = 3,
            };
            rig.S.Rewards.Grant(in request, out var outcome);
            Assert.AreEqual(cookie, outcome.ItemTreat);
            Assert.AreEqual(2, outcome.ItemTreats, "From 2 stars for this activity.");
            Assert.AreEqual(ActivityTestRig.CloverCrunch, outcome.Treat, "The record's sticker is 3 Clover Crunch before the albums.");
            Assert.AreEqual(2, rig.State.Progression.Treats[cookie]);
            Assert.GreaterOrEqual(rig.State.Progression.Treats[ActivityTestRig.CloverCrunch], 3);
            request.Stars = 1;
            rig.S.Rewards.Compute(in request, out outcome);
            Assert.AreEqual(0, outcome.ItemTreats);
            rig.S.Rewards.SetItemReward(ActivityId.Test, cookie, 0);
            request.Stars = 3;
            rig.S.Rewards.Compute(in request, out outcome);
            Assert.AreEqual(0, outcome.ItemTreats, "Removed.");
        }

        // ---------------- wishes and journal (doc 01, sections 2.3 and 5.2) ----------------

        [Test]
        public void TheHook_FeedsWishes019To022_AndJournal033()
        {
            var rig = GroomingRig.Create();
            uint pet = rig.A.AnimalId(0), other = rig.A.AnimalId(1);
            int groomPet = rig.AddWish(WishTemplateId.GroomAnimal, pet, 1);
            int groomOther = rig.AddWish(WishTemplateId.GroomAnimal, other, 1);
            int stars = rig.AddWish(WishTemplateId.GroomingStars, rig.A.AnimalId(2), 3);
            int leaves = rig.AddWish(WishTemplateId.GroomingLeaves, rig.A.AnimalId(3), 30);
            int bow = rig.AddWish(WishTemplateId.GroomingBow, rig.A.AnimalId(4), 1);
            rig.GroomingGoal(3);
            Assert.IsTrue(rig.Daily.Wishes.GroomWishAnimal(out uint wanted));
            Assert.AreEqual(pet, wanted, "The hub can feature the animal of « Brosse {animal} ».");

            // A practice game with the pet: groomed, its leaves and its bow count; practice stars do not.
            rig.Play(ActivityMode.Practice, GroomerSkill.Strong(), animal: pet);
            Assert.AreEqual(1, rig.Hooks.Games);
            Assert.IsTrue(rig.Wish(groomPet).IsDone, "WISH-019: a game with that animal, practice included.");
            Assert.IsFalse(rig.Wish(groomOther).IsDone);
            Assert.AreEqual(0, rig.Wish(stars).Progress, "WISH-020: stars of rewarded games.");
            Assert.AreEqual(rig.Game.DebrisRemoved, rig.Wish(leaves).Progress, "WISH-021: debris and burrs.");
            Assert.Greater(rig.Game.DebrisRemoved, 0);
            Assert.IsTrue(rig.Wish(bow).IsDone, "WISH-022: a bow placed.");
            Assert.AreEqual(1, rig.GoalProgress, "JRN-033: one animal brushed.");
            Assert.IsTrue(rig.Hooks.Last.Finished);
            Assert.IsFalse(rig.Hooks.Last.Rewarded);
            Assert.IsTrue(rig.Daily.Wishes.GroomWishAnimal(out wanted));
            Assert.AreEqual(other, wanted, "The pet's wish is done: the other animal's comes next.");

            // A rewarded game: its 3 stars.
            int before = rig.Wish(leaves).Progress;
            var free = rig.Play(ActivityMode.Free, GroomerSkill.Strong(), animal: pet);
            Assert.AreEqual(3, free.Stars);
            Assert.IsTrue(rig.Wish(stars).IsDone, "3 stars of a rewarded game.");
            Assert.AreEqual(Math.Min(30, before + rig.Game.DebrisRemoved), rig.Wish(leaves).Progress, "Cumulated.");
            Assert.AreEqual(2, rig.GoalProgress, "The same animal again counts again (kind).");

            // A game left early with the other animal: its leaves count, nothing else.
            before = rig.Wish(leaves).Progress;
            var request = rig.Request(ActivityMode.Practice, animal: other);
            Assert.AreEqual(ActivityStartResult.Started, rig.A.S.Runner.TryStart(in request, rig.Game));
            rig.A.S.Runner.Tick(0.1f);
            rig.Game.TapDebris(0);
            rig.Game.TapDebris(0);
            rig.A.S.Runner.Quit();
            Assert.AreEqual(before + 1, rig.Wish(leaves).Progress, "The leaves really picked off count.");
            Assert.IsFalse(rig.Wish(groomOther).IsDone, "A game left is not a grooming.");
            Assert.AreEqual(2, rig.GoalProgress);
            Assert.IsFalse(rig.Hooks.Last.Finished);

            // Another activity's game is not a grooming; after Dispose nothing counts.
            rig.A.Play(ActivityMode.Practice, 100);
            Assert.AreEqual(3, rig.Hooks.Games);
            rig.Hooks.Dispose();
            rig.Hooks.Dispose();
            rig.Play(ActivityMode.Practice, GroomerSkill.Strong(), animal: other);
            Assert.IsFalse(rig.Wish(groomOther).IsDone);
            Assert.AreEqual(2, rig.GoalProgress);
        }

        [Test]
        public void TheSummary_OfAnotherController_StillCountsTheGame()
        {
            var result = new ActivityGameResult { Activity = ActivityId.Grooming, Mode = ActivityMode.Free, Stars = 2, Score = 300, Rewarded = true };
            var summary = GroomingHooks.Summarize(in result, new DummyActivityController(), 42);
            Assert.AreEqual(42u, summary.Animal);
            Assert.IsTrue(summary.Finished);
            Assert.IsTrue(summary.Rewarded);
            Assert.AreEqual(2, summary.Stars);
            Assert.AreEqual(0, summary.DebrisRemoved);
            Assert.IsFalse(summary.BowPlaced);
            result.Abandoned = true;
            summary = GroomingHooks.Summarize(in result, null, 42);
            Assert.IsFalse(summary.Finished);
            Assert.IsFalse(summary.Rewarded);
            Assert.AreEqual(0, summary.Stars);
        }

        [Test]
        public void Sys09_OpensWithGrooming_InABuildWithActivities()
        {
            Assert.IsTrue(WishCatalog.IsShipped(WishSystem.Grooming));
            foreach (var id in new[] { WishTemplateId.GroomAnimal, WishTemplateId.GroomingStars, WishTemplateId.GroomingLeaves, WishTemplateId.GroomingBow })
            {
                Assert.IsTrue(WishCatalog.TryGet(id, out var template));
                Assert.IsTrue(template.ShippedInThisBuild, id.ToString());
                Assert.AreEqual(WishSystem.Grooming, template.System);
            }
            Assert.IsTrue(WeeklyJournalCatalog.TryGet(JournalGoalId.Grooming, out var goal));
            Assert.IsTrue(goal.ShippedInThisBuild, "JRN-033 ships with Grooming.");
            Assert.AreEqual(new[] { 3, 4, 5, 6 }, new[] { goal.P1, goal.P2, goal.P3, goal.P4 });

            // Refuge level 2: closed. Level 3 and 6 animals: open, and the draw brings grooming wishes.
            var rig = WishTestRig.Create(config: new DailyVisitConfig { ActivitiesAvailable = true });
            rig.State.Progression.Refuge.Level = 2;
            rig.Wishes.RefreshSystemUnlocks();
            Assert.IsFalse(rig.Wishes.IsSystemOpen(WishSystem.Grooming));
            var journal = new WeeklyJournalService(rig.Ctx);
            Assert.AreEqual(0.0, journal.DrawWeight(JournalGoalId.Grooming, false));

            rig.State.Progression.Refuge.Level = 3;
            rig.Wishes.RefreshSystemUnlocks();
            Assert.IsTrue(rig.Wishes.IsSystemOpen(WishSystem.Grooming));
            Assert.Greater(journal.DrawWeight(JournalGoalId.Grooming, false), 0.0, "JRN-033 in the Care slot.");
            int grooming = 0;
            for (int day = 2; day <= 12; day++)
            {
                rig.Wishes.DrawForDay(rig.SetDay(day));
                grooming += rig.Stock().Count(w => WishTestRig.SystemOf(w) == WishSystem.Grooming);
                var visit = rig.State.DailyVisit;
                for (int i = 0; i < visit.WishCount; i++)
                {
                    visit.Wishes[i].Progress = visit.Wishes[i].Target;
                    visit.Wishes[i].Flags |= WishRecord.FlagDone;
                }
                rig.Wishes.ClaimAll(WishClaimPath.ClaimAll);
            }
            Assert.Greater(grooming, 0, "Grooming wishes are drawn.");

            // A build without activities never opens SYS-09.
            var closed = WishTestRig.Create();
            closed.State.Progression.Refuge.Level = 3;
            closed.Wishes.RefreshSystemUnlocks();
            Assert.IsFalse(closed.Wishes.IsSystemOpen(WishSystem.Grooming));
            Assert.AreEqual(0.0, new WeeklyJournalService(closed.Ctx).DrawWeight(JournalGoalId.Grooming, false));
        }

        [Test]
        public void TheGroomingGame_AndItsHook_DoNotAllocate()
        {
            var rig = GroomingRig.Create();
            rig.AddWish(WishTemplateId.GroomingLeaves, rig.A.AnimalId(1), 1000);
            rig.AddWish(WishTemplateId.GroomAnimal, rig.A.AnimalId(2), 1);
            rig.GroomingGoal(1000);
            var summary = new GroomingGameSummary { Animal = rig.A.AnimalId(0), Finished = true, Rewarded = true, Stars = 2, DebrisRemoved = 3 };
            rig.Daily.OnGroomingGame(in summary);
            Assert.That(() => rig.Daily.OnGroomingGame(in summary), Is.Not.AllocatingGCMemory());

            var request = rig.Request(ActivityMode.Practice);
            Assert.AreEqual(ActivityStartResult.Started, rig.A.S.Runner.TryStart(in request, rig.Game));
            rig.A.S.Runner.Tick(0.016f);
            Assert.That(() => rig.A.S.Runner.Tick(0.016f), Is.Not.AllocatingGCMemory(), "The runner ticking the grooming game.");
        }
    }
}
