using System;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>T44C: where the journal screen lives, its sizes and its pictures (shared by the presenter, the view, the pipeline and the capture layer).</summary>
    public static class JournalLayout
    {
        public const string ScreenAddress = "journal.screen";
        public const string ScreenPrefabName = "PF_UI_Journal";
        public const string PrefabPath = "Assets/_Game/Prefabs/Journal/" + ScreenPrefabName + ".prefab";
        public const string Group = "Journal";

        /// <summary>Over the profile screen (23) it opens from.</summary>
        public const int SortingOrder = 24;

        /// <summary>The timeline: 6 pooled page cards and 4 pooled month headers, recycled over every page.</summary>
        public const int CardCells = 6;

        public const int HeaderCells = 4;

        /// <summary>The « Moments » list: 12 pooled rows for the 50 surprises.</summary>
        public const int MomentCells = 12;

        public const int MomentsTotal = 50;

        /// <summary>The timeline's steps (reference 1080 × 2340): a card, a month header, a moment row.</summary>
        public const float CardPitch = 380f;

        public const float HeaderPitch = 116f;
        public const float MomentPitch = 150f;
        public const float ListPadding = 24f;

        /// <summary>Doc section 6 « Images »: captures are 512 × 512, shown at about 300.</summary>
        public const int PictureSize = 512;

        public const float PictureShown = 300f;

        /// <summary>The page text's buffer (a memory with its variables filled) and the variables a page may carry.</summary>
        public const int TextCapacity = 360;

        public const int MaxVariables = 12;

        /// <summary>The captures' folder under the persistent data path, and their JPEG quality (about 40 KB at 512 × 512).</summary>
        public const string Folder = "Journal";

        public const string Prefix = "journal_";
        public const string Extension = ".jpg";
        public const int JpegQuality = 80;

        /// <summary>Doc: at most one capture a second, never during a hatch reveal, an activity, a purchase or a level up.</summary>
        public const float CaptureGapSeconds = 1f;

        /// <summary>Where the portrait stage is built, far below the refuge (away from the activity sets at -60 to -180).</summary>
        public static readonly Vector3 StagePosition = new Vector3(-420f, -420f, 0f);

        public static string FileName(int pageId) => Prefix + pageId.ToString(System.Globalization.CultureInfo.InvariantCulture) + Extension;
    }

    public enum JournalTab : byte
    {
        Pages = 0,
        Moments = 1,
    }

    /// <summary>The small icon of the system a page belongs to (derived from its memory key).</summary>
    public enum JournalSystem : byte
    {
        Refuge = 0,
        Hatch = 1,
        Collection = 2,
        Biome = 3,
        Activity = 4,
        Expedition = 5,
        Garden = 6,
        Album = 7,
        Decoration = 8,
        Bond = 9,
        Event = 10,
        Visitor = 11,
        Mastery = 12,
        Renewal = 13,
        Achievement = 14,
        Daily = 15,
        Photo = 16,
        Surprise = 17,
    }

    /// <summary>What a card's picture shows: the stored picture, the animal's codex portrait, or the system's icon (the moment's thumbnail).</summary>
    public enum JournalPictureSource : byte
    {
        Icon = 0,
        Codex = 1,
        Texture = 2,
    }

    /// <summary>A month header of the timeline (« Octobre 2026 »): its text from a char buffer.</summary>
    public struct JournalHeaderModel
    {
        public bool Shown;
        public char[] Text;
        public int Length;
    }

    /// <summary>A page card: date, picture, the text with its variables filled, the system icon, « Nouveau » for the pages added since the last visit.</summary>
    public struct JournalCardModel
    {
        public bool Shown;
        public char[] Date;
        public int DateLength;
        public char[] Text;
        public int TextLength;
        public JournalSystem System;
        public JournalPictureSource Source;
        public Texture Picture;

        /// <summary>The stored picture's visible part (a postcard is 512 × 768: its middle square).</summary>
        public Rect PictureRect;

        public ContentId Variant;
        public bool New;
    }

    /// <summary>A seen surprise in the « Moments » list.</summary>
    public struct JournalMomentModel
    {
        public bool Shown;
        public string Name;
    }

    /// <summary>The journal screen's touches (raised by the view, handled by <see cref="JournalScreenPresenter"/>).</summary>
    public interface IJournalScreenActions
    {
        void CloseScreen();

        void SelectTab(JournalTab tab);

        /// <summary>A timeline cell shows <paramref name="item"/> (a header or a card, as the presenter's timeline says).</summary>
        void FillTimeline(int item, bool header, int cell);

        void FillMoment(int cell, int item);
    }

    /// <summary>
    /// T44C: PF_UI_Journal's view (implemented in Game.UI by JournalScreenView, built by JournalScreenPipeline): the title and
    /// close, two tabs (the timeline of pages and « Moments »), a recycled timeline of month headers and page cards, a recycled
    /// list of the seen surprises with its empty state. Built once with pooled cells: nothing is instantiated after, and no call
    /// allocates once <see cref="Bind"/> ran. The UI kit's sprites, the icons and the fonts are borrowed from the scene.
    /// </summary>
    public interface IJournalScreenView
    {
        bool Valid { get; }

        bool Shown { get; }

        bool ReduceMotion { get; set; }

        void Bind(ILocalization text, IJournalScreenActions actions);

        void Show();

        void Hide();

        void SetTab(JournalTab tab);

        JournalTab TabShown { get; }

        /// <summary>The timeline: one kind per item (true: a month header), the content sized from their pitches.</summary>
        void SetTimeline(bool[] headers, int count, bool toTop);

        /// <summary>The visible timeline cells ask for their items again.</summary>
        void RefreshTimeline();

        void SetHeader(int cell, in JournalHeaderModel model);

        void SetCard(int cell, in JournalCardModel model);

        void SetMoments(int count, bool toTop);

        void SetMoment(int cell, in JournalMomentModel model);

        /// <summary>The card cell showing timeline item <paramref name="item"/> (-1: scrolled away).</summary>
        int CardCellOf(int item);

        /// <summary>Whether a card cell is in use (its picture is worth loading).</summary>
        bool CardCellShown(int cell);

        void Tick(float deltaTime);
    }

    /// <summary>T44C: the « Journal » button on the profile card (implemented by the profile screen's view), with its dot while pages are unseen.</summary>
    public interface IJournalEntryView
    {
        void BindJournal(Action open);

        void SetJournal(bool shown, bool dot);
    }

    /// <summary>T44C's screen text keys (Editor JournalScreenTexts holds the shipped texts). A page's text is « {key}.text » (agent CC's JournalTexts).</summary>
    public static class JournalText
    {
        public const string Title = "refuge_journal.title";
        public const string Entry = "refuge_journal.entry";
        public const string TabPages = "refuge_journal.tab.pages";
        public const string TabMoments = "refuge_journal.tab.moments";
        public const string Empty = "refuge_journal.empty";
        public const string MomentsEmpty = "refuge_journal.moments_empty";
        public const string New = "refuge_journal.new";

        /// <summary>« {day} {month} {year} » / "{month} {day}, {year}" and the header « {month} {year} ».</summary>
        public const string Date = "refuge_journal.date";

        public const string MonthHeader = "refuge_journal.month_header";

        /// <summary>The month's name as a header starts with it (« Octobre ») and inside a date (« octobre »): 1 to 12.</summary>
        public static readonly string[] Months =
        {
            "refuge_journal.month.01", "refuge_journal.month.02", "refuge_journal.month.03", "refuge_journal.month.04", "refuge_journal.month.05",
            "refuge_journal.month.06", "refuge_journal.month.07", "refuge_journal.month.08", "refuge_journal.month.09", "refuge_journal.month.10",
            "refuge_journal.month.11", "refuge_journal.month.12",
        };

        public static readonly string[] MonthsInDate =
        {
            "refuge_journal.month_in_date.01", "refuge_journal.month_in_date.02", "refuge_journal.month_in_date.03", "refuge_journal.month_in_date.04",
            "refuge_journal.month_in_date.05", "refuge_journal.month_in_date.06", "refuge_journal.month_in_date.07", "refuge_journal.month_in_date.08",
            "refuge_journal.month_in_date.09", "refuge_journal.month_in_date.10", "refuge_journal.month_in_date.11", "refuge_journal.month_in_date.12",
        };

        /// <summary>A visitor joins the collection: « Nouveau visiteur ! ».</summary>
        public const string NewVisitor = "refuge_visitor.new";

        /// <summary>The gift's pop, counted (« Cadeau : +{n} Gemmes »…); three rows each (.one, .other and the key).</summary>
        public const string GiftGems = "refuge_visitor.gift.gems";

        public const string GiftLight = "refuge_visitor.gift.light";
        public const string GiftTreats = "refuge_visitor.gift.treats";
        public const string GiftSeeds = "refuge_visitor.gift.seeds";
        public const string GiftTickets = "refuge_visitor.gift.tickets";
        public const string GiftStickers = "refuge_visitor.gift.stickers";
        public const string GiftSome = "refuge_visitor.gift.some";

        /// <summary>A memory's text and a surprise's name.</summary>
        public static string PageText(string key) => key + ".text";

        public static string SurpriseName(string key) => key + ".name";

        /// <summary>The system of a memory, from its key (memory.first_hatch → Hatch). Never allocates.</summary>
        public static JournalSystem SystemOf(string key, JournalImageKind image)
        {
            if (string.IsNullOrEmpty(key))
                return JournalSystem.Refuge;
            if (Has(key, "visitor"))
                return JournalSystem.Visitor;
            if (Has(key, "hatch") || Has(key, "egg"))
                return JournalSystem.Hatch;
            if (Has(key, "expedition") || Has(key, "postcard"))
                return JournalSystem.Expedition;
            if (Has(key, "garden") || Has(key, "plant") || Has(key, "harvest"))
                return JournalSystem.Garden;
            if (Has(key, "album") || Has(key, "sticker"))
                return JournalSystem.Album;
            if (Has(key, "decor"))
                return JournalSystem.Decoration;
            if (Has(key, "bond") || Has(key, "companion") || Has(key, "friend") || Has(key, "baby") || Has(key, "parent"))
                return JournalSystem.Bond;
            if (Has(key, "event") || Has(key, "season"))
                return JournalSystem.Event;
            if (Has(key, "master"))
                return JournalSystem.Mastery;
            if (Has(key, "renew") || Has(key, "essence"))
                return JournalSystem.Renewal;
            if (Has(key, "achievement") || Has(key, "title") || Has(key, "frame"))
                return JournalSystem.Achievement;
            if (Has(key, "activity") || Has(key, "score") || Has(key, "record") || Has(key, "game"))
                return JournalSystem.Activity;
            if (Has(key, "wish") || Has(key, "daily") || Has(key, "gift") || Has(key, "notebook"))
                return JournalSystem.Daily;
            if (Has(key, "photo"))
                return JournalSystem.Photo;
            if (Has(key, "surprise") || Has(key, "moment"))
                return JournalSystem.Surprise;
            if (Has(key, "biome") || Has(key, "zone"))
                return JournalSystem.Biome;
            if (Has(key, "species") || Has(key, "variant") || Has(key, "codex") || Has(key, "collection") || Has(key, "animal"))
                return JournalSystem.Collection;
            return image == JournalImageKind.AnimalCapture ? JournalSystem.Collection
                : image == JournalImageKind.Postcard ? JournalSystem.Expedition
                : image == JournalImageKind.Photo ? JournalSystem.Photo
                : JournalSystem.Refuge;
        }

        private static bool Has(string key, string word) => key.IndexOf(word, StringComparison.Ordinal) >= 0;
    }
}
