using System;
using UnityEngine;

namespace PocketWilds.UI
{
    /// <summary>Icon keys of the shop catalog (art keys, item icons) to sprites. Filled by the prefab builder.</summary>
    [Serializable]
    public sealed class ShopIconSet
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public Sprite sprite;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public void Set(Entry[] entries) => _entries = entries ?? Array.Empty<Entry>();

        public Sprite Find(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            for (int i = 0; i < _entries.Length; i++)
            {
                if (string.Equals(_entries[i].key, key, StringComparison.Ordinal))
                    return _entries[i].sprite;
            }
            return null;
        }
    }
}
