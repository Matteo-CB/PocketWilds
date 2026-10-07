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

        /// <summary>Name of a biome's mastery, e.g. "Maîtrise de la Prairie" (key biome.&lt;id&gt;.mastery).</summary>
        public static string BiomeMastery(string biomeSourceId) => biomeSourceId + ".mastery";
    }
}
