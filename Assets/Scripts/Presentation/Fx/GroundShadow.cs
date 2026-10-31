using System;

namespace LastWish.Presentation
{
    /// <summary>
    /// Contact shadows in the art's own pixels (docs/art-style.md): a flat ellipse in a cold near-black, darkest under the
    /// middle and dithered (Bayer 4x4) towards its edge, drawn on the floor under the feet of heroes and enemies so they
    /// stand on it instead of floating over it. One art pixel is <c>scale</c> screen pixels, like the sprite above it.
    /// The dungeon floors are almost black where the figures stand, so a shadow alone barely shows: heroes get a faint cold
    /// floor light under it first (enemies already have their coloured floor glow), a lit floor with a dark spot.
    /// </summary>
    public static class GroundShadow
    {
        private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
        /// <summary>Alpha of the three tones, from the dithered rim to the core.</summary>
        public static readonly int[] Tones = { 0, 110, 172, 228 };
        public static readonly Rgba Ink = new Rgba(10, 6, 18);
        /// <summary>The faint moonlight-cold floor light drawn under a hero's shadow.</summary>
        public static readonly Rgba FloorLight = new Rgba(32, 40, 62);
        public const int MinW = 6, MaxW = 256;

        /// <summary>Height of a shadow <paramref name="w"/> art pixels wide: a quarter of it, flatter for big creatures.</summary>
        public static int HeightFor(int w) => Math.Max(3, Math.Min(16, (int)Math.Round(w * 0.28)));

        /// <summary>The shadow <paramref name="w"/> art pixels wide (cached per size).</summary>
        public static Sprite Sprite(Art art, int w)
        {
            w = Math.Max(MinW, Math.Min(MaxW, w));
            var h = HeightFor(w);
            return new Sprite(art.Procedural("ground_shadow_" + w + "x" + h, w, h, () =>
            {
                var px = new Rgba[w * h];
                for (var y = 0; y < h; y++)
                    for (var x = 0; x < w; x++)
                    {
                        double dx = (x + 0.5) / w * 2 - 1, dy = (y + 0.5) / h * 2 - 1, d = Math.Sqrt(dx * dx + dy * dy);
                        if (d > 1) continue;
                        var level = (int)Math.Floor(3.2 * Math.Pow(1 - d, 0.8) + (Bayer[y % 4, x % 4] + 0.5) / 16.0);
                        level = Math.Max(0, Math.Min(Tones.Length - 1, level));
                        if (level > 0) px[y * w + x] = new Rgba(Ink.R, Ink.G, Ink.B, Tones[level]);
                    }
                return px;
            }));
        }

        /// <summary>Draws a shadow <paramref name="width"/> screen pixels wide centred on (<paramref name="x"/>,
        /// <paramref name="groundY"/>), the line the soles stand on; with <paramref name="light"/>, a soft floor light under it
        /// first (additive, about twice its size).</summary>
        public static void Draw(Canvas canvas, Art art, float x, float groundY, float width, int scale, float alpha = 1f, Rgba? light = null)
        {
            if (!(alpha > 0.01f) || !(width > 0) || float.IsNaN(x) || float.IsNaN(groundY)) return;
            scale = Math.Max(1, scale);
            var s = Sprite(art, (int)Math.Round(width / scale));
            float w = s.W * scale, h = s.H * scale;
            if (light.HasValue)
            {
                var k = Math.Min(1f, alpha); var c = light.Value;
                float lw = w * 1.9f, lh = h * 2.6f;
                canvas.BlitRect(Glows.Soft(art), new RectF(x - lw / 2f, groundY - lh / 2f, lw, lh), new Rgba((int)(c.R * k), (int)(c.G * k), (int)(c.B * k)), Blend.Add);
            }
            var px = (float)Math.Round(x - w / 2f);
            var py = (float)Math.Round(groundY - h / 2f);
            canvas.Blit(s, px, py, scale, alpha >= 0.999f ? Rgba.White : Rgba.White.WithAlpha((int)(255 * Math.Min(1f, alpha))));
        }
    }
}
