using System.Linq;
using NUnit.Framework;
using PocketWilds.Editor.Build;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T34C ED-010 and gate 33: the release content is clean, and the validator refuses what must never ship
    /// (benchmark scenes, debug content, development assemblies left in a release).
    /// </summary>
    public class BuildValidatorTests
    {
        [Test]
        public void ReleaseContent_IsClean()
        {
            var result = BuildValidator.Validate(true, true, BuildValidator.BuildScenes());
            Assert.IsFalse(result.HasErrors, BuildValidator.Summary(result));
            Assert.AreEqual(0, result.Warnings, BuildValidator.Summary(result));
            Assert.Greater(result.Shipped.Count, 200, "The walk reaches the scenes and every Addressables entry.");
            Assert.IsTrue(result.Shipped.Contains("Assets/_Game/Art/Shaders/SH_AnimalMaster.shader"), "Through the CommonAnimals group.");
            Assert.IsTrue(result.Shipped.Contains("Assets/_Game/Art/Shaders/SH_GroundMaster.shader"), "Through the Bootstrap group.");
            Assert.IsFalse(result.Shipped.Any(p => p.EndsWith(".cs")), "Scripts and their editor icons are never followed.");
        }

        [Test]
        public void BenchmarkScene_IsRefusedInARelease_ButAllowedInDevelopment()
        {
            var scenes = BuildValidator.BuildScenes().Append("Assets/_Game/Scenes/Benchmarks/SC_Benchmark_Animals.unity").ToArray();
            var release = BuildValidator.Validate(true, true, scenes);
            Assert.IsTrue(release.HasErrors);
            Assert.IsTrue(release.Issues.Any(i => i.Rule == "scenes" && i.Path.EndsWith("SC_Benchmark_Animals.unity")));
            Assert.IsTrue(release.Issues.Any(i => i.Rule == "forbidden"), "Its benchmark content is flagged too.");

            var development = BuildValidator.Validate(false, true, scenes);
            Assert.IsFalse(development.HasErrors, "A development build only reports: " + BuildValidator.Summary(development));
        }

        [Test]
        public void RequiredScenes_MustShip()
        {
            var result = BuildValidator.Validate(true, true, new[] { "Assets/_Game/Scenes/SC_Bootstrap.unity" });
            Assert.IsTrue(result.Issues.Any(i => i.Rule == "scenes" && i.Path.EndsWith("SC_VerticalSlice_Grassland.unity")));
        }

        [Test]
        public void DevelopmentAssemblies_AreCompiledOutOfReleases()
        {
            foreach (var path in BuildValidator.DevelopmentOnlyAssemblies)
                StringAssert.Contains("DEVELOPMENT_BUILD", System.IO.File.ReadAllText(path), path);
        }

        [Test]
        public void ResourcesFolders_AreWalked_AndTestRunFilesNeverShip()
        {
            Assert.IsTrue(BuildValidator.ResourcesContent().Contains("Assets/TextMesh Pro/Resources/TMP Settings.asset"),
                "Every project Resources folder ships, referenced or not.");
            foreach (var path in BuildValidator.PerformanceTestRunFiles)
                Assert.IsTrue(BuildValidator.ForbiddenParts.Any(part => path.Contains(part)), path + " is test content.");
        }
    }
}
