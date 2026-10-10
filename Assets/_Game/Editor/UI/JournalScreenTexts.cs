using System;
using System.Collections.Generic;
using System.Reflection;
using PocketWilds.Presentation;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T44C « Journal du refuge et visiteurs rares » shipped texts (doc « 01 Succès, titres, cadres, poses et journal », section
    /// 6; doc 05 sheet 25; Writing Guide: short, warm, no em dash, no pressure words): the journal screen's words, the dates,
    /// the « Journal » entry on the profile card, the visitor's « Nouveau visiteur ! » banner and its gift pop. Columns: key,
    /// en, fr. Merged into LOC_en / LOC_fr by <see cref="UiPipeline.EnsureLocalization"/>, with agent CC's JournalTexts (the
    /// memories « memory.….text », the surprises' and visitors' names), found by <see cref="ContentTexts"/>. Keys are
    /// <see cref="JournalText"/>'s (« journal. » is the weekly notebook's prefix: the screen's words use « refuge_journal. »).
    /// <para>Counted texts come as three rows: « key.one », « key.other » and the key itself. French uses a no-break space
    /// before « : », « ? » and « ! ».</para>
    /// </summary>
    public static class JournalScreenTexts
    {
        private const string Nbsp = " ";

        public static string[,] Texts => s_texts ??= Build();

        private static string[,] s_texts;

        private static readonly string[] MonthsEn =
            { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

        private static readonly string[] MonthsFr =
            { "Janvier", "Février", "Mars", "Avril", "Mai", "Juin", "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre" };

        private static string[,] Build()
        {
            var rows = new List<string[]>
            {
                new[] { JournalText.Title, "Refuge journal", "Journal du refuge" },
                new[] { JournalText.Entry, "Journal", "Journal" },
                new[] { JournalText.TabPages, "Memories", "Souvenirs" },
                new[] { JournalText.TabMoments, "Moments", "Moments" },
                new[] { JournalText.Empty, "The refuge's pages will write themselves here.", "Les pages du refuge s'écriront ici toutes seules." },
                new[] { JournalText.MomentsEmpty, "The world's little moments will appear here", "Les petits moments du monde apparaîtront ici" },
                new[] { JournalText.New, "New", "Nouveau" },
                new[] { JournalText.Date, "{month} {day}, {year}", "{day} {month} {year}" },
                new[] { JournalText.MonthHeader, "{month} {year}", "{month} {year}" },
                new[] { JournalText.NewVisitor, "New visitor!", "Nouveau visiteur" + Nbsp + "!" },
                new[] { JournalText.GiftSome, "A little gift!", "Un petit cadeau" + Nbsp + "!" },
            };
            for (int m = 0; m < 12; m++)
            {
                rows.Add(new[] { JournalText.Months[m], MonthsEn[m], MonthsFr[m] });
                rows.Add(new[] { JournalText.MonthsInDate[m], MonthsEn[m], MonthsFr[m].ToLowerInvariant() });
            }
            Counted(rows, JournalText.GiftGems, "Gift: +{n} Gem", "Gift: +{n} Gems", "Cadeau" + Nbsp + ": +{n} Gemme", "Cadeau" + Nbsp + ": +{n} Gemmes");
            Counted(rows, JournalText.GiftLight, "Gift: {n} minute of Light", "Gift: {n} minutes of Light", "Cadeau" + Nbsp + ": {n} minute de Lumière",
                "Cadeau" + Nbsp + ": {n} minutes de Lumière");
            Counted(rows, JournalText.GiftTreats, "Gift: {n} treat", "Gift: {n} treats", "Cadeau" + Nbsp + ": {n} friandise", "Cadeau" + Nbsp + ": {n} friandises");
            Counted(rows, JournalText.GiftSeeds, "Gift: {n} seed", "Gift: {n} seeds", "Cadeau" + Nbsp + ": {n} graine", "Cadeau" + Nbsp + ": {n} graines");
            Counted(rows, JournalText.GiftTickets, "Gift: {n} ticket", "Gift: {n} tickets", "Cadeau" + Nbsp + ": {n} ticket", "Cadeau" + Nbsp + ": {n} tickets");
            Counted(rows, JournalText.GiftStickers, "Gift: {n} sticker", "Gift: {n} stickers", "Cadeau" + Nbsp + ": {n} autocollant",
                "Cadeau" + Nbsp + ": {n} autocollants");
            var table = new string[rows.Count, 3];
            for (int i = 0; i < rows.Count; i++)
                for (int c = 0; c < 3; c++)
                    table[i, c] = rows[i][c];
            return table;
        }

        private static void Counted(List<string[]> rows, string key, string enOne, string enOther, string frOne, string frOther)
        {
            rows.Add(new[] { key + ".one", enOne, frOne });
            rows.Add(new[] { key + ".other", enOther, frOther });
            rows.Add(new[] { key, enOther, frOther });
        }

        /// <summary>The English and French texts of keys (this table, then agent CC's), for the pipelines' measures.</summary>
        public static string[] Of(params string[] keys)
        {
            var found = new List<string>();
            foreach (var table in new[] { Texts, ContentTexts() })
                foreach (string key in keys)
                    for (int i = 0; i < table.GetLength(0); i++)
                        if (table[i, 0] == key)
                        {
                            found.Add(table[i, 1]);
                            found.Add(table[i, 2]);
                        }
            return found.ToArray();
        }

        /// <summary>Agent CC's texts whose key starts with <paramref name="prefix"/> and ends with <paramref name="suffix"/> (en, fr).</summary>
        public static string[] ContentOf(string prefix, string suffix)
        {
            var table = ContentTexts();
            var found = new List<string>();
            for (int i = 0; i < table.GetLength(0); i++)
            {
                string key = table[i, 0];
                if (key == null || !key.StartsWith(prefix, StringComparison.Ordinal) || !key.EndsWith(suffix, StringComparison.Ordinal))
                    continue;
                found.Add(table[i, 1]);
                found.Add(table[i, 2]);
            }
            return found.ToArray();
        }

        private static readonly string[] ContentTableTypes = { "JournalTexts", "VisitorTexts", "SurpriseTexts" };

        /// <summary>Agent CC's tables (JournalTexts and friends in Game.Editor, a static « Texts » string[,] of key, en, fr), merged; empty when not there yet.</summary>
        public static string[,] ContentTexts()
        {
            var rows = new List<string[]>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "Game.Editor")
                    continue;
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }
                foreach (var type in types)
                {
                    if (type == null || Array.IndexOf(ContentTableTypes, type.Name) < 0)
                        continue;
                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                    object value = type.GetProperty("Texts", flags)?.GetValue(null) ?? type.GetField("Texts", flags)?.GetValue(null);
                    if (value is not string[,] table || table.GetLength(1) < 3)
                        continue;
                    for (int i = 0; i < table.GetLength(0); i++)
                        rows.Add(new[] { table[i, 0], table[i, 1], table[i, 2] });
                }
            }
            var merged = new string[rows.Count, 3];
            for (int i = 0; i < rows.Count; i++)
                for (int c = 0; c < 3; c++)
                    merged[i, c] = rows[i][c];
            return merged;
        }
    }
}
