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
    /// T36 (doc 01 section 10.1): the « Friandise » button under the bond bar, with the treat this animal would eat, how
    /// many are owned and a small heart when its family prefers it; a treat given sends hearts up to the bar, which grows.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalSheetScreen : MonoBehaviour, IAnimalSheetView, IAnimalSheetTreatView, IMotionSettings
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

        [Header("T36 treat (doc 01 section 10.1)")]
        [SerializeField] private RectTransform _treatRoot;
        [SerializeField] private Button _treatButton;
        [SerializeField] private Image _treatBackground;
        [SerializeField] private Image _treatIcon;
        [SerializeField] private TMP_Text _treatLabel;
        [SerializeField] private TMP_Text _treatCount;
        [SerializeField] private GameObject _treatPreferred;
        [SerializeField] private Sprite _treatSprite;
        [SerializeField] private uint[] _treatItemIds = System.Array.Empty<uint>();
        [SerializeField] private Sprite[] _treatItemIcons = System.Array.Empty<Sprite>();
        [SerializeField] private RectTransform[] _hearts = System.Array.Empty<RectTransform>();
        [SerializeField] private Image[] _heartImages = System.Array.Empty<Image>();

        private IAnimalSheetActions _actions;
        private AnimalSheetData _data;
        private float _time = -1f;
        private bool _closing;
        private Vector2 _cardHome;
        private bool _homeKnown;
        private readonly UiNumberText _level = new UiNumberText();
        private readonly SafeAreaFitter _safeFitter = new SafeAreaFitter();
        private readonly UiNumberText _treatNumber = new UiNumberText();
        private bool _treatShown;
        private int _treatOwnedShown = -1;
        private int _treatLook = -1;
        private float _bondNow;
        private float _bondFrom;
        private float _bondTime = -1f;
        private float _heartTime = -1f;
        private float _press = -1f;
        private const float BondGrowSeconds = 0.55f;
        private const float HeartSeconds = 0.75f;
        private const float HeartStagger = 0.09f;
        private const float PressSeconds = 0.22f;

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
        public Button TreatButton => _treatButton;
        public bool TreatShown => _treatShown;
        public bool TreatEnabled => _treatButton != null && _treatButton.interactable;
        public bool TreatPreferredShown => _treatPreferred != null && _treatPreferred.activeSelf;
        public string TreatCountText => _treatCount != null ? _treatCount.text : string.Empty;
        public Sprite TreatIconShown => _treatIcon != null ? _treatIcon.sprite : null;
        public bool TreatAnimating => _bondTime >= 0f || _heartTime >= 0f;

        /// <summary>The bond bar's fill as drawn (0 to 1), animations included.</summary>
        public float BondFillShown { get; private set; }

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
                if (_treatButton != null)
                    _treatButton.onClick.AddListener(OnTreat);
            }
            _bondLabel.text = localization.Get(FeedbackText.SheetBond);
            if (_treatLabel != null)
                _treatLabel.text = localization.Get(DailyVisitText.SheetTreat);
            // The treat row waits for SetTreat (sheets without treats keep the T34A look).
            ShowTreat(false);
            StopTreatMotion();
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
            SetTreat(new SheetTreatData { Available = true, Owned = 12, Preferred = true });
            _group.alpha = 0f;
            Canvas.ForceUpdateCanvases();
            ShowTreat(false);
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
            TickTreat(unscaledDeltaTime);
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

            _bondNow = Mathf.Clamp01(data.Bond);
            if (_bondTime < 0f)
                SetBondFill(_bondNow);
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
            StopTreatMotion();
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

        // ---------------- T36 treat (IAnimalSheetTreatView) ----------------

        /// <summary>
        /// The « Friandise » button (doc 01 section 10.1): the treat's icon, « ×n » owned, a small heart when the animal's
        /// family prefers it (+50 %). None owned: the button stays, soft and untouchable, so the player learns it exists.
        /// </summary>
        public void SetTreat(in SheetTreatData data)
        {
            if (_treatRoot == null)
                return;
            ShowTreat(true);
            var sprite = TreatSprite(data.Treat);
            if (_treatIcon.sprite != sprite)
                _treatIcon.sprite = sprite;
            int owned = Mathf.Max(0, data.Owned);
            if (owned != _treatOwnedShown)
            {
                _treatOwnedShown = owned;
                _treatNumber.SetCount(_treatCount, owned, '×');
            }
            bool preferred = data.Preferred && owned > 0;
            if (_treatPreferred != null && _treatPreferred.activeSelf != preferred)
                _treatPreferred.SetActive(preferred);
            int look = data.Available && owned > 0 ? 1 : 0;
            if (look == _treatLook)
                return;
            _treatLook = look;
            _treatButton.interactable = look == 1;
            if (_tokens != null)
            {
                _treatBackground.color = look == 1 ? _tokens.panelSecondary : _tokens.accentIdle;
                _treatLabel.color = look == 1 ? _tokens.textPrimary : _tokens.textDisabled;
                _treatCount.color = look == 1 ? _tokens.textPrimary : _tokens.textDisabled;
            }
            _treatIcon.color = look == 1 ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        }

        /// <summary>
        /// The treat was eaten: hearts rise from the treat to the bond bar, which grows by <paramref name="bondFraction"/>
        /// (wrapping to a new level when it fills). The sheet was refreshed just before: the bar ends on its new value.
        /// Reduce Motion: the bar takes its value and one heart fades in place.
        /// </summary>
        public void PlayTreatGiven(float bondFraction)
        {
            if (!IsOpen || _bondFill == null)
                return;
            float grow = Mathf.Clamp01(bondFraction);
            _bondFrom = _bondNow - grow;
            _heartTime = 0f;
            _press = 0f;
            if (ReduceMotion || grow <= 0f)
            {
                _bondTime = -1f;
                SetBondFill(_bondNow);
            }
            else
            {
                _bondTime = 0f;
                SetBondFill(_bondFrom < 0f ? _bondFrom + 1f : _bondFrom);
            }
            for (int i = 0; i < _hearts.Length; i++)
            {
                bool on = !ReduceMotion || i == 0;
                if (_hearts[i].gameObject.activeSelf != on)
                    _hearts[i].gameObject.SetActive(on);
                SetHeart(i, 0f);
            }
        }

        private void ShowTreat(bool visible)
        {
            if (_treatRoot == null || visible == _treatShown && _treatRoot.gameObject.activeSelf == visible)
                return;
            _treatShown = visible;
            _treatRoot.gameObject.SetActive(visible);
        }

        private Sprite TreatSprite(ContentId treat)
        {
            if (treat.IsValid)
            {
                for (int i = 0; i < _treatItemIds.Length && i < _treatItemIcons.Length; i++)
                {
                    if (_treatItemIds[i] == treat.Value && _treatItemIcons[i] != null)
                        return _treatItemIcons[i];
                }
            }
            return _treatSprite;
        }

        private void OnTreat()
        {
            if (_closing || !IsOpen || _treatLook != 1)
                return;
            // Answered at once: the button dips while the animal eats.
            _press = 0f;
            (_actions as IAnimalSheetTreatActions)?.GiveTreat(_data.InstanceId);
        }

        private void TickTreat(float dt)
        {
            if (_press >= 0f)
            {
                _press += dt;
                float k = Mathf.Clamp01(_press / PressSeconds);
                float s = ReduceMotion ? 1f : 1f - 0.08f * Mathf.Sin(k * Mathf.PI);
                _treatRoot.localScale = new Vector3(s, s, 1f);
                if (k >= 1f)
                {
                    _press = -1f;
                    _treatRoot.localScale = Vector3.one;
                }
            }
            if (_bondTime >= 0f)
            {
                _bondTime += dt;
                float t = Mathf.Clamp01(_bondTime / BondGrowSeconds);
                float e = 1f - (1f - t) * (1f - t);
                if (_bondFrom >= 0f)
                {
                    SetBondFill(Mathf.Lerp(_bondFrom, _bondNow, e));
                }
                else
                {
                    // A new bond level: the bar fills to the end, then starts again from zero.
                    float total = 1f - (_bondFrom + 1f) + _bondNow;
                    float along = e * total;
                    float first = 1f - (_bondFrom + 1f);
                    SetBondFill(along < first ? _bondFrom + 1f + along : along - first);
                }
                if (t >= 1f)
                {
                    _bondTime = -1f;
                    SetBondFill(_bondNow);
                }
            }
            if (_heartTime >= 0f)
            {
                _heartTime += dt;
                bool any = false;
                for (int i = 0; i < _hearts.Length; i++)
                {
                    if (!_hearts[i].gameObject.activeSelf)
                        continue;
                    float t = (_heartTime - i * HeartStagger) / HeartSeconds;
                    if (t >= 1f)
                    {
                        _hearts[i].gameObject.SetActive(false);
                        continue;
                    }
                    any = true;
                    SetHeart(i, Mathf.Max(0f, t));
                }
                if (!any)
                    _heartTime = -1f;
            }
        }

        /// <summary>Heart i at time t (0 to 1): rises from the treat toward the bar's end, swaying, then fades.</summary>
        private void SetHeart(int i, float t)
        {
            var heart = _hearts[i];
            var image = _heartImages[i];
            var color = image.color;
            if (ReduceMotion)
            {
                color.a = Mathf.Sin(t * Mathf.PI);
                image.color = color;
                heart.anchoredPosition = BondTarget();
                return;
            }
            var from = _treatRoot.anchoredPosition + new Vector2(-216f + i * 22f, 24f);
            var to = BondTarget() + new Vector2((i - 1.5f) * 26f, 6f);
            float e = 1f - (1f - t) * (1f - t);
            float sway = Mathf.Sin((t * 2.2f + i * 0.37f) * Mathf.PI * 2f) * 16f * (1f - t);
            heart.anchoredPosition = Vector2.Lerp(from, to, e) + new Vector2(sway, 0f);
            float s = t < 0.2f ? Mathf.Lerp(0.4f, 1.1f, t / 0.2f) : Mathf.Lerp(1.1f, 0.8f, (t - 0.2f) / 0.8f);
            heart.localScale = new Vector3(s, s, 1f);
            color.a = t < 0.15f ? t / 0.15f : t > 0.7f ? (1f - t) / 0.3f : 1f;
            image.color = color;
        }

        /// <summary>The bond bar's growing end, in the card's space.</summary>
        private Vector2 BondTarget()
        {
            var bar = _bondBar.anchoredPosition;
            float width = _bondBar.rect.width;
            return bar + new Vector2(width * Mathf.Clamp01(BondFillShown) - width * _bondBar.pivot.x, 0f);
        }

        private void SetBondFill(float fraction)
        {
            float width = _bondBar.rect.width;
            float height = _bondBar.rect.height;
            fraction = Mathf.Clamp01(fraction);
            BondFillShown = fraction;
            bool visible = fraction > 0.001f;
            if (_bondFill.gameObject.activeSelf != visible)
                _bondFill.gameObject.SetActive(visible);
            _bondFill.sizeDelta = new Vector2(Mathf.Max(height, width * fraction), _bondFill.sizeDelta.y);
        }

        private void StopTreatMotion()
        {
            _press = -1f;
            _heartTime = -1f;
            if (_treatRoot != null)
                _treatRoot.localScale = Vector3.one;
            for (int i = 0; i < _hearts.Length; i++)
            {
                if (_hearts[i].gameObject.activeSelf)
                    _hearts[i].gameObject.SetActive(false);
            }
            if (_bondTime >= 0f)
            {
                _bondTime = -1f;
                SetBondFill(_bondNow);
            }
        }

        /// <summary>Prefab builder hook for the T36 treat row (Editor FeedbackUiPipeline).</summary>
        public void SetTreatReferences(RectTransform root, Button button, Image background, Image icon, TMP_Text label, TMP_Text count,
            GameObject preferred, Sprite treatSprite, uint[] itemIds, Sprite[] itemIcons, RectTransform[] hearts, Image[] heartImages)
        {
            _treatRoot = root;
            _treatButton = button;
            _treatBackground = background;
            _treatIcon = icon;
            _treatLabel = label;
            _treatCount = count;
            _treatPreferred = preferred;
            _treatSprite = treatSprite;
            _treatItemIds = itemIds;
            _treatItemIcons = itemIcons;
            _hearts = hearts;
            _heartImages = heartImages;
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
            if (_treatButton != null)
                _treatButton.onClick.RemoveListener(OnTreat);
        }
    }
}
