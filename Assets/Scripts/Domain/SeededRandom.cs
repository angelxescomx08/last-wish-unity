using System;

namespace LastWish.Domain
{
    /// <summary>
    /// The seeded <see cref="System.Random"/> algorithm (Knuth subtractive, same sequence as <c>new Random(seed)</c>)
    /// with a copyable state, so a preview of a play can use the exact random stream the real play will use.
    /// </summary>
    public sealed class SeededRandom : Random
    {
        private const int Mbig = int.MaxValue, Mseed = 161803398;
        private readonly int[] seedArray = new int[56];
        private int inext, inextp;

        public SeededRandom(int seed)
        {
            var subtraction = seed == int.MinValue ? int.MaxValue : Math.Abs(seed);
            var mj = Mseed - subtraction;
            seedArray[55] = mj;
            var mk = 1;
            for (var i = 1; i < 55; i++)
            {
                var ii = 21 * i % 55;
                seedArray[ii] = mk;
                mk = mj - mk;
                if (mk < 0) mk += Mbig;
                mj = seedArray[ii];
            }
            for (var k = 1; k < 5; k++)
                for (var i = 1; i < 56; i++)
                {
                    seedArray[i] -= seedArray[1 + (i + 30) % 55];
                    if (seedArray[i] < 0) seedArray[i] += Mbig;
                }
            inext = 0; inextp = 21;
        }

        private SeededRandom(SeededRandom other) { Array.Copy(other.seedArray, seedArray, 56); inext = other.inext; inextp = other.inextp; }

        /// <summary>An independent generator that continues with exactly the same sequence.</summary>
        public SeededRandom Clone() => new SeededRandom(this);

        private int InternalSample()
        {
            var locINext = inext; var locINextp = inextp;
            if (++locINext >= 56) locINext = 1;
            if (++locINextp >= 56) locINextp = 1;
            var retVal = seedArray[locINext] - seedArray[locINextp];
            if (retVal == Mbig) retVal--;
            if (retVal < 0) retVal += Mbig;
            seedArray[locINext] = retVal;
            inext = locINext; inextp = locINextp;
            return retVal;
        }

        protected override double Sample() => InternalSample() * (1.0 / Mbig);
        public override double NextDouble() => Sample();
        public override int Next() => InternalSample();

        public override int Next(int maxValue)
        {
            if (maxValue < 0) throw new ArgumentOutOfRangeException(nameof(maxValue));
            return (int)(Sample() * maxValue);
        }

        public override int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
            var range = (long)maxValue - minValue;
            if (range <= int.MaxValue) return (int)(Sample() * range) + minValue;
            return (int)((long)(LargeRangeSample() * range) + minValue);
        }

        private double LargeRangeSample()
        {
            var result = InternalSample();
            if (InternalSample() % 2 == 0) result = -result;
            double d = result;
            d += int.MaxValue - 1;
            d /= 2 * (uint)int.MaxValue - 1;
            return d;
        }

        public override void NextBytes(byte[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            for (var i = 0; i < buffer.Length; i++) buffer[i] = (byte)(InternalSample() % (byte.MaxValue + 1));
        }
    }
}
