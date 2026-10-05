using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T17: hatch economy (doc 02 cost curve), free tutorial egg, deterministic rolls, save roundtrip.</summary>
    public class HatchServiceTests
    {
        private static readonly ContentId Basic = ContentId.FromSource("egg.grassland.basic");
        private static readonly ContentId Rabbit = ContentId.FromSource("species.rabbit");

        private sealed class SaveSpy : ISaveScheduler
        {
            public int Normal;
            public int Priority;

            public void RequestSave(SaveUrgency urgency)
            {
                if (urgency == SaveUrgency.Priority)
                    Priority++;
                else
                    Normal++;
            }
        }

        private sealed class Fixture
        {
            public PlayerState State;
            public EconomyService Economy;
            public HatchService Hatch;
            public SaveSpy Save;
        }

        private static ContentRegistry Content()
        {
            var builder = new ContentRegistryBuilder();
            TestUtil.AddSpecies(builder, "species.rabbit", 1.0);
            var variants = new[]
            {
                ("brown", RarityTier.Common), ("white", RarityTier.Common), ("grey", RarityTier.Uncommon),
                ("spotted", RarityTier.Rare), ("black", RarityTier.Epic), ("sakura", RarityTier.Legendary),
            };
            foreach (var (name, rarity) in variants)
            {
                string id = "variant.rabbit." + name;
                builder.AddVariant(new VariantDefinition(ContentId.FromSource(id), Rabbit, id + ".name", rarity,
                    new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0), id);
            }
            builder.AddEgg(new EggDefinition(Basic, "egg.grassland.basic.name", ContentId.FromSource("biome.grassland"), EggTier.Basic,
                new Color32[EggDefinition.PaletteSize], 15, 0, 15, "PF_Egg_Base", 10.0, 1.18,
                new[] { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 }, new[] { new EggDropEntry(Rabbit, 1.0) }), "egg.grassland.basic");
            return builder.Build();
        }

        private static Fixture Create(ContentRegistry content, PlayerState state = null)
        {
            var fixture = new Fixture { State = state ?? PlayerState.CreateNew(TestUtil.T0), Save = new SaveSpy() };
            fixture.Economy = new EconomyService(fixture.State, content, EconomyConfig.Default, null);
            fixture.Hatch = new HatchService(fixture.State, content, fixture.Economy, new FakeGameClock(TestUtil.T0), fixture.Save);
            return fixture;
        }

        [Test]
        public void Cost_FollowsDocCurve_AndStaysFiniteForHugeCounts()
        {
            var egg = Content().Eggs.Get(Basic);
            Assert.AreEqual(10.0, HatchService.Cost(egg, 0).ToDoubleClamped());
            Assert.AreEqual(12.0, HatchService.Cost(egg, 1).ToDoubleClamped());
            Assert.AreEqual(14.0, HatchService.Cost(egg, 2).ToDoubleClamped());
            Assert.AreEqual(23.0, HatchService.Cost(egg, 5).ToDoubleClamped());

            var huge = HatchService.Cost(egg, 5000);
            double expectedLog10 = 1.0 + 5000 * System.Math.Log10(1.18);
            Assert.AreEqual((long)System.Math.Floor(expectedLog10), huge.Exponent10);
            Assert.AreEqual(System.Math.Pow(10.0, expectedLog10 - huge.Exponent10), huge.Mantissa, 1e-6);
        }

        [Test]
        public void FirstHatch_IsFree_AndDiscoversTheSpecies()
        {
            var f = Create(Content());
            Assert.IsTrue(f.Hatch.IsNextHatchFree);
            Assert.IsTrue(f.Hatch.CanHatch(Basic), "The tutorial egg needs no Light.");

            var result = f.Hatch.Hatch(Basic);
            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(result.WasFree);
            Assert.IsTrue(result.NewSpecies);
            Assert.IsTrue(result.NewVariant);
            Assert.AreEqual(1, f.State.Collection.Count);
            Assert.AreEqual(0.0, f.Economy.Light.ToDoubleClamped());
            Assert.IsFalse(f.State.World.EggPurchasesInCycle.ContainsKey(Basic), "A free egg does not raise the price.");
            Assert.AreEqual(10.0, f.Hatch.GetCost(Basic).ToDoubleClamped());
            Assert.AreEqual(1, f.Save.Priority, "A new species is saved with priority.");
            Assert.IsFalse(f.Hatch.IsNextHatchFree);
            Assert.IsTrue(f.State.Collection.DiscoveredSpecies.Contains(Rabbit));
            Assert.Greater(f.Economy.GetLightPerSecond().ToDoubleClamped(), 0.0, "The new animal produces Light.");
        }

        [Test]
        public void NotEnoughLight_ChangesNothing()
        {
            var f = Create(Content());
            f.Hatch.Hatch(Basic);
            f.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(9), EconomyReason.Reward);
            uint nextId = f.State.Collection.NextInstanceId;
            int saves = f.Save.Normal + f.Save.Priority;

            Assert.IsFalse(f.Hatch.CanHatch(Basic));
            var result = f.Hatch.Hatch(Basic);
            Assert.AreEqual(HatchStatus.NotEnoughLight, result.Status);
            Assert.AreEqual(10.0, result.Cost.ToDoubleClamped());
            Assert.AreEqual(1, f.State.Collection.Count);
            Assert.AreEqual(nextId, f.State.Collection.NextInstanceId);
            Assert.AreEqual(9.0, f.Economy.Light.ToDoubleClamped());
            Assert.IsFalse(f.State.World.EggPurchasesInCycle.ContainsKey(Basic));
            Assert.AreEqual(saves, f.Save.Normal + f.Save.Priority);
        }

        [Test]
        public void PaidHatch_SpendsCost_AndRaisesNextPrice()
        {
            var f = Create(Content());
            f.Hatch.Hatch(Basic);
            f.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(25), EconomyReason.Reward);

            var result = f.Hatch.Hatch(Basic);
            Assert.IsTrue(result.Succeeded);
            Assert.IsFalse(result.WasFree);
            Assert.AreEqual(10.0, result.Cost.ToDoubleClamped());
            Assert.AreEqual(15.0, f.Economy.Light.ToDoubleClamped(), 1e-9);
            Assert.AreEqual(1, f.State.World.EggPurchasesInCycle[Basic]);
            Assert.AreEqual(12.0, f.Hatch.GetCost(Basic).ToDoubleClamped());
            Assert.AreEqual(2, f.State.Collection.Count);
            Assert.AreEqual(HatchStatus.UnknownEgg, f.Hatch.Hatch(ContentId.FromSource("egg.unknown")).Status);
        }

        [Test]
        public void Rolls_AreDeterministic_AndSurviveASaveRoundtrip()
        {
            var content = Content();
            var a = Create(content);
            a.Economy.Grant(CurrencyId.Light, BigValue.FromMantissaExponent(1.0, 30), EconomyReason.Reward);
            for (int i = 0; i < 12; i++)
                Assert.IsTrue(a.Hatch.Hatch(Basic).Succeeded);

            var serializer = new BinarySaveSerializer();
            var writer = new ByteBufferWriter(64 * 1024);
            serializer.Serialize(a.State, writer);
            var bytes = writer.WrittenSpan.ToArray();
            var b = Create(content, serializer.Deserialize(serializer.ReadDocument(bytes, bytes.Length)));

            Assert.AreEqual(a.State.Collection.Count, b.State.Collection.Count);
            Assert.AreEqual(a.Hatch.GetCost(Basic), b.Hatch.GetCost(Basic), "Purchases this cycle are saved.");
            Assert.AreEqual(a.Economy.Light, b.Economy.Light);
            for (int i = 0; i < 20; i++)
            {
                var ra = a.Hatch.Hatch(Basic);
                var rb = b.Hatch.Hatch(Basic);
                Assert.AreEqual(ra.Animal.Variant, rb.Animal.Variant, "Same profile, same instance id: same roll.");
                Assert.AreEqual(ra.Animal.Seed, rb.Animal.Seed);
                Assert.AreEqual(ra.RolledTier, rb.RolledTier);
            }

            var other = Create(content);
            other.Economy.Grant(CurrencyId.Light, BigValue.FromMantissaExponent(1.0, 30), EconomyReason.Reward);
            int differences = 0;
            for (int i = 0; i < 32; i++)
            {
                if (other.Hatch.Hatch(Basic).Animal.Seed != a.State.Collection[i].Seed)
                    differences++;
            }
            Assert.Greater(differences, 28, "Another profile rolls differently.");
        }

        [Test]
        public void RarityDistribution_MatchesEggWeights_WithLowerTierFallback()
        {
            var f = Create(Content());
            f.Economy.Grant(CurrencyId.Light, BigValue.FromMantissaExponent(1.0, 2000), EconomyReason.Reward);
            const int Samples = 20000;
            var rolled = new int[EggDefinition.RarityTierCount];
            var variants = new Dictionary<ContentId, int>();
            for (int i = 0; i < Samples; i++)
            {
                var result = f.Hatch.Hatch(Basic);
                Assert.IsTrue(result.Succeeded);
                rolled[(int)result.RolledTier]++;
                Assert.LessOrEqual(result.Animal.Rarity, result.RolledTier, "Fallback goes to a lower tier first.");
                variants.TryGetValue(result.Animal.Variant, out int count);
                variants[result.Animal.Variant] = count + 1;
            }

            Assert.AreEqual(0.72, rolled[0] / (double)Samples, 0.015);
            Assert.AreEqual(0.20, rolled[1] / (double)Samples, 0.012);
            Assert.AreEqual(0.06, rolled[2] / (double)Samples, 0.007);
            Assert.AreEqual(0.016, rolled[3] / (double)Samples, 0.004);
            // Both common rabbits share the common tier.
            int brown = variants[ContentId.FromSource("variant.rabbit.brown")];
            int white = variants[ContentId.FromSource("variant.rabbit.white")];
            Assert.AreEqual(1.0, brown / (double)white, 0.08);
            Assert.AreEqual(Samples + 0, f.State.Collection.Count);
        }
    }
}
