using System;
using System.Collections.Generic;

namespace LastWish.Presentation
{
    /// <summary>ui/targeting.py: chevron arrow from the held card to the pointer, and the reticle brackets.</summary>
    public static class Targeting
    {
        public static readonly Rgba ColdFill = new Rgba(240, 232, 214), ColdLine = new Rgba(44, 30, 30), HotFill = new Rgba(232, 56, 42), HotLine = new Rgba(48, 8, 10);
        public static readonly Rgba ReticleEnemy = new Rgba(255, 212, 60), ReticleSelf = new Rgba(120, 210, 255);
        public const float Spacing = 26f, FlowSpeed = 70f, HeadGap = 44f;
        private const int AngleStep = 5, Pixel = 2;
        private static readonly float[] Sizes = { 0.6f, 0.78f, 0.95f, 1.1f };

        public static (float x, float y) ControlPoint((float x, float y) s, (float x, float y) e)
        { float dx = e.x - s.x, dy = e.y - s.y; return (s.x + dx * 0.12f, s.y + dy * 0.95f - Math.Abs(dx) * 0.18f); }
        public static (float x, float y) Bezier((float x, float y) s, (float x, float y) c, (float x, float y) e, float t)
        { var u = 1 - t; return (u * u * s.x + 2 * u * t * c.x + t * t * e.x, u * u * s.y + 2 * u * t * c.y + t * t * e.y); }

        private static readonly List<(float x, float y)> pts = new List<(float, float)>(49);
        private static readonly List<float> lengths = new List<float>(49);

        private static List<(float x, float y, float angle, float size)> Segments((float x, float y) start, (float x, float y) end, float phase)
        {
            var c = ControlPoint(start, end);
            pts.Clear(); for (var i = 0; i <= 48; i++) pts.Add(Bezier(start, c, end, i / 48f));
            lengths.Clear(); lengths.Add(0);
            for (var i = 1; i < pts.Count; i++) lengths.Add(lengths[i - 1] + (float)Math.Sqrt(Math.Pow(pts[i].x - pts[i - 1].x, 2) + Math.Pow(pts[i].y - pts[i - 1].y, 2)));
            var total = lengths[lengths.Count - 1]; var usable = total - HeadGap;
            var list = new List<(float, float, float, float)>();
            if (usable <= Spacing * 0.5f) return list;
            var s = (float)MathX.PyMod(phase * FlowSpeed, Spacing); var j = 0;
            while (s < usable)
            {
                while (j < lengths.Count - 2 && lengths[j + 1] < s) j++;
                var seg = Math.Max(1e-6f, lengths[j + 1] - lengths[j]); var f = (s - lengths[j]) / seg;
                var (x0, y0) = pts[j]; var (x1, y1) = pts[j + 1];
                list.Add((x0 + (x1 - x0) * f, y0 + (y1 - y0) * f, (float)(Math.Atan2(y1 - y0, x1 - x0) * 180 / Math.PI), s / total));
                s += Spacing;
            }
            return list;
        }

        private static (float[] xs, float[] ys) ChevronPoints(float k)
        { float L = 9 * k, H = 7 * k, T = 4 * k; return (new[] { 0, T, L, T, 0, L - T }, new[] { 0, 0, H, 2 * H, 2 * H, H }); }
        private static (float[] xs, float[] ys) HeadPoints() => (new float[] { 0, 22, 0, 6 }, new float[] { 0, 13, 26, 13 });

        private static bool Inside(float[] xs, float[] ys, float px, float py)
        {
            var inside = false;
            for (int i = 0, j = xs.Length - 1; i < xs.Length; j = i++)
                if ((ys[i] > py) != (ys[j] > py) && px < (xs[j] - xs[i]) * (py - ys[i]) / (ys[j] - ys[i]) + xs[i]) inside = !inside;
            return inside;
        }

