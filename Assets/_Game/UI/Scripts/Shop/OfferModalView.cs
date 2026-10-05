using System;
using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_OfferModal (UI doc SCREEN 014): one offer over the sanctuary, the hero art, what you get, the price,
    /// close and buy. No timer unless the offer really ends (the sandbox Starter Pack does not). Feedback line under
    /// the card. Content saved inactive; ticked by the sanctuary while open.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfferModalView : MonoBehaviour, IOfferModalView
    {
        private const float ToastSeconds = 2.4f;

        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private GameObject _content;
        [SerializeField] private Button _close;
        [SerializeField] private ShopCardView _card;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private CanvasGroup _toast;
        [SerializeField] private TMP_Text _toastLabel;
        [SerializeField] private ShopIconSet _icons = new ShopIconSet();

        private IShopModel _model;
        private ILocalization _text;
        private IShopActions _actions;
        private Func<string, Sprite> _sprites;
        private bool _initialized;
        private float _toastTime = -1f;
        private float _openTime = -1f;

        public bool IsOpen => _content != null && _content.activeSelf;
        public ShopCardView Card => _card;
        public string ToastText => _toast != null && _toast.alpha > 0f ? _toastLabel.text : string.Empty;

        /// <summary>Reduce Motion: the card appears without the scale pop.</summary>
        public bool ReduceMotion { get; set; }

        public void Open(ShopCardModel card, IShopModel model, ILocalization text, IShopActions actions)
        {
            _model = model;
            _text = text;
            _actions = actions;
            if (!_initialized)
            {
                _initialized = true;
                _sprites = _icons.Find;
                _close.onClick.AddListener(OnClose);
                _card.Initialize(OnBuy);
            }
            _toast.alpha = 0f;
            _toastTime = -1f;
            _openTime = 0f;
            Refresh(card);
            _content.SetActive(true);
        }

        public void Close()
        {
            if (_content != null)
                _content.SetActive(false);
        }

        public void Prewarm(ShopCardModel card, IShopModel model, ILocalization text, IShopActions actions)
        {
            if (IsOpen)
                return;
            Open(card, model, text, actions);
            Canvas.ForceUpdateCanvases();
            Close();
        }

        public void Refresh(ShopCardModel card)
        {
            bool busy = _model != null && _model.Busy;
            _card.Bind(card, _text, _sprites, busy, busy && _model.BusyOffer == card.Offer);
        }

        public void ShowMessage(ShopMessage message, long value)
        {
            _toastLabel.SetText(ShopText.Message(_text, message, value));
            _toastLabel.color = message == ShopMessage.Failed || message == ShopMessage.Unavailable ? _tokens.errorSoft : _tokens.textPrimary;
            _toastTime = 0f;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsOpen)
                return;
            _card.Tick(unscaledDeltaTime);
            if (_openTime >= 0f)
            {
                _openTime += unscaledDeltaTime;
                float t = Mathf.Clamp01(_openTime / 0.22f);
                float s = ReduceMotion ? 1f : 0.9f + 0.1f * (1f - (1f - t) * (1f - t));
                _panel.localScale = new Vector3(s, s, 1f);
                if (t >= 1f)
                    _openTime = -1f;
            }
            if (_toastTime < 0f)
                return;
            _toastTime += unscaledDeltaTime;
            _toast.alpha = Mathf.Min(Mathf.Clamp01(_toastTime / 0.18f), Mathf.Clamp01((ToastSeconds - _toastTime) / 0.3f));
            if (_toastTime >= ToastSeconds)
            {
                _toastTime = -1f;
                _toast.alpha = 0f;
            }
        }

        private void OnBuy(ContentId offer) => _actions?.Buy(offer);
        private void OnClose() => _actions?.CloseOffer();

        private void OnDestroy()
        {
            if (_close != null)
                _close.onClick.RemoveListener(OnClose);
        }

        /// <summary>Prefab builder hook (Editor ShopUiPipeline).</summary>
        public void SetReferences(UiColorTokens tokens, GameObject content, Button close, ShopCardView card, RectTransform panel,
            CanvasGroup toast, TMP_Text toastLabel, ShopIconSet.Entry[] icons)
        {
            _tokens = tokens;
            _content = content;
            _close = close;
            _card = card;
            _panel = panel;
            _toast = toast;
            _toastLabel = toastLabel;
            _icons.Set(icons);
        }
    }
}
