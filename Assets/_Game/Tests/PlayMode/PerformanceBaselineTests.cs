using System.Collections;
using System.IO;
using NUnit.Framework;
using PocketWilds.Benchmarks;
using UnityEngine;
using UnityEngine.TestTools;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T22 baseline in the Editor (on demand: Explicit, about two minutes). Editor numbers include editor
    /// overhead and editor allocations: they track relative regressions, never device budgets.
    /// The development player run (Pocket Wilds/Build/Windows Benchmark) gives the cleaner baseline.
    /// </summary>
    public class PerformanceBaselineTests
    {
        [UnityTest, Explicit("Performance baseline, run on demand."), Timeout(900000)]
        public IEnumerator Baseline_AllScenarios_Editor()
        {
            PerformanceReport report = null;
            yield return PerformanceBaselineRunner.RunAll(PerformanceBaselineRunner.DefaultScenarios(), "Editor PlayMode", r => report = r);
            Assert.IsNotNull(report);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestResults", "Performance", "T22_baseline_editor.json"));
            PerformanceBaselineRunner.Write(report, path);
            Debug.Log("[Benchmark] Editor report: " + path);
            Assert.AreEqual(PerformanceBaselineRunner.DefaultScenarios().Length, report.scenarios.Count, "Every scenario booted.");
            foreach (var scenario in report.scenarios)
            {
                Assert.Greater(scenario.frames, 30, scenario.id);
                int expectedVisible = Mathf.Min(scenario.rabbitsInSave, scenario.tier == "Low" ? 30 : scenario.tier == "High" ? 60 : 50);
                if (scenario.id != "P5")
                    Assert.AreEqual(expectedVisible, scenario.visibleAnimals, scenario.id + ": the save's animals are in the meadow.");
            }
        }
    }
}
