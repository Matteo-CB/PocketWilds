using System;
using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T36, doc « 01 — Contenu des rendez-vous, des activités et de l'accueil », section 1.4: 100, 100 and 100 Gems by login
    /// day (2 tickets on day 2, 5 seeds of Trèfle doux on day 3), skipped days lose nothing, three at most, one a day; the
    /// return gift tables of sections 6.2 and 6.3, which share the same substitutions.
    /// </summary>
    public class WelcomeGiftTests
    {
        private static readonly ContentId CloverCrunch = ContentId.FromSource("treat.clover_crunch");

        private sealed class Rig
        {
            public PlayerState State;
            public FakeGameClock Clock;
            public EconomyService Economy;
            public DailyVisitConfig Config;
            public DailyContext Ctx;
            public WelcomeGiftService Welcome;

            /// <summary>Moves to a later day, as the facade does at a day boundary.</summary>
            public void NextDay(int days = 1)
            {
                Clock.Advance(TimeSpan.FromDays(days));
                Ctx.TodayKey = DailyDay.DayKey(DailyDay.Now(State, Clock), Ctx.Zone, Config.ResetHour);
            }

            public GrantedRewards Claim()
            {
                Assert.IsTrue(Welcome.TryClaim(out var granted), "today's welcome gift");
                return granted;
            }
        }

        private static Rig Create(DailyVisitConfig config = null, bool onboarded = true)
        {
            var content = TestUtil.ThreeSpecies();
            var rig = new Rig
            {
                State = PlayerState.CreateNew(TestUtil.T0),
                Clock = new FakeGameClock(TestUtil.T0),
                Config = config ?? new DailyVisitConfig(),
            };
            rig.State.DailyVisit.OnboardingDone = onboarded;
            rig.Economy = new EconomyService(rig.State, content, EconomyConfig.Default, null);
            var hatch = new HatchService(rig.State, content, rig.Economy, rig.Clock, null);
            rig.Ctx = new DailyContext(rig.State, content, rig.Config, new ProgressionConfig { TimeZone = TimeZoneInfo.Utc }, rig.Clock,
                rig.Economy, hatch, null, null, null);
            rig.Welcome = new WelcomeGiftService(rig.Ctx);
            return rig;
        }

        private static RewardContext Closed() => new RewardContext(false, false, false, false, CloverCrunch, 15.0);

        private static RewardContext Open() => new RewardContext(true, true, true, true, CloverCrunch, 15.0);

        // ---------------- Welcome gifts (section 1.4) ----------------

        [Test]
        public void ThreeLoginDays_Give100Gems_Each_WithTicketsThenSeeds()
        {
            var rig = Create();
            Assert.AreEqual(0, rig.Welcome.NextIndex);

            var day1 = rig.Claim();
            Assert.AreEqual(100, day1.Bundle.Gems);
            Assert.AreEqual(100, rig.State.Wallet.Gems);
            Assert.AreEqual(0, day1.Bundle.Tickets);
            Assert.AreEqual(0, day1.Bundle.Seeds);

            rig.NextDay();
            var day2 = rig.Claim();
            Assert.AreEqual(100, day2.Bundle.Gems);
            Assert.AreEqual(30.0, day2.Bundle.LightMinutes, "2 tickets become Light 15 min each before the Play button");
            Assert.IsTrue(day2.Bundle.Substituted);
            Assert.AreEqual(0, rig.State.Inventory.Tickets);

            rig.NextDay();
            var day3 = rig.Claim();
            Assert.AreEqual(100, day3.Bundle.Gems);
            Assert.AreEqual(5, day3.Bundle.Seeds);
            Assert.AreEqual(DailyRewardTables.SweetClover, day3.Bundle.SeedPlant);
            Assert.IsFalse(day3.Bundle.Substituted, "the welcome seeds are kept for the Garden, never replaced");
            Assert.AreEqual(5, rig.State.Inventory.Count(ContentId.FromSource("plant.sweet_clover")));

            Assert.AreEqual(300, rig.State.Wallet.Gems, "doc Gemmes: 300 Gems of welcome");
            Assert.IsTrue(rig.Welcome.AllClaimed);
        }

        [Test]
        public void Day2Tickets_GoToTheInventory_OnceThePlayButtonExists()
        {
            var rig = Create(new DailyVisitConfig { ActivitiesAvailable = true });
            rig.Claim();
            rig.NextDay();
            var day2 = rig.Claim();
            Assert.AreEqual(2, day2.Bundle.Tickets);
            Assert.AreEqual(0.0, day2.Bundle.LightMinutes);
            Assert.AreEqual(2, rig.State.Inventory.Tickets);
        }

        [Test]
        public void SkippedDays_LoseNothing()
        {
            var rig = Create();
            rig.Claim();
            rig.NextDay(10);
            Assert.AreEqual(1, rig.Welcome.NextIndex, "the second gift waits");
            Assert.IsTrue(rig.Welcome.CanClaim);
            Assert.AreEqual(100, rig.Claim().Bundle.Gems);
            rig.NextDay(3);
            var third = rig.Claim();
            Assert.AreEqual(5, third.Bundle.Seeds, "the third gift on the third login day");
            Assert.AreEqual(300, rig.State.Wallet.Gems);
        }

        [Test]
        public void AtMostThree()
        {
            var rig = Create();
            int claimed = 0;
            for (int day = 0; day < 10; day++)
            {
                if (rig.Welcome.TryClaim(out _))
                    claimed++;
                rig.NextDay();
            }
            Assert.AreEqual(3, claimed);
            Assert.AreEqual(3, rig.State.DailyVisit.WelcomeClaimed);
            Assert.AreEqual(300, rig.State.Wallet.Gems);
            Assert.IsFalse(rig.Welcome.CanClaim);
            Assert.AreEqual(3, rig.Welcome.NextIndex);
            Assert.IsTrue(rig.Welcome.Peek().IsEmpty);
        }

        [Test]
        public void AtMostOnePerDay()
        {
            var rig = Create();
            rig.Claim();
            Assert.IsTrue(rig.Welcome.ClaimedToday);
            Assert.IsFalse(rig.Welcome.CanClaim);
            Assert.IsFalse(rig.Welcome.TryClaim(out _), "a second welcome gift the same day");
            rig.Clock.Advance(TimeSpan.FromHours(11));
            rig.Ctx.TodayKey = DailyDay.DayKey(DailyDay.Now(rig.State, rig.Clock), rig.Ctx.Zone, rig.Config.ResetHour);
            Assert.IsFalse(rig.Welcome.CanClaim, "still the same day at 23:00");
            Assert.AreEqual(100, rig.State.Wallet.Gems);
            rig.NextDay();
            Assert.IsTrue(rig.Welcome.CanClaim);
        }

        [Test]
        public void WaitsForToday()
        {
            var rig = Create(onboarded: false);
            Assert.IsFalse(rig.Welcome.CanClaim, "given when Today first opens (step A12)");
            Assert.IsFalse(rig.Welcome.TryClaim(out _));
            Assert.AreEqual(0, rig.State.Wallet.Gems);
            rig.State.DailyVisit.OnboardingDone = true;
            Assert.IsTrue(rig.Welcome.CanClaim);
        }

        [Test]
        public void Total_IsSplitInThreeParts_ThatAddUp()
        {
            var config = new DailyVisitConfig();
            Assert.IsTrue(config.TryOverride("gems.welcome_total", 301));
            long sum = 0;
            for (int i = 0; i < 3; i++)
            {
                long part = DailyRewardTables.Welcome(i, Closed(), config).Gems;
                Assert.That(part, Is.InRange(100L, 101L));
                sum += part;
            }
            Assert.AreEqual(301, sum);
            Assert.IsTrue(DailyRewardTables.Welcome(3, Closed(), config).IsEmpty);
            Assert.IsTrue(DailyRewardTables.Welcome(-1, Closed(), config).IsEmpty);
            Assert.AreEqual(100, DailyRewardTables.Welcome(0, Closed(), new DailyVisitConfig()).Gems);
        }

        [Test]
        public void Claims_UseTheWelcomeReason_AndRaiseClaimed()
        {
            var rig = Create();
            var reasons = new List<EconomyReason>();
            var indices = new List<int>();
            rig.Economy.Changed += change => reasons.Add(change.Reason);
            rig.Welcome.Claimed += (index, granted) => indices.Add(index);
            for (int day = 0; day < 4; day++)
            {
                rig.Welcome.TryClaim(out _);
                rig.NextDay();
            }
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, indices);
            Assert.IsNotEmpty(reasons);
            foreach (var reason in reasons)
                Assert.AreEqual(EconomyReason.WelcomeGift, reason);
        }

        // ---------------- Return gift tables (sections 6.2 and 6.3) ----------------

        [Test]
        public void ReturnGift_3To13Days_IsTheDocs()
        {
            var open = DailyRewardTables.Return(false, false, Open());
            Assert.AreEqual(120.0, open.LightMinutes);
            Assert.AreEqual(3, open.Tickets);
            Assert.AreEqual(1, open.DiscoveryEggs);
            Assert.AreEqual(3, open.Treats);
            Assert.AreEqual(CloverCrunch, open.Treat);
            Assert.AreEqual(0, open.Stickers);
            Assert.AreEqual(0, open.Gems);
            Assert.IsFalse(open.Substituted);

            var closed = DailyRewardTables.Return(false, false, Closed());
            Assert.AreEqual(165.0, closed.LightMinutes, "3 tickets become Light 45 min before the Play button");
            Assert.AreEqual(0, closed.Tickets);
            Assert.IsTrue(closed.Substituted);
            Assert.AreEqual(closed.Size, open.Size);
        }

        [Test]
        public void ReturnGift_14DaysOrMore_IsTheDocs()
        {
            var open = DailyRewardTables.Return(true, false, Open());
            Assert.AreEqual(240.0, open.LightMinutes);
            Assert.AreEqual(5, open.Tickets);
            Assert.AreEqual(1, open.DiscoveryEggs);
            Assert.AreEqual(5, open.Treats);
            Assert.AreEqual(1, open.Stickers, "a missing sticker of the player's choice");
            Assert.AreEqual(GiftSize.Big, open.Size);

            var closed = DailyRewardTables.Return(true, false, Closed());
            Assert.AreEqual(240.0 + 75.0, closed.LightMinutes, "5 tickets become Light 75 min");
            Assert.AreEqual(8, closed.Treats, "the sticker becomes 3 treats before the albums");
            Assert.AreEqual(0, closed.Stickers);
            Assert.AreEqual(GiftSize.Big, closed.Size);
        }

        [Test]
        public void ReturnGift_WarmReturn_AddsADiscoveryEgg()
        {
            Assert.AreEqual(2, DailyRewardTables.Return(false, true, Closed()).DiscoveryEggs);
            Assert.AreEqual(2, DailyRewardTables.Return(true, true, Closed()).DiscoveryEggs);
        }
    }
}
