using System.Linq;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// The activity rig with the rendez-vous and the hide-and-seek hook (T39): the shipped rules registered as at boot (refuge
    /// level 4 and 8 animals, Hide and Seek open), the real <see cref="HideSeekController"/> played by a scripted seeker
    /// through the runner.
    /// </summary>
    internal sealed class HideSeekRig
    {
        public ActivityTestRig A;
        public DailyVisitServices Daily;
        public HideSeekHooks Hooks;
        public HideSeekController Game = new HideSeekController();

        public static HideSeekRig Create(int level = 4, int animals = 8, bool activitiesInBuild = true)
        {
            var rig = new HideSeekRig { A = ActivityTestRig.Create(animals: animals, level: level) };
            ActivityRegistry.RegisterShipped(rig.A.S);
            rig.A.S.RefreshUnlocks();
            rig.Daily = new DailyVisitServices(rig.A.State, rig.A.Registry, rig.A.Economy, rig.A.Hatch, rig.A.SpeciesMastery, null, rig.A.P,
                rig.A.Clock, null, new DailyVisitConfig { ActivitiesAvailable = activitiesInBuild });
            rig.Hooks = new HideSeekHooks(rig.A.S, rig.Daily);
            return rig;
        }

        public ActivityRequest Request(ActivityMode mode, int challenge = -1)
        {
            var request = new ActivityRequest { Activity = ActivityId.HideAndSeek, Mode = mode, Challenge = challenge };
            request.SetAnimals(A.AnimalId(0), A.AnimalId(1), A.AnimalId(2));
            return request;
        }

        /// <summary>A whole hide-and-seek game through the runner, played by <paramref name="skill"/>.</summary>
        public ActivityGameResult Play(ActivityMode mode, SeekerSkill skill, int challenge = -1)
        {
            var request = Request(mode, challenge);
            Assert.AreEqual(ActivityStartResult.Started, A.S.Runner.TryStart(in request, Game), "Start hide and seek " + mode);
            ScriptedSeeker.Play(Game, skill, dt => A.S.Runner.Tick(dt), () => A.S.Runner.IsRunning);
            Assert.IsFalse(A.S.Runner.IsRunning, "The game ended.");
            return A.S.Runner.LastResult;
        }

        public int Treats(ContentId treat) => A.State.Progression.Treats.TryGetValue(treat, out int n) ? n : 0;

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

        /// <summary>A journal of this week holding JRN-016 with this target.</summary>
        public void FindsGoal(int target)
        {
            var visit = A.State.DailyVisit;
            visit.Goals[0] = new JournalGoalRecord { Goal = (ushort)JournalGoalId.HideAndSeekFinds, Slot = (byte)JournalSlot.Activities, Target = target };
            visit.GoalCount = 1;
            visit.JournalStartKey = Daily.TodayKey;
            visit.JournalEndKey = DailyDay.AddDays(Daily.TodayKey, 7);
            visit.FirstJournalDone = true;
        }

        public int GoalProgress => A.State.DailyVisit.Goals[0].Progress;
    }

    /// <summary>
    /// T39 « Cache-cache » in the framework (doc 01 Contenu, S-ACT-002, A18, sections 2.3 and 5.2; doc Mini-jeux, section 6):
    /// shipped and registered, opening at refuge level 4 with 8 animals, a whole cycle through <see cref="ActivityServices"/>
    /// (a ticket, stars, the record, rewards), the 3-star Mushroom Biscuit through the runner and the reward service, the
    /// wishes WISH-051 to 053 and the journal's JRN-016 through <see cref="HideSeekHooks"/>, the SYS-10 draws, and
    /// allocation-free hooks.
    /// </summary>
    public sealed class HideSeekRewardTests
    {
        [Test]
        public void T39ShipsHideAndSeek_ItsRules_AndItsMushroomBiscuit()
        {
            var definition = ActivityCatalog.Get(ActivityId.HideAndSeek);
            Assert.IsTrue(definition.ShippedInThisBuild, "T39 ships Hide and Seek.");
            Assert.IsFalse(definition.FirstGameFree, "Only Grooming's first game is offered (A15).");
            Assert.IsTrue(definition.UsesTickets, "1 ticket.");
            var rig = HideSeekRig.Create();
            var services = rig.A.S;
            Assert.IsTrue(services.Rules.Has(ActivityId.HideAndSeek));
            Assert.IsTrue(services.IsShipped(ActivityId.HideAndSeek));
            Assert.IsTrue(services.IsAvailable(ActivityId.HideAndSeek), "Level 4 and 8 animals: open (A18).");
            Assert.IsTrue(services.IsAvailable(ActivityId.Grooming), "Grooming stays.");
            Assert.AreEqual(HideSeekRules.ThreeStarTreat, services.Rewards.ItemReward(ActivityId.HideAndSeek, out int count, out int stars));
            Assert.AreEqual(1, count);
            Assert.AreEqual(3, stars);
            Assert.AreEqual(GroomingRules.ThreeStarTreat, services.Rewards.ItemReward(ActivityId.Grooming, out _, out _));
            Assert.IsFalse(HideSeekRig.Create(level: 3).A.S.IsAvailable(ActivityId.HideAndSeek), "Level 3: not open yet.");
            Assert.IsFalse(HideSeekRig.Create(animals: 7).A.S.IsAvailable(ActivityId.HideAndSeek), "7 animals: not open yet.");
        }

        [Test]
        public void AWholeCycle_ATicket_3Stars_TheMushroomBiscuit_ThenACasualGame()
        {
            var rig = HideSeekRig.Create();
            var biscuit = HideSeekRules.ThreeStarTreat;
            int treats = rig.Treats(biscuit);
            Assert.AreEqual(5, rig.A.S.Tickets.Count);
            Assert.IsFalse(rig.A.S.FreeGameAvailable(ActivityId.HideAndSeek));

            var first = rig.Play(ActivityMode.Free, SeekerSkill.Strong());
            Assert.IsFalse(first.FreeGame);
            Assert.IsTrue(first.Rewarded);
            Assert.AreEqual(4, rig.A.S.Tickets.Count, "1 ticket.");
            Assert.AreEqual(3, first.Stars, "A strong player at d3: " + first.Score);
            Assert.AreEqual(rig.Game.Score, first.Score);
            Assert.IsTrue(rig.Game.AllFound);
            Assert.Less(first.Seconds, 60.0, "Every animal found ends the game early.");
            Assert.AreEqual(AdaptiveDifficulty.Start, first.Difficulty);
            Assert.AreEqual(biscuit, first.Rewards.ItemTreat, "« Objet gagné : 1 Biscuit champignon à 3 étoiles ».");
            Assert.AreEqual(1, first.Rewards.ItemTreats);
            int stickerTreats = first.Rewards.Treat == biscuit ? first.Rewards.Treats : 0;
            Assert.AreEqual(treats + 1 + stickerTreats, rig.Treats(biscuit), "The Mushroom Biscuit reaches the treat stock.");
            Assert.AreEqual(20.0, first.Rewards.LightMinutes, 1e-9);
            Assert.AreEqual(18, rig.A.Bond.Points, "+6 bond to each of the 3 animals played.");
            Assert.AreEqual(first.Score, rig.A.Record(ActivityId.HideAndSeek).Best);

            treats = rig.Treats(biscuit);
            var second = rig.Play(ActivityMode.Free, SeekerSkill.Casual());
            Assert.AreEqual(3, rig.A.S.Tickets.Count);
            Assert.That(second.Stars, NUnit.Framework.Is.InRange(1, 2), "A casual player: " + second.Score);
            Assert.AreEqual(0, second.Rewards.ItemTreats, "No Mushroom Biscuit under 3 stars.");
            Assert.AreEqual(ContentId.None, second.Rewards.ItemTreat);
            stickerTreats = second.Rewards.Treat == biscuit ? second.Rewards.Treats : 0;
            Assert.AreEqual(treats + stickerTreats, rig.Treats(biscuit));
            Assert.AreEqual(2, rig.A.Record(ActivityId.HideAndSeek).GamesPlayed);
            Assert.AreEqual(first.Score, rig.A.Record(ActivityId.HideAndSeek).Best, "The record stays.");
        }

        [Test]
        public void PracticeGivesNoBiscuit_AChallengeAt3StarsDoes()
        {
            var rig = HideSeekRig.Create();
            var biscuit = HideSeekRules.ThreeStarTreat;
            int treats = rig.Treats(biscuit);
            var practice = rig.Play(ActivityMode.Practice, SeekerSkill.Strong());
            Assert.IsFalse(practice.Rewarded);
            Assert.AreEqual(3, practice.Stars, "Stars are shown.");
            Assert.AreEqual(0, practice.Rewards.ItemTreats);
            Assert.AreEqual(treats, rig.Treats(biscuit), "Practice gives nothing.");
            Assert.AreEqual(5, rig.A.S.Tickets.Count, "Practice is free.");

            var challenge = rig.Play(ActivityMode.Challenge, SeekerSkill.Strong(), challenge: 0);
            Assert.AreEqual(3, challenge.Stars);
            Assert.AreEqual(1, challenge.Rewards.ItemTreats, "A challenge at 3 stars gives it too.");
            Assert.AreEqual(3, rig.A.S.ChallengeStars(ActivityId.HideAndSeek, 0));
            Assert.IsTrue(rig.A.S.IsChallengeOpen(ActivityId.HideAndSeek, 1));
            Assert.IsFalse(rig.A.S.IsChallengeOpen(ActivityId.HideAndSeek, 2));

            // Challenge 6 through the runner (the five before it cleared): the 4th mistake ends the game before its time.
            int slot = ActivityCatalog.SlotOf(ActivityId.HideAndSeek);
            for (int c = 0; c < HideSeekRules.OneLookChallenge; c++)
                rig.A.Activities.ChallengeStars[ActivityState.ChallengeIndex(slot, c)] = 1;
            var request = rig.Request(ActivityMode.Practice, HideSeekRules.OneLookChallenge);
            Assert.AreEqual(ActivityStartResult.Started, rig.A.S.Runner.TryStart(in request, rig.Game));
            int empty = HideSeekTests.FreeSpot(rig.Game);
            for (int i = 0; i < 4; i++)
            {
                rig.A.S.Runner.Tick(1.1f);
                rig.Game.TapSpot(empty);
            }
            rig.A.S.Runner.Tick(0.1f);
            Assert.IsFalse(rig.A.S.Runner.IsRunning);
            Assert.AreEqual(4, rig.Game.Mistakes);
            Assert.Less(rig.A.S.Runner.LastResult.Seconds, 10.0);
        }

        // ---------------- wishes and journal (doc 01, sections 2.3 and 5.2) ----------------

        [Test]
        public void TheHook_FeedsWishes051To053_AndJournal016()
        {
            var rig = HideSeekRig.Create();
            int play = rig.AddWish(WishTemplateId.PlayHideAndSeek, 1);
            int finds = rig.AddWish(WishTemplateId.FindHiddenAnimals, 10);
            int every = rig.AddWish(WishTemplateId.FindEveryHiddenAnimal, 1);
            rig.FindsGoal(20);

            // A game left early with two animals found: they count, the game does not.
            var request = rig.Request(ActivityMode.Practice);
            Assert.AreEqual(ActivityStartResult.Started, rig.A.S.Runner.TryStart(in request, rig.Game));
            rig.A.S.Runner.Tick(1f);
            rig.Game.TapSpot(rig.Game.AnimalSpot(0));
            rig.Game.TapSpot(rig.Game.AnimalSpot(1));
            rig.A.S.Runner.Quit();
            Assert.AreEqual(1, rig.Hooks.Games);
            Assert.IsFalse(rig.Hooks.Last.Finished);
            Assert.AreEqual(2, rig.Hooks.Last.Found);
            Assert.AreEqual(0, rig.Wish(play).Progress, "WISH-051: a game left is not a game played.");
            Assert.AreEqual(2, rig.Wish(finds).Progress, "WISH-052: the animals really found count.");
            Assert.IsFalse(rig.Wish(every).IsDone);
            Assert.AreEqual(2, rig.GoalProgress, "JRN-016: the animals found.");
            rig.Game.EndGame();

            // A practice game played to its end, every animal found.
            rig.Play(ActivityMode.Practice, SeekerSkill.Strong());
            Assert.AreEqual(2, rig.Hooks.Games);
            var last = rig.Hooks.Last;
            Assert.IsTrue(last.Finished);
            Assert.IsFalse(last.Rewarded);
            Assert.IsTrue(last.AllFound);
            Assert.AreEqual(rig.Game.HiddenCount, last.Hidden);
            Assert.AreEqual(rig.Game.HiddenCount, last.Found);
            Assert.AreEqual(rig.Game.FoundBeforeHint, last.FoundBeforeHint);
            Assert.AreEqual(-1, last.Challenge);
            Assert.IsTrue(rig.Wish(play).IsDone, "WISH-051: a game played, practice included.");
            Assert.AreEqual(System.Math.Min(10, 2 + last.Found), rig.Wish(finds).Progress, "WISH-052: cumulated.");
            Assert.IsTrue(rig.Wish(every).IsDone, "WISH-053: every animal of one game.");
            Assert.AreEqual(2 + last.Found, rig.GoalProgress);

            // A game ended by its time with animals left: WISH-053 waits; another activity's game is not a hide and seek.
            int every2 = rig.AddWish(WishTemplateId.FindEveryHiddenAnimal, 1);
            request = rig.Request(ActivityMode.Practice);
            Assert.AreEqual(ActivityStartResult.Started, rig.A.S.Runner.TryStart(in request, rig.Game));
            rig.A.S.Runner.Tick(2f);
            rig.Game.TapSpot(rig.Game.AnimalSpot(0));
            for (int i = 0; i < 60 && rig.A.S.Runner.IsRunning; i++)
                rig.A.S.Runner.Tick(1f);
            Assert.IsTrue(rig.Hooks.Last.Finished);
            Assert.IsFalse(rig.Hooks.Last.AllFound);
            Assert.IsFalse(rig.Wish(every2).IsDone);
            int progress = rig.GoalProgress;
            rig.A.Play(ActivityMode.Practice, 100);
            Assert.AreEqual(3, rig.Hooks.Games, "The test activity's game is not counted.");

            // After Dispose nothing counts.
            rig.Hooks.Dispose();
            rig.Hooks.Dispose();
            rig.Play(ActivityMode.Practice, SeekerSkill.Strong());
            Assert.IsFalse(rig.Wish(every2).IsDone);
            Assert.AreEqual(progress, rig.GoalProgress);
        }

        [Test]
        public void TheSummary_OfAnotherController_StillCountsTheGame()
        {
            var result = new ActivityGameResult { Activity = ActivityId.HideAndSeek, Mode = ActivityMode.Free, Stars = 2, Score = 640, Rewarded = true, Challenge = -1 };
            var summary = HideSeekHooks.Summarize(in result, new DummyActivityController());
            Assert.IsTrue(summary.Finished);
            Assert.IsTrue(summary.Rewarded);
            Assert.AreEqual(2, summary.Stars);
            Assert.AreEqual(640, summary.Score);
            Assert.AreEqual(0, summary.Found);
            Assert.IsFalse(summary.AllFound);
            result.Abandoned = true;
            summary = HideSeekHooks.Summarize(in result, null);
            Assert.IsFalse(summary.Finished);
            Assert.IsFalse(summary.Rewarded);
            Assert.AreEqual(0, summary.Stars);
        }

        [Test]
        public void Sys10_OpensWithHideAndSeek_InABuildWithActivities()
        {
            Assert.IsTrue(WishCatalog.IsShipped(WishSystem.HideAndSeek));
            var ids = new[] { WishTemplateId.PlayHideAndSeek, WishTemplateId.FindHiddenAnimals, WishTemplateId.FindEveryHiddenAnimal };
            foreach (var id in ids)
            {
                Assert.IsTrue(WishCatalog.TryGet(id, out var template));
                Assert.IsTrue(template.ShippedInThisBuild, id.ToString());
                Assert.AreEqual(WishSystem.HideAndSeek, template.System);
                Assert.AreEqual(WishFamily.Games, template.Family);
            }
            Assert.IsTrue(WishCatalog.TryGet(WishTemplateId.FindHiddenAnimals, out var find));
            Assert.AreEqual(new[] { 4, 6, 8, 10 }, new[] { find.P1, find.P2, find.P3, find.P4 });
            Assert.IsTrue(WeeklyJournalCatalog.TryGet(JournalGoalId.HideAndSeekFinds, out var goal));
            Assert.IsTrue(goal.ShippedInThisBuild, "JRN-016 ships with Hide and Seek.");
            Assert.AreEqual(JournalSlot.Activities, goal.Slot);
            Assert.AreEqual(new[] { 20, 30, 40, 50 }, new[] { goal.P1, goal.P2, goal.P3, goal.P4 });

            // Refuge level 3, or 7 animals: closed. Level 4 and 8 animals: open, and the draws bring hide-and-seek wishes.
            var rig = WishTestRig.Create(animals: 8, config: new DailyVisitConfig { ActivitiesAvailable = true });
            rig.State.Progression.Refuge.Level = 3;
            rig.Wishes.RefreshSystemUnlocks();
            Assert.IsFalse(rig.Wishes.IsSystemOpen(WishSystem.HideAndSeek));
            var journal = new WeeklyJournalService(rig.Ctx);
            Assert.AreEqual(0.0, journal.DrawWeight(JournalGoalId.HideAndSeekFinds, false));

            rig.State.Progression.Refuge.Level = 4;
            rig.Wishes.RefreshSystemUnlocks();
            Assert.IsTrue(rig.Wishes.IsSystemOpen(WishSystem.HideAndSeek));
            Assert.Greater(journal.DrawWeight(JournalGoalId.HideAndSeekFinds, false), 0.0, "JRN-016 in the Activities slot.");
            int hideSeek = 0;
            for (int day = 2; day <= 14; day++)
            {
                rig.Wishes.DrawForDay(rig.SetDay(day));
                hideSeek += rig.Stock().Count(w => WishTestRig.SystemOf(w) == WishSystem.HideAndSeek);
                var visit = rig.State.DailyVisit;
                for (int i = 0; i < visit.WishCount; i++)
                {
                    visit.Wishes[i].Progress = visit.Wishes[i].Target;
                    visit.Wishes[i].Flags |= WishRecord.FlagDone;
                }
                rig.Wishes.ClaimAll(WishClaimPath.ClaimAll);
            }
            Assert.Greater(hideSeek, 0, "Hide-and-seek wishes are drawn.");

            var few = WishTestRig.Create(animals: 7, config: new DailyVisitConfig { ActivitiesAvailable = true });
            few.State.Progression.Refuge.Level = 4;
            few.Wishes.RefreshSystemUnlocks();
            Assert.IsFalse(few.Wishes.IsSystemOpen(WishSystem.HideAndSeek), "7 animals: closed.");

            // A build without activities never opens SYS-10.
            var closed = WishTestRig.Create(animals: 8);
            closed.State.Progression.Refuge.Level = 4;
            closed.Wishes.RefreshSystemUnlocks();
            Assert.IsFalse(closed.Wishes.IsSystemOpen(WishSystem.HideAndSeek));
            Assert.AreEqual(0.0, new WeeklyJournalService(closed.Ctx).DrawWeight(JournalGoalId.HideAndSeekFinds, false));
        }

        [Test]
        public void TheHideSeekGame_AndItsHook_DoNotAllocate()
        {
            var rig = HideSeekRig.Create();
            rig.AddWish(WishTemplateId.FindHiddenAnimals, 1000);
            rig.AddWish(WishTemplateId.PlayHideAndSeek, 1000);
            rig.AddWish(WishTemplateId.FindEveryHiddenAnimal, 1000);
            rig.FindsGoal(1000);
            var summary = new HideSeekGameSummary { Finished = true, Rewarded = true, Stars = 2, Hidden = 6, Found = 6, AllFound = true };
            rig.Daily.OnHideSeekGame(in summary);
            Assert.That(() => rig.Daily.OnHideSeekGame(in summary), Is.Not.AllocatingGCMemory());

            var request = rig.Request(ActivityMode.Practice);
            Assert.AreEqual(ActivityStartResult.Started, rig.A.S.Runner.TryStart(in request, rig.Game));
            rig.A.S.Runner.Tick(0.016f);
            Assert.That(() => rig.A.S.Runner.Tick(0.016f), Is.Not.AllocatingGCMemory(), "The runner ticking the hide-and-seek game.");
        }
    }
}
