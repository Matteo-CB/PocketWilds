#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// A destination's specialty (doc 01 Contenu, tables 7.4 and 7.5), as data: what it adds to the common loot, and from
    /// which duration on (<see cref="ExpeditionDuration.OneHour"/>: « à chaque durée »).
    /// </summary>
    public struct ExpeditionSpecialty
    {
        /// <summary>+n seeds of the destination's line at every duration.</summary>
        public int Seeds;

        /// <summary>+n treats at every duration.</summary>
        public int Treats;

        /// <summary>+n points of sticker chance at every duration.</summary>
        public int StickerPoints;

        /// <summary>Light +n % from <see cref="LightFrom"/> on.</summary>
        public int LightPercent;

        public ExpeditionDuration LightFrom;

        public int Tickets;
        public ExpeditionDuration TicketsFrom;

        /// <summary>+n points of Discovery egg chance from <see cref="DiscoveryFrom"/> on.</summary>
        public int DiscoveryPoints;

        public ExpeditionDuration DiscoveryFrom;

        /// <summary>A Bakery ingredient (Pâtisserie ouverte) from <see cref="IngredientFrom"/> on.</summary>
        public ContentId Ingredient;

        public int Ingredients;
        public ExpeditionDuration IngredientFrom;

        /// <summary>A second Bakery ingredient at every duration (the Golden Oak Wood's Hazelnut; event destinations, T48+).</summary>
        public ContentId Ingredient2;

        public int Ingredients2;

        /// <summary>Seeds of one named plant from <see cref="PlantFrom"/> on (the Petal Bridge's Dwarf Cherry).</summary>
        public ContentId Plant;

        public int PlantSeeds;
        public ExpeditionDuration PlantFrom;

        /// <summary>A dye (Teintures ouvertes) from <see cref="DyeFrom"/> on (the Moon Garden's Moon Silver).</summary>
        public ContentId Dye;

        public int Dyes;
        public ExpeditionDuration DyeFrom;

        /// <summary>Event currency +n % (+20; +50 is the Great Migration's × 1.5 as the featured activity).</summary>
        public int CurrencyPercent;

        /// <summary>Kilometres added to the Great Migration's shared goal (event destinations, T48+).</summary>
        public int MigrationKm;
    }

    /// <summary>One destination of the catalogue with its data: the destination, its seed line, its specialty, its event.</summary>
    public sealed class ExpeditionDestinationInfo
    {
        public readonly ExpeditionDestination Destination;

        /// <summary>Its place in <see cref="ExpeditionCatalog.All"/> (biome destinations first, in opening order).</summary>
        public readonly int Index;

        /// <summary>The plants of its seed line (doc 01, table 7.4); an event destination uses the newest biome's.</summary>
        public readonly ContentId[] SeedLine;

        public readonly ExpeditionSpecialty Specialty;

        /// <summary>The big event (EV101 … EV112 → 101 … 112); 0 for a biome destination.</summary>
        public readonly int EventNumber;

        /// <summary>This build has its postcard set and card art (T42: Prés fleuris only); the others stay locked.</summary>
        public readonly bool ShippedInThisBuild;

        public ExpeditionDestinationInfo(int index, string key, string productionId, string? biome, RigFamilyMask favorite, int eventNumber,
            string[] seedLine, ExpeditionSpecialty specialty, bool shipped)
        {
            Index = index;
            Destination = new ExpeditionDestination(ContentId.FromSource(key), key, productionId,
                biome != null ? ContentId.FromSource(biome) : default, favorite, eventNumber > 0);
            SeedLine = new ContentId[seedLine.Length];
            for (int i = 0; i < seedLine.Length; i++)
                SeedLine[i] = ContentId.FromSource(seedLine[i]);
            Specialty = specialty;
            EventNumber = eventNumber;
            ShippedInThisBuild = shipped;
        }

        public ContentId Id => Destination.Id;
        public bool IsEvent => Destination.Event;
    }

    /// <summary>How a challenge picks its destination (doc 01 S-ACT-005 « Défis », column « Objectif »).</summary>
    public enum ExpeditionChallengeTarget : byte
    {
        /// <summary>« partir vers les Prés fleuris »: one named destination.</summary>
        Named = 0,

        /// <summary>« partir où l'on veut »: any open destination.</summary>
        Any = 1,

        /// <summary>« partir vers une destination de biome ».</summary>
        AnyBiome = 2,

        /// <summary>« partir vers la destination du biome le plus récent ».</summary>
        NewestBiome = 3,
    }

    /// <summary>A challenge's team constraint (column « Contrainte »).</summary>
    public enum ExpeditionChallengeRule : byte
    {
        None = 0,

        /// <summary>At least <see cref="ExpeditionChallenge.Count"/> animals of the biome <see cref="ExpeditionChallenge.Biome"/>.</summary>
        BiomeAnimals = 1,

        /// <summary>At least one animal of the family <see cref="ExpeditionChallenge.Family"/>.</summary>
        AnyOfFamily = 2,

        /// <summary>3 animals.</summary>
        TeamOfThree = 3,

        /// <summary>3 animals, all of the family <see cref="ExpeditionChallenge.Family"/>.</summary>
        ThreeOfFamily = 4,

        /// <summary>3 animals of 3 different rig families.</summary>
        ThreeFamilies = 5,

        /// <summary>3 animals whose species come from the destination's biome.</summary>
        ThreeFromDestinationBiome = 6,

        /// <summary>3 species from 3 different biomes.</summary>
        ThreeBiomes = 7,
    }

    /// <summary>One of the 10 expedition challenges (doc 01 S-ACT-005 « Défis »): stars from the team rating's points.</summary>
    public readonly struct ExpeditionChallenge
    {
        public readonly ExpeditionDuration Duration;
        public readonly ExpeditionChallengeTarget Target;
        public readonly ContentId Destination;
        public readonly ExpeditionChallengeRule Rule;
        public readonly RigFamilyMask Family;
        public readonly ContentId Biome;
        public readonly int Count;
        public readonly int OneStar;
        public readonly int TwoStars;
        public readonly int ThreeStars;

        public ExpeditionChallenge(ExpeditionDuration duration, ExpeditionChallengeTarget target, string? destination, ExpeditionChallengeRule rule,
            RigFamilyMask family, string? biome, int count, int oneStar, int twoStars, int threeStars)
        {
            Duration = duration;
            Target = target;
            Destination = destination != null ? ContentId.FromSource(destination) : default;
            Rule = rule;
            Family = family;
            Biome = biome != null ? ContentId.FromSource(biome) : default;
            Count = count;
            OneStar = oneStar;
            TwoStars = twoStars;
            ThreeStars = threeStars;
        }

        public int Threshold(int stars) => stars <= 1 ? OneStar : stars == 2 ? TwoStars : ThreeStars;
    }

    /// <summary>
    /// The 13 biome and 24 event destinations of doc 01 Contenu (tables 7.4 and 7.5), the plants of their seed lines with
    /// the biome that opens each biome plant (section 8.2), the Bakery ingredients and the dye they give, and the 10
    /// challenges of S-ACT-005. Static, built once; lookups never allocate.
    /// </summary>
    public static class ExpeditionCatalog
    {
        public const int BiomeDestinationCount = 13;
        public const int EventDestinationCount = 24;
        public const int Count = BiomeDestinationCount + EventDestinationCount;
        public const int ChallengeCount = 10;

        public static readonly ContentId FlowerMeadows = ContentId.FromSource("expedition.flower_meadows");

        // Bakery ingredients (doc 01, section 11) and the Moon Garden's dye.
        public static readonly ContentId GentleHoney = ContentId.FromSource("ingredient.gentle_honey");
        public static readonly ContentId VanillaCream = ContentId.FromSource("ingredient.vanilla_cream");
        public static readonly ContentId WheatFlour = ContentId.FromSource("ingredient.wheat_flour");
        public static readonly ContentId Blueberry = ContentId.FromSource("ingredient.blueberry");
        public static readonly ContentId Apple = ContentId.FromSource("ingredient.apple");
        public static readonly ContentId Hazelnut = ContentId.FromSource("ingredient.hazelnut");
        public static readonly ContentId MoonSilver = ContentId.FromSource("dye.moon_silver");

        private const ExpeditionDuration H1 = ExpeditionDuration.OneHour;
        private const ExpeditionDuration H4 = ExpeditionDuration.FourHours;
        private const ExpeditionDuration H8 = ExpeditionDuration.EightHours;
        private const ExpeditionDuration H12 = ExpeditionDuration.TwelveHours;

        /// <summary>The 13 biome plants of section 8.2 and the biome that makes each known; the starting plants are always known.</summary>
        private static readonly ContentId[] BiomePlants =
        {
            P("plant.sweet_pea"), P("plant.hazel"), P("plant.water_bloom"), P("plant.blue_gentian"), P("plant.snow_lingonberry"),
            P("plant.dwarf_banana"), P("plant.flowering_cactus"), P("plant.dwarf_cherry"), P("plant.glow_moss"), P("plant.cloud_puff"),
            P("plant.moonflower"), P("plant.giant_fern"), P("plant.star_bloom"),
        };

        private static readonly ContentId[] BiomePlantBiomes =
        {
            P("biome.grassland"), P("biome.forest"), P("biome.lake"), P("biome.mountain"), P("biome.snow"), P("biome.jungle"),
            P("biome.desert"), P("biome.sakura"), P("biome.mushroom"), P("biome.cloudlands"), P("biome.lunar"), P("biome.prehistoric"),
            P("biome.starlight"),
        };

        /// <summary>Biome destinations first (opening order of the biomes), then the event destinations (EV101 to EV112, A then B).</summary>
        public static readonly ExpeditionDestinationInfo[] All = Build();

        /// <summary>The 10 challenges, in doc order (challenge.expeditions.01 = index 0).</summary>
        public static readonly ExpeditionChallenge[] Challenges =
        {
            // 01 « Premier voyage »: to the Flower Meadows, 1 h.
            new ExpeditionChallenge(H1, ExpeditionChallengeTarget.Named, "expedition.flower_meadows", ExpeditionChallengeRule.None,
                RigFamilyMask.None, null, 0, 10, 30, 40),
            // 02 « Équipe des bois »: to the Stump Wood, 4 h, at least 2 Forest animals.
            new ExpeditionChallenge(H4, ExpeditionChallengeTarget.Named, "expedition.stump_wood", ExpeditionChallengeRule.BiomeAnimals,
                RigFamilyMask.None, "biome.forest", 2, 30, 50, 60),
            // 03 « Au bord de l'eau »: to the Reed Shores, 4 h, at least one SwimmerLow.
            new ExpeditionChallenge(H4, ExpeditionChallengeTarget.Named, "expedition.reed_shores", ExpeditionChallengeRule.AnyOfFamily,
                RigFamilyMask.SwimmerLow, null, 1, 20, 40, 60),
            // 04 « Grand départ »: anywhere, 8 h, 3 animals.
            new ExpeditionChallenge(H8, ExpeditionChallengeTarget.Any, null, ExpeditionChallengeRule.TeamOfThree,
                RigFamilyMask.None, null, 3, 30, 50, 70),
            // 05 « Tout petits »: anywhere, 4 h, 3 SmallQuadruped animals.
            new ExpeditionChallenge(H4, ExpeditionChallengeTarget.Any, null, ExpeditionChallengeRule.ThreeOfFamily,
                RigFamilyMask.SmallQuadruped, null, 3, 30, 50, 60),
            // 06 « Ailes au vent »: to the Torrent Pasture, 8 h, at least one bird.
            new ExpeditionChallenge(H8, ExpeditionChallengeTarget.Named, "expedition.torrent_pasture", ExpeditionChallengeRule.AnyOfFamily,
                RigFamilyMask.Bird, null, 1, 20, 40, 60),
            // 07 « Trio varié »: anywhere, 8 h, 3 different rig families.
            new ExpeditionChallenge(H8, ExpeditionChallengeTarget.Any, null, ExpeditionChallengeRule.ThreeFamilies,
                RigFamilyMask.None, null, 3, 30, 50, 60),
            // 08 « Longue route »: anywhere, 12 h.
            new ExpeditionChallenge(H12, ExpeditionChallengeTarget.Any, null, ExpeditionChallengeRule.None,
                RigFamilyMask.None, null, 0, 30, 50, 70),
            // 09 « Retour aux sources »: a biome destination, 8 h, 3 animals of the destination's biome.
            new ExpeditionChallenge(H8, ExpeditionChallengeTarget.AnyBiome, null, ExpeditionChallengeRule.ThreeFromDestinationBiome,
                RigFamilyMask.None, null, 3, 60, 70, 80),
            // 10 « Le grand voyage »: the newest biome's destination, 12 h, 3 species of 3 different biomes.
            new ExpeditionChallenge(H12, ExpeditionChallengeTarget.NewestBiome, null, ExpeditionChallengeRule.ThreeBiomes,
                RigFamilyMask.None, null, 3, 30, 50, 60),
        };

        /// <summary>Index of a destination in <see cref="All"/>, or −1. No allocation.</summary>
        public static int IndexOf(ContentId destination)
        {
            if (!destination.IsValid)
                return -1;
            var all = All;
            for (int i = 0; i < all.Length; i++)
                if (all[i].Destination.Id == destination)
                    return i;
            return -1;
        }

        public static bool TryGet(ContentId destination, out ExpeditionDestinationInfo info)
        {
            int index = IndexOf(destination);
            info = index >= 0 ? All[index] : null!;
            return index >= 0;
        }

        /// <summary>The biome destination of a biome (−1: none).</summary>
        public static int IndexOfBiome(ContentId biome)
        {
            if (!biome.IsValid)
                return -1;
            for (int i = 0; i < BiomeDestinationCount; i++)
                if (All[i].Destination.Biome == biome)
                    return i;
            return -1;
        }

        /// <summary>
        /// The biome that makes a plant known (section 7.4: « une plante qui n'est pas encore connue du joueur (biome fermé)
        /// est retirée du tirage »); <see cref="ContentId.None"/> for a starting plant, always known.
        /// </summary>
        public static ContentId PlantBiome(ContentId plant)
        {
            for (int i = 0; i < BiomePlants.Length; i++)
                if (BiomePlants[i] == plant)
                    return BiomePlantBiomes[i];
            return ContentId.None;
        }

        /// <summary>
        /// A biome destination is open: shipped in this build and its biome open (<paramref name="progression"/> null: the
        /// Prairie alone). An event destination never opens before the big events exist (T48+).
        /// </summary>
        public static bool IsOpen(ExpeditionDestinationInfo info, ProgressionServices? progression)
        {
            if (!info.ShippedInThisBuild || info.IsEvent)
                return false;
            var biome = info.Destination.Biome;
            return progression != null ? progression.Store.IsUnlocked(biome) : biome == ActivityCatalog.Meadow;
        }

        /// <summary>Open destinations now (JRN-026's « au moins n destinations ouvertes »). No allocation.</summary>
        public static int OpenCount(ProgressionServices? progression)
        {
            int open = 0;
            for (int i = 0; i < All.Length; i++)
                if (IsOpen(All[i], progression))
                    open++;
            return open;
        }

        private static ContentId P(string source) => ContentId.FromSource(source);

        private static ExpeditionDestinationInfo[] Build()
        {
            var all = new ExpeditionDestinationInfo[Count];
            int n = 0;
            // ---------------- 7.4: the 13 biome destinations ----------------
            // EXP-GL Prés fleuris: +1 seed at every duration; +1 Wheat Flour at 8 h and 12 h (Bakery open).
            all[n] = new ExpeditionDestinationInfo(n++, "expedition.flower_meadows", "EXP-GL", "biome.grassland", RigFamilyMask.SmallQuadruped, 0,
                new[] { "plant.sweet_pea", "plant.sweet_clover", "plant.daisy", "plant.wild_strawberry" },
                new ExpeditionSpecialty { Seeds = 1, Ingredient = WheatFlour, Ingredients = 1, IngredientFrom = H8 }, shipped: true);
            // EXP-FO Bois des souches: +1 treat at every duration; +1 Blueberry at every duration (Bakery open).
            all[n] = new ExpeditionDestinationInfo(n++, "expedition.stump_wood", "EXP-FO", "biome.forest", RigFamilyMask.MediumQuadruped, 0,
                new[] { "plant.hazel", "plant.blueberry", "plant.poppy" },
                new ExpeditionSpecialty { Treats = 1, Ingredient = Blueberry, Ingredients = 1, IngredientFrom = H1 }, shipped: false);
            // EXP-LA Rives aux roseaux: +10 points of sticker chance.
            all[n] = new ExpeditionDestinationInfo(n++, "expedition.reed_shores", "EXP-LA", "biome.lake", RigFamilyMask.SwimmerLow, 0,
                new[] { "plant.water_bloom", "plant.round_carrot", "plant.lavender" },
                new ExpeditionSpecialty { StickerPoints = 10 }, shipped: false);
            // EXP-MO Alpage du torrent: Light +15 %; +1 Wheat Flour at 8 h and 12 h (Bakery open).
            all[n] = new ExpeditionDestinationInfo(n++, "expedition.torrent_pasture", "EXP-MO", "biome.mountain", RigFamilyMask.Bird, 0,
                new[] { "plant.blue_gentian", "plant.golden_wheat", "plant.blueberry" },
                new ExpeditionSpecialty { LightPercent = 15, LightFrom = H1, Ingredient = WheatFlour, Ingredients = 1, IngredientFrom = H8 },
                shipped: false);
            // EXP-SN Pinède givrée: +1 ticket at 4 h, 8 h and 12 h.
            all[n] = new ExpeditionDestinationInfo(n++, "expedition.frosted_pines", "EXP-SN", "biome.snow", RigFamilyMask.HeavyQuadruped, 0,
                new[] { "plant.snow_lingonberry", "plant.meadow_indigo", "plant.dwarf_apple" },
                new ExpeditionSpecialty { Tickets = 1, TicketsFrom = H4 }, shipped: false);
            // EXP-JU Cascade turquoise: +1 Vanilla Cream at 4 h, 8 h and 12 h (Bakery open).
            all[n] = new ExpeditionDestinationInfo(n++, "expedition.turquoise_falls", "EXP-JU", "biome.jungle", RigFamilyMask.ClimberPrimate, 0,
                new[] { "plant.dwarf_banana", "plant.round_melon", "plant.sunflower" },
                new ExpeditionSpecialty { Ingredient = VanillaCream, Ingredients = 1, IngredientFrom = H4 }, shipped: false);
            // EXP-DE Oasis des arches: Discovery egg chance +2 points at 4 h, 8 h and 12 h.
            all[n] = new ExpeditionDestinationInfo(n++, "expedition.arch_oasis", "EXP-DE", "biome.desert", RigFamilyMask.SmallQuadruped, 0,
                new[] { "plant.flowering_cactus", "plant.sunflower", "plant.golden_wheat" },
                new ExpeditionSpecialty { DiscoveryPoints = 2, DiscoveryFrom = H4 }, shipped: false);
            // EXP-SA Pont des pétales: +1 Dwarf Cherry seed at 8 h and 12 h.
            all[n] = new ExpeditionDestinationInfo(n++, "expedition.petal_bridge", "EXP-SA", "biome.sakura", RigFamilyMask.Bird, 0,
                new[] { "plant.dwarf_cherry", "plant.wild_strawberry", "plant.sweet_pea" },
                new ExpeditionSpecialty { Plant = P("plant.dwarf_cherry"), PlantSeeds = 1, PlantFrom = H8 }, shipped: false);
            // EXP-MU Cercle de champignons: +1 treat and +5 points of sticker chance.
            all[n] = new ExpeditionDestinationInfo(n++, "expedition.mushroom_ring", "EXP-MU", "biome.mushroom", RigFamilyMask.Hopper, 0,
                new[] { "plant.glow_moss", "plant.blueberry", "plant.lavender" },
                new ExpeditionSpecialty { Treats = 1, StickerPoints = 5 }, shipped: false);
            // EXP-CL Archipel des nuages: Light +20 % at 12 h.
            all[n] = new ExpeditionDestinationInfo(n++, "expedition.cloud_archipelago", "EXP-CL", "biome.cloudlands", RigFamilyMask.MediumQuadruped, 0,
                new[] { "plant.cloud_puff", "plant.daisy", "plant.round_melon" },
                new ExpeditionSpecialty { LightPercent = 20, LightFrom = H12 }, shipped: false);
            // EXP-LU Jardin de lune: +1 Moon Silver dye at 8 h and 12 h (dyes open).
            all[n] = new ExpeditionDestinationInfo(n++, "expedition.moon_garden", "EXP-LU", "biome.lunar", RigFamilyMask.ClimberPrimate, 0,
                new[] { "plant.moonflower", "plant.lavender", "plant.meadow_indigo" },
                new ExpeditionSpecialty { Dye = MoonSilver, Dyes = 1, DyeFrom = H8 }, shipped: false);
            // EXP-PR Vallée des fougères: +1 seed and +10 points of sticker chance.
            all[n] = new ExpeditionDestinationInfo(n++, "expedition.fern_valley", "EXP-PR", "biome.prehistoric", RigFamilyMask.HeavyQuadruped, 0,
                new[] { "plant.giant_fern", "plant.dwarf_banana", "plant.dwarf_apple" },
                new ExpeditionSpecialty { Seeds = 1, StickerPoints = 10 }, shipped: false);
            // EXP-SL Rivage des nébuleuses: Discovery egg chance +3 points at 8 h and 12 h.
            all[n] = new ExpeditionDestinationInfo(n++, "expedition.nebula_shore", "EXP-SL", "biome.starlight", RigFamilyMask.SwimmerLow, 0,
                new[] { "plant.star_bloom", "plant.moonflower", "plant.cloud_puff" },
                new ExpeditionSpecialty { DiscoveryPoints = 3, DiscoveryFrom = H8 }, shipped: false);

            // ---------------- 7.5: the 24 event destinations (the newest biome's treats and seeds; family: the featured animal's) ----------------
            var currency = new ExpeditionSpecialty { CurrencyPercent = 20 };
            var ticket = new ExpeditionSpecialty { Tickets = 1, TicketsFrom = H1 };
            var treat = new ExpeditionSpecialty { Treats = 1 };
            var seed = new ExpeditionSpecialty { Seeds = 1 };
            var sticker = new ExpeditionSpecialty { StickerPoints = 10 };
            all[n] = Event(n++, "expedition.aurora_frozen_lake", "EXP-EV101-A", 101, currency);
            all[n] = Event(n++, "expedition.aurora_veil_plateau", "EXP-EV101-B", 101, ticket);
            all[n] = Event(n++, "expedition.contest_meadow_stage", "EXP-EV102-A", 102, currency);
            all[n] = Event(n++, "expedition.contest_ribbon_lane", "EXP-EV102-B", 102, treat);
            all[n] = Event(n++, "expedition.burrows_bank", "EXP-EV103-A", 103, currency);
            all[n] = Event(n++, "expedition.burrows_root_tunnel", "EXP-EV103-B", 103, seed);
            all[n] = Event(n++, "expedition.bloom_petal_valley", "EXP-EV104-A", 104, currency);
            all[n] = Event(n++, "expedition.bloom_white_orchard", "EXP-EV104-B", 104, seed);
            // The Great Migration: currency × 1.5 (the featured activity), +10 km to the shared goal or +1 ticket.
            all[n] = Event(n++, "expedition.migration_faraway_hills", "EXP-EV105-A", 105, new ExpeditionSpecialty { CurrencyPercent = 50, MigrationKm = 10 });
            all[n] = Event(n++, "expedition.migration_sea_of_clouds", "EXP-EV105-B", 105,
                new ExpeditionSpecialty { CurrencyPercent = 50, Tickets = 1, TicketsFrom = H1 });
            all[n] = Event(n++, "expedition.fireflies_pond", "EXP-EV106-A", 106, currency);
            all[n] = Event(n++, "expedition.fireflies_lantern_glade", "EXP-EV106-B", 106, treat);
            all[n] = Event(n++, "expedition.sunny_golden_cove", "EXP-EV107-A", 107, currency);
            all[n] = Event(n++, "expedition.sunny_shell_islet", "EXP-EV107-B", 107, sticker);
            all[n] = Event(n++, "expedition.stars_shooting_hill", "EXP-EV108-A", 108, currency);
            all[n] = Event(n++, "expedition.stars_stardust_dunes", "EXP-EV108-B", 108, ticket);
            all[n] = Event(n++, "expedition.winds_windy_ridges", "EXP-EV109-A", 109, currency);
            all[n] = Event(n++, "expedition.winds_kite_plain", "EXP-EV109-B", 109, treat);
            all[n] = Event(n++, "expedition.harvest_autumn_orchard", "EXP-EV110-A", 110, currency);
            // Golden Oak Wood: +1 Apple and +1 Hazelnut at every duration (Bakery open).
            all[n] = Event(n++, "expedition.harvest_golden_oaks", "EXP-EV110-B", 110,
                new ExpeditionSpecialty { Ingredient = Apple, Ingredients = 1, IngredientFrom = H1, Ingredient2 = Hazelnut, Ingredients2 = 1 });
            all[n] = Event(n++, "expedition.mushrooms_glowing_undergrowth", "EXP-EV111-A", 111, currency);
            all[n] = Event(n++, "expedition.mushrooms_hidden_clearing", "EXP-EV111-B", 111, sticker);
            all[n] = Event(n++, "expedition.frost_frost_lake", "EXP-EV112-A", 112, currency);
            all[n] = Event(n++, "expedition.frost_snowy_valley", "EXP-EV112-B", 112, treat);
            return all;
        }

        private static ExpeditionDestinationInfo Event(int index, string key, string productionId, int eventNumber, ExpeditionSpecialty specialty) =>
            new ExpeditionDestinationInfo(index, key, productionId, null, RigFamilyMask.None, eventNumber, System.Array.Empty<string>(), specialty,
                shipped: false);
    }
}
