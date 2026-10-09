using System.Collections.Generic;
using System.Reflection;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.UI;
using PocketWilds.Editor.Vfx;
using PocketWilds.Editor.World;
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
    /// T39 « Cache-cache » review in edit mode (doc 01 Contenu S-ACT-002; doc « Mini-jeux &amp; activités » section 1): the
    /// refuge scene with the zone's chunk and its neighbours (palettes, scatter, the day mood), the real stage and the
    /// hide-and-seek game (HideSeekController) driven step by step with six animals of the three Prairie species: the start
    /// at d5 with the find tip and the paw, a hint (the decor's ripple and the hint tip), a « coucou » mid-hop with its heart
    /// and ring (and the items tip), the magnifier's ring, the night challenge with the HUD's spotlight, all found gathered.
    /// The tips in each shipped language. Needs DEF_HideSpots_Grassland (the hide-spot pipeline). Nothing is saved: the scene
    /// is reopened at the end.
    /// </summary>
    public static class HideSeekCapture
    {
        private static readonly (string species, string variant)[] Animals =
        {
            ("species.rabbit", "variant.rabbit.brown"), ("species.squirrel", "variant.squirrel.red"), ("species.hedgehog", "variant.hedgehog.brown"),
            ("species.rabbit", "variant.rabbit.white"), ("species.squirrel", "variant.squirrel.autumn"), ("species.hedgehog", "variant.hedgehog.mushroom"),
        };

        private const int Width = 1080;
        private const int Height = 2340;
        private const float Step = 1f / 30f;

        [MenuItem("Pocket Wilds/Capture/Hide and Seek (T39)", priority = 533)]
        public static void CaptureMenu() => Debug.Log("[HideSeekCapture] " + Capture("T39_hide_seek"));

        /// <summary>The measured spots of the Prairie chunks (agent HW's pipeline), or null.</summary>
        public static HideSpotSet FindSpots()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:HideSpotSet"))
            {
                var set = AssetDatabase.LoadAssetAtPath<HideSpotSet>(AssetDatabase.GUIDToAssetPath(guid));
                if (set != null && set.name == "DEF_HideSpots_Grassland")
                    return set;
            }
            return null;
        }

        public static string Capture(string prefix)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != WorldCapture.ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            var spots = FindSpots();
            if (spots == null || spots.ZoneCount == 0 || spots.SpotCount == 0)
                return "no DEF_HideSpots_Grassland (or it holds no zone): run the hide-spot pipeline (agent HW) first, then this capture";
            EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var registry = database.CreateBuilder().Build();
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
            var texts = new List<(string language, LocalizationService text)>();
            foreach (string language in UiPipeline.Languages)
                texts.Add((language, new LocalizationService(database.localization, language, SystemLanguage.English)));
            int level0 = QualitySettings.GetQualityLevel();
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    return CaptureGame(prefix, spots, registry, palettes, catalog, texts, handle);
                }
                finally
                {
                    QualitySettings.SetQualityLevel(level0, true);
                    EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
                }
            }
        }

        private static string CaptureGame(string prefix, HideSpotSet spots, ContentRegistry registry, PaletteTable palettes, AnimalVisualCatalog catalog,
            List<(string language, LocalizationService text)> texts, MethodInfo handle)
        {
            var shots = new List<string>();
            var tier = QualityTier.Medium;
            QualitySettings.SetQualityLevel((int)tier, true);

            // The zone of the start chunk (else the first one), its chunk and the neighbours the camera may see.
            int zone = spots.FindZone(WorldContentPipeline.StartAddress);
            if (zone < 0)
                zone = 0;
            string chunkId = spots.GetZone(zone).chunkId;
            var chunks = new List<WorldChunk>();
            WorldChunk played = null;
            Vector3 playedOrigin = Vector3.zero;
            foreach (var entry in WorldContentPipeline.GrasslandChunks)
                if (entry.address == chunkId)
                    playedOrigin = entry.origin;
            foreach (var entry in WorldContentPipeline.GrasslandChunks)
            {
                if (Mathf.Abs(entry.origin.x - playedOrigin.x) > 14.5f || Mathf.Abs(entry.origin.z - playedOrigin.z) > 14.5f)
                    continue;
                var chunk = entry.pinned ? WorldCapture.SceneChunk() : WorldContentPipeline.SpawnForTools(entry.address);
                chunk.Bind(palettes);
                chunk.ApplyQuality(tier);
                chunks.Add(chunk);
                if (entry.address == chunkId)
                    played = chunk;
            }
            if (played == null)
                return $"zone {zone}: chunk {chunkId} is not a Prairie chunk of the strip";

            var scene = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var sun = Object.FindAnyObjectByType<Light>();
            var camera = Camera.main;
            var quality = WorldQualityProfile.Default(tier);
            var scatter = new ScatterRenderer(palettes, quality.scatterDensity);
            scatter.SetDistanceFade(quality.scatterFadeStart, quality.scatterFadeEnd);
            foreach (var chunk in chunks)
                scatter.Add(chunk);
            var morning = AssetDatabase.LoadAssetAtPath<LightingProfile>(WorldCapture.LightingProfilePath("Morning"));
            var evening = AssetDatabase.LoadAssetAtPath<LightingProfile>(WorldCapture.LightingProfilePath("Evening"));
            var night = AssetDatabase.LoadAssetAtPath<LightingProfile>(WorldCapture.LightingProfilePath("Night"));
            scene.Lighting.Apply(sun, camera, tier);
            var atmosphere = new WorldAtmosphere(morning, scene.Lighting, evening, sun, camera, quality, night);
            atmosphere.ForceMood(LightingMood.Day);
            atmosphere.Tick(0f);

            var resolver = new AnimalVisualResolver(registry, palettes);
            var context = new GameContext(null, registry, null, null, null, null, null, palettes, resolver, catalog, null, null, localization: texts[0].text);
            var hud = Object.FindAnyObjectByType<ActivityHudScreen>(FindObjectsInactive.Include);
            var refugeHud = Object.FindAnyObjectByType<RefugeHud>(FindObjectsInactive.Include);
            if (refugeHud != null)
                refugeHud.gameObject.SetActive(false);
            var root = new GameObject("HideSeekCapture").transform;
            var host = new ActivityStageHost(context, null, null, root) { Hud = hud, Text = texts[0].text, Tutorial = true };
            var vfxCatalog = AssetDatabase.LoadAssetAtPath<VfxCatalog>(VfxPipeline.CatalogPath);
            var vfx = vfxCatalog != null ? new FeedbackVfx(vfxCatalog, root, tier) : null;
            var controller = new HideSeekController();
            var stage = new HideSeekStage(host, spots, controller, camera) { PollInput = false, Vfx = vfx, Atmosphere = atmosphere };
            stage.SetSharedAssets(AssetDatabase.LoadAssetAtPath<Material>(VfxPipeline.MaterialPath),
                AssetDatabase.LoadAssetAtPath<Mesh>(GrasslandPipeline.KitMeshFolder + "/GL_Leaf_A.asset"),
                AssetDatabase.LoadAssetAtPath<Material>(GrasslandPipeline.EnvironmentMaterialPath));
            var animals = new AnimalInstance[Animals.Length];
            for (int i = 0; i < animals.Length; i++)
                animals[i] = new AnimalInstance
                {
                    InstanceId = (uint)(i + 1), Species = ContentId.FromSource(Animals[i].species), Variant = ContentId.FromSource(Animals[i].variant),
                    Seed = 0x48534B00u + (uint)i * 7919u, Rarity = RarityTier.Common,
                };
            stage.UseAnimals(animals, animals.Length);
            stage.UseZone(zone, played);

            Canvas canvas = null;
            var previousMode = RenderMode.ScreenSpaceOverlay;
            var rules = HideSeekRules.Create();
            float vfxStart = -1f;
            float clock = 0f;
            try
            {
                camera.aspect = Width / (float)Height;
                if (hud != null)
                {
                    canvas = hud.GetComponentInParent<Canvas>(true);
                    canvas = canvas != null ? canvas.rootCanvas : null;
                    if (canvas != null)
                        previousMode = canvas.renderMode;
                }

                ActivitySetup Setup(int challenge, uint seed)
                {
                    bool isChallenge = challenge >= 0;
                    return new ActivitySetup
                    {
                        Activity = ActivityId.HideAndSeek, Mode = isChallenge ? ActivityMode.Challenge : ActivityMode.Free, Challenge = challenge,
                        Difficulty = 5, Seed = seed, Seconds = rules.ChallengeSeconds(challenge, HideSeekRules.BaseSeconds), SpeedMultiplier = 1f,
                        Theme = HideSeekStage.PrairieTheme, AnimalCount = 1, Animal0 = animals[0].InstanceId, Rules = rules,
                        Thresholds = rules.Thresholds(challenge),
                    };
                }

                void Begin(in ActivitySetup setup)
                {
                    var request = new ActivityRequest { Activity = setup.Activity, Mode = setup.Mode, Challenge = setup.Challenge, Theme = setup.Theme };
                    stage.Prepare(in request);
                    if (!stage.Ready)
                        Debug.LogWarning("[HideSeekCapture] the stage is not ready (spots, shared assets?)");
                    controller.Begin(in setup);
                    stage.Enter(in setup, host);
                    for (int i = 0; i < stage.HiddenShown; i++)
                    {
                        var animator = stage.View(i) != null ? stage.View(i).Animator : null;
                        if (animator == null)
                            continue;
                        animator.Rebind();
                        animator.Update(0f);
                    }
                    if (hud != null)
                    {
                        var model = new ActivityHudModel
                        {
                            Activity = ActivityId.HideAndSeek, Mode = setup.Mode, Duration = setup.Seconds, Star1 = setup.Thresholds.One,
                            Star2 = setup.Thresholds.Two, Star3 = setup.Thresholds.Three, Best = 640,
                        };
                        hud.Show(in model, texts[0].text, new NoActions());
                    }
                }

                void Run(float seconds, bool advanceGame = true)
                {
                    int frames = Mathf.Max(1, Mathf.RoundToInt(seconds / Step));
                    for (int f = 0; f < frames; f++)
                    {
                        clock += Step;
                        if (advanceGame)
                            controller.Tick(Step);
                        stage.Tick(Step, Step);
                        for (int i = 0; i < stage.HiddenShown; i++)
                            if (stage.View(i) != null && stage.View(i).Animator != null)
                                stage.View(i).Animator.Update(Step);
                        vfx?.Tick(clock);
                        if (hud != null)
                        {
                            hud.SetScore(controller.Score);
                            hud.SetTimeLeft(Mathf.Max(0f, controller.Seconds - controller.Elapsed));
                            hud.SetStreak(controller.Streak);
                            hud.Tick(Step);
                        }
                    }
                }

                void Shot(string label)
                {
                    // Edit mode: the hearts' particles are simulated to their age.
                    if (vfxStart >= 0f)
                        foreach (var system in root.GetComponentsInChildren<ParticleSystem>(false))
                            system.Simulate(Mathf.Max(0.01f, clock - vfxStart), true, true);
                    atmosphere.Tick(clock + 1000f);
                    string path = $"{CaptureUtility.DefaultFolder}/{prefix}_{label}.png";
                    shots.Add(CaptureUtility.RenderWithFreshCamera(camera, Width, Height, path, cam =>
                    {
                        if (canvas != null)
                        {
                            canvas.renderMode = RenderMode.ScreenSpaceCamera;
                            canvas.worldCamera = cam;
                            canvas.planeDistance = Mathf.Max(cam.nearClipPlane + 0.1f, 0.5f);
                            if (handle != null)
                                handle.Invoke(canvas.GetComponent<CanvasScaler>(), null);
                            Canvas.ForceUpdateCanvases();
                        }
                        scatter.Render(cam);
                    }));
                }

                int TapMostVisible()
                {
                    int animal = stage.MostVisible();
                    if (animal < 0)
                        return -1;
                    int spot = controller.AnimalSpot(animal);
                    if (spot >= 0)
                        stage.TapAt(stage.SpotScreen(spot));
                    return animal;
                }

                // 1. The start at d5: the animals hidden, the find tip and the paw on the most visible one (each language).
                Begin(Setup(-1, 0x48534B31u));
                Run(HideSeekLayout.TutorialDelay + 0.3f);
                foreach (var (language, text) in texts)
                {
                    hud?.ShowTip(text.Get(HideSeekStage.TipFind), 2f);
                    Run(0.25f, false);
                    Shot("start_" + language);
                }

                // 2. A hint: an ear twitches, the decor's grass ripples (H-VFX-ACT-002-03), the hint tip.
                for (int f = 0; f < 600 && !AnyHinting(controller); f++)
                    Run(Step);
                Run(0.35f);
                foreach (var (language, text) in texts)
                {
                    hud?.ShowTip(text.Get(HideSeekStage.TipHint), 2f);
                    Run(0.1f, false);
                    Shot("hint_" + language);
                }

                // 3. « Coucou » mid-hop: the heart above it, the ring where it lands; the items tip.
                TapMostVisible();
                vfxStart = clock;
                Run(0.2f);
                foreach (var (language, text) in texts)
                {
                    hud?.ShowTip(text.Get(HideSeekStage.TipItems), 2f);
                    Shot("coucou_" + language);
                }
                Run(0.6f);

                // 4. The magnifier: a soft glass ring around the nearest occupied spot.
                stage.UseItem(HideSeekStage.MagnifierItem);
                Run(0.45f);
                hud?.HideTip();
                Shot("magnifier");
                vfxStart = -1f;

                // 5. All found: every animal out, gathered in front, happy, hearts.
                for (int guard = 0; guard < 12 && !controller.AllFound; guard++)
                {
                    TapMostVisible();
                    Run(0.25f);
                }
                vfxStart = clock;
                Run(HideSeekLayout.HopSeconds + HideSeekLayout.GatherDelay + HideSeekLayout.GatherSeconds + 0.4f);
                stage.End();
                Run(0.3f, false);
                Shot("all_found");

                // 6. Challenge 7 « Dans le noir »: the moonlit refuge, the lit circle around the finger.
                stage.Exit();
                vfxStart = -1f;
                host.Tutorial = false;
                Begin(Setup(HideSeekRules.NightChallenge, 0x48534B77u));
                int target = stage.MostVisible();
                var finger = target >= 0 ? stage.AnimalScreen(target) + new Vector2(60f, 40f) : new Vector2(Width * 0.5f, Height * 0.5f);
                stage.PointerAt(finger, true);
                Run(0.8f);
                Shot("night");
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
                atmosphere.SetNight(false);
            }
        }

        private static bool AnyHinting(HideSeekController game)
        {
            for (int i = 0; i < game.HiddenCount; i++)
                if (game.AnimalHinting(i))
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
