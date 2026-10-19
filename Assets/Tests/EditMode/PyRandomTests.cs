using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using LastWish.Domain;
using NUnit.Framework;
using static LastWish.Tests.FixtureJson;

namespace LastWish.Tests
{
    public sealed class PyRandomTests
    {
        [Test] public void EveryMethodMatchesCPython()
        {
            var data = Obj(Load("py-random.json"));
            foreach (var raw in Arr(data["cases"]))
            {
                var c = Obj(raw); var seed = Str(c["seed"]);
                var r = Str(c["kind"]) == "str" ? new PyRandom(seed) : new PyRandom(BigInteger.Parse(seed, CultureInfo.InvariantCulture));
                Assert.That(Enumerable.Range(0, 5).Select(_ => (long)r.NextUInt32()), Is.EqualTo(Arr(c["u32"]).Select(Long)), seed);
                Assert.That(Enumerable.Range(0, 5).Select(_ => r.Random()), Is.EqualTo(Strs(c["random"]).Select(v => double.Parse(v, CultureInfo.InvariantCulture))), seed);
                Assert.That(Enumerable.Range(0, 8).Select(_ => r.RandInt(-3, 17)), Is.EqualTo(Ints(c["randint"])), seed);
                Assert.That(Enumerable.Range(0, 8).Select(_ => r.Choice(new[] { -1, 0, 1 })), Is.EqualTo(Ints(c["choice"])), seed);
                var items = Enumerable.Range(0, 30).ToList(); r.Shuffle(items);
                Assert.That(items, Is.EqualTo(Ints(c["shuffle"])), seed);
                Assert.That(r.Sample(Enumerable.Range(0, 12).ToList(), 3), Is.EqualTo(Ints(c["sample4"])), seed);
                Assert.That(r.Sample(Enumerable.Range(0, 40).ToList(), 7), Is.EqualTo(Ints(c["sample30"])), seed);
                var weights = new[] { 50.0, 28.0 * 1.2, 14.0, 6.0, 2.0 };
                Assert.That(Enumerable.Range(0, 10).Select(_ => r.WeightedChoice(new[] { 1, 2, 3, 4, 5 }, weights)), Is.EqualTo(Ints(c["choices"])), seed);
                Assert.That(Enumerable.Range(0, 3).Select(_ => (long)r.GetRandBits(40)), Is.EqualTo(Arr(c["bits40"]).Select(Long)), seed);
                Assert.That(Enumerable.Range(0, 3).Select(_ => r.RandBelow(1000000000000L)), Is.EqualTo(Arr(c["big"]).Select(Long)), seed);
            }
            foreach (var pair in Obj(data["crc"])) Assert.That((long)PyRandom.Crc32(pair.Key), Is.EqualTo(Long(pair.Value)), pair.Key);
        }
    }
}
