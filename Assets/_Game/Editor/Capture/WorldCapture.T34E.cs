using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
    /// <summary>
    /// T34E review sheets of the charte « fait main » (sections 3.5, 4.1 and 10): black silhouettes at 64 px on a light
    /// and a dark background, the family side by side under the morning, day and evening light, the gameplay frame per
    /// quality tier, and the value contrast between every animal variant and the grass around it (12 L* at least).
    /// Written to Docs/Captures/Review. The scene is reopened from disk afterwards, nothing is saved.
    /// </summary>
    public static partial class WorldCapture
    {
        public const string ReviewFolder = "Docs/Captures/Review";

        /// <summary>A layer no game object uses: the mask passes draw only what is moved onto it.</summary>
        private const int ReviewLayer = 31;

        private static readonly string[] ReviewFamily = { "rabbit.brown", "squirrel.red", "hedgehog.brown" };

        /// <summary>The variants closest in value to the grass (T34E contrast measure), in the gameplay frame.</summary>
        private static readonly string[] LowContrastMix = { "rabbit.grey", "squirrel.grey", "hedgehog.grey", "squirrel.autumn", "hedgehog.mushroom", "rabbit.sakura" };

        private static readonly Color32 SilhouetteInk = new Color32(21, 21, 24, 255);
        private static readonly Color32 LightPaper = new Color32(238, 232, 218, 255);
        private static readonly Color32 DarkPaper = new Color32(60, 66, 80, 255);

        [MenuItem("Pocket Wilds/Capture/Handmade Review (T34E)", priority = 530)]
        public static void CaptureHandmadeMenu() => Debug.Log("[WorldCapture] " + CaptureHandmadeReview("T34E"));

        public static string CaptureHandmadeReview(string prefix)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(ReviewFolder);

            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var quality = AssetDatabase.LoadAssetAtPath<WorldQualityProfile>(GrasslandPipeline.WorldQualityPath);
            int level0 = QualitySettings.GetQualityLevel();
            var temp = new List<GameObject>();
            var results = new List<string>();
            var unlit = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.white };
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var start = SceneChunk();
                    start.Bind(palettes);
                    var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var sun = Object.FindAnyObjectByType<Light>();
                    var camera = Camera.main;
                    FrameLikeGame(start);
                    var resolver = new AnimalVisualResolver(registry, palettes);
                    ScatterRenderer scatter = null;

                    void Setup(QualityTier tier)
                    {
                        QualitySettings.SetQualityLevel((int)tier, true);
                        entry.Lighting.Apply(sun, camera, tier);
                        start.ApplyQuality(tier);
                        var tierQuality = quality != null ? quality.Get(tier) : WorldQualityProfile.Default(tier);
                        scatter = new ScatterRenderer(palettes, tierQuality.scatterDensity);
                        scatter.Add(start);
                    }
                    string Shot(string name, int w, int h) =>
                        CaptureUtility.RenderWithFreshCamera(camera, w, h, $"{ReviewFolder}/{prefix}_{name}.png", cam => scatter.Render(cam));
                    void Clear()
                    {
                        foreach (var go in temp)
                            Object.DestroyImmediate(go);
                        temp.Clear();
                    }

                    Setup(QualityTier.Medium);
                    results.Add(Silhouettes(prefix, registry, resolver, camera, unlit, temp));
                    Clear();

                    // The family side by side, same scale, same light, three-quarter towards the camera.
                    var position = camera.transform.position;
                    var rotation = camera.transform.rotation;
                    var egg = start.EggSpawn.position;
                    var row = egg + new Vector3(0f, 0f, -1.5f);
                    for (int i = 0; i < ReviewFamily.Length; i++)
                        temp.Add(Spawn(registry, resolver, ReviewFamily[i], (uint)(i + 1), (uint)(i * 31 + 3), row + new Vector3((i - 1) * 0.62f, 0f, 0f), 200f, 0f).gameObject);
                    camera.transform.position = row + new Vector3(0.5f, 0.75f, -2.1f);
                    camera.transform.LookAt(row + new Vector3(0f, 0.2f, 0f));
                    var morning = AssetDatabase.LoadAssetAtPath<LightingProfile>(LightingProfilePath("Morning"));
                    var evening = AssetDatabase.LoadAssetAtPath<LightingProfile>(LightingProfilePath("Evening"));
                    var mediumQuality = quality != null ? quality.Get(QualityTier.Medium) : WorldQualityProfile.Default(QualityTier.Medium);
                    var atmosphere = new WorldAtmosphere(morning, entry.Lighting, evening, sun, camera, mediumQuality);
                    foreach (var mood in new[] { LightingMood.Morning, LightingMood.Day, LightingMood.Evening })
                    {
                        atmosphere.ForceMood(mood);
                        atmosphere.Tick(0f);
                        results.Add(Shot("family_" + mood, 1200, 700));
                    }
                    atmosphere.ForceMood(LightingMood.Day);
                    atmosphere.Tick(0f);
                    Clear();
                    camera.transform.SetPositionAndRotation(position, rotation);

                    // The gameplay frame per tier (60/30/10 bands are measured on the Medium one).
                    for (int level = 0; level < QualitySettings.names.Length; level++)
                    {
                        Setup((QualityTier)level);
                        Clear();
                        Populate(registry, palettes, start, SpeciesMix.Length, temp, SpeciesMix);
                        results.Add(Shot("game_" + QualitySettings.names[level], 540, 1170));
                    }
                    Setup(QualityTier.Medium);
                    Clear();
                    Populate(registry, palettes, start, LowContrastMix.Length, temp, LowContrastMix);
                    results.Add(Shot("game_low_contrast_variants", 540, 1170));
                    Clear();

                    Setup(QualityTier.Medium);
                    results.Add(Contrast(prefix, registry, resolver, camera, start, unlit, temp, () => scatter));
                }
                finally
                {
                    QualitySettings.SetQualityLevel(level0, true);
                    foreach (var go in temp)
                        Object.DestroyImmediate(go);
                    Object.DestroyImmediate(unlit);
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }
            return string.Join(", ", results);
        }

        /// <summary>
        /// Charte 4.1 remedy preview: the low-contrast variants side by side on the grass and in the gameplay frame, with
        /// the rim of MAT_Animal_Master raised to <paramref name="rimStrength"/> on a copy (the asset is untouched).
        /// </summary>
        public static string CaptureReadabilityRim(string prefix, float rimStrength)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var quality = AssetDatabase.LoadAssetAtPath<WorldQualityProfile>(GrasslandPipeline.WorldQualityPath);
            var temp = new List<GameObject>();
            var results = new List<string>();
            Material rim = null;
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var start = SceneChunk();
                    start.Bind(palettes);
                    var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var camera = Camera.main;
                    FrameLikeGame(start);
                    QualitySettings.SetQualityLevel((int)QualityTier.Medium, true);
                    entry.Lighting.Apply(Object.FindAnyObjectByType<Light>(), camera, QualityTier.Medium);
                    start.ApplyQuality(QualityTier.Medium);
                    var tierQuality = quality != null ? quality.Get(QualityTier.Medium) : WorldQualityProfile.Default(QualityTier.Medium);
                    var scatter = new ScatterRenderer(palettes, tierQuality.scatterDensity);
                    scatter.Add(start);
                    var resolver = new AnimalVisualResolver(registry, palettes);
                    string tag = "rim" + Mathf.RoundToInt(rimStrength * 100f).ToString("D2", CultureInfo.InvariantCulture);

                    void Rim(GameObject root)
                    {
                        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                        {
                            var shared = renderer.sharedMaterials;
                            if (rim == null && shared.Length > 0)
                            {
                                rim = new Material(shared[0]) { hideFlags = HideFlags.DontSave };
                                rim.SetFloat("_RimStrength", rimStrength);
                            }
                            renderer.sharedMaterials = Enumerable.Repeat(rim, shared.Length).ToArray();
                        }
                    }

                    var position = camera.transform.position;
                    var rotation = camera.transform.rotation;
                    var row = start.EggSpawn.position + new Vector3(0f, 0f, -1.5f);
                    for (int i = 0; i < LowContrastMix.Length; i++)
                    {
                        var view = Spawn(registry, resolver, LowContrastMix[i], (uint)(i + 1), (uint)(i * 31 + 3), row + new Vector3((i - 2.5f) * 0.55f, 0f, (i % 2) * 0.35f), 195f, 0f);
                        Rim(view.gameObject);
                        temp.Add(view.gameObject);
                    }
                    camera.transform.position = row + new Vector3(0f, 1.25f, -3.0f);
                    camera.transform.LookAt(row + new Vector3(0f, 0.15f, 0.15f));
                    results.Add(CaptureUtility.RenderWithFreshCamera(camera, 1400, 620, $"{ReviewFolder}/{prefix}_low_contrast_row_{tag}.png", cam => scatter.Render(cam)));
                    foreach (var go in temp)
                        Object.DestroyImmediate(go);
                    temp.Clear();
                    camera.transform.SetPositionAndRotation(position, rotation);

                    Populate(registry, palettes, start, LowContrastMix.Length, temp, LowContrastMix);
                    foreach (var go in temp)
                        if (go.GetComponent<AnimalView>() != null)
                            Rim(go);
                    results.Add(CaptureUtility.RenderWithFreshCamera(camera, 540, 1170, $"{ReviewFolder}/{prefix}_low_contrast_game_{tag}.png", cam => scatter.Render(cam)));
                }
                finally
                {
                    foreach (var go in temp)
                        Object.DestroyImmediate(go);
                    if (rim != null)
                        Object.DestroyImmediate(rim);
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }
            return string.Join(", ", results);
        }

        private static readonly string[] Paws = { "FrontPaw_L", "FrontPaw_R", "HindPaw_L", "HindPaw_R" };

        /// <summary>
        /// Charte section 8 on the real clips of each species (its override controller), sampled at 60 Hz on the LOD0
        /// skin: body travel (3 cm at most), largest bone scale change (idle 2 %, others 10 %), ear swing in the idles
        /// (8 degrees), the gap under each paw at rest, and how far a paw slides while it bears weight when the body
        /// moves at the species' speed (1 cm). Writes Docs/Captures/Review/{prefix}_animation.csv and a frame strip.
        /// </summary>
        public static string MeasureAnimation(string prefix)
        {
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var report = new StringBuilder("species,clip,length_s,body_travel_cm,max_scale_change_pct,ear_swing_deg,paw_gap_rest_mm,paw_slide_cm\n");
            var inv = CultureInfo.InvariantCulture;
            foreach (var speciesName in new[] { "rabbit", "squirrel", "hedgehog" })
            {
                var species = registry.Species.Get(ContentId.FromSource("species." + speciesName));
                catalog.TryGet(species.VisualKey, out var visual);
                var view = Object.Instantiate(visual.prefab);
                try
                {
                    var root = view.Animator.gameObject;
                    // The prefabs ship with optimized skeletons (bones hidden): this copy gets its bones back.
                    AnimatorUtility.DeoptimizeTransformHierarchy(root);
                    view.Animator.enabled = false;
                    var bones = root.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                    var skin = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s => s.sharedMesh.vertexCount).First();
                    var weights = skin.sharedMesh.boneWeights;
                    var skinBones = skin.bones;
                    var pawOf = new int[weights.Length];
                    for (int i = 0; i < weights.Length; i++)
                    {
                        string bone = skinBones[weights[i].boneIndex0].name;
                        pawOf[i] = System.Array.IndexOf(Paws, bone);
                    }
                    var baked = new Mesh();
                    float speed = visual.behavior != null ? visual.behavior.moveSpeed : 0.38f;
                    foreach (var clip in view.Animator.runtimeAnimatorController.animationClips.Distinct())
                    {
                        int frames = Mathf.Max(2, Mathf.RoundToInt(clip.length * 60f));
                        bool idle = clip.name.Contains("Idle");
                        bool move = clip.name.Contains("Locomotion");
                        Vector3 pelvis0 = Vector3.zero;
                        float travel = 0f, scaleChange = 0f, earSwing = 0f;
                        var earRest = new Dictionary<string, Quaternion>();
                        var pawGap = new float[4];
                        var pawMinY = new float[frames + 1, 4];
                        var pawZ = new float[frames + 1, 4];
                        for (int f = 0; f <= frames; f++)
                        {
                            clip.SampleAnimation(root, clip.length * f / frames);
                            if (bones.TryGetValue("Pelvis", out var pelvis))
                            {
                                var p = pelvis.position;
                                if (f == 0)
                                    pelvis0 = p;
                                travel = Mathf.Max(travel, new Vector2(p.x - pelvis0.x, p.z - pelvis0.z).magnitude);
                            }
                            foreach (var t in bones.Values)
                                scaleChange = Mathf.Max(scaleChange, Mathf.Abs(t.localScale.x - 1f), Mathf.Abs(t.localScale.y - 1f), Mathf.Abs(t.localScale.z - 1f));
                            foreach (var ear in new[] { "Ear_L_01", "Ear_R_01" })
                                if (bones.TryGetValue(ear, out var e))
                                {
                                    if (f == 0)
                                        earRest[ear] = e.localRotation;
                                    earSwing = Mathf.Max(earSwing, Quaternion.Angle(earRest[ear], e.localRotation));
                                }
                            skin.BakeMesh(baked, true);
                            var vertices = baked.vertices;
                            var m = skin.transform.localToWorldMatrix;
                            for (int k = 0; k < 4; k++)
                                pawMinY[f, k] = float.MaxValue;
                            for (int i = 0; i < vertices.Length; i++)
                            {
                                int k = pawOf[i];
                                if (k < 0)
                                    continue;
                                var w = m.MultiplyPoint3x4(vertices[i]);
                                if (w.y < pawMinY[f, k])
                                {
                                    pawMinY[f, k] = w.y;
                                    pawZ[f, k] = w.z;
                                }
                            }
                        }
                        float rootY = root.transform.position.y;
                        for (int k = 0; k < 4; k++)
                        {
                            float gap = float.MaxValue;
                            for (int f = 0; f <= frames; f++)
                                gap = Mathf.Min(gap, pawMinY[f, k] - rootY);
                            pawGap[k] = gap;
                        }
                        // Sliding: while a paw is within 6 mm of its lowest point it bears weight; the body moves forward
                        // (+Z at yaw 0) at the species' speed, so the paw's ground speed is its clip speed plus that.
                        float slide = 0f;
                        if (move)
                        {
                            float dt = clip.length / frames;
                            for (int k = 0; k < 4; k++)
                            {
                                float run = 0f;
                                for (int f = 1; f <= frames; f++)
                                {
                                    bool stance = pawMinY[f, k] - rootY - pawGap[k] < 0.006f && pawMinY[f - 1, k] - rootY - pawGap[k] < 0.006f;
                                    if (stance)
                                        run += pawZ[f, k] - pawZ[f - 1, k] + speed * dt;
                                    else
                                        run = 0f;
                                    slide = Mathf.Max(slide, Mathf.Abs(run));
                                }
                            }
                        }
                        report.Append(string.Format(inv, "{0},{1},{2:F2},{3:F1},{4:F1},{5},{6},{7}\n", speciesName, clip.name, clip.length, travel * 100f,
                            scaleChange * 100f, idle ? earSwing.ToString("F1", inv) : "",
                            idle ? string.Join(" ", pawGap.Select(g => (g * 1000f).ToString("F0", inv))) : "",
                            move ? (slide * 100f).ToString("F1", inv) : ""));
                    }
                    Object.DestroyImmediate(baked);
                }
                finally
                {
                    Object.DestroyImmediate(view.gameObject);
                }
            }
            Directory.CreateDirectory(ReviewFolder);
            string path = $"{ReviewFolder}/{prefix}_animation.csv";
            File.WriteAllText(path, report.ToString());
            return path;
        }

        /// <summary>
        /// Charte section 8 review strip: the clips of one species in profile, <paramref name="frames"/> evenly spaced
        /// frames each, lit, over a ground line, so poses, lift and contacts read frame by frame.
        /// </summary>
        public static string CaptureFrameStrip(string prefix, string speciesAndVariant, string[] clipNames, int frames = 8)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            const int cell = 220;
            var holder = new GameObject("ReviewCamera") { hideFlags = HideFlags.HideAndDontSave };
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.45f, 0.5f, 0.42f) };
            Texture2D sheet = null;
            AnimalView view = null;
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var sun = Object.FindAnyObjectByType<Light>();
                    QualitySettings.SetQualityLevel((int)QualityTier.Medium, true);
                    var cam = holder.AddComponent<Camera>();
                    cam.enabled = false;
                    entry.Lighting.Apply(sun, Camera.main, QualityTier.Medium);
                    var far = new Vector3(0f, -50f, 0f);
                    view = Spawn(registry, new AnimalVisualResolver(registry, palettes), speciesAndVariant, 1, 3, far, 270f, 0f);
                    view.LodGroup.ForceLOD(0);
                    var root = view.Animator.gameObject;
                    AnimatorUtility.DeoptimizeTransformHierarchy(root);
                    view.Animator.enabled = false;
                    ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
                    ground.transform.SetPositionAndRotation(far + new Vector3(0f, -0.004f, 0f), Quaternion.identity);
                    ground.transform.localScale = new Vector3(1.4f, 0.008f, 0.8f);
                    MoveToReviewLayer(view.gameObject, null);
                    MoveToReviewLayer(ground, null);
                    cam.orthographic = true;
                    cam.orthographicSize = 0.45f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.93f, 0.91f, 0.86f);
                    cam.cullingMask = 1 << ReviewLayer;
                    cam.nearClipPlane = 0.05f;
                    cam.farClipPlane = 20f;
                    cam.transform.SetPositionAndRotation(far + new Vector3(0f, 0.36f, -4f), Quaternion.identity);
                    var clips = view.Animator.runtimeAnimatorController.animationClips;
                    sheet = new Texture2D(cell * frames, cell * clipNames.Length, TextureFormat.RGB24, false);
                    string tmp = $"{CaptureUtility.DefaultFolder}/{prefix}_strip_frame.png";
                    for (int c = 0; c < clipNames.Length; c++)
                    {
                        var clip = clips.FirstOrDefault(k => k.name.EndsWith(clipNames[c]));
                        if (clip == null)
                            continue;
                        for (int f = 0; f < frames; f++)
                        {
                            clip.SampleAnimation(root, clip.length * f / frames);
                            CaptureUtility.Render(cam, cell, cell, tmp);
                            var pixels = ReadPng(tmp);
                            sheet.SetPixels32(f * cell, (clipNames.Length - 1 - c) * cell, cell, cell, pixels);
                        }
                    }
                    sheet.Apply();
                    string path = $"{ReviewFolder}/{prefix}_frames_{speciesAndVariant.Substring(0, speciesAndVariant.IndexOf('.'))}.png";
                    File.WriteAllBytes(path, sheet.EncodeToPNG());
                    return path;
                }
                finally
                {
                    if (view != null)
                        Object.DestroyImmediate(view.gameObject);
                    Object.DestroyImmediate(ground);
                    Object.DestroyImmediate(groundMaterial);
                    if (sheet != null)
                        Object.DestroyImmediate(sheet);
                    Object.DestroyImmediate(holder);
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }
        }

        private static void MoveToReviewLayer(GameObject root, Material mask)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = ReviewLayer;
            if (mask == null)
                return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.SetPropertyBlock(null);
                renderer.sharedMaterials = Enumerable.Repeat(mask, renderer.sharedMaterials.Length).ToArray();
            }
        }

        /// <summary>Coverage (0..1) of what stands on the review layer, through a throwaway camera.</summary>
        private static float[] Mask(Camera template, int width, int height)
        {
            int cullingMask = template.cullingMask;
            template.cullingMask = 1 << ReviewLayer;
            try
            {
                var pixels = CaptureUtility.RenderLinear(template, width, height, Color.clear, null);
                return pixels.Select(p => Mathf.Clamp01(p.r)).ToArray();
            }
            finally
            {
                template.cullingMask = cullingMask;
            }
        }

        /// <summary>
        /// Charte 3.5: every species as a black silhouette 64 px high, front, profile and three-quarter, on a light and a
        /// dark background. Each 64 px cell is shown three times larger, pixel for pixel, so the sheet reads in a doc.
        /// </summary>
        private static string Silhouettes(string prefix, ContentRegistry registry, AnimalVisualResolver resolver, Camera camera, Material unlit, List<GameObject> temp)
        {
            const int render = 512, cellHeight = 64, zoom = 3, cellWidth = 96, gap = 8;
            float[] yaws = { 180f, 270f, 225f };
            var holder = new GameObject("ReviewCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = holder.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 20f;
            var far = new Vector3(0f, -50f, 0f);
            int columns = yaws.Length * 2;
            int sheetW = columns * (cellWidth * zoom + gap) + gap, sheetH = ReviewFamily.Length * (cellHeight * zoom + gap) + gap;
            var sheet = new Texture2D(sheetW, sheetH, TextureFormat.RGB24, false);
            var fill = Enumerable.Repeat(new Color32(250, 250, 248, 255), sheetW * sheetH).ToArray();
            sheet.SetPixels32(fill);
            try
            {
                for (int s = 0; s < ReviewFamily.Length; s++)
                {
                    for (int v = 0; v < yaws.Length; v++)
                    {
                        var view = Spawn(registry, resolver, ReviewFamily[s], 1, 3, far, yaws[v], 0f);
                        temp.Add(view.gameObject);
                        view.LodGroup.ForceLOD(0);
                        MoveToReviewLayer(view.gameObject, unlit);
                        var bounds = new Bounds(view.transform.position, Vector3.zero);
                        foreach (var r in view.GetComponentsInChildren<Renderer>())
                            bounds.Encapsulate(r.bounds);
                        cam.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.15f;
                        cam.transform.SetPositionAndRotation(bounds.center + new Vector3(0f, 0f, -5f), Quaternion.identity);
                        var mask = Mask(cam, render, render);
                        temp.Remove(view.gameObject);
                        Object.DestroyImmediate(view.gameObject);

                        // Tight box, then box-filter down so the silhouette is 64 px high.
                        int minX = render, minY = render, maxX = -1, maxY = -1;
                        for (int y = 0; y < render; y++)
                            for (int x = 0; x < render; x++)
                                if (mask[y * render + x] > 0.5f)
                                {
                                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                                }
                        if (maxX < 0)
                            continue;
                        float scale = (maxY - minY + 1) / (float)cellHeight;
                        int width = Mathf.Min(cellWidth, Mathf.CeilToInt((maxX - minX + 1) / scale));
                        var small = new float[cellWidth * cellHeight];
                        for (int y = 0; y < cellHeight; y++)
                            for (int x = 0; x < width; x++)
                            {
                                float sum = 0f; int n = 0;
                                for (int sy = Mathf.FloorToInt(minY + y * scale); sy < Mathf.Min(render, Mathf.FloorToInt(minY + (y + 1) * scale)); sy++)
                                    for (int sx = Mathf.FloorToInt(minX + x * scale); sx < Mathf.Min(render, Mathf.FloorToInt(minX + (x + 1) * scale)); sx++)
                                    { sum += mask[sy * render + sx]; n++; }
                                small[y * cellWidth + x + (cellWidth - width) / 2] = n > 0 ? sum / n : 0f;
                            }
                        for (int b = 0; b < 2; b++)
                        {
                            var paper = b == 0 ? LightPaper : DarkPaper;
                            int ox = gap + (v * 2 + b) * (cellWidth * zoom + gap);
                            int oy = sheetH - (s + 1) * (cellHeight * zoom + gap);
                            for (int y = 0; y < cellHeight * zoom; y++)
                                for (int x = 0; x < cellWidth * zoom; x++)
                                {
                                    float a = small[(y / zoom) * cellWidth + x / zoom];
                                    sheet.SetPixel(ox + x, oy + y, Color.Lerp(paper, SilhouetteInk, a));
                                }
                        }
                    }
                }
                sheet.Apply();
                string path = $"{ReviewFolder}/{prefix}_silhouettes_64px.png";
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                return path;
            }
            finally
            {
                Object.DestroyImmediate(sheet);
                Object.DestroyImmediate(holder);
            }
        }

        /// <summary>CIELAB L* of an sRGB color (D65).</summary>
        public static float LStar(Color32 c)
        {
            float Linear(byte v)
            {
                float f = v / 255f;
                return f <= 0.04045f ? f / 12.92f : Mathf.Pow((f + 0.055f) / 1.055f, 2.4f);
            }
            float y = 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);
            return y > 0.008856f ? 116f * Mathf.Pow(y, 1f / 3f) - 16f : 903.3f * y;
        }

        /// <summary>
        /// CIELAB (D65) of an sRGB color; with <paramref name="deutan"/>, as a deuteranope sees it (Machado 2009,
        /// full severity, in linear RGB): red-green colorblindness, about one man in twelve.
        /// </summary>
        public static Vector3 Lab(Color32 c, bool deutan = false)
        {
            float Linear(byte v)
            {
                float f = v / 255f;
                return f <= 0.04045f ? f / 12.92f : Mathf.Pow((f + 0.055f) / 1.055f, 2.4f);
            }
            float F(float t) => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;
            float r = Linear(c.r), g = Linear(c.g), b = Linear(c.b);
            if (deutan)
            {
                float dr = 0.367322f * r + 0.860646f * g - 0.227968f * b;
                float dg = 0.280085f * r + 0.672501f * g + 0.047413f * b;
                float db = -0.011820f * r + 0.042940f * g + 0.968881f * b;
                r = Mathf.Clamp01(dr); g = Mathf.Clamp01(dg); b = Mathf.Clamp01(db);
            }
            float x = (0.4124f * r + 0.3576f * g + 0.1805f * b) / 0.95047f;
            float y = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            float z = (0.0193f * r + 0.1192f * g + 0.9505f * b) / 1.08883f;
            return new Vector3(116f * F(y) - 16f, 500f * (F(x) - F(y)), 200f * (F(y) - F(z)));
        }

        private static Color32[] ReadPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                texture.LoadImage(File.ReadAllBytes(path));
                return texture.GetPixels32();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Charte 4.1: in the gameplay frame (Medium, day), the mean L* of each variant against the mean L* of the grass
        /// in a ring around it, the grass rendered without the animal. A variant passes at 12 L* apart or more.
        /// </summary>
        private static string Contrast(string prefix, ContentRegistry registry, AnimalVisualResolver resolver, Camera camera, WorldChunk start,
            Material unlit, List<GameObject> temp, System.Func<ScatterRenderer> scatter)
        {
            const int w = 1080, h = 2340, ring = 14;
            var spot = start.EggSpawn.position + new Vector3(-0.3f, 0f, -1.6f);
            string tmpA = $"{CaptureUtility.DefaultFolder}/{prefix}_contrast_animal.png", tmpG = $"{CaptureUtility.DefaultFolder}/{prefix}_contrast_ground.png";
            var report = new StringBuilder("variant,animal_L,ground_L,delta_L,delta_E,delta_E_deutan,pixels,value_pass\n");
            int failed = 0, total = 0;
            var variants = new List<string>();
            for (int i = 0; i < registry.Variants.Count; i++)
            {
                var variant = registry.Variants[i];
                string id = variant.Id.ToString();
                if (id.StartsWith("variant.rabbit.") || id.StartsWith("variant.squirrel.") || id.StartsWith("variant.hedgehog."))
                    variants.Add(id.Substring("variant.".Length));
            }
            CaptureUtility.RenderWithFreshCamera(camera, w, h, tmpG, cam => scatter().Render(cam));
            var ground = ReadPng(tmpG);
            foreach (var name in variants)
            {
                var view = Spawn(registry, resolver, name, 1, 3, spot, 200f, 0f);
                temp.Add(view.gameObject);
                CaptureUtility.RenderWithFreshCamera(camera, w, h, tmpA, cam => scatter().Render(cam));
                var lit = ReadPng(tmpA);
                MoveToReviewLayer(view.gameObject, unlit);
                var mask = Mask(camera, w, h);
                temp.Remove(view.gameObject);
                Object.DestroyImmediate(view.gameObject);

                // Animal pixels: covered and changed by the animal (grass in front of the feet stays grass).
                var inside = new bool[w * h];
                int minX = w, minY = h, maxX = 0, maxY = 0, count = 0;
                double animalL = 0;
                Vector3 animalLab = Vector3.zero, animalDeutan = Vector3.zero;
                for (int k = 0; k < inside.Length; k++)
                {
                    if (mask[k] < 0.5f)
                        continue;
                    var a = lit[k]; var g = ground[k];
                    if (Mathf.Abs(a.r - g.r) + Mathf.Abs(a.g - g.g) + Mathf.Abs(a.b - g.b) < 6)
                        continue;
                    inside[k] = true;
                    animalL += LStar(a);
                    animalLab += Lab(a);
                    animalDeutan += Lab(a, true);
                    count++;
                    int x = k % w, y = k / w;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
                if (count == 0)
                    continue;
                double groundL = 0;
                int groundCount = 0;
                Vector3 groundLab = Vector3.zero, groundDeutan = Vector3.zero;
                for (int y = Mathf.Max(0, minY - ring); y <= Mathf.Min(h - 1, maxY + ring); y++)
                    for (int x = Mathf.Max(0, minX - ring); x <= Mathf.Min(w - 1, maxX + ring); x++)
                    {
                        int k = y * w + x;
                        if (mask[k] > 0.01f)
                            continue;
                        groundL += LStar(ground[k]);
                        groundLab += Lab(ground[k]);
                        groundDeutan += Lab(ground[k], true);
                        groundCount++;
                    }
                float al = (float)(animalL / count), gl = (float)(groundL / Mathf.Max(1, groundCount));
                bool pass = Mathf.Abs(al - gl) >= 12f;
                total++;
                if (!pass)
                    failed++;
                float deltaE = Vector3.Distance(animalLab / count, groundLab / Mathf.Max(1, groundCount));
                float deltaDeutan = Vector3.Distance(animalDeutan / count, groundDeutan / Mathf.Max(1, groundCount));
                report.Append(string.Format(CultureInfo.InvariantCulture, "{0},{1:F1},{2:F1},{3:F1},{4:F1},{5:F1},{6},{7}\n", name, al, gl, al - gl,
                    deltaE, deltaDeutan, count, pass ? "yes" : "no"));
                if (total == 1)
                    File.Copy(tmpA, $"{ReviewFolder}/{prefix}_contrast_spot.png", true);
            }
            string path = $"{ReviewFolder}/{prefix}_contrast.csv";
            File.WriteAllText(path, report.ToString());
            return $"{path} ({total - failed}/{total} at 12 L* or more)";
        }
    }
}
