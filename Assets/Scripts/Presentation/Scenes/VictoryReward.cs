using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;

namespace LastWish.Presentation
{
    public enum RewardStepKind { Gold, Card, Pack, Relic }
    public enum RewardStepState { Upcoming, Current, Done, Skipped }
    /// <summary>Which fight was won: it sets the title, the colours and the texts of the reward screen.</summary>
    public enum VictoryKind { Combat, Elite, Boss }

    /// <summary>One step of the reward screen, shown in the step tracker under the title.</summary>
    public sealed class RewardStep
    {
        public RewardStepKind Kind { get; }
        public string Label { get; }
        public RewardStepState State { get; internal set; }
        /// <summary>What came of it, under the label: "+45 oro", the card or relic taken, "Omitida".</summary>
        public string Result { get; internal set; } = "";
        internal CardDefinition Card;
        internal RelicOffer Relic;
        public RewardStep(RewardStepKind kind, string label) { Kind = kind; Label = label; }
    }

    /// <summary>
    /// The victory rewards, one step at a time (interface redesigned 2026-11-08: the old screen put the gold, the elite's
    /// relic and the cards on one flat page and the boss reward was three bare phases). A step tracker under the title says
    /// what there is and where you are — "Oro ✓ → Carta → Reliquia" (elites), "Oro ✓ → Carta" (normal fights),
    /// "Oro ✓ → Sobre épico → Reliquia" (bosses) — and the screen shows one step at a time: the gold is counted on arrival
    /// (coins fly to the gold counter), then you pick a card (click selects it, a second click / Enter adds it and it flies
    /// to the deck plate) or skip it, then you take the relic (it flies to the relic plate) or leave it. The boss's epic pack
    /// is opened by the scene manager (<see cref="PackOpeningScene"/>) and comes back with what was kept. Same look as the
    /// other rooms: dungeon behind a coloured focus, title band, kit panels and buttons, ink-outlined text. A click or a key
    /// during an animation only hurries it. The scene manager applies the results when <see cref="Cleared"/> is set.
    /// </summary>
    public abstract class VictoryRewardScene : Scene
    {
        public enum Phase { Intro, Enter, Choosing, Taking, Skipping, WaitingPack, Returned, Finished }

        // ------------------------------------------------------------------ layout
        public const int TrackerY = 136, PillH = 46, LinkW = 40, HeadingY = 194, SubY = 220, CardsTop = 282, CardGap = 30, RarityTagY = 18;
        public const int RelicTop = 250, PackCy = 412, PackH = 250, ButtonY = 596, ButtonH = 40, PrimaryW = 264, SecondaryW = 200, ButtonGap = 24, FooterY = 666;
        public static readonly IRect RelicCounter = new IRect(24, 16, 196, 36), DeckCounter = new IRect(24, 58, 196, 36);
        /// <summary>Where the coins of the gold step go: the gold counter in the top right corner.</summary>
        public const float GoldHudX = 1196, GoldHudY = 30;
        // timing (seconds)
        public const float IntroT = 0.75f, GoldPopAt = 0.25f, CoinT = 0.5f, CoinStagger = 0.06f, EnterT = 0.38f, Stagger = 0.07f;
        public const float FlyT = 0.55f, TakeT = 0.78f, SkipT = 0.36f, ReturnT = 0.5f;

        public static int PillW(int steps) => steps <= 2 ? 236 : 220;
        public static int CardsGap(int n) => n <= 4 ? CardGap : n == 5 ? 22 : 14;

        protected static readonly Rgba Ink = Colors.Ink, Cream = Colors.Cream, Dim = new Rgba(186, 176, 160), Gilt = new Rgba(255, 216, 128),
            Done = new Rgba(120, 220, 130), Faint = new Rgba(120, 112, 128), Warm = new Rgba(255, 190, 90);

        /// <summary>Where everything of the current step goes (tests and input use the same rects as the drawing).</summary>
        public sealed class RewardLayout
        {
            public List<RectF> Pills = new List<RectF>();
            public string Heading = "", Sub = "";
            public List<RectF> Cards = new List<RectF>(), RarityTags = new List<RectF>();
            public List<RelicCardView> Relics = new List<RelicCardView>();
            public RectF Pack, Primary, Secondary, Footer;
            public bool HasSecondary;
        }

        protected readonly RunState run;
        protected readonly List<OfferedCard> cards;
        protected readonly List<RelicOffer> relics;
        private readonly CardBonus bonus;
        private readonly List<RewardStep> steps = new List<RewardStep>();
        private readonly DungeonBackdrop backdrop;
        private readonly BurstParticles fx = new BurstParticles(500, 67);
        private readonly Rng rng;
        private RewardLayout layout;
        private int current, selected = -1, hovered = -1;
        private string hoveredButton; private int hoveredPill = -1;
        private float time, t, deckPulse, relicPulse, feedbackTime, shake;
        private string feedbackText = "";
        private bool goldPopped, coinsLanded, landed;
        private int cardsLanded, relicsLanded;
        private (float x, float y) mouse = (-1, -1);

        public VictoryKind Kind { get; }
        public int GoldEarned { get; }
        public int Interest { get; }
        public Phase State { get; private set; }
        public bool Cleared { get; set; }
        public IReadOnlyList<RewardStep> Steps => steps;
        /// <summary>Index of the step being played (the gold step is done from the start).</summary>
        public int Current => current;
        public RewardStep CurrentStep => current < steps.Count ? steps[current] : null;
        /// <summary>Picked card / relic of the current step (−1 = none yet).</summary>
        public int Selected => selected;
        /// <summary>Seconds into the current phase (tests).</summary>
        public float PhaseTime => t;
        public IReadOnlyList<OfferedCard> Cards => cards;
        public IReadOnlyList<RelicOffer> RelicOffers => relics;

        protected VictoryRewardScene(GameContext game, RunState run, VictoryKind kind, int goldEarned, List<OfferedCard> cards, List<RelicOffer> relics) : base(game)
        {
            this.run = run; Kind = kind; GoldEarned = goldEarned; Interest = run?.LastInterest ?? 0;
            this.cards = cards?.Where(c => c != null).ToList() ?? new List<OfferedCard>();
            this.relics = relics?.Where(r => r != null).ToList() ?? new List<RelicOffer>();
            bonus = run != null ? CardBonus.ForRun(run) : default;
            backdrop = new DungeonBackdrop(Art, kind == VictoryKind.Boss ? 59 : kind == VictoryKind.Elite ? 53 : 47, 0.6f);
            rng = new Rng((int)((run?.Seed ?? 1) % 100003) * 13 + (run?.Floor ?? 1) * 7 + (int)kind);
            var gold = AddStep(RewardStepKind.Gold, "Oro");
            gold.State = RewardStepState.Done; gold.Result = GoldText(goldEarned, Interest);
        }

        /// <summary>"+45 oro" / "+45 oro +4 interés".</summary>
        public static string GoldText(int gold, int interest) => "+" + gold + " oro" + (interest > 0 ? " +" + interest + " interés" : "");

