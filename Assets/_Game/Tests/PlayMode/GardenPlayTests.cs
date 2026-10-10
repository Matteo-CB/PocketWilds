using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T43 « Jardin » in the real game (doc 01 Contenu section 8, S-ACT-006; doc 05 sheet 24): the garden opens from the hub
    /// over a hidden refuge, its set (agent GP's PF_Activity_Garden_Set), plant stages and overlay load from the Addressables
    /// group Activity_Garden and reference nothing of the scene (fonts, kit and the scene's icons borrowed); the first visit's
    /// paw shows a bare bed, the Sweet Clover packet, the can and the sprout; a seed is planted (its stage 1 view), watered,
    /// grows to stage 2 on the clock, ripens with « Finir maintenant », and is harvested (it flies to the basket, the products
    /// count up, the inventory and the garden points grow); « Tout récolter » picks every ripe bed; nothing allocates per frame
    /// while the garden shows; closing releases the set, the plants and the overlay and brings the refuge and the hub back.
    /// The first test plays over a scripted IGarden (its clock set by the test); the second over the shipped garden service
    /// when the scene wires it (<see cref="ActivityPresenter.GardenService"/>).
    /// </summary>
    public class GardenPlayTests
    {
        private static readonly ContentId SweetClover = ContentId.FromSource("plant.sweet_clover");
        private static readonly ContentId CloverFlower = ContentId.FromSource("crop.clover_flower");

        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "PocketWildsPlayTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            foreach (var bootstrap in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(bootstrap.gameObject);
            try
            {
                Directory.Delete(_saveDir, true);
            }
            catch (IOException)
            {
            }
        }

        [UnityTest]
        public IEnumerator Garden_PlantWaterRipenHarvest_OnItsSet()
        {
            SeedSave(false);
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var activities = entry.Activities;
            Assert.IsNotNull(activities, "The scene runs the activities.");
            yield return ClearScreens(entry);

            var previous = activities.Garden;
            var garden = new ScriptedGarden { Inventory = context.State.Inventory };
            var presenter = new GardenPresenter(context, garden, activities);
            try
            {
                Assert.AreSame(presenter, activities.Garden, "The garden registers itself with the hub.");
                Assert.AreEqual(ActivityCardState.New, presenter.CardStatus(out _), "« Nouveau » before the first sprout.");
                Assert.IsTrue(presenter.WantsDot, "A dot on Jouer before the first sprout.");

                // The set, the plants and the overlay load from Activity_Garden over a hidden refuge.
                presenter.OpenFromHub();
                yield return WaitFor(() => presenter.View != null && presenter.View.Shown, 8f, "The garden loads in a moment.");
                var view = (GardenScreenView)presenter.View;
                Assert.IsTrue(presenter.AssetsLoaded, "The set, the plants and the overlay come from their Addressables group.");
                Assert.IsTrue(view.Valid);
                Assert.IsTrue(activities.IsOpen, "World touches wait while the garden shows.");
                Assert.IsFalse(entry.CameraRig.Camera.enabled, "The refuge does not render under the garden.");
                Assert.IsNotNull(presenter.SetCamera);
                Assert.IsTrue(presenter.SetCamera.enabled, "The set camera draws the garden.");
                Assert.Less(presenter.Set.transform.position.y, -100f, "The set lives far below the refuge.");
                Assert.AreEqual(GardenLayout.SortingOrder, view.Canvas.sortingOrder);
#if UNITY_EDITOR
                foreach (string dependency in UnityEditor.AssetDatabase.GetDependencies(GardenLayout.ScreenPrefabPath, true))
                {
                    StringAssert.DoesNotContain("/Fonts/", dependency, "No font in the overlay's bundle (borrowed from the scene).");
                    StringAssert.DoesNotContain("/Art/UI/Kit/", dependency, "No kit sprite in the overlay's bundle.");
                    if (dependency.Contains("/Art/UI/Icons/"))
                        Assert.IsTrue(GardenIcon(Path.GetFileNameWithoutExtension(dependency)), dependency + ": only the garden's own icons are bundled.");
                }
#endif
                foreach (var label in view.BorrowedLabels)
                    Assert.IsNotNull(label.font, label.name + " got its font from the scene.");
                for (int plot = 0; plot < 3; plot++)
                    Assert.IsTrue(view.BedShown(plot), "Each owned bed has its label.");
                Assert.IsTrue(view.BedLocked(3) && view.BedLocked(4), "The Gems and Club beds wear their sign.");

                // The first visit: the paw on a bare bed, the Sweet Clover packet, the can, the sprout.
                yield return null;
                Assert.AreEqual(1, presenter.TutorialStep, "The paw shows a bare bed.");
                Assert.IsTrue(view.PawShown);
                Assert.AreEqual(0, presenter.PickBed(presenter.BedScreenPoint(0)), "A tap on the bed's plant point picks that bed (no raycast).");
                presenter.PointerDown(presenter.BedScreenPoint(0));
                Assert.IsTrue(presenter.SeedsOpen && view.SeedsShown, "A bare bed opens the seed sheet.");
                Assert.AreEqual(0, presenter.SeedPlot);
                Assert.AreEqual(2, presenter.TutorialStep, "The paw moves to the Sweet Clover packet.");
                int packet = -1;
                for (int cell = 0; cell < presenter.PacketCount; cell++)
                    if (presenter.PacketAt(cell) == SweetClover)
                        packet = cell;
                Assert.GreaterOrEqual(packet, 0, "The Sweet Clover packet is offered.");
                Assert.AreEqual(SweetClover, presenter.PacketAt(0), "The open plants with seeds come first.");
                presenter.TapPacket(packet);
                Assert.AreEqual(1, garden.Plants, "The seed is planted.");
                Assert.IsFalse(presenter.SeedsOpen);
                yield return null;
                Assert.AreEqual(1, presenter.StageShown(0), "The sprout's stage 1 shows.");
                Assert.IsNotNull(presenter.PlantViewOf(0), "Agent GP's stage model stands in the bed.");
                StringAssert.Contains("30", view.BedText(0), "« Prêt dans 30 min » before watering.");
                Assert.AreEqual(3, presenter.TutorialStep, "The paw moves to the can.");
                presenter.TapCan();
                Assert.IsTrue(presenter.CanArmed);
                Assert.AreEqual(4, presenter.TutorialStep, "The paw moves to the sprout.");
                presenter.TapBed(0);
                Assert.AreEqual(1, garden.Waterings, "The can waters the sprout.");
                Assert.IsFalse(presenter.CanArmed);
                Assert.AreEqual(0, presenter.TutorialStep, "No paw once the sprout is watered.");
                yield return null;
                Assert.IsTrue(view.BedDrop(0), "The drop shows on the watered bed.");
                Assert.AreNotEqual(ActivityCardState.New, presenter.CardStatus(out string status));
                Assert.IsNotNull(status, "The hub card shows the soonest ripening.");

                // No allocation per frame while the garden shows (the time's text unchanged inside a minute).
                garden.Advance(5.0);
                for (int f = 0; f < 60; f++)
                    Frame(presenter, garden);
                string shown = view.BedText(0);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int f = 0; f < 120; f++)
                    Frame(presenter, garden);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0, allocated, "The garden allocates nothing per frame.");
                Assert.AreEqual(shown, view.BedText(0), "« Prêt dans 27 min » rewritten only when it changes.");

                // Stage 2 on the clock (40 % of the growth), then « Finir maintenant » ripens it.
                garden.Advance(garden.TotalSecondsOf(0) * 0.5);
                Frame(presenter, garden);
                Assert.AreEqual(2, presenter.StageShown(0), "The plant reaches stage 2 on the clock.");
                presenter.TapBed(0);
                Assert.AreEqual(0, presenter.Selected, "A growing bed is selected (« Finir maintenant »).");
                presenter.SpeedUp();
                Assert.AreEqual(1, garden.SpeedUps);
                Frame(presenter, garden);
                Assert.AreEqual(3, presenter.StageShown(0), "Ripe: stage 3.");
                StringAssert.Contains(context.Localization.Get(GardenText.Ready), view.BedText(0));

                // The harvest: granted once, the plant flies to the basket, the products count up, the points grow.
                int flowers = context.State.Inventory.Count(CloverFlower);
                int levelPoints = garden.Points;
                presenter.TapBed(0);
                Assert.AreEqual(1, presenter.Harvests, "TryHarvest once.");
                Assert.AreEqual(1, presenter.FlightsActive, "The plant hops and flies to the basket.");
                Assert.Greater(context.State.Inventory.Count(CloverFlower), flowers, "The clover flowers reach the inventory.");
                Assert.Greater(garden.Points, levelPoints, "The harvest gives garden points.");
                yield return null;
                Assert.Greater(view.ProductsShown, 0, "The products count up on the overlay.");
                yield return new WaitForSecondsRealtime(0.9f);
                Assert.AreEqual(0, presenter.FlightsActive, "The flight lands in 700 ms.");
                Assert.AreEqual(GardenPlotState.Empty, garden.StateOf(0));

                // « Tout récolter »: every ripe bed, 120 ms apart.
                garden.SetGrowing(0, "plant.sweet_clover", 1.0, true);
                garden.SetGrowing(1, "plant.daisy", 1.0, false);
                garden.SetGrowing(2, "plant.round_carrot", 1.0, true);
                yield return null;
                yield return null;
                int made = presenter.PlantViewsMade;
                presenter.HarvestAll();
                Assert.IsTrue(presenter.Harvesting, "The beds are picked one after the other.");
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.AreEqual(4, presenter.Harvests, "« Tout récolter » picked the three ripe beds.");
                Assert.AreEqual(0, garden.RipeCount);
                Assert.AreEqual(made, presenter.PlantViewsMade, "Harvesting makes no new plant view.");

                // Closing releases the set, the plants and the overlay; the refuge and the hub come back.
                presenter.CloseScreen();
                yield return null;
                Assert.IsFalse(presenter.IsOpen);
                Assert.IsFalse(presenter.AssetsLoaded, "The garden's bundle is released.");
                Assert.IsNull(presenter.Set, "The set is gone.");
                Assert.IsTrue(entry.CameraRig.Camera.enabled);
                Assert.IsTrue(activities.HubOpen, "The hub comes back on close.");
                activities.CloseActivities();
            }
            finally
            {
                presenter.Dispose();
                activities.Garden = previous;
            }
        }

        [UnityTest]
        public IEnumerator Garden_ShippedService_PlantsAndHarvestsIntoTheInventory()
        {
            SeedSave(true);
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var activities = entry.Activities;
            yield return ClearScreens(entry);
            var presenter = activities.Garden;
            if (presenter == null)
                Assert.Ignore("The scene does not wire the garden yet (ActivityPresenter.GardenService = context.Garden).");
            var garden = presenter.Service;
            if (!garden.IsOpen)
                Assert.Inconclusive("The seeded save (refuge level 8, day 3) does not open the garden: adjust SeedSave to the service's rule.");

            presenter.OpenFromHub();
            yield return WaitFor(() => presenter.View != null && presenter.View.Shown, 8f, "The garden loads in a moment.");
            int plot = -1;
            for (int i = 0; i < garden.PlotCount; i++)
                if (garden.StateOf(i) == GardenPlotState.Empty)
                {
                    plot = i;
                    break;
                }
            Assert.GreaterOrEqual(plot, 0, "A bare bed.");
            int seeds = garden.SeedsOf(SweetClover);
            Assert.Greater(seeds, 0, "The seeded Sweet Clover seeds are in the inventory.");
            presenter.TapBed(plot);
            int packet = -1;
            for (int cell = 0; cell < presenter.PacketCount; cell++)
                if (presenter.PacketAt(cell) == SweetClover)
                    packet = cell;
            presenter.TapPacket(packet);
            Assert.AreEqual(GardenPlotState.Growing, garden.StateOf(plot), "Planted.");
            Assert.AreEqual(seeds - 1, garden.SeedsOf(SweetClover), "One seed used.");
            presenter.TapCan();
            presenter.TapBed(plot);
            Assert.IsTrue(garden.Watered(plot), "Watered.");
            presenter.TapBed(plot);
            presenter.SpeedUp();
            Assert.AreEqual(GardenPlotState.Ripe, garden.StateOf(plot), "« Finir maintenant » ripens it.");
            yield return null;
            int flowers = context.State.Inventory.Count(CloverFlower);
            int level = garden.Level, points = garden.PointsInLevel;
            presenter.TapBed(plot);
            Assert.AreEqual(1, presenter.Harvests);
            var harvest = presenter.LastHarvest;
            Assert.AreEqual(SweetClover, harvest.Plant);
            Assert.GreaterOrEqual(harvest.Stars, 2, "Watered: 2 stars at least.");
            Assert.Greater(context.State.Inventory.Count(CloverFlower), flowers, "The clover flowers reach the inventory.");
            Assert.IsTrue(garden.Level > level || garden.PointsInLevel > points, "The harvest gives garden points.");
            presenter.CloseScreen();
            yield return null;
            Assert.IsFalse(presenter.AssetsLoaded, "Released on close.");
        }

        private static bool GardenIcon(string name) =>
            name.StartsWith("ICO_Seed_", StringComparison.Ordinal) || name.StartsWith("ICO_Crop_", StringComparison.Ordinal)
            || name.StartsWith("ICO_Dye_", StringComparison.Ordinal) || name.StartsWith("ICO_Ingredient_", StringComparison.Ordinal)
            || name.StartsWith("ICO_WateringCan", StringComparison.Ordinal);

        /// <summary>One frame of the open garden as the game runs it (the clock, the presenter, the canvases' rebuild).</summary>
        private static void Frame(GardenPresenter presenter, ScriptedGarden garden)
        {
            garden.Advance(1.0 / 60.0);
            presenter.Tick(1f / 60f);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>A save with a few animals; with <paramref name="garden"/>, the garden's conditions (refuge level 8, day 3), seeds and Gems.</summary>
        private void SeedSave(bool garden)
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            string[] species = { "species.rabbit", "species.hedgehog", "species.squirrel" };
            string[] variants = { "variant.rabbit.brown", "variant.hedgehog.brown", "variant.squirrel.red" };
            for (int i = 0; i < species.Length; i++)
            {
                var s = ContentId.FromSource(species[i]);
                var v = ContentId.FromSource(variants[i]);
                state.Collection.Add(new AnimalInstance { Species = s, Variant = v, Rarity = RarityTier.Common, Seed = SeedHash.Mix((uint)i + 1u, 0x47415244u) });
                state.Collection.DiscoveredSpecies.Add(s);
                state.Collection.DiscoveredVariants.Add(v);
            }
            if (garden)
            {
                state.Progression.Refuge.Level = 8;
                state.Progression.Refuge.LastRewardedLevel = 8;
                state.DailyVisit.LoginDays = 3;
                state.Inventory.Add(SweetClover, 5);
                state.Wallet.Gems = 1000;
            }
            Assert.IsTrue(service.FlushNow());
            service.Dispose();
        }

        private IEnumerator Boot(Action<GameBootstrap> ready)
        {
#if UNITY_EDITOR
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = _saveDir,
                ContentOverride = database.CreateBuilder().Build(),
                LocalizationOverride = database.localization.ToArray(),
                VisualCatalogOverride = catalog,
                LoadTargetScene = true,
                ClockOverride = new FakeGameClock(new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero)),
            };
