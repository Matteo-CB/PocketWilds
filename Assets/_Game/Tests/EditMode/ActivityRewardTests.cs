using System;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T37: the rewards of a rewarded game (doc Mini-jeux section 2; doc 01 Contenu section 12.0; doc 02 Gemmes): the
    /// tables for 0 to 3 stars, the Light of the production, bond and mastery, the « first 3 stars of the day » Gems and
    /// their cap across activities and days, a challenge's Gems once and a decoration every 5, no paid randomness, stickers
    /// as 3 treats before the albums, the event and weekend hooks, the Essence nodes, and a computation that never allocates.
    /// </summary>
    public sealed class ActivityRewardTests
    {
        private sealed class TestEvents : IActivityEventHooks
        {
            public bool Running = true;
            public bool Weekend = true;
            public ActivityId Featured = ActivityId.Test;
            public int Granted;

            public bool EventRunning => Running;
            public bool IsFeatured(ActivityId activity) => activity == Featured;
            public bool ActivitiesWeekend => Weekend;
            public void GrantEventCurrency(int amount, ActivityId activity) => Granted += amount;
        }

        private static ActivityRewardRequest Request(ActivityTestRig rig, int stars, ActivityId activity = ActivityId.Test,
            bool paid = false, uint seed = 1, bool record = false, int challenge = -1, bool firstThree = false)
        {
            return new ActivityRewardRequest
            {
                Activity = activity,
                Mode = challenge >= 0 ? ActivityMode.Challenge : ActivityMode.Free,
                Challenge = challenge,
                Stars = stars,
                PaidTicket = paid,
                Seed = seed,
                NewRecord = record,
                ChallengeFirstThreeStars = firstThree,
                AnimalCount = 1,
                Animal0 = rig.AnimalId(0),
            };
        }

        private static ActivityRewardOutcome Grant(ActivityTestRig rig, in ActivityRewardRequest request)
        {
            rig.S.Rewards.Grant(in request, out var outcome);
            return outcome;
        }

        private static long TotalSpeciesXp(MasteryService mastery, ContentId species)
        {
            var progress = mastery.Get(species);
            long total = progress.Xp;
            for (int level = 0; level < progress.Level; level++)
                total += mastery.XpForNext(level);
            return total;
        }

        [Test]
        public void TheTables_ForZeroToThreeStars()
        {
            var rig = ActivityTestRig.Create();
            double[] light = { 0, 5, 10, 20 };
            int[] bond = { 0, 2, 4, 6 };
            int[] xp = { 0, 1, 2, 3 };
            for (int stars = 0; stars <= 3; stars++)
            {
                var request = Request(rig, stars);
                rig.S.Rewards.Compute(in request, out var outcome);
                Assert.IsTrue(outcome.Rewarded);
                Assert.AreEqual(light[stars], outcome.LightMinutes, 1e-9, stars + " stars: Light");
                Assert.AreEqual(bond[stars], outcome.BondPerAnimal, stars + " stars: bond");
                Assert.AreEqual(xp[stars], outcome.SpeciesXp, stars + " stars: species XP");
                Assert.AreEqual(stars == 0 ? 0 : 6, outcome.BiomeXp, stars + " stars: biome XP (doc 08 econ.biome_mastery.xp.activity)");
                Assert.AreEqual(stars == 3 ? 2 : 0, outcome.DailyGems, stars + " stars: Gems");
                if (stars > 0)
                    Assert.AreEqual(ActivityTestRig.Grassland, outcome.Biome, "No theme: the animal's biome.");
            }

            var practice = Request(rig, 3);
            practice.Mode = ActivityMode.Practice;
            rig.S.Rewards.Compute(in practice, out var none);
            Assert.IsFalse(none.Rewarded, "Practice never rewards.");
            Assert.AreEqual(0.0, none.LightMinutes);
            Assert.AreEqual(0, none.Gems);
            Assert.AreEqual(0, none.Stickers);
        }

        [Test]
        public void TheGrant_GivesTheProductionsLight_TheBond_AndBothMasteries()
        {
            var rig = ActivityTestRig.Create();
            var species = rig.State.Collection[0].Species;
            long speciesXp = TotalSpeciesXp(rig.SpeciesMastery, species);
            long biomeXp = rig.P.Store.Get(ActivityTestRig.Grassland).MasteryXp;
            double rate = rig.Economy.GetLightPerSecond().ToDoubleClamped();
            long gems = rig.Gems;

            var outcome = Grant(rig, Request(rig, 3));

            Assert.AreEqual(rate * 1200.0, outcome.Light.ToDoubleClamped(), rate * 1e-6, "20 minutes of the production.");
            Assert.AreEqual(1, rig.Bond.Calls);
            Assert.AreEqual(6, rig.Bond.Points);
            Assert.AreEqual(rig.AnimalId(0), rig.Bond.LastAnimal);
            Assert.AreEqual(BondSource.Activity, rig.Bond.LastSource);
            Assert.AreEqual(6, outcome.BondAdded);
            Assert.AreEqual(speciesXp + 3, TotalSpeciesXp(rig.SpeciesMastery, species));
            Assert.AreEqual(biomeXp + 6, rig.P.Store.Get(ActivityTestRig.Grassland).MasteryXp);
            Assert.AreEqual(gems + 2, rig.Gems);
        }

        [Test]
        public void TwoAnimalsOfOneSpecies_EachGetTheBond_TheSpeciesItsXpOnce()
        {
            var rig = ActivityTestRig.Create(animals: 12);
            uint first = rig.AnimalId(0);
            uint twin = 0;
            for (int i = 1; i < rig.State.Collection.Count && twin == 0; i++)
                if (rig.State.Collection[i].Species == rig.State.Collection[0].Species)
                    twin = rig.AnimalId(i);
            Assert.AreNotEqual(0u, twin, "Twelve Prairie hatches hold two of one species.");
            var species = rig.State.Collection[0].Species;
            long xp = TotalSpeciesXp(rig.SpeciesMastery, species);

            var request = Request(rig, 2);
            request.AnimalCount = 2;
            request.Animal0 = first;
            request.Animal1 = twin;
            var outcome = Grant(rig, request);
            Assert.AreEqual(2, outcome.BondAnimals);
            Assert.AreEqual(8, rig.Bond.Points, "+4 each.");
            Assert.AreEqual(1, outcome.Species);
            Assert.AreEqual(xp + 2, TotalSpeciesXp(rig.SpeciesMastery, species));
        }

        [Test]
        public void TheFirstThreeStarsOfTheDay_Give2Gems_PerActivity_6ADayAtMost()
        {
            var rig = ActivityTestRig.Create();
            long gems = rig.Gems;
            Assert.AreEqual(2, Grant(rig, Request(rig, 3)).DailyGems);
            Assert.AreEqual(0, Grant(rig, Request(rig, 3)).DailyGems, "Once a day per activity.");
            Assert.AreEqual(0, Grant(rig, Request(rig, 2, ActivityId.Grooming)).DailyGems, "3 stars only.");
            Assert.AreEqual(2, Grant(rig, Request(rig, 3, ActivityId.Grooming)).DailyGems);
            Assert.AreEqual(2, Grant(rig, Request(rig, 3, ActivityId.HideAndSeek)).DailyGems);
            Assert.AreEqual(0, Grant(rig, Request(rig, 3, ActivityId.NutRain)).DailyGems, "6 a day across activities.");
            Assert.AreEqual(gems + 6, rig.Gems);

            rig.Clock.Advance(TimeSpan.FromDays(1));
            rig.S.OnResume();
            Assert.AreEqual(2, Grant(rig, Request(rig, 3)).DailyGems, "A new day.");
            Assert.AreEqual(2, Grant(rig, Request(rig, 3, ActivityId.NutRain)).DailyGems);
            Assert.AreEqual(gems + 10, rig.Gems);
        }

        [Test]
        public void TheDailyCap_CutsTheLastGems()
        {
            var config = new ActivityConfig();
            Assert.IsTrue(config.TryOverride("gems.activity_daily_cap", 5));
            var rig = ActivityTestRig.Create(config: config);
            Assert.AreEqual(2, Grant(rig, Request(rig, 3)).DailyGems);
            Assert.AreEqual(2, Grant(rig, Request(rig, 3, ActivityId.Grooming)).DailyGems);
            Assert.AreEqual(1, Grant(rig, Request(rig, 3, ActivityId.HideAndSeek)).DailyGems);
            Assert.AreEqual(5, rig.Activities.GemsToday);
        }

        [Test]
        public void AChallengesFirstThreeStars_Give5Gems_AndEveryFifthADecoration_InOrder()
        {
            var rig = ActivityTestRig.Create();
            var brush = ContentId.FromSource("decor.giant_brush");
            var tub = ContentId.FromSource("decor.ribbon_tub");
            for (int i = 0; i < 4; i++)
            {
                var outcome = Grant(rig, Request(rig, 3, challenge: i, firstThree: true));
                Assert.AreEqual(5, outcome.ChallengeGems, "Challenge " + i);
                Assert.AreEqual(ContentId.None, outcome.Decoration);
            }
            var fifth = Grant(rig, Request(rig, 3, ActivityId.Grooming, challenge: 0, firstThree: true));
            Assert.AreEqual(brush, fifth.Decoration, "The 5th challenge at 3 stars, any activity: E-DEC-CH-01.");
            Assert.AreEqual(1, rig.State.Inventory.Count(brush));
            Assert.AreEqual(5, rig.Activities.ChallengesThreeStars);
            Assert.AreEqual(1, rig.Activities.ChallengeDecorations);

            var notFirst = Grant(rig, Request(rig, 3, challenge: 4));
            Assert.AreEqual(0, notFirst.ChallengeGems, "Only a challenge's first 3 stars pay.");
            for (int i = 5; i < 10; i++)
                Grant(rig, Request(rig, 3, challenge: i, firstThree: true));
            Assert.AreEqual(1, rig.State.Inventory.Count(tub), "The 10th: E-DEC-CH-02.");
            Assert.AreEqual(2, rig.Activities.ChallengeDecorations);

            Assert.AreEqual(ContentId.FromSource("decor.great_challenge_rosette"), ActivityRewardService.ChallengeDecoration(35));
            Assert.AreEqual(ContentId.None, ActivityRewardService.ChallengeDecoration(36));
            Assert.AreEqual("decor.giant_brush", ActivityRewardService.ChallengeDecorationSource(0));
        }

        [Test]
        public void APaidGame_DrawsNothing_ItsStickerIsAFixedCounter()
        {
            var rig = ActivityTestRig.Create();
            var first = Grant(rig, Request(rig, 3, paid: true, seed: 11));
            Assert.IsTrue(first.FixedStickerRule);
            Assert.AreEqual(0, first.Stickers);
            Assert.AreEqual(35, first.StickerPoints);
            var second = Grant(rig, Request(rig, 3, paid: true, seed: 12));
            Assert.AreEqual(70, second.StickerPoints);
            var third = Grant(rig, Request(rig, 3, paid: true, seed: 13));
            Assert.AreEqual(1, third.Stickers, "105 points: a sticker, 5 kept.");
            Assert.IsTrue(third.ChanceSticker);
            Assert.AreEqual(5, third.StickerPoints);
            Assert.AreEqual(5, rig.Record().PaidStickerPoints);
            Assert.AreEqual(0, rig.Record(ActivityId.Grooming).PaidStickerPoints, "One counter per activity.");

            // The seed never changes a paid game's outcome.
            var reference = Request(rig, 1, paid: true, seed: 0);
            rig.S.Rewards.Compute(in reference, out var expected);
            for (uint seed = 1; seed < 500; seed++)
            {
                var request = Request(rig, 1, paid: true, seed: seed);
                rig.S.Rewards.Compute(in request, out var outcome);
                Assert.AreEqual(expected.Stickers, outcome.Stickers, "seed " + seed);
                Assert.AreEqual(expected.StickerPoints, outcome.StickerPoints, "seed " + seed);
            }
            Assert.AreEqual(15, expected.StickerPoints, "5 kept + 10 for a star.");
        }

        [Test]
        public void AFreeTicketGame_DrawsItsStickerFromItsSeed_AtTheTablesChance()
        {
            var rig = ActivityTestRig.Create();
            int[] hits = new int[4];
            const int Games = 4000;
            for (int stars = 1; stars <= 3; stars++)
                for (uint seed = 0; seed < Games; seed++)
                {
                    var request = Request(rig, stars, seed: seed * 7919u + 3u);
                    rig.S.Rewards.Compute(in request, out var outcome);
                    Assert.IsFalse(outcome.FixedStickerRule);
                    hits[stars] += outcome.Stickers;
                }
            Assert.AreEqual(0.10, hits[1] / (double)Games, 0.02);
            Assert.AreEqual(0.20, hits[2] / (double)Games, 0.025);
            Assert.AreEqual(0.35, hits[3] / (double)Games, 0.03);

            var again = Request(rig, 3, seed: 42);
            rig.S.Rewards.Compute(in again, out var a);
            rig.S.Rewards.Compute(in again, out var b);
            Assert.AreEqual(a.Stickers, b.Stickers, "Same game, same draw.");
        }

        [Test]
        public void BeforeTheAlbums_AStickerIs3TreatsOfTheNewestBiome_After_ItWaitsForTheAlbum()
        {
            var rig = ActivityTestRig.Create();
            rig.State.Progression.Treats.TryGetValue(ActivityTestRig.CloverCrunch, out int treats);
            var outcome = Grant(rig, Request(rig, 1, paid: true, record: true));
            Assert.AreEqual(1, outcome.Stickers);
            Assert.IsTrue(outcome.RecordSticker, "A new record: a guaranteed sticker.");
            Assert.AreEqual(ActivityTestRig.CloverCrunch, outcome.Treat);
            Assert.AreEqual(3, outcome.Treats);
            Assert.AreEqual(treats + 3, rig.State.Progression.Treats[ActivityTestRig.CloverCrunch]);
            Assert.AreEqual(0, rig.State.Inventory.PendingStickers);

            rig.Config.AlbumsAvailable = true;
            rig.State.Progression.Refuge.Level = 12;
            rig.Clock.Advance(TimeSpan.FromDays(4));
            var later = Grant(rig, Request(rig, 1, paid: true, record: true));
            Assert.AreEqual(1, later.Stickers);
            Assert.AreEqual(0, later.Treats, "The albums are open (level 12, day 5).");
            Assert.AreEqual(1, rig.State.Inventory.PendingStickers);
        }

        [Test]
        public void TheWeekend_DoublesTheRewards_ButNeverGemsNorStickers_AndEventsPayTheirCurrency()
        {
            var rig = ActivityTestRig.Create();
            var events = new TestEvents();
            rig.S.Events = events;
            var outcome = Grant(rig, Request(rig, 3, record: true));
            Assert.IsTrue(outcome.Weekend);
            Assert.AreEqual(40.0, outcome.LightMinutes, 1e-9);
            Assert.AreEqual(12, outcome.BondPerAnimal);
            Assert.AreEqual(6, outcome.SpeciesXp);
            Assert.AreEqual(12, outcome.BiomeXp);
            Assert.AreEqual(2, outcome.DailyGems, "Gems are never doubled.");
            Assert.IsTrue(outcome.Stickers <= 2, "The record's sticker is never doubled.");
            Assert.IsTrue(outcome.RecordSticker);
            Assert.AreEqual(120, outcome.EventCurrency, "40, × 1.5 on the featured activity, × 2 on the weekend.");
            Assert.AreEqual(120, events.Granted);

            events.Weekend = false;
            var other = Grant(rig, Request(rig, 1, ActivityId.Grooming));
            Assert.AreEqual(20, other.EventCurrency);
            Assert.AreEqual(5.0, other.LightMinutes, 1e-9);

            events.Running = false;
            Assert.AreEqual(0, Grant(rig, Request(rig, 3, ActivityId.Grooming)).EventCurrency, "No event, no currency.");
        }

        [Test]
        public void TheEssenceNodes_AddLight_AndStickerChance()
        {
            var rig = ActivityTestRig.Create();
            rig.State.Progression.EssenceRanks[ContentId.FromSource("essence.economy.soft_light")] = 5;
            rig.State.Progression.EssenceRanks[ContentId.FromSource("essence.discovery.flair")] = 4;
            rig.State.Progression.EssenceRanks[ContentId.FromSource("essence.discovery.variant_eye")] = 1;
            rig.Economy.Grant(CurrencyId.Essence, BigValue.FromDouble(1000), EconomyReason.Debug);
            Assert.IsTrue(rig.P.Essence.TryBuy(ContentId.FromSource("essence.economy.activity_harvest"), out _));
            Assert.IsTrue(rig.P.Essence.TryBuy(ContentId.FromSource("essence.discovery.stickers"), out _));

            var request = Request(rig, 3, paid: true);
            rig.S.Rewards.Compute(in request, out var outcome);
            Assert.AreEqual(22.0, outcome.LightMinutes, 1e-9, "+10 % per Activity Harvest rank.");
            Assert.AreEqual(37, outcome.StickerPoints, "35 + 2 points per Stickers rank.");
        }

        [Test]
        public void LightMinutes_FollowRemoteConfig_AndGrowWithTheStars()
        {
            var config = new ActivityConfig();
            Assert.IsTrue(config.TryOverride("econ.light_minutes.activity_1star", 30));
            Assert.IsFalse(config.TryOverride("econ.light_minutes.activity_3star", 4));
            Assert.AreEqual(30.0, config.LightMinutes(1));
            Assert.AreEqual(30.0, config.LightMinutes(2), "Never below the star under it.");
            Assert.AreEqual(30.0, config.LightMinutes(3));
            Assert.IsTrue(config.TryOverride("gems.activity_challenge", 7));
            Assert.IsTrue(config.TryOverride("activities.challenge_decoration_every", 1));
            var rig = ActivityTestRig.Create(config: config);
            var outcome = Grant(rig, Request(rig, 3, challenge: 0, firstThree: true));
            Assert.AreEqual(7, outcome.ChallengeGems);
            Assert.AreEqual(ContentId.FromSource("decor.giant_brush"), outcome.Decoration, "A decoration at every challenge now.");
        }

        [Test]
        public void TheComputation_DoesNotAllocate()
        {
            var rig = ActivityTestRig.Create();
            var request = Request(rig, 3, paid: false, record: true, challenge: 2, firstThree: true);
            rig.S.Rewards.Compute(in request, out var outcome);
            Assert.That(() => rig.S.Rewards.Compute(in request, out outcome), Is.Not.AllocatingGCMemory());
            var paid = Request(rig, 2, paid: true);
            Assert.That(() => rig.S.Rewards.Compute(in paid, out outcome), Is.Not.AllocatingGCMemory());
        }
    }
}
