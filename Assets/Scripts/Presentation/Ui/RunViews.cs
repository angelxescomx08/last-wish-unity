using System;
using System.Collections.Generic;
using System.Numerics;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>application/card_preview.py: the numbers a card shows outside the hand.</summary>
    public readonly struct CardBonus
    {
        public readonly int Attack, Block;
        public CardBonus(int attack, int block) { Attack = attack; Block = block; }
        public static readonly CardBonus None = new CardBonus(0, 0);
        public CardShow For(CardDefinition card) =>
            CardShow.With(CardTotals.Damage(card) > 0 ? Attack : 0, CardTotals.Block(card) > 0 ? Block : 0);
        public static CardBonus ForRun(RunState run)
        {
            if (run == null) return None;
            var hero = run.Hero;
            return new CardBonus(hero.AttackBonus + Tuning.Current.ExtraDamage + run.RelicEffect("fire_orb", 2), hero.Dexterity + Tuning.Current.ExtraDexterity);
        }
    }

    /// <summary>application/luck.py: what luck does to every drop (now vs the character's own luck).</summary>
    public sealed class LuckReport
    {
        public int Luck, Base;
        public double[] Tiers = new double[6], BaseTiers = new double[6];
        public Dictionary<string, double> Golden = new Dictionary<string, double>(), BaseGolden = new Dictionary<string, double>();
        public List<(string label, int amount)> Sources = new List<(string, int)>();
        public int Bonus => Luck - Base;

        public static double GoldenChance(string kind, int luck)
        {
            var boost = RunState.LuckChromaMultiplier(luck);
            // Only golden is droppable (diamond comes from Cartas de Diamante).
            return Math.Min(1.0, Math.Min(1.0, Tuning.ChromaChance(ChromaKind.Golden, kind) * boost));
        }
        public static double[] RarityOdds(int luck)
        {
            var odds = new double[6]; double total = 0;
            for (var r = 1; r <= 5; r++) total += RunState.RarityWeight(r, luck);
            for (var r = 1; r <= 5; r++) odds[r] = total > 0 ? RunState.RarityWeight(r, luck) / total : 0;
            return odds;
        }
        public double RareOrBetter(bool useBase = false) { var t = useBase ? BaseTiers : Tiers; return t[3] + t[4] + t[5]; }
        public double ExtraCard => RunState.LuckyCardChances(Luck).first;
        public double SecondExtraCard => RunState.LuckyCardChances(Luck).second;

        public static LuckReport For(RunState run)
        {
            var r = new LuckReport { Luck = run.Luck, Base = Tuning.HeroLuck(run.Hero.Luck) };
            r.Tiers = RarityOdds(r.Luck); r.BaseTiers = RarityOdds(r.Base);
            foreach (var k in Tuning.ChromaKinds) { r.Golden[k] = GoldenChance(k, r.Luck); r.BaseGolden[k] = GoldenChance(k, r.Base); }
            foreach (var relic in run.OwnedRelics)
            {
                var tag = relic.Definition.Tag; var amount = tag == "seven_leaf_clover" ? 100 : tag == "silver_horseshoe" ? 30 : tag == "rabbit_foot" ? 15 : 0;
                if (amount > 0 && relic.Active) r.Sources.Add((relic.Definition.Name, amount * relic.Multiplier));
            }
            return r;
        }

        /// <summary>Python's "{x:.0%}" (round half to even).</summary>
        public static string Pct(double x) => MathX.Round(x * 100) + "%";
        public string Summary() => "Suerte " + Luck + ": Rara o mejor " + Pct(RareOrBetter()) + " · dorada " + Pct(Golden["card"]);
    }

    /// <summary>ui/luck_badge.py</summary>
    public static class LuckBadge
    {
        public static readonly Rgba Lucky = new Rgba(140, 240, 120), Plain = new Rgba(236, 226, 204), DimInk = new Rgba(188, 180, 164);
        public static (string title, string detail) Line(LuckReport report) =>
            ("Suerte " + report.Luck + (report.Bonus > 0 ? " ▲" : ""),
             "Rara o mejor " + LuckReport.Pct(report.RareOrBetter()) + " · dorada " + LuckReport.Pct(report.Golden["card"]) + " · carta extra " + LuckReport.Pct(report.ExtraCard));

        public static RectF Draw(Canvas canvas, GameContext g, Anchor anchor, float x, float y, LuckReport report)
        {
            var (title, detail) = Line(report);
            var tf = g.Fonts.Get(15); var df = g.Fonts.Get(12);
            var (tw, _) = Txt.OutlinedSize(tf, title); var (dw, _) = Txt.OutlinedSize(df, detail);
            var icon = g.Art.Icon("clover");
            var iconW = icon.Valid ? icon.W * 2 + 6 : 0;
            var w = iconW + Math.Max(tw, dw) + 34;
            var rect = Txt.Place(w, 50, anchor, x, y);
            PixelUi.Panel(canvas, g.Art, rect);
            var px = rect.X + 14;
            if (icon.Valid) { canvas.BlitRect(icon, new RectF(px, rect.Y + 25 - icon.H, icon.W * 2, icon.H * 2), Rgba.White); px += iconW; }
            Txt.Outlined(canvas, tf, title, px, rect.Y + 7, report.Bonus > 0 ? Lucky : Plain);
            Txt.Outlined(canvas, df, detail, px, rect.Y + 27, DimInk);
            return rect;
        }
    }
}
