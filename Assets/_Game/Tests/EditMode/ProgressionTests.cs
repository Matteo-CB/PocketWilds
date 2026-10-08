using System;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T35A, doc « 02 — Améliorations, agrandissements, niveau du refuge et ouverture des biomes », section 16: Light
    /// upgrades, zones, biome gates, refuge level, biome mastery, Light decorations, treat stand, Renewal.
    /// </summary>
    public class ProgressionTests
    {
        private static readonly ContentId Grassland = ContentId.FromSource("biome.grassland");
        private static readonly ContentId Forest = ContentId.FromSource("biome.forest");
        private static readonly ContentId Lake = ContentId.FromSource("biome.lake");
        private static readonly ContentId Basic = ContentId.FromSource("egg.grassland.basic");

        private sealed class Rig
        {
            public PlayerState State;
            public FakeGameClock Clock;
            public EconomyService Economy;
            public HatchService Hatch;
            public ProgressionServices P;
            public ProgressionConfig Config;

            public void Give(double light) => Economy.Grant(CurrencyId.Light, BigValue.FromDouble(light), EconomyReason.Debug);
        }

        /// <summary>Five Prairie species (the Forest gate asks for four), one Forest species, one egg.</summary>
        private static ContentRegistry Content(bool withForest = true)
        {
            var builder = new ContentRegistryBuilder();
            string[] grassland = { "rabbit", "squirrel", "hedgehog", "mole", "duckling" };
            foreach (var name in grassland)
                AddSpecies(builder, name, Grassland);
            if (withForest)
                AddSpecies(builder, "fawn", Forest);
            builder.AddEgg(new EggDefinition(Basic, "egg.grassland.basic.name", Grassland, EggTier.Basic,
                new Color32[EggDefinition.PaletteSize], 15, 0, 15, "PF_Egg_Base", 10.0, 1.18,
                new[] { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 }, new[] { new EggDropEntry(ContentId.FromSource("species.rabbit"), 1.0) }),
                "egg.grassland.basic");
            return builder.Build();
        }

        private static void AddSpecies(ContentRegistryBuilder builder, string name, ContentId biome)
        {
            var species = ContentId.FromSource("species." + name);
            builder.AddSpecies(new SpeciesDefinition(species, "species." + name + ".name", RigFamily.SmallQuadruped, biome, 1.0, "PF_Animal_Test"),
                "species." + name);
            foreach (var (variant, rarity) in new[] { ("brown", RarityTier.Common), ("white", RarityTier.Common), ("spotted", RarityTier.Rare) })
            {
                string id = "variant." + name + "." + variant;
                builder.AddVariant(new VariantDefinition(ContentId.FromSource(id), species, id + ".name", rarity,
                    new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0), id);
            }
        }

        private static Rig Create(ContentRegistry content = null, PlayerState state = null)
        {
            var config = new ProgressionConfig { TimeZone = TimeZoneInfo.Utc };
            var rig = new Rig
            {
                State = state ?? PlayerState.CreateNew(TestUtil.T0),
                Clock = new FakeGameClock(TestUtil.T0),
                Config = config,
            };
            content ??= Content();
            rig.Economy = new EconomyService(rig.State, content, EconomyConfig.Default, null);
            var mastery = new MasteryService(rig.State, rig.Economy.Config, rig.Economy);
            rig.Hatch = new HatchService(rig.State, content, rig.Economy, rig.Clock, null, mastery);
            rig.P = new ProgressionServices(rig.State, content, rig.Economy, rig.Hatch, rig.Clock, null, config);
            return rig;
        }

        private static void Discover(Rig rig, string species, int variants = 1)
        {
            rig.State.Collection.DiscoveredSpecies.Add(ContentId.FromSource("species." + species));
            string[] names = { "brown", "white", "spotted" };
            for (int i = 0; i < variants; i++)
                rig.State.Collection.DiscoveredVariants.Add(ContentId.FromSource("variant." + species + "." + names[i]));
            rig.P.Refuge.Recompute();
        }

        // ---------------- Light upgrades (section 3) ----------------

        [TestCase(0, 50.0, 1.0)]
        [TestCase(1, 62.5, 1.1)]
        [TestCase(5, 153.0, 1.5)]
        [TestCase(10, 466.0, 2.0)]
        [TestCase(24, 10_588.0, 3.4)]
        [TestCase(25, 13_235.0, 7.0)]
        [TestCase(49, 2_802_597.0, 11.8)]
        [TestCase(50, 3_503_246.0, 24.0)]
        [TestCase(100, 2.454547e11, 176.0)]
        public void Production_CostAndEffect_FollowTheDocTable(int level, double cost, double multiplier)
        {
            var rig = Create();
            double price = rig.P.Upgrades.LevelPrice(Grassland, UpgradeKind.Production, level).ToDoubleClamped();
            Assert.AreEqual(cost, price, Math.Max(1.0, cost * 1e-4), $"Price of level {level}.");
            Assert.AreEqual(multiplier, rig.P.Upgrades.ProductionMultiplierAt(level), 1e-9, $"Multiplier at level {level}.");
        }

        [TestCase(0, 200.0)]
        [TestCase(5, 897.0)]
        [TestCase(10, 4_021.0)]
        [TestCase(20, 80_900.0)]
        [TestCase(30, 1.63e6)]
        [TestCase(40, 3.27e7)]
        [TestCase(49, 4.87e8)]
        public void CozyNests_CostFollowsTheDoc(int level, double cost)
        {
            var rig = Create();
            double price = rig.P.Upgrades.LevelPrice(Grassland, UpgradeKind.Nests, level).ToDoubleClamped();
            Assert.AreEqual(cost, price, cost * 0.003, $"Price of level {level}.");
        }

        [Test]
        public void CuriousEye_CostDoublesFrom500()
        {
            var rig = Create();
            for (int level = 0; level < 10; level++)
                Assert.AreEqual(500.0 * Math.Pow(2, level), rig.P.Upgrades.LevelPrice(Grassland, UpgradeKind.Curiosity, level).ToDoubleClamped(), 0.5);
        }

        [Test]
        public void Max_ByGeometricSum_MatchesTheLevelByLevelLoop_On10000Draws()
        {
            var rng = new System.Random(35);
            double[] growths = { 1.25, 1.35, 2.0, 1.18 };
            int mismatches = 0;
            for (int draw = 0; draw < 10_000; draw++)
            {
                double growth = growths[draw % growths.Length];
                double firstValue = Math.Round(Math.Pow(10, rng.NextDouble() * 6.0) + 1.0);
                var first = BigValue.FromDouble(firstValue);
                double budget = firstValue * Math.Pow(10, rng.NextDouble() * 5.0);
                int limit = draw % 3 == 0 ? 50 : int.MaxValue;
                int n = ProgressionMath.MaxAffordable(first, growth, BigValue.FromDouble(budget), limit);

                int loop = 0;
                double spent = 0.0, price = firstValue;
                while (loop < limit && spent + price <= budget * (1 + 1e-12))
                {
                    spent += price;
                    price *= growth;
                    loop++;
                }
                if (n != loop)
                {
                    // Only a budget sitting on the boundary (rounding of the total) may differ, by one level.
                    double boundary = firstValue * (Math.Pow(growth, Math.Max(n, loop)) - 1) / (growth - 1);
                    Assert.LessOrEqual(Math.Abs(n - loop), 1, $"draw {draw}: closed sum {n}, loop {loop}");
                    Assert.LessOrEqual(Math.Abs(boundary - budget), 1.0, $"draw {draw}: closed sum {n}, loop {loop} away from a boundary (totals are whole Light)");
                    mismatches++;
                }
            }
            TestContext.WriteLine($"Boundary draws: {mismatches} of 10 000.");
        }

        [Test]
        public void Buying_SpendsTheQuote_RaisesTheLevel_AndTheProductionFollows()
        {
            var rig = Create();
            rig.State.Collection.Add(new AnimalInstance { Species = ContentId.FromSource("species.rabbit"), Rarity = RarityTier.Common });
            rig.Economy.RebuildAggregates();
            var before = rig.Economy.GetLightPerSecond().ToDoubleClamped();
            rig.Give(2_000);
            Assert.IsTrue(rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Production, PurchaseQty.Ten, out var purchase));
            Assert.AreEqual(0, purchase.LevelFrom);
            Assert.AreEqual(10, purchase.LevelTo);
            double expectedCost = ProgressionMath.GeometricTotal(BigValue.FromDouble(50), 1.25, 10).ToDoubleClamped();
            Assert.AreEqual(2_000 - expectedCost, rig.Economy.Light.ToDoubleClamped(), 0.5);
            Assert.AreEqual(before * 2.0, rig.Economy.GetLightPerSecond().ToDoubleClamped(), 1e-9, "Level 10: x 2.");
            Assert.IsFalse(rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Production, PurchaseQty.Ten, out _), "Not enough Light: nothing spent.");
        }

        [Test]
        public void MaxPurchase_CrossingAMilestone_ReportsItOnce()
        {
            var rig = Create();
            rig.Give(1e5);
            Assert.IsTrue(rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Production, PurchaseQty.Max, out var purchase));
            Assert.GreaterOrEqual(purchase.LevelTo, 25);
            Assert.IsTrue(purchase.MilestoneCrossed);
        }

        [Test]
        public void CozyNests_StopsAt50_AndCurious_EyeAt10()
        {
            var rig = Create();
            rig.P.Mastery.AddXp(Grassland, 1_000_000);
            rig.Give(1e30);
            Assert.IsTrue(rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Nests, PurchaseQty.Max, out var nests));
            Assert.AreEqual(50, nests.LevelTo);
            Assert.IsTrue(rig.P.Upgrades.Quote(Grassland, UpgradeKind.Nests, PurchaseQty.One).Maxed);
            Assert.AreEqual(Math.Pow(0.96, 50), rig.P.Upgrades.EggPriceMultiplier(Grassland), 1e-12);
            Assert.IsTrue(rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Curiosity, PurchaseQty.Max, out var curiosity));
            Assert.AreEqual(10, curiosity.LevelTo);
            Assert.AreEqual(0.10, rig.P.Upgrades.VariantChanceBonus(Grassland), 1e-12);
            // The protection is capped at 30 %: with Curious Eye at 10, the undiscovered variant chance never passes 40 %.
            Assert.LessOrEqual(rig.Hatch.VariantBonus(ContentId.FromSource("species.rabbit"), 10_000) + rig.P.VariantChanceBonus(Grassland), 0.40 + 1e-12);
        }

        [Test]
        public void Upgrades_WaitForBiomeMastery()
        {
            var rig = Create();
            rig.Give(1e9);
            Assert.AreEqual(2, rig.P.Upgrades.Quote(Grassland, UpgradeKind.Nests, PurchaseQty.One).LockedUntilMastery);
            Assert.IsFalse(rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Nests, PurchaseQty.One, out _));
            rig.P.Mastery.AddXp(Grassland, ProgressionMath.MasteryXpForLevel(rig.Config, 1) + ProgressionMath.MasteryXpForLevel(rig.Config, 2));
            Assert.AreEqual(2, rig.P.Mastery.GetLevel(Grassland));
            Assert.IsTrue(rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Nests, PurchaseQty.One, out _));
            Assert.IsFalse(rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Curiosity, PurchaseQty.One, out _), "Curious Eye waits for mastery 5.");
        }

        [Test]
        public void CozyNests_LowersTheEggPrice()
        {
            var rig = Create();
            rig.State.Collection.Add(new AnimalInstance { Species = ContentId.FromSource("species.rabbit") });
            rig.State.World.EggPurchasesInCycle[Basic] = 10;
            double before = rig.Hatch.GetCost(Basic).ToDoubleClamped();
            rig.P.Mastery.AddXp(Grassland, 1_000);
            rig.Give(1e6);
            Assert.IsTrue(rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Nests, PurchaseQty.Ten, out _));
            Assert.AreEqual(Math.Round(before * Math.Pow(0.96, 10)), rig.Hatch.GetCost(Basic).ToDoubleClamped(), 1.0);
        }

        // ---------------- Zones (section 4) ----------------

        [Test]
        public void Zones_OpenInOrder_WithMastery_AndThePassageIsNeverForSale()
        {
            var rig = Create();
            var zones = rig.P.Zones;
            Assert.IsTrue(zones.TryGetNext(Grassland, out var first));
            Assert.AreEqual("zone.grassland.meadow", first.SourceId);
            Assert.AreEqual(250.0, zones.Price(first).ToDoubleClamped());
            Assert.AreEqual(1, zones.RequiredMastery(first));
            rig.Give(1e9);
            Assert.AreEqual(ZoneState.NeedsMastery, zones.GetState(first));
            Assert.IsFalse(zones.TryOpen(first.Id, out _));

            rig.P.Mastery.AddXp(Grassland, 40);
            Assert.AreEqual(ZoneState.Available, zones.GetState(first));
            Assert.IsTrue(zones.TryGet(ContentId.FromSource("zone.grassland.streambend"), out var second));
            Assert.AreEqual(ZoneState.Later, zones.GetState(second), "One zone at a time, in order.");
            Assert.IsTrue(zones.TryOpen(first.Id, out var opening));
            Assert.AreEqual(250.0, opening.Cost.ToDoubleClamped());
            Assert.AreEqual(ZoneState.NeedsMastery, zones.GetState(second), "Zone 2 needs mastery 4.");
            Assert.AreEqual(537.5, zones.Price(second).ToDoubleClamped(), 0.5);
            Assert.AreEqual(1, zones.ExpansionsOpen(Grassland));
            Assert.AreEqual(3, zones.VisibleAnimalBonus(Grassland));

            Assert.IsTrue(zones.TryGet(ContentId.FromSource("zone.grassland.forestedge"), out var passage));
            Assert.AreEqual(ZoneState.Passage, zones.GetState(passage));
            rig.P.Mastery.AddXp(Grassland, 1_000_000);
            for (int k = 0; k < 6; k++)
                zones.TryOpen(zones.TryGetNext(Grassland, out var next) ? next.Id : default, out _);
            Assert.IsFalse(zones.TryGetNext(Grassland, out _), "Six zones to open in the Prairie.");
            Assert.IsFalse(zones.TryOpen(passage.Id, out _), "The passage is never bought.");
            Assert.AreEqual(Math.Pow(1.25, 6), zones.ProductionMultiplier(Grassland), 1e-12);
            zones.OpenPassage(Grassland);
            Assert.IsTrue(zones.IsOpen(passage), "The passage opens with the next biome.");
        }

        [Test]
        public void ZoneOpening_GivesItsGift_MasteryXp_AndRefugeScore()
        {
            var rig = Create();
            rig.State.Collection.Add(new AnimalInstance { Species = ContentId.FromSource("species.rabbit") });
            rig.Economy.RebuildAggregates();
            rig.P.Mastery.AddXp(Grassland, 40);
            rig.Give(250);
            long score = rig.P.Refuge.Score;
            long xp = rig.State.World.Biomes.Find(b => b.Biome == Grassland).MasteryXp;
            double rate = rig.Economy.GetLightPerSecond().ToDoubleClamped();
            Assert.IsTrue(rig.P.Zones.TryOpen(ContentId.FromSource("zone.grassland.meadow"), out var opening));
            Assert.AreEqual(rate * 600.0, opening.Gift.ToDoubleClamped(), 1e-6, "Ten minutes of production.");
            Assert.AreEqual(xp + 60, rig.State.World.Biomes.Find(b => b.Biome == Grassland).MasteryXp);
            Assert.AreEqual(score + 12, rig.P.Refuge.Score, "Zone 12 (its 60 XP stay inside mastery level 1).");
        }

        // ---------------- Biome gates (section 5) ----------------

        /// <summary>Four Prairie species, mastery 3, 31 minutes of play, 6 000 Light and a refuge at level 4.</summary>
        private static Rig ForestReady()
        {
            var rig = Create();
            foreach (var name in new[] { "rabbit", "squirrel", "hedgehog", "mole" })
                Discover(rig, name);
            rig.P.Mastery.AddXp(Grassland, 305);
            rig.State.Meta.TotalPlaySeconds = 31 * 60;
            rig.Give(6_000);
            ReachLevel(rig, 4);
            return rig;
        }

        /// <summary>The stored score never goes down: setting it reaches a level whatever the sources.</summary>
        private static void ReachLevel(Rig rig, int level)
        {
            rig.State.Progression.Refuge.Score = ProgressionMath.RefugeScoreForLevel(rig.Config, level);
            rig.P.Refuge.Recompute();
        }

        [Test]
        public void ForestGate_EveryConditionTogetherOpens_TheBiome()
        {
            var rig = ForestReady();
            var status = rig.P.Gates.GetStatus(Forest);
            Assert.GreaterOrEqual(rig.P.Refuge.Level, 4, $"score {rig.P.Refuge.Score}");
            Assert.AreEqual(GateConditions.All, status.Met);
            Assert.AreEqual(6_000.0, status.Price.ToDoubleClamped());
            long gems = rig.State.Wallet.Gems;
            Assert.IsTrue(rig.P.Gates.TryOpen(Forest, out var opening));
            Assert.IsTrue(rig.P.Store.IsUnlocked(Forest));
            Assert.AreEqual(gems + 50, rig.State.Wallet.Gems);
            Assert.IsTrue(rig.P.Zones.TryGet(ContentId.FromSource("zone.grassland.forestedge"), out var passage));
            Assert.IsTrue(rig.P.Zones.IsOpen(passage), "The Prairie's passage opens with the Forest.");
            Assert.AreEqual(0.0, rig.Economy.Light.ToDoubleClamped(), 0.5);
        }

        [TestCase(GateConditions.RefugeLevel)]
        [TestCase(GateConditions.Species)]
        [TestCase(GateConditions.Mastery)]
        [TestCase(GateConditions.Floor)]
        [TestCase(GateConditions.Light)]
        public void ForestGate_EachConditionAlone_Blocks(GateConditions missing)
        {
            var rig = Create();
            var names = missing == GateConditions.Species ? new[] { "rabbit", "squirrel", "hedgehog" } : new[] { "rabbit", "squirrel", "hedgehog", "mole" };
            foreach (var name in names)
                Discover(rig, name);
            if (missing != GateConditions.Mastery)
                rig.P.Mastery.AddXp(Grassland, 305);
            if (missing != GateConditions.Floor)
                rig.State.Meta.TotalPlaySeconds = 31 * 60;
            if (missing != GateConditions.Light)
                rig.Give(6_000);
            // Four species with one variant each and mastery 3 make 67 points: level 3, one short of the gate.
            if (missing != GateConditions.RefugeLevel)
                ReachLevel(rig, 4);
            else
                Assert.AreEqual(3, rig.P.Refuge.Level);
            var status = rig.P.Gates.GetStatus(Forest);
            Assert.AreEqual(GateConditions.All & ~missing, status.Met, $"{missing} alone missing");
            Assert.IsFalse(rig.P.Gates.TryOpen(Forest, out _));
            Assert.IsFalse(rig.P.Store.IsUnlocked(Forest));
        }

        [Test]
        public void LakeGate_WaitsForItsDay_WithAFakeClock()
        {
            var rig = ForestReady();
            Assert.IsTrue(rig.P.Gates.TryOpen(Forest, out _));
            Assert.AreEqual(2, rig.P.Gates.GetStatus(Lake).FloorValue);
            Assert.AreEqual(1, rig.P.Gates.GetStatus(Lake).FloorLeft, "Day 1: the Lake opens tomorrow at the earliest.");
            Assert.IsFalse((rig.P.Gates.GetStatus(Lake).Met & GateConditions.Floor) != 0);
            // The install day is day 1; the next local day after midnight is day 2.
            rig.Clock.Set(new DateTimeOffset(2026, 10, 5, 0, 1, 0, TimeSpan.Zero));
            Assert.AreEqual(0, rig.P.Gates.GetStatus(Lake).FloorLeft);
            Assert.IsTrue((rig.P.Gates.GetStatus(Lake).Met & GateConditions.Floor) != 0);
            Assert.IsFalse(rig.P.Gates.GetStatus(Lake).ContentAvailable, "No Lake species in this content: Coming soon.");
            // A clock moved back keeps the day already reached.
            rig.State.Offline.HighWaterUtc = rig.Clock.UtcNow;
            rig.Clock.Set(TestUtil.T0);
            Assert.AreEqual(0, rig.P.Gates.GetStatus(Lake).FloorLeft);
        }

        [Test]
        public void Gate_WithoutTheBiomesContent_IsComingSoon()
        {
            var rig = Create(Content(withForest: false));
            foreach (var name in new[] { "rabbit", "squirrel", "hedgehog", "mole" })
                Discover(rig, name);
            rig.P.Mastery.AddXp(Grassland, 305);
            rig.State.Meta.TotalPlaySeconds = 3600;
            ReachLevel(rig, 4);
            rig.Give(1e6);
            rig.P.Refuge.Recompute();
            var status = rig.P.Gates.GetStatus(Forest);
            Assert.IsFalse(status.ContentAvailable);
            Assert.IsFalse(status.CanOpen);
            Assert.IsFalse(rig.P.Gates.TryOpen(Forest, out _));
        }

        // ---------------- Refuge level (section 6) ----------------

        [TestCase(1, 0)]
        [TestCase(2, 20)]
        [TestCase(3, 55)]
        [TestCase(4, 98)]
        [TestCase(10, 484)]
        [TestCase(15, 918)]
        [TestCase(30, 2_639)]
        [TestCase(50, 5_647)]
        [TestCase(100, 15_657)]
        [TestCase(200, 43_089)]
        public void RefugeCurve_MatchesTheDocTable(int level, long score)
        {
            var config = new ProgressionConfig();
            Assert.AreEqual(score, ProgressionMath.RefugeScoreForLevel(config, level));
            Assert.AreEqual(level, ProgressionMath.RefugeLevelForScore(config, score));
            if (level > 1)
                Assert.AreEqual(level - 1, ProgressionMath.RefugeLevelForScore(config, score - 1));
        }

        [TestCase(1, 40, 40)]
        [TestCase(2, 98, 138)]
        [TestCase(5, 324, 872)]
        [TestCase(10, 798, 3_876)]
        [TestCase(20, 1_965, 18_079)]
        [TestCase(40, 4_839, 86_582)]
        public void BiomeMasteryCurve_MatchesTheDocTable(int level, long xpForLevel, long cumulative)
        {
            var config = new ProgressionConfig();
            Assert.AreEqual(xpForLevel, ProgressionMath.MasteryXpForLevel(config, level));
            Assert.AreEqual(level, ProgressionMath.MasteryLevelForXp(config, cumulative, out long left));
            Assert.AreEqual(0, left);
            Assert.AreEqual(level - 1, ProgressionMath.MasteryLevelForXp(config, cumulative - 1, out _));
        }

        [Test]
        public void RefugeScore_EachSourceCountsOnce_AndNeverGoesDown()
        {
            var rig = Create();
            Discover(rig, "rabbit", 2);
            Assert.AreEqual(10 + 3 + 3, rig.P.Refuge.Score);
            rig.P.Refuge.Recompute();
            Assert.AreEqual(16, rig.P.Refuge.Score, "Recomputing counts nothing twice.");
            rig.State.Collection.DiscoveredVariants.Add(ContentId.FromSource("variant.rabbit.brown"));
            rig.P.Refuge.Recompute();
            Assert.AreEqual(16, rig.P.Refuge.Score, "A variant found again is not new.");
            rig.P.Refuge.OnAnimalAdded(new AnimalInstance { Species = ContentId.FromSource("species.rabbit"), TraitBits = 0b101 });
            rig.P.Refuge.OnAnimalAdded(new AnimalInstance { Species = ContentId.FromSource("species.rabbit"), TraitBits = 0b001 });
            rig.P.Refuge.Recompute();
            Assert.AreEqual(16 + 2 * 5, rig.P.Refuge.Score, "Two traits found on the rabbit, each once.");
            // Even if the profile lost something (a removed animal), the score stays.
            rig.State.Collection.DiscoveredVariants.Clear();
            rig.P.Refuge.Recompute();
            Assert.AreEqual(26, rig.P.Refuge.Score);
            var breakdown = rig.P.Refuge.Breakdown();
            Assert.AreEqual(0, breakdown.Biomes, "The first biome is there from the start: no points.");
        }

        [Test]
        public void LevelRewards_ManyLevelsAtOnce_OneCard_ClaimedOnce()
        {
            var rig = Create();
            int levelUps = 0;
            rig.P.Refuge.LeveledUp += _ => levelUps++;
            foreach (var name in new[] { "rabbit", "squirrel", "hedgehog", "mole", "duckling" })
                Discover(rig, name, 3);
            rig.P.Mastery.AddXp(Grassland, 3_876);
            Assert.GreaterOrEqual(rig.P.Refuge.Level, 4);
            var pending = rig.P.Refuge.PendingRewards();
            Assert.AreEqual(1, pending.FromLevel);
            Assert.AreEqual(rig.P.Refuge.Level, pending.ToLevel);
            Assert.AreEqual(5L * (pending.ToLevel - 1), pending.Gems);
            long gems = rig.State.Wallet.Gems;
            Assert.IsTrue(rig.P.Refuge.TryClaimRewards(out var claimed));
            Assert.AreEqual(gems + claimed.Gems, rig.State.Wallet.Gems);
            Assert.IsFalse(rig.P.Refuge.TryClaimRewards(out _), "Rewards are given once.");
            Assert.Greater(levelUps, 0);
        }

        [Test]
        public void LevelEssence_IsKeptUntilTheFirstRenewal()
        {
            var rig = Create();
            rig.State.Progression.Refuge.Score = ProgressionMath.RefugeScoreForLevel(rig.Config, 20);
            rig.P.Refuge.Recompute();
            Assert.AreEqual(20, rig.P.Refuge.Level);
            Assert.IsTrue(rig.P.Refuge.TryClaimRewards(out var rewards));
            Assert.AreEqual(10, rewards.Essence, "Levels 10 and 20.");
            Assert.AreEqual(0, rig.State.Wallet.Essence);
            Assert.AreEqual(10, rig.State.Progression.Refuge.PendingEssenceFromLevels);
            Assert.AreEqual(10, rewards.Decorations, "One common decoration every two levels from 2.");
        }

        [Test]
        public void Features_OpenWithTheirLevelAndConditions()
        {
            var rig = Create();
            Assert.IsFalse(rig.P.Refuge.IsFeatureUnlocked(RefugeFeature.UpgradeButton), "Three hatches first.");
            for (int i = 0; i < 3; i++)
                rig.State.Collection.Add(new AnimalInstance { Species = ContentId.FromSource("species.rabbit") });
            Assert.IsTrue(rig.P.Refuge.IsFeatureUnlocked(RefugeFeature.UpgradeButton));
            Assert.IsFalse(rig.P.Refuge.IsFeatureUnlocked(RefugeFeature.ZoneSigns), "Level 2.");
            Discover(rig, "rabbit", 2);
            Assert.GreaterOrEqual(rig.P.Refuge.Level, 1);
            rig.State.Progression.Refuge.Score = 20;
            rig.P.Refuge.Recompute();
            Assert.IsTrue(rig.P.Refuge.IsFeatureUnlocked(RefugeFeature.ZoneSigns));
        }

        // ---------------- Decorations and treats (sections 8 and 9) ----------------

        [Test]
        public void CommonDecorations_UnlockByLevel_PriceGrowsPerCopy_TenAtMost()
        {
            var rig = Create();
            var bench = ContentId.FromSource("decor.bench_cute");
            rig.Give(1e9);
            Assert.IsFalse(rig.P.Decorations.TryBuy(bench, out _), "The Cute Bench comes with level 2.");
            rig.State.Progression.Refuge.Score = 20;
            rig.P.Refuge.Recompute();
            Assert.AreEqual(300.0, rig.P.Decorations.NextPrice(bench).ToDoubleClamped(), "30 x B of the Prairie.");
            for (int i = 0; i < 10; i++)
                Assert.IsTrue(rig.P.Decorations.TryBuy(bench, out _), $"copy {i + 1}");
            Assert.IsFalse(rig.P.Decorations.TryBuy(bench, out _), "Ten copies at most.");
            Assert.AreEqual(10, rig.P.Decorations.Copies(bench));
        }

        [Test]
        public void TreatStand_ThreeADay_RefillsAtLocalMidnight()
        {
            var rig = Create();
            rig.State.Collection.Add(new AnimalInstance { Species = ContentId.FromSource("species.rabbit") });
            rig.Economy.RebuildAggregates();
            rig.Give(1e6);
            rig.P.Treats.Refresh(Grassland);
            var clover = ContentId.FromSource("treat.clover_crunch");
            Assert.AreEqual(clover, rig.P.Treats.GetSlot(0));
            Assert.AreEqual(clover, rig.P.Treats.GetSlot(1), "A single biome open: Clover Crunch.");
            Assert.AreEqual(3, rig.P.Treats.Left);
            Assert.AreEqual(rig.Economy.GetLightPerSecond().ToDoubleClamped() * 120.0, rig.P.Treats.Price().ToDoubleClamped(), 1.0);
            Assert.IsTrue(rig.P.Treats.TryBuy(0, out _));
            Assert.IsFalse(rig.P.Treats.TryBuy(0, out _), "Each slot once a day.");
            Assert.AreEqual(2, rig.P.Treats.Left);
            Assert.AreEqual(1, rig.P.Treats.Owned(clover));
            rig.Clock.Advance(TimeSpan.FromHours(6));
            rig.P.Treats.Refresh(Grassland);
            Assert.AreEqual(2, rig.P.Treats.Left, "Same day: nothing refilled.");
            rig.Clock.Set(new DateTimeOffset(2026, 10, 5, 0, 0, 1, TimeSpan.Zero));
            rig.P.Treats.Refresh(Grassland);
            Assert.AreEqual(3, rig.P.Treats.Left, "Refilled at midnight.");
        }

        // ---------------- Renewal, economy chain, allocations ----------------

        [Test]
        public void Renewal_ResetsProductionAndNests_KeepsCuriosityZonesMasteryAndScore()
        {
            var rig = Create();
            rig.P.Mastery.AddXp(Grassland, 1_000_000);
            rig.Give(1e12);
            rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Production, PurchaseQty.Ten, out _);
            rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Nests, PurchaseQty.Ten, out _);
            rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Curiosity, PurchaseQty.Ten, out _);
            rig.P.Zones.TryOpen(ContentId.FromSource("zone.grassland.meadow"), out _);
            int mastery = rig.P.Mastery.GetLevel(Grassland);
            long score = rig.P.Refuge.Score;
            rig.P.Upgrades.OnRenewal();
            Assert.AreEqual(0, rig.P.Upgrades.GetLevel(Grassland, UpgradeKind.Production));
            Assert.AreEqual(0, rig.P.Upgrades.GetLevel(Grassland, UpgradeKind.Nests));
            Assert.AreEqual(10, rig.P.Upgrades.GetLevel(Grassland, UpgradeKind.Curiosity));
            Assert.AreEqual(1, rig.P.Zones.ExpansionsOpen(Grassland));
            Assert.AreEqual(mastery, rig.P.Mastery.GetLevel(Grassland));
            Assert.AreEqual(score, rig.P.Refuge.Score);
        }

        [Test]
        public void Production_MultipliesUpgradeZonesAndBiomeMastery()
        {
            var rig = Create();
            rig.State.Collection.Add(new AnimalInstance { Species = ContentId.FromSource("species.rabbit"), Rarity = RarityTier.Common });
            rig.Economy.RebuildAggregates();
            double baseRate = rig.Economy.GetLightPerSecond().ToDoubleClamped();
            rig.P.Mastery.AddXp(Grassland, 3_876); // level 10: x 1.20
            rig.Give(1e9);
            rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Production, PurchaseQty.Ten, out _); // x 2.0
            rig.P.Zones.TryOpen(ContentId.FromSource("zone.grassland.meadow"), out _); // x 1.25
            Assert.AreEqual(baseRate * 1.20 * 2.0 * 1.25, rig.Economy.GetLightPerSecond().ToDoubleClamped(), 1e-9);
        }

        [Test]
        public void HatchingGivesBiomeMasteryXp_AndScore()
        {
            var rig = Create();
            var result = rig.Hatch.Hatch(Basic);
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(52, rig.State.World.Biomes.Find(b => b.Biome == Grassland).MasteryXp, "A new species: 40 + its first variant 12.");
            Assert.AreEqual(18, rig.P.Refuge.Score, "Species 10 + variant 3 + Prairie mastery level 1 (5).");
            rig.Give(1e6);
            rig.Hatch.Hatch(Basic);
            Assert.GreaterOrEqual(rig.State.World.Biomes.Find(b => b.Biome == Grassland).MasteryXp, 54);
        }

        [Test]
        public void BuyingAndRecomputingProduction_DoNotAllocate()
        {
            var rig = Create();
            rig.State.Collection.Add(new AnimalInstance { Species = ContentId.FromSource("species.rabbit") });
            rig.Economy.RebuildAggregates();
            rig.P.Mastery.AddXp(Grassland, 1_000_000);
            rig.Give(1e40);
            // Warm the paths once.
            rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Production, PurchaseQty.One, out _);
            rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Nests, PurchaseQty.One, out _);
            rig.Economy.GetLightPerSecond();
            Assert.That(() =>
            {
                for (int i = 0; i < 200; i++)
                {
                    rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Production, i % 3 == 0 ? PurchaseQty.Max : PurchaseQty.One, out _);
                    rig.P.Upgrades.Quote(Grassland, UpgradeKind.Nests, PurchaseQty.Ten);
                    rig.Economy.MarkProductionDirty();
                    rig.Economy.GetLightPerSecond();
                    rig.Hatch.GetCost(Basic);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void RemoteConfig_KeepsValuesWithinTheirBounds()
        {
            var config = new ProgressionConfig();
            Assert.IsTrue(config.TryOverride("econ.upgrade.production.growth", 1.3));
            Assert.AreEqual(1.3, config.ProductionGrowth);
            Assert.IsFalse(config.TryOverride("econ.upgrade.production.growth", 1.9), "Out of bounds: ignored.");
            Assert.AreEqual(1.3, config.ProductionGrowth);
            Assert.IsFalse(config.TryOverride("econ.unknown", 1.0));
            Assert.IsFalse(config.TryOverride("econ.zone.growth", double.NaN));
        }
    }
}
