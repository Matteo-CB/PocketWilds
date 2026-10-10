using PocketWilds.Simulation;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T42 « Expéditions »: the screen's localization keys (doc 01 Contenu, table 7.6, and the screen's own words). The texts
    /// live in Editor/UI/ExpeditionTexts.cs (English and French). Placeholders: {n} a number, {h} hours and {m} minutes on two
    /// digits, {time} a duration already written (« 3 h 12 »), {goal} a challenge goal. Counted texts have « .one » and
    /// « .other » forms (ILocalization.GetPlural).
    /// </summary>
    public static class ExpeditionText
    {
        // ---------------- doc 01, table 7.6 ----------------
        public const string Title = "expedition.title";
        public const string PickTeam = "expedition.pick_team";
        public const string Suggested = "expedition.suggested";
        public const string Duration = "expedition.duration";
        public const string Go = "expedition.go";
        public const string BackIn = "expedition.back_in";
        public const string Back = "expedition.back";
        public const string OpenPostcard = "expedition.open_postcard";
        public const string SpeedUp = "expedition.speed_up";
        public const string SlotLocked = "expedition.slot_locked";
        public const string FirstStroll = "expedition.first_stroll";

        /// <summary>Counted: « Œuf Découverte garanti dans {n} longues expéditions ».</summary>
        public const string DiscoveryGuarantee = "expedition.discovery_guarantee";

        // ---------------- the screen's own words ----------------

        /// <summary>The result card's « Repartir » (doc 05 sheet 23).</summary>
        public const string SendAgain = "expedition.send_again";

        /// <summary>« Tout réclamer » when 2 teams or more are back (doc 05 sheet 23, repeated version).</summary>
        public const string ClaimAll = "expedition.claim_all";

        public const string Destination = "expedition.destination";
        public const string Team = "expedition.team";
        public const string Loot = "expedition.loot";
        public const string Challenges = "expedition.challenges";
        public const string SlotClub = "expedition.slot_club";

        /// <summary>The Gems slot's first tap arms it: « Touche encore pour l'ouvrir » (no Gems spent by a slip of the thumb).</summary>
        public const string SlotConfirm = "expedition.slot_confirm";

        public const string NeedGems = "expedition.need_gems";
        public const string ClubSoon = "expedition.club_soon";
        public const string NoSlot = "expedition.no_slot";
        public const string DestinationLocked = "expedition.destination_locked";
        public const string AnimalAway = "expedition.animal_away";
        public const string TeamFull = "expedition.team_full";
        public const string NoAnimal = "expedition.no_animal";

        /// <summary>Counted: « {n} de retour » (the hub card).</summary>
        public const string BackCount = "expedition.back_count";

        /// <summary>Counted: « Encore {n} aujourd'hui » (speed-ups left).</summary>
        public const string SpeedUpsLeft = "expedition.speedups_left";

        public const string SpeedUpsNone = "expedition.speedups_none";

        // The team rating's reasons (doc 01, section 7.2).
        public const string Rating = "expedition.rating";

        /// <summary>Counted: « {n} du biome ».</summary>
        public const string ReasonBiome = "expedition.reason_biome";

        public const string ReasonFamily = "expedition.reason_family";
        public const string ReasonSpecies = "expedition.reason_species";

        // Durations and times (the screens write {m} on two digits after hours: « 3 h 05 »).
        public const string TimeHoursMinutes = "expedition.time_hm";
        public const string TimeHours = "expedition.time_h";
        public const string TimeMinutes = "expedition.time_m";
        public const string TimeSeconds = "expedition.time_s";

        /// <summary>A chance in the loot preview: « {n} % ».</summary>
        public const string Percent = "expedition.percent";

        /// <summary>The destination's name: « {Key}.name » (expedition.flower_meadows.name). Builds a string: once per bind.</summary>
        public static string DestinationName(string destinationKey) => string.IsNullOrEmpty(destinationKey) ? null : destinationKey + ".name";

        /// <summary>« challenge.expeditions.01.name » (0 based).</summary>
        public static string ChallengeName(int challenge) => ActivityText.ChallengeNameKey(ActivityId.Expeditions, challenge);

        /// <summary>« challenge.expeditions.01.goal »: the challenge's destination, duration and constraint in one short line.</summary>
        public static string ChallengeGoal(int challenge) => ActivityText.ChallengeGoalKey(ActivityId.Expeditions, challenge);
    }
}
