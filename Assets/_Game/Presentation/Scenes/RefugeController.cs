using System;
using System.Collections.Generic;
using System.Threading;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Analytics;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Where the refuge's animals live (T35A): the open zones side by side. Area, ground heights, every volume they
    /// must not enter, points of interest. The arrays are copied by the scheduler.
    /// </summary>
    public readonly struct RefugeWalkData
    {
        public readonly WanderArea Area;
        public readonly GroundSurface Ground;
        public readonly Obstacle[] Obstacles;
        public readonly InterestPoint[] Interests;

        public RefugeWalkData(WanderArea area, GroundSurface ground, Obstacle[] obstacles, InterestPoint[] interests)
        {
            Area = area;
            Ground = ground;
            Obstacles = obstacles;
            Interests = interests;
        }

        /// <summary>One chunk alone (tests, a world without zones).</summary>
        public static RefugeWalkData FromChunk(WorldChunk chunk) =>
            new RefugeWalkData(chunk.WanderArea, chunk.Ground, chunk.GetWorldObstacles(), chunk.GetWorldInterests());
    }

    /// <summary>
    /// The playable refuge loop (T17): animals from the save live in the meadow, an egg waits on its
    /// spawn point, a touch (on the egg or the hatch button) buys and hatches it, the new animal joins the
    /// meadow and the next egg appears. Plain class ticked by VerticalSliceEntry: one Update for the scene,
    /// pooled views, no per-animal logic, no allocation in steady state. Away time comes back through the
    /// offline return screen; its Light counts up into the HUD when collected (T18).
    /// </summary>
    public sealed class RefugeController : IRefugeActions, IHatchListener, IOfflineReturnActions, ICodexActions, IAnimalSheetActions,
        IAnimalSheetGroomActions,
        IModalActions, IAnimalVoiceListener, IEggChoiceActions, IMultiHatchListener, IRewardSummaryActions, IAnimalSheetTreatActions,
        IDisposable
    {
        private const float NextEggDelay = 0.45f;
        // Room around the egg spot no animal enters: the egg (0.33 m) or a giant newborn squirrel standing on it.
        public const float EggSpotRadius = 0.55f;
        // The egg's own footprint: the next egg appears once no body is within it (the newborn is bounding off).
        public const float EggBodyRadius = 0.36f;
        // Animals cross the space in front of the egg but never settle in it: the newborn always has a way out, the
        // next egg never waits and the egg stays in sight however full the refuge is.
        public const float EggApronRadius = 1.3f;
        private const float RepeatWindowSeconds = 15f;
        private const float EggTapRadius = 0.32f;
        private const float AnimalTapRadius = 0.34f;
        private const float CollectCountSeconds = 0.8f;
        // T34A rare glints: one visible rare animal at a time, every 2.2 s (twice as long on Low: half the particles).
        private const float RareGlintSeconds = 2.2f;
        // T34A: a second touch on the same animal within this time opens its sheet (doc 05: reaction first, then the sheet).
        private const float SecondTapSeconds = 2.5f;
        /// <summary>T34B: after a touch, that animal does not add its own chatter (the touch already spoke).</summary>
        private const float AfterTouchQuietSeconds = 2.5f;
        // T34A: the tutorial hand waits this long on the first egg before showing.
        private const float TapHintDelay = 1.2f;
        private const float NotEnoughToastSeconds = 4f;
        // Bond levels on the sheet: the economy's bond range (1000 points) in five steps.
        private const int BondPointsPerLevel = 200;
        private const int BondPointsForMax = 1000;

        /// <summary>Delay after a hatch before a moment offer may show: the new animal has landed first.</summary>
        private const float MomentOfferDelay = 1.6f;

        private readonly GameContext _context;
        private readonly WorldChunk _chunk;
        private readonly RefugeCameraRig _rig;
        private readonly IRefugeHud _hud;
        private readonly IOfflineReturnView _offlineView;
        private readonly ICodexView _codexView;
        private readonly IAnimalSheetView _sheetView;
        private readonly IModalView _modalView;
        private readonly RewardCell[] _returnCells = new RewardCell[RewardCells.Max];
        private readonly RefugeCameraInput _input;
        private ContentId _eggId;
        private EggDefinition _egg;
        private uint _eggVisual;

        private readonly AnimalViewPool _animals;
        private readonly GameObjectPool<EggView> _eggs;
        private readonly FeedbackVfx _vfx;
        private readonly HatchSequencePlayer _hatch;
        private readonly AnimalScheduler _scheduler;
        // One pool and behavior profile per species with a visual (T23): the visible budget is shared.
        private readonly int[] _speciesHandles;
        private readonly BehaviorProfile[] _speciesProfiles;
        private readonly Dictionary<ContentId, int> _speciesSlot;
        private readonly int _visibleBudget;
        private int _visibleAllowance;
        private WanderArea _walkArea;
        private readonly AnimalView[] _views;
        private readonly int[] _agentByView;
        private readonly RareGlowKind[] _rareByView;
        private readonly Transform _root;

        private EggView _waitingEgg;
        private bool _noticeShown;
        private AnimalView _hatchingAnimal;
        private uint _hatchingInstance;
        private ContentId _hatchingSpecies;
        private uint _hatchingSeed;
        private RareGlowKind _hatchingRare;
        private int _glintCursor;
        private float _nextGlintTime;
        private int _tappedView = -1;
        private float _tappedTime = -100f;
        private float _idleOnEgg;
        private bool _tapHintShown;
        private float _lastNotEnoughToast = -100f;
        private int _shopUnlockState = -1;
        private RevealInfo _pendingReveal;
        private bool _hasPendingReveal;
        private float _nextEggTime = -1f;
        private float _time;
        private float _lastHatchEnd = -100f;
        private BigValue _eggCost;
        private BigValue _heldLight;
        private BigValue _heldAtCollect;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private MasteryGain _pendingMastery;
        private bool _masteryShown = true;
        private float _collectTime = -1f;
        private readonly ShopPresenter _shop;
        private float _momentOfferTime = -1f;

        // T35B egg choice and multi hatch (doc 02 Œufs, sections 3 to 6).
        private const float ChoiceCheckSeconds = 0.25f;
        private const float MultiEggHeight = 0.4f;
        private const float MultiEggSpacing = 0.42f;
        /// <summary>Ten eggs: a wider pitch than three, so the back row shows between the front one's eggs.</summary>
        private const float MultiTenSpacing = 0.46f;
        /// <summary>Free space kept between two newborns of a group, at their largest (the reveal's overshoot).</summary>
        private const float MultiBodyGap = 0.05f;

        /// <summary>
        /// The group's layout from the species' measured bodies (rule « aucune collision »): newborns face the camera, so
        /// a row's bodies reach <see cref="_multiRowGap"/> in depth (the longest nose plus the longest tail, at the reveal's
        /// overshoot) and <see cref="_multiPitch"/> across. The two rows of ten stand that far apart, the eggs that far
        /// apart in a row, and each spot is checked for a whole body, not only for the egg.
        /// </summary>
        private readonly float _multiPitch;
        private readonly float _multiRowGap;
        private readonly float _multiFrontZ;
        private readonly float _multiBackZ;
        private readonly float _multiBodyRadius;
        private readonly float _multiBodyOffset;
        /// <summary>
        /// How long a group waits for its whole circle (formation and bodies) to be free of animals, resting or walking: no
        /// animal settles in front of the egg (the apron), so the last group's newborns and passers-by are gone within a
        /// moment. After that, the spots are checked one by one and blocked eggs take the free points of the rings.
        /// </summary>
        private const float MultiWaitSeconds = 4f;
        /// <summary>
        /// The walk out of a multi hatch: places beyond the group's bodies by this much and more (a newborn resting there
        /// leaves the next group's formation free), a few directions around the way out.
        /// </summary>
        private static readonly float[] ExitMargins = { 0.6f, 0.95f, 1.3f };
        private static readonly float[] ExitAngles = { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 80f, -80f };
        private const float ExitSpacing = 0.5f;
        private const float ExitLaneRadius = 0.2f;
        private const float ExitLaneGap = 0.34f;
        private const float ExitStagger = 0.05f;
        private const float ExitBackRowDelay = 0.35f;
        private readonly EggChoice _choice;
        private readonly IEggChoiceHud _choiceHud;
        private readonly EggSlot[] _slots = new EggSlot[EggChoice.MaxSlots];
        private readonly PortraitSlot[] _portraits = new PortraitSlot[3];
        private readonly ContentId[] _targets = new ContentId[EggOdds.MaxTargets];
        private readonly EggOdds _odds = new EggOdds();
        private float _nextChoiceCheck;
        private readonly MultiHatchPlayer _multi;
        private readonly HatchResult[] _multiResults = new HatchResult[MultiHatchPlayer.MaxEggs];
        private readonly EggView[] _multiEggs = new EggView[MultiHatchPlayer.MaxEggs];
        private readonly AnimalView[] _multiAnimals = new AnimalView[MultiHatchPlayer.MaxEggs];
        private readonly ContentId[] _multiSpecies = new ContentId[MultiHatchPlayer.MaxEggs];
        private readonly RarityTier[] _multiRarity = new RarityTier[MultiHatchPlayer.MaxEggs];
        private readonly HatchNews[] _multiNews = new HatchNews[MultiHatchPlayer.MaxEggs];
        private readonly RareGlowKind[] _multiRare = new RareGlowKind[MultiHatchPlayer.MaxEggs];
        private readonly Vector3[] _multiSpots = new Vector3[MultiHatchPlayer.MaxEggs];
        private int _multiCount;
        private int _multiPending;
        private float _multiRequestTime;
        private IRewardSummaryView _summaryView;
        private readonly Vector3[] _multiExits = new Vector3[MultiHatchPlayer.MaxEggs];
        private readonly float[] _multiDelays = new float[MultiHatchPlayer.MaxEggs];
        private readonly SummaryEntry[] _summary = new SummaryEntry[MultiHatchPlayer.MaxEggs];
        private Vector3 _eggCenter;
        // A hatch ended: the moment offer may come once the next egg is ready and the player pauses (T33C: the egg
        // now waits for the newborn to bound off its spot, so the pause is counted from the egg, not the hatch).
        private bool _momentOfferArmed;

        /// <summary>
        /// Visible animals of the arrival zone; each open zone adds ProgressionConfig.ZoneVisibleAnimals (doc 04 Heure,
        /// section 4.2), always within the device tier's budget.
        /// </summary>
        public const int ArrivalVisibleAnimals = 20;

        public RefugeController(GameContext context, WorldChunk chunk, RefugeCameraRig rig, IRefugeHud hud,
            HatchPresentationConfig hatchConfig, string eggSourceId, Transform root, IOfflineReturnView offlineView = null,
            ICodexView codexView = null, IShopView shopView = null, IOfferModalView offerView = null,
            IAnimalSheetView sheetView = null, IModalView modalView = null, RefugeWalkData? walk = null, int visibleAllowance = -1)
        {
            _sheetView = sheetView;
            _modalView = modalView;
            _context = context;
            _chunk = chunk;
            _rig = rig;
            _hud = hud;
            _offlineView = offlineView;
            _codexView = codexView;
            _input = rig != null ? rig.GetComponent<RefugeCameraInput>() : null;
            _root = root;
            // T35B: the egg on the pedestal is the player's choice (the last one used), the scene's egg by default.
            _choice = new EggChoice(context, ContentId.FromSource(eggSourceId));
            SetEggData(_choice.Selected);

            var settings = SchedulerSettings.ForQuality(context.Quality.CurrentTier, false);
            // Visible population: every tier A slot plus a calm background (Performance Test Matrix P3 shape).
            _visibleBudget = settings.MaxTierA + (context.Quality.CurrentTier == QualityTier.Low ? 10 : 20);
            _views = new AnimalView[_visibleBudget];
            _agentByView = new int[_visibleBudget];
            _rareByView = new RareGlowKind[_visibleBudget];
            for (int i = 0; i < _agentByView.Length; i++)
                _agentByView[i] = AnimalScheduler.InvalidHandle;

            _animals = new AnimalViewPool(root);
            var catalog = context.VisualCatalog;
            if (catalog == null)
                throw new InvalidOperationException("No animal visual catalog.");
            var handles = new List<int>(8);
            var profiles = new List<BehaviorProfile>(8);
            _speciesSlot = new Dictionary<ContentId, int>(8);
            var table = context.Content.Species;
            for (int i = 0; i < table.Count; i++)
            {
                var species = table[i];
                // Each species may show the whole budget: its pool is sized like the budget.
                if (!catalog.TryGet(species.VisualKey, out var entry) || entry.prefab == null)
                    continue;
                _speciesSlot.Add(species.Id, handles.Count);
                handles.Add(_animals.Register(entry.visualKey, entry.prefab, _visibleBudget + 1, _visibleBudget + 2));
                profiles.Add(entry.behavior);
            }
            if (handles.Count == 0)
                throw new InvalidOperationException("Visual catalog has no species prefab.");
            _speciesHandles = handles.ToArray();
            _speciesProfiles = profiles.ToArray();
            float front = 0f, back = 0f, half = 0f;
            for (int i = 0; i < _speciesProfiles.Length; i++)
            {
                var body = _speciesProfiles[i].body;
                front = Mathf.Max(front, body.front);
                back = Mathf.Max(back, -body.back);
                half = Mathf.Max(half, body.halfWidth);
            }
            float grow = Mathf.Max(1f, hatchConfig.revealOvershoot);
            _multiPitch = 2f * half * grow + MultiBodyGap;
            _multiRowGap = (front + back) * grow + MultiBodyGap;
            _multiFrontZ = -0.4f * _multiRowGap;
            _multiBackZ = 0.6f * _multiRowGap;
            // Facing the camera, a body runs from its nose (toward the camera, -z) to its tail (+z).
            _multiBodyRadius = 0.5f * (front + back) * grow;
            _multiBodyOffset = 0.5f * (back - front) * grow;
            // The waiting egg plus a full × 10 (doc 02 Œufs, section 5: pools sized for ten eggs).
            _eggs = new GameObjectPool<EggView>(hatchConfig.eggPrefab, root, MultiHatchPlayer.MaxEggs + 1, MultiHatchPlayer.MaxEggs + 1);
            _eggs.Warmup(MultiHatchPlayer.MaxEggs + 1);
            // T34A: every feedback effect warmed here, counts by the device tier (Gate 10: nothing instantiates in play).
            _vfx = hatchConfig.vfx != null ? new FeedbackVfx(hatchConfig.vfx, root, PresentationQuality.Tier) : null;
            _hatch = new HatchSequencePlayer(hatchConfig, _vfx, context.Audio, context.Haptics);
            _multi = new MultiHatchPlayer(hatchConfig, _vfx, context.Audio, context.Haptics);
            _hatchConfig = hatchConfig;
            _visibleAllowance = visibleAllowance > 0 ? Math.Min(visibleAllowance, _visibleBudget) : _visibleBudget;
            var walkData = walk ?? RefugeWalkData.FromChunk(chunk);
            _walkArea = walkData.Area;
            _scheduler = new AnimalScheduler(_visibleBudget + 2, settings, walkData.Area);
            _scheduler.SetVoiceListener(this);
            _scheduler.SetInterests(walkData.Interests);
            // T33C no-collision rule: animals stand on the ground, never enter a prop, the water or the egg spot.
            _scheduler.SetGround(walkData.Ground);
            _scheduler.SetObstacles(walkData.Obstacles);
            if (chunk.EggSpawn != null)
            {
                _eggCenter = chunk.EggSpawn.position;
                _scheduler.SetEggSpot(_eggCenter, EggSpotRadius, EggBodyRadius, EggApronRadius);
            }

            if (context.Store != null && (shopView != null || offerView != null))
            {
                _shop = new ShopPresenter(context.Store, context.Localization, shopView, offerView, context.Audio, context.Haptics,
                    SetWorldVisible, _lifetime.Token, context.Analytics);
                _shop.Prewarm();
            }

            SpawnSavedAnimals();
            WarmupSpares();
            PresentEgg();
            _hud?.Bind(this);
            _choiceHud = _hud as IEggChoiceHud;
            _choiceHud?.BindEggs(this);
            PushChoice(false);
            // T34A: the sheet, the modal and the HUD's hidden parts are built once now (T28 lesson: first opens never hitch).
            var loc = context.Localization;
            _sheetView?.Prewarm(loc);
            _modalView?.Prewarm(loc);
            _hud?.PrewarmFeedback(loc.Get(FeedbackText.RevealNewSpecies), loc.Get(FeedbackText.ToastNotEnoughLight));
        }

        public int VisibleAnimals { get; private set; }
        public HatchSequencePlayer Hatch => _hatch;

        /// <summary>T35B: a × 3 or × 10 hatch staging.</summary>
        public MultiHatchPlayer Multi => _multi;

        /// <summary>A single or multi hatch is playing (screens, offers and taps wait).</summary>
        public bool Hatching => _hatch.IsPlaying || _multi.IsPlaying;

        public EggChoice Choice => _choice;
        public ContentId EggId => _eggId;
        public FeedbackVfx Vfx => _vfx;
        public AnimalScheduler Scheduler => _scheduler;

        /// <summary>Time of the last Tick, the clock the scheduler runs on.</summary>
        public float SimulationTime => _time;
        public EggView WaitingEgg => _waitingEgg;
        public BigValue EggCost => _eggCost;

        /// <summary>Light the HUD shows: the wallet minus offline Light not counted in yet.</summary>
        public BigValue DisplayedLight
        {
            get
            {
                var light = _context.Economy.Light;
                if (_heldLight.IsZero)
                    return light;
                return BigValue.Max(BigValue.Zero, BigValue.Subtract(light, _heldLight));
            }
        }

        public bool OfflineScreenOpen => _offlineView != null && _offlineView.IsOpen;
        public bool CodexOpen => _codexView != null && _codexView.IsOpen;
        public bool ShopOpen => _shop != null && (_shop.ShopOpen || _shop.OfferOpen);
        public ShopPresenter Shop => _shop;
        public bool SheetOpen => _sheetView != null && _sheetView.IsOpen;
        public bool DialogOpen => _modalView != null && _modalView.IsOpen;

        /// <summary>T35A: the progression screens (Upgrades, zone, refuge level, gate), set by the scene entry.</summary>
        public ProgressionPresenter Progression { get; set; }

        public bool ProgressionOpen => Progression != null && Progression.IsOpen;

        /// <summary>T36: the rendez-vous of the day (Today, the journal, the wish card); null without daily services.</summary>
        public DailyVisitPresenter Daily { get; set; }

        public bool DailyOpen => Daily != null && Daily.IsOpen;

        /// <summary>T37: the activities (hub, games, result); null without activity services.</summary>
        public ActivityPresenter Activities { get; set; }

        public bool ActivitiesOpen => Activities != null && Activities.IsOpen;

        /// <summary>T38: the animals shown in the refuge right now (their instance ids), for the activities' animal choice.</summary>
        public int CopyVisibleAnimals(uint[] into)
        {
            int n = 0;
            for (int i = 0; i < _views.Length && n < into.Length; i++)
            {
                if (_views[i] == null || _agentByView[i] == AnimalScheduler.InvalidHandle)
                    continue;
                into[n++] = _views[i].OwnerInstanceId;
            }
            return n;
        }

        /// <summary>T38: the sheet's « Brosser » button (doc 01 S-ACT-001: the animal is chosen in its sheet).</summary>
        public void GroomAnimal(uint instanceId)
        {
            if (Activities == null || !Activities.CanGroom(out _))
                return;
            _sheetView?.Close();
            Activities.GroomAnimal(instanceId);
        }

        // ---------------- T39: an activity played in the refuge itself (Hide and Seek) ----------------

        private bool _actorsHidden;
        private readonly List<Renderer> _actorRenderers = new List<Renderer>(16);
        private bool _cameraHeld;
        private bool _cameraReturning;
        private float _cameraT = 1f;
        private float _cameraSeconds;
        private Vector3 _cameraFromPosition, _cameraToPosition, _cameraHomePosition;
        private Quaternion _cameraFromRotation, _cameraToRotation, _cameraHomeRotation;
        private float _cameraFromFov, _cameraToFov, _cameraHomeFov;

        /// <summary>The refuge's own animals and the waiting egg are drawn (false while an activity plays in the refuge).</summary>
        public bool ActorsVisible => !_actorsHidden;

        /// <summary>
        /// T39 « Cache-cache » plays in the refuge (doc 01 Contenu S-ACT-002: the camera frames a zone of the biome): the
        /// refuge's own animals and the waiting egg are hidden for the game, then shown again. Their simulation keeps
        /// running; with their renderers off their animators rest (CullCompletely). Wish bubbles, voices, rare glints and the
        /// tap hand already wait under any activity (ModalOpen); a new egg waits for the end. Not per frame.
        /// </summary>
        public void SetActorsVisible(bool visible)
        {
            if (_actorsHidden == !visible)
                return;
            _actorsHidden = !visible;
            for (int i = 0; i < _views.Length; i++)
                if (_views[i] != null)
                    ShowActor(_views[i], visible);
            if (_hatchingAnimal != null)
                ShowActor(_hatchingAnimal, visible);
            if (_waitingEgg != null)
                _waitingEgg.gameObject.SetActive(visible);
        }

        private void ShowActor(AnimalView view, bool shown)
        {
            if (view == null)
                return;
            view.GetComponentsInChildren(true, _actorRenderers);
            for (int i = 0; i < _actorRenderers.Count; i++)
                _actorRenderers[i].enabled = shown;
            _actorRenderers.Clear();
            // The blob shadow follows the quality tier again (on only without a shadow map).
            if (shown)
                view.RefreshQuality();
        }

        /// <summary>An activity holds the refuge camera (or it is still gliding back to the player's view).</summary>
        public bool CameraHeld => _cameraHeld;

        /// <summary>
        /// The ground point the camera looks at: the rig's focus, or while an activity holds the camera (and on its way back)
        /// the point under the screen's center at the focus height, so the weather's rain box follows the framed zone.
        /// </summary>
        public Vector3 ViewFocus
        {
            get
            {
                if (_rig == null)
                    return Vector3.zero;
                var focus = _rig.Focus;
                var camera = _rig.Camera;
                if ((!_cameraHeld && !_cameraReturning) || camera == null)
                    return focus;
                var transform = camera.transform;
                var forward = transform.forward;
                if (forward.y > -0.05f)
                    return focus;
                float t = (focus.y - transform.position.y) / forward.y;
                return transform.position + forward * t;
            }
        }

        /// <summary>The held camera is still gliding (to the activity's pose, or back to the player's view).</summary>
        public bool CameraGliding => (_cameraHeld || _cameraReturning) && _cameraT < 1f;

        /// <summary>
        /// T39: an activity takes the refuge camera to its own framing (a world pose and a vertical field of view), gliding
        /// there over <paramref name="seconds"/> (eased). The rig stops (no pan, no zoom, its own state kept) until
        /// <see cref="ReleaseCamera"/> glides back to the view the player left, where the rig takes over again. Calling it
        /// again while held glides on to the new pose.
        /// </summary>
        public void HoldCamera(Vector3 position, Quaternion rotation, float fieldOfView, float seconds)
        {
            var camera = _rig != null ? _rig.Camera : null;
            if (camera == null)
                return;
            var transform = camera.transform;
            if (!_cameraHeld && !_cameraReturning)
            {
                _cameraHomePosition = transform.position;
                _cameraHomeRotation = transform.rotation;
                _cameraHomeFov = camera.fieldOfView;
            }
            _cameraHeld = true;
            _cameraReturning = false;
            _rig.StopFling();
            _rig.enabled = false;
            StartCameraGlide(transform.position, transform.rotation, camera.fieldOfView, position, rotation, fieldOfView, seconds);
        }

        /// <summary>T39: the activity gives the camera back: it glides to the player's view, then the rig resumes.</summary>
        public void ReleaseCamera(float seconds)
        {
            var camera = _rig != null ? _rig.Camera : null;
            if (!_cameraHeld || camera == null)
                return;
            _cameraHeld = false;
            _cameraReturning = true;
            var transform = camera.transform;
            StartCameraGlide(transform.position, transform.rotation, camera.fieldOfView, _cameraHomePosition, _cameraHomeRotation, _cameraHomeFov, seconds);
            if (_cameraT >= 1f)
                TickHeldCamera(0f);
        }

        private void StartCameraGlide(Vector3 fromPosition, Quaternion fromRotation, float fromFov, Vector3 toPosition, Quaternion toRotation,
            float toFov, float seconds)
        {
            _cameraFromPosition = fromPosition;
            _cameraFromRotation = fromRotation;
            _cameraFromFov = fromFov;
            _cameraToPosition = toPosition;
            _cameraToRotation = toRotation;
            _cameraToFov = toFov;
            _cameraSeconds = Mathf.Max(0f, seconds);
            _cameraT = _cameraSeconds > 0f ? 0f : 1f;
            ApplyHeldCamera();
        }

        /// <summary>One frame of the held camera (unscaled time): an eased glide, then the pose held; the rig back at the end.</summary>
        private void TickHeldCamera(float unscaledDeltaTime)
        {
            if (!_cameraHeld && !_cameraReturning)
                return;
            if (_cameraT < 1f)
            {
                _cameraT = Mathf.Min(1f, _cameraT + unscaledDeltaTime / Mathf.Max(0.0001f, _cameraSeconds));
                ApplyHeldCamera();
            }
            if (_cameraReturning && _cameraT >= 1f)
            {
                _cameraReturning = false;
                if (_rig != null)
                    _rig.enabled = true;
            }
        }

        private void ApplyHeldCamera()
        {
            var camera = _rig != null ? _rig.Camera : null;
            if (camera == null)
                return;
            float t = _cameraT;
            // Ease in and out (smootherstep): the glide leaves and lands softly.
            float k = t * t * t * (t * (6f * t - 15f) + 10f);
            camera.transform.SetPositionAndRotation(Vector3.LerpUnclamped(_cameraFromPosition, _cameraToPosition, k),
                Quaternion.SlerpUnclamped(_cameraFromRotation, _cameraToRotation, k));
            camera.fieldOfView = Mathf.LerpUnclamped(_cameraFromFov, _cameraToFov, k);
        }

        /// <summary>T36: the wish bubbles above animals (doc 06 screen 002); null without the prefab.</summary>
        public WishBubbleLayer Bubbles { get; set; }

        /// <summary>A screen covers the refuge: world touches and hatching wait.</summary>
        public bool ModalOpen =>
            OfflineScreenOpen || CodexOpen || ShopOpen || SheetOpen || DialogOpen || ProgressionOpen || SummaryOpen || DailyOpen
            || ActivitiesOpen;

        /// <summary>T35B: the summary card of a multi hatch (screen 028) is up.</summary>
        public bool SummaryOpen => _summaryView != null && _summaryView.IsOpen;

        /// <summary>T35A: Cozy Nests lowers the egg's price at once (the cost is cached, never evaluated per frame).</summary>
        public void OnUpgradePurchased(in UpgradePurchase purchase)
        {
            if (purchase.Kind == UpgradeKind.Nests)
                RefreshEggCost();
        }

        /// <summary>The tutorial hand shows on the first egg (tests).</summary>
        public bool TapHintShown => _tapHintShown;

        /// <summary>Collected offline Light is still counting into the HUD.</summary>
        public bool OfflineCollecting => _collectTime >= 0f;

        /// <summary>T34: adaptive quality turned real shadows on or off: visible animals and the egg update their blob shadows.</summary>
        public void RefreshPresentationQuality()
        {
            // T39: views hidden for an activity take their blob shadow back when they show again (ShowActor).
            for (int i = 0; i < _views.Length && !_actorsHidden; i++)
                _views[i]?.RefreshQuality();
            if (!_actorsHidden)
                _hatchingAnimal?.RefreshQuality();
            _waitingEgg?.RefreshQuality();
            _vfx?.SetTier(PresentationQuality.Tier);
        }

        public void Tick(float time, float deltaTime)
        {
            _time = time;
            TickHeldCamera(Time.unscaledDeltaTime);
            // T34: while heat keeps the picture lighter, the HUD says so in one short line.
            bool notice = _context.Quality != null && _context.Quality.ThermalNotice;
            if (notice != _noticeShown)
            {
                _noticeShown = notice;
                _hud?.SetNotice(notice ? _context.Localization.Get(QualityService.ThermalNoticeKey) : null);
            }
            // A new egg waits until the newborn has walked off its spot (no-collision rule).
            if (_nextEggTime >= 0f && time >= _nextEggTime && !_actorsHidden && _scheduler.IsEggSpotClear())
            {
                _nextEggTime = -1f;
                PresentEgg();
            }

            UpdateOfflineReturn(deltaTime);
            _codexView?.Tick(deltaTime);
            _sheetView?.Tick(deltaTime);
            _modalView?.Tick(deltaTime);
            _summaryView?.Tick(deltaTime);
            if (_shop != null)
            {
                _shop.Tick(deltaTime);
                if (_momentOfferTime >= 0f && time >= _momentOfferTime && !ModalOpen && !Hatching)
                {
                    _momentOfferTime = -1f;
                    _shop.TryShowMomentOffer();
                }
            }

            if (_rig != null)
            {
                _scheduler.SetFocus(_rig.Focus);
                // T34B: animal voices fade with their distance to what the player looks at.
                if (_rig.Camera != null)
                    _context.Audio.SetView(_rig.Focus, _rig.Camera.transform.right, _rig.Distance);
            }
            _scheduler.Tick(time, deltaTime);
            // T36: bubbles follow their animals; hidden under a screen or a hatch (the wish card alone keeps its bubble).
            Bubbles?.Tick(time, deltaTime, (ModalOpen && !(Daily != null && Daily.CardOnlyOpen)) || Hatching);
            _hatch.Tick(time, deltaTime);
            _multi.Tick(time, deltaTime);
            if (time >= _nextChoiceCheck)
            {
                // The offer changes rarely (a Discovery egg won, the Focus egg or × 3 unlocked): a few checks a second.
                _nextChoiceCheck = time + ChoiceCheckSeconds;
                if (!Hatching && _choice.Refresh())
                    PushChoice(false);
            }
            if (_multiPending > 0)
                TryStartMulti();
            if (_vfx != null)
            {
                TickRareGlints();
                _vfx.Tick(time);
            }

            if (_hud != null)
            {
                var economy = _context.Economy;
                _hud.SetLight(DisplayedLight, economy.GetLightPerSecond());
                bool free = _context.Hatch.IsNextHatchFree;
                // Cached cost (refreshed after each hatch): no price curve evaluation per frame.
                _hud.SetEgg(_eggCost, free, free || economy.CanSpend(CurrencyId.Light, _eggCost));
                if (_context.Codex != null)
                    _hud.SetCodexBadge(_context.Codex.HasNew);
                if (_shop != null)
                {
                    bool unlocked = _shop.Unlocked;
                    _hud.SetShop(unlocked, _context.State.Wallet.Gems);
                    if (unlocked)
                        _context.Analytics.Milestone(FunnelMilestone.FirstShopExposure);
                    // T34A unlock toast: only when the shop opens during play, never for a shop already open at entry.
                    int state = unlocked ? 1 : 0;
                    if (_shopUnlockState == 0 && state == 1)
                    {
                        _hud.ShowToast(ToastKind.Unlock, _context.Localization.Get(FeedbackText.ToastShopOpen));
                        _context.Audio.Play(AudioEventId.Claim, Vector3.zero, default);
                    }
                    _shopUnlockState = state;
                }
                UpdateTapHint(deltaTime);
                _hud.Tick(deltaTime);
            }
        }

        /// <summary>
        /// T34A tutorial gesture: on the very first egg (no animal yet), after a short pause, a hand taps on it. It
        /// follows the egg on screen and leaves for good with the first hatch. No text: the animation explains.
        /// </summary>
        private void UpdateTapHint(float deltaTime)
        {
            bool wanted = _context.State.Collection.Count == 0 && _waitingEgg != null && !Hatching && !ModalOpen;
            _idleOnEgg = wanted ? _idleOnEgg + deltaTime : 0f;
            bool show = wanted && _idleOnEgg >= TapHintDelay;
            var camera = _rig != null ? _rig.Camera : Camera.main;
            if (show && camera != null)
            {
                var screen = camera.WorldToScreenPoint(_waitingEgg.VfxPoint.position + new Vector3(0f, 0.12f, 0f));
                _hud.SetTapHint(true, new Vector2(screen.x, screen.y));
                _tapHintShown = true;
            }
            else if (_tapHintShown)
            {
                _hud.SetTapHint(false, default);
                _tapHintShown = false;
            }
        }

        /// <summary>World tap from the camera input (already filtered from UI touches).</summary>
        public void OnTap(Vector2 screenPosition)
        {
            if (ModalOpen || (_hud != null && _hud.IsOverUi(screenPosition)))
                return;
            if (_multi.IsPlaying)
            {
                _multi.Skip();
                return;
            }
            if (_hatch.IsPlaying)
            {
                _hatch.Skip();
                return;
            }
            var camera = _rig != null ? _rig.Camera : Camera.main;
            if (camera == null)
                return;
            var ray = camera.ScreenPointToRay(screenPosition);

            // T36: a wish bubble first (doc 01 A08, section 2.6), then the egg, then the animals.
            if (Bubbles != null && Daily != null && Bubbles.TryHit(ray, out int bubble))
            {
                Daily.OnBubbleTapped(bubble);
                return;
            }

            if (_waitingEgg != null && HitsSphere(ray, _waitingEgg.VfxPoint.position, EggTapRadius))
            {
                RequestHatch();
                return;
            }

            int bestView = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _views.Length; i++)
            {
                if (_views[i] == null || _agentByView[i] == AnimalScheduler.InvalidHandle)
                    continue;
                var center = _scheduler.GetPosition(_agentByView[i]) + new Vector3(0f, 0.25f, 0f);
                float along = Vector3.Dot(center - ray.origin, ray.direction);
                if (along <= 0f || along >= bestDistance)
                    continue;
                if (HitsSphere(ray, center, AnimalTapRadius))
                {
                    bestDistance = along;
                    bestView = i;
                }
            }
            if (bestView >= 0)
            {
                // T36 (doc 01 A10, section 2.6): touching an animal whose wish is fulfilled claims it; any other touch
                // counts for its bond (daily caps).
                if (Daily != null && Daily.OnAnimalTapped(_views[bestView].OwnerInstanceId))
                {
                    _tappedView = -1;
                    return;
                }
                // Doc 05: a touch first brings a reaction; touching the same animal again opens its sheet.
                if (bestView == _tappedView && _time - _tappedTime <= SecondTapSeconds && _sheetView != null)
                {
                    _tappedView = -1;
                    OpenSheet(_views[bestView]);
                    return;
                }
                _tappedView = bestView;
                _tappedTime = _time;
                _scheduler.Poke(_agentByView[bestView], _time);
                _context.Audio.Play(AudioEventId.AnimalHappy, _scheduler.GetPosition(_agentByView[bestView]) + new Vector3(0f, 0.2f, 0f),
                    _views[bestView].Species);
                _context.Haptics.Play(HapticEventId.AnimalTouch);
            }
        }

        private void OpenSheet(AnimalView view)
        {
            if (view == null || ModalOpen || !TryBuildSheet(view.OwnerInstanceId, out var data))
                return;
            _sheetView.Open(data, _context.Localization, this);
            RefreshSheetTreat(view.OwnerInstanceId);
            if (_sheetView is IAnimalSheetGroomView groom)
            {
                bool free = false;
                bool available = Activities != null && Activities.CanGroom(out free);
                groom.SetGroom(available, available && free);
            }
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
        }

        /// <summary>T36 (doc 01 section 10.1): the sheet's « Friandise » button, with the treat this animal would eat.</summary>
        private void RefreshSheetTreat(uint instanceId)
        {
            if (!(_sheetView is IAnimalSheetTreatView treats) || _context.Daily == null)
                return;
            var bond = _context.Daily.Bond;
            var treat = bond.BestTreatFor(instanceId);
            int owned = treat != ContentId.None && _context.State.Progression.Treats.TryGetValue(treat, out int count) ? count : 0;
            treats.SetTreat(new SheetTreatData
            {
                Available = owned > 0,
                Treat = treat,
                Owned = owned,
                Preferred = owned > 0 && bond.IsPreferred(instanceId, treat),
            });
        }

        /// <summary>T36: a treat eaten from the sheet: bond, hearts above the animal, its happy hop, the eat sound.</summary>
        public void GiveTreat(uint instanceId)
        {
            var daily = _context.Daily;
            if (daily == null || !daily.Bond.TryGiveTreat(instanceId, out _, out int points))
            {
                _context.Audio.Play(AudioEventId.ErrorSoft, Vector3.zero, default);
                return;
            }
            int index = ViewIndexOf(instanceId);
            if (index >= 0)
            {
                float scale = _views[index].transform.localScale.x;
                var position = _scheduler.GetPosition(_agentByView[index]);
                _vfx?.Play(VfxId.FavoriteHeart, position + new Vector3(0f, 0.2f * scale, 0f), _time);
                _scheduler.Poke(_agentByView[index], _time);
                _context.Audio.Play(AudioEventId.AnimalEat, position + new Vector3(0f, 0.2f, 0f), _views[index].Species);
            }
            else
            {
                _context.Audio.Play(AudioEventId.AnimalEat, Vector3.zero, default);
            }
            _context.Haptics.Play(HapticEventId.Confirm);
            if (TryBuildSheet(instanceId, out var data))
                _sheetView?.Refresh(data);
            (_sheetView as IAnimalSheetTreatView)?.PlayTreatGiven(Mathf.Clamp01(points / (float)BondPointsPerLevel));
            RefreshSheetTreat(instanceId);
        }

        /// <summary>Sheet data of a saved animal: names localized once here, never per frame.</summary>
        public bool TryBuildSheet(uint instanceId, out AnimalSheetData data)
        {
            data = default;
            var collection = _context.State.Collection;
            if (!collection.TryGet(instanceId, out var animal))
                return false;
            var loc = _context.Localization;
            string species = _context.Content.Species.TryGet(animal.Species, out var speciesDef) ? loc.Get(speciesDef.DisplayKey) : string.Empty;
            string variant = _context.Content.Variants.TryGet(animal.Variant, out var variantDef) ? loc.Get(variantDef.DisplayKey) : string.Empty;
            string name = collection.CustomNames.TryGetValue(instanceId, out var custom) && !string.IsNullOrEmpty(custom) ? custom : species;
            int bond = Math.Min((int)animal.BondPoints, BondPointsForMax);
            float inLevel = bond >= BondPointsForMax ? 1f : (bond % BondPointsPerLevel) / (float)BondPointsPerLevel;
            // The second line names the variant (and the species when the animal has its own name).
            data = new AnimalSheetData(instanceId, name, name == species ? variant : species, name == species ? null : variant,
                animal.Species, animal.Variant, animal.Rarity, animal.IsFavorite, inLevel, bond / BondPointsPerLevel, animal.TraitBits);
            return true;
        }

        /// <summary>T34A: the heart on the sheet. Saved soon (normal urgency), shown at once with a few hearts.</summary>
        public void ToggleFavorite(uint instanceId)
        {
            var collection = _context.State.Collection;
            if (!collection.TryGet(instanceId, out var animal))
                return;
            bool favorite = !animal.IsFavorite;
            animal.Flags = favorite ? animal.Flags | AnimalFlags.Favorite : animal.Flags & ~AnimalFlags.Favorite;
            collection.Update(animal);
            _context.Save?.RequestSave(SaveUrgency.Normal);
            if (favorite)
            {
                for (int i = 0; i < _views.Length; i++)
                {
                    if (_views[i] == null || _views[i].OwnerInstanceId != instanceId || _agentByView[i] == AnimalScheduler.InvalidHandle)
                        continue;
                    float scale = _views[i].transform.localScale.x;
                    _vfx?.Play(VfxId.FavoriteHeart, _scheduler.GetPosition(_agentByView[i]) + new Vector3(0f, 0.2f * scale, 0f), _time);
                    break;
                }
                _context.Audio.Play(AudioEventId.ToggleOn, Vector3.zero, default);
                _context.Haptics.Play(HapticEventId.Confirm);
            }
            else
            {
                _context.Audio.Play(AudioEventId.ToggleOff, Vector3.zero, default);
                _context.Haptics.Play(HapticEventId.TapLight);
            }
            if (TryBuildSheet(instanceId, out var data))
                _sheetView?.Refresh(data);
        }

        public void OpenSpeciesDetails(ContentId species)
        {
            if (_codexView == null || _context.Codex == null || ModalOpen || Hatching)
                return;
            _codexView.OpenSpecies(_context.Codex, _context.Localization, this, species);
            _context.Analytics.ScreenView(ScreenId.Codex);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            SetWorldVisible(false);
        }

        public void AnimalSheetClosed() => _context.Audio.Play(AudioEventId.UiBack, Vector3.zero, default);

        /// <summary>
        /// T34A egg odds (doc 05 Hatch UX: the info button always shows the chances): rarity odds of this egg and,
        /// while species are left to find, the new species guarantee.
        /// </summary>
        public void OpenEggInfo()
        {
            if (_modalView == null || ModalOpen || Hatching)
                return;
            var loc = _context.Localization;
            _context.Hatch.GetOdds(_eggId, _odds);
            string body = EggOddsText(_odds, _egg, loc, _context.Content);
            _modalView.Show(ModalKind.Info, loc.Get(_egg.DisplayKey), body, loc.Get(FeedbackText.ModalOkButton), null, this);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            _context.Analytics.EggOddsOpened(_eggId);
        }

        /// <summary>
        /// The odds text: one line per rarity (percent of the egg's rarity weights, French decimals in fr), then the new
        /// species guarantee while species are left to find. Built when the info opens, never per frame.
        /// </summary>
        public static string EggOddsText(EggDefinition egg, ILocalization loc, bool speciesLeft)
        {
            var weights = egg.RarityWeights;
            double total = 0.0;
            for (int i = 0; i < weights.Length; i++)
                total += weights[i];
            var culture = loc.Language == "fr" ? System.Globalization.CultureInfo.GetCultureInfo("fr-FR") : System.Globalization.CultureInfo.InvariantCulture;
            var body = new System.Text.StringBuilder(256);
            string line = loc.Get(FeedbackText.EggOddsLine);
            for (int i = 0; i < weights.Length && total > 0.0; i++)
            {
                if (i > 0)
                    body.Append('\n');
                double percent = weights[i] / total * 100.0;
                string number = percent.ToString(percent >= 10.0 ? "0.#" : percent >= 1.0 ? "0.##" : "0.###", culture);
                body.Append(string.Format(line, loc.Get(FeedbackText.RarityKey((RarityTier)i)), number));
            }
            if (speciesLeft)
                body.Append("\n\n").Append(string.Format(loc.Get(FeedbackText.EggOddsGuarantee), egg.PityHardLimit));
            return body.ToString();
        }

        /// <summary>
        /// T35B odds window (doc 02 Œufs, section 6): this egg's table with the stars and the word of each rarity, the Focus
        /// targets, the protections running now in plain words, the guarantee in one sentence; for a Discovery egg its rules
        /// and the stock. Every number comes from <see cref="HatchService.GetOdds"/>, the ones the roll uses. Built when the
        /// window opens, never per frame.
        /// </summary>
        public static string EggOddsText(EggOdds odds, EggDefinition egg, ILocalization loc, ContentRegistry content)
        {
            var culture = loc.Language == "fr" ? System.Globalization.CultureInfo.GetCultureInfo("fr-FR") : System.Globalization.CultureInfo.InvariantCulture;
            var body = new System.Text.StringBuilder(384);
            string line = loc.Get(FeedbackText.EggOddsLine);
            if (odds.Tier == EggTier.Discovery)
                body.Append(loc.Get(EggText.OddsDiscovery)).Append('\n');
            bool first = true;
            for (int i = 0; i < odds.RarityPercent.Length; i++)
            {
                double percent = odds.RarityPercent[i];
                if (percent <= 0.0)
                    continue;
                if (!first)
                    body.Append('\n');
                first = false;
                body.Append('★', i + 1).Append(' ');
                body.Append(string.Format(line, loc.Get(FeedbackText.RarityKey((RarityTier)i)), Percent(percent, culture)));
            }
            if (odds.TargetCount > 0 && odds.TargetCount < egg.Pool.Length)
            {
                var names = new System.Text.StringBuilder(64);
                for (int i = 0; i < odds.TargetCount; i++)
                {
                    if (i > 0)
                        names.Append(", ");
                    names.Append(content.Species.TryGet(odds.Targets[i], out var species) ? loc.Get(species.DisplayKey) : "?");
                }
                body.Append("\n\n").Append(string.Format(loc.Get(EggText.OddsTargets), names));
            }
            if (odds.NewSpeciesBonus > 0.0 || odds.NewVariantBonus > 0.0)
                body.Append('\n');
            if (odds.NewSpeciesBonus > 0.0)
                body.Append('\n').Append(string.Format(loc.Get(EggText.OddsNewSpecies), Percent(odds.NewSpeciesBonus * 100.0, culture)));
            if (odds.NewVariantBonus > 0.0)
                body.Append('\n').Append(string.Format(loc.Get(EggText.OddsNewColor), Percent(odds.NewVariantBonus * 100.0, culture)));
            if (odds.EggsToGuarantee > 0)
                body.Append("\n\n").Append(string.Format(loc.Get(FeedbackText.EggOddsGuarantee), odds.PityHardLimit));
            if (odds.Free)
                body.Append("\n\n").Append(string.Format(loc.Get(EggText.OddsStock), odds.Stock));
            return body.ToString();
        }

        private static string Percent(double percent, System.Globalization.CultureInfo culture) =>
            percent.ToString(percent >= 10.0 ? "0.#" : percent >= 1.0 ? "0.##" : "0.###", culture);

        private bool HasSpeciesToFind()
        {
            var discovered = _context.State.Collection.DiscoveredSpecies;
            foreach (var entry in _egg.Pool)
                if (!discovered.Contains(entry.Species))
                    return true;
            return false;
        }

        public void ModalClosed(bool confirmed) =>
            _context.Audio.Play(confirmed ? AudioEventId.UiConfirm : AudioEventId.UiBack, Vector3.zero, default);

        /// <summary>
        /// T34B: a near animal greets, rejoices or falls asleep on its own. Quiet and rare (the audio service spaces
        /// them for the whole refuge); never over a hatch or a screen, never right after the player touched it.
        /// </summary>
        void IAnimalVoiceListener.AnimalVoice(AnimalView view, AnimalBehaviorState state, Vector3 position)
        {
            if (view == null || ModalOpen || Hatching)
                return;
            if (_tappedView >= 0 && _views[_tappedView] == view && _time - _tappedTime < AfterTouchQuietSeconds)
                return;
            var id = state == AnimalBehaviorState.Sleep ? AudioEventId.AnimalSleep
                : state == AnimalBehaviorState.Eat ? AudioEventId.AnimalEat
                : AudioEventId.AnimalChatter;
            _context.Audio.Play(id, position + new Vector3(0f, 0.2f, 0f), view.Species);
        }

        public void RequestHatch()
        {
            if (ModalOpen)
                return;
            Daily?.OnHatchRequested();
            if (_multi.IsPlaying)
            {
                _multi.Skip();
                return;
            }
            if (_hatch.IsPlaying)
            {
                _hatch.Skip();
                return;
            }
            if (_waitingEgg == null || _multiPending > 0)
                return;
            int count = _choice.NextCount;
            if (!_context.Hatch.CanHatch(_eggId, count))
            {
                _hatch.Nudge();
                _hud?.PulseNotEnough();
                _context.Audio.Play(AudioEventId.ErrorSoft, Vector3.zero, default);
                _context.Haptics.Play(HapticEventId.ErrorSoft);
                // One calm line the first time and then at most every few seconds: the shake says the rest.
                if (_time - _lastNotEnoughToast >= NotEnoughToastSeconds)
                {
                    _lastNotEnoughToast = _time;
                    _hud?.ShowToast(ToastKind.Warning, _context.Localization.Get(FeedbackText.ToastNotEnoughLight));
                }
                return;
            }

            if (count > 1)
            {
                // × 3 or × 10: the spots around the pedestal must be clear first (no-collision rule). The apron grows to
                // the group's size and the animals resting there move on.
                _multiPending = count;
                _multiRequestTime = _time;
                _scheduler.SetEggSpot(_eggCenter, EggSpotRadius, EggBodyRadius, Mathf.Max(EggApronRadius, MultiCircle(count)));
                _scheduler.ClearEggApron(_time);
                TryStartMulti();
                return;
            }

            bool paid = !_context.Hatch.IsNextHatchFree && _egg.Tier != EggTier.Discovery;
            var result = _context.Hatch.Hatch(_eggId);
            if (!result.Succeeded)
            {
                _hatch.Nudge();
                _hud?.PulseNotEnough();
                return;
            }
            RefreshEggCost();
            // T34B2: a paid egg is heard leaving the counter (soft, under the egg tap).
            if (paid)
                _context.Audio.Play(AudioEventId.SpendLight, Vector3.zero, default);

            var reveal = _waitingEgg.RevealPoint;
            _context.Visuals.TryResolve(result.Animal, reveal.position, reveal.eulerAngles.y, out var snapshot);
            if (!TryAcquire(snapshot, out _hatchingAnimal))
                _hatchingAnimal = null;
            _pendingMastery = result.Mastery;
            _masteryShown = false;
            _hatchingInstance = result.Animal.InstanceId;
            _hatchingSpecies = result.Animal.Species;
            _hatchingSeed = result.Animal.Seed;
            _hatchingRare = RareGlow.KindOf(result.Animal.Rarity, result.Animal.TraitBits);
            var news = result.NewSpecies ? HatchNews.NewSpecies : result.NewVariant ? HatchNews.NewVariant : HatchNews.None;
            PrepareReveal(result.Animal, news);

            if (_hatchingAnimal != null && _hatch.Hatch(_hatchingAnimal, result.Animal.Species, result.Animal.Rarity, HatchSpeed(), this, news))
            {
                _rig?.FocusOn(_waitingEgg.VfxPoint.position, RefugeCameraState.HatchReveal);
                _hud?.SetHatching(true);
            }
            else
            {
                // Presentation could not stage it (pool exhausted): the animal is saved, show it directly.
                OnHatchFinished();
            }
        }

        /// <summary>
        /// HUD shortcut. The Codex is opaque and full screen: the world camera stops while it is open
        /// (no GPU spent on hidden pixels) and restarts instantly on close.
        /// </summary>
        public void OpenCodex()
        {
            if (_codexView == null || _context.Codex == null || ModalOpen || Hatching)
                return;
            _codexView.Open(_context.Codex, _context.Localization, this);
            _context.Analytics.ScreenView(ScreenId.Codex);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            SetWorldVisible(false);
        }

        public void CodexClosed()
        {
            _context.Audio.Play(AudioEventId.PanelClose, Vector3.zero, default);
            SetWorldVisible(true);
        }

        public void CodexNavigated(bool deeper) =>
            _context.Audio.Play(deeper ? AudioEventId.UiTab : AudioEventId.UiBack, Vector3.zero, default);

        /// <summary>HUD shortcut. Like the Codex, the shop is opaque: the world camera stops while it is open.</summary>
        public void OpenShop()
        {
            if (_shop == null || ModalOpen || Hatching)
                return;
            _shop.OpenShop();
        }

        private void SetWorldVisible(bool visible)
        {
            if (_rig != null && _rig.Camera != null)
                _rig.Camera.enabled = visible;
            if (_input != null)
                _input.Blocked = !visible;
        }

        /// <summary>T36 (doc 01 section 6): the return gift waiting for this card, or none.</summary>
        private void SetReturnGift()
        {
            if (!(_offlineView is IOfflineReturnGiftView gift))
                return;
            if (Daily != null && Daily.TryBuildReturnGift(out var card, _returnCells, out int count))
                gift.SetGift(card, _returnCells, count, _context.Localization);
            else
                gift.ClearGift();
        }

        /// <summary>Collect button: the held Light counts up into the HUD counter (and the return gift is taken).</summary>
        public void CollectOffline()
        {
            Daily?.ClaimReturnGift();
            if (_heldLight.IsZero || _collectTime >= 0f)
                return;
            _collectTime = 0f;
            _context.Analytics.Milestone(FunnelMilestone.FirstCollect);
            _context.Audio.Play(AudioEventId.CollectLight, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.Confirm);
            // T34A: the Light flies from the card (screen center) into the chip.
            _hud?.PlayCollect(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }

        void IHatchListener.OnHatchRevealed()
        {
            ShowMastery();
            if (_hasPendingReveal)
            {
                _hasPendingReveal = false;
                _hud?.ShowReveal(_pendingReveal);
            }
        }

        /// <summary>
        /// T34A reveal banner: a new species or variant says so; otherwise a rarity from Rare up says its name. Common
        /// duplicates show nothing (the mastery toast already moves). Texts resolved once per hatch.
        /// </summary>
        private void PrepareReveal(in AnimalInstance animal, HatchNews news)
        {
            _hasPendingReveal = false;
            if (news == HatchNews.None && animal.Rarity < RarityTier.Rare)
                return;
            var loc = _context.Localization;
            string species = _context.Content.Species.TryGet(animal.Species, out var speciesDef) ? loc.Get(speciesDef.DisplayKey) : string.Empty;
            string title;
            string name = species;
            if (news == HatchNews.NewSpecies)
            {
                title = loc.Get(FeedbackText.RevealNewSpecies);
            }
            else if (news == HatchNews.NewVariant)
            {
                title = loc.Get(FeedbackText.RevealNewVariant);
                if (_context.Content.Variants.TryGet(animal.Variant, out var variantDef))
                    name = species + " \u00B7 " + loc.Get(variantDef.DisplayKey);
            }
            else
            {
                title = loc.Get(FeedbackText.RarityKey(animal.Rarity));
            }
            _pendingReveal = new RevealInfo(title, name, animal.Rarity, news);
            _hasPendingReveal = true;
        }

        /// <summary>Species progress right after the reveal: every hatch moves something forward.</summary>
        private void ShowMastery()
        {
            if (_masteryShown)
                return;
            _masteryShown = true;
            if (!_pendingMastery.HasGain)
                return;
            _hud?.ShowMastery(_pendingMastery);
            if (_pendingMastery.LeveledUp)
            {
                // H-VFX-009: a short star and paw burst around the newborn.
                if (_hatchingAnimal != null)
                    _vfx?.Play(VfxId.MasteryLevelUp, _hatchingAnimal.transform.position + new Vector3(0f, 0.18f, 0f), _time);
                _context.Audio.Play(AudioEventId.MasteryLevelUp, Vector3.zero, _pendingMastery.Species);
                _context.Haptics.Play(_pendingMastery.Milestone > 0 ? HapticEventId.Rare : HapticEventId.Confirm);
            }
        }

        public void OnHatchFinished()
        {
            ShowMastery();
            _lastHatchEnd = _time;
            if (_waitingEgg != null)
            {
                _eggs.Release(_waitingEgg);
                _waitingEgg = null;
            }
            if (_hatchingAnimal != null)
            {
                AddToMeadow(_hatchingAnimal, _hatchingSpecies, _hatchingInstance, _hatchingSeed, _hatchingRare, emerging: true);
                _hatchingAnimal = null;
            }
            _rig?.ReturnToWorld();
            _hud?.SetHatching(false);
            _nextEggTime = _time + NextEggDelay;
            // A hatch is a happy moment (doc 09): the only time a moment offer may appear.
            _momentOfferTime = -1f;
            _momentOfferArmed = true;
            // The stock of Discovery eggs, the Focus targets and the price follow the new animal.
            _choice.Refresh();
            PushChoice(false);
        }

        /// <summary>
        /// Fills the rest of the visible budget with pooled views for every species the egg can give, about
        /// two per frame in total right after entering: later hatches never instantiate during gameplay
        /// (Performance Guide: pooling, no runtime Instantiate). Species are warmed side by side.
        /// </summary>
        private void WarmupSpares()
        {
            int spares = _visibleBudget + 1 - VisibleAnimals;
            if (spares <= 0)
                return;
            // T35B: every egg type can give any species of an open biome (Discovery eggs too): all species are warmed.
            int perFrame = Math.Max(1, 2 / Math.Max(1, _speciesHandles.Length));
            for (int i = 0; i < _speciesHandles.Length; i++)
                _ = WarmupSpeciesAsync(_speciesHandles[i], spares, perFrame);
        }

        private async Awaitable WarmupSpeciesAsync(int handle, int count, int perFrame)
        {
            try
            {
                int missing = count - _animals.GetPool(handle).Stats.Inactive;
                if (missing > 0)
                    await _animals.WarmupAsync(handle, missing, perFrame, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>Spares of the least warmed species the egg can give: the next hatch is covered while this is above 0.</summary>
        public int PooledSpares
        {
            get
            {
                int min = int.MaxValue;
                for (int i = 0; i < _speciesHandles.Length; i++)
                    min = Math.Min(min, _animals.GetPool(_speciesHandles[i]).Stats.Inactive);
                return min == int.MaxValue ? 0 : min;
            }
        }

        /// <summary>Spares of one species (tests and diagnostics).</summary>
        public int PooledSparesOf(ContentId species) =>
            _speciesSlot.TryGetValue(species, out int slot) ? _animals.GetPool(_speciesHandles[slot]).Stats.Inactive : 0;

        private bool TryAcquire(in AnimalVisualSnapshot snapshot, out AnimalView view)
        {
            view = null;
            return _speciesSlot.TryGetValue(snapshot.Species, out int slot)
                   && _animals.TryAcquire(_speciesHandles[slot], snapshot, out view);
        }

        private BehaviorProfile ProfileOf(ContentId species) =>
            _speciesSlot.TryGetValue(species, out int slot) ? _speciesProfiles[slot] : _speciesProfiles[0];

        // ---------------- T35B egg choice ----------------

        private readonly HatchPresentationConfig _hatchConfig;

        /// <summary>Device setting "Fast hatches" (on by default; the Settings screen comes with T56).</summary>
        private readonly bool _fastHatches = Services.Device.LocalDeviceSettingsStore.Load().FastHatches;

        private void SetEggData(ContentId egg)
        {
            _eggId = egg;
            _egg = _context.Content.Eggs.Get(egg);
            _context.Palettes.TryGetRow(egg, out int row);
            _eggVisual = EggVisualPacking.FromDefinition(_egg, row);
        }

        /// <summary>
        /// The pedestal follows the choice: the waiting egg takes the chosen egg's colors, the HUD its pills, the price (or
        /// "Free"), the × 1 / × 3 / × 10 pill and the Focus targets. Called on a change, never every frame.
        /// </summary>
        private void PushChoice(bool announce)
        {
            var selected = _choice.Selected;
            if (selected != _eggId)
            {
                SetEggData(selected);
                if (_waitingEgg != null && !Hatching && _chunk.EggSpawn != null)
                {
                    _waitingEgg.Configure(_chunk.EggSpawn.position, 180f, _eggVisual);
                    _hatch.Present(_waitingEgg);
                }
            }
            RefreshEggCost();
            if (_choiceHud == null)
                return;
            var eggs = _context.Content.Eggs;
            for (int i = 0; i < _choice.Count; i++)
            {
                var egg = eggs.Get(_choice[i]);
                _slots[i] = new EggSlot(egg.Id, egg.Palette[0], egg.Palette[1], egg.Tier == EggTier.Discovery ? _context.Hatch.DiscoveryEggs : -1);
            }
            _choiceHud.SetEggSlots(_slots, _choice.Count, _choice.SelectedIndex);
            _choiceHud.SetHatchQuantity(_choice.Quantity, _choice.MaxQuantity > 1);
            _choiceHud.SetPriceText(_egg.Tier == EggTier.Discovery ? _context.Localization.Get(EggText.Free) : null);
            PushTargets();
            if (announce)
                _choiceHud.ShowEggName(_context.Localization.Get(_egg.DisplayKey));
        }

        /// <summary>The Focus egg's targets (doc 02 Œufs, section 2.2), recomputed after every hatch. None once it holds the whole biome.</summary>
        private void PushTargets()
        {
            if (_choiceHud == null)
                return;
            int count = _egg.FocusTargetCount > 0 ? _context.Hatch.GetFocusTargets(_eggId, _targets) : 0;
            if (count > _egg.FocusTargetCount)
                count = 0;
            count = Math.Min(count, _portraits.Length);
            var variants = _context.Content.Variants;
            var found = _context.State.Collection.DiscoveredVariants;
            for (int i = 0; i < count; i++)
            {
                var shown = ContentId.None;
                for (int v = 0; v < variants.Count && !shown.IsValid; v++)
                    if (variants[v].Species == _targets[i] && found.Contains(variants[v].Id))
                        shown = variants[v].Id;
                _portraits[i] = new PortraitSlot(_targets[i], shown);
            }
            _choiceHud.SetFocusTargets(_portraits, count);
        }

        public void SelectEgg(int index)
        {
            if (ModalOpen || Hatching || _multiPending > 0 || !_choice.Select(index))
                return;
            PushChoice(true);
            _context.Audio.Play(AudioEventId.UiTab, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
        }

        public void StepEgg(int direction)
        {
            if (ModalOpen || Hatching || _multiPending > 0 || !_choice.Step(direction))
                return;
            PushChoice(true);
            _context.Audio.Play(AudioEventId.UiTab, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
        }

        public void CycleHatchQuantity()
        {
            if (ModalOpen || Hatching || _multiPending > 0 || !_choice.CycleQuantity())
                return;
            PushChoice(false);
            _context.Audio.Play(AudioEventId.UiTab, Vector3.zero, default);
        }

        /// <summary>
        /// Doc 02 Œufs, section 4: the first hatches play the whole sequence; afterwards the fast one (1.2 s) when the
        /// device setting allows it (on by default), else the repeat speed of a quick series.
        /// </summary>
        private float HatchSpeed()
        {
            int hatched = (int)_context.State.Collection.NextInstanceId - 1;
            int full = _context.Progression != null ? _context.Progression.Config.FullHatchSequences : 20;
            if (hatched <= full)
                return 1f;
            if (_fastHatches)
                return _hatchConfig.fastSpeed;
            return _time - _lastHatchEnd < RepeatWindowSeconds ? _hatchConfig.repeatSpeed : 1f;
        }

        // ---------------- T35B multi hatch ----------------

        /// <summary>
        /// Spots for the eggs around the pedestal (doc 02 Œufs, section 5): an arc for three, two rows of five for ten, each
        /// spot on clear ground (no prop, no water, no animal), the eggs never touching. While <paramref name="fallback"/> is
        /// false the whole formation must be free; afterwards blocked spots move to the nearest free point of two rings
        /// around the pedestal. False when not enough room yet (an animal is passing).
        /// </summary>
        private bool FindMultiSpots(int count, bool fallback)
        {
            var c = _eggCenter;
            int found = 0;
            for (int i = 0; i < count; i++)
            {
                var preferred = c + PreferredSpot(i, count);
                if (TryTakeSpot(preferred, found))
                {
                    _multiSpots[found++] = Ground(preferred);
                    continue;
                }
                if (!fallback)
                    return false;
                bool placed = false;
                for (int ring = 0; ring < 2 && !placed; ring++)
                {
                    // Beyond the regular formation, a whole body apart from each other.
                    float radius = ring == 0 ? 1.3f : 1.8f;
                    int steps = ring == 0 ? 12 : 16;
                    for (int k = 0; k < steps && !placed; k++)
                    {
                        float angle = Mathf.PI * 2f * k / steps;
                        var candidate = c + new Vector3(Mathf.Sin(angle) * radius, 0f, -Mathf.Cos(angle) * radius);
                        if (TryTakeSpot(candidate, found))
                        {
                            _multiSpots[found++] = Ground(candidate);
                            placed = true;
                        }
                    }
                }
                if (!placed)
                    return false;
            }
            return true;
        }

        private Vector3 PreferredSpot(int i, int count)
        {
            float pitch = Mathf.Max(count <= 3 ? MultiEggSpacing : MultiTenSpacing, _multiPitch);
            if (count <= 3)
            {
                // The pedestal egg in the middle, one on each side a little behind.
                return i == 0 ? Vector3.zero : new Vector3(i == 1 ? -pitch : pitch, 0f, 0.1f);
            }
            int row = i / 5, column = i % 5;
            return new Vector3((column - 2) * pitch, 0f, row == 0 ? _multiFrontZ : _multiBackZ);
        }

        /// <summary>The disc a group's eggs and newborns cover around the pedestal (its regular formation).</summary>
        private float MultiCircle(int count)
        {
            float reach = 0f;
            for (int i = 0; i < count; i++)
                reach = Mathf.Max(reach, PreferredSpot(i, count).magnitude);
            return reach + _multiBodyRadius + Mathf.Abs(_multiBodyOffset) + 0.1f;
        }

        /// <summary>
        /// A spot is free when the newborn's whole body fits there: no other newborn of the group closer than a body
        /// across and a body in depth, and no prop, water or animal in the disc around the body (nose to tail).
        /// </summary>
        private bool TryTakeSpot(Vector3 point, int taken)
        {
            for (int j = 0; j < taken; j++)
            {
                var d = _multiSpots[j] - point;
                if (Mathf.Abs(d.x) < _multiPitch - 1e-3f && Mathf.Abs(d.z) < _multiRowGap - 1e-3f)
                    return false;
            }
            var body = Ground(point + new Vector3(0f, 0f, _multiBodyOffset));
            return _scheduler.IsDiskClear(body, _multiBodyRadius, MultiEggHeight, withAnimals: true);
        }

        private Vector3 Ground(Vector3 point) => new Vector3(point.x, _scheduler.GroundHeight(point), point.z);

        /// <summary>Once the spots are clear: the eggs hatch in the simulation (all paid at once), then the staging starts.</summary>
        private void TryStartMulti()
        {
            int count = _multiPending;
            if (ModalOpen || _waitingEgg == null)
                return;
            bool waited = _time - _multiRequestTime >= MultiWaitSeconds;
            if (!waited && _scheduler.AnyAnimalWithin(_eggCenter, MultiCircle(count)))
                return;
            if (!FindMultiSpots(count, waited))
                return;
            _multiPending = 0;
            if (!_context.Hatch.CanHatch(_eggId, count))
            {
                _scheduler.SetEggSpot(_eggCenter, EggSpotRadius, EggBodyRadius, EggApronRadius);
                return;
            }
            bool paid = _egg.Tier != EggTier.Discovery;
            int done = _context.Hatch.Hatch(_eggId, count, _multiResults);
            if (done == 0)
            {
                _hatch.Nudge();
                _hud?.PulseNotEnough();
                return;
            }
            if (paid)
                _context.Audio.Play(AudioEventId.SpendLight, Vector3.zero, default);

            // The pedestal egg is the first of the group; the others come from the pool onto their spots.
            float reach = 0f;
            _multiCount = 0;
            for (int i = 0; i < done; i++)
            {
                EggView egg;
                if (i == 0)
                    egg = _waitingEgg;
                else if (!_eggs.TryAcquire(out egg))
                    break;
                egg.Configure(_multiSpots[i], 180f, _eggVisual);
                var result = _multiResults[i];
                var reveal = egg.RevealPoint;
                _context.Visuals.TryResolve(result.Animal, reveal.position, reveal.eulerAngles.y, out var snapshot);
                TryAcquire(snapshot, out var view);
                _multiEggs[i] = egg;
                _multiAnimals[i] = view;
                _multiSpecies[i] = result.Animal.Species;
                _multiRarity[i] = result.Animal.Rarity;
                _multiNews[i] = result.NewSpecies ? HatchNews.NewSpecies : result.NewVariant ? HatchNews.NewVariant : HatchNews.None;
                _multiRare[i] = RareGlow.KindOf(result.Animal.Rarity, result.Animal.TraitBits);
                var offset = _multiSpots[i] - _eggCenter;
                offset.y = 0f;
                reach = Mathf.Max(reach, offset.magnitude);
                _multiCount++;
            }
            _waitingEgg = null;
            // The eggs' spot covers them all while they stand: animals walk around, never through (no-collision rule).
            _scheduler.SetEggSpot(_eggCenter, reach + _multiBodyRadius + Mathf.Abs(_multiBodyOffset) + 0.1f, EggBodyRadius,
                Mathf.Max(EggApronRadius, reach + 0.6f));
            _scheduler.EvictFromEggSpot(_time);
            _multi.ReduceMotion = _context.State.Settings.ReduceMotion;
            _multi.Play(_multiEggs, _multiAnimals, _multiSpecies, _multiRarity, _multiNews, _multiCount, this);
            // Three eggs: the hatch framing. Ten: a wider view on the middle of the two rows, so the whole group shows.
            // Reduced motion: no camera coming closer (doc 05 Sensations, section 30).
            if (_rig != null && (count > 3 || !_multi.ReduceMotion))
            {
                if (count > 3)
                    _rig.FocusOn(_eggCenter + new Vector3(0f, 0f, (_multiFrontZ + _multiBackZ) * 0.5f), RefugeCameraState.HatchReveal,
                        _rig.DefaultDistance * 0.85f);
                else
                    _rig.FocusOn(_eggCenter, RefugeCameraState.HatchReveal);
            }
            _hud?.SetHatching(true);
            RefreshEggCost();
        }

        public void OnMultiRevealed()
        {
        }

        /// <summary>A Rare or better result, or a new species or color: the camera comes closer and its banner shows.</summary>
        public void OnMultiMoment(int index)
        {
            var view = _multiAnimals[index];
            if (view == null)
                return;
            if (!_multi.ReduceMotion)
                _rig?.FocusOn(view.transform.position + new Vector3(0f, 0.2f, 0f), RefugeCameraState.HatchReveal);
            PrepareReveal(_multiResults[index].Animal, _multiNews[index]);
            if (_hasPendingReveal)
            {
                _hasPendingReveal = false;
                _hud?.ShowReveal(_pendingReveal);
            }
        }

        /// <summary>
        /// Doc 02 Œufs, section 5: each newborn walks out to a free place. The places lie beyond the group's bodies (none
        /// rests where the next group's formation stands), on lanes that meet no prop, no water, no animal and never cross
        /// another newborn's lane or starting point (the front row before the back row). The walk itself is the meadow's:
        /// the scheduler checks every step against every animal. A newborn with no such place keeps its own (none).
        /// </summary>
        private void PlanExits(Vector3[] exits, float[] delays)
        {
            int count = _multiCount;
            bool rows = count > 3;
            for (int i = 0; i < count; i++)
                delays[i] = (rows && i >= 5 ? ExitBackRowDelay : 0f) + (rows ? i % 5 : i) * ExitStagger;
            // How far the group's bodies reach from the pedestal, nose to tail, wherever the eggs stood.
            float reach = 0f;
            for (int i = 0; i < count; i++)
            {
                var offset = _multiSpots[i] - _eggCenter;
                offset.y = 0f;
                reach = Mathf.Max(reach, offset.magnitude);
            }
            reach += _multiBodyRadius + Mathf.Abs(_multiBodyOffset);
            for (int i = 0; i < count; i++)
            {
                var view = _multiAnimals[i];
                if (view == null)
                    continue;
                var start = view.transform.position;
                exits[i] = start;
                var outward = start - _eggCenter;
                outward.y = 0f;
                if (outward.sqrMagnitude < 1e-4f)
                    outward = new Vector3(0f, 0f, -1f);
                outward.Normalize();
                bool found = false;
                for (int d = 0; d < ExitMargins.Length && !found; d++)
                {
                    for (int a = 0; a < ExitAngles.Length && !found; a++)
                    {
                        var direction = Quaternion.Euler(0f, ExitAngles[a], 0f) * outward;
                        var candidate = Ground(_eggCenter + direction * (reach + ExitMargins[d]));
                        if (!ExitFits(i, start, candidate, exits, delays))
                            continue;
                        exits[i] = candidate;
                        found = true;
                    }
                }
            }
        }

        private bool ExitFits(int i, Vector3 start, Vector3 candidate, Vector3[] exits, float[] delays)
        {
            var from = new Vector2(start.x, start.z);
            var to = new Vector2(candidate.x, candidate.z);
            for (int j = 0; j < _multiCount; j++)
            {
                if (j == i || _multiAnimals[j] == null)
                    continue;
                var otherStart = _multiAnimals[j].transform.position;
                var jFrom = new Vector2(otherStart.x, otherStart.z);
                if (j < i)
                {
                    // Places already given: apart from this one, and the two lanes never cross.
                    var jTo = new Vector2(exits[j].x, exits[j].z);
                    if ((jTo - to).sqrMagnitude < ExitSpacing * ExitSpacing)
                        return false;
                    if (CollisionMath.SegmentSegmentDistanceSq(from, to, jFrom, jTo) < ExitLaneGap * ExitLaneGap)
                        return false;
                }
                else if (delays[j] >= delays[i] - 1e-3f
                    && CollisionMath.PointSegmentDistanceSq(jFrom, from, to) < ExitLaneGap * ExitLaneGap)
                {
                    // A newborn still waiting at its egg: no lane passes through it.
                    return false;
                }
            }
            return _scheduler.IsLaneClear(start, candidate, ExitLaneRadius, MultiEggHeight);
        }

        public void OnMultiFinished()
        {
            // The places are planned while the group still owns its spot, then the spot shrinks back and each newborn
            // joins the meadow and walks to its place.
            PlanExits(_multiExits, _multiDelays);
            _scheduler.SetEggSpot(_eggCenter, EggSpotRadius, EggBodyRadius, EggApronRadius);
            for (int i = 0; i < _multiCount; i++)
            {
                _eggs.Release(_multiEggs[i]);
                _multiEggs[i] = null;
                var view = _multiAnimals[i];
                if (view != null)
                {
                    var animal = _multiResults[i].Animal;
                    var start = view.transform.position;
                    AddToMeadow(view, animal.Species, animal.InstanceId, animal.Seed, _multiRare[i], emerging: true);
                    var exit = _multiExits[i] - start;
                    exit.y = 0f;
                    if (exit.sqrMagnitude > 1e-4f && VisibleAnimals > 0 && _views[VisibleAnimals - 1] == view)
                        _scheduler.SendTo(_agentByView[VisibleAnimals - 1], _multiExits[i], _time);
                }
                _multiAnimals[i] = null;
            }
            ShowMultiSummary();
            _lastHatchEnd = _time;
            _rig?.ReturnToWorld();
            _hud?.SetHatching(false);
            _nextEggTime = _time + NextEggDelay;
            _momentOfferTime = -1f;
            _momentOfferArmed = true;
            _choice.Refresh();
            PushChoice(false);
        }

        /// <summary>T35B: the summary card of the scene (screen 028), set by the scene entry; prewarmed once.</summary>
        public void SetSummaryView(IRewardSummaryView view)
        {
            _summaryView = view;
            _summaryView?.Prewarm(_context.Localization);
        }

        /// <summary>
        /// The summary card (screen 028) of the last multi hatch: each newborn with its rarity (stars, word, frame motif)
        /// and the « Nouveau » badge for a new species or color. Names and words are the table's cached strings.
        /// </summary>
        private void ShowMultiSummary()
        {
            if (_summaryView == null || _multiCount <= 0)
                return;
            var loc = _context.Localization;
            for (int i = 0; i < _multiCount; i++)
            {
                var animal = _multiResults[i].Animal;
                _summary[i] = new SummaryEntry
                {
                    Species = animal.Species,
                    Variant = animal.Variant,
                    Rarity = animal.Rarity,
                    Name = _context.Content.Species.TryGet(animal.Species, out var species) ? loc.Get(species.DisplayKey) : string.Empty,
                    RarityWord = loc.Get(FeedbackText.RarityKey(animal.Rarity)),
                    New = _multiNews[i] != HatchNews.None,
                };
            }
            _summaryView.Open(_summary, _multiCount, loc, this);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
        }

        public void CloseSummary()
        {
            if (!SummaryOpen)
                return;
            _summaryView.Close();
            _context.Audio.Play(AudioEventId.UiBack, Vector3.zero, default);
        }

        /// <summary>
        /// Tests: the smallest free space (m) between the newborns of the multi hatch playing, and between each of them and
        /// the props, the water and the meadow's animals; <see cref="AnimalScheduler.ClearanceHorizon"/> when none is out.
        /// Negative: an overlap (rule « aucune collision »).
        /// </summary>
        public float MultiClearance()
        {
            float best = AnimalScheduler.ClearanceHorizon;
            if (!_multi.IsPlaying)
                return best;
            for (int i = 0; i < _multiCount; i++)
            {
                var view = _multiAnimals[i];
                if (view == null || view.transform.localScale.x < 0.5f)
                    continue;
                var body = ProfileOf(_multiSpecies[i]).body;
                float scale = view.transform.localScale.x;
                var position = view.transform.position;
                body.Segment(position, view.transform.eulerAngles.y, scale, out var a0, out var a1);
                float radius = body.halfWidth * scale;
                best = Mathf.Min(best, _scheduler.BodyClearance(a0, a1, radius, position.y, body.height * scale));
                for (int j = i + 1; j < _multiCount; j++)
                {
                    var other = _multiAnimals[j];
                    if (other == null || other.transform.localScale.x < 0.5f)
                        continue;
                    var otherBody = ProfileOf(_multiSpecies[j]).body;
                    float otherScale = other.transform.localScale.x;
                    otherBody.Segment(other.transform.position, other.transform.eulerAngles.y, otherScale, out var b0, out var b1);
                    best = Mathf.Min(best, CollisionMath.Gap(a0, a1, radius, b0, b1, otherBody.halfWidth * otherScale));
                }
            }
            return best;
        }

        public void Dispose()
        {
            // T39: the rig never stays switched off behind a held camera.
            if (_rig != null)
                _rig.enabled = true;
            _shop?.Dispose();
            _lifetime.Cancel();
            _lifetime.Dispose();
            _hatch.Clear();
            _multi.Clear();
            _vfx?.Dispose();
            _animals.Dispose();
            _eggs.Clear();
        }

        private void UpdateOfflineReturn(float deltaTime)
        {
            if (_offlineView == null)
                return;
            if (!_offlineView.IsOpen && !CodexOpen && !ShopOpen && _collectTime < 0f && !Hatching
                && _context.Offline != null && _context.Offline.TryTakePending(out var report))
            {
                // Already in the wallet (saved at once); the HUD holds it back until Collect.
                _heldLight = report.Light;
                SetReturnGift();
                _offlineView.Show(report.Light, _context.Localization, this);
                _context.Analytics.ScreenView(ScreenId.OfflineReturn);
                _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            }
            _offlineView.Tick(deltaTime);

            if (_collectTime >= 0f)
            {
                _collectTime += deltaTime;
                float t = Mathf.Clamp01(_collectTime / CollectCountSeconds);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                if (t >= 1f)
                {
                    _heldLight = BigValue.Zero;
                    _collectTime = -1f;
                    _heldAtCollect = BigValue.Zero;
                }
                else
                {
                    if (_heldAtCollect.IsZero)
                        _heldAtCollect = _heldLight;
                    _heldLight = BigValue.Multiply(_heldAtCollect, 1.0 - eased);
                }
            }

            bool modal = ModalOpen;
            if (_input != null && _input.Blocked != modal)
                _input.Blocked = modal;
        }

        private void PresentEgg()
        {
            if (_waitingEgg != null || _chunk.EggSpawn == null || !_eggs.TryAcquire(out _waitingEgg))
                return;
            // The egg faces the camera (its stage-1 crack is on its front).
            _waitingEgg.Configure(_chunk.EggSpawn.position, 180f, _eggVisual);
            _hatch.Present(_waitingEgg);
            if (_momentOfferArmed)
            {
                // Same pause as before the newborn walked off: 1.6 s after the hatch, the egg being back after 0.45 s.
                _momentOfferArmed = false;
                _momentOfferTime = _time + (MomentOfferDelay - NextEggDelay);
            }
        }

        private void RefreshEggCost() => _eggCost = _context.Hatch.GetCost(_eggId, _choice.NextCount);

        private void SpawnSavedAnimals() => SpawnSaved(_walkArea);

        /// <summary>
        /// Favorites first, then the newest animals, up to the visible allowance, each somewhere in the area (spread
        /// by its seed). The rest lives in the logical reserve and keeps producing through the aggregates.
        /// </summary>
        private void SpawnSaved(WanderArea area)
        {
            var collection = _context.State.Collection;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = collection.Count - 1; i >= 0 && VisibleAnimals < _visibleAllowance; i--)
                {
                    ref readonly var animal = ref collection[i];
                    // T36: an animal with a pending wish is shown first too (its bubble is in the world, doc 06 screen 002).
                    bool first = animal.IsFavorite || HasWish(animal.InstanceId);
                    if (first != (pass == 0) || IsVisible(animal.InstanceId))
                        continue;
                    var position = area.PointAt(SeedHash.Unit(animal.Seed, 0x51A70001u) * 0.9f + 0.05f,
                        SeedHash.Unit(animal.Seed, 0x51A70002u) * 0.9f + 0.05f);
                    if (!_context.Visuals.TryResolve(animal, position, SeedHash.Unit(animal.Seed, 0x51A70003u) * 360f, out var snapshot))
                        continue;
                    if (!TryAcquire(snapshot, out var view))
                        continue;
                    AddToMeadow(view, animal.Species, animal.InstanceId, animal.Seed, RareGlow.KindOf(animal.Rarity, animal.TraitBits), emerging: false);
                }
            }
        }

        private bool HasWish(uint instanceId) => _context.Daily != null && _context.Daily.Wishes.WishOf(instanceId, out _);

        private int ViewIndexOf(uint instanceId)
        {
            for (int i = 0; i < _views.Length; i++)
                if (_views[i] != null && _views[i].OwnerInstanceId == instanceId && _agentByView[i] != AnimalScheduler.InvalidHandle)
                    return i;
            return -1;
        }

        /// <summary>
        /// T36: the animal of a new wish comes into view if it is in the reserve (its bubble must be seen): the oldest visible
        /// animal without a wish steps back for it. Nothing when it is already visible or no room can be made.
        /// </summary>
        public void EnsureVisible(uint instanceId)
        {
            if (IsVisible(instanceId) || Hatching || !_context.State.Collection.TryGet(instanceId, out var animal))
                return;
            var area = _walkArea;
            var position = area.PointAt(SeedHash.Unit(animal.Seed, 0x51A70001u) * 0.9f + 0.05f, SeedHash.Unit(animal.Seed, 0x51A70002u) * 0.9f + 0.05f);
            if (!_context.Visuals.TryResolve(animal, position, SeedHash.Unit(animal.Seed, 0x51A70003u) * 360f, out var snapshot))
                return;
            if (!TryAcquire(snapshot, out var view))
                return;
            AddToMeadow(view, animal.Species, animal.InstanceId, animal.Seed, RareGlow.KindOf(animal.Rarity, animal.TraitBits), emerging: false);
        }

        /// <summary>T36: screen point just above an animal's head (the wish card, the tutorial paw); false when not on screen.</summary>
        public bool TryGetAnimalScreenPoint(uint instanceId, out Vector2 screen)
        {
            screen = default;
            int index = ViewIndexOf(instanceId);
            var camera = _rig != null ? _rig.Camera : Camera.main;
            if (index < 0 || camera == null)
                return false;
            float scale = _views[index].transform.localScale.x;
            var point = camera.WorldToScreenPoint(_scheduler.GetPosition(_agentByView[index]) + new Vector3(0f, 0.45f * scale, 0f));
            if (point.z <= 0f)
                return false;
            screen = new Vector2(point.x, point.y);
            return true;
        }

        /// <summary>T36 (doc 05 sheet 17): a wish fulfilled in the world: the animal hops, a happy voice, hearts above it.</summary>
        public void PlayWishDoneAt(uint instanceId, float time)
        {
            int index = ViewIndexOf(instanceId);
            if (index < 0)
                return;
            float scale = _views[index].transform.localScale.x;
            var position = _scheduler.GetPosition(_agentByView[index]);
            _vfx?.Play(VfxId.FavoriteHeart, position + new Vector3(0f, 0.3f * scale, 0f), time);
            _scheduler.Poke(_agentByView[index], time);
            _context.Audio.Play(AudioEventId.AnimalHappy, position + new Vector3(0f, 0.2f, 0f), _views[index].Species);
        }

        /// <summary>T36 (doc 05 sheet 17, H-VFX-070): a wish bubble burst at its place: the effect, then the animal hops happily.</summary>
        public void PlayWishBurst(uint instanceId, Vector3 point, float time)
        {
            _vfx?.Play(VfxId.WishDone, point, time);
            int index = ViewIndexOf(instanceId);
            if (index < 0)
                return;
            _scheduler.Poke(_agentByView[index], time);
            _context.Audio.Play(AudioEventId.AnimalHappy, _scheduler.GetPosition(_agentByView[index]) + new Vector3(0f, 0.2f, 0f),
                _views[index].Species);
        }

        public bool TryWorldToScreen(Vector3 world, out Vector2 screen)
        {
            screen = default;
            var camera = _rig != null ? _rig.Camera : Camera.main;
            if (camera == null)
                return false;
            var point = camera.WorldToScreenPoint(world);
            if (point.z <= 0f)
                return false;
            screen = new Vector2(point.x, point.y);
            return true;
        }

        /// <summary>T36: an egg won (a Discovery egg) waits on the hatch button, selected (« Gratuit »).</summary>
        public void SelectEgg(ContentId egg)
        {
            _choice.Refresh();
            for (int i = 0; i < _choice.Count; i++)
            {
                if (_choice[i] != egg)
                    continue;
                _choice.Select(i);
                PushChoice(false);
                return;
            }
        }

        /// <summary>T36: an information card (the gift's odds), over Today if it is open.</summary>
        public void ShowInfo(string title, string body)
        {
            if (_modalView == null || _modalView.IsOpen)
                return;
            _modalView.Show(ModalKind.Info, title, body, _context.Localization.Get(FeedbackText.ModalOkButton), null, this);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
        }

        private bool IsVisible(uint instanceId)
        {
            for (int i = 0; i < _views.Length; i++)
                if (_views[i] != null && _views[i].OwnerInstanceId == instanceId)
                    return true;
            return false;
        }

        /// <summary>The visible allowance this refuge shows now (arrival zone plus the open zones, within the budget).</summary>
        public int VisibleAllowance => _visibleAllowance;

        /// <summary>The area animals walk in now.</summary>
        public WanderArea WalkArea => _walkArea;

        /// <summary>
        /// T35A: a zone opened or a zone's chunk finished loading: animals walk in the new area. Those already
        /// standing stay where they are; the next walks spread them.
        /// </summary>
        public void SetWalkData(in RefugeWalkData walk)
        {
            _walkArea = walk.Area;
            _scheduler.SetArea(walk.Area);
            _scheduler.SetGround(walk.Ground);
            _scheduler.SetObstacles(walk.Obstacles);
            _scheduler.SetInterests(walk.Interests);
        }

        /// <summary>
        /// T35A: more animals may be seen (a zone opened). The new ones appear in <paramref name="spawnArea"/> (the new
        /// zone, away from the egg); fewer never removes anyone at once (the oldest step back at the next hatches).
        /// </summary>
        public void SetVisibleAllowance(int allowance, WanderArea spawnArea)
        {
            int before = _visibleAllowance;
            _visibleAllowance = Math.Max(1, Math.Min(allowance, _visibleBudget));
            if (_visibleAllowance > before)
                SpawnSaved(spawnArea);
        }

        /// <summary>T35A: visible animals standing in a zone (and, with <paramref name="heading"/>, those walking to it).</summary>
        public int CountAnimalsIn(in WanderArea zone, bool heading)
        {
            int count = 0;
            for (int i = 0; i < _views.Length; i++)
            {
                int handle = _agentByView[i];
                if (_views[i] == null || handle == AnimalScheduler.InvalidHandle)
                    continue;
                if (heading ? _scheduler.IsInOrHeadingInto(handle, zone) : zone.Contains(_scheduler.GetPosition(handle)))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// T35A, zone opening: the animals nearest to the new zone walk into it (two or three, doc 02 section 4.6).
        /// Returns how many set off.
        /// </summary>
        public int SendAnimalsInto(WanderArea zone, int count)
        {
            // Each walker goes straight ahead at its own depth, at least a lane apart from the others: their paths never
            // cross, so they never stand in each other's way at a narrow passage between two props.
            const float LaneSpacing = 0.9f;
            const float LaneMargin = 0.8f;
            count = Math.Min(count, 8);
            Span<float> lanes = stackalloc float[8];
            float minZ = zone.Center.z - zone.HalfSize.y + LaneMargin;
            float maxZ = zone.Center.z + zone.HalfSize.y - LaneMargin;
            // Lanes already taken by animals on their way there (sent a moment ago): the new walkers keep clear of them.
            int taken = 0;
            for (int i = 0; i < _views.Length && taken < lanes.Length; i++)
            {
                int handle = _agentByView[i];
                if (_views[i] != null && handle != AnimalScheduler.InvalidHandle && _scheduler.GetGoalLegs(handle) > 0
                    && zone.Contains(_scheduler.GetGoal(handle)) && !zone.Contains(_scheduler.GetPosition(handle)))
                    lanes[taken++] = _scheduler.GetGoal(handle).z;
            }
            count = Math.Min(count, lanes.Length - taken);
            int sent = 0;
            // First the animals with a free straight lane (no prop to go around, no animal standing in it), then lanes free
            // of props only, then any (they plan around the prop).
            for (int pass = 0; pass < 3 && sent < count; pass++)
            {
                int tries = 0;
                float tried = float.MinValue;
                while (sent < count && tries < _views.Length)
                {
                    tries++;
                    // The next nearest animal not tried yet (by its distance to the zone along the strip).
                    int best = -1;
                    float bestDistance = float.MaxValue;
                    for (int i = 0; i < _views.Length; i++)
                    {
                        int handle = _agentByView[i];
                        if (_views[i] == null || handle == AnimalScheduler.InvalidHandle || _scheduler.IsEmerging(handle)
                            || _scheduler.GetState(handle) == AnimalBehaviorState.Wander || zone.Contains(_scheduler.GetPosition(handle)))
                            continue;
                        float distance = Mathf.Abs(_scheduler.GetPosition(handle).x - zone.Center.x);
                        if (distance > tried && distance < bestDistance)
                        {
                            bestDistance = distance;
                            best = handle;
                        }
                    }
                    if (best < 0)
                        break;
                    tried = bestDistance;
                    var position = _scheduler.GetPosition(best);
                    float lane = Mathf.Clamp(position.z, minZ, maxZ);
                    bool crowded = false;
                    for (int k = 0; k < taken + sent && !crowded; k++)
                        crowded = Mathf.Abs(lanes[k] - lane) < LaneSpacing;
                    if (crowded)
                        continue;
                    float u = (sent + 0.5f) / count;
                    float x = zone.Center.x - zone.HalfSize.x + (0.25f + 0.2f * u) * 2f * zone.HalfSize.x;
                    var target = new Vector3(x, position.y, lane);
                    if (pass < 2 && !_scheduler.IsStraightPathClear(best, target))
                        continue;
                    if (pass == 0 && !_scheduler.IsLaneFreeOfAnimals(best, target))
                        continue;
                    if (_scheduler.TryWalkTo(best, target, _time))
                        lanes[taken + sent++] = lane;
                }
            }
            return sent;
        }

        /// <summary>emerging: the newborn stands on the egg spot and first walks out of it.</summary>
        private void AddToMeadow(AnimalView view, ContentId species, uint instanceId, uint seed, RareGlowKind rare, bool emerging)
        {
            // Views stay packed from index 0 in arrival order, so index 0 is always the oldest visible animal.
            int slot = VisibleAnimals;
            if (VisibleAnimals >= _visibleAllowance || slot >= _views.Length)
            {
                // Allowance reached: the oldest visible animal steps back into the reserve; T36: one with a pending wish
                // stays (its bubble is in the world), the next oldest goes instead.
                int leave = 0;
                for (int i = 0; i < VisibleAnimals; i++)
                {
                    if (_views[i] != null && !HasWish(_views[i].OwnerInstanceId) && _views[i].OwnerInstanceId != instanceId)
                    {
                        leave = i;
                        break;
                    }
                }
                _scheduler.Unregister(_agentByView[leave]);
                // T39: a view hidden for an activity goes back to its pool drawable again.
                if (_actorsHidden)
                    ShowActor(_views[leave], true);
                _animals.Release(_views[leave]);
                VisibleAnimals--;
                int last = VisibleAnimals;
                Array.Copy(_views, leave + 1, _views, leave, last - leave);
                Array.Copy(_agentByView, leave + 1, _agentByView, leave, last - leave);
                Array.Copy(_rareByView, leave + 1, _rareByView, leave, last - leave);
                slot = last;
            }
            var transform = view.transform;
            _views[slot] = view;
            // T39: an animal arriving while an activity plays in the refuge stays hidden with the others until it ends.
            if (_actorsHidden)
                ShowActor(view, false);
            _rareByView[slot] = rare;
            _agentByView[slot] = _scheduler.Register(view, instanceId, seed, transform.position, transform.eulerAngles.y, ProfileOf(species), _time,
                transform.localScale.x, emerging);
            VisibleAnimals++;
        }

        /// <summary>
        /// T34A rare glints: the next visible rare animal (round robin) gets a few soft glints in its glow color. One
        /// effect at a time, never one per animal per frame; nothing while a screen covers the world.
        /// </summary>
        private void TickRareGlints()
        {
            if (_time < _nextGlintTime)
                return;
            _nextGlintTime = _time + RareGlintSeconds * (PresentationQuality.Tier == QualityTier.Low ? 2f : 1f);
            if (ModalOpen || Hatching)
                return;
            int count = _views.Length;
            for (int n = 0; n < count; n++)
            {
                int i = (_glintCursor + n) % count;
                if (_views[i] == null || _rareByView[i] == RareGlowKind.None || _agentByView[i] == AnimalScheduler.InvalidHandle)
                    continue;
                _glintCursor = i + 1;
                float scale = _views[i].transform.localScale.x;
                float pick = Mathf.Repeat(_time * 0.37f, 1f);
                var position = _scheduler.GetPosition(_agentByView[i]) + new Vector3((pick - 0.5f) * 0.2f, 0.32f * scale, 0f);
                _vfx.Play(VfxId.RareGlint, position, _time, RareGlow.GlintColor(_rareByView[i], pick));
                return;
            }
        }

        private static bool HitsSphere(Ray ray, Vector3 center, float radius)
        {
            var toCenter = center - ray.origin;
            float along = Vector3.Dot(toCenter, ray.direction);
            if (along < 0f)
                return false;
            return (toCenter - ray.direction * along).sqrMagnitude <= radius * radius;
        }
    }
}
