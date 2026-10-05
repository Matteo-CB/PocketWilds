using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Editor.UI;
using PocketWilds.Services.Localization;
using PocketWilds.UI;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T18: shipped text follows the Writing Guide and resolves per language with an English fallback.</summary>
    public class LocalizationTests
    {
        private static LocalizationTableAsset[] ShippedTables()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(UiPipeline.ContentDatabasePath);
            return database.localization.ToArray();
        }

        [Test]
        public void ShippedTables_CoverEveryUiKey_InEnglishAndFrench()
        {
            var tables = ShippedTables();
            Assert.AreEqual("en", tables[0].languageCode, "English first: it is the fallback.");
            foreach (string language in new[] { "en", "fr" })
            {
                var table = tables.Single(t => t.languageCode == language);
                foreach (string key in UiTextKeys.All)
                {
                    Assert.IsTrue(table.TryGet(key, out string value), language + " misses " + key);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(value), language + " " + key);
                }
            }
        }

        [Test]
        public void ShippedText_FollowsTheWritingGuide()
        {
            foreach (var table in ShippedTables())
            {
                foreach (var entry in table.entries)
                {
                    string where = table.languageCode + " " + entry.key;
                    Assert.IsFalse(entry.value.Contains('—'), where + ": no em dash.");
                    Assert.IsFalse(entry.value.Contains(';'), where + ": no semicolon.");
                    Assert.AreNotEqual(entry.key, entry.value, where + ": the key is not the text.");
                    Assert.IsFalse(entry.key.Contains(' '), where + ": keys are ids, not sentences.");
                    if (entry.key.EndsWith(".button"))
                        Assert.LessOrEqual(entry.value.Split(' ').Length, 3, where + ": buttons use 1 to 3 words.");
                }
            }
        }

        [Test]
        public void Language_FollowsTheProfile_ThenTheDevice_ThenEnglish()
        {
            var tables = ShippedTables();
            Assert.AreEqual("fr", new LocalizationService(tables, "fr", SystemLanguage.English).Language);
            Assert.AreEqual("fr", new LocalizationService(tables, "", SystemLanguage.French).Language);
            Assert.AreEqual("en", new LocalizationService(tables, "", SystemLanguage.German).Language, "No German table yet.");
            Assert.AreEqual("en", new LocalizationService(tables, "xx", SystemLanguage.Japanese).Language);

            var french = new LocalizationService(tables, "fr", SystemLanguage.French);
            Assert.AreEqual("Bon retour", french.Get(UiTextKeys.OfflineWelcomeBack));
            Assert.AreEqual("Récolter", french.Get(UiTextKeys.CollectButton));
            var english = new LocalizationService(tables, "en", SystemLanguage.English);
            Assert.AreEqual("Welcome back", english.Get(UiTextKeys.OfflineWelcomeBack));
        }

        [Test]
        public void MissingFrenchText_FallsBackToEnglish()
        {
            var en = ScriptableObject.CreateInstance<LocalizationTableAsset>();
            var fr = ScriptableObject.CreateInstance<LocalizationTableAsset>();
            try
            {
                en.languageCode = "en";
                en.Set("test.only_english", "Only English");
                fr.languageCode = "fr";
                var service = new LocalizationService(new[] { en, fr }, "fr", SystemLanguage.French);
                Assert.AreEqual("fr", service.Language);
                Assert.AreEqual("Only English", service.Get("test.only_english"));
                Assert.IsTrue(service.Has("test.only_english"));
                Assert.IsFalse(service.Has("test.nowhere"));
            }
            finally
            {
                Object.DestroyImmediate(en);
                Object.DestroyImmediate(fr);
            }
        }
    }
}
