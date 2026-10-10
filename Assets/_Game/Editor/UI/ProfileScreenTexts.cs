using System;
using System.Collections.Generic;
using System.Reflection;
using PocketWilds.Presentation;
using PocketWilds.Simulation;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T44B « Succès, titres, cadres, poses » shipped texts (doc « 01 Succès, titres, cadres, poses et journal », sections 1
    /// and 5, and the profile screen's own words; Writing Guide: short, warm, no em dash, no pressure words). Columns: key,
    /// en, fr. Merged into LOC_en / LOC_fr by <see cref="UiPipeline.EnsureLocalization"/>. Keys are <see cref="ProfileText"/>'s.
    /// The names, goals and sources of the 200 achievements, 322 titles, 42 frames and 47 poses (« {key}.name », « .goal »,
    /// « .source ») come from agent BC's generated table, found by <see cref="ContentNames"/>.
    /// <para>Counted texts come as three rows: « key.one », « key.other » and the key itself. French uses a no-break space
    /// before « : », « ? » and « ! ».</para>
    /// </summary>
    public static class ProfileScreenTexts
    {
        /// <summary>Built on first use.</summary>
        public static string[,] Texts => s_texts ??= Build();

        private static string[,] s_texts;

        /// <summary>The 13 categories in <see cref="AchievementCategory"/> order (section 1 « Répartition »).</summary>
        private static readonly string[,] Categories =
        {
            { "Collection", "Collection" },
            { "Biomes", "Biomes" },
            { "Mastery", "Maîtrise" },
            { "Hatching", "Éclosions" },
            { "Activities", "Activités" },
            { "Daily visits", "Rendez-vous du jour" },
            { "Expeditions and Garden", "Expéditions et Jardin" },
            { "Albums", "Albums" },
            { "Decoration", "Décoration" },
            { "Bonds and companions", "Liens et compagnons" },
            { "Events", "Événements" },
            { "Renewal and Essence", "Renouveau et Essence" },
            { "Refuge", "Refuge" },
        };

        private static string[,] Build()
        {
            var rows = new List<string[]>(48);

            void Plain(string key, string en, string fr) => rows.Add(new[] { key, en, fr });

            void Counted(string key, string enOne, string enOther, string frOne, string frOther)
            {
                rows.Add(new[] { key, enOther, frOther });
                rows.Add(new[] { key + ".one", enOne, frOne });
                rows.Add(new[] { key + ".other", enOther, frOther });
            }

            Plain(ProfileText.Title, "My profile", "Mon profil");
            Plain(ProfileText.TabAchievements, "Achievements", "Succès");
            Plain(ProfileText.TabTitles, "Titles", "Titres");
            Plain(ProfileText.TabFrames, "Frames", "Cadres");
            Plain(ProfileText.TabPoses, "Poses", "Poses");
            Plain(ProfileText.All, "All", "Tout");
            Plain(ProfileText.Claim, "Claim", "Réclamer");
            // Section 1: « Plus que 3 » (« 3 to go »).
            Counted(ProfileText.ToGo, "{n} to go", "{n} to go", "Plus que {n}", "Plus que {n}");
            Plain(ProfileText.Complete, "All tiers done", "Tous les paliers");
            Counted(ProfileText.Claimed, "+{n} Gem", "+{n} Gems", "+{n} Gemme", "+{n} Gemmes");
            Plain(ProfileText.New, "New", "Nouveau");
            Plain(ProfileText.NoTitle, "No title", "Aucun titre");
            Plain(ProfileText.NoTitleSource, "Nothing under your name", "Rien sous ton nom");
            Plain(ProfileText.PlainFrame, "Simple frame", "Cadre simple");
            Plain(ProfileText.Locked, "Not earned yet", "Pas encore gagné");
            // Section 5.
            Plain(ProfileText.PhotoSoon, "Photo mode soon", "Mode photo bientôt");
            Plain(ProfileText.NoRoom, "Not enough room here.", "Pas assez de place ici.");
            for (int i = 0; i < Categories.GetLength(0); i++)
                Plain(ProfileText.Category((AchievementCategory)i), Categories[i, 0], Categories[i, 1]);

            var table = new string[rows.Count, 3];
            for (int i = 0; i < rows.Count; i++)
            {
                table[i, 0] = rows[i][0];
                table[i, 1] = rows[i][1];
                table[i, 2] = rows[i][2];
            }
            return table;
        }

        /// <summary>The type names agent BC's generated table may have (key, en, fr in a static string[,] Texts).</summary>
        private static readonly string[] ContentTableTypes = { "ProfileTexts", "AchievementTexts", "ProfileContentTexts", "ProfileItemTexts" };

        /// <summary>
        /// The achievements', titles', frames' and poses' names, goals and sources from agent BC's generated table, found by
        /// reflection so the screen compiles without it; empty until it exists.
        /// </summary>
        public static string[,] ContentNames()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "Game.Editor")
                    continue;
                foreach (var type in assembly.GetTypes())
                {
                    if (Array.IndexOf(ContentTableTypes, type.Name) < 0)
                        continue;
                    var flags = BindingFlags.Public | BindingFlags.Static;
                    object value = type.GetProperty("Texts", flags)?.GetValue(null) ?? type.GetField("Texts", flags)?.GetValue(null);
                    if (value is string[,] table && table.GetLength(1) >= 3)
                        return table;
                }
            }
            return new string[0, 3];
        }
    }
}
