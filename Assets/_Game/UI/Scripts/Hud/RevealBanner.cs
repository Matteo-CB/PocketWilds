using PocketWilds.Content;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T34A reveal banner (doc 06 SCREEN 003: rarity reveal label in Display style, New Species / New Variant
    /// badges). Passive: no raycast, own canvas, active only while shown. Rarity reads three ways: the badge motif,
    /// the stars, the word; the color only adds to them. Ticked by the HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RevealBanner : MonoBehaviour
    {
        private const float In = 0.22f;
        private const float Hold = 1.9f;
        private const float Out = 0.3f;
        private const int MaxStars = 6;

        [SerializeField] private RectTransform _root;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _glow;
        [SerializeField] private Image _badge;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private Image[] _stars;
        [SerializeField] private UiRaritySet _rarity;
        [SerializeField] private UiColorTokens _tokens;

        private float _time = -1f;

        public bool ReduceMotion { get; set; }
        public bool Visible => _time >= 0f;
        public string TitleText => _title != null ? _title.text : string.Empty;
        public string NameText => _name != null ? _name.text : string.Empty;
        public int StarsShown { get; private set; }
        public Sprite BadgeShown => _badge != null ? _badge.sprite : null;

        public void Show(in RevealInfo info)
        {
            if (_root == null)
                return;
            var color = RarityColor(info.Rarity);
            _title.text = info.Title;
            _name.text = info.Name;
            _badge.sprite = _rarity != null ? _rarity.Badge(info.Rarity) : null;
            _badge.color = color;
            _glow.color = new Color(color.r, color.g, color.b, 0.55f);
            StarsShown = Mathf.Min(MaxStars, (int)info.Rarity + 1);
            for (int i = 0; i < _stars.Length; i++)
            {
                bool on = i < StarsShown;
                if (_stars[i].gameObject.activeSelf != on)
                    _stars[i].gameObject.SetActive(on);
                _stars[i].color = color;
            }
            // Stars stay centered under the name, whatever their count.
            float pitch = 46f;
            float start = -(StarsShown - 1) * pitch * 0.5f;
            for (int i = 0; i < StarsShown; i++)
                _stars[i].rectTransform.anchoredPosition = new Vector2(start + i * pitch, _stars[i].rectTransform.anchoredPosition.y);
            _time = 0f;
            _root.gameObject.SetActive(true);
            Apply(0f);
        }

        public void Hide()
        {
            _time = -1f;
            if (_root != null)
                _root.gameObject.SetActive(false);
        }

        public void Tick(float deltaTime)
        {
            if (_time < 0f)
                return;
            _time += deltaTime;
            if (_time >= In + Hold + Out)
            {
                Hide();
                return;
            }
            Apply(_time);
        }

        private void Apply(float t)
        {
            float appear = Mathf.Clamp01(t / In);
            float fade = Mathf.Clamp01((In + Hold + Out - t) / Out);
            _group.alpha = Mathf.Min(appear, fade);
            float scale = 1f;
            if (!ReduceMotion)
            {
                // Pop past full size and settle: a happy little jump, like the newborn's.
                float k = 1f - (1f - appear) * (1f - appear);
                scale = appear < 1f ? Mathf.Lerp(0.82f, 1.06f, k) : 1f + 0.06f * Mathf.Exp(-(t - In) * 12f);
            }
            _root.localScale = new Vector3(scale, scale, 1f);
        }

        private Color RarityColor(RarityTier rarity)
        {
            if (_tokens == null || _tokens.rarity == null || (int)rarity >= _tokens.rarity.Length)
                return Color.white;
            return _tokens.rarity[(int)rarity];
        }

        /// <summary>Prefab builder hook (Editor FeedbackUiPipeline).</summary>
        public void SetReferences(RectTransform root, CanvasGroup group, Image glow, Image badge, TMP_Text title, TMP_Text name,
            Image[] stars, UiRaritySet rarity, UiColorTokens tokens)
        {
            _root = root;
            _group = group;
            _glow = glow;
            _badge = badge;
            _title = title;
            _name = name;
            _stars = stars;
            _rarity = rarity;
            _tokens = tokens;
        }
    }
}
