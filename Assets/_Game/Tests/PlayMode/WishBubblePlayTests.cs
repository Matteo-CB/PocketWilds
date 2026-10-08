using System.Collections;
using NUnit.Framework;
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
    /// T36 wish bubbles in the world (PlayMode, plan section 8.15; doc 06 screen 002; doc 05 sheet 17; rule « aucune
    /// collision »): nine bubbles over a crowd of thirty walking animals in the refuge's arrival zone, with the real
    /// PF_World_WishBubble and the refuge camera. Every frame, every drawn bubble (circle and trailing dots, at its current
    /// scale and bob) stays out of the props, the crowns, the egg's room, every animal's body and the other bubbles. A tap
    /// on a bubble finds its wish; a completed bubble bursts 250 ms later (the listener is told) and is gone; a screen or a
    /// hatch hides them all; and after warm-up the bubbles cost no garbage.
    /// </summary>
    public class WishBubblePlayTests
    {
        private const float Dt = 1f / 30f;
        private const string ChunkPath = "Assets/_Game/Prefabs/World/Chunks/Grassland/PF_Chunk_Grassland_Start.prefab";
        private const string CatalogPath = "Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset";
        private const string PrefabPath = "Assets/_Game/Prefabs/World/PF_World_WishBubble.prefab";

        private sealed class Source : IWishBubbleSource
        {
            public readonly WishBubbleState[] States = new WishBubbleState[WishBubbleLayer.MaxBubbles];
            public int Count { get; set; }
            public int Version { get; set; }

            public bool TryGet(int index, out WishBubbleState state)
            {
                state = States[index];
                return index < Count;
            }
        }

        private sealed class Bursts : IWishBubbleListener
        {
            public int Count;
            public uint LastAnimal;
            public Vector3 LastPoint;
            public float LastTime;
            public float Now;

            public void OnWishBubbleBurst(int slot, uint animal, Vector3 worldPoint)
            {
                Count++;
                LastAnimal = animal;
                LastPoint = worldPoint;
                LastTime = Now;
            }
        }

        private GameObject _chunkObject;
        private GameObject _rigObject;
        private GameObject _root;
        private WishBubbleLayer _layer;

        [TearDown]
        public void TearDown()
        {
            _layer?.Dispose();
            if (_chunkObject != null) Object.Destroy(_chunkObject);
            if (_rigObject != null) Object.Destroy(_rigObject);
            if (_root != null) Object.Destroy(_root);
        }

        [UnityTest]
        public IEnumerator NineBubbles_OverAWalkingCrowd_NeverEnterAnything_TapBurstHide_NoGarbage()
        {
#if UNITY_EDITOR
            var chunkPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ChunkPath);
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(CatalogPath);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "Run Pocket Wilds/World/Build Wish Bubble (T36).");
            _chunkObject = Object.Instantiate(chunkPrefab);
            var chunk = _chunkObject.GetComponent<WorldChunk>();
            var area = chunk.WanderArea;

            var scheduler = new AnimalScheduler(40, SchedulerSettings.ForQuality(QualityTier.Medium, false), area);
            scheduler.SetGround(chunk.Ground);
            scheduler.SetObstacles(chunk.GetWorldObstacles());
            scheduler.SetInterests(chunk.GetWorldInterests());
            scheduler.SetEggSpot(chunk.EggSpawn.position, RefugeController.EggSpotRadius, RefugeController.EggBodyRadius, RefugeController.EggApronRadius);
            string[] keys = { "PF_Animal_Rabbit", "PF_Animal_Squirrel", "PF_Animal_Hedgehog" };
            var handles = new int[30];
            for (int i = 0; i < handles.Length; i++)
            {
                Assert.IsTrue(catalog.TryGet(keys[i % 3], out var entry), keys[i % 3]);
                float angle = i * 2.39996f;
                float radius = area.Radius * 0.92f * Mathf.Sqrt((i + 0.5f) / handles.Length);
                var position = area.Center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                float scale = 0.8f * (i % 4 == 0 ? 1.2f : 1f) + 0.1f * (i % 5) / 4f + 0.2f;
                handles[i] = scheduler.Register(null, (uint)(i + 1), (uint)(5300 + i * 7919), position, i * 53f, entry.behavior, 0f, scale);
            }

            _rigObject = new GameObject("TestRig");
            var camera = _rigObject.AddComponent<Camera>();
            var rig = _rigObject.AddComponent<RefugeCameraRig>();
            rig.SetCamera(camera);
            rig.SetBounds(area.Center, new Vector2(3.5f, 4f));
            rig.SnapTo(area.Center, rig.DefaultDistance);

            var source = new Source { Count = WishBubbleLayer.MaxBubbles, Version = 1 };
            for (int i = 0; i < source.Count; i++)
            {
                source.States[i] = new WishBubbleState
                {
                    Slot = i,
                    Animal = (uint)(i * 3 + 1),
                    Progress = i % 3,
                    Target = i == 0 ? 2 : 5,
                    Done = i == 4,
                    System = i % 2 == 0 ? WishSystem.Hatching : WishSystem.CuddlesAndTreats,
                };
            }
            _root = new GameObject("Bubbles");
            _layer = new WishBubbleLayer(scheduler, prefab, _root.transform, rig, source);
            var bursts = new Bursts();
            _layer.Listener = bursts;
            _layer.Prewarm();
            Assert.IsTrue(_layer.Built);

            // 30 s of a walking crowd, real frames (the rig moves the camera in LateUpdate).
            float time = 0f, worst = float.MaxValue, shownSum = 0f;
            int frames = 0;
            for (int f = 0; f < 30 * 30; f++)
            {
                time += Dt;
                scheduler.Tick(time, Dt);
                if (f == 300)
                {
                    // Progress moves and a wish comes true while the bubbles are up.
                    source.States[1].Progress = 4;
                    source.States[2].Done = true;
                    source.Version++;
                }
                _layer.Tick(time, Dt, false);
                worst = Mathf.Min(worst, _layer.MinClearance());
                if (f > 30)
                {
                    shownSum += _layer.ShownCount;
                    frames++;
                }
                if (f % 3 == 0)
                    yield return null;
            }
            TestContext.WriteLine($"Smallest clearance of a drawn bubble: {worst * 100f:F2} cm; bubbles shown per frame: {shownSum / frames:F2}");
            Assert.GreaterOrEqual(worst, -1e-4f, "No drawn bubble ever entered a prop, a crown, the egg's room, an animal or another bubble.");
            Assert.GreaterOrEqual(shownSum / frames, 5f, "Most of the nine wishes stay in sight over the crowd.");

            // A tap through a drawn bubble finds its wish.
            int tapped = -1;
            for (int i = 0; i < WishBubbleLayer.MaxBubbles && tapped < 0; i++)
            {
                if (!_layer.TryGetBubble(i, out _, out int slot, out bool drawn, out _) || !drawn)
                    continue;
                Assert.IsTrue(_layer.TryGetScreenPoint(slot, out var screen));
                Assert.IsTrue(_layer.TryHit(camera.ScreenPointToRay(screen), out int hit), "A tap on the bubble's center hits it.");
                Assert.AreEqual(slot, hit);
                tapped = slot;
            }
            Assert.GreaterOrEqual(tapped, 0, "Some bubble was drawn to tap.");

            // Completion: the ring fills, the bubble bursts at 250 ms (the listener is told), then it is gone.
            uint animal = source.States[tapped].Animal;
            Assert.IsTrue(_layer.PlayCompleted(tapped));
            float start = time;
            while (time - start < 0.6f)
            {
                time += Dt;
                bursts.Now = time;
                scheduler.Tick(time, Dt);
                _layer.Tick(time, Dt, false);
                Assert.GreaterOrEqual(_layer.MinClearance(), -1e-4f, "The burst enters nothing either.");
                yield return null;
            }
            Assert.AreEqual(1, bursts.Count, "One burst.");
            Assert.AreEqual(animal, bursts.LastAnimal);
            Assert.AreEqual(WishBubbleView.RingFillSeconds, bursts.LastTime - start, 2.5f * Dt, "The bubble bursts once the ring is full (250 ms).");
            // The claim follows: the wish leaves the source.
            for (int i = tapped; i < source.Count - 1; i++)
            {
                source.States[i] = source.States[i + 1];
                source.States[i].Slot = i;
            }
            source.Count--;
            source.Version++;
            for (int f = 0; f < 10; f++)
            {
                time += Dt;
                scheduler.Tick(time, Dt);
                _layer.Tick(time, Dt, false);
            }
            Assert.AreEqual(source.Count, _layer.ActiveCount, "One bubble per wish left.");

            // A screen or a hatch: every bubble fades out, then nothing is drawn.
            for (int f = 0; f < 15; f++)
            {
                time += Dt;
                _layer.Tick(time, Dt, true);
                yield return null;
            }
            Assert.AreEqual(0, _layer.ShownCount, "Hidden while a screen covers the world or a hatch plays.");
            for (int f = 0; f < 30; f++)
            {
                time += Dt;
                scheduler.Tick(time, Dt);
                _layer.Tick(time, Dt, false);
                yield return null;
            }
            Assert.Greater(_layer.ShownCount, 0, "Back with the world.");

            // Steady state: 600 frames of bubbles following a walking crowd, no garbage (the crowd's own steps, then the bubbles).
            Assert.That(() =>
            {
                for (int f = 0; f < 300; f++)
                {
                    time += Dt;
                    scheduler.Tick(time, Dt);
                }
            }, Is.Not.AllocatingGCMemory(), "The crowd walks without garbage.");
            Assert.That(() =>
            {
                for (int f = 0; f < 600; f++)
                {
                    time += Dt;
                    scheduler.Tick(time, Dt);
                    _layer.Tick(time, Dt, false);
                }
            }, Is.Not.AllocatingGCMemory(), "The bubbles follow without garbage.");
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
