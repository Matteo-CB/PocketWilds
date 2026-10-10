#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    // T44C « Journal du refuge et visiteurs rares » (doc « 01 Succès, titres, cadres, poses et journal », section 6; doc
    // « 01 Rétention », visiteurs rares; doc « Événements originaux », section 5; doc 05 sheets 25 and SUR-06): the contract
    // between the journal and the visitors (simulation) and their presentation (the journal screen, the refuge where a
    // visitor walks in, the moment captures). Frozen for T44C: the simulation implements it, the presentation only uses it.

    /// <summary>What a journal page shows as its picture (doc section 6 « Images »).</summary>
    public enum JournalImageKind : byte
    {
        /// <summary>A 512 × 512 portrait of the animal, captured once (its file named by the page id).</summary>
        AnimalCapture = 0,

        /// <summary>The game camera's view at that instant, interface hidden, 512 × 512, captured once.</summary>
        RefugeCapture = 1,

        /// <summary>An icon or capture that already exists (biome, decoration, sticker, album page, activity, badge…).</summary>
        Thumbnail = 2,

        /// <summary>The expedition's postcard (its id in <see cref="JournalPage.ImageRef"/>).</summary>
        Postcard = 3,

        /// <summary>A photo the player took (photo mode, later).</summary>
        Photo = 4,
    }

    /// <summary>One page of the journal: the moment, its date and the values its text needs (never a sentence put together).</summary>
    public readonly struct JournalPage
    {
        /// <summary>A running number (stable; the capture file is journal_{Id}.jpg).</summary>
        public readonly int Id;

        /// <summary>memory.first_hatch…; its text is « {Key}.text » with the variables below.</summary>
        public readonly string Key;

        /// <summary>Local date as days since 2000-01-01 (the month groups the timeline).</summary>
        public readonly int Day;

        public readonly JournalImageKind Image;

        /// <summary>The image's reference: an animal instance id (captures), a postcard id, a content id value (thumbnails).</summary>
        public readonly uint ImageRef;

        /// <summary>The capture is stored (else the thumbnail of the moment stands in).</summary>
        public readonly bool Captured;

        public JournalPage(int id, string key, int day, JournalImageKind image, uint imageRef, bool captured)
        {
            Id = id;
            Key = key;
            Day = day;
            Image = image;
            ImageRef = imageRef;
            Captured = captured;
        }
    }

    /// <summary>The journal as its screen and the capture layer see it. Reads never allocate.</summary>
    public interface IJournal
    {
        /// <summary>Pages, newest first.</summary>
        int PageCount { get; }

        JournalPage PageAt(int index);

        /// <summary>The page's variables for its text ({name}, {species}, {biome}, {count}…): written into the caller's buffers.</summary>
        int Variables(int pageId, string[] names, string[] values);

        /// <summary>Pages whose capture is still to be made (the capture layer takes them one at a time, at a calm moment).</summary>
        int PendingCaptureCount { get; }

        JournalPage PendingCaptureAt(int index);

        /// <summary>The capture layer stored the page's image (journal_{id}.jpg under the persistent data path).</summary>
        void CaptureStored(int pageId);

        /// <summary>The 50 world surprises already seen (the « Moments » page): their keys (surprise.…).</summary>
        int SeenSurpriseCount { get; }

        string SeenSurpriseAt(int index);

        /// <summary>New pages not looked at yet (the entry's dot).</summary>
        int UnseenCount { get; }

        void MarkSeen();

        event System.Action? PageAdded;
    }

    /// <summary>A rare visitor's visit (doc « Rétention »: one or two a day, 2 hours each).</summary>
    public readonly struct VisitorVisit
    {
        /// <summary>visitor.meadow_rabbit…; its name text is « {Key}.name ».</summary>
        public readonly string Key;

        /// <summary>The species and the seasonal variant the visitor wears.</summary>
        public readonly ContentId Species;

        public readonly ContentId Variant;

        /// <summary>Its arrival and departure (UTC unix ms).</summary>
        public readonly long ArriveUnixMs;

        public readonly long LeaveUnixMs;

        public VisitorVisit(string key, ContentId species, ContentId variant, long arriveUnixMs, long leaveUnixMs)
        {
            Key = key;
            Species = species;
            Variant = variant;
            ArriveUnixMs = arriveUnixMs;
            LeaveUnixMs = leaveUnixMs;
        }
    }

    /// <summary>What touching a visitor gave.</summary>
    public readonly struct VisitorGreeting
    {
        public readonly string VisitorKey;

        /// <summary>The gift (Light minutes, a treat, a seed or Gems as the doc sets) is granted; its text is shown in the pop.</summary>
        public readonly RewardBundle Gift;

        /// <summary>The first touch of this visitor ever: its seasonal sticker.</summary>
        public readonly bool FirstTouch;

        /// <summary>It joins the collection (30 %, guaranteed at the 4th contact): « Nouveau visiteur ! ».</summary>
        public readonly bool Joined;

        public readonly uint JoinedInstanceId;

        public VisitorGreeting(string visitorKey, in RewardBundle gift, bool firstTouch, bool joined, uint joinedInstanceId)
        {
            VisitorKey = visitorKey;
            Gift = gift;
            FirstTouch = firstTouch;
            Joined = joined;
            JoinedInstanceId = joinedInstanceId;
        }
    }

    /// <summary>The rare visitors as the refuge sees them. Reads never allocate; the schedule is drawn per day from a seed.</summary>
    public interface IVisitors
    {
        /// <summary>Open: refuge level 8 and day 3 (doc 02 Améliorations).</summary>
        bool IsOpen { get; }

        /// <summary>A visitor is in the refuge now (between its arrival and departure, not greeted yet).</summary>
        bool Present { get; }

        VisitorVisit Current { get; }

        /// <summary>The next visit today (−1 when none is left), for the presentation's timing.</summary>
        long NextArrivalUnixMs { get; }

        /// <summary>The player touched the visitor: gift, sticker, maybe the collection. False when no visitor is present.</summary>
        bool TryGreet(out VisitorGreeting greeting);

        /// <summary>Raised on the main thread when a visitor arrives or leaves (the refuge walks it in or out).</summary>
        event System.Action? VisitChanged;
    }
}
