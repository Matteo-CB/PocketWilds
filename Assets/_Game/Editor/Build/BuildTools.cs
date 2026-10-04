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
