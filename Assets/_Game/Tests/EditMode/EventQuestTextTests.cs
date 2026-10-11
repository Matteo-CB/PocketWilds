using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T48: every event quest template has its line in both languages, its numbers match its target, its placeholders are filled.</summary>
    public class EventQuestTextTests
    {
        // The lines whose number is not the count: the daily gift (none), 3 or 2 stars in one round (EQ-D-21, 26), {n} biomes (EQ-D-23).
        private static readonly HashSet<string> NotCounted = new HashSet<string> { "EQ-D-14", "EQ-D-21", "EQ-D-23", "EQ-D-26" };

        [Test]
        public void EveryTemplate_HasBothLanguages_ItsTarget_KnownPlaceholders()
        {
            var rows = new Dictionary<string, (string En, string Fr)>();
            var table = EventQuestTexts.Texts;
            for (int i = 0; i < table.GetLength(0); i++)
            {
                Assert.IsFalse(rows.ContainsKey(table[i, 0]), "Twice: " + table[i, 0]);
                rows[table[i, 0]] = (table[i, 1], table[i, 2]);
            }
            var placeholders = new HashSet<string>(EventQuestText.Placeholders);
            var templates = new List<EventQuestTemplate>(EventQuestCatalog.Daily);
            templates.AddRange(EventQuestCatalog.Long);
            foreach (var t in templates)
            {
                Assert.IsTrue(rows.TryGetValue(t.TextKey, out var line), "Missing text " + t.TextKey);
                foreach (var text in new[] { line.En, line.Fr })
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(text), t.Code);
                    foreach (Match m in Regex.Matches(text, @"\{(\w+)\}"))
                        Assert.IsTrue(placeholders.Contains(m.Groups[1].Value), t.Code + " names {" + m.Groups[1].Value + "}");
                    if (NotCounted.Contains(t.Code))
                        continue;
                    var number = Regex.Match(text.Replace(" ", "").Replace(",", ""), @"\d+");
                    if (text == line.En && t.Code == "EQ-D-03")
                        continue; // « Play {activity} twice »
                    Assert.IsTrue(number.Success, t.Code + ": " + text);
                    Assert.AreEqual(t.Target, int.Parse(number.Value), t.Code + ": " + text);
                }
            }
            Assert.IsTrue(rows.ContainsKey(EventQuestCatalog.Daily[22].TextKey + ".one"), "EQ-D-23 for a single biome.");
            Assert.AreEqual(templates.Count + 1, rows.Count);
        }

        [Test]
        public void Formatter_FillsActivityCurrencyAndCount()
        {
            var format = new EventQuestText(null);
            Assert.AreEqual("Joue 2 parties de Cache-cache", format.Format("Joue 2 parties de {activity}", "Cache-cache", "", 2, true));
            Assert.AreEqual("Joue 1 partie d'Énigmes", format.Format("Joue 1 partie de {activity}", "Énigmes", "", 1, true));
            Assert.AreEqual("Earn 200 Sprouts", format.Format("Earn 200 {currency}", "", "Sprouts", 200, false));
            Assert.AreEqual("Rends visite à 2 biomes", format.Format("Rends visite à {n} biomes", "", "", 2, true));
            Assert.AreEqual("Play Hide and Seek twice", format.Format("Play {activity} twice", "Hide and Seek", "", 2, false));
        }
    }
}
