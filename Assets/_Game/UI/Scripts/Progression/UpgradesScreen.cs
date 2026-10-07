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
    /// PF_UI_Upgrades (doc 02 Améliorations, section 11.2, I-SCR-039): a bottom panel over two thirds of the screen,
    /// the refuge stays visible above it. Header with the whole production and the ×1 / ×10 / Max choice, three
    /// reused cards, then the biome's mastery and the next zone with a See button. Own canvas, separate from the HUD;
    /// the cards are rebound only when the presenter says the model changed (never per frame). Touching the refuge
    /// above the panel closes it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UpgradesScreen : MonoBehaviour, IUpgradesView, IMotionSettings
    {
        private const float RateCountSeconds = 0.4f;

        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _safeArea;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Button _outside;
        [SerializeField] private Button _close;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _rate;
        [SerializeField] private Button[] _qty = Array.Empty<Button>();
        [SerializeField] private Image[] _qtyBack = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] _qtyLabel = Array.Empty<TMP_Text>();
        [SerializeField] private UpgradeCardView[] _cards = Array.Empty<UpgradeCardView>();
        [SerializeField] private TMP_Text _masteryTitle;
        [SerializeField] private TMP_Text _masteryLevel;
        [SerializeField] private RectTransform _masteryBar;
        [SerializeField] private RectTransform _masteryFill;
        [SerializeField] private TMP_Text _masteryNext;
        [SerializeField] private GameObject _zoneBlock;
        [SerializeField] private TMP_Text _zoneLabel;
        [SerializeField] private TMP_Text _zoneTitle;
        [SerializeField] private GameObject _zonePriceRow;
        [SerializeField] private TMP_Text _zonePrice;
        [SerializeField] private TMP_Text _zoneCondition;
        [SerializeField] private Button _see;
        [SerializeField] private TMP_Text _seeLabel;

        private readonly WindowMotion _motion = new WindowMotion(0.22f, true);
        private readonly SafeAreaFitter _fitter = new SafeAreaFitter();
        private IUpgradesModel _model;
        private ILocalization _text;
        private IUpgradesActions _actions;
        private BigValueText _rateText;
        private BigValueText _zonePriceText;
        private bool _initialized;
        private BigValue _rateFrom;
        private BigValue _rateTo;
        private float _rateTime = -1f;
        private bool _reduceMotion;

        public bool IsOpen => _motion.IsOpen && !_motion.Closing;
        public int CardCount => _cards.Length;
        public UpgradeCardView GetCard(int index) => _cards[index];
        public Button SeeButton => _see;
        public Button CloseButton => _close;
        public Button QtyButton(int index) => _qty[index];
        public string RateText => _rate != null ? _rate.text : string.Empty;
        public bool ZoneBlockVisible => _zoneBlock != null && _zoneBlock.activeSelf;

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

        public void Open(IUpgradesModel model, ILocalization text, IUpgradesActions actions)
        {
            Bind(model, text, actions);
            _motion.Show();
        }

        public void Prewarm(IUpgradesModel model, ILocalization text, IUpgradesActions actions)
        {
            if (_motion.IsOpen)
                return;
            Bind(model, text, actions);
            _motion.Show();
            Canvas.ForceUpdateCanvases();
            _motion.HideNow();
        }

        private void Bind(IUpgradesModel model, ILocalization text, IUpgradesActions actions)
        {
            _model = model;
            _text = text;
            _actions = actions;
            Initialize();
            _fitter.Fit(_safeArea);
            _title.text = text.Get(ProgressionText.UpgradesTitle);
            _qtyLabel[0].text = text.Get(ProgressionText.QtyOne);
            _qtyLabel[1].text = text.Get(ProgressionText.QtyTen);
            _qtyLabel[2].text = text.Get(ProgressionText.QtyMax);
            _seeLabel.text = text.Get(ProgressionText.UpgradesSee);
            _zoneLabel.text = text.Get(ProgressionText.UpgradesNextZone);
            foreach (var card in _cards)
                card.SetTexts(text);
            _rateTime = -1f;
            _rateTo = model.Rate;
            _rateText.Set(_rateTo);
            Refresh();
        }

        public void Refresh()
        {
            if (_model == null)
                return;
            for (int i = 0; i < _qtyBack.Length; i++)
            {
                bool selected = (int)_model.Qty == i;
                _qtyBack[i].color = selected ? _tokens.accentPrimary : _tokens.panelSecondary;
                _qtyLabel[i].color = selected ? _tokens.textPrimary : _tokens.textSecondary;
            }
            int count = Mathf.Min(_model.CardCount, _cards.Length);
            for (int i = 0; i < count; i++)
                _cards[i].Bind(_model.GetCard(i), _text);

            // The production counts up to its new value (section 11.6).
            var rate = _model.Rate;
            if (rate != _rateTo)
            {
                _rateFrom = _rateTime >= 0f ? CurrentRate() : _rateTo;
                _rateTo = rate;
                _rateTime = _reduceMotion || !_motion.IsOpen ? -1f : 0f;
                if (_rateTime < 0f)
                    _rateText.Set(rate);
            }

            var footer = _model.Footer;
            _masteryTitle.text = footer.MasteryTitle;
            _masteryLevel.text = footer.MasteryLevel.ToString();
            _masteryNext.text = footer.MasteryNext;
            float width = _masteryBar.rect.width;
            float height = _masteryBar.rect.height;
            _masteryFill.gameObject.SetActive(footer.MasteryFraction > 0.001f);
            _masteryFill.sizeDelta = new Vector2(Mathf.Max(height, width * Mathf.Clamp01(footer.MasteryFraction)), _masteryFill.sizeDelta.y);

            _zoneBlock.SetActive(footer.HasZone);
            if (footer.HasZone)
            {
                _zoneTitle.text = footer.ZoneTitle;
                bool condition = !string.IsNullOrEmpty(footer.ZoneCondition);
                _zoneCondition.gameObject.SetActive(condition);
                _zonePriceRow.SetActive(!condition);
                if (condition)
                {
                    _zoneCondition.text = footer.ZoneCondition;
                }
                else
                {
                    _zonePriceText.Set(footer.ZonePrice);
                    _zonePrice.color = footer.ZoneAffordable ? _tokens.textPrimary : _tokens.textSecondary;
                }
            }
        }

        public void PlayPurchase(int card, bool milestone)
        {
            if (card >= 0 && card < _cards.Length)
                _cards[card].PlayPurchase(milestone);
        }

        public void PlayNotEnough(int card)
        {
            if (card >= 0 && card < _cards.Length)
                _cards[card].PlayNotEnough();
        }

        public void Close() => _motion.Hide();

        public void Tick(float unscaledDeltaTime)
        {
            if (!_motion.IsOpen)
                return;
            if (_motion.BackPressed)
                _actions?.CloseUpgrades();
            _fitter.Fit(_safeArea);
            _motion.Tick(unscaledDeltaTime);
            for (int i = 0; i < _cards.Length; i++)
                _cards[i].Tick(unscaledDeltaTime);
            if (_rateTime >= 0f)
            {
                _rateTime += unscaledDeltaTime;
                if (_rateTime >= RateCountSeconds)
                {
                    _rateTime = -1f;
                    _rateText.Set(_rateTo);
                }
                else
                {
                    _rateText.Set(CurrentRate());
                }
            }
        }

        private BigValue CurrentRate()
        {
            float t = Mathf.Clamp01(_rateTime / RateCountSeconds);
            float eased = 1f - (1f - t) * (1f - t);
            return BigValue.Add(_rateFrom, BigValue.Multiply(BigValue.Subtract(_rateTo, _rateFrom), eased));
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _motion.Bind(_canvas, _content, _group, _panel);
            _motion.ReduceMotion = _reduceMotion;
            _rateText = new BigValueText(_rate, null, "/s");
            _zonePriceText = new BigValueText(_zonePrice);
            _close.onClick.AddListener(OnClose);
            _outside.onClick.AddListener(OnClose);
            _see.onClick.AddListener(OnSee);
            for (int i = 0; i < _qty.Length; i++)
            {
                var qty = (PurchaseQty)i;
                _qty[i].onClick.AddListener(() => _actions?.SetQty(qty));
            }
            for (int i = 0; i < _cards.Length; i++)
            {
                _cards[i].Initialize(i, _tokens, OnBuy);
                _cards[i].ReduceMotion = _reduceMotion;
            }
        }

        private void OnBuy(int card) => _actions?.BuyUpgrade(card);

        private void OnClose() => _actions?.CloseUpgrades();

        private void OnSee() => _actions?.ShowNextZone();

        private void OnDestroy()
        {
            if (!_initialized)
                return;
            _close.onClick.RemoveListener(OnClose);
            _outside.onClick.RemoveListener(OnClose);
            _see.onClick.RemoveListener(OnSee);
            foreach (var button in _qty)
                button.onClick.RemoveAllListeners();
        }

        /// <summary>Prefab builder hook (Editor ProgressionUiPipeline).</summary>
        public void SetReferences(UiColorTokens tokens, Canvas canvas, GameObject content, CanvasGroup group, RectTransform safeArea, RectTransform panel,
            Button outside, Button close, TMP_Text title, TMP_Text rate, Button[] qty, Image[] qtyBack, TMP_Text[] qtyLabel, UpgradeCardView[] cards)
        {
            _tokens = tokens;
            _safeArea = safeArea;
            _canvas = canvas;
            _content = content;
            _group = group;
            _panel = panel;
            _outside = outside;
            _close = close;
            _title = title;
            _rate = rate;
            _qty = qty;
            _qtyBack = qtyBack;
            _qtyLabel = qtyLabel;
            _cards = cards;
        }

        /// <summary>Prefab builder hook for the block under the cards.</summary>
        public void SetFooterReferences(TMP_Text masteryTitle, TMP_Text masteryLevel, RectTransform masteryBar, RectTransform masteryFill,
            TMP_Text masteryNext, GameObject zoneBlock, TMP_Text zoneLabel, TMP_Text zoneTitle, GameObject zonePriceRow, TMP_Text zonePrice, TMP_Text zoneCondition,
            Button see, TMP_Text seeLabel)
        {
            _masteryTitle = masteryTitle;
            _masteryLevel = masteryLevel;
            _masteryBar = masteryBar;
            _masteryFill = masteryFill;
            _masteryNext = masteryNext;
            _zoneBlock = zoneBlock;
            _zoneLabel = zoneLabel;
            _zoneTitle = zoneTitle;
            _zonePriceRow = zonePriceRow;
            _zonePrice = zonePrice;
            _zoneCondition = zoneCondition;
            _see = see;
            _seeLabel = seeLabel;
        }
    }
}
