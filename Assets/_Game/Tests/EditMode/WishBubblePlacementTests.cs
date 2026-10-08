using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.UI;
using PocketWilds.Editor.Vfx;
using PocketWilds.Editor.World;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T36 wish bubbles (plan sections 7.1 and 8.13; doc 06 screen 002; rule « aucune collision »,
    /// Docs/Design/Collision_Rule.md): placed bubbles never enter a prop band, a tree crown, an animal's body column, the
    /// egg's room or another bubble, at every scale they are drawn at; nearest to the camera first; a bubble with no free
    /// place waits; circles never overlap on screen and never cover another animal's face; the screen-size clamp keeps
    /// them readable at every zoom; the pass is deterministic and allocation-free. Also the built prefab, its wiring and
    /// H-VFX-070 VFX_WishDone (11 / 6 / 14 particles: soft bubbles, hearts, a ring).
    /// </summary>
    public class WishBubblePlacementTests
    {
        private const float Dt = 1f / 30f;
        private const float Gap = 1e-4f;
        private const float Pitch = 40f;
        private const float Fov = 30f;
        private const float Distance = 13.8f;

        private GameObject _start;
        private AnimalVisualCatalog _catalog;

        private WorldChunk StartChunk => _start.GetComponent<WorldChunk>();

        [OneTimeSetUp]
        public void SetUp()
        {
            _start = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GrasslandPipeline.ChunkPath));
            _catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
        }

        [OneTimeTearDown]
        public void TearDown() => Object.DestroyImmediate(_start);

        private BehaviorProfile Profile(string visualKey)
        {
            Assert.IsTrue(_catalog.TryGet(visualKey, out var entry), visualKey);
            return entry.behavior;
        }

        /// <summary>The refuge camera (PF_CameraRig_Refuge framing: 40° pitch, 30° field, 13.8 m) on the reference phone.</summary>
        private static BubbleCamera RefugeCamera(Vector3 focus, float distance = Distance)
        {
            var rotation = Quaternion.Euler(Pitch, 0f, 0f);
            return BubbleCamera.Create(focus - rotation * Vector3.forward * distance, rotation, Fov, 1080f, 2340f);
        }

        private AnimalScheduler RefugeScheduler(int capacity)
        {
            var chunk = StartChunk;
            var scheduler = new AnimalScheduler(capacity, SchedulerSettings.ForQuality(Core.QualityTier.Medium, false), chunk.WanderArea);
            scheduler.SetGround(chunk.Ground);
            scheduler.SetObstacles(chunk.GetWorldObstacles());
            scheduler.SetInterests(chunk.GetWorldInterests());
            scheduler.SetEggSpot(chunk.EggSpawn.position, RefugeController.EggSpotRadius, RefugeController.EggBodyRadius,
                RefugeController.EggApronRadius);
            return scheduler;
        }

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
                int handle = scheduler.Register(null, (uint)(i + 1), (uint)(9100 + i * 7919), position, i * 53f, Profile(keys[i % 3]), 0f, scale);
                Assert.AreNotEqual(AnimalScheduler.InvalidHandle, handle);
                handles.Add(handle);
            }
            return handles;
        }

        private static AnimalScheduler FlatScheduler(int capacity = 8) =>
            new AnimalScheduler(capacity, SchedulerSettings.ForQuality(Core.QualityTier.Medium, false), new WanderArea(Vector3.zero, 4f));

        private static BehaviorProfile SmallBody() => new BehaviorProfile { body = new BodyShape(0.15f, -0.15f, 0.1f, 0.3f) };

        /// <summary>
        /// Every placed bubble, drawn at any scale it reaches (pop-in start, rest, its largest) and at both ends of its bob,
        /// stays out of the world (owner included) and out of every other placed bubble.
        /// </summary>
        private static void AssertNothingEntered(AnimalScheduler scheduler, in BubbleCamera camera, float radius, BubbleRequest[] requests,
            BubbleSpot[] spots, int count, float time)
        {
            for (int i = 0; i < count; i++)
            {
                if (!spots[i].Placed)
                    continue;
                var center = scheduler.GetPosition(requests[i].Handle) + spots[i].Offset;
                foreach (float scale in Scales(requests[i].Done))
                {
                    foreach (float bob in new[] { -1f, 1f })
                    {
                        var drawn = center + camera.Up * (bob * WishBubblePlacement.BobAmplitude * radius);
                        WishBubblePlacement.VisualSpheres(camera, drawn, spots[i].Flip, radius * scale, out var c0, out float r0, out var c1,
                            out float r1, out var c2, out float r2);
                        foreach (var (c, r) in new[] { (c0, r0), (c1, r1), (c2, r2) })
                        {
                            float clearance = scheduler.AirClearance(c, r, AnimalScheduler.InvalidHandle);
                            if (clearance < -Gap)
                                Assert.Fail($"t={time:F2}s: bubble {i} (candidate {spots[i].Candidate}, scale {scale}) enters something by {-clearance * 100f:F2} cm at {c}.");
                        }
                    }
                }
                for (int j = i + 1; j < count; j++)
                {
                    if (!spots[j].Placed)
                        continue;
                    var other = scheduler.GetPosition(requests[j].Handle) + spots[j].Offset;
                    float gap = PairGap(camera, center, spots[i].Flip, radius * WishBubblePlacement.ReserveScale(requests[i].Done), other,
                        spots[j].Flip, radius * WishBubblePlacement.ReserveScale(requests[j].Done));
                    if (gap < -Gap)
                        Assert.Fail($"t={time:F2}s: bubbles {i} and {j} overlap by {-gap * 100f:F2} cm.");
                }
            }
        }

        private static IEnumerable<float> Scales(bool done)
        {
            yield return WishBubblePlacement.MinScale;
            yield return 1f;
            yield return WishBubblePlacement.ReserveScale(done);
        }

        private static float PairGap(in BubbleCamera camera, Vector3 a, bool flipA, float radiusA, Vector3 b, bool flipB, float radiusB)
        {
            WishBubblePlacement.VisualSpheres(camera, a, flipA, radiusA, out var a0, out float ra0, out var a1, out float ra1, out var a2, out float ra2);
            WishBubblePlacement.VisualSpheres(camera, b, flipB, radiusB, out var b0, out float rb0, out var b1, out float rb1, out var b2, out float rb2);
            float best = float.MaxValue;
            foreach (var (ca, ra) in new[] { (a0, ra0), (a1, ra1), (a2, ra2) })
                foreach (var (cb, rb) in new[] { (b0, rb0), (b1, rb1), (b2, rb2) })
                    best = Mathf.Min(best, Vector3.Distance(ca, cb) - ra - rb);
            return best;
        }

        /// <summary>Circles on screen never overlap, and none covers the face of an animal other than its own.</summary>
        private static void AssertLegible(AnimalScheduler scheduler, in BubbleCamera camera, float radius, BubbleRequest[] requests,
            BubbleSpot[] spots, int count, float time)
        {
            for (int i = 0; i < count; i++)
            {
                if (!spots[i].Placed)
                    continue;
                var center = scheduler.GetPosition(requests[i].Handle) + spots[i].Offset;
                Assert.IsTrue(camera.ToScreen(center, out var screen, out float depth));
                float pixels = radius * WishBubblePlacement.ReserveScale(requests[i].Done) * camera.PixelsPerMeter(depth);
                for (int j = i + 1; j < count; j++)
                {
                    if (!spots[j].Placed)
                        continue;
                    var other = scheduler.GetPosition(requests[j].Handle) + spots[j].Offset;
                    Assert.IsTrue(camera.ToScreen(other, out var otherScreen, out float otherDepth));
                    float otherPixels = radius * WishBubblePlacement.ReserveScale(requests[j].Done) * camera.PixelsPerMeter(otherDepth);
                    Assert.GreaterOrEqual(Vector2.Distance(screen, otherScreen), pixels + otherPixels - 0.01f,
                        $"t={time:F2}s: bubbles {i} and {j} overlap on screen.");
                }
                for (int a = 0; a < scheduler.Count; a++)
                {
                    int handle = scheduler.GetActiveHandle(a);
                    if (handle == requests[i].Handle)
                        continue;
                    scheduler.GetBody(handle, out _, out var front, out float bodyRadius, out float groundY, out float height);
                    var head = new Vector3(front.x, groundY + WishBubblePlacement.FaceHeight * height, front.y);
                    if (!camera.ToScreen(head, out var face, out float faceDepth))
                        continue;
                    float facePixels = bodyRadius * WishBubblePlacement.FaceScale * camera.PixelsPerMeter(faceDepth);
                    Assert.GreaterOrEqual(Vector2.Distance(screen, face), pixels + facePixels - 0.01f,
                        $"t={time:F2}s: bubble {i} covers the face of animal {handle}.");
                }
            }
        }

        // ------------------------------------------------------------------------------------------- the query

        [Test]
        public void AirQuery_MatchesItsClearance_SeesCrowns_TheEggRoom_AndBodies()
        {
            var scheduler = RefugeScheduler(40);
            var handles = Populate(scheduler, 20);
            var area = StartChunk.WanderArea;
            var random = new System.Random(36);
            int blocked = 0;
            for (int n = 0; n < 4000; n++)
            {
                var c = area.Center + new Vector3((float)(random.NextDouble() * 2 - 1) * area.Radius * 1.3f, (float)random.NextDouble() * 4f,
                    (float)(random.NextDouble() * 2 - 1) * area.Radius * 1.3f);
                float r = 0.05f + (float)random.NextDouble() * 0.3f;
                bool free = scheduler.IsAirFree(c, r, AnimalScheduler.InvalidHandle);
                float clearance = scheduler.AirClearance(c, r, AnimalScheduler.InvalidHandle);
                Assert.AreEqual(clearance >= 0f, free, $"IsAirFree and AirClearance agree at {c} r {r} (clearance {clearance}).");
                if (!free)
                    blocked++;
            }
            Assert.Greater(blocked, 0, "Some random spheres meet something.");

            // A tree crown (above every walker) still blocks the air.
            Obstacle crown = default;
            bool hasCrown = false;
            foreach (var o in StartChunk.GetWorldObstacles())
            {
                if (o.kind == ObstacleKind.Solid && o.bottom > 1.5f)
                {
                    crown = o;
                    hasCrown = true;
                    break;
                }
            }
            if (hasCrown)
            {
                var inside = new Vector3(crown.Center.x, crown.WorldBottom + 0.1f, crown.Center.y);
                Assert.IsFalse(scheduler.IsAirFree(inside, 0.05f, AnimalScheduler.InvalidHandle), "A tree crown is a volume in the air.");
            }

            var egg = StartChunk.EggSpawn.position;
            Assert.IsFalse(scheduler.IsAirFree(egg + new Vector3(0f, 0.3f, 0f), 0.05f, AnimalScheduler.InvalidHandle), "The egg's room.");

            int h = handles[0];
            scheduler.GetBody(h, out _, out _, out _, out float groundY, out float height);
            var root = scheduler.GetPosition(h);
            var inBody = new Vector3(root.x, groundY + height * 0.5f, root.z);
            Assert.IsFalse(scheduler.IsAirFree(inBody, 0.05f, AnimalScheduler.InvalidHandle), "An animal's body column.");
            Assert.IsTrue(scheduler.AirClearance(inBody, 0.05f, h) > scheduler.AirClearance(inBody, 0.05f, AnimalScheduler.InvalidHandle),
                "Its own column is skipped when asked.");
        }

        // ------------------------------------------------------------------------------------------- the crowd

        [Test]
        public void Crowd_NineBubbles_TwoMinutes_NeverEnterAnything_NorOverlapOnScreen()
        {
            var scheduler = RefugeScheduler(40);
            var handles = Populate(scheduler, 30);
            var camera = RefugeCamera(StartChunk.WanderArea.Center);
            float radius = WishBubblePlacement.WorldRadius(Distance, camera.TanHalfFov);
            var requests = new BubbleRequest[9];
            var spots = new BubbleSpot[9];
            var buffers = new BubblePlacementBuffers(9, 40);
            for (int i = 0; i < 9; i++)
                requests[i] = new BubbleRequest { Handle = handles[i * 3], Candidate = -1, Done = i % 3 == 0 };

            float time = 0f;
            long placedTotal = 0, passes = 0, moved = 0;
            for (int f = 0; f < 120 * 30; f++)
            {
                time += Dt;
                scheduler.Tick(time, Dt);
                if (f % 3 != 0)
                    continue;
                int placed = WishBubblePlacement.Resolve(scheduler, camera, radius, requests, 9, spots, buffers);
                placedTotal += placed;
                passes++;
                AssertNothingEntered(scheduler, camera, radius, requests, spots, 9, time);
                AssertLegible(scheduler, camera, radius, requests, spots, 9, time);
                for (int i = 0; i < 9; i++)
                {
                    if (spots[i].Placed && requests[i].Candidate >= 0 && spots[i].Candidate != requests[i].Candidate)
                        moved++;
                    requests[i].Candidate = spots[i].Placed ? spots[i].Candidate : -1;
                    requests[i].Offset = spots[i].Offset;
                    requests[i].Flip = spots[i].Flip;
                }
            }
            float mean = (float)placedTotal / passes;
            TestContext.WriteLine($"Bubbles shown per pass: {mean:F2} of 9; place changes per pass: {(float)moved / passes:F3}");
            Assert.GreaterOrEqual(mean, 5f, "Most wishes stay visible over a moving crowd.");
            Assert.Less((float)moved / passes, 1.5f, "Bubbles keep their place while it stays free (no flicker).");
        }

        // ------------------------------------------------------------------------------------------- order and waiting

        [Test]
        public void NearestToTheCamera_KeepsItsPlace_TheOtherMovesAside()
        {
            var scheduler = FlatScheduler();
            var camera = RefugeCamera(Vector3.zero);
            float radius = WishBubblePlacement.WorldRadius(Distance, camera.TanHalfFov);
            // Side by side, 5 cm apart, one a step farther from the camera: their bubbles straight above would overlap.
            int near = scheduler.Register(null, 1, 11, Vector3.zero, 0f, SmallBody(), 0f);
            int far = scheduler.Register(null, 2, 22, new Vector3(0.25f, 0f, 0.05f), 0f, SmallBody(), 0f);
            var buffers = new BubblePlacementBuffers(9, 8);
            var spots = new BubbleSpot[2];
            // Listed far first: the order follows the camera, not the list.
            var requests = new[]
            {
                new BubbleRequest { Handle = far, Candidate = -1 },
                new BubbleRequest { Handle = near, Candidate = -1 },
            };
            Assert.AreEqual(2, WishBubblePlacement.Resolve(scheduler, camera, radius, requests, 2, spots, buffers));
            Assert.AreEqual(0, spots[1].Candidate, "The animal nearer the camera keeps the place straight above it.");
            Assert.AreNotEqual(0, spots[0].Candidate, "The farther one's bubble moves aside.");
            Assert.AreEqual(1, buffers.Order[0], "Nearest first.");
            AssertNothingEntered(scheduler, camera, radius, requests, spots, 2, 0f);
            AssertLegible(scheduler, camera, radius, requests, spots, 2, 0f);

            var again = new BubbleSpot[2];
            WishBubblePlacement.Resolve(scheduler, camera, radius, requests, 2, again, buffers);
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(spots[i].Candidate, again[i].Candidate, "Deterministic.");
                Assert.AreEqual(spots[i].Offset, again[i].Offset);
                Assert.AreEqual(spots[i].Flip, again[i].Flip);
            }
        }

        [Test]
        public void NoFreePlace_TheBubbleWaits_ThenComesBack()
        {
            var scheduler = FlatScheduler();
            var camera = RefugeCamera(Vector3.zero);
            float radius = WishBubblePlacement.WorldRadius(Distance, camera.TanHalfFov);
            int handle = scheduler.Register(null, 1, 11, Vector3.zero, 0f, SmallBody(), 0f);
            // A low roof over the animal (a shelter's boards, 0.4 m to 4 m up): no candidate fits under or beside it.
            scheduler.SetObstacles(new[] { new Obstacle(Vector2.zero, Vector2.zero, 1.6f, 0f, 0.4f, 4f, ObstacleKind.Solid) });
            Assert.AreEqual(Vector3.zero, scheduler.GetPosition(handle), "The animal stands under it (lower than the roof).");
            var buffers = new BubblePlacementBuffers(9, 8);
            var requests = new[] { new BubbleRequest { Handle = handle, Candidate = 0 } };
            var spots = new BubbleSpot[1];
            Assert.AreEqual(0, WishBubblePlacement.Resolve(scheduler, camera, radius, requests, 1, spots, buffers));
            Assert.IsFalse(spots[0].Placed, "Nowhere free: the bubble fades out and waits.");

            scheduler.SetObstacles(null);
            requests[0].Candidate = -1;
            Assert.AreEqual(1, WishBubblePlacement.Resolve(scheduler, camera, radius, requests, 1, spots, buffers));
            Assert.IsTrue(spots[0].Placed);
            Assert.AreEqual(0, spots[0].Candidate, "Back straight above the animal once the place is free.");
        }

        [Test]
        public void ABetterPlace_IsTakenBack_OnlyWithRoomToSpare()
        {
            var scheduler = FlatScheduler();
            var camera = RefugeCamera(Vector3.zero);
            float radius = WishBubblePlacement.WorldRadius(Distance, camera.TanHalfFov);
            int handle = scheduler.Register(null, 1, 11, Vector3.zero, 0f, SmallBody(), 0f);
            var buffers = new BubblePlacementBuffers(9, 8);
            var spots = new BubbleSpot[1];
            var requests = new[] { new BubbleRequest { Handle = handle, Candidate = 3 } };
            WishBubblePlacement.Resolve(scheduler, camera, radius, requests, 1, spots, buffers);
            Assert.AreEqual(0, spots[0].Candidate, "Nothing around: the bubble comes back straight above.");

            // A post right beside the place above: free with the pass's look-ahead, but without the switch margin to spare.
            scheduler.GetBody(handle, out _, out _, out _, out float groundY, out float height);
            var above = WishBubblePlacement.CandidateOffset(camera, groundY + height, 0f, radius, 0, out _);
            float reserve = WishBubblePlacement.ReserveRadius(radius, false);
            float x = above.x + reserve + 0.05f + WishBubblePlacement.LookAhead + WishBubblePlacement.SwitchMargin * 0.5f;
            scheduler.SetObstacles(new[] { new Obstacle(new Vector2(x, above.z), new Vector2(x, above.z), 0.05f, 0f, 0f, 3f, ObstacleKind.Solid) });
            requests[0].Candidate = 2;
            WishBubblePlacement.Resolve(scheduler, camera, radius, requests, 1, spots, buffers);
            Assert.IsTrue(spots[0].Placed);
            buffers.ClearPlaced();
            if (WishBubblePlacement.IsFree(scheduler, camera, buffers, Vector3.zero + WishBubblePlacement.CandidateOffset(camera, groundY + height, 0f, radius, 2, out bool flip),
                    flip, radius, false, handle, false))
                Assert.AreEqual(2, spots[0].Candidate, "Still free: the bubble keeps its place rather than coming back by a hair.");
        }

        // ------------------------------------------------------------------------------------------- size and tail

        [Test]
        public void ScreenSize_StaysReadable_AtEveryZoom()
        {
            float tan = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            foreach (float distance in new[] { 8.5f, 9.5f, 13.8f, 17f })
            {
                float radius = WishBubblePlacement.WorldRadius(distance, tan);
                float fraction = 2f * radius / (2f * distance * tan);
                Assert.GreaterOrEqual(fraction, WishBubblePlacement.MinScreenDiameter - 1e-4f, $"At {distance} m the bubble is large enough to read.");
                Assert.LessOrEqual(fraction, WishBubblePlacement.MaxScreenDiameter + 1e-4f, $"At {distance} m it never grows huge.");
                TestContext.WriteLine($"{distance} m: radius {radius:F3} m, {fraction * 2340f:F0} px wide on 1080 x 2340");
            }
        }

        [Test]
        public void TheTail_PointsAtTheAnimal_AndNeverTouchesIt()
        {
            var scheduler = FlatScheduler();
            var camera = RefugeCamera(Vector3.zero);
            float radius = WishBubblePlacement.WorldRadius(Distance, camera.TanHalfFov);
            int handle = scheduler.Register(null, 1, 11, Vector3.zero, 0f, Profile("PF_Animal_Rabbit"), 0f);
            scheduler.GetBody(handle, out _, out _, out _, out float groundY, out float height);
            float top = groundY + height;
            for (int k = 0; k < WishBubblePlacement.CandidateCount; k++)
            {
                var offset = WishBubblePlacement.CandidateOffset(camera, top, 0f, radius, k, out bool flip);
                var center = scheduler.GetPosition(handle) + offset;
                foreach (float scale in Scales(true))
                {
                    WishBubblePlacement.VisualSpheres(camera, center - camera.Up * (WishBubblePlacement.BobAmplitude * radius), flip, radius * scale,
                        out var c0, out float r0, out var c1, out float r1, out var c2, out float r2);
                    Assert.Greater(Mathf.Min(c0.y - r0, Mathf.Min(c1.y - r1, c2.y - r2)), top + WishBubblePlacement.TailClearance * 0.5f,
                        $"Candidate {k}: the bubble and its dots stay above the body.");
                }
                float side = Vector3.Dot(new Vector3(offset.x, 0f, offset.z), camera.Right);
                if (Mathf.Abs(side) > 0.01f)
                    Assert.AreEqual(side < 0f, flip, $"Candidate {k}: the dots point toward the animal.");
            }
        }

        [Test]
        public void Resolve_DoesNotAllocate()
        {
            var scheduler = RefugeScheduler(40);
            var handles = Populate(scheduler, 30);
            var camera = RefugeCamera(StartChunk.WanderArea.Center);
            float radius = WishBubblePlacement.WorldRadius(Distance, camera.TanHalfFov);
            var requests = new BubbleRequest[9];
            var spots = new BubbleSpot[9];
            var buffers = new BubblePlacementBuffers(9, 40);
            for (int i = 0; i < 9; i++)
                requests[i] = new BubbleRequest { Handle = handles[i * 3], Candidate = -1 };
            float time = 0f;
            WishBubblePlacement.Resolve(scheduler, camera, radius, requests, 9, spots, buffers);
            Assert.That(() =>
            {
                for (int f = 0; f < 300; f++)
                {
                    scheduler.Tick(time += Dt, Dt);
                    WishBubblePlacement.Resolve(scheduler, camera, radius, requests, 9, spots, buffers);
                }
            }, Is.Not.AllocatingGCMemory());
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int f = 0; f < 300; f++)
                WishBubblePlacement.Resolve(scheduler, camera, radius, requests, 9, spots, buffers);
            TestContext.WriteLine($"One pass for 9 bubbles over 30 animals: {watch.Elapsed.TotalMilliseconds / 300:F3} ms (editor)");
        }

        // ------------------------------------------------------------------------------------------- built assets

        [Test]
        public void Prefab_IsBuilt_WithItsParts_AndWiredInTheVerticalSlice()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WishBubblePipeline.PrefabPath);
            Assert.IsNotNull(prefab, "Run Pocket Wilds/World/Build Wish Bubble (T36).");
            var view = prefab.GetComponent<WishBubbleView>();
            Assert.IsNotNull(view);
            var renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.AreEqual(3, renderers.Length, "Bubble, ring, icon.");
            var names = new HashSet<string>();
            foreach (var r in renderers)
            {
                Assert.IsNotNull(r.sharedMaterial);
                Assert.AreEqual(WishBubblePipeline.ShaderName, r.sharedMaterial.shader.name, r.name);
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, r.shadowCastingMode);
                if (r.sprite != null)
                    names.Add(r.sprite.name);
            }
            Assert.IsTrue(names.Contains(WishBubblePipeline.BubbleSprite) && names.Contains(WishBubblePipeline.RingSprite));
            var serialized = new SerializedObject(view);
            var icons = serialized.FindProperty("_icons");
            for (int i = 0; i < icons.arraySize; i++)
            {
                var sprite = icons.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                Assert.IsNotNull(sprite, "Icon for system " + i);
                Assert.AreEqual(WishIcons.SpriteName((WishSystem)i), sprite.name);
            }
            Assert.AreEqual(WishIcons.Done, (serialized.FindProperty("_doneIcon").objectReferenceValue as Sprite)?.name);
            Assert.AreEqual(1, view.CountTextEvent.GetPersistentEventCount(), "The count's text setter is wired.");
            Assert.AreEqual("set_text", view.CountTextEvent.GetPersistentMethodName(0));
            var count = prefab.transform.Find("Body/Count");
            Assert.IsNotNull(count);
            Assert.AreEqual(WishBubblePipeline.CountOrder, count.GetComponent<MeshRenderer>().sortingOrder, "The count draws over the icon.");

            string guid = AssetDatabase.AssetPathToGUID(WishBubblePipeline.PrefabPath);
            string scene = File.ReadAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, UiPipeline.ScenePath));
            StringAssert.Contains("_wishBubblePrefab: {fileID: ", scene);
            StringAssert.Contains(guid, scene, "VerticalSliceEntry._wishBubblePrefab points at PF_World_WishBubble.");
        }

        [Test]
        public void WishDoneEffect_SoftBubblesHeartsAndARing_ElevenSixFourteen()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VfxCatalog>(VfxPipeline.CatalogPath);
            Assert.IsNotNull(catalog, "Run Pocket Wilds/VFX/Build Feedback VFX (T34A).");
            Assert.IsTrue(catalog.TryGet(VfxId.WishDone, out var effect), "H-VFX-070 VFX_WishDone is in the catalog.");
            Assert.GreaterOrEqual(effect.warmup, 3, "Three wishes may come true together: all warmed.");
            int medium = 0, low = 0, high = 0;
            float longest = 0f;
            var tiles = new HashSet<int>();
            foreach (var ps in effect.prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                for (int b = 0; b < ps.emission.burstCount; b++)
                {
                    var burst = ps.emission.GetBurst(b);
                    int n = (int)burst.count.constant;
                    medium += n;
                    low += Mathf.Max(1, Mathf.RoundToInt(n * catalog.lowCountScale));
                    high += Mathf.Max(1, Mathf.RoundToInt(n * catalog.highCountScale));
                    longest = Mathf.Max(longest, main.startDelay.constantMax + burst.time + main.startLifetime.constantMax);
                }
                tiles.Add(Mathf.FloorToInt(ps.textureSheetAnimation.frameOverTime.constant * VfxPipeline.TileCount));
            }
            Assert.AreEqual(11, medium, "Doc 05 VFX table: 11 on Équilibrée (6 bubbles, 4 hearts, a ring).");
            Assert.AreEqual(6, low, "6 on Fluide.");
            Assert.AreEqual(14, high, "14 on Magnifique.");
            Assert.LessOrEqual(longest, 0.95f, "0.9 s.");
            CollectionAssert.AreEquivalent(new[] { VfxPipeline.TileBubble, VfxPipeline.TileHeart, VfxPipeline.TileRing }, tiles,
                "Soft bubble, heart, ring: round shapes only.");
        }
    }
}