        protected RewardStep AddStep(RewardStepKind kind, string label) { var s = new RewardStep(kind, label); steps.Add(s); return s; }

        /// <summary>Call at the end of the subclass constructor, once every step is added.</summary>
        protected void Start()
        {
            current = NextStepAfter(0);
            if (current < steps.Count) steps[current].State = RewardStepState.Current;
            State = Phase.Intro; t = 0;
        }

        private int NextStepAfter(int i) { var k = i + 1; while (k < steps.Count && steps[k].Kind == RewardStepKind.Gold) k++; return k; }

        // ------------------------------------------------------------------ what the subclasses answer
        /// <summary>The epic pack was asked for (boss): the scene manager opens it and calls <see cref="ReturnFromPack"/>.</summary>
        protected virtual void RequestPack() { }
        /// <summary>A step ended: the subclass records what was taken (null = skipped).</summary>
        protected virtual void StepFinished(RewardStep step) { }

        // ------------------------------------------------------------------ texts
        public string Title => Kind == VictoryKind.Boss ? "¡Jefe derrotado!" : Kind == VictoryKind.Elite ? "¡Élite derrotada!" : "¡Victoria!";
        private Rgba TitleColor => Kind == VictoryKind.Boss ? new Rgba(255, 128, 104) : Kind == VictoryKind.Elite ? new Rgba(255, 160, 150) : Gilt;
        private Rgba FocusColor => Kind == VictoryKind.Boss ? new Rgba(96, 22, 18) : Kind == VictoryKind.Elite ? new Rgba(86, 26, 34) : new Rgba(80, 56, 20);

        /// <summary>The ribbon under the title: "Paso 2 de 3 · Elige una carta".</summary>
        public string RibbonText
        {
            get
            {
                if (State == Phase.Finished || current >= steps.Count) return "Recompensas recogidas";
                return "Paso " + (current + 1) + " de " + steps.Count + " · " + Instruction(steps[current]);
            }
        }

        private string Instruction(RewardStep s)
        {
            switch (s.Kind)
            {
                case RewardStepKind.Card: return cards.Count == 0 ? "Sin cartas" : "Elige una carta";
                case RewardStepKind.Pack: return "Abre el sobre épico";
                case RewardStepKind.Relic: return relics.Count == 0 ? "Sin reliquias" : relics.Count == 1 ? "Tómala si quieres" : "Elige una reliquia";
                default: return "Oro";
            }
        }

        private (string heading, string sub) StepTexts(RewardStep s)
        {
            switch (s.Kind)
            {
                case RewardStepKind.Card:
                    if (cards.Count == 0) return ("No hay cartas que elegir", "No queda ninguna carta disponible para tu héroe.");
                    return ("Elige una carta para tu mazo", Kind == VictoryKind.Elite ? "Cartas de élite: más probabilidad de ser raras. Puedes omitirla." : "Se añade a tu mazo. Si ninguna te convence, omítela.");
                case RewardStepKind.Pack:
                    return ("Sobre épico", "Ábrelo y quédate con 1 de sus " + RunState.PackSize + " cartas épicas.");
                case RewardStepKind.Relic:
                    if (relics.Count == 0) return ("No quedan reliquias", "Ya tienes todas las reliquias que podías encontrar.");
                    if (Kind == VictoryKind.Boss) return relics.Count == 1 ? ("Reliquia de jefe", "Tómala o déjala: es opcional.") : ("Elige una reliquia de jefe", "Llévate una de las " + relics.Count + ", o ninguna.");
                    return ("Trofeo de la élite", "Tómala o déjala: es opcional.");
                default: return ("", "");
            }
        }

        private int ItemCount(RewardStep s) => s == null ? 0 : s.Kind == RewardStepKind.Card ? cards.Count : s.Kind == RewardStepKind.Relic ? relics.Count : s.Kind == RewardStepKind.Pack ? 1 : 0;

        /// <summary>The gold button's label for the current step and selection.</summary>
        public string PrimaryLabel
        {
            get
            {
                var s = CurrentStep; if (s == null) return "Continuar";
                var n = ItemCount(s);
                switch (s.Kind)
                {
                    case RewardStepKind.Card: return n == 0 ? "Continuar" : selected < 0 ? "Elige una carta" : "Añadir al mazo";
                    case RewardStepKind.Pack: return "Abrir sobre";
                    case RewardStepKind.Relic: return n == 0 ? "Continuar" : selected < 0 ? "Elige una reliquia" : "Tomar reliquia";
                    default: return "Continuar";
                }
            }
        }

        private string PrimaryIcon
        {
            get
            {
                var s = CurrentStep; if (s == null || ItemCount(s) == 0) return null;
                return s.Kind == RewardStepKind.Card ? "deck" : s.Kind == RewardStepKind.Pack ? "cards" : "bag";
            }
        }

        /// <summary>The gold button can be pressed (a choice is made, or there is nothing to choose).</summary>
        public bool PrimaryEnabled
        {
            get
            {
                var s = CurrentStep; if (s == null) return false;
                var n = ItemCount(s);
                return s.Kind == RewardStepKind.Pack || n == 0 || selected >= 0;
            }
        }

        // ------------------------------------------------------------------ layout
        /// <summary>Everything's place for the current step (cached until the step changes).</summary>
        public RewardLayout Layout()
        {
            if (layout != null) return layout;
            var lay = new RewardLayout();
            var pw = PillW(steps.Count); var total = steps.Count * pw + Math.Max(0, steps.Count - 1) * LinkW;
            var x = (float)Math.Floor((Canvas.Width - total) / 4f) * 2;
            for (var i = 0; i < steps.Count; i++) lay.Pills.Add(new RectF(x + i * (pw + LinkW), TrackerY - PillH / 2, pw, PillH));
            var s = CurrentStep;
            if (s != null) (lay.Heading, lay.Sub) = StepTexts(s);
            var n = ItemCount(s);
            if (s != null && s.Kind == RewardStepKind.Card && n > 0)
            {
                var gap = CardsGap(n); var w = n * CardWidget.CardW + (n - 1) * gap;
                var x0 = (float)Math.Floor((Canvas.Width - w) / 4f) * 2;
                for (var i = 0; i < n; i++)
                {
                    var r = new RectF(x0 + i * (CardWidget.CardW + gap), CardsTop, CardWidget.CardW, CardWidget.CardH);
                    lay.Cards.Add(r);
                    var chip = Tooltip.RarityChip(cards[i].Card.Rarity);
                    var cw = Tooltip.ChipWidth(Art, chip);
                    lay.RarityTags.Add(new RectF((float)Math.Floor((r.CenterX - cw / 2f) / 2) * 2, r.Bottom + RarityTagY - 12, cw, 24));
                }
            }
            else if (s != null && s.Kind == RewardStepKind.Relic && n > 0)
            {
                var h = RelicCards.Height(RelicCards.Lines(Game, relics));
                var top = Math.Min(RelicTop, ButtonY - 14 - h);
                lay.Relics = RelicCards.Build(Game, relics, top);
            }
            else if (s != null && s.Kind == RewardStepKind.Pack)
            {
                var (pw2, ph) = PackArt.Size(Art, PackTheme.Epico, PackH);
                if (pw2 == 0) { pw2 = 180; ph = PackH; }
                lay.Pack = RectF.CenteredInt(Canvas.Width / 2f, PackCy, pw2, ph);
            }
            var two = s != null && s.Kind != RewardStepKind.Pack && n > 0;
            if (two)
            {
                var bw = PrimaryW + ButtonGap + SecondaryW; var bx = (float)Math.Floor((Canvas.Width - bw) / 4f) * 2;
                lay.Primary = new RectF(bx, ButtonY, PrimaryW, ButtonH);
                lay.Secondary = new RectF(bx + PrimaryW + ButtonGap, ButtonY, SecondaryW, ButtonH); lay.HasSecondary = true;
            }
            else lay.Primary = new RectF(Canvas.Width / 2f - PrimaryW / 2f, ButtonY, PrimaryW, ButtonH);
            lay.Footer = RectF.CenteredInt(Canvas.Width / 2f, FooterY, 520, 28);
            return layout = lay;
        }

