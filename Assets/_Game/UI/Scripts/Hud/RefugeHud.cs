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
    /// T36 (doc 06 screen 002, doc 01 section 1.3): the contextual right rail with the Today button and its dot, the Gems
    /// chip shown from the first Gem (split from the shop), Gems motes, and the wish card it carries for the presenter.
    /// T37 (doc 01 A15, doc « Mini-jeux & activités » section 2): the Play button above Today on the rail, with the tickets
    /// owned in a small round badge and its dot; while a game plays the whole HUD fades out and catches no touch.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RefugeHud : MonoBehaviour, IRefugeHud, IProgressionHud, IEggChoiceHud, IDailyHud, IPlayHud, IAlbumHud, IProfileHud, IMotionSettings
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

        [Header("T35B egg choice")]
        [SerializeField] private EggChoiceBar _eggChoice;
        [SerializeField] private Image _costIcon;
        [SerializeField] private TMP_Text _priceWord;

        [Header("T36 rendez-vous: right rail (Play, Today, Event), Gems motes, wish card")]
        [SerializeField] private RectTransform _rail;
        [SerializeField] private RectTransform[] _railItems = System.Array.Empty<RectTransform>();
        [SerializeField] private Button _todayButton;
        [SerializeField] private RectTransform _todayRoot;
        [SerializeField] private CanvasGroup _todayGroup;
        [SerializeField] private RectTransform _todayIcon;
        [SerializeField] private RectTransform _todayDot;
        [SerializeField] private CollectMotes _gemsCollect;
        [SerializeField] private WishCardView _wishCard;

        [Header("T37 activities: the Play button on the rail, the HUD hidden while a game plays")]
        [SerializeField] private Button _playButton;
        [SerializeField] private RectTransform _playRoot;
        [SerializeField] private CanvasGroup _playGroup;
        [SerializeField] private RectTransform _playIcon;
        [SerializeField] private RectTransform _playBadge;
        [SerializeField] private TMP_Text _playCount;
        [SerializeField] private RectTransform _playDot;
        [SerializeField] private CanvasGroup _rootGroup;

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
                if (_eggChoice != null) _eggChoice.ReduceMotion = value;
                if (_gemsCollect != null) _gemsCollect.ReduceMotion = value;
                if (_wishCard != null) _wishCard.ReduceMotion = value;
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
                _shopShown = _shopRoot.gameObject.activeSelf;
            }
            if (_gemsLabel != null)
            {
                _gems = new BigValueText(_gemsLabel);
                _gemsChipShown = _gemsChip.gameObject.activeSelf;
            }
            if (_todayRoot != null)
            {
                _todayShown = _todayRoot.gameObject.activeSelf;
                _todayDotShown = _todayDot != null && _todayDot.gameObject.activeSelf;
            }
            if (_playRoot != null)
            {
                _playShown = _playRoot.gameObject.activeSelf;
                _playDotShown = _playDot != null && _playDot.gameObject.activeSelf;
                if (_playIcon != null)
                    _playIconHome = _playIcon.anchoredPosition;
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
            if (!free && _priceText == null)
                _cost.Set(cost);

            int state = _hatchingNow ? 2 : (affordable ? 1 : 0);
            if (state == _state)
                return;
            _state = state;
            _hatchBackground.color = state == 2 ? _tokens.accentBusy : (state == 1 ? _tokens.accentPrimary : _tokens.accentIdle);
            _costLabel.color = state == 0 ? _tokens.errorSoft : _tokens.textPrimary;
            _eggIcon.color = state == 0 ? new Color(1f, 1f, 1f, 0.8f) : Color.white;
            if (_eggChoice != null)
                _eggChoice.SetButtonAlpha(state == 0 ? 0.8f : 1f);
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
            if (_eggChoice != null)
                _eggChoice.HideName();
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

        /// <summary>The shop shortcut. The Gems chip shows when the shop or <see cref="SetGems"/> asks for it (T36 split).</summary>
        public void SetShop(bool unlocked, long gems)
        {
            if (!_bound || _shopRoot == null)
                return;
            if (unlocked != _shopShown)
            {
                _shopShown = unlocked;
                _shopRoot.gameObject.SetActive(unlocked);
                ApplyGemsChip();
            }
            if (unlocked)
                SetGemsValue(gems);
        }

        // ---------------- T36 rendez-vous (IDailyHud) ----------------

        /// <summary>Distance between two rail buttons, top to bottom (Play, Today, Event; canvas units).</summary>
        public const float RailPitch = 188f;
        private const float AppearSeconds = 0.35f;
        private const float DotBreathPeriod = 2.4f;
        private const float NodPeriod = 5.2f;
        private const float NodSeconds = 0.7f;

        private IDailyHudActions _dailyActions;
        private bool _todayShown;
        private bool _todayDotShown;
        private bool _todayHint;
        private float _appearTime = -1f;
        private float _todayIdle;
        private bool _todayAtRest = true;
        private bool _gemsWanted;
        private bool _gemsChipShown;
        private long _gemsTarget = -1;
        private bool _gemsHold;

        public bool TodayVisible => _todayShown;
        public bool TodayDotVisible => _todayDotShown;
        public bool TodayAppearing => _appearTime >= 0f;
        public bool TodayHintShown => _todayHint && !_eggHint && !_upgradeHint && _todayShown;
        public bool GemsChipVisible => _gemsChipShown;
        public bool GemsCollectPlaying => _gemsCollect != null && _gemsCollect.Playing;
        public Button TodayButton => _todayButton;
        public RectTransform TodayRoot => _todayRoot;

        /// <summary>The right rail (Play, Today, Event): later rail buttons go into its items, stacked by visibility.</summary>
        public RectTransform Rail => _rail;
        public CollectMotes GemsCollect => _gemsCollect;

        /// <summary>The wish card the HUD carries on its own canvas (sorting order 24); the presenter opens and ticks it.</summary>
        public WishCardView WishCard => _wishCard;

        public Vector2 TodayScreenPosition => ScreenCenter(_todayRoot);

        public Vector2 HatchScreenPosition => ScreenCenter(_hatchButton != null ? (RectTransform)_hatchButton.transform : _hatchRoot);

        public void BindDaily(IDailyHudActions actions)
        {
            _dailyActions = actions;
            if (_todayButton == null)
                return;
            _todayButton.onClick.RemoveListener(OnTodayPressed);
            _todayButton.onClick.AddListener(OnTodayPressed);
        }

        /// <summary>The Today button on the right rail and its dot (doc 06 screen 002: dot when a wish or the gift waits).</summary>
        public void SetToday(bool visible, bool dot)
        {
            if (_todayRoot == null)
                return;
            if (visible != _todayShown)
            {
                _todayShown = visible;
                _todayRoot.gameObject.SetActive(visible);
                LayoutRail();
                if (!visible)
                    _appearTime = -1f;
            }
            bool showDot = visible && dot;
            if (showDot != _todayDotShown && _todayDot != null)
            {
                _todayDotShown = showDot;
                _todayDot.gameObject.SetActive(showDot);
                _todayIdle = 0f;
                RestToday();
            }
        }

        /// <summary>A12: the button pops in once (0.35 s, a soft overshoot); with Reduce Motion it only fades in.</summary>
        public void PlayTodayAppear()
        {
            if (_todayRoot == null)
                return;
            _appearTime = 0f;
            ApplyAppear(0f);
        }

        /// <summary>A12: the tutorial paw taps on the Today button until the presenter takes it away.</summary>
        public void SetTodayHint(bool visible)
        {
            if (visible == _todayHint)
                return;
            _todayHint = visible;
            if (!visible && !_eggHint && !_upgradeHint)
                _tapHint?.Set(false, default);
        }

        /// <summary>
        /// The Gems chip from the first Gem (doc 01 A10), shop or not. While Gems motes fly, the counter waits and changes as
        /// the first mote lands (doc 05 sheet 17: at 1 020 ms the motes arrive and the Gems counter pops).
        /// </summary>
        public void SetGems(bool visible, long gems)
        {
            if (!_bound || _gemsChip == null)
                return;
            if (visible != _gemsWanted)
            {
                _gemsWanted = visible;
                ApplyGemsChip();
            }
            if (visible)
                SetGemsValue(gems);
        }

        /// <summary>Doc 05 sheet 17: 3 Gem motes fly from a screen point into the Gems chip (H-VFX-008 tinted Gems), which pops.</summary>
        public void PlayGemsCollect(Vector2 fromScreenPosition)
        {
            if (_gemsCollect == null)
                return;
            _gemsCollect.Play(fromScreenPosition);
            _gemsHold = _gemsCollect.Playing;
        }

        private void ApplyGemsChip()
        {
            bool show = _shopShown || _gemsWanted;
            if (show == _gemsChipShown || _gemsChip == null)
                return;
            _gemsChipShown = show;
            _gemsChip.gameObject.SetActive(show);
        }

        private void SetGemsValue(long gems)
        {
            _gemsTarget = gems;
            if (_gemsHold || gems == _gemsShown || _gems == null)
                return;
            _gemsShown = gems;
            _gems.Set(BigValue.FromDouble(gems));
        }

        /// <summary>Stacks the visible rail buttons from the top (no layout group on the HUD). Only on a visibility change.</summary>
        private void LayoutRail()
        {
            int row = 0;
            for (int i = 0; i < _railItems.Length; i++)
            {
                var item = _railItems[i];
                if (item == null || !item.gameObject.activeSelf)
                    continue;
                item.anchoredPosition = new Vector2(0f, -RailPitch * (row + 0.5f));
                row++;
            }
        }

        /// <summary>
        /// The daily invitation stays alive without ever flashing: while the dot is on, the dot breathes slowly and the
        /// page nods once every few seconds. Nothing moves with Reduce Motion or without the dot.
        /// </summary>
        private void TickToday(float dt)
        {
            if (_appearTime >= 0f && _todayShown)
            {
                _appearTime += dt;
                float k = Mathf.Clamp01(_appearTime / AppearSeconds);
                ApplyAppear(k);
                if (k >= 1f)
                    _appearTime = -1f;
            }
            if (!_todayShown || !_todayDotShown || ReduceMotion || _todayDot == null)
            {
                RestToday();
                return;
            }
            _todayAtRest = false;
            _todayIdle += dt;
            float breath = 0.5f - 0.5f * Mathf.Cos(_todayIdle * (2f * Mathf.PI / DotBreathPeriod));
            float dot = 1f + 0.14f * breath;
            _todayDot.localScale = new Vector3(dot, dot, 1f);
            if (_todayIcon == null)
                return;
            // A small happy nod of the page: tilt, settle back, rest.
            float nod = Mathf.Repeat(_todayIdle, NodPeriod) - (NodPeriod - NodSeconds);
            float angle = 0f;
            if (nod > 0f)
            {
                float t = nod / NodSeconds;
                angle = 7f * Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
            }
            _todayIcon.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void RestToday()
        {
            if (_todayAtRest)
                return;
            _todayAtRest = true;
            if (_todayDot != null)
                _todayDot.localScale = Vector3.one;
            if (_todayIcon != null)
                _todayIcon.localRotation = Quaternion.identity;
        }

        private void ApplyAppear(float k)
        {
            if (_todayGroup != null)
                _todayGroup.alpha = ReduceMotion ? k : Mathf.Clamp01(k * 3f);
            float s = ReduceMotion ? 1f : (k >= 1f ? 1f : EaseOutBack(k));
            _todayRoot.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>0 to 1 with a soft overshoot (about 1.1 at its peak).</summary>
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private void TickTodayHint()
        {
            if (_tapHint == null || !_todayHint || _eggHint || _upgradeHint || !_todayShown)
                return;
            _tapHint.Set(true, TodayScreenPosition);
        }

        private void TickGemsCollect(float dt)
        {
            if (_gemsCollect == null)
                return;
            _gemsCollect.Tick(dt);
            if (_gemsHold && (!_gemsCollect.Playing || _gemsCollect.Landed > 0))
            {
                _gemsHold = false;
                if (_gemsTarget >= 0)
                    SetGemsValue(_gemsTarget);
            }
        }

        private static Vector2 ScreenCenter(RectTransform rect)
        {
            if (rect == null)
                return default;
            // Overlay canvas: no camera.
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        private void OnTodayPressed() => _dailyActions?.OpenToday();

        /// <summary>Prefab builder hook for the T36 parts (Editor UiPipeline).</summary>
        public void SetDailyReferences(RectTransform rail, RectTransform[] railItems, Button todayButton, RectTransform todayRoot,
            CanvasGroup todayGroup, RectTransform todayIcon, RectTransform todayDot, CollectMotes gemsCollect, WishCardView wishCard)
        {
            _rail = rail;
            _railItems = railItems;
            _todayButton = todayButton;
            _todayRoot = todayRoot;
            _todayGroup = todayGroup;
            _todayIcon = todayIcon;
            _todayDot = todayDot;
            _gemsCollect = gemsCollect;
            _wishCard = wishCard;
        }

        // ---------------- T37 activities (IPlayHud) ----------------

        private const float PlayingFadeSeconds = 0.15f;
        private const float HopPeriod = 5.2f;
        private const float HopSeconds = 0.45f;

        private IPlayHudActions _playActions;
        private readonly UiNumberText _playCountText = new UiNumberText();
        private bool _playShown;
        private bool _playDotShown;
        private bool _playHint;
        private int _ticketsShown = -1;
        private float _playAppearTime = -1f;
        private float _playIdle;
        private bool _playAtRest = true;
        private Vector2 _playIconHome;
        private bool _playing;
        private float _playingAlpha = 1f;
        private Canvas _rootCanvas;

        public bool PlayVisible => _playShown;
        public bool PlayDotVisible => _playDotShown;
        public bool PlayAppearing => _playAppearTime >= 0f;
        public bool PlayHintShown => _playHint && !_eggHint && !_upgradeHint && !(_todayHint && _todayShown) && _playShown;
        public string PlayCountText => _playCount != null ? _playCount.text : string.Empty;
        public Button PlayButton => _playButton;
        public RectTransform PlayRoot => _playRoot;
        public RectTransform PlayBadge => _playBadge;

        /// <summary>A game plays: the HUD is faded out and takes no touch (<see cref="SetPlaying"/>).</summary>
        public bool Playing => _playing;

        /// <summary>True between SetPlaying(true) and SetPlaying(false): the refuge HUD faded out and not raycast.</summary>
        public bool PlayingMode => _playing;

        /// <summary>The HUD's opacity as a game starts or ends (0 while a game plays, 1 otherwise).</summary>
        public float PlayingAlpha => _playingAlpha;

        public CanvasGroup RootGroup => _rootGroup;

        public Vector2 PlayScreenPosition => ScreenCenter(_playRoot);

        public void BindPlay(IPlayHudActions actions)
        {
            _playActions = actions;
            if (_playButton == null)
                return;
            _playButton.onClick.RemoveListener(OnPlayPressed);
            _playButton.onClick.AddListener(OnPlayPressed);
        }

        /// <summary>
        /// The Play button on the rail, above Today (doc 01 A15: refuge level 3 and 5 animals), the tickets owned in its round
        /// badge, its dot (a free game, a new activity, a challenge to try). Touches nothing when nothing changed.
        /// </summary>
        public void SetPlay(bool visible, int tickets, bool dot)
        {
            if (_playRoot == null)
                return;
            if (visible != _playShown)
            {
                _playShown = visible;
                _playRoot.gameObject.SetActive(visible);
                LayoutRail();
                if (!visible)
                    _playAppearTime = -1f;
            }
            if (tickets != _ticketsShown && _playCount != null)
            {
                _ticketsShown = tickets;
                _playCountText.SetCount(_playCount, Mathf.Max(0, tickets));
            }
            bool showDot = visible && dot;
            if (showDot != _playDotShown && _playDot != null)
            {
                _playDotShown = showDot;
                _playDot.gameObject.SetActive(showDot);
                _playIdle = 0f;
                RestPlay();
            }
        }

        /// <summary>A15: the button pops in once (0.35 s, a soft overshoot); with Reduce Motion it only fades in.</summary>
        public void PlayPlayAppear()
        {
            if (_playRoot == null)
                return;
            _playAppearTime = 0f;
            ApplyPlayAppear(0f);
        }

        /// <summary>A15: the tutorial paw taps on Play until the presenter takes it away (Today's paw goes first).</summary>
        public void SetPlayHint(bool visible)
        {
            if (visible == _playHint)
                return;
            _playHint = visible;
            if (!visible && !_eggHint && !_upgradeHint && !_todayHint)
                _tapHint?.Set(false, default);
        }

        /// <summary>
        /// A game plays: the whole refuge HUD (chips, egg button, rails, toasts, hints, the wish card) fades out in 150 ms
        /// (at once with Reduce Motion) and catches no touch; its canvas turns off once faded. The game's end brings it back
        /// the same way. No allocation.
        /// </summary>
        public void SetPlaying(bool playing)
        {
            if (playing == _playing)
                return;
            _playing = playing;
            if (_rootGroup == null)
                _rootGroup = GetComponent<CanvasGroup>();
            if (_rootCanvas == null)
                _rootCanvas = GetComponent<Canvas>();
            if (_rootGroup != null)
            {
                _rootGroup.blocksRaycasts = !playing;
                _rootGroup.interactable = !playing;
            }
            if (!playing && _rootCanvas != null && !_rootCanvas.enabled)
                _rootCanvas.enabled = true;
            if (ReduceMotion || !_bound)
                ApplyPlaying(playing ? 0f : 1f);
        }

        private void TickPlaying(float dt)
        {
            float target = _playing ? 0f : 1f;
            if (_playingAlpha == target)
                return;
            ApplyPlaying(ReduceMotion ? target : Mathf.MoveTowards(_playingAlpha, target, dt / PlayingFadeSeconds));
        }

        private void ApplyPlaying(float alpha)
        {
            _playingAlpha = alpha;
            if (_rootGroup != null)
                _rootGroup.alpha = alpha;
            // Faded out: nothing left to draw.
            if (_rootCanvas != null && _playing && alpha <= 0f && _rootCanvas.enabled)
                _rootCanvas.enabled = false;
        }

        /// <summary>
        /// The invitation stays alive without flashing: while the dot is on, the dot breathes slowly and the play icon hops
        /// once every few seconds. Nothing moves with Reduce Motion or without the dot.
        /// </summary>
        private void TickPlay(float dt)
        {
            if (_playRoot == null)
                return;
            if (_playAppearTime >= 0f && _playShown)
            {
                _playAppearTime += dt;
                float k = Mathf.Clamp01(_playAppearTime / AppearSeconds);
                ApplyPlayAppear(k);
                if (k >= 1f)
                    _playAppearTime = -1f;
            }
            if (!_playShown || !_playDotShown || ReduceMotion || _playDot == null)
            {
                RestPlay();
                return;
            }
            _playAtRest = false;
            _playIdle += dt;
            float breath = 0.5f - 0.5f * Mathf.Cos(_playIdle * (2f * Mathf.PI / DotBreathPeriod));
            float scale = 1f + 0.14f * breath;
            _playDot.localScale = new Vector3(scale, scale, 1f);
            if (_playIcon == null)
                return;
            float hop = Mathf.Repeat(_playIdle, HopPeriod) - (HopPeriod - HopSeconds);
            float lift = hop > 0f ? 12f * Mathf.Sin(hop / HopSeconds * Mathf.PI) : 0f;
            _playIcon.anchoredPosition = _playIconHome + new Vector2(0f, lift);
        }

        private void RestPlay()
        {
            if (_playAtRest)
                return;
            _playAtRest = true;
            if (_playDot != null)
                _playDot.localScale = Vector3.one;
            if (_playIcon != null)
                _playIcon.anchoredPosition = _playIconHome;
        }

        private void ApplyPlayAppear(float k)
        {
            if (_playGroup != null)
                _playGroup.alpha = ReduceMotion ? k : Mathf.Clamp01(k * 3f);
            float s = ReduceMotion ? 1f : (k >= 1f ? 1f : EaseOutBack(k));
            _playRoot.localScale = new Vector3(s, s, 1f);
        }

        private void TickPlayHint()
        {
            if (_tapHint == null || !_playHint || _eggHint || _upgradeHint || (_todayHint && _todayShown) || !_playShown)
                return;
            _tapHint.Set(true, PlayScreenPosition);
        }

        private void OnPlayPressed() => _playActions?.OpenActivities();

        /// <summary>Prefab builder hook for the T37 parts (Editor UiPipeline).</summary>
        public void SetPlayReferences(Button playButton, RectTransform playRoot, CanvasGroup playGroup, RectTransform playIcon, RectTransform playBadge,
            TMP_Text playCount, RectTransform playDot, CanvasGroup rootGroup)
        {
            _playButton = playButton;
            _playRoot = playRoot;
            _playGroup = playGroup;
            _playIcon = playIcon;
            _playBadge = playBadge;
            _playCount = playCount;
            _playDot = playDot;
            _rootGroup = rootGroup;
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
            TickTodayHint();
            TickPlayHint();
            TickPlay(unscaledDeltaTime);
            TickPlaying(unscaledDeltaTime);
            TickLevelUp(unscaledDeltaTime);
            TickToday(unscaledDeltaTime);
            if (_eggChoice != null)
                _eggChoice.Tick(unscaledDeltaTime);
            if (_tapHint != null)
                _tapHint.Tick(unscaledDeltaTime);
            if (_collect != null)
                _collect.Tick(unscaledDeltaTime);
            TickGemsCollect(unscaledDeltaTime);
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
            !_playing && (RectTransformUtility.RectangleContainsScreenPoint(_hatchRoot, screenPosition, null)
            || RectTransformUtility.RectangleContainsScreenPoint(_lightChip, screenPosition, null)
            || (_codexRoot != null && RectTransformUtility.RectangleContainsScreenPoint(_codexRoot, screenPosition, null))
            || (_infoRoot != null && RectTransformUtility.RectangleContainsScreenPoint(_infoRoot, screenPosition, null))
            || (_upgradeShown && RectTransformUtility.RectangleContainsScreenPoint(_upgradeRoot, screenPosition, null))
            || (_levelRoot != null && RectTransformUtility.RectangleContainsScreenPoint(_levelRoot, screenPosition, null))
            || (_eggChoice != null && _eggChoice.Contains(screenPosition))
            || (_shopShown && RectTransformUtility.RectangleContainsScreenPoint(_shopRoot, screenPosition, null))
            || (_gemsChipShown && RectTransformUtility.RectangleContainsScreenPoint(_gemsChip, screenPosition, null))
            || (_todayShown && RectTransformUtility.RectangleContainsScreenPoint(_todayRoot, screenPosition, null))
            || (_playShown && RectTransformUtility.RectangleContainsScreenPoint(_playRoot, screenPosition, null))
            // The wish card's touch catcher covers the screen: a touch beside it only closes it (doc 01 A08).
            || (_wishCard != null && _wishCard.TakesTouch));

        // ---------------- T35B egg choice (IEggChoiceHud) ----------------

        private string _priceText;

        public EggChoiceBar EggChoice => _eggChoice;

        public void BindEggs(IEggChoiceActions actions)
        {
            if (_eggChoice != null)
                _eggChoice.Bind(actions);
        }

        public void SetEggSlots(EggSlot[] slots, int count, int selected) => _eggChoice?.SetSlots(slots, count, selected);

        public void ShowEggName(string name)
        {
            // The name and the mastery toast share the space above the button; the toast wins.
            if (_eggChoice != null && !MasteryVisible)
                _eggChoice.ShowName(name);
        }

        public void SetHatchQuantity(int quantity, bool visible) => _eggChoice?.SetQuantity(quantity, visible);

        public void SetFocusTargets(PortraitSlot[] portraits, int count) => _eggChoice?.SetTargets(portraits, count);

        /// <summary>"Free" for a Discovery egg: the word replaces the Light icon and price; null brings the price back.</summary>
        public void SetPriceText(string text)
        {
            if (text == _priceText || _priceWord == null)
                return;
            _priceText = text;
            bool word = text != null;
            _priceWord.gameObject.SetActive(word);
            if (word)
                _priceWord.text = text;
            if (_costIcon != null)
                _costIcon.enabled = !word;
            _costLabel.enabled = !word;
        }

        public string PriceTextShown => _priceText;

        /// <summary>Prefab builder hook (Editor EggUiPipeline).</summary>
        public void SetEggChoiceReferences(EggChoiceBar bar, Image costIcon, TMP_Text priceWord)
        {
            _eggChoice = bar;
            _costIcon = costIcon;
            _priceWord = priceWord;
        }

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

        // ---------------- T44A albums (IAlbumHud) ----------------

        [Header("T44A albums: the round Albums button above the shop")]
        [SerializeField] private Button _albumsButton;
        [SerializeField] private RectTransform _albumsRoot;
        [SerializeField] private GameObject _albumsDot;

        private IAlbumHudActions _albumActions;

        public bool AlbumsVisible => _albumsRoot != null && _albumsRoot.gameObject.activeSelf;
        public bool AlbumsDotVisible => _albumsDot != null && _albumsDot.activeSelf;
        public Button AlbumsButton => _albumsButton;
        public RectTransform AlbumsRoot => _albumsRoot;

        /// <summary>Where gained stickers fly while the albums screen is closed.</summary>
        public Vector2 AlbumsScreenPosition => ScreenCenter(_albumsRoot);

        public void BindAlbums(IAlbumHudActions actions)
        {
            _albumActions = actions;
            if (_albumsButton == null)
                return;
            _albumsButton.onClick.RemoveListener(OnAlbumsPressed);
            _albumsButton.onClick.AddListener(OnAlbumsPressed);
        }

        /// <summary>Shown once the albums open (refuge level 12 and day 5); the dot while a sticker waits to be seen.</summary>
        public void SetAlbums(bool visible, bool dot)
        {
            if (_albumsRoot == null)
                return;
            if (_albumsRoot.gameObject.activeSelf != visible)
                _albumsRoot.gameObject.SetActive(visible);
            bool showDot = visible && dot;
            if (_albumsDot != null && _albumsDot.activeSelf != showDot)
                _albumsDot.SetActive(showDot);
        }

        private void OnAlbumsPressed() => _albumActions?.OpenAlbums();

        /// <summary>Prefab builder hook for the Albums button (Editor UiPipeline).</summary>
        public void SetAlbumsReferences(Button button, RectTransform root, GameObject dot)
        {
            _albumsButton = button;
            _albumsRoot = root;
            _albumsDot = dot;
        }

        // ---------------- T44B profile (IProfileHud) ----------------

        [Header("T44B profile: the round Succès button above the Albums button")]
        [SerializeField] private Button _profileButton;
        [SerializeField] private RectTransform _profileRoot;
        [SerializeField] private GameObject _profileDot;

        private IProfileHudActions _profileActions;

        public bool ProfileVisible => _profileRoot != null && _profileRoot.gameObject.activeSelf;
        public bool ProfileDotVisible => _profileDot != null && _profileDot.activeSelf;
        public Button ProfileButton => _profileButton;
        public RectTransform ProfileRoot => _profileRoot;

        public void BindProfile(IProfileHudActions actions)
        {
            _profileActions = actions;
            if (_profileButton == null)
                return;
            _profileButton.onClick.RemoveListener(OnProfilePressed);
            _profileButton.onClick.AddListener(OnProfilePressed);
        }

        /// <summary>The dot while an achievement tier waits to be claimed.</summary>
        public void SetProfile(bool visible, bool dot)
        {
            if (_profileRoot == null)
                return;
            if (_profileRoot.gameObject.activeSelf != visible)
                _profileRoot.gameObject.SetActive(visible);
            bool showDot = visible && dot;
            if (_profileDot != null && _profileDot.activeSelf != showDot)
                _profileDot.SetActive(showDot);
        }

        private void OnProfilePressed() => _profileActions?.OpenProfile();

        /// <summary>Prefab builder hook for the Succès button (Editor UiPipeline).</summary>
        public void SetProfileReferences(Button button, RectTransform root, GameObject dot)
        {
            _profileButton = button;
            _profileRoot = root;
            _profileDot = dot;
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
            if (_todayButton != null)
                _todayButton.onClick.RemoveListener(OnTodayPressed);
            if (_playButton != null)
                _playButton.onClick.RemoveListener(OnPlayPressed);
        }
    }
}
