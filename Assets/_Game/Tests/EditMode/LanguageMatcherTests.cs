using NUnit.Framework;
using PocketWilds.Services.Localization;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// Localization Plan (Docs/Design/Localization_Plan.md), « Choisir la langue sans demander »: the device's ordered
    /// languages pick the table, English when none is served; the player's choice wins. Prototype, wired in T39B.
    /// </summary>
    public class LanguageMatcherTests
    {
        // Every table the plan ships, in its preference order.
        private static readonly string[] All =
        {
            "en", "fr", "de", "es-419", "es-ES", "it", "pt-BR", "nl", "pl", "tr", "id", "ru", "uk", "el", "vi", "ja", "ko",
            "zh-Hans", "zh-Hant", "th",
        };

        private static string M(params string[] device) => LanguageMatcher.Match(device, All);

        [Test]
        public void Normalize_ReadsAnyCaseAndSeparator_DropsExtensions_MapsLegacyCodes()
        {
            Assert.AreEqual("pt-BR", LanguageMatcher.Normalize("PT_br"));
            Assert.AreEqual("zh-Hant-TW", LanguageMatcher.Normalize("zh-hant-tw"));
            Assert.AreEqual("es-419", LanguageMatcher.Normalize("es-419"));
            Assert.AreEqual("en-US", LanguageMatcher.Normalize("en-US-u-mu-celsius"));
            Assert.AreEqual("de-CH", LanguageMatcher.Normalize("de-CH-1996"));
            Assert.AreEqual("id-ID", LanguageMatcher.Normalize("in-ID"));
            Assert.AreEqual("he-IL", LanguageMatcher.Normalize("iw-IL"));
            Assert.AreEqual("nb-NO", LanguageMatcher.Normalize("no-NO"));
            Assert.AreEqual("fil-PH", LanguageMatcher.Normalize("tl-PH"));
            Assert.AreEqual("zh-Hant-HK", LanguageMatcher.Normalize("yue-HK"));
            Assert.AreEqual("zh-Hant-HK", LanguageMatcher.Normalize("zh-yue-HK"));
            Assert.AreEqual("zh-Hans-CN", LanguageMatcher.Normalize("yue-Hans-CN"));
            Assert.AreEqual(string.Empty, LanguageMatcher.Normalize(""));
            Assert.AreEqual(string.Empty, LanguageMatcher.Normalize("und"));
            Assert.AreEqual(string.Empty, LanguageMatcher.Normalize("C"));
            Assert.AreEqual(string.Empty, LanguageMatcher.Normalize(null));
        }

        [Test]
        public void Chinese_RegionOrScriptPicksTheScript()
        {
            Assert.AreEqual("zh-Hant", M("zh-TW"));
            Assert.AreEqual("zh-Hant", M("zh-HK"));
            Assert.AreEqual("zh-Hant", M("zh-MO"));
            Assert.AreEqual("zh-Hant", M("zh-Hant-TW"));
            Assert.AreEqual("zh-Hant", M("yue-Hant-HK"));
            Assert.AreEqual("zh-Hans", M("zh-CN"));
            Assert.AreEqual("zh-Hans", M("zh-SG"));
            Assert.AreEqual("zh-Hans", M("zh"));
            Assert.AreEqual("zh-Hans", M("zh-Hans-HK"), "An explicit script wins over the region.");
            Assert.AreEqual("zh-TW", LanguageMatcher.Match(new[] { "zh-Hant-TW" }, new[] { "en", "zh-Hant", "zh-TW" }), "A regional table beats the script table.");
        }

        [Test]
        public void Chinese_OtherScriptOnlyWhenNoDeviceLanguageMatches()
        {
            var simplifiedOnly = new[] { "en", "zh-Hans" };
            Assert.AreEqual("zh-Hans", LanguageMatcher.Match(new[] { "zh-TW" }, simplifiedOnly), "Simplified reads better than English.");
            Assert.AreEqual("en", LanguageMatcher.Match(new[] { "zh-TW", "en-US" }, simplifiedOnly), "The player's second language comes first.");
            Assert.AreEqual("zh-Hant", LanguageMatcher.Match(new[] { "zh-CN" }, new[] { "fr", "zh-Hant" }));
        }

        [Test]
        public void Portuguese_GoesToBrazil_WhenOnlyBrazilExists()
        {
            Assert.AreEqual("pt-BR", M("pt-PT"));
            Assert.AreEqual("pt-BR", M("pt-AO"));
            Assert.AreEqual("pt-BR", M("pt"));
            Assert.AreEqual("pt-BR", M("pt-PT", "en-GB"), "Another Portuguese beats English.");
            Assert.AreEqual("pt-PT", LanguageMatcher.Match(new[] { "pt-PT" }, new[] { "en", "pt-BR", "pt-PT" }));
            Assert.AreEqual("pt-BR", LanguageMatcher.Match(new[] { "pt" }, new[] { "en", "pt-PT", "pt-BR" }), "Plain pt is Brazil (CLDR likely subtags).");
        }

        [Test]
        public void Spanish_LatinAmericaAndUnitedStatesGoTo419_SpainToSpain()
        {
            Assert.AreEqual("es-419", M("es-MX"));
            Assert.AreEqual("es-419", M("es-AR"));
            Assert.AreEqual("es-419", M("es-CO"));
            Assert.AreEqual("es-419", M("es-US"));
            Assert.AreEqual("es-419", M("es-419"));
            Assert.AreEqual("es-ES", M("es-ES"));
            Assert.AreEqual("es-ES", M("es"), "Plain es is Spain (CLDR likely subtags).");

            var neutralOnly = new[] { "en", "es-419" };
            Assert.AreEqual("es-419", LanguageMatcher.Match(new[] { "es-ES" }, neutralOnly));
            Assert.AreEqual("es-419", LanguageMatcher.Match(new[] { "es" }, neutralOnly));

            var plainAndLatin = new[] { "en", "es", "es-419" };
            Assert.AreEqual("es-419", LanguageMatcher.Match(new[] { "es-MX" }, plainAndLatin), "The regional group beats the plain table.");
            Assert.AreEqual("es", LanguageMatcher.Match(new[] { "es-ES" }, plainAndLatin));
            Assert.AreEqual("es", LanguageMatcher.Match(new[] { "es-MX" }, new[] { "en", "es" }));
        }

        [Test]
        public void RegionalVariants_FallBackToTheBaseLanguage()
        {
            Assert.AreEqual("de", M("de-CH"));
            Assert.AreEqual("de", M("de-AT"));
            Assert.AreEqual("fr", M("fr-CA"));
            Assert.AreEqual("fr", M("fr-BE"));
            Assert.AreEqual("en", M("en-GB"));
            Assert.AreEqual("en", M("en-FR"), "iOS: English with the French region.");
            Assert.AreEqual("id", M("in-ID"), "Android's legacy Indonesian code.");
            Assert.AreEqual("ja", M("ja-JP"));
            Assert.AreEqual("ko", M("ko-KR"));
            Assert.AreEqual("uk", M("uk-UA"));
            Assert.AreEqual("fr-CA", LanguageMatcher.Match(new[] { "fr-CA" }, new[] { "en", "fr", "fr-CA" }));
        }

        [Test]
        public void OrderedList_FirstServedLanguageWins_ElseEnglish()
        {
            Assert.AreEqual("de", M("gsw-CH", "en-US"), "Swiss German reads Standard German.");
            Assert.AreEqual("de", M("rm-CH", "de-CH", "fr-CH"), "Romansh is not served: the second language is.");
            Assert.AreEqual("es-ES", M("ca-ES", "es-ES"));
            Assert.AreEqual("fr", M("eu-ES", "fr-FR", "es-ES"));
            Assert.AreEqual("it", M("it-IT", "en-US"));
            Assert.AreEqual("en", M("sw-KE"));
            Assert.AreEqual("en", M("hi-IN", "mr-IN"));
            Assert.AreEqual("en", M());
            Assert.AreEqual("en", LanguageMatcher.Match(null, All));
            Assert.AreEqual("en", M("", "und", "x-private"));
        }

        [Test]
        public void Fallback_IsEnglish_ElseTheFirstTable()
        {
            Assert.AreEqual("fr", LanguageMatcher.Match(new[] { "sw" }, new[] { "fr", "de" }));
            Assert.AreEqual("en", LanguageMatcher.Match(new[] { "sw" }, new string[0]));
            Assert.AreSame(All[0], LanguageMatcher.Match(new[] { "EN-us" }, All), "The table's own string comes back.");
        }

        [Test]
        public void Resolve_SavedChoiceWins_EmptyFollowsTheDevice()
        {
            Assert.AreEqual("fr", LanguageMatcher.Resolve("fr", new[] { "de-DE" }, All));
            Assert.AreEqual("fr", LanguageMatcher.Resolve("FR", new[] { "de-DE" }, All));
            Assert.AreEqual("de", LanguageMatcher.Resolve("", new[] { "de-DE" }, All));
            Assert.AreEqual("de", LanguageMatcher.Resolve(null, new[] { "de-DE" }, All));
            Assert.AreEqual("de", LanguageMatcher.Resolve("xx", new[] { "de-DE" }, All), "An unknown saved code is ignored.");
            Assert.AreEqual("pt-BR", LanguageMatcher.Resolve("pt", new[] { "de-DE" }, All), "An old two-letter save.");
            Assert.AreEqual("zh-Hans", LanguageMatcher.Resolve("zh", new[] { "de-DE" }, All), "The current service's « zh ».");
            Assert.AreEqual("en", LanguageMatcher.Resolve("", new[] { "sw" }, All));
        }

        [Test]
        public void Score_RanksExactThenGroupThenPlainThenOtherRegion()
        {
            LanguageTag.TryParse("es-MX", out var device);
            LanguageTag.TryParse("es-MX", out var exact);
            LanguageTag.TryParse("es-419", out var group);
            LanguageTag.TryParse("es", out var plain);
            LanguageTag.TryParse("es-ES", out var spain);
            LanguageTag.TryParse("fr", out var french);
            int e = LanguageMatcher.Score(device, exact), g = LanguageMatcher.Score(device, group);
            int p = LanguageMatcher.Score(device, plain), o = LanguageMatcher.Score(device, spain);
            Assert.Greater(e, g);
            Assert.Greater(g, p);
            Assert.Greater(p, o);
            Assert.Greater(o, 0);
            Assert.AreEqual(0, LanguageMatcher.Score(device, french));
        }

        [Test]
        public void DeviceLanguages_ParseSplitsTrimsAndKeepsOrder()
        {
            CollectionAssert.AreEqual(new[] { "fr-FR", "en-US", "de" }, DeviceLanguages.Parse(" fr-FR, en-US,,FR-fr, de "));
            CollectionAssert.IsEmpty(DeviceLanguages.Parse(""));
            CollectionAssert.IsEmpty(DeviceLanguages.Parse(null));
        }

        [Test]
        public void DeviceLanguages_SystemLanguageFallbackTags()
        {
            Assert.AreEqual("zh-Hant", DeviceLanguages.FromSystemLanguage(SystemLanguage.ChineseTraditional));
            Assert.AreEqual("zh-Hans", DeviceLanguages.FromSystemLanguage(SystemLanguage.ChineseSimplified));
            Assert.AreEqual("pt", DeviceLanguages.FromSystemLanguage(SystemLanguage.Portuguese));
            Assert.AreEqual("nb", DeviceLanguages.FromSystemLanguage(SystemLanguage.Norwegian));
            Assert.AreEqual(string.Empty, DeviceLanguages.FromSystemLanguage(SystemLanguage.Unknown));
            Assert.AreEqual("zh-Hant", M(DeviceLanguages.FromSystemLanguage(SystemLanguage.ChineseTraditional)));
        }

        [Test]
        public void DeviceLanguages_OverrideFeedsTheMatcher()
        {
            try
            {
                DeviceLanguages.SetOverride(new[] { "pt-PT", "en-GB" });
                Assert.AreEqual("override", DeviceLanguages.Source);
                Assert.AreEqual("pt-BR", LanguageMatcher.Match(DeviceLanguages.Preferred, All));
            }
            finally
            {
                DeviceLanguages.SetOverride(null);
            }
        }

        [Test]
        public void DeviceLanguages_ReadsDoNotAllocateAfterTheFirst()
        {
            var first = DeviceLanguages.Preferred;
            Assert.IsNotNull(first);
            Assert.AreSame(first, DeviceLanguages.Preferred, "Cached after the first read.");
            Assert.That(() => { _ = DeviceLanguages.Preferred.Count; }, Is.Not.AllocatingGCMemory(), "No allocation after the first call.");
        }
    }
}
