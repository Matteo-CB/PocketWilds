using System;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_Today, screen 031 (doc 06; doc 01 Contenu sections 0, 1.4, 2.6, 3, 4 and 5; doc 05 Sensations sheets 18 and
    /// 19): the daily visit as a bottom sheet, the refuge visible above it. Top to bottom: the welcome gift (days 1 to 3),
    /// the Cadeau du jour, the wishes with their animals (« Tout récupérer »; « Tes souhaits t'attendent »), the 28-day
    /// calendar, the weekly journal's row, one Close at the thumb. Dismissed in one gesture (a touch above the sheet, Back,
    /// Close); the sheet takes the height of what it shows so the visit fits on the reference phone without scrolling when
    /// it can. Sections are placed by hand when what they show changes (no layout group, no rebuild per frame); the list
    /// and the strip have their own canvases so scrolling and the breathing box never rebuild the rest. Closed: canvas
    /// disabled, content inactive, nothing ticks. Prewarmed at scene entry; nothing is instantiated after it. Sounds and
    /// claims belong to the presenter: the screen only plays what it is told.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TodayScreen : MonoBehaviour, ITodayView, IMotionSettings
    {
        public const int MaxWishRows = 9;
        public const int MaxWelcomeCells = 3;
        private const int MaxHeartsAtOnce = 3;
        private const float HeartsStagger = 0.4f;

        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _safeArea;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Button _outside;
        [SerializeField] private Button _close;
        [SerializeField] private TMP_Text _closeLabel;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private RectTransform _viewport;
        [SerializeField] private RectTransform _list;

        [Header("Welcome gift (days 1 to 3)")]
        [SerializeField] private RectTransform _welcome;
        [SerializeField] private TMP_Text _welcomeTitle;
        [SerializeField] private TMP_Text _welcomeDay;
        [SerializeField] private RewardCellView[] _welcomeCells = Array.Empty<RewardCellView>();
        [SerializeField] private Button _welcomeClaim;
        [SerializeField] private TMP_Text _welcomeClaimLabel;
        [SerializeField] private GameObject _welcomeDone;
        [SerializeField] private TMP_Text _welcomeTomorrow;

        [Header("Gift, wishes, calendar, journal")]
        [SerializeField] private GiftBoxView _gift;
        [SerializeField] private RectTransform _wishHeader;
        [SerializeField] private TMP_Text _wishTitle;
        [SerializeField] private Button _claimAll;
        [SerializeField] private TMP_Text _claimAllLabel;
        [SerializeField] private TMP_Text _swapUsed;
        [SerializeField] private TMP_Text _waiting;
        [SerializeField] private WishRowView[] _rows = Array.Empty<WishRowView>();
        [SerializeField] private RectTransform _calendarCard;
        [SerializeField] private CalendarStripView _calendar;
        [SerializeField] private RectTransform _journalRow;
        [SerializeField] private Button _journalButton;
        [SerializeField] private TMP_Text _journalLabel;
        [SerializeField] private GameObject _journalDot;

        [Header("Feel")]
        [SerializeField] private UiBurst _burst;
        [SerializeField] private TapHint _hint;
        [SerializeField] private Color _heartsColor = new Color(0.941f, 0.561f, 0.722f, 1f);
        [SerializeField] private Color _claimColor = new Color(0.498f, 0.698f, 0.941f, 1f);
        [SerializeField] private Color _claimAccent = new Color(0.949f, 0.710f, 0.267f, 1f);

        [Header("Art")]
        [SerializeField] private RewardIconSet _icons = new RewardIconSet();
        [Tooltip("Wish icons by WishSystem value (WishIcons.SpriteName).")]
        [SerializeField] private Sprite[] _wishIcons = Array.Empty<Sprite>();
        [SerializeField] private Sprite _wishDoneIcon;
        [SerializeField] private Sprite _portraitFallback;
        [SerializeField] private CodexVisualCatalog _catalog;

        [Header("Layout (1080 x 2340 reference)")]
        [SerializeField] private float _headerHeight = 140f;
        [SerializeField] private float _footerHeight = 176f;
        [SerializeField] private float _refugeAbove = 220f;
        [SerializeField] private float _minPanel = 900f;
        [SerializeField] private float _topPadding = 16f;
        [SerializeField] private float _bottomPadding = 20f;
        [SerializeField] private float _gap = 20f;
        [SerializeField] private float _welcomeHeight = 228f;
        [SerializeField] private float _giftHeight = 336f;
        [SerializeField] private float _wishHeaderHeight = 112f;
        [SerializeField] private float _captionHeight = 60f;
        [SerializeField] private float _rowHeight = 150f;
        [SerializeField] private float _rowGap = 10f;
        [SerializeField] private float _calendarHeight = 400f;
        [SerializeField] private float _journalHeight = 132f;
        [SerializeField] private float _welcomeCellPitch = 132f;
        [SerializeField] private float _welcomeCellsX = 420f;

        private readonly WindowMotion _motion = new WindowMotion(0.22f, true);
        private readonly SafeAreaFitter _fitter = new SafeAreaFitter();
        private readonly RewardTexts _rewardTexts = new RewardTexts();
        private readonly RewardCell[] _cells = new RewardCell[RewardCells.Max];
        private ITodayModel _model;
        private ITodayActions _actions;
        private UiLabel _titleLabel;
        private UiLabel _closeText;
        private UiLabel _welcomeTitleLabel;
        private UiLabel _welcomeDayLabel;
        private UiLabel _welcomeClaimText;
        private UiLabel _welcomeTomorrowLabel;
        private UiLabel _wishTitleLabel;
        private UiLabel _claimAllText;
        private UiLabel _swapUsedLabel;
        private UiLabel _waitingLabel;
        private UiLabel _journalText;
        private string _welcomeDayTemplate;
        private string _journalTemplate;
        private int _layoutKey = -1;
        private float _layoutSafeHeight = -1f;
        private int _welcomeIndexShown = -2;
        private int _heartsFrame = -1;
        private int _heartsInFrame;
        private bool _hintWanted;
        private TodayGiftState _giftState;
        private bool _reduceMotion;
        private bool _initialized;

        public bool IsOpen => _motion.IsOpen && !_motion.Closing;
        public bool Visible => _motion.IsOpen;

        public bool ReduceMotion
        {
            get => _reduceMotion;
            set
            {
                _reduceMotion = value;
                _motion.ReduceMotion = value;
                if (_gift != null)
                    _gift.ReduceMotion = value;
                if (_calendar != null)
                    _calendar.ReduceMotion = value;
                if (_burst != null)
                    _burst.ReduceMotion = value;
                if (_hint != null)
                    _hint.ReduceMotion = value;
                foreach (var row in _rows)
                    if (row != null)
                        row.ReduceMotion = value;
                foreach (var cell in _welcomeCells)
                    if (cell != null)
                        cell.ReduceMotion = value;
            }
        }

        // ---------------- read by tests and captures ----------------

        public Button CloseButton => _close;
        public Button OutsideButton => _outside;
        public Button ClaimAllButton => _claimAll;
        public Button WelcomeClaimButton => _welcomeClaim;
        public Button JournalButton => _journalButton;
        public GiftBoxView Gift => _gift;
        public CalendarStripView Calendar => _calendar;
        public UiBurst Burst => _burst;
        public WishRowView Row(int i) => _rows[i];
        public int RowCapacity => _rows.Length;
        public bool WelcomeShown => _welcome.gameObject.activeSelf;
        public bool WelcomeClaimShown => _welcomeClaim.gameObject.activeSelf;
        public bool GiftShown => _gift.gameObject.activeSelf;
        public bool ClaimAllShown => _claimAll.gameObject.activeSelf;
        public bool WaitingShown => _waiting.gameObject.activeSelf;
        public bool SwapUsedShown => _swapUsed.gameObject.activeSelf;
        public bool JournalShown => _journalRow.gameObject.activeSelf;
        public bool JournalDotShown => _journalDot.activeSelf;
        public string JournalText => _journalLabel.text;
        public string WelcomeDayText => _welcomeDay.text;
        public bool HintShown => _hint != null && _hint.Visible;
        public TapHint Hint => _hint;
        public float PanelHeight => _panel.rect.height;
        public float ListHeight => _list.sizeDelta.y;

        public int RowsShown
        {
            get
            {
                int n = 0;
                foreach (var row in _rows)
                    if (row.gameObject.activeSelf)
                        n++;
                return n;
            }
        }

        // ---------------- ITodayView ----------------

        public void Open(ITodayModel model, ILocalization text, ITodayActions actions)
        {
            Bind(model, text, actions);
            if (!_motion.IsOpen || _motion.Closing)
                _motion.Show();
            _list.anchoredPosition = Vector2.zero;
            _scroll.StopMovement();
            _calendar.ScrollToToday();
        }

        public void Prewarm(ITodayModel model, ILocalization text, ITodayActions actions)
        {
            if (_motion.IsOpen)
                return;
            Bind(model, text, actions);
            _motion.Show();
            // Every canvas built once, the burst's images included: the first opening never hitches.
            _burst.PlayLocal(UiBurstKind.Gift, Vector2.zero, Color.clear, Color.clear, true);
            _burst.PlayLocal(UiBurstKind.Hearts, Vector2.zero, Color.clear, Color.clear);
            Canvas.ForceUpdateCanvases();
            _burst.Clear();
            _motion.HideNow();
        }

        public void Refresh()
        {
            if (_model == null)
                return;
            int welcomeIndex = _model.WelcomeIndex;
            var giftState = _model.Gift;
            _giftState = giftState;
            int wishes = Mathf.Min(_model.WishCount, _rows.Length);
            bool undone = false;
            for (int i = 0; i < wishes; i++)
            {
                if (!_model.Wish(i).Card.Done)
                {
                    undone = true;
                    break;
                }
            }
            // Doc 01 section 2.4: with 9 wishes waiting nothing new is drawn; « Tes souhaits t'attendent ».
            bool waiting = wishes == 0 || _model.WishCount >= MaxWishRows;
            bool swapUsed = _model.SwapsLeft <= 0 && undone;
            bool welcome = welcomeIndex >= 0;
            bool gift = giftState != TodayGiftState.Hidden || _gift.Revealing;
            bool journal = _model.JournalVisible;
            int key = (welcome ? 1 : 0) | (gift ? 2 : 0) | (waiting ? 4 : 0) | (swapUsed ? 8 : 0) | (journal ? 16 : 0) | (wishes << 5);
            float safeHeight = _safeArea.rect.height;
            if (key != _layoutKey || !Mathf.Approximately(safeHeight, _layoutSafeHeight))
                Layout(welcome, gift, wishes, waiting, swapUsed, journal);

            if (welcome)
                BindWelcome(welcomeIndex);
            _gift.Bind(_model, _icons, _rewardTexts);

            for (int i = 0; i < _rows.Length; i++)
            {
                bool shown = i < wishes;
                _rows[i].SetShown(shown);
                if (!shown)
                    continue;
                var row = _model.Wish(i);
                var card = row.Card;
                var icon = card.Done ? _wishDoneIcon : WishIcon(card.System);
                _rows[i].Bind(row, card.CanSwap && _model.SwapsLeft > 0, icon, Portrait(row.Variant), _tokens.accentPrimary, _tokens.success);
            }
            SetActive(_claimAll.gameObject, _model.AnyWishDone);

            _calendar.Bind(_model, _icons, _rewardTexts);

            if (journal)
            {
                _journalText.SetTemplate(_journalTemplate, _model.JournalReached, _model.JournalGoals);
                SetActive(_journalDot, _model.JournalDot);
            }
            if (giftState != TodayGiftState.Ready && _hint != null)
                _hint.Set(false, default);
        }

        public void PlayWelcomeClaimed()
        {
            for (int i = 0; i < _welcomeCells.Length; i++)
                if (_welcomeCells[i].gameObject.activeSelf)
                    _welcomeCells[i].Bump();
            _burst.Play(UiBurstKind.Claim, _welcomeClaim.gameObject.activeSelf ? (RectTransform)_welcomeClaim.transform : _welcome, _claimColor,
                _claimAccent);
        }

        public void PlayGiftOpened()
        {
            if (_model == null)
                return;
            _giftState = TodayGiftState.Opened;
            _gift.PlayOpened(_model, _icons, _rewardTexts);
            if (_hint != null)
                _hint.Set(false, default);
        }

        public void PlayWishClaimed(int row)
        {
            if ((uint)row >= (uint)_rows.Length || !_rows[row].gameObject.activeInHierarchy)
                return;
            // Several at once (« Tout récupérer »): 400 ms apart, three at most (doc 05 sheet 17).
            int frame = Time.frameCount;
            _heartsInFrame = frame == _heartsFrame ? _heartsInFrame + 1 : 0;
            _heartsFrame = frame;
            if (_heartsInFrame >= MaxHeartsAtOnce)
                return;
            _rows[row].PlayClaimed();
            _burst.Play(UiBurstKind.Hearts, _rows[row].RewardPoint, _heartsColor, Color.white, false, _heartsInFrame * HeartsStagger);
        }

        public void PlayStamp(int box)
        {
            if (_model == null || (uint)box >= CalendarStripView.Boxes)
                return;
            var cell = _model.Box(box);
            _calendar.PlayStamp(box, cell.Big, cell.Reward.Kind == RewardKind.Gems);
        }

        public Vector2 GiftScreenPosition =>
            _motion.IsOpen && _gift.gameObject.activeInHierarchy ? ScreenPoint(_gift.Box) : ScreenCenter;

        public void SetGiftHint(bool visible)
        {
            _hintWanted = visible;
            UpdateHint();
        }

        public Vector2 RowScreenPosition(int row)
        {
            if (!_motion.IsOpen || (uint)row >= (uint)_rows.Length || !_rows[row].gameObject.activeInHierarchy)
                return ScreenCenter;
            return ScreenPoint(_rows[row].RewardPoint);
        }

        public void Close()
        {
            _motion.Hide();
            if (_hint != null)
                _hint.Set(false, default);
        }

        /// <summary>Android back, or Escape: the same as Close (the journal above takes it first).</summary>
        public void PressBack()
        {
            if (IsOpen && !WeeklyJournalScreen.AnyOpen)
                _actions?.CloseToday();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_motion.IsOpen)
                return;
            if (_motion.BackPressed)
                PressBack();
            _fitter.Fit(_safeArea);
            if (_model != null && !Mathf.Approximately(_safeArea.rect.height, _layoutSafeHeight))
                Refresh();
            bool closed = _motion.Tick(unscaledDeltaTime);
            _gift.Tick(unscaledDeltaTime);
            for (int i = 0; i < _rows.Length; i++)
                _rows[i].Tick(unscaledDeltaTime);
            for (int i = 0; i < _welcomeCells.Length; i++)
                _welcomeCells[i].Tick(unscaledDeltaTime);
            _calendar.Tick(unscaledDeltaTime);
            _burst.Tick(unscaledDeltaTime);
            UpdateHint();
            if (_hint != null)
                _hint.Tick(unscaledDeltaTime);
            if (closed)
            {
                // Closed mid-sequence: the next opening shows the gift as it is.
                _gift.Finish();
                _burst.Clear();
            }
        }

        // ---------------- binding ----------------

        private void Bind(ITodayModel model, ILocalization text, ITodayActions actions)
        {
            _model = model;
            _actions = actions;
            Initialize();
            _fitter.Fit(_safeArea);
            _rewardTexts.Bind(text);
            _titleLabel.SetString(text.Get(DailyVisitText.TodayTitle));
            _closeText.SetString(text.Get(DailyVisitText.TodayClose));
            _welcomeTitleLabel.SetString(text.Get(DailyVisitText.WelcomeTitle));
            _welcomeClaimText.SetString(text.Get(DailyVisitText.TodayClaim));
            _welcomeTomorrowLabel.SetString(text.Get(DailyVisitText.GiftTomorrow));
            _welcomeDayTemplate = text.Get(DailyVisitText.WelcomeDay);
            _wishTitleLabel.SetString(text.Get(DailyVisitText.WishesTitle));
            _claimAllText.SetString(text.Get(DailyVisitText.TodayClaimAll));
            _swapUsedLabel.SetString(text.Get(DailyVisitText.WishSwapUsed));
            _waitingLabel.SetString(text.Get(DailyVisitText.WishWaiting));
            _journalTemplate = text.Get(DailyVisitText.JournalRow);
            string claim = text.Get(DailyVisitText.WishClaim);
            string swap = text.Get(DailyVisitText.WishSwap);
            for (int i = 0; i < _rows.Length; i++)
                _rows[i].SetTexts(claim, swap);
            _gift.SetTexts(text);
            _calendar.SetTexts(text);
            _welcomeIndexShown = -2;
            _layoutKey = -1;
            Refresh();
        }

        private void BindWelcome(int index)
        {
            if (index != _welcomeIndexShown)
            {
                _welcomeIndexShown = index;
                _welcomeDayLabel.SetTemplate(_welcomeDayTemplate, index + 1);
            }
            int count = Mathf.Min(_model.WelcomeRewards(_cells), _welcomeCells.Length);
            float left = _welcomeCellsX - (count - 1) * 0.5f * _welcomeCellPitch;
            for (int i = 0; i < _welcomeCells.Length; i++)
            {
                bool shown = i < count;
                _welcomeCells[i].SetShown(shown);
                if (!shown)
                    continue;
                _welcomeCells[i].Bind(_cells[i], _icons, _rewardTexts);
                var rect = _welcomeCells[i].Rect;
                rect.anchoredPosition = new Vector2(left + i * _welcomeCellPitch, rect.anchoredPosition.y);
            }
            bool claimable = _model.WelcomeClaimable;
            SetActive(_welcomeClaim.gameObject, claimable);
            SetActive(_welcomeDone, !claimable);
        }

        /// <summary>
        /// Places the sections top to bottom and sizes the sheet to them (the refuge keeps at least a band above it). Runs
        /// when what is shown changes, never per frame.
        /// </summary>
        private void Layout(bool welcome, bool gift, int wishes, bool waiting, bool swapUsed, bool journal)
        {
            _layoutKey = (welcome ? 1 : 0) | (gift ? 2 : 0) | (waiting ? 4 : 0) | (swapUsed ? 8 : 0) | (journal ? 16 : 0) | (wishes << 5);
            float y = -_topPadding;
            y = Place(_welcome, welcome, y, _welcomeHeight);
            y = Place((RectTransform)_gift.transform, gift, y, _giftHeight);
            Place(_wishHeader, true, y, _wishHeaderHeight);
            y -= _wishHeaderHeight;
            y = PlaceCaption(_swapUsed, swapUsed, y);
            y = PlaceCaption(_waiting, waiting, y);
            for (int i = 0; i < _rows.Length; i++)
            {
                bool shown = i < wishes;
                _rows[i].SetShown(shown);
                if (!shown)
                    continue;
                var rect = _rows[i].Rect;
                // Rows pivot at their middle (the claim pop scales around it).
                rect.anchoredPosition = new Vector2(0f, y - _rowHeight * 0.5f);
                y -= _rowHeight + _rowGap;
            }
            y -= _gap - (wishes > 0 ? _rowGap : 0f);
            y = Place(_calendarCard, true, y, _calendarHeight);
            y = Place(_journalRow, journal, y, _journalHeight);
            float listHeight = -y - _gap + _bottomPadding;
            _list.sizeDelta = new Vector2(_list.sizeDelta.x, listHeight);

            float safeHeight = _safeArea.rect.height;
            _layoutSafeHeight = safeHeight;
            if (safeHeight < 400f)
                safeHeight = 2340f - 160f;
            float wanted = _headerHeight + listHeight + _footerHeight;
            float max = Mathf.Max(_minPanel, safeHeight - _refugeAbove);
            float height = Mathf.Clamp(wanted, Mathf.Min(_minPanel, max), max);
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, height);
            float viewport = height - _headerHeight - _footerHeight;
            _scroll.vertical = listHeight > viewport + 1f;
            if (!_scroll.vertical)
                _list.anchoredPosition = Vector2.zero;
        }

        private float Place(RectTransform section, bool shown, float y, float height)
        {
            SetActive(section.gameObject, shown);
            if (!shown)
                return y;
            section.anchoredPosition = new Vector2(0f, y);
            return y - height - _gap;
        }

        private float PlaceCaption(TMP_Text caption, bool shown, float y)
        {
            SetActive(caption.gameObject, shown);
            if (!shown)
                return y;
            caption.rectTransform.anchoredPosition = new Vector2(0f, y);
            return y - _captionHeight;
        }

        private void UpdateHint()
        {
            if (_hint == null)
                return;
            bool show = _hintWanted && _motion.IsOpen && !_motion.Closing && _giftState == TodayGiftState.Ready && !_gift.Revealing
                        && _gift.gameObject.activeInHierarchy;
            if (show)
                _hint.Set(true, GiftScreenPosition);
            else if (_hint.Visible)
                _hint.Set(false, default);
        }

        private Sprite WishIcon(WishSystem system) =>
            (int)system < _wishIcons.Length && _wishIcons[(int)system] != null ? _wishIcons[(int)system] : _wishIcons.Length > 0 ? _wishIcons[0] : null;

        private Sprite Portrait(ContentId variant)
        {
            if (_catalog != null && variant.IsValid && _catalog.TryGetVariant(variant, out var sprite) && sprite != null)
                return sprite;
            return _portraitFallback;
        }

        private static Vector2 ScreenPoint(RectTransform rect) =>
            RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));

        private static Vector2 ScreenCenter => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _motion.Bind(_canvas, _content, _group, _panel);
            _motion.ReduceMotion = _reduceMotion;
            _titleLabel = new UiLabel(_title);
            _closeText = new UiLabel(_closeLabel);
            _welcomeTitleLabel = new UiLabel(_welcomeTitle);
            _welcomeDayLabel = new UiLabel(_welcomeDay, 48);
            _welcomeClaimText = new UiLabel(_welcomeClaimLabel);
            _welcomeTomorrowLabel = new UiLabel(_welcomeTomorrow);
            _wishTitleLabel = new UiLabel(_wishTitle);
            _claimAllText = new UiLabel(_claimAllLabel);
            _swapUsedLabel = new UiLabel(_swapUsed);
            _waitingLabel = new UiLabel(_waiting);
            _journalText = new UiLabel(_journalLabel, 96);
            _close.onClick.AddListener(OnClose);
            _outside.onClick.AddListener(OnClose);
            _welcomeClaim.onClick.AddListener(OnClaimWelcome);
            _claimAll.onClick.AddListener(OnClaimAll);
            _journalButton.onClick.AddListener(OnJournal);
            _gift.Initialize(OnOpenGift, OnGiftInfo, _burst);
            _calendar.Initialize(OnStamp, _burst, _scroll);
            for (int i = 0; i < _rows.Length; i++)
                _rows[i].Initialize(i, OnClaimWish, OnSwapWish);
            ReduceMotion = _reduceMotion;
        }

        private void OnClose() => _actions?.CloseToday();
        private void OnClaimWelcome() => _actions?.ClaimWelcome();
        private void OnClaimAll() => _actions?.ClaimAllWishes();
        private void OnJournal() => _actions?.OpenJournal();
        private void OnOpenGift() => _actions?.OpenGift();
        private void OnGiftInfo() => _actions?.OpenGiftInfo();
        private void OnStamp() => _actions?.StampCalendar();
        private void OnClaimWish(int row) => _actions?.ClaimWish(row);
        private void OnSwapWish(int row) => _actions?.SwapWish(row);

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        private void OnDestroy()
        {
            if (!_initialized)
                return;
            _close.onClick.RemoveListener(OnClose);
            _outside.onClick.RemoveListener(OnClose);
            _welcomeClaim.onClick.RemoveListener(OnClaimWelcome);
            _claimAll.onClick.RemoveListener(OnClaimAll);
            _journalButton.onClick.RemoveListener(OnJournal);
        }

        // ---------------- prefab builder hooks (Editor DailyVisitUiPipeline) ----------------

        public void SetReferences(UiColorTokens tokens, Canvas canvas, GameObject content, CanvasGroup group, RectTransform safeArea, RectTransform panel,
            Button outside, Button close, TMP_Text closeLabel, TMP_Text title, ScrollRect scroll, RectTransform viewport, RectTransform list)
        {
            _tokens = tokens;
            _canvas = canvas;
            _content = content;
            _group = group;
            _safeArea = safeArea;
            _panel = panel;
            _outside = outside;
            _close = close;
            _closeLabel = closeLabel;
            _title = title;
            _scroll = scroll;
            _viewport = viewport;
            _list = list;
        }

        public void SetWelcomeReferences(RectTransform welcome, TMP_Text title, TMP_Text day, RewardCellView[] cells, Button claim, TMP_Text claimLabel,
            GameObject done, TMP_Text tomorrow, float cellPitch, float cellsX)
        {
            _welcome = welcome;
            _welcomeTitle = title;
            _welcomeDay = day;
            _welcomeCells = cells;
            _welcomeClaim = claim;
            _welcomeClaimLabel = claimLabel;
            _welcomeDone = done;
            _welcomeTomorrow = tomorrow;
            _welcomeCellPitch = cellPitch;
            _welcomeCellsX = cellsX;
        }

        public void SetSectionReferences(GiftBoxView gift, RectTransform wishHeader, TMP_Text wishTitle, Button claimAll, TMP_Text claimAllLabel,
            TMP_Text swapUsed, TMP_Text waiting, WishRowView[] rows, RectTransform calendarCard, CalendarStripView calendar, RectTransform journalRow,
            Button journalButton, TMP_Text journalLabel, GameObject journalDot)
        {
            _gift = gift;
            _wishHeader = wishHeader;
            _wishTitle = wishTitle;
            _claimAll = claimAll;
            _claimAllLabel = claimAllLabel;
            _swapUsed = swapUsed;
            _waiting = waiting;
            _rows = rows;
            _calendarCard = calendarCard;
            _calendar = calendar;
            _journalRow = journalRow;
            _journalButton = journalButton;
            _journalLabel = journalLabel;
            _journalDot = journalDot;
        }

        public void SetArtReferences(UiBurst burst, TapHint hint, RewardIconSet icons, Sprite[] wishIcons, Sprite wishDoneIcon, Sprite portraitFallback,
            CodexVisualCatalog catalog)
        {
            _burst = burst;
            _hint = hint;
            _icons = icons;
            _wishIcons = wishIcons;
            _wishDoneIcon = wishDoneIcon;
            _portraitFallback = portraitFallback;
            _catalog = catalog;
        }

        public void SetLayout(float headerHeight, float footerHeight, float welcomeHeight, float giftHeight, float wishHeaderHeight, float captionHeight,
            float rowHeight, float rowGap, float calendarHeight, float journalHeight, float gap)
        {
            _headerHeight = headerHeight;
            _footerHeight = footerHeight;
            _welcomeHeight = welcomeHeight;
            _giftHeight = giftHeight;
            _wishHeaderHeight = wishHeaderHeight;
            _captionHeight = captionHeight;
            _rowHeight = rowHeight;
            _rowGap = rowGap;
            _calendarHeight = calendarHeight;
            _journalHeight = journalHeight;
            _gap = gap;
        }
    }
}
