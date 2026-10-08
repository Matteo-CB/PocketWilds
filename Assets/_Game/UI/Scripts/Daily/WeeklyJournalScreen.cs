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
    /// PF_UI_WeeklyJournal, screen 032 (doc 06; doc 01 Contenu section 5): the week's goals (7, up to 10 by Remote Config)
    /// with their progress, each taken with « Réclamer »; the completion chest (UI_Journal_Chest: 15 Gems and a Discovery
    /// egg) that opens on its claim; the real days left (« Encore {n} jours », « Dernier jour »: never a countdown); the
    /// first journal's title and the « Tu as déjà commencé » badge on the goals that started ahead; last week's reached
    /// goals on their own line. A bottom sheet above Today, dismissed in one gesture (a touch above it, Back, Close). Rows
    /// are built with the prefab and rebound in place; closed, it costs nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeeklyJournalScreen : MonoBehaviour, IJournalView, IMotionSettings
    {
        public const int MaxGoals = 10;
        public const int MaxChestCells = 2;
        public const int MaxCarryCells = 3;
        private const float ChestSeconds = 1.1f;

        private static int s_open;

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
        [SerializeField] private TMP_Text _days;

        // What every goal pays, said once beside the days left.
        [SerializeField] private TMP_Text _each;
        [SerializeField] private RewardCellView _each0;
        [SerializeField] private RewardCellView _each1;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private RectTransform _list;

        [Header("Last week")]
        [SerializeField] private RectTransform _carry;
        [SerializeField] private TMP_Text _carryText;
        [SerializeField] private RewardCellView[] _carryCells = Array.Empty<RewardCellView>();
        [SerializeField] private Button _carryClaim;
        [SerializeField] private TMP_Text _carryClaimLabel;

        [Header("Goals and chest")]
        [SerializeField] private JournalGoalRowView[] _rows = Array.Empty<JournalGoalRowView>();
        [SerializeField] private RectTransform _chestCard;
        [SerializeField] private RectTransform _chest;
        [SerializeField] private Image _chestGlow;
        [SerializeField] private TMP_Text _chestTitle;
        [SerializeField] private TMP_Text _chestProgress;
        [SerializeField] private RewardCellView[] _chestCells = Array.Empty<RewardCellView>();
        [SerializeField] private Button _chestClaim;
        [SerializeField] private TMP_Text _chestClaimLabel;
        [SerializeField] private Image _chestCheck;

        [Header("Feel and art")]
        [SerializeField] private UiBurst _burst;
        [SerializeField] private RewardIconSet _icons = new RewardIconSet();
        [Tooltip("Slot icons by JournalSlot value.")]
        [SerializeField] private Sprite[] _slotIcons = Array.Empty<Sprite>();
        [SerializeField] private Color _claimColor = new Color(0.498f, 0.698f, 0.941f, 1f);
        [SerializeField] private Color _goldColor = new Color(0.949f, 0.710f, 0.267f, 1f);

        [Header("Layout (1080 x 2340 reference)")]
        [SerializeField] private float _headerHeight = 210f;
        [SerializeField] private float _footerHeight = 176f;
        [SerializeField] private float _refugeAbove = 220f;
        [SerializeField] private float _topPadding = 12f;
        [SerializeField] private float _bottomPadding = 28f;
        [SerializeField] private float _gap = 22f;
        [SerializeField] private float _carryHeight = 236f;
        [SerializeField] private float _rowHeight = 144f;
        [SerializeField] private float _rowGap = 10f;
        [SerializeField] private float _chestHeight = 270f;
        [SerializeField] private float _cellPitch = 132f;
        [SerializeField] private float _carryCellsX = 330f;
        [SerializeField] private float _chestCellsX = 420f;

        private readonly WindowMotion _motion = new WindowMotion(0.22f, true);
        private readonly SafeAreaFitter _fitter = new SafeAreaFitter();
        private readonly RewardTexts _rewardTexts = new RewardTexts();
        private readonly RewardCell[] _cells = new RewardCell[RewardCells.Max];
        private IJournalModel _model;
        private IJournalActions _actions;
        private UiLabel _titleLabel;
        private UiLabel _daysLabel;
        private UiLabel _closeText;
        private UiLabel _carryLabel;
        private UiLabel _carryClaimText;
        private UiLabel _chestTitleLabel;
        private UiLabel _chestProgressLabel;
        private UiLabel _chestClaimText;
        private string _titleText;
        private string _firstText;
        private UiCounted _daysLeft;
        private string _lastDay;
        private int _daysShown = -1;
        private int _layoutKey = -1;
        private float _layoutSafeHeight = -1f;
        private float _chestTime = -1f;
        private float _glowTime;
        private bool _counted;
        private bool _reduceMotion;
        private bool _initialized;

        /// <summary>A journal is open or closing (Today leaves Back to it).</summary>
        public static bool AnyOpen => s_open > 0;

        public bool IsOpen => _motion.IsOpen && !_motion.Closing;

        public bool ReduceMotion
        {
            get => _reduceMotion;
            set
            {
                _reduceMotion = value;
                _motion.ReduceMotion = value;
                if (_burst != null)
                    _burst.ReduceMotion = value;
                foreach (var row in _rows)
                    if (row != null)
                        row.ReduceMotion = value;
                foreach (var cell in _chestCells)
                    if (cell != null)
                        cell.ReduceMotion = value;
            }
        }

        // ---------------- read by tests and captures ----------------

        public Button CloseButton => _close;
        public Button OutsideButton => _outside;
        public Button ChestClaimButton => _chestClaim;
        public Button CarryClaimButton => _carryClaim;
        public JournalGoalRowView Row(int i) => _rows[i];
        public string TitleText => _title.text;
        public string DaysText => _days.text;
        public bool CarryShown => _carry.gameObject.activeSelf;
        public bool ChestClaimShown => _chestClaim.gameObject.activeSelf;
        public bool ChestCheckShown => _chestCheck.gameObject.activeSelf;
        public bool ChestOpening => _chestTime >= 0f;
        public UiBurst Burst => _burst;

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

        // ---------------- IJournalView ----------------

        public void Open(IJournalModel model, ILocalization text, IJournalActions actions)
        {
            Bind(model, text, actions);
            if (!_motion.IsOpen || _motion.Closing)
            {
                _motion.Show();
                Count(true);
            }
            _list.anchoredPosition = Vector2.zero;
            _scroll.StopMovement();
        }

        public void Prewarm(IJournalModel model, ILocalization text, IJournalActions actions)
        {
            if (_motion.IsOpen)
                return;
            Bind(model, text, actions);
            _motion.Show();
            _burst.PlayLocal(UiBurstKind.Claim, Vector2.zero, Color.clear, Color.clear);
            Canvas.ForceUpdateCanvases();
            _burst.Clear();
            _motion.HideNow();
        }

        public void Refresh()
        {
            if (_model == null)
                return;
            int goals = Mathf.Min(_model.GoalCount, _rows.Length);
            bool carry = _model.HasCarry;
            int key = (carry ? 1 : 0) | (goals << 1);
            if (key != _layoutKey || !Mathf.Approximately(_safeArea.rect.height, _layoutSafeHeight))
                Layout(carry, goals);

            _titleLabel.SetString(_model.IsFirst ? _firstText : _titleText);
            int days = Mathf.Max(1, _model.DaysLeft);
            if (days != _daysShown)
            {
                _daysShown = days;
                if (days <= 1)
                    _daysLabel.SetString(_lastDay);
                else
                    _daysLabel.SetTemplate(_daysLeft.For(days), days);
            }

            if (carry)
            {
                int count = Mathf.Min(_model.CarryRewards(_cells), _carryCells.Length);
                BindCells(_carryCells, count, _carryCellsX);
            }

            int reached = 0;
            for (int i = 0; i < _rows.Length; i++)
            {
                bool shown = i < goals;
                _rows[i].SetShown(shown);
                if (!shown)
                    continue;
                var goal = _model.Goal(i);
                if (goal.Reached || goal.Claimed)
                    reached++;
                _rows[i].Bind(goal, SlotIcon(goal.Slot), _icons, _rewardTexts, _tokens.accentPrimary, _tokens.success);
            }

            if (_each0 != null && goals > 0 && !_eachShown)
            {
                // Every goal of a journal pays the same: bound once per opening.
                _eachShown = true;
                var first = _model.Goal(0);
                BindEach(_each0, first.Reward0);
                BindEach(_each1, first.Reward1);
            }

            int chestCount = Mathf.Min(_model.CompleteRewards(_cells), _chestCells.Length);
            BindCells(_chestCells, chestCount, _chestCellsX);
            bool claimable = _model.CanClaimComplete;
            bool claimed = _model.CompleteClaimed;
            SetActive(_chestClaim.gameObject, claimable);
            SetActive(_chestCheck.gameObject, claimed);
            SetActive(_chestProgress.gameObject, !claimable && !claimed);
            if (!claimable && !claimed)
                _chestProgressLabel.SetRatio(reached, goals);
            SetActive(_chestGlow.gameObject, claimable || _chestTime >= 0f);
        }

        public void PlayGoalClaimed(int i)
        {
            if ((uint)i >= (uint)_rows.Length || !_rows[i].gameObject.activeInHierarchy)
                return;
            _rows[i].PlayClaimed();
            _burst.Play(UiBurstKind.Claim, _rows[i].ClaimPoint, _claimColor, _goldColor);
        }

        /// <summary>The chest opens: it bounces, a gold burst, its Gems and egg pop out (doc 05 sheet 18's feel).</summary>
        public void PlayCompleteClaimed()
        {
            _chestTime = 0f;
            SetActive(_chestGlow.gameObject, true);
            _burst.Play(UiBurstKind.Gift, _chest, _goldColor, _goldColor, true, ReduceMotion ? 0f : 0.18f);
            for (int i = 0; i < _chestCells.Length; i++)
                if (_chestCells[i].gameObject.activeSelf)
                    _chestCells[i].Reveal(0.3f + i * 0.22f);
        }

        public Vector2 GoalScreenPosition(int i)
        {
            if (!_motion.IsOpen || (uint)i >= (uint)_rows.Length || !_rows[i].gameObject.activeInHierarchy)
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var rect = _rows[i].ClaimPoint;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        public void Close() => _motion.Hide();

        /// <summary>Android back, or Escape: the same as Close.</summary>
        public void PressBack()
        {
            if (IsOpen)
                _actions?.CloseJournal();
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
            for (int i = 0; i < _rows.Length; i++)
                _rows[i].Tick(unscaledDeltaTime);
            for (int i = 0; i < _chestCells.Length; i++)
                _chestCells[i].Tick(unscaledDeltaTime);
            for (int i = 0; i < _carryCells.Length; i++)
                _carryCells[i].Tick(unscaledDeltaTime);
            TickChest(unscaledDeltaTime);
            _burst.Tick(unscaledDeltaTime);
            if (closed)
            {
                _burst.Clear();
                FinishChest();
                Count(false);
            }
        }

        private void TickChest(float deltaTime)
        {
            if (_chestGlow.gameObject.activeSelf)
            {
                _glowTime += deltaTime;
                var color = _chestGlow.color;
                color.a = ReduceMotion ? 0.6f : 0.5f + 0.25f * Mathf.Sin(_glowTime * 1.1f * 6.2832f);
                _chestGlow.color = color;
            }
            if (_chestTime < 0f)
                return;
            _chestTime += deltaTime;
            float t = _chestTime;
            if (!ReduceMotion)
            {
                // Anticipation (a small rock), the lid gives way (a squash and a bounce), then it settles.
                float angle = t < 0.18f ? 5f * Mathf.Sin(t / 0.18f * Mathf.PI * 2f) : 0f;
                _chest.localEulerAngles = new Vector3(0f, 0f, angle);
                float s;
                if (t < 0.18f)
                    s = 1f;
                else if (t < 0.26f)
                    s = Mathf.Lerp(1f, 0.92f, UiCurves.Smooth((t - 0.18f) / 0.08f));
                else if (t < 0.42f)
                    s = Mathf.Lerp(0.92f, 1.12f, UiCurves.EaseOut((t - 0.26f) / 0.16f));
                else if (t < 0.52f)
                    s = Mathf.Lerp(1.12f, 0.98f, UiCurves.Smooth((t - 0.42f) / 0.1f));
                else
                    s = Mathf.Lerp(0.98f, 1f, UiCurves.Smooth((t - 0.52f) / 0.1f));
                _chest.localScale = new Vector3(s, s, 1f);
            }
            if (t >= ChestSeconds)
                FinishChest();
        }

        private void FinishChest()
        {
            if (_chestTime < 0f)
                return;
            _chestTime = -1f;
            _chest.localEulerAngles = Vector3.zero;
            _chest.localScale = Vector3.one;
            for (int i = 0; i < _chestCells.Length; i++)
                if (_chestCells[i].gameObject.activeSelf)
                    _chestCells[i].ShowNow();
            if (_model != null)
                SetActive(_chestGlow.gameObject, _model.CanClaimComplete);
        }

        // ---------------- binding ----------------

        private void Bind(IJournalModel model, ILocalization text, IJournalActions actions)
        {
            _model = model;
            _actions = actions;
            Initialize();
            _fitter.Fit(_safeArea);
            _rewardTexts.Bind(text);
            _titleText = text.Get(DailyVisitText.JournalTitle);
            _firstText = text.Get(DailyVisitText.JournalFirst);
            _daysLeft = UiCounted.From(text, DailyVisitText.JournalDaysLeft);
            _lastDay = text.Get(DailyVisitText.JournalLastDay);
            _daysShown = -1;
            _closeText.SetString(text.Get(DailyVisitText.TodayClose));
            _carryLabel.SetString(text.Get(DailyVisitText.JournalCarry));
            string claim = text.Get(DailyVisitText.JournalClaim);
            _carryClaimText.SetString(claim);
            _chestClaimText.SetString(claim);
            _chestTitleLabel.SetString(text.Get(DailyVisitText.JournalComplete));
            if (_each != null)
                _each.text = text.Get(DailyVisitText.JournalEachGoal);
            _eachShown = false;
            string headStart = text.Get(DailyVisitText.JournalHeadStart);
            for (int i = 0; i < _rows.Length; i++)
                _rows[i].SetTexts(claim, headStart);
            _layoutKey = -1;
            Refresh();
        }

        private bool _eachShown;

        private void BindEach(RewardCellView view, in RewardCell cell)
        {
            if (view == null)
                return;
            bool shown = !cell.IsEmpty;
            view.SetShown(shown);
            if (shown)
            {
                view.Bind(cell, _icons, _rewardTexts);
                view.ShowNow();
            }
        }

        private void BindCells(RewardCellView[] views, int count, float centerX)
        {
            float left = centerX - (count - 1) * 0.5f * _cellPitch;
            for (int i = 0; i < views.Length; i++)
            {
                bool shown = i < count;
                views[i].SetShown(shown);
                if (!shown)
                    continue;
                views[i].Bind(_cells[i], _icons, _rewardTexts);
                var rect = views[i].Rect;
                rect.anchoredPosition = new Vector2(left + i * _cellPitch, rect.anchoredPosition.y);
            }
        }

        private void Layout(bool carry, int goals)
        {
            _layoutKey = (carry ? 1 : 0) | (goals << 1);
            float y = -_topPadding;
            SetActive(_carry.gameObject, carry);
            if (carry)
            {
                _carry.anchoredPosition = new Vector2(0f, y);
                y -= _carryHeight + _gap;
            }
            for (int i = 0; i < _rows.Length; i++)
            {
                bool shown = i < goals;
                _rows[i].SetShown(shown);
                if (!shown)
                    continue;
                _rows[i].Rect.anchoredPosition = new Vector2(0f, y - _rowHeight * 0.5f);
                y -= _rowHeight + _rowGap;
            }
            y -= _gap - _rowGap;
            _chestCard.anchoredPosition = new Vector2(0f, y);
            y -= _chestHeight;
            float listHeight = -y + _bottomPadding;
            _list.sizeDelta = new Vector2(_list.sizeDelta.x, listHeight);

            float safeHeight = _safeArea.rect.height;
            _layoutSafeHeight = safeHeight;
            if (safeHeight < 400f)
                safeHeight = 2340f - 160f;
            float max = Mathf.Max(800f, safeHeight - _refugeAbove);
            float height = Mathf.Clamp(_headerHeight + listHeight + _footerHeight, 800f, max);
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, height);
            _scroll.vertical = listHeight > height - _headerHeight - _footerHeight + 1f;
            if (!_scroll.vertical)
                _list.anchoredPosition = Vector2.zero;
        }

        private Sprite SlotIcon(JournalSlot slot) =>
            (int)slot < _slotIcons.Length && _slotIcons[(int)slot] != null ? _slotIcons[(int)slot] : _slotIcons.Length > 0 ? _slotIcons[_slotIcons.Length - 1] : null;

        private void Count(bool open)
        {
            if (open == _counted)
                return;
            _counted = open;
            s_open += open ? 1 : -1;
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _motion.Bind(_canvas, _content, _group, _panel);
            _titleLabel = new UiLabel(_title);
            _daysLabel = new UiLabel(_days, 96);
            _closeText = new UiLabel(_closeLabel);
            _carryLabel = new UiLabel(_carryText);
            _carryClaimText = new UiLabel(_carryClaimLabel);
            _chestTitleLabel = new UiLabel(_chestTitle);
            _chestProgressLabel = new UiLabel(_chestProgress, 32);
            _chestClaimText = new UiLabel(_chestClaimLabel);
            _close.onClick.AddListener(OnClose);
            _outside.onClick.AddListener(OnClose);
            _chestClaim.onClick.AddListener(OnChest);
            _carryClaim.onClick.AddListener(OnCarry);
            for (int i = 0; i < _rows.Length; i++)
                _rows[i].Initialize(i, OnGoal);
            ReduceMotion = _reduceMotion;
        }

        private void OnClose() => _actions?.CloseJournal();
        private void OnChest() => _actions?.ClaimComplete();
        private void OnCarry() => _actions?.ClaimCarry();
        private void OnGoal(int i) => _actions?.ClaimGoal(i);

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        private void OnDestroy()
        {
            Count(false);
            if (!_initialized)
                return;
            _close.onClick.RemoveListener(OnClose);
            _outside.onClick.RemoveListener(OnClose);
            _chestClaim.onClick.RemoveListener(OnChest);
            _carryClaim.onClick.RemoveListener(OnCarry);
        }

        // ---------------- prefab builder hooks (Editor DailyVisitUiPipeline) ----------------

        public void SetReferences(UiColorTokens tokens, Canvas canvas, GameObject content, CanvasGroup group, RectTransform safeArea, RectTransform panel,
            Button outside, Button close, TMP_Text closeLabel, TMP_Text title, TMP_Text days, ScrollRect scroll, RectTransform list)
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
            _days = days;
            _scroll = scroll;
            _list = list;
        }

        public void SetSectionReferences(RectTransform carry, TMP_Text carryText, RewardCellView[] carryCells, Button carryClaim, TMP_Text carryClaimLabel,
            JournalGoalRowView[] rows, RectTransform chestCard, RectTransform chest, Image chestGlow, TMP_Text chestTitle, TMP_Text chestProgress,
            RewardCellView[] chestCells, Button chestClaim, TMP_Text chestClaimLabel, Image chestCheck)
        {
            _carry = carry;
            _carryText = carryText;
            _carryCells = carryCells;
            _carryClaim = carryClaim;
            _carryClaimLabel = carryClaimLabel;
            _rows = rows;
            _chestCard = chestCard;
            _chest = chest;
            _chestGlow = chestGlow;
            _chestTitle = chestTitle;
            _chestProgress = chestProgress;
            _chestCells = chestCells;
            _chestClaim = chestClaim;
            _chestClaimLabel = chestClaimLabel;
            _chestCheck = chestCheck;
        }

        public void SetEachGoalReferences(TMP_Text each, RewardCellView each0, RewardCellView each1)
        {
            _each = each;
            _each0 = each0;
            _each1 = each1;
        }

        public void SetArtReferences(UiBurst burst, RewardIconSet icons, Sprite[] slotIcons)
        {
            _burst = burst;
            _icons = icons;
            _slotIcons = slotIcons;
        }

        public void SetLayout(float headerHeight, float footerHeight, float carryHeight, float rowHeight, float rowGap, float chestHeight, float gap,
            float cellPitch, float carryCellsX, float chestCellsX)
        {
            _headerHeight = headerHeight;
            _footerHeight = footerHeight;
            _carryHeight = carryHeight;
            _rowHeight = rowHeight;
            _rowGap = rowGap;
            _chestHeight = chestHeight;
            _gap = gap;
            _cellPitch = cellPitch;
            _carryCellsX = carryCellsX;
            _chestCellsX = chestCellsX;
        }
    }
}
