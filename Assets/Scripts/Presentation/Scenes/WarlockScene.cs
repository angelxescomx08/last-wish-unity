using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>
    /// El Brujo's room (redesigned 2026-11-04, same language as the shop, gachapón and events): the dungeon behind a green
    /// focus, a title band, El Brujo himself by his bubbling cauldron (animated, code-drawn: Tools/VfxLab/warlock.html) with
    /// his speech bubble and his price list, and your deck in an iron panel where every card has its price as a kit button.
    /// Picking a card opens a "now → upgraded" panel with the changes as chips; confirming makes him raise his staff, a green
    /// bolt flies from the flame to the card and the upgraded card bursts into view. Rules and prices are the domain's
    /// (<see cref="RunState.UpgradePrice"/>, <see cref="RunState.BuyUpgrade"/>): each card is upgraded once.
    /// </summary>
    public sealed class WarlockScene : Scene
    {
        public enum Phase { Browse, Confirm, Upgrading }

        // ------------------------------------------------------------------ layout
        public static readonly IRect Counter = new IRect(24, 16, 196, 36), Tariffs = new IRect(20, 140, 268, 166);
        public static readonly IRect DeckPanel = new IRect(312, 100, 952, 552), Viewport = new IRect(336, 162, 888, 480), Track = new IRect(1238, 162, 10, 480);
        public static readonly IRect ToggleRect = new IRect(1028, 116, 212, 34), ExitButton = new IRect(1102, 664, 150, 36);
        public static readonly IRect ConfirmPanel = new IRect(356, 112, 864, 532);
        public const int TileW = 148, TileH = 240, Columns = 6, ButtonH = 36, CardTop = 4;
        /// <summary>Where El Brujo stands (his feet) and his cauldron, left of the deck.</summary>
        public const int NpcX = 140, NpcGround = 642, BubbleMaxW = 210, CauldronX = 250;
        public const float CardScale = 1.25f, BoltT = 0.22f, HoldT = 1.15f;
        public static readonly int CardGap = 175;

        private static readonly Rgba Ink = Colors.Ink, Cream = Colors.Cream, Dim = new Rgba(186, 176, 160), Lime = new Rgba(178, 244, 150),
            Arcane = new Rgba(110, 230, 90), Gilt = new Rgba(255, 214, 120), Short = new Rgba(255, 140, 118);
        private static readonly Rgba[] Sparks = { Rgba.White, new Rgba(214, 255, 196), new Rgba(139, 224, 122), new Rgba(60, 154, 76) };

        // ------------------------------------------------------------------ state
        private readonly RunState run;
        private readonly DungeonBackdrop backdrop;
        private readonly NpcSheet sheet;
        private readonly WarlockSpeech speech;
        private readonly WarlockPose pose;
        private readonly BurstParticles fx = new BurstParticles(500, 37), steam = new BurstParticles(80, 53);
        private Phase phase = Phase.Browse;
        private int? selected, hovered;
        private bool toggleHovered, exitHovered, confirmHovered, cancelHovered, mouseDown;
        private (float x, float y) mouse = (-1, -1);
        private float scroll, scrollShown, time, feedbackTime, showT, glowT, bubbleTimer, confirmT, shakeT;
        private int? dragOffset, glowIndex, shakeTile; private bool shakeConfirm;
        private string feedbackText = ""; private Rgba feedbackColor = Lime;
        private CardDefinition before, after;
        private bool impacted;
        public bool Cleared { get; set; }
        public bool ShowUpgraded { get; set; }

        public WarlockScene(GameContext game, RunState run) : base(game)
        {
            this.run = run;
            backdrop = new DungeonBackdrop(Art, 43, 0.6f);
            sheet = WarlockSheet.Load(Art);
            var seed = (int)(run.Seed % 100003) * 37 + run.Floor * 11 + (run.CurrentRoomId ?? "").Length;
            speech = new WarlockSpeech(seed); pose = new WarlockPose(seed + 1);
            speech.Say(AnythingToUpgrade ? WarlockCue.Enter : WarlockCue.AllDone);
        }

        public override RectF? PauseButtonRect => null;
        public override bool HasOverlay => phase != Phase.Browse;
        public Phase Current => phase;
        public int? SelectedIndex => selected;
        public WarlockSpeech Speech => speech;
        public NpcSheet Npc => sheet;
        public WarlockPose Pose => pose;
        public bool Busy => phase == Phase.Upgrading;
        public float ShowTime => showT;
        public float Scroll => scroll;
        private bool AnythingToUpgrade => run.Deck.Any(RunState.CanUpgrade);

        // ------------------------------------------------------------------ geometry
        private int Rows => (run.Deck.Count + Columns - 1) / Columns;
        public float MaxScroll => Math.Max(0, Rows * TileH - Viewport.H);
        /// <summary>The tile of card <paramref name="i"/> in content coordinates moved by the scroll shown.</summary>
        public RectF TileRect(int i) => new RectF(Viewport.X + i % Columns * TileW, Viewport.Y + i / Columns * TileH - (float)Math.Round(scrollShown), TileW, TileH);
        public RectF CardRect(int i) { var t = TileRect(i); return new RectF(t.X + (TileW - CardWidget.CardW) / 2, t.Y + CardTop, CardWidget.CardW, CardWidget.CardH); }
        public RectF ButtonRect(int i) { var c = CardRect(i); return new RectF(c.X, c.Bottom + 6, CardWidget.CardW, ButtonH); }
        public static RectF ConfirmButton => new RectF(ConfirmPanel.CenterX - 272, ConfirmPanel.Bottom - 84, 340, 44);
        public static RectF CancelButton => new RectF(ConfirmPanel.CenterX + 92, ConfirmPanel.Bottom - 84, 180, 44);
        public static (float x, float y) OldCardCenter => (ConfirmPanel.CenterX - CardGap, ConfirmPanel.Y + 216);
        public static (float x, float y) NewCardCenter => (ConfirmPanel.CenterX + CardGap, ConfirmPanel.Y + 216);
        public RectF NpcRect => sheet.Rect(NpcX, NpcGround);

        private bool Visible(int i) { var r = TileRect(i); return r.Bottom > Viewport.Y && r.Y < Viewport.Bottom; }

        // ------------------------------------------------------------------ views (what is drawn; tests check that it fits)
        /// <summary>One card's tile: its price button or the "Mejorada" tag.</summary>
        public sealed class TileView
        {
            public RectF Tile, Card, Button; public int Price; public bool Upgraded, Affordable;
            public string Label; public int Size; public int Width;
        }

        public TileView Tile(int i)
        {
            var card = run.Deck[i];
            var v = new TileView { Tile = TileRect(i), Card = CardRect(i), Button = ButtonRect(i), Upgraded = !RunState.CanUpgrade(card) };
            if (v.Upgraded) { v.Label = "Mejorada"; v.Size = 16; v.Width = PixelUi.InkTextWidth(Art, v.Label, 16) + 28; return v; }
            v.Price = run.UpgradePrice(card); v.Affordable = run.Gold >= v.Price;
            (v.Label, v.Size, v.Width) = PriceLine(v.Price, v.Button.W - 20);
            return v;
        }

        /// <summary>"75 oro" in the chunky face with the coin before it, shrinking so it never leaves the button.</summary>
        public (string label, int size, int width) PriceLine(int price, float room)
        {
            string Num(int n) => n.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");
            foreach (var (label, size) in new[] { (Num(price) + " oro", 20), (Num(price), 20), (Num(price) + " oro", 16), (Num(price), 16) })
            {
                var w = 32 + PixelUi.InkTextWidth(Art, label, size);
                if (w <= room) return (label, size, w);
            }
            var cut = TextFit.Fit(Fonts.Get(16), Num(price), (int)room - 34);
            return (cut, 16, 32 + PixelUi.InkTextWidth(Art, cut, 16));
        }

        /// <summary>The confirmation panel: title, the two cards, the changes as chips, the price button.</summary>
        public sealed class ConfirmView
        {
            public string Title; public RectF Old, New; public List<(string icon, string text, Rgba color, RectF rect)> Chips = new List<(string, string, Rgba, RectF)>();
            public int Price; public bool Affordable; public string ButtonLabel; public int ButtonSize; public int ButtonWidth; public string Note; public Rgba NoteColor;
        }

        public ConfirmView Confirmation()
        {
            var v = new ConfirmView();
            if (!selected.HasValue) return v;
            var card = phase == Phase.Upgrading ? before : run.Deck[selected.Value];
            v.Title = TextFit.Fit(Fonts.Bold(22), "Mejorar " + card.Name, ConfirmPanel.W - 80);
            var (w, h) = CardWidget.CardSize(CardScale);
            v.Old = RectF.CenteredInt(OldCardCenter.x, OldCardCenter.y, w, h); v.New = RectF.CenteredInt(NewCardCenter.x, NewCardCenter.y, w, h);
            var changes = Upgrades.Changes(card);
            var widths = changes.Select(c => PixelUi.InkTextWidth(Art, c.text, 20) + 22 + (c.icon != null ? 32 : 0)).ToList();
            var rows = new List<List<int>> { new List<int>() }; float rowW = 0; var room = ConfirmPanel.W - 64;
            for (var k = 0; k < changes.Count; k++)
            {
                if (rows[rows.Count - 1].Count > 0 && rowW + 8 + widths[k] > room) { rows.Add(new List<int>()); rowW = 0; }
                rowW += (rows[rows.Count - 1].Count > 0 ? 8 : 0) + Math.Min(widths[k], room); rows[rows.Count - 1].Add(k);
            }
            var y = v.Old.Bottom + 22;
            foreach (var row in rows)
            {
                var total = row.Sum(k => Math.Min(widths[k], room)) + 8 * Math.Max(0, row.Count - 1);
                var x = (float)Math.Floor(ConfirmPanel.CenterX - total / 2f);
                foreach (var k in row) { var cw = Math.Min(widths[k], room); v.Chips.Add((changes[k].icon, changes[k].text, changes[k].color, new RectF(x, y, cw, 36))); x += cw + 8; }
                y += 42;
            }
            v.Price = run.UpgradePrice(card); v.Affordable = phase == Phase.Upgrading || run.Gold >= v.Price;
            var fixedW = PixelUi.InkTextWidth(Art, "Mejorar", 20) + 14 + 10 + PixelUi.KeycapWidth(Art, "Enter");
            var (label, size, width) = PriceLine(v.Price, ConfirmButton.W - 28 - fixedW);
            v.ButtonLabel = label; v.ButtonSize = size; v.ButtonWidth = fixedW + width;
            if (phase == Phase.Upgrading) { v.Note = impacted ? "Clic para continuar" : ""; v.NoteColor = Dim; }
            else if (v.Affordable) { v.Note = "Tienes " + run.Gold.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ") + " de oro · no se puede deshacer"; v.NoteColor = Dim; }
            else { v.Note = "Te faltan " + (v.Price - run.Gold) + " de oro"; v.NoteColor = Short; }
            return v;
        }

        /// <summary>The price list on the left: one row per rarity.</summary>
        public sealed class TariffView { public RectF Box; public List<(int rarity, string name, int price, int was, RectF row)> Rows = new List<(int, string, int, int, RectF)>(); public string Note; }

        public TariffView TariffList()
        {
            var v = new TariffView();
            var discount = run.UpgradeDiscount;
            v.Box = new RectF(Tariffs.X, Tariffs.Y, Tariffs.W, Tariffs.H + (discount ? 22 : 0));
            for (var r = 1; r <= 5; r++)
            {
                var price = run.UpgradePriceFor(r); var was = discount ? price * 2 : 0;
                v.Rows.Add((r, RunContent.RarityLabel(r), price, was, new RectF(Tariffs.X + 20, Tariffs.Y + 44 + (r - 1) * 22, Tariffs.W - 40, 22)));
            }
            if (discount) v.Note = "Martillo del Herrero: mitad de precio";
            return v;
        }

        public SpeechBubble.View Bubble(string text)
        {
            var r = NpcRect;
            return SpeechBubble.Layout(Fonts.Get(13), text, BubbleMaxW, sheet.ToScreen(r, sheet.Mouth), r.Y + sheet.Top * sheet.Scale, 16, DeckPanel.X - 14);
        }

        // ------------------------------------------------------------------ actions
        public void Select(int index)
        {
            if (index < 0 || index >= run.Deck.Count || phase != Phase.Browse) return;
            var card = run.Deck[index];
            if (!RunState.CanUpgrade(card))
            {
                speech.Say(WarlockCue.Maxed); ShowFeedback(card.Name + " ya está mejorada", true); shakeTile = index; shakeT = 0.35f; Sound.PlayError();
                return;
            }
            selected = index; phase = Phase.Confirm; confirmT = 0; hovered = null; impacted = false;
            speech.Say(WarlockCue.Select, card.Name);
            Sound.PlayConfirm();
        }

        public bool Confirm()
        {
            if (phase != Phase.Confirm || !selected.HasValue) return false;
            var i = selected.Value; var card = run.Deck[i]; var price = run.UpgradePrice(card);
            if (run.Gold < price)
            {
                speech.Say(WarlockCue.Poor, card.Name, price - run.Gold);
                ShowFeedback("Te faltan " + (price - run.Gold) + " de oro", true); shakeConfirm = true; shakeT = 0.35f; Sound.PlayError();
                return false;
            }
            before = card;
            if (!run.BuyUpgrade(i)) { Sound.PlayError(); return false; }
            after = run.Deck[i];
            phase = Phase.Upgrading; showT = 0; impacted = false;
            speech.Say(WarlockCue.Upgrade, before.Name);
            Sound.PlayPurchase();
            var b = ConfirmButton;
            fx.Burst(b.X + 40, b.CenterY, 18, new[] { Rgba.White, Gilt, new Rgba(230, 150, 30) }, (80f, 240f), ((float)Math.PI * 1.1f, (float)Math.PI * 1.9f), (0.35f, 0.7f), (2f, 3.5f), ParticleStyle.Square, 2.4f, 380f);
            return true;
        }

        public void Cancel()
        {
            if (phase != Phase.Confirm) return;
            phase = Phase.Browse; selected = null; Sound.PlayCancel();
            speech.Say(WarlockCue.Cancel);
        }

        /// <summary>Seconds into the show when the flame bursts (the cast's release frame) and when the bolt hits the card.</summary>
        public float ReleaseAt => sheet.Valid && sheet.Events.TryGetValue("cast", out var e) ? sheet.TimeOf("cast", e.frame) : 0.5f;
        public float ImpactAt => ReleaseAt + BoltT;

        private void Finish()
        {
            var i = selected ?? 0;
            phase = Phase.Browse; selected = null; glowIndex = i; glowT = 1.6f; impacted = false;
            ShowFeedback(before.Name + " → " + after.Name);
            if (!AnythingToUpgrade) speech.Say(WarlockCue.AllDone);
        }

        private void ShowFeedback(string text, bool error = false) { feedbackText = text; feedbackTime = 2.2f; feedbackColor = error ? Short : Lime; }

        private void ScrollTo(float value) => scroll = Math.Max(0, Math.Min(MaxScroll, value));

        // ------------------------------------------------------------------ input
        public override void HandleEvent(in InputEvent e)
        {
            if (Cleared) return;
            if (e.Type == EventType.MouseMove) { mouse = e.Pos; UpdateHover(); if (dragOffset.HasValue) DragThumb(e.Y); }
            if (e.Type == EventType.MouseUp && e.Button == 1) { mouseDown = false; dragOffset = null; }
            switch (phase)
            {
                case Phase.Upgrading:
                    if (impacted && (e.IsLeftDown || e.Type == EventType.KeyDown && (e.IsConfirmKey || e.Key == Key.Escape))) { showT = Math.Max(showT, ImpactAt + HoldT); Finish(); }
                    return;
                case Phase.Confirm:
                    if (e.Type == EventType.KeyDown) { if (e.Key == Key.Escape) Cancel(); else if (e.IsConfirmKey) Confirm(); }
                    else if (e.IsLeftDown)
                    {
                        mouseDown = true;
                        if (ConfirmButton.Contains(e.X, e.Y)) Confirm();
                        else if (CancelButton.Contains(e.X, e.Y) || !((RectF)ConfirmPanel).Contains(e.X, e.Y) && e.X >= DeckPanel.X - 12) Cancel();
                    }
                    return;
            }
            switch (e.Type)
            {
                case EventType.KeyDown:
                    switch (e.Key)
                    {
                        case Key.V: ShowUpgraded = !ShowUpgraded; Sound.PlayNav(); break;
                        case Key.Home: ScrollTo(0); break;
                        case Key.End: ScrollTo(MaxScroll); break;
                        case Key.Up: ScrollTo(scroll - 60); break;
                        case Key.Down: ScrollTo(scroll + 60); break;
                        case Key.PageUp: ScrollTo(scroll - Viewport.H); break;
                        case Key.PageDown: ScrollTo(scroll + Viewport.H); break;
                    }
                    break;
                case EventType.Wheel: ScrollTo(scroll - e.Wheel * 60); break;
                case EventType.MouseDown:
                    if (e.Button == 4 || e.Button == 5) { ScrollTo(scroll + (e.Button == 4 ? -60 : 60)); break; }
                    if (e.Button != 1) break;
                    mouseDown = true;
                    if (ToggleRect.Contains(e.X, e.Y)) { ShowUpgraded = !ShowUpgraded; Sound.PlayNav(); break; }
                    if (ExitButton.Contains(e.X, e.Y)) { Cleared = true; Sound.PlayCancel(); break; }
                    if (MaxScroll > 0 && Track.Inflate(8, 0).Contains(e.X, e.Y))
                    {
                        var thumb = Thumb;
                        if (thumb.Inflate(8, 0).Contains(e.X, e.Y)) dragOffset = (int)(e.Y - thumb.Y);
                        else ScrollTo(scroll + (e.Y < thumb.Y ? -1 : 1) * Viewport.H);
                        break;
                    }
                    if (((RectF)Viewport).Contains(e.X, e.Y))
                        for (var i = 0; i < run.Deck.Count; i++)
                            if (Visible(i) && (CardRect(i).Contains(e.X, e.Y) || ButtonRect(i).Contains(e.X, e.Y))) { Select(i); break; }
                    break;
            }
        }

        private RectF Thumb
        {
            get
            {
                var content = Math.Max(1, Rows * TileH);
                var h = Math.Min(Track.H, Math.Max(36, Track.H * Viewport.H / (float)content));
                var travel = Track.H - h;
                return new RectF(Track.X, Track.Y + (float)Math.Round(travel * scrollShown / Math.Max(1, MaxScroll)), Track.W, h);
            }
        }

        private void DragThumb(float y)
        {
            var travel = Track.H - Thumb.H;
            ScrollTo((y - Track.Y - dragOffset.Value) * MaxScroll / Math.Max(1, travel));
            scrollShown = scroll;
        }

        private void UpdateHover()
        {
            var (x, y) = mouse;
            var wasToggle = toggleHovered; var wasExit = exitHovered; var wasOk = confirmHovered; var wasNo = cancelHovered; var previous = hovered;
            toggleHovered = phase == Phase.Browse && ToggleRect.Contains(x, y);
            exitHovered = phase == Phase.Browse && ExitButton.Contains(x, y);
            confirmHovered = phase == Phase.Confirm && ConfirmButton.Contains(x, y);
            cancelHovered = phase == Phase.Confirm && CancelButton.Contains(x, y);
            hovered = null;
            if (phase == Phase.Browse && ((RectF)Viewport).Contains(x, y))
                for (var i = 0; i < run.Deck.Count; i++)
                    if (Visible(i) && (CardRect(i).Contains(x, y) || ButtonRect(i).Contains(x, y))) { hovered = i; break; }
            if (hovered.HasValue && hovered != previous || toggleHovered && !wasToggle || exitHovered && !wasExit || confirmHovered && !wasOk || cancelHovered && !wasNo) Sound.PlayNav();
            if (hovered.HasValue && hovered != previous) Comment(hovered.Value);
        }

        /// <summary>El Brujo may say something about the card under the mouse.</summary>
        private void Comment(int i)
        {
            var card = run.Deck[i];
            if (!RunState.CanUpgrade(card)) { speech.Hover("card" + i, WarlockCue.HoverUpgraded, card.Name); return; }
            var price = run.UpgradePrice(card);
            var cue = run.Gold < price ? WarlockCue.HoverPoor : card.Rarity >= 3 ? WarlockCue.HoverRare : WarlockCue.HoverCard;
            speech.Hover("card" + i, cue, card.Name, Math.Max(0, price - run.Gold));
        }

        // ------------------------------------------------------------------ simulation
        public override void Update(float dt)
        {
            dt = Math.Min(0.1f, Math.Max(0f, dt));
            time += dt; feedbackTime = Math.Max(0, feedbackTime - dt); glowT = Math.Max(0, glowT - dt); confirmT += dt;
            if (shakeT > 0 && (shakeT -= dt) <= 0) { shakeTile = null; shakeConfirm = false; }
            scrollShown += (scroll - scrollShown) * Math.Min(1f, dt * 14f);
            if (Math.Abs(scroll - scrollShown) < 0.5f) scrollShown = scroll;
            backdrop.Update(dt);
            speech.Update(dt);
            pose.Update(dt, speech, sheet);
            if (phase == Phase.Upgrading)
            {
                var was = showT; showT += dt;
                var (fx0, fy0) = FlameScreen(true);
                if (was < ReleaseAt && showT >= ReleaseAt)
                    fx.Burst(fx0, fy0, 22, Sparks, (60f, 220f), null, (0.3f, 0.7f), (1.5f, 3f), ParticleStyle.Spark, 3f);
                if (showT >= ReleaseAt && showT < ImpactAt)
                {
                    var (bx, by) = BoltAt((showT - ReleaseAt) / BoltT);
                    fx.Burst(bx, by, 3, Sparks, (10f, 60f), null, (0.2f, 0.45f), (1.5f, 2.5f), ParticleStyle.Glow, 2f);
                }
                if (!impacted && showT >= ImpactAt)
                {
                    impacted = true; Sound.PlayReward();
                    var (nx, ny) = NewCardCenter;
                    fx.Burst(nx, ny, 46, Sparks, (120f, 420f), null, (0.4f, 0.9f), (2f, 4f), ParticleStyle.Spark, 2.6f);
                    fx.Burst(nx, ny, 20, new[] { Rgba.White, Gilt }, (40f, 160f), null, (0.5f, 1.0f), (2f, 3f), ParticleStyle.Square, 1.8f, 160f);
                }
                if (showT >= ImpactAt + HoldT) Finish();
            }
            // the cauldron bubbles
            if ((bubbleTimer -= dt) <= 0)
            {
                bubbleTimer = steam.Random.UniformF(0.18f, 0.42f);
                steam.Emit(CauldronX + steam.Random.UniformF(-22, 22), NpcGround - 44, steam.Random.UniformF(-6, 6), steam.Random.UniformF(-34, -18), steam.Random.UniformF(0.5f, 0.9f), new[] { new Rgba(214, 255, 196), new Rgba(139, 224, 122), new Rgba(60, 154, 76) }, 3f, ParticleStyle.Square, 0.5f);
            }
            steam.Update(dt);
            fx.Update(dt);
        }

        /// <summary>The ghost flame on screen (at the cast's release when <paramref name="cast"/>).</summary>
        private (float x, float y) FlameScreen(bool cast)
        {
            var p = cast && sheet.Events.TryGetValue("cast", out var e) ? e.point : sheet.Anchor("staff", (36.5f, 21.4f));
            return sheet.ToScreen(NpcRect, p);
        }

        /// <summary>The bolt's head at <paramref name="u"/> ∈ [0, 1]: an arc from the flame up over the panel to the new card.</summary>
        private (float x, float y) BoltAt(float u)
        {
            u = Math.Max(0, Math.Min(1, u));
            var (x0, y0) = FlameScreen(true); var (x1, y1) = NewCardCenter;
            float cx = (x0 + x1) / 2, cy = ConfirmPanel.Y - 150;
            var a = (1 - u) * (1 - u); var b = 2 * (1 - u) * u; var c = u * u;
            return (a * x0 + b * cx + c * x1, a * y0 + b * cy + c * y1);
        }

        // ------------------------------------------------------------------ drawing
        public override void Draw(Canvas canvas)
        {
            backdrop.Draw(canvas);
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(4, 10, 6, 130));
            canvas.BlitRect(Vignette(), new RectF(0, 0, Canvas.Width, Canvas.Height), Rgba.White);
            DrawBand(canvas);
            DrawCounter(canvas);
            DrawTariffs(canvas);
            DrawDeck(canvas);
            DrawCauldron(canvas);
            DrawNpc(canvas);
            steam.Draw(canvas, Art);
            DrawBottom(canvas);
            if (phase != Phase.Browse) DrawConfirm(canvas);
            if (speech.Visible) SpeechBubble.Draw(canvas, Fonts.Get(13), Bubble(speech.Text), speech.Shown, speech.Age, speech.Alpha);
            if (phase == Phase.Upgrading) DrawBolt(canvas);
            fx.Draw(canvas, Art);
            if (phase == Phase.Browse && hovered.HasValue && !dragOffset.HasValue)
                Tooltip.Draw(canvas, Game, TooltipOf(run.Deck[hovered.Value]), mouse, CardRect(hovered.Value));
        }

        private Sprite Vignette() => new Sprite(Art.Procedural("warlock_vignette", 160, 90, () =>
        {
            var px = new Rgba[160 * 90];
            for (var y = 0; y < 90; y++)
                for (var x = 0; x < 160; x++)
                {
                    var d = Math.Sqrt(Math.Pow((x - 80) / 96.0, 2) + Math.Pow((y - 50) / 62.0, 2));
                    px[y * 160 + x] = new Rgba(2, 6, 4, (int)(225 * Math.Pow(MathX.Clamp01((d - 0.5) / 0.6), 1.4)));
                }
            return px;
        }, smooth: true));

        private Sprite Band() => new Sprite(Art.Procedural("warlock_band", 1, 96, () =>
        {
            var px = new Rgba[96];
            for (var y = 0; y < 96; y++) px[y] = new Rgba(4, 8, 6, (int)(220 * Math.Pow(1 - y / 96.0, 1.4)));
            return px;
        }));

        private void DrawBand(Canvas canvas)
        {
            canvas.BlitRect(Band(), new RectF(0, 0, Canvas.Width, 96), Rgba.White);
            Glows.Draw(canvas, Art, 640, 40, 150, new Rgba(26, 70, 24));
            PixelUi.InkText(canvas, Art, "El Brujo", 30, Anchor.Center, 640, 38, Lime, true, true);
            PixelUi.Ribbon(canvas, Art, 640, 80, "Mejora tus cartas a cambio de oro", 13);
        }

        /// <summary>How many cards of the deck are upgraded: an iron plate with the deck icon.</summary>
        private void DrawCounter(Canvas canvas)
        {
            var plate = (RectF)Counter;
            if (glowT > 0) { plate = plate.Move(0, glowT > 1.3f ? -2 : 0); PixelUi.Frame2(canvas, plate, Glows.Scaled(Arcane, 0.5 + 0.5 * glowT / 1.6)); }
            PixelUi.Panel(canvas, Art, plate);
            var icon = Art.Icon("deck");
            if (icon.Valid) canvas.BlitRect(icon, new RectF(plate.X + 10, plate.CenterY - icon.H, icon.W * 2, icon.H * 2), Rgba.White);
            PixelUi.InkText(canvas, Art, "Mejoradas", 13, Anchor.MidLeft, plate.X + 44, plate.CenterY - 1, Dim);
            var done = run.Deck.Count(c => !RunState.CanUpgrade(c));
            var r = PixelUi.InkText(canvas, Art, "/" + run.Deck.Count, 13, Anchor.MidRight, plate.Right - 12, plate.CenterY - 1, Dim);
            PixelUi.InkText(canvas, Art, done.ToString(), 20, Anchor.MidRight, r.X, plate.CenterY - 1, Lime);
        }

        private void DrawTariffs(Canvas canvas)
        {
            var v = TariffList(); var box = v.Box;
            PixelUi.Panel(canvas, Art, box);
            canvas.Fill(box.Inflate(-16, -16), new Rgba(8, 14, 10, 120));
            PixelUi.InkText(canvas, Art, "Tarifa por mejora", 16, Anchor.Center, box.CenterX, box.Y + 26, Lime, false, true);
            DitherRule(canvas, box.X + 18, box.Y + 40, box.W - 36, new Rgba(70, 110, 64));
            foreach (var (rarity, name, price, was, row) in v.Rows)
            {
                var rc = CardWidget.RarityColor(rarity);
                Gem(canvas, row.X + 4, row.CenterY, rc);
                PixelUi.InkText(canvas, Art, name, 13, Anchor.MidLeft, row.X + 14, row.CenterY - 1, Glows.Scaled(rc, 1.3));
                var pr = PixelUi.InkText(canvas, Art, price.ToString(), 16, Anchor.MidRight, row.Right, row.CenterY - 1, Cream);
                var coin = Art.Icon("coin");
                if (coin.Valid) canvas.BlitRect(coin, new RectF(pr.X - coin.W - 4, (float)Math.Floor(row.CenterY - coin.H / 2f), coin.W, coin.H), Rgba.White);
                if (was > 0)
                {
                    var wr = PixelUi.InkText(canvas, Art, was.ToString(), 13, Anchor.MidRight, pr.X - coin.W - 10, row.CenterY - 1, new Rgba(170, 150, 130));
                    canvas.Fill(new RectF(wr.X + 2, (float)Math.Floor(wr.CenterY), wr.W - 4, 2), Short);
                }
            }
            if (v.Note != null) PixelUi.InkText(canvas, Art, v.Note, 13, Anchor.Center, box.CenterX, box.Bottom - 22, Lime);
        }

        private void DrawDeck(Canvas canvas)
        {
            var panel = (RectF)DeckPanel;
            PixelUi.Panel(canvas, Art, panel);
            canvas.Fill(panel.Inflate(-16, -16), new Rgba(8, 12, 10, 150));
            var t = PixelUi.InkText(canvas, Art, "Tu mazo", 16, Anchor.MidLeft, Viewport.X, 133, Cream, false, true);
            var n = PixelUi.InkText(canvas, Art, "· " + run.Deck.Count + " cartas", 13, Anchor.MidLeft, t.Right + 8, 133, Dim);
            if (ShowUpgraded) PixelUi.InkText(canvas, Art, "Así quedarán al mejorarlas", 13, Anchor.MidLeft, n.Right + 14, 133, Lime);
            var tstate = phase != Phase.Browse ? ButtonState.Off : toggleHovered ? (mouseDown ? ButtonState.Press : ButtonState.Hover) : ButtonState.Idle;
            PixelUi.Button(canvas, Art, ToggleRect, "Ver mejoras", ShowUpgraded ? "gold" : "bronze", tstate, null, "V", time, 0f, 15);
            DitherRule(canvas, panel.X + 20, Viewport.Y - 10, panel.W - 40, new Rgba(70, 110, 64));
            canvas.PushClip(new RectF(Viewport.X - 8, Viewport.Y - 6, Viewport.W + 16, Viewport.H + 6));
            for (var i = 0; i < run.Deck.Count; i++) if (Visible(i)) DrawTile(canvas, i);
            canvas.PopClip();
            if (MaxScroll > 0)
            {
                PixelUi.PixelBox(canvas, Track, new Rgba(10, 12, 12), Ink, new Rgba(24, 28, 26));
                var th = Thumb; var hot = dragOffset.HasValue || th.Inflate(8, 0).Contains(mouse.x, mouse.y);
                PixelUi.PixelBox(canvas, th, hot ? new Rgba(150, 200, 120) : new Rgba(96, 140, 82), Ink, hot ? new Rgba(200, 240, 170) : new Rgba(130, 176, 108));
            }
        }

        private void DrawTile(Canvas canvas, int i)
        {
            var v = Tile(i); var card = run.Deck[i];
            var hot = hovered == i && phase == Phase.Browse; var ok = !v.Upgraded && v.Affordable;
            var dx = shakeTile == i ? (float)Math.Round(Math.Sin(shakeT * 60) * 3 * shakeT / 0.35f) * 2 : 0f;
            var lift = hot ? -4 : 0;
            var c = v.Card.Move(dx, lift);
            var shown = ShowUpgraded && !v.Upgraded ? card.Upgrade() : card;
            if (glowIndex == i && glowT > 0)
            {
                Glows.Draw(canvas, Art, c.CenterX, c.CenterY, 110, Glows.Scaled(new Rgba(70, 180, 60), glowT / 1.6));
            }
            if (hot) PixelUi.Frame2(canvas, c, ok ? new Rgba(255, 222, 140) : v.Upgraded ? new Rgba(150, 230, 130) : new Rgba(220, 150, 130));
            CardWidget.Draw(canvas, Game, shown, c.X, c.Y, CardBonus.ForRun(run).For(shown));
            if (!v.Upgraded && !v.Affordable) canvas.Fill(c, new Rgba(0, 0, 0, 70));
            if (glowIndex == i && glowT > 0) CardTwinkles(canvas, c, glowT);
            var b = v.Button.Move(dx, 0);
            if (v.Upgraded)
            {
                var tag = RectF.CenteredInt(b.CenterX, b.CenterY, v.Width, 28);
                PixelUi.PixelBox(canvas, tag, new Rgba(20, 52, 26, 235), new Rgba(96, 186, 84), new Rgba(36, 84, 40, 235));
                Chevron(canvas, tag.X + 12, tag.CenterY, new Rgba(170, 240, 150));
                PixelUi.InkText(canvas, Art, v.Label, 16, Anchor.MidLeft, tag.X + 22, tag.CenterY - 1, Lime);
                return;
            }
            var state = !ok ? ButtonState.Off : hot ? (mouseDown ? ButtonState.Press : ButtonState.Hover) : ButtonState.Idle;
            PixelUi.Button(canvas, Art, b, "", "bronze", state, null, null, time + i * 0.7f);
            var sb = b.Move(0, state == ButtonState.Hover ? -1 : state == ButtonState.Press ? 2 : 0);
            var x = (float)Math.Floor(sb.CenterX - v.Width / 2f); var cy = sb.CenterY - 1;
            var coin = Art.Icon("coin");
            if (coin.Valid) { canvas.BlitRect(coin, new RectF(x, (float)Math.Floor(cy) - coin.H, coin.W * 2, coin.H * 2), state == ButtonState.Off ? new Rgba(150, 150, 150) : Rgba.White); x += 32; }
            PixelUi.InkText(canvas, Art, v.Label, v.Size, Anchor.MidLeft, x, cy, v.Affordable ? Cream : Short);
        }

        /// <summary>El Brujo's bubbling cauldron (drawn by code, <see cref="WarlockCauldron"/>): green fire under it, the brew's
        /// light on the floor.</summary>
        private void DrawCauldron(Canvas canvas)
        {
            var pot = WarlockCauldron.Sprite(Art); var w = pot.W * 2; var h = pot.H * 2;
            var flick = 0.85f + 0.15f * (float)Math.Sin(time * 6.1) * (float)Math.Sin(time * 2.7 + 1);
            Glows.Draw(canvas, Art, CauldronX, NpcGround - 46, 90, Glows.Scaled(new Rgba(30, 90, 26), flick));
            GroundShadow.Draw(canvas, Art, CauldronX, NpcGround, w * 0.9f, 2, 1f, new Rgba(18, 52, 18));
            WarlockCauldron.DrawFire(canvas, CauldronX, NpcGround, time);
            canvas.BlitRect(pot, new RectF(CauldronX - w / 2f, NpcGround - h, w, h), Rgba.White);
            Glows.Draw(canvas, Art, CauldronX, NpcGround - h + 22, 34, Glows.Scaled(new Rgba(90, 220, 80), 0.5f * flick));
        }

        /// <summary>El Brujo: green floor light and a contact shadow, the sprite and its halo, the flame's glow.</summary>
        private void DrawNpc(Canvas canvas)
        {
            if (!sheet.Valid) return;
            var r = NpcRect; var flick = 0.85f + 0.15f * (float)Math.Sin(time * 7.7) * (float)Math.Sin(time * 3.3 + 2);
            var (fx0, fy0) = sheet.ToScreen(r, sheet.Anchor("staff", (36.5f, 21.4f)));
            var burst = phase == Phase.Upgrading && showT > ReleaseAt - 0.3f && showT < ImpactAt + 0.3f ? 1.8f : 1f;
            Glows.Draw(canvas, Art, fx0, fy0, 62 * burst, Glows.Scaled(new Rgba(30, 90, 26), flick * burst));
            GroundShadow.Draw(canvas, Art, r.X + sheet.Feet.x * sheet.Scale, NpcGround, sheet.Feet.w * sheet.Scale * 2.2f, sheet.Scale, 1f, new Rgba(34, 80, 34));
            GroundShadow.Draw(canvas, Art, fx0, NpcGround, 18, sheet.Scale, 0.8f);
            sheet.Draw(canvas, pose.Anim, pose.Elapsed, r, flick);
            Glows.Draw(canvas, Art, fx0, fy0, 22, Glows.Scaled(new Rgba(120, 255, 100), 0.3f * flick));
        }

        private void DrawBottom(Canvas canvas)
        {
            var x = 22f; const float y = 684;
            foreach (var (key, label) in new[] { ("Clic", "elegir"), ("Rueda", "desplazar"), ("V", "ver mejoras") })
            {
                var k = PixelUi.Keycap(canvas, Art, x + PixelUi.KeycapWidth(Art, key) / 2f, y, key);
                var r = PixelUi.InkText(canvas, Art, label, 13, Anchor.MidLeft, k.Right + 6, y - 1, Dim);
                x = r.Right + 12;
            }
            if (feedbackTime > 0 && phase == Phase.Browse)
            {
                canvas.PushAlpha(Math.Min(1f, feedbackTime / 0.3f));
                var w = PixelUi.InkTextWidth(Art, feedbackText, 16) + 48;
                var box = RectF.CenteredInt(700, 682, w, 40);
                PixelUi.Panel(canvas, Art, box);
                PixelUi.InkText(canvas, Art, feedbackText, 16, Anchor.Center, box.CenterX, box.CenterY - 1, feedbackColor);
                canvas.PopAlpha();
            }
            var st = phase != Phase.Browse ? ButtonState.Off : exitHovered ? (mouseDown ? ButtonState.Press : ButtonState.Hover) : ButtonState.Idle;
            if (exitHovered && phase == Phase.Browse) PixelUi.Frame2(canvas, ((RectF)ExitButton).Move(0, -1), new Rgba(220, 200, 160));
            PixelUi.Button(canvas, Art, ExitButton, "Salir", "bronze", st, null, null, time, 0f, 16);
        }

        private void DrawConfirm(Canvas canvas)
        {
            var v = Confirmation(); if (v.Title == null) return;
            var appear = Math.Min(1f, confirmT / 0.14f);
            canvas.Fill(new RectF(DeckPanel.X - 12, 92, Canvas.Width - DeckPanel.X + 12, Canvas.Height - 92), new Rgba(2, 6, 4, (int)(180 * appear)));
            canvas.PushAlpha(appear);
            var panel = ((RectF)ConfirmPanel).Move(0, (float)Math.Round((1 - appear) * 4) * 2);
            var dy = panel.Y - ConfirmPanel.Y;
            PixelUi.Panel(canvas, Art, panel);
            canvas.Fill(panel.Inflate(-16, -16), new Rgba(8, 14, 10, 200));
            PixelUi.InkText(canvas, Art, v.Title, 22, Anchor.Center, panel.CenterX, panel.Y + 34, Lime, true, true);
            var bonus = CardBonus.ForRun(run);
            var oldCard = phase == Phase.Upgrading ? before : run.Deck[selected.Value];
            var newCard = phase == Phase.Upgrading ? after : oldCard.Upgrade();
            var o = v.Old.Move(0, dy); var n = v.New.Move(0, dy);
            // labels over the cards
            Tag(canvas, o.CenterX, o.Y - 20, "Ahora", Dim);
            if (impacted && showT - ImpactAt > 0.06f) PixelUi.Ribbon(canvas, Art, n.CenterX, n.Y - 20, "¡Mejorada!", 16);
            else Tag(canvas, n.CenterX, n.Y - 20, "Mejorada", Lime);
            // the new card glows (stronger as the spell gathers), the old one fades once the spell lands
            var gather = phase == Phase.Upgrading ? MathX.Clamp01(showT / Math.Max(0.01f, ImpactAt)) : 0f;
            var pulse = 0.5 + 0.5 * Math.Sin(time * 2.4);
            Glows.Draw(canvas, Art, n.CenterX, n.CenterY, 150 + 60 * gather, Glows.Scaled(new Rgba(40, 120, 36), 0.6 + 0.25 * pulse + 0.6 * gather + (impacted ? 0.5 : 0)));
            CardWidget.DrawAt(canvas, Game, oldCard, o.CenterX, o.CenterY, bonus.For(oldCard), CardScale);
            if (impacted) canvas.Fill(o, new Rgba(8, 10, 12, (int)(140 * MathX.Clamp01((showT - ImpactAt) / 0.35f))));
            CardWidget.DrawAt(canvas, Game, newCard, n.CenterX, n.CenterY, bonus.For(newCard), CardScale);
            if (phase == Phase.Upgrading && !impacted) canvas.Fill(n, new Rgba(6, 16, 8, (int)(120 * (1 - 0.6f * gather))));
            if (impacted)
            {
                var k = MathX.Clamp01(1 - (showT - ImpactAt) / 0.25f);
                if (k > 0) canvas.Fill(n, new Rgba((int)(110 * k), (int)(170 * k), (int)(100 * k)), Blend.Add);
                CardTwinkles(canvas, n, 1.6f);
            }
            Arrow(canvas, panel.CenterX, o.CenterY, impacted);
            foreach (var (icon, text, color, rect) in v.Chips) Chip(canvas, rect.Move(0, dy), icon, text, color);
            // buttons (hidden during the spell)
            if (phase == Phase.Confirm)
            {
                var sx = shakeConfirm ? (float)Math.Round(Math.Sin(shakeT * 60) * 3 * shakeT / 0.35f) * 2 : 0f;
                var ok = ConfirmButton.Move(sx, dy);
                var okState = !v.Affordable ? ButtonState.Off : confirmHovered ? (mouseDown ? ButtonState.Press : ButtonState.Hover) : ButtonState.Idle;
                if (confirmHovered && v.Affordable) PixelUi.Frame2(canvas, ok.Move(0, -1), new Rgba(255, 222, 140));
                PixelUi.Button(canvas, Art, ok, "", "gold", okState, null, null, time);
                var sb = ok.Move(0, okState == ButtonState.Hover ? -1 : okState == ButtonState.Press ? 2 : 0);
                var lx = (float)Math.Floor(sb.CenterX - v.ButtonWidth / 2f);
                var lr = PixelUi.InkText(canvas, Art, "Mejorar", 20, Anchor.MidLeft, lx, sb.CenterY - 1, okState == ButtonState.Off ? new Rgba(196, 190, 184) : Cream);
                var coin = Art.Icon("coin"); var px = lr.Right + 14;
                if (coin.Valid) { canvas.BlitRect(coin, new RectF(px, (float)Math.Floor(sb.CenterY - 1) - coin.H, coin.W * 2, coin.H * 2), okState == ButtonState.Off ? new Rgba(150, 150, 150) : Rgba.White); px += 32; }
                var pr = PixelUi.InkText(canvas, Art, v.ButtonLabel, v.ButtonSize, Anchor.MidLeft, px, sb.CenterY - 1, v.Affordable ? Cream : Short);
                PixelUi.Keycap(canvas, Art, pr.Right + 10 + PixelUi.KeycapWidth(Art, "Enter") / 2f, sb.CenterY - 1, "Enter");
                var no = CancelButton.Move(0, dy);
                var noState = cancelHovered ? (mouseDown ? ButtonState.Press : ButtonState.Hover) : ButtonState.Idle;
                PixelUi.Button(canvas, Art, no, "Cancelar", "bronze", noState, null, "Esc", time, 0f, 16);
            }
            if (!string.IsNullOrEmpty(v.Note)) PixelUi.InkText(canvas, Art, v.Note, 13, Anchor.Center, panel.CenterX, panel.Bottom - 22, v.NoteColor);
            canvas.PopAlpha();
        }

        /// <summary>The green bolt from the staff to the new card: a bright head, a trail of soft light and dots behind it.</summary>
        private void DrawBolt(Canvas canvas)
        {
            if (showT < ReleaseAt || showT > ImpactAt + 0.05f) return;
            var u = MathX.Clamp01((showT - ReleaseAt) / BoltT);
            for (var k = 10; k >= 0; k--)
            {
                var uu = u - k * 0.035f; if (uu < 0) continue;
                var (x, y) = BoltAt(uu); var fade = 1 - k / 11f;
                Glows.Draw(canvas, Art, x, y, 26 * fade + 8, Glows.Scaled(new Rgba(80, 210, 70), fade));
                var s = (float)Math.Floor(2 + 3 * fade) * 2;
                canvas.Fill(new RectF((float)Math.Floor(x / 2) * 2 - s / 2, (float)Math.Floor(y / 2) * 2 - s / 2, s, s), k == 0 ? Rgba.White : Glows.Scaled(new Rgba(170, 255, 150), fade));
            }
        }

        private TooltipContent TooltipOf(CardDefinition item)
        {
            var shown = ShowUpgraded && RunState.CanUpgrade(item) ? item.Upgrade() : item; var s = CardBonus.ForRun(run).For(shown);
            var tip = Tooltip.Card(Game, shown, s.BonusDamage, s.BonusBlock);
            var lines = RunState.CanUpgrade(item)
                ? new List<string> { "Al mejorarla: " + Upgrades.Describe(item), "Precio: " + run.UpgradePrice(item) + " de oro" }
                : new List<string> { "Ya está mejorada al máximo." };
            lines.Add(""); lines.AddRange(tip.Lines);
            return new TooltipContent(tip.Title) { Lines = lines, Icon = tip.Icon, Subtitle = tip.Subtitle, Panels = tip.Panels };
        }

        // ------------------------------------------------------------------ small pieces
        private void Tag(Canvas canvas, float cx, float cy, string text, Rgba color)
        {
            var w = PixelUi.InkTextWidth(Art, text, 13) + 20;
            var r = RectF.CenteredInt(cx, cy, w, 24);
            PixelUi.PixelBox(canvas, r, Glows.Scaled(color, 0.16).WithAlpha(235), Glows.Scaled(color, 0.7), Glows.Scaled(color, 0.3).WithAlpha(235));
            PixelUi.InkText(canvas, Art, text, 13, Anchor.Center, r.CenterX, r.CenterY - 1, color);
        }

        private void Chip(Canvas canvas, RectF r, string icon, string text, Rgba c)
        {
            PixelUi.PixelBox(canvas, r, Glows.Scaled(c, 0.2).WithAlpha(235), Glows.Scaled(c, 0.82), Glows.Scaled(c, 0.34).WithAlpha(235));
            var x = r.X + 10;
            var s = icon != null ? Art.Icon(icon) : default;
            if (s.Valid) { canvas.BlitRect(s, new RectF(x, (float)Math.Floor(r.CenterY - s.H), s.W * 2, s.H * 2), Rgba.White); x += s.W * 2 + 4; }
            PixelUi.InkText(canvas, Art, text, 20, Anchor.MidLeft, x, r.CenterY - 1, Glows.Scaled(c, 1.32));
        }

        /// <summary>Three chevrons pointing at the upgraded card, lighting up one after another.</summary>
        private void Arrow(Canvas canvas, float cx, float cy, bool done)
        {
            for (var k = 0; k < 3; k++)
            {
                var on = done || ((int)(time * 6) % 4) == k;
                var col = on ? Lime : new Rgba(60, 110, 56);
                var x0 = (float)Math.Floor((cx - 26 + k * 18) / 2) * 2;
                for (var j = 0; j < 7; j++)
                {
                    var off = (3 - Math.Abs(j - 3)) * 2;
                    canvas.Fill(new RectF(x0 + off - 2, (float)Math.Floor(cy / 2) * 2 - 14 + j * 4 - 2, 8, 8), Ink);
                }
                for (var j = 0; j < 7; j++)
                {
                    var off = (3 - Math.Abs(j - 3)) * 2;
                    canvas.Fill(new RectF(x0 + off, (float)Math.Floor(cy / 2) * 2 - 14 + j * 4, 4, 4), col);
                }
            }
        }

        private static void Chevron(Canvas canvas, float cx, float cy, Rgba c)
        {
            float x = (float)Math.Floor(cx / 2) * 2 - 4, y = (float)Math.Floor(cy / 2) * 2 - 3;
            canvas.Fill(new RectF(x + 2, y, 2, 2), c); canvas.Fill(new RectF(x, y + 2, 2, 2), c); canvas.Fill(new RectF(x + 4, y + 2, 2, 2), c);
            canvas.Fill(new RectF(x + 2, y + 2, 2, 6), c);
        }

        private static void DitherRule(Canvas canvas, float x, float y, float w, Rgba color)
        {
            for (var px = (int)x; px < x + w - 1; px += 4) canvas.Fill(new RectF(px, (float)Math.Floor(y / 2) * 2, 2, 2), color);
        }

        private static void Gem(Canvas canvas, float cx, float cy, Rgba c)
        {
            float x = (float)Math.Floor(cx / 2) * 2 - 4, y = (float)Math.Floor(cy / 2) * 2 - 4;
            canvas.Fill(new RectF(x + 2, y, 4, 8), Ink); canvas.Fill(new RectF(x, y + 2, 8, 4), Ink);
            canvas.Fill(new RectF(x + 2, y + 2, 4, 4), c); canvas.Fill(new RectF(x + 2, y + 2, 2, 2), Glows.Scaled(c, 1.5));
        }

        private static readonly (float u, float v, float ph)[] Stars = { (0.1f, 0.12f, 0f), (0.86f, 0.3f, 1.7f), (0.3f, 0.92f, 3.1f), (0.04f, 0.66f, 4.4f), (0.7f, 0.06f, 2.3f), (0.95f, 0.88f, 5.2f) };

        /// <summary>Green four-point stars twinkling round a card that was just upgraded.</summary>
        private void CardTwinkles(Canvas canvas, RectF r, float k)
        {
            foreach (var (u, v, ph) in Stars)
            {
                var s = Math.Sin(time * 5 + ph) * Math.Min(1, k); if (s < 0.25) continue;
                var arm = 2 * (1 + (int)(s * 2.5));
                var cx = (float)Math.Floor((r.X + r.W * u) / 2) * 2; var cy = (float)Math.Floor((r.Y + r.H * v) / 2) * 2;
                canvas.Fill(new RectF(cx - arm, cy, 2 * arm + 2, 2), new Rgba(170, 255, 150)); canvas.Fill(new RectF(cx, cy - arm, 2, 2 * arm + 2), new Rgba(170, 255, 150));
                canvas.Fill(new RectF(cx, cy, 2, 2), Rgba.White);
            }
        }
    }

    /// <summary>
    /// El Brujo's cauldron, drawn by code in native pixels (shown ×2, docs/art-style.md): an iron pot on three legs, lit from
    /// the upper left with Bayer dither, a band of rivets, the rim, the green brew in its mouth, one ink outline; the green
    /// fire under it is drawn live (<see cref="DrawFire"/>).
    /// </summary>
    public static class WarlockCauldron
    {
        public const int W = 44, H = 32;
        private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
        private static float B(int x, int y) => (Bayer[y & 3, x & 3] + 0.5f) / 16f;
        private static readonly Rgba Ink = new Rgba(7, 5, 12);
        private static readonly Rgba[] Iron = { new Rgba(20, 18, 32), new Rgba(35, 32, 54), new Rgba(54, 50, 79), new Rgba(77, 72, 112), new Rgba(106, 100, 148) };
        private static readonly Rgba[] Brew = { new Rgba(18, 48, 32), new Rgba(31, 90, 50), new Rgba(60, 154, 76), new Rgba(139, 224, 122), new Rgba(230, 255, 208) };
        private static readonly Rgba[] Fire = { new Rgba(31, 90, 50), new Rgba(60, 154, 76), new Rgba(139, 224, 122), new Rgba(230, 255, 208) };

        public static Sprite Sprite(Art art) => new Sprite(art.Procedural("warlock_cauldron", W, H, Pixels));

        private static bool Body(int x, int y) { float dx = (x + 0.5f - 22) / 17f, dy = (y + 0.5f - 18.5f) / 11.5f; return y >= 9 && dx * dx + dy * dy <= 1; }
        private static bool Rim(int x, int y) { float dx = (x + 0.5f - 22) / 18.5f, dy = (y + 0.5f - 9.5f) / 3.6f; return dx * dx + dy * dy <= 1; }
        private static bool Mouth(int x, int y) { float dx = (x + 0.5f - 22) / 15f, dy = (y + 0.5f - 9.2f) / 2.2f; return dx * dx + dy * dy <= 1; }
        private static bool Leg(int x, int y)
        {
            if (y < 26 || y >= H - 1) return false;
            var k = y - 26;
            return x >= 9 - k / 2 && x <= 11 - k / 2 || x >= 32 + k / 2 && x <= 34 + k / 2 || y >= 28 && x >= 21 && x <= 23;
        }

        public static Rgba[] Pixels()
        {
            var px = new Rgba[W * H];
            bool Solid(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && (Body(x, y) || Rim(x, y) || Leg(x, y));
            for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                {
                    if (!Solid(x, y)) continue;
                    var i = y * W + x;
                    if (Rim(x, y) && Mouth(x, y))
                    {
                        // the brew: brighter in the middle, a crest of light at the back edge
                        float dx = (x + 0.5f - 22) / 15f, dy = (y + 0.5f - 9.2f) / 2.2f; var d = (float)Math.Sqrt(dx * dx + dy * dy);
                        var tone = 3.2f - 2.4f * d + (B(x, y) - 0.5f) * 0.9f;
                        px[i] = Brew[Math.Max(1, Math.Min(4, (int)Math.Round(tone)))];
                        continue;
                    }
                    float t;
                    if (Rim(x, y)) t = 3.4f - (x - 22) / 9f + (y < 9 ? 0.6f : -0.4f);
                    else if (Leg(x, y) && !Body(x, y)) t = 1.6f - (x - 22) / 18f;
                    else t = 2.8f - (x - 12) / 12f - (y - 12) / 9f;
                    t += (B(x, y) - 0.5f) * 0.8f;
                    px[i] = Iron[Math.Max(0, Math.Min(4, (int)Math.Round(t)))];
                    if (!Rim(x, y) && Body(x, y) && (y == 17 || y == 18)) px[i] = Iron[Math.Max(0, Math.Min(4, (int)Math.Round(t - 1)))];
                    if (!Rim(x, y) && Body(x, y) && y == 17 && x % 5 == 2) px[i] = Iron[4];
                    // green light from the brew on the rim's inner edge
                    if (Rim(x, y) && (Mouth(x, y + 1) || Mouth(x, y - 1) || Mouth(x - 1, y) || Mouth(x + 1, y))) px[i] = y < 9 ? Brew[2] : Iron[3];
                }
            // one ink outline
            var outline = new List<int>();
            for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                    if (!Solid(x, y) && (Solid(x - 1, y) || Solid(x + 1, y) || Solid(x, y - 1) || Solid(x, y + 1))) outline.Add(y * W + x);
            foreach (var i in outline) px[i] = Ink;
            // the rim's lower edge over the pot
            for (var x = 0; x < W; x++)
                for (var y = 10; y < 15; y++)
                    if (Rim(x, y) && !Rim(x, y + 1) && Body(x, y + 1)) { px[(y + 1) * W + x] = Ink; break; }
            return px;
        }

        /// <summary>Little green flames licking the bottom of the pot (2 px blocks, ×2 grain).</summary>
        public static void DrawFire(Canvas canvas, float cx, float groundY, float t)
        {
            for (var k = 0; k < 6; k++)
            {
                var x = (float)Math.Floor((cx - 18 + k * 7) / 2) * 2;
                var h = 3 + (int)(2.5 * (0.5 + 0.5 * Math.Sin(t * 9 + k * 1.9)));
                for (var j = 0; j < h; j++)
                {
                    var col = Fire[Math.Max(0, Math.Min(3, 3 - j * 3 / Math.Max(1, h)))];
                    var w = j < h - 1 ? 4 : 2;
                    canvas.Fill(new RectF(x - w / 2 + (j == h - 1 && (k + (int)(t * 8)) % 2 == 0 ? 2 : 0), groundY - 6 - j * 2, w, 2), col);
                }
            }
        }
    }
}
