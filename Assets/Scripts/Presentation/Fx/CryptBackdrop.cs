using System;
using System.Collections.Generic;
using System.Linq;

namespace LastWish.Presentation
{
    /// <summary>What a scene updates and draws behind everything else.</summary>
    public interface IBackdrop
    {
        void Update(float dt);
        void Draw(Canvas canvas);
    }

    /// <summary>The combat backdrop of each floor: floor 1 is La Cripta; the other floors keep the dungeon room.</summary>
    public static class Backdrops
    {
        public static IBackdrop ForFloor(Art art, int floor) => floor <= 1 ? new CryptBackdrop(art) : (IBackdrop)new DungeonBackdrop(art);
    }

    /// <summary>
    /// Floor 1, La Cripta: drawn by code in Tools/VfxLab/floor1_backdrop.html and exported by export_backdrop_art.py
    /// (docs/art-style.md). Back to front: the sky through the barred window with twinkling stars, drifting clouds and a flock
    /// of bats; the lit room with the window hole; the emissive halo added on top; the candle glows (3 flicker levels), the
    /// soul eyes in the ossuary and the slab runes breathing; the candle flames; the puddle ripple; two fog bands; pooled
    /// particles (embers, moon dust, souls, grit, drips that splash). Native art px drawn ×scale; everything depends only on
    /// the elapsed time and fixed seeds, like the page that previews it.
    /// </summary>
    public sealed class CryptBackdrop : IBackdrop
    {
        public const string MetaKey = "Dungeon/Floor1/backdrop";

        private sealed class FlameKind { public Sprite[] Frames; public float[] Durations; public float Total; }
        private readonly struct Placed { public readonly int X, Y; public readonly float Phase; public readonly string Kind; public Placed(string kind, int x, int y, float phase) { Kind = kind; X = x; Y = y; Phase = phase; } }
        private sealed class Ripple { public float X, Y, Start; }

        private readonly int scale, haloScale, cloudY;
        private readonly float haloGain, cloudSpeed;
        private readonly Sprite room, sky, halo, clouds;
        private readonly (int x, int y) skyAt;
        private readonly int[] cloudClip, batClip;
        private readonly List<(Sprite strip, int y, float speed)> fog = new List<(Sprite, int, float)>();
        private readonly Dictionary<string, FlameKind> flameKinds = new Dictionary<string, FlameKind>();
        private readonly List<Placed> flames = new List<Placed>(), glows = new List<Placed>();
        private readonly Dictionary<string, Sprite[]> glowKinds = new Dictionary<string, Sprite[]>();
        private readonly List<(int x, int y)> eyes = new List<(int, int)>(), stars = new List<(int, int)>();
        private readonly (int x, int y) runesAt; private readonly float runesPeriod;
        private readonly Sprite[] bats; private readonly float batFps, batPeriod, batDuration, batBob;
        private readonly (float x, float y) batFrom, batTo;
        private readonly List<(float dx, float dy, float delay)> flock = new List<(float, float, float)>();
        private readonly Sprite[] rippleFrames; private readonly float[] rippleDurations; private readonly float rippleTotal;
        private readonly List<Ripple> ripples = new List<Ripple>();
        private readonly List<ParticleSystem> systems = new List<ParticleSystem>();

        public float Time { get; private set; }
        public int FlameCount => flames.Count;
        public int ParticleCount => systems.Sum(s => s.Count + (s.OnFloor?.Count ?? 0));
        public int RippleCount => ripples.Count;

        private static Sprite Cut(Tex tex, object rect) { var r = Json.Ints(rect); return new Sprite(tex, r[0], r[1], r[2], r[3]); }
        private static float F(object o, double fallback = 0) => (float)Json.Num(o, fallback);
        private static (float, float) Pair(object o, (float, float) fallback) { var a = Json.Nums(o); return a == null ? fallback : ((float)a[0], (float)a[1]); }