        /// <summary>The rect card <paramref name="i"/> is drawn in now (lifted when hovered or picked).</summary>
        public RectF CardRect(int i)
        {
            var lay = Layout(); if (i < 0 || i >= lay.Cards.Count) return default;
            var lift = State == Phase.Choosing && (i == selected || i == hovered) ? -20 : 0;
            return lay.Cards[i].Move(0, lift);
        }

        // ------------------------------------------------------------------ input
        public override void HandleEvent(in InputEvent e)
        {
            if (e.Type == EventType.MouseMove) { mouse = (e.X, e.Y); UpdateHover(); return; }
            if (Cleared || e.Repeat) return;
            switch (State)
            {
                case Phase.Intro: case Phase.Enter: case Phase.Taking: case Phase.Skipping: case Phase.Returned:
                    if (e.IsLeftDown || e.Type == EventType.KeyDown) Hurry();
                    return;
                case Phase.Choosing: break;
                default: return;
            }
            var s = CurrentStep; if (s == null) return;
            var lay = Layout(); var n = ItemCount(s);
            if (s.Kind == RewardStepKind.Pack)
            {
                if (e.IsConfirmKey || e.IsLeftDown && (lay.Primary.Contains(e.X, e.Y) || lay.Pack.Contains(e.X, e.Y))) OpenPack();
                return;
            }
            if (n > 0 && e.Type == EventType.KeyDown && e.Digit >= 1 && e.Digit <= Math.Min(9, n)) { Select(e.Digit - 1); return; }
            if (n > 1 && (e.IsKey(Key.Right) || e.IsKey(Key.Tab))) { Select(selected < 0 ? 0 : (selected + 1) % n); return; }
            if (n > 1 && e.IsKey(Key.Left)) { Select(selected < 0 ? n - 1 : (selected + n - 1) % n); return; }
            if (e.IsConfirmKey) { if (n > 0 && selected < 0) Select(0); else Take(); return; }
            if (e.IsKey(Key.O) && lay.HasSecondary) { Skip(); return; }
            if (!e.IsLeftDown) return;
            var item = ItemAt(e.X, e.Y);
            if (item >= 0) { if (item == selected) Take(); else Select(item); return; }
            if (lay.Primary.Contains(e.X, e.Y))
            {
                if (PrimaryEnabled) Take();
                else Refuse(s.Kind == RewardStepKind.Card ? "Primero elige una carta (clic o 1-" + n + ")." : "Primero elige una reliquia (clic o 1-" + n + ").");
            }
            else if (lay.HasSecondary && lay.Secondary.Contains(e.X, e.Y)) Skip();
        }

        private int ItemAt(float x, float y)
        {
            var s = CurrentStep; if (s == null) return -1;
            var lay = Layout();
            if (s.Kind == RewardStepKind.Card) { for (var i = 0; i < lay.Cards.Count; i++) if (CardRect(i).Contains(x, y) || lay.Cards[i].Contains(x, y)) return i; }
            else if (s.Kind == RewardStepKind.Relic) { foreach (var v in lay.Relics) if (v.Rect.Contains(x, y)) return v.Index; }
            return -1;
        }

        private void UpdateHover()
        {
            var lay = Layout(); var before = (hovered, hoveredButton);
            hovered = -1; hoveredButton = null; hoveredPill = -1;
            for (var i = 0; i < lay.Pills.Count; i++) if (lay.Pills[i].Contains(mouse)) hoveredPill = i;
            if (State != Phase.Choosing) return;
            hovered = ItemAt(mouse.x, mouse.y);
            if (CurrentStep?.Kind == RewardStepKind.Pack && lay.Pack.Contains(mouse)) hoveredButton = "pack";
            if (lay.Primary.Contains(mouse)) hoveredButton = "primary";
            else if (lay.HasSecondary && lay.Secondary.Contains(mouse)) hoveredButton = "secondary";
            if ((hovered >= 0 || hoveredButton != null) && (hovered, hoveredButton) != before) Sound.PlayNav();
        }

        private void Select(int i)
        {
            if (i < 0 || i >= ItemCount(CurrentStep)) return;
            if (i != selected) Sound.PlayNav();
            selected = i;
        }

        private void Refuse(string text)
        {
            Sound.PlayError(); feedbackText = text; feedbackTime = 1.8f; shake = 0.35f;
        }

        /// <summary>Jump to the end of the arrival show of the current step (tests; a click or key during it).</summary>
        public void SkipIntro()
        {
            for (var guard = 0; guard < 4 && (State == Phase.Intro || State == Phase.Enter); guard++) Hurry();
        }

        /// <summary>A click or key during an animation: finish it at once (never takes nor skips anything).</summary>
        private void Hurry()
        {
            switch (State)
            {
                case Phase.Intro: PopGold(); coinsLanded = true; BeginStep(); break;
                case Phase.Enter: Arrived(); State = Phase.Choosing; t = 0; UpdateHover(); break;
                case Phase.Taking: if (!landed) Land(); EndStep(); break;
                case Phase.Skipping: case Phase.Returned: EndStep(); break;
            }
        }

        private void OpenPack()
        {
            if (State != Phase.Choosing) return;
            Sound.PlayOpenPack();
            State = Phase.WaitingPack; t = 0; hovered = -1; hoveredButton = null;
            RequestPack();
        }

        /// <summary>The epic pack came back (boss): what was kept goes into the tracker, then the next step.</summary>
        protected void ReturnFromPack(IReadOnlyList<CardDefinition> kept)
        {
            var s = CurrentStep;
            if (s == null || s.Kind != RewardStepKind.Pack || State != Phase.WaitingPack) return;
            if (kept != null && kept.Count > 0)
            {
                s.State = RewardStepState.Done; s.Card = kept[0];
                s.Result = kept.Count == 1 ? kept[0].Name : kept[0].Name + " +" + (kept.Count - 1);
                deckPulse = 1f;
                fx.Burst(DeckCounter.X + 22, DeckCounter.CenterY, 16, new[] { Rgba.White, Gilt, Warm }, (60f, 200f), null, (0.3f, 0.6f), (1.5f, 3f), ParticleStyle.Spark, 3f);
            }
            else { s.State = RewardStepState.Skipped; s.Result = "Omitida"; }
            StepFinished(s);
            State = Phase.Returned; t = 0;
        }

