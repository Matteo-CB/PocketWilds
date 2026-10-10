#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>What a garden level opens (doc 01 Contenu, table 8.3), besides its plants and its decoration.</summary>
    [Flags]
    public enum GardenLevelOpens : ushort
    {
        None = 0,

        /// <summary>Level 2: the flower decorations (section 8.4, the craft tab of T56).</summary>
        FlowerDecorations = 1,

        /// <summary>Level 3: the Pâtisserie (S-ACT-009, T63) and its recipes.</summary>
        Bakery = 2,

        /// <summary>Level 4: dyes and dye palettes (section 9).</summary>
        Dyes = 4,

        /// <summary>Level 5: a seed back 75 % of the time.</summary>
        SeedBack75 = 8,

        /// <summary>Level 7: « Pousse heureuse », 10 % of harvests doubled.</summary>
        HappySprout = 16,

        /// <summary>Level 9: a seed back 90 % of the time.</summary>
        SeedBack90 = 32,

        /// <summary>Level 10: +1 seed on every 3-star harvest.</summary>
        ThreeStarSeed = 64,
    }

    /// <summary>One garden level (table 8.3): the cumulated points, the plants, the decoration and what else it opens.</summary>
    public readonly struct GardenLevelInfo
    {
        public readonly int Level;

        /// <summary>Garden points cumulated to reach it.</summary>
        public readonly int Points;

        public readonly GardenLevelOpens Opens;

        /// <summary>The garden decoration given at this level (decor.garden_sign …), ContentId.None when none.</summary>
        public readonly ContentId Decoration;

        /// <summary>The Pâtisserie recipes it opens (1 to 4, 5 to 8, 9 to 12), 0 when none.</summary>
        public readonly int RecipesFrom;

        public readonly int RecipesTo;

        public GardenLevelInfo(int level, int points, GardenLevelOpens opens, string? decoration, int recipesFrom, int recipesTo)
        {
            Level = level;
            Points = points;
            Opens = opens;
            Decoration = decoration != null ? ContentId.FromSource(decoration) : ContentId.None;
            RecipesFrom = recipesFrom;
            RecipesTo = recipesTo;
        }
    }

    /// <summary>One of the 25 dyes (section 9.2): its colour, its plant and its single palette's colours.</summary>
    public readonly struct GardenDye
    {
        public readonly ContentId Id;

        /// <summary>« dye.clover_green »; its name text is « {Key}.name ».</summary>
        public readonly string Key;

        /// <summary>The colour as 0xRRGGBB (#7CC37A).</summary>
        public readonly uint Rgb;

        public readonly ContentId Plant;

        /// <summary>Its single palette (palette.dye.clover) and that palette's key.</summary>
        public readonly ContentId Palette;

        public readonly string PaletteKey;

        /// <summary>The palette's secondary and accent colours, as dye indexes.</summary>
        public readonly int Secondary;

        public readonly int Accent;

        public GardenDye(string key, uint rgb, string plant, string palette, int secondary, int accent)
        {
            Key = key;
            Id = ContentId.FromSource(key);
            Rgb = rgb;
            Plant = ContentId.FromSource(plant);
            Palette = ContentId.FromSource(palette);
            PaletteKey = palette;
            Secondary = secondary;
            Accent = accent;
        }
    }

    /// <summary>A dye palette (sections 9.2 and 9.3): a single one costs 5 dyes of its colour, a duo 3 of each of its two.</summary>
    public readonly struct GardenPalette
    {
        public readonly ContentId Id;

        /// <summary>« palette.dye.clover »; its name text is « {Key}.name ».</summary>
        public readonly string Key;

        /// <summary>The dye indexes it costs (B is −1 for a single palette) and its accent colour.</summary>
        public readonly int DyeA;

        public readonly int DyeB;
        public readonly int Accent;

        /// <summary>Dyes of each colour it costs (5 single, 3 duo).</summary>
        public readonly int CostEach;

        public GardenPalette(string key, int dyeA, int dyeB, int accent, int costEach)
        {
            Key = key;
            Id = ContentId.FromSource(key);
            DyeA = dyeA;
            DyeB = dyeB;
            Accent = accent;
            CostEach = costEach;
        }

        public bool IsDuo => DyeB >= 0;
    }

    /// <summary>A flower decoration (section 8.4): assembled from garden flowers in the craft tab (T56), 5 copies at most.</summary>
    public readonly struct GardenFlowerDecoration
    {
        public readonly ContentId Id;
        public readonly string Key;
        public readonly ContentId Crop;
        public readonly int Cost;
        public readonly int MaxCopies;

        public GardenFlowerDecoration(string key, ContentId crop, int cost)
        {
            Key = key;
            Id = ContentId.FromSource(key);
            Crop = crop;
            Cost = cost;
            MaxCopies = 5;
        }
    }

    /// <summary>What a garden challenge counts (S-ACT-006 « Défis »).</summary>
    public enum GardenChallengeRule : byte
    {
        /// <summary>Every harvest counts.</summary>
        Any = 0,

        /// <summary>02: fruit harvests, each one beside a flower.</summary>
        FruitBesideFlower = 1,

        /// <summary>04 and 09: each harvest of a plant not counted yet in the run.</summary>
        DifferentPlants = 2,

        /// <summary>05: plants of 5 h or more.</summary>
        LongPlants = 3,

        /// <summary>06: every owned plot (3 at least), all in the same minute.</summary>
        AllPlotsSameMinute = 4,

        /// <summary>07: biome plants of different biomes.</summary>
        DifferentBiomes = 5,

        /// <summary>08: Golden Wheat, Wild Strawberry and Blueberry, each once.</summary>
        CakeBasket = 6,
    }

    /// <summary>One of the 10 garden challenges: what counts, the harvests, the stars thresholds, a time window.</summary>
    public readonly struct GardenChallenge
    {
        public readonly ContentId Id;
        public readonly GardenChallengeRule Rule;
        public readonly int Harvests;
        public readonly int Threshold1;
        public readonly int Threshold2;
        public readonly int Threshold3;

        /// <summary>The run must fit in this window (60 s for 06, 3 days for 09, 2 days for 10); 0: none.</summary>
        public readonly double WindowSeconds;

        /// <summary>Only with the dyes open (09 « Teintures ouvertes »).</summary>
        public readonly bool NeedsDyes;

        public GardenChallenge(int number, GardenChallengeRule rule, int harvests, int t1, int t2, int t3, double windowSeconds = 0.0,
            bool needsDyes = false)
        {
            Id = ContentId.FromSource("challenge.garden." + number.ToString("00", System.Globalization.CultureInfo.InvariantCulture));
            Rule = rule;
            Harvests = harvests;
            Threshold1 = t1;
            Threshold2 = t2;
            Threshold3 = t3;
            WindowSeconds = windowSeconds;
            NeedsDyes = needsDyes;
        }

        public int Threshold(int stars) => stars <= 1 ? Threshold1 : stars == 2 ? Threshold2 : Threshold3;

        /// <summary>The challenge stars of a run: 0 to 3 from the harvest stars added up.</summary>
        public byte StarsFor(int harvestStars) =>
            (byte)(harvestStars >= Threshold3 ? 3 : harvestStars >= Threshold2 ? 2 : harvestStars >= Threshold1 ? 1 : 0);
    }

    /// <summary>
    /// The garden's content (doc 01 Contenu: table 8.2 « Les 25 plantes », table 8.3 « Niveaux du Jardin », table 8.4
    /// « Décorations de fleurs », tables 9.2 and 9.3 « Teintures » and palettes, S-ACT-006 « Défis »). Static, built once;
    /// lookups never allocate. Products: the Pâtisserie ingredients keep their ingredient ids, the others are crop.*; dyes are
    /// dye.*, palettes palette.dye.*.
    /// </summary>
    public static class GardenCatalog
    {
        public const int PlantCount = 25;
        public const int StarterPlants = 12;
        public const int DyeCount = GardenState.DyeCount;
        public const int SinglePalettes = 25;
        public const int DuoPalettes = 6;
        public const int PaletteCount = SinglePalettes + DuoPalettes;
        public const int LevelCount = 10;
        public const int ChallengeCount = 10;
        public const int FlowerDecorationCount = 8;

        private const double Min = 60.0;
        private const double Hour = 3600.0;

        // ---- Products (section 8.2; the capitalised ones are Pâtisserie ingredients, section 11) ----
        public static readonly ContentId FlowerSugar = Id("ingredient.flower_sugar");
        public static readonly ContentId Strawberry = Id("ingredient.strawberry");
        public static readonly ContentId Blueberry = Id("ingredient.blueberry");
        public static readonly ContentId WheatFlour = Id("ingredient.wheat_flour");
        public static readonly ContentId Apple = Id("ingredient.apple");
        public static readonly ContentId Hazelnut = Id("ingredient.hazelnut");
        public static readonly ContentId GentleHoney = Id("ingredient.gentle_honey");
        public static readonly ContentId CloverFlower = Id("crop.clover_flower");
        public static readonly ContentId Daisy = Id("crop.daisy");
        public static readonly ContentId RoundCarrot = Id("crop.round_carrot");
        public static readonly ContentId Poppy = Id("crop.poppy");
        public static readonly ContentId Lavender = Id("crop.lavender");
        public static readonly ContentId IndigoFlower = Id("crop.indigo_flower");
        public static readonly ContentId Sunflower = Id("crop.sunflower");
        public static readonly ContentId Melon = Id("crop.melon");
        public static readonly ContentId SweetPeaFlower = Id("crop.sweet_pea_flower");
        public static readonly ContentId WaterBloom = Id("crop.water_bloom");
        public static readonly ContentId Gentian = Id("crop.gentian");
        public static readonly ContentId Lingonberry = Id("crop.lingonberry");
        public static readonly ContentId Banana = Id("crop.banana");
        public static readonly ContentId CactusFlower = Id("crop.cactus_flower");
        public static readonly ContentId CactusFruit = Id("crop.cactus_fruit");
        public static readonly ContentId Cherry = Id("crop.cherry");
        public static readonly ContentId GlowMoss = Id("crop.glow_moss");
        public static readonly ContentId CloudPuff = Id("crop.cloud_puff");
        public static readonly ContentId Moonflower = Id("crop.moonflower");
        public static readonly ContentId FernFiddlehead = Id("crop.fern_fiddlehead");
        public static readonly ContentId StarBloom = Id("crop.star_bloom");
        public static readonly ContentId LightPearl = Id("crop.light_pearl");

        public static readonly ContentId SweetClover = Id("plant.sweet_clover");
        public static readonly ContentId WildStrawberry = Id("plant.wild_strawberry");
        public static readonly ContentId BlueberryBush = Id("plant.blueberry");
        public static readonly ContentId GoldenWheat = Id("plant.golden_wheat");
        public static readonly ContentId MeadowIndigo = Id("plant.meadow_indigo");
        public static readonly ContentId SweetPea = Id("plant.sweet_pea");

        private static readonly ContentId None = ContentId.None;

        /// <summary>The 25 plants in doc order: the 12 starter plants (garden levels 1 to 7), then one per biome.</summary>
        public static readonly GardenPlant[] Plants =
        {
            P("plant.sweet_clover", GardenPlantKind.Flower, 1, null, 30 * Min, CloverFlower, 3, FlowerSugar, 1, "dye.clover_green", 1),
            P("plant.wild_strawberry", GardenPlantKind.Fruit, 1, null, 1 * Hour, Strawberry, 4, None, 0, "dye.strawberry_pink", 1),
            P("plant.daisy", GardenPlantKind.Flower, 1, null, 45 * Min, Daisy, 3, FlowerSugar, 1, "dye.daisy_cream", 1),
            P("plant.round_carrot", GardenPlantKind.Vegetable, 1, null, 90 * Min, RoundCarrot, 3, None, 0, "dye.carrot_orange", 1),
            P("plant.poppy", GardenPlantKind.Flower, 2, null, 1 * Hour, Poppy, 3, None, 0, "dye.poppy_red", 1),
            P("plant.blueberry", GardenPlantKind.Fruit, 2, null, 2 * Hour, Blueberry, 5, None, 0, "dye.blueberry_blue", 1),
            P("plant.lavender", GardenPlantKind.Flower, 3, null, 150 * Min, Lavender, 4, FlowerSugar, 1, "dye.lavender_lilac", 1),
            P("plant.golden_wheat", GardenPlantKind.Grain, 3, null, 3 * Hour, WheatFlour, 4, None, 0, "dye.wheat_gold", 1),
            P("plant.meadow_indigo", GardenPlantKind.Flower, 4, null, 4 * Hour, IndigoFlower, 2, None, 0, "dye.meadow_indigo", 2),
            P("plant.sunflower", GardenPlantKind.Flower, 5, null, 5 * Hour, Sunflower, 2, None, 0, "dye.sun_yellow", 1),
            P("plant.dwarf_apple", GardenPlantKind.Fruit, 6, null, 6 * Hour, Apple, 4, None, 0, "dye.apple_green", 1),
            P("plant.round_melon", GardenPlantKind.Fruit, 7, null, 8 * Hour, Melon, 2, None, 0, "dye.melon_peach", 1),
            P("plant.sweet_pea", GardenPlantKind.Flower, 0, "biome.grassland", 40 * Min, SweetPeaFlower, 3, FlowerSugar, 1, "dye.powder_pink", 1),
            P("plant.hazel", GardenPlantKind.Fruit, 0, "biome.forest", 4 * Hour, Hazelnut, 6, None, 0, "dye.hazel_brown", 1),
            P("plant.water_bloom", GardenPlantKind.Flower, 0, "biome.lake", 3 * Hour, WaterBloom, 2, None, 0, "dye.water_bloom_pink", 1),
            P("plant.blue_gentian", GardenPlantKind.Flower, 0, "biome.mountain", 2 * Hour, Gentian, 3, None, 0, "dye.gentian_blue", 1),
            P("plant.snow_lingonberry", GardenPlantKind.Fruit, 0, "biome.snow", 5 * Hour, Lingonberry, 5, None, 0, "dye.lingonberry_garnet", 1),
            P("plant.dwarf_banana", GardenPlantKind.Fruit, 0, "biome.jungle", 7 * Hour, Banana, 3, None, 0, "dye.tropical_green", 1),
            P("plant.flowering_cactus", GardenPlantKind.Flower, 0, "biome.desert", 8 * Hour, CactusFlower, 2, CactusFruit, 2, "dye.cactus_coral", 1),
            P("plant.dwarf_cherry", GardenPlantKind.Fruit, 0, "biome.sakura", 6 * Hour, Cherry, 6, None, 0, "dye.cherry_red", 1),
            P("plant.glow_moss", GardenPlantKind.Vegetable, 0, "biome.mushroom", 150 * Min, GlowMoss, 3, None, 0, "dye.glow_turquoise", 1),
            P("plant.cloud_puff", GardenPlantKind.Flower, 0, "biome.cloudlands", 4 * Hour, CloudPuff, 3, None, 0, "dye.cloud_white", 1),
            P("plant.moonflower", GardenPlantKind.Flower, 0, "biome.lunar", 8 * Hour, Moonflower, 2, GentleHoney, 1, "dye.moon_silver", 1),
            P("plant.giant_fern", GardenPlantKind.Vegetable, 0, "biome.prehistoric", 5 * Hour, FernFiddlehead, 3, None, 0, "dye.fern_green", 1),
            P("plant.star_bloom", GardenPlantKind.Flower, 0, "biome.starlight", 8 * Hour, StarBloom, 1, LightPearl, 3, "dye.nebula_blue", 1),
        };

        /// <summary>
        /// The plants built for this build (decision of T43: the 12 starter plants and the Prairie's Sweet Pea, 3 stages each);
        /// the 12 other biome plants are in the catalogue but stay closed until their task builds their models.
        /// </summary>
        public static readonly bool[] Built = BuildBuilt();

        // Dye indexes, doc order.
        private const int CloverGreen = 0, StrawberryPink = 1, DaisyCream = 2, CarrotOrange = 3, PoppyRed = 4, BlueberryBlue = 5,
            LavenderLilac = 6, WheatGold = 7, MeadowIndigoDye = 8, SunYellow = 9, AppleGreen = 10, MelonPeach = 11, PowderPink = 12,
            HazelBrown = 13, WaterBloomPink = 14, GentianBlue = 15, LingonberryGarnet = 16, TropicalGreen = 17, CactusCoral = 18,
            CherryRed = 19, GlowTurquoise = 20, CloudWhite = 21, MoonSilver = 22, FernGreen = 23, NebulaBlue = 24;

        /// <summary>The 25 dyes of table 9.2, in doc order (index i is the dye of plant i).</summary>
        public static readonly GardenDye[] Dyes =
        {
            new GardenDye("dye.clover_green", 0x7CC37A, "plant.sweet_clover", "palette.dye.clover", DaisyCream, HazelBrown),
            new GardenDye("dye.strawberry_pink", 0xF28CA0, "plant.wild_strawberry", "palette.dye.strawberry", DaisyCream, CloverGreen),
            new GardenDye("dye.daisy_cream", 0xF5EBC8, "plant.daisy", "palette.dye.daisy", SunYellow, AppleGreen),
            new GardenDye("dye.carrot_orange", 0xF28E3C, "plant.round_carrot", "palette.dye.carrot", FernGreen, DaisyCream),
            new GardenDye("dye.poppy_red", 0xE2574C, "plant.poppy", "palette.dye.poppy", DaisyCream, TropicalGreen),
            new GardenDye("dye.blueberry_blue", 0x5A6FB8, "plant.blueberry", "palette.dye.blueberry", LavenderLilac, CloudWhite),
            new GardenDye("dye.lavender_lilac", 0xB79CD9, "plant.lavender", "palette.dye.lavender", CloudWhite, MoonSilver),
            new GardenDye("dye.wheat_gold", 0xE3B65A, "plant.golden_wheat", "palette.dye.wheat", HazelBrown, DaisyCream),
            new GardenDye("dye.meadow_indigo", 0x4B5BA6, "plant.meadow_indigo", "palette.dye.indigo", CloudWhite, SunYellow),
            new GardenDye("dye.sun_yellow", 0xF4C542, "plant.sunflower", "palette.dye.sunflower", HazelBrown, CloverGreen),
            new GardenDye("dye.apple_green", 0xA7D46F, "plant.dwarf_apple", "palette.dye.apple", CherryRed, DaisyCream),
            new GardenDye("dye.melon_peach", 0xF7B58B, "plant.round_melon", "palette.dye.melon", AppleGreen, CloudWhite),
            new GardenDye("dye.powder_pink", 0xF2C4CE, "plant.sweet_pea", "palette.dye.sweet_pea", LavenderLilac, CloudWhite),
            new GardenDye("dye.hazel_brown", 0xA8754A, "plant.hazel", "palette.dye.hazel", WheatGold, DaisyCream),
            new GardenDye("dye.water_bloom_pink", 0xE99AC4, "plant.water_bloom", "palette.dye.water_bloom", GlowTurquoise, CloudWhite),
            new GardenDye("dye.gentian_blue", 0x3F6FD1, "plant.blue_gentian", "palette.dye.gentian", CloudWhite, SunYellow),
            new GardenDye("dye.lingonberry_garnet", 0x9E2F46, "plant.snow_lingonberry", "palette.dye.lingonberry", CloudWhite, FernGreen),
            new GardenDye("dye.tropical_green", 0x2FA36B, "plant.dwarf_banana", "palette.dye.tropical", SunYellow, CactusCoral),
            new GardenDye("dye.cactus_coral", 0xF47C6B, "plant.flowering_cactus", "palette.dye.cactus", GlowTurquoise, WheatGold),
            new GardenDye("dye.cherry_red", 0xC8243F, "plant.dwarf_cherry", "palette.dye.cherry", PowderPink, CloverGreen),
            new GardenDye("dye.glow_turquoise", 0x3CC9C0, "plant.glow_moss", "palette.dye.glow", NebulaBlue, WaterBloomPink),
            new GardenDye("dye.cloud_white", 0xFAFAF7, "plant.cloud_puff", "palette.dye.cloud", GentianBlue, WheatGold),
            new GardenDye("dye.moon_silver", 0xC9D3E3, "plant.moonflower", "palette.dye.moon", MeadowIndigoDye, CloudWhite),
            new GardenDye("dye.fern_green", 0x5E8C3A, "plant.giant_fern", "palette.dye.fern", HazelBrown, CarrotOrange),
            new GardenDye("dye.nebula_blue", 0x5B4FC9, "plant.star_bloom", "palette.dye.nebula", PowderPink, MoonSilver),
        };

        /// <summary>The 25 single palettes (5 dyes of their colour) then the 6 duo palettes of table 9.3 (3 + 3).</summary>
        public static readonly GardenPalette[] Palettes = BuildPalettes();

        /// <summary>Table 8.3.</summary>
        public static readonly GardenLevelInfo[] Levels =
        {
            new GardenLevelInfo(1, 0, GardenLevelOpens.None, null, 0, 0),
            new GardenLevelInfo(2, 30, GardenLevelOpens.FlowerDecorations, "decor.garden_sign", 0, 0),
            new GardenLevelInfo(3, 90, GardenLevelOpens.Bakery, null, 1, 4),
            new GardenLevelInfo(4, 200, GardenLevelOpens.Dyes, null, 0, 0),
            new GardenLevelInfo(5, 380, GardenLevelOpens.SeedBack75, null, 5, 8),
            new GardenLevelInfo(6, 650, GardenLevelOpens.None, "decor.garden_arch", 0, 0),
            new GardenLevelInfo(7, 1000, GardenLevelOpens.HappySprout, null, 0, 0),
            new GardenLevelInfo(8, 1500, GardenLevelOpens.None, "decor.garden_cloche", 9, 12),
            new GardenLevelInfo(9, 2200, GardenLevelOpens.SeedBack90, null, 0, 0),
            new GardenLevelInfo(10, 3000, GardenLevelOpens.ThreeStarSeed, "decor.big_garden_sunflower", 0, 0),
        };

        /// <summary>Table 8.4 (data only: the craft tab is T56).</summary>
        public static readonly GardenFlowerDecoration[] FlowerDecorations =
        {
            new GardenFlowerDecoration("decor.clover_carpet", CloverFlower, 12),
            new GardenFlowerDecoration("decor.daisy_pot", Daisy, 10),
            new GardenFlowerDecoration("decor.poppy_bed", Poppy, 10),
            new GardenFlowerDecoration("decor.lavender_box", Lavender, 10),
            new GardenFlowerDecoration("decor.sunflower_row", Sunflower, 6),
            new GardenFlowerDecoration("decor.sweet_pea_arch", SweetPeaFlower, 12),
            new GardenFlowerDecoration("decor.water_bloom_bowl", WaterBloom, 6),
            new GardenFlowerDecoration("decor.gentian_rockery", Gentian, 8),
        };

        /// <summary>S-ACT-006 « Défis »: thresholds in harvest stars added up.</summary>
        public static readonly GardenChallenge[] Challenges =
        {
            new GardenChallenge(1, GardenChallengeRule.Any, 2, 2, 4, 6),
            new GardenChallenge(2, GardenChallengeRule.FruitBesideFlower, 3, 3, 6, 9),
            new GardenChallenge(3, GardenChallengeRule.Any, 3, 3, 6, 9),
            new GardenChallenge(4, GardenChallengeRule.DifferentPlants, 3, 3, 6, 9),
            new GardenChallenge(5, GardenChallengeRule.LongPlants, 2, 2, 4, 6),
            new GardenChallenge(6, GardenChallengeRule.AllPlotsSameMinute, 3, 3, 6, 9, 60.0),
            new GardenChallenge(7, GardenChallengeRule.DifferentBiomes, 2, 2, 4, 6),
            new GardenChallenge(8, GardenChallengeRule.CakeBasket, 3, 3, 6, 9),
            new GardenChallenge(9, GardenChallengeRule.DifferentPlants, 5, 5, 10, 15, 3 * 24 * Hour, needsDyes: true),
            new GardenChallenge(10, GardenChallengeRule.Any, 6, 6, 12, 18, 2 * 24 * Hour),
        };

        /// <summary>« Récolte longue »: plants of 5 h or more.</summary>
        public const double LongPlantSeconds = 5 * Hour;

        /// <summary>Index of a plant in <see cref="Plants"/> (by plant id), −1 when unknown. No allocation.</summary>
        public static int IndexOf(ContentId plant)
        {
            if (!plant.IsValid)
                return -1;
            for (int i = 0; i < Plants.Length; i++)
                if (Plants[i].Id == plant)
                    return i;
            return -1;
        }

        /// <summary>The plant whose main product is <paramref name="crop"/> (WISH-034/035), −1 when none.</summary>
        public static int IndexOfMain(ContentId crop)
        {
            if (!crop.IsValid)
                return -1;
            for (int i = 0; i < Plants.Length; i++)
                if (Plants[i].Main == crop)
                    return i;
            return -1;
        }

        public static int DyeIndexOf(ContentId dye)
        {
            if (!dye.IsValid)
                return -1;
            for (int i = 0; i < Dyes.Length; i++)
                if (Dyes[i].Id == dye)
                    return i;
            return -1;
        }

        public static int PaletteIndexOf(ContentId palette)
        {
            if (!palette.IsValid)
                return -1;
            for (int i = 0; i < Palettes.Length; i++)
                if (Palettes[i].Id == palette)
                    return i;
            return -1;
        }

        /// <summary>The cake basket's plants (challenge 08), as plant indexes.</summary>
        public static bool InCakeBasket(ContentId plant) => plant == GoldenWheat || plant == WildStrawberry || plant == BlueberryBush;

        private static GardenPlant P(string key, GardenPlantKind kind, int level, string? biome, double seconds, ContentId main, int mainCount,
            ContentId side, int sideCount, string dye, int dyeCount) =>
            new GardenPlant(ContentId.FromSource(key), key, kind, level, biome != null ? ContentId.FromSource(biome) : ContentId.None, seconds,
                main, mainCount, side, sideCount, ContentId.FromSource(dye), dyeCount);

        private static ContentId Id(string source) => ContentId.FromSource(source);

        /// <summary>Every product's runtime id (its name text is « {id}.name »): the wishes' {crop}.</summary>
        public static readonly string[] ProductKeys =
        {
            "ingredient.flower_sugar", "ingredient.strawberry", "ingredient.blueberry", "ingredient.wheat_flour", "ingredient.apple",
            "ingredient.hazelnut", "ingredient.gentle_honey", "crop.clover_flower", "crop.daisy", "crop.round_carrot", "crop.poppy",
            "crop.lavender", "crop.indigo_flower", "crop.sunflower", "crop.melon", "crop.sweet_pea_flower", "crop.water_bloom",
            "crop.gentian", "crop.lingonberry", "crop.banana", "crop.cactus_flower", "crop.cactus_fruit", "crop.cherry",
            "crop.glow_moss", "crop.cloud_puff", "crop.moonflower", "crop.fern_fiddlehead", "crop.star_bloom", "crop.light_pearl",
        };

        private static readonly ContentId[] ProductIds = BuildProductIds();

        private static ContentId[] BuildProductIds()
        {
            var ids = new ContentId[ProductKeys.Length];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = Id(ProductKeys[i]);
            return ids;
        }

        /// <summary>The runtime id of a product (a crop or a garden ingredient); false for anything else.</summary>
        public static bool TryGetProductKey(ContentId product, out string key)
        {
            for (int i = 0; i < ProductIds.Length; i++)
            {
                if (ProductIds[i] == product)
                {
                    key = ProductKeys[i];
                    return true;
                }
            }
            key = string.Empty;
            return false;
        }

        private static bool[] BuildBuilt()
        {
            var built = new bool[PlantCount];
            for (int i = 0; i < StarterPlants; i++)
                built[i] = true;
            built[StarterPlants] = true; // the Prairie's Sweet Pea
            return built;
        }

        private static GardenPalette[] BuildPalettes()
        {
            var palettes = new GardenPalette[PaletteCount];
            for (int i = 0; i < SinglePalettes; i++)
            {
                var dye = Dyes[i];
                palettes[i] = new GardenPalette(dye.PaletteKey, i, -1, dye.Accent, 5);
            }
            palettes[SinglePalettes + 0] = new GardenPalette("palette.dye.duo_meadow", StrawberryPink, CloverGreen, DaisyCream, 3);
            palettes[SinglePalettes + 1] = new GardenPalette("palette.dye.duo_sunset", CarrotOrange, LavenderLilac, SunYellow, 3);
            palettes[SinglePalettes + 2] = new GardenPalette("palette.dye.duo_lagoon", GlowTurquoise, GentianBlue, CloudWhite, 3);
            palettes[SinglePalettes + 3] = new GardenPalette("palette.dye.duo_orchard", AppleGreen, CherryRed, DaisyCream, 3);
            palettes[SinglePalettes + 4] = new GardenPalette("palette.dye.duo_soft_night", MoonSilver, NebulaBlue, PowderPink, 3);
            palettes[SinglePalettes + 5] = new GardenPalette("palette.dye.duo_harvest", WheatGold, HazelBrown, PoppyRed, 3);
            return palettes;
        }
    }
}
