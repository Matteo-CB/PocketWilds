using System;
using PocketWilds.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T37 one theme pill of the hub (doc 01 Contenu section 12.0 « Thème par biome »: a row of pills chooses the theme, the
    /// last choice is kept): the biome's soft color dot and its name, filled when chosen. A theme changes only data
    /// (palette, backdrop, objects), never the rules: the pill says nothing more.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActivityThemePillView : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private Image _dot;
        [SerializeField] private TMP_Text _name;

        private Action<int> _onTap;
        private UiLabel _nameText;
        private int _index;
        private int _style = -1;
        private bool _initialized;

        public Button Button => _button;
        public bool Chosen => _style == 1;
        public string NameText => _name != null ? _name.text : string.Empty;
        public RectTransform Rect => _root != null ? _root : (RectTransform)transform;

        public void Initialize(int index, Action<int> onTap)
        {
            _index = index;
            _onTap = onTap;
            if (_initialized)
                return;
            _initialized = true;
            _nameText = new UiLabel(_name, 64);
            if (_button != null)
                _button.onClick.AddListener(OnTap);
        }

        public void SetShown(bool shown)
        {
            if (gameObject.activeSelf != shown)
                gameObject.SetActive(shown);
        }

        public void Invalidate() => _style = -1;

        public void Bind(string name, Color tint, bool chosen, in ActivityHubColors colors)
        {
            _nameText.SetString(name);
            if (_dot.color != tint)
                _dot.color = tint;
            int style = chosen ? 1 : 0;
            if (style == _style)
                return;
            _style = style;
            _background.color = chosen ? colors.Warm : colors.Card;
            _name.color = chosen ? colors.Text : colors.TextSoft;
        }

        private void OnTap() => _onTap?.Invoke(_index);

        private void OnDestroy()
        {
            if (_initialized && _button != null)
                _button.onClick.RemoveListener(OnTap);
        }

        /// <summary>Prefab builder hook (Editor ActivityUiPipeline).</summary>
        public void SetReferences(RectTransform root, Button button, Image background, Image dot, TMP_Text name)
        {
            _root = root;
            _button = button;
            _background = background;
            _dot = dot;
            _name = name;
        }
    }

    /// <summary>
    /// The biome themes the hub can name (doc 01 section 12.0): source id, name key and the soft color of its dot. The ids
    /// are hashed once, on first use. A theme missing here (or without a text yet) reads « Thème {n} ».
    /// </summary>
    [Serializable]
    public sealed class ActivityThemeSet
    {
        [Serializable]
        public struct Entry
        {
            public string sourceId;
            public string nameKey;
            public Color tint;
        }

        public Entry[] entries = Array.Empty<Entry>();
        public Color fallbackTint = new Color(0.89f, 0.86f, 0.81f, 1f);

        [NonSerialized] private ContentId[] _ids;

        /// <summary>The entry of <paramref name="theme"/>, or -1.</summary>
        public int Find(ContentId theme)
        {
            if (_ids == null || _ids.Length != entries.Length)
            {
                _ids = new ContentId[entries.Length];
                for (int i = 0; i < entries.Length; i++)
                    _ids[i] = ContentId.FromSource(entries[i].sourceId);
            }
            for (int i = 0; i < _ids.Length; i++)
            {
                if (_ids[i] == theme)
                    return i;
            }
            return -1;
        }
    }
}
