using System;
using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T36, doc « 01 — Contenu des rendez-vous, des activités et de l'accueil », section 3: the first gift is the fixed
    /// Discovery egg and counts as big; the table's weights and sizes; a million draws follow the table; the Gem mean
    /// (2.83 without the guarantee, 3.00 with it); never more than 14 gifts without a big lot; the guaranteed draw among
    /// G10, G15 and EGG at 6:4:2; substitutions keep the size; same day, same lot; one gift a day; an absence keeps the
    /// counter.
    /// </summary>
    public class DailyGiftTests
    {
        private static readonly ContentId CloverCrunch = ContentId.FromSource("treat.clover_crunch");
        private static readonly Guid Profile = new Guid("2f8c6b1e-5a4d-4c3b-9e7f-1a2b3c4d5e6f");

        private sealed class Rig
        {
            public PlayerState State;
            public FakeGameClock Clock;
            public EconomyService Economy;
            public HatchService Hatch;
            public DailyVisitConfig Config;
            public DailyContext Ctx;
            public DailyGiftService Gifts;

            /// <summary>Moves to a later day, as the facade does at a day boundary.</summary>
            public void NextDay(int days = 1)
            {
                Clock.Advance(TimeSpan.FromDays(days));
                Ctx.TodayKey = DailyDay.DayKey(DailyDay.Now(State, Clock), Ctx.Zone, Config.ResetHour);
            }

            public GiftOpening Open()
            {
                Assert.IsTrue(Gifts.TryOpen(out var opening), "today's gift opens");
                return opening;
            }
        }

        private static Rig Create(DailyVisitConfig config = null, Guid? profile = null, bool onboarded = true)
        {
            var content = TestUtil.ThreeSpecies();
            var rig = new Rig
            {
                State = PlayerState.CreateNew(TestUtil.T0),
                Clock = new FakeGameClock(TestUtil.T0),
                Config = config ?? new DailyVisitConfig(),
            };
            if (profile.HasValue)
                rig.State.Meta.ProfileId = profile.Value;
            rig.State.DailyVisit.OnboardingDone = onboarded;
            rig.Economy = new EconomyService(rig.State, content, EconomyConfig.Default, null);
            rig.Hatch = new HatchService(rig.State, content, rig.Economy, rig.Clock, null);
            rig.Ctx = new DailyContext(rig.State, content, rig.Config, new ProgressionConfig { TimeZone = TimeZoneInfo.Utc }, rig.Clock,
                rig.Economy, rig.Hatch, null, null, null);
            rig.Gifts = new DailyGiftService(rig.Ctx);
            return rig;
        }

        private static RewardContext Closed() => new RewardContext(false, false, false, false, CloverCrunch, 15.0);

        private static RewardContext Open() => new RewardContext(true, true, true, true, CloverCrunch, 15.0);

        // ---------------- First gift (sections 3.1 and 3.3) ----------------

        [Test]
        public void FirstGift_IsTheFixedDiscoveryEgg_AndCountsAsBig()
        {
            var rig = Create();
            rig.State.DailyVisit.GiftsSinceBig = 9;
            int eggs = 0;
            var source = DiscoverySource.Debug;
            rig.Hatch.DiscoveryEggsGranted += (count, from) =>
            {
                eggs += count;
                source = from;
            };

            var opening = rig.Open();

            Assert.AreEqual(GiftLot.Egg, opening.Lot);
            Assert.IsTrue(opening.FirstFixed);
            Assert.IsFalse(opening.Forced, "fixed, not drawn");
            Assert.AreEqual(GiftSize.Big, opening.Size);
            Assert.AreEqual(1, opening.Granted.Bundle.DiscoveryEggs);
            Assert.AreEqual(1, eggs);
            Assert.AreEqual(DiscoverySource.DailyGift, source);
            Assert.AreEqual(1, rig.State.World.DiscoveryEggs);
            Assert.AreEqual(0, rig.State.DailyVisit.GiftsSinceBig, "the fixed egg counts as a big lot");
            Assert.AreEqual(1, rig.State.DailyVisit.GiftsOpened);
            Assert.AreEqual(GiftLot.Egg, rig.Gifts.LastLot);
            Assert.AreEqual(2, opening.Flags, "analytics flag: first fixed");
        }

        [Test]
        public void FirstGift_IsTheEgg_ForEveryProfile()
        {
            for (int i = 0; i < 20; i++)
            {
                var rig = Create(profile: new Guid(i + 1, 7, 7, 1, 2, 3, 4, 5, 6, 7, 8));
                Assert.AreEqual(GiftLot.Egg, rig.Open().Lot);
            }
        }

        // ---------------- Table (section 3.2) ----------------

        [Test]
        public void Table_WeightsSizesAndGems_AreTheDocs()
        {
            int[] weights = { 21, 19, 6, 4, 5, 13, 6, 9, 7, 5, 3, 2 };
            GiftSize[] sizes =
            {
                GiftSize.Small, GiftSize.Nice, GiftSize.Big, GiftSize.Big, GiftSize.Nice, GiftSize.Small, GiftSize.Nice, GiftSize.Small,
                GiftSize.Small, GiftSize.Small, GiftSize.Nice, GiftSize.Big,
            };
            long[] gems = { 3, 5, 10, 15, 1, 0, 0, 0, 0, 0, 0, 0 };
            Assert.AreEqual(12, DailyRewardTables.GiftLotCount);
            int sum = 0;
            long weightedGems = 0;
            for (int i = 0; i < DailyRewardTables.GiftLotCount; i++)
            {
                var lot = (GiftLot)i;
                Assert.AreEqual(weights[i], DailyRewardTables.Weight(lot), lot.ToString());
                Assert.AreEqual(sizes[i], DailyRewardTables.SizeOf(lot), lot.ToString());
                Assert.AreEqual(gems[i], DailyRewardTables.GiftGems(lot), lot.ToString());
                Assert.AreEqual(gems[i], DailyRewardTables.Gift(lot, Closed()).Gems, lot.ToString());
                sum += DailyRewardTables.Weight(lot);
                weightedGems += DailyRewardTables.Weight(lot) * gems[i];
            }
            Assert.AreEqual(100, sum, "the weights are percentages");
            Assert.AreEqual(100, DailyRewardTables.GiftWeightTotal);
            Assert.AreEqual(283, weightedGems, "section 3.4: 2.83 Gems a gift from the table alone");

            Assert.AreEqual(12, DailyRewardTables.ForcedWeightTotal);
            Assert.AreEqual(6, DailyRewardTables.ForcedWeight(GiftLot.G10));
            Assert.AreEqual(4, DailyRewardTables.ForcedWeight(GiftLot.G15));
            Assert.AreEqual(2, DailyRewardTables.ForcedWeight(GiftLot.Egg));
            Assert.AreEqual(0, DailyRewardTables.ForcedWeight(GiftLot.G5));

            // Stable values (save, analytics).
            Assert.AreEqual(0, (int)GiftLot.G3);
            Assert.AreEqual(4, (int)GiftLot.Mix);
            Assert.AreEqual(9, (int)GiftLot.Seed);
            Assert.AreEqual(11, (int)GiftLot.Egg);
        }

        [Test]
        public void Table_LotContents_AreTheDocs()
        {
            var open = Open();
            Assert.AreEqual(15.0, DailyRewardTables.Gift(GiftLot.Mix, open).LightMinutes);
            Assert.AreEqual(1, DailyRewardTables.Gift(GiftLot.Mix, open).Tickets);
            Assert.AreEqual(30.0, DailyRewardTables.Gift(GiftLot.L30, open).LightMinutes);
            Assert.AreEqual(60.0, DailyRewardTables.Gift(GiftLot.L60, open).LightMinutes);
            Assert.AreEqual(2, DailyRewardTables.Gift(GiftLot.Tix, open).Tickets);
            var treats = DailyRewardTables.Gift(GiftLot.Trt, open);
            Assert.AreEqual(3, treats.Treats);
            Assert.AreEqual(CloverCrunch, treats.Treat, "treats of the most recent biome");
            var seeds = DailyRewardTables.Gift(GiftLot.Seed, open);
            Assert.AreEqual(4, seeds.Seeds);
            Assert.AreEqual(ContentId.FromSource("plant.sweet_clover"), seeds.SeedPlant);
            Assert.AreEqual(1, DailyRewardTables.Gift(GiftLot.Stk, open).Stickers);
            Assert.AreEqual(1, DailyRewardTables.Gift(GiftLot.Egg, open).DiscoveryEggs);
            for (int i = 0; i < DailyRewardTables.GiftLotCount; i++)
                Assert.IsFalse(DailyRewardTables.Gift((GiftLot)i, open).IsEmpty, "never an empty gift");
        }

        [Test]
        public void MillionDraws_FollowTheTable()
        {
            const int draws = 1_000_000;
            var counts = new int[DailyRewardTables.GiftLotCount];
            var rng = new DeterministicRandom(0xC0FFEEu);
            for (int i = 0; i < draws; i++)
                counts[(int)DailyGiftService.Roll(ref rng, false)]++;
            for (int i = 0; i < counts.Length; i++)
            {
                var lot = (GiftLot)i;
                Assert.That(counts[i] / (double)draws, Is.EqualTo(DailyRewardTables.Weight(lot) / 100.0).Within(0.002), lot.ToString());
            }
        }

        // ---------------- Gem mean (section 3.4) ----------------

        [Test]
        public void GemMean_WithoutTheGuarantee_Is2_83()
        {
            const int draws = 1_000_000;
            var rewards = Closed();
            var rng = new DeterministicRandom(17u);
            long gems = 0;
            for (int i = 0; i < draws; i++)
                gems += DailyRewardTables.Gift(DailyGiftService.Roll(ref rng, false), rewards).Gems;
            Assert.That(gems / (double)draws, Is.EqualTo(2.83).Within(0.02));
        }

        [Test]
        public void GemMean_WithTheGuarantee_Is3_00_AndNoRunPasses14()
        {
            const int draws = 1_000_000;
            var rewards = Closed();
            var rng = new DeterministicRandom(29u);
            long gems = 0;
            int since = 0;
            int longest = 0;
            for (int i = 0; i < draws; i++)
            {
                var lot = DailyGiftService.Roll(ref rng, DailyRewardTables.MustForceBig(since, 15));
                gems += DailyRewardTables.Gift(lot, rewards).Gems;
                if (DailyRewardTables.IsBig(lot))
                {
                    since = 0;
                }
                else
                {
                    since++;
                    longest = Math.Max(longest, since);
                }
            }
            Assert.That(gems / (double)draws, Is.EqualTo(3.00).Within(0.03), "section 3.4: 21.31 Gems a cycle of 7.11 gifts");
            Assert.AreEqual(14, longest, "14 gifts without a big lot at most, then the guarantee");
        }

        // ---------------- Guarantee (section 3.3) ----------------

        [Test]
        public void ForcedDraw_IsOnlyG10G15Egg_At6To4To2()
        {
            const int draws = 120_000;
            var counts = new int[DailyRewardTables.GiftLotCount];
            var rng = new DeterministicRandom(5u);
            for (int i = 0; i < draws; i++)
                counts[(int)DailyGiftService.Roll(ref rng, true)]++;
            for (int i = 0; i < counts.Length; i++)
            {
                var lot = (GiftLot)i;
                if (lot != GiftLot.G10 && lot != GiftLot.G15 && lot != GiftLot.Egg)
                    Assert.AreEqual(0, counts[i], lot + " is never guaranteed");
            }
            Assert.That(counts[(int)GiftLot.G10] / (double)draws, Is.EqualTo(6.0 / 12.0).Within(0.01));
            Assert.That(counts[(int)GiftLot.G15] / (double)draws, Is.EqualTo(4.0 / 12.0).Within(0.01));
            Assert.That(counts[(int)GiftLot.Egg] / (double)draws, Is.EqualTo(2.0 / 12.0).Within(0.01));
        }

        [TestCase(15, 14)]
        [TestCase(5, 4)]
        public void Service_NeverGivesMoreThanTheGuaranteeAllows(int bigEvery, int longestAllowed)
        {
            var config = new DailyVisitConfig();
            Assert.IsTrue(config.TryOverride("daily.gift_big_every", bigEvery));
            var rig = Create(config, Profile);
            int run = 0;
            int longest = 0;
            int forced = 0;
            for (int day = 0; day < 600; day++)
            {
                var opening = rig.Open();
                if (opening.Size == GiftSize.Big)
                    run = 0;
                else
                    run++;
                longest = Math.Max(longest, run);
                if (opening.Forced)
                {
                    forced++;
                    Assert.AreEqual(longestAllowed, opening.SinceBigBefore, "forced exactly after the allowed run");
                    Assert.AreEqual(GiftSize.Big, opening.Size);
                }
                Assert.AreEqual(run, rig.State.DailyVisit.GiftsSinceBig);
                rig.NextDay();
            }
            Assert.LessOrEqual(longest, longestAllowed);
            Assert.Greater(forced, 0, "the guarantee was needed at least once in 600 days");
        }

        // ---------------- Substitutions (section 3.2) ----------------

        [Test]
        public void Substitutions_BeforePlayGardenAndAlbums_KeepTheSize()
        {
            var closed = Closed();
            var open = Open();
            for (int i = 0; i < DailyRewardTables.GiftLotCount; i++)
            {
                var lot = (GiftLot)i;
                var before = DailyRewardTables.Gift(lot, closed);
                var after = DailyRewardTables.Gift(lot, open);
                Assert.AreEqual(DailyRewardTables.SizeOf(lot), before.Size, lot + ": the substitution keeps the size");
                Assert.AreEqual(DailyRewardTables.SizeOf(lot), after.Size, lot.ToString());
                Assert.AreEqual(after.Gems, before.Gems, lot + ": Gems are never replaced");
                Assert.AreEqual(after.DiscoveryEggs, before.DiscoveryEggs, lot.ToString());
                Assert.IsFalse(after.Substituted, lot + ": nothing replaced once every system is open");
                bool replaced = lot == GiftLot.Mix || lot == GiftLot.Tix || lot == GiftLot.Seed || lot == GiftLot.Stk;
                Assert.AreEqual(replaced, before.Substituted, lot.ToString());
                if (!replaced)
                {
                    Assert.AreEqual(after.LightMinutes, before.LightMinutes, lot.ToString());
                    Assert.AreEqual(after.Treats, before.Treats, lot.ToString());
                }
            }

            var mix = DailyRewardTables.Gift(GiftLot.Mix, closed);
            Assert.AreEqual(30.0, mix.LightMinutes, "MIX: the ticket becomes Light 15 min");
            Assert.AreEqual(0, mix.Tickets);
            Assert.AreEqual(1, mix.Gems);
            var tix = DailyRewardTables.Gift(GiftLot.Tix, closed);
            Assert.AreEqual(30.0, tix.LightMinutes, "TIX: Light 30 min before the Play button");
            Assert.AreEqual(0, tix.Tickets);
            var seed = DailyRewardTables.Gift(GiftLot.Seed, closed);
            Assert.AreEqual(30.0, seed.LightMinutes, "SEED: Light 30 min before the Garden");
            Assert.AreEqual(0, seed.Seeds);
            var stk = DailyRewardTables.Gift(GiftLot.Stk, closed);
            Assert.AreEqual(3, stk.Treats, "STK: 3 treats before the albums");
            Assert.AreEqual(CloverCrunch, stk.Treat);
            Assert.AreEqual(0, stk.Stickers);

            // Each system on its own: the Play button open, the Garden and the albums still closed.
            var playOnly = new RewardContext(true, false, false, false, CloverCrunch, 15.0);
            Assert.IsFalse(DailyRewardTables.Gift(GiftLot.Mix, playOnly).Substituted);
            Assert.IsFalse(DailyRewardTables.Gift(GiftLot.Tix, playOnly).Substituted);
            Assert.IsTrue(DailyRewardTables.Gift(GiftLot.Seed, playOnly).Substituted);
            Assert.IsTrue(DailyRewardTables.Gift(GiftLot.Stk, playOnly).Substituted);
        }

        [Test]
        public void Service_AppliesTheSubstitutions_OfItsBuild()
        {
            var closed = Create(profile: Profile);
            var openConfig = new DailyVisitConfig { ActivitiesAvailable = true, GardenAvailable = true, AlbumsAvailable = true };
            var open = Create(openConfig, Profile);
            int tickets = 0;
            int seeds = 0;
            int stickers = 0;
            int replaced = 0;
            for (int day = 0; day < 250; day++)
            {
                var a = closed.Open();
                var b = open.Open();
                Assert.AreEqual(a.Lot, b.Lot, "same profile, same day, same lot");
                Assert.AreEqual(a.Size, b.Size);
                Assert.IsFalse(b.Substituted);
                if (a.Substituted)
                    replaced++;
                tickets += b.Granted.Bundle.Tickets;
                seeds += b.Granted.Bundle.Seeds;
                stickers += b.Granted.Bundle.Stickers;
                closed.NextDay();
                open.NextDay();
            }
            Assert.Greater(replaced, 0);
            Assert.AreEqual(0, closed.State.Inventory.Tickets, "no ticket before the Play button");
            Assert.AreEqual(0, closed.State.Inventory.Count(DailyRewardTables.SweetClover), "no seed before the Garden");
            Assert.AreEqual(0, closed.State.Inventory.PendingStickers, "no sticker before the albums");
            Assert.Greater(tickets, 0);
            Assert.AreEqual(tickets, open.State.Inventory.Tickets);
            Assert.AreEqual(seeds, open.State.Inventory.Count(DailyRewardTables.SweetClover));
            Assert.AreEqual(stickers, open.State.Inventory.PendingStickers);
        }

        // ---------------- Same day, same lot ----------------

        [Test]
        public void SameDayAndProfile_GiveTheSameLot_SoACrashNeverRerolls()
        {
            var first = Create(profile: Profile);
            var replay = Create(profile: Profile);
            var other = Create(profile: new Guid("8d1e2c3b-4a59-4f6e-8a7b-9c0d1e2f3a4b"));
            first.State.DailyVisit.GiftsOpened = replay.State.DailyVisit.GiftsOpened = other.State.DailyVisit.GiftsOpened = 1;
            int differences = 0;
            for (int day = 0; day < 30; day++)
            {
                var a = first.Open();
                var b = replay.Open();
                var c = other.Open();
                Assert.AreEqual(a.Lot, b.Lot, "the same day gives the same lot (a reopened app finds the same gift)");
                var rng = new DeterministicRandom(first.Ctx.Seed(first.Ctx.TodayKey, DailyGiftService.RollSalt));
                Assert.AreEqual(DailyGiftService.Roll(ref rng, a.Forced), a.Lot, "seeded by the profile and the day");
                if (a.Lot != c.Lot)
                    differences++;
                first.NextDay();
                replay.NextDay();
                other.NextDay();
            }
            Assert.Greater(differences, 0, "another profile draws its own lots");
        }

        // ---------------- One a day, no streak ----------------

        [Test]
        public void OneGiftPerDay()
        {
            var rig = Create();
            rig.Open();
            Assert.IsTrue(rig.Gifts.OpenedToday);
            Assert.IsFalse(rig.Gifts.CanOpen);
            Assert.IsFalse(rig.Gifts.TryOpen(out _), "a second gift the same day");

            rig.Clock.Advance(TimeSpan.FromHours(11));
            rig.Ctx.TodayKey = DailyDay.DayKey(DailyDay.Now(rig.State, rig.Clock), rig.Ctx.Zone, rig.Config.ResetHour);
            Assert.IsFalse(rig.Gifts.CanOpen, "still the same day at 23:00");

            rig.NextDay();
            Assert.IsFalse(rig.Gifts.OpenedToday);
            Assert.IsTrue(rig.Gifts.CanOpen);
            rig.Open();
            Assert.AreEqual(2, rig.State.DailyVisit.GiftsOpened);
            Assert.IsFalse(rig.Gifts.TryOpen(out _));
        }

        [Test]
        public void DayWithoutVisit_GivesNothing_AndKeepsTheCounter()
        {
            var rig = Create();
            rig.State.DailyVisit.GiftsOpened = 20;
            rig.State.DailyVisit.GiftsSinceBig = 14;
            long gems = rig.State.Wallet.Gems;
            int eggs = rig.State.World.DiscoveryEggs;

            rig.NextDay(9);

            Assert.AreEqual(gems, rig.State.Wallet.Gems, "nothing given while away");
            Assert.AreEqual(eggs, rig.State.World.DiscoveryEggs);
            Assert.AreEqual(14, rig.State.DailyVisit.GiftsSinceBig, "an absence never resets the counter");
            var opening = rig.Open();
            Assert.IsTrue(opening.Forced, "the counter survived: the guarantee applies");
            Assert.AreEqual(14, opening.SinceBigBefore);
            Assert.AreEqual(GiftSize.Big, opening.Size);
            Assert.AreEqual(0, rig.State.DailyVisit.GiftsSinceBig);
            Assert.AreEqual(21, rig.State.DailyVisit.GiftsOpened);
        }

        // ---------------- Gates, grants, events ----------------

        [Test]
        public void Gift_WaitsForToday_AndItsFlag()
        {
            var rig = Create(onboarded: false);
            Assert.IsFalse(rig.Gifts.CanOpen, "the welcome is not over");
            Assert.IsFalse(rig.Gifts.TryOpen(out _));
            Assert.AreEqual(0, rig.State.DailyVisit.GiftsOpened);

            rig.State.DailyVisit.OnboardingDone = true;
            Assert.IsTrue(rig.Gifts.CanOpen);
            Assert.IsTrue(rig.Config.TryOverrideFlag("flags.daily_gift", false));
            Assert.IsFalse(rig.Gifts.CanOpen, "flags.daily_gift hides the gift");
            Assert.IsTrue(rig.Config.TryOverrideFlag("flags.daily_gift", true));
            Assert.IsTrue(rig.Gifts.CanOpen, "and erases nothing");
        }

        [Test]
        public void Gift_WaitsForRefugeLevel2_AndTheEndOfTheWelcome()
        {
            var content = TestUtil.ThreeSpecies();
            var clock = new FakeGameClock(TestUtil.T0);
            var state = PlayerState.CreateNew(TestUtil.T0);
            var economy = new EconomyService(state, content, EconomyConfig.Default, null);
            var hatch = new HatchService(state, content, economy, clock, null);
            var config = new ProgressionConfig { TimeZone = TimeZoneInfo.Utc };
            var progression = new ProgressionServices(state, content, economy, hatch, clock, null, config);
            try
            {
                var ctx = new DailyContext(state, content, new DailyVisitConfig(), config, clock, economy, hatch, null, progression, null);
                var gifts = new DailyGiftService(ctx);
                state.DailyVisit.OnboardingDone = true;
                state.Progression.Refuge.Level = 1;
                Assert.IsFalse(gifts.CanOpen, "refuge level 1");
                state.Progression.Refuge.Level = 2;
                state.DailyVisit.OnboardingDone = false;
                Assert.IsFalse(gifts.CanOpen, "refuge level 2, the welcome still running");
                state.DailyVisit.OnboardingDone = true;
                Assert.IsTrue(gifts.CanOpen);
                Assert.IsTrue(gifts.TryOpen(out var opening));
                Assert.AreEqual(GiftLot.Egg, opening.Lot);
            }
            finally
            {
                progression.Dispose();
            }
        }

        [Test]
        public void Grants_GoThroughTheEconomy_WithTheDailyGiftReason()
        {
            var rig = Create(profile: Profile);
            var reasons = new List<EconomyReason>();
            bool gems = false;
            rig.Economy.Changed += change =>
            {
                reasons.Add(change.Reason);
                gems |= change.Currency == CurrencyId.Gems;
            };
            long before = rig.State.Wallet.Gems;
            long given = 0;
            for (int day = 0; day < 60; day++)
            {
                given += rig.Open().Granted.Bundle.Gems;
                rig.NextDay();
            }
            Assert.IsTrue(gems, "some gifts gave Gems in 60 days");
            Assert.AreEqual(before + given, rig.State.Wallet.Gems);
            foreach (var reason in reasons)
                Assert.AreEqual(EconomyReason.DailyGift, reason);
        }

        [Test]
        public void Opened_IsRaisedOnce_WithTheOpening()
        {
            var rig = Create(profile: Profile);
            int raised = 0;
            GiftOpening seen = default;
            rig.Gifts.Opened += opening =>
            {
                raised++;
                seen = opening;
            };
            var first = rig.Open();
            rig.Gifts.TryOpen(out _);
            Assert.AreEqual(1, raised);
            Assert.AreEqual(first.Lot, seen.Lot);
            Assert.AreEqual(rig.Ctx.TodayKey, seen.DayKey);
            rig.NextDay();
            var second = rig.Open();
            Assert.AreEqual(2, raised);
            Assert.AreEqual(second.Lot, seen.Lot);
            Assert.AreEqual(second.Lot, rig.Gifts.LastLot);
        }
    }
}
