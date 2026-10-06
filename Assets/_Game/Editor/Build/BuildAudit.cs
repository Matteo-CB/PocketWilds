using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.Build.Reporting;

namespace PocketWilds.Editor.Build
{
    /// <summary>
    /// T34C gate 33 build content audit: what a build really ships, by category and largest first. Two sources: the
    /// player build report (scenes and what they pull in) and the Addressables build layout (every bundle and the
    /// assets packed in it). Sizes are the packed sizes on the target (serialized data plus streamed data).
    /// </summary>
    public static class BuildAudit
    {
        public const string LayoutPath = "Library/com.unity.addressables/buildlayout.json";

        private readonly struct Entry
        {
            public Entry(string category, string path, ulong bytes, string where)
            {
                Category = category;
                Path = path;
                Bytes = bytes;
                Where = where;
            }

            public string Category { get; }
            public string Path { get; }
            public ulong Bytes { get; }
            public string Where { get; }
        }

        /// <summary>Writes the audit as Markdown and returns a one-line summary.</summary>
        public static string Write(BuildReport report, string title, string markdownPath, int topPerCategory = 8)
        {
            var entries = new List<Entry>();
            foreach (var packed in report.packedAssets)
                foreach (var info in packed.contents)
                    entries.Add(new Entry(Category(MainType(info.sourceAssetPath) ?? (info.type != null ? info.type.Name : string.Empty), info.sourceAssetPath),
                        info.sourceAssetPath, info.packedSize, "player"));

            var bundles = new List<(string Group, string Bundle, ulong Compressed, ulong Uncompressed, string Compression)>();
            var layout = File.Exists(LayoutPath) ? BuildLayout.Open(LayoutPath, true, true) : null;
            if (layout != null)
            {
                foreach (var group in layout.Groups)
                {
                    foreach (var bundle in group.Bundles)
                    {
                        bundles.Add((group.Name, bundle.Name, bundle.FileSize, bundle.UncompressedFileSize, bundle.Compression));
                        foreach (var file in bundle.Files)
                        {
                            foreach (var asset in file.Assets)
                                entries.Add(new Entry(Category(MainType(asset.AssetPath) ?? asset.MainAssetType.ToString(), asset.AssetPath),
                                    asset.AssetPath, asset.SerializedSize + asset.StreamedSize, group.Name));
                            foreach (var other in file.OtherAssets)
                                entries.Add(new Entry(Category(MainType(other.AssetPath) ?? other.MainAssetType.ToString(), other.AssetPath),
                                    other.AssetPath, other.SerializedSize + other.StreamedSize, group.Name + " (dependency)"));
                        }
                    }
                }
            }

            // One line per asset and place: the player report lists an asset once per object it packs.
            var merged = entries.GroupBy(e => (e.Path, e.Where))
                .Select(g => new Entry(g.First().Category, g.Key.Path, (ulong)g.Sum(e => (decimal)e.Bytes), g.Key.Where))
                .ToList();
            var summary = report.summary;
            // The report total counts the whole exported project on Android: the shipped file is what a player downloads.
            string output = File.Exists(summary.outputPath) ? $"{Path.GetFileName(summary.outputPath)} {Mb((ulong)new FileInfo(summary.outputPath).Length)}" : Mb(summary.totalSize) + " au total";
            var text = new StringBuilder();
            text.Append("# ").Append(title).Append("\n\n");
            text.Append($"- Cible : {summary.platform}, {((summary.options & UnityEditor.BuildOptions.Development) != 0 ? "développement" : "release")}\n");
            text.Append($"- Résultat : {summary.result}, {output}, {summary.totalTime.TotalSeconds:F0} s, {summary.totalErrors} erreurs, {summary.totalWarnings} avertissements\n");
            text.Append($"- Contenu packé : {Mb(Sum(merged))} (player {Mb(Sum(merged.Where(e => e.Where == "player")))}, bundles {Mb(Sum(merged.Where(e => e.Where != "player")))})\n\n");

            text.Append("## Par catégorie\n\n| Catégorie | Taille | Assets |\n|---|---:|---:|\n");
            foreach (var category in merged.GroupBy(e => e.Category).OrderByDescending(g => Sum(g)))
                text.Append($"| {category.Key} | {Mb(Sum(category))} | {category.Count()} |\n");

            if (bundles.Count > 0)
            {
                text.Append("\n## Bundles Addressables\n\n| Groupe | Bundle | Compressé | Décompressé | Compression |\n|---|---|---:|---:|---|\n");
                foreach (var b in bundles.OrderByDescending(b => b.Compressed))
                    text.Append($"| {b.Group} | {Path.GetFileName(b.Bundle)} | {Kb(b.Compressed)} | {Kb(b.Uncompressed)} | {b.Compression} |\n");
            }

            var messages = report.steps.SelectMany(s => s.messages)
                .Where(m => m.type == UnityEngine.LogType.Warning || m.type == UnityEngine.LogType.Error || m.type == UnityEngine.LogType.Exception)
                .Select(m => m.type + " : " + FirstLine(m.content)).Distinct().Take(12).ToList();
            if (messages.Count > 0)
            {
                text.Append("\n## Avertissements et erreurs du build\n\n");
                foreach (var message in messages)
                    text.Append("- ").Append(message).Append('\n');
            }

            text.Append($"\n## Les plus gros par catégorie ({topPerCategory} premiers)\n");
            foreach (var category in merged.GroupBy(e => e.Category).OrderByDescending(g => Sum(g)))
            {
                text.Append($"\n### {category.Key}\n\n| Asset | Taille | Où |\n|---|---:|---|\n");
                foreach (var e in category.OrderByDescending(e => e.Bytes).Take(topPerCategory))
                    text.Append($"| {Display(e.Path)} | {Kb(e.Bytes)} | {e.Where} |\n");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(markdownPath));
            File.WriteAllText(markdownPath, text.ToString(), new UTF8Encoding(false));
            return $"{merged.Count} assets, {Mb(Sum(merged))} packed, {bundles.Count} bundles -> {markdownPath}";
        }

