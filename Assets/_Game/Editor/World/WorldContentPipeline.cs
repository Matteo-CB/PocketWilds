using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PocketWilds.Editor.Environment;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.World
{
    /// <summary>
    /// T27 world streaming setup, all from code (doc 11: nothing hidden in one Editor state):
    /// Addressables groups N-GRP-BIOME-GRASSLAND (one bundle per chunk) and its shared kit (meshes, materials and
    /// shaders both chunks use, so no bundle duplicates them), label biome_grassland, DEF_ChunkSet_Grassland, and
    /// SC_VerticalSlice_Grassland without a chunk in the scene file. Idempotent.
    /// </summary>
    public static class WorldContentPipeline
    {
        public const string ChunkSetPath = "Assets/_Game/Content/Biomes/DEF_ChunkSet_Grassland.asset";
        public const string ScenePath = "Assets/_Game/Scenes/SC_VerticalSlice_Grassland.unity";
        public const string ChunksGroup = "Biome_Grassland_Chunks";
        public const string KitGroup = "Biome_Grassland_Kit";
        public const string BiomeLabel = "biome_grassland";
        public const string StartAddress = "chunk.grassland.start";
        public const string MeadowAddress = "chunk.grassland.meadow";

        /// <summary>
        /// Chunk prefabs in world order: address, path, origin, pinned. T33B: the strip continues east of the meadow
        /// (StreamBend, Orchard, Hill, WindmillRise, PondEdge, ForestEdge), one chunk every 14 m.
        /// </summary>
        public static readonly (string address, string path, Vector3 origin, bool pinned)[] GrasslandChunks =
            new[]
            {
                (StartAddress, GrasslandPipeline.ChunkPath, Vector3.zero, true),
                (MeadowAddress, GrasslandPipeline.MeadowChunkPath, GrasslandPipeline.MeadowOffset, false),
            }.Concat(GrasslandPipeline.StripChunkEntries.Select(e => (e.address, e.path, e.origin, false))).ToArray();

        [MenuItem("Pocket Wilds/World/Setup Grassland Streaming (T27)", priority = 180)]
        public static void SetupMenu() => Debug.Log("[WorldContent] " + Setup());

        public static string Setup()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            // Player builds always rebuild the content: the shipped catalog can never be stale.
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            settings.AddLabel(BiomeLabel);

            var chunks = EnsureGroup(settings, ChunksGroup, BundledAssetGroupSchema.BundlePackingMode.PackSeparately);
            var kit = EnsureGroup(settings, KitGroup, BundledAssetGroupSchema.BundlePackingMode.PackTogether);

            foreach (var chunk in GrasslandChunks)
                AddEntry(settings, chunks, chunk.path, chunk.address);

            // Shared dependencies get one owner: the kit bundle. Each chunk bundle then only carries what is its own
            // (its ground mesh and merged prop meshes), which is exactly what a release frees.
            var shared = SharedDependencies(GrasslandChunks.Select(c => c.path).ToArray());
            var stale = kit.entries.Where(e => !shared.Contains(e.AssetPath)).ToList();
            foreach (var entry in stale)
                settings.RemoveAssetEntry(entry.guid);
            foreach (var path in shared)
                AddEntry(settings, kit, path, "kit.grassland." + Path.GetFileNameWithoutExtension(path).ToLowerInvariant());

            EnsureChunkSet();
            MigrateScene();
            AssetDatabase.SaveAssets();
            return $"{chunks.entries.Count} chunks, {kit.entries.Count} shared kit assets ({string.Join(", ", shared.Select(Path.GetFileName))}).";
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name, BundledAssetGroupSchema.BundlePackingMode mode)
        {
            var group = settings.FindGroup(name) ?? settings.CreateGroup(name, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = mode;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            EditorUtility.SetDirty(group);
            return group;
        }

        private static void AddEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("Missing asset " + path);
            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.address = address;
            entry.SetLabel(BiomeLabel, true, false, false);
        }

        /// <summary>Project assets two or more chunks depend on (scripts, prefabs and package assets excluded).</summary>
        public static HashSet<string> SharedDependencies(string[] chunkPaths)
        {
            var counts = new Dictionary<string, int>();
            foreach (var chunk in chunkPaths)
            {
                foreach (var dependency in AssetDatabase.GetDependencies(chunk, true))
                {
                    // Package assets (URP) stay with the player; kit FBX files must not be shared (see GrasslandPipeline.KitMesh).
                    if (dependency == chunk || !dependency.StartsWith("Assets/", StringComparison.Ordinal)
                        || dependency.EndsWith(".cs", StringComparison.Ordinal) || dependency.EndsWith(".prefab", StringComparison.Ordinal))
                        continue;
                    counts.TryGetValue(dependency, out int n);
                    counts[dependency] = n + 1;
                }
            }
            return new HashSet<string>(counts.Where(p => p.Value > 1).Select(p => p.Key).OrderBy(p => p, StringComparer.Ordinal));
        }

        private static void EnsureChunkSet()
        {
            var set = AssetDatabase.LoadAssetAtPath<WorldChunkSet>(ChunkSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<WorldChunkSet>();
                AssetDatabase.CreateAsset(set, ChunkSetPath);
            }
            var entries = GrasslandChunks.Select(c => new WorldChunkSet.Entry
            {
                id = c.address,
                prefab = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(c.path)),
                origin = c.origin,
                pinned = c.pinned,
            }).ToArray();
            set.SetAuthoring(entries, 7f, StreamingWindow.Default);
            EditorUtility.SetDirty(set);
        }

        /// <summary>The scene keeps its camera, light, UI and entry; the chunks now come from the streamer.</summary>
        private static void MigrateScene()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path != ScenePath && active.isDirty)
                throw new InvalidOperationException("Save or discard changes in " + active.path + " first.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            VerticalSliceEntry entry = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var chunk in root.GetComponentsInChildren<WorldChunk>(true))
                    Object.DestroyImmediate(PrefabUtility.GetOutermostPrefabInstanceRoot(chunk.gameObject) ?? chunk.gameObject);
                entry = entry != null ? entry : root.GetComponentInChildren<VerticalSliceEntry>(true);
            }
            if (entry == null)
                throw new InvalidOperationException("SceneEntry (VerticalSliceEntry) missing in " + ScenePath);
            var serialized = new SerializedObject(entry);
            serialized.FindProperty("_chunkSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WorldChunkSet>(ChunkSetPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Chunk prefab instance for edit-mode tools (captures) in the open scene. Callers reopen the scene from
        /// disk afterwards and never save it.
        /// </summary>
        public static WorldChunk SpawnForTools(string address)
        {
            var chunk = GrasslandChunks.First(c => c.address == address);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(chunk.path);
            var instance = Object.Instantiate(prefab, chunk.origin, Quaternion.identity);
            instance.name = prefab.name;
            return instance.GetComponent<WorldChunk>();
        }

        /// <summary>Egg spawn of the pinned start chunk, read from the prefab asset (no instance needed).</summary>
        public static Vector3 StartEggSpawn()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrasslandPipeline.ChunkPath);
            var chunk = prefab != null ? prefab.GetComponent<WorldChunk>() : null;
            return chunk != null && chunk.EggSpawn != null ? chunk.EggSpawn.position : Vector3.zero;
        }

        // ---------------- Content build and duplicate review (ADDRESSABLE DONE) ----------------

        [MenuItem("Pocket Wilds/World/Build Addressables Content", priority = 181)]
        public static void BuildMenu() => Debug.Log("[WorldContent] " + BuildContent());

        /// <summary>Builds the player content for the active target and reports the bundles and any duplication.</summary>
        public static string BuildContent()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false)
                           ?? throw new InvalidOperationException("Run Setup Grassland Streaming first.");
            // With the build layout on, Addressables never opens its first-build modal prompt (it blocked MCP once),
            // and the layout report is what the duplicate review reads.
            ProjectConfigData.GenerateBuildLayout = true;
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error))
                return "FAILED: " + result.Error;
            var report = new StringBuilder();
            report.Append($"built in {result.Duration:F1} s; ");
            foreach (var bundle in result.AssetBundleBuildResults.OrderBy(b => b.SourceAssetGroup.Name))
            {
                long size = File.Exists(bundle.FilePath) ? new FileInfo(bundle.FilePath).Length : -1;
                report.Append($"{Path.GetFileName(bundle.FilePath)} {size / 1024f:F0} KB; ");
            }
            report.Append(Analyze(settings));
            return report.ToString();
        }

        /// <summary>Addressables analyze rules: assets duplicated across bundles, or between bundles and scenes.</summary>
        public static string Analyze(AddressableAssetSettings settings)
        {
            var report = new StringBuilder();
            foreach (var rule in new AnalyzeRule[] { new CheckBundleDupeDependencies(), new CheckSceneDupeDependencies() })
            {
                var results = rule.RefreshAnalysis(settings);
                var issues = results.Where(r => r.severity != MessageType.None && r.resultName != "No issues found").ToList();
                report.Append($"{rule.ruleName}: {(issues.Count == 0 ? "no duplicates" : string.Join(" | ", issues.Select(r => r.resultName)))}; ");
                rule.ClearAnalysis();
            }
            return report.ToString();
        }
    }
}
