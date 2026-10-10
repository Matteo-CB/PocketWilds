using System;
using System.Collections.Generic;
using System.Reflection;
using PocketWilds.Presentation;
using PocketWilds.Simulation;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T44A « Albums » shipped texts (doc « 01 Albums et autocollants », table 1.13, and the albums screen's own words;
    /// Writing Guide: short, warm, no em dash, no pressure words). Columns: key, en, fr. Merged into LOC_en / LOC_fr by
    /// <see cref="UiPipeline.EnsureLocalization"/>. Keys are <see cref="AlbumText"/>'s. The names of the albums, pages and
    /// stickers (« {key}.name ») come from agent AC's generated table, found by <see cref="ContentNames"/>.
    /// <para>Counted texts come as three rows: « key.one », « key.other » and the key itself. French uses a no-break space
    /// before « : », « ? » and « ! ». The doc's « Déjà collé : +{0} étoiles » keeps its {0} (the view writes it as {n}).</para>
    /// <para>Long labels, measured by AlbumScreenPipeline at the reference 1080 × 2340: « Choisir un autocollant » (44, box
    /// 470), « 10 étoiles pour cet autocollant ? » (44, two lines of 700), « Déjà collé : +3 étoiles » (44, box 900).</para>
    /// </summary>
    public static class AlbumTexts
    {
        /// <summary>Built on first use.</summary>
        public static string[,] Texts => s_texts ??= Build();

        private static string[,] s_texts;

        private const string Nbsp = " ";

        /// <summary>The 11 hint sources in <see cref="StickerHint"/> order (table 1.13; the garden's is the screen's own).</summary>
        private static readonly string[,] Hints =
        {
            { "Earn stars while playing", "Gagne des étoiles en jouant" },
            { "Beat your best", "Bats ton record" },
            { "Clear the challenges", "Réussis les défis" },
            { "Grant wishes", "Réalise des souhaits" },
            { "Go on an expedition", "Pars en expédition" },
            { "Open the daily gift", "Ouvre le Cadeau du jour" },
            { "Progress in the event", "Avance dans l'événement" },
            { "Greet this visitor", "Salue ce visiteur" },
            { "Grow the garden", "Fais pousser le jardin" },
            { "Open this area", "Ouvre cette zone" },
            { "Keep an eye on this place", "Regarde bien le biome" },
        };

        private static string[,] Build()
        {
            var rows = new List<string[]>(64);

            void Plain(string key, string en, string fr) => rows.Add(new[] { key, en, fr });

            void Counted(string key, string enOne, string enOther, string frOne, string frOther)
            {
                rows.Add(new[] { key, enOther, frOther });
                rows.Add(new[] { key + ".one", enOne, frOne });
                rows.Add(new[] { key + ".other", enOther, frOther });
            }

            // Table 1.13.
            Plain(AlbumText.Title, "Albums", "Albums");
            Plain(AlbumText.Stars, "Album stars", "Étoiles d'album");
            Plain(AlbumText.Choose, "Pick a sticker", "Choisir un autocollant");
            Plain(AlbumText.ChooseConfirm, "10 stars for this sticker?", "10" + Nbsp + "étoiles pour cet autocollant" + Nbsp + "?");
            Plain(AlbumText.New, "New", "Nouveau");
            Counted(AlbumText.Duplicate, "Already in: +{0} star", "Already in: +{0} stars", "Déjà collé" + Nbsp + ": +{0}" + Nbsp + "étoile",
                "Déjà collé" + Nbsp + ": +{0}" + Nbsp + "étoiles");
            Plain(AlbumText.PageHalf, "Page half full", "Page à moitié pleine");
            Plain(AlbumText.PageComplete, "Page complete!", "Page complète" + Nbsp + "!");
            Plain(AlbumText.AlbumComplete, "Album complete!", "Album complet" + Nbsp + "!");
            Plain(AlbumText.Companion, "Companion", "Compagnon");
            for (int i = 0; i < Hints.GetLength(0); i++)
                Plain(AlbumText.Hint((StickerHint)i), Hints[i, 0], Hints[i, 1]);
            Plain(AlbumText.Odds, "Sticker odds", "Chances des autocollants");
            Plain("shop.tickets.stickers", "With a bought ticket, stickers follow the page order.",
                "Avec un billet acheté, les autocollants suivent l'ordre de la page.");
            Plain("shop.tickets.next_sticker", "Next sticker: {0}", "Prochain autocollant" + Nbsp + ": {0}");

            // The screen's own words (rarity words are never the only sign: the border, the dot or the pearl go with them).
            Plain(AlbumText.Common, "Common", "Commun");
            Plain(AlbumText.Rare, "Rare", "Rare");
            Plain(AlbumText.Shiny, "Shiny", "Brillant");
            Plain(AlbumText.Soon, "Soon", "Bientôt");
            Plain(AlbumText.Yes, "Yes", "Oui");
            Plain(AlbumText.No, "Not now", "Pas maintenant");
            Counted(AlbumText.Missing, "{n} to find", "{n} to find", "{n} à trouver", "{n} à trouver");
            Plain(AlbumText.NotEnoughStars, "A sticker takes 10 album stars", "Un autocollant demande 10" + Nbsp + "étoiles d'album");
            Plain(AlbumText.ChooseHint, "Tap a missing sticker", "Touche un autocollant manquant");
            Plain(AlbumText.Locked, "Opens later", "S'ouvre plus tard");
            Plain(AlbumText.Closed, "This album opens later", "Cet album s'ouvre plus tard");

            var table = new string[rows.Count, 3];
            for (int i = 0; i < rows.Count; i++)
            {
                table[i, 0] = rows[i][0];
                table[i, 1] = rows[i][1];
                table[i, 2] = rows[i][2];
            }
            return table;
        }

        /// <summary>The type names agent AC's generated names table may have (key, en, fr in a static string[,] Texts).</summary>
        private static readonly string[] ContentTableTypes = { "StickerTexts", "AlbumContentTexts", "StickerNames", "AlbumStickerTexts" };

        /// <summary>
        /// The albums', pages' and stickers' names (rows whose key ends with « .name ») from agent AC's generated table, found
        /// by reflection so the screen compiles without it; empty until it exists.
        /// </summary>
        public static string[,] ContentNames()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (name != "Game.Editor")
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
