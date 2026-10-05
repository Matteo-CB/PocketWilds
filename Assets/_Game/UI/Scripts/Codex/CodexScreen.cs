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
    /// PF_UI_Codex (UI doc SCREEN 007/008, T19): biome sections of species cards in a virtualized list,
    /// and a species sheet with its variants. Only the rows on screen exist as views: thousands of
    /// entries never create thousands of cards. Full screen and opaque, so the sanctuary camera can stop
    /// while it is open; closing is instant. Ticked by the sanctuary, no Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CodexScreen : MonoBehaviour, ICodexView
    {
        public const float HeaderRowHeight = 112f;
        public const float CardRowHeight = 428f;
        public const int SheetColumns = 3;
        public const float SheetSlotsTop = 860f;
        public const float SheetSlotPitch = 364f;
        private const int Overscan = 1;
        private const float FadeSeconds = 0.14f;

        private enum RowKind : byte
        {
            Header,
            Cards,
        }

        private struct Row
        {
            public RowKind Kind;
            public int Section;
            public int First;
            public int Count;
        }

        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private CodexVisualCatalog _visuals;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _safeArea;

        [Header("Header")]
        [SerializeField] private Button _back;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private RectTransform _progressBack;
        [SerializeField] private RectTransform _progressFill;
        [SerializeField] private TMP_Text _progressLabel;

        [Header("List")]
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private RectTransform _viewport;
        [SerializeField] private RectTransform _listContent;
        [SerializeField] private CodexHeaderView _headerTemplate;
        [SerializeField] private CodexCardRow _cardRowTemplate;

        [Header("Species sheet")]
        [SerializeField] private GameObject _sheet;
        [SerializeField] private ScrollRect _sheetScroll;
        [SerializeField] private RectTransform _sheetContent;
        [SerializeField] private Image _sheetPortrait;
        [SerializeField] private TMP_Text _sheetName;
        [SerializeField] private TMP_Text _sheetOwned;
        [SerializeField] private TMP_Text _sheetVariantsTitle;
        [SerializeField] private CodexVariantSlotView[] _slots;
        [SerializeField] private GameObject _sheetMastery;
        [SerializeField] private TMP_Text _sheetMasteryLevel;
        [SerializeField] private RectTransform _sheetMasteryBar;
        [SerializeField] private RectTransform _sheetMasteryFill;
        [SerializeField] private TMP_Text _sheetMasteryXp;

        private readonly VirtualListLayout _layout = new VirtualListLayout();
        private readonly Stack<CodexHeaderView> _freeHeaders = new Stack<CodexHeaderView>();
        private readonly Stack<CodexCardRow> _freeCardRows = new Stack<CodexCardRow>();
        private readonly UiNumberText _numbers = new UiNumberText();
        private readonly SafeAreaFitter _safeFitter = new SafeAreaFitter();
        private readonly UiNumberText _masteryLevelText = new UiNumberText();
        private readonly UiNumberText _masteryXpText = new UiNumberText();
        private Row[] _rows = new Row[0];
        private CodexHeaderView[] _headerAt = new CodexHeaderView[0];
        private CodexCardRow[] _cardRowAt = new CodexCardRow[0];
        private int _boundFirst;
        private int _boundLast = -1;
        private float _boundScroll = float.NaN;
        private float _boundViewport = float.NaN;

        private CodexService _codex;
        private ILocalization _text;
        private ICodexActions _actions;
        private bool _open;
        private bool _closing;
        private float _fade;
        private bool _listening;
        private int _sheetIndex = -1;
        private string _newLabel = string.Empty;

        public bool IsOpen => _open;
        public bool SheetOpen => _sheetIndex >= 0;
        public int SheetSpecies => _sheetIndex;

        /// <summary>Row views ever created (pool size): stays small whatever the number of entries.</summary>
        public int CreatedRowViews { get; private set; }
        public int RowCount => _layout.Count;
        public float ContentHeight => _layout.ContentHeight;
        public bool ReduceMotion { get; set; }

        private void Awake()
        {
            _content.SetActive(false);
            _headerTemplate.gameObject.SetActive(false);
            _cardRowTemplate.gameObject.SetActive(false);
        }

        public void Open(CodexService codex, ILocalization text, ICodexActions actions)
        {
            _codex = codex;
            _text = text;
            _actions = actions;
            if (!_listening)
            {
                _listening = true;
                _back.onClick.AddListener(OnBackPressed);
            }

            ReleaseAll();
            _codex.Refresh();
            _title.text = Text(UiTextKeys.CodexTitle);
            _sheetVariantsTitle.text = Text(UiTextKeys.CodexVariants);
            _newLabel = Text(UiTextKeys.CodexNew);
            for (int i = 0; i < _slots.Length; i++)
                _slots[i].SetNewLabel(_newLabel);
            BindProgress();
            BuildRows();

            _content.SetActive(true);
            _safeFitter.Fit(_safeArea);
            _sheet.SetActive(false);
            _sheetIndex = -1;
            _open = true;
            _closing = false;
            _fade = 0f;
            _group.alpha = ReduceMotion ? 1f : 0f;
            _listContent.anchoredPosition = Vector2.zero;
            _scroll.velocity = Vector2.zero;
            UpdateRows(true);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_open)
                return;
            _safeFitter.Fit(_safeArea);
            if (_closing)
            {
                _fade -= unscaledDeltaTime / FadeSeconds;
                _group.alpha = Mathf.Clamp01(_fade);
                if (_fade <= 0f || ReduceMotion)
                    FinishClose();
                return;
            }
            if (_fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + unscaledDeltaTime / FadeSeconds);
                _group.alpha = ReduceMotion ? 1f : _fade;
            }
            if (_sheetIndex < 0)
                UpdateRows(false);
        }

        /// <summary>Card tap: discovered species open their sheet; unknown ones keep their mystery.</summary>
        public void OnCardPressed(CodexCardView card)
        {
            if (!_open || _closing || !card.Discovered)
                return;
            OpenSheet(card.CodexIndex);
        }

        public void OpenSheet(int codexIndex)
        {
            var entry = _codex.GetSpecies(codexIndex);
            if (!entry.Discovered)
                return;
            _sheetIndex = codexIndex;
            _sheet.SetActive(true);
            _sheetName.text = Text(entry.DisplayKey);
            _numbers.SetCount(_sheetOwned, entry.Owned, 'x');
            BindMastery(codexIndex);
            _sheetPortrait.sprite = PortraitOf(codexIndex, entry.Species);
            _sheetPortrait.enabled = _sheetPortrait.sprite != null;

            int count = _codex.GetVariantCount(codexIndex);
            int slotRows = (Mathf.Min(count, _slots.Length) + SheetColumns - 1) / SheetColumns;
            _sheetContent.sizeDelta = new Vector2(_sheetContent.sizeDelta.x, SheetSlotsTop + slotRows * SheetSlotPitch + 80f);
            _sheetContent.anchoredPosition = Vector2.zero;
            _sheetScroll.velocity = Vector2.zero;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i >= count)
                {
                    _slots[i].Clear();
                    continue;
                }
                var variant = _codex.GetVariant(codexIndex, i);
                Sprite picture = null;
                if (variant.Discovered)
                    _visuals.TryGetVariant(variant.Variant, out picture);
                else
                    _visuals.TryGetSilhouette(entry.Species, out picture);
                string name = variant.Discovered ? Text(variant.DisplayKey) : Text(UiTextKeys.CodexUnknown);
                _slots[i].Bind(variant, picture, name, _tokens);
            }
            // Looking at the sheet is what clears "New" (badges shown this time, gone next time).
            _codex.MarkSeen(codexIndex);
        }

        public int SheetMasteryLevel { get; private set; }

        private void BindMastery(int codexIndex)
        {
            bool show = _codex.HasMastery;
            _sheetMastery.SetActive(show);
            if (!show)
                return;
            var mastery = _codex.GetMastery(codexIndex);
            SheetMasteryLevel = mastery.Level;
            _masteryLevelText.SetCount(_sheetMasteryLevel, mastery.Level);
            _masteryXpText.SetRatio(_sheetMasteryXp, mastery.Xp, mastery.XpForNext);
            float width = _sheetMasteryBar.rect.width;
            float height = _sheetMasteryBar.rect.height;
            _sheetMasteryFill.gameObject.SetActive(mastery.Xp > 0);
            _sheetMasteryFill.sizeDelta = new Vector2(Mathf.Max(height, width * mastery.Fraction), _sheetMasteryFill.sizeDelta.y);
        }

        public void CloseSheet()
        {
            if (_sheetIndex < 0)
                return;
            _sheetIndex = -1;
            _sheet.SetActive(false);
            BindProgress();
            UpdateRows(true);
        }

        public void Close()
        {
            if (!_open || _closing)
                return;
            _closing = true;
            if (ReduceMotion)
                FinishClose();
        }

        /// <summary>Prefab builder hook for the mastery row of the species sheet.</summary>
        public void SetMasteryReferences(GameObject row, TMP_Text level, RectTransform bar, RectTransform fill, TMP_Text xp)
        {
            _sheetMastery = row;
            _sheetMasteryLevel = level;
            _sheetMasteryBar = bar;
            _sheetMasteryFill = fill;
            _sheetMasteryXp = xp;
        }

        /// <summary>Scroll the list to a row (tests and "jump to species").</summary>
        public void ScrollTo(float y)
        {
            _scroll.velocity = Vector2.zero;
            float max = Mathf.Max(0f, _layout.ContentHeight - _viewport.rect.height);
            _listContent.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, max));
            UpdateRows(false);
        }

        public bool TryGetVisibleCard(int codexIndex, out CodexCardView card)
        {
            for (int r = _boundFirst; r <= _boundLast; r++)
            {
                var row = _cardRowAt[r];
                if (row == null)
                    continue;
                for (int c = 0; c < CodexCardRow.Columns; c++)
                {
                    if (row[c].CodexIndex == codexIndex && row[c].gameObject.activeSelf)
                    {
                        card = row[c];
                        return true;
                    }
                }
            }
            card = null;
            return false;
        }

        public void SetReferences(UiColorTokens tokens, CodexVisualCatalog visuals, GameObject content, CanvasGroup group,
            RectTransform safeArea, Button back,
            TMP_Text title, RectTransform progressBack, RectTransform progressFill, TMP_Text progressLabel, ScrollRect scroll,
            RectTransform viewport, RectTransform listContent, CodexHeaderView headerTemplate, CodexCardRow cardRowTemplate,
            GameObject sheet, ScrollRect sheetScroll, RectTransform sheetContent, Image sheetPortrait, TMP_Text sheetName,
            TMP_Text sheetOwned, TMP_Text sheetVariantsTitle, CodexVariantSlotView[] slots)
        {
            _slots = slots;
            _safeArea = safeArea;
            _sheetScroll = sheetScroll;
            _sheetContent = sheetContent;
            _tokens = tokens;
            _visuals = visuals;
            _content = content;
            _group = group;
            _back = back;
            _title = title;
            _progressBack = progressBack;
            _progressFill = progressFill;
            _progressLabel = progressLabel;
            _scroll = scroll;
            _viewport = viewport;
            _listContent = listContent;
            _headerTemplate = headerTemplate;
            _cardRowTemplate = cardRowTemplate;
            _sheet = sheet;
            _sheetPortrait = sheetPortrait;
            _sheetName = sheetName;
            _sheetOwned = sheetOwned;
            _sheetVariantsTitle = sheetVariantsTitle;
            _slots = slots;
        }

        // ---------------- List ----------------

        private void BuildRows()
        {
            int rows = 0;
            for (int s = 0; s < _codex.SectionCount; s++)
            {
                var section = _codex.GetSection(s);
                rows += 1 + (section.Count + CodexCardRow.Columns - 1) / CodexCardRow.Columns;
            }
            if (_rows.Length < rows)
            {
                _rows = new Row[rows];
                _headerAt = new CodexHeaderView[rows];
                _cardRowAt = new CodexCardRow[rows];
            }

            _layout.Begin(rows);
            int r = 0;
            for (int s = 0; s < _codex.SectionCount; s++)
            {
                var section = _codex.GetSection(s);
                _rows[r] = new Row { Kind = RowKind.Header, Section = s, First = section.Start, Count = 0 };
                _layout.SetHeight(r++, HeaderRowHeight);
                for (int i = 0; i < section.Count; i += CodexCardRow.Columns)
                {
                    _rows[r] = new Row
                    {
                        Kind = RowKind.Cards,
                        Section = s,
                        First = section.Start + i,
                        Count = Mathf.Min(CodexCardRow.Columns, section.Count - i),
                    };
                    _layout.SetHeight(r++, CardRowHeight);
                }
            }
            _listContent.sizeDelta = new Vector2(_listContent.sizeDelta.x, _layout.ContentHeight + 48f);
        }

        private void UpdateRows(bool force)
        {
            float scroll = _listContent.anchoredPosition.y;
            float viewport = _viewport.rect.height;
            if (!force && scroll == _boundScroll && viewport == _boundViewport)
                return;
            _boundScroll = scroll;
            _boundViewport = viewport;

            using (GameProfilerMarkers.UICodexBind.Auto())
            {
                _layout.GetVisibleRange(scroll, viewport, Overscan, out int first, out int last);
                // Release rows that left the window (or all of them on a forced rebind).
                for (int r = _boundFirst; r <= _boundLast; r++)
                {
                    if (force || r < first || r > last)
                        Release(r);
                }
                for (int r = first; r <= last; r++)
                {
                    if (_headerAt[r] == null && _cardRowAt[r] == null)
                        Acquire(r);
                }
                _boundFirst = first;
                _boundLast = last;
            }
        }

        private void Acquire(int r)
        {
            var row = _rows[r];
            if (row.Kind == RowKind.Header)
            {
                var header = _freeHeaders.Count > 0 ? _freeHeaders.Pop() : CreateHeader();
                Place(header.Rect, r);
                header.gameObject.SetActive(true);
                var section = _codex.GetSection(row.Section);
                string title = section.DisplayKey != null ? Text(section.DisplayKey) : string.Empty;
                header.Bind(title, section.Discovered, section.Count);
                _headerAt[r] = header;
                return;
            }

            var cards = _freeCardRows.Count > 0 ? _freeCardRows.Pop() : CreateCardRow();
            for (int c = 0; c < CodexCardRow.Columns; c++)
                cards[c].SetNewLabel(_newLabel);
            Place(cards.Rect, r);
            cards.gameObject.SetActive(true);
            string unknown = Text(UiTextKeys.CodexUnknown);
            for (int c = 0; c < CodexCardRow.Columns; c++)
            {
                if (c >= row.Count)
                {
                    cards[c].Clear();
                    continue;
                }
                int index = row.First + c;
                var entry = _codex.GetSpecies(index);
                var picture = entry.Discovered ? PortraitOf(index, entry.Species) : Silhouette(entry.Species);
                cards[c].Bind(index, entry, picture, entry.Discovered ? Text(entry.DisplayKey) : unknown, _tokens);
            }
            _cardRowAt[r] = cards;
        }

        private void Release(int r)
        {
            if (r < 0 || r >= _headerAt.Length)
                return;
            var header = _headerAt[r];
            if (header != null)
            {
                header.gameObject.SetActive(false);
                _freeHeaders.Push(header);
                _headerAt[r] = null;
            }
            var cards = _cardRowAt[r];
            if (cards != null)
            {
                cards.gameObject.SetActive(false);
                _freeCardRows.Push(cards);
                _cardRowAt[r] = null;
            }
        }

        private void ReleaseAll()
        {
            for (int r = _boundFirst; r <= _boundLast; r++)
                Release(r);
            _boundFirst = 0;
            _boundLast = -1;
            _boundScroll = float.NaN;
        }

        private void Place(RectTransform rect, int row)
        {
            rect.anchoredPosition = new Vector2(0f, -_layout.Top(row));
            rect.sizeDelta = new Vector2(0f, _layout.Height(row));
        }

        private CodexHeaderView CreateHeader()
        {
            var header = Instantiate(_headerTemplate, _listContent, false);
            CreatedRowViews++;
            return header;
        }

        private CodexCardRow CreateCardRow()
        {
            var row = Instantiate(_cardRowTemplate, _listContent, false);
            for (int c = 0; c < CodexCardRow.Columns; c++)
                row[c].Init(this);
            CreatedRowViews++;
            return row;
        }

        // ---------------- Helpers ----------------

        private void BindProgress()
        {
            int found = _codex.DiscoveredSpecies;
            int total = Mathf.Max(1, _codex.TotalSpecies);
            _numbers.SetRatio(_progressLabel, found, _codex.TotalSpecies);
            float width = _progressBack.rect.width;
            float height = _progressBack.rect.height;
            float fill = Mathf.Clamp01(found / (float)total);
            // Rounded ends need at least a full circle; an empty bar shows no fill at all.
            _progressFill.gameObject.SetActive(found > 0);
            _progressFill.sizeDelta = new Vector2(Mathf.Max(height, width * fill), _progressFill.sizeDelta.y);
        }

        /// <summary>The species picture: its first discovered variant in rarity order.</summary>
        private Sprite PortraitOf(int codexIndex, ContentId species)
        {
            int count = _codex.GetVariantCount(codexIndex);
            for (int i = 0; i < count; i++)
            {
                var variant = _codex.GetVariant(codexIndex, i);
                if (variant.Discovered && _visuals.TryGetVariant(variant.Variant, out var sprite))
                    return sprite;
            }
            return Silhouette(species);
        }

        private Sprite Silhouette(ContentId species) => _visuals.TryGetSilhouette(species, out var sprite) ? sprite : null;

        private string Text(string key) => _text != null ? _text.Get(key) : key;

        private void OnBackPressed()
        {
            if (_sheetIndex >= 0)
                CloseSheet();
            else
                Close();
        }

        private void FinishClose()
        {
            _open = false;
            _closing = false;
            ReleaseAll();
            _sheet.SetActive(false);
            _sheetIndex = -1;
            _content.SetActive(false);
            _actions?.CodexClosed();
        }

        private void OnDestroy()
        {
            if (_back != null)
                _back.onClick.RemoveListener(OnBackPressed);
        }
    }
}
