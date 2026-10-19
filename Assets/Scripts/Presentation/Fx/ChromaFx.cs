using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    public sealed class ChromaStyle
    {
        public Rgba Bright, Main, Dark, Sheen, Glow, FrameMult, FrameAdd;
        public string Name, NameMasc, CardNote, RelicNote, ShortNote, PackNote;
    }

    /// <summary>fx/chroma_fx.py + domain/chroma.py texts: golden and diamond finishes.</summary>
    public static class ChromaFx
    {
        public const float SheenCycle = 3.2f, SheenSweep = 1.1f;
        public const int SheenFrames = 28;
        private static readonly (float x, float y, float ph)[] Twinkles = { (0.18f, 0.13f, 0f), (0.83f, 0.34f, 2.1f), (0.25f, 0.70f, 4.2f), (0.76f, 0.84f, 1.3f), (0.55f, 0.22f, 3.3f), (0.12f, 0.47f, 5.1f), (0.66f, 0.60f, 0.7f) };
        private static readonly (double fx, double period, double phase, double size, double sway)[] Motes;

        public static readonly ChromaStyle Golden = new ChromaStyle
        {
            Bright = new Rgba(255, 246, 196), Main = new Rgba(255, 204, 72), Dark = new Rgba(120, 74, 10), Sheen = new Rgba(255, 232, 160), Glow = new Rgba(255, 168, 36), FrameMult = new Rgba(255, 214, 122), FrameAdd = new Rgba(52, 32, 0),
            Name = "Dorada", NameMasc = "Dorado", CardNote = "Dorada: al jugarla se lanza 2 veces (su efecto completo x2).",
            RelicNote = "Dorada: por ser dorada, sus efectos son x2.", ShortNote = "Dorada: efectos x2", PackNote = "Dorado: eliges 2 cartas (x2).",
        };
        public static readonly ChromaStyle Diamond = new ChromaStyle
        {
            Bright = new Rgba(240, 254, 255), Main = new Rgba(140, 230, 255), Dark = new Rgba(30, 80, 120), Sheen = new Rgba(225, 250, 255), Glow = new Rgba(90, 210, 255), FrameMult = new Rgba(190, 240, 255), FrameAdd = new Rgba(10, 30, 50),
            Name = "Diamante", NameMasc = "Diamante", CardNote = "Diamante: al jugarla se lanza 3 veces (su efecto completo x3).",
            RelicNote = "Diamante: sus efectos son x3.", ShortNote = "Diamante: efectos x3", PackNote = "Diamante: eliges 3 cartas (x3).",
        };

        static ChromaFx()
        {
            // _MOTE_RNG = random.Random(1717): the original motes are deterministic.
            var rng = new PyRandom(1717);
            Motes = new (double, double, double, double, double)[18];
            for (var i = 0; i < 18; i++) Motes[i] = (rng.Uniform(-0.08, 1.08), rng.Uniform(1.6, 3.0), rng.Uniform(0, 3.0), rng.Uniform(1.5, 3.2), rng.Uniform(2.0, 7.0));
        }

        public static ChromaStyle Style(ChromaKind chroma) => chroma == ChromaKind.Diamond ? Diamond : Golden;
        public static string Title(string name, ChromaKind chroma, bool masculine = false)
        {
            if (chroma == ChromaKind.None) return name;
            var st = Style(chroma);
            return name + " · " + (masculine && !string.IsNullOrEmpty(st.NameMasc) ? st.NameMasc : st.Name);
        }
        public static string Suffix(ChromaKind chroma) => chroma == ChromaKind.Diamond ? "diamond" : "golden";

        private static Rgba Lerp(Rgba a, Rgba b, double k) => new Rgba((int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k));

        /// <summary>The sweeping band of <c>sheen_frame</c> drawn additively inside <paramref name="rect"/>, masked by <paramref name="mask"/> (null = the whole rect).</summary>
        public static void Sheen(Canvas canvas, RectF rect, ChromaKind chroma, float t, Sprite? mask = null, Art art = null)
        {
            var phase = (float)MathX.PyMod(t, SheenCycle);
            if (phase >= SheenSweep || rect.W <= 0 || rect.H <= 0) return;
            var st = Style(chroma);
            var i = Math.Min(SheenFrames - 1, (int)(phase / SheenSweep * SheenFrames));
            var u = i / (float)(SheenFrames - 1);
            var w = rect.W; var h = rect.H;
            var slant = h * 0.45f; var bw = Math.Max(5f, w * 0.10f);
            var cx = -bw + u * (w + slant + 2 * bw);
            var white = new Sprite(canvas.White);
            foreach (var (hw, k) in new[] { (bw, 0.22), (bw * 0.6f, 0.45), (bw * 0.28f, 0.85) })
            {
                var poly = new[] { (rect.X + cx - hw, rect.Y), (rect.X + cx + hw, rect.Y), (rect.X + cx + hw - slant, rect.Bottom), (rect.X + cx - hw - slant, rect.Bottom) };
                var color = Glows.Scaled(st.Sheen, k);
                if (mask.HasValue) canvas.BlitClippedConvex(mask.Value, rect, poly, color, Blend.AddMask);
                else canvas.BlitClippedConvex(white, rect, poly, color, Blend.Add);
            }
        }

        public static void DrawTwinkles(Canvas canvas, RectF rect, ChromaKind chroma, float t, float scale = 1f)
        {
            var st = Style(chroma);
            foreach (var (fx, fy, ph) in Twinkles)
            {
                var k = Math.Sin(t * 2.6 + ph);
                if (k <= 0.35) continue;
                k = (k - 0.35) / 0.65;
                var r = Math.Max(2, (int)((2 + 5 * k) * scale));
                var x = rect.X + (int)(fx * rect.W); var y = rect.Y + (int)(fy * rect.H);
                var col = Lerp(st.Main, st.Bright, k);
                canvas.Line(x - r, y, x + r, y, col, 1); canvas.Line(x, y - r, x, y + r, col, 1);
                var d = Math.Max(1, r / 3);
                canvas.Line(x - d, y - d, x + d, y + d, st.Bright, 1); canvas.Line(x - d, y + d, x + d, y - d, st.Bright, 1);
                canvas.Fill(new RectF(x - 1, y - 1, 2, 2), st.Bright);
            }
        }

        /// <summary>_mote_glow(chroma, r): concentric circles on black, drawn additively.</summary>
        private static Sprite MoteGlow(Art art) => Glows.Soft(art);

        public static void DrawMotes(Canvas canvas, Art art, RectF rect, ChromaKind chroma, float t, int count = 14, float scale = 1f)
        {
            var st = Style(chroma);
            var rise = rect.H * 0.95 + 24 * scale;
            for (var i = 0; i < Math.Min(count, Motes.Length); i++)
            {
                var (fx, period, phase, sz, sway) = Motes[i];
                var u = MathX.PyMod((t + phase) / period, 1.0);
                var a = Math.Sin(Math.PI * u);
                var x = rect.Left + fx * rect.W + Math.Sin((t + phase) * 2.1 + i) * sway * scale;
                var y = rect.Bottom + 4 * scale - u * rise;
                var s = Math.Max(1, (int)(sz * scale * (0.45 + 0.55 * a)));
                var col = a < 0.75 ? Lerp(st.Dark, st.Bright, a) : st.Bright;
                canvas.Fill(new RectF((int)x - s / 2, (int)y - s / 2, s, s), col);
                if (a > 0.55)
                {
                    var gr = Math.Max(2, (int)(s * 2.2));
                    // (1 - rr/r)^2 * 0.7 falloff: the shared soft glow at 70 % of the colour.
                    canvas.BlitRect(MoteGlow(art), new RectF((int)x - gr, (int)y - gr, gr * 2, gr * 2), Glows.Scaled(st.Glow, 0.7), Blend.Add);
                }
            }
        }

        /// <summary>gild_frame + animate_card_face for any sprite (golden packs): recoloured, then the sheen and twinkles.</summary>
        public static void DrawGilded(Canvas canvas, Sprite sprite, RectF dst, ChromaKind chroma, float t, float baseW, float alpha = 1f)
        {
            var st = Style(chroma);
            canvas.BlitRect(sprite, dst, st.FrameMult.WithAlpha((int)(255 * alpha)));
            canvas.BlitRect(sprite, dst, Glows.Scaled(st.FrameAdd, alpha), Blend.AddMask);
            if (alpha < 1f) return;
            Sheen(canvas, dst, chroma, t, sprite);
            DrawTwinkles(canvas, dst, chroma, t, Math.Max(0.5f, dst.W / baseW));
        }

        /// <summary>Silhouette aura of a card (precomputed white aura per rarity, tinted).</summary>
        public static void DrawCardAura(Canvas canvas, Art art, int rarity, float cx, float cy, float w, float h, float angle, Rgba color, float t, float speed = 1.8f)
        {
            var breath = 0.5 + 0.5 * Math.Sin(t * speed);
            var level = 2 + (int)(breath * 1.999);
            var tex = art.Texture("Cards/Fx/aura_" + Art.RarityKey(rarity).ToLowerInvariant());
            var pad = Math.Max(12, (int)Math.Min(w, h) / 4);
            var aw = w + 2 * pad; var ah = h + 2 * pad;
            var sprite = new Sprite(tex);
            canvas.PushRotateScale((int)cx, (int)cy, angle, 1f);
            canvas.BlitRect(sprite, new RectF((int)cx - aw / 2, (int)cy - ah / 2, aw, ah), Glows.Scaled(color, level / 4.0), Blend.Add);
            canvas.PopTransform();
        }

        /// <summary>_aura(w, h, chroma, level, radius): edge-lit glow around a rounded rect (relic boxes).</summary>
        public static void DrawHalo(Canvas canvas, Art art, float cx, float cy, float w, float h, ChromaKind chroma, float t, bool strong = true, int radius = 10)
        {
            var breath = 0.5 + 0.5 * Math.Sin(t * 1.8);
            var level = (strong ? 2 : 1) + (int)(breath * 1.999);
            var iw = (int)w; var ih = (int)h;
            var pad = Math.Max(12, Math.Min(iw, ih) / 4);
            var key = "halo_" + iw + "x" + ih + "_" + radius;
            var tex = art.Procedural(key, iw + 2 * pad, ih + 2 * pad, () => Procedural.HaloPixels(iw, ih, pad, radius), smooth: true);
            canvas.BlitRect(new Sprite(tex), RectF.CenteredInt(cx, cy, iw + 2 * pad, ih + 2 * pad), Glows.Scaled(Style(chroma).Glow, level / 4.0), Blend.Add);
        }

        /// <summary>draw_chroma_box: relic boxes and tiles.</summary>
        public static void DrawBox(Canvas canvas, Art art, RectF rect, ChromaKind chroma, float t, int radius = 6, bool halo = true)
        {
            var st = Style(chroma);
            if (halo) DrawHalo(canvas, art, rect.CenterX, rect.CenterY, rect.W, rect.H, chroma, t, false, radius);
            var phase = (float)MathX.PyMod(t, SheenCycle);
            if (phase < SheenSweep && rect.W > 0 && rect.H > 0)
            {
                var mask = new Sprite(art.Procedural("boxmask_" + (int)rect.W + "x" + (int)rect.H + "_" + radius, (int)rect.W, (int)rect.H,
                    () => Procedural.RoundRectMask((int)rect.W, (int)rect.H, radius)));
                Sheen(canvas, rect, chroma, t, mask, art);
            }
            var pulse = 0.5 + 0.5 * Math.Sin(t * 2.2);
            canvas.Rect(rect, Lerp(st.Main, st.Bright, pulse * 0.6), 2, radius);
            if (rect.W > 8 && rect.H > 8) canvas.Rect(rect.Inflate(-4, -4), st.Dark, 1, Math.Max(0, radius - 2));
            var scale = Math.Min(1f, Math.Max(0.5f, rect.W / 140f));
            DrawTwinkles(canvas, rect, chroma, t, scale);
            DrawMotes(canvas, art, rect, chroma, t, rect.W < 100 ? 6 : 10, scale);
        }
    }

    /// <summary>Procedural pixel buffers that mirror pygame surface recipes (built once and cached).</summary>
    public static class Procedural
    {
        /// <summary>White rounded rect (alpha mask) like pygame.draw.rect(border_radius).</summary>
        public static Rgba[] RoundRectMask(int w, int h, int radius)
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            var px = new Rgba[w * h];
            radius = Math.Min(radius, Math.Min(w, h) / 2);
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    px[y * w + x] = InRound(x, y, w, h, radius) ? new Rgba(255, 255, 255, 255) : new Rgba(255, 255, 255, 0);
            return px;
        }

        public static bool InRound(int x, int y, int w, int h, int r)
        {
            if (r <= 0) return true;
            var cx = x < r ? r : x >= w - r ? w - r - 1 : x;
            var cy = y < r ? r : y >= h - r ? h - r - 1 : y;
            var dx = x - cx; var dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        /// <summary>Box-blur by a halving pyramid like chroma_fx._blur (grayscale, float).</summary>
        public static float[] PyramidBlur(float[] src, int w, int h, int levels)
        {
            var sizes = new List<(int, int)>(); var cur = src; int cw = w, ch = h;
            for (var l = 0; l < levels; l++)
            {
                sizes.Add((cw, ch));
                int nw = Math.Max(1, cw / 2), nh = Math.Max(1, ch / 2);
                var next = new float[nw * nh];
                for (var y = 0; y < nh; y++)
                    for (var x = 0; x < nw; x++)
                    {
                        float sum = 0; var n = 0;
                        for (var yy = y * ch / nh; yy < Math.Max(y * ch / nh + 1, (y + 1) * ch / nh); yy++)
                            for (var xx = x * cw / nw; xx < Math.Max(x * cw / nw + 1, (x + 1) * cw / nw); xx++) { sum += cur[yy * cw + xx]; n++; }
                        next[y * nw + x] = sum / n;
                    }
                cur = next; cw = nw; ch = nh;
            }
            for (var l = sizes.Count - 1; l >= 0; l--)
            {
                var (tw, th) = sizes[l];
                var up = new float[tw * th];
                for (var y = 0; y < th; y++)
                    for (var x = 0; x < tw; x++)
                    {
                        var fx = Math.Max(0, Math.Min(cw - 1, (x + 0.5f) * cw / tw - 0.5f)); var fy = Math.Max(0, Math.Min(ch - 1, (y + 0.5f) * ch / th - 0.5f));
                        int x0 = (int)fx, y0 = (int)fy, x1 = Math.Min(cw - 1, x0 + 1), y1 = Math.Min(ch - 1, y0 + 1);
                        float ax = fx - x0, ay = fy - y0;
                        up[y * tw + x] = cur[y0 * cw + x0] * (1 - ax) * (1 - ay) + cur[y0 * cw + x1] * ax * (1 - ay) + cur[y1 * cw + x0] * (1 - ax) * ay + cur[y1 * cw + x1] * ax * ay;
                    }
                cur = up; cw = tw; ch = th;
            }
            return cur;
        }

        /// <summary>chroma_fx._aura at full level with white: rim + 0.55 haze, cleared inside (tinted when drawn).</summary>
        public static Rgba[] HaloPixels(int w, int h, int pad, int radius)
        {
            int sw = w + 2 * pad, sh = h + 2 * pad;
            var rim = new float[sw * sh]; var haze = new float[sw * sh];
            var thick = Math.Max(2, pad / 5);
            for (var y = 0; y < sh; y++)
                for (var x = 0; x < sw; x++)
                {
                    int rx = x - (pad - 2), ry = y - (pad - 2);
                    var outer = rx >= 0 && ry >= 0 && rx < w + 4 && ry < h + 4 && InRound(rx, ry, w + 4, h + 4, radius + 2);
                    var inner = rx >= thick && ry >= thick && rx < w + 4 - thick && ry < h + 4 - thick && InRound(rx - thick, ry - thick, w + 4 - 2 * thick, h + 4 - 2 * thick, Math.Max(0, radius + 2 - thick));
                    rim[y * sw + x] = outer && !inner ? 255f : 0f;
                    int hx = x - pad, hy = y - pad;
                    haze[y * sw + x] = hx >= 0 && hy >= 0 && hx < w && hy < h && InRound(hx, hy, w, h, radius) ? 255f * 0.55f : 0f;
                }
            rim = PyramidBlur(rim, sw, sh, 2); haze = PyramidBlur(haze, sw, sh, 4);
            var inset = Math.Max(2, Math.Min(w, h) / 20);
            var px = new Rgba[sw * sh];
            for (var y = 0; y < sh; y++)
                for (var x = 0; x < sw; x++)
                {
                    var v = Math.Min(255f, rim[y * sw + x] + haze[y * sw + x]);
                    int ix = x - pad - inset, iy = y - pad - inset;
                    if (ix >= 0 && iy >= 0 && ix < w - 2 * inset && iy < h - 2 * inset && InRound(ix, iy, w - 2 * inset, h - 2 * inset, Math.Max(0, radius - inset))) v = 0;
                    var c = (int)v; px[y * sw + x] = new Rgba(c, c, c, 255);
                }
            return px;
        }
    }
}
