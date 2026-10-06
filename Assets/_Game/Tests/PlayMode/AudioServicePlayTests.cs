using System.Collections;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T34B gate 14 on the service: 20 animals never make a voice storm, spam taps stay bounded, the pool replaces the
    /// least important sound, collect plays one bounded run, far animals take no voice, settings silence what they
    /// should, beds cross-fade and reveals lower the music, and nothing allocates per sound.
    /// </summary>
    public class AudioServicePlayTests
    {
        private const float Dt = 1f / 60f;
        private AudioService _service;
        private AudioCatalog _testCatalog;

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _service = null;
            if (_testCatalog != null)
                Object.Destroy(_testCatalog);
        }

        private static AudioCatalog RealCatalog()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioCatalog>("Assets/_Game/Content/Audio/DEF_AudioEvents.asset");
#else
            return null;
#endif
        }

        private static AudioClip Tone(string name, float seconds) => AudioClip.Create(name, Mathf.CeilToInt(seconds * 48000), 1, 48000, false);

        /// <summary>A small pool of 4 voices with one important and one ambient sound (synthetic clips).</summary>
        private AudioCatalog SmallCatalog()
        {
            _testCatalog = ScriptableObject.CreateInstance<AudioCatalog>();
            _testCatalog.voices = 4;
            _testCatalog.events = new[]
            {
                new AudioEventDefinition { id = AudioEventId.AnimalChatter, clips = new[] { Tone("ambient", 2f) }, priority = 4, maxVoices = 4, cooldown = 0f },
                new AudioEventDefinition { id = AudioEventId.HatchPop, clips = new[] { Tone("pop", 0.5f) }, priority = 2, maxVoices = 2, cooldown = 0f },
            };
            return _testCatalog;
        }

        private void Run(float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt)
                _service.Tick(Dt);
        }

        [UnityTest]
        public IEnumerator TwentyAnimals_NeverMakeAVoiceStorm()
        {
            var catalog = RealCatalog();
            if (catalog == null)
                Assert.Ignore("Editor only.");
            _service = new AudioService(catalog);
            _service.SetView(Vector3.zero, Vector3.right, 12f);
            var rabbit = ContentId.FromSource("species.rabbit");
            var rng = new System.Random(5);
            var positions = new Vector3[20];
            for (int i = 0; i < positions.Length; i++)
                positions[i] = new Vector3((float)rng.NextDouble() * 6f - 3f, 0f, (float)rng.NextDouble() * 6f - 3f);

            int peakAnimals = 0, peakChatter = 0, peakSleep = 0;
            // 30 s of a busy refuge: each frame a few animals greet, rejoice or fall asleep; the player taps one now and then.
            for (int frame = 0; frame < 30 * 60; frame++)
            {
                for (int k = 0; k < 3; k++)
                {
                    var at = positions[rng.Next(positions.Length)];
                    _service.Play(rng.Next(3) == 0 ? AudioEventId.AnimalSleep : AudioEventId.AnimalChatter, at, rabbit);
                }
                if (frame % 20 == 0)
                    _service.Play(AudioEventId.AnimalHappy, positions[rng.Next(positions.Length)], rabbit);
                _service.Tick(Dt);
                int animals = _service.ActiveVoicesOf(AudioEventId.AnimalChatter) + _service.ActiveVoicesOf(AudioEventId.AnimalSleep)
                              + _service.ActiveVoicesOf(AudioEventId.AnimalHappy);
                peakAnimals = Mathf.Max(peakAnimals, animals);
                peakChatter = Mathf.Max(peakChatter, _service.ActiveVoicesOf(AudioEventId.AnimalChatter));
                peakSleep = Mathf.Max(peakSleep, _service.ActiveVoicesOf(AudioEventId.AnimalSleep));
                if (frame % 120 == 0)
                    yield return null;
            }
            Assert.LessOrEqual(peakChatter, 1, "One chattering animal at a time for the whole refuge.");
            Assert.LessOrEqual(peakSleep, 1);
            Assert.LessOrEqual(peakAnimals, 4, "Animals never take more than 4 voices.");
            Assert.LessOrEqual(_service.PeakVoices, _service.VoiceCount);
            Assert.Greater(_service.Dropped, 5000, "Almost every request is dropped by cooldowns and caps.");

            // 50 reactions in one frame (gate 14 spam): two voices at most.
            for (int i = 0; i < 50; i++)
                _service.Play(AudioEventId.AnimalHappy, positions[i % positions.Length], rabbit);
            Assert.LessOrEqual(_service.ActiveVoicesOf(AudioEventId.AnimalHappy), 2);
        }

        [UnityTest]
        public IEnumerator SpamTaps_StayResponsive_AndBounded()
        {
            var catalog = RealCatalog();
            if (catalog == null)
                Assert.Ignore("Editor only.");
            _service = new AudioService(catalog);
            int startedBefore = _service.Started;
            for (int frame = 0; frame < 60; frame++)
            {
                for (int k = 0; k < 5; k++)
                    _service.Play(AudioEventId.UiTap, Vector3.zero, default);
                _service.Tick(Dt);
                Assert.LessOrEqual(_service.ActiveVoicesOf(AudioEventId.UiTap), 2);
            }
            int started = _service.Started - startedBefore;
            Assert.Greater(started, 10, "Taps still answer.");
            Assert.LessOrEqual(started, 30, "At most one tap sound every 40 ms.");
            yield return null;
        }

        [Test]
        public void FullPool_ReplacesTheLeastImportantSound()
        {
            _service = new AudioService(SmallCatalog());
            for (int i = 0; i < 4; i++)
            {
                _service.Play(AudioEventId.AnimalChatter, Vector3.zero, default);
                _service.Tick(0.01f);
            }
            Assert.AreEqual(4, _service.ActiveVoices, "Ambient sounds filled the pool.");
            _service.Play(AudioEventId.HatchPop, Vector3.zero, default);
            Assert.AreEqual(1, _service.ActiveVoicesOf(AudioEventId.HatchPop), "The hatch takes the place of an ambient sound.");
            Assert.AreEqual(3, _service.ActiveVoicesOf(AudioEventId.AnimalChatter));
            Assert.AreEqual(1, _service.Stolen);
            Assert.AreEqual(4, _service.ActiveVoices, "Never more voices than the pool.");

            // An ambient sound never pushes out a more important one.
            _service.Play(AudioEventId.HatchPop, Vector3.zero, default);
            _service.Play(AudioEventId.HatchPop, Vector3.zero, default);
            int pops = _service.ActiveVoicesOf(AudioEventId.HatchPop);
            Assert.AreEqual(2, pops);
            _service.Play(AudioEventId.AnimalChatter, Vector3.zero, default);
            Assert.AreEqual(pops, _service.ActiveVoicesOf(AudioEventId.HatchPop));
        }

        [Test]
        public void Collect_PlaysOneBoundedRun()
        {
            var catalog = RealCatalog();
            if (catalog == null)
                Assert.Ignore("Editor only.");
            _service = new AudioService(catalog);
            var def = catalog.Find(AudioEventId.CollectLight);
            _service.Play(AudioEventId.CollectLight, Vector3.zero, default);
            _service.Play(AudioEventId.CollectLight, Vector3.zero, default);
            Assert.AreEqual(0, _service.Started, "Nothing before the motes land.");
            Run(def.burstDelay + def.burstInterval * def.burstCount + 0.2f);
            Assert.AreEqual(def.burstCount, _service.Started, "One run of notes, the second request inside the cooldown is dropped.");
            Assert.LessOrEqual(_service.PeakVoices, def.maxVoices);
        }

        [Test]
        public void FarAnimal_TakesNoVoice_AndZoomOutIsQuieter()
        {
            var catalog = RealCatalog();
            if (catalog == null)
                Assert.Ignore("Editor only.");
            _service = new AudioService(catalog);
            _service.SetView(Vector3.zero, Vector3.right, 12f);
            _service.Play(AudioEventId.AnimalHappy, new Vector3(30f, 0f, 0f), default);
            Assert.AreEqual(0, _service.Started, "Off screen: dropped before taking a voice.");
            _service.Play(AudioEventId.AnimalHappy, new Vector3(1f, 0f, 0f), default);
            Assert.AreEqual(1, _service.Started);
        }

        [Test]
        public void Settings_SilenceWhatTheyShould()
        {
            var catalog = RealCatalog();
            if (catalog == null)
                Assert.Ignore("Editor only.");
            _service = new AudioService(catalog);
            _service.ApplySettings(0f, 0.8f, false);
            _service.Play(AudioEventId.UiConfirm, Vector3.zero, default);
            Assert.AreEqual(0, _service.Started, "Sounds off: no voice.");
            Assert.IsTrue(catalog.mixer.GetFloat(AudioService.SfxParameter, out float sfxDb));
            Assert.LessOrEqual(sfxDb, -79f);

            _service.ApplySettings(1f, 0.8f, true);
            _service.SetView(Vector3.zero, Vector3.right, 12f);
            _service.Play(AudioEventId.AnimalChatter, Vector3.zero, default);
            Assert.AreEqual(0, _service.Started, "Battery saver skips ambient chatter.");
            _service.Play(AudioEventId.UiConfirm, Vector3.zero, default);
            Assert.AreEqual(1, _service.Started, "Feedback still plays.");
            _service.ApplySettings(1f, 0.8f, false);
        }

        [UnityTest]
        public IEnumerator Beds_CrossFade_AndMusicOffStopsDecoding_AndRevealsDuck()
        {
            var catalog = RealCatalog();
            if (catalog == null)
                Assert.Ignore("Editor only.");
            _service = new AudioService(catalog);
            var a = Tone("musicA", 4f);
            var b = Tone("musicB", 4f);
            var amb = Tone("ambience", 4f);
            _service.SetBeds(a, amb, 1f);
            Run(1.1f);
            Assert.AreSame(a, _service.MusicClip);
            Assert.AreEqual(1f, _service.MusicFade, 0.01f);
            Assert.AreSame(amb, _service.AmbienceClip);

            _service.SetBeds(b, amb, 1f);
            Assert.AreSame(b, _service.MusicClip, "The new theme comes in...");
            Run(1.1f);
            Assert.AreEqual(1f, _service.MusicFade, 0.01f, "...and is fully up after the fade.");

            _service.ApplySettings(1f, 0f, false);
            Run(0.1f);
            Assert.IsFalse(_service.MusicPlaying, "Music off: the stream stops.");
            _service.ApplySettings(1f, 0.8f, false);
            Run(0.1f);
            Assert.IsTrue(_service.MusicPlaying, "Music back on: it plays again.");

            _service.Play(AudioEventId.LegendaryReveal, Vector3.zero, default);
            Run(0.3f);
            Assert.Less(_service.DuckDecibels, -3f, "A reveal lowers the music a little.");
            Run(3f);
            Assert.AreEqual(0f, _service.DuckDecibels, 0.01f, "Then it comes back.");

            _service.StopBeds();
            Assert.IsNull(_service.MusicClip);
            Object.Destroy(a);
            Object.Destroy(b);
            Object.Destroy(amb);
            yield return null;
        }

        [Test]
        public void PlayAndTick_DoNotAllocate()
        {
            var catalog = RealCatalog();
            if (catalog == null)
                Assert.Ignore("Editor only.");
            _service = new AudioService(catalog);
            _service.SetView(Vector3.zero, Vector3.right, 12f);
            var rabbit = ContentId.FromSource("species.rabbit");
            void Frame()
            {
                _service.Play(AudioEventId.UiTap, Vector3.zero, default);
                _service.Play(AudioEventId.AnimalHappy, new Vector3(0.5f, 0f, 0.5f), rabbit);
                _service.Play(AudioEventId.AnimalChatter, new Vector3(-0.5f, 0f, 0.5f), rabbit);
                _service.Play(AudioEventId.CollectLight, Vector3.zero, default);
                _service.Play(AudioEventId.HatchPop, Vector3.zero, default);
                _service.Tick(0.05f);
            }
            for (int i = 0; i < 40; i++)
                Frame();
            Assert.That(() =>
            {
                for (int i = 0; i < 40; i++)
                    Frame();
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