        /// <summary>Main asset type of a project or package asset (a font asset, not its atlas; a model, not its meshes).</summary>
        private static string MainType(string path)
        {
            if (string.IsNullOrEmpty(path) || !(path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal)))
                return null;
            return UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(path)?.Name;
        }

        private static string Category(string type, string path)
        {
            string ext = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            if (ext == ".fbx" || ext == ".blend" || ext == ".obj")
                return "Models";
            if (ext == ".spriteatlasv2" || ext == ".spriteatlas")
                return "Textures";
            switch (type)
            {
                case "Texture2D":
                case "Texture":
                case "Sprite":
                case "Cubemap":
                case "SpriteAtlas":
                    return "Textures";
                case "Mesh":
                    return "Meshes";
                case "AudioClip":
                case "AudioMixer":
                case "AudioMixerController":
                    return "Audio";
                case "Shader":
                case "ComputeShader":
                case "ShaderVariantCollection":
                    return "Shaders";
                case "AnimationClip":
                case "AnimatorController":
                case "RuntimeAnimatorController":
                case "Avatar":
                    return "Animation";
                case "Material":
                    return "Materials";
                case "Font":
                case "TMP_FontAsset":
                    return "Fonts";
                case "GameObject":
                case "Transform":
                    return ext == ".unity" ? "Scenes" : "Prefabs";
                case "MonoScript":
                    return "Scripts";
            }
            if (ext == ".unity")
                return "Scenes";
            if (type == "MonoBehaviour" || ext == ".asset")
                return "Data";
            return string.IsNullOrEmpty(type) ? "Other" : "Other (" + type + ")";
        }

        private static string FirstLine(string content)
        {
            string line = (content ?? string.Empty).Split('\n')[0].Trim().Replace("|", "/");
            return line.Length > 220 ? line.Substring(0, 220) + "…" : line;
        }

        private static ulong Sum(IEnumerable<Entry> entries) => (ulong)entries.Sum(e => (decimal)e.Bytes);

        private static string Display(string path) => string.IsNullOrEmpty(path) ? "(built-in)" : path.Replace("|", "/");

        private static string Kb(ulong bytes) => (bytes / 1024.0).ToString("N1", CultureInfo.GetCultureInfo("fr-FR")) + " Ko";

        private static string Mb(ulong bytes) => (bytes / (1024.0 * 1024.0)).ToString("N2", CultureInfo.GetCultureInfo("fr-FR")) + " Mo";
    }
}
