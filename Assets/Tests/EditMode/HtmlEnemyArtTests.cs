using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastWish.Presentation;
using NUnit.Framework;
namespace LastWish.Tests
{
    /// <summary>Enemies redrawn with the HTML method (docs/art-style.md section 3.1): one page per enemy in
    /// Tools/VfxLab/enemies/&lt;id&gt;.html, one complete figure per frame, exported by export_enemy_art.py through Node at
    /// 12 fps. For each one: the sheet the game picks, its cells and anchor, every clip the game expects with its timing,
    /// the strike events, the move map, every frame and halo cell inside its sheet, the additive halo, and that every action
    /// plays and the death finishes, headless. The Espectro has its own WraithArtTests.</summary>
    public sealed class HtmlEnemyArtTests
    {
        public sealed class Spec
        {
            public string Name, Id;
            public int CellW, CellH, AnchorX, AnchorY;
            public (string name, int frames, bool loop)[] Clips;
            public Dictionary<string, int[]> Strikes;
            public Dictionary<string, string> Moves;
            public string[] Terminal = new string[0];
            public override string ToString() => Id;
        }

        public static IEnumerable<Spec> Specs()
        {
            yield return new Spec
            {
                Name = "Babosa Ácida", Id = "slime", CellW = 128, CellH = 96, AnchorX = 78, AnchorY = 93,
                Clips = new[] { ("idle", 24, true), ("attack", 11, false), ("spit", 10, false), ("cast", 12, false), ("hurt", 7, false), ("death", 17, false) },
                Strikes = new Dictionary<string, int[]> { ["attack"] = new[] { 5 }, ["spit"] = new[] { 5 } },
                Moves = new Dictionary<string, string> { ["slam"] = "attack", ["spit"] = "spit", ["corrode"] = "cast", ["vengeance"] = "cast" },
            };
            yield return new Spec
            {
                Name = "Gusano de Tumba", Id = "worm", CellW = 128, CellH = 96, AnchorX = 80, AnchorY = 93,
                Clips = new[] { ("idle", 24, true), ("attack", 10, false), ("burrow", 12, false), ("cast", 12, false), ("hurt", 7, false), ("death", 17, false) },
                Strikes = new Dictionary<string, int[]> { ["attack"] = new[] { 4 } },
                Moves = new Dictionary<string, string> { ["bite"] = "attack", ["burrow"] = "burrow", ["bile"] = "cast", ["vengeance"] = "cast" },
            };
            yield return new Spec
            {
                Name = "Ojo Vigilante", Id = "eye", CellW = 144, CellH = 96, AnchorX = 102, AnchorY = 93,
                Clips = new[] { ("idle", 24, true), ("attack", 10, false), ("cast", 12, false), ("hurt", 7, false), ("death", 17, false) },
                Strikes = new Dictionary<string, int[]> { ["attack"] = new[] { 4 } },
                Moves = new Dictionary<string, string> { ["beam"] = "attack", ["stare"] = "cast", ["dread"] = "cast", ["vengeance"] = "cast" },
            };
            yield return new Spec
            {
                Name = "Cráneo Ígneo", Id = "skull", CellW = 128, CellH = 96, AnchorX = 86, AnchorY = 93,
                Clips = new[] { ("idle", 24, true), ("attack", 10, false), ("cast", 12, false), ("hurt", 7, false), ("death", 17, false) },
                Strikes = new Dictionary<string, int[]> { ["attack"] = new[] { 3 } },
                Moves = new Dictionary<string, string> { ["flame"] = "attack", ["stoke"] = "cast", ["vengeance"] = "cast" },
            };
        }

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

        [TestCaseSource(nameof(Specs))]
        public void TheEnemyUsesTheSheetDrawnByCode(Spec e)
        {
            var sheet = Load().art.SheetForEnemy(e.Name);
            Assert.That(sheet.Id, Is.EqualTo(e.Id));
            Assert.That(sheet.Meta["generator"] as string, Is.EqualTo($"Tools/VfxLab/enemies/{e.Id}.html"));
            Assert.That((sheet.CellW, sheet.CellH, sheet.Scale), Is.EqualTo((e.CellW, e.CellH, 2)), "native cells, drawn x2");
            Assert.That(sheet.Anchor, Is.EqualTo((e.AnchorX * 2, e.AnchorY * 2)), "the anchor is the shadow line under the figure");
            Assert.That(sheet.Top, Is.InRange(2, e.CellH * 2 - 40), "name and intent go above the figure");
            Assert.That(sheet.IdleBox.w > 20 && sheet.IdleBox.h > 20, Is.True, "the portrait holds the whole figure");
            Assert.That(sheet.HasHalo, Is.True, "drawn by code: it has its halo");
        }