#endif
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            float timeout = Time.realtimeSinceStartup + 60f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                if (Time.realtimeSinceStartup > timeout)
                    Assert.Fail("Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            ready(bootstrap);
        }

        /// <summary>Closes what a boot may open over the refuge: the offline return card, a refuge level's reward card, Today.</summary>
        private static IEnumerator ClearScreens(VerticalSliceEntry entry)
        {
            var refuge = entry.Refuge;
            float until = Time.realtimeSinceStartup + 8f;
            bool collected = false;
            while (refuge.ModalOpen && Time.realtimeSinceStartup < until)
            {
                var offline = Object.FindAnyObjectByType<OfflineReturnScreen>();
                if (refuge.OfflineScreenOpen && !collected && offline != null && offline.IsOpen)
                {
                    yield return new WaitForSecondsRealtime(0.35f);
                    offline.GetComponentInChildren<UnityEngine.UI.Button>(true).onClick.Invoke();
                    collected = true;
                }
                var progression = entry.ProgressionScreens;
                if (progression != null && progression.RewardWaiting && !progression.IsOpen)
                    progression.ShowPendingRewards();
                var card = Object.FindAnyObjectByType<LevelRewardScreen>();
                if (card != null && card.IsOpen)
                    card.ClaimButton.onClick.Invoke();
                if (entry.DailyVisit != null && entry.DailyVisit.ScreenOpen)
                    entry.DailyVisit.CloseToday();
                yield return null;
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string message)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until)
                    Assert.Fail(message);
                yield return null;
            }
        }

        /// <summary>
        /// A scripted IGarden (the same as GardenCapture's): the doc's starter plants and two biome plants
        /// (table 8.2), growth from a clock the caller sets, watering −10 %, the stage rule (40 %, 100 %), the harvest points
        /// (minutes / 10, at least 3, +2 a star above the first) and the levels of table 8.3; products go to an inventory when given.
        /// </summary>
        private sealed class ScriptedGarden : IGarden
        {
            private static readonly int[] Thresholds = { 0, 30, 90, 200, 380, 650, 1000, 1500, 2200, 3000 };

            private static readonly (string key, GardenPlantKind kind, int level, string biome, double minutes, string main, int count, string side, string dye)[] Table =
            {
                ("plant.sweet_clover", GardenPlantKind.Flower, 1, null, 30, "crop.clover_flower", 3, "ingredient.flower_sugar", "dye.clover_green"),
                ("plant.wild_strawberry", GardenPlantKind.Fruit, 1, null, 60, "ingredient.strawberry", 4, null, "dye.strawberry_pink"),
                ("plant.daisy", GardenPlantKind.Flower, 1, null, 45, "crop.daisy", 3, "ingredient.flower_sugar", "dye.daisy_cream"),
                ("plant.round_carrot", GardenPlantKind.Vegetable, 1, null, 90, "crop.round_carrot", 3, null, "dye.carrot_orange"),
                ("plant.poppy", GardenPlantKind.Flower, 2, null, 60, "crop.poppy", 3, null, "dye.poppy_red"),
                ("plant.blueberry", GardenPlantKind.Fruit, 2, null, 120, "ingredient.blueberry", 5, null, "dye.blueberry_blue"),
                ("plant.lavender", GardenPlantKind.Flower, 3, null, 150, "crop.lavender", 4, "ingredient.flower_sugar", "dye.lavender_lilac"),
                ("plant.golden_wheat", GardenPlantKind.Grain, 3, null, 180, "ingredient.wheat_flour", 4, null, "dye.wheat_gold"),
                ("plant.meadow_indigo", GardenPlantKind.Flower, 4, null, 240, "crop.indigo_flower", 2, null, "dye.meadow_indigo"),
                ("plant.sunflower", GardenPlantKind.Flower, 5, null, 300, "crop.sunflower", 2, null, "dye.sun_yellow"),
                ("plant.dwarf_apple", GardenPlantKind.Fruit, 6, null, 360, "ingredient.apple", 4, null, "dye.apple_green"),
                ("plant.round_melon", GardenPlantKind.Fruit, 7, null, 480, "crop.melon", 2, null, "dye.melon_peach"),
                ("plant.sweet_pea", GardenPlantKind.Flower, 0, "biome.grassland", 40, "crop.sweet_pea_flower", 3, "ingredient.flower_sugar", "dye.powder_pink"),
                ("plant.hazel", GardenPlantKind.Fruit, 0, "biome.forest", 240, "ingredient.hazelnut", 6, null, "dye.hazel_brown"),
            };

            private readonly GardenPlant[] _plants = new GardenPlant[Table.Length];
            private readonly ContentId[] _plot = new ContentId[GardenLayout.PlotCount];
            private readonly double[] _start = new double[GardenLayout.PlotCount];
            private readonly double[] _total = new double[GardenLayout.PlotCount];
            private readonly bool[] _watered = new bool[GardenLayout.PlotCount];
            private readonly Dictionary<ContentId, int> _seeds = new Dictionary<ContentId, int>();
            private int _challenge = -1;

            public ScriptedGarden()
            {
                for (int i = 0; i < Table.Length; i++)
                {
                    var t = Table[i];
                    _plants[i] = new GardenPlant(ContentId.FromSource(t.key), t.key, t.kind, t.level, t.biome != null ? ContentId.FromSource(t.biome) : ContentId.None,
                        t.minutes * 60.0, ContentId.FromSource(t.main), t.count, t.side != null ? ContentId.FromSource(t.side) : ContentId.None, t.side != null ? 1 : 0,
                        ContentId.FromSource(t.dye), t.key == "plant.meadow_indigo" ? 2 : 1);
                    _seeds[_plants[i].Id] = i < 4 || i == 12 ? 5 : 0;
                }
            }

            /// <summary>Seconds on the scripted clock.</summary>
            public double Now { get; set; }

            public int Points { get; set; }

            public int OwnedPlots { get; set; } = 3;

            public long Gems { get; set; } = 1000;

            public InventoryState Inventory { get; set; }

            public int Plants { get; private set; }

            public int Waterings { get; private set; }

            public int SpeedUps { get; private set; }

            public void Advance(double seconds) => Now += seconds;

            public void SetSeeds(string plant, int count) => _seeds[ContentId.FromSource(plant)] = count;

            /// <summary>A plot growing <paramref name="plant"/> at <paramref name="growth"/> (0 to 1; 1: ripe).</summary>
            public void SetGrowing(int plot, string plant, double growth, bool watered)
            {
                var id = ContentId.FromSource(plant);
                TryGetPlant(id, out var info);
                _plot[plot] = id;
                _watered[plot] = watered;
                _total[plot] = info.GrowSeconds * (watered ? 0.9 : 1.0);
                _start[plot] = Now - _total[plot] * growth;
                PlotChanged?.Invoke(plot);
            }

            public bool IsOpen => true;
            public int PlotCount => GardenLayout.PlotCount;

            public GardenPlotState StateOf(int plot)
            {
                if (plot >= OwnedPlots)
                    return GardenPlotState.Locked;
                if (_plot[plot] == ContentId.None)
                    return GardenPlotState.Empty;
                return GrowthOf(plot) >= 1f ? GardenPlotState.Ripe : GardenPlotState.Growing;
            }

            public GardenPlotUnlock UnlockOf(int plot) => plot < OwnedPlots ? GardenPlotUnlock.None : plot == 3 ? GardenPlotUnlock.Gems : GardenPlotUnlock.Club;
            public long PlotPrice => 400;

            public bool TryBuyPlot()
            {
                if (OwnedPlots != 3 || Gems < PlotPrice)
                    return false;
                Gems -= PlotPrice;
                OwnedPlots = 4;
                PlotChanged?.Invoke(3);
                return true;
            }

            public ContentId PlantOf(int plot) => _plot[plot];

            public int StageOf(int plot)
            {
                if (_plot[plot] == ContentId.None)
                    return 0;
                float growth = GrowthOf(plot);
                return growth >= 1f ? 3 : growth >= 0.4f ? 2 : 1;
            }

            public float GrowthOf(int plot) =>
                _plot[plot] == ContentId.None ? 0f : _total[plot] <= 0.0 ? 1f : Mathf.Clamp01((float)((Now - _start[plot]) / _total[plot]));

            public double SecondsLeft(int plot) => _plot[plot] == ContentId.None ? 0.0 : Math.Max(0.0, Math.Min(_total[plot], _start[plot] + _total[plot] - Now));
            public double TotalSecondsOf(int plot) => _total[plot];
            public bool Watered(int plot) => _watered[plot];

            public bool Helped(int plot)
            {
                if (_plot[plot] == ContentId.None || !_watered[plot])
                    return false;
                for (int side = -1; side <= 1; side += 2)
                {
                    int other = plot + side;
                    if (other >= 0 && other < OwnedPlots && _plot[other] != ContentId.None && _plot[other] != _plot[plot])
                        return true;
                }
                return false;
            }

            public int RipeCount
            {
                get
                {
                    int n = 0;
                    for (int plot = 0; plot < OwnedPlots; plot++)
                        if (StateOf(plot) == GardenPlotState.Ripe)
                            n++;
                    return n;
                }
            }

            public double SoonestRipe
            {
                get
                {
                    double best = -1.0;
                    for (int plot = 0; plot < OwnedPlots; plot++)
                        if (StateOf(plot) == GardenPlotState.Growing && (best < 0.0 || SecondsLeft(plot) < best))
                            best = SecondsLeft(plot);
                    return best;
                }
            }

            public int PlantCount => _plants.Length;
            public GardenPlant PlantAt(int index) => _plants[index];

            public bool TryGetPlant(ContentId plant, out GardenPlant info)
            {
                for (int i = 0; i < _plants.Length; i++)
                    if (_plants[i].Id == plant)
                    {
                        info = _plants[i];
                        return true;
                    }
                info = default;
                return false;
            }

            public bool IsPlantOpen(ContentId plant) =>
                TryGetPlant(plant, out var info) && (info.Level > 0 ? info.Level <= Level : info.Biome == ContentId.FromSource("biome.grassland"));

            public int SeedsOf(ContentId plant) => _seeds.TryGetValue(plant, out int n) ? n : 0;

            public GardenPlantResult Plant(int plot, ContentId plant)
            {
                if (StateOf(plot) != GardenPlotState.Empty)
                    return GardenPlantResult.PlotNotEmpty;
                if (!IsPlantOpen(plant))
                    return GardenPlantResult.PlantLocked;
                if (SeedsOf(plant) <= 0)
                    return GardenPlantResult.NoSeed;
                _seeds[plant] = SeedsOf(plant) - 1;
                TryGetPlant(plant, out var info);
                _plot[plot] = plant;
                _watered[plot] = false;
                _total[plot] = info.GrowSeconds;
                _start[plot] = Now;
                Plants++;
                PlotChanged?.Invoke(plot);
                return GardenPlantResult.Planted;
            }

            public bool Water(int plot)
            {
                if (StateOf(plot) != GardenPlotState.Growing || _watered[plot])
                    return false;
                TryGetPlant(_plot[plot], out var info);
                double growth = GrowthOf(plot);
                _watered[plot] = true;
                _total[plot] = Math.Max(1.0, _total[plot] - info.GrowSeconds * 0.1);
                _start[plot] = Now - _total[plot] * Math.Min(growth, 0.999);
                Waterings++;
                PlotChanged?.Invoke(plot);
                return true;
            }

            public int SpeedUpCost(int plot) => Math.Max(3, (int)Math.Ceiling(SecondsLeft(plot) / 600.0));
            public int SpeedUpsLeftToday => 3 - SpeedUps;

            public bool TrySpeedUp(int plot)
            {
                if (StateOf(plot) != GardenPlotState.Growing || SpeedUpsLeftToday <= 0 || Gems < SpeedUpCost(plot))
                    return false;
                Gems -= SpeedUpCost(plot);
                _start[plot] = Now - _total[plot];
                SpeedUps++;
                PlotChanged?.Invoke(plot);
                return true;
            }

            public bool TryHarvest(int plot, out GardenHarvest harvest)
            {
                harvest = default;
                if (StateOf(plot) != GardenPlotState.Ripe)
                    return false;
                TryGetPlant(_plot[plot], out var info);
                byte stars = (byte)(Helped(plot) ? 3 : _watered[plot] ? 2 : 1);
                int before = Level;
                int points = Math.Max(3, (int)Math.Round(info.GrowSeconds / 600.0)) + 2 * (stars - 1);
                Points += points;
                harvest = new GardenHarvest
                {
                    Plot = plot, Plant = info.Id, Stars = stars, Watered = _watered[plot], Helped = stars == 3, Main = info.Main,
                    MainCount = info.MainCount + (stars == 3 ? 1 : 0), Side = info.Side, SideCount = info.SideCount, Dye = DyesOpen ? info.Dye : ContentId.None,
                    Dyes = DyesOpen ? info.DyeCount : 0, SeedsBack = 1, Points = points, LevelBefore = before, LevelAfter = Level,
                };
                Inventory?.Add(harvest.Main, harvest.MainCount);
                Inventory?.Add(harvest.Side, harvest.SideCount);
                Inventory?.Add(harvest.Dye, harvest.Dyes);
                _seeds[info.Id] = SeedsOf(info.Id) + harvest.SeedsBack;
                if (_challenge >= 0)
                {
                    ChallengeHarvests++;
                    ChallengeStarsSoFar += stars;
                }
                _plot[plot] = ContentId.None;
                _watered[plot] = false;
                PlotChanged?.Invoke(plot);
                return true;
            }

            public int Level
            {
                get
                {
                    int level = 1;
                    for (int i = 1; i < Thresholds.Length; i++)
                        if (Points >= Thresholds[i])
                            level = i + 1;
                    return level;
                }
            }

            public int PointsInLevel => Points - Thresholds[Level - 1];
            public int PointsForNext => Level >= Thresholds.Length ? 0 : Thresholds[Level] - Thresholds[Level - 1];
            public bool DyesOpen => Level >= 4;
            public int ChallengeCount => 10;
            public int ActiveChallenge => _challenge;
            public bool IsChallengeOpen(int challenge) => challenge != 8 || DyesOpen;

            public bool StartChallenge(int challenge)
            {
                if (!IsChallengeOpen(challenge))
                    return false;
                _challenge = challenge;
                ChallengeHarvests = 0;
                ChallengeStarsSoFar = 0;
                return true;
            }

            public void StopChallenge() => _challenge = -1;
            public int ChallengeHarvests { get; private set; }
            public int ChallengeHarvestsNeeded(int challenge) => challenge == 8 ? 5 : challenge == 9 ? 6 : challenge == 0 || challenge == 4 || challenge == 6 ? 2 : 3;
            public int ChallengeStarsSoFar { get; private set; }
            public int ChallengeThreshold(int challenge, int stars) => ChallengeHarvestsNeeded(challenge) * stars;
            public byte ChallengeStars(int challenge) => (byte)(challenge == 0 ? 2 : 0);
            public event Action<int> PlotChanged;
        }
    }
}
