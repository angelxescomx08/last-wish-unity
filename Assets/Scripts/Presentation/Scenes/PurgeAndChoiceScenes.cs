using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>fx/card_burn.py: a card burns away in 6-px blocks from the bottom up, with an ember edge.</summary>
    public sealed class CardBurn
    {
        public const int Cell = 6; public const double Edge = 0.09; public const float BurnSeconds = 1.6f;
        public static readonly Rgba[] Ember = { new Rgba(255, 250, 220), new Rgba(255, 200, 90), new Rgba(240, 120, 30), new Rgba(150, 40, 20) };
        public readonly Sprite Face; public readonly int W, H, Cols, Rows;
        private readonly double[,] threshold;

        public CardBurn(Sprite face, int w, int h, int seed = 0)
        {
            Face = face; W = w; H = h;
            var rng = new PyRandom(seed);
            Cols = Math.Max(1, (w + Cell - 1) / Cell); Rows = Math.Max(1, (h + Cell - 1) / Cell);
            threshold = new double[Rows, Cols];
            for (var r = 0; r < Rows; r++) for (var c = 0; c < Cols; c++) threshold[r, c] = 0.72 * (1.0 - (r + 0.5) / Rows) + 0.28 * rng.Random();
        }
        public bool Gone(double progress) => progress >= 1.0 + Edge;

        /// <summary>Draw the card at <paramref name="progress"/> with its top-left at (x, y): runs of intact blocks are one quad.</summary>
        public void Draw(Canvas canvas, int x, int y, double progress)
        {
            float su = Face.W / (float)W, sv = Face.H / (float)H;
            for (var r = 0; r < Rows; r++)
            {
                var top = r * Cell; var bh = Math.Min(Cell, H - top);
                var c = 0;
                while (c < Cols)
                {
                    var t = threshold[r, c];
                    if (progress > 0 && t < progress) { c++; continue; }
                    if (progress > 0 && t < progress + Edge)
                    {
                        var k = (t - progress) / Edge;
                        var bw0 = Math.Min(Cell, W - c * Cell);
                        canvas.Fill(new RectF(x + c * Cell, y + top, bw0, bh), Ember[Math.Min(Ember.Length - 1, (int)(k * Ember.Length))]);
                        c++; continue;
                    }
                    var start = c;
                    while (c < Cols && (progress <= 0 || threshold[r, c] >= progress + Edge)) c++;
                    var left = start * Cell; var width = Math.Min(c * Cell, W) - left;
                    canvas.BlitRect(Face.Sub((int)(left * su), (int)(top * sv), Math.Max(1, (int)Math.Round(width * su)), Math.Max(1, (int)Math.Round(bh * sv))),
                        new RectF(x + left, y + top, width, bh), Rgba.White);
                }
            }
        }

        public List<(int x, int y)> EdgePoints(double progress, int limit = 12)
        {
            var pts = new List<(int, int)>();
            for (var r = 0; r < Rows; r++) for (var c = 0; c < Cols; c++)
            { var t = threshold[r, c]; if (progress <= t && t < progress + Edge * 0.5) pts.Add((c * Cell + Cell / 2, r * Cell + Cell / 2)); }
            if (pts.Count > limit)
            {
                var step = pts.Count / (double)limit; var picked = new List<(int, int)>();
                for (var i = 0; i < limit; i++) picked.Add(pts[(int)(i * step)]);
                return picked;
            }
            return pts;
        }
    }

    internal sealed class PurgeGrid : PileViewer
    {
        private static readonly Rgba Gold = new Rgba(235, 195, 70), Bad = new Rgba(220, 90, 80);
        private readonly RunState run;
        public int? Clicked;
        public override string CloseLabel => "Salir";
        public override string HintText => "Haz clic en la carta que quieras eliminar  ·  Rueda / ↑ ↓ para desplazarte";
        public override bool CloseOnOutsideClick => false;
        public override string FooterText => "Solo puedes eliminar una carta en este altar  ·  Precio: " + run.PurgePrice + " de oro  ·  Siempre te queda al menos 1 carta";
        public PurgeGrid(GameContext game, RunState run) : base(game, "Altar de Purga", run.Deck, CardBonus.ForRun(run)) { this.run = run; }
        public void Sync() { Items.Clear(); Items.AddRange(run.Deck); Recount(); }
        public void ClearMouse() => MousePos = (-1, -1);

        public override void HandleEvent(in InputEvent e)
        {
            if (e.IsLeftDown && Viewport.Contains(e.X, e.Y))
                foreach (var pair in ItemRects) if (pair.Value.Contains(e.X, e.Y)) { Clicked = pair.Key; return; }
            base.HandleEvent(e);
        }

        protected override void DrawItem(Canvas canvas, CardDefinition item, IRect rect, int index)
        {
            var ok = run.CanPurge;
            Game.Fonts.Get(15).DrawCentered(canvas, "Eliminar · " + run.PurgePrice, rect.CenterX, rect.Y + 24, ok ? Gold : Bad);
            CardWidget.Draw(canvas, Game, item, rect.X + 20, rect.Y + 44, CardBonus.ForRun(run).For(item));
            if (!ok) canvas.Fill(new RectF(rect.X + 20, rect.Y + 44, CardWidget.CardW, CardWidget.CardH), new Rgba(0, 0, 0, 130));
        }

        protected override TooltipContent TooltipOf(CardDefinition item)
        {
            var s = CardBonus.ForRun(run).For(item); var tip = Tooltip.Card(Game, item, s.BonusDamage, s.BonusBlock);
            var reason = run.PurgeBlockReason();
            var lines = new List<string> { reason.Length > 0 ? reason : "Eliminar por " + run.PurgePrice + " de oro (para siempre)", "" };
            lines.AddRange(tip.Lines);
            return new TooltipContent(tip.Title) { Lines = lines, Icon = tip.Icon, Subtitle = tip.Subtitle, Panels = tip.Panels };
        }
    }

    /// <summary>scenes/purge_scene.py: Altar de Purga, one card per altar, then it burns.</summary>
    public sealed class PurgeScene : Scene
    {
        private static readonly Rgba Accent = new Rgba(240, 130, 50), Gold = new Rgba(235, 195, 70), Bad = new Rgba(220, 90, 80);
        private static readonly IRect PanelRect = new IRect(390, 100, 500, 520);
        private const int BtnW = 200, BtnH = 44; private const float BurnScale = 1.5f, AfterBurn = 0.7f;
        private const int BurnCx = 640, BurnCy = 330;
        private readonly RunState run; private readonly PurgeGrid grid;
        private int? selected; private IRect? confirmRect, cancelRect; private string message = "";
        private CardBurn burn; private float burnT; private string burntName = "";
        private readonly BurstParticles particles = new BurstParticles(500, 17);
        private readonly PyRandom rng = new PyRandom(23);
        public CardDefinition Removed { get; private set; }
        public bool Cleared { get; set; }
        public bool Burning => burn != null;
        public int? SelectedIndex => selected;
        public override RectF? PauseButtonRect => new RectF(78, 652, 64, 36);
        public override (Anchor anchor, float x, float y)? GoldHudPos => (Anchor.BottomRight, 1268, 708);
        public override bool HasOverlay => selected.HasValue || burn != null;

        public PurgeScene(GameContext game, RunState run) : base(game) { this.run = run; grid = new PurgeGrid(game, run); }

        public void Select(int index)
        {
            if (burn != null || index < 0 || index >= run.Deck.Count) return;
            var reason = run.PurgeBlockReason();
            if (reason.Length > 0) { message = reason; Sound.PlayError(); return; }
            selected = index; grid.ClearMouse(); Sound.PlayConfirm();
        }

        public bool Confirm()
        {
            if (!selected.HasValue) return false;
            var card = run.Deck[selected.Value];
            var face = CardWidget.Face(Game, card, CardBonus.ForRun(run).For(card));
            var removed = run.PurgeCard(selected.Value);
            if (removed == null) { var r = run.PurgeBlockReason(); message = r.Length > 0 ? r : "No se pudo eliminar"; Sound.PlayError(); return false; }
            Removed = removed; selected = null; grid.Sync();
            burn = new CardBurn(face, MathX.Round(CardWidget.CardW * BurnScale), MathX.Round(CardWidget.CardH * BurnScale), run.Deck.Count);
            burnT = 0; burntName = removed.Name;
            Sound.PlayPurchase(); Sound.PlayDeath();
            return true;
        }

        public void Cancel() { if (selected.HasValue) { selected = null; Sound.PlayCancel(); } }
        public void SkipBurn() { if (burn != null) burnT = CardBurn.BurnSeconds + AfterBurn; }

        public override void HandleEvent(in InputEvent e)
        {
            if (Cleared) return;
            if (burn != null) { if (e.Type == EventType.MouseDown || e.Type == EventType.KeyDown) SkipBurn(); return; }
            if (selected.HasValue)
            {
                if (e.Type == EventType.KeyDown) { if (e.Key == Key.Escape) Cancel(); else if (e.IsConfirmKey) Confirm(); }
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

        public override void Update(float dt)
        {
            dt = Math.Max(0f, Math.Min(dt, 0.1f));
            particles.Update(dt);
            if (burn == null) return;
            burnT += dt;
            var progress = burnT / CardBurn.BurnSeconds;
            int x0 = BurnCx - burn.W / 2, y0 = BurnCy - burn.H / 2;
            foreach (var (px, py) in burn.EdgePoints(progress, 6))
            {
                var vx = rng.Uniform(-30, 30); var vy = rng.Uniform(-160, -60); var life = rng.Uniform(0.5, 1.1); var size = rng.Uniform(1.5, 3.0);
                particles.Emit(x0 + px, y0 + py, (float)vx, (float)vy, (float)life, CardBurn.Ember, (float)size, ParticleStyle.Square, 1.2f);
            }
            if (burnT >= CardBurn.BurnSeconds + AfterBurn) { burn = null; Cleared = true; }
        }

        public override void Draw(Canvas canvas)
        {
            grid.Draw(canvas);
            if (message.Length > 0) Txt.DrawAt(canvas, Fonts.Get(15), message, Anchor.MidRight, 1100, 104, Bad);
            if (selected.HasValue) DrawConfirm(canvas, run.Deck[selected.Value]);
            if (burn != null) DrawBurn(canvas);
            particles.Draw(canvas, Art);
        }

        private void DrawConfirm(Canvas canvas, CardDefinition card)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(0, 0, 0, 170));
            canvas.Rect(PanelRect, Colors.BgPanel, 0, 12); canvas.Rect(PanelRect, Accent, 2, 12);
            var cx = PanelRect.CenterX;
            Fonts.Get(24).DrawMidTop(canvas, "Eliminar carta", cx, PanelRect.Y + 18, Accent);
            var top = PanelRect.Y + 66;
            CardWidget.Draw(canvas, Game, card, cx - CardWidget.CardW / 2, top, CardBonus.ForRun(run).For(card));
            var y = top + CardWidget.CardH + 20;
            Fonts.Get(16).DrawMidTop(canvas, "Sale de tu mazo para siempre.", cx, y, Colors.TextPrimary);
            var price = run.PurgePrice;
            Fonts.Get(15).DrawMidTop(canvas, "Precio: " + price + " de oro  ·  Tienes: " + run.Gold, cx, y + 28, Gold);
            Fonts.Get(13).DrawMidTop(canvas, "Solo una carta por altar", cx, y + 52, Colors.TextSecondary);
            var by = PanelRect.Bottom - BtnH - 22;
            var ok = new IRect(cx - BtnW - 12, by, BtnW, BtnH); var no = new IRect(cx + 12, by, BtnW, BtnH);
            confirmRect = ok; cancelRect = no;
            canvas.Rect(ok, new Rgba(120, 50, 20), 0, 6); canvas.Rect(ok, Accent, 2, 6);
            canvas.Rect(no, Colors.BgDark, 0, 6); canvas.Rect(no, Colors.PanelBorder, 1, 6);
            Fonts.Get(16).DrawCentered(canvas, "Eliminar (" + price + ")", ok.CenterX, ok.CenterY, Colors.TextPrimary);
            Fonts.Get(16).DrawCentered(canvas, "Cancelar", no.CenterX, no.CenterY, Colors.TextSecondary);
        }

        private void DrawBurn(Canvas canvas)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(0, 0, 0, 190));
            var progress = burnT / CardBurn.BurnSeconds;
            var flicker = 0.5 + 0.5 * Math.Sin(burnT * 23.0);
            var heat = Math.Max(0.0, 1.0 - Math.Abs(progress - 0.5) * 1.6) * (0.75 + 0.25 * flicker);
            var level = Math.Max(1, Math.Min(8, MathX.Round(8 * heat)));
            Glows.Draw(canvas, Art, BurnCx, BurnCy, 220, new Rgba(150 * level / 8, 60 * level / 8, 18 * level / 8));
            if (!burn.Gone(progress)) burn.Draw(canvas, BurnCx - burn.W / 2, BurnCy - burn.H / 2, progress);
            var below = BurnCy + CardWidget.CardH * BurnScale / 2;
            Fonts.Get(22).DrawMidTop(canvas, burntName + " se consume en el altar", 640, (int)(below + 24), Accent);
            Fonts.Get(13).DrawMidTop(canvas, "Clic para continuar", 640, (int)(below + 56), Colors.TextSecondary);
        }
    }

    internal sealed class ChoiceGrid : PileViewer
    {
        private static readonly Rgba Gold = new Rgba(235, 195, 70);
        private readonly DeckChoiceScene scene;
        public int? Clicked;
        public override string CloseLabel => "Confirmar";
        public override bool CloseOnOutsideClick => false;
        public override string HintText => "Elige hasta " + scene.Limit + " carta" + (scene.Limit != 1 ? "s" : "") + " para " + (scene.Kind == "remove" ? "eliminar" : scene.Kind == "innate" ? "marcar" : "elegir")
            + "  ·  Clic para marcar o desmarcar  ·  Rueda / ↑ ↓";
        public override string FooterText => scene.Kind == "remove"
            ? "Elegidas: " + scene.Selected.Count + "/" + scene.Limit + "  ·  Salen de tu mazo para siempre  ·  Pulsa Confirmar"
            : "Elegidas: " + scene.Selected.Count + "/" + scene.Limit + "  ·  Empezarás cada combate con ellas en la mano";

        public ChoiceGrid(GameContext game, DeckChoiceScene scene, RunState run)
            : base(game, scene.Kind == "remove" ? "Tijeras del Sastre" : scene.Kind == "innate" ? "Cartas Marcadas" : "Elige cartas", run.Deck, CardBonus.ForRun(run)) { this.scene = scene; }

        public override void HandleEvent(in InputEvent e)
        {
            if (e.IsKey(Key.Escape)) return;
            if (e.IsLeftDown && Viewport.Contains(e.X, e.Y))
                foreach (var pair in ItemRects) if (pair.Value.Contains(e.X, e.Y)) { Clicked = pair.Key; return; }
            base.HandleEvent(e);
        }

        protected override void DrawItem(Canvas canvas, CardDefinition item, IRect rect, int index)
        {
            var chosen = scene.Selected.Contains(index);
            Game.Fonts.Get(15).DrawCentered(canvas, chosen ? "✓ Elegida" : " ", rect.CenterX, rect.Y + 24, Gold);
            CardWidget.Draw(canvas, Game, item, rect.X + 20, rect.Y + 44, Bonus.For(item));
            if (chosen) canvas.Rect(new RectF(rect.X + 16, rect.Y + 40, CardWidget.CardW + 8, CardWidget.CardH + 8), Gold, 3, 10);
        }
    }

    /// <summary>scenes/deck_choice_scene.py: Tijeras del Sastre (remove) / Cartas Marcadas (innate).</summary>
    public sealed class DeckChoiceScene : Scene
    {
        private readonly RunState run; private readonly ChoiceGrid grid;
        public string Kind { get; }
        public int Limit { get; }
        public List<int> Selected { get; } = new List<int>();
        public bool Cleared { get; set; }
        public override RectF? PauseButtonRect => new RectF(78, 652, 64, 36);
        public override (Anchor anchor, float x, float y)? GoldHudPos => (Anchor.BottomRight, 1268, 708);

        public DeckChoiceScene(GameContext game, RunState run, string kind, int count) : base(game)
        {
            this.run = run; Kind = kind; Limit = run.ChoiceLimit(kind, count);
            grid = new ChoiceGrid(game, this, run);
        }

        public void Toggle(int index)
        {
            if (index < 0 || index >= run.Deck.Count) return;
            if (Selected.Contains(index)) { Selected.Remove(index); Sound.PlayCancel(); }
            else if (Selected.Count < Limit) { Selected.Add(index); Sound.PlayConfirm(); }
            else Sound.PlayError();
        }

        public void Confirm() { run.ApplyChoice(Kind, Selected); Cleared = true; Sound.PlayPurchase(); }

        public override void HandleEvent(in InputEvent e)
        {
            if (Cleared) return;
            if (e.Type == EventType.KeyDown && (e.Key == Key.Return || e.Key == Key.KeypadEnter)) { Confirm(); return; }
            grid.HandleEvent(e);
            if (grid.Clicked.HasValue) { var i = grid.Clicked.Value; grid.Clicked = null; Toggle(i); }
            if (grid.Dismissed) { grid.Dismissed = false; Confirm(); }
        }

        public override void Draw(Canvas canvas)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), Colors.BgDark);
            grid.Draw(canvas);
        }
    }
}
