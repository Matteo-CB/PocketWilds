using System.Collections;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>T16: camera rig limits and states, blob shadows by quality tier.</summary>
    public class CameraAndQualityTests
    {
        private GameObject _rigObject;
        private AnimalView _rabbit;

        [TearDown]
        public void TearDown()
        {
            PresentationQuality.Apply(QualityTier.Medium);
            if (_rigObject != null) Object.Destroy(_rigObject);
            if (_rabbit != null) Object.Destroy(_rabbit.gameObject);
        }

        private SanctuaryCameraRig SpawnRig()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<SanctuaryCameraRig>("Assets/_Game/Prefabs/World/PF_CameraRig_Sanctuary.prefab");
            var rig = Object.Instantiate(prefab);
            _rigObject = rig.gameObject;
            rig.SetBounds(new Vector3(0f, 0f, 0.65f), new Vector2(3.5f, 4f));
            rig.SnapTo(new Vector3(0f, 0f, 0.65f));
            return rig;
#else
            Assert.Ignore("Editor only.");
            return null;
#endif
        }

        [UnityTest]
        public IEnumerator Pan_StaysInsideTheSanctuary_AndZoomRespectsLimits()
        {
            var rig = SpawnRig();
            for (int i = 0; i < 200; i++)
            {
                rig.PanPixels(new Vector2(-400f, -400f));
                rig.Zoom(0.5f);
                yield return null;
            }
            Assert.LessOrEqual(rig.Focus.x, 3.5f + 1e-3f);
            Assert.LessOrEqual(rig.Focus.z, 0.65f + 4f + 1e-3f);
            Assert.AreEqual(rig.MinDistance, rig.Distance, 0.05f);

            for (int i = 0; i < 200; i++)
            {
                rig.Zoom(2f);
                yield return null;
            }
            Assert.AreEqual(rig.MaxDistance, rig.Distance, 0.05f);
            // The camera always looks down at the fixed diorama angle.
            Assert.AreEqual(40f, rig.Camera.transform.eulerAngles.x, 0.01f);
        }

        [UnityTest]
        public IEnumerator FocusOnAnimal_ThenReturn_RestoresTheWorldView()
        {
            var rig = SpawnRig();
            rig.PanPixels(new Vector2(120f, 60f));
            for (int i = 0; i < 40; i++) yield return null;
            var worldFocus = rig.Focus;
            float worldDistance = rig.Distance;

            rig.FocusOn(new Vector3(1.2f, 0f, 1.5f), SanctuaryCameraState.FocusAnimal);
            rig.PanPixels(new Vector2(500f, 0f)); // ignored while focused
            for (int i = 0; i < 60; i++) yield return null;
            Assert.AreEqual(SanctuaryCameraState.FocusAnimal, rig.State);
            Assert.Less(Vector3.Distance(rig.Focus, new Vector3(1.2f, 0f, 1.5f)), 0.05f);

            rig.ReturnToWorld();
            for (int i = 0; i < 60; i++) yield return null;
            Assert.AreEqual(SanctuaryCameraState.DefaultWorld, rig.State);
            Assert.Less(Vector3.Distance(rig.Focus, worldFocus), 0.05f);
            Assert.AreEqual(worldDistance, rig.Distance, 0.05f);
        }

        [UnityTest]
        public IEnumerator CameraControls_DoNotAllocate()
        {
            var rig = SpawnRig();
            yield return null;
            Assert.That(() =>
            {
                for (int i = 0; i < 500; i++)
                {
                    rig.PanPixels(new Vector2(3f, -2f));
                    rig.Zoom(1.001f);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [UnityTest]
        public IEnumerator BlobShadow_ShowsOnLowOnly()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalView>("Assets/_Game/Prefabs/Animals/PF_Animal_Rabbit.prefab");
            _rabbit = Object.Instantiate(prefab);
            var blob = _rabbit.transform.Find("BlobShadow").GetComponent<Renderer>();
            var snapshot = new AnimalVisualSnapshot { InstanceId = 1, Scale = 1f };

            PresentationQuality.Apply(QualityTier.Low);
            _rabbit.Configure(snapshot);
            Assert.IsTrue(blob.enabled);

            PresentationQuality.Apply(QualityTier.Medium);
            _rabbit.Configure(snapshot);
            Assert.IsFalse(blob.enabled);
            yield return null;
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
