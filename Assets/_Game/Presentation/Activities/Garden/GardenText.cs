using System.Text;
using PocketWilds.Simulation;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T43 « Jardin »: the overlay's localization keys (doc 01 Contenu table 8.6 and the overlay's own words) and the ids
    /// the overlay names (the products, the 25 dyes and the 31 palettes, doc 01 sections 8.2, 9.2 and 9.3). The texts live in
    /// Editor/UI/GardenTexts.cs (English and French). Placeholders: {n} a number, {total} a second number, {time} a duration
    /// already written (« 25 min »), {list} the names of what opened. Counted texts have « .one » and « .other » forms.
    /// A plant's name is « {Key}.name » (plant.sweet_clover.name), a product's « {id}.name », a dye's « {id}.name », a
    /// palette's « {id}.name ».
    /// </summary>
    public static class GardenText
    {
        // ---------------- doc 01, table 8.6 ----------------
        public const string Title = "garden.title";
        public const string Plant = "garden.plant";
        public const string Water = "garden.water";
        public const string Harvest = "garden.harvest";
        public const string ReadyIn = "garden.ready_in";
        public const string Ready = "garden.ready";
        public const string Watered = "garden.watered";
        public const string Helped = "garden.helped";

        /// <summary>« Jardin niveau {n} »: the level-up banner, the level pill and a locked plant's packet.</summary>
        public const string LevelUp = "garden.level_up";

        public const string SpeedUp = "garden.speed_up";
        public const string NewPlot = "garden.new_plot";

        // ---------------- the overlay's own words ----------------

        /// <summary>« Tout récolter » when 2 plants or more are ripe (doc 05 sheet 24, repeated version).</summary>
        public const string HarvestAll = "garden.harvest_all";

        /// <summary>The seed sheet's title.</summary>
        public const string Seeds = "garden.seeds";

        public const string NoSeeds = "garden.no_seeds";
        public const string PlotClub = "garden.plot_club";

        /// <summary>The Gems plot's first tap arms it: « Touche encore pour l'ouvrir ».</summary>
        public const string PlotConfirm = "garden.plot_confirm";

        public const string NeedGems = "garden.need_gems";
        public const string ClubSoon = "garden.club_soon";

        /// <summary>Counted: « Encore {n} aujourd'hui » (speed-ups left).</summary>
        public const string SpeedUpsLeft = "garden.speedups_left";

        public const string SpeedUpsNone = "garden.speedups_none";
        public const string Challenges = "garden.challenges";

        /// <summary>The active challenge's progress: « {n} sur {total} ».</summary>
        public const string ChallengeProgress = "garden.challenge_progress";

        /// <summary>The can is armed: « Touche une pousse ».</summary>
        public const string CanHint = "garden.can_hint";

        public const string AlreadyWatered = "garden.already_watered";

        /// <summary>Counted: « {n} prêts » (the hub card).</summary>
        public const string ReadyCount = "garden.ready_count";

        /// <summary>The level-up banner's second line: « Nouveau : {list} ».</summary>
        public const string Opened = "garden.opened";

        public const string OpenedDyes = "garden.opened_dyes";

        /// <summary>A biome plant whose biome is not open (no content to name it).</summary>
        public const string PlantBiome = "garden.plant_biome";

        /// <summary>« Pousse heureuse »: the products doubled (garden level 7).</summary>
        public const string Happy = "garden.happy";

        /// <summary>A challenge whose condition cannot be met yet (« Teintures ouvertes », its plants).</summary>
        public const string ChallengeLocked = "garden.challenge_locked";

        public const string ChallengeStarted = "garden.challenge_started";
        public const string ChallengeStopped = "garden.challenge_stopped";

        /// <summary>The time inside « Prêt dans {time} » (shared with the expeditions: « 1 h 05 », « 25 min », « 40 s »).</summary>
        public const string TimeHoursMinutes = ExpeditionText.TimeHoursMinutes;

        public static string PlantName(string plantKey) => string.IsNullOrEmpty(plantKey) ? null : plantKey + ".name";

        /// <summary>« challenge.garden.01.name » (0 based).</summary>
        public static string ChallengeName(int challenge) => ActivityText.ChallengeNameKey(ActivityId.Garden, challenge);

        /// <summary>« challenge.garden.01.goal »: its goal and constraint in one short line.</summary>
        public static string ChallengeGoal(int challenge) => ActivityText.ChallengeGoalKey(ActivityId.Garden, challenge);

        // ---------------- ids the overlay names (doc 01 sections 8.2, 9.2, 9.3) ----------------

        /// <summary>The 25 plants (doc 01 table 8.2): the 12 starter plants by level, then the 13 biome plants.</summary>
        public static readonly string[] Plants =
        {
            "plant.sweet_clover", "plant.wild_strawberry", "plant.daisy", "plant.round_carrot", "plant.poppy", "plant.blueberry",
            "plant.lavender", "plant.golden_wheat", "plant.meadow_indigo", "plant.sunflower", "plant.dwarf_apple", "plant.round_melon",
            "plant.sweet_pea", "plant.hazel", "plant.water_bloom", "plant.blue_gentian", "plant.snow_lingonberry", "plant.dwarf_banana",
            "plant.flowering_cactus", "plant.dwarf_cherry", "plant.glow_moss", "plant.cloud_puff", "plant.moonflower", "plant.giant_fern",
            "plant.star_bloom",
        };

        /// <summary>
        /// Every product a harvest can give, in plant order (decision 6 of the brief: the bakery ones keep their ingredient
        /// ids). The overlay's product chips look their icon up from these (ICO_Crop_{Pascal}, else ICO_Ingredient_{Pascal}).
        /// </summary>
        public static readonly string[] Products =
        {
            "crop.clover_flower", "ingredient.flower_sugar", "ingredient.strawberry", "crop.daisy", "crop.round_carrot", "crop.poppy",
            "ingredient.blueberry", "crop.lavender", "ingredient.wheat_flour", "crop.indigo_flower", "crop.sunflower", "ingredient.apple",
            "crop.melon", "crop.sweet_pea_flower", "ingredient.hazelnut", "crop.water_bloom", "crop.gentian", "crop.lingonberry",
            "crop.banana", "crop.cactus_flower", "crop.cactus_fruit", "crop.cherry", "crop.glow_moss", "crop.cloud_puff", "crop.moonflower",
            "ingredient.gentle_honey", "crop.fern_fiddlehead", "crop.star_bloom", "crop.light_pearl",
        };

        /// <summary>The 25 dyes (doc 01 table 9.2), in plant order; their icon is ICO_Dye_{Pascal}.</summary>
        public static readonly string[] Dyes =
        {
            "dye.clover_green", "dye.strawberry_pink", "dye.daisy_cream", "dye.carrot_orange", "dye.poppy_red", "dye.blueberry_blue",
            "dye.lavender_lilac", "dye.wheat_gold", "dye.meadow_indigo", "dye.sun_yellow", "dye.apple_green", "dye.melon_peach",
            "dye.powder_pink", "dye.hazel_brown", "dye.water_bloom_pink", "dye.gentian_blue", "dye.lingonberry_garnet", "dye.tropical_green",
            "dye.cactus_coral", "dye.cherry_red", "dye.glow_turquoise", "dye.cloud_white", "dye.moon_silver", "dye.fern_green", "dye.nebula_blue",
        };

        /// <summary>Each dye's colour (doc 01 table 9.2, sRGB hex), the harvest effect's tint for its plant.</summary>
        public static readonly uint[] DyeColors =
        {
            0x7CC37A, 0xF28CA0, 0xF5EBC8, 0xF28E3C, 0xE2574C, 0x5A6FB8, 0xB79CD9, 0xE3B65A, 0x4B5BA6, 0xF4C542, 0xA7D46F, 0xF7B58B,
            0xF2C4CE, 0xA8754A, 0xE99AC4, 0x3F6FD1, 0x9E2F46, 0x2FA36B, 0xF47C6B, 0xC8243F, 0x3CC9C0, 0xFAFAF7, 0xC9D3E3, 0x5E8C3A, 0x5B4FC9,
        };

        /// <summary>The 25 single palettes then the 6 duo palettes (doc 01 tables 9.2 and 9.3).</summary>
        public static readonly string[] Palettes =
        {
            "palette.dye.clover", "palette.dye.strawberry", "palette.dye.daisy", "palette.dye.carrot", "palette.dye.poppy", "palette.dye.blueberry",
            "palette.dye.lavender", "palette.dye.wheat", "palette.dye.indigo", "palette.dye.sunflower", "palette.dye.apple", "palette.dye.melon",
            "palette.dye.sweet_pea", "palette.dye.hazel", "palette.dye.water_bloom", "palette.dye.gentian", "palette.dye.lingonberry",
            "palette.dye.tropical", "palette.dye.cactus", "palette.dye.cherry", "palette.dye.glow", "palette.dye.cloud", "palette.dye.moon",
            "palette.dye.fern", "palette.dye.nebula",
            "palette.dye.duo_meadow", "palette.dye.duo_sunset", "palette.dye.duo_lagoon", "palette.dye.duo_orchard", "palette.dye.duo_soft_night",
            "palette.dye.duo_harvest",
        };

        /// <summary>« sweet_clover » of « plant.sweet_clover » → « SweetClover » (sprite names). Allocates: once per bind.</summary>
        public static string Pascal(string id)
        {
            if (string.IsNullOrEmpty(id))
                return string.Empty;
            int dot = id.LastIndexOf('.');
            var builder = new StringBuilder(id.Length);
            bool upper = true;
            for (int i = dot + 1; i < id.Length; i++)
            {
                char c = id[i];
                if (c == '_')
                {
                    upper = true;
                    continue;
                }
                builder.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            return builder.ToString();
        }

        /// <summary>The seed packet's sprite of a plant (ICO_Seed_SweetClover).</summary>
        public static string SeedIcon(string plantKey) => "ICO_Seed_" + Pascal(plantKey);

        /// <summary>A product's sprite names, the second after « | » (ICO_Crop_Strawberry|ICO_Ingredient_Strawberry).</summary>
        public static string ProductIcon(string productId) =>
            productId != null && productId.StartsWith("ingredient.", System.StringComparison.Ordinal)
                ? "ICO_Crop_" + Pascal(productId) + "|ICO_Ingredient_" + Pascal(productId)
                : "ICO_Crop_" + Pascal(productId);

        public static string DyeIcon(string dyeId) => "ICO_Dye_" + Pascal(dyeId);
    }
}
