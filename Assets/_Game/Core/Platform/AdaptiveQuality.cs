#nullable enable
using System;

namespace PocketWilds.Core
{
    /// <summary>
    /// What the player picked in the settings (doc 07 « Beauté maximale », section 2): Automatique by default, or a
    /// fixed tier. Battery saving (30 FPS) is a separate switch.
    /// </summary>
    public enum QualityMode : byte
    {
        Automatic = 0,
        Low = 1,
        Medium = 2,
        High = 3,
    }

    /// <summary>The parts of the picture adaptive quality lowers one at a time, in this order (doc 07, section 5).</summary>
    public enum QualityDimension : byte
    {
        /// <summary>Butterflies, bees, birds.</summary>
        Life = 0,
        /// <summary>Grass, flowers, pebbles, secondary props, distance fade.</summary>
        Ground = 1,
        /// <summary>Rain drops, sun rays, clouds, height fog, water detail.</summary>
        Weather = 2,
        /// <summary>Real-time shadows: distance, cascades, softness.</summary>
        Shadows = 3,
        /// <summary>Render scale, anti-aliasing, LOD distance: the renderer's quality level.</summary>
        Resolution = 4,
    }

    /// <summary>Device heat, from the platform's thermal API when it has one (Android PowerManager thermal status).</summary>
    public enum ThermalLevel : byte
    {
        /// <summary>No thermal API on this platform: never acts.</summary>
        Unknown = 0,
        Nominal = 1,
        /// <summary>Warm: no raise.</summary>
        Fair = 2,
        /// <summary>Hot: one step lower now, before the device throttles.</summary>
        Serious = 3,
        /// <summary>Very hot: lowest tier and a 30 FPS cap.</summary>
        Critical = 4,
    }

    /// <summary>The tier of each dimension. Uniform at a tier means exactly that tier's quality.</summary>
    [Serializable]
    public struct QualityState : IEquatable<QualityState>
    {
        public const int DimensionCount = 5;

        public QualityTier Life;
        public QualityTier Ground;
        public QualityTier Weather;
        public QualityTier Shadows;
        public QualityTier Resolution;

        public static QualityState Uniform(QualityTier tier) => new QualityState
        {
            Life = tier, Ground = tier, Weather = tier, Shadows = tier, Resolution = tier,
        };

        public QualityTier this[QualityDimension dimension]
        {
            get => dimension switch
            {
                QualityDimension.Life => Life,
                QualityDimension.Ground => Ground,
                QualityDimension.Weather => Weather,
                QualityDimension.Shadows => Shadows,
                _ => Resolution,
            };
            set
            {
                switch (dimension)
                {
                    case QualityDimension.Life: Life = value; break;
                    case QualityDimension.Ground: Ground = value; break;
                    case QualityDimension.Weather: Weather = value; break;
                    case QualityDimension.Shadows: Shadows = value; break;
                    default: Resolution = value; break;
                }
            }
        }

        public QualityTier Highest
        {
            get
            {
                var t = Life;
                for (int i = 1; i < DimensionCount; i++)
                    if (this[(QualityDimension)i] > t)
                        t = this[(QualityDimension)i];
                return t;
            }
        }

        public QualityTier Lowest
        {
            get
            {
                var t = Life;
                for (int i = 1; i < DimensionCount; i++)
                    if (this[(QualityDimension)i] < t)
                        t = this[(QualityDimension)i];
                return t;
            }
        }

