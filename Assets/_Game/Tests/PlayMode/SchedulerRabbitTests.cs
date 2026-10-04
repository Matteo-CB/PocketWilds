using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
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
    /// <summary>T12 criterion: 30 real rabbits, no individual Update, no synchronized spike, 0 GC per tick.</summary>
    public class SchedulerRabbitTests
    {
        private const int Rabbits = 30;

        private GameObject _root;
        private GameObject _camera;
        private AnimalViewPool _pool;
        private AnimalPaletteTable _palettes;
        private AnimalScheduler _scheduler;
        private AnimalView[] _views;

        [TearDown]
        public void TearDown()
        {
            _pool?.Dispose();
            _palettes?.Dispose();
            if (_root != null) Object.Destroy(_root);
            if (_camera != null) Object.Destroy(_camera);
        }

        private IEnumerator Spawn()
        {
#if UNITY_EDITOR
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            Assert.IsTrue(catalog.TryGet("PF_Animal_Rabbit", out var entry));
            var registry = database.CreateBuilder().Build();
            _palettes = new AnimalPaletteTable(registry.Variants);
            var resolver = new AnimalVisualResolver(registry, _palettes);

            // Animators cull completely when unseen: the camera frames the whole meadow.
            _camera = new GameObject("TestCamera");
            _camera.AddComponent<Camera>();
            _camera.transform.SetPositionAndRotation(new Vector3(0f, 5.5f, -6.5f), Quaternion.Euler(40f, 0f, 0f));

            _root = new GameObject("SchedulerTestRoot");
            _pool = new AnimalViewPool(_root.transform);
            int handle = _pool.Register(entry.visualKey, entry.prefab, Rabbits, Rabbits);
            _pool.Warmup(handle, Rabbits);

            _scheduler = new AnimalScheduler(64, SchedulerSettings.ForQuality(QualityTier.Medium, false), new WanderArea(Vector3.zero, 3f));
            _views = new AnimalView[Rabbits];
            var rabbit = ContentId.FromSource("species.rabbit");
            string[] variants = { "brown", "white", "grey", "black", "spotted", "sakura" };
            for (int i = 0; i < Rabbits; i++)
            {
                var animal = new AnimalInstance
                {
                    InstanceId = (uint)(i + 1),
                    Species = rabbit,
                    Variant = ContentId.FromSource("variant.rabbit." + variants[i % variants.Length]),
                    Seed = (uint)(i * 2654435761u + 17u),
                };
                var position = new Vector3((i % 6 - 2.5f) * 0.8f, 0f, (i / 6 - 2f) * 0.8f);
                Assert.IsTrue(resolver.TryResolve(animal, position, i * 41f, out var snapshot));
                Assert.IsTrue(_pool.TryAcquire(handle, snapshot, out _views[i]));
                int agent = _scheduler.Register(_views[i], animal.InstanceId, animal.Seed, position, snapshot.Yaw, entry.behavior, Time.time);
                Assert.AreEqual(i, agent);
            }
            yield return null;
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        [Test]
        public void ViewsHaveNoPerFrameCallbacks()
        {
            string[] forbidden = { "Update", "LateUpdate", "FixedUpdate", "OnGUI" };
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var name in forbidden)
                Assert.IsNull(typeof(AnimalView).GetMethod(name, flags), "AnimalView." + name);

#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Animals/PF_Animal_Rabbit.prefab");
            foreach (var behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                foreach (var name in forbidden)
                    Assert.IsNull(behaviour.GetType().GetMethod(name, flags), behaviour.GetType().Name + "." + name);
#endif
        }

        [UnityTest]
        public IEnumerator ThirtyRabbits_LiveWithoutSpikes()
        {
            yield return Spawn();
            var start = new Vector3[Rabbits];
            for (int i = 0; i < Rabbits; i++) start[i] = _views[i].transform.position;

            int maxChanges = 0, maxTicked = 0;
            bool sawWanderInAnimator = false;
            float end = Time.time + 8f;
            while (Time.time < end)
            {
                _scheduler.Tick(Time.time, Time.deltaTime);
                maxChanges = Mathf.Max(maxChanges, _scheduler.StateChangesLastTick);
                maxTicked = Mathf.Max(maxTicked, _scheduler.AgentsTickedLastTick);
                yield return null;

                for (int i = 0; i < Rabbits && !sawWanderInAnimator; i++)
                {
                    if (_scheduler.GetState(i) != AnimalBehaviorState.Wander)
                        continue;
                    var animator = _views[i].Animator;
                    var info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
                    sawWanderInAnimator = info.shortNameHash == AnimalStateHashes.Move;
                    Assert.AreEqual(0f, Vector3.Distance(_views[i].transform.position, _scheduler.GetPosition(i)), 1e-4f, "View follows its agent.");
                }
            }

            Assert.AreEqual(Rabbits, _scheduler.CountA);
            // Editor frame times vary: a long frame legitimately evaluates more agents, never all of them.
            Assert.Less(maxTicked, Rabbits / 3, "Ticks must be sliced across frames.");
            Assert.LessOrEqual(maxChanges, 4, "Synchronized decision spike.");
            Assert.Greater(_scheduler.TotalStateChanges, 15);
            Assert.IsTrue(sawWanderInAnimator, "A wandering rabbit must hop.");

            int moved = 0;
            for (int i = 0; i < Rabbits; i++)
                if (Vector3.Distance(start[i], _views[i].transform.position) > 0.2f) moved++;
            Assert.Greater(moved, 5);
        }

        [UnityTest]
        public IEnumerator Tick_WithRealViews_DoesNotAllocate()
        {
            yield return Spawn();
            float time = Time.time;
            for (int f = 0; f < 120; f++)
            {
                time += 1f / 60f;
                _scheduler.Tick(time, 1f / 60f);
            }
            yield return null;

            Assert.That(() =>
            {
                for (int f = 0; f < 600; f++)
                {
                    time += 1f / 60f;
                    _scheduler.Tick(time, 1f / 60f);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.Greater(_scheduler.TotalStateChanges, 30);
        }

        [UnityTest]
        public IEnumerator Poke_PlaysReact_OnTheRealAnimator()
        {
            yield return Spawn();
            _scheduler.Poke(4, Time.time);
            yield return null;
            yield return null;
            var animator = _views[4].Animator;
            var info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            Assert.AreEqual(AnimalStateHashes.React, info.shortNameHash);
        }
    }
}
