namespace PocketWilds.UI
{
    /// <summary>
    /// Localization keys used by the UI (Writing Guide key style). Every key must exist in LOC_en and
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

        public const string ShopTitle = "shop.title";
        public const string ShopFeatured = "shop.section.featured";
        public const string ShopGems = "shop.section.gems";
        public const string ShopBestValue = "shop.badge.best_value";
        public const string ShopOwned = "shop.badge.owned";
        public const string ShopOneTime = "shop.one_time";
        public const string ShopContentsGems = "shop.contents.gems";
        public const string ShopRestore = "shop.restore.button";
        public const string ShopThanks = "shop.purchase.thanks";
        public const string ShopThanksItem = "shop.purchase.thanks_item";
        public const string ShopFailed = "shop.purchase.failed";
        public const string ShopPending = "shop.purchase.pending";
        public const string ShopUnavailable = "shop.unavailable";
        public const string ShopNotEnoughGems = "shop.not_enough_gems";
        public const string ShopRestored = "shop.restore.done";
        public const string ShopNothingToRestore = "shop.restore.none";

        public static readonly string[] All =
        {
            OfflineWelcomeBack, CollectButton, CodexTitle, CodexVariants, CodexNew, CodexUnknown,
            ShopTitle, ShopFeatured, ShopGems, ShopBestValue, ShopOwned, ShopOneTime, ShopContentsGems, ShopRestore, ShopThanks, ShopThanksItem,
            ShopFailed, ShopPending, ShopUnavailable, ShopNotEnoughGems, ShopRestored, ShopNothingToRestore,
        };
    }
}
