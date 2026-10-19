using System;
using System.Collections.Generic;

namespace LastWish.Presentation
{
    /// <summary>infrastructure/colors.py</summary>
    public static class Colors
    {
        public static readonly Rgba BgDark = new Rgba(12, 12, 20), BgPanel = new Rgba(22, 22, 36), BgCardArea = new Rgba(8, 10, 16), BgOverlay = new Rgba(0, 0, 0, 160);
        public static readonly Rgba PlayerBody = new Rgba(50, 80, 160), PlayerAccent = new Rgba(90, 130, 220), EnemyBody = new Rgba(130, 35, 35), EnemyAccent = new Rgba(190, 65, 65);
        public static readonly Rgba HpFill = new Rgba(55, 175, 55), HpLow = new Rgba(200, 80, 20), HpEmpty = new Rgba(70, 18, 18), HpText = new Rgba(220, 220, 220);
        public static readonly Rgba BlockColor = new Rgba(75, 145, 210), BlockBg = new Rgba(20, 50, 90);
        public static readonly Rgba CardAttack = new Rgba(155, 38, 38), CardAttackDark = new Rgba(85, 15, 15), CardSkill = new Rgba(38, 125, 55), CardSkillDark = new Rgba(15, 70, 28);
        public static readonly Rgba CardPower = new Rgba(45, 55, 165), CardPowerDark = new Rgba(18, 22, 100), CardSelected = new Rgba(235, 210, 55), CardHover = new Rgba(200, 185, 110), CardBorder = new Rgba(160, 145, 95);
        public static readonly Rgba CardBroken = new Rgba(175, 80, 210), CardBrokenDark = new Rgba(80, 25, 105);
        public static readonly Rgba ManaFill = new Rgba(55, 155, 235), ManaEmpty = new Rgba(25, 55, 95), ManaOrbRing = new Rgba(90, 185, 255), ManaText = new Rgba(195, 225, 255);
        public static readonly Rgba IntentAttack = new Rgba(210, 55, 55), IntentBlock = new Rgba(55, 115, 210), IntentBuff = new Rgba(55, 200, 95), IntentDebuff = new Rgba(175, 55, 175), IntentUnknown = new Rgba(130, 130, 130);
        public static readonly Rgba BuffColor = new Rgba(55, 200, 120), DebuffColor = new Rgba(200, 90, 200);
        public static readonly Rgba TextPrimary = new Rgba(220, 215, 200), TextSecondary = new Rgba(155, 150, 135), TextAccent = new Rgba(215, 180, 75);
        public static readonly Rgba TextDamage = new Rgba(255, 120, 80), TextBlock = new Rgba(100, 175, 255), BorderDim = new Rgba(55, 50, 45), BorderBright = new Rgba(115, 105, 85);
        public static readonly Rgba RelicBg = new Rgba(38, 32, 52), RelicBorder = new Rgba(90, 75, 115), EndTurnBg = new Rgba(165, 115, 25), EndTurnHover = new Rgba(200, 150, 40), EndTurnText = new Rgba(255, 245, 195), PanelBorder = new Rgba(45, 40, 65);
        public static readonly Rgba Cream = new Rgba(250, 236, 206), Ink = new Rgba(14, 10, 18);
    }

    /// <summary>ui/text_fit.py</summary>
    public static class TextFit
    {
        public const string Ellipsis = "…";

        public static List<string> Wrap(FontFace font, string text, int maxW)
        {
            maxW = Math.Max(1, maxW);
            var lines = new List<string>();
            foreach (var paragraph in (text ?? "").Split('\n'))
            {
                var line = "";
                foreach (var word in paragraph.Split(' '))
                {
                    var trial = line.Length > 0 ? line + " " + word : word;
                    if (font.Width(trial) <= maxW) { line = trial; continue; }
                    if (line.Length > 0) lines.Add(line);
                    line = font.Width(word) <= maxW ? word : Fit(font, word, maxW);
                }
                lines.Add(line);
            }
            return lines;
        }

        public static string Fit(FontFace font, string text, int maxW)
        {
            text = text ?? "";
            if (font.Width(text) <= maxW) return text;
            while (text.Length > 0 && font.Width(text + Ellipsis) > maxW) text = text.Substring(0, text.Length - 1);
            return text.Length > 0 ? text.TrimEnd() + Ellipsis : "";
        }

