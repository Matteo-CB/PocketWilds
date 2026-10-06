using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>One species card: thumbnail or silhouette, name or "???", variants found, New badge.</summary>
    [DisallowMultipleComponent]
    public sealed class CodexCardView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _thumbnail;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _count;
        [SerializeField] private GameObject _newBadge;
        [SerializeField] private TMP_Text _newLabel;
        [SerializeField] private Button _button;

        private readonly UiNumberText _numbers = new UiNumberText();
        private CodexScreen _owner;
        private bool _listening;

        public int CodexIndex { get; private set; } = -1;
        public bool Discovered { get; private set; }
        public bool ShowsNew => _newBadge.activeSelf;

        public void Init(CodexScreen owner)
        {
            _owner = owner;
            if (_listening)
                return;
            _listening = true;
            _button.onClick.AddListener(OnPressed);
        }

        public void Bind(int codexIndex, in CodexSpeciesEntry entry, Sprite picture, string name, UiColorTokens tokens)
        {
            CodexIndex = codexIndex;
            Discovered = entry.Discovered;
            gameObject.SetActive(true);
            _thumbnail.sprite = picture;
            _thumbnail.enabled = picture != null;
            _name.text = name;
            if (entry.Discovered)
            {
                _background.color = Color.white;
                _thumbnail.color = Color.white;
                _name.color = tokens.textPrimary;
                if (!_count.gameObject.activeSelf)
                    _count.gameObject.SetActive(true);
                _numbers.SetRatio(_count, entry.VariantsFound, entry.VariantsTotal);
            }
            else
            {
                // Undiscovered: the species silhouette, quiet and readable, never a black hole.
                _background.color = tokens.panelSecondary;
                _thumbnail.color = new Color(tokens.textSecondary.r, tokens.textSecondary.g, tokens.textSecondary.b, 0.55f);
                _name.color = tokens.textDisabled;
                if (_count.gameObject.activeSelf)
                    _count.gameObject.SetActive(false);
            }
            if (_newBadge.activeSelf != entry.IsNew)
                _newBadge.SetActive(entry.IsNew);
        }

        public void Clear()
        {
            CodexIndex = -1;
            gameObject.SetActive(false);
        }

        public void SetReferences(Image background, Image thumbnail, TMP_Text name, TMP_Text count, GameObject newBadge,
            TMP_Text newLabel, Button button)
        {
            _background = background;
            _thumbnail = thumbnail;
            _name = name;
            _count = count;
            _newBadge = newBadge;
            _newLabel = newLabel;
            _button = button;
        }

        /// <summary>Localized "New", set when the row comes out of the pool (same string reference: no rebuild).</summary>
        public void SetNewLabel(string text)
        {
            if (!ReferenceEquals(_newLabel.text, text))
                _newLabel.text = text;
        }

        private void OnPressed()
        {
            if (_owner != null && CodexIndex >= 0)
                _owner.OnCardPressed(this);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnPressed);
        }
    }
}
