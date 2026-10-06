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

        /// <summary>
        /// Gate 33: the release APK (bootstrap and refuge only). BuildValidator stops it on any error; the content audit
        /// (largest assets per category, Addressables bundles) is written next to the gate documents.
        /// </summary>
        [MenuItem("Pocket Wilds/Build/Android Release APK and Audit (gate 33)", priority = 301)]
        public static void BuildAndroidReleaseMenu() => Debug.Log("[Build] " + BuildAndroidReleaseWithAudit(ReleaseAuditPath));

        public const string ReleaseAuditPath = "Docs/Gates/T34C_Build_Audit.md";

        public static string BuildAndroidReleaseWithAudit(string auditPath)
        {
            var report = BuildAndroid(development: false);
            if (report.summary.result != BuildResult.Succeeded)
                return "Android release build " + report.summary.result + " (" + report.summary.totalErrors + " errors, see the console)";
            return "Android release: " + BuildAudit.Write(report, "Audit du contenu du build release Android (porte 33)", auditPath);
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
            // The streamed world needs its bundles next to the catalog (T27).
            string content = Path.Combine(Path.GetDirectoryName(WindowsBenchmarkPath), "PocketWilds-bench_Data", "StreamingAssets", "aa");
            if (!Directory.Exists(content) || Directory.GetFiles(content, "*.bundle", SearchOption.AllDirectories).Length == 0)
                throw new Exception("Windows benchmark build has no Addressables bundles in " + content);
        }

        public const string WindowsBenchmarkPath = OutputFolder + "/Windows/PocketWilds-bench.exe";

        public static BuildReport BuildWindows()
        {
            Directory.CreateDirectory(Path.Combine(OutputFolder, "Windows"));
            PlayerSettings.enableFrameTimingStats = true;
            var options = new BuildPlayerOptions
            {
                scenes = new[]
                {
                    ProjectSetup.BootstrapScenePath, ProjectSetup.VerticalSliceScenePath, ProjectSetup.BenchmarkAnimalsScenePath, ProjectSetup.BenchmarkUIScenePath,
                    BenchmarkScenesPipeline.VfxScenePath,
                },
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                locationPathName = WindowsBenchmarkPath,
                options = BuildOptions.Development,
            };
            var activeTarget = EditorUserBuildSettings.activeBuildTarget;
            var activeGroup = BuildPipeline.GetBuildTargetGroup(activeTarget);
            // Addressables resolve [BuildTarget] from the active target: building Windows content while the editor
            // is on Android wrote Windows bundles under aa/Android and shipped a catalog without them (T27).
            if (activeTarget != BuildTarget.StandaloneWindows64)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            PrepareAddressables();
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Build] Windows benchmark: {summary.result}, {summary.totalSize / (1024f * 1024f):F1} MB, " +
                      $"{summary.totalTime.TotalSeconds:F0} s, {summary.totalErrors} errors -> {summary.outputPath}");
            // BuildPlayer leaves the editor on Windows: the project stays an Android project.
            if (EditorUserBuildSettings.activeBuildTarget != activeTarget)
                EditorUserBuildSettings.SwitchActiveBuildTarget(activeGroup, activeTarget);
            return report;
        }

        /// <summary>
        /// The player build rebuilds the Addressables content (setting stored in AddressableAssetSettings, T27).
        /// The build layout stays on: it feeds the duplicate review and keeps Addressables from opening its
        /// first-build modal prompt in an unattended build.
        /// </summary>
        private static void PrepareAddressables()
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
                throw new Exception("Addressables are not set up: run Pocket Wilds/World/Setup Grassland Streaming.");
            settings.BuildAddressablesWithPlayerBuild = UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            UnityEditor.AddressableAssets.Settings.ProjectConfigData.GenerateBuildLayout = true;
        }

        private static BuildReport BuildAndroid(bool development)
        {
            Directory.CreateDirectory(Path.Combine(OutputFolder, "Android"));
            EditorUserBuildSettings.buildAppBundle = false;

            var scenes = development
                ? new[]
                {
                    ProjectSetup.BootstrapScenePath, ProjectSetup.VerticalSliceScenePath, ProjectSetup.BenchmarkAnimalsScenePath, ProjectSetup.BenchmarkUIScenePath,
                    BenchmarkScenesPipeline.VfxScenePath,
                }
                : new[] { ProjectSetup.BootstrapScenePath, ProjectSetup.VerticalSliceScenePath };

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.Android,
                locationPathName = Path.Combine(OutputFolder, "Android", development ? "PocketWilds-dev.apk" : "PocketWilds.apk"),
                // A release starts from a clean cache: reproducible, and its report lists every packed asset (the audit).
                options = development ? BuildOptions.Development | BuildOptions.ConnectWithProfiler : BuildOptions.CleanBuildCache,
            };

            PrepareAddressables();
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Build] Android {(development ? "development" : "release")}: {summary.result}, " +
                      $"{summary.totalSize / (1024f * 1024f):F1} MB, {summary.totalTime.TotalSeconds:F0} s, " +
                      $"{summary.totalErrors} errors, {summary.totalWarnings} warnings -> {summary.outputPath}");
            return report;
        }
    }
}
