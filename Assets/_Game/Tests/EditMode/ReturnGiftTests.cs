using System;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T36, doc « 01 — Contenu des rendez-vous », section 6 (return gifts): nothing below 3 days, the short gift from 3 to
    /// 13 days, the big one from 14; at most one gift per 7 days and one big gift per 30 (the short one in between); Warm
    /// Return from 2 days with one more egg; nothing after a clock moved back or before the welcome; an absence longer than
    /// the offline cap is still measured; collecting gives Light at the current production, tickets (or their Light while
    /// the Play button does not exist), the Discovery egg and the treats.
    /// </summary>
    public class ReturnGiftTests
    {
        private const double Day = 86_400.0;
        private static readonly ContentId Rabbit = ContentId.FromSource("species.rabbit");
        private static readonly ContentId CloverCrunch = ContentId.FromSource("treat.clover_crunch");
        private static readonly ContentId WarmReturnNode = ContentId.FromSource("essence.offline.warm_return");

        private sealed class Rig
        {
            public PlayerState State;
            public FakeGameClock Clock;
            public EconomyService Economy;
            public HatchService Hatch;
            public ProgressionServices P;
            public DailyVisitConfig Config;
            public DailyContext Ctx;
            public ReturnGiftService Returns;
            public int EggsGranted;
            public DiscoverySource EggSource;

            /// <summary>The player stays away, then the game resolves the return (a report shaped like OfflineService's).</summary>
            public void ComeBack(double days, bool rolledBack = false)
            {
                Clock.Advance(TimeSpan.FromSeconds(days * Day));
                Returns.Evaluate(Report(days, rolledBack));
            }

            public byte PendingKind => Returns.Pending.Kind;

            public void Collect() => Assert.IsTrue(Returns.TryClaim(out _), "A gift was waiting.");
        }

        private static OfflineReport Report(double days, bool rolledBack = false)
        {
            double away = days * Day;
            double credited = Math.Min(away, EconomyConfig.Default.OfflineMaxSeconds);
            return new OfflineReport(away, credited, credited, BigValue.One, BigValue.Zero, rolledBack, away);
        }

        private static Rig Create(bool warm = false, bool onboarded = true, EconomyConfig economy = null)
        {
            var content = TestUtil.ThreeSpecies();
            var rig = new Rig { State = PlayerState.CreateNew(TestUtil.T0), Clock = new FakeGameClock(TestUtil.T0), Config = new DailyVisitConfig() };
            rig.State.DailyVisit.OnboardingDone = onboarded;
            rig.State.Collection.Add(new AnimalInstance { Species = Rabbit, Rarity = RarityTier.Common });
            if (warm)
                rig.State.Progression.EssenceRanks[WarmReturnNode] = 1;
            var config = new ProgressionConfig { TimeZone = TimeZoneInfo.Utc };
            rig.Economy = new EconomyService(rig.State, content, economy ?? EconomyConfig.Default, null);
            rig.Hatch = new HatchService(rig.State, content, rig.Economy, rig.Clock, null);
            rig.Hatch.DiscoveryEggsGranted += (count, source) =>
            {
                rig.EggsGranted += count;
                rig.EggSource = source;
            };
            if (warm)
                rig.P = new ProgressionServices(rig.State, content, rig.Economy, rig.Hatch, rig.Clock, null, config);
            rig.Ctx = new DailyContext(rig.State, content, rig.Config, config, rig.Clock, rig.Economy, rig.Hatch, null, rig.P, null);
            rig.Returns = new ReturnGiftService(rig.Ctx);
            return rig;
        }

        // ---------------- How long (sections 6.1 to 6.3) ----------------

        [Test]
        public void LessThanThreeDays_NoGift()
        {
            var rig = Create();
            rig.ComeBack(2.99);
            Assert.IsFalse(rig.Returns.HasPending, "The offline summary only (T18).");
            Assert.AreEqual(0, rig.State.DailyVisit.LastReturnGiftDay, "Nothing recorded.");
        }

        [TestCase(3.0, 3)]
        [TestCase(7.5, 7)]
        [TestCase(13.99, 13)]
        public void ThreeToThirteenDays_TheShortGift(double days, int whole)
        {
            var rig = Create();
            rig.ComeBack(days);
            Assert.IsTrue(rig.Returns.HasPending);
            var gift = rig.Returns.Pending;
            Assert.AreEqual(ReturnGift.Short, gift.Kind);
            Assert.AreEqual(whole, gift.DaysAway);
            Assert.IsFalse(gift.Warm);
            Assert.AreEqual(120.0 + 3 * 15.0, gift.Bundle.LightMinutes, 1e-9, "Light 120 min, and 3 tickets as Light before the Play button.");
            Assert.AreEqual(0, gift.Bundle.Tickets);
            Assert.AreEqual(1, gift.Bundle.DiscoveryEggs);
            Assert.AreEqual(CloverCrunch, gift.Bundle.Treat, "The newest biome's treat.");
            Assert.AreEqual(3, gift.Bundle.Treats);
            Assert.AreEqual(0, gift.Bundle.Stickers);
            Assert.AreEqual(0L, gift.Bundle.Gems);
            Assert.IsTrue(gift.Bundle.Substituted);
            Assert.AreEqual(rig.Ctx.DayNumber, rig.State.DailyVisit.LastReturnGiftDay);
            Assert.AreEqual(0, rig.State.DailyVisit.LastBigReturnGiftDay);
        }

        [TestCase(14.0, 14)]
        [TestCase(40.0, 40)]
        public void FourteenDaysOrMore_TheBigGift(double days, int whole)
        {
            var rig = Create();
            rig.ComeBack(days);
            var gift = rig.Returns.Pending;
            Assert.AreEqual(ReturnGift.Big, gift.Kind);
            Assert.IsTrue(gift.IsBig);
            Assert.AreEqual(whole, gift.DaysAway, "Measured past the 30-day offline cap too.");
            Assert.AreEqual(240.0 + 5 * 15.0, gift.Bundle.LightMinutes, 1e-9, "Light 240 min, and 5 tickets as Light.");
            Assert.AreEqual(1, gift.Bundle.DiscoveryEggs);
            Assert.AreEqual(5 + 3, gift.Bundle.Treats, "5 treats, and the sticker as 3 more treats before the albums.");
            Assert.AreEqual(0, gift.Bundle.Stickers);
            Assert.AreEqual(GiftSize.Big, gift.Bundle.Size);
            Assert.AreEqual(rig.Ctx.DayNumber, rig.State.DailyVisit.LastBigReturnGiftDay);
        }

        [Test]
        public void OpenSystems_GetTheirOwnRewards()
        {
            var rig = Create();
            rig.Config.ActivitiesAvailable = true;
            rig.Config.AlbumsAvailable = true;
            rig.ComeBack(20);
            var gift = rig.Returns.Pending;
            Assert.AreEqual(240.0, gift.Bundle.LightMinutes, 1e-9);
            Assert.AreEqual(5, gift.Bundle.Tickets);
            Assert.AreEqual(5, gift.Bundle.Treats);
            Assert.AreEqual(1, gift.Bundle.Stickers, "A missing sticker of the player's choice.");
            Assert.IsFalse(gift.Bundle.Substituted);
        }

        [Test]
        public void RemoteConfig_MovesTheMinimumDays()
        {
            var rig = Create();
            Assert.IsTrue(rig.Config.TryOverride("daily.return_gift_min_days", 5));
            rig.ComeBack(4.5);
            Assert.IsFalse(rig.Returns.HasPending);
            rig.ComeBack(5);
            Assert.IsTrue(rig.Returns.HasPending);
        }

        // ---------------- Frequency ----------------

        [Test]
        public void AtMostOneGiftPerSevenDays()
        {
            var rig = Create();
            rig.ComeBack(3);
            Assert.AreEqual(4, rig.State.DailyVisit.LastReturnGiftDay, "Day 4.");
            rig.Collect();

            rig.ComeBack(3);
            Assert.IsFalse(rig.Returns.HasPending, "Day 7: 3 days after the last gift.");
            rig.ComeBack(4);
            Assert.IsTrue(rig.Returns.HasPending, "Day 11: 7 days after.");
            Assert.AreEqual(ReturnGift.Short, rig.PendingKind);
            Assert.AreEqual(11, rig.State.DailyVisit.LastReturnGiftDay);
        }

        [Test]
        public void AtMostOneBigGiftPerThirtyDays_TheShortOneInBetween()
        {
            var rig = Create();
            rig.ComeBack(14);
            Assert.AreEqual(ReturnGift.Big, rig.PendingKind, "Day 15.");
            rig.Collect();

            rig.ComeBack(14);
            Assert.AreEqual(ReturnGift.Short, rig.PendingKind, "Day 29: 14 days away, but 14 days after the big one.");
            Assert.AreEqual(14, rig.Returns.Pending.DaysAway);
            Assert.AreEqual(15, rig.State.DailyVisit.LastBigReturnGiftDay);
            rig.Collect();

            rig.ComeBack(16);
            Assert.AreEqual(ReturnGift.Big, rig.PendingKind, "Day 45: 30 days after the big one.");
            Assert.AreEqual(45, rig.State.DailyVisit.LastBigReturnGiftDay);
        }

        [Test]
        public void AWaitingGift_StaysAsItWas_AndOneAbsenceGivesOneGift()
        {
            var rig = Create();
            rig.ComeBack(5);
            rig.ComeBack(20);
            Assert.AreEqual(ReturnGift.Short, rig.PendingKind, "Not collected yet: the waiting gift stays as it was.");
            Assert.AreEqual(5, rig.Returns.Pending.DaysAway);
            rig.Collect();
            Assert.IsFalse(rig.Returns.TryClaim(out _), "Collected once.");

            var again = Create();
            again.ComeBack(5);
            again.Collect();
            again.Returns.Evaluate(Report(5));
            Assert.IsFalse(again.Returns.HasPending, "The same return resolved again (boot, then resume) gives nothing.");
        }

        // ---------------- Warm Return (doc 02 Renouveau, section 2) ----------------

        [Test]
        public void WarmReturn_FromTwoDays_WithOneMoreEgg()
        {
            var cold = Create();
            cold.ComeBack(2);
            Assert.IsFalse(cold.Returns.WarmReturn);
            Assert.IsFalse(cold.Returns.HasPending, "Two days are not enough without the node.");

            var rig = Create(warm: true);
            Assert.IsTrue(rig.Returns.WarmReturn);
            rig.ComeBack(1.9);
            Assert.IsFalse(rig.Returns.HasPending, "Still below 2 days.");
            rig.ComeBack(2);
            var gift = rig.Returns.Pending;
            Assert.AreEqual(ReturnGift.Short, gift.Kind);
            Assert.IsTrue(gift.Warm);
            Assert.AreEqual(2, gift.Bundle.DiscoveryEggs);
            rig.Collect();
            Assert.AreEqual(2, rig.State.World.DiscoveryEggs);

            var big = Create(warm: true);
            big.ComeBack(14);
            Assert.AreEqual(ReturnGift.Big, big.PendingKind);
            Assert.AreEqual(2, big.Returns.Pending.Bundle.DiscoveryEggs);
        }

        // ---------------- Never ----------------

        [Test]
        public void ClockMovedBack_GivesNothing()
        {
            var rig = Create();
            rig.ComeBack(10, rolledBack: true);
            Assert.IsFalse(rig.Returns.HasPending);
            Assert.AreEqual(0, rig.State.DailyVisit.LastReturnGiftDay);

            // The real offline service: the profile already saw a later time than the clock shows now.
            var real = Create();
            real.State.Offline.HighWaterUtc = TestUtil.T0 + TimeSpan.FromDays(20);
            real.Clock.Set(TestUtil.T0 + TimeSpan.FromDays(10));
            var report = new OfflineService(real.State, real.Economy, real.Clock).ResolveReturn();
            Assert.IsTrue(report.ClockRolledBack);
            real.Returns.Evaluate(report);
            Assert.IsFalse(real.Returns.HasPending);
        }

        [Test]
        public void BeforeTheWelcome_NoGift()
        {
            var rig = Create(onboarded: false);
            rig.ComeBack(5);
            Assert.IsFalse(rig.Returns.HasPending, "The welcome is not over.");
            Assert.AreEqual(0, rig.State.DailyVisit.LastReturnGiftDay);

            // A save that already holds 5 animals skips the welcome (doc 01, section 1.1).
            for (int i = 0; i < 4; i++)
                rig.State.Collection.Add(new AnimalInstance { Species = Rabbit, Rarity = RarityTier.Common });
            rig.ComeBack(5);
            Assert.IsTrue(rig.Returns.HasPending);
        }

        [Test]
        public void FlagOff_HidesWithoutErasing()
        {
            var rig = Create();
            rig.Config.ReturnGiftEnabled = false;
            rig.ComeBack(5);
            Assert.IsFalse(rig.Returns.HasPending);

            rig.Config.ReturnGiftEnabled = true;
            rig.ComeBack(5);
            Assert.IsTrue(rig.Returns.HasPending);
            rig.Config.ReturnGiftEnabled = false;
            rig.Returns.Evaluate(Report(30));
            Assert.IsTrue(rig.Returns.HasPending, "A waiting gift stays when the flag goes off.");
            Assert.AreEqual(ReturnGift.Short, rig.PendingKind);
        }

        // ---------------- Measuring the absence ----------------

        [Test]
        public void AbsenceLongerThanTheOfflineCap_IsStillMeasured()
        {
            var rig = Create(economy: new EconomyConfig { OfflineMaxSeconds = 7 * Day });
            rig.Clock.Advance(TimeSpan.FromDays(20));
            var report = new OfflineService(rig.State, rig.Economy, rig.Clock).ResolveReturn();
            Assert.AreEqual(7 * Day, report.CreditedSeconds, 1e-6, "econ.offline.max_days = 7 caps the Light.");
            Assert.AreEqual(20 * Day, report.AwaySeconds, 1e-6);

            rig.Returns.Evaluate(report);
            Assert.AreEqual(ReturnGift.Big, rig.PendingKind);
            Assert.AreEqual(20, rig.Returns.Pending.DaysAway);
        }

        [Test]
        public void DaysAway_TheUnseenTimeBelowTheCap_TheUncappedTimeAbove()
        {
            Assert.AreEqual(3, ReturnGiftService.DaysAway(new OfflineReport(10 * Day, 3.5 * Day, 0, BigValue.Zero, BigValue.Zero, false), 30 * Day),
                "From the high-water mark (time already seen never counts twice).");
            Assert.AreEqual(20, ReturnGiftService.DaysAway(new OfflineReport(20.5 * Day, 7 * Day, 0, BigValue.Zero, BigValue.Zero, false, 20.2 * Day), 7 * Day),
                "Past the cap: the unseen time without the cap.");
            Assert.AreEqual(0, ReturnGiftService.DaysAway(default, 30 * Day));
        }

        // ---------------- Collecting ----------------

        [Test]
        public void Collect_GivesLightEggAndTreats_TicketsAsLight()
        {
            var rig = Create();
            double rate = rig.Economy.GetLightPerSecond().ToDoubleClamped();
            Assert.AreEqual(1.0, rate, 1e-9, "One Common rabbit.");
            rig.ComeBack(4);
            double lightBefore = rig.State.Wallet.Light.ToDoubleClamped();
            ReturnGift seen = default;
            int events = 0;
            rig.Returns.Granted += gift =>
            {
                seen = gift;
                events++;
            };

            Assert.IsTrue(rig.Returns.TryClaim(out var granted));
            double expected = rate * 60.0 * (120.0 + 3 * 15.0);
            Assert.AreEqual(expected, granted.Light.ToDoubleClamped(), 1e-6, "Minutes of the current production.");
            Assert.AreEqual(lightBefore + expected, rig.State.Wallet.Light.ToDoubleClamped(), 1e-6);
            Assert.AreEqual(0, rig.State.Inventory.Tickets, "Tickets became Light.");
            Assert.AreEqual(1, rig.State.World.DiscoveryEggs);
            Assert.AreEqual(1, rig.EggsGranted);
            Assert.AreEqual(DiscoverySource.ReturnGift, rig.EggSource);
            Assert.AreEqual(3, rig.State.Progression.Treats[CloverCrunch]);
            Assert.AreEqual(0L, rig.State.Wallet.Gems);

            Assert.AreEqual(1, events);
            Assert.AreEqual(ReturnGift.Short, seen.Kind);
            Assert.AreEqual(4, seen.DaysAway);
            Assert.IsFalse(rig.Returns.HasPending);
            Assert.AreEqual(0, rig.State.DailyVisit.PendingReturnGift);
            Assert.IsFalse(rig.Returns.TryClaim(out _));
        }

        [Test]
        public void Collect_WithThePlayButton_GivesTickets()
        {
            var rig = Create();
            rig.Config.ActivitiesAvailable = true;
            rig.ComeBack(14);
            Assert.IsTrue(rig.Returns.TryClaim(out var granted));
            Assert.AreEqual(60.0 * 240.0, granted.Light.ToDoubleClamped(), 1e-6);
            Assert.AreEqual(5, rig.State.Inventory.Tickets);
            Assert.AreEqual(8, rig.State.Progression.Treats[CloverCrunch], "5 treats and the sticker's 3 before the albums.");
            Assert.AreEqual(0, rig.State.Inventory.PendingStickers);
            Assert.AreEqual(1, rig.State.World.DiscoveryEggs);
        }
    }
}
