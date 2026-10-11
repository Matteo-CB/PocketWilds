using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.Profiling;

namespace PocketWilds.Editor.Events
{
    /// <summary>
    /// T49 decision 5: the Addressables group « LiveOps_Burrows » (label « liveops.burrows », local for now, remote with T55) holds
    /// the event's own assets: the mole (prefab, model, textures, clips, voice), the 8 accessories (PF_Acc_*, ACC_* models), the
    /// decorations and the monument (BurrowsDecorPipeline: PF_Decor_Burrows_*, PF_Landmark_Burrows_RootBurrow and their FBX, addressed
    /// by prefab name as DEF_CosmeticVisualCatalog's decoration entries expect), the event's icons (ICO_Accessory_*, ICO_Outfit_Burrows_*,
    /// ICO_Decor_Burrows_*) and its album art. Found by name in Assets/_Game (never scripts, definitions or editor files); an asset
    /// already an entry of another group stays there (nothing common is moved or duplicated). Size budget 25 MB.
    /// </summary>
    public static class BurrowsContentGroup
    {
        public const long BudgetBytes = 25L * 1024L * 1024L;
        public const string ReportPath = "Docs/Build/LiveOps_Burrows_Size.txt";
        public static string Group => EventContentGroups.GroupOf(BurrowsEventPipeline.Key);
        public static string Label => EventContentGroups.LabelOf(BurrowsEventPipeline.Key);

        /// <summary>Name fragments of the event's own assets (case sensitive).</summary>
        public static readonly string[] Tokens =
        {
            "Mole", "Burrows", "Pousses", "SproutBurst", "EVT_103", "EVT-103", "evt.103", "SproutHat", "SeedSack", "BurrowGoggles",
            "GardenerScarf", "MinerHelmet", "GoldenShovel", "OverallsBib", "OverallsStraps", "EventBurrows",
        };

        private static readonly string[] Types = { "t:Prefab", "t:Model", "t:Texture2D", "t:Sprite", "t:Material", "t:Mesh", "t:AnimationClip", "t:AudioClip" };

        /// <summary>
        /// What the group itself holds today: the decorations and the monument, loaded by address only. The mole, the accessories,
        /// icons, the hatch effect and the stickers are still referenced by the base catalogs (visuals, shop icons, VFX, albums):
        /// in the group they would only be duplicated, so they stay in the base build until T55 moves them to addresses. The size
        /// rule (25 MB) still counts every asset of the event, wherever it lives (<see cref="Footprint"/>).
        /// </summary>
        public static readonly string[] GroupPrefixes = { "PF_Decor_Burrows_", "PF_Landmark_Burrows_", "Decor_Burrows_", "Landmark_Burrows_" };

        public static bool InGroup(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            foreach (var prefix in GroupPrefixes)
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>The event's own assets in the project today (paths, sorted).</summary>
        public static List<string> Candidates()
        {
            var found = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var type in Types)
            {
                foreach (var guid in AssetDatabase.FindAssets(type, new[] { "Assets/_Game" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (path.Contains("/Editor/") || path.Contains("/Tests/") || path.EndsWith(".cs", StringComparison.Ordinal) || path.StartsWith("Assets/_Game/Content/", StringComparison.Ordinal))
                        continue;
                    foreach (var token in Tokens)
                    {
                        if (name.Contains(token))
                        {
                            found.Add(path);
                            break;
                        }
                    }
                }
            }
            return found.ToList();
        }

        /// <summary>Creates or updates the group and its entries, writes the size report. Returns a one-line summary.</summary>
        public static string Build()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
                return "Addressables are not set up.";
            var group = settings.FindGroup(Group) ?? settings.CreateGroup(Group, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            // Content-update friendly: the event's group can be rebuilt alone.
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = false;
            var kept = new List<string>();
            int added = 0;
            var wanted = new HashSet<string>(Candidates().Where(InGroup), StringComparer.Ordinal);
            // Entries an older build put in the group go back to the base build.
            foreach (var stale in group.entries.Where(e => !wanted.Contains(e.AssetPath)).ToList())
                settings.RemoveAssetEntry(stale.guid);
            foreach (var path in wanted)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                var existing = settings.FindAssetEntry(guid);
                if (existing != null && existing.parentGroup != group)
                {
                    kept.Add(path + " (" + existing.parentGroup.Name + ")");
                    continue;
                }
                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.address = Path.GetFileNameWithoutExtension(path);
                entry.SetLabel(Label, true, true, false);
                added++;
            }
            ShareRenderPipelineDefaults(settings, group);
            EditorUtility.SetDirty(group);
            AssetDatabase.SaveAssets();
            var measure = Measure();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, measure.Report(kept));
            var footprint = Footprint();
            return $"{Group}: {added} entries, {kept.Count} kept in other groups; {measure.Summary}; whole event {footprint.Bytes / 1048576.0:F2} MB of 25 MB";
        }

