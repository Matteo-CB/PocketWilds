using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_HUD_Main (T17 scope): Light chip and the hatch action, numbers and icons only. The chip and
    /// the action sit on their own nested canvases so the ticking counter never rebuilds the button.
    /// States use color tokens and scale, not extra sprites. No Update: ticked by the scene controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RefugeHud : MonoBehaviour, IRefugeHud, IProgressionHud, IMotionSettings
    {
        private const float PulseSeconds = 0.4f;

        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private RectTransform _safeArea;

        [Header("Light chip")]
        [SerializeField] private RectTransform _lightChip;
        [SerializeField] private TMP_Text _lightLabel;
        [SerializeField] private TMP_Text _rateLabel;

        [Header("Hatch action")]
        [SerializeField] private Button _hatchButton;
        [SerializeField] private RectTransform _hatchRoot;
        [SerializeField] private Image _hatchBackground;
        [SerializeField] private Image _eggIcon;
        [SerializeField] private GameObject _costRow;
        [SerializeField] private TMP_Text _costLabel;

        [Header("Mastery toast")]
        [SerializeField] private RectTransform _masteryRoot;
        [SerializeField] private CanvasGroup _masteryGroup;
        [SerializeField] private RectTransform _masteryIcon;
        [SerializeField] private TMP_Text _masteryLevel;
        [SerializeField] private TMP_Text _masteryXp;
        [SerializeField] private RectTransform _masteryBar;
        [SerializeField] private RectTransform _masteryFill;
        [SerializeField] private CanvasGroup _masteryGlow;

        [Header("Shortcuts")]
        [SerializeField] private Button _codexButton;
        [SerializeField] private RectTransform _codexRoot;
        [SerializeField] private GameObject _codexBadge;

        [Header("Notice (T34): the device is warm, the picture is lighter")]
        [SerializeField] private RectTransform _noticeRoot;
        [SerializeField] private CanvasGroup _noticeGroup;
        [SerializeField] private TMP_Text _noticeLabel;

        [Header("Shop (T28): shown once unlocked")]
        [SerializeField] private Button _shopButton;
        [SerializeField] private RectTransform _shopRoot;
        [SerializeField] private RectTransform _gemsChip;
        [SerializeField] private TMP_Text _gemsLabel;

        [Header("Feedback (T34A)")]
        [SerializeField] private RevealBanner _reveal;
        [SerializeField] private ToastQueue _toasts;
        [SerializeField] private TapHint _tapHint;
        [SerializeField] private CollectMotes _collect;
        [SerializeField] private Button _infoButton;
        [SerializeField] private RectTransform _infoRoot;

        [Header("Progression (T35A): Upgrades button, refuge level badge")]
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private RectTransform _upgradeRoot;
        [SerializeField] private GameObject _upgradeDot;
        [SerializeField] private Button _levelButton;
        [SerializeField] private RectTransform _levelRoot;
        [SerializeField] private RectTransform _levelMedal;
        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private RectTransform _levelRing;
        [SerializeField] private Image _levelRingImage;
        [SerializeField] private RectTransform[] _levelPaws = System.Array.Empty<RectTransform>();
        [SerializeField] private Image[] _levelPawImages = System.Array.Empty<Image>();
        [SerializeField] private Button _lightButton;
        [SerializeField] private GameObject _levelDot;

        private IRefugeActions _actions;
        private IProgressionHudActions _progressionActions;
        private bool _upgradeShown;
        private bool _upgradeDotShown;
        private bool _upgradeHint;
        private bool _eggHint;
        private int _levelShown = -1;
        private readonly UiNumberText _levelText = new UiNumberText();
        private float _levelUpTime = -1f;
        private bool _levelDotShown;
        private const float LevelUpSeconds = 1.2f;
        private BigValueText _light;
        private BigValueText _rate;
        private BigValueText _cost;
        private bool _bound;
        private int _state = -1;
        private bool _freeShown;
        private bool _hatchingNow;
        private float _pulse = -1f;
        private float _breath;
        private Vector2 _chipHome;
        private Rect _appliedSafeArea;
        private Vector2Int _appliedScreen;
        private bool _badgeShown;
        private bool _shopShown;
        private long _gemsShown = -1;
        private BigValueText _gems;
        private readonly UiNumberText _masteryLevelText = new UiNumberText();
        private readonly UiNumberText _masteryXpText = new UiNumberText();
        private MasteryGain _mastery;
        private float _masteryTime = -1f;
        private bool _masteryLevelShown;
        private float _noticeAlpha;
        private float _noticeTarget;

        private const float ToastIn = 0.18f;
        private const float ToastFillStart = 0.15f;
        private const float ToastFill = 0.45f;
        private const float ToastHold = 1.9f;
        private const float ToastOut = 0.25f;

        private bool _reduceMotion;

        /// <summary>Reduce Motion: no breathing or shaking, states stay readable through color. Passed to every part.</summary>
        public bool ReduceMotion
        {
            get => _reduceMotion;
            set
            {
                _reduceMotion = value;
                if (_reveal != null) _reveal.ReduceMotion = value;
                if (_toasts != null) _toasts.ReduceMotion = value;
                if (_tapHint != null) _tapHint.ReduceMotion = value;
                if (_collect != null) _collect.ReduceMotion = value;
            }
        }

        public RevealBanner Reveal => _reveal;
        public ToastQueue Toasts => _toasts;
        public TapHint TapHint => _tapHint;
        public CollectMotes Collect => _collect;

        public bool IsBound => _bound;
        public bool CostVisible => _costRow != null && _costRow.activeSelf;
        public int VisualState => _state;

        public void Bind(IRefugeActions actions)
        {
            _actions = actions;
            if (_bound)
                return;
            _bound = true;
            _light = new BigValueText(_lightLabel);
            _rate = new BigValueText(_rateLabel, "+", "/s");
            _cost = new BigValueText(_costLabel);
            _chipHome = _lightChip.anchoredPosition;
            _freeShown = !_costRow.activeSelf;
            _hatchButton.onClick.AddListener(OnHatchPressed);
            if (_codexButton != null)
                _codexButton.onClick.AddListener(OnCodexPressed);
            if (_infoButton != null)
                _infoButton.onClick.AddListener(OnInfoPressed);
            if (_shopButton != null)
            {
                _shopButton.onClick.AddListener(OnShopPressed);
                _gems = new BigValueText(_gemsLabel);
                _shopShown = _shopRoot.gameObject.activeSelf;
            }
            _badgeShown = _codexBadge != null && _codexBadge.activeSelf;
            if (_upgradeRoot != null)
            {
                _upgradeShown = _upgradeRoot.gameObject.activeSelf;
                _upgradeDotShown = _upgradeDot.activeSelf;
            }
            if (_masteryRoot != null)
                _masteryRoot.gameObject.SetActive(false);
            _reveal?.Hide();
            _toasts?.Clear();
            ReduceMotion = _reduceMotion;
            FitSafeArea();
        }

        public void SetLight(BigValue light, BigValue perSecond)
        {
            if (!_bound)
                return;
            _light.Set(light);
            _rate.Set(perSecond);
        }

        public void SetEgg(BigValue cost, bool free, bool affordable)
        {
            if (!_bound)
                return;
            if (free != _freeShown)
            {
                // The tutorial egg needs no reading: no price, just the inviting button.
                _freeShown = free;
                _costRow.SetActive(!free);
            }
            if (!free)
                _cost.Set(cost);

            int state = _hatchingNow ? 2 : (affordable ? 1 : 0);
            if (state == _state)
                return;
            _state = state;
            _hatchBackground.color = state == 2 ? _tokens.accentBusy : (state == 1 ? _tokens.accentPrimary : _tokens.accentIdle);
            _costLabel.color = state == 0 ? _tokens.errorSoft : _tokens.textPrimary;
            _eggIcon.color = state == 0 ? new Color(1f, 1f, 1f, 0.8f) : Color.white;
        }

        public void SetHatching(bool hatching)
        {
            _hatchingNow = hatching;
            _state = -1; // colors refresh on the next SetEgg
        }

        public void PulseNotEnough() => _pulse = 0f;

        public bool MasteryVisible => _masteryTime >= 0f;
        public int MasteryLevelShown { get; private set; }

        public void ShowMastery(in MasteryGain gain)
        {
            if (!_bound || _masteryRoot == null || !gain.HasGain)
                return;
            _mastery = gain;
            _masteryTime = 0f;
            _masteryLevelShown = false;
            _masteryRoot.gameObject.SetActive(true);
            MasteryLevelShown = gain.Before.Level;
            _masteryLevelText.SetCount(_masteryLevel, gain.Before.Level);
            _masteryXpText.SetCount(_masteryXp, gain.Xp, '+');
            SetMasteryFill(gain.Before.Fraction);
            _masteryGlow.alpha = 0f;
        }

        /// <summary>T34: a calm one-line note under the Light chip; null fades it out.</summary>
        public void SetNotice(string text)
        {
            if (_noticeRoot == null)
                return;
            if (string.IsNullOrEmpty(text))
            {
                _noticeTarget = 0f;
                return;
            }
            _noticeLabel.text = text;
            _noticeTarget = 1f;
            _noticeRoot.gameObject.SetActive(true);
        }

        public bool NoticeVisible => _noticeTarget > 0f;

        public void ShowReveal(in RevealInfo info) => _reveal?.Show(info);

        public void ShowToast(ToastKind kind, string text) => _toasts?.Enqueue(kind, text);

        public void SetTapHint(bool visible, Vector2 screenPosition)
        {
            // The first egg's hint wins over the Upgrades button's (they never meet in play, tests may ask for both).
            _eggHint = visible;
            _tapHint?.Set(visible, screenPosition);
        }

        // ---------------- T35A progression parts ----------------

        public void BindProgression(IProgressionHudActions actions)
        {
            _progressionActions = actions;
            if (_upgradeButton != null)
            {
                _upgradeButton.onClick.RemoveListener(OnUpgradePressed);
                _upgradeButton.onClick.AddListener(OnUpgradePressed);
            }
            if (_levelButton != null)
            {
                _levelButton.onClick.RemoveListener(OnLevelPressed);
                _levelButton.onClick.AddListener(OnLevelPressed);
            }
            if (_lightButton != null)
            {
                _lightButton.onClick.RemoveListener(OnUpgradePressed);
                _lightButton.onClick.AddListener(OnUpgradePressed);
            }
            if (_levelRing != null)
                _levelRing.gameObject.SetActive(false);
            for (int i = 0; i < _levelPaws.Length; i++)
                _levelPaws[i].gameObject.SetActive(false);
        }

        public bool UpgradeVisible => _upgradeShown;
        public bool UpgradeDotVisible => _upgradeDotShown;
        public bool UpgradeHintShown => _upgradeHint && !_eggHint;
        public string LevelText => _levelLabel != null ? _levelLabel.text : string.Empty;
        public Button UpgradeButton => _upgradeButton;
        public Button LevelButton => _levelButton;
        public Button LightButton => _lightButton;
        public bool LevelUpPlaying => _levelUpTime >= 0f;

        public void SetUpgrade(bool visible, bool dot)
        {
            if (_upgradeRoot == null)
                return;
            if (visible != _upgradeShown)
            {
                _upgradeShown = visible;
                _upgradeRoot.gameObject.SetActive(visible);
            }
            bool showDot = visible && dot;
            if (showDot != _upgradeDotShown)
            {
                _upgradeDotShown = showDot;
                _upgradeDot.SetActive(showDot);
            }
        }

        public void SetUpgradeHint(bool visible)
        {
            if (visible == _upgradeHint)
                return;
            _upgradeHint = visible;
            if (!visible && !_eggHint)
                _tapHint?.Set(false, default);
        }

        public void SetRefugeLevel(int level, float fraction)
        {
            if (_levelLabel == null || level == _levelShown)
                return;
            _levelShown = level;
            _levelText.SetCount(_levelLabel, level);
        }

        public void SetLevelReward(bool pending)
        {
            if (_levelDot == null || pending == _levelDotShown)
                return;
            _levelDotShown = pending;
            _levelDot.SetActive(pending);
        }

        public bool LevelRewardDotVisible => _levelDotShown;

        public void PlayRefugeLevelUp()
        {
            if (_levelRoot == null)
                return;
            _levelUpTime = 0f;
            if (ReduceMotion)
                return;
            _levelRing.gameObject.SetActive(true);
            for (int i = 0; i < _levelPaws.Length; i++)
                _levelPaws[i].gameObject.SetActive(true);
        }

        /// <summary>
        /// The badge grows and settles; a ring and a ring of paws spread from it and fade (section 11.6, 1.2 s). With
        /// Reduce Motion the number simply changes (sound and vibration stay).
        /// </summary>
        private void TickLevelUp(float dt)
        {
            if (_levelUpTime < 0f)
                return;
            _levelUpTime += dt;
            float t = Mathf.Clamp01(_levelUpTime / LevelUpSeconds);
            if (!ReduceMotion)
            {
                float grow = t < 0.25f ? Mathf.Lerp(1f, 1.3f, t / 0.25f) : Mathf.Lerp(1.3f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.25f) / 0.75f));
                _levelMedal.localScale = new Vector3(grow, grow, 1f);
                float spread = 1f - (1f - t) * (1f - t);
                float ring = Mathf.Lerp(0.8f, 2.2f, spread);
                _levelRing.localScale = new Vector3(ring, ring, 1f);
                var color = _levelRingImage.color;
                color.a = 0.9f * (1f - t);
                _levelRingImage.color = color;
                for (int i = 0; i < _levelPaws.Length; i++)
                {
                    float angle = (i / (float)_levelPaws.Length) * Mathf.PI * 2f + 0.3f;
                    float radius = Mathf.Lerp(50f, 150f, spread);
                    _levelPaws[i].anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    _levelPaws[i].localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
                    var paw = _levelPawImages[i].color;
                    paw.a = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
                    _levelPawImages[i].color = paw;
                }
            }
            if (t < 1f)
                return;
            _levelUpTime = -1f;
            _levelMedal.localScale = Vector3.one;
            _levelRing.gameObject.SetActive(false);
            for (int i = 0; i < _levelPaws.Length; i++)
                _levelPaws[i].gameObject.SetActive(false);
        }

        private void TickUpgradeHint()
        {
            if (_tapHint == null || !_upgradeHint || _eggHint || !_upgradeShown)
                return;
            var center = _upgradeRoot.TransformPoint(_upgradeRoot.rect.center);
            _tapHint.Set(true, RectTransformUtility.WorldToScreenPoint(null, center));
        }

        private void OnUpgradePressed() => _progressionActions?.OpenUpgrades();

        private void OnLevelPressed() => _progressionActions?.OpenRefugeLevel();

        /// <summary>Prefab builder hook for the T35A parts (Editor ProgressionUiPipeline).</summary>
        public void SetProgressionReferences(Button upgradeButton, RectTransform upgradeRoot, GameObject upgradeDot, Button levelButton,
            RectTransform levelRoot, RectTransform levelMedal, TMP_Text levelLabel, RectTransform levelRing, Image levelRingImage,
            RectTransform[] levelPaws, Image[] levelPawImages, Button lightButton, GameObject levelDot)
        {
            _levelDot = levelDot;
            _upgradeButton = upgradeButton;
            _upgradeRoot = upgradeRoot;
            _upgradeDot = upgradeDot;
            _levelButton = levelButton;
            _levelRoot = levelRoot;
            _levelMedal = levelMedal;
            _levelLabel = levelLabel;
            _levelRing = levelRing;
            _levelRingImage = levelRingImage;
            _levelPaws = levelPaws;
            _levelPawImages = levelPawImages;
            _lightButton = lightButton;
        }

        public void PlayCollect(Vector2 fromScreenPosition) => _collect?.Play(fromScreenPosition);

        public void PrewarmFeedback(string sampleTitle, string sampleText)
        {
            if (_reveal != null)
            {
                _reveal.Show(new RevealInfo(sampleTitle, sampleText, Content.RarityTier.Fabulous, HatchNews.NewSpecies));
                Canvas.ForceUpdateCanvases();
                _reveal.Hide();
            }
            if (_toasts != null)
            {
                _toasts.Enqueue(ToastKind.Warning, sampleText);
                _toasts.Tick(0f);
                Canvas.ForceUpdateCanvases();
                _toasts.Clear();
            }
        }
        public string NoticeText => _noticeLabel != null ? _noticeLabel.text : string.Empty;

        public void SetCodexBadge(bool visible)
        {
            if (_codexBadge == null || visible == _badgeShown)
                return;
            _badgeShown = visible;
            _codexBadge.SetActive(visible);
        }

        public bool CodexBadgeVisible => _badgeShown;

        public bool ShopVisible => _shopShown;
        public string GemsText => _gemsLabel != null ? _gemsLabel.text : string.Empty;

        public void SetShop(bool unlocked, long gems)
        {
            if (!_bound || _shopRoot == null)
                return;
            if (unlocked != _shopShown)
            {
                _shopShown = unlocked;
                _shopRoot.gameObject.SetActive(unlocked);
                _gemsChip.gameObject.SetActive(unlocked);
            }
            if (unlocked && gems != _gemsShown)
            {
                _gemsShown = gems;
                _gems.Set(BigValue.FromDouble(gems));
            }
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_bound)
                return;
            FitSafeArea();
            if (_noticeRoot != null && _noticeRoot.gameObject.activeSelf)
            {
                // Fades in and out over a third of a second; hidden (inactive) once faded out.
                _noticeAlpha = Mathf.MoveTowards(_noticeAlpha, _noticeTarget, unscaledDeltaTime * 3f);
                _noticeGroup.alpha = _noticeAlpha;
                if (_noticeAlpha <= 0f && _noticeTarget <= 0f)
                    _noticeRoot.gameObject.SetActive(false);
            }

            float scale = 1f;
            if (!ReduceMotion)
            {
                // An affordable egg breathes softly to invite the touch.
                _breath += unscaledDeltaTime;
                if (_state == 1)
                    scale = 1f + 0.035f * (0.5f - 0.5f * Mathf.Cos(_breath * 4.2f));
            }
            if (_pulse >= 0f)
            {
                // "Not yet": the Light chip shakes, the button dips. Short and soft.
                _pulse += unscaledDeltaTime;
                float decay = Mathf.Clamp01(1f - _pulse / PulseSeconds);
                float shake = ReduceMotion ? 0f : 14f * decay * Mathf.Sin(_pulse * 42f);
                _lightChip.anchoredPosition = _chipHome + new Vector2(shake, 0f);
                scale *= 1f - 0.06f * decay;
                if (decay <= 0f)
                {
                    _pulse = -1f;
                    _lightChip.anchoredPosition = _chipHome;
                }
            }
            var current = _hatchRoot.localScale;
            if (current.x != scale)
                _hatchRoot.localScale = new Vector3(scale, scale, 1f);
            TickMastery(unscaledDeltaTime);
            if (_reveal != null)
                _reveal.Tick(unscaledDeltaTime);
            if (_toasts != null)
            {
                // Toasts share the slot under the chip with the heat notice: they wait while it shows.
                _toasts.Paused = _noticeRoot != null && _noticeRoot.gameObject.activeSelf;
                _toasts.Tick(unscaledDeltaTime);
            }
            TickUpgradeHint();
            TickLevelUp(unscaledDeltaTime);
            if (_tapHint != null)
                _tapHint.Tick(unscaledDeltaTime);
            if (_collect != null)
                _collect.Tick(unscaledDeltaTime);
        }

        /// <summary>
        /// Toast timeline: appear, bar fills toward the new XP; on a level up it fills to the end, the level
        /// number pops with a glow, then the bar refills from zero. Holds briefly, fades out.
        /// </summary>
        private void TickMastery(float deltaTime)
        {
            if (_masteryTime < 0f)
                return;
            _masteryTime += deltaTime;
            float t = _masteryTime;
            float appear = Mathf.Clamp01(t / ToastIn);
            float fade = Mathf.Clamp01((ToastHold + ToastOut - t) / ToastOut);
            _masteryGroup.alpha = Mathf.Min(appear, fade);
            float pop = 1f;

            float fill = Mathf.Clamp01((t - ToastFillStart) / ToastFill);
            float eased = 1f - (1f - fill) * (1f - fill);
            if (!_mastery.LeveledUp)
            {
                SetMasteryFill(Mathf.Lerp(_mastery.Before.Fraction, _mastery.After.Fraction, eased));
            }
            else if (fill < 0.55f)
            {
                SetMasteryFill(Mathf.Lerp(_mastery.Before.Fraction, 1f, eased / 0.7975f));
            }
            else
            {
                if (!_masteryLevelShown)
                {
                    _masteryLevelShown = true;
                    MasteryLevelShown = _mastery.After.Level;
                    _masteryLevelText.SetCount(_masteryLevel, _mastery.After.Level);
                }
                float after = Mathf.Clamp01((fill - 0.55f) / 0.45f);
                SetMasteryFill(Mathf.Lerp(0f, _mastery.After.Fraction, after));
                float since = (fill - 0.55f) * ToastFill;
                float glow = Mathf.Clamp01(1f - since / 0.6f);
                _masteryGlow.alpha = glow;
                if (!ReduceMotion)
                    pop = 1f + 0.18f * glow * glow;
            }
            if (!ReduceMotion)
            {
                float enter = 0.85f + 0.15f * (1f - (1f - appear) * (1f - appear));
                _masteryRoot.localScale = new Vector3(enter, enter, 1f);
                _masteryIcon.localScale = new Vector3(pop, pop, 1f);
            }
            if (t >= ToastHold + ToastOut)
            {
                _masteryTime = -1f;
                _masteryRoot.gameObject.SetActive(false);
            }
        }

        private void SetMasteryFill(float fraction)
        {
            float width = _masteryBar.rect.width;
            float height = _masteryBar.rect.height;
            bool visible = fraction > 0.001f;
            if (_masteryFill.gameObject.activeSelf != visible)
                _masteryFill.gameObject.SetActive(visible);
            _masteryFill.sizeDelta = new Vector2(Mathf.Max(height, width * Mathf.Clamp01(fraction)), _masteryFill.sizeDelta.y);
        }

        public bool IsOverUi(Vector2 screenPosition) =>
            RectTransformUtility.RectangleContainsScreenPoint(_hatchRoot, screenPosition, null)
            || RectTransformUtility.RectangleContainsScreenPoint(_lightChip, screenPosition, null)
            || (_codexRoot != null && RectTransformUtility.RectangleContainsScreenPoint(_codexRoot, screenPosition, null))
            || (_infoRoot != null && RectTransformUtility.RectangleContainsScreenPoint(_infoRoot, screenPosition, null))
            || (_upgradeShown && RectTransformUtility.RectangleContainsScreenPoint(_upgradeRoot, screenPosition, null))
            || (_levelRoot != null && RectTransformUtility.RectangleContainsScreenPoint(_levelRoot, screenPosition, null))
            || (_shopShown && (RectTransformUtility.RectangleContainsScreenPoint(_shopRoot, screenPosition, null)
                               || RectTransformUtility.RectangleContainsScreenPoint(_gemsChip, screenPosition, null)));

        /// <summary>Prefab builder hook (Editor UiPipeline).</summary>
        public void SetReferences(UiColorTokens tokens, RectTransform safeArea, RectTransform lightChip, TMP_Text lightLabel,
            TMP_Text rateLabel, Button hatchButton, RectTransform hatchRoot, Image hatchBackground, Image eggIcon,
            GameObject costRow, TMP_Text costLabel)
        {
            _tokens = tokens;
            _safeArea = safeArea;
            _lightChip = lightChip;
            _lightLabel = lightLabel;
            _rateLabel = rateLabel;
            _hatchButton = hatchButton;
            _hatchRoot = hatchRoot;
            _hatchBackground = hatchBackground;
            _eggIcon = eggIcon;
            _costRow = costRow;
            _costLabel = costLabel;
        }

        private void FitSafeArea()
        {
            if (_safeArea == null)
                return;
            var screen = new Vector2Int(Screen.width, Screen.height);
            var safe = Screen.safeArea;
            if (safe == _appliedSafeArea && screen == _appliedScreen)
                return;
            _appliedSafeArea = safe;
            _appliedScreen = screen;
            if (screen.x <= 0 || screen.y <= 0)
                return;
            _safeArea.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            _safeArea.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;
        }

        /// <summary>Prefab builder hook for the mastery toast (Editor UiPipeline).</summary>
        public void SetMasteryReferences(RectTransform root, CanvasGroup group, RectTransform icon, TMP_Text level, TMP_Text xp,
            RectTransform bar, RectTransform fill, CanvasGroup glow)
        {
            _masteryRoot = root;
            _masteryGroup = group;
            _masteryIcon = icon;
            _masteryLevel = level;
            _masteryXp = xp;
            _masteryBar = bar;
            _masteryFill = fill;
            _masteryGlow = glow;
        }

        /// <summary>Prefab builder hook for the shortcut row (Editor UiPipeline).</summary>
        public void SetShortcutReferences(Button codexButton, RectTransform codexRoot, GameObject codexBadge)
        {
            _codexButton = codexButton;
            _codexRoot = codexRoot;
            _codexBadge = codexBadge;
        }

        private void OnHatchPressed() => _actions?.RequestHatch();

        private void OnCodexPressed() => _actions?.OpenCodex();

        private void OnShopPressed() => _actions?.OpenShop();

        private void OnInfoPressed() => _actions?.OpenEggInfo();

        /// <summary>Prefab builder hook for the T34A feedback parts (Editor FeedbackUiPipeline).</summary>
        public void SetFeedbackReferences(RevealBanner reveal, ToastQueue toasts, TapHint tapHint, CollectMotes collect,
            Button infoButton, RectTransform infoRoot)
        {
            _reveal = reveal;
            _toasts = toasts;
            _tapHint = tapHint;
            _collect = collect;
            _infoButton = infoButton;
            _infoRoot = infoRoot;
        }

        /// <summary>Prefab builder hook for the shop shortcut and Gems chip (Editor ShopUiPipeline).</summary>
        public void SetNoticeReferences(RectTransform root, CanvasGroup group, TMP_Text label)
        {
            _noticeRoot = root;
            _noticeGroup = group;
            _noticeLabel = label;
        }

        public void SetShopReferences(Button shopButton, RectTransform shopRoot, RectTransform gemsChip, TMP_Text gemsLabel)
        {
            _shopButton = shopButton;
            _shopRoot = shopRoot;
            _gemsChip = gemsChip;
            _gemsLabel = gemsLabel;
        }

        private void OnDestroy()
        {
            if (_hatchButton != null)
                _hatchButton.onClick.RemoveListener(OnHatchPressed);
            if (_codexButton != null)
                _codexButton.onClick.RemoveListener(OnCodexPressed);
            if (_shopButton != null)
                _shopButton.onClick.RemoveListener(OnShopPressed);
            if (_infoButton != null)
                _infoButton.onClick.RemoveListener(OnInfoPressed);
            if (_upgradeButton != null)
                _upgradeButton.onClick.RemoveListener(OnUpgradePressed);
            if (_levelButton != null)
                _levelButton.onClick.RemoveListener(OnLevelPressed);
            if (_lightButton != null)
                _lightButton.onClick.RemoveListener(OnUpgradePressed);
        }
    }
}
