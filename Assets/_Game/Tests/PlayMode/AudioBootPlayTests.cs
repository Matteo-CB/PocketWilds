using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Feedback;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>Play mode tests never make a sound on the developer's machine (the audio still runs).</summary>
    [SetUpFixture]
    public class SilentPlayModeFixture
    {
        private float _volume;

        [OneTimeSetUp]
        public void Mute()
        {
            _volume = AudioListener.volume;
            AudioListener.volume = 0f;
        }

        [OneTimeTearDown]
        public void Restore() => AudioListener.volume = _volume <= 0f ? 1f : _volume;
    }

    /// <summary>
    /// T34B in the real game: the boot builds the audio and haptic services from DEF_AudioEvents and
    /// DEF_HapticProfiles, the refuge loads its beds (theme and ambience) through Addressables, the hatch speaks and
    /// vibrates by rarity, the haptics setting is honoured from the save.
    /// </summary>
    public class AudioBootPlayTests
    {
        private sealed class Recorder : IHapticBackend
        {
            public bool Supported => true;
            public readonly int[] Played = new int[64];
            public int Count;

            public void Play(int profile)
            {
                if (Count < Played.Length)
                    Played[Count] = profile;
                Count++;
            }

            public bool Has(HapticEventId id) => Played.Take(Math.Min(Count, Played.Length)).Contains((int)id);
        }

        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "PocketWildsPlayTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var bootstrap in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(bootstrap.gameObject);
            foreach (var splash in Object.FindObjectsByType<SplashScreen>(FindObjectsSortMode.None))
                Object.Destroy(splash.gameObject);
            try
            {
                Directory.Delete(_saveDir, true);
            }
            catch (IOException)
            {
            }
        }

        private void Seed(bool haptics, double light)
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            var rabbit = ContentId.FromSource("species.rabbit");
            for (int i = 0; i < 4; i++)
            {
                state.Collection.Add(new AnimalInstance
                {
                    Species = rabbit, Variant = ContentId.FromSource("variant.rabbit.brown"), Rarity = RarityTier.Common,
                    Seed = SeedHash.Mix((uint)i + 1u, 0x0A0A0003u),
                });
            }
            state.Collection.DiscoveredSpecies.Add(rabbit);
            state.Settings.HapticsEnabled = haptics;
            state.Wallet.Light = BigValue.FromDouble(light);
            Assert.IsTrue(service.FlushNow());
            service.Dispose();
        }

        private IEnumerator Boot(Recorder haptics)
        {
#if UNITY_EDITOR
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = _saveDir,
                ContentOverride = database.CreateBuilder().Build(),
                VisualCatalogOverride = catalog,
                LocalizationOverride = database.localization.ToArray(),
                LoadTargetScene = true,
                MuteAudio = true,
                HapticBackendOverride = haptics,
            };
            var bootScene = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>("Assets/_Game/Scenes/SC_Bootstrap.unity");
            Assert.IsNotNull(bootScene);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Scenes/SC_Bootstrap.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            float timeout = Time.realtimeSinceStartup + 25f;
            GameBootstrap bootstrap = null;
            while (bootstrap == null || bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Boot timed out.");
                bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            yield return null;
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator Boot_BuildsAudio_RefugeLoadsItsBeds_HatchSpeaksAndVibrates()
        {
            Seed(true, 1e9);
            var haptics = new Recorder();
            yield return Boot(haptics);
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            var audio = bootstrap.Audio;
            Assert.IsNotNull(audio, "The boot builds the audio service.");
            Assert.AreSame(audio, bootstrap.Context.Audio);
            Assert.Greater(audio.VoiceCount, 0, "DEF_AudioEvents is wired in SC_Bootstrap.");
            Assert.IsTrue(bootstrap.Haptics.Supported);

            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            Assert.IsNotNull(entry.BiomeAudio, "The Grassland beds load through Addressables.");
            Assert.AreSame(entry.BiomeAudio.music, audio.MusicClip, "The refuge theme plays.");
            Assert.AreSame(entry.BiomeAudio.ambience, audio.AmbienceClip, "The meadow ambience plays.");
            Assert.AreEqual("MUS_RefugeTheme", audio.MusicClip.name);
            for (int i = 0; i < 30; i++)
                yield return null;
            Assert.IsTrue(audio.MusicPlaying);

            var refuge = entry.Refuge;
            int started = audio.Started;
            refuge.RequestHatch();
            float until = Time.realtimeSinceStartup + 8f;
            while ((refuge.Hatch.IsPlaying || refuge.WaitingEgg == null) && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.Greater(audio.Started, started + 3, "Egg tap, cracks, pop, the newborn's voice.");
            Assert.IsTrue(haptics.Has(HapticEventId.TapLight), "The egg tap is felt.");
            Assert.IsTrue(haptics.Has(HapticEventId.HatchCommon) || haptics.Has(HapticEventId.Rare)
                          || haptics.Has(HapticEventId.Legendary) || haptics.Has(HapticEventId.Fabulous), "The reveal is felt by rarity.");
            Assert.AreEqual(0f, AudioListener.volume, "Muted for tests and benchmarks.");
        }

        [UnityTest]
        public IEnumerator HapticsOff_InTheSave_NothingVibrates()
        {
            Seed(false, 1e9);
            var haptics = new Recorder();
            yield return Boot(haptics);
            var refuge = Object.FindAnyObjectByType<VerticalSliceEntry>().Refuge;
            refuge.RequestHatch();
            float until = Time.realtimeSinceStartup + 8f;
            while ((refuge.Hatch.IsPlaying || refuge.WaitingEgg == null) && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.AreEqual(0, haptics.Count, "The setting turns every vibration off; the sound and the picture still tell the hatch.");
            Assert.Greater(Object.FindAnyObjectByType<GameBootstrap>().Audio.Started, 0);
        }
    }
}
