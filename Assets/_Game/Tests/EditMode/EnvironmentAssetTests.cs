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
            // T39: the tall grass props draw with the meadow tufts' own material (the scatter's, already in every frame).
            var scatterMaterial = Chunk().Scatter != null && Chunk().Scatter.kit != null ? Chunk().Scatter.kit.material : null;
            var materials = Chunk().GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial).Where(m => m != scatterMaterial)
                .Distinct().ToList();
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

        private static WorldChunk Meadow() => AssetDatabase.LoadAssetAtPath<WorldChunk>(PocketWilds.Editor.Environment.GrasslandPipeline.MeadowChunkPath);

        [Test]
        public void LowQuality_HidesOnlySecondaryDecor_AndKeepsTreesRocksAndGround()
        {
            // T33: ground details thin out through the ScatterRenderer; Low only hides a few background props.
            foreach (var chunk in new[] { Chunk(), Meadow() })
            {
                int secondary = 0;
                for (int i = 0; i < chunk.BindingCount; i++)
                {
                    var binding = chunk.GetBinding(i);
                    if (!binding.secondary)
                        continue;
                    secondary++;
                    string name = binding.renderer.GetComponent<MeshFilter>().sharedMesh.name;
                    Assert.IsFalse(name.StartsWith("GL_Tree") || name.StartsWith("GL_Rock") || name.StartsWith("GL_Ground"),
                        name + " is structural and must stay on Low (doc 07: Fluide keeps silhouettes).");
                }
                Assert.Greater(secondary, 0, chunk.name);
            }
        }

        [Test]
        public void GroundDetails_AreInstancedScatter_NotGameObjects()
        {
            foreach (var chunk in new[] { Chunk(), Meadow() })
            {
                var renderers = chunk.GetComponentsInChildren<MeshRenderer>(true);
                Assert.LessOrEqual(renderers.Length, 50, chunk.name);
                foreach (var renderer in renderers)
                {
                    string name = renderer.GetComponent<MeshFilter>().sharedMesh.name;
                    Assert.IsFalse(name.StartsWith("MSH_Chunk_") || name.StartsWith("GL_GrassTuft_") || name.StartsWith("GL_GrassWisp_")
                        || name.StartsWith("GL_Flower_") || name.StartsWith("GL_Pebble_"), name + ": ground details belong to the ScatterRenderer (doc 07).");
                }
                var scatter = chunk.Scatter;
                Assert.IsNotNull(scatter, chunk.name);
                Assert.IsNotNull(scatter.kit);
                Assert.AreEqual("PocketWilds/FoliageWind", scatter.kit.material.shader.name);
                Assert.IsTrue(scatter.kit.material.enableInstancing);
                // Doc 07: up to about 4,000 ground detail instances visible on Magnifique.
                Assert.That(scatter.Count, NUnit.Framework.Is.InRange(2000, 4000), chunk.name);
                Assert.AreEqual(scatter.Count, scatter.scales.Length);
                int expected = 0;
                foreach (var group in scatter.groups)
                {
                    Assert.AreEqual(expected, group.start, "Groups are contiguous and sorted.");
                    Assert.That(group.kind, NUnit.Framework.Is.InRange(0, scatter.kit.kinds.Length - 1));
                    expected += group.count;
                }
                Assert.AreEqual(scatter.Count, expected);
                for (int i = 0; i < scatter.Count; i++)
                {
                    var p = scatter.placements[i];
                    Assert.LessOrEqual(Mathf.Abs(p.x), 7.05f, chunk.name + " instance " + i);
                    Assert.LessOrEqual(Mathf.Abs(p.z), 7.05f, chunk.name + " instance " + i);
                    Assert.IsTrue(scatter.bounds.Contains(new Vector3(p.x, p.y, p.z)));
                }
            }
        }

        [Test]
        public void VertexBrightnessPacking_RoundTrips()
        {
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
