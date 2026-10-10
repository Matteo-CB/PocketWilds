using System;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// A refuge for the garden tests: the expedition rig's animals and content, real economy, hatch, mastery, progression,
    /// rendez-vous (wishes, journal) and activity services, a UTC calendar, the garden service and its hooks. Refuge level 8
    /// on day 3 by default: the Garden is open, with 10 Sweet Clover seeds in the inventory.
    /// </summary>
    internal sealed class GardenRig
    {
        /// <summary>Day 3 of a profile created at <see cref="TestUtil.T0"/>.</summary>
        public static readonly DateTimeOffset Day3 = TestUtil.T0 + TimeSpan.FromDays(2);

        public PlayerState State;
        public FakeGameClock Clock;
        public ContentRegistry Registry;
        public EconomyService Economy;
        public MasteryService SpeciesMastery;
        public HatchService Hatch;
        public ProgressionServices P;
        public DailyVisitServices Daily;
        public ActivityServices A;
        public GardenService G;
        public GardenHooks Hooks;

        public static GardenRig Create(int level = 8, long gems = 0, int clover = 10, GardenConfig config = null, PlayerState state = null,
            DateTimeOffset? now = null, int profile = 7, int points = 0)
        {
            var rig = new GardenRig { Registry = ExpeditionRig.BuildContent(), Clock = new FakeGameClock(now ?? Day3) };
            if (state == null)
            {
                state = PlayerState.CreateNew(TestUtil.T0);
                state.Meta.ProfileId = new Guid(profile, 3, 5, 1, 2, 3, 4, 5, 6, 7, 8);
                foreach (var name in ExpeditionRig.DefaultAnimals)
                    ExpeditionRig.AddAnimal(state, name);
                state.DailyVisit.OnboardingDone = true;
                state.Inventory.Add(GardenCatalog.SweetClover, clover);
                state.Garden.Points = points;
            }
            rig.State = state;
            var progressionConfig = new ProgressionConfig { TimeZone = TimeZoneInfo.Utc };
            rig.Economy = new EconomyService(state, rig.Registry, EconomyConfig.Default, null);
            rig.SpeciesMastery = new MasteryService(state, rig.Economy.Config, rig.Economy);
            rig.Hatch = new HatchService(state, rig.Registry, rig.Economy, rig.Clock, null, rig.SpeciesMastery);
            rig.P = new ProgressionServices(state, rig.Registry, rig.Economy, rig.Hatch, rig.Clock, null, progressionConfig, rig.SpeciesMastery);
            state.Progression.Refuge.Level = level;
            if (gems > 0)
                rig.Economy.Grant(CurrencyId.Gems, BigValue.FromDouble(gems), EconomyReason.Debug);
            rig.Daily = new DailyVisitServices(state, rig.Registry, rig.Economy, rig.Hatch, rig.SpeciesMastery, null, rig.P, rig.Clock, null,
                new DailyVisitConfig { ActivitiesAvailable = true });
            rig.A = new ActivityServices(state, state.Activities, rig.Registry, rig.Economy, rig.Hatch, rig.SpeciesMastery, rig.P, rig.Daily.Bond,
                rig.Clock, null, new ActivityConfig(), rig.Daily.Context);
            rig.A.OnResume();
            rig.G = new GardenService(rig.A, config);
            rig.Hooks = new GardenHooks(rig.G, rig.Daily);
            rig.G.Tick();
            return rig;
        }

        public void Advance(TimeSpan delta)
        {
            Clock.Advance(delta);
            G.Tick();
        }

        public void AdvanceMinutes(double minutes) => Advance(TimeSpan.FromMinutes(minutes));

        public long Gems => (long)Economy.Balance(CurrencyId.Gems).ToDoubleClamped();

        public int Count(ContentId item) => State.Inventory.Count(item);

        public void Give(ContentId item, int count) => State.Inventory.Add(item, count);

        /// <summary>Saves the state, then builds new services from the loaded save <paramref name="after"/> later (a closed game).</summary>
        public GardenRig Reload(TimeSpan after) => Create(state: ExpeditionRig.RoundTrip(State), now: Clock.UtcNow + after);

        public GardenPlantResult Plant(int plot, ContentId plant) => G.Plant(plot, plant);

        public static ContentId Plant(string name) => ContentId.FromSource("plant." + name);
    }

    /// <summary>
    /// T43 « Jardin » (doc 01 Contenu, sections 8 and 9, S-ACT-006): exact offline growth with stages at the exact seconds,
    /// watering, a clock set back, a ripe plant waiting for ever, draws at planting (same seed, same draws; a speed-up draws
    /// nothing), the stars and the neighbours' help across a save, products, seeds back and dyes, points and levels, the
    /// plot purchase, speed-up prices and the daily cap, challenges, palettes, the wishes and the journal, the save, and no
    /// allocation in the frame.
    /// </summary>
    public class GardenTests
    {
        private static readonly ContentId Clover = GardenCatalog.SweetClover;
        private static readonly ContentId Strawberry = GardenCatalog.WildStrawberry;
        private static readonly ContentId Daisy = GardenRig.Plant("daisy");
        private static readonly ContentId Carrot = GardenRig.Plant("round_carrot");
        private static readonly ContentId Indigo = GardenCatalog.MeadowIndigo;

        // ---------------- Catalogue ----------------

        [Test]
        public void Catalog_HoldsTheDocTables()
        {
            Assert.AreEqual(25, GardenCatalog.Plants.Length);
            Assert.AreEqual(25, GardenCatalog.Dyes.Length);
            Assert.AreEqual(31, GardenCatalog.Palettes.Length);
            Assert.AreEqual(10, GardenCatalog.Levels.Length);
            Assert.AreEqual(8, GardenCatalog.FlowerDecorations.Length);
            Assert.AreEqual(10, GardenCatalog.Challenges.Length);
            for (int i = 0; i < GardenCatalog.PlantCount; i++)
            {
                var plant = GardenCatalog.Plants[i];
                Assert.AreEqual(ContentId.FromSource(plant.Key), plant.Id);
                Assert.IsTrue(plant.Key.StartsWith("plant.", StringComparison.Ordinal));
                Assert.GreaterOrEqual(plant.GrowSeconds, 30 * 60.0);
                Assert.LessOrEqual(plant.GrowSeconds, 8 * 3600.0);
                Assert.AreEqual(GardenCatalog.Dyes[i].Id, plant.Dye, "Dye i is the dye of plant i (table 9.2).");
                Assert.AreEqual(plant.Id, GardenCatalog.Dyes[i].Plant);
                Assert.AreEqual(i < 12, plant.Level > 0, "12 starter plants, then one per biome.");
                Assert.AreEqual(i < 13, GardenCatalog.Built[i], "Built: the 12 starter plants and the Sweet Pea.");
            }
            Assert.AreEqual(2, GardenCatalog.Plants[GardenCatalog.IndexOf(Indigo)].DyeCount, "2 Meadow Indigo dyes.");
            var clover = GardenCatalog.Plants[0];
            Assert.AreEqual(ContentId.FromSource("crop.clover_flower"), clover.Main);
            Assert.AreEqual(3, clover.MainCount);
            Assert.AreEqual(ContentId.FromSource("ingredient.flower_sugar"), clover.Side);
            Assert.AreEqual(ContentId.FromSource("ingredient.strawberry"), GardenCatalog.Plants[1].Main, "Bakery products keep their ingredient ids.");
            Assert.AreEqual(0x7CC37Au, GardenCatalog.Dyes[0].Rgb);
            Assert.AreEqual(0x5B4FC9u, GardenCatalog.Dyes[24].Rgb);
            Assert.AreEqual(ContentId.FromSource("palette.dye.clover"), GardenCatalog.Palettes[0].Id);
            var meadow = GardenCatalog.Palettes[25];
            Assert.AreEqual(ContentId.FromSource("palette.dye.duo_meadow"), meadow.Id);
            Assert.IsTrue(meadow.IsDuo);
            Assert.AreEqual(3, meadow.CostEach);
            Assert.AreEqual(5, GardenCatalog.Palettes[3].CostEach);
            int[] points = { 0, 30, 90, 200, 380, 650, 1000, 1500, 2200, 3000 };
            for (int l = 0; l < 10; l++)
                Assert.AreEqual(points[l], GardenCatalog.Levels[l].Points, "Level " + (l + 1));
            Assert.AreEqual(ContentId.FromSource("decor.garden_sign"), GardenCatalog.Levels[1].Decoration);
            Assert.AreEqual(ContentId.FromSource("decor.big_garden_sunflower"), GardenCatalog.Levels[9].Decoration);
            Assert.AreEqual(12, GardenCatalog.FlowerDecorations[0].Cost);
            Assert.AreEqual(ContentId.FromSource("challenge.garden.01"), GardenCatalog.Challenges[0].Id);
            Assert.AreEqual(15, GardenCatalog.Challenges[8].Threshold(3));
        }

        // ---------------- Opening ----------------

        [Test]
        public void TheGarden_OpensAtLevel8OnDay3_AndMarksItselfBuilt()
        {
            var rig = GardenRig.Create();
            Assert.IsTrue(ActivityCatalog.Get(ActivityId.Garden).ShippedInThisBuild, "T43 ships the Garden.");
            Assert.IsTrue(rig.A.Config.GardenAvailable, "Building the service marks the Garden as existing.");
            Assert.IsTrue(rig.Daily.Config.GardenAvailable, "Building the hooks does it for the rendez-vous.");
            Assert.IsTrue(rig.A.Context.GardenOpen);
            Assert.IsTrue(rig.G.IsOpen);
            Assert.AreEqual(1, rig.A.GardenLevel, "ActivityServices follows the garden level.");
            Assert.AreEqual(5, rig.G.PlotCount);
            Assert.IsFalse(GardenRig.Create(level: 7).G.IsOpen, "Refuge level 7: closed.");
            var early = GardenRig.Create(now: TestUtil.T0 + TimeSpan.FromDays(1));
            Assert.IsFalse(early.G.IsOpen, "Day 2: closed.");
            Assert.AreEqual(GardenPlantResult.NotOpen, early.Plant(0, Clover));

            Assert.AreEqual(GardenPlantResult.PlantLocked, rig.Plant(0, GardenRig.Plant("poppy")), "Poppy: garden level 2.");
            Assert.AreEqual(GardenPlantResult.NoSeed, rig.Plant(0, Daisy));
            Assert.IsTrue(rig.G.IsPlantOpen(GardenCatalog.SweetPea), "The Prairie's Sweet Pea from the opening.");
            Assert.IsFalse(rig.G.IsPlantOpen(GardenRig.Plant("hazel")), "Biome plants without models stay closed.");
            Assert.AreEqual(GardenPlantResult.Planted, rig.Plant(0, Clover));
            Assert.AreEqual(9, rig.G.SeedsOf(Clover), "The seed left the inventory.");
            Assert.AreEqual(GardenPlantResult.PlotNotEmpty, rig.Plant(0, Clover));
            Assert.AreEqual(GardenPlantResult.PlotNotEmpty, rig.Plant(3, Clover), "The Gems plot is not bought.");
        }

        // ---------------- Growth ----------------

        [Test]
        public void Growth_IsExactOffline_StagesAtTheExactSeconds()
        {
            var rig = GardenRig.Create();
            Assert.AreEqual(GardenPlantResult.Planted, rig.Plant(0, Clover));
            Assert.AreEqual(GardenPlotState.Growing, rig.G.StateOf(0));
            Assert.AreEqual(1, rig.G.StageOf(0));
            Assert.AreEqual(1800.0, rig.G.SecondsLeft(0), 1e-9);
            Assert.AreEqual(1800.0, rig.G.TotalSecondsOf(0), 1e-9);

            // The game closed: each load later sees the growth of the time away, to the second.
            var a = rig.Reload(TimeSpan.FromSeconds(719));
            Assert.AreEqual(1, a.G.StageOf(0), "Stage 1 until 40 %.");
            Assert.AreEqual(1081.0, a.G.SecondsLeft(0), 1e-9);
            var b = rig.Reload(TimeSpan.FromSeconds(720));
            Assert.AreEqual(2, b.G.StageOf(0), "Stage 2 from 40 % (12 min of 30).");
            Assert.AreEqual(0.4f, b.G.GrowthOf(0), 1e-6f);
            var c = rig.Reload(TimeSpan.FromSeconds(1799));
            Assert.AreEqual(GardenPlotState.Growing, c.G.StateOf(0));
            Assert.AreEqual(2, c.G.StageOf(0));
            var d = rig.Reload(TimeSpan.FromSeconds(1800));
            Assert.AreEqual(GardenPlotState.Ripe, d.G.StateOf(0));
            Assert.AreEqual(3, d.G.StageOf(0));
            Assert.AreEqual(0.0, d.G.SecondsLeft(0));
            Assert.AreEqual(1, d.G.RipeCount);

            // The frame raises a change once per stage.
            int changes = 0, stages = 0;
            rig.G.PlotChanged += _ => changes++;
            rig.G.StageChanged += (_, _) => stages++;
            rig.Advance(TimeSpan.FromSeconds(719));
            Assert.AreEqual(0, changes);
            rig.Advance(TimeSpan.FromSeconds(1));
            Assert.AreEqual(1, changes, "Stage 2.");
            rig.Advance(TimeSpan.FromSeconds(1080));
            Assert.AreEqual(2, changes, "Ripe.");
            Assert.AreEqual(2, stages);
            rig.Advance(TimeSpan.FromSeconds(60));
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Watering_TakesTenPercentOfTheTotal_OncePerSprout()
        {
            var rig = GardenRig.Create();
            rig.Plant(0, Clover);
            Assert.IsFalse(rig.G.Watered(0));
            rig.AdvanceMinutes(10);
            Assert.IsTrue(rig.G.Water(0));
            Assert.IsTrue(rig.G.Watered(0));
            Assert.AreEqual(17 * 60.0, rig.G.SecondsLeft(0), 1e-9, "30 min − 3 min, 10 min gone.");
            Assert.AreEqual(27 * 60.0, rig.G.TotalSecondsOf(0), 1e-9);
            Assert.IsFalse(rig.G.Water(0), "Once per sprout.");
            Assert.AreEqual(1, rig.Hooks.Waterings);
            var loaded = rig.Reload(TimeSpan.FromMinutes(17));
            Assert.AreEqual(GardenPlotState.Ripe, loaded.G.StateOf(0), "Ripe at 27 min, across a save.");
            Assert.IsFalse(loaded.G.Water(0), "Not once ripe.");
            Assert.IsFalse(loaded.G.Water(1), "Not a bare plot.");

            // Watered near the end: ripe now, never earlier than now.
            var late = GardenRig.Create();
            late.Plant(0, Clover);
            late.AdvanceMinutes(29);
            Assert.IsTrue(late.G.Water(0));
            Assert.AreEqual(GardenPlotState.Ripe, late.G.StateOf(0));
            Assert.AreEqual(29 * 60.0, late.G.TotalSecondsOf(0), 1e-9);
        }

        [Test]
        public void AClockSetBack_NeverMakesAGrowthLonger()
        {
            var rig = GardenRig.Create();
            rig.Plant(0, Clover);
            rig.AdvanceMinutes(10);
            rig.Clock.Set(rig.Clock.UtcNow - TimeSpan.FromHours(3));
            Assert.LessOrEqual(rig.G.SecondsLeft(0), 1800.0, "Read before the frame: clamped to the total.");
            rig.G.Tick();
            Assert.AreEqual(1800.0, rig.G.SecondsLeft(0), 1e-9, "The growth starts again from now, never longer.");
            rig.AdvanceMinutes(30);
            Assert.AreEqual(GardenPlotState.Ripe, rig.G.StateOf(0));
        }

        [Test]
        public void ARipePlant_WaitsForEver_AndNothingIsLost()
        {
            var rig = GardenRig.Create();
            rig.Plant(0, Clover);
            var loaded = rig.Reload(TimeSpan.FromDays(7));
            Assert.AreEqual(GardenPlotState.Ripe, loaded.G.StateOf(0));
            Assert.IsTrue(loaded.G.TryHarvest(0, out var harvest));
            Assert.AreEqual(3, harvest.MainCount);
            Assert.AreEqual(3, loaded.Count(GardenCatalog.CloverFlower));
            Assert.AreEqual(1, loaded.Count(GardenCatalog.FlowerSugar));
            Assert.AreEqual(GardenPlotState.Empty, loaded.G.StateOf(0));
            Assert.IsFalse(loaded.G.TryHarvest(0, out _));
        }

        // ---------------- Draws ----------------

        [Test]
        public void Draws_HappenAtPlanting_SameSeedSameResult_AndASpeedUpDrawsNothing()
        {
            var a = GardenRig.Create(gems: 100, points: 1000);
            var b = GardenRig.Create(gems: 100, points: 1000);
            for (int i = 0; i < 3; i++)
            {
                a.Plant(i, Clover);
                b.Plant(i, Clover);
                Assert.AreEqual(a.State.Garden.Plots[i].Seed, b.State.Garden.Plots[i].Seed);
                Assert.AreEqual(a.State.Garden.Plots[i].SeedsBack, b.State.Garden.Plots[i].SeedsBack);
                Assert.AreEqual(a.State.Garden.Plots[i].Happy, b.State.Garden.Plots[i].Happy);
            }
            Assert.AreNotEqual(a.State.Garden.Plots[0].Seed, a.State.Garden.Plots[1].Seed, "Each sprout its own seed.");
            var before = a.State.Garden.Plots[0];
            Assert.IsTrue(a.G.TrySpeedUp(0));
            var after = a.State.Garden.Plots[0];
            Assert.AreEqual(before.Seed, after.Seed);
            Assert.AreEqual(before.SeedsBack, after.SeedsBack, "A speed-up draws nothing.");
            Assert.AreEqual(before.Happy, after.Happy);
            Assert.IsTrue(a.G.TryHarvest(0, out var harvest));
            Assert.AreEqual(before.SeedsBack, harvest.SeedsBack);
            Assert.AreEqual(before.Happy, harvest.Happy);
            Assert.AreEqual(before.Happy ? 6 : 3, harvest.MainCount, "« Pousse heureuse » doubles the products.");

            // The rates: 60 % / 75 % / 90 % for the seed back, 10 % happy from level 7.
            var config = new GardenConfig();
            Assert.AreEqual(60, GardenRules.SeedBackPercent(4, config));
            Assert.AreEqual(75, GardenRules.SeedBackPercent(5, config));
            Assert.AreEqual(90, GardenRules.SeedBackPercent(9, config));
            config.WeekendActive = true;
            Assert.AreEqual(100, GardenRules.SeedBackPercent(1, config), "Garden weekend: seeds back 100 %.");
            config.WeekendActive = false;
            int back = 0, happy = 0, unlucky = 0;
            const int n = 20000;
            for (uint s = 1; s <= n; s++)
            {
                uint seed = SeedHash.Mix(s);
                back += GardenRules.DrawSeedsBack(seed, 75);
                happy += GardenRules.DrawHappy(seed, 7, config) ? 1 : 0;
                unlucky += GardenRules.DrawHappy(seed, 6, config) ? 1 : 0;
            }
            Assert.AreEqual(0.75, back / (double)n, 0.015);
            Assert.AreEqual(0.10, happy / (double)n, 0.01);
            Assert.AreEqual(0, unlucky, "No « Pousse heureuse » before level 7.");
        }

        // ---------------- Stars ----------------

        [Test]
        public void Stars_WateredAndANeighbourOfAnotherSpeciesHalfTheGrowth_AcrossASave()
        {
            var rig = GardenRig.Create();
            rig.Give(Strawberry, 2);
            // Plot 0: a strawberry (1 h), watered: 54 min. Plot 1: a clover beside it for 30 min, harvested, then nothing.
            rig.Plant(0, Strawberry);
            rig.G.Water(0);
            rig.Plant(1, Clover);
            Assert.IsFalse(rig.G.Helped(0), "Not yet half of 54 min.");
            rig.AdvanceMinutes(27);
            Assert.IsTrue(rig.G.Helped(0), "27 min of 54: half.");
            rig.AdvanceMinutes(3);
            Assert.IsTrue(rig.G.TryHarvest(1, out var clover));
            Assert.AreEqual(1, clover.Stars, "Not watered: 1 star.");
            Assert.IsTrue(clover.Helped, "The strawberry helped the clover the whole time.");
            // The clover's 30 minutes are kept in the save.
            var loaded = rig.Reload(TimeSpan.FromMinutes(24));
            Assert.IsTrue(loaded.G.Helped(0));
            Assert.IsTrue(loaded.G.TryHarvest(0, out var strawberry));
            Assert.AreEqual(3, strawberry.Stars);
            Assert.IsTrue(strawberry.Watered && strawberry.Helped);
            Assert.AreEqual(5, strawberry.MainCount, "+1 main product at 3 stars.");

            // Same species beside it: no help.
            var same = GardenRig.Create();
            same.Plant(0, Clover);
            same.G.Water(0);
            same.Plant(1, Clover);
            same.AdvanceMinutes(27);
            Assert.IsTrue(same.G.TryHarvest(0, out var twin));
            Assert.AreEqual(2, twin.Stars, "A clover beside a clover: 2 stars.");
            Assert.IsFalse(twin.Helped);
        }

        [Test]
        public void TheHelp_AddsUpEveryCropBesideIt_AndNeedsHalf()
        {
            var rig = GardenRig.Create();
            rig.Give(Carrot, 2);
            // A carrot (90 min, watered: 81) with a clover beside it for 30 min only: 2 stars.
            rig.Plant(0, Carrot);
            rig.G.Water(0);
            rig.Plant(1, Clover);
            rig.AdvanceMinutes(30);
            Assert.IsTrue(rig.G.TryHarvest(1, out _));
            Assert.IsFalse(rig.G.Helped(0), "30 min of 81.");
            rig.AdvanceMinutes(51);
            Assert.IsTrue(rig.G.TryHarvest(0, out var alone));
            Assert.AreEqual(2, alone.Stars);

            // The same with a second clover after the first: 60 min of 81, more than half.
            rig.Plant(0, Carrot);
            rig.G.Water(0);
            rig.Plant(1, Clover);
            rig.AdvanceMinutes(30);
            Assert.IsTrue(rig.G.TryHarvest(1, out _));
            rig.Plant(1, Clover);
            rig.AdvanceMinutes(10);
            Assert.IsFalse(rig.G.Helped(0), "40 min of 81: not yet half.");
            rig.AdvanceMinutes(1);
            Assert.IsTrue(rig.G.Helped(0), "41 min of 81.");
            rig.AdvanceMinutes(40);
            Assert.IsTrue(rig.G.TryHarvest(0, out var helped));
            Assert.AreEqual(3, helped.Stars);

            // Unwatered: 1 star even with help.
            rig.Plant(0, Clover);
            rig.AdvanceMinutes(30);
            Assert.IsTrue(rig.G.TryHarvest(0, out var dry));
            Assert.AreEqual(1, dry.Stars);
        }

        // ---------------- Products, dyes, seeds, points, levels ----------------

        [Test]
        public void Harvests_FeedTheInventory_DyesOnlyFromLevel4()
        {
            var rig = GardenRig.Create(points: 199);
            rig.Plant(0, Clover);
            rig.AdvanceMinutes(30);
            Assert.IsTrue(rig.G.TryHarvest(0, out var before));
            Assert.AreEqual(ContentId.None, before.Dye, "Level 3: no dye.");
            Assert.AreEqual(0, rig.G.Owned(GardenCatalog.Dyes[0].Id));
            Assert.AreEqual(4, before.LevelAfter, "199 + 3 points: level 4.");
            Assert.IsTrue(rig.G.DyesOpen);
            Assert.AreEqual(4, rig.A.GardenLevel);

            rig.Plant(0, Clover);
            rig.AdvanceMinutes(30);
            Assert.IsTrue(rig.G.TryHarvest(0, out var after));
            Assert.AreEqual(GardenCatalog.Dyes[0].Id, after.Dye);
            Assert.AreEqual(1, after.Dyes);
            Assert.AreEqual(1, rig.G.Owned(GardenCatalog.Dyes[0].Id));
            Assert.AreEqual(6, rig.Count(GardenCatalog.CloverFlower));
            Assert.AreEqual(2, rig.Count(GardenCatalog.FlowerSugar));

            rig.Give(Indigo, 1);
            Assert.AreEqual(GardenPlantResult.Planted, rig.Plant(1, Indigo), "Meadow Indigo opens at level 4.");
            rig.Advance(TimeSpan.FromHours(4));
            Assert.IsTrue(rig.G.TryHarvest(1, out var indigo));
            Assert.AreEqual(2, indigo.Dyes, "2 Meadow Indigo dyes.");
            Assert.AreEqual(2, rig.G.Owned(GardenCatalog.Dyes[8].Id));
            Assert.AreEqual(2, rig.Count(GardenCatalog.IndigoFlower));
            Assert.AreEqual(10 - 2 + before.SeedsBack + after.SeedsBack, rig.G.SeedsOf(Clover), "Seeds back go to the inventory.");
        }

        [Test]
        public void PointsAndLevels_FollowTable83()
        {
            Assert.AreEqual(3, GardenRules.Points(30 * 60, 1), "30 min: 3.");
            Assert.AreEqual(7, GardenRules.Points(30 * 60, 3), "+2 per star above the first.");
            Assert.AreEqual(5, GardenRules.Points(45 * 60, 1), "4.5 rounded.");
            Assert.AreEqual(9, GardenRules.Points(90 * 60, 1));
            Assert.AreEqual(50, GardenRules.Points(8 * 3600, 2));
            Assert.AreEqual(1, GardenRules.LevelOf(29));
            Assert.AreEqual(2, GardenRules.LevelOf(30));
            Assert.AreEqual(9, GardenRules.LevelOf(2999));
            Assert.AreEqual(10, GardenRules.LevelOf(50000));
            Assert.AreEqual(10, GardenRules.PointsInLevel(100));
            Assert.AreEqual(110, GardenRules.PointsForNext(100), "Level 3: 90 to 200.");
            Assert.AreEqual(0, GardenRules.PointsForNext(3000));

            var rig = GardenRig.Create(points: 25);
            int levels = 0;
            rig.G.LeveledUp += (from, to) => levels = to;
            rig.Plant(0, Clover);
            rig.G.Water(0);
            rig.AdvanceMinutes(27);
            Assert.IsTrue(rig.G.TryHarvest(0, out var harvest));
            Assert.AreEqual(5, harvest.Points);
            Assert.AreEqual(1, harvest.LevelBefore);
            Assert.AreEqual(2, harvest.LevelAfter);
            Assert.AreEqual(2, levels);
            Assert.AreEqual(2, rig.G.Level);
            Assert.AreEqual(0, rig.G.PointsInLevel);
            Assert.AreEqual(60, rig.G.PointsForNext);
            Assert.AreEqual(1, rig.Count(ContentId.FromSource("decor.garden_sign")), "Level 2 gives the garden sign.");
            Assert.IsTrue(rig.G.IsPlantOpen(GardenRig.Plant("poppy")));

            // Seeds back: +1 at 3 stars from level 10.
            Assert.AreEqual(1, GardenRules.SeedsBack(0, 3, 10));
            Assert.AreEqual(0, GardenRules.SeedsBack(0, 3, 9));
            Assert.AreEqual(2, GardenRules.SeedsBack(1, 3, 10));
            Assert.AreEqual(1, GardenRules.SeedsBack(1, 2, 10));
        }

        // ---------------- Plots and speed-ups ----------------

        [Test]
        public void ThePlots_ThreeFree_400GemsForTheFourth_TheClubForTheFifth()
        {
            var rig = GardenRig.Create(gems: 399);
            Assert.AreEqual(3, rig.G.OwnedPlots);
            Assert.AreEqual(GardenPlotState.Empty, rig.G.StateOf(2));
            Assert.AreEqual(GardenPlotState.Locked, rig.G.StateOf(3));
            Assert.AreEqual(GardenPlotUnlock.Gems, rig.G.UnlockOf(3));
            Assert.AreEqual(GardenPlotUnlock.Club, rig.G.UnlockOf(4));
            Assert.AreEqual(GardenPlotUnlock.None, rig.G.UnlockOf(0));
            Assert.AreEqual(400, rig.G.PlotPrice);
            Assert.IsFalse(rig.G.TryBuyPlot(), "399 Gems.");
            rig.Economy.Grant(CurrencyId.Gems, BigValue.FromDouble(1), EconomyReason.Debug);
            Assert.IsTrue(rig.G.TryBuyPlot());
            Assert.AreEqual(0, rig.Gems);
            Assert.AreEqual(GardenPlotState.Empty, rig.G.StateOf(3));
            Assert.IsFalse(rig.G.TryBuyPlot(), "Once.");
            rig.A.Config.ClubActive = true;
            Assert.AreEqual(GardenPlotState.Empty, rig.G.StateOf(4));
            Assert.AreEqual(5, rig.G.OwnedPlots);
            Assert.IsTrue(rig.Reload(TimeSpan.Zero).State.Garden.PlotBought, "Permanent.");
        }

        [Test]
        public void SpeedUps_CostAGemPer10Minutes_3AtLeast_3ADay()
        {
            var config = new GardenConfig();
            Assert.AreEqual(3, GardenRules.SpeedUpCost(30 * 60, config));
            Assert.AreEqual(3, GardenRules.SpeedUpCost(5 * 60, config), "3 at least.");
            Assert.AreEqual(4, GardenRules.SpeedUpCost(35 * 60, config), "Started 10 minutes.");
            Assert.AreEqual(48, GardenRules.SpeedUpCost(8 * 3600, config));
            Assert.AreEqual(0, GardenRules.SpeedUpCost(0, config));

            var rig = GardenRig.Create(gems: 100);
            int spent = 0;
            rig.G.SpedUp += (_, gems, _) => spent += gems;
            for (int p = 0; p < 3; p++)
                rig.Plant(p, Clover);
            Assert.AreEqual(3, rig.G.SpeedUpCost(0));
            Assert.AreEqual(3, rig.G.SpeedUpsLeftToday);
            for (int p = 0; p < 3; p++)
            {
                Assert.IsTrue(rig.G.TrySpeedUp(p));
                Assert.AreEqual(GardenPlotState.Ripe, rig.G.StateOf(p));
            }
            Assert.AreEqual(91, rig.Gems);
            Assert.AreEqual(9, spent);
            Assert.AreEqual(0, rig.G.SpeedUpsLeftToday);
            Assert.AreEqual(0, rig.G.SpeedUpCost(0), "Nothing to speed up once ripe.");
            Assert.IsTrue(rig.G.TryHarvest(0, out _));
            rig.Plant(0, Clover);
            Assert.IsFalse(rig.G.TrySpeedUp(0), "3 a day.");
            rig.Advance(TimeSpan.FromDays(1));
            rig.Daily.CheckDay();
            rig.A.CheckDay();
            Assert.AreEqual(3, rig.G.SpeedUpsLeftToday, "A new day.");
            Assert.IsTrue(rig.G.TryHarvest(0, out _));
            rig.Plant(0, Clover);
            Assert.IsTrue(rig.G.TrySpeedUp(0));
        }

        // ---------------- Challenges ----------------

        [Test]
        public void AChallenge_AddsTheHarvestStars_AndKeepsTheBest()
        {
            var rig = GardenRig.Create();
            Assert.AreEqual(10, rig.G.ChallengeCount);
            Assert.IsTrue(rig.G.IsChallengeOpen(0));
            Assert.IsFalse(rig.G.IsChallengeOpen(4), "Récolte longue: no plant of 5 h at level 1.");
            Assert.IsFalse(rig.G.IsChallengeOpen(7), "Panier à gâteaux: Golden Wheat at level 3.");
            Assert.IsFalse(rig.G.IsChallengeOpen(8), "Cinq couleurs: the dyes open.");
            Assert.IsFalse(rig.G.IsChallengeOpen(6), "Fleurs du monde: one biome plant only.");
            Assert.IsTrue(rig.G.StartChallenge(0));
            Assert.AreEqual(0, rig.G.ActiveChallenge);
            Assert.AreEqual(2, rig.G.ChallengeHarvestsNeeded(0));
            Assert.AreEqual(6, rig.G.ChallengeThreshold(0, 3));
            rig.Plant(0, Clover);
            rig.Plant(1, Clover);
            rig.G.Water(0);
            rig.G.Water(1);
            rig.AdvanceMinutes(27);
            Assert.IsTrue(rig.G.TryHarvest(0, out _));
            Assert.AreEqual(1, rig.G.ChallengeHarvests);
            Assert.AreEqual(2, rig.G.ChallengeStarsSoFar);
            Assert.IsTrue(rig.G.TryHarvest(1, out _));
            Assert.AreEqual(-1, rig.G.ActiveChallenge, "Done after its harvests.");
            Assert.AreEqual(2, rig.G.ChallengeStars(0), "4 harvest stars: 2 challenge stars.");
            Assert.AreEqual(1, rig.State.Activities.ChallengeAttempts[ActivityState.ChallengeIndex(ActivityCatalog.SlotOf(ActivityId.Garden), 0)]);

            // Plein jardin: every owned plot within the same minute.
            Assert.IsTrue(rig.G.StartChallenge(5));
            Assert.AreEqual(3, rig.G.ChallengeHarvestsNeeded(5));
            for (int p = 0; p < 3; p++)
                rig.Plant(p, Clover);
            rig.AdvanceMinutes(30);
            Assert.IsTrue(rig.G.TryHarvest(0, out _));
            rig.AdvanceMinutes(2);
            Assert.IsTrue(rig.G.TryHarvest(1, out _));
            Assert.AreEqual(1, rig.G.ChallengeHarvests, "Two minutes later: the run starts again.");
            Assert.IsTrue(rig.G.TryHarvest(2, out _));
            Assert.AreEqual(2, rig.G.ChallengeHarvests);
            rig.G.StopChallenge();
            Assert.AreEqual(-1, rig.G.ActiveChallenge);
        }

        // ---------------- Palettes ----------------

        [Test]
        public void Palettes_Cost5Dyes_Duos3And3_AndArePermanent()
        {
            var rig = GardenRig.Create(points: 200);
            var dyes = rig.State.Garden.Dyes;
            var clover = GardenCatalog.Palettes[0].Id;
            var meadow = GardenCatalog.Palettes[25].Id;
            dyes[0] = 4;
            Assert.IsFalse(rig.G.CanBuyPalette(clover), "4 dyes.");
            dyes[0] = 8;
            Assert.IsTrue(rig.G.TryBuyPalette(clover));
            Assert.AreEqual(3, dyes[0]);
            Assert.IsTrue(rig.G.OwnsPalette(clover));
            Assert.IsFalse(rig.G.TryBuyPalette(clover), "Once.");
            Assert.IsFalse(rig.G.CanBuyPalette(meadow), "No Strawberry Pink.");
            dyes[1] = 3;
            Assert.IsTrue(rig.G.TryBuyPalette(meadow));
            Assert.AreEqual(0, dyes[0]);
            Assert.AreEqual(0, dyes[1]);
            Assert.AreEqual(31, rig.G.PaletteCount);
            Assert.AreEqual(25, rig.G.DyeCount);
            Assert.AreEqual(meadow, rig.G.PaletteAt(25));
            var loaded = rig.Reload(TimeSpan.Zero);
            Assert.IsTrue(loaded.G.OwnsPalette(clover) && loaded.G.OwnsPalette(meadow), "Permanent.");

            var early = GardenRig.Create(points: 199);
            early.State.Garden.Dyes[0] = 9;
            Assert.IsFalse(early.G.CanBuyPalette(clover), "Palettes open with the dyes (garden level 4).");
        }

        // ---------------- Wishes and journal ----------------

        [Test]
        public void TheHooks_FeedWishes031To036_AndTheJournal()
        {
            var rig = GardenRig.Create(points: 200);
            int day = rig.Daily.TodayKey;
            var visit = rig.State.DailyVisit;
            void Wish(WishTemplateId template, int target = 1, ContentId param = default)
            {
                visit.Wishes[visit.WishCount++] = new WishRecord
                {
                    Template = (ushort)template, Animal = 900000u + (uint)template, Target = target, DrawnDay = day, Param = param,
                };
            }
            bool Done(WishTemplateId t) => visit.Wishes.Take(visit.WishCount).Any(w => (WishTemplateId)w.Template == t && w.IsDone);
            int Progress(WishTemplateId t) => visit.Wishes.Take(visit.WishCount).First(w => (WishTemplateId)w.Template == t).Progress;
            Wish(WishTemplateId.PlantSeeds, 2);
            Wish(WishTemplateId.HarvestPlants);
            Wish(WishTemplateId.WaterSprouts);
            Wish(WishTemplateId.HarvestCrop, 3, GardenCatalog.CloverFlower);
            Wish(WishTemplateId.BringCrop, 2, GardenCatalog.CloverFlower);
            Wish(WishTemplateId.HarvestDye);
            visit.Goals[0] = new JournalGoalRecord { Goal = (ushort)JournalGoalId.Harvests, Target = 10 };
            visit.Goals[1] = new JournalGoalRecord { Goal = (ushort)JournalGoalId.SproutsWatered, Target = 6 };
            visit.Goals[2] = new JournalGoalRecord { Goal = (ushort)JournalGoalId.ThreeStarHarvests, Target = 3 };
            visit.Goals[3] = new JournalGoalRecord { Goal = (ushort)JournalGoalId.Dyes, Target = 3 };
            visit.Goals[4] = new JournalGoalRecord { Goal = (ushort)JournalGoalId.GardenPoints, Target = 60 };
            visit.GoalCount = 5;
            visit.JournalEndKey = 99991231;

            rig.Give(Strawberry, 1);
            rig.Plant(0, Clover);
            Assert.AreEqual(1, Progress(WishTemplateId.PlantSeeds));
            rig.Plant(1, Strawberry);
            Assert.IsTrue(Done(WishTemplateId.PlantSeeds));
            Assert.AreEqual(2, rig.Hooks.Plantings);
            Assert.IsTrue(rig.G.Water(0));
            Assert.IsTrue(Done(WishTemplateId.WaterSprouts));
            rig.AdvanceMinutes(27);
            Assert.IsTrue(rig.G.TryHarvest(0, out var harvest));
            Assert.AreEqual(3, harvest.Stars, "Watered, a strawberry beside it the whole time.");
            Assert.AreEqual(4, harvest.MainCount);
            Assert.AreEqual(1, rig.Hooks.Harvests);
            Assert.IsTrue(Done(WishTemplateId.HarvestPlants));
            Assert.IsTrue(Done(WishTemplateId.HarvestCrop), "4 clover flowers of 3.");
            Assert.IsTrue(Done(WishTemplateId.HarvestDye), "Level 4: a dye.");
            Assert.IsTrue(Done(WishTemplateId.BringCrop), "The crops held are given.");
            Assert.AreEqual(2, rig.Count(GardenCatalog.CloverFlower), "4 harvested, 2 given to the animal.");
            Assert.AreEqual(1, visit.Goals[0].Progress, "JRN-027.");
            Assert.AreEqual(1, visit.Goals[1].Progress, "JRN-028.");
            Assert.AreEqual(1, visit.Goals[2].Progress, "JRN-029: a 3-star harvest.");
            Assert.AreEqual(1, visit.Goals[3].Progress, "JRN-030.");
            Assert.AreEqual(harvest.Points, visit.Goals[4].Progress, "JRN-041.");
            Assert.AreEqual(7, harvest.Points);

            Assert.IsTrue(WishCatalog.IsShipped(WishSystem.Garden));
            Assert.AreEqual(WishParamKind.Crop, WishCatalog.All[(int)WishTemplateId.HarvestCrop - 1].ParamKind);
            var journal = new WeeklyJournalService(rig.Daily.Context);
            Assert.Greater(journal.DrawWeight(JournalGoalId.Harvests, false), 0.0, "JRN-027 in the Timers slot.");
            Assert.Greater(journal.DrawWeight(JournalGoalId.Dyes, false), 0.0, "JRN-030 with the dyes open.");
            Assert.AreEqual(0.0, new WeeklyJournalService(GardenRig.Create().Daily.Context).DrawWeight(JournalGoalId.Dyes, false),
                "JRN-030 waits for garden level 4.");
        }

        [Test]
        public void WishesDrawn_NameACropWhoseSeedIsHeld()
        {
            var rig = GardenRig.Create();
            var visit = rig.State.DailyVisit;
            rig.Daily.Wishes.RefreshSystemUnlocks();
            Assert.IsTrue(rig.Daily.Wishes.IsSystemOpen(WishSystem.Garden), "SYS-07 opens with the Garden.");
            int crops = 0;
            for (int day = 0; day < 40; day++)
            {
                rig.Daily.Wishes.DrawForDay(rig.Daily.TodayKey);
                for (int i = 0; i < visit.WishCount; i++)
                {
                    var wish = visit.Wishes[i];
                    var id = (WishTemplateId)wish.Template;
                    if (id == WishTemplateId.HarvestCrop || id == WishTemplateId.BringCrop)
                    {
                        crops++;
                        Assert.AreEqual(GardenCatalog.CloverFlower, wish.Param, "The only seed held is Sweet Clover's.");
                    }
                    Assert.AreNotEqual(WishTemplateId.HarvestDye, id, "No dye before garden level 4.");
                }
                visit.WishCount = 0;
                rig.Advance(TimeSpan.FromDays(1));
                rig.Daily.CheckDay();
            }
            Assert.Greater(crops, 0, "Crop wishes are drawn.");
        }

        // ---------------- Save ----------------

        [Test]
        public void TheSave_KeepsPlotsTimersDrawsHelpChallengeDyesAndPalettes()
        {
            var rig = GardenRig.Create(gems: 500, points: 230);
            Assert.IsTrue(rig.G.TryBuyPlot());
            rig.Give(Strawberry, 3);
            rig.Plant(0, Strawberry);
            rig.G.Water(0);
            rig.Plant(1, Clover);
            rig.Plant(3, Clover);
            rig.AdvanceMinutes(30);
            Assert.IsTrue(rig.G.TryHarvest(1, out _));
            Assert.IsTrue(rig.G.StartChallenge(2));
            Assert.IsTrue(rig.G.TryHarvest(3, out _));
            rig.State.Garden.Dyes[5] = 4;
            rig.State.Garden.Palettes = 1u << 30;
            rig.G.MarkVisited();
            var g = rig.State.Garden;

            var loaded = ExpeditionRig.RoundTrip(rig.State).Garden;
            Assert.AreEqual(g.PlotBought, loaded.PlotBought);
            Assert.AreEqual(g.FirstVisitDone, loaded.FirstVisitDone);
            Assert.AreEqual(g.Points, loaded.Points);
            Assert.AreEqual(g.Planted, loaded.Planted);
            Assert.AreEqual(g.Harvested, loaded.Harvested);
            Assert.AreEqual(g.Challenge, loaded.Challenge);
            Assert.AreEqual(1, loaded.ChallengeHarvests);
            Assert.AreEqual(g.ChallengeStars, loaded.ChallengeStars);
            Assert.AreEqual(g.ChallengeRunStartUnixMs, loaded.ChallengeRunStartUnixMs);
            Assert.AreEqual(g.ChallengeMask, loaded.ChallengeMask);
            for (int p = 0; p < GardenState.MaxPlots; p++)
            {
                var a = g.Plots[p];
                var b = loaded.Plots[p];
                Assert.AreEqual(a.Active, b.Active, "Plot " + p);
                Assert.AreEqual(a.Plant, b.Plant);
                Assert.AreEqual(a.PlantedUnixMs, b.PlantedUnixMs);
                Assert.AreEqual(a.RipeUnixMs, b.RipeUnixMs);
                Assert.AreEqual(a.GrowMs, b.GrowMs);
                Assert.AreEqual(a.Watered, b.Watered);
                Assert.AreEqual(a.Seed, b.Seed);
                Assert.AreEqual(a.SeedsBack, b.SeedsBack);
                Assert.AreEqual(a.Happy, b.Happy);
                Assert.AreEqual(a.HelpLeftMs, b.HelpLeftMs);
                Assert.AreEqual(a.HelpRightMs, b.HelpRightMs);
                Assert.AreEqual(a.FlowerBeside, b.FlowerBeside);
            }
            Assert.AreEqual(30 * 60_000L, g.Plots[0].HelpRightMs, "The clover's 30 minutes are saved.");
            Assert.IsTrue(g.Plots[0].FlowerBeside);
            CollectionAssert.AreEqual(g.Dyes, loaded.Dyes);
            Assert.AreEqual(g.Palettes, loaded.Palettes);
            Assert.AreEqual(13, SaveSchema.CurrentVersion);
            Assert.AreEqual(19, (int)SaveSectionId.Garden);
        }

        // ---------------- Remote Config ----------------

        [Test]
        public void TheConfig_TakesRemoteValuesInsideTheirBounds()
        {
            var config = new GardenConfig();
            Assert.IsFalse(config.TryOverride("garden.plots_base", 2), "Never lowered.");
            Assert.IsTrue(config.TryOverride("gems.price.garden_plot", 500));
            Assert.AreEqual(500, config.PlotPrice);
            Assert.IsFalse(config.TryOverride("gems.price.garden_plot", 5000));
            Assert.IsFalse(config.TryOverride("garden.water_reduction", 0.5));
            Assert.IsTrue(config.TryOverride("garden.water_reduction", 0.2));
            Assert.IsTrue(config.TryOverride("gems.garden_speedup_daily_cap", 5));
            Assert.IsFalse(config.TryOverride("garden.unknown", 1));
            Assert.AreEqual(1800.0, new GardenConfig().SpreadSeconds(1800.0), 1e-9, "The defaults keep the doc's times.");
            Assert.IsTrue(config.TryOverride("garden.grow_minutes_max", 240));
            Assert.AreEqual(240 * 60.0, config.SpreadSeconds(8 * 3600.0), 1e-6, "The longest plant follows the new maximum.");
            Assert.IsTrue(config.TryOverrideFlag(GardenConfig.EnabledFlag, false));
            var rig = GardenRig.Create(config: config);
            Assert.IsFalse(rig.G.IsOpen, "flags.garden off hides the garden.");
        }

        // ---------------- Stable values and performance ----------------

        [Test]
        public void WireIds_AreStable()
        {
            Assert.AreEqual(19, (int)EconomyReason.Garden);
            Assert.AreEqual(6, (int)WishParamKind.Crop);
            Assert.AreEqual(6, (int)ActivityId.Garden);
            Assert.AreEqual(150, (int)Services.Analytics.AnalyticsEventId.GardenPlant);
            Assert.AreEqual(151, (int)Services.Analytics.AnalyticsEventId.GardenHarvest);
            Assert.AreEqual(152, (int)Services.Analytics.AnalyticsEventId.GardenSpeedup);
        }

        [Test]
        public void TheTick_AndTheReads_DoNotAllocate()
        {
            var rig = GardenRig.Create(points: 230);
            rig.Give(Strawberry, 2);
            rig.Plant(0, Clover);
            rig.Plant(1, Strawberry);
            rig.G.Water(1);
            double sink = 0;
            void Frame()
            {
                rig.Clock.Advance(TimeSpan.FromSeconds(1));
                rig.G.Tick();
                for (int p = 0; p < rig.G.PlotCount; p++)
                {
                    sink += (int)rig.G.StateOf(p) + (int)rig.G.UnlockOf(p) + rig.G.StageOf(p) + rig.G.GrowthOf(p) + rig.G.SecondsLeft(p)
                            + rig.G.TotalSecondsOf(p) + rig.G.SpeedUpCost(p) + (rig.G.Watered(p) ? 1 : 0) + (rig.G.Helped(p) ? 1 : 0)
                            + rig.G.PlantOf(p).Value;
                }
                sink += rig.G.RipeCount + rig.G.SoonestRipe + rig.G.SpeedUpsLeftToday + rig.G.Level + rig.G.PointsInLevel + rig.G.PointsForNext;
                sink += rig.G.SeedsOf(Clover) + (rig.G.IsPlantOpen(Daisy) ? 1 : 0) + rig.G.PlantAt(3).MainCount;
                sink += rig.G.ChallengeHarvests + rig.G.ChallengeStarsSoFar + rig.G.ChallengeStars(0) + (rig.G.IsChallengeOpen(3) ? 1 : 0);
                sink += rig.G.Owned(GardenCatalog.Dyes[0].Id) + (rig.G.CanBuyPalette(GardenCatalog.Palettes[2].Id) ? 1 : 0);
                sink += rig.G.TryGetPlant(Carrot, out var carrot) ? carrot.GrowSeconds : 0;
            }
            Frame();
            Assert.That(Frame, Is.Not.AllocatingGCMemory());
            rig.AdvanceMinutes(30);
            Assert.That(Frame, Is.Not.AllocatingGCMemory(), "With a plant ripe.");
            Assert.Greater(sink, 0);
        }
    }
}
