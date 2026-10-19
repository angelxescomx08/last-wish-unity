using System.Collections.Generic;

namespace LastWish.Presentation
{
    public enum DeathAction { None, NewGame, MainMenu }

    /// <summary>scenes/death_scene.DeathScene</summary>
    public sealed class DeathScene : Scene
    {
        private static readonly Rgba Bg = new Rgba(8, 5, 10);
        private const int TitleY = 220, SubtitleY = 290, OptionsY = 390, OptionGap = 60, HintY = 692;
        private readonly int turnReached;
        private int selected;
        private readonly List<RectF> rects = new List<RectF>();
        private readonly (string label, DeathAction action)[] options = { ("Nueva Partida", DeathAction.NewGame), ("Menú Principal", DeathAction.MainMenu) };
        public DeathAction RequestedAction { get; set; }

        public DeathScene(GameContext game, int turnReached) : base(game) { this.turnReached = turnReached; }

        public override void HandleEvent(in InputEvent e)
        {
            if (RequestedAction != DeathAction.None) return;
            if (e.Type == EventType.KeyDown)
            {
                if (e.Key == Key.Up) Move(-1); else if (e.Key == Key.Down) Move(1); else if (e.IsConfirmKey) Confirm();
            }
            else if (e.Type == EventType.MouseMove)
            { for (var i = 0; i < rects.Count; i++) if (rects[i].Contains(e.Pos)) { if (selected != i) Sound.PlayNav(); selected = i; return; } }
            else if (e.IsLeftDown)
            { for (var i = 0; i < rects.Count; i++) if (rects[i].Contains(e.Pos)) { selected = i; Confirm(); return; } }
        }

        private void Move(int d) { var prev = selected; selected = ((selected + d) % 2 + 2) % 2; if (selected != prev) Sound.PlayNav(); }
        private void Confirm() { if (RequestedAction == DeathAction.None) { RequestedAction = options[selected].action; Sound.PlayConfirm(); } }

        public override void Draw(Canvas canvas)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), Bg);
            var cx = Canvas.Width / 2;
            Txt.DrawAt(canvas, Fonts.Get(72), "HAS MUERTO", Anchor.Center, cx, TitleY, new Rgba(210, 40, 40));
            Txt.DrawAt(canvas, Fonts.Get(24), "Llegaste al turno " + turnReached, Anchor.Center, cx, SubtitleY, Colors.TextPrimary);
            rects.Clear();
            for (var i = 0; i < options.Length; i++)
            {
                var sel = i == selected; var size = sel ? 38 : 30;
                var color = sel ? Colors.TextAccent : Colors.TextPrimary;
                var rect = Txt.DrawAt(canvas, Fonts.Get(size), options[i].label, Anchor.Center, cx, OptionsY + i * OptionGap, color);
                if (sel) { var f = Fonts.Get(size); f.Draw(canvas, ">", rect.Left - f.Width(">") - 14, rect.Top, Colors.TextAccent); }
                rects.Add(rect);
            }
            Txt.DrawAt(canvas, Fonts.Get(13), "↑ ↓ para navegar  |  ENTER para confirmar", Anchor.Center, cx, HintY, Colors.TextSecondary);
        }

        public override bool ShowGoldHud => false;
    }
}
