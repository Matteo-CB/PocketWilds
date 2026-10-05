using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Eggs;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using PocketWilds.Editor.World;
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
                    var chunk = SceneChunk();
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
                SceneChunk().Bind(palettes);
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

        /// <summary>
        /// The start chunk for a capture: streamed at runtime since T27, so tools instance the prefab in the open
        /// scene (reopened from disk afterwards, never saved).
        /// </summary>
        internal static WorldChunk SceneChunk()
        {
            var existing = Object.FindAnyObjectByType<WorldChunk>();
            return existing != null ? existing : WorldContentPipeline.SpawnForTools(WorldContentPipeline.StartAddress);
        }

        [MenuItem("Pocket Wilds/Capture/Chunk Seam (T27)", priority = 525)]
        public static void CaptureSeamMenu() => Debug.Log("[WorldCapture] " + CaptureSeam("T27"));

        /// <summary>Start and meadow chunks side by side: overview, three-quarter view on the seam, gameplay frames.</summary>
        public static string CaptureSeam(string prefix)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var results = new List<string>();
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var start = SceneChunk();
                    var meadow = WorldContentPipeline.SpawnForTools(WorldContentPipeline.MeadowAddress);
                    var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var camera = Camera.main;
                    entry.Lighting.Apply(Object.FindAnyObjectByType<Light>(), camera, QualityTier.Medium);
                    foreach (var chunk in new[] { start, meadow })
                    {
                        chunk.Bind(palettes);
                        chunk.ApplyQuality(QualityTier.Medium);
                    }
                    var position = camera.transform.position;
                    var rotation = camera.transform.rotation;
                    float fov = camera.fieldOfView, far = camera.farClipPlane;
                    var egg = start.EggSpawn.position;
                    float seam = meadow.transform.position.x * 0.5f;
                    string folder = CaptureUtility.DefaultFolder;

                    camera.farClipPlane = 200f;
                    camera.fieldOfView = 50f;
                    camera.transform.position = new Vector3(seam, 16f, -14f);
                    camera.transform.LookAt(new Vector3(seam, 0f, 0f));
                    CaptureUtility.Render(camera, 1400, 900, $"{folder}/{prefix}_seam_overview.png");
                    results.Add(CaptureUtility.Render(camera, 1400, 900, $"{folder}/{prefix}_seam_overview.png"));
                    camera.fieldOfView = fov;
                    camera.transform.position = new Vector3(seam - 1.5f, 4.5f, -9.5f);
                    camera.transform.LookAt(new Vector3(seam, 0.2f, 0.5f));
                    results.Add(CaptureUtility.Render(camera, 1200, 760, $"{folder}/{prefix}_seam_threequarter.png"));
                    camera.transform.SetPositionAndRotation(position + new Vector3(seam - egg.x, 0f, 0f), rotation);
                    results.Add(CaptureUtility.Render(camera, 540, 1170, $"{folder}/{prefix}_seam_gameplay.png"));
                    camera.transform.SetPositionAndRotation(position + new Vector3(meadow.transform.position.x - egg.x, 0f, 0f), rotation);
                    results.Add(CaptureUtility.Render(camera, 540, 1170, $"{folder}/{prefix}_meadow_gameplay.png"));
                    camera.transform.SetPositionAndRotation(position, rotation);
                    camera.farClipPlane = far;
                }
                finally
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }
            return string.Join(", ", results);
        }

        private static readonly string[] RabbitMix = { "rabbit.brown", "rabbit.white", "rabbit.grey", "rabbit.spotted", "rabbit.black", "rabbit.sakura" };

        /// <summary>The grassland species side by side in the gameplay frame, plus a closer view.</summary>
        public static readonly string[] SpeciesMix = { "rabbit.brown", "squirrel.red", "hedgehog.brown", "squirrel.autumn", "hedgehog.mushroom", "rabbit.spotted" };

        [MenuItem("Pocket Wilds/Capture/Species Mix and Hedgehog Variants", priority = 522)]
        public static void CaptureSpeciesMenu() =>
            Debug.Log("[WorldCapture] " + CaptureSpecies("T24", "hedgehog", new[] { "brown", "cream", "grey", "black", "white", "mushroom" }));

        /// <summary>
        /// Shared variant prompt: the six palettes of one species on one prefab in a 2 x 3 grid on the meadow
        /// (Medium lighting), then the gameplay frame with a species mix and a closer three-quarter view.
        /// </summary>
        public static string CaptureSpecies(string prefix, string species, string[] variants)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var temp = new List<GameObject>();
            var results = new List<string>();
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var chunk = SceneChunk();
                    var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var camera = Camera.main;
                    chunk.Bind(palettes);
                    entry.Lighting.Apply(Object.FindAnyObjectByType<Light>(), camera, QualityTier.Medium);
                    chunk.ApplyQuality(QualityTier.Medium);
                    var position = camera.transform.position;
                    var rotation = camera.transform.rotation;

                    // 2 x 3 grid, every animal facing the camera three-quarter.
                    var origin = chunk.EggSpawn.position + new Vector3(0f, 0f, 1.6f);
                    var resolver = new AnimalVisualResolver(registry, palettes);
                    for (int i = 0; i < variants.Length; i++)
                    {
                        var spot = origin + new Vector3((i % 3 - 1) * 0.85f, 0f, (i / 3) * 0.95f);
                        temp.Add(Spawn(registry, resolver, species + "." + variants[i], (uint)(i + 1), (uint)(i * 31 + 3), spot, 200f, 0.6f).gameObject);
                    }
                    camera.transform.position = origin + new Vector3(0.9f, 1.55f, -2.7f);
                    camera.transform.LookAt(origin + new Vector3(0f, 0.15f, 0.5f));
                    string grid = $"{CaptureUtility.DefaultFolder}/{prefix}_{species}_variants.png";
                    CaptureUtility.Render(camera, 1200, 800, grid);
                    results.Add(CaptureUtility.Render(camera, 1200, 800, grid));
                    foreach (var go in temp)
                        Object.DestroyImmediate(go);
                    temp.Clear();

                    camera.transform.SetPositionAndRotation(position, rotation);
                    Populate(registry, palettes, chunk, SpeciesMix.Length, temp, SpeciesMix);
                    string world = $"{CaptureUtility.DefaultFolder}/{prefix}_world_species_mix.png";
                    CaptureUtility.Render(camera, 540, 1170, world);
                    results.Add(CaptureUtility.Render(camera, 540, 1170, world));
                    var focus = chunk.EggSpawn.position;
                    camera.transform.position = focus + new Vector3(2.4f, 2.6f, -5.2f);
                    camera.transform.LookAt(focus + new Vector3(0f, 0.4f, 1.2f));
                    results.Add(CaptureUtility.Render(camera, 1200, 760, $"{CaptureUtility.DefaultFolder}/{prefix}_world_species_mix_close.png"));
                    camera.transform.SetPositionAndRotation(position, rotation);
                }
                finally
                {
                    foreach (var go in temp)
                        Object.DestroyImmediate(go);
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }
            return string.Join(", ", results);
        }

        [MenuItem("Pocket Wilds/Capture/Genetics Families", priority = 524)]
        public static void CaptureFamiliesMenu() => Debug.Log("[WorldCapture] " + CaptureFamilies("T26"));

        /// <summary>
        /// T26 reference: two families on the meadow, parents behind and six children in front, bred by the real
        /// GeneticsService (palette, carried pattern, size, traits). Children are the first distinct results.
        /// </summary>
        public static string CaptureFamilies(string prefix)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var genetics = new GeneticsService(registry);
            var temp = new List<GameObject>();
            var results = new List<string>();
            var log = new System.Text.StringBuilder();
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var chunk = SceneChunk();
                    var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var camera = Camera.main;
                    chunk.Bind(palettes);
                    entry.Lighting.Apply(Object.FindAnyObjectByType<Light>(), camera, QualityTier.Medium);
                    chunk.ApplyQuality(QualityTier.Medium);
                    var resolver = new AnimalVisualResolver(registry, palettes);
                    var egg = chunk.EggSpawn.position;

                    void Family(string species, string variantA, string variantB, Vector3 origin, uint salt)
                    {
                        var a = new AnimalInstance { InstanceId = 1, Species = ContentId.FromSource("species." + species), Variant = ContentId.FromSource($"variant.{species}.{variantA}"), Seed = 0xA000u + salt };
                        var b = new AnimalInstance { InstanceId = 2, Species = a.Species, Variant = ContentId.FromSource($"variant.{species}.{variantB}"), Seed = 0xB000u + salt };
                        temp.Add(Spawn(registry, resolver, $"{species}.{variantA}", 1, a.Seed, origin + new Vector3(-0.35f, 0f, 0.75f), 190f, 0.5f).gameObject);
                        temp.Add(Spawn(registry, resolver, $"{species}.{variantB}", 2, b.Seed, origin + new Vector3(0.35f, 0f, 0.75f), 170f, 1.1f).gameObject);
                        var seen = new HashSet<(uint, uint)>();
                        int placed = 0;
                        for (uint context = 0; context < 400 && placed < 6; context++)
                        {
                            genetics.TryBreed(a, b, context + salt * 1000u, out var child);
                            if (!seen.Add((child.Variant.Value, child.TraitBits)))
                                continue;
                            var spot = origin + new Vector3((placed % 3 - 1) * 0.42f, 0f, -(placed / 3) * 0.45f);
                            string name = $"{species}.{child.Variant.ToString().Substring(("variant." + species + ".").Length)}";
                            temp.Add(Spawn(registry, resolver, name, 10u + (uint)placed, child.Seed, spot, 180f + (placed - 2.5f) * 9f, 0.3f * placed, child.TraitBits).gameObject);
                            log.Append($"{name} size {AnimalGenome.Size(child.TraitBits)} pattern {AnimalGenome.CarriedPattern(child.TraitBits)} traits {AnimalGenome.Traits(child.TraitBits)}; ");
                            placed++;
                        }
                    }
                    Family("rabbit", "spotted", "brown", egg + new Vector3(-0.85f, 0f, 1.1f), 1);
                    Family("squirrel", "autumn", "grey", egg + new Vector3(0.95f, 0f, 1.1f), 2);

                    var position = camera.transform.position;
                    var rotation = camera.transform.rotation;
                    camera.transform.position = egg + new Vector3(0f, 1.7f, -2.4f);
                    camera.transform.LookAt(egg + new Vector3(0f, 0.15f, 1.2f));
                    string path = $"{CaptureUtility.DefaultFolder}/{prefix}_genetics_families.png";
                    CaptureUtility.Render(camera, 1200, 760, path);
                    results.Add(CaptureUtility.Render(camera, 1200, 760, path));
                    camera.transform.SetPositionAndRotation(position, rotation);
                }
                finally
                {
                    foreach (var go in temp)
                        Object.DestroyImmediate(go);
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }
            return string.Join(", ", results) + " | " + log;
        }

        [MenuItem("Pocket Wilds/Capture/Social and Points of Interest", priority = 523)]
        public static void CaptureSocialMenu() => Debug.Log("[WorldCapture] " + CaptureSocial("T25"));

        /// <summary>
        /// T25 reference: two greeting pairs nose to nose (mid Social clip, root gap from the behavior data) and a
        /// squirrel inspecting the big rock from its stand-off point, Medium lighting, gameplay and close views.
        /// </summary>
        public static string CaptureSocial(string prefix)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var temp = new List<GameObject>();
            var results = new List<string>();
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var chunk = SceneChunk();
                    var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var camera = Camera.main;
                    chunk.Bind(palettes);
                    entry.Lighting.Apply(Object.FindAnyObjectByType<Light>(), camera, QualityTier.Medium);
                    chunk.ApplyQuality(QualityTier.Medium);
                    var resolver = new AnimalVisualResolver(registry, palettes);
                    var egg = chunk.EggSpawn.position;

                    float Gap(string a, string b)
                    {
                        catalog.TryGet(registry.Species.Get(ContentId.FromSource("species." + a)).VisualKey, out var ea);
                        catalog.TryGet(registry.Species.Get(ContentId.FromSource("species." + b)).VisualKey, out var eb);
                        return 0.5f * (ea.behavior.socialGap + eb.behavior.socialGap);
                    }
                    void Pair(string a, string b, Vector3 center, float yaw, uint seed)
                    {
                        float half = 0.5f * Gap(a.Split('.')[0], b.Split('.')[0]);
                        var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                        var va = Spawn(registry, resolver, a, seed, seed * 31u + 3u, center - dir * half, yaw, 0f);
                        var vb = Spawn(registry, resolver, b, seed + 1, seed * 31u + 34u, center + dir * half, yaw + 180f, 0f);
                        foreach (var v in new[] { va, vb })
                        {
                            v.Animator.Play(AnimalStateHashes.Social, 0, 0.45f);
                            v.Animator.Update(0f);
                            temp.Add(v.gameObject);
                        }
                    }
                    Pair("rabbit.brown", "squirrel.red", egg + new Vector3(0.35f, 0f, 1.1f), 80f, 1);
                    Pair("hedgehog.brown", "rabbit.white", egg + new Vector3(-0.95f, 0f, 1.25f), 110f, 5);

                    // The squirrel stands off the nearest inspect point, facing it.
                    var points = chunk.GetWorldInterests();
                    InterestPoint rock = default;
                    float best = float.MaxValue;
                    foreach (var p in points)
                    {
                        float d = Vector3.Distance(p.position, egg);
                        if (p.kind == InterestKind.Inspect && d < best)
                        {
                            best = d;
                            rock = p;
                        }
                    }
                    var toRock = rock.position - egg;
                    toRock.y = 0f;
                    var stand = rock.position - toRock.normalized * rock.standOff;
                    var squirrel = Spawn(registry, resolver, "squirrel.autumn", 9, 300u, stand, Mathf.Atan2(toRock.x, toRock.z) * Mathf.Rad2Deg, 0f);
                    squirrel.Animator.Play(AnimalStateHashes.Inspect, 0, 0.4f);
                    squirrel.Animator.Update(0f);
                    temp.Add(squirrel.gameObject);

                    string world = $"{CaptureUtility.DefaultFolder}/{prefix}_world_social.png";
                    CaptureUtility.Render(camera, 540, 1170, world);
                    results.Add(CaptureUtility.Render(camera, 540, 1170, world));
                    var position = camera.transform.position;
                    var rotation = camera.transform.rotation;
                    camera.transform.position = egg + new Vector3(0.3f, 1.6f, -2.6f);
                    camera.transform.LookAt(egg + new Vector3(0.2f, 0.2f, 1.1f));
                    results.Add(CaptureUtility.Render(camera, 1200, 760, $"{CaptureUtility.DefaultFolder}/{prefix}_world_social_close.png"));
                    camera.transform.SetPositionAndRotation(position, rotation);
                }
                finally
                {
                    foreach (var go in temp)
                        Object.DestroyImmediate(go);
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }
            return string.Join(", ", results);
        }

        private static AnimalView Spawn(ContentRegistry registry, AnimalVisualResolver resolver, string speciesAndVariant, uint instanceId,
            uint seed, Vector3 position, float yaw, float animationTime, uint traitBits = 0u)
        {
            string speciesName = speciesAndVariant.Substring(0, speciesAndVariant.IndexOf('.'));
            var speciesId = ContentId.FromSource("species." + speciesName);
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            catalog.TryGet(registry.Species.Get(speciesId).VisualKey, out var visual);
            var animal = new AnimalInstance
            {
                InstanceId = instanceId, Seed = seed, Species = speciesId,
                Variant = ContentId.FromSource("variant." + speciesAndVariant), TraitBits = traitBits,
            };
            resolver.TryResolve(animal, position, yaw, out var snapshot);
            var view = Object.Instantiate(visual.prefab);
            view.Configure(snapshot);
            view.Animator.Rebind();
            view.Animator.Update(animationTime);
            return view;
        }

        internal static void Populate(ContentRegistry registry, PaletteTable palettes, WorldChunk chunk, int rabbits, List<GameObject> temp,
            string[] animals = null)
        {
            animals = animals ?? RabbitMix;
            var config = AssetDatabase.LoadAssetAtPath<HatchPresentationConfig>(EggPipeline.HatchConfigPath);
            var egg = Object.Instantiate(config.eggPrefab);
            var basic = registry.Eggs.Get(ContentId.FromSource("egg.grassland.basic"));
            palettes.TryGetRow(basic.Id, out int row);
            egg.Configure(chunk.EggSpawn.position, 180f, EggVisualPacking.FromDefinition(basic, row));
            temp.Add(egg.gameObject);

            var resolver = new AnimalVisualResolver(registry, palettes);
            Vector3[] offsets =
            {
                new Vector3(1.0f, 0f, 0.4f), new Vector3(-1.1f, 0f, 0.9f), new Vector3(0.3f, 0f, 1.9f),
                new Vector3(-0.5f, 0f, -1.3f), new Vector3(1.2f, 0f, -0.8f), new Vector3(-0.2f, 0f, 3.0f),
            };
            for (int i = 0; i < rabbits && i < offsets.Length; i++)
                temp.Add(Spawn(registry, resolver, animals[i % animals.Length], (uint)(i + 1), (uint)(i * 31 + 3),
                    chunk.EggSpawn.position + offsets[i], 150f + i * 23f, 0.4f * i).gameObject);
        }
    }
}
