using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_RewardSummary (screen 028, doc 02 Œufs section 5, doc 05 Sensations 30): the card at the end of a × 3 or
    /// × 10 hatch. One cell per newborn in a grid (one row for three, two rows of five for ten): its portrait in the
    /// frame of its rarity (each rarity has its own motif), its name, its stars and rarity word, and the « Nouveau »
    /// badge for a new species or color. Rarity never rests on color alone. The cells pop in one after another
    /// (fade only with reduced motion); one button closes it. Built by EggUiPipeline, no allocation after Prewarm.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardSummaryScreen : MonoBehaviour, IRewardSummaryView, IMotionSettings
    {
        public const int MaxCells = 10;
        public const int MaxStars = 6;
        private const int Columns = 5;
        private const float CellPop = 0.22f;
        private const float CellStagger = 0.05f;

        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _card;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private RectTransform[] _cells = System.Array.Empty<RectTransform>();
        [SerializeField] private CanvasGroup[] _cellGroups = System.Array.Empty<CanvasGroup>();
        [SerializeField] private Image[] _frames = System.Array.Empty<Image>();
        [SerializeField] private Image[] _portraits = System.Array.Empty<Image>();
        [SerializeField] private TMP_Text[] _names = System.Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] _rarityWords = System.Array.Empty<TMP_Text>();
        [SerializeField] private Image[] _badges = System.Array.Empty<Image>();
        /// <summary>MaxStars per cell, cell after cell.</summary>
        [SerializeField] private Image[] _stars = System.Array.Empty<Image>();
        [SerializeField] private GameObject[] _newBadges = System.Array.Empty<GameObject>();
        [SerializeField] private TMP_Text[] _newLabels = System.Array.Empty<TMP_Text>();
        [SerializeField] private Button _continue;
        [SerializeField] private TMP_Text _continueLabel;
        [SerializeField] private float _cellPitchX = 196f;
        [SerializeField] private float _cellPitchY = 330f;
        [SerializeField] private float _gridY = 20f;
        [SerializeField] private float _starPitch = 30f;
        [SerializeField] private UiRaritySet _rarity;
        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private CodexVisualCatalog _catalog;

        private readonly WindowMotion _motion = new WindowMotion(0.2f);
        private IRewardSummaryActions _actions;
        private bool _initialized;
        private int _count;
        private float _time = -1f;

        public bool IsOpen => _motion.IsOpen && !_motion.Closing;
        public bool ReduceMotion { get => _motion.ReduceMotion; set => _motion.ReduceMotion = value; }
        public int CellsShown => _count;
        public Button ContinueButton => _continue;
        public string NameText(int index) => _names[index].text;
        public string RarityText(int index) => _rarityWords[index].text;
        public bool NewShown(int index) => _newBadges[index].activeSelf;

        public int StarsShown(int index)
        {
            int n = 0;
            for (int s = 0; s < MaxStars; s++)
                if (_stars[index * MaxStars + s].gameObject.activeSelf)
                    n++;
            return n;
        }

        public void Open(SummaryEntry[] entries, int count, ILocalization text, IRewardSummaryActions actions)
        {
            Initialize();
            _actions = actions;
            Bind(entries, count, text);
            _motion.Show();
            _time = 0f;
            Apply(ReduceMotion ? float.MaxValue : 0f);
        }

        private void Bind(SummaryEntry[] entries, int count, ILocalization text)
        {
            _count = Mathf.Min(count, _cells.Length);
            _title.text = text.Get(EggText.SummaryTitle);
            _continueLabel.text = text.Get(EggText.SummaryContinue);
            string newWord = text.Get(EggText.SummaryNew);
            int rows = _count > Columns ? 2 : 1;
            for (int i = 0; i < _cells.Length; i++)
            {
                bool shown = i < _count;
                if (_cells[i].gameObject.activeSelf != shown)
                    _cells[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                // One row for three, two rows of five for ten; each row centered.
                int row = rows == 2 ? i / Columns : 0;
                int inRow = rows == 2 ? (row == 0 ? Columns : _count - Columns) : _count;
                int column = rows == 2 ? i % Columns : i;
                float x = (column - (inRow - 1) * 0.5f) * _cellPitchX;
                float y = _gridY + (rows == 2 ? (row == 0 ? 0.5f : -0.5f) * _cellPitchY : 0f);
                _cells[i].anchoredPosition = new Vector2(x, y);

                ref readonly var entry = ref entries[i];
                var color = RarityColor(entry.Rarity);
                _frames[i].sprite = _rarity != null ? _rarity.Frame(entry.Rarity) : null;
                _frames[i].color = color;
                _badges[i].sprite = _rarity != null ? _rarity.Badge(entry.Rarity) : null;
                _badges[i].color = color;
                Sprite portrait = null;
                if (_catalog != null && !_catalog.TryGetVariant(entry.Variant, out portrait))
                    _catalog.TryGetSilhouette(entry.Species, out portrait);
                _portraits[i].sprite = portrait;
                _portraits[i].enabled = portrait != null;
                _names[i].text = entry.Name;
                _rarityWords[i].text = entry.RarityWord;
                int stars = Mathf.Min(MaxStars, (int)entry.Rarity + 1);
                float start = -(stars - 1) * _starPitch * 0.5f;
                for (int s = 0; s < MaxStars; s++)
                {
                    var star = _stars[i * MaxStars + s];
                    bool on = s < stars;
                    if (star.gameObject.activeSelf != on)
                        star.gameObject.SetActive(on);
                    if (!on)
                        continue;
                    star.color = color;
                    var position = star.rectTransform.anchoredPosition;
                    star.rectTransform.anchoredPosition = new Vector2(start + s * _starPitch, position.y);
                }
                if (_newBadges[i].activeSelf != entry.New)
                    _newBadges[i].SetActive(entry.New);
                if (entry.New)
                    _newLabels[i].text = newWord;
            }
        }

        private Color RarityColor(RarityTier rarity)
        {
            if (_tokens == null || _tokens.rarity == null || _tokens.rarity.Length == 0)
                return Color.white;
            return _tokens.rarity[Mathf.Clamp((int)rarity, 0, _tokens.rarity.Length - 1)];
        }

        public void Close() => _motion.Hide();

        public void Prewarm(ILocalization text)
        {
            if (_motion.IsOpen)
                return;
            Initialize();
            var entries = new SummaryEntry[_cells.Length];
            for (int i = 0; i < entries.Length; i++)
                entries[i] = new SummaryEntry { Name = "Prewarm", RarityWord = text.Get(FeedbackText.RarityKey((RarityTier)(i % 6))),
                    Rarity = (RarityTier)(i % 6), New = true };
            Bind(entries, entries.Length, text);
            _motion.Show();
            Canvas.ForceUpdateCanvases();
            _motion.HideNow();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_motion.IsOpen)
                return;
            if (_motion.BackPressed)
                OnContinue();
            _motion.Tick(unscaledDeltaTime);
            if (_time < 0f)
                return;
            _time += unscaledDeltaTime;
            Apply(ReduceMotion ? float.MaxValue : _time);
            if (_time >= CellPop + CellStagger * MaxCells)
                _time = -1f;
        }

        /// <summary>Each cell pops in after the one before it: 0.85 to 1.08 and back to 1.</summary>
        private void Apply(float time)
        {
            for (int i = 0; i < _count; i++)
            {
                float k = Mathf.Clamp01((time - i * CellStagger) / CellPop);
                float scale = k < 0.6f ? Mathf.Lerp(0.85f, 1.08f, k / 0.6f) : Mathf.Lerp(1.08f, 1f, (k - 0.6f) / 0.4f);
                if (ReduceMotion)
                    scale = 1f;
                _cells[i].localScale = new Vector3(scale, scale, 1f);
                _cellGroups[i].alpha = Mathf.Clamp01(k * 2f);
            }
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _motion.Bind(_canvas, _content, _group, _card);
            _continue.onClick.AddListener(OnContinue);
        }

        private void OnContinue()
        {
            if (!_motion.Closing)
                _actions?.CloseSummary();
        }

        private void OnDestroy()
        {
            if (_initialized)
                _continue.onClick.RemoveListener(OnContinue);
        }

        /// <summary>Prefab builder hook (Editor EggUiPipeline).</summary>
        public void SetReferences(Canvas canvas, GameObject content, CanvasGroup group, RectTransform card, TMP_Text title, RectTransform[] cells,
            CanvasGroup[] cellGroups, Image[] frames, Image[] portraits, TMP_Text[] names, TMP_Text[] rarityWords, Image[] badges, Image[] stars,
            GameObject[] newBadges, TMP_Text[] newLabels, Button continueButton, TMP_Text continueLabel, float cellPitchX, float cellPitchY,
            float gridY, float starPitch, UiRaritySet rarity, UiColorTokens tokens, CodexVisualCatalog catalog)
        {
            _canvas = canvas;
            _content = content;
            _group = group;
            _card = card;
            _title = title;
            _cells = cells;
            _cellGroups = cellGroups;
            _frames = frames;
            _portraits = portraits;
            _names = names;
            _rarityWords = rarityWords;
            _badges = badges;
            _stars = stars;
            _newBadges = newBadges;
            _newLabels = newLabels;
            _continue = continueButton;
            _continueLabel = continueLabel;
            _cellPitchX = cellPitchX;
            _cellPitchY = cellPitchY;
            _gridY = gridY;
            _starPitch = starPitch;
            _rarity = rarity;
            _tokens = tokens;
            _catalog = catalog;
        }
    }
}