        private void Take()
        {
            if (State != Phase.Choosing) return;
            var s = CurrentStep; if (s == null) return;
            var n = ItemCount(s);
            if (n == 0) { Sound.PlayConfirm(); s.State = RewardStepState.Skipped; s.Result = "Nada"; StepFinished(s); EndStep(); return; }
            if (selected < 0) return;
            landed = false;
            if (s.Kind == RewardStepKind.Card)
            {
                s.Card = cards[selected].Card; s.Result = s.Card.Name;
                Sound.PlayReward();
            }
            else
            {
                s.Relic = relics[selected]; s.Result = s.Relic.Definition.Name;
                Sound.PlayGachaKeep();
                var nr = Layout().Relics[selected].Niche;
                fx.Burst(nr.CenterX, nr.CenterY - 6, 20, RelicCards.Palette(s.Relic.Definition.Rarity), (60f, 220f), null, (0.3f, 0.6f), (1.5f, 3f), ParticleStyle.Spark, 3f);
            }
            s.State = RewardStepState.Done;
            StepFinished(s);
            State = Phase.Taking; t = 0; hovered = -1; hoveredButton = null;
        }

        private void Skip()
        {
            if (State != Phase.Choosing) return;
            var s = CurrentStep; if (s == null) return;
            s.State = RewardStepState.Skipped; s.Result = "Omitida";
            StepFinished(s);
            Sound.PlayCancel();
            State = Phase.Skipping; t = 0; hovered = -1; hoveredButton = null;
        }

        /// <summary>The taken card / relic reached its plate.</summary>
        private void Land()
        {
            landed = true;
            var s = CurrentStep; if (s == null) return;
            if (s.Kind == RewardStepKind.Card)
            {
                cardsLanded++; deckPulse = 1f; Sound.PlayCard();
                fx.Burst(DeckCounter.X + 22, DeckCounter.CenterY, 16, RelicCards.Palette(s.Card?.Rarity ?? 1), (60f, 200f), null, (0.3f, 0.6f), (1.5f, 3f), ParticleStyle.Spark, 3f);
            }
            else if (s.Kind == RewardStepKind.Relic)
            {
                relicsLanded++; relicPulse = 1f; Sound.PlayGachaStash();
                fx.Burst(RelicCounter.X + 22, RelicCounter.CenterY, 18, RelicCards.Palette(s.Relic?.Definition.Rarity ?? 1), (60f, 200f), null, (0.3f, 0.6f), (1.5f, 3f), ParticleStyle.Spark, 3f);
            }
        }

        /// <summary>The current step is over: on to the next one, or the rewards are done.</summary>
        private void EndStep()
        {
            var next = NextStepAfter(current);
            selected = -1; hovered = -1; hoveredButton = null; layout = null;
            if (next >= steps.Count) { current = steps.Count; State = Phase.Finished; t = 0; Cleared = true; return; }
            current = next; steps[current].State = RewardStepState.Current;
            BeginStep();
        }

        private void BeginStep()
        {
            layout = null; selected = -1; hovered = -1; hoveredButton = null; landed = false;
            var s = CurrentStep;
            if (s == null) { State = Phase.Finished; Cleared = true; return; }
            if (s.Kind == RewardStepKind.Relic && relics.Count == 1) selected = 0;     // a single relic is the choice already
            State = Phase.Enter; t = 0;
            if (s.Kind == RewardStepKind.Card && cards.Count > 0) Sound.PlayCard();
        }

        private float EnterLength => EnterT + Math.Max(0, ItemCount(CurrentStep) - 1) * Stagger;

        /// <summary>The items of the step have arrived: sparks in the niches, the relic's rarity sound.</summary>
        private void Arrived()
        {
            var s = CurrentStep; if (s == null) return;
            if (s.Kind == RewardStepKind.Relic && relics.Count > 0)
            {
                Sound.PlayGachaTier(relics.Max(r => Math.Max(1, Math.Min(5, r.Definition.Rarity))));
                if (relics.Any(r => r.Chroma != ChromaKind.None)) Sound.PlayGachaGolden();
                foreach (var v in Layout().Relics)
                    fx.Burst(v.Niche.CenterX, v.Niche.CenterY - 6, 14, RelicCards.Palette(relics[v.Index].Definition.Rarity), (60f, 200f), null, (0.3f, 0.6f), (1.5f, 3f), ParticleStyle.Spark, 3f);
            }
        }

        private void PopGold()
        {
            if (goldPopped) return;
            goldPopped = true;
            Sound.PlayPurchase();
            var pill = Layout().Pills[0];
            fx.Burst(pill.X + 26, pill.CenterY, 22, new[] { Rgba.White, new Rgba(255, 238, 190), Gilt, new Rgba(214, 150, 40) }, (60f, 220f), null, (0.3f, 0.7f), (1.5f, 3f), ParticleStyle.Spark, 3f);
        }

        // ------------------------------------------------------------------ simulation
        public override void Update(float dt)
        {
            dt = Math.Min(0.1f, Math.Max(0f, dt));
            time += dt; t += dt;
            deckPulse = Math.Max(0f, deckPulse - dt * 2.2f); relicPulse = Math.Max(0f, relicPulse - dt * 2.2f);
            feedbackTime = Math.Max(0f, feedbackTime - dt); shake = Math.Max(0f, shake - dt);
            backdrop.Update(dt);
            switch (State)
            {
                case Phase.Intro:
                    if (!goldPopped && t >= GoldPopAt) PopGold();
                    if (!coinsLanded && t >= GoldPopAt + CoinT + 5 * CoinStagger)
                    {
                        coinsLanded = true;
                        fx.Burst(GoldHudX, GoldHudY, 14, new[] { Rgba.White, Gilt, new Rgba(214, 150, 40) }, (40f, 160f), null, (0.25f, 0.5f), (1.5f, 2.5f), ParticleStyle.Spark, 3f);
                    }
                    if (t >= IntroT) BeginStep();
                    break;
                case Phase.Enter:
                    if (t >= EnterLength) { Arrived(); State = Phase.Choosing; t = 0; UpdateHover(); }
                    break;
                case Phase.Taking:
                    if (!landed && t >= FlyT) Land();
                    if (t >= TakeT) EndStep();
                    break;
                case Phase.Skipping:
                    if (t >= SkipT) EndStep();
                    break;
                case Phase.Returned:
                    if (t >= ReturnT) EndStep();
                    break;
            }
            // coins still in the air after the intro was hurried keep flying (drawn from the clock)
            fx.Update(dt);
        }

