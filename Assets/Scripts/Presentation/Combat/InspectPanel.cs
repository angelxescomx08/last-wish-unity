using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LastWish.Presentation
{
    /// <summary>ui/inspect_panel.py: modal, scrollable view of one TooltipContent (right click / long press / I).</summary>
    public sealed class InspectOverlay : IOverlay
    {
        public static readonly IRect PanelRect = new IRect(240, 60, 800, 600);
        private const int Pad = 22, HeaderH = 92, FooterH = 52, DragPx = 6, StepPx = 48;
        private static readonly Regex IconRx = new Regex(@"^\[\[(\w+)\]\]\s*", RegexOptions.Compiled);

        /// <summary>One laid-out piece of the body: a rule line (icon + rich rows) or a panel header.</summary>
        private sealed class Item
        {
            public int Y, H;
            public string Icon; public int Indent;
            public RichLine[] Rows;
            public TooltipPanel Header;
        }

        public TooltipContent Content { get; }
        public bool Dismissed { get; private set; }
        private readonly GameContext game;
        private readonly IRect view;
        private readonly List<Item> body = new List<Item>();
        private readonly int bodyH;
        private float offset, target;
        private readonly IRect closeRect = new IRect(PanelRect.Right - 150, PanelRect.Bottom - 46, 130, 36);
        private readonly Sprite? portrait;
        private (float x, float y) mouse;
        private bool down, dragging;
        private (float y, float off)? press;

        public InspectOverlay(GameContext game, TooltipContent content, Sprite? portrait = null)
        {
            this.game = game; Content = content; this.portrait = portrait;
            view = new IRect(PanelRect.X + Pad, PanelRect.Y + HeaderH, PanelRect.W - 2 * Pad - 14, PanelRect.H - HeaderH - FooterH);
            bodyH = Layout(content, view.W);
        }

        public float Offset => offset;
        public int BodyHeight => bodyH;
        public int MaxOffset => Math.Max(0, bodyH - view.H);
        private float Clamp(float v) => Math.Max(0f, Math.Min(MaxOffset, v));
        public void Scroll(float px) => target = Clamp(target + px);

        private Item LineItem(string line, int width)
        {
            var g = game.Data.Glossary; string icon = null;
            var m = IconRx.Match(line);
            if (m.Success) { icon = game.Art.HasIcon(m.Groups[1].Value) ? m.Groups[1].Value : null; line = line.Substring(m.Length); }
            var dim = line.StartsWith("  ");
            var font = game.Fonts.Get(16);
            var indent = icon != null ? game.Art.Icon(icon).W * 2 + 6 : dim ? 14 : 0;
            var rows = RichText.Lines(g, line.Trim(), font, width - indent, dim ? g.Dim : g.Body);
            if (rows.Length == 0) return null;
            var h = 0; foreach (var r in rows) h += r.Height;
            return new Item { Icon = icon, Indent = indent, Rows = rows, H = Math.Max(h, icon != null ? game.Art.Icon(icon).H * 2 : 0) };
        }

        private int Layout(TooltipContent content, int width)
        {
            var y = 0;
            void Push(Item it, int gap) { if (it == null) return; it.Y = y; body.Add(it); y += it.H + gap; }
            foreach (var line in content.Lines) Push(LineItem(line, width), 4);
            foreach (var panel in content.Panels)
            {
                Push(new Item { Header = panel, H = 32 }, 2);
                foreach (var line in panel.Lines) Push(LineItem(line, width), 3);
                y += 8;
            }
            return Math.Max(1, y);
        }

        public void HandleEvent(in InputEvent e)
        {
            switch (e.Type)
            {
                case EventType.MouseMove:
                    mouse = e.Pos;
                    if (press.HasValue)
                    {
                        var (y0, off0) = press.Value;
                        if (dragging || Math.Abs(e.Y - y0) >= DragPx) { dragging = true; target = offset = Clamp(off0 - (e.Y - y0)); }
                    }
                    break;
                case EventType.Wheel: Scroll(-e.Wheel * StepPx); break;
                case EventType.MouseDown:
                    if (e.Button == 4 || e.Button == 5) Scroll(e.Button == 5 ? StepPx : -StepPx);
                    else if (e.Button == 3) Dismissed = true;
                    else if (e.Button == 1)
                    {
                        down = true;
                        if (closeRect.Contains(e.X, e.Y) || !PanelRect.Contains(e.X, e.Y)) Dismissed = true;
                        else if (view.Contains(e.X, e.Y)) { press = (e.Y, offset); dragging = false; }
                    }
                    break;
                case EventType.MouseUp:
                    if (e.Button != 1) break;
                    down = false;
                    if (press.HasValue && !dragging)
                    {
                        if (ArrowUp.Contains(e.X, e.Y)) Scroll(-3 * StepPx);
                        else if (ArrowDown.Contains(e.X, e.Y)) Scroll(3 * StepPx);
                    }
                    press = null; dragging = false;
                    break;
                case EventType.KeyDown:
                    if (e.Key == Key.Escape || e.Key == Key.Return || e.Key == Key.I) Dismissed = true;
                    else if (e.Key == Key.Down || e.Key == Key.S || e.Key == Key.PageDown) Scroll(e.Key != Key.PageDown ? StepPx : view.H - StepPx);
                    else if (e.Key == Key.Up || e.Key == Key.W || e.Key == Key.PageUp) Scroll(e.Key != Key.PageUp ? -StepPx : -(view.H - StepPx));
                    break;
            }
        }

        public void Update(float dt)
        {
            dt = Math.Max(0f, Math.Min(dt, 0.1f));
            offset += (target - offset) * Math.Min(1f, dt * 14);
            if (Math.Abs(target - offset) < 0.5f) offset = target;
        }

        private IRect ArrowUp => new IRect(view.Right - 30, view.Y, 30, 24);
        private IRect ArrowDown => new IRect(view.Right - 30, view.Bottom - 24, 30, 24);

        public void Draw(Canvas canvas)
        {
            var art = game.Art; var g = game.Data.Glossary; var fonts = game.Fonts;
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(6, 4, 10, 190));
            PixelUi.Panel(canvas, art, PanelRect);
            var x = PanelRect.X + Pad;
            if (portrait.HasValue && portrait.Value.Valid)
            {
                var p = portrait.Value; var k = Math.Min(64f / Math.Max(1, p.W), 64f / Math.Max(1, p.H));
                int pw = Math.Max(1, (int)(p.W * k)), ph = Math.Max(1, (int)(p.H * k));
                canvas.BlitRect(p, Txt.Place(pw, ph, Anchor.MidLeft, x, PanelRect.Y + 46), Rgba.White);
                x += 76;
            }
            Txt.Outlined(canvas, fonts.Get(32), Content.Title, x, PanelRect.Y + 18, new Rgba(246, 204, 92));
            var parts = new List<string>(2);
            if (!string.IsNullOrEmpty(Content.Subtitle)) parts.Add(Content.Subtitle);
            if (!string.IsNullOrEmpty(Content.Tag)) parts.Add(Content.Tag);
            if (parts.Count > 0) Txt.Outlined(canvas, fonts.Get(16), string.Join(" · ", parts), x, PanelRect.Y + 56, g.Dim);
            canvas.Line(PanelRect.X + Pad, view.Y - 6, PanelRect.Right - Pad, view.Y - 6, new Rgba(110, 96, 70));

            var off = MathX.Round(offset);
            var h = Math.Min(view.H, bodyH - off);
            if (h > 0)
            {
                canvas.PushClip(new RectF(view.X, view.Y, view.W, h));
                foreach (var it in body)
                {
                    var y = view.Y + it.Y - off;
                    if (y + it.H <= view.Y || y >= view.Y + h) continue;
                    DrawItem(canvas, art, fonts, g, it, view.X, y, view.W);
                }
                canvas.PopClip();
            }
            if (MaxOffset > 0)
            {
                var track = new IRect(view.Right + 6, view.Y, 6, view.H);
                canvas.Rect(track, new Rgba(40, 34, 50), 0, 3);
                var knobH = Math.Max(30, (int)(track.H * view.H / (double)bodyH));
                var knobY = track.Y + (int)((track.H - knobH) * (offset / MaxOffset));
                canvas.Rect(new IRect(track.X, knobY, track.W, knobH), new Rgba(246, 204, 92), 0, 3);
                foreach (var (rect, up, show) in new[] { (ArrowUp, true, offset > 0.5f), (ArrowDown, false, offset < MaxOffset - 0.5f) })
                {
                    if (!show) continue;
                    int cx = rect.CenterX, cy = rect.CenterY; var gold = new Rgba(246, 204, 92);
                    canvas.Rect(rect, new Rgba(14, 10, 18), 0, 5);
                    if (up) canvas.Triangle(cx - 8, cy + 4, cx + 8, cy + 4, cx, cy - 5, gold);
                    else canvas.Triangle(cx - 8, cy - 4, cx + 8, cy - 4, cx, cy + 5, gold);
                }
                Txt.Outlined(canvas, fonts.Get(8), "Rueda / arrastrar / ↑↓ para desplazar", PanelRect.X + Pad, PanelRect.Bottom - 34, g.Dim);
            }
            PixelUi.Button(canvas, art, closeRect, "Cerrar", "bronze", PixelUi.StateOf(closeRect, mouse, down), null, "Esc", game.Time, 0, 14);
        }

        private static void DrawItem(Canvas canvas, Art art, FontRegistry fonts, Glossary g, Item it, int x, int y, int width)
        {
            if (it.Header != null)
            {
                var p = it.Header;
                canvas.Line(x, y + 2, x + width, y + 2, p.Color);
                var tx = x;
                if (!string.IsNullOrEmpty(p.Icon) && art.HasIcon(p.Icon)) { var ic = art.Icon(p.Icon); canvas.Blit(ic, x, y + 6, 2); tx = x + ic.W * 2 + 8; }
                var tf = fonts.Get(16);
                Txt.Outlined(canvas, tf, p.Title, tx, y + 6 + (28 - Txt.OutlinedSize(tf, p.Title).h) / 2, p.Color);
                if (!string.IsNullOrEmpty(p.Tag))
                {
                    var sf = fonts.Get(8);
                    Txt.Outlined(canvas, sf, p.Tag, x + width - Txt.OutlinedSize(sf, p.Tag).w, y + 14, g.Dim);
                }
                return;
            }
            if (it.Icon != null) canvas.Blit(art.Icon(it.Icon), x, y, 2);
            var ry = y;
            foreach (var r in it.Rows) { r.Draw(canvas, x + it.Indent, ry); ry += r.Height; }
        }
    }
}
