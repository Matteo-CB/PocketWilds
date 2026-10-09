#nullable enable
using System;
using System.Collections.Generic;

namespace PocketWilds.Services.Localization
{
    /// <summary>
    /// A BCP 47 language tag reduced to what language matching reads: language, script and region. Parsing accepts "-"
    /// or "_", any case, and drops variants, extensions ("-u-mu-celsius") and private use. Legacy codes are mapped to
    /// today's ("in" to "id", "iw" to "he", "no" to "nb", "tl" to "fil", "mo" to "ro"), Cantonese ("yue") reads written
    /// Chinese ("zh", Traditional unless the tag says Simplified), and the extended language of a macrolanguage
    /// ("zh-yue", "zh-cmn") replaces its prefix. This is the matching form, not a full BCP 47 canonicalization.
    /// </summary>
    public readonly struct LanguageTag
    {
        /// <summary>Lower case language subtag, aliases resolved: "en", "pt", "zh", "fil".</summary>
        public readonly string Language;
        /// <summary>Title case script subtag ("Hant") or empty.</summary>
        public readonly string Script;
        /// <summary>Upper case region ("BR") or a UN M.49 area ("419"), or empty.</summary>
        public readonly string Region;

        public LanguageTag(string language, string script, string region)
        {
            Language = language;
            Script = script;
            Region = region;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Language);

        /// <summary>The script, else the one the language and region imply (Chinese: Traditional in TW, HK and MO).</summary>
        public string ScriptOrLikely => Script.Length > 0 ? Script : LanguageMatcher.LikelyScript(Language, Region);

        /// <summary>The region, else the one CLDR's likely subtags give the language (es: ES, pt: BR, zh-Hant: TW).</summary>
        public string RegionOrLikely => Region.Length > 0 ? Region : LanguageMatcher.LikelyRegion(Language, ScriptOrLikely);

        public override string ToString()
        {
            if (IsEmpty)
                return string.Empty;
            string text = Language;
            if (Script.Length > 0)
                text += "-" + Script;
            if (Region.Length > 0)
                text += "-" + Region;
            return text;
        }

        public static bool TryParse(string? tag, out LanguageTag result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(tag))
                return false;
            string[] parts = tag!.Trim().Replace('_', '-').Split('-');
            string language = parts[0].ToLowerInvariant();
            if (language.Length < 2 || language.Length > 8 || !IsAlpha(language) || language == "und" || language == "posix")
                return false;
            string script = string.Empty, region = string.Empty;
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length <= 1)
                    break; // extension or private use: the rest is not language, script or region
                if (i == 1 && part.Length == 3 && IsAlpha(part) && (language == "zh" || language == "ar" || language == "ms"))
                {
                    language = part.ToLowerInvariant(); // extended language subtag of a macrolanguage, e.g. zh-yue
                    continue;
                }
                if (script.Length == 0 && region.Length == 0 && part.Length == 4 && IsAlpha(part))
                    script = char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant();
                else if (region.Length == 0 && part.Length == 2 && IsAlpha(part))
                    region = part.ToUpperInvariant();
                else if (region.Length == 0 && part.Length == 3 && IsDigits(part))
                    region = part;
                // anything else is a variant: ignored
            }
            switch (language)
            {
                case "in": language = "id"; break;
                case "iw": language = "he"; break;
                case "ji": language = "yi"; break;
                case "jw": language = "jv"; break;
                case "mo": language = "ro"; break;
                case "no": language = "nb"; break;
                case "tl": language = "fil"; break;
                case "cmn": language = "zh"; break;
                case "sh":
                    language = "sr";
                    if (script.Length == 0)
                        script = "Latn";
                    break;
                case "yue":
                    language = "zh";
                    if (script.Length == 0)
                        script = "Hant";
                    break;
            }
            result = new LanguageTag(language, script, region);
            return true;
        }

        private static bool IsAlpha(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z'))
                    return false;
            }
            return true;
        }

        private static bool IsDigits(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] < '0' || s[i] > '9')
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Picks the text table for the device's ordered preferred languages (Docs/Design/Localization_Plan.md, « Choisir
    /// la langue sans demander »). Pure C#, run once at boot and when the player changes the language.
    /// <para>Rules, for each device language in order, the first that any table serves wins:</para>
    /// <list type="number">
    /// <item>same language, script and region (pt-BR for pt-BR, zh-TW for zh-Hant-TW);</item>
    /// <item>a regional group: Spanish of Latin America and the United States goes to es-419 (es-MX, es-AR, es-US);</item>
    /// <item>the plain language table (de for de-CH, fr for fr-CA, zh-Hant for zh-HK, zh-TW and zh-MO, zh-Hans for zh-CN);</item>
    /// <item>another region of the same language and script (pt-PT goes to pt-BR when only pt-BR exists);</item>
    /// <item>a close language that reads the same written form (Swiss German to German, Nynorsk to Bokmal).</item>
    /// </list>
    /// <para>Only when no device language matches that way, a Chinese reader gets the other Chinese script rather than
    /// English. Then the fallback (English), else the first table.</para>
    /// </summary>
    public static class LanguageMatcher
    {
        public const string DefaultFallback = "en";

        private const int Exact = 100;
        private const int RegionalGroup = 90;
        private const int PlainLanguage = 80;
        private const int LikelyRegionMatch = 70;
        private const int OtherRegion = 50;
        private const int CloseLanguage = 30;
        private const int OtherScript = 10;

        /// <summary>UN M.49 area 419, Latin America and the Caribbean, by ISO 3166 region.</summary>
        private static readonly HashSet<string> LatinAmerica = new HashSet<string>(StringComparer.Ordinal)
        {
            "AR", "BO", "BR", "BZ", "CL", "CO", "CR", "CU", "DO", "EC", "GT", "HN", "MX", "NI", "PA", "PE", "PR", "PY", "SV",
            "UY", "VE", "AG", "AI", "AW", "BB", "BL", "BQ", "BS", "CW", "DM", "FK", "GD", "GF", "GP", "GS", "GY", "HT", "JM",
            "KN", "KY", "LC", "MF", "MQ", "MS", "SR", "SX", "TC", "TT", "VC", "VG", "VI",
        };

        /// <summary>Languages whose readers read another language's written form natively.</summary>
        private static readonly Dictionary<string, string> Close = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "gsw", "de" }, // Swiss German speakers write and read Standard German
            { "nn", "nb" },  // Norwegian Nynorsk and Bokmal
        };

        /// <summary>
        /// The table for the device's ordered preferred languages, as the instance found in <paramref name="supported"/>.
        /// </summary>
        public static string Match(IReadOnlyList<string>? preferred, IReadOnlyList<string> supported, string fallback = DefaultFallback)
        {
            if (supported == null || supported.Count == 0)
                return fallback;
            var tables = ParseAll(supported);
            if (preferred != null)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    int minimum = pass == 0 ? CloseLanguage : OtherScript;
                    for (int i = 0; i < preferred.Count; i++)
                    {
                        if (!LanguageTag.TryParse(preferred[i], out var device))
                            continue;
                        int best = Best(device, tables, minimum);
                        if (best >= 0)
                            return supported[best];
                    }
                }
            }
            return Fallback(supported, fallback);
        }

        /// <summary>
        /// The player's saved choice when a table serves it (an old "pt" save opens pt-BR), else the device languages
        /// (<see cref="Match"/>). An empty choice means « follow the device ».
        /// </summary>
        public static string Resolve(string? saved, IReadOnlyList<string>? preferred, IReadOnlyList<string> supported, string fallback = DefaultFallback)
        {
            if (!string.IsNullOrWhiteSpace(saved) && supported.Count > 0)
            {
                for (int i = 0; i < supported.Count; i++)
                {
                    if (string.Equals(supported[i], saved, StringComparison.OrdinalIgnoreCase))
                        return supported[i];
                }
                string? near = MatchOne(saved, supported);
                if (near != null)
                    return near;
            }
            return Match(preferred, supported, fallback);
        }

        /// <summary>The table that serves one language tag (same script only), or null.</summary>
        public static string? MatchOne(string? tag, IReadOnlyList<string> supported)
        {
            if (supported == null || supported.Count == 0 || !LanguageTag.TryParse(tag, out var device))
                return null;
            int best = Best(device, ParseAll(supported), CloseLanguage);
            return best >= 0 ? supported[best] : null;
        }

        /// <summary>The matching form of a tag: "pt_br" to "pt-BR", "zh-hant-tw" to "zh-Hant-TW", "in-ID" to "id-ID"; empty if unreadable.</summary>
        public static string Normalize(string? tag) => LanguageTag.TryParse(tag, out var parsed) ? parsed.ToString() : string.Empty;

        /// <summary>How well a table serves a device language: 0 when it does not.</summary>
        public static int Score(in LanguageTag device, in LanguageTag table)
        {
            if (device.IsEmpty || table.IsEmpty)
                return 0;
            if (device.Language != table.Language)
                return Close.TryGetValue(device.Language, out var close) && close == table.Language ? CloseLanguage : 0;
            string deviceScript = device.ScriptOrLikely, tableScript = table.ScriptOrLikely;
            if (deviceScript.Length > 0 && tableScript.Length > 0 && deviceScript != tableScript)
                return OtherScript;
            if (table.Region.Length == 0)
                return PlainLanguage;
            if (device.Region.Length > 0 && device.Region == table.Region)
                return Exact;
            string region = device.RegionOrLikely;
            if (table.Region == "419" && IsInSpanishAmerica(device.Language, region))
                return RegionalGroup;
            if (device.Region.Length == 0 && region == table.Region)
                return LikelyRegionMatch;
            return OtherRegion;
        }

        internal static string LikelyScript(string language, string region)
        {
            switch (language)
            {
                case "zh":
                    return region == "TW" || region == "HK" || region == "MO" ? "Hant" : "Hans";
                case "sr":
                    return "Cyrl";
                default:
                    return string.Empty;
            }
        }

        internal static string LikelyRegion(string language, string script)
        {
            switch (language)
            {
                case "en": return "US";
                case "es": return "ES";
                case "pt": return "BR";
                case "fr": return "FR";
                case "de": return "DE";
                case "it": return "IT";
                case "nl": return "NL";
                case "zh": return script == "Hant" ? "TW" : "CN";
                default: return string.Empty;
            }
        }

        private static bool IsInSpanishAmerica(string language, string region) =>
            region == "419" || LatinAmerica.Contains(region) || (language == "es" && region == "US");

        private static LanguageTag[] ParseAll(IReadOnlyList<string> supported)
        {
            var tables = new LanguageTag[supported.Count];
            for (int i = 0; i < tables.Length; i++)
                LanguageTag.TryParse(supported[i], out tables[i]);
            return tables;
        }

        /// <summary>Index of the best table at or above <paramref name="minimum"/>, the earlier table on a tie; -1 if none.</summary>
        private static int Best(in LanguageTag device, LanguageTag[] tables, int minimum)
        {
            int best = -1, bestScore = minimum - 1;
            for (int i = 0; i < tables.Length; i++)
            {
                int score = Score(device, tables[i]);
                if (score > bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }
            return best;
        }

        private static string Fallback(IReadOnlyList<string> supported, string fallback)
        {
            for (int i = 0; i < supported.Count; i++)
            {
                if (string.Equals(supported[i], fallback, StringComparison.OrdinalIgnoreCase))
                    return supported[i];
            }
            return supported[0];
        }
    }
}
