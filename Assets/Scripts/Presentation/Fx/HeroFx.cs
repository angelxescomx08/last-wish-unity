using System;
using System.Collections.Generic;

namespace LastWish.Presentation
{
    /// <summary>fx/hero_fx.py: timed particle cues and the floor shadow of the animated hero.</summary>
    public sealed class HeroFx
    {
        private const float MaxDt = 0.1f, DeathKneel = 0.18f;
        private static readonly Rgba[] Warm = { new Rgba(255, 255, 236), new Rgba(255, 214, 124), new Rgba(236, 136, 44), new Rgba(130, 54, 20) };
        private static readonly Rgba[] Ward = { new Rgba(236, 250, 255), new Rgba(150, 204, 255), new Rgba(70, 134, 224), new Rgba(30, 64, 130) };
        private static readonly Rgba[] Ember = { new Rgba(255, 236, 210), new Rgba(255, 140, 90), new Rgba(200, 50, 40), new Rgba(90, 20, 20) };
        private static readonly Rgba[] Dust = { new Rgba(150, 128, 110), new Rgba(110, 92, 80), new Rgba(70, 58, 54) };
        private static readonly Rgba[] Gold = { new Rgba(255, 250, 210), new Rgba(255, 214, 90), new Rgba(230, 150, 30), new Rgba(140, 80, 10) };
        private static readonly Rgba[] Arcane = { new Rgba(236, 255, 255), new Rgba(142, 240, 255), new Rgba(62, 158, 230), new Rgba(72, 65, 150) };
        private static readonly Rgba[] Violet = { new Rgba(246, 238, 255), new Rgba(194, 168, 255), new Rgba(130, 98, 230), new Rgba(66, 49, 126) };
        public static readonly (float x, float y) BladeHit = (82, 36), WardAt = (36, -14), Chest = (-4, -4), CastTip = (12, -92), Feet = (0, 94), FrontFoot = (24, 92);
        private const double Pi = Math.PI;

        public readonly float Strike, CastSeconds;
        public readonly (float x, float y) StrikePoint, CastPoint;
        private readonly Rgba[] attackPalette, castPalette;
        private readonly float guardTime, deathKneel, chargeTime;
        private readonly bool arcane;
        private readonly Rng rng;
        public readonly BurstParticles Particles;
        public string Action { get; private set; }
        private float t;
        private readonly HashSet<string> fired = new HashSet<string>();
        private readonly List<(string action, float delay)> queue = new List<(string, float)>();
        private (float x, float y)? center;

        public HeroFx(float strike, int seed = 0, int particles = 260, (float, float)? strikePoint = null, (float, float)? castPoint = null,
            float? castSeconds = null, bool arcane = false, float? guardSeconds = null, float? kneelSeconds = null)
        {
            Strike = Math.Max(0f, strike);
            StrikePoint = strikePoint ?? BladeHit; CastPoint = castPoint ?? CastTip;
            CastSeconds = castSeconds.HasValue ? Math.Max(0f, castSeconds.Value) : arcane ? 0.5f : 0.25f;
            attackPalette = arcane ? Arcane : Warm; castPalette = arcane ? Violet : Gold;
            guardTime = guardSeconds.HasValue ? Math.Max(0f, guardSeconds.Value) : arcane ? 0.28f : 0.06f;
            deathKneel = kneelSeconds.HasValue ? Math.Max(0f, kneelSeconds.Value) : arcane ? 0.84f : DeathKneel;
            chargeTime = Math.Max(0f, CastSeconds - 0.2f);
            this.arcane = arcane;
            rng = new Rng(seed);
            Particles = new BurstParticles(particles, seed + 13);
        }

        public void Play(string action, float delay = 0f)
        {
            if (action != "attack" && action != "guard" && action != "hurt" && action != "cast" && action != "death") return;
            if (delay > 0) { queue.Add((action, delay)); return; }
            Action = action; t = 0f; fired.Clear();
        }

        public void Update(float dt)
        {
            dt = Math.Min(MaxDt, Math.Max(0f, dt));
            for (var i = 0; i < queue.Count; i++)
            {
                var (a, d) = queue[i]; d -= dt;
                if (d <= 0) { queue.RemoveAt(i); i--; Play(a); }
                else queue[i] = (a, d);
            }
            if (Action != null)
            {
                t += dt; Cues();
                if (t > 2.0f) Action = null;
            }
            Particles.Update(dt);
        }

        private bool Once(string key, float at)
        {
            if (fired.Contains(key) || t < at) return false;
            fired.Add(key); return true;
        }

        private (float x, float y) At((float x, float y) off) => (center.Value.x + off.x, center.Value.y + off.y);

