#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The hide-and-seek game (doc 01 Contenu, S-ACT-002; doc « Mini-jeux &amp; activités », S-ACT-002), ticked by the
    /// <see cref="ActivityRunner"/> and played by its stage through <see cref="IHideSeekGame"/>.
    /// <list type="bullet">
    /// <item>The framed zone's hiding spots come from the stage, best hider first (<see cref="SetSpots"/>, up to 32; without
    /// them a synthetic meadow of 12 spots on a 6 × 4 m grid, so tests and headless runs work). A game hides N animals and
    /// marks D decoys (N + D clamped to the spots; challenge 9 keeps one spot free to move to), all from the game's seed: the
    /// hidden spots among the best ones (N + 2 at 8 % visible, the whole zone from 20 %), spread farthest first (jittered)
    /// and never two side by side when the zone allows it (closer than 1.25 × the zone's mean nearest-neighbor distance),
    /// the window widening only when spacing forces it; the previous game's spots weigh half and its exact layout is not
    /// repeated when the zone allows another as good (<see cref="LastLayout"/>); the animals take the spots in a seeded
    /// order; then the decoys among the free spots, away from the animals and from each other.</item>
    /// <item>A touch on an occupied spot finds its animal (« coucou »): +100 and +2 per whole second left, or +50 and +1 after
    /// its hint, the magnifier or the whistle. An empty spot or a decoy: a few leaves fly, nothing lost, the streak starts
    /// again (challenge 6: a mistake, the 4th ends the game); the 3rd empty touch within 2 s pauses the touches for 1 s. A
    /// touch on a spot whose animal was already found does nothing (the animal stands there, happy). Every animal found: +5
    /// per whole second left, then the end.</item>
    /// <item>Hints: each animal not found hints at the hint delay (+2 s if Timid) plus its own stagger (0 to 1.5 s, evenly
    /// spread in a seeded order), so hints never pop together; it stays hinting until found. No hint in challenges 2 and
    /// 10; no item in challenge 2.</item>
    /// <item>Decoys rustle on their own every 3.5 to 5.5 s each, never within 0.6 s of a hint's start nor 0.35 s of another
    /// decoy.</item>
    /// <item>Items, once each per game when the challenge allows them: the magnifier rings the occupied spot nearest to a
    /// point for 2 s, the whistle makes every animal still hidden wiggle (one <see cref="HideSeekEventKind.Whistle"/> event
    /// per animal, in animal order: the stage plays the flute once and each wiggle). Their animals count as found after
    /// their hint.</item>
    /// <item>Challenge 9: every 15 s one animal not found yet (never the one the magnifier rings) slips to the free spot
    /// farthest from the last touch among the best ones (any free spot when none of those is), never a decoy, an occupied
    /// spot nor a spot whose animal was found; a hinting animal keeps hinting at its new spot.</item>
    /// <item>The game ends when every animal is found, at its time (60 s; 40 s and 45 s in challenges 4 and 10; × 1.25 with
    /// « Activités plus lentes », and so are the magnifier's ring and challenge 9's moves; the hints keep their seconds, so
    /// the slower mode only gives time) or at challenge 6's 4th mistake.</item>
    /// </list>
    /// Events go to a fixed ring the stage drains each frame. Nothing allocates after construction: <see cref="Tick"/>, the
    /// inputs and the queries only read and write preallocated arrays.
    /// </summary>
    public sealed class HideSeekController : IActivityController, IHideSeekGame
    {
        public const int MaxSpots = 32;

        /// <summary>The synthetic meadow's spots: 4 columns 2 m apart × 3 rows 2 m apart (a 6 × 4 m grid).</summary>
        public const int DefaultSpotCount = 12;

        public const int EventCapacity = 128;

        /// <summary>Two spots closer than this × the set's mean nearest-neighbor distance stand side by side.</summary>
        public const float SideBySideFactor = 1.25f;

        private const uint LayoutSalt = 0x48494445u;
        private const uint TimingSalt = 0x5345454Bu;
        private const uint MoveSalt = 0x4D4F5645u;
        private const int MaxStepsPerTick = 16;

        /// <summary>Steps of each layout search for spots none side by side (Begin only; well under a millisecond on the sets).</summary>
        private const int SearchBudget = 1500;

        /// <summary>Steps of all the layout's searches together (a crowded set of many spots stops there).</summary>
        private const int LayoutBudget = 6000;

        /// <summary>
        /// The measured spots come best first (the hide-spot pipeline's order: index 0 reaches the lowest visible share for
        /// every species): the animals take the first N + 2 spots, apart, the window widening only when spacing forces it.
        /// </summary>
        public const int BestWindowExtra = 2;

        /// <summary>
        /// The best window follows the visibility: N + 2 spots at 8 % visible (only the best hiders leave that little), the
        /// whole zone from 20 % (every measured spot reaches the share), a straight line between: replays use the whole zone
        /// whenever the animals may show more.
        /// </summary>
        public const float NarrowWindowVisibility = 0.08f;

        public const float WholeZoneVisibility = 0.20f;

        /// <summary>A spot of the previous layout weighs this much in the draw (rotation over replays, never a ban).</summary>
        public const float RecentSpotWeight = 0.5f;

        /// <summary>Draws tried for a layout other than the previous one, as good (no more side-by-side pairs).</summary>
        private const int LayoutAttempts = 6;

        private static ActivityRules? _defaultRules;

        // ---------------- the set ----------------
        private readonly float[] _x = new float[MaxSpots];
        private readonly float[] _y = new float[MaxSpots];
        private readonly float[] _z = new float[MaxSpots];
        private int _spots;
        private float _sideBySide2;
        private bool _defaultSpots;

        // ---------------- spots of the game ----------------
        private readonly sbyte[] _animalAt = new sbyte[MaxSpots];
        private readonly bool[] _decoy = new bool[MaxSpots];
        private readonly bool[] _foundAt = new bool[MaxSpots];
        private readonly int[] _decoySpot = new int[MaxSpots];
        private readonly double[] _rustleAt = new double[MaxSpots];
        private readonly double[] _rustlePeriod = new double[MaxSpots];
        private int _decoys;
        private double _lastRustle;

        // ---------------- hidden animals ----------------
        private readonly int[] _animalSpot = new int[MaxSpots];
        private readonly bool[] _found = new bool[MaxSpots];
        private readonly bool[] _hinting = new bool[MaxSpots];
        private readonly bool[] _assisted = new bool[MaxSpots];
        private readonly bool[] _timid = new bool[MaxSpots];
        private readonly double[] _stagger = new double[MaxSpots];
        private readonly double[] _hintAt = new double[MaxSpots];
        private int _hidden;

        // ---------------- layout scratch ----------------
        private readonly float[] _scratch = new float[MaxSpots];
        private readonly int[] _order = new int[MaxSpots];
        private readonly int[] _chosen = new int[MaxSpots];
        private readonly int[] _candidate = new int[MaxSpots * MaxSpots];
        private readonly float[] _candidateScore = new float[MaxSpots * MaxSpots];
        private int _nodes;
        private int _nodesTotal;

        /// <summary>The spots the layout searches now (the first ones: measured spots come best first).</summary>
        private int _window = MaxSpots;

        /// <summary>The best spots of this game: the window the animals took (all the spots for the synthetic meadow).</summary>
        private int _bestWindow = MaxSpots;

        private uint _lastLayout;
        private uint _layoutMask;
        private readonly int[] _firstDraw = new int[MaxSpots];

        // ---------------- empty touches ----------------
        private readonly double[] _emptyAt = new double[HideSeekRules.EmptyBurstTouches];
        private int _emptyRing;
        private int _emptyInRing;
        private double _pausedUntil;

        // ---------------- events ----------------
        private readonly HideSeekEvent[] _events = new HideSeekEvent[EventCapacity];
        private int _eventStart;
        private int _eventCount;

        // ---------------- the game ----------------
        private bool _begun;
        private bool _done;
        private bool _paused;
        private double _elapsed;
        private float _seconds = HideSeekRules.BaseSeconds;
        private float _visibility;
        private float _hintDelay;
        private bool _hintsEnabled = true;
        private bool _itemsEnabled = true;
        private int _mistakeLimit;
        private float _night;
        private bool _sameSpecies;
        private float _moveEvery;
        private double _nextMove = double.PositiveInfinity;
        private DeterministicRandom _moveRng;
        private float _magnifierDuration = HideSeekRules.MagnifierSeconds;
        private bool _magnifierUsed;
        private bool _whistleUsed;
        private int _magnifierSpot = -1;
        private double _magnifierLeft;
        private int _lastTap = -1;
        private int _score;
        private int _streak;
        private int _bestStreak;
        private int _foundCount;
        private int _foundBefore;
        private int _emptyTouches;
        private int _mistakes;
        private int _moves;
        private int _timeBonus;

        public HideSeekController()
        {
            SetDefaultSpots();
        }

        // ================================================================== the set

        /// <summary>
        /// The framed zone's hiding spots (meters, at most 32, copied), in the hide-spot pipeline's order: index 0 hides best
        /// (the lowest visible share for every species), the last ones least well. The animals take the best ones; ignored
        /// during a game (<see cref="EndGame"/> after a quit); none: the synthetic meadow.
        /// </summary>
        public void SetSpots(int count, float[] x, float[] y, float[] z)
        {
            if (Running)
                return;
            int n = Math.Min(Math.Min(count, MaxSpots), Math.Min(x.Length, Math.Min(y.Length, z.Length)));
            if (n <= 0)
            {
                SetDefaultSpots();
                return;
            }
            // The same zone again (the stage gives its spots before every game): the last layout stays remembered.
            bool same = !_defaultSpots && n == _spots;
            for (int i = 0; i < n && same; i++)
                same = _x[i] == x[i] && _y[i] == y[i] && _z[i] == z[i];
            if (!same)
                _lastLayout = 0;
            Array.Copy(x, _x, n);
            Array.Copy(y, _y, n);
            Array.Copy(z, _z, n);
            _spots = n;
            _defaultSpots = false;
            MeasureSideBySide();
        }

        /// <summary>
        /// The synthetic meadow: 12 spots on a 6 × 4 m grid (x −3, −1, 1, 3; z −2, 0, 2; y 0). For tests, headless runs and a
        /// set whose spots are not measured. Ignored during a game.
        /// </summary>
        public void SetDefaultSpots()
        {
            if (Running)
                return;
            if (!_defaultSpots)
                _lastLayout = 0;
            for (int i = 0; i < DefaultSpotCount; i++)
            {
                _x[i] = -3f + 2f * (i % 4);
                _y[i] = 0f;
                _z[i] = -2f + 2f * (i / 4);
            }
            _spots = DefaultSpotCount;
            _defaultSpots = true;
            MeasureSideBySide();
        }

        /// <summary>The spots are the synthetic meadow's (no <see cref="SetSpots"/> yet, or an empty one).</summary>
        public bool UsesDefaultSpots => _defaultSpots;

        /// <summary>
        /// The occupied spots of the previous game in this zone (bit s: spot s; 0: none known). The next <see cref="Begin"/>
        /// prefers other spots (a soft weight) and never repeats this exact layout when the zone allows another one as good.
        /// The game remembers its own layout here after each Begin and keeps it while the same spots come back; the stage
        /// sets it after <see cref="SetSpots"/> when it returns to a zone after playing elsewhere (it keeps
        /// <see cref="LayoutMask"/> per zone).
        /// </summary>
        public uint LastLayout
        {
            get => _lastLayout;
            set => _lastLayout = value;
        }

        /// <summary>This game's occupied spots at its start (bit s: spot s), for the stage to keep per zone.</summary>
        public uint LayoutMask => _layoutMask;

        public float SpotX(int spot) => (uint)spot < (uint)_spots ? _x[spot] : 0f;

        public float SpotY(int spot) => (uint)spot < (uint)_spots ? _y[spot] : 0f;

        public float SpotZ(int spot) => (uint)spot < (uint)_spots ? _z[spot] : 0f;

        /// <summary>Two spots stand side by side (closer than <see cref="SideBySideFactor"/> × the mean nearest-neighbor distance).</summary>
        public bool SideBySide(int a, int b) =>
            (uint)a < (uint)_spots && (uint)b < (uint)_spots && a != b && Distance2(a, b) < _sideBySide2;

        /// <summary>
        /// Ends a game left unfinished (the runner's Quit does not reach the controller): the stage calls it before giving the
        /// next set's spots, so <see cref="SetSpots"/> applies again. A finished game is already ended.
        /// </summary>
        public void EndGame() => _begun = false;

        // ================================================================== IActivityController

        /// <summary>
        /// A new game: the parameters at d (or the challenge's fixed ones), the hidden spots, decoys and hint times from the
        /// seed. Allocation-free (the rules come with the setup). Call <see cref="SetTimid"/> right after.
        /// </summary>
        public void Begin(in ActivitySetup setup)
        {
            var rules = setup.Rules ?? (_defaultRules ??= HideSeekRules.Create());
            _seconds = setup.Seconds > 0f ? setup.Seconds : HideSeekRules.BaseSeconds;
            float slower = setup.Slower ? ActivityConfig.SlowerTimeMultiplier : 1f;

            int hidden = Read(rules, in setup, HideSeekRules.Hidden);
            int decoys = Read(rules, in setup, HideSeekRules.Decoys);
            _visibility = (float)Clamp01(ReadValue(rules, in setup, HideSeekRules.Visibility));
            _hintDelay = (float)Math.Max(0.0, ReadValue(rules, in setup, HideSeekRules.HintDelay));
            _hintsEnabled = Read(rules, in setup, HideSeekRules.NoHints) <= 0;
            _itemsEnabled = Read(rules, in setup, HideSeekRules.NoItems) <= 0;
            _mistakeLimit = Math.Max(0, Read(rules, in setup, HideSeekRules.MistakeLimit));
            _night = (float)Clamp01(ReadValue(rules, in setup, HideSeekRules.Night));
            _sameSpecies = Read(rules, in setup, HideSeekRules.SameSpecies) > 0;
            double move = ReadValue(rules, in setup, HideSeekRules.MoveEvery);
            _moveEvery = move > 0.0 ? (float)(move * slower) : 0f;
            _magnifierDuration = HideSeekRules.MagnifierSeconds * slower;

            // The zone's spots bound the counts; challenge 9 keeps one free spot (never a decoy) to move to.
            int room = _moveEvery > 0f && _spots >= 2 ? _spots - 1 : _spots;
            _hidden = Clamp(hidden, _spots > 0 ? 1 : 0, room);
            _decoys = Clamp(decoys, 0, room - _hidden);

            _begun = true;
            _done = false;
            _paused = false;
            _elapsed = 0.0;
            _magnifierUsed = false;
            _whistleUsed = false;
            _magnifierSpot = -1;
            _magnifierLeft = 0.0;
            _lastTap = -1;
            _score = 0;
            _streak = 0;
            _bestStreak = 0;
            _foundCount = 0;
            _foundBefore = 0;
            _emptyTouches = 0;
            _mistakes = 0;
            _moves = 0;
            _timeBonus = 0;
            _emptyRing = 0;
            _emptyInRing = 0;
            _pausedUntil = double.NegativeInfinity;
            _lastRustle = double.NegativeInfinity;
            ClearEvents();

            Layout(setup.Seed);
            // The next game on these spots avoids this layout (the stage may set another zone's memory instead).
            _lastLayout = _layoutMask;
            Timings(setup.Seed);
            _moveRng = new DeterministicRandom(SeedHash.Mix(setup.Seed, MoveSalt));
            _nextMove = _moveEvery > 0f ? _moveEvery : double.PositiveInfinity;
            if (_hidden == 0)
                End();
        }

        public void Tick(float deltaSeconds)
        {
            if (!Running || _paused || deltaSeconds <= 0f)
                return;
            _elapsed += deltaSeconds;
            double now = Math.Min(_elapsed, _seconds);

            if (_magnifierSpot >= 0)
            {
                _magnifierLeft -= deltaSeconds;
                if (_magnifierLeft <= 0.0)
                    EndMagnifier();
            }

            if (_hintsEnabled)
            {
                for (int a = 0; a < _hidden; a++)
                {
                    if (_found[a] || _hinting[a] || now < _hintAt[a])
                        continue;
                    _hinting[a] = true;
                    Push(HideSeekEventKind.Hint, _animalSpot[a], a, 0);
                }
            }

            for (int step = 0; step < MaxStepsPerTick && _nextMove <= now && _nextMove < _seconds; step++)
            {
                MoveOne();
                _nextMove += _moveEvery;
            }

            for (int k = 0; k < _decoys; k++)
            {
                for (int step = 0; step < MaxStepsPerTick && _rustleAt[k] <= now; step++)
                {
                    double at = _rustleAt[k];
                    double clear = RustleClearTime(at);
                    if (clear > at)
                    {
                        // Too close to a hint's start or to another decoy: a little later.
                        _rustleAt[k] = clear;
                        continue;
                    }
                    Push(HideSeekEventKind.DecoyRustle, _decoySpot[k], -1, 0);
                    _lastRustle = at;
                    _rustleAt[k] = at + _rustlePeriod[k];
                }
            }

            if (_elapsed >= _seconds)
                End();
        }

        public void Pause() => _paused = true;

        public void Resume() => _paused = false;

        public int Score => _score;

        /// <summary>The challenges measure the score.</summary>
        public int ChallengeScore => _score;

        /// <summary>Animals found in a row without an empty touch (the HUD's « Série »).</summary>
        public int Streak => _streak;

        public bool Finished => _begun && _done;

        // ================================================================== IHideSeekGame: state

        public int SpotCount => _spots;

        public int HiddenCount => _hidden;

        public int AnimalSpot(int animal) => (uint)animal < (uint)_hidden && !_found[animal] ? _animalSpot[animal] : -1;

        public bool AnimalFound(int animal) => (uint)animal < (uint)_hidden && _found[animal];

        public bool AnimalHinting(int animal) => (uint)animal < (uint)_hidden && _hinting[animal] && !_found[animal];

        /// <inheritdoc />
        public void SetTimid(int animal, bool timid)
        {
            if ((uint)animal >= (uint)_hidden)
                return;
            _timid[animal] = timid;
            _hintAt[animal] = _hintDelay + _stagger[animal] + (timid ? HideSeekRules.TimidExtraSeconds : 0f);
        }

        public bool IsTimid(int animal) => (uint)animal < (uint)_hidden && _timid[animal];

        public bool IsDecoy(int spot) => (uint)spot < (uint)_spots && _decoy[spot];

        public float Visibility => _visibility;

        public float HintDelay => _hintDelay;

        public bool HintsEnabled => _hintsEnabled;

        public bool ItemsEnabled => _itemsEnabled;

        public bool MagnifierAvailable => Running && _itemsEnabled && !_magnifierUsed;

        public bool WhistleAvailable => Running && _itemsEnabled && !_whistleUsed;

        public int MagnifierSpot => _magnifierSpot;

        public float MagnifierLeft => _magnifierSpot >= 0 ? (float)Math.Max(0.0, _magnifierLeft) : 0f;

        public bool TouchesPaused => Running && _elapsed < _pausedUntil;

        public int Mistakes => _mistakes;

        public int MistakeLimit => _mistakeLimit;

        public float NightSpotlight => _night;

        public bool SameSpecies => _sameSpecies;

        public float MoveEvery => _moveEvery;

        public float Elapsed => (float)_elapsed;

        public float Seconds => _seconds;

        /// <summary>The animal hiding in a spot now (−1: none).</summary>
        public int AnimalAt(int spot) => (uint)spot < (uint)_spots ? _animalAt[spot] : -1;

        /// <summary>An animal was found at this spot this game (it stands there, happy; touching it does nothing).</summary>
        public bool FoundAt(int spot) => (uint)spot < (uint)_spots && _foundAt[spot];

        /// <summary>Decoys this game (D, clamped to the spots left free) and the spot of each.</summary>
        public int DecoyCount => _decoys;

        public int DecoySpot(int decoy) => (uint)decoy < (uint)_decoys ? _decoySpot[decoy] : -1;

        /// <summary>The second an animal hints this game (hint delay + its stagger + 2 s if Timid).</summary>
        public float HintSecond(int animal) => (uint)animal < (uint)_hidden ? (float)_hintAt[animal] : 0f;

        /// <summary>The animal's stagger (0 to 1.5 s): its hint comes this long after the hint delay.</summary>
        public float HintStagger(int animal) => (uint)animal < (uint)_hidden ? (float)_stagger[animal] : 0f;

        /// <summary>Found now, the animal would give the after-hint points (hinting, ringed by the magnifier or whistled).</summary>
        public bool CountsAfterHint(int animal) =>
            (uint)animal < (uint)_hidden && !_found[animal] && (_hinting[animal] || _assisted[animal]);

        /// <summary>The second of challenge 9's next move (infinity: none).</summary>
        public double NextMoveSecond => _nextMove;

        /// <summary>The last spot touched (−1: none yet); challenge 9's animals slip away from it.</summary>
        public int LastTapSpot => _lastTap;

        /// <summary>Whole seconds left (the points per second count these).</summary>
        public int SecondsLeft => (int)Math.Floor(Math.Max(0.0, _seconds - _elapsed));

        // ================================================================== IHideSeekGame: inputs

        /// <summary>
        /// A touch on a spot: its animal is found (+100 and +2 per whole second left; +50 and +1 after its hint, the magnifier
        /// or the whistle), or the spot is empty (leaves, the streak starts again; challenge 6: a mistake); the 3rd empty
        /// touch within 2 s pauses the touches for 1 s. Ignored while paused, while the touches are paused and on a spot whose
        /// animal was already found. Allocation-free.
        /// </summary>
        public void TapSpot(int spot)
        {
            if (!Running || _paused || (uint)spot >= (uint)_spots || _elapsed < _pausedUntil)
                return;
            int animal = _animalAt[spot];
            if (animal >= 0)
            {
                _lastTap = spot;
                Find(animal, spot);
                return;
            }
            if (_foundAt[spot])
                return;
            _lastTap = spot;
            _emptyTouches++;
            _streak = 0;
            Push(HideSeekEventKind.Empty, spot, -1, 0);
            if (_mistakeLimit > 0)
            {
                _mistakes++;
                Push(HideSeekEventKind.Mistake, spot, -1, 0);
                if (_mistakes >= _mistakeLimit)
                {
                    End();
                    return;
                }
            }
            _emptyAt[_emptyRing] = _elapsed;
            _emptyRing = (_emptyRing + 1) % _emptyAt.Length;
            if (_emptyInRing < _emptyAt.Length)
                _emptyInRing++;
            // The oldest of the last 3 empty touches is where the ring writes next.
            if (_emptyInRing == _emptyAt.Length && _elapsed - _emptyAt[_emptyRing] <= HideSeekRules.EmptyBurstSeconds)
            {
                _pausedUntil = _elapsed + HideSeekRules.TouchPauseSeconds;
                _emptyInRing = 0;
                Push(HideSeekEventKind.TouchesPaused, -1, -1, 0);
            }
        }

        /// <summary>
        /// The magnifier, once per game when the items exist: the occupied spot nearest to the point gets a soft ring for 2 s
        /// and its animal counts as found after its hint. Nothing happens (and it stays available) when no animal is hidden.
        /// </summary>
        public void UseMagnifier(float x, float y, float z)
        {
            if (!Running || _paused || !_itemsEnabled || _magnifierUsed)
                return;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int s = 0; s < _spots; s++)
            {
                if (_animalAt[s] < 0)
                    continue;
                float dx = _x[s] - x, dy = _y[s] - y, dz = _z[s] - z;
                float distance = dx * dx + dy * dy + dz * dz;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = s;
                }
            }
            if (best < 0)
                return;
            _magnifierUsed = true;
            _magnifierSpot = best;
            _magnifierLeft = _magnifierDuration;
            int animal = _animalAt[best];
            _assisted[animal] = true;
            Push(HideSeekEventKind.Magnifier, best, animal, 0);
        }

        /// <summary>
        /// The whistle, once per game when the items exist: every animal still hidden wiggles once (one Whistle event each, in
        /// animal order) and counts as found after its hint.
        /// </summary>
        public void UseWhistle()
        {
            if (!Running || _paused || !_itemsEnabled || _whistleUsed || _foundCount >= _hidden)
                return;
            _whistleUsed = true;
            for (int a = 0; a < _hidden; a++)
            {
                if (_found[a])
                    continue;
                _assisted[a] = true;
                Push(HideSeekEventKind.Whistle, _animalSpot[a], a, 0);
            }
        }

        // ================================================================== IHideSeekGame: events and numbers

        public int EventCount => _eventCount;

        /// <summary>The events since the last <see cref="ClearEvents"/>, oldest first (the ring keeps the last 128).</summary>
        public HideSeekEvent Event(int index) =>
            (uint)index < (uint)_eventCount ? _events[(_eventStart + index) % EventCapacity] : default;

        public void ClearEvents()
        {
            _eventStart = 0;
            _eventCount = 0;
        }

        public int FoundCount => _foundCount;

        public int FoundBeforeHint => _foundBefore;

        public int EmptyTouches => _emptyTouches;

        public bool AllFound => _hidden > 0 && _foundCount >= _hidden;

        public int BestStreak => _bestStreak;

        /// <summary>The bonus given when every animal was found (+5 per whole second left).</summary>
        public int TimeBonus => _timeBonus;

        public bool MagnifierUsed => _magnifierUsed;

        public bool WhistleUsed => _whistleUsed;

        /// <summary>Challenge 9's moves this game.</summary>
        public int Moves => _moves;

        // ================================================================== rules

        private bool Running => _begun && !_done;

        private void Find(int animal, int spot)
        {
            bool after = _hinting[animal] || _assisted[animal];
            int left = SecondsLeft;
            int points = after
                ? HideSeekRules.HintFoundPoints + HideSeekRules.HintFoundSecondPoints * left
                : HideSeekRules.FoundPoints + HideSeekRules.FoundSecondPoints * left;
            _found[animal] = true;
            _animalAt[spot] = -1;
            _foundAt[spot] = true;
            _foundCount++;
            if (!after)
                _foundBefore++;
            _streak++;
            if (_streak > _bestStreak)
                _bestStreak = _streak;
            _score += points;
            if (_magnifierSpot == spot)
                EndMagnifier();
            Push(after ? HideSeekEventKind.FoundAfterHint : HideSeekEventKind.Found, spot, animal, points);
            if (_foundCount < _hidden)
                return;
            _timeBonus = HideSeekRules.AllFoundSecondPoints * left;
            _score += _timeBonus;
            Push(HideSeekEventKind.AllFound, -1, -1, _timeBonus);
            End();
        }

        private void EndMagnifier()
        {
            _magnifierSpot = -1;
            _magnifierLeft = 0.0;
        }

        /// <summary>The end (every animal found, time up or challenge 6's 4th mistake): <see cref="HideSeekEventKind.Finished"/>.</summary>
        private void End()
        {
            if (_done)
                return;
            _done = true;
            EndMagnifier();
            Push(HideSeekEventKind.Finished, -1, -1, 0);
        }

        /// <summary>
        /// Challenge 9: one animal not found yet (seeded; never the one the magnifier rings) slips to the free spot farthest
        /// from the last touch (from its own spot before any touch), jittered, among the game's best spots first; never a
        /// decoy, an occupied spot nor a spot whose animal was found. No candidate: no move (Begin keeps one free spot).
        /// </summary>
        private void MoveOne()
        {
            int ringed = _magnifierSpot >= 0 ? _animalAt[_magnifierSpot] : -1;
            int count = 0;
            for (int a = 0; a < _hidden; a++)
                if (!_found[a] && a != ringed)
                    _order[count++] = a;
            if (count == 0)
                return;
            int animal = _order[_moveRng.Next(count)];
            int from = _animalSpot[animal];
            int away = _lastTap >= 0 ? _lastTap : from;
            int best = -1;
            double bestScore = -1.0;
            // The best spots (the layout's window) first; the others only when none of them is free.
            for (int pass = 0; pass < 2 && best < 0; pass++)
            {
                int end = pass == 0 ? _bestWindow : _spots;
                for (int s = 0; s < end; s++)
                {
                    if (s == from || _animalAt[s] >= 0 || _decoy[s] || _foundAt[s])
                        continue;
                    double score = (Distance2(s, away) + 1e-3) * (0.85 + 0.3 * _moveRng.NextDouble());
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = s;
                    }
                }
            }
            if (best < 0)
                return;
            _animalAt[from] = -1;
            _animalAt[best] = (sbyte)animal;
            _animalSpot[animal] = best;
            _moves++;
            Push(HideSeekEventKind.Moved, best, animal, 0);
        }

        /// <summary>
        /// The first second at or after <paramref name="at"/> when a decoy may rustle: 0.6 s clear of every hint that starts
        /// (or started) this game, 0.35 s clear of the last decoy rustle. Returns <paramref name="at"/> when it is clear.
        /// </summary>
        private double RustleClearTime(double at)
        {
            double clear = at;
            if (clear - _lastRustle < HideSeekRules.DecoyGapSeconds)
                clear = _lastRustle + HideSeekRules.DecoyGapSeconds;
            if (!_hintsEnabled)
                return clear;
            for (int pass = 0; pass < _hidden + 1; pass++)
            {
                bool moved = false;
                for (int a = 0; a < _hidden; a++)
                {
                    // A hint that will never start (its animal found first) does not count.
                    if (_found[a] && !_hinting[a])
                        continue;
                    if (Math.Abs(_hintAt[a] - clear) < HideSeekRules.HintGapSeconds)
                    {
                        clear = _hintAt[a] + HideSeekRules.HintGapSeconds;
                        moved = true;
                    }
                }
                if (!moved)
                    break;
            }
            return clear;
        }

        // ================================================================== layout

        /// <summary>
        /// The hidden spots and the decoys from the seed (no allocation):
        /// <list type="number">
        /// <item>N spots, no two side by side, among the zone's best ones (<see cref="BestWindow"/>: N + 2 at 8 % visible, the
        /// whole zone from 20 %; all the spots of the synthetic meadow): a depth-first search over that window (the first in a
        /// seeded order, each next by its distance to those chosen, farthest first, jittered × 0.6 to 1.4; a spot of the
        /// previous layout weighs × 0.5), at most <see cref="SearchBudget"/> steps. No answer: the window takes the next spot,
        /// and so on. The first full answer is the most spread one the jitter allows; a grid's checkerboard is found even
        /// when the farthest corners would block it.</item>
        /// <item>No full answer (more animals than the zone has room apart, or the budget spent): the same search for one
        /// animal fewer, and so on; then each animal left on the free spot with the fewest hidden neighbors, a best one first,
        /// the farthest (jittered) among those.</item>
        /// <item>The previous game's exact layout (<see cref="LastLayout"/>) is drawn again up to 6 times while another as
        /// good (no more pairs side by side) exists; then the animals take the spots in a seeded order.</item>
        /// <item>The D decoys among the free spots, the farthest (jittered) from the animals and from each other.</item>
        /// </list>
        /// </summary>
        private void Layout(uint seed)
        {
            var rng = new DeterministicRandom(SeedHash.Mix(seed, LayoutSalt));
            for (int i = 0; i < MaxSpots; i++)
            {
                _animalAt[i] = -1;
                _decoy[i] = false;
                _foundAt[i] = false;
                _animalSpot[i] = -1;
                _found[i] = false;
                _hinting[i] = false;
                _assisted[i] = false;
                _timid[i] = false;
                _decoySpot[i] = -1;
            }
            _layoutMask = 0;
            if (_hidden == 0)
                return;

            // A draw other than the previous game's layout, as good as the first draw (no more pairs side by side); none in a
            // few tries (the zone has room for one layout only): the first draw.
            int first = BestWindow();
            int firstPairs = 0;
            int firstWindow = first;
            bool kept = false;
            for (int attempt = 0; attempt < LayoutAttempts && !kept; attempt++)
            {
                DrawSpots(ref rng, first);
                uint mask = ChosenMask();
                int pairs = ChosenPairs();
                if (attempt == 0)
                {
                    Array.Copy(_chosen, _firstDraw, _hidden);
                    firstPairs = pairs;
                    firstWindow = _bestWindow;
                }
                kept = (_lastLayout == 0 || mask != _lastLayout) && pairs <= firstPairs;
                for (int a = 0; a < _hidden; a++)
                    _animalAt[_chosen[a]] = -1;
            }
            if (!kept)
            {
                Array.Copy(_firstDraw, _chosen, _hidden);
                _bestWindow = firstWindow;
            }

            // Which animal takes which spot: a seeded order, never the spots' (the first animal does not always get the best).
            for (int a = _hidden - 1; a > 0; a--)
            {
                int j = rng.Next(a + 1);
                int swap = _chosen[a];
                _chosen[a] = _chosen[j];
                _chosen[j] = swap;
            }
            _layoutMask = 0;
            for (int a = 0; a < _hidden; a++)
            {
                Hide(a, _chosen[a]);
                _layoutMask |= 1u << _chosen[a];
            }
            int n = _spots;
            for (int i = 0; i < n; i++)
            {
                float nearest = float.MaxValue;
                for (int a = 0; a < _hidden; a++)
                    nearest = Math.Min(nearest, Distance2(i, _animalSpot[a]));
                _scratch[i] = nearest;
            }
            for (int k = 0; k < _decoys; k++)
            {
                int spot = Farthest(ref rng);
                if (spot < 0)
                {
                    _decoys = k;
                    break;
                }
                _decoy[spot] = true;
                _decoySpot[k] = spot;
                for (int i = 0; i < n; i++)
                    _scratch[i] = Math.Min(_scratch[i], Distance2(i, spot));
            }
        }

        /// <summary>
        /// One draw of the N hidden spots into <see cref="_chosen"/> (marked in <see cref="_animalAt"/>): the search for N apart
        /// in the best window, widening only when spacing forces it; then fewer apart and the rest where they crowd least.
        /// </summary>
        private void DrawSpots(ref DeterministicRandom rng, int first)
        {
            int placed = 0;
            _nodesTotal = 0;
            for (int target = _hidden; target >= 1 && placed == 0; target--)
            {
                for (int width = first; width <= _spots && placed == 0 && _nodesTotal < LayoutBudget; width++)
                {
                    _window = width;
                    _nodes = 0;
                    if (SearchApart(ref rng, 0, target))
                        placed = target;
                }
            }
            if (placed == 0)
            {
                // Budget spent (a crowded set of many spots): the best spot alone, the others where they crowd least.
                _window = first;
                _chosen[0] = 0;
                placed = 1;
            }
            _bestWindow = placed == _hidden ? Math.Max(first, _window) : first;
            for (int a = 0; a < placed; a++)
                _animalAt[_chosen[a]] = (sbyte)a;
            int n = _spots;
            for (int i = 0; i < n; i++)
            {
                float nearest = float.MaxValue;
                for (int a = 0; a < placed; a++)
                    nearest = Math.Min(nearest, Distance2(i, _chosen[a]));
                _scratch[i] = nearest;
            }
            for (int a = placed; a < _hidden; a++)
            {
                int spot = FewestNeighbors(ref rng);
                _chosen[a] = spot;
                _animalAt[spot] = (sbyte)a;
                for (int i = 0; i < n; i++)
                    _scratch[i] = Math.Min(_scratch[i], Distance2(i, spot));
            }
        }

        /// <summary>
        /// The best window: N + 2 spots at 8 % visible or less, the whole zone from 20 %, a straight line between (rounded);
        /// the synthetic meadow has no order: all its spots.
        /// </summary>
        private int BestWindow()
        {
            if (_defaultSpots)
                return _spots;
            int narrow = Math.Min(_spots, _hidden + BestWindowExtra);
            double t = (_visibility - NarrowWindowVisibility) / (double)(WholeZoneVisibility - NarrowWindowVisibility);
            t = t < 0.0 ? 0.0 : t > 1.0 ? 1.0 : t;
            int width = (int)Math.Round(narrow + (_spots - narrow) * t, MidpointRounding.AwayFromZero);
            return Clamp(width, narrow, _spots);
        }

        private uint ChosenMask()
        {
            uint mask = 0;
            for (int a = 0; a < _hidden; a++)
                mask |= 1u << _chosen[a];
            return mask;
        }

        private int ChosenPairs()
        {
            int pairs = 0;
            for (int a = 0; a < _hidden; a++)
                for (int b = a + 1; b < _hidden; b++)
                    if (Distance2(_chosen[a], _chosen[b]) < _sideBySide2)
                        pairs++;
            return pairs;
        }

        /// <summary>A spot of the previous layout weighs <see cref="RecentSpotWeight"/> in the draws.</summary>
        private float Freshness(int spot) => (_lastLayout & (1u << spot)) != 0 ? RecentSpotWeight : 1f;

        /// <summary>
        /// One step of the search for <paramref name="target"/> spots none side by side, into <see cref="_chosen"/>: the
        /// candidates of this depth (free, next to none chosen) ordered farthest first, jittered.
        /// </summary>
        private bool SearchApart(ref DeterministicRandom rng, int depth, int target)
        {
            if (depth >= target)
                return true;
            if (++_nodes > SearchBudget || ++_nodesTotal > LayoutBudget)
                return false;
            int row = depth * MaxSpots;
            int count = 0;
            for (int s = 0; s < _window; s++)
            {
                float nearest = float.MaxValue;
                bool skip = false;
                for (int k = 0; k < depth && !skip; k++)
                {
                    float distance = Distance2(s, _chosen[k]);
                    skip = _chosen[k] == s || distance < _sideBySide2;
                    nearest = Math.Min(nearest, distance);
                }
                if (skip)
                    continue;
                float score = depth == 0
                    ? (float)rng.NextDouble() * Freshness(s)
                    : (nearest + 1e-4f) * (0.6f + 0.8f * (float)rng.NextDouble()) * Freshness(s);
                int at = count++;
                while (at > 0 && _candidateScore[row + at - 1] < score)
                {
                    _candidate[row + at] = _candidate[row + at - 1];
                    _candidateScore[row + at] = _candidateScore[row + at - 1];
                    at--;
                }
                _candidate[row + at] = s;
                _candidateScore[row + at] = score;
            }
            for (int i = 0; i < count; i++)
            {
                if (depth + (count - i) < target || _nodes > SearchBudget || _nodesTotal > LayoutBudget)
                    return false;
                _chosen[depth] = _candidate[row + i];
                if (SearchApart(ref rng, depth + 1, target))
                    return true;
            }
            return false;
        }

        private void Hide(int animal, int spot)
        {
            _animalAt[spot] = (sbyte)animal;
            _animalSpot[animal] = spot;
        }

        /// <summary>
        /// The free spot with the fewest hidden neighbors; among those a spot of the best ones (the layout's window) first, then
        /// the farthest (<see cref="_scratch"/>, jittered).
        /// </summary>
        private int FewestNeighbors(ref DeterministicRandom rng)
        {
            int best = -1;
            int bestNeighbors = int.MaxValue;
            bool bestInWindow = false;
            double bestScore = -1.0;
            for (int i = 0; i < _spots; i++)
            {
                if (_animalAt[i] >= 0 || _decoy[i])
                    continue;
                int neighbors = 0;
                for (int j = 0; j < _spots; j++)
                    if (_animalAt[j] >= 0 && Distance2(i, j) < _sideBySide2)
                        neighbors++;
                bool inWindow = i < _bestWindow;
                double score = (_scratch[i] + 1e-4) * (0.6 + 0.8 * rng.NextDouble()) * Freshness(i);
                bool better = neighbors < bestNeighbors
                    || (neighbors == bestNeighbors && inWindow && !bestInWindow)
                    || (neighbors == bestNeighbors && inWindow == bestInWindow && score > bestScore);
                if (better)
                {
                    bestNeighbors = neighbors;
                    bestInWindow = inWindow;
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>The free spot farthest from those chosen (<see cref="_scratch"/>), jittered; −1 when none is free.</summary>
        private int Farthest(ref DeterministicRandom rng)
        {
            int best = -1;
            double bestScore = -1.0;
            for (int i = 0; i < _spots; i++)
            {
                if (_animalAt[i] >= 0 || _decoy[i])
                    continue;
                double score = (_scratch[i] + 1e-4) * (0.6 + 0.8 * rng.NextDouble());
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// The hint times and the decoys' rhythm from the seed: the N staggers evenly spread over 0 to 1.5 s in a seeded
        /// order, each jittered by a quarter of their spacing at most (never two together); each decoy's period (3.5 to 5.5 s)
        /// and first rustle (1.2 s, then about 1.1 s later for each next decoy, plus up to 1.5 s).
        /// </summary>
        private void Timings(uint seed)
        {
            var rng = new DeterministicRandom(SeedHash.Mix(seed, TimingSalt));
            int n = _hidden;
            for (int a = 0; a < n; a++)
                _order[a] = a;
            for (int a = n - 1; a > 0; a--)
            {
                int j = rng.Next(a + 1);
                int swap = _order[a];
                _order[a] = _order[j];
                _order[j] = swap;
            }
            double spacing = n > 1 ? HideSeekRules.HintStaggerSeconds / (n - 1) : 0.0;
            for (int a = 0; a < n; a++)
            {
                double offset = _order[a] * spacing + (rng.NextDouble() - 0.5) * 0.5 * spacing;
                _stagger[a] = Math.Max(0.0, Math.Min(HideSeekRules.HintStaggerSeconds, offset));
                _hintAt[a] = _hintDelay + _stagger[a];
            }
            for (int k = 0; k < _decoys; k++)
            {
                _rustlePeriod[k] = HideSeekRules.DecoyPeriodMinSeconds + HideSeekRules.DecoyPeriodSpanSeconds * rng.NextDouble();
                _rustleAt[k] = HideSeekRules.DecoyFirstSeconds + 1.1 * k + 1.5 * rng.NextDouble();
            }
        }

        private void MeasureSideBySide()
        {
            if (_spots < 2)
            {
                _sideBySide2 = 0f;
                return;
            }
            double sum = 0.0;
            for (int i = 0; i < _spots; i++)
            {
                float nearest = float.MaxValue;
                for (int j = 0; j < _spots; j++)
                    if (j != i)
                        nearest = Math.Min(nearest, Distance2(i, j));
                sum += Math.Sqrt(nearest);
            }
            double radius = SideBySideFactor * sum / _spots;
            _sideBySide2 = (float)(radius * radius);
        }

        private float Distance2(int a, int b)
        {
            float dx = _x[a] - _x[b], dy = _y[a] - _y[b], dz = _z[a] - _z[b];
            return dx * dx + dy * dy + dz * dz;
        }

        private void Push(HideSeekEventKind kind, int spot, int animal, int points)
        {
            int slot;
            if (_eventCount < EventCapacity)
            {
                slot = (_eventStart + _eventCount) % EventCapacity;
                _eventCount++;
            }
            else
            {
                // Full (nobody drained): the oldest event makes room.
                slot = _eventStart;
                _eventStart = (_eventStart + 1) % EventCapacity;
            }
            _events[slot] = new HideSeekEvent(kind, spot, animal, points);
        }

        /// <summary>A whole-number parameter; a rule set without it (another activity's rules) falls back to Hide and Seek's at d.</summary>
        private static int Read(ActivityRules rules, in ActivitySetup setup, int parameter)
        {
            double value = ReadValue(rules, in setup, parameter);
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static double ReadValue(ActivityRules rules, in ActivitySetup setup, int parameter)
        {
            double value = rules.Value(parameter, setup.Difficulty, setup.Challenge);
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                var fallback = _defaultRules ??= HideSeekRules.Create();
                value = fallback.Value(parameter, setup.Difficulty, -1);
            }
            return double.IsNaN(value) ? 0.0 : value;
        }

        private static double Clamp01(double value) => value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
