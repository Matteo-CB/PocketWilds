using PocketWilds.Content;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>How a rare animal shows it in the meadow (doc 07, G-SH-006).</summary>
    public enum RareGlowKind : byte
    {
        None = 0,
        /// <summary>Epic or Shiny: an occasional white glint, no rim glow.</summary>
        Glint = 1,
        /// <summary>Legendary or Golden: warm gold rim and gold glints.</summary>
        Gold = 2,
        /// <summary>Fabulous: soft sky-to-pink rim and pastel glints.</summary>
        Sky = 3,
    }

    /// <summary>
    /// SH_RareGlow module (T34A). Option 3 of the doc for the rim, nearly free: the level rides in bits 17-20 of the
    /// renderer user value SH_AnimalMaster already reads (1-7 warm gold, 8-15 sky gradient, 0 off), no extra
    /// renderer, no material, no light. Option 1 for the glints: pooled round bubbles of light near one visible rare
    /// animal at a time (<see cref="VfxId.RareGlint"/>), half as often on Low. Never a four-point sparkle star.
    /// </summary>
    public static class RareGlow
    {
        public const int LegendaryGold = 3;
        public const int GoldenGold = 5;
        /// <summary>8 + 4: sky gradient at half strength.</summary>
        public const int FabulousSky = 12;

        public static RareGlowKind KindOf(RarityTier rarity, uint traitBits)
        {
            if (rarity >= RarityTier.Fabulous)
                return RareGlowKind.Sky;
            if (rarity == RarityTier.Legendary || AnimalGenome.Has(traitBits, AnimalTrait.Golden))
                return RareGlowKind.Gold;
            if (rarity == RarityTier.Epic || AnimalGenome.Has(traitBits, AnimalTrait.Shiny))
                return RareGlowKind.Glint;
            return RareGlowKind.None;
        }

        /// <summary>The 4-bit level SH_AnimalMaster reads: subtle, a plush glow and never a light bulb.</summary>
        public static int ShaderLevel(RarityTier rarity, uint traitBits)
        {
            if (rarity >= RarityTier.Fabulous)
                return FabulousSky;
            if (AnimalGenome.Has(traitBits, AnimalTrait.Golden))
                return GoldenGold;
            if (rarity == RarityTier.Legendary)
                return LegendaryGold;
            return 0;
        }

        /// <summary>Glint color for a kind (the rare glint's start color).</summary>
        public static Color GlintColor(RareGlowKind kind, float pick)
        {
            switch (kind)
            {
                case RareGlowKind.Gold: return new Color(1f, 0.86f, 0.52f, 1f);
                case RareGlowKind.Sky: return Color.Lerp(new Color(1f, 0.74f, 0.9f, 1f), new Color(0.7f, 0.86f, 1f, 1f), pick);
                default: return new Color(1f, 0.97f, 0.92f, 1f);
            }
        }
    }
}
