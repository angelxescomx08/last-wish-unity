using System.Numerics;

namespace LastWish.Presentation
{
    public static class Numbers
    {
        private static readonly (BigInteger threshold, string suffix)[] Thresholds = {
            (BigInteger.Pow(10, 18), "E"), (BigInteger.Pow(10, 15), "Q"), (BigInteger.Pow(10, 12), "T"),
            (BigInteger.Pow(10, 9), "B"), (BigInteger.Pow(10, 6), "M"), (BigInteger.Pow(10, 3), "K") };

        /// <summary>BigValue.format_int: "1.5K", "2.0M"… integer arithmetic only.</summary>
        public static string FormatInt(BigInteger value)
        {
            if (value < 0) return "-" + FormatInt(-value);
            foreach (var (threshold, suffix) in Thresholds)
                if (value >= threshold)
                {
                    var whole = value / threshold; var frac = value % threshold * 10 / threshold;
                    return whole + "." + frac + suffix;
                }
            return value.ToString();
        }
        public static string FormatInt(long value) => FormatInt(new BigInteger(value));

        /// <summary>gold_hud.format_gold: "1 234" below a million, then suffixes.</summary>
        public static string FormatGold(long amount)
        {
            if (System.Math.Abs(amount) >= 1000000) return FormatInt(amount);
            var s = System.Math.Abs(amount).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");
            return (amount < 0 ? "-" : "") + s;
        }
    }
}
