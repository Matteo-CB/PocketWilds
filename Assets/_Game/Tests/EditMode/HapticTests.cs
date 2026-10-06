using System;
using NUnit.Framework;
using PocketWilds.Editor.Audio;
using PocketWilds.Services.Feedback;
using UnityEditor;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T34B gate 15 (haptic review) without a phone: every pattern short and distinct, light taps, no continuous
    /// vibration, the setting turns everything off, an unsupported device stays silent, spam never becomes a buzz.
    /// The feel itself is checked on a real phone (T66).
    /// </summary>
    public class HapticTests
    {
        private sealed class Recorder : IHapticBackend
        {
            public bool Supported { get; set; } = true;
            public int Count;
            public int Last = -1;

            public void Play(int profile)
            {
                Count++;
                Last = profile;
            }
        }

        private static HapticProfileSet Set => AssetDatabase.LoadAssetAtPath<HapticProfileSet>(AudioPipeline.HapticsPath);

        [Test]
        public void EveryPattern_IsDefined_ShortAndNeverRepeating()
        {
            var set = Set;
            Assert.IsNotNull(set, "DEF_HapticProfiles (run Pocket Wilds/Audio/Build Audio and Haptics).");
            foreach (HapticEventId id in Enum.GetValues(typeof(HapticEventId)))
            {
                if (id == HapticEventId.None)
                    continue;
                var profile = set.Find(id);
                Assert.IsNotNull(profile, id + " has a pattern.");
                Assert.IsNotEmpty(profile.steps, id + " has a composed form.");
                Assert.IsNotEmpty(profile.timingsMs, id + " has a waveform fallback.");
                Assert.AreEqual(profile.timingsMs.Length, profile.amplitudes.Length, id.ToString());
                Assert.AreEqual(0, profile.timingsMs.Length % 2, id + ": off/on pairs.");
                for (int i = 0; i < profile.timingsMs.Length; i++)
                {
                    Assert.GreaterOrEqual(profile.timingsMs[i], 0L, id.ToString());
                    Assert.That(profile.amplitudes[i], Is.InRange(0, 255), id.ToString());
                    if (i % 2 == 0)
                        Assert.AreEqual(0, profile.amplitudes[i], id + ": off times are silent.");
                    else
                        Assert.Greater(profile.amplitudes[i], 0, id + ": on times vibrate.");
                }
                Assert.LessOrEqual(profile.WaveformMs, HapticProfileSet.MaxPatternMs, id + " lasts at most 300 ms.");
                Assert.LessOrEqual(profile.ComposedMs, HapticProfileSet.MaxPatternMs, id + " composed form at most 300 ms.");
                foreach (var step in profile.steps)
                    Assert.That(step.scale, Is.InRange(0.05f, 1f), id.ToString());
            }
        }

        [Test]
        public void TapIsLight_AndHatchTiersAreDistinct()
        {
            var set = Set;
            var tap = set.Find(HapticEventId.TapLight);
            Assert.LessOrEqual(tap.WaveformMs, 20, "Doc 08: TapLight 10-20 ms.");
            Assert.LessOrEqual(tap.amplitudes[1], 90, "Light.");
            Assert.LessOrEqual(set.Find(HapticEventId.AnimalTouch).amplitudes[1], tap.amplitudes[1], "Touching an animal is the lightest.");
            Assert.LessOrEqual(set.Find(HapticEventId.ErrorSoft).amplitudes[1], 90, "ErrorSoft is not aggressive.");

            int Pulses(HapticEventId id) => set.Find(id).timingsMs.Length / 2;
            int Peak(HapticEventId id)
            {
                int max = 0;
                foreach (var a in set.Find(id).amplitudes)
                    max = Math.Max(max, a);
                return max;
            }
            Assert.AreEqual(2, Pulses(HapticEventId.HatchCommon), "Two light pulses.");
            Assert.Greater(Peak(HapticEventId.Rare), Peak(HapticEventId.HatchCommon), "Rare is stronger than common.");
            Assert.Greater(Pulses(HapticEventId.Legendary), Pulses(HapticEventId.Rare), "Legendary has its own pattern.");
            Assert.GreaterOrEqual(Pulses(HapticEventId.Fabulous), 3, "Fabulous: a distinct pattern.");
            Assert.Greater(Peak(HapticEventId.Fabulous), Peak(HapticEventId.Legendary));
        }

        [Test]
        public void Setting_TurnsEveryPatternOff()
        {
            var backend = new Recorder();
            var service = new HapticService(Set, backend) { Enabled = false };
            foreach (HapticEventId id in Enum.GetValues(typeof(HapticEventId)))
            {
                service.Tick(1f);
                service.Play(id);
            }
            Assert.AreEqual(0, backend.Count);
            service.Enabled = true;
            service.Tick(1f);
            service.Play(HapticEventId.Confirm);
            Assert.AreEqual(1, backend.Count);
        }

        [Test]
        public void UnsupportedDevice_StaysSilent()
        {
            var backend = new Recorder { Supported = false };
            var service = new HapticService(Set, backend);
            service.Play(HapticEventId.Legendary);
            Assert.AreEqual(0, backend.Count);
            Assert.IsFalse(service.Supported);
            var none = new HapticService(Set, null);
            Assert.DoesNotThrow(() => none.Play(HapticEventId.Fabulous));
        }

        [Test]
        public void Spam_NeverBecomesABuzz_ButAStrongerMomentGetsThrough()
        {
            var backend = new Recorder();
            var service = new HapticService(Set, backend);
            // 200 taps in one second (60 fps, several per frame).
            for (int frame = 0; frame < 60; frame++)
            {
                service.Tick(1f / 60f);
                for (int k = 0; k < 4; k++)
                    service.Play(HapticEventId.TapLight);
            }
            Assert.LessOrEqual(backend.Count, 1f / HapticService.MinGapSeconds + 1, "Taps are spaced (no continuous vibration).");
            Assert.Greater(backend.Count, 5, "Taps still answer.");

            int before = backend.Count;
            service.Play(HapticEventId.TapLight);
            service.Play(HapticEventId.Legendary);
            Assert.AreEqual((int)HapticEventId.Legendary, backend.Last, "The hatch goes through right after a tap.");
            Assert.LessOrEqual(backend.Count - before, 2);
        }

        [Test]
        public void BatterySaver_SkipsOnlyDecorativePatterns()
        {
            var backend = new Recorder();
            var service = new HapticService(Set, backend) { BatterySaver = true };
            service.Tick(1f);
            service.Play(HapticEventId.AnimalTouch);
            Assert.AreEqual(0, backend.Count, "Touching an animal is decorative.");
            service.Tick(1f);
            service.Play(HapticEventId.Rare);
            Assert.AreEqual(1, backend.Count, "A hatch still speaks.");
        }
    }
}
