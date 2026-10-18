using System;
using System.IO;
using LastWish.Domain;
using UnityEngine;

namespace LastWish.Presentation
{
    public static class RunPersistence
    {
        private static string SavePath => Path.Combine(UnityEngine.Application.persistentDataPath, "run-v1.json");
        public static void Save(RunState run)
        {
            try
            {
                var json = JsonUtility.ToJson(run.CreateSave(), true);
                Directory.CreateDirectory(UnityEngine.Application.persistentDataPath);
                File.WriteAllText(SavePath + ".tmp", json);
                if (File.Exists(SavePath)) File.Replace(SavePath + ".tmp", SavePath, SavePath + ".bak");
                else File.Move(SavePath + ".tmp", SavePath);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            { Debug.LogWarning("No se pudo guardar la partida: " + error.Message); }
        }
        public static RunState Load()
        {
            foreach (var path in new[] { SavePath, SavePath + ".bak" })
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    if (new FileInfo(path).Length > 2000000) continue;
                    return RunState.FromSave(JsonUtility.FromJson<RunSave>(File.ReadAllText(path)));
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is InvalidOperationException)
                { Debug.LogWarning("No se pudo leer el guardado: " + error.Message); }
            }
            return null;
        }
        public static void Delete()
        {
            try { if (File.Exists(SavePath)) File.Delete(SavePath); if (File.Exists(SavePath + ".bak")) File.Delete(SavePath + ".bak"); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { Debug.LogWarning("No se pudo borrar el guardado: " + error.Message); }
        }
    }
}
