using System;
using System.IO;
using System.Reflection;
using System.Text;
using PocketWilds.Benchmarks;
using PocketWilds.Bootstrap;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PocketWilds.Editor.Setup
{
    /// <summary>
    /// T00 project configuration as code. Idempotent: safe to run again at any time.
    /// Nothing essential depends on a hand-ticked Editor checkbox.
    /// Batch: Unity -batchmode -projectPath . -executeMethod PocketWilds.Editor.Setup.ProjectSetup.RunBatch
    /// </summary>
    public static class ProjectSetup
    {
        public const string RenderingFolder = "Assets/_Game/Content/Quality/Rendering";
        public const string ScenesFolder = "Assets/_Game/Scenes";
        public const string BenchmarkScenesFolder = "Assets/_Game/Scenes/Benchmarks";
        public const string LightingFolder = "Assets/_Game/Art/Lighting";

        public const string BootstrapScenePath = ScenesFolder + "/SC_Bootstrap.unity";
        public const string VerticalSliceScenePath = ScenesFolder + "/SC_VerticalSlice_Grassland.unity";
        public const string BenchmarkAnimalsScenePath = BenchmarkScenesFolder + "/SC_Benchmark_Animals.unity";
        public const string BenchmarkUIScenePath = BenchmarkScenesFolder + "/SC_Benchmark_UI.unity";
        public const string LightingSettingsPath = LightingFolder + "/LS_Sanctuary_NoGI.lighting";

        public const string CompanyName = "PocketWilds";
        public const string ProductName = "Pocket Wilds";
        public const string ApplicationId = "com.pocketwilds.game";

        private struct TierSpec
        {
            public string Name;
            public float RenderScale;
            public int Msaa;
            public int ShadowResolution;
            public bool MainLightShadows;
            public float ShadowDistance;
            public int Cascades;
            public bool SoftShadows;
            public bool FastSrgb;
            public float LodBias;
            public int SkinWeights;
        }

        private static readonly TierSpec[] Tiers =
        {
            // T16: the gameplay camera sits 13.8 m from its focus, so realtime shadows must reach ~24 m to cover the play
            // area. Low drops the shadow map entirely and grounds animals and eggs with blob shadows (Performance Bible).
            new TierSpec { Name = "Low", RenderScale = 0.85f, Msaa = 1, ShadowResolution = 512, MainLightShadows = false, ShadowDistance = 0f, Cascades = 1, SoftShadows = false, FastSrgb = true, LodBias = 0.7f, SkinWeights = 2 },
            new TierSpec { Name = "Medium", RenderScale = 1.0f, Msaa = 2, ShadowResolution = 1024, MainLightShadows = true, ShadowDistance = 24f, Cascades = 1, SoftShadows = true, FastSrgb = false, LodBias = 1.0f, SkinWeights = 4 },
            new TierSpec { Name = "High", RenderScale = 1.0f, Msaa = 4, ShadowResolution = 2048, MainLightShadows = true, ShadowDistance = 30f, Cascades = 2, SoftShadows = true, FastSrgb = false, LodBias = 1.3f, SkinWeights = 4 },
        };

        [MenuItem("Pocket Wilds/Setup/Apply Project Settings (T00)", priority = 100)]
        public static void Apply()
        {
            var log = new StringBuilder();
            EnsureFolders();

            var renderer = EnsureRendererData(log);
            var pipelines = new UniversalRenderPipelineAsset[Tiers.Length];
            for (int i = 0; i < Tiers.Length; i++)
                pipelines[i] = EnsurePipelineAsset(Tiers[i], renderer, log);

            GraphicsSettings.defaultRenderPipeline = pipelines[1];
            ConfigureQualityLevels(pipelines, log);

            // URP creates these at the Assets root on first use. Keep them with the other render settings (GUIDs preserved).
            MoveAssetIfPresent("Assets/UniversalRenderPipelineGlobalSettings.asset", RenderingFolder + "/URP_GlobalSettings.asset", log);
            MoveAssetIfPresent("Assets/DefaultVolumeProfile.asset", RenderingFolder + "/URP_DefaultVolumeProfile.asset", log);
            ConfigurePlayer(log);

            var lighting = EnsureLightingSettings(log);
            EnsureScenes(lighting, log);
            ConfigureBuildScenes(log);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Setup] T00 project settings applied.\n" + log);
        }

        /// <summary>Re-applies only the URP renderer, pipeline assets and quality levels (T16 tier changes).</summary>
        [MenuItem("Pocket Wilds/Setup/Apply Quality Tiers", priority = 101)]
        public static void ApplyQualityTiers()
        {
            var log = new StringBuilder();
            var renderer = EnsureRendererData(log);
            var pipelines = new UniversalRenderPipelineAsset[Tiers.Length];
            for (int i = 0; i < Tiers.Length; i++)
                pipelines[i] = EnsurePipelineAsset(Tiers[i], renderer, log);
            GraphicsSettings.defaultRenderPipeline = pipelines[1];
            ConfigureQualityLevels(pipelines, log);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Quality tiers applied." + System.Environment.NewLine + log);
        }

        /// <summary>Batch entry point. Exits with 0 on success, 1 on failure.</summary>
        public static void RunBatch()
        {
            try
            {
                Apply();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                RenderingFolder, ScenesFolder, BenchmarkScenesFolder, LightingFolder,
                "Assets/_Game/Art/Animals", "Assets/_Game/Art/Rigs", "Assets/_Game/Art/Animations",
                "Assets/_Game/Art/Environment", "Assets/_Game/Art/Props", "Assets/_Game/Art/Eggs",
                "Assets/_Game/Art/Materials", "Assets/_Game/Art/Shaders", "Assets/_Game/Art/VFX",
                "Assets/_Game/Art/UI", "Assets/_Game/Art/Audio",
                "Assets/_Game/Content/Species", "Assets/_Game/Content/Biomes", "Assets/_Game/Content/Eggs",
                "Assets/_Game/Prefabs/Animals", "Assets/_Game/Prefabs/Eggs", "Assets/_Game/Prefabs/UI",
                "Assets/_Game/Prefabs/World", "Assets/_Game/Addressables",
            };

            foreach (var folder in folders)
                EnsureFolder(folder);
        }

        private static void MoveAssetIfPresent(string from, string to, StringBuilder log)
        {
            if (AssetDatabase.LoadMainAssetAtPath(from) == null || AssetDatabase.LoadMainAssetAtPath(to) != null)
                return;
            string error = AssetDatabase.MoveAsset(from, to);
            log.AppendLine(string.IsNullOrEmpty(error) ? $"Moved {from} -> {to}" : $"WARNING move failed {from}: {error}");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---------------- Rendering ----------------

        private static UniversalRendererData EnsureRendererData(StringBuilder log)
        {
            string path = RenderingFolder + "/URP_Renderer_Mobile.asset";
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, path);
                log.AppendLine("Created " + path);
            }

            if (data.postProcessData == null)
                data.postProcessData = FindDefaultPostProcessData(log);

            data.renderingMode = RenderingMode.Forward;
            data.depthPrimingMode = DepthPrimingMode.Disabled;
            data.rendererFeatures.Clear();
            EditorUtility.SetDirty(data);
            return data;
        }

        private static PostProcessData FindDefaultPostProcessData(StringBuilder log)
        {
            var method = typeof(PostProcessData).GetMethod("GetDefaultPostProcessData", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null && method.Invoke(null, null) is PostProcessData fromApi)
            {
                log.AppendLine("PostProcessData: package default (API).");
                return fromApi;
            }

            var fromPackage = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            log.AppendLine(fromPackage != null ? "PostProcessData: package default (asset)." : "WARNING PostProcessData not found; post-processing unavailable.");
            return fromPackage;
        }

        private static UniversalRenderPipelineAsset EnsurePipelineAsset(TierSpec tier, UniversalRendererData renderer, StringBuilder log)
        {
            string path = $"{RenderingFolder}/URP_{tier.Name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, path);
                log.AppendLine("Created " + path);
            }

            var so = new SerializedObject(asset);
            SetFloat(so, "m_RenderScale", tier.RenderScale, log);
            SetInt(so, "m_MSAA", tier.Msaa, log);
            SetBool(so, "m_SupportsHDR", false, log);
            SetBool(so, "m_MainLightShadowsSupported", tier.MainLightShadows, log);
            SetInt(so, "m_MainLightShadowmapResolution", tier.ShadowResolution, log);
            SetInt(so, "m_AdditionalLightsRenderingMode", 0, log);
            SetBool(so, "m_AdditionalLightShadowsSupported", false, log);
            SetFloat(so, "m_ShadowDistance", tier.ShadowDistance, log);
            SetInt(so, "m_ShadowCascadeCount", tier.Cascades, log);
            SetBool(so, "m_SoftShadowsSupported", tier.SoftShadows, log);
            SetBool(so, "m_UseSRPBatcher", true, log);
            SetBool(so, "m_SupportsDynamicBatching", false, log);
            SetBool(so, "m_UseFastSRGBLinearConversion", tier.FastSrgb, log);
            SetBool(so, "m_SupportsLightCookies", false, log);
            SetBool(so, "m_ReflectionProbeBlending", false, log);
            SetBool(so, "m_ReflectionProbeBoxProjection", false, log);
            SetBool(so, "m_SupportsTerrainHoles", false, log);
            SetInt(so, "m_ColorGradingMode", 0, log);
            SetInt(so, "m_ColorGradingLutSize", 16, log);
            SetInt(so, "m_GPUResidentDrawerMode", 0, log);
            so.ApplyModifiedPropertiesWithoutUndo();

            // No depth/opaque copies by default: nothing in the baseline needs them.
            asset.supportsCameraDepthTexture = false;
            asset.supportsCameraOpaqueTexture = false;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void ConfigureQualityLevels(UniversalRenderPipelineAsset[] pipelines, StringBuilder log)
        {
            var qualityAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var so = new SerializedObject(qualityAsset);
            var levels = so.FindProperty("m_QualitySettings");
            levels.arraySize = Tiers.Length;

            for (int i = 0; i < Tiers.Length; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                var tier = Tiers[i];
                SetString(level, "name", tier.Name, log);
                SetObject(level, "customRenderPipeline", pipelines[i], log);
                SetInt(level, "pixelLightCount", 0, log);
                SetInt(level, "shadows", 0, log);
                SetInt(level, "skinWeights", tier.SkinWeights, log);
                SetFloat(level, "lodBias", tier.LodBias, log);
                SetInt(level, "maximumLODLevel", 0, log);
                SetInt(level, "vSyncCount", 0, log);
                SetInt(level, "antiAliasing", 0, log);
                SetInt(level, "anisotropicTextures", i == 0 ? 0 : 1, log);
                SetBool(level, "realtimeReflectionProbes", false, log);
                SetBool(level, "softParticles", false, log);
                SetBool(level, "softVegetation", false, log);
                SetBool(level, "billboardsFaceCameraPosition", false, log);
                SetInt(level, "particleRaycastBudget", 16, log);
                SetInt(level, "asyncUploadTimeSlice", 2, log);
                SetInt(level, "asyncUploadBufferSize", 16, log);
                SetInt(level, "globalTextureMipmapLimit", 0, log);
            }

            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null && perPlatform.isArray)
            {
                for (int i = 0; i < perPlatform.arraySize; i++)
                {
                    var second = perPlatform.GetArrayElementAtIndex(i).FindPropertyRelative("second");
                    if (second != null)
                        second.intValue = (int)PocketWilds.Core.QualityTier.Medium;
                }
            }
            else
            {
                log.AppendLine("WARNING m_PerPlatformDefaultQuality not found.");
            }

            var current = so.FindProperty("m_CurrentQuality");
            if (current != null)
                current.intValue = (int)PocketWilds.Core.QualityTier.Medium;

            so.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.SetQualityLevel((int)PocketWilds.Core.QualityTier.Medium, true);
            log.AppendLine("Quality levels: " + string.Join(", ", QualitySettings.names));
        }

        // ---------------- Player ----------------

        private static void ConfigurePlayer(StringBuilder log)
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "1.0")
                PlayerSettings.bundleVersion = "0.1.0";

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, ApplicationId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, ApplicationId);

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.useAnimatedAutorotation = false;

            // Desktop window for quick checks keeps the portrait phone ratio.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 1170;
            PlayerSettings.resizableWindow = true;

            PlayerSettings.gcIncremental = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.optimizedFramePacing = true;
            TrySetAndroidMinSdk(log);

            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);

            // Input System only (1). Touch input goes through com.unity.inputsystem.
            var playerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
            var so = new SerializedObject(playerAsset);
            SetInt(so, "activeInputHandler", 1, log);
            so.ApplyModifiedPropertiesWithoutUndo();

            log.AppendLine($"Player: {ProductName} ({ApplicationId}), portrait, Linear, IL2CPP ARM64.");
        }

        private static void TrySetAndroidMinSdk(StringBuilder log)
        {
            foreach (var name in new[] { "AndroidApiLevel26", "AndroidApiLevel25", "AndroidApiLevel24" })
            {
                if (Enum.TryParse(name, out AndroidSdkVersions version))
                {
                    if ((int)PlayerSettings.Android.minSdkVersion < (int)version)
                        PlayerSettings.Android.minSdkVersion = version;
                    log.AppendLine("Android min SDK: " + PlayerSettings.Android.minSdkVersion);
                    return;
                }
            }
            log.AppendLine("Android min SDK left at default: " + PlayerSettings.Android.minSdkVersion);
        }

        // ---------------- Lighting and scenes ----------------

        private static LightingSettings EnsureLightingSettings(StringBuilder log)
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (settings == null)
            {
                settings = new LightingSettings { name = "LS_Sanctuary_NoGI" };
                AssetDatabase.CreateAsset(settings, LightingSettingsPath);
                log.AppendLine("Created " + LightingSettingsPath);
            }
            settings.bakedGI = false;
            settings.realtimeGI = false;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static void EnsureScenes(LightingSettings lighting, StringBuilder log)
        {
            var previousSetup = EditorSceneManager.GetSceneManagerSetup();

            EnsureScene(BootstrapScenePath, lighting, log, scene =>
            {
                CreateCamera("BootCamera", new Color(0.965f, 0.937f, 0.886f), renderWorld: false);
                new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            });

            EnsureScene(VerticalSliceScenePath, lighting, log, scene =>
            {
                ConfigureSanctuaryRenderSettings();
                CreateCamera("Main Camera", new Color(0.75f, 0.89f, 0.95f), renderWorld: true);
                CreateSun();
                new GameObject("World");
                new GameObject("SceneEntry").AddComponent<VerticalSliceEntry>();
            });

            EnsureScene(BenchmarkAnimalsScenePath, lighting, log, scene =>
            {
                ConfigureSanctuaryRenderSettings();
                CreateCamera("Main Camera", new Color(0.75f, 0.89f, 0.95f), renderWorld: true);
                CreateSun();
                new GameObject("BenchmarkEntry").AddComponent<BenchmarkAnimalsEntry>();
            });

            EnsureScene(BenchmarkUIScenePath, lighting, log, scene =>
            {
                CreateCamera("Main Camera", new Color(0.2f, 0.2f, 0.22f), renderWorld: false);
                new GameObject("BenchmarkEntry");
            });

            bool restorable = previousSetup != null && previousSetup.Length > 0;
            if (restorable)
            {
                foreach (var setup in previousSetup)
                    restorable &= !string.IsNullOrEmpty(setup.path);
            }
            if (restorable)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        private static void EnsureScene(string path, LightingSettings lighting, StringBuilder log, Action<Scene> populate)
        {
            if (File.Exists(path))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            populate(scene);
            Lightmapping.SetLightingSettingsForScene(scene, lighting);
            EditorSceneManager.SaveScene(scene, path);
            log.AppendLine("Created scene " + path);
        }

        private static void ConfigureSanctuaryRenderSettings()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.86f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.82f, 0.84f, 0.76f);
            RenderSettings.ambientGroundColor = new Color(0.55f, 0.6f, 0.45f);
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        }

        private static Camera CreateCamera(string name, Color background, bool renderWorld)
        {
            var go = new GameObject(name);
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            if (renderWorld)
            {
                go.tag = "MainCamera";
                camera.fieldOfView = 30f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 80f;
                go.transform.SetPositionAndRotation(new Vector3(0f, 9f, -10.5f), Quaternion.Euler(40f, 0f, 0f));
            }
            else
            {
                camera.cullingMask = 0;
            }

            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = renderWorld;
            return camera;
        }

        private static void CreateSun()
        {
            var go = new GameObject("Sun");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void ConfigureBuildScenes(StringBuilder log)
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(VerticalSliceScenePath, true),
            };
            log.AppendLine("Build scenes: SC_Bootstrap, SC_VerticalSlice_Grassland (benchmarks excluded).");
        }

        // ---------------- SerializedObject helpers ----------------

        private static void SetFloat(SerializedObject so, string name, float value, StringBuilder log)
        {
            var p = so.FindProperty(name);
            if (p == null) { log.AppendLine("WARNING missing " + name); return; }
            p.floatValue = value;
        }

        private static void SetInt(SerializedObject so, string name, int value, StringBuilder log)
        {
            var p = so.FindProperty(name);
            if (p == null) { log.AppendLine("WARNING missing " + name); return; }
            p.intValue = value;
        }

        private static void SetBool(SerializedObject so, string name, bool value, StringBuilder log)
        {
            var p = so.FindProperty(name);
            if (p == null) { log.AppendLine("WARNING missing " + name); return; }
            p.boolValue = value;
        }

        private static void SetString(SerializedProperty parent, string name, string value, StringBuilder log)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null) { log.AppendLine("WARNING missing quality." + name); return; }
            p.stringValue = value;
        }

        private static void SetObject(SerializedProperty parent, string name, UnityEngine.Object value, StringBuilder log)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null) { log.AppendLine("WARNING missing quality." + name); return; }
            p.objectReferenceValue = value;
        }

        private static void SetFloat(SerializedProperty parent, string name, float value, StringBuilder log)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null) { log.AppendLine("WARNING missing quality." + name); return; }
            p.floatValue = value;
        }

        private static void SetInt(SerializedProperty parent, string name, int value, StringBuilder log)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null) { log.AppendLine("WARNING missing quality." + name); return; }
            p.intValue = value;
        }

        private static void SetBool(SerializedProperty parent, string name, bool value, StringBuilder log)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null) { log.AppendLine("WARNING missing quality." + name); return; }
            p.boolValue = value;
        }
    }
}
