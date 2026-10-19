using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;

namespace LastWish.Presentation
{
    public readonly struct StatSource { public readonly string Label; public readonly int Amount; public StatSource(string l, int a) { Label = l; Amount = a; } }

    public sealed class HeroStat
    {
        public string Key, Name, Icon, Effect = ""; public int Value, Base, Reference = 10; public int? Current; public List<StatSource> Sources = new List<StatSource>();
        public int Bonus => Value - Base;
        public string Breakdown()
        {
            var parts = new List<string> { "Base " + Base };
            foreach (var s in Sources) if (s.Amount != 0) parts.Add(s.Label + " " + (s.Amount > 0 ? "+" : "−") + Math.Abs(s.Amount));
            return string.Join(" · ", parts);
        }
    }

    /// <summary>application/hero_stats.py: every stat, where each point comes from and what it does.</summary>
    public sealed class HeroSheetData
    {
        public string Name, Title; public List<HeroStat> Stats = new List<HeroStat>();
        public int Gold, Floor, DeckSize, RelicCount; public int? Turn;
        public List<(string kind, int n)> DeckByType = new List<(string, int)>();
        public double GoldenCard, GoldenRelic, EpicRelic;
        public List<(string name, int stacks, bool buff)> Statuses = new List<(string, int, bool)>();
        public HeroStat Stat(string key) => Stats.First(s => s.Key == key);
        public const int BaseDraw = 5;

        private static List<StatSource> PerRelic(IEnumerable<OwnedRelic> relics, Func<OwnedRelic, int> amount)
        {
            var list = new List<StatSource>();
            foreach (var r in relics) { var a = r.Active ? amount(r) : 0; if (a != 0) list.Add(new StatSource(r.Definition.Name, a)); }
            return list;
        }
        private static int Tag(OwnedRelic r, string tag, int amount) => r.Definition.Tag == tag ? amount * r.Multiplier : 0;
        private static List<StatSource> Pruebas(int amount) => amount != 0 ? new List<StatSource> { new StatSource("Pruebas", amount) } : new List<StatSource>();

