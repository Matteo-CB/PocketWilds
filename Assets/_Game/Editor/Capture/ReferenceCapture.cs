using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Eggs;
using PocketWilds.Editor.Environment;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Capture
{
    /// <summary>
    /// T34C review captures, all driven by CaptureProfile data (M-DEF-CAPTURE):
    /// - debug views (R-DBG-LOD, R-DBG-OVERDRAW): LOD colors on the refuge seen from the farthest zoom, overdraw heat
    ///   maps of the refuge and of every feedback effect at once (layers counted per pixel, with their statistics);
    /// - golden references (R-GOLDEN-VISUAL): the refuge on each tier, deterministic (fixed animals, day mood, no
    ///   clock), stored in Docs/Captures/Golden; GoldenVisualTests renders them again and compares;
    /// - store and share frames (Capture_MarketingPortrait, Capture_ShareSquare, Capture_ShareStory) from the real game.
    /// Debug materials live in Art/Debug and never ship (BuildValidator).
    /// </summary>
    public static class ReferenceCapture
    {
        public const string DebugFolder = "Assets/_Game/Art/Debug";
        public const string GoldenFolder = "Docs/Captures/Golden";
        public static readonly string[] GoldenProfiles = { "Capture_Golden_Refuge_Low", "Capture_Golden_Refuge_Medium", "Capture_Golden_Refuge_High" };
        public static readonly string[] ReviewProfiles = { "Capture_LOD_Refuge", "Capture_Overdraw_Refuge", "Capture_Overdraw_Hatch" };
        public static readonly string[] StoreProfiles = { "Capture_MarketingPortrait", "Capture_ShareSquare", "Capture_ShareStory" };

        private static readonly int WindId = Shader.PropertyToID("_PW_Wind");
        private static readonly int WindWavesId = Shader.PropertyToID("_PW_WindWaves");
        private static readonly Color[] LodColors = { new Color(0.9f, 0.25f, 0.2f), new Color(0.3f, 0.8f, 0.3f), new Color(0.25f, 0.45f, 0.95f) };

        // ---------------------------------------------------------------- profiles and debug materials

        [MenuItem("Pocket Wilds/Capture/Build Capture Profiles (T34C)", priority = 400)]
        public static void BuildProfilesMenu() => Debug.Log("[ReferenceCapture] " + BuildProfiles());

        public static string BuildProfiles()
        {
            Directory.CreateDirectory(CaptureProfile.Folder);
            int count = 0;
            void Make(string name, Action<CaptureProfile> set)
            {
                string path = $"{CaptureProfile.Folder}/{name}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<CaptureProfile>(path);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<CaptureProfile>();
                    AssetDatabase.CreateAsset(profile, path);
                }
                set(profile);
                EditorUtility.SetDirty(profile);
                count++;
            }

            // Icons (doc 06 icon sizes): rendered three times larger, framed on the silhouette, transparent.
            Make("Capture_AnimalIcon", p => Icon(p, CaptureShot.AnimalIcon, 256, 24f, 28f, 12f, 0.07f));
            Make("Capture_EggIcon", p => Icon(p, CaptureShot.EggIcon, 256, 24f, 20f, 10f, 0.08f));
            Make("Capture_DecorIcon", p => Icon(p, CaptureShot.DecorIcon, 256, 26f, 35f, 22f, 0.08f));
            // Store and share (doc 10): the real game frame on the top tier.
            Make("Capture_MarketingPortrait", p => Frame(p, CaptureShot.RefugeView, 1242, 2688, QualityTier.High, 6));
            Make("Capture_ShareSquare", p => Frame(p, CaptureShot.RefugeView, 1080, 1080, QualityTier.High, 6));
            Make("Capture_ShareStory", p => Frame(p, CaptureShot.RefugeView, 1080, 1920, QualityTier.High, 6));
            // Golden references: phone frame, every tier.
            Make("Capture_Golden_Refuge_Low", p => Frame(p, CaptureShot.RefugeView, 540, 1170, QualityTier.Low, 6));
            Make("Capture_Golden_Refuge_Medium", p => Frame(p, CaptureShot.RefugeView, 540, 1170, QualityTier.Medium, 6));
            Make("Capture_Golden_Refuge_High", p => Frame(p, CaptureShot.RefugeView, 540, 1170, QualityTier.High, 6));
            // Debug reviews.
            Make("Capture_LOD_Refuge", p =>
            {
                Frame(p, CaptureShot.RefugeFar, 540, 1170, QualityTier.Medium, 6);
                p.debugView = CaptureDebugView.LodColors;
            });
            Make("Capture_Overdraw_Refuge", p =>
            {
                Frame(p, CaptureShot.RefugeView, 540, 1170, QualityTier.High, 6);
                p.debugView = CaptureDebugView.Overdraw;
                p.msaa = 1;
            });
            Make("Capture_Overdraw_Hatch", p =>
            {
                Frame(p, CaptureShot.HatchBurst, 540, 1170, QualityTier.High, 0);
                p.debugView = CaptureDebugView.Overdraw;
                p.msaa = 1;
            });
            EnsureDebugMaterials();
            AssetDatabase.SaveAssets();
            return $"{count} capture profiles in {CaptureProfile.Folder}, DBG_LOD materials in {DebugFolder}";
        }

        private static void Icon(CaptureProfile p, CaptureShot shot, int size, float fov, float azimuth, float elevation, float padding)
        {
            p.shot = shot;
            p.width = p.height = size;
            p.supersample = 3;
            p.fieldOfView = fov;
            p.azimuth = azimuth;
            p.elevation = elevation;
            p.padding = padding;
            p.background = new Color(0f, 0f, 0f, 0f);
            p.transparent = true;
            p.tier = QualityTier.High;
            p.animals = 0;
            p.debugView = CaptureDebugView.None;
            p.msaa = 8;
        }

        private static void Frame(CaptureProfile p, CaptureShot shot, int width, int height, QualityTier tier, int animals)
        {
            p.shot = shot;
            p.width = width;
            p.height = height;
            p.supersample = 1;
            p.fieldOfView = 0f;
            p.padding = 0f;
            p.transparent = false;
            p.tier = tier;
            p.animals = animals;
            p.debugView = CaptureDebugView.None;
            p.msaa = 4;
        }

        /// <summary>DBG_LOD_Red/Green/Blue (R-DBG-LOD): flat URP Unlit colors, editor and development only.</summary>
        public static Material[] EnsureDebugMaterials()
        {
            Directory.CreateDirectory(DebugFolder);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            string[] names = { "DBG_LOD_Red", "DBG_LOD_Green", "DBG_LOD_Blue" };
            var materials = new Material[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string path = $"{DebugFolder}/{names[i]}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                }
                material.SetColor("_BaseColor", LodColors[i]);
                EditorUtility.SetDirty(material);
                materials[i] = material;
            }
            return materials;
        }

        // ---------------------------------------------------------------- captures

        [MenuItem("Pocket Wilds/Capture/Review Captures: LOD, Overdraw, Store (T34C)", priority = 401)]
        public static void ReviewMenu() => Debug.Log("[ReferenceCapture] " + CaptureAll(ReviewProfiles.Concat(StoreProfiles), CaptureUtility.DefaultFolder));

        [MenuItem("Pocket Wilds/Capture/Update Golden References (T34C)", priority = 402)]
        public static void GoldenMenu()
        {
            if (EditorUtility.DisplayDialog("Golden references", "Replace the golden reference images with the current renders?", "Replace", "Cancel"))
                Debug.Log("[ReferenceCapture] " + CaptureAll(GoldenProfiles, GoldenFolder));
        }

        public static string CaptureAll(IEnumerable<string> profiles, string folder)
        {
            var report = new StringBuilder();
            foreach (var name in profiles)
            {
                var profile = CaptureProfile.Load(name);
                report.Append(Capture(profile, $"{folder}/{profile.OutputName}.png")).Append("; ");
            }
            return report.ToString();
        }

        /// <summary>Renders one profile of the refuge (RefugeView, RefugeFar, HatchBurst) to <paramref name="path"/>.</summary>
        public static string Capture(CaptureProfile profile, string path)
        {
            if (profile.shot != CaptureShot.RefugeView && profile.shot != CaptureShot.RefugeFar && profile.shot != CaptureShot.HatchBurst)
                throw new NotSupportedException(profile.shot + " is captured by its own pipeline (AnimalThumbnailPipeline for icons).");
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != WorldCapture.ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var lifeKit = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(GrasslandPipeline.LifeKitPath);
            int level0 = QualitySettings.GetQualityLevel();
            var temp = new List<GameObject>();
            var swapped = new List<(Renderer renderer, Material[] materials)>();
            var transient = new List<Object>();
            bool fog0 = RenderSettings.fog;
            var wind0 = Shader.GetGlobalVector(WindId);
            var waves0 = Shader.GetGlobalVector(WindWavesId);
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var start = WorldCapture.SceneChunk();
                    var sceneEntry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var sun = Object.FindAnyObjectByType<Light>();
                    var camera = Camera.main;
                    var tier = profile.tier;
                    QualitySettings.SetQualityLevel((int)tier, true);
                    sceneEntry.Lighting.Apply(sun, camera, tier);
                    // The editor clock never stops: the wind is held still so two renders of a profile match.
                    var wind = Shader.GetGlobalVector(WindId);
                    var waves = Shader.GetGlobalVector(WindWavesId);
                    Shader.SetGlobalVector(WindId, new Vector4(wind.x, wind.y, wind.z, 0f));
                    Shader.SetGlobalVector(WindWavesId, new Vector4(waves.x, waves.y, 0f, 0f));
                    var quality = WorldQualityProfile.Default(tier);
                    var scatter = new ScatterRenderer(palettes, quality.scatterDensity);
                    scatter.SetDistanceFade(quality.scatterFadeStart, quality.scatterFadeEnd);
                    var life = lifeKit != null ? new AmbientLife(lifeKit, palettes, quality.ambientLifeDensity) : null;
                    start.Bind(palettes);
                    start.ApplyQuality(tier);
                    scatter.Add(start);
                    life?.Add(start);
                    for (int i = 0; i < 120; i++)
                        life?.Tick(1f / 30f, i / 30f);
                    WorldCapture.Populate(registry, palettes, start, profile.animals, temp);

                    if (profile.fieldOfView > 0f)
                        camera.fieldOfView = profile.fieldOfView;
                    if (profile.shot == CaptureShot.RefugeFar)
                    {
                        // The farthest zoom of the rig: LOD switches show here first.
                        var rig = Object.FindAnyObjectByType<RefugeCameraRig>();
                        var forward = camera.transform.forward;
                        var ground = new Plane(Vector3.up, start.EggSpawn.position);
                        if (ground.Raycast(new Ray(camera.transform.position, forward), out float hit))
                        {
                            var focus = camera.transform.position + forward * hit;
                            camera.transform.position = focus - forward * (rig != null ? rig.MaxDistance : 17f);
                        }
                    }
                    if (profile.shot == CaptureShot.HatchBurst)
                        SpawnBurst(start.EggSpawn.position, temp);
                    if (profile.debugView == CaptureDebugView.LodColors)
                        PaintLods(EnsureDebugMaterials(), swapped);
                    if (profile.debugView == CaptureDebugView.Overdraw)
                        return CaptureOverdraw(profile, camera, path, scatter, life, swapped, transient);
                    return CaptureUtility.RenderWithFreshCamera(camera, profile.width, profile.height, path, cam =>
                    {
                        scatter.Render(cam);
                        life?.Render(cam, 4f);
                    }, profile.msaa);
                }
                finally
                {
                    RenderSettings.fog = fog0;
                    Shader.SetGlobalVector(WindId, wind0);
                    Shader.SetGlobalVector(WindWavesId, waves0);
                    foreach (var (renderer, materials) in swapped)
                        if (renderer != null)
                            renderer.sharedMaterials = materials;
                    QualitySettings.SetQualityLevel(level0, true);
                    foreach (var go in temp)
                        Object.DestroyImmediate(go);
                    foreach (var item in transient)
                        Object.DestroyImmediate(item);
                    EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
                }
            }
        }

        // ---------------------------------------------------------------- overdraw

        /// <summary>One layer in the counting target: 1/32, so even an 8-bit intermediate target holds 31 layers.</summary>
        private const float OverdrawStep = 1f / 32f;

        /// <summary>Heat colors for 1, 2, ... 10 or more layers (0 stays dark: the sky, not counted).</summary>
        public static readonly Color[] OverdrawColors =
        {
            new Color(0.10f, 0.18f, 0.50f), new Color(0.10f, 0.42f, 0.85f), new Color(0.12f, 0.68f, 0.72f), new Color(0.30f, 0.80f, 0.32f),
            new Color(0.80f, 0.85f, 0.22f), new Color(0.97f, 0.66f, 0.16f), new Color(0.96f, 0.40f, 0.14f), new Color(0.90f, 0.14f, 0.12f),
            new Color(0.80f, 0.10f, 0.45f), new Color(1.00f, 0.85f, 0.95f),
        };

        /// <summary>
        /// Overdraw heat map (R-DBG-OVERDRAW): every renderer, the instanced scatter and life, and every particle drawn
        /// with an additive counting material (opaque: depth test and write, back faces culled, like the game opaque
        /// shaders; transparent: no depth write, both faces, like its effects), one step per layer, into a float target.
        /// Each pixel is then coloured by its count, with a legend strip at the bottom. The sky (one layer where
        /// nothing opaque hides it) is not counted. The URP rendering debugger needs DEBUG_DISPLAY in every shader and
        /// only colours the screen, so the game shaders never showed in it.
        /// </summary>
        private static string CaptureOverdraw(CaptureProfile profile, Camera camera, string path, ScatterRenderer scatter, AmbientLife life,
            List<(Renderer renderer, Material[] materials)> swapped, List<Object> transient)
        {
            var opaque = CountingMaterial("Overdraw_Opaque", true, transient);
            var transparent = CountingMaterial("Overdraw_Transparent", false, transient);
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var materials = renderer.sharedMaterials;
                if (!renderer.enabled || materials.Length == 0)
                    continue;
                swapped.Add((renderer, materials));
                renderer.sharedMaterials = materials.Select(m => IsTransparent(m) ? transparent : opaque).ToArray();
            }
            scatter.OverrideMaterial = opaque;
            if (life != null)
                life.OverrideMaterial = opaque;
            RenderSettings.fog = false;

            int width = profile.width, height = profile.height;
            var pixels = CaptureUtility.RenderLinear(camera, width, height, Color.clear, cam =>
            {
                scatter.Render(cam);
                life?.Render(cam, 4f);
            });

            var histogram = new int[32];
            var image = new Color32[pixels.Length];
            long layers = 0;
            int covered = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                int count = Mathf.Clamp(Mathf.RoundToInt(pixels[i].r / OverdrawStep), 0, histogram.Length - 1);
                histogram[count]++;
                layers += count;
                if (count > 0)
                    covered++;
                image[i] = count == 0 ? new Color32(14, 16, 24, 255) : (Color32)OverdrawColors[Mathf.Min(count, OverdrawColors.Length) - 1];
            }
            // Legend: one swatch per count, left to right (1 to 10 or more), along the bottom edge.
            int legend = Mathf.Max(12, height / 48);
            for (int y = 0; y < legend; y++)
            {
                for (int x = 0; x < width; x++)
                    image[y * width + x] = OverdrawColors[Mathf.Min(x * OverdrawColors.Length / width, OverdrawColors.Length - 1)];
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(image);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            float Share(int n) => covered > 0 ? histogram.Skip(n).Sum() / (float)covered : 0f;
            LastOverdraw = new OverdrawStats(covered > 0 ? (float)(layers / (double)covered) : 0f, covered / (float)pixels.Length,
                Share(4), Share(6), Share(8), Array.FindLastIndex(histogram, c => c > 0));
            return $"{path}: {LastOverdraw}";
        }

        public readonly struct OverdrawStats
        {
            public OverdrawStats(float meanLayers, float coveredShare, float share4, float share6, float share8, int max)
            {
                MeanLayers = meanLayers;
                CoveredShare = coveredShare;
                Share4 = share4;
                Share6 = share6;
                Share8 = share8;
                Max = max;
            }

            /// <summary>Mean layers on the pixels something covers (the sky is not counted).</summary>
            public float MeanLayers { get; }
            public float CoveredShare { get; }
            /// <summary>Share of covered pixels with at least 4, 6 and 8 layers.</summary>
            public float Share4 { get; }
            public float Share6 { get; }
            public float Share8 { get; }
            public int Max { get; }

            public override string ToString() =>
                $"overdraw mean {MeanLayers:F2} layers on covered pixels ({CoveredShare:P0} covered), >=4 {Share4:P1}, >=6 {Share6:P1}, >=8 {Share8:P1}, max {Max}";
        }

        /// <summary>Statistics of the last overdraw capture.</summary>
        public static OverdrawStats LastOverdraw { get; private set; }

        private static Material CountingMaterial(string name, bool opaque, List<Object> transient)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                enableInstancing = true,
                renderQueue = opaque ? (int)RenderQueue.Geometry : (int)RenderQueue.Transparent,
            };
            // Color properties are gamma: this reaches the shader as exactly one step in linear.
            material.SetColor("_BaseColor", new Color(OverdrawStep, OverdrawStep, OverdrawStep, 1f).gamma);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", opaque ? 1f : 0f);
            material.SetFloat("_Cull", (float)(opaque ? CullMode.Back : CullMode.Off));
            transient.Add(material);
            return material;
        }

        private static bool IsTransparent(Material material) =>
            material != null && (material.renderQueue >= (int)RenderQueue.GeometryLast || material.GetTag("RenderType", false) == "Transparent");

        /// <summary>Every feedback effect at once over the egg, simulated to their densest moment.</summary>
        private static void SpawnBurst(Vector3 egg, List<GameObject> temp)
        {
            var config = AssetDatabase.LoadAssetAtPath<HatchPresentationConfig>(EggPipeline.HatchConfigPath);
            int index = 0;
            foreach (var effect in config.vfx.effects)
            {
                if (effect.prefab == null)
                    continue;
                var system = Object.Instantiate(effect.prefab);
                system.transform.position = egg + new Vector3((index % 4 - 1.5f) * 0.25f, 0.05f, (index / 4) * 0.25f - 0.25f);
                system.Simulate(0.3f, true, true);
                temp.Add(system.gameObject);
                index++;
            }
        }

        /// <summary>LOD0 red, LOD1 green, LOD2 blue on every LODGroup in the open scenes (restored after the shot).</summary>
        private static void PaintLods(Material[] colors, List<(Renderer, Material[])> swapped)
        {
            foreach (var group in Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
            {
                var lods = group.GetLODs();
                for (int level = 0; level < lods.Length; level++)
                {
                    foreach (var renderer in lods[level].renderers)
                    {
                        if (renderer == null)
                            continue;
                        swapped.Add((renderer, renderer.sharedMaterials));
                        renderer.sharedMaterials = Enumerable.Repeat(colors[Mathf.Min(level, colors.Length - 1)], renderer.sharedMaterials.Length).ToArray();
                    }
                }
            }
        }

        // ---------------------------------------------------------------- golden comparison

        public readonly struct Comparison
        {
            public Comparison(float meanDifference, float p99Difference, float changedShare)
            {
                MeanDifference = meanDifference;
                P99Difference = p99Difference;
                ChangedShare = changedShare;
            }

            /// <summary>Mean absolute difference per channel (0-255).</summary>
            public float MeanDifference { get; }

            /// <summary>99th percentile of the per-pixel difference (largest channel, 0-255).</summary>
            public float P99Difference { get; }

            /// <summary>Share of pixels whose largest channel moved by more than 24.</summary>
            public float ChangedShare { get; }

            public override string ToString() => $"mean {MeanDifference:F2}, p99 {P99Difference:F0}, changed {ChangedShare:P2}";
        }

        /// <summary>Compares two PNGs of the same size; writes a difference image next to the current one.</summary>
        public static Comparison Compare(string goldenPath, string currentPath)
        {
            var golden = new Texture2D(2, 2);
            var current = new Texture2D(2, 2);
            try
            {
                golden.LoadImage(File.ReadAllBytes(goldenPath));
                current.LoadImage(File.ReadAllBytes(currentPath));
                if (golden.width != current.width || golden.height != current.height)
                    return new Comparison(255f, 255f, 1f);
                var a = golden.GetPixels32();
                var b = current.GetPixels32();
                var histogram = new int[256];
                double sum = 0.0;
                int changed = 0;
                var diff = new Color32[a.Length];
                for (int i = 0; i < a.Length; i++)
                {
                    int dr = Math.Abs(a[i].r - b[i].r), dg = Math.Abs(a[i].g - b[i].g), db = Math.Abs(a[i].b - b[i].b);
                    sum += dr + dg + db;
                    int max = Math.Max(dr, Math.Max(dg, db));
                    histogram[max]++;
                    if (max > 24)
                        changed++;
                    byte v = (byte)Math.Min(255, max * 4);
                    diff[i] = new Color32(v, v, v, 255);
                }
                int target = (int)(a.Length * 0.99), seen = 0, p99 = 0;
                for (int v = 0; v < 256; v++)
                {
                    seen += histogram[v];
                    if (seen >= target)
                    {
                        p99 = v;
                        break;
                    }
                }
                var image = new Texture2D(golden.width, golden.height, TextureFormat.RGBA32, false);
                image.SetPixels32(diff);
                File.WriteAllBytes(Path.ChangeExtension(currentPath, null) + "_diff.png", image.EncodeToPNG());
                Object.DestroyImmediate(image);
                return new Comparison((float)(sum / (a.Length * 3.0)), p99, changed / (float)a.Length);
            }
            finally
            {
                Object.DestroyImmediate(golden);
                Object.DestroyImmediate(current);
            }
        }
    }
}
