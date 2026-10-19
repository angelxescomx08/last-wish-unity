using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>domain/character.ALL_CHARACTERS: presentation text of the three heroes.</summary>
    public static class Characters
    {
        public static readonly HeroKind[] All = { HeroKind.Warrior, HeroKind.Mage, HeroKind.Rogue };
        public static string Description(HeroKind kind)
        {
            switch (kind)
            {
                case HeroKind.Warrior: return "Luchadora resistente que prefiere la defensa.";
                case HeroKind.Mage: return "Maestro de los arcanos con gran poder ofensivo.";
                default: return "Ágil y astuta, con la suerte de su lado.";
            }
        }
    }

    /// <summary>scenes/character_select_scene.CharacterSelectScene</summary>
    public sealed class CharacterSelectScene : Scene
    {
        private static readonly Rgba Bg = new Rgba(14, 20, 14);
        private const int TitleY = 50, PanelW = 278, PanelH = 420, PanelGap = 26, PanelsTop = 110, HintY = 692, BarH = 5, Corner = 7;
        private const int PanelsLeft = (1280 - 3 * PanelW - 2 * PanelGap) / 2, BarW = PanelW - 28;
        private const int SeedBoxY = 548, SeedBoxW = 320, SeedBoxH = 32, SeedBoxX = (1280 - SeedBoxW) / 2;
        private const string Title = "SELECCIÓN DE PERSONAJE", NavHint = "← → para navegar  |  ENTER para confirmar  |  ESC para volver";
        private static readonly (string label, Func<HeroDefinition, int> value, Rgba color)[] Rows = {
            ("Daño", h => h.AttackBonus, Colors.TextDamage), ("Vida", h => h.MaxHp, Colors.HpFill), ("Suerte", h => h.Luck, Colors.TextAccent),
            ("Maná", h => h.MaxMana, Colors.ManaFill), ("Destreza", h => h.Dexterity, Colors.BlockColor) };

        private int selected;
        private readonly List<RectF> panelRects = new List<RectF>();
        private float idleTime;
        private readonly long defaultSeed;
        private string seedText;
        private bool seedActive;
        private readonly RectF seedRect = new RectF(SeedBoxX, SeedBoxY, SeedBoxW, SeedBoxH);
        public bool Confirmed { get; set; }
        public bool BackToMenu { get; set; }

        public CharacterSelectScene(GameContext game) : base(game)
        {
            defaultSeed = (long)(Rng.Shared.NextULong() >> 1);
            seedText = defaultSeed.ToString();
        }

        public HeroKind SelectedCharacter => Characters.All[selected];
        public long Seed
        {
            get
            {
                if (seedText.Length == 0) return defaultSeed;
                return System.Numerics.BigInteger.TryParse(seedText, out var v) ? (long)(v % System.Numerics.BigInteger.Pow(2, 63)) : defaultSeed;
            }
        }

        public override void HandleEvent(in InputEvent e)
        {
            if (Confirmed || BackToMenu) return;
            if (e.Type == EventType.KeyDown)
            {
                if (seedActive)
                {
                    if (e.Key == Key.Backspace) { if (seedText.Length > 0) seedText = seedText.Substring(0, seedText.Length - 1); }
                    else if (e.Key == Key.Return || e.Key == Key.KeypadEnter || e.Key == Key.Escape) seedActive = false;
                }
                else if (e.Key == Key.Left) Move(-1);
                else if (e.Key == Key.Right) Move(1);
                else if (e.Key == Key.Return || e.Key == Key.KeypadEnter) { Confirmed = true; Sound.PlayConfirm(); }
                else if (e.Key == Key.Escape) { BackToMenu = true; Sound.PlayCancel(); }
            }
            else if (e.Type == EventType.Text && seedActive)
            { if (char.IsDigit(e.Char) && seedText.Length < 19) seedText += e.Char; }
            else if (e.Type == EventType.MouseMove) Hover(e.Pos);
            else if (e.IsLeftDown) Click(e.Pos);
        }

        public override void Update(float dt)
        {
            var cycle = Art.Hero("warrior").Seconds("idle");
            idleTime = (idleTime + Math.Max(0f, dt)) % (cycle > 0 ? cycle : 1f);
        }

        public override void Draw(Canvas canvas)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), Bg);
            var cx = Canvas.Width / 2;
            Txt.DrawAt(canvas, Fonts.Get(30), Title, Anchor.Center, cx, TitleY, Colors.TextAccent);
            panelRects.Clear();
            for (var i = 0; i < Characters.All.Length; i++)
                panelRects.Add(DrawPanel(canvas, Characters.All[i], PanelsLeft + i * (PanelW + PanelGap), PanelsTop, i == selected));
            DrawSeed(canvas, cx);
            Txt.DrawAt(canvas, Fonts.Get(13), NavHint, Anchor.Center, cx, HintY, Colors.TextSecondary);
        }

        private RectF DrawPanel(Canvas canvas, HeroKind kind, float x, float y, bool isSelected)
        {
            var hero = HeroDefinition.OriginalFor(kind);
            var rect = new RectF(x, y, PanelW, PanelH);
            canvas.Rect(rect, Colors.BgPanel, 0, Corner);
            canvas.Rect(rect, isSelected ? Colors.TextAccent : Colors.PanelBorder, isSelected ? 2 : 1, Corner);
            var cx = x + PanelW / 2;
            Txt.DrawAt(canvas, Fonts.Get(22), hero.Name, Anchor.Center, cx, y + 28, isSelected ? Colors.TextAccent : Colors.TextPrimary);
            canvas.Line(x + 12, y + 50, x + PanelW - 12, y + 50, Colors.PanelBorder);
            var font = Fonts.Get(11);
            var lines = TextFit.WrapLimited(font, Characters.Description(kind), PanelW - 28, 2);
            var lineH = font.LineSize;
            var top = y + 68 - MathX.FloorDiv(lines.Count * lineH, 2);
            for (var k = 0; k < lines.Count; k++) Txt.DrawAt(canvas, font, lines[k], Anchor.MidTop, cx, top + k * lineH, Colors.TextSecondary);
            var sheets = Art.Hero(hero.AssetId);
            var frame = sheets.Frame("idle", idleTime, 96, out var scale);
            canvas.BlitRect(frame, RectF.CenteredInt(cx, y + 137, (int)(frame.W * scale), (int)(frame.H * scale)), Rgba.White);
            Fonts.Get(9).Draw(canvas, "ESTADÍSTICAS", x + 14, y + 200, Colors.TextSecondary);
            var statY = y + 218;
            foreach (var (label, value, color) in Rows)
            {
                DrawStat(canvas, label, value(hero), Characters.All.Max(k => value(HeroDefinition.OriginalFor(k))), color, x + 14, statY);
                statY += 32;
            }
            if (isSelected) Txt.DrawAt(canvas, Fonts.Get(12), "SELECCIONADO", Anchor.Center, cx, y + PanelH - 18, Colors.TextAccent);
            return rect;
        }

        private void DrawStat(Canvas canvas, string label, int value, int max, Rgba color, float x, float y)
        {
            var font = Fonts.Get(11);
            font.Draw(canvas, label + ":", x, y, Colors.TextPrimary);
            var v = value.ToString();
            font.Draw(canvas, v, x + BarW - font.Width(v), y, color);
            var barY = y + 17;
            canvas.Rect(new RectF(x, barY, BarW, BarH), Colors.BgDark, 0, 2);
            var fill = Math.Max(2, (int)(BarW * value / (double)Math.Max(1, max)));
            canvas.Rect(new RectF(x, barY, fill, BarH), color, 0, 2);
        }

        private void DrawSeed(Canvas canvas, float cx)
        {
            var small = Fonts.Get(11);
            small.Draw(canvas, "Semilla:", seedRect.X - small.Width("Semilla:") - 8, seedRect.CenterY - small.Height / 2, Colors.TextSecondary);
            canvas.Rect(seedRect, Colors.BgPanel, 0, 4);
            canvas.Rect(seedRect, seedActive ? Colors.TextAccent : Colors.PanelBorder, 1, 4);
            var display = seedText.Length > 0 ? seedText : defaultSeed.ToString();
            if (seedActive && seedText.Length == 0) display = "";
            var font = Fonts.Get(12);
            font.Draw(canvas, display, seedRect.X + 6, seedRect.CenterY - font.Height / 2, Colors.TextPrimary);
            Txt.DrawAt(canvas, Fonts.Get(10), "Haz clic para editar la semilla", Anchor.Center, cx, SeedBoxY + SeedBoxH + 12, Colors.TextSecondary);
        }

        private void Move(int delta)
        {
            var next = ((selected + delta) % 3 + 3) % 3;
            if (next != selected) Sound.PlayNav();
            selected = next;
        }

        private void Hover((float x, float y) pos)
        {
            for (var i = 0; i < panelRects.Count; i++)
                if (panelRects[i].Contains(pos)) { if (selected != i) Sound.PlayNav(); selected = i; return; }
        }

        private void Click((float x, float y) pos)
        {
            if (seedRect.Contains(pos)) { if (!seedActive) Sound.PlayConfirm(); seedActive = true; return; }
            seedActive = false;
            for (var i = 0; i < panelRects.Count; i++)
                if (panelRects[i].Contains(pos))
                {
                    if (selected == i) { Confirmed = true; Sound.PlayConfirm(); }
                    else { selected = i; Sound.PlayNav(); }
                    return;
                }
        }

        public override bool ShowGoldHud => false;
    }
}