        public static HeroSheetData For(GameContext game, RunState run, CombatState state = null)
        {
            var hero = run.Hero; var t = Tuning.Current; var relics = run.OwnedRelics; var g = game.Data.Glossary;
            var sheet = new HeroSheetData { Name = hero.Name, Title = Characters.Description(run.HeroKind) };

            var hpSources = PerRelic(relics, r => Tag(r, "iron_heart", 15) + Tag(r, "vitality_amulet", 10) + Tag(r, "wild_fruit", 7) + Tag(r, "stone_heart", 30));
            hpSources.AddRange(Pruebas(t.ExtraMaxHp));
            var maxHp = hero.MaxHp + hpSources.Sum(s => s.Amount); var current = run.Hp;
            if (state != null)
            {
                if (state.Hero.MaxHp != maxHp) { hpSources.Add(new StatSource("Este combate", state.Hero.MaxHp - maxHp)); maxHp = state.Hero.MaxHp; }
                current = state.Hp;
            }
            sheet.Stats.Add(new HeroStat { Key = "hp", Name = "Vida", Icon = "heal", Value = maxHp, Base = hero.MaxHp, Sources = hpSources, Effect = "Si llega a 0, la partida termina. Se conserva entre salas.", Current = Math.Min(current, maxHp), Reference = maxHp });

            var manaSources = PerRelic(relics, r => Tag(r, "combat_amulet", 1)); manaSources.AddRange(Pruebas(t.ExtraMana));
            var manaTotal = hero.MaxMana + manaSources.Sum(s => s.Amount);
            if (state != null && state.ManaMaximum != manaTotal) { manaSources.Add(new StatSource("Este combate", state.ManaMaximum - manaTotal)); manaTotal = state.ManaMaximum; }
            var growth = run.RelicEffect("eternal_fount", 1);
            sheet.Stats.Add(new HeroStat { Key = "mana", Name = "Maná", Icon = "mana", Value = manaTotal, Base = hero.MaxMana, Sources = manaSources, Reference = 6,
                Effect = "Cada turno empiezas con " + manaTotal + " de maná para jugar cartas." + (growth != 0 ? " Sube " + growth + " por turno." : "") });

            var atkSources = PerRelic(relics, r => Tag(r, "fire_orb", 2)); atkSources.AddRange(Pruebas(t.ExtraDamage));
            var atkTotal = hero.AttackBonus + atkSources.Sum(s => s.Amount);
            if (state != null) { var live = state.AttackBonusNow + run.RelicEffect("fire_orb", 2); if (live != atkTotal) { atkSources.Add(new StatSource("Este combate", live - atkTotal)); atkTotal = live; } }
            sheet.Stats.Add(new HeroStat { Key = "attack", Name = "Ataque", Icon = "damage", Value = atkTotal, Base = hero.AttackBonus, Sources = atkSources, Effect = "Cada carta de ataque inflige +" + atkTotal + " de daño." });

            var dexSources = Pruebas(t.ExtraDexterity); var dexTotal = hero.Dexterity + t.ExtraDexterity;
            if (state != null && state.DexterityNow != dexTotal) { dexSources.Add(new StatSource("Este combate", state.DexterityNow - dexTotal)); dexTotal = state.DexterityNow; }
            sheet.Stats.Add(new HeroStat { Key = "dexterity", Name = "Destreza", Icon = "block", Value = dexTotal, Base = hero.Dexterity, Sources = dexSources, Effect = "Cada carta de escudo da +" + dexTotal + " de escudo." });

            var luckSources = Pruebas(Tuning.HeroLuck(hero.Luck) - hero.Luck);
            luckSources.AddRange(PerRelic(relics, r => Tag(r, "seven_leaf_clover", 100) + Tag(r, "silver_horseshoe", 30) + Tag(r, "rabbit_foot", 15)));
            var luckTotal = Tuning.HeroLuck(hero.Luck) + run.RelicLuck;
            var goldCard = LuckReport.GoldenChance("card", luckTotal); var goldRelic = LuckReport.GoldenChance("relic", luckTotal);
            var odds = LuckReport.RarityOdds(luckTotal); var epic = odds[4] + odds[5];
            sheet.Stats.Add(new HeroStat { Key = "luck", Name = "Suerte", Icon = "clover", Value = luckTotal, Base = hero.Luck, Sources = luckSources, Reference = 20,
                Effect = "En sobres, premios y gachapón: Rara o mejor " + LuckReport.Pct(odds[3] + epic) + " · carta dorada " + LuckReport.Pct(goldCard) + "." });

            var drawSources = PerRelic(relics, r => Tag(r, "broken_totem", 1) + Tag(r, "energy_stone", 1)); drawSources.AddRange(Pruebas(t.ExtraDraw));
            var drawTotal = BaseDraw + drawSources.Sum(s => s.Amount);
            sheet.Stats.Add(new HeroStat { Key = "draw", Name = "Robo", Icon = "draw", Value = drawTotal, Base = BaseDraw, Sources = drawSources, Effect = "Robas " + drawTotal + " cartas al empezar cada turno." });
            const int handMax = CombatState.HandMaxSize;
            sheet.Stats.Add(new HeroStat { Key = "hand", Name = "Mano máxima", Icon = "hand_cards", Value = handMax, Base = handMax, Effect = "Puedes tener hasta " + handMax + " cartas; las que no caben se quedan en el mazo." });

            var labels = new Dictionary<string, string> { ["ATTACK"] = "Ataques", ["SKILL"] = "Habilidades", ["POWER"] = "Poderes", ["STATUS"] = "Estados" };
            foreach (var card in run.Deck)
            {
                var label = labels.TryGetValue(CardWidget.TypeOf(game, card), out var l) ? l : "Otras";
                var i = sheet.DeckByType.FindIndex(p => p.kind == label);
                if (i < 0) sheet.DeckByType.Add((label, 1)); else sheet.DeckByType[i] = (label, sheet.DeckByType[i].n + 1);
            }
            if (state != null)
                foreach (var k in state.HeroStatuses.Active)
                {
                    var name = g.StatusName.TryGetValue(k, out var n) ? n : k.ToString();
                    sheet.Statuses.Add((name, state.HeroStatuses[k], g.IsBuffStatus(name)));
                }
            sheet.Gold = run.Gold; sheet.Floor = run.Floor; sheet.DeckSize = run.Deck.Count; sheet.RelicCount = relics.Count;
            sheet.GoldenCard = goldCard; sheet.GoldenRelic = goldRelic; sheet.EpicRelic = epic; sheet.Turn = state?.Turn;
            return sheet;
        }
    }

