using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.World;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Capture
{
    public static partial class WorldCapture
    {
        [MenuItem("Pocket Wilds/Capture/Prairie Magnifique (T33)", priority = 526)]
        public static void CaptureMagnifiqueMenu() => Debug.Log("[WorldCapture] " + CaptureMagnifique("T33", 6));

        /// <summary>
        /// T33 beauty review frames: start and meadow chunks with their scatter, the gameplay portrait frame on every
        /// tier (Fluide, Équilibrée, Magnifique side by side), the three moods on Équilibrée, a close three-quarter
        /// view and a wide diorama view. Scatter is drawn by a ScatterRenderer just before each render.
        /// </summary>
        public static string CaptureMagnifique(string prefix, int rabbits, int width = 540, int height = 1170)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var quality = AssetDatabase.LoadAssetAtPath<WorldQualityProfile>(GrasslandPipeline.WorldQualityPath);
            int level0 = QualitySettings.GetQualityLevel();
            var temp = new List<GameObject>();
            var results = new List<string>();
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var start = SceneChunk();
                    var meadow = WorldContentPipeline.SpawnForTools(WorldContentPipeline.MeadowAddress);
                    var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var sun = Object.FindAnyObjectByType<Light>();
                    var camera = Camera.main;
                    var chunks = new[] { start, meadow };
                    foreach (var chunk in chunks)
                        chunk.Bind(palettes);
                    var morning = AssetDatabase.LoadAssetAtPath<LightingProfile>(LightingProfilePath("Morning"));
                    var evening = AssetDatabase.LoadAssetAtPath<LightingProfile>(LightingProfilePath("Evening"));
                    string folder = CaptureUtility.DefaultFolder;

                    var lifeKit = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(GrasslandPipeline.LifeKitPath);
                    ScatterRenderer scatter = null;
                    AmbientLife life = null;
                    float lifeTime = 0f;
                    void Simulate(float seconds)
                    {
                        for (float t = 0f; t < seconds; t += 1f / 30f)
                        {
                            lifeTime += 1f / 30f;
                            life?.Tick(1f / 30f, lifeTime);
                        }
                    }
                    void Setup(QualityTier tier)
                    {
                        QualitySettings.SetQualityLevel((int)tier, true);
                        entry.Lighting.Apply(sun, camera, tier);
                        foreach (var chunk in chunks)
                            chunk.ApplyQuality(tier);
                        var tierQuality = quality != null ? quality.Get(tier) : WorldQualityProfile.Default(tier);
                        scatter = new ScatterRenderer(palettes, tierQuality.scatterDensity);
                        foreach (var chunk in chunks)
                            scatter.Add(chunk);
                        life = lifeKit != null ? new AmbientLife(lifeKit, palettes, tierQuality.ambientLifeDensity) : null;
                        foreach (var chunk in chunks)
                            life?.Add(chunk);
                        lifeTime = 0f;
                        Simulate(4f);
                        foreach (var go in temp)
                            Object.DestroyImmediate(go);
                        temp.Clear();
                        Populate(registry, palettes, start, rabbits, temp);
                    }
                    string Shot(string name, int w, int h)
                    {
                        string path = $"{folder}/{prefix}_{name}.png";
                        // First render after a tier switch may still compile shader variants: render twice.
                        void Draw()
                        {
                            scatter.Render(camera);
                            life?.Render(camera, lifeTime);
                        }
                        CaptureUtility.Render(camera, w, h, path, 4, Draw);
                        return CaptureUtility.Render(camera, w, h, path, 4, Draw);
                    }

                    for (int level = 0; level < QualitySettings.names.Length; level++)
                    {
                        Setup((QualityTier)level);
                        results.Add(Shot("world_" + QualitySettings.names[level], width, height));
                    }

                    Setup(QualityTier.Medium);
                    if (morning != null && evening != null)
                    {
                        var tierQuality = quality != null ? quality.Get(QualityTier.Medium) : WorldQualityProfile.Default(QualityTier.Medium);
                        var atmosphere = new WorldAtmosphere(morning, entry.Lighting, evening, sun, camera, tierQuality);
                        foreach (var mood in new[] { LightingMood.Morning, LightingMood.Day, LightingMood.Evening })
                        {
                            atmosphere.ForceMood(mood);
                            atmosphere.Tick(0f);
                            results.Add(Shot("mood_" + mood, width, height));
                        }
                        atmosphere.ForceMood(LightingMood.Day);
                        atmosphere.Tick(0f);
                    }

                    Setup(QualityTier.High);
                    var position = camera.transform.position;
                    var rotation = camera.transform.rotation;
                    float far = camera.farClipPlane, fov = camera.fieldOfView;
                    var focus = start.EggSpawn.position;
                    camera.transform.position = focus + new Vector3(2.4f, 2.6f, -5.2f);
                    camera.transform.LookAt(focus + new Vector3(0f, 0.4f, 1.2f));
                    results.Add(Shot("world_close", 1200, 760));
                    camera.transform.position = focus + new Vector3(0.6f, 0.9f, -2.2f);
                    camera.transform.LookAt(focus + new Vector3(-0.4f, 0.15f, 1.0f));
                    results.Add(Shot("world_ground", 1200, 760));
                    // Life close-ups: butterflies over the flowers, robins on the grass, then the robins startled.
                    var butterflies = Anchor(start, "VFX_Butterfly_01");
                    var birds = Anchor(start, "VFX_Birds_01");
                    if (butterflies != null && birds != null)
                    {
                        camera.transform.position = butterflies.position + new Vector3(1.2f, 0.5f, -2.6f);
                        camera.transform.LookAt(butterflies.position + new Vector3(0f, -0.1f, 0f));
                        results.Add(Shot("life_butterflies", 1200, 760));
                        camera.transform.position = birds.position + new Vector3(0.9f, 0.7f, -1.8f);
                        camera.transform.LookAt(birds.position + new Vector3(0f, 0.15f, 0f));
                        results.Add(Shot("life_birds", 1200, 760));
                        life?.Startle(birds.position, 2f);
                        Simulate(0.7f);
                        camera.transform.position = birds.position + new Vector3(1.6f, 1.2f, -3.4f);
                        camera.transform.LookAt(birds.position + new Vector3(0f, 1.1f, 0.6f));
                        results.Add(Shot("life_birds_startled", 1200, 760));
                    }
                    camera.farClipPlane = 200f;
                    camera.fieldOfView = 45f;
                    camera.transform.position = new Vector3(7f, 9f, -16f);
                    camera.transform.LookAt(new Vector3(7f, 0f, 1.5f));
                    results.Add(Shot("world_wide", 1400, 900));
                    camera.transform.SetPositionAndRotation(position, rotation);
                    camera.farClipPlane = far;
                    camera.fieldOfView = fov;
                }
                finally
                {
                    QualitySettings.SetQualityLevel(level0, true);
                    foreach (var go in temp)
                        Object.DestroyImmediate(go);
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }
            return string.Join(", ", results);
        }

        private static Transform Anchor(WorldChunk chunk, string name)
        {
            foreach (var anchor in chunk.VfxAnchors)
            {
                if (anchor != null && anchor.name == name)
                    return anchor;
            }
            return null;
        }

        public static string LightingProfilePath(string mood) => $"Assets/_Game/Art/Lighting/Grassland/LP_Grassland_{mood}.asset";
    }
}
