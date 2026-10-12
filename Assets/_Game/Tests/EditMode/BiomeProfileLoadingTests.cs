using System;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Editor.Production;
using PocketWilds.Editor.World;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T50 frozen decision 8: every biome, the Grassland included, is loaded by its world profile (world.{key} in its kit group);
    /// the vertical slice scene names no biome (no biome asset among its dependencies, its keys list every shipped biome, the
    /// Grassland first); what several biomes use lives once, in World_Shared (or with a group outside the biome kits that owns
    /// it). Run « Pocket Wilds/World/Load Every Biome By Its Profile (T50) » when one fails.
    /// </summary>
    public class BiomeProfileLoadingTests
    {
        private static AddressableAssetSettings Settings => AddressableAssetSettingsDefaultObject.Settings;

        [Test]
        public void EveryBiome_HasItsWorldProfile_AddressableInItsKitGroup()
        {
            var biomes = ContentWizard.Database().biomes.Where(b => b != null && BiomeWizard.RootOf(b) == BiomeWizard.DefaultRoot).ToList();
            Assert.IsTrue(biomes.Any(b => b.key == "grassland"), "DEF_Biome_Grassland registered.");
            foreach (var biome in biomes)
            {
                var paths = new BiomeWizard.BiomePaths(BiomeWizard.NameOf(biome), BiomeWizard.DefaultRoot);
                var profile = AssetDatabase.LoadAssetAtPath<BiomeWorldProfile>(paths.World);
                Assert.IsNotNull(profile, paths.World + " (Biome Wizard).");
                var entry = Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(paths.World));
                Assert.IsNotNull(entry, paths.World + " is Addressable: the scene loads it by key.");
                Assert.AreEqual(BiomeWorldProfile.AddressOf(biome.key), entry.address);
                Assert.AreEqual(paths.KitGroup, entry.parentGroup.Name, "A biome's profile lives in its kit group.");
                Assert.IsTrue(entry.labels.Contains(BiomeWizard.Label(biome.key)), "With the biome's label.");
                Assert.IsNotNull(profile.chunkSet, biome.key + ": chunk set.");
                Assert.IsNotNull(profile.day, biome.key + ": day mood.");
            }
        }

        [Test]
        public void VerticalSlice_NamesNoBiome_AndListsEveryBiomeByKey()
        {
            var settings = Settings;
            var users = WorldContentPipeline.SharedUsers(WorldContentPipeline.BiomeContents(settings));
            var conflicts = WorldContentPipeline.SceneConflicts(users.Keys);
            Assert.IsEmpty(conflicts, "Build scenes must not share an asset with a biome's bundles:\n" + string.Join("\n", conflicts));

            var biomeTypes = new[] { typeof(BiomeWorldProfile), typeof(WorldChunkSet), typeof(LightingProfile), typeof(WeatherKit), typeof(AmbientLifeKit), typeof(ZoneKit), typeof(PocketWilds.Services.Feedback.BiomeAudioSet) };
            foreach (var dependency in AssetDatabase.GetDependencies(SliceScene, true))
            {
                var type = AssetDatabase.GetMainAssetTypeAtPath(dependency);
                Assert.IsFalse(biomeTypes.Contains(type), $"The scene references {dependency}: every biome comes from its profile (T50).");
            }

            var scene = EditorSceneManager.OpenScene(SliceScene, OpenSceneMode.Additive);
            try
            {
                var entry = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<VerticalSliceEntry>(true)).First(e => e != null);
                var so = new SerializedObject(entry);
                foreach (var field in WorldContentPipeline.SceneBiomeReferenceFields)
                    Assert.IsNull(so.FindProperty(field).objectReferenceValue, field + " stays empty (fallback of test scenes only).");
                foreach (var field in WorldContentPipeline.SceneBiomeKeyFields)
                    Assert.IsEmpty(so.FindProperty(field).stringValue, field + " stays empty (the profiles carry the keys).");
                Assert.AreEqual(0, so.FindProperty("_biomes").arraySize, "No profile wired directly in the shipped scene.");
                var keysProperty = so.FindProperty("_biomeKeys");
                var keys = Enumerable.Range(0, keysProperty.arraySize).Select(i => keysProperty.GetArrayElementAtIndex(i).stringValue).ToList();
                var expected = ContentWizard.Database().biomes.Where(b => b != null && BiomeWizard.RootOf(b) == BiomeWizard.DefaultRoot)
                    .OrderBy(b => b.order).Select(b => b.key).ToList();
                Assert.AreEqual("grassland", keys.FirstOrDefault(), "The Grassland first: it hosts the refuge.");
                foreach (var key in expected)
                    Assert.Contains(key, keys, "Every shipped biome loads by key.");
                CollectionAssert.AreEqual(expected, keys.Where(expected.Contains).ToList(), "Keys in world order.");

                // Edit-mode tools (captures) read the first biome's day mood from its profile without a boot.
                var grassland = AssetDatabase.LoadAssetAtPath<BiomeWorldProfile>(new BiomeWizard.BiomePaths("Grassland", BiomeWizard.DefaultRoot).World);
                Assert.IsNotNull(entry.Lighting, "Captures apply entry.Lighting in edit mode.");
                Assert.AreSame(grassland.day, entry.Lighting);
                Assert.AreSame(grassland.chunkSet, entry.ChunkSet);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void AssetsUsedBySeveralBiomes_LiveOnce_InWorldShared()
        {
            var settings = Settings;
            var biomes = WorldContentPipeline.BiomeContents(settings);
            var kits = biomes.Select(b => b.KitGroup).ToList();
            var users = WorldContentPipeline.SharedUsers(biomes);
            foreach (var pair in users.Where(p => p.Value.Count > 1 && WorldContentPipeline.Shareable(p.Key)))
            {
                Assert.IsFalse(pair.Key.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase),
                    $"{pair.Key} is used by {string.Join(", ", pair.Value)}: bake its meshes (an FBX carries all of them).");
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(pair.Key));
                Assert.IsNotNull(entry, $"{pair.Key} is used by {string.Join(", ", pair.Value)}: World_Shared owns it.");
                Assert.IsFalse(kits.Contains(entry.parentGroup.Name),
                    $"{pair.Key} is used by {string.Join(", ", pair.Value)} but sits in {entry.parentGroup.Name}: World_Shared owns it.");
            }
            // Within a biome: what two of its bundles carry (two chunks, a chunk and the zone kit) is an entry of a group.
            foreach (var biome in biomes)
                foreach (var pair in WorldContentPipeline.BundleUsers(settings, biome).Where(p => p.Value.Count > 1 && WorldContentPipeline.Shareable(p.Key)))
                    Assert.IsNotNull(settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(pair.Key)),
                        $"{pair.Key} is carried by {pair.Value.Count} bundles of {biome.Key}: its kit (or World_Shared) owns it.");
            var shared = settings.FindGroup(WorldContentPipeline.SharedGroup);
            if (shared == null)
                return;
            foreach (var entry in shared.entries)
            {
                Assert.IsTrue(users.TryGetValue(entry.AssetPath, out var keys) && keys.Count > 1, entry.AssetPath + " is no longer shared: rerun Setup World Shared.");
                Assert.IsTrue(entry.labels.Contains(WorldContentPipeline.SharedLabel), entry.AssetPath);
            }
        }

        private const string SliceScene = WorldContentPipeline.ScenePath;
    }
}
