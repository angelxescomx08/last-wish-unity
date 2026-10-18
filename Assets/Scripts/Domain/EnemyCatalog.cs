using System;
using System.Collections.Generic;
using System.Linq;

namespace LastWish.Domain
{
    public static partial class EnemyCatalog
    {
        private static readonly string[][] Pairs = {
            new[] { "culto", "acolito", "craneo" }, new[] { "muro", "golem", "babosa" },
            new[] { "caceria", "ojo", "murcielago" }, new[] { "cementerio", "gusano", "seta" },
            new[] { "tesoro", "mimico", "diablillo" }, new[] { "procesion", "espectro", "acolito" }
        };
        public static int Scale(int value, int floor) => (int)Math.Min(int.MaxValue, Math.Floor(value * (1 + .15 * (Math.Max(1, floor) - 1.0))));
        public static EnemyState Create(string ai, int floor, string id, double hpFactor = 1)
        {
            var definition = Definitions.FirstOrDefault(d => d.Ai == ai) ?? throw new ArgumentException("Unknown enemy: " + ai);
            var enemy = new EnemyState(id, definition, Math.Max(1, floor), Math.Max(1, (int)(Scale(definition.BaseHp, floor) * hpFactor)));
            if (ai == "seta") enemy.Statuses.Add(StatusKind.Fuse, 3);
            if (ai == "caballero") enemy.Statuses.Add(StatusKind.Blades, 2);
            enemy.Plan(new StatusSet(), new[] { enemy }); return enemy;
        }
        public static EnemyState[] CreatePair(string key, int floor, string prefix)
        {
            var definition = Pairs.FirstOrDefault(p => p[0] == key) ?? throw new ArgumentException("Unknown pair: " + key);
            var a = Create(definition[1], floor, prefix + "_e0", .8);
            var b = Create(definition[2], floor, prefix + "_e1", .8);
            a.PartnerId = b.Id; b.PartnerId = a.Id;
            var enemies = new[] { a, b };
            foreach (var enemy in enemies) { enemy.Step = 0; enemy.Plan(new StatusSet(), enemies); }
            return enemies;
        }
        public static IReadOnlyList<EnemyState> Roll(int seed, int floor, string roomId, RoomKind room, int forced = 0)
        {
            var random = new Random(seed);
            if (room == RoomKind.Boss)
            {
                if (floor == 1)
                {
                    var bosses = Definitions.Where(d => d.Boss).ToArray();
                    return new[] { Create(bosses[forced >= 1 && forced <= bosses.Length ? forced - 1 : random.Next(bosses.Length)].Ai, floor, roomId + "_boss") };
                }
                var boss = new EnemyState(roomId + "_boss", new EnemyDefinition("crypt", "Señor de la Cripta (Piso " + floor + ")", "wraith", 120, true), floor, Scale(120, floor));
                boss.Intent = new EnemyIntent(IntentKind.Attack, Scale(18, floor)); return new[] { boss };
            }
            if (room == RoomKind.Elite)
            {
                var elites = Definitions.Where(d => d.Elite).ToArray();
                return new[] { Create(elites[forced >= 1 && forced <= elites.Length ? forced - 1 : random.Next(elites.Length)].Ai, floor, roomId + "_elite") };
            }
            var solo = Definitions.Where(d => !d.Boss && !d.Elite).ToArray();
            if (forced > 0 && forced <= solo.Length + Pairs.Length)
                return forced <= solo.Length ? new[] { Create(solo[forced - 1].Ai, floor, roomId + "_e0") }
                    : CreatePair(Pairs[forced - solo.Length - 1][0], floor, roomId);
            if (random.NextDouble() >= (floor <= 1 ? .35 : floor == 2 ? .5 : .6))
                return new[] { Create(solo[random.Next(solo.Length)].Ai, floor, roomId + "_e0") };
            var pairs = Pairs.Where(p => p[0] != "tesoro" || floor >= 2).OrderBy(p => p[0]).ToArray();
            var pair = pairs[random.Next(pairs.Length)];
            var result = CreatePair(pair[0], floor, roomId).ToList();
            if (floor >= 3 && random.NextDouble() < .25)
            {
                var choices = solo.Where(e => e.Ai != pair[1] && e.Ai != pair[2]).OrderBy(e => e.Ai).ToArray();
                result.Add(Create(choices[random.Next(choices.Length)].Ai, floor, roomId + "_e2", .8));
            }
            return result;
        }
    }
}
