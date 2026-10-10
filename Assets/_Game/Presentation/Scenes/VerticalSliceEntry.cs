using System;
using System.Threading;
using System.Threading.Tasks;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Root of SC_VerticalSlice_Grassland. Receives the GameContext from the bootstrap, streams the grassland
    /// chunks (the pinned start chunk is ready before Enter), applies lighting and quality, frames the camera and
    /// runs the playable refuge loop (RefugeController). The only Update of the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VerticalSliceEntry : MonoBehaviour, ISceneEntry, ISceneEntryPreparation, IRefugeTapHandler, IAmbientLifeListener
    {
        [Tooltip("Chunks of the biome, streamed through Addressables. The pinned one hosts the refuge.")]
        [SerializeField] private WorldChunkSet _chunkSet;
        [Tooltip("Ground footprint of the camera is clamped to this distance for rays above the horizon (m).")]
        [SerializeField] private float _footprintMaxDistance = 30f;
        [SerializeField] private LightingProfile _lighting;
        [Tooltip("Morning and evening moods blended with the day profile over the local time (T33). Optional.")]
        [SerializeField] private LightingProfile _morning;
        [SerializeField] private LightingProfile _evening;
        [Tooltip("Night mood, only when an event asks for it (T33B). Optional.")]
        [SerializeField] private LightingProfile _night;
        [Tooltip("Light rain, rainbow and Magnifique sun rays of the biome (T33B). Optional.")]
        [SerializeField] private WeatherKit _weatherKit;
        [Tooltip("World densities per quality tier (scatter, ambient life, clouds, height fog).")]
        [SerializeField] private WorldQualityProfile _worldQuality;
        [Tooltip("Butterflies, bees and birds of the biome, spawned at the chunks' VFX anchors (T33). Optional.")]
        [SerializeField] private AmbientLifeKit _ambientLife;
        [SerializeField] private Light _sun;
        [SerializeField] private RefugeCameraRig _cameraRig;
        [Tooltip("Pan limits around the egg spawn (half size, m). Keeps the island edge from filling the screen.")]
        [SerializeField] private Vector2 _panHalfSize = new Vector2(3.5f, 4f);
        [Tooltip("T34B: music and ambience of the biome (BiomeAudioSet address, group Audio_Grassland). Empty: silent beds.")]
        [SerializeField] private string _biomeAudioKey = "audio.grassland";

        [Header("Refuge loop")]
        [Tooltip("T34C: hatch staging (egg prefab, feedback effects) in the Addressables group CommonAnimals.")]
        [SerializeField] private string _hatchConfigKey = "animals.hatch";
        [SerializeField] private string _eggId = "egg.grassland.basic";
        [Tooltip("T35A: hedge and sign of the next zone (ZoneKit address, group Biome_Grassland_Kit). Empty: no hedge.")]
        [SerializeField] private string _zoneKitKey = "zones.grassland";
        [Tooltip("HUD component implementing IRefugeHud (UI assembly).")]
        [SerializeField] private MonoBehaviour _hud;
        [Tooltip("Offline return screen implementing IOfflineReturnView (UI assembly).")]
        [SerializeField] private MonoBehaviour _offlineReturn;
        [Tooltip("Codex screen implementing ICodexView (UI assembly).")]
        [SerializeField] private MonoBehaviour _codex;
        [Tooltip("Shop screen implementing IShopView (UI assembly).")]
        [SerializeField] private MonoBehaviour _shop;
        [Tooltip("Offer modal implementing IOfferModalView (UI assembly).")]
        [SerializeField] private MonoBehaviour _offerModal;
        [Tooltip("T34A animal quick sheet implementing IAnimalSheetView (UI assembly).")]
        [SerializeField] private MonoBehaviour _animalSheet;
        [Tooltip("T34A modal shell implementing IModalView (UI assembly).")]
        [SerializeField] private MonoBehaviour _modal;
        [Header("T35A progression screens (UI assembly)")]
        [SerializeField] private MonoBehaviour _upgrades;
        [SerializeField] private MonoBehaviour _zoneOpen;
        [SerializeField] private MonoBehaviour _refugeLevel;
        [SerializeField] private MonoBehaviour _levelReward;
        /// <summary>T35B: PF_UI_RewardSummary (screen 028), the card at the end of a multi hatch.</summary>
        [SerializeField] private MonoBehaviour _rewardSummary;
        [SerializeField] private MonoBehaviour _biomeGate;
        [Header("T36 rendez-vous of the day (UI assembly)")]
        /// <summary>PF_UI_Today (screen 031), implementing ITodayView.</summary>
        [SerializeField] private MonoBehaviour _today;
        /// <summary>PF_UI_WeeklyJournal (screen 032), implementing IJournalView.</summary>
        [SerializeField] private MonoBehaviour _weeklyJournal;
        /// <summary>The wish card of screen 002, implementing IWishCardView.</summary>
        [SerializeField] private MonoBehaviour _wishCard;
        /// <summary>PF_World_WishBubble: the pooled bubble above an animal with a wish (WishBubbleView).</summary>
        [SerializeField] private GameObject _wishBubblePrefab;
        [Header("T37 activities (UI assembly)")]
        /// <summary>PF_UI_ActivityHub (screen 029), implementing IActivityHubView.</summary>
        [SerializeField] private MonoBehaviour _activityHub;
        /// <summary>PF_UI_ActivityHud (screen 030), implementing IActivityHudView.</summary>
        [SerializeField] private MonoBehaviour _activityHud;
        /// <summary>PF_UI_ActivityResult (screen 030), implementing IActivityResultView.</summary>
        [SerializeField] private MonoBehaviour _activityResult;
        /// <summary>The « no ticket » card, implementing ITicketsEmptyView.</summary>
        [SerializeField] private MonoBehaviour _ticketsEmpty;

        private GameContext _context;
        private RefugeController _refuge;
        private WorldStreamer _streamer;
        private ScatterRenderer _scatter;
        private AmbientLife _life;
        private WorldAtmosphere _atmosphere;
        private WeatherSystem _weather;
        private WorldChunk _startChunk;
        private Action<int, ChunkStreamState> _onChunkState;
        private Action<QualityState> _onQuality;
        private AsyncOperationHandle<BiomeAudioSet> _biomeAudio;
        private AsyncOperationHandle<HatchPresentationConfig> _hatch;
        private AsyncOperationHandle<ZoneKit> _zoneKit;
        private RefugeZones _zones;
        private ProgressionPresenter _progressionPresenter;
        private DailyVisitPresenter _dailyPresenter;
        private ActivityPresenter _activityPresenter;
        private AlbumScreenPresenter _albumPresenter;
        private ProfileScreenPresenter _profilePresenter;
        private JournalScreenPresenter _journalPresenter;
        private JournalCaptureLayer _journalCaptures;
        private WishBubbleLayer _bubbles;

        public bool HasContext => _context != null;
        public WorldChunkSet ChunkSet => _chunkSet;
        public WorldStreamer Streamer => _streamer;
        /// <summary>The pinned chunk instance once prepared (streamed, not part of the scene file).</summary>
        public WorldChunk StartChunk => _startChunk;
        public RefugeCameraRig CameraRig => _cameraRig;
        public LightingProfile Lighting => _lighting;
        public ScatterRenderer Scatter => _scatter;
        public AmbientLife Life => _life;
        public WorldAtmosphere Atmosphere => _atmosphere;
        public WeatherSystem Weather => _weather;

        private WorldQualityProfile.Tier WorldQuality(QualityTier tier) =>
            _worldQuality != null ? _worldQuality.Get(tier) : WorldQualityProfile.Default(tier);
        public RefugeController Refuge => _refuge;

        /// <summary>T35A: the open zones, the hedge and the sign (null without progression services).</summary>
        public RefugeZones Zones => _zones;

        /// <summary>T35A: the Upgrades, zone, refuge level and gate screens (null without progression services).</summary>
        public ProgressionPresenter ProgressionScreens => _progressionPresenter;

        /// <summary>T36: the rendez-vous of the day (null without daily services).</summary>
        public DailyVisitPresenter DailyVisit => _dailyPresenter;

        /// <summary>T37: the Play button, the hub, games and their result (null without activity services).</summary>
        public ActivityPresenter Activities => _activityPresenter;

        /// <summary>T44A: the albums screen and its reveal layer (null in scenes without the albums).</summary>
        public AlbumScreenPresenter AlbumScreen => _albumPresenter;

        /// <summary>T44B, tests: lets go of the scene's profile presenter so a test drives the HUD's achievements button alone.</summary>
        public void DetachProfile()
        {
            _profilePresenter?.Dispose();
            _profilePresenter = null;
        }

        /// <summary>T44A, tests: lets go of the scene's albums presenter so a test drives the HUD's Albums button alone.</summary>
        public void DetachAlbums()
        {
            _albumPresenter?.Dispose();
            _albumPresenter = null;
            if (_refuge != null)
                _refuge.AlbumsOpenCheck = null;
        }

        public ZoneKit ZoneKit => _zoneKit.IsValid() && _zoneKit.Status == AsyncOperationStatus.Succeeded ? _zoneKit.Result : null;

        /// <summary>Hatch staging once loaded (null before PrepareAsync or when it failed).</summary>
        public HatchPresentationConfig HatchConfig => _hatch.IsValid() && _hatch.Status == AsyncOperationStatus.Succeeded ? _hatch.Result : null;

        /// <summary>The biome's beds once loaded (null before PrepareAsync or when the key is empty or failed).</summary>
        public BiomeAudioSet BiomeAudio => _biomeAudio.IsValid() && _biomeAudio.Status == AsyncOperationStatus.Succeeded ? _biomeAudio.Result : null;

        /// <summary>Loads the pinned start chunk (and starts the neighbours' preload) behind the loading screen.</summary>
        public async Task PrepareAsync(GameContext context, CancellationToken cancellationToken)
        {
            if (_chunkSet == null)
            {
                GameLog.Warning(LogCategory.Presentation, "VerticalSliceEntry has no chunk set: no world.");
                return;
            }
            _streamer?.Dispose();
            // T34B: the biome's music and ambience load beside the start chunk (small asset; the clips stream).
            if (!string.IsNullOrEmpty(_biomeAudioKey) && !_biomeAudio.IsValid())
                _biomeAudio = Addressables.LoadAssetAsync<BiomeAudioSet>(_biomeAudioKey);
            if (!string.IsNullOrEmpty(_hatchConfigKey) && !_hatch.IsValid())
                _hatch = Addressables.LoadAssetAsync<HatchPresentationConfig>(_hatchConfigKey);
            if (!string.IsNullOrEmpty(_zoneKitKey) && !_zoneKit.IsValid())
                _zoneKit = Addressables.LoadAssetAsync<ZoneKit>(_zoneKitKey);
            // T34: each world part follows its own dimension of the adaptive quality state.
            var quality = context.Quality.State;
            _streamer = new WorldStreamer(_chunkSet, new AddressablesChunkSource(), context.Palettes, quality.Ground);
            var ground = WorldQuality(quality.Ground);
            _scatter = new ScatterRenderer(context.Palettes, ground.scatterDensity);
            _scatter.SetDistanceFade(ground.scatterFadeStart, ground.scatterFadeEnd);
            _life = _ambientLife != null ? new AmbientLife(_ambientLife, context.Palettes, WorldQuality(quality.Life).ambientLifeDensity) : null;
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
                GameLog.Error(LogCategory.Presentation, "The start chunk did not load: the refuge cannot start.");
            for (int i = 0; i < _streamer.Count; i++)
            {
                if (_streamer.GetState(i) == ChunkStreamState.Active)
                {
                    _scatter.Add(_streamer.GetChunk(i));
                    _life?.Add(_streamer.GetChunk(i));
                }
            }
            _onChunkState = OnChunkState;
            _streamer.StateChanged += _onChunkState;
            while ((_biomeAudio.IsValid() && !_biomeAudio.IsDone) || (_hatch.IsValid() && !_hatch.IsDone) || (_zoneKit.IsValid() && !_zoneKit.IsDone))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
            if (_hatch.IsValid() && _hatch.Status != AsyncOperationStatus.Succeeded)
                GameLog.Error(LogCategory.Presentation, "Hatch staging did not load: " + _hatchConfigKey);
            if (_biomeAudio.IsValid() && _biomeAudio.Status != AsyncOperationStatus.Succeeded)
                GameLog.Warning(LogCategory.Presentation, "Biome audio did not load: " + _biomeAudioKey);
            if (_zoneKit.IsValid() && _zoneKit.Status != AsyncOperationStatus.Succeeded)
                GameLog.Warning(LogCategory.Presentation, "Zone kit did not load: " + _zoneKitKey);
        }

        public void Enter(GameContext context)
        {
            _context = context;
            var quality = context.Quality.State;
            var camera = _cameraRig != null ? _cameraRig.Camera : Camera.main;
            if (_lighting != null)
            {
                _lighting.Apply(_sun, camera, quality);
                _atmosphere = new WorldAtmosphere(_morning, _lighting, _evening, _sun, camera, WorldQuality(quality.Weather), _night);
                _atmosphere.Tick(Time.unscaledTime);
                _weather = _weatherKit != null ? new WeatherSystem(_weatherKit, _atmosphere, WorldQuality(quality.Weather)) : null;
            }
            else
                PresentationQuality.Apply(quality);
            if (_streamer == null)
                GameLog.Warning(LogCategory.Presentation, "Entered without PrepareAsync: no streamed world.");
            if (_startChunk != null && _cameraRig != null && _startChunk.EggSpawn != null)
            {
                var egg = _startChunk.EggSpawn.position;
                UpdateCameraBounds();
                _cameraRig.SnapTo(egg);
            }

            var hatchConfig = HatchConfig;
            if (_startChunk != null && hatchConfig != null && context.VisualCatalog != null && context.Content.Eggs.TryGet(ContentId.FromSource(_eggId), out _))
            {
                ApplyReduceMotion(context.State.Settings.ReduceMotion);
                // T35A: the arrival zone shows a set number of animals, each open zone a few more (within the device budget).
                int allowance = context.Progression != null
                    ? RefugeController.ArrivalVisibleAnimals + context.Progression.Zones.VisibleAnimalBonus(context.Progression.FirstBiome)
                    : -1;
                _refuge = new RefugeController(context, _startChunk, _cameraRig, _hud as IRefugeHud, hatchConfig, _eggId, transform,
                    _offlineReturn as IOfflineReturnView, _codex as ICodexView, _shop as IShopView, _offerModal as IOfferModalView,
                    _animalSheet as IAnimalSheetView, _modal as IModalView, null, allowance);
                _refuge.SetSummaryView(_rewardSummary as IRewardSummaryView);
                var input = _cameraRig != null ? _cameraRig.GetComponent<RefugeCameraInput>() : null;
                if (input != null)
                    input.SetTapHandler(this);
                // No-collision rule: insects fly around the animals, birds fly off when one comes near.
                _life?.SetAnimals(_refuge.Scheduler);
                if (context.Progression != null && _streamer != null)
                {
                    // T35A: animals walk across every open zone; the next one is closed by a hedge and a sign.
                    _zones = new RefugeZones(context, _streamer, _chunkSet, ZoneKit, _refuge, _scatter, _life, _cameraRig, transform);
                    _zones.CameraRangeChanged += UpdateCameraBounds;
                    UpdateCameraBounds();
                }
                if (context.Progression != null)
                {
                    // T35A: the Upgrades button and screen, the zone window, the refuge level and the biome gates.
                    _progressionPresenter = new ProgressionPresenter(context, _refuge, _zones, _cameraRig, _hud as IProgressionHud,
                        _upgrades as IUpgradesView, _zoneOpen as IZoneOpenView, _refugeLevel as IRefugeLevelView,
                        _levelReward as ILevelRewardView, _biomeGate as IBiomeGateView);
                    _refuge.Progression = _progressionPresenter;
                }
                if (context.Daily != null)
                {
                    // T36: the Today button, the wish card and bubbles, Today and the journal, the welcome's last steps.
                    _dailyPresenter = new DailyVisitPresenter(context, _refuge, _progressionPresenter, _hud as IDailyHud, _today as ITodayView,
                        _weeklyJournal as IJournalView, _wishCard as IWishCardView);
                    _refuge.Daily = _dailyPresenter;
                    if (_wishBubblePrefab != null)
                    {
                        // The 9 bubbles are built now, behind the loading screen.
                        _bubbles = new WishBubbleLayer(_refuge.Scheduler, _wishBubblePrefab, transform, _cameraRig, _dailyPresenter);
                        _bubbles.Prewarm();
                        _bubbles.Listener = _dailyPresenter;
                        _bubbles.ReduceMotion = context.State.Settings.ReduceMotion;
                        _refuge.Bubbles = _bubbles;
                        _dailyPresenter.Bubbles = _bubbles;
                        _life?.SetBubbles(_bubbles);
                    }
                }
                if (context.Activities != null)
                {
                    // T37: the Play button, the hub, the activity HUD, the result card and the « no ticket » card.
                    var host = new ActivityStageHost(context, _refuge, _cameraRig, transform);
                    // A stage with its own set turns the refuge's rendering off while it plays (doc Mini-jeux, section 1).
                    host.SetWorldVisible = SetWorldVisible;
                    host.World = this;
                    _activityPresenter = new ActivityPresenter(context, _hud as IPlayHud, _activityHub as IActivityHubView,
                        _activityHud as IActivityHudView, _activityResult as IActivityResultView, _ticketsEmpty as ITicketsEmptyView, host);
                    _activityPresenter.Daily = _dailyPresenter;
                    _refuge.Activities = _activityPresenter;
                    if (_dailyPresenter != null)
                        _dailyPresenter.Activities = _activityPresenter;
                    // T44A: the albums screen and the sticker reveal (never over an activity, a screen or a hatch).
                    if (context.Albums != null)
                    {
                        var refuge = _refuge;
                        var activities = _activityPresenter;
                        _albumPresenter = AlbumScreenPresenter.Create(context, context.Albums, activities, _hud as IAlbumHud);
                        if (_albumPresenter != null)
                        {
                            var albums = _albumPresenter;
                            _albumPresenter.RevealBlocked = () => activities.IsOpen || refuge.Hatching
                                || (refuge.ModalOpen && !albums.IsOpen);
                            refuge.AlbumsOpenCheck = () => albums.IsOpen;
                        }
                    }
                    // T44B: the achievements screen and the profile card (titles, frames, poses).
                    if (context.Achievements != null && context.ProfileItems != null)
                    {
                        _profilePresenter = ProfileScreenPresenter.Create(context, context.Achievements, context.ProfileItems,
                            _activityPresenter, _hud as IProfileHud);
                        if (_profilePresenter != null)
                        {
                            var profile = _profilePresenter;
                            var albumsScreen = _albumPresenter;
                            _refuge.AlbumsOpenCheck = () => profile.IsOpen || (albumsScreen != null && albumsScreen.IsOpen);
                        }
                    }
                    // T44C: the journal (opened from the profile; the profile ticks it), its picture captures and the visitors.
                    if (context.Journal != null)
                    {
                        if (_profilePresenter != null)
                            _journalPresenter = JournalScreenPresenter.Create(context, context.Journal, _activityPresenter, _profilePresenter);
                        _journalCaptures = JournalCaptureLayer.Create(context, context.Journal, _refuge, _cameraRig, transform);
                    }
                    if (context.Visitors != null)
                        _refuge.Visitors = context.Visitors;
                }
            }
            _onQuality = ApplyQuality;
            context.Quality.StateChanged += _onQuality;
            var beds = BiomeAudio;
            if (beds != null)
            {
                context.Audio.SetBeds(beds.music, beds.ambience, beds.fadeSeconds);
                context.Audio.SetAmbienceLayers(beds.ambienceEvening, beds.rain);
                UpdateAmbienceMix();
            }
            if (_activityPresenter != null)
                _activityPresenter.RefugeBeds = beds;
            _life?.SetListener(this);
            GameLog.Info(LogCategory.Presentation, "Vertical slice entered.");
        }

        /// <summary>T34A: the player's Reduce Motion setting reaches every screen of the scene.</summary>
        public void ApplyReduceMotion(bool reduce)
        {
            if (_bubbles != null)
                _bubbles.ReduceMotion = reduce;
            foreach (var screen in new[] { _hud, _offlineReturn, _codex, _shop, _offerModal, _animalSheet, _modal, _upgrades, _zoneOpen,
                         _refugeLevel, _levelReward, _biomeGate, _rewardSummary, _today, _weeklyJournal, _wishCard, _activityHub, _activityHud,
                         _activityResult, _ticketsEmpty })
                if (screen is IMotionSettings motion)
                    motion.ReduceMotion = reduce;
        }

        /// <summary>
        /// T34 adaptive quality: one dimension changed. Life, ground details (scatter, fade, secondary props), weather
        /// (drops, sun rays, clouds, height fog, water), real or blob shadows. The renderer part (quality level, shadow
        /// distance) is the quality service's. No allocation; colors and moods stay as they are.
        /// </summary>
        public void ApplyQuality(QualityState quality)
        {
            var ground = WorldQuality(quality.Ground);
            if (_scatter != null)
            {
                _scatter.SetDensity(ground.scatterDensity);
                _scatter.SetDistanceFade(ground.scatterFadeStart, ground.scatterFadeEnd);
            }
            _streamer?.SetQuality(quality.Ground);
            _life?.SetDensity(WorldQuality(quality.Life).ambientLifeDensity);
            var weather = WorldQuality(quality.Weather);
            _weather?.SetQuality(weather);
            _atmosphere?.SetQuality(weather);
            var camera = _cameraRig != null ? _cameraRig.Camera : Camera.main;
            LightingProfile.ApplyQuality(_sun, camera, quality);
            _refuge?.RefreshPresentationQuality();
        }

        public void Exit()
        {
            // T34B: beds stop before their clips are released with the biome.
            _context?.Audio.StopBeds();
            if (_biomeAudio.IsValid())
                Addressables.Release(_biomeAudio);
            _biomeAudio = default;
            if (_context != null && _onQuality != null)
                _context.Quality.StateChanged -= _onQuality;
            _onQuality = null;
            _life?.SetAnimals(null);
            _life?.SetListener(null);
            if (_refuge != null)
            {
                _refuge.Daily = null;
                _refuge.Activities = null;
                _refuge.Bubbles = null;
            }
            _activityPresenter?.Dispose();
            if (_refuge != null)
                _refuge.Visitors = null;
            _journalCaptures?.Dispose();
            _journalCaptures = null;
            _journalPresenter?.Dispose();
            _journalPresenter = null;
            _profilePresenter?.Dispose();
            _profilePresenter = null;
            _albumPresenter?.Dispose();
            _albumPresenter = null;
            _activityPresenter = null;
            _life?.SetBubbles(null);
            _bubbles?.Dispose();
            _bubbles = null;
            _dailyPresenter?.Dispose();
            _dailyPresenter = null;
            _progressionPresenter?.Dispose();
            _progressionPresenter = null;
            if (_zones != null)
            {
                _zones.CameraRangeChanged -= UpdateCameraBounds;
                _zones.Dispose();
                _zones = null;
            }
            _refuge?.Dispose();
            _refuge = null;
            if (_streamer != null)
            {
                if (_onChunkState != null)
                    _streamer.StateChanged -= _onChunkState;
                _streamer.Dispose();
                _streamer = null;
            }
            _scatter?.Clear();
            _scatter = null;
            _life?.Clear();
            _life = null;
            _atmosphere = null;
            _weather = null;
            _startChunk = null;
            _context = null;
            // After the refuge is gone (its pools are destroyed): the staging assets can leave memory.
            if (_hatch.IsValid())
                Addressables.Release(_hatch);
            if (_zoneKit.IsValid())
                Addressables.Release(_zoneKit);
            _zoneKit = default;
            _hatch = default;
        }

        public void OnTap(Vector2 screenPosition)
        {
            var tapCamera = _cameraRig != null ? _cameraRig.Camera : null;
            // T35A: the next zone's hedge or sign shows what opening it brings.
            if (_zones != null && _refuge != null && tapCamera != null && !_refuge.ModalOpen && !_refuge.Hatching
                && _zones.TryTap(tapCamera.ScreenPointToRay(screenPosition)))
                return;
            _refuge?.OnTap(screenPosition);
            // Butterflies and birds near the tapped ground flee: the meadow reacts to the finger.
            var camera = _cameraRig != null ? _cameraRig.Camera : null;
            if (_life == null || camera == null || _startChunk == null || _startChunk.EggSpawn == null)
                return;
            var ray = camera.ScreenPointToRay(screenPosition);
            var ground = new Plane(Vector3.up, _startChunk.EggSpawn.position);
            if (ground.Raycast(ray, out float distance))
                _life.Startle(ray.GetPoint(distance), 1.8f);
        }

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
            if (_zones != null)
            {
                // T35A: the open zones only, and a glimpse past the next zone's hedge.
                _zones.GetCameraRange(out float zonesMin, out float zonesMax);
                minX = Mathf.Min(minX, zonesMin);
                maxX = Mathf.Max(maxX, zonesMax);
            }
            else if (_streamer != null && _streamer.TryGetBounds(out var world))
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
            _zones?.OnChunkState(index, state);
            if (_scatter == null)
                return;
            var chunk = _streamer.GetChunk(index);
            if (state == ChunkStreamState.Active)
            {
                _scatter.Add(chunk);
                _life?.Add(chunk);
            }
            else if (chunk != null)
            {
                _scatter.Remove(chunk);
                _life?.Remove(chunk);
            }
        }

        private Rect Footprint()
        {
            var camera = _cameraRig != null ? _cameraRig.Camera : null;
            if (camera == null)
                return new Rect(-1f, -1f, 2f, 2f);
            return WorldStreamer.GroundFootprint(camera, _footprintMaxDistance);
        }

        /// <summary>
        /// T38: the refuge's world rendering on or off (an activity set renders alone): its camera, so nothing of the world
        /// is drawn; the simulation and the animals keep living.
        /// </summary>
        private void SetWorldVisible(bool visible)
        {
            var camera = _cameraRig != null ? _cameraRig.Camera : null;
            if (camera != null)
                camera.enabled = visible;
        }

        /// <summary>T37: the app leaves mid-game: the game pauses (the pause card waits on return).</summary>
        private void OnApplicationPause(bool paused) => _activityPresenter?.OnApplicationPause(paused);

        private void Update()
        {
            // PrepareAsync ticks the streamer itself until Enter.
            if (_context != null)
                _streamer?.Tick(Footprint(), Time.unscaledTime);
            _refuge?.Tick(Time.time, Time.deltaTime);
            _zones?.Tick(Time.time);
            _progressionPresenter?.Tick(Time.time, Time.unscaledDeltaTime);
            _dailyPresenter?.Tick(Time.time, Time.unscaledDeltaTime);
            _activityPresenter?.Tick(Time.deltaTime, Time.unscaledDeltaTime);
            _albumPresenter?.Tick(Time.unscaledDeltaTime);
            _profilePresenter?.Tick(Time.unscaledDeltaTime);
            _journalCaptures?.Tick(Time.unscaledTime);
            _weather?.Tick(Time.unscaledTime);
            _atmosphere?.Tick(Time.unscaledTime);
            var camera = _cameraRig != null ? _cameraRig.Camera : null;
            _scatter?.Render(camera);
            if (_life != null)
            {
                _life.Tick(Time.deltaTime, Time.time);
                _life.Render(camera, Time.time);
            }
            if (_weather != null && _cameraRig != null)
                _weather.Render(camera, _refuge != null ? _refuge.ViewFocus : _cameraRig.Focus);
            if (_context != null)
                UpdateAmbienceMix();
        }

        /// <summary>
        /// T34B2: evening weight from the local hour (the crickets come in from 19:00 to 20:30 and leave from 5:30 to
        /// 7:00; the night mood of events is full evening), rain weight from the shower. No allocation.
        /// </summary>
        private void UpdateAmbienceMix()
        {
            float evening = 0f;
            if (_atmosphere != null)
                evening = _atmosphere.Night ? 1f : EveningWeight(_atmosphere.Hour);
            _context.Audio.SetAmbienceMix(evening, _weather != null ? _weather.Rain : 0f);
        }

        public static float EveningWeight(float hour)
        {
            if (hour >= 20.5f || hour <= 5.5f)
                return 1f;
            if (hour > 19f)
                return Mathf.SmoothStep(0f, 1f, (hour - 19f) / 1.5f);
            if (hour < 7f)
                return Mathf.SmoothStep(1f, 0f, (hour - 5.5f) / 1.5f);
            return 0f;
        }

        void IAmbientLifeListener.BirdTookOff(Vector3 position) => _context?.Audio.Play(PocketWilds.Services.Feedback.AudioEventId.BirdTakeOff, position, default);

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
