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
            IAnalytics analytics = null,
            ProgressionServices progression = null,
            DailyVisitServices daily = null,
            ActivityServices activities = null)
        {
            Progression = progression;
            Daily = daily;
            Activities = activities;
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

        /// <summary>
        /// Light upgrades, zones, biome mastery, refuge level, biome gates, Light decorations, treat stand (T35A).
        /// Null in headless tests that do not need it.
        /// </summary>
        public ProgressionServices Progression { get; }

        /// <summary>T36: the rendez-vous of the day (null in scenes started without it).</summary>
        public DailyVisitServices Daily { get; }

        /// <summary>T37: the activities: tickets, rules, the runner, rewards (null in scenes started without them).</summary>
        public ActivityServices Activities { get; }

        /// <summary>T42: the expeditions (set by the bootstrap after the context; null in scenes started without them).</summary>
        public ExpeditionService Expeditions { get; set; }

        /// <summary>T43: the garden (set by the bootstrap after the context; null in scenes started without it).</summary>
        public GardenService Garden { get; set; }

        /// <summary>T44A: the albums (set by the bootstrap after the context; null in scenes started without them).</summary>
        public AlbumService Albums { get; set; }

        /// <summary>T44B: the 200 achievements and the profile's titles, frames and poses (set by the bootstrap).</summary>
        public AchievementService Achievements { get; set; }

        public ProfileItemService ProfileItems { get; set; }

        /// <summary>T44C: the refuge journal and the rare visitors (set by the bootstrap).</summary>
        public JournalService Journal { get; set; }

        public VisitorService Visitors { get; set; }

        /// <summary>T46: the mastery colors given at species mastery 10, 20 and 30.</summary>
        public MasteryColorService MasteryColors { get; set; }
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
