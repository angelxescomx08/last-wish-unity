using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>
    /// Event room (interface redesigned 2026-11-04). The dungeon room behind a violet focus; a title band with the
    /// event's name and a ribbon with its rarity and floor; on the left a plate with a code-drawn arch whose opening glows
    /// (<see cref="EventPortal"/>, until each event has its own art); on the right the story in an iron panel and one card per choice, whose costs,
    /// rewards and risk are pixel chips with icons. After a choice, a result panel shows what happened and what changed.
    /// All payments and outcomes belong to the domain session; this screen only reads it.
    /// </summary>
    public sealed class EventScene : Scene
    {
        // ------------------------------------------------------------------ layout
        public const int PlateX = 88, PlateW = 304, ColX = 416, ColW = 776, FooterY = 682, PlateMinH = 380;
        /// <summary>The band where the plate and the column are centred (under the title band, over the footer).</summary>
        public const int AreaTop = 108, AreaBottom = 648, StoryMinH = 84;
        public const int CardH = 88, CompactH = 52, Gap = 12, ChipH = 24, ChipGap = 8, BodyLine = 24, SmallLine = 22;
        private const int TitleMaxW = 720, ContinueW = 248, ContinueH = 48;
        private const float AppearT = 0.32f, StaggerT = 0.07f;

        private static readonly Rgba Ink = Colors.Ink, Cream = Colors.Cream, Dim = new Rgba(186, 176, 160), Faint = new Rgba(132, 124, 116),
            Gilt = new Rgba(255, 214, 120), Violet = new Rgba(170, 100, 255), Lilac = new Rgba(222, 200, 255),
            Gain = new Rgba(120, 226, 116), Loss = new Rgba(255, 112, 96), Risk = new Rgba(244, 188, 76), Story = new Rgba(190, 140, 255),
            Upgrade = new Rgba(108, 182, 255), Purge = new Rgba(255, 150, 70), Short = new Rgba(255, 140, 118);
        private static readonly string[] RarityNames = { "común", "poco común", "raro", "épico", "legendario" };
        private static readonly Regex DetailPattern = new Regex(@"(Mejorada|Eliminada|Recibiste): ([^.]+)\.", RegexOptions.CultureInvariant);

        /// <summary>A cost, reward, risk or story tag: icon (Art.Icon name, may be null), text and colour.</summary>
        public struct Chip
        {
            public string Icon, Text; public Rgba Color;
            /// <summary>What hovering the chip shows: a card (and its upgrade) or a relic.</summary>
            public CardDefinition Card, After; public RelicDefinition Relic;
            public Chip(string icon, string text, Rgba color) { Icon = icon; Text = text; Color = color; Card = null; After = null; Relic = null; }
            public bool HasPreview => Card != null || Relic != null;
        }

        /// <summary>A text piece in a chip row: a chip or a plain label ("Si falla:").</summary>
        public struct Piece
        {
            public Chip Chip; public bool Label; public RectF Rect;
        }

        /// <summary>One choice card: where it is, what it says and what it costs, gives and risks.</summary>
        public sealed class ChoiceView
        {
            public int Index; public RectF Rect; public bool Compact, Blocked; public string Label, Reason = "";
            public Chip? Cost; public List<Piece> Pieces = new List<Piece>(); public List<string> Note = new List<string>();
            public RectF LabelRect, CostRect, ReasonRect; public float NoteY;
        }

        /// <summary>What the screen shows for the current state of the session (tests: every box fits).</summary>
        public sealed class EventLayout
        {
            public string Title; public int TitleSize; public RectF TitleRect; public string Ribbon;
            public RectF Plate, Story; public List<string> Previous = new List<string>(), Body = new List<string>();
            public List<ChoiceView> Choices = new List<ChoiceView>();
            public bool Result; public RectF ResultPanel; public string ResultTitle = ""; public Chip? Outcome;
            public List<string> ResultLines = new List<string>(); public List<Piece> ResultPieces = new List<Piece>(); public RectF Continue;
            public float ResultHeaderH, ResultTextY;
            public float ContentBottom;
        }

        private readonly RunState run;
        private readonly DungeonBackdrop backdrop;
        private readonly BurstParticles fx;
        private readonly Rng rng;
        private int selected, hovered = -1, shakeIndex = -1;
        private float time, appear, shake, moteClock, feedbackT;
        private string feedback = "";
        private string previous = "";
        private (int gold, int hp, int maxHp, int luck) before;
        private List<CardDefinition> deckBefore = new List<CardDefinition>();
        private (float x, float y) mouse = (-1, -1);
        private EventLayout layout; private string layoutKey;
        public EventSession Session { get; }
        public bool Cleared { get; set; }

        public EventScene(GameContext game, RunState run, string roomId) : base(game)
        {
            this.run = run; Session = run.OpenEvent(roomId);
            var seed = (int)(Hash(Session.Definition.Id) & 0x7fffffff);
            backdrop = new DungeonBackdrop(Art, seed % 997 + 5, 0.6f);
            fx = new BurstParticles(400, seed);
            rng = new Rng(seed);
            before = Snapshot(); deckBefore = run.Deck.ToList();
            selected = FirstOpen();
        }

        /// <summary>The first choice the hero can take (the focus starts there; leaving is always possible).</summary>
        private int FirstOpen()
        {
            if (Session.Completed) return 0;
            for (var i = 0; i < Session.Stage.Choices.Count; i++) if (Session.BlockReason(i).Length == 0) return i;
            return 0;
        }

        private static uint Hash(string s) { var h = 2166136261u; foreach (var ch in s) { h ^= ch; h *= 16777619u; } return h; }
        private (int, int, int, int) Snapshot() => (run.Gold, run.Hp, run.MaxHp, run.Luck);
        private static float Clamp01(float t) => t < 0 ? 0 : t > 1 ? 1 : t;
        private static float Smooth(float t) { t = Clamp01(t); return t * t * (3 - 2 * t); }

        // ------------------------------------------------------------------ input
        public override void HandleEvent(in InputEvent e)
        {
            if (e.Type == EventType.MouseMove) mouse = (e.X, e.Y);
            if (Cleared || e.Repeat) return;
            var lay = Layout();
            if (Session.Completed)
            {
                if (e.Type == EventType.MouseMove) { var over = lay.Continue.Contains(e.X, e.Y) ? 0 : -1; if (over != hovered && over >= 0) Sound.PlayNav(); hovered = over; }
                if (e.IsConfirmKey || e.IsLeftDown && lay.Continue.Contains(e.X, e.Y)) { Sound.PlayConfirm(); Cleared = true; }
                return;
            }
            var count = Session.Stage.Choices.Count;
            if (e.Type == EventType.MouseMove)
            {
                var over = -1;
                foreach (var c in lay.Choices) if (c.Rect.Contains(e.X, e.Y)) over = c.Index;
                if (over >= 0 && over != hovered) { Sound.PlayNav(); selected = over; }
                hovered = over;
                return;
            }
            if (e.IsKey(Key.Down) || e.IsKey(Key.Tab)) { selected = (selected + 1) % count; Sound.PlayNav(); }
            else if (e.IsKey(Key.Up)) { selected = (selected + count - 1) % count; Sound.PlayNav(); }
            else if (e.Type == EventType.KeyDown && e.Digit >= 1 && e.Digit <= count) Choose(e.Digit - 1);
            else if (e.IsConfirmKey) Choose(selected);
            else if (e.IsLeftDown)
                foreach (var c in lay.Choices) if (c.Rect.Contains(e.X, e.Y)) { Choose(c.Index); break; }
        }

        private void Choose(int index)
        {
            if (index < 0 || index >= Session.Stage.Choices.Count) return;
            var blocked = Session.BlockReason(index);
            if (blocked.Length > 0)
            {
                feedback = ReasonText(Session.Stage.Choices[index], blocked); feedbackT = 2.2f; shakeIndex = index; shake = 1f; selected = index;
                Sound.PlayError(); return;
            }
            var card = Layout().Choices.FirstOrDefault(c => c.Index == index)?.Rect ?? new RectF(ColX, AreaTop, ColW, CardH);
            before = Snapshot(); deckBefore = run.Deck.ToList();
            if (!Session.Choose(index)) return;
            Sound.PlayReward();
            fx.Burst(card.X + 30, card.Y + 22, 18, new[] { Rgba.White, Lilac, Violet }, (60f, 220f), null, (0.3f, 0.6f), (1.5f, 3f), ParticleStyle.Spark, 3f);
            previous = Session.Completed ? "" : Session.Message;
            selected = FirstOpen(); hovered = -1; appear = 0; layoutKey = null;
            if (Session.Completed) Celebrate();
        }

        /// <summary>Sparks on the result panel in the colours of what changed.</summary>
        private void Celebrate()
        {
            var lay = Layout(); var p = lay.ResultPanel;
            var palette = new List<Rgba> { Rgba.White };
            if (run.Gold > before.gold) palette.Add(Gilt);
            if (run.Hp > before.hp || run.MaxHp > before.maxHp || run.Luck > before.luck) palette.Add(Gain);
            if (run.Hp < before.hp) palette.Add(Loss);
            if (palette.Count == 1) palette.Add(Lilac);
            fx.Burst(p.CenterX, p.Y + 30, 34, palette.ToArray(), (90f, 320f), ((float)Math.PI * 1.05f, (float)Math.PI * 1.95f), (0.4f, 0.9f), (2f, 3.5f), ParticleStyle.Square, 2.2f, 220f);
        }

        // ------------------------------------------------------------------ simulation
        public override void Update(float dt)
        {
            dt = Math.Min(0.1f, Math.Max(0f, dt));
            time += dt; appear += dt;
            shake = Math.Max(0f, shake - dt * 3f); feedbackT = Math.Max(0f, feedbackT - dt);
            backdrop.Update(dt);
            moteClock -= dt;
            if (moteClock <= 0)
            {
                moteClock = (float)rng.Uniform(0.07, 0.16);
                var (cx, cy) = EmblemCenter(Layout());
                var x = cx + (float)rng.Uniform(-40, 40); var y = cy + (float)rng.Uniform(-8, 70);
                fx.Emit(x, y, (float)rng.Uniform(-6, 6), (float)rng.Uniform(-42, -18), (float)rng.Uniform(0.9, 1.7), new[] { Lilac, Violet, Glows.Scaled(Violet, 0.6) }, (float)rng.Uniform(1.5, 2.5), ParticleStyle.Square, 0.3f);
            }
            fx.Update(dt);
        }

        // ------------------------------------------------------------------ texts and chips
        private string RarityText => "Suceso " + RarityNames[Math.Max(1, Math.Min(5, Session.Definition.Rarity)) - 1];

        private CardDefinition FirstUpgradable => run.Deck.FirstOrDefault(RunState.CanUpgrade);

        private static RelicDefinition RelicDef(string id) => RunContent.RelicById(id) ?? RunContent.RelicByTag(id);
        private static string RelicName(string id) => RelicDef(id)?.Name ?? "una reliquia";

        /// <summary>What an effect gives or takes, as chips (gold, life, max life, luck, cards, relics).</summary>
        public List<Chip> EffectChips(EventEffect e)
        {
            var list = new List<Chip>();
            if (e == null) return list;
            if (e.Gold > 0) list.Add(new Chip("coin", "+" + e.Gold + " oro", Gain));
            if (e.Heal > 0) list.Add(new Chip("heal", "+" + e.Heal + " vida", Gain));
            if (e.MaxHp > 0) list.Add(new Chip("heal", "+" + e.MaxHp + " vida máx.", Gain));
            if (e.Luck > 0) list.Add(new Chip("clover", "+" + e.Luck + " suerte", Gain));
            if (e.Damage > 0) list.Add(new Chip("fury", "hasta −" + e.Damage + " vida", Loss));
            if (e.Upgrade)
            {
                var card = FirstUpgradable;
                list.Add(new Chip("buff", "Mejora " + (card?.Name ?? "una carta"), Upgrade) { Card = card, After = card?.Upgrade() });
            }
            if (e.Remove)
            {
                var card = run.Deck.Count > 0 ? run.Deck[0] : null;
                list.Add(new Chip("exhaust", "Pierdes " + (card?.Name ?? "una carta"), Purge) { Card = card });
            }
            if (e.Card) list.Add(new Chip("draw", "Carta nueva", Gain));
            if (e.Relic.Length > 0) list.Add(new Chip("bag", "Reliquia: " + RelicName(e.Relic), Gilt) { Relic = RelicDef(e.Relic) });
            return list;
        }

        /// <summary>The price of a choice (at most one per choice in the catalogue).</summary>
        public static Chip? CostChip(EventChoice c)
        {
            if (c.GoldCost > 0) return new Chip("coin", "−" + c.GoldCost + " oro", Loss);
            if (c.HpCost > 0) return new Chip("fury", "−" + c.HpCost + " vida", Loss);
            if (c.MaxHpCost > 0) return new Chip("fury", "−" + c.MaxHpCost + " vida máx.", Loss);
            return null;
        }

        private static Chip? StoryChip(EventChoice c)
        {
            if (c.NextStage >= 0) return new Chip("combo", "Continúa", Story);
            if (c.NextEventId.Length > 0 || c.SetFlag.Length > 0) return new Chip("spoil", "Sigue la historia", Story);
            return null;
        }

        private string ReasonText(EventChoice c, string reason)
        {
            if (c.GoldCost > run.Gold) return "Te faltan " + (c.GoldCost - run.Gold) + " de oro.";
            return reason;
        }

        // ------------------------------------------------------------------ layout
        private RectF Text(Canvas canvas, string text, int size, Anchor anchor, float x, float y, Rgba color, bool shadow = false, bool bold = false)
        {
            if (string.IsNullOrEmpty(text)) return new RectF(x, y, 0, 0);
            var font = bold ? Fonts.Bold(size) : Fonts.Get(size); var k = font.Scale;
            var r = Txt.Place(font.Width(text) + 2 * k, font.Height + 2 * k, anchor, x, y);
            float ox = (float)Math.Floor(r.X) + k, oy = (float)Math.Floor(r.Y) + k;
            if (shadow) foreach (var dx in new[] { -1, 0, 1 }) font.Draw(canvas, text, ox + dx * k, oy + 2 * k, Ink);
            foreach (var (dx, dy) in Ring) font.Draw(canvas, text, ox + dx * k, oy + dy * k, Ink);
            font.Draw(canvas, text, ox, oy, color);
            return r;
        }

        private static readonly (int dx, int dy)[] Ring = { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) };

        public int TextWidth(string text, int size, bool bold = false) { var f = bold ? Fonts.Bold(size) : Fonts.Get(size); return f.Width(text) + 2 * f.Scale; }
        private int TextHeight(int size, bool bold = false) { var f = bold ? Fonts.Bold(size) : Fonts.Get(size); return f.Height + 2 * f.Scale; }
        public int ChipWidth(Chip c) => TextWidth(c.Text, 13) + 16 + (c.Icon != null && Art.Icon(c.Icon).Valid ? Art.Icon(c.Icon).W + 4 : 0);

        /// <summary>Flow chips (and labels) left to right in rows of <paramref name="width"/>; returns the rows used.</summary>
        private int Flow(List<Piece> pieces, float x, float y, float width)
        {
            if (pieces.Count == 0) return 0;
            float cx = x; var rows = 1;
            for (var i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                var w = p.Label ? TextWidth(p.Chip.Text, 13) : ChipWidth(p.Chip);
                if (cx > x && cx + w > x + width) { cx = x; y += ChipH + 6; rows++; }
                p.Rect = new RectF(cx, y, Math.Min(w, width), ChipH);
                pieces[i] = p;
                cx += w + ChipGap;
            }
            return rows;
        }

        /// <summary>The layout of the current state (cached until the stage, the outcome or the hero's numbers change).</summary>
        public EventLayout Layout()
        {
            var key = Session.StageIndex + "|" + Session.Completed + "|" + run.Gold + "|" + run.Hp + "|" + run.MaxHp + "|" + run.Deck.Count + "|" + run.OwnedRelics.Count;
            if (layout != null && key == layoutKey) return layout;
            layoutKey = key;
            var def = Session.Definition; var lay = new EventLayout();
            // title band
            lay.TitleSize = TextWidth(def.Name, 30, true) <= TitleMaxW ? 30 : 20;
            lay.Title = TextFit.Fit(Fonts.Bold(lay.TitleSize), def.Name, TitleMaxW);
            lay.TitleRect = Txt.Place(TextWidth(lay.Title, lay.TitleSize, true), TextHeight(lay.TitleSize, true), Anchor.Center, 640, 38);
            lay.Ribbon = RarityText + "  ·  Piso " + Session.Floor;
            Place(lay, AreaTop);
            var height = Math.Max(PlateMinH, lay.ContentBottom - AreaTop);
            var top = Math.Max(AreaTop, (int)Math.Floor((AreaTop + AreaBottom - height) / 4f) * 2);
            if (top != AreaTop) { var keepTitle = (lay.Title, lay.TitleSize, lay.TitleRect, lay.Ribbon); lay = new EventLayout(); (lay.Title, lay.TitleSize, lay.TitleRect, lay.Ribbon) = keepTitle; Place(lay, top); }
            lay.Plate = new RectF(PlateX, top, PlateW, Math.Max(PlateMinH, lay.ContentBottom - top));
            // a column shorter than the plate sits in the middle of it
            var slack = (int)Math.Floor((lay.Plate.Bottom - lay.ContentBottom) / 4f) * 2;
            if (slack > 0) Shift(lay, slack);
            return layout = lay;
        }

        private static void Shift(EventLayout lay, float dy)
        {
            static List<Piece> Moved(List<Piece> list, float d) => list.Select(p => { var q = p; q.Rect = p.Rect.Move(0, d); return q; }).ToList();
            lay.Story = lay.Story.Move(0, dy);
            foreach (var c in lay.Choices)
            {
                c.Rect = c.Rect.Move(0, dy); c.LabelRect = c.LabelRect.Move(0, dy); c.CostRect = c.CostRect.Move(0, dy); c.ReasonRect = c.ReasonRect.Move(0, dy);
                c.Pieces = Moved(c.Pieces, dy); c.NoteY += dy;
            }
            lay.ResultPanel = lay.ResultPanel.Move(0, dy); lay.ResultTextY += dy; lay.ResultPieces = Moved(lay.ResultPieces, dy);
            lay.Continue = lay.Continue.Move(0, dy); lay.ContentBottom += dy;
        }

        /// <summary>Lay the column out from <paramref name="top"/> (story and choices, or the result).</summary>
        private void Place(EventLayout lay, float top)
        {
            var inner = ColW - 48;
            float y = top;
            if (!Session.Completed)
            {
                // story panel: what the previous step settled (multi-stage events), a rule, then the story
                if (previous.Length > 0) lay.Previous = TextFit.Wrap(Fonts.Get(13), previous, inner - 28);
                lay.Body = TextFit.Wrap(Fonts.Get(16), Session.Stage.Body, inner);
                var h = 22 + lay.Body.Count * BodyLine + 22;
                if (lay.Previous.Count > 0) h += lay.Previous.Count * SmallLine + 16;
                lay.Story = new RectF(ColX, y, ColW, Math.Max(StoryMinH, h));
                y = lay.Story.Bottom + 16;
                for (var i = 0; i < Session.Stage.Choices.Count; i++)
                {
                    var view = BuildChoice(i, y);
                    lay.Choices.Add(view);
                    y = view.Rect.Bottom + Gap;
                }
                lay.ContentBottom = y - Gap;
            }
            else
            {
                BuildResult(lay, top);
                lay.ContentBottom = lay.Continue.Bottom;
            }
        }

        private ChoiceView BuildChoice(int i, float y)
        {
            var c = Session.Stage.Choices[i]; var reason = Session.BlockReason(i);
            var v = new ChoiceView { Index = i, Label = c.Label, Blocked = reason.Length > 0, Reason = reason.Length > 0 ? ReasonText(c, reason) : "", Cost = CostChip(c) };
            var gains = EffectChips(c.Success); var story = StoryChip(c);
            if (story.HasValue) gains.Add(story.Value);
            v.Compact = c.IsExit && gains.Count == 0 && !v.Cost.HasValue && c.SuccessPercent == 100;
            var x = ColX + 52; var width = ColW - 52 - 24;
            v.LabelRect = Txt.Place(TextWidth(c.Label, 16, true), TextHeight(16, true), Anchor.MidLeft, x, y + (v.Compact ? CompactH / 2 : 24));
            if (v.Cost.HasValue)
            {
                var cw = ChipWidth(v.Cost.Value);
                v.CostRect = new RectF(ColX + ColW - 20 - cw, y + 12, cw, ChipH);
            }
            if (v.Compact) { v.Rect = new RectF(ColX, y, ColW, CompactH); return v; }
            if (c.SuccessPercent < 100) v.Pieces.Add(new Piece { Chip = new Chip("unknown", c.SuccessPercent + " % de éxito", Risk) });
            foreach (var g in gains) v.Pieces.Add(new Piece { Chip = g });
            if (c.SuccessPercent < 100)
            {
                var fail = EffectChips(c.Failure);
                v.Pieces.Add(new Piece { Label = true, Chip = new Chip(null, fail.Count == 0 ? "Si falla: nada." : "Si falla:", Dim) });
                foreach (var f in fail) v.Pieces.Add(new Piece { Chip = f });
            }
            var rowY = y + 48;
            var rows = Flow(v.Pieces, x, rowY, width);
            float bottom;
            if (rows == 0)
            {
                // nothing countable happens: the story says what it does
                v.Note = TextFit.Wrap(Fonts.Get(13), c.Success.Text, width); v.NoteY = rowY;
                bottom = rowY + v.Note.Count * SmallLine;
            }
            else bottom = rowY + rows * (ChipH + 6) - 6;
            if (v.Blocked)
            {
                v.ReasonRect = new RectF(x, bottom + 8, Math.Min(width, TextWidth(v.Reason, 13) + 22), 20);
                bottom = v.ReasonRect.Bottom;
            }
            v.Rect = new RectF(ColX, y, ColW, Math.Max(CardH, bottom + 16 - y));
            return v;
        }

        private void BuildResult(EventLayout lay, float top)
        {
            var choice = Session.LastChoice; var effect = Session.LastEffect;
            var inner = ColW - 48;
            lay.Result = true;
            lay.ResultTitle = TextFit.Fit(Fonts.Bold(20), choice?.Label ?? Session.Definition.Name, inner);
            if (choice != null && choice.SuccessPercent < 100)
                lay.Outcome = Session.LastSucceeded ? new Chip("buff", "¡Éxito!", Gain) : new Chip("lethal", "Ha fallado", Loss);
            lay.ResultLines = TextFit.Wrap(Fonts.Get(16), effect?.Text ?? Session.Message, inner);
            // what changed: the hero's numbers, then the cards and relics the session names
            var pieces = new List<Piece>();
            void Add(string icon, string text, Rgba color, CardDefinition card = null, RelicDefinition relic = null)
                => pieces.Add(new Piece { Chip = new Chip(icon, text, color) { Card = card, Relic = relic } });
            string Signed(int d) => d > 0 ? "+" + d : "−" + -d;
            var dg = run.Gold - before.gold; var dh = run.Hp - before.hp; var dm = run.MaxHp - before.maxHp; var dl = run.Luck - before.luck;
            if (dg != 0) Add("coin", Signed(dg) + " oro", dg > 0 ? Gain : Loss);
            if (dm != 0) Add("heal", Signed(dm) + " vida máx.", dm > 0 ? Gain : Loss);
            if (dh != 0) Add(dh > 0 ? "heal" : "fury", Signed(dh) + " vida", dh > 0 ? Gain : Loss);
            if (dl != 0) Add("clover", Signed(dl) + " suerte", dl > 0 ? Gain : Loss);
            foreach (Match m in DetailPattern.Matches(Session.Message))
            {
                var name = m.Groups[2].Value;
                if (m.Groups[1].Value == "Mejorada") Add("buff", "Mejorada: " + name, Upgrade, run.Deck.LastOrDefault(c => c.Name == name));
                else if (m.Groups[1].Value == "Eliminada") Add("exhaust", "Eliminada: " + name, Purge, deckBefore.FirstOrDefault(c => c.Name == name));
                else if (effect != null && effect.Relic.Length > 0 && name == RelicName(effect.Relic)) Add("bag", "Reliquia: " + name, Gilt, null, RelicDef(effect.Relic));
                else Add("draw", "Nueva carta: " + name, Gain, run.Deck.LastOrDefault(c => c.Name == name));
            }
            if (choice != null && Session.LastSucceeded && (choice.NextEventId.Length > 0 || choice.SetFlag.Length > 0))
                Add("spoil", "La historia continuará", Story);
            var titleH = TextHeight(20, true);
            var headerH = 12 + titleH + (lay.Outcome.HasValue ? ChipH + 8 : 0) + 8;
            lay.ResultHeaderH = headerH;
            var y = top + 8 + headerH + 18;
            lay.ResultTextY = y;
            y += lay.ResultLines.Count * BodyLine + 12;
            var rows = Flow(pieces, ColX + 24, y, inner);
            if (rows == 0)
            {
                pieces.Add(new Piece { Label = true, Chip = new Chip(null, "Nada más cambia.", Dim) });
                rows = Flow(pieces, ColX + 24, y, inner);
            }
            lay.ResultPieces = pieces;
            y += rows * (ChipH + 6) - 6 + 22;
            lay.ResultPanel = new RectF(ColX, top, ColW, y - top);
            lay.Continue = new RectF(ColX + ColW / 2 - ContinueW / 2, lay.ResultPanel.Bottom + 18, ContinueW, ContinueH);
        }

        /// <summary>The middle of the arch: the arch, the rarity tag and the chapter lines are centred in the plate.</summary>
        private (float x, float y) EmblemCenter(EventLayout lay)
        {
            var def = Session.Definition;
            var notes = (def.Stages.Count > 1 ? 1 : 0) + (def.RequiredFlag.Length > 0 ? 1 : 0);
            var block = EventPortal.H * 2 + 14 + ChipH + (notes > 0 ? 14 + notes * SmallLine : 0);
            return (lay.Plate.CenterX, (float)Math.Floor((lay.Plate.CenterY - block / 2f) / 2) * 2 + EventPortal.H);
        }

        private (Sprite paint, Sprite light, Sprite rune) PortalArt(int rarity)
        {
            var gem = CardWidget.RarityColor(rarity);
            Rgba[] built = null, glow = null;
            void Build() { if (built == null) (built, glow) = EventPortal.Paint(gem); }
            var paint = new Sprite(Art.Procedural("event_portal_" + rarity, EventPortal.W, EventPortal.H, () => { Build(); return built; }));
            var light = new Sprite(Art.Procedural("event_portal_light_" + rarity, EventPortal.W, EventPortal.H, () => { Build(); return glow; }));
            var rune = new Sprite(Art.Procedural("event_rune", EventPortal.RuneW, EventPortal.RuneH, EventPortal.RuneSprite));
            return (paint, light, rune);
        }

        /// <summary>The card of choice <paramref name="index"/> (tests click it).</summary>
        public RectF ChoiceRect(int index) => Layout().Choices.First(c => c.Index == index).Rect;
        public RectF ContinueRect => Layout().Continue;

        // ------------------------------------------------------------------ drawing
        public override void Draw(Canvas canvas)
        {
            var lay = Layout();
            backdrop.Draw(canvas);
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(8, 4, 14, 150));
            canvas.BlitRect(Vignette(), new RectF(0, 0, Canvas.Width, Canvas.Height), Rgba.White);
            DrawBand(canvas, lay);
            DrawPlate(canvas, lay);
            if (lay.Result) DrawResult(canvas, lay);
            else { DrawStory(canvas, lay); foreach (var c in lay.Choices) DrawChoice(canvas, c); }
            fx.Draw(canvas, Art);
            DrawFooter(canvas, lay);
            DrawFeedback(canvas);
            DrawPreview(canvas, lay);
        }

        /// <summary>The chip under the mouse that has something to show (a card or a relic), if any.</summary>
        public Piece? HoveredChip(EventLayout lay)
        {
            IEnumerable<Piece> pieces = lay.Result ? lay.ResultPieces : lay.Choices.SelectMany(c => c.Pieces);
            foreach (var p in pieces) if (!p.Label && p.Chip.HasPreview && p.Rect.Contains(mouse)) return p;
            return null;
        }

        /// <summary>Hovering a card chip shows the card (and its upgrade); a relic chip shows the relic's tooltip.</summary>
        private void DrawPreview(Canvas canvas, EventLayout lay)
        {
            var hit = HoveredChip(lay); if (!hit.HasValue) return;
            var chip = hit.Value.Chip; var at = hit.Value.Rect;
            if (chip.Relic != null)
            {
                if (Game.Data == null) return;
                // an offered relic is not "active" yet: drop the state line of the owned-relic tooltip
                var content = Tooltip.Relic(Game, chip.Relic, ChromaKind.None);
                var state = content.Lines.FindIndex(l => l.StartsWith("Estado:", StringComparison.Ordinal));
                if (state >= 0) { content.Lines.RemoveAt(state); if (state > 0 && content.Lines[state - 1].Length == 0) content.Lines.RemoveAt(state - 1); }
                Tooltip.Draw(canvas, Game, content, mouse, at);
                return;
            }
            var box = PreviewBox(chip, at);
            PixelUi.Panel(canvas, Art, box);
            var bonus = CardBonus.ForRun(run);
            var cy = box.Y + 16;
            if (chip.After != null)
            {
                CardWidget.Draw(canvas, Game, chip.Card, box.X + 16, cy, bonus.For(chip.Card));
                Text(canvas, "→", 20, Anchor.Center, box.X + 16 + CardWidget.CardW + 20, cy + CardWidget.CardH / 2f, Upgrade, true);
                CardWidget.Draw(canvas, Game, chip.After, box.X + 16 + CardWidget.CardW + 40, cy, bonus.For(chip.After));
            }
            else CardWidget.Draw(canvas, Game, chip.Card, box.X + 16, cy, bonus.For(chip.Card));
        }

        /// <summary>Where a card preview goes: under the chip when it fits, else over it; always on screen.</summary>
        public static RectF PreviewBox(Chip chip, RectF at)
        {
            var w = 32 + CardWidget.CardW + (chip.After != null ? CardWidget.CardW + 40 : 0); var h = 32 + CardWidget.CardH;
            var x = Math.Max(8, Math.Min(Canvas.Width - 8 - w, (float)Math.Floor(at.CenterX - w / 2f)));
            var below = at.Bottom + 8; var above = at.Y - 8 - h;
            var y = at.CenterY < Canvas.Height / 2f ? (below + h <= Canvas.Height - 8 ? below : above) : (above >= 8 ? above : below);
            return new RectF(x, Math.Max(8, y), w, h);
        }

        private Sprite Vignette() => new Sprite(Art.Procedural("event_vignette", 160, 90, () =>
        {
            var px = new Rgba[160 * 90]; const double cx = 80, cy = 50;
            for (var y = 0; y < 90; y++)
                for (var x = 0; x < 160; x++)
                {
                    var d = Math.Sqrt(Math.Pow((x - cx) / 92.0, 2) + Math.Pow((y - cy) / 60.0, 2));
                    px[y * 160 + x] = new Rgba(4, 2, 10, (int)(230 * Math.Pow(MathX.Clamp01((d - 0.5) / 0.6), 1.4)));
                }
            return px;
        }, smooth: true));

        private Sprite Band() => new Sprite(Art.Procedural("event_band", 1, 96, () =>
        {
            var px = new Rgba[96];
            for (var y = 0; y < 96; y++) px[y] = new Rgba(6, 4, 12, (int)(220 * Math.Pow(1 - y / 96.0, 1.4)));
            return px;
        }));

        private void DrawBand(Canvas canvas, EventLayout lay)
        {
            canvas.BlitRect(Band(), new RectF(0, 0, Canvas.Width, 96), Rgba.White);
            Glows.Draw(canvas, Art, 640, 40, 150, Glows.Scaled(new Rgba(60, 28, 96), 1.0));
            Text(canvas, lay.Title, lay.TitleSize, Anchor.Center, 640, 38, Lilac, true, true);
            PixelUi.Ribbon(canvas, Art, 640, 80, lay.Ribbon, 13);
        }

        /// <summary>A box with notched 2 px corners, a 2 px edge and an optional light top row.</summary>
        private static void PixelBox(Canvas canvas, RectF r, Rgba fill, Rgba edge, Rgba? light = null)
        {
            r = new RectF((float)Math.Floor(r.X), (float)Math.Floor(r.Y), (float)Math.Floor(r.W), (float)Math.Floor(r.H));
            canvas.Fill(new RectF(r.X + 2, r.Y, r.W - 4, 2), edge); canvas.Fill(new RectF(r.X + 2, r.Bottom - 2, r.W - 4, 2), edge);
            canvas.Fill(new RectF(r.X, r.Y + 2, 2, r.H - 4), edge); canvas.Fill(new RectF(r.Right - 2, r.Y + 2, 2, r.H - 4), edge);
            canvas.Fill(new RectF(r.X + 2, r.Y + 2, r.W - 4, r.H - 4), fill);
            if (light.HasValue) canvas.Fill(new RectF(r.X + 2, r.Y + 2, r.W - 4, 2), light.Value);
        }

        /// <summary>A 2 px frame just outside <paramref name="r"/> with notched corners (hover, selection).</summary>
        private static void Halo2(Canvas canvas, RectF r, Rgba color)
        {
            r = r.Inflate(8, 8);
            canvas.Fill(new RectF(r.X + 4, r.Y, r.W - 8, 2), color); canvas.Fill(new RectF(r.X + 4, r.Bottom - 2, r.W - 8, 2), color);
            canvas.Fill(new RectF(r.X, r.Y + 4, 2, r.H - 8), color); canvas.Fill(new RectF(r.Right - 2, r.Y + 4, 2, r.H - 8), color);
            canvas.Fill(new RectF(r.X + 2, r.Y + 2, 2, 2), color); canvas.Fill(new RectF(r.Right - 4, r.Y + 2, 2, 2), color);
            canvas.Fill(new RectF(r.X + 2, r.Bottom - 4, 2, 2), color); canvas.Fill(new RectF(r.Right - 4, r.Bottom - 4, 2, 2), color);
        }

        /// <summary>A dithered rule two art pixels high (every other 2 px block lit).</summary>
        private static void DitherRule(Canvas canvas, float x, float y, float w, Rgba color)
        {
            for (var px = (int)x; px < x + w - 1; px += 4) canvas.Fill(new RectF(px, (float)Math.Floor(y / 2) * 2, 2, 2), color);
        }

        private void DrawChip(Canvas canvas, Piece p, float alpha = 1f)
        {
            if (p.Label) { Text(canvas, p.Chip.Text, 13, Anchor.MidLeft, p.Rect.X, p.Rect.CenterY - 1, p.Chip.Color); return; }
            var c = p.Chip; var r = p.Rect;
            PixelBox(canvas, r, Glows.Scaled(c.Color, 0.2).WithAlpha(235), Glows.Scaled(c.Color, 0.8), Glows.Scaled(c.Color, 0.34).WithAlpha(235));
            var x = r.X + 8;
            var icon = c.Icon != null ? Art.Icon(c.Icon) : default;
            if (icon.Valid) { canvas.BlitRect(icon, new RectF(x, (float)Math.Floor(r.CenterY - icon.H / 2f), icon.W, icon.H), Rgba.White.WithAlpha((int)(255 * alpha))); x += icon.W + 4; }
            Text(canvas, c.Text, 13, Anchor.MidLeft, x, r.CenterY - 1, Glows.Scaled(c.Color, 1.3));
        }

        private void DrawPlate(Canvas canvas, EventLayout lay)
        {
            var plate = lay.Plate; var def = Session.Definition; var rarity = Math.Max(1, Math.Min(5, def.Rarity)); var rc = CardWidget.RarityColor(rarity);
            PixelUi.Panel(canvas, Art, plate);
            var inner = plate.Inflate(-24, -24);
            canvas.Fill(inner, new Rgba(10, 6, 18, 200));
            var (cx, cy) = EmblemCenter(lay);
            var pulse = 0.5 + 0.5 * Math.Sin(time * 2.1);
            // the arch in the crypt wall: light behind it on the plate, the stone, the glow of its opening, the rune
            Glows.Draw(canvas, Art, cx, cy + 8, 170, Glows.Scaled(new Rgba(56, 26, 98), 0.8 + 0.2 * pulse));
            for (var row = 0; row < 3; row++)
            {
                var w = 228 - row * 36; var yy = cy + EventPortal.H - 4 + row * 2;
                canvas.Fill(new RectF((float)Math.Floor((cx - w / 2f) / 2) * 2, yy, w, 2), new Rgba(4, 2, 8, 120 + row * 40));
            }
            var art = PortalArt(rarity);
            var dst = new RectF(cx - EventPortal.W, cy - EventPortal.H, EventPortal.W * 2, EventPortal.H * 2);
            canvas.BlitRect(art.paint, dst, Rgba.White);
            canvas.BlitRect(art.light, dst, Glows.Scaled(Rgba.White, 0.75 + 0.35 * pulse), Blend.Add);
            Glows.Draw(canvas, Art, cx, cy + (EventPortal.GlowY - EventPortal.H / 2) * 2, 54, Glows.Scaled(new Rgba(120, 70, 200), 0.35 + 0.2 * pulse));
            var bob = (float)Math.Round(Math.Sin(time * 1.7) * 1.4) * 2;
            var runeY = cy + (EventPortal.GlowY - 8 - EventPortal.H / 2) * 2;
            var rune = new RectF(cx - EventPortal.RuneW, runeY - EventPortal.RuneH + bob, EventPortal.RuneW * 2, EventPortal.RuneH * 2);
            canvas.BlitRect(art.rune, rune, Rgba.White);
            canvas.BlitRect(art.rune, rune, Glows.Scaled(new Rgba(150, 100, 230), 0.25 + 0.35 * pulse), Blend.Add);
            Twinkles(canvas, cx, cy);
            // rarity and chapter
            var y = cy + EventPortal.H + 14;
            var chip = new Piece { Chip = new Chip(null, char.ToUpper(RarityText[0]) + RarityText.Substring(1), rc) };
            var cw = ChipWidth(chip.Chip) + 14;
            chip.Rect = new RectF((float)Math.Floor(cx - cw / 2f), y, cw, ChipH);
            PixelBox(canvas, chip.Rect, Glows.Scaled(rc, 0.2).WithAlpha(235), Glows.Scaled(rc, 0.85), Glows.Scaled(rc, 0.34).WithAlpha(235));
            Gem(canvas, chip.Rect.X + 12, chip.Rect.CenterY, rc);
            Text(canvas, chip.Chip.Text, 13, Anchor.MidLeft, chip.Rect.X + 20, chip.Rect.CenterY - 1, Glows.Scaled(rc, 1.3));
            y += ChipH + 14;
            var notes = new List<string>();
            if (def.Stages.Count > 1) notes.Add("Parte " + (Session.StageIndex + 1) + " de " + def.Stages.Count);
            if (def.RequiredFlag.Length > 0) notes.Add("Continúa una historia");
            var scroll = Art.Icon("spoil");
            foreach (var n in notes)
            {
                var w = TextWidth(n, 13) + (scroll.Valid ? scroll.W + 6 : 0); var x = (float)Math.Floor(cx - w / 2f);
                if (scroll.Valid) { canvas.BlitRect(scroll, new RectF(x, (float)Math.Floor(y - scroll.H / 2f), scroll.W, scroll.H), Rgba.White); x += scroll.W + 6; }
                Text(canvas, n, 13, Anchor.MidLeft, x, y - 1, Story);
                y += SmallLine;
            }
            if (rarity >= 4)
            {
                var k2 = 0.5 + 0.5 * Math.Sin(time * 2.2);
                Halo2(canvas, plate.Inflate(-8, -8), Glows.Scaled(rc, 0.55 + 0.35 * k2));
            }
        }

        /// <summary>A 6×6 rarity gem on the 2 px grain.</summary>
        private static void Gem(Canvas canvas, float cx, float cy, Rgba c)
        {
            float x = (float)Math.Floor(cx / 2) * 2 - 4, y = (float)Math.Floor(cy / 2) * 2 - 4;
            canvas.Fill(new RectF(x + 2, y, 4, 8), Ink); canvas.Fill(new RectF(x, y + 2, 8, 4), Ink);
            canvas.Fill(new RectF(x + 2, y + 2, 4, 4), c); canvas.Fill(new RectF(x + 2, y + 2, 2, 2), Glows.Scaled(c, 1.5));
        }

        private static readonly (float dx, float dy, float ph)[] StarSpots = { (-112, -64, 0f), (108, -92, 1.9f), (114, 18, 3.4f), (-118, 44, 4.6f), (-54, -126, 2.7f), (64, -130, 5.5f) };

        /// <summary>Four-point stars that come and go around the doorway.</summary>
        private void Twinkles(Canvas canvas, float cx, float cy)
        {
            foreach (var (dx, dy, ph) in StarSpots)
            {
                var k = Math.Sin(time * 1.9 + ph); if (k < 0.25) continue;
                var arm = 2 * (1 + (int)(k * 2.2));
                var x = (float)Math.Floor((cx + dx) / 2) * 2; var y = (float)Math.Floor((cy + dy) / 2) * 2;
                var col = Glows.Scaled(Lilac, 0.6 + 0.4 * k);
                canvas.Fill(new RectF(x - arm, y, 2 * arm + 2, 2), col); canvas.Fill(new RectF(x, y - arm, 2, 2 * arm + 2), col);
                canvas.Fill(new RectF(x, y, 2, 2), Rgba.White);
            }
        }

        private float Appear(float delay) => Smooth((appear - delay) / AppearT);

        private void DrawStory(Canvas canvas, EventLayout lay)
        {
            var k = Appear(0f);
            canvas.PushAlpha(k);
            var rise = (int)Math.Round((1 - k) * 6) * 2;
            var r = lay.Story.Move(0, rise);
            PixelUi.Panel(canvas, Art, r);
            var used = lay.Body.Count * BodyLine + (lay.Previous.Count > 0 ? lay.Previous.Count * SmallLine + 16 : 0);
            float y = (float)Math.Floor((r.CenterY - used / 2f) / 2) * 2;
            if (lay.Previous.Count > 0)
            {
                var scroll = Art.Icon("spoil");
                if (scroll.Valid) canvas.BlitRect(scroll, new RectF(r.X + 24, (float)Math.Floor(y + SmallLine / 2f - scroll.H / 2f - 1), scroll.W, scroll.H), Rgba.White);
                foreach (var line in lay.Previous) { Text(canvas, line, 13, Anchor.MidLeft, r.X + 24 + 22, y + SmallLine / 2f - 1, Gilt); y += SmallLine; }
                y += 6;
                DitherRule(canvas, r.X + 24, y, r.W - 48, new Rgba(120, 96, 64));
                y += 10;
            }
            foreach (var line in lay.Body) { Text(canvas, line, 16, Anchor.MidLeft, r.X + 24, y + BodyLine / 2f - 1, Cream); y += BodyLine; }
            canvas.PopAlpha();
        }

        private void DrawChoice(Canvas canvas, ChoiceView v)
        {
            var k = Appear(0.08f + StaggerT * v.Index);
            if (k <= 0.01f) return;
            var focus = v.Index == selected && !v.Blocked; var hover = v.Index == hovered;
            var dx = v.Index == shakeIndex && shake > 0 ? (float)Math.Round(Math.Sin(shake * 40) * 3 * shake) * 2 : 0f;
            var lift = focus ? -2 : 0; var rise = (int)Math.Round((1 - k) * 6) * 2;
            var r = v.Rect.Move(dx, lift + rise);
            canvas.PushAlpha(k);
            if (focus) Halo2(canvas, r, new Rgba(255, 222, 140));
            else if (v.Index == selected) Halo2(canvas, r, new Rgba(170, 120, 110));
            PixelUi.Panel(canvas, Art, r);
            var inner = r.Inflate(-16, -16);
            canvas.Fill(inner, v.Blocked ? new Rgba(30, 20, 26, 90) : focus ? new Rgba(120, 70, 190, 72) : new Rgba(70, 46, 110, v.Compact ? 24 : 40));
            if (focus) canvas.Fill(new RectF(inner.X, inner.Y, 4, inner.H), Violet);
            // number, label, cost
            PixelUi.Keycap(canvas, Art, r.X + 30, v.LabelRect.CenterY + lift + rise, (v.Index + 1).ToString());
            var labelColor = v.Blocked ? Dim : focus ? new Rgba(255, 240, 210) : Cream;
            Text(canvas, v.Label, 16, Anchor.MidLeft, v.LabelRect.X, v.LabelRect.CenterY - 1 + lift + rise, labelColor, false, true);
            if (v.Compact)
            {
                var arrow = Art.Icon("combo");
                if (arrow.Valid) canvas.BlitRect(arrow, new RectF(r.Right - 24 - arrow.W * 2, (float)Math.Floor(r.CenterY - arrow.H), arrow.W * 2, arrow.H * 2), focus ? Rgba.White : new Rgba(170, 160, 150));
            }
            if (v.Cost.HasValue) DrawChip(canvas, new Piece { Chip = v.Cost.Value, Rect = v.CostRect.Move(dx, lift + rise) });
            foreach (var p in v.Pieces) { var q = p; q.Rect = p.Rect.Move(dx, lift + rise); DrawChip(canvas, q); }
            var ny = v.NoteY + lift + rise;
            foreach (var line in v.Note) { Text(canvas, line, 13, Anchor.MidLeft, v.LabelRect.X, ny + SmallLine / 2f - 1, Dim); ny += SmallLine; }
            if (v.Blocked)
            {
                var rr = v.ReasonRect.Move(dx, lift + rise);
                var no = Art.Icon("unplayable"); var tx = rr.X;
                if (no.Valid) { canvas.BlitRect(no, new RectF(tx, (float)Math.Floor(rr.CenterY - no.H / 2f), no.W, no.H), Rgba.White); tx += no.W + 6; }
                Text(canvas, v.Reason, 13, Anchor.MidLeft, tx, rr.CenterY - 1, Short);
                canvas.Fill(r.Inflate(-8, -8), new Rgba(8, 6, 14, 70));
            }
            canvas.PopAlpha();
        }

        private void DrawResult(Canvas canvas, EventLayout lay)
        {
            var k = Appear(0f);
            canvas.PushAlpha(k);
            var rise = (int)Math.Round((1 - k) * 6) * 2;
            var r = lay.ResultPanel.Move(0, rise);
            var col = lay.Outcome.HasValue ? lay.Outcome.Value.Color : Violet;
            PixelUi.Panel(canvas, Art, r);
            var inner = r.Inflate(-16, -16);
            var headerH = lay.ResultHeaderH;
            canvas.Fill(new RectF(inner.X, inner.Y, inner.W, headerH), Glows.Scaled(col, 0.26).WithAlpha(150));
            DitherRule(canvas, inner.X, inner.Y + headerH, inner.W, Glows.Scaled(col, 0.3).WithAlpha(150));
            canvas.Fill(new RectF(inner.X + 4, inner.Y + headerH + 4, inner.W - 8, 2), Glows.Scaled(col, 0.7));
            float y = r.Y + 12 + 8;
            Text(canvas, lay.ResultTitle, 20, Anchor.MidTop, r.CenterX, y, Cream, true, true);
            y += TextHeight(20, true) + 4;
            if (lay.Outcome.HasValue)
            {
                var o = lay.Outcome.Value; var w = ChipWidth(o);
                DrawChip(canvas, new Piece { Chip = o, Rect = new RectF((float)Math.Floor(r.CenterX - w / 2f), y, w, ChipH) });
            }
            y = lay.ResultTextY + rise;
            foreach (var line in lay.ResultLines) { Text(canvas, line, 16, Anchor.MidTop, r.CenterX, y, Cream); y += BodyLine; }
            // the chips are laid out from the left; centre each row in the panel
            foreach (var row in lay.ResultPieces.GroupBy(p => p.Rect.Y))
            {
                var list = row.ToList(); var left = list.Min(p => p.Rect.X); var right = list.Max(p => p.Rect.Right);
                var shift = (float)Math.Floor((r.CenterX - (left + right) / 2f) / 2) * 2;
                foreach (var p in list) { var q = p; q.Rect = p.Rect.Move(shift, rise); DrawChip(canvas, q); }
            }
            canvas.PopAlpha();
            var b = lay.Continue; var hov = hovered == 0;
            if (hov) Halo2(canvas, b.Move(0, -1), new Rgba(255, 226, 140));
            canvas.PushAlpha(Appear(0.15f));
            PixelUi.Button(canvas, Art, b, "Continuar", "gold", hov ? ButtonState.Hover : ButtonState.Idle, null, "Enter", time, 0f, 16);
            canvas.PopAlpha();
        }

        private void DrawFooter(Canvas canvas, EventLayout lay)
        {
            // the hero: life (with the price of the focused choice blinking) and luck
            var plate = new RectF(PlateX, FooterY - 20, PlateW, 40);
            PixelUi.Panel(canvas, Art, plate);
            var bar = new IRect((int)plate.X + 38, (int)plate.CenterY - 7, 120, 14);
            var ratio = run.MaxHp > 0 ? Math.Min(1.0, run.Hp / (double)run.MaxHp) : 0;
            canvas.Rect(bar, new Rgba(50, 14, 18), 0, 3);
            canvas.Rect(new RectF(bar.X, bar.Y, (int)(bar.W * ratio), bar.H), new Rgba(200, 46, 52), 0, 3);
            var loss = 0;
            if (!Session.Completed && selected >= 0 && selected < Session.Stage.Choices.Count && Session.BlockReason(selected).Length == 0)
                loss = Math.Min(run.Hp - 1, Session.Stage.Choices[selected].HpCost);
            if (loss > 0 && run.MaxHp > 0)
            {
                var after = (int)(bar.W * (run.Hp - loss) / (double)run.MaxHp); var full = (int)(bar.W * ratio);
                var kk = 0.5 + 0.5 * Math.Sin(time * 6.0);
                canvas.Fill(new RectF(bar.X + after, bar.Y + 2, Math.Max(2, full - after), bar.H - 4), new Rgba((int)(170 + 85 * kk), (int)(60 + 90 * kk), (int)(60 + 60 * kk)));
            }
            canvas.Rect(bar, new Rgba(14, 8, 12), 1, 3);
            var heart = Art.Icon("heal");
            if (heart.Valid) canvas.BlitRect(heart, new RectF(plate.X + 14, (float)Math.Floor(plate.CenterY - heart.H / 2f), heart.W, heart.H), Rgba.White);
            Txt.OutlinedAt(canvas, Fonts.Get(11), run.Hp + "/" + run.MaxHp, Anchor.Center, bar.CenterX, bar.CenterY, Cream);
            if (loss > 0) Text(canvas, "−" + loss, 13, Anchor.MidLeft, bar.Right + 6, plate.CenterY - 1, Short);
            var clover = Art.Icon("clover");
            var luckText = "Suerte " + run.Luck; var lx = plate.Right - 18 - TextWidth(luckText, 13) - 20;
            if (clover.Valid) canvas.BlitRect(clover, new RectF(lx, (float)Math.Floor(plate.CenterY - clover.H / 2f), clover.W, clover.H), Rgba.White);
            Text(canvas, luckText, 13, Anchor.MidLeft, lx + 20, plate.CenterY - 1, new Rgba(170, 236, 150));
            // key hints, right-aligned under the column
            var hints = Session.Completed ? new[] { ("Enter", "Continuar") } : new[] { ("1-" + Session.Stage.Choices.Count, "Elegir"), ("Enter", "Confirmar") };
            float x = ColX + ColW;
            for (var i = hints.Length - 1; i >= 0; i--)
            {
                var (key, label) = hints[i];
                var t = Text(canvas, label, 13, Anchor.MidRight, x, FooterY - 1, Dim);
                var kw = PixelUi.KeycapWidth(Art, key);
                PixelUi.Keycap(canvas, Art, t.X - kw / 2f - 2, FooterY, key);
                x = t.X - kw - 18;
            }
        }

        private void DrawFeedback(Canvas canvas)
        {
            if (feedbackT <= 0) return;
            canvas.PushAlpha(Math.Min(1f, feedbackT / 0.3f));
            var w = TextWidth(feedback, 16) + 48;
            var box = RectF.CenteredInt(ColX + ColW / 2f, FooterY - 52, w, 40);
            PixelUi.Panel(canvas, Art, box);
            Text(canvas, feedback, 16, Anchor.Center, box.CenterX, box.CenterY - 1, Short);
            canvas.PopAlpha();
        }
    }
}

