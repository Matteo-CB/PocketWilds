using System;
using System.Collections.Generic;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_Album (T44A « Albums », doc « 01 Albums et autocollants » sections 1.2, 1.7, 1.10, 1.12; built by
    /// AlbumScreenPipeline, Addressables group Albums). A screen-space canvas over the hidden refuge: the album stars (ICO_AlbumStar,
    /// a round five-pointed star), the title and close; the shelf of 16 covers (a progress bar and « 37/120 » each, a soft
    /// lock while no page is open) with « Choisir un autocollant » and its price; a page of 12 recycled slots (3 × 4) with
    /// « 6/12 » and its half and full rewards, following the finger on a horizontal swipe; the choose list and the
    /// confirmation « 10 étoiles pour cet autocollant ? »; a note pill; the reveal layer on its own canvas (sorting order
    /// 31) that also plays over the refuge with the screen part hidden. Rarity is never colour alone: Common a single white
    /// border and « Commun », Rare a double cream border, a small round dot and « Rare », Shiny the double border, a round
    /// pearl and the slow pearly sheen (SH_UI_StickerPearl, still with Reduce Motion) and « Brillant ». Empty slots show the
    /// shared silhouette (SH_UI_StickerSilhouette: soft grey 40 %, a dotted outline) and their source's icon. The UI kit's
    /// sprites, the icons and the fonts are borrowed from what the scene loaded (<see cref="Lend"/>). Nothing allocates per
    /// frame after <see cref="Bind"/>.
    /// </summary>
    public sealed class AlbumScreenView : MonoBehaviour, IAlbumScreenView
    {
        /// <summary>The icons, borrowed by name: the 11 hints (by <see cref="StickerHint"/>) then the screen's own.</summary>
        public enum AlbumIcon
        {
            HintActivity = 0,
            HintRecord = 1,
            HintChallenge = 2,
            HintWish = 3,
            HintExpedition = 4,
            HintGift = 5,
            HintEvent = 6,
            HintVisitor = 7,
            HintGarden = 8,
            HintZone = 9,
            HintWorld = 10,
            Star = 11,
            Album = 12,
            Back = 13,
            Check = 14,
            Gift = 15,
            Lock = 16,
            KindBiome = 17,
            KindEvent = 18,
            KindSeason = 19,
            KindActivity = 20,
            Paw = 21,
        }

        private const float NoteIn = 0.15f;
        private const float NoteHold = 2f;
        private const float NoteOut = 0.25f;
        private const float PressSeconds = 0.35f;

        /// <summary>The grids' pitches (AlbumScreenPipeline places the pooled views at these steps).</summary>
        public const float CoverPitchX = 500f;

        public const float CoverPitchY = 372f;
        public const float RowPitch = 172f;
        public const float ListPadding = 24f;

        private static readonly int StillId = Shader.PropertyToID(AlbumLayout.StillProperty);

        [SerializeField] private AlbumScreenParts _parts = new AlbumScreenParts();

        [Header("In the bundle")]
        [SerializeField] private Material _pearl;
        [SerializeField] private Material _silhouette;
        [SerializeField] private StickerArtCatalog _art;

        [Header("Borrowed at run time from the scene (never in the bundle)")]
        [SerializeField] private Image[] _kitImages = Array.Empty<Image>();
        [SerializeField] private string[] _kitNames = Array.Empty<string>();
        [SerializeField] private TMP_Text[] _labels = Array.Empty<TMP_Text>();
        [SerializeField] private string[] _labelFonts = Array.Empty<string>();
        [SerializeField] private string[] _labelMaterials = Array.Empty<string>();

        /// <summary>One name per <see cref="AlbumIcon"/>, a second choice after « | ».</summary>
        [SerializeField] private string[] _iconNames = Array.Empty<string>();

        [Header("Colours")]
        [SerializeField] private Color _cream = new Color(0.99f, 0.96f, 0.90f, 1f);
        [SerializeField] private Color _card = new Color(1f, 0.99f, 0.96f, 1f);
        [SerializeField] private Color _cardLocked = new Color(0.93f, 0.90f, 0.85f, 1f);
        [SerializeField] private Color _text = new Color(0.29f, 0.23f, 0.17f, 1f);
        [SerializeField] private Color _textSoft = new Color(0.54f, 0.46f, 0.38f, 1f);
        [SerializeField] private Color _accent = new Color(1f, 0.69f, 0.37f, 1f);
        [SerializeField] private Color _disabled = new Color(0.80f, 0.77f, 0.72f, 1f);
        [SerializeField] private Color _frameCommon = Color.white;
        [SerializeField] private Color _frameCream = new Color(0.98f, 0.92f, 0.80f, 1f);
        [SerializeField] private Color _innerCream = new Color(0.93f, 0.84f, 0.68f, 1f);
        [SerializeField] private Color _rareDot = new Color(0.86f, 0.55f, 0.50f, 1f);
        [SerializeField] private Color _pearlTint = new Color(0.98f, 0.95f, 0.97f, 1f);
        [SerializeField] private Color _emptyFrame = new Color(0.95f, 0.92f, 0.87f, 1f);
        [SerializeField] private Color _confetti0 = new Color(1f, 0.78f, 0.55f, 1f);
        [SerializeField] private Color _confetti1 = new Color(0.96f, 0.76f, 0.83f, 1f);
        [SerializeField] private Color _confetti2 = new Color(0.72f, 0.86f, 0.95f, 1f);

        private readonly SafeAreaFitter _fitter = new SafeAreaFitter();
        private IAlbumScreenActions _actions;
        private CanvasScaler _scaler;
        private Sprite[] _icons;
        private bool _lent;
        private bool _initialized;
        private bool _shown;
        private bool _screenShown;
        private bool _reduceMotion;
        private AlbumScreenMode _mode;

        // Texts read at Bind.
        private readonly string[] _rarityWords = new string[3];
        private UiCounted _missing;
        private UiCounted _duplicate;
        private string _chooseHint = string.Empty;

        // Labels from char buffers.
        private UiLabel _starsLabel;
        private UiLabel _priceLabel;
        private UiLabel _confirmPriceLabel;
        private UiLabel _ratioLabel;
        private UiLabel _duplicateLabel;
        private UiLabel[] _coverRatios;
        private UiLabel[] _rowMissing;
        private UiLabel[] _slotPrices;
        private int _priceShown = -1;

        // Layout of the pooled lists.
        private bool[] _coverShown;
        private bool[] _rowShown;

        // The page: slide, drag, the « Nouveau » press.
        private float _slideT = -1f;
        private int _slideDir;
        private float _dragOffset;
        private float _springFrom;
        private float _springT = -1f;
        private float[] _pressT;

        // The note.
        private float _noteT = -1f;

        // The reveal.
        private bool _revealShown;
        private StickerRevealModel _reveal;
        private Vector2 _revealTarget;
        private Vector2 _starsTarget;
        private Vector2 _cardHome;
        private Vector2 _pouchHome;
        private Vector2[] _bubbleHome;

        // ---------------- IAlbumScreenView ----------------

        public bool Valid
        {
            get
            {
                var p = _parts;
                if (p == null || p.canvas == null || p.content == null || p.screen == null || p.safeArea == null || p.title == null || p.close == null
                    || p.stars == null || p.back == null || p.shelf == null || p.shelfContent == null || p.choose == null || p.page == null
                    || p.pagePanel == null || p.pageGroup == null || p.swipe == null || p.barFill == null || p.ratio == null || p.chooseList == null
                    || p.chooseContent == null || p.confirm == null || p.yes == null || p.no == null || p.notePill == null || p.noteGroup == null
                    || p.noteLabel == null || p.reveal == null)
                    return false;
                if (p.covers.Length < AlbumLayout.AlbumCapacity || p.slots.Length < AlbumLayout.SlotsPerPage || p.rows.Length < AlbumLayout.PageCapacity)
                    return false;
                foreach (var c in p.covers)
                    if (c == null || c.root == null || c.button == null || c.card == null || c.kindIcon == null || c.title == null || c.barFill == null
                        || c.ratio == null || c.lockIcon == null || c.check == null)
                        return false;
                foreach (var s in p.slots)
                    if (s == null || s.root == null || s.button == null || s.frame == null || s.inner == null || s.art == null || s.silhouette == null
                        || s.silhouetteUv == null || s.artUv == null || s.hintIcon == null || s.sign == null || s.name == null || s.rarity == null
                        || s.soon == null || s.newPill == null || s.priceChip == null || s.price == null)
                        return false;
                foreach (var r in p.rows)
                    if (r == null || r.root == null || r.button == null || r.title == null || r.missing == null || r.barFill == null)
                        return false;
                var v = p.reveal;
                if (v.canvas == null || v.root == null || v.tap == null || v.pouch == null || v.card == null || v.frame == null || v.art == null
                    || v.artUv == null || v.silhouette == null || v.cardBack == null || v.duplicate == null || v.banner == null || v.ring == null
                    || v.landRing == null || v.confetti.Length < AlbumLayout.Confetti || v.bubbles.Length < AlbumLayout.Bubbles
                    || v.stars.Length < AlbumLayout.FlyingStars)
                    return false;
                return true;
            }
        }

        public bool Shown => _shown;

        public bool ScreenShown => _screenShown;

        public bool ConfirmShown => _parts.confirm != null && _parts.confirm.activeSelf;

        public bool RevealShown => _revealShown;

        public StickerArtCatalog ArtCatalog => _art;

        public bool ReduceMotion
        {
            get => _reduceMotion;
            set
            {
                _reduceMotion = value;
                Shader.SetGlobalFloat(StillId, value ? 1f : 0f);
            }
        }

        public Canvas Canvas => _parts.canvas;

        public AlbumScreenParts Parts => _parts;

        public AlbumScreenMode ModeShown => _mode;

        public Material PearlMaterial => _pearl;

        public Material SilhouetteMaterial => _silhouette;

        /// <summary>Tests: the labels whose fonts are borrowed.</summary>
        public TMP_Text[] BorrowedLabels => _labels;

        public bool NoteShown => _noteT >= 0f;

        public string NoteText => _parts.noteLabel.text;

        public string StarsText => _parts.stars.text;

        public string RatioText => _parts.ratio.text;

        public string DuplicateText => _parts.reveal.duplicate.text;

        public Sprite IconOf(AlbumIcon icon) => _icons != null && (int)icon < _icons.Length ? _icons[(int)icon] : null;

        public void Bind(ILocalization text, IAlbumScreenActions actions)
        {
            Initialize();
            _actions = actions;
            if (!_lent)
                Lend(Resources.FindObjectsOfTypeAll<Sprite>(), Resources.FindObjectsOfTypeAll<TMP_FontAsset>(), Resources.FindObjectsOfTypeAll<Material>());
            BindTexts(text);
        }

        /// <summary>
        /// Borrows the scene's sprites (the UI kit, the icons), fonts and font materials by name: the bundle never holds a
        /// copy. Captures and tests lend what they loaded. Allocates (one lookup), once per load.
        /// </summary>
        public void Lend(IReadOnlyList<Sprite> sprites, IReadOnlyList<TMP_FontAsset> fonts, IReadOnlyList<Material> materials)
        {
            Initialize();
            _lent = true;
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in _kitNames)
                if (!string.IsNullOrEmpty(name))
                    wanted.Add(name);
            foreach (var names in _iconNames)
                if (!string.IsNullOrEmpty(names))
                    foreach (var name in names.Split('|'))
                        wanted.Add(name);
            var found = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            if (sprites != null)
                for (int i = 0; i < sprites.Count; i++)
                {
                    var sprite = sprites[i];
                    if (sprite != null && wanted.Contains(sprite.name) && !found.ContainsKey(sprite.name))
                        found.Add(sprite.name, sprite);
                }
            List<string> missing = null;
            for (int i = 0; i < _kitImages.Length && i < _kitNames.Length; i++)
            {
                var image = _kitImages[i];
                if (image == null)
                    continue;
                if (found.TryGetValue(_kitNames[i] ?? string.Empty, out var sprite))
                    image.sprite = sprite;
                else if (!string.IsNullOrEmpty(_kitNames[i]) && (missing ??= new List<string>()).IndexOf(_kitNames[i]) < 0)
                    missing.Add(_kitNames[i]);
            }
            if (missing != null)
                GameLog.Warning(LogCategory.Presentation, "Albums: kit sprites not loaded by the scene (plain shapes shown): " + string.Join(", ", missing));
            int iconCount = Enum.GetValues(typeof(AlbumIcon)).Length;
            _icons = new Sprite[iconCount];
            for (int i = 0; i < iconCount && i < _iconNames.Length; i++)
            {
                if (string.IsNullOrEmpty(_iconNames[i]))
                    continue;
                foreach (var name in _iconNames[i].Split('|'))
                    if (found.TryGetValue(name, out var sprite))
                    {
                        _icons[i] = sprite;
                        break;
                    }
            }
            var fontByName = new Dictionary<string, TMP_FontAsset>(StringComparer.Ordinal);
            if (fonts != null)
                for (int i = 0; i < fonts.Count; i++)
                    if (fonts[i] != null && !fontByName.ContainsKey(fonts[i].name))
                        fontByName.Add(fonts[i].name, fonts[i]);
            var materialWanted = new HashSet<string>(_labelMaterials, StringComparer.Ordinal);
            var materialByName = new Dictionary<string, Material>(StringComparer.Ordinal);
            if (materials != null)
                for (int i = 0; i < materials.Count; i++)
                    if (materials[i] != null && materialWanted.Contains(materials[i].name) && !materialByName.ContainsKey(materials[i].name))
                        materialByName.Add(materials[i].name, materials[i]);
            for (int i = 0; i < _labels.Length && i < _labelFonts.Length; i++)
            {
                var label = _labels[i];
                if (label == null || !fontByName.TryGetValue(_labelFonts[i] ?? string.Empty, out var font))
                    continue;
                label.font = font;
                if (i < _labelMaterials.Length && materialByName.TryGetValue(_labelMaterials[i] ?? string.Empty, out var material))
                    label.fontSharedMaterial = material;
                else
                    label.fontSharedMaterial = font.material;
            }
            ApplyIcons();
        }

        private void ApplyIcons()
        {
            var p = _parts;
            var star = IconOf(AlbumIcon.Star);
            SetIcon(p.starsIcon, star);
            SetIcon(p.chooseStar, star);
            SetIcon(p.confirmStar, star);
            SetIcon(p.backIcon, IconOf(AlbumIcon.Back));
            SetIcon(p.previousIcon, IconOf(AlbumIcon.Back));
            SetIcon(p.nextIcon, IconOf(AlbumIcon.Back));
            SetIcon(p.halfIcon, IconOf(AlbumIcon.Gift));
            SetIcon(p.fullIcon, IconOf(AlbumIcon.Gift));
            SetIcon(p.halfCheck, IconOf(AlbumIcon.Check));
            SetIcon(p.fullCheck, IconOf(AlbumIcon.Check));
            foreach (var c in p.covers)
            {
                SetIcon(c.lockIcon, IconOf(AlbumIcon.Lock));
                SetIcon(c.check, IconOf(AlbumIcon.Check));
            }
            foreach (var s in p.slots)
                SetIcon(s.priceStar, star);
            foreach (var s in p.reveal.stars)
                SetIcon(s, star);
            SetIcon(p.reveal.cardBackPaw, IconOf(AlbumIcon.Paw));
        }

        private static void SetIcon(Image image, Sprite sprite)
        {
            if (image == null)
                return;
            image.sprite = sprite;
            image.preserveAspect = true;
            // An icon the scene did not load stays hidden (never a plain square).
            image.enabled = sprite != null;
        }

        private void BindTexts(ILocalization text)
        {
            string T(string key) => text != null ? text.Get(key) : key;
            var p = _parts;
            SetText(p.title, T(AlbumText.Title));
            SetText(p.chooseLabel, T(AlbumText.Choose));
            SetText(p.chooseTitle, T(AlbumText.Choose));
            SetText(p.confirmText, T(AlbumText.ChooseConfirm));
            SetText(p.yesLabel, T(AlbumText.Yes));
            SetText(p.noLabel, T(AlbumText.No));
            _chooseHint = T(AlbumText.ChooseHint);
            _rarityWords[0] = T(AlbumText.Common);
            _rarityWords[1] = T(AlbumText.Rare);
            _rarityWords[2] = T(AlbumText.Shiny);
            string soon = T(AlbumText.Soon), isNew = T(AlbumText.New);
            foreach (var s in p.slots)
            {
                SetText(s.soon, soon);
                SetText(s.newLabel, isNew);
            }
            SetText(p.reveal.newLabel, isNew);
            _missing = UiCounted.From(text, AlbumText.Missing);
            _duplicate = UiCounted.From(text, AlbumText.Duplicate);
            // The doc's « {0} » is written like the other counted texts' « {n} ».
            _duplicate.One = _duplicate.One?.Replace("{0}", "{n}");
            _duplicate.Other = _duplicate.Other?.Replace("{0}", "{n}");
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            var p = _parts;
            _scaler = GetComponent<CanvasScaler>();
            _starsLabel = new UiLabel(p.stars, 16);
            _priceLabel = new UiLabel(p.choosePrice, 16);
            _confirmPriceLabel = new UiLabel(p.confirmPrice, 16);
            _ratioLabel = new UiLabel(p.ratio, 16);
            _duplicateLabel = new UiLabel(p.reveal.duplicate, 96);
            _coverRatios = new UiLabel[p.covers.Length];
            _coverShown = new bool[p.covers.Length];
            for (int i = 0; i < p.covers.Length; i++)
            {
                _coverRatios[i] = new UiLabel(p.covers[i].ratio, 16);
                int cover = i;
                p.covers[i].button.onClick.AddListener(() => _actions?.OpenAlbum(cover));
            }
            _rowMissing = new UiLabel[p.rows.Length];
            _rowShown = new bool[p.rows.Length];
            for (int i = 0; i < p.rows.Length; i++)
            {
                _rowMissing[i] = new UiLabel(p.rows[i].missing, 64);
                int row = i;
                p.rows[i].button.onClick.AddListener(() => _actions?.PickChoicePage(row));
            }
            _slotPrices = new UiLabel[p.slots.Length];
            _pressT = new float[p.slots.Length];
            for (int i = 0; i < p.slots.Length; i++)
            {
                _slotPrices[i] = new UiLabel(p.slots[i].price, 16);
                _pressT[i] = -1f;
                int slot = i;
                p.slots[i].button.onClick.AddListener(() => _actions?.TapSlot(slot));
            }
            p.close.onClick.AddListener(() => _actions?.CloseScreen());
            p.back.onClick.AddListener(() => _actions?.Back());
            p.choose.onClick.AddListener(() => _actions?.OpenChoose());
            if (p.previous != null)
                p.previous.onClick.AddListener(() => _actions?.PreviousPage());
            if (p.next != null)
                p.next.onClick.AddListener(() => _actions?.NextPage());
            p.yes.onClick.AddListener(() => _actions?.ConfirmChoice());
            p.no.onClick.AddListener(() => _actions?.CancelChoice());
            if (p.confirmDim != null)
                p.confirmDim.onClick.AddListener(() => _actions?.CancelChoice());
            p.reveal.tap.onClick.AddListener(() => _actions?.TapReveal());
            p.swipe.Released = OnSwipeReleased;
            _cardHome = p.reveal.card.anchoredPosition;
            _pouchHome = p.reveal.pouch.anchoredPosition;
            _bubbleHome = new Vector2[p.reveal.bubbles.Length];
            for (int i = 0; i < p.reveal.bubbles.Length; i++)
                _bubbleHome[i] = p.reveal.bubbles[i].rectTransform.anchoredPosition;
            p.confirm.SetActive(false);
            p.reveal.root.gameObject.SetActive(false);
        }

        public void Show()
        {
            Initialize();
            _parts.content.SetActive(true);
            _parts.canvas.enabled = true;
            _shown = true;
            if (_scaler != null)
            {
                _scaler.enabled = false;
                _scaler.enabled = true;
            }
            _fitter.Fit(_parts.safeArea);
            Canvas.ForceUpdateCanvases();
        }

        public void Hide()
        {
            Initialize();
            _shown = false;
            _noteT = -1f;
            _parts.notePill.gameObject.SetActive(false);
            HideReveal();
            SetScreenShown(false);
            _parts.canvas.enabled = false;
            _parts.content.SetActive(false);
        }

        public void SetScreenShown(bool shown)
        {
            Initialize();
            _screenShown = shown;
            _parts.screen.SetActive(shown);
            if (!shown)
            {
                _parts.confirm.SetActive(false);
                _noteT = -1f;
                _parts.notePill.gameObject.SetActive(false);
            }
        }

        public void SetMode(AlbumScreenMode mode)
        {
            Initialize();
            _mode = mode;
            var p = _parts;
            bool page = mode == AlbumScreenMode.Page || mode == AlbumScreenMode.ChoosePage;
            SetActive(p.shelf, mode == AlbumScreenMode.Shelf);
            SetActive(p.page, page);
            SetActive(p.chooseList, mode == AlbumScreenMode.ChooseList);
            SetActive(p.back.gameObject, mode != AlbumScreenMode.Shelf);
            SetActive(p.title.gameObject, true);
            _slideT = -1f;
            _springT = -1f;
            _dragOffset = 0f;
            ApplyPanel(0f, 1f);
            if (mode == AlbumScreenMode.Shelf)
                p.shelfScroll?.StopMovement();
            if (mode == AlbumScreenMode.ChooseList)
                p.chooseScroll?.StopMovement();
        }

        public void SetStars(int stars, int price, bool canChoose)
        {
            Initialize();
            _starsLabel.SetNumber(stars);
            if (price != _priceShown)
            {
                _priceShown = price;
                _priceLabel.SetNumber(price);
                _confirmPriceLabel.SetNumber(price);
                for (int i = 0; i < _slotPrices.Length; i++)
                    _slotPrices[i].SetNumber(price);
            }
            _parts.choose.interactable = canChoose;
            if (_parts.chooseBack != null)
                _parts.chooseBack.color = canChoose ? _accent : _disabled;
        }

        public void SetCover(int cover, in AlbumCoverModel model)
        {
            Initialize();
            if ((uint)cover >= (uint)_parts.covers.Length)
                return;
            var c = _parts.covers[cover];
            SetActive(c.root.gameObject, model.Shown);
            if (_coverShown[cover] != model.Shown)
            {
                _coverShown[cover] = model.Shown;
                LayoutGrid();
            }
            if (!model.Shown)
                return;
            SetText(c.title, model.Title);
            c.title.color = model.Open ? _text : _textSoft;
            c.card.color = model.Open ? _card : _cardLocked;
            var kind = model.Kind switch
            {
                AlbumKind.Event => AlbumIcon.KindEvent,
                AlbumKind.Season => AlbumIcon.KindSeason,
                AlbumKind.Activity => AlbumIcon.KindActivity,
                _ => AlbumIcon.KindBiome,
            };
            var icon = IconOf(kind);
            c.kindIcon.sprite = icon;
            bool drawnCover = c.coverArt != null && c.coverArt.sprite != null;
            c.kindIcon.enabled = icon != null && !drawnCover;
            c.kindIcon.color = model.Open ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            if (drawnCover)
                c.coverArt.color = model.Open ? Color.white : new Color(1f, 1f, 1f, 0.5f);
            if (c.art != null)
                c.art.color = model.Open ? _frameCream : _cardLocked;
            SetActive(c.lockIcon.gameObject, !model.Open);
            SetActive(c.check.gameObject, model.Complete);
            if (c.dot != null)
                SetActive(c.dot.gameObject, model.New);
            c.barFill.fillAmount = model.Total > 0 ? Mathf.Clamp01(model.Owned / (float)model.Total) : 0f;
            _coverRatios[cover].SetRatio(model.Owned, model.Total);
        }

        public void SetPage(in AlbumPageModel model)
        {
            Initialize();
            var p = _parts;
            SetText(p.pageAlbum, model.Choosing ? _chooseHint : model.Album);
            SetText(p.pageTitle, model.Title);
            _ratioLabel.SetRatio(model.Owned, model.Total);
            p.barFill.fillAmount = model.Total > 0 ? Mathf.Clamp01(model.Owned / (float)model.Total) : 0f;
            bool half = model.Owned * 2 >= model.Total, full = model.Owned >= model.Total;
            if (p.halfCheck != null)
                SetActive(p.halfCheck.gameObject, half);
            if (p.fullCheck != null)
                SetActive(p.fullCheck.gameObject, full);
            if (p.halfIcon != null)
                p.halfIcon.color = half ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
            if (p.fullIcon != null)
                p.fullIcon.color = full ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
            if (p.previous != null)
            {
                p.previous.interactable = model.HasPrevious;
                if (p.previousIcon != null)
                    p.previousIcon.color = model.HasPrevious ? _textSoft : new Color(_textSoft.r, _textSoft.g, _textSoft.b, 0.25f);
            }
            if (p.next != null)
            {
                p.next.interactable = model.HasNext;
                if (p.nextIcon != null)
                    p.nextIcon.color = model.HasNext ? _textSoft : new Color(_textSoft.r, _textSoft.g, _textSoft.b, 0.25f);
            }
        }

        public void SetSlot(int slot, in AlbumSlotModel model)
        {
            Initialize();
            if ((uint)slot >= (uint)_parts.slots.Length)
                return;
            var s = _parts.slots[slot];
            bool owned = model.State == StickerSlotState.Owned;
            bool empty = model.State == StickerSlotState.Empty;
            bool rare = model.Rarity != StickerRarity.Common;
            bool shiny = model.Rarity == StickerRarity.Shiny;
            s.frame.color = owned ? (rare ? _frameCream : _frameCommon) : _emptyFrame;
            SetActive(s.inner.gameObject, owned && rare);
            s.inner.color = _innerCream;
            bool art = owned && model.Art != null;
            SetActive(s.art.gameObject, art);
            if (art)
            {
                s.art.sprite = model.Art;
                s.art.material = shiny ? _pearl : null;
                s.art.color = Color.white;
            }
            // The silhouette: the missing sticker's own shape when its art is there, else the soft generic shape.
            bool silhouette = !art;
            SetActive(s.silhouette.gameObject, silhouette);
            if (silhouette)
            {
                s.silhouette.sprite = model.Art;
                s.silhouetteUv.Generic = model.Art == null;
                s.silhouette.material = _silhouette;
                s.silhouette.color = owned ? new Color(1f, 1f, 1f, 1f) : model.State == StickerSlotState.Soon ? new Color(1f, 1f, 1f, 0.6f) : Color.white;
            }
            var hint = empty ? IconOf((AlbumIcon)(int)model.Hint) : null;
            SetActive(s.hintIcon.gameObject, empty && hint != null);
            if (empty && hint != null)
                s.hintIcon.sprite = hint;
            SetSign(s.sign, s.signLight, owned ? model.Rarity : StickerRarity.Common, owned && rare);
            SetActive(s.name.gameObject, owned);
            SetActive(s.rarity.gameObject, owned);
            if (owned)
            {
                SetText(s.name, model.Name);
                SetText(s.rarity, _rarityWords[(int)model.Rarity]);
            }
            SetActive(s.soon.gameObject, model.State == StickerSlotState.Soon);
            SetActive(s.newPill.gameObject, owned && model.New);
            SetActive(s.priceChip.gameObject, model.Choosable);
            s.button.interactable = !owned;
            // « Nouveau »: it settles with a little press as the page opens.
            _pressT[slot] = owned && model.New && !_reduceMotion ? 0f : -1f;
            s.root.localScale = Vector3.one;
        }

        private void SetSign(Image sign, Image light, StickerRarity rarity, bool shown)
        {
            SetActive(sign.gameObject, shown);
            if (light != null)
                SetActive(light.gameObject, shown && rarity == StickerRarity.Shiny);
            if (!shown)
                return;
            bool shiny = rarity == StickerRarity.Shiny;
            sign.color = shiny ? _pearlTint : _rareDot;
            sign.material = shiny ? _pearl : null;
            sign.rectTransform.localScale = shiny ? new Vector3(1.25f, 1.25f, 1f) : Vector3.one;
        }

        public void SlidePage(int direction)
        {
            Initialize();
            _springT = -1f;
            _slideDir = direction;
            _slideT = 0f;
            ApplySlide(0f);
        }

        public void SetChoiceRow(int row, in AlbumChoiceRowModel model)
        {
            Initialize();
            if ((uint)row >= (uint)_parts.rows.Length)
                return;
            var r = _parts.rows[row];
            SetActive(r.root.gameObject, model.Shown);
            if (_rowShown[row] != model.Shown)
            {
                _rowShown[row] = model.Shown;
                LayoutRows();
            }
            if (!model.Shown)
                return;
            SetText(r.title, model.Title);
            _rowMissing[row].SetTemplate(_missing.For(model.Missing), model.Missing);
            r.barFill.fillAmount = Mathf.Clamp01(model.Owned / (float)AlbumLayout.SlotsPerPage);
        }

        public void ShowConfirm(bool shown, in AlbumSlotModel model, int price)
        {
            Initialize();
            var p = _parts;
            SetActive(p.confirm, shown);
            if (!shown)
                return;
            if (p.confirmArt != null)
            {
                p.confirmArt.sprite = model.Art;
                p.confirmArt.material = _silhouette;
                if (p.confirmArtUv != null)
                    p.confirmArtUv.Generic = model.Art == null;
            }
            _confirmPriceLabel.SetNumber(price);
        }

        public void ShowNote(string text)
        {
            Initialize();
            if (string.IsNullOrEmpty(text) || !_screenShown)
                return;
            _parts.noteLabel.text = text;
            _parts.notePill.gameObject.SetActive(true);
            _noteT = 0f;
            ApplyNote();
        }

        public Vector2 SlotScreenPosition(int slot) =>
            (uint)slot < (uint)_parts.slots.Length ? ScreenOf(_parts.slots[slot].art.rectTransform) : StarsScreenPosition;

        public Vector2 StarsScreenPosition => ScreenOf(_parts.starsIcon != null ? _parts.starsIcon.rectTransform : (RectTransform)_parts.stars.transform);

        public Vector2 TitleScreenPosition => ScreenOf((RectTransform)_parts.title.transform);

        private Vector2 ScreenOf(RectTransform rect)
        {
            var canvas = _parts.canvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(camera, rect.position);
        }

        // ---------------- the reveal ----------------

        public void ShowReveal(in StickerRevealModel model)
        {
            Initialize();
            var v = _parts.reveal;
            _reveal = model;
            _revealShown = true;
            v.root.gameObject.SetActive(true);
            v.canvas.enabled = true;
            bool rare = model.Rarity != StickerRarity.Common;
            bool shiny = model.Rarity == StickerRarity.Shiny;
            v.frame.color = rare ? _frameCream : _frameCommon;
            if (v.inner != null)
            {
                SetActive(v.inner.gameObject, rare);
                v.inner.color = _innerCream;
            }
            bool art = model.Art != null;
            SetActive(v.art.gameObject, art);
            if (art)
            {
                v.art.sprite = model.Art;
                v.art.material = shiny ? _pearl : null;
                v.artUv.SetPhase(shiny, shiny ? -1f : 0f);
            }
            SetActive(v.silhouette.gameObject, !art);
            if (!art)
            {
                v.silhouette.sprite = null;
                v.silhouette.material = _silhouette;
                if (v.silhouetteUv != null)
                    v.silhouetteUv.Generic = true;
            }
            if (v.sign != null)
                SetSign(v.sign, v.signLight, model.Rarity, rare);
            SetText(v.name, model.Name);
            SetText(v.rarity, _rarityWords[(int)model.Rarity]);
            if (model.Stars > 0)
                _duplicateLabel.SetTemplate(_duplicate.For(model.Stars), model.Stars);
            else
                _duplicateLabel.SetString(string.Empty);
            SetText(v.banner, model.Banner ?? string.Empty);
            _revealTarget = LocalOf(v.root, model.Target);
            _starsTarget = LocalOf(v.root, model.StarsTarget);
            ApplyReveal(0f, false, 0f);
        }

        private Vector2 LocalOf(RectTransform root, Vector2 screen)
        {
            var canvas = _parts.canvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, camera, out var local) ? local : Vector2.zero;
        }

        public void SetRevealTime(float pouchAge, bool opened, float sinceOpen)
        {
            if (!_revealShown)
                return;
            ApplyReveal(pouchAge, opened, sinceOpen);
        }

        public void HideReveal()
        {
            var v = _parts.reveal;
            _revealShown = false;
            if (v.root != null)
                v.root.gameObject.SetActive(false);
        }

        /// <summary>Section 1.10, frame by frame from the times the presenter gives (no state of its own but the model).</summary>
        private void ApplyReveal(float pouchAge, bool opened, float t)
        {
            var v = _parts.reveal;
            var rarity = _reveal.Rarity;
            float exit = StickerRevealTiming.Exit(rarity);
            float total = StickerRevealTiming.Total(rarity);
            float u = opened ? t - exit : -1f;
            bool motion = !_reduceMotion;

            // The dim: in with the pouch, out at the end.
            float dimIn = Mathf.Clamp01(pouchAge / 0.15f);
            float dimOut = opened ? 1f - Mathf.Clamp01((t - (total - 0.2f)) / 0.2f) : 1f;
            if (v.dim != null)
                v.dim.color = new Color(v.dim.color.r, v.dim.color.g, v.dim.color.b, 0.32f * dimIn * dimOut);

            // The pouch: pops in, waits (a soft breath), opens its tab and fades.
            float pouchAlpha;
            float pouchScale;
            if (!opened)
            {
                float k = Mathf.Clamp01(pouchAge / StickerRevealTiming.PouchIn);
                pouchScale = motion ? Mathf.LerpUnclamped(0.6f, 1f, BackOut(k)) : 1f;
                pouchAlpha = motion ? 1f : k;
                if (motion && k >= 1f)
                    pouchScale *= 1f + 0.02f * Mathf.Sin((pouchAge - StickerRevealTiming.PouchIn) * 6f);
            }
            else
            {
                float k = Mathf.Clamp01(t / 0.2f);
                pouchScale = motion ? 1f + 0.08f * k : 1f;
                pouchAlpha = 1f - k;
            }
            SetActive(v.pouch.gameObject, pouchAlpha > 0.01f);
            v.pouch.localScale = new Vector3(pouchScale, pouchScale, 1f);
            Alpha(v.pouchBack, pouchAlpha);
            Alpha(v.flapImage, pouchAlpha);
            Alpha(v.tab, pouchAlpha);
            if (v.flap != null)
                v.flap.localEulerAngles = new Vector3(opened && motion ? Mathf.Clamp01(t / 0.15f) * 70f : 0f, 0f, 0f);

            // The sticker.
            SetActive(v.card.gameObject, opened);
            bool showRing = false, showBubbles = false, showConfetti = false;
            float ringK = 0f, bubbleK = 0f, confettiK = 0f, sheen = -1f;
            if (opened)
            {
                Vector2 position = _cardHome;
                float scale = 1f, angle = 0f, alpha = 1f, flip = 1f;
                bool faceDown = false;
                if (u < 0f)
                {
                    // H-VFX-038: out face down, (Shiny: a pause,) turned over with a soft ring.
                    float e = t;
                    float rise = Mathf.Clamp01(e / 0.15f);
                    position = Vector2.LerpUnclamped(_pouchHome, _cardHome, motion ? EaseOut(rise) : 1f);
                    float flipStart = rarity == StickerRarity.Shiny ? 0.15f + StickerRevealTiming.ShinyPause : 0.15f;
                    float flipK = Mathf.Clamp01((e - flipStart) / 0.15f);
                    faceDown = flipK < 0.5f;
                    flip = motion ? Mathf.Abs(Mathf.Cos(flipK * Mathf.PI)) : 1f;
                    alpha = motion ? 1f : Mathf.Clamp01(e / 0.15f);
                    if (!motion)
                        faceDown = false;
                    if (e >= flipStart)
                    {
                        showRing = true;
                        ringK = Mathf.Clamp01((e - flipStart) / 0.3f);
                    }
                    if (rarity == StickerRarity.Shiny && e >= flipStart + 0.05f)
                    {
                        showBubbles = motion;
                        bubbleK = Mathf.Clamp01((e - flipStart - 0.05f) / 0.6f);
                        // Then the pearl sheen passes once over the sticker.
                        sheen = Mathf.Clamp01((e - (exit - 0.25f)) / 0.25f);
                    }
                }
                else
                {
                    // Moment 21: pop with -8° to 0, squash 0.95 for 80 ms, then the flight or the direct paste.
                    float pop = Mathf.Clamp01(u / StickerRevealTiming.PopEnd);
                    if (motion)
                    {
                        scale = u < StickerRevealTiming.PopEnd ? Mathf.LerpUnclamped(1.15f, 1f, EaseOut(pop))
                            : u < StickerRevealTiming.SquashEnd ? 0.95f : 1f;
                        angle = u < StickerRevealTiming.PopEnd ? Mathf.Lerp(-8f, 0f, EaseOut(pop)) : 0f;
                    }
                    float fly = Mathf.Clamp01((u - StickerRevealTiming.FlyStart) / (StickerRevealTiming.Paste - StickerRevealTiming.FlyStart));
                    if (_reveal.Stars > 0)
                    {
                        // A duplicate: « Déjà collé », then it turns into stars that fly to the counter.
                        alpha = 1f - Mathf.Clamp01((u - StickerRevealTiming.FlyStart) / 0.25f);
                        if (motion)
                            scale *= 1f - 0.2f * Mathf.Clamp01((u - StickerRevealTiming.FlyStart) / 0.25f);
                    }
                    else if (motion)
                    {
                        float f = EaseInOut(fly);
                        position = Vector2.LerpUnclamped(_cardHome, _revealTarget, f);
                        scale *= Mathf.Lerp(1f, _reveal.Direct ? 0.62f : 0.22f, f);
                        if (!_reveal.Direct)
                            alpha = 1f - Mathf.Clamp01((fly - 0.8f) / 0.2f);
                    }
                    else
                    {
                        alpha = 1f - Mathf.Clamp01((u - (StickerRevealTiming.Paste - 0.3f)) / 0.3f);
                    }
                    if (_reveal.Full && motion && _reveal.Stars == 0)
                    {
                        // H-VFX-074: 6 round confetti and a ring where it lands.
                        confettiK = Mathf.Clamp01((u - 0.8f) / 0.1f);
                        showConfetti = u >= 0.78f;
                    }
                    if (rarity == StickerRarity.Shiny)
                        sheen = -1f;
                }
                v.card.anchoredPosition = position;
                v.card.localScale = new Vector3(scale * flip, scale, 1f);
                v.card.localEulerAngles = new Vector3(0f, 0f, angle);
                SetCardAlpha(alpha, faceDown);
                if (rarity == StickerRarity.Shiny && v.art.gameObject.activeSelf)
                    v.artUv.SetPhase(sheen >= 0f, sheen >= 0f ? sheen : 0f);
                // « Nouveau » during the paste; « Déjà collé : +n étoiles » for a duplicate; the page or album banner.
                bool pasting = u >= 0f && u < StickerRevealTiming.Paste - 0.05f;
                if (v.newPill != null)
                    SetActive(v.newPill.gameObject, pasting && _reveal.New);
                SetActive(v.duplicate.gameObject, u >= 0.05f && _reveal.Stars > 0);
                SetActive(v.banner.gameObject, u >= 0.05f && !string.IsNullOrEmpty(_reveal.Banner));
                ApplyStars(u);
            }
            else
            {
                if (v.newPill != null)
                    SetActive(v.newPill.gameObject, false);
                SetActive(v.duplicate.gameObject, false);
                SetActive(v.banner.gameObject, false);
                ApplyStars(-1f);
            }

            SetActive(v.ring.gameObject, showRing && motion);
            if (showRing && motion)
            {
                float s = Mathf.Lerp(0.4f, 1.5f, EaseOut(ringK));
                v.ring.rectTransform.localScale = new Vector3(s, s, 1f);
                Alpha(v.ring, 0.8f * (1f - ringK));
            }
            for (int i = 0; i < v.bubbles.Length; i++)
            {
                var bubble = v.bubbles[i];
                bool on = showBubbles && i < AlbumLayout.Bubbles;
                SetActive(bubble.gameObject, on);
                if (!on)
                    continue;
                float k = Mathf.Clamp01(bubbleK * 1.2f - i * 0.03f);
                var home = _bubbleHome[i];
                bubble.rectTransform.anchoredPosition = home + new Vector2(Mathf.Sin(k * 6f + i) * 14f, k * 260f);
                Alpha(bubble, 0.85f * (1f - k));
            }
            SetActive(v.landRing.gameObject, showConfetti);
            Vector2 land = _reveal.Direct ? _revealTarget : _revealTarget;
            if (showConfetti)
            {
                v.landRing.rectTransform.anchoredPosition = land;
                float s = Mathf.Lerp(0.5f, 1.4f, confettiK);
                v.landRing.rectTransform.localScale = new Vector3(s, s, 1f);
                Alpha(v.landRing, 0.7f * (1f - confettiK));
            }
            for (int i = 0; i < v.confetti.Length; i++)
            {
                var dot = v.confetti[i];
                bool on = showConfetti && i < AlbumLayout.Confetti;
                SetActive(dot.gameObject, on);
                if (!on)
                    continue;
                float a = i * (Mathf.PI * 2f / AlbumLayout.Confetti) + 0.3f;
                float r = 30f + 110f * EaseOut(confettiK);
                dot.rectTransform.anchoredPosition = land + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                dot.color = i % 3 == 0 ? _confetti0 : i % 3 == 1 ? _confetti1 : _confetti2;
                Alpha(dot, 1f - confettiK * 0.8f);
            }
        }

        /// <summary>A duplicate's 1 to 6 round stars: they pop around the card, then fly to the stars counter.</summary>
        private void ApplyStars(float u)
        {
            var stars = _parts.reveal.stars;
            int count = Mathf.Min(_reveal.Stars, stars.Length);
            for (int i = 0; i < stars.Length; i++)
            {
                bool on = u >= StickerRevealTiming.SquashEnd && i < count && u < StickerRevealTiming.Paste;
                SetActive(stars[i].gameObject, on);
                if (!on)
                    continue;
                float a = (i - (count - 1) * 0.5f) * 0.6f + Mathf.PI * 0.5f;
                var around = _cardHome + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 170f;
                float fly = Mathf.Clamp01((u - StickerRevealTiming.FlyStart - i * 0.04f) / 0.4f);
                var position = _reduceMotion ? around : Vector2.LerpUnclamped(around, _starsTarget, EaseInOut(fly));
                stars[i].rectTransform.anchoredPosition = position;
                float s = _reduceMotion ? 1f : Mathf.Lerp(1f, 0.6f, fly);
                stars[i].rectTransform.localScale = new Vector3(s, s, 1f);
                Alpha(stars[i], _reduceMotion ? 1f - fly : 1f);
            }
        }

        private void SetCardAlpha(float alpha, bool faceDown)
        {
            var v = _parts.reveal;
            SetActive(v.cardBack.gameObject, faceDown);
            Alpha(v.cardBack, alpha);
            Alpha(v.cardBackPaw, alpha);
            Alpha(v.frame, alpha);
            Alpha(v.inner, alpha);
            Alpha(v.art, alpha);
            Alpha(v.silhouette, alpha);
            Alpha(v.sign, alpha);
            Alpha(v.signLight, alpha);
            if (v.name != null)
                v.name.alpha = alpha;
            if (v.rarity != null)
                v.rarity.alpha = alpha;
        }

        // ---------------- Tick ----------------

        public void Tick(float deltaTime)
        {
            if (!_initialized)
                return;
            if (_screenShown)
            {
                TickPage(deltaTime);
                if (_noteT >= 0f)
                {
                    _noteT += deltaTime;
                    ApplyNote();
                }
            }
        }

        private void TickPage(float dt)
        {
            if (_mode != AlbumScreenMode.Page && _mode != AlbumScreenMode.ChoosePage)
                return;
            var swipe = _parts.swipe;
            if (swipe.Dragging)
            {
                _slideT = -1f;
                _springT = -1f;
                _dragOffset = Mathf.Clamp(swipe.Offset, -400f, 400f) * (_reduceMotion ? 0f : 0.85f);
                ApplyPanel(_dragOffset, 1f);
            }
            else if (_slideT >= 0f)
            {
                _slideT += dt;
                float k = Mathf.Clamp01(_slideT / AlbumLayout.PageSlideSeconds);
                ApplySlide(k);
                if (k >= 1f)
                    _slideT = -1f;
            }
            else if (_springT >= 0f)
            {
                _springT += dt;
                float k = Mathf.Clamp01(_springT / 0.15f);
                ApplyPanel(Mathf.Lerp(_springFrom, 0f, EaseOut(k)), 1f);
                if (k >= 1f)
                    _springT = -1f;
            }
            for (int i = 0; i < _pressT.Length; i++)
            {
                if (_pressT[i] < 0f)
                    continue;
                _pressT[i] += dt;
                float k = Mathf.Clamp01(_pressT[i] / PressSeconds);
                float s = k < 0.4f ? Mathf.Lerp(1f, 0.92f, k / 0.4f) : Mathf.Lerp(0.92f, 1f, EaseOut((k - 0.4f) / 0.6f));
                _parts.slots[i].root.localScale = new Vector3(s, s, 1f);
                if (k >= 1f)
                    _pressT[i] = -1f;
            }
        }

        private void OnSwipeReleased(float travel)
        {
            if (_mode != AlbumScreenMode.Page && _mode != AlbumScreenMode.ChoosePage)
                return;
            // Spring back unless the presenter turns the page (SlidePage cancels the spring).
            _springFrom = _dragOffset;
            _springT = 0f;
            _dragOffset = 0f;
            if (travel <= -AlbumLayout.SwipeThreshold)
                _actions?.NextPage();
            else if (travel >= AlbumLayout.SwipeThreshold)
                _actions?.PreviousPage();
        }

        private void ApplySlide(float k)
        {
            if (_reduceMotion)
            {
                ApplyPanel(0f, k);
                return;
            }
            float from = _slideDir * 320f;
            ApplyPanel(Mathf.Lerp(from, 0f, EaseOut(k)), Mathf.Lerp(0.2f, 1f, k));
        }

        private void ApplyPanel(float x, float alpha)
        {
            var panel = _parts.pagePanel;
            if (panel == null)
                return;
            var position = panel.anchoredPosition;
            if (Mathf.Abs(position.x - x) > 0.01f)
                panel.anchoredPosition = new Vector2(x, position.y);
            if (_parts.pageGroup != null && Mathf.Abs(_parts.pageGroup.alpha - alpha) > 0.001f)
                _parts.pageGroup.alpha = alpha;
        }

        private void ApplyNote()
        {
            float t = _noteT;
            float alpha;
            if (t < NoteIn)
                alpha = t / NoteIn;
            else if (t < NoteIn + NoteHold)
                alpha = 1f;
            else
                alpha = 1f - (t - NoteIn - NoteHold) / NoteOut;
            if (alpha <= 0f && t > NoteIn)
            {
                _noteT = -1f;
                _parts.notePill.gameObject.SetActive(false);
                return;
            }
            _parts.noteGroup.alpha = Mathf.Clamp01(alpha);
        }

        // ---------------- layout of the pooled lists ----------------

        private void LayoutGrid()
        {
            int shown = 0;
            for (int i = 0; i < _coverShown.Length; i++)
                if (_coverShown[i])
                {
                    var root = _parts.covers[i].root;
                    int column = shown % 2, row = shown / 2;
                    root.anchoredPosition = new Vector2((column - 0.5f) * CoverPitchX, -ListPadding - CoverPitchY * (row + 0.5f));
                    shown++;
                }
            int rows = (shown + 1) / 2;
            var content = _parts.shelfContent;
            content.sizeDelta = new Vector2(content.sizeDelta.x, rows * CoverPitchY + 2f * ListPadding);
        }

        private void LayoutRows()
        {
            int shown = 0;
            for (int i = 0; i < _rowShown.Length; i++)
                if (_rowShown[i])
                {
                    _parts.rows[i].root.anchoredPosition = new Vector2(0f, -ListPadding - RowPitch * (shown + 0.5f));
                    shown++;
                }
            var content = _parts.chooseContent;
            content.sizeDelta = new Vector2(content.sizeDelta.x, shown * RowPitch + 2f * ListPadding);
        }

        // ---------------- helpers ----------------

        private static float EaseOut(float k) => 1f - (1f - k) * (1f - k);

        private static float EaseInOut(float k) => k < 0.5f ? 2f * k * k : 1f - 2f * (1f - k) * (1f - k);

        private static float BackOut(float k)
        {
            const float c = 1.70158f;
            float x = k - 1f;
            return 1f + x * x * ((c + 1f) * x + c);
        }

        private static void Alpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
                return;
            var color = graphic.color;
            if (Mathf.Abs(color.a - alpha) < 0.002f)
                return;
            color.a = alpha;
            graphic.color = color;
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label == null)
                return;
            text ??= string.Empty;
            if (!string.Equals(label.text, text, StringComparison.Ordinal))
                label.text = text;
        }

        // ---------------- pipeline hooks ----------------

        /// <summary>Prefab builder hook (Editor AlbumScreenPipeline).</summary>
        public void SetParts(AlbumScreenParts parts, Material pearl, Material silhouette, StickerArtCatalog art)
        {
            _parts = parts;
            _pearl = pearl;
            _silhouette = silhouette;
            _art = art;
        }

        public void SetBorrowed(Image[] kitImages, string[] kitNames, TMP_Text[] labels, string[] labelFonts, string[] labelMaterials, string[] iconNames)
        {
            _kitImages = kitImages;
            _kitNames = kitNames;
            _labels = labels;
            _labelFonts = labelFonts;
            _labelMaterials = labelMaterials;
            _iconNames = iconNames;
        }

        public void SetColors(Color cream, Color card, Color cardLocked, Color text, Color textSoft, Color accent, Color disabled, Color frameCream,
            Color innerCream, Color rareDot, Color emptyFrame)
        {
            _cream = cream;
            _card = card;
            _cardLocked = cardLocked;
            _text = text;
            _textSoft = textSoft;
            _accent = accent;
            _disabled = disabled;
            _frameCream = frameCream;
            _innerCream = innerCream;
            _rareDot = rareDot;
            _emptyFrame = emptyFrame;
        }
    }
}
