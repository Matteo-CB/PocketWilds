using System.Collections.Generic;
using System.Reflection;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.Services.Localization;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Capture
{
    /// <summary>
    /// T35B screen review in the real refuge: the egg pills under the hatch button (Basic chosen; Focus chosen with its
    /// targets, its name and × 10; Discovery chosen with « Gratuit » and its stock), the odds window of the Focus egg, and
    /// the summary card of a × 10 and of a × 3 (screen 028), in each shipped language. Overlay canvases are switched to
    /// the capture camera for the render; nothing is saved.
    /// </summary>
    public static class EggCapture
    {
        [MenuItem("Pocket Wilds/Capture/Egg Choice and Summary (T35B)", priority = 529)]
        public static void CaptureMenu() => Debug.Log("[EggCapture] " + Capture("T35B", 6));

        public static string Capture(string prefix, int rabbits)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != WorldCapture.ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var registry = database.CreateBuilder().Build();
            var temp = new List<GameObject>();
            var results = new List<string>();
            var handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var chunk = WorldCapture.SceneChunk();
                    var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var hud = Object.FindAnyObjectByType<RefugeHud>();
                    var modal = Object.FindAnyObjectByType<ModalScreen>();
                    var summary = Object.FindAnyObjectByType<RewardSummaryScreen>(FindObjectsInactive.Include);
                    var camera = Camera.main;
                    WorldCapture.FrameLikeGame(chunk);
                    chunk.Bind(palettes);
                    entry.Lighting.Apply(Object.FindAnyObjectByType<Light>(), camera, QualityTier.Medium);
                    chunk.ApplyQuality(QualityTier.Medium);
                    WorldCapture.Populate(registry, palettes, chunk, rabbits, temp);

                    var canvases = new List<(Canvas canvas, CanvasScaler scaler)>();
                    void Attach(Component screen, float distance)
                    {
                        if (screen == null)
                            return;
                        var canvas = screen.GetComponent<Canvas>();
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        canvas.planeDistance = camera.nearClipPlane + distance;
                        canvases.Add((canvas, screen.GetComponent<CanvasScaler>()));
                    }
                    Attach(hud, 0.1f);
                    Attach(modal, 0.03f);
                    Attach(summary, 0.03f);
                    System.Action refresh = () =>
                    {
                        foreach (var c in canvases)
                            handle?.Invoke(c.scaler, null);
                        Canvas.ForceUpdateCanvases();
                    };
                    void Shot(string name)
                    {
                        string path = $"{CaptureUtility.DefaultFolder}/{prefix}_{name}.png";
                        results.Add(CaptureUtility.Render(camera, 1080, 2340, path, 4, refresh));
                    }

                    var basic = registry.Eggs.Get(ContentId.FromSource("egg.grassland.basic"));
                    var focus = registry.Eggs.Get(ContentId.FromSource("egg.grassland.focus"));
                    var discovery = registry.Eggs.Get(ContentId.FromSource("egg.discovery"));
                    var slots = new[]
                    {
                        new EggSlot(basic.Id, basic.Palette[0], basic.Palette[1], -1),
                        new EggSlot(focus.Id, focus.Palette[0], focus.Palette[1], -1),
                        new EggSlot(discovery.Id, discovery.Palette[0], discovery.Palette[1], 3),
                    };
                    var targets = new[]
                    {
                        new PortraitSlot(ContentId.FromSource("species.rabbit"), ContentId.FromSource("variant.rabbit.white")),
                        new PortraitSlot(ContentId.FromSource("species.squirrel"), default),
                        new PortraitSlot(ContentId.FromSource("species.hedgehog"), default),
                    };
                    var bar = hud.EggChoice;
                    hud.Bind(null);
                    bar.Bind(null);
                    hud.SetLight(BigValue.FromDouble(18400), BigValue.FromDouble(34));

                    foreach (string language in UiPipeline.Languages)
                    {
                        var text = new LocalizationService(database.localization, language, SystemLanguage.English);

                        // Basic chosen: three pills, the Light price.
                        hud.SetPriceText(null);
                        hud.SetEgg(BigValue.FromDouble(873), false, true);
                        bar.SetSlots(slots, 3, 0);
                        bar.SetQuantity(1, false);
                        bar.SetTargets(targets, 0);
                        for (int i = 0; i < 12; i++) hud.Tick(0.05f);
                        Shot($"choice_basic_{language}");

                        // Focus chosen: its name for a moment, its three targets, × 10 and the price of ten.
                        hud.SetEgg(BigValue.FromDouble(41200), false, true);
                        bar.SetSlots(slots, 3, 1);
                        bar.SetQuantity(10, true);
                        bar.SetTargets(targets, 3);
                        bar.ShowName(text.Get(focus.DisplayKey));
                        for (int i = 0; i < 8; i++) hud.Tick(0.05f);
                        Shot($"choice_focus_{language}");

                        // Discovery chosen: « Gratuit » and the stock on its pill.
                        bar.SetSlots(slots, 3, 2);
                        bar.SetQuantity(3, true);
                        bar.SetTargets(targets, 0);
                        hud.SetPriceText(text.Get(EggText.Free));
                        bar.ShowName(text.Get(discovery.DisplayKey));
                        for (int i = 0; i < 8; i++) hud.Tick(0.05f);
                        Shot($"choice_discovery_{language}");
                        for (int i = 0; i < 40; i++) hud.Tick(0.05f);

                        // The Focus egg's odds: stars and words, targets, bonuses, the guarantee.
                        if (modal != null)
                        {
                            var odds = SampleOdds(focus);
                            modal.Show(ModalKind.Info, text.Get(focus.DisplayKey), RefugeController.EggOddsText(odds, focus, text, registry),
                                text.Get(FeedbackText.ModalOkButton), null, null);
                            for (int i = 0; i < 10; i++) modal.Tick(0.05f);
                            Shot($"odds_focus_{language}");
                            modal.Close();
                            for (int i = 0; i < 10; i++) modal.Tick(0.05f);
                        }

                        // The summary card of a × 10, then of a × 3.
                        if (summary != null)
                        {
                            var entries = SampleEntries(registry, text);
                            summary.Open(entries, 10, text, null);
                            for (int i = 0; i < 20; i++) summary.Tick(0.05f);
                            Shot($"summary10_{language}");
                            summary.Close();
                            for (int i = 0; i < 10; i++) summary.Tick(0.05f);
                            summary.Open(entries, 3, text, null);
                            for (int i = 0; i < 20; i++) summary.Tick(0.05f);
                            Shot($"summary3_{language}");
                            summary.Close();
                            for (int i = 0; i < 10; i++) summary.Tick(0.05f);
                        }
                    }
                }
                finally
                {
                    foreach (var go in temp)
                        Object.DestroyImmediate(go);
                    EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
                }
            }
            return string.Join(", ", results);
        }

        /// <summary>The Focus egg's table with three targets, the bonuses of a fresh biome and the guarantee.</summary>
        private static EggOdds SampleOdds(EggDefinition egg)
        {
            var odds = new EggOdds { Egg = egg.Id, Tier = egg.Tier, NewSpeciesBonus = 0.09, NewVariantBonus = 0.06, EggsToGuarantee = 14, PityHardLimit = 20 };
            double total = 0.0;
            for (int i = 0; i < egg.RarityWeights.Length; i++)
                total += egg.RarityWeights[i];
            for (int i = 0; i < egg.RarityWeights.Length && i < odds.RarityPercent.Length; i++)
                odds.RarityPercent[i] = total > 0.0 ? egg.RarityWeights[i] * 100.0 / total : 0.0;
            for (int i = 0; i < egg.Pool.Length && odds.TargetCount < 3; i++)
                odds.Targets[odds.TargetCount++] = egg.Pool[i].Species;
            return odds;
        }

        /// <summary>Ten newborns of every rarity, three of them new (two colors and a species).</summary>
        private static SummaryEntry[] SampleEntries(ContentRegistry registry, LocalizationService text)
        {
            string[] variants =
            {
                "variant.rabbit.white", "variant.squirrel.autumn", "variant.hedgehog.brown", "variant.rabbit.spotted", "variant.squirrel.red",
                "variant.rabbit.sakura", "variant.hedgehog.cream", "variant.rabbit.white", "variant.squirrel.red", "variant.hedgehog.brown",
            };
            var entries = new SummaryEntry[variants.Length];
            for (int i = 0; i < variants.Length; i++)
            {
                var id = ContentId.FromSource(variants[i]);
                var variant = registry.Variants.TryGet(id, out var v) ? v : null;
                var species = variant != null ? variant.Species : ContentId.FromSource("species.rabbit");
                var rarity = variant != null ? variant.Rarity : RarityTier.Common;
                entries[i] = new SummaryEntry
                {
                    Species = species,
                    Variant = id,
                    Rarity = rarity,
                    Name = registry.Species.TryGet(species, out var s) ? text.Get(s.DisplayKey) : "?",
                    RarityWord = text.Get(FeedbackText.RarityKey(rarity)),
                    New = i == 1 || i == 3 || i == 5,
                };
            }
            return entries;
        }
    }
}
