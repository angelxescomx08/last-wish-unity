using System;
using System.Collections.Generic;
using System.Linq;

namespace LastWish.Presentation
{
    /// <summary>fx/sprite_animation.SpriteAnimation over sprite frames.</summary>
    public sealed class FrameAnimation
    {
        private readonly float[] durations;
        public Sprite[] Frames { get; }
        public bool Loop { get; }
        public float Total { get; }
        public float Time { get; private set; }
        public FrameAnimation(Sprite[] frames, float[] durations, bool loop = true, float start = 0f)
        {
            if (frames == null || frames.Length == 0 || frames.Length != durations.Length) throw new ArgumentException("frames and durations must match");
            Frames = frames; this.durations = durations.Select(d => Math.Max(1e-6f, d)).ToArray(); Loop = loop; Total = this.durations.Sum();
            Time = loop ? Math.Max(0f, start) % Total : Math.Max(0f, start);
        }
        public void Update(float dt) { Time += Math.Max(0f, dt); if (Loop) Time %= Total; }
        public int Index
        {
            get
            {
                var t = Time;
                if (!Loop && t >= Total) return Frames.Length - 1;
                for (var i = 0; i < durations.Length; i++) { if (t < durations[i]) return i; t -= durations[i]; }
                return Frames.Length - 1;
            }
        }
        public Sprite Frame => Frames[Index];
        public bool Finished => !Loop && Time >= Total;
    }

    /// <summary>ui/dungeon_backdrop.DungeonBackdrop: baked room, flickering torches and pooled particles.</summary>
    public sealed class DungeonBackdrop
    {
        private sealed class Torch
        {
            public int X, Y; public float Phase, T; public FrameAnimation Flame; public int GlowLevel = 1;
            public void Update(float dt)
            {
                Flame.Update(dt); T += dt;
                var v = Math.Sin(T * 7.3 + Phase) * 0.5 + Math.Sin(T * 13.1 + Phase * 2) * 0.3 + Math.Sin(T * 2.1) * 0.2;
                GlowLevel = v < -0.35 ? 0 : v > 0.35 ? 2 : 1;
            }
        }

        private readonly Art art;
        private readonly List<Torch> torches = new List<Torch>();
        private readonly List<ParticleSystem> systems = new List<ParticleSystem>();
        private readonly Sprite room;
        private readonly Sprite[] glows = new Sprite[3];
        private readonly int scale;
        private readonly (int w, int h) flameSize;
        private readonly int glowSize;

        private static Rgba[] Rgb(object values) => Json.Arr(values).Select(v => { var a = Json.Ints(v); return new Rgba(a[0], a[1], a[2]); }).ToArray();

