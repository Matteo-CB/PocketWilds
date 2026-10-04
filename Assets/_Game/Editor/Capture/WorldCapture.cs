using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Eggs;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Capture
{
    /// <summary>
    /// ED-008 reference captures of SC_VerticalSlice_Grassland (MVP Asset Pack A15): gameplay portrait frame
    /// with the egg and a few rabbits, per quality level, plus a closer three-quarter view. Temporary objects
    /// are never saved: the scene is reopened from disk afterwards.
    /// </summary>
    public static class WorldCapture
    {
        public const string ScenePath = "Assets/_Game/Scenes/SC_VerticalSlice_Grassland.unity";

        [MenuItem("Pocket Wilds/Capture/Vertical Slice References", priority = 521)]
        public static void CaptureMenu() => Debug.Log("[WorldCapture] " + CaptureVerticalSlice("T15", 6));

        public static string CaptureVerticalSlice(string prefix, int rabbits)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var temp = new List<GameObject>();
            int quality = QualitySettings.GetQualityLevel();
            var results = new List<string>();
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var chunk = Object.FindAnyObjectByType<WorldChunk>();
                    var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var sun = Object.FindAnyObjectByType<Light>();
                    chunk.Bind(palettes);

                    var camera = Camera.main;
                    for (int level = 0; level < QualitySettings.names.Length; level++)
                    {
                        var tier = (QualityTier)level;
                        QualitySettings.SetQualityLevel(level, true);
                        if (entry != null && entry.Lighting != null)
                            entry.Lighting.Apply(sun, camera, tier);
                        else
                            PresentationQuality.Apply(tier);
                        chunk.ApplyQuality(tier);
                        // Views read the quality (blob shadows) when configured: repopulate per tier.
                        foreach (var go in temp)
                            Object.DestroyImmediate(go);
                        temp.Clear();
                        Populate(registry, palettes, chunk, rabbits, temp);
                        string path = $"{CaptureUtility.DefaultFolder}/{prefix}_world_{QualitySettings.names[level]}.png";
                        // First render after a pipeline switch may still compile shader variants: render twice.
                        CaptureUtility.Render(camera, 540, 1170, path);
                        results.Add(CaptureUtility.Render(camera, 540, 1170, path));
                    }
                    QualitySettings.SetQualityLevel(quality, true);
                    if (entry != null && entry.Lighting != null)
                        entry.Lighting.Apply(sun, camera, (QualityTier)quality);
                    chunk.ApplyQuality((QualityTier)quality);
                    foreach (var go in temp)
                        Object.DestroyImmediate(go);
                    temp.Clear();
                    Populate(registry, palettes, chunk, rabbits, temp);

                    var position = camera.transform.position;
                    var rotation = camera.transform.rotation;
                    var focus = chunk.EggSpawn.position;
                    camera.transform.position = focus + new Vector3(2.4f, 2.6f, -5.2f);
                    camera.transform.LookAt(focus + new Vector3(0f, 0.4f, 1.2f));
                    results.Add(CaptureUtility.Render(camera, 1200, 760, $"{CaptureUtility.DefaultFolder}/{prefix}_world_close.png"));
                    camera.transform.SetPositionAndRotation(position, rotation);
                }
                finally
                {
                    QualitySettings.SetQualityLevel(quality, true);
                    foreach (var go in temp)
                        Object.DestroyImmediate(go);
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }
            return string.Join(", ", results);
        }

        /// <summary>Close-up of any point of the vertical slice with palettes and the Medium lighting (prop reviews).</summary>
        public static string CaptureCloseUp(string fileName, Vector3 target, Vector3 cameraOffset, int width = 900, int height = 900)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            string result;
            using (var palettes = new PaletteTable(registry))
            {
                Object.FindAnyObjectByType<WorldChunk>().Bind(palettes);
                var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                var camera = Camera.main;
                entry.Lighting.Apply(Object.FindAnyObjectByType<Light>(), camera, QualityTier.Medium);
                camera.transform.position = target + cameraOffset;
                camera.transform.LookAt(target);
                string path = $"{CaptureUtility.DefaultFolder}/{fileName}.png";
                CaptureUtility.Render(camera, width, height, path);
                result = CaptureUtility.Render(camera, width, height, path);
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return result;
        }

        private static void Populate(ContentRegistry registry, PaletteTable palettes, WorldChunk chunk, int rabbits, List<GameObject> temp)
        {
            var config = AssetDatabase.LoadAssetAtPath<HatchPresentationConfig>(EggPipeline.HatchConfigPath);
            var egg = Object.Instantiate(config.eggPrefab);
            var basic = registry.Eggs.Get(ContentId.FromSource("egg.grassland.basic"));
            palettes.TryGetRow(basic.Id, out int row);
            egg.Configure(chunk.EggSpawn.position, 180f, EggVisualPacking.FromDefinition(basic, row));
            temp.Add(egg.gameObject);

            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            catalog.TryGet("PF_Animal_Rabbit", out var entry);
            var resolver = new AnimalVisualResolver(registry, palettes);
            string[] variants = { "brown", "white", "grey", "spotted", "black", "sakura" };
            Vector3[] offsets =
            {
                new Vector3(1.0f, 0f, 0.4f), new Vector3(-1.1f, 0f, 0.9f), new Vector3(0.3f, 0f, 1.9f),
                new Vector3(-0.5f, 0f, -1.3f), new Vector3(1.2f, 0f, -0.8f), new Vector3(-0.2f, 0f, 3.0f),
            };
            for (int i = 0; i < rabbits && i < offsets.Length; i++)
            {
                var animal = new AnimalInstance
                {
                    InstanceId = (uint)(i + 1), Seed = (uint)(i * 31 + 3),
                    Species = ContentId.FromSource("species.rabbit"),
                    Variant = ContentId.FromSource("variant.rabbit." + variants[i % variants.Length]),
                };
                resolver.TryResolve(animal, chunk.EggSpawn.position + offsets[i], 150f + i * 23f, out var snapshot);
                var view = Object.Instantiate(entry.prefab);
                view.Configure(snapshot);
                view.Animator.Rebind();
                view.Animator.Update(0.4f * i);
                temp.Add(view.gameObject);
            }
        }
    }
}