        // ------------------------------------------------------------------ drawing
        private static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        private static float EaseOut(float v) { v = Clamp01(v); return 1 - (1 - v) * (1 - v) * (1 - v); }
        private static float Smooth(float v) { v = Clamp01(v); return v * v * (3 - 2 * v); }

        public override void Draw(Canvas canvas)
        {
            var lay = Layout();
            backdrop.Draw(canvas);
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(10, 6, 6, 150));
            canvas.BlitRect(Vignette(), new RectF(0, 0, Canvas.Width, Canvas.Height), Rgba.White);
            Glows.Draw(canvas, Art, 640, 420, 300, Glows.Scaled(FocusColor, 0.55));
            DrawBand(canvas);
            DrawCounters(canvas);
            DrawTracker(canvas, lay);
            DrawStep(canvas, lay);
            DrawFlights(canvas, lay);
            DrawCoins(canvas, lay);
            fx.Draw(canvas, Art);
            DrawButtons(canvas, lay);
            DrawTooltips(canvas, lay);
        }

        private Sprite Vignette() => new Sprite(Art.Procedural("victory_vignette", 160, 90, () =>
        {
            var px = new Rgba[160 * 90];
            for (var y = 0; y < 90; y++)
                for (var x = 0; x < 160; x++)
                {
                    var d = Math.Sqrt(Math.Pow((x - 80) / 96.0, 2) + Math.Pow((y - 52) / 62.0, 2));
                    px[y * 160 + x] = new Rgba(8, 4, 4, (int)(228 * Math.Pow(MathX.Clamp01((d - 0.46) / 0.6), 1.4)));
                }
            return px;
        }, smooth: true));

        private Sprite Band() => new Sprite(Art.Procedural("victory_band", 1, 96, () =>
        {
            var px = new Rgba[96];
            for (var y = 0; y < 96; y++) px[y] = new Rgba(10, 6, 6, (int)(220 * Math.Pow(1 - y / 96.0, 1.4)));
            return px;
        }));

        private void DrawBand(Canvas canvas)
        {
            canvas.BlitRect(Band(), new RectF(0, 0, Canvas.Width, 96), Rgba.White);
            Glows.Draw(canvas, Art, 640, 40, 160, Glows.Scaled(FocusColor, 1.1));
            PixelUi.InkText(canvas, Art, Title, 30, Anchor.Center, 640, 38, TitleColor, true, true);
            PixelUi.Ribbon(canvas, Art, 640, 80, RibbonText, 13);
        }

        /// <summary>The deck and relic plates in the top left corner: where taken cards and relics land.</summary>
        /// <summary>The numbers on the deck and relic plates: taken cards and relics join the run when the scene closes (the
        /// scene manager adds them), so they are counted as soon as they land on their plate.</summary>
        public int ShownDeckCount => (run?.Deck.Count ?? 0) + (Cleared ? 0 : cardsLanded);
        public int ShownRelicCount => (run?.OwnedRelics.Count ?? 0) + (Cleared ? 0 : relicsLanded);

        private void DrawCounters(Canvas canvas)
        {
            Plate(canvas, DeckCounter, "deck", "Mazo", ShownDeckCount, deckPulse);
            Plate(canvas, RelicCounter, "bag", "Reliquias", ShownRelicCount, relicPulse);
        }

        private void Plate(Canvas canvas, RectF plate, string icon, string label, int count, float pulse)
        {
            if (pulse > 0) { plate = plate.Move(0, pulse > 0.5f ? -2 : 0); PixelUi.Frame2(canvas, plate, Glows.Scaled(Gilt, 0.6 + 0.6 * pulse)); }
            PixelUi.Panel(canvas, Art, plate);
            var sprite = Art.Icon(icon);
            if (sprite.Valid) canvas.BlitRect(sprite, new RectF(plate.X + 10, plate.CenterY - sprite.H, sprite.W * 2, sprite.H * 2), Rgba.White);
            PixelUi.InkText(canvas, Art, label, 13, Anchor.MidLeft, plate.X + 44, plate.CenterY - 1, Dim);
            PixelUi.InkText(canvas, Art, count.ToString(), 20, Anchor.MidRight, plate.Right - 12, plate.CenterY - 1, Gilt);
        }

        private static string StepIcon(RewardStepKind kind) => kind == RewardStepKind.Gold ? "coin" : kind == RewardStepKind.Card ? "deck" : kind == RewardStepKind.Pack ? "cards" : "bag";

        /// <summary>The step tracker: one plate per step joined by dotted links, done steps ticked with what came of them.</summary>
        private void DrawTracker(Canvas canvas, RewardLayout lay)
        {
            var appear = State == Phase.Intro ? Smooth(t / 0.3f) : 1f;
            canvas.PushAlpha(appear);
            for (var i = 0; i + 1 < lay.Pills.Count; i++)
            {
                var a = lay.Pills[i]; var b = lay.Pills[i + 1];
                var done = steps[i].State == RewardStepState.Done || steps[i].State == RewardStepState.Skipped;
                var col = done ? Glows.Scaled(Gilt, 0.8) : new Rgba(84, 76, 92);
                for (var x = (int)a.Right + 6; x < b.X - 6; x += 6) canvas.Fill(new RectF(x, (float)Math.Floor(a.CenterY / 2) * 2, 2, 2), col);
                // a small chevron in the middle of the link
                var mx = (float)Math.Floor((a.Right + b.X) / 4) * 2; var my = (float)Math.Floor(a.CenterY / 2) * 2;
                for (var k = 0; k < 3; k++) { canvas.Fill(new RectF(mx - 2 + k * 2, my - 4 + k * 2, 2, 2), col); canvas.Fill(new RectF(mx - 2 + k * 2, my + 4 - k * 2, 2, 2), col); }
            }
            for (var i = 0; i < lay.Pills.Count; i++) DrawPill(canvas, lay.Pills[i], steps[i], i);
            canvas.PopAlpha();
        }

