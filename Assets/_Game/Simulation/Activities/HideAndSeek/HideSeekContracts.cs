#nullable enable

namespace PocketWilds.Simulation
{
    // T39 « Cache-cache » (doc « Mini-jeux & activités », S-ACT-002; doc 01 Contenu, S-ACT-002 and section 12.0): the contract
    // between the hide-and-seek game (simulation, ticked by the ActivityRunner) and its stage (presentation). The stage
    // measures the hiding spots of its set, places the player's animals in the spots the game picks (part of the silhouette
    // visible), turns taps into spot touches and plays the game's events. Frozen for T39: the simulation implements it, the
    // stage only uses it.

    /// <summary>What happened, for the stage's animations, sounds and effects (doc 01 S-ACT-002 « Sons et effets »).</summary>
    public enum HideSeekEventKind : byte
    {
        None = 0,

        /// <summary>An animal found before its hint: +100 and +2 per second left (« coucou », a heart, a ring on the ground).</summary>
        Found = 1,

        /// <summary>An animal found after its hint (or after the magnifier or the whistle): +50 and +1 per second left.</summary>
        FoundAfterHint = 2,

        /// <summary>A touch on an empty spot or a decoy: a few leaves fly, no point lost.</summary>
        Empty = 3,

        /// <summary>The third empty touch within 2 s: touches are ignored for 1 s (Index: none).</summary>
        TouchesPaused = 4,

        /// <summary>An animal gives its hint (an ear twitches, the grass of its spot ripples).</summary>
        Hint = 5,

        /// <summary>A decoy spot rustles in the wind (empty).</summary>
        DecoyRustle = 6,

        /// <summary>The magnifier: a soft ring around the nearest occupied spot for 2 s.</summary>
        Magnifier = 7,

        /// <summary>The whistle: every animal still hidden wiggles once.</summary>
        Whistle = 8,

        /// <summary>Challenge 9: an animal not found yet slips to another spot (Index: the new spot, Animal: which one).</summary>
        Moved = 9,

        /// <summary>Challenge 6: a mistake counted (the 4th ends the game).</summary>
        Mistake = 10,

        /// <summary>Every animal found: +5 per second left, the game ends.</summary>
        AllFound = 11,

        /// <summary>The game ended (all found, time up, or challenge 6's 4th mistake).</summary>
        Finished = 12,
    }

    /// <summary>One event: its kind, the spot it concerns, the hidden animal (its slot, 0 to HiddenCount − 1), the points.</summary>
    public readonly struct HideSeekEvent
    {
        public readonly HideSeekEventKind Kind;
        public readonly int Spot;
        public readonly int Animal;
        public readonly int Points;

        public HideSeekEvent(HideSeekEventKind kind, int spot, int animal, int points)
        {
            Kind = kind;
            Spot = spot;
            Animal = animal;
            Points = points;
        }
    }

    /// <summary>
    /// The hide-and-seek game as its stage sees it. The stage gives the spots of its set (<see cref="SetSpots"/>) before the
    /// runner starts the game; after Begin it reads which spot each hidden animal takes and which spots are decoys, places
    /// the animals, then calls the inputs and drains the events each frame. Nothing here allocates after <see cref="SetSpots"/>.
    /// </summary>
    public interface IHideSeekGame
    {
        /// <summary>
        /// The hiding spots of the set (points on the ground beside a decor, at most 32), in the set's own space (meters):
        /// the game uses them for distances (spread, the magnifier's nearest spot, challenge 9's moves). Copied.
        /// </summary>
        void SetSpots(int count, float[] x, float[] y, float[] z);

        int SpotCount { get; }

        /// <summary>Animals hidden this game (doc 01: 4 at d1, 6 at d5, 8 at d10; the challenge's count).</summary>
        int HiddenCount { get; }

        /// <summary>The spot an animal hides in (−1 once found).</summary>
        int AnimalSpot(int animal);

        bool AnimalFound(int animal);

        /// <summary>The animal gave its hint (it stays hinting until found).</summary>
        bool AnimalHinting(int animal);

        /// <summary>
        /// A timid animal waits 2 s more before its hint (doc 01, doc 02 Renouveau temperaments): the stage tells which
        /// hidden animals are timid, right after the runner's Begin.
        /// </summary>
        void SetTimid(int animal, bool timid);

        /// <summary>A decoy: an empty spot that moves in the wind (0 at d1, 2 at d5, 4 at d10).</summary>
        bool IsDecoy(int spot);

        /// <summary>Share of the silhouette left visible this game (0.30 at d1, 0.15 at d5, 0.08 at d10; the challenge's).</summary>
        float Visibility { get; }

        /// <summary>Seconds before an animal gives its hint (6, 10, 15 s); hints off in challenges 2 and 10.</summary>
        float HintDelay { get; }

        bool HintsEnabled { get; }

        /// <summary>The magnifier and the whistle exist this game (not in challenge 2, « ni loupe ni sifflet »).</summary>
        bool ItemsEnabled { get; }

        bool MagnifierAvailable { get; }

        bool WhistleAvailable { get; }

        /// <summary>The spot the magnifier rings (−1: none) and its seconds left.</summary>
        int MagnifierSpot { get; }

        float MagnifierLeft { get; }

        /// <summary>Touches are ignored right now (after 3 empty touches within 2 s, for 1 s).</summary>
        bool TouchesPaused { get; }

        /// <summary>Challenge 6: mistakes so far and the count that ends the game (0: no limit).</summary>
        int Mistakes { get; }

        int MistakeLimit { get; }

        /// <summary>Challenge 7: night, only a circle of this share of the screen around the finger is lit (0: day).</summary>
        float NightSpotlight { get; }

        /// <summary>Challenge 8: the hidden animals are all of one species (the stage picks it).</summary>
        bool SameSpecies { get; }

        /// <summary>Challenge 9: an animal not found yet changes spot every this many seconds (0: never).</summary>
        float MoveEvery { get; }

        float Elapsed { get; }

        float Seconds { get; }

        // ---------------- inputs (the stage's touches) ----------------

        /// <summary>A touch on a spot (the stage maps the finger to the spot whose decor it touched).</summary>
        void TapSpot(int spot);

        /// <summary>The magnifier, toward the spot nearest this point (the finger's last place, or the screen's center).</summary>
        void UseMagnifier(float x, float y, float z);

        void UseWhistle();

        // ---------------- events (drained by the stage each frame) ----------------

        int EventCount { get; }

        HideSeekEvent Event(int index);

        void ClearEvents();

        // ---------------- the game's numbers (wishes, journal, analytics) ----------------

        int FoundCount { get; }

        int FoundBeforeHint { get; }

        int EmptyTouches { get; }

        bool AllFound { get; }
    }
}
