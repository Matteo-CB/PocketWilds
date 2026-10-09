using System;

namespace PocketWilds.Core
{
    /// <summary>CLDR cardinal plural categories. A counted text has one form per category its language uses.</summary>
    public enum PluralCategory : byte
    {
        Zero,
        One,
        Two,
        Few,
        Many,
        Other,
    }

    /// <summary>A set of <see cref="PluralCategory"/> values, one bit each.</summary>
    [Flags]
    public enum PluralCategorySet : byte
    {
        None = 0,
        Zero = 1 << PluralCategory.Zero,
        One = 1 << PluralCategory.One,
        Two = 1 << PluralCategory.Two,
        Few = 1 << PluralCategory.Few,
        Many = 1 << PluralCategory.Many,
        Other = 1 << PluralCategory.Other,
    }

    /// <summary>
    /// CLDR cardinal plural rules (Unicode CLDR plurals.xml, version 46) for whole counts, for the languages the
    /// Localization Plan ships or studies (Docs/Design/Localization_Plan.md). The game counts animals, eggs, days and
    /// Gems: decimal rules (CLDR operands v, f, t) are left out on purpose.
    /// <para>The language is a table code such as "en", "pt-BR", "pt-PT", "zh-Hant" or "es-419", any case, "-" or "_".
    /// Only Portuguese reads the region (pt-PT counts like Italian, every other pt like Brazil). A language with no rule
    /// here counts like English (one for 1, other otherwise): see <see cref="IsKnown"/>.</para>
    /// <para>No allocation: codes are compared in place.</para>
    /// </summary>
    public static class PluralRules
    {
        private enum Rule : byte
        {
            /// <summary>English, German, Dutch, Greek, Turkish, Nordic, Hungarian, Bulgarian, Marathi: one for 1.</summary>
            OneForOne,
            /// <summary>Japanese, Korean, Chinese, Thai, Vietnamese, Indonesian, Malay: one form.</summary>
            OtherOnly,
            /// <summary>Hindi, Bengali, Persian, Gujarati, Kannada: one for 0 and 1.</summary>
            OneForZeroAndOne,
            /// <summary>Spanish, Italian, Catalan, European Portuguese: one for 1, many for exact millions.</summary>
            OneForOneManyMillions,
            /// <summary>French, Brazilian Portuguese: one for 0 and 1, many for exact millions.</summary>
            OneForZeroAndOneManyMillions,
            /// <summary>Russian, Ukrainian, Belarusian: one (1, 21, 101), few (2-4, 22-24), many (0, 5-20, 25-30).</summary>
            EastSlavic,
            /// <summary>Croatian, Serbian, Bosnian: as East Slavic but 0 and 5-20 are other.</summary>
            SouthSlavic,
            /// <summary>Polish: one (1 only), few (2-4, 22-24, not 12-14), many (the rest).</summary>
            Polish,
            /// <summary>Czech, Slovak: one (1), few (2-4), other (the rest; many is for decimals).</summary>
            CzechSlovak,
            /// <summary>Romanian: one (1), few (0, 2-19, 101-119...), other (20, 21, 100...).</summary>
            Romanian,
            /// <summary>Arabic: zero, one, two, few (3-10, 103-110), many (11-99, 111-199), other (100, 101, 102...).</summary>
            Arabic,
            /// <summary>Hebrew: one (1), two (2), other.</summary>
            Hebrew,
            /// <summary>Filipino: other only when the last digit is 4, 6 or 9 (and not 1, 2, 3).</summary>
            Filipino,
        }

        private struct Entry
        {
            public readonly string Code;
            public readonly Rule Rule;

            public Entry(string code, Rule rule)
            {
                Code = code;
                Rule = rule;
            }
        }