namespace LastWish.Presentation
{
    /// <summary>
    /// The art of the event plate, drawn by code in native pixels (shown ×2, docs/art-style.md): a stone arch in the crypt
    /// wall whose opening glows violet, two worn steps, a keystone with a gem in the event's rarity colour, and the "?" rune
    /// of the map's event room floating inside. Two layers like every sprite: the opaque paint and the light of the magic
    /// (the opening and the rune) to add on top.
    /// </summary>
    public static class EventPortal
    {
        public const int W = 96, H = 112, Cx = 48, SpringY = 46, OuterR = 36, InnerR = 24, BaseY = 96, Course = 8, GlowY = 72;
        public const int RuneW = 13, RuneH = 17;
        private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
        private static float B(int x, int y) => (Bayer[y & 3, x & 3] + 0.5f) / 16f;
        private static readonly Rgba Ink = new Rgba(7, 5, 12), Mortar = new Rgba(13, 10, 20);
        private static readonly Rgba[] Stone = { new Rgba(18, 14, 26), new Rgba(29, 24, 40), new Rgba(42, 35, 56), new Rgba(58, 49, 80), new Rgba(76, 67, 102), new Rgba(100, 91, 130) };
        private static readonly Rgba[] Void = { new Rgba(11, 6, 20), new Rgba(23, 11, 42), new Rgba(38, 18, 67), new Rgba(58, 27, 102), new Rgba(87, 41, 154), new Rgba(126, 69, 207) };
        private static readonly Rgba[] Rim = { new Rgba(64, 40, 110), new Rgba(104, 74, 176), new Rgba(150, 118, 226) };
        private static readonly string[] Rune =
        {
            "...XXXXX...", "..XXXXXXX..", ".XXX...XXX.", ".XX.....XX.", ".......XXX.", "......XXX..", ".....XXX...", "....XXX....", "....XX.....", "....XX.....", "...........", "....XX.....", "....XX.....",
        };

