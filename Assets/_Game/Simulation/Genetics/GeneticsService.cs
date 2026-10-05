#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    public enum BreedCheck : byte
    {
        Ok = 0,
        SameAnimal = 1,
        DifferentSpecies = 2,
        UnknownSpecies = 3,
    }

    /// <summary>One possible child palette and its exact chance.</summary>
    public readonly struct VariantChance
    {
        public readonly ContentId Variant;
        public readonly double Chance;
        public readonly bool IsMutation;

        public VariantChance(ContentId variant, double chance, bool isMutation)
        {
            Variant = variant;
            Chance = chance;
            IsMutation = isMutation;
        }
    }

    /// <summary>The simple hints doc 03 shows before breeding: likely colors, mutation chance, rare trait chance.</summary>
    public readonly struct GeneticsPreview
    {
        public readonly double MutationChance;
        public readonly double PatternChance;
        public readonly double RareTraitChance;

        public GeneticsPreview(double mutationChance, double patternChance, double rareTraitChance)
        {
            MutationChance = mutationChance;
            PatternChance = patternChance;
            RareTraitChance = rareTraitChance;
        }
    }

    /// <summary>Everything a child inherits. Becomes an <see cref="AnimalInstance"/> when its egg hatches.</summary>
    public readonly struct ChildGenome
    {
        public readonly ContentId Species;
        public readonly ContentId Variant;
        public readonly uint Seed;
        public readonly uint TraitBits;
        public readonly RarityTier Rarity;
        public readonly bool Mutated;

        public ChildGenome(ContentId species, ContentId variant, uint seed, uint traitBits, RarityTier rarity, bool mutated)
        {
            Species = species;
            Variant = variant;
            Seed = seed;
            TraitBits = traitBits;
            Rarity = rarity;
            Mutated = mutated;
        }
    }

    /// <summary>
    /// Genetics prototype (T26, Codebase Blueprint GeneticsService). Two compatible parents and a context seed give
    /// one child genome through the weighted tables of <see cref="GeneticsConfig"/>:
    /// <list type="bullet">
    /// <item>palette: a parent's variant most of the time, otherwise a mutation toward a neighbouring rarity
    /// (never a parent's variant, never a secret);</item>
    /// <item>pattern: a carried pattern from the species' own patterns, shown tone on tone on a plain palette;</item>
    /// <item>traits: each one from both, one or no parent, with Tiny/Giant and Albino/Melanistic exclusive;</item>
    /// <item>size: the parents' average with a small jitter.</item>
    /// </list>
    /// Pure and deterministic: no Unity object, no clock, no global random. Breed(a, b) equals Breed(b, a).
    /// <see cref="Preview"/> computes the exact odds that <see cref="TryBreed"/> samples.
    /// </summary>
    public sealed class GeneticsService
    {
        private const uint BreedSalt = 0x6E7E7001u;

        private readonly ContentRegistry _content;
        private readonly GeneticsConfig _config;
        private readonly Dictionary<ContentId, int[]> _variantsBySpecies = new Dictionary<ContentId, int[]>();
        private readonly Dictionary<ContentId, PatternChannel[]> _patternsBySpecies = new Dictionary<ContentId, PatternChannel[]>();
        private readonly double[] _weights;
        private readonly int[] _tierCounts = new int[EggDefinition.RarityTierCount];
        private readonly double[] _traitRolls = new double[AnimalGenome.TraitCount];

        public GeneticsService(ContentRegistry content, GeneticsConfig? config = null)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _config = config ?? GeneticsConfig.Default;
            var lists = new Dictionary<ContentId, List<int>>();
            var variants = content.Variants;
            for (int i = 0; i < variants.Count; i++)
            {
                if (!lists.TryGetValue(variants[i].Species, out var list))
                    lists.Add(variants[i].Species, list = new List<int>());
                list.Add(i);
            }
            int widest = 1;
            foreach (var pair in lists)
            {
                _variantsBySpecies.Add(pair.Key, pair.Value.ToArray());
                widest = Math.Max(widest, pair.Value.Count);
                // Allowed patterns are the ones the species' own variants use (doc 03: authored associations only).
                var channels = new List<PatternChannel>();
                foreach (int index in pair.Value)
                {
                    var v = variants[index];
                    if (!v.IsSecret && v.PatternChannel != PatternChannel.None && !channels.Contains(v.PatternChannel))
                        channels.Add(v.PatternChannel);
                }
                _patternsBySpecies.Add(pair.Key, channels.ToArray());
            }
            _weights = new double[widest];
        }

        public GeneticsConfig Config => _config;

        public BreedCheck Check(in AnimalInstance a, in AnimalInstance b)
        {
            if (a.InstanceId != 0 && a.InstanceId == b.InstanceId)
                return BreedCheck.SameAnimal;
            if (a.Species != b.Species)
                return BreedCheck.DifferentSpecies;
            if (!_content.Species.TryGet(a.Species, out _) || !_variantsBySpecies.ContainsKey(a.Species))
                return BreedCheck.UnknownSpecies;
            return BreedCheck.Ok;
        }

        /// <summary>Exact odds of the child's palette (filled into odds, likeliest first) and the headline chances.</summary>
        public BreedCheck Preview(in AnimalInstance a, in AnimalInstance b, List<VariantChance>? odds, out GeneticsPreview preview)
        {
            preview = default;
            var check = Check(a, b);
            if (check != BreedCheck.Ok)
                return check;

            var list = _variantsBySpecies[a.Species];
            FillVariantWeights(list, a.Variant, b.Variant, out int ia, out int ib, out double mutation);
            if (odds != null)
            {
                odds.Clear();
                for (int i = 0; i < list.Length; i++)
                    if (_weights[i] > 0.0)
                        odds.Add(new VariantChance(_content.Variants[list[i]].Id, _weights[i], i != ia && i != ib));
                odds.Sort((x, y) => y.Chance.CompareTo(x.Chance));
            }

            double none = 1.0;
            for (int t = 0; t < AnimalGenome.TraitCount; t++)
                none *= 1.0 - TraitChance(t, a.TraitBits, b.TraitBits);
            preview = new GeneticsPreview(mutation, PatternChance(a, b), 1.0 - none);
            return BreedCheck.Ok;
        }

        /// <summary>One child. The same parents and context seed always give the same child, in either order.</summary>
        public BreedCheck TryBreed(in AnimalInstance a, in AnimalInstance b, uint contextSeed, out ChildGenome child)
        {
            child = default;
            var check = Check(a, b);
            if (check != BreedCheck.Ok)
                return check;

            using (GameProfilerMarkers.GeneticsBreed.Auto())
            {
                uint lo = Math.Min(a.Seed, b.Seed), hi = Math.Max(a.Seed, b.Seed);
                var rng = new DeterministicRandom(SeedHash.Mix(lo ^ SeedHash.Mix(hi, BreedSalt), contextSeed));
                // Fixed draw order: every rule always consumes its rolls, so the sequence never shifts.
                uint childSeed = rng.NextUInt();
                double variantRoll = rng.NextDouble();
                for (int t = 0; t < AnimalGenome.TraitCount; t++)
                    _traitRolls[t] = rng.NextDouble();
                double sizeRoll = rng.NextDouble();
                double patternRoll = rng.NextDouble();
                double carrierRoll = rng.NextDouble();
                double strengthRoll = rng.NextDouble();
                double channelRoll = rng.NextDouble();

                // Palette.
                var list = _variantsBySpecies[a.Species];
                FillVariantWeights(list, a.Variant, b.Variant, out int ia, out int ib, out _);
                int pick = list.Length - 1;
                double cumulative = 0.0;
                for (int i = 0; i < list.Length; i++)
                {
                    cumulative += _weights[i];
                    if (variantRoll < cumulative && _weights[i] > 0.0)
                    {
                        pick = i;
                        break;
                    }
                }
                while (pick > 0 && _weights[pick] <= 0.0)
                    pick--;
                var variant = _content.Variants[list[pick]];

                // Traits, with the two exclusive pairs.
                var traits = AnimalTrait.None;
                for (int t = 0; t < AnimalGenome.TraitCount; t++)
                {
                    if (t == 1 && (traits & AnimalTrait.Tiny) != 0)
                        continue;
                    if (t == 5 && (traits & AnimalTrait.Albino) != 0)
                        continue;
                    if (_traitRolls[t] < TraitChance(t, a.TraitBits, b.TraitBits))
                        traits |= AnimalGenome.TraitAt(t);
                }

                // Size: the parents' average, then -1, 0 or +1.
                int size = (AnimalGenome.Size(a.TraitBits) + AnimalGenome.Size(b.TraitBits)) / 2;
                size += sizeRoll < 0.25 ? -1 : sizeRoll >= 0.75 ? 1 : 0;

                uint bits = AnimalGenome.WithSize(AnimalGenome.WithTraits(0u, traits), size);

                // Carried pattern.
                if (patternRoll < PatternChance(a, b))
                {
                    var carriedA = Carried(a, out int strengthA);
                    var carriedB = Carried(b, out int strengthB);
                    PatternChannel channel;
                    int strength;
                    if (carriedA != PatternChannel.None && carriedB != PatternChannel.None)
                    {
                        channel = carrierRoll < 0.5 ? carriedA : carriedB;
                        strength = (strengthA + strengthB) / 2;
                    }
                    else if (carriedA != PatternChannel.None || carriedB != PatternChannel.None)
                    {
                        channel = carriedA != PatternChannel.None ? carriedA : carriedB;
                        strength = carriedA != PatternChannel.None ? strengthA : strengthB;
                    }
                    else
                    {
                        var allowed = _patternsBySpecies[a.Species];
                        channel = allowed.Length > 0 ? allowed[(int)(channelRoll * allowed.Length) % allowed.Length] : PatternChannel.None;
                        strength = 10;
                    }
                    strength += strengthRoll < 0.3 ? -2 : strengthRoll >= 0.7 ? 2 : 0;
                    bits = AnimalGenome.WithPattern(bits, channel, Math.Max(6, Math.Min(15, strength)));
                }

                child = new ChildGenome(a.Species, variant.Id, childSeed, bits, variant.Rarity, pick != ia && pick != ib);
                return BreedCheck.Ok;
            }
        }

        /// <summary>The child once its egg hatches. The caller adds it to the collection.</summary>
        public static AnimalInstance CreateChild(in ChildGenome genome, uint createdUtcSeconds) => new AnimalInstance
        {
            Species = genome.Species,
            Variant = genome.Variant,
            Seed = genome.Seed,
            TraitBits = genome.TraitBits,
            Rarity = genome.Rarity,
            CreatedUtcSeconds = createdUtcSeconds,
        };

        // ---------------- Tables ----------------

        private void FillVariantWeights(int[] list, ContentId variantA, ContentId variantB, out int ia, out int ib, out double mutation)
        {
            ia = IndexIn(list, variantA);
            ib = IndexIn(list, variantB);
            Array.Clear(_weights, 0, list.Length);
            double inherit;
            if (ia == ib)
            {
                inherit = _config.SameVariantKeep;
                _weights[ia] = inherit;
            }
            else
            {
                inherit = 2.0 * _config.ParentVariantShare;
                _weights[ia] = _config.ParentVariantShare;
                _weights[ib] = _config.ParentVariantShare;
            }
            mutation = 1.0 - inherit;

            var variants = _content.Variants;
            int top = Math.Max((int)variants[list[ia]].Rarity, (int)variants[list[ib]].Rarity);
            Array.Clear(_tierCounts, 0, _tierCounts.Length);
            for (int i = 0; i < list.Length; i++)
                if (IsMutationCandidate(list, i, ia, ib))
                    _tierCounts[(int)variants[list[i]].Rarity]++;
            var tierWeights = _config.MutationTierWeights;
            double total = 0.0;
            for (int o = 0; o < tierWeights.Length; o++)
            {
                int tier = top + GeneticsConfig.MutationLowestOffset + o;
                if (tier >= 0 && tier < _tierCounts.Length && _tierCounts[tier] > 0)
                    total += tierWeights[o];
            }
            if (total <= 0.0)
            {
                // Nothing to mutate into: the parents' variants share everything.
                if (ia == ib)
                    _weights[ia] = 1.0;
                else
                    _weights[ia] = _weights[ib] = 0.5;
                mutation = 0.0;
                return;
            }
            for (int i = 0; i < list.Length; i++)
            {
                if (!IsMutationCandidate(list, i, ia, ib))
                    continue;
                int tier = (int)variants[list[i]].Rarity;
                int o = tier - top - GeneticsConfig.MutationLowestOffset;
                if (o < 0 || o >= tierWeights.Length)
                    continue;
                _weights[i] += mutation * tierWeights[o] / total / _tierCounts[tier];
            }
        }

        private bool IsMutationCandidate(int[] list, int i, int ia, int ib) =>
            i != ia && i != ib && !_content.Variants[list[i]].IsSecret;

        private int IndexIn(int[] list, ContentId variant)
        {
            for (int i = 0; i < list.Length; i++)
                if (_content.Variants[list[i]].Id == variant)
                    return i;
            return 0; // removed variant: the species default, like the visual resolver
        }

        private double TraitChance(int trait, uint bitsA, uint bitsB)
        {
            var t = AnimalGenome.TraitAt(trait);
            int count = (AnimalGenome.Has(bitsA, t) ? 1 : 0) + (AnimalGenome.Has(bitsB, t) ? 1 : 0);
            return count == 2 ? _config.TraitBoth : count == 1 ? _config.TraitOne : _config.TraitNew[trait];
        }

        /// <summary>Effective chance of each trait, exclusions included (index = trait bit).</summary>
        public double EffectiveTraitChance(int trait, uint bitsA, uint bitsB)
        {
            double p = TraitChance(trait, bitsA, bitsB);
            if (trait == 1)
                return (1.0 - TraitChance(0, bitsA, bitsB)) * p;
            if (trait == 5)
                return (1.0 - TraitChance(4, bitsA, bitsB)) * p;
            return p;
        }

        private double PatternChance(in AnimalInstance a, in AnimalInstance b)
        {
            int carriers = (Carried(a, out _) != PatternChannel.None ? 1 : 0) + (Carried(b, out _) != PatternChannel.None ? 1 : 0);
            if (carriers == 0 && _patternsBySpecies[a.Species].Length == 0)
                return 0.0;
            return carriers == 2 ? _config.PatternBoth : carriers == 1 ? _config.PatternOne : _config.PatternNew;
        }

        /// <summary>The pattern an animal passes on: its carried one, else its variant's own pattern.</summary>
        private PatternChannel Carried(in AnimalInstance animal, out int strength)
        {
            var carried = AnimalGenome.CarriedPattern(animal.TraitBits);
            if (carried != PatternChannel.None)
            {
                strength = AnimalGenome.CarriedStrength(animal.TraitBits);
                return carried;
            }
            if (_content.Variants.TryGet(animal.Variant, out var variant) && variant.PatternChannel != PatternChannel.None)
            {
                strength = variant.PatternStrength;
                return variant.PatternChannel;
            }
            strength = 0;
            return PatternChannel.None;
        }
    }
}
