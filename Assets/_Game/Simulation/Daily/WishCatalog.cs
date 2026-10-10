#nullable enable
using System;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The wish templates of doc 01 Contenu (section 2.3) by their doc number (WISH-001 = 1 … WISH-083 = 83), and the
    /// tutorial wish WISH-T01. Stable values: saves keep them in <see cref="WishRecord.Template"/>.
    /// </summary>
    public enum WishTemplateId : ushort
    {
        None = 0,
        HatchEggs = 1,
        HatchBiomeEggs = 2,
        HatchPreciousEgg = 3,
        DiscoverVariant = 4,
        RaiseSpeciesMastery = 5,
        LookAtSpeciesInCodex = 6,
        DiscoverSpecies = 7,
        StartBreeding = 8,
        WelcomeNewborn = 9,
        EarnSticker = 10,
        OpenBiomeAlbum = 11,
        GreetVisitor = 12,
        PhotographVisitor = 13,
        TapAnimals = 14,
        CuddleAnimal = 15,
        GiveAnimalTreat = 16,
        GiveTreats = 17,
        EarnBond = 18,
        GroomAnimal = 19,
        GroomingStars = 20,
        GroomingLeaves = 21,
        GroomingBow = 22,
        DressAnimal = 23,
        ChangeOutfits = 24,
        BuyUpgrades = 25,
        UpgradeInBiome = 26,
        PlaceDecoration = 27,
        PlaceBiomeDecorations = 28,
        MoveDecoration = 29,
        ExpandBiome = 30,
        PlantSeeds = 31,
        HarvestPlants = 32,
        WaterSprouts = 33,
        HarvestCrop = 34,
        BringCrop = 35,
        HarvestDye = 36,
        BakeCake = 37,
        BakeCakeForAnimal = 38,
        BakeCakeNoMistake = 39,
        BakeCakes = 40,
        StartExpedition = 41,
        StartLongExpedition = 42,
        SendAnimalOnExpedition = 43,
        SendTeamOfThree = 44,
        OpenPostcard = 45,
        HeadToDestination = 46,
        PhotographAnimal = 47,
        SafariShots = 48,
        PhotographTogether = 49,
        PhotographNap = 50,
        PlayHideAndSeek = 51,
        FindHiddenAnimals = 52,
        FindEveryHiddenAnimal = 53,
        PlayNutDrop = 54,
        NutDropCatch = 55,
        NutDropStreak = 56,
        PlayPawMemory = 57,
        PawMemoryPairs = 58,
        PawMemoryMoves = 59,
        PlayTreasureFishing = 60,
        ReelTreasures = 61,
        ReelPearl = 62,
        PlayLilyHop = 63,
        LilyHopDistance = 64,
        LilyHopPerfect = 65,
        CuddleMatchLevels = 66,
        CuddleMatchFreeAnimals = 67,
        CuddleMatchBigHugs = 68,
        PlayGlide = 69,
        GlideDistance = 70,
        GlideStars = 71,
        DrawConstellation = 72,
        DrawSpeciesConstellation = 73,
        PlayFireflySong = 74,
        FireflyStreak = 75,
        PopBubbles = 76,
        PopBubbleGroup = 77,
        FinishTilePuzzle = 78,
        FinishBigTilePuzzle = 79,
        FindBurrowMazeExit = 80,
        BurrowMazeSeeds = 81,
        EnterGrandContest = 82,
        VoteGrandContest = 83,

        /// <summary>WISH-T01 "Hatch 2 eggs", expressed by the first animal at welcome step A08, outside the daily draw.</summary>
        Tutorial = 900,
    }

    /// <summary>What a template's subject is (matches <see cref="WishRecord.ParamKind"/>). Stable values.</summary>
    public enum WishParamKind : byte
    {
        None = 0,
        Species = 1,
        Biome = 2,

        /// <summary>The animal expressing the wish ({animal} in the text).</summary>
        Animal = 3,

        /// <summary>An upgrade kind (<see cref="WishRecord.ParamExtra"/>) in the biome of <see cref="WishRecord.Param"/>.</summary>
        Upgrade = 4,

        /// <summary>T42: an expedition destination ({destination}, WISH-046): its id in <see cref="WishRecord.Param"/>.</summary>
        Destination = 5,

        /// <summary>
        /// T43: a garden product ({crop}, WISH-034): its id (crop.* or ingredient.*) in <see cref="WishRecord.Param"/>. WISH-035
        /// « Apporte {n} {crop} à {animal} » keeps <see cref="Animal"/> for its {animal} and holds its crop in Param too.
        /// </summary>
        Crop = 6,
    }

    /// <summary>
    /// One wish template (doc 01 Contenu, section 2.3): its system and family, base draw weight, typical duration, the
    /// amounts P1 to P4 by refuge level tier, what it names, and whether its system exists in this build.
    /// </summary>
    public readonly struct WishTemplate
    {
        public readonly WishTemplateId Id;
        public readonly WishSystem System;
        public readonly WishFamily Family;

        /// <summary>Base draw weight (section 2.4).</summary>
        public readonly int Weight;

        /// <summary>Typical real play time to fulfil it, in seconds (0 for a timer wish).</summary>
        public readonly int TypicalSeconds;

        /// <summary>Waits for a sprout, an expedition, a breeding or a visitor (« minuteur »).</summary>
        public readonly bool Timer;

        /// <summary>Quick: 2 minutes or less and no timer.</summary>
        public readonly bool Quick;

        public readonly int P1;
        public readonly int P2;
        public readonly int P3;
        public readonly int P4;

        public readonly WishParamKind ParamKind;

        /// <summary>A score wish (one game): its amount follows the score rule (<see cref="WishCatalog.ScoreTarget"/>).</summary>
        public readonly bool Score;

        /// <summary>Its system exists in this build (T36: SYS-01, 02, 03, 04, 06; T38: SYS-09; T39: SYS-10; T40: SYS-11; T41: SYS-12; T42: SYS-13; T43: SYS-07). Other templates are never drawn.</summary>
        public readonly bool ShippedInThisBuild;

        public WishTemplate(WishTemplateId id, WishSystem system, int weight, int typicalSeconds, bool timer, int p1, int p2, int p3, int p4,
            WishParamKind paramKind, bool score, bool shipped)
        {
            Id = id;
            System = system;
            Family = WishSystems.FamilyOf(system);
            Weight = weight;
            TypicalSeconds = typicalSeconds;
            Timer = timer;
            Quick = !timer && typicalSeconds <= WishCatalog.QuickMaxSeconds;
            P1 = p1;
            P2 = p2;
            P3 = p3;
            P4 = p4;
            ParamKind = paramKind;
            Score = score;
            ShippedInThisBuild = shipped;
        }

        public bool IsValid => Id != WishTemplateId.None;

        /// <summary>Long: 10 minutes or more, or a timer.</summary>
        public bool Long => Timer || TypicalSeconds >= WishCatalog.LongMinSeconds;

        /// <summary>The amount {n} at a tier (1 to 4, section 0: P1 = NR 1-14, P2 = 15-39, P3 = 40-69, P4 = 70+).</summary>
        public int Amount(int tier) => tier <= 1 ? P1 : tier == 2 ? P2 : tier == 3 ? P3 : P4;
    }

    /// <summary>
    /// The 83 templates of doc 01 Contenu, section 2.3, in doc order (index = number - 1), and WISH-T01. Text keys are
    /// « wish.nnn » and « wish.t01 » (section 0). Only the systems of this build are drawn (<see cref="IsShipped"/>); a
    /// later task opens its system here when its feature ships.
    /// </summary>
    public static class WishCatalog
    {
        /// <summary>Quick: 2 minutes or less without a timer (section 2.3).</summary>
        public const int QuickMaxSeconds = 120;

        /// <summary>Long: 10 minutes or more, or a timer.</summary>
        public const int LongMinSeconds = 600;

        private const int Min = 60;

        public static readonly WishTemplate[] All =
        {
            // ---- SYS-01 Hatching (Collection) ----
            // WISH-001 "Hatch {n} eggs": always.
            T(WishTemplateId.HatchEggs, WishSystem.Hatching, 10, 2 * Min, 3, 5, 8, 10),
            // WISH-002 "Hatch {n} {biome} eggs": 2 biomes open; {biome} is an open biome other than the newest.
            T(WishTemplateId.HatchBiomeEggs, WishSystem.Hatching, 6, 3 * Min, 2, 3, 4, 5, WishParamKind.Biome),
            // WISH-003 "Hatch a precious egg": an open Focus egg costs at most 15 minutes of production.
            T(WishTemplateId.HatchPreciousEgg, WishSystem.Hatching, 5, 5 * Min, 1, 1, 1, 1),

            // ---- SYS-02 Discoveries and mastery (Collection) ----
            // WISH-004 "Discover a variant": at least one base variant not found yet in an open egg.
            T(WishTemplateId.DiscoverVariant, WishSystem.DiscoveriesAndMastery, 6, 10 * Min, 1, 1, 1, 1),
            // WISH-005 "Raise {species} by one mastery level": {species} owned, at most 40 % of the next level's XP missing.
            T(WishTemplateId.RaiseSpeciesMastery, WishSystem.DiscoveriesAndMastery, 4, 10 * Min, 1, 1, 1, 1, WishParamKind.Species),
            // WISH-006 "Look at {species} in the Codex": discovered less than 3 days ago, page never opened.
            T(WishTemplateId.LookAtSpeciesInCodex, WishSystem.DiscoveriesAndMastery, 3, 30, 1, 1, 1, 1, WishParamKind.Species),
            // WISH-007 "Discover a new species": a normal species not discovered yet in an open egg.
            T(WishTemplateId.DiscoverSpecies, WishSystem.DiscoveriesAndMastery, 3, 20 * Min, 1, 1, 1, 1),

            // ---- SYS-24 Breeding (Collection) ----
            // WISH-008 "Start a breeding": a free breeding slot.
            T(WishTemplateId.StartBreeding, WishSystem.Breeding, 5, 1 * Min, 1, 1, 1, 1),
            // WISH-009 "Welcome a newborn": a breeding running or a free slot (timer: the breeding).
            T(WishTemplateId.WelcomeNewborn, WishSystem.Breeding, 4, 0, 1, 1, 1, 1, timer: true),

            // ---- SYS-25 Albums (Collection) ----
            // WISH-010 "Earn a sticker": albums open.
            T(WishTemplateId.EarnSticker, WishSystem.Albums, 5, 10 * Min, 1, 1, 1, 1),
            // WISH-011 "Open the {biome} album": a sticker of that album earned and never seen.
            T(WishTemplateId.OpenBiomeAlbum, WishSystem.Albums, 2, 30, 1, 1, 1, 1, WishParamKind.Biome),

            // ---- SYS-26 Rare visitors (Collection) ----
            // WISH-012 "Say hi to {visitor}": a rare visitor is in the refuge or arrives within 24 h (timer: its visit).
            T(WishTemplateId.GreetVisitor, WishSystem.RareVisitors, 4, 0, 1, 1, 1, 1, timer: true),
            // WISH-013 "Take a photo of a visitor": photo mode open and a visitor expected within 24 h (timer: its visit).
            T(WishTemplateId.PhotographVisitor, WishSystem.RareVisitors, 2, 0, 1, 1, 1, 1, timer: true),

            // ---- SYS-03 Cuddles and treats (Care) ----
            // WISH-014 "Tap {n} animals": always; n different animals.
            T(WishTemplateId.TapAnimals, WishSystem.CuddlesAndTreats, 8, 30, 5, 8, 10, 12),
            // WISH-015 "Give {animal} {n} cuddles": always; touches at least 2 s apart.
            T(WishTemplateId.CuddleAnimal, WishSystem.CuddlesAndTreats, 6, 30, 3, 3, 3, 3, WishParamKind.Animal),
            // WISH-016 "Give {animal} a treat": 1 treat owned or the treat stand open.
            T(WishTemplateId.GiveAnimalTreat, WishSystem.CuddlesAndTreats, 7, 30, 1, 1, 1, 1, WishParamKind.Animal),
            // WISH-017 "Give {n} treats": n treats owned or the treat stand open.
            T(WishTemplateId.GiveTreats, WishSystem.CuddlesAndTreats, 4, 1 * Min, 2, 3, 3, 4),
            // WISH-018 "Earn {n} bond points": always.
            T(WishTemplateId.EarnBond, WishSystem.CuddlesAndTreats, 4, 5 * Min, 20, 30, 40, 50),

            // ---- SYS-09 Grooming (Care) ----
            // WISH-019 "Groom {animal}": Grooming open; 1 game with that animal.
            T(WishTemplateId.GroomAnimal, WishSystem.Grooming, 7, 1 * Min, 1, 1, 1, 1, WishParamKind.Animal),
            // WISH-020 "Earn {n} stars in Grooming": Grooming open; cumulated.
            T(WishTemplateId.GroomingStars, WishSystem.Grooming, 4, 2 * Min, 2, 3, 3, 3),
            // WISH-021 "Pick off {n} leaves and twigs": Grooming open; cumulated.
            T(WishTemplateId.GroomingLeaves, WishSystem.Grooming, 3, 2 * Min, 8, 12, 15, 18),
            // WISH-022 "Finish a grooming with a bow": Grooming open.
            T(WishTemplateId.GroomingBow, WishSystem.Grooming, 3, 1 * Min, 1, 1, 1, 1),

            // ---- SYS-27 Outfits (Care) ----
            // WISH-023 "Dress up {animal}": 1 outfit or accessory owned.
            T(WishTemplateId.DressAnimal, WishSystem.Outfits, 5, 1 * Min, 1, 1, 1, 1, WishParamKind.Animal),
            // WISH-024 "Change outfits on {n} animals": 2 outfits or accessories owned.
            T(WishTemplateId.ChangeOutfits, WishSystem.Outfits, 3, 2 * Min, 2, 2, 3, 3),

            // ---- SYS-04 Upgrades (Refuge) ----
            // WISH-025 "Buy {n} upgrades": an upgrade costs at most 30 minutes of production (levels bought count).
            T(WishTemplateId.BuyUpgrades, WishSystem.Upgrades, 6, 3 * Min, 1, 2, 3, 3),
            // WISH-026 "Upgrade {upgrade} in {biome}": that level costs at most 30 minutes of production.
            T(WishTemplateId.UpgradeInBiome, WishSystem.Upgrades, 4, 3 * Min, 1, 1, 1, 1, WishParamKind.Upgrade),

            // ---- SYS-05 Decoration (Refuge) ----
            // WISH-027 "Place a decoration": 1 decoration not placed.
            T(WishTemplateId.PlaceDecoration, WishSystem.Decoration, 6, 1 * Min, 1, 1, 1, 1),
            // WISH-028 "Place {n} decorations in {biome}": n decorations not placed.
            T(WishTemplateId.PlaceBiomeDecorations, WishSystem.Decoration, 3, 2 * Min, 2, 2, 3, 3, WishParamKind.Biome),
            // WISH-029 "Move a decoration": 1 decoration placed.
            T(WishTemplateId.MoveDecoration, WishSystem.Decoration, 3, 30, 1, 1, 1, 1),

            // ---- SYS-06 Expansions (Refuge) ----
            // WISH-030 "Expand a biome": an expansion costs at most 45 minutes of production.
            T(WishTemplateId.ExpandBiome, WishSystem.Expansions, 5, 3 * Min, 1, 1, 1, 1),

            // ---- SYS-07 Garden (Refuge) ----
            // WISH-031 "Plant {n} seeds": n seeds and 1 free plot.
            T(WishTemplateId.PlantSeeds, WishSystem.Garden, 6, 1 * Min, 2, 3, 3, 4),
            // WISH-032 "Harvest {n} plants": Garden open (timer: 1 h).
            T(WishTemplateId.HarvestPlants, WishSystem.Garden, 6, 0, 2, 3, 4, 5, timer: true),
            // WISH-033 "Water {n} sprouts": 1 sprout growing.
            T(WishTemplateId.WaterSprouts, WishSystem.Garden, 4, 30, 2, 3, 3, 4),
            // WISH-034 "Harvest {n} {crop}": a seed of that crop's plant owned (timer: the plant's growth). T43: {crop} is the
            // main product of an open plant whose seed the player holds, in WishRecord.Param.
            T(WishTemplateId.HarvestCrop, WishSystem.Garden, 4, 0, 2, 3, 4, 5, WishParamKind.Crop, timer: true),
            // WISH-035 "Bring {animal} {n} {crop}": the crop or its seed owned; the crops are given (timer: growth). T43: the
            // crop in WishRecord.Param; held crops are given (taken from the inventory) at each garden harvest.
            T(WishTemplateId.BringCrop, WishSystem.Garden, 4, 0, 1, 2, 3, 3, WishParamKind.Animal, timer: true),
            // WISH-036 "Harvest a dye": Garden level 4 and a dye plant seed (timer: 2 h).
            T(WishTemplateId.HarvestDye, WishSystem.Garden, 3, 0, 1, 1, 1, 1, timer: true),

            // ---- SYS-08 Bakery (Refuge) ----
            // WISH-037 "Bake a cake": the bakery open.
            T(WishTemplateId.BakeCake, WishSystem.Bakery, 6, 2 * Min, 1, 1, 1, 1),
            // WISH-038 "Bake a {cake} for {animal}": recipe known and ingredients owned or harvestable.
            T(WishTemplateId.BakeCakeForAnimal, WishSystem.Bakery, 5, 3 * Min, 1, 1, 1, 1, WishParamKind.Animal),
            // WISH-039 "Bake a cake with no mistakes": the bakery open.
            T(WishTemplateId.BakeCakeNoMistake, WishSystem.Bakery, 3, 3 * Min, 1, 1, 1, 1),
            // WISH-040 "Bake {n} cakes": the bakery open.
            T(WishTemplateId.BakeCakes, WishSystem.Bakery, 3, 5 * Min, 2, 2, 3, 3),

            // ---- SYS-13 Expeditions (Travels) ----
            // WISH-041 "Start an expedition": 1 free slot.
            T(WishTemplateId.StartExpedition, WishSystem.Expeditions, 7, 1 * Min, 1, 1, 1, 1),
            // WISH-042 "Start an expedition of 4 h or more": 1 free slot.
            T(WishTemplateId.StartLongExpedition, WishSystem.Expeditions, 4, 1 * Min, 1, 1, 1, 1),
            // WISH-043 "Send {animal} on an expedition": {animal} free and 1 free slot.
            T(WishTemplateId.SendAnimalOnExpedition, WishSystem.Expeditions, 4, 1 * Min, 1, 1, 1, 1, WishParamKind.Animal),
            // WISH-044 "Send a team of 3 animals": 1 free slot.
            T(WishTemplateId.SendTeamOfThree, WishSystem.Expeditions, 3, 1 * Min, 1, 1, 1, 1),
            // WISH-045 "Open a postcard": 1 expedition running (timer: its return).
            T(WishTemplateId.OpenPostcard, WishSystem.Expeditions, 4, 0, 1, 1, 1, 1, timer: true),
            // WISH-046 "Head to {destination}": the destination open (T42: an open destination drawn, in WishRecord.Param).
            T(WishTemplateId.HeadToDestination, WishSystem.Expeditions, 3, 1 * Min, 1, 1, 1, 1, WishParamKind.Destination),

            // ---- SYS-16 Photo (Travels) ----
            // WISH-047 "Take a photo of {animal}": photo mode open.
            T(WishTemplateId.PhotographAnimal, WishSystem.Photo, 5, 1 * Min, 1, 1, 1, 1, WishParamKind.Animal),
            // WISH-048 "Take {n} Photo Safari shots": Photo Safari open; cumulated.
            T(WishTemplateId.SafariShots, WishSystem.Photo, 4, 2 * Min, 2, 3, 3, 3),
            // WISH-049 "Photograph {n} animals together": photo mode open.
            T(WishTemplateId.PhotographTogether, WishSystem.Photo, 3, 2 * Min, 2, 3, 3, 4),
            // WISH-050 "Photograph {animal} napping": photo mode open.
            T(WishTemplateId.PhotographNap, WishSystem.Photo, 2, 5 * Min, 1, 1, 1, 1, WishParamKind.Animal),

            // ---- SYS-10 Hide and Seek (Games; T39) ----
            // WISH-051 "Play Hide and Seek": open; 1 game played to its end.
            T(WishTemplateId.PlayHideAndSeek, WishSystem.HideAndSeek, 5, 1 * Min, 1, 1, 1, 1),
            // WISH-052 "Find {n} hidden animals": open; cumulated.
            T(WishTemplateId.FindHiddenAnimals, WishSystem.HideAndSeek, 4, 2 * Min, 4, 6, 8, 10),
            // WISH-053 "Find every animal in one game": open.
            T(WishTemplateId.FindEveryHiddenAnimal, WishSystem.HideAndSeek, 3, 1 * Min, 1, 1, 1, 1),

            // ---- SYS-11 Nut Drop (Games; T40) ----
            // WISH-054 "Play Nut Drop": open; 1 game played to its end.
            T(WishTemplateId.PlayNutDrop, WishSystem.NutRain, 5, 1 * Min, 1, 1, 1, 1),
            // WISH-055 "Catch {n} nuts in one game": open; score wish.
            T(WishTemplateId.NutDropCatch, WishSystem.NutRain, 4, 1 * Min, 30, 40, 50, 60, score: true),
            // WISH-056 "Get a {n} streak in Nut Drop": open; score wish.
            T(WishTemplateId.NutDropStreak, WishSystem.NutRain, 3, 1 * Min, 15, 20, 25, 30, score: true),

            // ---- SYS-12 Pawprint Memory (Games; T41) ----
            // WISH-057 "Play Pawprint Memory": open; 1 game played to its end.
            T(WishTemplateId.PlayPawMemory, WishSystem.PawMemory, 5, 1 * Min, 1, 1, 1, 1),
            // WISH-058 "Find {n} pairs in Memory": open; cumulated.
            T(WishTemplateId.PawMemoryPairs, WishSystem.PawMemory, 4, 2 * Min, 6, 8, 10, 12),
            // WISH-059 "Finish Memory in {n} moves or fewer": open; n = 2 x the grid's pairs + 6, set at the draw from the
            // player's Memory grid (0 here; WishService) and checked against each game's own grid.
            T(WishTemplateId.PawMemoryMoves, WishSystem.PawMemory, 3, 1 * Min, 0, 0, 0, 0),

            // ---- SYS-14 Treasure Fishing (Games) ----
            // WISH-060 "Play Treasure Fishing": the Lake; 1 game.
            T(WishTemplateId.PlayTreasureFishing, WishSystem.TreasureFishing, 5, 1 * Min, 1, 1, 1, 1),
            // WISH-061 "Reel in {n} treasures": the Lake; cumulated.
            T(WishTemplateId.ReelTreasures, WishSystem.TreasureFishing, 4, 2 * Min, 8, 12, 15, 18),
            // WISH-062 "Reel in a pearl": the Lake.
            T(WishTemplateId.ReelPearl, WishSystem.TreasureFishing, 2, 3 * Min, 1, 1, 1, 1),

            // ---- SYS-15 Lily Pad Hop (Games) ----
            // WISH-063 "Play Lily Pad Hop": the Lake; 1 game.
            T(WishTemplateId.PlayLilyHop, WishSystem.LilyHop, 5, 1 * Min, 1, 1, 1, 1),
            // WISH-064 "Hop {n} meters in one game": the Lake; score wish.
            T(WishTemplateId.LilyHopDistance, WishSystem.LilyHop, 4, 1 * Min, 40, 60, 80, 100, score: true),
            // WISH-065 "Land {n} perfect hops": the Lake; cumulated.
            T(WishTemplateId.LilyHopPerfect, WishSystem.LilyHop, 3, 1 * Min, 3, 5, 6, 8),

            // ---- SYS-17 Cuddle Match (Games) ----
            // WISH-066 "Clear {n} Cuddle Match levels": open.
            T(WishTemplateId.CuddleMatchLevels, WishSystem.CuddleMatch, 5, 3 * Min, 1, 2, 2, 3),
            // WISH-067 "Free {n} animals from bushes": Match level 5 reached; cumulated.
            T(WishTemplateId.CuddleMatchFreeAnimals, WishSystem.CuddleMatch, 3, 3 * Min, 3, 5, 6, 8),
            // WISH-068 "Make {n} big hugs": open; cumulated.
            T(WishTemplateId.CuddleMatchBigHugs, WishSystem.CuddleMatch, 3, 3 * Min, 2, 3, 4, 5),

            // ---- SYS-18 Glide (Games) ----
            // WISH-069 "Play Glide": the Mountain; 1 game.
            T(WishTemplateId.PlayGlide, WishSystem.Slide, 5, 1 * Min, 1, 1, 1, 1),
            // WISH-070 "Glide {n} meters in one game": the Mountain; score wish.
            T(WishTemplateId.GlideDistance, WishSystem.Slide, 4, 1 * Min, 300, 500, 700, 900, score: true),
            // WISH-071 "Collect {n} stars in Glide": the Mountain; cumulated.
            T(WishTemplateId.GlideStars, WishSystem.Slide, 3, 1 * Min, 10, 15, 20, 25),

            // ---- SYS-19 Constellations (Games) ----
            // WISH-072 "Draw a constellation": open.
            T(WishTemplateId.DrawConstellation, WishSystem.Constellations, 5, 1 * Min, 1, 1, 1, 1),
            // WISH-073 "Draw the {species} constellation": {species} discovered and in the list of section 15.
            T(WishTemplateId.DrawSpeciesConstellation, WishSystem.Constellations, 3, 1 * Min, 1, 1, 1, 1, WishParamKind.Species),

            // ---- SYS-20 Firefly Rhythm (Games) ----
            // WISH-074 "Play a Firefly Rhythm song": open.
            T(WishTemplateId.PlayFireflySong, WishSystem.FireflyRhythm, 5, 2 * Min, 1, 1, 1, 1),
            // WISH-075 "Hit a {n}-note streak": open; score wish.
            T(WishTemplateId.FireflyStreak, WishSystem.FireflyRhythm, 3, 2 * Min, 20, 30, 40, 50, score: true),

            // ---- SYS-21 Bubbles (Games) ----
            // WISH-076 "Pop {n} bubbles": open; cumulated.
            T(WishTemplateId.PopBubbles, WishSystem.Bubbles, 4, 1 * Min, 40, 60, 80, 100),
            // WISH-077 "Pop a group of {n} bubbles": open.
            T(WishTemplateId.PopBubbleGroup, WishSystem.Bubbles, 3, 1 * Min, 5, 6, 7, 8),

            // ---- SYS-22 Tile Puzzle (Games) ----
            // WISH-078 "Finish a Tile Puzzle": open.
            T(WishTemplateId.FinishTilePuzzle, WishSystem.TilePuzzle, 5, 2 * Min, 1, 1, 1, 1),
            // WISH-079 "Finish a 16-tile puzzle": open and a 16-tile grid already finished once.
            T(WishTemplateId.FinishBigTilePuzzle, WishSystem.TilePuzzle, 2, 3 * Min, 1, 1, 1, 1),

            // ---- SYS-23 Burrow Maze (Games) ----
            // WISH-080 "Find the way out of the Burrow Maze": open.
            T(WishTemplateId.FindBurrowMazeExit, WishSystem.BurrowMaze, 5, 1 * Min, 1, 1, 1, 1),
            // WISH-081 "Collect {n} seeds in the Burrow Maze": open; cumulated.
            T(WishTemplateId.BurrowMazeSeeds, WishSystem.BurrowMaze, 3, 1 * Min, 10, 15, 20, 25),

            // ---- SYS-28 Grand Contest (Games) ----
            // WISH-082 "Enter an animal in the Grand Contest": this week's contest open, not entered yet.
            T(WishTemplateId.EnterGrandContest, WishSystem.GrandContest, 5, 2 * Min, 1, 1, 1, 1),
            // WISH-083 "Vote {n} times in the Grand Contest": votes open (backend present).
            T(WishTemplateId.VoteGrandContest, WishSystem.GrandContest, 3, 2 * Min, 5, 10, 10, 15),
        };

        /// <summary>WISH-T01 "Hatch 2 eggs" (welcome step A08), never drawn: <see cref="WishService.StartTutorialWish"/> gives it.</summary>
        public static readonly WishTemplate Tutorial =
            new WishTemplate(WishTemplateId.Tutorial, WishSystem.Hatching, 0, 2 * Min, false, 2, 2, 2, 2, WishParamKind.None, false, true);

        private static readonly string[] Keys = BuildKeys();

        /// <summary>The systems whose wishes exist in this build (T36: Hatching, Discoveries and mastery, Cuddles and treats,
        /// Upgrades, Expansions; T38: Grooming, T39: Hide and Seek, T40: Nut Drop, T41: Pawprint Memory, T42: Expeditions and
        /// T43: the Garden, each with its activity).</summary>
        public static bool IsShipped(WishSystem system) =>
            system == WishSystem.Hatching || system == WishSystem.DiscoveriesAndMastery || system == WishSystem.CuddlesAndTreats
            || system == WishSystem.Upgrades || system == WishSystem.Expansions
            || (system == WishSystem.Grooming && ActivityCatalog.Get(ActivityId.Grooming).ShippedInThisBuild)
            || (system == WishSystem.HideAndSeek && ActivityCatalog.Get(ActivityId.HideAndSeek).ShippedInThisBuild)
            || (system == WishSystem.NutRain && ActivityCatalog.Get(ActivityId.NutRain).ShippedInThisBuild)
            || (system == WishSystem.PawMemory && ActivityCatalog.Get(ActivityId.PawMemory).ShippedInThisBuild)
            || (system == WishSystem.Expeditions && ActivityCatalog.Get(ActivityId.Expeditions).ShippedInThisBuild)
            || (system == WishSystem.Garden && ActivityCatalog.Get(ActivityId.Garden).ShippedInThisBuild);

        /// <summary>Index in <see cref="All"/> (number - 1), or -1 (the tutorial and unknown ids).</summary>
        public static int IndexOf(WishTemplateId id)
        {
            int index = (int)id - 1;
            return index >= 0 && index < All.Length ? index : -1;
        }

        public static bool TryGet(WishTemplateId id, out WishTemplate template)
        {
            if (id == WishTemplateId.Tutorial)
            {
                template = Tutorial;
                return true;
            }
            int index = IndexOf(id);
            if (index < 0)
            {
                template = default;
                return false;
            }
            template = All[index];
            return true;
        }

        /// <summary>Localization key: « wish.001 » … « wish.083 », « wish.t01 » (cached, no allocation).</summary>
        public static string TextKey(WishTemplateId id)
        {
            if (id == WishTemplateId.Tutorial)
                return Keys[0];
            int index = IndexOf(id);
            return index >= 0 ? Keys[index + 1] : string.Empty;
        }

        /// <summary>Refuge level tier of the amounts (section 0): 1 for NR 1-14, 2 for 15-39, 3 for 40-69, 4 for 70 and more.</summary>
        public static int TierOf(int refugeLevel) => refugeLevel >= 70 ? 4 : refugeLevel >= 40 ? 3 : refugeLevel >= 15 ? 2 : 1;

        /// <summary>
        /// Score wishes (section 2.3): the tier value, lowered to 80 % of the personal record rounded down to a multiple of 5
        /// when that is lower, never below P1. No record (0): the tier value.
        /// </summary>
        public static int ScoreTarget(int tierValue, int personalRecord, int p1)
        {
            if (personalRecord <= 0)
                return tierValue;
            int lowered = (int)(personalRecord * 0.8) / 5 * 5;
            return lowered < tierValue ? Math.Max(lowered, p1) : tierValue;
        }

        private static WishTemplate T(WishTemplateId id, WishSystem system, int weight, int seconds, int p1, int p2, int p3, int p4,
            WishParamKind paramKind = WishParamKind.None, bool timer = false, bool score = false) =>
            new WishTemplate(id, system, weight, seconds, timer, p1, p2, p3, p4, paramKind, score, IsShipped(system));

        private static string[] BuildKeys()
        {
            var keys = new string[All.Length + 1];
            keys[0] = "wish.t01";
            for (int i = 0; i < All.Length; i++)
                keys[i + 1] = "wish." + ((int)All[i].Id).ToString("000", System.Globalization.CultureInfo.InvariantCulture);
            return keys;
        }
    }
}
