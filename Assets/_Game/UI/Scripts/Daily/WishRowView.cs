using System;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T36 one wish in Today (doc 01 Contenu section 2, doc 06 screen 031): the animal's portrait (its Codex thumbnail,
    /// a soft paw while none exists) with the wish's icon on a small badge, the sentence, a progress bar with n/target, the
    /// Gems it gives, then « Récupérer » once fulfilled or « Changer » while the day's free swap remains. Up to 9 rows are
    /// built with the prefab and rebound in place (no instantiation); labels change only when their value changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WishRowView : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private Image _portrait;
        [SerializeField] private Image _badgeIcon;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private RectTransform _bar;
        [SerializeField] private RectTransform _fill;
        [SerializeField] private Image _fillImage;
        [SerializeField] private TMP_Text _progress;
        [SerializeField] private RectTransform _gemsRow;
        [SerializeField] private TMP_Text _gems;
        [SerializeField] private Button _claim;
        [SerializeField] private TMP_Text _claimLabel;
        [SerializeField] private Button _swap;
        [SerializeField] private TMP_Text _swapLabel;

        private UiLabel _textLabel;
        private UiLabel _progressLabel;
        private UiLabel _gemsLabel;
        private UiLabel _claimText;
        private UiLabel _swapText;
        private Action<int> _onClaim;
        private Action<int> _onSwap;
        private int _index;
        private int _shownProgress = -1;
        private int _shownTarget = -1;
        private long _shownGems = -1;
        private bool _done;
        private float _pop = -1f;
        private bool _initialized;

        public bool ReduceMotion { get; set; }
        public bool Done => _done;
        public bool ClaimShown => _claim != null && _claim.gameObject.activeSelf;
        public bool SwapShown => _swap != null && _swap.gameObject.activeSelf;
        public Button ClaimButton => _claim;
        public Button SwapButton => _swap;
        public string Text => _text != null ? _text.text : string.Empty;
        public string ProgressText => _progress != null ? _progress.text : string.Empty;
        public Sprite Portrait => _portrait != null ? _portrait.sprite : null;
        public Sprite Icon => _badgeIcon != null ? _badgeIcon.sprite : null;
        public RectTransform Rect => _root != null ? _root : (RectTransform)transform;

        /// <summary>Where a claim's motes and hearts start: the Gems of the row.</summary>
        public RectTransform RewardPoint => _gemsRow != null ? _gemsRow : Rect;

        public void Initialize(int index, Action<int> onClaim, Action<int> onSwap)
        {
            _index = index;
            _onClaim = onClaim;
            _onSwap = onSwap;
            if (_initialized)
                return;
            _initialized = true;
            _textLabel = new UiLabel(_text);
            _progressLabel = new UiLabel(_progress, 32);
            _gemsLabel = new UiLabel(_gems, 32);
            _claimText = new UiLabel(_claimLabel);
            _swapText = new UiLabel(_swapLabel);
            _claim.onClick.AddListener(OnClaim);
            _swap.onClick.AddListener(OnSwap);
        }

        public void SetTexts(string claim, string swap)
        {
            _claimText.SetString(claim);
            _swapText.SetString(swap);
        }

        public void Bind(in TodayWishRow row, bool canSwap, Sprite icon, Sprite portrait, Color progressColor, Color doneColor)
        {
            var card = row.Card;
            _textLabel.SetString(card.Text);
            if (_portrait.sprite != portrait)
                _portrait.sprite = portrait;
            if (_badgeIcon.sprite != icon)
                _badgeIcon.sprite = icon;
            int target = Mathf.Max(1, card.Target);
            int progress = Mathf.Clamp(card.Progress, 0, target);
            if (progress != _shownProgress || target != _shownTarget || card.Done != _done)
            {
                _shownProgress = progress;
                _shownTarget = target;
                _progressLabel.SetRatio(card.Done ? target : progress, target);
                float fraction = card.Done ? 1f : progress / (float)target;
                float width = _bar.rect.width;
                float height = _bar.rect.height;
                bool any = fraction > 0.001f;
                if (_fill.gameObject.activeSelf != any)
                    _fill.gameObject.SetActive(any);
                _fill.sizeDelta = new Vector2(Mathf.Max(height, width * Mathf.Clamp01(fraction)), _fill.sizeDelta.y);
                _fillImage.color = card.Done ? doneColor : progressColor;
            }
            _done = card.Done;
            if (row.Gems != _shownGems)
            {
                _shownGems = row.Gems;
                _gemsLabel.SetNumber(row.Gems);
            }
            bool gems = row.Gems > 0;
            if (_gemsRow.gameObject.activeSelf != gems)
                _gemsRow.gameObject.SetActive(gems);
            SetActive(_claim.gameObject, card.Done);
            SetActive(_swap.gameObject, !card.Done && canSwap);
        }

        /// <summary>The row's badge pops (the hearts burst is the screen's).</summary>
        public void PlayClaimed()
        {
            if (!ReduceMotion)
                _pop = 0f;
        }

        public void Tick(float deltaTime)
        {
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

        private void OnClaim() => _onClaim?.Invoke(_index);

        private void OnSwap()
        {
            if (!ReduceMotion)
                _pop = 0f;
            _onSwap?.Invoke(_index);
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go.activeSelf != active)
                go.SetActive(active);
        }

        private void OnDestroy()
        {
            if (!_initialized)
                return;
            _claim.onClick.RemoveListener(OnClaim);
            _swap.onClick.RemoveListener(OnSwap);
        }

        /// <summary>Prefab builder hook (Editor DailyVisitUiPipeline).</summary>
        public void SetReferences(RectTransform root, Image portrait, Image badgeIcon, TMP_Text text, RectTransform bar, RectTransform fill, Image fillImage,
            TMP_Text progress, RectTransform gemsRow, TMP_Text gems, Button claim, TMP_Text claimLabel, Button swap, TMP_Text swapLabel)
        {
            _root = root;
            _portrait = portrait;
            _badgeIcon = badgeIcon;
            _text = text;
            _bar = bar;
            _fill = fill;
            _fillImage = fillImage;
            _progress = progress;
            _gemsRow = gemsRow;
            _gems = gems;
            _claim = claim;
            _claimLabel = claimLabel;
            _swap = swap;
            _swapLabel = swapLabel;
        }
    }
}
