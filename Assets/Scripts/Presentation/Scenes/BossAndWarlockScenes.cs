using System;
using System.Collections.Generic;
using System.Numerics;
using LastWish.Domain;

namespace LastWish.Presentation
{
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

        private static readonly Rgba ManaInk = new Rgba(110, 190, 255), DamageInk = new Rgba(255, 120, 90), BlockInk = new Rgba(100, 175, 255),
            DrawInk = new Rgba(140, 230, 120), SpecialInk = new Rgba(190, 140, 255);

        /// <summary>What an upgrade changes, one chip each (icon, Spanish text, colour): El Brujo's confirmation panel.</summary>
        public static List<(string icon, string text, Rgba color)> Changes(CardDefinition card)
        {
            var up = card.Upgrade();
            var (d0, b0, r0) = Base(card); var (d1, b1, r1) = Base(up);
            var list = new List<(string, string, Rgba)>();
            var dc = up.Cost - card.Cost;
            if (dc != 0) list.Add(("mana", card.Cost + dc <= 0 ? "Coste 0" : (dc > 0 ? "+" : "−") + Math.Abs(dc) + " de maná", ManaInk));
            if (d1 != d0) list.Add(("damage", "+" + (d1 - d0) + " de daño", DamageInk));
            if (b1 != b0) list.Add(("block", "+" + (b1 - b0) + " de escudo", BlockInk));
            if (r1 != r0) list.Add(("draw", "Roba " + (r1 - r0) + " más", DrawInk));
            if (up.ManaGain != card.ManaGain) list.Add(("mana", "+" + (up.ManaGain - card.ManaGain) + " de maná al jugarla", ManaInk));
            if (list.Count == 0) list.Add(("buff", "Mejora especial", SpecialInk));
            return list;
        }
    }
}
