using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Capture;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.World;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Albums
{
    /// <summary>
    /// T44A « Albums », the sticker art by capture (doc « 01 Albums et autocollants », section 1.11, L-SC-011): every
    /// <see cref="StickerCaptureDefinition"/> rendered from the game's own prefabs with the game shaders and palettes
    /// (animals from their real prefab and variant palette in a chosen pose, props and plants as the game binds them, the
    /// Prairie zones with their ground, plants and sky), the light profile of the biome with the main light from the upper
    /// left at 35 degrees, never painted. Cutouts (Portrait, Full, Detail) render twice on black and white in an empty
    /// studio for their alpha; cards (Scene) and medallions (Landscape) render in the world scene over the zone or the
    /// Garden set. 1024 x 1024 with MSAA 8x, a 512 x 512 PNG master in SourceArt/UI/Stickers/« page »/, the 256 x 256 game
    /// image with its 10 px white cut border computed from the alpha in Art/UI/Albums/Stickers/« page »/, packed into one atlas per
    /// page (Atlas_Page_« page », Addressables group Albums, albums.page.« page key »), and DEF_StickerArt listing every
    /// captured sticker. No collision rule: subjects stand on measured shapes (bodies of the catalog, capsules measured on
    /// the props' meshes, the zones' obstacles, the Garden beds); any overlap refuses the sticker. Nothing is saved in a
    /// scene; the scene open before is reopened from disk. Entry for the main session: <see cref="CaptureAvailable"/>.
    /// </summary>
    public static class StickerCapturePipeline
    {
        public const string MasterRoot = "SourceArt/UI/Stickers";
        public const string GameRoot = "Assets/_Game/Art/UI/Albums";
        /// <summary>The 256 game images, one folder per page (listed as rendered in SourceArt/Handmade_Sources.json).</summary>
        public const string SpriteRoot = GameRoot + "/Stickers";
        public const string ContactSheetPath = "Docs/Captures/Review/T44A_stickers_contact.png";
        public const string LightingFolder = "Assets/_Game/Art/Lighting/Grassland";
        public const string GardenSetPrefab = "Assets/_Game/Prefabs/Activities/PF_Activity_Garden_Set.prefab";
        public const int RenderSize = 1024;
        public const int Msaa = 8;
        public const int MasterSize = 512;
        public const int GameSize = 256;
        public const int Border = 10;
        public const int CardCorner = 24;
        public const float SunElevation = 35f;
        /// <summary>The sun travels this far right of the camera's heading: it comes from the image's upper left.</summary>
        public const float SunHeading = 45f;

        private const float Touch = 0.002f;
        private const float SearchStep = 0.2f;
        private const int SearchRings = 15;
        private const float KeepOutMargin = 0.04f;
        private const int MaxPointsPerMesh = 1500;

        public static string GamePath(StickerCaptureDefinition d) => $"{SpriteRoot}/{d.PageFolder}/{d.id}.png";

        public static string MasterPath(StickerCaptureDefinition d) => $"{MasterRoot}/{d.PageFolder}/{d.id}.png";

        public static string AtlasPath(string page) => $"{GameRoot}/Atlas_Page_{StickerCaptureDefinition.FolderOf(page)}.spriteatlasv2";

        // ---------------------------------------------------------------- menus and entries

        [MenuItem("Pocket Wilds/Albums/Capture page/Prairie 1 (page.grassland.1)", priority = 260)]
        private static void CaptureGrassland1() => Log(CapturePage(StickerCaptureDefinitions.PageGrassland1));

        [MenuItem("Pocket Wilds/Albums/Capture page/Prairie 2 (page.grassland.2)", priority = 261)]
        private static void CaptureGrassland2() => Log(CapturePage(StickerCaptureDefinitions.PageGrassland2));

        [MenuItem("Pocket Wilds/Albums/Capture page/Grooming (page.activity.001)", priority = 262)]
        private static void CaptureGrooming() => Log(CapturePage(StickerCaptureDefinitions.PageGrooming));

        [MenuItem("Pocket Wilds/Albums/Capture page/Hide and Seek (page.activity.002)", priority = 263)]
        private static void CaptureHideSeek() => Log(CapturePage(StickerCaptureDefinitions.PageHideSeek));

        [MenuItem("Pocket Wilds/Albums/Capture page/Nut Drop (page.activity.003)", priority = 264)]
        private static void CaptureNutDrop() => Log(CapturePage(StickerCaptureDefinitions.PageNutDrop));

        [MenuItem("Pocket Wilds/Albums/Capture page/Expeditions (page.activity.005)", priority = 265)]
        private static void CaptureExpeditions() => Log(CapturePage(StickerCaptureDefinitions.PageExpeditions));

        [MenuItem("Pocket Wilds/Albums/Capture page/Garden (page.activity.006)", priority = 266)]
        private static void CaptureGarden() => Log(CapturePage(StickerCaptureDefinitions.PageGarden));

        [MenuItem("Pocket Wilds/Albums/Capture all available stickers", priority = 280)]
        private static void CaptureAllMenu() => Log(CaptureAvailable());

        private static void Log(string report) => Debug.Log("[StickerCapture] " + report);

        /// <summary>Every capturable sticker (execute_code entry). Returns the report: what was done and every WARNING.</summary>
        public static string CaptureAvailable() => Capture(StickerCaptureDefinitions.All.ToList(), "all available stickers");

        /// <summary>The capturable stickers of one page (page.grassland.1, page.activity.003...).</summary>
        public static string CapturePage(string page) => Capture(StickerCaptureDefinitions.All.Where(d => d.page == page).ToList(), page);

        private static string Capture(List<StickerCaptureDefinition> definitions, string scope)
        {
            var watch = Stopwatch.StartNew();
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.isDirty && !string.IsNullOrEmpty(active.path) && active.path != WorldCapture.ScenePath)
                return "refused: save or discard the changes in " + active.path;
            string returnScene = !string.IsNullOrEmpty(active.path) ? active.path : WorldCapture.ScenePath;
            var report = new Report();
            definitions = Validate(definitions, report);
            var captured = new List<StickerCaptureDefinition>();
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            int level = QualitySettings.GetQualityLevel();
            bool fog = RenderSettings.fog;
            ShaderUtil.allowAsyncCompilation = false; // no placeholder shader in a capture
            try
            {
                var studio = definitions.Where(d => d.backdrop == StickerBackdrop.None).ToList();
                var world = definitions.Where(d => d.backdrop != StickerBackdrop.None).ToList();
                if (studio.Count > 0)
                    CaptureStudio(studio, report, captured);
                if (world.Count > 0)
                    CaptureWorld(world, report, captured);
            }
            catch (Exception exception)
            {
                report.Warn("capture stopped: " + exception.GetType().Name + ": " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                QualitySettings.SetQualityLevel(level, true);
                RenderSettings.fog = fog;
                EditorSceneManager.OpenScene(returnScene, OpenSceneMode.Single);
            }
            string assets = FinishAssets(captured, report);
            var text = new StringBuilder();
            text.Append($"T44A stickers ({scope}): {captured.Count} of {definitions.Count} captured in {watch.Elapsed.TotalSeconds:F1} s");
            if (captured.Count > 0)
                text.Append(" (").Append(string.Join(", ", captured.Select(d => d.id))).Append(')');
            text.Append(". ").Append(assets);
            foreach (var line in report.Refused)
                text.Append("\nWARNING: refused ").Append(line);
            foreach (var line in report.Warnings)
                text.Append("\nWARNING: ").Append(line);
            foreach (var line in report.Notes)
                text.Append("\nnote: ").Append(line);
            text.Append($"\nNot capturable today (species or prop not in the game yet): {StickerCaptureDefinitions.Unavailable.Length} stickers, "
                        + "listed in StickerCaptureDefinitions.Unavailable; their slots keep the soft silhouette.");
            return text.ToString();
        }

        private static List<StickerCaptureDefinition> Validate(List<StickerCaptureDefinition> definitions, Report report)
        {
            var ids = new HashSet<string>();
            var valid = new List<StickerCaptureDefinition>();
            foreach (var d in definitions)
            {
                if (d == null || string.IsNullOrEmpty(d.id) || !d.id.StartsWith("STK-", StringComparison.Ordinal) || string.IsNullOrEmpty(d.page)
                    || !d.page.StartsWith("page.", StringComparison.Ordinal))
                {
                    report.Warn("a definition without a valid id or page was skipped");
                    continue;
                }
                if (!ids.Add(d.id))
                {
                    report.Refuse(d, "duplicate id");
                    continue;
                }
                if (d.framing == StickerFraming.Landscape && d.backdrop == StickerBackdrop.None)
                {
                    report.Refuse(d, "a landscape needs a zone or the Garden set behind it");
                    continue;
                }
                valid.Add(d);
            }
            return valid;
        }

        // ---------------------------------------------------------------- sessions

        /// <summary>Cutouts: an empty scene, the profile's sun and ambient, two renders (black, white) for the alpha.</summary>
        private static void CaptureStudio(List<StickerCaptureDefinition> definitions, Report report, List<StickerCaptureDefinition> captured)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Loaded after the scene switch: it can unload assets loaded before it.
            var context = Context.Load(report);
            if (context == null)
                return;
            using (context)
            {
                QualitySettings.SetQualityLevel((int)QualityTier.High, true);
                var sun = new GameObject("StickerSun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                var template = new GameObject("StickerStudioCamera").AddComponent<Camera>();
                template.allowHDR = false;
                template.allowMSAA = true;
                var data = template.gameObject.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.renderShadows = true;
                var stage = new Stage { Studio = true, Anchor = Vector3.zero, Ground = (x, z) => 0f };
                bool warmed = false;
                foreach (var definition in definitions)
                {
                    if (!ApplyLight(definition, sun, template, report))
                        continue;
                    DynamicGI.UpdateEnvironment();
                    var placed = Spawn(context, definition, stage, report);
                    if (placed == null)
                        continue;
                    try
                    {
                        var pose = Frame(definition, placed, stage);
                        sun.transform.rotation = Quaternion.Euler(SunElevation, pose.yaw + SunHeading, 0f);
                        if (!warmed)
                        {
                            // The first render after the scene switch lacks the ambient and shadow setup: rendered, discarded.
                            Render(template, pose, Color.black, false, null);
                            warmed = true;
                        }
                        var black = Render(template, pose, Color.black, false, null);
                        var white = Render(template, pose, Color.white, false, null);
                        var full = StickerImage.FromPair(black, white, RenderSize);
                        if (definition.framing != StickerFraming.Portrait && full.TouchesEdge())
                            report.Warn(definition.id + ": the subject touches the frame's edge (cut by the inset)");
                        var crop = definition.framing == StickerFraming.Portrait
                            ? new RectInt(0, 0, RenderSize, RenderSize)
                            : full.CropAround((Border + 4f) / GameSize);
                        WriteImages(definition, full.Resample(crop, MasterSize));
                        captured.Add(definition);
                    }
                    finally
                    {
                        Destroy(placed);
                    }
                }
            }
        }

        /// <summary>
        /// Cards and medallions: the world scene with every Prairie zone spawned (their edges meet, as T35A's zone captures),
        /// palettes bound, the scatter and, for landscapes without subjects, the ambient life; the Garden set at its origin
        /// when a sticker needs it. Plants never grow in a placed subject (scatter keep-out rebuilt per sticker).
        /// </summary>
        private static void CaptureWorld(List<StickerCaptureDefinition> definitions, Report report, List<StickerCaptureDefinition> captured)
        {
            EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
            Shader.SetGlobalVector("_PW_ClosedZone", Vector4.zero);
            var context = Context.Load(report);
            if (context == null)
                return;
            GameObject garden = null;
            using (context)
            {
                try
                {
                    var start = WorldCapture.SceneChunk();
                    var zones = new Dictionary<string, WorldChunk> { ["start"] = start };
                    var chunks = new List<WorldChunk> { start };
                    foreach (var entry in WorldContentPipeline.GrasslandChunks)
                    {
                        if (entry.pinned)
                            continue;
                        var chunk = WorldContentPipeline.SpawnForTools(entry.address);
                        if (chunk == null)
                            continue;
                        chunks.Add(chunk);
                        string name = Path.GetFileNameWithoutExtension(entry.path);
                        zones[name.Substring(name.LastIndexOf('_') + 1).ToLowerInvariant()] = chunk;
                    }
                    var sun = Object.FindAnyObjectByType<Light>();
                    var camera = Camera.main;
                    if (sun == null || camera == null)
                    {
                        report.Warn("the world scene has no sun or main camera: cards and medallions skipped");
                        return;
                    }
                    QualitySettings.SetQualityLevel((int)QualityTier.High, true);
                    var quality = WorldQualityProfile.Default(QualityTier.High);
                    var scatter = new ScatterRenderer(context.Palettes, quality.scatterDensity);
                    scatter.SetDistanceFade(0f, 0f);
                    var lifeKit = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(GrasslandPipeline.LifeKitPath);
                    var life = lifeKit != null ? new AmbientLife(lifeKit, context.Palettes, quality.ambientLifeDensity) : null;
                    int missing = 0;
                    foreach (var chunk in chunks)
                    {
                        missing += chunk.Bind(context.Palettes);
                        chunk.ApplyQuality(QualityTier.High);
                        scatter.Add(chunk);
                        life?.Add(chunk);
                    }
                    if (missing > 0)
                        report.Warn($"{missing} zone renderers reference an unknown palette");
                    for (int i = 0; i < 120; i++)
                        life?.Tick(1f / 30f, i / 30f);
                    var obstacles = chunks.SelectMany(c => c.GetWorldObstacles()).ToArray();
                    Obstacle[] gardenObstacles = null;
                    Transform[] plots = null;
                    bool warmed = false;

                    foreach (var definition in definitions)
                    {
                        var stage = new Stage();
                        if (definition.backdrop == StickerBackdrop.Zone)
                        {
                            if (string.IsNullOrEmpty(definition.zone) || !zones.TryGetValue(definition.zone, out var chunk))
                            {
                                report.Refuse(definition, $"zone « {definition.zone} » not in this build");
                                continue;
                            }
                            var surface = chunk.Ground;
                            stage.Anchor = chunk.transform.position + new Vector3(definition.anchor.x, 0f, definition.anchor.z);
                            stage.Ground = (x, z) => surface.HeightAt(x, z);
                            stage.Blockers = obstacles;
                        }
                        else
                        {
                            if (garden == null)
                            {
                                garden = SpawnGarden(context, report, out gardenObstacles, out plots);
                                if (garden == null)
                                {
                                    report.Refuse(definition, "Garden set prefab missing");
                                    continue;
                                }
                            }
                            float floor = GardenLayout.SetOrigin.y;
                            stage.Anchor = GardenLayout.SetOrigin + new Vector3(definition.anchor.x, 0f, definition.anchor.z);
                            stage.Ground = (x, z) => floor;
                            stage.Blockers = gardenObstacles;
                            stage.Plots = plots;
                        }
                        if (!ApplyLight(definition, sun, camera, report))
                            continue;
                        var placed = Spawn(context, definition, stage, report);
                        if (placed == null)
                            continue;
                        try
                        {
                            scatter.SetKeepOut(KeepOut(placed));
                            foreach (var chunk in chunks)
                                scatter.Rebuild(chunk);
                            var pose = Frame(definition, placed, stage);
                            sun.transform.rotation = Quaternion.Euler(SunElevation, pose.yaw + SunHeading, 0f);
                            if (definition.backdrop == StickerBackdrop.Zone && zones.TryGetValue(definition.zone, out var shown))
                            {
                                string edge = NearGroundOutside(shown, pose);
                                if (edge != null)
                                    report.Warn(definition.id + ": " + edge);
                            }
                            bool drawLife = definition.life && definition.subjects.Count == 0 && life != null;
                            if (!warmed)
                            {
                                // The first render after the scene opens still lacks part of its setup: rendered, discarded.
                                Render(camera, pose, null, true, cam => scatter.Render(cam));
                                warmed = true;
                            }
                            var pixels = Render(camera, pose, null, true, cam =>
                            {
                                scatter.Render(cam);
                                if (drawLife)
                                    life.Render(cam, 4f);
                            });
                            WriteImages(definition, StickerImage.FromOpaque(pixels, RenderSize).Resample(MasterSize));
                            captured.Add(definition);
                        }
                        finally
                        {
                            Destroy(placed);
                        }
                    }
                    scatter.SetKeepOut(null);
                }
                finally
                {
                    if (garden != null)
                        Object.DestroyImmediate(garden);
                }
            }
        }

        /// <summary>The Garden set at its origin: palettes bound, covers off, its own sun off (the sticker's profile lights it).</summary>
        private static GameObject SpawnGarden(Context context, Report report, out Obstacle[] obstacles, out Transform[] plots)
        {
            obstacles = Array.Empty<Obstacle>();
            plots = null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GardenSetPrefab);
            if (prefab == null)
                return null;
            var set = Object.Instantiate(prefab, GardenLayout.SetOrigin, Quaternion.identity);
            set.hideFlags = HideFlags.DontSave;
            var chunk = set.GetComponent<WorldChunk>();
            if (chunk != null && chunk.Bind(context.Palettes) > 0)
                report.Warn("Garden set: some renderers reference an unknown palette");
            var view = set.GetComponent<GardenSetView>();
            var list = new List<Obstacle>();
            if (view != null)
            {
                foreach (var cover in view.lockedCovers)
                    if (cover != null)
                        cover.SetActive(false);
                if (view.sun != null)
                    view.sun.enabled = false;
                plots = view.plantAnchors;
                // The beds from their measured sizes: a capsule along the longer side, as tall as the planks.
                for (int i = 0; i < view.beds.Length && i < view.bedSizes.Length; i++)
                {
                    var bed = view.beds[i];
                    if (bed == null)
                        continue;
                    var half = view.bedSizes[i] * 0.5f;
                    var along = half.x >= half.y ? bed.right : bed.forward;
                    float radius = Mathf.Min(half.x, half.y);
                    float length = Mathf.Max(half.x, half.y) - radius;
                    var center = CollisionMath.Flat(bed.position);
                    var axis = new Vector2(along.x, along.z).normalized;
                    list.Add(new Obstacle(center - axis * length, center + axis * length, radius, bed.position.y, -0.05f, 0.35f, ObstacleKind.Solid));
                }
            }
            // Every other measured prop of the set (can, basket, kit decor); floors, paths, the fence ring and the backdrop
            // are not footprints.
            foreach (var renderer in set.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                string name = renderer.name;
                if (name.Contains("Ground") || name.Contains("Path") || name.Contains("Fence") || name.Contains("Backdrop") || name.Contains("Bed"))
                    continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;
                var points = WorldPoints(context, filter.sharedMesh, renderer.transform);
                if (points.Count == 0)
                    continue;
                FlatCapsule(points, renderer.transform.eulerAngles.y, out var a, out var b, out float r);
                float minY = points.Min(p => p.y), maxY = points.Max(p => p.y);
                if ((b - a).magnitude * 0.5f + r > 1.6f || maxY - minY < 0.02f)
                    continue;
                list.Add(new Obstacle(a, b, r, minY, 0f, maxY - minY, ObstacleKind.Solid));
            }
            obstacles = list.ToArray();
            return set;
        }

        private static bool ApplyLight(StickerCaptureDefinition definition, Light sun, Camera camera, Report report)
        {
            string path = $"{LightingFolder}/LP_Grassland_{definition.light}.asset";
            var lighting = AssetDatabase.LoadAssetAtPath<LightingProfile>(path);
            if (lighting == null)
            {
                report.Refuse(definition, "light profile missing: " + path);
                return false;
            }
            lighting.Apply(sun, camera, QualityTier.High);
            // A picture, not a game frame: no distance fog over the subjects.
            RenderSettings.fog = false;
            return true;
        }

        // ---------------------------------------------------------------- subjects

        private sealed class Report
        {
            public readonly List<string> Refused = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public readonly List<string> Notes = new List<string>();

            public void Refuse(StickerCaptureDefinition d, string why) => Refused.Add(d.id + ": " + why);

            public void Warn(string text) => Warnings.Add(text);
        }

        private sealed class Context : IDisposable
        {
            public ContentRegistry Registry;
            public PaletteTable Palettes;
            public AnimalVisualResolver Resolver;
            public AnimalVisualCatalog Visuals;
            public readonly Dictionary<Mesh, Vector3[]> Vertices = new Dictionary<Mesh, Vector3[]>();

            public static Context Load(Report report)
            {
                var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
                var visuals = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
                if (database == null || visuals == null)
                {
                    report.Warn("no content database or animal visual catalog: nothing captured");
                    return null;
                }
                var context = new Context { Registry = database.CreateBuilder().Build(), Visuals = visuals };
                context.Palettes = new PaletteTable(context.Registry);
                context.Resolver = new AnimalVisualResolver(context.Registry, context.Palettes);
                return context;
            }

            public void Dispose() => Palettes?.Dispose();
        }

        private sealed class Stage
        {
            public bool Studio;
            public Vector3 Anchor;
            public Func<float, float, float> Ground;
            public Obstacle[] Blockers = Array.Empty<Obstacle>();
            public Transform[] Plots;
        }

        private sealed class Placed
        {
            public StickerSubject Subject;
            public int SubjectIndex;
            public string Name;
            public GameObject Root;
            public AnimalView View;
            public BodyShape Body;
            public Placed Holder;
            public float Yaw;
            public bool Droppable;
            public bool Dropped;
            public Vector3 Jitter;
            public Vector3 JitterAngles;
            public readonly List<(Mesh mesh, Transform transform)> Meshes = new List<(Mesh mesh, Transform transform)>();
            public readonly List<Vector3> Points = new List<Vector3>();
            public Vector2 A;
            public Vector2 B;
            public float Radius;
            public float Bottom;
            public float Top;

            public bool IsAnimal => View != null;

            public bool Attached => Subject.mount == SubjectMount.RestOn || Subject.mount == SubjectMount.Inside || Subject.mount == SubjectMount.Socket;

            public Vector2 Center => (A + B) * 0.5f;

            public float Reach => (B - A).magnitude * 0.5f + Radius;
        }

        /// <summary>
        /// Instantiates the subjects, places them (the nearest clear spot around the anchor in a zone), checks the no-collision
        /// rule. Returns null when refused (everything destroyed, the reason in the report).
        /// </summary>
        private static List<Placed> Spawn(Context context, StickerCaptureDefinition definition, Stage stage, Report report)
        {
            var placed = new List<Placed>();
            var firstOf = new Dictionary<int, Placed>();
            for (int s = 0; s < definition.subjects.Count; s++)
            {
                var subject = definition.subjects[s];
                bool needsHolder = subject.mount == SubjectMount.RestOn || subject.mount == SubjectMount.Inside || subject.mount == SubjectMount.Socket;
                Placed holder = null;
                if (needsHolder || subject.holder >= 0)
                {
                    if (subject.holder < 0 || subject.holder >= s || !firstOf.TryGetValue(subject.holder, out holder))
                        return Refuse(placed, definition, report, $"subject {s} ({subject.Name}) needs an earlier holder");
                    if (subject.mount == SubjectMount.Socket && !holder.IsAnimal)
                        return Refuse(placed, definition, report, $"subject {s} ({subject.Name}): a socket holder must be an animal");
                }
                if (subject.mount == SubjectMount.Plot && (stage.Plots == null || subject.plot < 0 || subject.plot >= stage.Plots.Length || stage.Plots[subject.plot] == null))
                    return Refuse(placed, definition, report, $"subject {s} ({subject.Name}): plot {subject.plot} unavailable (Garden set only)");
                int copies = Mathf.Max(1, subject.count);
                var random = new System.Random(StableHash(definition.id) * 31 + s);
                for (int c = 0; c < copies; c++)
                {
                    string why;
                    var p = Create(context, definition, subject, s, report, out why);
                    if (p == null)
                        return Refuse(placed, definition, report, why);
                    p.Holder = holder;
                    p.Droppable = copies > 1 || subject.mount == SubjectMount.Inside;
                    if (copies > 1)
                    {
                        p.Jitter = new Vector3(Signed(random) * subject.spread.x, Signed(random) * subject.spread.y, Signed(random) * subject.spread.z);
                        p.JitterAngles = new Vector3(Signed(random) * 60f, (float)random.NextDouble() * 360f, Signed(random) * 60f);
                    }
                    placed.Add(p);
                    if (c == 0)
                        firstOf[s] = p;
                }
            }
            if (placed.Count == 0)
            {
                // A landscape aimed at its anchor needs no subject; anything else frames its subjects.
                return definition.framing == StickerFraming.Landscape && definition.distance > 0f
                    ? placed
                    : Refuse(placed, definition, report, "no subject to frame");
            }

            // The anchor: as given in the studio, else the nearest spot where nothing stands in the world's obstacles.
            bool found = false;
            Vector3 anchor = stage.Anchor;
            string blocked = null;
            foreach (var candidate in Candidates(stage.Anchor, stage.Studio ? 0 : SearchRings))
            {
                PlaceAll(definition, stage, placed, candidate, false);
                blocked = FirstBlocked(placed, stage, false);
                if (blocked == null)
                {
                    anchor = candidate;
                    found = true;
                    break;
                }
            }
            if (!found)
                return Refuse(placed, definition, report, $"no clear spot within {SearchStep * SearchRings:F1} m of the anchor ({blocked})");
            PlaceAll(definition, stage, placed, anchor, true);
            if (!stage.Studio && (anchor - stage.Anchor).sqrMagnitude > 0.0001f)
                report.Notes.Add($"{definition.id}: placed {(anchor - stage.Anchor).magnitude:F2} m from its anchor (clear spot)");

            string overlap = FirstOverlap(placed);
            if (overlap != null)
                return Refuse(placed, definition, report, "overlap: " + overlap);

            // Cloud members and pieces inside a holder that would touch anything are left out, never pushed in.
            int dropped = 0, droppable = 0, droppedInside = 0;
            foreach (var p in placed)
            {
                if (!p.Droppable)
                    continue;
                droppable++;
                bool touches = FirstOverlapWith(p, placed) != null || (p.Subject.mount == SubjectMount.Air && Blocker(p, stage) != null);
                if (!touches)
                    continue;
                p.Dropped = true;
                p.Root.SetActive(false);
                dropped++;
                if (p.Subject.mount == SubjectMount.Inside)
                    droppedInside++;
            }
            if (droppedInside > 0)
                report.Warn($"{definition.id}: {droppedInside} pieces inside a holder left out (they would touch another piece)");
            if (dropped > droppedInside)
                report.Notes.Add($"{definition.id}: {dropped - droppedInside} of {droppable - droppedInside} cloud pieces left out (they would touch something)");
            return placed;
        }

        private static List<Placed> Refuse(List<Placed> placed, StickerCaptureDefinition definition, Report report, string why)
        {
            report.Refuse(definition, why);
            Destroy(placed);
            return null;
        }

        private static void Destroy(List<Placed> placed)
        {
            foreach (var p in placed)
                if (p.Root != null)
                    Object.DestroyImmediate(p.Root);
            placed.Clear();
        }

        private static Placed Create(Context context, StickerCaptureDefinition definition, StickerSubject subject, int index, Report report, out string why)
        {
            why = null;
            var p = new Placed { Subject = subject, SubjectIndex = index, Name = subject.Name };
            switch (subject.kind)
            {
                case SubjectKind.Animal:
                {
                    string variant = subject.source ?? "";
                    int dot = variant.IndexOf('.');
                    if (dot <= 0)
                    {
                        why = $"animal « {variant} »: expected species.variant";
                        return null;
                    }
                    var speciesId = ContentId.FromSource("species." + variant.Substring(0, dot));
                    string visualKey;
                    try
                    {
                        visualKey = context.Registry.Species.Get(speciesId).VisualKey;
                    }
                    catch (Exception)
                    {
                        why = $"species « {variant.Substring(0, dot)} » not in this build";
                        return null;
                    }
                    if (!context.Visuals.TryGet(visualKey, out var entry) || entry.prefab == null)
                    {
                        why = $"no visual for « {variant} »";
                        return null;
                    }
                    var animal = new AnimalInstance
                    {
                        InstanceId = (uint)(index + 1),
                        Seed = 3u + (uint)index * 7u,
                        Species = speciesId,
                        Variant = ContentId.FromSource("variant." + variant),
                    };
                    if (!context.Resolver.TryResolve(animal, Vector3.zero, 0f, out var snapshot))
                    {
                        why = $"variant « {variant} » not resolved";
                        return null;
                    }
                    var view = Object.Instantiate(entry.prefab);
                    view.gameObject.hideFlags = HideFlags.DontSave;
                    view.Configure(snapshot);
                    if (view.LodGroup != null)
                        view.LodGroup.ForceLOD(0);
                    if (definition.backdrop == StickerBackdrop.None)
                    {
                        // The cutout floats on transparency: no ground blob.
                        foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
                            if (renderer.name.Contains("BlobShadow"))
                                renderer.enabled = false;
                    }
                    Pose(view, subject, definition, report);
                    p.Root = view.gameObject;
                    p.View = view;
                    p.Body = entry.behavior != null ? entry.behavior.body : new BodyShape(0.15f, -0.15f, 0.1f, 0.25f);
                    return p;
                }
                case SubjectKind.Prefab:
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(subject.source);
                    if (prefab == null)
                    {
                        why = "prefab missing: " + subject.source;
                        return null;
                    }
                    var go = Object.Instantiate(prefab);
                    go.hideFlags = HideFlags.DontSave;
                    go.name = prefab.name;
                    foreach (var lod in go.GetComponentsInChildren<LODGroup>(true))
                        lod.ForceLOD(0);
                    int missing = BindPrefab(context, go, subject, prefab.name);
                    if (missing > 0)
                        report.Warn($"{definition.id}: {missing} renderers of {prefab.name} without a known palette");
                    p.Root = go;
                    CollectMeshes(p);
                    return p;
                }
                default:
                {
                    if (!TryMesh(subject.source, subject.meshName, out var mesh, out var matrix))
                    {
                        why = $"mesh « {subject.meshName} » not found in {subject.source}";
                        return null;
                    }
                    var material = AssetDatabase.LoadAssetAtPath<Material>(subject.material);
                    if (material == null)
                    {
                        why = "material missing: " + subject.material;
                        return null;
                    }
                    var go = new GameObject(subject.Name) { hideFlags = HideFlags.DontSave };
                    var child = new GameObject(mesh.name) { hideFlags = HideFlags.DontSave };
                    child.transform.SetParent(go.transform, false);
                    child.transform.localPosition = matrix.GetColumn(3);
                    child.transform.localRotation = matrix.rotation;
                    child.transform.localScale = matrix.lossyScale;
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = child.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    if (!string.IsNullOrEmpty(subject.palette) && context.Palettes.TryGetRow(ContentId.FromSource(subject.palette), out int row))
                        renderer.SetShaderUserValue(EnvironmentVisualPacking.Pack(row, WorldChunk.NeutralBrightness));
                    else
                        report.Warn($"{definition.id}: palette « {subject.palette} » unknown for {subject.Name}");
                    p.Root = go;
                    CollectMeshes(p);
                    return p;
                }
            }
        }

        /// <summary>Highest detail, the chosen state at its normalized time (Idle when the state does not exist).</summary>
        private static void Pose(AnimalView view, StickerSubject subject, StickerCaptureDefinition definition, Report report)
        {
            var animator = view.Animator;
            if (animator == null)
                return;
            animator.Rebind();
            string state = string.IsNullOrEmpty(subject.pose) ? "Idle" : subject.pose;
            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash))
            {
                report.Warn($"{definition.id}: no state « {state} » for {subject.source}, Idle used");
                hash = AnimalView.IdleState;
            }
            animator.Play(hash, 0, subject.poseTime);
            animator.Update(0f);
            animator.Update(0f);
        }

        /// <summary>Palettes as the game binds them: a chunk's bindings, a set view's Bind, else the Prairie prop's row.</summary>
        private static int BindPrefab(Context context, GameObject go, StickerSubject subject, string prefabName)
        {
            int missing = 0;
            bool bound = false;
            foreach (var chunk in go.GetComponentsInChildren<WorldChunk>(true))
            {
                missing += chunk.Bind(context.Palettes);
                bound = true;
            }
            if (!bound)
            {
                foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    var bind = behaviour != null ? behaviour.GetType().GetMethod("Bind", new[] { typeof(PaletteTable) }) : null;
                    if (bind == null || bind.ReturnType != typeof(int))
                        continue;
                    missing += (int)bind.Invoke(behaviour, new object[] { context.Palettes });
                    bound = true;
                }
            }
            if (bound)
                return missing;
            string palette = !string.IsNullOrEmpty(subject.palette) ? subject.palette : GrasslandPalette(prefabName);
            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            if (string.IsNullOrEmpty(palette) || !context.Palettes.TryGetRow(ContentId.FromSource(palette), out int row))
                return renderers.Length;
            uint packed = EnvironmentVisualPacking.Pack(row, WorldChunk.NeutralBrightness);
            foreach (var renderer in renderers)
                renderer.SetShaderUserValue(packed);
            return 0;
        }

        private static MethodInfo _paletteFor;

        /// <summary>The Prairie prop's palette row as the chunk pipeline picks it (GrasslandPipeline.PaletteFor).</summary>
        private static string GrasslandPalette(string prefabName)
        {
            if (!prefabName.StartsWith("PF_GL_", StringComparison.Ordinal))
                return null;
            if (_paletteFor == null)
                _paletteFor = typeof(GrasslandPipeline).GetMethod("PaletteFor", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            return _paletteFor != null ? _paletteFor.Invoke(null, new object[] { prefabName.Substring(3) }) as string : null;
        }

        /// <summary>A mesh of a model (with its node's transform) or of a folder of baked meshes, by name; the first mesh when unnamed.</summary>
        private static bool TryMesh(string source, string name, out Mesh mesh, out Matrix4x4 matrix)
        {
            mesh = null;
            matrix = Matrix4x4.identity;
            if (string.IsNullOrEmpty(source))
                return false;
            if (AssetDatabase.IsValidFolder(source))
            {
                if (string.IsNullOrEmpty(name))
                    return false;
                foreach (var guid in AssetDatabase.FindAssets(name + " t:Mesh", new[] { source }))
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                        if (asset is Mesh m && m.name == name)
                        {
                            mesh = m;
                            return true;
                        }
                return false;
            }
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if (root != null)
            {
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || (!string.IsNullOrEmpty(name) && filter.name != name && filter.sharedMesh.name != name))
                        continue;
                    mesh = filter.sharedMesh;
                    matrix = filter.transform.localToWorldMatrix;
                    return true;
                }
            }
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(source))
                if (asset is Mesh m && (string.IsNullOrEmpty(name) || m.name == name))
                {
                    mesh = m;
                    return true;
                }
            return false;
        }

        private static void CollectMeshes(Placed p)
        {
            foreach (var filter in p.Root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<Renderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled || !filter.gameObject.activeInHierarchy
                    || filter.name.Contains("LOD1") || filter.name.Contains("LOD2"))
                    continue;
                p.Meshes.Add((filter.sharedMesh, filter.transform));
            }
        }

        // ---------------------------------------------------------------- placement and the no-collision rule

        private static IEnumerable<Vector3> Candidates(Vector3 preferred, int rings)
        {
            yield return preferred;
            for (int k = 1; k <= rings; k++)
            {
                int count = 8 * k;
                float radius = k * SearchStep;
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    yield return preferred + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                }
            }
        }

        /// <summary>Places every subject in order (holders come first) for an anchor, and measures its shape.</summary>
        private static void PlaceAll(StickerCaptureDefinition definition, Stage stage, List<Placed> placed, Vector3 anchor, bool withDroppable)
        {
            var frame = Quaternion.Euler(0f, definition.anchorYaw, 0f);
            foreach (var p in placed)
            {
                if (p.Droppable && !withDroppable)
                    continue;
                var s = p.Subject;
                var offset = frame * new Vector3(s.position.x + p.Jitter.x, 0f, s.position.z + p.Jitter.z);
                float yaw = definition.anchorYaw + s.yaw + p.JitterAngles.y;
                Vector3 position;
                var holder = p.Holder;
                switch (s.mount)
                {
                    case SubjectMount.Air:
                        position = (holder != null ? holder.Root.transform.position : anchor) + offset;
                        position.y = stage.Ground(position.x, position.z) + s.position.y + p.Jitter.y;
                        break;
                    case SubjectMount.Plot:
                        position = stage.Plots[s.plot].position + offset;
                        position.y = stage.Plots[s.plot].position.y + s.position.y;
                        break;
                    case SubjectMount.Socket:
                    {
                        var socket = SocketOf(holder.View, s.socket);
                        var turn = Quaternion.Euler(0f, holder.Yaw, 0f);
                        var at = socket != null ? socket.position : new Vector3(holder.Root.transform.position.x, holder.Top, holder.Root.transform.position.z);
                        position = at + Vector3.up * s.position.y + turn * new Vector3(s.position.x, 0f, s.position.z);
                        yaw = holder.Yaw + s.yaw;
                        break;
                    }
                    case SubjectMount.Inside:
                    {
                        var center = holder.Center;
                        float reach = holder.Reach;
                        var local = frame * new Vector3(s.position.x * reach, 0f, s.position.z * reach);
                        position = new Vector3(center.x + local.x, holder.Bottom, center.y + local.z);
                        break;
                    }
                    case SubjectMount.RestOn:
                        position = anchor + offset;
                        position.y = holder.Top;
                        break;
                    default:
                        position = anchor + offset;
                        position.y = stage.Ground(position.x, position.z) + s.position.y;
                        break;
                }
                var rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(s.tilt.x + p.JitterAngles.x, 0f, s.tilt.z + p.JitterAngles.z);
                Move(p, position, rotation, yaw);
                if (s.mount == SubjectMount.RestOn)
                {
                    float top = HolderTopUnder(holder, p);
                    Shift(p, top + s.position.y - p.Bottom);
                }
                else if (s.mount == SubjectMount.Inside)
                {
                    float target = holder.Bottom + s.position.y * (holder.Top - holder.Bottom);
                    Shift(p, target - p.Bottom);
                }
            }
        }

        private static void Move(Placed p, Vector3 position, Quaternion rotation, float yaw)
        {
            p.Yaw = yaw;
            float scale = Mathf.Max(0.01f, p.Subject.scale);
            if (p.IsAnimal)
            {
                p.View.SetScale(scale);
                p.View.SetPose(position, yaw);
                p.Body.Segment(position, yaw, scale, out p.A, out p.B);
                p.Radius = p.Body.halfWidth * scale;
                p.Bottom = position.y;
                p.Top = position.y + p.Body.height * scale;
                return;
            }
            p.Root.transform.SetPositionAndRotation(position, rotation);
            p.Root.transform.localScale = Vector3.one * scale;
            Measure(p);
        }

        private static void Shift(Placed p, float dy)
        {
            if (Mathf.Abs(dy) < 1e-5f)
                return;
            p.Root.transform.position += Vector3.up * dy;
            p.Bottom += dy;
            p.Top += dy;
            for (int i = 0; i < p.Points.Count; i++)
                p.Points[i] += Vector3.up * dy;
        }

        /// <summary>A prop's footprint: a capsule along its own yaw holding every vertex, and its height band.</summary>
        private static void Measure(Placed p)
        {
            p.Points.Clear();
            foreach (var (mesh, transform) in p.Meshes)
                p.Points.AddRange(WorldPoints(null, mesh, transform));
            if (p.Points.Count == 0)
            {
                var at = CollisionMath.Flat(p.Root.transform.position);
                p.A = p.B = at;
                p.Radius = 0.01f;
                p.Bottom = p.Root.transform.position.y;
                p.Top = p.Bottom + 0.01f;
                return;
            }
            FlatCapsule(p.Points, p.Yaw, out p.A, out p.B, out p.Radius);
            float min = float.MaxValue, max = float.MinValue;
            foreach (var point in p.Points)
            {
                min = Mathf.Min(min, point.y);
                max = Mathf.Max(max, point.y);
            }
            p.Bottom = min;
            p.Top = max;
        }

        private static readonly Dictionary<Mesh, Vector3[]> SharedVertices = new Dictionary<Mesh, Vector3[]>();

        private static List<Vector3> WorldPoints(Context context, Mesh mesh, Transform transform)
        {
            var cache = context != null ? context.Vertices : SharedVertices;
            if (!cache.TryGetValue(mesh, out var local))
            {
                var all = mesh.vertices;
                int stride = Mathf.Max(1, all.Length / MaxPointsPerMesh);
                local = new Vector3[(all.Length + stride - 1) / stride];
                for (int i = 0, j = 0; i < all.Length && j < local.Length; i += stride, j++)
                    local[j] = all[i];
                cache[mesh] = local;
            }
            var matrix = transform.localToWorldMatrix;
            var points = new List<Vector3>(local.Length);
            foreach (var v in local)
                points.Add(matrix.MultiplyPoint3x4(v));
            return points;
        }

        private static void FlatCapsule(List<Vector3> points, float yaw, out Vector2 a, out Vector2 b, out float radius)
        {
            var axis = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            var along = new Vector2(axis.x, axis.z);
            var across = new Vector2(-axis.z, axis.x);
            var center = Vector2.zero;
            foreach (var p in points)
                center += new Vector2(p.x, p.z);
            center /= points.Count;
            float minA = float.MaxValue, maxA = float.MinValue;
            radius = 0f;
            foreach (var p in points)
            {
                var d = new Vector2(p.x, p.z) - center;
                float t = Vector2.Dot(d, along);
                minA = Mathf.Min(minA, t);
                maxA = Mathf.Max(maxA, t);
                radius = Mathf.Max(radius, Mathf.Abs(Vector2.Dot(d, across)));
            }
            float mid = (minA + maxA) * 0.5f;
            float half = Mathf.Max(0f, (maxA - minA) * 0.5f - radius);
            a = center + along * (mid - half);
            b = center + along * (mid + half);
            foreach (var p in points)
                radius = Mathf.Max(radius, Mathf.Sqrt(CollisionMath.PointSegmentDistanceSq(new Vector2(p.x, p.z), a, b)));
            radius = Mathf.Max(radius, 0.005f);
        }

        private static float HolderTopUnder(Placed holder, Placed p)
        {
            if (holder.IsAnimal || holder.Points.Count == 0)
                return holder.Top;
            var center = p.Center;
            float reach = Mathf.Max(0.02f, p.Reach);
            float top = float.MinValue;
            foreach (var point in holder.Points)
                if ((new Vector2(point.x, point.z) - center).sqrMagnitude <= reach * reach)
                    top = Mathf.Max(top, point.y);
            return top > float.MinValue ? top : holder.Top;
        }

        private static Transform SocketOf(AnimalView view, AnimalSocket socket) => socket switch
        {
            AnimalSocket.HeadTop => view.SocketHeadTop,
            AnimalSocket.Neck => view.SocketNeck,
            AnimalSocket.Back => view.SocketBack,
            _ => view.SocketTailBase,
        };

        /// <summary>One rests on, sits inside or is worn by the other (directly or through a chain): they may touch.</summary>
        private static bool Related(Placed a, Placed b)
        {
            for (var p = a; p != null && p.Attached; p = p.Holder)
                if (p.Holder == b)
                    return true;
            for (var p = b; p != null && p.Attached; p = p.Holder)
                if (p.Holder == a)
                    return true;
            return false;
        }

        private static bool Overlaps(Placed a, Placed b, out float gap)
        {
            gap = CollisionMath.Gap(a.A, a.B, a.Radius, b.A, b.B, b.Radius);
            float band = Mathf.Min(a.Top, b.Top) - Mathf.Max(a.Bottom, b.Bottom);
            return gap < -Touch && band > Touch;
        }

        private static string FirstOverlap(List<Placed> placed)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i].Droppable)
                    continue;
                for (int j = i + 1; j < placed.Count; j++)
                {
                    if (placed[j].Droppable || Related(placed[i], placed[j]))
                        continue;
                    if (Overlaps(placed[i], placed[j], out float gap))
                        return $"{placed[i].Name} and {placed[j].Name} by {-gap * 100f:F1} cm";
                }
            }
            return null;
        }

        private static string FirstOverlapWith(Placed piece, List<Placed> placed)
        {
            foreach (var other in placed)
            {
                if (other == piece || other.Dropped || Related(piece, other))
                    continue;
                // Pieces are kept in order: a later piece is checked when its own turn comes.
                if (other.Droppable && placed.IndexOf(other) > placed.IndexOf(piece))
                    continue;
                if (Overlaps(piece, other, out float gap))
                    return $"{piece.Name} and {other.Name} by {-gap * 100f:F1} cm";
            }
            return null;
        }

        /// <summary>The first subject standing in a world obstacle (ground subjects as walkers, air subjects against solids).</summary>
        private static string FirstBlocked(List<Placed> placed, Stage stage, bool withDroppable)
        {
            foreach (var p in placed)
            {
                if (p.Droppable && !withDroppable)
                    continue;
                string blocker = Blocker(p, stage);
                if (blocker != null)
                    return p.Name + " in " + blocker;
            }
            return null;
        }

        private static string Blocker(Placed p, Stage stage)
        {
            var mount = p.Subject.mount;
            if (stage.Blockers == null || (mount != SubjectMount.Ground && mount != SubjectMount.Air))
                return null;
            for (int i = 0; i < stage.Blockers.Length; i++)
            {
                ref var o = ref stage.Blockers[i];
                if (mount == SubjectMount.Ground)
                {
                    if (!o.BlocksWalker(p.Bottom, Mathf.Max(0.01f, p.Top - p.Bottom)))
                        continue;
                }
                else if (!o.IsSolid || o.WorldBottom >= p.Top || o.WorldTop <= p.Bottom)
                {
                    continue;
                }
                if (CollisionMath.Gap(p.A, p.B, p.Radius, in o) < -Touch)
                    return $"an obstacle at ({o.Center.x:F1}, {o.Center.y:F1})";
            }
            return null;
        }

        /// <summary>Plants never grow inside a subject standing on the ground.</summary>
        private static Obstacle[] KeepOut(List<Placed> placed)
        {
            var list = new List<Obstacle>();
            foreach (var p in placed)
                if (!p.Dropped && (p.Subject.mount == SubjectMount.Ground || p.Subject.mount == SubjectMount.Plot))
                    list.Add(new Obstacle(p.A, p.B, p.Radius + KeepOutMargin, p.Bottom, -1f, 10f, ObstacleKind.Solid));
            return list.ToArray();
        }

        private static int StableHash(string text)
        {
            unchecked
            {
                int hash = (int)2166136261;
                foreach (char c in text)
                    hash = (hash ^ c) * 16777619;
                return hash & 0x7FFFFFFF;
            }
        }

        private static float Signed(System.Random random) => (float)random.NextDouble() * 2f - 1f;

        // ---------------------------------------------------------------- framing and rendering

        private struct CameraPose
        {
            public Vector3 position;
            public Quaternion rotation;
            public float fov;
            public float near;
            public float far;
            public float yaw;
        }

        /// <summary>
        /// The camera of the framing: around the sticker frame at its azimuth and elevation, the focal length of the table,
        /// fitted on the focus subjects (Portrait: the head between neck and head top, a little higher in the frame) or, for
        /// a landscape with a distance, aimed at the anchor.
        /// </summary>
        private static CameraPose Frame(StickerCaptureDefinition definition, List<Placed> placed, Stage stage)
        {
            float fov = definition.FieldOfView;
            float yaw = definition.anchorYaw + definition.azimuth;
            var rotation = Quaternion.Euler(definition.elevation, yaw, 0f);
            var forward = rotation * Vector3.forward;
            if (definition.framing == StickerFraming.Landscape && definition.distance > 0f)
            {
                var a = stage.Anchor;
                var focus = new Vector3(a.x, stage.Ground(a.x, a.z) + definition.aimHeight, a.z);
                return new CameraPose { position = focus - forward * definition.distance, rotation = rotation, fov = fov, near = 0.1f, far = 400f, yaw = yaw };
            }
            var focusSet = placed.Where(p => !p.Dropped && p.Subject.focus).ToList();
            if (focusSet.Count == 0)
                focusSet = placed.Where(p => !p.Dropped).ToList();
            var region = definition.framing == StickerFraming.Portrait ? HeadRegion(focusSet) : RendererBounds(focusSet);
            float margin = definition.shape switch
            {
                StickerShape.Cutout => definition.framing == StickerFraming.Portrait ? 0.02f : 0.06f,
                StickerShape.Card => 0.1f,
                _ => 0.17f,
            };
            float allowed = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * Mathf.Max(0.1f, 1f - 2f * margin) * Mathf.Max(0.1f, definition.zoom);
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var center = region.center;
            float distance = 0.05f;
            for (int i = 0; i < 8; i++)
            {
                var corner = center + Vector3.Scale(region.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                var v = corner - center;
                float x = Vector3.Dot(v, right), y = Vector3.Dot(v, up), z = Vector3.Dot(v, forward);
                distance = Mathf.Max(distance, Mathf.Abs(x) / allowed - z, Mathf.Abs(y) / allowed - z);
            }
            var position = center - forward * distance;
            if (definition.framing == StickerFraming.Portrait)
                position -= up * (region.size.y * 0.12f);
            float radius = region.extents.magnitude;
            return new CameraPose
            {
                position = position,
                rotation = rotation,
                fov = fov,
                near = Mathf.Max(0.01f, (distance - radius) * 0.5f),
                far = stage.Studio ? distance + radius * 4f + 2f : 400f,
                yaw = yaw,
            };
        }

        /// <summary>Where the bottom of the frame meets the ground, against the zone's ground field (its near edge may show).</summary>
        private static string NearGroundOutside(WorldChunk chunk, CameraPose pose)
        {
            var field = chunk.GroundField;
            if (field == null || !field.IsValid)
                return null;
            var direction = pose.rotation * Quaternion.Euler(pose.fov * 0.5f, 0f, 0f) * Vector3.forward;
            if (direction.y > -0.01f)
                return "the bottom of the frame looks above the ground";
            float ground = chunk.Ground.HeightAt(pose.position.x, pose.position.z);
            float t = (pose.position.y - ground) / -direction.y;
            var hit = pose.position + direction * t - chunk.transform.position;
            float minZ = field.origin.y;
            return hit.z < minZ - 0.5f
                ? $"the near ground ({hit.z:F1} m) lies before the zone's ground field ({minZ:F1} m): its edge may show, check the picture"
                : null;
        }

        private static Bounds RendererBounds(List<Placed> set)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var p in set)
            {
                foreach (var renderer in p.Root.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || renderer.name.Contains("LOD1") || renderer.name.Contains("LOD2") || renderer.name.Contains("BlobShadow"))
                        continue;
                    if (first)
                    {
                        bounds = renderer.bounds;
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }
            return first ? new Bounds(set.Count > 0 ? set[0].Root.transform.position : Vector3.zero, Vector3.one * 0.3f) : bounds;
        }

        private static Bounds HeadRegion(List<Placed> set)
        {
            var animal = set.FirstOrDefault(p => p.IsAnimal);
            if (animal == null)
                return RendererBounds(set);
            var head = animal.View.SocketHeadTop;
            var neck = animal.View.SocketNeck;
            if (head != null && neck != null)
            {
                float size = Mathf.Max(0.06f, Vector3.Distance(head.position, neck.position)) * 2.6f;
                return new Bounds(Vector3.Lerp(neck.position, head.position, 0.45f), Vector3.one * size);
            }
            var body = RendererBounds(new List<Placed> { animal });
            return new Bounds(new Vector3(body.center.x, body.max.y - body.size.y * 0.35f, body.center.z), body.size * 0.7f);
        }

        /// <summary>
        /// One 1024 x 1024 MSAA 8x render through a throwaway copy of <paramref name="template"/> (instanced draws stay queued
        /// for the camera they were issued for). Studio renders clear to <paramref name="background"/>, without post-processing.
        /// </summary>
        private static Color32[] Render(Camera template, CameraPose pose, Color? background, bool post, Action<Camera> draw)
        {
            var go = new GameObject("StickerCamera") { hideFlags = HideFlags.HideAndDontSave };
            var target = RenderTexture.GetTemporary(RenderSize, RenderSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, Msaa);
            var read = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            Camera camera = null;
            try
            {
                camera = go.AddComponent<Camera>();
                camera.CopyFrom(template);
                camera.enabled = false;
                go.transform.SetPositionAndRotation(pose.position, pose.rotation);
                camera.fieldOfView = pose.fov;
                camera.nearClipPlane = pose.near;
                camera.farClipPlane = pose.far;
                camera.aspect = 1f;
                camera.allowMSAA = true;
                if (background.HasValue)
                {
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = background.Value;
                }
                var source = template.GetComponent<UniversalAdditionalCameraData>();
                var data = go.GetComponent<UniversalAdditionalCameraData>();
                if (data == null)
                    data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = post && source != null && source.renderPostProcessing;
                data.antialiasing = AntialiasingMode.None;
                data.renderShadows = true;
                camera.targetTexture = target;
                draw?.Invoke(camera);
                camera.Render();
                RenderTexture.active = target;
                read.ReadPixels(new Rect(0, 0, RenderSize, RenderSize), 0, 0);
                read.Apply(false, false);
                return read.GetPixels32();
            }
            finally
            {
                if (camera != null)
                    camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(read);
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>The 512 master (clean, no border) and the 256 game image (its shape, then the white cut border).</summary>
        private static void WriteImages(StickerCaptureDefinition definition, StickerImage master)
        {
            master.WritePng(MasterPath(definition));
            var game = master.Resample(GameSize);
            game.ApplyShape(definition.shape, Border, CardCorner);
            game.AddBorder(Border);
            game.WritePng(GamePath(definition));
        }

        // ---------------------------------------------------------------- assets: sprites, atlases, Addressables, catalog

        private static string FinishAssets(List<StickerCaptureDefinition> captured, Report report)
        {
            var parts = new List<string>();
            if (captured.Count > 0)
            {
                AssetDatabase.Refresh();
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var definition in captured)
                        ConfigureSprite(GamePath(definition));
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
                foreach (var page in captured.Select(d => d.page).Distinct())
                {
                    var sprites = StickerCaptureDefinitions.All.Where(d => d.page == page && File.Exists(GamePath(d))).Select(GamePath).ToList();
                    parts.Add(EnsureAtlas(page, sprites, report));
                }
                parts.Add(EnsureAddressables(captured.Select(d => d.page).Distinct().ToList(), report));
            }
            parts.Add($"DEF_StickerArt: {UpdateCatalog()} stickers listed");
            var images = StickerCaptureDefinitions.All.Select(GamePath).ToList();
            int drawn = StickerImage.WriteContactSheet(images, GameSize, 8, ContactSheetPath);
            parts.Add($"contact sheet {ContactSheetPath} ({drawn} of {images.Count} cells, table order)");
            AssetDatabase.SaveAssets();
            return string.Join("; ", parts) + ".";
        }

        /// <summary>A sticker sprite: single, no mipmaps, clamped, uncompressed source (its page atlas owns the compression).</summary>
        private static void ConfigureSprite(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = GameSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        /// <summary>Atlas_Page_« page »: the page's captured stickers, ASTC 4x4 on mobile, loaded with the page.</summary>
        private static string EnsureAtlas(string page, List<string> sprites, Report report)
        {
            var textures = sprites.Select(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p)).Where(t => t != null).Cast<Object>().ToArray();
            string path = AtlasPath(page);
            if (textures.Length == 0)
            {
                report.Warn(page + ": no sprite imported, no atlas");
                return page + ": no atlas";
            }
            if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var atlas = new SpriteAtlasAsset();
            atlas.Add(textures);
            SpriteAtlasAsset.Save(atlas, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (!(AssetImporter.GetAtPath(path) is SpriteAtlasImporter importer))
            {
                report.Warn(path + ": no sprite atlas importer");
                return path;
            }
            importer.includeInBuild = true;
            var packing = importer.packingSettings;
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.padding = 4;
            importer.packingSettings = packing;
            var textureSettings = importer.textureSettings;
            textureSettings.generateMipMaps = false;
            textureSettings.filterMode = FilterMode.Bilinear;
            textureSettings.sRGB = true;
            importer.textureSettings = textureSettings;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = 2048;
                settings.format = TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformSettings(settings);
            }
            importer.SaveAndReimport();
            return $"{Path.GetFileNameWithoutExtension(path)} ({textures.Length} stickers)";
        }

        private static string EnsureAddressables(List<string> pages, Report report)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(StickerArtCatalog.Group) ?? settings.CreateGroup(StickerArtCatalog.Group, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            var update = group.GetSchema<ContentUpdateGroupSchema>() ?? group.AddSchema<ContentUpdateGroupSchema>();
            update.StaticContent = true;
            var addresses = new List<string>();
            foreach (var page in pages)
            {
                string guid = AssetDatabase.AssetPathToGUID(AtlasPath(page));
                if (string.IsNullOrEmpty(guid))
                {
                    report.Warn(page + ": atlas missing, no Addressables entry");
                    continue;
                }
                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.address = StickerCaptureDefinition.AddressOf(page);
                addresses.Add(entry.address);
            }
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            return $"Addressables {StickerArtCatalog.Group}: " + string.Join(", ", addresses);
        }

        /// <summary>DEF_StickerArt: every sticker whose game image and page atlas exist, in table order.</summary>
        private static int UpdateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StickerArtCatalog>(StickerArtCatalog.AssetPath);
            if (catalog == null)
            {
                EnsureFolder(Path.GetDirectoryName(StickerArtCatalog.AssetPath).Replace('\\', '/'));
                catalog = ScriptableObject.CreateInstance<StickerArtCatalog>();
                AssetDatabase.CreateAsset(catalog, StickerArtCatalog.AssetPath);
            }
            var entries = new List<StickerArtCatalog.Entry>();
            foreach (var definition in StickerCaptureDefinitions.All)
            {
                if (!File.Exists(GamePath(definition)) || !File.Exists(AtlasPath(definition.page)))
                    continue;
                entries.Add(new StickerArtCatalog.Entry
                {
                    stickerKey = definition.StickerKey,
                    pageAtlasAddress = definition.AtlasAddress,
                    spriteName = definition.id,
                });
            }
            catalog.entries = entries.ToArray();
            EditorUtility.SetDirty(catalog);
            WriteKeys(catalog.entries);
            return entries.Count;
        }

        /// <summary>
        /// The captured stickers' keys as code (Presentation/Albums/StickerArtKeys.Generated.cs): the bootstrap hands them to
        /// the albums at launch, before any screen loads, so a sticker without art is never drawn.
        /// </summary>
        private static void WriteKeys(StickerArtCatalog.Entry[] entries)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// Generated by StickerCapturePipeline (T44A): the stickers whose art is captured. Do not edit.");
            sb.AppendLine("namespace PocketWilds.Presentation");
            sb.AppendLine("{");
            sb.AppendLine("    public static class StickerArtKeys");
            sb.AppendLine("    {");
            sb.AppendLine("        public static readonly string[] Captured =");
            sb.AppendLine("        {");
            foreach (var entry in entries)
                sb.Append("            \"").Append(entry.stickerKey).AppendLine("\",");
            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            const string path = "Assets/_Game/Presentation/Albums/StickerArtKeys.Generated.cs";
            string text = sb.ToString();
            if (!System.IO.File.Exists(path) || System.IO.File.ReadAllText(path) != text)
            {
                System.IO.File.WriteAllText(path, text);
                AssetDatabase.ImportAsset(path);
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
