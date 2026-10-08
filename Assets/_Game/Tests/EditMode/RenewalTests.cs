using System;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T35C, doc « 02 — Renouveau, Essence, maîtrises et liens », section 8: nothing of the « Tu gardes » column changes
    /// (whole state compared before and after), two Renewals in a row only give the bonus, the formula's exact gains,
    /// free redistribution only during a Renewal; the tree's branch conditions, costs, caps, no purchase without Essence,
    /// and each effect reaching its system.
    /// </summary>
    public class RenewalTests
    {
        private static readonly ContentId Grassland = ContentId.FromSource("biome.grassland");
        private static readonly ContentId Forest = ContentId.FromSource("biome.forest");
        private static readonly ContentId Basic = ContentId.FromSource("egg.grassland.basic");
        private static readonly ContentId SoftLight = ContentId.FromSource("essence.economy.soft_light");
        private static readonly ContentId MorningEggs = ContentId.FromSource("essence.economy.morning_eggs");
        private static readonly ContentId Restart = ContentId.FromSource("essence.economy.restart");
        private static readonly ContentId EasyZones = ContentId.FromSource("essence.economy.easy_zones");
        private static readonly ContentId GrandRefuge = ContentId.FromSource("essence.economy.grand_refuge");
        private static readonly ContentId KeenNose = ContentId.FromSource("essence.discovery.flair");
        private static readonly ContentId FastMastery = ContentId.FromSource("essence.discovery.fast_mastery");
        private static readonly ContentId LongNap = ContentId.FromSource("essence.offline.long_nap");
        private static readonly ContentId GentleWake = ContentId.FromSource("essence.offline.soft_wake");
        private static readonly ContentId Roots = ContentId.FromSource("essence.habitat.biome_mastery");
        private static readonly ContentId RoomForAll = ContentId.FromSource("essence.habitat.more_room");

        private sealed class Rig
        {
            public PlayerState State;
            public FakeGameClock Clock;
            public EconomyService Economy;
            public MasteryService SpeciesMastery;
            public HatchService Hatch;
            public OfflineService Offline;
            public ProgressionServices P;

            public void Give(double light) => Economy.Grant(CurrencyId.Light, BigValue.FromDouble(light), EconomyReason.Debug);

            public void GiveEssence(long essence) => Economy.Grant(CurrencyId.Essence, BigValue.FromDouble(essence), EconomyReason.Debug);

            /// <summary>Refuge level 15 on day 6: the Renewal shows as a goal.</summary>
            public void MakeVisible()
            {
                State.Progression.Refuge.Level = Math.Max(15, State.Progression.Refuge.Level);
                State.Progression.Refuge.Score = Math.Max(State.Progression.Refuge.Score, P.Refuge.ScoreForLevel(15));
                Clock.Set(TestUtil.T0 + TimeSpan.FromDays(5));
            }
        }

        private static ContentRegistry Content()
        {
            var builder = new ContentRegistryBuilder();
            foreach (var name in new[] { "rabbit", "squirrel", "hedgehog", "mole", "duckling" })
                AddSpecies(builder, name, Grassland);
            AddSpecies(builder, "fawn", Forest);
            builder.AddEgg(new EggDefinition(Basic, "egg.grassland.basic.name", Grassland, EggTier.Basic,
                new Color32[EggDefinition.PaletteSize], 15, 0, 15, "PF_Egg_Base", 10.0, 1.18,
                new[] { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 },
                new[]
                {
                    new EggDropEntry(ContentId.FromSource("species.rabbit"), 1.0), new EggDropEntry(ContentId.FromSource("species.squirrel"), 1.0),
                    new EggDropEntry(ContentId.FromSource("species.hedgehog"), 1.0),
                }),
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

        private static Rig Create(PlayerState state = null)
        {
            var config = new ProgressionConfig { TimeZone = TimeZoneInfo.Utc };
            var content = Content();
            var rig = new Rig { State = state ?? PlayerState.CreateNew(TestUtil.T0), Clock = new FakeGameClock(TestUtil.T0) };
            rig.Economy = new EconomyService(rig.State, content, EconomyConfig.Default, null);
            rig.SpeciesMastery = new MasteryService(rig.State, rig.Economy.Config, rig.Economy);
            rig.Hatch = new HatchService(rig.State, content, rig.Economy, rig.Clock, null, rig.SpeciesMastery);
            rig.Offline = new OfflineService(rig.State, rig.Economy, rig.Clock);
            rig.P = new ProgressionServices(rig.State, content, rig.Economy, rig.Hatch, rig.Clock, null, config, rig.SpeciesMastery, rig.Offline);
            return rig;
        }

        private static byte[] Bytes(PlayerState state)
        {
            var writer = new ByteBufferWriter();
            new BinarySaveSerializer().Serialize(state, writer);
            return writer.WrittenSpan.ToArray();
        }

        private static PlayerState Copy(PlayerState state)
        {
            var serializer = new BinarySaveSerializer();
            var bytes = Bytes(state);
            return serializer.Deserialize(serializer.ReadDocument(bytes, bytes.Length));
        }

        // ---------------- Formula (section 1.3) ----------------

        [TestCase(0.0, 0L)]
        [TestCase(5e7, 0L)]
        [TestCase(1e8, 1L)]
        [TestCase(1e10, 6L)]
        [TestCase(1e12, 47L)]
        [TestCase(1e15, 870L)]
        public void EssenceForLight_IsTheDocFormula(double lifetime, long expected)
        {
            var rig = Create();
            Assert.AreEqual(expected, rig.P.Renewal.EssenceForLight(BigValue.FromDouble(lifetime)),
                "floor((LT / 1e8)^0.42)");
        }

        [Test]
        public void FirstRenewal_GivesLightEssence_Completion_PendingLevels_AndTheGift()
        {
            var rig = Create();
            rig.Give(1e12);
            for (int i = 0; i < 3; i++)
                rig.State.Collection.DiscoveredSpecies.Add(ContentId.FromSource(new[] { "species.rabbit", "species.squirrel", "species.hedgehog" }[i]));
            rig.State.Progression.Refuge.PendingEssenceFromLevels = 5;
            rig.MakeVisible();
            var preview = rig.P.Renewal.Preview();
            Assert.AreEqual(47, preview.FromLight);
            Assert.AreEqual(3, preview.FromSpecies, "+1 per species found.");
            Assert.AreEqual(5, preview.FromBiomes, "+5 for the open Prairie.");
            Assert.AreEqual(5, preview.Pending, "Essence of the levels waits for the first Renewal.");
            Assert.AreEqual(20, preview.Gift, "The first Renewal's gift.");
            Assert.IsTrue(rig.P.Renewal.CanRenew());
            Assert.IsTrue(rig.P.Renewal.TryRenew(out var done));
            Assert.AreEqual(1, done.Count);
            Assert.AreEqual(47 + 3 + 5 + 5 + 20, rig.State.Wallet.Essence);
            Assert.AreEqual(rig.State.Wallet.Essence, rig.State.Progression.Renewal.EssenceEarned);
            Assert.AreEqual(0, rig.State.Progression.Refuge.PendingEssenceFromLevels);
            Assert.IsTrue(rig.State.Wallet.Light.IsZero, "The Light in the wallet starts over.");
        }

        [Test]
        public void TwoRenewalsInARow_TheSecondOnlyGivesTheCompletionBonus()
        {
            var rig = Create();
            rig.Give(1e12);
            rig.MakeVisible();
            Assert.IsTrue(rig.P.Renewal.TryRenew(out _));
            var again = rig.P.Renewal.Preview();
            Assert.AreEqual(0, again.FromLight, "The lifetime Light already gave its Essence.");
            Assert.AreEqual(0, again.Gift);
            Assert.AreEqual(again.Completion, again.Earned, "Only the completion bonus is left.");
            Assert.IsFalse(rig.P.Renewal.CanRenew(), "Below 15: the button waits.");

            // New discoveries since: they are the bonus of the next Renewal, nothing more.
            foreach (var name in new[] { "rabbit", "squirrel", "hedgehog", "mole", "duckling" })
                rig.State.Collection.DiscoveredSpecies.Add(ContentId.FromSource("species." + name));
            Assert.AreEqual(5, rig.P.Renewal.Preview().Earned);
        }

        [Test]
        public void Button_NeedsVisibility_FifteenEssence_ThenTenPercentOfAllEarned()
        {
            var rig = Create();
            rig.Give(1e12);
            Assert.IsFalse(rig.P.Renewal.IsVisible, "Refuge level 1 on day 1.");
            Assert.IsFalse(rig.P.Renewal.CanRenew());
            rig.State.Progression.Refuge.Level = 15;
            Assert.IsFalse(rig.P.Renewal.IsVisible, "Day 1: not before day 6.");
            rig.Clock.Set(TestUtil.T0 + TimeSpan.FromDays(5));
            Assert.IsTrue(rig.P.Renewal.IsVisible);
            Assert.IsTrue(rig.P.Renewal.CanRenew());

            var small = Create();
            small.Give(1e9); // E_light 2, Prairie bonus 5: 7 < 15
            small.MakeVisible();
            Assert.Less(small.P.Renewal.Preview().Earned, 15);
            Assert.IsFalse(small.P.Renewal.CanRenew(), "A gain under 15 never renews.");

            // After a Renewal: the gain must also reach 10 % of all Essence earned.
            Assert.IsTrue(rig.P.Renewal.TryRenew(out _));
            rig.State.Progression.Renewal.EssenceEarned = 1000;
            rig.Give(5e14); // E_light(LT) - 47 ≈ 643 - 47
            var preview = rig.P.Renewal.Preview();
            Assert.GreaterOrEqual(preview.Earned, 15);
            Assert.AreEqual(preview.Earned >= 100, rig.P.Renewal.CanRenew(), "At least 10 % of the 1 000 earned.");
        }

        // ---------------- What stays and what starts over (section 1.4) ----------------

        [Test]
        public void Renewal_KeepsEverythingOfTheKeptColumn_WholeStateCompared()
        {
            var rig = Create();
            rig.Give(1e13);
            rig.P.Mastery.AddXp(Grassland, 2_000_000);
            for (int i = 0; i < 12; i++)
                rig.Hatch.Hatch(Basic);
            rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Production, PurchaseQty.Ten, out _);
            rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Nests, PurchaseQty.Ten, out _);
            rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Curiosity, PurchaseQty.Ten, out _);
            rig.P.Zones.TryOpen(ContentId.FromSource("zone.grassland.meadow"), out _);
            rig.State.Collection.CustomNames[rig.State.Collection[0].InstanceId] = "Pompon";
            rig.State.World.DiscoveryEggs = 2;
            rig.State.Wallet.Gems = 345;
            rig.State.Progression.DecorationCopies[ContentId.FromSource("decor.bench_cute")] = 2;
            rig.State.Progression.Treats[ContentId.FromSource("treat.clover_crunch")] = 4;
            rig.GiveEssence(30);
            Assert.IsTrue(rig.P.Essence.TryBuy(SoftLight, out _));
            rig.MakeVisible();

            var preview = rig.P.Renewal.Preview();
            var expected = Copy(rig.State);
            Assert.IsTrue(rig.P.Renewal.TryRenew(out var done));
            Assert.AreEqual(preview.Total, done.Gain.Total);

            // « Ça repart »: Light, Production, Cozy Nests, the egg counters; then the Renewal's memory and the Essence.
            expected.Wallet.Light = BigValue.Zero;
            for (int i = 0; i < expected.World.Biomes.Count; i++)
            {
                var biome = expected.World.Biomes[i];
                biome.ProductionLevel = 0;
                biome.NestsLevel = 0;
                expected.World.Biomes[i] = biome;
            }
            expected.World.EggPurchasesInCycle.Clear();
            expected.World.RenewalCount++;
            expected.Wallet.Essence += preview.Total;
            var memory = expected.Progression.Renewal;
            memory.EssenceFromLight = rig.P.Renewal.EssenceForLight(expected.Wallet.LifetimeLight);
            memory.SpeciesAtLast = rig.State.Progression.Renewal.SpeciesAtLast;
            memory.BiomesAtLast = 1;
            memory.MasteredAtLast = 0;
            memory.EssenceEarned += preview.Total;
            expected.Progression.Refuge.PendingEssenceFromLevels = 0;
            expected.Progression.Refuge.Score = rig.State.Progression.Refuge.Score;
            expected.Progression.Refuge.Level = rig.State.Progression.Refuge.Level;

            CollectionAssert.AreEqual(Bytes(expected), Bytes(rig.State),
                "Animals, names, Codex, masteries, biomes, zones, Curious Eye, decorations, Gems, Essence tree, refuge level, treats and " +
                "Discovery eggs are exactly as they were.");
            Assert.AreEqual(10, rig.P.Upgrades.GetLevel(Grassland, UpgradeKind.Curiosity), "Curious Eye stays.");
            Assert.AreEqual(1, rig.P.Zones.ExpansionsOpen(Grassland), "Zones stay.");
            Assert.AreEqual(1, rig.P.Essence.GetRank(SoftLight), "The tree stays.");
        }

        [Test]
        public void FreshStart_ProductionOfEachOpenBiomeStartsAtFiveLevelsPerRank()
        {
            var rig = Create();
            rig.Give(1e12);
            rig.GiveEssence(500);
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(rig.P.Essence.TryBuy(SoftLight, out _), "Five Soft Light ranks open the middle nodes.");
            Assert.IsTrue(rig.P.Essence.TryBuy(Restart, out _));
            Assert.IsTrue(rig.P.Essence.TryBuy(Restart, out _));
            rig.MakeVisible();
            Assert.IsTrue(rig.P.Renewal.TryRenew(out _));
            Assert.AreEqual(10, rig.P.Upgrades.GetLevel(Grassland, UpgradeKind.Production), "2 ranks × 5 levels.");
            Assert.AreEqual(0, rig.P.Upgrades.GetLevel(Forest, UpgradeKind.Production), "Only open biomes.");
        }

        // ---------------- Redistribution (section 1.5) ----------------

        [Test]
        public void Redistribution_IsFree_OnlyWhileARenewalIsPrepared()
        {
            var rig = Create();
            rig.Give(1e12);
            rig.GiveEssence(40);
            for (int i = 0; i < 6; i++)
                Assert.IsTrue(rig.P.Essence.TryBuy(SoftLight, out _));
            long spent = 1 + 2 + 3 + 4 + 5 + 6;
            Assert.AreEqual(40 - spent, rig.State.Wallet.Essence);
            Assert.IsFalse(rig.P.Essence.RefundAll(), "Never outside a Renewal.");
            Assert.AreEqual(6, rig.P.Essence.GetRank(SoftLight));

            Assert.IsFalse(rig.P.Renewal.BeginRedistribution(), "Not visible yet: no Renewal to prepare.");
            rig.MakeVisible();
            Assert.IsTrue(rig.P.Renewal.BeginRedistribution());
            Assert.IsTrue(rig.P.Essence.RefundAll());
            Assert.AreEqual(40, rig.State.Wallet.Essence, "Every rank refunded, nothing lost.");
            Assert.AreEqual(0, rig.P.Essence.GetRank(SoftLight));
            Assert.IsTrue(rig.P.Renewal.TryRenew(out _));
            Assert.IsFalse(rig.P.Essence.RedistributionOpen, "Closed with the Renewal.");
            Assert.IsFalse(rig.P.Essence.RefundAll());
        }

        // ---------------- The tree (section 2) ----------------

        [Test]
        public void Catalog_ThirtyNodes_TheDocCosts()
        {
            Assert.AreEqual(30, EssenceCatalog.Nodes.Length);
            Assert.AreEqual(1587, EssenceCatalog.TotalCost(), "Doc 2.7: 1 587 Essence buy everything.");
            long[] perBranch = new long[5];
            int[] starts = new int[5];
            foreach (var node in EssenceCatalog.Nodes)
            {
                perBranch[(int)node.Branch] += node.TotalCost(node.Ranks);
                if (node.RequiredPoints == 0)
                    starts[(int)node.Branch]++;
            }
            CollectionAssert.AreEqual(new long[] { 775, 288, 197, 134, 193 }, perBranch, "Economy, Discovery, Offline, Habitat, Breeding.");
            CollectionAssert.AreEqual(new[] { 2, 2, 2, 2, 2 }, starts, "Two start nodes per branch.");
        }

        [Test]
        public void Tree_BranchConditions_Costs_Caps_AndNothingWithoutEssence()
        {
            var rig = Create();
            Assert.IsTrue(EssenceCatalog.TryGet(SoftLight, out var soft));
            Assert.IsTrue(EssenceCatalog.TryGet(Restart, out var restart));
            Assert.IsTrue(EssenceCatalog.TryGet(GrandRefuge, out var grand));
            Assert.IsFalse(rig.P.Essence.CanBuy(soft), "No Essence, no node.");
            Assert.IsFalse(rig.P.Essence.TryBuy(SoftLight, out _));

            rig.GiveEssence(2000);
            Assert.AreEqual(1, rig.P.Essence.NextCost(soft), "Rank 1 costs c × 1.");
            Assert.IsFalse(rig.P.Essence.IsOpen(restart), "Fresh Start waits for 5 points in the branch.");
            for (int i = 0; i < 4; i++)
                rig.P.Essence.TryBuy(SoftLight, out _);
            Assert.AreEqual(5, rig.P.Essence.NextCost(soft), "Rank 5 costs c × 5.");
            Assert.IsFalse(rig.P.Essence.CanBuy(restart));
            Assert.IsTrue(rig.P.Essence.TryBuy(MorningEggs, out var eggs), "A start node of the same branch counts too.");
            Assert.AreEqual(2, eggs.Cost);
            Assert.IsTrue(rig.P.Essence.CanBuy(restart), "5 points: the middle nodes open.");
            Assert.IsFalse(rig.P.Essence.IsOpen(grand), "Grand Refuge waits for 15 points.");
            for (int i = 0; i < 10; i++)
                rig.P.Essence.TryBuy(SoftLight, out _);
            Assert.IsTrue(rig.P.Essence.IsOpen(grand));
            Assert.IsTrue(rig.P.Essence.TryBuy(GrandRefuge, out var big));
            Assert.AreEqual(25, big.Cost);
            Assert.IsFalse(rig.P.Essence.CanBuy(grand), "One rank only.");
            while (rig.P.Essence.TryBuy(SoftLight, out _))
            {
            }
            Assert.AreEqual(25, rig.P.Essence.GetRank(SoftLight), "Capped at 25 ranks.");
            Assert.AreEqual(0, rig.P.Essence.NextCost(soft));
            Assert.AreEqual(0, rig.P.Essence.BranchPoints(EssenceBranch.Discovery), "Points count per branch.");
        }

        [Test]
        public void Effects_ReachTheirSystems()
        {
            var rig = Create();
            rig.Give(1e6);
            for (int i = 0; i < 3; i++)
                rig.Hatch.Hatch(Basic);
            double rate = rig.Economy.GetLightPerSecond().ToDoubleClamped();
            double egg = rig.Hatch.GetCost(Basic).ToDoubleClamped();
            var meadow = ContentId.FromSource("zone.grassland.meadow");
            rig.P.Zones.TryGet(meadow, out var zone);
            double zonePrice = rig.P.Zones.Price(zone).ToDoubleClamped();
            double gatePrice = rig.P.Gates.GetStatus(Forest).Price.ToDoubleClamped();

            rig.GiveEssence(5000);
            for (int i = 0; i < 5; i++)
                rig.P.Essence.TryBuy(SoftLight, out _);
            Assert.AreEqual(rate * 1.5, rig.Economy.GetLightPerSecond().ToDoubleClamped(), rate * 1e-6, "Soft Light: +10 % per rank.");
            for (int i = 0; i < 10; i++)
                rig.P.Essence.TryBuy(SoftLight, out _);
            rig.P.Essence.TryBuy(GrandRefuge, out _);
            Assert.AreEqual(rate * 2.5 * 1.5, rig.Economy.GetLightPerSecond().ToDoubleClamped(), rate * 1e-6, "Grand Refuge: × 1.5.");

            rig.P.Essence.TryBuy(MorningEggs, out _);
            rig.P.Essence.TryBuy(MorningEggs, out _);
            Assert.AreEqual(Math.Round(egg * 0.97 * 0.97), rig.Hatch.GetCost(Basic).ToDoubleClamped(), 1.0, "Morning Eggs: × 0.97 per rank.");

            rig.P.Essence.TryBuy(EasyZones, out _);
            Assert.AreEqual(Math.Round(zonePrice * 0.95), rig.P.Zones.Price(zone).ToDoubleClamped(), 1.0, "Easy Spaces: zones × 0.95.");
            Assert.AreEqual(Math.Round(gatePrice * 0.95), rig.P.Gates.GetStatus(Forest).Price.ToDoubleClamped(), 1.0, "And gates.");

            for (int i = 0; i < 5; i++)
                rig.P.Essence.TryBuy(KeenNose, out _); // Discovery points for Fast Mastery later
            rig.P.Essence.TryBuy(Roots, out _); // needs 5 Habitat points: not open yet
            Assert.AreEqual(0, rig.P.Essence.GetRank(Roots));
            for (int i = 0; i < 5; i++)
                rig.P.Essence.TryBuy(RoomForAll, out _);
            Assert.AreEqual(10, rig.P.Essence.ExtraVisibleAnimals, "Room for All: +2 visible animals per rank.");
            Assert.IsTrue(rig.P.Essence.TryBuy(Roots, out _));
            var gain = rig.P.Mastery.AddXp(Grassland, 100);
            Assert.AreEqual(110, gain.Xp, "Roots: +10 % biome mastery XP.");

            Assert.AreEqual(0.005 * 4, rig.Hatch.SpeciesPityBonusPerMiss, 1e-12, "Keen Nose: +0.5 point per miss (4 ranks at most).");
            Assert.AreEqual(Math.Min(0.35, (0.03 + 0.02) * 5), rig.Hatch.SpeciesBonus(5), 1e-12);

            for (int i = 0; i < 6; i++)
                rig.P.Essence.TryBuy(LongNap, out _);
            for (int i = 0; i < 5; i++)
                rig.P.Essence.TryBuy(GentleWake, out _);
            Assert.AreEqual(12 * 3600.0, rig.Offline.ExtraFullSeconds, 1e-6, "Long Nap: +2 h per rank, 24 h becomes 36 h.");
            Assert.AreEqual(0.25, rig.Offline.FloorBonus, 1e-9, "Gentle Wake: 25 % becomes 50 %.");
            double day = 36 * 3600.0;
            Assert.AreEqual(day, OfflineService.EffectiveSeconds(day, rig.Economy.Config, rig.Offline.ExtraFullSeconds, rig.Offline.FloorBonus), 1e-6,
                "36 h away are paid in full.");

            // Fast Mastery needs 15 Discovery points: Keen Nose 4 + Color Eye 5 + Rare Traits 6.
            for (int i = 0; i < 5; i++)
                rig.P.Essence.TryBuy(ContentId.FromSource("essence.discovery.variant_eye"), out _);
            for (int i = 0; i < 6; i++)
                rig.P.Essence.TryBuy(ContentId.FromSource("essence.discovery.traits"), out _);
            Assert.IsTrue(rig.P.Essence.TryBuy(FastMastery, out _));
            var species = rig.SpeciesMastery.AddXp(ContentId.FromSource("species.rabbit"), MasteryReason.Duplicate, 4);
            Assert.AreEqual(5, species.Xp, "Fast Mastery: +25 % species XP.");
        }
    }
}
