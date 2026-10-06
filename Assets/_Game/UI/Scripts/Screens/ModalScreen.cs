using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_Modal (T34A, doc 06 SCREEN 025): one reusable shell for Info, Confirm and Warning. Icon, title, a short
    /// body (Writing Guide: two short sentences for errors), one or two buttons. The card fits its body's height
    /// once per Show (no layout group). Scrim or Back = cancel (Confirm) or OK (Info, Warning). Above every screen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModalScreen : MonoBehaviour, IModalView, IMotionSettings
    {
        private const float FadeSeconds = 0.14f;
        private const float BaseHeight = 470f;
        private const float MinBody = 60f;
        private const float MaxBody = 900f;

        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _card;
        [SerializeField] private Button _scrim;
        [SerializeField] private Image _iconBack;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _body;
        [SerializeField] private Button _confirm;
        [SerializeField] private TMP_Text _confirmLabel;
        [SerializeField] private Button _cancel;
        [SerializeField] private TMP_Text _cancelLabel;
        [Tooltip("Info, Confirm, Warning.")]
        [SerializeField] private Sprite[] _icons;
        [SerializeField] private Color[] _iconBackColors;

        private IModalActions _actions;
        private ModalKind _kind;
        private float _time = -1f;
        private bool _closing;
        private bool _result;
        private bool _bound;
        private Vector2 _confirmHome;

        public bool IsOpen { get; private set; }
        public bool ReduceMotion { get; set; }
        public ModalKind Kind => _kind;
        public string TitleText => _title != null ? _title.text : string.Empty;
        public string BodyText => _body != null ? _body.text : string.Empty;
        public bool CancelVisible => _cancel != null && _cancel.gameObject.activeSelf;
        public Button ConfirmButton => _confirm;
        public Button CancelButton => _cancel;
        public float CardHeight => _card != null ? _card.sizeDelta.y : 0f;

        public void Show(ModalKind kind, string title, string body, string confirm, string cancel, IModalActions actions)
        {
            if (!_bound)
            {
                _bound = true;
                _confirmHome = _confirm.GetComponent<RectTransform>().anchoredPosition;
                _confirm.onClick.AddListener(OnConfirm);
                _cancel.onClick.AddListener(OnCancel);
                _scrim.onClick.AddListener(OnScrim);
            }
            _kind = kind;
            _actions = actions;
            int index = (int)kind;
            _icon.sprite = _icons != null && index < _icons.Length ? _icons[index] : null;
            _iconBack.gameObject.SetActive(_icon.sprite != null);
            if (_iconBackColors != null && index < _iconBackColors.Length)
                _iconBack.color = _iconBackColors[index];
            _title.text = title;
            _body.text = body;
            _confirmLabel.text = confirm;
            bool two = kind == ModalKind.Confirm && !string.IsNullOrEmpty(cancel);
            _cancel.gameObject.SetActive(two);
            if (two)
                _cancelLabel.text = cancel;
            // One button sits centered; two share the row (cancel left, confirm right).
            var confirmRect = _confirm.GetComponent<RectTransform>();
            confirmRect.anchoredPosition = two ? _confirmHome : new Vector2(0f, _confirmHome.y);

            float width = _body.rectTransform.rect.width;
            float bodyHeight = Mathf.Clamp(_body.GetPreferredValues(body, width, 0f).y, MinBody, MaxBody);
            _body.rectTransform.sizeDelta = new Vector2(_body.rectTransform.sizeDelta.x, bodyHeight);
            _card.sizeDelta = new Vector2(_card.sizeDelta.x, BaseHeight + bodyHeight);

            IsOpen = true;
            _closing = false;
            _time = 0f;
            _canvas.enabled = true;
            _content.SetActive(true);
            Apply(0f);
        }

        public void Close()
        {
            if (IsOpen && !_closing)
                Begin(false);
        }

        public void Prewarm(ILocalization localization)
        {
            if (IsOpen)
                return;
            Show(ModalKind.Confirm, localization.Get(FeedbackText.EggOddsTitle), localization.Get(FeedbackText.EggOddsGuarantee),
                localization.Get(FeedbackText.ModalOkButton), localization.Get(FeedbackText.ModalCancelButton), null);
            _group.alpha = 0f;
            Canvas.ForceUpdateCanvases();
            IsOpen = false;
            _closing = false;
            _content.SetActive(false);
            _canvas.enabled = false;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsOpen)
                return;
            if (!_closing && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                OnScrim();
            _time += unscaledDeltaTime;
            float k = Mathf.Clamp01(_time / FadeSeconds);
            Apply(_closing ? 1f - k : k);
            if (_closing && k >= 1f)
            {
                IsOpen = false;
                _closing = false;
                _content.SetActive(false);
                _canvas.enabled = false;
                var actions = _actions;
                _actions = null;
                actions?.ModalClosed(_result);
            }
        }

        private void Apply(float k)
        {
            _group.alpha = k;
            float scale = ReduceMotion ? 1f : Mathf.Lerp(0.94f, 1f, 1f - (1f - k) * (1f - k));
            _card.localScale = new Vector3(scale, scale, 1f);
        }

        private void Begin(bool result)
        {
            _result = result;
            _closing = true;
            _time = 0f;
        }

        private void OnConfirm()
        {
            if (!_closing)
                Begin(true);
        }

        private void OnCancel()
        {
            if (!_closing)
                Begin(false);
        }

        private void OnScrim()
        {
            if (!_closing)
                Begin(_kind != ModalKind.Confirm);
        }

        /// <summary>Prefab builder hook (Editor FeedbackUiPipeline).</summary>
        public void SetReferences(Canvas canvas, GameObject content, CanvasGroup group, RectTransform card, Button scrim, Image iconBack,
            Image icon, TMP_Text title, TMP_Text body, Button confirm, TMP_Text confirmLabel, Button cancel, TMP_Text cancelLabel,
            Sprite[] icons, Color[] iconBackColors)
        {
            _iconBack = iconBack;
            _iconBackColors = iconBackColors;
            _canvas = canvas;
            _content = content;
            _group = group;
            _card = card;
            _scrim = scrim;
            _icon = icon;
            _title = title;
            _body = body;
            _confirm = confirm;
            _confirmLabel = confirmLabel;
            _cancel = cancel;
            _cancelLabel = cancelLabel;
            _icons = icons;
        }

        private void OnDestroy()
        {
            if (!_bound)
                return;
            _confirm.onClick.RemoveListener(OnConfirm);
            _cancel.onClick.RemoveListener(OnCancel);
            _scrim.onClick.RemoveListener(OnScrim);
        }
    }
}
