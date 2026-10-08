using System;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T36, doc « 02 — Renouveau, Essence, maîtrises et liens », section 4.1 and doc « 01 — Contenu des rendez-vous »,
    /// section 10 (the minimal bond service, plan gap 15): touches counted 5 a day per animal and 30 a day in all, again
    /// the next day; each treat's points and +50 % for its family; bond capped at 1 000 and never lowered; the production
    /// stays exact (a rebuilt rate gives the same); Essence Comfort and Best Friends.
    /// </summary>
    public class BondTests
    {
        private static readonly ContentId Grassland = ContentId.FromSource("biome.grassland");
        private static readonly ContentId Rabbit = ContentId.FromSource("species.rabbit");
        private static readonly ContentId Penguin = ContentId.FromSource("species.penguin");
        private static readonly ContentId CloverCrunch = ContentId.FromSource("treat.clover_crunch");
        private static readonly ContentId Hazelnut = ContentId.FromSource("treat.toasted_hazelnut");
        private static readonly ContentId Comfort = ContentId.FromSource("essence.habitat.comfort");
        private static readonly ContentId BestFriends = ContentId.FromSource("essence.habitat.best_friends");

        /// <summary>Doc 01, section 10.2, in the order of ProgressionConfig.Treats.</summary>
        private static readonly int[] DocPoints = { 10, 10, 10, 12, 12, 12, 14, 14, 14, 16, 16, 16, 18 };

        private sealed class Rig
        {
            public PlayerState State;
            public FakeGameClock Clock;
            public ContentRegistry Content;
            public EconomyService Economy;
            public ProgressionServices P;
            public DailyContext Ctx;
            public BondService Bond;

            public uint Add(ContentId species, bool favorite = false, ushort bond = 0)
            {
                var animal = new AnimalInstance
                {
                    Species = species,
                    Rarity = RarityTier.Common,
                    BondPoints = bond,
                    Flags = favorite ? AnimalFlags.Favorite : AnimalFlags.None,
                };
                uint id = State.Collection.Add(animal);
                State.Collection.TryGet(id, out animal);
                Economy.OnAnimalAdded(animal);
                return id;
            }

            public int BondOf(uint id)
            {
                State.Collection.TryGet(id, out var animal);
                return animal.BondPoints;
            }

            public int Owned(ContentId treat) => State.Progression.Treats.TryGetValue(treat, out int count) ? count : 0;

            public void Give(ContentId treat, int count) => State.Progression.Treats[treat] = Owned(treat) + count;

            public double Rate() => Economy.GetLightPerSecond().ToDoubleClamped();
        }

        /// <summary>One species per family that matters here; the penguin's family is preferred by no treat.</summary>
        private static ContentRegistry Content()
        {
            var builder = new ContentRegistryBuilder();
            AddSpecies(builder, "rabbit", RigFamily.SmallQuadruped, 1.0);
            AddSpecies(builder, "fawn", RigFamily.MediumQuadruped, 2.0);
            AddSpecies(builder, "robin", RigFamily.Bird, 1.5);
            AddSpecies(builder, "otter", RigFamily.SwimmerLow, 2.5);
            AddSpecies(builder, "penguin", RigFamily.Penguin, 3.0);
            return builder.Build();
        }

        private static void AddSpecies(ContentRegistryBuilder builder, string name, RigFamily family, double production)
        {
            builder.AddSpecies(new SpeciesDefinition(ContentId.FromSource("species." + name), "species." + name + ".name", family, Grassland,
                production, "PF_Animal_Test"), "species." + name);
        }

        private static Rig Create(bool progression = false, int comfort = 0, int bestFriends = 0)
        {
            var content = Content();
            var rig = new Rig { State = PlayerState.CreateNew(TestUtil.T0), Clock = new FakeGameClock(TestUtil.T0), Content = content };
            if (comfort > 0)
                rig.State.Progression.EssenceRanks[Comfort] = comfort;
            if (bestFriends > 0)
                rig.State.Progression.EssenceRanks[BestFriends] = bestFriends;
            var config = new ProgressionConfig { TimeZone = TimeZoneInfo.Utc };
            rig.Economy = new EconomyService(rig.State, content, EconomyConfig.Default, null);
            if (progression)
                rig.P = new ProgressionServices(rig.State, content, rig.Economy, null, rig.Clock, null, config);
            rig.Ctx = new DailyContext(rig.State, content, new DailyVisitConfig(), config, rig.Clock, rig.Economy, null, null, rig.P, null);
            rig.Bond = new BondService(rig.Ctx);
            return rig;
        }

        // ---------------- Touches (doc 02 Renouveau, section 4.1; doc 08 econ.bond.touch*) ----------------

        [Test]
        public void Touch_CountsFiveTimesADayPerAnimal()
        {
            var rig = Create();
            uint rabbit = rig.Add(Rabbit);
            int events = 0;
            int counted = 0;
            rig.Bond.Touched += (animal, inside) =>
            {
                events++;
                if (inside)
                    counted++;
            };

            for (int i = 0; i < 5; i++)
                Assert.IsTrue(rig.Bond.TryTouch(rabbit), "Touch " + (i + 1));
            Assert.IsFalse(rig.Bond.TryTouch(rabbit), "The 6th touch of the day gives nothing.");
            Assert.IsFalse(rig.Bond.TryTouch(rabbit));

            Assert.AreEqual(5, rig.BondOf(rabbit), "1 point per counted touch.");
            Assert.AreEqual(7, events, "Every touch is reported: the animal still reacts.");
            Assert.AreEqual(5, counted);
            Assert.AreEqual(5, rig.Bond.TouchesToday);
            Assert.IsFalse(rig.Bond.TryTouch(4242u), "An unknown animal is never counted.");
        }

        [Test]
        public void Touch_CountsThirtyTimesADayInAll()
        {
            var rig = Create();
            var animals = new uint[8];
            for (int i = 0; i < animals.Length; i++)
                animals[i] = rig.Add(Rabbit);

            int counted = 0;
            foreach (uint animal in animals)
                for (int t = 0; t < 5; t++)
                    if (rig.Bond.TryTouch(animal))
                        counted++;

            Assert.AreEqual(30, counted, "Six animals × 5, then the refuge's daily total.");
            Assert.AreEqual(30, rig.Bond.TouchesToday);
            uint fresh = rig.Add(Penguin);
            Assert.IsFalse(rig.Bond.TryTouch(fresh), "A new animal past the total gives nothing either.");
            int total = 0;
            foreach (uint animal in animals)
                total += rig.BondOf(animal);
            Assert.AreEqual(30, total);
            Assert.AreEqual(0, rig.BondOf(fresh));
        }

        [Test]
        public void Touch_CapsStartAgainTheNextDay_NeverWhenTheDayGoesBack()
        {
            var rig = Create();
            uint rabbit = rig.Add(Rabbit);
            for (int i = 0; i < 6; i++)
                rig.Bond.TryTouch(rabbit);
            int today = rig.Ctx.TodayKey;

            rig.Ctx.TodayKey = DailyDay.AddDays(today, -1);
            Assert.IsFalse(rig.Bond.TryTouch(rabbit), "A day key that went back (time zone westward) keeps today's counters.");
            rig.Ctx.TodayKey = today;
            Assert.IsFalse(rig.Bond.TryTouch(rabbit));

            rig.Ctx.TodayKey = DailyDay.AddDays(today, 1);
            rig.Bond.OnDayChanged();
            Assert.AreEqual(0, rig.Bond.TouchesToday, "A new day: the caps start again.");
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(rig.Bond.TryTouch(rabbit), "Next day, touch " + (i + 1));
            Assert.IsFalse(rig.Bond.TryTouch(rabbit));
            Assert.AreEqual(10, rig.BondOf(rabbit));
            Assert.AreEqual(rig.Ctx.TodayKey, rig.State.DailyVisit.TouchDayKey);
        }

        // ---------------- Treats (doc 01, section 10) ----------------

        [Test]
        public void Treat_PointsFollowTheDoc()
        {
            var rig = Create();
            uint penguin = rig.Add(Penguin);
            var treats = rig.Ctx.ProgressionConfig.Treats;
            Assert.AreEqual(DocPoints.Length, treats.Length, "The 13 biome treats.");
            int total = 0;
            for (int i = 0; i < treats.Length; i++)
            {
                rig.Give(treats[i].Id, 1);
                Assert.AreEqual(DocPoints[i], rig.Bond.TreatPoints(penguin, treats[i].Id), treats[i].SourceId);
                Assert.IsTrue(rig.Bond.TryGiveTreat(penguin, treats[i].Id, out int points), treats[i].SourceId);
                Assert.AreEqual(DocPoints[i], points, treats[i].SourceId);
                Assert.AreEqual(0, rig.Owned(treats[i].Id), "The treat is eaten.");
                total += points;
            }
            Assert.AreEqual(174, total);
            Assert.AreEqual(total, rig.BondOf(penguin));
        }

        [TestCase("rabbit", "treat.clover_crunch", 15)]
        [TestCase("rabbit", "treat.golden_date", 21)]
        [TestCase("fawn", "treat.toasted_hazelnut", 15)]
        [TestCase("fawn", "treat.cloud_marshmallow", 24)]
        [TestCase("robin", "treat.alpine_biscuit", 18)]
        [TestCase("robin", "treat.cherry_bite", 21)]
        [TestCase("otter", "treat.star_candy", 27)]
        [TestCase("rabbit", "treat.toasted_hazelnut", 10)]
        [TestCase("robin", "treat.star_candy", 18)]
        public void Treat_ItsFamily_GetsHalfMore(string species, string treat, int expected)
        {
            var rig = Create();
            uint animal = rig.Add(ContentId.FromSource("species." + species));
            var id = ContentId.FromSource(treat);
            rig.Give(id, 1);
            Assert.IsTrue(rig.Bond.TryGiveTreat(animal, id, out int points));
            Assert.AreEqual(expected, points);
            Assert.AreEqual(expected, rig.BondOf(animal));
        }

        [Test]
        public void Treat_TheFamilysTreatFirst_ElseTheMostPlentiful()
        {
            var rig = Create();
            uint rabbit = rig.Add(Rabbit);
            uint penguin = rig.Add(Penguin);
            rig.Give(Hazelnut, 4);
            rig.Give(CloverCrunch, 1);

            Assert.AreEqual(CloverCrunch, rig.Bond.BestTreatFor(rabbit), "The rabbit's family prefers Clover Crunch.");
            Assert.IsTrue(rig.Bond.TryGiveTreat(rabbit, out var treat, out int points));
            Assert.AreEqual(CloverCrunch, treat);
            Assert.AreEqual(15, points);
            Assert.IsFalse(rig.State.Progression.Treats.ContainsKey(CloverCrunch), "The last one leaves the stock.");

            Assert.AreEqual(Hazelnut, rig.Bond.BestTreatFor(rabbit), "None of its family left: the most plentiful.");
            Assert.IsTrue(rig.Bond.TryGiveTreat(penguin, out treat, out points));
            Assert.AreEqual(Hazelnut, treat);
            Assert.AreEqual(10, points);
            Assert.AreEqual(3, rig.Owned(Hazelnut));
        }

        [Test]
        public void Treat_NoneOwned_GivesNothing()
        {
            var rig = Create();
            uint rabbit = rig.Add(Rabbit);
            Assert.IsFalse(rig.Bond.TryGiveTreat(rabbit, out var treat, out int points));
            Assert.AreEqual(ContentId.None, treat);
            Assert.IsFalse(rig.Bond.TryGiveTreat(rabbit, CloverCrunch, out points), "Not owned.");

            rig.Give(CloverCrunch, 1);
            Assert.IsFalse(rig.Bond.TryGiveTreat(4242u, out treat, out points), "Unknown animal.");
            Assert.IsFalse(rig.Bond.TryGiveTreat(4242u, CloverCrunch, out points));
            Assert.AreEqual(1, rig.Owned(CloverCrunch), "Nothing eaten.");
            Assert.AreEqual(0, rig.BondOf(rabbit));
        }

        [Test]
        public void Treat_SeedCookieAndGoldenTreat_HaveNoFamily_TheBasketIsNotForOneAnimal()
        {
            var rig = Create();
            uint rabbit = rig.Add(Rabbit);
            var basket = ContentId.FromSource("treat.treat_basket");
            rig.Give(BondService.SeedCookie, 1);
            rig.Give(BondService.GoldenTreat, 1);
            rig.Give(basket, 1);

            Assert.AreEqual(ContentId.None, rig.Bond.BestTreatFor(rabbit), "Rare treats are never picked on their own.");
            Assert.IsTrue(rig.Bond.TryGiveTreat(rabbit, BondService.SeedCookie, out int points));
            Assert.AreEqual(20, points);
            Assert.IsTrue(rig.Bond.TryGiveTreat(rabbit, BondService.GoldenTreat, out points));
            Assert.AreEqual(40, points);
            Assert.IsFalse(rig.Bond.TryGiveTreat(rabbit, basket, out points), "The basket feeds a whole biome (later task).");
            Assert.AreEqual(1, rig.Owned(basket));
            Assert.AreEqual(60, rig.BondOf(rabbit));
        }

        // ---------------- Cap, never lower ----------------

        [Test]
        public void Bond_CappedAtOneThousand_NeverLowered()
        {
            var rig = Create();
            uint rabbit = rig.Add(Rabbit, bond: 995);
            IBondSink sink = rig.Bond;
            Assert.AreEqual(5, sink.AddBond(rabbit, 15, BondSource.Wish), "Only what fits under 1 000.");
            Assert.AreEqual(1000, rig.BondOf(rabbit));
            Assert.AreEqual(0, sink.AddBond(rabbit, 15, BondSource.Wish));
            Assert.IsTrue(rig.Bond.TryTouch(rabbit), "The touch still counts for the day.");
            Assert.AreEqual(1000, rig.BondOf(rabbit));

            rig.Give(CloverCrunch, 1);
            Assert.IsTrue(rig.Bond.TryGiveTreat(rabbit, out _, out int points), "A full animal still eats its treat.");
            Assert.AreEqual(0, points);
            Assert.AreEqual(0, rig.Bond.AddBond(rabbit, -50, BondSource.Wish), "Never lowered.");
            Assert.AreEqual(0, rig.Bond.AddBond(rabbit, 0, BondSource.Touch));
            Assert.AreEqual(1000, rig.BondOf(rabbit));

            uint penguin = rig.Add(Penguin, bond: 800);
            rig.Ctx.Config.BondMax = 500;
            Assert.AreEqual(0, rig.Bond.AddBond(penguin, 10, BondSource.Touch), "A lower cap never takes points away.");
            Assert.AreEqual(800, rig.BondOf(penguin));
            Assert.AreEqual(0, rig.Bond.AddBond(4242u, 10, BondSource.Touch), "Unknown animal.");
        }

        // ---------------- Production ----------------

        [Test]
        public void Bond_KeepsTheProductionExact()
        {
            var rig = Create();
            uint rabbit = rig.Add(Rabbit);
            uint penguin = rig.Add(Penguin);
            Assert.AreEqual(4.0, rig.Rate(), 1e-9, "1 + 3 Light per second, no bond.");

            Assert.AreEqual(500, rig.Bond.AddBond(penguin, 500, BondSource.Wish));
            Assert.AreEqual(1.0 + 3.0 * 1.1, rig.Rate(), 1e-9, "Half the bond: +10 % for that animal (up to +20 % at 1 000).");

            for (int i = 0; i < 5; i++)
                rig.Bond.TryTouch(rabbit);
            rig.Give(CloverCrunch, 2);
            rig.Bond.TryGiveTreat(rabbit, out _, out _);
            rig.Bond.TryGiveTreat(penguin, out _, out _);
            Assert.AreEqual(20, rig.BondOf(rabbit));
            Assert.AreEqual(510, rig.BondOf(penguin));

            var rebuilt = new EconomyService(rig.State, rig.Content, EconomyConfig.Default, null);
            Assert.AreEqual(rebuilt.GetLightPerSecond().ToDoubleClamped(), rig.Rate(), 1e-9, "The same rate as one rebuilt from the collection.");
            Assert.AreEqual(1.0 * (1.0 + 0.2 * 0.020) + 3.0 * (1.0 + 0.2 * 0.510), rig.Rate(), 1e-9);
        }

        // ---------------- Essence (doc 02 Renouveau, section 2) ----------------

        [Test]
        public void Essence_ComfortForAll_BestFriendsForFavorites()
        {
            var rig = Create(progression: true, comfort: 5, bestFriends: 3);
            uint rabbit = rig.Add(Rabbit);
            uint favorite = rig.Add(Penguin, favorite: true);
            Assert.AreEqual(0.5, rig.P.Essence.Sum(EssenceEffect.BondGain), 1e-9);

            Assert.IsTrue(rig.Bond.TryTouch(rabbit));
            Assert.AreEqual(2, rig.BondOf(rabbit), "1 × 1.5, rounded.");
            Assert.AreEqual(15, rig.Bond.AddBond(rabbit, 10, BondSource.Treat));
            Assert.AreEqual(23, rig.Bond.AddBond(rabbit, 15, BondSource.Wish), "15 × 1.5 = 22.5, rounded up.");
            Assert.AreEqual(18, rig.Bond.AddBond(favorite, 10, BondSource.Treat), "A favorite: × (1 + 0.5 + 0.3).");
            Assert.AreEqual(2 + 15 + 23, rig.BondOf(rabbit));
        }

        [Test]
        public void Essence_None_GivesTheBasePoints()
        {
            var rig = Create(progression: true);
            uint favorite = rig.Add(Rabbit, favorite: true);
            Assert.AreEqual(15, rig.Bond.AddBond(favorite, 15, BondSource.Wish));
            rig.Give(CloverCrunch, 1);
            Assert.IsTrue(rig.Bond.TryGiveTreat(favorite, out _, out int points));
            Assert.AreEqual(15, points, "Clover Crunch for its family, no Essence.");
        }

        // ---------------- Events ----------------

        [Test]
        public void Events_ReportWhatReallyHappened()
        {
            var rig = Create();
            uint rabbit = rig.Add(Rabbit, bond: 990);
            int gained = 0;
            uint gainedAnimal = 0;
            var given = ContentId.None;
            int givenPoints = -1;
            rig.Bond.BondGained += (animal, points) =>
            {
                gainedAnimal = animal;
                gained += points;
            };
            rig.Bond.TreatGiven += (animal, treat, points) =>
            {
                given = treat;
                givenPoints = points;
            };

            rig.Give(CloverCrunch, 1);
            Assert.IsTrue(rig.Bond.TryGiveTreat(rabbit, out _, out _));
            Assert.AreEqual(CloverCrunch, given);
            Assert.AreEqual(10, givenPoints, "15 for its family, 10 fit under the cap.");
            Assert.AreEqual(10, gained);
            Assert.AreEqual(rabbit, gainedAnimal);

            Assert.IsTrue(rig.Bond.TryTouch(rabbit), "Inside the caps even when full.");
            Assert.AreEqual(10, gained, "No points left to add: no bond event.");
        }
    }
}
