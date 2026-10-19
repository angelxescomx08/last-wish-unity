using System;
using System.Collections.Generic;

namespace LastWish.Presentation
{
    public enum MenuAction { None, Play, Settings, Dev, Exit }

    /// <summary>Shared look helpers of the run screens (vignette, hero sprites).</summary>
    public static class Looks
    {
        /// <summary>main_menu_scene._vignette: 64×36 radial shade smooth-scaled to the screen.</summary>
        public static void Vignette(Canvas canvas, Art art)
        {
            var tex = art.Procedural("menu_vignette", 64, 36, () =>
            {
                var px = new Rgba[64 * 36];
                for (var y = 0; y < 36; y++)
                    for (var x = 0; x < 64; x++)
                    {
                        var d = Math.Sqrt(Math.Pow((x + 0.5) / 32 - 1, 2) + Math.Pow(((y + 0.5) / 18 - 0.9) * 1.1, 2));
                        var a = (int)Math.Min(210, 40 + 150 * Math.Pow(Math.Max(0.0, d - 0.35), 1.5));
                        px[y * 64 + x] = new Rgba(6, 4, 12, a);
                    }
                return px;
            }, smooth: true);
            canvas.BlitRect(new Sprite(tex), new RectF(0, 0, Canvas.Width, Canvas.Height), Rgba.White);
        }

        /// <summary>SpriteLoader.get_player_sprite drawn with midbottom at (x, bottom); returns false without a sheet.</summary>
        public static bool Hero(Canvas canvas, Art art, string playerName, int size, float elapsed, float x, float bottom, string animation = "idle", bool flip = false, Rgba? tint = null)
        {
            var id = Art.HeroIdFor(playerName);
            if (id == null) return false;
            var sheets = art.Hero(id);
            var frame = sheets.Frame(animation, elapsed, size, out var scale);
            var w = frame.W * scale; var h = frame.H * scale;
            var rect = new RectF((float)Math.Floor(x) - (int)w / 2, bottom - h, w, h);
            canvas.BlitFlipped(frame, rect, flip, false, tint ?? Rgba.White);
            return true;
        }
    }

    /// <summary>scenes/main_menu_scene.MainMenuScene</summary>
    public sealed class MainMenuScene : Scene
    {
        private sealed class Option { public string Label; public MenuAction Action; public bool Enabled = true; }
        private const int TitleY = 150, SubtitleY = 222, OptionsY = 340, OptionGap = 62, NavHintY = 694, ButtonW = 330, ButtonH = 48;
        private const float FadeIn = 0.6f;
        private static readonly Dictionary<MenuAction, string> Icons = new Dictionary<MenuAction, string> {
            { MenuAction.Play, "attack_2" }, { MenuAction.Settings, "gear" }, { MenuAction.Dev, "ritual" }, { MenuAction.Exit, "unplayable" } };
        private static readonly (string name, int x, int feet, int size)[] Heroes = { ("La Pícara", 150, 640, 192), ("La Guerrera", 300, 650, 192), ("El Mago", 1080, 650, 192) };
        private const string Title = "Último Deseo", Subtitle = "Un juego de cartas";

        private readonly List<Option> options;
        private readonly List<RectF> optionRects = new List<RectF>();
        private int selected;
        private float t;
        private DungeonBackdrop backdrop;
        private (float x, float y) mouse = (-1, -1);
        private bool pressed;
        public MenuAction RequestedAction { get; set; }

        public MainMenuScene(GameContext game, bool hasActiveGame = false, bool fadeIn = true) : base(game)
        {
            options = new List<Option> {
                new Option { Label = hasActiveGame ? "Continuar" : "Jugar", Action = MenuAction.Play },
                new Option { Label = "Ajustes", Action = MenuAction.Settings },
                new Option { Label = "Pruebas", Action = MenuAction.Dev },
                new Option { Label = "Salir", Action = MenuAction.Exit },
            };
            t = fadeIn ? 0f : FadeIn;
        }

        public override void HandleEvent(in InputEvent e)
        {
            if (RequestedAction != MenuAction.None) return;
            if (e.Type == EventType.KeyDown)
            {
                if (e.Key == Key.Up) Move(-1);
                else if (e.Key == Key.Down) Move(1);
                else if (e.IsConfirmKey) Confirm();
            }
            else if (e.Type == EventType.MouseMove) { mouse = e.Pos; Hover(e.Pos); }
            else if (e.IsLeftDown) { pressed = true; Click(e.Pos); }
            else if (e.IsLeftUp) pressed = false;
        }

        public override void Update(float dt)
        {
            dt = Math.Max(0f, Math.Min(dt, 0.1f));
            t += dt;
            backdrop?.Update(dt);
        }

