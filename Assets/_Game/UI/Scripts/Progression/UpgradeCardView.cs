using System;
using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_UpgradeCard (doc 02 Améliorations, section 11.2): icon, name, level, what the purchase brings, the price
    /// and the buy button. Four states: affordable, not enough Light (soft price, never red), maxed, locked. A purchase
    /// squashes the card and bounces it to 1.06 while six round motes rise (a ×2 milestone adds a golden ring and round
    /// five-point stars). Reused: one card per kind, rebound when the model changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UpgradeCardView : MonoBehaviour
    {
        private const float BuySeconds = 0.25f;
        private const float MilestoneSeconds = 0.9f;
        private const float ShakeSeconds = 0.3f;

        [SerializeField] private RectTransform _root;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _level;
        [SerializeField] private TMP_Text _gain;
        [SerializeField] private TMP_Text _detail;
        [SerializeField] private Button _buy;
        [SerializeField] private Image _buyBack;
        [SerializeField] private GameObject _priceRow;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private TMP_Text _levels;
        [SerializeField] private TMP_Text _state;
        [SerializeField] private Image _stateIcon;
        [SerializeField] private GameObject _best;
        [SerializeField] private TMP_Text _bestLabel;
        [SerializeField] private RectTransform[] _motes = Array.Empty<RectTransform>();
        [SerializeField] private Image[] _moteImages = Array.Empty<Image>();
        [SerializeField] private Sprite _dot;
        [SerializeField] private Sprite _star;
        [SerializeField] private RectTransform _ring;
        [SerializeField] private Image _ringImage;

        private UiColorTokens _tokens;
        private BigValueText _priceText;
        private Action<int> _onBuy;
        private int _index;
        private Vector2 _home;
        private float _buyTime = -1f;
        private float _ringTime = -1f;
        private float _shakeTime = -1f;
        private bool _moved;
        private int _levelShown = -1;
        private int _levelsShown = -1;
        private string _levelTemplate;
        private string _levelsTemplate;

        public bool ReduceMotion { get; set; }
        public UpgradeCardState State { get; private set; }
        public bool BestShown => _best != null && _best.activeSelf;
        public string GainText => _gain != null ? _gain.text : string.Empty;
        public string PriceText => _price != null ? _price.text : string.Empty;
        public Button BuyButton => _buy;
        public bool Animating => _buyTime >= 0f || _ringTime >= 0f || _shakeTime >= 0f;

        public void Initialize(int index, UiColorTokens tokens, Action<int> onBuy)
        {
            _index = index;
            _tokens = tokens;
            _onBuy = onBuy;
            _priceText = new BigValueText(_price);
            _home = _root.anchoredPosition;
            _buy.onClick.AddListener(OnBuy);
            for (int i = 0; i < _motes.Length; i++)
                _motes[i].gameObject.SetActive(false);
            _ring.gameObject.SetActive(false);
        }

        public void SetTexts(ILocalization text)
        {
            _levelTemplate = text.Get(ProgressionText.UpgradesLevel);
            _levelsTemplate = text.Get(ProgressionText.UpgradesLevels);
            _bestLabel.text = text.Get(ProgressionText.UpgradesBest);
            _levelShown = -1;
            _levelsShown = -1;
        }

        public void Bind(in UpgradeCardModel card, ILocalization text)
        {
            State = card.State;
            _name.text = card.Name;
            if (card.Level != _levelShown)
            {
                _levelShown = card.Level;
                _level.text = _levelTemplate.Replace("{n}", card.Level.ToString());
            }
            _gain.text = card.Gain;
            _detail.text = card.Detail;
            _best.SetActive(card.Best && card.State == UpgradeCardState.Affordable);

            bool buyable = card.State == UpgradeCardState.Affordable || card.State == UpgradeCardState.NotEnough;
            _buy.gameObject.SetActive(buyable);
            _state.gameObject.SetActive(!buyable);
            if (buyable)
            {
                _priceText.Set(card.Price);
                if (card.Levels != _levelsShown)
                {
                    _levelsShown = card.Levels;
                    _levels.text = _levelsTemplate.Replace("{n}", card.Levels.ToString());
                }
                bool affordable = card.State == UpgradeCardState.Affordable;
                _buyBack.color = affordable ? _tokens.accentPrimary : _tokens.accentIdle;
                _price.color = affordable ? _tokens.textPrimary : _tokens.textSecondary;
                _levels.color = affordable ? _tokens.textPrimary : _tokens.textSecondary;
            }
            else
            {
                bool locked = card.State == UpgradeCardState.Locked;
                _state.text = locked ? card.LockText : text.Get(ProgressionText.UpgradesMax);
                _state.color = locked ? _tokens.textSecondary : _tokens.success;
                _stateIcon.gameObject.SetActive(locked);
            }
            _gain.color = card.State == UpgradeCardState.Locked ? _tokens.textSecondary : GainColor;
        }

        private static readonly Color GainColor = new Color(0.79f, 0.47f, 0.16f, 1f);

        public void PlayPurchase(bool milestone)
        {
            if (ReduceMotion)
                return;
            _buyTime = 0f;
            for (int i = 0; i < _motes.Length; i++)
            {
                _motes[i].gameObject.SetActive(true);
                _moteImages[i].sprite = milestone && i % 2 == 0 ? _star : _dot;
                _moteImages[i].color = _tokens.currencyLight;
            }
            if (milestone)
            {
                _ringTime = 0f;
                _ring.gameObject.SetActive(true);
            }
        }

        public void PlayNotEnough()
        {
            if (!ReduceMotion)
                _shakeTime = 0f;
        }

        public void Tick(float dt)
        {
            // Idle cards are not touched (no canvas rebuild while the screen just sits open).
            if (!Animating && !_moved)
                return;
            _moved = Animating;
            float scale = 1f;
            if (_buyTime >= 0f)
            {
                _buyTime += dt;
                float t = Mathf.Clamp01(_buyTime / BuySeconds);
                // Squash to 0.95, bounce to 1.06, settle at 1.
                scale = t < 0.3f ? Mathf.Lerp(1f, 0.95f, t / 0.3f) : t < 0.65f ? Mathf.Lerp(0.95f, 1.06f, (t - 0.3f) / 0.35f)
                    : Mathf.Lerp(1.06f, 1f, (t - 0.65f) / 0.35f);
                // Motes rise from the button for a little longer than the bounce, then fade.
                float m = Mathf.Clamp01(_buyTime / (BuySeconds * 2.4f));
                for (int i = 0; i < _motes.Length; i++)
                {
                    float spread = (i - (_motes.Length - 1) * 0.5f) * 34f;
                    float rise = 40f + 150f * (1f - (1f - m) * (1f - m)) * (0.75f + 0.08f * (i % 3));
                    _motes[i].anchoredPosition = new Vector2(spread * (0.6f + m), rise);
                    var color = _moteImages[i].color;
                    color.a = 1f - m * m;
                    _moteImages[i].color = color;
                }
                if (m >= 1f)
                {
                    _buyTime = -1f;
                    for (int i = 0; i < _motes.Length; i++)
                        _motes[i].gameObject.SetActive(false);
                }
            }
            if (_ringTime >= 0f)
            {
                _ringTime += dt;
                float t = Mathf.Clamp01(_ringTime / MilestoneSeconds);
                float size = Mathf.Lerp(0.6f, 1.6f, 1f - (1f - t) * (1f - t));
                _ring.localScale = new Vector3(size, size, 1f);
                var color = _tokens.currencyLight;
                color.a = 1f - t;
                _ringImage.color = color;
                if (t >= 1f)
                {
                    _ringTime = -1f;
                    _ring.gameObject.SetActive(false);
                }
            }
            float shake = 0f;
            if (_shakeTime >= 0f)
            {
                _shakeTime += dt;
                float decay = Mathf.Clamp01(1f - _shakeTime / ShakeSeconds);
                shake = 12f * decay * Mathf.Sin(_shakeTime * 46f);
                if (decay <= 0f)
                    _shakeTime = -1f;
            }
            _root.localScale = new Vector3(scale, scale, 1f);
            _root.anchoredPosition = _home + new Vector2(shake, 0f);
        }

        private void OnBuy() => _onBuy?.Invoke(_index);

        private void OnDestroy()
        {
            if (_buy != null)
                _buy.onClick.RemoveListener(OnBuy);
        }

        /// <summary>Prefab builder hook (Editor ProgressionUiPipeline).</summary>
        public void SetReferences(RectTransform root, TMP_Text name, TMP_Text level, TMP_Text gain, TMP_Text detail, Button buy, Image buyBack,
            GameObject priceRow, TMP_Text price, TMP_Text levels, TMP_Text state, Image stateIcon, GameObject best, TMP_Text bestLabel,
            RectTransform[] motes, Image[] moteImages, Sprite dot, Sprite star, RectTransform ring, Image ringImage)
        {
            _root = root;
            _name = name;
            _level = level;
            _gain = gain;
            _detail = detail;
            _buy = buy;
            _buyBack = buyBack;
            _priceRow = priceRow;
            _price = price;
            _levels = levels;
            _state = state;
            _stateIcon = stateIcon;
            _best = best;
            _bestLabel = bestLabel;
            _motes = motes;
            _moteImages = moteImages;
            _dot = dot;
            _star = star;
            _ring = ring;
            _ringImage = ringImage;
        }
    }
}
