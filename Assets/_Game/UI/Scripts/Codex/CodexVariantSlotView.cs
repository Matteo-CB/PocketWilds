using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>Variant slot of the species sheet: picture or silhouette, name or "???", rarity stars.</summary>
    [DisallowMultipleComponent]
    public sealed class CodexVariantSlotView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _thumbnail;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private Image[] _stars;
        [SerializeField] private GameObject _newBadge;
        [SerializeField] private TMP_Text _newLabel;

        public const float StarPitch = 38f;

        public bool Discovered { get; private set; }
        public int Stars { get; private set; }

        public void Bind(in CodexVariantEntry entry, Sprite picture, string name, UiColorTokens tokens)
        {
            gameObject.SetActive(true);
            Discovered = entry.Discovered;
            _thumbnail.sprite = picture;
            _thumbnail.enabled = picture != null;
            _name.text = name;
            _background.color = entry.Discovered ? Color.white : tokens.panelSecondary;
            _thumbnail.color = entry.Discovered
                ? Color.white
                : new Color(tokens.textSecondary.r, tokens.textSecondary.g, tokens.textSecondary.b, 0.5f);
            _name.color = entry.Discovered ? tokens.textPrimary : tokens.textDisabled;

            // Rarity reads by shape (star count) and color, never by color alone.
            int stars = Mathf.Clamp((int)entry.Rarity + 1, 1, _stars.Length);
            int tier = Mathf.Clamp((int)entry.Rarity, 0, tokens.rarity.Length - 1);
            var color = tokens.rarity[tier];
            for (int i = 0; i < _stars.Length; i++)
            {
                bool on = i < stars;
                if (_stars[i].gameObject.activeSelf != on)
                    _stars[i].gameObject.SetActive(on);
                if (!on)
                    continue;
                _stars[i].color = entry.Discovered ? color : new Color(color.r, color.g, color.b, 0.45f);
                // Centered row without a layout group: placed once per bind.
                var rect = _stars[i].rectTransform;
                rect.anchoredPosition = new Vector2((i - (stars - 1) * 0.5f) * StarPitch, rect.anchoredPosition.y);
            }
            Stars = stars;
            if (_newBadge.activeSelf != entry.IsNew)
                _newBadge.SetActive(entry.IsNew);
        }

        public void Clear() => gameObject.SetActive(false);

        public void SetReferences(Image background, Image thumbnail, TMP_Text name, Image[] stars, GameObject newBadge, TMP_Text newLabel)
        {
            _background = background;
            _thumbnail = thumbnail;
            _name = name;
            _stars = stars;
            _newBadge = newBadge;
            _newLabel = newLabel;
        }

        public void SetNewLabel(string text)
        {
            if (!ReferenceEquals(_newLabel.text, text))
                _newLabel.text = text;
        }
    }
}
