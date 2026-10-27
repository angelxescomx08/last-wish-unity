using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastWish.VfxLab.Editor
{
    /// <summary>
    /// Development hook for unattended iteration: when Temp/vfxlab-build.request or Temp/vfxlab-capture.request
    /// exists, the editor refreshes (picking up new scripts), then rebuilds the lab and, for a capture request,
    /// runs the capture test. Temp/vfxlab-tests.request runs the project's tests instead: its text is the mode
    /// ("EditMode" or "PlayMode") optionally followed by a test class or namespace, and the result is written to
    /// TestResults/remote-*.txt (see <see cref="VfxLabTestReport"/>). Temp/ is removed when Unity closes and is
    /// ignored by Git. Never touches a dirty scene and never runs with compile errors.
    /// </summary>
    [InitializeOnLoad]
    internal static class VfxLabRemote
    {
        public const string BuildRequest = "Temp/vfxlab-build.request";
        public const string CaptureRequest = "Temp/vfxlab-capture.request";
        public const string TestsRequest = "Temp/vfxlab-tests.request";
        public const string StatusFile = "Temp/vfxlab-remote.status";
        private static double next;
        private static bool refreshed;
        private static readonly DateTime loadedAt = DateTime.UtcNow;     // this domain (scripts compiled before it)

        static VfxLabRemote()
        {
            if (Application.isBatchMode) return;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        /// <summary>
        /// Files written from outside the editor (another machine, a synced folder) can escape Unity's directory
        /// monitor until the next restart: import every asset under Assets changed since this domain loaded.
        /// </summary>
        private static void ImportChangedFiles()
        {
            var since = loadedAt.AddSeconds(-2);
            var changed = new System.Collections.Generic.HashSet<string>();
            foreach (var file in Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories))
            {
                if (File.GetLastWriteTimeUtc(file) < since) continue;
                var path = file.Replace('\\', '/');
                if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - 5);
                if (File.Exists(path) || Directory.Exists(path)) changed.Add(path);
            }
            if (changed.Count == 0) return;
            AssetDatabase.StartAssetEditing();
            try { foreach (var path in changed) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            finally { AssetDatabase.StopAssetEditing(); }
            Debug.Log("VFX Lab: importados " + changed.Count + " archivos cambiados desde fuera del editor.");
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var capture = File.Exists(CaptureRequest);
            var tests = File.Exists(TestsRequest);
            if (!capture && !tests && !File.Exists(BuildRequest)) return;
            if (!refreshed) { refreshed = true; ImportChangedFiles(); AssetDatabase.Refresh(); return; }   // the request survives the domain reload
            refreshed = false;
            string spec = null;
            if (tests) { try { spec = File.ReadAllText(TestsRequest).Trim(); } catch (IOException) { return; } }
            TryDelete(CaptureRequest);
            TryDelete(BuildRequest);
            TryDelete(TestsRequest);
            if (EditorUtility.scriptCompilationFailed)
            {
                Write("skipped: compile errors " + DateTime.Now.ToString("HH:mm:ss"));
                Debug.LogWarning("VFX Lab: hay errores de compilación; no se reconstruye, no se captura y no se prueba.");
                return;
            }
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                if (!EditorSceneManager.GetSceneAt(i).isDirty) continue;
                Write("skipped: unsaved scene " + EditorSceneManager.GetSceneAt(i).path);
                Debug.LogWarning("VFX Lab: hay una escena con cambios sin guardar; no se hace nada.");
                return;
            }
            try
            {
                if (tests) { var name = VfxLabTestReport.Run(spec); Write("tests started " + name + " " + DateTime.Now.ToString("HH:mm:ss")); }
                else if (capture) { VfxLabBuilder.BuildAndCaptureUnattended(); Write("capture started " + DateTime.Now.ToString("HH:mm:ss")); }
                else { VfxLabBuilder.BuildAll(); EditorSceneManager.OpenScene(VfxLabBuilder.ScenePath); Write("built " + DateTime.Now.ToString("HH:mm:ss")); }
            }
            catch (Exception e)
            {
                Write("error " + e.Message);
                Debug.LogException(e);
            }
        }

        private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } }

        private static void Write(string text) { try { File.WriteAllText(StatusFile, text); } catch (IOException) { } }
    }
}
