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

        /// <summary>T42 (H-VFX-076): a small puff of dust in the biome's soil colour where an animal back from an expedition arrives (8 dots).</summary>
        ArrivalDust = 14,

        /// <summary>T43 (H-VFX-ACT-006-01): watering, 6 round drops falling on a sprout.</summary>
        GardenDrops = 15,

        /// <summary>T43 (H-VFX-ACT-006-02): a sprout reaches its next stage, a soft green ring.</summary>
        GardenGrow = 16,

        /// <summary>T43 (H-VFX-ACT-006-03, H-VFX-077): a harvest, 8 leaves or petals and a ring (tinted with the plant).</summary>
        GardenHarvest = 17,

        /// <summary>T43 (H-VFX-ACT-006-04): neighbours help, 2 small green hearts between two beds.</summary>
        GardenHelp = 18,

        /// <summary>T44C (doc 05 sheet 25): one round soft dot behind a walking visitor, played every 250 ms (8 visible at most).</summary>
        VisitorTrail = 19,

        /// <summary>T44C (doc 05 sheet 25, H-VFX-078): a visitor arrives, a soft warm ring on the grass where it stops.</summary>
        VisitorArrive = 20,
        Count = 21,
    }

    /// <summary>What a hatch brings that the player did not have: shown by its own effect and the reveal banner.</summary>
    public enum HatchNews : byte
    {
        None = 0,
        NewVariant = 1,
        NewSpecies = 2,
    }
}
