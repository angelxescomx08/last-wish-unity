using System;

namespace LastWish.Presentation
{
    public enum PauseAction { None, Resume, Abandon }

    /// <summary>ui/pause_menu.py: pause overlay and the run buttons.</summary>
    public sealed class PauseMenu
    {
        public static readonly RectF PauseButton = new RectF(232, 16, 64, 36);
        private const int StatsGap = 6;
        private readonly Art art;
        public bool Confirming { get; private set; }
        public int Selected { get; private set; }
        public PauseAction Action { get; private set; }
        private float t;
        private readonly RectF[] buttons = { new RectF(425, 348, 430, 52), new RectF(425, 420, 430, 52) };

        public PauseMenu(Art art) { this.art = art; }

        public void HandleEvent(in InputEvent e)
        {
            if (Action != PauseAction.None) return;
            if (e.Type == EventType.KeyDown)
            {
                if (e.Key == Key.Escape) { if (Confirming) { Confirming = false; Selected = 0; } else Action = PauseAction.Resume; }
                else if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Tab) Selected = 1 - Selected;
                else if (e.IsConfirmKey && !e.Repeat) Activate();
            }
            else if (e.Type == EventType.MouseMove) { for (var i = 0; i < buttons.Length; i++) if (buttons[i].Contains(e.Pos)) Selected = i; }
            else if (e.IsLeftDown)
                for (var i = 0; i < buttons.Length; i++) if (buttons[i].Contains(e.Pos)) { Selected = i; Activate(); break; }
        }

        public void Update(float dt) => t += Math.Max(0f, Math.Min(dt, 0.1f));

        private void Activate()
        {
            if (Selected == 0) { if (Confirming) Confirming = false; else Action = PauseAction.Resume; }
            else if (Confirming) Action = PauseAction.Abandon;
            else { Confirming = true; Selected = 0; }
        }

        public void Draw(Canvas canvas)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(5, 8, 16, 205));
            var panel = new RectF(350, 190, 580, 350);
            PixelUi.Panel(canvas, art, panel);
            PixelUi.Ribbon(canvas, art, panel.CenterX, panel.Y + 6, "PAUSA", 17);
            Text(canvas, Confirming ? "¿Abandonar la partida?" : "Partida en pausa", 34, 640, 248, new Rgba(237, 227, 207));
            Text(canvas, Confirming ? "Perderás el progreso de esta partida." : "La partida se detiene hasta que vuelvas.", 19, 640, 298, new Rgba(200, 190, 170));
            var labels = Confirming ? new[] { "Cancelar", "Abandonar y volver al menú" } : new[] { "Reanudar", "Abandonar partida…" };
            var icons = Confirming ? new string[] { null, null } : new[] { "hourglass", "unplayable" };
            for (var i = 0; i < 2; i++)
            {
                var danger = i == 1;
                PixelUi.Button(canvas, art, buttons[i], labels[i], danger ? "bronze" : "gold", i == Selected ? ButtonState.Hover : ButtonState.Idle, icons[i], null, t,
                    i == Selected && !danger ? (float)(0.4 + 0.3 * Math.Sin(t * 4)) : 0f, 22);
            }
            float x = 640 - 150;
            foreach (var (key, text) in new[] { ("↑↓", "seleccionar"), ("Enter", "aceptar"), ("Esc", "volver") })
            {
                var cap = PixelUi.Keycap(canvas, art, x, 512, key);
                var label = Txt.OutlinedAt(canvas, art.Fonts.Get(14), text, Anchor.MidLeft, cap.Right + 5, 512, new Rgba(200, 190, 170));
                x = cap.Right + 5 + label.W + 26;
            }
        }

        private void Text(Canvas canvas, string text, int size, float cx, float cy, Rgba color) =>
            Txt.OutlinedAt(canvas, art.Fonts.Get(size), text, Anchor.Center, cx, cy, color);

        public static RectF? PauseButtonRect(Scene scene)
        {
            if (scene.HasOverlay || scene.HidePauseButton) return null;
            return scene.PauseButtonRect ?? PauseButton;
        }

        public static RectF? StatsButtonRect(Scene scene)
        {
            var pause = PauseButtonRect(scene);
            if (pause == null) return null;
            return new RectF(pause.Value.Right + StatsGap, pause.Value.Y, pause.Value.W, pause.Value.H);
        }

        private static ButtonState State(RectF rect, (float x, float y) mouse, bool pressed) =>
            rect.Contains(mouse) ? (pressed ? ButtonState.Press : ButtonState.Hover) : ButtonState.Idle;

        private static void HoverLabel(Canvas canvas, Art art, RectF rect, string text)
        {
            var font = art.Fonts.Get(13);
            var (w, h) = Txt.OutlinedSize(font, text);
            var box = Txt.Place(w, h, Anchor.MidTop, rect.CenterX, rect.Bottom + 6).Inflate(12, 6);
            if (box.X < 0) box.X = 0; if (box.Right > Canvas.Width) box.X = Canvas.Width - box.W;
            if (box.Y < 0) box.Y = 0; if (box.Bottom > Canvas.Height) box.Y = Canvas.Height - box.H;
            canvas.Rect(box, new Rgba(14, 10, 18), 0, 4);
            canvas.Rect(box, new Rgba(176, 140, 72), 1, 4);
            Txt.OutlinedAt(canvas, font, text, Anchor.Center, box.CenterX, box.CenterY, new Rgba(250, 236, 206));
        }

        public static void DrawPauseButton(Canvas canvas, Art art, RectF rect, (float x, float y) mouse, bool pressed, float t)
        {
            PixelUi.Button(canvas, art, rect, "", "bronze", State(rect, mouse, pressed), "gear", "Esc", t);
            if (rect.Contains(mouse)) HoverLabel(canvas, art, rect, "Pausa");
        }

        public static void DrawStatsButton(Canvas canvas, Art art, RectF rect, (float x, float y) mouse, bool pressed, float t)
        {
            PixelUi.Button(canvas, art, rect, "", "bronze", State(rect, mouse, pressed), "helmet", "C", t);
            if (rect.Contains(mouse)) HoverLabel(canvas, art, rect, "Héroe y estadísticas");
        }
    }
}
