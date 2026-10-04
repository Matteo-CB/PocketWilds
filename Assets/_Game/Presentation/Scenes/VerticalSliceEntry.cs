using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Root of SC_VerticalSlice_Grassland. Receives the GameContext from the bootstrap, binds the world
    /// chunk palettes and quality density, and will host the egg, animals and HUD controllers as their
    /// tasks land.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VerticalSliceEntry : MonoBehaviour, ISceneEntry
    {
        [SerializeField] private WorldChunk _startChunk;

        private GameContext _context;

        public bool HasContext => _context != null;
        public WorldChunk StartChunk => _startChunk;

        public void Enter(GameContext context)
        {
            _context = context;
            if (_startChunk != null)
            {
                int missing = _startChunk.Bind(context.Palettes);
                if (missing > 0)
                    GameLog.Warning(LogCategory.Presentation, missing + " chunk renderers reference unknown palettes.");
                _startChunk.ApplyQuality(context.Quality.CurrentTier);
            }
            GameLog.Info(LogCategory.Presentation, "Vertical slice entered.");
        }

        public void Exit()
        {
            _context = null;
        }

        private void Start()
        {
            if (_context == null)
                GameLog.Warning(LogCategory.Presentation, "Started without bootstrap. Use 'Pocket Wilds/Play From Bootstrap'.");
        }
    }
}
