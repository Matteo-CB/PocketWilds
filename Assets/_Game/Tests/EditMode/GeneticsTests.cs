using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T26 genetics prototype: two parents and a seed give a child genome (palette, carried pattern, traits,
    /// size). Pure and deterministic; the preview odds are the odds the breeding samples.
    /// </summary>
    public class GeneticsTests
    {
        private static readonly ContentId Rabbit = ContentId.FromSource("species.rabbit");
        private static readonly ContentId Squirrel = ContentId.FromSource("species.squirrel");

        private static ContentRegistry RealContent()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            return database.CreateBuilder().Build();
        }

        private static AnimalInstance Animal(uint id, ContentId species, string variant, uint seed, uint bits = 0u) => new AnimalInstance
        {
            InstanceId = id, Species = species, Variant = ContentId.FromSource(variant), Seed = seed, TraitBits = bits,
        };

        [Test]
        public void Genome_EncodesAndDecodes_ZeroIsAPlainAnimal()
        {
            Assert.AreEqual(1f, AnimalGenome.Scale(0u));
            Assert.AreEqual(PatternChannel.None, AnimalGenome.CarriedPattern(0u));
            Assert.AreEqual(AnimalTrait.None, AnimalGenome.Traits(0u));
            for (int size = AnimalGenome.MinSize; size <= AnimalGenome.MaxSize; size++)
            {
                uint bits = AnimalGenome.WithPattern(AnimalGenome.WithSize(AnimalGenome.WithTraits(0u, AnimalTrait.Shiny | AnimalTrait.Albino), size), PatternChannel.MaskB, 11);
                Assert.AreEqual(size, AnimalGenome.Size(bits));
                Assert.AreEqual(PatternChannel.MaskB, AnimalGenome.CarriedPattern(bits));
                Assert.AreEqual(11, AnimalGenome.CarriedStrength(bits));
                Assert.AreEqual(AnimalTrait.Shiny | AnimalTrait.Albino, AnimalGenome.Traits(bits));
            }
            Assert.AreEqual(1f + 4 * AnimalGenome.SizeStep, AnimalGenome.Scale(AnimalGenome.WithSize(0u, 4)), 1e-6f);
            Assert.AreEqual(AnimalGenome.TinyScale, AnimalGenome.Scale(AnimalGenome.WithTraits(0u, AnimalTrait.Tiny)), 1e-6f);
            Assert.AreEqual(AnimalGenome.MaxSize, AnimalGenome.Size(AnimalGenome.WithSize(0u, 40)), "Clamped to the small range.");
        }

        [Test]
        public void Breed_IsDeterministic_AndSymmetric()
        {
            var genetics = new GeneticsService(RealContent());
            var a = Animal(1, Rabbit, "variant.rabbit.brown", 0xA11CEu, AnimalGenome.WithSize(0u, 3));
            var b = Animal(2, Rabbit, "variant.rabbit.spotted", 0xB0Bu, AnimalGenome.WithTraits(0u, AnimalTrait.Shiny));
            var seeds = new HashSet<uint>();
            for (uint context = 0; context < 200; context++)
            {
                Assert.AreEqual(BreedCheck.Ok, genetics.TryBreed(a, b, context, out var first));
                genetics.TryBreed(a, b, context, out var again);
                genetics.TryBreed(b, a, context, out var swapped);
                Assert.AreEqual(first.Seed, again.Seed);
                Assert.AreEqual(first.Variant, again.Variant);
                Assert.AreEqual(first.TraitBits, again.TraitBits);
                Assert.AreEqual(first.Seed, swapped.Seed, "Parent order does not matter.");
                Assert.AreEqual(first.Variant, swapped.Variant);
                Assert.AreEqual(first.TraitBits, swapped.TraitBits);
                seeds.Add(first.Seed);
            }
            Assert.Greater(seeds.Count, 195, "Each context gives another child.");
        }

        [Test]
        public void Lineage_OverTenGenerations_IsReproducible()
        {
            uint Run()
            {
                var genetics = new GeneticsService(RealContent());
                var a = Animal(1, Rabbit, "variant.rabbit.white", 11u);
                var b = Animal(2, Rabbit, "variant.rabbit.sakura", 22u, AnimalGenome.WithTraits(0u, AnimalTrait.Golden));
                uint hash = 17u;
                for (uint generation = 0; generation < 10; generation++)
                {
                    genetics.TryBreed(a, b, generation * 7919u, out var child);
                    var animal = GeneticsService.CreateChild(child, 0u);
                    animal.InstanceId = 100 + generation;
                    hash = SeedHash.Mix(hash ^ child.Seed ^ child.TraitBits ^ child.Variant.Value);
                    a = b;
                    b = animal;
                }
                return hash;
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void Compatibility_SameSpeciesAndTwoDifferentAnimals()
        {
            var genetics = new GeneticsService(RealContent());
            var rabbit = Animal(1, Rabbit, "variant.rabbit.brown", 1u);
            Assert.AreEqual(BreedCheck.SameAnimal, genetics.TryBreed(rabbit, rabbit, 0u, out _));
            Assert.AreEqual(BreedCheck.DifferentSpecies, genetics.TryBreed(rabbit, Animal(2, Squirrel, "variant.squirrel.red", 2u), 0u, out _));
            var ghost = Animal(3, ContentId.FromSource("species.ghost"), "variant.ghost.a", 3u);
            Assert.AreEqual(BreedCheck.UnknownSpecies, genetics.TryBreed(ghost, Animal(4, ghost.Species, "variant.ghost.a", 4u), 0u, out _));
        }

        [Test]
        public void Breeding_FollowsThePreviewOdds()
        {
            var content = RealContent();
            var genetics = new GeneticsService(content);
            var a = Animal(1, Rabbit, "variant.rabbit.brown", 5u, AnimalGenome.WithTraits(0u, AnimalTrait.Shiny));
            var b = Animal(2, Rabbit, "variant.rabbit.grey", 6u, AnimalGenome.WithTraits(0u, AnimalTrait.Shiny | AnimalTrait.Tiny));
            var odds = new List<VariantChance>();
            Assert.AreEqual(BreedCheck.Ok, genetics.Preview(a, b, odds, out var preview));
            double sum = 0.0;
            foreach (var o in odds) sum += o.Chance;
            Assert.AreEqual(1.0, sum, 1e-9, "The odds cover every outcome.");
            Assert.AreEqual(1.0 - 2 * genetics.Config.ParentVariantShare, preview.MutationChance, 1e-9);

            const int N = 200000;
            var counts = new Dictionary<ContentId, int>();
            int mutated = 0, shiny = 0, tiny = 0, giant = 0, rare = 0;
            for (uint i = 0; i < N; i++)
            {
                genetics.TryBreed(a, b, i, out var child);
                counts.TryGetValue(child.Variant, out int c);
                counts[child.Variant] = c + 1;
                if (child.Mutated) mutated++;
                var traits = AnimalGenome.Traits(child.TraitBits);
                if ((traits & AnimalTrait.Shiny) != 0) shiny++;
                if ((traits & AnimalTrait.Tiny) != 0) tiny++;
                if ((traits & AnimalTrait.Giant) != 0) giant++;
                if (traits != AnimalTrait.None) rare++;
                Assert.IsFalse((traits & AnimalTrait.Tiny) != 0 && (traits & AnimalTrait.Giant) != 0, "Tiny and Giant exclude each other.");
                Assert.IsFalse((traits & AnimalTrait.Albino) != 0 && (traits & AnimalTrait.Melanistic) != 0);
            }
            foreach (var o in odds)
            {
                counts.TryGetValue(o.Variant, out int c);
                double sigma = System.Math.Sqrt(o.Chance * (1 - o.Chance) / N);
                Assert.AreEqual(o.Chance, c / (double)N, 5 * sigma + 1e-4, o.Variant.ToString());
            }
            Assert.AreEqual(preview.MutationChance, mutated / (double)N, 0.004);
            Assert.AreEqual(genetics.Config.TraitBoth, shiny / (double)N, 0.004, "Both parents shiny.");
            Assert.AreEqual(genetics.Config.TraitOne, tiny / (double)N, 0.004, "One parent tiny.");
            Assert.AreEqual(genetics.EffectiveTraitChance(1, a.TraitBits, b.TraitBits), giant / (double)N, 0.0015);
            Assert.AreEqual(preview.RareTraitChance, rare / (double)N, 0.004);
        }

        [Test]
        public void SameVariantParents_UsuallyBreedTrue()
        {
            var genetics = new GeneticsService(RealContent());
            var a = Animal(1, Rabbit, "variant.rabbit.white", 7u);
            var b = Animal(2, Rabbit, "variant.rabbit.white", 8u);
            int same = 0;
            for (uint i = 0; i < 20000; i++)
            {
                genetics.TryBreed(a, b, i, out var child);
                if (child.Variant == a.Variant) same++;
            }
            Assert.AreEqual(genetics.Config.SameVariantKeep, same / 20000.0, 0.01);
        }

        [Test]
        public void Mutations_NeverGiveASecretVariant_NorAParentsVariant()
        {
            var builder = new ContentRegistryBuilder();
            TestUtil.AddSpecies(builder, "species.rabbit", 1.0);
            void Add(string name, RarityTier rarity, bool secret = false) =>
                builder.AddVariant(new VariantDefinition(ContentId.FromSource("variant.rabbit." + name), Rabbit, name, rarity,
                    new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0, secret), "variant.rabbit." + name);
            Add("brown", RarityTier.Common);
            Add("white", RarityTier.Common);
            Add("grey", RarityTier.Uncommon);
            Add("moon", RarityTier.Uncommon, secret: true);
            var genetics = new GeneticsService(builder.Build());
            var a = Animal(1, Rabbit, "variant.rabbit.brown", 1u);
            var b = Animal(2, Rabbit, "variant.rabbit.white", 2u);
            var moon = ContentId.FromSource("variant.rabbit.moon");
            int grey = 0;
            for (uint i = 0; i < 20000; i++)
            {
                genetics.TryBreed(a, b, i, out var child);
                Assert.AreNotEqual(moon, child.Variant, "Secrets are never a mutation.");
                if (child.Mutated)
                {
                    Assert.AreNotEqual(a.Variant, child.Variant);
                    Assert.AreNotEqual(b.Variant, child.Variant);
                    grey++;
                }
            }
            Assert.Greater(grey, 0, "The only mutation left is the grey one.");
        }

        [Test]
        public void Pattern_IsCarriedFromAPatternedParent_AndShowsOnAPlainPalette()
        {
            var content = RealContent();
            var genetics = new GeneticsService(content);
            var spotted = Animal(1, Rabbit, "variant.rabbit.spotted", 9u);
            var brown = Animal(2, Rabbit, "variant.rabbit.brown", 10u);
            Assert.AreEqual(BreedCheck.Ok, genetics.Preview(spotted, brown, null, out var preview));
            Assert.AreEqual(genetics.Config.PatternOne, preview.PatternChance, 1e-9);
            int carried = 0, shown = 0;
            using (var palettes = new PaletteTable(content))
            {
                var resolver = new AnimalVisualResolver(content, palettes);
                for (uint i = 0; i < 20000; i++)
                {
                    genetics.TryBreed(spotted, brown, i, out var child);
                    if (AnimalGenome.CarriedPattern(child.TraitBits) == PatternChannel.None)
                        continue;
                    carried++;
                    Assert.AreEqual(PatternChannel.MaskA, AnimalGenome.CarriedPattern(child.TraitBits), "Rabbit spots are the carried pattern.");
                    if (child.Variant != brown.Variant || shown > 0)
                        continue;
                    var animal = GeneticsService.CreateChild(child, 0u);
                    resolver.TryResolve(animal, Vector3.zero, 0f, out var snapshot);
                    Assert.AreEqual(PatternChannel.MaskA, AnimalVisualPacking.Pattern(snapshot.VisualData), "Brown child shows the spots tone on tone.");
                    Assert.AreEqual(AnimalGenome.CarriedStrength(child.TraitBits), AnimalVisualPacking.PatternStrength(snapshot.VisualData));
                    shown++;
                }
            }
            Assert.AreEqual(genetics.Config.PatternOne, carried / 20000.0, 0.012);
            Assert.AreEqual(1, shown);
        }

        [Test]
        public void Size_FollowsTheParents_WithinTheSmallRange()
        {
            var genetics = new GeneticsService(RealContent());
            var big = Animal(1, Rabbit, "variant.rabbit.brown", 1u, AnimalGenome.WithSize(0u, 6));
            var bigToo = Animal(2, Rabbit, "variant.rabbit.brown", 2u, AnimalGenome.WithSize(0u, 6));
            var small = Animal(3, Rabbit, "variant.rabbit.brown", 3u, AnimalGenome.WithSize(0u, -6));
            double average = 0.0;
            for (uint i = 0; i < 4000; i++)
            {
                genetics.TryBreed(big, bigToo, i, out var child);
                int size = AnimalGenome.Size(child.TraitBits);
                Assert.That(size, NUnit.Framework.Is.InRange(5, 6));
                genetics.TryBreed(big, small, i, out var mixed);
                average += AnimalGenome.Size(mixed.TraitBits);
            }
            Assert.AreEqual(0.0, average / 4000.0, 0.1, "A big and a small parent average out.");
        }

        [Test]
        public void Breed_DoesNotAllocate()
        {
            var genetics = new GeneticsService(RealContent());
            var a = Animal(1, Rabbit, "variant.rabbit.brown", 1u);
            var b = Animal(2, Rabbit, "variant.rabbit.spotted", 2u);
            genetics.TryBreed(a, b, 0u, out _);
            Assert.That(() =>
            {
                for (uint i = 0; i < 1000; i++)
                    genetics.TryBreed(a, b, i, out _);
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
