using System.Collections.Generic;
using PocketWilds.Presentation;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T42 « Expéditions » shipped texts (doc 01 Contenu section 7.6, the destinations of tables 7.4 and 7.5, the 10
    /// challenges of S-ACT-005, tutorial A18 and A19; doc 05 sheet 23 « Repartir » and « Tout réclamer »; Writing Guide:
    /// short, warm, no em dash, no pressure words, real time never a countdown). Columns: key, en, fr. Merged into LOC_en /
    /// LOC_fr by <see cref="UiPipeline.EnsureLocalization"/>. Keys are <see cref="ExpeditionText"/>'s; a destination's name is
    /// « {runtime id}.name », a challenge's « challenge.expeditions.nn.name » and « .goal » (its destination, duration and
    /// constraint on one line of the « Défis » row).
    /// <para>Counted texts come as three rows: « key.one », « key.other » and the key itself (the usual plural). French uses
    /// a no-break space before « : » and between a number and its unit (« 3 h 12 », « 25 % »).</para>
    /// <para>Long labels, measured against the screen's boxes at the reference 1080 × 2340 (Pocket Round Bold, the sizes of
    /// ExpeditionPipeline): the slot's « Voir la carte postale » (46 units, box 430, auto-size to 34), « Finir maintenant »
    /// (36, box 260, auto-size to 26), « Nouvel emplacement » (40, box 430), the sheet's « Équipe conseillée » (40, box 420),
    /// « Première balade : 3 min » (40, chip 620); destination names wrap on two lines in their 320 wide card (32, auto-size
    /// to 24: « Colline des étoiles filantes », the longest, fits two lines at 32).</para>
    /// </summary>
    public static class ExpeditionTexts
    {
        /// <summary>Built on first use: the destination tables below are static fields too, initialised in text order.</summary>
        public static string[,] Texts => s_texts ??= Build();

        private static string[,] s_texts;

        private const string Nbsp = " ";

        /// <summary>The 13 biome destinations (doc 01 table 7.4): runtime id, English, French.</summary>
        public static readonly string[,] BiomeDestinations =
        {
            { "expedition.flower_meadows", "Flower Meadows", "Prés fleuris" },
            { "expedition.stump_wood", "Stump Wood", "Bois des souches" },
            { "expedition.reed_shores", "Reed Shores", "Rives aux roseaux" },
            { "expedition.torrent_pasture", "Torrent Pasture", "Alpage du torrent" },
            { "expedition.frosted_pines", "Frosted Pines", "Pinède givrée" },
            { "expedition.turquoise_falls", "Turquoise Falls", "Cascade turquoise" },
            { "expedition.arch_oasis", "Arch Oasis", "Oasis des arches" },
            { "expedition.petal_bridge", "Petal Bridge", "Pont des pétales" },
            { "expedition.mushroom_ring", "Mushroom Ring", "Cercle de champignons" },
            { "expedition.cloud_archipelago", "Cloud Archipelago", "Archipel des nuages" },
            { "expedition.moon_garden", "Moon Garden", "Jardin de lune" },
            { "expedition.fern_valley", "Fern Valley", "Vallée des fougères" },
            { "expedition.nebula_shore", "Nebula Shore", "Rivage des nébuleuses" },
        };

        /// <summary>The 24 event destinations (doc 01 table 7.5): runtime id, English, French.</summary>
        public static readonly string[,] EventDestinations =
        {
            { "expedition.aurora_frozen_lake", "Northern Frozen Lake", "Lac gelé du Nord" },
            { "expedition.aurora_veil_plateau", "Veil Plateau", "Plateau des voiles" },
            { "expedition.contest_meadow_stage", "Meadow Stage", "Scène des prés" },
            { "expedition.contest_ribbon_lane", "Ribbon Lane", "Allée des rubans" },
            { "expedition.burrows_bank", "Burrow Bank", "Talus aux terriers" },
            { "expedition.burrows_root_tunnel", "Root Tunnel", "Galerie des racines" },
            { "expedition.bloom_petal_valley", "Petal Valley", "Vallée des corolles" },
            { "expedition.bloom_white_orchard", "White Orchard", "Verger blanc" },
            { "expedition.migration_faraway_hills", "Faraway Hills", "Collines lointaines" },
            { "expedition.migration_sea_of_clouds", "Sea of Clouds", "Mer de nuages" },
            { "expedition.fireflies_pond", "Firefly Pond", "Étang des lucioles" },
            { "expedition.fireflies_lantern_glade", "Lantern Glade", "Clairière des lampions" },
            { "expedition.sunny_golden_cove", "Golden Cove", "Crique dorée" },
            { "expedition.sunny_shell_islet", "Shell Islet", "Îlot des coquillages" },
            { "expedition.stars_shooting_hill", "Shooting Star Hill", "Colline des étoiles filantes" },
            { "expedition.stars_stardust_dunes", "Stardust Dunes", "Dunes de poussière d'étoile" },
            { "expedition.winds_windy_ridges", "Windy Ridges", "Crêtes venteuses" },
            { "expedition.winds_kite_plain", "Kite Plain", "Plaine des cerfs-volants" },
            { "expedition.harvest_autumn_orchard", "Autumn Orchard", "Verger d'automne" },
            { "expedition.harvest_golden_oaks", "Golden Oak Wood", "Chênaie dorée" },
            { "expedition.mushrooms_glowing_undergrowth", "Glowing Undergrowth", "Sous-bois lumineux" },
            { "expedition.mushrooms_hidden_clearing", "Hidden Clearing", "Clairière cachée" },
            { "expedition.frost_frost_lake", "Frost Lake", "Lac de givre" },
            { "expedition.frost_snowy_valley", "Snowy Valley", "Vallée enneigée" },
        };

        private static string[,] Build()
        {
            var rows = new List<string[]>(160);

            void Plain(string key, string en, string fr) => rows.Add(new[] { key, en, fr });

            void Counted(string key, string enOne, string enOther, string frOne, string frOther)
            {
                rows.Add(new[] { key, enOther, frOther });
                rows.Add(new[] { key + ".one", enOne, frOne });
                rows.Add(new[] { key + ".other", enOther, frOther });
            }

            // Doc 01, table 7.6.
            Plain(ExpeditionText.Title, "Expeditions", "Expéditions");
            Plain(ExpeditionText.PickTeam, "Pick your team", "Choisis ton équipe");
            Plain(ExpeditionText.Suggested, "Suggested team", "Équipe conseillée");
            Plain(ExpeditionText.Duration, "Duration", "Durée");
            Plain(ExpeditionText.Go, "Go", "Partir");
            Plain(ExpeditionText.BackIn, "Back in {time}", "De retour dans {time}");
            Plain(ExpeditionText.Back, "Expedition back", "Expédition de retour");
            Plain(ExpeditionText.OpenPostcard, "See the postcard", "Voir la carte postale");
            Plain(ExpeditionText.SpeedUp, "Finish now", "Finir maintenant");
            Plain(ExpeditionText.SlotLocked, "New slot", "Nouvel emplacement");
            Plain(ExpeditionText.FirstStroll, "First stroll: 3 min", "Première balade" + Nbsp + ": 3" + Nbsp + "min");
            Counted(ExpeditionText.DiscoveryGuarantee, "Discovery egg guaranteed in {n} long expedition",
                "Discovery egg guaranteed in {n} long expeditions", "Œuf Découverte garanti dans {n} longue expédition",
                "Œuf Découverte garanti dans {n} longues expéditions");

            // The screen's own words (doc 05 sheet 23: « Repartir » / "Send again", « Tout réclamer » / "Claim all").
            Plain(ExpeditionText.SendAgain, "Send again", "Repartir");
            Plain(ExpeditionText.ClaimAll, "Claim all", "Tout réclamer");
            Plain(ExpeditionText.Destination, "Destination", "Destination");
            Plain(ExpeditionText.Team, "Team", "Équipe");
            Plain(ExpeditionText.Loot, "They may bring back", "Ce qu'ils peuvent rapporter");
            Plain(ExpeditionText.Challenges, "Challenges", "Défis");
            Plain(ExpeditionText.SlotClub, "With the Wild Club", "Avec le Wild Club");
            Plain(ExpeditionText.SlotConfirm, "Tap again to open it", "Touche encore pour l'ouvrir");
            Plain(ExpeditionText.NeedGems, "Not enough Gems yet", "Pas encore assez de Gemmes");
            Plain(ExpeditionText.ClubSoon, "The Wild Club is coming soon", "Le Wild Club arrive bientôt");
            Plain(ExpeditionText.NoSlot, "Every slot is busy", "Tous les emplacements sont pris");
            Plain(ExpeditionText.DestinationLocked, "Opens with its biome", "S'ouvre avec son biome");
            Plain(ExpeditionText.AnimalAway, "Already on an expedition", "Déjà en expédition");
            Plain(ExpeditionText.TeamFull, "Three friends at most", "Trois amis au plus");
            Plain(ExpeditionText.NoAnimal, "Pick a friend first", "Choisis d'abord un ami");
            Counted(ExpeditionText.BackCount, "{n} back", "{n} back", "{n} de retour", "{n} de retour");
            Counted(ExpeditionText.SpeedUpsLeft, "{n} left today", "{n} left today", "Encore {n} aujourd'hui", "Encore {n} aujourd'hui");
            Plain(ExpeditionText.SpeedUpsNone, "More tomorrow", "D'autres demain");

            // The team rating's reasons (doc 01, section 7.2).
            Plain(ExpeditionText.Rating, "Team rating", "Note d'équipe");
            Counted(ExpeditionText.ReasonBiome, "{n} from here", "{n} from here", "{n} d'ici", "{n} d'ici");
            Plain(ExpeditionText.ReasonFamily, "Favourite family", "Famille préférée");
            Plain(ExpeditionText.ReasonSpecies, "3 species", "3" + Nbsp + "espèces");

            // Durations and times (the screens write {m} on two digits after hours).
            Plain(ExpeditionText.TimeHoursMinutes, "{h} h {m}", "{h}" + Nbsp + "h" + Nbsp + "{m}");
            Plain(ExpeditionText.TimeHours, "{h} h", "{h}" + Nbsp + "h");
            Plain(ExpeditionText.TimeMinutes, "{n} min", "{n}" + Nbsp + "min");
            Plain(ExpeditionText.TimeSeconds, "{n} s", "{n}" + Nbsp + "s");
            Plain(ExpeditionText.Percent, "{n}%", "{n}" + Nbsp + "%");

            // The 37 destinations (doc 01 tables 7.4 and 7.5).
            for (int i = 0; i < BiomeDestinations.GetLength(0); i++)
                Plain(ExpeditionText.DestinationName(BiomeDestinations[i, 0]), BiomeDestinations[i, 1], BiomeDestinations[i, 2]);
            for (int i = 0; i < EventDestinations.GetLength(0); i++)
                Plain(ExpeditionText.DestinationName(EventDestinations[i, 0]), EventDestinations[i, 1], EventDestinations[i, 2]);

            // The 10 challenges (doc 01 S-ACT-005 « Défis »): the doc's French names, natural English ones, and each
            // constraint in one short line (destination, duration, team).
            // Each constraint fits two lines of a 390 wide « Défis » cell at the caption size (36).
            Challenge(1, "First Trip", "Premier voyage", "Flower Meadows, 1 h", "Prés fleuris, 1" + Nbsp + "h");
            Challenge(2, "Woodland Team", "Équipe des bois", "Stump Wood, 4 h, 2 from the Forest", "Bois des souches, 4" + Nbsp + "h, 2 de la Forêt");
            Challenge(3, "By the Water", "Au bord de l'eau", "Reed Shores, 4 h, a swimmer", "Rives aux roseaux, 4" + Nbsp + "h, un nageur");
            Challenge(4, "Big Departure", "Grand départ", "Anywhere, 8 h, 3 animals", "Partout, 8" + Nbsp + "h, 3 animaux");
            Challenge(5, "Little Ones", "Tout petits", "Anywhere, 4 h, 3 small ones", "Partout, 4" + Nbsp + "h, 3 petits");
            Challenge(6, "Wings in the Wind", "Ailes au vent", "Torrent Pasture, 8 h, a bird", "Alpage du torrent, 8" + Nbsp + "h, un oiseau");
            Challenge(7, "Mixed Trio", "Trio varié", "Anywhere, 8 h, 3 families", "Partout, 8" + Nbsp + "h, 3 familles");
            Challenge(8, "Long Road", "Longue route", "Anywhere, 12 h", "Partout, 12" + Nbsp + "h");
            Challenge(9, "Back to the Roots", "Retour aux sources", "A biome, 8 h, 3 from it", "Un biome, 8" + Nbsp + "h, 3 de ce biome");
            Challenge(10, "The Grand Journey", "Le grand voyage", "Newest biome, 12 h, 3 biomes", "Dernier biome, 12" + Nbsp + "h, 3 biomes");

            void Challenge(int number, string enName, string frName, string enGoal, string frGoal)
            {
                Plain(ExpeditionText.ChallengeName(number - 1), enName, frName);
                Plain(ExpeditionText.ChallengeGoal(number - 1), enGoal, frGoal);
            }

            var table = new string[rows.Count, 3];
            for (int i = 0; i < rows.Count; i++)
            {
                table[i, 0] = rows[i][0];
                table[i, 1] = rows[i][1];
                table[i, 2] = rows[i][2];
            }
            return table;
        }
    }
}
