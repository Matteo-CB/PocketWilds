using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_HUD_Main (T17 scope): Light chip and the hatch action, numbers and icons only. The chip and
    /// the action sit on their own nested canvases so the ticking counter never rebuilds the button.
    /// States use color tokens and scale, not extra sprites. No Update: ticked by the scene controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SanctuaryHud : MonoBehaviour, ISanctuaryHud
    {
        private const float PulseSeconds = 0.4f;

        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private RectTransform _safeArea;

        [Header("Light chip")]
        [SerializeField] private RectTransform _lightChip;
        [SerializeField] private TMP_Text _lightLabel;
        [SerializeField] private TMP_Text _rateLabel;

        [Header("Hatch action")]
        [SerializeField] private Button _hatchButton;
        [SerializeField] private RectTransform _hatchRoot;
        [SerializeField] private Image _hatchBackground;
        [SerializeField] private Image _eggIcon;
        [SerializeField] private GameObject _costRow;
        [SerializeField] private TMP_Text _costLabel;

        private ISanctuaryActions _actions;
        private BigValueText _light;
        private BigValueText _rate;
        private BigValueText _cost;
        private bool _bound;
        private int _state = -1;
        private bool _freeShown;
        private bool _hatchingNow;
        private float _pulse = -1f;
        private float _breath;
        private Vector2 _chipHome;
        private Rect _appliedSafeArea;
        private Vector2Int _appliedScreen;

        /// <summary>Reduce Motion: no breathing or shaking, states stay readable through color.</summary>
        public bool ReduceMotion { get; set; }

        public bool IsBound => _bound;
        public bool CostVisible => _costRow != null && _costRow.activeSelf;
        public int VisualState => _state;

        public void Bind(ISanctuaryActions actions)
        {
            _actions = actions;
            if (_bound)
                return;
            _bound = true;
            _light = new BigValueText(_lightLabel);
            _rate = new BigValueText(_rateLabel, "+", "/s");
            _cost = new BigValueText(_costLabel);
            _chipHome = _lightChip.anchoredPosition;
            _freeShown = !_costRow.activeSelf;
            _hatchButton.onClick.AddListener(OnHatchPressed);
            FitSafeArea();
        }

        public void SetLight(BigValue light, BigValue perSecond)
        {
            if (!_bound)
                return;
            _light.Set(light);
            _rate.Set(perSecond);
        }

        public void SetEgg(BigValue cost, bool free, bool affordable)
        {
            if (!_bound)
                return;
            if (free != _freeShown)
            {
                // The tutorial egg needs no reading: no price, just the inviting button.
                _freeShown = free;
                _costRow.SetActive(!free);
            }
            if (!free)
                _cost.Set(cost);

            int state = _hatchingNow ? 2 : (affordable ? 1 : 0);
            if (state == _state)
                return;
            _state = state;
            _hatchBackground.color = state == 2 ? _tokens.accentBusy : (state == 1 ? _tokens.accentPrimary : _tokens.accentIdle);
            _costLabel.color = state == 0 ? _tokens.errorSoft : _tokens.textPrimary;
            _eggIcon.color = state == 0 ? new Color(1f, 1f, 1f, 0.8f) : Color.white;
        }

        public void SetHatching(bool hatching)
        {
            _hatchingNow = hatching;
            _state = -1; // colors refresh on the next SetEgg
        }

        public void PulseNotEnough() => _pulse = 0f;

        public void Tick(float unscaledDeltaTime)
        {
            if (!_bound)
                return;
            FitSafeArea();

            float scale = 1f;
            if (!ReduceMotion)
            {
                // An affordable egg breathes softly to invite the touch.
                _breath += unscaledDeltaTime;
                if (_state == 1)
                    scale = 1f + 0.035f * (0.5f - 0.5f * Mathf.Cos(_breath * 4.2f));
            }
            if (_pulse >= 0f)
            {
                // "Not yet": the Light chip shakes, the button dips. Short and soft.
                _pulse += unscaledDeltaTime;
                float decay = Mathf.Clamp01(1f - _pulse / PulseSeconds);
                float shake = ReduceMotion ? 0f : 14f * decay * Mathf.Sin(_pulse * 42f);
                _lightChip.anchoredPosition = _chipHome + new Vector2(shake, 0f);
                scale *= 1f - 0.06f * decay;
                if (decay <= 0f)
                {
                    _pulse = -1f;
                    _lightChip.anchoredPosition = _chipHome;
                }
            }
            var current = _hatchRoot.localScale;
            if (current.x != scale)
                _hatchRoot.localScale = new Vector3(scale, scale, 1f);
        }

        public bool IsOverUi(Vector2 screenPosition) =>
            RectTransformUtility.RectangleContainsScreenPoint(_hatchRoot, screenPosition, null)
            || RectTransformUtility.RectangleContainsScreenPoint(_lightChip, screenPosition, null);

        /// <summary>Prefab builder hook (Editor UiPipeline).</summary>
        public void SetReferences(UiColorTokens tokens, RectTransform safeArea, RectTransform lightChip, TMP_Text lightLabel,
            TMP_Text rateLabel, Button hatchButton, RectTransform hatchRoot, Image hatchBackground, Image eggIcon,
            GameObject costRow, TMP_Text costLabel)
        {
            _tokens = tokens;
            _safeArea = safeArea;
            _lightChip = lightChip;
            _lightLabel = lightLabel;
            _rateLabel = rateLabel;
            _hatchButton = hatchButton;
            _hatchRoot = hatchRoot;
            _hatchBackground = hatchBackground;
            _eggIcon = eggIcon;
            _costRow = costRow;
            _costLabel = costLabel;
        }

        private void FitSafeArea()
        {
            if (_safeArea == null)
                return;
            var screen = new Vector2Int(Screen.width, Screen.height);
            var safe = Screen.safeArea;
            if (safe == _appliedSafeArea && screen == _appliedScreen)
                return;
            _appliedSafeArea = safe;
            _appliedScreen = screen;
            if (screen.x <= 0 || screen.y <= 0)
                return;
            _safeArea.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            _safeArea.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;
        }

        private void OnHatchPressed() => _actions?.RequestHatch();

        private void OnDestroy()
        {
            if (_hatchButton != null)
                _hatchButton.onClick.RemoveListener(OnHatchPressed);
        }
    }
}
