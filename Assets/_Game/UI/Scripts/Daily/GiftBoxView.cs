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
    /// T36 the Cadeau du jour card of Today (doc 01 Contenu section 3, doc 05 Sensations sheet 18, doc 06 screen 031): a
    /// round light-wood box marked with a paw (never a parcel with a bow), drawn as three layers on one frame:
    /// UI_GiftBox_Base, UI_GiftBox_Band (tinted cream, sky blue or gold by the lot's size once open; soft rose while closed)
    /// and UI_GiftBox_Lid, which tilts around its own center (pivot at 0.73 of the frame, sprites review note). Closed, the
    /// box wiggles now and then and waits for « Ouvrir »; opening (the lot is already granted): two 6° swings (300 ms), the
    /// lid jumps and lands beside the box, a burst of round glints, then the lot's cells pop one by one (220 ms apart),
    /// 1 600 ms plus 220 ms per object, and « À demain ». A touch during the opening shows everything at once. Reduce
    /// Motion: lid and cells fade. Never shows another lot before the real one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GiftBoxView : MonoBehaviour
    {
        public const int MaxCells = 4;

        private const float SwingEnd = 0.30f;
        private const float HopEnd = 0.46f;
        private const float LandEnd = 0.78f;
        private const float BurstAt = 0.40f;
        private const float CellsStart = 0.82f;
        private const float CellStagger = 0.22f;
        private const float BaseSeconds = 1.6f;
        private const float IdlePeriod = 3.4f;
        private const float IdleWiggle = 0.6f;
        private const float HopHeight = 32f;

        [SerializeField] private RectTransform _card;
        [SerializeField] private Button _boxButton;
        [SerializeField] private RectTransform _rock;
        [SerializeField] private Image _band;
        [SerializeField] private RectTransform _lidPivot;
        [SerializeField] private Image _lid;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _sizeWord;
        [SerializeField] private Button _open;
        [SerializeField] private TMP_Text _openLabel;
        [SerializeField] private TMP_Text _tomorrow;
        [SerializeField] private Button _info;
        [SerializeField] private RewardCellView[] _cells = Array.Empty<RewardCellView>();
        [SerializeField] private float _cellPitch = 140f;
        [SerializeField] private Vector2 _lidRest = new Vector2(132f, -118f);
        [SerializeField] private float _lidRestAngle = -16f;
        [SerializeField] private Color _closedBand = new Color(0.949f, 0.663f, 0.663f, 1f);
        [SerializeField] private Color _smallBand = new Color(0.976f, 0.910f, 0.784f, 1f);
        [SerializeField] private Color _niceBand = new Color(0.596f, 0.792f, 0.949f, 1f);
        [SerializeField] private Color _bigBand = new Color(0.949f, 0.737f, 0.267f, 1f);
        [SerializeField] private Color _smallWord = new Color(0.541f, 0.463f, 0.376f, 1f);
        [SerializeField] private Color _niceWord = new Color(0.290f, 0.541f, 0.769f, 1f);
        [SerializeField] private Color _bigWord = new Color(0.769f, 0.506f, 0.137f, 1f);
        [SerializeField] private Color _glintColor = new Color(1f, 0.761f, 0.227f, 1f);
        [SerializeField] private Color _goldColor = new Color(0.949f, 0.710f, 0.267f, 1f);

        private readonly RewardCell[] _lot = new RewardCell[RewardCells.Max];
        private UiLabel _titleLabel;
        private UiLabel _sizeLabel;
        private UiLabel _openText;
        private UiLabel _tomorrowLabel;
        private string _smallText;
        private string _niceText;
        private string _bigText;
        private Action _onOpen;
        private Action _onInfo;
        private UiBurst _burst;
        private TodayGiftState _state = (TodayGiftState)255;
        private GiftSize _size;
        private int _count;
        private bool _gold;
        private float _time = -1f;
        private float _before;
        private float _end;
        private float _idle;
        private float _press = -1f;
        private bool _initialized;

        public bool ReduceMotion { get; set; }

        /// <summary>The opening sequence is running.</summary>
        public bool Revealing => _time >= 0f;

        public TodayGiftState State => _state;
        public int CellCount => _count;
        public RectTransform Box => _rock;
        public Button BoxButton => _boxButton;
        public Button OpenButton => _open;
        public Button InfoButton => _info;
        public bool OpenShown => _open != null && _open.gameObject.activeSelf;
        public bool TomorrowShown => _tomorrow != null && _tomorrow.gameObject.activeSelf;
        public string SizeText => _sizeWord != null ? _sizeWord.text : string.Empty;
        public Color BandColor => _band != null ? _band.color : default;

        /// <summary>Cells shown at full size and opacity.</summary>
        public int CellsVisible
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _cells.Length; i++)
                    if (_cells[i].FullyVisible)
                        n++;
                return n;
            }
        }

        public RewardCellView Cell(int i) => _cells[i];

        public void Initialize(Action onOpen, Action onInfo, UiBurst burst)
        {
            _onOpen = onOpen;
            _onInfo = onInfo;
            _burst = burst;
            if (_initialized)
                return;
            _initialized = true;
            _titleLabel = new UiLabel(_title);
            _sizeLabel = new UiLabel(_sizeWord);
            _openText = new UiLabel(_openLabel);
            _tomorrowLabel = new UiLabel(_tomorrow);
            _boxButton.onClick.AddListener(OnBox);
            _open.onClick.AddListener(OnBox);
            if (_info != null)
                _info.onClick.AddListener(OnInfo);
        }

        public void SetTexts(ILocalization text)
        {
            _titleLabel.SetString(text.Get(DailyVisitText.GiftTitle));
            _openText.SetString(text.Get(DailyVisitText.GiftOpen));
            _tomorrowLabel.SetString(text.Get(DailyVisitText.GiftTomorrow));
            _smallText = text.Get(DailyVisitText.GiftSmall);
            _niceText = text.Get(DailyVisitText.GiftNice);
            _bigText = text.Get(DailyVisitText.GiftBig);
            _state = (TodayGiftState)255;
        }

        /// <summary>Shows the model's state; while the opening runs, the sequence keeps the box and finishes on its own.</summary>
        public void Bind(ITodayModel model, RewardIconSet icons, RewardTexts texts)
        {
            if (Revealing)
                return;
            var state = model.Gift;
            if (state == TodayGiftState.Opened)
            {
                ReadLot(model, icons, texts);
                if (_state != TodayGiftState.Opened)
                {
                    for (int i = 0; i < _cells.Length; i++)
                        if (i < _count)
                            _cells[i].ShowNow();
                }
                ShowOpened();
            }
            else
            {
                ShowClosed();
            }
            _state = state;
        }

        /// <summary>The box opens and reveals the lot (doc 05 sheet 18); the model already says Opened.</summary>
        public void PlayOpened(ITodayModel model, RewardIconSet icons, RewardTexts texts)
        {
            ReadLot(model, icons, texts);
            _state = TodayGiftState.Opened;
            ShowClosed();
            SetActive(_open.gameObject, false);
            SetActive(_tomorrow.gameObject, false);
            _sizeLabel.SetString(SizeWord());
            _sizeWord.color = WordColor();
            SetActive(_sizeWord.gameObject, true);
            SetAlpha(_sizeWord, 0f);
            _time = 0f;
            float cellsStart = ReduceMotion ? 0.40f : CellsStart;
            // The cells wait, hidden, for their turn (ShowClosed put them away with the closed box).
            for (int i = 0; i < _cells.Length; i++)
            {
                bool shown = i < _count;
                _cells[i].SetShown(shown);
                if (!shown)
                    continue;
                _cells[i].ReduceMotion = ReduceMotion;
                _cells[i].Reveal(cellsStart + i * CellStagger);
            }
            _end = ReduceMotion ? cellsStart + _count * CellStagger + 0.2f : BaseSeconds + CellStagger * _count;
        }

        /// <summary>Everything shown at once (a touch during the opening, the screen closing).</summary>
        public void Finish()
        {
            if (!Revealing)
                return;
            _time = -1f;
            for (int i = 0; i < _count && i < _cells.Length; i++)
                _cells[i].ShowNow();
            ShowOpened();
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _cells.Length; i++)
                _cells[i].Tick(deltaTime);
            TickPress(deltaTime);
            if (_time >= 0f)
            {
                _before = _time;
                _time += deltaTime;
                if (ReduceMotion)
                    TickCalm();
                else
                    TickOpening();
                if (_time >= _end)
                    Finish();
                return;
            }
            if (_state == TodayGiftState.Ready && !ReduceMotion)
            {
                // Now and then the closed box rocks twice on its base: something waits inside.
                _idle += deltaTime;
                float t = Mathf.Repeat(_idle, IdlePeriod);
                float angle = t < IdleWiggle ? 4f * Mathf.Sin(t / IdleWiggle * Mathf.PI * 4f) * (1f - t / IdleWiggle) : 0f;
                _rock.localEulerAngles = new Vector3(0f, 0f, angle);
            }
        }

        private void TickOpening()
        {
            float t = _time;
            // Anticipation: two 6° swings on the base.
            float angle = t < SwingEnd ? 6f * Mathf.Sin(t / SwingEnd * Mathf.PI * 4f) * (1f - 0.4f * t / SwingEnd) : 0f;
            _rock.localEulerAngles = new Vector3(0f, 0f, angle);
            // The box gives a little as the lid pops (Bond), then settles.
            float squash = t < SwingEnd ? 1f : t < SwingEnd + 0.08f ? Mathf.Lerp(1f, 0.94f, (t - SwingEnd) / 0.08f)
                : t < SwingEnd + 0.23f ? Mathf.Lerp(0.94f, 1.03f, UiCurves.Smooth((t - SwingEnd - 0.08f) / 0.15f))
                : Mathf.Lerp(1.03f, 1f, UiCurves.Smooth((t - SwingEnd - 0.23f) / 0.15f));
            _rock.localScale = new Vector3(2f - squash, squash, 1f);

            // Impact: the lid jumps up, then lands beside the box, tilted.
            Vector2 lid;
            float lidAngle;
            if (t < SwingEnd)
            {
                lid = Vector2.zero;
                lidAngle = 0f;
            }
            else if (t < HopEnd)
            {
                float k = UiCurves.EaseOut((t - SwingEnd) / (HopEnd - SwingEnd));
                lid = new Vector2(0f, HopHeight * k);
                lidAngle = -4f * k;
            }
            else
            {
                float k = Mathf.Clamp01((t - HopEnd) / (LandEnd - HopEnd));
                float e = UiCurves.Smooth(k);
                float arc = 4f * k * (1f - k) * 40f;
                lid = new Vector2(_lidRest.x * e, Mathf.Lerp(HopHeight, _lidRest.y, e * e) + arc);
                lidAngle = Mathf.Lerp(-4f, _lidRestAngle, e);
            }
            _lidPivot.anchoredPosition = lid;
            _lidPivot.localEulerAngles = new Vector3(0f, 0f, lidAngle);

            // The band takes the color of the lot as the lid leaves.
            float bandK = Mathf.Clamp01((t - SwingEnd - 0.05f) / 0.2f);
            _band.color = Color.Lerp(_closedBand, SizeBand(_size), bandK);
            if (_before < BurstAt && t >= BurstAt)
                Burst();
            SetAlpha(_sizeWord, Mathf.Clamp01((t - 0.5f) / 0.2f));
            float wordScale = t < 0.5f ? 0f : UiCurves.Pop(t - 0.5f);
            _sizeWord.rectTransform.localScale = new Vector3(wordScale, wordScale, 1f);
        }

        private void TickCalm()
        {
            float t = _time;
            _rock.localEulerAngles = Vector3.zero;
            _rock.localScale = Vector3.one;
            // The lid fades away, then fades in beside the box.
            if (t < 0.15f)
            {
                SetAlpha(_lid, 1f - t / 0.15f);
            }
            else
            {
                _lidPivot.anchoredPosition = _lidRest;
                _lidPivot.localEulerAngles = new Vector3(0f, 0f, _lidRestAngle);
                SetAlpha(_lid, Mathf.Clamp01((t - 0.15f) / 0.15f));
            }
            _band.color = Color.Lerp(_closedBand, SizeBand(_size), Mathf.Clamp01((t - 0.1f) / 0.2f));
            if (_before < 0.15f && t >= 0.15f)
                Burst();
            _sizeWord.rectTransform.localScale = Vector3.one;
            SetAlpha(_sizeWord, Mathf.Clamp01((t - 0.2f) / 0.2f));
        }

        private void Burst()
        {
            if (_burst != null)
                _burst.Play(UiBurstKind.Gift, _rock, _glintColor, _goldColor, _gold);
        }

        private void TickPress(float deltaTime)
        {
            if (_press < 0f || Revealing)
                return;
            _press += deltaTime;
            float s = UiCurves.Press(_press);
            _rock.localScale = new Vector3(s, s, 1f);
            if (_press >= UiCurves.PressSeconds)
            {
                _press = -1f;
                _rock.localScale = Vector3.one;
            }
        }

        private void ReadLot(ITodayModel model, RewardIconSet icons, RewardTexts texts)
        {
            _size = model.GiftSize;
            _count = Mathf.Min(model.GiftRewards(_lot), _cells.Length);
            _gold = false;
            float left = -(_count - 1) * 0.5f * _cellPitch;
            for (int i = 0; i < _cells.Length; i++)
            {
                bool shown = i < _count;
                _cells[i].SetShown(shown);
                if (!shown)
                    continue;
                _cells[i].Bind(_lot[i], icons, texts);
                var rect = _cells[i].Rect;
                rect.anchoredPosition = new Vector2(left + i * _cellPitch, rect.anchoredPosition.y);
                if (_lot[i].Kind == RewardKind.Gems || _lot[i].Kind == RewardKind.DiscoveryEgg)
                    _gold = true;
            }
        }

        private void ShowClosed()
        {
            _rock.localEulerAngles = Vector3.zero;
            _rock.localScale = Vector3.one;
            _lidPivot.anchoredPosition = Vector2.zero;
            _lidPivot.localEulerAngles = Vector3.zero;
            SetAlpha(_lid, 1f);
            _band.color = _closedBand;
            SetActive(_open.gameObject, true);
            SetActive(_tomorrow.gameObject, false);
            SetActive(_sizeWord.gameObject, false);
            for (int i = 0; i < _cells.Length; i++)
                _cells[i].SetShown(false);
        }

        private void ShowOpened()
        {
            _rock.localEulerAngles = Vector3.zero;
            _rock.localScale = Vector3.one;
            _lidPivot.anchoredPosition = _lidRest;
            _lidPivot.localEulerAngles = new Vector3(0f, 0f, _lidRestAngle);
            SetAlpha(_lid, 1f);
            _band.color = SizeBand(_size);
            SetActive(_open.gameObject, false);
            SetActive(_tomorrow.gameObject, true);
            _sizeLabel.SetString(SizeWord());
            _sizeWord.color = WordColor();
            _sizeWord.rectTransform.localScale = Vector3.one;
            SetActive(_sizeWord.gameObject, true);
            for (int i = 0; i < _cells.Length; i++)
                _cells[i].SetShown(i < _count);
        }

        private string SizeWord() => _size == GiftSize.Big ? _bigText : _size == GiftSize.Nice ? _niceText : _smallText;

        private Color WordColor() => _size == GiftSize.Big ? _bigWord : _size == GiftSize.Nice ? _niceWord : _smallWord;

        private Color SizeBand(GiftSize size) => size == GiftSize.Big ? _bigBand : size == GiftSize.Nice ? _niceBand : _smallBand;

        private void OnBox()
        {
            if (Revealing)
            {
                // Touch to pass (doc 05 section 0.3): the whole lot at once.
                Finish();
                return;
            }
            if (_state != TodayGiftState.Ready)
            {
                // Every touch answers: the open box gives a little.
                if (!ReduceMotion)
                    _press = 0f;
                return;
            }
            _onOpen?.Invoke();
        }

        private void OnInfo() => _onInfo?.Invoke();

        private static void SetActive(GameObject go, bool active)
        {
            if (go.activeSelf != active)
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
            _boxButton.onClick.RemoveListener(OnBox);
            _open.onClick.RemoveListener(OnBox);
            if (_info != null)
                _info.onClick.RemoveListener(OnInfo);
        }

        /// <summary>Prefab builder hook (Editor DailyVisitUiPipeline).</summary>
        public void SetReferences(RectTransform card, Button boxButton, RectTransform rock, Image band, RectTransform lidPivot, Image lid, TMP_Text title,
            TMP_Text sizeWord, Button open, TMP_Text openLabel, TMP_Text tomorrow, Button info, RewardCellView[] cells, float cellPitch, Vector2 lidRest)
        {
            _card = card;
            _boxButton = boxButton;
            _rock = rock;
            _band = band;
            _lidPivot = lidPivot;
            _lid = lid;
            _title = title;
            _sizeWord = sizeWord;
            _open = open;
            _openLabel = openLabel;
            _tomorrow = tomorrow;
            _info = info;
            _cells = cells;
            _cellPitch = cellPitch;
            _lidRest = lidRest;
        }
    }
}
