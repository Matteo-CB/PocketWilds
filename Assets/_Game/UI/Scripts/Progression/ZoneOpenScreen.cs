using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_ZoneOpen (doc 02 Améliorations, section 11.3, I-SCR-040): "Open the Orchard", a capture of the zone (the
    /// sign's icon until one exists), what it brings in icons, the price and Open; when a condition is missing, the
    /// condition and its bar instead of an active button. Pooled modal above the HUD; closed, it costs nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ZoneOpenScreen : MonoBehaviour, IZoneOpenView, IMotionSettings
    {
        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _card;
        [SerializeField] private Button _scrim;
        [SerializeField] private Button _close;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private Image _capture;
        [SerializeField] private GameObject _captureStandIn;
        [SerializeField] private TMP_Text _gainProduction;
        [SerializeField] private TMP_Text _gainAnimals;
        [SerializeField] private GameObject _gainDecorRow;
        [SerializeField] private TMP_Text _gainDecor;
        [SerializeField] private RectTransform _priceIcon;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private GameObject _conditionRow;
        [SerializeField] private TMP_Text _condition;
        [SerializeField] private RectTransform _conditionBar;
        [SerializeField] private RectTransform _conditionFill;
        [SerializeField] private Button _open;
        [SerializeField] private Image _openBack;
        [SerializeField] private TMP_Text _openLabel;

        private readonly WindowMotion _motion = new WindowMotion();
        private IZoneOpenActions _actions;
        private BigValueText _priceText;
        private bool _initialized;
        private bool _canOpen;
        private bool _openRequested;

        public bool IsOpen => _motion.IsOpen && !_motion.Closing;
        public bool ReduceMotion { get => _motion.ReduceMotion; set => _motion.ReduceMotion = value; }
        public string TitleText => _title != null ? _title.text : string.Empty;
        public bool CanOpen => _canOpen;
        public Button OpenButton => _open;
        public bool ConditionVisible => _conditionRow != null && _conditionRow.activeSelf;

        public void Open(in ZoneOpenData data, ILocalization text, IZoneOpenActions actions)
        {
            Initialize();
            _actions = actions;
            _openRequested = false;
            _openLabel.text = text.Get(ProgressionText.ZoneOpenButton);
            Refresh(data);
            _motion.Show();
        }

        public void Refresh(in ZoneOpenData data)
        {
            _title.text = data.Title;
            bool capture = data.Capture != null;
            _capture.gameObject.SetActive(capture);
            _captureStandIn.SetActive(!capture);
            if (capture)
                _capture.sprite = data.Capture;
            _gainProduction.text = data.GainProduction;
            _gainAnimals.text = data.GainAnimals;
            bool decor = !string.IsNullOrEmpty(data.GainDecor);
            _gainDecorRow.SetActive(decor);
            if (decor)
                _gainDecor.text = data.GainDecor;
            if (_priceText.Set(data.Price))
                UiRow.Center(_priceIcon, _price, 12f);
            _canOpen = data.CanOpen;
            _price.color = data.CanOpen ? _tokens.textPrimary : _tokens.textSecondary;
            bool condition = !string.IsNullOrEmpty(data.Condition);
            _conditionRow.SetActive(condition);
            if (condition)
            {
                _condition.text = data.Condition;
                float width = _conditionBar.rect.width;
                float height = _conditionBar.rect.height;
                _conditionFill.gameObject.SetActive(data.ConditionFraction > 0.001f);
                _conditionFill.sizeDelta = new Vector2(Mathf.Max(height, width * Mathf.Clamp01(data.ConditionFraction)), _conditionFill.sizeDelta.y);
            }
            _openBack.color = data.CanOpen ? _tokens.accentPrimary : _tokens.accentIdle;
            _openLabel.color = data.CanOpen ? _tokens.textPrimary : _tokens.textSecondary;
        }

        public void Close() => _motion.Hide();

        public void Prewarm(ILocalization text)
        {
            if (_motion.IsOpen)
                return;
            Initialize();
            var data = new ZoneOpenData
            {
                Title = text.Get(ProgressionText.ZoneOpenTitle),
                GainProduction = text.Get(ProgressionText.ZoneGainProduction),
                GainAnimals = text.Get(ProgressionText.ZoneGainAnimals),
                GainDecor = text.Get(ProgressionText.ZoneGainDecor),
                Condition = text.Get(ProgressionText.ZoneNeedLight),
                ConditionFraction = 0.5f,
            };
            _openLabel.text = text.Get(ProgressionText.ZoneOpenButton);
            Refresh(data);
            _motion.Show();
            Canvas.ForceUpdateCanvases();
            _motion.HideNow();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_motion.IsOpen)
                return;
            if (_motion.BackPressed)
                _motion.Hide();
            if (_motion.Tick(unscaledDeltaTime) && !_openRequested)
                _actions?.ZoneWindowClosed();
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _motion.Bind(_canvas, _content, _group, _card);
            _priceText = new BigValueText(_price);
            _scrim.onClick.AddListener(OnClose);
            _close.onClick.AddListener(OnClose);
            _open.onClick.AddListener(OnOpen);
        }

        private void OnClose() => _motion.Hide();

        private void OnOpen()
        {
            if (_motion.Closing)
                return;
            // The presenter decides: it opens the zone (and closes the window) or answers "not yet".
            _openRequested = true;
            _actions?.OpenZone();
            _openRequested = _motion.Closing;
        }

        private void OnDestroy()
        {
            if (!_initialized)
                return;
            _scrim.onClick.RemoveListener(OnClose);
            _close.onClick.RemoveListener(OnClose);
            _open.onClick.RemoveListener(OnOpen);
        }

        /// <summary>Prefab builder hook (Editor ProgressionUiPipeline).</summary>
        public void SetReferences(UiColorTokens tokens, Canvas canvas, GameObject content, CanvasGroup group, RectTransform card, Button scrim,
            Button close, TMP_Text title, Image capture, GameObject captureStandIn, TMP_Text gainProduction, TMP_Text gainAnimals,
            GameObject gainDecorRow, TMP_Text gainDecor, RectTransform priceIcon, TMP_Text price, GameObject conditionRow, TMP_Text condition, RectTransform conditionBar,
            RectTransform conditionFill, Button open, Image openBack, TMP_Text openLabel)
        {
            _tokens = tokens;
            _canvas = canvas;
            _content = content;
            _group = group;
            _card = card;
            _scrim = scrim;
            _close = close;
            _title = title;
            _capture = capture;
            _captureStandIn = captureStandIn;
            _gainProduction = gainProduction;
            _gainAnimals = gainAnimals;
            _gainDecorRow = gainDecorRow;
            _gainDecor = gainDecor;
            _priceIcon = priceIcon;
            _price = price;
            _conditionRow = conditionRow;
            _condition = condition;
            _conditionBar = conditionBar;
            _conditionFill = conditionFill;
            _open = open;
            _openBack = openBack;
            _openLabel = openLabel;
        }
    }
}
