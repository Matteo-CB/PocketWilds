#nullable enable
using System.Collections.Generic;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// What an achievement counts (doc 01 Succès, section 2). Each (counter, parameter) pair belongs to one achievement; the
    /// parameter is a biome index (the four biome counters) or an activity number (<see cref="ActivityProgress"/>). Values
    /// are never saved: the save keeps progress by achievement index.
    /// </summary>
    public enum AchievementCounter : byte
    {
        // Collection
        SpeciesDiscovered, VariantsDiscovered, BaseSetsComplete, SecretVariants, TraitTiny, TraitGiant, TraitShiny,
        TraitGolden, TraitAlbino, TraitMelanistic, SpeciesTraitPairs, RarityRare, RarityEpic, RarityLegendary,
        RarityFabulous, VisitorsKept, BiomeCodexComplete, CodexPercent, AnimalsNamed, Favorites,

        // Biomes (parameter: the biome index)
        BiomeHatches, BiomeVariants, BiomeExpeditions, BiomeBonds,

        // Mastery
        SpeciesMastery5, SpeciesMastery10, SpeciesMastery20, SpeciesMastery30, MasteryMilestones, MasteryVariants,
        BiomeMastery12, BiomeMastery36, EventSpeciesMastery10, MasteryLevels,

        // Hatching
        Hatches, FocusHatches, DiscoveryHatches, EventEggs, HatchBiomes, NewbornTaps, BreedingHatches, BreedingMutations,
        ThirdGeneration, BestDayHatches, FullEventEggs, HatchRarities,

        // Activities (ActivityProgress: parameter the activity number)
        ActivityProgress, ActivityStars, ChallengesThreeStars, PersonalBests, ActivitiesStarred, PracticeGames,
        PerfectActivities, ThreeStarGames, AnimalsPlayed, ActivityBondPoints, ActivityStickers,

        // Daily rendez-vous
        DailyGifts, WishesGranted, CalendarDays, CalendarsComplete, JournalsComplete, JournalGoals, AnimalsWished,
        CakeWishes, VisitorsTapped, FullWishDays,

        // Expeditions and Garden
        ExpeditionsDone, ExpeditionHours, Destinations, LongExpeditions, TeamExpeditions, ExpeditionDiscoveryEggs,
        SeedsPlanted, FlowersHarvested, FruitsHarvested, DyesHarvested, PlantsHarvested, SproutsWatered,

        // Albums
        StickersOwned, PagesComplete, BiomeAlbumsComplete, EventPagesComplete, AlbumStarsEarned, StickersPicked,
        RecordStickers, BiomePagesHalf, SeasonPagesComplete, ExpeditionStickers,

        // Decoration
        DecorationsOwned, DecorationsPlaced, CommonDecorations, ProgressionDecorations, LandmarksPlaced, CozyAreas, Naps,
        Sits, Meals, Perches, Recolors, SouvenirsPlaced,

        // Bonds and companions
        Bond1Animals, Bond3Animals, Bond5Animals, BondPoints, Pets, AnimalsPetted, AccessoriesEquipped, FullOutfits, Photos,
        PosesUsed, TreatsGiven, EventAnimalsBond5,

        // Events
        EventsJoined, EventPathTiers, FeaturedAnimals, FreePathsComplete, EventCurrency, EventQuests,
        FeaturedChallengeStars, ChallengeOutfits, SharedGoals, EventVariants,

        // Renewal and Essence
        Renewals, EssenceLevels, BiomesAtRenewal, RunHatches, EssenceRecords, ProductionBeaten, EssenceMilestones,
        RunLightLevels,

        // Refuge
        RefugeLevel, BiomesUnlocked, ZonesOpen, LightLevels, RefugeSeasons, JournalPages,
    }

    /// <summary>One achievement row of the generated table.</summary>
    public readonly struct AchievementRow
    {
        public readonly string Key;
        public readonly AchievementCategory Category;
        public readonly AchievementCounter Counter;
        public readonly int Param;
        public readonly long Bronze;
        public readonly long Silver;
        public readonly long Gold;
        public readonly long Star;

        /// <summary>The Gold tier's biome decoration (decor.achievement.{biome}.{17..20}), empty without.</summary>
        public readonly string GoldDecoration;

        /// <summary>The Star tier's title, frame or pose key, empty without.</summary>
        public readonly string StarExtra;

        public AchievementRow(string key, AchievementCategory category, AchievementCounter counter, int param, long bronze,
            long silver, long gold, long star, string goldDecoration, string starExtra)
        {
            Key = key;
            Category = category;
            Counter = counter;
            Param = param;
            Bronze = bronze;
            Silver = silver;
            Gold = gold;
            Star = star;
            GoldDecoration = goldDecoration;
            StarExtra = starExtra;
        }
    }

    /// <summary>
    /// T44B: the 200 achievements (generated from doc 01 Succès, section 2 by Tools/Data/pw_profile_data.py into
    /// ProfileData.Generated.cs) and their lookups. Every lookup is built once; reads never allocate.
    /// </summary>
    public static partial class AchievementData
    {
        /// <summary>Gems by tier (doc 01 Succès, section 1 « Paliers et récompenses »): 85 per achievement.</summary>
        public static int GemsFor(AchievementTier tier) =>
            tier == AchievementTier.Bronze ? 5 : tier == AchievementTier.Silver ? 10 : tier == AchievementTier.Gold ? 20 : tier == AchievementTier.Star ? 50 : 0;

        /// <summary>The largest counter parameter (an activity number, 18) plus one.</summary>
        public const int ParamSlots = 19;

        private static readonly int[] ByCounter;
        private static readonly ContentId[] Decorations;

        // A static constructor runs after the field initializers of every part (the generated rows): the derived tables are
        // built here, never in a field initializer whose order across partial files is not defined.
        static AchievementData()
        {
            ByCounter = BuildByCounter();
            Decorations = BuildDecorations();
        }

        public static ref readonly AchievementRow Row(int index) => ref Rows[index];

        /// <summary>The achievement following a counter (−1 when none does).</summary>
        public static int IndexOf(AchievementCounter counter, int param = 0)
        {
            if ((uint)param >= ParamSlots)
                return -1;
            int slot = (int)counter * ParamSlots + param;
            return (uint)slot < (uint)ByCounter.Length ? ByCounter[slot] : -1;
        }

        public static int IndexOf(string key)
        {
            // achievement.NNN: the index is the number minus one.
            if (key == null || key.Length != 15 || !key.StartsWith("achievement.", System.StringComparison.Ordinal))
                return -1;
            int n = 0;
            for (int i = 12; i < 15; i++)
            {
                char c = key[i];
                if (c < '0' || c > '9')
                    return -1;
                n = n * 10 + (c - '0');
            }
            return n >= 1 && n <= Count ? n - 1 : -1;
        }

        /// <summary>The Gold decoration as a content id (None without).</summary>
        public static ContentId GoldDecoration(int index) => Decorations[index];

        /// <summary>The Star threshold of the achievement following a counter (the size of its distinct lists).</summary>
        public static int StarOf(AchievementCounter counter, int param = 0)
        {
            int index = IndexOf(counter, param);
            return index >= 0 ? (int)Rows[index].Star : 0;
        }

        private static int[] BuildByCounter()
        {
            int counters = 0;
            for (int i = 0; i < Rows.Length; i++)
                if ((int)Rows[i].Counter + 1 > counters)
                    counters = (int)Rows[i].Counter + 1;
            var map = new int[counters * ParamSlots];
            for (int i = 0; i < map.Length; i++)
                map[i] = -1;
            for (int i = 0; i < Rows.Length; i++)
                map[(int)Rows[i].Counter * ParamSlots + Rows[i].Param] = i;
            return map;
        }

        private static ContentId[] BuildDecorations()
        {
            var ids = new ContentId[Rows.Length];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = string.IsNullOrEmpty(Rows[i].GoldDecoration) ? ContentId.None : ContentId.FromSource(Rows[i].GoldDecoration);
            return ids;
        }
    }

    /// <summary>Where a title, frame or pose comes from (doc 01 Succès, sections 3 to 5).</summary>
    public enum ProfileSource : byte
    {
        /// <summary>Given when the account is created (title.little_paw, frame.soft_round).</summary>
        Start = 0,

        /// <summary>An achievement's Star tier, claimed (Subject: the achievement number 1 to 200).</summary>
        Achievement = 1,

        /// <summary>An event page complete in the albums (Subject: the event number 101 to 112).</summary>
        EventPage = 2,

        /// <summary>An activity page complete in the albums (Subject: the activity number 1 to 18).</summary>
        ActivityPage = 3,

        /// <summary>A companion's bond (Value: 25 for its title, 20 for its pose; Subject: A-SP-101 to 113).</summary>
        Companion = 4,

        /// <summary>A refuge level (Value).</summary>
        RefugeLevel = 5,

        /// <summary>A biome's mastery level (Value; Subject: the biome index).</summary>
        BiomeMastery = 6,

        /// <summary>A species' mastery level (Value 30 or 100; Subject: the species index of <see cref="ProfileData"/>).</summary>
        SpeciesMastery = 7,

        /// <summary>A Refuge Pass season (Subject: the season; titles at free level 40, poses Premium+).</summary>
        PassSeason = 8,

        /// <summary>Moments on the journal's Moments page (Value: how many).</summary>
        JournalMoments = 9,

        /// <summary>A big event's free path tier (Value; Subject: the event number).</summary>
        EventTrack = 10,

        /// <summary>A big event's shared goal, or the personal goal standing in for it (Subject: the event number).</summary>
        EventGoal = 11,

        /// <summary>Photo mode opening (the 8 base poses).</summary>
        PhotoMode = 12,
    }

    /// <summary>One title, frame or pose row of the generated tables.</summary>
    public readonly struct ProfileRow
    {
        public readonly string Key;
        public readonly ProfileSource Source;
        public readonly int Value;
        public readonly int Subject;

        /// <summary>The gallery order (1-based, the doc's order).</summary>
        public readonly int Order;

        public ProfileRow(string key, ProfileSource source, int value, int subject, int order)
        {
            Key = key;
            Source = source;
            Value = value;
            Subject = subject;
            Order = order;
        }
    }

    /// <summary>
    /// T44B: the 322 titles, 42 frames and 47 poses (generated from doc 01 Succès, sections 3 to 5) and the 90 species that
    /// have mastery titles (78 of the biomes, 6 per biome in biome order, then the 12 event animals).
    /// </summary>
    public static partial class ProfileData
    {
        /// <summary>The first event species: indexes from here are the 12 event animals.</summary>
        public const int FirstEventSpecies = 78;

        public const string StartingTitle = "title.little_paw";
        public const string StartingFrame = "frame.soft_round";

        private static readonly ContentId[] SpeciesIds;
        private static readonly Dictionary<ContentId, int> SpeciesIndexes;
        private static readonly Dictionary<string, int>[] KeyIndexes;

        // After the generated rows (see AchievementData's static constructor).
        static ProfileData()
        {
            SpeciesIds = BuildSpecies();
            SpeciesIndexes = BuildSpeciesIndexes();
            KeyIndexes = BuildKeyIndexes();
        }

        public static int Count(ProfileItemKind kind) =>
            kind == ProfileItemKind.Title ? TitleRows.Length : kind == ProfileItemKind.Frame ? FrameRows.Length : PoseRows.Length;

        public static ref readonly ProfileRow Row(ProfileItemKind kind, int index)
        {
            if (kind == ProfileItemKind.Title)
                return ref TitleRows[index];
            if (kind == ProfileItemKind.Frame)
                return ref FrameRows[index];
            return ref PoseRows[index];
        }

        /// <summary>The item's index by key (−1 when unknown).</summary>
        public static int IndexOf(ProfileItemKind kind, string key) =>
            key != null && KeyIndexes[(int)kind].TryGetValue(key, out int index) ? index : -1;

        public static ContentId Species(int index) => SpeciesIds[index];

        public static string SpeciesKey(int index) => SpeciesKeys[index];

        /// <summary>The species' index (−1 when it has no mastery title: a companion, a visitor, an unknown id).</summary>
        public static int SpeciesIndex(ContentId species) => SpeciesIndexes.TryGetValue(species, out int index) ? index : -1;

        public static bool IsEventSpecies(ContentId species) => SpeciesIndex(species) >= FirstEventSpecies;

        private static ContentId[] BuildSpecies()
        {
            var ids = new ContentId[SpeciesKeys.Length];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = ContentId.FromSource(SpeciesKeys[i]);
            return ids;
        }

        private static Dictionary<ContentId, int> BuildSpeciesIndexes()
        {
            var map = new Dictionary<ContentId, int>(SpeciesIds.Length);
            for (int i = 0; i < SpeciesIds.Length; i++)
                map[SpeciesIds[i]] = i;
            return map;
        }

        private static Dictionary<string, int>[] BuildKeyIndexes()
        {
            var maps = new Dictionary<string, int>[3];
            for (int k = 0; k < 3; k++)
            {
                var kind = (ProfileItemKind)k;
                int n = Count(kind);
                maps[k] = new Dictionary<string, int>(n, System.StringComparer.Ordinal);
                for (int i = 0; i < n; i++)
                    maps[k][Row(kind, i).Key] = i;
            }
            return maps;
        }
    }
}
