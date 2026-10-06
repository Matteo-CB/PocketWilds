using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Editor.Build;
using PocketWilds.Editor.Capture;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T34C R-GOLDEN-VISUAL, M-DEF-CAPTURE, R-DBG-LOD and R-DBG-OVERDRAW: every capture is a data profile, the debug
    /// materials stay in the editor, overdraw stays low, and the refuge renders as its golden references on each tier
    /// (fixed animals, wind held still).
    /// The references are this machine's renders: after a GPU or driver change, Pocket Wilds/Capture/Update Golden
    /// References makes them again.
    /// </summary>
    public class GoldenVisualTests
    {
        private const string OutputFolder = "TestResults/Golden";

        private static IEnumerable<string> GoldenProfiles() => ReferenceCapture.GoldenProfiles;

        [Test]
        public void CaptureProfiles_CoverEveryCapture()
        {
            var names = ReferenceCapture.GoldenProfiles.Concat(ReferenceCapture.ReviewProfiles).Concat(ReferenceCapture.StoreProfiles)
                .Concat(new[] { CaptureProfile.AnimalIcon, "Capture_EggIcon", "Capture_DecorIcon" });
            foreach (var name in names)
            {
                var profile = CaptureProfile.Load(name);
                Assert.GreaterOrEqual(profile.width, 16, name);
                Assert.GreaterOrEqual(profile.height, 16, name);
                Assert.AreEqual(name, profile.OutputName, name);
            }

            var tiers = ReferenceCapture.GoldenProfiles.Select(CaptureProfile.Load).ToArray();
            CollectionAssert.AreEquivalent(new[] { QualityTier.Low, QualityTier.Medium, QualityTier.High }, tiers.Select(p => p.tier).ToArray());
            Assert.IsTrue(tiers.All(p => p.shot == CaptureShot.RefugeView && p.debugView == CaptureDebugView.None && p.width == 540 && p.height == 1170),
                "Golden references are the phone frame of the refuge, without a debug view.");

            var icon = CaptureProfile.Load(CaptureProfile.AnimalIcon);
            Assert.IsTrue(icon.transparent, "Icons are cut out.");
            Assert.AreEqual(3, icon.supersample, "Icons are filtered down from three times their size.");
            Assert.AreEqual(CaptureDebugView.LodColors, CaptureProfile.Load("Capture_LOD_Refuge").debugView);
            Assert.AreEqual(CaptureDebugView.Overdraw, CaptureProfile.Load("Capture_Overdraw_Hatch").debugView);
            Assert.AreEqual(CaptureShot.HatchBurst, CaptureProfile.Load("Capture_Overdraw_Hatch").shot);
        }

        [Test]
        public void DebugMaterials_ExistAndNeverShip()
        {
            foreach (var name in new[] { "DBG_LOD_Red", "DBG_LOD_Green", "DBG_LOD_Blue" })
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>($"{ReferenceCapture.DebugFolder}/{name}.mat"), name);

            var shipped = BuildValidator.Validate(true, true, BuildValidator.BuildScenes()).Shipped;
            Assert.IsFalse(shipped.Any(p => p.StartsWith(ReferenceCapture.DebugFolder)), "Debug materials stay in the editor.");
            Assert.IsFalse(shipped.Any(p => p.StartsWith(CaptureProfile.Folder)), "Capture profiles stay in the editor.");
        }

        [Test]
        public void Overdraw_StaysLow_InTheRefugeAndTheWorstHatch()
        {
            Directory.CreateDirectory(OutputFolder);
            ReferenceCapture.Capture(CaptureProfile.Load("Capture_Overdraw_Refuge"), $"{OutputFolder}/Capture_Overdraw_Refuge.png");
            var refuge = ReferenceCapture.LastOverdraw;
            TestContext.WriteLine("Refuge: " + refuge);
            Assert.Greater(refuge.CoveredShare, 0.8f, "The counting reaches the ground, the grass and the animals: " + refuge);
            Assert.Less(refuge.MeanLayers, 2f, "No grass overdraw (T30 gate): " + refuge);
            Assert.Less(refuge.Share6, 0.01f, refuge.ToString());

            // Every feedback effect at once over the egg: a worst case the game never plays.
            ReferenceCapture.Capture(CaptureProfile.Load("Capture_Overdraw_Hatch"), $"{OutputFolder}/Capture_Overdraw_Hatch.png");
            var hatch = ReferenceCapture.LastOverdraw;
            TestContext.WriteLine("Hatch: " + hatch);
            Assert.Greater(hatch.Max, refuge.Max, "The effects add layers over the egg: " + hatch);
            Assert.Less(hatch.Share8, 0.03f, hatch.ToString());
        }

        [Test]
        public void Refuge_MatchesItsGoldenReference([ValueSource(nameof(GoldenProfiles))] string name)
        {
            var profile = CaptureProfile.Load(name);
            string golden = $"{ReferenceCapture.GoldenFolder}/{profile.OutputName}.png";
            Assert.IsTrue(File.Exists(golden), $"Missing {golden} (Pocket Wilds/Capture/Update Golden References).");

            Directory.CreateDirectory(OutputFolder);
            string current = $"{OutputFolder}/{profile.OutputName}.png";
            string capture = ReferenceCapture.Capture(profile, current);
            Assert.IsTrue(File.Exists(current), capture);

            var comparison = ReferenceCapture.Compare(golden, current);
            TestContext.WriteLine($"{name}: {comparison}");
            Assert.Less(comparison.MeanDifference, 1.0f, $"{name}: {comparison} (difference image next to {current})");
            Assert.Less(comparison.ChangedShare, 0.005f, $"{name}: {comparison} (difference image next to {current})");
        }
    }
}
