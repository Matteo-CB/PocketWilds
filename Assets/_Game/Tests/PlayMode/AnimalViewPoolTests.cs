using System.Collections;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>T05 criteria: 1000 cycles after warmup, 0 Instantiate, 0 Destroy, 0 GC, no stale state.</summary>
    public class AnimalViewPoolTests
    {
        private GameObject _root;
        private GameObject _template;
        private AnimalViewPool _pool;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestPoolRoot");
            // TEMP placeholder: a cube view, created in memory for tests only.
            _template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _template.name = "TEMP_PlaceholderAnimal";
            Object.DestroyImmediate(_template.GetComponent<Collider>());
            _template.AddComponent<AnimalView>();
            _template.SetActive(false);
            _pool = new AnimalViewPool(_root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();
            Object.Destroy(_root);
            Object.Destroy(_template);
        }

        private static AnimalVisualSnapshot Snapshot(uint id, uint visualData) => new AnimalVisualSnapshot
        {
            InstanceId = id,
            Species = ContentId.FromSource("species.rabbit"),
            Position = new Vector3(id, 0f, 0f),
            Scale = 1f,
            VisualData = visualData,
        };

        [UnityTest]
        public IEnumerator ThousandCycles_AfterWarmup_NoInstantiateNoDestroyNoGC()
        {
            int handle = _pool.Register("TEMP_PlaceholderAnimal", _template.GetComponent<AnimalView>(), 32, 64);
            _pool.Warmup(handle, 32);
            var gameObjectPool = _pool.GetPool(handle);
            int instantiated = gameObjectPool.InstantiateCount;
            Assert.AreEqual(32, instantiated);
            yield return null;

            var views = new AnimalView[20];
            var snapshot = Snapshot(1, 7u);

            // Warm the code path once.
            for (int i = 0; i < views.Length; i++)
                _pool.TryAcquire(handle, snapshot, out views[i]);
            for (int i = 0; i < views.Length; i++)
                _pool.Release(views[i]);

            Assert.That(() =>
            {
                for (int cycle = 0; cycle < 1000; cycle++)
                {
                    for (int i = 0; i < views.Length; i++)
                        _pool.TryAcquire(handle, snapshot, out views[i]);
                    for (int i = 0; i < views.Length; i++)
                        _pool.Release(views[i]);
                }
            }, Is.Not.AllocatingGCMemory());

            Assert.AreEqual(instantiated, gameObjectPool.InstantiateCount, "No Instantiate in steady state.");
            Assert.AreEqual(0, gameObjectPool.DestroyCount, "No Destroy in steady state.");
        }

        [UnityTest]
        public IEnumerator ReleasedView_HasNoStaleOwnerOrPalette()
        {
            int handle = _pool.Register("TEMP_PlaceholderAnimal", _template.GetComponent<AnimalView>(), 1, 1);
            _pool.Warmup(handle, 1);
            yield return null;

            uint red = AnimalVisualPacking.Pack(3, PatternChannel.Uv1X, 12, 0);
            uint blue = AnimalVisualPacking.Pack(9, PatternChannel.None, 0, 2);
            Assert.IsTrue(_pool.TryAcquire(handle, Snapshot(5, red), out var view));
            var renderer = view.GetComponent<MeshRenderer>();
            Assert.AreEqual(5u, view.OwnerInstanceId);
            Assert.AreEqual(red, renderer.GetShaderUserValue());
            Assert.IsFalse(renderer.HasPropertyBlock(), "Variants never use MaterialPropertyBlocks.");

            _pool.Release(view);
            Assert.AreEqual(0u, view.OwnerInstanceId);
            Assert.AreEqual(0u, renderer.GetShaderUserValue(), "Palette must not leak to the next owner.");
            Assert.IsFalse(view.gameObject.activeSelf);

            Assert.IsTrue(_pool.TryAcquire(handle, Snapshot(6, blue), out var again));
            Assert.AreSame(view, again);
            Assert.AreEqual(6u, again.OwnerInstanceId);
            Assert.AreEqual(blue, renderer.GetShaderUserValue());
            Assert.AreEqual(9, AnimalVisualPacking.PaletteRow(renderer.GetShaderUserValue()));
            Assert.AreEqual(2, AnimalVisualPacking.RareIntensity(renderer.GetShaderUserValue()));
            Assert.AreEqual(new Vector3(6f, 0f, 0f), again.transform.position);
            Assert.AreEqual(1, renderer.sharedMaterials.Length);
            _pool.Release(again);
        }

        [UnityTest]
        public IEnumerator HardCap_ReturnsFalse_AndSurplusIsTrimmedLater()
        {
            int handle = _pool.Register("TEMP_PlaceholderAnimal", _template.GetComponent<AnimalView>(), 4, 8);
            var views = new AnimalView[8];
            for (int i = 0; i < 8; i++)
                Assert.IsTrue(_pool.TryAcquire(handle, Snapshot((uint)i + 1, 7u), out views[i]));
            Assert.IsFalse(_pool.TryAcquire(handle, Snapshot(99, 7u), out _));

            for (int i = 0; i < 8; i++)
                _pool.Release(views[i]);

            var gameObjectPool = _pool.GetPool(handle);
            Assert.AreEqual(4, gameObjectPool.Stats.Inactive);
            Assert.AreEqual(4, gameObjectPool.Stats.PendingDestroy);
            Assert.AreEqual(4, _pool.TrimPending(8));
            yield return null;
            Assert.AreEqual(4, gameObjectPool.DestroyCount);
            Assert.AreEqual(4, _root.transform.childCount);
        }
    }
}
