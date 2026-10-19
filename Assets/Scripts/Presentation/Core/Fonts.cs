using System;
using System.Collections.Generic;

namespace LastWish.Presentation
{
    /// <summary>One pixel font baked by Tools/ExportPixelFonts.py at its grid size (glyphs from SDL_ttf).</summary>
    public sealed class BitmapFont
    {
        public string Key { get; }
        public int Base { get; }
        public int LineHeight { get; }
        public int Ascent { get; }
        public int[] LineSizes { get; }
        public Tex Texture { get; }
        private readonly Dictionary<int, (int x, int y, int w, int h, int advance)> glyphs = new Dictionary<int, (int, int, int, int, int)>();
        private readonly Dictionary<long, int> kerning = new Dictionary<long, int>();

        public BitmapFont(string key, Tex texture, string json)
        {
            Key = key; Texture = texture;
            var meta = Json.Obj(Json.Parse(json));
            Base = Json.Int(meta.Get("base")); LineHeight = Json.Int(meta.Get("height")); Ascent = Json.Int(meta.Get("ascent"));
            LineSizes = Json.Ints(meta.Get("linesizes")) ?? new[] { LineHeight };
            foreach (var pair in Json.Obj(meta.Get("glyphs")))
            {
                var g = Json.Ints(pair.Value);
                glyphs[int.Parse(pair.Key)] = (g[0], g[1], g[2], g[3], g[4]);
            }
            var kern = Json.Obj(meta.Get("kerning"));
            if (kern != null)
                foreach (var pair in kern)
                {
                    var comma = pair.Key.IndexOf(',');
                    var a = int.Parse(pair.Key.Substring(0, comma)); var b = int.Parse(pair.Key.Substring(comma + 1));
                    kerning[(long)a << 32 | (uint)b] = Json.Int(pair.Value);
                }
        }

        public bool Has(int code) => glyphs.ContainsKey(code);
        internal bool TryGlyph(int code, out (int x, int y, int w, int h, int advance) glyph)
        {
            if (glyphs.TryGetValue(code, out glyph)) return true;
            return glyphs.TryGetValue('?', out glyph);
        }
        internal int Kern(int a, int b) => kerning.Count == 0 ? 0 : kerning.TryGetValue((long)a << 32 | (uint)b, out var k) ? k : 0;
    }

    /// <summary>A font at a whole multiple of its grid: what <c>FontRegistry.get(size)</c> returns.</summary>
    public sealed class FontFace
    {
        public BitmapFont Font { get; }
        public int Scale { get; }
        public int Size => Font.Base * Scale;
        public int Height => Font.LineHeight * Scale;
        public int Ascent => Font.Ascent * Scale;
        /// <summary>font.get_linesize() at this size.</summary>
        public int LineSize => Scale <= Font.LineSizes.Length ? Font.LineSizes[Scale - 1] : (int)Math.Round(Font.LineSizes[0] * Scale * 1.1);
        public FontFace(BitmapFont font, int scale) { Font = font; Scale = Math.Max(1, scale); }

        /// <summary>font.size(text)[0].</summary>
        public int Width(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var total = 0; var prev = -1;
            for (var i = 0; i < text.Length; i++)
            {
                int code = text[i];
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length) { code = char.ConvertToUtf32(text[i], text[i + 1]); i++; }
                if (Font.TryGlyph(code, out var g)) total += g.advance;
                if (prev >= 0) total += Font.Kern(prev, code);
                prev = code;
            }
            return total * Scale;
        }
        public int Width(string text, int start, int length) => Width(text.Substring(start, length));
        public (int w, int h) Size2(string text) => (Width(text), Height);

