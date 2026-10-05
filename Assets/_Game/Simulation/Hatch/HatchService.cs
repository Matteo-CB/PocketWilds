#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    public enum HatchStatus : byte
    {
        Hatched = 0,
        UnknownEgg = 1,
        NotEnoughLight = 2,
        NoCandidate = 3,
    }

    /// <summary>Immutable outcome handed to presentation. The animal is already saved in the collection.</summary>
    public readonly struct HatchResult
    {
        public readonly HatchStatus Status;
        public readonly AnimalInstance Animal;
        public readonly ContentId Egg;
        public readonly BigValue Cost;
        public readonly RarityTier RolledTier;
        public readonly bool NewSpecies;
        public readonly bool NewVariant;
        public readonly bool WasFree;

        public HatchResult(HatchStatus status, AnimalInstance animal, ContentId egg, BigValue cost, RarityTier rolledTier,
            bool newSpecies, bool newVariant, bool wasFree)
        {
            Status = status;
            Animal = animal;
            Egg = egg;
            Cost = cost;
            RolledTier = rolledTier;
            NewSpecies = newSpecies;
            NewVariant = newVariant;
            WasFree = wasFree;
        }

        public bool Succeeded => Status == HatchStatus.Hatched;

        public static HatchResult Failed(HatchStatus status, ContentId egg, BigValue cost) =>
            new HatchResult(status, default, egg, cost, RarityTier.Common, false, false, false);
    }

    /// <summary>
    /// RequestHatch (Codebase Blueprint): validates and pays the cost, rolls tier, species and variant with
    /// a deterministic RNG, creates the AnimalInstance, updates discoveries, economy and save, then returns
    /// an immutable result for the presentation. Pity (T21), mastery XP (T20) and traits (T26) plug in here.
    /// <list type="bullet">
    /// <item>Cost(n) = round(BaseCost x CostGrowth^n), n = eggs of this type bought this cycle (doc 02).</item>
    /// <item>The very first hatch of a profile is free: the tutorial egg needs a single touch.</item>
    /// <item>The roll seed is derived from the profile and the next instance id: reproducible from the save,
    /// no extra persisted RNG state, never UnityEngine.Random.</item>
    /// </list>
    /// </summary>
    public sealed class HatchService
    {
        private const uint RollSalt = 0x4A7C0001u;

        private readonly PlayerState _state;
        private readonly ContentRegistry _content;
        private readonly EconomyService _economy;
        private readonly IGameClock _clock;
        private readonly ISaveScheduler? _save;
        private readonly uint _profileSeed;

        /// <summary>Variant dense indices per species and rarity tier, built once.</summary>
        private readonly Dictionary<ContentId, int[][]> _variantsBySpeciesTier;

        public HatchService(PlayerState state, ContentRegistry content, EconomyService economy, IGameClock clock, ISaveScheduler? save)
        {
            _state = state;
            _content = content;
            _economy = economy;
            _clock = clock;
            _save = save;
            _profileSeed = ProfileSeed(state.Meta.ProfileId);

            var lists = new Dictionary<ContentId, List<int>[]>();
            var variants = content.Variants;
            for (int i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                if (!lists.TryGetValue(variant.Species, out var tiers))
                {
                    tiers = new List<int>[EggDefinition.RarityTierCount];
                    for (int t = 0; t < tiers.Length; t++)
                        tiers[t] = new List<int>();
                    lists.Add(variant.Species, tiers);
                }
                int tier = Math.Min((int)variant.Rarity, EggDefinition.RarityTierCount - 1);
                tiers[tier].Add(i);
            }
            _variantsBySpeciesTier = new Dictionary<ContentId, int[][]>(lists.Count);
            foreach (var pair in lists)
            {
                var arrays = new int[pair.Value.Length][];
                for (int t = 0; t < arrays.Length; t++)
                    arrays[t] = pair.Value[t].ToArray();
                _variantsBySpeciesTier.Add(pair.Key, arrays);
            }
        }

        /// <summary>The first animal of a profile comes from the tutorial egg, without a cost.</summary>
        public bool IsNextHatchFree => _state.Collection.Count == 0;

        public BigValue GetCost(ContentId eggId)
        {
            if (!_content.Eggs.TryGet(eggId, out var egg))
                return BigValue.Zero;
            _state.World.EggPurchasesInCycle.TryGetValue(eggId, out int bought);
            return Cost(egg, bought);
        }

        public static BigValue Cost(EggDefinition egg, int bought)
        {
            double log10 = Math.Log10(egg.BaseCost) + bought * Math.Log10(egg.CostGrowth);
            if (log10 < 15.0)
                return BigValue.FromDouble(Math.Round(egg.BaseCost * Math.Pow(egg.CostGrowth, bought), MidpointRounding.AwayFromZero));
            long exponent = (long)Math.Floor(log10);
            return BigValue.FromMantissaExponent(Math.Pow(10.0, log10 - exponent), exponent);
        }

        public bool CanHatch(ContentId eggId) =>
            _content.Eggs.TryGet(eggId, out _) && (IsNextHatchFree || _economy.CanSpend(CurrencyId.Light, GetCost(eggId)));

        public HatchResult Hatch(ContentId eggId)
        {
            using (GameProfilerMarkers.HatchResolve.Auto())
            {
                if (!_content.Eggs.TryGet(eggId, out var egg))
                    return HatchResult.Failed(HatchStatus.UnknownEgg, eggId, BigValue.Zero);

                bool free = IsNextHatchFree;
                var cost = free ? BigValue.Zero : GetCost(eggId);

                var rng = new DeterministicRandom(SeedHash.Mix(_profileSeed ^ _state.Collection.NextInstanceId, RollSalt));
                var rolled = RollTier(egg, ref rng);
                if (!TryPickVariant(egg, rolled, ref rng, out int variantIndex))
                    return HatchResult.Failed(HatchStatus.NoCandidate, eggId, cost);

                // Pay only once the result is known to exist: a failed roll never costs Light.
                if (!free)
                {
                    if (!_economy.Spend(CurrencyId.Light, cost, EconomyReason.EggPurchase))
                        return HatchResult.Failed(HatchStatus.NotEnoughLight, eggId, cost);
                    _state.World.EggPurchasesInCycle.TryGetValue(eggId, out int bought);
                    _state.World.EggPurchasesInCycle[eggId] = bought + 1;
                }

                var variant = _content.Variants[variantIndex];
                var animal = new AnimalInstance
                {
                    Species = variant.Species,
                    Variant = variant.Id,
                    Seed = rng.NextUInt(),
                    CreatedUtcSeconds = (uint)Math.Max(0L, _clock.UtcNow.ToUnixTimeSeconds()),
                    Rarity = variant.Rarity,
                };
                animal.InstanceId = _state.Collection.Add(animal);

                bool newSpecies = _state.Collection.DiscoveredSpecies.Add(variant.Species);
                bool newVariant = _state.Collection.DiscoveredVariants.Add(variant.Id);
                _economy.OnAnimalAdded(animal);
                _save?.RequestSave(newSpecies || newVariant || variant.Rarity >= RarityTier.Rare ? SaveUrgency.Priority : SaveUrgency.Normal);
                return new HatchResult(HatchStatus.Hatched, animal, eggId, cost, rolled, newSpecies, newVariant, free);
            }
        }

        private static RarityTier RollTier(EggDefinition egg, ref DeterministicRandom rng)
        {
            var weights = egg.RarityWeights;
            double total = 0.0;
            for (int i = 0; i < weights.Length; i++)
                total += weights[i];
            double roll = rng.NextDouble() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0.0)
                    return (RarityTier)i;
            }
            return RarityTier.Common;
        }

        /// <summary>
        /// Species by pool weight, then a variant of the rolled tier. A species without that tier falls back
        /// to its nearest lower tier (then higher): the roll describes a tier, never a promise of a species.
        /// </summary>
        private bool TryPickVariant(EggDefinition egg, RarityTier tier, ref DeterministicRandom rng, out int variantIndex)
        {
            variantIndex = -1;
            var pool = egg.Pool;
            double total = 0.0;
            for (int i = 0; i < pool.Length; i++)
                if (_variantsBySpeciesTier.ContainsKey(pool[i].Species))
                    total += pool[i].Weight;
            if (total <= 0.0)
                return false;

            double roll = rng.NextDouble() * total;
            ContentId species = default;
            for (int i = 0; i < pool.Length; i++)
            {
                if (!_variantsBySpeciesTier.ContainsKey(pool[i].Species))
                    continue;
                species = pool[i].Species;
                roll -= pool[i].Weight;
                if (roll < 0.0)
                    break;
            }

            var tiers = _variantsBySpeciesTier[species];
            int start = (int)tier;
            for (int offset = 0; offset < tiers.Length * 2; offset++)
            {
                // start, start-1, ..., 0, then start+1, start+2, ...
                int t = offset <= start ? start - offset : offset;
                if (t < 0 || t >= tiers.Length)
                    continue;
                var candidates = tiers[t];
                if (candidates.Length == 0)
                    continue;
                variantIndex = candidates[rng.Next(candidates.Length)];
                return true;
            }
            return false;
        }

        private static uint ProfileSeed(Guid profileId)
        {
            var bytes = profileId.ToByteArray();
            uint seed = 0x9E3779B9u;
            for (int i = 0; i < 16; i += 4)
                seed = SeedHash.Mix(seed ^ BitConverter.ToUInt32(bytes, i));
            return seed;
        }
    }
}
