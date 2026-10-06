using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PocketWilds.Content;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Build
{
    /// <summary>
    /// ED-010 BuildValidator (doc 11 BUILD ASSET STRIPPING, gate 33). Walks what a build really ships (the build
    /// scenes, every Addressables entry and every project Resources folder, through their asset references, never
    /// through scripts) and checks:
    /// - scenes: a release ships only the bootstrap and the refuge (no benchmark, capture or sandbox scene);
    /// - forbidden content: TEMP, sandbox, debug (DBG_), benchmark, test, editor or SourceArt assets;
    /// - shaders: approved families only, and no compile error;
    /// - textures: never readable, size within the cap of their family, compressed on the target unless an atlas
    ///   included in the build packs them;
    /// - content: ContentRegistry builds without error (stable ids, references);
    /// - Addressables: nothing duplicated between bundles or between bundles and scenes;
    /// - development-only assemblies (sandbox store, benchmarks) compiled out of release builds;
    /// - prefabs and scenes without missing scripts.
    /// The checks run while the build prepares (BuildPlayerProcessor, with the scenes really built): the Addressables
    /// duplicate rules build bundles, which Unity refuses once the player build is in progress. A release build then
    /// stops on any error (IPreprocessBuildWithReport); a development build only reports and skips the duplicate rules
    /// (the tests and the menu run them).
    /// </summary>
    public sealed class BuildValidator : IPreprocessBuildWithReport
    {
        /// <summary>Runs the checks before the player build starts, on the scenes of this build.</summary>
        public sealed class Preparation : BuildPlayerProcessor
        {
            public override int callbackOrder => 0;

            public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
            {
                var options = buildPlayerContext.BuildPlayerOptions;
                bool release = (options.options & BuildOptions.Development) == 0;
                bool mobile = options.target == BuildTarget.Android || options.target == BuildTarget.iOS;
                var scenes = options.scenes != null && options.scenes.Length > 0 ? options.scenes : BuildScenes();
                s_Prepared = Validate(release, mobile, scenes, analyzeAddressables: release);
            }
        }

        private static Result s_Prepared;

        /// <summary>
        /// Runs after the performance testing package wrote its run files (its callback order is 0) and before Unity
        /// collects the Resources folders: a release build removes them, a development build keeps them (the package
        /// reads them in development players).
        /// </summary>
        public sealed class ReleaseResourcesGuard : IPreprocessBuildWithReport
        {
            public int callbackOrder => 100;

            public void OnPreprocessBuild(BuildReport report)
            {
                if ((report.summary.options & BuildOptions.Development) != 0)
                    return;
                foreach (var path in PerformanceTestRunFiles)
                {
                    if (!File.Exists(path))
                        continue;
                    if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)))
                        AssetDatabase.DeleteAsset(path);
                    else
                    {
                        File.Delete(path);
                        if (File.Exists(path + ".meta"))
                            File.Delete(path + ".meta");
                    }
                    Debug.Log("[BuildValidator] release: removed " + path + " (performance test run details never ship)");
                }
            }
        }

        public enum Severity
        {
            Warning,
            Error,
        }

        public readonly struct Issue
        {
            public Issue(Severity severity, string rule, string path, string message)
            {
                Severity = severity;
                Rule = rule;
                Path = path;
                Message = message;
            }

            public Severity Severity { get; }
            public string Rule { get; }
            public string Path { get; }
            public string Message { get; }
            public override string ToString() => $"{Severity} [{Rule}] {Path}: {Message}";
        }

        public sealed class Result
        {
            public readonly List<Issue> Issues = new List<Issue>();
            public readonly List<string> Shipped = new List<string>();
            public bool HasErrors => Issues.Any(i => i.Severity == Severity.Error);
            public int Errors => Issues.Count(i => i.Severity == Severity.Error);
            public int Warnings => Issues.Count(i => i.Severity == Severity.Warning);

            public void Add(Severity severity, string rule, string path, string message) => Issues.Add(new Issue(severity, rule, path, message));
        }

        /// <summary>The only scenes a release ships.</summary>
        public static readonly string[] ReleaseScenes =
        {
            "Assets/_Game/Scenes/SC_Bootstrap.unity",
            "Assets/_Game/Scenes/SC_VerticalSlice_Grassland.unity",
        };

        /// <summary>Path parts that never ship in a release (doc 11: sandbox, capture, benchmark, debug, editor, tests).</summary>
        public static readonly string[] ForbiddenParts =
        {
            "/Sandbox/", "/TEMP/", "_TEMP", "/Temp/", "/Debug/", "/DBG_", "/Benchmarks/", "/ArtCapture", "/Editor/", "/Tests/", "SourceArt/",
            "/PerformanceTestRun",
        };

        /// <summary>
        /// Written into Assets/Resources before every player build by the performance testing package (a dependency of
        /// com.unity.collections, no opt-out): the build machine and test run details. The package deletes them after
        /// the build, so without <see cref="ReleaseResourcesGuard"/> they shipped in every release.
        /// </summary>
        public static readonly string[] PerformanceTestRunFiles =
        {
            "Assets/Resources/PerformanceTestRunInfo.json", "Assets/Resources/PerformanceTestRunSettings.json",
        };

        /// <summary>Approved shader families (doc 07): the game's own, URP, TextMeshPro, uGUI.</summary>
        public static readonly string[] ApprovedShaderRoots =
        {
            "Assets/_Game/Art/Shaders/", "Packages/com.unity.render-pipelines.universal/", "Packages/com.unity.render-pipelines.core/",
            "Assets/TextMesh Pro/Shaders/", "Packages/com.unity.ugui/",
        };

        /// <summary>Largest side allowed per texture family (doc 07 texture budgets; atlases are checked as a whole).</summary>
        public static readonly (string Root, int Max)[] TextureCaps =
        {
            ("Assets/_Game/Art/UI/Thumbnails/", 256),
            ("Assets/_Game/Art/UI/", 1024),
            ("Assets/_Game/Art/VFX/", 256),
            ("Assets/_Game/Art/Eggs/", 512),
            ("Assets/_Game/Art/Animals/", 512),
            ("Assets/_Game/Art/", 1024),
        };

        /// <summary>Assemblies that must be compiled out of release builds.</summary>
        public static readonly string[] DevelopmentOnlyAssemblies =
        {
            "Assets/_Game/Services/StoreSandbox/Game.Services.StoreSandbox.asmdef",
            "Assets/_Game/Benchmarks/Game.Benchmarks.asmdef",
            "Assets/_Game/Benchmarks/Runner/Game.Benchmarks.Runner.asmdef",
        };

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            bool release = (report.summary.options & BuildOptions.Development) == 0;
            // Prepared with this build's scenes; a build that skipped the preparation is checked on the build settings scenes.
            var result = s_Prepared ?? Validate(release, report.summary.platform == BuildTarget.Android || report.summary.platform == BuildTarget.iOS,
                BuildScenes(), analyzeAddressables: false);
            s_Prepared = null;
            foreach (var issue in result.Issues)
            {
                if (issue.Severity == Severity.Error && release)
                    Debug.LogError("[BuildValidator] " + issue);
                else
                    Debug.LogWarning("[BuildValidator] " + issue);
            }
            Debug.Log($"[BuildValidator] {(release ? "release" : "development")}: {result.Errors} errors, {result.Warnings} warnings, {result.Shipped.Count} assets checked.");
            if (release && result.HasErrors)
                throw new BuildFailedException($"BuildValidator: {result.Errors} errors (see the console). A release never ships them.");
        }

        [MenuItem("Pocket Wilds/Build/Validate Release Content (ED-010)", priority = 305)]
        public static void ValidateMenu()
        {
            var result = Validate(true, true, BuildScenes());
            Debug.Log("[BuildValidator] " + Summary(result));
        }

        public static string[] BuildScenes() => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        public static string Summary(Result result)
        {
            var text = new StringBuilder($"{result.Errors} errors, {result.Warnings} warnings, {result.Shipped.Count} assets checked");
            foreach (var issue in result.Issues.Take(40))
                text.Append("\n  ").Append(issue);
            return text.ToString();
        }

        /// <param name="release">Release rules (scenes, forbidden content) instead of development ones.</param>
        /// <param name="mobile">Texture compression is checked for the mobile target.</param>
        /// <param name="analyzeAddressables">Runs the Addressables duplicate rules (never while a player build is in progress).</param>
        public static Result Validate(bool release, bool mobile, IReadOnlyList<string> scenes, bool analyzeAddressables = true)
        {
            var result = new Result();
            var errorWhenRelease = release ? Severity.Error : Severity.Warning;

            // Scenes.
            foreach (var scene in scenes)
                if (!ReleaseScenes.Contains(scene))
                    result.Add(errorWhenRelease, "scenes", scene, "only SC_Bootstrap and SC_VerticalSlice_Grassland ship in a release");
            foreach (var required in ReleaseScenes)
                if (!scenes.Contains(required))
                    result.Add(Severity.Error, "scenes", required, "required scene missing from the build");

            // What ships: scenes, Addressables entries and every project Resources folder, through asset references
            // (scripts and their icons excluded).
            var roots = new List<string>(scenes);
            roots.AddRange(ResourcesContent());
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                foreach (var group in settings.groups)
                {
                    var bundled = group != null ? group.GetSchema<BundledAssetGroupSchema>() : null;
                    if (bundled == null || !bundled.IncludeInBuild)
                        continue;
                    roots.AddRange(group.entries.Select(e => e.AssetPath));
                }
            }
            result.Shipped.AddRange(Shipped(roots));

            var atlased = AtlasedTextures();
            foreach (var path in result.Shipped)
            {
                // Project content only (package internals follow their own rules).
                if (path.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    string forbidden = ForbiddenParts.FirstOrDefault(part => path.Contains(part, StringComparison.Ordinal));
                    if (forbidden == null && Path.GetFileName(path).StartsWith("DBG_", StringComparison.Ordinal))
                        forbidden = "DBG_";
                    if (forbidden != null)
                        result.Add(errorWhenRelease, "forbidden", path, $"\"{forbidden.Trim('/')}\" content never ships");
                }

                if (path.EndsWith(".shader", StringComparison.Ordinal))
                {
                    if (!ApprovedShaderRoots.Any(r => path.StartsWith(r, StringComparison.Ordinal)))
                        result.Add(Severity.Error, "shaders", path, "not an approved shader family");
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    if (shader != null && ShaderUtil.ShaderHasError(shader))
                        result.Add(Severity.Error, "shaders", path, "does not compile");
                }

                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                    CheckTexture(result, path, importer, atlased, mobile);

                if (path.EndsWith(".prefab", StringComparison.Ordinal))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        int missing = prefab.GetComponentsInChildren<Transform>(true)
                            .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                        if (missing > 0)
                            result.Add(Severity.Error, "missing scripts", path, missing + " missing scripts");
                    }
                }
            }

            // Content ids and references.
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            if (database == null)
                result.Add(Severity.Error, "content", "DEF_ContentDatabase", "missing");
            else
            {
                var builder = database.CreateBuilder();
                foreach (var issue in builder.Issues)
                    result.Add(builder.HasErrors ? Severity.Error : Severity.Warning, "content", "DEF_ContentDatabase", issue.ToString());
            }

            // Addressables duplicates.
            if (settings != null && analyzeAddressables)
            {
                foreach (var rule in new AnalyzeRule[] { new CheckBundleDupeDependencies(), new CheckSceneDupeDependencies() })
                {
                    var issues = rule.RefreshAnalysis(settings).Where(r => r.severity != MessageType.None && r.resultName != "No issues found").ToList();
                    foreach (var issue in issues)
                        result.Add(Severity.Error, "addressables", issue.resultName, rule.ruleName);
                    rule.ClearAnalysis();
                }
            }

            // Development-only assemblies.
            foreach (var path in DevelopmentOnlyAssemblies)
            {
                if (!File.Exists(path))
                    continue;
                var json = File.ReadAllText(path);
                if (!json.Contains("DEVELOPMENT_BUILD", StringComparison.Ordinal))
                    result.Add(Severity.Error, "assemblies", path, "must be compiled out of release builds (defineConstraints UNITY_EDITOR || DEVELOPMENT_BUILD)");
            }
            return result;
        }

        private static void CheckTexture(Result result, string path, TextureImporter importer, HashSet<string> atlased, bool mobile)
        {
            if (importer.isReadable)
                result.Add(Severity.Error, "textures", path, "Read/Write is on (doubles its memory)");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
                return;
            // The caps are phone budgets: measured on the Android import whatever the active target (a desktop benchmark
            // build imports at the desktop size).
            importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
            var android = importer.GetPlatformTextureSettings("Android");
            int side = Mathf.Min(Mathf.Max(sourceWidth, sourceHeight), android.overridden ? android.maxTextureSize : importer.maxTextureSize);
            var cap = TextureCaps.FirstOrDefault(c => path.StartsWith(c.Root, StringComparison.Ordinal));
            if (cap.Root != null && side > cap.Max)
                result.Add(Severity.Error, "textures", path, $"{side} px on Android, above the {cap.Max} px cap of its family");
            if (!mobile || atlased.Contains(path))
                return;
            if (IsUncompressed(texture.format) && side > 64)
                result.Add(Severity.Warning, "textures", path, $"uncompressed {texture.format} on the target ({texture.width}x{texture.height})");
        }

        private static bool IsUncompressed(TextureFormat format) => format switch
        {
            TextureFormat.RGBA32 or TextureFormat.ARGB32 or TextureFormat.RGB24 or TextureFormat.BGRA32 or TextureFormat.RGBAHalf
                or TextureFormat.RGBAFloat or TextureFormat.RGBA4444 or TextureFormat.ARGB4444 or TextureFormat.RGB565 => true,
            _ => false,
        };

        /// <summary>Every asset under a Resources folder of the project: Unity ships them all, referenced or not.</summary>
        public static IEnumerable<string> ResourcesContent()
        {
            var folders = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && Path.GetFileName(p) == "Resources" && AssetDatabase.IsValidFolder(p))
                .ToArray();
            if (folders.Length == 0)
                return Enumerable.Empty<string>();
            return AssetDatabase.FindAssets(string.Empty, folders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !AssetDatabase.IsValidFolder(p))
                .Distinct();
        }

        /// <summary>Every asset reachable from the roots through asset references (scripts are not followed).</summary>
        public static List<string> Shipped(IEnumerable<string> roots)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>(roots.Where(r => !string.IsNullOrEmpty(r)));
            while (stack.Count > 0)
            {
                string path = stack.Pop();
                if (!visited.Add(path))
                    continue;
                foreach (var dependency in AssetDatabase.GetDependencies(path, false))
                {
                    if (dependency == path || IsScript(dependency))
                        continue;
                    stack.Push(dependency);
                }
            }
            return visited.Where(p => !IsScript(p)).OrderBy(p => p, StringComparer.Ordinal).ToList();
        }

        private static bool IsScript(string path) =>
            path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".dll", StringComparison.Ordinal) || path.EndsWith(".asmdef", StringComparison.Ordinal);

        /// <summary>Sprite textures packed by an atlas included in the build (the atlas owns their compression).</summary>
        public static HashSet<string> AtlasedTextures()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:SpriteAtlas"))
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AssetDatabase.GUIDToAssetPath(guid));
                if (atlas == null || !atlas.IsIncludeInBuild())
                    continue;
                foreach (var packable in atlas.GetPackables())
                {
                    string path = AssetDatabase.GetAssetPath(packable);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        foreach (var texture in AssetDatabase.FindAssets("t:Texture2D", new[] { path }))
                            set.Add(AssetDatabase.GUIDToAssetPath(texture));
                    }
                    else
                        set.Add(path);
                }
            }
            return set;
        }
    }
}