        public override void Draw(Canvas canvas)
        {
            var cx = Canvas.Width / 2;
            if (backdrop == null) backdrop = new DungeonBackdrop(Art, 3);
            backdrop.Draw(canvas);
            Looks.Vignette(canvas, Art);
            for (var k = 0; k < Heroes.Length; k++)
            {
                var (name, x, feet, size) = Heroes[k];
                canvas.Ellipse(RectF.CenteredInt(x, feet - 4, 110, 20), new Rgba(0, 0, 0, 120));
                Looks.Hero(canvas, Art, name, size, t + k * 0.37f, x, feet + 20, flip: x > cx);
            }

            var pulse = 0.5 + 0.5 * Math.Sin(t * 1.4);
            Glows.Draw(canvas, Art, cx, TitleY, (int)(260 + 30 * pulse), new Rgba(255, 170, 70));
            var big = Fonts.Get(76);
            Txt.OutlinedAt(canvas, big, Title, Anchor.Center, cx + 4, TitleY + 5, new Rgba(40, 20, 10), new Rgba(40, 20, 10));
            Txt.OutlinedAt(canvas, big, Title, Anchor.Center, cx, TitleY, new Rgba(252, 214, 112), new Rgba(36, 18, 8));
            PixelUi.Ribbon(canvas, Art, cx, SubtitleY, Subtitle.ToUpperInvariant(), 18);

            var n = options.Count;
            var panel = RectF.CenteredInt(cx, OptionsY + (n - 1) * OptionGap / 2, ButtonW + 70, (n - 1) * OptionGap + ButtonH + 56);
            PixelUi.Panel(canvas, Art, panel);
            optionRects.Clear();
            for (var i = 0; i < n; i++) optionRects.Add(DrawOption(canvas, options[i], cx, OptionsY + i * OptionGap, i == selected));

            float hx = cx - 170;
            foreach (var (key, text) in new[] { ("↑↓", "navegar"), ("Enter", "confirmar"), ("Clic", "elegir") })
            {
                var cap = PixelUi.Keycap(canvas, Art, hx, NavHintY, key);
                var label = Txt.OutlinedAt(canvas, Fonts.Get(14), text, Anchor.MidLeft, cap.Right + 6, NavHintY, new Rgba(214, 204, 186));
                hx = cap.Right + 6 + label.W + 34;
            }
            if (t < FadeIn) canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(0, 0, 0, (int)(255 * (1 - t / FadeIn))));
        }

        private RectF DrawOption(Canvas canvas, Option opt, float cx, float cy, bool isSelected)
        {
            var rect = RectF.CenteredInt(cx, cy, ButtonW, ButtonH);
            ButtonState state;
            if (!opt.Enabled) state = ButtonState.Off;
            else if (isSelected) state = pressed && rect.Contains(mouse) ? ButtonState.Press : ButtonState.Hover;
            else state = ButtonState.Idle;
            var glow = isSelected && opt.Enabled ? (float)(0.35 + 0.25 * Math.Sin(t * 4.0)) : 0f;
            PixelUi.Button(canvas, Art, rect, opt.Label, isSelected ? "gold" : "bronze", state, Icons[opt.Action], null, t, glow, isSelected ? 26 : 22);
            if (isSelected && opt.Enabled)
            {
                var bob = (int)(Math.Sin(t * 5.0) * 3);
                foreach (var (side, sign) in new[] { (rect.Left - 18, 1), (rect.Right + 18, -1) })
                {
                    var tip = (side + sign * 8 + sign * bob, cy);
                    var pts = new[] { tip, (tip.Item1 - sign * 12, cy - 9), (tip.Item1 - sign * 12, cy + 9) };
                    canvas.Polygon(pts, new Rgba(252, 214, 112));
                    canvas.Polygon(pts, new Rgba(40, 20, 10), 2);
                }
            }
            return rect;
        }

        private void Move(int delta)
        {
            var previous = selected; var n = options.Count;
            for (var i = 0; i < n; i++)
            {
                selected = ((selected + delta) % n + n) % n;
                if (options[selected].Enabled) { if (selected != previous) Sound.PlayNav(); return; }
            }
        }

        private void Confirm()
        {
            var opt = options[selected];
            if (opt.Enabled && RequestedAction == MenuAction.None) { RequestedAction = opt.Action; Sound.PlayConfirm(); }
        }

        private void Hover((float x, float y) pos)
        {
            for (var i = 0; i < optionRects.Count; i++)
                if (optionRects[i].Contains(pos) && options[i].Enabled) { if (selected != i) Sound.PlayNav(); selected = i; return; }
        }

        private void Click((float x, float y) pos)
        {
            for (var i = 0; i < optionRects.Count; i++)
                if (optionRects[i].Contains(pos) && options[i].Enabled) { selected = i; Confirm(); return; }
        }

        public override bool ShowGoldHud => false;
    }
}
