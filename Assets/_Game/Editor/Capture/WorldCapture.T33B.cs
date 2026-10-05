using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.Lighting;
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
        [MenuItem("Pocket Wilds/Capture/Weather, Night and Sun Rays (T33B)", priority = 527)]
        public static void CaptureWeatherMenu()
        {
            // One shot per editor frame: Graphics.RenderMesh* draws stay queued until the frame ends.
            foreach (var shot in WeatherShots)
                Debug.Log("[WorldCapture] " + CaptureWeather("T33B", 6, shot));
        }

        public static readonly string[] WeatherShots = { "rain", "rain_wide", "rainbow_wide", "night", "night_sky", "sunrays", "sunrays_wide" };

        /// <summary>
        /// T33B frame, one per call (see <see cref="WeatherShots"/>): light rain, the rainbow after it (wide sky
        /// frame), the night mood of events (gameplay frame and the starry sky), the Magnifique sun rays. Draws issued
        /// with Graphics.RenderMesh* stay queued for every render of the same editor frame, so each shot is its own
        /// call. Extra chunks given by address are spawned at their set origin.
        /// </summary>
        public static string CaptureWeather(string prefix, int rabbits, string shot, params string[] extraChunks)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var kit = AssetDatabase.LoadAssetAtPath<WeatherKit>(GrasslandPipeline.WeatherKitPath);
            var lifeKit = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(GrasslandPipeline.LifeKitPath);
            var morning = AssetDatabase.LoadAssetAtPath<LightingProfile>(LightingPipeline.MorningProfilePath);
            var evening = AssetDatabase.LoadAssetAtPath<LightingProfile>(LightingPipeline.EveningProfilePath);
            var night = AssetDatabase.LoadAssetAtPath<LightingProfile>(LightingPipeline.NightProfilePath);
            int level0 = QualitySettings.GetQualityLevel();
            var temp = new List<GameObject>();
            var results = new List<string>();
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var start = SceneChunk();
                    var chunks = new List<WorldChunk> { start, WorldContentPipeline.SpawnForTools(WorldContentPipeline.MeadowAddress) };
                    foreach (var address in extraChunks)
                        chunks.Add(WorldContentPipeline.SpawnForTools(address));
                    var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var sun = Object.FindAnyObjectByType<Light>();
                    var camera = Camera.main;
                    foreach (var chunk in chunks)
                        chunk.Bind(palettes);

                    ScatterRenderer scatter = null;
                    AmbientLife life = null;
                    WeatherSystem weather = null;
                    WorldAtmosphere atmosphere = null;
                    void Setup(QualityTier tier)
                    {
                        QualitySettings.SetQualityLevel((int)tier, true);
                        entry.Lighting.Apply(sun, camera, tier);
                        var quality = WorldQualityProfile.Default(tier);
                        foreach (var chunk in chunks)
                            chunk.ApplyQuality(tier);
                        scatter = new ScatterRenderer(palettes, quality.scatterDensity);
                        scatter.SetDistanceFade(quality.scatterFadeStart, quality.scatterFadeEnd);
                        life = lifeKit != null ? new AmbientLife(lifeKit, palettes, quality.ambientLifeDensity) : null;
                        foreach (var chunk in chunks)
                        {
                            scatter.Add(chunk);
                            life?.Add(chunk);
                        }
                        for (int i = 0; i < 120; i++)
                            life?.Tick(1f / 30f, i / 30f);
                        atmosphere = new WorldAtmosphere(morning, entry.Lighting, evening, sun, camera, quality, night);
                        atmosphere.ForceMood(LightingMood.Day);
                        weather = kit != null ? new WeatherSystem(kit, atmosphere, quality) : null;
                        foreach (var go in temp)
                            Object.DestroyImmediate(go);
                        temp.Clear();
                        Populate(registry, palettes, start, rabbits, temp);
                    }
                    string Shot(string name, int w, int h)
                    {
                        atmosphere.ForceHour(13f);
                        atmosphere.Tick(0f);
                        var focus = start.EggSpawn.position;
                        string path = $"{CaptureUtility.DefaultFolder}/{prefix}_{name}.png";
                        return CaptureUtility.RenderWithFreshCamera(camera, w, h, path, cam =>
                        {
                            scatter.Render(cam);
                            life?.Render(cam, 4f);
                            weather?.Render(cam, focus);
                        });
                    }
                    var position = camera.transform.position;
                    var rotation = camera.transform.rotation;
                    float far = camera.farClipPlane, fov = camera.fieldOfView;
                    void Restore()
                    {
                        camera.transform.SetPositionAndRotation(position, rotation);
                        camera.farClipPlane = far;
                        camera.fieldOfView = fov;
                    }
                    void Wide()
                    {
                        camera.farClipPlane = 200f;
                        camera.fieldOfView = 50f;
                        var focus = start.EggSpawn.position;
                        camera.transform.position = focus + new Vector3(-1.5f, 3.2f, -10.5f);
                        camera.transform.LookAt(focus + new Vector3(2.5f, 2.6f, 6f));
                    }

                    bool wide = shot.EndsWith("_wide", System.StringComparison.Ordinal) || shot == "night_sky";
                    Setup(shot.StartsWith("sunrays", System.StringComparison.Ordinal) ? QualityTier.High : QualityTier.Medium);
                    if (shot.StartsWith("rain_", System.StringComparison.Ordinal) || shot == "rain")
                        weather?.Force(1f, 0f);
                    else if (shot.StartsWith("rainbow", System.StringComparison.Ordinal))
                        weather?.Force(0f, 1f);
                    if (shot.StartsWith("night", System.StringComparison.Ordinal))
                        atmosphere.SetNight(true);
                    if (wide)
                        Wide();
                    if (shot == "night_sky")
                        camera.transform.LookAt(camera.transform.position + new Vector3(0.3f, 0.75f, 1f));
                    results.Add(Shot(shot, wide ? 1400 : 540, wide ? 900 : 1170));
                    Restore();
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

        /// <summary>
        /// T33B strip frames, one per call: "chunk:&lt;Name&gt;" (gameplay frame centered on that chunk), "close:&lt;Name&gt;"
        /// (three-quarter view of its landmark or water), "panorama" (the whole strip from above). All eight chunks are
        /// spawned with their scatter and life on the given tier, under the day mood.
        /// </summary>
        public static string CaptureStrip(string prefix, string shot, QualityTier tier = QualityTier.High)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var lifeKit = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(GrasslandPipeline.LifeKitPath);
            int level0 = QualitySettings.GetQualityLevel();
            var temp = new List<GameObject>();
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var start = SceneChunk();
                    var chunks = new List<WorldChunk> { start };
                    var byName = new Dictionary<string, WorldChunk>();
                    foreach (var entry in WorldContentPipeline.GrasslandChunks)
                    {
                        if (entry.pinned)
                            continue;
                        var chunk = WorldContentPipeline.SpawnForTools(entry.address);
                        chunks.Add(chunk);
                        byName[chunk.name.Replace("PF_Chunk_Grassland_", string.Empty)] = chunk;
                    }
                    var sceneEntry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var sun = Object.FindAnyObjectByType<Light>();
                    var camera = Camera.main;
                    QualitySettings.SetQualityLevel((int)tier, true);
                    sceneEntry.Lighting.Apply(sun, camera, tier);
                    var quality = WorldQualityProfile.Default(tier);
                    var scatter = new ScatterRenderer(palettes, quality.scatterDensity);
                    scatter.SetDistanceFade(quality.scatterFadeStart, quality.scatterFadeEnd);
                    var life = lifeKit != null ? new AmbientLife(lifeKit, palettes, quality.ambientLifeDensity) : null;
                    foreach (var chunk in chunks)
                    {
                        chunk.Bind(palettes);
                        chunk.ApplyQuality(tier);
                        scatter.Add(chunk);
                        life?.Add(chunk);
                    }
                    for (int i = 0; i < 120; i++)
                        life?.Tick(1f / 30f, i / 30f);
                    Populate(registry, palettes, start, 6, temp);

                    var offset = camera.transform.position - start.EggSpawn.position;
                    int w = 540, h = 1170;
                    if (shot == "panorama")
                    {
                        // An overview, not a game frame: no distance fog, no height fog.
                        RenderSettings.fog = false;
                        Shader.SetGlobalVector("_PW_HeightFog", Vector4.zero);
                        scatter.SetDistanceFade(0f, 0f);
                        camera.farClipPlane = 300f;
                        camera.fieldOfView = 36f;
                        camera.transform.position = new Vector3(49f, 44f, -48f);
                        camera.transform.LookAt(new Vector3(49f, 0f, 1f));
                        w = 1800;
                        h = 760;
                    }
                    else
                    {
                        string name = shot.Substring(shot.IndexOf(':') + 1);
                        var chunk = byName[name];
                        var focus = chunk.transform.position + new Vector3(0f, 0f, 0.6f);
                        if (shot.StartsWith("chunk:", System.StringComparison.Ordinal))
                        {
                            camera.transform.position = focus + offset;
                        }
                        else
                        {
                            var target = focus + new Vector3(0.6f, 1.4f, 2.4f);
                            camera.farClipPlane = 120f;
                            camera.fieldOfView = 42f;
                            camera.transform.position = target + new Vector3(4.5f, 2.2f, -9.5f);
                            camera.transform.LookAt(target);
                            w = 1400;
                            h = 900;
                        }
                    }
                    string path = $"{CaptureUtility.DefaultFolder}/{prefix}_{shot.Replace(':', '_')}.png";
                    return CaptureUtility.RenderWithFreshCamera(camera, w, h, path, cam =>
                    {
                        scatter.Render(cam);
                        life?.Render(cam, 4f);
                    });
                }
                finally
                {
                    QualitySettings.SetQualityLevel(level0, true);
                    foreach (var go in temp)
                        Object.DestroyImmediate(go);
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }
        }
    }
}