        public CryptBackdrop(Art art, float budget = 1f)
        {
            var meta = Json.Obj(Json.Parse(art.ReadText(MetaKey)));
            scale = Json.Int(meta.Get("scale"), 2);
            room = new Sprite(art.Texture(Json.Str(meta.Get("room"))));
            var skyMeta = Json.Obj(meta.Get("sky")); var at = Json.Ints(skyMeta.Get("at"));
            sky = new Sprite(art.Texture(Json.Str(skyMeta.Get("image")))); skyAt = (at[0], at[1]);
            var haloMeta = Json.Obj(meta.Get("halo"));
            halo = new Sprite(art.Texture(Json.Str(haloMeta.Get("image")))); haloScale = Json.Int(haloMeta.Get("scale"), 4); haloGain = F(haloMeta.Get("gain"), 0.5);
            var atlas = art.Texture(Json.Str(meta.Get("atlas")));

            var cm = Json.Obj(meta.Get("clouds"));
            clouds = Cut(atlas, cm.Get("rect")); cloudY = Json.Int(cm.Get("y")); cloudSpeed = F(cm.Get("speed")); cloudClip = Json.Ints(cm.Get("clip"));
            foreach (var o in Json.Arr(meta.Get("fog"))) { var f = Json.Obj(o); fog.Add((Cut(atlas, f.Get("rect")), Json.Int(f.Get("y")), F(f.Get("speed")))); }

            foreach (var pair in Json.Obj(meta.Get("flame_kinds")))
            {
                var k = Json.Obj(pair.Value);
                var durations = Json.Nums(k.Get("durations_ms")).Select(ms => (float)(ms / 1000)).ToArray();
                flameKinds[pair.Key] = new FlameKind { Frames = Json.Arr(k.Get("frames")).Select(r => Cut(atlas, r)).ToArray(), Durations = durations, Total = durations.Sum() };
            }
            foreach (var o in Json.Arr(meta.Get("flames"))) { var f = Json.Obj(o); flames.Add(new Placed(Json.Str(f.Get("kind")), Json.Int(f.Get("x")), Json.Int(f.Get("y")), F(f.Get("phase")))); }
            foreach (var pair in Json.Obj(meta.Get("glow_kinds"))) glowKinds[pair.Key] = Json.Arr(Json.Obj(pair.Value).Get("levels")).Select(r => Cut(atlas, r)).ToArray();
            foreach (var o in Json.Arr(meta.Get("glows"))) { var g = Json.Obj(o); glows.Add(new Placed(Json.Str(g.Get("kind")), Json.Int(g.Get("x")), Json.Int(g.Get("y")), 0f)); }
            foreach (var o in Json.Arr(meta.Get("eyes"))) { var e = Json.Ints(o); eyes.Add((e[0], e[1])); }
            foreach (var o in Json.Arr(meta.Get("stars"))) { var s = Json.Ints(o); stars.Add((s[0], s[1])); }
            var runes = Json.Obj(meta.Get("runes")); runesAt = (Json.Int(runes.Get("x")), Json.Int(runes.Get("y"))); runesPeriod = F(runes.Get("period"), 3.2);

            var bm = Json.Obj(meta.Get("bats"));
            bats = Json.Arr(bm.Get("frames")).Select(r => Cut(atlas, r)).ToArray();
            batFps = F(bm.Get("fps"), 12); batPeriod = F(bm.Get("period"), 16); batDuration = F(bm.Get("duration"), 4.6); batBob = F(bm.Get("bob"), 2);
            batFrom = Pair(bm.Get("from"), (0, 0)); batTo = Pair(bm.Get("to"), (0, 0)); batClip = Json.Ints(bm.Get("clip"));
            foreach (var o in Json.Arr(bm.Get("flock"))) { var a = Json.Nums(o); flock.Add(((float)a[0], (float)a[1], (float)a[2])); }
            var rm = Json.Obj(meta.Get("ripple"));
            rippleFrames = Json.Arr(rm.Get("frames")).Select(r => Cut(atlas, r)).ToArray();
            rippleDurations = Json.Nums(rm.Get("durations_ms")).Select(ms => (float)(ms / 1000)).ToArray(); rippleTotal = rippleDurations.Sum();

            var pal = Json.Obj(meta.Get("palette"));
            Rgba[] Colors(string name) => Json.Arr(pal.Get(name)).Select(v => { var c = Json.Ints(v); return new Rgba(c[0], c[1], c[2]); }).ToArray();
            var emitters = Json.Arr(meta.Get("emitters"));
            for (var k = 0; k < emitters.Count; k++)
            {
                var e = Json.Obj(emitters[k]); var area = Json.Nums(e.Get("area"));
                var floor = e.Get("floor_y");
                systems.Add(new ParticleSystem(new EmitterConfig
                {
                    Rate = F(e.Get("rate")), Lifetime = Pair(e.Get("life"), (1, 1)), Area = ((float)area[0], (float)area[1], (float)area[2], (float)area[3]),
                    Vx = Pair(e.Get("vx"), (0, 0)), Vy = Pair(e.Get("vy"), (0, 0)), Gravity = F(e.Get("gravity")), Wobble = F(e.Get("wobble")),
                    Colors = Colors(Json.Str(e.Get("colors"))), Capacity = Json.Int(e.Get("capacity"), 16), FloorY = floor == null ? (float?)null : F(floor),
                }, scale, 11 + k, budget: budget));
            }
            var drips = Json.Arr(meta.Get("drips"));
            for (var k = 0; k < drips.Count; k++)
            {
                var d = Json.Obj(drips[k]);
                var splash = new ParticleSystem(new EmitterConfig { Rate = 0, Lifetime = (0.15f, 0.3f), Vx = (-24, 24), Vy = (-45, -20), Gravity = 380, Colors = Colors("splash"), Capacity = 12 }, scale, 31 + k, budget: budget);
                var drip = new ParticleSystem(new EmitterConfig
                {
                    Rate = F(d.Get("rate")), Lifetime = (3f, 3f), Area = (F(d.Get("x")), F(d.Get("y")), 1, 1), Gravity = 380, Colors = Colors("drip"), Length = 2,
                    FloorY = F(d.Get("floor_y")), BurstOnFloor = 3, Capacity = 4,
                }, scale, 41 + k, onFloor: splash, budget: budget);
                var rp = Json.Ints(d.Get("ripple"));
                if (rp != null) drip.Landed = (x, y) => ripples.Add(new Ripple { X = rp[0], Y = rp[1], Start = Time });
                systems.Add(drip);
            }
            foreach (var s in systems) s.Prewarm(3f);
            ripples.Clear();
        }

