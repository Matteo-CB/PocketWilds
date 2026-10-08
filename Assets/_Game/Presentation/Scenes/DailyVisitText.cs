namespace PocketWilds.Presentation
{
    /// <summary>
    /// T36 text keys (doc 01 Contenu; Writing Guide: short, no em dash, no pressure words, real days never a countdown).
    /// The texts live in the shipped localization tables, written by the UI pipeline from Editor/UI/DailyVisitTexts.cs.
    /// Wish and journal sentences use the catalogs' keys (WishCatalog.TextKey, WeeklyJournalCatalog.TextKey) with their
    /// parameters ({n}, {species}, {biome}, {animal}).
    /// </summary>
    public static class DailyVisitText
    {
        // HUD rail and toasts.
        public const string HudToday = "hud.today";
        public const string ToastWishDone = "toast.wish_done";
        public const string ToastJournalNew = "toast.journal_new";
        public const string ToastGemsFirst = "toast.gems_first";

        // Today (screen 031).
        public const string TodayTitle = "today.title";
        public const string TodayClaim = "today.claim";
        public const string TodayClaimAll = "today.claim_all";
        public const string TodayClose = "today.close";

        // Welcome gifts (section 1.4).
        public const string WelcomeTitle = "daily.welcome.title";
        public const string WelcomeDay = "daily.welcome.day";

        // Daily gift (section 3).
        public const string GiftTitle = "daily.gift.title";
        public const string GiftOpen = "daily.gift.open";
        public const string GiftTomorrow = "daily.gift.tomorrow";
        public const string GiftSmall = "daily.gift.small";
        public const string GiftNice = "daily.gift.nice";
        public const string GiftBig = "daily.gift.big";
        public const string GiftInfoTitle = "daily.gift.info.title";
        public const string GiftInfo = "daily.gift.info";

        // Wishes (section 2).
        public const string WishesTitle = "wish.title";
        public const string WishWaiting = "wish.waiting";
        public const string WishDone = "wish.done";
        public const string WishSwap = "wish.swap";
        public const string WishSwapUsed = "wish.swap_used";
        public const string WishClaim = "wish.claim";
        public const string WishProgress = "wish.progress";
        public const string WishTutorial = "wish.t01";

        // Calendar (section 4).
        public const string CalendarTitle = "daily.calendar.title";
        public const string CalendarStamp = "daily.calendar.stamp";
        public const string CalendarTomorrow = "daily.calendar.tomorrow";
        public const string CalendarBigIn = "daily.calendar.big_in";
        public const string CalendarBigTomorrow = "daily.calendar.big_tomorrow";
        public const string CalendarCycle = "daily.calendar.cycle";
        public const string CalendarDone = "daily.calendar.done";

        // Weekly journal (section 5, screen 032).
        public const string JournalTitle = "journal.title";
        public const string JournalRow = "journal.row";
        public const string JournalDaysLeft = "journal.days_left";
        public const string JournalLastDay = "journal.last_day";
        public const string JournalFirst = "journal.first";
        public const string JournalHeadStart = "journal.head_start";
        public const string JournalClaim = "journal.claim";
        public const string JournalComplete = "journal.complete";
        public const string JournalCarry = "journal.carry";
        public const string JournalEachGoal = "journal.each_goal";

        // Return gifts (section 6).
        public const string ReturnTitle = "return.title";
        public const string ReturnSubtitle = "return.subtitle";
        public const string ReturnSubtitleBig = "return.subtitle_big";
        public const string ReturnLineWishes = "return.line.wishes";
        public const string ReturnLineGift = "return.line.gift";
        public const string ReturnLineJournal = "return.line.journal";
        public const string ReturnLineCalendar = "return.line.calendar";

        // Rewards (cells and their accessibility labels).
        public const string RewardGems = "reward.gems";
        public const string RewardLightMinutes = "reward.light_minutes";
        public const string RewardTickets = "reward.tickets";
        public const string RewardTreats = "reward.treats";
        public const string RewardSeeds = "reward.seeds";
        public const string RewardEgg = "reward.discovery_egg";
        public const string RewardDecoration = "reward.decoration";
        public const string RewardSticker = "reward.sticker";
        public const string RewardFlowerSugar = "reward.flower_sugar";
        public const string RewardBond = "reward.bond";

        // Animal sheet (section 10.1).
        public const string SheetTreat = "sheet.treat";
    }
}
