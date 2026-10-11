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

        [Header("T47 pack (« Pack de la Prairie »)")]
        [SerializeField] private GameObject _oneTimeChip;
        [SerializeField] private TMP_Text _oneTimeLabel;
        [SerializeField] private GameObject _packRow;
        [SerializeField] private Image[] _packIcons = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] _packLabels = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text _note;
        [SerializeField] private Sprite _packFallback;
        [Tooltip("The buy button's place for other offers, then for a pack (bottom anchors, pivot = anchor).")]
        [SerializeField] private Vector2 _buyAnchor = new Vector2(1f, 0f);
        [SerializeField] private Vector2 _buyPosition;
        [SerializeField] private Vector2 _packBuyAnchor = new Vector2(1f, 0f);
        [SerializeField] private Vector2 _packBuyPosition;

        private Action<ContentId> _onBuy;
        private ContentId _offer;
        private bool _waiting;
        private float _waitTime;

        public ContentId Offer => _offer;
        public string PriceText => _price != null ? _price.text : string.Empty;
        public bool Interactable => _buy != null && _buy.interactable;
        public string TitleText => _title != null ? _title.text : string.Empty;
        public string ContentsText => _contents != null && _contents.gameObject.activeSelf ? _contents.text : string.Empty;
        public string OneTimeText => _oneTimeChip != null && _oneTimeChip.activeSelf ? _oneTimeLabel.text : string.Empty;
        public string NoteText => _note != null && _note.gameObject.activeSelf ? _note.text : string.Empty;
        public Sprite ArtShown => _art != null && _art.enabled ? _art.sprite : null;

        /// <summary>T47: the pack's content row (icons with labels) as shown.</summary>
        public int PackItemsShown { get; private set; }
        public string PackLabel(int index) => _packLabels[index].text;
        public Sprite PackIcon(int index) => _packIcons[index].sprite;

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

            BindPack(card, text, sprites);
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

        /// <summary>
        /// T47 pack: the body line, « Offre unique » as a badge (no caption), the content row (the companion, the Gems, the burrow,
        /// the crown), the restore note under the button, which takes its pack place. Other offers keep the T28 look.
        /// </summary>
        private void BindPack(in ShopCardModel card, ILocalization text, Func<string, Sprite> sprites)
        {
            bool pack = card.ContentCount > 0;
            if (pack)
            {
                if (_gemsIcon != null)
                    _gemsIcon.SetActive(false);
                if (_contents != null && !string.IsNullOrEmpty(card.BodyKey))
                    _contents.SetText(ShopText.Get(text, card.BodyKey));
            }
            bool chip = pack && !string.IsNullOrEmpty(card.OneTimeKey) && !card.Owned && _oneTimeChip != null;
            if (_oneTimeChip != null)
            {
                _oneTimeChip.SetActive(chip);
                if (chip)
                    _oneTimeLabel.SetText(ShopText.Get(text, card.OneTimeKey));
            }
            if (chip && _caption != null)
                _caption.gameObject.SetActive(false);
            PackItemsShown = 0;
            if (_packRow != null)
            {
                _packRow.SetActive(pack);
                for (int i = 0; i < _packIcons.Length && i < _packLabels.Length; i++)
                {
                    bool on = pack && i < card.ContentCount;
                    _packIcons[i].transform.parent.gameObject.SetActive(on);
                    if (!on)
                        continue;
                    var item = card.GetContent(i);
                    var icon = string.IsNullOrEmpty(item.IconKey) ? null : sprites(item.IconKey);
                    _packIcons[i].sprite = icon != null ? icon : _packFallback;
                    _packLabels[i].SetText(item.Amount > 0
                        ? ShopText.Format(text, UiTextKeys.ShopContentsGems, item.Amount)
                        : ShopText.Get(text, item.LabelKey));
                    PackItemsShown++;
                }
            }
            if (_note != null)
            {
                _note.gameObject.SetActive(pack && card.RestoreNote);
                if (pack && card.RestoreNote)
                    _note.SetText(ShopText.Get(text, UiTextKeys.ShopRestoreNote));
            }
            var buy = _buyBackground != null ? _buyBackground.rectTransform : null;
            if (buy != null && _packRow != null)
            {
                var anchor = pack ? _packBuyAnchor : _buyAnchor;
                buy.anchorMin = anchor;
                buy.anchorMax = anchor;
                buy.pivot = anchor;
                buy.anchoredPosition = pack ? _packBuyPosition : _buyPosition;
            }
        }

        /// <summary>Prefab builder hook for the T47 pack parts (Editor ShopUiPipeline).</summary>
        public void SetPackReferences(GameObject oneTimeChip, TMP_Text oneTimeLabel, GameObject packRow, Image[] packIcons, TMP_Text[] packLabels,
            TMP_Text note, Sprite packFallback, Vector2 buyAnchor, Vector2 buyPosition, Vector2 packBuyAnchor, Vector2 packBuyPosition)
        {
            _oneTimeChip = oneTimeChip;
            _oneTimeLabel = oneTimeLabel;
            _packRow = packRow;
            _packIcons = packIcons;
            _packLabels = packLabels;
            _note = note;
            _packFallback = packFallback;
            _buyAnchor = buyAnchor;
            _buyPosition = buyPosition;
            _packBuyAnchor = packBuyAnchor;
            _packBuyPosition = packBuyPosition;
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
