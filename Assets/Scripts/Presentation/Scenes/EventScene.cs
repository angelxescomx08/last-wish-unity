using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>Text-only event decisions. All payments and outcomes belong to the domain session.</summary>
    public sealed class EventScene : Scene
    {
        private readonly RunState run;
        private readonly List<IRect> buttons = new List<IRect>();
        private int selected;
        public EventSession Session { get; }
        public bool Cleared { get; set; }
        public EventScene(GameContext game, RunState run, string roomId) : base(game)
        { this.run = run; Session = run.OpenEvent(roomId); }

        public override void HandleEvent(in InputEvent e)
        {
            if (Cleared || e.Repeat) return;
            if (Session.Completed)
            {
                if (e.IsConfirmKey || (e.IsLeftDown && new IRect(440, 590, 400, 50).Contains(e.X, e.Y))) Cleared = true;
                return;
            }
            if (e.Type == EventType.MouseMove)
                for (var i = 0; i < buttons.Count; i++) if (buttons[i].Contains(e.X, e.Y)) selected = i;
            if (e.IsKey(Key.Down) || e.IsKey(Key.Tab)) selected = (selected + 1) % Session.Stage.Choices.Count;
            if (e.IsKey(Key.Up)) selected = (selected + Session.Stage.Choices.Count - 1) % Session.Stage.Choices.Count;
            if (e.Type == EventType.KeyDown && e.Digit >= 1 && e.Digit <= Session.Stage.Choices.Count) Choose(e.Digit - 1);
            else if (e.IsConfirmKey) Choose(selected);
            else if (e.IsLeftDown)
                for (var i = 0; i < buttons.Count; i++) if (buttons[i].Contains(e.X, e.Y)) { Choose(i); break; }
        }

        private void Choose(int index)
        { if (Session.Choose(index)) { Sound.PlayReward(); selected = 0; buttons.Clear(); } }

        public override void Draw(Canvas canvas)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(10, 10, 20));
            Fonts.Get(24).DrawCentered(canvas, Session.Definition.Name, 640, 80, Colors.TextAccent);
            var rarity = new[] { "Común", "Poco común", "Raro", "Épico", "Legendario" }[Session.Definition.Rarity - 1];
            Fonts.Get(14).DrawCentered(canvas, rarity + " · Piso " + run.Floor, 640, 120, Colors.TextSecondary);
            buttons.Clear();
            if (Session.Completed)
            {
                Wrapped(canvas, Session.Message, 245, Colors.TextPrimary, 18);
                var rect = new IRect(440, 590, 400, 50);
                canvas.Rect(rect, new Rgba(50, 120, 70), 0, 6);
                Fonts.Get(16).DrawCentered(canvas, "Continuar · Enter", 640, 605, Colors.TextPrimary);
                return;
            }
            Wrapped(canvas, Session.Stage.Body, 165, Colors.TextPrimary, 15);
            if (Session.Message.Length > 0) Wrapped(canvas, Session.Message, 235, Colors.TextAccent, 12);
            for (var i = 0; i < Session.Stage.Choices.Count; i++)
            {
                var choice = Session.Stage.Choices[i]; var y = 310 + i * 95;
                var rect = new IRect(70, y, 1140, 88); buttons.Add(rect);
                var available = Session.BlockReason(i).Length == 0;
                canvas.Rect(rect, new Rgba(25, 30, 43), 0, 5);
                canvas.Rect(rect, i == selected ? Colors.TextAccent : Colors.TextSecondary, i == selected ? 2 : 1, 5);
                var label = (i + 1) + ". " + choice.Label;
                if (choice.Success.Upgrade) label += " (primera sin mejorar)";
                if (choice.GoldCost > 0) label += " · " + choice.GoldCost + " oro";
                if (choice.HpCost > 0) label += " · " + choice.HpCost + " vida";
                if (choice.MaxHpCost > 0) label += " · " + choice.MaxHpCost + " vida máxima";
                Fonts.Get(14).Draw(canvas, label, 88, y + 9, available ? Colors.TextPrimary : Colors.TextSecondary);
                var details = choice.Success.Text;
                if (choice.SuccessPercent < 100) details = choice.SuccessPercent + "%: " + details + " Fallo: " + choice.Failure.Text;
                Wrapped(canvas, details, y + 38, available ? Colors.TextPrimary : Colors.TextSecondary, 12);
            }
            var reason = Session.BlockReason(selected);
            Fonts.Get(12).DrawCentered(canvas, reason.Length > 0 ? reason : "↑ ↓ / 1–3 elegir · Enter confirmar", 640, 615, Colors.TextSecondary);
            Fonts.Get(14).DrawCentered(canvas, "Vida: " + run.Hp + "/" + run.MaxHp + " · Oro: " + run.Gold + " · Suerte: " + run.Luck, 640, 660, Colors.TextPrimary);
        }

        private void Wrapped(Canvas canvas, string text, int y, Rgba color, int size)
        {
            var font = Fonts.Get(size); var line = "";
            foreach (var word in text.Split(' '))
            {
                var next = line.Length == 0 ? word : line + " " + word;
                if (font.Width(next) > 1080 && line.Length > 0)
                { font.DrawCentered(canvas, line, 640, y, color); y += size + 7; line = word; }
                else line = next;
            }
            if (line.Length > 0) font.DrawCentered(canvas, line, 640, y, color);
        }
    }
}