        /// <summary>_outlined_polygon: filled polygon with an 8-neighbour outline at half resolution (doubled when drawn).</summary>
        private static Sprite Shape(Art art, bool head, int sizeIdx, bool hot)
        {
            var key = "arrow_" + (head ? "head" : "chev" + sizeIdx) + (hot ? "_hot" : "_cold");
            var (xs, ys) = head ? HeadPoints() : ChevronPoints(Sizes[sizeIdx]);
            float maxX = 0, maxY = 0; foreach (var x in xs) maxX = Math.Max(maxX, x); foreach (var y in ys) maxY = Math.Max(maxY, y);
            int w = (int)Math.Ceiling(maxX) + 3, h = (int)Math.Ceiling(maxY) + 3;
            var fill = hot ? HotFill : ColdFill; var line = hot ? HotLine : ColdLine;
            return new Sprite(art.Procedural(key, w, h, () =>
            {
                var px = new Rgba[w * h];
                for (var y = 0; y < h; y++)
                    for (var x = 0; x < w; x++)
                    {
                        float cx = x + 0.5f - 1, cy = y + 0.5f - 1;
                        if (Inside(xs, ys, cx, cy)) { px[y * w + x] = fill; continue; }
                        for (var oy = -1; oy <= 1; oy++) for (var ox = -1; ox <= 1; ox++)
                                if ((ox != 0 || oy != 0) && Inside(xs, ys, cx - ox, cy - oy)) px[y * w + x] = line;
                    }
                return px;
            }));
        }

        private static void DrawSprite(Canvas canvas, Sprite s, float cx, float cy, float angleDeg)
        {
            var bucket = ((MathX.Round(angleDeg / AngleStep) % (360 / AngleStep)) + 360 / AngleStep) % (360 / AngleStep);
            // pygame rotates counter-clockwise; the screen angle grows clockwise.
            canvas.BlitRotated(s, MathX.Round(cx), MathX.Round(cy), -bucket * AngleStep, Pixel, Rgba.White);
        }

        public static void DrawArrow(Canvas canvas, Art art, (float x, float y) start, (float x, float y) end, bool hot, float phase)
        {
            foreach (var (x, y, angle, t) in Segments(start, end, phase))
                DrawSprite(canvas, Shape(art, false, Math.Min(Sizes.Length - 1, (int)(t * Sizes.Length)), hot), x, y, angle);
            var c = ControlPoint(start, end); var p = Bezier(start, c, end, 0.94f);
            var a = (float)(Math.Atan2(end.y - p.y, end.x - p.x) * 180 / Math.PI);
            var back = 22 * Pixel * 0.45f;
            DrawSprite(canvas, Shape(art, true, 0, hot), end.x - (float)Math.Cos(a * Math.PI / 180) * back, end.y - (float)Math.Sin(a * Math.PI / 180) * back, a);
        }

        public static void DrawReticle(Canvas canvas, RectF rect, Rgba color, float t)
        {
            var pulse = 5 + MathX.Round(3 * Math.Sin(t * 6.0));
            var r = new IRect((int)rect.X - pulse, (int)rect.Y - pulse, (int)rect.W + pulse * 2, (int)rect.H + pulse * 2);
            var arm = Math.Max(10, Math.Min(r.W, r.H) / 5); const int thick = 4;
            var dark = new Rgba(20, 12, 8);
            foreach (var (cx, cy, sx, sy) in new[] { (r.X, r.Y, 1, 1), (r.Right, r.Y, -1, 1), (r.X, r.Bottom, 1, -1), (r.Right, r.Bottom, -1, -1) })
            {
                var x0 = sx > 0 ? cx : cx - thick; var y0 = sy > 0 ? cy : cy - thick;
                var hx = sx > 0 ? cx : cx - arm; var vy = sy > 0 ? cy : cy - arm;
                foreach (var (col, grow) in new[] { (dark, 2), (color, 0) })
                {
                    canvas.Fill(new RectF(hx - grow / 2, y0 - grow / 2, arm + grow, thick + grow), col);
                    canvas.Fill(new RectF(x0 - grow / 2, vy - grow / 2, thick + grow, arm + grow), col);
                }
            }
        }
    }

