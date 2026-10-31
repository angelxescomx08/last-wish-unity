using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>Floor 1's combat backdrop, La Cripta, drawn by code in Tools/VfxLab/floor1_backdrop.html and exported by
    /// export_backdrop_art.py: the layers it loads, the order it draws them in, what moves and that it only depends on time.</summary>
    public sealed class CryptBackdropTests
    {
        private sealed class Recorder : IRenderBackend
        {
            public readonly List<(string tex, Blend blend, Rgba color, float x, float y, float u, float v)> Quads = new List<(string, Blend, Rgba, float, float, float, float)>();
            private int next = 1;
            public Tex WhiteTexture { get; } = new Tex(0, "white", 1, 1);
            public Tex LoadTexture(string key) { var (w, h) = Clips.PngSize(key); return new Tex(next++, key, w, h); }
            public Tex CreateTexture(string key, int width, int height, Rgba[] pixels, bool smooth = false) => new Tex(next++, key, width, height, smooth);
            public void BeginFrame() { }
            public void Quad(Tex tex, float u0, float v0, float u1, float v1, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, Rgba color, Blend blend)
                => Quads.Add((tex?.Key, blend, color, x0, y0, u0, v0));
            public void Triangle(float x0, float y0, float x1, float y1, float x2, float y2, Rgba color, Blend blend) { }
            public void TexTriangle(Tex tex, float x0, float y0, float u0, float v0, float x1, float y1, float u1, float v1, float x2, float y2, float u2, float v2, Rgba color, Blend blend) { }
            public Tex CreateTarget(string key, int width, int height) => new Tex(next++, key, width, height, false, true);
            public void ReleaseTarget(Tex target) { }
            public void BeginTarget(Tex target) { }
            public void EndTarget() { }
            public void SetClip(RectF? clip) { }
            public void EndFrame() { }
        }

        private static (Art art, Recorder render, Canvas canvas) Load()
        {
            var render = new Recorder(); var art = new Art(render, new FileAssets()); art.LoadCore();
            return (art, render, new Canvas(render));
        }

        private static List<(string tex, Blend blend, Rgba color, float x, float y, float u, float v)> Frame(CryptBackdrop b, Recorder render, Canvas canvas)
        {
            render.Quads.Clear(); b.Draw(canvas); return render.Quads.ToList();
        }

        [Test] public void FloorOneFightsInTheCryptAndTheOtherFloorsKeepTheDungeon()
        {
            var (art, _, _) = Load();
            Assert.That(Backdrops.ForFloor(art, 1), Is.InstanceOf<CryptBackdrop>());
            Assert.That(Backdrops.ForFloor(art, 2), Is.InstanceOf<DungeonBackdrop>());
            Assert.That(Backdrops.ForFloor(art, 3), Is.InstanceOf<DungeonBackdrop>());
        }

        [Test] public void TheLayersComeFromTheDrawingAndFitTheirTextures()
        {
            var meta = Json.Obj(Json.Parse(File.ReadAllText(Path.Combine(Clips.Root, CryptBackdrop.MetaKey + ".json"))));
            Assert.That(Json.Str(meta.Get("generator")), Is.EqualTo("Tools/VfxLab/floor1_backdrop.html"));
            Assert.That(Clips.PngSize("Dungeon/Floor1/room"), Is.EqualTo((640, 360)), "the room is the whole 640x360 native screen");
            Assert.That(Clips.PngSize("Dungeon/Floor1/halo"), Is.EqualTo((320, 180)), "the halo is half resolution, drawn x4");
            var (aw, ah) = Clips.PngSize("Dungeon/Floor1/atlas");
            var rects = new List<int[]>();
            foreach (var k in Json.Obj(meta.Get("flame_kinds")).Values) rects.AddRange(Json.Arr(Json.Obj(k).Get("frames")).Select(Json.Ints));
            foreach (var k in Json.Obj(meta.Get("glow_kinds")).Values) rects.AddRange(Json.Arr(Json.Obj(k).Get("levels")).Select(Json.Ints));
            rects.AddRange(Json.Arr(Json.Obj(meta.Get("bats")).Get("frames")).Select(Json.Ints));
            rects.AddRange(Json.Arr(Json.Obj(meta.Get("ripple")).Get("frames")).Select(Json.Ints));
            rects.Add(Json.Ints(Json.Obj(meta.Get("clouds")).Get("rect")));
            Assert.That(rects.Count, Is.GreaterThan(30));
            foreach (var r in rects) Assert.That(r[0] >= 0 && r[1] >= 0 && r[0] + r[2] <= aw && r[1] + r[3] <= ah, Is.True, string.Join(",", r) + " inside the atlas");
            foreach (var k in Json.Obj(meta.Get("glow_kinds")).Values) Assert.That(Json.Arr(Json.Obj(k).Get("levels")).Count, Is.EqualTo(3), "three flicker levels per glow");
        }

        [Test] public void ItDrawsSkyThenRoomThenLightThenFlamesAtTwiceNativeSize()
        {
            var (art, render, canvas) = Load();
            var b = new CryptBackdrop(art);
            var q = Frame(b, render, canvas);
            int First(string tex) => q.FindIndex(e => e.tex == tex);
            int sky = First("Dungeon/Floor1/sky"), room = First("Dungeon/Floor1/room"), halo = First("Dungeon/Floor1/halo");
            Assert.That(sky, Is.GreaterThanOrEqualTo(0)); Assert.That(room, Is.GreaterThan(sky), "the room (with its window hole) goes over the sky");
            Assert.That(halo, Is.EqualTo(room + 1), "the halo is added right over the room");
            Assert.That(q[halo].blend, Is.EqualTo(Blend.Add)); Assert.That(q[halo].color.R, Is.EqualTo(128), "halo gain 0.5");
            Assert.That((q[sky].x, q[sky].y), Is.EqualTo((268f * 2, 42f * 2)), "the sky sits behind the window at x2");
            var atlas = q.Where(e => e.tex == "Dungeon/Floor1/atlas").ToList();
            Assert.That(atlas.Count(e => e.blend == Blend.Alpha), Is.GreaterThanOrEqualTo(b.FlameCount + 2), "every flame, the clouds and the fog");
            Assert.That(atlas.Count(e => e.blend == Blend.Add), Is.GreaterThanOrEqualTo(7), "the candle glows and the runes add light");
            Assert.That(b.FlameCount, Is.EqualTo(14), "6 candelabra candles, 2 sconces, 6 candles on the sill");
            Assert.That(q.All(e => e.x % 2 == 0 && e.y % 2 == 0), Is.True, "everything lands on whole native pixels");
        }

        [Test] public void TheCryptMovesWithTimeAndOnlyWithTime()
        {
            var (art, render, canvas) = Load();
            var a = new CryptBackdrop(art); var b = new CryptBackdrop(art);
            Assert.That(a.ParticleCount, Is.GreaterThan(10), "the particles are prewarmed: embers, dust and souls are already there");
            for (var i = 0; i < 90; i++) { a.Update(1 / 30f); b.Update(1 / 30f); }
            var fa = Frame(a, render, canvas); var fb = Frame(b, render, canvas);
            Assert.That(fa, Is.EqualTo(fb), "same time, same picture");
            a.Update(0.35f);
            Assert.That(Frame(a, render, canvas), Is.Not.EqualTo(fb), "flames, glows, fog and particles move");
            a.Update(-1f);
            Assert.That(a.Time, Is.EqualTo(3.35f).Within(1e-3f), "time never goes back");
        }

        [Test] public void LightsFlickerThroughTheirThreeLevelsAndSkullEyesWakeUp()
        {
            var levels = Enumerable.Range(0, 400).Select(i => CryptBackdrop.GlowLevel(2, i * 0.05f)).ToList();
            Assert.That(levels.Distinct().OrderBy(v => v), Is.EqualTo(new[] { 0, 1, 2 }));
            var eyes = Enumerable.Range(0, 400).Select(i => CryptBackdrop.EyeLevel(1, i * 0.05f)).ToList();
            Assert.That(eyes.Distinct().OrderBy(v => v), Is.EqualTo(new[] { -1, 0, 1, 2 }), "dark, then glowing up and down again");
            Assert.That(eyes.Count(v => v < 0), Is.GreaterThan(100), "dark most of the time");
        }

        [Test] public void DripsFallIntoThePuddleAndOpenARipple()
        {
            var (art, _, _) = Load();
            var b = new CryptBackdrop(art);
            var seen = false;
            for (var i = 0; i < 30 * 6 && !seen; i++) { b.Update(1 / 30f); seen = b.RippleCount > 0; }
            Assert.That(seen, Is.True, "a drop lands in the puddle every two seconds");
            for (var i = 0; i < 30; i++) b.Update(1 / 30f);
            Assert.That(b.RippleCount, Is.EqualTo(0), "the ripple fades out in under a second");
        }
    }
}
