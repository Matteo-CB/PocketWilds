using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PocketWilds.Bootstrap;
using PocketWilds.Services.Feedback;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Audio
{
    /// <summary>
    /// T34B audio and haptics, all from code (doc 08, doc 11):
    /// - import settings of every clip (SFX mono ADPCM or Vorbis in memory, beds streamed Vorbis);
    /// - AM_Game mixer: Master > Music, Ambience, Sfx > Animals, UI, Rewards, World; exposed MusicVolume,
    ///   AmbienceVolume, SfxVolume;
    /// - DEF_AudioEvents (every AudioEventId, species voices), DEF_HapticProfiles (every HapticEventId);
    /// - DEF_BiomeAudio_Grassland in the Addressables group Audio_Grassland (address audio.grassland);
    /// - SC_Bootstrap wired.
    /// Clips come from SourceArt/Audio/Tools/pw_audio_build.py (render first). Idempotent.
    /// </summary>
    public static class AudioPipeline
    {
        public const string AudioFolder = "Assets/_Game/Art/Audio";
        public const string SfxFolder = AudioFolder + "/SFX";
        public const string MusicFolder = AudioFolder + "/Music";
        public const string AmbienceFolder = AudioFolder + "/Ambience";
        public const string MixerPath = AudioFolder + "/Mixers/AM_Game.mixer";
        public const string DefinitionsFolder = "Assets/_Game/Content/Audio";
        public const string CatalogPath = DefinitionsFolder + "/DEF_AudioEvents.asset";
        public const string HapticsPath = DefinitionsFolder + "/DEF_HapticProfiles.asset";
        public const string GrasslandBedsPath = DefinitionsFolder + "/DEF_BiomeAudio_Grassland.asset";
        public const string GrasslandGroup = "Audio_Grassland";
        public const string GrasslandAddress = "audio.grassland";
        public const string MusicClip = MusicFolder + "/MUS_RefugeTheme.wav";
        public const string AmbienceClip = AmbienceFolder + "/AMB_Grassland_Day.wav";
        public const string EveningClip = AmbienceFolder + "/AMB_Grassland_Evening.wav";
        public const string RainClip = AmbienceFolder + "/AMB_Rain_Light.wav";
        public const string BootstrapScenePath = "Assets/_Game/Scenes/SC_Bootstrap.unity";

        /// <summary>Mixer groups in AudioGroup order, with their parent.</summary>
        public static readonly (string name, string parent)[] Groups =
        {
            ("Music", "Master"), ("Ambience", "Master"), ("Animals", "Sfx"), ("UI", "Sfx"), ("Rewards", "Sfx"), ("World", "Sfx"),
        };

        /// <summary>A short clip played often is kept as ADPCM (cheap to decode); a long cue as Vorbis.</summary>
        public const float AdpcmMaxSeconds = 1.2f;

        [MenuItem("Pocket Wilds/Audio/Build Audio and Haptics (T34B)", priority = 200)]
        public static void BuildMenu() => Debug.Log("[Audio] " + BuildAll());

        public static string BuildAll()
        {
            Directory.CreateDirectory(DefinitionsFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(MixerPath));
            AssetDatabase.Refresh();
            int clips = ConfigureImporters();
            var mixer = EnsureMixer();
            var catalog = EnsureCatalog(mixer);
            var haptics = EnsureHaptics();
            EnsureGrasslandBeds();
            WireBootstrap(catalog, haptics);
            AssetDatabase.SaveAssets();
            return $"{clips} clips imported, {catalog.events.Length} sounds, {catalog.species.Length} species voices, " +
                   $"{haptics.profiles.Length} haptic patterns, beds in {GrasslandGroup}.";
        }

        // ---------------------------------------------------------------- import settings

        public static int ConfigureImporters()
        {
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                    continue;
                var settings = importer.defaultSampleSettings;
                if (path.StartsWith(MusicFolder, StringComparison.Ordinal) || path.StartsWith(AmbienceFolder, StringComparison.Ordinal))
                {
                    bool music = path.StartsWith(MusicFolder, StringComparison.Ordinal);
                    importer.forceToMono = false;
                    importer.loadInBackground = true;
                    settings.preloadAudioData = false;
                    settings.loadType = AudioClipLoadType.Streaming;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    // The theme keeps its air; the ambience has nothing above 9 kHz, 32 kHz is enough.
                    settings.quality = music ? 0.5f : 0.35f;
                    settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                    settings.sampleRateOverride = music ? 44100u : 32000u;
                }
                else
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    bool shortClip = clip == null || clip.length <= AdpcmMaxSeconds;
                    importer.forceToMono = true;
                    importer.loadInBackground = false;
                    settings.preloadAudioData = true;
                    settings.loadType = AudioClipLoadType.CompressedInMemory;
                    settings.compressionFormat = shortClip ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.Vorbis;
                    settings.quality = 0.6f;
                    settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
                }
                importer.defaultSampleSettings = settings;
                importer.ClearSampleSettingOverride("Android");
                importer.ClearSampleSettingOverride("iOS");
                importer.SaveAndReimport();
                count++;
            }
            return count;
        }

        // ---------------------------------------------------------------- mixer (internal editor API, by reflection)

        private static readonly Assembly EditorAssembly = typeof(UnityEditor.Editor).Assembly;
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        public static AudioMixer EnsureMixer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null && Groups.All(g => existing.FindMatchingGroups(g.name).Any(m => m.name == g.name)))
                return existing;
            if (existing != null)
                AssetDatabase.DeleteAsset(MixerPath);

            var controllerType = EditorAssembly.GetType("UnityEditor.Audio.AudioMixerController", true);
            var groupType = EditorAssembly.GetType("UnityEditor.Audio.AudioMixerGroupController", true);
            var pathType = EditorAssembly.GetType("UnityEditor.Audio.AudioGroupParameterPath", true);
            var controller = controllerType.GetMethod("CreateMixerControllerAtPath", Any).Invoke(null, new object[] { MixerPath });
            var master = controllerType.GetProperty("masterGroup", Any).GetValue(controller);
            var create = controllerType.GetMethod("CreateNewGroup", Any);
            var addChild = controllerType.GetMethod("AddChildToParent", Any);
            var byName = new Dictionary<string, object> { ["Master"] = master };

            object NewGroup(string name, string parent)
            {
                var group = create.Invoke(controller, new object[] { name, false });
                addChild.Invoke(controller, new[] { group, byName[parent] });
                byName[name] = group;
                return group;
            }

            NewGroup("Sfx", "Master");
            foreach (var (name, parent) in Groups)
                NewGroup(name, parent);

            // Expose the faders the player's settings drive.
            var addExposed = controllerType.GetMethod("AddExposedParameter", Any);
            var guidOfVolume = groupType.GetMethod("GetGUIDForVolume", Any);
            var exposedProperty = controllerType.GetProperty("exposedParameters", Any);
            var names = new Dictionary<GUID, string>();
            foreach (var (groupName, parameter) in new[]
                     {
                         ("Music", AudioService.MusicParameter), ("Ambience", AudioService.AmbienceParameter), ("Sfx", AudioService.SfxParameter),
                     })
            {
                var guid = (GUID)guidOfVolume.Invoke(byName[groupName], null);
                var parameterPath = Activator.CreateInstance(pathType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                    new[] { byName[groupName], (object)guid }, null);
                addExposed.Invoke(controller, new[] { parameterPath });
                names[guid] = parameter;
            }
            var exposed = (Array)exposedProperty.GetValue(controller);
            var elementType = exposed.GetType().GetElementType();
            var guidField = elementType.GetField("guid", Any);
            var nameField = elementType.GetField("name", Any);
            for (int i = 0; i < exposed.Length; i++)
            {
                var item = exposed.GetValue(i);
                if (names.TryGetValue((GUID)guidField.GetValue(item), out var parameterName))
                {
                    nameField.SetValue(item, parameterName);
                    exposed.SetValue(item, i);
                }
            }
            exposedProperty.SetValue(controller, exposed);
            EditorUtility.SetDirty((Object)controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        // ---------------------------------------------------------------- definitions

        private static AudioClip[] Clips(string prefix)
        {
            var clips = AssetDatabase.FindAssets(prefix + " t:AudioClip", new[] { SfxFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith(prefix + "_", StringComparison.Ordinal))
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .ToArray();
            if (clips.Length == 0)
                throw new InvalidOperationException("No clip " + prefix + "_NN in " + SfxFolder + ": run SourceArt/Audio/Tools/pw_audio_build.py sfx.");
            return clips;
        }

        private static AudioEventDefinition Def(AudioEventId id, string prefix, AudioGroup group, int priority, float cooldown, int maxVoices,
            float volume = 1f, float volumeJitter = 0.05f, float pitchJitter = 0.02f)
        {
            return new AudioEventDefinition
            {
                id = id,
                clips = Clips(prefix),
                group = group,
                priority = priority,
                cooldown = cooldown,
                maxVoices = maxVoices,
                volume = volume,
                volumeJitter = volumeJitter,
                pitchJitter = pitchJitter,
            };
        }

        /// <summary>Every sound of the game (doc 08 values: tap pitch +/-3 %, egg tap 2 voices, etc.).</summary>
        public static AudioEventDefinition[] Events()
        {
            var events = new List<AudioEventDefinition>
            {
                Def(AudioEventId.EggTap, "SFX_Egg_Tap", AudioGroup.Rewards, 2, 0.06f, 2, pitchJitter: 0.03f),
                Def(AudioEventId.Crack1, "SFX_Egg_Crack", AudioGroup.Rewards, 2, 0.05f, 2, pitchJitter: 0.03f),
                Def(AudioEventId.Crack2, "SFX_Egg_Crack", AudioGroup.Rewards, 2, 0.05f, 2, pitchJitter: 0.03f),
                Def(AudioEventId.HatchPop, "SFX_Hatch_Pop", AudioGroup.Rewards, 2, 0.1f, 2, volumeJitter: 0.03f),
                Def(AudioEventId.MasteryLevelUp, "SFX_Reward_MasteryUp", AudioGroup.Rewards, 2, 0.3f, 1, volumeJitter: 0f, pitchJitter: 0f),
                Def(AudioEventId.PanelOpen, "SFX_UI_PanelOpen", AudioGroup.UI, 1, 0.08f, 1, pitchJitter: 0.03f),
                Def(AudioEventId.ErrorSoft, "SFX_UI_ErrorSoft", AudioGroup.UI, 1, 0.35f, 1),
                Def(AudioEventId.UiTap, "SFX_UI_Tap", AudioGroup.UI, 1, 0.04f, 2, volumeJitter: 0.08f, pitchJitter: 0.04f),
                Def(AudioEventId.UiConfirm, "SFX_UI_Confirm", AudioGroup.UI, 1, 0.06f, 1),
                Def(AudioEventId.UiBack, "SFX_UI_Back", AudioGroup.UI, 1, 0.06f, 1),
                // T34B2
                Def(AudioEventId.PanelClose, "SFX_UI_PanelClose", AudioGroup.UI, 1, 0.08f, 1, pitchJitter: 0.03f),
                Def(AudioEventId.UiTab, "SFX_UI_Tab", AudioGroup.UI, 1, 0.05f, 2, volumeJitter: 0.08f, pitchJitter: 0.04f),
                Def(AudioEventId.ToggleOn, "SFX_UI_ToggleOn", AudioGroup.UI, 1, 0.08f, 1),
                Def(AudioEventId.ToggleOff, "SFX_UI_ToggleOff", AudioGroup.UI, 1, 0.08f, 1),
                Def(AudioEventId.PurchasePending, "SFX_UI_PurchasePending", AudioGroup.UI, 1, 1f, 1, volumeJitter: 0f, pitchJitter: 0f),
                Def(AudioEventId.SpendLight, "SFX_Currency_SpendLight", AudioGroup.Rewards, 2, 0.1f, 1, 0.9f, pitchJitter: 0.02f),
                Def(AudioEventId.Claim, "SFX_Reward_Claim", AudioGroup.Rewards, 2, 0.5f, 1, volumeJitter: 0f, pitchJitter: 0f),
                // T35A: upgrades (a run of purchases plays at most one sound every 0.15 s), zones, refuge level, biome mastery.
                Def(AudioEventId.UpgradeBuy, "SFX_Upgrade_Buy", AudioGroup.Rewards, 2, 0.15f, 1, pitchJitter: 0.01f),
                Def(AudioEventId.UpgradeMilestone, "SFX_Upgrade_Milestone", AudioGroup.Rewards, 2, 0.4f, 1, volumeJitter: 0f, pitchJitter: 0f),
                Def(AudioEventId.ZoneOpen, "SFX_Reward_ZoneOpen", AudioGroup.Rewards, 2, 1f, 1, volumeJitter: 0f, pitchJitter: 0f),
                Def(AudioEventId.RefugeLevelUp, "SFX_Reward_RefugeLevel", AudioGroup.Rewards, 2, 1f, 1, volumeJitter: 0f, pitchJitter: 0f),
                Def(AudioEventId.BiomeMasteryUp, "SFX_Reward_BiomeMastery", AudioGroup.Rewards, 2, 0.5f, 1, volumeJitter: 0f, pitchJitter: 0f),
            };
            foreach (var (id, prefix) in new[]
                     {
                         (AudioEventId.RareReveal, "SFX_Reveal_Rare"), (AudioEventId.EpicReveal, "SFX_Reveal_Epic"),
                         (AudioEventId.LegendaryReveal, "SFX_Reveal_Legendary"), (AudioEventId.FabulousReveal, "SFX_Reveal_Fabulous"),
                     })
            {
                var reveal = Def(id, prefix, AudioGroup.Rewards, 2, 0.3f, 1, volumeJitter: 0f, pitchJitter: 0f);
                reveal.duckMusic = true;
                events.Add(reveal);
            }
            var purchase = Def(AudioEventId.PurchaseComplete, "SFX_UI_PurchaseSuccess", AudioGroup.UI, 1, 0.3f, 1, volumeJitter: 0f, pitchJitter: 0f);
            purchase.duckMusic = true;
            events.Add(purchase);

            // Collect: one run of five rising notes as the motes land in the Light chip (CollectMotes: 0.62 s flight).
            var collect = Def(AudioEventId.CollectLight, "SFX_Collect_Light", AudioGroup.Rewards, 2, 0.3f, 4, 0.8f, pitchJitter: 0.01f);
            collect.burstCount = 5;
            collect.burstDelay = 0.62f;
            collect.burstInterval = 0.1f;
            collect.burstSemitones = new[] { 0, 2, 4, 7, 9 };
            events.Add(collect);

            // Animals: the touched one answers at once; chatter and sleep breaths are rare for the whole refuge.
            var happy = Def(AudioEventId.AnimalHappy, "SFX_Animal_Small_Chirp", AudioGroup.Animals, 3, 0.12f, 2, 0.9f, 0.08f, 0.05f);
            happy.world = true;
            events.Add(happy);
            var chatter = Def(AudioEventId.AnimalChatter, "SFX_Animal_Small_Chirp", AudioGroup.Animals, 4, 3f, 1, 0.55f, 0.1f, 0.06f);
            chatter.world = true;
            chatter.skipOnBatterySaver = true;
            events.Add(chatter);
            var sleep = Def(AudioEventId.AnimalSleep, "SFX_Animal_Small_Sleep", AudioGroup.Animals, 5, 6f, 1, 0.8f, 0.1f, 0.04f);
            sleep.world = true;
            sleep.skipOnBatterySaver = true;
            events.Add(sleep);
            // T34B2: nibbles of a near animal eating, and small birds flying off; both rare for the whole refuge.
            var eat = Def(AudioEventId.AnimalEat, "SFX_Animal_Small_Eat", AudioGroup.Animals, 5, 4f, 1, 0.8f, 0.1f, 0.05f);
            eat.world = true;
            eat.skipOnBatterySaver = true;
            events.Add(eat);
            var birds = Def(AudioEventId.BirdTakeOff, "SFX_World_BirdTakeOff", AudioGroup.World, 5, 2.5f, 1, 0.8f, 0.1f, 0.06f);
            birds.world = true;
            birds.skipOnBatterySaver = true;
            events.Add(birds);
            return events.OrderBy(e => (int)e.id).ToArray();
        }

        public static AudioCatalog EnsureCatalog(AudioMixer mixer)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AudioCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.mixer = mixer;
            catalog.groups = Groups.Select(g => mixer.FindMatchingGroups(g.name).First(m => m.name == g.name)).ToArray();
            catalog.voices = 12;
            catalog.events = Events();
            var smallHappy = Clips("SFX_Animal_Small_Chirp");
            var smallSleep = Clips("SFX_Animal_Small_Sleep");
            var smallEat = Clips("SFX_Animal_Small_Eat");
            catalog.species = new[]
            {
                new SpeciesVoice
                {
                    species = "species.rabbit", happy = Clips("SFX_Animal_Rabbit_Happy"), sleep = Clips("SFX_Animal_Rabbit_Sleep"),
                    eat = Clips("SFX_Animal_Rabbit_Eat"), pitch = 1f,
                },
                new SpeciesVoice { species = "species.squirrel", happy = smallHappy, sleep = smallSleep, eat = smallEat, pitch = 1.12f },
                new SpeciesVoice { species = "species.hedgehog", happy = smallHappy, sleep = smallSleep, eat = smallEat, pitch = 0.84f },
            };
            catalog.familyFallback = new SpeciesVoice { species = string.Empty, happy = smallHappy, sleep = smallSleep, eat = smallEat, pitch = 1f };
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static HapticProfile Haptic(HapticEventId id, float cooldown, HapticStep[] steps, long[] timings, int[] amplitudes, bool batterySkip = false)
        {
            return new HapticProfile
            {
                id = id,
                cooldown = cooldown,
                steps = steps,
                timingsMs = timings,
                amplitudes = amplitudes,
                skipOnBatterySaver = batterySkip,
            };
        }

        /// <summary>J-HAPTIC-* (doc 08): tap 10-20 ms, hatch tiers distinct and brief, Fabulous 2-3 pulses under 300 ms.</summary>
        public static HapticProfile[] HapticProfiles() => new[]
        {
            Haptic(HapticEventId.TapLight, 0.03f, new[] { new HapticStep(HapticPrimitive.Tick, 0.5f, 0) },
                new long[] { 0, 12 }, new[] { 0, 70 }),
            Haptic(HapticEventId.Confirm, 0.05f, new[] { new HapticStep(HapticPrimitive.Click, 0.6f, 0) },
                new long[] { 0, 18 }, new[] { 0, 130 }),
            Haptic(HapticEventId.HatchCommon, 0.2f,
                new[] { new HapticStep(HapticPrimitive.Click, 0.55f, 0), new HapticStep(HapticPrimitive.Tick, 0.7f, 70) },
                new long[] { 0, 16, 70, 14 }, new[] { 0, 120, 0, 90 }),
            Haptic(HapticEventId.Rare, 0.3f,
                new[] { new HapticStep(HapticPrimitive.Click, 0.85f, 0), new HapticStep(HapticPrimitive.Tick, 0.6f, 90) },
                new long[] { 0, 24, 85, 14 }, new[] { 0, 180, 0, 90 }),
            Haptic(HapticEventId.Legendary, 0.4f,
                new[] { new HapticStep(HapticPrimitive.Click, 0.9f, 0), new HapticStep(HapticPrimitive.Click, 0.6f, 70), new HapticStep(HapticPrimitive.Tick, 0.7f, 70) },
                new long[] { 0, 28, 60, 20, 60, 14 }, new[] { 0, 200, 0, 140, 0, 90 }),
            Haptic(HapticEventId.Fabulous, 0.5f,
                new[]
                {
                    new HapticStep(HapticPrimitive.QuickRise, 0.6f, 0), new HapticStep(HapticPrimitive.Click, 1f, 20),
                    new HapticStep(HapticPrimitive.Tick, 0.8f, 80), new HapticStep(HapticPrimitive.Tick, 0.6f, 60),
                },
                new long[] { 0, 40, 20, 30, 80, 14, 60, 12 }, new[] { 0, 90, 0, 220, 0, 120, 0, 90 }),
            Haptic(HapticEventId.ErrorSoft, 0.3f, new[] { new HapticStep(HapticPrimitive.LowTick, 0.7f, 0) },
                new long[] { 0, 35 }, new[] { 0, 70 }),
            Haptic(HapticEventId.PurchaseSuccess, 0.5f,
                new[] { new HapticStep(HapticPrimitive.Click, 0.7f, 0), new HapticStep(HapticPrimitive.Tick, 0.5f, 80) },
                new long[] { 0, 20, 80, 14 }, new[] { 0, 150, 0, 90 }),
            Haptic(HapticEventId.AnimalTouch, 0.08f, new[] { new HapticStep(HapticPrimitive.Tick, 0.3f, 0) },
                new long[] { 0, 8 }, new[] { 0, 45 }, batterySkip: true),
        };

        public static HapticProfileSet EnsureHaptics()
        {
            var set = AssetDatabase.LoadAssetAtPath<HapticProfileSet>(HapticsPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<HapticProfileSet>();
                AssetDatabase.CreateAsset(set, HapticsPath);
            }
            set.profiles = HapticProfiles();
            EditorUtility.SetDirty(set);
            return set;
        }

        // ---------------------------------------------------------------- beds (Addressables)

        public static BiomeAudioSet EnsureGrasslandBeds()
        {
            var beds = AssetDatabase.LoadAssetAtPath<BiomeAudioSet>(GrasslandBedsPath);
            if (beds == null)
            {
                beds = ScriptableObject.CreateInstance<BiomeAudioSet>();
                AssetDatabase.CreateAsset(beds, GrasslandBedsPath);
            }
            beds.music = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicClip) ?? throw new InvalidOperationException("Missing " + MusicClip);
            beds.ambience = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbienceClip) ?? throw new InvalidOperationException("Missing " + AmbienceClip);
            beds.ambienceEvening = AssetDatabase.LoadAssetAtPath<AudioClip>(EveningClip) ?? throw new InvalidOperationException("Missing " + EveningClip);
            beds.rain = AssetDatabase.LoadAssetAtPath<AudioClip>(RainClip) ?? throw new InvalidOperationException("Missing " + RainClip);
            beds.fadeSeconds = 2.5f;
            EditorUtility.SetDirty(beds);
            AssetDatabase.SaveAssets();

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(GrasslandGroup) ?? settings.CreateGroup(GrasslandGroup, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            // LZ4: streamed clips play straight from the bundle file.
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(GrasslandBedsPath), group, false, false);
            entry.address = GrasslandAddress;
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            return beds;
        }

        // ---------------------------------------------------------------- scene

        public static void WireBootstrap(AudioCatalog catalog, HapticProfileSet haptics)
        {
            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null)
                throw new InvalidOperationException("SC_Bootstrap has no GameBootstrap.");
            const BindingFlags field = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(GameBootstrap).GetField("_audioCatalog", field).SetValue(bootstrap, catalog);
            typeof(GameBootstrap).GetField("_hapticProfiles", field).SetValue(bootstrap, haptics);
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
