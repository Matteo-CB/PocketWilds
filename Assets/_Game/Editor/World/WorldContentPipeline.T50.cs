using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PocketWilds.Editor.Production;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketWilds.Editor.World
{
    /// <summary>
    /// T50 frozen decision 8: every biome, the Grassland included, is loaded by its BiomeWorldProfile (world.{key}, Addressable in
    /// its kit group) at the scene entry; the scene keeps only what no biome owns (world quality, season looks, sun, camera, UI).
    /// The world meshes, materials, kits and clips several biomes use live once, in the explicit group World_Shared, so no bundle
    /// and no scene copies them (« Check Duplicate Bundle Dependencies », « Check Scene to Addressable Duplicate Dependencies »).
    /// All from code, idempotent: <see cref="LoadBiomesByProfile"/> runs the whole step.
    /// </summary>
    public static partial class WorldContentPipeline
    {
        public const string SharedGroup = "World_Shared";
        public const string SharedLabel = "world_shared";

        /// <summary>Address of an asset World_Shared takes in (an asset that already had an address keeps it: code may load it).</summary>
        public const string SharedAddressPrefix = "shared.";

        /// <summary>Scene entry fields that wired the first biome before T50 (the fallback of test scenes): empty in the shipped scene.</summary>
        public static readonly string[] SceneBiomeReferenceFields = { "_chunkSet", "_lighting", "_morning", "_evening", "_night", "_weatherKit", "_ambientLife" };

        /// <summary>The fallback biome's Addressables keys on the scene entry: empty in the shipped scene (the profiles carry them).</summary>
        public static readonly string[] SceneBiomeKeyFields = { "_biomeAudioKey", "_zoneKitKey" };

        [MenuItem("Pocket Wilds/World/Load Every Biome By Its Profile (T50)", priority = 183)]
        public static void LoadBiomesByProfileMenu() => Debug.Log("[WorldContent] " + LoadBiomesByProfile());

        [MenuItem("Pocket Wilds/World/Setup World Shared Content (T50)", priority = 184)]
        public static void SetupSharedMenu() => Debug.Log("[WorldContent] " + SetupShared());

        /// <summary>
        /// The whole T50 step, in order: every biome's world profile Addressable (BiomeWizard.EnsureWorldEntries); the scene entry
        /// without its biome references, loading world.{key} for every biome; every biome's world shaders in Bootstrap with the
        /// warmup split again (CommonContentPipeline); World_Shared; then the Addressables duplicate analysis.
        /// </summary>
        public static string LoadBiomesByProfile()
        {
            var report = new StringBuilder();
            report.Append(BiomeWizard.EnsureWorldEntries()).Append(' ');
            report.Append(MigrateSceneToProfiles()).Append(' ');
            report.Append(CommonContentPipeline.Setup(false)).Append(' ');
            report.Append(SetupShared()).Append(' ');
            report.Append(Analyze(AddressableAssetSettingsDefaultObject.GetSettings(false)));
            return report.ToString();
        }

        // ---------------------------------------------------------------- biomes

        /// <summary>A shipped biome's Addressables content: the entries of its chunk, kit, zone and audio groups.</summary>
        public sealed class BiomeContent
        {
            public string Key;
            public string KitGroup;

            /// <summary>The biome's groups that exist: chunks (one bundle per chunk), kit, zones, audio.</summary>
            public readonly List<string> Groups = new List<string>();

            public readonly List<string> Roots = new List<string>();
        }

        /// <summary>Every shipped biome (DEF_ContentDatabase, under Assets/_Game) in opening order, with its Addressables roots.</summary>
        public static List<BiomeContent> BiomeContents(AddressableAssetSettings settings)
        {
            var result = new List<BiomeContent>();
            foreach (var biome in ContentWizard.Database().biomes.Where(b => b != null).OrderBy(b => b.order))
            {
                if (string.IsNullOrEmpty(biome.key) || BiomeWizard.RootOf(biome) != BiomeWizard.DefaultRoot)
                    continue;
                var paths = new BiomeWizard.BiomePaths(BiomeWizard.NameOf(biome), BiomeWizard.DefaultRoot);
                var content = new BiomeContent { Key = biome.key, KitGroup = paths.KitGroup };
                foreach (var name in new[] { paths.ChunksGroup, paths.KitGroup, paths.ZonesGroup, paths.AudioGroup })
                {
                    var group = settings != null ? settings.FindGroup(name) : null;
                    if (group == null)
                        continue;
                    content.Groups.Add(name);
                    foreach (var entry in group.entries)
                        if (!string.IsNullOrEmpty(entry.AssetPath))
                            content.Roots.Add(entry.AssetPath);
                }
                result.Add(content);
            }
            return result;
        }

        /// <summary>The biome keys the scene loads, west to east: the shipped biomes by order, then listed keys no biome has yet.</summary>
        public static List<string> SceneBiomeKeys(IEnumerable<string> listed)
        {
            var keys = ContentWizard.Database().biomes
                .Where(b => b != null && !string.IsNullOrEmpty(b.key) && BiomeWizard.RootOf(b) == BiomeWizard.DefaultRoot)
                .OrderBy(b => b.order).Select(b => b.key).ToList();
            foreach (var key in listed ?? Enumerable.Empty<string>())
                if (!string.IsNullOrEmpty(key) && !keys.Contains(key))
                    keys.Add(key);
            return keys;
        }

        // ---------------------------------------------------------------- scene

        /// <summary>
        /// The scene entry loads every biome by key and names none: the fallback fields (chunk set, moods, kits, audio and zone
        /// keys) are emptied, the direct profiles cleared, the keys set from the shipped biomes, and the scene's render settings
        /// lose their biome sky (each profile's day mood sets it on Enter). Saved; the previous scene is reopened by path.
        /// </summary>
        public static string MigrateSceneToProfiles()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path != ScenePath && active.isDirty)
                throw new InvalidOperationException("Save or discard changes in " + active.path + " first.");
            string previous = active.path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            VerticalSliceEntry entry = null;
            foreach (var root in scene.GetRootGameObjects())
                entry = entry != null ? entry : root.GetComponentInChildren<VerticalSliceEntry>(true);
            if (entry == null)
                throw new InvalidOperationException("SceneEntry (VerticalSliceEntry) missing in " + ScenePath);
            var serialized = new SerializedObject(entry);
            int cleared = ClearSceneBiomeReferences(serialized);
            var keysProperty = serialized.FindProperty("_biomeKeys")
                               ?? throw new InvalidOperationException("VerticalSliceEntry._biomeKeys missing: compile T50 first.");
            var listed = new List<string>();
            for (int i = 0; i < keysProperty.arraySize; i++)
                listed.Add(keysProperty.GetArrayElementAtIndex(i).stringValue);
            var keys = SceneBiomeKeys(listed);
            keysProperty.arraySize = keys.Count;
            for (int i = 0; i < keys.Count; i++)
                keysProperty.GetArrayElementAtIndex(i).stringValue = keys[i];
            var direct = serialized.FindProperty("_biomes");
            int directCount = direct != null ? direct.arraySize : 0;
            if (direct != null)
                direct.arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var sky = RenderSettings.skybox;
            bool skyCleared = sky != null && AssetDatabase.GetAssetPath(sky).StartsWith("Assets/", StringComparison.Ordinal);
            if (skyCleared)
                RenderSettings.skybox = null;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(previous) && previous != ScenePath && File.Exists(previous))
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            return $"scene: {cleared} biome fields emptied, {directCount} direct profiles cleared{(skyCleared ? ", biome sky left the render settings" : string.Empty)}, " +
                   $"biomes by key {string.Join(", ", keys)};";
        }

        /// <summary>Empties the scene entry's fallback biome fields (references and keys); returns how many held something.</summary>
        public static int ClearSceneBiomeReferences(SerializedObject entry)
        {
            int cleared = 0;
            foreach (var name in SceneBiomeReferenceFields)
            {
                var property = entry.FindProperty(name);
                if (property == null || property.objectReferenceValue == null)
                    continue;
                property.objectReferenceValue = null;
                cleared++;
            }
            foreach (var name in SceneBiomeKeyFields)
            {
                var property = entry.FindProperty(name);
                if (property == null || string.IsNullOrEmpty(property.stringValue))
                    continue;
                property.stringValue = string.Empty;
                cleared++;
            }
            return cleared;
        }

        // ---------------------------------------------------------------- World_Shared

        /// <summary>
        /// First, within each biome, what two of its bundles carry joins its kit group (<see cref="ShareWithinBiomes"/>: the Forest's
        /// chunks share FO meshes like the Grassland's share GL ones). Then
        /// World_Shared (pack together, LZ4, local, label world_shared): every asset two or more biomes use (as a dependency of
        /// their Addressables content, or an entry of one biome's kit group another biome depends on), unless a group outside the
        /// biome kits already owns it (Bootstrap's world shaders, explicit clips, render defaults). An asset moved in keeps its
        /// address; one no longer shared leaves (back to implicit), or goes back to its single biome's kit when it came from
        /// there with its own address (code may load it, HideSeekLayout's kit.grassland.*). Reports
        /// the FBX files several biomes use (bake their meshes instead: an FBX carries all its meshes) and the assets a build
        /// scene still shares with a biome (the scene must let go of them).
        /// </summary>
        public static string SetupShared()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.AddLabel(SharedLabel);
            var group = EnsureGroup(settings, SharedGroup, BundledAssetGroupSchema.BundlePackingMode.PackTogether);
            var biomes = BiomeContents(settings);
            var kitGroups = new HashSet<string>(biomes.Select(b => b.KitGroup), StringComparer.Ordinal);
            var users = SharedUsers(biomes);

            var wanted = new SortedSet<string>(StringComparer.Ordinal);
            var models = new List<string>();
            foreach (var pair in users)
            {
                if (pair.Value.Count < 2 || !Shareable(pair.Key))
                    continue;
                var owned = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(pair.Key));
                if (owned != null && owned.parentGroup != group && (owned.parentGroup == null || !kitGroups.Contains(owned.parentGroup.Name)))
                    continue;
                if (pair.Key.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    models.Add(pair.Key);
                    continue;
                }
                wanted.Add(pair.Key);
            }

            int returned = 0, removed = 0, added = 0;
            foreach (var stale in group.entries.Where(e => !wanted.Contains(e.AssetPath)).ToList())
            {
                // Taken in by World_Shared (its address): back to implicit. Brought from a kit (its own address, code may load it): back there.
                string single = !stale.address.StartsWith(SharedAddressPrefix, StringComparison.Ordinal)
                                && users.TryGetValue(stale.AssetPath, out var keys) && keys.Count == 1
                    ? keys.Min
                    : null;
                var owner = single != null ? biomes.FirstOrDefault(b => b.Key == single) : null;
                var kit = owner != null ? settings.FindGroup(owner.KitGroup) : null;
                if (kit != null)
                {
                    string address = stale.address;
                    var moved = settings.CreateOrMoveEntry(stale.guid, kit, false, false);
                    moved.address = address;
                    returned++;
                }
                else
                {
                    settings.RemoveAssetEntry(stale.guid, false);
                    removed++;
                }
            }
            // After the stale ones left (one may now be shared by two bundles of its biome), before World_Shared takes the wanted ones.
            int kitAdded = ShareWithinBiomes(settings, biomes, wanted);
            foreach (var path in wanted)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                var entry = settings.FindAssetEntry(guid);
                if (entry == null || entry.parentGroup != group)
                {
                    string address = entry != null && !string.IsNullOrEmpty(entry.address) && entry.address != path
                        ? entry.address
                        : SharedAddressPrefix + Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    entry = settings.CreateOrMoveEntry(guid, group, false, false);
                    entry.address = address;
                    added++;
                }
                if (!entry.labels.Contains(SharedLabel))
                    entry.SetLabel(SharedLabel, true, false, false);
            }
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            var report = new StringBuilder($"{SharedGroup}: {group.entries.Count} assets used by several of {biomes.Count} biomes " +
                                           $"({added} in, {returned} back to their kit, {removed} out); {kitAdded} assets two bundles of one biome use joined its kit");
            if (models.Count > 0)
                report.Append($"; FBX used by several biomes, bake their meshes: {string.Join(", ", models.Select(Path.GetFileName))}");
            var conflicts = SceneConflicts(users.Keys);
            report.Append(conflicts.Count == 0
                ? "; no build scene shares an asset with a biome;"
                : $"; CONFLICT, build scenes share with biome bundles (the scene must let go): {string.Join(", ", conflicts.Select(Path.GetFileName))};");
            return report.ToString();
        }

        /// <summary>
        /// Within one biome: an asset two of its bundles use (two chunks, packed one per bundle, or a chunk and the zone kit...) and
        /// no group owns joins the biome's kit group (kit.{key}.{name}, its label), as WorldContentPipeline.Setup does for the
        /// Grassland's chunks; World_Shared then takes what several biomes use. Returns how many joined.
        /// </summary>
        /// <param name="skip">Assets World_Shared is about to take (several biomes use them).</param>
        public static int ShareWithinBiomes(AddressableAssetSettings settings, IEnumerable<BiomeContent> biomes, ICollection<string> skip = null)
        {
            int added = 0;
            foreach (var biome in biomes)
            {
                var kit = settings.FindGroup(biome.KitGroup);
                if (kit == null)
                    continue;
                string label = BiomeWizard.Label(biome.Key);
                settings.AddLabel(label);
                foreach (var pair in BundleUsers(settings, biome).Where(p => p.Value.Count > 1).OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (!Shareable(pair.Key) || pair.Key.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) || (skip != null && skip.Contains(pair.Key)))
                        continue;
                    string guid = AssetDatabase.AssetPathToGUID(pair.Key);
                    if (string.IsNullOrEmpty(guid) || settings.FindAssetEntry(guid) != null)
                        continue;
                    var entry = settings.CreateOrMoveEntry(guid, kit, false, false);
                    entry.address = $"kit.{biome.Key}.{Path.GetFileNameWithoutExtension(pair.Key).ToLowerInvariant()}";
                    entry.SetLabel(label, true, false, false);
                    added++;
                }
                EditorUtility.SetDirty(kit);
            }
            return added;
        }

        /// <summary>
        /// For each dependency of one biome's Addressables content, the bundles that would carry it: one per entry of a group packed
        /// separately (the chunks), one per group packed together (kit, zones, audio).
        /// </summary>
        public static Dictionary<string, HashSet<string>> BundleUsers(AddressableAssetSettings settings, BiomeContent biome)
        {
            var bundles = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var name in biome.Groups)
            {
                var group = settings.FindGroup(name);
                if (group == null)
                    continue;
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                bool separately = schema != null && schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                foreach (var entry in group.entries)
                {
                    if (string.IsNullOrEmpty(entry.AssetPath))
                        continue;
                    string bundle = separately ? name + "/" + entry.AssetPath : name;
                    foreach (var dependency in AssetDatabase.GetDependencies(entry.AssetPath, true))
                    {
                        if (dependency == entry.AssetPath)
                            continue;
                        if (!bundles.TryGetValue(dependency, out var set))
                            bundles[dependency] = set = new HashSet<string>(StringComparer.Ordinal);
                        set.Add(bundle);
                    }
                }
            }
            return bundles;
        }

        /// <summary>For each asset of the biomes' content (roots and their dependencies), the keys of the biomes using it.</summary>
        public static Dictionary<string, SortedSet<string>> SharedUsers(IEnumerable<BiomeContent> biomes)
        {
            var users = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            void Use(string path, string key)
            {
                if (!users.TryGetValue(path, out var keys))
                    users[path] = keys = new SortedSet<string>(StringComparer.Ordinal);
                keys.Add(key);
            }
            foreach (var biome in biomes)
            {
                foreach (var root in biome.Roots)
                {
                    Use(root, biome.Key);
                    foreach (var dependency in AssetDatabase.GetDependencies(root, true))
                        if (dependency != root)
                            Use(dependency, biome.Key);
                }
            }
            return users;
        }

        /// <summary>
        /// What World_Shared may own: project assets, never scripts, prefabs (a nested prefab is baked into its chunk), scenes or
        /// shaders (the world shaders belong to Bootstrap, CommonContentPipeline), nor a biome's world profile.
        /// </summary>
        public static bool Shareable(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                return false;
            foreach (var extension in new[] { ".cs", ".prefab", ".unity", ".shader", ".shadervariants", ".hlsl", ".cginc" })
                if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    return false;
            return AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(BiomeWorldProfile);
        }

        /// <summary>Project assets (not scripts) that both an enabled build scene and a biome's Addressables content use.</summary>
        public static List<string> SceneConflicts(IEnumerable<string> biomeAssets)
        {
            var scenes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scene in EditorBuildSettings.scenes.Where(s => s.enabled))
                scenes.UnionWith(AssetDatabase.GetDependencies(scene.path, true));
            return biomeAssets.Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && !p.EndsWith(".cs", StringComparison.Ordinal) && scenes.Contains(p))
                .OrderBy(p => p, StringComparer.Ordinal).ToList();
        }
    }
}
