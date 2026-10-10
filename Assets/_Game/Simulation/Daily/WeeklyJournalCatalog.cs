#nullable enable
using System.Globalization;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The slots of the weekly journal, in draw order (doc 01 Contenu, section 5.3: Collection, Activités, Rendez-vous,
    /// Minuteurs, Soin, Refuge, then Libre). Stable values (saved in <see cref="JournalGoalRecord.Slot"/>).
    /// </summary>
    public enum JournalSlot : byte
    {
        Collection = 0,
        Activities = 1,
        Rendezvous = 2,
        Timers = 3,
        Care = 4,
        Refuge = 5,
        Free = 6,
    }

    /// <summary>The 52 goals of the weekly journal (doc 01 Contenu, section 5.2). Stable values: the value is the JRN number.</summary>
    public enum JournalGoalId : ushort
    {
        None = 0,
        HatchEggs = 1,
        DiscoverVariants = 2,
        MasteryLevels = 3,
        NewSpecies = 4,
        PreciousEggs = 5,
        Newborns = 6,
        Stickers = 7,
        RareVisitors = 8,
        ActivityStars = 9,
        RewardedGames = 10,
        PersonalBests = 11,
        ActivityChallenges = 12,
        DifferentActivities = 13,
        ThreeStarGames = 14,
        CuddleMatchLevels = 15,
        HideAndSeekFinds = 16,
        NutsCaught = 17,
        TreasuresReeled = 18,
        Constellations = 19,
        FireflySongs = 20,
        Wishes = 21,
        DailyGifts = 22,
        CalendarDays = 23,
        Expeditions = 24,
        LongExpeditions = 25,
        PostcardPlaces = 26,
        Harvests = 27,
        SproutsWatered = 28,
        ThreeStarHarvests = 29,
        Dyes = 30,
        BondPoints = 31,
        TreatsGiven = 32,
        Grooming = 33,
        AnimalsTapped = 34,
        Outfits = 35,
        Upgrades = 36,
        DecorationsPlaced = 37,
        Expansions = 38,
        CakesBaked = 39,
        CakesGiven = 40,
        GardenPoints = 41,
        Photos = 42,
        SafariShots = 43,
        ContestEntry = 44,
        ContestVotes = 45,
        AlbumStars = 46,
        BubblesPopped = 47,
        GlideMeters = 48,
        MazeSeeds = 49,
        TilePuzzles = 50,
        PerfectHops = 51,
        MemoryPairs = 52,
    }

    /// <summary>
    /// One goal of the weekly journal: its slot, the system it belongs to (at most 2 goals of a system in a journal), its
    /// targets by quantity tier (P1 = refuge level 1 to 14, P2 = 15 to 39, P3 = 40 to 69, P4 = 70 and more, fixed at the
    /// draw), whether only rewarded games count, and whether this build has the system that makes it progress.
    /// </summary>
    public readonly struct JournalGoal
    {
        public readonly JournalGoalId Id;
        public readonly JournalSlot Slot;

        /// <summary>
        /// The system the « 2 per system » rule counts. <see cref="WishSystem.None"/> stands for the Play button as a whole
        /// (goals about any activity, JRN-009 to JRN-014); a goal about one activity counts for that activity (section 5.3).
        /// </summary>
        public readonly WishSystem System;

        public readonly int P1;
        public readonly int P2;
        public readonly int P3;
        public readonly int P4;

        /// <summary>Only rewarded games count (JRN-010 and JRN-014); every other goal counts practice games too (section 5.1).</summary>
        public readonly bool RewardedGamesOnly;

        /// <summary>The system exists in this build and feeds the goal; a goal not shipped is defined but never drawn.</summary>
        public readonly bool ShippedInThisBuild;

        public JournalGoal(JournalGoalId id, JournalSlot slot, WishSystem system, int p1, int p2, int p3, int p4, bool rewardedGamesOnly,
            bool shippedInThisBuild)
        {
            Id = id;
            Slot = slot;
            System = system;
            P1 = p1;
            P2 = p2;
            P3 = p3;
            P4 = p4;
            RewardedGamesOnly = rewardedGamesOnly;
            ShippedInThisBuild = shippedInThisBuild;
        }

        /// <summary>The target of a quantity tier (1 to 4).</summary>
        public int Target(int tier) => tier <= 1 ? P1 : tier == 2 ? P2 : tier == 3 ? P3 : P4;
    }

    /// <summary>
    /// The 52 goals of the weekly journal (doc 01 Contenu, section 5.2), in id order: <c>All[id - 1]</c>. Eligibility is in
    /// the comment of each line and lives in <see cref="WeeklyJournalService"/> for the goals this build ships. Text keys
    /// are « journal.001 » to « journal.052 » (section 0, localization keys).
    /// <para>T36 ships the goals whose systems exist (plan 1.7): JRN-001 to 005, 021 to 023, 031, 032, 034, 036 and 038;
    /// T38 adds JRN-033 with Grooming, T39 JRN-016 with Hide and Seek, T40 JRN-017 with Nut Drop. The others wait for their
    /// tasks (activities, expeditions and garden T43, albums T44, decoration T56...).</para>
    /// <para>Systems of the goals the doc marks « toujours » (plan, gap 18): hatching goals to SYS-01, discoveries and
    /// mastery to SYS-02, wishes, gifts and calendar to the rendez-vous, care to SYS-03, upgrades to SYS-04, expansions to
    /// SYS-06.</para>
    /// </summary>
    public static class WeeklyJournalCatalog
    {
        public const int Count = 52;

        public static readonly JournalGoal[] All =
        {
            // ---------------- Collection ----------------
            // JRN-001 « Fais éclore {n} œufs »: always.
            G(JournalGoalId.HatchEggs, JournalSlot.Collection, WishSystem.Hatching, 30, 60, 100, 150, shipped: true),
            // JRN-002 « Découvre {n} variantes »: at least n base variants not found yet in the unlocked eggs.
            G(JournalGoalId.DiscoverVariants, JournalSlot.Collection, WishSystem.DiscoveriesAndMastery, 2, 3, 3, 4, shipped: true),
            // JRN-003 « Gagne {n} niveaux de maîtrise »: always.
            G(JournalGoalId.MasteryLevels, JournalSlot.Collection, WishSystem.DiscoveriesAndMastery, 3, 5, 6, 8, shipped: true),
            // JRN-004 « Découvre une nouvelle espèce »: a normal species not discovered yet in an open biome.
            G(JournalGoalId.NewSpecies, JournalSlot.Collection, WishSystem.DiscoveriesAndMastery, 1, 1, 1, 1, shipped: true),
            // JRN-005 « Fais éclore {n} œufs précieux »: a Focus egg unlocked.
            G(JournalGoalId.PreciousEggs, JournalSlot.Collection, WishSystem.Hatching, 2, 3, 4, 5, shipped: true),
            // JRN-006 « Accueille {n} nouveau-nés »: breeding open.
            G(JournalGoalId.Newborns, JournalSlot.Collection, WishSystem.Breeding, 2, 3, 3, 4),
            // JRN-007 « Gagne {n} autocollants »: albums open.
            G(JournalGoalId.Stickers, JournalSlot.Collection, WishSystem.Albums, 3, 4, 5, 6),
            // JRN-008 « Dis bonjour à {n} visiteurs rares »: rare visitors open.
            G(JournalGoalId.RareVisitors, JournalSlot.Collection, WishSystem.RareVisitors, 3, 4, 5, 5),

            // ---------------- Activities ----------------
            // JRN-009 « Gagne {n} étoiles en activités »: the Play button.
            G(JournalGoalId.ActivityStars, JournalSlot.Activities, WishSystem.None, 20, 25, 30, 35),
            // JRN-010 « Joue {n} parties récompensées »: the Play button; rewarded games only.
            G(JournalGoalId.RewardedGames, JournalSlot.Activities, WishSystem.None, 10, 12, 15, 18, rewardedOnly: true),
            // JRN-011 « Bats {n} records »: 3 activities open.
            G(JournalGoalId.PersonalBests, JournalSlot.Activities, WishSystem.None, 2, 3, 3, 4),
            // JRN-012 « Réussis {n} défis d'activité »: at least n challenges not cleared yet.
            G(JournalGoalId.ActivityChallenges, JournalSlot.Activities, WishSystem.None, 1, 2, 2, 3),
            // JRN-013 « Joue à {n} activités différentes »: at least n activities open.
            G(JournalGoalId.DifferentActivities, JournalSlot.Activities, WishSystem.None, 3, 4, 5, 6),
            // JRN-014 « Obtiens 3 étoiles {n} fois »: the Play button; rewarded games only.
            G(JournalGoalId.ThreeStarGames, JournalSlot.Activities, WishSystem.None, 3, 5, 6, 8, rewardedOnly: true),
            // JRN-015 « Termine {n} niveaux de Match des câlins »: Cuddle Match open.
            G(JournalGoalId.CuddleMatchLevels, JournalSlot.Activities, WishSystem.CuddleMatch, 5, 8, 10, 12),
            // JRN-016 « Trouve {n} animaux à Cache-cache »: Hide and Seek open (T39: every animal found counts).
            G(JournalGoalId.HideAndSeekFinds, JournalSlot.Activities, WishSystem.HideAndSeek, 20, 30, 40, 50,
                shipped: ActivityCatalog.Get(ActivityId.HideAndSeek).ShippedInThisBuild),
            // JRN-017 « Attrape {n} noisettes »: Nut Rain open (T40: every nut caught counts, never a pinecone).
            G(JournalGoalId.NutsCaught, JournalSlot.Activities, WishSystem.NutRain, 150, 250, 350, 450,
                shipped: ActivityCatalog.Get(ActivityId.NutRain).ShippedInThisBuild),
            // JRN-018 « Remonte {n} trésors »: Treasure Fishing open.
            G(JournalGoalId.TreasuresReeled, JournalSlot.Activities, WishSystem.TreasureFishing, 40, 60, 80, 100),
            // JRN-019 « Dessine {n} constellations »: Constellations open.
            G(JournalGoalId.Constellations, JournalSlot.Activities, WishSystem.Constellations, 4, 6, 8, 10),
            // JRN-020 « Joue {n} morceaux au Rythme des lucioles »: Firefly Rhythm open.
            G(JournalGoalId.FireflySongs, JournalSlot.Activities, WishSystem.FireflyRhythm, 3, 4, 5, 6),

            // ---------------- Rendez-vous ----------------
            // JRN-021 « Réalise {n} souhaits »: always (wishes shown).
            G(JournalGoalId.Wishes, JournalSlot.Rendezvous, WishSystem.Rendezvous, 10, 12, 15, 15, shipped: true),
            // JRN-022 « Ouvre {n} cadeaux du jour »: always (daily gift shown).
            G(JournalGoalId.DailyGifts, JournalSlot.Rendezvous, WishSystem.Rendezvous, 4, 5, 5, 5, shipped: true),
            // JRN-023 « Avance de {n} cases au calendrier »: always (calendar shown).
            G(JournalGoalId.CalendarDays, JournalSlot.Rendezvous, WishSystem.Rendezvous, 4, 5, 5, 5, shipped: true),

            // ---------------- Timers ----------------
            // JRN-024 « Fais {n} expéditions »: expeditions open.
            G(JournalGoalId.Expeditions, JournalSlot.Timers, WishSystem.Expeditions, 5, 7, 9, 10),
            // JRN-025 « Fais {n} expéditions de 8 h ou plus »: expeditions open.
            G(JournalGoalId.LongExpeditions, JournalSlot.Timers, WishSystem.Expeditions, 2, 3, 4, 5),
            // JRN-026 « Rapporte des cartes postales de {n} destinations »: at least n destinations open.
            G(JournalGoalId.PostcardPlaces, JournalSlot.Timers, WishSystem.Expeditions, 2, 3, 4, 4),
            // JRN-027 « Récolte {n} plantes »: the Garden.
            G(JournalGoalId.Harvests, JournalSlot.Timers, WishSystem.Garden, 10, 15, 20, 25),
            // JRN-028 « Arrose {n} pousses »: the Garden.
            G(JournalGoalId.SproutsWatered, JournalSlot.Timers, WishSystem.Garden, 6, 10, 12, 15),
            // JRN-029 « Récolte {n} plantes à 3 étoiles »: the Garden.
            G(JournalGoalId.ThreeStarHarvests, JournalSlot.Timers, WishSystem.Garden, 3, 5, 6, 8),
            // JRN-030 « Récolte {n} teintures »: dyes open (Garden level 4).
            G(JournalGoalId.Dyes, JournalSlot.Timers, WishSystem.Garden, 3, 5, 6, 8),

            // ---------------- Care ----------------
            // JRN-031 « Gagne {n} points de lien »: always.
            G(JournalGoalId.BondPoints, JournalSlot.Care, WishSystem.CuddlesAndTreats, 80, 120, 160, 200, shipped: true),
            // JRN-032 « Donne {n} friandises »: the treat stand open (in the Shop).
            G(JournalGoalId.TreatsGiven, JournalSlot.Care, WishSystem.CuddlesAndTreats, 6, 9, 12, 15, shipped: true),
            // JRN-033 « Brosse {n} animaux »: Grooming open (T38: each grooming game finished counts one animal brushed).
            G(JournalGoalId.Grooming, JournalSlot.Care, WishSystem.Grooming, 3, 4, 5, 6,
                shipped: ActivityCatalog.Get(ActivityId.Grooming).ShippedInThisBuild),
            // JRN-034 « Touche {n} animaux »: always (touches counted for bond).
            G(JournalGoalId.AnimalsTapped, JournalSlot.Care, WishSystem.CuddlesAndTreats, 30, 45, 60, 75, shipped: true),
            // JRN-035 « Change {n} tenues »: 2 outfits or accessories owned.
            G(JournalGoalId.Outfits, JournalSlot.Care, WishSystem.Outfits, 3, 4, 5, 6),

            // ---------------- Refuge ----------------
            // JRN-036 « Achète {n} améliorations »: always (levels bought).
            G(JournalGoalId.Upgrades, JournalSlot.Refuge, WishSystem.Upgrades, 6, 10, 12, 15, shipped: true),
            // JRN-037 « Place {n} décorations »: decoration mode.
            G(JournalGoalId.DecorationsPlaced, JournalSlot.Refuge, WishSystem.Decoration, 2, 3, 4, 5),
            // JRN-038 « Agrandis {n} fois tes biomes »: at least n expansions still possible.
            G(JournalGoalId.Expansions, JournalSlot.Refuge, WishSystem.Expansions, 1, 2, 2, 3, shipped: true),
            // JRN-039 « Prépare {n} gâteaux »: the Pâtisserie.
            G(JournalGoalId.CakesBaked, JournalSlot.Refuge, WishSystem.Bakery, 3, 5, 6, 8),
            // JRN-040 « Offre {n} gâteaux »: the Pâtisserie.
            G(JournalGoalId.CakesGiven, JournalSlot.Refuge, WishSystem.Bakery, 2, 3, 4, 5),
            // JRN-041 « Gagne {n} points de Jardin »: the Garden.
            G(JournalGoalId.GardenPoints, JournalSlot.Refuge, WishSystem.Garden, 60, 100, 150, 200),

            // ---------------- Later lines of the table ----------------
            // JRN-042 « Prends {n} photos »: photo mode (Free slot).
            G(JournalGoalId.Photos, JournalSlot.Free, WishSystem.Photo, 5, 8, 10, 12),
            // JRN-043 « Réussis {n} photos au Safari »: Photo Safari.
            G(JournalGoalId.SafariShots, JournalSlot.Activities, WishSystem.Photo, 6, 9, 12, 15),
            // JRN-044 « Inscris un animal au Grand Concours »: Grand Contest (Free slot).
            G(JournalGoalId.ContestEntry, JournalSlot.Free, WishSystem.GrandContest, 1, 1, 1, 1),
            // JRN-045 « Vote {n} fois au Grand Concours »: Grand Contest with the server (Free slot).
            G(JournalGoalId.ContestVotes, JournalSlot.Free, WishSystem.GrandContest, 20, 30, 40, 50),
            // JRN-046 « Gagne {n} étoiles d'album »: albums.
            G(JournalGoalId.AlbumStars, JournalSlot.Collection, WishSystem.Albums, 3, 5, 6, 8),
            // JRN-047 « Éclate {n} bulles »: Bubbles.
            G(JournalGoalId.BubblesPopped, JournalSlot.Activities, WishSystem.Bubbles, 200, 300, 400, 500),
            // JRN-048 « Glisse {n} mètres »: Slide.
            G(JournalGoalId.GlideMeters, JournalSlot.Activities, WishSystem.Slide, 2000, 3000, 4000, 5000),
            // JRN-049 « Ramasse {n} graines au Labyrinthe »: Burrow Maze.
            G(JournalGoalId.MazeSeeds, JournalSlot.Activities, WishSystem.BurrowMaze, 60, 90, 120, 150),
            // JRN-050 « Termine {n} Taquins »: Tile Puzzle.
            G(JournalGoalId.TilePuzzles, JournalSlot.Activities, WishSystem.TilePuzzle, 3, 4, 5, 6),
            // JRN-051 « Réussis {n} atterrissages parfaits »: Lily Pad Hop.
            G(JournalGoalId.PerfectHops, JournalSlot.Activities, WishSystem.LilyHop, 10, 15, 20, 25),
            // JRN-052 « Trouve {n} paires au Memory »: Pawprint Memory.
            G(JournalGoalId.MemoryPairs, JournalSlot.Activities, WishSystem.PawMemory, 30, 40, 50, 60),
        };

        private static readonly string[] TextKeys = BuildTextKeys();

        public static bool TryGet(JournalGoalId id, out JournalGoal goal)
        {
            int index = (int)id - 1;
            if (index < 0 || index >= All.Length)
            {
                goal = default;
                return false;
            }
            goal = All[index];
            return true;
        }

        /// <summary>The localization key of a goal's text, « journal.001 » (built once, no allocation).</summary>
        public static string TextKey(JournalGoalId id)
        {
            int index = (int)id - 1;
            return index >= 0 && index < TextKeys.Length ? TextKeys[index] : string.Empty;
        }

        /// <summary>The quantity tier of a refuge level (doc 01 Contenu, section 0): 1 for 1 to 14, 2 for 15 to 39, 3 for 40 to 69, 4 from 70.</summary>
        public static int TierOf(int refugeLevel) => refugeLevel >= 70 ? 4 : refugeLevel >= 40 ? 3 : refugeLevel >= 15 ? 2 : 1;

        private static JournalGoal G(JournalGoalId id, JournalSlot slot, WishSystem system, int p1, int p2, int p3, int p4, bool shipped = false,
            bool rewardedOnly = false) =>
            new JournalGoal(id, slot, system, p1, p2, p3, p4, rewardedOnly, shipped);

        private static string[] BuildTextKeys()
        {
            var keys = new string[Count];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = "journal." + (i + 1).ToString("000", CultureInfo.InvariantCulture);
            return keys;
        }
    }
}
