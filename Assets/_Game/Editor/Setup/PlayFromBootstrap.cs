using UnityEditor;
using UnityEditor.SceneManagement;

namespace PocketWilds.Editor.Setup
{
    /// <summary>
    /// Enters Play Mode through SC_Bootstrap, like a real launch. The start scene override is
    /// cleared afterwards so the Test Runner is never affected.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromBootstrap
    {
        private const string ActiveKey = "PocketWilds.PlayFromBootstrap.Active";

        static PlayFromBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Pocket Wilds/Play From Bootstrap", priority = 0)]
        public static void Play()
        {
            if (EditorApplication.isPlaying)
                return;

            var bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectSetup.BootstrapScenePath);
            if (bootstrap == null)
            {
                EditorUtility.DisplayDialog("Pocket Wilds", "SC_Bootstrap is missing. Run Pocket Wilds/Setup first.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.playModeStartScene = bootstrap;
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ActiveKey, false))
            {
                EditorSceneManager.playModeStartScene = null;
                SessionState.SetBool(ActiveKey, false);
            }
        }
    }
}
