using System;

namespace LastWish.Presentation
{
    /// <summary>Shared procedural textures (fx.bursts.soft_glow and friends), built once.</summary>
    public static class Glows
    {
        public const int BaseRadius = 24;
        /// <summary>White soft light on black (quadratic falloff), drawn additively and tinted.</summary>
        public static Sprite Soft(Art art)
        {
            var tex = art.Procedural("glow_soft", BaseRadius * 2, BaseRadius * 2, () =>
            {
                var px = new Rgba[BaseRadius * 2 * BaseRadius * 2];
                for (var y = 0; y < BaseRadius * 2; y++)
                    for (var x = 0; x < BaseRadius * 2; x++)
                    {
                        var d = Math.Sqrt((x - BaseRadius) * (x - BaseRadius) + (y - BaseRadius) * (y - BaseRadius));
                        var r = (int)Math.Ceiling(d - 0.0001);
                        double v = 0;
                        if (r <= BaseRadius) { var k = Math.Pow(1.0 - Math.Max(1, r) / (double)BaseRadius, 2); v = 255 * k; }
                        var c = (int)v;
                        px[y * BaseRadius * 2 + x] = new Rgba(c, c, c, 255);
                    }
                return px;
            }, smooth: true);
            return new Sprite(tex);
        }

        /// <summary>Draw <c>soft_glow(color, radius)</c> centred at (cx, cy) with BLEND_RGB_ADD.</summary>
        public static void Draw(Canvas canvas, Art art, float cx, float cy, float radius, Rgba color)
        {
            radius = Math.Max(1, (int)radius);
            canvas.BlitRect(Soft(art), new RectF((float)Math.Floor(cx) - radius, (float)Math.Floor(cy) - radius, radius * 2, radius * 2), color, Blend.Add);
        }
        /// <summary>Same but with the top-left at (x, y) like blit(g, (x, y)).</summary>
        public static void DrawAt(Canvas canvas, Art art, float x, float y, float radius, Rgba color)
        {
            radius = Math.Max(1, (int)radius);
            canvas.BlitRect(Soft(art), new RectF(x, y, radius * 2, radius * 2), color, Blend.Add);
        }
        public static Rgba Scaled(Rgba c, double k) { k = Math.Max(0, k); return new Rgba(Math.Min(255, (int)(c.R * k)), Math.Min(255, (int)(c.G * k)), Math.Min(255, (int)(c.B * k))); }
    }

    public enum ParticleStyle { Square, Spark, Glow }

    /// <summary>fx/bursts.BurstParticles: fixed pool of one-shot particles in screen px.</summary>
    public sealed class BurstParticles
    {
        private const float MaxDt = 0.1f;
        private const int GlowLevels = 6;
        private readonly Rng rng;
        private readonly float[] x, y, vx, vy, age, life, size, drag, grav;
        private readonly ParticleStyle[] style;
        private readonly Rgba[][] pal;
        public int Capacity { get; }
        public int Count { get; private set; }

        public BurstParticles(int capacity = 600, int seed = 0)
        {
            Capacity = Math.Max(0, capacity); rng = new Rng(seed);
            x = new float[Capacity]; y = new float[Capacity]; vx = new float[Capacity]; vy = new float[Capacity];
            age = new float[Capacity]; life = new float[Capacity]; size = new float[Capacity]; drag = new float[Capacity]; grav = new float[Capacity];
            style = new ParticleStyle[Capacity]; pal = new Rgba[Capacity][];
        }

        public Rng Random => rng;
        public void Clear() => Count = 0;

        public bool Emit(float px, float py, float pvx, float pvy, float plife, Rgba[] palette, float psize = 3f, ParticleStyle pstyle = ParticleStyle.Square, float pdrag = 0f, float gravity = 0f)
        {
            if (Count >= Capacity || palette == null || palette.Length == 0) return false;
            var i = Count;
            x[i] = px; y[i] = py; vx[i] = pvx; vy[i] = pvy; age[i] = 0; life[i] = Math.Max(1e-3f, plife);
            size[i] = Math.Max(0.5f, psize); drag[i] = Math.Max(0f, pdrag); grav[i] = gravity; style[i] = pstyle; pal[i] = palette;
            Count++; return true;
        }

