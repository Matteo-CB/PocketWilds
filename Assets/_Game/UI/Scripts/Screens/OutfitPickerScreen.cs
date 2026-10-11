using System;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_OutfitPicker (T47): a bottom panel over the refuge; the camera keeps the animal in view above it, so the real animal
    /// wears each change at once. Four slots (Head, Neck, Back, Tail; a slot the species hides or with nothing owned shows a calm
    /// locked state and its line), the owned accessories that fit the chosen slot, palette chips for the one worn (meadow,
    /// blossom, frost), « Retirer ». Nothing owned: a short line and « Voir le pack ». Changes go through the refuge
    /// (CosmeticService.Equip / Unequip). Content saved inactive; ticked by the refuge.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OutfitPickerScreen : MonoBehaviour, IOutfitPickerView, IMotionSettings
    {
        public const int Slots = 4;
        public const int Choices = 4;
        public const int Palettes = 3;
        private const float OpenSeconds = 0.22f;
        private const float CloseSeconds = 0.16f;
        private const float Slide = 260f;

        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _safeArea;
        [SerializeField] private RectTransform _card;
        [SerializeField] private Button _close;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _animalName;
        [SerializeField] private Button[] _slotButtons = Array.Empty<Button>();
        [SerializeField] private Image[] _slotBacks = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] _slotLabels = Array.Empty<TMP_Text>();
        [SerializeField] private GameObject[] _slotLocks = Array.Empty<GameObject>();
        [SerializeField] private TMP_Text _line;
        [SerializeField] private Button[] _choiceButtons = Array.Empty<Button>();
        [SerializeField] private Image[] _choiceBacks = Array.Empty<Image>();
        [SerializeField] private Image[] _choiceIcons = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] _choiceLabels = Array.Empty<TMP_Text>();
        [SerializeField] private Button[] _paletteButtons = Array.Empty<Button>();
        [SerializeField] private Image[] _paletteBacks = Array.Empty<Image>();
        [SerializeField] private Image[] _paletteDots = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] _paletteLabels = Array.Empty<TMP_Text>();
        [SerializeField] private Button _remove;
        [SerializeField] private Image _removeBack;
        [SerializeField] private TMP_Text _removeLabel;
        [SerializeField] private Button _toShop;
        [SerializeField] private TMP_Text _toShopLabel;
        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private Sprite _fallbackIcon;
        [SerializeField] private string[] _iconKeys = Array.Empty<string>();
        [SerializeField] private Sprite[] _icons = Array.Empty<Sprite>();
        [SerializeField] private string[] _paletteNames = { "meadow", "blossom", "frost" };
        [SerializeField] private Color[] _paletteColors = { new Color(0.56f, 0.78f, 0.38f), new Color(0.96f, 0.63f, 0.75f), new Color(0.63f, 0.82f, 0.97f) };

        private OutfitPicker _model;
        private ILocalization _text;
        private IOutfitPickerActions _actions;
        private int _slot;
        private float _time = -1f;
        private bool _closing;
        private bool _listening;
        private Vector2 _cardHome;
        private bool _homeKnown;
        private readonly SafeAreaFitter _safeFitter = new SafeAreaFitter();
        private readonly int[] _choiceIndex = new int[Choices];

        public bool IsOpen { get; private set; }
        public bool ReduceMotion { get; set; }
        public int SelectedSlot => _slot;
        public int ChoicesShown { get; private set; }
        public int PalettesShown { get; private set; }
        public bool EmptyShown => _toShop != null && _toShop.gameObject.activeSelf || _model != null && !_model.AnyOwned;
        public bool ShopButtonShown => _toShop != null && _toShop.gameObject.activeSelf;
        public string LineText => _line != null && _line.gameObject.activeSelf ? _line.text : string.Empty;
        public Button SlotButton(int index) => _slotButtons[index];
        public Button ChoiceButton(int index) => _choiceButtons[index];
        public Button PaletteButton(int index) => _paletteButtons[index];
        public Button RemoveButton => _remove;
        public Button ShopButton => _toShop;
        public Button CloseButton => _close;
        public bool SlotLockedShown(int index) => _slotLocks[index].activeSelf;

        public void Open(OutfitPicker model, string animalName, ILocalization text, IOutfitPickerActions actions)
        {
            _model = model;
            _text = text;
            _actions = actions;
            if (!_homeKnown)
            {
                _homeKnown = true;
                _cardHome = _card.anchoredPosition;
            }
            Listen();
            _title.text = Text(OutfitText.Title);
            _animalName.text = animalName ?? string.Empty;
            _removeLabel.text = Text(OutfitText.Remove);
            if (_toShopLabel != null)
                _toShopLabel.text = Text(OutfitText.ToShop);
            for (int i = 0; i < _slotLabels.Length && i < OutfitPicker.Sockets.Length; i++)
                _slotLabels[i].text = Text(OutfitText.SlotKey(OutfitPicker.Sockets[i]));
            _slot = model != null ? model.FirstUsableSlot() : 0;
            IsOpen = true;
            _closing = false;
            _time = 0f;
            _canvas.enabled = true;
            _content.SetActive(true);
            if (_safeArea != null)
                _safeFitter.Fit(_safeArea);
            Bind();
            Apply(0f);
        }

        public void Refresh()
        {
            if (IsOpen)
                Bind();
        }

        public void Close()
        {
            if (!IsOpen || _closing)
                return;
            _closing = true;
            _time = 0f;
        }

        public void Prewarm(ILocalization text)
        {
            if (IsOpen)
                return;
            Open(null, string.Empty, text, null);
            _group.alpha = 0f;
            Canvas.ForceUpdateCanvases();
            IsOpen = false;
            _closing = false;
            _time = -1f;
            _content.SetActive(false);
            _canvas.enabled = false;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsOpen)
                return;
            // Android Back closes the panel (the Input System maps it to Escape).
            if (!_closing && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
            _time += unscaledDeltaTime;
            if (_closing)
            {
                float k = Mathf.Clamp01(_time / CloseSeconds);
                Apply(1f - k);
                if (k >= 1f)
                    Finish();
                return;
            }
            Apply(Mathf.Clamp01(_time / OpenSeconds));
        }

        /// <summary>Taps a slot (tests and buttons): its accessories and palettes show; a locked slot shows its calm line.</summary>
        public void SelectSlot(int index)
        {
            if (!IsOpen || _closing || index < 0 || index >= Slots)
                return;
            _slot = index;
            Bind();
        }

        // ---------------- Binding (on open and after each change, never per frame) ----------------

        private void Bind()
        {
            bool any = _model != null && _model.AnyOwned;
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                var slot = _model != null ? _model.GetSlot(i) : default;
                bool usable = _model != null && slot.Usable;
                bool selected = i == _slot;
                _slotLocks[i].SetActive(!usable);
                if (_tokens != null)
                {
                    _slotBacks[i].color = selected ? (usable ? _tokens.accentPrimary : _tokens.accentIdle) : _tokens.panelSecondary;
                    _slotLabels[i].color = usable || selected ? _tokens.textPrimary : _tokens.textDisabled;
                }
            }

            var current = _model != null ? _model.GetSlot(_slot) : default;
            string line = null;
            if (_model == null || !any)
                line = Text(OutfitText.Empty);
            else if (current.State == OutfitSlotState.Hidden)
                line = Text(OutfitText.SlotHidden);
            else if (current.State == OutfitSlotState.Locked)
                line = Text(OutfitText.SlotLocked);
            _line.gameObject.SetActive(line != null);
            if (line != null)
                _line.text = line;
            if (_toShop != null)
                _toShop.gameObject.SetActive(_model != null && !any && _model.ShopAvailable);

            ChoicesShown = 0;
            int count = _model != null && current.Usable ? Mathf.Min(_model.ChoiceCount(_slot), _choiceButtons.Length) : 0;
            int wornPalettes = 0;
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                bool on = i < count;
                if (_choiceButtons[i].gameObject.activeSelf != on)
                    _choiceButtons[i].gameObject.SetActive(on);
                if (!on)
                    continue;
                var choice = _model.GetChoice(_slot, i);
                _choiceIndex[i] = i;
                _choiceIcons[i].sprite = IconOf(choice.IconKey);
                _choiceLabels[i].text = Text(choice.NameKey);
                if (_tokens != null)
                    _choiceBacks[i].color = choice.Worn ? _tokens.accentPrimary : _tokens.panelSecondary;
                if (choice.Worn)
                    wornPalettes = choice.Palettes != null ? choice.Palettes.Length : 0;
                ChoicesShown++;
            }

            // Palettes of the worn accessory, in its own order (meadow, blossom, frost).
            PalettesShown = 0;
            var worn = current.State == OutfitSlotState.Worn ? WornChoice() : default;
            for (int i = 0; i < _paletteButtons.Length; i++)
            {
                bool on = i < wornPalettes && worn.Palettes != null;
                if (_paletteButtons[i].gameObject.activeSelf != on)
                    _paletteButtons[i].gameObject.SetActive(on);
                if (!on)
                    continue;
                string palette = worn.Palettes[i];
                _paletteLabels[i].text = Text(OutfitText.PaletteKey(palette));
                _paletteDots[i].color = PaletteColor(palette);
                if (_tokens != null)
                    _paletteBacks[i].color = i == current.Palette ? _tokens.accentPrimary : _tokens.panelSecondary;
                PalettesShown++;
            }

            bool canRemove = current.State == OutfitSlotState.Worn;
            _remove.gameObject.SetActive(_model == null || any);
            _remove.interactable = canRemove;
            if (_tokens != null)
            {
                _removeBack.color = canRemove ? _tokens.panelSecondary : _tokens.accentIdle;
                _removeLabel.color = canRemove ? _tokens.textPrimary : _tokens.textDisabled;
            }
        }

        private OutfitChoice WornChoice()
        {
            int count = _model.ChoiceCount(_slot);
            for (int i = 0; i < count; i++)
            {
                var choice = _model.GetChoice(_slot, i);
                if (choice.Worn)
                    return choice;
            }
            return default;
        }

        private Color PaletteColor(string palette)
        {
            for (int i = 0; i < _paletteNames.Length && i < _paletteColors.Length; i++)
                if (_paletteNames[i] == palette)
                    return _paletteColors[i];
            return Color.white;
        }

        private Sprite IconOf(string key)
        {
            if (!string.IsNullOrEmpty(key))
                for (int i = 0; i < _iconKeys.Length && i < _icons.Length; i++)
                    if (_iconKeys[i] == key && _icons[i] != null)
                        return _icons[i];
            return _fallbackIcon;
        }

        // ---------------- Buttons ----------------

        private void Listen()
        {
            if (_listening)
                return;
            _listening = true;
            _close.onClick.AddListener(Close);
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                int index = i;
                _slotButtons[i].onClick.AddListener(() => SelectSlot(index));
            }
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                int index = i;
                _choiceButtons[i].onClick.AddListener(() => OnChoice(index));
            }
            for (int i = 0; i < _paletteButtons.Length; i++)
            {
                int index = i;
                _paletteButtons[i].onClick.AddListener(() => OnPalette(index));
            }
            _remove.onClick.AddListener(OnRemove);
            if (_toShop != null)
                _toShop.onClick.AddListener(OnToShop);
        }

        private void OnChoice(int index)
        {
            if (!IsOpen || _closing || _model == null || index >= ChoicesShown)
                return;
            var choice = _model.GetChoice(_slot, _choiceIndex[index]);
            if (choice.Worn)
                return;
            // A new accessory keeps the palette worn before when it has it (same order for every accessory).
            var slot = _model.GetSlot(_slot);
            int palette = slot.State == OutfitSlotState.Worn && choice.Palettes != null && slot.Palette < choice.Palettes.Length ? slot.Palette : 0;
            _actions?.EquipAccessory(_model.Animal, choice.Accessory, OutfitPicker.Sockets[_slot], palette);
        }

        private void OnPalette(int index)
        {
            if (!IsOpen || _closing || _model == null || index >= PalettesShown)
                return;
            var slot = _model.GetSlot(_slot);
            if (slot.State != OutfitSlotState.Worn || slot.Palette == index)
                return;
            _actions?.EquipAccessory(_model.Animal, slot.Worn, OutfitPicker.Sockets[_slot], index);
        }

        private void OnRemove()
        {
            if (!IsOpen || _closing || _model == null || _model.GetSlot(_slot).State != OutfitSlotState.Worn)
                return;
            _actions?.RemoveAccessory(_model.Animal, OutfitPicker.Sockets[_slot]);
        }

        private void OnToShop()
        {
            if (IsOpen && !_closing)
                _actions?.OpenPackOffer();
        }

        private void Apply(float k)
        {
            float eased = 1f - (1f - k) * (1f - k);
            _group.alpha = eased;
            float offset = ReduceMotion ? 0f : (1f - eased) * -Slide;
            _card.anchoredPosition = _cardHome + new Vector2(0f, offset);
        }

        private void Finish()
        {
            IsOpen = false;
            _closing = false;
            _time = -1f;
            _content.SetActive(false);
            _canvas.enabled = false;
            var actions = _actions;
            _actions = null;
            actions?.OutfitClosed();
        }

        private string Text(string key) => _text != null ? _text.Get(key) : key;

        private void OnDestroy()
        {
            if (!_listening)
                return;
            _close.onClick.RemoveAllListeners();
            foreach (var button in _slotButtons)
                button.onClick.RemoveAllListeners();
            foreach (var button in _choiceButtons)
                button.onClick.RemoveAllListeners();
            foreach (var button in _paletteButtons)
                button.onClick.RemoveAllListeners();
            _remove.onClick.RemoveAllListeners();
            if (_toShop != null)
                _toShop.onClick.RemoveAllListeners();
        }

        /// <summary>Prefab builder hook (Editor OutfitUiPipeline).</summary>
        public void SetReferences(Canvas canvas, GameObject content, CanvasGroup group, RectTransform safeArea, RectTransform card, Button close,
            TMP_Text title, TMP_Text animalName, Button[] slotButtons, Image[] slotBacks, TMP_Text[] slotLabels, GameObject[] slotLocks, TMP_Text line,
            Button[] choiceButtons, Image[] choiceBacks, Image[] choiceIcons, TMP_Text[] choiceLabels, Button[] paletteButtons, Image[] paletteBacks,
            Image[] paletteDots, TMP_Text[] paletteLabels, Button remove, Image removeBack, TMP_Text removeLabel, Button toShop, TMP_Text toShopLabel,
            UiColorTokens tokens, Sprite fallbackIcon, string[] iconKeys, Sprite[] icons)
        {
            _canvas = canvas;
            _content = content;
            _group = group;
            _safeArea = safeArea;
            _card = card;
            _close = close;
            _title = title;
            _animalName = animalName;
            _slotButtons = slotButtons;
            _slotBacks = slotBacks;
            _slotLabels = slotLabels;
            _slotLocks = slotLocks;
            _line = line;
            _choiceButtons = choiceButtons;
            _choiceBacks = choiceBacks;
            _choiceIcons = choiceIcons;
            _choiceLabels = choiceLabels;
            _paletteButtons = paletteButtons;
            _paletteBacks = paletteBacks;
            _paletteDots = paletteDots;
            _paletteLabels = paletteLabels;
            _remove = remove;
            _removeBack = removeBack;
            _removeLabel = removeLabel;
            _toShop = toShop;
            _toShopLabel = toShopLabel;
            _tokens = tokens;
            _fallbackIcon = fallbackIcon;
            _iconKeys = iconKeys;
            _icons = icons;
        }
    }
}
