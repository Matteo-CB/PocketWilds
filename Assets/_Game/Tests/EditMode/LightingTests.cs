using NUnit.Framework;
using PocketWilds.Editor.Lighting;
using PocketWilds.Editor.Validation;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T16 gate: lighting reference data, quality tiers and camera rig follow doc 07 and the Performance Bible.</summary>
    public class LightingTests
    {
        private static UniversalRenderPipelineAsset Tier(string name) =>
            AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>($"Assets/_Game/Content/Quality/Rendering/URP_{name}.asset");

        [Test]
        public void GrasslandProfile_HasSkyFogAndOneSun()
        {
            var profile = AssetDatabase.LoadAssetAtPath<LightingProfile>(LightingPipeline.ProfilePath);
            Assert.IsNotNull(profile);
            Assert.IsNotNull(profile.sky);
            Assert.AreEqual("PocketWilds/SkyGradient", profile.sky.shader.name);
            Assert.IsTrue(profile.fog);
            Assert.Less(profile.fogStart, profile.fogEnd);
            Assert.That(profile.sunIntensity, NUnit.Framework.Is.InRange(0.6f, 1.6f));
        }

        [Test]
        public void QualityTiers_LowDropsTheShadowMap_MediumCoversThePlayArea()
        {
            Assert.IsFalse(Tier("Low").supportsMainLightShadows, "Low grounds animals with blob shadows instead.");
            Assert.IsTrue(Tier("Medium").supportsMainLightShadows);
            // The gameplay camera sits 13.8 m from its focus: shadows must reach past it.
            Assert.GreaterOrEqual(Tier("Medium").shadowDistance, 22f);
            Assert.GreaterOrEqual(Tier("High").shadowDistance, Tier("Medium").shadowDistance);
            Assert.LessOrEqual(Tier("Low").renderScale, Tier("Medium").renderScale);
            foreach (var name in new[] { "Low", "Medium", "High" })
                Assert.AreEqual(LightRenderingMode.Disabled, Tier(name).additionalLightsRenderingMode, name + ": one sun only.");
        }

        [Test]
        public void PostProcessing_StaysMinimal()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LightingPipeline.VolumeProfilePath);
            Assert.IsNotNull(profile);
            Assert.IsFalse(profile.Has<Bloom>() && profile.TryGet<Bloom>(out var bloom) && bloom.active && bloom.intensity.value > 0.3f);
            Assert.IsFalse(profile.Has<DepthOfField>(), "No gameplay depth of field.");
            Assert.IsFalse(profile.Has<MotionBlur>(), "No motion blur.");
            Assert.IsFalse(profile.Has<ScreenSpaceLensFlare>());
            Assert.LessOrEqual(profile.components.Count, 3);
        }

        [Test]
        public void CameraRig_IsPortraitDiorama_WithStableFov()
        {
            var rig = AssetDatabase.LoadAssetAtPath<SanctuaryCameraRig>(LightingPipeline.CameraRigPath);
            Assert.IsNotNull(rig);
            Assert.IsNotNull(rig.GetComponent<SanctuaryCameraInput>());
            var camera = rig.GetComponentInChildren<Camera>(true);
            Assert.IsNotNull(camera);
            Assert.AreEqual("MainCamera", camera.tag);
            Assert.AreEqual(30f, camera.fieldOfView, 0.01f);
            Assert.Less(rig.MinDistance, rig.MaxDistance);
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            Assert.AreEqual(AntialiasingMode.None, data.antialiasing, "MSAA comes from the tier; no extra post AA.");
        }

        [Test]
        public void AnimalAndEggPrefabs_StillPassTheValidator_WithBlobShadows()
        {
            var issues = AssetValidator.ValidateAllAnimals();
            issues.AddRange(AssetValidator.ValidateEggPrefab());
            issues.RemoveAll(i => i.Severity != IssueSeverity.Error);
            Assert.IsEmpty(issues, string.Join("\n", issues));
            var rabbit = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Animals/PF_Animal_Rabbit.prefab");
            Assert.IsNotNull(rabbit.transform.Find("BlobShadow"));
        }
    }
}
