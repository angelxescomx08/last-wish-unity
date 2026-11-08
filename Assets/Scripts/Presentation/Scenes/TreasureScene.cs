using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>
    /// The treasure room (interface redesigned 2026-11-07, scenes/treasure_scene.py): the dungeon room behind a warm focus, a
    /// title band and a code-drawn chest (<see cref="TreasureChest"/>) that rattles, opens in a burst of gold light and lets
    /// its relics rise into iron cards above it. Each card shows the relic in a niche lit in its rarity colour, its name,
    /// rarity / chroma / class tags and its colour-coded rule text. Pick one (click, 1-N, ← →) and take it (kit button,
    /// Enter): it flies to the relic counter while the others sink back and the lid closes; or skip (O) and leave them all.
    /// The rules are the domain's: the scene manager acquires <see cref="ChosenRelic"/> when the room clears.
    /// </summary>
    public sealed class TreasureScene : Scene
    {
        public enum Phase { Intro, Choosing, Taking, Skipping, Done }

        // ------------------------------------------------------------------ layout
        public const int CardTop = 126, Inset = RelicCards.Inset, NicheH = RelicCards.NicheH, NameCy = RelicCards.NameCy, TagsY = RelicCards.TagsY,
            TagH = RelicCards.TagH, RuleY = RelicCards.RuleY, TextY = RelicCards.TextY, Line = RelicCards.Line, MinLines = RelicCards.MinLines;
        public const int ChestCx = 640, ChestGround = 616, ButtonY = 640, ButtonH = 40, ButtonW = 200, ButtonGap = 24, IconSize = RelicCards.IconSize;
        public static readonly IRect RelicCounter = new IRect(24, 16, 196, 36);
        // timing (seconds)
        public const float ShakeT = 0.42f, OpenAt = 0.54f, RiseStart = 0.64f, RiseT = 0.5f, Stagger = 0.12f, CardFadeT = 0.26f;
        public const float FlyT = 0.6f, SinkT = 0.36f, CloseAt = 0.5f, TakeEndT = 0.95f, SkipEndT = 0.72f;

        public static int CardWidth(int count) => RelicCards.CardWidth(count);
        public static int CardGap(int count) => RelicCards.CardGap(count);
        /// <summary>The chest in screen pixels (native ×2), its ground line at <see cref="ChestGround"/>.</summary>
        public static RectF ChestRect => new RectF(ChestCx - TreasureChest.W, ChestGround - TreasureChest.Ground * 2, TreasureChest.W * 2, TreasureChest.H * 2);
        public static float MouthY => ChestRect.Y + TreasureChest.Mouth * 2;
        public static float BodyTopY => ChestRect.Y + TreasureChest.BodyTop * 2;

        private static readonly Rgba Ink = Colors.Ink, Cream = Colors.Cream, Dim = new Rgba(186, 176, 160), Gilt = new Rgba(255, 216, 128),
            Warm = new Rgba(255, 190, 90), Glint = new Rgba(255, 238, 190);

        public sealed class TreasureLayout
        {
            public List<RelicCardView> Cards = new List<RelicCardView>();
            public string Ribbon; public RectF Take, Skip, Empty, Bonus; public bool HasSkip, HasBonus; public string TakeLabel;
            public float CardBottom;
        }

        private readonly RunState run;
        private readonly List<RelicOffer> relics;
        private readonly DungeonBackdrop backdrop;
        private readonly BurstParticles fx = new BurstParticles(500, 61);
        private readonly Rng rng;
        private readonly int bonusGold; private readonly string bonusSources;
        private TreasureLayout layout;
        private int selected, hovered = -1;
        private string hoveredButton;
        private float time, t, counterPulse, moteClock, flash;
        private int arrived;          // relics that reached their card during the intro
        private bool opened, landed, closed;
        private (float x, float y) mouse = (-1, -1);

        public Phase State { get; private set; }
        public bool Cleared { get; set; }
        public bool TookRelic { get; private set; }
        public RelicOffer ChosenRelic { get; private set; }
        public IReadOnlyList<RelicOffer> Relics => relics;
        public int Selected => selected;
        /// <summary>Seconds into the current phase (tests).</summary>
        public float PhaseTime => t;
        /// <summary>Gold Pico de Minero and Ganzúa added when the room opened, and their names.</summary>
        public int BonusGold => bonusGold;
        public string BonusSources => bonusSources ?? "";

        public TreasureScene(GameContext game, RunState run, List<RelicOffer> relics) : base(game)
        {
            this.run = run; this.relics = relics?.Where(r => r != null).ToList() ?? new List<RelicOffer>();
            var seed = (int)((run?.Seed ?? 1) % 100003) * 17 + (run?.Floor ?? 1) * 5 + (run?.CurrentRoomId ?? "").Length;
            backdrop = new DungeonBackdrop(Art, 43, 0.6f);
            rng = new Rng(seed);
            if (run != null)
            {
                // Pico de Minero / Ganzúa paid their gold when the room was entered (RunState.EnterRoomRelics): say so here
                var sources = new List<string>();
                var miner = run.RelicEffect("miner_pick", 25); var pick = run.RelicEffect("lockpick", 20);
                if (miner > 0) sources.Add(RelicName("miner_pick", "Pico de Minero"));
                if (pick > 0) sources.Add(RelicName("lockpick", "Ganzúa"));
                bonusGold = miner + pick; bonusSources = string.Join(" · ", sources);
            }
        }

        private static string RelicName(string tag, string fallback) => RunContent.RelicByTag(tag)?.Name ?? fallback;

        private int BestRarity => relics.Count == 0 ? 1 : relics.Max(r => Math.Max(1, Math.Min(5, r.Definition.Rarity)));
        private bool AnyChroma => relics.Any(r => r.Chroma != ChromaKind.None);
        public float IntroLength => relics.Count == 0 ? OpenAt + 0.3f : RiseStart + (relics.Count - 1) * Stagger + RiseT;

        // ------------------------------------------------------------------ input
        public override void HandleEvent(in InputEvent e)
        {
            if (e.Type == EventType.MouseMove) { mouse = (e.X, e.Y); UpdateHover(); return; }
            if (Cleared || e.Repeat) return;
            switch (State)
            {
                case Phase.Intro: if (e.IsLeftDown || e.Type == EventType.KeyDown) SkipIntro(); return;
                case Phase.Taking: case Phase.Skipping: if (e.IsLeftDown || e.IsConfirmKey) Finish(); return;
                case Phase.Done: return;
            }
            var n = relics.Count; var lay = Layout();
            if (n > 1 && e.Type == EventType.KeyDown && e.Digit >= 1 && e.Digit <= n) { Select(e.Digit - 1); return; }
            if (n > 1 && (e.IsKey(Key.Right) || e.IsKey(Key.Tab))) { Select((selected + 1) % n); return; }
            if (n > 1 && e.IsKey(Key.Left)) { Select((selected + n - 1) % n); return; }
            if (e.IsConfirmKey) { Take(); return; }
            if (e.IsKey(Key.O) && lay.HasSkip) { Skip(); return; }
            if (!e.IsLeftDown) return;
            foreach (var c in lay.Cards) if (c.Rect.Contains(e.X, e.Y)) { Select(c.Index); return; }
            if (lay.Take.Contains(e.X, e.Y)) Take();
            else if (lay.HasSkip && lay.Skip.Contains(e.X, e.Y)) Skip();
        }

        private void UpdateHover()
        {
            var lay = Layout(); var before = (hovered, hoveredButton);
            hovered = -1; hoveredButton = null;
            if (State != Phase.Choosing) return;
            foreach (var c in lay.Cards) if (c.Rect.Contains(mouse)) hovered = c.Index;
            if (lay.Take.Contains(mouse)) hoveredButton = "take";
            else if (lay.HasSkip && lay.Skip.Contains(mouse)) hoveredButton = "skip";
            if ((hovered >= 0 || hoveredButton != null) && (hovered, hoveredButton) != before) Sound.PlayNav();
        }

        private void Select(int i)
        {
            if (i < 0 || i >= relics.Count) return;
            if (i != selected) Sound.PlayNav();
            selected = i;
        }

        /// <summary>Jump to the end of the opening show (any click or key during it).</summary>
        public void SkipIntro()
        {
            if (State != Phase.Intro) return;
            if (!opened) Open();
            while (arrived < relics.Count) Arrive(arrived);
            State = Phase.Choosing; t = 0; UpdateHover();
        }

        private void Take()
        {
            if (State != Phase.Choosing) return;
            if (relics.Count == 0) { Sound.PlayConfirm(); TookRelic = false; ChosenRelic = null; Cleared = true; State = Phase.Done; return; }
            TookRelic = true; ChosenRelic = relics[selected];
            State = Phase.Taking; t = 0; hovered = -1; hoveredButton = null;
            Sound.PlayGachaKeep();
            var n = Layout().Cards[selected].Niche;
            fx.Burst(n.CenterX, n.CenterY - 6, 20, Palette(ChosenRelic.Definition.Rarity), (60f, 220f), null, (0.3f, 0.6f), (1.5f, 3f), ParticleStyle.Spark, 3f);
        }

        private void Skip()
        {
            if (State != Phase.Choosing) return;
            TookRelic = false; ChosenRelic = null;
            State = Phase.Skipping; t = 0; hovered = -1; hoveredButton = null;
            Sound.PlayCancel();
        }

        /// <summary>A click or Enter while the relic flies or the lid closes leaves at once.</summary>
        private void Finish()
        {
            if (State == Phase.Taking && !landed) Land();
            Cleared = true; State = Phase.Done;
        }

        // ------------------------------------------------------------------ simulation
        public override void Update(float dt)
        {
            dt = Math.Min(0.1f, Math.Max(0f, dt));
            time += dt; t += dt;
            counterPulse = Math.Max(0f, counterPulse - dt * 2.2f); flash = Math.Max(0f, flash - dt * 3.2f);
            backdrop.Update(dt);
            switch (State)
            {
                case Phase.Intro:
                    if (!opened && t >= OpenAt) Open();
                    for (var i = arrived; i < relics.Count; i++)
                    {
                        var u = RiseU(i);
                        if (u >= 1f) Arrive(i); else { if (u > 0f) Trail(i, u); break; }
                    }
                    if (t >= IntroLength && arrived >= relics.Count) { State = Phase.Choosing; t = 0; UpdateHover(); }
                    break;
                case Phase.Taking:
                    if (!landed && t >= FlyT) Land();
                    if (!closed && t >= CloseAt + 0.12f) Close();
                    if (t >= TakeEndT) { Cleared = true; State = Phase.Done; }
                    break;
                case Phase.Skipping:
                    if (!closed && t >= CloseAt * 0.8f + 0.12f) Close();
                    if (t >= SkipEndT) { Cleared = true; State = Phase.Done; }
                    break;
            }
            // warm motes rising from the open chest
            if (LidFrame == TreasureChest.Open)
            {
                moteClock -= dt;
                if (moteClock <= 0)
                {
                    moteClock = (float)rng.Uniform(0.06, 0.14);
                    var x = ChestCx + (float)rng.Uniform(-56, 56); var y = MouthY + (float)rng.Uniform(-4, 6);
                    fx.Emit(x, y, (float)rng.Uniform(-8, 8), (float)rng.Uniform(-60, -26), (float)rng.Uniform(0.9, 1.8), new[] { Glint, Gilt, Glows.Scaled(Warm, 0.7) }, (float)rng.Uniform(1.5, 2.5), ParticleStyle.Square, 0.3f);
                }
            }
            fx.Update(dt);
        }

        private void Open()
        {
            opened = true; flash = 1f;
            Sound.PlayGachaOpen();
            var gold = new[] { Rgba.White, Glint, Gilt, new Rgba(214, 150, 40) };
            fx.Burst(ChestCx, MouthY, 34, gold, (120f, 340f), ((float)Math.PI * 1.1f, (float)Math.PI * 1.9f), (0.5f, 0.95f), (2f, 3.5f), ParticleStyle.Square, 1.4f, 520f);
            fx.Burst(ChestCx, MouthY - 6, 18, gold, (80f, 240f), null, (0.3f, 0.6f), (1.5f, 3f), ParticleStyle.Spark, 3f);
        }

        private void Arrive(int i)
        {
            arrived = Math.Max(arrived, i + 1);
            var n = Layout().Cards[i].Niche;
            fx.Burst(n.CenterX, n.CenterY - 6, 16, Palette(relics[i].Definition.Rarity), (60f, 200f), null, (0.3f, 0.6f), (1.5f, 3f), ParticleStyle.Spark, 3f);
            if (i == 0) Sound.PlayGachaTier(BestRarity);
            if (i == relics.Count - 1 && AnyChroma) Sound.PlayGachaGolden();
        }

        private void Land()
        {
            landed = true; counterPulse = 1f;
            Sound.PlayGachaStash();
            if (ChosenRelic != null) fx.Burst(RelicCounter.X + 22, RelicCounter.CenterY, 18, Palette(ChosenRelic.Definition.Rarity), (60f, 200f), null, (0.3f, 0.6f), (1.5f, 3f), ParticleStyle.Spark, 3f);
        }

        private void Close()
        {
            closed = true;
            Sound.PlayGachaThunk();
            fx.Burst(ChestCx, ChestGround - 2, 12, new[] { new Rgba(90, 76, 70), new Rgba(60, 50, 48) }, (40f, 120f), ((float)Math.PI * 1.0f, (float)Math.PI * 2.0f), (0.25f, 0.5f), (2f, 3f), ParticleStyle.Square, 2.5f, 200f);
        }

        /// <summary>A trail of motes behind a rising relic.</summary>
        private void Trail(int i, float u)
        {
            if (rng.Random() > 0.6) return;
            var (x, y) = RisePoint(i, u);
            fx.Emit(x + (float)rng.Uniform(-6, 6), y + (float)rng.Uniform(-6, 6), (float)rng.Uniform(-10, 10), (float)rng.Uniform(10, 30), (float)rng.Uniform(0.3, 0.6), Palette(relics[i].Definition.Rarity), (float)rng.Uniform(1.5, 2.5), ParticleStyle.Square, 1f);
        }

        private static Rgba[] Palette(int rarity) => RelicCards.Palette(rarity);
        private static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        private static float EaseOut(float v) { v = Clamp01(v); return 1 - (1 - v) * (1 - v) * (1 - v); }
        private static float Smooth(float v) { v = Clamp01(v); return v * v * (3 - 2 * v); }

        /// <summary>0 … 1 along the rise of relic <paramref name="i"/> out of the chest during the intro.</summary>
        private float RiseU(int i) => State != Phase.Intro ? 1f : Clamp01((t - RiseStart - i * Stagger) / RiseT);

        /// <summary>Where a relic is along its path from the chest's mouth to its niche (arc, eased).</summary>
        private (float x, float y) RisePoint(int i, float u)
        {
            var n = Layout().Cards[i].Niche; var e = EaseOut(u);
            float x0 = ChestCx, y0 = MouthY + 4, x1 = n.CenterX, y1 = n.CenterY - 6;
            var x = x0 + (x1 - x0) * e; var y = y0 + (y1 - y0) * e - 60 * (float)Math.Sin(Math.PI * e);
            return (x, y);
        }

        /// <summary>Which chest frame shows now: closed while it rattles, ajar for two beats, open, and closing again on leaving.</summary>
        public int LidFrame
        {
            get
            {
                switch (State)
                {
                    case Phase.Intro: return t < OpenAt - 0.12f ? 0 : t < OpenAt - 0.06f ? 1 : t < OpenAt ? 2 : TreasureChest.Open;
                    case Phase.Taking: case Phase.Skipping: case Phase.Done:
                    {
                        var at = State == Phase.Skipping ? CloseAt * 0.8f : CloseAt;
                        if (State == Phase.Done) return closed ? 0 : TreasureChest.Open;
                        return t < at ? TreasureChest.Open : t < at + 0.06f ? 2 : t < at + 0.12f ? 1 : 0;
                    }
                    default: return TreasureChest.Open;
                }
            }
        }

        // ------------------------------------------------------------------ layout
        /// <summary>The ribbon under the title.</summary>
        public string RibbonText => relics.Count == 0 ? "El cofre está vacío" : relics.Count == 1 ? "Una reliquia te espera" : "Elige una de las " + relics.Count + " reliquias";

        /// <summary>"Dorada ×2" / "Diamante ×3".</summary>
        public static string ChromaTag(ChromaKind chroma) => RelicCards.ChromaTag(chroma);

        /// <summary>Everything's place (cached: it only depends on the relics on offer).</summary>
        public TreasureLayout Layout()
        {
            if (layout != null) return layout;
            var lay = new TreasureLayout { Ribbon = RibbonText };
            var n = relics.Count;
            lay.Cards = RelicCards.Build(Game, relics, CardTop);
            var h = n > 0 ? lay.Cards[0].Rect.H : 0f;
            lay.CardBottom = n > 0 ? CardTop + h : CardTop;
            lay.Empty = RectF.CenteredInt(Canvas.Width / 2f, 300, 520, 132);
            if (n == 0)
            {
                lay.Take = new RectF(ChestCx - 120, ButtonY, 240, ButtonH); lay.TakeLabel = "Continuar";
            }
            else
            {
                lay.Take = new RectF(ChestCx - ButtonGap / 2f - ButtonW, ButtonY, ButtonW, ButtonH); lay.TakeLabel = "Tomar";
                lay.Skip = new RectF(ChestCx + ButtonGap / 2f, ButtonY, ButtonW, ButtonH); lay.HasSkip = true;
            }
            if (bonusGold > 0)
            {
                var bw = Math.Max(PixelUi.InkTextWidth(Art, "+" + bonusGold + " oro", 16, true) + 40, PixelUi.InkTextWidth(Art, bonusSources, 13) + 24);
                lay.Bonus = new RectF(ChestRect.Right + 24, ChestGround - 88, bw, 56); lay.HasBonus = true;
            }
            return layout = lay;
        }

        public RectF CardRect(int i) => Layout().Cards[i].Rect;
        public RectF TakeRect => Layout().Take;
        public RectF? SkipRect => Layout().HasSkip ? Layout().Skip : (RectF?)null;

        /// <summary>The tooltip of the hovered card, only when it adds something the card does not say (glossary terms,
        /// a class); an offered relic has no "Estado" line.</summary>
        public TooltipContent HoverTooltip()
        {
            if (State != Phase.Choosing || hovered < 0 || hovered >= relics.Count) return null;
            return RelicCards.OfferTooltip(Game, relics[hovered]);
        }

        // ------------------------------------------------------------------ drawing
        public override void Draw(Canvas canvas)
        {
            var lay = Layout();
            backdrop.Draw(canvas);
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(10, 6, 4, 150));
            canvas.BlitRect(Vignette(), new RectF(0, 0, Canvas.Width, Canvas.Height), Rgba.White);
            DrawBand(canvas, lay);
            DrawRelicCounter(canvas);
            DrawLight(canvas);
            if (relics.Count == 0) DrawEmpty(canvas, lay);
            foreach (var v in lay.Cards) DrawCard(canvas, v);
            DrawChest(canvas);
            DrawFlights(canvas, lay);
            fx.Draw(canvas, Art);
            DrawBonus(canvas, lay);
            DrawButtons(canvas, lay);
            var tip = HoverTooltip();
            if (tip != null) Tooltip.Draw(canvas, Game, tip, mouse, lay.Cards[hovered].Rect);
        }

        private Sprite Vignette() => new Sprite(Art.Procedural("treasure_vignette", 160, 90, () =>
        {
            var px = new Rgba[160 * 90];
            for (var y = 0; y < 90; y++)
                for (var x = 0; x < 160; x++)
                {
                    var d = Math.Sqrt(Math.Pow((x - 80) / 94.0, 2) + Math.Pow((y - 56) / 62.0, 2));
                    px[y * 160 + x] = new Rgba(8, 4, 2, (int)(228 * Math.Pow(MathX.Clamp01((d - 0.48) / 0.6), 1.4)));
                }
            return px;
        }, smooth: true));

        private Sprite Band() => new Sprite(Art.Procedural("treasure_band", 1, 96, () =>
        {
            var px = new Rgba[96];
            for (var y = 0; y < 96; y++) px[y] = new Rgba(10, 6, 4, (int)(220 * Math.Pow(1 - y / 96.0, 1.4)));
            return px;
        }));

        private void DrawBand(Canvas canvas, TreasureLayout lay)
        {
            canvas.BlitRect(Band(), new RectF(0, 0, Canvas.Width, 96), Rgba.White);
            Glows.Draw(canvas, Art, 640, 40, 150, new Rgba(84, 56, 18));
            PixelUi.InkText(canvas, Art, "Sala del Tesoro", 30, Anchor.Center, 640, 38, Gilt, true, true);
            PixelUi.Ribbon(canvas, Art, 640, 80, lay.Ribbon, 13);
        }

        /// <summary>Where the taken relic lands: an iron plate with the bag and the number of relics.</summary>
        private void DrawRelicCounter(Canvas canvas)
        {
            var plate = (RectF)RelicCounter;
            if (counterPulse > 0) { plate = plate.Move(0, counterPulse > 0.5f ? -2 : 0); PixelUi.Frame2(canvas, plate, Glows.Scaled(Gilt, 0.6 + 0.6 * counterPulse)); }
            PixelUi.Panel(canvas, Art, plate);
            var bag = Art.Icon("bag");
            if (bag.Valid) canvas.BlitRect(bag, new RectF(plate.X + 10, plate.CenterY - bag.H, bag.W * 2, bag.H * 2), Rgba.White);
            PixelUi.InkText(canvas, Art, "Reliquias", 13, Anchor.MidLeft, plate.X + 44, plate.CenterY - 1, Dim);
            var count = (run?.OwnedRelics.Count ?? 0) + (landed && !Cleared ? 1 : 0);
            PixelUi.InkText(canvas, Art, count.ToString(), 20, Anchor.MidRight, plate.Right - 12, plate.CenterY - 1, Gilt);
        }

        /// <summary>How open the chest is, 0 … 1, for its light (beam, floor, glow).</summary>
        private float Openness
        {
            get
            {
                var f = LidFrame;
                var k = f == TreasureChest.Open ? 1f : f == 2 ? 0.55f : f == 1 ? 0.3f : 0f;
                if (State == Phase.Intro && f == TreasureChest.Open) k = 0.6f + 0.4f * Smooth((t - OpenAt) / 0.4f);
                return k;
            }
        }

        /// <summary>The warm light of the treasure: on the floor, the column rising from the chest towards the cards.</summary>
        private void DrawLight(Canvas canvas)
        {
            var k = Openness; var pulse = 0.85 + 0.15 * Math.Sin(time * 2.1);
            Glows.Draw(canvas, Art, ChestCx, ChestGround - 30, 230, Glows.Scaled(new Rgba(60, 36, 12), 0.45 + 0.55 * k));
            if (k <= 0) return;
            var beam = TreasureChest.Beam(Art);
            var top = Layout().CardBottom - 40; var hgt = MouthY - top;
            canvas.BlitRect(beam, new RectF(ChestCx - 110, top, 220, hgt), Glows.Scaled(new Rgba(150, 100, 36), k * pulse), Blend.Add);
            canvas.BlitRect(beam, new RectF(ChestCx - 48, top + hgt * 0.25f, 96, hgt * 0.75f), Glows.Scaled(new Rgba(190, 140, 64), k * pulse * 0.8), Blend.Add);
            canvas.BlitRect(beam, new RectF(ChestCx - 18, top + hgt * 0.5f, 36, hgt * 0.5f), Glows.Scaled(new Rgba(255, 220, 150), k * pulse * 0.45), Blend.Add);
        }

        private void DrawEmpty(Canvas canvas, TreasureLayout lay)
        {
            if (State == Phase.Intro && t < OpenAt + 0.2f) return;
            var r = lay.Empty;
            PixelUi.Panel(canvas, Art, r);
            PixelUi.InkText(canvas, Art, "El cofre está vacío", 20, Anchor.Center, r.CenterX, r.Y + 44, Gilt, true, true);
            PixelUi.InkText(canvas, Art, "Solo quedan unas monedas: ya tienes", 13, Anchor.Center, r.CenterX, r.Y + 80, Cream);
            PixelUi.InkText(canvas, Art, "todas las reliquias que podías encontrar.", 13, Anchor.Center, r.CenterX, r.Y + 100, Cream);
        }

        /// <summary>How visible card <paramref name="i"/> is now (it appears as its relic arrives; it fades when you leave).</summary>
        private float CardAlpha(int i)
        {
            switch (State)
            {
                case Phase.Intro: return Smooth((t - RiseStart - i * Stagger - RiseT * 0.45f) / CardFadeT);
                case Phase.Taking: return 1f - Smooth((t - 0.15f) / 0.35f);
                case Phase.Skipping: return 1f - Smooth(t / 0.3f);
                case Phase.Done: return 0f;
                default: return 1f;
            }
        }

        private void DrawCard(Canvas canvas, RelicCardView v)
        {
            var i = v.Index; var a = CardAlpha(i); if (a <= 0.01f) return;
            var choosing = State == Phase.Choosing; var many = relics.Count > 1;
            var sel = many && i == selected; var hov = choosing && hovered == i;
            var look = new RelicCardLook
            {
                Alpha = a, Time = time, Many = many, Selected = sel, Hovered = hov,
                Dy = (float)Math.Round((1 - a) * 6) * 2 * (State == Phase.Intro ? 1 : -1),
                Lift = choosing && (sel || hov) ? -2 : 0,
                // the relic floats in its niche (empty while it is rising or once it has left)
                WithRelic = State == Phase.Choosing || State == Phase.Intro && RiseU(i) >= 1f,
                Dimmed = !sel && many && choosing, Arrow = sel && choosing,
            };
            RelicCards.Draw(canvas, Game, v, relics[i], look);
        }

        private void DrawChest(Canvas canvas)
        {
            var frame = LidFrame; var rect = ChestRect;
            var dx = 0f;
            if (State == Phase.Intro && t < OpenAt - 0.12f && t > 0.1f)
            {
                // the chest rattles harder until the lid gives
                var k = (t - 0.1f) / (OpenAt - 0.22f);
                dx = (float)Math.Round(Math.Sin(t * 46) * (0.6 + 1.2 * k)) * 2;
            }
            rect = rect.Move(dx, 0);
            GroundShadow.Draw(canvas, Art, ChestCx, ChestGround, (TreasureChest.Right - TreasureChest.Left + 12) * 2, 2, 1f, new Rgba(52, 34, 16));
            var (paint, light) = TreasureChest.Sprites(Art, frame);
            canvas.BlitRect(paint, rect, Rgba.White);
            var pulse = 0.85 + 0.15 * Math.Sin(time * 2.4);
            canvas.BlitRect(light, rect, Glows.Scaled(Rgba.White, 0.8 * pulse + 0.4 * flash), Blend.Add);
            var k2 = Openness;
            if (k2 > 0) Glows.Draw(canvas, Art, ChestCx, MouthY + 2, 70 + 30 * flash, Glows.Scaled(new Rgba(150, 100, 40), 0.35 * k2 * pulse + 0.6 * flash));
            if (flash > 0) Glows.Draw(canvas, Art, ChestCx, MouthY, 200, Glows.Scaled(new Rgba(255, 210, 130), 0.5 * flash));
            // the lock glints while the chest is closed
            if (frame == 0 && State != Phase.Done)
            {
                var k = Math.Sin(time * 2.6);
                if (k > 0.35) RelicCards.Star(canvas, rect.X + (TreasureChest.LockX - 3) * 2, rect.Y + (TreasureChest.LockY - 4) * 2, 2 * (1 + (int)(k * 2.2)), Glint);
            }
        }

        /// <summary>Relics rising from the chest (intro), the taken one flying to the counter, the rest sinking back.</summary>
        private void DrawFlights(Canvas canvas, TreasureLayout lay)
        {
            if (State == Phase.Intro)
            {
                for (var i = 0; i < relics.Count; i++)
                {
                    var u = RiseU(i); if (u <= 0f || u >= 1f) continue;
                    var (x, y) = RisePoint(i, u);
                    var size = (int)Math.Round((32 + (IconSize - 32) * EaseOut(u)) / 2) * 2;
                    canvas.PushClip(new RectF(0, 0, Canvas.Width, BodyTopY));
                    Glows.Draw(canvas, Art, x, y, 30 + size / 3f, Glows.Scaled(CardWidget.RarityColor(relics[i].Definition.Rarity), 0.55));
                    RelicViewer.DrawRelicIcon(canvas, Art, relics[i].Definition.Name, size, x, y);
                    canvas.PopClip();
                }
                return;
            }
            if (State != Phase.Taking && State != Phase.Skipping) return;
            for (var i = 0; i < relics.Count; i++)
            {
                var from = lay.Cards[i].Niche;
                if (State == Phase.Taking && i == selected)
                {
                    if (landed) continue;
                    var u = EaseOut(t / FlyT);
                    float x0 = from.CenterX, y0 = from.CenterY - 6, x1 = RelicCounter.X + 22, y1 = RelicCounter.CenterY;
                    var x = x0 + (x1 - x0) * u; var y = y0 + (y1 - y0) * u - 70 * (float)Math.Sin(Math.PI * u);
                    var size = (int)Math.Round((IconSize - 62 * u) / 2) * 2;
                    Glows.Draw(canvas, Art, x, y, 44, Glows.Scaled(CardWidget.RarityColor(relics[i].Definition.Rarity), 0.6));
                    RelicViewer.DrawRelicIcon(canvas, Art, relics[i].Definition.Name, size, x, y);
                    continue;
                }
                // back into the chest
                var s = Smooth(t / SinkT); if (s >= 1f) continue;
                float sx0 = from.CenterX, sy0 = from.CenterY - 6, sx1 = ChestCx, sy1 = MouthY + 6;
                var sx = sx0 + (sx1 - sx0) * s; var sy = sy0 + (sy1 - sy0) * s - 40 * (float)Math.Sin(Math.PI * s);
                var ssize = (int)Math.Round((IconSize - (IconSize - 32) * s) / 2) * 2;
                canvas.PushClip(new RectF(0, 0, Canvas.Width, BodyTopY));
                RelicViewer.DrawRelicIcon(canvas, Art, relics[i].Definition.Name, ssize, sx, sy, 1f - 0.4f * s);
                canvas.PopClip();
            }
        }

        /// <summary>Pico de Minero / Ganzúa: the gold they added when the room opened, beside the chest.</summary>
        private void DrawBonus(Canvas canvas, TreasureLayout lay)
        {
            if (!lay.HasBonus || State == Phase.Intro && t < OpenAt) return;
            var a = State == Phase.Intro ? Smooth((t - OpenAt) / 0.3f) : 1f;
            canvas.PushAlpha(a);
            var r = lay.Bonus;
            PixelUi.Panel(canvas, Art, r);
            var coin = Art.Icon("coin");
            var x = r.X + 14;
            if (coin.Valid) { canvas.BlitRect(coin, new RectF(x, r.Y + 18 - coin.H, coin.W * 2, coin.H * 2), Rgba.White); x += coin.W * 2 + 6; }
            PixelUi.InkText(canvas, Art, "+" + bonusGold + " oro", 16, Anchor.MidLeft, x, r.Y + 17, Gilt, false, true);
            PixelUi.InkText(canvas, Art, bonusSources, 13, Anchor.MidLeft, r.X + 14, r.Y + 39, Dim);
            canvas.PopAlpha();
        }

        private void DrawButtons(Canvas canvas, TreasureLayout lay)
        {
            var on = State == Phase.Choosing;
            var takeState = !on ? ButtonState.Off : hoveredButton == "take" ? ButtonState.Hover : ButtonState.Idle;
            if (takeState == ButtonState.Hover) PixelUi.Frame2(canvas, lay.Take.Move(0, -1), new Rgba(255, 222, 140));
            PixelUi.Button(canvas, Art, lay.Take, lay.TakeLabel, "gold", takeState, relics.Count > 0 ? "bag" : null, "Enter", time, 0f, 16);
            if (lay.HasSkip)
            {
                var skipState = !on ? ButtonState.Off : hoveredButton == "skip" ? ButtonState.Hover : ButtonState.Idle;
                if (skipState == ButtonState.Hover) PixelUi.Frame2(canvas, lay.Skip.Move(0, -1), new Rgba(220, 200, 160));
                PixelUi.Button(canvas, Art, lay.Skip, "Omitir", "bronze", skipState, null, "O", time, 0f, 16);
            }
            if (relics.Count > 1 && on)
            {
                // the selected relic's name over the buttons, and how to pick another
                var name = relics[selected].Definition.Name;
                PixelUi.InkText(canvas, Art, "Elegida: " + name, 13, Anchor.MidRight, lay.Take.X - 24, lay.Take.CenterY - 1, Gilt);
                PixelUi.InkText(canvas, Art, "Elegir", 13, Anchor.MidLeft, lay.Skip.Right + 52, lay.Skip.CenterY - 1, Dim);
                PixelUi.Keycap(canvas, Art, lay.Skip.Right + 36, lay.Skip.CenterY, "1-" + relics.Count);
            }
        }
    }
}
