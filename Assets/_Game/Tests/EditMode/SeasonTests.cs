using System;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Events;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T48B the refuge seasons (month map and its Remote Config, flag, card once per season run, local midnight) and the special
    /// weekends (window edges, rotation, Remote Config list, never during a big event, each kind's effect, seen once), their save
    /// and the allocation-free ticks.
    /// </summary>
    public class SeasonTests
    {
        private const string DefaultMap = "{\"awakening\":[3,4,5],\"sun\":[6,7,8],\"harvest\":[9,10,11],\"frost\":[12,1,2]}";
        private static readonly ContentId Grassland = ContentId.FromSource("biome.grassland");
        private static readonly ContentId Discovery = ContentId.FromSource("egg.wk.discovery");
        private static readonly ContentId Species = ContentId.FromSource("species.wk");
        private static readonly ContentId Variant = ContentId.FromSource("variant.wk.v0");
        private static readonly TimeZoneInfo PlusTwo = TimeZoneInfo.CreateCustomTimeZone("PW+2", TimeSpan.FromHours(2), "PW+2", "PW+2");

        private static DateTimeOffset Utc(int y, int mo, int d, int h = 12, int mi = 0, int s = 0) => new DateTimeOffset(y, mo, d, h, mi, s, TimeSpan.Zero);

        private static DateTimeOffset Local(int y, int mo, int d, int h, int mi = 0, int s = 0) =>
            new DateTimeOffset(y, mo, d, h, mi, s, TimeSpan.FromHours(2)).ToUniversalTime();

        // ------------------------------------------------------------------------------------------- month map

        [Test]
        public void MonthMap_DefaultIsTheAlbumMap()
        {
            var config = new SeasonConfig();
            for (int m = 1; m <= 12; m++)
            {
                Assert.AreEqual(AlbumHooks.SeasonOfMonth(m), (int)config.SeasonOfMonth(m), "Month " + m);
                Assert.AreEqual(AlbumHooks.SeasonOfMonth(m), SeasonConfig.SeasonNumber(null, m));
            }
            Assert.AreEqual(DefaultMap.Replace("[12,1,2]", "[1,2,12]"), config.MonthsJson(), "Months in calendar order.");
            Assert.AreEqual(2026, SeasonConfig.RunYear(null, 2027, 1), "A winter spans December to February.");
            Assert.AreEqual(2026, SeasonConfig.RunYear(config, 2027, 2));
            Assert.AreEqual(2027, SeasonConfig.RunYear(config, 2027, 3));
        }

        [TestCase("{\"awakening\":[3,4],\"sun\":[6,7,8],\"harvest\":[9,10,11],\"frost\":[12,1,2]}", TestName = "MonthMap_Invalid_Gap")]
        [TestCase("{\"awakening\":[3,4,5],\"sun\":[5,6,7,8],\"harvest\":[9,10,11],\"frost\":[12,1,2]}", TestName = "MonthMap_Invalid_Duplicate")]
        [TestCase("{\"awakening\":[3,4,5],\"sun\":[6,7,8],\"harvest\":[9,10,11],\"frost\":[12,1,2,13]}", TestName = "MonthMap_Invalid_Month13")]
        [TestCase("{\"spring\":[3,4,5],\"sun\":[6,7,8],\"harvest\":[9,10,11],\"frost\":[12,1,2]}", TestName = "MonthMap_Invalid_UnknownKey")]
        [TestCase("{\"awakening\":[3,4,5],\"awakening\":[6,7,8],\"harvest\":[9,10,11],\"frost\":[12,1,2]}", TestName = "MonthMap_Invalid_DuplicateKey")]
        [TestCase("{\"awakening\":[3,4,5.0],\"sun\":[6,7,8],\"harvest\":[9,10,11],\"frost\":[12,1,2]}", TestName = "MonthMap_Invalid_Fraction")]
        [TestCase("{\"awakening\":[3,4,5],\"sun\":[6,7,8],\"harvest\":[9,10,11],\"frost\":[12,1,2]", TestName = "MonthMap_Invalid_Unclosed")]
        [TestCase("{\"awakening\":[3,4,5],\"sun\":[6,7,8],\"harvest\":[9,10,11],\"frost\":[12,1,2]} x", TestName = "MonthMap_Invalid_Trailing")]
        [TestCase("[3,4,5]", TestName = "MonthMap_Invalid_NotAnObject")]
        [TestCase("{\"awakening\":\"3,4,5\"}", TestName = "MonthMap_Invalid_NotAList")]
        public void MonthMap_InvalidIsIgnoredWhole(string json)
        {
            var config = new SeasonConfig();
            Assert.IsTrue(config.ApplyRemoteMonths("{\"awakening\":[1,2,3],\"sun\":[4,5,6],\"harvest\":[7,8,9],\"frost\":[10,11,12]}"));
            string before = config.MonthsJson();
            int changes = 0;
            config.Changed += () => changes++;
            Assert.IsFalse(config.ApplyRemoteMonths(json));
            Assert.AreEqual(before, config.MonthsJson(), "An invalid map changes nothing.");
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void MonthMap_RuntimeRemoteSwitchFiresChangedAtOnce()
        {
            var clock = new FakeGameClock(Utc(2026, 10, 15));
            var state = PlayerState.CreateNew(clock.UtcNow);
            var seasons = new SeasonService(state, clock, null, null, TimeZoneInfo.Utc);
            Assert.AreEqual(RefugeSeason.Harvest, seasons.Current);
            Assert.IsFalse(seasons.CardPending, "Never on the very first launch.");
            var seen = RefugeSeason.None;
            int changes = 0;
            seasons.Changed += s =>
            {
                seen = s;
                changes++;
            };

            Assert.IsTrue(seasons.Config.ApplyRemoteMonths("{\"awakening\":[3,4,5],\"sun\":[6,7,8,9],\"harvest\":[11],\"frost\":[10,12,1,2]}"));
            Assert.AreEqual(1, changes, "No new build: the new map applies at once.");
            Assert.AreEqual(RefugeSeason.Frost, seen);
            Assert.AreEqual(RefugeSeason.Frost, seasons.Current);
            Assert.AreEqual(2, SeasonConfig.SeasonNumber(seasons.Config, 9), "Every season lookup reads the new map.");
            Assert.AreEqual(2026, SeasonConfig.RunYear(seasons.Config, 2026, 10));
            Assert.IsTrue(seasons.CardPending, "A new season run: its card is owed.");

            Assert.IsTrue(seasons.Config.ApplyRemoteMonths(DefaultMap));
            Assert.AreEqual(2, changes);
            Assert.AreEqual(RefugeSeason.Harvest, seasons.Current);
            Assert.IsFalse(seasons.CardPending, "Back in the run whose card was stored.");
            Assert.IsTrue(seasons.Config.ApplyRemoteMonths(null), "Absent: the default map.");
            Assert.AreEqual(2, changes, "The same map raises nothing.");
        }

        [Test]
        public void Flag_Off_AlwaysAwakeningAndNoCard()
        {
            var clock = new FakeGameClock(Utc(2026, 7, 10));
            var state = PlayerState.CreateNew(clock.UtcNow);
            var seasons = new SeasonService(state, clock, null, null, TimeZoneInfo.Utc);
            Assert.AreEqual(RefugeSeason.Sun, seasons.Current);
            Assert.IsTrue(seasons.Config.TryOverrideFlag(SeasonConfig.EnabledFlag, false));
            Assert.AreEqual(RefugeSeason.Awakening, seasons.Current, "The doc's fallback.");
            for (int m = 1; m <= 12; m++)
                Assert.AreEqual(1, SeasonConfig.SeasonNumber(seasons.Config, m));
            clock.Set(Utc(2026, 9, 1, 1));
            seasons.Tick();
            Assert.AreEqual(RefugeSeason.Awakening, seasons.Current);
            Assert.IsFalse(seasons.CardPending);
            Assert.IsFalse(seasons.Config.TryOverrideFlag("flags.other", false));
        }

        [Test]
        public void Card_NeverOnTheFirstLaunch_OncePerSeasonRun()
        {
            var clock = new FakeGameClock(Utc(2026, 11, 30, 23));
            var state = PlayerState.CreateNew(clock.UtcNow);
            var seasons = new SeasonService(state, clock, null, null, TimeZoneInfo.Utc);
            Assert.IsFalse(seasons.CardPending);
            Assert.AreEqual(2026093, state.Seasons.CardKey, "The first season is stored as seen silently.");
            int changes = 0;
            seasons.Changed += _ => changes++;

            clock.Set(Utc(2026, 12, 1, 0));
            seasons.Tick();
            Assert.AreEqual(RefugeSeason.Frost, seasons.Current);
            Assert.AreEqual(1, changes);
            Assert.IsTrue(seasons.CardPending);
            seasons.MarkCardShown();
            Assert.IsFalse(seasons.CardPending);
            Assert.AreEqual(2026124, state.Seasons.CardKey);

            clock.Set(Utc(2027, 1, 1, 0, 0, 1));
            seasons.Tick();
            Assert.AreEqual(1, changes, "January is the same Givre.");
            Assert.IsFalse(seasons.CardPending);

            clock.Set(Utc(2027, 3, 1, 8));
            seasons.Tick();
            Assert.AreEqual(RefugeSeason.Awakening, seasons.Current);
            Assert.IsTrue(seasons.CardPending);

            // A reload keeps the card owed until shown.
            var again = new SeasonService(state, clock, null, null, TimeZoneInfo.Utc);
            Assert.IsTrue(again.CardPending);
            again.Override = RefugeSeason.Harvest;
            Assert.AreEqual(RefugeSeason.Harvest, again.Current, "Captures force a season.");
            Assert.IsFalse(again.CardPending);
        }

        [Test]
        public void Season_SwitchesAtLocalMidnight()
        {
            var clock = new FakeGameClock(Local(2026, 11, 30, 12));
            var state = PlayerState.CreateNew(clock.UtcNow);
            var seasons = new SeasonService(state, clock, null, null, PlusTwo);
            clock.Set(Local(2026, 11, 30, 23, 59, 59));
            seasons.Tick();
            Assert.AreEqual(RefugeSeason.Harvest, seasons.Current);
            clock.Set(Local(2026, 12, 1, 0));
            seasons.Tick();
            Assert.AreEqual(RefugeSeason.Frost, seasons.Current, "Midnight local, not UTC.");
        }

        [Test]
        public void Season_TickAllocatesNothing()
        {
            var clock = new FakeGameClock(Utc(2026, 10, 15, 8));
            var state = PlayerState.CreateNew(clock.UtcNow);
            var seasons = new SeasonService(state, clock, null, null, TimeZoneInfo.Utc);
            seasons.Tick();
            var t = clock.UtcNow;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++)
            {
                clock.Set(t.AddSeconds(i));
                seasons.Tick();
            }
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before, "A tick without a new day allocates nothing.");
        }

        // ------------------------------------------------------------------------------------------- weekends

        [Test]
        public void Weekend_WindowEdges_FridayToSundayLocal()
        {
            var clock = new FakeGameClock(Local(2026, 10, 8, 12));
            var state = PlayerState.CreateNew(clock.UtcNow);
            var weekends = new WeekendService(state, clock) { Zone = PlusTwo };
            weekends.Refresh();
            Assert.AreEqual(WeekendKind.None, weekends.Active);
            Assert.AreEqual(0, weekends.EndsUnix);

            clock.Set(Local(2026, 10, 8, 23, 59, 59));
            weekends.Tick();
            Assert.AreEqual(WeekendKind.None, weekends.Active, "Thursday 23:59:59.");
            clock.Set(Local(2026, 10, 9, 0));
            weekends.Tick();
            Assert.AreEqual(WeekendKind.Activities, weekends.Active, "Friday 00:00 (9 October 2026: week -12 of the rotation).");
            Assert.AreEqual(Local(2026, 10, 12, 0).ToUnixTimeSeconds(), weekends.EndsUnix);
            clock.Set(Local(2026, 10, 11, 23, 59, 59));
            weekends.Tick();
            Assert.AreEqual(WeekendKind.Activities, weekends.Active, "Sunday 23:59:59.");
            clock.Set(Local(2026, 10, 12, 0));
            weekends.Tick();
            Assert.AreEqual(WeekendKind.None, weekends.Active, "Monday 00:00.");
            clock.Set(Local(2026, 10, 16, 0, 0, 1));
            weekends.Tick();
            Assert.AreEqual(WeekendKind.Eggs, weekends.Active, "Next Friday: the next kind.");
        }

        [Test]
        public void Weekend_DefaultRotation_ByWeekFromFirstJanuary2027()
        {
            var epoch = new DateTime(2027, 1, 1);
            Assert.AreEqual(DayOfWeek.Friday, epoch.DayOfWeek);
            var expected = new[]
            {
                WeekendKind.Activities, WeekendKind.Eggs, WeekendKind.Cuddles, WeekendKind.Garden, WeekendKind.Expeditions, WeekendKind.Albums,
                WeekendKind.Activities,
            };
            for (int w = 0; w < expected.Length; w++)
                Assert.AreEqual(expected[w], WeekendConfig.DefaultKind(epoch.AddDays(7 * w)), "Week " + w);
            Assert.AreEqual(WeekendKind.Albums, WeekendConfig.DefaultKind(epoch.AddDays(-7)), "Testers before 2027 get weekends too.");
            Assert.AreEqual(WeekendKind.Expeditions, WeekendConfig.DefaultKind(epoch.AddDays(-14)));
            Assert.AreEqual(WeekendKind.Activities, WeekendConfig.DefaultKind(new DateTime(2026, 10, 9)));
        }

        [Test]
        public void Weekend_RemoteList_Parsing()
        {
            Assert.IsTrue(WeekendConfig.TryParseList(
                "[{\"kind\":\"garden\",\"start_local\":\"2027-02-05T00:00\",\"end_local\":\"2027-02-07T23:59\"},"
                + " {\"kind\":\"eggs\",\"start_local\":\"2027-02-12\",\"end_local\":\"2027-02-14\"}]", out var list));
            Assert.AreEqual(2, list.Length);
            Assert.AreEqual(WeekendKind.Garden, list[0].Kind);
            Assert.AreEqual(new DateTime(2027, 2, 5), list[0].StartLocal);
            Assert.AreEqual(new DateTime(2027, 2, 8), list[0].EndLocal, "An end minute is inclusive.");
            Assert.AreEqual(new DateTime(2027, 2, 15), list[1].EndLocal, "An end date covers its day.");
            Assert.IsTrue(WeekendConfig.TryParseList("[]", out list));
            Assert.AreEqual(0, list.Length);

            string ok = "{\"kind\":\"cuddles\",\"start_local\":\"2027-02-05\",\"end_local\":\"2027-02-07\"}";
            var bad = new[]
            {
                "[{\"kind\":\"snow\",\"start_local\":\"2027-02-05\",\"end_local\":\"2027-02-07\"}]",
                "[{\"kind\":\"cuddles\",\"start_local\":\"2027-02-05\"}]",
                "[{\"kind\":\"cuddles\",\"start_local\":\"2027-02-05\",\"end_local\":\"2027-02-07\",\"x\":\"y\"}]",
                "[{\"kind\":\"cuddles\",\"start_local\":\"2027-02-07\",\"end_local\":\"2027-02-05\"}]",
                "[{\"kind\":\"cuddles\",\"start_local\":\"07/02/2027\",\"end_local\":\"2027-02-09\"}]",
                "[{\"kind\":1,\"start_local\":\"2027-02-05\",\"end_local\":\"2027-02-07\"}]",
                "{\"kind\":\"cuddles\"}",
                "[" + ok + "] [",
                "[" + string.Join(",", System.Linq.Enumerable.Repeat(ok, WeekendConfig.MaxEntries + 1)) + "]",
            };
            foreach (var json in bad)
                Assert.IsFalse(WeekendConfig.TryParseList(json, out _), json);
            Assert.IsTrue(WeekendConfig.TryParseList("[" + string.Join(",", System.Linq.Enumerable.Repeat(ok, WeekendConfig.MaxEntries)) + "]", out _));

            var config = new WeekendConfig();
            Assert.IsTrue(config.ApplyRemoteList("[" + ok + "]"));
            Assert.AreEqual(1, config.Schedule.Count);
            Assert.IsFalse(config.ApplyRemoteList(bad[0]));
            Assert.AreEqual(1, config.Schedule.Count, "An invalid list is ignored whole.");
            Assert.IsTrue(config.ApplyRemoteList(null));
            Assert.IsNull(config.Schedule, "Absent: the default rule.");
        }

        [Test]
        public void Weekend_RemoteListReplacesTheRotation_AndFlagOffStopsAll()
        {
            var clock = new FakeGameClock(Utc(2027, 2, 2, 8));
            var state = PlayerState.CreateNew(clock.UtcNow);
            var weekends = new WeekendService(state, clock) { Zone = TimeZoneInfo.Utc };
            weekends.Refresh();
            int changes = 0;
            weekends.Changed += _ => changes++;
            Assert.IsTrue(weekends.Config.ApplyRemoteList("[{\"kind\":\"albums\",\"start_local\":\"2027-02-02T10:00\",\"end_local\":\"2027-02-02T11:59\"}]"));
            Assert.AreEqual(WeekendKind.None, weekends.Active);
            clock.Set(Utc(2027, 2, 2, 10));
            weekends.Tick();
            Assert.AreEqual(WeekendKind.Albums, weekends.Active);
            Assert.AreEqual(Utc(2027, 2, 2, 12).ToUnixTimeSeconds(), weekends.EndsUnix);
            clock.Set(Utc(2027, 2, 2, 12));
            weekends.Tick();
            Assert.AreEqual(WeekendKind.None, weekends.Active);
            clock.Set(Utc(2027, 2, 5, 12));
            weekends.Tick();
            Assert.AreEqual(WeekendKind.None, weekends.Active, "A list replaces the default rotation.");
            Assert.AreEqual(2, changes);

            Assert.IsTrue(weekends.Config.ApplyRemoteList(null));
            Assert.AreEqual(WeekendKind.Albums, weekends.Active, "Week 5 of 2027 (Friday 5 February): Albums, applied at once.");
            Assert.IsTrue(weekends.Config.TryOverrideFlag(WeekendConfig.EnabledFlag, false));
            Assert.AreEqual(WeekendKind.None, weekends.Active);
            Assert.AreEqual(0, weekends.EndsUnix);
        }

        [Test]
        public void Weekend_NeverDuringABigEvent()
        {
            var builder = new ContentRegistryBuilder();
            EventTestFixture.AddContent(builder, 1);
            var content = builder.Build();
            var entry = EventTestFixture.Entry();
            var friday = entry.StartUtc.UtcDateTime.Date.AddDays(1);
            while (friday.DayOfWeek != DayOfWeek.Friday)
                friday = friday.AddDays(1);
            var clock = new FakeGameClock(entry.StartUtc.AddDays(-10));
            var state = PlayerState.CreateNew(clock.UtcNow);
            var events = new EventService(state, content, clock, null, new EventConfig(), EventTestFixture.Calendar(entry));
            var weekends = new WeekendService(state, clock) { Zone = TimeZoneInfo.Utc, Events = events, BigEventRunning = () => events.IsRunning };
            events.PhaseChanged += _ => weekends.Invalidate();

            clock.Set(new DateTimeOffset(friday.AddHours(12), TimeSpan.Zero));
            Assert.IsTrue(events.IsRunning);
            weekends.Tick();
            Assert.AreEqual(WeekendKind.None, weekends.Active, "No weekend while the big event runs.");
            Assert.IsFalse(events.ActivitiesWeekend);

            var after = entry.EndUtc.UtcDateTime.Date.AddDays(1);
            while (after.DayOfWeek != DayOfWeek.Saturday)
                after = after.AddDays(1);
            clock.Set(new DateTimeOffset(after.AddHours(10), TimeSpan.Zero));
            weekends.Tick();
            Assert.IsFalse(events.IsRunning);
            Assert.AreNotEqual(WeekendKind.None, weekends.Active, "The first weekend after the event.");

            // An event starting inside a window ends the weekend at the next check (a minute at most).
            bool running = false;
            var clock2 = new FakeGameClock(Utc(2027, 1, 8, 9));
            var weekends2 = new WeekendService(PlayerState.CreateNew(clock2.UtcNow), clock2) { Zone = TimeZoneInfo.Utc, BigEventRunning = () => running };
            weekends2.Tick();
            Assert.AreEqual(WeekendKind.Eggs, weekends2.Active);
            running = true;
            clock2.Set(Utc(2027, 1, 8, 9, 0, 30));
            weekends2.Tick();
            Assert.AreEqual(WeekendKind.Eggs, weekends2.Active);
            clock2.Set(Utc(2027, 1, 8, 9, 1, 0));
            weekends2.Tick();
            Assert.AreEqual(WeekendKind.None, weekends2.Active);
        }

        private sealed class EffectRig
        {
            public FakeGameClock Clock;
            public PlayerState State;
            public HatchService Hatch;
            public BondService Bond;
            public EventService Events;
            public GardenConfig Garden = new GardenConfig();
            public ExpeditionConfig Expeditions = new ExpeditionConfig();
            public AlbumConfig Albums = new AlbumConfig();
            public WeekendService Weekends;
            public uint Animal;

            public EffectRig()
            {
                var builder = new ContentRegistryBuilder();
                builder.AddSpecies(new SpeciesDefinition(Species, "species.wk.name", RigFamily.SmallQuadruped, Grassland, 1.0, "PF_Animal_Test"), "species.wk");
                builder.AddVariant(new VariantDefinition(Variant, Species, "variant.wk.v0.name", RarityTier.Common, new Color32[VariantDefinition.PaletteSize],
                    PatternChannel.None, 0), "variant.wk.v0");
                builder.AddEgg(new EggDefinition(Discovery, "egg.wk.discovery.name", ContentId.None, EggTier.Discovery, new Color32[EggDefinition.PaletteSize],
                    15, 0, 15, "PF_Egg_Base", 1.0, 1.0, new[] { 0.0, 0.0, 70.0, 22.0, 7.0, 1.0 }, new EggDropEntry[0]), "egg.wk.discovery");
                var content = builder.Build();
                Clock = new FakeGameClock(Utc(2027, 2, 2, 8));
                State = PlayerState.CreateNew(Clock.UtcNow);
                State.World.Biomes.Add(new BiomeProgress { Biome = Grassland, Unlocked = true, ZonesOpened = 1u });
                var economy = new EconomyService(State, content, EconomyConfig.Default, null);
                Hatch = new HatchService(State, content, economy, Clock, null);
                var ctx = new DailyContext(State, content, new DailyVisitConfig(), new ProgressionConfig { TimeZone = TimeZoneInfo.Utc }, Clock, economy,
                    null, null, null, null);
                Bond = new BondService(ctx);
                Events = new EventService(State, content, Clock, null);
                var animal = new AnimalInstance { Species = Species, Variant = Variant, Rarity = RarityTier.Common };
                Animal = State.Collection.Add(animal);
                State.Collection.TryGet(Animal, out animal);
                economy.OnAnimalAdded(animal);
                Weekends = new WeekendService(State, Clock)
                {
                    Zone = TimeZoneInfo.Utc, Events = Events, Hatch = Hatch, Bond = Bond, Garden = Garden, Expeditions = Expeditions, Albums = Albums,
                };
            }

            public void Run(WeekendKind kind)
            {
                Assert.IsTrue(Weekends.Config.ApplyRemoteList(kind == WeekendKind.None
                    ? "[]"
                    : "[{\"kind\":\"" + WeekendConfig.KindKeys[(int)kind] + "\",\"start_local\":\"2027-02-01\",\"end_local\":\"2027-02-28\"}]"));
                Assert.AreEqual(kind, Weekends.Active);
            }
        }

        [Test]
        public void Weekend_EachKindSwitchesItsOwnEffect()
        {
            var rig = new EffectRig();
            var odds = new EggOdds();
            var plant = GardenCatalog.Plants[0];
            rig.Run(WeekendKind.None);
            long grow = GardenRules.GrowMs(in plant, rig.Garden, 0.0);
            int seedBack = GardenRules.SeedBackPercent(1, rig.Garden);
            int stars = rig.Albums.DuplicateStars(StickerRarity.Common);
            rig.Hatch.GetOdds(Discovery, odds);
            Assert.AreEqual(0.0, odds.NoveltyBonus);
            Assert.AreEqual(10, rig.Bond.AddBond(rig.Animal, 10, BondSource.Touch));
            double speciesPity = rig.Hatch.SpeciesBonus(3);
            Assert.Greater(speciesPity, 0.0);

            rig.Run(WeekendKind.Activities);
            Assert.IsTrue(rig.Events.ActivitiesWeekend);
            Assert.AreEqual(2.0, new ActivityConfig().WeekendMultiplier, "Activity rewards x2 (ActivityRewardService).");

            rig.Run(WeekendKind.Eggs);
            Assert.IsFalse(rig.Events.ActivitiesWeekend);
            rig.Hatch.GetOdds(Discovery, odds);
            Assert.AreEqual(0.5, odds.NoveltyBonus, 1e-9, "The egg HUD shows the bonus.");
            Assert.AreEqual(1.0, odds.NoveltyChance, 1e-9, "A Discovery egg already gives a novelty.");
            Assert.AreEqual(HatchService.WeekendNovelty(speciesPity, 0.5), rig.Hatch.SpeciesBonus(3), 1e-9, "New species protection x1.5.");
            Assert.AreEqual(0.6, HatchService.WeekendNovelty(0.4, 0.5), 1e-9);
            Assert.AreEqual(1.0, HatchService.WeekendNovelty(0.8, 0.5), 1e-9);
            rig.State.Collection.DiscoveredSpecies.Add(Species);
            rig.State.Collection.DiscoveredVariants.Add(Variant);
            rig.Hatch.GetOdds(Discovery, odds);
            Assert.AreEqual(0.0, odds.NoveltyChance, "Nothing left to discover.");

            rig.Run(WeekendKind.Cuddles);
            Assert.AreEqual(0.0, rig.Hatch.EggNoveltyBonus);
            Assert.AreEqual(20, rig.Bond.AddBond(rig.Animal, 10, BondSource.Touch), "Bond points x2.");

            rig.Run(WeekendKind.Garden);
            Assert.AreEqual(1.0, rig.Bond.WeekendMultiplier);
            Assert.AreEqual(grow / 2.0, GardenRules.GrowMs(in plant, rig.Garden, 0.0), 2.0, "Growth x2.");
            Assert.AreEqual(100, GardenRules.SeedBackPercent(1, rig.Garden), "Seeds given back 100 %.");
            Assert.Less(seedBack, 100);

            rig.Run(WeekendKind.Expeditions);
            Assert.IsFalse(rig.Garden.WeekendActive);
            Assert.IsTrue(rig.Expeditions.WeekendActive);
            Assert.AreEqual(0.75, rig.Expeditions.WeekendFactor, "Expeditions started now last 25 % less.");

            rig.Run(WeekendKind.Albums);
            Assert.IsFalse(rig.Expeditions.WeekendActive);
            Assert.AreEqual(stars * 2, rig.Albums.DuplicateStars(StickerRarity.Common), "Duplicate stars x2.");
            Assert.IsTrue(rig.Albums.TryOverride("events.weekend.album_star_multiplier", 3), "The doc 08 key.");
            Assert.AreEqual(stars * 3, rig.Albums.DuplicateStars(StickerRarity.Common));
            Assert.IsTrue(rig.Albums.TryOverride("events.weekend.albums_multiplier", 2), "The older key stays an alias.");

            rig.Run(WeekendKind.None);
            Assert.IsFalse(rig.Albums.WeekendActive || rig.Garden.WeekendActive || rig.Expeditions.WeekendActive || rig.Events.ActivitiesWeekend);
            Assert.IsTrue(rig.Weekends.Config.TryOverride(WeekendConfig.BondKey, 3));
            Assert.IsFalse(rig.Weekends.Config.TryOverride(WeekendConfig.BondKey, 4), "Out of bounds.");
            Assert.IsFalse(rig.Weekends.Config.TryOverride(WeekendConfig.EggKey, 1.5));
        }

        [Test]
        public void Weekend_SeenOncePerWeekend()
        {
            var clock = new FakeGameClock(Utc(2027, 1, 8, 9));
            var state = PlayerState.CreateNew(clock.UtcNow);
            var weekends = new WeekendService(state, clock) { Zone = TimeZoneInfo.Utc };
            int seen = 0;
            var kind = WeekendKind.None;
            weekends.Seen += k =>
            {
                seen++;
                kind = k;
            };
            weekends.MarkSeen();
            Assert.AreEqual(0, seen, "Nothing before the first read.");
            weekends.Tick();
            weekends.MarkSeen();
            weekends.MarkSeen();
            Assert.AreEqual(1, seen);
            Assert.AreEqual(WeekendKind.Eggs, kind);
            Assert.AreEqual(202701082, state.Seasons.WeekendSeenKey);
            clock.Set(Utc(2027, 1, 10, 20));
            weekends.Tick();
            weekends.MarkSeen();
            Assert.AreEqual(1, seen, "Still the same weekend.");
            clock.Set(Utc(2027, 1, 13, 20));
            weekends.Tick();
            weekends.MarkSeen();
            Assert.AreEqual(1, seen, "No weekend on Wednesday.");
            clock.Set(Utc(2027, 1, 15, 7));
            weekends.Tick();
            weekends.MarkSeen();
            Assert.AreEqual(2, seen);
            Assert.AreEqual(WeekendKind.Cuddles, kind);
        }

        [Test]
        public void Weekend_TickAllocatesNothing()
        {
            var clock = new FakeGameClock(Utc(2027, 1, 12, 8));
            var state = PlayerState.CreateNew(clock.UtcNow);
            var weekends = new WeekendService(state, clock) { Zone = TimeZoneInfo.Utc, BigEventRunning = () => false };
            weekends.Tick();
            var t = clock.UtcNow;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++)
            {
                clock.Set(t.AddSeconds(i));
                weekends.Tick();
            }
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before, "Outside a window: a clock comparison.");

            clock.Set(Utc(2027, 1, 16, 8));
            weekends.Tick();
            t = clock.UtcNow;
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 50; i++)
            {
                clock.Set(t.AddSeconds(i));
                weekends.Tick();
            }
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before, "Inside a window, between two checks.");
        }

        // ------------------------------------------------------------------------------------------- save

        [Test]
        public void Save_RoundTrip_AndMigration18To19()
        {
            var state = PlayerState.CreateNew(Utc(2026, 10, 9));
            state.Seasons.CardKey = 2026093;
            state.Seasons.WeekendSeenKey = 202610091;
            var serializer = new BinarySaveSerializer();
            var writer = new ByteBufferWriter();
            serializer.Serialize(state, writer);
            var bytes = writer.WrittenSpan.ToArray();
            var loaded = serializer.Deserialize(serializer.ReadDocument(bytes, bytes.Length));
            Assert.AreEqual(2026093, loaded.Seasons.CardKey);
            Assert.AreEqual(202610091, loaded.Seasons.WeekendSeenKey);

            var document = serializer.ReadDocument(bytes, bytes.Length);
            document.RemoveSection(SaveSectionId.Seasons);
            document.SchemaVersion = 18;
            SaveMigrations.CreatePipeline().Migrate(document);
            Assert.AreEqual(19, SaveSchema.CurrentVersion);
            Assert.AreEqual(SaveSchema.CurrentVersion, document.SchemaVersion);
            var migrated = serializer.Deserialize(document);
            Assert.AreEqual(0, migrated.Seasons.CardKey + migrated.Seasons.WeekendSeenKey);
            var clock = new FakeGameClock(Utc(2026, 10, 15));
            var seasons = new SeasonService(migrated, clock, null, null, TimeZoneInfo.Utc);
            Assert.IsFalse(seasons.CardPending, "An updated profile gets no card for the season it is in.");
        }
    }
}