        private static bool InArc(float px, float py, float r) => py < SpringY ? (px - Cx) * (px - Cx) + (py - SpringY) * (py - SpringY) <= r * r : Math.Abs(px - Cx) <= r;
        private static bool Opening(int x, int y) => y < BaseY && InArc(x + .5f, y + .5f, InnerR);
        private static bool Arch(int x, int y) => y < BaseY && InArc(x + .5f, y + .5f, OuterR) && !Opening(x, y);
        private static bool Keystone(int x, int y) { var hw = 4f + (26 - y) * 0.36f; return y >= 7 && y < 26 && Math.Abs(x + .5f - Cx) <= hw && !Opening(x, y); }
        private static bool Step(int x, int y) => y >= BaseY && y < BaseY + 6 && x >= 5 && x < W - 5 || y >= BaseY + 6 && y < H - 1 && x >= 0 && x < W;
        private static uint Hash(int a, int b) { var h = (uint)(a * 73856093) ^ (uint)(b * 19349663); h ^= h >> 13; h *= 0x5bd1e995; return h ^ (h >> 15); }

        /// <summary>Which stone a wall pixel belongs to (-1: mortar or none).</summary>
        private static int StoneId(int x, int y)
        {
            if (Keystone(x, y)) return 1000;
            if (Step(x, y))
            {
                var row = y < BaseY + 6 ? 0 : 1; var off = row == 0 ? 5 : 13; var w = 16;
                if (x == 0 || (x + off) % w == 0) return -1;
                if (row == 0 && y == BaseY || row == 1 && y == BaseY + 6) return 2000 + row * 100 + (x + off) / w;   // top face
                return 3000 + row * 100 + (x + off) / w;
            }
            if (!Arch(x, y)) return -1;
            float px = x + .5f, py = y + .5f, dx = px - Cx;
            if (py < SpringY)
            {
                var r = (float)Math.Sqrt(dx * dx + (py - SpringY) * (py - SpringY));
                var a = Math.Atan2(py - SpringY, dx) + Math.PI;      // 0 left … π right
                const double seg = Math.PI / 9; var k = a / seg; var near = Math.Abs(k - Math.Round(k)) * seg * r;
                if (near < 0.75) return -1;
                return (int)Math.Floor(k);
            }
            var course = (int)((py - SpringY) / Course);
            if ((int)(py - SpringY) % Course == 0) return -1;
            var left = dx < 0; var jx = left ? x - (Cx - OuterR) : x - (Cx + InnerR); var half = (OuterR - InnerR) / 2;
            if (course % 2 == 1 && jx == half) return -1;
            return 100 + course * 4 + (left ? 0 : 2) + (course % 2 == 1 && jx > half ? 1 : 0);
        }