        /// <summary>Draw text with its top-left at (x, y), like blitting font.render(text).</summary>
        public void Draw(Canvas canvas, string text, float x, float y, Rgba color)
        {
            if (string.IsNullOrEmpty(text) || color.A == 0) return;
            if (canvas.Transform.IsAxisAligned) { x = (float)Math.Floor(x); y = (float)Math.Floor(y); }
            var pen = 0; var prev = -1;
            for (var i = 0; i < text.Length; i++)
            {
                int code = text[i];
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length) { code = char.ConvertToUtf32(text[i], text[i + 1]); i++; }
                if (prev >= 0) pen += Font.Kern(prev, code);
                prev = code;
                if (!Font.TryGlyph(code, out var g)) continue;
                if (g.w > 0 && code != ' ')
                    canvas.BlitRect(new Sprite(Font.Texture, g.x, g.y, g.w, g.h), new RectF(x + pen * Scale, y, g.w * Scale, g.h * Scale), color);
                pen += g.advance;
            }
        }

        /// <summary>Draw centred on (cx, cy) like <c>surf.get_rect(center=...)</c>.</summary>
        public void DrawCentered(Canvas canvas, string text, float cx, float cy, Rgba color)
        {
            var w = Width(text); var h = Height;
            Draw(canvas, text, (float)Math.Floor(cx - Math.Floor(w / 2.0)), (float)Math.Floor(cy - Math.Floor(h / 2.0)), color);
        }
        public void DrawMidTop(Canvas canvas, string text, float cx, float top, Rgba color)
        { var w = Width(text); Draw(canvas, text, (float)Math.Floor(cx - Math.Floor(w / 2.0)), top, color); }
        public void DrawRight(Canvas canvas, string text, float right, float top, Rgba color) => Draw(canvas, text, right - Width(text), top, color);
    }

    /// <summary>infrastructure/fonts.FontRegistry: requested sizes snap to the pixel grid.</summary>
    public sealed class FontRegistry
    {
        private readonly BitmapFont lw8, lw16, lw8b, lw16b, tiny;
        private readonly Dictionary<int, FontFace> regular = new Dictionary<int, FontFace>();
        private readonly Dictionary<int, FontFace> bold = new Dictionary<int, FontFace>();
        private readonly Dictionary<int, FontFace> tinyFaces = new Dictionary<int, FontFace>();

        public FontRegistry(BitmapFont lw8, BitmapFont lw16, BitmapFont lw8b, BitmapFont lw16b, BitmapFont tiny)
        { this.lw8 = lw8; this.lw16 = lw16; this.lw8b = lw8b; this.lw16b = lw16b; this.tiny = tiny; }

        /// <summary>(16-px grid?, pixel size) for a requested size (fonts.pixel_size).</summary>
        public static (bool grid16, int px) PixelSize(int size)
        {
            if (size <= 11) return (false, 8);
            if (size <= 17) return (true, 16);
            if (size <= 23) return (false, 16);
            if (size <= 30) return (true, 32);
            if (size <= 38) return (false, 24);
            return (false, 8 * Math.Max(4, (int)Math.Round(size / 10.0, MidpointRounding.ToEven)));
        }

        public FontFace Get(int size)
        {
            size = Math.Max(1, size);
            if (!regular.TryGetValue(size, out var face))
            {
                var (grid16, px) = PixelSize(size);
                var font = grid16 ? lw16 : lw8;
                regular[size] = face = new FontFace(font, px / font.Base);
            }
            return face;
        }

        public FontFace Bold(int size)
        {
            size = Math.Max(1, size);
            if (!bold.TryGetValue(size, out var face))
            {
                var (grid16, px) = PixelSize(size);
                var font = grid16 ? lw16b : lw8b;
                bold[size] = face = new FontFace(font, px / font.Base);
            }
            return face;
        }

        public FontFace Tiny(int size = 8)
        {
            var px = 8 * Math.Max(1, (int)Math.Round(size / 8.0, MidpointRounding.ToEven));
            if (!tinyFaces.TryGetValue(px, out var face)) tinyFaces[px] = face = new FontFace(tiny, px / tiny.Base);
            return face;
        }
    }
}
