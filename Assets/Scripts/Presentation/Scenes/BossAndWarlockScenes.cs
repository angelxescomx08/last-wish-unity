using System;
using System.Collections.Generic;
using System.Numerics;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>scenes/boss_reward_scene.py: gold → epic pack → relic choice.</summary>
    public sealed class BossRewardScene : Scene
    {
        private enum Phase { Gold, Pack, Relics, Done }
        private static readonly Rgba Bg = new Rgba(18, 6, 6);
        private const int BoxW = 280, BoxH = 140;
        private readonly int goldEarned, interest;
        private readonly List<RelicOffer> relics;
        private Phase phase = Phase.Gold;
        private readonly List<IRect> relicRects = new List<IRect>();
        private IRect? button;
        private int? hovered;
        private (float x, float y) mouse;
        public bool Cleared { get; set; }
        public bool OpenPackRequested { get; set; }
        public RelicOffer ChosenRelic { get; private set; }

        public BossRewardScene(GameContext game, RunState run, int goldEarned, List<RelicOffer> relics) : base(game)
        { this.goldEarned = goldEarned; interest = run.LastInterest; this.relics = relics; }

        public void PackDone() => phase = Phase.Relics;

        public override void HandleEvent(in InputEvent e)
        {
            if (e.Type == EventType.MouseMove) { mouse = e.Pos; if (phase == Phase.Relics) UpdateHover(e.X, e.Y); }
            else if (e.IsLeftDown) Click(e.X, e.Y);
        }

        public override void Draw(Canvas canvas)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), Bg);
            const int cx = 640;
            if (phase == Phase.Gold)
            {
                Fonts.Get(30).DrawCentered(canvas, "¡JEFE DERROTADO!", cx, 80, new Rgba(230, 50, 50));
                Fonts.Get(20).DrawCentered(canvas, "Oro obtenido: +" + goldEarned + (interest != 0 ? "  (+" + interest + " de interés)" : ""), cx, 150, new Rgba(220, 190, 50));
                Fonts.Get(14).DrawCentered(canvas, "Abre el sobre épico para elegir una carta poderosa.", cx, 220, Colors.TextPrimary);
                var f = Fonts.Get(16); const string label = "Abrir Sobre Épico";
                int w = f.Width(label), h = f.Height, bx = cx - w / 2, by = 310 - h / 2;
                var rect = new IRect(bx - 20, by - 10, w + 40, h + 20); button = rect;
                canvas.Rect(rect, new Rgba(100, 30, 30), 0, 7); canvas.Rect(rect, new Rgba(200, 60, 60), 2, 7);
                f.Draw(canvas, label, bx, by, Colors.TextPrimary);
            }
            else if (phase == Phase.Pack) Fonts.Get(18).DrawCentered(canvas, "Abriendo sobre…", cx, 360, Colors.TextSecondary);
            else if (phase == Phase.Relics) DrawRelics(canvas, cx);
        }

        private void DrawRelics(Canvas canvas, int cx)
        {
            Fonts.Get(24).DrawCentered(canvas, "Elige una Reliquia", cx, 60, Colors.TextAccent);
            var n = relics.Count; var totalW = n * BoxW + Math.Max(0, n - 1) * 20; var startX = cx - totalW / 2;
            relicRects.Clear();
            for (var i = 0; i < n; i++)
            {
                var relic = relics[i]; var rect = new IRect(startX + i * (BoxW + 20), 120, BoxW, BoxH); relicRects.Add(rect);
                canvas.Rect(rect, Colors.BgPanel, 0, 8); canvas.Rect(rect, hovered == i ? Colors.TextAccent : Colors.PanelBorder, 2, 8);
                var nf = Fonts.Get(14);
                nf.DrawCentered(canvas, TextFit.Fit(nf, ChromaFx.Title(relic.Definition.Name, relic.Chroma), rect.W - 16), rect.CenterX, rect.Y + 35, new Rgba(220, 190, 60));
                var font = Fonts.Get(13); var lineH = font.LineSize - 2;
                var room = rect.Y + (relic.Chroma != ChromaKind.None ? 105 : rect.H - 10) - (rect.Y + 58);
                var lines = TextFit.WrapLimited(font, relic.Definition.Description, rect.W - 24, Math.Max(1, room / lineH));
                var top = rect.Y + 58 + (room - lines.Count * lineH) / 2;
                for (var k = 0; k < lines.Count; k++) font.DrawMidTop(canvas, lines[k], rect.CenterX, top + k * lineH, Colors.TextSecondary);
                if (relic.Chroma != ChromaKind.None)
                {
                    var st = ChromaFx.Style(relic.Chroma);
                    Fonts.Get(11).DrawCentered(canvas, st.ShortNote, rect.CenterX, rect.Y + 115, st.Main);
                    ChromaFx.DrawBox(canvas, Art, rect, relic.Chroma, Game.Time, 8);
                }
            }
            if (hovered.HasValue && hovered.Value < relics.Count)
                Tooltip.Draw(canvas, Game, Tooltip.Relic(Game, relics[hovered.Value].Definition, relics[hovered.Value].Chroma), mouse);
            Fonts.Get(11).DrawCentered(canvas, "Haz clic en una reliquia para tomarla", cx, 300, Colors.TextSecondary);
        }

        private void UpdateHover(float x, float y)
        {
            var previous = hovered; hovered = null;
            for (var i = 0; i < relicRects.Count; i++) if (relicRects[i].Contains(x, y)) { hovered = i; if (i != previous) Sound.PlayNav(); return; }
        }

        private void Click(float x, float y)
        {
            if (phase == Phase.Gold)
            {
                if (button.HasValue && button.Value.Contains(x, y)) { Sound.PlayConfirm(); phase = Phase.Pack; OpenPackRequested = true; }
            }
            else if (phase == Phase.Relics)
                for (var i = 0; i < relicRects.Count; i++)
                    if (relicRects[i].Contains(x, y)) { Sound.PlayReward(); ChosenRelic = relics[i]; phase = Phase.Done; Cleared = true; return; }
        }
    }

    /// <summary>card_upgrade.describe_upgrade for the port's upgrade rule (base layer +3 or +1/3, else −1 cost, else +1 draw).</summary>
    public static class Upgrades
    {
        private static (BigInteger d, long b, int draw) Base(CardDefinition c)
        {
            if (c.Effects != null && c.Effects.Count > 0 && c.Effects[0].Condition == EffectCondition.Always) return (c.Effects[0].Damage, c.Effects[0].Block, c.Effects[0].Draw);
            return (c.Damage.Value, c.Block, c.Draw);
        }
        public static string Describe(CardDefinition card)
        {
            var up = card.Upgrade();
            var (d0, b0, r0) = Base(card); var (d1, b1, r1) = Base(up);
            var parts = new List<string>();
            var dc = up.Cost - card.Cost;
            if (dc != 0) parts.Add(card.Cost + dc >= 0 ? (dc > 0 ? "+" : "") + dc + " de maná" : "coste 0");
            if (d1 != d0) parts.Add("+" + (d1 - d0) + " de daño");
            if (b1 != b0) parts.Add("+" + (b1 - b0) + " de escudo");
            if (r1 != r0) parts.Add("roba " + (r1 - r0) + " más");
            if (up.ManaGain != card.ManaGain) parts.Add("+" + (up.ManaGain - card.ManaGain) + " de maná al jugarla");
            return parts.Count > 0 ? string.Join(", ", parts) : "mejora especial";
        }

        private static readonly Rgba ManaInk = new Rgba(110, 190, 255), DamageInk = new Rgba(255, 120, 90), BlockInk = new Rgba(100, 175, 255),
            DrawInk = new Rgba(140, 230, 120), SpecialInk = new Rgba(190, 140, 255);

        /// <summary>What an upgrade changes, one chip each (icon, Spanish text, colour): El Brujo's confirmation panel.</summary>
        public static List<(string icon, string text, Rgba color)> Changes(CardDefinition card)
        {
            var up = card.Upgrade();
            var (d0, b0, r0) = Base(card); var (d1, b1, r1) = Base(up);
            var list = new List<(string, string, Rgba)>();
            var dc = up.Cost - card.Cost;
            if (dc != 0) list.Add(("mana", card.Cost + dc <= 0 ? "Coste 0" : (dc > 0 ? "+" : "−") + Math.Abs(dc) + " de maná", ManaInk));
            if (d1 != d0) list.Add(("damage", "+" + (d1 - d0) + " de daño", DamageInk));
            if (b1 != b0) list.Add(("block", "+" + (b1 - b0) + " de escudo", BlockInk));
            if (r1 != r0) list.Add(("draw", "Roba " + (r1 - r0) + " más", DrawInk));
            if (up.ManaGain != card.ManaGain) list.Add(("mana", "+" + (up.ManaGain - card.ManaGain) + " de maná al jugarla", ManaInk));
            if (list.Count == 0) list.Add(("buff", "Mejora especial", SpecialInk));
            return list;
        }
    }
}
