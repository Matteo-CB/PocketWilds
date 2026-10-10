using System.Collections.Generic;
using PocketWilds.Presentation;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T43 « Jardin » shipped texts (doc 01 Contenu table 8.6, the 25 plants of table 8.2, their products, the 25 dyes and 31
    /// palettes of section 9, the 10 challenges of S-ACT-006; doc 05 sheet 24 « Tout récolter »; Writing Guide: short, warm,
    /// no em dash, no pressure words, real time never a countdown). Columns: key, en, fr. Merged into LOC_en / LOC_fr by
    /// <see cref="UiPipeline.EnsureLocalization"/>. Keys are <see cref="GardenText"/>'s; a plant's name is « {Key}.name »,
    /// a product's, a dye's and a palette's « {id}.name »; a product also has a counted « {id}.count » (« {n} fraises ») for
    /// the wishes' {crop}; a challenge has « challenge.garden.nn.name » and « .goal ».
    /// <para>Counted texts come as three rows: « key.one », « key.other » and the key itself (the usual plural). French uses a
    /// no-break space before « : » and between a number and its unit (« 5 h »).</para>
    /// <para>Long labels, measured against the overlay's boxes at the reference 1080 × 2340 by GardenScreenPipeline (every text
    /// below, both languages): a bed's « Prêt dans 7 h 59 » (36, box 350), « Nouvelle parcelle » (36, box 380), a packet's
    /// name on two lines (36, box 236), a challenge's goal on two lines (36, box 380).</para>
    /// <para>The table is built on first use (T42 met a static-initialisation-order null when one static table read another).</para>
    /// </summary>
    public static class GardenTexts
    {
        public static string[,] Texts => s_texts ??= Build();

        private static string[,] s_texts;

        private const string Nbsp = " ";

        /// <summary>The 25 plants (doc 01 table 8.2): runtime id, English, French.</summary>
        public static string[,] Plants => s_plants ??= new[,]
        {
            { "plant.sweet_clover", "Sweet Clover", "Trèfle doux" },
            { "plant.wild_strawberry", "Wild Strawberry", "Fraise des bois" },
            { "plant.daisy", "Daisy", "Marguerite" },
            { "plant.round_carrot", "Round Carrot", "Carotte ronde" },
            { "plant.poppy", "Poppy", "Coquelicot" },
            { "plant.blueberry", "Blueberry Bush", "Myrtillier" },
            { "plant.lavender", "Lavender", "Lavande" },
            { "plant.golden_wheat", "Golden Wheat", "Blé doré" },
            { "plant.meadow_indigo", "Meadow Indigo", "Indigo des prés" },
            { "plant.sunflower", "Sunflower", "Tournesol" },
            { "plant.dwarf_apple", "Dwarf Apple Tree", "Pommier nain" },
            { "plant.round_melon", "Round Melon", "Melon rond" },
            { "plant.sweet_pea", "Sweet Pea", "Pois de senteur" },
            { "plant.hazel", "Hazel Bush", "Noisetier" },
            { "plant.water_bloom", "Water Bloom", "Fleur d'eau" },
            { "plant.blue_gentian", "Blue Gentian", "Gentiane bleue" },
            { "plant.snow_lingonberry", "Snow Lingonberry", "Airelle des neiges" },
            { "plant.dwarf_banana", "Dwarf Banana", "Bananier nain" },
            { "plant.flowering_cactus", "Flowering Cactus", "Cactus à fleurs" },
            { "plant.dwarf_cherry", "Dwarf Cherry", "Cerisier nain" },
            { "plant.glow_moss", "Glow Moss", "Mousse lumineuse" },
            { "plant.cloud_puff", "Cloud Puff", "Fleur-nuage" },
            { "plant.moonflower", "Moonflower", "Fleur de lune" },
            { "plant.giant_fern", "Giant Fern", "Fougère géante" },
            { "plant.star_bloom", "Star Bloom", "Fleur-étoile" },
        };

        private static string[,] s_plants;

        /// <summary>
        /// Every product (doc 01 table 8.2): id, English name, French name, then the counted forms for the wishes (« {n}
        /// strawberry », « {n} strawberries », « {n} fraise », « {n} fraises »).
        /// </summary>
        public static string[,] Products => s_products ??= new[,]
        {
            { "crop.clover_flower", "Clover Flower", "Fleur de trèfle", "{n} clover flower", "{n} clover flowers", "{n} fleur de trèfle", "{n} fleurs de trèfle" },
            { "ingredient.flower_sugar", "Flower Sugar", "Sucre de fleurs", "{n} Flower Sugar", "{n} Flower Sugar", "{n} Sucre de fleurs", "{n} Sucres de fleurs" },
            { "ingredient.strawberry", "Strawberry", "Fraise", "{n} strawberry", "{n} strawberries", "{n} fraise", "{n} fraises" },
            { "crop.daisy", "Daisy", "Marguerite", "{n} daisy", "{n} daisies", "{n} marguerite", "{n} marguerites" },
            { "crop.round_carrot", "Round Carrot", "Carotte ronde", "{n} round carrot", "{n} round carrots", "{n} carotte ronde", "{n} carottes rondes" },
            { "crop.poppy", "Poppy", "Coquelicot", "{n} poppy", "{n} poppies", "{n} coquelicot", "{n} coquelicots" },
            { "ingredient.blueberry", "Blueberry", "Myrtille", "{n} blueberry", "{n} blueberries", "{n} myrtille", "{n} myrtilles" },
            { "crop.lavender", "Lavender Sprig", "Brin de lavande", "{n} lavender sprig", "{n} lavender sprigs", "{n} brin de lavande", "{n} brins de lavande" },
            { "ingredient.wheat_flour", "Wheat Flour", "Farine de blé", "{n} Wheat Flour", "{n} Wheat Flour", "{n} Farine de blé", "{n} Farines de blé" },
            { "crop.indigo_flower", "Indigo Flower", "Fleur d'indigo", "{n} indigo flower", "{n} indigo flowers", "{n} fleur d'indigo", "{n} fleurs d'indigo" },
            { "crop.sunflower", "Sunflower", "Fleur de tournesol", "{n} sunflower", "{n} sunflowers", "{n} fleur de tournesol", "{n} fleurs de tournesol" },
            { "ingredient.apple", "Apple", "Pomme", "{n} apple", "{n} apples", "{n} pomme", "{n} pommes" },
            { "crop.melon", "Melon", "Melon", "{n} melon", "{n} melons", "{n} melon", "{n} melons" },
            { "crop.sweet_pea_flower", "Sweet Pea Flower", "Fleur de pois de senteur", "{n} sweet pea flower", "{n} sweet pea flowers", "{n} fleur de pois de senteur",
                "{n} fleurs de pois de senteur" },
            { "ingredient.hazelnut", "Hazelnut", "Noisette", "{n} hazelnut", "{n} hazelnuts", "{n} noisette", "{n} noisettes" },
            { "crop.water_bloom", "Water Bloom", "Fleur d'eau", "{n} water bloom", "{n} water blooms", "{n} fleur d'eau", "{n} fleurs d'eau" },
            { "crop.gentian", "Gentian", "Gentiane", "{n} gentian", "{n} gentians", "{n} gentiane", "{n} gentianes" },
            { "crop.lingonberry", "Lingonberry", "Airelle", "{n} lingonberry", "{n} lingonberries", "{n} airelle", "{n} airelles" },
            { "crop.banana", "Banana", "Banane", "{n} banana", "{n} bananas", "{n} banane", "{n} bananes" },
            { "crop.cactus_flower", "Cactus Flower", "Fleur de cactus", "{n} cactus flower", "{n} cactus flowers", "{n} fleur de cactus", "{n} fleurs de cactus" },
            { "crop.cactus_fruit", "Cactus Fruit", "Fruit de cactus", "{n} cactus fruit", "{n} cactus fruits", "{n} fruit de cactus", "{n} fruits de cactus" },
            { "crop.cherry", "Cherry", "Cerise", "{n} cherry", "{n} cherries", "{n} cerise", "{n} cerises" },
            { "crop.glow_moss", "Glow Moss Tuft", "Touffe de mousse lumineuse", "{n} glow moss tuft", "{n} glow moss tufts", "{n} touffe de mousse lumineuse",
                "{n} touffes de mousse lumineuse" },
            { "crop.cloud_puff", "Cloud Puff", "Houppe de nuage", "{n} cloud puff", "{n} cloud puffs", "{n} houppe de nuage", "{n} houppes de nuage" },
            { "crop.moonflower", "Moonflower", "Fleur de lune", "{n} moonflower", "{n} moonflowers", "{n} fleur de lune", "{n} fleurs de lune" },
            { "ingredient.gentle_honey", "Gentle Honey", "Miel doux", "{n} Gentle Honey", "{n} Gentle Honey", "{n} Miel doux", "{n} Miels doux" },
            { "crop.fern_fiddlehead", "Fern Fiddlehead", "Crosse de fougère", "{n} fern fiddlehead", "{n} fern fiddleheads", "{n} crosse de fougère",
                "{n} crosses de fougère" },
            { "crop.star_bloom", "Star Bloom", "Fleur-étoile", "{n} star bloom", "{n} star blooms", "{n} fleur-étoile", "{n} fleurs-étoiles" },
            { "crop.light_pearl", "Light Pearl", "Perle de lumière", "{n} light pearl", "{n} light pearls", "{n} perle de lumière", "{n} perles de lumière" },
        };

        private static string[,] s_products;

        /// <summary>The 25 dyes (doc 01 table 9.2): id, English, French.</summary>
        public static string[,] Dyes => s_dyes ??= new[,]
        {
            { "dye.clover_green", "Clover Green", "Vert trèfle" },
            { "dye.strawberry_pink", "Strawberry Pink", "Rose fraise" },
            { "dye.daisy_cream", "Daisy Cream", "Crème marguerite" },
            { "dye.carrot_orange", "Carrot Orange", "Orange carotte" },
            { "dye.poppy_red", "Poppy Red", "Rouge coquelicot" },
            { "dye.blueberry_blue", "Blueberry Blue", "Bleu myrtille" },
            { "dye.lavender_lilac", "Lavender Lilac", "Lilas lavande" },
            { "dye.wheat_gold", "Wheat Gold", "Or des blés" },
            { "dye.meadow_indigo", "Meadow Indigo", "Indigo des prés" },
            { "dye.sun_yellow", "Sun Yellow", "Jaune soleil" },
            { "dye.apple_green", "Apple Green", "Vert pomme" },
            { "dye.melon_peach", "Melon Peach", "Pêche melon" },
            { "dye.powder_pink", "Powder Pink", "Rose poudré" },
            { "dye.hazel_brown", "Hazel Brown", "Brun noisette" },
            { "dye.water_bloom_pink", "Water Bloom Pink", "Rose d'eau" },
            { "dye.gentian_blue", "Gentian Blue", "Bleu gentiane" },
            { "dye.lingonberry_garnet", "Lingonberry Garnet", "Grenat airelle" },
            { "dye.tropical_green", "Tropical Green", "Vert tropical" },
            { "dye.cactus_coral", "Cactus Coral", "Corail cactus" },
            { "dye.cherry_red", "Cherry Red", "Rouge cerise" },
            { "dye.glow_turquoise", "Glow Turquoise", "Turquoise lueur" },
            { "dye.cloud_white", "Cloud White", "Blanc nuage" },
            { "dye.moon_silver", "Moon Silver", "Argent lune" },
            { "dye.fern_green", "Fern Green", "Vert fougère" },
            { "dye.nebula_blue", "Nebula Blue", "Bleu nébuleuse" },
        };

        private static string[,] s_dyes;

        /// <summary>The 25 palettes and the 6 duo palettes (doc 01 tables 9.2 and 9.3): id, English, French.</summary>
        public static string[,] Palettes => s_palettes ??= new[,]
        {
            { "palette.dye.clover", "Clover Palette", "Palette Trèfle" },
            { "palette.dye.strawberry", "Strawberry Palette", "Palette Fraise" },
            { "palette.dye.daisy", "Daisy Palette", "Palette Marguerite" },
            { "palette.dye.carrot", "Carrot Palette", "Palette Carotte" },
            { "palette.dye.poppy", "Poppy Palette", "Palette Coquelicot" },
            { "palette.dye.blueberry", "Blueberry Palette", "Palette Myrtille" },
            { "palette.dye.lavender", "Lavender Palette", "Palette Lavande" },
            { "palette.dye.wheat", "Wheat Palette", "Palette Blés" },
            { "palette.dye.indigo", "Indigo Palette", "Palette Indigo" },
            { "palette.dye.sunflower", "Sunflower Palette", "Palette Tournesol" },
            { "palette.dye.apple", "Apple Palette", "Palette Pomme" },
            { "palette.dye.melon", "Melon Palette", "Palette Melon" },
            { "palette.dye.sweet_pea", "Sweet Pea Palette", "Palette Pois de senteur" },
            { "palette.dye.hazel", "Hazel Palette", "Palette Noisette" },
            { "palette.dye.water_bloom", "Water Bloom Palette", "Palette Fleur d'eau" },
            { "palette.dye.gentian", "Gentian Palette", "Palette Gentiane" },
            { "palette.dye.lingonberry", "Lingonberry Palette", "Palette Airelle" },
            { "palette.dye.tropical", "Jungle Palette", "Palette Jungle" },
            { "palette.dye.cactus", "Oasis Palette", "Palette Oasis" },
            { "palette.dye.cherry", "Cherry Palette", "Palette Cerise" },
            { "palette.dye.glow", "Glow Palette", "Palette Lueur" },
            { "palette.dye.cloud", "Cloud Palette", "Palette Nuage" },
            { "palette.dye.moon", "Moon Palette", "Palette Lune" },
            { "palette.dye.fern", "Fern Palette", "Palette Fougère" },
            { "palette.dye.nebula", "Nebula Palette", "Palette Nébuleuse" },
            { "palette.dye.duo_meadow", "Meadow in Bloom", "Prairie en fleurs" },
            { "palette.dye.duo_sunset", "Sunset", "Coucher de soleil" },
            { "palette.dye.duo_lagoon", "Lagoon", "Lagon" },
            { "palette.dye.duo_orchard", "Orchard", "Verger" },
            { "palette.dye.duo_soft_night", "Soft Night", "Nuit douce" },
            { "palette.dye.duo_harvest", "Harvest", "Moisson" },
        };

        private static string[,] s_palettes;

        /// <summary>The 10 challenges (doc 01 S-ACT-006): the doc's French names, natural English ones, a short goal each.</summary>
        public static string[,] Challenges => s_challenges ??= new[,]
        {
            { "First Basket", "Premier panier", "2 harvests", "2" + Nbsp + "récoltes" },
            { "Flower Friends", "Voisins fleuris", "3 fruits, each next to a flower", "3" + Nbsp + "fruits, chacun près d'une fleur" },
            { "Perfect Watering", "Arrosage parfait", "3 harvests, well watered", "3" + Nbsp + "récoltes bien arrosées" },
            { "Rainbow", "Arc-en-ciel", "3 harvests of 3 different plants", "3" + Nbsp + "plantes différentes" },
            { "Long Harvest", "Récolte longue", "2 plants of 5 h or more", "2" + Nbsp + "plantes de 5" + Nbsp + "h ou plus" },
            { "Full Garden", "Plein jardin", "Every plot within one minute", "Toutes les parcelles en 1" + Nbsp + "minute" },
            { "World Flowers", "Fleurs du monde", "2 plants from different biomes", "2" + Nbsp + "plantes de biomes différents" },
            { "Cake Basket", "Panier à gâteaux", "Wheat, strawberry and blueberry", "Blé, fraise et myrtille" },
            { "Five Colours", "Cinq couleurs", "5 plants in 3 days", "5" + Nbsp + "plantes en 3" + Nbsp + "jours" },
            { "Golden Garden", "Jardin d'or", "6 harvests in 2 days", "6" + Nbsp + "récoltes en 2" + Nbsp + "jours" },
        };

        private static string[,] s_challenges;

        private static string[,] Build()
        {
            var rows = new List<string[]>(260);

            void Plain(string key, string en, string fr) => rows.Add(new[] { key, en, fr });

            void Counted(string key, string enOne, string enOther, string frOne, string frOther)
            {
                rows.Add(new[] { key, enOther, frOther });
                rows.Add(new[] { key + ".one", enOne, frOne });
                rows.Add(new[] { key + ".other", enOther, frOther });
            }

            // Doc 01, table 8.6.
            Plain(GardenText.Title, "Garden", "Jardin");
            Plain(GardenText.Plant, "Plant", "Planter");
            Plain(GardenText.Water, "Water", "Arroser");
            Plain(GardenText.Harvest, "Harvest", "Récolter");
            Plain(GardenText.ReadyIn, "Ready in {time}", "Prêt dans {time}");
            Plain(GardenText.Ready, "Ready to harvest", "Prêt à récolter");
            Plain(GardenText.Watered, "Watered", "Arrosé");
            Plain(GardenText.Helped, "Good neighbors", "Les voisines s'entraident");
            Plain(GardenText.LevelUp, "Garden level {n}", "Jardin niveau {n}");
            Plain(GardenText.SpeedUp, "Finish now", "Finir maintenant");
            Plain(GardenText.NewPlot, "New plot", "Nouvelle parcelle");

            // The overlay's own words (doc 05 sheet 24: « Tout récolter » / "Harvest all").
            Plain(GardenText.HarvestAll, "Harvest all", "Tout récolter");
            Plain(GardenText.Seeds, "Seeds", "Graines");
            Plain(GardenText.NoSeeds, "No seeds of this one yet", "Pas encore de graines de celle-ci");
            Plain(GardenText.PlotClub, "With the Wild Club", "Avec le Wild Club");
            Plain(GardenText.PlotConfirm, "Tap again to open it", "Touche encore pour l'ouvrir");
            Plain(GardenText.NeedGems, "Not enough Gems yet", "Pas encore assez de Gemmes");
            Plain(GardenText.ClubSoon, "The Wild Club is coming soon", "Le Wild Club arrive bientôt");
            Counted(GardenText.SpeedUpsLeft, "{n} left today", "{n} left today", "Encore {n} aujourd'hui", "Encore {n} aujourd'hui");
            Plain(GardenText.SpeedUpsNone, "More tomorrow", "D'autres demain");
            Plain(GardenText.Challenges, "Challenges", "Défis");
            Plain(GardenText.ChallengeProgress, "{n} of {total}", "{n} sur {total}");
            Plain(GardenText.CanHint, "Tap a sprout", "Touche une pousse");
            Plain(GardenText.AlreadyWatered, "Every sprout is watered", "Toutes les pousses sont arrosées");
            Counted(GardenText.ReadyCount, "{n} ready", "{n} ready", "{n} prêt", "{n} prêts");
            Plain(GardenText.Opened, "New: {list}", "Nouveau" + Nbsp + ": {list}");
            Plain(GardenText.OpenedDyes, "Dyes", "Teintures");
            Plain(GardenText.PlantBiome, "Opens with its biome", "S'ouvre avec son biome");
            Plain(GardenText.Happy, "Happy sprout: double harvest", "Pousse heureuse" + Nbsp + ": récolte double");
            Plain(GardenText.ChallengeLocked, "Not open yet", "Pas encore ouvert");
            Plain(GardenText.ChallengeStarted, "Challenge on: your next harvests count", "Défi lancé" + Nbsp + ": tes prochaines récoltes comptent");
            Plain(GardenText.ChallengeStopped, "Challenge stopped", "Défi arrêté");

            // The 25 plants (doc 01 table 8.2).
            var plants = Plants;
            for (int i = 0; i < plants.GetLength(0); i++)
                Plain(GardenText.PlantName(plants[i, 0]), plants[i, 1], plants[i, 2]);

            // The products, with their counted forms for the wishes' {crop}.
            var products = Products;
            for (int i = 0; i < products.GetLength(0); i++)
            {
                string id = products[i, 0];
                Plain(id + ".name", products[i, 1], products[i, 2]);
                Counted(id + ".count", products[i, 3], products[i, 4], products[i, 5], products[i, 6]);
            }

            // The 25 dyes and the 31 palettes (doc 01 section 9).
            var dyes = Dyes;
            for (int i = 0; i < dyes.GetLength(0); i++)
                Plain(dyes[i, 0] + ".name", dyes[i, 1], dyes[i, 2]);
            var palettes = Palettes;
            for (int i = 0; i < palettes.GetLength(0); i++)
                Plain(palettes[i, 0] + ".name", palettes[i, 1], palettes[i, 2]);

            // The 10 challenges (doc 01 S-ACT-006 « Défis »).
            var challenges = Challenges;
            for (int i = 0; i < challenges.GetLength(0); i++)
            {
                Plain(GardenText.ChallengeName(i), challenges[i, 0], challenges[i, 1]);
                Plain(GardenText.ChallengeGoal(i), challenges[i, 2], challenges[i, 3]);
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
