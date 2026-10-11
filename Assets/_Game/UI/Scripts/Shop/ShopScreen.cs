using System;
using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_Shop (UI doc SCREEN 013, T28 sandbox scope): Featured and Gems sections of reusable product cards, the
    /// Gems balance, Restore purchases and a short feedback line. Opaque: the refuge camera stops behind it.
    /// Talks only to <see cref="IShopModel"/> and <see cref="IShopActions"/>; never to a store. No Update: ticked
    /// by the refuge while open; content saved inactive so a closed shop costs nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopScreen : MonoBehaviour, IShopView
    {
        private const float ToastSeconds = 2.6f;
        private const float SectionGap = 36f;
        private const float HeaderHeight = 84f;

        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private GameObject _content;
        [SerializeField] private RectTransform _safeArea;
        [SerializeField] private Button _back;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _gemsLabel;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private RectTransform _list;
        [Tooltip("T48: the « Événement » section (Carnet d'or, Coffret) above Featured while an event runs.")]
        [SerializeField] private TMP_Text _eventHeader;
        [SerializeField] private ShopCardView[] _eventCards = Array.Empty<ShopCardView>();
        [SerializeField] private TMP_Text _featuredHeader;
        [SerializeField] private ShopCardView[] _featuredCards = Array.Empty<ShopCardView>();
        [SerializeField] private TMP_Text _gemsHeader;
        [SerializeField] private ShopCardView[] _gemCards = Array.Empty<ShopCardView>();
        [SerializeField] private Button _restore;
        [SerializeField] private TMP_Text _restoreLabel;
        [SerializeField] private CanvasGroup _toast;
        [SerializeField] private TMP_Text _toastLabel;
        [SerializeField] private ShopIconSet _icons = new ShopIconSet();
        [SerializeField] private float _featuredHeight = 430f;
        [Tooltip("T47: a pack card (content row, restore note) is taller.")]
        [SerializeField] private float _packHeight = 760f;
        [SerializeField] private float _gemCardHeight = 470f;
        [SerializeField] private float _gemRowGap = 28f;

        private IShopModel _model;
        private ILocalization _text;
        private IShopActions _actions;
        private Func<string, Sprite> _sprites;
        private bool _initialized;
        private float _toastTime = -1f;
        private readonly SafeAreaFitter _fitter = new SafeAreaFitter();

        public bool IsOpen => _content != null && _content.activeSelf;
        public string ToastText => _toastLabel != null && _toast != null && _toast.alpha > 0f ? _toastLabel.text : string.Empty;
        public int VisibleGemCards { get; private set; }
        public int VisibleFeaturedCards { get; private set; }
        public int VisibleEventCards { get; private set; }
        public ShopCardView GetEventCard(int index) => _eventCards[index];
        public ShopCardView GetGemCard(int index) => _gemCards[index];
        public ShopCardView GetFeaturedCard(int index) => _featuredCards[index];

        public void Open(IShopModel model, ILocalization text, IShopActions actions)
        {
            _model = model;
            _text = text;
            _actions = actions;
            Initialize();
            _title.SetText(ShopText.Get(text, UiTextKeys.ShopTitle));
            _featuredHeader.SetText(ShopText.Get(text, UiTextKeys.ShopFeatured));
            if (_eventHeader != null)
                _eventHeader.SetText(ShopText.Get(text, EventText.ShopSection));
            _gemsHeader.SetText(ShopText.Get(text, UiTextKeys.ShopGems));
            _restoreLabel.SetText(ShopText.Get(text, UiTextKeys.ShopRestore));
            _toast.alpha = 0f;
            _toastTime = -1f;
            _fitter.Fit(_safeArea);
            Refresh();
            _content.SetActive(true);
            _scroll.verticalNormalizedPosition = 1f;
        }

        public void Close()
        {
            if (_content != null)
                _content.SetActive(false);
        }

        public void Prewarm(IShopModel model, ILocalization text, IShopActions actions)
        {
            if (IsOpen)
                return;
            Open(model, text, actions);
            Canvas.ForceUpdateCanvases();
            Close();
        }

        public void Refresh()
        {
            if (_model == null)
                return;
            _gemsLabel.SetText(ShopText.Grouped(_model.Gems, ShopText.GroupSeparator(_text)));
            bool busy = _model.Busy;
            var busyOffer = _model.BusyOffer;

            float y = 0f;
            // T48: « Événement » first while an event runs (the Carnet d'or and the Coffret, never as popups).
            var events = _model as IShopEventModel;
            VisibleEventCards = events != null && _eventHeader != null ? Mathf.Min(events.EventCount, _eventCards.Length) : 0;
            if (_eventHeader != null)
                _eventHeader.gameObject.SetActive(VisibleEventCards > 0);
            if (VisibleEventCards > 0)
            {
                Place(_eventHeader.rectTransform, ref y, HeaderHeight);
                for (int i = 0; i < VisibleEventCards; i++)
                {
                    var card = events.GetEvent(i);
                    _eventCards[i].gameObject.SetActive(true);
                    _eventCards[i].Bind(card, _text, _sprites, busy, busyOffer == card.Offer);
                    Place((RectTransform)_eventCards[i].transform, ref y, _packHeight);
                    y += SectionGap * 0.5f;
                }
                y += SectionGap;
            }
            for (int i = VisibleEventCards; i < _eventCards.Length; i++)
                _eventCards[i].gameObject.SetActive(false);

            VisibleFeaturedCards = Mathf.Min(_model.FeaturedCount, _featuredCards.Length);
            _featuredHeader.gameObject.SetActive(VisibleFeaturedCards > 0);
            if (VisibleFeaturedCards > 0)
            {
                Place(_featuredHeader.rectTransform, ref y, HeaderHeight);
                for (int i = 0; i < VisibleFeaturedCards; i++)
                {
                    var card = _model.GetFeatured(i);
                    _featuredCards[i].gameObject.SetActive(true);
                    _featuredCards[i].Bind(card, _text, _sprites, busy, busyOffer == card.Offer);
                    Place((RectTransform)_featuredCards[i].transform, ref y, card.ContentCount > 0 ? _packHeight : _featuredHeight);
                    y += SectionGap * 0.5f;
                }
                y += SectionGap;
            }
            for (int i = VisibleFeaturedCards; i < _featuredCards.Length; i++)
                _featuredCards[i].gameObject.SetActive(false);

            VisibleGemCards = Mathf.Min(_model.GemPackCount, _gemCards.Length);
            _gemsHeader.gameObject.SetActive(VisibleGemCards > 0);
            if (VisibleGemCards > 0)
                Place(_gemsHeader.rectTransform, ref y, HeaderHeight);
            // Two columns anchored to halves of the list: right at any resolution, no width read from layout.
            float halfGap = _gemRowGap * 0.5f;
            for (int i = 0; i < _gemCards.Length; i++)
            {
                bool visible = i < VisibleGemCards;
                _gemCards[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var card = _model.GetGemPack(i);
                _gemCards[i].Bind(card, _text, _sprites, busy, busyOffer == card.Offer);
                var rect = (RectTransform)_gemCards[i].transform;
                int row = i / 2, column = i % 2;
                rect.anchorMin = new Vector2(column * 0.5f, 1f);
                rect.anchorMax = new Vector2(column * 0.5f + 0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                float top = -(y + row * (_gemCardHeight + _gemRowGap));
                rect.offsetMin = new Vector2(column == 0 ? 0f : halfGap, top - _gemCardHeight);
                rect.offsetMax = new Vector2(column == 0 ? -halfGap : 0f, top);
            }
            if (VisibleGemCards > 0)
                y += ((VisibleGemCards + 1) / 2) * (_gemCardHeight + _gemRowGap);
            y += SectionGap * 0.5f;
            Place((RectTransform)_restore.transform, ref y, 110f);
            _restore.interactable = !busy;
            _list.sizeDelta = new Vector2(_list.sizeDelta.x, y + SectionGap * 2f);
        }

        public void ShowMessage(ShopMessage message, long value)
        {
            _toastLabel.SetText(ShopText.Message(_text, message, value));
            _toastLabel.color = message == ShopMessage.Failed || message == ShopMessage.Unavailable || message == ShopMessage.NotEnoughGems
                ? _tokens.errorSoft : _tokens.textPrimary;
            _toastTime = 0f;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsOpen)
                return;
            _fitter.Fit(_safeArea);
            for (int i = 0; i < VisibleFeaturedCards; i++)
                _featuredCards[i].Tick(unscaledDeltaTime);
            for (int i = 0; i < VisibleEventCards; i++)
                _eventCards[i].Tick(unscaledDeltaTime);
            for (int i = 0; i < VisibleGemCards; i++)
                _gemCards[i].Tick(unscaledDeltaTime);
            if (_toastTime < 0f)
                return;
            _toastTime += unscaledDeltaTime;
            float t = _toastTime;
            _toast.alpha = Mathf.Min(Mathf.Clamp01(t / 0.18f), Mathf.Clamp01((ToastSeconds - t) / 0.3f));
            if (t >= ToastSeconds)
            {
                _toastTime = -1f;
                _toast.alpha = 0f;
            }
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _sprites = _icons.Find;
            _back.onClick.AddListener(OnBack);
            _restore.onClick.AddListener(OnRestore);
            foreach (var card in _featuredCards)
                card.Initialize(OnBuy);
            foreach (var card in _eventCards)
                card.Initialize(OnBuy);
            foreach (var card in _gemCards)
                card.Initialize(OnBuy);
        }

        private static void Place(RectTransform rect, ref float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, -y);
            y += height;
        }

        private void OnBuy(Core.ContentId offer) => _actions?.Buy(offer);
        private void OnBack() => _actions?.CloseShop();
        private void OnRestore() => _actions?.Restore();

        private void OnDestroy()
        {
            if (_back != null)
                _back.onClick.RemoveListener(OnBack);
            if (_restore != null)
                _restore.onClick.RemoveListener(OnRestore);
        }

        /// <summary>T48 prefab builder hook: the « Événement » header and its two wide cards (Editor ShopUiPipeline).</summary>
        public void SetEventReferences(TMP_Text header, ShopCardView[] cards)
        {
            _eventHeader = header;
            _eventCards = cards ?? Array.Empty<ShopCardView>();
        }

        /// <summary>Prefab builder hook (Editor ShopUiPipeline).</summary>
        public void SetReferences(UiColorTokens tokens, GameObject content, RectTransform safeArea, Button back, TMP_Text title, TMP_Text gemsLabel, ScrollRect scroll,
            RectTransform list, TMP_Text featuredHeader, ShopCardView[] featuredCards, TMP_Text gemsHeader, ShopCardView[] gemCards,
            Button restore, TMP_Text restoreLabel, CanvasGroup toast, TMP_Text toastLabel, ShopIconSet.Entry[] icons)
        {
            _tokens = tokens;
            _content = content;
            _safeArea = safeArea;
            _back = back;
            _title = title;
            _gemsLabel = gemsLabel;
            _scroll = scroll;
            _list = list;
            _featuredHeader = featuredHeader;
            _featuredCards = featuredCards;
            _gemsHeader = gemsHeader;
            _gemCards = gemCards;
            _restore = restore;
            _restoreLabel = restoreLabel;
            _toast = toast;
            _toastLabel = toastLabel;
            _icons.Set(icons);
        }
    }
}
