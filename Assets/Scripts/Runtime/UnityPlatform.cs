using System;
using System.Collections.Generic;
using System.IO;
using LastWish.Presentation;
using UnityEngine;

namespace LastWish.Runtime
{
    /// <summary>Reads the JSON metadata from Resources/Game.</summary>
    public sealed class ResourceAssets : IAssetSource
    {
        private readonly Dictionary<string, string> cache = new Dictionary<string, string>();
        public string ReadText(string key)
        {
            if (cache.TryGetValue(key, out var text)) return text;
            var asset = Resources.Load<TextAsset>("Game/" + key);
            if (asset == null) throw new InvalidOperationException("Missing metadata: " + key);
            cache[key] = text = asset.text;
            Resources.UnloadAsset(asset);
            return text;
        }
    }

    /// <summary>preferences.json, dev_settings.json and checkpoints under Application.persistentDataPath (atomic writes).</summary>
    public sealed class FileStorage : IStorage
    {
        private static string Root => UnityEngine.Application.persistentDataPath;
        public string Read(string name)
        {
            foreach (var path in new[] { Path.Combine(Root, name), Path.Combine(Root, name + ".bak") })
            {
                try { if (File.Exists(path) && new FileInfo(path).Length < 4000000) return File.ReadAllText(path); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { Debug.LogWarning("No se pudo leer " + name + ": " + error.Message); }
            }
            return null;
        }
        public void Write(string name, string text)
        {
            try
            {
                Directory.CreateDirectory(Root);
                var path = Path.Combine(Root, name);
                File.WriteAllText(path + ".tmp", text);
                if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak"); else File.Move(path + ".tmp", path);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            { Debug.LogWarning("No se pudo guardar " + name + ": " + error.Message); }
        }
        public void Delete(string name)
        {
            try
            {
                var path = Path.Combine(Root, name);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { Debug.LogWarning("No se pudo borrar " + name + ": " + error.Message); }
        }
    }

    /// <summary>pygame.mixer equivalent: 8 effect voices and one streamed music source with a fade-in.</summary>
    public sealed class UnityAudio : IAudioBackend
    {
        /// <summary>Variants of each of <see cref="SoundPlayer.Effects"/> are name_0, name_1… up to the first missing file.</summary>
        private const int MaxVariants = 8;
        /// <summary>Seconds a stopped voice takes to fade out (an instant stop clicks).</summary>
        private const float StopFade = .04f;
        private readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        private readonly AudioSource[] voices;
        private readonly float[] fadeLeft, fadeFrom;
        private readonly AudioSource music;
        private float musicTarget, musicFade;
        private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

        public UnityAudio(GameObject host)
        {
            voices = new AudioSource[8]; fadeLeft = new float[voices.Length]; fadeFrom = new float[voices.Length];
            for (var i = 0; i < voices.Length; i++) { voices[i] = host.AddComponent<AudioSource>(); voices[i].playOnAwake = false; }
            music = host.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false;
        }

        public void LoadEffects()
        {
            foreach (var name in SoundPlayer.Effects)
            {
                var list = new List<AudioClip>();
                for (var v = 0; v < MaxVariants; v++)
                {
                    var clip = Resources.Load<AudioClip>("Game/Audio/" + name + "_" + v);
                    if (clip == null) break;
                    list.Add(clip);
                }
                clips[name] = list.ToArray();
            }
        }

        public long Ticks => clock.ElapsedMilliseconds;
        public int Variants(string name) => clips.TryGetValue(name, out var list) ? list.Length : 0;
        public int BusyVoices { get { var n = 0; foreach (var v in voices) if (v.isPlaying) n++; return n; } }

        public bool Play(string name, int variant, float volume)
        {
            if (!clips.TryGetValue(name, out var list) || list.Length == 0) return true;
            for (var i = 0; i < voices.Length; i++)
            {
                var voice = voices[i];
                if (voice.isPlaying) continue;
                fadeLeft[i] = 0f;
                voice.clip = list[variant % list.Length]; voice.volume = volume; voice.Play();
                return true;
            }
            return false;
        }

        public void Stop(string name)
        {
            if (!clips.TryGetValue(name, out var list) || list.Length == 0) return;
            for (var i = 0; i < voices.Length; i++)
            {
                if (!voices[i].isPlaying || fadeLeft[i] > 0f || Array.IndexOf(list, voices[i].clip) < 0) continue;
                fadeLeft[i] = StopFade; fadeFrom[i] = voices[i].volume;
            }
        }

        public void StartMusic(float volume)
        {
            if (music.clip == null) music.clip = Resources.Load<AudioClip>("Game/Audio/last_wish_theme");
            if (music.clip == null) return;
            musicTarget = volume; musicFade = 0f; music.volume = 0f; music.Play();
        }
        public void StopMusic() { music.Stop(); }
        public void SetMusicVolume(float volume) { musicTarget = volume; if (musicFade >= 1.2f) music.volume = volume; }

        /// <summary>The 1.2 s fade-in of pygame.mixer.music.play(fade_ms=1200), and the short fade of stopped effects.</summary>
        public void Tick(float dt)
        {
            for (var i = 0; i < voices.Length; i++)
            {
                if (fadeLeft[i] <= 0f) continue;
                fadeLeft[i] -= dt;
                if (fadeLeft[i] > 0f && voices[i].isPlaying) { voices[i].volume = fadeFrom[i] * fadeLeft[i] / StopFade; continue; }
                fadeLeft[i] = 0f; voices[i].Stop();
            }
            if (!music.isPlaying || musicFade >= 1.2f) return;
            musicFade += dt; music.volume = musicTarget * Mathf.Clamp01(musicFade / 1.2f);
        }
    }
}
