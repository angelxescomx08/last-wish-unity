using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>One offered relic's iron card: where everything goes (layout tests check that every relic fits).</summary>
    public sealed class RelicCardView
    {
        public int Index; public RectF Rect, Niche, Name, Text; public int NameSize; public bool NameBold; public string Title;
        public List<(string text, Rgba color, int gem, RectF rect)> Tags = new List<(string, Rgba, int, RectF)>();
        public List<RichLine> Lines = new List<RichLine>(); public List<string> Plain = new List<string>();
        public int LineCount => Lines.Count + Plain.Count;
    }

    /// <summary>How one relic card is shown this frame (alpha, slide, hover / selection, whether its relic sits in the niche).</summary>
    public struct RelicCardLook
    {
        public float Alpha, Dy, Lift, Time;
        public bool Selected, Hovered, Dimmed, Many, Arrow, WithRelic;
    }

    /// <summary>
    /// The relic cards shared by the treasure room and the victory rewards (elites and bosses): an iron card with the relic
    /// floating in a niche lit in its rarity colour, its name (one size for the whole row), rarity / chroma / class tags in one
    /// row and its colour-coded rule text, every card of a row as tall as the longest text. Golden and diamond relics get a
    /// halo and a pulsing frame with stars on the frame (never over the text). Plus the little pixel pieces both screens use.
    /// </summary>
    public static class RelicCards
    {
        public const int Inset = 16, NicheH = 112, NameCy = 146, TagsY = 162, TagH = 24, RuleY = 198, TextY = 208, Line = 20, MinLines = 3, IconSize = 96;

        public static int CardWidth(int count) => count <= 1 ? 360 : count == 2 ? 330 : count == 3 ? 300 : 262;
        public static int CardGap(int count) => count == 2 ? 40 : count == 3 ? 28 : 18;

        private static readonly Rgba Ink = Colors.Ink, Cream = Colors.Cream;

        /// <summary>"Dorada ×2" / "Diamante ×3".</summary>
        public static string ChromaTag(ChromaKind chroma) => ChromaFx.Style(chroma).Name + " ×" + ((int)chroma + 1);

        public static Rgba[] Palette(int rarity) { var c = CardWidget.RarityColor(rarity); return new[] { Rgba.White, Glows.Scaled(c, 1.3), c }; }

        private static int TagWidth(Art art, string text, int gem) => PixelUi.InkTextWidth(art, text, 13) + 16 + (gem > 0 ? 12 : 0);

        /// <summary>Card height for <paramref name="lines"/> lines of rule text (at least <see cref="MinLines"/>).</summary>
        public static int Height(int lines) => TextY + Math.Max(MinLines, lines) * Line + 8;

        /// <summary>Lay out a centred row of relic cards whose tops are at <paramref name="top"/>.</summary>
        public static List<RelicCardView> Build(GameContext game, IReadOnlyList<RelicOffer> relics, float top)
        {
            var art = game.Art; var fonts = art.Fonts;
            var cards = new List<RelicCardView>();
            var n = relics.Count; var w = CardWidth(n); var gap = CardGap(n);
            var total = n * w + Math.Max(0, n - 1) * gap; var x0 = (float)Math.Floor((Canvas.Width - total) / 4f) * 2;
            var font = fonts.Get(13);
            var nameSize = 13; var nameBold = false;
            foreach (var (size, bold) in new[] { (20, true), (16, true), (16, false), (13, false) })
                if (relics.All(o => PixelUi.InkTextWidth(art, o.Definition.Name, size, bold) <= w - 2 * Inset)) { nameSize = size; nameBold = bold; break; }
            for (var i = 0; i < n; i++)
            {
                var offer = relics[i]; var def = offer.Definition;
                var v = new RelicCardView { Index = i, Title = def.Name, Rect = new RectF(x0 + i * (w + gap), top, w, 0) };
                var r = v.Rect; var inner = w - 2 * Inset;
                v.Niche = new RectF(r.X + Inset, r.Y + Inset, inner, NicheH);
                // the name: one size for the whole row (the chunky 20 when every name fits, else bold 16)
                v.NameSize = nameSize; v.NameBold = nameBold;
                var nf = v.NameBold ? fonts.Bold(v.NameSize) : fonts.Get(v.NameSize);
                v.Name = Txt.Place(PixelUi.InkTextWidth(art, def.Name, v.NameSize, v.NameBold), nf.Height + 2 * nf.Scale, Anchor.Center, r.CenterX, r.Y + NameCy);
                // tags: rarity, chroma, class — centred in one row; long variants shrink, the class tag goes first if needed
                var rarity = Math.Max(1, Math.Min(5, def.Rarity));
                var tags = new List<(string text, Rgba color, int gem)> { (RunContent.RarityLabel(rarity), CardWidget.RarityColor(rarity), rarity) };
                if (offer.Chroma != ChromaKind.None) tags.Add((ChromaTag(offer.Chroma), ChromaFx.Style(offer.Chroma).Main, 0));
                if (!string.IsNullOrEmpty(def.ClassId) && def.ClassId != "neutral") tags.Add(("Solo " + RunContent.ClassLabel(def.ClassId), new Rgba(150, 200, 255), 0));
                int RowW() => tags.Sum(tg => TagWidth(art, tg.text, tg.gem)) + 6 * Math.Max(0, tags.Count - 1);
                if (RowW() > inner && offer.Chroma != ChromaKind.None) tags[1] = (ChromaFx.Style(offer.Chroma).Name, tags[1].color, 0);
                if (RowW() > inner && tags.Count > 2) tags[2] = (RunContent.ClassLabel(def.ClassId), tags[2].color, 0);
                if (RowW() > inner && tags.Count > 2) tags.RemoveAt(2);
                var tx = (float)Math.Floor((r.CenterX - RowW() / 2f) / 2) * 2;
                foreach (var (text, color, gem) in tags) { var tw = TagWidth(art, text, gem); v.Tags.Add((text, color, gem, new RectF(tx, r.Y + TagsY, tw, TagH))); tx += tw + 6; }
                // rule text, colour-coded, centred
                v.Text = new RectF(r.X + Inset, r.Y + TextY, inner, 0);
                if (game.Data != null) v.Lines = RichText.Lines(game.Data.Glossary, def.Description, font, inner, Cream).ToList();
                else v.Plain = TextFit.Wrap(font, def.Description, inner);
                cards.Add(v);
            }
            // every card as tall as the longest text (at least three lines)
            var lines = Math.Max(MinLines, cards.Count == 0 ? 0 : cards.Max(c => c.LineCount));
            var h = Height(lines);
            foreach (var v in cards) { v.Rect = new RectF(v.Rect.X, v.Rect.Y, v.Rect.W, h); v.Text = new RectF(v.Text.X, v.Text.Y, v.Text.W, lines * Line); }
            return cards;
        }

        /// <summary>Lines of rule text the tallest card of this row needs (to place the row before building it).</summary>
        public static int Lines(GameContext game, IReadOnlyList<RelicOffer> relics)
        {
            if (relics.Count == 0) return MinLines;
            var inner = CardWidth(relics.Count) - 2 * Inset; var font = game.Art.Fonts.Get(13);
            return Math.Max(MinLines, relics.Max(o => game.Data != null
                ? RichText.Lines(game.Data.Glossary, o.Definition.Description, font, inner, Cream).Count()
                : TextFit.Wrap(font, o.Definition.Description, inner).Count));
        }

        /// <summary>The tooltip of an offered relic, only when it adds something the card does not say (glossary terms, a
        /// class); an offered relic has no "Estado" line.</summary>
        public static TooltipContent OfferTooltip(GameContext game, RelicOffer offer)
        {
            if (offer == null || game.Data == null) return null;
            var content = Tooltip.Relic(game, offer.Definition, offer.Chroma);
            var state = content.Lines.FindIndex(l => l.StartsWith("Estado:", StringComparison.Ordinal));
            if (state >= 0) { content.Lines.RemoveAt(state); if (state > 0 && content.Lines[state - 1].Length == 0) content.Lines.RemoveAt(state - 1); }
            var classRelic = !string.IsNullOrEmpty(offer.Definition.ClassId) && offer.Definition.ClassId != "neutral";
            return content.Panels.Count > 0 || classRelic ? content : null;
        }

        // ------------------------------------------------------------------ drawing
        public static void Draw(Canvas canvas, GameContext game, RelicCardView v, RelicOffer offer, in RelicCardLook look)
        {
            var a = look.Alpha; if (a <= 0.01f) return;
            var art = game.Art; var i = v.Index; var chroma = offer.Chroma; var time = look.Time;
            var off = look.Dy + look.Lift;
            var r = v.Rect.Move(0, off);
            RectF M(RectF q) => q.Move(0, off);
            canvas.PushAlpha(a);
            if (chroma != ChromaKind.None) ChromaFx.DrawHalo(canvas, art, r.CenterX, r.CenterY, r.W, r.H, chroma, game.Time, false, 8);
            if (look.Selected)
            {
                var pulse = 0.5 + 0.5 * Math.Sin(time * 3.0);
                PixelUi.Frame2(canvas, r, Glows.Scaled(new Rgba(255, 222, 140), 0.75 + 0.3 * pulse));
            }
            else if (look.Hovered) PixelUi.Frame2(canvas, r, new Rgba(200, 184, 150));
            PixelUi.Panel(canvas, art, r);
            canvas.Fill(r.Inflate(-16, -16), look.Selected || look.Hovered ? new Rgba(120, 80, 30, 46) : new Rgba(60, 40, 20, 26));
            DrawNiche(canvas, game, M(v.Niche), offer, look.WithRelic, i, time);
            if (look.Many) PixelUi.Keycap(canvas, art, M(v.Niche).X + 18, M(v.Niche).Y + 16, (i + 1).ToString());
            // name and tags
            var nameColor = chroma != ChromaKind.None ? new Rgba(255, 226, 140) : Cream;
            PixelUi.InkText(canvas, art, v.Title, v.NameSize, Anchor.Center, M(v.Name).CenterX, M(v.Name).CenterY - 1, nameColor, false, v.NameBold);
            foreach (var (text, color, gem, rect) in v.Tags)
            {
                var tr = M(rect);
                PixelUi.PixelBox(canvas, tr, Glows.Scaled(color, 0.2).WithAlpha(235), Glows.Scaled(color, 0.82), Glows.Scaled(color, 0.34).WithAlpha(235));
                var tx = tr.X + 8;
                if (gem > 0) { Gem(canvas, tx + 3, tr.CenterY, color); tx += 12; }
                PixelUi.InkText(canvas, art, text, 13, Anchor.MidLeft, tx, tr.CenterY - 1, Glows.Scaled(color, 1.32));
            }
            // rule text, colour-coded and centred
            DitherRule(canvas, r.X + Inset, r.Y + RuleY, r.W - 2 * Inset, new Rgba(110, 88, 60));
            var y = M(v.Text).Y; var cx = r.CenterX;
            foreach (var line in v.Lines) { line.Draw(canvas, (float)Math.Floor((cx - line.Width / 2f) / 2) * 2, y); y += Line; }
            foreach (var line in v.Plain) { var f = art.Fonts.Get(13); f.Draw(canvas, line, (float)Math.Floor((cx - f.Width(line) / 2f) / 2) * 2, y, Cream); y += Line; }
            if (chroma != ChromaKind.None)
            {
                var st = ChromaFx.Style(chroma); var pulse = 0.5 + 0.5 * Math.Sin(game.Time * 2.2);
                PixelUi.Frame2(canvas, r.Inflate(-8, -8), Glows.Scaled(st.Main, 0.75 + 0.3 * pulse));
                FrameTwinkles(canvas, r, st.Bright, game.Time + i * 1.3f);
            }
            // the cards not picked step back; a gold arrow bobs over the chosen one
            if (look.Dimmed) canvas.Fill(r.Inflate(-8, -8), new Rgba(8, 6, 12, look.Hovered ? 40 : 96));
            if (look.Arrow) Chevron(canvas, r.CenterX, r.Y - 4 + (float)Math.Round(Math.Sin(time * 4.0) * 1.2) * 2);
            canvas.PopAlpha();
        }

        private static readonly (float u, float v, float ph)[] NicheStars = { (0.16f, 0.24f, 0f), (0.84f, 0.3f, 2.2f), (0.78f, 0.78f, 4.1f), (0.22f, 0.72f, 1.3f) };

        /// <summary>The niche: dark box, rarity-coloured light, a pool of light under the relic, the relic bobbing, stars.</summary>
        public static void DrawNiche(Canvas canvas, GameContext game, RectF n, RelicOffer offer, bool withRelic, int i, float time)
        {
            var art = game.Art;
            var rarity = Math.Max(1, Math.Min(5, offer.Definition.Rarity)); var rc = CardWidget.RarityColor(rarity);
            PixelUi.PixelBox(canvas, n, new Rgba(14, 10, 18), Ink, new Rgba(34, 26, 40));
            canvas.PushClip(n.Inflate(-4, -4));
            Glows.Draw(canvas, art, n.CenterX, n.CenterY + 6, 70, Glows.Scaled(rc, withRelic ? 0.34 + 0.08 * Math.Sin(time * 2.0 + i) : 0.12));
            // a soft pool of light on the floor of the niche
            for (var row = 0; row < 3; row++)
            {
                var w = 64 - row * 16; var yy = n.Bottom - 12 + row * 2;
                canvas.Fill(new RectF((float)Math.Floor((n.CenterX - w / 2f) / 2) * 2, yy, w, 2), Glows.Scaled(rc, 0.22 + row * 0.1).WithAlpha(withRelic ? 150 : 60));
            }
            if (withRelic)
            {
                var bob = (float)Math.Round(Math.Sin(time * 1.8 + i * 1.7) * 1.4) * 2;
                RelicViewer.DrawRelicIcon(canvas, art, offer.Definition.Name, IconSize, n.CenterX, n.CenterY - 6 + bob);
                if (offer.Chroma != ChromaKind.None) ChromaFx.Sheen(canvas, n.Inflate(-4, -4), offer.Chroma, game.Time);
                foreach (var (u, v, ph) in NicheStars)
                {
                    var k = Math.Sin(time * (1.6 + rarity * 0.15) + ph + i * 0.9); if (k < 0.3) continue;
                    var arm = 2 * (1 + (int)(k * (rarity >= 4 ? 2.5 : 1.6)));
                    Star(canvas, n.X + 8 + (n.W - 16) * u, n.Y + 8 + (n.H - 16) * v, arm, Glows.Scaled(rc, 0.9 + 0.5 * k));
                }
            }
            canvas.PopClip();
        }

        // ------------------------------------------------------------------ little pieces
        private static readonly (float u, float v, float ph)[] FrameStars = { (0f, 0f, 0f), (1f, 0.3f, 1.7f), (0.32f, 1f, 3.1f), (0f, 0.72f, 4.4f), (0.74f, 0f, 2.3f), (1f, 1f, 5.2f) };

        /// <summary>Four-point stars that come and go on the frame of golden relics (never over the text).</summary>
        public static void FrameTwinkles(Canvas canvas, RectF r, Rgba color, float t)
        {
            foreach (var (u, v, ph) in FrameStars)
            {
                var k = Math.Sin(t * 2.4 + ph); if (k < 0.2) continue;
                Star(canvas, r.X + 1 + (r.W - 4) * u, r.Y + 1 + (r.H - 4) * v, 2 * (1 + (int)(k * 2.5)), color);
            }
        }

        private static readonly string[] Arrow = { "#############", "#yyyyyyyyyyy#", ".#yyyyyyyyy#.", "..#yyyyyyy#..", "...#yyyyy#...", "....#yyy#....", ".....#w#.....", "......#......" };

        /// <summary>A small gold arrow pointing down (2 px grain) with its tip at (cx, tipY).</summary>
        public static void Chevron(Canvas canvas, float cx, float tipY)
        {
            var x0 = (float)Math.Floor(cx / 2) * 2 - 12; var y0 = (float)Math.Floor(tipY / 2) * 2 - (Arrow.Length - 1) * 2;
            for (var row = 0; row < Arrow.Length; row++)
                for (var col = 0; col < Arrow[row].Length; col++)
                {
                    var ch = Arrow[row][col]; if (ch == '.') continue;
                    var c = ch == '#' ? Ink : ch == 'w' ? new Rgba(255, 246, 220) : row == 1 ? new Rgba(255, 230, 150) : new Rgba(232, 176, 70);
                    canvas.Fill(new RectF(x0 + col * 2, y0 + row * 2, 2, 2), c);
                }
        }

        /// <summary>A four-point star on the 2 px grain: white core, arms of <paramref name="arm"/> px.</summary>
        public static void Star(Canvas canvas, float x, float y, int arm, Rgba color)
        {
            var cx = (float)Math.Floor(x / 2) * 2; var cy = (float)Math.Floor(y / 2) * 2;
            canvas.Fill(new RectF(cx - arm, cy, 2 * arm + 2, 2), color); canvas.Fill(new RectF(cx, cy - arm, 2, 2 * arm + 2), color);
            canvas.Fill(new RectF(cx, cy, 2, 2), Rgba.White);
        }

        public static void DitherRule(Canvas canvas, float x, float y, float w, Rgba color)
        {
            for (var px = (int)x; px < x + w - 1; px += 4) canvas.Fill(new RectF(px, (float)Math.Floor(y / 2) * 2, 2, 2), color);
        }

        public static void Gem(Canvas canvas, float cx, float cy, Rgba c)
        {
            float x = (float)Math.Floor(cx / 2) * 2 - 4, y = (float)Math.Floor(cy / 2) * 2 - 4;
            canvas.Fill(new RectF(x + 2, y, 4, 8), Ink); canvas.Fill(new RectF(x, y + 2, 8, 4), Ink);
            canvas.Fill(new RectF(x + 2, y + 2, 4, 4), c); canvas.Fill(new RectF(x + 2, y + 2, 2, 2), Glows.Scaled(c, 1.5));
        }
    }
}
