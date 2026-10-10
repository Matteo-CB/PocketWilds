#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    // T44A « Albums » (doc « 01 Albums et autocollants », sections 1 to 6): the contract between the albums (simulation:
    // the 720 stickers' data, the draws with their guarantees, duplicates and album stars, page and album rewards, the
    // sticker sources of every system) and their presentation (the albums screen, the sticker reveal, the captured sticker
    // art). Frozen for T44A: the simulation implements it, the presentation only uses it.

    public enum StickerRarity : byte
    {
        Common = 0,
        Rare = 1,

        /// <summary>« Brillant »: a pearl and a slow pearly sheen, never a pointed glint.</summary>
        Shiny = 2,
    }

    /// <summary>The four kinds of album (section 1.1).</summary>
    public enum AlbumKind : byte
    {
        Biome = 0,
        Event = 1,
        Season = 2,
        Activity = 3,
    }

    /// <summary>The source an empty slot shows as its hint icon (section 1.12).</summary>
    public enum StickerHint : byte
    {
        Activity = 0,
        Record = 1,
        Challenge = 2,
        Wish = 3,
        Expedition = 4,
        Gift = 5,
        Event = 6,
        Visitor = 7,
        Garden = 8,
        Zone = 9,

        /// <summary>Offered, or a moment of the world (ICO_Paw).</summary>
        World = 10,
    }

    /// <summary>One sticker (section 1.14 StickerDefinition): ids, place, rarity, drawable or fixed, the slot's hint.</summary>
    public readonly struct StickerInfo
    {
        /// <summary>The index in the 720 (stable: new stickers are added at the end, a removed id is never reused).</summary>
        public readonly int Index;

        /// <summary>sticker.gl.01; its name text is « {Key}.name ».</summary>
        public readonly string Key;

        /// <summary>The page index (0 to 59) and the slot in the page (0 to 11, 3 columns × 4 rows).</summary>
        public readonly int Page;

        public readonly int Slot;
        public readonly StickerRarity Rarity;

        /// <summary>Can come out of a draw (else it is given by a fixed condition).</summary>
        public readonly bool Drawable;

        public readonly StickerHint Hint;

        public StickerInfo(int index, string key, int page, int slot, StickerRarity rarity, bool drawable, StickerHint hint)
        {
            Index = index;
            Key = key;
            Page = page;
            Slot = slot;
            Rarity = rarity;
            Drawable = drawable;
            Hint = hint;
        }
    }

    /// <summary>One page (section 1.14 AlbumPageDefinition).</summary>
    public readonly struct AlbumPageInfo
    {
        public readonly int Index;

        /// <summary>page.grassland.1; its title text is « {Key}.name ».</summary>
        public readonly string Key;

        public readonly int Album;
        public readonly AlbumKind Kind;

        /// <summary>The index of its first sticker (its 12 stickers follow).</summary>
        public readonly int FirstSticker;

        public AlbumPageInfo(int index, string key, int album, AlbumKind kind, int firstSticker)
        {
            Index = index;
            Key = key;
            Album = album;
            Kind = kind;
            FirstSticker = firstSticker;
        }
    }

    /// <summary>A sticker just gained, handed to the presentation for the reveal (section 1.10).</summary>
    public readonly struct StickerGain
    {
        public readonly int Sticker;

        /// <summary>A new one (else a duplicate turned into album stars).</summary>
        public readonly bool New;

        public readonly int StarsAdded;

        /// <summary>The page reached half or full with this sticker, and the album full (their rewards are granted).</summary>
        public readonly bool PageHalf;

        public readonly bool PageComplete;
        public readonly bool AlbumComplete;

        public StickerGain(int sticker, bool isNew, int starsAdded, bool pageHalf, bool pageComplete, bool albumComplete)
        {
            Sticker = sticker;
            New = isNew;
            StarsAdded = starsAdded;
            PageHalf = pageHalf;
            PageComplete = pageComplete;
            AlbumComplete = albumComplete;
        }
    }

    /// <summary>
    /// The albums as the presentation sees them. Reads never allocate. Gains are queued by the simulation (every source:
    /// activities, wishes, expeditions, the garden, the daily gift, the calendar, zones, visitors…) and the albums screen or
    /// the reveal layer drains them.
    /// </summary>
    public interface IAlbums
    {
        /// <summary>Open: refuge level 12 and day 5 (doc 02 Améliorations, section 6.4).</summary>
        bool IsOpen { get; }

        int AlbumCount { get; }

        AlbumKind KindOfAlbum(int album);

        /// <summary>album.grassland…; its title text is « {key}.name ».</summary>
        string AlbumKey(int album);

        int PageCount { get; }

        AlbumPageInfo PageAt(int page);

        /// <summary>The page is open (its biome, activity, event or season opened it); a closed page is not shown.</summary>
        bool IsPageOpen(int page);

        int StickerCount { get; }

        StickerInfo StickerAt(int sticker);

        bool Owned(int sticker);

        /// <summary>Gained but not looked at yet (the « Nouveau » badge until the page is seen).</summary>
        bool Unseen(int sticker);

        void MarkPageSeen(int page);

        /// <summary>
        /// Its subject exists in this build (its species, zone or activity is in the game and its art was captured): a sticker
        /// not available is never drawn and its slot shows a soft « bientôt » silhouette.
        /// </summary>
        bool Available(int sticker);

        int OwnedOnPage(int page);

        int AlbumStars { get; }

        /// <summary>The price of a sticker of choice (10 stars).</summary>
        int ChoicePrice { get; }

        /// <summary>Spends the stars for a missing sticker of an open page (queued as a gain for its reveal).</summary>
        bool TryChoose(int sticker);

        /// <summary>Gains waiting for their reveal, oldest first.</summary>
        int PendingCount { get; }

        StickerGain PendingAt(int index);

        void ClearPending();

        /// <summary>Raised on the main thread when a sticker is gained (the reveal layer starts if the screen allows it).</summary>
        event System.Action? Gained;
    }
}