        public void Update(float dt)
        {
            dt = Math.Max(0f, dt);
            Time += dt;
            foreach (var s in systems) s.Update(dt);
            ripples.RemoveAll(r => Time - r.Start >= rippleTotal);
        }

        private static float Mod(float a, float b) => ((a % b) + b) % b;
        private static int FrameAt(float[] durations, float total, float t)
        {
            var u = Mod(t, total);
            for (var i = 0; i < durations.Length; i++) { u -= durations[i]; if (u < 0) return i; }
            return durations.Length - 1;
        }
        /// <summary>Flicker level 0..2 of glow i at time t (three sines, like the dungeon torches).</summary>
        public static int GlowLevel(int i, float t)
        {
            var ph = i * 0.37;
            var v = Math.Sin(t * 7.3 + ph) * 0.5 + Math.Sin(t * 13.1 + ph * 2) * 0.3 + Math.Sin(t * 2.1 + ph) * 0.2;
            return v < -0.35 ? 0 : v > 0.35 ? 2 : 1;
        }
        /// <summary>Glow level of a skull's soul eyes (-1 = dark): each skull lights up for 60 % of its own period.</summary>
        public static int EyeLevel(int skull, float t)
        {
            var period = 3.4f + (skull % 3) * 0.9f; var u = Mod(t / period + skull * 0.31f, 1f);
            if (u > 0.6f) return -1;
            var env = Math.Pow(Math.Sin(Math.PI * u / 0.6), 1.5);
            return env > 0.75 ? 2 : env > 0.4 ? 1 : env > 0.12 ? 0 : -1;
        }

        private RectF Native(float x, float y, float w, float h) => new RectF(x * scale, y * scale, w * scale, h * scale);
        private void Put(Canvas canvas, Sprite s, float x, float y, Blend blend = Blend.Alpha) => canvas.Blit(s, (float)Math.Floor(x) * scale, (float)Math.Floor(y) * scale, scale, Rgba.White, blend);
        private void Dot(Canvas canvas, int x, int y, Rgba c) => canvas.Fill(Native(x, y, 1, 1), c);

