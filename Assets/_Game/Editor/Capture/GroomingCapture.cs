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
    /// T38 « Toilettage » review in edit mode (doc 01 Contenu S-ACT-001 and A15): the real set prefab and stage, with a
    /// rabbit, a squirrel and a hedgehog, the grooming game itself (GroomingController) driven step by step: the dirty start
    /// with its debris and the tutorial's tip and paw, mid-game with foam, a clean zone's glints and a soap bubble, the golden
    /// brush offered then in the hand, the bow phase with the HUD's picker, the end with the bow on the head. The HUD's tips in
    /// each shipped language. Nothing is saved (the scene is reopened at the end).
    /// </summary>
    public static class GroomingCapture
    {
        private static readonly (string species, string variant)[] Animals =
        {
            ("species.rabbit", "variant.rabbit.brown"), ("species.squirrel", "variant.squirrel.red"), ("species.hedgehog", "variant.hedgehog.brown"),
        };

        private const int Width = 1080;
        private const int Height = 2340;
        private const float Step = 1f / 30f;

        [MenuItem("Pocket Wilds/Capture/Grooming (T38)", priority = 532)]
        public static void CaptureMenu() => Debug.Log("[GroomingCapture] " + Capture("T38_grooming"));

        public static string Capture(string prefix)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != WorldCapture.ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GroomingPipeline.PrefabPath);
            if (prefab == null)
                return "no set prefab: run Pocket Wilds/Activities/Build Grooming (T38) first";
            EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var registry = database.CreateBuilder().Build();
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var results = new List<string>();
            var handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
            var texts = new List<(string language, LocalizationService text)>();
            foreach (string language in UiPipeline.Languages)
                texts.Add((language, new LocalizationService(database.localization, language, SystemLanguage.English)));
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var resolver = new AnimalVisualResolver(registry, palettes);
                    var context = new GameContext(null, registry, null, null, null, null, null, palettes, resolver, catalog, null, null, localization: texts[0].text);
                    var hud = Object.FindAnyObjectByType<ActivityHudScreen>(FindObjectsInactive.Include);
                    var refugeHud = Object.FindAnyObjectByType<RefugeHud>(FindObjectsInactive.Include);
                    if (refugeHud != null)
                        refugeHud.gameObject.SetActive(false);
                    var actions = new NoActions();
                    for (int a = 0; a < Animals.Length; a++)
                        results.Add(CaptureAnimal(prefix, a, prefab, context, hud, texts, actions, handle));
                }
                finally
                {
                    EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
                }
            }
            return string.Join(", ", results);
        }

        private static string CaptureAnimal(string prefix, int index, GameObject prefab, GameContext context, ActivityHudScreen hud,
            List<(string language, LocalizationService text)> texts, NoActions actions, MethodInfo handle)
        {
            var (speciesId, variantId) = Animals[index];
            string name = speciesId.Substring(speciesId.IndexOf('.') + 1);
            var host = new ActivityStageHost(context, null, null, null) { Hud = hud, Text = texts[0].text, Tutorial = true };
            var controller = new GroomingController();
            var stage = new GroomingStage(host, prefab, controller) { PollInput = false };
            var shots = new List<string>();
            Canvas canvas = null;
            var previousMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var animal = new AnimalInstance
                {
                    InstanceId = (uint)(index + 1), Species = ContentId.FromSource(speciesId), Variant = ContentId.FromSource(variantId),
                    Seed = 0x5EED0000u + (uint)index, Rarity = RarityTier.Common,
                };
                stage.PrepareAnimal(in animal);
                if (stage.Animal == null)
                    return name + ": no view";
                var camera = stage.Camera;
                camera.aspect = Width / (float)Height;
                var rules = GroomingRules.Create();
                var setup = new ActivitySetup
                {
                    Activity = ActivityId.Grooming, Mode = ActivityMode.Free, Challenge = -1, Difficulty = 5, Seed = 0x47524F4Du + (uint)index * 7919u,
                    Seconds = 45f, SpeedMultiplier = 1f, Theme = GroomingStage.PrairieTheme, AnimalCount = 1, Animal0 = animal.InstanceId,
                    Rules = rules, Thresholds = rules.Free,
                };
                controller.Begin(in setup);
                stage.Enter(in setup, host);
                var game = stage.Game;
                var animator = stage.Animal.View.Animator;
                if (animator != null)
                {
                    animator.Rebind();
                    animator.Update(0f);
                }

                if (hud != null)
                {
                    canvas = hud.GetComponentInParent<Canvas>(true);
                    canvas = canvas != null ? canvas.rootCanvas : null;
                    if (canvas != null)
                    {
                        previousMode = canvas.renderMode;
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        // Not too close to the set camera's short near plane: TMP's distance-field text vanishes at tiny scales.
                        canvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.1f, 0.5f);
                    }
                    var model = new ActivityHudModel
                    {
                        Activity = ActivityId.Grooming, Mode = ActivityMode.Free, Duration = 45f, Star1 = rules.Free.One, Star2 = rules.Free.Two,
                        Star3 = rules.Free.Three, Best = 410,
                    };
                    hud.Show(in model, texts[0].text, actions);
                }

                void Run(float seconds, bool advanceGame = true)
                {
                    int frames = Mathf.Max(1, Mathf.RoundToInt(seconds / Step));
                    for (int f = 0; f < frames; f++)
                    {
                        if (advanceGame)
                            controller.Tick(Step);
                        stage.Tick(Step, Step);
                        if (animator != null)
                            animator.Update(Step);
                        stage.LateTick();
                        if (hud != null)
                        {
                            hud.SetScore(controller.Score);
                            hud.SetTimeLeft(Mathf.Max(0f, setup.Seconds - game.Elapsed));
                            hud.SetStreak(controller.Streak);
                            hud.Tick(Step);
                        }
                    }
                }

                void Shot(string label)
                {
                    string path = $"{CaptureUtility.DefaultFolder}/{prefix}_{name}_{label}.png";
                    shots.Add(CaptureUtility.Render(camera, Width, Height, path, 4, () =>
                    {
                        if (canvas != null && handle != null)
                            handle.Invoke(canvas.GetComponent<CanvasScaler>(), null);
                        Canvas.ForceUpdateCanvases();
                    }));
                }

                // 1. The dirty start: dust, debris popping on, the brush tip and the swipe paw (A15), in each language.
                Run(0.6f);
                foreach (var (language, text) in texts)
                {
                    hud?.ShowTip(text.Get(ActivityText.GroomingTipBrush), 2f);
                    Run(0.25f, false);
                    Shot("start_" + language);
                }

                // 2. Mid-game: zones brushed (foam), one cleaned (glints), the first soap bubble on the coat.
                int cleaned = 0;
                for (int z = 0; z < game.ZoneCount && cleaned < 6; z++)
                {
                    if (!game.IsDirty(z))
                        continue;
                    while (game.IsDirty(z))
                        game.Brush(z);
                    cleaned++;
                }
                int pass = FirstDirty(game);
                if (pass >= 0)
                    game.Brush(pass);
                Run(Mathf.Max(0.1f, 15.3f - game.Elapsed));
                pass = FirstDirty(game);
                if (pass >= 0)
                    game.Brush(pass);
                Run(0.15f);
                Shot("mid");

                // 3. The golden brush: offered on the coat at 20 s (the streak at × 1.5), then in the hand.
                Run(Mathf.Max(0.1f, 20.4f - game.Elapsed));
                Shot("golden_offer");
                game.TakeGoldenBrush();
                Run(0.1f);
                int target = FirstDirty(game);
                if (target >= 0)
                {
                    var screen = stage.Animal.Screen[target];
                    stage.PointerDown(screen - new Vector2(90f, 20f));
                    for (int k = 1; k <= 6; k++)
                    {
                        stage.PointerMove(screen - new Vector2(90f - 30f * k, 20f - 4f * k));
                        Run(Step);
                    }
                }
                Shot("golden");
                stage.PointerUp();

                // 4. Everything clean: the bow phase, the HUD's three bows and the bow tip (each language).
                for (int z = 0; z < game.ZoneCount; z++)
                    while (game.IsDirty(z))
                        game.Brush(z);
                Run(0.5f);
                foreach (var (language, text) in texts)
                {
                    hud?.ShowTip(text.Get(ActivityText.GroomingTipBow), 2f);
                    Run(0.2f, false);
                    Shot("bow_" + language);
                }

                // 5. The bow on the head, the animal content (eyes half closed).
                stage.Pick(index % 3);
                Run(0.7f, false);
                stage.End();
                Run(0.8f, false);
                Shot("final");
                return string.Join(", ", shots);
            }
            finally
            {
                if (canvas != null)
                {
                    canvas.renderMode = previousMode;
                    canvas.worldCamera = null;
                }
                hud?.Hide();
                stage.Exit();
                stage.Dispose();
            }
        }

        private static int FirstDirty(IGroomingGame game)
        {
            for (int z = 0; z < game.ZoneCount; z++)
                if (game.IsDirty(z))
                    return z;
            return -1;
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
