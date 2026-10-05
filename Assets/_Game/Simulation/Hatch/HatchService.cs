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

    /// <summary>Which bad-luck protection shaped a hatch (analytics, tests). Stable values.</summary>
    public enum PityOutcome : byte
    {
        None = 0,

        /// <summary>The new species bonus chance picked a species still missing.</summary>
        SpeciesBonus = 1,

        /// <summary>The hard limit was reached: a missing species was guaranteed.</summary>
        SpeciesGuarantee = 2,

        /// <summary>The new variant bonus picked a missing variant of the same rarity.</summary>
        VariantBonus = 3,
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

        /// <summary>Species XP from this hatch (duplicates included): no hatch is ever wasted.</summary>
        public readonly MasteryGain Mastery;

        public readonly PityOutcome Pity;

        public HatchResult(HatchStatus status, AnimalInstance animal, ContentId egg, BigValue cost, RarityTier rolledTier,
            bool newSpecies, bool newVariant, bool wasFree)
            : this(status, animal, egg, cost, rolledTier, newSpecies, newVariant, wasFree, default, PityOutcome.None)
        {
        }

        public HatchResult(HatchStatus status, AnimalInstance animal, ContentId egg, BigValue cost, RarityTier rolledTier,
            bool newSpecies, bool newVariant, bool wasFree, MasteryGain mastery, PityOutcome pity)
        {
            Mastery = mastery;
            Pity = pity;
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
    /// a deterministic RNG, applies the pity, creates the AnimalInstance, updates discoveries, mastery,
    /// economy and save, then returns an immutable result for the presentation. Traits (T26) plug in here.
    /// <list type="bullet">
    /// <item>Cost(n) = round(BaseCost x CostGrowth^n), n = eggs of this type bought this cycle (doc 02).</item>
    /// <item>The very first hatch of a profile is free: the tutorial egg needs a single touch. When the egg
    /// names a starter species, that hatch gives it (rarity tier still rolled).</item>
    /// <item>The roll seed is derived from the profile and the next instance id: reproducible from the save,
    /// no extra persisted RNG state, never UnityEngine.Random.</item>
    /// <item>Pity (doc 02): while the pool has normal species to find, chance of a missing one =
    /// min(35%, 3% x misses), guaranteed after the egg's hard limit (20). Secrets never count. Variant
    /// pity: +1.5 points per miss (capped, boosted by mastery milestones) toward a missing variant of the
    /// rarity already rolled: pity changes which animal, never the rarity odds.</item>
    /// </list>
    /// </summary>
    public sealed class HatchService
    {
        private const uint RollSalt = 0x4A7C0001u;

        private readonly PlayerState _state;
        private readonly ContentRegistry _content;
        private readonly EconomyService _economy;
        private readonly EconomyConfig _config;
        private readonly IGameClock _clock;
        private readonly ISaveScheduler? _save;
        private readonly MasteryService? _mastery;
        private readonly uint _profileSeed;

        /// <summary>Variant dense indices per species and rarity tier, built once.</summary>
        private readonly Dictionary<ContentId, int[][]> _variantsBySpeciesTier;

        public HatchService(PlayerState state, ContentRegistry content, EconomyService economy, IGameClock clock, ISaveScheduler? save,
            MasteryService? mastery = null)
        {
            _mastery = mastery;
            _state = state;
            _content = content;
            _economy = economy;
            _config = economy.Config;
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

        /// <summary>Current new species bonus chance of an egg (0 when nothing is left to find).</summary>
        public double SpeciesPityChance(ContentId eggId)
        {
            if (!_content.Eggs.TryGet(eggId, out var egg) || MissingWeight(egg) <= 0.0)
                return 0.0;
            _state.Pity.NewSpeciesMisses.TryGetValue(eggId, out int misses);
            return misses >= egg.PityHardLimit ? 1.0 : SpeciesBonus(misses);
        }

        public double SpeciesBonus(int misses) => Math.Min(_config.PitySpeciesCap, _config.PitySpeciesPerMiss * misses);

        /// <summary>New variant bonus for a species given its misses and mastery milestones.</summary>
        public double VariantBonus(ContentId species, int misses)
        {
            int every = Math.Max(1, _config.MasteryMilestoneEvery);
            int milestones = _state.Mastery.GetLevel(species) / every;
            double perMiss = _config.PityVariantPerMiss * (1.0 + _config.PityVariantMilestoneBoost * milestones);
            return Math.Min(_config.PityVariantCap, perMiss * misses);
        }

        /// <summary>Every successful hatch (analytics, achievements). Raised once per animal, never per frame.</summary>
        public event Action<HatchResult>? Hatched;

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
                bool speciesPityActive = MissingWeight(egg) > 0.0;
                // The tutorial egg can name the first species of a profile; the tier is still rolled.
                var starter = free ? egg.StarterSpecies : ContentId.None;
                if (!TryPick(egg, rolled, speciesPityActive, starter, ref rng, out int variantIndex, out var pity))
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
                UpdatePity(eggId, speciesPityActive, newSpecies, variant.Species, newVariant);
                _economy.OnAnimalAdded(animal);
                var mastery = _mastery != null ? _mastery.OnAnimalObtained(variant.Species, newSpecies, newVariant) : default;
                bool priority = newSpecies || newVariant || variant.Rarity >= RarityTier.Rare || mastery.Milestone > 0;
                _save?.RequestSave(priority ? SaveUrgency.Priority : SaveUrgency.Normal);
                var result = new HatchResult(HatchStatus.Hatched, animal, eggId, cost, rolled, newSpecies, newVariant, free, mastery, pity);
                Hatched?.Invoke(result);
                return result;
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
        /// Species by pool weight (restricted to missing species when the pity fires), then a variant of
        /// the rolled tier. A species without that tier falls back to its nearest lower tier (then higher):
        /// the roll describes a tier, never a promise of a species. Variant pity stays inside that tier.
        /// </summary>
        private bool TryPick(EggDefinition egg, RarityTier tier, bool speciesPityActive, ContentId starter, ref DeterministicRandom rng,
            out int variantIndex, out PityOutcome pity)
        {
            variantIndex = -1;
            pity = PityOutcome.None;
            var pool = egg.Pool;
            double total = 0.0;
            for (int i = 0; i < pool.Length; i++)
                if (_variantsBySpeciesTier.ContainsKey(pool[i].Species))
                    total += pool[i].Weight;
            if (total <= 0.0)
                return false;

            ContentId species = default;
            bool onlyMissing = false;
            bool forced = starter.IsValid && _variantsBySpeciesTier.ContainsKey(starter);
            if (forced)
                species = starter;
            else if (speciesPityActive)
            {
                _state.Pity.NewSpeciesMisses.TryGetValue(egg.Id, out int misses);
                if (misses >= egg.PityHardLimit)
                {
                    onlyMissing = true;
                    pity = PityOutcome.SpeciesGuarantee;
                }
                else if (rng.NextDouble() < SpeciesBonus(misses))
                {
                    onlyMissing = true;
                    pity = PityOutcome.SpeciesBonus;
                }
            }

            if (!forced)
            {
                double weight = onlyMissing ? MissingWeight(egg) : total;
                double roll = rng.NextDouble() * weight;
                for (int i = 0; i < pool.Length; i++)
                {
                    if (!_variantsBySpeciesTier.ContainsKey(pool[i].Species) || (onlyMissing && !IsMissing(pool[i].Species)))
                        continue;
                    species = pool[i].Species;
                    roll -= pool[i].Weight;
                    if (roll < 0.0)
                        break;
                }
            }

            var tiers = _variantsBySpeciesTier[species];
            int start = (int)tier;
            int[]? candidates = null;
            for (int offset = 0; offset < tiers.Length * 2 && candidates == null; offset++)
            {
                // start, start-1, ..., 0, then start+1, start+2, ...
                int t = offset <= start ? start - offset : offset;
                if (t >= 0 && t < tiers.Length && tiers[t].Length > 0)
                    candidates = tiers[t];
            }
            if (candidates == null)
                return false;
            variantIndex = candidates[rng.Next(candidates.Length)];

            // Variant pity: the pick is already known and this tier still hides a variant.
            var variants = _content.Variants;
            if (_state.Collection.DiscoveredVariants.Contains(variants[variantIndex].Id))
            {
                int missing = 0;
                for (int i = 0; i < candidates.Length; i++)
                    if (IsMissingVariant(candidates[i]))
                        missing++;
                if (missing > 0)
                {
                    _state.Pity.VariantMisses.TryGetValue(species, out int variantMisses);
                    if (rng.NextDouble() < VariantBonus(species, variantMisses))
                    {
                        int pick = rng.Next(missing);
                        for (int i = 0; i < candidates.Length; i++)
                        {
                            if (!IsMissingVariant(candidates[i]))
                                continue;
                            if (pick-- == 0)
                            {
                                variantIndex = candidates[i];
                                break;
                            }
                        }
                        if (pity == PityOutcome.None)
                            pity = PityOutcome.VariantBonus;
                    }
                }
            }
            return true;
        }

        private void UpdatePity(ContentId egg, bool speciesPityActive, bool newSpecies, ContentId species, bool newVariant)
        {
            var counters = _state.Pity;
            if (speciesPityActive)
            {
                counters.NewSpeciesMisses.TryGetValue(egg, out int misses);
                counters.NewSpeciesMisses[egg] = newSpecies ? 0 : misses + 1;
            }
            else
            {
                counters.NewSpeciesMisses.Remove(egg); // nothing left to find in this pool
            }

            if (newVariant)
            {
                counters.VariantMisses.Remove(species);
            }
            else if (HasMissingVariant(species))
            {
                counters.VariantMisses.TryGetValue(species, out int misses);
                counters.VariantMisses[species] = misses + 1;
            }
        }

        /// <summary>Pool weight of normal (non-secret) species not discovered yet.</summary>
        private double MissingWeight(EggDefinition egg)
        {
            double weight = 0.0;
            var pool = egg.Pool;
            for (int i = 0; i < pool.Length; i++)
            {
                if (_variantsBySpeciesTier.ContainsKey(pool[i].Species) && IsMissing(pool[i].Species))
                    weight += pool[i].Weight;
            }
            return weight;
        }

        private bool IsMissing(ContentId species) =>
            !_state.Collection.DiscoveredSpecies.Contains(species)
            && (!_content.Species.TryGet(species, out var definition) || !definition.IsSecret);

        private bool IsMissingVariant(int variantIndex)
        {
            var variant = _content.Variants[variantIndex];
            return !variant.IsSecret && !_state.Collection.DiscoveredVariants.Contains(variant.Id);
        }

        private bool HasMissingVariant(ContentId species)
        {
            if (!_variantsBySpeciesTier.TryGetValue(species, out var tiers))
                return false;
            for (int t = 0; t < tiers.Length; t++)
                for (int i = 0; i < tiers[t].Length; i++)
                    if (IsMissingVariant(tiers[t][i]))
                        return true;
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
