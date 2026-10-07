namespace PocketWilds.Presentation
{
    /// <summary>
    /// T35B localization keys of the egg choice and the odds window (doc 02 Œufs, sections 3 and 6). Values live in
    /// LOC_en / LOC_fr (UiPipeline.Texts); UiTextKeys lists them for the tests.
    /// </summary>
    public static class EggText
    {
        /// <summary>The price pill of a Discovery egg.</summary>
        public const string Free = "egg.price.free";

        /// <summary>The Discovery egg's rules, before its table.</summary>
        public const string OddsDiscovery = "egg.odds.discovery";

        /// <summary>Format: {0} the targeted species' names.</summary>
        public const string OddsTargets = "egg.odds.targets";

        /// <summary>Format: {0} percent number.</summary>
        public const string OddsNewSpecies = "egg.odds.new_species";

        /// <summary>Format: {0} percent number.</summary>
        public const string OddsNewColor = "egg.odds.new_color";

        /// <summary>Format: {0} Discovery eggs in stock.</summary>
        public const string OddsStock = "egg.odds.stock";

        public static readonly string[] All = { Free, OddsDiscovery, OddsTargets, OddsNewSpecies, OddsNewColor, OddsStock };
    }
}