        public DungeonBackdrop(Art art, int seed = 7, float budget = 1f)
        {
            this.art = art;
            var meta = art.Dungeon; var roomMeta = Json.Obj(meta.Get("room")); var pal = Json.Obj(meta.Get("palette"));
            scale = Json.Int(meta.Get("scale"), 2);
            room = new Sprite(art.Texture("Dungeon/room_combat"));
            var fm = Json.Obj(meta.Get("flame")); var fs = Json.Ints(fm.Get("size")); var frames = Json.Int(fm.Get("frames"));
            flameSize = (fs[0] * scale, fs[1] * scale);
            var flameTex = art.Texture("Dungeon/flame");
            var flameFrames = Enumerable.Range(0, frames).Select(i => new Sprite(flameTex, i * fs[0], 0, fs[0], fs[1])).ToArray();
            var durations = Json.Nums(fm.Get("durations_ms")).Select(ms => (float)(ms / 1000)).ToArray();
            var gm = Json.Obj(meta.Get("glow")); var gsize = Json.Int(gm.Get("size")); glowSize = gsize * scale;
            var glowTex = art.Texture("Dungeon/glow");
            for (var i = 0; i < 3; i++) glows[i] = new Sprite(glowTex, i * gsize, 0, gsize, gsize);
            var torchPoints = Json.Arr(roomMeta.Get("torches")).Select(Json.Ints).ToList();
            for (var i = 0; i < torchPoints.Count; i++)
                torches.Add(new Torch { X = torchPoints[i][0], Y = torchPoints[i][1], Phase = i * 0.37f, T = i * 0.37f, Flame = new FrameAnimation(flameFrames, durations, true, i * 0.37f) });

            for (var i = 0; i < torchPoints.Count; i++)
            {
                var (x, y) = (torchPoints[i][0], torchPoints[i][1]);
                systems.Add(new ParticleSystem(new EmitterConfig { Rate = 7, Lifetime = (0.9f, 1.9f), Area = (x - 3, y - 20, 6, 4), Vx = (-6, 6), Vy = (-40, -22), Gravity = -6, Wobble = 9, Colors = Rgb(pal.Get("ember")), Capacity = 24 }, scale, 11 + i, budget: budget));
            }
            var wi = Json.Ints(roomMeta.Get("window_interior")); var sill = Json.Int(roomMeta.Get("sill_y"));
            var splash = new ParticleSystem(new EmitterConfig { Rate = 0, Lifetime = (0.12f, 0.26f), Vx = (-28, 22), Vy = (-55, -25), Gravity = 420, Colors = Rgb(pal.Get("splash")), Capacity = 60 }, scale, 21, budget: budget);
            var rain = Rgb(pal.Get("rain"));
            systems.Add(new ParticleSystem(new EmitterConfig { Rate = 95, Lifetime = (1.5f, 1.5f), Area = (wi[0] - 6, wi[1] - 12, wi[2] - wi[0] + 14, 4), Vx = (-60, -48), Vy = (260, 320),
                Colors = new[] { rain[0] }, Length = 2, Trail = 4, TrailColor = rain[2], FloorY = sill - 1, BurstOnFloor = 2, Capacity = 110 },
                scale, 22, (wi[0], wi[1], wi[2] - wi[0], wi[3] - wi[1]), splash, budget));
            var floor = Json.Int(roomMeta.Get("floor_y")) + 30;
            var drips = Json.Arr(roomMeta.Get("drips")).Select(Json.Ints).ToList();
            for (var i = 0; i < drips.Count; i++)
            {
                var dropSplash = new ParticleSystem(new EmitterConfig { Rate = 0, Lifetime = (0.15f, 0.3f), Vx = (-24, 24), Vy = (-45, -20), Gravity = 380, Colors = Rgb(pal.Get("splash")), Capacity = 12 }, scale, 31 + i, budget: budget);
                systems.Add(new ParticleSystem(new EmitterConfig { Rate = 0.45f, Lifetime = (3f, 3f), Area = (drips[i][0], drips[i][1], 1, 1), Gravity = 380, Colors = Rgb(pal.Get("drip")), Length = 2, FloorY = floor, BurstOnFloor = 3, Capacity = 4 },
                    scale, 41 + i, onFloor: dropSplash, budget: budget));
            }
            var beam = Json.Obj(roomMeta.Get("moonbeam")); var top = Json.Ints(beam.Get("top"));
            var dust = Rgb(pal.Get("dust"));
            systems.Add(new ParticleSystem(new EmitterConfig { Rate = 5, Lifetime = (3f, 6f), Area = (top[0] - 30, top[1] + 6, 80, 90), Vx = (2, 7), Vy = (-3, 3), Wobble = 4, Colors = new[] { dust[1], dust[0], dust[0], dust[1] }, Capacity = 30 }, scale, 51, budget: budget));
            foreach (var s in systems) s.Prewarm(3f);
        }

        public void Update(float dt)
        {
            foreach (var t in torches) t.Update(dt);
            foreach (var s in systems) s.Update(dt);
        }

        public void Draw(Canvas canvas)
        {
            canvas.Blit(room, 0, 0, scale);
            var half = glowSize / 2;
            foreach (var t in torches)
            {
                canvas.Blit(glows[t.GlowLevel], t.X * scale - half, (t.Y - 8) * scale - half, scale, Rgba.White, Blend.Add);
                canvas.Blit(t.Flame.Frame, t.X * scale - flameSize.w / 2, t.Y * scale - flameSize.h + scale, scale);
            }
            foreach (var s in systems) s.Draw(canvas);
        }
    }
}