    /// <summary>ui/hero_sheet.py: "Héroe" overlay (key C).</summary>
    public sealed class HeroSheetOverlay : IOverlay
    {
        public static readonly IRect PanelRect = new IRect(100, 46, 1080, 630);
        private static readonly IRect Close = new IRect(PanelRect.Right - 58, PanelRect.Y + 14, 40, 36), Portrait = new IRect(PanelRect.X + 34, PanelRect.Y + 70, 300, 268);
        private const int RowX = 100 + 372, RowY = 46 + 72, RowH = 63, BarX = RowX + 300, BarW = 340, BarH = 14;
        private const float OpenTime = 0.25f, FillTime = 0.55f, FillStagger = 0.08f;
        public static readonly Dictionary<string, Rgba> StatColor = new Dictionary<string, Rgba>
        {
            ["hp"] = new Rgba(226, 62, 62), ["mana"] = new Rgba(70, 140, 240), ["attack"] = new Rgba(240, 112, 70), ["dexterity"] = new Rgba(80, 190, 230),
            ["luck"] = new Rgba(110, 210, 90), ["draw"] = new Rgba(236, 190, 70), ["hand"] = new Rgba(180, 120, 236),
        };
        private static readonly Rgba BonusC = new Rgba(130, 245, 120), Malus = new Rgba(255, 90, 80), Gold = new Rgba(246, 204, 92), Dim = new Rgba(178, 170, 156);
        private readonly GameContext game; private readonly BurstParticles particles = new BurstParticles(240, 9);
        private readonly HashSet<string> sparked = new HashSet<string>();
        private (float x, float y) mouse = (-1, -1);
        private static Tex layer;
        public HeroSheetData Sheet { get; }
        public float T { get; private set; }
        public bool Dismissed { get; private set; }

        public HeroSheetOverlay(GameContext game, HeroSheetData sheet) { this.game = game; Sheet = sheet; }

        public void HandleEvent(in InputEvent e)
        {
            if (e.Type == EventType.KeyDown && (e.Key == Key.Escape || e.Key == Key.C)) Dismissed = true;
            else if (e.Type == EventType.MouseMove) mouse = e.Pos;
            else if (e.IsLeftDown && (Close.Contains(e.X, e.Y) || !PanelRect.Contains(e.X, e.Y))) Dismissed = true;
        }

        public float Fill(int index)
        {
            var start = OpenTime + index * FillStagger;
            var k = Math.Max(0f, Math.Min(1f, (T - start) / FillTime));
            return 1 - (1 - k) * (1 - k) * (1 - k);
        }
        private static float BarTotal(HeroStat s) { var scale = Math.Max(Math.Max(s.Reference, s.Value), Math.Max(s.Base, 1)); return Math.Min(1f, Math.Max(s.Value, 0) / (float)scale); }

        public void Update(float dt)
        {
            dt = Math.Max(0f, Math.Min(dt, 0.1f)); T += dt;
            for (var i = 0; i < Sheet.Stats.Count; i++)
            {
                var stat = Sheet.Stats[i];
                if (sparked.Contains(stat.Key) || Fill(i) < 1f) continue;
                sparked.Add(stat.Key);
                var end = BarX + (int)(BarW * BarTotal(stat)); var y = RowY + i * RowH + 10 + BarH / 2;
                particles.Burst(end, y, 10, new[] { StatColor[stat.Key], Rgba.White, BonusC }, (30f, 120f), null, (0.25f, 0.5f), (2f, 3f), ParticleStyle.Glow);
            }
            particles.Update(dt);
        }

