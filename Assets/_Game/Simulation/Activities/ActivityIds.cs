#nullable enable

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The 18 activities (doc « Mini-jeux & activités », S-ACT-001 to 018; doc 01 Contenu, section 12). Stable values: the
    /// value is the doc's number (analytics, saves). <see cref="Test"/> exists only for the framework's tests.
    /// </summary>
    public enum ActivityId : ushort
    {
        None = 0,
        Grooming = 1,
        HideAndSeek = 2,
        NutRain = 3,
        PawMemory = 4,
        Expeditions = 5,
        Garden = 6,
        TreasureFishing = 7,
        LilyHop = 8,
        Bakery = 9,
        PhotoSafari = 10,
        CuddleMatch = 11,
        Slide = 12,
        Constellations = 13,
        FireflyRhythm = 14,
        Bubbles = 15,
        TilePuzzle = 16,
        BurrowMaze = 17,
        GrandContest = 18,

        /// <summary>The framework's own test activity (never shown to players).</summary>
        Test = 900,
    }

    /// <summary>How a game is played (doc Mini-jeux, section 2). Stable values.</summary>
    public enum ActivityMode : byte
    {
        /// <summary>The free mode with its fixed star thresholds and the adaptive difficulty; a ticket for the rewards.</summary>
        Free = 0,

        /// <summary>One of the 10 fixed challenges (fixed parameters, no adaptation); a ticket for the rewards.</summary>
        Challenge = 1,

        /// <summary>Practice: free and unlimited, no reward (the fun is never blocked).</summary>
        Practice = 2,
    }

    /// <summary>Where activity tickets come from (analytics TicketsGain, doc 09). Stable values.</summary>
    public enum TicketSource : byte
    {
        Refill = 0,
        Gems = 1,
        Ad = 2,
        DailyGift = 3,
        Calendar = 4,
        WeeklyJournal = 5,
        WelcomeGift = 6,
        ReturnGift = 7,
        Event = 8,
        Circle = 9,
        Album = 10,
        Visitor = 11,
        Debug = 12,
        Wish = 13,

        /// <summary>T42: the tickets an expedition brings back (8 h and 12 h; the Frosted Pines' specialty).</summary>
        Expedition = 14,
    }
}
