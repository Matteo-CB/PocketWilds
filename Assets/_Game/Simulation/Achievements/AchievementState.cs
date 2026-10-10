#nullable enable

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T44B « Succès, titres, cadres, poses » (doc 01 Succès, sections 1 to 5): everything the achievements and the profile
    /// items remember. Plain data, fixed-size arrays allocated once, indexed like the generated tables (an id is appended,
    /// never reused). Saved in section 21 (schema 15). Never touched by the Renewal: the counters run for the account's life;
    /// only the « Run* » values restart with each run, and the per-run achievements keep their best run.
    /// </summary>
    public sealed class AchievementState
    {
        public const int Achievements = AchievementData.Count;
        public const int Titles = ProfileData.TitleCount;
        public const int Frames = ProfileData.FrameCount;
        public const int Poses = ProfileData.PoseCount;
        public const int TitleBytes = (Titles + 7) / 8;
        public const int SpeciesSlots = ProfileData.SpeciesCount;

        /// <summary>Each achievement's counter (cumulated since the account began; the best run for the per-run ones).</summary>
        public long[] Progress = new long[Achievements];

        /// <summary>The highest tier reached and the highest claimed (<see cref="AchievementTier"/> values).</summary>
        public byte[] Reached = new byte[Achievements];

        public byte[] Claimed = new byte[Achievements];

        // ---- The current run (from the last Renewal) ----

        public long RunHatches;
        public long RunLightLevels;

        /// <summary>« Plus haut » (ACH-192) already counted in this run.</summary>
        public bool RunProductionBeaten;

        /// <summary>The best Essence a Renewal gave (ACH-191: each Renewal beating it counts).</summary>
        public long BestRenewalEssence;

        // ---- The local day (yyyymmdd) ----

        public int DayKey;
        public int DayHatches;
        public int DayWishes;

        // ---- Seasons and the weekly journal ----

        /// <summary>The last refuge season counted (year × 4 + season), ACH-199.</summary>
        public int SeasonKey;

        /// <summary>The weekly journal counted (its start day), its goals reached already counted, its completion counted.</summary>
        public int JournalWeek;

        public int JournalGoalsCounted;
        public bool JournalCompleteCounted;

        // ---- Distinct things, as bit sets ----

        /// <summary>13 bits: biomes whose eggs hatched (ACH-087).</summary>
        public uint HatchBiomes;

        /// <summary>6 bits: rarities hatched, Common to Fabulous (ACH-094).</summary>
        public uint HatchRarities;

        /// <summary>18 bits: activities with a star (ACH-114).</summary>
        public uint ActivitiesStarred;

        /// <summary>Expedition destinations visited: 13 biome bits, then the event destinations (ACH-133).</summary>
        public uint Destinations;

        /// <summary>Garden plants harvested (ACH-141).</summary>
        public uint PlantsHarvested;

        /// <summary>Poses used in photos (ACH-174).</summary>
        public ulong PosesUsed;

        /// <summary>Per species index, 6 trait bits found (ACH-011).</summary>
        public byte[] SpeciesTraits = new byte[SpeciesSlots];

        // ---- Distinct ids, capped at the Star threshold (counting past it changes nothing) ----

        public uint[] AnimalsPlayed = new uint[AchievementData.StarOf(AchievementCounter.AnimalsPlayed)];
        public int AnimalsPlayedCount;
        public uint[] AnimalsWished = new uint[AchievementData.StarOf(AchievementCounter.AnimalsWished)];
        public int AnimalsWishedCount;
        public uint[] AnimalsPetted = new uint[AchievementData.StarOf(AchievementCounter.AnimalsPetted)];
        public int AnimalsPettedCount;
        public uint[] Accessories = new uint[AchievementData.StarOf(AchievementCounter.AccessoriesEquipped)];
        public int AccessoriesCount;

        // ---- Profile items ----

        /// <summary>The starting title and frame were given (at creation, or on the first load of an older account).</summary>
        public bool StartGiven;

        public byte[] TitlesOwned = new byte[TitleBytes];
        public byte[] TitlesUnseen = new byte[TitleBytes];
        public ulong FramesOwned;
        public ulong FramesUnseen;
        public ulong PosesOwned;
        public ulong PosesUnseen;

        /// <summary>The title and frame shown (−1: none).</summary>
        public short SelectedTitle = -1;

        public short SelectedFrame = -1;

        public static bool Bit(byte[] bits, int index) => (bits[index >> 3] & (1 << (index & 7))) != 0;

        public static void SetBit(byte[] bits, int index, bool value)
        {
            if (value)
                bits[index >> 3] |= (byte)(1 << (index & 7));
            else
                bits[index >> 3] &= (byte)~(1 << (index & 7));
        }

        /// <summary>Back to a new account (tests, the debug panel).</summary>
        public void Clear()
        {
            System.Array.Clear(Progress, 0, Progress.Length);
            System.Array.Clear(Reached, 0, Reached.Length);
            System.Array.Clear(Claimed, 0, Claimed.Length);
            RunHatches = 0;
            RunLightLevels = 0;
            RunProductionBeaten = false;
            BestRenewalEssence = 0;
            DayKey = 0;
            DayHatches = 0;
            DayWishes = 0;
            SeasonKey = 0;
            JournalWeek = 0;
            JournalGoalsCounted = 0;
            JournalCompleteCounted = false;
            HatchBiomes = 0;
            HatchRarities = 0;
            ActivitiesStarred = 0;
            Destinations = 0;
            PlantsHarvested = 0;
            PosesUsed = 0;
            System.Array.Clear(SpeciesTraits, 0, SpeciesTraits.Length);
            AnimalsPlayedCount = 0;
            AnimalsWishedCount = 0;
            AnimalsPettedCount = 0;
            AccessoriesCount = 0;
            StartGiven = false;
            System.Array.Clear(TitlesOwned, 0, TitlesOwned.Length);
            System.Array.Clear(TitlesUnseen, 0, TitlesUnseen.Length);
            FramesOwned = 0;
            FramesUnseen = 0;
            PosesOwned = 0;
            PosesUnseen = 0;
            SelectedTitle = -1;
            SelectedFrame = -1;
        }
    }
}
