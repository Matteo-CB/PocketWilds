using System;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_Memory (T41 « Memory des empreintes », doc 01 Contenu S-ACT-004; built by MemoryPipeline, Addressables group
    /// Activity_Memory): a screen-space canvas under the activity HUD. A soft cream tablecloth over the whole screen, and in
    /// the safe area under the HUD's top card a grid of 20 pooled card views (4 × 3, 4 × 4 or 5 × 4 shown), each a rounded
    /// card of about 3:4: the Prairie clover back, the cream face with the animal's portrait (its capture thumbnail,
    /// borrowed from the codex catalog the scene already holds, never copied into this bundle) or its pawprint (the species'
    /// paw tinted with the variant's coat over a soft stamp shadow); the glow card's gold edge, the double pair's
    /// « ×2 » badge (face up only: on the backs it would give the pair away), a matched card's soft mark. A flip squashes
    /// the card to nothing across and back with a small lift (0.22 s); a mismatch shakes gently; the glow card's pair pulses
    /// gold; challenge 6's cards glide to each other's place; a match sends a round glint on each card and a little heart
    /// rising (pooled). Reduce Motion: instant changes, no squash, shake, rise or glide. No raycaster: the stage hit tests
    /// the cards' rects itself, so touches over the cards never block the HUD. Per frame, only the cards in motion are
    /// written; nothing allocates after <see cref="Bind"/>.
    /// </summary>
    public sealed class MemoryScreenView : MonoBehaviour, IMemoryScreenView
    {
        /// <summary>One pooled card view (MemoryPipeline builds 20).</summary>
        [Serializable]
        public sealed class CardParts
        {
            /// <summary>At the card's place in the grid (its size is the card's).</summary>
            public RectTransform root;

            /// <summary>The card itself: squashed across in a flip, lifted, shaken, pulsed.</summary>
            public RectTransform body;

            public Image shadow;
            public Image back;
            public GameObject face;
            public Image faceCard;
            public Image portrait;
            /// <summary>The pawprint's soft stamp shadow (the same paw, warm brown, a little lower).</summary>
            public Image pawOutline;
            public Image paw;
            public Image mark;
            public Image glow;
            public Image badge;
        }

        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private RectTransform _safeArea;
        [SerializeField] private RectTransform _grid;
        [SerializeField] private RectTransform _effects;

        /// <summary>A plain cream under the cloth (drawn only when the cloth's sprite is missing: one full screen layer).</summary>
        [SerializeField] private Image _base;

        [SerializeField] private Image _cloth;
        [SerializeField] private CardParts[] _cards = Array.Empty<CardParts>();
        [SerializeField] private Image[] _glints = Array.Empty<Image>();
        [SerializeField] private Image[] _hearts = Array.Empty<Image>();

        [Header("Pawprints (one per species, agent MA)")]
        [SerializeField] private uint[] _pawSpecies = Array.Empty<uint>();
        [SerializeField] private Sprite[] _paws = Array.Empty<Sprite>();

        [Header("Coat colours (MemoryPipeline: each variant's palette primary)")]
        [SerializeField] private string[] _coatSources = Array.Empty<string>();
        [SerializeField] private uint[] _coatVariants = Array.Empty<uint>();
        [SerializeField] private Color[] _coats = Array.Empty<Color>();

        [Header("Effects: own sprites when drawn, else lent by the activity HUD")]
        [SerializeField] private Sprite _glintSprite;
        [SerializeField] private Sprite _heartSprite;
        [SerializeField] private Sprite _markSprite;
        [SerializeField] private string _heartIconName = "ICO_Favorite_Heart";
        [SerializeField] private string _markIconName = "ICO_Check";

        [Header("Colours")]
        [SerializeField] private Color _gold = new Color(0.949f, 0.710f, 0.267f, 1f);

        /// <summary>The gold edge's tint: the gold over a white sprite, white over one already drawn gold (MemoryPipeline looks).</summary>
        [SerializeField] private Color _glowTint = new Color(0.949f, 0.710f, 0.267f, 1f);
        [SerializeField] private Color _glintColor = new Color(0.949f, 0.710f, 0.267f, 1f);
        [SerializeField] private Color _heartColor = new Color(0.96f, 0.55f, 0.66f, 1f);
        [SerializeField] private Color _markColor = new Color(0.52f, 0.76f, 0.48f, 1f);
        [SerializeField] private Color _cream = new Color(0.99f, 0.96f, 0.90f, 1f);
        [SerializeField] private Color _backFallback = new Color(0.80f, 0.90f, 0.70f, 1f);

        private readonly SafeAreaFitter _fitter = new SafeAreaFitter();
        private CanvasScaler _scaler;
        private CodexVisualCatalog _catalog;
        private bool _catalogSearched;
        private Sprite _heart;
        private Sprite _mark;
        private Sprite _glint;
        private bool _initialized;
        private bool _reduceMotion;
        private bool _paused;
        private bool _shown;
        private float _time;

        // The grid.
        private int _columns = 4;
        private int _rows = 3;
        private int _used;
        private Vector2 _size = new Vector2(200f, 266f);
        private Vector2 _laidFor = new Vector2(-1f, -1f);

        // Per card.
        private uint[] _variant;
        private uint[] _speciesOf;
        private MemoryFace[] _faceKind;
        private bool[] _isGlow;
        private bool[] _isDouble;
        private bool[] _up;
        private bool[] _faceSide;
        private bool[] _matched;
        private bool[] _dirty;
        private float[] _flipT;
        private float[] _flipDelay;
        private float[] _markT;
        private float[] _shakeT;
        private float[] _pulseT;
        private float[] _pulseLength;
        private float[] _glideT;
        private float[] _glideLength;
        private float[] _glideArc;
        private Vector2[] _slot;
        private Vector2[] _glideFrom;
        private Color[] _coat;

        // Effects.
        private float[] _glintT;
        private float[] _heartT;
        private Vector2[] _glintAt;
        private Vector2[] _heartAt;

        // ---------------- IMemoryScreenView ----------------

        public bool Valid
        {
            get
            {
                if (_canvas == null || _content == null || _grid == null || _effects == null || _cards == null || _cards.Length < MemoryLayout.CardCapacity)
                    return false;
                for (int i = 0; i < _cards.Length; i++)
                {
                    var c = _cards[i];
                    if (c == null || c.root == null || c.body == null || c.back == null || c.face == null || c.faceCard == null || c.portrait == null
                        || c.paw == null || c.pawOutline == null || c.mark == null || c.glow == null || c.badge == null || c.shadow == null)
                        return false;
                }
                return true;
            }
        }

        public int Capacity => _cards.Length;

        public bool Shown => _shown;

        public bool ReduceMotion
        {
            get => _reduceMotion;
            set => _reduceMotion = value;
        }

        public void Bind(IActivityHudView hud)
        {
            Initialize();
            if (_catalog == null)
                _catalogSearched = false;
            EnsureCatalog();
            // A heart and a mark: the screen's own when drawn, else the HUD's lent icons (the HUD's atlas is the scene's:
            // borrowed, never copied into this bundle), else the heart stands in for the mark.
            _heart = _heartSprite != null ? _heartSprite : hud?.Icon(_heartIconName);
            _mark = _markSprite != null ? _markSprite : hud?.Icon(_markIconName);
            if (_mark == null)
                _mark = _heart;
            _glint = _glintSprite;
            ApplyEffectSprites();
            if (_heart == null || _glint == null)
                GameLog.Warning(LogCategory.Presentation, "Memory: " + (_glint == null ? "no glint sprite (UI_Memory_Glint)" : "")
                    + (_glint == null && _heart == null ? " and " : "") + (_heart == null ? "no heart (UI_Memory_Heart, or " + _heartIconName + " lent by the activity HUD)" : "")
                    + ": the match effect shows without it.");
        }

        /// <summary>Tests and captures: the portraits' catalog (else the one the scene loaded).</summary>
        public void SetPortraits(CodexVisualCatalog catalog)
        {
            _catalog = catalog;
            _catalogSearched = catalog != null;
        }

        /// <summary>Captures: the sprites the activity HUD lends at run time (a heart, a mark), when this HUD does not carry them.</summary>
        public void SetBorrowedSprites(Sprite heart, Sprite mark)
        {
            Initialize();
            if (_heart == null)
                _heart = heart;
            if (_mark == null || _mark == _heart && mark != null)
                _mark = mark != null ? mark : _heart;
            ApplyEffectSprites();
        }

        public bool TryCoat(uint variant, out Color coat)
        {
            int n = Mathf.Min(_coatVariants.Length, _coats.Length);
            for (int i = 0; i < n; i++)
            {
                if (_coatVariants[i] == variant)
                {
                    coat = _coats[i];
                    return true;
                }
            }
            coat = default;
            return false;
        }

        public bool HasPortrait(uint variant)
        {
            EnsureCatalog();
            return _catalog != null && _catalog.TryGetVariant(new ContentId(variant), out var sprite) && sprite != null;
        }

        public void Show()
        {
            Initialize();
            _content.SetActive(true);
            _canvas.enabled = true;
            _shown = true;
            // The scaler measures the screen now (its OnEnable), so the first frame is laid out at its true size.
            if (_scaler != null)
            {
                _scaler.enabled = false;
                _scaler.enabled = true;
            }
            _fitter.Fit(_safeArea);
            bool cloth = _cloth != null && _cloth.sprite != null;
            if (_cloth != null)
                _cloth.enabled = cloth;
            if (_base != null)
                _base.enabled = !cloth;
        }

        public void Hide()
        {
            Initialize();
            _shown = false;
            _paused = false;
            for (int i = 0; i < _glints.Length; i++)
                StopGlint(i);
            for (int i = 0; i < _hearts.Length; i++)
                StopHeart(i);
            if (_canvas != null)
                _canvas.enabled = false;
            if (_content != null)
                _content.SetActive(false);
        }

        public void Layout(int columns, int rows)
        {
            Initialize();
            _columns = Mathf.Max(1, columns);
            _rows = Mathf.Max(1, rows);
            _used = Mathf.Min(_columns * _rows, _cards.Length);
            // A catalog not found at the last game is looked for again (once per game, never per frame).
            if (_catalog == null)
                _catalogSearched = false;
            for (int i = 0; i < _cards.Length; i++)
            {
                bool used = i < _used;
                if (_cards[i].root.gameObject.activeSelf != used)
                    _cards[i].root.gameObject.SetActive(used);
            }
            Relayout();
        }

        public void Deal(int card, uint variant, uint species, MemoryFace face, bool glow, bool isDouble, Color coat)
        {
            Initialize();
            if ((uint)card >= (uint)_cards.Length)
                return;
            var c = _cards[card];
            _variant[card] = variant;
            _speciesOf[card] = species;
            _faceKind[card] = face;
            _isGlow[card] = glow;
            _isDouble[card] = isDouble;
            _coat[card] = coat;
            _up[card] = false;
            _faceSide[card] = false;
            _matched[card] = false;
            _flipT[card] = -1f;
            _flipDelay[card] = 0f;
            _markT[card] = -1f;
            _shakeT[card] = -1f;
            _pulseT[card] = -1f;
            _glideT[card] = -1f;

            // The face: the portrait (borrowed), or the species' pawprint in the coat colour.
            Sprite portrait = null;
            Sprite paw = PawOf(species);
            EnsureCatalog();
            if (_catalog != null)
            {
                if (!_catalog.TryGetVariant(new ContentId(variant), out portrait) || portrait == null)
                    _catalog.TryGetSilhouette(new ContentId(species), out portrait);
            }
            bool showPaw = face == MemoryFace.Pawprint && paw != null || portrait == null && paw != null;
            c.portrait.sprite = showPaw ? null : portrait;
            c.portrait.enabled = !showPaw && portrait != null;
            c.paw.sprite = showPaw ? paw : null;
            c.pawOutline.sprite = showPaw ? paw : null;
            c.paw.enabled = showPaw;
            c.pawOutline.enabled = showPaw;
            ApplyColors(card, 1f);
            c.badge.enabled = isDouble && c.badge.sprite != null;
            c.mark.enabled = false;
            c.mark.rectTransform.localScale = Vector3.one;
            ApplySide(card, false);
            ApplyGlow(card, 0f);
            _dirty[card] = true;
            Rest(card);
        }

        public void SetUp(int card, bool up, bool instant, float delay)
        {
            if ((uint)card >= (uint)_used || _up[card] == up)
                return;
            _up[card] = up;
            if (instant || _reduceMotion)
            {
                _flipT[card] = -1f;
                ApplySide(card, up);
                _dirty[card] = true;
                return;
            }
            if (_flipT[card] < 0f)
            {
                _flipT[card] = 0f;
                _flipDelay[card] = Mathf.Max(0f, delay);
            }
            // A flip already playing turns to the new side at its half way (or plays once more at its end).
            _dirty[card] = true;
        }

        public void SetMatched(int card, bool matched, bool instant)
        {
            if ((uint)card >= (uint)_used || _matched[card] == matched)
                return;
            _matched[card] = matched;
            var c = _cards[card];
            if (matched)
            {
                _markT[card] = instant || _reduceMotion ? MemoryLayout.MarkPopSeconds : 0f;
                c.mark.enabled = _mark != null && _faceSide[card];
                ApplyGlow(card, _pulseT[card] >= 0f ? 1f : 0f);
            }
            else
            {
                _markT[card] = -1f;
                c.mark.enabled = false;
                ApplyColors(card, 1f);
            }
            _dirty[card] = true;
        }

        public void Shake(int card)
        {
            if ((uint)card >= (uint)_used || _reduceMotion)
                return;
            _shakeT[card] = 0f;
            _dirty[card] = true;
        }

        public void Pulse(int card, float seconds)
        {
            if ((uint)card >= (uint)_used)
                return;
            _pulseT[card] = 0f;
            _pulseLength[card] = Mathf.Max(0.1f, seconds);
            _dirty[card] = true;
        }

        public void GlideFrom(int card, int fromCard, float seconds)
        {
            if ((uint)card >= (uint)_used || (uint)fromCard >= (uint)_used)
                return;
            if (_reduceMotion || seconds <= 0f)
            {
                _glideT[card] = -1f;
                _dirty[card] = true;
                return;
            }
            _glideFrom[card] = _slot[fromCard];
            // Each card arcs to the left of its own way: the two ways are opposite, so they pass each other on either side.
            _glideArc[card] = 1f;
            _glideT[card] = 0f;
            _glideLength[card] = seconds;
            _dirty[card] = true;
        }

        public void Match(int card, int other)
        {
            if ((uint)card >= (uint)_used || (uint)other >= (uint)_used)
                return;
            if (_glint != null)
            {
                StartGlint(_slot[card]);
                StartGlint(_slot[other]);
            }
            if (_heart != null)
                StartHeart(_slot[other] + new Vector2(0f, _size.y * 0.3f));
        }

        public int HitTest(Vector2 screen)
        {
            if (!_shown || _grid == null)
                return -1;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_grid, screen, UiCamera(), out var local))
                return -1;
            // Cards hang from the grid's centre.
            var rect = _grid.rect;
            local -= rect.center;
            float halfX = _size.x * 0.5f, halfY = _size.y * 0.5f;
            for (int i = 0; i < _used; i++)
            {
                var d = local - _slot[i];
                if (d.x >= -halfX && d.x <= halfX && d.y >= -halfY && d.y <= halfY)
                    return i;
            }
            return -1;
        }

        public Vector2 ScreenPoint(int card)
        {
            if ((uint)card >= (uint)_cards.Length)
                return Vector2.zero;
            return RectTransformUtility.WorldToScreenPoint(UiCamera(), _cards[card].root.position);
        }

        public void SetPaused(bool paused) => _paused = paused;

        public void Tick(float deltaTime)
        {
            if (!_shown || !_initialized)
                return;
            float dt = _paused ? 0f : Mathf.Max(0f, deltaTime);
            _time += dt;
            _fitter.Fit(_safeArea);
            var area = _grid.rect.size;
            if (area != _laidFor)
                Relayout();
            for (int i = 0; i < _used; i++)
                AnimateCard(i, dt);
            for (int i = 0; i < _glints.Length; i++)
                AnimateGlint(i, dt);
            for (int i = 0; i < _hearts.Length; i++)
                AnimateHeart(i, dt);
        }

        // ---------------- read by tests and captures ----------------

        public bool FaceShown(int card) => (uint)card < (uint)_used && _faceSide[card];

        public bool Animating(int card) =>
            (uint)card < (uint)_used && (_flipT[card] >= 0f || _shakeT[card] >= 0f || _pulseT[card] >= 0f || _glideT[card] >= 0f);

        public bool Pulsing(int card) => (uint)card < (uint)_used && _pulseT[card] >= 0f;

        public Sprite PortraitShown(int card) => (uint)card < (uint)_cards.Length && _cards[card].portrait.enabled ? _cards[card].portrait.sprite : null;

        public Color PawTint(int card) => (uint)card < (uint)_cards.Length && _cards[card].paw.enabled ? _cards[card].paw.color : Color.clear;

        public int GlintsShown => CountOn(_glintT);

        public int HeartsShown => CountOn(_heartT);

        public Canvas Canvas => _canvas;
        public RectTransform Grid => _grid;
        public Vector2 CardSize => _size;
        public CodexVisualCatalog Portraits => _catalog;
        public CardParts Card(int card) => _cards[card];
        public bool Matched(int card) => (uint)card < (uint)_used && _matched[card];
        public bool MarkShown(int card) => (uint)card < (uint)_cards.Length && _cards[card].mark.enabled;
        public bool BadgeShown(int card) => (uint)card < (uint)_cards.Length && _cards[card].badge.enabled && _cards[card].badge.gameObject.activeInHierarchy;
        public bool GlowEdgeShown(int card) => (uint)card < (uint)_cards.Length && _cards[card].glow.enabled;
        public int CoatCount => _coatVariants.Length;
        public int PawCount => _paws.Length;
        public Sprite HeartSprite => _heart;
        public Sprite MarkSprite => _mark;
        public Sprite GlintSprite => _glint;

        /// <summary>The card's centre and size in the canvas's units (captures check the grid stays clear of the HUD).</summary>
        public Rect CardRectInSafeArea(int card)
        {
            var corners = new Vector3[4];
            _cards[card].root.GetWorldCorners(corners);
            var min = _safeArea.InverseTransformPoint(corners[0]);
            var max = _safeArea.InverseTransformPoint(corners[2]);
            var safe = _safeArea.rect;
            return Rect.MinMaxRect(min.x - safe.xMin, min.y - safe.yMin, max.x - safe.xMin, max.y - safe.yMin);
        }

        // ---------------- builder hooks (MemoryPipeline) ----------------

        public void SetReferences(Canvas canvas, GameObject content, RectTransform safeArea, RectTransform grid, RectTransform effects, Image baseImage,
            Image cloth, CardParts[] cards, Image[] glints, Image[] hearts)
        {
            _canvas = canvas;
            _content = content;
            _safeArea = safeArea;
            _grid = grid;
            _effects = effects;
            _base = baseImage;
            _cloth = cloth;
            _cards = cards;
            _glints = glints;
            _hearts = hearts;
        }

        public void SetArt(uint[] pawSpecies, Sprite[] paws, Sprite glint, Sprite heart, Sprite mark)
        {
            _pawSpecies = pawSpecies;
            _paws = paws;
            _glintSprite = glint;
            _heartSprite = heart;
            _markSprite = mark;
        }

        public void SetCoats(string[] sources, uint[] variants, Color[] coats)
        {
            _coatSources = sources;
            _coatVariants = variants;
            _coats = coats;
        }

        public void SetColors(Color gold, Color glowTint, Color glint, Color heart, Color mark, Color cream, Color backFallback)
        {
            _gold = gold;
            _glowTint = glowTint;
            _glintColor = glint;
            _heartColor = heart;
            _markColor = mark;
            _cream = cream;
            _backFallback = backFallback;
        }

        // ---------------- internals ----------------

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _scaler = GetComponent<CanvasScaler>();
            int n = _cards.Length;
            _variant = new uint[n];
            _speciesOf = new uint[n];
            _faceKind = new MemoryFace[n];
            _isGlow = new bool[n];
            _isDouble = new bool[n];
            _up = new bool[n];
            _faceSide = new bool[n];
            _matched = new bool[n];
            _dirty = new bool[n];
            _flipT = new float[n];
            _flipDelay = new float[n];
            _markT = new float[n];
            _shakeT = new float[n];
            _pulseT = new float[n];
            _pulseLength = new float[n];
            _glideT = new float[n];
            _glideLength = new float[n];
            _glideArc = new float[n];
            _slot = new Vector2[n];
            _glideFrom = new Vector2[n];
            _coat = new Color[n];
            for (int i = 0; i < n; i++)
            {
                _flipT[i] = _markT[i] = _shakeT[i] = _pulseT[i] = _glideT[i] = -1f;
                _coat[i] = Color.white;
            }
            _glintT = new float[_glints.Length];
            _glintAt = new Vector2[_glints.Length];
            _heartT = new float[_hearts.Length];
            _heartAt = new Vector2[_hearts.Length];
            for (int i = 0; i < _glints.Length; i++)
                StopGlint(i);
            for (int i = 0; i < _hearts.Length; i++)
                StopHeart(i);
            // The backs and faces keep their look when a sprite is missing (a soft green back, a cream face).
            for (int i = 0; i < n; i++)
            {
                var c = _cards[i];
                if (c == null)
                    continue;
                if (c.back != null && c.back.sprite == null)
                    c.back.color = _backFallback;
                if (c.faceCard != null && c.faceCard.sprite == null)
                    c.faceCard.color = _cream;
            }
        }

        /// <summary>The codex thumbnails the scene already loaded (DEF_CodexVisuals, referenced by the codex and the sheets).</summary>
        private void EnsureCatalog()
        {
            if (_catalog != null || _catalogSearched)
                return;
            _catalogSearched = true;
            var found = Resources.FindObjectsOfTypeAll<CodexVisualCatalog>();
            int best = -1;
            for (int i = 0; i < found.Length; i++)
            {
                int count = found[i].variants != null ? found[i].variants.Count : 0;
                if (count > best)
                {
                    best = count;
                    _catalog = found[i];
                }
            }
            if (_catalog == null)
                GameLog.Warning(LogCategory.Presentation, "Memory: no codex catalog loaded (DEF_CodexVisuals): the cards show pawprints only.");
        }

        private Sprite PawOf(uint species)
        {
            int n = Mathf.Min(_pawSpecies.Length, _paws.Length);
            for (int i = 0; i < n; i++)
                if (_pawSpecies[i] == species && _paws[i] != null)
                    return _paws[i];
            // An unknown species borrows the first paw drawn (its coat still tells it apart).
            for (int i = 0; i < _paws.Length; i++)
                if (_paws[i] != null)
                    return _paws[i];
            return null;
        }

        private void ApplyEffectSprites()
        {
            for (int i = 0; i < _glints.Length; i++)
            {
                _glints[i].sprite = _glint;
                _glints[i].color = _glintColor;
            }
            for (int i = 0; i < _hearts.Length; i++)
            {
                _hearts[i].sprite = _heart;
                _hearts[i].color = _heartColor;
            }
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null || _cards[i].mark == null)
                    continue;
                _cards[i].mark.sprite = _mark;
                _cards[i].mark.color = _mark == _heart ? _heartColor : _markColor;
                if (_mark == null)
                    _cards[i].mark.enabled = false;
            }
        }

        private Camera UiCamera() => _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

        /// <summary>The cards' size and places for the grid area now (the safe area under the HUD's top card).</summary>
        private void Relayout()
        {
            var area = _grid.rect.size;
            _laidFor = area;
            // A portrait screen lays a wide grid on its side (4 × 3 as 3 columns of 4) when its cards come out larger.
            var upright = MemoryLayout.Fit(_columns, _rows, area.x, area.y);
            var turned = MemoryLayout.Fit(_rows, _columns, area.x, area.y);
            bool turn = _columns > _rows && turned.x > upright.x + 0.5f;
            _size = turn ? turned : upright;
            for (int i = 0; i < _used; i++)
            {
                int slot = turn ? (i % _columns) * _rows + i / _columns : i;
                _slot[i] = turn ? MemoryLayout.SlotCenter(slot, _rows, _columns, _size) : MemoryLayout.SlotCenter(slot, _columns, _rows, _size);
                var c = _cards[i];
                c.root.sizeDelta = _size;
                float outset = MemoryLayout.GlowOutset;
                c.glow.rectTransform.offsetMin = new Vector2(-outset, -outset);
                c.glow.rectTransform.offsetMax = new Vector2(outset, outset);
                float inset = _size.x * MemoryLayout.PortraitInset;
                c.portrait.rectTransform.offsetMin = new Vector2(inset, inset);
                c.portrait.rectTransform.offsetMax = new Vector2(-inset, -inset);
                float paw = _size.x * MemoryLayout.PawSize;
                c.paw.rectTransform.sizeDelta = new Vector2(paw, paw);
                c.pawOutline.rectTransform.sizeDelta = new Vector2(paw, paw);
                c.pawOutline.rectTransform.anchoredPosition = new Vector2(0f, -paw * MemoryLayout.PawShadowDrop);
                float badge = _size.x * MemoryLayout.BadgeSize;
                c.badge.rectTransform.sizeDelta = new Vector2(badge, badge);
                c.badge.rectTransform.anchoredPosition = MemoryLayout.BadgeAt(badge);
                float mark = _size.x * MemoryLayout.MarkSize;
                c.mark.rectTransform.sizeDelta = new Vector2(mark, mark);
                c.mark.rectTransform.anchoredPosition = MemoryLayout.MarkAt(mark);
                c.shadow.rectTransform.offsetMin = new Vector2(2f, -9f);
                c.shadow.rectTransform.offsetMax = new Vector2(-2f, -3f);
                _dirty[i] = true;
                Rest(i);
            }
            for (int i = 0; i < _glints.Length; i++)
                _glints[i].rectTransform.sizeDelta = new Vector2(_size.x * 0.55f, _size.x * 0.55f);
            for (int i = 0; i < _hearts.Length; i++)
                _hearts[i].rectTransform.sizeDelta = new Vector2(_size.x * 0.36f, _size.x * 0.36f);
        }

        private void ApplySide(int card, bool faceUp)
        {
            _faceSide[card] = faceUp;
            var c = _cards[card];
            if (c.back.gameObject.activeSelf == faceUp)
                c.back.gameObject.SetActive(!faceUp);
            if (c.face.activeSelf != faceUp)
                c.face.SetActive(faceUp);
            bool badge = faceUp && _isDouble[card] && c.badge.sprite != null;
            if (c.badge.enabled != badge)
                c.badge.enabled = badge;
            bool mark = faceUp && _matched[card] && _mark != null;
            if (c.mark.enabled != mark)
                c.mark.enabled = mark;
        }

        /// <summary>The gold edge: always on the glow card until it is matched, pulsing on the glow card's pair.</summary>
        private void ApplyGlow(int card, float pulse)
        {
            var c = _cards[card];
            bool steady = _isGlow[card] && !_matched[card];
            bool on = c.glow.sprite != null && (steady || pulse > 0f);
            if (c.glow.enabled != on)
                c.glow.enabled = on;
            if (!on)
                return;
            float alpha = pulse > 0f ? Mathf.Max(steady ? 1f : 0f, pulse) : 1f;
            var color = _glowTint;
            color.a = alpha;
            c.glow.color = color;
        }

        private void ApplyColors(int card, float dim)
        {
            var c = _cards[card];
            var shade = new Color(dim, dim, dim, 1f);
            c.faceCard.color = c.faceCard.sprite != null ? shade : _cream * shade;
            c.portrait.color = shade;
            var coat = _coat[card];
            c.paw.color = new Color(coat.r * dim, coat.g * dim, coat.b * dim, 1f);
            c.pawOutline.color = MemoryLayout.PawShadowColor(coat);
        }

        private void AnimateCard(int i, float dt)
        {
            bool flipping = _flipT[i] >= 0f, shaking = _shakeT[i] >= 0f, pulsing = _pulseT[i] >= 0f, gliding = _glideT[i] >= 0f;
            bool popping = _markT[i] >= 0f && _markT[i] < MemoryLayout.MarkPopSeconds;
            if (!flipping && !shaking && !pulsing && !gliding && !popping && !_dirty[i])
                return;
            _dirty[i] = false;
            var c = _cards[i];
            float scaleX = 1f, lift = 0f, shakeX = 0f, grow = 1f;
            var place = _slot[i];

            if (flipping)
            {
                if (_flipDelay[i] > 0f)
                {
                    _flipDelay[i] -= dt;
                }
                else
                {
                    _flipT[i] += dt;
                    float t = Mathf.Clamp01(_flipT[i] / MemoryLayout.FlipSeconds);
                    if (t >= 0.5f && _faceSide[i] != _up[i])
                        ApplySide(i, _up[i]);
                    // Squash across to nothing and back (ease in, ease out), lifted a little at the middle.
                    float half = t < 0.5f ? 1f - t * 2f : t * 2f - 1f;
                    scaleX = Mathf.Max(0.001f, half * half * (3f - 2f * half));
                    lift = Mathf.Sin(t * Mathf.PI) * MemoryLayout.FlipLift;
                    if (t >= 1f)
                    {
                        scaleX = 1f;
                        lift = 0f;
                        _flipT[i] = _faceSide[i] != _up[i] ? 0f : -1f;
                    }
                }
            }

            if (shaking)
            {
                _shakeT[i] += dt;
                float u = _shakeT[i] / MemoryLayout.ShakeSeconds;
                if (u >= 1f)
                    _shakeT[i] = -1f;
                else
                    shakeX = Mathf.Sin(u * Mathf.PI * 6f) * (1f - u) * MemoryLayout.ShakeAmplitude;
            }

            if (pulsing)
            {
                _pulseT[i] += dt;
                float u = _pulseT[i] / _pulseLength[i];
                if (u >= 1f)
                {
                    _pulseT[i] = -1f;
                    ApplyGlow(i, 0f);
                }
                else
                {
                    // Two soft beats of gold in the second, fading in and out at the ends.
                    float envelope = Mathf.Clamp01(u * 6f) * Mathf.Clamp01((1f - u) * 6f);
                    float beat = 0.55f + 0.45f * Mathf.Sin(_pulseT[i] * Mathf.PI * 4f - Mathf.PI * 0.5f);
                    ApplyGlow(i, Mathf.Max(0.05f, envelope * (_reduceMotion ? 1f : beat)));
                    if (!_reduceMotion)
                        grow = 1f + 0.035f * envelope * beat;
                }
            }

            if (gliding)
            {
                _glideT[i] += dt;
                float u = Mathf.Clamp01(_glideT[i] / _glideLength[i]);
                float s = u * u * (3f - 2f * u);
                place = Vector2.LerpUnclamped(_glideFrom[i], _slot[i], s);
                var way = _slot[i] - _glideFrom[i];
                var side = new Vector2(-way.y, way.x).normalized;
                place += side * (_glideArc[i] * Mathf.Sin(u * Mathf.PI) * Mathf.Min(way.magnitude * 0.3f, _size.y * 0.55f));
                grow *= 1f + 0.06f * Mathf.Sin(u * Mathf.PI);
                if (u >= 1f)
                {
                    _glideT[i] = -1f;
                    place = _slot[i];
                }
            }

            if (_markT[i] >= 0f)
            {
                if (popping)
                    _markT[i] += dt;
                float u = Mathf.Clamp01(_markT[i] / MemoryLayout.MarkPopSeconds);
                // A soft pop of the mark (to 115 % and back), then the card dims very slightly.
                float pop = u < 0.6f ? Mathf.Lerp(0.3f, 1.15f, u / 0.6f) : Mathf.Lerp(1.15f, 1f, (u - 0.6f) / 0.4f);
                c.mark.rectTransform.localScale = new Vector3(pop, pop, 1f);
                ApplyColors(i, Mathf.Lerp(1f, MemoryLayout.MatchedDim, u));
                if (u >= 1f)
                    _markT[i] = MemoryLayout.MarkPopSeconds;
            }

            c.root.anchoredPosition = place;
            c.body.localScale = new Vector3(scaleX * grow, grow, 1f);
            c.body.anchoredPosition = new Vector2(shakeX, lift);
        }

        /// <summary>The card at rest at its place (no squash, lift, shake or glide).</summary>
        private void Rest(int i)
        {
            var c = _cards[i];
            c.root.anchoredPosition = _glideT[i] >= 0f ? _glideFrom[i] : _slot[i];
            c.body.localScale = Vector3.one;
            c.body.anchoredPosition = Vector2.zero;
        }

        private void StartGlint(Vector2 at)
        {
            int slot = FreeSlot(_glintT);
            _glintT[slot] = 0f;
            _glintAt[slot] = at;
            var image = _glints[slot];
            image.rectTransform.anchoredPosition = at;
            image.enabled = true;
            AnimateGlint(slot, 0f);
        }

        private void StartHeart(Vector2 at)
        {
            int slot = FreeSlot(_heartT);
            _heartT[slot] = 0f;
            _heartAt[slot] = at;
            var image = _hearts[slot];
            image.rectTransform.anchoredPosition = at;
            image.enabled = true;
            AnimateHeart(slot, 0f);
        }

        private void AnimateGlint(int i, float dt)
        {
            if (_glintT[i] < 0f)
                return;
            _glintT[i] += dt;
            float t = _glintT[i] / MemoryLayout.GlintSeconds;
            if (t >= 1f)
            {
                StopGlint(i);
                return;
            }
            var image = _glints[i];
            float scale = _reduceMotion ? 1f : Mathf.Lerp(0.35f, 1.25f, 1f - (1f - t) * (1f - t));
            image.rectTransform.localScale = new Vector3(scale, scale, 1f);
            var color = _glintColor;
            color.a = t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f;
            image.color = color;
        }

        private void AnimateHeart(int i, float dt)
        {
            if (_heartT[i] < 0f)
                return;
            _heartT[i] += dt;
            float t = _heartT[i] / MemoryLayout.HeartSeconds;
            if (t >= 1f)
            {
                StopHeart(i);
                return;
            }
            var image = _hearts[i];
            float rise = _reduceMotion ? 0f : MemoryLayout.HeartRise * (1f - (1f - t) * (1f - t));
            image.rectTransform.anchoredPosition = _heartAt[i] + new Vector2(0f, rise);
            float pop = _reduceMotion ? 1f : t < 0.25f ? Mathf.Lerp(0.5f, 1.08f, t / 0.25f) : Mathf.Lerp(1.08f, 1f, Mathf.Clamp01((t - 0.25f) / 0.2f));
            image.rectTransform.localScale = new Vector3(pop, pop, 1f);
            var color = _heartColor;
            color.a = t < 0.6f ? Mathf.Clamp01(t / 0.12f) : 1f - (t - 0.6f) / 0.4f;
            image.color = color;
        }

        private void StopGlint(int i)
        {
            _glintT[i] = -1f;
            if (_glints[i] != null)
                _glints[i].enabled = false;
        }

        private void StopHeart(int i)
        {
            _heartT[i] = -1f;
            if (_hearts[i] != null)
                _hearts[i].enabled = false;
        }

        /// <summary>A free pooled effect, else the oldest one (reused).</summary>
        private static int FreeSlot(float[] times)
        {
            int oldest = 0;
            float age = -1f;
            for (int i = 0; i < times.Length; i++)
            {
                if (times[i] < 0f)
                    return i;
                if (times[i] > age)
                {
                    age = times[i];
                    oldest = i;
                }
            }
            return oldest;
        }

        private static int CountOn(float[] times)
        {
            int count = 0;
            if (times == null)
                return 0;
            for (int i = 0; i < times.Length; i++)
                if (times[i] >= 0f)
                    count++;
            return count;
        }
    }
}
