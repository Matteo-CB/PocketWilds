#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// Multipliers a biome's animals share (doc 02 Améliorations, section 10): Light upgrade x zones x biome mastery.
    /// Read only when the rate is recomputed, never per frame.
    /// </summary>
    public interface IProductionModifiers
    {
        double BiomeProductionMultiplier(ContentId biome);
    }

    /// <summary>
    /// Only gameplay writer of Light and Essence. Production comes from species aggregates:
    /// rate = sum over species of BaseProduction x (sum of Rarity x Bond) x Mastery x Biome x Global, where Biome is
    /// the biome's shared multipliers (<see cref="IProductionModifiers"/>).
    /// Advance is O(1) and deterministic for a given state, delta and config.
    /// </summary>
    public sealed class EconomyService
    {
        private readonly PlayerState _state;
        private readonly ContentRegistry _content;
        private readonly EconomyConfig _config;
        private readonly SpeciesAggregateTable _aggregates;
        private readonly ISaveScheduler? _saveScheduler;

        private BigValue _lightPerSecond;
        private BigValue _globalMultiplier = BigValue.One;
        private IProductionModifiers? _modifiers;
        private bool _rateDirty = true;

        public EconomyService(
            PlayerState state,
            ContentRegistry content,
            EconomyConfig config,
            ISaveScheduler? saveScheduler)
        {
            _state = state;
            _content = content;
            _config = config;
            _saveScheduler = saveScheduler;
            _aggregates = new SpeciesAggregateTable(content.Species.Count, config);
            RebuildAggregates();
        }

        /// <summary>Raised on spend and grant. Production ticks do not raise it; UI polls the wallet at its own rate.</summary>
        public event Action<EconomyChange>? Changed;

        public SpeciesAggregateTable Aggregates => _aggregates;
        public EconomyConfig Config => _config;
        public BigValue Light => _state.Wallet.Light;
        public BigValue GlobalMultiplier => _globalMultiplier;

        /// <summary>Animals ignored because their species is not in the current registry.</summary>
        public int UnknownSpeciesAnimals { get; private set; }

        public BigValue GetLightPerSecond()
        {
            if (_rateDirty)
                RecomputeRate();
            return _lightPerSecond;
        }

        public void SetGlobalMultiplier(BigValue multiplier)
        {
            _globalMultiplier = multiplier;
            _rateDirty = true;
        }

        /// <summary>Biome multipliers (upgrades, zones, biome mastery). Null: none.</summary>
        public void SetModifiers(IProductionModifiers? modifiers)
        {
            _modifiers = modifiers;
            _rateDirty = true;
        }

        /// <summary>Call when mastery levels or other per-species multipliers change.</summary>
        public void MarkProductionDirty() => _rateDirty = true;

        public void RebuildAggregates()
        {
            _aggregates.Rebuild(_state.Collection, _content, out int unknown);
            UnknownSpeciesAnimals = unknown;
            _rateDirty = true;
        }

        public void OnAnimalAdded(in AnimalInstance animal)
        {
            if (_content.Species.TryGetIndex(animal.Species, out int index))
            {
                _aggregates.Add(index, animal.Rarity, animal.BondPoints);
                _rateDirty = true;
            }
        }

        public void OnAnimalRemoved(in AnimalInstance animal)
        {
            if (_content.Species.TryGetIndex(animal.Species, out int index))
            {
                _aggregates.Remove(index, animal.Rarity, animal.BondPoints);
                _rateDirty = true;
            }
        }

        public void OnBondChanged(in AnimalInstance animal, ushort oldBond)
        {
            if (_content.Species.TryGetIndex(animal.Species, out int index))
            {
                _aggregates.ChangeBond(index, animal.Rarity, oldBond, animal.BondPoints);
                _rateDirty = true;
            }
        }

        public void Advance(TimeSpan delta) => Advance(delta.TotalSeconds);

        public void Advance(double seconds)
        {
            if (seconds <= 0.0)
                return;

            using (GameProfilerMarkers.EconomyAdvance.Auto())
            {
                var gain = BigValue.Multiply(GetLightPerSecond(), seconds);
                if (gain.IsZero)
                    return;

                var wallet = _state.Wallet;
                wallet.Light = BigValue.Add(wallet.Light, gain);
                wallet.LifetimeLight = BigValue.Add(wallet.LifetimeLight, gain);
            }
        }

        public bool CanSpend(CurrencyId currency, BigValue amount)
        {
            if (amount.IsNegative)
                return false;
            return Balance(currency) >= amount;
        }

        public bool Spend(CurrencyId currency, BigValue amount, EconomyReason reason)
        {
            if (!CanSpend(currency, amount))
                return false;

            Apply(currency, BigValue.Negate(amount));
            Changed?.Invoke(new EconomyChange(currency, BigValue.Negate(amount), Balance(currency), reason));
            _saveScheduler?.RequestSave(SaveUrgency.Normal);
            return true;
        }

        public void Grant(CurrencyId currency, BigValue amount, EconomyReason reason)
        {
            if (amount.IsNegative)
                throw new ArgumentOutOfRangeException(nameof(amount), "Grant needs a non-negative amount.");
            if (amount.IsZero)
                return;

            Apply(currency, amount);
            if (currency == CurrencyId.Light)
                _state.Wallet.LifetimeLight = BigValue.Add(_state.Wallet.LifetimeLight, amount);

            Changed?.Invoke(new EconomyChange(currency, amount, Balance(currency), reason));
            _saveScheduler?.RequestSave(reason == EconomyReason.Purchase ? SaveUrgency.Priority : SaveUrgency.Normal);
        }

        public BigValue Balance(CurrencyId currency)
        {
            var wallet = _state.Wallet;
            switch (currency)
            {
                case CurrencyId.Light: return wallet.Light;
                case CurrencyId.Essence: return BigValue.FromDouble(wallet.Essence);
                case CurrencyId.Gems: return BigValue.FromDouble(wallet.Gems);
                default: throw new ArgumentOutOfRangeException(nameof(currency));
            }
        }

        private void Apply(CurrencyId currency, BigValue delta)
        {
            var wallet = _state.Wallet;
            switch (currency)
            {
                case CurrencyId.Light:
                    wallet.Light = BigValue.Add(wallet.Light, delta);
                    break;
                case CurrencyId.Essence:
                    wallet.Essence = AddClamped(wallet.Essence, delta);
                    break;
                case CurrencyId.Gems:
                    wallet.Gems = AddClamped(wallet.Gems, delta);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(currency));
            }
        }

        private static long AddClamped(long current, BigValue delta)
        {
            double result = Math.Floor(current + delta.ToDoubleClamped());
            if (result >= long.MaxValue)
                return long.MaxValue;
            if (result <= 0.0)
                return 0;
            return (long)result;
        }

        private void RecomputeRate()
        {
            var total = BigValue.Zero;
            var species = _content.Species;
            for (int i = 0; i < species.Count; i++)
            {
                double sum = _aggregates.GetRarityBondSum(i);
                if (sum <= 0.0)
                    continue;

                var definition = species[i];
                int masteryLevel = _state.Mastery.GetLevel(definition.Id);
                double masteryMultiplier = 1.0 + _config.MasteryPerLevel * masteryLevel;
                double biomeMultiplier = _modifiers != null ? _modifiers.BiomeProductionMultiplier(definition.Biome) : 1.0;
                total = BigValue.Add(total, BigValue.FromDouble(definition.BaseProduction * sum * masteryMultiplier * biomeMultiplier));
            }

            _lightPerSecond = BigValue.Multiply(total, _globalMultiplier);
            _rateDirty = false;
        }
    }
}
