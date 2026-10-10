#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The nut drop game (doc 01 Contenu, S-ACT-003; doc « Mini-jeux &amp; activités », S-ACT-003), ticked by the
    /// <see cref="ActivityRunner"/> and played by its stage through <see cref="INutDropGame"/>.
    /// <list type="bullet">
    /// <item>The playfield: x within ± 2.4, items appear at y = 6 and fall to the basket's rim at y = 0, then to the ground
    /// at −0.6. The basket runs toward the finger (<see cref="SetBasketTarget"/>) at 14 u/s, easing over the last unit, its
    /// center kept inside the field; its catch width is the d's (1.20 / 1.00 / 0.85; 0.70 in challenge 4), × 1.5 while the
    /// wide basket lasts. An item crossing the rim with |x − basket| ≤ width / 2 + 0.18 is caught; otherwise it falls on
    /// beside the basket (pushed aside by the basket and the animal under it, never through them) and is missed on the
    /// ground.</item>
    /// <item>Spawning, from the game's seed: an item every 1 / rate seconds (± 25 %), the first at 0.6 s, none that could not
    /// reach the rim before the end; a pinecone at its share, a golden hazelnut at its share, else a hazelnut, an acorn or a
    /// berry alike. Fair: a catchable item never lands farther from the previous catchable one than the basket's catch span
    /// plus 5 u/s × the seconds between them (less a 0.12 s reaction); a pinecone landing within 0.6 s of a catchable item
    /// keeps clear enough of it that the basket can catch the nut and avoid the pinecone, the field's sides included; no item
    /// ever overlaps another at spawn (they then fall together and never touch).</item>
    /// <item>Specials: each (wide basket 8 s, feather × 0.6 for 5 s, golden shower 5 s) falls at most once, with a 50 %
    /// chance, between 15 and 45 s, at least 9 s apart so their effects never overlap; caught, its effect starts. Challenge
    /// 7 has no golden shower (golden hazelnuts count 0 there).</item>
    /// <item>Scoring: a catch adds to the streak (a special too, for 0 points) and scores its item's points (10, 10, 15, 50) ×
    /// the multiplier at that catch (10 + 1 tenth per 5 in the streak, this catch counted, 30 at most), rounded half up; the
    /// 5th catch in a row scores × 1.1 and raises <see cref="NutDropEventKind.MultiplierUp"/>. A pinecone resets the streak
    /// (a <see cref="NutDropEventKind.Pinecone"/> event, no <see cref="NutDropEventKind.Caught"/>); a miss never breaks it.
    /// Challenge 7 scores the berries alone (the other nuts count 0 and still add to the streak; a pinecone still resets it).
    /// Challenge 6 measures the longest streak (<see cref="ChallengeScore"/>).</item>
    /// <item>Challenge 8's wind: a gentle sine, 0.6 u/s at most, swinging over 8 s from a seeded phase; every item in the air
    /// drifts with it alike, and the fair spacing is measured where the items will land.</item>
    /// <item>« Activités plus lentes »: the game lasts × 1.25 (the setup's seconds), falls, spawns and wind × 0.8, the
    /// specials' window and effects × 1.25 (as many items over a longer game); the basket keeps its speed (it is the
    /// player's).</item>
    /// </list>
    /// The simulation runs in steps of 1/60 s at most, so a long frame never lets an item slip past the rim. Events go to a
    /// fixed ring the stage drains each frame. Nothing allocates after construction.
    /// </summary>
    public sealed class NutDropController : IActivityController, INutDropGame, IActivityRewardCounts
    {
        /// <summary>Item slots (a busy game holds about 10 at once).</summary>
        public const int Capacity = 24;

        public const int EventCapacity = 128;

        /// <summary>The largest simulation step, seconds.</summary>
        public const float MaxStep = 1f / 60f;

        private const int MaxStepsPerTick = 20000;
        private const int PlaceTries = 12;
        private const float PostponeSeconds = 0.05f;
        private const uint SpawnSalt = 0x4E555453u;
        private const uint SpecialSalt = 0x53504543u;
        private const uint WindSalt = 0x57494E44u;

        private static ActivityRules? _defaultRules;

        // ---------------- items (fixed slots) ----------------
        private readonly bool[] _active = new bool[Capacity];
        private readonly NutDropItemKind[] _kind = new NutDropItemKind[Capacity];
        private readonly float[] _x = new float[Capacity];
        private readonly float[] _y = new float[Capacity];

        /// <summary>Where and when the item appeared (the wind's drift is measured from there).</summary>
        private readonly float[] _x0 = new float[Capacity];
        private readonly double[] _t0 = new double[Capacity];

        /// <summary>Past the rim beside the basket (missed), and on which side of it.</summary>
        private readonly bool[] _below = new bool[Capacity];
        private readonly sbyte[] _side = new sbyte[Capacity];
        private int _nextSlot;

        // ---------------- events ----------------
        private readonly NutDropEvent[] _events = new NutDropEvent[EventCapacity];
        private int _eventStart;
        private int _eventCount;

        // ---------------- specials ----------------
        private readonly NutDropItemKind[] _specialKind = new NutDropItemKind[3];
        private readonly double[] _specialAt = new double[3];
        private int _specialCount;
        private int _specialNext;
        private NutDropItemKind _special;
        private double _specialLeft;

        // ---------------- parameters of this game ----------------
        private float _seconds = NutDropRules.BaseSeconds;
        private float _timeScale = 1f;
        private float _rate;
        private float _fall;
        private float _pineconeShare;
        private float _goldenShare;
        private float _baseWidth = 1f;
        private bool _berriesOnly;
        private bool _streakGoal;
        private float _windAmplitude;
        private double _windPhase;

        // ---------------- state ----------------
        private bool _begun;
        private bool _done;
        private bool _paused;
        private double _elapsed;
        private float _basketX;
        private float _target;
        private float _velocity;
        private int _score;
        private int _streak;
        private int _bestStreak;
        private int _caught;
        private int _hazelnuts;
        private int _berries;
        private int _pinecones;
        private int _missed;
        private int _spawned;
        private double _nextSpawn;
        private NutDropItemKind _pending;
        private DeterministicRandom _rng;

        // Fair spacing: the last catchable item and the last pinecone, where and when they land.
        private bool _hasGood;
        private float _goodX;
        private double _goodLand;
        private bool _hasPinecone;
        private float _pineconeX;
        private double _pineconeLand;

        private bool Running => _begun && !_done;

        // ================================================================== IActivityController

        /// <summary>A new game: the parameters at d (or the challenge's fixed ones), the specials' times and the wind from the seed. Allocation-free.</summary>
        public void Begin(in ActivitySetup setup)
        {
            var rules = setup.Rules ?? (_defaultRules ??= NutDropRules.Create());
            _seconds = setup.Seconds > 0f ? setup.Seconds : NutDropRules.BaseSeconds;
            _timeScale = setup.Slower ? ActivityConfig.SlowerTimeMultiplier : 1f;
            float speed = setup.SpeedMultiplier > 0f ? setup.SpeedMultiplier : 1f;

            _rate = (float)Math.Max(0.05, Read(rules, in setup, NutDropRules.Rate)) * speed;
            _fall = (float)Math.Max(0.1, Read(rules, in setup, NutDropRules.FallSpeed)) * speed;
            _pineconeShare = (float)Clamp01(Read(rules, in setup, NutDropRules.PineconeShare));
            _goldenShare = (float)Math.Min(1.0 - _pineconeShare, Clamp01(Read(rules, in setup, NutDropRules.GoldenShare)));
            _baseWidth = (float)Math.Max(0.2, Math.Min(NutDropRules.HalfWidth, Read(rules, in setup, NutDropRules.BasketWidth)));
            _berriesOnly = Read(rules, in setup, NutDropRules.BerriesOnly) > 0.5;
            _streakGoal = Read(rules, in setup, NutDropRules.StreakGoal) > 0.5;
            _windAmplitude = (float)Math.Max(0.0, Read(rules, in setup, NutDropRules.Wind)) * speed;
            _windPhase = _windAmplitude > 0f ? 2.0 * Math.PI * SeedHash.Unit(setup.Seed, WindSalt) : 0.0;

            for (int i = 0; i < Capacity; i++)
                _active[i] = false;
            _nextSlot = 0;
            _begun = true;
            _done = false;
            _paused = false;
            _elapsed = 0.0;
            _basketX = 0f;
            _target = 0f;
            _velocity = 0f;
            _score = 0;
            _streak = 0;
            _bestStreak = 0;
            _caught = 0;
            _hazelnuts = 0;
            _berries = 0;
            _pinecones = 0;
            _missed = 0;
            _spawned = 0;
            _special = NutDropItemKind.None;
            _specialLeft = 0.0;
            _hasGood = false;
            _hasPinecone = false;
            _pending = NutDropItemKind.None;
            ClearEvents();

            _rng = new DeterministicRandom(SeedHash.Mix(setup.Seed, SpawnSalt));
            _nextSpawn = NutDropRules.FirstSpawnSeconds;
            ScheduleSpecials(setup.Seed);
        }

        /// <summary>Per frame: the basket, the specials' clocks, the spawns, the falls, catches and misses, in steps of 1/60 s at most. No allocation.</summary>
        public void Tick(float deltaSeconds)
        {
            if (!Running || _paused || deltaSeconds <= 0f)
                return;
            int steps = (int)Math.Ceiling(deltaSeconds / MaxStep);
            if (steps < 1)
                steps = 1;
            else if (steps > MaxStepsPerTick)
                steps = MaxStepsPerTick;
            double h = (double)deltaSeconds / steps;
            float before = _basketX;
            for (int s = 0; s < steps && Running; s++)
                Step(h);
            _velocity = (float)((_basketX - before) / deltaSeconds);
        }

        public void Pause()
        {
            _paused = true;
            _velocity = 0f;
        }

        public void Resume() => _paused = false;

        public int Score => _score;

        /// <summary>The score; challenge 6 « Série d'or » measures the longest streak.</summary>
        public int ChallengeScore => _streakGoal ? _bestStreak : _score;

        /// <summary>Catches in a row without a pinecone (the HUD's « Série »).</summary>
        public int Streak => _streak;

        public bool Finished => _begun && _done;

        // ================================================================== INutDropGame: the field and the basket

        public float HalfWidth => NutDropRules.HalfWidth;

        public float SpawnHeight => NutDropRules.SpawnHeight;

        public float GroundY => NutDropRules.GroundY;

        public float ItemRadius => NutDropRules.ItemRadius;

        public void SetBasketTarget(float x)
        {
            if (!float.IsNaN(x) && !float.IsInfinity(x))
                _target = x;
        }

        public float BasketTarget => _target;

        public float BasketX => _basketX;

        public float BasketWidth => _special == NutDropItemKind.WideBasket ? _baseWidth * NutDropRules.WideFactor : _baseWidth;

        public float BasketVelocity => _velocity;

        /// <summary>The farthest the basket's center goes: ± (half the field − half its width now).</summary>
        public float BasketLimit => Math.Max(0f, NutDropRules.HalfWidth - BasketWidth * 0.5f);

        // ================================================================== INutDropGame: the items

        public int ItemCapacity => Capacity;

        public bool ItemActive(int slot) => (uint)slot < Capacity && _active[slot];

        public NutDropItemKind ItemKind(int slot) => (uint)slot < Capacity && _active[slot] ? _kind[slot] : NutDropItemKind.None;

        public float ItemX(int slot) => (uint)slot < Capacity ? _x[slot] : 0f;

        public float ItemY(int slot) => (uint)slot < Capacity ? _y[slot] : 0f;

        /// <summary>The item went past the rim beside the basket (it falls to the ground, missed).</summary>
        public bool ItemMissed(int slot) => (uint)slot < Capacity && _active[slot] && _below[slot];

        /// <summary>Seconds before an item in the air reaches the rim at today's fall (0 once past it).</summary>
        public float ItemRimSeconds(int slot)
        {
            if (!ItemActive(slot) || _below[slot] || _y[slot] <= 0f)
                return 0f;
            return _y[slot] / (_fall * FallFactor);
        }

        /// <summary>Where an item in the air will cross the rim (the wind's drift included; its x now once past it).</summary>
        public float ItemRimX(int slot)
        {
            if (!ItemActive(slot) || _below[slot] || _windAmplitude <= 0f)
                return ItemX(slot);
            double at = _elapsed + ItemRimSeconds(slot);
            return ClampField(_x0[slot] + (float)(WindDrift(at) - WindDrift(_t0[slot])));
        }

        // ================================================================== INutDropGame: the state

        public int BestStreak => _bestStreak;

        public int MultiplierTenths => NutDropRules.MultiplierTenths(_streak);

        public NutDropItemKind ActiveSpecial => _special;

        public float SpecialLeft => _special != NutDropItemKind.None ? (float)Math.Max(0.0, _specialLeft) : 0f;

        /// <summary>The side wind now, units per second (positive: toward +x); 0 outside challenge 8.</summary>
        public float Wind => _windAmplitude > 0f ? (float)(_windAmplitude * Math.Sin(2.0 * Math.PI * _elapsed / NutDropRules.WindPeriodSeconds + _windPhase)) : 0f;

        public float Elapsed => (float)_elapsed;

        public float Seconds => _seconds;

        /// <summary>Fall speed now (the d's, the slower mode's and the feather's).</summary>
        public float FallSpeedNow => _fall * FallFactor;

        /// <summary>Only the berries score (challenge 7).</summary>
        public bool BerriesOnly => _berriesOnly;

        /// <summary>This game's specials in their falling order, and when each falls (seconds of the game).</summary>
        public int SpecialCount => _specialCount;

        public NutDropItemKind SpecialKind(int index) => (uint)index < (uint)_specialCount ? _specialKind[index] : NutDropItemKind.None;

        public float SpecialSecond(int index) => (uint)index < (uint)_specialCount ? (float)_specialAt[index] : 0f;

        /// <summary>The share of pinecones and of golden hazelnuts, and the base catch width, of this game.</summary>
        public float PineconeShare => _pineconeShare;

        public float GoldenShare => _goldenShare;

        public float BaseBasketWidth => _baseWidth;

        public float SpawnRate => _rate;

        public float BaseFallSpeed => _fall;

        // ================================================================== events

        public int EventCount => _eventCount;

        /// <summary>The events since the last <see cref="ClearEvents"/>, oldest first (the ring keeps the last 128).</summary>
        public NutDropEvent Event(int index) =>
            (uint)index < (uint)_eventCount ? _events[(_eventStart + index) % EventCapacity] : default;

        public void ClearEvents()
        {
            _eventStart = 0;
            _eventCount = 0;
        }

        // ================================================================== the game's numbers

        /// <summary>Nuts caught: hazelnuts, acorns, berries and golden hazelnuts (never a pinecone nor a special).</summary>
        public int Caught => _caught;

        public int HazelnutsCaught => _hazelnuts;

        public int BerriesCaught => _berries;

        public int PineconesCaught => _pinecones;

        /// <summary>Items that reached the ground beside the basket (pinecones included).</summary>
        public int Missed => _missed;

        /// <summary>Items that appeared this game (specials included).</summary>
        public int Spawned => _spawned;

        /// <summary>T40 reward counts (<see cref="NutDropRules.HazelnutCount"/>, <see cref="NutDropRules.BerryCount"/>).</summary>
        public int RewardCount(int index) =>
            index == NutDropRules.HazelnutCount ? _hazelnuts : index == NutDropRules.BerryCount ? _berries : 0;

        // ================================================================== the simulation

        private float FallFactor => _special == NutDropItemKind.Feather ? NutDropRules.FeatherFactor : 1f;

        private void Step(double h)
        {
            _elapsed += h;

            MoveBasket((float)h);

            if (_special != NutDropItemKind.None)
            {
                _specialLeft -= h;
                if (_specialLeft <= 0.0)
                    EndSpecial();
            }

            // Spawns in time order: the regular rain and the specials.
            for (int guard = 0; guard < 16; guard++)
            {
                bool special = _specialNext < _specialCount;
                double specialAt = special ? _specialAt[_specialNext] : double.PositiveInfinity;
                double at = Math.Min(specialAt, _nextSpawn);
                if (at > _elapsed)
                    break;
                if (specialAt <= _nextSpawn)
                {
                    if (Spawn(_specialKind[_specialNext], specialAt))
                        _specialNext++;
                    else
                        _specialAt[_specialNext] = specialAt + PostponeSeconds;
                }
                else
                {
                    SpawnRegular(_nextSpawn);
                }
            }

            float fall = _fall * FallFactor;
            float reach = BasketWidth * 0.5f + NutDropRules.ItemRadius;
            for (int i = 0; i < Capacity; i++)
            {
                if (!_active[i])
                    continue;
                float before = _y[i];
                _y[i] = before - fall * (float)h;
                if (_below[i])
                {
                    // Missed: on beside the basket, pushed aside by it (and the animal under it), never through them.
                    float dx = _x[i] - _basketX;
                    if (dx * _side[i] < reach)
                        _x[i] = Math.Max(-NutDropRules.HalfWidth - NutDropRules.ItemRadius,
                            Math.Min(NutDropRules.HalfWidth + NutDropRules.ItemRadius, _basketX + _side[i] * reach));
                    if (_y[i] <= NutDropRules.GroundY)
                    {
                        _y[i] = NutDropRules.GroundY;
                        _active[i] = false;
                        _missed++;
                        Push(NutDropEventKind.Missed, i, _kind[i], _x[i], 0);
                    }
                    continue;
                }
                if (_windAmplitude > 0f)
                    _x[i] = ClampField(_x0[i] + (float)(WindDrift(_elapsed) - WindDrift(_t0[i])));
                if (before > 0f && _y[i] <= 0f)
                {
                    float dx = _x[i] - _basketX;
                    if (Math.Abs(dx) <= reach)
                    {
                        Catch(i);
                    }
                    else
                    {
                        _below[i] = true;
                        _side[i] = (sbyte)(dx < 0f ? -1 : 1);
                    }
                }
            }

            if (_elapsed >= _seconds)
                End();
        }

        private void MoveBasket(float h)
        {
            float limit = BasketLimit;
            float target = Math.Max(-limit, Math.Min(limit, _target));
            float distance = target - _basketX;
            float gap = Math.Abs(distance);
            if (gap <= 1e-4f)
            {
                _basketX = target;
            }
            else
            {
                float speed = Math.Min(NutDropRules.BasketSpeed, NutDropRules.BasketEase * gap);
                float step = speed * h;
                _basketX = step >= gap ? target : _basketX + (distance > 0f ? step : -step);
            }
            if (_basketX > limit)
                _basketX = limit;
            else if (_basketX < -limit)
                _basketX = -limit;
        }

        private void Catch(int slot)
        {
            var kind = _kind[slot];
            float x = _x[slot];
            _active[slot] = false;
            if (kind == NutDropItemKind.Pinecone)
            {
                _pinecones++;
                _streak = 0;
                Push(NutDropEventKind.Pinecone, slot, kind, x, 0);
                return;
            }

            // The multiplier at the catch counts this catch: the 5th in a row scores × 1.1.
            int before = NutDropRules.MultiplierTenths(_streak);
            int tenths = NutDropRules.MultiplierTenths(_streak + 1);
            int points = NutDropRules.Points(kind);
            if (_berriesOnly && kind != NutDropItemKind.Berry)
                points = 0;
            int gained = (points * tenths + 5) / 10;
            _score += gained;
            _streak++;
            if (_streak > _bestStreak)
                _bestStreak = _streak;
            if (!NutDropRules.IsSpecial(kind))
                _caught++;
            if (kind == NutDropItemKind.Hazelnut || kind == NutDropItemKind.GoldenNut)
                _hazelnuts++;
            else if (kind == NutDropItemKind.Berry)
                _berries++;
            _events[PushSlot()] = new NutDropEvent(NutDropEventKind.Caught, slot, kind, x, gained, _streak, tenths, (float)_elapsed);
            if (tenths > before)
                Push(NutDropEventKind.MultiplierUp, -1, NutDropItemKind.None, _basketX, 0);
            if (NutDropRules.IsSpecial(kind))
                StartSpecial(kind, slot, x);
        }

        private void StartSpecial(NutDropItemKind kind, int slot, float x)
        {
            if (_special != NutDropItemKind.None)
                EndSpecial();
            float seconds = NutDropRules.SpecialSeconds(kind) * _timeScale;
            _special = kind;
            _specialLeft = seconds;
            _events[PushSlot()] = new NutDropEvent(NutDropEventKind.SpecialStarted, slot, kind, x, 0, _streak, MultiplierTenths, seconds);
        }

        private void EndSpecial()
        {
            var kind = _special;
            _special = NutDropItemKind.None;
            _specialLeft = 0.0;
            Push(NutDropEventKind.SpecialEnded, -1, kind, _basketX, 0);
        }

        private void End()
        {
            if (_done)
                return;
            _done = true;
            _velocity = 0f;
            Push(NutDropEventKind.Finished, -1, NutDropItemKind.None, _basketX, 0);
        }

        // ---------------- spawning ----------------

        private void SpawnRegular(double at)
        {
            float fallSeconds = NutDropRules.SpawnHeight / _fall;
            if (at + fallSeconds > _seconds)
            {
                // It could not reach the rim before the end: the rain stops here.
                _nextSpawn = double.PositiveInfinity;
                return;
            }
            if (_pending == NutDropItemKind.None)
            {
                double u = _rng.NextDouble();
                double v = _rng.NextDouble();
                if (u < _pineconeShare)
                    _pending = NutDropItemKind.Pinecone;
                else if (u < _pineconeShare + _goldenShare)
                    _pending = NutDropItemKind.GoldenNut;
                else
                    _pending = v < 1.0 / 3.0 ? NutDropItemKind.Hazelnut : v < 2.0 / 3.0 ? NutDropItemKind.Acorn : NutDropItemKind.Berry;
            }
            // The golden shower: only golden hazelnuts while it lasts.
            var kind = _special == NutDropItemKind.GoldenShower ? NutDropItemKind.GoldenNut : _pending;
            if (!Spawn(kind, at))
            {
                // No room right now (another item fills the top): a little later, the same draw kept.
                _nextSpawn = at + PostponeSeconds;
                return;
            }
            _pending = NutDropItemKind.None;
            float jitter = 1f + NutDropRules.RateJitter * (float)(2.0 * _rng.NextDouble() - 1.0);
            _nextSpawn = at + jitter / _rate;
        }

        /// <summary>Places an item that appeared at <paramref name="at"/> (fair, never overlapping); false when there is no room yet.</summary>
        private bool Spawn(NutDropItemKind kind, double at)
        {
            int slot = FreeSlot();
            if (slot < 0)
                return false;
            float r = NutDropRules.ItemRadius;
            float limit = NutDropRules.HalfWidth - r;
            double land = at + NutDropRules.SpawnHeight / _fall;
            float drift = _windAmplitude > 0f ? (float)(WindDrift(land) - WindDrift(at)) : 0f;
            float lo = Math.Max(-limit, -limit + drift);
            float hi = Math.Min(limit, limit + drift);
            if (hi < lo)
            {
                lo = -limit;
                hi = limit;
            }
            bool catchable = kind != NutDropItemKind.Pinecone;
            float spanLo = lo, spanHi = hi;
            if (catchable && _hasGood)
            {
                float reach = _baseWidth + 2f * r + FairMove(land - _goodLand);
                spanLo = Math.Max(lo, _goodX - reach);
                spanHi = Math.Min(hi, _goodX + reach);
                if (spanHi < spanLo)
                {
                    spanLo = lo;
                    spanHi = hi;
                }
            }
            float age = (float)Math.Max(0.0, _elapsed - at);
            float y = NutDropRules.SpawnHeight - _fall * FallFactor * age;

            float landing = 0f;
            bool placed = false;
            for (int t = 0; t < PlaceTries && !placed; t++)
            {
                landing = spanLo + (float)_rng.NextDouble() * (spanHi - spanLo);
                placed = ClearOfPinecone(kind, landing, land) && Room(landing - drift, y);
            }
            // The fair rules could not all be kept: room alone (never an overlap).
            for (int t = 0; t < PlaceTries && !placed; t++)
            {
                landing = lo + (float)_rng.NextDouble() * (hi - lo);
                placed = Room(landing - drift, y);
            }
            if (!placed)
                return false;

            float x0 = ClampField(landing - drift);
            _active[slot] = true;
            _kind[slot] = kind;
            _x0[slot] = x0;
            _x[slot] = _windAmplitude > 0f ? ClampField(x0 + (float)(WindDrift(_elapsed) - WindDrift(at))) : x0;
            _y[slot] = y;
            _t0[slot] = at;
            _below[slot] = false;
            _side[slot] = 0;
            _spawned++;
            if (catchable)
            {
                _hasGood = true;
                _goodX = landing;
                _goodLand = land;
            }
            else
            {
                _hasPinecone = true;
                _pineconeX = landing;
                _pineconeLand = land;
            }
            Push(NutDropEventKind.Spawned, slot, kind, _x[slot], 0);
            return true;
        }

        /// <summary>
        /// A catchable item and a pinecone landing close in time: the basket can catch the nut (its center clamped by the field's
        /// sides) and be clear of the pinecone's catch zone, with a margin, after a fair move (5 u/s × the seconds between them,
        /// less a 0.12 s reaction).
        /// </summary>
        private bool ClearOfPinecone(NutDropItemKind kind, float landing, double land)
        {
            bool catchable = kind != NutDropItemKind.Pinecone;
            bool other = catchable ? _hasPinecone : _hasGood;
            if (!other)
                return true;
            double gap = Math.Abs(land - (catchable ? _pineconeLand : _goodLand));
            if (gap >= NutDropRules.PineconeClearSeconds)
                return true;
            float nut = catchable ? landing : _goodX;
            float cone = catchable ? _pineconeX : landing;
            float reach = BasketWidth * 0.5f + NutDropRules.ItemRadius;
            float limit = BasketLimit;
            float lo = Math.Max(-limit, nut - reach), hi = Math.Min(limit, nut + reach);
            float zone = reach + NutDropRules.PineconeClearMargin;
            float move = float.PositiveInfinity;
            float left = cone - zone, right = cone + zone;
            if (left >= -limit)
                move = Math.Max(0f, lo - left);
            if (right <= limit)
                move = Math.Min(move, Math.Max(0f, right - hi));
            return move <= FairMove(gap);
        }

        /// <summary>How far a fair player moves the basket between two landings this many seconds apart (after a short reaction).</summary>
        private static float FairMove(double seconds) =>
            NutDropRules.FairReachSpeed * (float)Math.Max(0.0, seconds - NutDropRules.FairReactionSeconds);

        /// <summary>No item in the air within two radii and a gap of this spot.</summary>
        private bool Room(float x, float y)
        {
            float min = 2f * NutDropRules.ItemRadius + NutDropRules.ItemGap;
            float min2 = min * min;
            for (int i = 0; i < Capacity; i++)
            {
                if (!_active[i])
                    continue;
                float dx = _x[i] - x, dy = _y[i] - y;
                if (dx * dx + dy * dy < min2)
                    return false;
            }
            return Math.Abs(x) <= NutDropRules.HalfWidth - NutDropRules.ItemRadius + 1e-4f;
        }

        private int FreeSlot()
        {
            for (int k = 0; k < Capacity; k++)
            {
                int slot = (_nextSlot + k) % Capacity;
                if (_active[slot])
                    continue;
                _nextSlot = (slot + 1) % Capacity;
                return slot;
            }
            return -1;
        }

        /// <summary>The specials of this game: each at 50 %, in a seeded order, between 15 and 45 s and 9 s apart (× 1.25 slower).</summary>
        private void ScheduleSpecials(uint seed)
        {
            var rng = new DeterministicRandom(SeedHash.Mix(seed, SpecialSalt));
            _specialCount = 0;
            _specialNext = 0;
            if (rng.NextDouble() < NutDropRules.SpecialChance)
                _specialKind[_specialCount++] = NutDropItemKind.WideBasket;
            if (rng.NextDouble() < NutDropRules.SpecialChance)
                _specialKind[_specialCount++] = NutDropItemKind.Feather;
            // Challenge 7 scores the berries alone: a shower of golden hazelnuts (worth 0 there) never falls.
            if (rng.NextDouble() < NutDropRules.SpecialChance && !_berriesOnly)
                _specialKind[_specialCount++] = NutDropItemKind.GoldenShower;
            for (int i = _specialCount - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var swap = _specialKind[i];
                _specialKind[i] = _specialKind[j];
                _specialKind[j] = swap;
            }
            double from = NutDropRules.SpecialFromSeconds * _timeScale;
            double to = NutDropRules.SpecialToSeconds * _timeScale;
            double gap = NutDropRules.SpecialGapSeconds * _timeScale;
            double previous = from - gap;
            for (int i = 0; i < _specialCount; i++)
            {
                double lo = Math.Max(from, previous + gap);
                double hi = Math.Max(lo, to - (_specialCount - 1 - i) * gap);
                _specialAt[i] = lo + rng.NextDouble() * (hi - lo);
                previous = _specialAt[i];
            }
        }

        // ---------------- helpers ----------------

        /// <summary>The wind's drift since the start (its antiderivative): x moves by Drift(t1) − Drift(t0) between two times.</summary>
        private double WindDrift(double t)
        {
            if (_windAmplitude <= 0f)
                return 0.0;
            double w = 2.0 * Math.PI / NutDropRules.WindPeriodSeconds;
            return -_windAmplitude / w * Math.Cos(w * t + _windPhase);
        }

        private static float ClampField(float x)
        {
            float limit = NutDropRules.HalfWidth - NutDropRules.ItemRadius;
            return x < -limit ? -limit : x > limit ? limit : x;
        }

        private void Push(NutDropEventKind kind, int slot, NutDropItemKind item, float x, int points) =>
            _events[PushSlot()] = new NutDropEvent(kind, slot, item, x, points, _streak, MultiplierTenths, (float)_elapsed);

        private int PushSlot()
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
            return slot;
        }

        /// <summary>A parameter; a rule set without it (another activity's rules) falls back to Nut Drop's at d.</summary>
        private static double Read(ActivityRules rules, in ActivitySetup setup, int parameter)
        {
            double value = rules.Value(parameter, setup.Difficulty, setup.Challenge);
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                var fallback = _defaultRules ??= NutDropRules.Create();
                value = fallback.Value(parameter, setup.Difficulty, -1);
            }
            return double.IsNaN(value) ? 0.0 : value;
        }

        private static double Clamp01(double value) => value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;
    }
}
