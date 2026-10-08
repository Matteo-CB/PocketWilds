#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// What every rendez-vous service shares (T36): the state, the content, both configs, the clock, the services they
    /// feed or read, the reward granter and today's key. <see cref="DailyVisitServices"/> builds it and moves
    /// <see cref="TodayKey"/> forward at each day boundary; services read it, never compute the day themselves on a hot path.
    /// </summary>
    public sealed class DailyContext
    {
        public DailyContext(PlayerState state, ContentRegistry content, DailyVisitConfig config, ProgressionConfig progressionConfig,
            IGameClock clock, EconomyService economy, HatchService? hatch, MasteryService? speciesMastery, ProgressionServices? progression,
            ISaveScheduler? save)
        {
            State = state;
            Content = content;
            Config = config;
            ProgressionConfig = progressionConfig;
            Clock = clock;
            Economy = economy;
            Hatch = hatch;
            SpeciesMastery = speciesMastery;
            Progression = progression;
            Save = save;
            Granter = new RewardGranter(state, economy, hatch, save);
            ProfileSeed = ProfileHash(state.Meta.ProfileId);
            TodayKey = DailyDay.DayKey(DailyDay.Now(state, clock), progressionConfig.TimeZone, config.ResetHour);
        }

        public PlayerState State { get; }
        public ContentRegistry Content { get; }
        public DailyVisitConfig Config { get; }
        public ProgressionConfig ProgressionConfig { get; }
        public IGameClock Clock { get; }
        public EconomyService Economy { get; }
        public HatchService? Hatch { get; }
        public MasteryService? SpeciesMastery { get; }
        public ProgressionServices? Progression { get; }
        public ISaveScheduler? Save { get; }
        public RewardGranter Granter { get; }

        /// <summary>A seed of the profile for the day's draws (same day, same draw: killing the app never rerolls).</summary>
        public uint ProfileSeed { get; }

        /// <summary>Today's key (yyyymmdd, reset hour applied), kept by <see cref="DailyVisitServices"/>.</summary>
        public int TodayKey { get; set; }

        public TimeZoneInfo Zone => ProgressionConfig.TimeZone;

        /// <summary>Calendar day since install (day 1 = install day), for unlocks and caps. A time-zone conversion: not per frame.</summary>
        public int DayNumber => ProgressionMath.DayNumber(State, Clock.UtcNow, ProgressionConfig.TimeZone);

        public int RefugeLevel => Progression?.Refuge.Level ?? 1;

        /// <summary>
        /// The rendez-vous of the day is open (doc 01, section 0: refuge level 2 and the welcome over; the welcome gifts of
        /// days 1 to 3 and the first gift follow it).
        /// </summary>
        public bool TodayOpen => State.IsOnboardingDone && Feature(RefugeFeature.DailyRendezvous);

        /// <summary>A refuge function is open (true without the progression services, as in tests of one service).</summary>
        public bool Feature(RefugeFeature feature) => Progression == null || Progression.Refuge.IsFeatureUnlocked(feature);

        /// <summary>The substitutions' predicates and the newest biome's treat.</summary>
        public RewardContext Rewards()
        {
            bool play = Config.ActivitiesAvailable && Feature(RefugeFeature.Grooming);
            bool garden = Config.GardenAvailable && Feature(RefugeFeature.Garden);
            bool albums = Config.AlbumsAvailable && Feature(RefugeFeature.Albums);
            return new RewardContext(play, garden, albums, Config.BakeryAvailable, NewestTreat(), Config.TicketLightMinutes);
        }

        /// <summary>The treat of the most recent open biome (Clover Crunch with the Prairie alone).</summary>
        public ContentId NewestTreat()
        {
            var biome = Progression?.Store.MostAdvancedUnlocked() ?? ProgressionConfig.Biomes[0].Id;
            return ProgressionConfig.TryGetBiome(biome, out var economy) ? economy.Treat : ProgressionConfig.Biomes[0].Treat;
        }

        public void RequestSave(SaveUrgency urgency) => Save?.RequestSave(urgency);

        /// <summary>A deterministic draw seed for a day and a purpose.</summary>
        public uint Seed(int dayKey, uint salt) => SeedHash.Mix(ProfileSeed ^ (uint)dayKey, salt);

        private static uint ProfileHash(Guid id)
        {
            var bytes = id.ToByteArray();
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ bytes[i]) * 16777619u;
            return SeedHash.Mix(hash);
        }
    }
}
