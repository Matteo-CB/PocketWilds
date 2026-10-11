using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Analytics;
using PocketWilds.Services.Device;
using PocketWilds.Services.Feedback;
using PocketWilds.Services.Localization;
using PocketWilds.Services.Save;
using PocketWilds.Services.Store;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Profiling;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace PocketWilds.Bootstrap
{
    public enum BootState
    {
        NotStarted,
        Running,
        Ready,
        Failed,
    }

    /// <summary>
    /// Composition root, lives in SC_Bootstrap. Builds every service in an explicit order:
    /// platform -> device settings -> content -> save -> clock -> simulation -> presentation -> background services.
    /// The player enters the refuge without waiting for cloud, analytics or remote config.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        /// <summary>Test/dev hook, consumed by the next boot. Never set by production code.</summary>
        public static BootOptions PendingOptions;

        [SerializeField] private string _targetScene = "SC_VerticalSlice_Grassland";
        [SerializeField] private ContentDatabaseAsset _contentDatabase;
        [Tooltip("T34C: the animal catalog (prefabs, rigs, master material) lives in the Addressables group CommonAnimals.")]
        [SerializeField] private string _animalCatalogKey = "animals.catalog";
        [Tooltip("T34C: warmup sets of the shaders that live in Addressables groups (label), warmed after the built-in set.")]
        [SerializeField] private string _warmupLabel = "warmup";
        [Tooltip("T34: graphics states warmed behind the loading screen (first displays never stutter).")]
        [SerializeField] private ShaderWarmupSet _shaderWarmup;
        [Tooltip("T34A boot screen (PF_UI_Splash): shown from the first frame until the refuge is ready.")]
        [SerializeField] private PocketWilds.UI.SplashScreen _splashPrefab;
        [Tooltip("T34B: every sound, the mixer and the voice pool (DEF_AudioEvents).")]
        [SerializeField] private AudioCatalog _audioCatalog;
        [Tooltip("T34B: haptic patterns (DEF_HapticProfiles).")]
        [SerializeField] private HapticProfileSet _hapticProfiles;

        private PocketWilds.UI.SplashScreen _splash;

        private BootOptions _options;
        private IGameClock _clock;
        private ContentRegistry _content;
        private SaveService _save;
        private SaveLoadResult _loadResult;
        private EconomyService _economy;
        private HatchService _hatch;
        private MasteryService _mastery;
        private OfflineService _offline;
        private CodexService _codex;
        private ProgressionServices _progression;
        private DailyVisitServices _daily;
        private ActivityServices _activities;
        private GroomingHooks _groomingHooks;
        private HideSeekHooks _hideSeekHooks;
        private NutDropHooks _nutDropHooks;
        private MemoryHooks _memoryHooks;
        private ExpeditionService _expeditions;
        private ExpeditionHooks _expeditionHooks;
        private GardenService _garden;
        private GardenHooks _gardenHooks;
        private AlbumService _albums;
        private AlbumHooks _albumHooks;
        private AchievementService _achievements;
        private ProfileItemService _profileItems;
        private AchievementHooks _achievementHooks;
        private JournalService _journal;
        private VisitorService _visitors;
        private MasteryColorService _masteryColors;
        private CosmeticService _cosmetics;
        private CompanionService _companions;
        private EventService _events;
        private JournalHooks _journalHooks;
        private GrantService _grants;
        private OfferService _offers;
        private StoreService _store;
        private AnalyticsService _analytics;
        private AnalyticsOutbox _analyticsOutbox;
        private bool _batterySaver;
        private LocalizationService _localization;
        private QualityService _quality;
        private FrameRateService _frameRate;
        private GameLoop _loop;
        private PaletteTable _palettes;
        private ISceneEntry _activeEntry;
        private AudioService _audio;
        private HapticService _haptics;
        private IHapticBackend _hapticBackend;
        private AsyncOperationHandle<AnimalVisualCatalog> _animalCatalog;
        private AsyncOperationHandle<System.Collections.Generic.IList<ShaderWarmupSet>> _warmupSets;

        public BootState State { get; private set; } = BootState.NotStarted;
        public GameContext Context { get; private set; }
        public SaveLoadResult LoadResult => _loadResult;

        /// <summary>Away time paid at boot (OfflineService). Also handed to the scene through the context.</summary>
        public OfflineReport OfflineAtBoot { get; private set; }

        public BootSequence Sequence { get; private set; }

        /// <summary>Audio and haptics (T34B). Null before the Audio step.</summary>
        public AudioService Audio => _audio;
        public HapticService Haptics => _haptics;

        /// <summary>Analytics (T29). Null before the Analytics step.</summary>
        public AnalyticsService Analytics => _analytics;
        public AnalyticsOutbox AnalyticsOutbox => _analyticsOutbox;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _options = PendingOptions ?? new BootOptions();
            PendingOptions = null;
            // A player run with -pw-trace-graphics records every graphics state it draws (T34 warmup data).
            ShaderWarmup.BeginBootTrace();
            if (_splashPrefab != null)
                _splash = Instantiate(_splashPrefab);
        }

        /// <summary>One rendered frame of the refuge behind it, then the boot screen fades away.</summary>
        private async Awaitable DismissSplashAsync()
        {
            if (_splash == null)
                return;
            try
            {
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            if (_splash != null)
                _splash.Dismiss();
        }

        /// <summary>The boot screen in front while booting (tests, captures).</summary>
        public PocketWilds.UI.SplashScreen Splash => _splash;

        private async Awaitable Start()
        {
            State = BootState.Running;
            Sequence = BuildSequence();
            try
            {
                await Sequence.RunAsync(destroyCancellationToken);
                State = BootState.Ready;
                GameLog.Info(LogCategory.Boot, "Boot complete.");
                _ = DismissSplashAsync();
            }
            catch (OperationCanceledException)
            {
                State = BootState.Failed;
            }
            catch (Exception ex)
            {
                State = BootState.Failed;
                GameLog.Error(LogCategory.Boot, "Boot failed: " + ex.Message);
                Debug.LogException(ex);
            }
            finally
            {
                // Never leave the boot screen up over a failed or cancelled boot.
                if (_splash != null && State != BootState.Ready)
                    _splash.Dismiss();
            }
        }

        private BootSequence BuildSequence()
        {
            var sequence = new BootSequence();

            sequence.AddBlocking("Platform", () =>
            {
                Screen.sleepTimeout = SleepTimeout.SystemSetting;
            });

            sequence.AddBlocking("DeviceSettings", () =>
            {
                var device = LocalDeviceSettingsStore.Load();
                _frameRate = new FrameRateService();
                _frameRate.Apply(device.BatterySaver);
                _batterySaver = device.BatterySaver;
                // T34: automatic by default (the device's tier, adaptive steps and heat protection), or the player's
                // tier; benchmarks and tests boot a fixed tier.
                _quality = new QualityService();
                _quality.Initialize(_options.QualityOverride, device.QualityOverride, _frameRate);
            });

            sequence.AddBlocking("ContentRegistry", () =>
            {
                _content = _options.ContentOverride ?? BuildShippedContent(_contentDatabase);
            });

            sequence.AddBlocking("Save", async cancellationToken =>
            {
                _clock = _options.ClockOverride ?? new RuntimeGameClock();
                string directory = _options.SaveDirectory ?? Path.Combine(Application.persistentDataPath, "Saves");
                var store = new SaveFileStore(directory);
                var migrations = SaveMigrations.CreatePipeline();
                var saveOptions = new SaveServiceOptions { AppVersion = Application.version };
                _save = new SaveService(store, new BinarySaveSerializer(), migrations, _clock, saveOptions);
                _loadResult = await _save.LoadOrCreateAsync(cancellationToken);
                GameLog.Info(LogCategory.Save, $"Save {_loadResult.Outcome}, revision {(_loadResult.State != null ? _loadResult.State.Meta.Revision : 0)}");
                if (_loadResult.Outcome == SaveLoadOutcome.NewerSchemaRefused)
                    throw new InvalidOperationException("Save was made by a newer version. Update the game.");
            });

            sequence.AddBlocking("Localization", () =>
            {
                var tables = _options.LocalizationOverride
                             ?? (_contentDatabase != null ? _contentDatabase.localization.ToArray() : null);
                _localization = new LocalizationService(tables, _save.State.Settings.LanguageCode, Application.systemLanguage);
            });

            sequence.AddBlocking("Simulation", () =>
            {
                _economy = new EconomyService(_save.State, _content, EconomyConfig.Default, _save);
                _mastery = new MasteryService(_save.State, _economy.Config, _economy);
                _hatch = new HatchService(_save.State, _content, _economy, _clock, _save, _mastery);
                _offline = new OfflineService(_save.State, _economy, _clock);
                _codex = new CodexService(_save.State, _content, _save, _mastery);
                // T35A: upgrades, zones, biome mastery, refuge level, gates; plugs its multipliers into economy and hatch.
                _progression = new ProgressionServices(_save.State, _content, _economy, _hatch, _clock, _save, null, _mastery, _offline);
                // T36: the rendez-vous of the day (welcome, gift, calendar, wishes, journal, return gifts, bond).
                _daily = new DailyVisitServices(_save.State, _content, _economy, _hatch, _mastery, _codex, _progression, _clock, _save);
                // T37: the activities (tickets, rules, runner, rewards). Rendez-vous tickets go to their pool once an
                // activity exists in this build; before that a ticket stays worth Light (doc 01, section 0).
                _activities = new ActivityServices(_save.State, _save.State.Activities, _content, _economy, _hatch, _mastery, _progression,
                    _daily.Bond, _clock, _save, null, _daily.Context);
                ActivityRegistry.RegisterShipped(_activities);
                _activities.Runner.Ended += _daily.OnActivityEnded;
                // T38: grooming games feed their wishes (WISH-019 to 022) and the journal (JRN-033).
                _groomingHooks = new GroomingHooks(_activities, _daily);
                // T39: hide-and-seek games feed their wishes (WISH-051 to 053) and the journal (JRN-016).
                _hideSeekHooks = new HideSeekHooks(_activities, _daily);
                // T40: nut drop games feed their wishes (WISH-054 to 056) and the journal (JRN-017).
                _nutDropHooks = new NutDropHooks(_activities, _daily);
                // T41: memory games feed their wishes (WISH-057 to 059) and the journal (JRN-052).
                _memoryHooks = new MemoryHooks(_activities, _daily);
                // T42: the expeditions (a meta activity: timers in the save, loot drawn at departure) and their wishes
                // (WISH-041 to 046) and journal goals (JRN-024 to 026, the stars of JRN-009).
                _expeditions = new ExpeditionService(_activities);
                _expeditionHooks = new ExpeditionHooks(_expeditions, _daily);
                // T43: the garden (beds, sprouts, harvests, dyes; growth times in the save) and its wishes (WISH-031 to 036)
                // and journal goals (JRN-027 to 030); both GardenAvailable flags are set by the service and its hooks.
                _garden = new GardenService(_activities);
                _gardenHooks = new GardenHooks(_garden, _daily);
                // T44A: the albums (720 stickers, draws with their guarantees, album stars, page rewards) fed by every source.
                _albums = new AlbumService(_save.State.Albums);
                // Only stickers whose art was captured (and whose subject exists) are ever drawn.
                _albums.SetAvailableArt(StickerArtKeys.Captured);
                _albumHooks = new AlbumHooks(_albums, _activities, _daily, _expeditions, _garden);
                // T44B: the 200 achievements (counters since the account began, four tiers) and the titles, frames and poses.
                _profileItems = new ProfileItemService(_save.State.Achievements) { Save = _save };
                _achievements = new AchievementService(_save.State.Achievements) { Save = _save };
                _achievementHooks = new AchievementHooks(_achievements, _profileItems, _save.State, _clock, _content, _economy, _hatch,
                    _mastery, _progression, _activities, _daily, _expeditions, _garden, _albums, _albumHooks);
                // T44C: the refuge journal (63 moments, pictures captured once) and the rare visitors (one or two a day).
                // T46: the mastery colors (10, 20 and 30 of a species); what an older save already earned is granted now.
                _masteryColors = new MasteryColorService(_save.State, _content, _clock)
                {
                    Mastery = _mastery, Economy = _economy, Refuge = _progression.Refuge, AchievementHooks = _achievementHooks, Save = _save,
                };
                _masteryColors.CatchUp();
                _journal = new JournalService(_save.State.Journal, _clock, _save.State) { Save = _save };
                var activities = _activities;
                var daily = _daily;
                _visitors = new VisitorService(_save.State, _content, _clock, _save)
                {
                    Refuge = _progression.Refuge, Rewards = _daily.Context.Granter, Albums = _albums, Mastery = _mastery, Economy = _economy,
                    Essence = _progression.Essence, Achievements = _achievements, AchievementHooks = _achievementHooks,
                    TicketsOpen = () => daily.Config.ActivitiesAvailable && activities.AnyShipped,
                };
                _journalHooks = new JournalHooks(_journal, _save.State, _clock, _content, _hatch, _mastery, _progression, _activities, _daily,
                    _expeditions, _garden, _albums, _achievements, _profileItems, _visitors);
                if (_activities.AnyShipped)
                {
                    _daily.Config.ActivitiesAvailable = true;
                    _daily.Context.Granter.Tickets = _activities.Tickets;
                }
                _save.BeforeSerialize += _offline.CaptureSnapshot;
                _grants = new GrantService(_content, _save.State, _save);
                // T47: accessories worn and the companions (the Meadow Pack's Field Hamster). Every grant or revoke syncs the
                // companions; a pack bought or restored before this build places its companion now, worn items no longer valid come off.
                _cosmetics = new CosmeticService(_save.State, _content, _save) { Grants = _grants };
                _companions = new CompanionService(_save.State, _content, _clock, _save)
                {
                    Grants = _grants, Economy = _economy, Essence = _progression.Essence, Refuge = _progression.Refuge, ProfileItems = _profileItems,
                    Cosmetics = _cosmetics, DayKey = () => daily.Context.TodayKey,
                };
                _daily.Bond.Companions = _companions;
                var journalHooks = _journalHooks;
                var content = _content;
                _companions.Arrived += (species, animal) =>
                    journalHooks.OnCompanionJoined(species, animal, content.Species.TryGet(species, out var d) ? d.DisplayKey : string.Empty);
                _companions.LevelReached += (in CompanionLevelUp up) =>
                {
                    if (up.Level == CompanionService.MaxLevel)
                        journalHooks.OnCompanionBond25(up.Species, up.Animal, content.Species.TryGet(up.Species, out var d) ? d.DisplayKey : string.Empty);
                };
                _companions.Sync();
                _cosmetics.Prune();
                // T48: the events framework (no live event in this build: the calendar is empty). Activities, expeditions, wishes,
                // hatches and album stickers feed it; the Carnet d'or and the Box resolve their per-instance items through it.
                // T49: the shipped calendar (L'Éveil des Terriers 2027) unless a test overrides it; each event's content group is
                // checked by the Addressables hook (its « Télécharger » page when missing).
                _events = new EventService(_save.State, _content, _clock, _save, null, _options.EventCalendarOverride ?? DefaultEventCalendar.Create())
                {
                    Availability = new AddressablesEventContent(),
                    Economy = _economy, Granter = _daily.Context.Granter, Mastery = _mastery, Albums = _albums, ProfileItems = _profileItems,
                    Achievements = _achievements, AchievementHooks = _achievementHooks, Refuge = _progression.Refuge, Journal = _journal,
                    DayKey = () => daily.Context.TodayKey, ActivityShipped = activities.IsShipped,
                    DefaultTreat = () => daily.Context.Rewards().TreatOfNewestBiome,
                };
                _activities.Events = _events;
                _grants.Events = _events;
                // Every moment an event quest counts comes from its own service (the quest gates read the same services).
                _events.AttachSources(new EventQuestSources
                {
                    Hatch = _hatch, Wishes = _daily.Wishes, Runner = _activities.Runner, Activities = _activities, Albums = _albums,
                    Expeditions = _expeditions, Garden = _garden, DailyGift = _daily.Gift, Bond = _daily.Bond, Offline = _offline,
                    Cosmetics = _cosmetics, Upgrades = _progression.Upgrades, WeeklyJournal = _daily.Journal, Biomes = _progression.Store,
                });
                _offers = new OfferService(_content, _save.State, _grants, _save) { Events = _events };
                _store = new StoreService(_content, _offers, CreateStoreProvider(), _clock, _save);
            });

            // Before the store starts: purchases delivered at launch are counted too. Nothing here waits on a network.
            sequence.AddBlocking("Analytics", () =>
            {
                var consent = _options.AnalyticsConsentOverride ?? AnalyticsDeviceStore.LoadConsent();
                var device = new AnalyticsDeviceInfo
                {
                    AnalyticsId = consent == AnalyticsConsent.Denied ? string.Empty : AnalyticsDeviceStore.LoadOrCreateId(),
                    AppVersion = Application.version,
                    BuildType = Debug.isDebugBuild ? "development" : "release",
                    Platform = Application.platform.ToString(),
                    DeviceModel = SystemInfo.deviceModel,
                    OperatingSystem = SystemInfo.operatingSystem,
                    SystemMemoryMb = SystemInfo.systemMemorySize,
                };
                // Tests that move the save also keep their batches next to it (never in the real app folder).
                string directory = _options.AnalyticsDirectory
                                   ?? (_options.SaveDirectory != null
                                       ? Path.Combine(_options.SaveDirectory, "Analytics")
                                       : Path.Combine(Application.persistentDataPath, "Analytics"));
                _analyticsOutbox = new AnalyticsOutbox(directory, CreateAnalyticsTransport());
                _analytics = new AnalyticsService(_save.State, _content, _clock, _save, device, _analyticsOutbox, consent);
                _analytics.Attach(_economy, _hatch, _store);
                _analytics.AttachProgression(_progression);
                _analytics.AttachDaily(_daily);
                _analytics.AttachActivities(_activities);
                _analytics.AttachExpeditions(_expeditions);
                _analytics.AttachGarden(_garden);
                _analytics.AttachAlbums(_albums);
                _analytics.AttachAchievements(_achievements, _profileItems);
                _analytics.AttachJournal(_journal, _visitors);
                _analytics.AttachCosmetics(_cosmetics, _companions);
                _analytics.AttachEvents(_events);
                _analytics.SetDeviceState((int)_quality.CurrentTier, _batterySaver, ReadMemoryMb);
            });

            // Prices and pending purchases load in the background: the refuge never waits for the store.
            sequence.AddBackground("Store", cancellationToken => _store.InitializeAsync(cancellationToken));

            sequence.AddBlocking("Offline", () =>
            {
                OfflineAtBoot = _offline.ResolveReturn();
                _analytics.StartSession(OfflineAtBoot.CreditedSeconds);
                _analytics.OfflineReturn(OfflineAtBoot);
                _daily.OnResume(OfflineAtBoot);
                _activities.OnResume();
                // T48: an event ended while away converts its leftover currency to Light now (once; the summary waits for the view).
                _events.Tick();
                if (OfflineAtBoot.CreditedSeconds > 0.0)
                    GameLog.Info(LogCategory.Save, $"Offline {OfflineAtBoot.CreditedSeconds:F0} s, +{BigValueFormatter.ToDebugString(OfflineAtBoot.Light)} Light");
            });

            // T34B: the voice pool and beds exist before the refuge; settings decide levels and vibrations.
            sequence.AddBlocking("Audio", () =>
            {
                var settings = _save.State.Settings;
                _audio = new AudioService(_audioCatalog, transform, listener: true);
                _audio.ApplySettings(settings.SfxVolume, settings.MusicVolume, _batterySaver);
                _hapticBackend = CreateHapticBackend();
                _haptics = new HapticService(_hapticProfiles, _hapticBackend)
                {
                    Enabled = settings.HapticsEnabled,
                    BatterySaver = _batterySaver,
                };
                if (_options.MuteAudio)
                    AudioListener.volume = 0f;
            });

            // T34C: shared content is loaded once from Addressables (groups Bootstrap and CommonAnimals): one copy in the
            // build, shared later by biome and species packs without duplicating it.
            sequence.AddBlocking("CommonContent", async cancellationToken =>
            {
                if (!string.IsNullOrEmpty(_warmupLabel))
                    _warmupSets = Addressables.LoadAssetsAsync<ShaderWarmupSet>(_warmupLabel, null);
                if (_options.VisualCatalogOverride == null && !string.IsNullOrEmpty(_animalCatalogKey))
                    _animalCatalog = Addressables.LoadAssetAsync<AnimalVisualCatalog>(_animalCatalogKey);
                while ((_warmupSets.IsValid() && !_warmupSets.IsDone) || (_animalCatalog.IsValid() && !_animalCatalog.IsDone))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
                if (_warmupSets.IsValid() && _warmupSets.Status != AsyncOperationStatus.Succeeded)
                    GameLog.Warning(LogCategory.Boot, "Shader warmup sets did not load (label " + _warmupLabel + ").");
                if (_animalCatalog.IsValid() && _animalCatalog.Status != AsyncOperationStatus.Succeeded)
                    throw new InvalidOperationException("The animal catalog did not load: " + _animalCatalogKey);
            });

            // T34: every traced graphics state is built now, behind the loading screen, not at its first display.
            sequence.AddBlocking("ShaderWarmup", async cancellationToken =>
            {
                var sets = new System.Collections.Generic.List<ShaderWarmupSet> { _shaderWarmup };
                if (_warmupSets.IsValid() && _warmupSets.Status == AsyncOperationStatus.Succeeded && _warmupSets.Result != null)
                    sets.AddRange(_warmupSets.Result);
                await ShaderWarmup.RunAsync(sets, cancellationToken);
            });

            sequence.AddBlocking("Presentation", async cancellationToken =>
            {
                _palettes = new PaletteTable(_content);
                var visuals = new AnimalVisualResolver(_content, _palettes);
                var catalog = _options.VisualCatalogOverride != null ? _options.VisualCatalogOverride
                    : _animalCatalog.IsValid() && _animalCatalog.Status == AsyncOperationStatus.Succeeded ? _animalCatalog.Result : null;
                Context = new GameContext(_clock, _content, _save, _economy, _hatch, _quality, _frameRate, _palettes, visuals, catalog,
                    _audio, _haptics, _offline, _localization, _codex, _store, _analytics, _progression, _daily, _activities);
                Context.Expeditions = _expeditions;
                Context.Garden = _garden;
                Context.Albums = _albums;
                Context.Achievements = _achievements;
                Context.ProfileItems = _profileItems;
                Context.Journal = _journal;
                Context.Visitors = _visitors;
                Context.MasteryColors = _masteryColors;
                Context.Cosmetics = _cosmetics;
                Context.Companions = _companions;
                Context.Events = _events;
                _loop = gameObject.AddComponent<GameLoop>();
                _loop.Initialize(Context);
                _loop.SetAnalytics(_analytics);
                _loop.SetFeedback(_audio, _haptics);
                if (_options.LoadTargetScene)
                {
                    var scene = Stopwatch.StartNew();
                    await EnterSceneAsync(_options.TargetScene ?? _targetScene, cancellationToken);
                    _analytics.LoadTimes(Time.realtimeSinceStartupAsDouble * 1000.0, scene.Elapsed.TotalMilliseconds);
                }
            });

            // Cloud save, Remote Config and analytics register here as background steps when they exist.
            return sequence;
        }

        /// <summary>The device's vibrator on Android; nothing elsewhere (editor, PC), or a test recorder.</summary>
        private IHapticBackend CreateHapticBackend()
        {
            if (_options.HapticBackendOverride != null)
                return _options.HapticBackendOverride;
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidHapticBackend(_hapticProfiles);
#else
            return NoHapticBackend.Instance;
#endif
        }

        private static ContentRegistry BuildShippedContent(ContentDatabaseAsset database)
        {
            if (database == null)
                throw new InvalidOperationException("GameBootstrap has no content database.");
            var builder = database.CreateBuilder();
            foreach (var issue in builder.Issues)
                GameLog.Warning(LogCategory.Boot, issue.ToString());
            if (builder.HasErrors)
                throw new InvalidOperationException("Shipped content has errors. See the log.");
            return builder.Build();
        }

        private async Task EnterSceneAsync(string sceneName, CancellationToken cancellationToken)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
                throw new InvalidOperationException("Scene not in build settings: " + sceneName);
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            var scene = SceneManager.GetSceneByName(sceneName);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<ISceneEntry>(out var entry))
                {
                    if (entry is ISceneEntryPreparation preparation)
                        await preparation.PrepareAsync(Context, cancellationToken);
                    _activeEntry = entry;
                    entry.Enter(Context);
                    return;
                }
            }

            GameLog.Warning(LogCategory.Boot, "Scene " + sceneName + " has no ISceneEntry root.");
        }

        private void OnApplicationPause(bool paused)
        {
            if (State != BootState.Ready)
                return;

            if (paused)
            {
                _offline.MarkActive();
                _analytics.EndSession();
                _save.FlushNow();
                _analyticsOutbox.WaitIdle(TimeSpan.FromMilliseconds(100));
            }
            else
            {
                // Away time is paid analytically; the frame loop never replays it.
                var report = _offline.ResolveReturn();
                _analytics.StartSession(report.CreditedSeconds);
                _analytics.OfflineReturn(report);
                _daily.OnResume(report);
                _activities.OnResume();
                // Coming back to the refuge is a visit of the biome on screen (the event quest « visit biomes »).
                if (_hatch != null && _hatch.CameraBiome.IsValid)
                    _events?.OnBiomeVisited(_hatch.CameraBiome);
                _loop.ResetAccumulators();
            }
        }

        /// <summary>
        /// The real platform store plugs in here. Until then the editor and development builds use the sandbox
        /// store (fake Gems), and a release build gets no store at all rather than free Gems.
        /// </summary>
        private IStoreProvider CreateStoreProvider()
        {
            if (_options.StoreProviderOverride != null)
                return _options.StoreProviderOverride;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return new SandboxStoreProvider(_localization.Language);
#else
            return new UnavailableStoreProvider();
#endif
        }

        /// <summary>
        /// The analytics backend plugs in here (doc 09 leaves the provider open). Until it is chosen, batches stay on
        /// the phone, bounded, and are never sent.
        /// </summary>
        private IAnalyticsTransport CreateAnalyticsTransport() => _options.AnalyticsTransportOverride ?? NullAnalyticsTransport.Instance;

        private static uint ReadMemoryMb() => (uint)(Profiler.GetTotalAllocatedMemoryLong() >> 20);

        private void OnDestroy()
        {
            ShaderWarmup.EndBootTrace();
            _quality?.Dispose();
            _audio?.Dispose();
            _audio = null;
            if (_animalCatalog.IsValid())
                Addressables.Release(_animalCatalog);
            _animalCatalog = default;
            if (_warmupSets.IsValid())
                Addressables.Release(_warmupSets);
            _warmupSets = default;
            (_hapticBackend as IDisposable)?.Dispose();
            _hapticBackend = null;
            if (_options != null && _options.MuteAudio)
                AudioListener.volume = 1f;
            _analytics?.Dispose();
            _analytics = null;
            _groomingHooks?.Dispose();
            _groomingHooks = null;
            _hideSeekHooks?.Dispose();
            _hideSeekHooks = null;
            _nutDropHooks?.Dispose();
            _nutDropHooks = null;
            _memoryHooks?.Dispose();
            _memoryHooks = null;
            _expeditionHooks?.Dispose();
            _expeditionHooks = null;
            _expeditions = null;
            _gardenHooks?.Dispose();
            _gardenHooks = null;
            _garden = null;
            _journalHooks?.Dispose();
            _journalHooks = null;
            _journal = null;
            _visitors = null;
            _masteryColors?.Dispose();
            _masteryColors = null;
            _companions?.Dispose();
            _companions = null;
            _cosmetics = null;
            _achievementHooks?.Dispose();
            _achievementHooks = null;
            _achievements = null;
            _profileItems = null;
            _albumHooks?.Dispose();
            _albumHooks = null;
            _albums = null;
            _activities?.Dispose();
            _activities = null;
            _daily?.Dispose();
            _daily = null;
            _store?.Dispose();
            if (_save != null && _offline != null)
                _save.BeforeSerialize -= _offline.CaptureSnapshot;
            _save?.Dispose();
            _palettes?.Dispose();
            _palettes = null;
        }

        private void OnApplicationQuit()
        {
            if (State == BootState.Ready)
            {
                _activeEntry?.Exit();
                _analytics.EndSession();
                _save.FlushNow();
                _analyticsOutbox.WaitIdle(TimeSpan.FromMilliseconds(200));
            }
        }
    }
}
