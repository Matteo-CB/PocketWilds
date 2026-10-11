using System.Collections.Generic;
using PocketWilds.Content;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T48 shipped texts of the event quests (doc Événements, templates EQ-D-01 to 30 and EQ-L-01 to 20, the doc's sentences). Columns:
    /// key, en, fr. Keys come from <see cref="EventQuestTemplate.TextKey"/> (« event.quest.eq_d_01 »). The numbers are written out and
    /// match each template's target; only EQ-D-23 names {n} (its target drops to the open biomes when they are fewer than 3) and has a
    /// « .one » row for a single biome. {activity} and {currency} are filled at runtime by
    /// <see cref="PocketWilds.Presentation.EventQuestText"/>: the quest activity or the activity of the day, the event currency's name.
    /// French uses a no-break space between a number and its unit, a narrow no-break space for thousands.
    /// </summary>
    public static class EventQuestTexts
    {
        private const string Nbsp = " ";
        private const string Thin = " ";

        public static readonly string[,] Texts = Build();

        private static string[,] Build()
        {
            var rows = new List<string[]>(56);

            void Daily(int number, string en, string fr) => rows.Add(new[] { EventQuestCatalog.Daily[number - 1].TextKey, en, fr });

            void Long(int number, string en, string fr) => rows.Add(new[] { EventQuestCatalog.Long[number - 1].TextKey, en, fr });

            // ---------------- daily quests (40 each, one per group a day) ----------------
            Daily(1, "Hatch 10 eggs", "Fais éclore 10 œufs");
            Daily(2, "Open 1 event egg", "Ouvre 1 œuf de l'événement");
            Daily(3, "Play {activity} twice", "Joue 2 parties de {activity}");
            Daily(4, "Earn 5 stars in activities", "Gagne 5 étoiles en activités");
            Daily(5, "Grant 2 wishes", "Accomplis 2 souhaits");
            Daily(6, "Start 1 expedition", "Lance 1 expédition");
            Daily(7, "Welcome back 1 expedition", "Accueille 1 expédition de retour");
            Daily(8, "Groom 3 animals", "Brosse 3 animaux");
            Daily(9, "Harvest 2 plants", "Récolte 2 plantes");
            Daily(10, "Plant 3 seeds", "Plante 3 graines");
            Daily(11, "Place 1 decoration", "Place 1 décoration");
            Daily(12, "Take 1 photo", "Prends 1 photo");
            Daily(13, "Pet 10 animals", "Caresse 10 animaux");
            Daily(14, "Open the daily gift", "Ouvre le Cadeau du jour");
            Daily(15, "Play 3 different activities", "Joue à 3 activités différentes");
            Daily(16, "Earn 1 challenge star", "Gagne 1 étoile de défi");
            Daily(17, "Collect Light 5 times", "Récupère la Lumière 5 fois");
            Daily(18, "Hatch 1 Focus egg", "Fais éclore 1 œuf Focus");
            Daily(19, "Give 3 treats", "Donne 3 friandises");
            Daily(20, "Hatch 5 animals in your newest biome", "Fais éclore 5 animaux dans ton dernier biome");
            Daily(21, "Get 3 stars in one round", "Obtiens 3 étoiles dans une partie");
            Daily(22, "Play 1 round of {activity}", "Joue 1 partie de {activity}");
            Daily(23, "Visit {n} biomes", "Rends visite à {n} biomes");
            rows.Add(new[] { EventQuestCatalog.Daily[22].TextKey + ".one", "Visit 1 biome", "Rends visite à 1 biome" });
            Daily(24, "Equip 1 accessory", "Équipe 1 accessoire");
            Daily(25, "Earn 200 {currency}", "Gagne 200 {currency}");
            Daily(26, "Get 2 stars in {activity}", "Obtiens 2 étoiles à {activity}");
            Daily(27, "Buy 1 upgrade", "Achète 1 amélioration");
            Daily(28, "Start 1 breeding", "Lance 1 reproduction");
            Daily(29, "Water 2 plants", "Arrose 2 plantes");
            Daily(30, "Start 2 expeditions of 1" + Nbsp + "h or more", "Lance 2 expéditions de 1" + Nbsp + "h ou plus");

            // ---------------- long quests (150 each, 10 an event) ----------------
            Long(1, "Hatch 150 eggs", "Fais éclore 150 œufs");
            Long(2, "Earn 60 stars in activities", "Gagne 60 étoiles en activités");
            Long(3, "Play {activity} 15 times", "Joue 15 parties de {activity}");
            Long(4, "Grant 20 wishes", "Accomplis 20 souhaits");
            Long(5, "Complete 8 expeditions", "Termine 8 expéditions");
            Long(6, "Open 8 event eggs", "Ouvre 8 œufs de l'événement");
            Long(7, "Earn 12 challenge stars", "Gagne 12 étoiles de défi");
            Long(8, "Reach tier 15 of the event track", "Atteins le palier 15 du parcours");
            Long(9, "Groom 25 animals", "Brosse 25 animaux");
            Long(10, "Harvest 15 plants", "Récolte 15 plantes");
            Long(11, "Place 3 event decorations", "Place 3 décorations de l'événement");
            Long(12, "Take 8 photos", "Prends 8 photos");
            Long(13, "Pet 150 animals", "Caresse 150 animaux");
            Long(14, "Get 3 items from the event egg", "Obtiens 3 objets de l'œuf de l'événement");
            Long(15, "Earn 3,000 {currency}", "Gagne 3" + Thin + "000 {currency}");
            Long(16, "Complete 10 weekly journal goals", "Complète 10 objectifs du Carnet de la semaine");
            Long(17, "Open 7 daily gifts", "Ouvre 7 Cadeaux du jour");
            Long(18, "Play 30 activity rounds", "Joue 30 parties d'activités");
            Long(19, "Discover 3 new variants", "Découvre 3 nouvelles variantes");
            Long(20, "Give 30 treats", "Donne 30 friandises");

            var table = new string[rows.Count, 3];
            for (int i = 0; i < rows.Count; i++)
                for (int c = 0; c < 3; c++)
                    table[i, c] = rows[i][c];
            return table;
        }
    }
}
