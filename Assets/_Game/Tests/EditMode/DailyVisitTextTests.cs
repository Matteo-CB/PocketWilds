using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.Services.Localization;
using PocketWilds.Simulation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T36 texts (doc 01 Contenu sections 0 to 6; In-game Writing &amp; Localization Guide, doc 06 « COPY RULE »): every key
    /// the screens use and every shipped wish and journal sentence exists in English and French, in singular and plural
    /// when it counts; no em dash, no sparkle, no banned word, no pressure word; the formatter fills every placeholder,
    /// writes the doc's French (contractions and elision included), follows a language change and allocates nothing when
    /// the same text is asked again.
    /// </summary>
    public class DailyVisitTextTests
    {
        private const string Nbsp = " ";
        private const string NarrowNbsp = " ";

        private static readonly Regex PlaceholderPattern = new Regex(@"\{([a-z_]+)\}", RegexOptions.Compiled);

        /// <summary>Placeholders of later systems' wishes (Garden, Bakery, visitors, expeditions), filled by their tasks.</summary>
        private static readonly string[] LaterPlaceholders = { "crop", "cake", "visitor", "destination" };

        /// <summary>Writing Guide: no pressure, no guilt, no countdown words; no corporate or technical words.</summary>
        private static readonly string[] Forbidden =
        {
            "hurry", "last chance", "don't miss", "miss out", "expire", "expires", "expiring", "limited", "only today", "now or never",
            "lose", "lost", "before it's gone", "error", "success", "successfully", "failed", "invalid", "user", "data",
            "dépêche", "vite", "dernière chance", "ne rate", "expire", "perdu", "perds", "limité", "seulement aujourd'hui",
            "avant qu'il soit trop tard", "échec", "utilisateur", "données",
        };

        private readonly List<Object> _tables = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var table in _tables)
                Object.DestroyImmediate(table);
            _tables.Clear();
        }

        // ------------------------------------------------------------------------------------------- tables

        private static Dictionary<string, string[]> Own()
        {
            var rows = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var texts = DailyVisitTexts.Texts;
            for (int i = 0; i < texts.GetLength(0); i++)
                rows[texts[i, 0]] = new[] { texts[i, 1], texts[i, 2] };
            return rows;
        }

        /// <summary>What LOC_en / LOC_fr hold: UiPipeline.Texts, then the T36 table (UiPipeline.EnsureLocalization order).</summary>
        private static Dictionary<string, string[]> Merged()
        {
            var rows = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var table in new[] { UiPipeline.Texts, DailyVisitTexts.Texts })
                for (int i = 0; i < table.GetLength(0); i++)
                    rows[table[i, 0]] = new[] { table[i, 1], table[i, 2] };
            return rows;
        }

        private static IEnumerable<string> ContractKeys() =>
            typeof(DailyVisitText).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue());

        private static IEnumerable<string> ShippedSentenceKeys()
        {
            yield return WishCatalog.TextKey(WishTemplateId.Tutorial);
            foreach (var template in WishCatalog.All.Where(t => t.ShippedInThisBuild))
                yield return WishCatalog.TextKey(template.Id);
            foreach (var key in DailyVisitTextFormatter.UpgradeWishKeys)
                yield return key;
            foreach (var goal in WeeklyJournalCatalog.All.Where(g => g.ShippedInThisBuild))
                yield return WeeklyJournalCatalog.TextKey(goal.Id);
        }

        private static IEnumerable<string> Placeholders(string text) =>
            PlaceholderPattern.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value);

        private static string Normalized(string placeholder) =>
            placeholder.EndsWith("_the", StringComparison.Ordinal) ? placeholder.Substring(0, placeholder.Length - 4) : placeholder;

        [Test]
        public void Table_HasNoDuplicate_AndOverridesNoExistingText()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var existing = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < UiPipeline.Texts.GetLength(0); i++)
                existing.Add(UiPipeline.Texts[i, 0]);
            var texts = DailyVisitTexts.Texts;
            var problems = new List<string>();
            for (int i = 0; i < texts.GetLength(0); i++)
            {
                if (!seen.Add(texts[i, 0]))
                    problems.Add("twice: " + texts[i, 0]);
                if (existing.Contains(texts[i, 0]))
                    problems.Add("already in UiPipeline.Texts: " + texts[i, 0]);
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void EveryContractKey_HasEnglishAndFrench()
        {
            var merged = Merged();
            var missing = ContractKeys().Where(k => !merged.TryGetValue(k, out var t) || string.IsNullOrWhiteSpace(t[0]) || string.IsNullOrWhiteSpace(t[1]))
                .ToList();
            Assert.IsEmpty(missing, "Keys of DailyVisitText without English or French:\n" + string.Join("\n", missing));
        }

        [Test]
        public void EveryWishAndGoal_HasItsSentence_InBothLanguages_WithBothFormsWhenItCounts()
        {
            var own = Own();
            var keys = new List<string> { WishCatalog.TextKey(WishTemplateId.Tutorial) };
            keys.AddRange(WishCatalog.All.Select(t => WishCatalog.TextKey(t.Id)));
            keys.AddRange(WeeklyJournalCatalog.All.Select(g => WeeklyJournalCatalog.TextKey(g.Id)));
            keys.AddRange(DailyVisitTextFormatter.UpgradeWishKeys);
            Assert.AreEqual(1 + 83 + 52 + 3, keys.Distinct().Count(), "WISH-T01, WISH-001 to 083, JRN-001 to 052, the three WISH-026 upgrades.");
            var problems = new List<string>();
            foreach (var key in keys)
            {
                if (!own.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text[0]) || string.IsNullOrWhiteSpace(text[1]))
                {
                    problems.Add("missing: " + key);
                    continue;
                }
                // Doc 01 section 0: a sentence with {n} exists in singular and plural.
                if (text[0].Contains("{n}") || text[1].Contains("{n}"))
                {
                    foreach (string form in new[] { ".one", ".other" })
                        if (!own.TryGetValue(key + form, out var f) || string.IsNullOrWhiteSpace(f[0]) || string.IsNullOrWhiteSpace(f[1]))
                            problems.Add("no " + form + ": " + key);
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void Texts_FollowTheWritingGuide_AndTheOwnerRules()
        {
            var violations = typeof(VocabularyRuleTests).GetMethod("Violations", BindingFlags.NonPublic | BindingFlags.Static);
            var sparkles = typeof(NoSparkleRuleTests).GetField("Characters", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as string[]
                           ?? new[] { char.ConvertFromUtf32(0x2728) };
            var forbidden = Forbidden.Select(w => new Regex(@"(?<![\p{L}'])" + Regex.Escape(w) + @"(?![\p{L}])", RegexOptions.IgnoreCase)).ToArray();
            var problems = new List<string>();
            foreach (var row in Own())
            {
                for (int language = 0; language < 2; language++)
                {
                    string text = row.Value[language];
                    string where = row.Key + (language == 0 ? " (en): " : " (fr): ");
                    if (text.Contains('—') || text.Contains('–'))
                        problems.Add(where + "dash");
                    if (sparkles.Any(s => text.Contains(s)))
                        problems.Add(where + "sparkle");
                    if (text != text.Trim() || text.Contains("  "))
                        problems.Add(where + "spaces");
                    if (violations != null && violations.Invoke(null, new object[] { text, language == 0 }) is List<string> words && words.Count > 0)
                        problems.Add(where + "vocabulary rule: " + string.Join(", ", words));
                    foreach (var word in forbidden)
                        if (word.IsMatch(text))
                            problems.Add(where + "writing guide: " + word);
                    // French typography: a no-break space before « : » and « ! », never a plain one.
                    if (language == 1 && (text.Contains(" :") || text.Contains(" !") || text.Contains(" ?")))
                        problems.Add(where + "plain space before French punctuation");
                }
            }
            Assert.IsNotNull(violations, "VocabularyRuleTests.Violations is the shared word list.");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void Placeholders_AreTheOnesTheFormatterFills()
        {
            var own = Own();
            var filled = new HashSet<string>(DailyVisitTextFormatter.Placeholders, StringComparer.Ordinal);
            var known = new HashSet<string>(filled.Concat(LaterPlaceholders), StringComparer.Ordinal);
            var shipped = new HashSet<string>(ShippedSentenceKeys().Concat(ContractKeys()), StringComparer.Ordinal);
            var problems = new List<string>();
            foreach (var row in own)
            {
                string baseKey = row.Key.EndsWith(".one", StringComparison.Ordinal) ? row.Key.Substring(0, row.Key.Length - 4)
                    : row.Key.EndsWith(".other", StringComparison.Ordinal) ? row.Key.Substring(0, row.Key.Length - 6) : row.Key;
                var allowed = shipped.Contains(baseKey) ? filled : known;
                foreach (string name in Placeholders(row.Value[0]).Concat(Placeholders(row.Value[1])))
                    if (!allowed.Contains(name))
                        problems.Add(row.Key + ": {" + name + "} is not filled");
                // Both languages name the same things (with or without the article).
                var en = new HashSet<string>(Placeholders(row.Value[0]).Select(Normalized));
                var fr = new HashSet<string>(Placeholders(row.Value[1]).Select(Normalized));
                if (!en.SetEquals(fr))
                    problems.Add(row.Key + ": English and French name different things");
                // The singular never names more than the plural.
                if (row.Key.EndsWith(".one", StringComparison.Ordinal) && own.TryGetValue(baseKey + ".other", out var other))
                {
                    var plural = new HashSet<string>(Placeholders(other[0]).Concat(Placeholders(other[1])).Select(Normalized));
                    if (!en.IsSubsetOf(plural))
                        problems.Add(row.Key + ": the singular names more than the plural");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // ------------------------------------------------------------------------------------------- plurals

        [Test]
        public void Plurals_PickTheRightForm_InEnglishAndFrench()
        {
            Assert.IsFalse(PluralForms.IsOne("en", 0));
            Assert.IsTrue(PluralForms.IsOne("en", 1));
            Assert.IsFalse(PluralForms.IsOne("en", 2));
            Assert.IsTrue(PluralForms.IsOne("fr", 0), "French: 0 is singular (« 0 souhait »).");
            Assert.IsTrue(PluralForms.IsOne("fr", 1));
            Assert.IsFalse(PluralForms.IsOne("fr", 2));

            ILocalization en = Service("en");
            ILocalization fr = Service("fr");
            Assert.AreEqual("{n} tickets", en.GetPlural(DailyVisitText.RewardTickets, 0));
            Assert.AreEqual("{n} ticket", en.GetPlural(DailyVisitText.RewardTickets, 1));
            Assert.AreEqual("{n} tickets", en.GetPlural(DailyVisitText.RewardTickets, 2));
            Assert.AreEqual("{n} billet", fr.GetPlural(DailyVisitText.RewardTickets, 0));
            Assert.AreEqual("{n} billet", fr.GetPlural(DailyVisitText.RewardTickets, 1));
            Assert.AreEqual("{n} billets", fr.GetPlural(DailyVisitText.RewardTickets, 2));
            Assert.AreEqual("Souhaits", fr.GetPlural(DailyVisitText.WishesTitle, 2), "No forms: the key's own text.");

            // The interface's default (other implementations, test fakes) picks the same forms.
            var fake = new FakeLocalization("fr");
            Assert.AreEqual("{n} billet", ((ILocalization)fake).GetPlural(DailyVisitText.RewardTickets, 0));
            Assert.AreEqual("{n} billets", ((ILocalization)fake).GetPlural(DailyVisitText.RewardTickets, 2));
            fake.Language = "en";
            Assert.AreEqual("{n} tickets", ((ILocalization)fake).GetPlural(DailyVisitText.RewardTickets, 0));
            Assert.AreEqual("{n} ticket", ((ILocalization)fake).GetPlural(DailyVisitText.RewardTickets, 1));
            Assert.AreEqual("unknown.key", ((ILocalization)fake).GetPlural("unknown.key", 1), "Nothing at all: the key, like Get.");
        }

        // ------------------------------------------------------------------------------------------- formatter

        [Test]
        public void Formatter_WritesTheDocFrench()
        {
            var rig = Rig.Create(Service("fr"));
            var f = rig.Formatter;
            Assert.AreEqual("Fais éclore 2 œufs", f.Wish(Tutorial()), "WISH-T01 (A08).");
            Assert.AreEqual("Fais éclore 3 œufs", f.Wish(W(WishTemplateId.HatchEggs, 3)));
            Assert.AreEqual("Fais éclore 2 œufs de Prairie", f.Wish(W(WishTemplateId.HatchBiomeEggs, 2, Rig.Grassland)));
            Assert.AreEqual("Fais éclore un œuf précieux", f.Wish(W(WishTemplateId.HatchPreciousEgg, 1)));
            Assert.AreEqual("Fais monter le Lapin d'un niveau de maîtrise", f.Wish(W(WishTemplateId.RaiseSpeciesMastery, 1, Rig.RabbitId)));
            Assert.AreEqual("Regarde l'Écureuil dans le Codex", f.Wish(W(WishTemplateId.LookAtSpeciesInCodex, 1, Rig.SquirrelId)));
            Assert.AreEqual("Touche 5 animaux", f.Wish(W(WishTemplateId.TapAnimals, 5)));
            Assert.AreEqual("Fais 3 câlins à Mochi", f.Wish(W(WishTemplateId.CuddleAnimal, 3, animal: rig.Mochi)));
            Assert.AreEqual("Donne une friandise au Lapin", f.Wish(W(WishTemplateId.GiveAnimalTreat, 1, animal: rig.Rabbit)), "à le: au.");
            Assert.AreEqual("Donne une friandise à l'Écureuil", f.Wish(W(WishTemplateId.GiveAnimalTreat, 1, animal: rig.Squirrel)));
            Assert.AreEqual("Gagne 20 points de lien", f.Wish(W(WishTemplateId.EarnBond, 20)));
            Assert.AreEqual("Achète une amélioration", f.Wish(W(WishTemplateId.BuyUpgrades, 1)), "Singular.");
            Assert.AreEqual("Achète 2 améliorations", f.Wish(W(WishTemplateId.BuyUpgrades, 2)));
            Assert.AreEqual("Améliore les Nids douillets de la Prairie", f.Wish(W(WishTemplateId.UpgradeInBiome, 1, Rig.Grassland, extra: 1)));
            Assert.AreEqual("Améliore la Lumière de la Forêt", f.Wish(W(WishTemplateId.UpgradeInBiome, 1, Rig.Forest, extra: 0)));
            Assert.AreEqual("Agrandis un biome", f.Wish(W(WishTemplateId.ExpandBiome, 1)));
            // Later systems keep the same grammar.
            Assert.AreEqual("Ouvre l'album de la Forêt", f.Wish(W(WishTemplateId.OpenBiomeAlbum, 1, Rig.Forest)));
            Assert.AreEqual("Prends une photo du Lapin", f.Wish(W(WishTemplateId.PhotographAnimal, 1, animal: rig.Rabbit)), "de le: du.");
            Assert.AreEqual("Prends une photo de Mochi", f.Wish(W(WishTemplateId.PhotographAnimal, 1, animal: rig.Mochi)));
            Assert.AreEqual("Dessine la constellation du Hérisson", f.Wish(W(WishTemplateId.DrawSpeciesConstellation, 1, Rig.HedgehogId)));

            Assert.AreEqual("Fais éclore 30 œufs", f.Goal(G(JournalGoalId.HatchEggs, 30)));
            Assert.AreEqual("Réalise 10 souhaits", f.Goal(G(JournalGoalId.Wishes, 10)));
            Assert.AreEqual("Avance de 4 cases au calendrier", f.Goal(G(JournalGoalId.CalendarDays, 4)));
            Assert.AreEqual("Gagne 80 points de lien", f.Goal(G(JournalGoalId.BondPoints, 80)));
            Assert.AreEqual("Agrandis un biome", f.Goal(G(JournalGoalId.Expansions, 1)));
            Assert.AreEqual("Agrandis 2 fois tes biomes", f.Goal(G(JournalGoalId.Expansions, 2)));
            Assert.AreEqual("Découvre une nouvelle espèce", f.Goal(G(JournalGoalId.NewSpecies, 1)));
            Assert.AreEqual("Glisse 5" + NarrowNbsp + "000 mètres", f.Goal(G(JournalGoalId.GlideMeters, 5000)));

            Assert.AreEqual("3 souhaits t'attendent", f.ReturnLine(DailyVisitText.ReturnLineWishes, 3));
            Assert.AreEqual("Un souhait t'attend", f.ReturnLine(DailyVisitText.ReturnLineWishes, 1));
            Assert.AreEqual("Le cadeau du jour t'attend", f.ReturnLine(DailyVisitText.ReturnLineGift, 1));
            Assert.AreEqual("Mochi", f.AnimalName(rig.Mochi));
            Assert.AreEqual("Lapin", f.AnimalName(rig.Rabbit), "No name of its own: its species.");
        }

        [Test]
        public void Formatter_WritesNaturalEnglish()
        {
            var rig = Rig.Create(Service("en"));
            var f = rig.Formatter;
            Assert.AreEqual("Hatch 2 eggs", f.Wish(Tutorial()));
            Assert.AreEqual("Hatch 2 Meadow eggs", f.Wish(W(WishTemplateId.HatchBiomeEggs, 2, Rig.Grassland)));
            Assert.AreEqual("Raise Rabbit mastery by one level", f.Wish(W(WishTemplateId.RaiseSpeciesMastery, 1, Rig.RabbitId)));
            Assert.AreEqual("Look at the Squirrel in the Codex", f.Wish(W(WishTemplateId.LookAtSpeciesInCodex, 1, Rig.SquirrelId)));
            Assert.AreEqual("Give Mochi 3 cuddles", f.Wish(W(WishTemplateId.CuddleAnimal, 3, animal: rig.Mochi)));
            Assert.AreEqual("Give the Rabbit a treat", f.Wish(W(WishTemplateId.GiveAnimalTreat, 1, animal: rig.Rabbit)));
            Assert.AreEqual("Buy an upgrade", f.Wish(W(WishTemplateId.BuyUpgrades, 1)));
            Assert.AreEqual("Upgrade Cozy Nests in the Meadow", f.Wish(W(WishTemplateId.UpgradeInBiome, 1, Rig.Grassland, extra: 1)));
            Assert.AreEqual("Upgrade Forest Glow", f.Wish(W(WishTemplateId.UpgradeInBiome, 1, Rig.Forest, extra: 0)));
            Assert.AreEqual("Make 10 wishes come true", f.Goal(G(JournalGoalId.Wishes, 10)));
            Assert.AreEqual("Stamp 4 days on the calendar", f.Goal(G(JournalGoalId.CalendarDays, 4)));
            Assert.AreEqual("Glide 5,000 meters", f.Goal(G(JournalGoalId.GlideMeters, 5000)));
            Assert.AreEqual("3 wishes are waiting", f.ReturnLine(DailyVisitText.ReturnLineWishes, 3));
            Assert.AreEqual("A wish is waiting", f.ReturnLine(DailyVisitText.ReturnLineWishes, 1));
        }

        [Test]
        public void Formatter_WritesRewardsDaysAndProgress()
        {
            var fr = Rig.Create(Service("fr")).Formatter;
            var gems = new RewardCell(RewardKind.Gems, 15);
            var light = new RewardCell(RewardKind.Light, 0) { LightMinutes = 30.0 };
            var tickets = new RewardCell(RewardKind.Ticket, 2);
            var egg = new RewardCell(RewardKind.DiscoveryEgg, 1);
            var armchair = new RewardCell(RewardKind.Decoration, 1, DailyRewardTables.CycleDecoration(1));
            Assert.AreEqual("+15", fr.RewardLabel(gems));
            Assert.AreEqual("30" + Nbsp + "min", fr.RewardLabel(light));
            Assert.AreEqual("×2", fr.RewardLabel(tickets));
            Assert.AreEqual("+12.4K", fr.RewardLabel(new RewardCell(RewardKind.Light, 0) { LightMinutes = 30.0, Light = BigValue.FromDouble(12_400) }),
                "The Light really given, as the HUD writes it.");
            Assert.AreEqual(string.Empty, fr.RewardLabel(armchair), "A single decoration: its icon says it.");
            Assert.AreEqual("15 Gemmes", fr.RewardName(gems));
            Assert.AreEqual("1 Gemme", fr.RewardName(new RewardCell(RewardKind.Gems, 1)));
            Assert.AreEqual("30" + Nbsp + "min de Lumière", fr.RewardName(light));
            Assert.AreEqual("1 œuf Découverte", fr.RewardName(egg));
            Assert.AreEqual("Demain" + Nbsp + ": 20 Gemmes", fr.Tomorrow(new RewardCell(RewardKind.Gems, 20)), "Doc 01 section 4.4.");
            Assert.AreEqual("Demain" + Nbsp + ": Fauteuil de mousse", fr.Tomorrow(armchair));
            Assert.AreEqual("Demain" + Nbsp + ": Fauteuil de mousse Crépuscule",
                fr.Tomorrow(new RewardCell(RewardKind.Decoration, 1, DailyRewardTables.CycleDecoration(14))));
            Assert.AreEqual("Encore 3 jours", fr.DaysLeft(3), "Real days, never a countdown.");
            Assert.AreEqual("Dernier jour", fr.DaysLeft(1));
            Assert.AreEqual("Encore 3 jours", fr.BigBoxIn(3));
            Assert.AreEqual("Grande case demain", fr.BigBoxIn(1));
            Assert.AreEqual("Grande case aujourd'hui", fr.BigBoxIn(0));
            Assert.AreEqual("Jour 1 sur 3", fr.WelcomeDay(0));
            Assert.AreEqual("Jour 28", fr.CalendarDay(27));
            Assert.AreEqual("1/2", fr.Progress(1, 2));
            Assert.AreEqual("Grand cadeau", fr.GiftSizeName(GiftSize.Big));
            Assert.AreEqual("Trèfle croquant", fr.ItemName(ContentId.FromSource("treat.clover_crunch")));

            var en = Rig.Create(Service("en")).Formatter;
            Assert.AreEqual("Tomorrow: 20 Gems", en.Tomorrow(new RewardCell(RewardKind.Gems, 20)));
            Assert.AreEqual("1 Gem", en.RewardName(new RewardCell(RewardKind.Gems, 1)));
            Assert.AreEqual("3 days left", en.DaysLeft(3));
            Assert.AreEqual("Last day", en.DaysLeft(1));
            Assert.AreEqual("2 tickets", en.RewardName(tickets));
        }

        [Test]
        public void Formatter_SameTextTwice_AllocatesNothing()
        {
            var rig = Rig.Create(Service("fr"));
            var f = rig.Formatter;
            var count = W(WishTemplateId.HatchEggs, 3);
            var named = W(WishTemplateId.CuddleAnimal, 3, animal: rig.Mochi);
            var species = W(WishTemplateId.GiveAnimalTreat, 1, animal: rig.Rabbit);
            var biome = W(WishTemplateId.UpgradeInBiome, 1, Rig.Grassland, extra: 1);
            var goal = G(JournalGoalId.HatchEggs, 30);
            var cell = new RewardCell(RewardKind.Gems, 20);
            string first = f.Wish(count);
            f.Wish(named);
            f.Wish(species);
            f.Wish(biome);
            f.Goal(goal);
            f.ReturnLine(DailyVisitText.ReturnLineWishes, 3);
            f.RewardLabel(cell);
            f.Tomorrow(cell);
            f.DaysLeft(3);
            f.AnimalName(rig.Mochi);
            f.Progress(1, 3);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                f.Wish(count);
                f.Wish(named);
                f.Wish(species);
                f.Wish(biome);
                f.Goal(goal);
                f.ReturnLine(DailyVisitText.ReturnLineWishes, 3);
                f.RewardLabel(cell);
                f.Tomorrow(cell);
                f.DaysLeft(3);
                f.AnimalName(rig.Mochi);
                f.Progress(1, 3);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "A text asked again is the cached one.");
            Assert.AreSame(first, f.Wish(count), "Same wish, same string.");

            // A rename is a real change: the sentence follows.
            rig.State.Collection.CustomNames[rig.Mochi] = "Pompon";
            Assert.AreEqual("Fais 3 câlins à Pompon", f.Wish(named));
        }

        [Test]
        public void Formatter_FollowsALanguageChange()
        {
            var fake = new FakeLocalization("en");
            var rig = Rig.Create(fake);
            var wish = W(WishTemplateId.HatchEggs, 3);
            Assert.AreEqual("Hatch 3 eggs", rig.Formatter.Wish(wish));
            Assert.AreEqual("A wish is waiting", rig.Formatter.ReturnLine(DailyVisitText.ReturnLineWishes, 1));
            fake.Language = "fr";
            Assert.AreEqual("Fais éclore 3 œufs", rig.Formatter.Wish(wish), "Settings changed the language: the cache drops.");
            Assert.AreEqual("Un souhait t'attend", rig.Formatter.ReturnLine(DailyVisitText.ReturnLineWishes, 1));
            Assert.AreEqual("Donne une friandise au Lapin", rig.Formatter.Wish(W(WishTemplateId.GiveAnimalTreat, 1, animal: rig.Rabbit)),
                "The interface's default plural and Has work for any localization.");
        }

        // ------------------------------------------------------------------------------------------- helpers

        private static WishRecord W(WishTemplateId template, int target, ContentId param = default, uint animal = 0, byte extra = 0)
        {
            WishCatalog.TryGet(template, out var t);
            return new WishRecord
            {
                Template = (ushort)template, Target = target, Param = param, ParamExtra = extra, ParamKind = (byte)t.ParamKind, Animal = animal,
            };
        }

        private static WishRecord Tutorial() =>
            new WishRecord { Template = (ushort)WishTemplateId.Tutorial, Target = 2, Flags = WishRecord.FlagTutorial };

        private static JournalGoalRecord G(JournalGoalId goal, int target) => new JournalGoalRecord { Goal = (ushort)goal, Target = target };

        /// <summary>The shipped tables as LOC_en / LOC_fr hold them, behind the game's localization service.</summary>
        private LocalizationService Service(string language)
        {
            var tables = new List<LocalizationTableAsset>();
            for (int l = 0; l < 2; l++)
            {
                var table = ScriptableObject.CreateInstance<LocalizationTableAsset>();
                _tables.Add(table);
                table.languageCode = l == 0 ? "en" : "fr";
                foreach (var row in Merged())
                    table.entries.Add(new LocalizationTableAsset.Entry { key = row.Key, value = row.Value[l] });
                tables.Add(table);
            }
            return new LocalizationService(tables, language, SystemLanguage.English);
        }

        /// <summary>Another implementation of the interface (its defaults for Has and GetPlural), language switchable.</summary>
        private sealed class FakeLocalization : ILocalization
        {
            private static readonly Dictionary<string, string[]> Rows = Merged();

            public FakeLocalization(string language)
            {
                Language = language;
            }

            public string Language { get; set; }

            public string Get(string key) => Rows.TryGetValue(key, out var row) ? row[Language == "fr" ? 1 : 0] : key;
        }

        private sealed class Rig
        {
            public static readonly ContentId Grassland = ContentId.FromSource("biome.grassland");
            public static readonly ContentId Forest = ContentId.FromSource("biome.forest");
            public static readonly ContentId RabbitId = ContentId.FromSource("species.rabbit");
            public static readonly ContentId SquirrelId = ContentId.FromSource("species.squirrel");
            public static readonly ContentId HedgehogId = ContentId.FromSource("species.hedgehog");

            public PlayerState State;
            public DailyVisitTextFormatter Formatter;
            public uint Mochi;
            public uint Rabbit;
            public uint Squirrel;

            public static Rig Create(ILocalization text)
            {
                var builder = new ContentRegistryBuilder();
                builder.AddBiome(new BiomeDefinition(Grassland, "biome.grassland.name", 0), "biome.grassland");
                builder.AddBiome(new BiomeDefinition(Forest, "biome.forest.name", 1), "biome.forest");
                foreach (var name in new[] { "rabbit", "squirrel", "hedgehog" })
                    builder.AddSpecies(new SpeciesDefinition(ContentId.FromSource("species." + name), "species." + name + ".name", RigFamily.SmallQuadruped,
                        Grassland, 1.0, "PF_Animal_Test"), "species." + name);
                var content = builder.Build();
                var state = PlayerState.CreateNew(TestUtil.T0);
                var rig = new Rig { State = state };
                rig.Mochi = state.Collection.Add(new AnimalInstance { Species = RabbitId });
                state.Collection.CustomNames[rig.Mochi] = "Mochi";
                rig.Rabbit = state.Collection.Add(new AnimalInstance { Species = RabbitId });
                rig.Squirrel = state.Collection.Add(new AnimalInstance { Species = SquirrelId });
                rig.Formatter = new DailyVisitTextFormatter(content, text, state);
                return rig;
            }
        }
    }
}