        public int Burst(float px, float py, int n, Rgba[] palette, (float, float)? speed = null, (float, float)? angle = null,
            (float, float)? lifeRange = null, (float, float)? sizeRange = null, ParticleStyle pstyle = ParticleStyle.Square,
            float pdrag = 2f, float gravity = 0f, float spread = 0f)
        {
            var sp = speed ?? (60f, 240f); var an = angle ?? (0f, (float)MathX.Tau); var li = lifeRange ?? (0.4f, 0.9f); var si = sizeRange ?? (2f, 4f);
            var made = 0;
            for (var k = 0; k < Math.Max(0, n); k++)
            {
                var a = rng.UniformF(an.Item1, an.Item2); var s = rng.UniformF(sp.Item1, sp.Item2);
                var ox = spread != 0 ? rng.UniformF(-spread, spread) : 0f; var oy = spread != 0 ? rng.UniformF(-spread, spread) : 0f;
                if (!Emit(px + ox, py + oy, (float)Math.Cos(a) * s, (float)Math.Sin(a) * s, rng.UniformF(li.Item1, li.Item2), palette, rng.UniformF(si.Item1, si.Item2), pstyle, pdrag, gravity)) break;
                made++;
            }
            return made;
        }

        public int Implode(float px, float py, int n, Rgba[] palette, (float, float)? radius = null, (float, float)? lifeRange = null, (float, float)? sizeRange = null, ParticleStyle pstyle = ParticleStyle.Spark)
        {
            var ra = radius ?? (90f, 180f); var li = lifeRange ?? (0.3f, 0.55f); var si = sizeRange ?? (1.5f, 3f);
            var made = 0;
            for (var k = 0; k < Math.Max(0, n); k++)
            {
                var a = rng.UniformF(0, (float)MathX.Tau); var d = rng.UniformF(ra.Item1, ra.Item2); var t = rng.UniformF(li.Item1, li.Item2);
                if (!Emit(px + (float)Math.Cos(a) * d, py + (float)Math.Sin(a) * d, -(float)Math.Cos(a) * d / t, -(float)Math.Sin(a) * d / t, t, palette, rng.UniformF(si.Item1, si.Item2), pstyle)) break;
                made++;
            }
            return made;
        }

        private void Kill(int i)
        {
            var last = Count - 1;
            if (i != last)
            {
                x[i] = x[last]; y[i] = y[last]; vx[i] = vx[last]; vy[i] = vy[last]; age[i] = age[last]; life[i] = life[last];
                size[i] = size[last]; drag[i] = drag[last]; grav[i] = grav[last]; style[i] = style[last]; pal[i] = pal[last];
            }
            Count = last;
        }

        public void Update(float dt)
        {
            dt = Math.Min(MaxDt, Math.Max(0f, dt));
            if (dt == 0f) return;
            var i = 0;
            while (i < Count)
            {
                age[i] += dt;
                if (age[i] >= life[i]) { Kill(i); continue; }
                if (drag[i] != 0) { var k = Math.Max(0f, 1f - drag[i] * dt); vx[i] *= k; vy[i] *= k; }
                vy[i] += grav[i] * dt; x[i] += vx[i] * dt; y[i] += vy[i] * dt;
                i++;
            }
        }

        public void Draw(Canvas canvas, Art art, float ox = 0, float oy = 0)
        {
            for (var i = 0; i < Count; i++)
            {
                var t = age[i] / life[i]; var p = pal[i]; var n = p.Length;
                var k = (int)(t * n); var col = p[k < n ? k : n - 1];
                var px = x[i] + ox; var py = y[i] + oy;
                switch (style[i])
                {
                    case ParticleStyle.Glow:
                        var level = Math.Max(1, (int)((1f - t) * GlowLevels + 0.999f));
                        var r = Math.Max(2, (int)size[i]);
                        Glows.DrawAt(canvas, art, (int)px - r, (int)py - r, r, Glows.Scaled(col, level / (double)GlowLevels));
                        break;
                    case ParticleStyle.Spark:
                        var w = Math.Max(1, (int)(size[i] * (1f - 0.6f * t)));
                        canvas.Line(px, py, px - vx[i] * 0.035f, py - vy[i] * 0.035f, col, w);
                        break;
                    default:
                        var s = Math.Max(1, (int)(size[i] * (1f - 0.7f * t) + 0.5f));
                        canvas.Fill(new RectF((int)px - s / 2, (int)py - s / 2, s, s), col);
                        break;
                }
            }
        }
    }

    /// <summary>fx/particles.EmitterConfig.</summary>
    public sealed class EmitterConfig
    {
        public float Rate; public (float, float) Lifetime; public (float x, float y, float w, float h) Area;
        public (float, float) Vx = (0, 0), Vy = (0, 0); public float Gravity, Wobble;
        public Rgba[] Colors = { Rgba.White }; public int Size = 1, Length = 1, Trail; public Rgba? TrailColor;
        public float? FloorY; public int BurstOnFloor; public int Capacity = 200;
    }

