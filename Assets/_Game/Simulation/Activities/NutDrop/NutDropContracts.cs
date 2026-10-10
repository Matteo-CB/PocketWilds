#nullable enable

namespace PocketWilds.Simulation
{
    // T40 « Pluie de noisettes » (doc « Mini-jeux & activités », S-ACT-003; doc 01 Contenu, S-ACT-003, lines 1411 to 1497):
    // the contract between the nut drop game (simulation, ticked by the ActivityRunner) and its stage (presentation). The
    // game runs in its own playfield units (about meters): x from -HalfWidth to +HalfWidth, items spawn at SpawnHeight and
    // fall toward the basket's rim at y = 0. The stage maps that field onto its set, moves the basket where the finger says,
    // shows the items where the game puts them and plays the game's events. Frozen for T40: the simulation implements it, the
    // stage only uses it.

    /// <summary>What falls (doc 01 S-ACT-003 « Règles de score » and « Objets »).</summary>
    public enum NutDropItemKind : byte
    {
        None = 0,

        /// <summary>Hazelnut: 10 points.</summary>
        Hazelnut = 1,

        /// <summary>Acorn: 10 points.</summary>
        Acorn = 2,

        /// <summary>Hedge berry: 15 points.</summary>
        Berry = 3,

        /// <summary>Golden hazelnut: 50 points.</summary>
        GoldenNut = 4,

        /// <summary>Pinecone: 0 points, catching it resets the streak.</summary>
        Pinecone = 5,

        /// <summary>Special: the basket 50 % wider for 8 s.</summary>
        WideBasket = 6,

        /// <summary>Special: everything falls × 0.6 for 5 s.</summary>
        Feather = 7,

        /// <summary>Special: for 5 s only golden hazelnuts fall.</summary>
        GoldenShower = 8,
    }

    /// <summary>What happened, for the stage's animations, sounds and effects (doc 01 S-ACT-003 « Sons et effets »).</summary>
    public enum NutDropEventKind : byte
    {
        None = 0,

        /// <summary>An item appears at the top (Item: its slot, Kind, X).</summary>
        Spawned = 1,

        /// <summary>An item lands in the basket (Points with the multiplier, Streak after the catch).</summary>
        Caught = 2,

        /// <summary>An item reaches the ground beside the basket (no loss, the streak goes on).</summary>
        Missed = 3,

        /// <summary>A pinecone caught: the streak goes back to 0 (a soft felted « bonk »).</summary>
        Pinecone = 4,

        /// <summary>The multiplier rose by 0.1 (every 5 catches in a row, up to × 3.0); Multiplier in tenths.</summary>
        MultiplierUp = 5,

        /// <summary>A special caught: its effect starts (Kind, Seconds).</summary>
        SpecialStarted = 6,

        /// <summary>A special's effect ended (Kind).</summary>
        SpecialEnded = 7,

        /// <summary>The game ended (time up).</summary>
        Finished = 8,
    }

    /// <summary>One event: its kind, the item slot (−1: none), the item kind, x in the playfield, points, streak, tenths, seconds.</summary>
    public readonly struct NutDropEvent
    {
        public readonly NutDropEventKind Kind;
        public readonly int Item;
        public readonly NutDropItemKind ItemKind;
        public readonly float X;
        public readonly int Points;
        public readonly int Streak;

        /// <summary>The multiplier in tenths (10 = × 1.0, 30 = × 3.0).</summary>
        public readonly int MultiplierTenths;

        public readonly float Seconds;

        public NutDropEvent(NutDropEventKind kind, int item, NutDropItemKind itemKind, float x, int points, int streak,
            int multiplierTenths, float seconds)
        {
            Kind = kind;
            Item = item;
            ItemKind = itemKind;
            X = x;
            Points = points;
            Streak = streak;
            MultiplierTenths = multiplierTenths;
            Seconds = seconds;
        }
    }

    /// <summary>
    /// The nut drop game as its stage sees it. Items live in a fixed pool of slots (<see cref="ItemCapacity"/>); the stage
    /// reads every active slot each frame to place its pooled meshes. Nothing here allocates.
    /// </summary>
    public interface INutDropGame
    {
        /// <summary>Half the playfield's width (the basket's center stays within ± HalfWidth − half the basket).</summary>
        float HalfWidth { get; }

        /// <summary>Height items appear at (they fall to the rim at y = 0, then to the ground at <see cref="GroundY"/>).</summary>
        float SpawnHeight { get; }

        float GroundY { get; }

        // ---------------- the basket ----------------

        /// <summary>Where the finger wants the basket (playfield x); the game moves it there at its own speed, clamped.</summary>
        void SetBasketTarget(float x);

        float BasketX { get; }

        /// <summary>The basket's catch width now (doc 01: 1.20 at d1, 1.00 at d5, 0.85 at d10; × 1.5 while wide).</summary>
        float BasketWidth { get; }

        /// <summary>The basket's speed this frame (signed, units per second): the stage's hop and lean.</summary>
        float BasketVelocity { get; }

        // ---------------- the items ----------------

        int ItemCapacity { get; }

        bool ItemActive(int slot);

        NutDropItemKind ItemKind(int slot);

        float ItemX(int slot);

        float ItemY(int slot);

        // ---------------- the state ----------------

        /// <summary>Catches in a row without a pinecone (a missed item does not break it).</summary>
        int Streak { get; }

        int BestStreak { get; }

        /// <summary>The multiplier in tenths: 10 + 1 per 5 in the streak, at most 30.</summary>
        int MultiplierTenths { get; }

        /// <summary>The special in effect (None) and its seconds left.</summary>
        NutDropItemKind ActiveSpecial { get; }

        float SpecialLeft { get; }

        /// <summary>Challenge 8: the side wind (units per second the items drift; 0: none).</summary>
        float Wind { get; }

        float Elapsed { get; }

        float Seconds { get; }

        // ---------------- events (drained by the stage each frame) ----------------

        int EventCount { get; }

        NutDropEvent Event(int index);

        void ClearEvents();

        // ---------------- the game's numbers (wishes, journal, rewards, analytics) ----------------

        int Caught { get; }

        /// <summary>Hazelnuts caught (golden ones included): 1 Hazelnut ingredient per 10, 3 at most.</summary>
        int HazelnutsCaught { get; }

        /// <summary>Berries caught: 1 Strawberry per 10, 2 at most (the Pâtisserie open).</summary>
        int BerriesCaught { get; }

        int PineconesCaught { get; }
    }
}
