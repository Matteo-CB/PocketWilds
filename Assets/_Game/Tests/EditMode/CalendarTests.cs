using System;
using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T36, doc « 01 — Contenu des rendez-vous, des activités et de l'accueil », section 4: the 28 boxes of section 4.2
    /// (with every system open and with the substitutions), 100 Gems a cycle, Discovery eggs at boxes 16 and 27, one stamp a
    /// login day, absences of 1, 5 or 40 days resume at the next box, cycle 2 after box 28, the cycle decorations (dusk from
    /// cycle 14, an egg from cycle 27), PeekBox and DaysToNextBig.
    /// </summary>
    public class CalendarTests
    {
        private static readonly ContentId CloverCrunch = ContentId.FromSource("treat.clover_crunch");

        private sealed class Rig
        {
            public PlayerState State;
            public FakeGameClock Clock;
            public EconomyService Economy;
            public HatchService Hatch;
            public DailyVisitConfig Config;
            public DailyContext Ctx;
            public CalendarService Calendar;

            /// <summary>Moves to a later day, as the facade does at a day boundary.</summary>
            public void NextDay(int days = 1)
            {
                Clock.Advance(TimeSpan.FromDays(days));
                Ctx.TodayKey = DailyDay.DayKey(DailyDay.Now(State, Clock), Ctx.Zone, Config.ResetHour);
            }

            public CalendarClaim Stamp()
            {
                Assert.IsTrue(Calendar.TryClaim(out var claim), "today's box stamps");
                return claim;
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
            rig.Hatch = new HatchService(rig.State, content, rig.Economy, rig.Clock, null);
            rig.Ctx = new DailyContext(rig.State, content, rig.Config, new ProgressionConfig { TimeZone = TimeZoneInfo.Utc }, rig.Clock,
                rig.Economy, rig.Hatch, null, null, null);
            rig.Calendar = new CalendarService(rig.Ctx);
            return rig;
        }

        private static RewardContext Closed() => new RewardContext(false, false, false, false, CloverCrunch, 15.0);

        private static RewardContext Open() => new RewardContext(true, true, true, true, CloverCrunch, 15.0);

        /// <summary>A short text of a bundle, for table comparisons (test code only).</summary>
        private static string Describe(in RewardBundle b)
        {
            var parts = new List<string>();
            if (b.Gems > 0) parts.Add("G" + b.Gems);
            if (b.LightMinutes > 0.0) parts.Add("L" + b.LightMinutes);
            if (b.Tickets > 0) parts.Add("Tix" + b.Tickets);
            if (b.Treats > 0) parts.Add("Trt" + b.Treats);
            if (b.FlowerSugar > 0) parts.Add("Sugar" + b.FlowerSugar);
            if (b.Seeds > 0) parts.Add("Seed" + b.Seeds);
            if (b.Stickers > 0) parts.Add("Stk" + b.Stickers);
            if (b.DiscoveryEggs > 0) parts.Add("Egg" + b.DiscoveryEggs);
            if (b.Decoration != ContentId.None) parts.Add("Decor");
            return string.Join("+", parts);
        }

        // ---------------- The 28 boxes (section 4.2) ----------------

        [Test]
        public void Boxes_AreTheDocs_WithEverySystemOpen()
        {
            string[] expected =
            {
                "L30", "Trt3+Sugar1", "Tix2", "Seed4", "L45", "Stk1", "G20",
                "L30", "Trt3+Sugar1", "Tix2", "L60", "Seed4", "Stk1", "G20",
                "L45", "Egg1", "Tix2", "Trt5", "Seed6", "L60", "G20",
                "L45", "Tix3", "Stk1", "Trt5", "L90", "Egg1", "G40+Decor",
            };
            var open = Open();
            var config = new DailyVisitConfig();
            Assert.AreEqual(28, DailyRewardTables.CalendarBoxCount);
            for (int box = 0; box < 28; box++)
            {
                var bundle = DailyRewardTables.CalendarBox(box, 1, open, config);
                Assert.AreEqual(expected[box], Describe(bundle), "box " + (box + 1));
                Assert.IsFalse(bundle.Substituted, "box " + (box + 1));
                if (bundle.Treats > 0)
                    Assert.AreEqual(CloverCrunch, bundle.Treat, "treats of the most recent biome");
                if (bundle.Seeds > 0)
                    Assert.AreEqual(DailyRewardTables.SweetClover, bundle.SeedPlant);
            }
        }

        [Test]
        public void Boxes_AreReplaced_WhileTheirSystemIsClosed()
        {
            string[] expected =
            {
                "L30", "Trt3", "L30", "L30", "L45", "Trt3", "G20",
                "L30", "Trt3", "L30", "L60", "L30", "Trt3", "G20",
                "L45", "Egg1", "L30", "Trt5", "L45", "L60", "G20",
                "L45", "L45", "Trt3", "Trt5", "L90", "Egg1", "G40+Decor",
            };
            int[] replaced = { 3, 4, 6, 10, 12, 13, 17, 19, 23, 24 };
            var closed = Closed();
            var config = new DailyVisitConfig();
            for (int box = 0; box < 28; box++)
            {
                var bundle = DailyRewardTables.CalendarBox(box, 1, closed, config);
                Assert.AreEqual(expected[box], Describe(bundle), "box " + (box + 1));
                Assert.AreEqual(Array.IndexOf(replaced, box + 1) >= 0, bundle.Substituted, "box " + (box + 1));
                Assert.IsFalse(bundle.IsEmpty, "never an empty box");
            }
        }

        [Test]
        public void EveryCycle_Gives100Gems_AndEggsAtBoxes16And27()
        {
            var config = new DailyVisitConfig();
            foreach (int cycle in new[] { 1, 13, 14, 26 })
            {
                long gems = 0;
                int eggs = 0;
                for (int box = 0; box < 28; box++)
                {
                    var bundle = DailyRewardTables.CalendarBox(box, cycle, Closed(), config);
                    gems += bundle.Gems;
                    eggs += bundle.DiscoveryEggs;
                    Assert.AreEqual(box == 6 || box == 13 || box == 20 || box == 27, bundle.Gems > 0, "Gems only at 7, 14, 21, 28");
                    Assert.AreEqual(box == 15 || box == 26, bundle.DiscoveryEggs > 0, "eggs at 16 and 27 (cycle " + cycle + ")");
                }
                Assert.AreEqual(100, gems, "cycle " + cycle);
                Assert.AreEqual(2, eggs, "cycle " + cycle);
            }
        }

        [Test]
        public void BigBoxes_Are7_14_21_28()
        {
            for (int box = 0; box < 28; box++)
                Assert.AreEqual(box == 6 || box == 13 || box == 20 || box == 27, DailyRewardTables.IsBigBox(box), "box " + (box + 1));
            Assert.IsFalse(DailyRewardTables.IsBigBox(-1));
            Assert.IsFalse(DailyRewardTables.IsBigBox(34));
        }

        [Test]
        public void CalendarGems_FollowRemoteConfig()
        {
            var config = new DailyVisitConfig();
            Assert.IsTrue(config.TryOverride("gems.calendar_day7", 25));
            Assert.IsTrue(config.TryOverride("gems.calendar_day28", 50));
            Assert.AreEqual(25, DailyRewardTables.CalendarBox(6, 1, Closed(), config).Gems);
            Assert.AreEqual(20, DailyRewardTables.CalendarBox(13, 1, Closed(), config).Gems);
            Assert.AreEqual(50, DailyRewardTables.CalendarBox(27, 1, Closed(), config).Gems);
        }

        // ---------------- Cycle decorations (section 4.3) ----------------

        [Test]
        public void CycleDecorations_InOrder_ThenDusk_ThenAnEgg()
        {
            string[] names =
            {
                "moss_armchair", "pebble_fountain", "branch_swing", "round_garden_lantern", "woven_linen_hammock", "giant_watering_can",
                "birdhouse", "log_bridge", "stump_shelf", "daisy_carousel", "cloud_mobile", "sunflower_clock", "round_greenhouse",
            };
            var seen = new HashSet<ContentId>();
            for (int i = 0; i < 13; i++)
            {
                Assert.AreEqual(ContentId.FromSource("decor." + names[i]), DailyRewardTables.CycleDecoration(i + 1), "cycle " + (i + 1));
                Assert.AreEqual(ContentId.FromSource("decor." + names[i] + "_dusk"), DailyRewardTables.CycleDecoration(i + 14),
                    "cycle " + (i + 14));
                Assert.AreEqual("decor." + names[i], DailyRewardTables.CycleDecorationSource(i + 1));
                Assert.IsTrue(seen.Add(DailyRewardTables.CycleDecoration(i + 1)));
                Assert.IsTrue(seen.Add(DailyRewardTables.CycleDecoration(i + 14)));
            }
            Assert.AreEqual(ContentId.None, DailyRewardTables.CycleDecoration(0));
            Assert.AreEqual(ContentId.None, DailyRewardTables.CycleDecoration(27));
            Assert.AreEqual(ContentId.None, DailyRewardTables.CycleDecoration(100));
            Assert.IsNull(DailyRewardTables.CycleDecorationSource(27));

            var config = new DailyVisitConfig();
            var cycle26 = DailyRewardTables.CalendarBox(27, 26, Closed(), config);
            Assert.AreEqual(ContentId.FromSource("decor.round_greenhouse_dusk"), cycle26.Decoration);
            Assert.AreEqual(0, cycle26.DiscoveryEggs);
            foreach (int cycle in new[] { 27, 28, 60 })
            {
                var last = DailyRewardTables.CalendarBox(27, cycle, Closed(), config);
                Assert.AreEqual(40, last.Gems, "cycle " + cycle);
                Assert.AreEqual(1, last.DiscoveryEggs, "cycle " + cycle + ": an egg instead of the decoration");
                Assert.AreEqual(ContentId.None, last.Decoration);
            }
        }

        // ---------------- The service ----------------

        [Test]
        public void FullCycle_StampsEachBoxOnce_ThenStartsCycle2()
        {
            var rig = Create();
            int eggs = 0;
            var source = DiscoverySource.Debug;
            rig.Hatch.DiscoveryEggsGranted += (count, from) =>
            {
                eggs += count;
                source = from;
            };
            long gemsBefore = rig.State.Wallet.Gems;
            for (int day = 0; day < 28; day++)
            {
                var claim = rig.Stamp();
                Assert.AreEqual(day, claim.Box);
                Assert.AreEqual(day + 1, claim.Number);
                Assert.AreEqual(1, claim.Cycle);
                Assert.AreEqual(DailyRewardTables.IsBigBox(day), claim.Big);
                rig.NextDay();
            }
            Assert.AreEqual(gemsBefore + 100, rig.State.Wallet.Gems, "100 Gems a cycle");
            Assert.AreEqual(2, eggs);
            Assert.AreEqual(DiscoverySource.Calendar, source);
            Assert.AreEqual(1, rig.State.Inventory.Count(ContentId.FromSource("decor.moss_armchair")), "cycle 1's decoration");
            Assert.AreEqual(0, rig.Calendar.NextBox);
            Assert.AreEqual(2, rig.Calendar.Cycle);
            Assert.AreEqual(28, rig.Calendar.Stamps);

            var day29 = rig.Stamp();
            Assert.AreEqual(0, day29.Box, "cycle 2 starts at box 1");
            Assert.AreEqual(2, day29.Cycle);
            Assert.AreEqual(30.0, day29.Granted.Bundle.LightMinutes);
        }

        [Test]
        public void Box28_GivesTheCycleDecoration_OrAnEggFromCycle27()
        {
            var rig = Create();
            rig.State.DailyVisit.CalendarCycle = 14;
            rig.State.DailyVisit.CalendarNext = 27;
            var dusk = rig.Stamp();
            Assert.AreEqual(ContentId.FromSource("decor.moss_armchair_dusk"), dusk.Decoration);
            Assert.AreEqual(1, rig.State.Inventory.Count(dusk.Decoration));
            Assert.AreEqual(15, rig.Calendar.Cycle);

            rig.NextDay();
            rig.State.DailyVisit.CalendarCycle = 27;
            rig.State.DailyVisit.CalendarNext = 27;
            int eggs = rig.State.World.DiscoveryEggs;
            long gems = rig.State.Wallet.Gems;
            var late = rig.Stamp();
            Assert.AreEqual(ContentId.None, late.Decoration);
            Assert.AreEqual(eggs + 1, rig.State.World.DiscoveryEggs);
            Assert.AreEqual(gems + 40, rig.State.Wallet.Gems);
            Assert.AreEqual(28, rig.Calendar.Cycle);
        }

        [Test]
        public void OneStampPerLoginDay()
        {
            var rig = Create();
            rig.Stamp();
            Assert.IsTrue(rig.Calendar.StampedToday);
            Assert.IsFalse(rig.Calendar.CanClaim);
            Assert.IsFalse(rig.Calendar.TryClaim(out _), "a second stamp the same day");
            Assert.AreEqual(1, rig.Calendar.NextBox);

            rig.NextDay();
            Assert.IsFalse(rig.Calendar.StampedToday);
            Assert.IsTrue(rig.Calendar.CanClaim);
            Assert.AreEqual(1, rig.Stamp().Box);
            Assert.IsFalse(rig.Calendar.TryClaim(out _));
        }

        [TestCase(1)]
        [TestCase(5)]
        [TestCase(40)]
        public void Absence_ResumesAtTheNextBox(int daysAway)
        {
            var rig = Create();
            for (int i = 0; i < 9; i++)
            {
                rig.Stamp();
                rig.NextDay();
            }
            Assert.AreEqual(9, rig.Calendar.NextBox);

            // Away: the missed days give nothing and take nothing.
            rig.NextDay(daysAway);
            Assert.AreEqual(9, rig.Calendar.NextBox, "nothing reset while away");
            Assert.AreEqual(1, rig.Calendar.Cycle);
            var claim = rig.Stamp();
            Assert.AreEqual(9, claim.Box, "box 10 after " + daysAway + " days away");
            Assert.AreEqual(1, claim.Cycle);
            Assert.AreEqual(10, rig.Calendar.Stamps);
        }

        [Test]
        public void Stamps_ReplaceClosedSystems_InTheService()
        {
            var rig = Create();
            rig.State.DailyVisit.CalendarNext = 2;
            var closed = rig.Stamp();
            Assert.IsTrue(closed.Substituted, "box 3 before the Play button");
            Assert.AreEqual(30.0, closed.Granted.Bundle.LightMinutes);
            Assert.AreEqual(0, rig.State.Inventory.Tickets);

            var open = Create(new DailyVisitConfig { ActivitiesAvailable = true });
            open.State.DailyVisit.CalendarNext = 2;
            var tickets = open.Stamp();
            Assert.IsFalse(tickets.Substituted);
            Assert.AreEqual(2, open.State.Inventory.Tickets);
        }

        [Test]
        public void Stamps_UseTheCalendarReason()
        {
            var rig = Create();
            var reasons = new List<EconomyReason>();
            rig.Economy.Changed += change => reasons.Add(change.Reason);
            for (int day = 0; day < 28; day++)
            {
                rig.Stamp();
                rig.NextDay();
            }
            Assert.IsNotEmpty(reasons);
            foreach (var reason in reasons)
                Assert.AreEqual(EconomyReason.Calendar, reason);
        }

        [Test]
        public void Calendar_WaitsForToday_AndItsFlag()
        {
            var rig = Create(onboarded: false);
            Assert.IsFalse(rig.Calendar.CanClaim, "the welcome is not over");
            Assert.IsFalse(rig.Calendar.TryClaim(out _));
            rig.State.DailyVisit.OnboardingDone = true;
            Assert.IsTrue(rig.Calendar.CanClaim);
            Assert.IsTrue(rig.Config.TryOverrideFlag("flags.calendar", false));
            Assert.IsFalse(rig.Calendar.CanClaim);
            Assert.AreEqual(0, rig.Calendar.NextBox);
        }

        [Test]
        public void Claimed_IsRaised_WithTheClaim()
        {
            var rig = Create();
            int raised = 0;
            CalendarClaim seen = default;
            rig.Calendar.Claimed += claim =>
            {
                raised++;
                seen = claim;
            };
            var stamp = rig.Stamp();
            rig.Calendar.TryClaim(out _);
            Assert.AreEqual(1, raised);
            Assert.AreEqual(stamp.Box, seen.Box);
            Assert.AreEqual(rig.Ctx.TodayKey, seen.DayKey);
        }

        // ---------------- Preview ----------------

        [Test]
        public void PeekBox_LooksAhead_AcrossCycles()
        {
            var rig = Create();
            rig.State.DailyVisit.CalendarNext = 26;
            Assert.AreEqual(1, rig.Calendar.PeekBox(0).DiscoveryEggs, "box 27, the next to stamp");
            var box28 = rig.Calendar.PeekBox(1);
            Assert.AreEqual(40, box28.Gems);
            Assert.AreEqual(ContentId.FromSource("decor.moss_armchair"), box28.Decoration);
            Assert.AreEqual(30.0, rig.Calendar.PeekBox(2).LightMinutes, "box 1 of cycle 2");
            Assert.AreEqual(ContentId.FromSource("decor.pebble_fountain"), rig.Calendar.PeekBox(29).Decoration, "box 28 of cycle 2");
            rig.Calendar.Locate(2, out int box, out int cycle);
            Assert.AreEqual(0, box);
            Assert.AreEqual(2, cycle);

            // « Demain : {reward} » after today's stamp is PeekBox(0).
            rig.Stamp();
            Assert.AreEqual(ContentId.FromSource("decor.moss_armchair"), rig.Calendar.PeekBox(0).Decoration);
        }

        [Test]
        public void DaysToNextBig_CountsTheStampsToBoxes7_14_21_28()
        {
            var rig = Create();
            int[] expected = { 7, 6, 5, 4, 3, 2, 1, 7, 6, 5, 4, 3, 2, 1, 7, 6, 5, 4, 3, 2, 1, 7, 6, 5, 4, 3, 2, 1 };
            for (int box = 0; box < 28; box++)
            {
                rig.State.DailyVisit.CalendarNext = (byte)box;
                Assert.AreEqual(expected[box], rig.Calendar.DaysToNextBig, "next box " + (box + 1));
                Assert.IsTrue(DailyRewardTables.IsBigBox(box + expected[box] - 1), "next box " + (box + 1));
            }
        }
    }
}
