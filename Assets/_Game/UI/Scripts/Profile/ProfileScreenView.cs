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
    /// PF_UI_Achievements (T44B « Succès, titres, cadres, poses », doc « 01 Succès, titres, cadres, poses et journal » sections
    /// 1, 3, 4, 5, screen I-SCR-020; built by ProfileScreenPipeline, Addressables group Profile). A screen-space canvas over the
    /// hidden refuge: the title and close; the profile card (the favorite animal's portrait inside the selected frame, the
    /// selected title under it); four tabs with their dots. Succès: 14 chips (« Tout » and the 13 categories with their icons
    /// and a dot while a tier waits) over a recycled grid of 12 badge cells for the 200 achievements: the tier's shared shape
    /// (Locked, Bronze, Silver, Gold, Star: a round five-point star) with the category's icon over it, four pips for the tiers
    /// reached (never colour alone), the name, the goal line with {n}, the bar and « Plus que 3 », or the claim button with its
    /// Gems. Titres and Poses share 12 recycled rows (the pose page says « Mode photo bientôt »); Cadres 18 recycled cells,
    /// each frame drawn as one white ring sprite tinted by its palette over a rim copy, with 1 to 6 motifs on the fixed points
    /// (<see cref="ProfileFrameStyle"/>, baked from agent BA's FrameArt). The UI kit's sprites, the icons and the fonts are
    /// borrowed from what the scene loaded (<see cref="Lend"/>). Nothing allocates per frame after <see cref="Bind"/>; no cell
    /// is ever instantiated at run time.
    /// </summary>
    public sealed class ProfileScreenView : MonoBehaviour, IProfileScreenView, IJournalEntryView
    {
        /// <summary>The icons, borrowed by name: the screen's own, then the 13 categories (by <see cref="AchievementCategory"/>).</summary>
        public enum ProfileIcon
        {
            Gems = 0,
            Lock = 1,
            Check = 2,
            Paw = 3,
            Photo = 4,
            Title = 5,
            BadgeRound = 6,
            BadgeStar = 7,
            Category0 = 8,

            /// <summary>T44C: the journal's entry on the card (after the 13 categories).</summary>
            Journal = Category0 + ProfileLayout.Categories,
        }

        public const int IconCount = (int)ProfileIcon.Journal + 1;

        private const float NoteIn = 0.15f;
        private const float NoteHold = 2f;
        private const float NoteOut = 0.25f;

        /// <summary>The lists' pitches (ProfileScreenPipeline places the pooled cells at these steps).</summary>
        public const float BadgePitchX = 510f;

        public const float BadgePitchY = 620f;
        public const float RowPitch = 254f;
        public const float FramePitchX = 336f;
        public const float FramePitchY = 400f;
        public const float ListPadding = 24f;

        [SerializeField] private ProfileScreenParts _parts = new ProfileScreenParts();

        [Header("In the bundle (Art/UI/Profile, Atlas_Frames)")]
        [SerializeField] private ProfileFrameStyle[] _frameStyles = Array.Empty<ProfileFrameStyle>();

        /// <summary>Locked, Bronze, Silver, Gold, Star (by <see cref="AchievementTier"/>); null: the round kit shape tinted.</summary>
        [SerializeField] private Sprite[] _badgeShapes = new Sprite[5];

        [Header("Borrowed at run time from the scene (never in the bundle)")]
        [SerializeField] private Image[] _kitImages = Array.Empty<Image>();
        [SerializeField] private string[] _kitNames = Array.Empty<string>();
        [SerializeField] private TMP_Text[] _labels = Array.Empty<TMP_Text>();
        [SerializeField] private string[] _labelFonts = Array.Empty<string>();
        [SerializeField] private string[] _labelMaterials = Array.Empty<string>();

        /// <summary>One name per <see cref="ProfileIcon"/>, a second choice after « | ».</summary>
        [SerializeField] private string[] _iconNames = Array.Empty<string>();

        [Header("Colours")]
        [SerializeField] private Color _card = new Color(1f, 0.99f, 0.96f, 1f);
        [SerializeField] private Color _cardLocked = new Color(0.93f, 0.90f, 0.85f, 1f);
        [SerializeField] private Color _text = new Color(0.29f, 0.23f, 0.17f, 1f);
        [SerializeField] private Color _textSoft = new Color(0.54f, 0.46f, 0.38f, 1f);
        [SerializeField] private Color _accent = new Color(1f, 0.69f, 0.37f, 1f);
        [SerializeField] private Color _selected = new Color(1f, 0.93f, 0.80f, 1f);
        [SerializeField] private Color _plainRing = new Color(1f, 0.95f, 0.87f, 1f);
        [SerializeField] private Color _plainRim = new Color(0.91f, 0.84f, 0.72f, 1f);
        [SerializeField] private Color _pipOff = new Color(0.90f, 0.86f, 0.80f, 1f);

        /// <summary>The tiers' tints when the round kit shape stands in for agent BA's shapes (Locked, Bronze, Silver, Gold, Star).</summary>
        [SerializeField] private Color[] _tierTints =
        {
            new Color(0.90f, 0.87f, 0.82f, 1f), new Color(0.89f, 0.66f, 0.47f, 1f), new Color(0.79f, 0.82f, 0.85f, 1f),
            new Color(0.95f, 0.79f, 0.36f, 1f), new Color(1f, 0.84f, 0.43f, 1f),
        };

        private readonly SafeAreaFitter _fitter = new SafeAreaFitter();
        private IProfileScreenActions _actions;
        private Action _openJournal;
        private CanvasScaler _scaler;
        private Sprite[] _icons;
        private bool _lent;
        private bool _initialized;
        private bool _shown;
        private bool _reduceMotion;
        private ProfileTab _tab;
        private readonly Dictionary<string, int> _styleIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        private Sprite _portrait;
        private CodexVisualCatalog _codex;
        private bool _codexSearched;

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

        // Texts read at Bind.
        private UiCounted _toGo;
        private string _complete = string.Empty;

        // Labels from char buffers.
        private UiLabel[] _goalLabels;
        private UiLabel[] _toGoLabels;
        private UiLabel[] _gemLabels;

        // The recycled lists.
        private Recycler[] _lists;
        private float[] _popT;

        // The note.
        private float _noteT = -1f;

        /// <summary>A pooled list: its cells sit at fixed pitches, the visible rows ask for their items when they change.</summary>
        private sealed class Recycler
        {
            public ProfileList List;
            public ScrollRect Scroll;
            public RectTransform Content;
            public RectTransform[] Cells;
            public int Columns;
            public int PoolRows;
            public float PitchX;
            public float PitchY;
            public int Count;
            public int First = -1;
            public int[] Items;
        }

        // ---------------- IProfileScreenView ----------------

        public bool Valid
        {
            get
            {
                var p = _parts;
                if (p == null || p.canvas == null || p.content == null || p.safeArea == null || p.title == null || p.close == null || p.card == null
                    || p.cardTitle == null || p.achievements == null || p.chipContent == null || p.badgeScroll == null || p.badgeContent == null
                    || p.rowsPage == null || p.photoSoon == null || p.rowScroll == null || p.rowContent == null || p.framesPage == null
                    || p.frameScroll == null || p.frameContent == null || p.notePill == null || p.noteGroup == null || p.noteLabel == null)
                    return false;
                if (!FrameValid(p.cardFrame) || p.tabs.Length < 4 || p.chips.Length < ProfileLayout.Chips || p.badges.Length < ProfileLayout.BadgeCells
                    || p.rows.Length < ProfileLayout.RowCells || p.frames.Length < ProfileLayout.FrameCells)
                    return false;
                foreach (var t in p.tabs)
                    if (t == null || t.button == null || t.back == null || t.label == null || t.dot == null)
                        return false;
                foreach (var c in p.chips)
                    if (c == null || c.root == null || c.button == null || c.back == null || c.icon == null || c.label == null || c.dot == null)
                        return false;
                foreach (var b in p.badges)
                    if (b == null || b.root == null || b.button == null || b.card == null || b.shape == null || b.icon == null || b.pips.Length < 4
                        || b.name == null || b.goal == null || b.barFill == null || b.toGo == null || b.claim == null || b.claimGems == null)
                        return false;
                foreach (var r in p.rows)
                    if (r == null || r.root == null || r.button == null || r.card == null || r.icon == null || r.lockIcon == null || r.name == null
                        || r.source == null || r.check == null || r.newPill == null)
                        return false;
                foreach (var f in p.frames)
                    if (f == null || f.root == null || f.button == null || f.card == null || !FrameValid(f.frame) || f.name == null || f.lockIcon == null
                        || f.check == null || f.newPill == null)
                        return false;
                return true;
            }
        }

        private static bool FrameValid(ProfileFrameParts f) =>
            f != null && f.root != null && f.disc != null && f.portrait != null && f.rim != null && f.ring != null && f.inner != null
            && f.motifs.Length >= ProfileLayout.MotifPoints;

        public bool Shown => _shown;

        public ProfileTab TabShown => _tab;

        public bool ReduceMotion
        {
            get => _reduceMotion;
            set => _reduceMotion = value;
        }

        public Canvas Canvas => _parts.canvas;

        public ProfileScreenParts Parts => _parts;

        public ProfileFrameStyle[] FrameStyles => _frameStyles;

        /// <summary>Tests: the labels whose fonts are borrowed.</summary>
        public TMP_Text[] BorrowedLabels => _labels;

        public bool NoteShown => _noteT >= 0f;

        public string NoteText => _parts.noteLabel.text;

        public string CardTitleText => _parts.cardTitle.text;

        public Sprite IconOf(ProfileIcon icon) => _icons != null && (int)icon < _icons.Length ? _icons[(int)icon] : null;

        /// <summary>The cells a list holds (tests: never more than the pool).</summary>
        public int PoolSize(ProfileList list) => _lists != null ? _lists[(int)list].Cells.Length : 0;

        /// <summary>The scroll of a list (tests and captures).</summary>
        public ScrollRect ScrollOf(ProfileList list) => list == ProfileList.Badges ? _parts.badgeScroll : list == ProfileList.Frames ? _parts.frameScroll : _parts.rowScroll;

        public void Bind(ILocalization text, IProfileScreenActions actions)
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
                GameLog.Warning(LogCategory.Presentation, "Profile: kit sprites not loaded by the scene (plain shapes shown): " + string.Join(", ", missing));
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
            ApplyIcons();
        }

        private void ApplyIcons()
        {
            var p = _parts;
            var gems = IconOf(ProfileIcon.Gems);
            var lockIcon = IconOf(ProfileIcon.Lock);
            var check = IconOf(ProfileIcon.Check);
            foreach (var b in p.badges)
                SetIcon(b.claimGem, gems);
            foreach (var r in p.rows)
            {
                SetIcon(r.lockIcon, lockIcon);
                SetIcon(r.check, check);
            }
            foreach (var f in p.frames)
            {
                SetIcon(f.lockIcon, lockIcon);
                SetIcon(f.check, check);
            }
            for (int c = 0; c < p.chips.Length; c++)
                SetIcon(p.chips[c].icon, c == 0 ? IconOf(ProfileIcon.BadgeStar) : IconOf(ProfileIcon.Category0 + (c - 1)));
            SetIcon(p.journalIcon, IconOf(ProfileIcon.Journal));
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
            SetText(p.title, T(ProfileText.Title));
            for (int i = 0; i < p.tabs.Length && i < 4; i++)
                SetText(p.tabs[i].label, T(ProfileText.Tab((ProfileTab)i)));
            SetText(p.photoSoon, T(ProfileText.PhotoSoon));
            SetText(p.journalLabel, T(JournalText.Entry));
            string claim = T(ProfileText.Claim), isNew = T(ProfileText.New);
            foreach (var b in p.badges)
                SetText(b.claimLabel, claim);
            foreach (var r in p.rows)
                SetText(r.newLabel, isNew);
            foreach (var f in p.frames)
                SetText(f.newLabel, isNew);
            _toGo = UiCounted.From(text, ProfileText.ToGo);
            _complete = T(ProfileText.Complete);
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            var p = _parts;
            _scaler = GetComponent<CanvasScaler>();
            _styleIndex.Clear();
            for (int i = 0; i < _frameStyles.Length; i++)
                if (_frameStyles[i] != null && !string.IsNullOrEmpty(_frameStyles[i].key) && !_styleIndex.ContainsKey(_frameStyles[i].key))
                    _styleIndex.Add(_frameStyles[i].key, i);
            p.close.onClick.AddListener(() => _actions?.CloseScreen());
            if (p.journalButton != null)
                p.journalButton.onClick.AddListener(() => _openJournal?.Invoke());
            for (int i = 0; i < p.tabs.Length; i++)
            {
                var tab = (ProfileTab)i;
                p.tabs[i].button.onClick.AddListener(() => _actions?.SelectTab(tab));
            }
            for (int i = 0; i < p.chips.Length; i++)
            {
                int chip = i;
                p.chips[i].button.onClick.AddListener(() => _actions?.SelectCategory(chip));
            }
            _goalLabels = new UiLabel[p.badges.Length];
            _toGoLabels = new UiLabel[p.badges.Length];
            _gemLabels = new UiLabel[p.badges.Length];
            _popT = new float[p.badges.Length];
            _lists = new Recycler[3];
            _lists[(int)ProfileList.Badges] = NewList(ProfileList.Badges, p.badgeScroll, p.badgeContent, ProfileLayout.BadgeColumns, BadgePitchX, BadgePitchY,
                Roots(p.badges, b => b.root));
            _lists[(int)ProfileList.Rows] = NewList(ProfileList.Rows, p.rowScroll, p.rowContent, 1, 0f, RowPitch, Roots(p.rows, r => r.root));
            _lists[(int)ProfileList.Frames] = NewList(ProfileList.Frames, p.frameScroll, p.frameContent, ProfileLayout.FrameColumns, FramePitchX, FramePitchY,
                Roots(p.frames, f => f.root));
            for (int i = 0; i < p.badges.Length; i++)
            {
                var b = p.badges[i];
                _goalLabels[i] = new UiLabel(b.goal, 192);
                _toGoLabels[i] = new UiLabel(b.toGo, 64);
                _gemLabels[i] = new UiLabel(b.claimGems, 16);
                _popT[i] = -1f;
                int cell = i;
                b.button.onClick.AddListener(() => Tap(ProfileList.Badges, cell));
                b.claim.onClick.AddListener(() =>
                {
                    int item = _lists[(int)ProfileList.Badges].Items[cell];
                    if (item >= 0)
                        _actions?.Claim(item);
                });
            }
            for (int i = 0; i < p.rows.Length; i++)
            {
                int cell = i;
                p.rows[i].button.onClick.AddListener(() => Tap(ProfileList.Rows, cell));
            }
            for (int i = 0; i < p.frames.Length; i++)
            {
                int cell = i;
                p.frames[i].button.onClick.AddListener(() => Tap(ProfileList.Frames, cell));
            }
        }

        private static RectTransform[] Roots<T>(T[] cells, Func<T, RectTransform> root)
        {
            var roots = new RectTransform[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                roots[i] = root(cells[i]);
            return roots;
        }

        private static Recycler NewList(ProfileList list, ScrollRect scroll, RectTransform content, int columns, float pitchX, float pitchY, RectTransform[] cells)
        {
            var items = new int[cells.Length];
            for (int i = 0; i < items.Length; i++)
                items[i] = int.MinValue;
            return new Recycler
            {
                List = list, Scroll = scroll, Content = content, Cells = cells, Columns = columns, PoolRows = cells.Length / columns, PitchX = pitchX,
                PitchY = pitchY, Items = items,
            };
        }

        private void Tap(ProfileList list, int cell)
        {
            int item = _lists[(int)list].Items[cell];
            if (item >= 0)
                _actions?.TapCell(list, item);
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
            _parts.canvas.enabled = false;
            _parts.content.SetActive(false);
        }

        public void SetTab(ProfileTab tab)
        {
            Initialize();
            _tab = tab;
            var p = _parts;
            SetActive(p.achievements, tab == ProfileTab.Achievements);
            SetActive(p.rowsPage, tab == ProfileTab.Titles || tab == ProfileTab.Poses);
            SetActive(p.framesPage, tab == ProfileTab.Frames);
            SetActive(p.photoSoon.gameObject, tab == ProfileTab.Poses);
            // The pose page's line sits over its list: the list starts lower there.
            if (p.rowViewport != null)
                p.rowViewport.offsetMax = new Vector2(p.rowViewport.offsetMax.x, tab == ProfileTab.Poses ? -RowsTopPoses : -RowsTop);
            for (int i = 0; i < p.tabs.Length; i++)
            {
                bool on = i == (int)tab;
                p.tabs[i].back.color = on ? _accent : _card;
                p.tabs[i].label.color = on ? _text : _textSoft;
            }
        }

        /// <summary>The row list's top inset under the tabs (and under « Mode photo bientôt » on the pose page).</summary>
        public const float RowsTop = 820f;

        public const float RowsTopPoses = 900f;

        // ---------------- IJournalEntryView (T44C) ----------------

        public void BindJournal(Action open)
        {
            Initialize();
            _openJournal = open;
        }

        public void SetJournal(bool shown, bool dot)
        {
            var p = _parts;
            if (p.journalButton == null)
                return;
            SetActive(p.journalButton.gameObject, shown);
            if (p.journalDot != null)
                SetActive(p.journalDot.gameObject, shown && dot);
        }

        /// <summary>Tests: the journal's entry shows, and its dot.</summary>
        public bool JournalEntryShown => _parts.journalButton != null && _parts.journalButton.gameObject.activeSelf;

        public bool JournalDotShown => JournalEntryShown && _parts.journalDot != null && _parts.journalDot.gameObject.activeSelf;

        public void SetTabDots(bool achievements, bool titles, bool frames, bool poses)
        {
            var p = _parts;
            SetActive(p.tabs[0].dot.gameObject, achievements);
            SetActive(p.tabs[1].dot.gameObject, titles);
            SetActive(p.tabs[2].dot.gameObject, frames);
            SetActive(p.tabs[3].dot.gameObject, poses);
        }

        public void SetCard(in ProfileCardModel model)
        {
            Initialize();
            _portrait = model.Portrait != null ? model.Portrait : CodexPortrait(model.PortraitVariant);
            ApplyFrame(_parts.cardFrame, model.FrameKey, true);
            SetText(_parts.cardTitle, model.Title);
            SetText(_parts.cardFrameName, model.FrameName);
        }

        public void SetChip(int chip, in ProfileChipModel model)
        {
            Initialize();
            if ((uint)chip >= (uint)_parts.chips.Length)
                return;
            var c = _parts.chips[chip];
            SetActive(c.root.gameObject, model.Shown);
            if (!model.Shown)
                return;
            SetText(c.label, model.Label);
            c.back.color = model.Selected ? _accent : _card;
            c.label.color = model.Selected ? _text : _textSoft;
            SetActive(c.dot.gameObject, model.Dot);
        }

        public void SetListCount(ProfileList list, int count, bool toTop)
        {
            Initialize();
            var r = _lists[(int)list];
            r.Count = Mathf.Max(0, count);
            int rows = (r.Count + r.Columns - 1) / r.Columns;
            var content = r.Content;
            content.sizeDelta = new Vector2(content.sizeDelta.x, rows * r.PitchY + 2f * ListPadding);
            if (toTop)
            {
                r.Scroll?.StopMovement();
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
            }
            UpdateList(r, true);
        }

        public void RefreshList(ProfileList list)
        {
            Initialize();
            UpdateList(_lists[(int)list], true);
        }

        public int CellOf(ProfileList list, int item)
        {
            if (_lists == null || item < 0)
                return -1;
            var items = _lists[(int)list].Items;
            for (int i = 0; i < items.Length; i++)
                if (items[i] == item)
                    return i;
            return -1;
        }

        /// <summary>The visible rows from the scroll position; only cells whose item changed ask for it (all when forced).</summary>
        private void UpdateList(Recycler r, bool force)
        {
            int totalRows = (r.Count + r.Columns - 1) / r.Columns;
            float y = r.Content.anchoredPosition.y;
            int first = Mathf.Clamp(Mathf.FloorToInt((y - ListPadding) / r.PitchY), 0, Mathf.Max(0, totalRows - r.PoolRows));
            if (!force && first == r.First)
                return;
            r.First = first;
            for (int row = first; row < first + r.PoolRows; row++)
            {
                int slot = row % r.PoolRows;
                for (int column = 0; column < r.Columns; column++)
                {
                    int cell = slot * r.Columns + column;
                    int item = row * r.Columns + column;
                    if (row >= totalRows || item >= r.Count)
                        item = -1;
                    if (!force && r.Items[cell] == item)
                        continue;
                    r.Items[cell] = item;
                    var root = r.Cells[cell];
                    SetActive(root.gameObject, item >= 0);
                    if (item < 0)
                        continue;
                    float x = r.Columns > 1 ? (column - (r.Columns - 1) * 0.5f) * r.PitchX : 0f;
                    var position = new Vector2(x, -ListPadding - r.PitchY * (row + 0.5f));
                    if (root.anchoredPosition != position)
                        root.anchoredPosition = position;
                    _actions?.FillCell(r.List, cell, item);
                }
            }
        }

        public void SetBadge(int cell, in AchievementCellModel model)
        {
            Initialize();
            if ((uint)cell >= (uint)_parts.badges.Length)
                return;
            var b = _parts.badges[cell];
            if (!model.Shown)
            {
                SetActive(b.root.gameObject, false);
                return;
            }
            int tier = Mathf.Clamp((int)model.Reached, 0, 4);
            var shape = tier < _badgeShapes.Length ? _badgeShapes[tier] : null;
            if (shape != null)
            {
                b.shape.sprite = shape;
                b.shape.color = Color.white;
            }
            else
            {
                b.shape.sprite = IconOf(model.Reached == AchievementTier.Star ? ProfileIcon.BadgeStar : ProfileIcon.BadgeRound);
                b.shape.color = _tierTints[tier];
            }
            b.shape.enabled = b.shape.sprite != null;
            var icon = IconOf(ProfileIcon.Category0 + (int)model.Category);
            b.icon.sprite = icon;
            b.icon.enabled = icon != null;
            b.icon.color = model.Reached == AchievementTier.None ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            for (int i = 0; i < b.pips.Length; i++)
                if (b.pips[i] != null)
                    b.pips[i].color = i < tier ? _tierTints[Mathf.Min(i + 1, 4)] : _pipOff;
            SetText(b.name, model.Name);
            _goalLabels[cell].SetTemplate(model.Goal, model.Next);
            bool claim = model.Claimable;
            SetActive(b.claim.gameObject, claim);
            SetActive(b.barTrack != null ? b.barTrack.gameObject : b.barFill.gameObject, !claim);
            SetActive(b.toGo.gameObject, !claim);
            if (claim)
                _gemLabels[cell].SetNumber(model.Gems, '+');
            else
            {
                b.barFill.fillAmount = Mathf.Clamp01(model.Fill);
                if (model.Complete || model.Reached == AchievementTier.Star)
                    _toGoLabels[cell].SetString(_complete);
                else
                    _toGoLabels[cell].SetTemplate(_toGo.For(model.Left), model.Left);
            }
            b.card.color = claim ? _selected : _card;
        }

        public void SetRow(int cell, in ProfileRowModel model)
        {
            Initialize();
            if ((uint)cell >= (uint)_parts.rows.Length)
                return;
            var r = _parts.rows[cell];
            if (!model.Shown)
            {
                SetActive(r.root.gameObject, false);
                return;
            }
            var icon = model.Kind == ProfileItemKind.Pose ? IconOf(ProfileIcon.Photo) : IconOf(ProfileIcon.Title);
            r.icon.sprite = icon;
            r.icon.enabled = icon != null && model.Owned;
            SetActive(r.lockIcon.gameObject, !model.Owned);
            SetText(r.name, model.Name);
            r.name.color = model.Owned ? _text : _textSoft;
            SetText(r.source, model.Source);
            SetActive(r.check.gameObject, model.Selected);
            SetActive(r.newPill.gameObject, model.New);
            r.card.color = model.Selected ? _selected : model.Owned ? _card : _cardLocked;
        }

        public void SetFrameCell(int cell, in ProfileFrameCellModel model)
        {
            Initialize();
            if ((uint)cell >= (uint)_parts.frames.Length)
                return;
            var f = _parts.frames[cell];
            if (!model.Shown)
            {
                SetActive(f.root.gameObject, false);
                return;
            }
            ApplyFrame(f.frame, model.FrameKey, model.Owned);
            SetText(f.name, model.Name);
            f.name.color = model.Owned ? _text : _textSoft;
            SetActive(f.lockIcon.gameObject, !model.Owned);
            SetActive(f.check.gameObject, model.Selected);
            SetActive(f.newPill.gameObject, model.New);
            f.card.color = model.Selected ? _selected : model.Owned ? _card : _cardLocked;
        }

        /// <summary>Section 4: the ring tinted by the frame's palette over its rim, the Double inner ring, the motifs on their points.</summary>
        private void ApplyFrame(ProfileFrameParts f, string key, bool owned)
        {
            ProfileFrameStyle style = null;
            if (key != null && _styleIndex.TryGetValue(key, out int index))
                style = _frameStyles[index];
            float alpha = owned ? 1f : 0.55f;
            f.ring.color = Faded(style != null ? style.ring : _plainRing, alpha);
            f.rim.color = Faded(style != null ? style.rim : _plainRim, alpha);
            bool inner = style != null && style.inner.a > 0.01f;
            SetActive(f.inner.gameObject, inner);
            if (inner)
                f.inner.color = Faded(style.inner, alpha);
            for (int i = 0; i < f.motifs.Length; i++)
            {
                var motif = f.motifs[i];
                if (motif == null)
                    continue;
                var sprite = style != null && i < style.motifs.Length ? style.motifs[i] : null;
                SetActive(motif.gameObject, sprite != null);
                if (sprite == null)
                    continue;
                motif.sprite = sprite;
                motif.color = Faded(i < style.tints.Length ? style.tints[i] : Color.white, alpha);
            }
            var portrait = _portrait != null ? _portrait : IconOf(ProfileIcon.Paw);
            f.portrait.sprite = portrait;
            f.portrait.enabled = portrait != null;
            f.portrait.color = _portrait != null ? new Color(1f, 1f, 1f, alpha) : new Color(_textSoft.r, _textSoft.g, _textSoft.b, 0.5f * alpha);
        }

        private static Color Faded(Color color, float alpha) => new Color(color.r, color.g, color.b, color.a * alpha);

        public void PlayClaim(int item)
        {
            int cell = CellOf(ProfileList.Badges, item);
            if (cell >= 0 && !_reduceMotion)
                _popT[cell] = 0f;
        }

        public void ShowNote(string text)
        {
            Initialize();
            if (string.IsNullOrEmpty(text) || !_shown)
                return;
            _parts.noteLabel.text = text;
            _parts.notePill.gameObject.SetActive(true);
            _noteT = 0f;
            ApplyNote();
        }

        // ---------------- Tick ----------------

        public void Tick(float deltaTime)
        {
            if (!_initialized || !_shown)
                return;
            var list = _tab == ProfileTab.Achievements ? ProfileList.Badges : _tab == ProfileTab.Frames ? ProfileList.Frames : ProfileList.Rows;
            UpdateList(_lists[(int)list], false);
            if (_noteT >= 0f)
            {
                _noteT += deltaTime;
                ApplyNote();
            }
            for (int i = 0; i < _popT.Length; i++)
            {
                if (_popT[i] < 0f)
                    continue;
                _popT[i] += deltaTime;
                float k = Mathf.Clamp01(_popT[i] / ProfileLayout.ClaimPopSeconds);
                float s = k < 0.35f ? Mathf.Lerp(1f, 1.18f, k / 0.35f) : Mathf.Lerp(1.18f, 1f, EaseOut((k - 0.35f) / 0.65f));
                _parts.badges[i].shape.rectTransform.localScale = new Vector3(s, s, 1f);
                if (k >= 1f)
                    _popT[i] = -1f;
            }
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

        // ---------------- helpers ----------------

        private static float EaseOut(float k) => 1f - (1f - k) * (1f - k);

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

        /// <summary>Prefab builder hook (Editor ProfileScreenPipeline).</summary>
        public void SetParts(ProfileScreenParts parts, ProfileFrameStyle[] frameStyles, Sprite[] badgeShapes)
        {
            _parts = parts;
            _frameStyles = frameStyles ?? Array.Empty<ProfileFrameStyle>();
            _badgeShapes = badgeShapes ?? new Sprite[5];
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

        public void SetColors(Color card, Color cardLocked, Color text, Color textSoft, Color accent, Color selected, Color plainRing, Color plainRim)
        {
            _card = card;
            _cardLocked = cardLocked;
            _text = text;
            _textSoft = textSoft;
            _accent = accent;
            _selected = selected;
            _plainRing = plainRing;
            _plainRim = plainRim;
        }
    }
}
