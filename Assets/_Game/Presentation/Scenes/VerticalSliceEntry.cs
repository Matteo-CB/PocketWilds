using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Root of SC_VerticalSlice_Grassland. Receives the GameContext from the bootstrap, applies lighting,
    /// chunk palettes and quality density, frames the camera and runs the playable sanctuary loop
    /// (SanctuaryController). The only Update of the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VerticalSliceEntry : MonoBehaviour, ISceneEntry, ISanctuaryTapHandler
    {
        [SerializeField] private WorldChunk _startChunk;
        [SerializeField] private LightingProfile _lighting;
        [SerializeField] private Light _sun;
        [SerializeField] private SanctuaryCameraRig _cameraRig;
        [Tooltip("Pan limits around the egg spawn (half size, m). Keeps the island edge from filling the screen.")]
        [SerializeField] private Vector2 _panHalfSize = new Vector2(3.5f, 4f);

        [Header("Sanctuary loop")]
        [SerializeField] private HatchPresentationConfig _hatchConfig;
        [SerializeField] private string _eggId = "egg.grassland.basic";
        [Tooltip("HUD component implementing ISanctuaryHud (UI assembly).")]
        [SerializeField] private MonoBehaviour _hud;

        private GameContext _context;
        private SanctuaryController _sanctuary;

        public bool HasContext => _context != null;
        public WorldChunk StartChunk => _startChunk;
        public SanctuaryCameraRig CameraRig => _cameraRig;
        public LightingProfile Lighting => _lighting;
        public SanctuaryController Sanctuary => _sanctuary;

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

            if (_startChunk != null && _hatchConfig != null && context.VisualCatalog != null && context.Content.Eggs.TryGet(ContentId.FromSource(_eggId), out _))
            {
                _sanctuary = new SanctuaryController(context, _startChunk, _cameraRig, _hud as ISanctuaryHud, _hatchConfig, _eggId, transform);
                var input = _cameraRig != null ? _cameraRig.GetComponent<SanctuaryCameraInput>() : null;
                if (input != null)
                    input.SetTapHandler(this);
            }
            GameLog.Info(LogCategory.Presentation, "Vertical slice entered.");
        }

        public void Exit()
        {
            _sanctuary?.Dispose();
            _sanctuary = null;
            _context = null;
        }

        public void OnTap(Vector2 screenPosition) => _sanctuary?.OnTap(screenPosition);

        private void Update()
        {
            _sanctuary?.Tick(Time.time, Time.deltaTime);
        }

        private void OnDestroy() => Exit();

        private void Start()
        {
            if (_context == null)
                GameLog.Warning(LogCategory.Presentation, "Started without bootstrap. Use 'Pocket Wilds/Play From Bootstrap'.");
        }
    }
}