        public static List<string> WrapLimited(FontFace font, string text, int maxW, int? maxLines)
        {
            var lines = Wrap(font, text, maxW);
            if (maxLines.HasValue && lines.Count > maxLines.Value)
            {
                lines = lines.GetRange(0, maxLines.Value);
                lines[lines.Count - 1] = Fit(font, lines[lines.Count - 1] + Ellipsis, maxW);
            }
            return lines;
        }
    }

    /// <summary>Text helpers: <c>outlined()</c> surfaces and rect placement like pygame get_rect(...).</summary>
    public static class Txt
    {
        private static readonly (int, int)[] OutlineOffsets = { (0, 1), (2, 1), (1, 0), (1, 2), (0, 0), (2, 2), (0, 2), (2, 0) };

        /// <summary>Size of the <c>outlined(font, text)</c> surface (text + 2 px).</summary>
        public static (int w, int h) OutlinedSize(FontFace font, string text) => (font.Width(text) + 2, font.Height + 2);

        /// <summary>Draw <c>outlined(font, text, color, outline)</c> with its top-left at (x, y).</summary>
        public static void Outlined(Canvas canvas, FontFace font, string text, float x, float y, Rgba color, Rgba? outline = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (canvas.Transform.IsAxisAligned) { x = (float)Math.Floor(x); y = (float)Math.Floor(y); }
            var edge = outline ?? Colors.Ink;
            foreach (var (dx, dy) in OutlineOffsets) font.Draw(canvas, text, x + dx, y + dy, edge);
            font.Draw(canvas, text, x + 1, y + 1, color);
        }

        private static readonly (int, int)[] CrossOffsets = { (0, 1), (2, 1), (1, 0), (1, 2) };
        /// <summary>card_widget._outlined: 4-neighbour outline (10, 8, 12) by default.</summary>
        public static void OutlinedCross(Canvas canvas, FontFace font, string text, float x, float y, Rgba color, Rgba? outline = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (canvas.Transform.IsAxisAligned) { x = (float)Math.Floor(x); y = (float)Math.Floor(y); }
            var edge = outline ?? new Rgba(10, 8, 12);
            foreach (var (dx, dy) in CrossOffsets) font.Draw(canvas, text, x + dx, y + dy, edge);
            font.Draw(canvas, text, x + 1, y + 1, color);
        }

        public static RectF Place(float w, float h, Anchor anchor, float ax, float ay)
        {
            // pygame integer rect placement (x = cx - w // 2).
            int iw = (int)w, ih = (int)h;
            float x, y;
            switch (anchor)
            {
                case Anchor.Center: x = (float)Math.Floor(ax) - iw / 2; y = (float)Math.Floor(ay) - ih / 2; break;
                case Anchor.MidLeft: x = ax; y = (float)Math.Floor(ay) - ih / 2; break;
                case Anchor.MidRight: x = ax - iw; y = (float)Math.Floor(ay) - ih / 2; break;
                case Anchor.MidTop: x = (float)Math.Floor(ax) - iw / 2; y = ay; break;
                case Anchor.MidBottom: x = (float)Math.Floor(ax) - iw / 2; y = ay - ih; break;
                case Anchor.TopRight: x = ax - iw; y = ay; break;
                case Anchor.BottomLeft: x = ax; y = ay - ih; break;
                case Anchor.BottomRight: x = ax - iw; y = ay - ih; break;
                default: x = ax; y = ay; break;
            }
            return new RectF(x, y, iw, ih);
        }

        public static RectF OutlinedAt(Canvas canvas, FontFace font, string text, Anchor anchor, float ax, float ay, Rgba color, Rgba? outline = null)
        {
            var (w, h) = OutlinedSize(font, text);
            var r = Place(w, h, anchor, ax, ay);
            Outlined(canvas, font, text, r.X, r.Y, color, outline);
            return r;
        }

        public static RectF DrawAt(Canvas canvas, FontFace font, string text, Anchor anchor, float ax, float ay, Rgba color)
        {
            var r = Place(font.Width(text), font.Height, anchor, ax, ay);
            font.Draw(canvas, text, r.X, r.Y, color);
            return r;
        }
    }

