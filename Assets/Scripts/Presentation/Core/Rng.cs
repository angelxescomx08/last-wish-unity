using System;

namespace LastWish.Presentation
{
    /// <summary>Fast seeded generator for visuals (the Python game used random.Random for effects).</summary>
    public sealed class Rng
    {
        private ulong state;
        public Rng(int seed) { state = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL; if (state == 0) state = 1; }
        public ulong NextULong()
        {
            state ^= state << 13; state ^= state >> 7; state ^= state << 17; return state;
        }
        /// <summary>random.random(): [0, 1).</summary>
        public double Random() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);
        public double Uniform(double a, double b) => a + (b - a) * Random();
        public float UniformF(float a, float b) => (float)(a + (b - a) * Random());
        public int RandInt(int a, int b) => a + (int)(Random() * (b - a + 1));
        public T Choice<T>(System.Collections.Generic.IReadOnlyList<T> items) => items[(int)(Random() * items.Count)];
        public static readonly Rng Shared = new Rng(Environment.TickCount);
    }

    public static class MathX
    {
        public const double Tau = Math.PI * 2;
        public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        public static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;
        public static float EaseOutCubic(float t) { t = Clamp01(t); return 1 - (1 - t) * (1 - t) * (1 - t); }
        public static double EaseOutCubic(double t) { t = Clamp01(t); return 1 - Math.Pow(1 - t, 3); }
        public static double EaseInOut(double t) { t = Clamp01(t); return t * t * (3 - 2 * t); }
        /// <summary>Python's int() truncation toward zero.</summary>
        public static int Trunc(double v) => (int)v;
        /// <summary>Python's round() (half to even).</summary>
        public static int Round(double v) => (int)Math.Round(v, MidpointRounding.ToEven);
        /// <summary>Python floor division for ints.</summary>
        public static int FloorDiv(int a, int b) => (int)Math.Floor(a / (double)b);
        public static double PyMod(double a, double b) { var r = a % b; return r < 0 ? r + b : r; }
    }
}