    /// <summary>ui/fx.py: hit flashes and floating numbers / labels.</summary>
    public sealed class FxLayer
    {
        private sealed class Effect
        {
            public bool Flash; public RectF Rect; public Rgba Color; public float Life, Age;
            public float X, Y; public string Text; public Sprite Icon;
        }
        private readonly List<Effect> effects = new List<Effect>();
        private readonly FontFace font; private readonly Art art;
        public FxLayer(Art art, FontFace font) { this.art = art; this.font = font; }
        public int Count => effects.Count;

        public void AddHitFlash(RectF rect, long damage, float dx = 0, float delay = 0, bool flash = true)
        {
            if (flash) effects.Add(new Effect { Flash = true, Rect = rect, Color = new Rgba(220, 40, 40), Life = 0.25f, Age = -Math.Max(0, delay) });
            effects.Add(new Effect { X = (int)rect.X + (int)rect.W / 2 + dx, Y = rect.Y + 20, Text = "-" + damage, Color = new Rgba(255, 90, 90), Life = 0.8f, Age = -Math.Max(0, delay) });
        }
        public void AddBlockFlash(RectF rect, long amount, bool flash = true)
        {
            if (flash) effects.Add(new Effect { Flash = true, Rect = rect, Color = new Rgba(60, 140, 255), Life = 0.30f });
            effects.Add(new Effect { X = (int)rect.X + (int)rect.W / 2, Y = rect.Y + 20, Text = "+" + amount + " BLQ", Color = new Rgba(100, 190, 255), Life = 0.8f });
        }
        public void AddText(float x, float y, string text, Rgba color, float lifetime = 1f, float delay = 0f, string icon = null)
            => effects.Add(new Effect { X = x, Y = y, Text = text, Color = color, Life = lifetime, Age = -Math.Max(0, delay), Icon = icon != null ? art.Icon(icon) : default });
        public void AddDeathFlash(RectF rect, float delay = 0) => effects.Add(new Effect { Flash = true, Rect = rect, Color = Rgba.White, Life = 0.4f, Age = -Math.Max(0, delay) });

        public void Update(float dt)
        {
            for (var i = effects.Count - 1; i >= 0; i--)
            {
                var e = effects[i]; e.Age += dt;
                if (!e.Flash && e.Age > 0) e.Y -= 42f * dt;
                if (e.Age >= e.Life) effects.RemoveAt(i);
            }
        }

        public void Draw(Canvas canvas)
        {
            foreach (var e in effects)
            {
                if (e.Age < 0) continue;
                if (e.Flash) { canvas.Fill(e.Rect, e.Color.WithAlpha((int)(170 * Math.Max(0, 1 - e.Age / e.Life)))); continue; }
                var a = (int)(255 * Math.Max(0, 1 - e.Age / e.Life));
                int tw = font.Width(e.Text), th = font.Height;
                if (e.Icon.Valid)
                {
                    // ui_icon(name) defaults to ×2
                    const int gap = 4; int iw = e.Icon.W * 2, ih = e.Icon.H * 2; int w = iw + gap + tw, h = Math.Max(ih, th);
                    int x = (int)e.X - w / 2, y = (int)e.Y - h / 2;
                    canvas.PushAlpha(a / 255f);
                    canvas.Blit(e.Icon, x, y + (h - ih) / 2, 2);
                    font.Draw(canvas, e.Text, x + iw + gap, y + (h - th) / 2, e.Color);
                    canvas.PopAlpha();
                }
                else font.Draw(canvas, e.Text, (int)e.X - tw / 2, (int)e.Y - th / 2, e.Color.WithAlpha(a));
            }
        }
    }
}
