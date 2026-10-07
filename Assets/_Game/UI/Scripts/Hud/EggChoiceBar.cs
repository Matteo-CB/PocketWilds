using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T35B egg choice around the hatch button (doc 02 Œufs, sections 2.2, 3 and 5): small egg pills under the button
    /// (shell and spots of each offered egg, a ring on the chosen one, the stock of a Discovery egg), the chosen egg's
    /// name for a moment after a change, the × 1 / × 3 / × 10 pill once × 3 is unlocked, and the Focus egg's three
    /// targets beside the button. The hatch button's egg takes the chosen egg's colors: slot 0 is always the first biome's
    /// Basic egg, drawn by ICO_Hatch_Egg; any other egg shows the tinted UI_Egg_Shell and UI_Egg_Spots over it. Built by
    /// EggUiPipeline, ticked by the HUD, nothing allocates after the first show.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EggChoiceBar : MonoBehaviour
    {
        private const float NameSeconds = 1.6f;
        private const float NameFade = 0.3f;
        private const float SelectPop = 0.25f;

        [SerializeField] private RectTransform _pillsRoot;
        [SerializeField] private float _pillPitch = 114f;
        [SerializeField] private Button[] _pills = System.Array.Empty<Button>();
        [SerializeField] private Image[] _shells = System.Array.Empty<Image>();
        [SerializeField] private Image[] _spots = System.Array.Empty<Image>();
        [SerializeField] private Image[] _rings = System.Array.Empty<Image>();
        [SerializeField] private TMP_Text[] _counts = System.Array.Empty<TMP_Text>();
        [SerializeField] private RectTransform _nameRoot;
        [SerializeField] private CanvasGroup _nameGroup;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private RectTransform _quantityRoot;
        [SerializeField] private Button _quantityButton;
        [SerializeField] private TMP_Text _quantityLabel;
        [SerializeField] private RectTransform _targetsRoot;
        [SerializeField] private Image[] _portraits = System.Array.Empty<Image>();
        [SerializeField] private CodexVisualCatalog _catalog;
        [SerializeField] private Image _buttonEgg;
        [SerializeField] private Image _buttonShell;
        [SerializeField] private Image _buttonSpots;

        private IEggChoiceActions _actions;
        private int _count = -1;
        private int _selected = -1;
        private float _nameTime = -1f;
        private float _popTime = -1f;
        private int _quantityShown = -1;
        private readonly int[] _stocksShown = { int.MinValue, int.MinValue, int.MinValue, int.MinValue };
        private readonly UiNumberText[] _countTexts = { new UiNumberText(), new UiNumberText(), new UiNumberText(), new UiNumberText() };
        private bool _bound;
        private float _buttonAlpha = 1f;
        private Color32 _buttonShellColor = new Color32(255, 255, 255, 255);
        private Color32 _buttonSpotsColor = new Color32(255, 255, 255, 255);
        private bool _buttonTinted;

        public bool ReduceMotion { get; set; }
        public int SlotsShown => _count;
        public int SelectedShown => _selected;
        public bool QuantityVisible => _quantityRoot != null && _quantityRoot.gameObject.activeSelf;
        public string QuantityText => _quantityLabel != null ? _quantityLabel.text : null;
        public bool TargetsVisible => _targetsRoot != null && _targetsRoot.gameObject.activeSelf;
        public string NameText => _nameLabel != null ? _nameLabel.text : null;
        public Button QuantityButton => _quantityButton;
        public Button Pill(int index) => _pills[index];
        public bool ButtonTinted => _buttonTinted;

        public void Bind(IEggChoiceActions actions)
        {
            _actions = actions;
            if (_bound)
                return;
            _bound = true;
            for (int i = 0; i < _pills.Length; i++)
            {
                int index = i;
                _pills[i].onClick.AddListener(() => _actions?.SelectEgg(index));
            }
            if (_quantityButton != null)
                _quantityButton.onClick.AddListener(() => _actions?.CycleHatchQuantity());
            if (_nameRoot != null)
                _nameRoot.gameObject.SetActive(false);
        }

        /// <summary>The swipe on the hatch button (EggSwipe) lands here.</summary>
        public void Step(int direction) => _actions?.StepEgg(direction);

        public void SetSlots(EggSlot[] slots, int count, int selected)
        {
            // A single egg needs no choice: the row stays hidden.
            bool visible = count > 1;
            if (_pillsRoot.gameObject.activeSelf != visible)
                _pillsRoot.gameObject.SetActive(visible);
            for (int i = 0; i < _pills.Length; i++)
            {
                bool shown = i < count;
                if (_pills[i].gameObject.activeSelf != shown)
                    _pills[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                _shells[i].color = slots[i].Shell;
                _spots[i].color = slots[i].Spots;
                // The stock badge is the label's parent (a warm dot): both show only for a Discovery egg.
                bool counted = slots[i].Stock >= 0;
                var badge = _counts[i].transform.parent.gameObject;
                if (badge.activeSelf != counted)
                    badge.SetActive(counted);
                if (counted && _stocksShown[i] != slots[i].Stock)
                {
                    _stocksShown[i] = slots[i].Stock;
                    _countTexts[i].SetCount(_counts[i], slots[i].Stock);
                }
                bool chosen = i == selected;
                if (_rings[i].enabled != chosen)
                    _rings[i].enabled = chosen;
            }
            if (selected != _selected && _count >= 0)
                _popTime = 0f;
            if (count != _count)
            {
                // Centered by hand (no layout group on the HUD): two, three or four pills stay under the button.
                for (int i = 0; i < count && i < _pills.Length; i++)
                {
                    var rect = (RectTransform)_pills[i].transform;
                    rect.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * _pillPitch, 0f);
                }
            }
            _count = count;
            _selected = selected;
            ApplyScales(1f);

            bool tinted = selected > 0 && selected < count;
            if (tinted)
            {
                _buttonShellColor = slots[selected].Shell;
                _buttonSpotsColor = slots[selected].Spots;
            }
            if (tinted != _buttonTinted || tinted)
            {
                _buttonTinted = tinted;
                ApplyButtonEgg();
            }
        }

        /// <summary>The HUD dims the button's egg when the price is out of reach; the tinted egg follows.</summary>
        public void SetButtonAlpha(float alpha)
        {
            if (_buttonAlpha == alpha)
                return;
            _buttonAlpha = alpha;
            ApplyButtonEgg();
        }

        private void ApplyButtonEgg()
        {
            if (_buttonEgg == null || _buttonShell == null || _buttonSpots == null)
                return;
            _buttonEgg.enabled = !_buttonTinted;
            if (_buttonShell.gameObject.activeSelf != _buttonTinted)
                _buttonShell.gameObject.SetActive(_buttonTinted);
            if (_buttonSpots.gameObject.activeSelf != _buttonTinted)
                _buttonSpots.gameObject.SetActive(_buttonTinted);
            if (!_buttonTinted)
                return;
            Color shell = _buttonShellColor;
            Color spots = _buttonSpotsColor;
            shell.a = _buttonAlpha;
            spots.a = _buttonAlpha;
            _buttonShell.color = shell;
            _buttonSpots.color = spots;
        }

        public void ShowName(string text)
        {
            if (_nameRoot == null)
                return;
            _nameLabel.text = text;
            _nameRoot.gameObject.SetActive(true);
            _nameTime = 0f;
            _nameGroup.alpha = 0f;
        }

        /// <summary>The mastery toast takes the space above the button: the name gives way.</summary>
        public void HideName()
        {
            if (_nameTime < 0f)
                return;
            _nameTime = -1f;
            _nameRoot.gameObject.SetActive(false);
        }

        public void SetQuantity(int quantity, bool visible)
        {
            if (_quantityRoot == null)
                return;
            if (_quantityRoot.gameObject.activeSelf != visible)
                _quantityRoot.gameObject.SetActive(visible);
            if (visible && quantity != _quantityShown)
            {
                _quantityShown = quantity;
                _quantityLabel.text = quantity >= 10 ? "× 10" : quantity >= 3 ? "× 3" : "× 1";
            }
        }

        public void SetTargets(PortraitSlot[] portraits, int count)
        {
            if (_targetsRoot == null)
                return;
            bool visible = count > 0;
            if (_targetsRoot.gameObject.activeSelf != visible)
                _targetsRoot.gameObject.SetActive(visible);
            for (int i = 0; i < _portraits.Length; i++)
            {
                bool shown = i < count;
                if (_portraits[i].transform.parent.gameObject.activeSelf != shown)
                    _portraits[i].transform.parent.gameObject.SetActive(shown);
                if (!shown || _catalog == null)
                    continue;
                Sprite sprite = null;
                bool known = portraits[i].Variant.IsValid && _catalog.TryGetVariant(portraits[i].Variant, out sprite);
                if (!known)
                    _catalog.TryGetSilhouette(portraits[i].Species, out sprite);
                _portraits[i].sprite = sprite;
                _portraits[i].enabled = sprite != null;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_nameTime >= 0f)
            {
                _nameTime += deltaTime;
                float alpha = Mathf.Min(Mathf.Clamp01(_nameTime / NameFade), Mathf.Clamp01((NameSeconds - _nameTime) / NameFade));
                _nameGroup.alpha = alpha;
                if (_nameTime >= NameSeconds)
                {
                    _nameTime = -1f;
                    _nameRoot.gameObject.SetActive(false);
                }
            }
            if (_popTime >= 0f)
            {
                _popTime += deltaTime;
                float t = Mathf.Clamp01(_popTime / SelectPop);
                ApplyScales(ReduceMotion ? 1f : 1f + 0.18f * Mathf.Sin(t * Mathf.PI));
                if (t >= 1f)
                    _popTime = -1f;
            }
        }

        private void ApplyScales(float pop)
        {
            for (int i = 0; i < _pills.Length; i++)
            {
                float scale = i == _selected ? 1.15f * pop : 0.9f;
                var transform = _pills[i].transform;
                if (transform.localScale.x != scale)
                    transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        public bool Contains(Vector2 screenPosition) =>
            (_pillsRoot != null && _pillsRoot.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(_pillsRoot, screenPosition, null))
            || (_quantityRoot != null && _quantityRoot.gameObject.activeSelf
                && RectTransformUtility.RectangleContainsScreenPoint(_quantityRoot, screenPosition, null));

        /// <summary>Prefab builder hook (Editor EggUiPipeline).</summary>
        public void SetReferences(RectTransform pillsRoot, float pillPitch, Button[] pills, Image[] shells, Image[] spots, Image[] rings, TMP_Text[] counts,
            RectTransform nameRoot, CanvasGroup nameGroup, TMP_Text nameLabel, RectTransform quantityRoot, Button quantityButton,
            TMP_Text quantityLabel, RectTransform targetsRoot, Image[] portraits, CodexVisualCatalog catalog, Image buttonEgg,
            Image buttonShell, Image buttonSpots)
        {
            _pillsRoot = pillsRoot;
            _pillPitch = pillPitch;
            _pills = pills;
            _shells = shells;
            _spots = spots;
            _rings = rings;
            _counts = counts;
            _nameRoot = nameRoot;
            _nameGroup = nameGroup;
            _nameLabel = nameLabel;
            _quantityRoot = quantityRoot;
            _quantityButton = quantityButton;
            _quantityLabel = quantityLabel;
            _targetsRoot = targetsRoot;
            _portraits = portraits;
            _catalog = catalog;
            _buttonEgg = buttonEgg;
            _buttonShell = buttonShell;
            _buttonSpots = buttonSpots;
        }
    }
}
