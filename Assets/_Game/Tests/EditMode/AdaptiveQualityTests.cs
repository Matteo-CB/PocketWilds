using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T34 adaptive quality (doc 07 « Beauté maximale », section 5; Performance Guide): the p95 window, the order in which
    /// the picture gets lighter, the climb back after 60 s stable, no ping-pong, the player's choice, heat protection,
    /// and the renderer part (quality level, shadow settings put back on the project assets).
    /// </summary>
    public class AdaptiveQualityTests
    {
        private const float Frame = 1f / 60f;

        private static AdaptiveQualityController Controller(QualityTier device, QualityMode mode = QualityMode.Automatic) =>
            new AdaptiveQualityController(device, mode, AdaptiveQualitySettings.Default(60));

        /// <summary>Runs seconds of frames at a work time; returns how many state changes happened.</summary>
        private static int Run(AdaptiveQualityController c, float seconds, float workMs, ThermalLevel heat = ThermalLevel.Nominal)
        {
            int changes = 0;
            int frames = Mathf.RoundToInt(seconds / Frame);
            for (int i = 0; i < frames; i++)
                if (c.Tick(Frame, workMs, heat))
                    changes++;
            return changes;
        }

        [Test]
        public void Window_P95_IgnoresTheOddSlowFrame()
        {
            var window = new FrameBudgetWindow(10f);
            bool done = false;
            for (int i = 0; i < 600 && !done; i++)
                done = window.Add(Frame, i % 50 == 0 ? 40f : 6f); // 2 % slow frames
            Assert.IsTrue(done);
            Assert.AreEqual(6f, window.LastP95, 1e-4f, "2 % of slow frames stay above the 95th percentile.");
            done = false;
            for (int i = 0; i < 600 && !done; i++)
                done = window.Add(Frame, i % 10 == 0 ? 40f : 6f); // 10 % slow frames
            Assert.AreEqual(40f, window.LastP95, 1e-4f, "10 % of slow frames do show.");
        }

        [Test]
        public void Ladder_LowersLifeFirst_ThenGround_Weather_Shadows_Resolution()
        {
            var s = QualityState.Uniform(QualityTier.High);
            var order = new System.Collections.Generic.List<QualityDimension>();
            while (s.StepDown(out var d))
                order.Add(d);
            CollectionAssert.AreEqual(new[]
            {
                QualityDimension.Life, QualityDimension.Ground, QualityDimension.Weather, QualityDimension.Shadows, QualityDimension.Resolution,
                QualityDimension.Life, QualityDimension.Ground, QualityDimension.Weather, QualityDimension.Shadows, QualityDimension.Resolution,
            }, order);
            Assert.AreEqual(QualityState.Uniform(QualityTier.Low), s);
            var up = new System.Collections.Generic.List<QualityDimension>();
            while (s.StepUp(QualityTier.High, out var d))
                up.Add(d);
            Assert.AreEqual(QualityDimension.Resolution, up[0], "The last lowered comes back first.");
            Assert.AreEqual(QualityDimension.Life, up[up.Count - 1]);
            Assert.AreEqual(QualityState.Uniform(QualityTier.High), s);
        }

        [Test]
        public void Ladder_ShadowsNeverAboveTheRenderer()
        {
            var s = QualityState.Uniform(QualityTier.High);
            do
                Assert.LessOrEqual(s.Shadows, s.Resolution, s.ToString());
            while (s.StepDown(out _));
            do
                Assert.LessOrEqual(s.Shadows, s.Resolution, s.ToString());
            while (s.StepUp(QualityTier.High, out _));
        }

        [Test]
        public void Automatic_TwoSlowWindows_LowerOneStep_OneIsNotEnough()
        {
            var c = Controller(QualityTier.High);
            Assert.AreEqual(0, Run(c, 10.2f, 20f), "One slow window alone changes nothing.");
            Assert.AreEqual(1, Run(c, 10f, 20f), "The second one in a row lowers one step.");
            Assert.AreEqual(QualityTier.Medium, c.State.Life, "Ambient life goes first.");
            Assert.AreEqual(QualityTier.High, c.State.Resolution, "Readability stays.");
            Assert.AreEqual(2, Run(c, 40f, 20f), "Still slow: one step every two windows.");
            Assert.AreEqual(QualityTier.Medium, c.State.Weather);
            Assert.AreEqual(QualityTier.High, c.State.Shadows);
        }

        [Test]
        public void Automatic_ClimbsBackAfter60sStable_NeverAboveTheDeviceTier()
        {
            var c = Controller(QualityTier.Medium);
            Run(c, 20.2f, 20f);
            Assert.AreEqual(QualityTier.Low, c.State.Life);
            Assert.AreEqual(0, Run(c, 50f, 5f), "Not yet: 60 s stable are needed.");
            Assert.AreEqual(1, Run(c, 10f, 5f));
            Assert.AreEqual(QualityState.Uniform(QualityTier.Medium), c.State);
            Assert.AreEqual(0, Run(c, 300f, 2f), "Never above the device's tier.");
        }

        [Test]
        public void Automatic_ARaiseThatFails_WaitsLongerBeforeTheNext()
        {
            var c = Controller(QualityTier.High);
            Run(c, 40.4f, 20f); // two steps down
            Assert.AreEqual(QualityTier.Medium, c.State.Ground);
            Run(c, 60f, 5f); // one step up (ground)
            Assert.AreEqual(QualityTier.High, c.State.Ground);
            Run(c, 20f, 20f); // it did not hold: down again
            Assert.AreEqual(QualityTier.Medium, c.State.Ground);
            Assert.AreEqual(0, Run(c, 70f, 5f), "After a failed raise, 60 s are not enough any more.");
            Assert.AreEqual(1, Run(c, 60f, 5f), "120 s are.");
        }

        [Test]
        public void Manual_FrameCostNeverLowersThePlayersChoice()
        {
            var c = Controller(QualityTier.Medium, QualityMode.High);
            Assert.AreEqual(QualityState.Uniform(QualityTier.High), c.State, "The player's tier, even above the device's.");
            Assert.AreEqual(0, Run(c, 120f, 30f));
            Assert.AreEqual(QualityState.Uniform(QualityTier.High), c.State);
            Assert.IsFalse(c.ThermalNotice);
        }

        [Test]
        public void Heat_Serious_LowersOneStepAtOnce_EvenInManual_AndSaysSo_ThenRestores()
        {
            var c = Controller(QualityTier.Medium, QualityMode.High);
            Assert.IsTrue(c.Tick(Frame, 5f, ThermalLevel.Serious), "Before the device throttles.");
            Assert.AreEqual(QualityTier.Medium, c.State.Life);
            Assert.IsTrue(c.ThermalNotice, "The HUD tells the player.");
            Assert.AreEqual(0, Run(c, 30f, 5f, ThermalLevel.Serious), "Once per hot spell.");
            Assert.AreEqual(0, Run(c, 30f, 5f, ThermalLevel.Nominal), "Not before 60 s cool.");
            Assert.AreEqual(1, Run(c, 31f, 5f, ThermalLevel.Nominal));
            Assert.AreEqual(QualityState.Uniform(QualityTier.High), c.State, "The player's choice comes back.");
            Assert.IsFalse(c.ThermalNotice);
        }

        [Test]
        public void Heat_Critical_GoesLowAndCaps30Fps_Fair_BlocksRaises()
        {
            var c = Controller(QualityTier.High);
            c.Tick(Frame, 5f, ThermalLevel.Critical);
            Assert.AreEqual(QualityState.Uniform(QualityTier.Low), c.State);
            Assert.IsTrue(c.ThermalFrameCap);
            Run(c, 61f, 5f, ThermalLevel.Nominal);
            Assert.IsFalse(c.ThermalFrameCap, "The cap lifts once cool.");
            Assert.AreEqual(0, Run(c, 120f, 5f, ThermalLevel.Fair), "Warm: no raise.");
            Assert.Greater(Run(c, 120f, 5f, ThermalLevel.Nominal), 0, "Cool and stable: it climbs back.");
        }

        [Test]
        public void UnknownHeat_NeverActs()
        {
            var c = Controller(QualityTier.High, QualityMode.Medium);
            Assert.AreEqual(0, Run(c, 60f, 5f, ThermalLevel.Unknown));
            Assert.IsFalse(c.ThermalNotice);
        }

        [Test]
        public void Tick_DoesNotAllocate()
        {
            var c = Controller(QualityTier.High);
            Run(c, 1f, 6f);
            Assert.That(() =>
            {
                for (int i = 0; i < 3000; i++)
                    c.Tick(Frame, i % 7 == 0 ? 20f : 6f, ThermalLevel.Nominal);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void AndroidThermalStatus_MapsToLevels()
        {
            Assert.AreEqual(ThermalLevel.Nominal, ThermalSources.FromAndroidStatus(0));
            Assert.AreEqual(ThermalLevel.Nominal, ThermalSources.FromAndroidStatus(1));
            Assert.AreEqual(ThermalLevel.Fair, ThermalSources.FromAndroidStatus(2));
            Assert.AreEqual(ThermalLevel.Serious, ThermalSources.FromAndroidStatus(3));
            Assert.AreEqual(ThermalLevel.Critical, ThermalSources.FromAndroidStatus(4));
            Assert.AreEqual(ThermalLevel.Critical, ThermalSources.FromAndroidStatus(6));
        }

        [Test]
        public void Service_ShadowStep_UsesTheLowerTiersShadows_AndPutsTheAssetBack()
        {
            int level0 = QualitySettings.GetQualityLevel();
            var high = QualitySettings.GetRenderPipelineAssetAt((int)QualityTier.High) as UniversalRenderPipelineAsset;
            var medium = QualitySettings.GetRenderPipelineAssetAt((int)QualityTier.Medium) as UniversalRenderPipelineAsset;
            float highDistance = high.shadowDistance;
            int highCascades = high.shadowCascadeCount;
            var service = new QualityService();
            try
            {
                service.Initialize(QualityTier.High, null, null, new NoThermalSource());
                Assert.AreEqual((int)QualityTier.High, QualitySettings.GetQualityLevel());
                var state = QualityState.Uniform(QualityTier.High);
                state.Shadows = QualityTier.Medium;
                service.ForceState(state);
                Assert.AreEqual(medium.shadowDistance, high.shadowDistance, 1e-4f, "Medium shadows on the High renderer.");
                Assert.AreEqual(medium.shadowCascadeCount, high.shadowCascadeCount);
                state.Resolution = QualityTier.Medium;
                service.ForceState(state);
                Assert.AreEqual((int)QualityTier.Medium, QualitySettings.GetQualityLevel());
                Assert.AreEqual(highDistance, high.shadowDistance, 1e-4f, "The High asset is back to its project values.");
                Assert.AreEqual(highCascades, high.shadowCascadeCount);
                state = QualityState.Uniform(QualityTier.High);
                state.Shadows = QualityTier.Medium;
                service.ForceState(state);
            }
            finally
            {
                service.Dispose();
                QualitySettings.SetQualityLevel(level0, true);
            }
            Assert.AreEqual(highDistance, high.shadowDistance, 1e-4f, "Disposing restores the project values.");
            Assert.AreEqual(highCascades, high.shadowCascadeCount);
            Assert.IsFalse(EditorUtility.IsDirty(high) && high.shadowDistance != highDistance);
        }

        [Test]
        public void Tiers_FollowTheDoc07Table()
        {
            float Scale(QualityTier t) => ((UniversalRenderPipelineAsset)QualitySettings.GetRenderPipelineAssetAt((int)t)).renderScale;
            Assert.AreEqual(0.8f, Scale(QualityTier.Low), 1e-4f);
            Assert.AreEqual(0.9f, Scale(QualityTier.Medium), 1e-4f);
            Assert.AreEqual(1f, Scale(QualityTier.High), 1e-4f);
        }

        [Test]
        public void ShaderWarmup_SetIsWiredIntoTheBootstrap()
        {
            var set = AssetDatabase.LoadMainAssetAtPath("Assets/_Game/Content/Graphics/DEF_ShaderWarmup.asset") as ShaderWarmupSet;
            Assert.IsNotNull(set, "DEF_ShaderWarmup (Pocket Wilds/Build/Import Graphics State Trace).");
            string scene = System.IO.File.ReadAllText("Assets/_Game/Scenes/SC_Bootstrap.unity");
            Assert.IsTrue(scene.Contains(AssetDatabase.AssetPathToGUID("Assets/_Game/Content/Graphics/DEF_ShaderWarmup.asset")),
                "GameBootstrap warms DEF_ShaderWarmup behind the loading screen.");
        }
    }
}