        // Base language subtags (ISO 639, lower case), legacy aliases included (in, iw, no, tl).
        private static readonly Entry[] Table =
        {
            new Entry("en", Rule.OneForOne), new Entry("de", Rule.OneForOne), new Entry("nl", Rule.OneForOne),
            new Entry("el", Rule.OneForOne), new Entry("tr", Rule.OneForOne), new Entry("sv", Rule.OneForOne),
            new Entry("da", Rule.OneForOne), new Entry("nb", Rule.OneForOne), new Entry("nn", Rule.OneForOne),
            new Entry("no", Rule.OneForOne), new Entry("fi", Rule.OneForOne), new Entry("et", Rule.OneForOne),
            new Entry("hu", Rule.OneForOne), new Entry("bg", Rule.OneForOne), new Entry("ka", Rule.OneForOne),
            new Entry("az", Rule.OneForOne), new Entry("kk", Rule.OneForOne), new Entry("ur", Rule.OneForOne),
            new Entry("sw", Rule.OneForOne),
            new Entry("ja", Rule.OtherOnly), new Entry("ko", Rule.OtherOnly), new Entry("zh", Rule.OtherOnly),
            new Entry("yue", Rule.OtherOnly), new Entry("th", Rule.OtherOnly), new Entry("vi", Rule.OtherOnly),
            new Entry("id", Rule.OtherOnly), new Entry("in", Rule.OtherOnly), new Entry("ms", Rule.OtherOnly),
            new Entry("lo", Rule.OtherOnly), new Entry("km", Rule.OtherOnly), new Entry("my", Rule.OtherOnly),
            new Entry("hi", Rule.OneForZeroAndOne), new Entry("bn", Rule.OneForZeroAndOne), new Entry("fa", Rule.OneForZeroAndOne),
            new Entry("gu", Rule.OneForZeroAndOne), new Entry("kn", Rule.OneForZeroAndOne), new Entry("mr", Rule.OneForOne),
            new Entry("es", Rule.OneForOneManyMillions), new Entry("it", Rule.OneForOneManyMillions),
            new Entry("ca", Rule.OneForOneManyMillions),
            new Entry("fr", Rule.OneForZeroAndOneManyMillions), new Entry("pt", Rule.OneForZeroAndOneManyMillions),
            new Entry("ru", Rule.EastSlavic), new Entry("uk", Rule.EastSlavic), new Entry("be", Rule.EastSlavic),
            new Entry("hr", Rule.SouthSlavic), new Entry("sr", Rule.SouthSlavic), new Entry("bs", Rule.SouthSlavic),
            new Entry("pl", Rule.Polish),
            new Entry("cs", Rule.CzechSlovak), new Entry("sk", Rule.CzechSlovak),
            new Entry("ro", Rule.Romanian), new Entry("mo", Rule.Romanian),
            new Entry("ar", Rule.Arabic),
            new Entry("he", Rule.Hebrew), new Entry("iw", Rule.Hebrew),
            new Entry("fil", Rule.Filipino), new Entry("tl", Rule.Filipino),
        };

        private static readonly string[] Suffixes = { ".zero", ".one", ".two", ".few", ".many", ".other" };

        /// <summary>The category of a whole count <paramref name="n"/> (negative counts take the form of their absolute value).</summary>
        public static PluralCategory Category(string language, long n)
        {
            ulong v = n < 0 ? (ulong)(-(n + 1)) + 1UL : (ulong)n;
            switch (RuleOf(language))
            {
                case Rule.OtherOnly:
                    return PluralCategory.Other;
                case Rule.OneForZeroAndOne:
                    return v <= 1 ? PluralCategory.One : PluralCategory.Other;
                case Rule.OneForOneManyMillions:
                    if (v == 1)
                        return PluralCategory.One;
                    return IsExactMillions(v) ? PluralCategory.Many : PluralCategory.Other;
                case Rule.OneForZeroAndOneManyMillions:
                    if (v <= 1)
                        return PluralCategory.One;
                    return IsExactMillions(v) ? PluralCategory.Many : PluralCategory.Other;
                case Rule.EastSlavic:
                {
                    ulong d = v % 10, h = v % 100;
                    if (d == 1 && h != 11)
                        return PluralCategory.One;
                    if (d >= 2 && d <= 4 && (h < 12 || h > 14))
                        return PluralCategory.Few;
                    return PluralCategory.Many;
                }
                case Rule.SouthSlavic:
                {
                    ulong d = v % 10, h = v % 100;
                    if (d == 1 && h != 11)
                        return PluralCategory.One;
                    if (d >= 2 && d <= 4 && (h < 12 || h > 14))
                        return PluralCategory.Few;
                    return PluralCategory.Other;
                }
                case Rule.Polish:
                {
                    if (v == 1)
                        return PluralCategory.One;
                    ulong d = v % 10, h = v % 100;
                    if (d >= 2 && d <= 4 && (h < 12 || h > 14))
                        return PluralCategory.Few;
                    return PluralCategory.Many;
                }
                case Rule.CzechSlovak:
                    if (v == 1)
                        return PluralCategory.One;
                    return v >= 2 && v <= 4 ? PluralCategory.Few : PluralCategory.Other;
                case Rule.Romanian:
                {
                    if (v == 1)
                        return PluralCategory.One;
                    ulong h = v % 100;
                    return v == 0 || (h >= 1 && h <= 19) ? PluralCategory.Few : PluralCategory.Other;
                }
                case Rule.Arabic:
                {
                    if (v == 0)
                        return PluralCategory.Zero;
                    if (v == 1)
                        return PluralCategory.One;
                    if (v == 2)
                        return PluralCategory.Two;
                    ulong h = v % 100;
                    if (h >= 3 && h <= 10)
                        return PluralCategory.Few;
                    return h >= 11 ? PluralCategory.Many : PluralCategory.Other;
                }
                case Rule.Hebrew:
                    if (v == 1)
                        return PluralCategory.One;
                    return v == 2 ? PluralCategory.Two : PluralCategory.Other;
                case Rule.Filipino:
                {
                    ulong d = v % 10;
                    return v <= 3 || (d != 4 && d != 6 && d != 9) ? PluralCategory.One : PluralCategory.Other;
                }
                default:
                    return v == 1 ? PluralCategory.One : PluralCategory.Other;
            }
        }

