using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using PocketWilds.Editor.Capture;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// Shipped fonts (doc 05): imports the TTF files built by SourceArt/Fonts/Tools/pw_font_build.py and makes their
    /// TextMeshPro assets. Every weight gets one static 1024 x 1024 SDF atlas with the whole character set, sampled at
    /// the largest size that fits, so the game never generates glyphs while it runs. SemiBold and Bold are linked to
    /// Regular through the weight table, so bold text uses the real Bold and never a faux bold.
    /// A rebuild updates the existing assets in place: prefabs keep their references.
    /// </summary>
    public static class FontPipeline
    {
        public const string SourceFolder = "SourceArt/Fonts/Build";
        public const string Folder = "Assets/_Game/Art/UI/Fonts";
        public const string Round = "PocketRound";
        public const string Plush = "PocketPlush";
        public const int AtlasSize = 1024;
        public const string MobileShader = "TextMeshPro/Mobile/Distance Field";
        public static readonly string[] RoundWeights = { "Regular", "SemiBold", "Bold" };

        public static string AssetPath(string family, string weight) => $"{Folder}/{family}/{family}-{weight} SDF.asset";

        public static TMP_FontAsset Load(string family, string weight) => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath(family, weight));

        /// <summary>Every character the fonts ship (written by pw_font_build.py next to the TTF files).</summary>
        public static string Charset()
        {
            string root = Path.GetDirectoryName(Application.dataPath)!;
            return File.ReadAllText(Path.Combine(root, SourceFolder, "charset.txt"), Encoding.UTF8);
        }

        [MenuItem("Pocket Wilds/UI/Fonts/Build Pocket Round", priority = 230)]
        public static void BuildRoundMenu() => Debug.Log("[FontPipeline] " + Build(Round, RoundWeights));

        public static string Build(string family, string[] weights)
        {
            string root = Path.GetDirectoryName(Application.dataPath)!;
            string folder = $"{Folder}/{family}";
            Directory.CreateDirectory(Path.Combine(root, folder));
            string charset = Charset();
            var report = new List<string>();
            foreach (string weight in weights)
            {
                string ttf = $"{folder}/{family}-{weight}.ttf";
                File.Copy(Path.Combine(root, SourceFolder, $"{family}-{weight}.ttf"), Path.Combine(root, ttf), true);
                AssetDatabase.ImportAsset(ttf, ImportAssetOptions.ForceUpdate);
                var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
                string kern = Path.Combine(root, SourceFolder, $"{family}-{weight}.kern.tsv");
                report.Add(CreateOrUpdate(font, AssetPath(family, weight), charset, kern));
            }
            if (weights.Length > 1)
                LinkWeights(family);
            AssetDatabase.SaveAssets();
            return string.Join("; ", report);
        }

        private static int Padding(int samplingSize) => Mathf.Max(5, Mathf.RoundToInt(samplingSize * 0.1f));

        private static TMP_FontAsset Make(Font font, int samplingSize, string charset, out bool complete)
        {
            // FontEngine keeps font faces (and their layout tables) after a TTF is reimported: start clean every time.
            FontEngine.UnloadAllFontFaces();
            var asset = TMP_FontAsset.CreateFontAsset(font, samplingSize, Padding(samplingSize), GlyphRenderMode.SDFAA,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
            complete = asset.TryAddCharacters(charset, out string missing) && string.IsNullOrEmpty(missing);
            return asset;
        }

        /// <summary>
        /// Kerning from the .kern.tsv file written beside the TTF by pw_font_build.py. TextCore's own GPOS import reads
        /// cached tables after a reimport (it gave one weight's pairs to the others), so the pairs come from the source.
        /// </summary>
        private static int ApplyKerning(TMP_FontAsset asset, string kernPath)
        {
            var records = asset.fontFeatureTable.glyphPairAdjustmentRecords;
            records.Clear();
            float scale = asset.faceInfo.pointSize / 1000f; // pw_font_build.py draws at 1000 units per em
            var lookup = asset.characterLookupTable;
            foreach (string line in File.ReadAllLines(kernPath))
            {
                if (line.Length == 0 || line[0] == '#')
                    continue;
                string[] parts = line.Split('\t');
                uint left = System.Convert.ToUInt32(parts[0], 16);
                uint right = System.Convert.ToUInt32(parts[1], 16);
                float value = float.Parse(parts[2], CultureInfo.InvariantCulture) * scale;
                if (!lookup.TryGetValue(left, out var first) || !lookup.TryGetValue(right, out var second))
                    throw new System.InvalidOperationException($"{asset.name}: kerning pair {parts[0]} {parts[1]} has no glyph.");
                records.Add(new GlyphPairAdjustmentRecord(
                    new GlyphAdjustmentRecord(first.glyphIndex, new GlyphValueRecord(0f, 0f, value, 0f)),
                    new GlyphAdjustmentRecord(second.glyphIndex, new GlyphValueRecord(0f, 0f, 0f, 0f))));
            }
            return records.Count;
        }

        private static void Destroy(TMP_FontAsset asset)
        {
            foreach (var texture in asset.atlasTextures)
                Object.DestroyImmediate(texture);
            Object.DestroyImmediate(asset.material);
            Object.DestroyImmediate(asset);
        }

        /// <summary>Largest sampling size (even, 24 to 80) whose whole character set fits one atlas.</summary>
        private static int FittingSize(Font font, string charset)
        {
            for (int size = 80; size >= 24; size -= 2)
            {
                var probe = Make(font, size, charset, out bool complete);
                Destroy(probe);
                if (complete)
                    return size;
            }
            throw new System.InvalidOperationException(font.name + ": the character set does not fit a " + AtlasSize + " atlas.");
        }

        private static string CreateOrUpdate(Font font, string path, string charset, string kernPath)
        {
            int size = FittingSize(font, charset);
            var fresh = Make(font, size, charset, out _);
            ApplyKerning(fresh, kernPath);
            string name = Path.GetFileNameWithoutExtension(path);
            fresh.name = name;
            fresh.atlasPopulationMode = AtlasPopulationMode.Static;
            fresh.boldSpacing = 0f;
            var shader = Shader.Find(MobileShader);
            if (shader != null)
                fresh.material.shader = shader;

            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                fresh.atlasTextures[0].name = name + " Atlas";
                AssetDatabase.AddObjectToAsset(fresh.atlasTextures[0], fresh);
                fresh.material.name = name + " Material";
                AssetDatabase.AddObjectToAsset(fresh.material, fresh);
                asset = fresh;
            }
            else
            {
                // Same asset, texture and material objects: only their content changes. LinkWeights relinks the weights.
                var texture = asset.atlasTextures[0];
                var material = asset.material;
                var freshTexture = fresh.atlasTextures[0];
                EditorUtility.CopySerialized(fresh, asset);
                SetReadable(texture, true);
                texture.Reinitialize(freshTexture.width, freshTexture.height, freshTexture.format, false);
                texture.SetPixelData(freshTexture.GetRawTextureData<byte>(), 0);
                texture.Apply(false, false);
                material.shader = fresh.material.shader;
                material.CopyPropertiesFromMaterial(fresh.material);
                material.SetTexture(ShaderUtilities.ID_MainTex, texture);
                asset.atlasTextures = new[] { texture };
                asset.material = material;
                asset.name = name;
                Destroy(fresh);
                EditorUtility.SetDirty(texture);
                EditorUtility.SetDirty(material);
            }
            // A static atlas is never written at run time: no CPU copy in builds, like the TMP Essentials atlas.
            SetReadable(asset.atlasTextures[0], false);
            asset.ReadFontAssetDefinition();
            EditorUtility.SetDirty(asset);
            int pairs = asset.fontFeatureTable.glyphPairAdjustmentRecords.Count;
            return $"{name}: {asset.characterTable.Count} characters, sampled at {size} px, {pairs} kerning pairs";
        }

        private static void SetReadable(Texture2D texture, bool readable)
        {
            var serialized = new SerializedObject(texture);
            serialized.FindProperty("m_IsReadable").boolValue = readable;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void LinkWeights(string family)
        {
            var regular = Load(family, "Regular");
            regular.fontWeightTable[6].regularTypeface = Load(family, "SemiBold");
            regular.fontWeightTable[7].regularTypeface = Load(family, "Bold");
            EditorUtility.SetDirty(regular);
        }

        // ---------------- readability proof ----------------

        /// <summary>Smallest target phone of the Performance Bible: 720 x 1520 at about 2 px per dp.</summary>
        public const int ProofWidth = 720;
        public const int ProofHeight = 1520;
        public const float PixelsPerDp = 2f;

        [MenuItem("Pocket Wilds/UI/Fonts/Readability Capture", priority = 231)]
        public static void ReadabilityMenu() => Debug.Log("[FontPipeline] " + CaptureReadability("T31"));

        /// <summary>
        /// Pocket Round at 12 to 32 dp in its three weights, on the cream panel, on the dark panel and on the meadow
        /// green, rendered at the smallest target resolution. Built in a temporary scene that is closed unsaved.
        /// </summary>
        public static string CaptureReadability(string prefix)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var cameraObject = new GameObject("ProofCamera");
                MoveToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = ProofHeight / 2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.98f, 0.96f, 0.92f);
                camera.cullingMask = 1 << 5;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                cameraObject.transform.position = new Vector3(20000f, 0f, -10f);

                var canvasObject = new GameObject("ProofCanvas", typeof(RectTransform));
                MoveToScene(canvasObject, scene);
                canvasObject.layer = 5;
                var canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)canvasObject.transform;
                rect.sizeDelta = new Vector2(ProofWidth, ProofHeight);
                rect.position = new Vector3(20000f, 0f, 0f);

                Panel(rect, 0, 0.42f, new Color(1f, 0.98f, 0.94f), new Color(0.24f, 0.17f, 0.12f));
                Panel(rect, 0.42f, 0.71f, new Color(0.24f, 0.17f, 0.12f), new Color(1f, 0.97f, 0.9f));
                Panel(rect, 0.71f, 1f, new Color(0.45f, 0.66f, 0.34f), Color.white);
                Canvas.ForceUpdateCanvases();
                string path = $"{CaptureUtility.DefaultFolder}/{prefix}_font_readability.png";
                return CaptureUtility.Render(camera, ProofWidth, ProofHeight, path, 4, Canvas.ForceUpdateCanvases);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void MoveToScene(GameObject go, UnityEngine.SceneManagement.Scene scene) =>
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

        private static void Panel(RectTransform canvas, float top, float bottom, Color background, Color ink)
        {
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.layer = 5;
            var rect = (RectTransform)panel.transform;
            rect.SetParent(canvas, false);
            rect.anchorMin = new Vector2(0f, 1f - bottom);
            rect.anchorMax = new Vector2(1f, 1f - top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = background;

            var rows = new (float dp, string weight, string text)[]
            {
                (12, "Regular", "12 dp · Le lapin roux s'endort près des fleurs."),
                (14, "Regular", "14 dp · Nouvelle espèce ! Écureuil roux, 1,99 €"),
                (14, "SemiBold", "14 dp · Souhaits du jour 2/3 · +24,8 K"),
                (16, "Regular", "16 dp · Fais éclore 3 œufs dans la prairie"),
                (20, "Bold", "20 dp · Récolter · Pack de la Prairie"),
                (24, "Bold", "24 dp · Bon retour ! 12 400"),
                (32, "Bold", "32 dp · Hérisson"),
            };
            float y = -14f;
            foreach (var row in rows)
            {
                var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.layer = 5;
                var labelRect = (RectTransform)label.transform;
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = new Vector2(0f, 1f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.pivot = new Vector2(0f, 1f);
                float size = row.dp * PixelsPerDp;
                labelRect.offsetMin = new Vector2(24f, y - size * 1.4f);
                labelRect.offsetMax = new Vector2(-24f, y);
                var text = label.GetComponent<TextMeshProUGUI>();
                text.font = Load(Round, row.weight);
                text.fontSize = size;
                text.color = ink;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.text = row.text;
                y -= size * 1.4f + 8f;
            }
        }
    }
}
