using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_AnimalQuickSheet (T34A, doc 06 SCREEN 005): a card that rises from the bottom while the refuge stays in
    /// view above it. Portrait in its rarity frame, name, species and variant, rarity (badge motif, stars and word),
    /// trait badges, bond, favorite heart, Details (the species page of the Codex). Closes on the cross, the scrim or
    /// Back. Content saved inactive; nothing runs while closed. Ticked by the scene controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalSheetScreen : MonoBehaviour, IAnimalSheetView, IMotionSettings
    {
        public const int MaxTraits = 6;
        private const float OpenSeconds = 0.22f;
        private const float CloseSeconds = 0.16f;
        private const float Slide = 220f;

        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _safeArea;
        [SerializeField] private RectTransform _card;
        [SerializeField] private Button _scrim;
        [SerializeField] private Button _close;
        [SerializeField] private Image _portrait;
        [SerializeField] private Image _frame;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _species;
        [SerializeField] private Image _rarityBadge;
        [SerializeField] private TMP_Text _rarityLabel;
        [SerializeField] private Image[] _stars;
        [SerializeField] private Image[] _traits;
        [SerializeField] private TMP_Text _bondLabel;
        [SerializeField] private RectTransform _bondBar;
        [SerializeField] private RectTransform _bondFill;
        [SerializeField] private TMP_Text _bondLevel;
        [SerializeField] private Button _favorite;
        [SerializeField] private Image _favoriteIcon;
        [SerializeField] private Button _details;
        [SerializeField] private TMP_Text _detailsLabel;
        [SerializeField] private UiRaritySet _rarity;
        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private CodexVisualCatalog _visuals;

        private IAnimalSheetActions _actions;
        private AnimalSheetData _data;
        private float _time = -1f;
        private bool _closing;
        private Vector2 _cardHome;
        private bool _homeKnown;
        private readonly UiNumberText _level = new UiNumberText();
        private readonly SafeAreaFitter _safeFitter = new SafeAreaFitter();

        public bool IsOpen { get; private set; }
        public bool ReduceMotion { get; set; }
        public uint InstanceShown => _data.InstanceId;
        public bool FavoriteShown => _data.Favorite;
        public string NameText => _name != null ? _name.text : string.Empty;
        public string SpeciesText => _species != null ? _species.text : string.Empty;
        public string RarityText => _rarityLabel != null ? _rarityLabel.text : string.Empty;
        public int TraitsShown { get; private set; }
        public int StarsShown { get; private set; }
        public Sprite FrameShown => _frame != null ? _frame.sprite : null;
        public Button FavoriteButton => _favorite;
        public Button DetailsButton => _details;
        public Button CloseButton => _close;

        public void Open(in AnimalSheetData data, ILocalization localization, IAnimalSheetActions actions)
        {
            _actions = actions;
            if (!_homeKnown)
            {
                _cardHome = _card.anchoredPosition;
                _homeKnown = true;
                _scrim.onClick.AddListener(Close);
                _close.onClick.AddListener(Close);
                _favorite.onClick.AddListener(OnFavorite);
                _details.onClick.AddListener(OnDetails);
            }
            _bondLabel.text = localization.Get(FeedbackText.SheetBond);
            _detailsLabel.text = localization.Get(FeedbackText.SheetDetailsButton);
            _rarityLabel.text = localization.Get(FeedbackText.RarityKey(data.Rarity));
            Bind(data);
            IsOpen = true;
            _closing = false;
            _time = 0f;
            _canvas.enabled = true;
            _content.SetActive(true);
            if (_safeArea != null)
                _safeFitter.Fit(_safeArea);
            Apply(0f);
        }

        public void Prewarm(ILocalization localization)
        {
            if (IsOpen)
                return;
            var sample = new AnimalSheetData(0u, localization.Get(FeedbackText.SheetBond), localization.Get(FeedbackText.RarityLegendary),
                localization.Get(FeedbackText.RarityFabulous), default, default, RarityTier.Fabulous, true, 0.5f, 1,
                (uint)(AnimalTrait.Tiny | AnimalTrait.Shiny | AnimalTrait.Golden));
            Open(sample, localization, null);
            _group.alpha = 0f;
            Canvas.ForceUpdateCanvases();
            IsOpen = false;
            _closing = false;
            _time = -1f;
            _content.SetActive(false);
            _canvas.enabled = false;
        }

        public void Refresh(in AnimalSheetData data)
        {
            if (IsOpen)
                Bind(data);
        }

        public void Close()
        {
            if (!IsOpen || _closing)
                return;
            _closing = true;
            _time = 0f;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsOpen)
                return;
            // Android Back closes the sheet (the Input System maps it to Escape).
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

        private void Bind(in AnimalSheetData data)
        {
            _data = data;
            _name.text = data.Name;
            _species.text = string.IsNullOrEmpty(data.Variant) ? data.Species : data.Species + " · " + data.Variant;
            var color = RarityColor(data.Rarity);
            _frame.sprite = _rarity != null ? _rarity.Frame(data.Rarity) : null;
            _frame.color = color;
            _rarityBadge.sprite = _rarity != null ? _rarity.Badge(data.Rarity) : null;
            _rarityBadge.color = color;
            StarsShown = Mathf.Min(_stars.Length, (int)data.Rarity + 1);
            for (int i = 0; i < _stars.Length; i++)
            {
                bool on = i < StarsShown;
                if (_stars[i].gameObject.activeSelf != on)
                    _stars[i].gameObject.SetActive(on);
                _stars[i].color = color;
            }
            if (_visuals != null && _visuals.TryGetVariant(data.VariantId, out var thumbnail) && thumbnail != null)
            {
                _portrait.sprite = thumbnail;
                _portrait.enabled = true;
            }
            else
            {
                _portrait.enabled = false;
            }

            TraitsShown = 0;
            var traits = (int)data.Traits;
            for (int bit = 0; bit < 8 && TraitsShown < _traits.Length; bit++)
            {
                if ((traits & (1 << bit)) == 0)
                    continue;
                var sprite = _rarity != null ? _rarity.Trait((AnimalTrait)(1 << bit)) : null;
                if (sprite == null)
                    continue;
                _traits[TraitsShown].sprite = sprite;
                TraitsShown++;
            }
            for (int i = 0; i < _traits.Length; i++)
            {
                bool on = i < TraitsShown;
                if (_traits[i].gameObject.activeSelf != on)
                    _traits[i].gameObject.SetActive(on);
            }

            float width = _bondBar.rect.width;
            float height = _bondBar.rect.height;
            float fraction = Mathf.Clamp01(data.Bond);
            _bondFill.gameObject.SetActive(fraction > 0.001f);
            _bondFill.sizeDelta = new Vector2(Mathf.Max(height, width * fraction), _bondFill.sizeDelta.y);
            _level.SetCount(_bondLevel, data.BondLevel);

            _favoriteIcon.sprite = _rarity != null ? (data.Favorite ? _rarity.favoriteOn : _rarity.favoriteOff) : null;
            _favoriteIcon.color = data.Favorite ? Color.white : (_tokens != null ? _tokens.textSecondary : Color.grey);
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
            actions?.AnimalSheetClosed();
        }

        private void OnFavorite()
        {
            if (!_closing)
                _actions?.ToggleFavorite(_data.InstanceId);
        }

        private void OnDetails()
        {
            if (_closing)
                return;
            var actions = _actions;
            var species = _data.SpeciesId;
            // Closes at once (no fade): the Codex opens over the world in the same frame.
            _closing = true;
            Finish();
            actions?.OpenSpeciesDetails(species);
        }

        private Color RarityColor(RarityTier rarity)
        {
            if (_tokens == null || _tokens.rarity == null || (int)rarity >= _tokens.rarity.Length)
                return Color.white;
            return _tokens.rarity[(int)rarity];
        }

        /// <summary>Prefab builder hook (Editor FeedbackUiPipeline).</summary>
        public void SetReferences(Canvas canvas, GameObject content, CanvasGroup group, RectTransform safeArea, RectTransform card, Button scrim, Button close,
            Image portrait, Image frame, TMP_Text name, TMP_Text species, Image rarityBadge, TMP_Text rarityLabel, Image[] stars,
            Image[] traits, TMP_Text bondLabel, RectTransform bondBar, RectTransform bondFill, TMP_Text bondLevel, Button favorite,
            Image favoriteIcon, Button details, TMP_Text detailsLabel, UiRaritySet rarity, UiColorTokens tokens, CodexVisualCatalog visuals)
        {
            _canvas = canvas;
            _content = content;
            _group = group;
            _safeArea = safeArea;
            _card = card;
            _scrim = scrim;
            _close = close;
            _portrait = portrait;
            _frame = frame;
            _name = name;
            _species = species;
            _rarityBadge = rarityBadge;
            _rarityLabel = rarityLabel;
            _stars = stars;
            _traits = traits;
            _bondLabel = bondLabel;
            _bondBar = bondBar;
            _bondFill = bondFill;
            _bondLevel = bondLevel;
            _favorite = favorite;
            _favoriteIcon = favoriteIcon;
            _details = details;
            _detailsLabel = detailsLabel;
            _rarity = rarity;
            _tokens = tokens;
            _visuals = visuals;
        }

        private void OnDestroy()
        {
            if (!_homeKnown)
                return;
            _scrim.onClick.RemoveListener(Close);
            _close.onClick.RemoveListener(Close);
            _favorite.onClick.RemoveListener(OnFavorite);
            _details.onClick.RemoveListener(OnDetails);
        }
    }
}
