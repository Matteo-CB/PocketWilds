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
using UnityEngine.Profiling;
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
    /// The player enters the sanctuary without waiting for cloud, analytics or remote config.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        /// <summary>Test/dev hook, consumed by the next boot. Never set by production code.</summary>
        public static BootOptions PendingOptions;

        [SerializeField] private string _targetScene = "SC_VerticalSlice_Grassland";
        [SerializeField] private ContentDatabaseAsset _contentDatabase;
        [SerializeField] private AnimalVisualCatalog _visualCatalog;

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

        public BootState State { get; private set; } = BootState.NotStarted;
        public GameContext Context { get; private set; }
        public SaveLoadResult LoadResult => _loadResult;

        /// <summary>Away time paid at boot (OfflineService). Also handed to the scene through the context.</summary>
        public OfflineReport OfflineAtBoot { get; private set; }

        public BootSequence Sequence { get; private set; }

        /// <summary>Analytics (T29). Null before the Analytics step.</summary>
        public AnalyticsService Analytics => _analytics;
        public AnalyticsOutbox AnalyticsOutbox => _analyticsOutbox;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _options = PendingOptions ?? new BootOptions();
            PendingOptions = null;
        }

        private async Awaitable Start()
        {
            State = BootState.Running;
            Sequence = BuildSequence();
            try
            {
                await Sequence.RunAsync(destroyCancellationToken);
                State = BootState.Ready;
                GameLog.Info(LogCategory.Boot, "Boot complete.");
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
                _quality = new QualityService();
                _quality.Apply(_options.QualityOverride ?? device.QualityOverride);
                _frameRate = new FrameRateService();
                _frameRate.Apply(device.BatterySaver);
                _batterySaver = device.BatterySaver;
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
                _save.BeforeSerialize += _offline.CaptureSnapshot;
                _grants = new GrantService(_content, _save.State, _save);
                _offers = new OfferService(_content, _save.State, _grants, _save);
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
                _analytics.SetDeviceState((int)_quality.CurrentTier, _batterySaver, ReadMemoryMb);
            });

            // Prices and pending purchases load in the background: the sanctuary never waits for the store.
            sequence.AddBackground("Store", cancellationToken => _store.InitializeAsync(cancellationToken));

            sequence.AddBlocking("Offline", () =>
            {
                OfflineAtBoot = _offline.ResolveReturn();
                _analytics.StartSession(OfflineAtBoot.CreditedSeconds);
                _analytics.OfflineReturn(OfflineAtBoot);
                if (OfflineAtBoot.CreditedSeconds > 0.0)
                    GameLog.Info(LogCategory.Save, $"Offline {OfflineAtBoot.CreditedSeconds:F0} s, +{BigValueFormatter.ToDebugString(OfflineAtBoot.Light)} Light");
            });

            sequence.AddBlocking("Presentation", async cancellationToken =>
            {
                _palettes = new PaletteTable(_content);
                var visuals = new AnimalVisualResolver(_content, _palettes);
                var catalog = _options.VisualCatalogOverride != null ? _options.VisualCatalogOverride : _visualCatalog;
                Context = new GameContext(_clock, _content, _save, _economy, _hatch, _quality, _frameRate, _palettes, visuals, catalog,
                    NullAudioService.Instance, NullHapticService.Instance, _offline, _localization, _codex, _store, _analytics);
                _loop = gameObject.AddComponent<GameLoop>();
                _loop.Initialize(Context);
                _loop.SetAnalytics(_analytics);
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
            _analytics?.Dispose();
            _analytics = null;
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
