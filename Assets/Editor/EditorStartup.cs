using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastWish.Editor
{
    public static class EditorStartup
    {
        private const string PreparedKey = "LastWish.EditorPrepared";

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (!UnityEngine.Application.isBatchMode) EditorApplication.delayCall += PrepareWorkspace;
        }

        private static void PrepareWorkspace()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(PreparedKey, false)) return;
            var bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectSetup.ScenePath);
            if (bootstrap == null) return;
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            EditorSceneManager.playModeStartScene = bootstrap;
            foreach (SceneView view in SceneView.sceneViews) view.in2DMode = true;
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && !scene.isDirty && string.IsNullOrEmpty(scene.path) && scene.rootCount == 0)
                EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            SessionState.SetBool(PreparedKey, true);
        }

        [MenuItem("Last Wish/Jugar")]
        public static void PlayGame()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.isPlaying = true;
        }
    }
}
