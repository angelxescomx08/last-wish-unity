using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>infrastructure/dev_settings.py: dev_settings.json (invalid values fall back to the defaults).</summary>
    public static class DevSettings
    {
        public const string File = "dev_settings.json";
        private static object Num(object v, object fallback, double lo, double hi, bool integer)
        {
            if (!(v is long) && !(v is double)) return fallback;
            var d = Json.Num(v);
            if (double.IsNaN(d) || double.IsInfinity(d)) return fallback;
            d = Math.Max(lo, Math.Min(hi, d));
            return integer ? (object)(int)d : d;
        }
        public static void Apply(object raw)
        {
            Tuning.Reset();
            var data = Json.Obj(raw); if (data == null) return;
            var t = Tuning.Current;
            var chances = Json.Obj(data.Get("chroma_chances"));
            if (chances != null) foreach (var pair in chances) if (pair.Value is long || pair.Value is double) t.ChromaChances[pair.Key] = (double)Num(pair.Value, 0.0, 0, 1, false);
            t.AllClassCards = data.Get("all_class_cards") is bool a && a;
            t.Invincible = data.Get("invincible") is bool b && b;
            t.StartingGold = (int)Num(data.Get("starting_gold"), t.StartingGold, 0, 1000000, true);
            t.GoldMultiplier = (double)Num(data.Get("gold_multiplier"), 1.0, 0, 100, false);
            t.ExtraMana = (int)Num(data.Get("extra_mana"), 0, 0, 20, true);
            t.ExtraDraw = (int)Num(data.Get("extra_draw"), 0, 0, 20, true);
            t.ExtraMaxHp = (int)Num(data.Get("extra_max_hp"), 0, 0, 10000, true);
            t.ExtraLuck = (int)Num(data.Get("extra_luck"), 0, 0, 1000, true);
            t.ExtraDamage = (int)Num(data.Get("extra_damage"), 0, 0, 10000, true);
            t.ExtraDexterity = (int)Num(data.Get("extra_dexterity"), 0, 0, 10000, true);
            t.ForcedBoss = (int)Num(data.Get("forced_boss"), 0, 0, 3, true);
            t.ForcedEncounter = (int)Num(data.Get("forced_encounter"), 0, 0, 99, true);
            t.BossRooms = data.Get("boss_rooms") is bool c && c;
            t.ForcedElite = (int)Num(data.Get("forced_elite"), 0, 0, 5, true);
            t.EliteRooms = data.Get("elite_rooms") is bool d && d;
            t.GachaRooms = data.Get("gacha_rooms") is bool e && e;
        }
        public static void Load(IStorage storage)
        {
            try { var text = storage?.Read(File); if (text == null) { Tuning.Reset(); return; } Apply(Json.Parse(text)); }
            catch (Exception) { Tuning.Reset(); }
        }
        public static void Save(IStorage storage)
        {
            var t = Tuning.Current;
            storage?.Write(File, Json.Write(new Dictionary<string, object> {
                { "chroma_chances", t.ChromaChances.ToDictionary(p => p.Key, p => (object)p.Value) }, { "all_class_cards", t.AllClassCards },
                { "starting_gold", t.StartingGold }, { "gold_multiplier", t.GoldMultiplier }, { "invincible", t.Invincible },
                { "extra_mana", t.ExtraMana }, { "extra_draw", t.ExtraDraw }, { "extra_max_hp", t.ExtraMaxHp }, { "extra_luck", t.ExtraLuck },
                { "extra_damage", t.ExtraDamage }, { "extra_dexterity", t.ExtraDexterity }, { "forced_boss", t.ForcedBoss },
                { "forced_encounter", t.ForcedEncounter }, { "boss_rooms", t.BossRooms }, { "forced_elite", t.ForcedElite },
                { "elite_rooms", t.EliteRooms }, { "gacha_rooms", t.GachaRooms } }));
        }
    }

    /// <summary>scenes/dev_settings_scene.DevSettingsScene ("Pruebas").</summary>
    public sealed class DevSettingsScene : Scene
    {
        private enum Kind { Percent, Number, Toggle, Action }
        private sealed class Row
        {
            public string Label; public Kind Kind; public string Attr = ""; public double Step = 1, Lo, Hi; public string Fmt = "{}";
            public string ChromaKind = ""; public int Column; public string[] Labels;
        }
        private static readonly Rgba Bg = new Rgba(12, 16, 22), On = new Rgba(80, 200, 80), Off = new Rgba(160, 100, 100);
        private const int RowW = 600, RowH = 38, RowGap = 44, ColGap = 20, Top = 150, BigStep = 10;
        private static readonly string[] SectionTitles = { "Probabilidades y partida", "Stats del héroe" };
        private static readonly Dictionary<string, string> KindLabel = new Dictionary<string, string> { { "card", "cartas" }, { "relic", "reliquias" }, { "pack", "sobres" } };
        private readonly List<Row> rows;
        private int selected;
        private readonly List<RectF> rowRects = new List<RectF>();
        private readonly List<(RectF minus, RectF plus)?> buttons = new List<(RectF, RectF)?>();
        public bool Cleared { get; set; }

        public DevSettingsScene(GameContext game) : base(game) { rows = BuildRows(); }

        private static List<Row> BuildRows()
        {
            var bosses = new[] { "Al azar" }.Concat(EnemyCatalog.Floor1Bosses.Select(a => EnemyCatalog.Definition(a).Name)).ToArray();
            var encounters = Enumerable.Range(0, EnemyCatalog.EncounterCount + 1).Select(EnemyCatalog.EncounterLabel).ToArray();
            var elites = Enumerable.Range(0, EnemyCatalog.EliteOrder.Count + 1).Select(EnemyCatalog.EliteLabel).ToArray();
            var list = new List<Row>();
            foreach (var kind in Tuning.ChromaKinds)
                list.Add(new Row { Label = "Probabilidad " + KindLabel[kind] + " " + (kind == "pack" ? "dorado" : "dorada") + "s", Kind = Kind.Percent, ChromaKind = kind });
            list.AddRange(new[] {
                new Row { Label = "Cartas de todas las clases", Kind = Kind.Toggle, Attr = "all_class_cards" },
                new Row { Label = "Oro inicial (nueva partida)", Kind = Kind.Number, Attr = "starting_gold", Step = 250, Lo = 0, Hi = 99750 },
                new Row { Label = "Multiplicador de oro", Kind = Kind.Number, Attr = "gold_multiplier", Step = 0.5, Lo = 0, Hi = 20, Fmt = "x{g}" },
                new Row { Label = "Invencible", Kind = Kind.Toggle, Attr = "invincible" },
                new Row { Label = "Suerte extra", Kind = Kind.Number, Attr = "extra_luck", Step = 1, Lo = 0, Hi = 200, Fmt = "+{}", Column = 1 },
                new Row { Label = "Daño extra", Kind = Kind.Number, Attr = "extra_damage", Step = 1, Lo = 0, Hi = 999, Fmt = "+{}", Column = 1 },
                new Row { Label = "Destreza extra", Kind = Kind.Number, Attr = "extra_dexterity", Step = 1, Lo = 0, Hi = 999, Fmt = "+{}", Column = 1 },
                new Row { Label = "HP máximo extra", Kind = Kind.Number, Attr = "extra_max_hp", Step = 25, Lo = 0, Hi = 1000, Fmt = "+{}", Column = 1 },
                new Row { Label = "Maná extra por combate", Kind = Kind.Number, Attr = "extra_mana", Step = 1, Lo = 0, Hi = 10, Fmt = "+{}", Column = 1 },
                new Row { Label = "Cartas extra por turno", Kind = Kind.Number, Attr = "extra_draw", Step = 1, Lo = 0, Hi = 10, Fmt = "+{}", Column = 1 },
                new Row { Label = "Jefe del piso 1", Kind = Kind.Number, Attr = "forced_boss", Step = 1, Lo = 0, Hi = bosses.Length - 1, Column = 1, Labels = bosses },
                new Row { Label = "Enemigos de los combates", Kind = Kind.Number, Attr = "forced_encounter", Step = 1, Lo = 0, Hi = encounters.Length - 1, Column = 1, Labels = encounters },
                new Row { Label = "Élite de las salas élite", Kind = Kind.Number, Attr = "forced_elite", Step = 1, Lo = 0, Hi = elites.Length - 1, Column = 1, Labels = elites },
                new Row { Label = "Todas las salas: el jefe", Kind = Kind.Toggle, Attr = "boss_rooms", Column = 1 },
                new Row { Label = "Todas las salas: élite", Kind = Kind.Toggle, Attr = "elite_rooms", Column = 1 },
                new Row { Label = "Todas las salas: gachapón", Kind = Kind.Toggle, Attr = "gacha_rooms", Column = 1 },
                new Row { Label = "Restablecer valores", Kind = Kind.Action, Attr = "reset" },
                new Row { Label = "Volver", Kind = Kind.Action, Attr = "back" },
            });
            return list;
        }

        private static bool GetBool(string attr)
        {
            var t = Tuning.Current;
            switch (attr)
            {
                case "all_class_cards": return t.AllClassCards; case "invincible": return t.Invincible; case "boss_rooms": return t.BossRooms;
                case "elite_rooms": return t.EliteRooms; case "gacha_rooms": return t.GachaRooms; default: return false;
            }
        }
        private static void SetBool(string attr, bool v)
        {
            var t = Tuning.Current;
            switch (attr)
            {
                case "all_class_cards": t.AllClassCards = v; break; case "invincible": t.Invincible = v; break; case "boss_rooms": t.BossRooms = v; break;
                case "elite_rooms": t.EliteRooms = v; break; case "gacha_rooms": t.GachaRooms = v; break;
            }
        }
        private static double GetNumber(string attr)
        {
            var t = Tuning.Current;
            switch (attr)
            {
                case "starting_gold": return t.StartingGold; case "gold_multiplier": return t.GoldMultiplier; case "extra_luck": return t.ExtraLuck;
                case "extra_damage": return t.ExtraDamage; case "extra_dexterity": return t.ExtraDexterity; case "extra_max_hp": return t.ExtraMaxHp;
                case "extra_mana": return t.ExtraMana; case "extra_draw": return t.ExtraDraw; case "forced_boss": return t.ForcedBoss;
                case "forced_encounter": return t.ForcedEncounter; case "forced_elite": return t.ForcedElite; default: return 0;
            }
        }
        private static void SetNumber(string attr, double v)
        {
            var t = Tuning.Current; var i = (int)v;
            switch (attr)
            {
                case "starting_gold": t.StartingGold = i; break; case "gold_multiplier": t.GoldMultiplier = v; break; case "extra_luck": t.ExtraLuck = i; break;
                case "extra_damage": t.ExtraDamage = i; break; case "extra_dexterity": t.ExtraDexterity = i; break; case "extra_max_hp": t.ExtraMaxHp = i; break;
                case "extra_mana": t.ExtraMana = i; break; case "extra_draw": t.ExtraDraw = i; break; case "forced_boss": t.ForcedBoss = i; break;
                case "forced_encounter": t.ForcedEncounter = i; break; case "forced_elite": t.ForcedElite = i; break;
            }
        }

        private static string FormatG(double v) => v.ToString("0.############", CultureInfo.InvariantCulture);

        private string ValueText(Row row)
        {
            switch (row.Kind)
            {
                case Kind.Percent: return MathX.Round(Tuning.ChromaChance(ChromaKind.Golden, row.ChromaKind) * 100) + " %";
                case Kind.Toggle: return GetBool(row.Attr) ? "Sí" : "No";
                case Kind.Number:
                    if (row.Labels != null) { var v = (int)GetNumber(row.Attr); return v >= 0 && v < row.Labels.Length ? row.Labels[v] : v.ToString(); }
                    var value = GetNumber(row.Attr);
                    var text = row.Fmt.Contains("{g}") ? FormatG(value) : (row.Attr == "gold_multiplier" ? FormatG(value) : ((int)value).ToString());
                    return row.Fmt.Replace("{g}", text).Replace("{}", text);
                default: return "";
            }
        }

        private void Adjust(int index, int direction)
        {
            var row = rows[index];
            if (row.Kind == Kind.Percent)
            {
                var current = MathX.Round(Tuning.ChromaChance(ChromaKind.Golden, row.ChromaKind) * 100);
                var value = Math.Max(0, Math.Min(100, MathX.Round((current + 5 * direction) / 5.0) * 5));
                Tuning.Current.ChromaChances[Tuning.ChromaKey(ChromaKind.Golden, row.ChromaKind)] = value / 100.0;
            }
            else if (row.Kind == Kind.Number) SetNumber(row.Attr, Math.Max(row.Lo, Math.Min(row.Hi, GetNumber(row.Attr) + row.Step * direction)));
            else if (row.Kind == Kind.Toggle) SetBool(row.Attr, !GetBool(row.Attr));
            else return;
            Sound.PlayNav();
        }

        private void Activate(int index)
        {
            var row = rows[index];
            if (row.Kind == Kind.Toggle) Adjust(index, 1);
            else if (row.Kind == Kind.Action && row.Attr == "reset") { Tuning.Reset(); Sound.PlayConfirm(); }
            else if (row.Kind == Kind.Action && row.Attr == "back") Leave();
        }

        private void Leave() { if (!Cleared) { Cleared = true; Sound.PlayCancel(); } }

        public override void HandleEvent(in InputEvent e)
        {
            if (Cleared) return;
            if (e.Type == EventType.KeyDown)
            {
                var step = e.Shift ? BigStep : 1;
                if (e.Key == Key.Up) Select(selected - 1);
                else if (e.Key == Key.Down) Select(selected + 1);
                else if (e.Key == Key.Left) Adjust(selected, -step);
                else if (e.Key == Key.Right) Adjust(selected, step);
                else if (e.IsConfirmKey) Activate(selected);
                else if (e.Key == Key.Escape) Leave();
            }
            else if (e.Type == EventType.MouseMove) { for (var i = 0; i < rowRects.Count; i++) if (rowRects[i].Contains(e.Pos) && i != selected) Select(i); }
            else if (e.IsLeftDown) Click(e.Pos, e.Shift);
        }

        private void Select(int index)
        {
            index = (index % rows.Count + rows.Count) % rows.Count;
            if (index != selected) { selected = index; Sound.PlayNav(); }
        }

        private void Click((float x, float y) pos, bool shift)
        {
            for (var i = 0; i < rowRects.Count; i++)
            {
                if (!rowRects[i].Contains(pos)) continue;
                selected = i;
                var b = i < buttons.Count ? buttons[i] : null;
                if (b.HasValue)
                {
                    var step = shift || Game.Input.Shift ? BigStep : 1;
                    if (b.Value.minus.Contains(pos)) { Adjust(i, -step); return; }
                    if (b.Value.plus.Contains(pos)) { Adjust(i, step); return; }
                }
                Activate(i); return;
            }
        }

        private static IEnumerable<string> LuckPreviewLines()
        {
            foreach (var kind in Characters.All)
            {
                var hero = HeroDefinition.OriginalFor(kind);
                var luck = Tuning.HeroLuck(hero.Luck);
                var weights = Enumerable.Range(1, 5).Select(t => RunState.RarityWeight(t, luck)).ToArray(); var total = weights.Sum();
                var boost = RunState.LuckChromaMultiplier(luck);
                var cardG = Math.Min(1.0, Tuning.ChromaChance(ChromaKind.Golden, "card") * boost);
                var relicG = Math.Min(1.0, Tuning.ChromaChance(ChromaKind.Golden, "relic") * boost);
                yield return hero.Name + " · suerte " + luck;
                yield return "    Legendaria " + Pct1(weights[4] / total) + " %  ·  Épica " + Pct1(weights[3] / total) + " %  ·  Rara " + Pct1(weights[2] / total)
                    + " %  ·  Dorada: carta " + MathX.Round(cardG * 100) + " %, reliquia " + MathX.Round(relicG * 100) + " %";
            }
        }
        private static string Pct1(double v) => (v * 100).ToString("0.0", CultureInfo.InvariantCulture);

        public override void Draw(Canvas canvas)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), Bg);
            var cx = Canvas.Width / 2;
            Txt.DrawAt(canvas, Fonts.Get(34), "Pruebas", Anchor.Center, cx, 48, Colors.TextAccent);
            Txt.DrawAt(canvas, Fonts.Get(13), "Ajustes para probar el juego. El oro inicial y el HP aplican al empezar partida; los stats, en el próximo combate.", Anchor.Center, cx, 84, Colors.TextSecondary);
            var leftX = cx - RowW - ColGap / 2;
            var colX = new[] { leftX, leftX + RowW + ColGap };
            for (var col = 0; col < 2; col++) Txt.DrawAt(canvas, Fonts.Get(18), SectionTitles[col], Anchor.MidLeft, colX[col] + 4, Top - 22, Colors.TextAccent);
            rowRects.Clear(); buttons.Clear();
            var slots = new[] { 0, 0 };
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var rect = new RectF(colX[row.Column], Top + slots[row.Column] * RowGap, RowW, RowH);
                slots[row.Column]++;
                rowRects.Add(rect);
                var sel = i == selected;
                canvas.Rect(rect, sel ? new Rgba(34, 30, 22) : Colors.BgPanel, 0, 6);
                canvas.Rect(rect, sel ? Colors.TextAccent : Colors.PanelBorder, 1, 6);
                var labelColor = sel ? Colors.TextAccent : Colors.TextPrimary;
                if (row.Kind == Kind.Action) { Txt.DrawAt(canvas, Fonts.Get(16), row.Label, Anchor.Center, rect.CenterX, rect.CenterY, labelColor); buttons.Add(null); continue; }
                Txt.DrawAt(canvas, Fonts.Get(16), row.Label, Anchor.MidLeft, rect.Left + 16, rect.CenterY, labelColor);
                var value = ValueText(row);
                var color = row.Kind == Kind.Toggle ? (GetBool(row.Attr) ? On : Off) : Colors.TextPrimary;
                var wide = row.Labels != null ? 80 : 0;
                var minus = new RectF(rect.Right - 212 - wide, rect.Top + 5, 28, 28); var plus = new RectF(rect.Right - 44, rect.Top + 5, 28, 28);
                buttons.Add((minus, plus));
                foreach (var (button, sign) in new[] { (minus, "-"), (plus, "+") })
                {
                    canvas.Rect(button, Colors.BgDark, 0, 5);
                    Txt.DrawAt(canvas, Fonts.Get(18), sign, Anchor.Center, button.CenterX, button.CenterY, Colors.TextPrimary);
                }
                var room = plus.Left - minus.Right - 8;
                var font = Fonts.Get(16);
                foreach (var size in new[] { 14, 12 }) { if (font.Width(value) <= room) break; font = Fonts.Get(size); }
                Txt.DrawAt(canvas, font, value, Anchor.Center, MathX.FloorDiv((int)(minus.Right + plus.Left), 2), rect.CenterY, color);
            }
            var pc = slots[0] < slots[1] ? 0 : 1;
            var y = Top + slots[pc] * RowGap + 8;
            Fonts.Get(13).Draw(canvas, "Con esta suerte (personaje + extra):", colX[pc] + 4, y, Colors.TextSecondary);
            var k = 0;
            foreach (var line in LuckPreviewLines())
            { Fonts.Get(11).Draw(canvas, line, colX[pc] + 4, y + 22 + k * 17, k % 2 == 0 ? Colors.TextPrimary : Colors.TextSecondary); k++; }
            Txt.DrawAt(canvas, Fonts.Get(12), "↑ ↓ navegar  |  ← → ajustar (Shift: x10)  |  ENTER activar  |  ESC volver  ·  se guarda al salir", Anchor.Center, cx, 696, Colors.TextSecondary);
        }

        public override bool ShowGoldHud => false;
    }
}
