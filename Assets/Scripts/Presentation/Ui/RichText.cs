using System;
using System.Collections.Generic;
using System.Text;

namespace LastWish.Presentation
{
    /// <summary>One laid-out line of colour-coded text: runs with their x offsets.</summary>
    public sealed class RichLine
    {
        public readonly List<(string text, Rgba color, int x)> Runs = new List<(string, Rgba, int)>();
        public int Width;
        public int Height;
        public bool Shadow;
        public FontFace Font;

        public void Draw(Canvas canvas, float x, float y, float alpha = 1f)
        {
            if (alpha < 1f) canvas.PushAlpha(alpha);
            foreach (var (text, color, rx) in Runs)
            {
                if (Shadow) Font.Draw(canvas, text, x + rx + 1, y + 1, new Rgba(8, 6, 12));
                Font.Draw(canvas, text, x + rx, y, color);
            }
            if (alpha < 1f) canvas.PopAlpha();
        }
    }

    /// <summary>ui/rich_text.py: card-game colour coding of rule text, word-wrapped (layouts cached).</summary>
    public static class RichText
    {
        private static readonly Dictionary<(string, FontFace, int, Rgba, bool), RichLine[]> Cache = new Dictionary<(string, FontFace, int, Rgba, bool), RichLine[]>();
        private const int CacheMax = 2048;

        public static List<(string text, Rgba color)> Spans(Glossary g, string text, Rgba baseColor)
        {
            var colors = new Rgba?[text.Length];
            void Apply(System.Text.RegularExpressions.Regex pattern, Rgba color)
            {
                foreach (System.Text.RegularExpressions.Match m in pattern.Matches(text))
                {
                    var a = m.Index; var b = m.Index + m.Length; var taken = false;
                    for (var i = a; i < b; i++) if (colors[i].HasValue) { taken = true; break; }
                    if (taken) continue;
                    for (var i = a; i < b; i++) colors[i] = color;
                }
            }
            foreach (var (p, c) in g.RichRules) Apply(p, c);
            foreach (var (p, c) in g.HighlightWords) Apply(p, c);
            var output = new List<(string, Rgba)>();
            var sb = new StringBuilder(); Rgba? current = null;
            for (var i = 0; i < text.Length; i++)
            {
                var col = colors[i] ?? baseColor;
                if (current.HasValue && !current.Value.Equals(col)) { output.Add((sb.ToString(), current.Value)); sb.Clear(); }
                current = col; sb.Append(text[i]);
            }
            if (sb.Length > 0) output.Add((sb.ToString(), current.Value));
            return output;
        }

        private static List<List<(string text, Rgba color)>> WordRuns(Glossary g, string text, Rgba baseColor)
        {
            var words = new List<List<(string, Rgba)>> { new List<(string, Rgba)>() };
            foreach (var (piece, col) in Spans(g, text, baseColor))
            {
                var parts = piece.Split(' ');
                for (var k = 0; k < parts.Length; k++)
                {
                    if (k > 0) words.Add(new List<(string, Rgba)>());
                    if (parts[k].Length > 0) words[words.Count - 1].Add((parts[k], col));
                }
            }
            words.RemoveAll(w => w.Count == 0);
            return words;
        }

        /// <summary>render_lines: word-wrapped colour-coded lines (one per entry).</summary>
        public static RichLine[] Lines(Glossary g, string text, FontFace font, int maxW, Rgba? baseColor = null, bool shadow = false)
        {
            var color = baseColor ?? g.Body;
            maxW = Math.Max(8, maxW);
            var key = (text ?? "", font, maxW, color, shadow);
            if (Cache.TryGetValue(key, out var hit)) return hit;
            var space = font.Width(" ");
            var height = font.Height + (shadow ? 1 : 0);
            var lines = new List<RichLine>();
            RichLine current = null; var width = 0;
            foreach (var pieces in WordRuns(g, text ?? "", color))
            {
                var wordW = 0; foreach (var (t, _) in pieces) wordW += font.Width(t);
                var surfW = wordW + (shadow ? 1 : 0);
                var extra = surfW + (current != null && current.Runs.Count > 0 ? space : 0);
                if (current != null && current.Runs.Count > 0 && width + extra > maxW)
                {
                    current.Width = width; lines.Add(current); current = null; width = 0; extra = surfW;
                }
                if (current == null) current = new RichLine { Height = height, Shadow = shadow, Font = font };
                var x = width + (current.Runs.Count > 0 ? space : 0);
                foreach (var (t, c) in pieces) { current.Runs.Add((t, c, x)); x += font.Width(t); }
                width += extra;
            }
            if (current != null) { current.Width = width; lines.Add(current); }
            if (lines.Count == 0) lines.Add(new RichLine { Height = height, Width = 1, Shadow = shadow, Font = font });
            var result = lines.ToArray();
            if (Cache.Count > CacheMax) Cache.Clear();
            Cache[key] = result;
            return result;
        }

        /// <summary>render_line: one colour-coded line, never wrapped.</summary>
        public static RichLine Line(Glossary g, string text, FontFace font, Rgba? baseColor = null, bool shadow = false) => Lines(g, text, font, 1 << 20, baseColor, shadow)[0];
    }
}
