using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_OfflineReturn (UI doc SCREEN 004, Writing Bible "Offline return"): "Welcome back", the Light
    /// earned, one Collect button. No five-page summary. Hidden screens cost nothing: the content is
    /// inactive and the canvas disabled. Animations are ticked by the sanctuary, no Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfflineReturnScreen : MonoBehaviour, IOfflineReturnView
    {
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

        private readonly BigValueTextBuffer _amountText = new BigValueTextBuffer("+");
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
            _title.text = text != null ? text.Get(UiTextKeys.OfflineWelcomeBack) : UiTextKeys.OfflineWelcomeBack;
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
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (_phase == Phase.Closed)
                return;
            _time += unscaledDeltaTime;
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
