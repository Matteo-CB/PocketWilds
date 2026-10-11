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

        /// <summary>A Discovery egg was asked for with none in stock.</summary>
        NoEggInStock = 4,

        /// <summary>The egg is not offered yet (Focus: biome mastery 3).</summary>
        Locked = 5,
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

    /// <summary>
    /// What the Light upgrades change in a hatch (doc 02 Améliorations, sections 3.3 and 3.4): Cozy Nests lowers the
    /// egg prices of a biome, Curious Eye adds undiscovered variant chance. Never the rarity odds.
    /// </summary>
    public interface IHatchModifiers
    {
        double EggPriceMultiplier(ContentId biome);
        double VariantChanceBonus(ContentId biome);

        /// <summary>
        /// The most an egg may cost now, in Light: a number of minutes of the current production (doc 02 Formules, « jamais
        /// plus de 15 minutes de production la première semaine, ni plus de 45 ensuite », a rule since T35C). Zero: no cap.
        /// </summary>
        BigValue EggPriceCap();
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

        /// <summary>Discovery eggs: the rule that gave the animal.</summary>
        public readonly DiscoveryRule Discovery;

        /// <summary>Position in a multi hatch, from 1 (0: a single hatch).</summary>
        public readonly byte MultiIndex;

        public HatchResult(HatchStatus status, AnimalInstance animal, ContentId egg, BigValue cost, RarityTier rolledTier,
            bool newSpecies, bool newVariant, bool wasFree)
            : this(status, animal, egg, cost, rolledTier, newSpecies, newVariant, wasFree, default, PityOutcome.None)
        {
        }

        public HatchResult(HatchStatus status, AnimalInstance animal, ContentId egg, BigValue cost, RarityTier rolledTier,
            bool newSpecies, bool newVariant, bool wasFree, MasteryGain mastery, PityOutcome pity,
            DiscoveryRule discovery = DiscoveryRule.None, byte multiIndex = 0)
        {
            Mastery = mastery;
            Pity = pity;
            Discovery = discovery;
            MultiIndex = multiIndex;
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
    /// <item>Cost(n) = round(BaseCost x CostGrowth^n), n = eggs of the biome bought this cycle: the Basic and the Focus of a
    /// biome share one counter, so the Focus always costs its multiple of the Basic of the moment (doc 02 Œufs, T35C).</item>
    /// <item>The very first hatch of a profile is free: the tutorial egg needs a single touch. When the egg
    /// names a starter species, that hatch gives it (rarity tier still rolled).</item>
    /// <item>The roll seed is derived from the profile and the next instance id: reproducible from the save,
    /// no extra persisted RNG state, never UnityEngine.Random.</item>
    /// <item>Pity (doc 02): while the pool has normal species to find, chance of a missing one =
    /// min(35%, 3% x misses), guaranteed after the egg's hard limit (20). Secrets never count. Variant
    /// pity: +1.5 points per miss (capped, boosted by mastery milestones) toward a missing variant of the
    /// rarity already rolled, Curious Eye on top, 40 % at most: pity changes which animal, never the rarity odds.</item>
    /// <item>T35B (doc 02 Œufs): Focus eggs target the pool species with the most undiscovered base variants; Discovery
    /// eggs are free, counted in a stock and follow three rules in priority order; a multi hatch is exactly that many
    /// single hatches in a row, paid egg by egg at the next prices.</item>
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
        private IHatchModifiers? _modifiers;
        private ContentId _cameraBiome;

        /// <summary>Normal (non-secret) variant dense indices per species and rarity tier, built once.</summary>
        private readonly Dictionary<ContentId, int[][]> _variantsBySpeciesTier;

        // Scratch buffers sized at construction: the effective pool of an egg (Focus targets), the Focus ranking, the
        // Discovery candidates. A hatch allocates nothing.
        private readonly EggDropEntry[] _pool;
        private readonly int[] _rank;
        private readonly bool[] _taken;
        private readonly int[] _candidates;
        private readonly ContentId[] _species;
        private readonly double[] _weights;

        // The counter each egg's price reads and advances: the Basic egg of its biome for Basic and Focus (T35C).
        private readonly Dictionary<ContentId, ContentId> _counterKey = new Dictionary<ContentId, ContentId>();

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
                // Doc 02 Œufs, section 2.1: secret variants have their own chance or condition, never the normal tables.
                if (variant.IsSecret)
                    continue;
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

            int maxPool = 1;
            for (int i = 0; i < content.Eggs.Count; i++)
                maxPool = Math.Max(maxPool, content.Eggs[i].Pool.Length);
            _pool = new EggDropEntry[maxPool];
            _rank = new int[maxPool];
            _taken = new bool[maxPool];
            _candidates = new int[Math.Max(1, variants.Count)];
            _species = new ContentId[Math.Max(1, content.Species.Count)];
            _weights = new double[Math.Max(1, content.Species.Count)];
            for (int i = 0; i < content.Eggs.Count; i++)
            {
                if (content.Eggs[i].Tier == EggTier.Discovery)
                {
                    DiscoveryEgg = content.Eggs[i].Id;
                    break;
                }
            }

            var basicOfBiome = new Dictionary<ContentId, ContentId>();
            for (int i = 0; i < content.Eggs.Count; i++)
                if (content.Eggs[i].Tier == EggTier.Basic && !basicOfBiome.ContainsKey(content.Eggs[i].Biome))
                    basicOfBiome.Add(content.Eggs[i].Biome, content.Eggs[i].Id);
            for (int i = 0; i < content.Eggs.Count; i++)
            {
                var egg = content.Eggs[i];
                _counterKey[egg.Id] = egg.Tier == EggTier.Focus && basicOfBiome.TryGetValue(egg.Biome, out var basic) ? basic : egg.Id;
            }
        }

        /// <summary>
        /// Remote Config econ.egg.growth (doc 02 Améliorations, section 15): one price growth for every biome egg; 0 keeps
        /// each egg's own CostGrowth (its content value).
        /// </summary>
        public double GrowthOverride { get; set; }

        /// <summary>Eggs of this egg's biome bought this cycle (its Basic and its Focus together).</summary>
        public int BoughtInCycle(ContentId eggId)
        {
            _state.World.EggPurchasesInCycle.TryGetValue(CounterKey(eggId), out int bought);
            return bought;
        }

        private ContentId CounterKey(ContentId eggId) => _counterKey.TryGetValue(eggId, out var key) ? key : eggId;

        private double Growth(EggDefinition egg) => GrowthOverride > 1.0 ? GrowthOverride : egg.CostGrowth;

        /// <summary>The first animal of a profile comes from the tutorial egg, without a cost.</summary>
        public bool IsNextHatchFree => _state.Collection.Count == 0;

        /// <summary>The Discovery egg of the content (None when the content has none).</summary>
        public ContentId DiscoveryEgg { get; }

        /// <summary>Discovery eggs won and not hatched yet.</summary>
        public int DiscoveryEggs => _state.World.DiscoveryEggs;

        /// <summary>Light upgrades (Cozy Nests, Curious Eye). Null: none.</summary>
        public void SetModifiers(IHatchModifiers? modifiers) => _modifiers = modifiers;

        /// <summary>The biome the camera is in: its undiscovered species weigh double in a Discovery egg.</summary>
        public void SetCameraBiome(ContentId biome) => _cameraBiome = biome;

        /// <summary>Every successful hatch (analytics, achievements). Raised once per animal, never per frame.</summary>
        public event Action<HatchResult>? Hatched;

        /// <summary>A multi hatch, once all its eggs are out.</summary>
        public event Action<MultiHatchSummary>? MultiHatched;

        /// <summary>Discovery eggs added to the stock, with their source.</summary>
        public event Action<int, DiscoverySource>? DiscoveryEggsGranted;

        // ------------------------------------------------------------------------------------------- offer and prices

        /// <summary>
        /// Whether the egg can be chosen now (doc 02 Œufs, sections 1 and 3): a biome egg once its biome mastery reaches the
        /// egg's level (Focus: 3), a Discovery egg while the stock has one.
        /// </summary>
        public bool IsOffered(ContentId eggId) => _content.Eggs.TryGet(eggId, out var egg) && IsOffered(egg);

        private bool IsOffered(EggDefinition egg)
        {
            if (egg.Tier == EggTier.Discovery)
                return _state.World.DiscoveryEggs > 0;
            return egg.UnlockBiomeMastery <= 0 || BiomeMastery(egg.Biome) >= egg.UnlockBiomeMastery;
        }

        public BigValue GetCost(ContentId eggId)
        {
            if (!_content.Eggs.TryGet(eggId, out var egg) || egg.Tier == EggTier.Discovery)
                return BigValue.Zero;
            return Price(egg, BoughtInCycle(eggId));
        }

        /// <summary>Price of the next <paramref name="count"/> eggs of this type (doc 02 Œufs, section 5: n, n + 1, ...).</summary>
        public BigValue GetCost(ContentId eggId, int count)
        {
            if (count <= 1)
                return GetCost(eggId);
            if (!_content.Eggs.TryGet(eggId, out var egg) || egg.Tier == EggTier.Discovery)
                return BigValue.Zero;
            int bought = BoughtInCycle(eggId);
            var total = BigValue.Zero;
            for (int i = 0; i < count; i++)
                total += Price(egg, bought + i);
            return total;
        }

        /// <summary>
        /// Price of the egg bought after <paramref name="bought"/> others this cycle, Cozy Nests applied, never above the cap
        /// in minutes of production.
        /// </summary>
        private BigValue Price(EggDefinition egg, int bought)
        {
            var cost = Cost(egg, bought, Growth(egg));
            if (_modifiers == null)
                return cost;
            double multiplier = _modifiers.EggPriceMultiplier(egg.Biome);
            if (multiplier < 1.0)
                cost = BigValue.Multiply(cost, multiplier);
            var cap = _modifiers.EggPriceCap();
            if (!cap.IsZero && cost > cap)
                cost = cap;
            // Whole Light while that matters, never below 1.
            return cost.Exponent10 < 15
                ? BigValue.FromDouble(Math.Max(1.0, Math.Round(cost.ToDoubleClamped(), MidpointRounding.AwayFromZero)))
                : cost;
        }

        public static BigValue Cost(EggDefinition egg, int bought) => Cost(egg, bought, egg.CostGrowth);

        public static BigValue Cost(EggDefinition egg, int bought, double growth)
        {
            double log10 = Math.Log10(egg.BaseCost) + bought * Math.Log10(growth);
            if (log10 < 15.0)
                return BigValue.FromDouble(Math.Round(egg.BaseCost * Math.Pow(growth, bought), MidpointRounding.AwayFromZero));
            long exponent = (long)Math.Floor(log10);
            return BigValue.FromMantissaExponent(Math.Pow(10.0, log10 - exponent), exponent);
        }

        public bool CanHatch(ContentId eggId) => CanHatch(eggId, 1);

        /// <summary>A multi hatch needs the whole price (or as many Discovery eggs) at once; the free tutorial egg is single.</summary>
        public bool CanHatch(ContentId eggId, int count)
        {
            if (count < 1 || !_content.Eggs.TryGet(eggId, out var egg) || !IsOffered(egg))
                return false;
            if (egg.Tier == EggTier.Discovery)
                return _state.World.DiscoveryEggs >= count;
            if (IsNextHatchFree)
                return count == 1;
            return _economy.CanSpend(CurrencyId.Light, GetCost(eggId, count));
        }

        // ------------------------------------------------------------------------------------------- Discovery stock

        /// <summary>Adds Discovery eggs to the stock (doc 02 Œufs, section 2.3: rewards only, never sold).</summary>
        public void GrantDiscoveryEggs(int count, DiscoverySource source)
        {
            if (count <= 0)
                return;
            _state.World.DiscoveryEggs += count;
            _save?.RequestSave(SaveUrgency.Priority);
            DiscoveryEggsGranted?.Invoke(count, source);
        }

        // ------------------------------------------------------------------------------------------- pity and odds

        /// <summary>Current new species bonus chance of an egg (0 when nothing is left to find).</summary>
        public double SpeciesPityChance(ContentId eggId)
        {
            if (!_content.Eggs.TryGet(eggId, out var egg) || egg.Tier == EggTier.Discovery)
                return 0.0;
            int count = EffectivePool(egg);
            if (MissingWeight(count) <= 0.0)
                return 0.0;
            _state.Pity.NewSpeciesMisses.TryGetValue(eggId, out int misses);
            return misses >= egg.PityHardLimit ? 1.0 : SpeciesBonus(misses);
        }

        /// <summary>T35C: Keen Nose (Essence tree) adds this much to the new species protection per miss (same cap).</summary>
        public double SpeciesPityBonusPerMiss { get; set; }

        /// <summary>T35C: Color Eye (Essence tree) strengthens the new variant protection per miss; 1 without it.</summary>
        public double VariantPityMultiplier { get; set; } = 1.0;

        public double SpeciesBonus(int misses) =>
            Math.Min(_config.PitySpeciesCap, (_config.PitySpeciesPerMiss + SpeciesPityBonusPerMiss) * misses);

        /// <summary>New variant bonus for a species given its misses and mastery milestones.</summary>
        public double VariantBonus(ContentId species, int misses)
        {
            int every = Math.Max(1, _config.MasteryMilestoneEvery);
            int milestones = _state.Mastery.GetLevel(species) / every;
            double perMiss = _config.PityVariantPerMiss * (1.0 + _config.PityVariantMilestoneBoost * milestones) * VariantPityMultiplier;
            return Math.Min(_config.PityVariantCap, perMiss * misses);
        }

        /// <summary>
        /// Chance that a known variant of this species becomes an undiscovered one of the same rarity: the protection plus
        /// Curious Eye, 40 % at most (doc 02 Améliorations, section 3.4).
        /// </summary>
        public double NewVariantChance(ContentId species, ContentId biome)
        {
            _state.Pity.VariantMisses.TryGetValue(species, out int misses);
            double curiosity = _modifiers != null ? _modifiers.VariantChanceBonus(biome) : 0.0;
            return Math.Min(_config.PityVariantTotalCap, VariantBonus(species, misses) + curiosity);
        }

        /// <summary>The species a Focus egg targets right now (doc 02 Œufs, section 2.2), in pool order. 0 for other eggs.</summary>
        public int GetFocusTargets(ContentId eggId, ContentId[] into)
        {
            if (!_content.Eggs.TryGet(eggId, out var egg) || egg.FocusTargetCount <= 0)
                return 0;
            int count = Math.Min(EffectivePool(egg), into.Length);
            for (int i = 0; i < count; i++)
                into[i] = _pool[i].Species;
            return count;
        }

        /// <summary>The odds window's numbers, the very ones the roll uses (doc 02 Œufs, section 6).</summary>
        public void GetOdds(ContentId eggId, EggOdds odds)
        {
            odds.Clear();
            if (!_content.Eggs.TryGet(eggId, out var egg))
                return;
            odds.Egg = eggId;
            odds.Tier = egg.Tier;
            odds.PityHardLimit = egg.PityHardLimit;
            var weights = egg.RarityWeights;
            double total = 0.0;
            for (int i = 0; i < weights.Length; i++)
                total += weights[i];
            for (int i = 0; i < odds.RarityPercent.Length && i < weights.Length; i++)
                odds.RarityPercent[i] = total > 0.0 ? weights[i] * 100.0 / total : 0.0;
            if (egg.Tier == EggTier.Discovery)
            {
                odds.Free = true;
                odds.Stock = _state.World.DiscoveryEggs;
                return;
            }

            int count = EffectivePool(egg);
            if (egg.FocusTargetCount > 0)
            {
                odds.TargetCount = Math.Min(count, EggOdds.MaxTargets);
                for (int i = 0; i < odds.TargetCount; i++)
                    odds.Targets[i] = _pool[i].Species;
            }
            if (MissingWeight(count) > 0.0)
            {
                _state.Pity.NewSpeciesMisses.TryGetValue(eggId, out int misses);
                odds.NewSpeciesBonus = misses >= egg.PityHardLimit ? 1.0 : SpeciesBonus(misses);
                odds.EggsToGuarantee = Math.Max(1, egg.PityHardLimit - misses);
            }
            for (int i = 0; i < count; i++)
                if (HasMissingVariant(_pool[i].Species))
                    odds.NewVariantBonus = Math.Max(odds.NewVariantBonus, NewVariantChance(_pool[i].Species, egg.Biome));
        }

        // ------------------------------------------------------------------------------------------- hatching

        public HatchResult Hatch(ContentId eggId) => HatchOne(eggId, 0);

        /// <summary>
        /// Doc 02 Œufs, section 5: <paramref name="count"/> eggs in a row, each one exactly a single hatch (its price, its
        /// guarantees, its seed), once the whole price is there. Writes one result per egg and returns how many hatched.
        /// </summary>
        public int Hatch(ContentId eggId, int count, HatchResult[] results)
        {
            count = Math.Min(count, results.Length);
            if (count <= 0)
                return 0;
            if (count == 1)
            {
                results[0] = HatchOne(eggId, 0);
                return results[0].Succeeded ? 1 : 0;
            }
            if (!_content.Eggs.TryGet(eggId, out var egg))
            {
                results[0] = HatchResult.Failed(HatchStatus.UnknownEgg, eggId, BigValue.Zero);
                return 0;
            }
            if (!CanHatch(eggId, count))
            {
                var status = !IsOffered(egg) ? (egg.Tier == EggTier.Discovery ? HatchStatus.NoEggInStock : HatchStatus.Locked)
                    : egg.Tier == EggTier.Discovery ? HatchStatus.NoEggInStock : HatchStatus.NotEnoughLight;
                results[0] = HatchResult.Failed(status, eggId, GetCost(eggId, count));
                return 0;
            }

            var cost = BigValue.Zero;
            int done = 0, rare = 0, fresh = 0;
            for (int i = 0; i < count; i++)
            {
                var result = HatchOne(eggId, (byte)Math.Min(255, i + 1));
                results[i] = result;
                if (!result.Succeeded)
                    break;
                done++;
                cost += result.Cost;
                if (result.Animal.Rarity >= RarityTier.Rare)
                    rare++;
                if (result.NewSpecies || result.NewVariant)
                    fresh++;
            }
            if (done > 0)
                MultiHatched?.Invoke(new MultiHatchSummary(eggId, done, rare, fresh, cost));
            return done;
        }

        private HatchResult HatchOne(ContentId eggId, byte multiIndex)
        {
            using (GameProfilerMarkers.HatchResolve.Auto())
            {
                if (!_content.Eggs.TryGet(eggId, out var egg))
                    return HatchResult.Failed(HatchStatus.UnknownEgg, eggId, BigValue.Zero);
                if (egg.Tier == EggTier.Discovery)
                    return HatchDiscovery(egg, multiIndex);
                if (!IsOffered(egg))
                    return HatchResult.Failed(HatchStatus.Locked, eggId, GetCost(eggId));

                bool free = IsNextHatchFree;
                var cost = free ? BigValue.Zero : GetCost(eggId);

                var rng = new DeterministicRandom(SeedHash.Mix(_profileSeed ^ _state.Collection.NextInstanceId, RollSalt));
                var rolled = RollTier(egg.RarityWeights, ref rng);
                int poolCount = EffectivePool(egg);
                bool speciesPityActive = MissingWeight(poolCount) > 0.0;
                // The tutorial egg can name the first species of a profile; the tier is still rolled.
                var starter = free ? egg.StarterSpecies : ContentId.None;
                if (!TryPick(egg, poolCount, rolled, speciesPityActive, starter, ref rng, out int variantIndex, out var pity))
                    return HatchResult.Failed(HatchStatus.NoCandidate, eggId, cost);

                // Pay only once the result is known to exist: a failed roll never costs Light.
                if (!free)
                {
                    if (!_economy.Spend(CurrencyId.Light, cost, EconomyReason.EggPurchase))
                        return HatchResult.Failed(HatchStatus.NotEnoughLight, eggId, cost);
                    var key = CounterKey(eggId);
                    _state.World.EggPurchasesInCycle.TryGetValue(key, out int bought);
                    _state.World.EggPurchasesInCycle[key] = bought + 1;
                }
                return Complete(egg, variantIndex, rolled, cost, free, speciesPityActive, pity, DiscoveryRule.None, multiIndex, ref rng);
            }
        }

        private HatchResult HatchDiscovery(EggDefinition egg, byte multiIndex)
        {
            if (_state.World.DiscoveryEggs <= 0)
                return HatchResult.Failed(HatchStatus.NoEggInStock, egg.Id, BigValue.Zero);
            var rng = new DeterministicRandom(SeedHash.Mix(_profileSeed ^ _state.Collection.NextInstanceId, RollSalt));
            if (!TryPickDiscovery(egg, ref rng, out int variantIndex, out var rolled, out var rule))
                return HatchResult.Failed(HatchStatus.NoCandidate, egg.Id, BigValue.Zero);
            _state.World.DiscoveryEggs--;
            return Complete(egg, variantIndex, rolled, BigValue.Zero, true, false, PityOutcome.None, rule, multiIndex, ref rng);
        }

        /// <summary>Creates the animal, updates discoveries, protections, economy, mastery and save, raises Hatched.</summary>
        private HatchResult Complete(EggDefinition egg, int variantIndex, RarityTier rolled, BigValue cost, bool free, bool speciesPityActive,
            PityOutcome pity, DiscoveryRule rule, byte multiIndex, ref DeterministicRandom rng)
        {
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
            UpdatePity(egg, speciesPityActive, newSpecies, variant.Species, newVariant);
            _economy.OnAnimalAdded(animal);
            var mastery = _mastery != null ? _mastery.OnAnimalObtained(variant.Species, newSpecies, newVariant) : default;
            bool priority = newSpecies || newVariant || variant.Rarity >= RarityTier.Rare || mastery.Milestone > 0;
            _save?.RequestSave(priority ? SaveUrgency.Priority : SaveUrgency.Normal);
            var result = new HatchResult(HatchStatus.Hatched, animal, egg.Id, cost, rolled, newSpecies, newVariant, free, mastery, pity, rule,
                multiIndex);
            Hatched?.Invoke(result);
            return result;
        }

        /// <summary>The rarity roll of every egg: one draw on the egg's table (public for the million-draw table test).</summary>
        public static RarityTier RollTier(double[] weights, ref DeterministicRandom rng)
        {
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
        /// The species an egg can give right now, in <see cref="_pool"/>: its pool, or for a Focus egg the
        /// <see cref="EggDefinition.FocusTargetCount"/> species with the most undiscovered base variants (ties: pool order),
        /// the whole pool once the biome hides none. Species without normal variants are left out. Returns the count.
        /// </summary>
        private int EffectivePool(EggDefinition egg)
        {
            var pool = egg.Pool;
            int usable = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                bool ok = _variantsBySpeciesTier.ContainsKey(pool[i].Species);
                _rank[i] = ok ? MissingBaseVariants(pool[i].Species) : -1;
                _taken[i] = false;
                if (ok)
                    usable++;
            }

            int targets = egg.FocusTargetCount;
            bool focus = targets > 0 && targets < usable;
            if (focus)
            {
                int missing = 0;
                for (int i = 0; i < pool.Length; i++)
                    if (_rank[i] > 0)
                        missing += _rank[i];
                focus = missing > 0;
            }
            if (focus)
            {
                for (int k = 0; k < targets; k++)
                {
                    int best = -1;
                    for (int i = 0; i < pool.Length; i++)
                        if (_rank[i] >= 0 && !_taken[i] && (best < 0 || _rank[i] > _rank[best]))
                            best = i;
                    if (best < 0)
                        break;
                    _taken[best] = true;
                }
            }

            int count = 0;
            for (int i = 0; i < pool.Length; i++)
                if (_rank[i] >= 0 && (!focus || _taken[i]))
                    _pool[count++] = pool[i];
            return count;
        }

        /// <summary>
        /// Species by pool weight (restricted to missing species when the pity fires), then a variant of
        /// the rolled tier. A species without that tier falls back to its nearest lower tier (then higher):
        /// the roll describes a tier, never a promise of a species. Variant pity stays inside that tier.
        /// </summary>
        private bool TryPick(EggDefinition egg, int poolCount, RarityTier tier, bool speciesPityActive, ContentId starter,
            ref DeterministicRandom rng, out int variantIndex, out PityOutcome pity)
        {
            variantIndex = -1;
            pity = PityOutcome.None;
            double total = 0.0;
            for (int i = 0; i < poolCount; i++)
                total += _pool[i].Weight;
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
                double weight = onlyMissing ? MissingWeight(poolCount) : total;
                double roll = rng.NextDouble() * weight;
                for (int i = 0; i < poolCount; i++)
                {
                    if (onlyMissing && !IsMissing(_pool[i].Species))
                        continue;
                    species = _pool[i].Species;
                    roll -= _pool[i].Weight;
                    if (roll < 0.0)
                        break;
                }
            }

            var candidates = InTier(_variantsBySpeciesTier[species], (int)tier);
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
                if (missing > 0 && rng.NextDouble() < NewVariantChance(species, egg.Biome))
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
            return true;
        }

        /// <summary>The variants of the tier, or of the nearest lower tier, then higher (start, start-1, ..., 0, start+1, ...).</summary>
        private static int[]? InTier(int[][] tiers, int start)
        {
            for (int offset = 0; offset < tiers.Length * 2; offset++)
            {
                int t = offset <= start ? start - offset : offset;
                if (t >= 0 && t < tiers.Length && tiers[t].Length > 0)
                    return tiers[t];
            }
            return null;
        }

        /// <summary>
        /// Doc 02 Œufs, section 2.3, in priority order: a normal species not discovered yet from an open biome (double weight
        /// in the camera's biome, tier rolled on that biome's Basic table); else a base variant not discovered yet of a
        /// discovered species; else a variant of rarity Rare or better on the egg's own table. Never a secret variant.
        /// </summary>
        private bool TryPickDiscovery(EggDefinition egg, ref DeterministicRandom rng, out int variantIndex, out RarityTier rolled,
            out DiscoveryRule rule)
        {
            variantIndex = -1;
            rolled = RarityTier.Common;
            rule = DiscoveryRule.None;
            var allSpecies = _content.Species;
            var collection = _state.Collection;

            // 1. A species to discover.
            int count = 0;
            double total = 0.0;
            for (int i = 0; i < allSpecies.Count; i++)
            {
                var species = allSpecies[i];
                if (species.IsSecret || species.IsCompanion || collection.DiscoveredSpecies.Contains(species.Id) || !_variantsBySpeciesTier.ContainsKey(species.Id)
                    || !IsBiomeOpen(species.Biome))
                    continue;
                double weight = species.Biome == _cameraBiome ? _config.DiscoveryCameraBiomeWeight : 1.0;
                _species[count] = species.Id;
                _weights[count] = weight;
                total += weight;
                count++;
            }
            if (count > 0)
            {
                double roll = rng.NextDouble() * total;
                int chosen = count - 1;
                for (int i = 0; i < count; i++)
                {
                    roll -= _weights[i];
                    if (roll < 0.0)
                    {
                        chosen = i;
                        break;
                    }
                }
                var species = _species[chosen];
                rolled = RollTier(BasicWeights(allSpecies.Get(species).Biome, egg), ref rng);
                var candidates = InTier(_variantsBySpeciesTier[species], (int)rolled);
                if (candidates == null)
                    return false;
                variantIndex = candidates[rng.Next(candidates.Length)];
                rule = DiscoveryRule.NewSpecies;
                return true;
            }

            // 2. A color to discover on a species already found.
            var variants = _content.Variants;
            count = 0;
            for (int i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                if (!variant.IsSecret && !collection.DiscoveredVariants.Contains(variant.Id) && collection.DiscoveredSpecies.Contains(variant.Species))
                    _candidates[count++] = i;
            }
            if (count > 0)
            {
                variantIndex = _candidates[rng.Next(count)];
                rolled = variants[variantIndex].Rarity;
                rule = DiscoveryRule.NewVariant;
                return true;
            }

            // 3. Everything found: Rare or better, on the egg's table (Rare 70, Epic 22, Legendary 7, Fabulous 1).
            rolled = RollTier(egg.RarityWeights, ref rng);
            int start = Math.Max((int)rolled, (int)RarityTier.Rare);
            // The rolled tier, then lower down to Rare, then higher: a species missing that tier never blocks the egg.
            for (int t = start; t >= (int)RarityTier.Rare; t--)
                if (TryRareTier(t, ref rng, out variantIndex))
                {
                    rolled = (RarityTier)t;
                    rule = DiscoveryRule.RareOrBetter;
                    return true;
                }
            for (int t = start + 1; t < EggDefinition.RarityTierCount; t++)
                if (TryRareTier(t, ref rng, out variantIndex))
                {
                    rolled = (RarityTier)t;
                    rule = DiscoveryRule.RareOrBetter;
                    return true;
                }
            return false;
        }

        /// <summary>A discovered species with a normal variant of tier <paramref name="tier"/>, then one of those variants.</summary>
        private bool TryRareTier(int tier, ref DeterministicRandom rng, out int variantIndex)
        {
            variantIndex = -1;
            var allSpecies = _content.Species;
            int count = 0;
            for (int i = 0; i < allSpecies.Count; i++)
            {
                var species = allSpecies[i];
                if (!species.IsSecret && _state.Collection.DiscoveredSpecies.Contains(species.Id)
                    && _variantsBySpeciesTier.TryGetValue(species.Id, out var tiers) && tiers[tier].Length > 0)
                    _species[count++] = species.Id;
            }
            if (count == 0)
                return false;
            var list = _variantsBySpeciesTier[_species[rng.Next(count)]][tier];
            variantIndex = list[rng.Next(list.Length)];
            return true;
        }

        /// <summary>The Basic egg table of a biome (the Discovery egg's own when the biome has no Basic egg).</summary>
        private double[] BasicWeights(ContentId biome, EggDefinition fallback)
        {
            var eggs = _content.Eggs;
            for (int i = 0; i < eggs.Count; i++)
                if (eggs[i].Tier == EggTier.Basic && eggs[i].Biome == biome)
                    return eggs[i].RarityWeights;
            return fallback.RarityWeights;
        }

        private void UpdatePity(EggDefinition egg, bool speciesPityActive, bool newSpecies, ContentId species, bool newVariant)
        {
            var counters = _state.Pity;
            if (egg.Tier != EggTier.Discovery)
            {
                if (speciesPityActive)
                {
                    counters.NewSpeciesMisses.TryGetValue(egg.Id, out int misses);
                    counters.NewSpeciesMisses[egg.Id] = newSpecies ? 0 : misses + 1;
                }
                else
                {
                    counters.NewSpeciesMisses.Remove(egg.Id); // nothing left to find in this pool
                }
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

        /// <summary>Pool weight, over the effective pool, of normal species not discovered yet.</summary>
        private double MissingWeight(int poolCount)
        {
            double weight = 0.0;
            for (int i = 0; i < poolCount; i++)
                if (IsMissing(_pool[i].Species))
                    weight += _pool[i].Weight;
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

        private bool HasMissingVariant(ContentId species) => MissingBaseVariants(species) > 0;

        /// <summary>Normal variants of a species not discovered yet.</summary>
        private int MissingBaseVariants(ContentId species)
        {
            if (!_variantsBySpeciesTier.TryGetValue(species, out var tiers))
                return 0;
            int missing = 0;
            for (int t = 0; t < tiers.Length; t++)
                for (int i = 0; i < tiers[t].Length; i++)
                    if (IsMissingVariant(tiers[t][i]))
                        missing++;
            return missing;
        }

        private int BiomeMastery(ContentId biome)
        {
            var biomes = _state.World.Biomes;
            for (int i = 0; i < biomes.Count; i++)
                if (biomes[i].Biome == biome)
                    return biomes[i].MasteryLevel;
            return 0;
        }

        /// <summary>An open biome; a profile with no biome recorded yet (tests, before the progression starts) has them all.</summary>
        private bool IsBiomeOpen(ContentId biome)
        {
            var biomes = _state.World.Biomes;
            if (biomes.Count == 0)
                return true;
            for (int i = 0; i < biomes.Count; i++)
                if (biomes[i].Biome == biome)
                    return biomes[i].Unlocked;
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
