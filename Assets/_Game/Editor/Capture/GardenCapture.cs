using System;
using System.Collections.Generic;
using System.Reflection;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Activities;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.Services.Localization;
using PocketWilds.Simulation;
using PocketWilds.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Capture
{
    /// <summary>
    /// T43 « Jardin » review (doc 01 Contenu section 8, S-ACT-006; doc 05 sheet 24): agent GP's real set and plant stages and
    /// the real overlay prefab, driven by the real presenter in edit mode over a scripted IGarden (plots and clock set by
    /// hand), rendered through the set camera at 1080 × 2340 with the overlay on it. Frames (each language): the garden with a
    /// sprout (stage 1, watered, the neighbours helping), a stage 2 plant, a ripe plant, the Gems bed and the Club bed
    /// covered; the harvest moment (the plant flying to the basket, the products counting up, the level-up banner); the seed
    /// sheet on the bare bed; the Gems bed armed by a first tap. The fonts and the UI kit are lent from the project the way
    /// the scene lends them at run time. No effect player in edit mode (the rings and drops show in play). Nothing is saved.
    /// </summary>
    public static class GardenCapture
    {
        private const int Width = 1080;
        private const int Height = 2340;
        private const float Step = 1f / 30f;

        [MenuItem("Pocket Wilds/Capture/Garden (T43)", priority = 537)]
        public static void CaptureMenu() => Debug.Log("[GardenCapture] " + Capture("T43_garden"));

        public static string Capture(string prefix)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != WorldCapture.ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            var screen = AssetDatabase.LoadAssetAtPath<GameObject>(GardenScreenPipeline.PrefabPath);
            if (screen == null)
                return "no overlay prefab: run Pocket Wilds/Activities/Build Garden Screen (T43) first";
            var set = AssetDatabase.LoadAssetAtPath<GameObject>(GardenLayout.SetPrefabPath);
            if (set == null)
                return "no set prefab: run agent GP's garden set pipeline first (" + GardenLayout.SetPrefabPath + ")";
            EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
            // Loaded after the scene opens: opening a scene unloads unused assets, which would leave a dead reference.
            var plants = AssetDatabase.LoadAssetAtPath<GardenPlantVisuals>(GardenLayout.PlantsAssetPath);

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var registry = database.CreateBuilder().Build();
            // The set and the plants take their colours from the palette table (their WorldChunk bindings).
            var palettes = new PaletteTable(registry);
            var handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
            var texts = new List<(string language, LocalizationService text)>();
            foreach (string language in UiPipeline.Languages)
                texts.Add((language, new LocalizationService(database.localization, language, SystemLanguage.English)));
            var sprites = LoadAll<Sprite>("t:Sprite", "Assets/_Game/Art/UI/Kit", "Assets/_Game/Art/UI/Icons");
            var fonts = LoadAll<TMP_FontAsset>("t:TMP_FontAsset", "Assets/_Game/Art/UI/Fonts");
            var materials = LoadAll<Material>("t:Material", "Assets/_Game/Art/UI/Fonts");
            var shots = new List<string>();
            var problems = new List<string>();
            var notes = new List<string>();
            if (plants == null)
                notes.Add("no plant visuals yet (" + GardenLayout.PlantsAssetPath + "): beds show their soil");

            GardenPresenter presenter = null;
            var refugeHud = Object.FindAnyObjectByType<RefugeHud>(FindObjectsInactive.Include);
            if (refugeHud != null)
                refugeHud.gameObject.SetActive(false);
            try
            {
                foreach (var (language, text) in texts)
                {
                    var context = new GameContext(null, registry, null, null, null, null, null, palettes, null, null, null, null, localization: text);
                    var garden = new ScriptedGarden();
                    garden.Points = 85;
                    garden.SetGrowing(0, "plant.sweet_clover", 0.15, true);
                    garden.SetGrowing(1, "plant.daisy", 0.7, false);
                    garden.SetGrowing(2, "plant.wild_strawberry", 1.0, true);
                    presenter?.Dispose();
                    presenter = new GardenPresenter(context, garden, null, set, plants, screen);
                    presenter.OpenFromHub();
                    var view = presenter.View as GardenScreenView;
                    var camera = presenter.SetCamera;
                    if (view == null || camera == null)
                        return "the garden did not open (set or overlay incomplete: rebuild them)";
                    view.Lend(sprites, fonts, materials);
                    var canvas = view.Canvas;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.05f, 0.5f);

                    void Refresh()
                    {
                        handle?.Invoke(canvas.GetComponent<CanvasScaler>(), null);
                        Canvas.ForceUpdateCanvases();
                    }

                    void Run(float seconds)
                    {
                        int frames = Mathf.Max(1, Mathf.RoundToInt(seconds / Step));
                        for (int f = 0; f < frames; f++)
                            presenter.Tick(Step);
                    }

                    void Shot(string label)
                    {
                        string path = $"{CaptureUtility.DefaultFolder}/{prefix}_{label}.png";
                        for (int pass = 0; pass < 2; pass++)
                        {
                            string written = CaptureUtility.Render(camera, Width, Height, path, 4, () =>
                            {
                                Refresh();
                                presenter.Tick(0f);
                                Refresh();
                            });
                            if (pass == 1)
                                shots.Add(written);
                        }
                    }

                    // 1. The garden: a watered sprout helped by its neighbour, a stage 2 daisy, ripe strawberries swaying, two covered beds.
                    Refresh();
                    Run(0.4f);
                    if (presenter.StageShown(0) != 1 || presenter.StageShown(1) != 2 || presenter.StageShown(2) != 3)
                        problems.Add($"{language}: stages shown {presenter.StageShown(0)} {presenter.StageShown(1)} {presenter.StageShown(2)} (want 1 2 3)");
                    if (!view.BedLocked(3) || !view.BedLocked(4))
                        problems.Add(language + ": the Gems and Club beds are not covered");
                    Shot("garden_" + language);

                    // 2. The harvest moment: the strawberries hop and fly to the basket, the products count up, level 3.
                    presenter.TapBed(2);
                    Run(0.38f);
                    if (presenter.Harvests != 1)
                        problems.Add(language + ": no harvest");
                    if (view.ProductsShown == 0)
                        problems.Add(language + ": no product chip");
                    if (!view.BannerShown)
                        problems.Add(language + ": no level-up banner (85 + the harvest's points pass 90)");
                    Shot("harvest_" + language);

                    // 3. The seed sheet on the bed just picked.
                    Run(1.2f);
                    presenter.TapBed(2);
                    Run(0.4f);
                    if (!presenter.SeedsOpen)
                        problems.Add(language + ": the seed sheet did not open");
                    Shot("seeds_" + language);

                    // 4. The Gems bed armed by a first tap (« Touche encore pour l'ouvrir »).
                    presenter.CloseSeeds();
                    Run(0.3f);
                    presenter.TapBed(3);
                    Run(0.25f);
                    Shot("locked_" + language);
                }
            }
            catch (Exception e)
            {
                problems.Add(e.GetType().Name + ": " + e.Message + " " + e.StackTrace);
            }
            finally
            {
                presenter?.Dispose();
                EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
            }
            return string.Join(", ", shots) + (notes.Count > 0 ? " [" + string.Join("; ", notes) + "]" : "")
                   + (problems.Count > 0 ? " PROBLEMS: " + string.Join("; ", problems) : "");
        }

        private static List<T> LoadAll<T>(string filter, params string[] folders) where T : Object
        {
            var found = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets(filter, folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is T typed)
                        found.Add(typed);
            }
            return found;
        }

        /// <summary>
        /// A scripted IGarden for the review (the same as GardenPlayTests'): the doc's starter plants and two biome plants
        /// (table 8.2), growth from a clock the caller sets, watering −10 %, the stage rule (40 %, 100 %), the harvest points
        /// (minutes / 10, at least 3, +2 a star above the first) and the levels of table 8.3; products go to an inventory when given.
        /// </summary>
        internal sealed class ScriptedGarden : IGarden
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
