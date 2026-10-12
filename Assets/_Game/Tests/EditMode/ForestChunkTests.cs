using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.Validation;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T50 Forêt Magnifique, the built Forest (ForestPipeline, menu « Build Forest Magnifique (T50) »): seven chunks on the strip
    /// with ground, scatter, three clear animal spawns, firefly and bird anchors, links; renderers bound to registered palettes on
    /// the shared world materials; the kit within its budgets on baked meshes; nothing entering anything from the ForestEdge to
    /// the Lake side; the ForestEdge open east on the Entry's seam. Ignored until the pipeline has run.
    /// </summary>
    public class ForestChunkTests
    {
        private static List<(string address, string path, Vector3 origin, WorldChunk chunk)> Forest()
        {
            var result = new List<(string, string, Vector3, WorldChunk)>();
            foreach (var (address, path, origin) in ForestPipeline.ChunkEntries)
            {
                var chunk = AssetDatabase.LoadAssetAtPath<WorldChunk>(path);
                if (chunk == null)
                    Assert.Ignore($"{path} is made by 'Pocket Wilds/Environment/Build Forest Magnifique (T50)'.");
                result.Add((address, path, origin, chunk));
            }
            return result;
        }

        private static (string address, string path, Vector3 origin) ForestEdge =>
            GrasslandPipeline.StripChunkEntries.First(e => e.address == "chunk.grassland.forestedge");

        [Test]
        public void Chunks_ContinueTheStrip_WithGroundScatterSpawnsAndLifeAnchors()
        {
            var chunks = Forest();
            Assert.AreEqual(7, chunks.Count, "Entry, DeepForest, Clearing, Hollow, Brook, MushroomCorner, LakeTransition.");
            for (int i = 0; i < chunks.Count; i++)
            {
                var (address, path, origin, chunk) = chunks[i];
                Assert.AreEqual(112f + 14f * i, origin.x, 1e-4f, address);
                Assert.AreEqual(Quaternion.identity, chunk.transform.rotation, "Chunks are only translated.");
                Assert.IsNotNull(chunk.Scatter, address + ": scatter data.");
                Assert.Greater(chunk.Scatter.Count, 300, address + ": a living forest floor.");
                Assert.LessOrEqual(chunk.Scatter.Count, AssetValidator.ScatterMaxInstancesPerChunk, address);
                Assert.AreEqual(3, chunk.AnimalSpawns.Length, address + ": three animal spawns.");
                Assert.GreaterOrEqual(chunk.VfxAnchors.Count(a => a.name.StartsWith("VFX_Butterfly")), 2, address + ": firefly spots (season fireflies).");
                Assert.IsTrue(chunk.VfxAnchors.Any(a => a.name.StartsWith(ForestPipeline.BirdAnchorPrefix)), address + ": canopy birds.");
                bool clearing = address == "chunk.forest.clearing";
                Assert.AreEqual(clearing, chunk.VfxAnchors.Any(a => a.name.StartsWith(ForestPipeline.GladeButterflyPrefix)), address + ": butterflies only in the Clearing.");
                Assert.LessOrEqual(chunk.GetComponentsInChildren<MeshRenderer>(true).Length, 50, address);
                Assert.Greater(chunk.ObstacleCount, 10, address);
                Assert.IsTrue(chunk.GroundField != null && chunk.GroundField.IsValid, address + ": ground heights.");
                var links = chunk.transform.Find("ChunkLinks").Cast<Transform>().Select(t => t.name).ToList();
                CollectionAssert.Contains(links, "Link_W", address);
                Assert.AreEqual(i < chunks.Count - 1, links.Contains("Link_E"), address + (i < chunks.Count - 1 ? ": joined east." : ": the Lake rim stays closed."));
            }
        }

        [Test]
        public void Renderers_AreBoundToRegisteredPalettes_OnTheSharedWorldMaterials()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var known = new HashSet<uint>(database.paletteSets.Where(s => s != null).SelectMany(s => s.palettes)
                .Where(p => p != null && !string.IsNullOrEmpty(p.sourceId)).Select(p => ContentId.FromSource(p.sourceId).Value));
            var allowed = new HashSet<Material>(AssetValidator.EnvironmentMaterialPaths.Select(AssetDatabase.LoadAssetAtPath<Material>));
            foreach (var (address, _, _, chunk) in Forest())
            {
                var bound = new HashSet<Renderer>();
                for (int i = 0; i < chunk.BindingCount; i++)
                {
                    var binding = chunk.GetBinding(i);
                    bound.Add(binding.renderer);
                    Assert.IsTrue(known.Contains(binding.paletteId), $"{address}/{binding.renderer.name}: palette {binding.paletteId} is not registered (run the Biome Wizard).");
                }
                foreach (var renderer in chunk.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.IsTrue(bound.Contains(renderer), $"{address}/{renderer.name} has no palette binding.");
                    Assert.IsTrue(renderer.sharedMaterials.Length == 1 && allowed.Contains(renderer.sharedMaterial), $"{address}/{renderer.name}: shared world material only.");
                }
            }
        }

        [Test]
        public void Kit_PassesTheValidator_BudgetsLodsAndBakedMeshes()
        {
            Forest();
            var errors = AssetValidator.ValidateForest().Where(i => i.Severity == IssueSeverity.Error).ToList();
            Assert.IsEmpty(errors, string.Join("\n", errors.Select(e => $"{e.AssetPath} [{e.Rule}] {e.Message}")));
        }

        [Test]
        public void Props_NeverEnterEachOther_FromTheForestEdgeToTheLake()
        {
            var chunks = Forest();
            var names = new HashSet<string>(chunks.Select(c => c.chunk.name)) { AssetDatabase.LoadAssetAtPath<GameObject>(ForestEdge.path).name };
            var overlaps = GrasslandPipeline.PropOverlaps().Where(o => names.Contains(o.a.Chunk) || names.Contains(o.b.Chunk)).ToList();
            Assert.IsEmpty(overlaps, string.Join("\n", overlaps.Select(o => $"{o.a.Chunk}/{o.a.Prop} x {o.b.Chunk}/{o.b.Prop}: {o.depth * 100f:F1} cm")));
        }

        [Test]
        public void Scatter_NeverGrowsInsideAProp_AndSolidDetailsKeepApart()
        {
            var entries = Forest().Select(c => (c.address, c.path, c.origin)).ToList();
            entries.Add(ForestEdge);
            var volumes = GrasslandPipeline.BuiltPropVolumes();
            foreach (var (address, path, origin) in entries)
            {
                var data = AssetDatabase.LoadAssetAtPath<WorldChunk>(path).Scatter;
                var outlines = volumes.Where(v => Mathf.Abs(v.Placed.Position.x - origin.x) < 11f).Select(v => v.Placed.Ground).Where(b => b != null).Select(b => b.Hull).ToList();
                var solids = new List<(Vector2 p, float r)>();
                foreach (var group in data.groups)
                {
                    var kind = data.kit.kinds[group.kind];
                    var b = kind.mesh.bounds;
                    float kindRadius = Mathf.Max(Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z)));
                    bool solid = ForestPipeline.IsSolidScatter(kind.id) || kind.id == "detail.mushroom";
                    for (int i = group.start; i < group.start + group.count; i++)
                    {
                        var local = data.placements[i];
                        var p = new Vector2(local.x + origin.x, local.z + origin.z);
                        float r = kindRadius * data.scales[i];
                        foreach (var hull in outlines)
                            Assert.GreaterOrEqual(CollisionProxies.HullDistance(hull, p), r - 0.002f, $"{address}: {kind.id} at ({local.x:F2}, {local.z:F2}) grows inside a prop.");
                        if (solid)
                            solids.Add((p, r));
                    }
                }
                for (int i = 0; i < solids.Count; i++)
                    for (int j = i + 1; j < solids.Count; j++)
                        Assert.GreaterOrEqual((solids[i].p - solids[j].p).magnitude, solids[i].r + solids[j].r - 0.002f,
                            $"{address}: two solid details enter each other at {solids[i].p}.");
            }
        }

        [Test]
        public void SpawnPoints_StandClearOfEveryObstacle()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            Assert.IsTrue(catalog.TryGet("PF_Animal_Squirrel", out var squirrel));
            var body = squirrel.behavior.body;
            foreach (var (address, _, _, chunk) in Forest())
            {
                foreach (var spawn in chunk.AnimalSpawns)
                {
                    var p = spawn.localPosition;
                    for (int i = 0; i < chunk.ObstacleCount; i++)
                    {
                        var o = chunk.GetObstacle(i);
                        if (!o.BlocksWalker(p.y, body.height))
                            continue;
                        float gap = Mathf.Sqrt(CollisionMath.PointSegmentDistanceSq(new Vector2(p.x, p.z), o.a, o.b)) - o.radius - body.Reach;
                        Assert.GreaterOrEqual(gap, 0f, $"{address}/{spawn.name} inside a {o.kind} obstacle.");
                    }
                }
            }
        }

        [Test]
        public void ForestEdge_OpensEast_OnTheEntrysSeam()
        {
            var edge = AssetDatabase.LoadAssetAtPath<WorldChunk>(ForestEdge.path);
            Assert.IsNotNull(edge, ForestEdge.path);
            var links = edge.transform.Find("ChunkLinks").Cast<Transform>().Select(t => t.name).ToList();
            CollectionAssert.Contains(links, "Link_E", "The ForestEdge opens east toward the Forest.");
            Assert.Less(GrasslandPipeline.ForestEdgeThin(new Vector3(6.9f, 0f, 0f)), 0.3f, "Its grass thins into the forest moss.");
            Assert.AreEqual(1f, GrasslandPipeline.ForestEdgeThin(new Vector3(0f, 0f, 0f)), 1e-4f, "Its west two thirds keep their meadow.");

            Mesh Ground(string file, string name) => AssetDatabase.LoadAllAssetsAtPath(file).OfType<Mesh>().FirstOrDefault(m => m.name == name);
            var a = Ground($"{GrasslandPipeline.ModelsFolder}/GL_Grounds_T33B.fbx", "GL_Ground_ForestEdge");
            var b = Ground($"{ForestPipeline.ModelsFolder}/FO_Grounds.fbx", "FO_Ground_Entry");
            Assert.IsNotNull(a, "GL_Ground_ForestEdge");
            Assert.IsNotNull(b, "FO_Ground_Entry");
            // ForestEdge's east edge (x = +7) against the Entry's west edge (x = -7), in world space (14 m apart).
            // Distinct positions (a vertex split for its normals counts once).
            List<Vector3> Distinct(IEnumerable<Vector3> points) =>
                points.GroupBy(v => new Vector3Int(Mathf.RoundToInt(v.x * 1000f), Mathf.RoundToInt(v.y * 1000f), Mathf.RoundToInt(v.z * 1000f))).Select(g => g.First()).ToList();
            var east = Distinct(a.vertices.Where(v => Mathf.Abs(v.x - 7f) < 2e-3f && v.y > -0.02f));
            var west = Distinct(b.vertices.Where(v => Mathf.Abs(v.x + 7f) < 2e-3f && v.y > -0.02f).Select(v => v + new Vector3(14f, 0f, 0f)));
            Assert.GreaterOrEqual(east.Count, 15, "The joined side is a straight edge from corner to corner.");
            foreach (var v in east)
                Assert.Less(west.Min(w => (w - v).magnitude), 0.003f, $"ForestEdge seam vertex {v} meets the Entry at the same place and height.");
            foreach (var w in west)
                Assert.Less(east.Min(v => (w - v).magnitude), 0.003f, $"Entry seam vertex {w} meets the ForestEdge at the same place and height.");
        }

        [Test]
        public void Models_ImportAsStaticEnvironment()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ForestPipeline.ModelsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(ModelImporterMaterialImportMode.None, importer.materialImportMode, path + ": no imported material (shared world materials).");
                Assert.IsFalse(importer.isReadable, path + ": no CPU copy in the player.");
                Assert.IsFalse(importer.addCollider, path + ": no physics.");
            }
        }

        [Test]
        public void Kits_ShareTheWorldAssets_SunShaftsAndLifeWhereTheyBelong()
        {
            Forest();
            var weather = AssetDatabase.LoadAssetAtPath<WeatherKit>(ForestPipeline.WeatherKitPath);
            Assert.IsNotNull(weather, ForestPipeline.WeatherKitPath);
            Assert.AreEqual(QualityTier.Medium, weather.sunRaysFrom, "The Forest's shafts from Équilibrée.");
            CollectionAssert.AreEqual(new[] { new Vector2(105f, 119f), new Vector2(133f, 147f) }, weather.sunRayAreas, "Shafts in the Entry and the Clearing.");
            var life = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(ForestPipeline.LifeKitPath);
            Assert.IsNotNull(life, ForestPipeline.LifeKitPath);
            Assert.IsTrue(life.species.Any(s => s.behaviour == AmbientLifeBehaviour.Bird && s.anchorPrefix == ForestPipeline.BirdAnchorPrefix), "Canopy birds.");
            Assert.IsTrue(life.species.Where(s => s.behaviour == AmbientLifeBehaviour.Butterfly).All(s => s.anchorPrefix == ForestPipeline.GladeButterflyPrefix),
                "Butterflies in the Clearing only.");
            // The scene keeps the season looks: the Forest's kits never carry their firefly and drift assets (a duplicate).
            var seasonOnly = new[] { SeasonLookPipeline.FireflyMaterialPath, SeasonLookPipeline.DriftMaterialPath, SeasonLookPipeline.QuadPath };
            foreach (var kit in new[] { ForestPipeline.WeatherKitPath, ForestPipeline.LifeKitPath, ForestPipeline.ZoneKitPath, ForestPipeline.ScatterKitPath })
            {
                var dependencies = AssetDatabase.GetDependencies(kit, true);
                foreach (var asset in seasonOnly)
                    CollectionAssert.DoesNotContain(dependencies, asset, kit);
                Assert.IsFalse(dependencies.Any(d => d.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)), kit + ": baked meshes only.");
            }
            var zones = AssetDatabase.LoadAssetAtPath<ZoneKit>(ForestPipeline.ZoneKitPath);
            Assert.IsNotNull(zones, ForestPipeline.ZoneKitPath);
            Assert.IsTrue(zones.bushes.Length > 0 && zones.bushes.All(m => m != null && m.name.StartsWith("FO_Bush_")), "Hedges of Forest bushes.");
        }

        [Test]
        public void Trees_StandOnTheRims_TheMiddleOfTheFrameStaysOpen()
        {
            // T50 art pass: no big trunk in the middle band of the frame (the settle may move a tree a few centimetres).
            foreach (var (address, _, _, chunk) in Forest())
            {
                var trees = chunk.transform.Find("Props/Trees");
                Assert.IsNotNull(trees, address);
                foreach (Transform tree in trees)
                {
                    float z = tree.localPosition.z;
                    Assert.IsTrue(z >= ForestPipeline.TreeBackRim - 0.3f || z <= ForestPipeline.TreeNearRim + 0.3f,
                        $"{address}: {tree.name} at z {z:F2} stands in the middle of the frame.");
                }
                var mushrooms = chunk.transform.Find("Props/Mushrooms").Cast<Transform>().Where(t => t.name.StartsWith("PF_FO_Mushroom")).ToList();
                Assert.IsTrue(mushrooms.All(t => t.localScale.x >= ForestPipeline.MushroomScale * 0.85f), address + ": mushrooms at their readable size.");
            }
        }

        [Test]
        public void MushroomCorner_RingsTheStump_WithMushrooms()
        {
            var corner = Forest().First(c => c.address == "chunk.forest.mushroomcorner").chunk;
            var props = corner.transform.Find("Props");
            var stump = props.Find("Decor").Cast<Transform>().First(t => t.name.StartsWith("PF_FO_Stump"));
            int ring = props.Find("Mushrooms").Cast<Transform>().Count(t => t.name.StartsWith("PF_FO_Mushroom")
                && Mathf.Abs(Vector2.Distance(new Vector2(t.localPosition.x, t.localPosition.z), new Vector2(stump.localPosition.x, stump.localPosition.z)) - 1.3f) < 0.3f);
            Assert.GreaterOrEqual(ring, 8, "A ring of mushrooms around the stump.");
        }
    }
}
