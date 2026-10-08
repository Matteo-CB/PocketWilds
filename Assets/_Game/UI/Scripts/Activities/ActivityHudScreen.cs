using System;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_ActivityHud, screen 030 (doc 06: score, timer, pause, combo; doc « Mini-jeux & activités » section 2; doc 01
    /// Contenu section 12.0): shared by every activity, each activity only adds its own props in the world. At the top: the
    /// score that rolls up to its value with a small bump, on a bar with the three star marks (each lights gold with a soft
    /// pop when reached) and a soft dot at the best; the time left as a soft ring that empties calmly (never red, no beep,
    /// the seconds in a quiet color); the streak in a small pill from ×2; « Entraînement » when nothing is at stake; the
    /// pause button opening a small card (« Reprendre », « Quitter »). Passive except the pause button and the pause card:
    /// touches go to the activity. Per-frame setters only touch a label when its characters change; the score and the
    /// ring have their own canvases. Hidden: canvas disabled, content inactive. Prewarmed; nothing is instantiated after.
    /// <para>T38 (doc « Mini-jeux & activités » section 1: understood without text in 3 s; doc 01 A15 and S-ACT-001), what
    /// an activity's stage shows over its game, each part on its own canvas and inactive while closed: a short tip in a soft
    /// cream bubble under the score card (fade and a tiny bounce, gone after its seconds); the tutorial paw on a passive
    /// full screen layer (a swipe that presses, slides, lifts and fades, leaving a soft trail; a tap or a double tap with a
    /// soft ring; Reduce Motion: a still paw that breathes softly); up to 4 big round choices at the bottom (they bounce in
    /// one after the other; a pick bounces the chosen one, lets the others go and calls the stage once); a small boost chip
    /// under the score with a soft ring that empties (never red, no ticking). Tip and boost timers wait while paused.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActivityHudScreen : MonoBehaviour, IActivityHudView, IMotionSettings
    {
        private const float RollRate = 10f;
        private const float MinRollSpeed = 40f;
        private const float MarkPopSeconds = 0.32f;
        private const float PauseFadeSeconds = 0.12f;

        /// <summary>The bar holds the third star at this share of its width (room to show more than 3 stars' worth).</summary>
        public const float ThirdStarShare = 0.87f;

        // ---------------- T38 layout (safe area units from its top edge; the pipeline builds with them) ----------------

        /// <summary>The tip's top edge: under the score card and the streak and practice row.</summary>
        public const float TipTop = -336f;

        /// <summary>The tip's top edge under the boost chip when the chip sits low (practice: the tag takes the row).</summary>
        public const float TipTopUnderBoost = -444f;

        public const float TipMaxWidth = 980f;
        public const float TipHeight = 104f;

        /// <summary>The tip's label box: the bubble's widest inner room (the longest tip plus 30 % fits it at the Body minimum).</summary>
        public const float TipLabelWidth = 920f;

        /// <summary>Room around the text inside the bubble, each side.</summary>
        public const float TipPadding = 44f;

        public const float TipMinWidth = 280f;

        /// <summary>The boost chip's center: right under the score card, between the streak pill and the practice tag.</summary>
        public const float BoostY = -270f;

        /// <summary>The boost chip's center when the practice tag shows (it would touch it): under the streak row.</summary>
        public const float BoostYPractice = -378f;

        public const float BoostSize = 104f;

        public const int MaxPickerItems = 4;
        public const int TrailDots = 6;

        /// <summary>Default seconds of a tip when the stage gives none (doc 01 A15: 2 s).</summary>
        public const float DefaultTipSeconds = 2f;

        /// <summary>The swipe: appears and presses (0.2 s), slides (0.8 s), lifts and fades (0.3 s), rests, loops.</summary>
        public const float SwipeAppear = 0.2f;
        public const float SwipeSlide = 0.8f;
        public const float SwipeLift = 0.3f;
        public const float SwipePeriod = 1.7f;

        /// <summary>A tap (or a double tap) per 1.1 s.</summary>
        public const float TapPeriod = 1.1f;

        private const float TipFadeIn = 0.2f;
        private const float TipFadeOut = 0.25f;
        private const float TipGlide = 700f;
        private const float GestureFade = 0.2f;
        private const float TrailLife = 0.6f;
        private const float TapDepth = 56f;
        private const float SwipeLean = 6f;
        private const float MaxPathAngle = 30f;

        /// <summary>UI_Gesture_Swipe's pad, the point that follows the path, in its 192 px frame (agent GI): the paw's pivot.</summary>
        public static readonly Vector2 SwipePadPivot = new Vector2(0.642f, 0.447f);
        private const float PickerStagger = 0.08f;
        private const float PickerFade = 0.18f;
        private const float PickFadeDelay = 0.5f;
        private const float BoostFadeIn = 0.15f;
        private const float BoostFadeOut = 0.25f;

        /// <summary>The tapping paw floats above its point (never hiding it) and taps down toward it, like the A12 paw.</summary>
        private static readonly Vector2 TapRest = new Vector2(0f, 130f);

        private static readonly ActivityPickerItem[] PrewarmItems = new ActivityPickerItem[MaxPickerItems];

        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _safeArea;

        [Header("Score")]
        [SerializeField] private RectTransform _scoreRoot;
        [SerializeField] private TMP_Text _score;
        [SerializeField] private RectTransform _bar;
        [SerializeField] private RectTransform _fill;
        [SerializeField] private RectTransform[] _marks = Array.Empty<RectTransform>();
        [SerializeField] private Image[] _markImages = Array.Empty<Image>();
        [SerializeField] private RectTransform _bestDot;

        [Header("Time")]
        [SerializeField] private RectTransform _timeRoot;
        [SerializeField] private Image _timeFill;
        [SerializeField] private TMP_Text _seconds;

        [Header("Streak and practice")]
        [SerializeField] private RectTransform _streakRoot;
        [SerializeField] private TMP_Text _streak;
        [SerializeField] private GameObject _practiceTag;
        [SerializeField] private TMP_Text _practiceLabel;

        [Header("Pause")]
        [SerializeField] private Button _pause;
        [SerializeField] private GameObject _pauseLayer;
        [SerializeField] private CanvasGroup _pauseGroup;
        [SerializeField] private RectTransform _pauseCard;
        [SerializeField] private TMP_Text _pauseTitle;
        [SerializeField] private Button _resume;
        [SerializeField] private TMP_Text _resumeLabel;
        [SerializeField] private Button _quit;
        [SerializeField] private TMP_Text _quitLabel;

        [Header("Colors")]
        [SerializeField] private Color _gold = new Color(0.949f, 0.710f, 0.267f, 1f);
        [SerializeField] private Color _markOff = new Color(0.89f, 0.86f, 0.81f, 1f);

        [Header("T38 tip")]
        [SerializeField] private RectTransform _tipRoot;
        [SerializeField] private CanvasGroup _tipGroup;
        [SerializeField] private RectTransform _tipBubble;
        [SerializeField] private TMP_Text _tip;

        [Header("T38 gesture")]
        [SerializeField] private RectTransform _gestureRoot;
        [SerializeField] private CanvasGroup _gestureGroup;
        [SerializeField] private RectTransform _paw;
        [SerializeField] private Image _pawImage;
        [SerializeField] private Image[] _rings = Array.Empty<Image>();
        [SerializeField] private Image[] _trail = Array.Empty<Image>();
        /// <summary>The activity icons lent to stages by name (<see cref="Icon"/>): the bows, the golden brush.</summary>
        [SerializeField] private Sprite[] _icons = System.Array.Empty<Sprite>();

        [SerializeField] private Sprite _swipeSprite;
        [SerializeField] private Sprite _tapSprite;
        [SerializeField] private Vector2 _swipePivot = new Vector2(0.642f, 0.447f);

        [Header("T38 picker")]
        [SerializeField] private RectTransform _pickerRoot;
        [SerializeField] private RectTransform[] _pickerItems = Array.Empty<RectTransform>();
        [SerializeField] private CanvasGroup[] _pickerGroups = Array.Empty<CanvasGroup>();
        [SerializeField] private Button[] _pickerButtons = Array.Empty<Button>();
        [SerializeField] private Image[] _pickerIcons = Array.Empty<Image>();
        [SerializeField] private float _pickerPitch = 216f;
        [SerializeField] private Sprite _pickerFallback;

        [Header("T38 boost")]
        [SerializeField] private RectTransform _boostRoot;
        [SerializeField] private CanvasGroup _boostGroup;
        [SerializeField] private Image _boostIcon;
        [SerializeField] private Image _boostRing;
        [SerializeField] private Sprite _boostFallback;

        private readonly WindowMotion _motion = new WindowMotion(0.15f);
        private readonly SafeAreaFitter _fitter = new SafeAreaFitter();
        private readonly float[] _thresholds = new float[3];
        private readonly float[] _markTime = { -1f, -1f, -1f };
        private readonly bool[] _markLit = new bool[3];
        private ActivityHudModel _model;
        private IActivityHudActions _actions;
        private UiLabel _scoreText;
        private UiLabel _secondsText;
        private UiLabel _streakText;
        private UiLabel _practiceText;
        private UiLabel _pauseTitleText;
        private UiLabel _resumeText;
        private UiLabel _quitText;
        private string _streakTemplate;
        private ILocalization _text;
        private float _barMax = 1f;
        private float _shown;
        private int _target;
        private int _labelShown = -1;
        private float _bump = -1f;
        private int _secondsShown = -1;
        private float _fillShown = -1f;
        private int _streakShown = -1;
        private float _streakPop = -1f;
        private bool _paused;
        private float _pauseAlpha;
        private bool _reduceMotion;
        private bool _initialized;
        private bool _practice;

        // T38 tip.
        private UiLabel _tipText;
        private bool _tipOn;
        private bool _tipHiding;
        private float _tipLeft;
        private float _tipAlpha;
        private float _tipPop = -1f;
        private float _tipY;

        // T38 gesture.
        private ActivityGesture _gesture;
        private bool _gestureOn;
        private bool _gestureHiding;
        private float _gestureTime;
        private float _gestureAlpha;
        private int _gestureCycles;
        private Vector2 _from;
        private Vector2 _to;
        private Vector2 _pawPoint;
        private float _lean;
        private float _pathAngle;
        private float _pawFlip = 1f;
        private float[] _trailBorn = Array.Empty<float>();

        // T38 picker.
        private UnityAction[] _pickHandlers = Array.Empty<UnityAction>();
        private float[] _itemAlpha = Array.Empty<float>();
        private float[] _itemScale = Array.Empty<float>();
        private IActivityPickerActions _pickerActions;
        private int _pickerCount;
        private int _picked = -1;
        private bool _pickerOn;
        private bool _pickerOpen;
        private bool _pickerHiding;
        private float _pickerTime;
        private float _pickerEndAt;

        // T38 boost.
        private bool _boostOn;
        private bool _boostHiding;
        private float _boostTotal = 1f;
        private float _boostLeft;
        private float _boostTime;
        private float _boostAlpha;
        private float _boostFillShown = -1f;

        public bool IsShown => _motion.IsOpen && !_motion.Closing;
        public bool Visible => _motion.IsOpen;

        public bool ReduceMotion
        {
            get => _reduceMotion;
            set
            {
                _reduceMotion = value;
                _motion.ReduceMotion = value;
            }
        }

        // ---------------- read by tests and captures ----------------

        public Button PauseButton => _pause;
        public Button ResumeButton => _resume;
        public Button QuitButton => _quit;
        public string ScoreText => _score.text;
        public string SecondsText => _seconds.text;
        public string StreakText => _streak.text;
        public bool StreakShown => _streakRoot.gameObject.activeSelf;
        public bool TimeShown => _timeRoot.gameObject.activeSelf;
        public float TimeFill => _timeFill.fillAmount;
        public Color TimeColor => _timeFill.color;
        public bool PracticeShown => _practiceTag.activeSelf;
        public bool PauseCardShown => _pauseLayer.activeSelf;
        public bool Paused => _paused;
        public float ScoreShown => _shown;
        public bool Rolling => _labelShown != _target;
        public bool MarkLit(int star) => _markLit[star];
        public float MarkX(int star) => _marks[star].anchoredPosition.x;
        public float FillWidth => _fill.sizeDelta.x;
        public float BarWidth => _bar.rect.width;

        // T38 parts, read by tests and captures.
        public bool HasT38Parts => _tipRoot != null && _gestureRoot != null && _pickerRoot != null && _boostRoot != null;
        public RectTransform ScoreRect => _scoreRoot;
        public bool TipShown => _tipOn && !_tipHiding;
        public bool TipVisible => _tipOn;
        public string TipText => _tip != null ? _tip.text : string.Empty;
        public float TipAlpha => _tipGroup != null ? _tipGroup.alpha : 0f;
        public RectTransform TipBubble => _tipBubble;
        public float TipBubbleWidth => _tipBubble != null ? _tipBubble.sizeDelta.x : 0f;
        public float TipScale => _tipRoot != null ? _tipRoot.localScale.y : 1f;
        public bool GestureVisible => _gestureOn;
        public ActivityGesture GestureKind => _gesture;
        public int GestureCycles => _gestureCycles;
        public Sprite PawSprite => _pawImage != null ? _pawImage.sprite : null;
        public Vector2 PawPosition => _paw != null ? _paw.anchoredPosition : Vector2.zero;
        public float PawScale => _paw != null ? _paw.localScale.y : 1f;
        public float PawAlpha => _pawImage != null && _gestureGroup != null ? _pawImage.color.a * _gestureGroup.alpha : 0f;

        /// <summary>The screen point (pixels) the paw shows right now: the brushed point of a swipe, the tapped point.</summary>
        public Vector2 GesturePoint => _gestureRoot != null ? RectTransformUtility.WorldToScreenPoint(UiCamera(), _gestureRoot.TransformPoint(_pawPoint)) : Vector2.zero;

        /// <summary>The paw sprite's center on screen (pixels): above its point for a tap, a little above the fur for a swipe.</summary>
        public Vector2 PawScreenPosition => _paw != null ? RectTransformUtility.WorldToScreenPoint(UiCamera(), _paw.position) : Vector2.zero;

        public float PawAngle => _paw != null ? Mathf.DeltaAngle(0f, _paw.localEulerAngles.z) : 0f;
        public float PawFlip => _paw != null ? Mathf.Sign(_paw.localScale.x) : 1f;
        public int TrailShown => CountShown(_trail);
        public int RingsShown => CountShown(_rings);
        public RectTransform StreakRect => _streakRoot;
        public RectTransform PracticeRect => _practiceTag != null ? (RectTransform)_practiceTag.transform : null;
        public Button PickerButton(int item) => _pickerButtons[item];
        public bool PickerItemVisible(int item) => _pickerItems[item].gameObject.activeInHierarchy && _pickerGroups[item].alpha > 0.01f;
        public float PickerItemScale(int item) => _pickerItems[item].localScale.x;
        public Sprite PickerIconShown(int item) => _pickerIcons[item].sprite;
        public Color PickerTintShown(int item) => _pickerIcons[item].color;
        public bool PickerVisible => _pickerOn;
        public bool BoostShown => _boostOn && !_boostHiding;
        public bool BoostVisible => _boostOn;
        public float BoostFill => _boostRing != null ? _boostRing.fillAmount : 0f;
        public Color BoostRingColor => _boostRing != null ? _boostRing.color : Color.clear;
        public Sprite BoostIconShown => _boostIcon != null ? _boostIcon.sprite : null;
        public RectTransform BoostRect => _boostRoot;

        // ---------------- IActivityHudView ----------------

        public void Show(in ActivityHudModel model, ILocalization text, IActivityHudActions actions)
        {
            Initialize();
            _model = model;
            _actions = actions;
            BindTexts(text);
            _fitter.Fit(_safeArea);
            // The bar: the third star at 87 % of its width; marks at their thresholds.
            int star3 = Mathf.Max(model.Star3, Mathf.Max(model.Star2, model.Star1));
            _barMax = star3 > 0 ? star3 / ThirdStarShare : 1f;
            _thresholds[0] = model.Star1;
            _thresholds[1] = model.Star2;
            _thresholds[2] = model.Star3;
            float width = _bar.rect.width;
            for (int i = 0; i < _marks.Length && i < 3; i++)
            {
                bool shown = star3 > 0 && _thresholds[i] > 0f;
                if (_marks[i].gameObject.activeSelf != shown)
                    _marks[i].gameObject.SetActive(shown);
                _marks[i].anchoredPosition = new Vector2(width * Mathf.Clamp01(_thresholds[i] / _barMax), _marks[i].anchoredPosition.y);
                _marks[i].localScale = Vector3.one;
                _markImages[i].color = _markOff;
                _markLit[i] = false;
                _markTime[i] = -1f;
            }
            bool best = model.Best > 0 && star3 > 0;
            SetActive(_bestDot.gameObject, best);
            if (best)
                _bestDot.anchoredPosition = new Vector2(width * Mathf.Clamp01(model.Best / _barMax), _bestDot.anchoredPosition.y);

            _target = 0;
            _shown = 0f;
            _labelShown = -1;
            _bump = -1f;
            _fillShown = -1f;
            _fillShownBar = -1f;
            ApplyScore();
            _scoreRoot.localScale = Vector3.one;

            bool timed = model.Duration > 0f;
            SetActive(_timeRoot.gameObject, timed);
            _secondsShown = -1;
            if (timed)
                SetTimeLeft(model.Duration);
            _streakShown = -1;
            _streakPop = -1f;
            SetActive(_streakRoot.gameObject, false);
            _practice = model.Mode == ActivityMode.Practice;
            SetActive(_practiceTag, _practice);
            _paused = false;
            _pauseAlpha = 0f;
            SetActive(_pauseLayer, false);
            if (!_motion.IsOpen || _motion.Closing)
            {
                // A new game starts clean (the stage shows its tips and paws after this).
                ResetParts();
                _motion.Show();
            }
            else
            {
                PlaceBoost();
            }
        }

        public void SetScore(int score)
        {
            if (score == _target)
                return;
            if (score > _target && !_reduceMotion)
                _bump = 0f;
            _target = score;
            if (_reduceMotion || !_motion.IsOpen)
            {
                _shown = score;
                ApplyScore();
            }
        }

        public void SetTimeLeft(float seconds)
        {
            if (_model.Duration <= 0f)
                return;
            float fill = Mathf.Clamp01(seconds / _model.Duration);
            // A visible step is a few hundredths of a turn: smaller changes would rebuild the ring for nothing.
            if (Mathf.Abs(fill - _fillShown) >= 0.002f || (fill <= 0f && _fillShown > 0f))
            {
                _fillShown = fill;
                _timeFill.fillAmount = fill;
            }
            int whole = Mathf.Max(0, Mathf.CeilToInt(seconds - 0.001f));
            if (whole != _secondsShown)
            {
                _secondsShown = whole;
                _secondsText.SetNumber(whole);
            }
        }

        public void SetStreak(int streak)
        {
            if (streak == _streakShown)
                return;
            bool grew = streak > _streakShown && _streakShown >= 0;
            _streakShown = streak;
            bool shown = streak >= 2;
            SetActive(_streakRoot.gameObject, shown);
            if (!shown)
                return;
            _streakText.Commit(ActivityChars.Fill(_streakText.Buffer, 0, _streakTemplate, streak));
            if (grew && !_reduceMotion)
                _streakPop = 0f;
        }

        public void SetPaused(bool paused)
        {
            if (paused == _paused)
                return;
            _paused = paused;
            if (paused)
                SetActive(_pauseLayer, true);
            if (_reduceMotion)
            {
                _pauseAlpha = paused ? 1f : 0f;
                ApplyPause();
            }
        }

        public void Hide()
        {
            _motion.Hide();
            _paused = false;
            // The parts fade with the whole HUD and are put away when it is closed.
            HideTip();
            HideGesture();
            HidePicker();
            HideBoost();
        }

        // ---------------- T38: tip (doc 01 A15: « Brosse le pelage » 2 s, then « Enlève les feuilles ») ----------------

        /// <summary>
        /// A short tip in the soft cream bubble under the score card, for <paramref name="seconds"/> (2 s when 0 or less;
        /// paused time does not count). A new tip replaces the one shown with the same tiny bounce. The bubble hugs its text.
        /// </summary>
        public void ShowTip(string text, float seconds)
        {
            Initialize();
            if (_tipRoot == null || _tip == null)
                return;
            if (string.IsNullOrEmpty(text))
            {
                HideTip();
                return;
            }
            _tipText.SetString(text);
            // The bubble's width from the text at its largest size (the label shrinks inside the widest bubble when needed).
            float width = _tip.GetPreferredValues(text, float.PositiveInfinity, float.PositiveInfinity).x + TipPadding * 2f;
            if (_tipBubble != null)
                _tipBubble.sizeDelta = new Vector2(Mathf.Clamp(width, TipMinWidth, TipMaxWidth), _tipBubble.sizeDelta.y);
            _tipLeft = seconds > 0f ? seconds : DefaultTipSeconds;
            // A tip replacing one already shown only nudges (the pop from its rise); a new one bounces in from 0.90.
            bool shown = _tipOn && !_tipHiding && _tipAlpha > 0.5f;
            _tipPop = _reduceMotion ? -1f : shown ? 0.1f : 0f;
            _tipHiding = false;
            if (!_tipOn)
            {
                _tipOn = true;
                _tipAlpha = 0f;
                _tipY = TipTarget();
                _tipRoot.gameObject.SetActive(true);
            }
            ApplyTip();
        }

        public void HideTip()
        {
            if (_tipOn)
                _tipHiding = true;
        }

        // ---------------- T38: tutorial paw (doc Mini-jeux section 1: understood without text in 3 s) ----------------

        /// <summary>
        /// The tutorial paw on its passive layer above the play area, in screen pixels: a swipe from <paramref name="from"/>
        /// to <paramref name="to"/> (UI_Gesture_Swipe), a tap or a double tap on <paramref name="from"/> (UI_Gesture_Tap). It
        /// loops until hidden; the same call again only moves it.
        /// </summary>
        public void ShowGesture(ActivityGesture gesture, Vector2 from, Vector2 to)
        {
            Initialize();
            if (_gestureRoot == null || _paw == null)
                return;
            if (gesture == ActivityGesture.None)
            {
                HideGesture();
                return;
            }
            var a = ToLocal(from);
            var b = gesture == ActivityGesture.Swipe ? ToLocal(to) : a;
            bool restart = !_gestureOn || _gestureHiding || gesture != _gesture;
            _gesture = gesture;
            _from = a;
            _to = b;
            _gestureHiding = false;
            bool swipe = gesture == ActivityGesture.Swipe;
            bool drawnSwipe = swipe && _swipeSprite != null;
            var sprite = drawnSwipe ? _swipeSprite : _tapSprite;
            if (sprite != null && _pawImage.sprite != sprite)
                _pawImage.sprite = sprite;
            // UI_Gesture_Swipe is drawn sliding right with its pad at its pivot: the pad follows the path, a leftward slide
            // flips it (never a half turn), a slope tilts it (30° at most). The tap paw turns around its center, upright.
            var pivot = drawnSwipe ? _swipePivot : new Vector2(0.5f, 0.5f);
            if (_paw.pivot != pivot)
                _paw.pivot = pivot;
            var way = b - a;
            _pawFlip = drawnSwipe && way.x < 0f ? -1f : 1f;
            _pathAngle = swipe ? PathAngle(way) : 0f;
            _lean = way.x >= 0f ? -SwipeLean : SwipeLean;
            for (int i = 0; i < _trail.Length; i++)
                _trail[i].rectTransform.anchoredPosition = Vector2.Lerp(a, b, (i + 0.5f) / _trail.Length);
            for (int i = 0; i < _rings.Length; i++)
                _rings[i].rectTransform.anchoredPosition = a;
            if (restart)
            {
                _gestureTime = 0f;
                _gestureCycles = 0;
                ResetTrail();
                // The pooled dots (a swipe) or rings (a tap) stay active for the whole gesture, drawn through their alpha.
                for (int i = 0; i < _trail.Length; i++)
                {
                    SetAlpha(_trail[i], 0f);
                    SetActive(_trail[i].gameObject, swipe);
                }
                for (int i = 0; i < _rings.Length; i++)
                {
                    SetAlpha(_rings[i], 0f);
                    SetActive(_rings[i].gameObject, !swipe);
                }
            }
            if (!_gestureOn)
            {
                _gestureOn = true;
                _gestureAlpha = 0f;
                _gestureGroup.alpha = 0f;
                _gestureRoot.gameObject.SetActive(true);
            }
            PoseGesture();
        }

        public void HideGesture()
        {
            if (_gestureOn)
                _gestureHiding = true;
        }

        public bool GestureShown => _gestureOn && !_gestureHiding;

        // ---------------- T38: picker (the three grooming bows) ----------------

        /// <summary>
        /// Up to 4 big round choices at the bottom of the play area, each with its icon tinted by its tint (a clear tint
        /// keeps the icon's colors), bouncing in one after the other. A pick bounces the chosen one, lets the others go and
        /// calls <see cref="IActivityPickerActions.Pick"/> once. The array is read now and not kept.
        /// </summary>
        public void ShowPicker(ActivityPickerItem[] items, int count, IActivityPickerActions actions)
        {
            Initialize();
            if (_pickerRoot == null)
                return;
            int n = items == null ? 0 : Mathf.Min(count, Mathf.Min(items.Length, _pickerItems.Length));
            if (n <= 0)
            {
                HidePicker();
                return;
            }
            _pickerActions = actions;
            _pickerCount = n;
            _picked = -1;
            _pickerOpen = true;
            _pickerHiding = false;
            _pickerTime = 0f;
            float start = -(n - 1) * 0.5f * _pickerPitch;
            for (int i = 0; i < _pickerItems.Length; i++)
            {
                bool on = i < n;
                SetActive(_pickerItems[i].gameObject, on);
                if (!on)
                    continue;
                var icon = items[i].Icon != null ? items[i].Icon : _pickerFallback;
                if (_pickerIcons[i].sprite != icon)
                    _pickerIcons[i].sprite = icon;
                var tint = items[i].Tint;
                _pickerIcons[i].color = tint.a <= 0f ? Color.white : tint;
                _pickerItems[i].anchoredPosition = new Vector2(start + i * _pickerPitch, _pickerItems[i].anchoredPosition.y);
                _itemAlpha[i] = 0f;
                _itemScale[i] = _reduceMotion ? 1f : 0f;
                _pickerGroups[i].alpha = 0f;
                _pickerItems[i].localScale = new Vector3(_itemScale[i], _itemScale[i], 1f);
                _pickerGroups[i].interactable = true;
                _pickerGroups[i].blocksRaycasts = true;
            }
            if (!_pickerOn)
            {
                _pickerOn = true;
                _pickerRoot.gameObject.SetActive(true);
            }
            PosePicker(0f);
        }

        public void HidePicker()
        {
            if (!_pickerOn || _pickerHiding)
                return;
            _pickerOpen = false;
            _pickerActions = null;
            if (_picked < 0)
            {
                _pickerHiding = true;
                _pickerEndAt = _pickerTime;
            }
            StopTouches();
        }

        public bool PickerShown => _pickerOpen;

        // ---------------- T38: boost chip (the golden brush) ----------------

        /// <summary>
        /// The small chip under the score card with <paramref name="icon"/> and a soft gold ring that empties over
        /// <paramref name="seconds"/> (paused time does not count), then fades. Never red, no ticking.
        /// </summary>
        public void ShowBoost(Sprite icon, float seconds)
        {
            Initialize();
            if (_boostRoot == null || _boostRing == null)
                return;
            if (seconds <= 0f)
            {
                HideBoost();
                return;
            }
            var sprite = icon != null ? icon : _boostFallback;
            if (_boostIcon != null && sprite != null && _boostIcon.sprite != sprite)
                _boostIcon.sprite = sprite;
            _boostTotal = seconds;
            _boostLeft = seconds;
            _boostHiding = false;
            _boostFillShown = -1f;
            SetBoostFill(1f);
            if (!_boostOn)
            {
                _boostOn = true;
                _boostTime = 0f;
                _boostAlpha = 0f;
                _boostGroup.alpha = 0f;
                _boostRoot.localScale = _reduceMotion ? Vector3.one : new Vector3(0.6f, 0.6f, 1f);
                _boostRoot.gameObject.SetActive(true);
            }
            PlaceBoost();
        }

        public void HideBoost()
        {
            if (_boostOn)
                _boostHiding = true;
        }

        public void Prewarm(ILocalization text)
        {
            if (_motion.IsOpen)
                return;
            Show(new ActivityHudModel { Activity = ActivityId.Test, Duration = 45f, Star1 = 200, Star2 = 380, Star3 = 520, Best = 300 }, text, null);
            SetStreak(3);
            SetPaused(true);
            _pauseAlpha = 1f;
            ApplyPause();
            PrewarmParts(text);
            Canvas.ForceUpdateCanvases();
            SetPaused(false);
            _pauseAlpha = 0f;
            ApplyPause();
            ResetParts();
            _motion.HideNow();
        }

        /// <summary>Android back, or Escape: pauses the game, or resumes it from the pause card.</summary>
        public void PressBack()
        {
            if (!IsShown)
                return;
            if (_paused)
                _actions?.ResumeActivity();
            else
                _actions?.PauseActivity();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_motion.IsOpen)
                return;
            if (_motion.BackPressed)
                PressBack();
            _fitter.Fit(_safeArea);
            bool closed = _motion.Tick(unscaledDeltaTime);
            if (closed)
            {
                SetActive(_pauseLayer, false);
                _pauseAlpha = 0f;
                ResetParts();
                return;
            }
            TickScore(unscaledDeltaTime);
            TickMarks(unscaledDeltaTime);
            TickStreak(unscaledDeltaTime);
            TickPause(unscaledDeltaTime);
            // T38 parts: each returns at once while closed.
            TickBoost(unscaledDeltaTime);
            TickTip(unscaledDeltaTime);
            TickGesture(unscaledDeltaTime);
            TickPicker(unscaledDeltaTime);
        }

        // ---------------- ticking ----------------

        private void TickScore(float dt)
        {
            if (_shown != _target)
            {
                float diff = _target - _shown;
                float step = Mathf.Max(MinRollSpeed, Mathf.Abs(diff) * RollRate) * dt;
                _shown = Mathf.Abs(diff) <= step ? _target : _shown + Mathf.Sign(diff) * step;
                ApplyScore();
            }
            if (_bump >= 0f)
            {
                _bump += dt;
                float s = ActivityCurves.Bump(_bump);
                _scoreRoot.localScale = new Vector3(s, s, 1f);
                if (_bump >= 0.18f)
                {
                    _bump = -1f;
                    _scoreRoot.localScale = Vector3.one;
                }
            }
        }

        private void ApplyScore()
        {
            int whole = Mathf.RoundToInt(_shown);
            if (_shown == _target)
                whole = _target;
            if (whole != _labelShown)
            {
                _labelShown = whole;
                _scoreText.SetNumber(whole);
            }
            float width = _bar.rect.width;
            float fill = Mathf.Clamp01(_shown / _barMax);
            float height = _bar.rect.height;
            bool visible = fill > 0.001f;
            if (_fill.gameObject.activeSelf != visible)
                _fill.gameObject.SetActive(visible);
            if (visible && Mathf.Abs(fill - _fillShownBar) > 0.0005f)
            {
                _fillShownBar = fill;
                _fill.sizeDelta = new Vector2(Mathf.Max(height, width * fill), _fill.sizeDelta.y);
            }
            for (int i = 0; i < 3 && i < _markImages.Length; i++)
            {
                if (_markLit[i] || _thresholds[i] <= 0f || _shown < _thresholds[i])
                    continue;
                _markLit[i] = true;
                _markImages[i].color = _gold;
                if (!_reduceMotion)
                    _markTime[i] = 0f;
            }
        }

        private float _fillShownBar = -1f;

        private void TickMarks(float dt)
        {
            for (int i = 0; i < 3 && i < _marks.Length; i++)
            {
                if (_markTime[i] < 0f)
                    continue;
                _markTime[i] += dt;
                float s = ActivityCurves.SoftPop(_markTime[i]) * 1.15f;
                if (_markTime[i] >= MarkPopSeconds)
                {
                    _markTime[i] = -1f;
                    s = 1f;
                }
                _marks[i].localScale = new Vector3(s, s, 1f);
            }
        }

        private void TickStreak(float dt)
        {
            if (_streakPop < 0f)
                return;
            _streakPop += dt;
            float s = 1f + 0.12f * Mathf.Sin(Mathf.Clamp01(_streakPop / 0.22f) * Mathf.PI);
            if (_streakPop >= 0.22f)
            {
                _streakPop = -1f;
                s = 1f;
            }
            _streakRoot.localScale = new Vector3(s, s, 1f);
        }

        private void TickPause(float dt)
        {
            float target = _paused ? 1f : 0f;
            if (_pauseAlpha == target)
                return;
            _pauseAlpha = _reduceMotion ? target : Mathf.MoveTowards(_pauseAlpha, target, dt / PauseFadeSeconds);
            ApplyPause();
        }

        private void ApplyPause()
        {
            if (_pauseGroup != null)
                _pauseGroup.alpha = _pauseAlpha;
            if (_pauseCard != null)
            {
                float s = _reduceMotion ? 1f : Mathf.Lerp(0.94f, 1f, UiCurves.EaseOut(_pauseAlpha));
                _pauseCard.localScale = new Vector3(s, s, 1f);
            }
            if (_pauseAlpha <= 0f && !_paused)
                SetActive(_pauseLayer, false);
        }

        // ---------------- T38 ticking ----------------

        private void TickTip(float dt)
        {
            if (!_tipOn)
                return;
            if (!_tipHiding && !_paused)
            {
                _tipLeft -= dt;
                if (_tipLeft <= 0f)
                    _tipHiding = true;
            }
            float target = _tipHiding ? 0f : 1f;
            if (_tipAlpha != target)
                _tipAlpha = Mathf.MoveTowards(_tipAlpha, target, dt / (target > _tipAlpha ? TipFadeIn : TipFadeOut));
            if (_tipPop >= 0f)
            {
                _tipPop += dt;
                if (_tipPop >= UiCurves.PopSeconds)
                    _tipPop = -1f;
            }
            // The bubble glides down when the boost chip takes the place above it (practice), and back up after.
            float y = TipTarget();
            if (_tipY != y)
                _tipY = _reduceMotion ? y : Mathf.MoveTowards(_tipY, y, dt * TipGlide);
            if (_tipHiding && _tipAlpha <= 0f)
            {
                StopTip();
                return;
            }
            ApplyTip();
        }

        private void ApplyTip()
        {
            if (_tipGroup != null && _tipGroup.alpha != _tipAlpha)
                _tipGroup.alpha = _tipAlpha;
            float s = 1f;
            if (!_reduceMotion)
            {
                // A tiny bounce in (0.90 to 1.01 and back to 1), a slight settle out.
                if (_tipPop >= 0f)
                    s = 0.9f + 0.1f * UiCurves.Pop(_tipPop);
                else if (_tipHiding)
                    s = Mathf.Lerp(0.96f, 1f, _tipAlpha);
            }
            if (_tipRoot.localScale.x != s)
                _tipRoot.localScale = new Vector3(s, s, 1f);
            var position = _tipRoot.anchoredPosition;
            if (position.y != _tipY)
                _tipRoot.anchoredPosition = new Vector2(position.x, _tipY);
        }

        private float TipTarget() => _practice && _boostOn && !_boostHiding ? TipTopUnderBoost : TipTop;

        private void TickGesture(float dt)
        {
            if (!_gestureOn)
                return;
            float target = _gestureHiding ? 0f : 1f;
            if (_gestureAlpha != target)
            {
                _gestureAlpha = Mathf.MoveTowards(_gestureAlpha, target, dt / GestureFade);
                _gestureGroup.alpha = _gestureAlpha;
            }
            if (_gestureHiding && _gestureAlpha <= 0f)
            {
                StopGesture();
                return;
            }
            _gestureTime += dt;
            PoseGesture();
        }

        private void PoseGesture()
        {
            if (_reduceMotion)
                PoseStill();
            else if (_gesture == ActivityGesture.Swipe)
                PoseSwipe();
            else
                PoseTap(_gesture == ActivityGesture.DoubleTap);
        }

        /// <summary>
        /// The swipe (UI_Gesture_Swipe): the paw comes down onto the fur and presses (0.2 s), slides from the first point
        /// to the second with a soft lean (0.8 s, eased like the A12 paw), lifts and fades (0.3 s), rests, and loops. Round
        /// dots stay a moment where it brushed.
        /// </summary>
        private void PoseSwipe()
        {
            while (_gestureTime >= SwipePeriod)
            {
                _gestureTime -= SwipePeriod;
                _gestureCycles++;
                ResetTrail();
            }
            float t = _gestureTime;
            float k;
            float press;
            float lift;
            float alpha;
            if (t < SwipeAppear)
            {
                float a = UiCurves.Smooth(t / SwipeAppear);
                k = 0f;
                press = a;
                lift = 0f;
                alpha = a;
            }
            else if (t < SwipeAppear + SwipeSlide)
            {
                k = UiCurves.Smooth((t - SwipeAppear) / SwipeSlide);
                press = 1f;
                lift = 0f;
                alpha = 1f;
            }
            else if (t < SwipeAppear + SwipeSlide + SwipeLift)
            {
                float a = UiCurves.Smooth((t - SwipeAppear - SwipeSlide) / SwipeLift);
                k = 1f;
                press = 1f - a;
                lift = a;
                alpha = 1f - a;
            }
            else
            {
                k = 1f;
                press = 0f;
                lift = 1f;
                alpha = 0f;
            }
            _pawPoint = Vector2.LerpUnclamped(_from, _to, k);
            // The pad (the paw's pivot) follows the path: hovering a little before the press and as it lifts, pressed
            // into the fur while it slides, tilted with the path and leaning softly as it goes.
            float hover = (1f - press) * 26f * (1f - lift) + lift * 34f;
            _paw.anchoredPosition = _pawPoint + new Vector2(0f, hover);
            float s = 1.04f - 0.1f * press;
            _paw.localScale = new Vector3(s * _pawFlip, s, 1f);
            _paw.localRotation = Quaternion.Euler(0f, 0f, _pathAngle + _lean * press * Mathf.Sin(k * Mathf.PI));
            SetAlpha(_pawImage, alpha);
            // Soft round dots stay a moment where it brushed (alpha only: drawn nothing when clear, never toggled).
            for (int i = 0; i < _trail.Length; i++)
            {
                if (_trailBorn[i] < 0f && t >= SwipeAppear && k >= (i + 0.5f) / _trail.Length)
                    _trailBorn[i] = t;
                float age = _trailBorn[i] < 0f ? -1f : (t - _trailBorn[i]) / TrailLife;
                bool on = age >= 0f && age < 1f;
                SetAlpha(_trail[i], on ? 0.6f * (1f - age) : 0f);
                if (!on)
                    continue;
                float d = Mathf.Lerp(0.6f, 1.15f, age);
                _trail[i].rectTransform.localScale = new Vector3(d, d, 1f);
            }
        }

        /// <summary>
        /// A tap (UI_Gesture_Tap, the A12 paw's curves): the paw floats above the point, comes down, holds, lifts, once per
        /// 1.1 s, a soft ring spreading on the point from the press. A double tap presses twice, quickly, in the same time.
        /// </summary>
        private void PoseTap(bool twice)
        {
            while (_gestureTime >= TapPeriod)
            {
                _gestureTime -= TapPeriod;
                _gestureCycles++;
            }
            float phase = _gestureTime / TapPeriod;
            float press;
            if (!twice)
                press = phase < 0.27f ? UiCurves.Smooth(phase / 0.27f) : phase < 0.38f ? 1f : 1f - UiCurves.Smooth((phase - 0.38f) / 0.32f);
            else if (phase < 0.12f)
                press = UiCurves.Smooth(phase / 0.12f);
            else if (phase < 0.24f)
                press = 1f - 0.7f * UiCurves.Smooth((phase - 0.12f) / 0.12f);
            else if (phase < 0.36f)
                press = 0.3f + 0.7f * UiCurves.Smooth((phase - 0.24f) / 0.12f);
            else if (phase < 0.44f)
                press = 1f;
            else
                press = 1f - UiCurves.Smooth((phase - 0.44f) / 0.28f);
            _pawPoint = _from;
            _paw.anchoredPosition = _from + TapRest - new Vector2(0f, TapDepth * press);
            float s = 1f - 0.08f * press;
            _paw.localScale = new Vector3(s, s, 1f);
            _paw.localRotation = Quaternion.identity;
            SetAlpha(_pawImage, 1f);
            PoseRing(0, phase, twice ? 0.12f : 0.27f);
            PoseRing(1, twice ? phase : -1f, 0.36f);
        }

        /// <summary>The swipe paw's tilt for its path: the slope, 30° at most up or down, mirrored for a leftward slide.</summary>
        private static float PathAngle(Vector2 way)
        {
            if (way.sqrMagnitude < 1e-4f)
                return 0f;
            float angle = Mathf.Clamp(Mathf.Atan2(way.y, Mathf.Abs(way.x)) * Mathf.Rad2Deg, -MaxPathAngle, MaxPathAngle);
            return way.x >= 0f ? angle : -angle;
        }

        private void PoseRing(int index, float phase, float start)
        {
            if (index >= _rings.Length)
                return;
            var ring = _rings[index];
            float t = (phase - start) / 0.55f;
            bool on = phase >= 0f && t >= 0f && t < 1f;
            SetAlpha(ring, on ? (1f - t) * 0.8f : 0f);
            if (!on)
                return;
            float s = Mathf.Lerp(0.45f, 1.35f, t);
            ring.rectTransform.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>
        /// Reduce Motion: a still paw that breathes softly, on the start point; a swipe keeps its way drawn in soft dots
        /// (the paw faces it), a tap a soft ring on its point.
        /// </summary>
        private void PoseStill()
        {
            bool swipe = _gesture == ActivityGesture.Swipe;
            _pawPoint = _from;
            _paw.anchoredPosition = swipe ? _from : _from + TapRest - new Vector2(0f, TapDepth * 0.5f);
            _paw.localScale = new Vector3(_pawFlip, 1f, 1f);
            _paw.localRotation = swipe ? Quaternion.Euler(0f, 0f, _pathAngle) : Quaternion.identity;
            float breath = 0.5f + 0.5f * Mathf.Sin(_gestureTime * 2.6f);
            SetAlpha(_pawImage, 0.72f + 0.28f * breath);
            for (int i = 0; i < _trail.Length; i++)
            {
                if (!swipe)
                    continue;
                SetAlpha(_trail[i], 0.45f);
                _trail[i].rectTransform.localScale = Vector3.one;
            }
            for (int i = 0; i < _rings.Length; i++)
            {
                bool on = !swipe && i == 0;
                SetAlpha(_rings[i], on ? 0.3f + 0.25f * breath : 0f);
                if (on)
                    _rings[i].rectTransform.localScale = Vector3.one;
            }
        }

        private void TickPicker(float dt)
        {
            if (!_pickerOn)
                return;
            _pickerTime += dt;
            PosePicker(dt);
        }

        /// <summary>
        /// The choices bounce in one after the other (the doc 05 pop, 80 ms apart); the picked one bounces (1.16, 0.96, 1)
        /// and fades after 0.5 s; the others shrink softly and fade (0.18 s). Reduce Motion: fades only.
        /// </summary>
        private void PosePicker(float dt)
        {
            bool closing = _picked >= 0 || _pickerHiding;
            bool any = false;
            for (int i = 0; i < _pickerCount; i++)
            {
                float alpha = _itemAlpha[i];
                float scale = _itemScale[i];
                if (i == _picked)
                {
                    float t = _pickerTime - _pickerEndAt;
                    scale = _reduceMotion ? 1f : PickBounce(t);
                    alpha = Mathf.MoveTowards(alpha, t < PickFadeDelay ? 1f : 0f, dt / PickerFade);
                }
                else if (closing)
                {
                    alpha = Mathf.MoveTowards(alpha, 0f, dt / PickerFade);
                    if (!_reduceMotion)
                        scale = Mathf.MoveTowards(scale, 0.7f, dt * 1.7f);
                }
                else
                {
                    float t = _pickerTime - i * PickerStagger;
                    alpha = Mathf.Clamp01(t / 0.12f);
                    scale = _reduceMotion ? 1f : UiCurves.Pop(t);
                }
                if (alpha != _itemAlpha[i])
                {
                    _itemAlpha[i] = alpha;
                    _pickerGroups[i].alpha = alpha;
                }
                if (scale != _itemScale[i])
                {
                    _itemScale[i] = scale;
                    _pickerItems[i].localScale = new Vector3(scale, scale, 1f);
                }
                if (alpha > 0f)
                    any = true;
            }
            if (closing && !any)
                StopPicker();
        }

        private static float PickBounce(float t)
        {
            if (t <= 0f || t >= 0.36f)
                return 1f;
            if (t < 0.12f)
                return Mathf.Lerp(1f, 1.16f, UiCurves.EaseOut(t / 0.12f));
            if (t < 0.24f)
                return Mathf.Lerp(1.16f, 0.96f, UiCurves.Smooth((t - 0.12f) / 0.12f));
            return Mathf.Lerp(0.96f, 1f, UiCurves.Smooth((t - 0.24f) / 0.12f));
        }

        private void OnPick(int index)
        {
            if (!_pickerOpen || _picked >= 0 || index < 0 || index >= _pickerCount)
                return;
            _picked = index;
            _pickerEndAt = _pickerTime;
            _pickerOpen = false;
            StopTouches();
            var actions = _pickerActions;
            _pickerActions = null;
            actions?.Pick(index);
        }

        private void StopTouches()
        {
            for (int i = 0; i < _pickerGroups.Length; i++)
            {
                _pickerGroups[i].interactable = false;
                _pickerGroups[i].blocksRaycasts = false;
            }
        }

        private void TickBoost(float dt)
        {
            if (!_boostOn)
                return;
            _boostTime += dt;
            if (!_boostHiding && !_paused)
            {
                _boostLeft -= dt;
                if (_boostLeft <= 0f)
                {
                    _boostLeft = 0f;
                    _boostHiding = true;
                }
            }
            SetBoostFill(_boostLeft / _boostTotal);
            float target = _boostHiding ? 0f : 1f;
            if (_boostAlpha != target)
            {
                _boostAlpha = Mathf.MoveTowards(_boostAlpha, target, dt / (_boostHiding ? BoostFadeOut : BoostFadeIn));
                _boostGroup.alpha = _boostAlpha;
            }
            if (_boostHiding && _boostAlpha <= 0f)
            {
                StopBoost();
                return;
            }
            if (_reduceMotion)
                return;
            // A soft pop in (0.6 to 1.08 to 1), a slight settle out.
            float s = _boostHiding ? Mathf.Lerp(0.85f, 1f, _boostAlpha) : _boostTime < 0.32f ? ActivityCurves.SoftPop(_boostTime) : 1f;
            if (_boostRoot.localScale.x != s)
                _boostRoot.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>The ring empties calmly: it moves only by visible steps (no rebuild for nothing).</summary>
        private void SetBoostFill(float fill)
        {
            fill = Mathf.Clamp01(fill);
            if (Mathf.Abs(fill - _boostFillShown) < 0.002f && !(fill <= 0f && _boostFillShown > 0f))
                return;
            _boostFillShown = fill;
            _boostRing.fillAmount = fill;
        }

        private void PlaceBoost()
        {
            if (_boostRoot == null)
                return;
            float y = _practice ? BoostYPractice : BoostY;
            var position = _boostRoot.anchoredPosition;
            if (position.y != y)
                _boostRoot.anchoredPosition = new Vector2(position.x, y);
        }

        // ---------------- T38 parts put away ----------------

        private void StopTip()
        {
            _tipOn = false;
            _tipHiding = false;
            _tipPop = -1f;
            _tipAlpha = 0f;
            if (_tipRoot != null)
                SetActive(_tipRoot.gameObject, false);
        }

        private void StopGesture()
        {
            _gestureOn = false;
            _gestureHiding = false;
            _gesture = ActivityGesture.None;
            _gestureAlpha = 0f;
            HideAll(_rings);
            HideAll(_trail);
            if (_gestureRoot != null)
                SetActive(_gestureRoot.gameObject, false);
        }

        private void StopPicker()
        {
            _pickerOn = false;
            _pickerOpen = false;
            _pickerHiding = false;
            _picked = -1;
            _pickerCount = 0;
            _pickerActions = null;
            if (_pickerRoot != null)
                SetActive(_pickerRoot.gameObject, false);
        }

        private void StopBoost()
        {
            _boostOn = false;
            _boostHiding = false;
            _boostAlpha = 0f;
            if (_boostRoot != null)
                SetActive(_boostRoot.gameObject, false);
        }

        /// <summary>Every T38 part closed at once (a new game, the HUD closed, the prewarm's end).</summary>
        private void ResetParts()
        {
            StopTip();
            StopGesture();
            StopPicker();
            StopBoost();
        }

        /// <summary>Every T38 part drawn once with its longest content, so the first real game builds nothing new.</summary>
        private void PrewarmParts(ILocalization text)
        {
            if (!HasT38Parts)
                return;
            string sample = text != null ? text.Get(ActivityText.GroomingTipBurr) : null;
            ShowTip(string.IsNullOrEmpty(sample) ? ActivityText.GroomingTipBurr : sample, DefaultTipSeconds);
            ShowGesture(ActivityGesture.Swipe, Vector2.zero, new Vector2(200f, 0f));
            ShowPicker(PrewarmItems, MaxPickerItems, null);
            ShowBoost(null, 1f);
            ShowAll(_trail);
            ShowAll(_rings);
            for (int i = 0; i < _pickerItems.Length; i++)
            {
                _pickerGroups[i].alpha = 1f;
                _pickerItems[i].localScale = Vector3.one;
            }
            _tipGroup.alpha = 1f;
            _gestureGroup.alpha = 1f;
            _boostGroup.alpha = 1f;
        }

        private void ResetTrail()
        {
            for (int i = 0; i < _trailBorn.Length; i++)
                _trailBorn[i] = -1f;
        }

        private Vector2 ToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_gestureRoot, screen, UiCamera(), out var local);
            return local;
        }

        private Camera UiCamera() => _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var color = graphic.color;
            if (color.a == alpha)
                return;
            color.a = alpha;
            graphic.color = color;
        }

        private static void HideAll(Image[] images)
        {
            for (int i = 0; i < images.Length; i++)
                SetActive(images[i].gameObject, false);
        }

        private static void ShowAll(Image[] images)
        {
            for (int i = 0; i < images.Length; i++)
                SetActive(images[i].gameObject, true);
        }

        /// <summary>Pooled images drawn right now: active and not clear.</summary>
        private static int CountShown(Image[] images)
        {
            int count = 0;
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].gameObject.activeSelf && images[i].color.a > 0.01f)
                    count++;
            }
            return count;
        }

        // ---------------- binding ----------------

        private void BindTexts(ILocalization text)
        {
            if (text == null)
                return;
            _text = text;
            _streakTemplate = text.Get(ActivityText.HudStreak);
            _practiceText.SetString(text.Get(ActivityText.Practice));
            _pauseTitleText.SetString(text.Get(ActivityText.PauseTitle));
            _resumeText.SetString(text.Get(ActivityText.PauseResume));
            _quitText.SetString(text.Get(ActivityText.PauseQuit));
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _motion.Bind(_canvas, _content, _group, null);
            _motion.ReduceMotion = _reduceMotion;
            _scoreText = new UiLabel(_score, 24);
            _secondsText = new UiLabel(_seconds, 16);
            _streakText = new UiLabel(_streak, 48);
            _practiceText = new UiLabel(_practiceLabel, 48);
            _pauseTitleText = new UiLabel(_pauseTitle, 48);
            _resumeText = new UiLabel(_resumeLabel, 48);
            _quitText = new UiLabel(_quitLabel, 48);
            _pause.onClick.AddListener(OnPause);
            _resume.onClick.AddListener(OnResume);
            _quit.onClick.AddListener(OnQuit);
            // T38 parts (absent from a HUD built before T38: every part checks its references).
            if (_tip != null)
                _tipText = new UiLabel(_tip, 128);
            _trailBorn = new float[_trail.Length];
            ResetTrail();
            _itemAlpha = new float[_pickerItems.Length];
            _itemScale = new float[_pickerItems.Length];
            _pickHandlers = new UnityAction[_pickerButtons.Length];
            for (int i = 0; i < _pickerButtons.Length; i++)
            {
                int index = i;
                _pickHandlers[i] = () => OnPick(index);
                _pickerButtons[i].onClick.AddListener(_pickHandlers[i]);
            }
        }

        private void OnPause()
        {
            if (!_paused)
                _actions?.PauseActivity();
        }

        private void OnResume() => _actions?.ResumeActivity();
        private void OnQuit() => _actions?.QuitActivity();

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        private void OnDestroy()
        {
            if (!_initialized)
                return;
            _pause.onClick.RemoveListener(OnPause);
            _resume.onClick.RemoveListener(OnResume);
            _quit.onClick.RemoveListener(OnQuit);
            for (int i = 0; i < _pickHandlers.Length; i++)
            {
                if (_pickerButtons[i] != null)
                    _pickerButtons[i].onClick.RemoveListener(_pickHandlers[i]);
            }
        }

        // ---------------- prefab builder hooks (Editor ActivityUiPipeline) ----------------

        public void SetReferences(UiColorTokens tokens, Canvas canvas, GameObject content, CanvasGroup group, RectTransform safeArea)
        {
            _tokens = tokens;
            _canvas = canvas;
            _content = content;
            _group = group;
            _safeArea = safeArea;
        }

        public void SetScoreReferences(RectTransform scoreRoot, TMP_Text score, RectTransform bar, RectTransform fill, RectTransform[] marks,
            Image[] markImages, RectTransform bestDot, RectTransform timeRoot, Image timeFill, TMP_Text seconds, RectTransform streakRoot,
            TMP_Text streak, GameObject practiceTag, TMP_Text practiceLabel)
        {
            _scoreRoot = scoreRoot;
            _score = score;
            _bar = bar;
            _fill = fill;
            _marks = marks;
            _markImages = markImages;
            _bestDot = bestDot;
            _timeRoot = timeRoot;
            _timeFill = timeFill;
            _seconds = seconds;
            _streakRoot = streakRoot;
            _streak = streak;
            _practiceTag = practiceTag;
            _practiceLabel = practiceLabel;
        }

        public void SetPauseReferences(Button pause, GameObject pauseLayer, CanvasGroup pauseGroup, RectTransform pauseCard, TMP_Text pauseTitle,
            Button resume, TMP_Text resumeLabel, Button quit, TMP_Text quitLabel, Color gold, Color markOff)
        {
            _pause = pause;
            _pauseLayer = pauseLayer;
            _pauseGroup = pauseGroup;
            _pauseCard = pauseCard;
            _pauseTitle = pauseTitle;
            _resume = resume;
            _resumeLabel = resumeLabel;
            _quit = quit;
            _quitLabel = quitLabel;
            _gold = gold;
            _markOff = markOff;
        }

        /// <summary>T38 builder hook: the tip (its own passive canvas), its bubble (width set per tip) and label.</summary>
        public void SetTipReferences(RectTransform root, CanvasGroup group, RectTransform bubble, TMP_Text label)
        {
            _tipRoot = root;
            _tipGroup = group;
            _tipBubble = bubble;
            _tip = label;
        }

        /// <summary>
        /// T38 builder hook: the paw layer (full screen, own passive canvas), the paw, its rings and trail dots, the swipe and
        /// tap paws, and the swipe paw's pad in its frame (the point that follows the path).
        /// </summary>
        public void SetGestureReferences(RectTransform root, CanvasGroup group, RectTransform paw, Image pawImage, Image[] rings, Image[] trail,
            Sprite swipe, Sprite tap, Vector2 swipePivot)
        {
            _swipePivot = swipePivot;
            _gestureRoot = root;
            _gestureGroup = group;
            _paw = paw;
            _pawImage = pawImage;
            _rings = rings;
            _trail = trail;
            _swipeSprite = swipe;
            _tapSprite = tap;
        }

        /// <summary>T38 builder hook: the picker (own canvas and raycaster) and its round choices.</summary>
        public void SetPickerReferences(RectTransform root, RectTransform[] items, CanvasGroup[] groups, Button[] buttons, Image[] icons, float pitch,
            Sprite fallback)
        {
            _pickerRoot = root;
            _pickerItems = items;
            _pickerGroups = groups;
            _pickerButtons = buttons;
            _pickerIcons = icons;
            _pickerPitch = pitch;
            _pickerFallback = fallback;
        }

        /// <summary>T38 builder hook: the boost chip (own passive canvas), its icon and its emptying ring.</summary>
        /// <summary>The icons the stages borrow (built by ActivityUiPipeline from Atlas_Activities).</summary>
        public void SetIcons(Sprite[] icons) => _icons = icons ?? System.Array.Empty<Sprite>();

        public Sprite Icon(string name)
        {
            if (string.IsNullOrEmpty(name) || _icons == null)
                return null;
            for (int i = 0; i < _icons.Length; i++)
                if (_icons[i] != null && string.Equals(_icons[i].name, name, System.StringComparison.Ordinal))
                    return _icons[i];
            return null;
        }

        public void SetBoostReferences(RectTransform root, CanvasGroup group, Image icon, Image ring, Sprite fallback)
        {
            _boostRoot = root;
            _boostGroup = group;
            _boostIcon = icon;
            _boostRing = ring;
            _boostFallback = fallback;
        }
    }
}
