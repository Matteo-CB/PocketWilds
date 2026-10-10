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
    /// T40 « Pluie de noisettes » review (doc 01 Contenu S-ACT-003): a real game played frame by frame in edit mode, on the
    /// real set prefab and stage, with the real game (NutDropController) and an autopilot finger that slides the basket under
    /// the items: the animal stands with its basket on its head and the items are where the game puts them (never an empty
    /// set: every frame checks that items show). Frames: the first game's start with the swipe paw and the slide tip (each
    /// language), items falling and a catch (round glints on the rim), a golden hazelnut's trail, a pinecone bonk (the
    /// pinecone tip in each language, the two leaves, the animal's start), the wide basket with the HUD's boost chip, a busy
    /// late game (the pile, the streak). The squirrel plays the whole game; the rabbit and the hedgehog show their seat.
    /// Nothing is saved (the scene is reopened at the end).
    /// </summary>
    public static class NutDropCapture
    {
        private static readonly (string species, string variant)[] Animals =
        {
            ("species.squirrel", "variant.squirrel.red"), ("species.rabbit", "variant.rabbit.brown"), ("species.hedgehog", "variant.hedgehog.brown"),
        };

        private const int Width = 1080;
        private const int Height = 2340;
        private const float Step = 1f / 30f;

        [MenuItem("Pocket Wilds/Capture/Nut Drop (T40)", priority = 534)]
        public static void CaptureMenu() => Debug.Log("[NutDropCapture] " + Capture("T40_nut_drop"));

        public static string Capture(string prefix)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != WorldCapture.ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NutDropPipeline.PrefabPath);
            if (prefab == null)
                return "no set prefab: run Pocket Wilds/Activities/Build Nut Drop (T40) first";
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
            bool full = index == 0;
            var host = new ActivityStageHost(context, null, null, null) { Hud = hud, Text = texts[0].text, Tutorial = true };
            var controller = new NutDropController();
            var stage = new NutDropStage(host, prefab, controller) { PollInput = false };
            var shots = new List<string>();
            var problems = new List<string>();
            Canvas canvas = null;
            RenderTexture target = null;
            var previousMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var animal = new AnimalInstance
                {
                    InstanceId = (uint)(index + 1), Species = ContentId.FromSource(speciesId), Variant = ContentId.FromSource(variantId),
                    Seed = 0x5EED0000u + (uint)index, Rarity = RarityTier.Common,
                };
                stage.PrepareAnimal(in animal);
                var rules = NutDropRules.Create();
                // A seed whose game drops the wide basket (doc 01: each special at most once, 50 % chance, 15 to 45 s).
                var setup = new ActivitySetup
                {
                    Activity = ActivityId.NutRain, Mode = ActivityMode.Free, Challenge = -1, Difficulty = 5, Seconds = 60f, SpeedMultiplier = 1f,
                    Theme = NutDropStage.PrairieTheme, AnimalCount = 1, Animal0 = animal.InstanceId, Rules = rules, Thresholds = rules.Free,
                };
                for (uint seed = 0x4E555430u + (uint)index * 977u; ; seed++)
                {
                    setup.Seed = seed;
                    controller.Begin(in setup);
                    if (!full || HasSpecial(controller, NutDropItemKind.WideBasket) || seed > 0x4E555430u + (uint)index * 977u + 400u)
                        break;
                }
                // The set camera at the capture's size the whole game (the paw's screen points and the HUD agree with the frames).
                var camera = stage.Camera;
                if (camera == null)
                    return name + ": the set has no camera";
                target = RenderTexture.GetTemporary(Width, Height, 24);
                camera.targetTexture = target;
                camera.aspect = Width / (float)Height;
                stage.Enter(in setup, host);
                var view = stage.Animal;
                if (view == null)
                    return name + ": no view";
                var animator = view.Animator;
                if (animator != null)
                {
                    animator.Rebind();
                    animator.Update(0f);
                }
                var set = stage.Set.Data;
                var systems = new List<ParticleSystem>();
                if (set.glints != null)
                    systems.AddRange(set.glints);
                systems.Add(set.trail);
                systems.Add(set.ring);
                systems.Add(set.hearts);
                systems.RemoveAll(s => s == null);

                if (hud != null)
                {
                    canvas = hud.GetComponentInParent<Canvas>(true);
                    canvas = canvas != null ? canvas.rootCanvas : null;
                    if (canvas != null)
                    {
                        previousMode = canvas.renderMode;
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        // Not too close to the set camera's near plane: TMP's distance-field text vanishes at tiny scales.
                        canvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.1f, 0.5f);
                    }
                    var model = new ActivityHudModel
                    {
                        Activity = ActivityId.NutRain, Mode = ActivityMode.Free, Duration = setup.Seconds, Star1 = rules.Free.One, Star2 = rules.Free.Two,
                        Star3 = rules.Free.Three, Best = 1180,
                    };
                    hud.Show(in model, texts[0].text, actions);
                }

                bool pressed = false;
                NutDropItemKind chase = NutDropItemKind.None;

                // The autopilot finger: under the next item to catch (or under the kind being chased).
                void Steer()
                {
                    int best = -1;
                    float soonest = float.MaxValue;
                    for (int s = 0; s < controller.ItemCapacity; s++)
                    {
                        if (!controller.ItemActive(s) || controller.ItemMissed(s))
                            continue;
                        var kind = controller.ItemKind(s);
                        bool wanted = chase != NutDropItemKind.None ? kind == chase : kind != NutDropItemKind.Pinecone;
                        float t = controller.ItemRimSeconds(s);
                        if (!wanted || t <= 0f || t >= soonest)
                            continue;
                        soonest = t;
                        best = s;
                    }
                    if (best < 0)
                        return;
                    var screen = new Vector2(stage.ScreenX(controller.ItemRimX(best)), Height * 0.3f);
                    if (!pressed)
                    {
                        stage.PointerDown(screen);
                        pressed = true;
                    }
                    else
                    {
                        stage.PointerMove(screen);
                    }
                }

                void Run(float seconds, bool steer = true, bool advanceGame = true)
                {
                    int frames = Mathf.Max(1, Mathf.RoundToInt(seconds / Step));
                    for (int f = 0; f < frames; f++)
                    {
                        if (steer && advanceGame)
                            Steer();
                        if (advanceGame)
                            controller.Tick(Step);
                        stage.Tick(Step, Step);
                        if (animator != null)
                            animator.Update(Step);
                        stage.LateTick();
                        foreach (var system in systems)
                            if (system.particleCount > 0)
                                system.Simulate(Step, true, false, false);
                        string overlap = stage.CheckOverlaps();
                        if (overlap != null && problems.Count < 8)
                            problems.Add($"{controller.Elapsed:F2} s: {overlap}");
                        if (hud != null)
                        {
                            hud.SetScore(controller.Score);
                            hud.SetTimeLeft(Mathf.Max(0f, setup.Seconds - controller.Elapsed));
                            hud.SetStreak(controller.Streak);
                            hud.Tick(Step);
                        }
                    }
                }

                // Runs until the condition holds (or the time is up for it).
                bool Until(System.Func<bool> condition, float seconds, bool steer = true)
                {
                    float until = controller.Elapsed + seconds;
                    while (!condition() && controller.Elapsed < until && !controller.Finished)
                        Run(Step, steer);
                    return condition();
                }

                void Shot(string label)
                {
                    if (stage.VisibleItems == 0 && controller.Elapsed > 1f)
                        problems.Add(label + ": no item shown");
                    string path = $"{CaptureUtility.DefaultFolder}/{prefix}_{name}_{label}.png";
                    shots.Add(CaptureUtility.Render(camera, Width, Height, path, 4, () =>
                    {
                        if (canvas != null && handle != null)
                            handle.Invoke(canvas.GetComponent<CanvasScaler>(), null);
                        Canvas.ForceUpdateCanvases();
                    }));
                }

                // 1. The first game's start: the swipe paw under the basket and « Glisse pour attraper » (each language), the
                // first items falling from the leaves.
                Run(1.35f, false);
                foreach (var (language, text) in texts)
                {
                    hud?.ShowTip(text.Get(ActivityText.NutDropTipSlide), 2f);
                    Run(0.2f, false, false);
                    Shot("start_" + language);
                }
                if (!full)
                {
                    // The rabbit and the hedgehog: the basket on their heads, a catch.
                    int caughtBefore = controller.Caught;
                    Until(() => controller.Caught > caughtBefore, 8f);
                    Run(0.1f);
                    Shot("catch");
                    return string.Join(", ", shots) + (problems.Count > 0 ? " PROBLEMS: " + string.Join("; ", problems) : "");
                }

                // 2. Items falling and a catch (four round glints on the rim).
                int caught = controller.Caught;
                Until(() => controller.Caught > caught, 8f);
                Run(0.1f);
                Shot("catch");

                // 3. A golden hazelnut on its way down with its trail of soft dots.
                Until(() => GoldenFalling(controller), 30f);
                Run(0.12f);
                Shot("golden");

                // 4. A pinecone caught: the bonk, two leaves, the animal's start; the pinecone tip in each language.
                chase = NutDropItemKind.Pinecone;
                int pinecones = controller.PineconesCaught;
                Until(() => controller.PineconesCaught > pinecones, 25f);
                chase = NutDropItemKind.None;
                Run(0.15f);
                foreach (var (language, text) in texts)
                {
                    hud?.ShowTip(text.Get(ActivityText.NutDropTipPinecone), 2f);
                    Run(0.05f, false, false);
                    Shot("pinecone_" + language);
                }

                // 5. The wide basket: caught, its effect on (the basket wider, the soft ring), the HUD's boost chip.
                chase = NutDropItemKind.WideBasket;
                Until(() => controller.ActiveSpecial == NutDropItemKind.WideBasket, 45f);
                chase = NutDropItemKind.None;
                Run(0.35f);
                Shot("wide_basket");

                // 6. A busy late game: the pile in the basket, the streak, many items in the air.
                Until(() => controller.Elapsed >= 52f, 60f);
                Shot("late");
                if (pressed)
                    stage.PointerUp();
                return string.Join(", ", shots) + $" (score {controller.Score}, caught {controller.Caught}, best streak {controller.BestStreak})"
                       + (problems.Count > 0 ? " PROBLEMS: " + string.Join("; ", problems) : "");
            }
            finally
            {
                if (canvas != null)
                {
                    canvas.renderMode = previousMode;
                    canvas.worldCamera = null;
                }
                hud?.Hide();
                if (stage.Camera != null)
                    stage.Camera.targetTexture = null;
                if (target != null)
                    RenderTexture.ReleaseTemporary(target);
                stage.Exit();
                stage.Dispose();
            }
        }

        private static bool HasSpecial(NutDropController controller, NutDropItemKind kind)
        {
            for (int i = 0; i < controller.SpecialCount; i++)
                if (controller.SpecialKind(i) == kind)
                    return true;
            return false;
        }

        private static bool GoldenFalling(NutDropController controller)
        {
            for (int s = 0; s < controller.ItemCapacity; s++)
                if (controller.ItemActive(s) && controller.ItemKind(s) == NutDropItemKind.GoldenNut && controller.ItemY(s) > 1.5f
                    && controller.ItemY(s) < 4.5f)
                    return true;
            return false;
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
