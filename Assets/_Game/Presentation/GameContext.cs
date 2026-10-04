using PocketWilds.Content;
using PocketWilds.Core;
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
            QualityService quality,
            FrameRateService frameRate)
        {
            Clock = clock;
            Content = content;
            Save = save;
            Economy = economy;
            Quality = quality;
            FrameRate = frameRate;
        }

        public IGameClock Clock { get; }
        public ContentRegistry Content { get; }
        public SaveService Save { get; }
        public PlayerState State => Save.State;
        public EconomyService Economy { get; }
        public QualityService Quality { get; }
        public FrameRateService FrameRate { get; }
    }

    /// <summary>Implemented by one root component per gameplay scene.</summary>
    public interface ISceneEntry
    {
        void Enter(GameContext context);
        void Exit();
    }
}
