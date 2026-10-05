using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PocketWilds.Editor.Capture;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T31 font review: imports the candidate fonts built by SourceArt/Fonts/Tools/pw_font_build.py, makes their
    /// TextMeshPro assets (static SDF atlas with the whole shipped character set, Bold and SemiBold linked through the
    /// weight table) and captures the real screens with every interface and title combination for the owner's choice.
    /// Captures swap fonts in memory only (CaptureUtility.FontOverride); no prefab changes.
    /// </summary>
    public static class FontReview
    {
        public const string SourceFolder = "SourceArt/Fonts/Build";
        public const string AssetFolder = "Assets/_Game/Art/UI/Fonts/Candidates";
        public static readonly string[] Interface = { "PWRoundDouce", "PWRoundClaire", "PWRoundCaline" };
        public static readonly string[] Titles = { "PWPlushBulle", "PWPlushGuimauve" };

        [MenuItem("Pocket Wilds/UI/Font Review (T31)/Import Candidates", priority = 240)]
        public static void ImportMenu() => Debug.Log("[FontReview] " + Import());

        public static string Import()
        {
            string root = Path.GetDirectoryName(Application.dataPath)!;
            string charset = File.ReadAllText(Path.Combine(root, SourceFolder, "charset.txt"), Encoding.UTF8);
            Directory.CreateDirectory(Path.Combine(root, AssetFolder));
            var copied = new List<string>();
            foreach (var ttf in Directory.GetFiles(Path.Combine(root, SourceFolder), "*.ttf"))
            {
                string dest = $"{AssetFolder}/{Path.GetFileName(ttf)}";
                File.Copy(ttf, Path.Combine(root, dest), true);
                copied.Add(dest);
            }
            AssetDatabase.Refresh();
            foreach (var path in copied)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var made = new List<string>();
            foreach (var path in copied)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                string assetPath = path.Replace(".ttf", " SDF.asset");
                made.Add(CreateFontAsset(font, assetPath, charset, out var missing) + (missing.Length > 0 ? $" (missing {missing.Length})" : ""));
            }
            foreach (var family in Interface)
                LinkWeights(family);
            AssetDatabase.SaveAssets();
            return string.Join(", ", made);
        }

        private static string CreateFontAsset(Font font, string assetPath, string charset, out string missing)
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);
            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);
            asset.atlasTextures[0].name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.TryAddCharacters(charset, out missing);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            // Real Bold and SemiBold exist: no extra spacing for bold text.
            asset.boldSpacing = 0f;
            EditorUtility.SetDirty(asset);
            return asset.name;
        }

        private static void LinkWeights(string family)
        {
            var regular = Load(family + "-Regular");
            if (regular == null)
                return;
            regular.fontWeightTable[6].regularTypeface = Load(family + "-SemiBold");
            regular.fontWeightTable[7].regularTypeface = Load(family + "-Bold");
            EditorUtility.SetDirty(regular);
        }

        /// <summary>Width of the TEMP font divided by the candidate's width on the same UI sample (pw_font_build measure).</summary>
        public static readonly Dictionary<string, float> WidthRatio = new Dictionary<string, float>
        {
            ["PWRoundDouce-Regular"] = 0.973f, ["PWRoundClaire-Regular"] = 1.025f, ["PWRoundCaline-Regular"] = 0.917f,
            ["PWRoundDouce-Bold"] = 0.917f, ["PWRoundClaire-Bold"] = 0.962f, ["PWRoundCaline-Bold"] = 0.855f,
            ["PWPlushBulle-Black"] = 0.884f, ["PWPlushGuimauve-Black"] = 0.835f,
        };

        public static TMP_FontAsset Load(string name) => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{AssetFolder}/{name} SDF.asset");

        /// <summary>One combination: interface font (null = current TEMP font) and title font (null = interface Bold).</summary>
        public static string Capture(string ui, string title)
        {
            string prefix = $"T31_{Short(ui)}_{Short(title)}";
            try
            {
                string titleName = title == null ? (ui == null ? null : ui + "-Bold") : title + "-Black";
                CaptureUtility.FontOverride = ui == null ? null : Load(ui + "-Regular");
                CaptureUtility.TitleFontOverride = titleName == null ? null : Load(titleName);
                CaptureUtility.FontScale = ui == null ? 1f : WidthRatio[ui + "-Regular"];
                CaptureUtility.TitleScale = titleName == null ? 1f : WidthRatio[titleName];
                string hud = HudCapture.Capture(prefix, 6);
                string shop = ShopCapture.Capture(prefix);
                return prefix + ": " + (hud.Split(',').Length + shop.Split(',').Length) + " frames";
            }
            finally
            {
                CaptureUtility.FontOverride = null;
                CaptureUtility.TitleFontOverride = null;
                CaptureUtility.FontScale = 1f;
                CaptureUtility.TitleScale = 1f;
            }
        }

        public static string Short(string family) => family == null ? "Base" : family.Replace("PWRound", "").Replace("PWPlush", "");
    }
}
