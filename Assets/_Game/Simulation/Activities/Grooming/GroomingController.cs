#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The grooming game (doc 01 Contenu, S-ACT-001; doc « Mini-jeux &amp; activités », S-ACT-001), ticked by the
    /// <see cref="ActivityRunner"/> and played by its stage through <see cref="IGroomingGame"/>.
    /// <list type="bullet">
    /// <item>The coat is cut into zones measured on the model (<see cref="SetBody"/>, up to 64; without it an ellipsoid of 40
    /// zones, the SmallQuadruped's count, so tests and headless runs work). A game dirties Z zones (clamped to the body) in
    /// a few soft patches spread over the body, lays D debris and B burrs (never two on a zone, never on a bubble spot) and
    /// chooses the 2 bubble spots, all from the game's seed.</item>
    /// <item>A zone is clean after P passes (<see cref="Brush"/>; × 2 with the golden brush): +10 × the streak multiplier
    /// (× 1.0, +0.1 per zone cleaned in a row, × 2.0 at most). Brushing a clean zone, or a zone that was never dirty, is a
    /// rebrush: the animal giggles and the streak starts again, no point lost (challenge 2: −20, never under 0). Kindness:
    /// one giggle at most per 0.6 s (one stroke over clean fur), and a zone just cleaned can be passed again for 0.5 s (the
    /// finger is still on it). Once every zone is clean, brushing is petting: a giggle, nothing counted.</item>
    /// <item>Debris: one tap, +15. Burr: the first tap loosens it, the second +25. They can be taken until the end, the bow
    /// phase included, and never hold the bow back.</item>
    /// <item>Soap bubbles at 15 s and 30 s, on their spots until tapped or the bow phase: a tap cleans the 3 dirty zones
    /// nearest to the bubble, each counted with the streak. The golden brush is offered once, at 20 s or as soon after as
    /// the streak reaches × 1.5, on a dirty zone, until taken or the bow phase; taken, 5 s of double passes.</item>
    /// <item>The bow phase starts when the last zone is clean, or 5 s before the end (40 s of 45; 25 s of challenge 4's
    /// 30 s); a bow placed gives +50 (challenge 8: 0 after 35 s), then +5 per whole second left when every zone is clean, and
    /// ends the game. Otherwise the game ends at its time. Every time of the game scales with its length (challenge 4,
    /// « Activités plus lentes »).</item>
    /// </list>
    /// Events go to a fixed ring the stage drains each frame. Nothing allocates after construction: <see cref="Tick"/>, the
    /// inputs and the queries only read and write preallocated arrays.
    /// </summary>
    public sealed class GroomingController : IActivityController, IGroomingGame
    {
        public const int MaxZones = 64;

        /// <summary>The synthetic body's zones (the SmallQuadruped's count, doc 01).</summary>
        public const int DefaultZoneCount = 40;

        /// <summary>Debris and burrs of a game, at most (one per zone, the bubble spots kept free).</summary>
        public const int MaxItems = MaxZones;

        public const int EventCapacity = 128;

        private const uint LayoutSalt = 0x47524F4Du;
        private const int Bubbles = GroomingRules.BubbleCount;

        private static ActivityRules? _defaultRules;

        // ---------------- the body ----------------
        private readonly float[] _x = new float[MaxZones];
        private readonly float[] _y = new float[MaxZones];
        private readonly float[] _z = new float[MaxZones];
        private int _bodyZones;

        // ---------------- zones of the game ----------------
        private readonly bool[] _wasDirty = new bool[MaxZones];
        private readonly byte[] _passesLeft = new byte[MaxZones];
        private readonly double[] _cleanedAt = new double[MaxZones];
        private readonly bool[] _taken = new bool[MaxZones];
        private readonly sbyte[] _patchOf = new sbyte[MaxZones];

        // ---------------- debris and burrs ----------------
        private readonly int[] _itemZone = new int[MaxItems];
        private readonly bool[] _itemBurr = new bool[MaxItems];
        private readonly bool[] _itemActive = new bool[MaxItems];
        private readonly byte[] _itemTaps = new byte[MaxItems];
        private int _items;

        // ---------------- bubbles ----------------
        private readonly int[] _bubbleZone = new int[Bubbles];

        /// <summary>Zones measured too thin to carry a prop (ears, tail tips): see <see cref="SetPropZones"/>.</summary>
        private readonly bool[] _propBlocked = new bool[MaxZones];

        /// <summary>The layout still keeps props off the measured zones (until the others are used up).</summary>
        private bool _avoidBlocked;

        private bool Avoided(int zone) => _avoidBlocked && _propBlocked[zone];
        private readonly bool[] _bubbleActive = new bool[Bubbles];
        private readonly bool[] _bubbleSpawned = new bool[Bubbles];
        private readonly double[] _bubbleAt = new double[Bubbles];
        private readonly int[] _nearest = new int[GroomingRules.BubbleZones];

        // ---------------- layout scratch ----------------
        private const int MaxPatches = (MaxZones + 2) / 5;
        private readonly float[] _scratch = new float[MaxZones];
        private readonly float[] _patchDistance = new float[MaxPatches * MaxZones];
        private readonly int[] _seeds = new int[MaxZones];
        private readonly int[] _order = new int[MaxZones];

        // ---------------- events ----------------
        private readonly GroomingEvent[] _events = new GroomingEvent[EventCapacity];
        private int _eventStart;
        private int _eventCount;

        // ---------------- the game ----------------
        private bool _begun;
        private bool _paused;
        private GroomingPhase _phase = GroomingPhase.Done;
        private double _elapsed;
        private float _seconds = GroomingRules.BaseSeconds;
        private double _bowAt;
        private double _goldenAt;
        private double _deadline;
        private float _goldenDuration = GroomingRules.GoldenBrushSeconds;
        private int _passesNeeded = 2;
        private int _patches;
        private int _dirtyTotal;
        private int _dirtyLeft;
        private int _penalty;
        private float _movement;
        private int _score;
        private int _streak;
        private int _bestStreak;
        private double _lastRebrush;
        private int _goldenZone = -1;
        private bool _goldenOffered;
        private bool _goldenActive;
        private bool _goldenTaken;
        private double _goldenLeft;
        private int _zonesCleaned;
        private int _debrisRemoved;
        private int _rebrushes;
        private int _bubblesPopped;
        private bool _bowPlaced;
        private GroomingBow _bow;
        private int _bowPoints;
        private int _timeBonus;

        public GroomingController()
        {
            SetSyntheticBody(DefaultZoneCount);
        }

        // ================================================================== the body

        /// <inheritdoc />
        public void SetBody(int zoneCount, float[] x, float[] y, float[] z)
        {
            if (Running)
                return;
            Array.Clear(_propBlocked, 0, _propBlocked.Length);
            int count = Math.Min(Math.Min(zoneCount, MaxZones), Math.Min(x.Length, Math.Min(y.Length, z.Length)));
            if (count <= 0)
            {
                SetSyntheticBody(DefaultZoneCount);
                return;
            }
            Array.Copy(x, _x, count);
            Array.Copy(y, _y, count);
            Array.Copy(z, _z, count);
            _bodyZones = count;
        }

        /// <summary>
        /// Ends a game left unfinished (the runner's Quit does not reach the controller): the stage calls it before measuring
        /// the next animal, so <see cref="SetBody"/> applies again. A finished game is already ended.
        /// </summary>
        public void EndGame() => _begun = false;

        /// <summary>
        /// The zones too thin to carry a prop, measured on the model by the stage (an ear, a tail tip): debris, burrs, the soap
        /// bubbles and the golden brush lie elsewhere when the coat has enough other zones (collision rule: nothing hangs over
        /// an edge). Call after <see cref="SetBody"/>, between games.
        /// </summary>
        public void SetPropZones(bool[] thin, int count)
        {
            Array.Clear(_propBlocked, 0, _propBlocked.Length);
            int n = Math.Min(Math.Min(count, MaxZones), thin.Length);
            for (int i = 0; i < n; i++)
                _propBlocked[i] = thin[i];
        }

        /// <summary>
        /// A synthetic body: <paramref name="zoneCount"/> zones spread evenly over an ellipsoid (a Fibonacci lattice, 1 long,
        /// 0.7 high, 0.6 wide). For tests, headless runs and a model whose zones are not measured. Ignored during a game.
        /// </summary>
        public void SetSyntheticBody(int zoneCount)
        {
            if (Running)
                return;
            Array.Clear(_propBlocked, 0, _propBlocked.Length);
            int n = Math.Max(1, Math.Min(MaxZones, zoneCount));
            const double golden = 2.399963229728653;
            for (int i = 0; i < n; i++)
            {
                double h = 1.0 - 2.0 * (i + 0.5) / n;
                double r = Math.Sqrt(Math.Max(0.0, 1.0 - h * h));
                double a = i * golden;
                _x[i] = (float)(Math.Cos(a) * r * 0.5);
                _y[i] = (float)(h * 0.35);
                _z[i] = (float)(Math.Sin(a) * r * 0.3);
            }
            _bodyZones = n;
        }

        /// <summary>A zone's point on the body (the stage's measured point, or the synthetic one).</summary>
        public float ZoneX(int zone) => (uint)zone < (uint)_bodyZones ? _x[zone] : 0f;

        public float ZoneY(int zone) => (uint)zone < (uint)_bodyZones ? _y[zone] : 0f;

        public float ZoneZ(int zone) => (uint)zone < (uint)_bodyZones ? _z[zone] : 0f;

        // ================================================================== IActivityController

        /// <summary>
        /// A new game: the parameters at d (or the challenge's fixed ones), the times scaled with the game's length, the
        /// layout from the seed. Allocation-free (the rules come with the setup).
        /// </summary>
        public void Begin(in ActivitySetup setup)
        {
            var rules = setup.Rules ?? (_defaultRules ??= GroomingRules.Create());
            _seconds = setup.Seconds > 0f ? setup.Seconds : GroomingRules.BaseSeconds;
            double scale = _seconds / GroomingRules.BaseSeconds;
            float slower = setup.Slower ? ActivityConfig.SlowerTimeMultiplier : 1f;
            _bubbleAt[0] = GroomingRules.FirstBubbleSecond * scale;
            _bubbleAt[1] = GroomingRules.SecondBubbleSecond * scale;
            _goldenAt = GroomingRules.GoldenBrushSecond * scale;
            _bowAt = Math.Max(0.0, _seconds - GroomingRules.BowLeadSeconds * slower);
            _goldenDuration = GroomingRules.GoldenBrushSeconds * slower;

            int zones = Clamp(Read(rules, in setup, GroomingRules.Zones), 0, _bodyZones);
            int debris = Math.Max(0, Read(rules, in setup, GroomingRules.Debris));
            int burrs = Math.Max(0, Read(rules, in setup, GroomingRules.Burrs));
            _passesNeeded = Clamp(Read(rules, in setup, GroomingRules.Passes), 1, 9);
            _penalty = Math.Max(0, Read(rules, in setup, GroomingRules.RebrushPenalty));
            int deadline = Math.Max(0, Read(rules, in setup, GroomingRules.BowDeadline));
            _deadline = deadline > 0 ? deadline * scale : 0.0;
            double movement = ReadValue(rules, in setup, GroomingRules.Movement);
            float speed = setup.SpeedMultiplier > 0f ? setup.SpeedMultiplier : 1f;
            _movement = (float)Math.Max(0.0, movement * speed);

            _begun = true;
            _paused = false;
            _phase = GroomingPhase.Brushing;
            _elapsed = 0.0;
            _score = 0;
            _streak = 0;
            _bestStreak = 0;
            _lastRebrush = double.NegativeInfinity;
            _goldenZone = -1;
            _goldenOffered = false;
            _goldenActive = false;
            _goldenTaken = false;
            _goldenLeft = 0.0;
            _zonesCleaned = 0;
            _debrisRemoved = 0;
            _rebrushes = 0;
            _bubblesPopped = 0;
            _bowPlaced = false;
            _bow = GroomingBow.Pink;
            _bowPoints = 0;
            _timeBonus = 0;
            ClearEvents();

            Layout(setup.Seed, zones, debris, burrs);
            if (_dirtyLeft == 0)
            {
                Push(GroomingEventKind.AllClean, -1, 0);
                EnterBow();
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (!Running || _paused || deltaSeconds <= 0f)
                return;
            _elapsed += deltaSeconds;
            if (_phase == GroomingPhase.Brushing)
            {
                for (int b = 0; b < Bubbles; b++)
                {
                    if (_bubbleSpawned[b] || _elapsed < _bubbleAt[b])
                        continue;
                    _bubbleSpawned[b] = true;
                    _bubbleActive[b] = true;
                    Push(GroomingEventKind.BubbleSpawned, b, 0);
                }
                if (!_goldenOffered && _elapsed >= _goldenAt && _streak >= GroomingRules.GoldenBrushStreak && _dirtyLeft > 0)
                    OfferGoldenBrush();
            }
            if (_goldenActive)
            {
                _goldenLeft -= deltaSeconds;
                if (_goldenLeft <= 0.0)
                    EndGoldenBrush();
            }
            if (_phase == GroomingPhase.Brushing && _elapsed >= _bowAt)
                EnterBow();
            if (_elapsed >= _seconds)
                End(0, 0);
        }

        public void Pause() => _paused = true;

        public void Resume() => _paused = false;

        public int Score => _score;

        /// <summary>The challenges measure the score.</summary>
        public int ChallengeScore => _score;

        /// <summary>Zones cleaned in a row (the HUD's « Série »).</summary>
        public int Streak => _streak;

        public bool Finished => _begun && _phase == GroomingPhase.Done;

        // ================================================================== IGroomingGame: state

        public int ZoneCount => _bodyZones;

        /// <summary>Dust is left on the zone now (dirty this game and not clean yet).</summary>
        public bool IsDirty(int zone) => (uint)zone < (uint)_bodyZones && _passesLeft[zone] > 0;

        /// <summary>The zone was dirty when the game began (cleaned or not since).</summary>
        public bool WasDirty(int zone) => (uint)zone < (uint)_bodyZones && _wasDirty[zone];

        /// <summary>The dust patch a zone belongs to (−1: never dirty): the A15 swipe can follow one patch.</summary>
        public int DirtPatch(int zone) => (uint)zone < (uint)_bodyZones ? _patchOf[zone] : -1;

        /// <summary>Dust patches this game (about one per 5 dirty zones).</summary>
        public int DirtPatches => _patches;

        public float Dust(int zone) =>
            (uint)zone < (uint)_bodyZones && _passesLeft[zone] > 0 ? _passesLeft[zone] / (float)_passesNeeded : 0f;

        public int DebrisCount => _items;

        public bool DebrisActive(int debris) => (uint)debris < (uint)_items && _itemActive[debris];

        public int DebrisZone(int debris) => (uint)debris < (uint)_items ? _itemZone[debris] : -1;

        public bool DebrisIsBurr(int debris) => (uint)debris < (uint)_items && _itemBurr[debris];

        public int DebrisTaps(int debris) => (uint)debris < (uint)_items ? _itemTaps[debris] : 0;

        public int BubbleCount => Bubbles;

        public bool BubbleActive(int bubble) => (uint)bubble < Bubbles && _bubbleActive[bubble];

        public int BubbleZone(int bubble) => (uint)bubble < Bubbles ? _bubbleZone[bubble] : -1;

        /// <summary>The second a bubble appears in this game (15 s and 30 s of 45, scaled).</summary>
        public float BubbleSecond(int bubble) => (uint)bubble < Bubbles ? (float)_bubbleAt[bubble] : 0f;

        public int GoldenBrushZone => _goldenZone;

        public bool GoldenBrushActive => _goldenActive;

        public float GoldenBrushLeft => _goldenActive ? (float)Math.Max(0.0, _goldenLeft) : 0f;

        public GroomingPhase Phase => _phase;

        public float StreakMultiplier => 1f + 0.1f * Math.Min(_streak, GroomingRules.StreakMaxBonus);

        public float Movement => _movement;

        public float Elapsed => (float)_elapsed;

        public float Seconds => _seconds;

        /// <summary>The second the bows appear if the coat is not clean before (40 s of 45).</summary>
        public float BowSecond => (float)_bowAt;

        /// <summary>Passes a zone needs this game (2, or 3 from d8 and in challenges 6 and 10).</summary>
        public int PassesPerZone => _passesNeeded;

        /// <summary>Zones dirtied this game (Z, clamped to the body), and those still dirty.</summary>
        public int DirtyZones => _dirtyTotal;

        public int DirtyLeft => _dirtyLeft;

        public int DebrisLeft
        {
            get
            {
                int left = 0;
                for (int i = 0; i < _items; i++)
                    if (_itemActive[i])
                        left++;
                return left;
            }
        }

        // ================================================================== IGroomingGame: inputs

        /// <summary>
        /// One pass over a zone. Dirty: one pass (two with the golden brush), +10 × the multiplier when it becomes clean.
        /// Clean or never dirty: a rebrush (giggle, streak back to × 1.0; challenge 2: −20), except within 0.5 s of its
        /// cleaning or 0.6 s of the last giggle; once all is clean, a giggle and nothing else. Allocation-free.
        /// </summary>
        public void Brush(int zone)
        {
            if (!Running || _paused || (uint)zone >= (uint)_bodyZones)
                return;
            if (_passesLeft[zone] > 0)
            {
                int passes = _goldenActive ? 2 : 1;
                int left = _passesLeft[zone] - passes;
                if (left > 0)
                {
                    _passesLeft[zone] = (byte)left;
                    Push(GroomingEventKind.ZonePass, zone, 0);
                    return;
                }
                CleanZone(zone);
                return;
            }
            if (_wasDirty[zone] && _elapsed - _cleanedAt[zone] < GroomingRules.CleanGraceSeconds)
                return;
            if (_elapsed - _lastRebrush < GroomingRules.RebrushMergeSeconds)
                return;
            _lastRebrush = _elapsed;
            if (_dirtyLeft == 0)
            {
                // Everything is clean: brushing is petting now, the animal giggles and nothing is counted.
                Push(GroomingEventKind.Rebrush, zone, 0);
                return;
            }
            _rebrushes++;
            _streak = 0;
            int lost = Math.Min(_penalty, _score);
            _score -= lost;
            Push(GroomingEventKind.Rebrush, zone, -lost);
        }

        /// <summary>A tap on a debris (+15) or a burr (first tap loosens, second +25). Inactive debris: nothing.</summary>
        public void TapDebris(int debris)
        {
            if (!Running || _paused || (uint)debris >= (uint)_items || !_itemActive[debris])
                return;
            if (_itemBurr[debris] && _itemTaps[debris] == 0)
            {
                _itemTaps[debris] = 1;
                Push(GroomingEventKind.BurrLoosened, debris, 0);
                return;
            }
            _itemActive[debris] = false;
            if (_itemTaps[debris] < byte.MaxValue)
                _itemTaps[debris]++;
            _debrisRemoved++;
            bool burr = _itemBurr[debris];
            int points = burr ? GroomingRules.BurrPoints : GroomingRules.DebrisPoints;
            _score += points;
            Push(burr ? GroomingEventKind.BurrRemoved : GroomingEventKind.DebrisRemoved, debris, points);
        }

        /// <summary>A tap on a soap bubble: it cleans the 3 dirty zones nearest to it, each with the streak.</summary>
        public void TapBubble(int bubble)
        {
            if (!Running || _paused || _phase != GroomingPhase.Brushing || (uint)bubble >= Bubbles || !_bubbleActive[bubble])
                return;
            _bubbleActive[bubble] = false;
            _bubblesPopped++;
            int count = NearestDirty(_bubbleZone[bubble]);
            int points = 0;
            for (int i = 0; i < count; i++)
                points += ZoneScore(_streak + i);
            Push(GroomingEventKind.BubblePopped, bubble, points, _streak + count);
            for (int i = 0; i < count; i++)
                CleanZone(_nearest[i]);
        }

        /// <summary>The golden brush waiting on the coat is taken: 5 s of double passes.</summary>
        public void TakeGoldenBrush()
        {
            if (!Running || _paused || _goldenZone < 0 || _goldenActive)
                return;
            int zone = _goldenZone;
            _goldenZone = -1;
            _goldenActive = true;
            _goldenTaken = true;
            _goldenLeft = _goldenDuration;
            Push(GroomingEventKind.GoldenBrushStart, zone, 0);
        }

        /// <summary>A bow chosen in the bow phase: +50 (challenge 8: 0 after 35 s), the time bonus when all is clean, the end.</summary>
        public void PlaceBow(GroomingBow bow)
        {
            if (!Running || _paused || _phase != GroomingPhase.Bow)
                return;
            _bowPlaced = true;
            _bow = bow;
            _bowPoints = _deadline > 0.0 && _elapsed > _deadline ? 0 : GroomingRules.BowPoints;
            _score += _bowPoints;
            Push(GroomingEventKind.BowPlaced, (int)bow, _bowPoints);
            int secondsLeft = 0;
            if (_dirtyLeft == 0)
                secondsLeft = (int)Math.Floor(Math.Max(0.0, _seconds - _elapsed));
            End(secondsLeft, secondsLeft * GroomingRules.SecondPoints);
        }

        // ================================================================== IGroomingGame: events and numbers

        public int EventCount => _eventCount;

        /// <summary>The events since the last <see cref="ClearEvents"/>, oldest first (the ring keeps the last 128).</summary>
        public GroomingEvent Event(int index) =>
            (uint)index < (uint)_eventCount ? _events[(_eventStart + index) % EventCapacity] : default;

        public void ClearEvents()
        {
            _eventStart = 0;
            _eventCount = 0;
        }

        public int ZonesCleaned => _zonesCleaned;

        public int DebrisRemoved => _debrisRemoved;

        public int Rebrushes => _rebrushes;

        public bool BowPlaced => _bowPlaced;

        public GroomingBow Bow => _bow;

        /// <summary>Points of the bow placed (50, or 0 after challenge 8's 35 s).</summary>
        public int BowPoints => _bowPoints;

        /// <summary>The time bonus given at the bow (+5 per whole second left with every zone clean).</summary>
        public int TimeBonus => _timeBonus;

        public int BestStreak => _bestStreak;

        public int BubblesPopped => _bubblesPopped;

        public bool GoldenBrushTaken => _goldenTaken;

        // ================================================================== rules

        private bool Running => _begun && _phase != GroomingPhase.Done;

        /// <summary>The points of a zone cleaned with <paramref name="streak"/> zones before it in a row: 10 to 20.</summary>
        private static int ZoneScore(int streak) =>
            GroomingRules.ZonePoints + GroomingRules.StreakStepPoints * Math.Min(streak, GroomingRules.StreakMaxBonus);

        private void CleanZone(int zone)
        {
            if (_passesLeft[zone] == 0)
                return;
            _passesLeft[zone] = 0;
            _cleanedAt[zone] = _elapsed;
            _dirtyLeft--;
            _zonesCleaned++;
            int points = ZoneScore(_streak);
            _score += points;
            _streak++;
            if (_streak > _bestStreak)
                _bestStreak = _streak;
            Push(GroomingEventKind.ZoneCleaned, zone, points);
            if (_dirtyLeft > 0)
                return;
            Push(GroomingEventKind.AllClean, -1, 0);
            if (_goldenActive)
                EndGoldenBrush();
            EnterBow();
        }

        private void OfferGoldenBrush()
        {
            _goldenOffered = true;
            // On the dirty zone nearest to the middle of the dust left: in sight, among the work still to do.
            double cx = 0.0, cy = 0.0, cz = 0.0;
            int n = 0;
            for (int i = 0; i < _bodyZones; i++)
            {
                if (_passesLeft[i] == 0)
                    continue;
                cx += _x[i];
                cy += _y[i];
                cz += _z[i];
                n++;
            }
            if (n == 0)
                return;
            cx /= n;
            cy /= n;
            cz /= n;
            int best = -1;
            double bestDistance = double.MaxValue;
            for (int pass = 0; pass < 2 && best < 0; pass++)
            {
                for (int i = 0; i < _bodyZones; i++)
                {
                    // A thin zone (ear, tail tip) only when no other dusty zone is left.
                    if (_passesLeft[i] == 0 || (pass == 0 && _propBlocked[i]))
                        continue;
                    double dx = _x[i] - cx, dy = _y[i] - cy, dz = _z[i] - cz;
                    double distance = dx * dx + dy * dy + dz * dz;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = i;
                    }
                }
            }
            _goldenZone = best;
            Push(GroomingEventKind.GoldenBrushOffered, best, 0);
        }

        private void EndGoldenBrush()
        {
            if (!_goldenActive)
                return;
            _goldenActive = false;
            _goldenLeft = 0.0;
            Push(GroomingEventKind.GoldenBrushEnd, -1, 0);
        }

        /// <summary>The bows come: the bubbles and a golden brush still waiting leave (the stage hides them on this event).</summary>
        private void EnterBow()
        {
            if (_phase != GroomingPhase.Brushing)
                return;
            _phase = GroomingPhase.Bow;
            for (int b = 0; b < Bubbles; b++)
                _bubbleActive[b] = false;
            _goldenZone = -1;
            Push(GroomingEventKind.BowPhase, -1, 0);
        }

        /// <summary>The end (bow placed or time up): the time bonus, the golden brush put away, <see cref="GroomingEventKind.Finished"/>.</summary>
        private void End(int secondsLeft, int bonus)
        {
            if (_phase == GroomingPhase.Done)
                return;
            EndGoldenBrush();
            for (int b = 0; b < Bubbles; b++)
                _bubbleActive[b] = false;
            _goldenZone = -1;
            _phase = GroomingPhase.Done;
            _timeBonus = bonus;
            _score += bonus;
            Push(GroomingEventKind.Finished, secondsLeft, bonus);
        }

        /// <summary>The up to 3 dirty zones nearest to a zone (itself first when dirty), into <see cref="_nearest"/>.</summary>
        private int NearestDirty(int from)
        {
            int count = 0;
            for (int i = 0; i < _bodyZones; i++)
            {
                if (_passesLeft[i] == 0)
                    continue;
                float distance = Distance2(from, i);
                int at = count < _nearest.Length ? count : _nearest.Length;
                while (at > 0 && Distance2(from, _nearest[at - 1]) > distance)
                {
                    if (at < _nearest.Length)
                        _nearest[at] = _nearest[at - 1];
                    at--;
                }
                if (at < _nearest.Length)
                {
                    _nearest[at] = i;
                    if (count < _nearest.Length)
                        count++;
                }
            }
            return count;
        }

        // ================================================================== layout

        /// <summary>
        /// Dirt, debris and bubble spots from the seed (no allocation):
        /// <list type="number">
        /// <item>Z dirty zones as soft patches of about 5 zones: the patch seeds spread by a jittered farthest-point draw, each
        /// patch then growing round by round onto the free zone nearest to it. Spread over the body, never one clump, never
        /// lone zones that would make every stroke cross clean fur.</item>
        /// <item>The 2 bubble spots on dirty zones far apart (any zone when fewer than 2 are dirty).</item>
        /// <item>D debris and B burrs, one per zone, never on a bubble spot, spread by a jittered farthest-point draw; which
        /// of them are burrs is drawn too. Fewer free zones than items: burrs first, then debris.</item>
        /// </list>
        /// </summary>
        private void Layout(uint seed, int zones, int debris, int burrs)
        {
            var rng = new DeterministicRandom(SeedHash.Mix(seed, LayoutSalt));
            int n = _bodyZones;
            for (int i = 0; i < MaxZones; i++)
            {
                _wasDirty[i] = false;
                _passesLeft[i] = 0;
                _cleanedAt[i] = double.NegativeInfinity;
                _taken[i] = false;
                _patchOf[i] = -1;
            }
            for (int b = 0; b < Bubbles; b++)
            {
                _bubbleZone[b] = 0;
                _bubbleActive[b] = false;
                _bubbleSpawned[b] = false;
            }

            // 1. Dirty patches.
            zones = Clamp(zones, 0, n);
            int patches = zones == 0 ? 0 : Clamp((zones + 2) / 5, 1, Math.Min(zones, MaxPatches));
            _patches = patches;
            int assigned = 0;
            if (patches > 0)
            {
                int first = rng.Next(n);
                MarkDirty(first, 0);
                _seeds[0] = first;
                assigned = 1;
                for (int i = 0; i < n; i++)
                    _scratch[i] = Distance2(i, first);
                for (int p = 1; p < patches; p++)
                {
                    int best = FarthestFree(ref rng, n, _wasDirty);
                    if (best < 0)
                    {
                        patches = p;
                        _patches = p;
                        break;
                    }
                    MarkDirty(best, p);
                    _seeds[p] = best;
                    assigned++;
                    for (int i = 0; i < n; i++)
                        _scratch[i] = Math.Min(_scratch[i], Distance2(i, best));
                }
                // Each patch's distance to every zone (its nearest member), kept up to date as it grows: O(Z × n).
                for (int p = 0; p < patches; p++)
                    for (int i = 0; i < n; i++)
                        _patchDistance[p * MaxZones + i] = Distance2(i, _seeds[p]);
                while (assigned < zones)
                {
                    bool grew = false;
                    for (int p = 0; p < patches && assigned < zones; p++)
                    {
                        int row = p * MaxZones;
                        int best = -1;
                        double bestCost = double.MaxValue;
                        for (int i = 0; i < n; i++)
                        {
                            if (_wasDirty[i])
                                continue;
                            // Nearest to the patch, a little toward its seed (round patches), jittered (never twice the same shape).
                            double cost = (_patchDistance[row + i] + 0.35 * Distance2(i, _seeds[p])) * (0.85 + 0.3 * rng.NextDouble());
                            if (cost < bestCost)
                            {
                                bestCost = cost;
                                best = i;
                            }
                        }
                        if (best < 0)
                            break;
                        MarkDirty(best, p);
                        for (int i = 0; i < n; i++)
                            _patchDistance[row + i] = Math.Min(_patchDistance[row + i], Distance2(i, best));
                        assigned++;
                        grew = true;
                    }
                    if (!grew)
                        break;
                }
            }
            _dirtyTotal = assigned;
            _dirtyLeft = assigned;

            // Zones measured thin (ears, paws, the silhouette's edge) take a prop only once the others are used up: the game
            // keeps its debris count, and a burr never hangs off an ear while a flank is free. They are still dirtied and brushed.
            _avoidBlocked = true;

            // 2. Bubble spots: a dirty zone, then the dirty zone farthest from it.
            int dirtyOpen = 0;
            for (int i = 0; i < n; i++)
                if (_wasDirty[i] && !_propBlocked[i])
                    dirtyOpen++;
            if (dirtyOpen < Bubbles)
                _avoidBlocked = false;
            bool onDirt = assigned >= Bubbles;
            int spot = PickRandom(ref rng, n, onDirt);
            _bubbleZone[0] = spot;
            _taken[spot] = true;
            for (int i = 0; i < n; i++)
                _scratch[i] = Distance2(i, spot);
            int second = -1;
            double farthest = -1.0;
            for (int i = 0; i < n; i++)
            {
                if (_taken[i] || (onDirt && !_wasDirty[i]) || Avoided(i))
                    continue;
                double score = _scratch[i] * (0.8 + 0.4 * rng.NextDouble());
                if (score > farthest)
                {
                    farthest = score;
                    second = i;
                }
            }
            if (second < 0)
                second = spot;
            _bubbleZone[1] = second;
            _taken[second] = true;

            // 3. Debris and burrs, one per free zone, spread.
            int free = 0;
            for (int i = 0; i < n; i++)
                if (!_taken[i])
                    free++;
            burrs = Math.Min(burrs, free);
            debris = Math.Min(debris, free - burrs);
            _items = Math.Min(MaxItems, debris + burrs);
            for (int i = 0; i < n; i++)
                _scratch[i] = float.MaxValue;
            for (int k = 0; k < _items; k++)
            {
                int zone;
                if (k == 0)
                    zone = PickFree(ref rng, n);
                else
                    zone = FarthestFree(ref rng, n, _taken);
                if (zone < 0 && _avoidBlocked)
                {
                    // The good zones are used up: the measured ones take the rest.
                    _avoidBlocked = false;
                    zone = k == 0 ? PickFree(ref rng, n) : FarthestFree(ref rng, n, _taken);
                }
                if (zone < 0)
                {
                    _items = k;
                    break;
                }
                _taken[zone] = true;
                _itemZone[k] = zone;
                _itemBurr[k] = false;
                _itemActive[k] = true;
                _itemTaps[k] = 0;
                for (int i = 0; i < n; i++)
                    _scratch[i] = Math.Min(_scratch[i], Distance2(i, zone));
            }
            // Which items are burrs: a partial shuffle of their indexes.
            for (int k = 0; k < _items; k++)
                _order[k] = k;
            int burrCount = Math.Min(burrs, _items);
            for (int k = 0; k < burrCount; k++)
            {
                int j = k + rng.Next(_items - k);
                int swap = _order[k];
                _order[k] = _order[j];
                _order[j] = swap;
                _itemBurr[_order[k]] = true;
            }
        }

        private void MarkDirty(int zone, int patch)
        {
            _wasDirty[zone] = true;
            _passesLeft[zone] = (byte)_passesNeeded;
            _patchOf[zone] = (sbyte)patch;
        }

        /// <summary>The zone not in <paramref name="used"/> farthest from those chosen (<see cref="_scratch"/>), jittered.</summary>
        private int FarthestFree(ref DeterministicRandom rng, int n, bool[] used)
        {
            int best = -1;
            double bestScore = -1.0;
            for (int i = 0; i < n; i++)
            {
                if (used[i] || Avoided(i))
                    continue;
                double score = _scratch[i] * (0.75 + 0.5 * rng.NextDouble());
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>A random zone not taken (nor avoided), dirty when <paramref name="dirtyOnly"/>.</summary>
        private int PickRandom(ref DeterministicRandom rng, int n, bool dirtyOnly)
        {
            int count = 0;
            for (int i = 0; i < n; i++)
                if ((!dirtyOnly || _wasDirty[i]) && !_taken[i] && !Avoided(i))
                    count++;
            if (count == 0)
                return 0;
            int pick = rng.Next(count);
            for (int i = 0; i < n; i++)
            {
                if ((dirtyOnly && !_wasDirty[i]) || _taken[i] || Avoided(i))
                    continue;
                if (pick-- == 0)
                    return i;
            }
            return 0;
        }

        /// <summary>A random zone not taken yet (−1: none).</summary>
        private int PickFree(ref DeterministicRandom rng, int n)
        {
            int count = 0;
            for (int i = 0; i < n; i++)
                if (!_taken[i] && !Avoided(i))
                    count++;
            if (count == 0)
                return -1;
            int pick = rng.Next(count);
            for (int i = 0; i < n; i++)
            {
                if (_taken[i] || Avoided(i))
                    continue;
                if (pick-- == 0)
                    return i;
            }
            return -1;
        }

        private float Distance2(int a, int b)
        {
            float dx = _x[a] - _x[b], dy = _y[a] - _y[b], dz = _z[a] - _z[b];
            return dx * dx + dy * dy + dz * dz;
        }

        private void Push(GroomingEventKind kind, int index, int points) => Push(kind, index, points, _streak);

        private void Push(GroomingEventKind kind, int index, int points, int streak)
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
            _events[slot] = new GroomingEvent(kind, index, points, streak);
        }

        /// <summary>A whole-number parameter; a rule set without it (another activity's rules) falls back to Grooming's at d.</summary>
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
                var fallback = _defaultRules ??= GroomingRules.Create();
                value = fallback.Value(parameter, setup.Difficulty, -1);
            }
            return double.IsNaN(value) ? 0.0 : value;
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
