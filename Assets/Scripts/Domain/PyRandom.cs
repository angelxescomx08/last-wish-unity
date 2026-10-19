using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace LastWish.Domain
{
    /// <summary>
    /// Bit-exact port of CPython's <c>random.Random</c> (MT19937, Python 3.13 algorithms).
    /// The original game seeds every run-level roll (map, encounters, rewards, shop, gachapón)
    /// with Python's generator, so the same seed must give the same run here.
    /// </summary>
    public sealed class PyRandom
    {
        private const int N = 624, M = 397;
        private readonly uint[] mt = new uint[N];
        private int index;

        public PyRandom(long seed) : this(new BigInteger(seed)) { }
        public PyRandom(ulong seed) : this(new BigInteger(seed)) { }
        public PyRandom(BigInteger seed) { SeedInt(BigInteger.Abs(seed)); }
        /// <summary>Seeding with a str (version 2): the UTF-8 bytes followed by their SHA-512.</summary>
        public PyRandom(string seed)
        {
            var bytes = Encoding.UTF8.GetBytes(seed ?? throw new ArgumentNullException(nameof(seed)));
            byte[] digest;
            using (var sha = SHA512.Create()) digest = sha.ComputeHash(bytes);
            var all = new byte[bytes.Length + digest.Length + 1];
            // BigInteger reads little-endian two's complement: reverse the big-endian bytes and keep a zero sign byte.
            for (var i = 0; i < bytes.Length; i++) all[all.Length - 2 - i] = bytes[i];
            for (var i = 0; i < digest.Length; i++) all[digest.Length - 1 - i] = digest[i];
            SeedInt(new BigInteger(all));
        }

        private void InitGenrand(uint s)
        {
            mt[0] = s;
            for (var i = 1; i < N; i++) mt[i] = unchecked(1812433253u * (mt[i - 1] ^ (mt[i - 1] >> 30)) + (uint)i);
            index = N;
        }

        private void SeedInt(BigInteger value)
        {
            var key = new List<uint>();
            while (value > 0) { key.Add((uint)(value & uint.MaxValue)); value >>= 32; }
            if (key.Count == 0) key.Add(0);
            InitGenrand(19650218u);
            int i = 1, j = 0;
            for (var k = Math.Max(N, key.Count); k > 0; k--)
            {
                mt[i] = unchecked((mt[i] ^ ((mt[i - 1] ^ (mt[i - 1] >> 30)) * 1664525u)) + key[j] + (uint)j);
                i++; j++;
                if (i >= N) { mt[0] = mt[N - 1]; i = 1; }
                if (j >= key.Count) j = 0;
            }
            for (var k = N - 1; k > 0; k--)
            {
                mt[i] = unchecked((mt[i] ^ ((mt[i - 1] ^ (mt[i - 1] >> 30)) * 1566083941u)) - (uint)i);
                i++;
                if (i >= N) { mt[0] = mt[N - 1]; i = 1; }
            }
            mt[0] = 0x80000000u;
            index = N;
        }

        public uint NextUInt32()
        {
            if (index >= N)
            {
                int kk;
                uint y;
                for (kk = 0; kk < N - M; kk++)
                { y = (mt[kk] & 0x80000000u) | (mt[kk + 1] & 0x7fffffffu); mt[kk] = mt[kk + M] ^ (y >> 1) ^ ((y & 1u) != 0 ? 0x9908b0dfu : 0u); }
                for (; kk < N - 1; kk++)
                { y = (mt[kk] & 0x80000000u) | (mt[kk + 1] & 0x7fffffffu); mt[kk] = mt[kk + (M - N)] ^ (y >> 1) ^ ((y & 1u) != 0 ? 0x9908b0dfu : 0u); }
                y = (mt[N - 1] & 0x80000000u) | (mt[0] & 0x7fffffffu);
                mt[N - 1] = mt[M - 1] ^ (y >> 1) ^ ((y & 1u) != 0 ? 0x9908b0dfu : 0u);
                index = 0;
            }
            var r = mt[index++];
            r ^= r >> 11;
            r ^= (r << 7) & 0x9d2c5680u;
            r ^= (r << 15) & 0xefc60000u;
            r ^= r >> 18;
            return r;
        }

        /// <summary>random.random(): 53-bit float in [0, 1).</summary>
        public double Random()
        {
            uint a = NextUInt32() >> 5, b = NextUInt32() >> 6;
            return (a * 67108864.0 + b) * (1.0 / 9007199254740992.0);
        }

        public ulong GetRandBits(int k)
        {
            if (k < 0 || k > 64) throw new ArgumentOutOfRangeException(nameof(k));
            if (k == 0) return 0;
            if (k <= 32) return NextUInt32() >> (32 - k);
            ulong low = NextUInt32();
            ulong high = NextUInt32() >> (64 - k);
            return low | high << 32;
        }

        private static int BitLength(ulong n) { var bits = 0; while (n != 0) { bits++; n >>= 1; } return bits; }

        /// <summary>_randbelow(n): uniform integer in [0, n).</summary>
        public long RandBelow(long n)
        {
            if (n <= 0) return 0;
            var k = BitLength((ulong)n);
            var r = GetRandBits(k);
            while (r >= (ulong)n) r = GetRandBits(k);
            return (long)r;
        }

        public int RandBelow(int n) => (int)RandBelow((long)n);
        /// <summary>random.randint(a, b), both ends included.</summary>
        public int RandInt(int a, int b)
        {
            if (b < a) throw new ArgumentException("empty range for randint");
            return (int)(a + RandBelow((long)b - a + 1));
        }
        public int RandRange(int stop) { if (stop <= 0) throw new ArgumentException("empty range"); return RandBelow(stop); }
        public int RandRange(int start, int stop) { if (stop <= start) throw new ArgumentException("empty range"); return (int)(start + RandBelow((long)stop - start)); }
        public double Uniform(double a, double b) => a + (b - a) * Random();

        public T Choice<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0) throw new IndexOutOfRangeException("Cannot choose from an empty sequence");
            return items[RandBelow(items.Count)];
        }

        public void Shuffle<T>(IList<T> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = RandBelow(i + 1);
                var swap = items[i]; items[i] = items[j]; items[j] = swap;
            }
        }

        /// <summary>random.sample(population, k).</summary>
        public List<T> Sample<T>(IReadOnlyList<T> population, int k)
        {
            var n = population.Count;
            if (k < 0 || k > n) throw new ArgumentException("Sample larger than population or is negative");
            var result = new List<T>(k);
            var setsize = 21;
            if (k > 5) setsize += (int)Math.Pow(4, Math.Ceiling(Math.Log(k * 3) / Math.Log(4)));
            if (n <= setsize)
            {
                var pool = new List<T>(population);
                for (var i = 0; i < k; i++)
                {
                    var j = RandBelow(n - i);
                    result.Add(pool[j]);
                    pool[j] = pool[n - i - 1];
                }
            }
            else
            {
                var selected = new HashSet<int>();
                for (var i = 0; i < k; i++)
                {
                    var j = RandBelow(n);
                    while (selected.Contains(j)) j = RandBelow(n);
                    selected.Add(j); result.Add(population[j]);
                }
            }
            return result;
        }

        /// <summary>random.choices(population, weights=weights, k=1)[0] (cumulative weights + bisect_right).</summary>
        public T WeightedChoice<T>(IReadOnlyList<T> population, IReadOnlyList<double> weights)
        {
            var n = population.Count;
            if (n == 0) throw new IndexOutOfRangeException("Cannot choose from an empty population");
            if (weights.Count != n) throw new ArgumentException("The number of weights does not match the population");
            var cumulative = new double[n];
            double sum = 0;
            for (var i = 0; i < n; i++) { sum += weights[i]; cumulative[i] = sum; }
            var total = cumulative[n - 1] + 0.0;
            if (total <= 0.0) throw new ArgumentException("Total of weights must be greater than zero");
            var x = Random() * total;
            int lo = 0, hi = n - 1;
            while (lo < hi) { var mid = (lo + hi) / 2; if (x < cumulative[mid]) hi = mid; else lo = mid + 1; }
            return population[lo];
        }

        /// <summary>zlib.crc32 of the UTF-8 bytes (Python's stable string hash in the original seeds).</summary>
        public static uint Crc32(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var crc = 0xFFFFFFFFu;
            foreach (var b in bytes)
            {
                crc ^= b;
                for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
            return ~crc;
        }
    }
}