        public void Draw(Canvas canvas)
        {
            var t = Time;
            // the sky behind the window: stars that twinkle, clouds over the moon, now and then a flock of bats
            canvas.Blit(sky, skyAt.x * scale, skyAt.y * scale, scale);
            for (var k = 0; k < stars.Count; k++)
            {
                var u = Mod(t / (2.7f + 0.9f * k) + 0.17f * k, 1f);
                if (u > 0.16f) continue;
                var (x, y) = stars[k]; var peak = u > 0.04f && u < 0.12f;
                Dot(canvas, x, y, peak ? Rgba.White : new Rgba(226, 220, 255));
                if (peak) { var arm = new Rgba(166, 155, 230); Dot(canvas, x - 1, y, arm); Dot(canvas, x + 1, y, arm); Dot(canvas, x, y - 1, arm); Dot(canvas, x, y + 1, arm); }
            }
            canvas.PushClip(Native(cloudClip[0], cloudClip[1], cloudClip[2], cloudClip[3]));
            var cw = clouds.W; var ox = (int)Math.Floor(Mod(t * cloudSpeed, cw));
            for (var x = cloudClip[0] + ox - cw; x < cloudClip[0] + cloudClip[2]; x += cw) Put(canvas, clouds, x, cloudY);
            canvas.PopClip();
            var tb = Mod(t, batPeriod);
            canvas.PushClip(Native(batClip[0], batClip[1], batClip[2], batClip[3]));
            foreach (var (dx, dy, delay) in flock)
            {
                var s = (tb - delay) / batDuration; if (s < 0 || s > 1) continue;
                var x = batFrom.x + (batTo.x - batFrom.x) * s + dx; var y = batFrom.y + (batTo.y - batFrom.y) * s + dy + batBob * (float)Math.Sin((t + delay) * 6.5);
                Put(canvas, bats[(int)Math.Floor((t + delay) * batFps) % bats.Length], x - 4, y - 3);
            }
            canvas.PopClip();

            // the room, its light (halo, candle glows, soul eyes, runes) and the flames
            canvas.Blit(room, 0, 0, scale);
            var gain = MathX.Round(255 * haloGain);
            canvas.Blit(halo, 0, 0, haloScale, new Rgba(gain, gain, gain), Blend.Add);
            for (var i = 0; i < glows.Count; i++)
            {
                var g = glows[i]; var s = glowKinds[g.Kind][GlowLevel(i, t)];
                Put(canvas, s, g.X - s.W / 2f, g.Y - s.H / 2f, Blend.Add);
            }
            var eyeGlow = glowKinds["eye"];
            for (var k = 0; k + 1 < eyes.Count; k += 2)
            {
                var level = EyeLevel(k / 2, t); if (level < 0) continue;
                for (var j = 0; j < 2; j++)
                {
                    var (x, y) = eyes[k + j];
                    Put(canvas, eyeGlow[level], x - 4, y - 4, Blend.Add);
                    if (level == 2) Dot(canvas, x, y, new Rgba(143, 241, 255));
                }
            }
            var ru = Mod(t / runesPeriod, 1f);
            Put(canvas, glowKinds["rune"][Math.Min(2, (int)Math.Floor((0.5 - 0.5 * Math.Cos(2 * Math.PI * ru)) * 3))], runesAt.x, runesAt.y, Blend.Add);
            foreach (var f in flames)
            {
                var kind = flameKinds[f.Kind];
                Put(canvas, kind.Frames[FrameAt(kind.Durations, kind.Total, t + f.Phase * kind.Total)], f.X, f.Y);
            }
            foreach (var r in ripples) Put(canvas, rippleFrames[FrameAt(rippleDurations, rippleTotal, Math.Min(Time - r.Start, rippleTotal - 1e-4f))], r.X, r.Y);
            // fog at floor level (behind the fighters, who are drawn after the backdrop) and the particles
            foreach (var (strip, y, speed) in fog)
            {
                var w = strip.W; var fx = (int)Math.Floor(Mod(t * speed, w));
                for (var x = fx - w; x < 640; x += w) Put(canvas, strip, x, y);
            }
            foreach (var s in systems) s.Draw(canvas);
        }
    }
}
