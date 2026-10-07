using System.Collections.Generic;
using System.IO;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.World;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Capture
{
    public static partial class WorldCapture
    {
        public const string ZoneCaptureFolder = "Assets/_Game/Art/UI/Zones";
        public const int ZoneCaptureWidth = 740;
        public const int ZoneCaptureHeight = 356;
        private const int ZoneCaptureCorner = 30;

        /// <summary>Address of a zone's capture: the zone window loads only the next zone's (RefugeZones).</summary>
        public static string ZoneCaptureAddress(string chunkAddress) => "zones.capture." + chunkAddress.Substring("chunk.".Length);

        [MenuItem("Pocket Wilds/Capture/Zone Captures (T35A)", priority = 529)]
        public static void CaptureZonesMenu() => Debug.Log("[WorldCapture] " + CaptureZones());

        /// <summary>
        /// T35A (doc 02 Améliorations, section 11.3): one picture per Prairie zone for its Open window, rendered here
        /// with the game's light, ground, plants and life (never painted). The whole strip is spawned so a zone's edges
        /// meet their neighbours; the camera keeps the game's pitch and looks at the zone from a little higher. Rendered
        /// at twice the size, box-filtered down, corners rounded in the alpha. Saved as CAP_Zone_* sprites (ASTC 6x6, no
        /// mipmaps, outside the HUD atlas) and made Addressable in the zones group, one address each.
        /// </summary>
        public static string CaptureZones(QualityTier tier = QualityTier.High)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var lifeKit = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(GrasslandPipeline.LifeKitPath);
            int level0 = QualitySettings.GetQualityLevel();
            var written = new List<string>();
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var start = SceneChunk();
                    var chunks = new List<WorldChunk> { start };
                    var byAddress = new Dictionary<string, WorldChunk>();
                    foreach (var entry in WorldContentPipeline.GrasslandChunks)
                    {
                        if (entry.pinned)
                            continue;
                        var chunk = WorldContentPipeline.SpawnForTools(entry.address);
                        chunks.Add(chunk);
                        byAddress[entry.address] = chunk;
                    }
                    var sceneEntry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var sun = Object.FindAnyObjectByType<Light>();
                    var camera = Camera.main;
                    QualitySettings.SetQualityLevel((int)tier, true);
                    sceneEntry.Lighting.Apply(sun, camera, tier);
                    var quality = WorldQualityProfile.Default(tier);
                    var scatter = new ScatterRenderer(palettes, quality.scatterDensity);
                    scatter.SetDistanceFade(0f, 0f);
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
                    // A picture, not a game frame: no distance fog over the zone.
                    RenderSettings.fog = false;
                    camera.farClipPlane = 200f;
                    camera.fieldOfView = 30f;
                    var forward = camera.transform.forward;

                    Directory.CreateDirectory(ZoneCaptureFolder);
                    var config = new PocketWilds.Simulation.ProgressionConfig();
                    foreach (var zone in config.Zones)
                    {
                        if (zone.Role != PocketWilds.Simulation.ZoneRole.Expansion || !byAddress.TryGetValue(zone.ChunkAddress, out var chunk))
                            continue;
                        // Tall landmarks (the hill's tree, the windmill) are framed a little further back so they stay whole.
                        float back = zone.ChunkAddress.EndsWith("windmillrise", System.StringComparison.Ordinal) ? 3.2f
                            : zone.ChunkAddress.EndsWith("hill", System.StringComparison.Ordinal) ? 2.2f : 0.8f;
                        var focus = chunk.transform.position + new Vector3(0f, 0.3f, back);
                        camera.transform.position = focus - forward * 15f;
                        camera.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                        string raw = $"{CaptureUtility.DefaultFolder}/T35A_zone_{chunk.name}.png";
                        CaptureUtility.RenderWithFreshCamera(camera, ZoneCaptureWidth * 2, ZoneCaptureHeight * 2, raw, cam =>
                        {
                            scatter.Render(cam);
                            life?.Render(cam, 4f);
                        });
                        string name = "CAP_Zone_" + zone.ChunkAddress.Substring(zone.ChunkAddress.LastIndexOf('.') + 1);
                        written.Add(WriteZoneCapture(raw, $"{ZoneCaptureFolder}/{name}.png"));
                    }
                }
                finally
                {
                    QualitySettings.SetQualityLevel(level0, true);
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }
            AssetDatabase.Refresh();
            foreach (var path in written)
                ImportZoneCapture(path);
            AssetDatabase.SaveAssets();
            return $"{written.Count} zone captures: " + string.Join(", ", written);
        }

        /// <summary>Box-filters the 2x render down and rounds the corners in the alpha.</summary>
        private static string WriteZoneCapture(string rawPath, string path)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var result = new Texture2D(ZoneCaptureWidth, ZoneCaptureHeight, TextureFormat.RGBA32, false);
            try
            {
                source.LoadImage(File.ReadAllBytes(rawPath));
                var pixels = source.GetPixels32();
                int sw = source.width;
                var output = new Color32[ZoneCaptureWidth * ZoneCaptureHeight];
                for (int y = 0; y < ZoneCaptureHeight; y++)
                {
                    for (int x = 0; x < ZoneCaptureWidth; x++)
                    {
                        int r = 0, g = 0, b = 0;
                        for (int dy = 0; dy < 2; dy++)
                        {
                            for (int dx = 0; dx < 2; dx++)
                            {
                                var p = pixels[(y * 2 + dy) * sw + x * 2 + dx];
                                r += p.r;
                                g += p.g;
                                b += p.b;
                            }
                        }
                        output[y * ZoneCaptureWidth + x] = new Color32((byte)(r / 4), (byte)(g / 4), (byte)(b / 4), CornerAlpha(x, y));
                    }
                }
                result.SetPixels32(output);
                result.Apply();
                File.WriteAllBytes(path, result.EncodeToPNG());
                return path;
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(result);
            }
        }

        /// <summary>Coverage of a rounded rectangle at a pixel center, one pixel of smooth edge.</summary>
        private static byte CornerAlpha(int x, int y)
        {
            float cx = Mathf.Clamp(x + 0.5f, ZoneCaptureCorner, ZoneCaptureWidth - ZoneCaptureCorner);
            float cy = Mathf.Clamp(y + 0.5f, ZoneCaptureCorner, ZoneCaptureHeight - ZoneCaptureCorner);
            float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) - ZoneCaptureCorner;
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f);
        }

        /// <summary>Sprite, no mipmaps, clamp, ASTC 6x6 on mobile; Addressable beside the zone kit.</summary>
        private static void ImportZoneCapture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 1024;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = 1024;
                settings.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();

            var addressables = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = addressables.FindGroup(ZonePipeline.Group);
            if (group == null)
                throw new System.InvalidOperationException("Build the zone kit first: " + ZonePipeline.Group);
            string chunk = "chunk.grassland." + Path.GetFileNameWithoutExtension(path).Substring("CAP_Zone_".Length);
            var entry = addressables.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group, false, false);
            entry.address = ZoneCaptureAddress(chunk);
            entry.SetLabel(WorldContentPipeline.BiomeLabel, true, true, false);
            EditorUtility.SetDirty(group);
        }
    }
}
