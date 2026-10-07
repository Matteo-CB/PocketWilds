using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T35A, doc 02 Améliorations section 16 (PlayMode): open a zone of the Prairie in the real scene. The hedge and
    /// the sign close the next zone without entering any prop, the opening sinks the hedge, the walk area and the
    /// camera reach the new zone, animals walk in, nothing ever enters anything, and the steady state allocates nothing.
    /// </summary>
    public class ZoneOpeningPlayTests
    {
        private const float Tolerance = AnimalScheduler.ContactTolerance + 1e-4f;
        private static readonly ContentId Grassland = ContentId.FromSource("biome.grassland");
        private static readonly ContentId Meadow = ContentId.FromSource("zone.grassland.meadow");
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "PocketWildsPlayTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var bootstrap in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(bootstrap.gameObject);
            try
            {
                Directory.Delete(_saveDir, true);
            }
            catch (IOException)
            {
            }
        }

        [UnityTest]
        public IEnumerator OpeningTheMeadow_HedgeSinks_AnimalsWalkIn_NothingEntersAnything()
        {
#if UNITY_EDITOR
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = _saveDir,
                ContentOverride = database.CreateBuilder().Build(),
                VisualCatalogOverride = catalog,
                LoadTargetScene = true,
            };
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            float timeout = Time.realtimeSinceStartup + 20f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                if (Time.realtimeSinceStartup > timeout)
                    Assert.Fail("Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var refuge = entry.Refuge;
            var zones = entry.Zones;
            Assert.IsNotNull(zones, "The scene runs the zones of the Prairie.");
            Assert.IsNotNull(entry.ZoneKit, "The zone kit loads from Addressables.");
            Assert.AreEqual(1, zones.OpenZones, "A new profile has the arrival zone only.");

            // A small refuge: ten animals hatched like a player (each hatch skipped).
            context.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(1e6), EconomyReason.Debug);
            for (int i = 0; i < 10; i++)
            {
                refuge.RequestHatch();
                refuge.RequestHatch();
                float wait = Time.realtimeSinceStartup + 8f;
                while (refuge.Hatch.IsPlaying || refuge.WaitingEgg == null)
                {
                    if (Time.realtimeSinceStartup > wait)
                        Assert.Fail("Hatch did not finish.");
                    yield return null;
                }
            }
            Assert.AreEqual(10, refuge.VisibleAnimals);

            // The next zone's chunk loads (it is kept, like the open ones) and the hedge stands on the seam.
            float load = Time.realtimeSinceStartup + 10f;
            while (!zones.Boundary.IsPlaced)
            {
                if (Time.realtimeSinceStartup > load)
                    Assert.Fail("The hedge never stood: the meadow chunk did not load.");
                yield return null;
            }
            Assert.AreEqual(7f, zones.SeamX, 1e-3f);
            float covered = 0f;
            foreach (var piece in zones.Boundary.Obstacles)
                if (piece.radius < 0.5f || (piece.b - piece.a).sqrMagnitude > 0f)
                    covered += (piece.b - piece.a).magnitude + 2f * piece.radius;
            Assert.Greater(covered, 0.7f * 2f * RefugeZones.HalfDepth, $"A hedge across the entrance ({zones.Boundary.BushCount} pieces, {covered:F1} m).");
            Assert.IsTrue(zones.Boundary.HasSign, "The sign stands by the hedge.");
            AssertHedgeClear(entry, zones);
            Assert.LessOrEqual(refuge.WalkArea.Center.x + refuge.WalkArea.HalfSize.x, 7f - RefugeZones.SeamMargin + 1e-3f,
                "Animals stop before the hedge.");

            // Open it: mastery 1 and 250 Light.
            context.Progression.Mastery.AddXp(Grassland, 40);
            Assert.IsTrue(context.Progression.Zones.TryOpen(Meadow, out _));
            var scheduler = refuge.Scheduler;
            bool entered = false;
            float farthest = float.MinValue;
            string farthestState = null;
            float end = Time.realtimeSinceStartup + 25f;
            float runTime = 0f;
            while (Time.realtimeSinceStartup < end && runTime < 20f)
            {
                runTime += Time.deltaTime;
                for (int i = 0; i < scheduler.Count; i++)
                {
                    int h = scheduler.GetActiveHandle(i);
                    float clearance = scheduler.Clearance(h);
                    if (clearance < -Tolerance)
                        Assert.Fail($"Animal {h} ({scheduler.GetState(h)}) overlaps by {-clearance * 100f:F1} cm at {scheduler.GetPosition(h)}.");
                    if (scheduler.GetPosition(h).x > farthest)
                    {
                        farthest = scheduler.GetPosition(h).x;
                        farthestState = scheduler.GetState(h) + " at " + scheduler.GetPosition(h);
                    }
                    if (scheduler.GetPosition(h).x > 7.3f)
                        entered = true;
                }
                if (entered && zones.WalkZones == 2 && !zones.Boundary.IsMoving && runTime > 2f)
                    break;
                yield return null;
            }
            Assert.AreEqual(2, zones.OpenZones);
            Assert.AreEqual(2, zones.WalkZones, "The meadow joined the walk area.");
            Assert.Greater(refuge.WalkArea.Center.x + refuge.WalkArea.HalfSize.x, 20f, "The animals may walk across the meadow.");
            Assert.IsTrue(entered, $"Animals walked into the new zone (farthest: {farthestState}, area {refuge.WalkArea.Center} {refuge.WalkArea.HalfSize}; " +
                $"agents: {Agents(scheduler)}; obstacles near the seam: {SeamObstacles(scheduler)}).");
            Assert.AreEqual(RefugeController.ArrivalVisibleAnimals + 3, refuge.VisibleAllowance, "Three more animals may be seen.");
            entry.Zones.GetCameraRange(out _, out float maxX);
            Assert.Greater(maxX, 21f, "The camera reaches the next hedge.");

            // Steady state (the walk-in watch over): the scheduler, the zones and the boundary allocate nothing.
            float watch = Time.realtimeSinceStartup + 25f;
            while (zones.WalkingIn)
            {
                Assert.Less(Time.realtimeSinceStartup, watch, "The walk-in watch never ended.");
                yield return null;
            }
            for (int i = 0; i < 5; i++)
                yield return null;
            float t = Time.time;
            Assert.That(() =>
            {
                for (int i = 0; i < 120; i++)
                {
                    t += 1f / 60f;
                    scheduler.Tick(t, 1f / 60f);
                    zones.Tick(t);
                }
            }, Is.Not.AllocatingGCMemory());
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        private static string SeamObstacles(AnimalScheduler scheduler)
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var obstacles = (Obstacle[])typeof(AnimalScheduler).GetField("_obstacles", flags).GetValue(scheduler);
            int count = (int)typeof(AnimalScheduler).GetField("_obstacleCount", flags).GetValue(scheduler);
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++)
                if (obstacles[i].Center.x > 6f && obstacles[i].Center.x < 8.5f)
                    text.Append(obstacles[i].kind).Append(' ').Append(obstacles[i].a.ToString("F2")).Append('-').Append(obstacles[i].b.ToString("F2"))
                        .Append(" r").Append(obstacles[i].radius.ToString("F2")).Append("; ");
            return text.ToString();
        }

        private static string Agents(AnimalScheduler scheduler)
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < scheduler.Count; i++)
            {
                int h = scheduler.GetActiveHandle(i);
                text.Append(h).Append(' ').Append(scheduler.GetState(h)).Append(' ').Append(scheduler.GetPosition(h).ToString("F1"))
                    .Append("->").Append(scheduler.GetTarget(h).ToString("F1")).Append(" legs ").Append(scheduler.GetGoalLegs(h)).Append("; ");
            }
            return text.ToString();
        }

        /// <summary>No bush and no sign enters a prop or the water of either zone.</summary>
        private static void AssertHedgeClear(VerticalSliceEntry entry, RefugeZones zones)
        {
            var streamer = entry.Streamer;
            foreach (var address in new[] { "chunk.grassland.start", "chunk.grassland.meadow" })
            {
                var chunk = streamer.GetChunk(streamer.IndexOf(address));
                Assert.IsNotNull(chunk, address);
                var obstacles = chunk.GetWorldObstacles();
                foreach (var hedge in zones.Boundary.Obstacles)
                {
                    foreach (var o in obstacles)
                    {
                        if (!o.BlocksWalker(hedge.baseY, 1.5f))
                            continue;
                        float gap = CollisionMath.Gap(hedge.a, hedge.b, hedge.radius, in o);
                        Assert.GreaterOrEqual(gap, -0.002f, $"A hedge bush at {hedge.Center} enters a {o.kind} of {address} at {o.Center}.");
                    }
                }
            }
        }
    }
}
