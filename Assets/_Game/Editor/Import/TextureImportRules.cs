using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Import
{
    /// <summary>
    /// Texture import presets (Asset Production Guide). Mask textures are data, not color:
    /// linear, alpha kept as a channel, ASTC 4x4 on mobile so thin crack lines survive compression.
    /// UI sprites (UI_*, ICO_* under Art/UI): Sprite, no mipmaps, clamp, borders for 9-slices.
    /// </summary>
    public sealed class TextureImportRules : AssetPostprocessor
    {
        private const uint Version = 7;
        public const string UiRoot = "Assets/_Game/Art/UI/";
        private const string ArtRoot = "Assets/_Game/Art/";

        public override uint GetVersion() => Version;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot, StringComparison.Ordinal))
                return;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            if (assetPath.StartsWith(UiRoot, StringComparison.Ordinal) && name.StartsWith("TH_", StringComparison.Ordinal))
                ApplyThumbnail((TextureImporter)assetImporter);
            else if (assetPath.StartsWith(UiRoot, StringComparison.Ordinal)
                && (name.StartsWith("UI_", StringComparison.Ordinal) || name.StartsWith("ICO_", StringComparison.Ordinal)
                    // T44B: the profile frames' motifs (M_Daisy…) are UI sprites too.
                    || (name.StartsWith("M_", StringComparison.Ordinal) && assetPath.Contains("/Profile/Frames/"))))
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
                case "UI_Panel_Primary_9Slice": return new Vector4(64f, 74f, 64f, 62f);
                case "UI_Button_Primary_9Slice": return new Vector4(72f, 98f, 72f, 70f);
                // T42 (pw_ui_sprites_t42.py): the expeditions' slot cards and duration chips (Sliced), the postcard's
                // deckled frame (Tiled at 592 x 848: its 64 px middle tile and deckle repeat without a seam).
                case "UI_Exp_Slot_Empty":
                case "UI_Exp_Slot_Away":
                case "UI_Exp_Slot_Back":
                case "UI_Exp_Slot_Locked": return new Vector4(64f, 64f, 64f, 64f);
                case "UI_Exp_Chip":
                case "UI_Exp_Chip_Selected": return new Vector4(64f, 60f, 64f, 56f);
                case "UI_Exp_PostcardFrame": return new Vector4(72f, 72f, 72f, 72f);
                // T43 (pw_ui_sprites_t43.py): the garden's level pill (its badge and sprout in the left border), the label
                // over a bed and the seed sheet's card (its green foot band in the bottom border), all Sliced.
                case "UI_Garden_LevelPill": return new Vector4(128f, 40f, 56f, 52f);
                case "UI_Garden_BedLabel": return new Vector4(48f, 48f, 48f, 48f);
                case "UI_Garden_SeedPacketCard": return new Vector4(64f, 72f, 64f, 64f);
                // T44C (pw_ui_sprites_t44c.py): the journal's paper card (its tape in the top left border) and the month label.
                case "UI_Journal_PageCard": return new Vector4(48f, 48f, 48f, 48f);
                case "UI_Journal_MonthTab": return new Vector4(28f, 24f, 28f, 24f);
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
            // T49: an event's banner is no atlas sprite (one banner at a time, 688 px wide): it compresses itself (ASTC 6x6), listed in
            // Handmade_Sources largeTextures.
            bool banner = name.StartsWith("UI_EventBanner_", StringComparison.Ordinal);
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var platformSettings = importer.GetPlatformTextureSettings(platform);
                platformSettings.overridden = banner;
                if (banner)
                {
                    platformSettings.maxTextureSize = 1024;
                    platformSettings.format = TextureImporterFormat.ASTC_6x6;
                }
                importer.SetPlatformTextureSettings(platformSettings);
            }
        }

        /// <summary>
        /// Codex thumbnails (TH_*): sprites loaded on demand, never packed into a static atlas (UI doc
        /// ATLAS PLAN). ASTC 4x4 on mobile (64 KB at 256 px), no mipmaps.
        /// </summary>
        public static void ApplyThumbnail(TextureImporter importer)
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
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var platformSettings = importer.GetPlatformTextureSettings(platform);
                platformSettings.overridden = true;
                platformSettings.maxTextureSize = 256;
                platformSettings.format = TextureImporterFormat.ASTC_4x4;
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
