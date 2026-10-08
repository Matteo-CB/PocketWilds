#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// What every activity service shares (T37): the state, the content, the config, the clock, the services it feeds (the
    /// economy, species and biome mastery, the bond), the reward granter and today's key. <see cref="ActivityServices"/>
    /// builds it. Today's key comes from the rendez-vous' <see cref="DailyContext"/> when there is one (one calendar for the
    /// whole game), else from its own check at the day boundary; it only moves forward.
    /// </summary>
    public sealed class ActivityContext
    {
        private readonly DailyContext? _daily;
        private int _ownTodayKey;

        public ActivityContext(PlayerState state, ActivityState activities, ContentRegistry content, ActivityConfig config,
            ProgressionConfig progressionConfig, IGameClock clock, EconomyService economy, HatchService? hatch,
            MasteryService? speciesMastery, ProgressionServices? progression, IBondSink? bond, ISaveScheduler? save, DailyContext? daily)
        {
            State = state;
            Activities = activities;
            Content = content;
            Config = config;
            ProgressionConfig = progressionConfig;
            Clock = clock;
            Economy = economy;
            Hatch = hatch;
            SpeciesMastery = speciesMastery;
            Progression = progression;
            Bond = bond;
            Save = save;
            _daily = daily;
            Granter = new RewardGranter(state, economy, hatch, save);
            ProfileSeed = ProfileHash(state.Meta.ProfileId);
            _ownTodayKey = DailyDay.DayKey(DailyDay.Now(state, clock), progressionConfig.TimeZone, ResetHour);
        }

        public PlayerState State { get; }
        /// <summary>The activities' saved state (PlayerState.Activities once the save holds it).</summary>
        public ActivityState Activities { get; }
        public ContentRegistry Content { get; }
        public ActivityConfig Config { get; }
        public ProgressionConfig ProgressionConfig { get; }
        public IGameClock Clock { get; }
        public EconomyService Economy { get; }
        public HatchService? Hatch { get; }
        public MasteryService? SpeciesMastery { get; }
        public ProgressionServices? Progression { get; }
        public IBondSink? Bond { get; }
        public ISaveScheduler? Save { get; }
        public RewardGranter Granter { get; }

        /// <summary>A seed of the profile (the game seeds are derived from it and the game count).</summary>
        public uint ProfileSeed { get; }

        /// <summary>The rendez-vous' reset hour when it exists, else local midnight.</summary>
        public int ResetHour => _daily?.Config.ResetHour ?? 0;

        /// <summary>Today's key (yyyymmdd, reset hour applied); only moves forward.</summary>
        public int TodayKey => _daily != null ? Math.Max(_daily.TodayKey, _ownTodayKey) : _ownTodayKey;

        /// <summary>The later of the clock and the profile's high-water mark (a clock moved back changes nothing).</summary>
        public DateTimeOffset Now => DailyDay.Now(State, Clock);

        public long NowUnixMs => Now.ToUnixTimeMilliseconds();

        /// <summary>Calendar day since install (day 1 = install day). A time-zone conversion: not per frame.</summary>
        public int DayNumber => ProgressionMath.DayNumber(State, Clock.UtcNow, ProgressionConfig.TimeZone);

        public int RefugeLevel => Progression?.Refuge.Level ?? State.Progression.Refuge.Level;

        /// <summary>
        /// Recomputes this context's own day key (used when no rendez-vous context exists) and returns the next boundary.
        /// A time-zone conversion: at boundaries and on resume only.
        /// </summary>
        public DateTimeOffset CheckDay()
        {
            var now = Now;
            int key = DailyDay.DayKey(now, ProgressionConfig.TimeZone, ResetHour);
            if (key > _ownTodayKey)
                _ownTodayKey = key;
            return DailyDay.NextBoundaryUtc(now, ProgressionConfig.TimeZone, ResetHour);
        }

        /// <summary>Summed Essence effect (0 without the progression services).</summary>
        public double Essence(EssenceEffect effect) => Progression?.Essence.Sum(effect) ?? 0.0;

        /// <summary>The treat of the most recent open biome (Clover Crunch with the Prairie alone), as T36's sticker substitution.</summary>
        public ContentId NewestTreat()
        {
            var biome = Progression?.Store.MostAdvancedUnlocked() ?? ProgressionConfig.Biomes[0].Id;
            return ProgressionConfig.TryGetBiome(biome, out var economy) ? economy.Treat : ProgressionConfig.Biomes[0].Treat;
        }

        /// <summary>Albums are open: built (T44) and their refuge function reached (level 12, day 5).</summary>
        public bool AlbumsOpen => Config.AlbumsAvailable && (Progression == null || Progression.Refuge.IsFeatureUnlocked(RefugeFeature.Albums));

        public void RequestSave(SaveUrgency urgency) => Save?.RequestSave(urgency);

        /// <summary>The seed of the n-th game since install for an activity: same game, same seed (an app kill never rerolls).</summary>
        public uint GameSeed(int game, ActivityId activity) => SeedHash.Mix(ProfileSeed ^ (uint)game, (uint)activity * 2654435761u);

        private static uint ProfileHash(Guid id)
        {
            var bytes = id.ToByteArray();
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ bytes[i]) * 16777619u;
            return SeedHash.Mix(hash ^ 0xA5C7u);
        }
    }
}
