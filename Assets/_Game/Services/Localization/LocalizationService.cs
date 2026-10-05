#nullable enable
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Services.Localization
{
    /// <summary>
    /// Built once at boot from the shipped tables. Language: the profile setting, else the device
    /// language when a table exists for it, else English. Lookups never allocate (dictionary of strings).
    /// </summary>
    public sealed class LocalizationService : ILocalization
    {
        public const string Fallback = "en";

        private readonly Dictionary<string, string> _current = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _fallback = new Dictionary<string, string>();

        public LocalizationService(IReadOnlyList<LocalizationTableAsset>? tables, string? savedLanguage, SystemLanguage deviceLanguage)
        {
            Language = ResolveLanguage(tables, savedLanguage, deviceLanguage);
            if (tables == null)
                return;
            for (int i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                if (table == null)
                    continue;
                if (table.languageCode == Fallback)
                    Fill(_fallback, table);
                if (table.languageCode == Language)
                    Fill(_current, table);
            }
        }

        public string Language { get; }

        public string Get(string key)
        {
            if (_current.TryGetValue(key, out var text) || _fallback.TryGetValue(key, out text))
                return text;
            GameLog.Warning(LogCategory.Boot, "Missing text: " + key);
            return key;
        }

        public bool Has(string key) => _current.ContainsKey(key) || _fallback.ContainsKey(key);

        public static string ResolveLanguage(IReadOnlyList<LocalizationTableAsset>? tables, string? savedLanguage, SystemLanguage deviceLanguage)
        {
            if (!string.IsNullOrEmpty(savedLanguage) && HasTable(tables, savedLanguage!))
                return savedLanguage!;
            string device = DeviceCode(deviceLanguage);
            return HasTable(tables, device) ? device : Fallback;
        }

        public static string DeviceCode(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.French: return "fr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.ChineseSimplified: return "zh";
                default: return Fallback;
            }
        }

        private static bool HasTable(IReadOnlyList<LocalizationTableAsset>? tables, string code)
        {
            if (tables == null)
                return false;
            for (int i = 0; i < tables.Count; i++)
            {
                if (tables[i] != null && tables[i].languageCode == code)
                    return true;
            }
            return false;
        }

        private static void Fill(Dictionary<string, string> target, LocalizationTableAsset table)
        {
            foreach (var entry in table.entries)
            {
                if (!string.IsNullOrEmpty(entry.key))
                    target[entry.key] = entry.value ?? string.Empty;
            }
        }
    }
}
