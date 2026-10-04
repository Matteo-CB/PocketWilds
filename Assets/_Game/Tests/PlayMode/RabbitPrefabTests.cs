using System.Collections;
using NUnit.Framework;
using PocketWilds.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>T10: the real rabbit prefab animates from the shared family clips through the optimized hierarchy.</summary>
    public class RabbitPrefabTests
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/Animals/PF_Animal_Rabbit.prefab";

        private GameObject _camera;
        private AnimalView _view;

        [TearDown]
        public void TearDown()
        {
            if (_view != null) Object.Destroy(_view.gameObject);
            if (_camera != null) Object.Destroy(_camera);
        }

        private IEnumerator Spawn()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalView>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            // Animator culling is CullCompletely: a camera must see the rabbit for it to update.
            _camera = new GameObject("TestCamera");
            var camera = _camera.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, 0.5f, 2.2f), Quaternion.Euler(8f, 180f, 0f));
            _view = Object.Instantiate(prefab);
            var snapshot = new AnimalVisualSnapshot { InstanceId = 1, Scale = 1f, Yaw = 0f };
            _view.Configure(snapshot);
            yield return null;
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator MoveState_BouncesTheHead()
        {
            yield return Spawn();
            _view.PlayState(Animator.StringToHash("Move"), 0f);
            float min = float.MaxValue, max = float.MinValue;
            float end = Time.time + 1.0f;
            while (Time.time < end)
            {
                float y = _view.SocketHeadTop.position.y;
                min = Mathf.Min(min, y);
                max = Mathf.Max(max, y);
                yield return null;
            }
            Assert.Greater(max - min, 0.03f, "Hop must lift the rabbit (Root bounce up to 5.5 cm).");
        }

        [UnityTest]
        public IEnumerator SleepEnter_FlowsIntoSleepLoop_AndOneShotsReturnToIdle()
        {
            yield return Spawn();
            var animator = _view.Animator;
            _view.PlayState(Animator.StringToHash("SleepEnter"), 0f);
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Sleep"));

            _view.PlayState(Animator.StringToHash("Happy"), 0f);
            yield return new WaitForSeconds(1.4f);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"));
        }

        [UnityTest]
        public IEnumerator Pool_HundredCycles_OnRealPrefab_NoInstantiateNoGcNoStaleState()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalView>(PrefabPath);
            var root = new GameObject("RabbitPoolRoot");
            var pool = new AnimalViewPool(root.transform);
            int handle = pool.Register("PF_Animal_Rabbit", prefab, 8, 16);
            pool.Warmup(handle, 8);
            int created = pool.GetPool(handle).InstantiateCount;
            yield return null;

            var views = new AnimalView[8];
            var snapshot = new AnimalVisualSnapshot { InstanceId = 7, Scale = 1f, VisualData = 3u, IdlePhase = 0.25f };
            for (int i = 0; i < views.Length; i++) pool.TryAcquire(handle, snapshot, out views[i]);
            for (int i = 0; i < views.Length; i++) pool.Release(views[i]);

            Assert.That(() =>
            {
                for (int cycle = 0; cycle < 100; cycle++)
                {
                    for (int i = 0; i < views.Length; i++) pool.TryAcquire(handle, snapshot, out views[i]);
                    for (int i = 0; i < views.Length; i++) pool.Release(views[i]);
                }
            }, Is.Not.AllocatingGCMemory());

            Assert.AreEqual(created, pool.GetPool(handle).InstantiateCount);
            foreach (var view in views)
            {
                Assert.AreEqual(0u, view.OwnerInstanceId);
                Assert.AreEqual(0u, view.VisualData);
                Assert.IsFalse(view.gameObject.activeSelf);
            }
            pool.Dispose();
            Object.Destroy(root);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator Sockets_FollowTheirBones()
        {
            yield return Spawn();
            Vector3 rest = _view.SocketHeadTop.position;
            _view.PlayState(Animator.StringToHash("Eat"), 0f);
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(Vector3.Distance(rest, _view.SocketHeadTop.position), 0.03f, "HeadTop must move with the head when eating.");
        }
    }
}
