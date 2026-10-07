#nullable enable
using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>Egg families from the Eggs prompts (C-EGG-001..015). Stable byte values.</summary>
    public enum EggTier : byte
    {
        Basic = 0,
        Focus = 1,
        Discovery = 2,
        Event = 3,
        Fabulous = 4,
    }

    /// <summary>One species an egg can hatch, with its relative weight in the pool.</summary>
    public readonly struct EggDropEntry
    {
        public readonly ContentId Species;
        public readonly double Weight;

        public EggDropEntry(ContentId species, double weight)
        {
            Species = species;
            Weight = weight;
        }
    }

    /// <summary>
    /// One egg type. Every egg shares the same mesh, mask texture and material: identity comes from
    /// the palette and how strongly each mask channel shows (Eggs prompts: no unique topology).
    /// Palette order: shell, mask R (spots), mask G (leaf band), mask B (flowers), crack, inner shell,
    /// accent, reserved. Economy (doc 02): Cost(n) = round(BaseCost x CostGrowth^n), n = eggs of this
    /// type bought in the current economic cycle. The rarity roll picks a tier, not a species.
    /// </summary>
    public sealed class EggDefinition : IContentDefinition
    {
        public const int PaletteSize = 8;
        public const int RarityTierCount = 6;

        /// <summary>Doc 02: after this many eggs without a new species, the next one gives one.</summary>
        public const int DefaultPityHardLimit = 20;

        public EggDefinition(ContentId id, string displayKey, ContentId biome, EggTier tier, Color32[] palette,
            byte maskStrengthR, byte maskStrengthG, byte maskStrengthB, string visualKey,
            double baseCost, double costGrowth, double[] rarityWeights, EggDropEntry[] pool, int pityHardLimit = DefaultPityHardLimit,
            ContentId starterSpecies = default, int unlockBiomeMastery = 0, int focusTargetCount = 0)
        {
            UnlockBiomeMastery = unlockBiomeMastery;
            FocusTargetCount = focusTargetCount;
            PityHardLimit = pityHardLimit;
            StarterSpecies = starterSpecies;
            Id = id;
            DisplayKey = displayKey;
            Biome = biome;
            Tier = tier;
            Palette = palette;
            MaskStrengthR = maskStrengthR;
            MaskStrengthG = maskStrengthG;
            MaskStrengthB = maskStrengthB;
            VisualKey = visualKey;
            BaseCost = baseCost;
            CostGrowth = costGrowth;
            RarityWeights = rarityWeights ?? Array.Empty<double>();
            Pool = pool ?? Array.Empty<EggDropEntry>();
        }

        public ContentId Id { get; }
        public string DisplayKey { get; }
        public ContentId Biome { get; }
        public EggTier Tier { get; }

        /// <summary>Exactly <see cref="PaletteSize"/> colors.</summary>
        public Color32[] Palette { get; }

        /// <summary>0 to 15 each.</summary>
        public byte MaskStrengthR { get; }
        public byte MaskStrengthG { get; }
        public byte MaskStrengthB { get; }

        /// <summary>Key of the shared egg view prefab.</summary>
        public string VisualKey { get; }

        /// <summary>Light cost of the first egg of this type in a cycle.</summary>
        public double BaseCost { get; }

        /// <summary>Cost multiplier per egg already bought in the cycle (1.18 for Grassland Basic).</summary>
        public double CostGrowth { get; }

        /// <summary>Relative weights indexed by RarityTier (Common..Fabulous), exactly <see cref="RarityTierCount"/>.</summary>
        public double[] RarityWeights { get; }

        public EggDropEntry[] Pool { get; }

        /// <summary>Eggs in a row without a new species before one is guaranteed (while any is missing).</summary>
        public int PityHardLimit { get; }

        /// <summary>
        /// Species of the free tutorial hatch when this egg is the first of a profile (Vertical Slice:
        /// the player starts with a rabbit). None: the tutorial hatch rolls the pool like any other.
        /// </summary>
        public ContentId StarterSpecies { get; }

        /// <summary>Biome mastery level that offers this egg (doc 02 Œufs, section 1: Focus at 3). 0: always offered.</summary>
        public int UnlockBiomeMastery { get; }

        /// <summary>
        /// Focus eggs (doc 02 Œufs, section 2.2): how many pool species the egg targets, those with the most undiscovered
        /// base variants (ties: pool order). 0: the whole pool.
        /// </summary>
        public int FocusTargetCount { get; }
    }
}
