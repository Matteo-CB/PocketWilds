using System;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T36 wish card (doc 01 A08 and section 2.1, doc 06 screen 002): the small card a touch on a wish bubble opens above
    /// the animal. It reads in one glance: the wish's icon and sentence, its progress « n/target » on a bar, what it gives
    /// (Gems, a treat, bond), « Récupérer » once fulfilled (the whole card takes the touch then), « Changer » while the
    /// day's free swap remains. Two soft dots point at the bubble, like the bubble's own trail. The card follows the bubble
    /// inside the safe area (never over the top bar), turns to below the animal when the top is too close, and closes on a
    /// touch beside it (A08) or Back. One pooled panel on its own canvas (sorting order 24: above Upgrades, below the
    /// windows); closed, the canvas is disabled, the content inactive and nothing runs. Ticked by the presenter. Reduce
    /// Motion: fades only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WishCardView : MonoBehaviour, IWishCardView, IMotionSettings
    {
        public const int MaxRewards = 3;

        /// <summary>From the bubble's point to the card's edge, above (canvas units at the 1080 x 2340 reference).</summary>
        public const float Lift = 150f;

        /// <summary>From the bubble's point to the card's top edge when the card turns to below the animal.</summary>
        public const float Drop = 180f;

        public const float SideMargin = 24f;

        /// <summary>Keeps the top bar readable (the Light and Gems chips the claim's motes fly to).</summary>
        public const float TopMargin = 196f;

        public const float BottomMargin = 24f;

        private const float OpenSeconds = 0.2f;
        private const float CloseSeconds = 0.12f;
        private const float PopSeconds = 0.4f;
        private const float PointerInset = 64f;
        private const float BubbleRim = 58f;

        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _space;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Button _outside;
        [SerializeField] private RectTransform _card;
        [SerializeField] private Button _body;
        [SerializeField] private RectTransform _dotFar;
        [SerializeField] private RectTransform _dotNear;
        [SerializeField] private RectTransform _iconRoot;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private RectTransform _bar;
        [SerializeField] private RectTransform _fill;
        [SerializeField] private Image _fillImage;
        [SerializeField] private TMP_Text _progress;
        [SerializeField] private RectTransform[] _rewardCells = Array.Empty<RectTransform>();
        [SerializeField] private Image[] _rewardIcons = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] _rewardLabels = Array.Empty<TMP_Text>();
        [SerializeField] private Button _claim;
        [SerializeField] private TMP_Text _claimLabel;
        [SerializeField] private Button _swap;
        [SerializeField] private TMP_Text _swapLabel;

        [Header("Icons (by WishSystem, by RewardKind, and per item)")]
        [SerializeField] private Sprite[] _systemIcons = Array.Empty<Sprite>();
        [SerializeField] private Sprite _wishIcon;
        [SerializeField] private Sprite _doneIcon;
        [SerializeField] private Sprite[] _kindIcons = Array.Empty<Sprite>();
        [SerializeField] private uint[] _itemIds = Array.Empty<uint>();
        [SerializeField] private Sprite[] _itemIcons = Array.Empty<Sprite>();
        [SerializeField] private Color _progressColor = new Color(1f, 0.69f, 0.37f, 1f);
        [SerializeField] private Color _doneColor = new Color(0.47f, 0.72f, 0.38f, 1f);

        private IWishCardActions _actions;
        private WishCardData _data;
        private bool _listening;
        private float _time = -1f;
        private bool _closing;
        private float _pop = -1f;
        private float _invite;
        private Vector2 _anchor;
        private Vector2 _cardShown;
        private Vector2 _farShown;
        private Vector2 _nearShown;
        private bool _dotsShown = true;
        private bool _layoutKnown;
        private Rect _safeLocal;
        private Rect _safeApplied;
        private Vector2Int _screenApplied;
        private Vector2 _spaceApplied;
        private int _closedFrame = -1;
        private string _textShown;
        private int _progressShown = -1;
        private int _targetShown = -1;
        private int _doneState = -1;
        private int _buttonsShown = -1;
        private bool _claimAtRest = true;
        private readonly UiNumberText _numbers = new UiNumberText();
        private UiNumberText[] _rewardNumbers;

        public bool IsOpen { get; private set; }
        public bool ReduceMotion { get; set; }

        /// <summary>True while the card is above the bubble (false: turned to below the animal).</summary>
        public bool Above { get; private set; } = true;

        /// <summary>The card (or its touch catcher) took the touches of this frame: world taps wait (RefugeHud.IsOverUi).</summary>
        public bool TakesTouch => IsOpen || _closedFrame == Time.frameCount;

        public bool Closing => _closing;
        public bool Animating => _closing || (IsOpen && _time < OpenSeconds) || _pop >= 0f;
        public string TextShown => _text != null ? _text.text : string.Empty;
        public string ProgressText => _progress != null ? _progress.text : string.Empty;
        public int RewardsShown { get; private set; }
        public bool ClaimVisible => _claim != null && _claim.gameObject.activeSelf;
        public bool SwapVisible => _swap != null && _swap.gameObject.activeSelf;
        public Sprite IconShown => _icon != null ? _icon.sprite : null;
        public Button OutsideButton => _outside;
        public Button ClaimButton => _claim;
        public Button SwapButton => _swap;
        public Button BodyButton => _body;
        public RectTransform Card => _card;
        public Canvas CardCanvas => _canvas;

        /// <summary>The camera screen points go through: none for an overlay canvas (the game), the canvas camera otherwise.</summary>
        private Camera EventCamera
        {
            get
            {
                var root = _canvas != null ? _canvas.rootCanvas : null;
                return root == null || root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            }
        }

        /// <summary>Sprite of a reward cell's icon (tests, captures).</summary>
        public Sprite RewardIcon(int cell) => cell >= 0 && cell < _rewardIcons.Length ? _rewardIcons[cell].sprite : null;

        public void Open(in WishCardData data, RewardCell[] rewards, int rewardCount, Vector2 screenPoint, ILocalization text,
            IWishCardActions actions)
        {
            if (_card == null)
                return;
            Listen();
            _actions = actions;
            if (text != null)
            {
                _claimLabel.text = text.Get(DailyVisitText.WishClaim);
                _swapLabel.text = text.Get(DailyVisitText.WishSwap);
            }
            bool wasShown = IsOpen || _closing;
            // A new wish: everything is written once, without the "fulfilled" pop.
            _textShown = null;
            _progressShown = -1;
            _targetShown = -1;
            _doneState = -1;
            _buttonsShown = -1;
            Bind(data, false);
            BindRewards(rewards, rewardCount);
            IsOpen = true;
            _closing = false;
            _pop = -1f;
            _invite = 0f;
            _canvas.enabled = true;
            _content.SetActive(true);
            _group.blocksRaycasts = true;
            _anchor = screenPoint;
            _layoutKnown = false;
            Layout();
            if (wasShown)
            {
                _time = OpenSeconds;
                Apply(1f, true);
            }
            else
            {
                _time = 0f;
                Apply(0f, true);
            }
        }

        public void Refresh(in WishCardData data)
        {
            if (IsOpen)
                Bind(data, true);
        }

        public void Follow(Vector2 screenPoint)
        {
            if (!IsOpen && !_closing)
                return;
            _anchor = screenPoint;
            Layout();
        }

        /// <summary>Closed by the presenter (a claim, a screen opening): no callback.</summary>
        public void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            _closedFrame = Time.frameCount;
            _actions = null;
            _group.blocksRaycasts = false;
            _closing = true;
            _time = 0f;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsOpen && !_closing)
                return;
            // Android Back closes the card (the Input System maps it to Escape).
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                CloseByPlayer();
            if (_closing)
            {
                _time += unscaledDeltaTime;
                float k = Mathf.Clamp01(_time / CloseSeconds);
                Apply(1f - k, false);
                if (k >= 1f)
                    Finish();
                return;
            }
            if (_time < OpenSeconds)
            {
                _time += unscaledDeltaTime;
                Apply(Mathf.Clamp01(_time / OpenSeconds), true);
            }
            TickPop(unscaledDeltaTime);
            TickInvite(unscaledDeltaTime);
        }

        public void Prewarm(ILocalization text)
        {
            if (IsOpen || _closing || _card == null)
                return;
            var sample = new WishCardData
            {
                Text = text != null ? text.Get(DailyVisitText.WishTutorial) : "Wish",
                Progress = 1,
                Target = 2,
                System = WishSystem.Hatching,
                CanSwap = true,
            };
            var rewards = new[]
            {
                new RewardCell(RewardKind.Gems, 3), new RewardCell(RewardKind.Treat, 1), new RewardCell(RewardKind.Bond, 15),
            };
            Open(sample, rewards, rewards.Length, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), text, null);
            _group.alpha = 0f;
            Canvas.ForceUpdateCanvases();
            // The fulfilled look too (check icon, Claim): the first real one never builds a mesh.
            sample.Done = true;
            sample.Progress = 2;
            Bind(sample, false);
            Canvas.ForceUpdateCanvases();
            IsOpen = false;
            _closing = false;
            _time = -1f;
            _pop = -1f;
            _actions = null;
            Finish();
        }

        public bool IsOverCard(Vector2 screenPoint) =>
            IsOpen && _card != null && RectTransformUtility.RectangleContainsScreenPoint(_card, screenPoint, null);

        // ---------------- binding ----------------

        private void Bind(in WishCardData data, bool celebrate)
        {
            bool wasDone = _doneState == 1;
            _data = data;
            string text = data.Text ?? string.Empty;
            if (!string.Equals(text, _textShown))
            {
                _textShown = text;
                _text.text = text;
            }

            int target = Mathf.Max(1, data.Target);
            int progress = data.Done ? target : Mathf.Clamp(data.Progress, 0, target);
            if (progress != _progressShown || target != _targetShown)
            {
                _progressShown = progress;
                _targetShown = target;
                _numbers.SetRatio(_progress, progress, target);
                SetFill(progress / (float)target);
            }

            int done = data.Done ? 1 : 0;
            if (done != _doneState)
            {
                _doneState = done;
                _fillImage.color = data.Done ? _doneColor : _progressColor;
            }
            // The icon follows the wish (a swap changes it) and turns into the check with a heart once fulfilled.
            var icon = data.Done ? _doneIcon : SystemIcon(data.System);
            if (_icon.sprite != icon)
                _icon.sprite = icon;

            // 1: Claim, 2: Swap, 0: none.
            int buttons = data.Done ? 1 : (data.CanSwap ? 2 : 0);
            if (buttons != _buttonsShown)
            {
                _buttonsShown = buttons;
                _claim.gameObject.SetActive(buttons == 1);
                _swap.gameObject.SetActive(buttons == 2);
                RestClaim();
            }
            if (celebrate && data.Done && !wasDone)
                _pop = 0f;
        }

        private void BindRewards(RewardCell[] rewards, int count)
        {
            if (_rewardNumbers == null || _rewardNumbers.Length != _rewardCells.Length)
            {
                _rewardNumbers = new UiNumberText[_rewardCells.Length];
                for (int i = 0; i < _rewardNumbers.Length; i++)
                    _rewardNumbers[i] = new UiNumberText();
            }
            int shown = 0;
            int n = rewards == null ? 0 : Mathf.Min(count, rewards.Length);
            for (int i = 0; i < n && shown < _rewardCells.Length; i++)
            {
                var cell = rewards[i];
                if (cell.IsEmpty)
                    continue;
                _rewardIcons[shown].sprite = RewardIconOf(cell);
                // Light counts in minutes of production (a wish never gives Light; journal goals may one day).
                long amount = cell.Kind == RewardKind.Light ? (long)Math.Round(cell.LightMinutes) : cell.Amount;
                _rewardNumbers[shown].SetCount(_rewardLabels[shown], (int)Math.Min(amount, int.MaxValue), '+');
                shown++;
            }
            for (int i = 0; i < _rewardCells.Length; i++)
            {
                bool on = i < shown;
                if (_rewardCells[i].gameObject.activeSelf != on)
                    _rewardCells[i].gameObject.SetActive(on);
            }
            RewardsShown = shown;
        }

        private Sprite SystemIcon(WishSystem system)
        {
            int index = (int)system;
            if (index >= 0 && index < _systemIcons.Length && _systemIcons[index] != null)
                return _systemIcons[index];
            return _wishIcon;
        }

        private Sprite RewardIconOf(in RewardCell cell)
        {
            if (cell.Item.IsValid)
            {
                for (int i = 0; i < _itemIds.Length && i < _itemIcons.Length; i++)
                {
                    if (_itemIds[i] == cell.Item.Value && _itemIcons[i] != null)
                        return _itemIcons[i];
                }
            }
            int kind = (int)cell.Kind;
            if (kind >= 0 && kind < _kindIcons.Length && _kindIcons[kind] != null)
                return _kindIcons[kind];
            return _wishIcon;
        }

        private void SetFill(float fraction)
        {
            float width = _bar.rect.width;
            float height = _bar.rect.height;
            bool visible = fraction > 0.001f;
            if (_fill.gameObject.activeSelf != visible)
                _fill.gameObject.SetActive(visible);
            _fill.sizeDelta = new Vector2(Mathf.Max(height, width * Mathf.Clamp01(fraction)), _fill.sizeDelta.y);
        }

        // ---------------- placement ----------------

        /// <summary>
        /// Places the card above the bubble's point (its bottom edge <see cref="Lift"/> higher), centered on it and clamped
        /// inside the safe area under the top bar; below the animal when the top is too close; the two dots between the card
        /// and the bubble. Writes transforms only when they move.
        /// </summary>
        private void Layout()
        {
            RefreshSafeArea();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_space, _anchor, EventCamera, out var local))
                return;
            var size = _card.rect.size;
            float half = size.x * 0.5f;
            float minX = _safeLocal.xMin + SideMargin + half;
            float maxX = _safeLocal.xMax - SideMargin - half;
            float x = minX <= maxX ? Mathf.Clamp(local.x, minX, maxX) : _safeLocal.center.x;
            float top = _safeLocal.yMax - TopMargin;
            float bottom = _safeLocal.yMin + BottomMargin;

            bool above;
            float edge; // the card's edge facing the bubble: its bottom when above, its top when below
            if (local.y + Lift + size.y <= top)
            {
                above = true;
                edge = local.y + Lift;
            }
            else if (local.y - Drop - size.y >= bottom)
            {
                above = false;
                edge = local.y - Drop;
            }
            else
            {
                // No room either way (a tiny screen): as high as the top bar allows, over the bubble.
                above = true;
                edge = Mathf.Max(bottom, top - size.y);
            }
            // The card grows from its pointer side: pivot on the edge facing the bubble.
            bool flipped = above != Above || !_layoutKnown;
            if (flipped)
            {
                Above = above;
                _card.pivot = new Vector2(0.5f, above ? 0f : 1f);
            }
            var cardPosition = new Vector2(x, edge);
            if (flipped || (cardPosition - _cardShown).sqrMagnitude > 0.0625f)
            {
                _cardShown = cardPosition;
                _card.anchoredPosition = cardPosition;
            }

            // The trail: a bigger dot at the card, a smaller one by the bubble.
            float toward = above ? -1f : 1f;
            float rim = local.y - toward * BubbleRim;
            float gap = (edge - rim) * -toward;
            bool dots = gap >= 44f;
            if (dots != _dotsShown || !_layoutKnown)
            {
                _dotsShown = dots;
                _dotFar.gameObject.SetActive(dots);
                _dotNear.gameObject.SetActive(dots);
            }
            if (dots)
            {
                float px = Mathf.Clamp(local.x, x - half + PointerInset, x + half - PointerInset);
                var far = new Vector2(px, edge + toward * 30f);
                var near = new Vector2(Mathf.Lerp(px, local.x, 0.6f), edge + toward * Mathf.Max(56f, gap * 0.74f));
                if (!_layoutKnown || (far - _farShown).sqrMagnitude > 0.0625f)
                {
                    _farShown = far;
                    _dotFar.anchoredPosition = far;
                }
                if (!_layoutKnown || (near - _nearShown).sqrMagnitude > 0.0625f)
                {
                    _nearShown = near;
                    _dotNear.anchoredPosition = near;
                }
            }
            _layoutKnown = true;
        }

        private void RefreshSafeArea()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            var safe = Screen.safeArea;
            var space = _space.rect.size;
            if (safe == _safeApplied && screen == _screenApplied && space == _spaceApplied)
                return;
            _safeApplied = safe;
            _screenApplied = screen;
            _spaceApplied = space;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_space, safe.min, EventCamera, out var min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_space, safe.max, EventCamera, out var max);
            _safeLocal = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>The safe rectangle the card stays in, in the card's space (tests).</summary>
        public Rect SafeLocal
        {
            get
            {
                RefreshSafeArea();
                return _safeLocal;
            }
        }

        // ---------------- motion ----------------

        /// <summary>Opening: fades in and grows from its pointer with a soft overshoot. Closing: fades out, settles a little.</summary>
        private void Apply(float k, bool opening)
        {
            float eased = 1f - (1f - k) * (1f - k);
            _group.alpha = eased;
            float s = 1f;
            if (!ReduceMotion)
                s = opening ? (k >= 1f ? 1f : 0.86f + 0.14f * EaseOutBack(k)) : Mathf.Lerp(0.94f, 1f, k);
            _card.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>The wish was just fulfilled (or swapped): the icon pops once.</summary>
        private void TickPop(float dt)
        {
            if (_pop < 0f)
                return;
            _pop += dt;
            float t = Mathf.Clamp01(_pop / PopSeconds);
            float s = ReduceMotion ? 1f : 1f + 0.28f * Mathf.Sin(t * Mathf.PI) * (1f - 0.4f * t);
            _iconRoot.localScale = new Vector3(s, s, 1f);
            if (t >= 1f)
            {
                _pop = -1f;
                _iconRoot.localScale = Vector3.one;
            }
        }

        /// <summary>A fulfilled wish: « Récupérer » breathes softly, like the egg that can be hatched. Still with Reduce Motion.</summary>
        private void TickInvite(float dt)
        {
            if (_buttonsShown != 1 || ReduceMotion)
            {
                RestClaim();
                return;
            }
            _claimAtRest = false;
            _invite += dt;
            float s = 1f + 0.04f * (0.5f - 0.5f * Mathf.Cos(_invite * 4.2f));
            _claim.transform.localScale = new Vector3(s, s, 1f);
        }

        private void RestClaim()
        {
            if (_claimAtRest)
                return;
            _claimAtRest = true;
            _invite = 0f;
            _claim.transform.localScale = Vector3.one;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private void Finish()
        {
            _closing = false;
            _time = -1f;
            _pop = -1f;
            if (_iconRoot != null)
                _iconRoot.localScale = Vector3.one;
            if (_card != null)
                _card.localScale = Vector3.one;
            RestClaim();
            _content.SetActive(false);
            _canvas.enabled = false;
        }

        // ---------------- touches ----------------

        private void Listen()
        {
            if (_listening)
                return;
            _listening = true;
            _outside.onClick.AddListener(CloseByPlayer);
            _body.onClick.AddListener(OnBody);
            _claim.onClick.AddListener(OnClaim);
            _swap.onClick.AddListener(OnSwap);
        }

        /// <summary>A touch beside the card (doc 01 A08) or Back: the card closes and tells the presenter.</summary>
        private void CloseByPlayer()
        {
            if (!IsOpen)
                return;
            var actions = _actions;
            Close();
            actions?.WishCardClosed();
        }

        /// <summary>A fulfilled wish's card is one big Claim button; otherwise the card just keeps the touch.</summary>
        private void OnBody()
        {
            if (IsOpen && _data.Done)
                OnClaim();
        }

        private void OnClaim()
        {
            if (!IsOpen || !_data.Done)
                return;
            _actions?.ClaimWishFromCard();
        }

        private void OnSwap()
        {
            if (!IsOpen || _data.Done || !_data.CanSwap)
                return;
            // Answered at once: the icon hops while the new wish arrives (the presenter refreshes the card).
            _pop = 0f;
            _actions?.SwapWishFromCard();
        }

        private void OnDestroy()
        {
            if (!_listening)
                return;
            _outside.onClick.RemoveListener(CloseByPlayer);
            _body.onClick.RemoveListener(OnBody);
            _claim.onClick.RemoveListener(OnClaim);
            _swap.onClick.RemoveListener(OnSwap);
        }

        /// <summary>Prefab builder hook (Editor UiPipeline.BuildHudPrefab).</summary>
        public void SetReferences(Canvas canvas, RectTransform space, GameObject content, CanvasGroup group, Button outside,
            RectTransform card, Button body, RectTransform dotFar, RectTransform dotNear, RectTransform iconRoot, Image icon,
            TMP_Text text, RectTransform bar, RectTransform fill, Image fillImage, TMP_Text progress, RectTransform[] rewardCells,
            Image[] rewardIcons, TMP_Text[] rewardLabels, Button claim, TMP_Text claimLabel, Button swap, TMP_Text swapLabel)
        {
            _canvas = canvas;
            _space = space;
            _content = content;
            _group = group;
            _outside = outside;
            _card = card;
            _body = body;
            _dotFar = dotFar;
            _dotNear = dotNear;
            _iconRoot = iconRoot;
            _icon = icon;
            _text = text;
            _bar = bar;
            _fill = fill;
            _fillImage = fillImage;
            _progress = progress;
            _rewardCells = rewardCells;
            _rewardIcons = rewardIcons;
            _rewardLabels = rewardLabels;
            _claim = claim;
            _claimLabel = claimLabel;
            _swap = swap;
            _swapLabel = swapLabel;
        }

        /// <summary>Prefab builder hook: icons by WishSystem (WishIcons names), by RewardKind, and per item (a biome's treat).</summary>
        public void SetIcons(Sprite[] systemIcons, Sprite wishIcon, Sprite doneIcon, Sprite[] kindIcons, uint[] itemIds, Sprite[] itemIcons,
            Color progressColor, Color doneColor)
        {
            _systemIcons = systemIcons;
            _wishIcon = wishIcon;
            _doneIcon = doneIcon;
            _kindIcons = kindIcons;
            _itemIds = itemIds;
            _itemIcons = itemIcons;
            _progressColor = progressColor;
            _doneColor = doneColor;
        }
    }
}
