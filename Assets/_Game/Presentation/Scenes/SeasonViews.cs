using PocketWilds.Simulation;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T48B the weekend chip on the HUD's right rail, in the Event button's slot (weekends never run during a big event): the
    /// weekend's icon on the cream circle; a tap opens its small info sheet (name, effect line, time left).
    /// </summary>
    public interface IWeekendHud
    {
        void BindWeekend(IWeekendHudActions actions);

        /// <param name="kind">The <see cref="WeekendKind"/> value (1 to 6) shown when visible.</param>
        void SetWeekend(bool visible, int kind);
    }

    public interface IWeekendHudActions
    {
        void OpenWeekend();
    }

    /// <summary>T48B text keys (fr/en in the editor's SeasonTexts): season names and card, weekend names, effect lines, time left.</summary>
    public static class SeasonText
    {
        public const string WeekendEnds = "weekend.ends";
        public const string TimeDaysHours = "weekend.time.days_hours";
        public const string TimeHoursMinutes = "weekend.time.hours_minutes";
        public const string TimeMinutes = "weekend.time.minutes";

        /// <summary>NOTIF-EVT-03, stored now for the notifications task: « C'est le {weekend} au refuge. »</summary>
        public const string WeekendNotification = "notif.weekend.start";

        private static readonly string[] Names = { "", "season.awakening.name", "season.sun.name", "season.harvest.name", "season.frost.name" };
        private static readonly string[] Cards = { "", "season.awakening.card", "season.sun.card", "season.harvest.card", "season.frost.card" };

        /// <summary>The card's title, one whole sentence per season (« L'Éveil commence »): short enough for the modal at 60 pt.</summary>
        private static readonly string[] CardTitles =
        {
            "", "season.awakening.card_title", "season.sun.card_title", "season.harvest.card_title", "season.frost.card_title",
        };

        private static readonly string[] WeekendNames =
        {
            "", "weekend.activities.name", "weekend.eggs.name", "weekend.cuddles.name", "weekend.garden.name", "weekend.expeditions.name",
            "weekend.albums.name",
        };

        private static readonly string[] WeekendEffects =
        {
            "", "weekend.activities.effect", "weekend.eggs.effect", "weekend.cuddles.effect", "weekend.garden.effect", "weekend.expeditions.effect",
            "weekend.albums.effect",
        };

        public static string Name(RefugeSeason season) => Names[(int)season < Names.Length ? (int)season : 0];
        public static string CardBody(RefugeSeason season) => Cards[(int)season < Cards.Length ? (int)season : 0];
        public static string CardTitle(RefugeSeason season) => CardTitles[(int)season < CardTitles.Length ? (int)season : 0];
        public static string WeekendName(WeekendKind kind) => WeekendNames[(int)kind < WeekendNames.Length ? (int)kind : 0];
        public static string WeekendEffect(WeekendKind kind) => WeekendEffects[(int)kind < WeekendEffects.Length ? (int)kind : 0];

        /// <summary>Every key, for the localization tests and the editor's text table.</summary>
        public static readonly string[] All =
        {
            WeekendEnds, TimeDaysHours, TimeHoursMinutes, TimeMinutes, WeekendNotification,
            Names[1], Names[2], Names[3], Names[4], Cards[1], Cards[2], Cards[3], Cards[4],
            CardTitles[1], CardTitles[2], CardTitles[3], CardTitles[4],
            WeekendNames[1], WeekendNames[2], WeekendNames[3], WeekendNames[4], WeekendNames[5], WeekendNames[6],
            WeekendEffects[1], WeekendEffects[2], WeekendEffects[3], WeekendEffects[4], WeekendEffects[5], WeekendEffects[6],
        };
    }
}