        private void DrawPill(Canvas canvas, RectF r, RewardStep s, int i)
        {
            var state = s.State;
            var popping = s.Kind == RewardStepKind.Gold && State == Phase.Intro && t < GoldPopAt;
            if (popping) state = RewardStepState.Current;
            var pulse = 0.5 + 0.5 * Math.Sin(time * 3.2);
            Rgba edge, fill, label, result;
            switch (state)
            {
                case RewardStepState.Current: edge = Glows.Scaled(Gilt, 0.8 + 0.2 * pulse); fill = new Rgba(44, 32, 18, 236); label = Gilt; result = Cream; break;
                case RewardStepState.Done: edge = new Rgba(84, 168, 96); fill = new Rgba(18, 32, 22, 236); label = Done; result = Cream; break;
                case RewardStepState.Skipped: edge = new Rgba(92, 86, 98); fill = new Rgba(22, 20, 28, 236); label = Faint; result = Faint; break;
                default: edge = new Rgba(70, 64, 80); fill = new Rgba(16, 14, 22, 220); label = new Rgba(150, 142, 158); result = Faint; break;
            }
            if (state == RewardStepState.Current) PixelUi.Frame2(canvas, r.Inflate(-4, -4), Glows.Scaled(Gilt, 0.45 + 0.35 * pulse));
            PixelUi.PixelBox(canvas, r, fill, edge, Glows.Scaled(edge, 0.55).WithAlpha(200));
            // badge: the step's icon in a little box, a tick when it is done
            var badge = new RectF(r.X + 8, r.CenterY - 17, 34, 34);
            PixelUi.PixelBox(canvas, badge, new Rgba(10, 8, 14), Ink, Glows.Scaled(edge, 0.5));
            var icon = Art.Icon(StepIcon(s.Kind));
            var tint = state == RewardStepState.Upcoming || state == RewardStepState.Skipped ? new Rgba(150, 140, 150) : Rgba.White;
            if (icon.Valid) canvas.BlitRect(icon, new RectF(badge.CenterX - icon.W, badge.CenterY - icon.H, icon.W * 2, icon.H * 2), tint);
            if (state == RewardStepState.Done) Tick(canvas, badge.Right - 6, badge.Bottom - 4);
            if (state == RewardStepState.Skipped) canvas.Fill(new RectF(badge.X + 6, badge.CenterY - 1, badge.W - 12, 2), new Rgba(200, 90, 80));
            var tx = r.X + 52; var room = (int)(r.Right - 10 - tx);
            var title = (state == RewardStepState.Current || state == RewardStepState.Upcoming ? (i + 1) + ". " : "") + s.Label;
            PixelUi.InkText(canvas, Art, title, 16, Anchor.MidLeft, tx, r.CenterY - 9, label, false, true);
            var line = state == RewardStepState.Current ? (popping ? "" : Instruction(s)) : state == RewardStepState.Upcoming ? "Pendiente" : s.Result;
            if (!string.IsNullOrEmpty(line))
                PixelUi.InkText(canvas, Art, TextFit.Fit(Fonts.Get(13), line, room - 2), 13, Anchor.MidLeft, tx, r.CenterY + 10, result);
        }

        /// <summary>A green tick (2 px grain) whose bottom tip is at (x, y).</summary>
        private static void Tick(Canvas canvas, float x, float y)
        {
            float cx = (float)Math.Floor(x / 2) * 2, cy = (float)Math.Floor(y / 2) * 2;
            var pts = new[] { (-8, -4), (-6, -2), (-4, 0), (-2, -2), (0, -4), (2, -6), (4, -8), (6, -10) };
            foreach (var (dx, dy) in pts) canvas.Fill(new RectF(cx + dx - 2, cy + dy - 2, 6, 6), Ink);
            foreach (var (dx, dy) in pts) canvas.Fill(new RectF(cx + dx, cy + dy, 2, 2), new Rgba(150, 255, 150));
        }

        /// <summary>How visible the items of the current step are (and their slide): they come in staggered, leave on a pick.</summary>
        private float ItemAlpha(int i)
        {
            switch (State)
            {
                case Phase.Intro: return 0f;
                case Phase.Enter: return Smooth((t - i * Stagger) / EnterT);
                case Phase.Taking: return i == selected ? 1f - Smooth((t - 0.15f) / 0.35f) : 1f - Smooth(t / 0.3f);
                case Phase.Skipping: return 1f - Smooth(t / SkipT);
                case Phase.WaitingPack: case Phase.Returned: case Phase.Finished: return 0f;
                default: return 1f;
            }
        }

        private float HeadingAlpha => State == Phase.Intro ? 0f : State == Phase.Enter ? Smooth(t / 0.25f) : State == Phase.Finished || State == Phase.WaitingPack || State == Phase.Returned ? 0f : 1f;

        private void DrawStep(Canvas canvas, RewardLayout lay)
        {
            var s = CurrentStep; if (s == null) return;
            var ha = HeadingAlpha;
            if (ha > 0.01f)
            {
                canvas.PushAlpha(ha);
                PixelUi.InkText(canvas, Art, lay.Heading, 20, Anchor.Center, 640, HeadingY, Cream, true, true);
                PixelUi.InkText(canvas, Art, lay.Sub, 13, Anchor.Center, 640, SubY, Dim);
                canvas.PopAlpha();
            }
            switch (s.Kind)
            {
                case RewardStepKind.Card: DrawCards(canvas, lay); break;
                case RewardStepKind.Relic: DrawRelics(canvas, lay); break;
                case RewardStepKind.Pack: DrawPack(canvas, lay); break;
            }
            if (ItemCount(s) == 0 && s.Kind != RewardStepKind.Pack && ha > 0.01f)
            {
                canvas.PushAlpha(ha);
                var box = RectF.CenteredInt(640, 380, 460, 110);
                PixelUi.Panel(canvas, Art, box);
                PixelUi.InkText(canvas, Art, s.Kind == RewardStepKind.Card ? "Nada que añadir" : "El trofeo ya es tuyo", 20, Anchor.Center, box.CenterX, box.Y + 40, Gilt, true, true);
                PixelUi.InkText(canvas, Art, "Pulsa Continuar para seguir.", 13, Anchor.Center, box.CenterX, box.Y + 74, Cream);
                canvas.PopAlpha();
            }
        }

        private void DrawCards(Canvas canvas, RewardLayout lay)
        {
            var choosing = State == Phase.Choosing;
            for (var i = 0; i < lay.Cards.Count; i++)
            {
                if (State == Phase.Taking && i == selected) continue;      // it is flying to the deck
                var a = ItemAlpha(i); if (a <= 0.01f) continue;
                var offer = cards[i]; var sel = choosing && i == selected; var hov = choosing && i == hovered;
                var dy = (float)Math.Round((1 - a) * 8) * 2 * (State == Phase.Enter ? 1 : -1);
                var shakeX = sel || selected >= 0 || shake <= 0 ? 0f : (float)Math.Round(Math.Sin(time * 60) * 2) * 2 * (shake / 0.35f);
                var at = lay.Cards[i].Move(shakeX, dy);
                canvas.PushAlpha(a);
                var lift = sel || hov ? -20 : 0;
                // once a card is picked the others step back (darker, not hidden: you can still change your mind)
                var dimmed = choosing && selected >= 0 && !sel;
                if (dimmed) canvas.PushTint(hov ? new Rgba(190, 186, 196) : new Rgba(128, 124, 136));
                if (offer.Lucky) CardFx.DrawLuckyBack(canvas, Art, at.Move(0, lift), time);
                if (sel) PixelUi.Frame2(canvas, at.Move(0, lift).Inflate(2, 2), Glows.Scaled(new Rgba(255, 222, 140), 0.8 + 0.25 * Math.Sin(time * 3.0)));
                var rect = CardWidget.Draw(canvas, Game, offer.Card, at.X, at.Y, bonus.For(offer.Card), selected: sel, hovered: hov && !sel);
                if (offer.Lucky) CardFx.DrawLuckyFront(canvas, Game, rect, time);
                if (dimmed) canvas.PopTint();
                if (sel) RelicCards.Chevron(canvas, rect.CenterX, rect.Y - 6 + (float)Math.Round(Math.Sin(time * 4.0) * 1.2) * 2);
                // the rarity under the card stays put while the card lifts; the key that picks it beside it
                var tag = lay.RarityTags[i].Move(0, dy);
                Tooltip.DrawChipCentered(canvas, Art, Tooltip.RarityChip(offer.Card.Rarity), tag.CenterX, tag.CenterY);
                if (cards.Count > 1 && i < 9) PixelUi.Keycap(canvas, Art, tag.X - 16, tag.CenterY, (i + 1).ToString());
                canvas.PopAlpha();
            }
            if (State == Phase.Choosing || State == Phase.Enter)
                LuckBadge.Draw(canvas, Game, Anchor.BottomLeft, 16, 704, LuckReport.For(run));
        }