        /// <summary>
        /// One step down: the first dimension, in the doc 07 order, still at the highest tier drops one tier.
        /// Returns false when everything is already at Low.
        /// </summary>
        public bool StepDown(out QualityDimension changed)
        {
            var top = Highest;
            changed = QualityDimension.Life;
            if (top == QualityTier.Low)
                return false;
            for (int i = 0; i < DimensionCount; i++)
            {
                var d = (QualityDimension)i;
                if (this[d] == top)
                {
                    this[d] = top - 1;
                    changed = d;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// One step up, the last lowered first (reverse order), never above <paramref name="ceiling"/>.
        /// Returns false when everything is already at the ceiling.
        /// </summary>
        public bool StepUp(QualityTier ceiling, out QualityDimension changed)
        {
            var low = Lowest;
            changed = QualityDimension.Resolution;
            if (low >= ceiling)
                return false;
            for (int i = DimensionCount - 1; i >= 0; i--)
            {
                var d = (QualityDimension)i;
                if (this[d] == low)
                {
                    this[d] = low + 1;
                    changed = d;
                    return true;
                }
            }
            return false;
        }

        public bool Equals(QualityState other) =>
            Life == other.Life && Ground == other.Ground && Weather == other.Weather && Shadows == other.Shadows && Resolution == other.Resolution;

        public override bool Equals(object? obj) => obj is QualityState other && Equals(other);

        public override int GetHashCode() =>
            (int)Life | ((int)Ground << 2) | ((int)Weather << 4) | ((int)Shadows << 6) | ((int)Resolution << 8);

        public override string ToString() => $"life {Life}, ground {Ground}, weather {Weather}, shadows {Shadows}, resolution {Resolution}";
    }

    /// <summary>
    /// Frame cost over fixed windows (doc 07: 10 s): the 95th percentile of the frame's work time (CPU main thread or
    /// GPU, whichever is longer). Preallocated, no allocation per frame or per window.
    /// </summary>
    public sealed class FrameBudgetWindow
    {
        private readonly float[] _samples;
        private readonly float[] _scratch;
        private int _count;
        private float _elapsed;

        public FrameBudgetWindow(float windowSeconds = 10f, int maxSamples = 2048)
        {
            WindowSeconds = windowSeconds;
            _samples = new float[maxSamples];
            _scratch = new float[maxSamples];
        }

        public float WindowSeconds { get; }

        /// <summary>95th percentile of the last completed window (ms), 0 before the first one.</summary>
        public float LastP95 { get; private set; }

        public int LastSampleCount { get; private set; }

        public void Reset()
        {
            _count = 0;
            _elapsed = 0f;
        }

        /// <summary>Adds one frame. Returns true when a window just completed (read <see cref="LastP95"/>).</summary>
        public bool Add(float frameSeconds, float workMs)
        {
            if (_count < _samples.Length)
                _samples[_count++] = workMs;
            else
                _samples[(int)(_elapsed * 997f) % _samples.Length] = workMs; // more frames than room: keep a spread sample
            _elapsed += frameSeconds;
            if (_elapsed < WindowSeconds)
                return false;
            Array.Copy(_samples, _scratch, _count);
            int index = Math.Max(0, Math.Min(_count - 1, (int)Math.Ceiling(_count * 0.95) - 1));
            LastP95 = _count > 0 ? Select(_scratch, _count, index) : 0f;
            LastSampleCount = _count;
            Reset();
            return true;
        }

        /// <summary>The k-th smallest of the first n values (in place, Hoare quickselect): no sort, no allocation.</summary>
        private static float Select(float[] values, int n, int k)
        {
            int left = 0, right = n - 1;
            while (left < right)
            {
                float pivot = values[(left + right) >> 1];
                int i = left, j = right;
                while (i <= j)
                {
                    while (values[i] < pivot) i++;
                    while (values[j] > pivot) j--;
                    if (i <= j)
                    {
                        (values[i], values[j]) = (values[j], values[i]);
                        i++;
                        j--;
                    }
                }
                if (k <= j)
                    right = j;
                else if (k >= i)
                    left = i;
                else
                    return values[k];
            }
            return values[k];
        }
    }

    /// <summary>Tuning of <see cref="AdaptiveQualityController"/> (doc 07 values by default).</summary>
    [Serializable]
    public struct AdaptiveQualitySettings
    {
        /// <summary>Frame budget (ms): 16.67 at 60 FPS, 33.3 at 30 FPS.</summary>
        public float BudgetMs;
        /// <summary>Over budget when the window's p95 work time exceeds this share of the budget.</summary>
        public float OverShare;
        /// <summary>A window counts as stable when its p95 stays under this share of the budget.</summary>
        public float StableShare;
        /// <summary>Consecutive over-budget windows before one step down (doc 07: two).</summary>
        public int OverWindowsToLower;
        /// <summary>Stable time before one step up (doc 07: 60 s).</summary>
        public float StableSecondsToRaise;
        public float WindowSeconds;

        public static AdaptiveQualitySettings Default(int targetFrameRate) => new AdaptiveQualitySettings
        {
            BudgetMs = 1000f / Math.Max(1, targetFrameRate),
            // Work time (not the vsync-padded frame time): over 90 % of the frame, the next hiccup drops a frame.
            OverShare = 0.9f,
            StableShare = 0.65f,
            OverWindowsToLower = 2,
            StableSecondsToRaise = 60f,
            WindowSeconds = 10f,
        };
    }

    /// <summary>
    /// Adaptive quality (doc 07 « Beauté maximale », section 5; Performance Guide, « Qualité adaptative »):
    /// <list type="bullet">
    /// <item>Automatic mode starts at the device's tier. Two 10-second windows over budget in a row lower one step:
    /// life, then ground details, then weather, then shadows, then resolution; animals and readability never go
    /// first. After 60 s stable it raises one step, the last lowered first, never above the device's tier. A raise
    /// followed by a fall doubles the wait before the next raise (up to 4 min): no ping-pong.</item>
    /// <item>The device's heat acts before the frame rate does: Serious lowers one step at once and blocks raises,
    /// Critical goes to Low and caps at 30 FPS. In manual mode heat is the only thing that lowers, and the HUD says so
    /// (<see cref="ThermalNotice"/>); once cool for 60 s, the player's tier comes back.</item>
    /// </list>
    /// Pure logic, no Unity call: the scene applies <see cref="State"/>. No allocation per frame.
    /// </summary>
    public sealed class AdaptiveQualityController
    {
        private const float MaxRaiseWait = 240f;
        private const float BounceWindow = 25f;

        private readonly FrameBudgetWindow _window;
        private AdaptiveQualitySettings _settings;
        private QualityState _state;
        private int _overWindows;
        private float _stableSeconds;
        private float _raiseWait;
        private float _sinceRaise = float.MaxValue;
        private float _coolSeconds;

        public AdaptiveQualityController(QualityTier deviceTier, QualityMode mode, AdaptiveQualitySettings settings)
        {
            _settings = settings;
            _window = new FrameBudgetWindow(settings.WindowSeconds);
            DeviceTier = deviceTier;
            SetMode(mode);
        }

        public QualityTier DeviceTier { get; }
        public QualityMode Mode { get; private set; }

        /// <summary>The tier the player chose, or the device's tier in automatic mode: never exceeded.</summary>
        public QualityTier Ceiling { get; private set; }

        public QualityState State => _state;

        /// <summary>Heat lowered the quality (manual mode, or below what automatic would keep): the HUD shows a short note.</summary>
        public bool ThermalNotice { get; private set; }

        /// <summary>Critical heat: the scene caps the frame rate at 30 until the device cools down.</summary>
        public bool ThermalFrameCap { get; private set; }

        public float LastP95Ms => _window.LastP95;
        public int StepsDown { get; private set; }
        public int StepsUp { get; private set; }

        /// <summary>A new choice from the settings: starts again from that tier (automatic: the device's tier).</summary>
        public void SetMode(QualityMode mode)
        {
            Mode = mode;
            Ceiling = mode == QualityMode.Automatic ? DeviceTier : (QualityTier)(mode - 1);
            _state = QualityState.Uniform(Ceiling);
            _overWindows = 0;
            _stableSeconds = 0f;
            _raiseWait = _settings.StableSecondsToRaise;
            _sinceRaise = float.MaxValue;
            _coolSeconds = 0f;
            ThermalNotice = false;
            ThermalFrameCap = false;
            _window.Reset();
        }

        /// <summary>Benchmarks and tests: the state is put by hand; adaptive steps go on from there.</summary>
        public void Force(QualityState state)
        {
            _state = state;
            _overWindows = 0;
            _stableSeconds = 0f;
            _window.Reset();
        }

        /// <summary>The frame budget changed (battery saving switches between 60 and 30 FPS).</summary>
        public void SetBudget(float budgetMs)
        {
            _settings.BudgetMs = budgetMs;
            _window.Reset();
            _overWindows = 0;
        }

        /// <summary>
        /// One frame: its duration, its work time (ms, the longer of CPU main thread and GPU; 0 if unknown, then the
        /// frame duration is used) and the current heat. Returns true when <see cref="State"/> changed.
        /// </summary>
        public bool Tick(float frameSeconds, float workMs, ThermalLevel thermal)
        {
            bool changed = Thermal(frameSeconds, thermal);
            _sinceRaise += frameSeconds;
            float work = workMs > 0f ? workMs : frameSeconds * 1000f;
            if (!_window.Add(frameSeconds, work))
                return changed;

            float p95 = _window.LastP95;
            bool over = p95 > _settings.BudgetMs * _settings.OverShare;
            bool stable = p95 <= _settings.BudgetMs * _settings.StableShare;
            if (Mode != QualityMode.Automatic)
                return changed; // the player's choice: only heat lowers it

            if (over)
            {
                _stableSeconds = 0f;
                if (++_overWindows >= _settings.OverWindowsToLower)
                {
                    _overWindows = 0;
                    if (_sinceRaise < BounceWindow)
                        _raiseWait = Math.Min(MaxRaiseWait, _raiseWait * 2f); // the raise did not hold
                    if (_state.StepDown(out _))
                    {
                        StepsDown++;
                        changed = true;
                    }
                }
                return changed;
            }

            _overWindows = 0;
            if (!stable || thermal >= ThermalLevel.Fair)
            {
                _stableSeconds = 0f;
                return changed;
            }
            _stableSeconds += _window.WindowSeconds;
            if (_stableSeconds >= _raiseWait && _state.StepUp(Ceiling, out _))
            {
                _stableSeconds = 0f;
                _sinceRaise = 0f;
                StepsUp++;
                changed = true;
            }
            return changed;
        }

        private bool Thermal(float frameSeconds, ThermalLevel thermal)
        {
            bool changed = false;
            if (thermal >= ThermalLevel.Critical)
            {
                _coolSeconds = 0f;
                if (!ThermalFrameCap || !_state.Equals(QualityState.Uniform(QualityTier.Low)))
                {
                    changed = !_state.Equals(QualityState.Uniform(QualityTier.Low));
                    _state = QualityState.Uniform(QualityTier.Low);
                    ThermalFrameCap = true;
                    ThermalNotice = true;
                }
                return changed;
            }
            if (thermal == ThermalLevel.Serious)
            {
                _coolSeconds = 0f;
                if (!ThermalNotice)
                {
                    // Once per hot spell: one step down before the device starts throttling.
                    ThermalNotice = true;
                    if (_state.StepDown(out _))
                    {
                        StepsDown++;
                        changed = true;
                    }
                }
                return changed;
            }
            if (thermal == ThermalLevel.Fair)
            {
                _coolSeconds = 0f;
                return false;
            }
            if (!ThermalNotice && !ThermalFrameCap)
                return false;
            // Cool again: after 60 s the frame cap lifts; a manual choice comes back at once, automatic climbs back.
            _coolSeconds += frameSeconds;
            if (_coolSeconds < _settings.StableSecondsToRaise)
                return false;
            ThermalNotice = false;
            ThermalFrameCap = false;
            _coolSeconds = 0f;
            if (Mode != QualityMode.Automatic && !_state.Equals(QualityState.Uniform(Ceiling)))
            {
                _state = QualityState.Uniform(Ceiling);
                return true;
            }
            return false;
        }
    }
}