    public enum Anchor { TopLeft, Center, MidLeft, MidRight, MidTop, MidBottom, TopRight, BottomLeft, BottomRight }

    public enum ButtonState { Idle, Hover, Press, Off }

    /// <summary>ui/pixel_ui.py: kit widgets.</summary>
    public static class PixelUi
    {
        public static Rgba Cream => Colors.Cream;
        public static Rgba Ink => Colors.Ink;

        public static void Panel(Canvas canvas, Art art, RectF rect, int alpha = 255)
        {
            if (!art.HasKitPiece("panel"))
            {
                canvas.Rect(rect, new Rgba(22, 20, 32), 0, 6); canvas.Rect(rect, new Rgba(150, 120, 70), 2, 6); return;
            }
            art.DrawKitSlice(canvas, "panel", rect, 2, Rgba.White.WithAlpha(alpha));
        }

        public static void Topbar(Canvas canvas, Art art, int height = 68)
        {
            var tile = art.KitPiece("topbar");
            if (!tile.Valid) { canvas.Fill(new RectF(0, 0, Canvas.Width, height), new Rgba(22, 20, 32)); return; }
            var w = tile.W * 2; var h = tile.H * 2;
            for (var x = 0; x < Canvas.Width; x += w) canvas.Blit(tile, x, height - h, 2);
        }

        public static void Trim(Canvas canvas, Art art, float y, bool flip = false)
        {
            var tile = art.KitPiece("trim");
            if (!tile.Valid) { canvas.Line(0, y, Canvas.Width, y, new Rgba(180, 140, 60), 2); return; }
            var w = tile.W * 2; var h = tile.H * 2;
            for (var x = 0; x < Canvas.Width; x += w)
                canvas.BlitFlipped(tile, new RectF(x, y - h / 2, w, h), false, flip, Rgba.White);
        }

        public static RectF Ribbon(Canvas canvas, Art art, float cx, float cy, string text, int size = 15)
        {
            var font = art.Fonts.Get(size);
            var (lw, lh) = Txt.OutlinedSize(font, text);
            var w = lw + 44;
            var rect = RectF.CenteredInt(cx, cy, w, 32);
            if (art.HasKitPiece("ribbon")) art.DrawKitSlice(canvas, "ribbon", rect);
            else canvas.Rect(rect, new Rgba(150, 30, 40), 0, 4);
            Txt.OutlinedAt(canvas, font, text, Anchor.Center, rect.CenterX, rect.CenterY - 1, new Rgba(255, 236, 200));
            return rect;
        }

        public static RectF Keycap(Canvas canvas, Art art, float cx, float cy, string text)
        {
            var font = art.Fonts.Get(11);
            var w = Math.Max(18, font.Width(text) + 8);
            var rect = RectF.CenteredInt(cx, cy, w, 18);
            canvas.Rect(rect.Inflate(2, 2), new Rgba(18, 12, 14), 0, 4);
            canvas.Rect(rect.Move(0, 1), new Rgba(150, 136, 116), 0, 3);
            canvas.Rect(new RectF(rect.X, rect.Y, rect.W, rect.H - 2), new Rgba(232, 220, 196), 0, 3);
            Txt.DrawAt(canvas, font, text, Anchor.Center, rect.CenterX, rect.CenterY - 1, new Rgba(40, 30, 24));
            return rect;
        }

        public static int KeycapWidth(Art art, string key) => key == null ? 0 : Math.Max(18, art.Fonts.Get(11).Width(key) + 8) + 6;

        private static void GlowRect(Canvas canvas, RectF shown, Rgba color)
        {
            // _glow_rect: concentric rects with alpha, blitted with BLEND_RGB_ADD (pygame ignores the alpha),
            // so what shows is the outermost rounded rect in full colour.
            const int pad = 14;
            var r = new RectF(0, 0, shown.W + pad * 2, shown.H + pad * 2);
            r = RectF.CenteredInt(shown.CenterX, shown.CenterY, (int)r.W, (int)r.H);
            canvas.Rect(r, color, 0, pad + 4, Blend.Add);
        }

