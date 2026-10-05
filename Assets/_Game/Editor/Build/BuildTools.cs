using System;
using System.IO;
using PocketWilds.Editor.Setup;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PocketWilds.Editor.Build
{
    /// <summary>
    /// Reproducible player builds. Development builds add the benchmark scenes and profiler connection;
    /// release builds never include them.
    /// </summary>
    public static class BuildTools
    {
        public const string OutputFolder = "Builds";

        [MenuItem("Pocket Wilds/Build/Android Development APK", priority = 300)]
        public static void BuildAndroidDevelopment()
        {
            var report = BuildAndroid(development: true);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Android development build failed: " + report.summary.result);
        }

        public static void BuildAndroidDevelopmentBatch()
        {
            try
            {
                var report = BuildAndroid(development: true);
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Windows development player for the T22 baseline (no phone connected). Run it with
        /// "-pw-benchmark &lt;report.json&gt;": it measures every scenario, writes the report and quits.
        /// Desktop numbers are a cleaner relative baseline than the Editor, never mobile budgets.
        /// </summary>
        [MenuItem("Pocket Wilds/Build/Windows Benchmark (development)", priority = 310)]
        public static void BuildWindowsBenchmark()
        {
            var report = BuildWindows();
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Windows benchmark build failed: " + report.summary.result);
        }

        public const string WindowsBenchmarkPath = OutputFolder + "/Windows/PocketWilds-bench.exe";

        public static BuildReport BuildWindows()
        {
            Directory.CreateDirectory(Path.Combine(OutputFolder, "Windows"));
            PlayerSettings.enableFrameTimingStats = true;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.BootstrapScenePath, ProjectSetup.VerticalSliceScenePath, ProjectSetup.BenchmarkAnimalsScenePath, ProjectSetup.BenchmarkUIScenePath },
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                locationPathName = WindowsBenchmarkPath,
                options = BuildOptions.Development,
            };
            var activeTarget = EditorUserBuildSettings.activeBuildTarget;
            var activeGroup = BuildPipeline.GetBuildTargetGroup(activeTarget);
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Build] Windows benchmark: {summary.result}, {summary.totalSize / (1024f * 1024f):F1} MB, " +
                      $"{summary.totalTime.TotalSeconds:F0} s, {summary.totalErrors} errors -> {summary.outputPath}");
            // BuildPlayer leaves the editor on Windows: the project stays an Android project.
            if (EditorUserBuildSettings.activeBuildTarget != activeTarget)
                EditorUserBuildSettings.SwitchActiveBuildTarget(activeGroup, activeTarget);
            return report;
        }

        private static BuildReport BuildAndroid(bool development)
        {
            Directory.CreateDirectory(Path.Combine(OutputFolder, "Android"));
            EditorUserBuildSettings.buildAppBundle = false;

            var scenes = development
                ? new[] { ProjectSetup.BootstrapScenePath, ProjectSetup.VerticalSliceScenePath, ProjectSetup.BenchmarkAnimalsScenePath, ProjectSetup.BenchmarkUIScenePath }
                : new[] { ProjectSetup.BootstrapScenePath, ProjectSetup.VerticalSliceScenePath };

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.Android,
                locationPathName = Path.Combine(OutputFolder, "Android", development ? "PocketWilds-dev.apk" : "PocketWilds.apk"),
                options = development ? BuildOptions.Development | BuildOptions.ConnectWithProfiler : BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Build] Android {(development ? "development" : "release")}: {summary.result}, " +
                      $"{summary.totalSize / (1024f * 1024f):F1} MB, {summary.totalTime.TotalSeconds:F0} s, " +
                      $"{summary.totalErrors} errors, {summary.totalWarnings} warnings -> {summary.outputPath}");
            return report;
        }
    }
}
