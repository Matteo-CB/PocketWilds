using System;
using System.Collections.Generic;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Maps species visual keys to view prefabs and pool sizes (DEF_AnimalVisualCatalog).
    /// Presentation data only. Will resolve through Addressables keys when biomes stream.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Animal Visual Catalog", fileName = "DEF_AnimalVisualCatalog")]
    public sealed class AnimalVisualCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string visualKey;
            public AnimalView prefab;
            [Min(0)] public int softCapacity = 24;
            [Min(1)] public int hardCapacity = 48;
        }

        public List<Entry> entries = new List<Entry>();

        public bool TryGet(string visualKey, out Entry entry)
        {
            foreach (var e in entries)
            {
                if (e != null && e.visualKey == visualKey)
                {
                    entry = e;
                    return true;
                }
            }
            entry = null;
            return false;
        }
    }
}
