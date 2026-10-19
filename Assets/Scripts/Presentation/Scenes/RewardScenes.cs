using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>scenes/combat_reward_scene.py: gold + pick one of the offered cards (and the elite's relic).</summary>
    public sealed class CombatRewardScene : Scene
    {
        private static readonly Rgba Bg = new Rgba(12, 18, 12), RelicGold = new Rgba(220, 190, 60);
        private const int Gap = 30, RelicW = 600, RelicH = 70;
        private readonly RunState run;
        private readonly CardBonus bonus;
        private readonly int goldEarned, interest;
        private readonly List<OfferedCard> cards;
        private readonly List<RectF> cardRects = new List<RectF>();
        private int? hovered;
        private RectF? skipRect, relicRect;
        private (float x, float y) mouse;
        private float t;
        public RelicOffer Relic { get; }
        public bool Cleared { get; set; }
        public CardDefinition ChosenCard { get; private set; }

        public CombatRewardScene(GameContext game, RunState run, int goldEarned, List<OfferedCard> cards, RelicOffer relic = null) : base(game)
        {
            this.run = run; bonus = CardBonus.ForRun(run); this.goldEarned = goldEarned; interest = run.LastInterest; this.cards = cards; Relic = relic;
        }

        public override void HandleEvent(in InputEvent e)
        {
            if (e.Type == EventType.MouseMove) { mouse = e.Pos; UpdateHover(e.X, e.Y); }
            else if (e.IsLeftDown) Click(e.X, e.Y);
        }
        public override void Update(float dt) => t += Math.Max(0f, Math.Min(dt, 0.1f));

        public override void Draw(Canvas canvas)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), Bg);
            const int cx = 640;
            Fonts.Get(28).DrawCentered(canvas, "¡VICTORIA!", cx, 60, Colors.TextAccent);
            Fonts.Get(18).DrawCentered(canvas, "Oro obtenido: +" + goldEarned + (interest != 0 ? "  (+" + interest + " de interés)" : ""), cx, 108, new Rgba(220, 190, 50));
            int promptY = 148, cardY = 190;
            if (Relic != null) { DrawRelic(canvas, cx); promptY = 228; cardY = 266; }
            Fonts.Get(14).DrawCentered(canvas, "Elige una carta para añadir a tu mazo:", cx, promptY, Colors.TextPrimary);
            if (Relic != null)
            {
                Fonts.Get(11).DrawCentered(canvas, "Cartas de élite: algo más de probabilidad de ser raras", cx, promptY + 18, Colors.TextSecondary);
                cardY += 8;
            }
            var n = cards.Count; var totalW = n * CardWidget.CardW + Math.Max(0, n - 1) * Gap; var startX = cx - totalW / 2;
            cardRects.Clear();
            for (var i = 0; i < n; i++)
            {
                var x = startX + i * (CardWidget.CardW + Gap);
                var offer = cards[i];
                if (offer.Lucky) CardFx.DrawLuckyBack(canvas, Art, new RectF(x, cardY - (hovered == i ? 20 : 0), CardWidget.CardW, CardWidget.CardH), t);
                var rect = CardWidget.Draw(canvas, Game, offer.Card, x, cardY, bonus.For(offer.Card), hovered: hovered == i);
                if (offer.Lucky) CardFx.DrawLuckyFront(canvas, Game, rect, t);
                cardRects.Add(rect);
            }
            var font = Fonts.Get(14); var sw = font.Width("Omitir"); var sh = font.Height;
            var sx = cx - sw / 2; var sy = cardY + CardWidget.CardH + 40 - sh / 2;
            skipRect = new RectF(sx - 10, sy - 6, sw + 20, sh + 12);
            canvas.Rect(skipRect.Value, Colors.BgPanel, 0, 5); canvas.Rect(skipRect.Value, Colors.PanelBorder, 1, 5);
            font.Draw(canvas, "Omitir", sx, sy, Colors.TextSecondary);
            LuckBadge.Draw(canvas, Game, Anchor.BottomLeft, 16, 704, LuckReport.For(run));
            if (Relic != null && relicRect.HasValue && relicRect.Value.Contains(mouse)) Tooltip.Draw(canvas, Game, Tooltip.Relic(Game, Relic.Definition, Relic.Chroma), mouse);
            else if (hovered.HasValue && hovered.Value < cards.Count)
            {
                var s = bonus.For(cards[hovered.Value].Card);
                Tooltip.Draw(canvas, Game, Tooltip.Card(Game, cards[hovered.Value].Card, s.BonusDamage, s.BonusBlock), mouse);
            }
        }

        private void DrawRelic(Canvas canvas, int cx)
        {
            var box = new IRect(cx - RelicW / 2, 136, RelicW, RelicH);
            relicRect = box;
            canvas.Rect(box, Colors.BgPanel, 0, 8); canvas.Rect(box, RelicGold, 2, 8);
            Fonts.Get(11).DrawMidTop(canvas, "RELIQUIA OBTENIDA", box.CenterX, box.Y + 6, Colors.TextSecondary);
            var nf = Fonts.Get(16);
            nf.DrawMidTop(canvas, TextFit.Fit(nf, ChromaFx.Title(Relic.Definition.Name, Relic.Chroma), box.W - 24), box.CenterX, box.Y + 20, RelicGold);
            var df = Fonts.Get(13);
            df.DrawMidTop(canvas, TextFit.Fit(df, Relic.Definition.Description, box.W - 24), box.CenterX, box.Y + 44, Colors.TextPrimary);
            if (Relic.Chroma != ChromaKind.None) ChromaFx.DrawBox(canvas, Art, box, Relic.Chroma, Game.Time, 8);
        }

        private void UpdateHover(float x, float y)
        {
            var previous = hovered; hovered = null;
            for (var i = 0; i < cardRects.Count; i++)
                if (cardRects[i].Contains(x, y)) { hovered = i; if (i != previous) Sound.PlayNav(); return; }
        }

        private void Click(float x, float y)
        {
            if (Cleared) return;
            for (var i = 0; i < cardRects.Count; i++)
                if (cardRects[i].Contains(x, y)) { Sound.PlayReward(); ChosenCard = cards[i].Card; Cleared = true; return; }
            if (skipRect.HasValue && skipRect.Value.Contains(x, y)) { Sound.PlayCancel(); ChosenCard = null; Cleared = true; }
        }
    }

    /// <summary>scenes/treasure_scene.py: take one of the relics or skip.</summary>
    public sealed class TreasureScene : Scene
    {
        private static readonly Rgba Bg = new Rgba(14, 10, 6), Gold = new Rgba(210, 170, 30), Picked = new Rgba(255, 225, 120);
        private const int BtnW = 160, BtnH = 40, BoxH = 220, Gap = 30;
        private readonly List<RelicOffer> relics;
        private int selected;
        private RectF? takeRect, skipRect;
        private readonly List<IRect> relicRects = new List<IRect>();
        private (float x, float y) mouse;
        public bool Cleared { get; set; }
        public bool TookRelic { get; private set; }
        public RelicOffer ChosenRelic { get; private set; }

        public TreasureScene(GameContext game, RunState run, List<RelicOffer> relics) : base(game) { this.relics = relics; }
        public static int BoxWidth(int count) => count <= 1 ? 400 : count == 2 ? 300 : 260;

        public override void HandleEvent(in InputEvent e)
        {
            if (e.Type == EventType.MouseMove) mouse = e.Pos;
            else if (e.IsLeftDown) Click(e.X, e.Y);
        }

        public override void Draw(Canvas canvas)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), Bg);
            const int cx = 640;
            Fonts.Get(26).DrawCentered(canvas, "Sala del Tesoro", cx, 70, Gold);
            if (relics.Count > 1) Fonts.Get(14).DrawCentered(canvas, "Elige una reliquia", cx, 104, Colors.TextSecondary);
            var n = relics.Count; var w = BoxWidth(n); var x0 = cx - (n * w + (n - 1) * Gap) / 2;
            relicRects.Clear();
            for (var i = 0; i < n; i++)
            {
                var box = new IRect(x0 + i * (w + Gap), 130, w, BoxH);
                relicRects.Add(box);
                DrawRelic(canvas, relics[i], box, n > 1 && i == selected);
            }
            var take = new IRect(cx - BtnW - 16, 390, BtnW, BtnH); var skip = new IRect(cx + 16, 390, BtnW, BtnH);
            takeRect = take; skipRect = skip;
            canvas.Rect(take, new Rgba(50, 120, 50), 0, 6); canvas.Rect(take, new Rgba(60, 150, 60), 2, 6);
            canvas.Rect(skip, Colors.BgPanel, 0, 6); canvas.Rect(skip, Colors.PanelBorder, 1, 6);
            Fonts.Get(15).DrawCentered(canvas, "Tomar", take.CenterX, take.CenterY, Colors.TextPrimary);
            Fonts.Get(15).DrawCentered(canvas, "Omitir", skip.CenterX, skip.CenterY, Colors.TextSecondary);
            for (var i = 0; i < relicRects.Count; i++)
                if (relicRects[i].Contains(mouse.x, mouse.y)) { Tooltip.Draw(canvas, Game, Tooltip.Relic(Game, relics[i].Definition, relics[i].Chroma), mouse); break; }
        }

        private void DrawRelic(Canvas canvas, RelicOffer relic, IRect box, bool picked)
        {
            canvas.Rect(box, Colors.BgPanel, 0, 8);
            canvas.Rect(box, picked ? Picked : Gold, picked ? 3 : 2, 8);
            Fonts.Get(18).DrawCentered(canvas, ChromaFx.Title(relic.Definition.Name, relic.Chroma), box.CenterX, box.Y + 60, new Rgba(220, 190, 60));
            var font = Fonts.Get(13); var y = box.Y + 100;
            var lines = RelicViewer.CardWrap(relic.Definition.Description, font, box.W - 30);
            for (var i = 0; i < Math.Min(4, lines.Count); i++) { font.DrawMidTop(canvas, lines[i], box.CenterX, y, Colors.TextPrimary); y += font.LineSize; }
            if (relic.Chroma != ChromaKind.None)
            {
                var st = ChromaFx.Style(relic.Chroma);
                font.DrawCentered(canvas, st.RelicNote, box.CenterX, box.Bottom - 25, st.Main);
                ChromaFx.DrawBox(canvas, Art, box, relic.Chroma, Game.Time, 8);
            }
        }

        private void Click(float x, float y)
        {
            if (Cleared) return;
            for (var i = 0; i < relicRects.Count; i++)
                if (relicRects[i].Contains(x, y)) { if (i != selected) { selected = i; Sound.PlayNav(); } return; }
            if (takeRect.HasValue && takeRect.Value.Contains(x, y)) { Sound.PlayReward(); TookRelic = true; ChosenRelic = relics[selected]; Cleared = true; }
            else if (skipRect.HasValue && skipRect.Value.Contains(x, y)) { Sound.PlayCancel(); TookRelic = false; ChosenRelic = null; Cleared = true; }
        }
    }
}
