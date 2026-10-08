using PocketWilds.Simulation;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T37 text keys of the activities (doc « Mini-jeux & activités » sections 1 and 2, doc 01 Contenu sections 0, A15 and
    /// 12.0, doc 06 screens 029 and 030, doc 05 Sensations sheet 20; Writing Guide: short, warm, no em dash, never a pressure
    /// word, real time never a ticking countdown). The texts live in the shipped localization tables, written by the UI
    /// pipeline from Editor/UI/ActivityTexts.cs. Counted texts have « key.one » and « key.other » forms, read with
    /// ILocalization.GetPlural. Placeholders: {n} and {total} numbers, {h} hours and {m} minutes (two digits after hours),
    /// {price} a price in Gems, {goal} a challenge goal already written, {first} and {second} two unlock conditions.
    /// </summary>
    public static class ActivityText
    {
        // HUD rail (doc 01 A15: the Play icon on the right rail with the tickets owned).
        public const string HudPlay = "hud.play";

        // Hub (screen 029).
        public const string HubTitle = "activity.hub.title";
        public const string HubPlay = "activity.hub.play";
        public const string HubFree = "activity.hub.free";
        public const string HubNew = "activity.hub.new";
        public const string HubBest = "activity.hub.best";
        public const string HubChallenges = "activity.hub.challenges";
        public const string HubChallengePick = "activity.hub.challenge_pick";
        public const string HubChallengeGoal = "activity.hub.challenge_goal";
        public const string HubChallengeLocked = "activity.hub.challenge_locked";
        public const string HubTheme = "activity.hub.theme";

        /// <summary>« Défi {n} »: a challenge by its number (1 to 10).</summary>
        public const string Challenge = "activity.challenge";

        // Tickets (doc Mini-jeux section 2: real time to the next free ticket, never a pressure).
        public const string TicketsNextMinutes = "activity.tickets.next_m";
        public const string TicketsNextHours = "activity.tickets.next_h";
        public const string TicketsNextHoursMinutes = "activity.tickets.next_hm";
        public const string TicketsFull = "activity.tickets.full";

        // Practice (free and unlimited, no reward: the fun is never blocked).
        public const string Practice = "activity.practice";
        public const string PracticeLine = "activity.practice.line";

        // Unlock hints of locked cards (doc 01 section 0), formatted by the presenter and shown as given.
        public const string UnlockRefugeLevel = "activity.unlock.refuge_level";
        public const string UnlockDay = "activity.unlock.day";
        public const string UnlockAnimals = "activity.unlock.animals";
        public const string UnlockAnd = "activity.unlock.and";
        public const string UnlockOr = "activity.unlock.or";
        public const string UnlockLake = "activity.unlock.lake";
        public const string UnlockMountain = "activity.unlock.mountain";
        public const string UnlockAxolotl = "activity.unlock.axolotl";
        public const string UnlockGardenLevel = "activity.unlock.garden_level";
        public const string UnlockNightVisitor = "activity.unlock.night_visitor";
        public const string UnlockOutfit = "activity.unlock.outfit";
        public const string UnlockSoon = "activity.unlock.soon";

        // In-game HUD and pause (screen 030).
        public const string HudStreak = "activity.hud.streak";
        public const string PauseTitle = "activity.pause.title";
        public const string PauseResume = "activity.pause.resume";
        public const string PauseQuit = "activity.pause.quit";

        // Result (doc 05 sheet 20; doc 01 section 12.0 « aucun hasard payant »).
        public const string ResultTitleNone = "activity.result.title0";
        public const string ResultTitleOne = "activity.result.title1";
        public const string ResultTitleTwo = "activity.result.title2";
        public const string ResultTitleThree = "activity.result.title3";
        public const string ResultRecord = "activity.result.record";
        public const string ResultBest = "activity.result.best";
        public const string ResultReplay = "activity.result.replay";
        public const string ResultBack = "activity.result.back";
        public const string ResultNextSticker = "activity.result.next_sticker";
        public const string ResultPractice = "activity.result.practice";
        public const string ResultNewStar = "activity.result.new_star";

        // « No ticket » card (doc Mini-jeux section 2: the time first, then the other options; never blocking). The pack
        // reads « 3 billets » beside its price chip (Gems icon and price).
        public const string EmptyTitle = "activity.tickets.empty.title";
        public const string EmptyPractice = "activity.tickets.empty.practice";
        public const string EmptyPack = "activity.tickets.empty.pack";
        public const string EmptyPacksUsed = "activity.tickets.empty.packs_used";
        public const string EmptyNeedGems = "activity.tickets.empty.need_gems";

        // T38 Grooming (doc 01 Contenu S-ACT-001 and A15): the tips the HUD's bubble shows over the game (tutorial and first
        // sightings), and the animal sheet's « Brosser » button. The 10 challenges' names and goals use the catalog's ids:
        // see ChallengeNameKey and ChallengeGoalKey.
        public const string GroomingTipBrush = "activity.grooming.tip.brush";
        public const string GroomingTipLeaves = "activity.grooming.tip.leaves";
        public const string GroomingTipBurr = "activity.grooming.tip.burr";
        public const string GroomingTipBow = "activity.grooming.tip.bow";
        public const string GroomingTipBubble = "activity.grooming.tip.bubble";
        public const string SheetGroom = "activity.grooming.sheet_groom";

        /// <summary>
        /// « challenge.grooming.01.name »: a challenge's name (0 based), from <see cref="ActivityDefinition.ChallengeId"/>;
        /// null for an activity outside the catalog. Builds a string: call it once per bind, never per frame.
        /// </summary>
        public static string ChallengeNameKey(ActivityId activity, int challenge) => ChallengeKey(activity, challenge, ".name");

        /// <summary>« challenge.grooming.01.goal »: a challenge's goal in one short line (the hub's « Défi n : goal »).</summary>
        public static string ChallengeGoalKey(ActivityId activity, int challenge) => ChallengeKey(activity, challenge, ".goal");

        private static string ChallengeKey(ActivityId activity, int challenge, string suffix)
        {
            if (!ActivityCatalog.TryGet(activity, out var definition))
                return null;
            string id = definition.ChallengeId(challenge);
            return string.IsNullOrEmpty(id) ? null : id + suffix;
        }

        /// <summary>The result title by stars (0 to 3): « Joli essai », « Bien joué », « Très bien », « Parfait ! ».</summary>
        public static string ResultTitle(int stars) =>
            stars <= 0 ? ResultTitleNone : stars == 1 ? ResultTitleOne : stars == 2 ? ResultTitleTwo : ResultTitleThree;

        /// <summary>
        /// The localization key of an activity's name (doc 01 section 0, keys activity.&lt;nom&gt;.&lt;élément&gt; with the
        /// doc's names: activity.grooming, activity.hide_seek, activity.nut_drop…), or null for None.
        /// </summary>
        public static string NameKey(ActivityId activity)
        {
            switch (activity)
            {
                case ActivityId.Grooming: return "activity.grooming.name";
                case ActivityId.HideAndSeek: return "activity.hide_seek.name";
                case ActivityId.NutRain: return "activity.nut_drop.name";
                case ActivityId.PawMemory: return "activity.memory.name";
                case ActivityId.Expeditions: return "activity.expeditions.name";
                case ActivityId.Garden: return "activity.garden.name";
                case ActivityId.TreasureFishing: return "activity.treasure_fishing.name";
                case ActivityId.LilyHop: return "activity.lily_hop.name";
                case ActivityId.Bakery: return "activity.baking.name";
                case ActivityId.PhotoSafari: return "activity.photo_safari.name";
                case ActivityId.CuddleMatch: return "activity.cuddle_match.name";
                case ActivityId.Slide: return "activity.glide.name";
                case ActivityId.Constellations: return "activity.constellations.name";
                case ActivityId.FireflyRhythm: return "activity.firefly_rhythm.name";
                case ActivityId.Bubbles: return "activity.bubbles.name";
                case ActivityId.TilePuzzle: return "activity.tile_puzzle.name";
                case ActivityId.BurrowMaze: return "activity.burrow_maze.name";
                case ActivityId.GrandContest: return "activity.grand_contest.name";
                case ActivityId.Test: return "activity.test.name";
                default: return null;
            }
        }

        /// <summary>The activity's icon in Art/UI/Icons (ICO_Act_Grooming…); the hub falls back to ICO_Play when missing.</summary>
        public static string IconName(ActivityId activity) => "ICO_Act_" + activity;
    }

    // ------------------------------------------------------------------------------------------- result beats

    /// <summary>
    /// The result card's beats (doc 05 sheet 20), for the presenter's sounds and vibrations: the view owns the timing, the
    /// presenter plays J-SFX-FEEL-013 per star (fa, la, do; TapLight, Confirm on the third), J-SFX-FEEL-012 per reward card
    /// (pitch rising with the index) and J-SFX-REWARD-001 for a record, at the moment each one shows.
    /// </summary>
    public interface IActivityResultBeats
    {
        /// <summary>An earned star lands (0, 1 or 2).</summary>
        void ResultStarLanded(int star);

        /// <summary>A reward card turned over and shows its face (0 based).</summary>
        void ResultRewardShown(int reward);

        /// <summary>The « Nouveau record » ribbon pops.</summary>
        void ResultRecordShown();

        /// <summary>One tap revealed everything left at once (quick reveal, or RevealAll): one soft chord, no more beats.</summary>
        void ResultRevealedAll();
    }

    /// <summary>Implemented by the result view next to <see cref="IActivityResultView"/>: the presenter listens to its beats.</summary>
    public interface IActivityResultBeatSource
    {
        void SetBeats(IActivityResultBeats beats);
    }
}