    /// <summary>fx/particles.ParticleSystem: pooled particles in native art px drawn ×scale.</summary>
    public sealed class ParticleSystem
    {
        private const float MaxDt = 0.1f;
        public EmitterConfig Config { get; }
        public int Scale { get; }
        public (float x, float y, float w, float h)? Clip { get; }
        public ParticleSystem OnFloor { get; }
        public int Capacity { get; }
        public float Rate { get; }
        private readonly Rng rng;
        private float accum, time;
        private readonly float[] x, y, vx, vy, age, life, phase;
        public int Count { get; private set; }

        public ParticleSystem(EmitterConfig config, int scale = 2, int seed = 0, (float, float, float, float)? clip = null, ParticleSystem onFloor = null, float budget = 1f)
        {
            Config = config; Scale = Math.Max(1, scale); Clip = clip; OnFloor = onFloor;
            Capacity = Math.Max(0, (int)(config.Capacity * Math.Max(0f, budget))); Rate = config.Rate * Math.Max(0f, budget);
            rng = new Rng(seed);
            x = new float[Capacity]; y = new float[Capacity]; vx = new float[Capacity]; vy = new float[Capacity];
            age = new float[Capacity]; life = new float[Capacity]; phase = new float[Capacity];
        }

        public int EmitAt(float px, float py, int n) { var made = 0; for (var i = 0; i < n; i++) { if (!Spawn(px, py)) break; made++; } return made; }

        private bool Spawn(float? px = null, float? py = null)
        {
            if (Count >= Capacity) return false;
            var c = Config; var i = Count;
            x[i] = px ?? c.Area.x + (float)rng.Random() * c.Area.w;
            y[i] = py ?? c.Area.y + (float)rng.Random() * c.Area.h;
            vx[i] = rng.UniformF(c.Vx.Item1, c.Vx.Item2); vy[i] = rng.UniformF(c.Vy.Item1, c.Vy.Item2);
            age[i] = 0; life[i] = rng.UniformF(c.Lifetime.Item1, c.Lifetime.Item2); phase[i] = (float)(rng.Random() * MathX.Tau);
            Count++; return true;
        }

        private void Kill(int i)
        {
            var last = Count - 1;
            if (i != last) { x[i] = x[last]; y[i] = y[last]; vx[i] = vx[last]; vy[i] = vy[last]; age[i] = age[last]; life[i] = life[last]; phase[i] = phase[last]; }
            Count = last;
        }

        public void Update(float dt)
        {
            dt = Math.Min(MaxDt, Math.Max(0f, dt));
            if (dt == 0f) return;
            var c = Config; time += dt;
            if (Rate > 0)
            {
                accum += Rate * dt;
                while (accum >= 1f) { accum -= 1f; if (!Spawn()) { accum = 0; break; } }
            }
            var i = 0;
            while (i < Count)
            {
                age[i] += dt;
                if (age[i] >= life[i]) { Kill(i); continue; }
                vy[i] += c.Gravity * dt;
                x[i] += vx[i] * dt + (c.Wobble != 0 ? c.Wobble * (float)Math.Sin(phase[i] + time * 5f) * dt : 0f);
                y[i] += vy[i] * dt;
                if (c.FloorY.HasValue && y[i] >= c.FloorY.Value)
                {
                    if (OnFloor != null && c.BurstOnFloor > 0) OnFloor.EmitAt(x[i], c.FloorY.Value, c.BurstOnFloor);
                    Kill(i); continue;
                }
                i++;
            }
            OnFloor?.Update(dt);
        }

        public void Prewarm(float seconds, float step = 1 / 30f) { for (var t = 0f; t < seconds; t += step) Update(step); }

        public void Draw(Canvas canvas, float ox = 0, float oy = 0)
        {
            var c = Config; var s = Scale; var colors = c.Colors; var nc = colors.Length;
            float w = c.Size * s, h = Math.Max(c.Size, c.Length) * s;
            if (Clip.HasValue) canvas.PushClip(new RectF(ox + Clip.Value.x * s, oy + Clip.Value.y * s, Clip.Value.w * s, Clip.Value.h * s));
            var tcol = c.TrailColor ?? colors[nc - 1];
            for (var i = 0; i < Count; i++)
            {
                var k = (int)(age[i] / life[i] * nc);
                var px = (int)x[i]; var py = (int)y[i];
                if (c.Trail > 0)
                {
                    var slope = vy[i] != 0 ? vx[i] / vy[i] : 0f;
                    for (var j = 1; j <= c.Trail; j++) canvas.Fill(new RectF(ox + (int)(x[i] - slope * j) * s, oy + (py - j) * s, w, s), tcol);
                }
                canvas.Fill(new RectF(ox + px * s, oy + py * s, w, h), colors[k < nc ? k : nc - 1]);
            }
            if (Clip.HasValue) canvas.PopClip();
            OnFloor?.Draw(canvas, ox, oy);
        }
    }
}
