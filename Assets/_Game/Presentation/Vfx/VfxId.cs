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
        Count = 12,
    }

    /// <summary>What a hatch brings that the player did not have: shown by its own effect and the reveal banner.</summary>
    public enum HatchNews : byte
    {
        None = 0,
        NewVariant = 1,
        NewSpecies = 2,
    }
}
