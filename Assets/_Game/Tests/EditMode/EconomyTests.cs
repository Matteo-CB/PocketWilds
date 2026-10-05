using System;
using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    public class EconomyTests
    {
        private static readonly ContentId Rabbit = ContentId.FromSource("species.rabbit");
        private static readonly ContentId Squirrel = ContentId.FromSource("species.squirrel");
        private static readonly ContentId Hedgehog = ContentId.FromSource("species.hedgehog");

        private sealed class SaveSpy : ISaveScheduler
        {
            public readonly List<SaveUrgency> Requests = new List<SaveUrgency>();
            public void RequestSave(SaveUrgency urgency) => Requests.Add(urgency);
        }

        private static AnimalInstance Animal(ContentId species, RarityTier rarity = RarityTier.Common, ushort bond = 0) =>
            new AnimalInstance { Species = species, Rarity = rarity, BondPoints = bond, Seed = 1 };

        private static (PlayerState state, EconomyService economy) Create(params AnimalInstance[] animals)
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            foreach (var a in animals)
                state.Collection.Add(a);
            var economy = new EconomyService(state, TestUtil.ThreeSpecies(), EconomyConfig.Default, null);
            return (state, economy);
        }

        private static void AddLive(PlayerState state, EconomyService economy, AnimalInstance animal)
        {
            uint id = state.Collection.Add(animal);
            state.Collection.TryGet(id, out var stored);
            economy.OnAnimalAdded(stored);
        }

        [Test]
        public void Rate_SumsAggregates_WithRarityMasteryBondAndGlobal()
        {
            var (state, economy) = Create(Animal(Rabbit), Animal(Rabbit));
            Assert.AreEqual(2.0, economy.GetLightPerSecond().ToDoubleClamped(), 1e-12);

            AddLive(state, economy, Animal(Rabbit, RarityTier.Rare));
            Assert.AreEqual(3.5, economy.GetLightPerSecond().ToDoubleClamped(), 1e-12);

            AddLive(state, economy, Animal(Squirrel, RarityTier.Common, bond: 1000));
            Assert.AreEqual(3.5 + 1.5 * 1.2, economy.GetLightPerSecond().ToDoubleClamped(), 1e-12);

            state.Mastery.Species[Rabbit] = new SpeciesMastery { Level = 10 };
            economy.MarkProductionDirty();
            Assert.AreEqual(3.5 * 1.15 + 1.8, economy.GetLightPerSecond().ToDoubleClamped(), 1e-12);

            economy.SetGlobalMultiplier(BigValue.FromDouble(2));
            Assert.AreEqual((3.5 * 1.15 + 1.8) * 2, economy.GetLightPerSecond().ToDoubleClamped(), 1e-12);
        }

        [Test]
        public void IncrementalAggregates_MatchFullRebuild()
        {
            var (state, economy) = Create();
            var rng = new Random(7);
            for (int i = 0; i < 500; i++)
            {
                var species = i % 3 == 0 ? Rabbit : (i % 3 == 1 ? Squirrel : Hedgehog);
                AddLive(state, economy, Animal(species, (RarityTier)rng.Next(0, 6), (ushort)rng.Next(0, 1200)));
            }
            for (int i = 0; i < 100; i++)
            {
                var animal = state.Collection[rng.Next(0, state.Collection.Count)];
                state.Collection.Remove(animal.InstanceId);
                economy.OnAnimalRemoved(animal);
            }

            double incremental = economy.GetLightPerSecond().ToDoubleClamped();
            economy.RebuildAggregates();
            double rebuilt = economy.GetLightPerSecond().ToDoubleClamped();
            Assert.AreEqual(rebuilt, incremental, rebuilt * 1e-12);
        }

        [Test]
        public void UnknownSpecies_IsKept_ButDoesNotProduce()
        {
            var (state, economy) = Create(Animal(Rabbit), Animal(ContentId.FromSource("species.future_dragon")));
            Assert.AreEqual(1, economy.UnknownSpeciesAnimals);
            Assert.AreEqual(2, state.Collection.Count);
            Assert.AreEqual(1.0, economy.GetLightPerSecond().ToDoubleClamped(), 1e-12);
        }

        [Test]
        public void Advance_AddsProduction_AndLifetime()
        {
            var (state, economy) = Create(Animal(Rabbit), Animal(Hedgehog));
            economy.Advance(TimeSpan.FromSeconds(10));
            Assert.AreEqual(30.0, state.Wallet.Light.ToDoubleClamped(), 1e-9);
            Assert.AreEqual(30.0, state.Wallet.LifetimeLight.ToDoubleClamped(), 1e-9);
            economy.Advance(0.0);
            economy.Advance(-5.0);
            Assert.AreEqual(30.0, state.Wallet.Light.ToDoubleClamped(), 1e-9);
        }

        [Test]
        public void OneDaySimulation_IsDeterministic_AndStepInvariant()
        {
            BigValue RunDay(int stepSeconds)
            {
                var (state, economy) = Create(Animal(Rabbit), Animal(Squirrel, RarityTier.Epic, 300), Animal(Hedgehog, RarityTier.Fabulous));
                state.Mastery.Species[Squirrel] = new SpeciesMastery { Level = 7 };
                economy.MarkProductionDirty();
                for (int t = 0; t < 86400; t += stepSeconds)
                    economy.Advance(stepSeconds);
                return state.Wallet.Light;
            }

            var first = RunDay(1);
            var second = RunDay(1);
            Assert.AreEqual(first, second, "Same state + delta + config must give the exact same result.");

            var oneStep = BigValue.Zero;
            {
                var (state, economy) = Create(Animal(Rabbit), Animal(Squirrel, RarityTier.Epic, 300), Animal(Hedgehog, RarityTier.Fabulous));
                state.Mastery.Species[Squirrel] = new SpeciesMastery { Level = 7 };
                economy.MarkProductionDirty();
                economy.Advance(TimeSpan.FromDays(1));
                oneStep = state.Wallet.Light;
            }
            Assert.IsTrue(BigValue.ApproximatelyEqual(first, oneStep, 1e-9), $"{first} vs {oneStep}");
            Assert.IsTrue(BigValue.ApproximatelyEqual(first, RunDay(60), 1e-9));
        }

        [Test]
        public void Spend_And_Grant_RaiseChanges_WithReasons()
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            var spy = new SaveSpy();
            var economy = new EconomyService(state, TestUtil.ThreeSpecies(), EconomyConfig.Default, spy);
            var changes = new List<EconomyChange>();
            economy.Changed += changes.Add;

            economy.Grant(CurrencyId.Light, BigValue.FromDouble(100), EconomyReason.Reward);
            Assert.IsTrue(economy.CanSpend(CurrencyId.Light, BigValue.FromDouble(100)));
            Assert.IsFalse(economy.Spend(CurrencyId.Light, BigValue.FromDouble(101), EconomyReason.EggPurchase));
            Assert.IsTrue(economy.Spend(CurrencyId.Light, BigValue.FromDouble(10), EconomyReason.EggPurchase));
            Assert.AreEqual(90.0, state.Wallet.Light.ToDoubleClamped(), 1e-9);
            Assert.AreEqual(100.0, state.Wallet.LifetimeLight.ToDoubleClamped(), 1e-9, "Spending never reduces lifetime.");

            economy.Grant(CurrencyId.Gems, BigValue.FromDouble(500), EconomyReason.Purchase);
            Assert.AreEqual(500, state.Wallet.Gems);
            Assert.IsFalse(economy.Spend(CurrencyId.Gems, BigValue.FromDouble(501), EconomyReason.Upgrade));
            Assert.IsTrue(economy.Spend(CurrencyId.Gems, BigValue.FromDouble(200), EconomyReason.Upgrade));
            Assert.AreEqual(300, state.Wallet.Gems);

            Assert.AreEqual(4, changes.Count);
            Assert.AreEqual(EconomyReason.EggPurchase, changes[1].Reason);
            Assert.AreEqual(-10.0, changes[1].Delta.ToDoubleClamped(), 1e-9);
            Assert.Contains(SaveUrgency.Priority, spy.Requests, "Purchases must be saved with priority.");
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.Grant(CurrencyId.Light, BigValue.FromDouble(-1), EconomyReason.Debug));
        }

        [Test]
        public void Advance_DoesNotAllocate()
        {
            var (state, economy) = Create(Animal(Rabbit), Animal(Squirrel));
            economy.Advance(0.1);
            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                    economy.Advance(0.1);
            }, Is.Not.AllocatingGCMemory());
            Assert.Greater(state.Wallet.Light.ToDoubleClamped(), 0.0);
        }

        [Test]
        public void Formulas_MatchDesignDocument()
        {
            var config = EconomyConfig.Default;
            Assert.AreEqual(10.0, EconomyFormulas.EggCost(10, 1.18, 0).ToDoubleClamped());
            Assert.AreEqual(12.0, EconomyFormulas.EggCost(10, 1.18, 1).ToDoubleClamped());
            Assert.AreEqual(14.0, EconomyFormulas.EggCost(10, 1.18, 2).ToDoubleClamped());
            Assert.AreEqual(52.0, EconomyFormulas.EggCost(10, 1.18, 10).ToDoubleClamped());
            var far = EconomyFormulas.EggCost(10, 1.18, 5000);
            Assert.AreEqual(360, far.Exponent10);

            Assert.AreEqual(0, EconomyFormulas.MasteryXpForLevel(config, 0));
            Assert.AreEqual(20, EconomyFormulas.MasteryXpForLevel(config, 1));
            Assert.AreEqual(51, EconomyFormulas.MasteryXpForLevel(config, 2));
            Assert.AreEqual(176, EconomyFormulas.MasteryXpForLevel(config, 5));
            Assert.AreEqual(448, EconomyFormulas.MasteryXpForLevel(config, 10));

            Assert.AreEqual(994.0, EconomyFormulas.ExpansionCost(BigValue.FromDouble(100), 2.15, 3).ToDoubleClamped());
            Assert.AreEqual(336, EconomyFormulas.RenewalEssence(config, BigValue.FromDouble(1e12), 1e6, 5));

            var production = EconomyFormulas.AnimalProduction(config, 1.0, RarityTier.Legendary, 10, 500, BigValue.FromDouble(2));
            Assert.AreEqual(1.0 * 3.0 * 1.15 * 1.1 * 2, production.ToDoubleClamped(), 1e-12);
        }
    }
}
