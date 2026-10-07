using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_LevelReward (doc 02 Améliorations, section 6.3): the small card of a new refuge level. The medallion with
    /// the level, "Level 3", the Gems and the functions that opened, one button to collect. Several levels at once
    /// make one card. The scrim does not close it: the rewards are always collected with a touch.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelRewardScreen : MonoBehaviour, ILevelRewardView, IMotionSettings
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _card;
        [SerializeField] private RectTransform _medal;
        [SerializeField] private TMP_Text _level;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private GameObject _gemsRow;
        [SerializeField] private RectTransform _gemsIcon;
        [SerializeField] private TMP_Text _gems;
        [SerializeField] private TMP_Text _unlocks;
        [SerializeField] private Button _claim;
        [SerializeField] private TMP_Text _claimLabel;

        private readonly WindowMotion _motion = new WindowMotion(0.2f);
        private ILevelRewardActions _actions;
        private bool _initialized;
        private float _pop = -1f;

        public bool IsOpen => _motion.IsOpen && !_motion.Closing;
        public bool ReduceMotion { get => _motion.ReduceMotion; set => _motion.ReduceMotion = value; }
        public string TitleText => _title != null ? _title.text : string.Empty;
        public string GemsText => _gems != null && _gemsRow.activeSelf ? _gems.text : string.Empty;
        public Button ClaimButton => _claim;

        public void Open(in LevelRewardData data, ILocalization text, ILevelRewardActions actions)
        {
            Initialize();
            _actions = actions;
            Bind(data, text);
            _motion.Show();
            _pop = ReduceMotion ? -1f : 0f;
        }

        private void Bind(in LevelRewardData data, ILocalization text)
        {
            _level.text = data.Level.ToString();
            _title.text = data.Title;
            _gemsRow.SetActive(data.Gems > 0);
            _gems.text = text.Get(ProgressionText.RewardGems).Replace("{n}", data.Gems.ToString());
            UiRow.Center(_gemsIcon, _gems, 14f);
            bool unlocks = !string.IsNullOrEmpty(data.Unlocks);
            _unlocks.gameObject.SetActive(unlocks);
            if (unlocks)
                _unlocks.text = data.Unlocks;
            _claimLabel.text = text.Get(ProgressionText.RefugeLevelClaim);
        }

        public void Close() => _motion.Hide();

        public void Prewarm(ILocalization text)
        {
            if (_motion.IsOpen)
                return;
            Initialize();
            Bind(new LevelRewardData { Level = 12, Title = text.Get(ProgressionText.RefugeLevelUp), Gems = 5, Unlocks = text.Get(ProgressionText.FeatureShop) }, text);
            _motion.Show();
            Canvas.ForceUpdateCanvases();
            _motion.HideNow();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_motion.IsOpen)
                return;
            if (_motion.BackPressed)
                OnClaim();
            _motion.Tick(unscaledDeltaTime);
            if (_pop < 0f)
                return;
            // The medallion pops in after the card: 0.95 to 1.12 and back to 1 in half a second.
            _pop += unscaledDeltaTime;
            float t = Mathf.Clamp01(_pop / 0.5f);
            float scale = t < 0.45f ? Mathf.Lerp(0.6f, 1.12f, t / 0.45f) : Mathf.Lerp(1.12f, 1f, (t - 0.45f) / 0.55f);
            _medal.localScale = new Vector3(scale, scale, 1f);
            if (t >= 1f)
                _pop = -1f;
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _motion.Bind(_canvas, _content, _group, _card);
            _claim.onClick.AddListener(OnClaim);
        }

        private void OnClaim()
        {
            if (!_motion.Closing)
                _actions?.ClaimLevelRewards();
        }

        private void OnDestroy()
        {
            if (_initialized)
                _claim.onClick.RemoveListener(OnClaim);
        }

        /// <summary>Prefab builder hook (Editor ProgressionUiPipeline).</summary>
        public void SetReferences(Canvas canvas, GameObject content, CanvasGroup group, RectTransform card, RectTransform medal, TMP_Text level,
            TMP_Text title, GameObject gemsRow, RectTransform gemsIcon, TMP_Text gems, TMP_Text unlocks, Button claim, TMP_Text claimLabel)
        {
            _gemsIcon = gemsIcon;
            _canvas = canvas;
            _content = content;
            _group = group;
            _card = card;
            _medal = medal;
            _level = level;
            _title = title;
            _gemsRow = gemsRow;
            _gems = gems;
            _unlocks = unlocks;
            _claim = claim;
            _claimLabel = claimLabel;
        }
    }
}
