using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Import
{
    /// <summary>
    /// Texture import presets (Asset Production Bible). Mask textures are data, not color:
    /// linear, alpha kept as a channel, ASTC 4x4 on mobile so thin crack lines survive compression.
    /// </summary>
    public sealed class TextureImportRules : AssetPostprocessor
    {
        private const uint Version = 1;
        private const string ArtRoot = "Assets/_Game/Art/";

        public override uint GetVersion() => Version;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot, StringComparison.Ordinal))
                return;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            if (name.StartsWith("T_", StringComparison.Ordinal) && name.Contains("Mask"))
                ApplyMask((TextureImporter)assetImporter, assetPath.StartsWith(AssetImportRules.EggsRoot, StringComparison.Ordinal));
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
