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
    }

    /// <summary>The deck grid of El Brujo with prices and the "Ver mejoras" toggle.</summary>
    internal sealed class UpgradeGrid : PileViewer
    {
        public override string CloseLabel => "Salir";
        public override string FooterText => "Las cartas normales se pueden mejorar una sola vez";
        public override string HintText => "Haz clic en una carta para mejorarla  ·  Rueda / ↑ ↓ para desplazarte";
        public override bool CloseOnOutsideClick => false;
        private static readonly Rgba Accent = new Rgba(80, 210, 170), Gold = new Rgba(235, 195, 70), TooExpensive = new Rgba(220, 90, 80);
        private readonly RunState run;
        public int? Clicked;
        public bool ShowUpgraded;
        public readonly IRect ToggleRect;

        public UpgradeGrid(GameContext game, RunState run) : base(game, "El Brujo", run.Deck, CardBonus.ForRun(run))
        { this.run = run; ToggleRect = new IRect(CloseRect.X - 220, CloseRect.Y, 208, CloseRect.H); }

        public void Sync() { Items.Clear(); Items.AddRange(run.Deck); }
        public void ClearMouse() => MousePos = (-1, -1);

        public override void HandleEvent(in InputEvent e)
        {
            if (e.IsLeftDown && ToggleRect.Contains(e.X, e.Y) || e.IsKey(Key.V)) { ShowUpgraded = !ShowUpgraded; return; }
            if (e.IsLeftDown && Viewport.Contains(e.X, e.Y))
                foreach (var pair in ItemRects) if (pair.Value.Contains(e.X, e.Y)) { Clicked = pair.Key; return; }
            base.HandleEvent(e);
        }

        private CardDefinition Shown(CardDefinition c) => ShowUpgraded && RunState.CanUpgrade(c) ? c.Upgrade() : c;

        protected override void DrawItem(Canvas canvas, CardDefinition item, IRect rect, int index)
        {
            string text; Rgba color;
            if (!RunState.CanUpgrade(item)) { text = "Ya mejorada"; color = Colors.TextSecondary; }
            else { var price = run.UpgradePrice(item); text = price + " de oro"; color = run.Gold >= price ? Gold : TooExpensive; }
            Game.Fonts.Get(15).DrawCentered(canvas, text, rect.CenterX, rect.Y + 24, color);
            var shown = Shown(item);
            CardWidget.Draw(canvas, Game, shown, rect.X + 20, rect.Y + 44, CardBonus.ForRun(run).For(shown));
            if (!run.CanBuyUpgrade(item)) canvas.Fill(new RectF(rect.X + 20, rect.Y + 44, CardWidget.CardW, CardWidget.CardH), new Rgba(0, 0, 0, 130));
        }

        public override void Draw(Canvas canvas)
        {
            base.Draw(canvas);
            var on = ShowUpgraded;
            canvas.Rect(ToggleRect, on ? new Rgba(30, 110, 85) : Colors.BgDark, 0, 6);
            canvas.Rect(ToggleRect, on ? Accent : Colors.PanelBorder, 1, 6);
            Game.Fonts.Get(15).DrawCentered(canvas, "Ver mejoras (V): " + (on ? "Sí" : "No"), ToggleRect.CenterX, ToggleRect.CenterY, Colors.TextPrimary);
        }

        protected override TooltipContent TooltipOf(CardDefinition item)
        {
            var shown = Shown(item); var s = CardBonus.ForRun(run).For(shown);
            var tip = Tooltip.Card(Game, shown, s.BonusDamage, s.BonusBlock);
            var lines = RunState.CanUpgrade(item)
                ? new List<string> { "Al mejorarla: " + Upgrades.Describe(item), "Precio: " + run.UpgradePrice(item) + " de oro" }
                : new List<string> { "Ya está mejorada al máximo." };
            lines.Add(""); lines.AddRange(tip.Lines);
            return new TooltipContent(tip.Title) { Lines = lines, Icon = tip.Icon, Subtitle = tip.Subtitle, Panels = tip.Panels };
        }
    }

    /// <summary>scenes/warlock_scene.py: El Brujo upgrades cards for gold.</summary>
    public sealed class WarlockScene : Scene
    {
        private static readonly Rgba Accent = new Rgba(80, 210, 170), Gold = new Rgba(235, 195, 70), TooExpensive = new Rgba(220, 90, 80);
        private static readonly IRect PanelRect = new IRect(290, 110, 700, 500);
        private const int BtnW = 200, BtnH = 44;
        private readonly RunState run;
        private readonly UpgradeGrid grid;
        private int? selected;
        private IRect? confirmRect, cancelRect;
        private string message = ""; private bool messageOk = true;
        public bool Cleared { get; set; }
        public int? SelectedIndex => selected;
        public override RectF? PauseButtonRect => new RectF(78, 652, 64, 36);
        public override (Anchor anchor, float x, float y)? GoldHudPos => (Anchor.BottomRight, 1268, 708);
        public override bool HasOverlay => selected.HasValue;

        public WarlockScene(GameContext game, RunState run) : base(game) { this.run = run; grid = new UpgradeGrid(game, run); }

        public void Select(int index)
        {
            if (index < 0 || index >= run.Deck.Count) return;
            var card = run.Deck[index];
            if (!RunState.CanUpgrade(card)) { Say(card.Name + " ya no se puede mejorar", false); Sound.PlayError(); return; }
            selected = index; grid.ClearMouse(); Sound.PlayConfirm();
        }

        public bool Confirm()
        {
            if (!selected.HasValue) return false;
            var oldName = run.Deck[selected.Value].Name;
            if (run.BuyUpgrade(selected.Value))
            {
                Say(oldName + " → " + run.Deck[selected.Value].Name); Sound.PlayPurchase(); selected = null; grid.Sync();
                return true;
            }
            Say("No tienes suficiente oro", false); Sound.PlayError();
            return false;
        }

        public void Cancel() { if (selected.HasValue) { selected = null; Sound.PlayCancel(); } }
        private void Say(string text, bool ok = true) { message = text; messageOk = ok; }

        public override void HandleEvent(in InputEvent e)
        {
            if (Cleared) return;
            if (selected.HasValue)
            {
                if (e.Type == EventType.KeyDown)
                {
                    if (e.Key == Key.Escape) Cancel();
                    else if (e.IsConfirmKey) Confirm();
                }
                else if (e.IsLeftDown)
                {
                    if (confirmRect.HasValue && confirmRect.Value.Contains(e.X, e.Y)) Confirm();
                    else if (cancelRect.HasValue && cancelRect.Value.Contains(e.X, e.Y) || !PanelRect.Contains(e.X, e.Y)) Cancel();
                }
                return;
            }
            grid.HandleEvent(e);
            if (grid.Clicked.HasValue) { var i = grid.Clicked.Value; grid.Clicked = null; Select(i); }
            if (grid.Dismissed) { Cleared = true; Sound.PlayCancel(); }
        }

        public override void Draw(Canvas canvas)
        {
            grid.Draw(canvas);
            if (message.Length > 0) Txt.DrawAt(canvas, Fonts.Get(15), message, Anchor.MidRight, 1100, 104, messageOk ? Accent : TooExpensive);
            if (selected.HasValue) DrawConfirm(canvas, run.Deck[selected.Value]);
        }

        private void DrawConfirm(Canvas canvas, CardDefinition card)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(0, 0, 0, 170));
            canvas.Rect(PanelRect, Colors.BgPanel, 0, 12); canvas.Rect(PanelRect, Accent, 2, 12);
            var cx = PanelRect.CenterX;
            Fonts.Get(24).DrawMidTop(canvas, "Mejorar carta", cx, PanelRect.Y + 18, Accent);
            var preview = card.Upgrade(); var top = PanelRect.Y + 70; var bonus = CardBonus.ForRun(run);
            CardWidget.Draw(canvas, Game, card, cx - 60 - CardWidget.CardW, top, bonus.For(card));
            CardWidget.Draw(canvas, Game, preview, cx + 60, top, bonus.For(preview));
            Fonts.Get(40).DrawCentered(canvas, "→", cx, top + CardWidget.CardH / 2, Accent);
            var y = top + CardWidget.CardH + 22;
            Fonts.Get(16).DrawMidTop(canvas, "Mejora: " + Upgrades.Describe(card), cx, y, Colors.TextPrimary);
            var price = run.UpgradePrice(card); var affordable = run.Gold >= price;
            Fonts.Get(15).DrawMidTop(canvas, "Precio: " + price + " de oro  ·  Tienes: " + run.Gold, cx, y + 28, affordable ? Gold : TooExpensive);
            var by = PanelRect.Bottom - BtnH - 22;
            var ok = new IRect(cx - BtnW - 12, by, BtnW, BtnH); var no = new IRect(cx + 12, by, BtnW, BtnH);
            confirmRect = ok; cancelRect = no;
            canvas.Rect(ok, affordable ? new Rgba(30, 110, 85) : new Rgba(50, 50, 55), 0, 6);
            canvas.Rect(ok, affordable ? Accent : Colors.PanelBorder, 2, 6);
            canvas.Rect(no, Colors.BgDark, 0, 6); canvas.Rect(no, Colors.PanelBorder, 1, 6);
            Fonts.Get(16).DrawCentered(canvas, "Mejorar (" + price + ")", ok.CenterX, ok.CenterY, affordable ? Colors.TextPrimary : Colors.TextSecondary);
            Fonts.Get(16).DrawCentered(canvas, "Cancelar", no.CenterX, no.CenterY, Colors.TextSecondary);
        }
    }
}
