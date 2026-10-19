using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LastWish.Domain
{
    public sealed class PairDefinition
    {
        public string Key { get; }
        public string Name { get; }
        public string FirstAi { get; }
        public string SecondAi { get; }
        public string Synergy { get; }
        public int MinFloor { get; }
        public PairDefinition(string key, string name, string first, string second, string synergy, int minFloor = 1)
        { Key = key; Name = name; FirstAi = first; SecondAi = second; Synergy = synergy; MinFloor = minFloor; }
    }

    public static partial class EnemyCatalog
    {
        public const double PairHpFactor = 0.8;
        public static readonly IReadOnlyList<PairDefinition> Pairs = Array.AsReadOnly(new[] {
            new PairDefinition("culto", "El Culto del Fuego", "acolito", "craneo", "El Acólito bendice al Cráneo con Fuerza mientras este no para de atacar."),
            new PairDefinition("muro", "Muro Corrosivo", "golem", "babosa", "El Gólem cubre a la Babosa con escudo mientras su ácido te corroe."),
            new PairDefinition("caceria", "Vigía y Cazador", "ojo", "murcielago", "El Ojo te deja Vulnerable y el Murciélago cae en picado sobre ti."),
            new PairDefinition("cementerio", "Cementerio Podrido", "gusano", "seta", "El Gusano te envenena y te entretiene mientras la Seta se consume."),
            new PairDefinition("tesoro", "Trampa del Tesoro", "mimico", "diablillo", "El Mímico se cubre como un cofre y el Diablillo ataca sin parar.", 2),
            new PairDefinition("procesion", "Procesión de Ceniza", "espectro", "acolito", "El Espectro te debilita mientras el Acólito lo fortalece y lo cura."),
        });
        public static readonly IReadOnlyList<string> Floor1Bosses = Array.AsReadOnly(new[] { "micelida", "tejedora", "caballero" });
        public static readonly IReadOnlyList<string> EliteOrder = Array.AsReadOnly(new[] { "verdugo", "bruja", "gargola", "minotauro", "escorpion" });
        public static IEnumerable<EnemyDefinition> Regulars => Definitions.Where(d => !d.Boss && !d.Elite);
        /// <summary>Pruebas encounter list (enemy_roster.ENCOUNTERS): each regular alone, then each pair.</summary>
        public static int EncounterCount => Regulars.Count() + Pairs.Count;

        public static EnemyDefinition Definition(string ai) => Definitions.FirstOrDefault(d => d.Ai == ai) ?? throw new ArgumentException("Unknown enemy: " + ai);
        public static int Scale(int value, int floor) => (int)Math.Min(int.MaxValue, Math.Floor(value * (1 + .15 * (Math.Max(1, floor) - 1.0))));

        public static EnemyState Create(string ai, int floor, string id, double hpFactor = 1)
        {
            var definition = Definition(ai);
            var enemy = new EnemyState(id, definition, Math.Max(1, floor), Math.Max(1, (int)(Scale(definition.BaseHp, floor) * hpFactor)));
            if (ai == "seta") enemy.Statuses.Add(StatusKind.Fuse, 3);
            if (ai == "caballero") enemy.Statuses.Add(StatusKind.Blades, 2);
            enemy.Plan(new StatusSet(), new[] { enemy }); return enemy;
        }

        public static List<EnemyState> CreatePair(string key, int floor, string prefix)
        {
            var pair = Pairs.FirstOrDefault(p => p.Key == key) ?? throw new ArgumentException("Unknown pair: " + key);
            var a = Create(pair.FirstAi, floor, prefix + "_e0", PairHpFactor);
            var b = Create(pair.SecondAi, floor, prefix + "_e1", PairHpFactor);
            a.PartnerId = b.Id; b.PartnerId = a.Id; a.PairName = b.PairName = pair.Name;
            var enemies = new List<EnemyState> { a, b };
            foreach (var enemy in enemies) { enemy.Step = 0; enemy.Plan(new StatusSet(), enemies); }
            return enemies;
        }

        public static string EncounterLabel(int index)
        {
            var regulars = Regulars.ToArray();
            if (index < 1 || index > EncounterCount) return "Al azar";
            return index <= regulars.Length ? regulars[index - 1].Name : "Pareja: " + Pairs[index - regulars.Length - 1].Name;
        }

        public static List<EnemyState> CreateEncounter(int index, int floor, string roomId)
        {
            var regulars = Regulars.ToArray();
            if (index <= regulars.Length) return new List<EnemyState> { Create(regulars[index - 1].Ai, floor, roomId + "_e0") };
            return CreatePair(Pairs[index - regulars.Length - 1].Key, floor, roomId);
        }

        public static double PairChance(int floor) => floor <= 1 ? 0.35 : floor == 2 ? 0.5 : 0.6;
        public static double TrioChance(int floor) => floor < 3 ? 0.0 : 0.25;

        /// <summary>enemy_roster.roll_encounter with the original generator.</summary>
        public static List<EnemyState> RollEncounter(PyRandom rng, int floor, string roomId, int forced = 0)
        {
            if (forced >= 1 && forced <= EncounterCount) return CreateEncounter(forced, floor, roomId);
            if (rng.Random() < PairChance(floor))
            {
                var keys = Pairs.Where(p => p.MinFloor <= floor).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
                var key = rng.Choice(keys);
                var enemies = CreatePair(key, floor, roomId);
                if (rng.Random() < TrioChance(floor))
                {
                    var pair = Pairs.First(p => p.Key == key);
                    var extra = rng.Choice(Regulars.Select(d => d.Ai).Where(a => a != pair.FirstAi && a != pair.SecondAi).OrderBy(a => a, StringComparer.Ordinal).ToList());
                    enemies.Add(Create(extra, floor, roomId + "_e2", PairHpFactor));
                }
                return enemies;
            }
            var ai = rng.Choice(Regulars.Select(d => d.Ai).OrderBy(a => a, StringComparer.Ordinal).ToList());
            return new List<EnemyState> { Create(ai, floor, roomId + "_e0") };
        }

        public static string EliteLabel(int index) => index >= 1 && index <= EliteOrder.Count ? Definition(EliteOrder[index - 1]).Name : "Al azar";
        public static string BossLabel(int index) => index >= 1 && index <= Floor1Bosses.Count ? Definition(Floor1Bosses[index - 1]).Name : "Por semilla";

        public static List<EnemyState> RollElite(PyRandom rng, int floor, string roomId, int forced = 0)
        {
            var ai = forced >= 1 && forced <= EliteOrder.Count ? EliteOrder[forced - 1] : rng.Choice(EliteOrder);
            return new List<EnemyState> { Create(ai, floor, roomId + "_elite") };
        }

        /// <summary>Later floors: the Señor de la Cripta (random intents, no pattern).</summary>
        public static EnemyState CreateCryptLord(int floor)
        {
            var hp = Scale(120, floor);
            var boss = new EnemyState("boss_f" + floor, new EnemyDefinition("crypt", "Señor de la Cripta (Piso " + floor + ")", "wraith", 120, true), floor, hp);
            boss.Intent = new EnemyIntent(IntentKind.Attack, Scale(18, floor));
            return boss;
        }
    }
}
