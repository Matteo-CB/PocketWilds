using System;
using System.Collections.Generic;

namespace PocketWilds.Core
{
    /// <summary>
    /// Player-facing text by key (Writing and Localization Guide: every visible string uses a key, never
    /// the English sentence). Screens read their labels once when they open, never per frame.
    /// </summary>
    public interface ILocalization
    {
        /// <summary>Two-letter language in use, for example "en" or "fr".</summary>
        string Language { get; }

        /// <summary>Text for the key in the current language, then English, then the key itself.</summary>
        string Get(string key);

        /// <summary>The key has a text (current language or English). Default: <see cref="Get"/> does not answer the key itself.</summary>
        bool Has(string key) => !string.Equals(Get(key), key, StringComparison.Ordinal);

        /// <summary>
        /// The form of a counted text (doc 01 Contenu, section 0: a sentence with {n} exists in singular and plural):
        /// « key.one » when <paramref name="n"/> is singular in the language (1 in English, 0 and 1 in French), else
        /// « key.other »; the key itself when the table has no forms. The {n} stays in the text: the caller fills it.
        /// No allocation once a key has been asked for (<see cref="PluralForms"/> caches the form keys).
        /// </summary>
        string GetPlural(string key, long n)
        {
            string form = PluralForms.Key(key, PluralForms.IsOne(Language, n));
            return Has(form) ? Get(form) : Get(key);
        }
    }

    /// <summary>Plural rules of the shipped languages (CLDR cardinal « one ») and the cached « .one » / « .other » keys.</summary>
    public static class PluralForms
    {
        public const string One = ".one";
        public const string Other = ".other";

        private static readonly Dictionary<string, string[]> Keys = new Dictionary<string, string[]>(StringComparer.Ordinal);

        /// <summary>
        /// The count takes the singular form: French 0 and 1 (« 0 souhait »), English and the other Latin languages
        /// only 1; Japanese, Korean and Chinese have one form (always « other »).
        /// </summary>
        public static bool IsOne(string language, long n)
        {
            if (n < 0)
                n = -n;
            switch (language)
            {
                case "fr":
                    return n <= 1;
                case "ja":
                case "ko":
                case "zh":
                    return false;
                default:
                    return n == 1;
            }
        }

        /// <summary>« key.one » or « key.other », built once per key (main thread only).</summary>
        public static string Key(string key, bool one)
        {
            if (!Keys.TryGetValue(key, out var forms))
            {
                forms = new[] { key + One, key + Other };
                Keys[key] = forms;
            }
            return one ? forms[0] : forms[1];
        }
    }
}
