using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Root of SC_VerticalSlice_Grassland. Receives the GameContext from the bootstrap and
    /// will host the world, egg and HUD controllers as their tasks land.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VerticalSliceEntry : MonoBehaviour, ISceneEntry
    {
        private GameContext _context;

        public bool HasContext => _context != null;

        public void Enter(GameContext context)
        {
            _context = context;
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
