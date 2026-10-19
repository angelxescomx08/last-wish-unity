using System;

namespace LastWish.Presentation
{
    /// <summary>ui/loading_screen.LoadingScreen: drawn with fonts and primitive shapes only.</summary>
    public sealed class LoadingScreen
    {
        private readonly FontRegistry fonts;
        public LoadingScreen(FontRegistry fonts) { this.fonts = fonts; }

        public void Draw(Canvas canvas, string label, float progress)
        {
            progress = Math.Max(0f, Math.Min(1f, progress));
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(12, 14, 24));
            var row = 0;
            for (var y = 0; y < 720; y += 48, row++)
                for (var x = -80; x < 1280; x += 160)
                    canvas.Rect(new RectF(x + (row % 2 == 1 ? 80 : 0), y, 158, 46), new Rgba(18, 21, 33), 1);
            var panel = new RectF(300, 206, 680, 308);
            canvas.Fill(panel.Move(6, 8), new Rgba(8, 10, 18));
            canvas.Fill(panel, new Rgba(22, 24, 38));
            canvas.Rect(panel, new Rgba(94, 76, 46), 2);
            canvas.Rect(panel.Inflate(-12, -12), new Rgba(45, 44, 54), 2);
            foreach (var (x, y) in new[] { (panel.Left, panel.Top), (panel.Right, panel.Top), (panel.Left, panel.Bottom), (panel.Right, panel.Bottom) })
                canvas.Fill(new RectF(x - 3, y - 3, 6, 6), Colors.TextAccent);
            Txt.DrawAt(canvas, fonts.Get(64), "ÚLTIMO DESEO", Anchor.Center, 640, 278, Colors.TextAccent);
            Txt.DrawAt(canvas, fonts.Get(18), "PREPARANDO LA AVENTURA", Anchor.Center, 640, 331, Colors.TextPrimary);
            Txt.DrawAt(canvas, fonts.Get(18), label ?? "", Anchor.Center, 640, 375, Colors.TextSecondary);
            var bar = new RectF(410, 400, 460, 24);
            canvas.Fill(bar, new Rgba(6, 10, 18));
            var fill = bar.Inflate(-8, -8);
            fill.W = MathX.Round(fill.W * progress);
            if (fill.W > 0)
            {
                canvas.Fill(fill, Colors.TextAccent);
                canvas.Fill(new RectF(fill.X, fill.Y, fill.W, 3), new Rgba(245, 216, 139));
            }
            canvas.Rect(bar, new Rgba(115, 105, 85), 2);
            Txt.DrawAt(canvas, fonts.Get(16), MathX.Round(progress * 100) + " %", Anchor.Center, 640, 455, Colors.TextPrimary);
            Txt.DrawAt(canvas, fonts.Get(16), "La mazmorra te espera...", Anchor.Center, 640, 550, Colors.TextSecondary);
        }
    }
}
