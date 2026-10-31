using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>El Mago's sheets drawn by code in Tools/VfxLab/mage_hero.html (one complete figure per frame, exported by
    /// export_hero_art.py through Node) at 12 fps like his card illustrations: animations, timing, events, every frame inside
    /// its sheet and the additive halo, headless.</summary>
    public sealed class MageHeroArtTests
    {
        private static readonly (string name, int frames, bool loop)[] Clips_ =
        {
            ("idle", 24, true), ("attack", 10, false), ("guard", 9, false), ("hurt", 7, false), ("cast", 12, false), ("death", 17, false),
        };

        /// <summary>A hero sheet whose metadata has no "halo" entry (every hero in the game has one now).</summary>
        internal const string NoHaloJson = "{\"cell\":96,\"sheets\":{\"96\":\"Heroes/mage_96\"},\"pack\":{\"96\":{\"columns\":24,\"split\":1}}," +
            "\"events\":{},\"animations\":{\"idle\":{\"row\":0,\"durations_ms\":[83,84],\"loop\":true}}}";

        private sealed class Recorder : IRenderBackend
        {
            public readonly List<(string tex, Blend blend, Rgba color)> Quads = new List<(string, Blend, Rgba)>();
            private int next = 1;
            public Tex WhiteTexture { get; } = new Tex(0, "white", 1, 1);
            public Tex LoadTexture(string key) { var (w, h) = Clips.PngSize(key); return new Tex(next++, key, w, h); }
            public Tex CreateTexture(string key, int width, int height, Rgba[] pixels, bool smooth = false) => new Tex(next++, key, width, height, smooth);
            public void BeginFrame() { }
            public void Quad(Tex tex, float u0, float v0, float u1, float v1, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, Rgba color, Blend blend)
                => Quads.Add((tex?.Key, blend, color));
            public void Triangle(float x0, float y0, float x1, float y1, float x2, float y2, Rgba color, Blend blend) { }
            public void TexTriangle(Tex tex, float x0, float y0, float u0, float v0, float x1, float y1, float u1, float v1, float x2, float y2, float u2, float v2, Rgba color, Blend blend) { }
            public Tex CreateTarget(string key, int width, int height) => new Tex(next++, key, width, height, false, true);
            public void ReleaseTarget(Tex target) { }
            public void BeginTarget(Tex target) { }
            public void EndTarget() { }
            public void SetClip(RectF? clip) { }
            public void EndFrame() { }
        }

        private static (Art art, Recorder render) Load()
        {
            var render = new Recorder(); var art = new Art(render, new FileAssets()); art.LoadCore();
            return (art, render);
        }

        [Test] public void EveryAnimationHasItsFramesTimingAndLoop()
        {
            var sheets = Load().art.Hero("mage");
            foreach (var (name, frames, loop) in Clips_)
            {
                var a = sheets.Animations[name];
                Assert.That((a.Durations.Length, a.Loop), Is.EqualTo((frames, loop)), name);
                Assert.That(a.Durations.All(d => System.Math.Abs(d - 1f / 12f) < 0.001f), Is.True, name + ": 12 fps frames (83/84 ms)");
                Assert.That(a.Total, Is.EqualTo(frames / 12f).Within(0.001f), name + ": n frames last n / 12 s");
            }
            Assert.That(sheets.Seconds("idle"), Is.EqualTo(2f).Within(1e-4f), "the idle loop lasts 2 s, like the card illustrations");
            Assert.That(sheets.Animations["idle"].FrameAt(1.95f), Is.EqualTo(23), "the whole idle loop is reachable");
            Assert.That(sheets.Seconds("attack"), Is.EqualTo(0.833f).Within(1e-4f));
            Assert.That(sheets.Seconds("death"), Is.EqualTo(1.417f).Within(1e-4f));
            Assert.That(sheets.Animations["death"].FrameAt(1e9f), Is.EqualTo(16), "death is held on its last frame");
        }

        [Test] public void StrikeAndCastEventsLandInsideTheirClips()
        {
            var sheets = Load().art.Hero("mage");
            Assert.That(sheets.StrikeSeconds, Is.EqualTo(0.333f).Within(1e-4f), "the bolt leaves the crystal on frame 4");
            Assert.That(sheets.CastSeconds, Is.EqualTo(0.5f).Within(1e-4f), "the spell is released on frame 6");
            Assert.That(sheets.StrikeSeconds, Is.LessThan(sheets.Seconds("attack")));
            var (sx, sy) = sheets.StrikePoint.Value; var (cx, cy) = sheets.CastPoint.Value;
            Assert.That(sx > 60 && sx < 96 && sy > 10 && sy < 60, Is.True, $"the crystal in front of him ({sx}, {sy})");
            Assert.That(cx > 40 && cx < 80 && cy > 5 && cy < 40, Is.True, $"cast above his head, at the crystal ({cx}, {cy})");
            Assert.That(sheets.StrikeOffset.Value.x, Is.GreaterThan(0), "the strike is towards the enemies");
        }

        [Test] public void EveryFrameAndHaloCellIsInsideItsSheet()
        {
            var sheets = Load().art.Hero("mage");
            foreach (var key in new[] { "Heroes/mage_96", "Heroes/mage_192", "Heroes/mage_halo_96" })
                Assert.That(File.Exists(Path.Combine(Clips.Root, key + ".png")), Is.True, key);
            Assert.That(sheets.HasHalo, Is.True);
            Assert.That(sheets.HaloGain, Is.InRange(0.1f, 1f));
            foreach (var (name, frames, _) in Clips_)
                for (var i = 0; i < frames; i++)
                {
                    var t = sheets.Animations[name].SecondsUntil(i) + 0.01f;
                    foreach (var size in new[] { 96, 192 })
                    {
                        var s = sheets.Frame(name, t, size, out var scale);
                        Assert.That(s.X + s.W <= s.Texture.Width && s.Y + s.H <= s.Texture.Height, Is.True, $"{name} {i} @{size}");
                        Assert.That(s.W * scale, Is.EqualTo(size));
                    }
                    var h = sheets.Halo(name, t);
                    Assert.That(h.X + h.W <= h.Texture.Width && h.Y + h.H <= h.Texture.Height && h.W == 96, Is.True, $"halo {name} {i}");
                    Assert.That((h.X, h.Y), Is.EqualTo((sheets.Frame(name, t, 96, out _).X, sheets.Frame(name, t, 96, out _).Y)),
                        "the halo cell matches the sprite cell");
                }
        }

        [Test] public void TheHaloIsAddedAndFollowsAlphaAndTint()
        {
            var (art, render) = Load();
            var canvas = new Canvas(render);
            var sheets = art.Hero("mage");
            sheets.DrawHalo(canvas, "idle", 0.3f, new RectF(0, 0, 192, 192));
            Assert.That(render.Quads.Single().blend, Is.EqualTo(Blend.Add));
            var full = render.Quads.Single().color.R;
            Assert.That(full, Is.EqualTo(MathX.Round(255 * sheets.HaloGain)));
            render.Quads.Clear();
            canvas.PushAlpha(0.5f); sheets.DrawHalo(canvas, "attack", 0.3f, new RectF(0, 0, 192, 192)); canvas.PopAlpha();
            Assert.That(render.Quads.Single().color.R, Is.EqualTo(MathX.Round(255 * sheets.HaloGain * 0.5f)), "fades with the canvas");
            render.Quads.Clear();
            canvas.PushAlpha(0f); sheets.DrawHalo(canvas, "idle", 0f, new RectF(0, 0, 192, 192)); canvas.PopAlpha();
            sheets.DrawHalo(canvas, "idle", 0f, new RectF(0, 0, 192, 192), tint: new Rgba(0, 0, 0));
            Assert.That(render.Quads, Is.Empty, "no light on an invisible or blacked-out hero");
            var bare = new HeroSheets("bare", NoHaloJson, key => render.LoadTexture(key));
            bare.DrawHalo(canvas, "idle", 0f, new RectF(0, 0, 192, 192));
            Assert.That((bare.HasHalo, render.Quads.Count), Is.EqualTo((false, 0)), "heroes without a halo sheet draw nothing");
            foreach (var t in new[] { float.NaN, -5f, 1e9f })
                Assert.DoesNotThrow(() => sheets.Halo("death", t));
        }
    }
}
