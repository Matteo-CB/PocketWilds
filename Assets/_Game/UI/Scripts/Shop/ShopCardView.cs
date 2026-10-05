using System;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_ProductCard (UI doc SCREEN 013): art, amount or title, value chip, badge, price button. One
    /// component for Gem packs, featured offers and the offer modal; states use color tokens, not extra sprites.
    /// Bound on shop events only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopCardView : MonoBehaviour
    {
        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private Image _art;
        [SerializeField] private Image _itemIcon;
        [SerializeField] private TMP_Text _title;
        [Tooltip("Gem packs: small gem left of the amount, kept centered with it.")]
        [SerializeField] private RectTransform _titleIcon;
        [Tooltip("Offers: gem badge on the art when the offer also gives Gems.")]
        [SerializeField] private GameObject _gemsIcon;
        [SerializeField] private TMP_Text _contents;
        [SerializeField] private TMP_Text _caption;
        [SerializeField] private GameObject _bonus;
        [SerializeField] private TMP_Text _bonusLabel;
        [SerializeField] private GameObject _badge;
        [SerializeField] private TMP_Text _badgeLabel;
        [SerializeField] private Button _buy;
        [SerializeField] private Image _buyBackground;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private Image _priceGem;
        [SerializeField] private RectTransform _wait;

        private Action<ContentId> _onBuy;
        private ContentId _offer;
        private bool _waiting;
        private float _waitTime;

        public ContentId Offer => _offer;
        public string PriceText => _price != null ? _price.text : string.Empty;
        public bool Interactable => _buy != null && _buy.interactable;

        public void Initialize(Action<ContentId> onBuy)
        {
            _onBuy = onBuy;
            _buy.onClick.RemoveListener(OnBuy);
            _buy.onClick.AddListener(OnBuy);
        }

        public void Bind(in ShopCardModel card, ILocalization text, Func<string, Sprite> sprites, bool busy, bool thisBusy)
        {
            _offer = card.Offer;
            if (_art != null)
            {
                var art = sprites(card.ArtKey);
                _art.sprite = art;
                _art.enabled = art != null;
            }
            if (_itemIcon != null)
            {
                var icon = string.IsNullOrEmpty(card.ItemIconKey) ? null : sprites(card.ItemIconKey);
                var artSprite = _art != null ? _art.sprite : null;
                _itemIcon.gameObject.SetActive(icon != null && icon != artSprite);
                _itemIcon.sprite = icon;
            }
            if (_title != null)
            {
                // Gem packs: the amount is the title. Offers: their name.
                _title.SetText(string.IsNullOrEmpty(card.TitleKey)
                    ? ShopText.Grouped(card.Gems, ShopText.GroupSeparator(text))
                    : ShopText.Get(text, card.TitleKey));
                if (_titleIcon != null)
                {
                    // Icon + amount centered as one group (bind time only).
                    float width = _title.GetPreferredValues().x;
                    float icon = _titleIcon.sizeDelta.x;
                    const float gap = 10f;
                    float left = -(width + icon + gap) * 0.5f;
                    _titleIcon.anchoredPosition = new Vector2(left + icon * 0.5f, _titleIcon.anchoredPosition.y);
                    _title.rectTransform.anchoredPosition = new Vector2(left + icon + gap + width * 0.5f, _title.rectTransform.anchoredPosition.y);
                }
            }
            if (_gemsIcon != null)
                _gemsIcon.SetActive(card.Gems > 0 && !string.IsNullOrEmpty(card.TitleKey));
            if (_contents != null)
            {
                bool show = !string.IsNullOrEmpty(card.TitleKey);
                _contents.gameObject.SetActive(show);
                if (show)
                    _contents.SetText(ShopText.Contents(text, card));
            }
            if (_caption != null)
            {
                _caption.gameObject.SetActive(card.OneTime && !card.Owned);
                _caption.SetText(ShopText.Get(text, UiTextKeys.ShopOneTime));
            }
            if (_bonus != null)
            {
                _bonus.SetActive(card.BonusPercent > 0);
                if (card.BonusPercent > 0)
                    _bonusLabel.SetText("+" + card.BonusPercent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%");
            }
            if (_badge != null)
            {
                bool owned = card.Owned;
                bool best = card.Badge == OfferBadge.BestValue && !owned;
                _badge.SetActive(owned || best);
                if (owned || best)
                {
                    _badgeLabel.SetText(ShopText.Get(text, owned ? UiTextKeys.ShopOwned : UiTextKeys.ShopBestValue));
                    _badge.GetComponent<Image>().color = owned ? _tokens.success : _tokens.currencyGems;
                }
            }

            ShopText.SetPrice(_price, text, card);
            if (_priceGem != null)
                _priceGem.gameObject.SetActive(card.GemCost > 0 && !card.Owned);
            bool priced = card.Price != null || card.GemCost > 0;
            _buy.interactable = !card.Owned && priced && !busy;
            _buyBackground.color = card.Owned ? _tokens.accentIdle : _tokens.accentPrimary;
            _price.color = card.Owned || !priced ? _tokens.textSecondary : _tokens.textPrimary;
            _waiting = thisBusy;
            if (_wait != null)
                _wait.gameObject.SetActive(thisBusy);
            _price.gameObject.SetActive(!thisBusy);
        }

        /// <summary>Three dots breathe while the store answers (transform only, no Animator).</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (!_waiting || _wait == null)
                return;
            _waitTime += unscaledDeltaTime;
            for (int i = 0; i < _wait.childCount; i++)
            {
                float s = 0.7f + 0.3f * Mathf.Max(0f, Mathf.Sin(_waitTime * 6f - i * 0.9f));
                _wait.GetChild(i).localScale = new Vector3(s, s, 1f);
            }
        }

        private void OnBuy() => _onBuy?.Invoke(_offer);

        private void OnDestroy()
        {
            if (_buy != null)
                _buy.onClick.RemoveListener(OnBuy);
        }

        /// <summary>Prefab builder hook (Editor ShopUiPipeline).</summary>
        public void SetReferences(UiColorTokens tokens, Image art, Image itemIcon, TMP_Text title, TMP_Text contents, TMP_Text caption,
            GameObject bonus, TMP_Text bonusLabel, GameObject badge, TMP_Text badgeLabel, Button buy, Image buyBackground, TMP_Text price,
            Image priceGem, RectTransform wait, RectTransform titleIcon = null, GameObject gemsIcon = null)
        {
            _titleIcon = titleIcon;
            _gemsIcon = gemsIcon;
            _tokens = tokens;
            _art = art;
            _itemIcon = itemIcon;
            _title = title;
            _contents = contents;
            _caption = caption;
            _bonus = bonus;
            _bonusLabel = bonusLabel;
            _badge = badge;
            _badgeLabel = badgeLabel;
            _buy = buy;
            _buyBackground = buyBackground;
            _price = price;
            _priceGem = priceGem;
            _wait = wait;
        }
    }
}
