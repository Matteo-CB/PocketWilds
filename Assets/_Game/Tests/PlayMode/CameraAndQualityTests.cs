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

        private RefugeCameraRig SpawnRig()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<RefugeCameraRig>("Assets/_Game/Prefabs/World/PF_CameraRig_Refuge.prefab");
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
        public IEnumerator Pan_StaysInsideTheRefuge_AndZoomRespectsLimits()
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

            rig.FocusOn(new Vector3(1.2f, 0f, 1.5f), RefugeCameraState.FocusAnimal);
            rig.PanPixels(new Vector2(500f, 0f)); // ignored while focused
            for (int i = 0; i < 60; i++) yield return null;
            Assert.AreEqual(RefugeCameraState.FocusAnimal, rig.State);
            Assert.Less(Vector3.Distance(rig.Focus, new Vector3(1.2f, 0f, 1.5f)), 0.05f);

            rig.ReturnToWorld();
            for (int i = 0; i < 60; i++) yield return null;
            Assert.AreEqual(RefugeCameraState.DefaultWorld, rig.State);
            Assert.Less(Vector3.Distance(rig.Focus, worldFocus), 0.05f);
            Assert.AreEqual(worldDistance, rig.Distance, 0.05f);
        }

        [UnityTest]
        public IEnumerator Fling_GlidesOnAfterRelease_SlowsDown_AndStopsAtTheEdge()
        {
            var rig = SpawnRig();
            // A long strip, like the eight Prairie zones side by side.
            rig.SetBounds(new Vector3(0f, 0f, 0.65f), new Vector2(40f, 4f));
            rig.SnapTo(new Vector3(0f, 0f, 0.65f));
            yield return null;

            // A quick swipe to the left: the view glides to the right on its own.
            rig.Fling(new Vector2(-3000f, 0f));
            Assert.Greater(rig.FlingVelocity.x, 0f);
            float start = rig.Focus.x;
            float t = 0f;
            while (rig.FlingVelocity != Vector3.zero && t < 8f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(Vector3.zero, rig.FlingVelocity, "The glide slows down and stops by itself.");
            Assert.Greater(rig.Focus.x - start, 2f, "One swipe carries the view several meters.");

            // Thrown hard against the end of the strip: it stops at the edge.
            rig.Fling(new Vector2(-1e6f, 0f));
            for (int i = 0; i < 600 && rig.FlingVelocity != Vector3.zero; i++) yield return null;
            Assert.LessOrEqual(rig.Focus.x, 40f + 1e-3f);

            // A new touch catches a gliding view; a focus state ends it too.
            rig.Fling(new Vector2(3000f, 0f));
            rig.StopFling();
            Assert.AreEqual(Vector3.zero, rig.FlingVelocity);
            rig.Fling(new Vector2(3000f, 0f));
            rig.FocusOn(new Vector3(1f, 0f, 1f), RefugeCameraState.HatchReveal);
            Assert.AreEqual(Vector3.zero, rig.FlingVelocity);
            rig.Fling(new Vector2(3000f, 0f));
            Assert.AreEqual(Vector3.zero, rig.FlingVelocity, "No fling while the camera frames a hatch.");
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
                    rig.Fling(new Vector2(300f, -200f));
                    rig.StopFling();
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
