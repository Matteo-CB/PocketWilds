using System;
using PocketWilds.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T48B the weekend chip on the right rail (IWeekendHud), in the Event button's slot: the cream circle, the weekend's icon
    /// inside a full warm ring. Saved hidden; RefugeController shows it while a special weekend runs. A tap opens its sheet.
    /// </summary>
    public sealed partial class RefugeHud : IWeekendHud
    {
        [Header("T48B weekend chip: the cream circle in the Event slot, the weekend's icon (one sprite per kind, 1 to 6)")]
        [SerializeField] private Button _weekendButton;
        [SerializeField] private RectTransform _weekendRoot;
        [SerializeField] private Image _weekendIcon;
        [SerializeField] private Sprite[] _weekendIcons = Array.Empty<Sprite>();

        private IWeekendHudActions _weekendActions;
        private int _weekendKind;

        public bool WeekendVisible => _weekendRoot != null && _weekendRoot.gameObject.activeSelf;

        /// <summary>The weekend kind on the chip (0 when hidden).</summary>
        public int WeekendKindShown => WeekendVisible ? _weekendKind : 0;

        public Button WeekendButton => _weekendButton;

        public void BindWeekend(IWeekendHudActions actions)
        {
            _weekendActions = actions;
            if (_weekendButton == null)
                return;
            _weekendButton.onClick.RemoveListener(OnWeekendPressed);
            _weekendButton.onClick.AddListener(OnWeekendPressed);
        }

        public void SetWeekend(bool visible, int kind)
        {
            if (_weekendRoot == null)
                return;
            if (visible && kind != _weekendKind)
            {
                _weekendKind = kind;
                int at = kind - 1;
                if (_weekendIcon != null && at >= 0 && at < _weekendIcons.Length && _weekendIcons[at] != null)
                    _weekendIcon.sprite = _weekendIcons[at];
            }
            if (_weekendRoot.gameObject.activeSelf == visible)
                return;
            _weekendRoot.gameObject.SetActive(visible);
            LayoutRail();
        }

        private void OnWeekendPressed() => _weekendActions?.OpenWeekend();

        /// <summary>Prefab builder hook for the weekend chip (Editor SeasonUiPipeline.AddHudWeekend).</summary>
        public void SetWeekendReferences(Button button, RectTransform root, Image icon, Sprite[] icons)
        {
            _weekendButton = button;
            _weekendRoot = root;
            _weekendIcon = icon;
            _weekendIcons = icons ?? Array.Empty<Sprite>();
        }
    }
}
