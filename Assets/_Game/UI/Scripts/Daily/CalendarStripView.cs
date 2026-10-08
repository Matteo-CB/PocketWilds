using System;
using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T36 the 28-day calendar strip of Today (doc 01 Contenu section 4, doc 05 Sensations sheet 19, doc 06 screen 031):
    /// 28 UI_Calendar_Box tiles in a horizontal strip, tinted cream once stamped (with UI_PawStamp), gold for today
    /// (breathing gently while it waits for its stamp), white ahead; big boxes (7, 14, 21, 28) carry a round five-point
    /// star; each tile shows its day and its reward. One touch on today's box (or « Tamponner ») stamps it: the paw comes
    /// down 1.4 to 0.95 in 140 ms, 1.00 in 160 ms, the ink spreads as a ring (H-VFX-072; round stars at big boxes, 1.5 s).
    /// Always readable: « Demain : {reward} » and the real days to the next big box, never a countdown. The strip opens on
    /// today; a vertical drag that starts on it scrolls the sheet instead. Reduce Motion: the paw fades in, no breathing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CalendarStripView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const int Boxes = 28;

        private const float BigSeconds = 1.5f;
        private const float InkSeconds = 0.5f;
        private const float BreathHz = 1.1f;

        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private ScrollRect _outer;
        [SerializeField] private RectTransform _content;
        [SerializeField] private RectTransform[] _boxes = Array.Empty<RectTransform>();
        [SerializeField] private Image[] _backs = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] _days = Array.Empty<TMP_Text>();
        [SerializeField] private Image[] _icons = Array.Empty<Image>();
        [SerializeField] private Image[] _spots = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] _amounts = Array.Empty<TMP_Text>();
        [SerializeField] private Image[] _stamps = Array.Empty<Image>();
        [SerializeField] private Image[] _bigMarks = Array.Empty<Image>();
        [SerializeField] private Button[] _buttons = Array.Empty<Button>();
        [SerializeField] private Image _glow;
        [SerializeField] private Image _ink;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private Button _stampButton;
        [SerializeField] private TMP_Text _stampLabel;
        [SerializeField] private GameObject _doneRow;
        [SerializeField] private TMP_Text _done;
        [SerializeField] private Image _tomorrowIcon;
        [SerializeField] private Image _tomorrowSpots;
        [SerializeField] private TMP_Text _tomorrow;
        [SerializeField] private TMP_Text _bigInText;
        [SerializeField] private float _pitch = 152f;
        [SerializeField] private Color _claimedTint = new Color(0.965f, 0.914f, 0.824f, 1f);
        [SerializeField] private Color _todayTint = new Color(0.969f, 0.812f, 0.416f, 1f);
        [SerializeField] private Color _futureTint = Color.white;
        [SerializeField] private Color _inkColor = new Color(0.290f, 0.227f, 0.173f, 0.6f);
        [SerializeField] private Color _starColor = new Color(0.949f, 0.710f, 0.267f, 1f);
        [SerializeField] private Color _gemsColor = new Color(0.498f, 0.698f, 0.941f, 1f);

        private readonly UiLabel[] _amountLabels = new UiLabel[Boxes];
        private readonly RewardKind[] _shownKind = new RewardKind[Boxes];
        private readonly ContentId[] _shownItem = new ContentId[Boxes];
        private readonly long[] _shownAmount = new long[Boxes];
        private readonly double[] _shownMinutes = new double[Boxes];
        private readonly byte[] _shownFlags = new byte[Boxes];
        private UiLabel _titleLabel;
        private UiLabel _stampText;
        private UiLabel _doneLabel;
        private UiLabel _tomorrowLabel;
        private UiLabel _bigInLabel;
        private string _tomorrowTemplate;
        private UiCounted _bigIn;
        private string _bigTomorrow;
        private string _bigToday;
        private string _cycleTemplate;
        private Action _onStamp;
        private UiBurst _burst;
        private int _today = -1;
        private bool _claimable;
        private float _breath;
        private int _stampBox = -1;
        private float _stampTime = -1f;
        private float _stampLength;
        private bool _stampBig;
        private int _pressBox = -1;
        private float _press = -1f;
        private bool _toOuter;
        private bool _initialized;

        public bool ReduceMotion { get; set; }
        public int TodayBox => _today;
        public bool Claimable => _claimable;
        public bool Stamping => _stampTime >= 0f;
        public Button StampButton => _stampButton;
        public string TomorrowText => _tomorrow != null ? _tomorrow.text : string.Empty;
        public string BigInText => _bigInText != null ? _bigInText.text : string.Empty;
        public Button BoxButton(int box) => _buttons[box];
        public RectTransform BoxRect(int box) => _boxes[box];
        public bool StampShown(int box) => _stamps[box].gameObject.activeSelf;
        public Vector3 StampScale(int box) => _stamps[box].rectTransform.localScale;
        public Color BoxTint(int box) => _backs[box].color;
        public bool BigMarked(int box) => _bigMarks[box].gameObject.activeSelf;
        public float ScrollX => _content != null ? _content.anchoredPosition.x : 0f;

        public void Initialize(Action onStamp, UiBurst burst, ScrollRect outer)
        {
            _onStamp = onStamp;
            _burst = burst;
            _outer = outer != null ? outer : _outer;
            if (_initialized)
                return;
            _initialized = true;
            _titleLabel = new UiLabel(_title);
            _stampText = new UiLabel(_stampLabel);
            _doneLabel = new UiLabel(_done);
            _tomorrowLabel = new UiLabel(_tomorrow, 160);
            _bigInLabel = new UiLabel(_bigInText, 96);
            for (int i = 0; i < _boxes.Length && i < Boxes; i++)
            {
                _amountLabels[i] = new UiLabel(_amounts[i], 48);
                new UiLabel(_days[i], 8).SetNumber(i + 1);
                _shownKind[i] = (RewardKind)255;
                _shownFlags[i] = 255;
                int box = i;
                _buttons[i].onClick.AddListener(() => OnBox(box));
            }
            _stampButton.onClick.AddListener(OnStampButton);
        }

        public void SetTexts(ILocalization text)
        {
            _titleLabel.SetString(text.Get(DailyVisitText.CalendarTitle));
            _stampText.SetString(text.Get(DailyVisitText.CalendarStamp));
            _doneLabel.SetString(text.Get(DailyVisitText.CalendarDone));
            _tomorrowTemplate = text.Get(DailyVisitText.CalendarTomorrow);
            _bigIn = UiCounted.From(text, DailyVisitText.CalendarBigIn);
            _bigTomorrow = text.Get(DailyVisitText.CalendarBigTomorrow);
            _bigToday = text.Get(DailyVisitTextFormatter.CalendarBigTodayKey);
            _cycleTemplate = text.Get(DailyVisitText.CalendarCycle);
            // Templates changed (a language switch): every sentence is written again.
            _tomorrowLabel.SetString(string.Empty);
            _bigInLabel.SetString(string.Empty);
        }

        public void Bind(ITodayModel model, RewardIconSet icons, RewardTexts texts)
        {
            _claimable = model.CalendarClaimable;
            int today = -1;
            for (int i = 0; i < _boxes.Length && i < Boxes; i++)
            {
                var cell = model.Box(i);
                if (cell.Today)
                    today = i;
                BindBox(i, cell, icons, texts);
            }
            if (today < 0)
                today = Mathf.Clamp(model.CalendarNext, 0, Boxes - 1);
            if (today != _today)
            {
                if (_today >= 0)
                    _boxes[_today].localScale = Vector3.one;
                _today = today;
            }
            SetActive(_glow.gameObject, _claimable);
            if (_claimable)
                _glow.rectTransform.anchoredPosition = _boxes[_today].anchoredPosition;

            SetActive(_stampButton.gameObject, _claimable);
            SetActive(_doneRow, !_claimable);

            // « Demain : {reward} »: tomorrow's box, its icon in front.
            var tomorrow = model.Tomorrow;
            bool hasTomorrow = !tomorrow.IsEmpty;
            SetActive(_tomorrow.gameObject, hasTomorrow);
            SetActive(_tomorrowIcon.gameObject, hasTomorrow);
            if (hasTomorrow)
            {
                // Box 28's decoration says whose cycle it crowns (« Demain : Décoration du cycle 2 »).
                if (tomorrow.Kind == RewardKind.Decoration)
                    _tomorrowLabel.Commit(UiChars.TemplateWithReward(_tomorrowLabel.Buffer, 0, _tomorrowTemplate, _cycleTemplate,
                        Mathf.Max(1, model.CalendarCycle)));
                else
                    _tomorrowLabel.SetTemplate(_tomorrowTemplate, tomorrow.Amount, tomorrow, texts);
                SetIcon(_tomorrowIcon, _tomorrowSpots, tomorrow, icons);
            }

            // The real days to the next big box (0 today, 1 tomorrow): today's stamp counts as one while it is to give.
            int days = Mathf.Max(0, _claimable ? model.DaysToBigBox - 1 : model.DaysToBigBox);
            if (days == 0)
                _bigInLabel.SetString(_bigToday);
            else if (days == 1)
                _bigInLabel.SetString(_bigTomorrow);
            else
                _bigInLabel.SetTemplate(_bigIn.For(days), days);
        }

        /// <summary>The strip opens on today's box, a little left of the middle so the next ones show.</summary>
        public void ScrollToToday()
        {
            if (_content == null || _scroll == null)
                return;
            var viewport = _scroll.viewport != null ? _scroll.viewport : (RectTransform)_scroll.transform;
            float view = viewport.rect.width;
            float width = _content.rect.width;
            float target = Mathf.Max(0f, _today) * _pitch - view * 0.38f;
            float x = -Mathf.Clamp(target, 0f, Mathf.Max(0f, width - view));
            _scroll.StopMovement();
            _content.anchoredPosition = new Vector2(x, _content.anchoredPosition.y);
        }

        /// <summary>The paw stamp lands on <paramref name="box"/> (doc 05 sheet 19).</summary>
        public void PlayStamp(int box, bool big, bool gems)
        {
            if ((uint)box >= (uint)Mathf.Min(_boxes.Length, Boxes))
                return;
            _stampBox = box;
            _stampBig = big;
            _stampTime = 0f;
            _stampLength = big ? BigSeconds : 0.7f;
            var stamp = _stamps[box];
            SetActive(stamp.gameObject, true);
            stamp.rectTransform.localScale = ReduceMotion ? Vector3.one : new Vector3(1.4f, 1.4f, 1f);
            SetAlpha(stamp, 0f);
            _ink.rectTransform.anchoredPosition = _boxes[box].anchoredPosition;
            SetActive(_ink.gameObject, true);
            SetAlpha(_ink, 0f);
            if (_burst != null)
                _burst.Play(big ? UiBurstKind.StampBig : UiBurstKind.Stamp, _boxes[box], _inkColor, gems ? _gemsColor : _starColor, false, 0.12f);
        }

        public void Tick(float deltaTime)
        {
            if (_claimable && _today >= 0)
            {
                // Respiration (doc 05 section 0.1): today's box breathes ±3.5% while its stamp waits.
                _breath += deltaTime;
                float wave = Mathf.Sin(_breath * BreathHz * 6.2832f);
                if (!ReduceMotion && _pressBox != _today && (_stampTime < 0f || _stampBox != _today))
                {
                    float s = 1f + 0.035f * wave;
                    _boxes[_today].localScale = new Vector3(s, s, 1f);
                }
                SetAlpha(_glow, ReduceMotion ? 0.55f : 0.45f + 0.25f * wave);
            }
            TickPress(deltaTime);
            TickStamp(deltaTime);
        }

        private void TickStamp(float deltaTime)
        {
            if (_stampTime < 0f)
                return;
            _stampTime += deltaTime;
            float t = _stampTime;
            var stamp = _stamps[_stampBox];
            var box = _boxes[_stampBox];
            if (ReduceMotion)
            {
                SetAlpha(stamp, Mathf.Clamp01(t / 0.2f));
                stamp.rectTransform.localScale = Vector3.one;
                // The ink brightens in place.
                _ink.rectTransform.localScale = Vector3.one;
                SetAlpha(_ink, 0.5f * Mathf.Sin(Mathf.Clamp01(t / InkSeconds) * Mathf.PI));
            }
            else
            {
                SetAlpha(stamp, Mathf.Clamp01(t / 0.06f));
                float s = UiCurves.Stamp(t);
                stamp.rectTransform.localScale = new Vector3(s, s, 1f);
                // The ink spreads as a ring from the impact.
                float k = Mathf.Clamp01((t - 0.12f) / InkSeconds);
                float ring = Mathf.Lerp(0.5f, 1.45f, UiCurves.EaseOut(k));
                _ink.rectTransform.localScale = new Vector3(ring, ring, 1f);
                SetAlpha(_ink, t < 0.12f ? 0f : 0.7f * (1f - k));
                // The box takes the impact; a big box pops once more.
                float boxScale = 1f;
                if (t >= 0.14f && t < 0.14f + UiCurves.PressSeconds)
                    boxScale = UiCurves.Press(t - 0.14f);
                else if (_stampBig && t >= 0.45f && t < 0.85f)
                    boxScale = 1f + 0.08f * Mathf.Sin((t - 0.45f) / 0.4f * Mathf.PI);
                box.localScale = new Vector3(boxScale, boxScale, 1f);
            }
            if (t >= _stampLength)
            {
                _stampTime = -1f;
                stamp.rectTransform.localScale = Vector3.one;
                SetAlpha(stamp, 1f);
                box.localScale = Vector3.one;
                SetActive(_ink.gameObject, false);
            }
        }

        private void TickPress(float deltaTime)
        {
            if (_press < 0f)
                return;
            _press += deltaTime;
            float s = UiCurves.Press(_press);
            _boxes[_pressBox].localScale = new Vector3(s, s, 1f);
            if (_press >= UiCurves.PressSeconds)
            {
                _boxes[_pressBox].localScale = Vector3.one;
                _press = -1f;
                _pressBox = -1;
            }
        }

        private void BindBox(int i, in CalendarBoxCell cell, RewardIconSet icons, RewardTexts texts)
        {
            byte flags = (byte)((cell.Stamped ? 1 : 0) | (cell.Today ? 2 : 0) | (cell.Big ? 4 : 0));
            if (flags != _shownFlags[i])
            {
                _shownFlags[i] = flags;
                _backs[i].color = cell.Today ? _todayTint : cell.Stamped ? _claimedTint : _futureTint;
                // A stamp being played keeps its own animation.
                if (_stampTime < 0f || i != _stampBox)
                    SetActive(_stamps[i].gameObject, cell.Stamped);
                // A stamped box shows its paw, not its amount (the reward was taken).
                SetActive(_amounts[i].gameObject, !cell.Stamped);
                SetActive(_bigMarks[i].gameObject, cell.Big);
                if (!cell.Today && i != _pressBox && (i != _stampBox || _stampTime < 0f))
                    _boxes[i].localScale = Vector3.one;
            }
            var reward = cell.Reward;
            if (reward.Kind != _shownKind[i] || reward.Item != _shownItem[i])
            {
                _shownKind[i] = reward.Kind;
                _shownItem[i] = reward.Item;
                SetIcon(_icons[i], _spots[i], reward, icons);
            }
            if (reward.Amount != _shownAmount[i] || reward.LightMinutes != _shownMinutes[i])
            {
                _shownAmount[i] = reward.Amount;
                _shownMinutes[i] = reward.LightMinutes;
                var label = _amountLabels[i];
                label.Commit(UiChars.Amount(label.Buffer, 0, reward, texts));
            }
        }

        private static void SetIcon(Image icon, Image spots, in RewardCell reward, RewardIconSet icons)
        {
            bool egg = reward.Kind == RewardKind.DiscoveryEgg;
            icon.sprite = icons != null ? (egg ? icons.eggShell : icons.Icon(reward.Kind, reward.Item)) : null;
            icon.color = egg && icons != null ? icons.eggShellColor : Color.white;
            icon.enabled = icon.sprite != null;
            if (spots == null)
                return;
            if (spots.gameObject.activeSelf != egg)
                spots.gameObject.SetActive(egg);
            if (egg && icons != null)
            {
                spots.sprite = icons.eggSpots;
                spots.color = icons.eggSpotsColor;
            }
        }

        private void OnBox(int box)
        {
            if (_claimable && box == _today)
            {
                _onStamp?.Invoke();
                return;
            }
            // Every touch answers: the box gives a little.
            if (ReduceMotion || Stamping)
                return;
            if (_pressBox >= 0 && _pressBox != box)
                _boxes[_pressBox].localScale = Vector3.one;
            _pressBox = box;
            _press = 0f;
        }

        private void OnStampButton()
        {
            if (_claimable)
                _onStamp?.Invoke();
        }

        // A drag that starts on the strip but goes mostly up or down scrolls the sheet (nested scroll views).

        public void OnBeginDrag(PointerEventData eventData)
        {
            _toOuter = _outer != null && _outer.enabled && Mathf.Abs(eventData.delta.y) > Mathf.Abs(eventData.delta.x);
            if (!_toOuter)
                return;
            _scroll.OnEndDrag(eventData);
            _outer.OnInitializePotentialDrag(eventData);
            _outer.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_toOuter)
                _outer.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_toOuter)
                return;
            _toOuter = false;
            _outer.OnEndDrag(eventData);
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var color = graphic.color;
            if (Mathf.Approximately(color.a, alpha))
                return;
            color.a = alpha;
            graphic.color = color;
        }

        private void OnDestroy()
        {
            if (!_initialized)
                return;
            foreach (var button in _buttons)
                if (button != null)
                    button.onClick.RemoveAllListeners();
            _stampButton.onClick.RemoveListener(OnStampButton);
        }

        /// <summary>Prefab builder hook (Editor DailyVisitUiPipeline): the strip and its 28 tiles.</summary>
        public void SetReferences(ScrollRect scroll, RectTransform content, RectTransform[] boxes, Image[] backs, TMP_Text[] days, Image[] icons, Image[] spots,
            TMP_Text[] amounts, Image[] stamps, Image[] bigMarks, Button[] buttons, Image glow, Image ink, float pitch)
        {
            _scroll = scroll;
            _content = content;
            _boxes = boxes;
            _backs = backs;
            _days = days;
            _icons = icons;
            _spots = spots;
            _amounts = amounts;
            _stamps = stamps;
            _bigMarks = bigMarks;
            _buttons = buttons;
            _glow = glow;
            _ink = ink;
            _pitch = pitch;
        }

        /// <summary>Prefab builder hook: the card's texts and buttons around the strip.</summary>
        public void SetCardReferences(ScrollRect outer, TMP_Text title, Button stampButton, TMP_Text stampLabel, GameObject doneRow, TMP_Text done,
            Image tomorrowIcon, Image tomorrowSpots, TMP_Text tomorrow, TMP_Text bigIn)
        {
            _outer = outer;
            _title = title;
            _stampButton = stampButton;
            _stampLabel = stampLabel;
            _doneRow = doneRow;
            _done = done;
            _tomorrowIcon = tomorrowIcon;
            _tomorrowSpots = tomorrowSpots;
            _tomorrow = tomorrow;
            _bigInText = bigIn;
        }
    }
}