        public void Draw(Canvas canvas)
        {
            var k = Math.Min(1f, T / OpenTime); var ease = 1 - (1 - k) * (1 - k) * (1 - k);
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(4, 4, 10, (int)(200 * ease)));
            var dy = (int)((1 - ease) * 40);
            if (ease < 1)
            {
                // The original fades a whole composed layer: compose it offscreen, then blit it with alpha.
                if (layer == null) layer = canvas.Backend.CreateTarget("hero_sheet_layer", Canvas.Width, Canvas.Height);
                canvas.BeginTarget(layer); DrawContent(canvas); canvas.EndTarget();
                canvas.BlitRect(new Sprite(layer), new RectF(0, dy, Canvas.Width, Canvas.Height), Rgba.White.WithAlpha((int)(255 * ease)));
            }
            else DrawContent(canvas);
            particles.Draw(canvas, game.Art, 0, dy);
        }

        private FontRegistry F => game.Fonts;

        private void DrawContent(Canvas canvas)
        {
            var art = game.Art;
            PixelUi.Panel(canvas, art, PanelRect);
            PixelUi.Ribbon(canvas, art, PanelRect.CenterX, PanelRect.Y + 8, "HÉROE", 17);
            PixelUi.Button(canvas, art, Close, "X", state: Close.Contains(mouse.x, mouse.y) ? ButtonState.Hover : ButtonState.Idle, t: T, size: 18);
            DrawPortrait(canvas);
            Txt.Outlined(canvas, F.Get(16), "ATRIBUTOS", RowX, PanelRect.Y + 40, Gold);
            Txt.OutlinedAt(canvas, F.Get(12), "verde = bonus de reliquias, cartas o Pruebas", Anchor.TopRight, BarX + BarW, PanelRect.Y + 44, Dim);
            for (var i = 0; i < Sheet.Stats.Count; i++) DrawRow(canvas, i, Sheet.Stats[i]);
            DrawTiles(canvas);
            var x = PanelRect.CenterX - 50;
            PixelUi.Keycap(canvas, art, x, PanelRect.Bottom - 16, "Esc");
            PixelUi.Keycap(canvas, art, x + 30, PanelRect.Bottom - 16, "C");
            Txt.OutlinedAt(canvas, F.Get(12), "cerrar", Anchor.MidLeft, x + 46, PanelRect.Bottom - 16, Dim);
        }

        private Sprite PortraitBack(int level)
        {
            // pygame.draw on an SRCALPHA surface overwrites pixels: each smaller circle replaces the bigger one.
            return new Sprite(game.Art.Procedural("hero_portrait_back_" + level, Portrait.W, Portrait.H, () =>
            {
                int w = Portrait.W, h = Portrait.H; var px = new Rgba[w * h];
                var pulse = level / 10.0; int cx = w / 2, cy = h / 2 + 30;
                for (var y = 0; y < h; y++)
                    for (var x = 0; x < w; x++)
                    {
                        var c = new Rgba(10, 8, 18, 255);
                        var d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                        for (var r = 12; r <= 150; r += 6)
                            if (d2 <= r * r) { c = new Rgba(255, 170, 80, (int)((26 + 10 * pulse) * (1 - r / 150.0))); break; }
                        double ex = (x + 0.5 - (w / 2 - 70 + 70)) / 70.0, ey = (y + 0.5 - (h - 44 + 11)) / 11.0;
                        if (ex * ex + ey * ey <= 1) c = new Rgba(0, 0, 0, 120);
                        px[y * w + x] = c;
                    }
                return px;
            }));
        }

        private void DrawPortrait(Canvas canvas)
        {
            var frame = Portrait;
            var pulse = 0.5 + 0.5 * Math.Sin(T * 1.6);
            canvas.BlitRect(PortraitBack(MathX.Round(pulse * 10)), frame, Rgba.White);
            Looks.Hero(canvas, game.Art, Sheet.Name, 192, T, frame.CenterX, frame.Bottom - 18);
            canvas.Rect(frame, new Rgba(120, 96, 60), 2);
            var y = frame.Bottom + 10;
            var r = Txt.OutlinedAt(canvas, F.Get(26), Sheet.Name, Anchor.MidTop, frame.CenterX, y, Gold);
            y += (int)r.H + 2;
            foreach (var line in RichText.Lines(game.Data.Glossary, Sheet.Title, F.Get(13), frame.W, Dim)) { line.Draw(canvas, frame.CenterX - line.Width / 2, y); y += line.Height; }
            y += 10;
            DrawHp(canvas, new IRect(frame.X, y, frame.W, 26), Sheet.Stat("hp"));
            y += 38;
            if (Sheet.Statuses.Count == 0) return;
            Txt.Outlined(canvas, F.Get(13), "Estados ahora", frame.X, y, Gold);
            y += 20; var x = frame.X;
            foreach (var (name, stacks, buff) in Sheet.Statuses.Take(6))
            {
                var icon = game.Art.Icon(game.Data.Glossary.StatusIconOf(name, buff));
                if (icon.Valid) canvas.Blit(icon, x, y, 2);
                var text = name + " " + stacks;
                Txt.Outlined(canvas, F.Get(12), text, x + 30, y + 7, buff ? new Rgba(160, 240, 160) : new Rgba(236, 170, 240));
                x += 30 + Txt.OutlinedSize(F.Get(12), text).w + 12;
                if (x > frame.Right - 60) { x = frame.X; y += 30; }
            }
        }