        private void Cues()
        {
            if (!center.HasValue) return;
            var p = Particles; var r = rng; var a = Action;
            if (a == "attack")
            {
                if (Once("kick", 0.12f))
                {
                    var (x, y) = At(FrontFoot);
                    p.Burst(x, y, 10, Dust, (30f, 110f), ((float)(Pi * 1.05), (float)(Pi * 1.6)), (0.3f, 0.6f), (2f, 4f), ParticleStyle.Square, 3.0f, 200f);
                }
                if (Once("hit", Math.Max(0f, Strike - (arcane ? 0f : 0.02f))))
                {
                    var (x, y) = At(StrikePoint);
                    p.Burst(x, y, 26, attackPalette, (180f, 460f), ((float)(-Pi * 0.45), (float)(Pi * 0.35)), (0.16f, 0.38f), (1.5f, 2.5f), ParticleStyle.Spark, 4.0f);
                    p.Burst(x, y, 12, attackPalette, (40f, 160f), null, (0.3f, 0.7f), (2f, 3f), ParticleStyle.Square, 3.0f, 240f, 10f);
                    p.Burst(x, y, 3, attackPalette, (0f, 20f), null, (0.18f, 0.3f), (28f, 40f), ParticleStyle.Glow, 4.0f);
                }
            }
            else if (a == "guard")
            {
                if (Once("ward", guardTime))
                {
                    var (x, y) = At(WardAt);
                    p.Burst(x, y, 20, Ward, (80f, 260f), ((float)(-Pi * 0.5), (float)(Pi * 0.5)), (0.25f, 0.55f), (1.5f, 3f), ParticleStyle.Spark, 3.5f, 0f, 20f);
                    p.Burst(x, y, 2, Ward, (0f, 10f), null, (0.25f, 0.4f), (34f, 46f), ParticleStyle.Glow, 4.0f);
                }
                if (Once("motes", guardTime + 0.12f))
                    for (var i = 0; i < 10; i++)
                    {
                        var (x, y) = At((WardAt.x + r.UniformF(-6, 10), WardAt.y + r.UniformF(-34, 34)));
                        p.Emit(x, y, r.UniformF(-10, 20), r.UniformF(-40, -10), r.UniformF(0.5f, 0.9f), Ward, r.UniformF(1.5f, 2.5f), ParticleStyle.Square, 1.0f);
                    }
            }
            else if (a == "hurt" || a == "death")
            {
                if (Once("hit", 0f))
                {
                    var (x, y) = At(Chest);
                    p.Burst(x, y, 22, Ember, (120f, 320f), ((float)(Pi * 0.6), (float)(Pi * 1.4)), (0.25f, 0.55f), (1.5f, 2.5f), ParticleStyle.Spark, 3.5f);
                    p.Burst(x, y, 2, Ember, (0f, 10f), null, (0.12f, 0.22f), (34f, 44f), ParticleStyle.Glow, 4.0f);
                }
                if (a == "death" && Once("kneel", deathKneel))
                {
                    var (x, y) = At(Feet);
                    p.Burst(x, y, 24, Dust, (40f, 160f), ((float)Pi, (float)(2 * Pi)), (0.4f, 0.9f), (2f, 4f), ParticleStyle.Square, 2.5f, 120f, 24f);
                }
            }
            else if (a == "cast")
            {
                if (Once("charge", chargeTime))
                {
                    var (x, y) = At(CastPoint);
                    p.Implode(x, y, 20, castPalette, (50f, 100f), (0.25f, 0.4f));
                }
                if (chargeTime + 0.05f < t && t < CastSeconds + 0.3f)
                    for (var i = 0; i < 2; i++)
                    {
                        var (x, y) = At((r.UniformF(-50, 50), r.UniformF(10, 90)));
                        p.Emit(x, y, r.UniformF(-8, 8), r.UniformF(-120, -60), r.UniformF(0.5f, 0.9f), castPalette, r.UniformF(1.5f, 3f), ParticleStyle.Square, 0.8f);
                    }
                if (Once("flare", CastSeconds))
                {
                    var (x, y) = At(CastPoint);
                    p.Burst(x, y, 3, castPalette, (0f, 10f), null, (0.3f, 0.45f), (30f, 44f), ParticleStyle.Glow, 4.0f);
                }
            }
        }

        /// <summary>_shadow(120, 22): concentric SRCALPHA ellipses (each overwrites), darker in the middle.</summary>
        public static Sprite Shadow(Art art, int w = 120, int h = 22)
        {
            return new Sprite(art.Procedural("hero_shadow_" + w + "x" + h, w, h, () =>
            {
                var px = new Rgba[w * h];
                for (var i = 6; i > 0; i--)
                {
                    var k = i / 6.0; var a = (int)(28 * (1.2 - k) + 12);
                    double ex = w * (1 - k) / 2, ey = h * (1 - k) / 2, ew = w * k, eh = h * k;
                    double cx = ex + ew / 2, cy = ey + eh / 2, rx = ew / 2, ry = eh / 2;
                    for (var y = 0; y < h; y++)
                        for (var x = 0; x < w; x++)
                        {
                            double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                            if (dx * dx + dy * dy <= 1) px[y * w + x] = new Rgba(0, 0, 0, a);
                        }
                }
                return px;
            }));
        }

        public void DrawShadow(Canvas canvas, Art art, (float x, float y) c)
        {
            center = c;
            var (x, y) = At(Feet);
            canvas.Blit(Shadow(art), (int)x - 60, (int)y - 9);
        }

        public void Draw(Canvas canvas, Art art, (float x, float y) c)
        {
            center = c;
            Particles.Draw(canvas, art);
        }
    }
}
