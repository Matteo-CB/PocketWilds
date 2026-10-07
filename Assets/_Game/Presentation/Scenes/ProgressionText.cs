namespace PocketWilds.Presentation
{
    /// <summary>
    /// T35A text keys (doc 02 Améliorations, section 11.7). The texts themselves live in the shipped localization
    /// tables, written by the UI pipeline.
    /// </summary>
    public static class ProgressionText
    {
        public const string ZoneOpenTitle = "zone.open.title";
        public const string ZoneOpenButton = "zone.open.button";
        public const string ZoneOpenPrice = "zone.open.price";
        public const string ZoneGainProduction = "zone.open.gain.production";
        public const string ZoneGainAnimals = "zone.open.gain.animals";
        public const string ZoneGainDecor = "zone.open.gain.decor";
        public const string ZoneLockedMastery = "zone.locked.mastery";
        public const string ZoneNeedLight = "zone.need_light";
        public const string ZoneLater = "zone.later";

        // Upgrades screen (I-SCR-039).
        public const string UpgradesTitle = "upgrades.title";
        public const string QtyOne = "upgrades.qty.one";
        public const string QtyTen = "upgrades.qty.ten";
        public const string QtyMax = "upgrades.qty.max";
        public const string ProductionName = "upgrades.production.name";
        public const string ProductionEffect = "upgrades.production.effect";
        public const string ProductionMilestone = "upgrades.production.milestone";
        public const string NestsName = "upgrades.nests.name";
        public const string NestsEffect = "upgrades.nests.effect";
        public const string CuriosityName = "upgrades.curiosity.name";
        public const string CuriosityEffect = "upgrades.curiosity.effect";
        public const string UpgradesMax = "upgrades.max";
        public const string UpgradesLockedMastery = "upgrades.locked.mastery";
        public const string UpgradesBest = "upgrades.best";
        public const string UpgradesTutorial = "upgrades.tutorial";
        public const string UpgradesLevel = "upgrades.level";
        public const string UpgradesLevels = "upgrades.levels";
        public const string UpgradesTotal = "upgrades.total";
        public const string UpgradesRateAfter = "upgrades.rate_after";
        public const string UpgradesNextZone = "upgrades.zone.next";
        public const string UpgradesNextBiome = "upgrades.biome.next";
        public const string UpgradesSee = "upgrades.zone.see";
        public const string UpgradesMasteryNext = "upgrades.mastery.next";
        public const string BiomeMasteryToast = "biome.mastery.up";

        // Refuge level (I-SCR-041) and its reward card.
        public const string RefugeLevelTitle = "refuge.level.title";
        public const string RefugeLevelUp = "refuge.level.up";
        public const string RefugeLevelScore = "refuge.level.score";
        public const string RefugeLevelRewards = "refuge.level.rewards";
        public const string RefugeLevelClaim = "refuge.level.claim";
        public const string SourceSpecies = "refuge.source.species";
        public const string SourceVariants = "refuge.source.variants";
        public const string SourceTraits = "refuge.source.traits";
        public const string SourceSpeciesMastery = "refuge.source.species_mastery";
        public const string SourceBiomes = "refuge.source.biomes";
        public const string SourceZones = "refuge.source.zones";
        public const string SourceBiomeMastery = "refuge.source.biome_mastery";
        public const string RewardGems = "reward.gems";
        public const string FeatureShop = "feature.shop";
        public const string RewardUnlock = "reward.unlock";

        // Biome gate (I-SCR-042).
        public const string BiomeGateTitle = "biome.gate.title";
        public const string BiomeGateSoon = "biome.gate.soon";
        public const string BiomeGateTomorrow = "biome.gate.day.tomorrow";
        public const string BiomeGateDaysIn = "biome.gate.day.in";
        public const string BiomeGateDay = "biome.gate.day";
        public const string BiomeGatePlay = "biome.gate.play";
        public const string BiomeGateLevel = "biome.gate.level";
        public const string BiomeGateSpecies = "biome.gate.species";

        /// <summary>Name of a biome's mastery, e.g. "Maîtrise de la Prairie" (key biome.&lt;id&gt;.mastery).</summary>
        public static string BiomeMastery(string biomeSourceId) => biomeSourceId + ".mastery";
    }
}
