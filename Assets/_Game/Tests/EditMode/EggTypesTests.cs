using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T35B, doc 02 Œufs, section 10: the tables a million times, the guarantees, Curious Eye and its 40 % ceiling, Focus
    /// targets, the three Discovery rules in order, a ×10 that is exactly ten single hatches, odds shown = odds used.
    /// </summary>
    public class EggTypesTests
    {
        private static readonly ContentId Grassland = ContentId.FromSource("biome.grassland");
        private static readonly ContentId Forest = ContentId.FromSource("biome.forest");
        private static readonly ContentId Basic = ContentId.FromSource("egg.test.basic");
        private static readonly ContentId Focus = ContentId.FromSource("egg.test.focus");
        private static readonly ContentId Discovery = ContentId.FromSource("egg.test.discovery");
        private static readonly ContentId ForestBasic = ContentId.FromSource("egg.test.forest");

        private static readonly double[] BasicTable = { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 };
        private static readonly double[] FocusTable = { 50.0, 30.0, 14.0, 4.5, 1.3, 0.2 };
        private static readonly double[] RareTable = { 0.0, 0.0, 70.0, 22.0, 7.0, 1.0 };

        private static readonly RarityTier[] SixColors =
            { RarityTier.Common, RarityTier.Common, RarityTier.Uncommon, RarityTier.Rare, RarityTier.Epic, RarityTier.Legendary };
        private static readonly RarityTier[] ThreeColors = { RarityTier.Common, RarityTier.Uncommon, RarityTier.Rare };

        private static ContentId G(int i) => ContentId.FromSource("species.g" + i);
        private static ContentId F(int i) => ContentId.FromSource("species.f" + i);
        private static ContentId SecretVariant => ContentId.FromSource("variant.g0.secret");

        private sealed class Modifiers : IHatchModifiers
        {
            public double Curiosity;
            public BigValue Cap;
            public double EggPriceMultiplier(ContentId biome) => 1.0;
            public double VariantChanceBonus(ContentId biome) => Curiosity;
            public BigValue EggPriceCap() => Cap;
        }

        private sealed class World
        {
            public PlayerState State;
            public EconomyService Economy;
            public HatchService Hatch;
            public ContentRegistry Content;
        }

        /// <summary>
        /// Prairie: g0, g1, g2 with six colors, g3, g4, g5 with three (and a secret color on g0); Forest: f0, f1 with three.
        /// Eggs: Basic and Focus of the Prairie (Focus offered at mastery 3, 3 targets), Basic of the Forest, Discovery.
        /// </summary>
        private static ContentRegistry Content()
        {
            var builder = new ContentRegistryBuilder();
            for (int i = 0; i < 6; i++)
                AddSpecies(builder, "g" + i, Grassland, i < 3 ? SixColors : ThreeColors);
            for (int i = 0; i < 2; i++)
                AddSpecies(builder, "f" + i, Forest, ThreeColors);
            builder.AddVariant(new VariantDefinition(SecretVariant, G(0), "variant.g0.secret.name", RarityTier.Legendary,
                new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0, true), "variant.g0.secret");
            var prairie = Enumerable.Range(0, 6).Select(i => new EggDropEntry(G(i), 1.0)).ToArray();
            builder.AddEgg(Egg(Basic, Grassland, EggTier.Basic, BasicTable, prairie), "egg.test.basic");
            builder.AddEgg(Egg(Focus, Grassland, EggTier.Focus, FocusTable, prairie, 40.0, unlock: 3, targets: 3), "egg.test.focus");
            builder.AddEgg(Egg(ForestBasic, Forest, EggTier.Basic, BasicTable, new[] { new EggDropEntry(F(0), 1.0), new EggDropEntry(F(1), 1.0) }),
                "egg.test.forest");
            builder.AddEgg(Egg(Discovery, ContentId.None, EggTier.Discovery, RareTable, new EggDropEntry[0], 1.0, growth: 1.0), "egg.test.discovery");
            return builder.Build();
        }

        private static EggDefinition Egg(ContentId id, ContentId biome, EggTier tier, double[] table, EggDropEntry[] pool, double cost = 10.0,
            double growth = 1.18, int unlock = 0, int targets = 0) =>
            new EggDefinition(id, id + ".name", biome, tier, new Color32[EggDefinition.PaletteSize], 15, 0, 15, "PF_Egg_Base", cost, growth,
                (double[])table.Clone(), pool, unlockBiomeMastery: unlock, focusTargetCount: targets);

        private static void AddSpecies(ContentRegistryBuilder builder, string name, ContentId biome, RarityTier[] colors)
        {
            var species = ContentId.FromSource("species." + name);
            builder.AddSpecies(new SpeciesDefinition(species, "species." + name + ".name", RigFamily.SmallQuadruped, biome, 1.0, "PF_Animal_Test",
                false), "species." + name);
            for (int v = 0; v < colors.Length; v++)
            {
                string id = "variant." + name + ".v" + v;
                builder.AddVariant(new VariantDefinition(ContentId.FromSource(id), species, id + ".name", colors[v],
                    new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0), id);
            }
        }

        private static World Create(int mastery = 3, bool forestOpen = false, Guid? profile = null, ContentRegistry content = null)
        {
            var world = new World { State = PlayerState.CreateNew(TestUtil.T0), Content = content ?? Content() };
            if (profile.HasValue)
                world.State.Meta.ProfileId = profile.Value;
            world.State.World.Biomes.Add(new BiomeProgress { Biome = Grassland, Unlocked = true, ZonesOpened = 1u, MasteryLevel = mastery });
            world.State.World.Biomes.Add(new BiomeProgress { Biome = Forest, Unlocked = forestOpen });
            world.Economy = new EconomyService(world.State, world.Content, EconomyConfig.Default, null);
            world.Hatch = new HatchService(world.State, world.Content, world.Economy, new FakeGameClock(TestUtil.T0), null);
            world.Economy.Grant(CurrencyId.Light, BigValue.FromMantissaExponent(1.0, 300), EconomyReason.Reward);
            return world;
        }

        private static void Discover(World world, ContentId species, bool allColors)
        {
            world.State.Collection.DiscoveredSpecies.Add(species);
            if (!allColors)
                return;
            var variants = world.Content.Variants;
            for (int i = 0; i < variants.Count; i++)
                if (variants[i].Species == species && !variants[i].IsSecret)
                    world.State.Collection.DiscoveredVariants.Add(variants[i].Id);
        }

        private static bool HasColorLeft(World world, ContentId species)
        {
            var variants = world.Content.Variants;
            for (int i = 0; i < variants.Count; i++)
                if (variants[i].Species == species && !variants[i].IsSecret && !world.State.Collection.DiscoveredVariants.Contains(variants[i].Id))
                    return true;
            return false;
        }

        // ------------------------------------------------------------------------------------------- prices (T35C)

        [Test]
        public void BasicAndFocus_ShareTheBiomeCounter_FocusStaysFourTimesTheBasic()
        {
            var world = Create();
            world.State.Collection.Add(new AnimalInstance { Species = G(0) }); // past the free tutorial egg
            Assert.AreEqual(10.0, world.Hatch.GetCost(Basic).ToDoubleClamped());
            Assert.AreEqual(40.0, world.Hatch.GetCost(Focus).ToDoubleClamped());
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(HatchStatus.Hatched, world.Hatch.Hatch(Basic).Status);
            Assert.AreEqual(3, world.Hatch.BoughtInCycle(Focus), "The Focus reads the Basic's counter.");
            Assert.AreEqual(4.0 * world.Hatch.GetCost(Basic).ToDoubleClamped(), world.Hatch.GetCost(Focus).ToDoubleClamped(), 2.5,
                "Four times the Basic of the moment (whole Light rounding apart).");
            Assert.AreEqual(HatchStatus.Hatched, world.Hatch.Hatch(Focus).Status);
            Assert.AreEqual(4, world.Hatch.BoughtInCycle(Basic), "A Focus advances the biome's counter too.");
            Assert.AreEqual(0, world.Hatch.BoughtInCycle(ForestBasic), "Each biome has its own counter.");
        }

        [Test]
        public void GrowthOverride_PricesEveryBiomeEgg()
        {
            var world = Create();
            world.State.Collection.Add(new AnimalInstance { Species = G(0) });
            world.Hatch.GrowthOverride = 1.45;
            world.Hatch.Hatch(Basic);
            world.Hatch.Hatch(Basic);
            Assert.AreEqual(21.0, world.Hatch.GetCost(Basic).ToDoubleClamped(), "round(10 x 1.45^2)");
            Assert.AreEqual(84.0, world.Hatch.GetCost(Focus).ToDoubleClamped(), "round(40 x 1.45^2)");
            world.Hatch.GrowthOverride = 0.0;
            Assert.AreEqual(14.0, world.Hatch.GetCost(Basic).ToDoubleClamped(), "0: the egg's own growth (1.18).");
        }

        [Test]
        public void PriceCap_NoEggCostsMoreThanTheCap()
        {
            var world = Create();
            world.State.Collection.Add(new AnimalInstance { Species = G(0) });
            world.Hatch.SetModifiers(new Modifiers { Cap = BigValue.FromDouble(25.0) });
            Assert.AreEqual(10.0, world.Hatch.GetCost(Basic).ToDoubleClamped(), "Under the cap: its own price.");
            Assert.AreEqual(25.0, world.Hatch.GetCost(Focus).ToDoubleClamped(), "40 capped at 25.");
            for (int i = 0; i < 12; i++)
                world.Hatch.Hatch(Basic);
            Assert.AreEqual(25.0, world.Hatch.GetCost(Basic).ToDoubleClamped(), "10 x 1.18^12 = 73, capped.");
            Assert.AreEqual(75.0, world.Hatch.GetCost(Basic, 3).ToDoubleClamped(), "A multi hatch pays the cap for each egg.");
        }

        // ------------------------------------------------------------------------------------------- tables

        [Test]
        public void Tables_TenMillionDrawsPerEggType_WithinATenthOfAPoint()
        {
            var content = Content();
            foreach (var id in new[] { Basic, Focus, Discovery })
            {
                var table = content.Eggs.Get(id).RarityWeights;
                var counts = new int[EggDefinition.RarityTierCount];
                var rng = new DeterministicRandom(0xC0FFEEu ^ id.Value);
                // The doc asks for a million draws at most 0.1 point off. A tenth of a point is only 2.2 standard deviations at a
                // million (a fair table fails one run in thirty); ten million make it 7: a failure means a wrong table.
                const int draws = 10_000_000;
                for (int i = 0; i < draws; i++)
                    counts[(int)HatchService.RollTier(table, ref rng)]++;
                for (int t = 0; t < counts.Length; t++)
                    Assert.AreEqual(table[t], counts[t] * 100.0 / draws, 0.1, $"{id}: tier {(RarityTier)t}");
            }
        }

        // ------------------------------------------------------------------------------------------- Focus

        [Test]
        public void Focus_IsOfferedFromBiomeMastery3()
        {
            var world = Create(mastery: 2);
            var light = world.State.Wallet.Light;
            Assert.IsFalse(world.Hatch.IsOffered(Focus));
            world.State.Collection.DiscoveredSpecies.Add(G(0)); // past the free tutorial egg
            world.State.Collection.Add(new AnimalInstance { Species = G(0), Variant = ContentId.FromSource("variant.g0.v0") });
            Assert.AreEqual(HatchStatus.Locked, world.Hatch.Hatch(Focus).Status);
            Assert.AreEqual(light, world.State.Wallet.Light, "A locked egg costs nothing.");

            world = Create(mastery: 3);
            Assert.IsTrue(world.Hatch.IsOffered(Focus));
            Assert.IsTrue(world.Hatch.IsOffered(Basic));
            Assert.AreEqual(4.0, world.Hatch.GetCost(Focus).ToDoubleClamped() / world.Hatch.GetCost(Basic).ToDoubleClamped(), 1e-9,
                "Focus: 4 x B.");
        }

        [Test]
        public void Focus_TargetsTheSpeciesWithTheMostColorsLeft_ThenTheWholePrairie()
        {
            var world = Create();
            var targets = new ContentId[8];
            Assert.AreEqual(3, world.Hatch.GetFocusTargets(Focus, targets));
            CollectionAssert.AreEqual(new[] { G(0), G(1), G(2) }, targets.Take(3).ToArray(), "Six colors left each.");

            Discover(world, G(0), allColors: true);
            Assert.AreEqual(3, world.Hatch.GetFocusTargets(Focus, targets));
            CollectionAssert.AreEqual(new[] { G(1), G(2), G(3) }, targets.Take(3).ToArray(), "g0 is done; ties keep the pool order.");

            for (int i = 0; i < 6; i++)
                Discover(world, G(i), allColors: true);
            Assert.AreEqual(6, world.Hatch.GetFocusTargets(Focus, targets), "Every color found: the egg holds the six species.");
            Assert.AreEqual(0, world.Hatch.GetFocusTargets(Basic, targets), "A Basic egg targets nothing.");
        }

        [Test]
        public void Focus_OnlyGivesItsTargets()
        {
            var world = Create();
            world.Hatch.Hatch(Basic); // the free tutorial egg
            var targets = new ContentId[8];
            for (int i = 0; i < 300; i++)
            {
                int count = world.Hatch.GetFocusTargets(Focus, targets);
                var result = world.Hatch.Hatch(Focus);
                Assert.IsTrue(result.Succeeded, result.Status.ToString());
                Assert.Contains(result.Animal.Species, targets.Take(count).ToArray(), $"Hatch {i}: outside the targets.");
            }
        }

        // ------------------------------------------------------------------------------------------- guarantees

        [Test]
        public void NewSpecies_ArrivesByTheTwentiethEgg_ForBasicAndFocus()
        {
            foreach (var egg in new[] { Basic, Focus })
            {
                for (int profile = 0; profile < 40; profile++)
                {
                    var world = Create(profile: new Guid(profile + 1, 0, 0, new byte[8]));
                    world.Hatch.Hatch(Basic);
                    int sinceNew = 0;
                    var odds = new EggOdds();
                    for (int i = 0; i < 120; i++)
                    {
                        world.Hatch.GetOdds(egg, odds);
                        if (odds.EggsToGuarantee == 0)
                            break; // nothing left to find in what this egg gives
                        var result = world.Hatch.Hatch(egg);
                        Assert.IsTrue(result.Succeeded);
                        sinceNew = result.NewSpecies ? 0 : sinceNew + 1;
                        Assert.LessOrEqual(sinceNew, 20, $"{egg}, profile {profile}: more than 20 eggs without a new species.");
                    }
                }
            }
        }

        [Test]
        public void NewVariantChance_CuriousEyeAddsItsPoints_FortyPercentAtMost()
        {
            var world = Create();
            var modifiers = new Modifiers { Curiosity = 0.10 };
            world.Hatch.SetModifiers(modifiers);
            world.State.Pity.VariantMisses[G(0)] = 2;
            Assert.AreEqual(0.03 + 0.10, world.Hatch.NewVariantChance(G(0), Grassland), 1e-9);
            world.State.Pity.VariantMisses[G(0)] = 400;
            Assert.AreEqual(0.40, world.Hatch.NewVariantChance(G(0), Grassland), 1e-9, "30 % of protection + 10 points.");
            modifiers.Curiosity = 0.25;
            Assert.AreEqual(0.40, world.Hatch.NewVariantChance(G(0), Grassland), 1e-9, "Never above 40 %.");
        }

        [Test]
        public void NormalEggs_NeverGiveASecretColor()
        {
            var world = Create();
            world.Hatch.Hatch(Basic);
            for (int i = 0; i < 2000; i++)
            {
                var result = world.Hatch.Hatch(i % 2 == 0 ? Basic : Focus);
                Assert.AreNotEqual(SecretVariant, result.Animal.Variant);
            }
        }

        // ------------------------------------------------------------------------------------------- Discovery

        [Test]
        public void Discovery_FirstANewSpeciesOfAnOpenBiome()
        {
            for (int profile = 0; profile < 60; profile++)
            {
                var world = Create(profile: new Guid(profile + 100, 0, 0, new byte[8]));
                world.Hatch.Hatch(Basic);
                world.Hatch.GrantDiscoveryEggs(1, DiscoverySource.Debug);
                var known = new HashSet<ContentId>(world.State.Collection.DiscoveredSpecies);
                var result = world.Hatch.Hatch(Discovery);
                Assert.IsTrue(result.Succeeded, result.Status.ToString());
                Assert.AreEqual(DiscoveryRule.NewSpecies, result.Discovery);
                Assert.IsTrue(result.NewSpecies);
                Assert.IsFalse(known.Contains(result.Animal.Species));
                Assert.AreEqual(Grassland, world.Content.Species.Get(result.Animal.Species).Biome, "The Forest is not open.");
                Assert.IsTrue(result.WasFree);
                Assert.IsTrue(result.Cost.IsZero);
            }
        }

        [Test]
        public void Discovery_TheCameraBiomeWeighsDouble()
        {
            // Open Prairie with one species left (g5), open Forest with two (f0, f1): weights 1 + 1 + 1, or 2 + 2 + 1.
            int forestWithCamera = 0, forestElsewhere = 0;
            const int profiles = 1500;
            var content = Content();
            for (int p = 0; p < profiles; p++)
            {
                for (int camera = 0; camera < 2; camera++)
                {
                    var world = Create(forestOpen: true, profile: new Guid(p + 1000, (short)camera, 0, new byte[8]), content: content);
                    for (int i = 0; i < 5; i++)
                        Discover(world, G(i), allColors: false);
                    world.State.Collection.Add(new AnimalInstance { Species = G(0), Variant = ContentId.FromSource("variant.g0.v0") });
                    world.Hatch.SetCameraBiome(camera == 0 ? Forest : Grassland);
                    world.Hatch.GrantDiscoveryEggs(1, DiscoverySource.Debug);
                    var species = world.Hatch.Hatch(Discovery).Animal.Species;
                    bool forest = species == F(0) || species == F(1);
                    if (camera == 0 && forest)
                        forestWithCamera++;
                    else if (camera == 1 && forest)
                        forestElsewhere++;
                }
            }
            Assert.AreEqual(0.8, forestWithCamera / (double)profiles, 0.045, "Camera in the Forest: 4 of 5.");
            Assert.AreEqual(0.5, forestElsewhere / (double)profiles, 0.045, "Camera in the Prairie: 2 of 4.");
        }

        [Test]
        public void Discovery_ThenANewColor_ThenRareOrBetter_NeverASecret()
        {
            var world = Create();
            for (int i = 0; i < 6; i++)
                Discover(world, G(i), allColors: false);
            world.State.Collection.Add(new AnimalInstance { Species = G(0), Variant = ContentId.FromSource("variant.g0.v0") });
            world.Hatch.GrantDiscoveryEggs(40, DiscoverySource.Debug);

            // Every species found: each egg gives a color not found yet, until none is left.
            int normalColors = 3 * SixColors.Length + 3 * ThreeColors.Length;
            for (int i = 0; i < normalColors; i++)
            {
                var result = world.Hatch.Hatch(Discovery);
                Assert.AreEqual(DiscoveryRule.NewVariant, result.Discovery, $"Egg {i}");
                Assert.IsTrue(result.NewVariant);
                Assert.AreNotEqual(SecretVariant, result.Animal.Variant);
            }

            // Nothing left: Rare or better.
            for (int i = 0; i < 40 - normalColors; i++)
            {
                var result = world.Hatch.Hatch(Discovery);
                Assert.AreEqual(DiscoveryRule.RareOrBetter, result.Discovery);
                Assert.GreaterOrEqual(result.Animal.Rarity, RarityTier.Rare);
                Assert.AreNotEqual(SecretVariant, result.Animal.Variant);
            }
            Assert.AreEqual(0, world.Hatch.DiscoveryEggs);
            Assert.AreEqual(HatchStatus.NoEggInStock, world.Hatch.Hatch(Discovery).Status);
            Assert.IsFalse(world.Hatch.IsOffered(Discovery), "An empty stock hides the egg.");
        }

        [Test]
        public void Discovery_Stock_GrantsCountAndAreFree()
        {
            var world = Create();
            int granted = 0;
            DiscoverySource source = default;
            world.Hatch.DiscoveryEggsGranted += (count, from) =>
            {
                granted += count;
                source = from;
            };
            world.Hatch.GrantDiscoveryEggs(3, DiscoverySource.BiomeOpening);
            Assert.AreEqual(3, world.Hatch.DiscoveryEggs);
            Assert.AreEqual(3, granted);
            Assert.AreEqual(DiscoverySource.BiomeOpening, source);
            Assert.IsTrue(world.Hatch.GetCost(Discovery, 3).IsZero);
            Assert.IsTrue(world.Hatch.CanHatch(Discovery, 3));
            Assert.IsFalse(world.Hatch.CanHatch(Discovery, 4));

            var light = world.State.Wallet.Light;
            var results = new HatchResult[10];
            Assert.AreEqual(3, world.Hatch.Hatch(Discovery, 3, results));
            Assert.AreEqual(0, world.Hatch.DiscoveryEggs);
            Assert.AreEqual(light, world.State.Wallet.Light, "Discovery eggs never cost Light.");
            Assert.IsFalse(world.State.World.EggPurchasesInCycle.ContainsKey(Discovery));
        }

        // ------------------------------------------------------------------------------------------- multi hatch

        [Test]
        public void TimesTen_IsExactlyTenSingleHatches()
        {
            var profile = new Guid("6f0c1a2e-0000-4000-8000-0000000035b0");
            var many = Create(profile: profile);
            var single = Create(profile: profile);
            many.Hatch.Hatch(Basic);
            single.Hatch.Hatch(Basic);

            var results = new HatchResult[10];
            int summaries = 0;
            many.Hatch.MultiHatched += summary =>
            {
                summaries++;
                Assert.AreEqual(10, summary.Count);
            };
            Assert.AreEqual(10, many.Hatch.Hatch(Basic, 10, results));
            Assert.AreEqual(1, summaries);
            for (int i = 0; i < 10; i++)
            {
                var one = single.Hatch.Hatch(Basic);
                var batch = results[i];
                Assert.AreEqual(i + 1, batch.MultiIndex);
                Assert.AreEqual(one.Animal.InstanceId, batch.Animal.InstanceId);
                Assert.AreEqual(one.Animal.Variant, batch.Animal.Variant, $"Egg {i}");
                Assert.AreEqual(one.Animal.Seed, batch.Animal.Seed);
                Assert.AreEqual(one.Cost, batch.Cost);
                Assert.AreEqual(one.Pity, batch.Pity);
                Assert.AreEqual(one.NewSpecies, batch.NewSpecies);
            }
            Assert.AreEqual(single.State.Wallet.Light, many.State.Wallet.Light);
            Assert.AreEqual(single.State.World.EggPurchasesInCycle[Basic], many.State.World.EggPurchasesInCycle[Basic]);
            CollectionAssert.AreEquivalent(single.State.Pity.NewSpeciesMisses, many.State.Pity.NewSpeciesMisses);
            CollectionAssert.AreEquivalent(single.State.Pity.VariantMisses, many.State.Pity.VariantMisses);
        }

        [Test]
        public void MultiHatch_CostsTheNextPrices_AndNeedsThemAll()
        {
            var world = Create();
            world.Hatch.Hatch(Basic);
            var egg = world.Content.Eggs.Get(Basic);
            int bought = world.State.World.EggPurchasesInCycle.TryGetValue(Basic, out int n) ? n : 0;
            var expected = HatchService.Cost(egg, bought) + HatchService.Cost(egg, bought + 1) + HatchService.Cost(egg, bought + 2);
            Assert.AreEqual(expected, world.Hatch.GetCost(Basic, 3));

            // One Light short of three eggs: nothing hatches, nothing is spent.
            var poor = Create();
            poor.Hatch.Hatch(Basic);
            var price = poor.Hatch.GetCost(Basic, 3);
            poor.Economy.Spend(CurrencyId.Light, poor.State.Wallet.Light, EconomyReason.EggPurchase);
            poor.Economy.Grant(CurrencyId.Light, price - BigValue.FromDouble(1), EconomyReason.Reward);
            var results = new HatchResult[3];
            int animals = poor.State.Collection.Count;
            Assert.IsFalse(poor.Hatch.CanHatch(Basic, 3));
            Assert.AreEqual(0, poor.Hatch.Hatch(Basic, 3, results));
            Assert.AreEqual(HatchStatus.NotEnoughLight, results[0].Status);
            Assert.AreEqual(animals, poor.State.Collection.Count);
            Assert.IsTrue(poor.Hatch.CanHatch(Basic, 1), "One egg is still affordable.");
        }

        // ------------------------------------------------------------------------------------------- odds shown = odds used

        [Test]
        public void OddsShown_AreTheOddsUsed()
        {
            var world = Create();
            world.Hatch.SetModifiers(new Modifiers { Curiosity = 0.04 });
            world.Hatch.Hatch(Basic);
            for (int i = 0; i < 7; i++)
                world.Hatch.Hatch(Focus);
            var odds = new EggOdds();
            foreach (var id in new[] { Basic, Focus })
            {
                world.Hatch.GetOdds(id, odds);
                var table = world.Content.Eggs.Get(id).RarityWeights;
                for (int t = 0; t < table.Length; t++)
                    Assert.AreEqual(table[t], odds.RarityPercent[t], 1e-9, $"{id} tier {t}");
                Assert.AreEqual(world.Hatch.SpeciesPityChance(id), odds.NewSpeciesBonus, 1e-12);

                var targets = new ContentId[8];
                int count = id == Focus ? world.Hatch.GetFocusTargets(id, targets) : 6;
                double best = 0.0;
                bool speciesLeft = false;
                for (int i = 0; i < count; i++)
                {
                    var species = id == Focus ? targets[i] : G(i);
                    speciesLeft |= !world.State.Collection.DiscoveredSpecies.Contains(species);
                    if (HasColorLeft(world, species))
                        best = Math.Max(best, world.Hatch.NewVariantChance(species, Grassland));
                }
                world.State.Pity.NewSpeciesMisses.TryGetValue(id, out int misses);
                Assert.AreEqual(speciesLeft ? Math.Max(1, 20 - misses) : 0, odds.EggsToGuarantee);
                Assert.AreEqual(best, odds.NewVariantBonus, 1e-12, "The best new color chance among the species the egg gives.");
                if (id == Focus)
                {
                    Assert.AreEqual(count, odds.TargetCount);
                    CollectionAssert.AreEqual(targets.Take(count).ToArray(), odds.Targets.Take(count).ToArray());
                }
            }

            world.Hatch.GrantDiscoveryEggs(2, DiscoverySource.Debug);
            world.Hatch.GetOdds(Discovery, odds);
            Assert.IsTrue(odds.Free);
            Assert.AreEqual(2, odds.Stock);
            Assert.AreEqual(70.0, odds.RarityPercent[(int)RarityTier.Rare], 1e-9);
        }

        // ------------------------------------------------------------------------------------------- content rules

        [Test]
        public void Validator_TablesSumTo100_DiscoveryHasNoPool()
        {
            var builder = new ContentRegistryBuilder();
            AddSpecies(builder, "g0", Grassland, ThreeColors);
            builder.AddEgg(Egg(Basic, Grassland, EggTier.Basic, new[] { 70.0, 20.0, 6.0, 1.6, 0.36, 0.04 }, new[] { new EggDropEntry(G(0), 1.0) }),
                "egg.test.basic");
            Assert.IsTrue(builder.HasErrors, "A table at 98 % cannot ship.");

            builder = new ContentRegistryBuilder();
            AddSpecies(builder, "g0", Grassland, ThreeColors);
            builder.AddEgg(Egg(Discovery, ContentId.None, EggTier.Discovery, RareTable, new[] { new EggDropEntry(G(0), 1.0) }, 1.0, growth: 1.0),
                "egg.test.discovery");
            Assert.IsTrue(builder.HasErrors, "A Discovery egg picks by its rules, not from a pool.");

            Assert.DoesNotThrow(() => Content());
        }
    }
}
