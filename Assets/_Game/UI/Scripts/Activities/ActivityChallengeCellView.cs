using System;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T37 one of the 10 challenges of the selected activity (doc 01 Contenu section 12.0: fixed parameters, 3 stars each,
    /// they open in order): its number, its three small stars, soft and grey while locked, outlined when picked. Bound
    /// when its model changes; the number is written once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActivityChallengeCellView : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private Image _outline;
        [SerializeField] private TMP_Text _number;
        [SerializeField] private Image[] _stars = Array.Empty<Image>();
        [SerializeField] private GameObject _lock;

        private Action<int> _onTap;
        private int _index;
        private int _starsShown = -1;
        private int _style = -1;
        private bool _initialized;

        public int Index => _index;
        public Button Button => _button;
        public bool Locked { get; private set; }
        public bool Picked => _outline != null && _outline.gameObject.activeSelf;
        public int StarsShown => _starsShown < 0 || Locked ? 0 : _starsShown;
        public RectTransform Rect => _root != null ? _root : (RectTransform)transform;

        public void Initialize(int index, Action<int> onTap)
        {
            _index = index;
            _onTap = onTap;
            if (_initialized)
                return;
            _initialized = true;
            new UiLabel(_number, 8).SetNumber(index + 1);
            if (_button != null)
                _button.onClick.AddListener(OnTap);
        }

        public void SetShown(bool shown)
        {
            if (gameObject.activeSelf != shown)
                gameObject.SetActive(shown);
        }

        public void Invalidate()
        {
            _starsShown = -1;
            _style = -1;
        }

        public void Bind(in ChallengeCellModel cell, bool picked, in ActivityHubColors colors)
        {
            int stars = cell.Locked ? 0 : Mathf.Clamp(cell.Stars, 0, 3);
            int style = (cell.Locked ? 1 : 0) | (picked ? 2 : 0);
            if (style != _style)
            {
                _style = style;
                Locked = cell.Locked;
                _background.color = cell.Locked ? colors.CardLocked : picked ? colors.Warm : colors.Card;
                _number.color = cell.Locked ? colors.TextOff : colors.Text;
                if (_outline.gameObject.activeSelf != picked)
                    _outline.gameObject.SetActive(picked);
                _outline.color = colors.Selected;
                for (int i = 0; i < _stars.Length; i++)
                    if (_stars[i].gameObject.activeSelf == cell.Locked)
                        _stars[i].gameObject.SetActive(!cell.Locked);
                if (_lock != null && _lock.activeSelf != cell.Locked)
                    _lock.SetActive(cell.Locked);
                _starsShown = -1;
            }
            if (stars != _starsShown && !cell.Locked)
            {
                _starsShown = stars;
                for (int i = 0; i < _stars.Length; i++)
                    _stars[i].color = i < stars ? colors.StarOn : colors.StarOff;
            }
        }

        private void OnTap() => _onTap?.Invoke(_index);

        private void OnDestroy()
        {
            if (_initialized && _button != null)
                _button.onClick.RemoveListener(OnTap);
        }

        /// <summary>Prefab builder hook (Editor ActivityUiPipeline).</summary>
        public void SetReferences(RectTransform root, Button button, Image background, Image outline, TMP_Text number, Image[] stars, GameObject lockMark)
        {
            _root = root;
            _button = button;
            _background = background;
            _outline = outline;
            _number = number;
            _stars = stars;
            _lock = lockMark;
        }
    }
}