        private void DrawRelics(Canvas canvas, RewardLayout lay)
        {
            var choosing = State == Phase.Choosing; var many = relics.Count > 1;
            foreach (var v in lay.Relics)
            {
                var i = v.Index; var a = ItemAlpha(i); if (a <= 0.01f) continue;
                var sel = many && i == selected; var hov = choosing && hovered == i;
                var look = new RelicCardLook
                {
                    Alpha = a, Time = time, Many = many, Selected = sel && choosing, Hovered = hov,
                    Dy = (float)Math.Round((1 - a) * 6) * 2 * (State == Phase.Enter ? 1 : -1),
                    Lift = choosing && (sel || hov) ? -2 : 0,
                    WithRelic = State == Phase.Choosing || State == Phase.Enter && Smooth((t - i * Stagger) / EnterT) >= 0.6f || State == Phase.Skipping,
                    Dimmed = many && choosing && selected >= 0 && !sel, Arrow = sel && choosing,
                };
                if (State == Phase.Taking && i == selected) look.WithRelic = false;
                RelicCards.Draw(canvas, Game, v, relics[i], look);
            }
        }

        private void DrawPack(Canvas canvas, RewardLayout lay)
        {
            var a = ItemAlpha(0); if (a <= 0.01f) return;
            var r = lay.Pack; var hov = State == Phase.Choosing && (hoveredButton == "pack" || hoveredButton == "primary");
            var dy = (float)Math.Round((1 - a) * 8) * 2 + (float)Math.Round(Math.Sin(time * 1.6) * 2) * 2 + (hov ? -4 : 0);
            canvas.PushAlpha(a);
            var pulse = 0.85 + 0.15 * Math.Sin(time * 2.2);
            Glows.Draw(canvas, Art, r.CenterX, r.CenterY + dy, 190, Glows.Scaled(new Rgba(120, 50, 140), 0.55 * pulse));
            Glows.Draw(canvas, Art, r.CenterX, r.CenterY + dy, 110, Glows.Scaled(new Rgba(255, 190, 110), 0.25 * pulse + (hov ? 0.12 : 0)));
            GroundShadow.Draw(canvas, Art, r.CenterX, r.Bottom + 26, r.W * 0.9f, 2, 0.8f, new Rgba(40, 20, 46));
            var drawn = PackArt.Draw(canvas, Art, PackTheme.Epico, (int)r.H, r.CenterX, r.CenterY + dy, ChromaKind.None, time, 1f);
            if (drawn.W <= 0) { PixelUi.Panel(canvas, Art, r.Move(0, dy)); PixelUi.InkText(canvas, Art, "Sobre Épico", 20, Anchor.Center, r.CenterX, r.CenterY + dy, Gilt, true, true); }
            if (hov) PixelUi.Frame2(canvas, r.Move(0, dy).Inflate(6, 6), new Rgba(255, 222, 140));
            // motes rising around the pack
            for (var k = 0; k < 6; k++)
            {
                var ph = (time * 0.35f + k / 6f) % 1f;
                var mx = r.X + 8 + (r.W - 16) * ((k * 37 % 11) / 10f); var my = r.Bottom - ph * (r.H + 40);
                var arm = ph < 0.5f ? 2 : 4;
                RelicCards.Star(canvas, mx, my + dy, arm, Glows.Scaled(new Rgba(255, 200, 140), 1.0 - Math.Abs(ph - 0.5)));
            }
            canvas.PopAlpha();
        }

        /// <summary>The taken card flies to the deck plate; a taken relic flies to the relic plate.</summary>
        private void DrawFlights(Canvas canvas, RewardLayout lay)
        {
            if (State != Phase.Taking || landed || selected < 0) return;
            var s = CurrentStep; if (s == null) return;
            var u = EaseOut(t / FlyT);
            if (s.Kind == RewardStepKind.Card && selected < lay.Cards.Count)
            {
                var from = lay.Cards[selected].Move(0, -20);
                float x0 = from.CenterX, y0 = from.CenterY, x1 = DeckCounter.X + 22, y1 = DeckCounter.CenterY;
                var x = x0 + (x1 - x0) * u; var y = y0 + (y1 - y0) * u - 60 * (float)Math.Sin(Math.PI * u);
                var k = 1f - 0.82f * u;
                var w = (float)Math.Round(CardWidget.CardW * k / 2) * 2; var h = (float)Math.Round(CardWidget.CardH * k / 2) * 2;
                var face = CardWidget.Face(Game, cards[selected].Card, bonus.For(cards[selected].Card));
                Glows.Draw(canvas, Art, x, y, 40 + 50 * k, Glows.Scaled(CardWidget.RarityColor(cards[selected].Card.Rarity), 0.55));
                canvas.BlitRect(face, new RectF(x - w / 2, y - h / 2, w, h), Rgba.White);
                if (rng.Random() < 0.7) fx.Emit(x + (float)rng.Uniform(-8, 8), y + (float)rng.Uniform(-8, 8), (float)rng.Uniform(-10, 10), (float)rng.Uniform(10, 30), (float)rng.Uniform(0.3, 0.6), RelicCards.Palette(cards[selected].Card.Rarity), (float)rng.Uniform(1.5, 2.5), ParticleStyle.Square, 1f);
            }
            else if (s.Kind == RewardStepKind.Relic && selected < lay.Relics.Count)
            {
                var from = lay.Relics[selected].Niche;
                float x0 = from.CenterX, y0 = from.CenterY - 6, x1 = RelicCounter.X + 22, y1 = RelicCounter.CenterY;
                var x = x0 + (x1 - x0) * u; var y = y0 + (y1 - y0) * u - 70 * (float)Math.Sin(Math.PI * u);
                var size = (int)Math.Round((RelicCards.IconSize - 62 * u) / 2) * 2;
                Glows.Draw(canvas, Art, x, y, 44, Glows.Scaled(CardWidget.RarityColor(relics[selected].Definition.Rarity), 0.6));
                RelicViewer.DrawRelicIcon(canvas, Art, relics[selected].Definition.Name, size, x, y);
            }
        }

