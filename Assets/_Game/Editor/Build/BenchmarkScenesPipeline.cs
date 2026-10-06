using System;
using System.IO;
using PocketWilds.Benchmarks;
using PocketWilds.Editor.Setup;
using PocketWilds.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Build
{
    /// <summary>
    /// T34C benchmark scenes (doc 11 L-SC-007, L-SC-008), from code: SC_Benchmark_UI gets its driver (Codex 5000 with the
    /// real PF_UI_Codex and PF_UI_HUD_Main) and SC_Benchmark_VFX is created (camera at the refuge framing over a meadow
    /// coloured background, the effects driver). Both ship only in development builds (BuildValidator refuses them in a
    /// release). Idempotent.
    /// </summary>
    public static class BenchmarkScenesPipeline
    {
        public const string VfxScenePath = ProjectSetup.BenchmarkScenesFolder + "/SC_Benchmark_VFX.unity";
        public const string CodexPrefabPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_Codex.prefab";
        public const string HudPrefabPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_HUD_Main.prefab";

        [MenuItem("Pocket Wilds/Build/Setup Benchmark Scenes (T34C)", priority = 320)]
        public static void SetupMenu() => Debug.Log("[BenchmarkScenes] " + Setup());

        public static string Setup()
        {
            var previous = EditorSceneManager.GetActiveScene().path;
            WireUiScene();
            CreateVfxScene();
            if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            return "SC_Benchmark_UI wired, SC_Benchmark_VFX ready";
        }

        private static void WireUiScene()
        {
            var scene = EditorSceneManager.OpenScene(ProjectSetup.BenchmarkUIScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("BenchmarkEntry") ?? new GameObject("BenchmarkEntry");
            var entry = root.GetComponent<BenchmarkUiEntry>() ?? root.AddComponent<BenchmarkUiEntry>();
            var so = new SerializedObject(entry);
            so.FindProperty("_codexPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(CodexPrefabPath).GetComponent<CodexScreen>()
                                                                   ?? throw new InvalidOperationException("PF_UI_Codex has no CodexScreen.");
            so.FindProperty("_hudPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath).GetComponent<RefugeHud>()
                                                                 ?? throw new InvalidOperationException("PF_UI_HUD_Main has no RefugeHud.");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void CreateVfxScene()
        {
            var scene = File.Exists(VfxScenePath)
                ? EditorSceneManager.OpenScene(VfxScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
                Object.DestroyImmediate(go);

            // Refuge framing: 40 degrees down, 9.5 m from the burst (the hatch zoom), meadow green behind the effects.
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.47f, 0.63f, 0.36f);
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 60f;
            var data = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            var rotation = Quaternion.Euler(40f, 0f, 0f);
            cameraGo.transform.SetPositionAndRotation(rotation * new Vector3(0f, 0f, -9.5f) + new Vector3(0f, 0.3f, 0f), rotation);
            cameraGo.AddComponent<AudioListener>();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            new GameObject("BenchmarkEntry").AddComponent<BenchmarkVfxEntry>();
            Directory.CreateDirectory(Path.GetDirectoryName(VfxScenePath));
            EditorSceneManager.SaveScene(scene, VfxScenePath);
        }
    }
}
