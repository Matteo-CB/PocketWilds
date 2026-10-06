using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Audio;
using PocketWilds.Services.Feedback;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.Audio;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T34B gate 14 (audio review) on the assets: every sound defined and routed, import settings by category,
    /// sources traceable, loops seamless and exact, beds streamed through their Addressables group, every species
    /// with a voice, no ambient sound able to become a chorus.
    /// </summary>
    public class AudioAssetTests
    {
        private static string Root => Directory.GetParent(Application.dataPath).FullName;

        private static AudioCatalog Catalog => AssetDatabase.LoadAssetAtPath<AudioCatalog>(AudioPipeline.CatalogPath);

        [Test]
        public void EverySound_IsDefined_WithClips_AndRoutedToItsMixerGroup()
        {
            var catalog = Catalog;
            Assert.IsNotNull(catalog, "DEF_AudioEvents (run Pocket Wilds/Audio/Build Audio and Haptics).");
            Assert.IsNotNull(catalog.mixer, "AM_Game mixer.");
            foreach (AudioEventId id in Enum.GetValues(typeof(AudioEventId)))
            {
                if (id == AudioEventId.None)
                    continue;
                var def = catalog.Find(id);
                Assert.IsNotNull(def, id + " has a definition.");
                Assert.IsNotEmpty(def.clips, id + " has clips.");
                Assert.IsTrue(def.clips.All(c => c != null), id + " has no missing clip.");
                var group = catalog.Group(def.group);
                Assert.IsNotNull(group, id + " routes to a mixer group.");
                Assert.AreEqual(def.group.ToString(), group.name);
                Assert.AreSame(catalog.mixer, group.audioMixer);
                Assert.GreaterOrEqual(def.maxVoices, 1);
                Assert.Less(def.maxVoices, catalog.voices, id + " can never hold the whole pool.");
            }
        }

        [Test]
        public void Mixer_HasTheDocGroups_AndExposesThePlayerFaders()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioPipeline.MixerPath);
            Assert.IsNotNull(mixer);
            foreach (var path in new[] { "Master/Music", "Master/Ambience", "Master/Sfx/Animals", "Master/Sfx/UI", "Master/Sfx/Rewards", "Master/Sfx/World" })
                Assert.AreEqual(1, mixer.FindMatchingGroups(path).Length, path);
            foreach (var parameter in new[] { AudioService.MusicParameter, AudioService.AmbienceParameter, AudioService.SfxParameter })
                Assert.IsTrue(mixer.GetFloat(parameter, out _), parameter + " is exposed.");
        }

        [Test]
        public void ImportSettings_FollowTheCategory()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioPipeline.SfxFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.IsTrue(importer.forceToMono, path + " is mono (UI and small voices).");
                Assert.AreEqual(1, clip.channels, path);
                Assert.AreEqual(AudioClipLoadType.CompressedInMemory, settings.loadType, path);
                Assert.IsTrue(settings.preloadAudioData, path + " is ready before its first play.");
                Assert.AreEqual(clip.length <= AudioPipeline.AdpcmMaxSeconds ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.Vorbis,
                    settings.compressionFormat, path);
                Assert.LessOrEqual(clip.length, 3.2f, path + " is a short effect.");
            }
            foreach (var path in new[] { AudioPipeline.MusicClip, AudioPipeline.AmbienceClip })
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                Assert.AreEqual(AudioClipLoadType.Streaming, settings.loadType, path + " streams.");
                Assert.AreEqual(AudioCompressionFormat.Vorbis, settings.compressionFormat, path);
                Assert.IsFalse(settings.preloadAudioData, path);
                Assert.AreEqual(2, AssetDatabase.LoadAssetAtPath<AudioClip>(path).channels, path + " is stereo.");
            }
        }

        [Test]
        public void Loops_AreExact_AndSeamless()
        {
            // The theme is 64 bars at 80 BPM: 192 s, a whole number of samples at 48 and 44.1 kHz.
            var music = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPipeline.MusicClip);
            Assert.AreEqual(192f, music.length, 0.001f);
            Assert.AreEqual(192 * music.frequency, music.samples, "The loop point is a whole sample.");
            var ambience = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPipeline.AmbienceClip);
            Assert.AreEqual(75f, ambience.length, 0.001f);
            Assert.GreaterOrEqual(ambience.length, 45f, "Doc 08: ambience loops of 45 to 120 s.");
            Assert.GreaterOrEqual(music.length, 150f, "Doc 08: theme of 2.5 to 4 min.");
            foreach (var path in new[] { AudioPipeline.MusicClip, AudioPipeline.AmbienceClip })
            {
                var (samples, channels) = ReadPcm16(Path.Combine(Root, path));
                int frames = samples.Length / channels;
                for (int c = 0; c < channels; c++)
                {
                    double sum = 0.0;
                    for (int i = 1; i < frames; i++)
                        sum += Math.Abs(samples[i * channels + c] - samples[(i - 1) * channels + c]);
                    double meanStep = sum / (frames - 1);
                    double seam = Math.Abs(samples[c] - samples[(frames - 1) * channels + c]);
                    Assert.Less(seam, meanStep * 4.0, $"{path} channel {c}: the end joins the start (seam {seam:F5}, mean step {meanStep:F5}).");
                }
            }
        }

        [Test]
        public void EveryClip_HasItsMasterAndSource()
        {
            Assert.IsTrue(File.Exists(Path.Combine(Root, "SourceArt/Audio/Tools/pw_audio_build.py")), "The clips are rebuilt from code.");
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioPipeline.AudioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string folder = Path.GetFileName(Path.GetDirectoryName(path));
                string master = Path.Combine(Root, "SourceArt/Audio/Masters", folder, Path.GetFileName(path));
                Assert.IsTrue(File.Exists(master), path + " has its 24-bit master in SourceArt/Audio/Masters.");
            }
        }

        [Test]
        public void Beds_AreInTheirAddressablesGroup()
        {
            var beds = AssetDatabase.LoadAssetAtPath<BiomeAudioSet>(AudioPipeline.GrasslandBedsPath);
            Assert.IsNotNull(beds);
            Assert.AreEqual(AudioPipeline.MusicClip, AssetDatabase.GetAssetPath(beds.music));
            Assert.AreEqual(AudioPipeline.AmbienceClip, AssetDatabase.GetAssetPath(beds.ambience));
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(AudioPipeline.GrasslandBedsPath));
            Assert.IsNotNull(entry, "DEF_BiomeAudio_Grassland is addressable.");
            Assert.AreEqual(AudioPipeline.GrasslandAddress, entry.address);
            Assert.AreEqual(AudioPipeline.GrasslandGroup, entry.parentGroup.Name);
        }

        [Test]
        public void EverySpecies_HasAVoice()
        {
            var catalog = Catalog;
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var registry = database.CreateBuilder().Build();
            int checkedSpecies = 0;
            for (int i = 0; i < registry.Species.Count; i++)
            {
                var species = registry.Species[i];
                var voice = catalog.FindSpecies(species.Id);
                Assert.IsNotNull(voice, species.Id + " has a voice.");
                Assert.IsNotEmpty(voice.happy, species.Id + " happy clips.");
                Assert.IsNotEmpty(voice.sleep, species.Id + " sleep clips.");
                checkedSpecies++;
            }
            Assert.GreaterOrEqual(checkedSpecies, 3);
            Assert.IsTrue(catalog.species.Any(v => ContentId.FromSource(v.species) == ContentId.FromSource("species.rabbit") && v.happy[0].name.Contains("Rabbit")),
                "The rabbit has its signature voice.");
        }

        [Test]
        public void AmbientSounds_CannotBecomeAChorus()
        {
            var catalog = Catalog;
            foreach (var def in catalog.events.Where(d => d.priority >= 4))
            {
                Assert.AreEqual(1, def.maxVoices, def.id + ": one voice for the whole refuge.");
                Assert.GreaterOrEqual(def.cooldown, 2f, def.id + ": rare.");
                Assert.IsTrue(def.world, def.id + ": only near the view.");
                Assert.IsTrue(def.skipOnBatterySaver, def.id + ": decorative, skipped in battery saver.");
            }
            var collect = catalog.Find(AudioEventId.CollectLight);
            Assert.LessOrEqual(collect.burstCount, 8, "Collect plays a bounded run, never one sound per mote.");
            Assert.Greater(collect.burstCount, 0);
        }

        private static (short[] samples, int channels) ReadPcm16(string path)
        {
            var bytes = File.ReadAllBytes(path);
            int channels = 0, bits = 0, dataOffset = -1, dataLength = 0;
            for (int pos = 12; pos + 8 <= bytes.Length;)
            {
                string id = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                int size = BitConverter.ToInt32(bytes, pos + 4);
                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(bytes, pos + 10);
                    bits = BitConverter.ToInt16(bytes, pos + 22);
                }
                else if (id == "data")
                {
                    dataOffset = pos + 8;
                    dataLength = size;
                    break;
                }
                pos += 8 + size + (size & 1);
            }
            Assert.AreEqual(16, bits, path + " runtime clip is 16-bit PCM.");
            Assert.Greater(dataOffset, 0);
            var samples = new short[dataLength / 2];
            Buffer.BlockCopy(bytes, dataOffset, samples, 0, samples.Length * 2);
            return (samples, channels);
        }
    }
}