        /// <summary>The coins of the gold step flying from its plate to the gold counter.</summary>
        private void DrawCoins(Canvas canvas, RewardLayout lay)
        {
            if (State != Phase.Intro || lay.Pills.Count == 0) return;
            var coin = Art.Icon("coin"); if (!coin.Valid) return;
            var p = lay.Pills[0];
            for (var k = 0; k < 6; k++)
            {
                var u = (t - GoldPopAt - k * CoinStagger) / CoinT; if (u <= 0f || u >= 1f) continue;
                var e = EaseOut(u);
                float x0 = p.X + 25, y0 = p.CenterY, x1 = GoldHudX, y1 = GoldHudY;
                var x = x0 + (x1 - x0) * e; var y = y0 + (y1 - y0) * e - (50 + k * 6) * (float)Math.Sin(Math.PI * e);
                canvas.BlitRect(coin, new RectF((float)Math.Floor(x / 2) * 2 - coin.W, (float)Math.Floor(y / 2) * 2 - coin.H, coin.W * 2, coin.H * 2), Rgba.White);
            }
        }

        private void DrawButtons(Canvas canvas, RewardLayout lay)
        {
            var s = CurrentStep;
            if (s == null || State == Phase.WaitingPack || State == Phase.Finished) return;
            var a = State == Phase.Intro ? 0f : State == Phase.Enter ? Smooth(t / 0.25f) : 1f;
            if (a <= 0.01f) return;
            canvas.PushAlpha(a);
            var on = State == Phase.Choosing; var n = ItemCount(s);
            var enabled = on && PrimaryEnabled;
            var pstate = !enabled ? ButtonState.Off : hoveredButton == "primary" ? ButtonState.Hover : ButtonState.Idle;
            if (pstate == ButtonState.Hover) PixelUi.Frame2(canvas, lay.Primary.Move(0, -1), new Rgba(255, 222, 140));
            PixelUi.Button(canvas, Art, lay.Primary, PrimaryLabel, "gold", pstate, PrimaryIcon, enabled ? "Enter" : null, time, 0f, 16);
            if (lay.HasSecondary)
            {
                var sstate = !on ? ButtonState.Off : hoveredButton == "secondary" ? ButtonState.Hover : ButtonState.Idle;
                if (sstate == ButtonState.Hover) PixelUi.Frame2(canvas, lay.Secondary.Move(0, -1), new Rgba(220, 200, 160));
                PixelUi.Button(canvas, Art, lay.Secondary, "Omitir", "bronze", sstate, null, "O", time, 0f, 16);
            }
            // what is picked, over the left of the buttons
            if (on && n > 0 && selected >= 0 && s.Kind != RewardStepKind.Pack && (n > 1 || s.Kind == RewardStepKind.Card))
            {
                var name = s.Kind == RewardStepKind.Card ? cards[selected].Card.Name : relics[selected].Definition.Name;
                var room = (int)lay.Primary.X - 40;
                PixelUi.InkText(canvas, Art, TextFit.Fit(Fonts.Get(13), "Elegida: " + name, room), 13, Anchor.MidRight, lay.Primary.X - 20, lay.Primary.CenterY - 1, Gilt);
            }
            // footer: what the keys do, or why a press was refused
            if (feedbackTime > 0)
            {
                canvas.PushAlpha(Math.Min(1f, feedbackTime / 0.3f));
                var w = PixelUi.InkTextWidth(Art, feedbackText, 16) + 48;
                var box = RectF.CenteredInt(640, FooterY, w, 40);
                PixelUi.Panel(canvas, Art, box);
                PixelUi.InkText(canvas, Art, feedbackText, 16, Anchor.Center, box.CenterX, box.CenterY - 1, new Rgba(255, 150, 130));
                canvas.PopAlpha();
            }
            else if (on) DrawHints(canvas, s, n);
            canvas.PopAlpha();
        }

        /// <summary>Key caps under the buttons: "1-3 Elegir · clic otra vez Añadir".</summary>
        private void DrawHints(Canvas canvas, RewardStep s, int n)
        {
            var parts = new List<(string key, string text)>();
            if (s.Kind == RewardStepKind.Pack) parts.Add(("Enter", "Abrir el sobre"));
            else if (n == 0) parts.Add(("Enter", "Continuar"));
            else if (selected < 0) { parts.Add(("1-" + Math.Min(9, n), "Elegir (o clic)")); parts.Add(("O", "Omitir")); }
            else
            {
                parts.Add(("Enter", s.Kind == RewardStepKind.Card ? "Añadir al mazo" : "Tomar"));
                if (n > 1) parts.Add(("1-" + Math.Min(9, n), "Cambiar"));
                parts.Add(("O", "Omitir"));
            }
            var widths = parts.Select(p => PixelUi.KeycapWidth(Art, p.key) + 4 + PixelUi.InkTextWidth(Art, p.text, 13)).ToList();
            var total = widths.Sum() + 26 * (parts.Count - 1);
            var x = (float)Math.Floor((640 - total / 2f) / 2) * 2;
            for (var i = 0; i < parts.Count; i++)
            {
                var cap = PixelUi.Keycap(canvas, Art, x + (PixelUi.KeycapWidth(Art, parts[i].key) - 6) / 2f, FooterY, parts[i].key);
                PixelUi.InkText(canvas, Art, parts[i].text, 13, Anchor.MidLeft, cap.Right + 6, FooterY - 1, Dim);
                x += widths[i] + 26;
            }
        }

        /// <summary>The hovered card's or relic's explanation beside it; a done step's card or relic when its plate is hovered.</summary>
        public TooltipContent HoverTooltip(out RectF? beside)
        {
            beside = null;
            var lay = Layout();
            if (hoveredPill >= 0 && hoveredPill < steps.Count && Game.Data != null)
            {
                var ps = steps[hoveredPill];
                if (ps.State == RewardStepState.Done && ps.Card != null) { beside = lay.Pills[hoveredPill]; var sh = bonus.For(ps.Card); return Tooltip.Card(Game, ps.Card, sh.BonusDamage, sh.BonusBlock); }
                if (ps.State == RewardStepState.Done && ps.Relic != null) { beside = lay.Pills[hoveredPill]; return RelicCards.OfferTooltip(Game, ps.Relic) ?? Tooltip.Relic(Game, ps.Relic.Definition, ps.Relic.Chroma); }
            }
            var s = CurrentStep;
            if (State != Phase.Choosing || s == null || hovered < 0) return null;
            if (s.Kind == RewardStepKind.Card && hovered < cards.Count && Game.Data != null)
            {
                beside = CardRect(hovered); var sh = bonus.For(cards[hovered].Card);
                return Tooltip.Card(Game, cards[hovered].Card, sh.BonusDamage, sh.BonusBlock);
            }
            if (s.Kind == RewardStepKind.Relic && hovered < relics.Count) { beside = lay.Relics[hovered].Rect; return RelicCards.OfferTooltip(Game, relics[hovered]); }
            return null;
        }

        private void DrawTooltips(Canvas canvas, RewardLayout lay)
        {
            var tip = HoverTooltip(out var beside);
            if (tip != null) Tooltip.Draw(canvas, Game, tip, mouse, beside);
        }
    }
}
