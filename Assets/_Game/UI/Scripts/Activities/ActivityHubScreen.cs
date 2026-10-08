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
    /// PF_UI_ActivityHub, screen 029 (doc 06; doc « Mini-jeux & activités » sections 1 and 2; doc 01 Contenu sections 0,
    /// A15 and 12.0): the activities as a bottom sheet, the refuge visible above it, closed in one gesture (a touch above the
    /// sheet, Back, the round close). Top to bottom: the title and the ticket chip (« 3/5 », its « + » opens the packs) with
    /// the real time to the next free ticket (« Prochain billet dans 1 h 12 », minutes only, never a ticking countdown, never
    /// red); the activity cards in a horizontal carousel recycled over 8 views (thumbnail, name, best, stars, challenges,
    /// « Nouveau », « Gratuit », locked cards grey and soft with what opens them); the selected activity: its name and
    /// record, its theme pills (one per open biome, shown from two), its 10 challenges (a touch picks one, a second touch
    /// lets go; a locked one says which star opens it); at the thumb the practice toggle (« Entraînement »: free, no reward,
    /// said in a short line) and the big « Jouer » with its cost (a ticket, or « Gratuit » for practice and the free game).
    /// Sections are placed by hand when what they show changes (no layout group, no rebuild per frame); the carousel and
    /// the pills have their own canvases. Closed: canvas disabled, content inactive, nothing ticks. Prewarmed at scene
    /// entry; nothing is instantiated after it. Sounds and choices belong to the presenter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActivityHubScreen : MonoBehaviour, IActivityHubView, IMotionSettings
    {
        public const int CardPool = 8;
        public const int MaxChallenges = 10;
        public const int MaxThemes = 13;
        private const float KnobSeconds = 0.15f;

        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _safeArea;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Button _outside;
        [SerializeField] private Button _close;
        [SerializeField] private TMP_Text _title;

        [Header("Tickets")]
        [SerializeField] private Button _ticketChip;
        [SerializeField] private TMP_Text _ticketCount;
        [SerializeField] private TMP_Text _ticketNext;

        [Header("Cards")]
        [SerializeField] private ScrollRect _cardScroll;
        [SerializeField] private RectTransform _cardViewport;
        [SerializeField] private RectTransform _cardList;
        [SerializeField] private ActivityCardView[] _cards = Array.Empty<ActivityCardView>();

        [Header("Selected activity")]
        [SerializeField] private RectTransform _details;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _best;
        [SerializeField] private RectTransform _themesRow;
        [SerializeField] private ScrollRect _themeScroll;
        [SerializeField] private RectTransform _themeList;
        [SerializeField] private ActivityThemePillView[] _pills = Array.Empty<ActivityThemePillView>();
        [SerializeField] private RectTransform _challengesBlock;
        [SerializeField] private TMP_Text _challengesTitle;
        [SerializeField] private TMP_Text _challengesCount;
        [SerializeField] private ActivityChallengeCellView[] _cells = Array.Empty<ActivityChallengeCellView>();
        [SerializeField] private TMP_Text _goal;

        [Header("Footer")]
        [SerializeField] private Button _practice;
        [SerializeField] private Image _practiceBack;
        [SerializeField] private Image _practiceTrack;
        [SerializeField] private RectTransform _practiceKnob;
        [SerializeField] private TMP_Text _practiceLabel;
        [SerializeField] private TMP_Text _practiceLine;
        [SerializeField] private Button _play;
        [SerializeField] private Image _playBack;
        [SerializeField] private TMP_Text _playLabel;
        [SerializeField] private RectTransform _costRow;
        [SerializeField] private Image _costIcon;
        [SerializeField] private TMP_Text _costText;

        [Header("Art")]
        [Tooltip("Activity icons by ActivityId value (ICO_Act_<Name>); empty slots use the fallback (ICO_Play).")]
        [SerializeField] private Sprite[] _activityIcons = Array.Empty<Sprite>();
        [SerializeField] private Sprite _fallbackIcon;
        [SerializeField] private ActivityThemeSet _themes = new ActivityThemeSet();
        [SerializeField] private Color _gold = new Color(0.949f, 0.710f, 0.267f, 1f);
        [SerializeField] private Color _warm = new Color(1f, 0.945f, 0.855f, 1f);
        [SerializeField] private Color _practiceOn = new Color(0.871f, 0.937f, 0.784f, 1f);

        [Header("Layout (1080 x 2340 reference)")]
        [SerializeField] private float _cardWidth = 340f;
        [SerializeField] private float _cardPitch = 364f;
        [SerializeField] private float _cardPad = 64f;
        [SerializeField] private float _headerHeight = 196f;
        [SerializeField] private float _carouselHeight = 530f;
        [SerializeField] private float _footerHeight = 338f;
        [SerializeField] private float _nameRowHeight = 120f;
        [SerializeField] private float _themesHeight = 112f;
        [SerializeField] private float _challengesHeight = 446f;
        [SerializeField] private float _detailsPadding = 24f;
        [SerializeField] private float _refugeAbove = 200f;
        [SerializeField] private float _pillPitch = 360f;
        [SerializeField] private float _knobTravel = 22f;

        private readonly WindowMotion _motion = new WindowMotion(0.22f, true);
        private readonly SafeAreaFitter _fitter = new SafeAreaFitter();
        private readonly TicketTimeText _ticketTime = new TicketTimeText();
        private ActivityHubColors _colors;
        private IActivityHubModel _model;
        private IActivityHubActions _actions;
        private UiLabel _titleText;
        private UiLabel _countText;
        private UiLabel _nextText;
        private UiLabel _nameText;
        private UiLabel _bestText;
        private UiLabel _challengesTitleText;
        private UiLabel _challengesCountText;
        private UiLabel _goalText;
        private UiLabel _practiceLabelText;
        private UiLabel _practiceLineText;
        private UiLabel _playText;
        private UiLabel _costLabel;
        private string _bestTemplate;
        private string _goalTemplate;
        private string _lockedTemplate;
        private string _pickText;
        private string _themeTemplate;
        private string _freeWord;
        private ILocalization _text;
        private int _cardCount = -1;
        private int _selected = -2;
        private int _challenge = -1;
        private int _lockedTap = -1;
        private int _ticketsShown = -1;
        private int _maxShown = -1;
        private int _bestShown = -1;
        private int _doneShown = -1;
        private int _layoutKey = -1;
        private float _layoutSafeHeight = -1f;
        private int _themeKey = -1;
        private int _footerKey = -1;
        private float _boundScroll = float.NaN;
        private float _boundViewport = float.NaN;
        private float _knob = -1f;
        private float _knobTarget;
        private bool _practiceShown;
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
                foreach (var card in _cards)
                    if (card != null)
                        card.ReduceMotion = value;
            }
        }

        // ---------------- read by tests and captures ----------------

        public Button OutsideButton => _outside;
        public Button CloseButton => _close;
        public Button TicketChip => _ticketChip;
        public Button PracticeButton => _practice;
        public Button PlayButton => _play;
        public ScrollRect CardScroll => _cardScroll;
        public int CardViews => _cards.Length;
        public ActivityCardView CardView(int i) => _cards[i];
        public ActivityChallengeCellView Cell(int i) => _cells[i];
        public ActivityThemePillView Pill(int i) => _pills[i];
        public string TicketCountText => _ticketCount.text;
        public string TicketNextText => _ticketNext.text;
        public Color TicketNextColor => _ticketNext.color;
        public string NameText => _name.text;
        public string BestText => _best.gameObject.activeSelf ? _best.text : string.Empty;
        public string GoalText => _goal.text;
        public string CostText => _costText.text;
        public bool CostTicketShown => _costIcon.gameObject.activeSelf;
        public bool PlayEnabled => _play.interactable;
        public bool PracticeOn => _practiceShown;
        public float KnobPosition => _practiceKnob.anchoredPosition.x;
        public bool ThemesShown => _themesRow.gameObject.activeSelf;
        public bool ChallengesShown => _challengesBlock.gameObject.activeSelf;
        public int PickedChallenge => _challenge;
        public float PanelHeight => _panel.rect.height;

        public int CellsShown
        {
            get
            {
                int n = 0;
                foreach (var cell in _cells)
                    if (cell.gameObject.activeSelf)
                        n++;
                return n;
            }
        }

        public int PillsShown
        {
            get
            {
                int n = 0;
                foreach (var pill in _pills)
                    if (pill.gameObject.activeSelf)
                        n++;
                return n;
            }
        }

        /// <summary>The card view showing activity card <paramref name="card"/>, or null when it is off screen.</summary>
        public ActivityCardView ViewOf(int card)
        {
            foreach (var view in _cards)
                if (view.gameObject.activeSelf && view.Index == card)
                    return view;
            return null;
        }

        /// <summary>Scrolls the carousel so that <paramref name="card"/> sits in the middle when it can (tests, the A15 paw).</summary>
        public void ScrollTo(int card)
        {
            if (_model == null)
                return;
            _cardScroll.StopMovement();
            float viewport = _cardViewport.rect.width;
            // Not laid out yet (the first opening): the reference width, so the first card never starts half hidden.
            if (viewport < 1f)
                viewport = 1080f;
            float content = _cardList.sizeDelta.x;
            float x = _cardPad + card * _cardPitch + _cardWidth * 0.5f - viewport * 0.5f;
            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, content - viewport));
            _cardList.anchoredPosition = new Vector2(-x, _cardList.anchoredPosition.y);
            UpdateCards(false);
        }

        // ---------------- IActivityHubView ----------------

        public void Open(IActivityHubModel model, ILocalization text, IActivityHubActions actions)
        {
            Bind(model, text, actions);
            if (!_motion.IsOpen || _motion.Closing)
                _motion.Show();
            _challenge = -1;
            _lockedTap = -1;
            RefreshGoal();
            ScrollTo(Mathf.Max(0, model.SelectedCard));
            SettleKnob();
            foreach (var card in _cards)
                card.Settle();
        }

        public void Prewarm(IActivityHubModel model, ILocalization text, IActivityHubActions actions)
        {
            if (_motion.IsOpen)
                return;
            Bind(model, text, actions);
            _motion.Show();
            Canvas.ForceUpdateCanvases();
            _motion.HideNow();
        }

        public void Refresh()
        {
            if (_model == null)
                return;
            RefreshTickets();
            RefreshPractice();
            int count = Mathf.Max(0, _model.CardCount);
            if (count != _cardCount)
            {
                _cardCount = count;
                _cardList.sizeDelta = new Vector2(_cardPad * 2f + Mathf.Max(0, count - 1) * _cardPitch + _cardWidth, _cardList.sizeDelta.y);
                UpdateCards(true);
            }
            int selected = _model.SelectedCard;
            if (selected != _selected)
            {
                _selected = selected;
                _challenge = -1;
                _lockedTap = -1;
                _bestShown = -1;
                _doneShown = -1;
            }
            BindVisibleCards();
            RefreshDetails();
        }

        public void Close() => _motion.Hide();

        /// <summary>Android back, or Escape: the same as the round close (the « no ticket » card above takes it first).</summary>
        public void PressBack()
        {
            if (IsOpen && !TicketsEmptyCard.AnyOpen)
                _actions?.CloseActivities();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_motion.IsOpen)
                return;
            if (_motion.BackPressed)
                PressBack();
            _fitter.Fit(_safeArea);
            if (_model != null && !Mathf.Approximately(_safeArea.rect.height, _layoutSafeHeight))
                Layout(_layoutKey & 1, (_layoutKey >> 1) & 1);
            bool closed = _motion.Tick(unscaledDeltaTime);
            UpdateCards(false);
            for (int i = 0; i < _cards.Length; i++)
                _cards[i].Tick(unscaledDeltaTime);
            TickKnob(unscaledDeltaTime);
            if (closed)
            {
                foreach (var card in _cards)
                    card.Settle();
            }
        }

        public Vector2 CardScreenPosition(int card)
        {
            if (!_motion.IsOpen || _model == null || (uint)card >= (uint)Mathf.Max(0, _cardCount))
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            // The list's pivot sits at its left edge, halfway up.
            var local = new Vector3(_cardPad + card * _cardPitch + _cardWidth * 0.5f, 0f, 0f);
            return RectTransformUtility.WorldToScreenPoint(null, _cardList.TransformPoint(local));
        }

        // ---------------- binding ----------------

        private void Bind(IActivityHubModel model, ILocalization text, IActivityHubActions actions)
        {
            _model = model;
            _actions = actions;
            Initialize();
            _fitter.Fit(_safeArea);
            // Texts are read again at each opening (a language change shows at once); labels compare before writing.
            if (text != null)
            {
                _text = text;
                _ticketTime.Bind(text);
                _titleText.SetString(text.Get(ActivityText.HubTitle));
                _challengesTitleText.SetString(text.Get(ActivityText.HubChallenges));
                _practiceLabelText.SetString(text.Get(ActivityText.Practice));
                _practiceLineText.SetString(text.Get(ActivityText.PracticeLine));
                _playText.SetString(text.Get(ActivityText.HubPlay));
                _bestTemplate = text.Get(ActivityText.HubBest);
                _goalTemplate = text.Get(ActivityText.HubChallengeGoal);
                _lockedTemplate = text.Get(ActivityText.HubChallengeLocked);
                _pickText = text.Get(ActivityText.HubChallengePick);
                string themeTemplate = text.Get(ActivityText.HubTheme);
                if (!string.Equals(themeTemplate, _themeTemplate, StringComparison.Ordinal))
                    Array.Clear(_themeFallbacks, 0, _themeFallbacks.Length);
                _themeTemplate = themeTemplate;
                _freeWord = text.Get(ActivityText.HubFree);
                string newWord = text.Get(ActivityText.HubNew);
                foreach (var card in _cards)
                {
                    card.SetTexts(newWord, _freeWord);
                    card.Invalidate();
                }
            }
            _cardCount = -1;
            _selected = -2;
            _ticketsShown = -1;
            _maxShown = -1;
            _themeKey = -1;
            _footerKey = -1;
            _layoutKey = -1;
            _boundScroll = float.NaN;
            Refresh();
        }

        private void RefreshTickets()
        {
            var tickets = _model.Tickets;
            if (tickets.Count != _ticketsShown || tickets.Max != _maxShown)
            {
                _ticketsShown = tickets.Count;
                _maxShown = tickets.Max;
                _countText.SetRatio(tickets.Count, tickets.Max);
            }
            // The line changes only when the minute does: nothing ticks on screen.
            _ticketTime.Write(_nextText, tickets.Count >= tickets.Max ? 0 : Mathf.Max(1, tickets.MinutesToNext));
        }

        private void RefreshPractice()
        {
            bool practice = _model.Practice;
            if (practice == _practiceShown && _knob >= 0f)
                return;
            _practiceShown = practice;
            _knobTarget = practice ? _knobTravel : -_knobTravel;
            _practiceTrack.color = practice ? _tokens.success : _tokens.accentIdle;
            _practiceBack.color = practice ? _practiceOn : _colors.Card;
            if (_knob < 0f || _reduceMotion || !_motion.IsOpen)
                SettleKnob();
            _footerKey = -1;
        }

        private void SettleKnob()
        {
            _knob = 1f;
            _practiceKnob.anchoredPosition = new Vector2(_knobTarget, _practiceKnob.anchoredPosition.y);
        }

        private void TickKnob(float dt)
        {
            float x = _practiceKnob.anchoredPosition.x;
            if (x == _knobTarget)
                return;
            float step = dt * (_knobTravel * 2f) / KnobSeconds;
            _practiceKnob.anchoredPosition = new Vector2(_reduceMotion ? _knobTarget : Mathf.MoveTowards(x, _knobTarget, step),
                _practiceKnob.anchoredPosition.y);
        }

        /// <summary>The carousel's visible window: views are recycled by index modulo the pool; rebinding only on a move.</summary>
        private void UpdateCards(bool force)
        {
            if (_model == null || _cards.Length == 0)
                return;
            float scroll = -_cardList.anchoredPosition.x;
            float viewport = _cardViewport.rect.width;
            if (!force && scroll == _boundScroll && viewport == _boundViewport)
                return;
            _boundScroll = scroll;
            _boundViewport = viewport;
            int count = Mathf.Max(0, _cardCount);
            int first = Mathf.Max(0, Mathf.FloorToInt((scroll - _cardPad) / _cardPitch) - 1);
            int last = Mathf.Min(count - 1, Mathf.FloorToInt((scroll + viewport - _cardPad) / _cardPitch) + 1);
            if (last - first + 1 > _cards.Length)
                last = first + _cards.Length - 1;
            int pool = _cards.Length;
            for (int slot = 0; slot < pool; slot++)
            {
                int index = first + ((slot - first % pool) + pool) % pool;
                var view = _cards[slot];
                bool shown = index <= last && index < count;
                view.SetShown(shown);
                if (!shown)
                    continue;
                if (view.Index != index || force)
                {
                    var rect = view.Rect;
                    rect.anchoredPosition = new Vector2(_cardPad + index * _cardPitch + _cardWidth * 0.5f, rect.anchoredPosition.y);
                    BindCard(view, index);
                }
            }
        }

        private void BindVisibleCards()
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                var view = _cards[i];
                if (view.gameObject.activeSelf && view.Index >= 0 && view.Index < _cardCount)
                    BindCard(view, view.Index);
            }
        }

        private void BindCard(ActivityCardView view, int index)
        {
            var card = _model.Card(index);
            view.Bind(index, card, Icon(card.Activity), index == _model.SelectedCard, _colors);
        }

        private void RefreshDetails()
        {
            int selected = _model.SelectedCard;
            bool any = (uint)selected < (uint)Mathf.Max(0, _cardCount);
            var card = any ? _model.Card(selected) : default;
            bool locked = !any || card.State == ActivityCardState.Locked;
            _nameText.SetString(any ? card.Name : string.Empty);
            int best = locked ? 0 : card.Best;
            if (best != _bestShown)
            {
                _bestShown = best;
                SetActive(_best.gameObject, best > 0);
                if (best > 0)
                    _bestText.SetTemplate(_bestTemplate, best);
            }

            int themes = locked ? 0 : Mathf.Min(_model.ThemeCount, _pills.Length);
            bool showThemes = themes >= 2;
            int challenges = locked ? 0 : Mathf.Min(_model.ChallengeCount, _cells.Length);
            bool showChallenges = challenges > 0;
            int key = (showThemes ? 1 : 0) | (showChallenges ? 2 : 0);
            if (key != _layoutKey || !Mathf.Approximately(_safeArea.rect.height, _layoutSafeHeight))
                Layout(key & 1, (key >> 1) & 1);

            if (showThemes)
                BindThemes(themes);
            int done = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                bool shown = i < challenges;
                _cells[i].SetShown(shown);
                if (!shown)
                    continue;
                var cell = _model.Challenge(i);
                if (!cell.Locked && cell.Stars >= 3)
                    done++;
                if (_challenge == i && cell.Locked)
                    _challenge = -1;
                _cells[i].Bind(cell, i == _challenge, _colors);
            }
            if (_challenge >= challenges)
                _challenge = -1;
            int doneKey = done * 32 + challenges;
            if (showChallenges && doneKey != _doneShown)
            {
                _doneShown = doneKey;
                _challengesCountText.SetRatio(done, challenges);
            }
            RefreshGoal();
            RefreshFooter(any, card, locked);
        }

        private void BindThemes(int themes)
        {
            int chosen = _model.SelectedTheme;
            for (int i = 0; i < _pills.Length; i++)
            {
                bool shown = i < themes;
                _pills[i].SetShown(shown);
                if (!shown)
                    continue;
                var theme = _model.Theme(i);
                int entry = _themes != null ? _themes.Find(theme) : -1;
                string name = null;
                var tint = _themes != null ? _themes.fallbackTint : _colors.CardLocked;
                if (entry >= 0)
                {
                    var e = _themes.entries[entry];
                    tint = e.tint;
                    if (_text != null && !string.IsNullOrEmpty(e.nameKey) && _text.Has(e.nameKey))
                        name = _text.Get(e.nameKey);
                }
                if (name == null)
                    name = ThemeFallback(i);
                _pills[i].Bind(name, tint, i == chosen, _colors);
            }
            int key = themes * 64 + chosen;
            if (key != _themeKey)
            {
                _themeKey = key;
                _themeList.sizeDelta = new Vector2(_cardPad * 2f + themes * _pillPitch, _themeList.sizeDelta.y);
                for (int i = 0; i < themes; i++)
                    _pills[i].Rect.anchoredPosition = new Vector2(_cardPad + i * _pillPitch, _pills[i].Rect.anchoredPosition.y);
            }
        }

        // « Thème {n} » strings are built once per theme index and kept: no allocation on later refreshes.
        private readonly string[] _themeFallbacks = new string[MaxThemes];

        private string ThemeFallback(int index)
        {
            if (index >= _themeFallbacks.Length)
                return string.Empty;
            if (_themeFallbacks[index] == null)
            {
                var buffer = new char[64];
                int length = ActivityChars.Fill(buffer, 0, _themeTemplate, index + 1);
                _themeFallbacks[index] = new string(buffer, 0, length);
            }
            return _themeFallbacks[index];
        }

        private void RefreshGoal()
        {
            if (_model == null || _goalText == null)
                return;
            int challenges = Mathf.Min(_model.ChallengeCount, _cells.Length);
            if (_challenge >= 0 && _challenge < challenges)
            {
                var cell = _model.Challenge(_challenge);
                _goalText.Commit(ActivityChars.Fill(_goalText.Buffer, 0, _goalTemplate, _challenge + 1, 0, cell.Goal));
                _goal.color = _colors.Text;
            }
            else if (_lockedTap > 0 && _lockedTap < challenges)
            {
                // Challenge n + 1 opens with a star on challenge n (they open in order).
                _goalText.Commit(ActivityChars.Fill(_goalText.Buffer, 0, _lockedTemplate, _lockedTap));
                _goal.color = _colors.TextSoft;
            }
            else
            {
                _goalText.SetString(_pickText);
                _goal.color = _colors.TextSoft;
            }
        }

        private void RefreshFooter(bool any, in ActivityCardModel card, bool locked)
        {
            bool free = _model.Practice || (any && card.FreeGame);
            int key = (locked ? 1 : 0) | (free ? 2 : 0) | (_model.Practice ? 4 : 0);
            if (key == _footerKey)
                return;
            _footerKey = key;
            _play.interactable = !locked;
            _playBack.color = locked ? _tokens.accentIdle : _tokens.accentPrimary;
            _playLabel.color = locked ? _colors.TextOff : _colors.Text;
            SetActive(_costRow.gameObject, !locked);
            SetActive(_costIcon.gameObject, !free);
            if (free)
            {
                _costLabel.SetString(_freeWord);
                _costText.color = _tokens.textPrimary;
            }
            else
            {
                _costLabel.SetNumber(1);
                _costText.color = _tokens.textPrimary;
            }
            // Icon and word centered together; only when the cost changes (never per frame).
            if (free)
            {
                _costText.alignment = TextAlignmentOptions.Center;
                _costText.rectTransform.anchorMin = _costText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _costText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _costText.rectTransform.anchoredPosition = new Vector2(0f, _costText.rectTransform.anchoredPosition.y);
            }
            else
            {
                _costText.alignment = TextAlignmentOptions.MidlineLeft;
                UiRow.Center((RectTransform)_costIcon.transform, _costText, 10f);
            }
        }

        /// <summary>
        /// Places the details (name row, themes when shown, challenges when shown) and sizes the sheet to them; the refuge
        /// keeps at least a band above it. Runs when what is shown changes, never per frame.
        /// </summary>
        private void Layout(int themes, int challenges)
        {
            _layoutKey = themes | (challenges << 1);
            float y = -_nameRowHeight;
            SetActive(_themesRow.gameObject, themes == 1);
            if (themes == 1)
            {
                _themesRow.anchoredPosition = new Vector2(_themesRow.anchoredPosition.x, y);
                y -= _themesHeight;
            }
            SetActive(_challengesBlock.gameObject, challenges == 1);
            if (challenges == 1)
            {
                _challengesBlock.anchoredPosition = new Vector2(_challengesBlock.anchoredPosition.x, y);
                y -= _challengesHeight;
            }
            float details = -y + _detailsPadding;
            _details.anchoredPosition = new Vector2(_details.anchoredPosition.x, -(_headerHeight + _carouselHeight));
            _details.sizeDelta = new Vector2(_details.sizeDelta.x, details);

            float safeHeight = _safeArea.rect.height;
            _layoutSafeHeight = safeHeight;
            if (safeHeight < 400f)
                safeHeight = 2340f - 160f;
            float wanted = _headerHeight + _carouselHeight + details + _footerHeight;
            float max = Mathf.Max(800f, safeHeight - _refugeAbove);
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, Mathf.Min(wanted, max));
        }

        private Sprite Icon(ActivityId activity)
        {
            int i = (int)activity;
            if (i >= 0 && i < _activityIcons.Length && _activityIcons[i] != null)
                return _activityIcons[i];
            return _fallbackIcon;
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _motion.Bind(_canvas, _content, _group, _panel);
            _motion.ReduceMotion = _reduceMotion;
            _colors = new ActivityHubColors
            {
                Card = Color.white,
                CardLocked = _tokens.panelSecondary,
                Disc = _warm,
                DiscLocked = new Color(0.925f, 0.902f, 0.863f, 1f),
                IconLocked = new Color(0.86f, 0.84f, 0.8f, 0.5f),
                Selected = _tokens.accentPrimary,
                Featured = _gold,
                StarOn = _gold,
                StarOff = new Color(_tokens.accentIdle.r, _tokens.accentIdle.g, _tokens.accentIdle.b, 1f),
                Text = _tokens.textPrimary,
                TextSoft = _tokens.textSecondary,
                TextOff = _tokens.textDisabled,
                Warm = _warm,
            };
            _titleText = new UiLabel(_title, 64);
            _countText = new UiLabel(_ticketCount, 24);
            _nextText = new UiLabel(_ticketNext, 96);
            _nameText = new UiLabel(_name, 96);
            _bestText = new UiLabel(_best, 64);
            _challengesTitleText = new UiLabel(_challengesTitle, 48);
            _challengesCountText = new UiLabel(_challengesCount, 24);
            _goalText = new UiLabel(_goal, 192);
            _practiceLabelText = new UiLabel(_practiceLabel, 64);
            _practiceLineText = new UiLabel(_practiceLine, 96);
            _playText = new UiLabel(_playLabel, 48);
            _costLabel = new UiLabel(_costText, 48);
            _close.onClick.AddListener(OnClose);
            _outside.onClick.AddListener(OnClose);
            _ticketChip.onClick.AddListener(OnTickets);
            _practice.onClick.AddListener(OnPractice);
            _play.onClick.AddListener(OnPlay);
            for (int i = 0; i < _cards.Length; i++)
                _cards[i].Initialize(OnCard);
            for (int i = 0; i < _cells.Length; i++)
                _cells[i].Initialize(i, OnChallenge);
            for (int i = 0; i < _pills.Length; i++)
                _pills[i].Initialize(i, OnTheme);
            ReduceMotion = _reduceMotion;
        }

        private void OnClose() => _actions?.CloseActivities();
        private void OnTickets() => _actions?.BuyTickets();
        private void OnPractice() => _actions?.SetPractice(_model == null || !_model.Practice);
        private void OnTheme(int theme) => _actions?.SetTheme(theme);

        private void OnCard(int card)
        {
            if (_model == null)
                return;
            _actions?.SelectActivity(card);
        }

        /// <summary>A touch picks the challenge (its goal shows, « Jouer » plays it); a second touch lets go.</summary>
        private void OnChallenge(int challenge)
        {
            if (_model == null || challenge >= _model.ChallengeCount)
                return;
            var cell = _model.Challenge(challenge);
            if (cell.Locked)
            {
                _lockedTap = challenge;
                _challenge = -1;
            }
            else
            {
                _lockedTap = -1;
                _challenge = _challenge == challenge ? -1 : challenge;
            }
            for (int i = 0; i < _cells.Length && i < _model.ChallengeCount; i++)
                _cells[i].Bind(_model.Challenge(i), i == _challenge, _colors);
            RefreshGoal();
        }

        private void OnPlay()
        {
            if (_model == null || !_play.interactable)
                return;
            if (_challenge >= 0)
                _actions?.PlayChallenge(_challenge);
            else
                _actions?.Play();
        }

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
            _ticketChip.onClick.RemoveListener(OnTickets);
            _practice.onClick.RemoveListener(OnPractice);
            _play.onClick.RemoveListener(OnPlay);
        }

        // ---------------- prefab builder hooks (Editor ActivityUiPipeline) ----------------

        public void SetReferences(UiColorTokens tokens, Canvas canvas, GameObject content, CanvasGroup group, RectTransform safeArea, RectTransform panel,
            Button outside, Button close, TMP_Text title, Button ticketChip, TMP_Text ticketCount, TMP_Text ticketNext)
        {
            _tokens = tokens;
            _canvas = canvas;
            _content = content;
            _group = group;
            _safeArea = safeArea;
            _panel = panel;
            _outside = outside;
            _close = close;
            _title = title;
            _ticketChip = ticketChip;
            _ticketCount = ticketCount;
            _ticketNext = ticketNext;
        }

        public void SetCardReferences(ScrollRect scroll, RectTransform viewport, RectTransform list, ActivityCardView[] cards)
        {
            _cardScroll = scroll;
            _cardViewport = viewport;
            _cardList = list;
            _cards = cards;
        }

        public void SetDetailReferences(RectTransform details, TMP_Text name, TMP_Text best, RectTransform themesRow, ScrollRect themeScroll,
            RectTransform themeList, ActivityThemePillView[] pills, RectTransform challengesBlock, TMP_Text challengesTitle, TMP_Text challengesCount,
            ActivityChallengeCellView[] cells, TMP_Text goal)
        {
            _details = details;
            _name = name;
            _best = best;
            _themesRow = themesRow;
            _themeScroll = themeScroll;
            _themeList = themeList;
            _pills = pills;
            _challengesBlock = challengesBlock;
            _challengesTitle = challengesTitle;
            _challengesCount = challengesCount;
            _cells = cells;
            _goal = goal;
        }

        public void SetFooterReferences(Button practice, Image practiceBack, Image practiceTrack, RectTransform practiceKnob, TMP_Text practiceLabel,
            TMP_Text practiceLine, Button play, Image playBack, TMP_Text playLabel, RectTransform costRow, Image costIcon, TMP_Text costText)
        {
            _practice = practice;
            _practiceBack = practiceBack;
            _practiceTrack = practiceTrack;
            _practiceKnob = practiceKnob;
            _practiceLabel = practiceLabel;
            _practiceLine = practiceLine;
            _play = play;
            _playBack = playBack;
            _playLabel = playLabel;
            _costRow = costRow;
            _costIcon = costIcon;
            _costText = costText;
        }

        public void SetArtReferences(Sprite[] activityIcons, Sprite fallbackIcon, ActivityThemeSet themes, Color gold, Color warm, Color practiceOn)
        {
            _activityIcons = activityIcons;
            _fallbackIcon = fallbackIcon;
            _themes = themes;
            _gold = gold;
            _warm = warm;
            _practiceOn = practiceOn;
        }

        public void SetLayout(float cardWidth, float cardPitch, float cardPad, float headerHeight, float carouselHeight, float footerHeight,
            float nameRowHeight, float themesHeight, float challengesHeight, float pillPitch, float knobTravel)
        {
            _cardWidth = cardWidth;
            _cardPitch = cardPitch;
            _cardPad = cardPad;
            _headerHeight = headerHeight;
            _carouselHeight = carouselHeight;
            _footerHeight = footerHeight;
            _nameRowHeight = nameRowHeight;
            _themesHeight = themesHeight;
            _challengesHeight = challengesHeight;
            _pillPitch = pillPitch;
            _knobTravel = knobTravel;
        }
    }
}
