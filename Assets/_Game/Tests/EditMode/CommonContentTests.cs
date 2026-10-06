using System.Linq;
using NUnit.Framework;
using PocketWilds.Editor.World;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T34C gate 17 (Addressables review) on the shared content: the Bootstrap and CommonAnimals groups own the world
    /// master shaders, the animals and the hatch staging; no scene built into the player references them; nothing is
    /// duplicated between bundles or between bundles and scenes; each group carries the warmup of its own shaders.
    /// </summary>
    public class CommonContentTests
    {
        private static AddressableAssetSettings Settings => AddressableAssetSettingsDefaultObject.Settings;

        private static AddressableAssetEntry Entry(string path) => Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));

        [Test]
        public void SharedContent_LivesInItsGroups_WithStableAddressesAndLabels()
        {
            var catalog = Entry(CommonContentPipeline.CatalogPath);
            Assert.IsNotNull(catalog, "The animal catalog is addressable.");
            Assert.AreEqual(CommonContentPipeline.AnimalsGroup, catalog.parentGroup.Name);
            Assert.AreEqual(CommonContentPipeline.CatalogAddress, catalog.address);
            Assert.IsTrue(catalog.labels.Contains(CommonContentPipeline.AnimalsLabel));

            var hatch = Entry(CommonContentPipeline.HatchPath);
            Assert.IsNotNull(hatch);
            Assert.AreEqual(CommonContentPipeline.AnimalsGroup, hatch.parentGroup.Name);
            Assert.AreEqual(CommonContentPipeline.HatchAddress, hatch.address);

            var shaders = CommonContentPipeline.WorldShaders();
            Assert.GreaterOrEqual(shaders.Count, 4, "Ground, foliage, water, environment.");
            foreach (var shader in shaders)
            {
                var entry = Entry(shader);
                Assert.IsNotNull(entry, shader + " is owned by a group.");
                Assert.AreEqual(CommonContentPipeline.BootstrapGroup, entry.parentGroup.Name, shader + ": one owner for every biome.");
                Assert.IsTrue(entry.labels.Contains(CommonContentPipeline.BootLabel));
            }

            foreach (var (path, group) in new[]
                     {
                         (CommonContentPipeline.WorldWarmupPath, CommonContentPipeline.BootstrapGroup),
                         (CommonContentPipeline.AnimalsWarmupPath, CommonContentPipeline.AnimalsGroup),
                     })
            {
                var entry = Entry(path);
                Assert.IsNotNull(entry, path);
                Assert.AreEqual(group, entry.parentGroup.Name);
                Assert.IsTrue(entry.labels.Contains(CommonContentPipeline.WarmupLabel), path + " is found by the boot warmup.");
                var set = AssetDatabase.LoadAssetAtPath<ShaderWarmupSet>(path);
                Assert.IsNotEmpty(set.collections, path + " has traced states.");
                Assert.IsNotNull(set.variants, path + " has its fallback variants.");
            }
        }

        [Test]
        public void ScenesBuiltIntoThePlayer_HoldKeys_NotTheSharedAssets()
        {
            foreach (var scene in EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path))
            {
                var dependencies = AssetDatabase.GetDependencies(scene, true);
                foreach (var shared in new[]
                         {
                             CommonContentPipeline.CatalogPath, CommonContentPipeline.HatchPath, CommonContentPipeline.WorldWarmupPath,
                             CommonContentPipeline.AnimalsWarmupPath,
                         }.Concat(CommonContentPipeline.WorldShaders()))
                    Assert.IsFalse(dependencies.Contains(shared), $"{scene} must not reference {shared} (it would be copied into the player).");
            }
        }

        [Test]
        public void NothingIsDuplicated_BetweenBundles_OrBetweenBundlesAndScenes()
        {
            foreach (var rule in new AnalyzeRule[] { new CheckBundleDupeDependencies(), new CheckSceneDupeDependencies() })
            {
                var results = rule.RefreshAnalysis(Settings);
                var issues = results.Where(r => r.severity != MessageType.None && r.resultName != "No issues found").Select(r => r.resultName).ToList();
                rule.ClearAnalysis();
                Assert.IsEmpty(issues, rule.ruleName + ":\n" + string.Join("\n", issues));
            }
        }
    }
}
