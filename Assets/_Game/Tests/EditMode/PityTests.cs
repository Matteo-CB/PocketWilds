using System;
using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T21 criterion: statistical simulations follow the doc 02 pity rules. New species chance =
    /// min(35%, 3% x misses), guaranteed after 20 misses, secrets excluded; new variant chance +1.5 points
    /// per miss (capped, boosted by mastery milestones) inside the rolled rarity, never changing the tier.
    /// </summary>
    public class PityTests
    {
        private static readonly ContentId Egg = ContentId.FromSource("egg.test.pity");
        private static readonly ContentId A = ContentId.FromSource("species.a");
        private static readonly ContentId B = ContentId.FromSource("species.b");
        private static readonly ContentId C = ContentId.FromSource("species.c");
        private static readonly ContentId S = ContentId.FromSource("species.secret");

        private sealed class World
        {
            public PlayerState State;
            public EconomyService Economy;
            public HatchService Hatch;
            public MasteryService Mastery;
        }

        /// <summary>
        /// Species A (weight 1000), B and C (weight 1 each), secret S (weight 1). Each species has
        /// <paramref name="commons"/> Common variants and one Uncommon. Egg tiers from <paramref name="weights"/>.
        /// </summary>
        private static World Create(int commons = 2, double[] weights = null, double weightB = 1.0, PlayerState state = null)
        {
            var builder = new ContentRegistryBuilder();
            AddSpecies(builder, "species.a", commons, false);
            AddSpecies(builder, "species.b", commons, false);
            AddSpecies(builder, "species.c", commons, false);
            AddSpecies(builder, "species.secret", commons, true);
            builder.AddEgg(new EggDefinition(Egg, "egg.test.pity.name", ContentId.FromSource("biome.grassland"), EggTier.Basic,
                new Color32[EggDefinition.PaletteSize], 15, 0, 15, "PF_Egg_Base", 10.0, 1.0,
                weights ?? new[] { 1.0, 0, 0, 0, 0, 0 },
                new[] { new EggDropEntry(A, 1000.0), new EggDropEntry(B, weightB), new EggDropEntry(C, 1.0), new EggDropEntry(S, 1.0) }),
                "egg.test.pity");
            var content = builder.Build();
            var world = new World { State = state ?? PlayerState.CreateNew(TestUtil.T0) };
            world.Economy = new EconomyService(world.State, content, EconomyConfig.Default, null);
            world.Mastery = new MasteryService(world.State, EconomyConfig.Default, world.Economy);
            world.Hatch = new HatchService(world.State, content, world.Economy, new FakeGameClock(TestUtil.T0), null, world.Mastery);
            if (state == null)
                world.Economy.Grant(CurrencyId.Light, BigValue.FromMantissaExponent(1.0, 300), EconomyReason.Reward);
            return world;
        }

        private static void AddSpecies(ContentRegistryBuilder builder, string id, int commons, bool secret)
        {
            builder.AddSpecies(new SpeciesDefinition(ContentId.FromSource(id), id + ".name", RigFamily.SmallQuadruped,
                ContentId.FromSource("biome.grassland"), 1.0, "PF_Animal_Test", secret), id);
            for (int v = 0; v <= commons; v++)
            {
                string variant = "variant." + id.Substring(8) + ".v" + v;
                builder.AddVariant(new VariantDefinition(ContentId.FromSource(variant), ContentId.FromSource(id), variant + ".name",
                    v < commons ? RarityTier.Common : RarityTier.Uncommon, new Color32[VariantDefinition.PaletteSize],
                    PatternChannel.None, 0), variant);
            }
        }

        private static ContentId Variant(string species, int v) => ContentId.FromSource("variant." + species + ".v" + v);

        /// <summary>Binomial tolerance: 4.5 standard deviations plus a small absolute margin.</summary>
        private static void AssertRate(double expected, int hits, int trials, string what)
        {
            double observed = hits / (double)trials;
            double sigma = Math.Sqrt(Math.Max(expected * (1 - expected), 1e-6) / trials);
            Assert.AreEqual(expected, observed, 4.5 * sigma + 0.004, what);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(10)]
        [TestCase(15)]
        [TestCase(19)]
        public void NewSpeciesChance_IsBonusPlusNaturalOdds(int misses)
        {
            var world = Create();
            world.State.Collection.DiscoveredSpecies.Add(A);
            const int Trials = 6000;
            int hits = 0;
            for (int i = 0; i < Trials; i++)
            {
                world.State.Pity.NewSpeciesMisses[Egg] = misses;
                var result = world.Hatch.Hatch(Egg);
                if (result.NewSpecies)
                    hits++;
                world.State.Collection.DiscoveredSpecies.Clear();
                world.State.Collection.DiscoveredSpecies.Add(A);
            }
            double bonus = Math.Min(0.35, 0.03 * misses);
            double natural = 3.0 / 1003.0; // B, C and the secret drop naturally
            // The bonus only draws among normal species (B, C), the rest is the natural roll.
            AssertRate(bonus + (1 - bonus) * natural, hits, Trials, "misses " + misses);
        }

        [Test]
        public void HardLimit_GuaranteesAMissingNormalSpecies_NeverASecret()
        {
            var world = Create();
            world.State.Collection.DiscoveredSpecies.Add(A);
            for (int i = 0; i < 2000; i++)
            {
                world.State.Pity.NewSpeciesMisses[Egg] = 20;
                var result = world.Hatch.Hatch(Egg);
                Assert.IsTrue(result.NewSpecies);
                Assert.AreEqual(PityOutcome.SpeciesGuarantee, result.Pity);
                Assert.IsTrue(result.Animal.Species == B || result.Animal.Species == C, "Secrets never come from the pity.");
                Assert.AreEqual(0, world.State.Pity.NewSpeciesMisses[Egg], "Reset on a new species.");
                world.State.Collection.DiscoveredSpecies.Remove(result.Animal.Species);
            }
        }

        [Test]
        public void Players_NeverWaitMoreThanTheHardLimit()
        {
            int worst = 0;
            long totalEggs = 0;
            const int Players = 400;
            for (int p = 0; p < Players; p++)
            {
                var world = Create();
                int streak = 0;
                int eggs = 0;
                var collection = world.State.Collection;
                while (!(collection.DiscoveredSpecies.Contains(B) && collection.DiscoveredSpecies.Contains(C)))
                {
                    var result = world.Hatch.Hatch(Egg);
                    eggs++;
                    streak = result.NewSpecies ? 0 : streak + 1;
                    worst = Math.Max(worst, streak);
                    Assert.Less(eggs, 200, "A missing species always arrives.");
                }
                totalEggs += eggs;
            }
            UnityEngine.Debug.Log($"[Pity] {Players} players: worst streak without a new species {worst}, " +
                                  $"average eggs to find A, B and C {totalEggs / (double)Players:F1}");
            Assert.LessOrEqual(worst, 20, "Doc 02: guaranteed after 20 misses.");
        }

        [Test]
        public void NothingLeftToFind_TurnsTheSpeciesPityOff_EvenWithASecretMissing()
        {
            var world = Create();
            world.State.Collection.DiscoveredSpecies.UnionWith(new[] { A, B, C });
            world.State.Pity.NewSpeciesMisses[Egg] = 19;
            Assert.AreEqual(0.0, world.Hatch.SpeciesPityChance(Egg));
            var result = world.Hatch.Hatch(Egg);
            Assert.AreNotEqual(PityOutcome.SpeciesBonus, result.Pity);
            Assert.AreNotEqual(PityOutcome.SpeciesGuarantee, result.Pity);
            Assert.IsFalse(world.State.Pity.NewSpeciesMisses.ContainsKey(Egg), "No counter while nothing is missing.");
        }

        [Test]
        public void SpeciesCounter_CountsMisses_AndResetsOnDiscovery()
        {
            var world = Create(weightB: 0.0001);
            world.State.Collection.DiscoveredSpecies.Add(A);
            int expected = 0;
            for (int i = 0; i < 60; i++)
            {
                var result = world.Hatch.Hatch(Egg);
                expected = result.NewSpecies ? 0 : expected + 1;
                if (world.State.Pity.NewSpeciesMisses.TryGetValue(Egg, out int stored))
                    Assert.AreEqual(expected, stored);
                if (result.NewSpecies)
                    world.State.Collection.DiscoveredSpecies.Remove(result.Animal.Species);
            }
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(10)]
        [TestCase(20)]
        [TestCase(40)]
        public void NewVariantChance_IsNaturalOddsPlusBonus(int misses)
        {
            // Only A drops (all other species found, pool weights tiny), 10 Common variants, one missing.
            var world = Create(commons: 10);
            var collection = world.State.Collection;
            collection.DiscoveredSpecies.UnionWith(new[] { A, B, C, S });
            for (int v = 0; v < 9; v++)
                collection.DiscoveredVariants.Add(Variant("a", v));
            const int Trials = 6000;
            int hits = 0, fromA = 0;
            for (int i = 0; i < Trials; i++)
            {
                world.State.Pity.VariantMisses[A] = misses;
                world.State.Mastery.Species.Clear(); // milestones boost the bonus: measure the base rule
                var result = world.Hatch.Hatch(Egg);
                if (result.Animal.Species != A)
                    continue;
                fromA++;
                Assert.AreEqual(RarityTier.Common, result.Animal.Rarity, "Pity never changes the rarity.");
                if (result.Animal.Variant == Variant("a", 9))
                {
                    hits++;
                    collection.DiscoveredVariants.Remove(Variant("a", 9));
                }
            }
            double bonus = Math.Min(0.30, 0.015 * misses);
            double natural = 0.1;
            AssertRate(natural + (1 - natural) * bonus, hits, fromA, "variant misses " + misses);
        }

        [Test]
        public void MasteryMilestones_StrengthenTheVariantPity()
        {
            var world = Create();
            Assert.AreEqual(0.15, world.Hatch.VariantBonus(A, 10), 1e-12);
            world.Mastery.AddXp(A, MasteryReason.Birth, 100000); // far past level 10
            int milestones = world.State.Mastery.GetLevel(A) / 5;
            Assert.GreaterOrEqual(milestones, 2);
            Assert.AreEqual(Math.Min(0.30, 0.015 * (1 + 0.1 * milestones) * 10), world.Hatch.VariantBonus(A, 10), 1e-12);
            Assert.AreEqual(0.30, world.Hatch.VariantBonus(A, 1000), 1e-12, "Capped.");
        }

        [Test]
        public void VariantCounter_CountsKnownVariants_WhileSomeAreMissing()
        {
            var world = Create(commons: 3, weights: new[] { 80.0, 20.0, 0, 0, 0, 0 });
            var collection = world.State.Collection;
            collection.DiscoveredSpecies.UnionWith(new[] { A, B, C, S });
            collection.DiscoveredVariants.UnionWith(new[] { Variant("a", 0), Variant("a", 1), Variant("a", 2) });
            int expected = 0;
            for (int i = 0; i < 200; i++)
            {
                var result = world.Hatch.Hatch(Egg);
                if (result.Animal.Species != A)
                    continue;
                if (result.NewVariant)
                {
                    Assert.IsFalse(world.State.Pity.VariantMisses.ContainsKey(A), "Reset on a new variant.");
                    break; // a.v3 (Uncommon) found: nothing missing anymore for A
                }
                expected++;
                Assert.AreEqual(expected, world.State.Pity.VariantMisses[A]);
            }
        }

        [Test]
        public void Pity_KeepsTheRarityOdds()
        {
            // Real-looking egg odds; every hatch lands on the rolled tier (or its documented fallback).
            var world = Create(commons: 2, weights: new[] { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 });
            var counts = new int[6];
            const int Trials = 20000;
            for (int i = 0; i < Trials; i++)
            {
                var result = world.Hatch.Hatch(Egg);
                // These species only have Common and Uncommon variants: higher rolls fall back to Uncommon.
                var expected = result.RolledTier == RarityTier.Common ? RarityTier.Common : RarityTier.Uncommon;
                Assert.AreEqual(expected, result.Animal.Rarity);
                counts[(int)result.RolledTier]++;
            }
            AssertRate(0.72, counts[0], Trials, "Common");
            AssertRate(0.20, counts[1], Trials, "Uncommon");
            AssertRate(0.06, counts[2], Trials, "Rare");
        }

        [Test]
        public void PityCounters_SurviveTheSaveFile_AndRollsStayDeterministic()
        {
            var world = Create();
            for (int i = 0; i < 30; i++)
                world.Hatch.Hatch(Egg);
            var serializer = new BinarySaveSerializer();
            var writer = new ByteBufferWriter();
            serializer.Serialize(world.State, writer);
            var bytes = writer.WrittenSpan.ToArray();
            var loaded = serializer.Deserialize(serializer.ReadDocument(bytes, bytes.Length));
            CollectionAssert.AreEquivalent(world.State.Pity.NewSpeciesMisses, loaded.Pity.NewSpeciesMisses);
            CollectionAssert.AreEquivalent(world.State.Pity.VariantMisses, loaded.Pity.VariantMisses);

            var twin = Create(state: loaded);
            for (int i = 0; i < 40; i++)
            {
                var a = world.Hatch.Hatch(Egg);
                var b = twin.Hatch.Hatch(Egg);
                Assert.AreEqual(a.Animal.Variant, b.Animal.Variant);
                Assert.AreEqual(a.Pity, b.Pity);
            }
        }
    }
}
