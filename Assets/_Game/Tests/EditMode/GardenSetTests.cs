using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Editor.Activities;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T43 « Jardin »: the garden set and the plants built by GardenSetPipeline from agent GP's models (doc 01 Contenu,
    /// section 8; collision rule): five beds with their anchors, covers on the locked plots, every renderer bound to a
    /// palette, the 13 launch plants in three stages, every ripe plant inside its bed's soil, no prop in another, the
    /// triangle budgets, the framing under the overlay, the Addressables entries.
    /// </summary>
    public class GardenSetTests
    {
        private const string Run = " (run Pocket Wilds/Activities/Build Garden Set (T43))";

        private static GameObject SetPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GardenLayout.SetPrefabPath);
            Assert.IsNotNull(prefab, GardenLayout.SetPrefabPath + " missing" + Run);
            return prefab;
        }

        private static GardenPlantVisuals Visuals()
        {
            var visuals = AssetDatabase.LoadAssetAtPath<GardenPlantVisuals>(GardenLayout.PlantsAssetPath);
            Assert.IsNotNull(visuals, GardenLayout.PlantsAssetPath + " missing" + Run);
            return visuals;
        }

        [Test]
        public void SetPrefab_HasFiveBeds_TheirAnchors_AndTheProps()
        {
            var view = SetPrefab().GetComponent<GardenSetView>();
            Assert.IsNotNull(view, "no GardenSetView on the set");
            Assert.AreEqual(GardenLayout.PlotCount, view.beds.Length);
            Assert.AreEqual(GardenLayout.PlotCount, view.plantAnchors.Length);
            Assert.AreEqual(GardenLayout.PlotCount, view.lockedCovers.Length);
            Assert.AreEqual(GardenLayout.PlotCount, view.bedSizes.Length);
            for (int i = 0; i < GardenLayout.PlotCount; i++)
            {
                Assert.IsNotNull(view.beds[i], $"bed {i}");
                Assert.IsNotNull(view.plantAnchors[i], $"anchor {i}");
                Assert.IsTrue(view.plantAnchors[i].IsChildOf(view.beds[i]), $"anchor {i} belongs to its bed");
                Assert.AreEqual(GardenSetPipeline.SoilTop, view.plantAnchors[i].localPosition.y, 1e-4f, $"anchor {i} on the soil");
                Assert.Greater(view.bedSizes[i].x, 2f * GardenSetPipeline.SoilHalf.x, $"bed {i} touch area");
                Assert.Greater(view.bedSizes[i].y, 2f * GardenSetPipeline.SoilHalf.y, $"bed {i} touch area");
                Assert.AreEqual(GardenSetPipeline.CoveredByDefault(i), view.lockedCovers[i] != null, $"cover on plot {i}");
                if (i > 0)
                {
                    // Plot 0 at the front, each next one farther; consecutive plots on both sides of the path.
                    Assert.Greater(view.beds[i].localPosition.z, view.beds[i - 1].localPosition.z, $"plot {i} behind plot {i - 1}");
                    Assert.Less(view.beds[i].localPosition.x * view.beds[i - 1].localPosition.x, 0f, $"plots {i - 1} and {i} zigzag");
                }
            }
            Assert.IsNotNull(view.setCamera, "camera");
            Assert.IsNotNull(view.wateringCan, "watering can");
            Assert.IsNotNull(view.basket, "basket");
            Assert.IsNotNull(view.sun, "sun");
            Assert.AreEqual(LightType.Directional, view.sun.type);
        }

        [Test]
        public void EveryRenderer_IsBoundToAPalette()
        {
            foreach (var root in new[] { SetPrefab() }.Concat(Visuals().entries.SelectMany(e => new[] { e.stage1, e.stage2, e.stage3 })).Where(g => g != null))
            {
                var chunk = root.GetComponent<WorldChunk>();
                Assert.IsNotNull(chunk, root.name + ": no WorldChunk binding its palettes");
                var bound = new HashSet<Renderer>();
                for (int i = 0; i < chunk.BindingCount; i++)
                {
                    var binding = chunk.GetBinding(i);
                    Assert.AreNotEqual(0u, binding.paletteId, root.name + ": a binding without palette");
                    bound.Add(binding.renderer);
                }
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    Assert.IsTrue(bound.Contains(renderer), $"{root.name}: {renderer.name} has no palette binding");
            }
        }

        [Test]
        public void EveryLaunchPlant_HasThreeStages_Measured()
        {
            var visuals = Visuals();
            for (int i = 0; i < GardenSetPipeline.Plants.Length; i++)
            {
                string key = GardenSetPipeline.PlantKey(i);
                Assert.IsTrue(visuals.TryGet(key, out var entry), key + " has no visuals");
                Assert.IsNotNull(entry.stage1, key + " stage 1");
                Assert.IsNotNull(entry.stage2, key + " stage 2");
                Assert.IsNotNull(entry.stage3, key + " stage 3");
                Assert.Greater(entry.radius, 0.05f, key + " radius");
                Assert.Greater(entry.height, 0.05f, key + " height");
                bool tall = key == "plant.sunflower" || key == "plant.dwarf_apple";
                Assert.LessOrEqual(entry.height, tall ? 1.05f : 0.65f, key + " height");
            }
            Assert.AreEqual(GardenSetPipeline.Plants.Length, visuals.entries.Select(e => e.plantKey).Distinct().Count(), "one entry per plant");
        }

        [Test]
        public void EveryPlant_StaysInsideItsBedsSoil()
        {
            foreach (var entry in Visuals().entries)
                foreach (var stage in new[] { entry.stage1, entry.stage2, entry.stage3 })
                {
                    if (stage == null)
                        continue;
                    float slack = GardenSetPipeline.SoilSlack(stage);
                    Assert.GreaterOrEqual(slack, GardenSetPipeline.SoilMargin, $"{stage.name} reaches {slack * 100f:F1} cm from its bed's planks");
                }
        }

        [Test]
        public void NothingOnTheSet_EntersAnythingElse()
        {
            var prefab = SetPrefab();
            var footprints = new List<(string name, List<Vector2> hull)>();
            var root = prefab.transform;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var points = filter.sharedMesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToList();
                string name = filter.name;
                string owner = filter.transform.parent != null ? filter.transform.parent.name : "";
                if (name == GardenSetPipeline.Bed)
                    footprints.Add(("bed " + owner, GardenSetPipeline.Hull(points)));
                else if (name == GardenSetPipeline.Cover)
                    footprints.Add(("cover " + filter.transform.parent.parent.name, GardenSetPipeline.Hull(points)));
                else if (name == GardenSetPipeline.Path)
                    foreach (var stone in GardenSetPipeline.Components((filter.sharedMesh, matrix), Matrix4x4.identity, -0.004f))
                        footprints.Add(("stone", GardenSetPipeline.Hull(stone)));
                else if (name == GardenSetPipeline.Fence)
                    foreach (var part in points.GroupBy(p => Mathf.FloorToInt(p.x / 0.5f)))
                        footprints.Add(("fence", GardenSetPipeline.Hull(part.ToList())));
                else if (name == GardenSetPipeline.Can || name == GardenSetPipeline.Basket)
                    footprints.Add((name, GardenSetPipeline.Hull(points)));
                else if (owner == "Kit")
                    footprints.Add(("kit " + name, GardenSetPipeline.Hull(points)));
            }
            Assert.AreEqual(GardenLayout.PlotCount, footprints.Count(f => f.name.StartsWith("bed ")), "five beds measured");
            Assert.Greater(footprints.Count(f => f.name == "stone"), 8, "the path's stones measured");
            for (int i = 0; i < footprints.Count; i++)
                for (int j = i + 1; j < footprints.Count; j++)
                {
                    var (a, ha) = footprints[i];
                    var (b, hb) = footprints[j];
                    if (a == "fence" && b == "fence")
                        continue;
                    if ((a.StartsWith("bed ") && b == "cover " + a.Substring(4)) || (b.StartsWith("bed ") && a == "cover " + b.Substring(4)))
                        continue;
                    float gap = GardenSetPipeline.Gap(ha, hb);
                    Assert.GreaterOrEqual(gap, GardenSetPipeline.PropGap, $"{a} and {b} are {gap * 100f:F1} cm apart");
                }
        }

        [Test]
        public void TriangleBudgets_AreKept()
        {
            foreach (var entry in Visuals().entries)
                foreach (var stage in new[] { entry.stage1, entry.stage2, entry.stage3 })
                {
                    if (stage == null)
                        continue;
                    int tris = stage.GetComponentsInChildren<MeshFilter>(true).Sum(f => GardenSetPipeline.Triangles(f.sharedMesh));
                    Assert.That(tris, Is.InRange(GardenSetPipeline.PlantMinTriangles, GardenSetPipeline.PlantMaxTriangles), stage.name + " triangles");
                }
            var meshes = GardenSetPipeline.ModelMeshes(GardenSetPipeline.SetModel);
            Assert.AreEqual(GardenSetPipeline.SetObjects.Length, GardenSetPipeline.SetObjects.Count(n => GardenSetPipeline.Find(meshes, n).mesh != null),
                "every set model in " + GardenSetPipeline.SetModel + ".fbx");
            int set = GardenSetPipeline.SetObjects.Sum(n => GardenSetPipeline.Triangles(GardenSetPipeline.Find(meshes, n).mesh));
            Assert.LessOrEqual(set, GardenSetPipeline.SetPropsBudget, "the set's models together");
        }

        [Test]
        public void EveryBed_AndItsTallestRipePlant_ShowBetweenTheOverlayBars()
        {
            var view = SetPrefab().GetComponent<GardenSetView>();
            Assert.AreEqual(GardenSetPipeline.CameraFov, view.setCamera.fieldOfView, 1e-3f);
            Assert.Less((view.setCamera.transform.localPosition - GardenSetPipeline.CameraAt).magnitude, 1e-3f, "the camera the framing was measured with");
            var entries = Visuals().entries;
            var (bottom, top, side) = GardenSetPipeline.FramingMargins(entries.Max(e => e.height), entries.Max(e => e.radius));
            Assert.GreaterOrEqual(bottom, 0f, "a bed under the bottom sheet");
            Assert.GreaterOrEqual(top, 0f, "a bed or a ripe plant under the top bar");
            Assert.GreaterOrEqual(side, 0f, "a bed off the screen's side");
        }

        [Test]
        public void TheSetAndThePlants_AreAddressableInTheGardenGroup()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.IsNotNull(settings, "Addressables settings");
            var set = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(GardenLayout.SetPrefabPath));
            Assert.IsNotNull(set, "the set is not Addressable" + Run);
            Assert.AreEqual(GardenLayout.SetAddress, set.address);
            Assert.AreEqual(GardenLayout.Group, set.parentGroup.Name);
            var plants = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(GardenLayout.PlantsAssetPath));
            Assert.IsNotNull(plants, "the plants are not Addressable" + Run);
            Assert.AreEqual(GardenLayout.PlantsAddress, plants.address);
            Assert.AreEqual(GardenLayout.Group, plants.parentGroup.Name);
        }
    }
}
