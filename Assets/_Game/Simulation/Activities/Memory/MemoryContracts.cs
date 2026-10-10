#nullable enable

namespace PocketWilds.Simulation
{
    // T41 « Memory des empreintes » (doc « Mini-jeux & activités », S-ACT-004; doc 01 Contenu, S-ACT-004, lines 1498 to
    // 1560): the contract between the memory game (simulation, ticked by the ActivityRunner) and its stage (an interface
    // screen). The game deals a grid of cards face down; each card belongs to a pair; the stage shows each card's face (a
    // portrait of one of the player's animal variants, or that variant's pawprint), turns touches into flips and plays the
    // game's events. Frozen for T41: the simulation implements it, the stage only uses it.

    /// <summary>What a card's face shows.</summary>
    public enum MemoryFace : byte
    {
        /// <summary>The variant's portrait (its capture thumbnail).</summary>
        Portrait = 0,

        /// <summary>The variant's pawprint: the species' paw shape in the variant's coat colour (« empreinte et portrait »).</summary>
        Pawprint = 1,
    }

    /// <summary>What happened, for the stage's animations, sounds and effects (doc 01 S-ACT-004 « Sons et effets »).</summary>
    public enum MemoryEventKind : byte
    {
        None = 0,

        /// <summary>The preview: every card shows its face for <see cref="MemoryEvent.Seconds"/>, then turns back.</summary>
        PreviewStarted = 1,

        PreviewEnded = 2,

        /// <summary>A card turned face up (Card).</summary>
        Flipped = 3,

        /// <summary>Two face-up cards make a pair: they stay (Card, Other, Points: +100, or 200 for the double pair).</summary>
        Matched = 4,

        /// <summary>Two face-up cards do not match: they turn back after 0.8 s (Card, Other; Mistake true if it counts).</summary>
        Mismatched = 5,

        /// <summary>The two cards of a mismatch turn face down again (Card, Other).</summary>
        FlippedBack = 6,

        /// <summary>The glow card was turned: the card of its pair glows for 1 s (Card: the glow card, Other: its pair).</summary>
        Glow = 7,

        /// <summary>Challenge 6: two face-down cards swapped places (Card, Other).</summary>
        Swapped = 8,

        /// <summary>Every pair found: the end bonus (Points) and the game ends.</summary>
        AllMatched = 9,

        /// <summary>The game ended (all matched or time up).</summary>
        Finished = 10,
    }

    /// <summary>One event: its kind, the card(s) it concerns (−1: none), points, whether it counts as a mistake, seconds.</summary>
    public readonly struct MemoryEvent
    {
        public readonly MemoryEventKind Kind;
        public readonly int Card;
        public readonly int Other;
        public readonly int Points;
        public readonly bool Mistake;
        public readonly float Seconds;

        public MemoryEvent(MemoryEventKind kind, int card, int other, int points, bool mistake, float seconds)
        {
            Kind = kind;
            Card = card;
            Other = other;
            Points = points;
            Mistake = mistake;
            Seconds = seconds;
        }
    }

    /// <summary>
    /// The memory game as its stage sees it. Before the runner begins, the stage gives the variants the cards may show
    /// (<see cref="SetVariants"/>: the player's own first); after Begin it reads the grid, then calls <see cref="Flip"/> and
    /// drains the events each frame. Nothing here allocates after <see cref="SetVariants"/>.
    /// </summary>
    public interface IMemoryGame
    {
        /// <summary>
        /// The variants the cards may show, best first (the player's own animals, then the ones the codex knows, then any),
        /// as ContentId values with each variant's species (at most 64, copied). The game deals distinct variants, one per
        /// pair; with fewer variants than pairs it repeats the list.
        /// </summary>
        void SetVariants(int count, uint[] variants, uint[] species);

        /// <summary>Columns and rows of the grid (4 × 3, 4 × 4 or 5 × 4); cards are numbered row by row from the top left.</summary>
        int Columns { get; }

        int Rows { get; }

        int CardCount { get; }

        int PairCount { get; }

        /// <summary>The pair a card belongs to (0 to PairCount − 1).</summary>
        int PairOf(int card);

        /// <summary>The variant (ContentId value) and the species a card shows.</summary>
        uint VariantOf(int card);

        uint SpeciesOf(int card);

        MemoryFace FaceOf(int card);

        bool FaceUp(int card);

        bool Matched(int card);

        /// <summary>The glow card (gold edge, −1: none) and the double pair's cards (−1: none).</summary>
        int GlowCard { get; }

        bool IsDouble(int card);

        /// <summary>The preview is showing every face (touches are ignored meanwhile).</summary>
        bool Previewing { get; }

        /// <summary>A mismatch is waiting to turn back (touches on other cards are ignored for those 0.8 s).</summary>
        bool Busy { get; }

        int PairsFound { get; }

        int Mistakes { get; }

        /// <summary>Flips so far (two per try): WISH-059 « finis en {n} coups ou moins » counts tries.</summary>
        int Moves { get; }

        float Elapsed { get; }

        float Seconds { get; }

        // ---------------- input ----------------

        /// <summary>A touch on a card: it turns face up when it may (face down, not matched, not previewing, not busy).</summary>
        void Flip(int card);

        // ---------------- events ----------------

        int EventCount { get; }

        MemoryEvent Event(int index);

        void ClearEvents();
    }
}
