using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>El Espectro drawn by code in Tools/VfxLab/enemies/wraith.html (one complete figure per frame, its sleeves and
    /// claws written frame by frame, exported by export_enemy_art.py through Node) at 12 fps like the heroes: animations,
    /// timing, strike events, every frame and halo cell inside its sheet, and the additive halo the animator adds, headless.</summary>
    public sealed class WraithArtTests
    {
        private static readonly (string name, int frames, bool loop)[] Clips_ =
        {
            ("idle", 24, true), ("attack", 10, false), ("hurt", 7, false), ("cast", 12, false), ("death", 17, false),
        };

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

        [Test] public void TheEspectroUsesTheSheetDrawnByCode()
        {
            var art = Load().art;
            var sheet = art.SheetForEnemy("Espectro");
            Assert.That(sheet.Id, Is.EqualTo("wraith"));
            Assert.That(sheet.Meta["generator"] as string, Is.EqualTo("Tools/VfxLab/enemies/wraith.html"));
            Assert.That((sheet.CellW, sheet.CellH, sheet.Scale), Is.EqualTo((128, 96, 2)), "128x96 native cells (room for the lunge and the wail), drawn x2");
            Assert.That(sheet.Anchor, Is.EqualTo((84 * 2, 93 * 2)), "the anchor is the shadow line under the figure");
            Assert.That(sheet.Top, Is.InRange(2, 40), "name and intent go above the hood");
            Assert.That(sheet.IdleBox.w > 20 && sheet.IdleBox.h > 50, Is.True, "the portrait holds the whole figure");
        }

        [Test] public void EveryAnimationHasItsFramesTimingAndLoop()
        {
            var sheet = Load().art.EnemySheetById("wraith");
            Assert.That(sheet.Animations.Keys, Is.EquivalentTo(Clips_.Select(c => c.name)));
            foreach (var (name, frames, loop) in Clips_)
            {
                var a = sheet.Animations[name];
                Assert.That((a.Durations.Length, a.Loop), Is.EqualTo((frames, loop)), name);
                Assert.That(a.Durations.All(d => System.Math.Abs(d - 1f / 12f) < 0.001f), Is.True, name + ": 12 fps frames (83/84 ms)");
            }
            Assert.That(sheet.Seconds("idle"), Is.EqualTo(2f).Within(1e-4f), "the idle loop lasts 2 s, like the heroes");
            Assert.That(sheet.Animations["death"].FrameAt(1e9f), Is.EqualTo(16), "death is held on its last (empty) frame");
            Assert.That(sheet.Terminal, Is.Empty);
        }

        [Test] public void TheClawsLandOnFrameFourAndTheWailOnFrameSix()
        {
            var sheet = Load().art.EnemySheetById("wraith");
            Assert.That(sheet.StrikeSeconds("attack").Single(), Is.EqualTo(0.333f).Within(1e-3f), "the claws rake on frame 5 (index 4)");
            Assert.That(sheet.StrikeSeconds("cast").Single(), Is.EqualTo(0.5f).Within(1e-3f), "the wail bursts on frame 7 (index 6)");
            Assert.That(sheet.StrikeSeconds("attack").Single(), Is.LessThan(sheet.Seconds("attack")));
            var anim = new EnemyAnimator(sheet, 3);
            Assert.That(anim.StrikeTime(), Is.EqualTo(0.333f).Within(1e-3f), "the hero flinches when the claws land");
            Assert.That(EnemyAnimator.StyleFor("wraith").Claw.x, Is.LessThan(-60), "the strike sparks burst in front, at the claws");
        }

        [Test] public void EveryFrameAndHaloCellIsInsideItsSheet()
        {
            var sheet = Load().art.EnemySheetById("wraith");
            foreach (var key in new[] { "Enemies/wraith_sheet", "Enemies/wraith_halo" })
                Assert.That(File.Exists(Path.Combine(Clips.Root, key + ".png")), Is.True, key);
            Assert.That(sheet.HasHalo, Is.True);
            Assert.That(sheet.HaloGain, Is.InRange(0.1f, 1f));
            foreach (var (name, frames, _) in Clips_)
                for (var i = 0; i < frames; i++)
                {
                    var t = sheet.Animations[name].SecondsUntil(i) + 0.01f;
                    var s = sheet.Frame(name, t);
                    var h = sheet.Halo(name, t);
                    Assert.That(s.X + s.W <= s.Texture.Width && s.Y + s.H <= s.Texture.Height, Is.True, $"{name} {i}");
                    Assert.That(h.X + h.W <= h.Texture.Width && h.Y + h.H <= h.Texture.Height, Is.True, $"halo {name} {i}");
                    Assert.That((h.X, h.Y, h.W, h.H), Is.EqualTo((s.X, s.Y, s.W, s.H)), "the halo cell matches the sprite cell");
                }
            foreach (var t in new[] { float.NaN, -5f, 1e9f })
                Assert.DoesNotThrow(() => sheet.Halo("death", t));
        }

        [Test] public void TheAnimatorAddsTheHaloOverTheSprite()
        {
            var (art, render) = Load();
            var canvas = new Canvas(render);
            var anim = new EnemyAnimator(art.EnemySheetById("wraith"), 5);
            anim.Draw(canvas, art, (400f, 300f));
            var halo = render.Quads.Where(q => q.tex == "Enemies/wraith_halo").ToList();
            Assert.That(halo.Count, Is.EqualTo(1));
            Assert.That(halo[0].blend, Is.EqualTo(Blend.Add));
            Assert.That(halo[0].color.R, Is.EqualTo(MathX.Round(255 * anim.Sheet.HaloGain)));
            var sprite = render.Quads.FindIndex(q => q.tex == "Enemies/wraith_sheet");
            Assert.That(sprite, Is.GreaterThanOrEqualTo(0).And.LessThan(render.Quads.FindIndex(q => q.tex == "Enemies/wraith_halo")), "light goes over the sprite");
            render.Quads.Clear();
            canvas.PushAlpha(0.5f); anim.DrawHalo(canvas, 0, 0); canvas.PopAlpha();
            Assert.That(render.Quads.Single().color.R, Is.EqualTo(MathX.Round(255 * anim.Sheet.HaloGain * 0.5f)), "fades with the canvas");
            render.Quads.Clear();
            canvas.PushAlpha(0f); anim.DrawHalo(canvas, 0, 0); canvas.PopAlpha();
            anim.DrawHalo(canvas, 0, 0, 0f);
            Assert.That(render.Quads, Is.Empty, "no light on an invisible enemy or before it arrives");
        }

        [Test] public void SheetsWithoutAHaloDrawNoLight()
        {
            var (art, render) = Load();
            var canvas = new Canvas(render);
            // any sheet not redrawn with the HTML method yet (they lose this status one by one)
            var old = Art.EnemySheetIds.Values.Distinct().Select(art.EnemySheetById).FirstOrDefault(s => s != null && !s.HasHalo);
            if (old == null) Assert.Pass("every enemy sheet has its halo now");
            var anim = new EnemyAnimator(old, 5);
            Assert.That(anim.Sheet.HasHalo, Is.False);
            anim.Draw(canvas, art, (400f, 300f));
            Assert.That(render.Quads.Any(q => q.tex != null && q.tex.Contains("_halo")), Is.False);
            Assert.That(anim.Sheet.Halo("idle", 0f).Valid, Is.False);
        }

        [Test] public void EveryActionPlaysAndTheDeathFinishes()
        {
            var anim = new EnemyAnimator(Load().art.EnemySheetById("wraith"), 9);
            foreach (var name in new[] { "attack", "hurt", "cast" })
            {
                anim.Play(name);
                Assert.That(anim.Action, Is.EqualTo(name));
                for (var k = 0; k < 40; k++) anim.Update(0.05f);
                Assert.That((anim.Action, anim.Busy), Is.EqualTo(((string)null, false)), name + " returns to the idle");
            }
            anim.Play("death");
            for (var k = 0; k < 40; k++) anim.Update(0.05f);
            Assert.That((anim.Dead, anim.DeathDone), Is.EqualTo((true, true)));
        }
    }
}
