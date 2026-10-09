using System;
using System.IO;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Analytics;
using PocketWilds.Services.Save;
using PocketWilds.Services.Store;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketWilds.Editor.Marketing
{
    /// <summary>
    /// What one capture run does. Saved in SessionState as JSON across the domain reload of Play Mode; every field can be
    /// overridden from <see cref="MarketingCapture.Begin(string)"/> (a quick preview: <c>{"stride":6,"supersample":1}</c>).
    /// </summary>
    [Serializable]
    public sealed class MarketingCaptureSettings
    {
        /// <summary>Folder under Marketing/Videos.</summary>
        public string video = "T01_test";

        public int width = 1080;
        public int height = 1920;

        /// <summary>The camera renders at this multiple of the output size (1 or 2, 4 halves twice), then is box filtered.</summary>
        public int supersample = 2;

        public int frameRate = 30;

        /// <summary>Preview: writes every Nth frame only (the edit reads them at frameRate / stride). The audio keeps every frame.</summary>
        public int stride = 1;

        /// <summary>Local hour of the light (WorldAtmosphere.ForceHour): 16 is the full day mood, 18.5 the full evening.</summary>
        public float hour = 17.2f;

        /// <summary>The egg of the hook: profile ids are tried until the first hatch gives this species.</summary>
        public string hatchSpecies = "species.rabbit";

        public int profileSearch = 96;

        /// <summary>The favorite hearts (H-VFX-012) over the newborn right after its happy jump.</summary>
        public bool heartsOnHatch = true;

        /// <summary>Shot 3 plays the real Hide and Seek (T39) when its stage exists; otherwise two animals play together.</summary>
        public bool hideSeek = true;

        /// <summary>"refuge" (a slow rise over the animals) or "windmill" (WindmillRise in the golden light).</summary>
        public string endCard = "refuge";

        /// <summary>Prairie zones open in the seeded save (bit 0 the arrival zone).</summary>
        public int zonesOpened = 0b11;

        /// <summary>Read back rows bottom-up (set if the frames come out upside down on another graphics API).</summary>
        public bool flipY;

        /// <summary>The shot list: "T01" (the first test) or "T02" (short shots of many moments, the edit cuts them on the beat).</summary>
        public string plan = "T01";

        /// <summary>Refuge level and Renewals of the seeded save (× 10 hatching needs level 25 and one Renewal).</summary>
        public int refugeLevel = 5;

        public int renewals;

        /// <summary>Light in the wallet (× 10 eggs and a zone opening need much more than the first test).</summary>
        public double light = 2_000_000;

        /// <summary>The first hatch's lowest rarity wanted (2 = Rare: its glow and its own burst).</summary>
        public int hatchMinRarity;

        public bool recordAudio = true;
        public float bootTimeoutSeconds = 240f;
        public bool exitPlayModeWhenDone = true;
    }

    /// <summary>
    /// Marketing videos made from the real game (owner's plan of 9 October 2026; storyboard
    /// Marketing/Videos/T01_test/storyboard.md). <see cref="Begin(string)"/> seeds a scratch save with a lovely collection
    /// (never the owner's own save), enters Play Mode from SC_Bootstrap like a real launch and returns at once. In Play Mode
    /// the session (<see cref="MarketingSession"/>) runs in the player loop at a fixed 30 fps (Time.captureFramerate): it
    /// drives the refuge camera along eased shots, stages the real hatch, hide and seek and treat moments, writes every
    /// frame as a PNG (1080 × 1920, supersampled) and the game's own audio rendered offline (AudioRenderer), then leaves
    /// Play Mode. Progress and the end ("done" or "failed") go to Marketing/Videos/&lt;video&gt;/capture_status.json.
    /// The edit (Tools/Marketing/make_video.py) adds our music, the English captions in our fonts and the loudness pass.
    /// </summary>
    [InitializeOnLoad]
    public static class MarketingCapture
    {
        public const string MenuPath = "Pocket Wilds/Marketing/Capture Test Video (T01)";
        public const string BootstrapScenePath = "Assets/_Game/Scenes/SC_Bootstrap.unity";
        private const string ContentDatabasePath = "Assets/_Game/Content/DEF_ContentDatabase.asset";
        private const string ArmedKey = "PW.Marketing.Armed";
        private const string SettingsKey = "PW.Marketing.Settings";
        private const string RestoreKey = "PW.Marketing.Restore";
        private const string ProfileKey = "PW.Marketing.Profile";
        private const string PredictionKey = "PW.Marketing.Prediction";
        private const string ArmedAtKey = "PW.Marketing.ArmedAt";

        /// <summary>The hand-over expires: a Play Mode entered minutes later (by hand) boots the normal game.</summary>
        private const float ArmedSeconds = 900f;

        /// <summary>The day of the capture: a Monday of October, mid-morning UTC (the light itself is forced).</summary>
        public static readonly DateTimeOffset ClockTime = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

        public const int DayKey = 20261012;

        /// <summary>
        /// The seeded refuge: 13 animals of the three Prairie species in varied coats (two favorites, which Hide and Seek
        /// picks first), rarities as their variants. Two Epic coats only, so the rare glints stay a treat.
        /// </summary>
        private static readonly (string species, string variant, RarityTier rarity, bool favorite)[] Roster =
        {
            ("species.rabbit", "variant.rabbit.white", RarityTier.Common, true),
            ("species.squirrel", "variant.squirrel.red", RarityTier.Common, false),
            ("species.hedgehog", "variant.hedgehog.brown", RarityTier.Common, false),
            ("species.rabbit", "variant.rabbit.brown", RarityTier.Common, false),
            ("species.rabbit", "variant.rabbit.spotted", RarityTier.Rare, false),
            ("species.squirrel", "variant.squirrel.cream", RarityTier.Uncommon, false),
            ("species.hedgehog", "variant.hedgehog.mushroom", RarityTier.Epic, true),
            ("species.rabbit", "variant.rabbit.grey", RarityTier.Uncommon, false),
            ("species.squirrel", "variant.squirrel.grey", RarityTier.Uncommon, false),
            ("species.rabbit", "variant.rabbit.sakura", RarityTier.Epic, false),
            ("species.hedgehog", "variant.hedgehog.cream", RarityTier.Common, false),
            ("species.squirrel", "variant.squirrel.brown", RarityTier.Common, false),
            ("species.rabbit", "variant.rabbit.black", RarityTier.Uncommon, false),
        };

        [Serializable]
        private sealed class Restore
        {
            public bool runInBackground;
            public bool asyncShaders;
            public bool mute;
            public bool startSceneChanged;
            public int hatchQuantity = -1;
        }

        static MarketingCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // The domain reload of Play Mode wiped the static options: give them back before SC_Bootstrap wakes up.
            if (Armed)
                GameBootstrap.PendingOptions = BootOptionsFor(LoadSettings());
        }

        /// <summary>Play Mode without domain reload never runs the static constructor again: same hand-over here.</summary>
        [InitializeOnEnterPlayMode]
        private static void OnEnterPlayMode()
        {
            if (Armed)
                GameBootstrap.PendingOptions = BootOptionsFor(LoadSettings());
        }

        private static bool Armed
        {
            get
            {
                if (!SessionState.GetBool(ArmedKey, false))
                    return false;
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(ArmedAtKey, 0f) <= ArmedSeconds)
                    return true;
                SessionState.EraseBool(ArmedKey);
                return false;
            }
        }

        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        public static string VideoDirectory(MarketingCaptureSettings s) => Path.Combine(ProjectRoot, "Marketing", "Videos", s.video);

        /// <summary>The scratch save, in the system temp folder: never Application.persistentDataPath.</summary>
        public static string SaveDirectory(MarketingCaptureSettings s) => Path.Combine(Path.GetTempPath(), "PocketWildsMarketing", s.video, "save");

        public static string StatusPath(MarketingCaptureSettings s) => Path.Combine(VideoDirectory(s), "capture_status.json");

        [MenuItem(MenuPath, priority = 400)]
        public static void CaptureTestVideoMenu() => Debug.Log("[MarketingCapture] " + Begin(null));

        [MenuItem("Pocket Wilds/Marketing/Preview Test Video (T01, every 6th frame)", priority = 401)]
        public static void PreviewTestVideoMenu() => Debug.Log("[MarketingCapture] " + Begin("{\"stride\":6,\"supersample\":1}"));

        [MenuItem("Pocket Wilds/Marketing/Stop Capture", priority = 402)]
        public static void StopMenu() => Debug.Log("[MarketingCapture] " + Stop());

        /// <summary>
        /// Seeds the scratch save, arms the hand-over and enters Play Mode; returns at once (MCP calls must stay short).
        /// Poll capture_status.json in the video folder. <paramref name="settingsJson"/> overrides fields of
        /// <see cref="MarketingCaptureSettings"/>.
        /// </summary>
        public static string Begin(string settingsJson)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "refused: already in Play Mode";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return "refused: Unity is compiling or importing";
            var settings = new MarketingCaptureSettings();
            if (!string.IsNullOrWhiteSpace(settingsJson))
                JsonUtility.FromJsonOverwrite(settingsJson, settings);
            settings.supersample = settings.supersample >= 4 ? 4 : settings.supersample >= 2 ? 2 : 1;
            settings.stride = Mathf.Max(1, settings.stride);
            bool restoring = false;
            try
            {
                string video = VideoDirectory(settings);
                Directory.CreateDirectory(video);
                string frames = Path.Combine(video, "frames");
                Directory.CreateDirectory(frames);
                foreach (var old in Directory.GetFiles(frames, "*.png"))
                    File.Delete(old);
                foreach (var name in new[] { "game_audio.wav", "capture_log.json" })
                    if (File.Exists(Path.Combine(video, name)))
                        File.Delete(Path.Combine(video, name));

                var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(ContentDatabasePath);
                if (database == null)
                    return "refused: no content database at " + ContentDatabasePath;
                var content = database.CreateBuilder().Build();
                var profile = ChooseProfile(content, settings, out string prediction);
                SeedSave(settings, profile);
                SessionState.SetString(ProfileKey, profile.ToString());
                SessionState.SetString(PredictionKey, prediction);
                SessionState.SetString(SettingsKey, JsonUtility.ToJson(settings));

                // Play Mode must keep running while the Unity window is not focused (it froze at frame 2 otherwise).
                var restore = new Restore
                {
                    runInBackground = PlayerSettings.runInBackground,
                    asyncShaders = ShaderUtil.allowAsyncCompilation,
                    mute = EditorUtility.audioMasterMute,
                    hatchQuantity = PlayerPrefs.HasKey(PocketWilds.Presentation.EggChoice.QuantityKey)
                        ? PlayerPrefs.GetInt(PocketWilds.Presentation.EggChoice.QuantityKey) : -1,
                };
                // The first hatch of a capture is a single egg (the × 10 is chosen in game, as a player does).
                PlayerPrefs.SetInt(PocketWilds.Presentation.EggChoice.QuantityKey, 1);
                if (EditorSceneManager.playModeStartScene == null)
                {
                    EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
                    restore.startSceneChanged = true;
                }
                SessionState.SetString(RestoreKey, JsonUtility.ToJson(restore));
                restoring = true;
                PlayerSettings.runInBackground = true;
                // Placeholder shaders while variants compile would show in the frames: compile on first use instead.
                ShaderUtil.allowAsyncCompilation = false;
                EditorUtility.audioMasterMute = false;

                SessionState.SetBool(ArmedKey, true);
                SessionState.SetFloat(ArmedAtKey, (float)EditorApplication.timeSinceStartup);
                GameBootstrap.PendingOptions = BootOptionsFor(settings);
                MarketingSession.WriteStatus(settings, "entering_play_mode", prediction, 0);
                EditorApplication.EnterPlaymode();
                return $"started {settings.video}: scratch save {SaveDirectory(settings)}, {prediction}. Poll {StatusPath(settings)}.";
            }
            catch (Exception ex)
            {
                SessionState.EraseBool(ArmedKey);
                if (restoring)
                    RestoreEditor();
                Debug.LogException(ex);
                return "failed: " + ex.Message;
            }
        }

        /// <summary>Ends a running capture now (status "aborted") and leaves Play Mode.</summary>
        public static string Stop()
        {
            if (MarketingSession.Current != null)
            {
                MarketingSession.Current.Abort("stopped from the menu");
                return "stopping";
            }
            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
            RestoreEditor();
            return "no capture running";
        }

        internal static MarketingCaptureSettings LoadSettings()
        {
            var settings = new MarketingCaptureSettings();
            string json = SessionState.GetString(SettingsKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
                JsonUtility.FromJsonOverwrite(json, settings);
            return settings;
        }

        internal static string Prediction => SessionState.GetString(PredictionKey, string.Empty);

        internal static string ProfileText => SessionState.GetString(ProfileKey, string.Empty);

        /// <summary>
        /// The scratch save, a fixed clock (no offline time, no new day to process), the High tier for every boot, the store
        /// unavailable (no moment offer pops up mid-shot) and analytics kept beside the scratch save.
        /// </summary>
        internal static BootOptions BootOptionsFor(MarketingCaptureSettings settings)
        {
            string save = SaveDirectory(settings);
            return new BootOptions
            {
                SaveDirectory = save,
                AnalyticsDirectory = Path.Combine(save, "Analytics"),
                AnalyticsConsentOverride = AnalyticsConsent.Unknown,
                ClockOverride = new FakeGameClock(ClockTime),
                QualityOverride = QualityTier.High,
                StoreProviderOverride = new UnavailableStoreProvider(),
                LoadTargetScene = true,
            };
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    if (Armed)
                    {
                        SessionState.EraseBool(ArmedKey);
                        MarketingSession.Start(LoadSettings());
                    }
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    MarketingSession.Current?.OnExitingPlayMode();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    SessionState.EraseBool(ArmedKey);
                    RestoreEditor();
                    break;
            }
        }

        /// <summary>Gives back what Begin changed (run in background, async shaders, mute, the start scene). Idempotent.</summary>
        internal static void RestoreEditor()
        {
            string json = SessionState.GetString(RestoreKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;
            SessionState.EraseString(RestoreKey);
            var restore = JsonUtility.FromJson<Restore>(json);
            PlayerSettings.runInBackground = restore.runInBackground;
            ShaderUtil.allowAsyncCompilation = restore.asyncShaders;
            EditorUtility.audioMasterMute = restore.mute;
            if (restore.startSceneChanged)
                EditorSceneManager.playModeStartScene = null;
            if (restore.hatchQuantity >= 0)
                PlayerPrefs.SetInt(PocketWilds.Presentation.EggChoice.QuantityKey, restore.hatchQuantity);
            else
                PlayerPrefs.DeleteKey(PocketWilds.Presentation.EggChoice.QuantityKey);
            PlayerPrefs.Save();
            Debug.Log("[MarketingCapture] Editor settings restored (run in background " + restore.runInBackground + ").");
        }

        // ---------------- The scratch save ----------------

        private static void SeedSave(MarketingCaptureSettings settings, Guid profile)
        {
            string directory = SaveDirectory(settings);
            // Only our own scratch folder is ever cleared.
            if (!directory.Replace('\\', '/').Contains("/PocketWildsMarketing/"))
                throw new InvalidOperationException("Unexpected scratch save folder: " + directory);
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
            Directory.CreateDirectory(directory);
            var service = new SaveService(new SaveFileStore(directory), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new FakeGameClock(ClockTime), new SaveServiceOptions { AppVersion = Application.version });
            try
            {
                service.LoadOrCreate();
                Fill(service.State, settings, profile);
                if (!service.FlushNow())
                    throw new InvalidOperationException("The scratch save was not written.");
            }
            finally
            {
                service.Dispose();
            }
        }

        /// <summary>
        /// A refuge a few days in: the collection, the Prairie with its open zones, refuge level 5 with its rewards taken,
        /// treats for the treat moment, the welcome and today's rendez-vous already done (no screen opens at boot, no wish
        /// bubble), the music bed silent (our music is laid in the edit), English texts.
        /// </summary>
        private static void Fill(PlayerState state, MarketingCaptureSettings settings, Guid profile)
        {
            state.Meta.ProfileId = profile;
            state.Settings.MusicVolume = 0f;
            state.Settings.SfxVolume = 1f;
            state.Settings.HapticsEnabled = false;
            state.Settings.ReduceMotion = false;
            state.Settings.LanguageCode = "en";
            state.Wallet.Light = BigValue.FromDouble(settings.light);
            state.Wallet.LifetimeLight = BigValue.FromDouble(settings.light * 4.5);
            state.World.RenewalCount = settings.renewals;
            state.Wallet.Gems = 120;

            for (int i = 0; i < Roster.Length; i++)
            {
                var (speciesId, variantId, rarity, favorite) = Roster[i];
                var species = ContentId.FromSource(speciesId);
                var variant = ContentId.FromSource(variantId);
                state.Collection.Add(new AnimalInstance
                {
                    Species = species,
                    Variant = variant,
                    Rarity = rarity,
                    Seed = SeedHash.Mix((uint)i + 1u, 0x4D4B5456u),
                    Flags = favorite ? AnimalFlags.Favorite : AnimalFlags.None,
                    CreatedUtcSeconds = (uint)ClockTime.AddDays(-3).ToUnixTimeSeconds(),
                });
                state.Collection.DiscoveredSpecies.Add(species);
                state.Collection.DiscoveredVariants.Add(variant);
                state.Codex.SeenSpecies.Add(species);
                state.Codex.SeenVariants.Add(variant);
            }

            int zones = settings.zonesOpened | 1;
            int expansions = 0;
            for (int bit = 1; bit < 31; bit++)
                if ((zones & (1 << bit)) != 0)
                    expansions++;
            state.World.Biomes.Clear();
            state.World.Biomes.Add(new BiomeProgress
            {
                Biome = ContentId.FromSource("biome.grassland"), Unlocked = true, Expansions = expansions, ZonesOpened = (uint)zones,
                MasteryXp = 600, MasteryLevel = 4, ProductionLevel = 10, NestsLevel = 3, CuriosityLevel = 0,
            });
            state.Progression.Refuge.Score = settings.refugeLevel <= 5 ? 150 : 150L * settings.refugeLevel * settings.refugeLevel;
            state.Progression.Refuge.Level = settings.refugeLevel;
            state.Progression.Refuge.LastRewardedLevel = settings.refugeLevel;
            state.Progression.Treats[ContentId.FromSource("treat.clover_crunch")] = 6;
            state.Progression.Treats[ContentId.FromSource("treat.mushroom_biscuit")] = 4;

            var daily = state.DailyVisit;
            daily.OnboardingDone = true;
            daily.TutorialStep = 3;
            daily.WelcomeClaimed = 3;
            daily.WelcomeDayKey = DayKey;
            daily.LoginDays = 6;
            daily.LastDayKey = DayKey;
            daily.GiftDayKey = DayKey;
            daily.GiftsOpened = 5;
            daily.CalendarDayKey = DayKey;
            daily.CalendarNext = 5;
            daily.CalendarStamps = 5;
            daily.WishDrawDayKey = DayKey;
            daily.WishCount = 0;
            daily.FirstJournalDone = true;
            daily.JournalStartKey = DayKey;
            daily.JournalEndKey = DayKey + 7;
        }

        // ---------------- The egg of the hook ----------------

        /// <summary>
        /// The first hatch is decided by the profile id (HatchService seeds its draws from it): candidate ids are tried on a
        /// copy of the seeded state, with the services the bootstrap builds, until one gives <see cref="MarketingCaptureSettings.hatchSpecies"/>.
        /// </summary>
        private static Guid ChooseProfile(ContentRegistry content, MarketingCaptureSettings settings, out string prediction)
        {
            var wanted = ContentId.FromSource(settings.hatchSpecies);
            var egg = ContentId.FromSource("egg.grassland.basic");
            Guid first = Candidate(0);
            prediction = "first hatch unknown";
            for (int k = 0; k < Mathf.Max(1, settings.profileSearch); k++)
            {
                var profile = Candidate(k);
                try
                {
                    var state = PlayerState.CreateNew(ClockTime);
                    Fill(state, settings, profile);
                    var clock = new FakeGameClock(ClockTime);
                    var economy = new EconomyService(state, content, EconomyConfig.Default, null);
                    var mastery = new MasteryService(state, economy.Config, economy);
                    var hatch = new HatchService(state, content, economy, clock, null, mastery);
                    var offline = new OfflineService(state, economy, clock);
                    _ = new ProgressionServices(state, content, economy, hatch, clock, null, null, mastery, offline);
                    var result = hatch.Hatch(egg);
                    if (!result.Succeeded)
                    {
                        prediction = "first hatch refused: " + result.Status;
                        return first;
                    }
                    string text = $"predicted first hatch {Name(content, result.Animal.Species)} / {VariantName(content, result.Animal.Variant)} ({result.Animal.Rarity}) with profile {profile}";
                    if (k == 0)
                        prediction = text;
                    if (result.Animal.Species == wanted && (int)result.Animal.Rarity >= settings.hatchMinRarity)
                    {
                        prediction = text;
                        return profile;
                    }
                }
                catch (Exception ex)
                {
                    prediction = "prediction failed: " + ex.Message;
                    return first;
                }
            }
            return first;
        }

        private static Guid Candidate(int k) => new Guid(0x4D560000 + k, 0x5401, 0x0001, new byte[] { 0x50, 0x57, 0x4D, 0x56, 0, 0, 0, 1 });

        internal static string Name(ContentRegistry content, ContentId species) =>
            content.Species.TryGet(species, out var definition) ? definition.DisplayKey : species.ToString();

        internal static string VariantName(ContentRegistry content, ContentId variant) =>
            content.Variants.TryGet(variant, out var definition) ? definition.DisplayKey : variant.ToString();
    }
}
