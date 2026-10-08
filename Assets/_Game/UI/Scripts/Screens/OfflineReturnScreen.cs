using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_OfflineReturn (UI doc SCREEN 004, Writing Guide "Offline return"): "Welcome back", the Light
    /// earned, one Collect button. No five-page summary. Hidden screens cost nothing: the content is
    /// inactive and the canvas disabled. Animations are ticked by the refuge, no Update.
    /// T36 (doc 01 Contenu section 6): after 3 days away or more, a gift block under the Light (« Un cadeau pour toi »,
    /// « Un grand cadeau pour toi » from 14 days): its reward cells pop in one by one once the card is open, and up to four
    /// « Pendant ton absence » lines. The card grows to hold it; the same « Récupérer » collects the Light and the gift. No
    /// reproach, no loss. The block is hidden for an ordinary return.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfflineReturnScreen : MonoBehaviour, IOfflineReturnView, IOfflineReturnGiftView, IMotionSettings
    {
        public const int MaxGiftCells = 5;
        public const int MaxGiftLines = 4;
        private const float GiftCellStagger = 0.12f;

        private const float OpenSeconds = 0.26f;
        private const float CloseSeconds = 0.18f;

        private enum Phase : byte
        {
            Closed,
            Opening,
            Open,
            Closing,
        }

        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _card;
        [SerializeField] private RectTransform _icon;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _amount;
        [SerializeField] private Button _collect;
        [SerializeField] private TMP_Text _collectLabel;

        [Header("Return gift (T36)")]
        [SerializeField] private GameObject _giftBlock;
        [SerializeField] private RectTransform _giftIcon;
        [SerializeField] private TMP_Text _giftSubtitle;
        [SerializeField] private RewardCellView[] _giftCells = System.Array.Empty<RewardCellView>();
        [SerializeField] private TMP_Text[] _giftLines = System.Array.Empty<TMP_Text>();
        [SerializeField] private RewardIconSet _giftIconSet = new RewardIconSet();
        [SerializeField] private float _giftCellPitch = 132f;
        [SerializeField] private float _giftBaseHeight = 290f;
        [SerializeField] private float _giftLinePitch = 52f;

        private readonly BigValueTextBuffer _amountText = new BigValueTextBuffer("+");
        private readonly RewardTexts _giftTexts = new RewardTexts();
        private float _cardBaseHeight = -1f;
        private bool _hasGift;
        private int _giftCellCount;
        private IOfflineReturnActions _actions;
        private Phase _phase;
        private float _time;
        private bool _listening;
        private Vector2 _iconHome;
        private bool _iconHomeSet;

        /// <summary>Reduce Motion: fades only, no scale or bob.</summary>
        public bool ReduceMotion { get; set; }

        public bool IsOpen => _phase != Phase.Closed;
        public string AmountText => _amount != null ? _amount.text : string.Empty;
        public string TitleText => _title != null ? _title.text : string.Empty;
        public bool GiftShown => _giftBlock != null && _giftBlock.activeSelf;
        public int GiftCellsShown => _hasGift ? _giftCellCount : 0;
        public RewardCellView GiftCell(int i) => _giftCells[i];
        public string GiftSubtitle => _giftSubtitle != null ? _giftSubtitle.text : string.Empty;
        public float CardHeight => _card != null ? _card.sizeDelta.y : 0f;

        public int GiftLinesShown
        {
            get
            {
                int n = 0;
                foreach (var line in _giftLines)
                    if (line != null && line.gameObject.activeSelf)
                        n++;
                return n;
            }
        }

        private void Awake()
        {
            // Closed by default whatever the prefab was saved with.
            _content.SetActive(false);
            _canvas.enabled = false;
            _phase = Phase.Closed;
        }

        public void Show(BigValue light, ILocalization text, IOfflineReturnActions actions)
        {
            _actions = actions;
            if (!_listening)
            {
                _listening = true;
                _collect.onClick.AddListener(OnCollectPressed);
            }
            string titleKey = _hasGift ? DailyVisitText.ReturnTitle : UiTextKeys.OfflineWelcomeBack;
            _title.text = text != null ? text.Get(titleKey) : titleKey;
            _collectLabel.text = text != null ? text.Get(UiTextKeys.CollectButton) : UiTextKeys.CollectButton;
            _amountText.Update(light);
            _amount.SetText(_amountText.Characters, 0, _amountText.Length);

            if (!_iconHomeSet)
            {
                _iconHome = _icon.anchoredPosition;
                _iconHomeSet = true;
            }
            _canvas.enabled = true;
            _content.SetActive(true);
            _phase = Phase.Opening;
            _time = 0f;
            Apply(0f, 0.86f);
            // The gift's cells pop in one by one once the card is open (the reveal of screen 028).
            for (int i = 0; i < _giftCells.Length; i++)
            {
                _giftCells[i].ReduceMotion = ReduceMotion;
                if (_hasGift && i < _giftCellCount)
                    _giftCells[i].Reveal(OpenSeconds + i * GiftCellStagger);
            }
        }

        /// <summary>The return gift under the Light (doc 01 section 6). Called before <see cref="Show"/>.</summary>
        public void SetGift(in ReturnGiftCard card, RewardCell[] rewards, int rewardCount, ILocalization text)
        {
            if (_giftBlock == null)
                return;
            KeepBaseHeight();
            _hasGift = true;
            _giftTexts.Bind(text);
            string subtitleKey = card.Big ? DailyVisitText.ReturnSubtitleBig : DailyVisitText.ReturnSubtitle;
            _giftSubtitle.text = text != null ? text.Get(subtitleKey) : subtitleKey;
            if (_giftIcon != null)
                UiRow.Center(_giftIcon, _giftSubtitle, 18f);

            _giftCellCount = Mathf.Min(rewards != null ? rewardCount : 0, _giftCells.Length);
            float left = -(_giftCellCount - 1) * 0.5f * _giftCellPitch;
            for (int i = 0; i < _giftCells.Length; i++)
            {
                bool shown = i < _giftCellCount;
                _giftCells[i].SetShown(shown);
                if (!shown)
                    continue;
                _giftCells[i].Bind(rewards[i], _giftIconSet, _giftTexts);
                _giftCells[i].ShowNow();
                var rect = _giftCells[i].Rect;
                rect.anchoredPosition = new Vector2(left + i * _giftCellPitch, rect.anchoredPosition.y);
            }

            int lines = 0;
            lines += ShowLine(lines, card.Line0);
            lines += ShowLine(lines, card.Line1);
            lines += ShowLine(lines, card.Line2);
            lines += ShowLine(lines, card.Line3);
            for (int i = lines; i < _giftLines.Length; i++)
                _giftLines[i].gameObject.SetActive(false);

            float block = _giftBaseHeight + lines * _giftLinePitch;
            var blockRect = (RectTransform)_giftBlock.transform;
            blockRect.sizeDelta = new Vector2(blockRect.sizeDelta.x, block);
            _card.sizeDelta = new Vector2(_card.sizeDelta.x, _cardBaseHeight + block);
            _giftBlock.SetActive(true);
        }

        /// <summary>An ordinary return: no gift block, the card at its own size.</summary>
        public void ClearGift()
        {
            _hasGift = false;
            _giftCellCount = 0;
            if (_giftBlock == null)
                return;
            KeepBaseHeight();
            _giftBlock.SetActive(false);
            _card.sizeDelta = new Vector2(_card.sizeDelta.x, _cardBaseHeight);
        }

        private int ShowLine(int index, string line)
        {
            if (string.IsNullOrEmpty(line) || index >= _giftLines.Length)
                return 0;
            var label = _giftLines[index];
            label.text = line;
            label.gameObject.SetActive(true);
            return 1;
        }

        private void KeepBaseHeight()
        {
            if (_cardBaseHeight < 0f)
                _cardBaseHeight = _card.sizeDelta.y;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (_phase == Phase.Closed)
                return;
            _time += unscaledDeltaTime;
            if (_hasGift)
            {
                for (int i = 0; i < _giftCellCount; i++)
                    _giftCells[i].Tick(unscaledDeltaTime);
            }
            switch (_phase)
            {
                case Phase.Opening:
                {
                    float t = Mathf.Clamp01(_time / OpenSeconds);
                    Apply(t, Mathf.LerpUnclamped(0.86f, 1f, EaseOutBack(t)));
                    if (t >= 1f)
                    {
                        _phase = Phase.Open;
                        _time = 0f;
                    }
                    break;
                }
                case Phase.Open:
                    if (!ReduceMotion)
                    {
                        // The Light icon floats gently: the reward feels alive, never flashy.
                        float bob = Mathf.Sin(_time * 2.6f);
                        _icon.anchoredPosition = new Vector2(0f, _iconHome.y + 6f * bob);
                        float pulse = 1f + 0.03f * bob;
                        _icon.localScale = new Vector3(pulse, pulse, 1f);
                    }
                    break;
                case Phase.Closing:
                {
                    float t = Mathf.Clamp01(_time / CloseSeconds);
                    Apply(1f - t, Mathf.Lerp(1f, 0.94f, t));
                    if (t >= 1f)
                    {
                        _phase = Phase.Closed;
                        _content.SetActive(false);
                        _canvas.enabled = false;
                    }
                    break;
                }
            }
        }

        /// <summary>Prefab builder hook (Editor UiPipeline).</summary>
        public void SetReferences(Canvas canvas, GameObject content, CanvasGroup group, RectTransform card,
            RectTransform icon, TMP_Text title, TMP_Text amount, Button collect, TMP_Text collectLabel)
        {
            _canvas = canvas;
            _content = content;
            _group = group;
            _card = card;
            _icon = icon;
            _title = title;
            _amount = amount;
            _collect = collect;
            _collectLabel = collectLabel;
        }

        /// <summary>Prefab builder hook for the return gift block (Editor DailyVisitUiPipeline).</summary>
        public void SetGiftReferences(GameObject giftBlock, RectTransform giftIcon, TMP_Text giftSubtitle, RewardCellView[] giftCells, TMP_Text[] giftLines,
            RewardIconSet iconSet, float cellPitch, float baseHeight, float linePitch)
        {
            _giftBlock = giftBlock;
            _giftIcon = giftIcon;
            _giftSubtitle = giftSubtitle;
            _giftCells = giftCells;
            _giftLines = giftLines;
            _giftIconSet = iconSet;
            _giftCellPitch = cellPitch;
            _giftBaseHeight = baseHeight;
            _giftLinePitch = linePitch;
        }

        private void OnCollectPressed()
        {
            if (_phase != Phase.Open && _phase != Phase.Opening)
                return;
            _phase = Phase.Closing;
            _time = 0f;
            _actions?.CollectOffline();
        }

        private void Apply(float alpha, float scale)
        {
            _group.alpha = alpha;
            float s = ReduceMotion ? 1f : scale;
            _card.localScale = new Vector3(s, s, 1f);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private void OnDestroy()
        {
            if (_collect != null)
                _collect.onClick.RemoveListener(OnCollectPressed);
        }
    }
}
