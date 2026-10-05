using System;
using System.Collections.Generic;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.UI
{
    /// <summary>
    /// Codex thumbnails rendered by the content pipeline (Pocket Wilds/UI/Render Animal Thumbnails): one
    /// sprite per variant, one silhouette per species made from the capture alpha. Never hand-drawn,
    /// never captured at runtime. Moves to per-biome Addressables with world streaming.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/UI/Codex Visual Catalog", fileName = "DEF_CodexVisuals")]
    public sealed class CodexVisualCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string sourceId;
            public Sprite sprite;
        }

        public List<Entry> variants = new List<Entry>();
        public List<Entry> silhouettes = new List<Entry>();

        private Dictionary<ContentId, Sprite> _variants;
        private Dictionary<ContentId, Sprite> _silhouettes;

        public bool TryGetVariant(ContentId variant, out Sprite sprite)
        {
            EnsureIndex();
            return _variants.TryGetValue(variant, out sprite);
        }

        public bool TryGetSilhouette(ContentId species, out Sprite sprite)
        {
            EnsureIndex();
            return _silhouettes.TryGetValue(species, out sprite);
        }

        public void Set(List<Entry> target, string sourceId, Sprite sprite)
        {
            for (int i = 0; i < target.Count; i++)
            {
                if (target[i].sourceId == sourceId)
                {
                    target[i] = new Entry { sourceId = sourceId, sprite = sprite };
                    _variants = null;
                    return;
                }
            }
            target.Add(new Entry { sourceId = sourceId, sprite = sprite });
            _variants = null;
        }

        private void EnsureIndex()
        {
            if (_variants != null)
                return;
            _variants = Build(variants);
            _silhouettes = Build(silhouettes);
        }

        private static Dictionary<ContentId, Sprite> Build(List<Entry> entries)
        {
            var map = new Dictionary<ContentId, Sprite>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry.sprite != null && !string.IsNullOrEmpty(entry.sourceId))
                    map[ContentId.FromSource(entry.sourceId)] = entry.sprite;
            }
            return map;
        }

        private void OnValidate() => _variants = null;
    }
}