        /// <summary>Owner of the render pipeline's default material and shaders every imported model points to.</summary>
        public const string SharedDefaultsGroup = "Shared_RenderDefaults";

        /// <summary>
        /// An imported model without its own materials still names the pipeline's default Lit material (and its shaders). Two bundles
        /// holding models would each carry a copy (« Check Duplicate Bundle Dependencies »): one small shared group owns them instead.
        /// </summary>
        private static void ShareRenderPipelineDefaults(AddressableAssetSettings settings, AddressableAssetGroup eventGroup)
        {
            var defaults = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var entry in eventGroup.entries)
                foreach (var dependency in AssetDatabase.GetDependencies(entry.AssetPath, true))
                    if (dependency.StartsWith("Packages/com.unity.render-pipelines.", StringComparison.Ordinal)
                        && (dependency.EndsWith(".mat", StringComparison.Ordinal) || dependency.EndsWith(".shader", StringComparison.Ordinal)))
                        defaults.Add(dependency);
            if (defaults.Count == 0)
                return;
            var shared = settings.FindGroup(SharedDefaultsGroup) ?? settings.CreateGroup(SharedDefaultsGroup, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = shared.GetSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.IncludeInBuild = true;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            foreach (var path in defaults)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                    continue;
                var existing = settings.FindAssetEntry(guid);
                if (existing == null || existing.parentGroup != shared)
                    settings.CreateOrMoveEntry(guid, shared, false, false);
            }
            EditorUtility.SetDirty(shared);
        }

        public sealed class Measurement
        {
            public long Bytes;
            public bool FromBundle;
            public string BundlePath = string.Empty;
            public readonly List<(string path, long bytes)> Assets = new List<(string, long)>();

            public string Summary =>
                $"{Bytes / (1024.0 * 1024.0):F2} MB of {BudgetBytes / (1024 * 1024)} MB ({(FromBundle ? "built bundle " + Path.GetFileName(BundlePath) : "estimate from asset sizes")})";

            public string Report(List<string> kept)
            {
                var text = new StringBuilder();
                text.AppendLine("LiveOps_Burrows size report (T49, BurrowsContentGroup)");
                text.AppendLine(Summary);
                text.AppendLine("Method: the built LZ4 bundle when Library/com.unity.addressables holds one, otherwise an upper estimate: runtime size of");
                text.AppendLine("textures, meshes, clips and sounds (Profiler.GetRuntimeMemorySizeLong) plus the file size of prefabs and materials, for the");
                text.AppendLine("group's assets and their dependencies that no other group holds (what the bundle would carry).");
                text.AppendLine();
                foreach (var (path, bytes) in Assets.OrderByDescending(a => a.bytes))
                    text.AppendLine($"{bytes / 1024.0,10:F1} KB  {path}");
                if (kept != null && kept.Count > 0)
                {
                    text.AppendLine();
                    text.AppendLine("Kept in their own group (not duplicated):");
                    foreach (var k in kept)
                        text.AppendLine("  " + k);
                }
                return text.ToString();
            }
        }

