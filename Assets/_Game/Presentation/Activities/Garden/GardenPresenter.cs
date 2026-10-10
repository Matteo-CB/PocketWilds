using System;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T43 « Jardin » (doc 01 Contenu sections 8 and 9, S-ACT-006; doc 05 sheet 24). The second meta activity: the hub's
    /// Garden card opens « le potager du refuge », its own little set far below the refuge (PF_Activity_Garden_Set at
    /// <see cref="GardenLayout.SetOrigin"/>, its own camera and light, the refuge not drawn), the plants' stage prefabs
    /// (<see cref="GardenPlantVisuals"/>) and the overlay PF_UI_Garden, all three from the Addressables group Activity_Garden:
    /// loaded when the garden opens, released when it closes. Five raised beds: a bare bed opens the seed sheet, the can then
    /// a sprout waters it (6 round drops), a ripe plant hops (Bond 1.25) and flies into the basket (450 ms) while its
    /// products count up; « Tout récolter » picks the beds 120 ms apart with one vibration and a rising pitch; the
    /// neighbours' help shows two small green hearts once; the garden level-up shows a soft banner with what opened; the
    /// first visit's paw shows a bare bed, the Sweet Clover packet, the can and the sprout. Out of the garden: the hub card's
    /// line (« Nouveau », « Prêt dans 25 min », « {n} prêts ») and the Play button's dot. Ticked by the activity presenter;
    /// nothing allocates per frame while the garden shows (labels change only when their text changes, plant views pooled).
    /// </summary>
    public sealed partial class GardenPresenter : IGardenScreenActions, IDisposable
    {
        /// <summary>The hub line and the plots' slow parts (help, gems, level) are checked twice a second.</summary>
        private const float CheckInterval = 0.5f;

        private static readonly ContentId SweetClover = ContentId.FromSource("plant.sweet_clover");

        private enum Phase : byte
        {
            Closed = 0,
            Loading = 1,
            Browsing = 2,
        }

        private readonly GameContext _context;
        private readonly IGarden _garden;
        private readonly ActivityPresenter _activities;
        private readonly ILocalization _text;
        private readonly GardenTimeText _time = new GardenTimeText();
        private readonly Action<int> _onPlotChanged;

        // Tests and captures: given assets (else Addressables).
        private readonly GameObject _setPrefab;
        private readonly GardenPlantVisuals _visualsAsset;
        private readonly GameObject _screenPrefab;

        private Phase _phase;
        private float _clock;
        private float _nextCheck;

        // Loading.
        private AsyncOperationHandle<GameObject> _setHandle;
        private AsyncOperationHandle<GardenPlantVisuals> _plantsHandle;
        private AsyncOperationHandle<GameObject> _screenHandle;
        private GameObject _screenObject;
        private IGardenScreenView _view;
        private bool _failed;

        // What the overlay shows.
        private readonly GardenPlotState[] _stateShown = new GardenPlotState[GardenLayout.PlotCount];
        private readonly bool[] _wateredShown = new bool[GardenLayout.PlotCount];
        private readonly bool[] _helpedShown = new bool[GardenLayout.PlotCount];
        private readonly bool[] _known = new bool[GardenLayout.PlotCount];
        private readonly ContentId[] _packets = new ContentId[GardenScreenLayout.PacketCapacity];
        private readonly int[] _challengeRow = new int[GardenScreenLayout.ChallengeCapacity];
        private int _packetCount;
        private int _challengeCount;
        private bool _dirty;
        private long _gemsShown = -1;
        private int _levelShown = -1;
        private int _pointsShown = -1;
        private int _forNextShown = -1;
        private int _harvestsShown = -1;
        private bool _toolsKnown;
        private GardenToolsModel _tools;

        // Touches.
        private int _selected = -1;
        private int _seedPlot = -1;
        private bool _canArmed;
        private float _confirmUntil = -1f;
        private int _confirmPlot = -1;

        // « Tout récolter ».
        private readonly int[] _chain = new int[GardenLayout.PlotCount];
        private int _chainCount;
        private int _chainNext;
        private float _chainAt;

        // The first visit's paw (1 a bare bed, 2 the Sweet Clover packet, 3 the can, 4 the sprout).
        private int _tutorial;

        // Out of the garden: the hub card's line.
        private long _statusKey = long.MinValue;
        private string _status;
        private bool _statusDirty = true;

        /// <summary>
        /// The shipped wiring (the activity presenter calls it when it is given the garden): the garden's presenter over the
        /// service, the refuge of the activity host for the world and its effects. Null without a garden.
        /// </summary>
        public static GardenPresenter Create(GameContext context, ActivityPresenter activities, IGarden garden)
        {
            if (garden == null || activities == null)
                return null;
            return new GardenPresenter(context, garden, activities);
        }

        /// <summary>The shipped garden: its set, plants and overlay come from Addressables. The presenter registers itself with the hub.</summary>
        public GardenPresenter(GameContext context, IGarden garden, ActivityPresenter activities)
            : this(context, garden, activities, null, null, null)
        {
        }

        /// <summary>Tests and captures: given prefabs and plant visuals (each null: from Addressables).</summary>
        public GardenPresenter(GameContext context, IGarden garden, ActivityPresenter activities, GameObject setPrefab, GardenPlantVisuals visuals,
            GameObject screenPrefab)
        {
            _context = context;
            _garden = garden ?? throw new ArgumentNullException(nameof(garden));
            _activities = activities;
            _text = context?.Localization ?? activities?.Host?.Text ?? KeysOnly.Instance;
            _setPrefab = setPrefab;
            _visualsAsset = visuals;
            _screenPrefab = screenPrefab;
            _time.Bind(_text);
            _onPlotChanged = OnPlotChanged;
            _garden.PlotChanged += _onPlotChanged;
            for (int i = 0; i < _challengeRow.Length; i++)
                _challengeRow[i] = -1;
            InitStage();
            if (activities != null)
                activities.Garden = this;
        }

        /// <summary>The garden is loading or shown (world touches and hatching wait).</summary>
        public bool IsOpen => _phase != Phase.Closed;

        public bool Loading => _phase == Phase.Loading;

        public IGarden Service => _garden;

        public IGardenScreenView View => _view;

        public GameObject ScreenObject => _screenObject;

        /// <summary>Every Addressables handle of the visit is held (released when the garden closes).</summary>
        public bool AssetsLoaded => _setHandle.IsValid() || _plantsHandle.IsValid() || _screenHandle.IsValid();

        public bool SeedsOpen => _view != null && _view.SeedsShown;

        public int SeedPlot => _seedPlot;

        public bool CanArmed => _canArmed;

        public int Selected => _selected;

        /// <summary>The packets of the seed sheet (tests: the open plants first).</summary>
        public int PacketCount => _packetCount;

        public ContentId PacketAt(int cell) => (uint)cell < (uint)_packetCount ? _packets[cell] : ContentId.None;

        /// <summary>The first visit: 1 a bare bed, 2 the Sweet Clover packet, 3 the can, 4 the sprout; 0 none.</summary>
        public int TutorialStep => _tutorial;

        /// <summary>« Tout récolter » is still picking beds.</summary>
        public bool Harvesting => _chainNext < _chainCount;

        /// <summary>The last harvest handed out (tests).</summary>
        public GardenHarvest LastHarvest { get; private set; }

        public int Harvests { get; private set; }

        /// <summary>Captures without a refuge: the effects player (else the refuge's).</summary>
        public FeedbackVfx VfxOverride { get; set; }

        // ---------------- the hub ----------------

        /// <summary>The hub's Play on the Garden card: the garden opens over a hidden refuge.</summary>
        public void OpenFromHub()
        {
            if (IsOpen || !_garden.IsOpen)
                return;
            _activities?.CloseHubQuietly();
            Sound(AudioEventId.PanelOpen);
            Haptic(HapticEventId.TapLight);
            Open();
        }

        /// <summary>« Nouveau » before the first sprout; then « {n} prêts » with ripe plants, else the soonest ripening (minutes).</summary>
        public ActivityCardState CardStatus(out string status)
        {
            UpdateStatus();
            status = _status;
            return IsNew ? ActivityCardState.New : ActivityCardState.Open;
        }

        /// <summary>Never planted: level 1, no point, nothing growing nor ripe.</summary>
        private bool IsNew => _garden.Level <= 1 && _garden.PointsInLevel == 0 && _garden.RipeCount == 0 && _garden.SoonestRipe < 0.0;

        /// <summary>The hub line changed since the last <see cref="CardStatus"/> (checked twice a second by the hub).</summary>
        public bool StatusChanged => _statusDirty || StatusKey() != _statusKey;

        /// <summary>The Play button's dot: a plant ripe, or the garden open and never planted.</summary>
        public bool WantsDot => _garden.IsOpen && (_garden.RipeCount > 0 || IsNew);

        private long StatusKey()
        {
            int ripe = _garden.RipeCount;
            if (ripe > 0)
                return -1000L - ripe;
            double soonest = _garden.SoonestRipe;
            if (soonest < 0.0)
                return -1L;
            return (long)Math.Ceiling(Math.Max(1.0, soonest) / 60.0);
        }

        private void UpdateStatus()
        {
            long key = StatusKey();
            if (!_statusDirty && key == _statusKey)
                return;
            _statusDirty = false;
            _statusKey = key;
            int ripe = _garden.RipeCount;
            if (ripe > 0)
                _status = Counted(GardenText.ReadyCount, ripe);
            else if (key > 0)
                _status = _time.ReadyInString(Math.Max(60.0, key * 60.0));
            else
                _status = null;
        }

        // ---------------- opening and closing ----------------

        private void Open()
        {
            _phase = Phase.Loading;
            _dirty = true;
            _failed = false;
            if (_view == null)
            {
                if (_screenPrefab != null)
                    BuildScreen(_screenPrefab);
                else
                    _screenHandle = Load<GameObject>(GardenLayout.ScreenAddress);
            }
            if (_set == null)
            {
                if (_setPrefab != null)
                    BuildSet(_setPrefab);
                else
                    _setHandle = Load<GameObject>(GardenLayout.SetAddress);
            }
            if (_visuals == null)
            {
                if (_visualsAsset != null)
                    _visuals = _visualsAsset;
                else
                    _plantsHandle = Load<GardenPlantVisuals>(GardenLayout.PlantsAddress);
            }
            Advance();
        }

        private AsyncOperationHandle<T> Load<T>(string address) where T : Object
        {
            foreach (var locator in Addressables.ResourceLocators)
                if (locator.Locate(address, typeof(T), out _))
                    return Addressables.LoadAssetAsync<T>(address);
            GameLog.Error(LogCategory.Presentation, $"Garden: no Addressables entry {address} (run the garden pipelines: Build Garden Set, Build Garden Screen (T43)).");
            if (typeof(T) != typeof(GardenPlantVisuals))
                _failed = true;
            return default;
        }

        /// <summary>Moves the loading on; once the set and the overlay are there the garden shows.</summary>
        private void Advance()
        {
            if (_phase != Phase.Loading)
                return;
            if (!_failed)
            {
                if (_screenHandle.IsValid() && _view == null)
                {
                    if (!_screenHandle.IsDone)
                        return;
                    if (_screenHandle.Status == AsyncOperationStatus.Succeeded && _screenHandle.Result != null)
                        BuildScreen(_screenHandle.Result);
                    else
                        _failed = true;
                }
                if (_setHandle.IsValid() && _set == null && !_failed)
                {
                    if (!_setHandle.IsDone)
                        return;
                    if (_setHandle.Status == AsyncOperationStatus.Succeeded && _setHandle.Result != null)
                        BuildSet(_setHandle.Result);
                    else
                        _failed = true;
                }
                if (_plantsHandle.IsValid() && _visuals == null)
                {
                    if (!_plantsHandle.IsDone)
                        return;
                    if (_plantsHandle.Status == AsyncOperationStatus.Succeeded && _plantsHandle.Result != null)
                        _visuals = _plantsHandle.Result;
                    else
                        GameLog.Warning(LogCategory.Presentation, "Garden: the plant visuals did not load (" + GardenLayout.PlantsAddress + "): beds show no plant.");
                }
            }
            if (_failed || _view == null || _set == null)
            {
                GameLog.Error(LogCategory.Presentation, "Garden: the set or the overlay did not load; back to the hub.");
                _phase = Phase.Closed;
                Release();
                _activities?.ReturnFromMeta(ActivityId.Garden);
                return;
            }
            ShowGarden();
        }

        private void BuildScreen(GameObject prefab)
        {
            var parent = _activities?.Host?.Root;
            _screenObject = Object.Instantiate(prefab, parent);
            _screenObject.name = GardenLayout.ScreenPrefabName;
            _view = _screenObject.GetComponent<IGardenScreenView>();
            if (_view == null || !_view.Valid)
            {
                GameLog.Error(LogCategory.Presentation, "Garden: the overlay prefab has no complete GardenScreenView (rebuild it with GardenScreenPipeline).");
                DestroyObject(_screenObject);
                _screenObject = null;
                _view = null;
                _failed = true;
                return;
            }
            _view.Bind(_text, this);
            _view.Hide();
        }

        private void ShowGarden()
        {
            _phase = Phase.Browsing;
            _view.ReduceMotion = ReduceMotion;
            for (int i = 0; i < _known.Length; i++)
                _known[i] = false;
            _gemsShown = -1;
            _levelShown = -1;
            _pointsShown = -1;
            _forNextShown = -1;
            _harvestsShown = -1;
            _toolsKnown = false;
            _selected = -1;
            _canArmed = false;
            _seedPlot = -1;
            _confirmUntil = -1f;
            _chainCount = _chainNext = 0;
            _view.ShowSeeds(false);
            EnterSet();
            RebuildChallenges();
            RefreshPlots(true);
            _view.Show();
            PlaceLabels(true);
            _view.Tick(0f);
            _activities?.Host?.SetWorldVisible?.Invoke(false);
            _activities?.PlayHud?.SetPlaying(true);
            UpdateTutorial();
        }

        public void CloseScreen()
        {
            if (_phase != Phase.Browsing)
                return;
            Sound(AudioEventId.UiBack);
            Close(true);
        }

        /// <summary>The garden goes: its set, plants and overlay are released, the refuge and its HUD come back (and the hub).</summary>
        private void Close(bool toHub)
        {
            if (_phase == Phase.Closed)
                return;
            _phase = Phase.Closed;
            _tutorial = 0;
            _chainCount = _chainNext = 0;
            _view?.SetPaw(false, default);
            _view?.Hide();
            ExitSet();
            _activities?.Host?.SetWorldVisible?.Invoke(true);
            _activities?.PlayHud?.SetPlaying(false);
            Release();
            _statusDirty = true;
            if (toHub)
                _activities?.ReturnFromMeta(ActivityId.Garden);
        }

        /// <summary>Lets go of the overlay, the set, the plant views and the three handles.</summary>
        private void Release()
        {
            ReleaseSet();
            _view = null;
            if (_screenObject != null)
                DestroyObject(_screenObject);
            _screenObject = null;
            if (_screenHandle.IsValid())
                Addressables.Release(_screenHandle);
            _screenHandle = default;
            if (_setHandle.IsValid())
                Addressables.Release(_setHandle);
            _setHandle = default;
            if (_plantsHandle.IsValid())
                Addressables.Release(_plantsHandle);
            _plantsHandle = default;
            if (_visualsAsset == null)
                _visuals = null;
            _failed = false;
        }

        // ---------------- Tick ----------------

        /// <summary>Per frame from the activity presenter (unscaled time). No allocation while the garden shows.</summary>
        public void Tick(float deltaTime)
        {
            _clock += deltaTime;
            switch (_phase)
            {
                case Phase.Loading:
                    Advance();
                    break;
                case Phase.Browsing:
                    TickBrowsing(deltaTime);
                    break;
            }
            if (_clock >= _nextCheck)
                _nextCheck = _clock + CheckInterval;
        }

        private void TickBrowsing(float deltaTime)
        {
            if (_confirmUntil >= 0f && _clock > _confirmUntil)
            {
                _confirmUntil = -1f;
                int armed = _confirmPlot;
                _confirmPlot = -1;
                if (armed >= 0)
                    _known[armed] = false;
                _dirty = true;
            }
            bool slow = _clock >= _nextCheck;
            RefreshPlots(false, slow);
            int plots = Plots;
            for (int plot = 0; plot < plots; plot++)
                if (_stateShown[plot] == GardenPlotState.Growing)
                    _view.SetBedTime(plot, _garden.SecondsLeft(plot));
            TickChain();
            TickPlants(deltaTime);
            PlaceLabels(false);
            KeepLighting();
            PollPointer();
            if (_view != null)
                _view.Tick(deltaTime);
        }

        private void OnPlotChanged(int plot)
        {
            _dirty = true;
            _statusDirty = true;
        }

        private int Plots => Mathf.Min(_garden.PlotCount, GardenLayout.PlotCount);

        // ---------------- the plots ----------------

        /// <summary>
        /// The beds as shown: a state, a stage, the drop or the hearts changed (polled each frame: a few reads, no allocation);
        /// the Gems, the level and the challenge's progress with <paramref name="slow"/> (twice a second) or on a change.
        /// </summary>
        private void RefreshPlots(bool force, bool slow = true)
        {
            if (_view == null)
                return;
            bool changed = force || _dirty;
            _dirty = false;
            int plots = Plots;
            for (int plot = 0; plot < GardenLayout.PlotCount; plot++)
            {
                if (plot >= plots)
                {
                    if (force || !_known[plot])
                    {
                        var hidden = new GardenBedModel { State = GardenPlotState.Locked, Unlock = GardenPlotUnlock.None };
                        _view.SetBed(plot, in hidden);
                        _known[plot] = true;
                        _stateShown[plot] = GardenPlotState.Locked;
                    }
                    continue;
                }
                var state = _garden.StateOf(plot);
                bool watered = state == GardenPlotState.Growing && _garden.Watered(plot);
                bool helped = (state == GardenPlotState.Growing || state == GardenPlotState.Ripe) && _garden.Helped(plot);
                SyncPlant(plot, state, !force && _known[plot]);
                if (!force && _known[plot] && state == _stateShown[plot] && watered == _wateredShown[plot] && helped == _helpedShown[plot])
                    continue;
                if (_known[plot] && helped && !_helpedShown[plot])
                    PlayHelp(plot);
                if (_selected == plot && state != GardenPlotState.Growing)
                    _selected = -1;
                _known[plot] = true;
                _stateShown[plot] = state;
                _wateredShown[plot] = watered;
                _helpedShown[plot] = helped;
                changed = true;
                var model = new GardenBedModel
                {
                    State = state,
                    Unlock = _garden.UnlockOf(plot),
                    Watered = watered,
                    Helped = helped,
                    Selected = _selected == plot,
                    Price = _garden.PlotPrice,
                    Armed = _confirmPlot == plot && _confirmUntil >= 0f,
                };
                _view.SetBed(plot, in model);
                SetCover(plot, state == GardenPlotState.Locked);
                if (state == GardenPlotState.Growing)
                    _view.SetBedTime(plot, _garden.SecondsLeft(plot));
            }
            if (changed || slow)
                RefreshTop();
            if (changed)
            {
                _statusDirty = true;
                RefreshTools();
                UpdateTutorial();
            }
        }

        private void RefreshTop()
        {
            long gems = State?.Wallet.Gems ?? 0L;
            if (gems != _gemsShown)
            {
                _gemsShown = gems;
                _view.SetGems(gems);
            }
            int level = _garden.Level, points = _garden.PointsInLevel, forNext = _garden.PointsForNext;
            if (level != _levelShown || points != _pointsShown || forNext != _forNextShown)
            {
                _levelShown = level;
                _pointsShown = points;
                _forNextShown = forNext;
                _view.SetLevel(level, points, forNext);
            }
            int harvests = _garden.ActiveChallenge >= 0 ? _garden.ChallengeHarvests : -1;
            if (harvests != _harvestsShown)
            {
                _harvestsShown = harvests;
                RebuildChallenges();
            }
        }

        private void RefreshTools()
        {
            bool speed = _selected >= 0 && _selected < Plots && _garden.StateOf(_selected) == GardenPlotState.Growing;
            var tools = new GardenToolsModel
            {
                CanArmed = _canArmed,
                CanUsable = FirstToWater() >= 0,
                HarvestAll = _garden.RipeCount >= 2,
                SpeedUpShown = speed,
                SpeedUpCost = speed ? _garden.SpeedUpCost(_selected) : 0,
                SpeedUpsLeft = _garden.SpeedUpsLeftToday,
            };
            if (_toolsKnown && tools.CanArmed == _tools.CanArmed && tools.CanUsable == _tools.CanUsable && tools.HarvestAll == _tools.HarvestAll
                && tools.SpeedUpShown == _tools.SpeedUpShown && tools.SpeedUpCost == _tools.SpeedUpCost && tools.SpeedUpsLeft == _tools.SpeedUpsLeft)
                return;
            _toolsKnown = true;
            _tools = tools;
            _view.SetTools(in tools);
        }

        private int FirstToWater()
        {
            int plots = Plots;
            for (int plot = 0; plot < plots; plot++)
                if (_garden.StateOf(plot) == GardenPlotState.Growing && !_garden.Watered(plot))
                    return plot;
            return -1;
        }

        // ---------------- a bed touched (picked from the set camera, or by tests and captures) ----------------

        /// <summary>A bed is touched: bare, the seed sheet; growing, watered with the can or selected; ripe, harvested; locked, bought.</summary>
        public void TapBed(int plot)
        {
            if (_phase != Phase.Browsing || (uint)plot >= (uint)Plots)
                return;
            switch (_garden.StateOf(plot))
            {
                case GardenPlotState.Empty:
                    _canArmed = false;
                    OpenSeeds(plot);
                    break;
                case GardenPlotState.Growing:
                    if (_canArmed)
                    {
                        WaterPlot(plot);
                    }
                    else
                    {
                        int previous = _selected;
                        _selected = _selected == plot ? -1 : plot;
                        _known[plot] = false;
                        if (previous >= 0)
                            _known[previous] = false;
                        if (_view.SeedsShown)
                            CloseSeeds();
                        Sound(AudioEventId.UiTab);
                        _dirty = true;
                        RefreshPlots(false);
                    }
                    break;
                case GardenPlotState.Ripe:
                    if (_view.SeedsShown)
                        CloseSeeds();
                    HarvestPlot(plot, 0, true);
                    break;
                case GardenPlotState.Locked:
                    TapLocked(plot);
                    break;
            }
        }

        private void TapLocked(int plot)
        {
            var unlock = _garden.UnlockOf(plot);
            if (unlock == GardenPlotUnlock.Club)
            {
                _view.ShowNote(_text.Get(GardenText.ClubSoon));
                Sound(AudioEventId.UiTab);
                return;
            }
            if (unlock != GardenPlotUnlock.Gems)
                return;
            // The first tap arms the purchase; the second, within 3 s, spends the Gems.
            if (_confirmUntil < 0f || _confirmPlot != plot)
            {
                _confirmUntil = _clock + GardenScreenLayout.ConfirmSeconds;
                _confirmPlot = plot;
                _known[plot] = false;
                _view.ShowNote(_text.Get(GardenText.PlotConfirm));
                Sound(AudioEventId.UiTab);
                _dirty = true;
                return;
            }
            _confirmUntil = -1f;
            _confirmPlot = -1;
            _known[plot] = false;
            if (!_garden.TryBuyPlot())
            {
                NotEnoughGems();
                _dirty = true;
                return;
            }
            Sound(AudioEventId.PurchaseComplete);
            Haptic(HapticEventId.PurchaseSuccess);
            RefreshPlots(true);
            // The new bed's label moves from its cover to under the bed.
            PlaceLabels(true);
        }

        // ---------------- the seed sheet ----------------

        /// <summary>The seed sheet for a bare bed: the open plants first (those with seeds first), then the locked ones.</summary>
        public void OpenSeeds(int plot)
        {
            if (_view == null || _phase != Phase.Browsing)
                return;
            _seedPlot = plot;
            _selected = -1;
            RebuildPackets();
            _view.ShowSeeds(true);
            Sound(AudioEventId.PanelOpen);
            _dirty = true;
            RefreshPlots(false);
            UpdateTutorial();
        }

        public void CloseSeeds()
        {
            if (_view == null || !_view.SeedsShown)
                return;
            _view.ShowSeeds(false);
            _seedPlot = -1;
            Sound(AudioEventId.UiBack);
            UpdateTutorial();
        }

        /// <summary>The packets: open with seeds, open without, then the locked ones (by level, then the biome plants). On opening only.</summary>
        private void RebuildPackets()
        {
            _packetCount = 0;
            int count = _garden.PlantCount;
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < count && _packetCount < _packets.Length; i++)
                {
                    var plant = _garden.PlantAt(i);
                    bool open = _garden.IsPlantOpen(plant.Id);
                    int rank = open ? (_garden.SeedsOf(plant.Id) > 0 ? 0 : 1) : 2;
                    if (rank != pass)
                        continue;
                    _packets[_packetCount++] = plant.Id;
                }
            }
            for (int cell = 0; cell < GardenScreenLayout.PacketCapacity; cell++)
            {
                var model = new GardenPacketModel();
                if (cell < _packetCount && _garden.TryGetPlant(_packets[cell], out var plant))
                {
                    bool open = _garden.IsPlantOpen(plant.Id);
                    model.Shown = true;
                    model.Key = plant.Key;
                    model.Name = Text(GardenText.PlantName(plant.Key));
                    model.Seeds = _garden.SeedsOf(plant.Id);
                    model.Open = open;
                    model.Lock = open ? null : LockText(in plant);
                }
                _view.SetPacket(cell, in model);
            }
        }

        private string LockText(in GardenPlant plant)
        {
            if (plant.Level > 0)
                return _text.Get(GardenText.LevelUp).Replace("{n}", plant.Level.ToString());
            if (_context?.Content != null && plant.Biome != ContentId.None && _context.Content.Biomes.TryGet(plant.Biome, out var biome)
                && _text.Has(biome.DisplayKey))
                return _text.Get(biome.DisplayKey);
            return _text.Get(GardenText.PlantBiome);
        }

        public void TapPacket(int cell)
        {
            if (_phase != Phase.Browsing || _view == null || !_view.SeedsShown || (uint)cell >= (uint)_packetCount || _seedPlot < 0)
                return;
            var id = _packets[cell];
            if (!_garden.TryGetPlant(id, out var plant))
                return;
            if (!_garden.IsPlantOpen(id))
            {
                Refuse(LockText(in plant));
                return;
            }
            if (_garden.SeedsOf(id) <= 0)
            {
                Refuse(_text.Get(GardenText.NoSeeds));
                return;
            }
            int plot = _seedPlot;
            var result = _garden.Plant(plot, id);
            if (result != GardenPlantResult.Planted)
            {
                Refuse(result == GardenPlantResult.NoSeed ? _text.Get(GardenText.NoSeeds) : result == GardenPlantResult.PlantLocked ? LockText(in plant) : null);
                return;
            }
            _view.ShowSeeds(false);
            _seedPlot = -1;
            if (_context?.Audio != null)
                GardenAudio.Plant(_context.Audio);
            Haptic(HapticEventId.TapLight);
            PlayPlanted(plot);
            _statusDirty = true;
            _dirty = true;
            RefreshPlots(false);
            UpdateTutorial();
        }

        // ---------------- the can ----------------

        public void TapCan()
        {
            if (_phase != Phase.Browsing)
                return;
            if (_canArmed)
            {
                _canArmed = false;
                Sound(AudioEventId.ToggleOff);
            }
            else
            {
                if (FirstToWater() < 0)
                {
                    Refuse(_text.Get(GardenText.AlreadyWatered));
                    return;
                }
                // A sprout already selected is watered at once; else the next sprout touched.
                if (_selected >= 0 && _garden.StateOf(_selected) == GardenPlotState.Growing && !_garden.Watered(_selected))
                {
                    WaterPlot(_selected);
                    return;
                }
                _canArmed = true;
                if (_view.SeedsShown)
                    CloseSeeds();
                _view.ShowNote(_text.Get(GardenText.CanHint));
                Sound(AudioEventId.ToggleOn);
            }
            Haptic(HapticEventId.TapLight);
            _toolsKnown = false;
            RefreshTools();
            UpdateTutorial();
        }

        private void WaterPlot(int plot)
        {
            _canArmed = false;
            if (!_garden.Water(plot))
            {
                Refuse(_text.Get(GardenText.AlreadyWatered));
                _toolsKnown = false;
                RefreshTools();
                UpdateTutorial();
                return;
            }
            if (_context?.Audio != null)
                GardenAudio.Water(_context.Audio);
            Haptic(HapticEventId.TapLight);
            PlayWatered(plot);
            _statusDirty = true;
            _dirty = true;
            _toolsKnown = false;
            RefreshPlots(false);
            UpdateTutorial();
        }

        // ---------------- « Finir maintenant » ----------------

        public void SpeedUp()
        {
            int plot = _selected;
            if (_phase != Phase.Browsing || (uint)plot >= (uint)Plots || _garden.StateOf(plot) != GardenPlotState.Growing)
                return;
            if (_garden.SpeedUpsLeftToday <= 0)
            {
                Refuse(_text.Get(GardenText.SpeedUpsNone));
                return;
            }
            if (!_garden.TrySpeedUp(plot))
            {
                NotEnoughGems();
                return;
            }
            Sound(AudioEventId.PurchaseComplete);
            Haptic(HapticEventId.PurchaseSuccess);
            _selected = -1;
            _statusDirty = true;
            _dirty = true;
            RefreshPlots(false);
        }

        // ---------------- the harvest (doc 05 sheet 24) ----------------

        /// <summary>« Tout récolter »: every ripe bed, 120 ms apart, one vibration, the pitch rising.</summary>
        public void HarvestAll()
        {
            if (_phase != Phase.Browsing || Harvesting || _garden.RipeCount < 2)
                return;
            _chainCount = 0;
            _chainNext = 0;
            int plots = Plots;
            for (int plot = 0; plot < plots; plot++)
                if (_garden.StateOf(plot) == GardenPlotState.Ripe)
                    _chain[_chainCount++] = plot;
            if (_chainCount == 0)
                return;
            if (_view.SeedsShown)
                CloseSeeds();
            Haptic(HapticEventId.TapLight);
            _chainAt = _clock;
            TickChain();
        }

        private void TickChain()
        {
            while (_chainNext < _chainCount && _clock >= _chainAt)
            {
                int index = _chainNext++;
                HarvestPlot(_chain[index], index, false);
                _chainAt += GardenScreenLayout.ChainGap;
            }
        }

        /// <summary>
        /// Picks a ripe plant: <see cref="IGarden.TryHarvest"/> grants it; the plant hops and flies into the basket, the
        /// effect takes its colour, the products count up, the level-up banner when the garden grows a level.
        /// </summary>
        private void HarvestPlot(int plot, int chainIndex, bool vibrate)
        {
            if (!_garden.TryHarvest(plot, out var harvest))
                return;
            LastHarvest = harvest;
            Harvests++;
            if (_context?.Audio != null)
                GardenAudio.Harvest(_context.Audio, chainIndex);
            if (vibrate)
                Haptic(HapticEventId.TapLight);
            PlayHarvest(plot, harvest.Plant);
            if (harvest.MainCount > 0)
                _view.AddProduct(harvest.Main, harvest.MainCount);
            if (harvest.SideCount > 0)
                _view.AddProduct(harvest.Side, harvest.SideCount);
            if (harvest.Dyes > 0 && harvest.Dye != ContentId.None)
                _view.AddProduct(harvest.Dye, harvest.Dyes);
            if (harvest.SeedsBack > 0)
                _view.AddProduct(harvest.Plant, harvest.SeedsBack);
            if (harvest.Happy)
                _view.ShowNote(_text.Get(GardenText.Happy));
            if (harvest.LevelAfter > harvest.LevelBefore)
                LevelUp(harvest.LevelAfter);
            if (_selected == plot)
                _selected = -1;
            _statusDirty = true;
            _dirty = true;
            RefreshPlots(false);
        }

        /// <summary>The soft banner « Jardin niveau {n} » with the plants it opened (and the dyes at level 4). Allocates: once per level.</summary>
        private void LevelUp(int level)
        {
            string list = null;
            int count = _garden.PlantCount;
            for (int i = 0; i < count; i++)
            {
                var plant = _garden.PlantAt(i);
                if (plant.Level != level)
                    continue;
                string name = Text(GardenText.PlantName(plant.Key));
                list = list == null ? name : list + ", " + name;
            }
            if (level == 4)
            {
                string dyes = _text.Get(GardenText.OpenedDyes);
                list = list == null ? dyes : list + ", " + dyes;
            }
            string opened = list != null ? _text.Get(GardenText.Opened).Replace("{list}", list) : null;
            _view.ShowLevelUp(level, opened);
            if (_context?.Audio != null)
                GardenAudio.LevelUp(_context.Audio);
            Haptic(HapticEventId.Confirm);
            _levelShown = -1;
        }

        // ---------------- the challenges ----------------

        private void RebuildChallenges()
        {
            if (_view == null)
                return;
            _challengeCount = 0;
            int count = Mathf.Min(_garden.ChallengeCount, GardenScreenLayout.ChallengeCapacity);
            for (int i = 0; i < count; i++)
                _challengeRow[_challengeCount++] = i;
            int active = _garden.ActiveChallenge;
            for (int row = 0; row < GardenScreenLayout.ChallengeCapacity; row++)
            {
                var model = new GardenChallengeModel();
                if (row < _challengeCount)
                {
                    int challenge = _challengeRow[row];
                    model.Shown = true;
                    model.Index = challenge;
                    model.Name = Text(GardenText.ChallengeName(challenge));
                    model.Goal = Text(GardenText.ChallengeGoal(challenge));
                    model.Stars = _garden.ChallengeStars(challenge);
                    model.Open = _garden.IsChallengeOpen(challenge);
                    model.Active = active == challenge;
                    model.Harvests = model.Active ? _garden.ChallengeHarvests : 0;
                    model.Needed = _garden.ChallengeHarvestsNeeded(challenge);
                }
                _view.SetChallenge(row, in model);
            }
            _harvestsShown = active >= 0 ? _garden.ChallengeHarvests : -1;
        }

        public void TapChallenge(int row)
        {
            if (_phase != Phase.Browsing || (uint)row >= (uint)_challengeCount)
                return;
            int challenge = _challengeRow[row];
            if (_garden.ActiveChallenge == challenge)
            {
                _garden.StopChallenge();
                _view.ShowNote(_text.Get(GardenText.ChallengeStopped));
                Sound(AudioEventId.ToggleOff);
                RebuildChallenges();
                return;
            }
            if (!_garden.IsChallengeOpen(challenge) || !_garden.StartChallenge(challenge))
            {
                Refuse(_text.Get(GardenText.ChallengeLocked));
                return;
            }
            Sound(AudioEventId.UiConfirm);
            Haptic(HapticEventId.TapLight);
            _view.ShowNote(_text.Get(GardenText.ChallengeStarted));
            RebuildChallenges();
        }

        // ---------------- the first visit (doc 01 day 3: 5 Sweet Clover seeds) ----------------

        /// <summary>
        /// The paw on a bare bed, then on the Sweet Clover packet, then on the can, then on the sprout. Only while the garden was
        /// never harvested (level 1, no point); updated when something changes (never per frame).
        /// </summary>
        private void UpdateTutorial()
        {
            if (_view == null || _phase != Phase.Browsing)
                return;
            int step = 0;
            Vector2 point = default;
            if (_garden.Level <= 1 && _garden.PointsInLevel == 0 && _garden.RipeCount == 0)
            {
                int sprout = FirstToWater();
                bool growing = _garden.SoonestRipe >= 0.0;
                if (!growing && _garden.SeedsOf(SweetClover) > 0)
                {
                    if (!_view.SeedsShown)
                    {
                        int bed = FirstPlot(GardenPlotState.Empty);
                        if (bed >= 0)
                        {
                            step = 1;
                            point = BedScreenPoint(bed);
                        }
                    }
                    else
                    {
                        int cell = PacketOf(SweetClover);
                        if (cell >= 0)
                        {
                            step = 2;
                            point = _view.PacketPoint(cell);
                        }
                    }
                }
                else if (sprout >= 0 && !_view.SeedsShown)
                {
                    step = _canArmed ? 4 : 3;
                    point = _canArmed ? BedScreenPoint(sprout) : _view.CanPoint;
                }
            }
            _tutorial = step;
            _view.SetPaw(step > 0, point);
        }

        private int FirstPlot(GardenPlotState state)
        {
            int plots = Plots;
            for (int plot = 0; plot < plots; plot++)
                if (_garden.StateOf(plot) == state)
                    return plot;
            return -1;
        }

        private int PacketOf(ContentId plant)
        {
            for (int cell = 0; cell < _packetCount; cell++)
                if (_packets[cell] == plant)
                    return cell;
            return -1;
        }

        // ---------------- helpers ----------------

        private void NotEnoughGems()
        {
            Refuse(_text.Get(GardenText.NeedGems));
            _activities?.RefugeHud?.PulseNotEnough();
        }

        private void Refuse(string note)
        {
            Sound(AudioEventId.ErrorSoft);
            Haptic(HapticEventId.ErrorSoft);
            if (note != null)
                _view?.ShowNote(note);
        }

        private bool ReduceMotion => _context != null && _context.Save != null && _context.State.Settings.ReduceMotion;

        /// <summary>The save's state (null in captures and headless tests without a save).</summary>
        private PlayerState State => _context != null && _context.Save != null ? _context.State : null;

        private void Sound(AudioEventId id)
        {
            if (_context != null && _context.Audio != null)
                _context.Audio.Play(id, Vector3.zero, default);
        }

        private void Haptic(HapticEventId id)
        {
            if (_context != null && _context.Haptics != null)
                _context.Haptics.Play(id);
        }

        private string Text(string key) => key != null ? _text.Get(key) : string.Empty;

        private string Counted(string key, long n) => _text.GetPlural(key, n).Replace("{n}", n.ToString());

        private static void DestroyObject(Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        public void Dispose()
        {
            _garden.PlotChanged -= _onPlotChanged;
            if (_phase != Phase.Closed)
                Close(false);
            Release();
            DisposeStage();
        }

        /// <summary>Headless scenes without localization: keys stand for texts.</summary>
        private sealed class KeysOnly : ILocalization
        {
            public static readonly KeysOnly Instance = new KeysOnly();

            public string Language => "en";

            public string Get(string key) => key;
        }
    }
}
