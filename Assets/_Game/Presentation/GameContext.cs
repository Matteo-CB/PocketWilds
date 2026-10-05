using System.Threading;
using System.Threading.Tasks;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Analytics;
using PocketWilds.Services.Feedback;
using PocketWilds.Services.Save;
using PocketWilds.Services.Store;
using PocketWilds.Simulation;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Services built by GameBootstrap and handed explicitly to scene entry points.
    /// Not a global locator: only ISceneEntry.Enter receives it.
    /// </summary>
    public sealed class GameContext
    {
        public GameContext(
            IGameClock clock,
            ContentRegistry content,
            SaveService save,
            EconomyService economy,
            HatchService hatch,
            QualityService quality,
            FrameRateService frameRate,
            PaletteTable palettes,
            AnimalVisualResolver visuals,
            AnimalVisualCatalog visualCatalog,
            IAudioService audio,
            IHapticService haptics,
            OfflineService offline = null,
            ILocalization localization = null,
            CodexService codex = null,
            StoreService store = null,
            IAnalytics analytics = null)
        {
            Clock = clock;
            Content = content;
            Save = save;
            Economy = economy;
            Hatch = hatch;
            Quality = quality;
            FrameRate = frameRate;
            Palettes = palettes;
            Visuals = visuals;
            VisualCatalog = visualCatalog;
            Audio = audio ?? NullAudioService.Instance;
            Haptics = haptics ?? NullHapticService.Instance;
            Offline = offline;
            Localization = localization;
            Codex = codex;
            Store = store;
            Analytics = analytics ?? NullAnalytics.Instance;
        }

        public IGameClock Clock { get; }
        public ContentRegistry Content { get; }
        public SaveService Save { get; }
        public PlayerState State => Save.State;
        public EconomyService Economy { get; }
        public HatchService Hatch { get; }
        public QualityService Quality { get; }
        public FrameRateService FrameRate { get; }
        public PaletteTable Palettes { get; }
        public AnimalVisualResolver Visuals { get; }

        /// <summary>Species visual key to prefab. Null in headless tests.</summary>
        public AnimalVisualCatalog VisualCatalog { get; }

        /// <summary>Named audio events. No-op until the audio service and clips land.</summary>
        public IAudioService Audio { get; }
        public IHapticService Haptics { get; }

        /// <summary>Away-time resolution. Null in headless tests that do not need it.</summary>
        public OfflineService Offline { get; }

        /// <summary>Player-facing text. Null in headless tests: screens fall back to the keys.</summary>
        public ILocalization Localization { get; }

        /// <summary>Codex view model over the collection. Null in headless tests that do not need it.</summary>
        public CodexService Codex { get; }

        /// <summary>Shop: offers, grants and the platform store behind one API (T28). Null when no shop runs.</summary>
        public StoreService Store { get; }

        /// <summary>Screens, offers and funnel steps seen by presentation (T29). Never null.</summary>
        public IAnalytics Analytics { get; }
    }

    /// <summary>Implemented by one root component per gameplay scene.</summary>
    public interface ISceneEntry
    {
        void Enter(GameContext context);
        void Exit();
    }

    /// <summary>
    /// Optional on a scene entry: asynchronous preparation (pinned world chunks...) that the bootstrap awaits
    /// before Enter, while the loading screen is still up.
    /// </summary>
    public interface ISceneEntryPreparation
    {
        Task PrepareAsync(GameContext context, CancellationToken cancellationToken);
    }
}