        /// <summary>
        /// The group's size: the built bundle when one exists (Library/com.unity.addressables/aa/&lt;platform&gt;), else the estimate over
        /// the group's entries (or, before the group exists, the candidates) and their dependencies outside every other group.
        /// </summary>
        public static Measurement Measure()
        {
            var result = new Measurement();
            string bundle = BuiltBundle();
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            var group = settings != null ? settings.FindGroup(Group) : null;
            var roots = group != null ? group.entries.Select(e => e.AssetPath).Where(p => !string.IsNullOrEmpty(p)).ToList() : Candidates();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in AssetDatabase.GetDependencies(roots.ToArray(), true))
            {
                if (!seen.Add(path) || path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".shader", StringComparison.Ordinal)
                    || !path.StartsWith("Assets/", StringComparison.Ordinal))
                    continue;
                if (settings != null)
                {
                    var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
                    if (entry != null && entry.parentGroup != group)
                        continue;
                }
                long bytes = EstimateBytes(path);
                result.Assets.Add((path, bytes));
                result.Bytes += bytes;
            }
            if (!string.IsNullOrEmpty(bundle))
            {
                result.FromBundle = true;
                result.BundlePath = bundle;
                result.Bytes = new FileInfo(bundle).Length;
            }
            return result;
        }

        /// <summary>
        /// Every asset of the event wherever it lives today (the group, or the base build until T55): the event-named assets and
        /// their event-named dependencies, estimated like <see cref="Measure"/>. The 25 MB rule holds on this whole footprint.
        /// </summary>
        public static Measurement Footprint()
        {
            var result = new Measurement();
            var roots = Candidates();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in AssetDatabase.GetDependencies(roots.ToArray(), true))
            {
                if (!seen.Add(path) || path.EndsWith(".cs", StringComparison.Ordinal) || !path.StartsWith("Assets/", StringComparison.Ordinal))
                    continue;
                string name = Path.GetFileNameWithoutExtension(path);
                if (!Tokens.Any(t => name.Contains(t)))
                    continue;
                long bytes = EstimateBytes(path);
                result.Assets.Add((path, bytes));
                result.Bytes += bytes;
            }
            return result;
        }

        private static string BuiltBundle()
        {
            string root = Path.Combine("Library", "com.unity.addressables", "aa");
            if (!Directory.Exists(root))
                return null;
            string prefix = Group.ToLowerInvariant();
            return Directory.GetFiles(root, "*.bundle", SearchOption.AllDirectories)
                .Where(f => Path.GetFileName(f).ToLowerInvariant().StartsWith(prefix, StringComparison.Ordinal))
                .OrderByDescending(f => File.GetLastWriteTimeUtc(f)).FirstOrDefault();
        }

        private static long EstimateBytes(string path)
        {
            // T48B: a texture counts its size on the phone (Android format, max size, mips) from its import settings: the editor's
            // runtime size followed the editor's state (formats, loaded mips, readable copies) and moved by several MB.
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                return AndroidTextureBytes(importer);
            long total = 0;
            bool runtime = false;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (obj is Texture || obj is Mesh || obj is AnimationClip || obj is AudioClip)
                {
                    total += Profiler.GetRuntimeMemorySizeLong(obj);
                    runtime = true;
                }
            }
            if (runtime)
                return total;
            return File.Exists(path) ? new FileInfo(path).Length : 0L;
        }

        /// <summary>Bytes of a texture on Android: its size after the max size, the format's bits per pixel, × 4/3 with mips.</summary>
        public static long AndroidTextureBytes(TextureImporter importer)
        {
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            var android = importer.GetPlatformTextureSettings("Android");
            int max = android.overridden ? android.maxTextureSize : importer.maxTextureSize;
            float scale = Mathf.Min(1f, (float)max / Mathf.Max(1, Mathf.Max(width, height)));
            long w = Math.Max(1, (long)Mathf.Round(width * scale));
            long h = Math.Max(1, (long)Mathf.Round(height * scale));
            var format = android.overridden ? android.format : importer.GetAutomaticFormat("Android");
            double bytes = w * h * BitsPerPixel(format) / 8.0;
            if (importer.mipmapEnabled)
                bytes *= 4.0 / 3.0;
            return (long)Math.Ceiling(bytes);
        }

        private static double BitsPerPixel(TextureImporterFormat format)
        {
            switch (format)
            {
                case TextureImporterFormat.ASTC_4x4: case TextureImporterFormat.ASTC_HDR_4x4: return 8.0;
                case TextureImporterFormat.ASTC_5x5: case TextureImporterFormat.ASTC_HDR_5x5: return 128.0 / 25.0;
                case TextureImporterFormat.ASTC_6x6: case TextureImporterFormat.ASTC_HDR_6x6: return 128.0 / 36.0;
                case TextureImporterFormat.ASTC_8x8: case TextureImporterFormat.ASTC_HDR_8x8: return 2.0;
                case TextureImporterFormat.ASTC_10x10: case TextureImporterFormat.ASTC_HDR_10x10: return 1.28;
                case TextureImporterFormat.ASTC_12x12: case TextureImporterFormat.ASTC_HDR_12x12: return 128.0 / 144.0;
                case TextureImporterFormat.ETC_RGB4: case TextureImporterFormat.ETC2_RGB4: case TextureImporterFormat.ETC2_RGB4_PUNCHTHROUGH_ALPHA:
                case TextureImporterFormat.ETC_RGB4Crunched: return 4.0;
                case TextureImporterFormat.ETC2_RGBA8: case TextureImporterFormat.ETC2_RGBA8Crunched: case TextureImporterFormat.Alpha8:
                case TextureImporterFormat.R8: return 8.0;
                case TextureImporterFormat.RGB16: case TextureImporterFormat.RGBA16: case TextureImporterFormat.R16: case TextureImporterFormat.RG16:
                    return 16.0;
                case TextureImporterFormat.RGB24: return 24.0;
                case TextureImporterFormat.RGBAHalf: return 64.0;
                case TextureImporterFormat.RGBAFloat: return 128.0;
                default: return 32.0; // RGBA32 and anything unknown: the safe upper bound.
            }
        }
    }
}
