using System;
using System.Collections.Generic;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>
    /// One language of player-facing text (LOC_en, LOC_fr). Keys follow the Writing Guide
    /// ("offline.welcome_back", "ui.collect.button"). Values are short, with no em dash.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Localization Table", fileName = "LOC_en")]
    public sealed class LocalizationTableAsset : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            [TextArea(1, 3)] public string value;
        }

        [Tooltip("Two-letter language code, lower case.")]
        public string languageCode = "en";

        public List<Entry> entries = new List<Entry>();

        public bool TryGet(string key, out string value)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].key == key)
                {
                    value = entries[i].value;
                    return true;
                }
            }
            value = null;
            return false;
        }

        public void Set(string key, string value)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].key == key)
                {
                    entries[i] = new Entry { key = key, value = value };
                    return;
                }
            }
            entries.Add(new Entry { key = key, value = value });
        }
    }
}
