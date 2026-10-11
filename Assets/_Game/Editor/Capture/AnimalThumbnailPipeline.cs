using System;
using System.Collections.Generic;
using System.IO;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Import;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Capture
{
    /// <summary>
    /// Codex thumbnails (UI doc THUMBNAIL CACHE): every variant rendered once in the content pipeline with
    /// the real master shader, palettes and day lighting. Transparency comes from two renders (black and
    /// white backgrounds), which works whatever the pipeline does with alpha. All variants of a species
    /// share one crop around the silhouette so they line up in the Codex. The species silhouette comes
    /// from the capture alpha (UI doc SCREEN 007), never drawn by hand.
    /// Writes TH_*.png into Art/UI/Thumbnails/Animals and fills DEF_CodexVisuals.
    /// </summary>
    public static class AnimalThumbnailPipeline
    {
        public const string Folder = TextureImportRules.UiRoot + "Thumbnails/Animals";
        public const string CatalogPath = "Assets/_Game/Content/Visuals/DEF_CodexVisuals.asset";
        // T34C: the framing is data (Capture_AnimalIcon, M-DEF-CAPTURE), never hardcoded here.
        private static CaptureProfile _profile;
        private static CaptureProfile Profile => _profile != null ? _profile : _profile = CaptureProfile.Load(CaptureProfile.AnimalIcon);
        public static int Size => Profile.width;
        private static int RenderSize => Profile.width * Profile.supersample;
        private static float Margin => Profile.padding;
        private static float FieldOfView => Profile.fieldOfView;
        private static float Azimuth => Profile.azimuth;
        private static float Elevation => Profile.elevation;

        [MenuItem("Pocket Wilds/UI/Render Animal Thumbnails", priority = 210)]
        public static void Menu() => Debug.Log("[Thumbnails] " + RenderAll());

        public static string RenderAll()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            string returnScene = active.path;

            Directory.CreateDirectory(Folder);
            var written = new List<string>();
            int skipped = 0;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load after the scene switch: it can unload assets loaded before it.
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var visuals = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var lighting = AssetDatabase.LoadAssetAtPath<LightingProfile>("Assets/_Game/Art/Lighting/Grassland/LP_Grassland_Day.asset");
            var registry = database.CreateBuilder().Build();
            bool fog = RenderSettings.fog;
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false; // no placeholder shader in the first frames
            try
            {
                using (var palettes = new PaletteTable(registry))
                {
                    var resolver = new AnimalVisualResolver(registry, palettes);
                    var sun = new GameObject("Sun").AddComponent<Light>();
                    sun.type = LightType.Directional;
                    var camera = CreateCamera();
                    if (lighting != null)
                        lighting.Apply(sun, camera, QualityTier.High);
                    RenderSettings.fog = false;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    DynamicGI.UpdateEnvironment();
                    bool warmed = false;

                    foreach (var speciesAsset in database.species)
                    {
                        if (speciesAsset == null || speciesAsset.variants.Count == 0)
                            continue;
                        var species = speciesAsset.ToRuntime();
                        if (!visuals.TryGet(species.VisualKey, out var visual) || visual.prefab == null)
                        {
                            skipped++;
                            continue;
                        }
                        var view = Object.Instantiate(visual.prefab);
                        try
                        {
                            RectInt crop = default;
                            float[] silhouette = null;
                            for (int i = 0; i < speciesAsset.variants.Count; i++)
                            {
                                var variantEntry = speciesAsset.variants[i];
                                var animal = new AnimalInstance
                                {
                                    InstanceId = 1,
                                    Seed = 3u,
                                    Species = species.Id,
                                    Variant = ContentId.FromSource(variantEntry.sourceId),
                                };
                                resolver.TryResolve(animal, Vector3.zero, 0f, out var snapshot);
                                view.Configure(snapshot);
                                Prepare(view);
                                Frame(camera, view);
                                if (!warmed)
                                {
                                    // The first render after the scene switch still lacks the ambient and
                                    // shadow setup: its black/white pair disagrees. Render once, discard it.
                                    Capture(camera, out _, out _);
                                    warmed = true;
                                }
                                Capture(camera, out var premultiplied, out var alpha);
                                if (i == 0)
                                {
                                    crop = CropAround(alpha);
                                    silhouette = alpha;
                                }
                                string path = $"{Folder}/TH_{variantEntry.sourceId.Replace('.', '_')}.png";
                                WritePng(path, Resample(premultiplied, alpha, crop, false));
                                written.Add(path);
                            }
                            string silhouettePath = $"{Folder}/TH_{speciesAsset.sourceId.Replace('.', '_')}_Silhouette.png";
                            WritePng(silhouettePath, Resample(null, silhouette, crop, true));
                            written.Add(silhouettePath);
                        }
                        finally
                        {
                            Object.DestroyImmediate(view.gameObject);
                        }
                    }
                }
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                RenderSettings.fog = fog;
                if (!string.IsNullOrEmpty(returnScene))
                    EditorSceneManager.OpenScene(returnScene, OpenSceneMode.Single);
            }

            foreach (var path in written)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            FillCatalog(AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath));
            AssetDatabase.SaveAssets();
            return written.Count + " thumbnails, " + skipped + " species without a visual";
        }

        internal static Camera CreateCamera()
        {
            var go = new GameObject("ThumbnailCamera");
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = FieldOfView;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 30f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = true;
            return camera;
        }

        /// <summary>Highest detail, idle pose, no ground blob: the thumbnail floats on transparency.</summary>
        private static void Prepare(AnimalView view)
        {
            view.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (view.LodGroup != null)
                view.LodGroup.ForceLOD(0);
            foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.Contains("BlobShadow"))
                    renderer.enabled = false;
            }
            view.Animator.Rebind();
            view.Animator.Update(0.6f);
        }

        /// <summary>Three-quarter front view, slightly above. Generous on purpose: the crop tightens it.</summary>
        private static void Frame(Camera camera, AnimalView view)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in view.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer.name.Contains("LOD1") || renderer.name.Contains("LOD2"))
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
            float radius = bounds.extents.magnitude;
            float distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            var direction = Quaternion.Euler(-Elevation, Azimuth, 0f) * Vector3.forward;
            camera.transform.position = bounds.center + direction * distance;
            camera.transform.LookAt(bounds.center);
        }

        /// <summary>
        /// Two renders: on black, color = C x a; on white, color = C x a + (1 - a). So a = 1 - (white - black)
        /// and the black render is the premultiplied color. Linear-light floats, row-major from the bottom.
        /// </summary>
        internal static void Capture(Camera camera, out float[] premultiplied, out float[] alpha)
        {
            var black = RenderOnce(camera, Color.black);
            var white = RenderOnce(camera, Color.white);
            int count = RenderSize * RenderSize;
            premultiplied = new float[count * 3];
            alpha = new float[count];
            for (int i = 0; i < count; i++)
            {
                // The render target is sRGB: blending happened in linear space, so solve there.
                float br = Linear(black[i].r), bg = Linear(black[i].g), bb = Linear(black[i].b);
                float wr = Linear(white[i].r), wg = Linear(white[i].g), wb = Linear(white[i].b);
                float a = 1f - ((wr - br) + (wg - bg) + (wb - bb)) / 3f;
                alpha[i] = Mathf.Clamp01(a);
                premultiplied[i * 3] = br;
                premultiplied[i * 3 + 1] = bg;
                premultiplied[i * 3 + 2] = bb;
            }
        }

        private static Color32[] RenderOnce(Camera camera, Color background)
        {
            camera.backgroundColor = background;
            var rt = RenderTexture.GetTemporary(RenderSize, RenderSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 8);
            var previous = RenderTexture.active;
            var read = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, RenderSize, RenderSize), 0, 0);
                read.Apply();
                return read.GetPixels32();
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(read);
            }
        }

        /// <summary>Square around the visible pixels plus a small margin, inside the render.</summary>
        internal static RectInt CropAround(float[] alpha)
        {
            int minX = RenderSize, minY = RenderSize, maxX = -1, maxY = -1;
            for (int y = 0; y < RenderSize; y++)
            {
                for (int x = 0; x < RenderSize; x++)
                {
                    if (alpha[y * RenderSize + x] < 0.02f)
                        continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0)
                return new RectInt(0, 0, RenderSize, RenderSize);
            int side = Mathf.CeilToInt(Mathf.Max(maxX - minX + 1, maxY - minY + 1) * (1f + 2f * Margin));
            side = Mathf.Min(side, RenderSize);
            int cx = (minX + maxX) / 2;
            int cy = (minY + maxY) / 2;
            int x0 = Mathf.Clamp(cx - side / 2, 0, RenderSize - side);
            int y0 = Mathf.Clamp(cy - side / 2, 0, RenderSize - side);
            return new RectInt(x0, y0, side, side);
        }

        /// <summary>Area-averaged downscale of the crop in premultiplied space, then straight alpha.</summary>
        internal static Texture2D Resample(float[] premultiplied, float[] alpha, RectInt crop, bool silhouette)
        {
            var pixels = new Color32[Size * Size];
            float scale = crop.width / (float)Size;
            for (int y = 0; y < Size; y++)
            {
                int sy0 = crop.y + Mathf.FloorToInt(y * scale);
                int sy1 = Mathf.Max(sy0 + 1, crop.y + Mathf.FloorToInt((y + 1) * scale));
                for (int x = 0; x < Size; x++)
                {
                    int sx0 = crop.x + Mathf.FloorToInt(x * scale);
                    int sx1 = Mathf.Max(sx0 + 1, crop.x + Mathf.FloorToInt((x + 1) * scale));
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    int n = 0;
                    for (int sy = sy0; sy < sy1; sy++)
                    {
                        for (int sx = sx0; sx < sx1; sx++)
                        {
                            int i = sy * RenderSize + sx;
                            a += alpha[i];
                            if (!silhouette)
                            {
                                r += premultiplied[i * 3];
                                g += premultiplied[i * 3 + 1];
                                b += premultiplied[i * 3 + 2];
                            }
                            n++;
                        }
                    }
                    a /= n;
                    if (silhouette)
                    {
                        pixels[y * Size + x] = new Color32(255, 255, 255, ToByte(a));
                        continue;
                    }
                    if (a < 0.004f)
                    {
                        pixels[y * Size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    pixels[y * Size + x] = new Color32(ToGamma(r / n / a), ToGamma(g / n / a), ToGamma(b / n / a), ToByte(a));
                }
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static byte ToByte(float value) => (byte)(Mathf.Clamp01(value) * 255f + 0.5f);

        private static float Linear(byte value) => Mathf.GammaToLinearSpace(value / 255f);

        private static byte ToGamma(float linear) => ToByte(Mathf.LinearToGammaSpace(Mathf.Clamp01(linear)));

        internal static void WritePng(string path, Texture2D texture)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static void FillCatalog(ContentDatabaseAsset database)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CodexVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CodexVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            foreach (var species in database.species)
            {
                if (species == null)
                    continue;
                var silhouette = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/TH_{species.sourceId.Replace('.', '_')}_Silhouette.png");
                if (silhouette != null)
                    catalog.Set(catalog.silhouettes, species.sourceId, silhouette);
                foreach (var variant in species.variants)
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/TH_{variant.sourceId.Replace('.', '_')}.png");
                    if (sprite != null)
                        catalog.Set(catalog.variants, variant.sourceId, sprite);
                }
            }
            EditorUtility.SetDirty(catalog);
        }
    }
}
