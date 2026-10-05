using System.Collections.Generic;
using System.Reflection;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.UI;
using PocketWilds.Services.Localization;
using PocketWilds.Presentation;
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
    /// Gameplay frames of the vertical slice with PF_UI_HUD_Main on top (T17 review): first free egg,
    /// affordable egg, egg not affordable. The overlay canvas is switched to the capture camera for the
    /// render only; the scene is reopened from disk afterwards, nothing is saved.
    /// </summary>
    public static class HudCapture
    {
        [MenuItem("Pocket Wilds/Capture/HUD Frames", priority = 523)]
        public static void CaptureMenu() => Debug.Log("[HudCapture] " + Capture("T17", 6));

        public static string Capture(string prefix, int rabbits)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != WorldCapture.ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);

            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var temp = new List<GameObject>();
            var results = new List<string>();
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var chunk = WorldCapture.SceneChunk();
                    var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var hud = Object.FindAnyObjectByType<SanctuaryHud>();
                    var camera = Camera.main;
                    chunk.Bind(palettes);
                    entry.Lighting.Apply(Object.FindAnyObjectByType<Light>(), camera, QualityTier.Medium);
                    chunk.ApplyQuality(QualityTier.Medium);
                    WorldCapture.Populate(registry, palettes, chunk, rabbits, temp);

                    var canvas = hud.GetComponent<Canvas>();
                    var scaler = hud.GetComponent<CanvasScaler>();
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = camera.nearClipPlane + 0.1f;
                    hud.Bind(null);
                    var handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                    System.Action refresh = () =>
                    {
                        handle?.Invoke(scaler, null);
                        Canvas.ForceUpdateCanvases();
                    };

                    Frame(hud, camera, refresh, BigValue.Zero, BigValue.Zero, BigValue.Zero, true, true, $"{prefix}_hud_first", results);
                    Frame(hud, camera, refresh, BigValue.FromDouble(37), BigValue.FromDouble(1.2), BigValue.FromDouble(12), false, true, $"{prefix}_hud_affordable", results);
                    Frame(hud, camera, refresh, BigValue.FromDouble(4250), BigValue.FromDouble(86), BigValue.FromDouble(18400), false, false, $"{prefix}_hud_waiting", results);

                    // Mastery toast caught just after a level up (3 to 4 on a new variant).
                    var gain = new MasteryGain(ContentId.FromSource("species.rabbit"), MasteryReason.NewVariant, 15,
                        new MasteryProgress(3, 80, 88), new MasteryProgress(4, 7, 130), 0);
                    hud.SetLight(BigValue.FromDouble(37), BigValue.FromDouble(1.2));
                    hud.SetEgg(BigValue.FromDouble(12), false, true);
                    hud.ShowMastery(gain);
                    for (int i = 0; i < 9; i++)
                        hud.Tick(0.05f);
                    string masteryPath = $"{CaptureUtility.DefaultFolder}/{prefix}_hud_mastery.png";
                    CaptureUtility.Render(camera, 1080, 2340, masteryPath, 4, refresh);
                    results.Add(CaptureUtility.Render(camera, 1080, 2340, masteryPath, 4, refresh));
                    for (int i = 0; i < 60; i++)
                        hud.Tick(0.05f);

                    // Offline return screen over the HUD, in each shipped language.
                    var offline = Object.FindAnyObjectByType<OfflineReturnScreen>();
                    if (offline != null)
                    {
                        var offlineCanvas = offline.GetComponent<Canvas>();
                        var offlineScaler = offline.GetComponent<CanvasScaler>();
                        offlineCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                        offlineCanvas.worldCamera = camera;
                        offlineCanvas.planeDistance = camera.nearClipPlane + 0.05f;
                        System.Action refreshBoth = () =>
                        {
                            handle?.Invoke(scaler, null);
                            handle?.Invoke(offlineScaler, null);
                            Canvas.ForceUpdateCanvases();
                        };
                        var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
                        foreach (string language in UiPipeline.Languages)
                        {
                            var text = new LocalizationService(database.localization, language, SystemLanguage.English);
                            hud.SetLight(BigValue.FromDouble(4250), BigValue.FromDouble(2.3));
                            offline.Show(BigValue.FromDouble(24800), text, null);
                            for (int i = 0; i < 20; i++)
                                offline.Tick(0.05f);
                            string path = $"{CaptureUtility.DefaultFolder}/{prefix}_offline_{language}.png";
                            CaptureUtility.Render(camera, 1080, 2340, path, 4, refreshBoth);
                            results.Add(CaptureUtility.Render(camera, 1080, 2340, path, 4, refreshBoth));
                        }
                        offline.transform.Find("Content").gameObject.SetActive(false);
                    }

                    // Codex list and species sheet, with a small real collection.
                    var codexScreen = Object.FindAnyObjectByType<CodexScreen>();
                    if (codexScreen != null)
                    {
                        var codexCanvas = codexScreen.GetComponent<Canvas>();
                        var codexScaler = codexScreen.GetComponent<CanvasScaler>();
                        codexCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                        codexCanvas.worldCamera = camera;
                        codexCanvas.planeDistance = camera.nearClipPlane + 0.04f;
                        System.Action refreshCodex = () =>
                        {
                            handle?.Invoke(scaler, null);
                            handle?.Invoke(codexScaler, null);
                            Canvas.ForceUpdateCanvases();
                        };
                        var state = PlayerState.CreateNew(System.DateTimeOffset.UtcNow);
                        var rabbit = ContentId.FromSource("species.rabbit");
                        string[] owned = { "brown", "brown", "brown", "white", "white", "spotted" };
                        foreach (string variant in owned)
                        {
                            var id = ContentId.FromSource("variant.rabbit." + variant);
                            state.Collection.Add(new AnimalInstance { Species = rabbit, Variant = id, Seed = 1u });
                            state.Collection.DiscoveredVariants.Add(id);
                        }
                        state.Collection.DiscoveredSpecies.Add(rabbit);
                        state.Codex.SeenSpecies.Add(rabbit);
                        state.Codex.SeenVariants.Add(ContentId.FromSource("variant.rabbit.brown"));
                        state.Mastery.Species[rabbit] = new SpeciesMastery { Level = 3, Xp = 40 };
                        var mastery = new MasteryService(state, EconomyConfig.Default, null);
                        var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
                        foreach (string language in UiPipeline.Languages)
                        {
                            var text = new LocalizationService(database.localization, language, SystemLanguage.English);
                            var codex = new CodexService(state, registry, null, mastery);
                            codexScreen.Open(codex, text, null);
                            refreshCodex();
                            for (int i = 0; i < 10; i++)
                                codexScreen.Tick(0.05f);
                            string listPath = $"{CaptureUtility.DefaultFolder}/{prefix}_codex_list_{language}.png";
                            CaptureUtility.Render(camera, 1080, 2340, listPath, 4, refreshCodex);
                            results.Add(CaptureUtility.Render(camera, 1080, 2340, listPath, 4, refreshCodex));
                            codexScreen.OpenSheet(0);
                            string sheetPath = $"{CaptureUtility.DefaultFolder}/{prefix}_codex_sheet_{language}.png";
                            CaptureUtility.Render(camera, 1080, 2340, sheetPath, 4, refreshCodex);
                            results.Add(CaptureUtility.Render(camera, 1080, 2340, sheetPath, 4, refreshCodex));
                            codexScreen.CloseSheet();
                            state.Codex.SeenVariants.Remove(ContentId.FromSource("variant.rabbit.white"));
                            state.Codex.SeenVariants.Remove(ContentId.FromSource("variant.rabbit.spotted"));
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

        private static void Frame(SanctuaryHud hud, Camera camera, System.Action refresh, BigValue light, BigValue rate, BigValue cost,
            bool free, bool affordable, string name, List<string> results)
        {
            hud.SetLight(light, rate);
            hud.SetHatching(false);
            hud.SetEgg(cost, free, affordable);
            string path = $"{CaptureUtility.DefaultFolder}/{name}.png";
            CaptureUtility.Render(camera, 1080, 2340, path, 4, refresh);
            results.Add(CaptureUtility.Render(camera, 1080, 2340, path, 4, refresh));
        }
    }
}