        private void DrawHp(Canvas canvas, IRect rect, HeroStat hp)
        {
            var fill = Fill(0); var current = hp.Current ?? hp.Value;
            canvas.Rect(rect, new Rgba(50, 14, 18), 0, 4);
            var ratio = hp.Value > 0 ? current / (double)hp.Value : 0;
            var iw = (int)(rect.W * ratio * fill);
            if (iw > 0)
            {
                canvas.Rect(new RectF(rect.X, rect.Y, iw, rect.H), new Rgba(200, 46, 52), 0, 4);
                canvas.Fill(new RectF(rect.X + 2, rect.Y + 3, Math.Max(0, iw - 4), 4), new Rgba(250, 110, 100));
            }
            canvas.Rect(rect, new Rgba(14, 8, 12), 2, 4);
            var heart = game.Art.Icon("heal");
            if (heart.Valid) canvas.BlitRect(heart, RectF.CenteredInt(rect.X + 4, rect.CenterY, heart.W * 2, heart.H * 2), Rgba.White);
            Txt.OutlinedAt(canvas, F.Get(17), current + " / " + hp.Value, Anchor.Center, rect.CenterX, rect.CenterY, new Rgba(255, 240, 230));
        }

        private void DrawRow(Canvas canvas, int i, HeroStat stat)
        {
            var y = RowY + i * RowH; var color = StatColor.TryGetValue(stat.Key, out var c) ? c : Gold;
            if (i % 2 == 0) canvas.Fill(new RectF(RowX - 8, y - 4, BarX + BarW - RowX + 16, RowH - 4), new Rgba(255, 255, 255, 8));
            var icon = game.Art.Icon(stat.Icon);
            if (icon.Valid) canvas.BlitRect(icon, RectF.CenteredInt(RowX + 20, y + 18, icon.W * 3, icon.H * 3), Rgba.White);
            Txt.Outlined(canvas, F.Get(18), stat.Name, RowX + 48, y, color);
            var valueTxt = stat.Current.HasValue ? stat.Current + " / " + stat.Value : stat.Value.ToString();
            Txt.OutlinedAt(canvas, F.Get(26), valueTxt, Anchor.TopRight, BarX - 14, y - 4, Rgba.White);
            var room = BarX + BarW - RowX - 48;
            var effect = RichText.Line(game.Data.Glossary, stat.Effect, F.Get(13), new Rgba(226, 220, 206));
            if (effect.Width > room) effect = RichText.Line(game.Data.Glossary, TextFit.Fit(F.Get(12), stat.Effect, room), F.Get(12), new Rgba(226, 220, 206));
            effect.Draw(canvas, RowX + 48, y + 30);
            DrawBar(canvas, i, stat, color, y + 4);
            Txt.OutlinedAt(canvas, F.Get(11), stat.Breakdown(), Anchor.TopRight, BarX + BarW, y + 4 + BarH + 2, Dim);
        }

