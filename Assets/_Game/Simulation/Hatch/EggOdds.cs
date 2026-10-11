#nullable enable
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>Where a Discovery egg came from (doc 02 Œufs, section 2.3; analytics discovery_egg_gain). Stable values.</summary>
    public enum DiscoverySource : byte
    {
        Debug = 0,
        Calendar = 1,
        WeeklyBook = 2,
        Expedition = 3,
        EventTrack = 4,
        DailyGift = 5,
        BiomeOpening = 6,
        Pass = 7,
        ReturnGift = 8,
    }

    /// <summary>Which rule of a Discovery egg gave the animal (doc 02 Œufs, section 2.3, in priority order). Stable values.</summary>
    public enum DiscoveryRule : byte
    {
        None = 0,

        /// <summary>A normal species not discovered yet, from an open biome.</summary>
        NewSpecies = 1,

        /// <summary>A base variant not discovered yet, of a discovered species.</summary>
        NewVariant = 2,

        /// <summary>Nothing left to find: a variant of rarity Rare or better.</summary>
        RareOrBetter = 3,
    }

    /// <summary>
    /// What the egg odds window shows (doc 02 Œufs, section 6): the rarity table this egg really rolls, the Focus targets,
    /// the protections running now and how far the new species guarantee is. Filled by <see cref="HatchService.GetOdds"/>
    /// from the same numbers the roll uses. One instance per window, refilled in place.
    /// </summary>
    public sealed class EggOdds
    {
        public const int MaxTargets = 8;

        public ContentId Egg;
        public EggTier Tier;

        /// <summary>Percentages per rarity tier (Common..Fabulous), summing to 100.</summary>
        public readonly double[] RarityPercent = new double[EggDefinition.RarityTierCount];

        /// <summary>Focus eggs: the species targeted right now.</summary>
        public readonly ContentId[] Targets = new ContentId[MaxTargets];
        public int TargetCount;

        /// <summary>Chance that the next egg gives a species not discovered yet through the protection (0 to 1).</summary>
        public double NewSpeciesBonus;

        /// <summary>Best chance, over the species the egg can give, that a new color replaces a known one (0 to 1).</summary>
        public double NewVariantBonus;

        /// <summary>Eggs until a new species is certain (0: none left to find in this egg).</summary>
        public int EggsToGuarantee;

        public int PityHardLimit;

        /// <summary>Discovery eggs: free and counted in a stock.</summary>
        public bool Free;
        public int Stock;

        /// <summary>
        /// T48B the Week-end des Œufs' bonus (events.weekend.egg_novelty_bonus, 0.5 = +50 %) on the new species and new color
        /// protections, already applied to <see cref="NewSpeciesBonus"/> and <see cref="NewVariantBonus"/>; 0 outside it.
        /// </summary>
        public double NoveltyBonus;

        /// <summary>
        /// T48B Discovery eggs: the chance the next egg gives something new (a species or a color not discovered yet): 1 while
        /// anything is left, 0 when nothing is left to discover.
        /// </summary>
        public double NoveltyChance;

        public void Clear()
        {
            Egg = ContentId.None;
            Tier = EggTier.Basic;
            for (int i = 0; i < RarityPercent.Length; i++)
                RarityPercent[i] = 0.0;
            TargetCount = 0;
            NewSpeciesBonus = 0.0;
            NewVariantBonus = 0.0;
            EggsToGuarantee = 0;
            PityHardLimit = 0;
            Free = false;
            Stock = 0;
            NoveltyBonus = 0.0;
            NoveltyChance = 0.0;
        }
    }

    /// <summary>One multi hatch, once every egg of it is out (doc 02 Œufs, section 8: multi_hatch).</summary>
    public readonly struct MultiHatchSummary
    {
        public readonly ContentId Egg;
        public readonly int Count;
        public readonly int RareOrBetter;
        public readonly int New;
        public readonly BigValue Cost;

        public MultiHatchSummary(ContentId egg, int count, int rareOrBetter, int newCount, BigValue cost)
        {
            Egg = egg;
            Count = count;
            RareOrBetter = rareOrBetter;
            New = newCount;
            Cost = cost;
        }
    }
}
