using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T34A tutorial gesture (doc 06 SCREEN 026, UI_Gesture_Tap): a plush paw taps down toward a screen point (the
    /// first egg, never hidden by it) and a soft ring spreads on the point. No text: the animation explains. Passive (no
    /// raycast), own canvas, ticked by the HUD. Reduce Motion: the paw stays still, only the ring breathes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TapHint : MonoBehaviour
    {
        private const float Period = 1.3f;
        private const float FadeSpeed = 3.5f;

        [SerializeField] private RectTransform _root;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _hand;
        [SerializeField] private Image _ring;

        private float _alpha;
        private bool _visible;
        private float _time;
        private Vector2 _handHome;
        private bool _homeKnown;

        public bool ReduceMotion { get; set; }
        public bool Visible => _visible;
        public bool Shown => _root != null && _root.gameObject.activeSelf;

        /// <summary>Screen point of the target (the egg). The paw floats just above it.</summary>
        public void Set(bool visible, Vector2 screenPosition)
        {
            if (_root == null)
                return;
            if (!visible)
            {
                _visible = false;
                return;
            }
            var parent = (RectTransform)_root.parent;
            // Overlay canvas: no camera. Local point is relative to the parent's pivot.
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, null, out var local))
                SetLocal(local);
        }

        /// <summary>Shows the hand on a point of the parent's space (relative to its pivot). Captures use it directly.</summary>
        public void SetLocal(Vector2 local)
        {
            if (_root == null)
                return;
            _visible = true;
            var parent = (RectTransform)_root.parent;
            _root.anchoredPosition = local - Vector2.Scale(_root.anchorMin - parent.pivot, parent.rect.size);
            if (!_root.gameObject.activeSelf)
            {
                _time = 0f;
                _root.gameObject.SetActive(true);
            }
        }

        public void Tick(float deltaTime)
        {
            if (_root == null || !_root.gameObject.activeSelf)
                return;
            if (!_homeKnown)
            {
                _handHome = _hand.anchoredPosition;
                _homeKnown = true;
            }
            _alpha = Mathf.MoveTowards(_alpha, _visible ? 1f : 0f, deltaTime * FadeSpeed);
            _group.alpha = _alpha;
            if (_alpha <= 0f && !_visible)
            {
                _root.gameObject.SetActive(false);
                return;
            }
            _time += deltaTime;
            float phase = Mathf.Repeat(_time / Period, 1f);
            // Hand: comes down to the point (press), holds, lifts. Ring: spreads from the press.
            float press = phase < 0.3f ? Mathf.SmoothStep(0f, 1f, phase / 0.3f) : phase < 0.42f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (phase - 0.42f) / 0.35f);
            if (!ReduceMotion)
            {
                // The paw rests above the point and taps down toward it.
                _hand.anchoredPosition = _handHome + new Vector2(0f, -44f) * press;
                float s = 1f - 0.08f * press;
                _hand.localScale = new Vector3(s, s, 1f);
            }
            float ringT = Mathf.Clamp01((phase - 0.3f) / 0.6f);
            float ringScale = ReduceMotion ? 1f : Mathf.Lerp(0.45f, 1.35f, ringT);
            _ring.rectTransform.localScale = new Vector3(ringScale, ringScale, 1f);
            var color = _ring.color;
            color.a = ReduceMotion ? 0.35f + 0.25f * Mathf.Sin(_time * 3f) : (phase < 0.3f ? 0f : (1f - ringT) * 0.85f);
            _ring.color = color;
        }

        /// <summary>Prefab builder hook (Editor FeedbackUiPipeline).</summary>
        public void SetReferences(RectTransform root, CanvasGroup group, RectTransform hand, Image ring)
        {
            _root = root;
            _group = group;
            _hand = hand;
            _ring = ring;
        }
    }
}