        /// <summary>The paint (opaque stone, dark opening) and the light (the glow of the opening) of the arch.</summary>
        public static (Rgba[] paint, Rgba[] light) Paint(Rgba gem)
        {
            var paint = new Rgba[W * H]; var light = new Rgba[W * H];
            var ids = new int[W * H]; var solid = new bool[W * H];
            for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                {
                    ids[y * W + x] = StoneId(x, y);
                    solid[y * W + x] = Arch(x, y) || Keystone(x, y) || Step(x, y) || Opening(x, y);
                }
            int Id(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? -2 : ids[y * W + x];
            bool Solid(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && solid[y * W + x];
            for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                {
                    var i = y * W + x; if (!solid[i]) continue;
                    float px = x + .5f, dx = px - Cx;
                    if (Opening(x, y) && !Keystone(x, y))
                    {
                        // the opening: dark at the edges, glowing violet towards the lower middle
                        var gx = dx / (InnerR + 2f); var gy = (y + .5f - GlowY) / 40f; var d = (float)Math.Sqrt(gx * gx + gy * gy);
                        var v = Math.Max(0f, 1f - d);
                        var level = Math.Min(5, (int)Math.Floor(Math.Pow(v, 0.85) * 5.2f + B(x, y) - 0.2f));
                        level = Math.Max(0, level);
                        paint[i] = Void[level];
                        if (level >= 3) light[i] = Glows.Scaled(Void[level], 0.35 + 0.15 * (level - 3));
                        continue;
                    }
                    var id = ids[i];
                    if (id == -1) { paint[i] = Mortar; continue; }
                    int tone;
                    if (id >= 3000) tone = id >= 3100 ? 1 : 2;          // step fronts
                    else if (id >= 2000) tone = 4;                       // step tops
                    else if (id == 1000) tone = 4;                       // keystone
                    else tone = 2 + (int)(Hash(id, 7) % 2) + (dx < -4 ? 1 : 0);
                    // bevel: lit where the stone meets mortar above or to its left, shaded below / right
                    if (Id(x, y - 1) != id || Id(x - 1, y) != id) tone++;
                    else if (Id(x, y + 1) != id || Id(x + 1, y) != id) tone--;
                    if (id < 1000 && Hash(x, y) % 23 == 0) tone--;      // pits in the old stone
                    paint[i] = Stone[Math.Max(0, Math.Min(5, tone))];
                    // the opening lights the inner rim of the arch and the middle of the steps
                    if (Arch(x, y) || Keystone(x, y))
                    {
                        var r = y + .5f < SpringY ? InnerR + 2.2f - (float)Math.Sqrt(dx * dx + (y + .5f - SpringY) * (y + .5f - SpringY)) : InnerR + 2.2f - Math.Abs(dx);
                        var near = Math.Max(0f, 1f - Math.Abs(y + .5f - GlowY) / 60f);
                        var k = (1f - Math.Abs(r) / 2.4f) * (0.5f + 0.5f * near);
                        if (r < 0 && k > 0 && k + (B(x, y) - .5f) * 0.5f > 0.3f) paint[i] = Rim[k > 0.62f ? 1 : 0];
                    }
                    else if (id >= 2000 && id < 3000 && Math.Abs(dx) + B(x, y) * 6 < 22) paint[i] = Rim[Math.Abs(dx) < 10 ? 2 : 1];
                    else if (id >= 3000 && id < 3100 && Math.Abs(dx) + B(x, y) * 8 < 17) paint[i] = Rim[0];
                }
            // the gem on the keystone, in the event's rarity colour
            var gy0 = 14;
            for (var y = 0; y < 5; y++)
                for (var x = 0; x < 5; x++)
                {
                    var m = Math.Abs(x - 2) + Math.Abs(y - 2); if (m > 2) continue;
                    var i = (gy0 + y) * W + Cx - 3 + x;
                    paint[i] = m == 2 ? Ink : x + y <= 3 ? Glows.Scaled(gem, 1.45) : gem;
                    if (m < 2) light[i] = Glows.Scaled(gem, 0.55);
                }
            // one dark outline round the whole silhouette and between the stone and the opening
            var outline = new List<int>();
            for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                {
                    var i = y * W + x;
                    if (!solid[i]) { if (Solid(x - 1, y) || Solid(x + 1, y) || Solid(x, y - 1) || Solid(x, y + 1)) outline.Add(i); continue; }
                    if (Opening(x, y) && !Keystone(x, y) && (Arch(x - 1, y) || Arch(x + 1, y) || Arch(x, y - 1) || Keystone(x, y - 1))) outline.Add(i);
                }
            foreach (var i in outline) { paint[i] = Ink; light[i] = default; }
            return (paint, light);
        }

        /// <summary>The floating "?" rune (ink outline, lilac body, white core) — drawn opaque, then added again as light.</summary>
        public static Rgba[] RuneSprite()
        {
            var px = new Rgba[RuneW * RuneH];
            bool On(int x, int y) => y >= 0 && y < Rune.Length && x >= 0 && x < Rune[0].Length && Rune[y][x] == 'X';
            for (var y = 0; y < RuneH; y++)
                for (var x = 0; x < RuneW; x++)
                {
                    int rx = x - 1, ry = y - 2;   // the glyph with a pixel of outline round it
                    if (On(rx, ry)) px[y * RuneW + x] = On(rx, ry - 1) && On(rx - 1, ry) ? new Rgba(255, 255, 255) : On(rx, ry - 1) || On(rx - 1, ry) ? new Rgba(240, 228, 255) : new Rgba(199, 155, 255);
                    else if (On(rx - 1, ry) || On(rx + 1, ry) || On(rx, ry - 1) || On(rx, ry + 1)) px[y * RuneW + x] = new Rgba(58, 33, 99);
                }
            return px;
        }
    }
}
