using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PocketWilds.Editor.Build;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.World
{
    /// <summary>
    /// T34C shared Addressables content (doc 11 ADDRESSABLES), all from code:
    /// - group Bootstrap (label boot): the world master shaders every biome uses, and their warmup states;
    /// - group CommonAnimals (label animal_common): the animal catalog (prefabs, rigs, animations, master material) and
    ///   the hatch staging (egg, feedback effects), with their warmup states.
    /// Nothing built into the player references these assets any more (the scenes hold keys), so each lives once, and
    /// later biome and species packs depend on them without copies. The shader warmup is split by owner: each group
    /// warms its own copy of its shaders. Idempotent; ends with the Addressables duplicate analysis.
    /// </summary>
    public static class CommonContentPipeline
    {
        public const string BootstrapGroup = "Bootstrap";
        public const string AnimalsGroup = "CommonAnimals";
        public const string BootLabel = "boot";
        public const string AnimalsLabel = "animal_common";
        public const string WarmupLabel = "warmup";
        public const string CatalogAddress = "animals.catalog";
        public const string HatchAddress = "animals.hatch";
        public const string WorldWarmupAddress = "warmup.world";
        public const string AnimalsWarmupAddress = "warmup.animals";
        public const string CatalogPath = "Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset";
        public const string HatchPath = "Assets/_Game/Content/Visuals/DEF_HatchPresentation.asset";
        public const string WorldWarmupPath = ShaderWarmupPipeline.Folder + "/DEF_ShaderWarmup_World.asset";
        public const string AnimalsWarmupPath = ShaderWarmupPipeline.Folder + "/DEF_ShaderWarmup_Animals.asset";

        /// <summary>Scenes re-saved so a removed direct reference (now a key) leaves their files.</summary>
        public static readonly string[] ScenesHoldingKeys =
        {
            "Assets/_Game/Scenes/SC_Bootstrap.unity",
            "Assets/_Game/Scenes/SC_VerticalSlice_Grassland.unity",
            "Assets/_Game/Scenes/Benchmarks/SC_Benchmark_Animals.unity",
        };

        [MenuItem("Pocket Wilds/World/Setup Common Content (T34C)", priority = 182)]
        public static void SetupMenu() => Debug.Log("[CommonContent] " + Setup());

        public static string Setup()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.AddLabel(BootLabel);
            settings.AddLabel(AnimalsLabel);
            settings.AddLabel(WarmupLabel);

            var boot = EnsureGroup(settings, BootstrapGroup);
            var animals = EnsureGroup(settings, AnimalsGroup);

            // World master shaders: every shader a chunk prefab uses. Owned here, so no biome bundle copies them.
            var worldShaders = WorldShaders();
            foreach (var path in worldShaders)
                AddEntry(settings, boot, path, "shader.world." + Path.GetFileNameWithoutExtension(path).ToLowerInvariant(), BootLabel);
            AddEntry(settings, animals, CatalogPath, CatalogAddress, AnimalsLabel);
            AddEntry(settings, animals, HatchPath, HatchAddress, AnimalsLabel);

            string warmup = SplitWarmup(worldShaders);
            AddEntry(settings, boot, WorldWarmupPath, WorldWarmupAddress, BootLabel, WarmupLabel);
            AddEntry(settings, animals, AnimalsWarmupPath, AnimalsWarmupAddress, AnimalsLabel, WarmupLabel);

            ResaveScenes();
            AssetDatabase.SaveAssets();
            return $"Bootstrap: {boot.entries.Count} entries ({string.Join(", ", worldShaders.Select(Path.GetFileName))}); " +
                   $"CommonAnimals: {animals.entries.Count} entries; {warmup}; " + WorldContentPipeline.Analyze(settings);
        }

        /// <summary>Shaders the world chunks depend on (the world masters).</summary>
        public static List<string> WorldShaders()
        {
            var shaders = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var chunk in WorldContentPipeline.GrasslandChunks)
                foreach (var dependency in AssetDatabase.GetDependencies(chunk.path, true))
                    if (dependency.StartsWith("Assets/", StringComparison.Ordinal) && dependency.EndsWith(".shader", StringComparison.Ordinal))
                        shaders.Add(dependency);
            return shaders.ToList();
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name) ?? settings.CreateGroup(name, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            EditorUtility.SetDirty(group);
            return group;
        }

        private static void AddEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address, params string[] labels)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("Missing asset " + path);
            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.address = address;
            foreach (var label in labels)
                entry.SetLabel(label, true, false, false);
        }

        // ---------------------------------------------------------------- warmup split

        private enum Owner
        {
            Core,
            World,
            Animals,
        }

        /// <summary>
        /// Splits the traced graphics states by where their shader lives: built into the player (DEF_ShaderWarmup), the
        /// world masters (DEF_ShaderWarmup_World, group Bootstrap) or the animals and hatch (DEF_ShaderWarmup_Animals,
        /// group CommonAnimals). Rebuilt from the union of the three sets, so it can run again after any change.
        /// </summary>
        public static string SplitWarmup(List<string> worldShaders)
        {
            var core = ShaderWarmupPipeline.EnsureSet();
            var world = EnsureSet(WorldWarmupPath);
            var animals = EnsureSet(AnimalsWarmupPath);
            var animalShaders = new HashSet<string>(
                AssetDatabase.GetDependencies(new[] { CatalogPath, HatchPath }, true)
                    .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && p.EndsWith(".shader", StringComparison.Ordinal)),
                StringComparer.Ordinal);
            var worldSet = new HashSet<string>(worldShaders, StringComparer.Ordinal);

            Owner OwnerOf(Shader shader)
            {
                string path = AssetDatabase.GetAssetPath(shader);
                if (worldSet.Contains(path))
                    return Owner.World;
                return animalShaders.Contains(path) ? Owner.Animals : Owner.Core;
            }

            // Every traced platform and graphics API: one source collection each, merged from the three sets.
            var sources = new Dictionary<(RuntimePlatform, GraphicsDeviceType), List<GraphicsStateCollection>>();
            foreach (var set in new[] { core, world, animals })
                foreach (var collection in set.collections)
                {
                    if (collection == null)
                        continue;
                    var key = (collection.runtimePlatform, collection.graphicsDeviceType);
                    if (!sources.TryGetValue(key, out var list))
                        sources[key] = list = new List<GraphicsStateCollection>();
                    if (!list.Contains(collection))
                        list.Add(collection);
                }

            var report = new StringBuilder("warmup split:");
            var outputs = new Dictionary<Owner, List<GraphicsStateCollection>>
            {
                [Owner.Core] = new List<GraphicsStateCollection>(),
                [Owner.World] = new List<GraphicsStateCollection>(),
                [Owner.Animals] = new List<GraphicsStateCollection>(),
            };
            var variants = new List<GraphicsStateCollection.ShaderVariant>();
            var states = new List<GraphicsStateCollection.GraphicsState>();
            foreach (var pair in sources)
            {
                var first = pair.Value[0];
                var parts = new Dictionary<Owner, GraphicsStateCollection>();
                foreach (Owner owner in Enum.GetValues(typeof(Owner)))
                {
                    parts[owner] = new GraphicsStateCollection
                    {
                        runtimePlatform = first.runtimePlatform,
                        graphicsDeviceType = first.graphicsDeviceType,
                        version = first.version,
                        qualityLevelName = first.qualityLevelName,
                    };
                }
                foreach (var source in pair.Value)
                {
                    variants.Clear();
                    source.GetVariants(variants);
                    foreach (var variant in variants)
                    {
                        if (variant.shader == null)
                            continue;
                        var target = parts[OwnerOf(variant.shader)];
                        target.AddVariant(variant.shader, variant.passId, variant.keywords);
                        states.Clear();
                        source.GetGraphicsStatesForVariant(variant, states);
                        foreach (var state in states)
                            target.AddGraphicsStateForVariant(variant, state);
                    }
                }
                string stem = $"GSC_{first.runtimePlatform}_{first.graphicsDeviceType}";
                foreach (var part in parts)
                {
                    string suffix = part.Key == Owner.Core ? string.Empty : "_" + part.Key;
                    string path = $"{ShaderWarmupPipeline.Folder}/{stem}{suffix}.graphicsstate";
                    int count = part.Value.totalGraphicsStateCount;
                    report.Append($" {part.Key} {count} states ({part.Value.variantCount} variants);");
                    if (count == 0 && part.Value.variantCount == 0)
                    {
                        Object.DestroyImmediate(part.Value);
                        continue;
                    }
                    string full = Path.GetFullPath(path);
                    if (!part.Value.SaveToFile(full))
                        throw new IOException("Could not write " + path);
                    Object.DestroyImmediate(part.Value);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    var asset = AssetDatabase.LoadAssetAtPath<GraphicsStateCollection>(path)
                                ?? throw new InvalidOperationException("Unity did not import " + path);
                    outputs[part.Key].Add(asset);
                }
            }

            core.collections = outputs[Owner.Core].ToArray();
            world.collections = outputs[Owner.World].ToArray();
            animals.collections = outputs[Owner.Animals].ToArray();
            ShaderWarmupPipeline.RebuildVariants(core, ShaderWarmupPipeline.VariantsPath);
            ShaderWarmupPipeline.RebuildVariants(world, ShaderWarmupPipeline.Folder + "/SVC_World.shadervariants");
            ShaderWarmupPipeline.RebuildVariants(animals, ShaderWarmupPipeline.Folder + "/SVC_Animals.shadervariants");
            foreach (var set in new[] { core, world, animals })
                EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        /// <summary>After a new trace import (ShaderWarmupPipeline): the old split of that platform leaves, then split again.</summary>
        public static string SplitAfterImport(RuntimePlatform platform, GraphicsDeviceType api)
        {
            foreach (var path in new[] { WorldWarmupPath, AnimalsWarmupPath })
            {
                var set = AssetDatabase.LoadAssetAtPath<ShaderWarmupSet>(path);
                if (set == null)
                    continue;
                set.collections = set.collections.Where(c => c != null && !(c.runtimePlatform == platform && c.graphicsDeviceType == api)).ToArray();
                EditorUtility.SetDirty(set);
            }
            return SplitWarmup(WorldShaders());
        }

        private static ShaderWarmupSet EnsureSet(string path)
        {
            var set = AssetDatabase.LoadAssetAtPath<ShaderWarmupSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ShaderWarmupSet>();
                AssetDatabase.CreateAsset(set, path);
            }
            return set;
        }

        // ---------------------------------------------------------------- scenes

        public static void ResaveScenes()
        {
            var previous = EditorSceneManager.GetActiveScene().path;
            foreach (var path in ScenesHoldingKeys)
            {
                if (!File.Exists(path))
                    continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }
    }
}
