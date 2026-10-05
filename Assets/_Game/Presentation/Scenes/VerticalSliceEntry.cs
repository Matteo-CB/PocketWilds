using System;
using System.Threading;
using System.Threading.Tasks;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Root of SC_VerticalSlice_Grassland. Receives the GameContext from the bootstrap, streams the grassland
    /// chunks (the pinned start chunk is ready before Enter), applies lighting and quality, frames the camera and
    /// runs the playable sanctuary loop (SanctuaryController). The only Update of the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VerticalSliceEntry : MonoBehaviour, ISceneEntry, ISceneEntryPreparation, ISanctuaryTapHandler
    {
        [Tooltip("Chunks of the biome, streamed through Addressables. The pinned one hosts the sanctuary.")]
        [SerializeField] private WorldChunkSet _chunkSet;
        [Tooltip("Ground footprint of the camera is clamped to this distance for rays above the horizon (m).")]
        [SerializeField] private float _footprintMaxDistance = 30f;
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
        [Tooltip("Offline return screen implementing IOfflineReturnView (UI assembly).")]
        [SerializeField] private MonoBehaviour _offlineReturn;
        [Tooltip("Codex screen implementing ICodexView (UI assembly).")]
        [SerializeField] private MonoBehaviour _codex;

        private GameContext _context;
        private SanctuaryController _sanctuary;
        private WorldStreamer _streamer;
        private WorldChunk _startChunk;
        private Action<int, ChunkStreamState> _onChunkState;

        public bool HasContext => _context != null;
        public WorldChunkSet ChunkSet => _chunkSet;
        public WorldStreamer Streamer => _streamer;
        /// <summary>The pinned chunk instance once prepared (streamed, not part of the scene file).</summary>
        public WorldChunk StartChunk => _startChunk;
        public SanctuaryCameraRig CameraRig => _cameraRig;
        public LightingProfile Lighting => _lighting;
        public SanctuaryController Sanctuary => _sanctuary;

        /// <summary>Loads the pinned start chunk (and starts the neighbours' preload) behind the loading screen.</summary>
        public async Task PrepareAsync(GameContext context, CancellationToken cancellationToken)
        {
            if (_chunkSet == null)
            {
                GameLog.Warning(LogCategory.Presentation, "VerticalSliceEntry has no chunk set: no world.");
                return;
            }
            _streamer?.Dispose();
            _streamer = new WorldStreamer(_chunkSet, new AddressablesChunkSource(), context.Palettes, context.Quality.CurrentTier);
            while (!_streamer.PinnedSettled)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _streamer.Tick(Footprint(), Time.unscaledTime);
                await Task.Yield();
            }
            for (int i = 0; i < _streamer.Count; i++)
            {
                if (_streamer.GetEntry(i).pinned && _streamer.GetState(i) == ChunkStreamState.Active)
                {
                    _startChunk = _streamer.GetChunk(i);
                    break;
                }
            }
            if (_startChunk == null)
                GameLog.Error(LogCategory.Presentation, "The start chunk did not load: the sanctuary cannot start.");
            _onChunkState = OnChunkState;
            _streamer.StateChanged += _onChunkState;
        }

        public void Enter(GameContext context)
        {
            _context = context;
            var tier = context.Quality.CurrentTier;
            if (_lighting != null)
                _lighting.Apply(_sun, _cameraRig != null ? _cameraRig.Camera : Camera.main, tier);
            else
                PresentationQuality.Apply(tier);
            if (_streamer == null)
                GameLog.Warning(LogCategory.Presentation, "Entered without PrepareAsync: no streamed world.");
            if (_startChunk != null && _cameraRig != null && _startChunk.EggSpawn != null)
            {
                var egg = _startChunk.EggSpawn.position;
                UpdateCameraBounds();
                _cameraRig.SnapTo(egg);
            }

            if (_startChunk != null && _hatchConfig != null && context.VisualCatalog != null && context.Content.Eggs.TryGet(ContentId.FromSource(_eggId), out _))
            {
                _sanctuary = new SanctuaryController(context, _startChunk, _cameraRig, _hud as ISanctuaryHud, _hatchConfig, _eggId, transform,
                    _offlineReturn as IOfflineReturnView, _codex as ICodexView);
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
            if (_streamer != null)
            {
                if (_onChunkState != null)
                    _streamer.StateChanged -= _onChunkState;
                _streamer.Dispose();
                _streamer = null;
            }
            _startChunk = null;
            _context = null;
        }

        public void OnTap(Vector2 screenPosition) => _sanctuary?.OnTap(screenPosition);

        /// <summary>
        /// Pan limits: around the egg as before, widened to every chunk the world can show (the start chunk's
        /// margin to its own edge applies to the whole strip). A failed chunk shrinks them back.
        /// </summary>
        private void UpdateCameraBounds()
        {
            if (_cameraRig == null || _startChunk == null || _startChunk.EggSpawn == null)
                return;
            var egg = _startChunk.EggSpawn.position;
            float minX = egg.x - _panHalfSize.x, maxX = egg.x + _panHalfSize.x;
            if (_streamer != null && _streamer.TryGetBounds(out var world))
            {
                float margin = _chunkSet.HalfSize - _panHalfSize.x;
                minX = Mathf.Min(minX, world.xMin + margin);
                maxX = Mathf.Max(maxX, world.xMax - margin);
            }
            _cameraRig.SetBounds(new Vector3((minX + maxX) * 0.5f, egg.y, egg.z), new Vector2((maxX - minX) * 0.5f, _panHalfSize.y));
        }

        private void OnChunkState(int index, ChunkStreamState state)
        {
            if (state == ChunkStreamState.Failed)
                UpdateCameraBounds();
        }

        private Rect Footprint()
        {
            var camera = _cameraRig != null ? _cameraRig.Camera : null;
            if (camera == null)
                return new Rect(-1f, -1f, 2f, 2f);
            return WorldStreamer.GroundFootprint(camera, _footprintMaxDistance);
        }

        private void Update()
        {
            // PrepareAsync ticks the streamer itself until Enter.
            if (_context != null)
                _streamer?.Tick(Footprint(), Time.unscaledTime);
            _sanctuary?.Tick(Time.time, Time.deltaTime);
        }

        private void OnDestroy() => Exit();

        private void Start()
        {
            // The bootstrap prepares (streams) before Enter: give it a moment before warning.
            if (_context == null)
                Invoke(nameof(WarnIfNotBootstrapped), 2f);
        }

        private void WarnIfNotBootstrapped()
        {
            if (_context == null && _streamer == null)
                GameLog.Warning(LogCategory.Presentation, "Started without bootstrap. Use 'Pocket Wilds/Play From Bootstrap'.");
        }
    }
}