        private static void Sheen(Canvas canvas, RectF rect, float phase, int strength)
        {
            var inner = rect.Inflate(-10, -10);
            if (inner.W <= 0 || inner.H <= 0) return;
            var x = (int)(-inner.H + phase * (inner.W + inner.H * 2));
            var col = new Rgba(strength, strength, Math.Max(0, strength - 10));
            canvas.PushClip(inner);
            canvas.Polygon(new[] { (inner.X + x, inner.Bottom), (inner.X + x + inner.H * 0.6f, inner.Y), (inner.X + x + inner.H * 0.6f + 10, inner.Y), (inner.X + x + 10, inner.Bottom) }, col, 0, Blend.Add);
            canvas.PopClip();
        }

        /// <summary>draw_button: bronze/gold kit button with icon, outlined label and key cap.</summary>
        public static RectF Button(Canvas canvas, Art art, RectF rect, string label, string style = "bronze", ButtonState state = ButtonState.Idle,
            string icon = null, string key = null, float t = 0f, float glow = 0f, int size = 15)
        {
            var shown = rect.Move(0, state == ButtonState.Press ? 2 : state == ButtonState.Hover ? -1 : 0);
            if (glow > 0 && state != ButtonState.Off) GlowRect(canvas, shown, new Rgba(255, 200, 90));
            var stateName = state == ButtonState.Press ? "press" : state == ButtonState.Hover ? "hover" : state == ButtonState.Off ? "off" : "idle";
            var piece = "btn_" + style + "_" + stateName;
            if (art.HasKitPiece(piece)) art.DrawKitSlice(canvas, piece, shown);
            else
            {
                var col = state == ButtonState.Off ? new Rgba(90, 90, 96) : style == "gold" ? new Rgba(150, 110, 40) : new Rgba(110, 50, 50);
                canvas.Rect(shown, col, 0, 6); canvas.Rect(shown, new Rgba(230, 190, 100), 2, 6);
            }
            if (state == ButtonState.Hover || style == "gold" && state == ButtonState.Idle)
            {
                var cycle = state == ButtonState.Hover ? 1.4f : 3.6f;
                var phase = (float)MathX.PyMod(t, cycle) / cycle;
                if (state == ButtonState.Hover || phase < 0.4f)
                    Sheen(canvas, shown, phase / (state == ButtonState.Hover ? 1f : 0.4f), state == ButtonState.Hover ? 70 : 45);
            }
            var ink = state == ButtonState.Off ? new Rgba(176, 172, 168) : Colors.Cream;
            var font = art.Fonts.Get(size);
            var hasText = !string.IsNullOrEmpty(label);
            var textW = hasText ? Txt.OutlinedSize(font, label).w : 0;
            var iconS = icon != null ? art.Icon(icon) : default;
            var keyW = KeycapWidth(art, key);
            var contentW = (iconS.Valid ? iconS.W * 2 + 4 : 0) + textW + keyW;
            var x = shown.CenterX - contentW / 2;
            x = (float)Math.Floor(shown.CenterX) - contentW / 2;
            var cy = shown.CenterY - (state != ButtonState.Press ? 1 : 0);
            if (iconS.Valid)
            {
                var tint = state == ButtonState.Off ? new Rgba(140, 140, 140) : Rgba.White;
                var r = Txt.Place(iconS.W * 2, iconS.H * 2, Anchor.MidLeft, x, cy);
                canvas.BlitRect(iconS, r, tint);
                x += iconS.W * 2 + 4;
            }
            if (hasText) { Txt.OutlinedAt(canvas, font, label, Anchor.MidLeft, x, cy, ink); x += textW; }
            if (key != null) Keycap(canvas, art, x + keyW / 2 + 3, cy, key);
            return rect;
        }

        public static ButtonState StateOf(RectF rect, (float x, float y) mouse, bool pressed, bool enabled = true)
        {
            if (!enabled) return ButtonState.Off;
            if (rect.Contains(mouse)) return pressed ? ButtonState.Press : ButtonState.Hover;
            return ButtonState.Idle;
        }

        /// <summary>_glow_circle(radius, color): concentric alpha circles blitted additively (only the outer disc shows).</summary>
        public static void GlowCircle(Canvas canvas, float cx, float cy, int radius, Rgba color)
        {
            canvas.Circle((int)cx, (int)cy, radius, color, 0, Blend.Add);
        }
    }
}
