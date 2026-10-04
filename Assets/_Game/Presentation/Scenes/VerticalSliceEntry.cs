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
        [SerializeField] private LightingProfile _lighting;
        [SerializeField] private Light _sun;
        [SerializeField] private SanctuaryCameraRig _cameraRig;
        [Tooltip("Pan limits around the egg spawn (half size, m). Keeps the island edge from filling the screen.")]
        [SerializeField] private Vector2 _panHalfSize = new Vector2(3.5f, 4f);

        private GameContext _context;

        public bool HasContext => _context != null;
        public WorldChunk StartChunk => _startChunk;
        public SanctuaryCameraRig CameraRig => _cameraRig;
        public LightingProfile Lighting => _lighting;

        public void Enter(GameContext context)
        {
            _context = context;
            var tier = context.Quality.CurrentTier;
            if (_lighting != null)
                _lighting.Apply(_sun, _cameraRig != null ? _cameraRig.Camera : Camera.main, tier);
            else
                PresentationQuality.Apply(tier);
            if (_startChunk != null)
            {
                int missing = _startChunk.Bind(context.Palettes);
                if (missing > 0)
                    GameLog.Warning(LogCategory.Presentation, missing + " chunk renderers reference unknown palettes.");
                _startChunk.ApplyQuality(tier);
                if (_cameraRig != null && _startChunk.EggSpawn != null)
                {
                    var egg = _startChunk.EggSpawn.position;
                    _cameraRig.SetBounds(egg, _panHalfSize);
                    _cameraRig.SnapTo(egg);
                }
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
