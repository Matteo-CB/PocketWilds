using System;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T36: the facade of the rendez-vous (DailyVisitServices): the day change and its order, the welcome's end opening
    /// Today, the game's events reaching the wishes, the rail's dot, the clock moved back, and a tick that never allocates.
    /// </summary>
    public sealed class DailyVisitServicesTests
    {
        private static readonly ContentId BasicEgg = WishTestRig.BasicEgg;

        private sealed class Rig
        {
            public PlayerState State;
            public FakeGameClock Clock;
            public EconomyService Economy;
            public HatchService Hatch;
            public ProgressionServices P;
            public DailyVisitServices Daily;
        }

        /// <param name="animals">Prairie hatches before the facade exists (5 or more: the welcome counts as over).</param>
        private static Rig Create(int animals, bool levelTwo = true)
        {
            var registry = WishTestRig.BuildContent();
            var rig = new Rig
            {
                State = PlayerState.CreateNew(TestUtil.T0),
                Clock = new FakeGameClock(TestUtil.T0),
            };
            rig.State.Meta.ProfileId = new Guid(36, 7, 11, 1, 2, 3, 4, 5, 6, 7, 8);
            var progressionConfig = new ProgressionConfig { TimeZone = TimeZoneInfo.Utc };
            rig.Economy = new EconomyService(rig.State, registry, EconomyConfig.Default, null);
            var mastery = new MasteryService(rig.State, rig.Economy.Config, rig.Economy);
            rig.Hatch = new HatchService(rig.State, registry, rig.Economy, rig.Clock, null, mastery);
            rig.P = new ProgressionServices(rig.State, registry, rig.Economy, rig.Hatch, rig.Clock, null, progressionConfig, mastery);
            rig.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(1e12), EconomyReason.Debug);
            for (int i = 0; i < animals; i++)
                Assert.IsTrue(rig.Hatch.Hatch(BasicEgg).Succeeded, "Rig hatch " + i);
            if (levelTwo && rig.State.Progression.Refuge.Level < 2)
                rig.State.Progression.Refuge.Level = 2;
            rig.Daily = new DailyVisitServices(rig.State, registry, rig.Economy, rig.Hatch, mastery, null, rig.P, rig.Clock, null);
            return rig;
        }

        private static OfflineReport Away(double seconds) =>
            new OfflineReport(seconds, seconds, seconds, BigValue.Zero, BigValue.Zero, false, seconds);

        [Test]
        public void FirstResume_CountsTheLoginDay_DrawsTheWishes_AndLightsTheDot()
        {
            var rig = Create(6);
            int changes = 0;
            rig.Daily.DayChanged += key => changes++;
            rig.Daily.OnResume(Away(0));

            var visit = rig.State.DailyVisit;
            Assert.AreEqual(1, changes);
            Assert.AreEqual(1, visit.LoginDays);
            Assert.AreEqual(DailyDay.DayKey(TestUtil.T0, TimeZoneInfo.Utc, 0), visit.LastDayKey);
            Assert.AreEqual(visit.LastDayKey, rig.Daily.TodayKey);
            Assert.IsTrue(rig.Daily.TodayUnlocked, "Level 2 and five animals: Today is open.");
            Assert.Greater(rig.Daily.Wishes.PendingCount, 0, "Today's wishes are drawn with the day.");
            Assert.IsTrue(rig.Daily.TodayDot, "The welcome gift, the gift and the calendar wait.");

            // The same day again changes nothing.
            int pending = rig.Daily.Wishes.PendingCount;
            rig.Daily.OnResume(Away(60));
            Assert.AreEqual(1, changes);
            Assert.AreEqual(1, visit.LoginDays);
            Assert.AreEqual(pending, rig.Daily.Wishes.PendingCount);
        }

        [Test]
        public void TodayWaitsForLevelTwo_AndTheWelcome()
        {
            var rig = Create(6, levelTwo: false);
            rig.State.Progression.Refuge.Level = 1;
            rig.Daily.OnResume(Away(0));
            Assert.IsFalse(rig.Daily.TodayUnlocked);
            Assert.IsFalse(rig.Daily.TodayDot);
            Assert.AreEqual(0, rig.Daily.Wishes.PendingCount, "No wish before Today exists.");
            Assert.AreEqual(1, rig.State.DailyVisit.LoginDays, "The login day counts anyway.");
        }

        [Test]
        public void TheTutorialWish_EndsTheWelcome_AndOpensToday()
        {
            var rig = Create(2);
            rig.Daily.OnResume(Away(0));
            Assert.IsFalse(rig.State.IsOnboardingDone);
            Assert.IsFalse(rig.Daily.TodayUnlocked, "Two animals: the welcome is still on.");

            int opened = 0;
            rig.Daily.TodayUnlockedNow += () => opened++;
            Assert.IsTrue(rig.State.Collection.TryGetIndex(1, out int first));
            rig.Daily.Wishes.StartTutorialWish(rig.State.Collection[first].InstanceId);
            Assert.IsTrue(rig.Daily.Wishes.TutorialActive);

            // The hatches reach the tutorial wish through the facade (HatchService.Hatched).
            for (int i = 0; i < 2 && !rig.State.DailyVisit.OnboardingDone; i++)
                Assert.IsTrue(rig.Hatch.Hatch(BasicEgg).Succeeded);

            Assert.IsTrue(rig.State.DailyVisit.OnboardingDone, "The tutorial wish ends the welcome.");
            Assert.IsTrue(rig.Daily.TodayUnlocked);
            Assert.AreEqual(1, opened, "Today opens at once, not at the next day.");
            Assert.Greater(rig.Daily.Wishes.PendingCount, 0, "Today's first wishes are drawn as it opens.");
        }

        [Test]
        public void TheBoundary_IsFoundByTheTick_OnceASecond()
        {
            var rig = Create(6);
            rig.Daily.OnResume(Away(0));
            int day = rig.Daily.TodayKey;
            int changes = 0;
            rig.Daily.DayChanged += key => changes++;

            var boundary = DailyDay.NextBoundaryUtc(rig.Clock.UtcNow, TimeZoneInfo.Utc, 0);
            rig.Clock.Set(boundary.AddSeconds(-2));
            rig.Daily.Tick(1.0);
            Assert.AreEqual(0, changes);
            rig.Clock.Set(boundary.AddMilliseconds(500));
            rig.Daily.Tick(0.4);
            Assert.AreEqual(0, changes, "Checked once a second, not every frame.");
            rig.Daily.Tick(0.7);
            Assert.AreEqual(1, changes);
            Assert.AreEqual(DailyDay.AddDays(day, 1), rig.Daily.TodayKey);
            Assert.AreEqual(2, rig.State.DailyVisit.LoginDays);
            Assert.IsTrue(rig.Daily.Gift.CanOpen, "A new day, a new gift.");
        }

        [Test]
        public void AClockMovedBack_NeverRegrants()
        {
            var rig = Create(6);
            rig.Clock.Advance(TimeSpan.FromDays(1));
            rig.Daily.OnResume(Away(0));
            Assert.IsTrue(rig.Daily.Gift.TryOpen(out _));
            Assert.IsTrue(rig.Daily.Calendar.TryClaim(out _));
            int day = rig.Daily.TodayKey;

            // The profile saw this day: the high-water mark holds it, a clock set back a day changes nothing.
            rig.State.Offline.HighWaterUtc = rig.Clock.UtcNow;
            rig.Clock.Set(rig.Clock.UtcNow.AddDays(-1));
            rig.Daily.CheckDay();
            Assert.AreEqual(day, rig.Daily.TodayKey);
            Assert.AreEqual(1, rig.State.DailyVisit.LoginDays);
            Assert.IsFalse(rig.Daily.Gift.CanOpen);
            Assert.IsFalse(rig.Daily.Calendar.CanClaim);
        }

        [Test]
        public void ADayAway_IsOneLoginDay_AndNothingIsLost()
        {
            var rig = Create(6);
            rig.Daily.OnResume(Away(0));
            int stock = rig.Daily.Wishes.PendingCount;
            rig.Clock.Advance(TimeSpan.FromDays(5));
            rig.Daily.OnResume(Away(5 * 86400));
            Assert.AreEqual(2, rig.State.DailyVisit.LoginDays, "Days away are not login days.");
            Assert.GreaterOrEqual(rig.Daily.Wishes.PendingCount, stock, "Pending wishes wait (doc 01, section 6.4).");
            Assert.AreEqual(ReturnGift.Short, rig.State.DailyVisit.PendingReturnGift, "Five days away: the short return gift waits.");
        }

        [Test]
        public void Dispose_StopsListening()
        {
            var rig = Create(6);
            rig.Daily.OnResume(Away(0));
            rig.Daily.Dispose();
            Assert.DoesNotThrow(() => rig.Hatch.Hatch(BasicEgg));
        }

        [Test]
        public void TickAndDot_DoNotAllocate()
        {
            var rig = Create(6);
            rig.Daily.OnResume(Away(0));
            rig.Daily.Tick(1.0);
            bool dot = rig.Daily.TodayDot;
            Assert.That(() => rig.Daily.Tick(0.016), Is.Not.AllocatingGCMemory());
            Assert.That(() => rig.Daily.Tick(1.0), Is.Not.AllocatingGCMemory(), "A check without a boundary.");
            Assert.That(new TestDelegate(() => { dot = rig.Daily.TodayDot; }), Is.Not.AllocatingGCMemory());
            Assert.IsTrue(dot);
        }
    }
}
