using System;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T51 decisions 5 and 6: a biome whose definition is not open to players stays « Bientôt » at its gate even with species in
    /// the content (the Forest until it is complete; saves still open it for tests, captures and benchmarks), and a season visitor
    /// comes only once its species' biome is open for the player (the golden fox waits for the Forest).
    /// </summary>
    public class ForestOpeningTests
    {
        private static readonly ContentId Grassland = ContentId.FromSource("biome.grassland");
        private static readonly ContentId Forest = ContentId.FromSource("biome.forest");

        private static ContentRegistry Content(bool forestOpen)
        {
            var builder = new ContentRegistryBuilder();
            builder.AddBiome(new BiomeDefinition(Grassland, "biome.grassland.name", 0, "grassland", "GL", null), "biome.grassland");
            builder.AddBiome(new BiomeDefinition(Forest, "biome.forest.name", 1, "forest", "FO", null, forestOpen), "biome.forest");
            foreach (var name in new[] { "rabbit", "squirrel", "hedgehog", "mole", "fieldmouse" })
                AddSpecies(builder, name, Grassland, RigFamily.SmallQuadruped);
            AddSpecies(builder, "fox", Forest, RigFamily.MediumQuadruped);
            AddSpecies(builder, "badger", Forest, RigFamily.MediumQuadruped);
            Variant(builder, "variant.rabbit.meadow_visitor", "rabbit", true);
            Variant(builder, "variant.fox.golden_visitor", "fox", true);
            Variant(builder, "variant.badger.amber_visitor", "badger", true);
            builder.AddEgg(new EggDefinition(ContentId.FromSource("egg.grassland.basic"), "egg.grassland.basic.name", Grassland, EggTier.Basic,
                new Color32[EggDefinition.PaletteSize], 15, 0, 15, "PF_Egg_Base", 10.0, 1.18,
                new[] { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 }, new[] { new EggDropEntry(ContentId.FromSource("species.rabbit"), 1.0) }),
                "egg.grassland.basic");
            var registry = builder.Build();
            Assert.IsFalse(builder.HasErrors, string.Join("; ", builder.Issues));
            return registry;
        }

        private static void AddSpecies(ContentRegistryBuilder builder, string name, ContentId biome, RigFamily family)
        {
            var id = "species." + name;
            builder.AddSpecies(new SpeciesDefinition(ContentId.FromSource(id), id + ".name", family, biome, 1.0, "PF_Animal_Test"), id);
            foreach (var (color, rarity) in new[] { ("brown", RarityTier.Common), ("white", RarityTier.Common), ("spotted", RarityTier.Rare) })
                Variant(builder, $"variant.{name}.{color}", name, false, rarity);
        }

        private static void Variant(ContentRegistryBuilder builder, string id, string species, bool visitor, RarityTier rarity = RarityTier.Rare)
        {
            builder.AddVariant(new VariantDefinition(ContentId.FromSource(id), ContentId.FromSource("species." + species), id + ".name",
                rarity, new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0, isSecret: visitor, isVisitor: visitor), id);
        }

        private sealed class Rig
        {
            public PlayerState State;
            public EconomyService Economy;
            public ProgressionServices P;
        }

        private static Rig Create(ContentRegistry content)
        {
            var config = new ProgressionConfig { TimeZone = TimeZoneInfo.Utc };
            var rig = new Rig { State = PlayerState.CreateNew(TestUtil.T0) };
            var clock = new FakeGameClock(TestUtil.T0);
            rig.Economy = new EconomyService(rig.State, content, EconomyConfig.Default, null);
            var mastery = new MasteryService(rig.State, rig.Economy.Config, rig.Economy);
            var hatch = new HatchService(rig.State, content, rig.Economy, clock, null, mastery);
            rig.P = new ProgressionServices(rig.State, content, rig.Economy, hatch, clock, null, config);
            return rig;
        }

        /// <summary>Every condition of the Forest gate met (four Prairie species, mastery 3, play time, refuge level 4, Light).</summary>
        private static Rig ForestReady(bool forestOpen)
        {
            var rig = Create(Content(forestOpen));
            foreach (var name in new[] { "rabbit", "squirrel", "hedgehog", "mole" })
            {
                rig.State.Collection.DiscoveredSpecies.Add(ContentId.FromSource("species." + name));
                rig.State.Collection.DiscoveredVariants.Add(ContentId.FromSource("variant." + name + ".brown"));
            }
            rig.P.Mastery.AddXp(Grassland, 305);
            rig.State.Meta.TotalPlaySeconds = 3600;
            rig.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(1e6), EconomyReason.Debug);
            rig.State.Progression.Refuge.Score = ProgressionMath.RefugeScoreForLevel(rig.P.Config, 4);
            rig.P.Refuge.Recompute();
            return rig;
        }

        [Test]
        public void ForestGate_ClosedToPlayers_IsComingSoon_EvenWithEveryConditionMet()
        {
            var rig = ForestReady(forestOpen: false);
            var status = rig.P.Gates.GetStatus(Forest);
            Assert.IsFalse(rig.P.Gates.IsContentAvailable(Forest), "Forest species are in the content, but the biome is not open to players.");
            Assert.IsFalse(status.ContentAvailable);
            Assert.AreEqual(GateConditions.All, status.Met, "every condition is met: only the open flag holds it");
            Assert.IsFalse(status.Ready);
            Assert.IsFalse(status.CanOpen);
            Assert.IsFalse(rig.P.Gates.TryOpen(Forest, out _));
            Assert.IsFalse(rig.P.Store.IsUnlocked(Forest));
            Assert.IsTrue(rig.P.Gates.IsContentAvailable(Grassland));
        }

        [Test]
        public void ForestGate_OpenToPlayers_OpensAsBefore()
        {
            var rig = ForestReady(forestOpen: true);
            Assert.IsTrue(rig.P.Gates.GetStatus(Forest).ContentAvailable);
            Assert.IsTrue(rig.P.Gates.TryOpen(Forest, out _));
            Assert.IsTrue(rig.P.Store.IsUnlocked(Forest));
        }

        [Test]
        public void ForestOpenedThroughTheSave_StaysOpen_WhileClosedToPlayers()
        {
            // Tests, captures and benchmarks open the Forest in the save: the flag only holds the gate.
            var rig = Create(Content(forestOpen: false));
            var progress = rig.P.Store.Get(Forest);
            progress.Unlocked = true;
            rig.P.Store.Set(progress);
            Assert.IsTrue(rig.P.Store.IsUnlocked(Forest));
            Assert.IsTrue(rig.P.Gates.GetStatus(Forest).Unlocked);
        }

        private static VisitorService Visitors(ContentRegistry content, PlayerState state, BiomeProgressStore biomes) =>
            new VisitorService(state, content, new FakeGameClock(TestUtil.T0)) { Zone = TimeZoneInfo.Utc, ForceOpen = true, Biomes = biomes };

        [Test]
        public void SeasonVisitors_WaitForTheirSpeciesBiome()
        {
            var content = Content(forestOpen: true);
            var state = PlayerState.CreateNew(TestUtil.T0);
            var store = new BiomeProgressStore(state, new ProgressionConfig { TimeZone = TimeZoneInfo.Utc });
            var visitors = Visitors(content, state, store);
            int fox = VisitorData.IndexOf("visitor.golden_fox");
            int badger = VisitorData.IndexOf("visitor.amber_badger");
            int rabbit = VisitorData.IndexOf("visitor.meadow_rabbit");
            Assert.IsTrue(visitors.IsAvailable(fox), "the golden fox exists in this content");
            Assert.IsFalse(visitors.CanCome(fox), "the Forest is not open for the player yet");
            Assert.IsFalse(visitors.CanCome(badger));
            Assert.IsTrue(visitors.CanCome(rabbit), "the Meadow is always open");

            var into = new int[VisitorData.Count];
            // Season 2 (Soleil): its only visitor here is the golden fox; while it cannot come, every visitor that can comes.
            int n = visitors.Candidates(2, into);
            Assert.AreEqual(1, n);
            Assert.AreEqual(rabbit, into[0]);

            var progress = store.Get(Forest);
            progress.Unlocked = true;
            store.Set(progress);
            Assert.IsTrue(visitors.CanCome(fox));
            n = visitors.Candidates(2, into);
            Assert.AreEqual(1, n);
            Assert.AreEqual(fox, into[0], "the Forest is open: the season's golden fox comes");
            n = visitors.Candidates(3, into);
            Assert.AreEqual(1, n);
            Assert.AreEqual(badger, into[0]);
        }

        [Test]
        public void SeasonVisitors_WithoutBiomeProgress_EveryBiomeCounts()
        {
            var content = Content(forestOpen: false);
            var visitors = Visitors(content, PlayerState.CreateNew(TestUtil.T0), null);
            Assert.IsTrue(visitors.CanCome(VisitorData.IndexOf("visitor.golden_fox")));
            var into = new int[VisitorData.Count];
            Assert.AreEqual(1, visitors.Candidates(2, into));
            Assert.AreEqual(VisitorData.IndexOf("visitor.golden_fox"), into[0]);
        }
    }
}
