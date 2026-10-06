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
    /// T34A screen review in the real refuge: reveal banners (new species, Legendary), a warning toast, the tutorial
    /// hand on the egg, collect motes in flight, the animal sheet, the egg odds modal and the splash, in each shipped
    /// language, plus the HUD and the sheet on three screen shapes (Gate 12: small 16:9 phone, tall 19.5:9 phone,
    /// 4:3 tablet). Overlay canvases are switched to the capture camera for the render; nothing is saved.
    /// </summary>
    public static class FeedbackCapture
    {
        private static readonly (string name, int width, int height)[] Shapes =
        {
            ("tall", 1080, 2340),
            ("small", 720, 1280),
            ("tablet", 1536, 2048),
        };

        [MenuItem("Pocket Wilds/Capture/Feedback Screens (T34A)", priority = 527)]
        public static void CaptureMenu() => Debug.Log("[FeedbackCapture] " + Capture("T34A", 8));

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
                    var sheet = Object.FindAnyObjectByType<AnimalSheetScreen>();
                    var modal = Object.FindAnyObjectByType<ModalScreen>();
                    var camera = Camera.main;
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
                    Attach(sheet, 0.06f);
                    Attach(modal, 0.03f);
                    System.Action refresh = () =>
                    {
                        foreach (var c in canvases)
                            handle?.Invoke(c.scaler, null);
                        Canvas.ForceUpdateCanvases();
                    };
                    void Shot(string name, int width = 1080, int height = 2340)
                    {
                        string path = $"{CaptureUtility.DefaultFolder}/{prefix}_{name}.png";
                        CaptureUtility.Render(camera, width, height, path, 4, refresh);
                        results.Add(CaptureUtility.Render(camera, width, height, path, 4, refresh));
                    }

                    hud.Bind(null);
                    hud.SetLight(BigValue.FromDouble(1840), BigValue.FromDouble(3.4));
                    hud.SetEgg(BigValue.FromDouble(120), false, true);

                    var rabbit = ContentId.FromSource("species.rabbit");
                    foreach (string language in UiPipeline.Languages)
                    {
                        var text = new LocalizationService(database.localization, language, SystemLanguage.English);
                        string rabbitName = text.Get("species.rabbit.name");

                        // Reveal banners, caught after their pop.
                        hud.ShowReveal(new RevealInfo(text.Get(FeedbackText.RevealNewSpecies), rabbitName, RarityTier.Rare, HatchNews.NewSpecies));
                        for (int i = 0; i < 10; i++) hud.Tick(0.05f);
                        Shot($"reveal_new_species_{language}");
                        hud.ShowReveal(new RevealInfo(text.Get(FeedbackText.RarityLegendary), rabbitName + " · " + text.Get("variant.rabbit.sakura.name"),
                            RarityTier.Legendary, HatchNews.NewVariant));
                        for (int i = 0; i < 10; i++) hud.Tick(0.05f);
                        Shot($"reveal_legendary_{language}");
                        hud.Reveal.Hide();

                        // A warning toast in its slot under the chip.
                        hud.ShowToast(ToastKind.Warning, text.Get(FeedbackText.ToastNotEnoughLight));
                        for (int i = 0; i < 8; i++) hud.Tick(0.05f);
                        Shot($"toast_{language}");
                        hud.Toasts.Clear();

                        // Animal sheet: a named, favorite, Rare animal with two traits.
                        if (sheet != null)
                        {
                            var data = new AnimalSheetData(1u, "Pompon", rabbitName, text.Get("variant.rabbit.spotted.name"), rabbit,
                                ContentId.FromSource("variant.rabbit.spotted"), RarityTier.Rare, true, 0.6f, 2,
                                (uint)(AnimalTrait.Giant | AnimalTrait.Shiny));
                            sheet.Open(data, text, null);
                            for (int i = 0; i < 10; i++) sheet.Tick(0.05f);
                            Shot($"sheet_{language}");
                            sheet.Close();
                            for (int i = 0; i < 10; i++) sheet.Tick(0.05f);
                        }

                        // Egg odds.
                        if (modal != null)
                        {
                            var egg = registry.Eggs.Get(ContentId.FromSource("egg.grassland.basic"));
                            modal.Show(ModalKind.Info, text.Get(FeedbackText.EggOddsTitle), RefugeController.EggOddsText(egg, text, true),
                                text.Get(FeedbackText.ModalOkButton), null, null);
                            for (int i = 0; i < 10; i++) modal.Tick(0.05f);
                            Shot($"odds_{language}");
                            modal.Close();
                            for (int i = 0; i < 10; i++) modal.Tick(0.05f);
                        }
                    }

                    // Tutorial hand on the egg spot and collect motes in flight (no text).
                    var hudRect = (RectTransform)hud.transform;
                    refresh();
                    if (chunk.EggSpawn != null)
                    {
                        var viewport = camera.WorldToViewportPoint(chunk.EggSpawn.position + new Vector3(0f, 0.34f, 0f));
                        var local = Vector2.Scale(new Vector2(viewport.x - 0.5f, viewport.y - 0.5f), hudRect.rect.size);
                        hud.TapHint.SetLocal(local);
                        for (int i = 0; i < 10; i++) hud.Tick(0.05f);
                        Shot("taphint");
                        hud.SetTapHint(false, default);
                        for (int i = 0; i < 10; i++) hud.Tick(0.05f);
                    }
                    hud.Collect.PlayFromLocal(new Vector2(0f, -100f));
                    for (int i = 0; i < 7; i++) hud.Tick(0.05f);
                    Shot("collect");
                    for (int i = 0; i < 40; i++) hud.Tick(0.05f);

                    // Gate 12 shapes: HUD with a banner, and the sheet, on each screen shape (English, the longest).
                    var english = new LocalizationService(database.localization, "en", SystemLanguage.English);
                    foreach (var shape in Shapes)
                    {
                        hud.ShowReveal(new RevealInfo(english.Get(FeedbackText.RevealNewVariant), "Rabbit · Spotted", RarityTier.Epic, HatchNews.NewVariant));
                        for (int i = 0; i < 10; i++) hud.Tick(0.05f);
                        Shot($"shape_{shape.name}_hud", shape.width, shape.height);
                        hud.Reveal.Hide();
                        if (sheet != null)
                        {
                            var data = new AnimalSheetData(2u, english.Get("species.squirrel.name"), english.Get("variant.squirrel.autumn.name"), null,
                                ContentId.FromSource("species.squirrel"), ContentId.FromSource("variant.squirrel.autumn"), RarityTier.Fabulous, false, 0.15f, 0,
                                (uint)(AnimalTrait.Tiny | AnimalTrait.Golden | AnimalTrait.Albino));
                            sheet.Open(data, english, null);
                            for (int i = 0; i < 10; i++) sheet.Tick(0.05f);
                            Shot($"shape_{shape.name}_sheet", shape.width, shape.height);
                            sheet.Close();
                            for (int i = 0; i < 10; i++) sheet.Tick(0.05f);
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

            results.Add(CaptureSplash(prefix));
            return string.Join(", ", results);
        }

        /// <summary>The boot screen alone, as the player sees it before the refuge.</summary>
        private static string CaptureSplash(string prefix)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("CaptureCamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var prefab = AssetDatabase.LoadAssetAtPath<SplashScreen>(FeedbackUiPipeline.SplashPrefabPath);
            var splash = Object.Instantiate(prefab);
            try
            {
                var canvas = splash.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                var scaler = splash.GetComponent<CanvasScaler>();
                var handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                string path = $"{CaptureUtility.DefaultFolder}/{prefix}_splash.png";
                System.Action refresh = () =>
                {
                    handle?.Invoke(scaler, null);
                    Canvas.ForceUpdateCanvases();
                };
                CaptureUtility.Render(camera, 1080, 2340, path, 4, refresh);
                return CaptureUtility.Render(camera, 1080, 2340, path, 4, refresh);
            }
            finally
            {
                Object.DestroyImmediate(splash.gameObject);
                EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
            }
        }
    }
}
