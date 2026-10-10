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
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Capture
{
    /// <summary>
    /// T41 « Memory des empreintes » review (doc 01 Contenu S-ACT-004): real games played frame by frame in edit mode, on the
    /// real screen prefab and stage, with the real game (MemoryController) dealing the cards and the real activity HUD on
    /// top, rendered through a camera at 1080 × 2340. Taps go through the stage's own hit test at each card's screen point.
    /// Frames: the preview (every face up, d1), the first game's tapping paw with « Trouve les paires » (each language), a
    /// flip in progress (the card squashed across, lifted), a match with its round glints and its heart, the glow card's
    /// pair pulsing gold, a pawprint grid at d8 (5 × 4: pawprints in their coat colours beside portraits, the « ×2 » badge),
    /// and the end (every pair matched, their soft marks). Every frame checks that cards show. Nothing is saved (the scene is
    /// reopened at the end).
    /// </summary>
    public static class MemoryCapture
    {
        private const int Width = 1080;
        private const int Height = 2340;
        private const float Step = 1f / 30f;

        [MenuItem("Pocket Wilds/Capture/Memory (T41)", priority = 535)]
        public static void CaptureMenu() => Debug.Log("[MemoryCapture] " + Capture("T41_memory"));

        public static string Capture(string prefix)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != WorldCapture.ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MemoryPipeline.PrefabPath);
            if (prefab == null)
                return "no screen prefab: run Pocket Wilds/Activities/Build Memory (T41) first";
            EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var registry = database.CreateBuilder().Build();
            var animals = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var codex = AssetDatabase.LoadAssetAtPath<CodexVisualCatalog>(AnimalThumbnailPipeline.CatalogPath);
            var handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
            var texts = new List<(string language, LocalizationService text)>();
            foreach (string language in UiPipeline.Languages)
                texts.Add((language, new LocalizationService(database.localization, language, SystemLanguage.English)));
            var shots = new List<string>();
            var problems = new List<string>();
            var notes = new List<string>();
            using (var palettes = new PaletteTable(registry))
            {
                GameObject cameraObject = null;
                RenderTexture target = null;
                MemoryStage stage = null;
                ActivityHudScreen hud = null;
                Canvas hudCanvas = null;
                var hudMode = RenderMode.ScreenSpaceOverlay;
                try
                {
                    var resolver = new AnimalVisualResolver(registry, palettes);
                    var context = new GameContext(null, registry, null, null, null, null, null, palettes, resolver, animals, null, null, localization: texts[0].text);
                    hud = Object.FindAnyObjectByType<ActivityHudScreen>(FindObjectsInactive.Include);
                    var refugeHud = Object.FindAnyObjectByType<RefugeHud>(FindObjectsInactive.Include);
                    if (refugeHud != null)
                        refugeHud.gameObject.SetActive(false);

                    // A camera that draws the interface only (the refuge does not render under the screen).
                    cameraObject = new GameObject("MemoryCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
                    var camera = cameraObject.AddComponent<Camera>();
                    camera.enabled = false;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = MemoryPipeline.ClothBase;
                    camera.cullingMask = 1 << 5;
                    camera.orthographic = true;
                    camera.nearClipPlane = 0.1f;
                    camera.farClipPlane = 50f;
                    target = RenderTexture.GetTemporary(Width, Height, 24);
                    camera.targetTexture = target;
                    camera.aspect = Width / (float)Height;

                    var host = new ActivityStageHost(context, null, null, null) { Hud = hud, Text = texts[0].text, Tutorial = true };
                    var controller = new MemoryController();
                    stage = new MemoryStage(host, prefab, controller) { PollInput = false };
                    var view = stage.View as MemoryScreenView;
                    if (view == null)
                        return "the screen prefab has no MemoryScreenView (rebuild it)";
                    view.SetPortraits(codex);
                    var screenCanvas = view.Canvas;
                    screenCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    screenCanvas.worldCamera = camera;
                    screenCanvas.planeDistance = 2f;
                    if (hud != null)
                    {
                        hudCanvas = hud.GetComponentInParent<Canvas>(true);
                        hudCanvas = hudCanvas != null ? hudCanvas.rootCanvas : null;
                        if (hudCanvas != null)
                        {
                            hudMode = hudCanvas.renderMode;
                            hudCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                            hudCanvas.worldCamera = camera;
                            hudCanvas.planeDistance = 1f;
                        }
                    }
                    var actions = new NoActions();

                    void Refresh()
                    {
                        if (handle != null)
                        {
                            handle.Invoke(screenCanvas.GetComponent<CanvasScaler>(), null);
                            if (hudCanvas != null)
                                handle.Invoke(hudCanvas.GetComponent<CanvasScaler>(), null);
                        }
                        Canvas.ForceUpdateCanvases();
                    }

                    // One game: prepared (the variants), begun (the deal), entered (the screen), the HUD shown.
                    void StartGame(int difficulty, uint seed, bool tutorial)
                    {
                        stage.Exit();
                        host.Tutorial = tutorial;
                        var request = ActivityRequest.Free(ActivityId.PawMemory);
                        stage.Prepare(in request);
                        var rules = MemoryRules.Create();
                        var setup = new ActivitySetup
                        {
                            Activity = ActivityId.PawMemory, Mode = ActivityMode.Free, Challenge = -1, Difficulty = difficulty, Seed = seed,
                            SpeedMultiplier = 1f, Theme = MemoryStage.PrairieTheme, Rules = rules,
                        };
                        controller.Begin(in setup);
                        setup.Seconds = controller.Seconds;
                        setup.Thresholds = MemoryRules.FreeOf(controller.Grid);
                        Refresh();
                        stage.Enter(in setup, host);
                        if (view.HeartSprite == null || view.MarkSprite == null)
                        {
                            // What the activity HUD lends at run time once it carries them (ICO_Favorite_Heart, ICO_Check).
                            view.SetBorrowedSprites(AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.IconFolder + "/ICO_Favorite_Heart.png"),
                                AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.IconFolder + "/ICO_Check.png"));
                            if (!notes.Contains("heart"))
                                notes.Add("heart");
                        }
                        if (hud != null)
                        {
                            var model = new ActivityHudModel
                            {
                                Activity = ActivityId.PawMemory, Mode = ActivityMode.Free, Duration = setup.Seconds, Star1 = setup.Thresholds.One,
                                Star2 = setup.Thresholds.Two, Star3 = setup.Thresholds.Three, Best = 0,
                            };
                            hud.Show(in model, texts[0].text, actions);
                        }
                        Refresh();
                        stage.View.Tick(0f);
                    }

                    void Run(float seconds)
                    {
                        int frames = Mathf.Max(1, Mathf.RoundToInt(seconds / Step));
                        for (int f = 0; f < frames; f++)
                        {
                            controller.Tick(Step);
                            stage.Tick(Step, Step);
                            if (hud != null)
                            {
                                hud.SetScore(controller.Score);
                                hud.SetTimeLeft(controller.SecondsLeft);
                                hud.Tick(Step);
                            }
                        }
                    }

                    // Waits until the game takes a touch (no preview, no mismatch waiting), then taps through the hit test.
                    void Tap(int card)
                    {
                        int guard = 0;
                        while ((controller.Previewing || controller.Busy) && guard++ < 200)
                            Run(Step);
                        Refresh();
                        int hit = stage.View.HitTest(stage.View.ScreenPoint(card));
                        if (hit != card && problems.Count < 8)
                            problems.Add($"tap on card {card} hit {hit}");
                        stage.Tap(card);
                        Run(Step);
                    }

                    void MatchPair(int card)
                    {
                        int partner = controller.PartnerOf(card);
                        Tap(card);
                        Tap(partner);
                    }

                    int FirstOpen(int skip = -1)
                    {
                        for (int c = 0; c < controller.CardCount; c++)
                            if (c != skip && !controller.Matched(c) && !controller.FaceUp(c) && c != controller.GlowCard
                                && controller.PartnerOf(c) != controller.GlowCard)
                                return c;
                        return -1;
                    }

                    void Shot(string label)
                    {
                        int shown = 0;
                        for (int c = 0; c < controller.CardCount; c++)
                            if (view.Card(c).root.gameObject.activeInHierarchy)
                                shown++;
                        if (shown != controller.CardCount)
                            problems.Add($"{label}: {shown} cards shown of {controller.CardCount}");
                        string path = $"{CaptureUtility.DefaultFolder}/{prefix}_{label}.png";
                        // Twice: the first render settles the canvases' size at the capture's resolution, the second is kept.
                        for (int pass = 0; pass < 2; pass++)
                        {
                            string written = CaptureUtility.Render(camera, Width, Height, path, 4, () =>
                            {
                                Refresh();
                                stage.View.Tick(0f);
                                Canvas.ForceUpdateCanvases();
                            });
                            if (pass == 1)
                                shots.Add(written);
                        }
                    }

                    // 1. The first game at d1 (4 × 3, a 2 s preview): every face up, the HUD on top.
                    StartGame(1, 0x4D454D31u, true);
                    Run(0.6f);
                    if (!controller.Previewing)
                        problems.Add("d1: no preview");
                    Shot("preview");

                    // 2. The tapping paw on a card and then its pair, « Trouve les paires » (each language).
                    int waited = 0;
                    while (stage.TutorialStep < 2 && waited++ < 150)
                        Run(Step);
                    Run(0.45f);
                    if (stage.TutorialStep != 2 || hud != null && !hud.GestureShown)
                        problems.Add("no tutorial paw");
                    foreach (var (language, text) in texts)
                    {
                        hud?.ShowTip(text.Get(ActivityText.MemoryTipPairs), 3f);
                        Run(0.25f);
                        Shot("tutorial_" + language);
                    }

                    // 3. A flip in progress: the card squashed across, lifted a little.
                    int first = stage.TutorialTarget >= 0 ? stage.TutorialTarget : FirstOpen();
                    if (first == controller.GlowCard || controller.PartnerOf(first) == controller.GlowCard)
                        first = FirstOpen();
                    stage.Tap(first);
                    controller.Tick(Step * 0.5f);
                    stage.Tick(Step * 0.5f, Step * 0.5f);
                    Run(Step * 2f);
                    Shot("flip");

                    // 4. A match: the two cards stay up, a round glint on each, a little heart rising.
                    Run(0.3f);
                    Tap(controller.PartnerOf(first));
                    Run(0.2f);
                    if (stage.Matches < 1)
                        problems.Add("no match");
                    Shot("match");
                    Run(0.8f);

                    // 5. The glow card: turned up while its pair is face down, the pair pulses gold for 1 s.
                    int glow = controller.GlowCard;
                    if (glow >= 0)
                    {
                        Tap(glow);
                        Run(0.3f);
                        if (stage.Glows < 1)
                            problems.Add("no glow");
                        Shot("glow");
                        Tap(controller.PartnerOf(glow));
                        Run(0.9f);
                    }
                    else
                    {
                        problems.Add("no glow card at d1");
                    }

                    // 6. A pawprint grid at d8 (5 × 4, a pawprint and a portrait per pair, the double pair): half the pairs found,
                    // one more card up.
                    StartGame(8, 0x4D454D38u, false);
                    if (!controller.Pawprints || controller.CardCount != 20)
                        problems.Add($"d8: {controller.Columns} × {controller.Rows}, pawprints {controller.Pawprints}");
                    Run(0.2f);
                    for (int c = 0; c < controller.CardCount && controller.PairsFound < 2; c++)
                        if (controller.IsDouble(c) && !controller.Matched(c))
                            MatchPair(c);
                    while (controller.PairsFound < 5)
                    {
                        int c = FirstOpen();
                        if (c < 0)
                            break;
                        MatchPair(c);
                    }
                    int open = FirstOpen();
                    if (open >= 0)
                        Tap(open);
                    Run(0.6f);
                    Shot("pawprints_d8");

                    // 7. The end: every pair matched, their soft marks, the HUD's score.
                    if (open >= 0)
                        Tap(controller.PartnerOf(open));
                    int guardPairs = 0;
                    while (!controller.Finished && guardPairs++ < 20)
                    {
                        int c = -1;
                        for (int k = 0; k < controller.CardCount; k++)
                            if (!controller.Matched(k))
                            {
                                c = k;
                                break;
                            }
                        if (c < 0)
                            break;
                        MatchPair(c);
                    }
                    Run(0.9f);
                    if (!controller.AllMatched)
                        problems.Add("the d8 game did not end on its last pair");
                    Shot("end");
                    notes.Add($"d8 score {controller.Score}, pairs {controller.PairsFound}, mistakes {controller.Mistakes}");
                }
                finally
                {
                    if (hudCanvas != null)
                    {
                        hudCanvas.renderMode = hudMode;
                        hudCanvas.worldCamera = null;
                    }
                    hud?.Hide();
                    stage?.Exit();
                    stage?.Dispose();
                    if (target != null)
                        RenderTexture.ReleaseTemporary(target);
                    if (cameraObject != null)
                        Object.DestroyImmediate(cameraObject);
                    EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
                }
            }
            string noteText = notes.Contains("heart")
                ? " (the match heart and the matched check are borrowed here the way the activity HUD would lend them: add ICO_Favorite_Heart and ICO_Check to ActivityUiPipeline.LentIcons, or draw UI_Memory_Heart and UI_Memory_Mark)"
                : "";
            notes.Remove("heart");
            return string.Join(", ", shots) + (notes.Count > 0 ? " [" + string.Join("; ", notes) + "]" : "") + noteText
                   + (problems.Count > 0 ? " PROBLEMS: " + string.Join("; ", problems) : "");
        }

        private sealed class NoActions : IActivityHudActions
        {
            public void PauseActivity()
            {
            }

            public void ResumeActivity()
            {
            }

            public void QuitActivity()
            {
            }
        }
    }
}
