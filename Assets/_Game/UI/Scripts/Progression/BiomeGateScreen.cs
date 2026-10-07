using System;
using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_BiomeGate (doc 02 Améliorations, section 11.5, I-SCR-042): "Open the Forest", its conditions with ticks
    /// (refuge level, species, mastery, day or play time in plain words, never a countdown), six round silhouettes of the
    /// biome's species, the price and Open. A biome without content in this version reads "Coming soon", without price
    /// or button. Pooled modal above the HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BiomeGateScreen : MonoBehaviour, IBiomeGateView, IMotionSettings
    {
        public const float ConditionsTop = 396f;
        public const float ConditionPitch = 84f;
        private const float SoonFooter = 200f;
        private const float BuyFooter = 430f;

        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _card;
        [SerializeField] private Button _scrim;
        [SerializeField] private Button _close;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private GameObject[] _rows = Array.Empty<GameObject>();
        [SerializeField] private Image[] _checks = Array.Empty<Image>();
        [SerializeField] private Image[] _dots = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] _texts = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text _soon;
        [SerializeField] private GameObject _buyRow;
        [SerializeField] private RectTransform _priceIcon;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private Button _open;
        [SerializeField] private Image _openBack;
        [SerializeField] private TMP_Text _openLabel;

        private readonly WindowMotion _motion = new WindowMotion();
        private IBiomeGateActions _actions;
        private BigValueText _priceText;
        private bool _initialized;
        private bool _openRequested;

        public bool IsOpen => _motion.IsOpen && !_motion.Closing;
        public bool ReduceMotion { get => _motion.ReduceMotion; set => _motion.ReduceMotion = value; }
        public string TitleText => _title != null ? _title.text : string.Empty;
        public bool SoonShown => _soon != null && _soon.gameObject.activeSelf;
        public int ConditionsShown { get; private set; }
        public bool ConditionMet(int index) => _checks[index].gameObject.activeSelf;

        public void Open(in BiomeGateData data, ILocalization text, IBiomeGateActions actions)
        {
            Initialize();
            _actions = actions;
            _openRequested = false;
            Bind(data, text);
            _motion.Show();
        }

        private void Bind(in BiomeGateData data, ILocalization text)
        {
            _title.text = data.Title;
            ConditionsShown = Mathf.Min(data.ConditionCount, _rows.Length);
            for (int i = 0; i < _rows.Length; i++)
            {
                bool shown = i < ConditionsShown;
                _rows[i].SetActive(shown);
                if (!shown)
                    continue;
                var line = data.Conditions[i];
                _checks[i].gameObject.SetActive(line.Met);
                _dots[i].gameObject.SetActive(!line.Met);
                _texts[i].text = line.Text;
                _texts[i].color = line.Met ? _tokens.textSecondary : _tokens.textPrimary;
            }
            _card.sizeDelta = new Vector2(_card.sizeDelta.x, ConditionsTop + ConditionsShown * ConditionPitch + (data.Soon ? SoonFooter : BuyFooter));
            _soon.gameObject.SetActive(data.Soon);
            _soon.text = text.Get(ProgressionText.BiomeGateSoon);
            _buyRow.SetActive(!data.Soon);
            if (!data.Soon)
            {
                if (_priceText.Set(data.Price))
                    UiRow.Center(_priceIcon, _price, 12f);
                _price.color = data.CanOpen ? _tokens.textPrimary : _tokens.textSecondary;
                _openBack.color = data.CanOpen ? _tokens.accentPrimary : _tokens.accentIdle;
                _openLabel.text = text.Get(ProgressionText.ZoneOpenButton);
                _openLabel.color = data.CanOpen ? _tokens.textPrimary : _tokens.textSecondary;
            }
        }

        public void Close() => _motion.Hide();

        public void Prewarm(ILocalization text)
        {
            if (_motion.IsOpen)
                return;
            Initialize();
            var data = new BiomeGateData
            {
                Title = text.Get(ProgressionText.BiomeGateTitle),
                ConditionCount = 4,
                Conditions = new GateConditionLine[BiomeGateData.MaxConditions],
            };
            for (int i = 0; i < 4; i++)
                data.Conditions[i] = new GateConditionLine { Text = text.Get(ProgressionText.BiomeGateLevel), Met = i % 2 == 0 };
            Bind(data, text);
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
                _actions?.BiomeGateClosed();
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
            _openRequested = true;
            _actions?.OpenBiome();
            _openRequested = !_motion.IsOpen || _motion.Closing;
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
            Button close, TMP_Text title, GameObject[] rows, Image[] checks, Image[] dots, TMP_Text[] texts, TMP_Text soon, GameObject buyRow,
            RectTransform priceIcon, TMP_Text price, Button open, Image openBack, TMP_Text openLabel)
        {
            _priceIcon = priceIcon;
            _tokens = tokens;
            _canvas = canvas;
            _content = content;
            _group = group;
            _card = card;
            _scrim = scrim;
            _close = close;
            _title = title;
            _rows = rows;
            _checks = checks;
            _dots = dots;
            _texts = texts;
            _soon = soon;
            _buyRow = buyRow;
            _price = price;
            _open = open;
            _openBack = openBack;
            _openLabel = openLabel;
        }
    }
}