        private void DrawBar(Canvas canvas, int i, HeroStat stat, Rgba color, int y)
        {
            var rect = new IRect(BarX, y, BarW, BarH);
            canvas.Rect(rect.Inflate(4, 4), new Rgba(16, 12, 22), 0, 3);
            canvas.Rect(rect, new Rgba(34, 30, 44), 0, 2);
            var scale = Math.Max(Math.Max(stat.Reference, stat.Value), Math.Max(stat.Base, 1)); var fill = Fill(i);
            var baseW = (int)(BarW * Math.Min(stat.Base, stat.Bonus < 0 ? stat.Value : stat.Base) / (double)scale * fill);
            var totalW = (int)(BarW * Math.Max(0, stat.Value) / (double)scale * fill);
            if (stat.Key == "hp" && stat.Current.HasValue)
            {
                baseW = (int)(BarW * Math.Min(stat.Base, stat.Current.Value) / (double)scale * fill);
                totalW = (int)(BarW * stat.Current.Value / (double)scale * fill);
            }
            if (baseW > 0)
            {
                canvas.Rect(new RectF(rect.X, rect.Y, baseW, rect.H), color, 0, 2);
                canvas.Line(rect.X + 1, rect.Y + 2, rect.X + baseW - 2, rect.Y + 2, new Rgba(Math.Min(255, color.R + 60), Math.Min(255, color.G + 60), Math.Min(255, color.B + 60)));
            }
            if (totalW > baseW)
            {
                canvas.Fill(new RectF(rect.X + baseW, rect.Y, totalW - baseW, rect.H), BonusC);
                canvas.Line(rect.X + baseW, rect.Y + 2, rect.X + totalW - 2, rect.Y + 2, new Rgba(220, 255, 210));
            }
            if (stat.Bonus < 0 && stat.Key != "hp")
            {
                var lostW = (int)(BarW * (stat.Base - Math.Max(0, stat.Value)) / (double)scale * fill);
                canvas.Rect(new RectF(rect.X + totalW, rect.Y, lostW, rect.H), Malus, 1);
            }
            if (scale <= 12)
                for (var k = 1; k < scale; k++) { var x = rect.X + (int)(BarW * k / (double)scale); canvas.Line(x, rect.Y, x, rect.Bottom - 1, new Rgba(12, 10, 16)); }
            canvas.Rect(rect, new Rgba(8, 6, 12), 1, 2);
        }

        private void DrawTiles(Canvas canvas)
        {
            var s = Sheet;
            var tiles = new List<(string icon, string label, string value)> { ("coin", "Oro", s.Gold.ToString()), ("tower", "Piso", s.Floor.ToString()), ("deck", "Mazo", s.DeckSize + " cartas"), ("bag", "Reliquias", s.RelicCount.ToString()) };
            if (s.Turn.HasValue) tiles.Add(("hourglass", "Turno", s.Turn.Value.ToString()));
            int x0 = RowX - 8, y0 = RowY + 7 * RowH + 4;
            var w = (BarX + BarW - x0 - 8 * (tiles.Count - 1)) / tiles.Count;
            for (var k = 0; k < tiles.Count; k++)
            {
                var (iconName, label, value) = tiles[k];
                var rect = new IRect(x0 + k * (w + 8), y0, w, 60);
                PixelUi.Panel(canvas, game.Art, rect);
                var icon = game.Art.Icon(iconName);
                if (icon.Valid) canvas.BlitRect(icon, Txt.Place(icon.W * 2, icon.H * 2, Anchor.MidLeft, rect.X + 12, rect.CenterY), Rgba.White);
                Txt.Outlined(canvas, F.Get(12), label, rect.X + 46, rect.Y + 11, Dim);
                Txt.Outlined(canvas, F.Get(18), value, rect.X + 46, rect.Y + 24, Rgba.White);
                if (label == "Mazo" && s.DeckSize > 0)
                {
                    var bar = new IRect(rect.X + 46, rect.Bottom - 13, rect.W - 58, 5);
                    var cols = new Dictionary<string, Rgba> { ["Ataques"] = new Rgba(226, 70, 60), ["Habilidades"] = new Rgba(70, 140, 230), ["Poderes"] = new Rgba(190, 120, 240) };
                    var x = bar.X;
                    foreach (var (kind, n) in s.DeckByType)
                    {
                        var seg = MathX.Round(bar.W * n / (double)s.DeckSize);
                        canvas.Fill(new RectF(x, bar.Y, seg, bar.H), cols.TryGetValue(kind, out var col) ? col : new Rgba(150, 150, 150));
                        x += seg;
                    }
                    canvas.Rect(bar.Inflate(2, 2), new Rgba(10, 8, 12), 1);
                }
            }
        }
    }
}
