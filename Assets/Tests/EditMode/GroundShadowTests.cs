using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>Contact shadows under heroes and enemies (Fx/GroundShadow.cs): the pixel-art shadow itself, where it lands
    /// against the ground line, the per-frame feet of every hero sheet (export_hero_art.ground), the main menu, combat's
    /// enemies; headless, no captures.</summary>
    public sealed class GroundShadowTests
    {
        private sealed class Recorder : IRenderBackend
        {
            public readonly List<(string tex, Blend blend, Rgba color, float x0, float y0, float x1, float y1)> Quads = new List<(string, Blend, Rgba, float, float, float, float)>();
            public readonly Dictionary<string, (int w, int h, Rgba[] px)> Created = new Dictionary<string, (int, int, Rgba[])>();
            private int next = 1;
            public Tex WhiteTexture { get; } = new Tex(0, "white", 1, 1);
            public Tex LoadTexture(string key) { var (w, h) = Clips.PngSize(key); return new Tex(next++, key, w, h); }
            public Tex CreateTexture(string key, int width, int height, Rgba[] pixels, bool smooth = false) { Created[key] = (width, height, pixels); return new Tex(next++, key, width, height, smooth); }
            public void BeginFrame() { }
            public void Quad(Tex tex, float u0, float v0, float u1, float v1, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, Rgba color, Blend blend)
                => Quads.Add((tex?.Key, blend, color, Math.Min(Math.Min(x0, x1), Math.Min(x2, x3)), Math.Min(Math.Min(y0, y1), Math.Min(y2, y3)),
                    Math.Max(Math.Max(x0, x1), Math.Max(x2, x3)), Math.Max(Math.Max(y0, y1), Math.Max(y2, y3))));
            public void Triangle(float x0, float y0, float x1, float y1, float x2, float y2, Rgba color, Blend blend) { }
            public void TexTriangle(Tex tex, float x0, float y0, float u0, float v0, float x1, float y1, float u1, float v1, float x2, float y2, float u2, float v2, Rgba color, Blend blend) { }
            public Tex CreateTarget(string key, int width, int height) => new Tex(next++, key, width, height, false, true);
            public void ReleaseTarget(Tex target) { }
            public void BeginTarget(Tex target) { }
            public void EndTarget() { }
            public void SetClip(RectF? clip) { }
            public void EndFrame() { }
            public IEnumerable<(string tex, Blend blend, Rgba color, float x0, float y0, float x1, float y1)> Shadows => Quads.Where(q => q.tex != null && q.tex.StartsWith("ground_shadow_"));
        }

        private static (Art art, Recorder render, Canvas canvas) Load()
        {
            var render = new Recorder(); var art = new Art(render, new FileAssets()); art.LoadCore();
            return (art, render, new Canvas(render));
        }

        private static readonly string[] HeroIds = { "warrior", "mage", "rogue" };

        [Test] public void TheShadowIsPixelArtDarkInTheMiddleAndDitheredAtTheEdge()
        {
            var (art, render, _) = Load();
            foreach (var w in new[] { 1, 6, 30, 76, 10_000 })
            {
                var s = GroundShadow.Sprite(art, w);
                var (cw, ch, px) = render.Created[s.Texture.Key];
                Assert.That(cw, Is.InRange(GroundShadow.MinW, GroundShadow.MaxW), $"width {w} is clamped");
                Assert.That(ch, Is.EqualTo(GroundShadow.HeightFor(cw)).And.InRange(3, 16), "flat");
                Assert.That(px.All(p => p.A == 0 || GroundShadow.Tones.Contains(p.A) && p.R == GroundShadow.Ink.R && p.B == GroundShadow.Ink.B), Is.True, "only the three tones of one ink");
                Assert.That(new int[] { px[0].A, px[cw - 1].A, px[(ch - 1) * cw].A, px[ch * cw - 1].A }, Is.All.EqualTo(0), "transparent corners");
                if (cw >= 20) Assert.That(px[ch / 2 * cw + cw / 2].A, Is.EqualTo(GroundShadow.Tones.Max()), "darkest under the middle");
                Assert.That(Enumerable.Range(0, ch).Sum(y => px.Skip(y * cw).Take(cw).Count(p => p.A > 0)), Is.GreaterThan(cw), "not empty");
            }
            Assert.That(GroundShadow.Sprite(art, 30).Texture, Is.SameAs(GroundShadow.Sprite(art, 30).Texture), "cached per size");
        }

        [Test] public void ItLandsOnTheGroundLineCentredUnderTheFeet()
        {
            var (art, render, canvas) = Load();
            GroundShadow.Draw(canvas, art, 300, 280, 60, 2);
            var q = render.Shadows.Single();
            Assert.That(q.blend, Is.EqualTo(Blend.Alpha), "a shadow darkens, it is not light");
            Assert.That((q.x0 + q.x1) / 2, Is.EqualTo(300).Within(1f), "centred on x");
            Assert.That(q.x1 - q.x0, Is.EqualTo(60).Within(2f), "60 px wide in 2 px art pixels");
            Assert.That((q.y0 + q.y1) / 2, Is.EqualTo(280).Within(1f), "centred on the line the soles stand on");
            Assert.That(render.Quads.Count, Is.EqualTo(1), "no floor light unless asked for");
            render.Quads.Clear();
            GroundShadow.Draw(canvas, art, 300, 280, 60, 2, 1f, GroundShadow.FloorLight);
            Assert.That(render.Quads.Select(r => r.blend), Is.EqualTo(new[] { Blend.Add, Blend.Alpha }), "the floor light first, the shadow on it");
            var light = render.Quads[0];
            Assert.That((light.x0 + light.x1) / 2, Is.EqualTo(300f).Within(1.5f), "around the same spot");
            Assert.That((light.y0 + light.y1) / 2, Is.EqualTo(280f).Within(1.5f));
            Assert.That(light.x1 - light.x0, Is.GreaterThan(q.x1 - q.x0), "wider than the shadow");
            render.Quads.Clear();
            foreach (var (a, w, x) in new[] { (0f, 60f, 300f), (0.5f, 0f, 300f), (1f, -5f, 300f), (1f, 60f, float.NaN), (float.NaN, 60f, 300f) })
                GroundShadow.Draw(canvas, art, x, 280, w, 2, a);
            Assert.That(render.Quads, Is.Empty, "nothing for an invisible, empty or broken shadow");
            GroundShadow.Draw(canvas, art, 300, 280, 60, 2, 0.5f);
            Assert.That(render.Shadows.Single().color.A, Is.EqualTo(127).Within(1), "fades with the figure");
        }

        [Test] public void EveryHeroFrameKnowsWhereItsFeetTouchTheFloor()
        {
            var art = Load().art;
            foreach (var id in HeroIds)
            {
                var sheets = art.Hero(id);
                Assert.That(sheets.HasGround, Is.True, id);
                foreach (var pair in sheets.Animations)
                    for (var f = 0; f < pair.Value.Durations.Length; f++)
                    {
                        var g = sheets.GroundAt(pair.Key, pair.Value.SecondsUntil(f) + 0.01f).Value;
                        Assert.That(g.y, Is.EqualTo(94f), $"{id} {pair.Key} {f}: the line under the soles (row 93)");
                        Assert.That(g.x, Is.InRange(12f, 84f), $"{id} {pair.Key} {f}");
                        Assert.That(g.w, Is.InRange(8f, 60f), $"{id} {pair.Key} {f}");
                    }
                var idle = sheets.GroundAt("idle", 0f).Value;
                Assert.That(idle.x, Is.InRange(36f, 46f), $"{id}: the figure stands left of the cell's middle, facing the enemies");
                Assert.That(sheets.GroundAt("attack", sheets.StrikeSeconds + 0.01f).Value.x, Is.GreaterThan(idle.x), $"{id}: the shadow follows the lunge");
                foreach (var t in new[] { float.NaN, -5f, 1e9f }) Assert.DoesNotThrow(() => sheets.GroundAt("death", t));
                Assert.That(sheets.GroundAt("nope", 0f), Is.EqualTo(sheets.GroundAt("idle", 0f)), "unknown clips fall back to idle");
            }
        }

        [Test] public void TheHeroShadowSitsUnderItsFeetAndMirrorsWithTheSprite()
        {
            var (art, render, canvas) = Load();
            var sheets = art.Hero("warrior");
            var g = sheets.GroundAt("idle", 0f).Value;
            sheets.DrawShadow(canvas, art, "idle", 0f, new RectF(100, 50, 192, 192));
            var q = render.Shadows.Single();
            Assert.That((q.x0 + q.x1) / 2, Is.EqualTo(100 + g.x * 2).Within(1f), "under her feet, not the middle of the cell");
            Assert.That((q.y0 + q.y1) / 2, Is.EqualTo(50 + g.y * 2).Within(1f), "on the floor line");
            Assert.That(q.x1 - q.x0, Is.EqualTo((g.w + HeroSheets.ShadowMargin) * 2).Within(2f), "a little wider than her feet");
            Assert.That(render.Quads.First().blend, Is.EqualTo(Blend.Add), "over a faint floor light");
            render.Quads.Clear();
            sheets.DrawShadow(canvas, art, "idle", 0f, new RectF(100, 50, 192, 192), flip: true);
            q = render.Shadows.Single();
            Assert.That((q.x0 + q.x1) / 2, Is.EqualTo(100 + (96 - g.x) * 2).Within(1f), "mirrored with a flipped sprite");
            render.Quads.Clear();
            sheets.DrawShadow(canvas, art, "idle", 0f, new RectF(0, 0, 96, 96));
            q = render.Shadows.Single();
            Assert.That(q.x1 - q.x0, Is.EqualTo(g.w + HeroSheets.ShadowMargin).Within(1f), "1 px art pixels at 96 px");
        }

        [Test] public void TheMenuDrawsEachHerosShadowUnderItsFeetBeforeTheSprite()
        {
            var (art, render, canvas) = Load();
            Looks.Hero(canvas, art, "La Guerrera", 192, 0f, 300, 670, shadow: true);
            var shadowAt = render.Quads.FindIndex(q0 => q0.tex != null && q0.tex.StartsWith("ground_shadow_"));
            var spriteAt = render.Quads.FindIndex(q0 => q0.tex != null && q0.tex.StartsWith("Heroes/warrior_192"));
            Assert.That(shadowAt, Is.GreaterThanOrEqualTo(0).And.LessThan(spriteAt), "the shadow is under the sprite");
            var q = render.Shadows.Single();
            var g = art.Hero("warrior").GroundAt("idle", 0f).Value;
            var spriteTop = 670 - 192;
            Assert.That((q.y0 + q.y1) / 2, Is.EqualTo(spriteTop + g.y * 2).Within(2f), "at her soles (the old ellipse floated 18 px above them)");
            Assert.That((q.x0 + q.x1) / 2, Is.EqualTo(300 - 96 + g.x * 2).Within(2f));
            render.Quads.Clear();
            Looks.Hero(canvas, art, "El Mago", 192, 0f, 1080, 670, flip: true, shadow: true);
            var g2 = art.Hero("mage").GroundAt("idle", 0f).Value;
            Assert.That((render.Shadows.Single().x0 + render.Shadows.Single().x1) / 2, Is.EqualTo(1080 - 96 + (96 - g2.x) * 2).Within(2f), "mirrored for the mage on the right");
            render.Quads.Clear();
            Looks.Hero(canvas, art, "La Pícara", 192, 0f, 150, 660);
            Assert.That(render.Shadows, Is.Empty, "only where it is asked for");
        }

        [Test] public void EnemiesStandOnAContactShadowThatFadesWithThem()
        {
            var (art, render, canvas) = Load();
            var sheet = art.SheetForEnemy("Gólem de Musgo");
            Assume.That(sheet, Is.Not.Null);
            var anim = new EnemyAnimator(sheet, 3, 0f);
            anim.Update(0.016f);
            anim.Draw(canvas, art, (900f, 290f));
            var order = render.Quads.Select(q0 => q0.tex ?? "").ToList();
            var shadowAt = order.FindIndex(k => k.StartsWith("ground_shadow_"));
            var glowAt = render.Quads.FindIndex(q0 => q0.blend == Blend.Add);
            var bodyAt = order.FindIndex(k => k.Contains("golem"));
            Assert.That(glowAt, Is.GreaterThanOrEqualTo(0).And.LessThan(shadowAt), "its coloured floor glow first, the dark spot on it");
            Assert.That(shadowAt, Is.LessThan(bodyAt), "and the creature over its shadow");
            var q = render.Shadows.Single();
            Assert.That((q.x0 + q.x1) / 2, Is.EqualTo(900).Within(1f));
            Assert.That((q.y0 + q.y1) / 2, Is.EqualTo(290).Within(1.5f), "centred on the line it stands on");
            Assert.That(q.color.A, Is.GreaterThan(200), "fully visible while the enemy is");
            Assert.That(render.Created[q.tex].px.Max(p => p.A), Is.EqualTo(GroundShadow.Tones.Max()), "a real, dark shadow (the old one peaked at alpha 36)");
        }
    }
}
