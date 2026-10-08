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
    /// PF_UI_ActivityHud, screen 030 (doc 06: score, timer, pause, combo; doc « Mini-jeux & activités » section 2; doc 01
    /// Contenu section 12.0): shared by every activity, each activity only adds its own props in the world. At the top: the
    /// score that rolls up to its value with a small bump, on a bar with the three star marks (each lights gold with a soft
    /// pop when reached) and a soft dot at the best; the time left as a soft ring that empties calmly (never red, no beep,
    /// the seconds in a quiet color); the streak in a small pill from ×2; « Entraînement » when nothing is at stake; the
    /// pause button opening a small card (« Reprendre », « Quitter »). Passive except the pause button and the pause card:
    /// touches go to the activity. Per-frame setters only touch a label when its characters change; the score and the
    /// ring have their own canvases. Hidden: canvas disabled, content inactive. Prewarmed; nothing is instantiated after.
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
            SetActive(_practiceTag, model.Mode == ActivityMode.Practice);
            _paused = false;
            _pauseAlpha = 0f;
            SetActive(_pauseLayer, false);
            if (!_motion.IsOpen || _motion.Closing)
                _motion.Show();
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
            Canvas.ForceUpdateCanvases();
            SetPaused(false);
            _pauseAlpha = 0f;
            ApplyPause();
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
                return;
            }
            TickScore(unscaledDeltaTime);
            TickMarks(unscaledDeltaTime);
            TickStreak(unscaledDeltaTime);
            TickPause(unscaledDeltaTime);
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
    }
}
