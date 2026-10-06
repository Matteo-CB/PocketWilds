using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Environment;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Capture
{
    public static partial class WorldCapture
    {
        [MenuItem("Pocket Wilds/Capture/No-Collision Refuge (T33C)", priority = 528)]
        public static void CaptureCollisionMenu()
        {
            foreach (var shot in CollisionShots)
                Debug.Log("[WorldCapture] " + CaptureCollision("T33C", shot));
        }

        public static readonly string[] CollisionShots = { "refuge", "rock_close" };

        private static readonly string[] CollisionMix =
        {
            "rabbit.brown", "squirrel.red", "hedgehog.brown", "rabbit.white", "squirrel.autumn", "rabbit.grey", "hedgehog.mushroom",
            "rabbit.spotted", "squirrel.red", "rabbit.black",
        };

        /// <summary>
        /// T33C proof frames: animals of the three species live <paramref name="seconds"/> s in the refuge under the
        /// no-collision rule (scheduler with the chunk's obstacles, ground and egg spot, insects and birds around
        /// them), then the gameplay frame ("refuge") or a close view on the rock nearest the egg ("rock_close"). The
        /// result line gives the smallest clearance seen during the whole run (never below zero).
        /// </summary>
        public static string CaptureCollision(string prefix, string shot, int animals = 20, float seconds = 60f)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var lifeKit = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(GrasslandPipeline.LifeKitPath);
            int level0 = QualitySettings.GetQualityLevel();
            var temp = new List<GameObject>();
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var chunk = SceneChunk();
                    var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var sun = Object.FindAnyObjectByType<Light>();
                    var camera = Camera.main;
                    const QualityTier tier = QualityTier.High;
                    QualitySettings.SetQualityLevel((int)tier, true);
                    entry.Lighting.Apply(sun, camera, tier);
                    var quality = WorldQualityProfile.Default(tier);
                    chunk.Bind(palettes);
                    chunk.ApplyQuality(tier);
                    var scatter = new ScatterRenderer(palettes, quality.scatterDensity);
                    scatter.SetDistanceFade(quality.scatterFadeStart, quality.scatterFadeEnd);
                    scatter.Add(chunk);

                    var config = AssetDatabase.LoadAssetAtPath<HatchPresentationConfig>(Eggs.EggPipeline.HatchConfigPath);
                    var egg = Object.Instantiate(config.eggPrefab);
                    var basic = registry.Eggs.Get(ContentId.FromSource("egg.grassland.basic"));
                    palettes.TryGetRow(basic.Id, out int eggRow);
                    egg.Configure(chunk.EggSpawn.position, 180f, EggVisualPacking.FromDefinition(basic, eggRow));
                    temp.Add(egg.gameObject);

                    var scheduler = new AnimalScheduler(animals + 1, SchedulerSettings.ForQuality(tier, false), chunk.WanderArea);
                    scheduler.SetGround(chunk.Ground);
                    scheduler.SetObstacles(chunk.GetWorldObstacles());
                    scheduler.SetInterests(chunk.GetWorldInterests());
                    scheduler.SetEggSpot(chunk.EggSpawn.position, RefugeController.EggSpotRadius, RefugeController.EggBodyRadius);
                    var resolver = new AnimalVisualResolver(registry, palettes);
                    var area = chunk.WanderArea;
                    var views = new List<AnimalView>();
                    for (int i = 0; i < animals; i++)
                    {
                        string kind = CollisionMix[i % CollisionMix.Length];
                        float angle = i * 2.39996f;
                        var position = area.Center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (area.Radius * 0.9f * Mathf.Sqrt((i + 0.5f) / animals));
                        var view = Spawn(registry, resolver, kind, (uint)(i + 1), (uint)(i * 7919 + 11), position, i * 47f, 0.3f * i);
                        var species = registry.Species.Get(ContentId.FromSource("species." + kind.Substring(0, kind.IndexOf('.'))));
                        catalog.TryGet(species.VisualKey, out var visual);
                        scheduler.Register(view, (uint)(i + 1), (uint)(i * 7919 + 11), view.transform.position, i * 47f, visual.behavior, 0f,
                            view.transform.localScale.x);
                        views.Add(view);
                        temp.Add(view.gameObject);
                    }
                    var life = lifeKit != null ? new AmbientLife(lifeKit, palettes, quality.ambientLifeDensity) : null;
                    life?.Add(chunk);
                    life?.SetAnimals(scheduler);

                    const float Dt = 1f / 30f;
                    float time = 0f, worstAnimals = float.MaxValue, worstLife = float.MaxValue;
                    int frames = Mathf.RoundToInt(seconds / Dt);
                    for (int f = 0; f < frames; f++)
                    {
                        time += Dt;
                        scheduler.Tick(time, Dt);
                        life?.Tick(Dt, time);
                        if (f % 5 != 0)
                            continue;
                        for (int i = 0; i < scheduler.Count; i++)
                            worstAnimals = Mathf.Min(worstAnimals, scheduler.Clearance(scheduler.GetActiveHandle(i)));
                        if (life != null)
                            worstLife = Mathf.Min(worstLife, life.MinClearance());
                    }
                    // The Animators were only told their states: settle their poses for the frame.
                    foreach (var view in views)
                        view.Animator.Update(0.6f);

                    int w = 540, h = 1170;
                    if (shot == "rock_close")
                    {
                        var points = chunk.GetWorldInterests();
                        var rock = chunk.EggSpawn.position;
                        float best = float.MaxValue;
                        foreach (var p in points)
                        {
                            float d = Vector3.Distance(p.position, chunk.EggSpawn.position);
                            if (p.kind == InterestKind.Inspect && p.standOff > 0.3f && d < best)
                            {
                                best = d;
                                rock = p.position;
                            }
                        }
                        camera.farClipPlane = 60f;
                        camera.fieldOfView = 40f;
                        camera.transform.position = rock + new Vector3(0.4f, 2.1f, -3.4f);
                        camera.transform.LookAt(rock + new Vector3(0f, 0.15f, 0f));
                        w = 1400;
                        h = 900;
                    }
                    string path = $"{CaptureUtility.DefaultFolder}/{prefix}_{shot}.png";
                    string result = CaptureUtility.RenderWithFreshCamera(camera, w, h, path, cam =>
                    {
                        scatter.Render(cam);
                        life?.Render(cam, time);
                    });
                    return $"{result} | {animals} animals over {seconds:F0} s: smallest clearance animals {worstAnimals * 100f:F1} cm, " +
                           $"insects and birds {worstLife * 100f:F1} cm, detours {scheduler.Detours}, blocked walks {scheduler.BlockedWalks}, " +
                           $"greetings {scheduler.SocialGreetings}, visits {scheduler.InterestVisits}";
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
