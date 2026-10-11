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
    /// - T37: the three activity musics in the Addressables group Audio_Activities (one bundle each, addresses from
    ///   ActivityAudio.Address);
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

        /// <summary>T37: the activity musics' Addressables group (one bundle per music: a game loads only its own).</summary>
        public const string ActivitiesGroup = "Audio_Activities";

        /// <summary>T37: J-MUS-ACT-01 to 03 (doc 01 Contenu, section 12.0), rendered by pw_audio_build.py music.</summary>
        public static readonly (ActivityMusicId music, string path, float seconds)[] ActivityMusics =
        {
            (ActivityMusicId.LittleGame, MusicFolder + "/MUS_Activity_LittleGame.wav", 80f),
            (ActivityMusicId.HurriedPaws, MusicFolder + "/MUS_Activity_HurriedPaws.wav", 120f),
            (ActivityMusicId.HugStreak, MusicFolder + "/MUS_Activity_HugStreak.wav", 100f),
        };

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
            int musics = EnsureActivityMusics();
            WireBootstrap(catalog, haptics);
            AssetDatabase.SaveAssets();
            return $"{clips} clips imported, {catalog.events.Length} sounds, {catalog.species.Length} species voices, " +
                   $"{haptics.profiles.Length} haptic patterns, beds in {GrasslandGroup}, {musics} activity musics in {ActivitiesGroup}.";
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
                // T36: the rendez-vous of the day (doc 05 Sensations, sheets 17 to 19, section 6.1; doc 08 J-SFX-CURRENCY-004).
                Def(AudioEventId.WishDone, "SFX_Reward_WishDone", AudioGroup.Rewards, 2, 0.4f, 2, volumeJitter: 0.03f, pitchJitter: 0.02f),
                Def(AudioEventId.GiftOpen, "SFX_Reward_GiftOpen", AudioGroup.Rewards, 2, 1f, 1, volumeJitter: 0f, pitchJitter: 0f),
                Def(AudioEventId.PawStamp, "SFX_UI_PawStamp", AudioGroup.UI, 1, 0.3f, 1, pitchJitter: 0.03f),
                Def(AudioEventId.CollectGems, "SFX_Currency_Gems", AudioGroup.Rewards, 2, 0.3f, 1, 0.9f, volumeJitter: 0.03f, pitchJitter: 0.01f),
                // T37: the activities (doc 01 Contenu, section 12.0; doc 05 Sensations, sheet 20 and section 6.1 for the
                // card and the star: 2 voices, 0.08 s and 0.1 s). Pitched variants are chosen by the presenter
                // (PlayVariant) and keep their pitch: no pitch jitter on the star and the streak.
                Def(AudioEventId.CardFlip, "SFX_UI_CardFlip", AudioGroup.UI, 1, 0.08f, 2, volumeJitter: 0.04f, pitchJitter: 0.005f),
                Def(AudioEventId.StarEarned, "SFX_Reward_Star", AudioGroup.Rewards, 2, 0.1f, 2, volumeJitter: 0.02f, pitchJitter: 0f),
                Def(AudioEventId.ActivityStart, "SFX_Activity_Start", AudioGroup.UI, 1, 1f, 1, volumeJitter: 0f, pitchJitter: 0f),
                Def(AudioEventId.ActivityEnd, "SFX_Activity_End", AudioGroup.Rewards, 2, 1f, 1, volumeJitter: 0f, pitchJitter: 0f),
                Def(AudioEventId.ActivityRecord, "SFX_Activity_Record", AudioGroup.Rewards, 2, 1f, 1, volumeJitter: 0f, pitchJitter: 0f),
                Def(AudioEventId.ActivityPause, "SFX_Activity_Pause", AudioGroup.UI, 1, 0.15f, 1, volumeJitter: 0.03f, pitchJitter: 0f),
                Def(AudioEventId.ActivityStreak, "SFX_Activity_Streak", AudioGroup.Rewards, 2, 0.06f, 2, volumeJitter: 0.03f, pitchJitter: 0f),
                // T38: the grooming (doc 01 Contenu, S-ACT-001 « Sons et effets »; GroomingAudio). The brush plays on every
                // pass: 0.12 s and 3 voices make one steady soft texture while the finger slides (a pass sounds for about
                // 0.35 s, so a fourth start only ever cuts a tail under -40 dB); level, pitch and its three strokes vary. The
                // burr's taps and the clean steps are chosen by the stage (PlayVariant) and keep their pitch; 0.05 s lets
                // zones cleaned in a quick run each sound their step.
                Def(AudioEventId.GroomBrush, "SFX_Groom_Brush", AudioGroup.UI, 2, 0.12f, 3, 0.9f, volumeJitter: 0.12f, pitchJitter: 0.04f),
                Def(AudioEventId.GroomDebris, "SFX_Groom_Debris", AudioGroup.Rewards, 2, 0.06f, 2, volumeJitter: 0.05f, pitchJitter: 0.03f),
                Def(AudioEventId.GroomBurr, "SFX_Groom_Burr", AudioGroup.Rewards, 2, 0.06f, 2, volumeJitter: 0.03f, pitchJitter: 0.01f),
                Def(AudioEventId.GroomZoneClean, "SFX_Groom_Clean", AudioGroup.Rewards, 2, 0.05f, 2, volumeJitter: 0.03f, pitchJitter: 0f),
                Def(AudioEventId.GroomBow, "SFX_Groom_Bow", AudioGroup.Rewards, 2, 0.5f, 1, volumeJitter: 0f, pitchJitter: 0f),
                // T38 J-SFX-FEEL-020 (doc 05 Sensations, section 6.1: World, 1 voice, 0.3 s): the soap bubble's puff, played
                // from the centre (the grooming close-up, later the animal sheet), not placed in the refuge.
                Def(AudioEventId.BubblePuff, "SFX_World_BubblePuff", AudioGroup.World, 2, 0.3f, 1, volumeJitter: 0.04f, pitchJitter: 0.03f),
                // T39: the hide and seek (doc 01 Contenu, S-ACT-002 « Sons et effets »; HideSeekAudio), from the centre (the
                // set's close-up). The coucou's pop: 2 voices, two animals found in a quick run each pop. The empty touch:
                // one rustle every 0.3 s at most, so quick taps never pile up (a rustle fades under -40 dB in 0.3 to 0.36 s:
                // 2 voices never cut one). The hint: 0.6 s and one voice, animals hinting at the same moment make one
                // rustle. The items and the motif once each (1 s), melodic: no pitch jitter.
                Def(AudioEventId.HideSeekCoucou, "SFX_HideSeek_Coucou", AudioGroup.Rewards, 2, 0.08f, 2, volumeJitter: 0.04f, pitchJitter: 0.02f),
                Def(AudioEventId.HideSeekEmpty, "SFX_HideSeek_Empty", AudioGroup.World, 2, 0.3f, 2, 0.9f, volumeJitter: 0.08f, pitchJitter: 0.04f),
                Def(AudioEventId.HideSeekHint, "SFX_HideSeek_Hint", AudioGroup.World, 2, 0.6f, 1, volumeJitter: 0.05f, pitchJitter: 0.03f),
                Def(AudioEventId.HideSeekMagnifier, "SFX_HideSeek_Magnifier", AudioGroup.UI, 1, 1f, 1, volumeJitter: 0f, pitchJitter: 0f),
                Def(AudioEventId.HideSeekWhistle, "SFX_HideSeek_Whistle", AudioGroup.UI, 1, 1f, 1, volumeJitter: 0f, pitchJitter: 0f),
                Def(AudioEventId.HideSeekAllFound, "SFX_HideSeek_AllFound", AudioGroup.Rewards, 2, 1f, 1, volumeJitter: 0f, pitchJitter: 0f),
                // T40: the nut drop (doc 01 Contenu, S-ACT-003 « Sons et effets »; NutDropAudio), from the centre (the set's
                // close-up). The catch is the game's most frequent sound (up to 2.5 a second, two items can land together):
                // 0.04 s and 3 voices, each pop gone under -40 dB in about 0.2 s; its item's pitch is chosen by the stage
                // (PlayVariant), a touch of pitch jitter keeps a run of hazelnuts alive. The golden hazelnut: 0.1 s and 2
                // voices (a golden shower lands one about every 0.4 s, each chime rings 0.75 s). The pinecone: 0.15 s, so a
                // pinecone reported as a catch and as its own event sounds once. The special: once (0.5 s), the same.
                Def(AudioEventId.NutDropCatch, "SFX_NutDrop_Catch", AudioGroup.Rewards, 2, 0.04f, 3, volumeJitter: 0.05f, pitchJitter: 0.015f),
                Def(AudioEventId.NutDropGolden, "SFX_NutDrop_Golden", AudioGroup.Rewards, 2, 0.1f, 2, volumeJitter: 0.03f, pitchJitter: 0f),
                Def(AudioEventId.NutDropPinecone, "SFX_NutDrop_Pinecone", AudioGroup.World, 2, 0.15f, 2, volumeJitter: 0.06f, pitchJitter: 0.03f),
                Def(AudioEventId.NutDropSpecial, "SFX_NutDrop_Special", AudioGroup.Rewards, 2, 0.5f, 1, volumeJitter: 0f, pitchJitter: 0f),
                // T41: the pawprint memory (doc 01 Contenu, S-ACT-004 « Sons et effets »; MemoryAudio), an interface screen.
                // The flip is the touch feedback, two per try: 0.05 s and 2 voices, so two cards turned in a quick run each
                // rustle (a rustle is under -40 dB in 0.22 s); level, pitch and its three variants vary. The pair's third is
                // chosen by the stage (PlayVariant, climbing with the pairs found) and keeps its pitch; 2 voices let the
                // last third ring under the next. The miss: one felt note at a time (0.3 s, a new try needs two flips). The
                // glow card's glass once (0.5 s, one glow card a game).
                Def(AudioEventId.MemoryFlip, "SFX_Memory_Flip", AudioGroup.UI, 1, 0.05f, 2, volumeJitter: 0.06f, pitchJitter: 0.04f),
                Def(AudioEventId.MemoryMatch, "SFX_Memory_Match", AudioGroup.Rewards, 2, 0.1f, 2, volumeJitter: 0.02f, pitchJitter: 0f),
                Def(AudioEventId.MemoryMiss, "SFX_Memory_Miss", AudioGroup.UI, 1, 0.3f, 1, volumeJitter: 0.04f, pitchJitter: 0f),
                Def(AudioEventId.MemoryGlow, "SFX_Memory_Glow", AudioGroup.Rewards, 2, 0.5f, 1, volumeJitter: 0f, pitchJitter: 0f),
                // T42: the expeditions (doc 01 Contenu, S-ACT-005 « Sons et effets »; ExpeditionAudio), an interface screen.
                // The departure and the door bell once a second at most (two slots ending together make one bell). The
                // postcard's slide: 0.25 s and 2 voices, so the postcards of a « Tout réclamer » (300 ms apart) each slide.
                // The loot's pop is chosen by the result (PlayVariant, one step higher per item) and keeps its pitch; 2
                // voices let a pop ring under the next. J-SFX-FEEL-016, the team's return: doc 05 section 6.1, Rewards, 1
                // voice, 1 s.
                Def(AudioEventId.ExpeditionDepart, "SFX_Expedition_Depart", AudioGroup.UI, 1, 1f, 1, volumeJitter: 0.03f, pitchJitter: 0.01f),
                Def(AudioEventId.ExpeditionBack, "SFX_Expedition_Back", AudioGroup.Rewards, 2, 1f, 1, volumeJitter: 0.03f, pitchJitter: 0f),
                Def(AudioEventId.ExpeditionPostcard, "SFX_Expedition_Postcard", AudioGroup.UI, 1, 0.25f, 2, volumeJitter: 0.04f, pitchJitter: 0.02f),
                Def(AudioEventId.ExpeditionLoot, "SFX_Expedition_Loot", AudioGroup.Rewards, 2, 0.1f, 2, volumeJitter: 0.03f, pitchJitter: 0f),
                Def(AudioEventId.ExpeditionReturn, "SFX_Reward_ExpeditionReturn", AudioGroup.Rewards, 2, 1f, 1, volumeJitter: 0f, pitchJitter: 0f),
                // T43: the garden (doc 01 Contenu, S-ACT-006 « Sons et effets »; GardenAudio), from the centre (the set's
                // close-up), in the World group. The planting: 0.2 s, one voice (one bed planted at a time). The trickle
                // once a second (it lasts 1 s). The bud's pop: 0.15 s and 2 voices, beds catching up together make one or
                // two pops. J-SFX-FEEL-017, the harvest: doc 05 section 6.1, World, 2 voices, 0.1 s (a « Tout récolter »
                // runs 120 ms apart); its pitch is chosen by the chain (PlayPitched) and kept: no pitch jitter. The
                // garden's level is J-SFX-REWARD-002, the mastery motif (SFX_Reward_MasteryUp), once.
                Def(AudioEventId.GardenHarvest, "SFX_World_Harvest", AudioGroup.World, 2, 0.1f, 2, volumeJitter: 0.04f, pitchJitter: 0f),
                Def(AudioEventId.GardenPlant, "SFX_Garden_Plant", AudioGroup.World, 2, 0.2f, 1, volumeJitter: 0.05f, pitchJitter: 0.03f),
                Def(AudioEventId.GardenWater, "SFX_Garden_Water", AudioGroup.World, 2, 1f, 1, volumeJitter: 0.03f, pitchJitter: 0.02f),
                Def(AudioEventId.GardenStage, "SFX_Garden_Stage", AudioGroup.World, 2, 0.15f, 2, volumeJitter: 0.04f, pitchJitter: 0.02f),
                Def(AudioEventId.GardenLevelUp, "SFX_Reward_MasteryUp", AudioGroup.Rewards, 2, 1f, 1, volumeJitter: 0f, pitchJitter: 0f),
                // T44A: the albums (doc 01 Albums et autocollants; doc 05 section 6.4, slots 43 and 44), Rewards. A sticker
                // won: once every 0.4 s at most (a draw of several stickers reveals them one after another, the peel is
                // 0.5 s). The page turn: once every 0.9 s (a page takes that long to turn, the notes ring 0.9 s).
                Def(AudioEventId.Sticker, "SFX_Reward_Sticker", AudioGroup.Rewards, 2, 0.4f, 1, volumeJitter: 0.02f, pitchJitter: 0f),
                Def(AudioEventId.AlbumPage, "SFX_Reward_AlbumPage", AudioGroup.Rewards, 2, 0.9f, 1, volumeJitter: 0.02f, pitchJitter: 0f),
                // T44C: the journal and the visitors. J-SFX-FEEL-018 (doc 05 section 6.1, slot 47): World, 1 voice, once per
                // 10 s, only when the visitor is in frame. The journal page: UI, once every 0.5 s (the capture queue lets
                // one through a second at most), a paper rustle that needs no variation.
                Def(AudioEventId.VisitorArrive, "SFX_World_VisitorArrive", AudioGroup.World, 2, 10f, 1, volumeJitter: 0.02f, pitchJitter: 0f),
                Def(AudioEventId.JournalPage, "SFX_UI_JournalPage", AudioGroup.UI, 1, 0.5f, 1, volumeJitter: 0.03f, pitchJitter: 0.01f),
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
                new SpeciesVoice { species = "species.fieldmouse", happy = smallHappy, sleep = smallSleep, eat = smallEat, pitch = 1.30f },
                new SpeciesVoice { species = "species.guineapig", happy = smallHappy, sleep = smallSleep, eat = smallEat, pitch = 1.15f },
                // Fawn: the small bank, lower and softer; the medium bank comes later.
                new SpeciesVoice { species = "species.fawn", happy = smallHappy, sleep = smallSleep, eat = smallEat, pitch = 0.90f },
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

        /// <summary>
        /// T37: the three activity musics as Addressables clips (doc 01 Contenu, section 12.0). They import like the
        /// theme (streamed Vorbis, ConfigureImporters); packed one bundle each (LZ4: streamed from the bundle file), so
        /// a game opens only its own music. The presenter loads ActivityAudio.Address(ActivityAudio.MusicOf(activity)).
        /// </summary>
        public static int EnsureActivityMusics()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(ActivitiesGroup) ?? settings.CreateGroup(ActivitiesGroup, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            int count = 0;
            foreach (var (music, path, _) in ActivityMusics)
            {
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                    throw new InvalidOperationException("Missing " + path + ": run SourceArt/Audio/Tools/pw_audio_build.py music.");
                var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group, false, false);
                entry.address = ActivityAudio.Address(music);
                count++;
            }
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            return count;
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
