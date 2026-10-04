using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Device;
using PocketWilds.Services.Feedback;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        private QualityService _quality;
        private FrameRateService _frameRate;
        private GameLoop _loop;
        private PaletteTable _palettes;
        private ISceneEntry _activeEntry;

        public BootState State { get; private set; } = BootState.NotStarted;
        public GameContext Context { get; private set; }
        public SaveLoadResult LoadResult => _loadResult;

        /// <summary>Wall time since the last save, measured at boot. Resolved by OfflineService (T18).</summary>
        public TimeSpan OfflineDeltaAtBoot { get; private set; }

        public BootSequence Sequence { get; private set; }

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
                _quality.Apply(device.QualityOverride);
                _frameRate = new FrameRateService();
                _frameRate.Apply(device.BatterySaver);
            });

            sequence.AddBlocking("ContentRegistry", () =>
            {
                _content = _options.ContentOverride ?? BuildShippedContent(_contentDatabase);
            });

            sequence.AddBlocking("Save", async cancellationToken =>
            {
                _clock = new RuntimeGameClock();
                string directory = _options.SaveDirectory ?? Path.Combine(Application.persistentDataPath, "Saves");
                var store = new SaveFileStore(directory);
                var migrations = new SaveMigrationPipeline(SaveSchema.CurrentVersion, Array.Empty<ISaveMigration>());
                var saveOptions = new SaveServiceOptions { AppVersion = Application.version };
                _save = new SaveService(store, new BinarySaveSerializer(), migrations, _clock, saveOptions);
                _loadResult = await _save.LoadOrCreateAsync(cancellationToken);
                GameLog.Info(LogCategory.Save, $"Save {_loadResult.Outcome}, revision {(_loadResult.State != null ? _loadResult.State.Meta.Revision : 0)}");
                if (_loadResult.Outcome == SaveLoadOutcome.NewerSchemaRefused)
                    throw new InvalidOperationException("Save was made by a newer version. Update the game.");
            });

            sequence.AddBlocking("GameClock", () =>
            {
                OfflineDeltaAtBoot = _clock.GetOfflineDelta(_save.State.Meta.LastSaveUtc);
            });

            sequence.AddBlocking("Simulation", () =>
            {
                _economy = new EconomyService(_save.State, _content, EconomyConfig.Default, _save);
            });

            sequence.AddBlocking("Presentation", async cancellationToken =>
            {
                _palettes = new PaletteTable(_content);
                var visuals = new AnimalVisualResolver(_content, _palettes);
                Context = new GameContext(_clock, _content, _save, _economy, _quality, _frameRate, _palettes, visuals, _visualCatalog,
                    NullAudioService.Instance, NullHapticService.Instance);
                _loop = gameObject.AddComponent<GameLoop>();
                _loop.Initialize(Context);
                if (_options.LoadTargetScene)
                    await EnterSceneAsync(_options.TargetScene ?? _targetScene, cancellationToken);
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
                _save.FlushNow();
            else
                _loop.ResetAccumulators();
        }

        private void OnDestroy()
        {
            _palettes?.Dispose();
            _palettes = null;
        }

        private void OnApplicationQuit()
        {
            if (State == BootState.Ready)
            {
                _activeEntry?.Exit();
                _save.FlushNow();
            }
        }
    }
}
