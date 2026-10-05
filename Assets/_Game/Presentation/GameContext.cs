using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Services.Save;
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
            IHapticService haptics)
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
    }

    /// <summary>Implemented by one root component per gameplay scene.</summary>
    public interface ISceneEntry
    {
        void Enter(GameContext context);
        void Exit();
    }
}
