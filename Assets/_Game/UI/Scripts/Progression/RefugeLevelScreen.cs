using System;
using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_RefugeLevel (doc 02 Améliorations, section 11.4, I-SCR-041), opened from the level badge: the big level
    /// number in its medallion, the bar to the next level and the score, the score by source in plain words, then the
    /// reward track (levels reached ticked, the next ones with what they bring). Rows are built in the prefab and
    /// reused; the card fits the rows shown. Pooled modal above the HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RefugeLevelScreen : MonoBehaviour, IRefugeLevelView, IMotionSettings
    {
        public const float HeaderHeight = 560f;
        public const float SectionHeight = 84f;
        public const float SourceRowHeight = 64f;
        public const float TrackRowHeight = 84f;
        public const float BottomPadding = 56f;

        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _card;
        [SerializeField] private Button _scrim;
        [SerializeField] private Button _close;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _level;
        [SerializeField] private RectTransform _bar;
        [SerializeField] private RectTransform _fill;
        [SerializeField] private TMP_Text _score;
        [SerializeField] private TMP_Text _sourcesHeader;
        [SerializeField] private RectTransform[] _sourceRows = Array.Empty<RectTransform>();
        [SerializeField] private TMP_Text[] _sourceNames = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] _sourceValues = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text _trackHeader;
        [SerializeField] private RectTransform[] _trackRows = Array.Empty<RectTransform>();
        [SerializeField] private Image[] _trackChecks = Array.Empty<Image>();
        [SerializeField] private Image[] _trackDots = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] _trackLevels = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] _trackRewards = Array.Empty<TMP_Text>();

        private readonly WindowMotion _motion = new WindowMotion();
        private IRefugeLevelActions _actions;
        private bool _initialized;

        public bool IsOpen => _motion.IsOpen && !_motion.Closing;
        public bool ReduceMotion { get => _motion.ReduceMotion; set => _motion.ReduceMotion = value; }
        public string LevelText => _level != null ? _level.text : string.Empty;
        public int SourcesShown { get; private set; }
        public int TrackShown { get; private set; }
        public string SourceName(int index) => _sourceNames[index].text;
        public string TrackReward(int index) => _trackRewards[index].text;
        public bool TrackReached(int index) => _trackChecks[index].gameObject.activeSelf;

        public void Open(in RefugeLevelData data, ILocalization text, IRefugeLevelActions actions)
        {
            Initialize();
            _actions = actions;
            Bind(data, text);
            _motion.Show();
        }

        private void Bind(in RefugeLevelData data, ILocalization text)
        {
            _title.text = text.Get(ProgressionText.RefugeLevelTitle);
            _sourcesHeader.text = text.Get(ProgressionText.RefugeLevelScore);
            _trackHeader.text = text.Get(ProgressionText.RefugeLevelRewards);
            _level.text = data.Level.ToString();
            _score.text = data.Score;
            float width = _bar.rect.width;
            float height = _bar.rect.height;
            _fill.gameObject.SetActive(data.Fraction > 0.001f);
            _fill.sizeDelta = new Vector2(Mathf.Max(height, width * Mathf.Clamp01(data.Fraction)), _fill.sizeDelta.y);

            float y = HeaderHeight;
            Place(_sourcesHeader.rectTransform, ref y, SectionHeight);
            SourcesShown = Mathf.Min(data.SourceCount, _sourceRows.Length);
            for (int i = 0; i < _sourceRows.Length; i++)
            {
                bool shown = i < SourcesShown;
                _sourceRows[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                _sourceNames[i].text = data.SourceNames[i];
                _sourceValues[i].text = data.SourceValues[i].ToString();
                Place(_sourceRows[i], ref y, SourceRowHeight);
            }
            y += 20f;
            Place(_trackHeader.rectTransform, ref y, SectionHeight);
            string levelTemplate = text.Get(ProgressionText.UpgradesLevel);
            TrackShown = Mathf.Min(data.TrackCount, _trackRows.Length);
            for (int i = 0; i < _trackRows.Length; i++)
            {
                bool shown = i < TrackShown;
                _trackRows[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                var entry = data.Track[i];
                _trackChecks[i].gameObject.SetActive(entry.Reached);
                _trackDots[i].gameObject.SetActive(!entry.Reached);
                _trackLevels[i].text = levelTemplate.Replace("{n}", entry.Level.ToString());
                _trackLevels[i].color = entry.Reached ? _tokens.textSecondary : _tokens.textPrimary;
                _trackRewards[i].text = entry.Reward;
                _trackRewards[i].color = entry.Reached ? _tokens.textSecondary : _tokens.textPrimary;
                Place(_trackRows[i], ref y, TrackRowHeight);
            }
            _card.sizeDelta = new Vector2(_card.sizeDelta.x, y + BottomPadding);
        }

        /// <summary>Rows stack from the card's top, full width with side margins.</summary>
        private static void Place(RectTransform rect, ref float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(64f, -(y + height));
            rect.offsetMax = new Vector2(-64f, -y);
            y += height;
        }

        public void Close() => _motion.Hide();

        public void Prewarm(ILocalization text)
        {
            if (_motion.IsOpen)
                return;
            Initialize();
            var data = new RefugeLevelData
            {
                Level = 12,
                Fraction = 0.5f,
                Score = "340 / 420",
                SourceCount = RefugeLevelData.MaxSources,
                SourceNames = new string[RefugeLevelData.MaxSources],
                SourceValues = new long[RefugeLevelData.MaxSources],
                TrackCount = RefugeLevelData.MaxTrack,
                Track = new RefugeTrackEntry[RefugeLevelData.MaxTrack],
            };
            for (int i = 0; i < RefugeLevelData.MaxSources; i++)
            {
                data.SourceNames[i] = text.Get(ProgressionText.SourceSpecies);
                data.SourceValues[i] = 120;
            }
            for (int i = 0; i < RefugeLevelData.MaxTrack; i++)
                data.Track[i] = new RefugeTrackEntry { Level = 10 + i, Reached = i < 2, Reward = text.Get(ProgressionText.RewardGems) };
            Bind(data, text);
            _motion.Show();
            Canvas.ForceUpdateCanvases();
            _motion.HideNow();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_motion.IsOpen)
                return;
            if (_motion.BackPressed)
                _actions?.CloseRefugeLevel();
            _motion.Tick(unscaledDeltaTime);
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _motion.Bind(_canvas, _content, _group, _card);
            _scrim.onClick.AddListener(OnClose);
            _close.onClick.AddListener(OnClose);
        }

        private void OnClose()
        {
            if (!_motion.Closing)
                _actions?.CloseRefugeLevel();
        }

        private void OnDestroy()
        {
            if (!_initialized)
                return;
            _scrim.onClick.RemoveListener(OnClose);
            _close.onClick.RemoveListener(OnClose);
        }

        /// <summary>Prefab builder hook (Editor ProgressionUiPipeline).</summary>
        public void SetReferences(UiColorTokens tokens, Canvas canvas, GameObject content, CanvasGroup group, RectTransform card, Button scrim,
            Button close, TMP_Text title, TMP_Text level, RectTransform bar, RectTransform fill, TMP_Text score)
        {
            _tokens = tokens;
            _canvas = canvas;
            _content = content;
            _group = group;
            _card = card;
            _scrim = scrim;
            _close = close;
            _title = title;
            _level = level;
            _bar = bar;
            _fill = fill;
            _score = score;
        }

        /// <summary>Prefab builder hook for the rows.</summary>
        public void SetRowReferences(TMP_Text sourcesHeader, RectTransform[] sourceRows, TMP_Text[] sourceNames, TMP_Text[] sourceValues,
            TMP_Text trackHeader, RectTransform[] trackRows, Image[] trackChecks, Image[] trackDots, TMP_Text[] trackLevels, TMP_Text[] trackRewards)
        {
            _sourcesHeader = sourcesHeader;
            _sourceRows = sourceRows;
            _sourceNames = sourceNames;
            _sourceValues = sourceValues;
            _trackHeader = trackHeader;
            _trackRows = trackRows;
            _trackChecks = trackChecks;
            _trackDots = trackDots;
            _trackLevels = trackLevels;
            _trackRewards = trackRewards;
        }
    }
}
