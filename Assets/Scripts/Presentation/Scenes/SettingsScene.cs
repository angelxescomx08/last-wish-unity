using System;
using System.Collections.Generic;

namespace LastWish.Presentation
{
    /// <summary>scenes/settings_scene.SettingsScene: FPS and volumes, applied live.</summary>
    public sealed class SettingsScene : Scene
    {
        private enum Kind { Toggle, Volume, Back }
        private sealed class Item { public string Label; public Kind Kind; public bool Music; }
        private static readonly Rgba Bg = new Rgba(14, 20, 14), On = new Rgba(80, 200, 80), Off = new Rgba(160, 100, 100);
        private const int TitleY = 170, ItemsY = 290, ItemGap = 86, HintY = 676;
        private const float Step = 0.1f;
        private const string NavHint = "↑ ↓ navegar  |  ← → volumen  |  ENTER confirmar  |  ESC volver";
        private readonly Item[] items = {
            new Item { Label = "Mostrar FPS", Kind = Kind.Toggle }, new Item { Label = "Efectos de sonido", Kind = Kind.Volume },
            new Item { Label = "Música", Kind = Kind.Volume, Music = true }, new Item { Label = "Volver", Kind = Kind.Back } };
        private int selected;
        private readonly List<RectF> itemRects = new List<RectF>();
        private readonly Dictionary<int, (RectF minus, RectF plus)> volumeButtons = new Dictionary<int, (RectF, RectF)>();
        public bool Cleared { get; set; }

        public SettingsScene(GameContext game) : base(game) { Sound.SetVolumes(Game.Prefs.SfxVolume, Game.Prefs.MusicVolume); }

        public override void HandleEvent(in InputEvent e)
        {
            if (e.Type == EventType.KeyDown)
            {
                if (e.Key == Key.Up || e.Key == Key.Down) Select(((selected + (e.Key == Key.Up ? -1 : 1)) % items.Length + items.Length) % items.Length);
                else if (e.IsConfirmKey) Confirm();
                else if (e.Key == Key.Left || e.Key == Key.Right) Adjust(e.Key == Key.Left ? -Step : Step);
                else if (e.Key == Key.Escape) Leave();
            }
            else if (e.Type == EventType.MouseMove) { for (var i = 0; i < itemRects.Count; i++) if (itemRects[i].Contains(e.Pos)) { Select(i); break; } }
            else if (e.IsLeftDown) Click(e.Pos);
        }

        public override void Draw(Canvas canvas)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), Bg);
            var cx = Canvas.Width / 2;
            Txt.DrawAt(canvas, Fonts.Get(56), "Ajustes", Anchor.Center, cx, TitleY, Colors.TextAccent);
            itemRects.Clear(); volumeButtons.Clear();
            for (var i = 0; i < items.Length; i++) itemRects.Add(DrawItem(canvas, items[i], i, cx, ItemsY + i * ItemGap));
            Txt.DrawAt(canvas, Fonts.Get(16), NavHint, Anchor.Center, cx, HintY, Colors.TextSecondary);
        }

        private RectF DrawItem(Canvas canvas, Item item, int index, float cx, float cy)
        {
            var isSelected = index == selected;
            var color = isSelected ? Colors.TextAccent : Colors.TextPrimary;
            var row = new RectF(cx - 340, cy - 33, 680, 66);
            if (isSelected) { canvas.Rect(row, new Rgba(25, 36, 26), 0, 9); canvas.Rect(row, new Rgba(73, 87, 49), 1, 9); }
            if (item.Kind == Kind.Volume)
            {
                var volume = item.Music ? Game.Prefs.MusicVolume : Game.Prefs.SfxVolume;
                Txt.DrawAt(canvas, Fonts.Get(28), item.Label, Anchor.MidLeft, row.Left + 22, row.CenterY, color);
                var minus = new RectF(row.Right - 266, row.Top + 11, 44, 44); var plus = new RectF(row.Right - 66, row.Top + 11, 44, 44);
                volumeButtons[index] = (minus, plus);
                VolumeButton(canvas, minus, "−", color, volume > 0); VolumeButton(canvas, plus, "+", color, volume < 1);
                var value = volume > 0 ? MathX.Round(volume * 100) + "%" : "0% · mudo";
                Txt.DrawAt(canvas, Fonts.Get(23), value, Anchor.Center, row.Right - 144, row.CenterY, volume > 0 ? On : Off);
            }
            else
            {
                var text = item.Label;
                if (item.Kind == Kind.Toggle) text += ":  " + (Game.Prefs.ShowFps ? "Activado" : "Desactivado");
                Txt.DrawAt(canvas, Fonts.Get(30), text, Anchor.Center, row.CenterX, row.CenterY, color);
            }
            return row;
        }

        private void VolumeButton(Canvas canvas, RectF rect, string label, Rgba color, bool enabled)
        {
            canvas.Rect(rect, new Rgba(31, 44, 31), 0, 6);
            var border = enabled ? color : Colors.TextSecondary;
            canvas.Rect(rect, border, 1, 6);
            Txt.DrawAt(canvas, Fonts.Get(30), label, Anchor.Center, rect.CenterX, rect.CenterY, border);
        }

        private void Select(int index) { if (index != selected) { selected = index; Sound.PlayNav(); } }

        private void Adjust(float delta)
        {
            var item = items[selected];
            if (item.Kind == Kind.Toggle) { Game.Prefs.ShowFps = !Game.Prefs.ShowFps; Sound.PlayConfirm(); }
            else if (item.Kind == Kind.Volume)
            {
                var current = item.Music ? Game.Prefs.MusicVolume : Game.Prefs.SfxVolume;
                var volume = (float)Math.Max(0, Math.Min(1, Math.Round(current + delta, 2, MidpointRounding.ToEven)));
                if (volume == current) return;
                if (item.Music) Game.Prefs.MusicVolume = volume; else Game.Prefs.SfxVolume = volume;
                Sound.SetVolumes(Game.Prefs.SfxVolume, Game.Prefs.MusicVolume); Sound.PlayConfirm();
            }
        }

        private void Confirm() { if (items[selected].Kind == Kind.Back) Leave(); else Adjust(Step); }
        private void Leave() { Sound.PlayCancel(); Cleared = true; }

        private void Click((float x, float y) pos)
        {
            for (var i = 0; i < itemRects.Count; i++)
            {
                if (!itemRects[i].Contains(pos)) continue;
                selected = i;
                if (volumeButtons.TryGetValue(i, out var b)) { if (b.minus.Contains(pos)) Adjust(-Step); else if (b.plus.Contains(pos)) Adjust(Step); }
                else Confirm();
                return;
            }
        }

        public override bool ShowGoldHud => false;
    }
}
