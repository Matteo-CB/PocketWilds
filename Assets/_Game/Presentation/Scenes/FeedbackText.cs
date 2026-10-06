using PocketWilds.Content;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T34A localization keys the refuge resolves itself (reveal banner, rarity names, egg odds, toasts, animal
    /// sheet). Values live in LOC_en / LOC_fr (UiPipeline.Texts); UiTextKeys lists them for the tests.
    /// </summary>
    public static class FeedbackText
    {
        public const string RevealNewSpecies = "reveal.new_species";
        public const string RevealNewVariant = "reveal.new_variant";

        public const string RarityCommon = "rarity.common";
        public const string RarityUncommon = "rarity.uncommon";
        public const string RarityRare = "rarity.rare";
        public const string RarityEpic = "rarity.epic";
        public const string RarityLegendary = "rarity.legendary";
        public const string RarityFabulous = "rarity.fabulous";

        public const string EggOddsTitle = "egg.odds.title";
        /// <summary>Format: {0} rarity name, {1} percent number (the % sign is in the text, spaced the French way in fr).</summary>
        public const string EggOddsLine = "egg.odds.line";
        /// <summary>Format: {0} eggs without a new species.</summary>
        public const string EggOddsGuarantee = "egg.odds.guarantee";
        public const string ModalOkButton = "ui.ok.button";
        public const string ModalCancelButton = "ui.cancel.button";

        public const string ToastShopOpen = "toast.shop_open";
        public const string ToastNotEnoughLight = "toast.not_enough_light";

        public const string SheetDetailsButton = "sheet.details.button";
        public const string SheetBond = "sheet.bond";

        public static readonly string[] All =
        {
            RevealNewSpecies, RevealNewVariant,
            RarityCommon, RarityUncommon, RarityRare, RarityEpic, RarityLegendary, RarityFabulous,
            EggOddsTitle, EggOddsLine, EggOddsGuarantee, ModalOkButton, ModalCancelButton,
            ToastShopOpen, ToastNotEnoughLight,
            SheetDetailsButton, SheetBond,
        };

        public static string RarityKey(RarityTier rarity)
        {
            switch (rarity)
            {
                case RarityTier.Uncommon: return RarityUncommon;
                case RarityTier.Rare: return RarityRare;
                case RarityTier.Epic: return RarityEpic;
                case RarityTier.Legendary: return RarityLegendary;
                case RarityTier.Fabulous: return RarityFabulous;
                default: return RarityCommon;
            }
        }
    }
}
