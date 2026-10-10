using System;
using System.Collections.Generic;
using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_Journal (T44C « Journal du refuge », doc « 01 Succès, titres, cadres, poses et journal » section 6, screen
    /// I-SCR-037; built by JournalScreenPipeline, Addressables group Journal). A screen-space canvas over the profile screen:
    /// the title and close, two tabs (the memories, « Moments »). The timeline recycles 6 page cards and 4 month headers at
    /// their own pitches over every page, newest first: each card shows its date, its picture at 300 (the stored capture or
    /// postcard in a pooled texture, else the animal's codex portrait or the system's icon on a soft disc), the memory with its
    /// variables (from a char buffer), the small icon of its system and « Nouveau ». « Moments » recycles 12 rows over the
    /// surprises seen, with its empty state. The UI kit's sprites, the icons, the codex portraits and the fonts are borrowed
    /// from what the scene loaded. Nothing allocates per frame after <see cref="Bind"/>; no cell is instantiated at run time.
    /// </summary>
    public sealed class JournalScreenView : MonoBehaviour, IJournalScreenView
    {
        /// <summary>The icons, borrowed by name: the paw, the moment, then one per <see cref="JournalSystem"/>.</summary>
        public enum JournalIcon
        {
            Paw = 0,
            Moment = 1,
            System0 = 2,
        }

        public const int SystemCount = (int)JournalSystem.Surprise + 1;
        public const int IconCount = (int)JournalIcon.System0 + SystemCount;

        /// <summary>The picture's sizes: a stored picture or a portrait fills its square, an icon sits smaller on its disc.</summary>
        public const float PortraitSize = 276f;

        public const float IconSize = 150f;

        [SerializeField] private JournalScreenParts _parts = new JournalScreenParts();

        [Header("Borrowed at run time from the scene (never in the bundle)")]
        [SerializeField] private Image[] _kitImages = Array.Empty<Image>();
        [SerializeField] private string[] _kitNames = Array.Empty<string>();
        [SerializeField] private TMP_Text[] _labels = Array.Empty<TMP_Text>();
        [SerializeField] private string[] _labelFonts = Array.Empty<string>();
        [SerializeField] private string[] _labelMaterials = Array.Empty<string>();
        [SerializeField] private string[] _iconNames = Array.Empty<string>();

        [Header("Colours")]
        [SerializeField] private Color _card = new Color(1f, 0.99f, 0.96f, 1f);
        [SerializeField] private Color _cardNew = new Color(1f, 0.95f, 0.86f, 1f);
        [SerializeField] private Color _text = new Color(0.29f, 0.23f, 0.17f, 1f);
        [SerializeField] private Color _textSoft = new Color(0.54f, 0.46f, 0.38f, 1f);
        [SerializeField] private Color _accent = new Color(1f, 0.69f, 0.37f, 1f);
        [SerializeField] private Color _disc = new Color(0.98f, 0.95f, 0.89f, 1f);

        /// <summary>The soft discs behind a system's icon (one per <see cref="JournalSystem"/>, wrapping).</summary>
        [SerializeField] private Color[] _systemTints =
        {
            new Color(0.93f, 0.95f, 0.86f, 1f), new Color(0.99f, 0.93f, 0.84f, 1f), new Color(0.90f, 0.94f, 0.97f, 1f),
            new Color(0.88f, 0.95f, 0.88f, 1f), new Color(0.99f, 0.91f, 0.88f, 1f), new Color(0.95f, 0.92f, 0.86f, 1f),
        };

        private readonly SafeAreaFitter _fitter = new SafeAreaFitter();
        private IJournalScreenActions _actions;
        private CanvasScaler _scaler;
        private Sprite[] _icons;
        private bool _lent;
        private bool _initialized;
        private bool _shown;
        private bool _reduceMotion;
        private JournalTab _tab;
        private CodexVisualCatalog _codex;
        private bool _codexSearched;

        // Labels from char buffers.
        private UiLabel[] _dateLabels;
        private UiLabel[] _textLabels;
        private UiLabel[] _headerLabels;

        // The timeline: its kinds (the presenter's array), tops, and which item each pooled cell shows.
        private bool[] _kinds = Array.Empty<bool>();
        private int _count;
        private float[] _tops = new float[32];
        private float _height;
        private int[] _cardItem;
        private int[] _headerItem;
        private float _lastY = float.NaN;

        // The moments: a uniform recycled list.
        private int _momentCount;
        private int _momentFirst = -1;
        private int[] _momentItem;

        // ---------------- IJournalScreenView ----------------

        public bool Valid
        {
            get
            {
                var p = _parts;
                if (p == null || p.canvas == null || p.content == null || p.safeArea == null || p.title == null || p.close == null || p.pagesPage == null
                    || p.timelineScroll == null || p.timelineContent == null || p.empty == null || p.momentsPage == null || p.momentScroll == null
                    || p.momentContent == null || p.momentsEmpty == null)
                    return false;
                if (p.tabs.Length < 2 || p.cards.Length < JournalLayout.CardCells || p.headers.Length < JournalLayout.HeaderCells
                    || p.moments.Length < JournalLayout.MomentCells)
                    return false;
                foreach (var t in p.tabs)
                    if (t == null || t.button == null || t.back == null || t.label == null)
                        return false;
                foreach (var c in p.cards)
                    if (c == null || c.root == null || c.card == null || c.pictureBack == null || c.picture == null || c.thumb == null || c.date == null
                        || c.text == null || c.systemIcon == null || c.newPill == null)
                        return false;
                foreach (var h in p.headers)
                    if (h == null || h.root == null || h.label == null)
                        return false;
                foreach (var m in p.moments)
                    if (m == null || m.root == null || m.card == null || m.icon == null || m.name == null)
                        return false;
                return true;
            }
        }

        public bool Shown => _shown;

        public JournalTab TabShown => _tab;

        public bool ReduceMotion
        {
            get => _reduceMotion;
            set => _reduceMotion = value;
        }

        public Canvas Canvas => _parts.canvas;

        public JournalScreenParts Parts => _parts;

        public TMP_Text[] BorrowedLabels => _labels;

        public Sprite IconOf(JournalIcon icon) => _icons != null && (int)icon < _icons.Length ? _icons[(int)icon] : null;

        public Sprite SystemIcon(JournalSystem system) => IconOf(JournalIcon.System0 + (int)system);

        /// <summary>Tests: the pooled cells (never more than built).</summary>
        public int CardPool => _parts.cards.Length;

        public int HeaderPool => _parts.headers.Length;

        public ScrollRect TimelineScroll => _parts.timelineScroll;

        public ScrollRect MomentScroll => _parts.momentScroll;

        public float TimelineHeight => _height;

        /// <summary>Tests: the text a card shows.</summary>
        public string CardText(int cell) => (uint)cell < (uint)_parts.cards.Length ? _parts.cards[cell].text.text : null;

        public bool CardShowsPicture(int cell) => (uint)cell < (uint)_parts.cards.Length && _parts.cards[cell].picture.enabled;

        public void Bind(ILocalization text, IJournalScreenActions actions)
        {
            Initialize();
            _actions = actions;
            if (!_lent)
                Lend(Resources.FindObjectsOfTypeAll<Sprite>(), Resources.FindObjectsOfTypeAll<TMP_FontAsset>(), Resources.FindObjectsOfTypeAll<Material>());
            BindTexts(text);
        }

        /// <summary>Borrows the scene's sprites, fonts and font materials by name (the bundle never holds a copy). Once per load.</summary>
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
                GameLog.Warning(LogCategory.Presentation, "Journal: kit sprites not loaded by the scene (plain shapes shown): " + string.Join(", ", missing));
            _icons = new Sprite[IconCount];
            for (int i = 0; i < IconCount && i < _iconNames.Length; i++)
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
            foreach (var m in _parts.moments)
                SetIcon(m.icon, IconOf(JournalIcon.Moment) ?? IconOf(JournalIcon.Paw));
        }

        private static void SetIcon(Image image, Sprite sprite)
        {
            if (image == null)
                return;
            image.sprite = sprite;
            image.preserveAspect = true;
            image.enabled = sprite != null;
        }

        /// <summary>The variant's codex thumbnail, borrowed from the catalog the scene already loaded (never copied in the bundle).</summary>
        private Sprite CodexPortrait(ContentId variant)
        {
            if (variant == ContentId.None)
                return null;
            if (!_codexSearched)
            {
                _codexSearched = true;
                int best = -1;
                foreach (var found in Resources.FindObjectsOfTypeAll<CodexVisualCatalog>())
                {
                    int count = found.variants != null ? found.variants.Count : 0;
                    if (count > best)
                    {
                        best = count;
                        _codex = found;
                    }
                }
            }
            return _codex != null && _codex.TryGetVariant(variant, out var sprite) ? sprite : null;
        }

        private void BindTexts(ILocalization text)
        {
            string T(string key) => text != null ? text.Get(key) : key;
            var p = _parts;
            SetText(p.title, T(JournalText.Title));
            SetText(p.tabs[0].label, T(JournalText.TabPages));
            SetText(p.tabs[1].label, T(JournalText.TabMoments));
            SetText(p.empty, T(JournalText.Empty));
            SetText(p.momentsEmpty, T(JournalText.MomentsEmpty));
            string isNew = T(JournalText.New);
            foreach (var c in p.cards)
                SetText(c.newLabel, isNew);
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            var p = _parts;
            _scaler = GetComponent<CanvasScaler>();
            p.close.onClick.AddListener(() => _actions?.CloseScreen());
            for (int i = 0; i < p.tabs.Length && i < 2; i++)
            {
                var tab = (JournalTab)i;
                p.tabs[i].button.onClick.AddListener(() => _actions?.SelectTab(tab));
            }
            _dateLabels = new UiLabel[p.cards.Length];
            _textLabels = new UiLabel[p.cards.Length];
            _cardItem = new int[p.cards.Length];
            for (int i = 0; i < p.cards.Length; i++)
            {
                _dateLabels[i] = new UiLabel(p.cards[i].date, 48);
                _textLabels[i] = new UiLabel(p.cards[i].text, JournalLayout.TextCapacity);
                _cardItem[i] = -1;
            }
            _headerLabels = new UiLabel[p.headers.Length];
            _headerItem = new int[p.headers.Length];
            for (int i = 0; i < p.headers.Length; i++)
            {
                _headerLabels[i] = new UiLabel(p.headers[i].label, 48);
                _headerItem[i] = -1;
            }
            _momentItem = new int[p.moments.Length];
            for (int i = 0; i < _momentItem.Length; i++)
                _momentItem[i] = int.MinValue;
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
            _parts.canvas.enabled = false;
            _parts.content.SetActive(false);
        }

        public void SetTab(JournalTab tab)
        {
            Initialize();
            _tab = tab;
            var p = _parts;
            SetActive(p.pagesPage, tab == JournalTab.Pages);
            SetActive(p.momentsPage, tab == JournalTab.Moments);
            for (int i = 0; i < p.tabs.Length && i < 2; i++)
            {
                bool on = i == (int)tab;
                p.tabs[i].back.color = on ? _accent : _card;
                p.tabs[i].label.color = on ? _text : _textSoft;
            }
        }

        // ---------------- the timeline ----------------

        public void SetTimeline(bool[] headers, int count, bool toTop)
        {
            Initialize();
            _kinds = headers ?? Array.Empty<bool>();
            _count = Mathf.Clamp(count, 0, _kinds.Length);
            if (_tops.Length < _count + 1)
                _tops = new float[Mathf.Max(_count + 1, _tops.Length * 2)];
            float y = JournalLayout.ListPadding;
            for (int i = 0; i < _count; i++)
            {
                _tops[i] = y;
                y += _kinds[i] ? JournalLayout.HeaderPitch : JournalLayout.CardPitch;
            }
            _tops[_count] = y;
            _height = y + JournalLayout.ListPadding;
            var content = _parts.timelineContent;
            content.sizeDelta = new Vector2(content.sizeDelta.x, _height);
            if (toTop)
            {
                _parts.timelineScroll.StopMovement();
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
            }
            SetActive(_parts.empty.gameObject, _count == 0);
            UpdateTimeline(true);
        }

        public void RefreshTimeline()
        {
            Initialize();
            UpdateTimeline(true);
        }

        public int CardCellOf(int item)
        {
            if (_cardItem == null || item < 0)
                return -1;
            for (int i = 0; i < _cardItem.Length; i++)
                if (_cardItem[i] == item)
                    return i;
            return -1;
        }

        public bool CardCellShown(int cell) => _cardItem != null && (uint)cell < (uint)_cardItem.Length && _cardItem[cell] >= 0;

        /// <summary>The first item whose bottom is under <paramref name="y"/> (binary search on the tops).</summary>
        private int FirstBelow(float y)
        {
            int lo = 0, hi = _count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_tops[mid + 1] <= y)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        /// <summary>The visible items keep their cells; items scrolled in take free cells and ask for their models (all when forced).</summary>
        private void UpdateTimeline(bool force)
        {
            float y = _parts.timelineContent.anchoredPosition.y;
            if (!force && Mathf.Abs(y - _lastY) < 0.5f)
                return;
            _lastY = y;
            var viewport = _parts.timelineScroll.viewport != null ? _parts.timelineScroll.viewport : (RectTransform)_parts.timelineScroll.transform;
            float height = Mathf.Max(400f, viewport.rect.height);
            int first = FirstBelow(Mathf.Max(0f, y - 40f));
            int last = first;
            while (last < _count && _tops[last] < y + height + 40f)
                last++;
            // Free the cells whose items left the window.
            for (int i = 0; i < _cardItem.Length; i++)
                if (_cardItem[i] >= 0 && (_cardItem[i] < first || _cardItem[i] >= last || _cardItem[i] >= _count || _kinds[_cardItem[i]]))
                    FreeCard(i);
            for (int i = 0; i < _headerItem.Length; i++)
                if (_headerItem[i] >= 0 && (_headerItem[i] < first || _headerItem[i] >= last || _headerItem[i] >= _count || !_kinds[_headerItem[i]]))
                    FreeHeader(i);
            for (int item = first; item < last; item++)
            {
                bool header = _kinds[item];
                int[] cells = header ? _headerItem : _cardItem;
                int cell = -1, free = -1;
                for (int i = 0; i < cells.Length; i++)
                {
                    if (cells[i] == item)
                    {
                        cell = i;
                        break;
                    }
                    if (free < 0 && cells[i] < 0)
                        free = i;
                }
                bool fresh = false;
                if (cell < 0)
                {
                    if (free < 0)
                        continue;
                    cell = free;
                    cells[cell] = item;
                    fresh = true;
                    var root = header ? _parts.headers[cell].root : _parts.cards[cell].root;
                    SetActive(root.gameObject, true);
                    float pitch = header ? JournalLayout.HeaderPitch : JournalLayout.CardPitch;
                    var position = new Vector2(0f, -_tops[item] - pitch * 0.5f);
                    if (root.anchoredPosition != position)
                        root.anchoredPosition = position;
                }
                else if (force)
                {
                    var root = header ? _parts.headers[cell].root : _parts.cards[cell].root;
                    float pitch = header ? JournalLayout.HeaderPitch : JournalLayout.CardPitch;
                    var position = new Vector2(0f, -_tops[item] - pitch * 0.5f);
                    if (root.anchoredPosition != position)
                        root.anchoredPosition = position;
                }
                if (fresh || force)
                    _actions?.FillTimeline(item, header, cell);
            }
        }

        private void FreeCard(int cell)
        {
            _cardItem[cell] = -1;
            SetActive(_parts.cards[cell].root.gameObject, false);
        }

        private void FreeHeader(int cell)
        {
            _headerItem[cell] = -1;
            SetActive(_parts.headers[cell].root.gameObject, false);
        }

        public void SetHeader(int cell, in JournalHeaderModel model)
        {
            Initialize();
            if ((uint)cell >= (uint)_parts.headers.Length)
                return;
            if (!model.Shown)
            {
                FreeHeader(cell);
                return;
            }
            Copy(_headerLabels[cell], model.Text, model.Length);
        }

        public void SetCard(int cell, in JournalCardModel model)
        {
            Initialize();
            if ((uint)cell >= (uint)_parts.cards.Length)
                return;
            var c = _parts.cards[cell];
            if (!model.Shown)
            {
                FreeCard(cell);
                return;
            }
            Copy(_dateLabels[cell], model.Date, model.DateLength);
            Copy(_textLabels[cell], model.Text, model.TextLength);
            var systemIcon = SystemIcon(model.System) ?? IconOf(JournalIcon.Paw);
            c.systemIcon.sprite = systemIcon;
            c.systemIcon.enabled = systemIcon != null;
            if (c.systemBack != null)
                c.systemBack.color = Tint(model.System);
            SetActive(c.newPill.gameObject, model.New);
            c.card.color = model.New ? _cardNew : _card;
            bool texture = model.Source == JournalPictureSource.Texture && model.Picture != null;
            c.picture.enabled = texture;
            if (texture)
            {
                if (c.picture.texture != model.Picture)
                    c.picture.texture = model.Picture;
                if (c.picture.uvRect != model.PictureRect)
                    c.picture.uvRect = model.PictureRect;
                c.thumb.enabled = false;
                c.pictureBack.color = _disc;
                return;
            }
            Sprite thumb = model.Source == JournalPictureSource.Codex ? CodexPortrait(model.Variant) : null;
            bool portrait = thumb != null;
            if (!portrait)
                thumb = systemIcon;
            c.thumb.sprite = thumb;
            c.thumb.enabled = thumb != null;
            c.thumb.preserveAspect = true;
            float size = portrait ? PortraitSize : IconSize;
            var rect = c.thumb.rectTransform;
            if (rect.sizeDelta.x != size)
                rect.sizeDelta = new Vector2(size, size);
            c.thumb.color = portrait ? Color.white : _textSoft;
            c.pictureBack.color = portrait ? _disc : Tint(model.System);
        }

        private Color Tint(JournalSystem system) => _systemTints.Length > 0 ? _systemTints[(int)system % _systemTints.Length] : _disc;

        private static void Copy(UiLabel label, char[] source, int length)
        {
            var buffer = label.Buffer;
            length = source == null ? 0 : Mathf.Clamp(length, 0, Mathf.Min(buffer.Length, source.Length));
            if (length > 0)
                Array.Copy(source, buffer, length);
            label.Commit(length);
        }

        // ---------------- moments ----------------

        public void SetMoments(int count, bool toTop)
        {
            Initialize();
            _momentCount = Mathf.Max(0, count);
            var content = _parts.momentContent;
            content.sizeDelta = new Vector2(content.sizeDelta.x, _momentCount * JournalLayout.MomentPitch + 2f * JournalLayout.ListPadding);
            if (toTop)
            {
                _parts.momentScroll.StopMovement();
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
            }
            SetActive(_parts.momentsEmpty.gameObject, _momentCount == 0);
            UpdateMoments(true);
        }

        private void UpdateMoments(bool force)
        {
            int pool = _parts.moments.Length;
            float y = _parts.momentContent.anchoredPosition.y;
            int first = Mathf.Clamp(Mathf.FloorToInt((y - JournalLayout.ListPadding) / JournalLayout.MomentPitch), 0, Mathf.Max(0, _momentCount - pool));
            if (!force && first == _momentFirst)
                return;
            _momentFirst = first;
            for (int row = first; row < first + pool; row++)
            {
                int cell = row % pool;
                int item = row < _momentCount ? row : -1;
                if (!force && _momentItem[cell] == item)
                    continue;
                _momentItem[cell] = item;
                var root = _parts.moments[cell].root;
                SetActive(root.gameObject, item >= 0);
                if (item < 0)
                    continue;
                var position = new Vector2(0f, -JournalLayout.ListPadding - JournalLayout.MomentPitch * (row + 0.5f));
                if (root.anchoredPosition != position)
                    root.anchoredPosition = position;
                _actions?.FillMoment(cell, item);
            }
        }

        public void SetMoment(int cell, in JournalMomentModel model)
        {
            Initialize();
            if ((uint)cell >= (uint)_parts.moments.Length)
                return;
            var m = _parts.moments[cell];
            if (!model.Shown)
            {
                SetActive(m.root.gameObject, false);
                return;
            }
            SetText(m.name, model.Name);
        }

        // ---------------- Tick ----------------

        public void Tick(float deltaTime)
        {
            if (!_initialized || !_shown)
                return;
            if (_tab == JournalTab.Pages)
                UpdateTimeline(false);
            else
                UpdateMoments(false);
        }

        // ---------------- helpers ----------------

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

        public void SetParts(JournalScreenParts parts) => _parts = parts;

        public void SetBorrowed(Image[] kitImages, string[] kitNames, TMP_Text[] labels, string[] labelFonts, string[] labelMaterials, string[] iconNames)
        {
            _kitImages = kitImages;
            _kitNames = kitNames;
            _labels = labels;
            _labelFonts = labelFonts;
            _labelMaterials = labelMaterials;
            _iconNames = iconNames;
        }

        public void SetColors(Color card, Color cardNew, Color text, Color textSoft, Color accent, Color disc, Color[] systemTints)
        {
            _card = card;
            _cardNew = cardNew;
            _text = text;
            _textSoft = textSoft;
            _accent = accent;
            _disc = disc;
            if (systemTints != null && systemTints.Length > 0)
                _systemTints = systemTints;
        }
    }
}
