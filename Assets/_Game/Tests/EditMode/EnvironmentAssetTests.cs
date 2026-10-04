using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Validation;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T15 gate: the Grassland kit assembles a chunk with very few materials and data-driven palettes.</summary>
    public class EnvironmentAssetTests
    {
        private static WorldChunk Chunk() => AssetDatabase.LoadAssetAtPath<WorldChunk>(AssetValidator.GrasslandChunkPath);

        [Test]
        public void GrasslandKitAndChunk_PassAssetValidator()
        {
            var errors = AssetValidator.ValidateEnvironment().Where(i => i.Severity == IssueSeverity.Error).ToList();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void StartChunk_UsesAtMostTwoMaterials()
        {
            var materials = Chunk().GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial).Distinct().ToList();
            Assert.LessOrEqual(materials.Count, 2, string.Join(", ", materials.Select(m => m.name)));
        }

        [Test]
        public void EveryBinding_ResolvesToAShippedPalette()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            var chunk = Chunk();
            for (int i = 0; i < chunk.BindingCount; i++)
            {
                var binding = chunk.GetBinding(i);
                Assert.IsTrue(registry.Palettes.TryGet(new ContentId(binding.paletteId), out _), binding.renderer.name);
            }
        }

        [Test]
        public void LowQuality_DropsAboutHalfOfTheSmallProps_AndKeepsTreesAndRocks()
        {
            var chunk = Chunk();
            int small = 0, secondary = 0;
            for (int i = 0; i < chunk.BindingCount; i++)
            {
                var binding = chunk.GetBinding(i);
                var mesh = binding.renderer.GetComponent<MeshFilter>().sharedMesh.name;
                bool isSmall = mesh.StartsWith("GL_GrassTuft_") || mesh.StartsWith("GL_Flower_");
                if (isSmall) small++;
                if (binding.secondary)
                {
                    secondary++;
                    Assert.IsTrue(isSmall, mesh + " is structural and must stay on Low.");
                }
            }
            float ratio = secondary / (float)small;
            Assert.That(ratio, NUnit.Framework.Is.InRange(0.35f, 0.5f), "Biomes doc: Low reduces flower/grass density by 35 to 50%.");
        }

        [Test]
        public void EnvironmentPacking_RoundTrips()
        {
            uint packed = EnvironmentVisualPacking.Pack(613, 11);
            Assert.AreEqual(613, EnvironmentVisualPacking.PaletteRow(packed));
            Assert.AreEqual(11, EnvironmentVisualPacking.Brightness(packed));
        }
    }
}
