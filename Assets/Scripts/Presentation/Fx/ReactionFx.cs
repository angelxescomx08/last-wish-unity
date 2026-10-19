using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>fx/reaction_fx.py: the two element icons clash, burst + flash + shake, then the reaction icon and its result.</summary>
    public sealed class ReactionShow
    {
        public const float Clash = 0.38f, Life = 1.9f, ShakeTime = 0.22f;
        public const int ShakePx = 5;
        private static readonly Dictionary<ReactionKind, Rgba[]> Palettes = new Dictionary<ReactionKind, Rgba[]>
        {
            [ReactionKind.Steam] = new[] { new Rgba(250, 250, 255), new Rgba(210, 216, 232), new Rgba(255, 150, 90), new Rgba(130, 190, 255) },
            [ReactionKind.Magma] = new[] { new Rgba(255, 236, 160), new Rgba(255, 150, 50), new Rgba(210, 70, 30), new Rgba(70, 40, 34) },
            [ReactionKind.Wildfire] = new[] { new Rgba(255, 240, 170), new Rgba(255, 150, 60), new Rgba(230, 70, 40) },
            [ReactionKind.Mud] = new[] { new Rgba(150, 110, 62), new Rgba(196, 160, 104), new Rgba(110, 176, 255), new Rgba(70, 46, 26) },
            [ReactionKind.Freeze] = new[] { new Rgba(236, 252, 255), new Rgba(170, 226, 255), new Rgba(90, 170, 230) },
            [ReactionKind.Sandstorm] = new[] { new Rgba(244, 222, 160), new Rgba(214, 176, 104), new Rgba(168, 128, 66) },
        };
        private static readonly Dictionary<ReactionKind, Rgba[]> Palettes2 = new Dictionary<ReactionKind, Rgba[]>();

        private sealed class Show
        {
            public float X, Y, Age; public ReactionKind Reaction; public ElementKind? First, Second; public string Result; public bool Hit;
        }

        private readonly BurstParticles bursts;
        private readonly List<Show> items = new List<Show>();
        private float shake;
        private readonly Rng rng;

        public ReactionShow(BurstParticles bursts, int seed = 0) { this.bursts = bursts; rng = new Rng(seed); }
        public bool Busy => items.Count > 0;
        public int Count => items.Count;

        public void Add((float x, float y) pos, ReactionKind reaction, ElementKind? first, ElementKind? second, string result, float delay = 0f)
            => items.Add(new Show { X = pos.x, Y = pos.y, Reaction = reaction, First = first, Second = second, Result = result ?? "", Age = -Math.Max(0f, delay) });

        public void Update(float dt)
        {
            dt = Math.Max(0f, Math.Min(dt, 0.1f));
            shake = Math.Max(0f, shake - dt);
            foreach (var s in items)
            {
                s.Age += dt;
                if (!s.Hit && s.Age >= Clash) { s.Hit = true; Impact(s); }
            }
            items.RemoveAll(s => s.Age >= Life);
        }

        private static Rgba[] FirstTwo(ReactionKind r)
        {
            if (!Palettes2.TryGetValue(r, out var p)) Palettes2[r] = p = new[] { Palettes[r][0], Palettes[r][1] };
            return p;
        }

        private void Impact(Show s)
        {
            var pal = Palettes[s.Reaction];
            var style = s.Reaction == ReactionKind.Magma || s.Reaction == ReactionKind.Freeze || s.Reaction == ReactionKind.Wildfire ? ParticleStyle.Spark : ParticleStyle.Square;
            bursts.Burst(s.X, s.Y, 46, pal, (90f, 360f), null, (0.4f, 1.0f), (2f, 5f), style, 2.4f, 0f, 10f);
            bursts.Burst(s.X, s.Y, 8, FirstTwo(s.Reaction), (10f, 70f), null, (0.5f, 0.9f), (22f, 40f), ParticleStyle.Glow, 3.0f, 0f, 24f);
            if (s.Reaction == ReactionKind.Steam)
                bursts.Burst(s.X, s.Y, 18, FirstTwo(s.Reaction), (30f, 90f), (-2.6f, -0.5f), (0.8f, 1.3f), (4f, 7f), ParticleStyle.Square, 1.2f, -60f);
            shake = ShakeTime;
        }

        public (int x, int y) ShakeOffset()
        {
            if (shake <= 0) return (0, 0);
            var k = shake / ShakeTime;
            return ((int)(rng.Uniform(-1, 1) * ShakePx * k), (int)(rng.Uniform(-1, 1) * ShakePx * k));
        }

        public void Draw(Canvas canvas, Art art, Glossary g)
        {
            foreach (var s in items)
            {
                if (s.Age < 0) continue;
                if (s.Age < Clash) DrawClash(canvas, art, g, s);
                else DrawResult(canvas, art, g, s);
            }
        }

        private static void DrawClash(Canvas canvas, Art art, Glossary g, Show s)
        {
            var u = s.Age / Clash; var ease = u * u;
            foreach (var (el, side) in new[] { (s.First, -1), (s.Second, 1) })
            {
                if (!el.HasValue) continue;
                var iconName = ElementPreview.Icon(el.Value);
                if (!art.HasIcon(iconName)) continue;
                var icon = art.Icon(iconName);
                var x = s.X + side * 90 * (1 - ease);
                var ink = ElementPreview.Ink(g, el.Value);
                for (var k = 1; k < 4; k++)
                {
                    var tx = s.X + side * 90 * (1 - Math.Max(0f, ease - 0.08f * k));
                    canvas.Circle((int)tx, (int)s.Y, Math.Max(1, 5 - k), ink);
                }
                var r = IRect.AtCenter((int)x, (int)s.Y, icon.W * 3, icon.H * 3);
                canvas.Blit(icon, r.X, r.Y, 3);
            }
        }

        private static void DrawResult(Canvas canvas, Art art, Glossary g, Show s)
        {
            var t = s.Age - Clash; var rest = Life - Clash;
            var color = ElementPreview.ReactionColor(g, s.Reaction);
            if (t < 0.18f) Glows.Draw(canvas, art, (int)s.X, (int)s.Y, (int)(40 + 160 * t), color);
            var scale = t < 0.16f ? 5 : 4;
            var rise = 26 * Math.Max(0f, t - 0.6f);
            var alpha = t < rest - 0.5f ? 255 : (int)(255 * Math.Max(0f, (rest - t) / 0.5f));
            var y = s.Y - rise;
            canvas.PushAlpha(alpha / 255f);
            var iconName = ElementPreview.ReactionIcon(g, s.Reaction);
            if (art.HasIcon(iconName))
            {
                var icon = art.Icon(iconName); var r = IRect.AtCenter((int)s.X, (int)(y - 22), icon.W * scale, icon.H * scale);
                canvas.Blit(icon, r.X, r.Y, scale);
            }
            Txt.OutlinedAt(canvas, art.Fonts.Get(32), "¡" + CombatLog.ReactionName(g, s.Reaction).ToUpperInvariant() + "!", Anchor.Center, (int)s.X, (int)(y + 22), color);
            if (s.Result.Length > 0) Txt.OutlinedAt(canvas, art.Fonts.Get(16), s.Result, Anchor.Center, (int)s.X, (int)(y + 50), new Rgba(236, 228, 214));
            canvas.PopAlpha();
        }
    }
}
