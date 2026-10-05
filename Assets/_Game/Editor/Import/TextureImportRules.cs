using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Import
{
    /// <summary>
    /// Texture import presets (Asset Production Bible). Mask textures are data, not color:
    /// linear, alpha kept as a channel, ASTC 4x4 on mobile so thin crack lines survive compression.
    /// UI sprites (UI_*, ICO_* under Art/UI): Sprite, no mipmaps, clamp, borders for 9-slices.
    /// </summary>
    public sealed class TextureImportRules : AssetPostprocessor
    {
        private const uint Version = 4;
        public const string UiRoot = "Assets/_Game/Art/UI/";
        private const string ArtRoot = "Assets/_Game/Art/";

        public override uint GetVersion() => Version;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot, StringComparison.Ordinal))
                return;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            if (assetPath.StartsWith(UiRoot, StringComparison.Ordinal)
                && (name.StartsWith("UI_", StringComparison.Ordinal) || name.StartsWith("ICO_", StringComparison.Ordinal)))
                ApplyUiSprite((TextureImporter)assetImporter, name);
            else if (name.StartsWith("T_", StringComparison.Ordinal) && name.Contains("Mask"))
                ApplyMask((TextureImporter)assetImporter, assetPath.StartsWith(AssetImportRules.EggsRoot, StringComparison.Ordinal));
            else if (name.StartsWith("T_VFX_", StringComparison.Ordinal))
                ApplyVfxAtlas((TextureImporter)assetImporter);
        }

        /// <summary>9-slice borders (left, bottom, right, top) of the kit sprites, matching pw_ui_sprites.py.</summary>
        public static Vector4 UiSpriteBorder(string name)
        {
            switch (name)
            {
                case "UI_Pill_9Slice": return new Vector4(64f, 70f, 64f, 62f);
                default: return Vector4.zero;
            }
        }

        /// <summary>
        /// UI sprite preset (UI doc, SPRITE IMPORT): 1 texel = 1 reference pixel (100 PPU with the HUD
        /// CanvasScaler), no mipmaps, full rect mesh so 9-slices and atlas packing stay predictable.
        /// Sources stay uncompressed: Atlas_CommonHUD packs them and owns the mobile compression (ASTC 4x4).
        /// </summary>
        public static void ApplyUiSprite(TextureImporter importer, string name)
        {
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
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spriteBorder = UiSpriteBorder(name);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var platformSettings = importer.GetPlatformTextureSettings(platform);
                platformSettings.overridden = false;
                importer.SetPlatformTextureSettings(platformSettings);
            }
        }

        /// <summary>Grayscale particle atlas: one channel, linear, R8 everywhere (a few KB).</summary>
        public static void ApplyVfxAtlas(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.SingleChannel;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
            importer.SetTextureSettings(settings);
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var platformSettings = importer.GetPlatformTextureSettings(platform);
                platformSettings.overridden = true;
                platformSettings.maxTextureSize = 128;
                platformSettings.format = TextureImporterFormat.R8;
                importer.SetPlatformTextureSettings(platformSettings);
            }
        }

        public static void ApplyMask(TextureImporter importer, bool eggLayout)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 1;
            // Egg UVs wrap around the shell (u) and stop at the poles (v).
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = eggLayout ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = 256;
                settings.format = TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformTextureSettings(settings);
            }
        }
    }
}