        [TestCaseSource(nameof(Specs))]
        public void EveryClipTheGameExpectsHasItsFramesTimingAndLoop(Spec e)
        {
            var sheet = Load().art.EnemySheetById(e.Id);
            Assert.That(sheet.Animations.Keys, Is.EquivalentTo(e.Clips.Select(c => c.name)));
            foreach (var (name, frames, loop) in e.Clips)
            {
                var a = sheet.Animations[name];
                Assert.That((a.Durations.Length, a.Loop), Is.EqualTo((frames, loop)), name);
                Assert.That(a.Durations.All(d => System.Math.Abs(d - 1f / 12f) < 0.001f), Is.True, name + ": 12 fps frames (83/84 ms)");
            }
            Assert.That(sheet.Seconds("idle"), Is.EqualTo(2f).Within(1e-4f), "the idle loop lasts 2 s, like the heroes");
            var death = e.Clips.Single(c => c.name == "death").frames;
            Assert.That(sheet.Animations["death"].FrameAt(1e9f), Is.EqualTo(death - 1), "death is held on its last (empty) frame");
            Assert.That(sheet.Terminal, Is.EquivalentTo(e.Terminal));
        }

        [TestCaseSource(nameof(Specs))]
        public void TheStrikesAndTheMovesAreTheOnesTheGameExpects(Spec e)
        {
            var sheet = Load().art.EnemySheetById(e.Id);
            foreach (var pair in e.Strikes)
            {
                var seconds = sheet.StrikeSeconds(pair.Key);
                Assert.That(seconds.Length, Is.EqualTo(pair.Value.Length), pair.Key + ": one event per hit");
                for (var i = 0; i < seconds.Length; i++)
                    Assert.That(seconds[i], Is.EqualTo(sheet.Animations[pair.Key].SecondsUntil(pair.Value[i])).Within(1e-4f), $"{pair.Key} hit {i}");
                Assert.That(seconds.All(s => s < sheet.Seconds(pair.Key)), Is.True, pair.Key + ": the hit lands before the clip ends");
            }
            foreach (var pair in e.Moves)
                Assert.That(sheet.AnimationForMove(pair.Key, "none"), Is.EqualTo(pair.Value), "move " + pair.Key);
            Assert.That(EnemyAnimator.StyleFor(e.Id).Claw.x, Is.LessThan(-40), "the strike sparks burst in front, towards the hero");
        }

        [TestCaseSource(nameof(Specs))]
        public void EveryFrameAndHaloCellIsInsideItsSheet(Spec e)
        {
            var sheet = Load().art.EnemySheetById(e.Id);
            foreach (var key in new[] { $"Enemies/{e.Id}_sheet", $"Enemies/{e.Id}_halo" })
                Assert.That(File.Exists(Path.Combine(Clips.Root, key + ".png")), Is.True, key);
            Assert.That(sheet.HaloGain, Is.InRange(0.1f, 1f));
            foreach (var (name, frames, _) in e.Clips)
                for (var i = 0; i < frames; i++)
                {
                    var t = sheet.Animations[name].SecondsUntil(i) + 0.01f;
                    var s = sheet.Frame(name, t);
                    var h = sheet.Halo(name, t);
                    Assert.That(s.X + s.W <= s.Texture.Width && s.Y + s.H <= s.Texture.Height, Is.True, $"{name} {i}");
                    Assert.That(h.X + h.W <= h.Texture.Width && h.Y + h.H <= h.Texture.Height, Is.True, $"halo {name} {i}");
                    Assert.That((h.X, h.Y, h.W, h.H), Is.EqualTo((s.X, s.Y, s.W, s.H)), "the halo cell matches the sprite cell");
                }
        }

        [TestCaseSource(nameof(Specs))]
        public void TheAnimatorAddsTheHaloOverTheSprite(Spec e)
        {
            var (art, render) = Load();
            var canvas = new Canvas(render);
            var anim = new EnemyAnimator(art.EnemySheetById(e.Id), 5);
            anim.Draw(canvas, art, (400f, 300f));
            var halo = render.Quads.Where(q => q.tex == $"Enemies/{e.Id}_halo").ToList();
            Assert.That(halo.Count, Is.EqualTo(1));
            Assert.That(halo[0].blend, Is.EqualTo(Blend.Add));
            var sprite = render.Quads.FindIndex(q => q.tex == $"Enemies/{e.Id}_sheet");
            Assert.That(sprite, Is.GreaterThanOrEqualTo(0).And.LessThan(render.Quads.FindIndex(q => q.tex == $"Enemies/{e.Id}_halo")), "light goes over the sprite");
        }

        [TestCaseSource(nameof(Specs))]
        public void EveryActionPlaysAndTheDeathFinishes(Spec e)
        {
            var anim = new EnemyAnimator(Load().art.EnemySheetById(e.Id), 9);
            foreach (var (name, _, loop) in e.Clips)
            {
                if (loop || name == "death" || e.Terminal.Contains(name)) continue;
                anim.Play(name);
                Assert.That(anim.Action, Is.EqualTo(name));
                for (var k = 0; k < 60; k++) anim.Update(0.05f);
                Assert.That((anim.Action, anim.Busy), Is.EqualTo(((string)null, false)), name + " returns to the idle");
            }
            anim.Play("death");
            for (var k = 0; k < 60; k++) anim.Update(0.05f);
            Assert.That((anim.Dead, anim.DeathDone), Is.EqualTo((true, true)));
        }
    }
}