        /// <summary>
        /// The forms a counted text needs in the language for whole counts, « other » always included (it is the
        /// fallback form). The translation checks require exactly these suffixes for every counted key.
        /// </summary>
        public static PluralCategorySet Categories(string language)
        {
            switch (RuleOf(language))
            {
                case Rule.OtherOnly:
                    return PluralCategorySet.Other;
                case Rule.OneForOneManyMillions:
                case Rule.OneForZeroAndOneManyMillions:
                    return PluralCategorySet.One | PluralCategorySet.Many | PluralCategorySet.Other;
                case Rule.EastSlavic:
                case Rule.Polish:
                    return PluralCategorySet.One | PluralCategorySet.Few | PluralCategorySet.Many | PluralCategorySet.Other;
                case Rule.SouthSlavic:
                case Rule.CzechSlovak:
                case Rule.Romanian:
                    return PluralCategorySet.One | PluralCategorySet.Few | PluralCategorySet.Other;
                case Rule.Arabic:
                    return PluralCategorySet.Zero | PluralCategorySet.One | PluralCategorySet.Two | PluralCategorySet.Few |
                           PluralCategorySet.Many | PluralCategorySet.Other;
                case Rule.Hebrew:
                    return PluralCategorySet.One | PluralCategorySet.Two | PluralCategorySet.Other;
                default:
                    return PluralCategorySet.One | PluralCategorySet.Other;
            }
        }

        /// <summary>The key suffix of a form: ".zero", ".one", ".two", ".few", ".many" or ".other".</summary>
        public static string Suffix(PluralCategory category) => Suffixes[(int)category];

        public static bool Contains(this PluralCategorySet set, PluralCategory category) =>
            (set & (PluralCategorySet)(1 << (int)category)) != 0;

        /// <summary>The language has its own rule here (otherwise it counts like English).</summary>
        public static bool IsKnown(string language) => Find(language, out _);

        private static bool IsExactMillions(ulong v) => v != 0 && v % 1000000UL == 0;

        private static Rule RuleOf(string language)
        {
            if (!Find(language, out var rule))
                return Rule.OneForOne;
            // CLDR « pt_PT »: European Portuguese counts 0 as other, like Italian.
            if (rule == Rule.OneForZeroAndOneManyMillions && IsRegion(language, 2, "pt"))
                return Rule.OneForOneManyMillions;
            return rule;
        }

        private static bool Find(string language, out Rule rule)
        {
            rule = Rule.OneForOne;
            if (string.IsNullOrEmpty(language))
                return false;
            int length = BaseLength(language);
            for (int i = 0; i < Table.Length; i++)
            {
                string code = Table[i].Code;
                if (code.Length == length && SameAscii(language, code, length))
                {
                    rule = Table[i].Rule;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Length of the language subtag: up to the first "-" or "_".</summary>
        private static int BaseLength(string tag)
        {
            for (int i = 0; i < tag.Length; i++)
            {
                if (tag[i] == '-' || tag[i] == '_')
                    return i;
            }
            return tag.Length;
        }

        /// <summary>The tag is <paramref name="language"/> with the region PT (any subtag after the language).</summary>
        private static bool IsRegion(string tag, int baseLength, string language)
        {
            if (BaseLength(tag) != baseLength || !SameAscii(tag, language, baseLength))
                return false;
            int start = baseLength + 1;
            while (start < tag.Length)
            {
                int end = start;
                while (end < tag.Length && tag[end] != '-' && tag[end] != '_')
                    end++;
                if (end - start == 2 && Lower(tag[start]) == 'p' && Lower(tag[start + 1]) == 't')
                    return true;
                start = end + 1;
            }
            return false;
        }

        private static bool SameAscii(string tag, string lowerCode, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (Lower(tag[i]) != lowerCode[i])
                    return false;
            }
            return true;
        }

        private static char Lower(char c) => c >= 'A' && c <= 'Z' ? (char)(c + 32) : c;
    }
}
