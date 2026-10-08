namespace PocketWilds.Presentation
{
    /// <summary>
    /// Every pooled feedback effect (doc 07, H-VFX-001 to 012). Values index <see cref="VfxCatalog"/> entries at runtime
    /// only: they are never saved, so the order may change.
    /// </summary>
    public enum VfxId : byte
    {
        Crack = 0,
        HatchCommon = 1,
        HatchRare = 2,
        HatchEpic = 3,
        HatchLegendary = 4,
        HatchFabulous = 5,
        EggTap = 6,
        NewSpecies = 7,
        NewVariant = 8,
        MasteryLevelUp = 9,
        FavoriteHeart = 10,
        RareGlint = 11,

        /// <summary>T35A H-VFX-102: a zone opens: petals and pale leaves lift off along the sinking hedge.</summary>
        ZoneOpen = 12,

        /// <summary>T36 H-VFX-070 (doc 05 sheet 17): a wish comes true: its bubble bursts into soft bubbles, hearts and a ring.</summary>
        WishDone = 13,
        Count = 14,
    }

    /// <summary>What a hatch brings that the player did not have: shown by its own effect and the reveal banner.</summary>
    public enum HatchNews : byte
    {
        None = 0,
        NewVariant = 1,
        NewSpecies = 2,
    }
}
