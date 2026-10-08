#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>The three activity musics (doc 01 Contenu, section 12.0, J-MUS-ACT-01 to 03). Stable values.</summary>
    public enum ActivityMusic : byte
    {
        /// <summary>« Petite partie », 84 BPM: Grooming, Memory, Baking, Constellations, Tile Puzzle, Photo Safari.</summary>
        Calm = 0,

        /// <summary>« Pattes pressées », 112 BPM: Hide and Seek, Nut Drop, Fishing, Lily Hop, Glide, Bubbles, Burrow Maze.</summary>
        Lively = 1,

        /// <summary>« Câlins en série », 96 BPM: Cuddle Match, Grand Contest.</summary>
        Puzzle = 2,
    }

    /// <summary>
    /// The doc 01 conditions that are not a refuge level, a number of animals or a day (doc 01 Contenu, section 0). A
    /// system that does not exist yet reports false (or 0), so its activity stays locked.
    /// </summary>
    public enum ActivitySpecialUnlock : byte
    {
        None = 0,

        /// <summary>The Lake is unlocked (Treasure Fishing, Lily Pad Hop).</summary>
        Lake = 1,

        /// <summary>The Mountain is unlocked (Glide).</summary>
        Mountain = 2,

        /// <summary>The Axolotl is discovered (Bubbles).</summary>
        Axolotl = 3,

        /// <summary>The Garden reached level 3 (Baking; T43).</summary>
        GardenLevel3 = 4,

        /// <summary>The first rare night visitor arrived, or day 10 at the latest (Constellations).</summary>
        NightVisitorOrDay10 = 5,

        /// <summary>At least one accessory or outfit owned (Grand Contest, with level 24 and day 14).</summary>
        Outfit = 6,
    }

    /// <summary>
    /// What an unlock check needs, gathered once by the caller (no time-zone math inside the check): the refuge level, the
    /// animals, the day number, the refuge level service's verdicts for the activities that have a
    /// <see cref="RefugeFeature"/>, and the special conditions.
    /// </summary>
    public struct ActivityUnlockInput
    {
        public int RefugeLevel;
        public int Animals;

        /// <summary>Calendar day since install (day 1 = install day).</summary>
        public int DayNumber;

        /// <summary>
        /// <see cref="RefugeLevelService.IsFeatureUnlocked"/> results, one bit per <see cref="RefugeFeature"/> value; used
        /// only when <see cref="HasFeatures"/> (so the hub and the refuge level screen never disagree).
        /// </summary>
        public ulong Features;

        public bool HasFeatures;

        public bool LakeUnlocked;
        public bool MountainUnlocked;
        public bool AxolotlDiscovered;

        /// <summary>The Garden's level (0 until T43).</summary>
        public int GardenLevel;

        public bool NightVisitorSeen;
        public bool OutfitOwned;

        public bool Feature(RefugeFeature feature) => (Features & (1UL << (int)feature)) != 0;

        public void SetFeature(RefugeFeature feature, bool open)
        {
            ulong bit = 1UL << (int)feature;
            Features = open ? Features | bit : Features & ~bit;
            HasFeatures = true;
        }
    }

    /// <summary>
    /// One activity (doc « Mini-jeux &amp; activités » section 3; doc 01 Contenu sections 0 and 12): its doc number, keys,
    /// unlock, duration, base biome (the default theme), music, tickets, and whether this build ships it.
    /// </summary>
    public sealed class ActivityDefinition
    {
        public readonly ActivityId Id;
        public readonly int Slot;

        /// <summary>The doc 01 key part: activity.&lt;Key&gt;.name, challenge.&lt;Key&gt;.&lt;nn&gt;.</summary>
        public readonly string Key;

        /// <summary>doc 01 id « activity.&lt;key&gt; », and its content id (analytics 110 to 112 send it as the U field).</summary>
        public readonly string SourceId;
        public readonly ContentId ContentId;

        public readonly string NameKey;

        /// <summary>The refuge level service's function for this activity, or null (the doc 01 table alone decides).</summary>
        public readonly RefugeFeature? Feature;

        public readonly int RefugeLevel;
        public readonly int Animals;
        public readonly int Day;

        /// <summary>« jour 4 ou 15 animaux »: this many animals replace the day condition (0: none).</summary>
        public readonly int AnimalsInsteadOfDay;

        public readonly ActivitySpecialUnlock Special;

        /// <summary>Seconds of the free mode (0: no timer, an endless run or a meta activity).</summary>
        public readonly int Seconds;

        /// <summary>The doc's range (« 45 à 90 s selon la grille », « 30 à 90 s »).</summary>
        public readonly int MinSeconds;
        public readonly int MaxSeconds;

        /// <summary>The biome of the default theme (doc 01, section 12.0 « Thème par biome »).</summary>
        public readonly ContentId BaseBiome;

        public readonly ActivityMusic Music;

        /// <summary>A rewarded game costs a ticket (Garden and Expeditions are timers; the Grand Contest is weekly).</summary>
        public readonly bool UsesTickets;

        /// <summary>A meta activity with its own screen and rules (Expeditions, Garden): not played through the runner.</summary>
        public readonly bool Meta;

        /// <summary>The first rewarded game is offered with full rewards (Grooming, doc 01 A15).</summary>
        public readonly bool FirstGameFree;

        /// <summary>Doc Mini-jeux wave: 1 and 2 soft launch, 3 V1; 0 for the test activity.</summary>
        public readonly byte Wave;

        /// <summary>This build has the activity's controller and content (flips true in each activity's task).</summary>
        public readonly bool ShippedInThisBuild;

        private readonly string[] _challengeIds;

        public ActivityDefinition(ActivityId id, string key, RefugeFeature? feature, int refugeLevel, int animals, int day,
            int animalsInsteadOfDay, ActivitySpecialUnlock special, int seconds, int minSeconds, int maxSeconds, string baseBiome,
            ActivityMusic music, bool usesTickets, bool meta, bool firstGameFree, byte wave, bool shipped)
        {
            Id = id;
            Slot = ActivityCatalog.SlotOf(id);
            Key = key;
            SourceId = "activity." + key;
            ContentId = ContentId.FromSource(SourceId);
            NameKey = SourceId + ".name";
            Feature = feature;
            RefugeLevel = refugeLevel;
            Animals = animals;
            Day = day;
            AnimalsInsteadOfDay = animalsInsteadOfDay;
            Special = special;
            Seconds = seconds;
            MinSeconds = minSeconds;
            MaxSeconds = maxSeconds;
            BaseBiome = ContentId.FromSource(baseBiome);
            Music = music;
            UsesTickets = usesTickets;
            Meta = meta;
            FirstGameFree = firstGameFree;
            Wave = wave;
            ShippedInThisBuild = shipped;
            _challengeIds = new string[ActivityCatalog.ChallengesPerActivity];
            for (int i = 0; i < _challengeIds.Length; i++)
                _challengeIds[i] = "challenge." + key + "." + (i + 1).ToString("00");
        }

        /// <summary>challenge.&lt;key&gt;.&lt;nn&gt; (nn from 01); its texts are the id + « .name » and « .goal ».</summary>
        public string ChallengeId(int challenge) =>
            (uint)challenge < (uint)_challengeIds.Length ? _challengeIds[challenge] : string.Empty;

        /// <summary>
        /// The doc 01 opening rule (section 0). The activities with a <see cref="RefugeFeature"/> follow the refuge level
        /// service when the input carries its verdicts, so the hub and the refuge level screen agree; the others, and a
        /// caller without the progression services, use the table (level, animals, day or its animal alternative, the
        /// special condition). No allocation.
        /// </summary>
        public bool IsUnlocked(in ActivityUnlockInput input)
        {
            if (Feature.HasValue && input.HasFeatures)
                return input.Feature(Feature.Value) && SpecialMet(in input);
            if (input.RefugeLevel < RefugeLevel || input.Animals < Animals)
                return false;
            bool dayMet = input.DayNumber >= Day || (AnimalsInsteadOfDay > 0 && input.Animals >= AnimalsInsteadOfDay);
            return dayMet && SpecialMet(in input);
        }

        private bool SpecialMet(in ActivityUnlockInput input)
        {
            switch (Special)
            {
                case ActivitySpecialUnlock.Lake: return input.LakeUnlocked;
                case ActivitySpecialUnlock.Mountain: return input.MountainUnlocked;
                case ActivitySpecialUnlock.Axolotl: return input.AxolotlDiscovered;
                case ActivitySpecialUnlock.GardenLevel3: return input.GardenLevel >= 3;
                case ActivitySpecialUnlock.NightVisitorOrDay10: return input.NightVisitorSeen || input.DayNumber >= 10;
                case ActivitySpecialUnlock.Outfit: return input.OutfitOwned;
                default: return true;
            }
        }
    }

    /// <summary>
    /// The 18 activities and the framework's test activity (doc 01 Contenu: section 0 « Déblocage des systèmes », the head
    /// line of each S-ACT section, section 12.0 for the musics). Static, built once; lookups never allocate.
    /// </summary>
    public static class ActivityCatalog
    {
        public const int ActivityCount = 18;

        /// <summary>The 18 activities and the test activity.</summary>
        public const int SlotCount = ActivityCount + 1;

        public const int TestSlot = ActivityCount;
        public const int ChallengesPerActivity = 10;

        public static readonly ContentId Meadow = ContentId.FromSource("biome.grassland");
        public static readonly ContentId Forest = ContentId.FromSource("biome.forest");
        public static readonly ContentId Lake = ContentId.FromSource("biome.lake");
        public static readonly ContentId Mountain = ContentId.FromSource("biome.mountain");
        public static readonly ContentId Axolotl = ContentId.FromSource("species.axolotl");

        private const string M = "biome.grassland";

        /// <summary>By slot: Grooming (slot 0) to Grand Contest (slot 17), then Test.</summary>
        private static readonly ActivityDefinition[] Definitions =
        {
            // id, key, feature, level, animals, day, animals instead of day, special, seconds, min, max, biome, music, tickets, meta, free first, wave, shipped
            new ActivityDefinition(ActivityId.Grooming, "grooming", RefugeFeature.Grooming, 3, 5, 0, 0, ActivitySpecialUnlock.None,
                45, 45, 45, M, ActivityMusic.Calm, true, false, true, 1, false),
            new ActivityDefinition(ActivityId.HideAndSeek, "hide_seek", RefugeFeature.HideAndSeek, 4, 8, 0, 0, ActivitySpecialUnlock.None,
                60, 60, 60, M, ActivityMusic.Lively, true, false, false, 1, false),
            new ActivityDefinition(ActivityId.NutRain, "nut_drop", RefugeFeature.NutRain, 10, 0, 4, 15, ActivitySpecialUnlock.None,
                60, 60, 90, M, ActivityMusic.Lively, true, false, false, 1, false),
            new ActivityDefinition(ActivityId.PawMemory, "memory", RefugeFeature.PawMemory, 11, 0, 5, 0, ActivitySpecialUnlock.None,
                60, 45, 90, M, ActivityMusic.Calm, true, false, false, 1, false),
            new ActivityDefinition(ActivityId.Expeditions, "expeditions", RefugeFeature.Expeditions, 5, 8, 0, 0, ActivitySpecialUnlock.None,
                0, 0, 0, M, ActivityMusic.Calm, false, true, false, 1, false),
            new ActivityDefinition(ActivityId.Garden, "garden", RefugeFeature.Garden, 8, 0, 3, 0, ActivitySpecialUnlock.None,
                0, 0, 0, M, ActivityMusic.Calm, false, true, false, 1, false),
            new ActivityDefinition(ActivityId.TreasureFishing, "treasure_fishing", null, 0, 0, 0, 0, ActivitySpecialUnlock.Lake,
                60, 60, 60, "biome.lake", ActivityMusic.Lively, true, false, false, 2, false),
            new ActivityDefinition(ActivityId.LilyHop, "lily_hop", null, 0, 0, 0, 0, ActivitySpecialUnlock.Lake,
                0, 30, 90, "biome.lake", ActivityMusic.Lively, true, false, false, 2, false),
            new ActivityDefinition(ActivityId.Bakery, "baking", null, 0, 0, 0, 0, ActivitySpecialUnlock.GardenLevel3,
                60, 60, 60, M, ActivityMusic.Calm, true, false, false, 2, false),
            new ActivityDefinition(ActivityId.PhotoSafari, "photo_safari", RefugeFeature.PhotoSafari, 16, 0, 6, 0, ActivitySpecialUnlock.None,
                90, 90, 90, M, ActivityMusic.Calm, true, false, false, 2, false),
            new ActivityDefinition(ActivityId.CuddleMatch, "cuddle_match", null, 14, 0, 0, 0, ActivitySpecialUnlock.None,
                90, 60, 180, M, ActivityMusic.Puzzle, true, false, false, 3, false),
            new ActivityDefinition(ActivityId.Slide, "glide", null, 0, 0, 0, 0, ActivitySpecialUnlock.Mountain,
                0, 30, 90, "biome.mountain", ActivityMusic.Lively, true, false, false, 3, false),
            new ActivityDefinition(ActivityId.Constellations, "constellations", null, 0, 0, 0, 0, ActivitySpecialUnlock.NightVisitorOrDay10,
                60, 60, 60, "biome.mountain", ActivityMusic.Calm, true, false, false, 3, false),
            new ActivityDefinition(ActivityId.FireflyRhythm, "firefly_rhythm", null, 22, 0, 12, 0, ActivitySpecialUnlock.None,
                90, 60, 90, "biome.forest", ActivityMusic.Lively, true, false, false, 3, false),
            new ActivityDefinition(ActivityId.Bubbles, "bubbles", null, 0, 0, 0, 0, ActivitySpecialUnlock.Axolotl,
                60, 60, 60, "biome.lake", ActivityMusic.Lively, true, false, false, 2, false),
            new ActivityDefinition(ActivityId.TilePuzzle, "tile_puzzle", RefugeFeature.TilePuzzle, 18, 0, 8, 0, ActivitySpecialUnlock.None,
                60, 60, 120, M, ActivityMusic.Calm, true, false, false, 2, false),
            new ActivityDefinition(ActivityId.BurrowMaze, "burrow_maze", RefugeFeature.BurrowMaze, 20, 0, 10, 0, ActivitySpecialUnlock.None,
                60, 60, 60, "biome.forest", ActivityMusic.Lively, true, false, false, 2, false),
            new ActivityDefinition(ActivityId.GrandContest, "grand_contest", null, 24, 0, 14, 0, ActivitySpecialUnlock.Outfit,
                0, 0, 0, M, ActivityMusic.Puzzle, false, false, false, 3, false),
            new ActivityDefinition(ActivityId.Test, "test", null, 1, 0, 0, 0, ActivitySpecialUnlock.None,
                30, 30, 30, M, ActivityMusic.Calm, true, false, false, 0, false),
        };

        /// <summary>The record slot of an activity: id − 1 for the 18, <see cref="TestSlot"/> for the test one, −1 otherwise.</summary>
        public static int SlotOf(ActivityId id)
        {
            int value = (int)id;
            if (value >= 1 && value <= ActivityCount)
                return value - 1;
            return id == ActivityId.Test ? TestSlot : -1;
        }

        public static ActivityId IdOfSlot(int slot) =>
            slot >= 0 && slot < ActivityCount ? (ActivityId)(slot + 1) : slot == TestSlot ? ActivityId.Test : ActivityId.None;

        /// <summary>The definition by slot (0 to <see cref="SlotCount"/> − 1).</summary>
        public static ActivityDefinition AtSlot(int slot) => Definitions[slot];

        public static bool TryGet(ActivityId id, out ActivityDefinition definition)
        {
            int slot = SlotOf(id);
            if (slot < 0)
            {
                definition = null!;
                return false;
            }
            definition = Definitions[slot];
            return true;
        }

        public static ActivityDefinition Get(ActivityId id) => Definitions[SlotOf(id)];
    }
}
