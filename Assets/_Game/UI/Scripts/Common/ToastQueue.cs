using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T34A toasts (doc 06 SCREEN 027): one reused panel, a bounded queue of four (the oldest waiting toast gives way),
    /// an icon per state and one short line. Passive (no raycast), own canvas, active only while a toast shows.
    /// Ticked by its screen; no Update, no allocation once the strings exist.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToastQueue : MonoBehaviour
    {
        public const int Capacity = 4;
        private const float In = 0.18f;
        private const float Hold = 2.1f;
        private const float Out = 0.25f;
        private const float Rise = 26f;

        [SerializeField] private RectTransform _root;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _label;
        [Tooltip("One icon per ToastKind: Reward, Discovery, Unlock, Visitor, Expedition, Warning.")]
        [SerializeField] private Sprite[] _icons;
        [SerializeField] private Color[] _iconTints;

        private readonly ToastKind[] _kinds = new ToastKind[Capacity];
        private readonly string[] _texts = new string[Capacity];
        private int _head;
        private int _count;
        private float _time = -1f;
        private Vector2 _home;
        private bool _homeKnown;

        public bool ReduceMotion { get; set; }

        /// <summary>The slot is busy (a notice shows there): toasts wait.</summary>
        public bool Paused { get; set; }

        public bool Showing => _time >= 0f;
        public int Waiting => _count;
        public string CurrentText => _label != null ? _label.text : string.Empty;
        public ToastKind CurrentKind { get; private set; }

        public void Enqueue(ToastKind kind, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            // The same toast already showing or waiting is not repeated.
            if (Showing && CurrentKind == kind && _label.text == text)
                return;
            for (int i = 0; i < _count; i++)
            {
                int k = (_head + i) % Capacity;
                if (_kinds[k] == kind && _texts[k] == text)
                    return;
            }
            if (_count == Capacity)
            {
                _head = (_head + 1) % Capacity;
                _count--;
            }
            int slot = (_head + _count) % Capacity;
            _kinds[slot] = kind;
            _texts[slot] = text;
            _count++;
        }

        public void Clear()
        {
            _count = 0;
            _time = -1f;
            if (_root != null)
                _root.gameObject.SetActive(false);
        }

        public void Tick(float deltaTime)
        {
            if (_root == null)
                return;
            if (!_homeKnown)
            {
                _home = _root.anchoredPosition;
                _homeKnown = true;
            }
            if (_time < 0f)
            {
                if (_count == 0 || Paused)
                    return;
                Next();
            }
            _time += deltaTime;
            float total = In + Hold + Out;
            if (_time >= total)
            {
                _time = -1f;
                _root.gameObject.SetActive(false);
                return;
            }
            float appear = Mathf.Clamp01(_time / In);
            float fade = Mathf.Clamp01((total - _time) / Out);
            _group.alpha = Mathf.Min(appear, fade);
            float eased = 1f - (1f - appear) * (1f - appear);
            float offset = ReduceMotion ? 0f : (1f - eased) * -Rise;
            _root.anchoredPosition = _home + new Vector2(0f, offset);
        }

        private void Next()
        {
            CurrentKind = _kinds[_head];
            _label.text = _texts[_head];
            _texts[_head] = null;
            _head = (_head + 1) % Capacity;
            _count--;
            int index = (int)CurrentKind;
            _icon.sprite = _icons != null && index < _icons.Length ? _icons[index] : null;
            _icon.color = _iconTints != null && index < _iconTints.Length ? _iconTints[index] : Color.white;
            _icon.enabled = _icon.sprite != null;
            _time = 0f;
            _group.alpha = 0f;
            _root.gameObject.SetActive(true);
        }

        /// <summary>Prefab builder hook (Editor FeedbackUiPipeline).</summary>
        public void SetReferences(RectTransform root, CanvasGroup group, Image icon, TMP_Text label, Sprite[] icons, Color[] tints)
        {
            _root = root;
            _group = group;
            _icon = icon;
            _label = label;
            _icons = icons;
            _iconTints = tints;
        }
    }
}
