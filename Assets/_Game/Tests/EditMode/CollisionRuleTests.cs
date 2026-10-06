using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.World;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T33C no-collision rule (Docs/Design/Collision_Rule.md): nothing ever enters anything. Props never enter each
    /// other (seams included), scatter never grows inside a prop, animals never overlap a prop, the water, the egg spot
    /// or each other, a newborn walks off the egg spot, insects and birds keep out of every volume and every animal.
    /// </summary>
    public class CollisionRuleTests
    {
        private const float Dt = 1f / 30f;
        private const float Tolerance = AnimalScheduler.ContactTolerance + 1e-4f;

        private GameObject _start;
        private GameObject _meadow;
        private AnimalVisualCatalog _catalog;

        private WorldChunk StartChunk => _start.GetComponent<WorldChunk>();

        [OneTimeSetUp]
        public void SetUp()
        {
            _start = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GrasslandPipeline.ChunkPath));
            _meadow = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GrasslandPipeline.MeadowChunkPath));
            _meadow.transform.position = GrasslandPipeline.MeadowOffset;
            _catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_start);
            Object.DestroyImmediate(_meadow);
        }

        private BehaviorProfile Profile(string visualKey)
        {
            Assert.IsTrue(_catalog.TryGet(visualKey, out var entry), visualKey);
            return entry.behavior;
        }

        private AnimalScheduler RefugeScheduler(int capacity)
        {
            var chunk = StartChunk;
            var scheduler = new AnimalScheduler(capacity, SchedulerSettings.ForQuality(Core.QualityTier.Medium, false), chunk.WanderArea);
            scheduler.SetGround(chunk.Ground);
            scheduler.SetObstacles(chunk.GetWorldObstacles());
            scheduler.SetInterests(chunk.GetWorldInterests());
            scheduler.SetEggSpot(chunk.EggSpawn.position, RefugeController.EggSpotRadius, RefugeController.EggBodyRadius);
            return scheduler;
        }

        /// <summary>Rabbits, squirrels and hedgehogs of every size, spread over the refuge's wander area.</summary>
        private List<int> Populate(AnimalScheduler scheduler, int count)
        {
            string[] keys = { "PF_Animal_Rabbit", "PF_Animal_Squirrel", "PF_Animal_Hedgehog" };
            var area = StartChunk.WanderArea;
            var handles = new List<int>();
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.39996f;
                float radius = area.Radius * 0.92f * Mathf.Sqrt((i + 0.5f) / count);
                var position = area.Center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                float scale = 0.8f * (i % 4 == 0 ? 1.2f : 1f) + 0.1f * (i % 5) / 4f + 0.2f;
                int handle = scheduler.Register(null, (uint)(i + 1), (uint)(7000 + i * 7919), position, i * 53f, Profile(keys[i % 3]), 0f, scale);
                Assert.AreNotEqual(AnimalScheduler.InvalidHandle, handle);
                handles.Add(handle);
            }
            return handles;
        }

        private static void AssertClear(AnimalScheduler scheduler, List<int> handles, GroundSurface ground, float time)
        {
            foreach (int h in handles)
            {
                if (!scheduler.IsValid(h))
                    continue;
                float clearance = scheduler.Clearance(h);
                if (clearance < -Tolerance)
                    Assert.Fail($"t={time:F2}s: animal {h} ({scheduler.GetState(h)}) overlaps by {-clearance * 100f:F1} cm at {scheduler.GetPosition(h)}.");
                var p = scheduler.GetPosition(h);
                Assert.AreEqual(ground.HeightAt(p.x, p.z), p.y, 1e-3f, "Animals stand on the ground heights.");
            }
        }

        // ---------------- props and scatter ----------------

        [Test]
        public void Props_NeverEnterEachOther_AcrossTheWholeStrip()
        {
            var overlaps = GrasslandPipeline.PropOverlaps();
            Assert.IsEmpty(overlaps, string.Join("\n", overlaps.Select(o => $"{o.a.Chunk}/{o.a.Prop} x {o.b.Chunk}/{o.b.Prop}: {o.depth * 100f:F1} cm")));
        }

        [Test]
        public void Scatter_NeverGrowsInsideAProp_AndSolidDetailsKeepApart()
        {
            var volumes = GrasslandPipeline.BuiltPropVolumes();
            foreach (var entry in WorldContentPipeline.GrasslandChunks)
            {
                var chunk = AssetDatabase.LoadAssetAtPath<WorldChunk>(entry.path);
                var data = chunk.Scatter;
                var outlines = volumes.Where(v => Mathf.Abs(v.Placed.Position.x - entry.origin.x) < 11f)
                    .Select(v => v.Placed.Ground).Where(b => b != null).Select(b => b.Hull).ToList();
                var solids = new List<(Vector2 p, float r)>();
                int inside = 0;
                string first = null;
                foreach (var group in data.groups)
                {
                    var kind = data.kit.kinds[group.kind];
                    var b = kind.mesh.bounds;
                    float kindRadius = Mathf.Max(Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z)));
                    bool solid = kind.id.StartsWith("flower.") || kind.id.StartsWith("detail.pebble") || kind.id == "detail.mushroom";
                    for (int i = group.start; i < group.start + group.count; i++)
                    {
                        var local = data.placements[i];
                        var p = new Vector2(local.x + entry.origin.x, local.z + entry.origin.z);
                        float r = kindRadius * data.scales[i];
                        foreach (var hull in outlines)
                        {
                            if (CollisionProxies.HullDistance(hull, p) < r - 0.002f)
                            {
                                inside++;
                                first ??= $"{kind.id} at ({local.x:F2}, {local.z:F2})";
                                break;
                            }
                        }
                        if (solid)
                            solids.Add((p, r));
                    }
                }
                Assert.AreEqual(0, inside, $"{entry.address}: scatter inside a prop, first {first}.");
                for (int i = 0; i < solids.Count; i++)
                    for (int j = i + 1; j < solids.Count; j++)
                        Assert.GreaterOrEqual((solids[i].p - solids[j].p).magnitude, solids[i].r + solids[j].r - 0.002f,
                            $"{entry.address}: two flowers, pebbles or mushrooms enter each other at {solids[i].p}.");
            }
        }

        [Test]
        public void Chunks_CarryObstaclesAndGround_SpawnPointsAndEggSpotStandClear()
        {
            var body = Profile("PF_Animal_Squirrel").body;
            foreach (var entry in WorldContentPipeline.GrasslandChunks)
            {
                var chunk = AssetDatabase.LoadAssetAtPath<WorldChunk>(entry.path);
                Assert.Greater(chunk.ObstacleCount, 10, entry.address);
                Assert.IsTrue(chunk.GroundField != null && chunk.GroundField.IsValid, entry.address + ": ground heights.");
                Assert.AreEqual(Quaternion.identity, chunk.transform.rotation, "Chunks are only translated.");
                void Check(Transform point, float radius)
                {
                    var p = point.localPosition;
                    for (int i = 0; i < chunk.ObstacleCount; i++)
                    {
                        var o = chunk.GetObstacle(i);
                        if (!o.BlocksWalker(p.y, body.height))
                            continue;
                        float gap = Mathf.Sqrt(CollisionMath.PointSegmentDistanceSq(new Vector2(p.x, p.z), o.a, o.b)) - o.radius - radius;
                        Assert.GreaterOrEqual(gap, 0f, $"{entry.address}/{point.name} inside a {o.kind} obstacle.");
                    }
                }
                foreach (var spawn in chunk.AnimalSpawns)
                    Check(spawn, body.Reach);
                if (chunk.EggSpawn != null)
                    Check(chunk.EggSpawn, RefugeController.EggSpotRadius);
            }
        }

        [Test]
        public void TurningParts_NeverSweepThroughTheirProp()
        {
            int checkedProps = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { GrasslandPipeline.PrefabsFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                var rotor = filters.FirstOrDefault(f => f.name.Contains("_Sails_") && f.name.EndsWith("LOD0"));
                if (rotor == null)
                    continue;
                checkedProps++;
                var root = prefab.transform;
                var hub = root.InverseTransformPoint(rotor.transform.position);
                var toRoot = root.worldToLocalMatrix * rotor.transform.localToWorldMatrix;
                // The rotor turns around its local Z: the blades (beyond the hub) sweep a disc between two depths.
                float sweep = 0f, bladeFront = float.MaxValue, bladeBack = float.MinValue;
                foreach (var v in rotor.sharedMesh.vertices)
                {
                    float radial = new Vector2(v.x, v.y).magnitude;
                    sweep = Mathf.Max(sweep, radial);
                    if (radial < 0.3f)
                        continue;
                    var p = toRoot.MultiplyPoint3x4(v);
                    bladeFront = Mathf.Min(bladeFront, p.z);
                    bladeBack = Mathf.Max(bladeBack, p.z);
                }
                foreach (var body in filters.Where(f => !f.name.Contains("_Sails_") && f.name.EndsWith("LOD0")))
                {
                    var bodyToRoot = root.worldToLocalMatrix * body.transform.localToWorldMatrix;
                    foreach (var v in body.sharedMesh.vertices)
                    {
                        var p = bodyToRoot.MultiplyPoint3x4(v);
                        if (new Vector2(p.x - hub.x, p.y - hub.y).magnitude > sweep + 0.05f)
                            continue;
                        Assert.IsTrue(p.z > bladeBack + 0.02f || p.z < bladeFront - 0.02f,
                            $"{prefab.name}: {body.name} at {p} lies in the sails' sweep ({bladeFront:F2} to {bladeBack:F2}).");
                    }
                }
            }
            Assert.Greater(checkedProps, 0, "The windmill is checked.");
        }

        [Test]
        public void Bodies_AreMeasured_AndGreetingNosesTouchWithoutOverlap()
        {
            foreach (var key in new[] { "PF_Animal_Rabbit", "PF_Animal_Squirrel", "PF_Animal_Hedgehog" })
            {
                var profile = Profile(key);
                var measured = AnimalPipeline.MeasureBody(_catalog.entries.First(e => e.visualKey == key).prefab.gameObject);
                Assert.AreEqual(measured.front, profile.body.front, 1e-3f, key);
                Assert.AreEqual(measured.back, profile.body.back, 1e-3f, key);
                Assert.AreEqual(measured.halfWidth, profile.body.halfWidth, 1e-3f, key);
                Assert.That(profile.body.halfWidth, NUnit.Framework.Is.InRange(0.1f, 0.25f), key);
                Assert.GreaterOrEqual(profile.socialGap, 2f * profile.body.front, key + ": noses never overlap.");
            }
        }

        // ---------------- animals ----------------

        [Test]
        public void Refuge_ThirtyAnimals_TwoMinutes_NoBodyEverEntersAnything()
        {
            var scheduler = RefugeScheduler(40);
            var handles = Populate(scheduler, 30);
            var ground = StartChunk.Ground;
            float time = 0f;
            AssertClear(scheduler, handles, ground, time);
            for (int f = 0; f < 120 * 30; f++)
            {
                time += Dt;
                scheduler.Tick(time, Dt);
                AssertClear(scheduler, handles, ground, time);
            }
            Assert.Greater(scheduler.TotalStateChanges, 30 * 120 / 8, "The refuge stays alive.");
            Assert.Greater(scheduler.InterestVisits, 0, "Rocks and flowers are still visited.");
            Assert.Greater(scheduler.SocialGreetings, 0, "Greetings still happen.");
        }

        [Test]
        public void Walks_GoAroundRocks_ThroughAWaypoint()
        {
            var scheduler = new AnimalScheduler(2, SchedulerSettings.ForQuality(Core.QualityTier.Medium, false), new WanderArea(Vector3.zero, 4f));
            // A rock right between the animal and the point it wants to inspect.
            scheduler.SetObstacles(new[] { new Obstacle(Vector2.zero, Vector2.zero, 0.5f, 0f, 0f, 0.6f, ObstacleKind.Solid) });
            scheduler.SetInterests(new[] { new InterestPoint(new Vector3(1.4f, 0f, 0f), 0.3f, InterestKind.Inspect) });
            var profile = new BehaviorProfile
            {
                idleWeight = 0f, idleLookWeight = 0f, wanderWeight = 0f, eatWeight = 0f, inspectWeight = 1f, sleepWeight = 0f, socialWeight = 0f,
                idleSeconds = new Vector2(0.2f, 0.4f), interestRadius = 3f,
            };
            int h = scheduler.Register(null, 1, 1, new Vector3(-1.4f, 0f, 0f), 90f, profile, 0f);
            float time = 0f;
            for (int f = 0; f < 20 * 30 && scheduler.InterestVisits == 0; f++)
            {
                time += Dt;
                scheduler.Tick(time, Dt);
                Assert.GreaterOrEqual(scheduler.Clearance(h), -Tolerance, $"t={time:F2}");
            }
            Assert.Greater(scheduler.Detours, 0, "The walk went around the rock.");
            Assert.Greater(scheduler.InterestVisits, 0, "And reached the point behind it.");
        }

        [Test]
        public void Hatch_NewbornWalksOffTheEggSpot_BeforeTheNextEgg()
        {
            var scheduler = RefugeScheduler(24);
            var handles = Populate(scheduler, 16);
            Assert.IsTrue(scheduler.IsEggSpotClear(), "Nobody stands on the egg spot.");
            var egg = StartChunk.EggSpawn.position;
            int newborn = scheduler.Register(null, 99, 99, egg, 180f, Profile("PF_Animal_Squirrel"), 0f, 1.2f, emerging: true);
            handles.Add(newborn);
            Assert.AreEqual(Flat(egg), Flat(scheduler.GetPosition(newborn)), "The newborn appears where it hatched.");
            Assert.IsTrue(scheduler.IsEmerging(newborn));
            Assert.IsFalse(scheduler.IsEggSpotClear());
            float time = 0f;
            while (!scheduler.IsEggSpotClear() && time < 6f)
            {
                time += Dt;
                scheduler.Tick(time, Dt);
                AssertClear(scheduler, handles, StartChunk.Ground, time);
            }
            Assert.IsTrue(scheduler.IsEggSpotClear(), "The newborn left the egg within 6 s.");
            TestContext.WriteLine($"Egg free after {time:F2} s.");
            Assert.Less(time, 1.2f, "The newborn bounds off: the next egg is not kept waiting.");
            for (int f = 0; f < 90 && scheduler.IsEmerging(newborn); f++)
            {
                time += Dt;
                scheduler.Tick(time, Dt);
                AssertClear(scheduler, handles, StartChunk.Ground, time);
            }
            Assert.IsFalse(scheduler.IsEmerging(newborn), "Then it leaves the whole egg spot.");
        }

        [Test]
        public void Scheduler_WithObstacles_DoesNotAllocate_AndStaysCheap()
        {
            var scheduler = RefugeScheduler(60);
            Populate(scheduler, 50);
            float time = 0f;
            for (int f = 0; f < 60; f++)
                scheduler.Tick(time += Dt, Dt);
            Assert.That(() =>
            {
                for (int f = 0; f < 300; f++)
                    scheduler.Tick(time += Dt, Dt);
            }, Is.Not.AllocatingGCMemory());
            var watch = System.Diagnostics.Stopwatch.StartNew();
            const int Frames = 600;
            for (int f = 0; f < Frames; f++)
                scheduler.Tick(time += Dt, Dt);
            double ms = watch.Elapsed.TotalMilliseconds / Frames;
            TestContext.WriteLine($"Scheduler tick with 50 animals and {scheduler.ObstacleCount} obstacles: {ms:F3} ms");
            Assert.Less(ms, 0.5, "Editor bound for 50 animals (the benchmark measures the player).");
        }

        // ---------------- ambient life ----------------

        [Test]
        public void AmbientLife_NeverEntersAPropAnAnimalOrItsKind()
        {
            var kit = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(GrasslandPipeline.LifeKitPath);
            var life = new AmbientLife(kit, null, 1f);
            life.Add(StartChunk);
            life.Add(_meadow.GetComponent<WorldChunk>());
            var scheduler = RefugeScheduler(30);
            Populate(scheduler, 20);
            life.SetAnimals(scheduler);
            float time = 0f;
            float worst = float.MaxValue;
            string where = null;
            for (int f = 0; f < 90 * 30; f++)
            {
                time += Dt;
                scheduler.Tick(time, Dt);
                life.Tick(Dt, time);
                float clearance = life.MinClearance(out string closest);
                if (clearance < worst)
                {
                    worst = clearance;
                    where = $"t={time:F2}s {closest}";
                }
            }
            TestContext.WriteLine($"Ambient life worst clearance over 90 s: {worst * 100f:F2} cm, {where}");
            Assert.GreaterOrEqual(worst, -0.005f, "Insects and birds never enter a bush, a crown, a rock, an animal or one another: " + where);
        }

        private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);
    }
}
