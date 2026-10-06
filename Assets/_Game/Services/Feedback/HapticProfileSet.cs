using System;
using UnityEngine;

namespace PocketWilds.Services.Feedback
{
    /// <summary>Android haptic primitives (VibrationEffect.Composition, API 30+). Values are sent to PwHaptics.</summary>
    public enum HapticPrimitive : byte
    {
        Click = 0,
        Tick = 1,
        /// <summary>API 31+.</summary>
        LowTick = 2,
        /// <summary>API 31+.</summary>
        Thud = 3,
        QuickRise = 4,
        SlowRise = 5,
        QuickFall = 6,
    }

    [Serializable]
    public struct HapticStep
    {
        public HapticPrimitive primitive;
        [Range(0f, 1f)] public float scale;
        [Tooltip("Pause before this step, ms.")]
        [Range(0, 300)] public int delayMs;

        public HapticStep(HapticPrimitive primitive, float scale, int delayMs)
        {
            this.primitive = primitive;
            this.scale = scale;
            this.delayMs = delayMs;
        }
    }

    /// <summary>
    /// One haptic pattern (J-HAPTIC-*). Best form first: composed primitives where the phone supports them (crisp,
    /// tuned by the maker), else a waveform with amplitudes, else the same waveform on/off. Short by rule: no
    /// pattern lasts more than 300 ms, none repeats.
    /// </summary>
    [Serializable]
    public sealed class HapticProfile
    {
        public HapticEventId id;
        public HapticStep[] steps = Array.Empty<HapticStep>();
        [Tooltip("Waveform fallback: alternating off/on durations in ms, starting with an off time.")]
        public long[] timingsMs = Array.Empty<long>();
        [Tooltip("Amplitude per timing (0 to 255; 0 for the off times).")]
        public int[] amplitudes = Array.Empty<int>();
        [Tooltip("Minimum seconds between two plays of this pattern.")]
        [Min(0f)] public float cooldown = 0.05f;
        [Tooltip("Skipped in battery saver mode (decorative patterns only).")]
        public bool skipOnBatterySaver;

        /// <summary>Length of the waveform fallback in ms.</summary>
        public long WaveformMs
        {
            get
            {
                long total = 0;
                foreach (var t in timingsMs)
                    total += t;
                return total;
            }
        }

        /// <summary>Approximate length of the composed form in ms (each primitive counted at 30 ms).</summary>
        public int ComposedMs
        {
            get
            {
                int total = 0;
                foreach (var s in steps)
                    total += s.delayMs + 30;
                return total;
            }
        }
    }

    /// <summary>DEF_HapticProfiles (M-DEF-HAPTIC): one profile per HapticEventId. Built by AudioPipeline.</summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Audio/Haptic Profiles", fileName = "DEF_HapticProfiles")]
    public sealed class HapticProfileSet : ScriptableObject
    {
        public const int MaxPatternMs = 300;

        public HapticProfile[] profiles = Array.Empty<HapticProfile>();

        public HapticProfile Find(HapticEventId id)
        {
            for (int i = 0; i < profiles.Length; i++)
                if (profiles[i] != null && profiles[i].id == id)
                    return profiles[i];
            return null;
        }
    }
}
