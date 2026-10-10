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
    /// T42 « Expéditions », the screen (doc 01 Contenu section 7, S-ACT-005, A18 and A19; doc 05 sheet 23). The first meta
    /// activity: the hub's Expeditions card opens PF_UI_Expeditions (Addressables group Activity_Expeditions, address
    /// activity.expeditions.screen: loaded when opened, released when closed) over a hidden refuge. The map's destination
    /// cards, the 4 slots (empty, away with « De retour dans {time} » and « Finir maintenant », back glowing with « Voir la
    /// carte postale », locked for 800 Gems or the Wild Club), the « Défis » row, « Tout réclamer », and the planning sheet
    /// (destination, the free animals with their portraits, the live team rating and its reasons, « Équipe conseillée », the
    /// durations with the loot preview, « Partir »). The welcome: <see cref="IExpeditions.TryCollect"/> grants the loot, the
    /// screen steps aside so the world shows, <see cref="IExpeditionWorld.TeamBack"/> walks the team in (about 1.2 s), the
    /// postcard (<see cref="IPostcardRenderer"/>) pivots in over a dimmed world (400 ms, a soft ring and 6 round petals,
    /// the paper slide), then the loot turns over in PF_UI_ActivityResult with « Repartir » and « Retour ». Out of the
    /// screen: the hub card's line (« Nouveau », the soonest timer, « {n} de retour »), the Play button's dot and the toast
    /// « Expédition de retour ». Ticked by the activity presenter; nothing allocates per frame while the screen is open
    /// (models are rebuilt when a slot changes, timers write their labels only when the shown text changes).
    /// </summary>
    public sealed class ExpeditionScreenPresenter : IExpeditionScreenActions, IActivityResultActions, IDisposable
    {
        /// <summary>Slot states, the back count and the hub line are checked twice a second (never per frame).</summary>
        private const float CheckInterval = 0.5f;

        /// <summary>Doc 05 sheet 23: the team is in by 1 200 ms.</summary>
        private const float MaxWalkSeconds = 1.2f;

        /// <summary>The Gems slot's first tap arms it this long.</summary>
        private const float ConfirmSeconds = 3f;

        private enum Phase : byte
        {
            Closed = 0,
            Loading = 1,
            Browsing = 2,

            /// <summary>The screen steps aside, the team walks in (doc 05 sheet 23, 0 to 1 200 ms).</summary>
            WelcomeWalk = 3,

            /// <summary>The postcards pivot in (400 ms, one per expedition 300 ms apart).</summary>
            WelcomeCards = 4,

            /// <summary>The loot in the result card over the postcards.</summary>
            WelcomeLoot = 5,
        }

        private readonly GameContext _context;
        private readonly IExpeditions _expeditions;
        private readonly ActivityPresenter _activities;
        private IPostcardRenderer _postcards;
        private bool _pruned;
        private readonly IExpeditionWorld _world;
        private readonly ILocalization _text;
        private readonly GameObject _prefab;
        private readonly ExpeditionTimeText _time = new ExpeditionTimeText();
        private readonly Action<int> _onSlotChanged;

        private Phase _phase;
        private float _clock;
        private float _nextCheck;

        // Loading.
        private AsyncOperationHandle<GameObject> _handle;
        private bool _ownsHandle;
        private GameObject _screenObject;
        private IExpeditionScreenView _view;
        private bool _failed;

        // What the screen shows.
        private readonly ExpeditionSlotState[] _slotState = new ExpeditionSlotState[ExpeditionLayout.SlotCapacity];
        private readonly bool[] _slotKnown = new bool[ExpeditionLayout.SlotCapacity];
        private readonly ContentId[] _cards = new ContentId[ExpeditionLayout.DestinationCapacity];
        private readonly int[] _challengeRow = new int[ExpeditionLayout.ChallengeCapacity];
        private int _cardCount;
        private int _challengeCount;
        private bool _dirty;
        private long _gemsShown = -1;
        private bool _claimAllShown;
        private float _confirmUntil = -1f;

        // The planning sheet.
        private bool _sheet;
        private int _sheetSlot = -1;
        private ContentId _destination;
        private ExpeditionDuration _duration = ExpeditionDuration.OneHour;
        private int _challenge = -1;
        private readonly uint[] _team = new uint[ExpeditionLayout.MaxTeam];
        private int _teamCount;
        private readonly uint[] _animals = new uint[ExpeditionLayout.AnimalCapacity];
        private int _animalCount;
        private readonly uint[] _suggested = new uint[ExpeditionLayout.MaxTeam];
        private readonly string[] _durationLabels = new string[ExpeditionLayout.DurationCount];
        private readonly double[] _durationSeconds = new double[ExpeditionLayout.DurationCount];

        // The welcome.
        private readonly ExpeditionReturn[] _welcomes = new ExpeditionReturn[ExpeditionLayout.SlotCapacity];
        private readonly Texture2D[] _cardPictures = new Texture2D[ExpeditionLayout.PostcardCapacity];
        private readonly uint[] _scratch = new uint[ExpeditionLayout.MaxTeam];
        private readonly RewardCell[] _cells = new RewardCell[RewardCells.Max];
        private int _welcomeCount;
        private float _walkEnds;
        private float _resultAt;
        private int _renderNext;
        private int _renderWaiting = -1;
        private bool _resultOpen;

        // The tutorial (A18: the destination, an animal, « Partir »; A19: the slot back).
        private int _tutorial;

        // Out of the screen: the hub card's line and the toast.
        private int _backSeen = -1;
        private long _statusKey = long.MinValue;
        private string _status;
        private bool _statusDirty = true;

        // Agent EA's sounds: J-SFX-ACT-005-01 to 03 and J-SFX-FEEL-016 (the loot's pops are ActivityPresenter's result beats).
        private const AudioEventId _departSound = AudioEventId.ExpeditionDepart;
        private const AudioEventId _bellSound = AudioEventId.ExpeditionBack;
        private const AudioEventId _postcardSound = AudioEventId.ExpeditionPostcard;
        private const AudioEventId _returnSound = AudioEventId.ExpeditionReturn;

        /// <summary>
        /// The shipped wiring (called by the activity presenter): the screen over the context's expeditions, the refuge of
        /// the activity host as its world, EP's postcard renderer made at the first opening. Null without expeditions.
        /// </summary>
        public static ExpeditionScreenPresenter Create(GameContext context, ActivityPresenter activities)
        {
            var expeditions = context?.Expeditions;
            if (expeditions == null || activities == null)
                return null;
            var root = activities.Host?.Root;
            return new ExpeditionScreenPresenter(context, expeditions, activities, null)
            {
                PostcardFactory = () => new PostcardRenderer(context, expeditions, root),
            };
        }

        /// <summary>Makes the postcard renderer at the first opening (when none was given).</summary>
        public Func<IPostcardRenderer> PostcardFactory { get; set; }

        /// <summary>The shipped screen: its prefab comes from Addressables. The presenter registers itself with the hub.</summary>
        public ExpeditionScreenPresenter(GameContext context, IExpeditions expeditions, ActivityPresenter activities, IPostcardRenderer postcards)
            : this(context, expeditions, activities, postcards, null, null)
        {
        }

        /// <summary>Tests and captures: a given world (else the refuge) and a given screen prefab (else the Addressables one).</summary>
        public ExpeditionScreenPresenter(GameContext context, IExpeditions expeditions, ActivityPresenter activities, IPostcardRenderer postcards,
            IExpeditionWorld world, GameObject screenPrefab)
        {
            _context = context;
            _expeditions = expeditions ?? throw new ArgumentNullException(nameof(expeditions));
            _activities = activities;
            _postcards = postcards;
            _world = world ?? activities?.Host?.Refuge;
            _text = context?.Localization ?? activities?.Host?.Text ?? KeysOnly.Instance;
            _prefab = screenPrefab;
            _time.Bind(_text);
            _onSlotChanged = OnSlotChanged;
            _expeditions.SlotChanged += _onSlotChanged;
            for (int i = 0; i < _challengeRow.Length; i++)
                _challengeRow[i] = -1;
            if (activities != null)
                activities.Expeditions = this;
        }

        /// <summary>The screen is loading or shown (world touches and hatching wait).</summary>
        public bool IsOpen => _phase != Phase.Closed;

        public bool Loading => _phase == Phase.Loading;

        /// <summary>The welcome plays (the world shows, the postcard, the loot).</summary>
        public bool Welcoming => _phase >= Phase.WelcomeWalk;

        public bool SheetOpen => _sheet;

        public IExpeditionScreenView View => _view;

        public GameObject ScreenObject => _screenObject;

        /// <summary>The screen's Addressables handle is held (released when the screen closes).</summary>
        public bool ScreenLoaded => _handle.IsValid();

        public IExpeditions Expeditions => _expeditions;

        /// <summary>The planning sheet's team (tests).</summary>
        public int TeamCount => _teamCount;

        public uint TeamAt(int index) => (uint)index < (uint)_teamCount ? _team[index] : 0u;

        public ContentId SheetDestination => _destination;

        public int SheetSlot => _sheetSlot;

        public ExpeditionDuration SheetDuration => _duration;

        /// <summary>The free animals the picker shows (tests: their order).</summary>
        public int AnimalCount => _animalCount;

        public uint AnimalAt(int cell) => (uint)cell < (uint)_animalCount ? _animals[cell] : 0u;

        public int DestinationCardCount => _cardCount;

        public ContentId DestinationCardAt(int card) => (uint)card < (uint)_cardCount ? _cards[card] : ContentId.None;

        /// <summary>The welcomes of the last collect (tests: the loot and the postcard asked for).</summary>
        public int WelcomeCount => _welcomeCount;

        public ExpeditionReturn WelcomeAt(int index) => _welcomes[Mathf.Clamp(index, 0, _welcomes.Length - 1)];

        public bool ResultOpen => _resultOpen;

        /// <summary>A18 step: 1 the destination, 2 an animal, 3 « Partir »; 4 A19 the slot back; 0 none.</summary>
        public int TutorialStep => _tutorial;

        // ---------------- the hub (doc 01 A18) ----------------

        /// <summary>The hub's Play on the Expeditions card: the screen opens over a hidden refuge.</summary>
        public void OpenFromHub()
        {
            if (IsOpen || !_expeditions.IsOpen)
                return;
            _activities?.CloseHubQuietly();
            Sound(AudioEventId.PanelOpen);
            Haptic(HapticEventId.TapLight);
            Open();
        }

        /// <summary>
        /// The hub card's state and line: « Nouveau » before the first expedition, « {n} de retour » with teams back, else the
        /// soonest return (minutes); null when nothing is away. The string is built only when it changes.
        /// </summary>
        public ActivityCardState CardStatus(out string status)
        {
            UpdateStatus();
            status = _status;
            return _expeditions.FirstStrollNext && _expeditions.BackCount == 0 && _expeditions.SoonestReturn < 0.0
                ? ActivityCardState.New
                : ActivityCardState.Open;
        }

        /// <summary>The hub line changed since the last <see cref="CardStatus"/> (checked twice a second by the hub).</summary>
        public bool StatusChanged
        {
            get
            {
                long key = StatusKey();
                return _statusDirty || key != _statusKey;
            }
        }

        /// <summary>The Play button's dot: a team back, or the expeditions open and never tried (A18).</summary>
        public bool WantsDot => _expeditions.IsOpen && (_expeditions.BackCount > 0 || _expeditions.FirstStrollNext && _expeditions.SoonestReturn < 0.0);

        private long StatusKey()
        {
            int back = _expeditions.BackCount;
            if (back > 0)
                return -1000L - back;
            double soonest = _expeditions.SoonestReturn;
            if (soonest < 0.0)
                return -1L;
            // Minutes on the hub (it refreshes twice a second at most; seconds stay on the screen).
            return (long)Math.Ceiling(Math.Max(1.0, soonest) / 60.0);
        }

        private void UpdateStatus()
        {
            long key = StatusKey();
            if (!_statusDirty && key == _statusKey)
                return;
            _statusDirty = false;
            _statusKey = key;
            int back = _expeditions.BackCount;
            if (back > 0)
                _status = _text.GetPlural(ExpeditionText.BackCount, back).Replace("{n}", back.ToString());
            else if (key > 0)
                _status = _time.BackInString(Math.Max(60.0, key * 60.0));
            else
                _status = null;
        }

        // ---------------- opening and closing ----------------

        private void Open()
        {
            _phase = Phase.Loading;
            _dirty = true;
            if (_postcards == null && PostcardFactory != null)
                _postcards = PostcardFactory();
            if (!_pruned && _postcards is PostcardRenderer)
            {
                // The postcards no longer kept (the first of each destination and the most recent stay) leave the disk.
                _pruned = true;
                try
                {
                    new PostcardStore().Prune(_expeditions);
                }
                catch (Exception e)
                {
                    GameLog.Warning(LogCategory.Presentation, "Expeditions: postcards not pruned (" + e.Message + ").");
                }
            }
            if (_view == null && !_failed)
            {
                if (_prefab != null)
                    BuildScreen(_prefab);
                else
                    StartLoading();
            }
            Advance();
        }

        private void StartLoading()
        {
            if (_handle.IsValid())
                return;
            if (!AddressExists(ExpeditionLayout.ScreenAddress))
            {
                _failed = true;
                GameLog.Error(LogCategory.Presentation,
                    $"Expeditions: no Addressables entry {ExpeditionLayout.ScreenAddress} (run Pocket Wilds/Activities/Build Expeditions (T42)).");
                return;
            }
            _handle = Addressables.LoadAssetAsync<GameObject>(ExpeditionLayout.ScreenAddress);
            _ownsHandle = true;
        }

        private static bool AddressExists(string address)
        {
            foreach (var locator in Addressables.ResourceLocators)
                if (locator.Locate(address, typeof(GameObject), out _))
                    return true;
            return false;
        }

        /// <summary>Moves the loading on; once the view is there the screen shows.</summary>
        private void Advance()
        {
            if (_phase != Phase.Loading)
                return;
            if (_view == null && !_failed)
            {
                if (!_handle.IsValid() || !_handle.IsDone)
                    return;
                if (_handle.Status != AsyncOperationStatus.Succeeded || _handle.Result == null)
                {
                    _failed = true;
                    GameLog.Error(LogCategory.Presentation, "Expeditions: the screen did not load (" + ExpeditionLayout.ScreenAddress + ").");
                }
                else
                {
                    BuildScreen(_handle.Result);
                }
            }
            if (_view == null)
            {
                // A screen that cannot load closes again at once (the hub comes back).
                _phase = Phase.Closed;
                Release();
                _activities?.ReturnFromMeta();
                return;
            }
            ShowScreen();
        }

        private void BuildScreen(GameObject prefab)
        {
            var parent = _activities?.Host?.Root;
            _screenObject = Object.Instantiate(prefab, parent);
            _screenObject.name = ExpeditionLayout.ScreenPrefabName;
            _view = _screenObject.GetComponent<IExpeditionScreenView>();
            if (_view == null || !_view.Valid)
            {
                GameLog.Error(LogCategory.Presentation, "Expeditions: the screen prefab has no complete ExpeditionScreenView (rebuild it with ExpeditionPipeline).");
                DestroyObject(_screenObject);
                _screenObject = null;
                _view = null;
                _failed = true;
                return;
            }
            _view.Bind(_text, this);
            _view.Hide();
        }

        private void ShowScreen()
        {
            _phase = Phase.Browsing;
            _view.ReduceMotion = ReduceMotion;
            for (int i = 0; i < _slotKnown.Length; i++)
                _slotKnown[i] = false;
            _gemsShown = -1;
            _claimAllShown = false;
            _view.SetClaimAll(false);
            _view.HidePostcards();
            _view.SetDim(false);
            _view.SetContentShown(true);
            _view.ShowSheet(false);
            _sheet = false;
            RebuildDestinations();
            RebuildChallenges();
            RefreshSlots(true);
            _view.Show();
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

        /// <summary>The screen goes, its bundle is released, the refuge and its HUD come back (and the hub with <paramref name="toHub"/>).</summary>
        private void Close(bool toHub)
        {
            if (_phase == Phase.Closed)
                return;
            if (_resultOpen)
                CloseResult();
            _phase = Phase.Closed;
            _sheet = false;
            _tutorial = 0;
            _view?.SetPaw(false, default);
            _view?.Hide();
            _activities?.Host?.SetWorldVisible?.Invoke(true);
            _activities?.PlayHud?.SetPlaying(false);
            Release();
            _statusDirty = true;
            if (toHub)
                _activities?.ReturnFromMeta();
        }

        /// <summary>Lets go of the screen, its bundle, the postcards' pictures and the renderer's set.</summary>
        private void Release()
        {
            _postcards?.Release();
            for (int i = 0; i < _cardPictures.Length; i++)
            {
                if (_cardPictures[i] != null)
                    DestroyObject(_cardPictures[i]);
                _cardPictures[i] = null;
            }
            _view = null;
            if (_screenObject != null)
                DestroyObject(_screenObject);
            _screenObject = null;
            if (_ownsHandle && _handle.IsValid())
                Addressables.Release(_handle);
            _handle = default;
            _ownsHandle = false;
            _failed = false;
        }

        // ---------------- Tick ----------------

        /// <summary>Per frame from the activity presenter (unscaled time). No allocation while the screen is open.</summary>
        public void Tick(float deltaTime)
        {
            _clock += deltaTime;
            switch (_phase)
            {
                case Phase.Loading:
                    Advance();
                    break;
                case Phase.Browsing:
                    TickBrowsing();
                    break;
                case Phase.WelcomeWalk:
                    TickRendering();
                    if (_clock >= _walkEnds)
                        ShowPostcards();
                    break;
                case Phase.WelcomeCards:
                    TickRendering();
                    if (_clock >= _resultAt)
                        OpenResult();
                    break;
                case Phase.WelcomeLoot:
                    TickRendering();
                    break;
            }
            if (_view != null && _phase != Phase.Closed)
                _view.Tick(deltaTime);
            if (_clock >= _nextCheck)
            {
                _nextCheck = _clock + CheckInterval;
                CheckBack();
            }
        }

        private void TickBrowsing()
        {
            if (_postcards != null)
                _postcards.Tick();
            if (_confirmUntil >= 0f && _clock > _confirmUntil)
                _confirmUntil = -1f;
            int slots = Mathf.Min(_expeditions.SlotCount, ExpeditionLayout.SlotCapacity);
            for (int slot = 0; slot < slots; slot++)
            {
                if (_slotKnown[slot] && _expeditions.StateOf(slot) == ExpeditionSlotState.Away)
                    _view.SetSlotTime(slot, _expeditions.SecondsLeft(slot), _expeditions.TotalSecondsOf(slot));
            }
            if (_dirty || _clock >= _nextCheck)
                RefreshSlots(false);
        }

        /// <summary>A team came back: the toast in the refuge (doc 01 A19), the bell on the screen; the hub line changes.</summary>
        private void CheckBack()
        {
            int back = _expeditions.IsOpen ? _expeditions.BackCount : 0;
            if (_backSeen < 0)
            {
                _backSeen = back;
                return;
            }
            if (back > _backSeen)
            {
                _statusDirty = true;
                // On the screen the slot rings its own bell when it turns back (RefreshSlots).
                if (_phase == Phase.Closed && (_activities == null || !_activities.InGame))
                {
                    _activities?.RefugeHud?.ShowToast(ToastKind.Expedition, _text.Get(ExpeditionText.Back));
                    Sound(_bellSound);
                }
            }
            _backSeen = back;
        }

        private void OnSlotChanged(int slot)
        {
            _dirty = true;
            _statusDirty = true;
        }

        // ---------------- the slots ----------------

        private void RefreshSlots(bool force)
        {
            if (_view == null)
                return;
            _dirty = false;
            int slots = Mathf.Min(_expeditions.SlotCount, ExpeditionLayout.SlotCapacity);
            bool changed = false;
            for (int slot = 0; slot < ExpeditionLayout.SlotCapacity; slot++)
            {
                if (slot >= slots)
                {
                    if (force || !_slotKnown[slot])
                    {
                        var hidden = new ExpeditionSlotModel { State = ExpeditionSlotState.Locked, Unlock = ExpeditionSlotUnlock.None };
                        _view.SetSlot(slot, in hidden);
                        _slotKnown[slot] = true;
                        _slotState[slot] = ExpeditionSlotState.Locked;
                    }
                    continue;
                }
                var state = _expeditions.StateOf(slot);
                if (!force && _slotKnown[slot] && state == _slotState[slot])
                    continue;
                if (_slotKnown[slot] && _slotState[slot] == ExpeditionSlotState.Away && state == ExpeditionSlotState.Back)
                    Sound(_bellSound);
                _slotKnown[slot] = true;
                _slotState[slot] = state;
                changed = true;
                var model = SlotModel(slot, state);
                _view.SetSlot(slot, in model);
                if (state == ExpeditionSlotState.Away)
                    _view.SetSlotTime(slot, _expeditions.SecondsLeft(slot), _expeditions.TotalSecondsOf(slot));
            }
            long gems = State?.Wallet.Gems ?? 0L;
            if (gems != _gemsShown)
            {
                _gemsShown = gems;
                _view.SetGems(gems);
                changed = true;
            }
            bool claimAll = _expeditions.BackCount >= 2;
            if (claimAll != _claimAllShown)
            {
                _claimAllShown = claimAll;
                _view.SetClaimAll(claimAll);
            }
            if (changed)
                UpdateTutorial();
        }

        /// <summary>A slot's model (when its state changes: its destination's name is read then, never per frame).</summary>
        private ExpeditionSlotModel SlotModel(int slot, ExpeditionSlotState state)
        {
            var model = new ExpeditionSlotModel
            {
                State = state,
                Unlock = _expeditions.UnlockOf(slot),
                Price = _expeditions.SlotPrice,
            };
            if (state != ExpeditionSlotState.Away && state != ExpeditionSlotState.Back)
                return model;
            model.Destination = DestinationName(_expeditions.DestinationOf(slot));
            model.Stars = _expeditions.StarsOf(slot);
            model.SpeedUpCost = state == ExpeditionSlotState.Away ? _expeditions.SpeedUpCost(slot) : 0;
            model.SpeedUpsLeft = _expeditions.SpeedUpsLeftToday;
            int size = Mathf.Min(_expeditions.TeamSizeOf(slot), ExpeditionLayout.MaxTeam);
            model.TeamSize = size;
            var collection = Collection;
            for (int k = 0; k < size; k++)
            {
                uint id = _expeditions.TeamMember(slot, k);
                uint variant = 0, species = 0;
                if (collection != null && collection.TryGet(id, out var animal))
                {
                    variant = animal.Variant.Value;
                    species = animal.Species.Value;
                }
                if (k == 0)
                {
                    model.Variant0 = variant;
                    model.Species0 = species;
                }
                else if (k == 1)
                {
                    model.Variant1 = variant;
                    model.Species1 = species;
                }
                else
                {
                    model.Variant2 = variant;
                    model.Species2 = species;
                }
            }
            return model;
        }

        public void TapSlot(int slot)
        {
            if (_phase != Phase.Browsing || (uint)slot >= (uint)Mathf.Min(_expeditions.SlotCount, ExpeditionLayout.SlotCapacity))
                return;
            switch (_expeditions.StateOf(slot))
            {
                case ExpeditionSlotState.Empty:
                    OpenSheet(slot, _destination, -1);
                    break;
                case ExpeditionSlotState.Back:
                    BeginWelcome(slot);
                    break;
                case ExpeditionSlotState.Locked:
                    TapLocked(slot);
                    break;
            }
        }

        private void TapLocked(int slot)
        {
            var unlock = _expeditions.UnlockOf(slot);
            if (unlock == ExpeditionSlotUnlock.Club)
            {
                _view.ShowNote(_text.Get(ExpeditionText.ClubSoon));
                Sound(AudioEventId.UiTab);
                return;
            }
            if (unlock != ExpeditionSlotUnlock.Gems)
                return;
            // The first tap arms the purchase; the second, within 3 s, spends the Gems.
            if (_confirmUntil < 0f)
            {
                _confirmUntil = _clock + ConfirmSeconds;
                _view.ShowNote(_text.Get(ExpeditionText.SlotConfirm));
                Sound(AudioEventId.UiTab);
                return;
            }
            _confirmUntil = -1f;
            if (!_expeditions.TryBuySlot())
            {
                NotEnoughGems();
                return;
            }
            Sound(AudioEventId.PurchaseComplete);
            Haptic(HapticEventId.PurchaseSuccess);
            RefreshSlots(true);
        }

        public void SpeedUp(int slot)
        {
            if (_phase != Phase.Browsing || (uint)slot >= (uint)_expeditions.SlotCount || _expeditions.StateOf(slot) != ExpeditionSlotState.Away)
                return;
            if (_expeditions.SpeedUpsLeftToday <= 0)
            {
                Refuse(_text.Get(ExpeditionText.SpeedUpsNone));
                return;
            }
            if (!_expeditions.TrySpeedUp(slot))
            {
                NotEnoughGems();
                return;
            }
            Sound(AudioEventId.PurchaseComplete);
            Haptic(HapticEventId.PurchaseSuccess);
            RefreshSlots(true);
        }

        private void NotEnoughGems()
        {
            Refuse(_text.Get(ExpeditionText.NeedGems));
            _activities?.RefugeHud?.PulseNotEnough();
        }

        private void Refuse(string note)
        {
            Sound(AudioEventId.ErrorSoft);
            Haptic(HapticEventId.ErrorSoft);
            if (note != null)
                _view?.ShowNote(note);
        }

        // ---------------- the map and the challenges ----------------

        /// <summary>The map's cards: the open destinations first (biome ones, then events running), then the next biomes locked.</summary>
        private void RebuildDestinations()
        {
            _cardCount = 0;
            int count = _expeditions.DestinationCount;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < count && _cardCount < _cards.Length; i++)
                {
                    var destination = _expeditions.DestinationAt(i);
                    bool open = _expeditions.IsDestinationOpen(destination.Id);
                    if (open != (pass == 0))
                        continue;
                    // An event destination shows only while its event runs.
                    if (!open && destination.Event)
                        continue;
                    _cards[_cardCount++] = destination.Id;
                }
            }
            if (_destination == ContentId.None || !_expeditions.IsDestinationOpen(_destination))
                _destination = _cardCount > 0 && _expeditions.IsDestinationOpen(_cards[0]) ? _cards[0] : ContentId.None;
            BindDestinations();
        }

        private void BindDestinations()
        {
            for (int card = 0; card < ExpeditionLayout.DestinationCapacity; card++)
            {
                var model = new ExpeditionDestinationModel();
                if (card < _cardCount && _expeditions.TryGetDestination(_cards[card], out var destination))
                {
                    model.Shown = true;
                    model.Key = destination.Key;
                    model.Name = DestinationName(destination.Id);
                    model.Open = _expeditions.IsDestinationOpen(destination.Id);
                    model.Selected = _sheet && destination.Id == _destination;
                }
                _view.SetDestination(card, in model);
            }
        }

        private void RebuildChallenges()
        {
            _challengeCount = 0;
            int count = Mathf.Min(_expeditions.ChallengeCount, ExpeditionLayout.ChallengeCapacity);
            for (int i = 0; i < count; i++)
                if (_expeditions.IsChallengeOpen(i))
                    _challengeRow[_challengeCount++] = i;
            for (int row = 0; row < ExpeditionLayout.ChallengeCapacity; row++)
            {
                var model = new ExpeditionChallengeModel();
                if (row < _challengeCount)
                {
                    int challenge = _challengeRow[row];
                    model.Shown = true;
                    model.Index = challenge;
                    model.Name = Text(ExpeditionText.ChallengeName(challenge));
                    model.Goal = Text(ExpeditionText.ChallengeGoal(challenge));
                    model.Stars = _expeditions.ChallengeStars(challenge);
                    model.Selected = _sheet && _challenge == challenge;
                }
                _view.SetChallenge(row, in model);
            }
        }

        public void TapDestination(int card)
        {
            if (_phase != Phase.Browsing || (uint)card >= (uint)_cardCount)
                return;
            var id = _cards[card];
            if (!_expeditions.IsDestinationOpen(id))
            {
                Refuse(_text.Get(ExpeditionText.DestinationLocked));
                return;
            }
            if (_challenge >= 0 && _sheet && !_expeditions.ChallengeAllows(_challenge, id))
            {
                Refuse(Text(ExpeditionText.ChallengeGoal(_challenge)));
                return;
            }
            Sound(AudioEventId.UiTab);
            if (_sheet)
            {
                SetDestination(id);
                return;
            }
            int slot = FirstEmptySlot();
            if (slot < 0)
            {
                Refuse(_text.Get(ExpeditionText.NoSlot));
                return;
            }
            _destination = id;
            OpenSheet(slot, id, -1);
        }

        public void TapChallenge(int row)
        {
            if (_phase != Phase.Browsing || (uint)row >= (uint)_challengeCount)
                return;
            int challenge = _challengeRow[row];
            int slot = _sheet ? _sheetSlot : FirstEmptySlot();
            if (slot < 0)
            {
                Refuse(_text.Get(ExpeditionText.NoSlot));
                return;
            }
            var destination = _destination;
            if (destination == ContentId.None || !_expeditions.ChallengeAllows(challenge, destination) || !_expeditions.IsDestinationOpen(destination))
                destination = FirstAllowed(challenge);
            if (destination == ContentId.None)
            {
                Refuse(Text(ExpeditionText.ChallengeGoal(challenge)));
                return;
            }
            Sound(AudioEventId.UiTab);
            OpenSheet(slot, destination, challenge);
        }

        private ContentId FirstAllowed(int challenge)
        {
            for (int card = 0; card < _cardCount; card++)
                if (_expeditions.IsDestinationOpen(_cards[card]) && _expeditions.ChallengeAllows(challenge, _cards[card]))
                    return _cards[card];
            return ContentId.None;
        }

        private int FirstEmptySlot()
        {
            int slots = Mathf.Min(_expeditions.SlotCount, ExpeditionLayout.SlotCapacity);
            for (int slot = 0; slot < slots; slot++)
                if (_expeditions.StateOf(slot) == ExpeditionSlotState.Empty)
                    return slot;
            return -1;
        }

        // ---------------- the planning sheet ----------------

        /// <summary>
        /// Opens the sheet for an empty slot: its destination (the one given, else the last, else the first open), no team
        /// picked (A18: the player picks), the durations of the moment, the loot preview.
        /// </summary>
        public void OpenSheet(int slot, ContentId destination, int challenge, uint[] team = null, int count = 0)
        {
            if (_view == null || _phase != Phase.Browsing)
                return;
            if (destination == ContentId.None || !_expeditions.IsDestinationOpen(destination))
                destination = _destination != ContentId.None && _expeditions.IsDestinationOpen(_destination) ? _destination : FirstOpenDestination();
            if (destination == ContentId.None)
            {
                Refuse(_text.Get(ExpeditionText.DestinationLocked));
                return;
            }
            _sheet = true;
            _sheetSlot = slot;
            _challenge = challenge;
            _destination = destination;
            _teamCount = 0;
            if (team != null)
            {
                for (int i = 0; i < count && i < team.Length && _teamCount < _team.Length; i++)
                    if (team[i] != 0 && !_expeditions.IsAway(team[i]) && Owned(team[i]))
                        _team[_teamCount++] = team[i];
            }
            if (challenge >= 0)
                _duration = _expeditions.ChallengeDuration(challenge);
            else if (_expeditions.FirstStrollNext)
                _duration = ExpeditionDuration.OneHour;
            BuildDurationLabels();
            RebuildAnimals();
            BindDestinations();
            RebuildChallenges();
            BindSheet();
            _view.ShowSheet(true);
            Sound(AudioEventId.PanelOpen);
            UpdateTutorial();
        }

        public void CloseSheet()
        {
            if (!_sheet)
                return;
            _sheet = false;
            _challenge = -1;
            _view?.ShowSheet(false);
            if (_view != null)
            {
                BindDestinations();
                RebuildChallenges();
            }
            Sound(AudioEventId.UiBack);
            UpdateTutorial();
        }

        private ContentId FirstOpenDestination()
        {
            for (int card = 0; card < _cardCount; card++)
                if (_expeditions.IsDestinationOpen(_cards[card]))
                    return _cards[card];
            return ContentId.None;
        }

        private void SetDestination(ContentId destination)
        {
            if (destination == _destination)
                return;
            _destination = destination;
            RebuildAnimals();
            BindDestinations();
            BindSheet();
            UpdateTutorial();
        }

        /// <summary>
        /// The free animals (in no team), the most suited first: the team picked, then those of the destination's biome,
        /// then the favorites, then the others in collection order. Once per sheet opening or destination change.
        /// </summary>
        private void RebuildAnimals()
        {
            _animalCount = 0;
            for (int i = 0; i < _teamCount; i++)
                _animals[_animalCount++] = _team[i];
            var collection = Collection;
            var biome = DestinationBiome();
            if (collection != null)
            {
                for (int pass = 0; pass < 3 && _animalCount < _animals.Length; pass++)
                {
                    for (int i = 0; i < collection.Count && _animalCount < _animals.Length; i++)
                    {
                        ref readonly var animal = ref collection[i];
                        if (_expeditions.IsAway(animal.InstanceId) || Listed(animal.InstanceId))
                            continue;
                        bool fromBiome = biome != ContentId.None && BiomeOf(animal.Species) == biome;
                        int rank = fromBiome ? 0 : animal.IsFavorite ? 1 : 2;
                        if (rank != pass)
                            continue;
                        _animals[_animalCount++] = animal.InstanceId;
                    }
                }
            }
            BindAnimals();
        }

        private bool Listed(uint instance)
        {
            for (int i = 0; i < _animalCount; i++)
                if (_animals[i] == instance)
                    return true;
            return false;
        }

        private void BindAnimals()
        {
            var collection = Collection;
            var biome = DestinationBiome();
            for (int cell = 0; cell < ExpeditionLayout.AnimalCapacity; cell++)
            {
                var model = new ExpeditionAnimalModel();
                if (cell < _animalCount && collection != null && collection.TryGet(_animals[cell], out var animal))
                {
                    model.Shown = true;
                    model.Instance = animal.InstanceId;
                    model.Variant = animal.Variant.Value;
                    model.Species = animal.Species.Value;
                    model.Picked = InTeam(animal.InstanceId);
                    model.FromBiome = biome != ContentId.None && BiomeOf(animal.Species) == biome;
                    model.Favorite = animal.IsFavorite;
                }
                _view.SetAnimal(cell, in model);
            }
        }

        public void TapAnimal(int cell)
        {
            if (!_sheet || (uint)cell >= (uint)_animalCount)
                return;
            uint id = _animals[cell];
            int at = IndexInTeam(id);
            if (at >= 0)
            {
                for (int i = at; i < _teamCount - 1; i++)
                    _team[i] = _team[i + 1];
                _teamCount--;
                Sound(AudioEventId.ToggleOff);
            }
            else
            {
                if (_teamCount >= ExpeditionLayout.MaxTeam)
                {
                    Refuse(_text.Get(ExpeditionText.TeamFull));
                    return;
                }
                if (_expeditions.IsAway(id))
                {
                    Refuse(_text.Get(ExpeditionText.AnimalAway));
                    return;
                }
                _team[_teamCount++] = id;
                Sound(AudioEventId.ToggleOn);
            }
            Haptic(HapticEventId.TapLight);
            BindAnimals();
            BindSheet();
            UpdateTutorial();
        }

        public void SuggestTeam()
        {
            if (!_sheet || _destination == ContentId.None)
                return;
            int count = _expeditions.SuggestTeam(_destination, _suggested);
            if (count <= 0)
            {
                Refuse(_text.Get(ExpeditionText.NoAnimal));
                return;
            }
            _teamCount = 0;
            for (int i = 0; i < count && i < _suggested.Length; i++)
                _team[_teamCount++] = _suggested[i];
            Sound(AudioEventId.UiConfirm);
            Haptic(HapticEventId.TapLight);
            RebuildAnimals();
            BindSheet();
            UpdateTutorial();
        }

        public void PickDuration(int duration)
        {
            if (!_sheet || (uint)duration >= ExpeditionLayout.DurationCount || _challenge >= 0 || _expeditions.FirstStrollNext)
                return;
            var picked = (ExpeditionDuration)duration;
            if (picked == _duration)
                return;
            _duration = picked;
            Sound(AudioEventId.UiTab);
            BindSheet();
        }

        /// <summary>The durations' labels: « 1 h » to « 12 h », or the weekend's shorter times (built when the sheet opens).</summary>
        private void BuildDurationLabels()
        {
            for (int i = 0; i < ExpeditionLayout.DurationCount; i++)
            {
                double seconds = _expeditions.DurationSeconds((ExpeditionDuration)i);
                if (_durationLabels[i] != null && Math.Abs(seconds - _durationSeconds[i]) < 0.5)
                    continue;
                _durationSeconds[i] = seconds;
                _durationLabels[i] = _time.TimeString(seconds);
            }
        }

        /// <summary>Writes the sheet: its destination, rating, durations, loot preview and « Partir ».</summary>
        private void BindSheet()
        {
            if (_view == null)
                return;
            var rating = _teamCount > 0 ? _expeditions.Rate(_destination, _team, _teamCount) : default;
            bool firstStroll = _expeditions.FirstStrollNext && _challenge < 0;
            bool locked = firstStroll || _challenge >= 0;
            bool canGo = _teamCount > 0 && _sheetSlot >= 0 && _expeditions.StateOf(_sheetSlot) == ExpeditionSlotState.Empty
                         && (_challenge < 0 || _expeditions.ChallengeAllows(_challenge, _destination)
                             && _expeditions.ChallengeAccepts(_challenge, _destination, _team, _teamCount));
            string note = null;
            if (_challenge >= 0)
                note = Text(ExpeditionText.ChallengeGoal(_challenge));
            else if (!firstStroll && _duration >= ExpeditionDuration.EightHours && _expeditions.DiscoveryGuaranteeIn > 0)
                note = Counted(ExpeditionText.DiscoveryGuarantee, _expeditions.DiscoveryGuaranteeIn);
            var sheet = new ExpeditionSheetModel
            {
                Title = _text.Get(ExpeditionText.PickTeam),
                Destination = DestinationName(_destination),
                Note = note,
                FirstStroll = firstStroll,
                Duration = (int)_duration,
                DurationLocked = locked,
                CanGo = canGo,
                Challenge = _challenge,
            };
            _view.SetSheet(in sheet);
            var ratingModel = new ExpeditionRatingModel
            {
                TeamSize = _teamCount,
                Points = rating.Points,
                Stars = _teamCount > 0 ? rating.Stars : (byte)0,
                FromBiome = rating.FromBiome,
                FavoriteFamily = rating.FavoriteFamily,
                ThreeSpecies = rating.ThreeSpecies,
            };
            _view.SetRating(in ratingModel);
            for (int i = 0; i < ExpeditionLayout.DurationCount; i++)
            {
                bool shown = !locked || i == (int)_duration;
                string label = firstStroll ? _text.Get(ExpeditionText.FirstStroll) : _durationLabels[i];
                _view.SetDuration(i, label, shown, i == (int)_duration);
            }
            byte stars = _teamCount > 0 ? (byte)Mathf.Max(1, rating.Stars) : (byte)1;
            _expeditions.Preview(_destination, _duration, stars, out var preview);
            _view.SetLoot(in preview);
        }

        public void Go()
        {
            if (!_sheet || _phase != Phase.Browsing)
                return;
            if (_teamCount == 0)
            {
                Refuse(_text.Get(ExpeditionText.NoAnimal));
                return;
            }
            int count = _teamCount;
            for (int i = 0; i < count; i++)
                _scratch[i] = _team[i];
            var result = _challenge >= 0
                ? _expeditions.StartChallenge(_sheetSlot, _challenge, _destination, _scratch, count)
                : _expeditions.Start(_sheetSlot, _destination, _duration, _scratch, count);
            if (result != ExpeditionStartResult.Started)
            {
                Refuse(StartRefusal(result));
                return;
            }
            int slot = _sheetSlot;
            // The team leaves the refuge while the screen covers it.
            _world?.TeamLeft(_scratch, count);
            Sound(_departSound);
            Haptic(HapticEventId.Confirm);
            _sheet = false;
            _challenge = -1;
            _teamCount = 0;
            _view.ShowSheet(false);
            BindDestinations();
            RebuildChallenges();
            _dirty = true;
            _statusDirty = true;
            RefreshSlots(true);
            _view.PlayDeparture(slot);
            UpdateTutorial();
        }

        private string StartRefusal(ExpeditionStartResult result)
        {
            switch (result)
            {
                case ExpeditionStartResult.SlotNotEmpty: return _text.Get(ExpeditionText.NoSlot);
                case ExpeditionStartResult.NoAnimal: return _text.Get(ExpeditionText.NoAnimal);
                case ExpeditionStartResult.TooManyAnimals: return _text.Get(ExpeditionText.TeamFull);
                case ExpeditionStartResult.AnimalUnavailable: return _text.Get(ExpeditionText.AnimalAway);
                case ExpeditionStartResult.DestinationLocked: return _text.Get(ExpeditionText.DestinationLocked);
                default: return _challenge >= 0 ? Text(ExpeditionText.ChallengeGoal(_challenge)) : null;
            }
        }

        // ---------------- the welcome (doc 05 sheet 23) ----------------

        public void ClaimAll()
        {
            if (_phase != Phase.Browsing || _expeditions.BackCount < 2)
                return;
            BeginWelcome(-1);
        }

        /// <summary>
        /// The teams back are welcomed (one slot, or every slot back for « Tout réclamer »): the loot is granted now, the
        /// screen steps aside, the world shows and the teams walk in; the postcards are asked for at once (they render
        /// while the teams walk).
        /// </summary>
        private void BeginWelcome(int onlySlot)
        {
            _welcomeCount = 0;
            int slots = Mathf.Min(_expeditions.SlotCount, ExpeditionLayout.SlotCapacity);
            for (int slot = 0; slot < slots && _welcomeCount < _welcomes.Length; slot++)
            {
                if (onlySlot >= 0 && slot != onlySlot)
                    continue;
                if (_expeditions.StateOf(slot) != ExpeditionSlotState.Back)
                    continue;
                if (_expeditions.TryCollect(slot, out var welcome))
                    _welcomes[_welcomeCount++] = welcome;
            }
            if (_welcomeCount == 0)
                return;
            _sheet = false;
            _view.ShowSheet(false);
            _view.SetPaw(false, default);
            _tutorial = 0;
            _view.HidePostcards();
            _view.SetContentShown(false);
            _activities?.Host?.SetWorldVisible?.Invoke(true);
            Sound(_returnSound);
            float walk = 0f;
            for (int i = 0; i < _welcomeCount; i++)
            {
                ref readonly var w = ref _welcomes[i];
                int n = Mathf.Min(w.TeamSize, _scratch.Length);
                for (int k = 0; k < n; k++)
                    _scratch[k] = w.AnimalAt(k);
                if (_world != null && n > 0)
                    walk = Mathf.Max(walk, _world.TeamBack(_scratch, n));
            }
            _walkEnds = _clock + Mathf.Clamp(walk, 0f, MaxWalkSeconds) + ExpeditionLayout.FadeSeconds;
            _renderNext = 0;
            _renderWaiting = -1;
            _phase = Phase.WelcomeWalk;
            _statusDirty = true;
            _dirty = true;
            TickRendering();
        }

        /// <summary>The postcards pivot in over a dimmed world: one per expedition, 300 ms apart (400 ms each).</summary>
        private void ShowPostcards()
        {
            _phase = Phase.WelcomeCards;
            _view.SetDim(true);
            for (int i = 0; i < _welcomeCount; i++)
                _view.ShowPostcard(i, _welcomeCount, DestinationName(_welcomes[i].Destination), i * ExpeditionLayout.ClaimAllGap);
            Sound(_postcardSound);
            Haptic(HapticEventId.Confirm);
            _resultAt = _clock + ExpeditionLayout.PostcardPivotSeconds + (_welcomeCount - 1) * ExpeditionLayout.ClaimAllGap;
        }

        /// <summary>One postcard at a time: begun when the renderer is free, shown once rendered (a copy each for « Tout réclamer »).</summary>
        private void TickRendering()
        {
            if (_postcards == null)
                return;
            _postcards.Tick();
            if (_renderWaiting >= 0)
            {
                if (_postcards.Busy)
                    return;
                ShowPicture(_renderWaiting, _postcards.Picture);
                _renderWaiting = -1;
            }
            if (_renderNext < _welcomeCount && !_postcards.Busy)
            {
                _postcards.Begin(in _welcomes[_renderNext]);
                _renderWaiting = _renderNext;
                _renderNext++;
            }
        }

        private void ShowPicture(int card, Texture2D picture)
        {
            if (_view == null || picture == null)
                return;
            if (_welcomeCount <= 1 || SystemInfo.copyTextureSupport == UnityEngine.Rendering.CopyTextureSupport.None)
            {
                _view.SetPostcardPicture(card, picture);
                return;
            }
            // The renderer keeps one picture: each card of « Tout réclamer » keeps its own copy (made once, on the GPU).
            var copy = _cardPictures[card];
            if (copy == null || copy.width != picture.width || copy.height != picture.height || copy.format != picture.format)
            {
                if (copy != null)
                    DestroyObject(copy);
                copy = new Texture2D(picture.width, picture.height, picture.format, false) { name = "Postcard" + card };
                _cardPictures[card] = copy;
            }
            Graphics.CopyTexture(picture, copy);
            _view.SetPostcardPicture(card, copy);
        }

        /// <summary>The loot, one item after the other, in PF_UI_ActivityResult with « Repartir » and « Retour ».</summary>
        private void OpenResult()
        {
            _phase = Phase.WelcomeLoot;
            _view.DockPostcards(true);
            int count = BuildCells();
            byte stars = 0;
            for (int i = 0; i < _welcomeCount; i++)
                stars = (byte)Mathf.Max(stars, _welcomes[i].Stars);
            var model = new ActivityResultModel
            {
                Activity = ActivityId.Expeditions,
                Mode = ActivityMode.Free,
                ChallengeIndex = -1,
                Stars = (byte)Mathf.Clamp(stars, 1, 3),
                Rewarded = true,
                CanReplay = true,
                Subtitle = _welcomeCount == 1 ? DestinationName(_welcomes[0].Destination) : _text.Get(ExpeditionText.Title),
            };
            var result = _activities?.ResultView;
            if (result == null)
            {
                // No result card (captures): the postcards stay in front until « Retour » (BackToHub).
                _view.DockPostcards(false);
                return;
            }
            result.Open(in model, _cells, count, _text, this);
            _resultOpen = true;
        }

        /// <summary>
        /// The loot of the welcomed teams in one row (doc 01 section 7.3; doc 05 sheet 23 « Toujours lisible »: the bond
        /// points per animal): Light, treats, seeds, the Discovery egg, the sticker, tickets, ingredients, bond. The result
        /// card shows 6: past that, the ingredients, then tickets, then the sticker give way.
        /// </summary>
        private int BuildCells()
        {
            double minutes = 0.0;
            var light = BigValue.Zero;
            int treats = 0, seeds = 0, stickers = 0, eggs = 0, tickets = 0, ingredients0 = 0, ingredients1 = 0, bond = 0;
            ContentId treat = ContentId.None, seed = ContentId.None, ingredient0 = ContentId.None, ingredient1 = ContentId.None;
            for (int i = 0; i < _welcomeCount; i++)
            {
                ref readonly var w = ref _welcomes[i];
                ref readonly var loot = ref w.Loot;
                minutes += loot.LightMinutes;
                light += w.Light;
                treats += loot.Treats;
                if (treat == ContentId.None && loot.Treats > 0)
                    treat = loot.Treat;
                for (int k = 0; k < loot.SeedKinds && k < 4; k++)
                {
                    seeds += loot.SeedsAt(k);
                    if (seed == ContentId.None && loot.SeedsAt(k) > 0)
                        seed = loot.SeedAt(k);
                }
                stickers += loot.Stickers;
                eggs += loot.DiscoveryEggs;
                tickets += loot.Tickets;
                if (loot.Ingredients0 > 0)
                {
                    if (ingredient0 == ContentId.None || ingredient0 == loot.Ingredient0)
                    {
                        ingredient0 = loot.Ingredient0;
                        ingredients0 += loot.Ingredients0;
                    }
                    else
                    {
                        ingredient1 = loot.Ingredient0;
                        ingredients1 += loot.Ingredients0;
                    }
                }
                if (loot.Ingredients1 > 0)
                {
                    if (ingredient1 == ContentId.None || ingredient1 == loot.Ingredient1)
                    {
                        ingredient1 = loot.Ingredient1;
                        ingredients1 += loot.Ingredients1;
                    }
                    else
                    {
                        ingredients0 += loot.Ingredients1;
                    }
                }
                bond = Mathf.Max(bond, loot.BondPoints);
            }
            bool showIngredients1 = ingredients1 > 0, showIngredients0 = ingredients0 > 0, showTickets = tickets > 0, showSticker = stickers > 0;
            int total = (minutes > 0.0 || !light.IsZero ? 1 : 0) + (treats > 0 ? 1 : 0) + (seeds > 0 ? 1 : 0) + (eggs > 0 ? 1 : 0)
                        + (showSticker ? 1 : 0) + (showTickets ? 1 : 0) + (showIngredients0 ? 1 : 0) + (showIngredients1 ? 1 : 0) + (bond > 0 ? 1 : 0);
            const int shown = 6;
            if (total > shown && showIngredients1)
            {
                showIngredients1 = false;
                total--;
            }
            if (total > shown && showIngredients0)
            {
                showIngredients0 = false;
                total--;
            }
            if (total > shown && showTickets)
            {
                showTickets = false;
                total--;
            }
            if (total > shown && showSticker)
                showSticker = false;
            int n = 0;
            if (minutes > 0.0 || !light.IsZero)
                _cells[n++] = new RewardCell(RewardKind.Light, 0) { LightMinutes = minutes, Light = light };
            if (treats > 0)
                _cells[n++] = new RewardCell(RewardKind.Treat, treats, treat);
            if (seeds > 0)
                _cells[n++] = new RewardCell(RewardKind.Seed, seeds, seed);
            if (eggs > 0)
                _cells[n++] = new RewardCell(RewardKind.DiscoveryEgg, eggs);
            if (showSticker && n < _cells.Length)
                _cells[n++] = new RewardCell(RewardKind.Sticker, stickers);
            if (showTickets && n < _cells.Length)
                _cells[n++] = new RewardCell(RewardKind.Ticket, tickets);
            if (showIngredients0 && n < _cells.Length)
                _cells[n++] = new RewardCell(RewardKind.Ingredient, ingredients0, ingredient0);
            if (showIngredients1 && n < _cells.Length)
                _cells[n++] = new RewardCell(RewardKind.Ingredient, ingredients1, ingredient1);
            if (bond > 0 && n < _cells.Length)
                _cells[n++] = new RewardCell(RewardKind.Bond, bond);
            return n;
        }

        /// <summary>« Repartir »: the planning sheet again with the same destination and team (the animals are free now).</summary>
        public void PlayAgain()
        {
            if (_phase != Phase.WelcomeLoot || _welcomeCount == 0)
                return;
            Sound(AudioEventId.UiConfirm);
            var first = _welcomes[0];
            EndWelcome();
            int count = Mathf.Min(first.TeamSize, _scratch.Length);
            var team = new uint[ExpeditionLayout.MaxTeam];
            for (int k = 0; k < count; k++)
                team[k] = first.AnimalAt(k);
            _duration = first.Duration;
            int slot = _expeditions.StateOf(first.Slot) == ExpeditionSlotState.Empty ? first.Slot : FirstEmptySlot();
            if (slot >= 0 && _expeditions.IsDestinationOpen(first.Destination))
                OpenSheet(slot, first.Destination, -1, team, count);
        }

        /// <summary>« Retour »: back to the expeditions screen.</summary>
        public void BackToHub()
        {
            if (_phase < Phase.WelcomeWalk)
                return;
            Sound(AudioEventId.UiBack);
            EndWelcome();
        }

        private void EndWelcome()
        {
            CloseResult();
            _phase = Phase.Browsing;
            _view.HidePostcards();
            _view.SetDim(false);
            _view.DockPostcards(false);
            _view.SetContentShown(true);
            _activities?.Host?.SetWorldVisible?.Invoke(false);
            RebuildDestinations();
            RebuildChallenges();
            RefreshSlots(true);
            UpdateTutorial();
        }

        private void CloseResult()
        {
            if (!_resultOpen)
                return;
            _resultOpen = false;
            var result = _activities?.ResultView;
            if (result != null && result.IsOpen)
                result.Close();
        }

        // ---------------- the tutorial (doc 01 A18 and A19) ----------------

        /// <summary>
        /// A18, the first expedition: the paw on the destination, then on an animal, then on « Partir ». A19, its return:
        /// the paw on the glowing slot. Updated when something changes (never per frame).
        /// </summary>
        private void UpdateTutorial()
        {
            if (_view == null || _phase != Phase.Browsing)
                return;
            int step = 0;
            int target = -1;
            if (_expeditions.PostcardCount == 0 && _expeditions.BackCount > 0)
            {
                step = 4;
                target = FirstSlot(ExpeditionSlotState.Back);
            }
            else if (_expeditions.FirstStrollNext && _expeditions.SoonestReturn < 0.0)
            {
                if (!_sheet)
                {
                    step = 1;
                    target = CardOf(_destination != ContentId.None ? _destination : FirstOpenDestination());
                }
                else if (_teamCount == 0)
                {
                    step = 2;
                    target = _animalCount > 0 ? 0 : -1;
                }
                else
                {
                    step = 3;
                }
            }
            _tutorial = step;
            switch (step)
            {
                case 1 when target >= 0:
                    _view.SetPaw(true, _view.DestinationPoint(target));
                    break;
                case 2 when target >= 0:
                    _view.SetPaw(true, _view.AnimalPoint(target));
                    break;
                case 3:
                    _view.SetPaw(true, _view.GoPoint);
                    break;
                case 4 when target >= 0:
                    _view.SetPaw(true, _view.SlotPoint(target));
                    break;
                default:
                    _view.SetPaw(false, default);
                    break;
            }
        }

        private int FirstSlot(ExpeditionSlotState state)
        {
            int slots = Mathf.Min(_expeditions.SlotCount, ExpeditionLayout.SlotCapacity);
            for (int slot = 0; slot < slots; slot++)
                if (_expeditions.StateOf(slot) == state)
                    return slot;
            return -1;
        }

        private int CardOf(ContentId destination)
        {
            for (int card = 0; card < _cardCount; card++)
                if (_cards[card] == destination)
                    return card;
            return -1;
        }

        // ---------------- helpers ----------------

        private bool ReduceMotion => _context != null && _context.Save != null && _context.State.Settings.ReduceMotion;

        /// <summary>The save's state (null in captures and headless tests without a save).</summary>
        private PlayerState State => _context != null && _context.Save != null ? _context.State : null;

        /// <summary>The animals: the save's, else <see cref="CollectionOverride"/> (captures).</summary>
        private CollectionState Collection => State != null ? State.Collection : CollectionOverride;

        /// <summary>Captures without a save: the animals the team picker and the slots read.</summary>
        public CollectionState CollectionOverride { get; set; }

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

        private bool InTeam(uint instance) => IndexInTeam(instance) >= 0;

        private int IndexInTeam(uint instance)
        {
            for (int i = 0; i < _teamCount; i++)
                if (_team[i] == instance)
                    return i;
            return -1;
        }

        private bool Owned(uint instance)
        {
            var collection = Collection;
            return collection == null || collection.TryGet(instance, out _);
        }

        private ContentId DestinationBiome() =>
            _destination != ContentId.None && _expeditions.TryGetDestination(_destination, out var destination) && !destination.Event
                ? destination.Biome
                : ContentId.None;

        private ContentId BiomeOf(ContentId species) =>
            _context?.Content != null && _context.Content.Species.TryGet(species, out var definition) ? definition.Biome : ContentId.None;

        private string DestinationName(ContentId id) =>
            id != ContentId.None && _expeditions.TryGetDestination(id, out var destination) ? Text(ExpeditionText.DestinationName(destination.Key)) : string.Empty;

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
            _expeditions.SlotChanged -= _onSlotChanged;
            if (_phase != Phase.Closed)
                Close(false);
            Release();
            if (PostcardFactory != null)
                (_postcards as IDisposable)?.Dispose();
            _postcards = null;
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
