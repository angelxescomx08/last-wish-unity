using System;
using UnityEngine;
using UnityEngine.UI;

namespace LastWish.Presentation
{
    [Serializable] public sealed class SpriteClip
    {
        public string name;
        public int row;
        public float[] durations;
        public bool loop;
        public float Total { get { float total = 0; foreach (var d in durations) total += d; return total; } }
        public int FrameAt(float elapsed)
        {
            if (loop) elapsed %= Total;
            for (var i = 0; i < durations.Length; i++)
            { if (elapsed < durations[i]) return i; elapsed -= durations[i]; }
            return durations.Length - 1;
        }
    }
    [Serializable] public sealed class MoveClip { public string id; public string clip; }
    [Serializable] public sealed class AtlasDefinition
    {
        public int cellWidth;
        public int cellHeight;
        public float anchorX, anchorY;
        public MoveClip[] moves;
        public float attackContactSeconds;
        public SpriteClip[] clips;
        public SpriteClip Find(string name) => Array.Find(clips, clip => clip.name == name);
    }
    public sealed class AtlasAnimation : MonoBehaviour
    {
        private RawImage image;
        private Texture2D texture;
        private AtlasDefinition definition;
        private SpriteClip clip;
        private float elapsed;
        private int displayed = -1;
        private bool returnToIdle;
        public void Initialize(RawImage target, Texture2D atlas, AtlasDefinition data)
        { image = target; texture = atlas; definition = data; image.texture = texture; Play("idle"); }
        public void PlayMove(string move, string fallback)
        {
            var mapped = definition.moves == null ? null : Array.Find(definition.moves, item => item.id == move);
            Play(mapped == null ? fallback : mapped.clip);
        }
        public void Play(string animation, bool recover = true)
        {
            var selected = definition.Find(animation) ?? definition.Find("idle");
            if (selected == null || selected.durations.Length == 0) return;
            clip = selected; elapsed = 0; displayed = -1; returnToIdle = recover;
            ShowFrame(0);
        }
        private void Update()
        {
            if (clip == null) return;
            elapsed += Time.unscaledDeltaTime;
            if (!clip.loop && elapsed >= clip.Total && returnToIdle) { Play("idle"); return; }
            if (clip.loop && elapsed >= clip.Total) elapsed %= clip.Total;
            ShowFrame(clip.FrameAt(elapsed));
        }
        private void ShowFrame(int frame)
        {
            if (frame == displayed) return;
            displayed = frame;
            var w = (float)definition.cellWidth / texture.width;
            var h = (float)definition.cellHeight / texture.height;
            image.uvRect = new Rect(frame * w, 1 - (clip.row + 1) * h, w, h);
        }
    }
}
