namespace PocketWilds.Editor.Economy
{
    public enum SimProfileKind
    {
        Casual = 0,
        Regular = 1,
        Passionate = 2,
        Club = 3,
        ClubPassPacks = 4,
        Collector = 5,
    }

    /// <summary>
    /// One player profile of the economy doc (doc 02 Gemmes, sections 2 and 7): when the player comes, for how long, what
    /// they play besides hatching, what they pay for. Sessions are local hours (the simulation's zone is UTC). The
    /// spending choices inside a session are the same for every profile (see <see cref="EconomySimulator"/>): profiles
    /// differ by time, side activities and purchases, never by a smarter or dumber economy.
    /// </summary>
    public sealed class SimProfile
    {
        public SimProfileKind Kind;
        public string Id = string.Empty;

        /// <summary>Name in the report (French).</summary>
        public string Label = string.Empty;

        /// <summary>Day 1: start hour and minutes of each session (the install is the first one).</summary>
        public double[] FirstDayHours = { 12.0 };
        public double[] FirstDayMinutes = { 20.0 };

        /// <summary>Following days: start hours; odd days use <see cref="OddDayHours"/> when given (1 to 2 sessions).</summary>
        public double[] DayHours = { 12.0 };
        public double[] OddDayHours;
        public double SessionMinutes = 8.0;

        /// <summary>Activity games a day once the first activity is open (5 tickets a day, 7 with the Club).</summary>
        public int ActivitiesPerDay;

        /// <summary>Average reward of a game, in minutes of production (1 star 5, 2 stars 10, 3 stars 20).</summary>
        public double ActivityMinutes = 10.0;

        /// <summary>Games a day that reach 3 stars for the first time that day (2 Gems each, 3 a day at most).</summary>
        public int ThreeStarGamesPerDay;

        /// <summary>
        /// T37: activity challenges completed at 3 stars a week (5 Gems each, once; a challenge decoration every 5), up to
        /// the 10 challenges of each open activity (doc Mini-jeux, section 2).
        /// </summary>
        public double ChallengesPerWeek;

        /// <summary>Wishes fulfilled a day (3 Gems each, 3 a day at most).</summary>
        public int WishesPerDay;

        /// <summary>Expeditions sent a day once they open: the last one of the day is a long one (8 h).</summary>
        public int ExpeditionsPerDay;

        public bool WatchesBonusAd;

        /// <summary>The weekly book is completed (7 objectives, 50 Gems and a Discovery egg).</summary>
        public double WeeklyBookShare;

        /// <summary>Plays the monthly event (free track: 150 Gems; its 7 objects are discoveries).</summary>
        public double EventShare;

        public bool Club;
        public bool Pass;
        public bool BiomePacks;

        /// <summary>Gems bought each month for cosmetics only (no power), the big collector.</summary>
        public long GemsBoughtPerMonth;

        /// <summary>Gems kept aside before buying a cosmetic (an accessory costs 250 to 450).</summary>
        public long CosmeticPrice = 350;

        /// <summary>Common Light decorations are bought when their price is at most this many minutes of production.</summary>
        public double DecorationMinutes = 10.0;

        public static SimProfile[] All()
        {
            return new[]
            {
                new SimProfile
                {
                    Kind = SimProfileKind.Casual, Id = "casual", Label = "Occasionnel",
                    FirstDayHours = new[] { 12.0, 20.0 }, FirstDayMinutes = new[] { 18.0, 10.0 },
                    DayHours = new[] { 12.5, 20.5 }, OddDayHours = new[] { 19.0 }, SessionMinutes = 7.0,
                    ActivitiesPerDay = 1, ActivityMinutes = 10.0, ThreeStarGamesPerDay = 0, ChallengesPerWeek = 0.5, WishesPerDay = 2, ExpeditionsPerDay = 1,
                    WatchesBonusAd = false, WeeklyBookShare = 0.5, EventShare = 0.6, DecorationMinutes = 8.0,
                },
                new SimProfile
                {
                    Kind = SimProfileKind.Regular, Id = "regular", Label = "Régulier",
                    FirstDayHours = new[] { 12.0, 15.0, 18.0, 21.5 }, FirstDayMinutes = new[] { 25.0, 10.0, 10.0, 10.0 },
                    DayHours = new[] { 8.5, 12.5, 18.0, 21.5 }, SessionMinutes = 8.0,
                    ActivitiesPerDay = 3, ActivityMinutes = 13.0, ThreeStarGamesPerDay = 2, ChallengesPerWeek = 2.0, WishesPerDay = 3, ExpeditionsPerDay = 2,
                    WatchesBonusAd = true, WeeklyBookShare = 1.0, EventShare = 1.0, DecorationMinutes = 10.0,
                },
                new SimProfile
                {
                    Kind = SimProfileKind.Passionate, Id = "passionate", Label = "Passionné",
                    FirstDayHours = new[] { 12.0, 14.0, 16.0, 18.0, 20.0, 22.0 }, FirstDayMinutes = new[] { 40.0, 15.0, 15.0, 15.0, 15.0, 10.0 },
                    DayHours = new[] { 8.0, 10.0, 12.0, 14.0, 16.0, 18.0, 20.0, 22.0 }, SessionMinutes = 10.0,
                    ActivitiesPerDay = 5, ActivityMinutes = 16.0, ThreeStarGamesPerDay = 3, ChallengesPerWeek = 4.0, WishesPerDay = 3, ExpeditionsPerDay = 3,
                    WatchesBonusAd = true, WeeklyBookShare = 1.0, EventShare = 1.0, DecorationMinutes = 15.0,
                },
                Regularish(SimProfileKind.Club, "club", "Payeur Club", club: true, pass: false, packs: false, bought: 0),
                Regularish(SimProfileKind.ClubPassPacks, "club_pass_packs", "Payeur Club + Pass + packs", club: true, pass: true, packs: true, bought: 0),
                Regularish(SimProfileKind.Collector, "collector", "Grand collectionneur", club: false, pass: false, packs: false, bought: 2400),
            };
        }

        private static SimProfile Regularish(SimProfileKind kind, string id, string label, bool club, bool pass, bool packs, long bought)
        {
            return new SimProfile
            {
                Kind = kind, Id = id, Label = label,
                FirstDayHours = new[] { 12.0, 15.0, 18.0, 21.5 }, FirstDayMinutes = new[] { 25.0, 10.0, 10.0, 10.0 },
                DayHours = new[] { 8.5, 12.5, 18.0, 21.5 }, SessionMinutes = 8.0,
                ActivitiesPerDay = club ? 4 : 3, ActivityMinutes = 13.0, ThreeStarGamesPerDay = 2, ChallengesPerWeek = 2.0, WishesPerDay = 3, ExpeditionsPerDay = club ? 3 : 2,
                WatchesBonusAd = true, WeeklyBookShare = 1.0, EventShare = 1.0, Club = club, Pass = pass, BiomePacks = packs,
                GemsBoughtPerMonth = bought, CosmeticPrice = bought > 0 ? 450 : 350, DecorationMinutes = 10.0,
            };
        }
    }
}
