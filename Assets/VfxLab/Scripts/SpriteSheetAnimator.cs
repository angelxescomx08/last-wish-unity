using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastWish.VfxLab
{
    /// <summary>
    /// Plays a row of equal cells from a sprite sheet texture (sprites are cut once at runtime and cached),
    /// so pixel-art sheets need no sprite slicing package. A follower (e.g. the emissive layer of a card
    /// illustration) can mirror another animator's frame.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteSheetAnimator : MonoBehaviour
    {
        [Serializable]
        public struct Clip
        {
            public string name;
            public int row;
            public int frames;
            public float fps;
            public bool loop;
        }

        public Texture2D sheet;
        public int cellWidth = 88;
        public int cellHeight = 64;
        [Tooltip("Pivote normalizado de cada celda (0,0 = esquina inferior izquierda).")]
        public Vector2 pivot = new Vector2(0.5f, 0.5f);
        public float pixelsPerUnit = 16f;
        public Clip[] clips = { new Clip { name = "idle", row = 0, frames = 24, fps = 12, loop = true } };
        [Tooltip("Desfase inicial en segundos (para que varias copias no vayan sincronizadas).")]
        public float timeOffset;
        [Tooltip("Si se asigna, este animador copia el fotograma del otro (capa emisiva).")]
        public SpriteSheetAnimator follow;
        public float speed = 1f;

        public event Action<string> Finished;
        public string CurrentClip => current.name;
        public int Frame { get; private set; }
        public float ClipTime { get; private set; }

        private static readonly Dictionary<(Texture2D, int, int, int, Vector2, float), Sprite[]> Cache = new Dictionary<(Texture2D, int, int, int, Vector2, float), Sprite[]>();
        private SpriteRenderer sr;
        private Clip current;
        private Sprite[] frames;
        private string fallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (clips != null && clips.Length > 0) Use(clips[0]);
            ClipTime = timeOffset;
            fallback = clips != null && clips.Length > 0 ? clips[0].name : null;
            Apply();
        }

        private void Use(Clip clip)
        {
            current = clip;
            frames = Cut(sheet, cellWidth, cellHeight, clip.row, pivot, pixelsPerUnit);
        }

        /// <summary>Play a clip by name; a non-looping clip returns to the first clip when it ends.</summary>
        public void Play(string clipName, string thenPlay = null, float startTime = 0f)
        {
            foreach (var c in clips)
            {
                if (c.name != clipName) continue;
                Use(c);
                ClipTime = startTime;
                fallback = thenPlay ?? (clips.Length > 0 ? clips[0].name : null);
                Apply();
                return;
            }
        }

        /// <summary>Play a clip starting at a given frame.</summary>
        public void PlayFromFrame(string clipName, int frame, string thenPlay = null)
        {
            foreach (var c in clips)
                if (c.name == clipName) { Play(clipName, thenPlay, frame / Mathf.Max(0.01f, c.fps)); return; }
        }

        public float Duration(string clipName)
        {
            foreach (var c in clips) if (c.name == clipName) return c.frames / Mathf.Max(0.01f, c.fps);
            return 0f;
        }

        private void LateUpdate()
        {
            if (follow != null) { Frame = follow.Frame; ApplyFrame(); }
        }

        private void Update()
        {
            if (follow != null) return;
            if (frames == null || frames.Length == 0) return;
            ClipTime += Time.deltaTime * speed;
            var f = Mathf.FloorToInt(ClipTime * current.fps);
            if (!current.loop && f >= current.frames)
            {
                var ended = current.name;
                if (!string.IsNullOrEmpty(fallback) && fallback != current.name) Play(fallback);
                else Frame = current.frames - 1;
                Finished?.Invoke(ended);
                return;
            }
            Apply();
        }

        private void Apply()
        {
            if (frames == null || frames.Length == 0) return;
            var f = Mathf.FloorToInt(ClipTime * current.fps);
            Frame = current.loop ? ((f % current.frames) + current.frames) % current.frames : Mathf.Clamp(f, 0, current.frames - 1);
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (frames == null && follow != null && clips.Length > 0) Use(clips[0]);
            if (frames == null || frames.Length == 0) return;
            sr.sprite = frames[Mathf.Clamp(Frame, 0, frames.Length - 1)];
        }

        public static Sprite[] Cut(Texture2D tex, int w, int h, int row, Vector2 pivot, float ppu)
        {
            if (tex == null || w <= 0 || h <= 0) return Array.Empty<Sprite>();
            var key = (tex, w, h, row, pivot, ppu);
            if (Cache.TryGetValue(key, out var cached) && cached.Length > 0 && cached[0] != null) return cached;
            var columns = tex.width / w;
            var result = new Sprite[columns];
            var y = tex.height - (row + 1) * h;
            for (var i = 0; i < columns; i++)
            {
                var s = Sprite.Create(tex, new Rect(i * w, y, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
                s.name = tex.name + "_" + row + "_" + i;
                s.hideFlags = HideFlags.DontSave;
                result[i] = s;
            }
            Cache[key] = result;
            return result;
        }
    }
}
