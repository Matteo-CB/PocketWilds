#nullable enable

namespace PocketWilds.Simulation
{
    // T38 « Toilettage » (doc « Mini-jeux & activités », S-ACT-001; doc 01 Contenu, S-ACT-001 and section 12.0): the contract
    // between the grooming game (simulation, ticked by the ActivityRunner) and its stage (presentation). The stage measures
    // the zones on the animal's model, turns the finger into passes, taps and choices, and reads back the game's state and
    // its events to show them. Frozen for T38: the simulation implements it, the stage only uses it.

    /// <summary>Where a grooming game stands.</summary>
    public enum GroomingPhase : byte
    {
        /// <summary>Brushing: dirty zones, debris, burrs, soap bubbles, the golden brush.</summary>
        Brushing = 0,

        /// <summary>Everything is clean, or 40 s passed: three bows wait (pink, sky blue, cream).</summary>
        Bow = 1,

        /// <summary>The bow is placed, or the time is up.</summary>
        Done = 2,
    }

    /// <summary>The bow colors, in the order the picker shows them (doc 01: « rose, bleu ciel, crème »). Stable values.</summary>
    public enum GroomingBow : byte
    {
        Pink = 0,
        SkyBlue = 1,
        Cream = 2,
    }

    /// <summary>What happened in a tick or a touch, for the stage's sounds and effects (doc 01 S-ACT-001 « Sons et effets »).</summary>
    public enum GroomingEventKind : byte
    {
        None = 0,

        /// <summary>A pass over a dirty zone that is not clean yet (soft foam, the brushing sound keeps going).</summary>
        ZonePass = 1,

        /// <summary>A zone became clean: +10 × the streak multiplier (glass note rising a semitone per zone in a row, glints).</summary>
        ZoneCleaned = 2,

        /// <summary>A pass over a zone already clean: the animal giggles, the streak goes back to × 1.0, no point lost.</summary>
        Rebrush = 3,

        /// <summary>A debris taken off with one tap: +15 (leaf pop; it flies off spinning).</summary>
        DebrisRemoved = 4,

        /// <summary>A burr's first tap (it loosens).</summary>
        BurrLoosened = 5,

        /// <summary>A burr's second tap: +25 (double pop).</summary>
        BurrRemoved = 6,

        /// <summary>A soap bubble appears on the animal (at 15 s and 30 s).</summary>
        BubbleSpawned = 7,

        /// <summary>A soap bubble tapped: it cleans the 3 nearest dirty zones, each counted with the streak.</summary>
        BubblePopped = 8,

        /// <summary>The golden brush appears (at 20 s with the streak at × 1.5 or more).</summary>
        GoldenBrushOffered = 9,

        /// <summary>The golden brush taken: for 5 s each pass counts double.</summary>
        GoldenBrushStart = 10,

        GoldenBrushEnd = 11,

        /// <summary>The bow phase starts (everything clean, or 40 s).</summary>
        BowPhase = 12,

        /// <summary>A bow placed: +50 (two music box notes), the game ends with its time bonus.</summary>
        BowPlaced = 13,

        /// <summary>The last dirty zone is clean.</summary>
        AllClean = 14,

        /// <summary>The game ended (bow placed or time up): hearts, a content sigh.</summary>
        Finished = 15,
    }

    /// <summary>One event of the game: its kind, the zone, debris or bubble it concerns, the points it gave.</summary>
    public readonly struct GroomingEvent
    {
        public readonly GroomingEventKind Kind;

        /// <summary>The zone (passes, cleaning, rebrush), the debris (debris, burrs), the bubble, or the bow color.</summary>
        public readonly int Index;

        public readonly int Points;

        /// <summary>Zones cleaned in a row after this event (the glass note's step; 0 after a rebrush).</summary>
        public readonly int Streak;

        public GroomingEvent(GroomingEventKind kind, int index, int points, int streak)
        {
            Kind = kind;
            Index = index;
            Points = points;
            Streak = streak;
        }
    }

    /// <summary>
    /// The grooming game as its stage sees it. The stage gives the zones measured on the animal (<see cref="SetBody"/>)
    /// before the runner starts the game, then calls the inputs and drains the events each frame. Nothing here allocates
    /// after <see cref="SetBody"/>.
    /// </summary>
    public interface IGroomingGame
    {
        /// <summary>
        /// The zones of the animal's coat, measured on its model (doc 01: SmallQuadruped 40, MediumQuadruped 48,
        /// HeavyQuadruped 56, Bird 36, Hopper 30, SwimmerLow 34, ClimberPrimate 44), as points in the animal's own space
        /// (any unit): the game uses them for distances only (the 3 zones nearest a bubble, debris spread over the coat).
        /// The arrays are copied; at most 64 zones.
        /// </summary>
        void SetBody(int zoneCount, float[] x, float[] y, float[] z);

        int ZoneCount { get; }

        /// <summary>The zone is dirty this game (the dust shows on it).</summary>
        bool IsDirty(int zone);

        /// <summary>Dust left on a zone, 1 (dirty) to 0 (clean): the passes left over the passes needed.</summary>
        float Dust(int zone);

        /// <summary>Debris and burrs of this game (both counted here), each lying on a zone.</summary>
        int DebrisCount { get; }

        bool DebrisActive(int debris);

        int DebrisZone(int debris);

        bool DebrisIsBurr(int debris);

        /// <summary>Taps already given to a burr (0 or 1).</summary>
        int DebrisTaps(int debris);

        /// <summary>The two soap bubbles of a game (15 s and 30 s).</summary>
        int BubbleCount { get; }

        bool BubbleActive(int bubble);

        int BubbleZone(int bubble);

        /// <summary>The golden brush waits to be taken, lying on this zone (−1: none).</summary>
        int GoldenBrushZone { get; }

        bool GoldenBrushActive { get; }

        /// <summary>Seconds left of the golden brush.</summary>
        float GoldenBrushLeft { get; }

        GroomingPhase Phase { get; }

        /// <summary>The streak multiplier, × 1.0 to × 2.0 (+0.1 per zone cleaned in a row).</summary>
        float StreakMultiplier { get; }

        /// <summary>
        /// How far the animal moves this game, as a share of the screen's width (doc 01 « Mouvement de l'animal »: 0 % to 12 %,
        /// 15 % in challenge 5). The stage animates it (a calm sway), always without entering anything.
        /// </summary>
        float Movement { get; }

        /// <summary>Seconds played (pauses excluded) and the game's length (45 s, 30 s in challenge 4).</summary>
        float Elapsed { get; }

        float Seconds { get; }

        // ---------------- inputs (the stage's touches) ----------------

        /// <summary>One pass of the brush over a zone (the stage counts a pass when the finger crosses the zone).</summary>
        void Brush(int zone);

        void TapDebris(int debris);

        void TapBubble(int bubble);

        void TakeGoldenBrush();

        void PlaceBow(GroomingBow bow);

        // ---------------- events (drained by the stage each frame) ----------------

        int EventCount { get; }

        GroomingEvent Event(int index);

        void ClearEvents();

        // ---------------- the game's numbers (wishes, journal, analytics) ----------------

        int ZonesCleaned { get; }

        /// <summary>Debris and burrs taken off (WISH-021 « Enlève {n} feuilles et brindilles »).</summary>
        int DebrisRemoved { get; }

        int Rebrushes { get; }

        bool BowPlaced { get; }

        GroomingBow Bow { get; }
    }
}
