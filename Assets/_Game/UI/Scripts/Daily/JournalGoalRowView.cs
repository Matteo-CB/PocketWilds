using System;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T36 one goal of the weekly journal (doc 01 Contenu section 5, doc 06 screen 032): the slot's icon on a soft disc,
    /// the goal's sentence, a bar with n/target, its two rewards (5 Gems and 2 tickets, or their Light), then « Réclamer »
    /// once reached or a check once taken. A goal that started ahead (first journal) wears « Tu as déjà commencé ».
    /// Rebound in place; labels change only when their value changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JournalGoalRowView : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private Image _slotIcon;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private RectTransform _bar;
        [SerializeField] private RectTransform _fill;
        [SerializeField] private Image _fillImage;
        [SerializeField] private TMP_Text _progress;
        [SerializeField] private RewardCellView _reward0;
        [SerializeField] private RewardCellView _reward1;

        /// <summary>False when the journal's header shows the goals' reward (the cells stay hidden).</summary>
        [SerializeField] private bool _rewardsInRow;
        [SerializeField] private Button _claim;
        [SerializeField] private TMP_Text _claimLabel;
        [SerializeField] private Image _check;
        [SerializeField] private GameObject _headStart;
        [SerializeField] private TMP_Text _headStartLabel;

        private UiLabel _textLabel;
        private UiLabel _progressLabel;
        private UiLabel _claimText;
        private UiLabel _headStartText;
        private Action<int> _onClaim;
        private int _index;
        private int _shownProgress = -1;
        private int _shownTarget = -1;
        private bool _shownReached;
        private float _pop = -1f;
        private bool _initialized;

        public bool ReduceMotion { get; set; }
        public bool ClaimShown => _claim.gameObject.activeSelf;
        public bool CheckShown => _check.gameObject.activeSelf;
        public bool HeadStartShown => _headStart.activeSelf;
        public Button ClaimButton => _claim;
        public string Text => _text.text;
        public string ProgressText => _progress.text;
        public RectTransform Rect => _root != null ? _root : (RectTransform)transform;
        public RectTransform ClaimPoint => (RectTransform)_claim.transform;

        public void Initialize(int index, Action<int> onClaim)
        {
            _index = index;
            _onClaim = onClaim;
            if (_initialized)
                return;
            _initialized = true;
            _textLabel = new UiLabel(_text);
            _progressLabel = new UiLabel(_progress, 32);
            _claimText = new UiLabel(_claimLabel);
            _headStartText = new UiLabel(_headStartLabel);
            _claim.onClick.AddListener(OnClaim);
        }

        public void SetTexts(string claim, string headStart)
        {
            _claimText.SetString(claim);
            _headStartText.SetString(headStart);
        }

        public void Bind(in JournalGoalRow goal, Sprite slotIcon, RewardIconSet icons, RewardTexts texts, Color progressColor, Color doneColor)
        {
            _textLabel.SetString(goal.Text);
            if (_slotIcon.sprite != slotIcon)
                _slotIcon.sprite = slotIcon;
            int target = Mathf.Max(1, goal.Target);
            int progress = Mathf.Clamp(goal.Progress, 0, target);
            bool reached = goal.Reached || goal.Claimed;
            if (progress != _shownProgress || target != _shownTarget || reached != _shownReached)
            {
                _shownProgress = progress;
                _shownTarget = target;
                _shownReached = reached;
                _progressLabel.SetRatio(reached ? target : progress, target);
                float fraction = reached ? 1f : progress / (float)target;
                float width = _bar.rect.width;
                float height = _bar.rect.height;
                bool any = fraction > 0.001f;
                if (_fill.gameObject.activeSelf != any)
                    _fill.gameObject.SetActive(any);
                _fill.sizeDelta = new Vector2(Mathf.Max(height, width * Mathf.Clamp01(fraction)), _fill.sizeDelta.y);
                _fillImage.color = reached ? doneColor : progressColor;
            }
            // The journal says once what every goal pays (its header); rows keep their cells hidden then.
            bool inRow = _reward0.gameObject.activeSelf || _reward1.gameObject.activeSelf || _rewardsInRow;
            if (inRow)
            {
                BindReward(_reward0, goal.Reward0, icons, texts);
                BindReward(_reward1, goal.Reward1, icons, texts);
            }
            SetActive(_claim.gameObject, goal.Reached && !goal.Claimed);
            SetActive(_check.gameObject, goal.Claimed);
            // The tag waits in the right column; « Réclamer » takes its place once the goal is reached.
            SetActive(_headStart, goal.HeadStart && !goal.Reached && !goal.Claimed);
        }

        public void PlayClaimed()
        {
            if (!ReduceMotion)
                _pop = 0f;
        }

        public void Tick(float deltaTime)
        {
            _reward0.Tick(deltaTime);
            _reward1.Tick(deltaTime);
            if (_pop < 0f)
                return;
            _pop += deltaTime;
            float s = UiCurves.Press(_pop);
            Rect.localScale = new Vector3(s, s, 1f);
            if (_pop >= UiCurves.PressSeconds)
            {
                _pop = -1f;
                Rect.localScale = Vector3.one;
            }
        }

        public void SetShown(bool shown)
        {
            if (gameObject.activeSelf != shown)
                gameObject.SetActive(shown);
        }

        private static void BindReward(RewardCellView view, in RewardCell cell, RewardIconSet icons, RewardTexts texts)
        {
            bool shown = !cell.IsEmpty;
            view.SetShown(shown);
            if (shown)
                view.Bind(cell, icons, texts);
        }

        private void OnClaim() => _onClaim?.Invoke(_index);

        private static void SetActive(GameObject go, bool active)
        {
            if (go.activeSelf != active)
                go.SetActive(active);
        }

        private void OnDestroy()
        {
            if (_initialized)
                _claim.onClick.RemoveListener(OnClaim);
        }

        /// <summary>Prefab builder hook (Editor DailyVisitUiPipeline).</summary>
        public void SetReferences(RectTransform root, Image slotIcon, TMP_Text text, RectTransform bar, RectTransform fill, Image fillImage, TMP_Text progress,
            RewardCellView reward0, RewardCellView reward1, Button claim, TMP_Text claimLabel, Image check, GameObject headStart, TMP_Text headStartLabel)
        {
            _root = root;
            _slotIcon = slotIcon;
            _text = text;
            _bar = bar;
            _fill = fill;
            _fillImage = fillImage;
            _progress = progress;
            _reward0 = reward0;
            _reward1 = reward1;
            _claim = claim;
            _claimLabel = claimLabel;
            _check = check;
            _headStart = headStart;
            _headStartLabel = headStartLabel;
        }
    }
}
