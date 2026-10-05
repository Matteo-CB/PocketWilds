using UnityEngine;

namespace PocketWilds.UI
{
    /// <summary>
    /// UI color tokens (UI asset doc, COLOR TOKENS): every screen tints the same white kit sprites with
    /// these values instead of shipping recolored sprite copies. One asset: DEF_UiColorTokens.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/UI/Color Tokens", fileName = "DEF_UiColorTokens")]
    public sealed class UiColorTokens : ScriptableObject
    {
        public const int RarityCount = 7;

        [Header("Surfaces")]
        public Color backgroundLight = Hex(0xFFF8EC);
        public Color darkOverlay = new Color(0.11f, 0.13f, 0.12f, 0.55f);
        public Color panelPrimary = new Color(1f, 0.988f, 0.957f, 0.95f);
        public Color panelSecondary = Hex(0xF3EBDD);

        [Header("Text")]
        public Color textPrimary = Hex(0x4A3A2C);
        public Color textSecondary = Hex(0x8A7660);
        public Color textDisabled = Hex(0xA49C90);

        [Header("Actions and states")]
        public Color accentPrimary = Hex(0xFFB15E);
        public Color accentIdle = Hex(0xE3DCCF);
        public Color accentBusy = Hex(0xFFE7BF);
        public Color success = Hex(0x78B860);
        public Color warningSoft = Hex(0xF2C14E);
        public Color errorSoft = Hex(0xC9694F);

        [Header("Currencies")]
        public Color currencyLight = Hex(0xFFC23A);
        public Color currencyEssence = Hex(0x8FD1B0);
        public Color currencyGems = Hex(0x7FB2F0);

        [Header("Rarity (Common..Secret), always paired with a shape motif")]
        public Color[] rarity =
        {
            Hex(0xC9C2B6), Hex(0x86C26A), Hex(0x6AA8E8), Hex(0xB58AE6), Hex(0xF2B544), Hex(0xF08FB8), Hex(0x6B6F8F),
        };

        public static Color Hex(uint rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
