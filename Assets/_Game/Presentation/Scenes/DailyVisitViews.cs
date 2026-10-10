using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    // T36 « Rendez-vous du jour »: the view contracts between the presenter (Presentation assembly) and the HUD, the wish
    // card, the Today and journal screens and the return gift (UI assembly). Doc 01 Contenu sections 1 to 6, doc 06 screens
    // 002, 031 and 032, doc 05 Sensations sheets 17 to 19. Views never read the simulation: the presenter fills these
    // models once per change; views only touch their labels when a value changes.

    /// <summary>What one reward cell shows (RewardCellView): its icon follows the kind. Stable values.</summary>
    public enum RewardKind : byte
    {
        None = 0,
        Gems = 1,
        Light = 2,
        Ticket = 3,
        Treat = 4,
        Seed = 5,
        DiscoveryEgg = 6,
        Decoration = 7,
        Sticker = 8,
        FlowerSugar = 9,
        Bond = 10,

        /// <summary>
        /// T40: a pastry ingredient counted from a game (Nut Drop's Hazelnuts and Strawberries, doc 01 Contenu S-ACT-003
        /// « Objets gagnés »), shown by its item's own icon (RewardIconSet.items) and « ×n ».
        /// </summary>
        Ingredient = 11,
    }

    /// <summary>
    /// One reward as shown: an icon (by <see cref="Kind"/>, or by <see cref="Item"/> for a treat, a seed or a decoration)
    /// and an amount. Light shows the value really granted when known (<see cref="Light"/>), else its minutes of production.
    /// </summary>
    public struct RewardCell
    {
        public RewardKind Kind;
        public long Amount;
        public double LightMinutes;
        public BigValue Light;
        public ContentId Item;

        public RewardCell(RewardKind kind, long amount, ContentId item = default)
        {
            Kind = kind;
            Amount = amount;
            LightMinutes = 0.0;
            Light = BigValue.Zero;
            Item = item;
        }

        public bool IsEmpty => Kind == RewardKind.None;
    }

    /// <summary>Turns a rendez-vous bundle into reward cells, in the doc's display order (Gems, Light, eggs, the rest).</summary>
    public static class RewardCells
    {
        /// <summary>The most cells one bundle can need.</summary>
        public const int Max = 9;

        /// <summary>
        /// Writes the bundle's cells into <paramref name="into"/> (at least <see cref="Max"/> long) and returns how many.
        /// <paramref name="grantedLight"/>: the Light really given (zero before the grant: the cell shows minutes).
        /// </summary>
        public static int FromBundle(in RewardBundle bundle, RewardCell[] into, BigValue grantedLight = default)
        {
            int n = 0;
            if (bundle.Gems > 0)
                into[n++] = new RewardCell(RewardKind.Gems, bundle.Gems);
            if (bundle.LightMinutes > 0.0)
            {
                var cell = new RewardCell(RewardKind.Light, 0) { LightMinutes = bundle.LightMinutes, Light = grantedLight };
                into[n++] = cell;
            }
            if (bundle.DiscoveryEggs > 0)
                into[n++] = new RewardCell(RewardKind.DiscoveryEgg, bundle.DiscoveryEggs);
            if (bundle.Decoration != ContentId.None)
                into[n++] = new RewardCell(RewardKind.Decoration, 1, bundle.Decoration);
            if (bundle.Treats > 0)
                into[n++] = new RewardCell(RewardKind.Treat, bundle.Treats, bundle.Treat);
            if (bundle.Tickets > 0)
                into[n++] = new RewardCell(RewardKind.Ticket, bundle.Tickets);
            if (bundle.Seeds > 0)
                into[n++] = new RewardCell(RewardKind.Seed, bundle.Seeds, bundle.SeedPlant);
            if (bundle.Stickers > 0)
                into[n++] = new RewardCell(RewardKind.Sticker, bundle.Stickers);
            if (bundle.FlowerSugar > 0)
                into[n++] = new RewardCell(RewardKind.FlowerSugar, bundle.FlowerSugar, RewardGranter.FlowerSugar);
            for (int i = n; i < into.Length && i < Max; i++)
                into[i] = default;
            return n;
        }
    }

    /// <summary>
    /// The icon of a wish, by its system (the bubble, the card and the Today rows share it; pipelines load the sprites by
    /// these names from Assets/_Game/Art/UI/Icons). A fulfilled wish shows ICO_Wish_Done instead.
    /// </summary>
    public static class WishIcons
    {
        public const string Done = "ICO_Wish_Done";
        public const string Default = "ICO_Wish";

        public static string SpriteName(WishSystem system)
        {
            switch (system)
            {
                case WishSystem.Hatching: return "ICO_Hatch_Egg";
                case WishSystem.DiscoveriesAndMastery: return "ICO_Mastery_StarPaw";
                case WishSystem.CuddlesAndTreats: return "ICO_Treat";
                case WishSystem.Upgrades: return "ICO_Upgrade";
                case WishSystem.Expansions: return "ICO_ZoneSign";
                default: return Default;
            }
        }
    }

    // ------------------------------------------------------------------------------------------- HUD (screen 002)

    /// <summary>The HUD's T36 parts (UI assembly, RefugeHud): the Today button on the right rail, its dot, the Gems chip.</summary>
    public interface IDailyHud
    {
        void BindDaily(IDailyHudActions actions);

        /// <summary>The Today button (doc 01 section 1.3: after the welcome, refuge level 2) and its dot.</summary>
        void SetToday(bool visible, bool dot);

        /// <summary>The Today button appears with a 0.35 s bounce (A12), when no screen is open. Once.</summary>
        void PlayTodayAppear();

        /// <summary>The tutorial paw taps on the Today button (A12) until the player opens it.</summary>
        void SetTodayHint(bool visible);

        /// <summary>
        /// The Gems chip, shown from the first Gem gained (A10) whether or not the shop is open (the shop button keeps
        /// <see cref="IRefugeHud.SetShop"/>). Implementations show the chip when either asks for it.
        /// </summary>
        void SetGems(bool visible, long gems);

        /// <summary>Gems motes fly from a screen point into the Gems chip, which pops as they land (doc 05 sheet 17).</summary>
        void PlayGemsCollect(Vector2 fromScreenPosition);

        bool TodayVisible { get; }

        /// <summary>Screen point of the Today button's center (tutorial paw, motes).</summary>
        Vector2 TodayScreenPosition { get; }

        /// <summary>Screen point of the Hatch button's center (A12: the paw shows Éclore once for the free Discovery egg).</summary>
        Vector2 HatchScreenPosition { get; }
    }

    public interface IDailyHudActions
    {
        void OpenToday();
    }

    // ------------------------------------------------------------------------------------------- wish card (screen 002)

    /// <summary>One wish as the card and the Today rows show it. Text is built once by the presenter (DailyVisitText).</summary>
    public struct WishCardData
    {
        public string Text;
        public int Progress;
        public int Target;
        public bool Done;
        public bool Tutorial;
        public WishSystem System;
        public bool CanSwap;
    }

    public interface IWishCardActions
    {
        void ClaimWishFromCard();
        void SwapWishFromCard();

        /// <summary>The card closed by itself (a tap beside it, back).</summary>
        void WishCardClosed();
    }

    /// <summary>
    /// The small card opened by a tap on a wish bubble (doc 01 A08, doc 06 screen 002): the wish's text, its progress n/target
    /// and its rewards, anchored at the bubble's screen point; closes on a tap beside it. One pooled panel (UI assembly).
    /// </summary>
    public interface IWishCardView
    {
        bool IsOpen { get; }

        void Open(in WishCardData data, RewardCell[] rewards, int rewardCount, Vector2 screenPoint, ILocalization text,
            IWishCardActions actions);

        void Refresh(in WishCardData data);

        /// <summary>The bubble moved (the animal walks): the card follows, clamped inside the safe area.</summary>
        void Follow(Vector2 screenPoint);

        void Close();

        void Tick(float unscaledDeltaTime);

        void Prewarm(ILocalization text);

        bool IsOverCard(Vector2 screenPoint);
    }

    // ------------------------------------------------------------------------------------------- animal sheet: treats

    /// <summary>The animal sheet's « Friandise » button (doc 01 section 10.1), shown when a treat is owned.</summary>
    public struct SheetTreatData
    {
        public bool Available;
        public ContentId Treat;
        public int Owned;

        /// <summary>The animal's family prefers this treat (+50 %): a small heart beside the button.</summary>
        public bool Preferred;
    }

    /// <summary>Implemented by the animal sheet next to <see cref="IAnimalSheetView"/> (optional: older sheets ignore treats).</summary>
    public interface IAnimalSheetTreatView
    {
        void SetTreat(in SheetTreatData data);

        /// <summary>The treat was eaten: hearts and the bond bar grow by <paramref name="bondFraction"/> (0 to 1 of the bar).</summary>
        void PlayTreatGiven(float bondFraction);
    }

    /// <summary>Implemented by the sheet's actions next to <see cref="IAnimalSheetActions"/>.</summary>
    public interface IAnimalSheetTreatActions
    {
        void GiveTreat(uint instanceId);
    }

    /// <summary>
    /// T38: the sheet's « Brosser » button (doc 01 S-ACT-001: the animal is chosen in its sheet). Implemented by the animal
    /// sheet next to <see cref="IAnimalSheetView"/>; hidden while Grooming is not open.
    /// </summary>
    public interface IAnimalSheetGroomView
    {
        /// <summary>The button shows, with « Gratuit » for the free first game, else the ticket cost (1).</summary>
        void SetGroom(bool available, bool free);
    }

    public interface IAnimalSheetGroomActions
    {
        void GroomAnimal(uint instanceId);
    }

    // ------------------------------------------------------------------------------------------- Today (screen 031)

    public enum TodayGiftState : byte
    {
        /// <summary>No gift today (the flag is off).</summary>
        Hidden = 0,

        /// <summary>The closed box waits: « Ouvrir ».</summary>
        Ready = 1,

        /// <summary>Opened today: the lot stays shown until midnight, « À demain ».</summary>
        Opened = 2,
    }

    public struct TodayWishRow
    {
        public WishCardData Card;

        /// <summary>The animal that expresses the wish (portrait from its variant).</summary>
        public uint Animal;

        public ContentId Variant;

        /// <summary>The animal's name (its custom name, else its species).</summary>
        public string AnimalName;

        public long Gems;
    }

    public struct CalendarBoxCell
    {
        public RewardCell Reward;
        public bool Big;
        public bool Stamped;

        /// <summary>The box today's stamp goes to (or went to).</summary>
        public bool Today;
    }

    /// <summary>What the Today screen shows (doc 01 sections 1.4, 2.6, 3, 4, 5; doc 06 screen 031). Filled by the presenter.</summary>
    public interface ITodayModel
    {
        /// <summary>The welcome gift shown (0 to 2), or -1 when the three are claimed.</summary>
        int WelcomeIndex { get; }

        bool WelcomeClaimable { get; }

        int WelcomeRewards(RewardCell[] into);

        TodayGiftState Gift { get; }

        /// <summary>The opened lot's size (band: cream, sky blue, gold).</summary>
        GiftSize GiftSize { get; }

        /// <summary>The opened lot's cells (only when <see cref="Gift"/> is Opened).</summary>
        int GiftRewards(RewardCell[] into);

        int WishCount { get; }

        TodayWishRow Wish(int row);

        int SwapsLeft { get; }

        bool AnyWishDone { get; }

        int CalendarCycle { get; }

        /// <summary>The next box to stamp, 0 to 27 (today's box while <see cref="CalendarClaimable"/>).</summary>
        int CalendarNext { get; }

        bool CalendarClaimable { get; }

        CalendarBoxCell Box(int box);

        /// <summary>Tomorrow's box reward (« Demain : … »).</summary>
        RewardCell Tomorrow { get; }

        /// <summary>Stamps until the next big box, counting it (1 when the next box is big).</summary>
        int DaysToBigBox { get; }

        bool JournalVisible { get; }
        int JournalReached { get; }
        int JournalGoals { get; }
        bool JournalDot { get; }
    }

    public interface ITodayActions
    {
        void ClaimWelcome();
        void OpenGift();
        void ClaimWish(int row);
        void ClaimAllWishes();
        void SwapWish(int row);
        void StampCalendar();
        void OpenJournal();
        void OpenGiftInfo();
        void CloseToday();
    }

    /// <summary>
    /// PF_UI_Today (screen 031): a bottom sheet with the refuge visible above, dismissible in one gesture (scrim, back,
    /// close), completable in under two minutes. Closed: canvas disabled, no Update; ticked by the presenter.
    /// </summary>
    public interface ITodayView
    {
        bool IsOpen { get; }

        void Open(ITodayModel model, ILocalization text, ITodayActions actions);

        /// <summary>The model changed (a claim, a swap, a progress): rows and buttons update; no rebuild.</summary>
        void Refresh();

        void PlayWelcomeClaimed();

        /// <summary>The lid lifts and the lot's cells are revealed one by one (doc 05 sheet 18); the model is already Opened.</summary>
        void PlayGiftOpened();

        void PlayWishClaimed(int row);

        /// <summary>The paw stamp lands on the box (doc 05 sheet 19).</summary>
        void PlayStamp(int box);

        /// <summary>Screen point of the gift box (the tutorial paw of A12), or of the screen center when hidden.</summary>
        Vector2 GiftScreenPosition { get; }

        /// <summary>
        /// A12: the tutorial paw (the same paw as the HUD's TapHint: plush paw tapping with a soft ring, still with Reduce
        /// Motion) taps on the gift box inside the screen, above its canvas, until the box is opened.
        /// </summary>
        void SetGiftHint(bool visible);

        /// <summary>Screen point of a reward cell just claimed (motes start there).</summary>
        Vector2 RowScreenPosition(int row);

        void Close();

        void Tick(float unscaledDeltaTime);

        void Prewarm(ITodayModel model, ILocalization text, ITodayActions actions);
    }

    // ------------------------------------------------------------------------------------------- weekly journal (032)

    public struct JournalGoalRow
    {
        public string Text;
        public int Progress;
        public int Target;
        public bool Reached;
        public bool Claimed;
        public JournalSlot Slot;

        /// <summary>The goal started ahead (first journal): « Tu as déjà commencé ».</summary>
        public bool HeadStart;

        public RewardCell Reward0;
        public RewardCell Reward1;
    }

    public interface IJournalModel
    {
        int GoalCount { get; }
        JournalGoalRow Goal(int i);

        /// <summary>Days left, counting today (1 on the last day).</summary>
        int DaysLeft { get; }

        bool IsFirst { get; }
        bool CanClaimComplete { get; }
        bool CompleteClaimed { get; }

        /// <summary>The completion's cells (Gems and a Discovery egg).</summary>
        int CompleteRewards(RewardCell[] into);

        bool HasCarry { get; }
        int CarryRewards(RewardCell[] into);
    }

    public interface IJournalActions
    {
        void ClaimGoal(int i);
        void ClaimComplete();
        void ClaimCarry();
        void CloseJournal();
    }

    /// <summary>PF_UI_WeeklyJournal (screen 032): 7 goals with progress, the completion chest, the real days left.</summary>
    public interface IJournalView
    {
        bool IsOpen { get; }
        void Open(IJournalModel model, ILocalization text, IJournalActions actions);
        void Refresh();
        void PlayGoalClaimed(int i);
        void PlayCompleteClaimed();
        Vector2 GoalScreenPosition(int i);
        void Close();
        void Tick(float unscaledDeltaTime);
        void Prewarm(IJournalModel model, ILocalization text, IJournalActions actions);
    }

    // ------------------------------------------------------------------------------------------- return gift (section 6)

    public struct ReturnGiftCard
    {
        public bool Big;
        public int DaysAway;

        /// <summary>« Pendant ton absence » lines, already formatted (up to 4; null for fewer).</summary>
        public string Line0;
        public string Line1;
        public string Line2;
        public string Line3;
    }

    /// <summary>
    /// Implemented by the offline return screen next to <see cref="IOfflineReturnView"/>: a gift block under the Light
    /// (title, subtitle, reward cells, lines). Called before <see cref="IOfflineReturnView.Show"/>; one « Récupérer »
    /// collects the Light and the gift. <see cref="ClearGift"/> hides it for an ordinary return.
    /// </summary>
    public interface IOfflineReturnGiftView
    {
        void SetGift(in ReturnGiftCard card, RewardCell[] rewards, int rewardCount, ILocalization text);
        void ClearGift();
    }
}
