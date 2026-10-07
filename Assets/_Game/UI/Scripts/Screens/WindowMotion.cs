using UnityEngine;
using UnityEngine.InputSystem;

namespace PocketWilds.UI
{
    /// <summary>
    /// T35A: the shared open and close motion of the progression windows (zone, refuge level, reward card, gate) and
    /// the Upgrades panel. A window fades in with a slight scale (or slides up for a bottom panel); with Reduce Motion
    /// it only fades. The canvas is disabled and the content inactive while closed, so a closed window costs nothing.
    /// Plain class, ticked by its screen.
    /// </summary>
    public sealed class WindowMotion
    {
        private readonly float _seconds;
        private readonly bool _slide;
        private Canvas _canvas;
        private GameObject _content;
        private CanvasGroup _group;
        private RectTransform _card;
        private Vector2 _home;
        private float _time = -1f;
        private bool _closing;

        /// <param name="slide">A bottom panel: it slides up from below the screen instead of scaling.</param>
        public WindowMotion(float seconds = 0.16f, bool slide = false)
        {
            _seconds = seconds;
            _slide = slide;
        }

        public bool IsOpen { get; private set; }
        public bool Closing => _closing;
        public bool ReduceMotion { get; set; }

        /// <summary>Escape (Android back) asks to close, once.</summary>
        public bool BackPressed => IsOpen && !_closing && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        public void Bind(Canvas canvas, GameObject content, CanvasGroup group, RectTransform card)
        {
            _canvas = canvas;
            _content = content;
            _group = group;
            _card = card;
            if (_card != null)
                _home = _card.anchoredPosition;
        }

        public void Show()
        {
            IsOpen = true;
            _closing = false;
            _time = 0f;
            if (_canvas != null)
                _canvas.enabled = true;
            _content.SetActive(true);
            Apply(0f);
        }

        public void Hide()
        {
            if (!IsOpen || _closing)
                return;
            _closing = true;
            _time = 0f;
        }

        /// <summary>Closed at once (prewarm, scene exit).</summary>
        public void HideNow()
        {
            IsOpen = false;
            _closing = false;
            _content.SetActive(false);
            if (_canvas != null)
                _canvas.enabled = false;
            if (_card != null)
            {
                _card.anchoredPosition = _home;
                _card.localScale = Vector3.one;
            }
        }

        /// <summary>Returns true on the frame the window finished closing.</summary>
        public bool Tick(float unscaledDeltaTime)
        {
            if (!IsOpen)
                return false;
            _time += unscaledDeltaTime;
            float k = Mathf.Clamp01(_time / _seconds);
            Apply(_closing ? 1f - k : k);
            if (!_closing || k < 1f)
                return false;
            HideNow();
            return true;
        }

        private void Apply(float k)
        {
            float eased = 1f - (1f - k) * (1f - k);
            if (_group != null)
                _group.alpha = _slide && !ReduceMotion ? Mathf.Clamp01(k * 3f) : k;
            if (_card == null)
                return;
            if (ReduceMotion)
            {
                _card.localScale = Vector3.one;
                _card.anchoredPosition = _home;
            }
            else if (_slide)
            {
                _card.anchoredPosition = _home - new Vector2(0f, (1f - eased) * _card.rect.height);
            }
            else
            {
                float scale = Mathf.Lerp(0.94f, 1f, eased);
                _card.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
