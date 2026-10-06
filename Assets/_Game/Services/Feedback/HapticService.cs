using System;
using PocketWilds.Core;

namespace PocketWilds.Services.Feedback
{
    /// <summary>The platform side of haptics: prepared once with the profiles, then plays one by index.</summary>
    public interface IHapticBackend
    {
        /// <summary>False on a device without a vibrator (or an unsupported platform): every play is silently skipped.</summary>
        bool Supported { get; }

        /// <summary>Plays the prepared pattern of this HapticEventId value.</summary>
        void Play(int profile);
    }

    /// <summary>Editor, PC and unsupported devices.</summary>
    public sealed class NoHapticBackend : IHapticBackend
    {
        public static readonly NoHapticBackend Instance = new NoHapticBackend();
        public bool Supported => false;

        public void Play(int profile)
        {
        }
    }

    /// <summary>
    /// T34B haptics (doc 08 HAPTIC DATA, gate 15). Short patterns only, with a cooldown per pattern and a short
    /// gap between any two (no vibration spam, no continuous buzz). The player's setting turns every pattern off;
    /// battery saver skips the decorative ones. The UX never depends on a vibration alone: each pattern doubles a
    /// sound or a visual.
    /// </summary>
    public sealed class HapticService : IHapticService
    {
        /// <summary>Two patterns never start closer than this (a stronger one may still replace a light one).</summary>
        public const float MinGapSeconds = 0.045f;

        private readonly HapticProfile[] _byId;
        private readonly float[] _lastPlay;
        private readonly IHapticBackend _backend;
        private float _time;
        private float _lastAny = -1000f;
        private HapticEventId _lastId;

        public HapticService(HapticProfileSet set, IHapticBackend backend)
        {
            _backend = backend ?? NoHapticBackend.Instance;
            int count = 0;
            foreach (HapticEventId value in Enum.GetValues(typeof(HapticEventId)))
                count = Math.Max(count, (int)value + 1);
            _byId = new HapticProfile[count];
            _lastPlay = new float[count];
            for (int i = 0; i < count; i++)
            {
                _byId[i] = set != null ? set.Find((HapticEventId)i) : null;
                _lastPlay[i] = -1000f;
            }
        }

        public bool Enabled { get; set; } = true;
        public bool BatterySaver { get; set; }
        public bool Supported => _backend.Supported;

        /// <summary>Patterns sent to the device, and skipped (off, unsupported, cooldown, too close) since start.</summary>
        public int Played { get; private set; }
        public int Skipped { get; private set; }

        public void Tick(float deltaTime) => _time += deltaTime;

        public void Play(HapticEventId id)
        {
            int index = (int)id;
            if (id == HapticEventId.None || index >= _byId.Length)
                return;
            var profile = _byId[index];
            if (!Enabled || !_backend.Supported || profile == null || (BatterySaver && profile.skipOnBatterySaver)
                || _time - _lastPlay[index] < profile.cooldown)
            {
                Skipped++;
                return;
            }
            // Within the gap only a stronger pattern than the last one goes through (the hatch over a tap).
            if (_time - _lastAny < MinGapSeconds && Strength(id) <= Strength(_lastId))
            {
                Skipped++;
                return;
            }
            _backend.Play(index);
            _lastPlay[index] = _time;
            _lastAny = _time;
            _lastId = id;
            Played++;
        }

        private static int Strength(HapticEventId id) => id switch
        {
            HapticEventId.Fabulous => 6,
            HapticEventId.Legendary => 5,
            HapticEventId.Rare => 4,
            HapticEventId.PurchaseSuccess => 4,
            HapticEventId.HatchCommon => 3,
            HapticEventId.Confirm => 2,
            HapticEventId.ErrorSoft => 2,
            HapticEventId.TapLight => 1,
            HapticEventId.AnimalTouch => 1,
            _ => 0,
        };
    }
}
