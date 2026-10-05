namespace PocketWilds.UI
{
    /// <summary>
    /// Localization keys used by the UI (Writing Bible key style). Every key must exist in LOC_en and
    /// LOC_fr (checked by tests). Values live in the tables, never in code.
    /// </summary>
    public static class UiTextKeys
    {
        public const string OfflineWelcomeBack = "offline.welcome_back";
        public const string CollectButton = "ui.collect.button";
        public const string CodexTitle = "codex.title";
        public const string CodexVariants = "codex.variants";
        public const string CodexNew = "codex.new";
        public const string CodexUnknown = "codex.unknown";

        public static readonly string[] All = { OfflineWelcomeBack, CollectButton, CodexTitle, CodexVariants, CodexNew, CodexUnknown };
    }
}
