using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LastWish.Presentation
{
    public sealed class GameAssets
    {
        private readonly Dictionary<string, Object> loaded = new Dictionary<string, Object>();
        public Font Font => Get<Font>("Fonts/Pixel");
        public T Get<T>(string path) where T : Object
        {
            var value = loaded[path] as T;
            if (value == null) throw new System.InvalidOperationException(
                "Resource " + path + " is not a live " + typeof(T).Name + "; imported type: " + loaded[path].GetType().Name);
            return value;
        }
        private readonly Dictionary<string, AtlasDefinition> atlasCache = new Dictionary<string, AtlasDefinition>();
        public AtlasDefinition Atlas(string id)
        {
            if (!atlasCache.TryGetValue(id, out var data))
                atlasCache[id] = data = JsonUtility.FromJson<AtlasDefinition>(Get<TextAsset>("Art/" + id + "Clips").text);
            return data;
        }
        public IEnumerator LoadEnemies(IEnumerable<string> ids, System.Action<string, float> progress)
        {
            var paths = new List<string>();
            foreach (var id in ids)
                foreach (var suffix in new[] { "Atlas", "Clips" })
                {
                    var path = "Art/" + id + suffix;
                    if (!loaded.ContainsKey(path) && !paths.Contains(path)) paths.Add(path);
                }
            for (var i = 0; i < paths.Count; i++)
            {
                progress("Preparando el encuentro...", (float)i / paths.Count);
                var type = paths[i].EndsWith("Clips") ? typeof(TextAsset) : typeof(Texture2D);
                var request = Resources.LoadAsync("Game/" + paths[i], type); yield return request;
                if (request.asset == null) throw new System.InvalidOperationException("Missing enemy resource: " + paths[i]);
                loaded.Add(paths[i], request.asset);
            }
            progress("Todo listo", 1);
        }
        public IEnumerator Load(System.Action<string, float> progress)
        {
            string[] paths = { "Fonts/Pixel", "Art/DungeonRoom", "Art/warriorAtlas", "Art/warriorClips",
                "Art/mageAtlas", "Art/mageClips", "Art/rogueAtlas", "Art/rogueClips",
                "Art/wraithAtlas", "Art/wraithClips", "Art/CardFrame", "Audio/Music", "Audio/Card", "Audio/Confirm", "Audio/Hit" };
            for (var i = 0; i < paths.Length; i++)
            {
                progress(i == 0 ? "Preparando la aventura..." : i < 11 ? "Despertando la mazmorra..." : "Afinando los sonidos...", (float)i / paths.Length);
                var type = paths[i].StartsWith("Fonts/") ? typeof(Font)
                    : paths[i].StartsWith("Audio/") ? typeof(AudioClip)
                    : paths[i].EndsWith("Clips") ? typeof(TextAsset) : typeof(Texture2D);
                var request = Resources.LoadAsync("Game/" + paths[i], type);
                yield return request;
                if (request.asset == null) throw new System.InvalidOperationException("Missing resource: " + paths[i]);
                loaded.Add(paths[i], request.asset);
            }
            progress("Todo listo", 1);
        }
    }
}
