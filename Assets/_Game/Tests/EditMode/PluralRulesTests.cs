using NUnit.Framework;
using PocketWilds.Core;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// Localization Plan (Docs/Design/Localization_Plan.md), « Pluriels et formats »: CLDR cardinal categories for whole
    /// counts in the planned languages. Prototype, wired into ILocalization.GetPlural in T39B.
    /// </summary>
    public class PluralRulesTests
    {
        private const PluralCategory Z = PluralCategory.Zero, O = PluralCategory.One, T = PluralCategory.Two,
            F = PluralCategory.Few, M = PluralCategory.Many, X = PluralCategory.Other;

        private static void Expect(string language, params (long n, PluralCategory category)[] cases)
        {
            foreach (var (n, category) in cases)
                Assert.AreEqual(category, PluralRules.Category(language, n), $"{language} {n}");
        }

        [Test]
        public void English_German_Dutch_Turkish_Greek_OneForOne()
        {
            foreach (string language in new[] { "en", "de", "nl", "tr", "el", "sv", "da", "nb", "fi", "hu" })
                Expect(language, (0, X), (1, O), (2, X), (11, X), (21, X), (101, X), (1000000, X));
        }

        [Test]
        public void French_OneForZeroAndOne_ManyForMillions()
        {
            Expect("fr", (0, O), (1, O), (2, X), (100, X), (1000000, M), (2000000, M), (1000001, X), (-1, O));
        }

        [Test]
        public void Portuguese_BrazilCountsZeroAsOne_PortugalDoesNot()
        {
            Expect("pt-BR", (0, O), (1, O), (2, X), (1000000, M));
            Expect("pt", (0, O), (1, O));
            Expect("pt-PT", (0, X), (1, O), (2, X), (1000000, M));
            Expect("PT_pt", (0, X));
        }

        [Test]
        public void Spanish_Italian_OneForOne_ManyForMillions()
        {
            foreach (string language in new[] { "es-419", "es-ES", "es", "it", "ca" })
                Expect(language, (0, X), (1, O), (2, X), (1000000, M), (3000000, M), (1500000, X));
        }

        [Test]
        public void Russian_Ukrainian_OneFewMany()
        {
            foreach (string language in new[] { "ru", "uk", "be" })
            {
                Expect(language, (0, M), (1, O), (2, F), (4, F), (5, M), (11, M), (12, M), (14, M), (20, M), (21, O),
                    (22, F), (25, M), (101, O), (111, M), (112, M), (122, F), (1000, M));
            }
        }

        [Test]
        public void Polish_OneOnlyForOne_TwentyOneIsMany()
        {
            Expect("pl", (0, M), (1, O), (2, F), (4, F), (5, M), (12, M), (21, M), (22, F), (102, F), (112, M), (1001, M));
        }

        [Test]
        public void Czech_Romanian_Croatian()
        {
            Expect("cs", (0, X), (1, O), (2, F), (4, F), (5, X), (22, X));
            Expect("ro", (0, F), (1, O), (2, F), (19, F), (20, X), (101, F), (119, F), (120, X));
            Expect("hr", (0, X), (1, O), (2, F), (5, X), (11, X), (21, O), (22, F));
        }

        [Test]
        public void EastAsian_Thai_Vietnamese_Indonesian_OneForm()
        {
            foreach (string language in new[] { "ja", "ko", "zh-Hans", "zh-Hant", "th", "vi", "id", "ms" })
                Expect(language, (0, X), (1, X), (2, X), (100, X));
        }

        [Test]
        public void Arabic_SixForms_Hindi_Hebrew_Filipino()
        {
            Expect("ar", (0, Z), (1, O), (2, T), (3, F), (10, F), (11, M), (99, M), (100, X), (101, X), (102, X), (103, F),
                (111, M), (1000, X));
            Expect("hi", (0, O), (1, O), (2, X));
            Expect("he", (1, O), (2, T), (3, X), (20, X));
            Expect("iw", (2, T));
            Expect("fil", (0, O), (1, O), (3, O), (4, X), (5, O), (6, X), (9, X), (10, O), (14, X));
        }

        [Test]
        public void UnknownLanguage_CountsLikeEnglish()
        {
            Assert.IsFalse(PluralRules.IsKnown("xx"));
            Assert.IsFalse(PluralRules.IsKnown(""));
            Assert.IsFalse(PluralRules.IsKnown(null));
            Assert.IsTrue(PluralRules.IsKnown("zh-Hant"));
            Assert.IsTrue(PluralRules.IsKnown("in-ID"));
            Expect("xx", (0, X), (1, O), (2, X));
            Assert.AreEqual(O, PluralRules.Category(null, 1));
        }

        [Test]
        public void Categories_CoverEveryCategoryTheRuleReturns()
        {
            string[] languages = { "en", "fr", "pt-BR", "pt-PT", "es-419", "it", "de", "ru", "uk", "pl", "cs", "ro", "hr", "tr", "el",
                "id", "vi", "ja", "ko", "zh-Hans", "th", "ar", "he", "hi", "fil" };
            foreach (string language in languages)
            {
                var set = PluralRules.Categories(language);
                Assert.IsTrue(set.Contains(X), language + ": other is always a form.");
                var seen = PluralCategorySet.Other;
                for (long n = 0; n <= 2500; n++)
                    seen |= (PluralCategorySet)(1 << (int)PluralRules.Category(language, n));
                seen |= (PluralCategorySet)(1 << (int)PluralRules.Category(language, 1000000));
                Assert.AreEqual(set, seen, language + ": the declared forms are exactly the reachable ones.");
            }
            Assert.AreEqual(PluralCategorySet.Other, PluralRules.Categories("ja"));
            Assert.AreEqual(PluralCategorySet.One | PluralCategorySet.Few | PluralCategorySet.Many | PluralCategorySet.Other, PluralRules.Categories("ru"));
        }

        [Test]
        public void Suffixes_NameTheKeyForms()
        {
            Assert.AreEqual(".zero", PluralRules.Suffix(Z));
            Assert.AreEqual(".one", PluralRules.Suffix(O));
            Assert.AreEqual(".two", PluralRules.Suffix(T));
            Assert.AreEqual(".few", PluralRules.Suffix(F));
            Assert.AreEqual(".many", PluralRules.Suffix(M));
            Assert.AreEqual(".other", PluralRules.Suffix(X));
            Assert.AreEqual(PluralForms.One, PluralRules.Suffix(O), "Same suffixes as today's PluralForms.");
            Assert.AreEqual(PluralForms.Other, PluralRules.Suffix(X));
        }

        [Test]
        public void TodaysTwoForms_AgreeWithCldr()
        {
            // PluralForms.IsOne (T36) keeps working for en and fr when T39B switches to PluralRules.
            foreach (string language in new[] { "en", "fr" })
            {
                for (long n = 0; n < 50; n++)
                    Assert.AreEqual(PluralForms.IsOne(language, n), PluralRules.Category(language, n) == O, $"{language} {n}");
            }
        }

        [Test]
        public void Category_DoesNotAllocate()
        {
            // Warm-up: the static tables and the JIT of every path measured below run once per run, not per call.
            TestDelegate calls = () =>
            {
                PluralRules.Category("pt-PT", 2);
                PluralRules.Category("ru", 22);
                PluralRules.Category("zh-Hant", 5);
                PluralRules.Suffix(PluralCategory.Few);
            };
            calls();
            calls();
            Assert.That(calls, Is.Not.AllocatingGCMemory());
        }
    }
}
