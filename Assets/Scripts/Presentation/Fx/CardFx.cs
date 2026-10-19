using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    public sealed class ReadyStyle
    {
        public string Label, Icon; public Rgba Color, Light;
        public ReadyStyle(string label, Rgba color, Rgba light, string icon) { Label = label; Color = color; Light = light; Icon = icon; }
    }

    /// <summary>fx/card_fx.py: "ready" keyword aura, comets, corner flares and badge; lucky-card glow.</summary>
    public static class CardFx
    {
        public static readonly Dictionary<EffectCondition, ReadyStyle> Styles = new Dictionary<EffectCondition, ReadyStyle>
        {
            [EffectCondition.Combo] = new ReadyStyle("¡COMBO!", new Rgba(60, 230, 170), new Rgba(200, 255, 230), "combo"),
            [EffectCondition.Spoil] = new ReadyStyle("¡DESPOJO!", new Rgba(250, 150, 50), new Rgba(255, 222, 170), "spoil"),
            [EffectCondition.Void] = new ReadyStyle("¡VACÍO!", new Rgba(90, 160, 255), new Rgba(200, 225, 255), "void"),
            [EffectCondition.Singular] = new ReadyStyle("¡SINGULAR!", new Rgba(190, 130, 255), new Rgba(236, 214, 255), "singular"),
            [EffectCondition.Reaction] = new ReadyStyle("¡REACCIÓN!", new Rgba(255, 120, 200), new Rgba(255, 220, 240), "reaction"),
            [EffectCondition.Fury] = new ReadyStyle("¡FURIA!", new Rgba(235, 50, 50), new Rgba(255, 200, 190), "fury"),
            [EffectCondition.Endure] = new ReadyStyle("¡AGUANTE!", new Rgba(200, 205, 220), new Rgba(245, 248, 255), "endure"),
        };
        public static readonly EffectCondition[] Order = { EffectCondition.Combo, EffectCondition.Reaction, EffectCondition.Fury, EffectCondition.Endure, EffectCondition.Spoil, EffectCondition.Void, EffectCondition.Singular };
        public const float CometSpeed = 0.32f; public const int CometsPerKeyword = 3, Trail = 9;
        public static readonly Rgba LuckyColor = new Rgba(120, 230, 110), LuckyLight = new Rgba(220, 255, 200);

        private static readonly List<ReadyStyle> scratch = new List<ReadyStyle>(8);
        public static List<ReadyStyle> StylesOf(uint mask)
        {
            scratch.Clear();
            foreach (var k in Order) if ((mask & CardTotals.Bit(k)) != 0) scratch.Add(Styles[k]);
            return scratch;
        }

        public static Rgba? CurrentColor(List<ReadyStyle> styles, float t)
        {
            if (styles.Count == 0) return null;
            if (styles.Count == 1) return styles[0].Color;
            var i = (int)(t / 1.2) % styles.Count;
            var k = MathX.PyMod(t / 1.2, 1.0);
            Rgba a = styles[i].Color, b = styles[(i + 1) % styles.Count].Color;
            k = Math.Max(0.0, (k - 0.75) / 0.25);
            return new Rgba((int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k));
        }

        public static void DrawReadyBack(Canvas canvas, Art art, int rarity, float cx, float cy, int w, int h, uint mask, float t, float angle)
        {
            var color = CurrentColor(StylesOf(mask), t);
            if (!color.HasValue) return;
            ChromaFx.DrawCardAura(canvas, art, rarity, cx, cy, w, h, angle, color.Value, t, 5f);
            ChromaFx.DrawCardAura(canvas, art, rarity, cx, cy, w, h, angle, color.Value, t + 0.6f, 5f);
        }

        public static (double x, double y) PerimeterPoint(double u, double w, double h)
        {
            u = MathX.PyMod(u, 1.0);
            var d = u * 2 * (w + h);
            if (d < w) return (-w / 2 + d, -h / 2);
            d -= w; if (d < h) return (w / 2, -h / 2 + d);
            d -= h; if (d < w) return (w / 2 - d, h / 2);
            d -= w; return (-w / 2, h / 2 - d);
        }
        public static (double x, double y) Rotate(double x, double y, double angle)
        {
            if (angle == 0) return (x, y);
            var a = angle * Math.PI / 180; double c = Math.Cos(a), s = Math.Sin(a);
            return (x * c + y * s, -x * s + y * c);
        }

        public static RectF? DrawReadyFront(Canvas canvas, GameContext g, float cx, float cy, int sw, int sh, uint mask, float t, float angle, bool badge = true)
        {
            var styles = StylesOf(mask);
            if (styles.Count == 0) return null;
            double w = sw + 4, h = sh + 4;
            var scale = Math.Max(0.6, sw / 150.0);
            var n = styles.Count * CometsPerKeyword;
            var step = 5.0 / (2 * (w + h));
            var art = g.Art;
            for (var i = 0; i < n; i++)
            {
                var st = styles[i % styles.Count];
                var u0 = t * CometSpeed + i / (double)n;
                for (var k = Trail; k >= 0; k--)
                {
                    var (x, y) = PerimeterPoint(u0 - k * step, w, h); (x, y) = Rotate(x, y, angle);
                    var fade = 1 - k / (double)(Trail + 1);
                    int px = (int)(cx + x), py = (int)(cy + y);
                    if (k == 0)
                    {
                        var gr = (int)(11 * scale);
                        canvas.BlitRect(Glows.Soft(art), new RectF(px - gr, py - gr, gr * 2, gr * 2), st.Color, Blend.Add);
                        var s = Math.Max(3, (int)(4 * scale));
                        canvas.Fill(new RectF(px - s / 2, py - s / 2, s, s), st.Light);
                    }
                    else
                    {
                        var s = Math.Max(1, (int)(3.2 * fade * scale));
                        var col = new Rgba((int)(st.Color.R * (0.35 + 0.65 * fade)), (int)(st.Color.G * (0.35 + 0.65 * fade)), (int)(st.Color.B * (0.35 + 0.65 * fade)));
                        canvas.Fill(new RectF(px - s / 2, py - s / 2, s, s), col);
                    }
                }
            }
            var pulse = 0.5 + 0.5 * Math.Sin(t * 6.0);
            foreach (var (fx, fy) in new[] { (0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0) })
            {
                var (x, y) = Rotate((fx - 0.5) * w, (fy - 0.5) * h, angle);
                var gr = (int)((6 + 5 * pulse) * scale);
                canvas.BlitRect(Glows.Soft(art), new RectF((int)(cx + x) - gr, (int)(cy + y) - gr, gr * 2, gr * 2), styles[0].Color, Blend.Add);
            }
            if (!badge) return null;
            return DrawBadge(canvas, g, cx, cy, h, styles, t, scale, angle);
        }

        private static RectF DrawBadge(Canvas canvas, GameContext g, float cx, float cy, double h, List<ReadyStyle> styles, float t, double scale, double angle)
        {
            var font = g.Fonts.Get(Math.Max(10, (int)(13 * scale)));
            var width = 16 + 10 * (styles.Count - 1);
            foreach (var st in styles) { var icon = g.Art.Icon(st.Icon); width += (icon.Valid ? icon.W + 3 : 0) + font.Width(st.Label); }
            var height = Math.Max(font.Height, 14) + 6;
            var bob = Math.Sin(t * 4.0) * 2 * scale;
            var (x, y) = Rotate(0, -h / 2 - height / 2.0 - 2, angle);
            var rect = IRect.AtCenter((int)(cx + x), (int)(cy + y + bob), width, height);
            var pulse = 0.5 + 0.5 * Math.Sin(t * 6.0);
            var main = styles.Count > 1 ? styles[(int)(t / 1.2) % styles.Count].Color : styles[0].Color;
            var gr = Math.Max(1, (int)(width * 0.45));
            canvas.BlitRect(Glows.Soft(g.Art), new RectF(rect.CenterX - gr, rect.CenterY - gr, gr * 2, gr * 2), main, Blend.Add);
            canvas.Rect(rect, new Rgba(12, 8, 18), 0, height / 2);
            canvas.Rect(rect, new Rgba((int)(main.R * (0.7 + 0.3 * pulse)), (int)(main.G * (0.7 + 0.3 * pulse)), (int)(main.B * (0.7 + 0.3 * pulse))), 2, height / 2);
            var px = rect.X + 8;
            foreach (var st in styles)
            {
                var icon = g.Art.Icon(st.Icon);
                if (icon.Valid) { canvas.Blit(icon, px, rect.CenterY - icon.H / 2); px += icon.W + 3; }
                var ty = rect.CenterY - font.Height / 2;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) font.Draw(canvas, st.Label, px + dx, ty + dy, new Rgba(10, 6, 14));
                font.Draw(canvas, st.Label, px, ty, st.Light);
                px += font.Width(st.Label) + 10;
            }
            return rect;
        }

        public static void DrawLuckyBack(Canvas canvas, Art art, RectF rect, float t)
        {
            var pulse = 0.5 + 0.5 * Math.Sin(t * 4.0);
            var c = Glows.Scaled(LuckyColor, 0.35 + 0.25 * pulse);
            var r = (int)Math.Max(rect.W, rect.H) / 2 + 30;
            canvas.BlitRect(Glows.Soft(art), new RectF((int)(rect.X + (int)rect.W / 2) - r, (int)(rect.Y + (int)rect.H / 2) - r, r * 2, r * 2), c, Blend.Add);
        }

        private static readonly List<ReadyStyle> luckyStyles = new List<ReadyStyle> { new ReadyStyle("¡SUERTE!", LuckyColor, LuckyLight, "clover") };
        public static RectF? DrawLuckyFront(Canvas canvas, GameContext g, RectF rect, float t, bool badge = true)
        {
            int cx = (int)rect.X + (int)rect.W / 2, cy = (int)rect.Y + (int)rect.H / 2;
            for (var k = 0; k < 6; k++)
            {
                var (x, y) = PerimeterPoint(t * 0.18 + k / 6.0, rect.W + 10, rect.H + 10);
                canvas.Fill(new RectF(cx + (int)x - 2, cy + (int)y - 2, 4, 4), k % 2 == 1 ? LuckyLight : LuckyColor);
            }
            if (!badge) return null;
            return DrawBadge(canvas, g, cx, cy, rect.H + 4, luckyStyles, t, Math.Max(0.6, rect.W / 150.0), 0);
        }
    }
}
