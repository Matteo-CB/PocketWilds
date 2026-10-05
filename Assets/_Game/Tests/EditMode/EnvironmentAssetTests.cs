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
            // Small props are merged per palette and density class: density is measured in vertices.
            var chunk = Chunk();
            int small = 0, secondary = 0;
            for (int i = 0; i < chunk.BindingCount; i++)
            {
                var binding = chunk.GetBinding(i);
                var mesh = binding.renderer.GetComponent<MeshFilter>().sharedMesh;
                bool isSmall = mesh.name.StartsWith("MSH_Chunk_") || mesh.name.StartsWith("GL_GrassTuft_") || mesh.name.StartsWith("GL_Flower_");
                if (isSmall) small += mesh.vertexCount;
                if (binding.secondary)
                {
                    secondary += mesh.vertexCount;
                    Assert.IsTrue(isSmall, mesh.name + " is structural and must stay on Low.");
                }
            }
            float ratio = secondary / (float)small;
            Assert.That(ratio, NUnit.Framework.Is.InRange(0.35f, 0.5f), "Biomes doc: Low reduces flower/grass density by 35 to 50%.");
        }

        [Test]
        public void SmallProps_AreMergedPerPalette_WithPerInstanceBrightness()
        {
            var chunk = Chunk();
            var renderers = chunk.GetComponentsInChildren<MeshRenderer>(true);
            Assert.LessOrEqual(renderers.Length, 50, "T22: grass and flowers merged, about a dozen draws instead of ~190.");
            int merged = 0;
            for (int i = 0; i < chunk.BindingCount; i++)
            {
                var binding = chunk.GetBinding(i);
                var mesh = binding.renderer.GetComponent<MeshFilter>().sharedMesh;
                if (!mesh.name.StartsWith("MSH_Chunk_"))
                {
                    Assert.IsFalse(binding.vertexBrightness, mesh.name);
                    continue;
                }
                merged++;
                Assert.IsTrue(binding.vertexBrightness, mesh.name + " keeps each instance's brightness in uv.y.");
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, binding.renderer.shadowCastingMode);
                var uvs = new System.Collections.Generic.List<Vector2>();
                mesh.GetUVs(0, uvs);
                Assert.AreEqual(mesh.vertexCount, uvs.Count);
                float min = 1f, max = 0f;
                foreach (var uv in uvs)
                {
                    min = Mathf.Min(min, uv.y);
                    max = Mathf.Max(max, uv.y);
                }
                Assert.GreaterOrEqual(min, 5f / 15f - 1e-4f);
                Assert.LessOrEqual(max, 11f / 15f + 1e-4f);
            }
            Assert.Greater(merged, 4);
            uint packed = EnvironmentVisualPacking.Pack(42, 8, true);
            Assert.IsTrue(EnvironmentVisualPacking.UsesVertexBrightness(packed));
            Assert.AreEqual(42, EnvironmentVisualPacking.PaletteRow(packed));
            Assert.AreEqual(8, EnvironmentVisualPacking.Brightness(packed));
            Assert.IsFalse(EnvironmentVisualPacking.UsesVertexBrightness(EnvironmentVisualPacking.Pack(42, 8)));
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
