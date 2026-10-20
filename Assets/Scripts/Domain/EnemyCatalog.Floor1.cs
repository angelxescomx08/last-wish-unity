using System;
using System.Collections.Generic;
using System.Linq;

namespace LastWish.Domain
{
    /// <summary>
    /// The floor-1 expansion (hand-written; the generated catalog is never touched): 17 enemies, 6 groups,
    /// 3 boss fights (one of them two linked bosses) and the roster-aware rolls and Pruebas lists.
    /// </summary>
    public static partial class EnemyCatalog
    {
        private static IReadOnlyList<EnemyDefinition> expansionDefinitions;
        private static Dictionary<string, EnemyProfile> profiles;
        private static IReadOnlyList<EncounterGroup> groups;
        private static IReadOnlyList<BossEncounter> bossEncounters;

        /// <summary>The 17 new enemies: 7 normals, 3 minions, 3 elites and 4 bosses.</summary>
        public static IReadOnlyList<EnemyDefinition> ExpansionDefinitions => expansionDefinitions ??= Array.AsReadOnly(new[] {
            new EnemyDefinition("madre_larva", "Madre Larva", "broodmother", 44),
            new EnemyDefinition("erizo", "Erizo de Hueso", "bonehog", 40),
            new EnemyDefinition("sapo", "Sapo Glotón", "toad", 50),
            new EnemyDefinition("cuervo", "Cuervo Carroñero", "crow", 32),
            new EnemyDefinition("rata", "Rata de Cripta", "rat", 20),
            new EnemyDefinition("escarabajo", "Escarabajo Acorazado", "beetle", 36),
            new EnemyDefinition("cieno", "Cieno Mayor", "ooze", 54),
            new EnemyDefinition("larva", "Larva", "larva", 9),
            new EnemyDefinition("cieno_menor", "Cieno Menor", "ooze_small", 14),
            new EnemyDefinition("vibora", "Víbora", "viper", 12),
            new EnemyDefinition("rey_rata", "Rey Rata", "ratking", 84, false, true),
            new EnemyDefinition("coloso", "Coloso de la Forja", "colossus", 96, false, true),
            new EnemyDefinition("duelista", "Duelista Espectral", "duelist", 80, false, true),
            new EnemyDefinition("inquisidor", "El Gran Inquisidor", "inquisitor", 144, true),
            new EnemyDefinition("sierpe", "La Sierpe Mudapiel", "serpent", 132, true),
            new EnemyDefinition("sepulturero", "El Sepulturero", "gravedigger", 84, true),
            new EnemyDefinition("planidera", "La Plañidera", "mourner", 64, true),
        });

        /// <summary>The 19 original definitions first, then the expansion.</summary>
        public static IEnumerable<EnemyDefinition> AllDefinitions => Definitions.Concat(ExpansionDefinitions);

        /// <summary>Spanish names of the expansion statuses (no collisions with cards, relics, identities or intents).</summary>
        public static readonly IReadOnlyDictionary<StatusKind, string> ExpansionStatusNames = new Dictionary<StatusKind, string> {
            [StatusKind.Thorns] = "Pinchos", [StatusKind.Shell] = "Blindaje", [StatusKind.Carrion] = "Carroña", [StatusKind.Swallowed] = "Buche",
            [StatusKind.Focus] = "Carga", [StatusKind.Parry] = "Desvío", [StatusKind.Vigil] = "Vigilancia",
        };
        /// <summary>The only two glossary terms the expansion adds (they are highlighted in texts).</summary>
        public static readonly IReadOnlyList<string> ExpansionTermNames = Array.AsReadOnly(new[] { "Aturdido", "Esbirro" });

        private static StatusAmount[] S(params (StatusKind k, int n)[] list) => list.Select(p => new StatusAmount(p.k, p.n)).ToArray();
        private static string[] L(params string[] a) => a;

        private static Dictionary<string, EnemyProfile> BuildProfiles()
        {
            var list = new[] {
                new EnemyProfile { Ai = "madre_larva", Tier = EnemyTier.Normal, SoloPool = true, Identity = "Invocadora", Title = "Pone Larvas sin parar; si no caben más, fortalece y cura a su camada.",
                    Mechanics = L("Invoca Larvas (nunca hay más de 3 enemigos en pantalla; como mucho 4 por combate).", "Las Larvas no dan oro."),
                    Summons = L("larva"), SummonBudget = 4, Moves = L("lay", "feed", "spit") },
                new EnemyProfile { Ai = "erizo", Tier = EnemyTier.Normal, SoloPool = true, Identity = "Pinchos", Title = "Cada carta tuya que lo dañe te devuelve daño. Más cartas, más pinchazos.",
                    Mechanics = L("Pinchos: cada carta tuya que le haga daño te hace tanto daño como Pinchos tenga (una vez por carta)."),
                    StartStatuses = S((StatusKind.Thorns, 2)), Moves = L("roll", "bristle", "quills") },
                new EnemyProfile { Ai = "sapo", Tier = EnemyTier.Normal, SoloPool = true, Identity = "Glotón", Title = "Se traga cartas de tu descarte; las recuperas al matarlo.",
                    Mechanics = L("Buche: las cartas que se traga vuelven a tu descarte cuando muere."), Moves = L("tongue", "swallow", "belly") },
                new EnemyProfile { Ai = "cuervo", Tier = EnemyTier.Normal, SoloPool = true, Identity = "Carroña", Title = "Cada vez que muere otro enemigo gana Fuerza y se cura. Mátalo primero.",
                    Mechanics = L("Carroña: cuando muere otro enemigo gana 2 de Fuerza y se cura 6 (los esbirros invocados no lo alimentan)."),
                    StartStatuses = S((StatusKind.Carrion, 2)), Moves = L("peck", "caw", "gouge") },
                new EnemyProfile { Ai = "rata", Tier = EnemyTier.Normal, SoloPool = false, Identity = "Manada", Title = "Muerde más fuerte cuantas más ratas siguen vivas.",
                    Mechanics = L("Manada: +1 de daño por cada otra Rata viva."), Moves = L("pack_bite", "squeak") },
                new EnemyProfile { Ai = "escarabajo", Tier = EnemyTier.Normal, SoloPool = true, Identity = "Blindaje", Title = "Conserva su escudo entre turnos y embiste con él. Rómpelo de golpe.",
                    Mechanics = L("Blindaje: al empezar su turno conserva hasta 12 de escudo en lugar de perderlo."),
                    StartStatuses = S((StatusKind.Shell, 12)), Moves = L("curl", "horn", "ram") },
                new EnemyProfile { Ai = "cieno", Tier = EnemyTier.Normal, SoloPool = true, Identity = "División", Title = "Al bajar a la mitad de vida se parte en dos Cienos Menores.",
                    Mechanics = L("División: con la mitad de vida o menos se parte en dos Cienos Menores con la mitad de su vida cada uno (sí dan oro)."),
                    Spawns = L("cieno_menor"), Moves = L("engulf", "sticky", "gel", "split") },
                new EnemyProfile { Ai = "larva", Tier = EnemyTier.Minion, Identity = "Camada", Title = "Débil, pero gana Fuerza si la dejas vivir.",
                    Mechanics = L("Esbirro invocado: no da oro."), Moves = L("gnaw", "harden") },
                new EnemyProfile { Ai = "cieno_menor", Tier = EnemyTier.Minion, Identity = "Fragmento", Title = "Un pedazo de Cieno: poca vida, pero muerde.",
                    Mechanics = L("Fragmento del Cieno Mayor: sí da oro."), Moves = L("nibble", "glob") },
                new EnemyProfile { Ai = "vibora", Tier = EnemyTier.Minion, Identity = "Nidada", Title = "Cría de la Sierpe: envenena con cada mordisco.",
                    Mechanics = L("Esbirro invocado: no da oro."), Moves = L("venom_fang", "lurk") },
                new EnemyProfile { Ai = "rey_rata", Tier = EnemyTier.Elite, HasPhase = true, Identity = "Plaga", Title = "Llama a su manada de ratas y las fortalece. Sin ratas, es mucho más débil.",
                    Mechanics = L("Invoca Ratas de Cripta (máximo 3 enemigos en pantalla; como mucho 5 por combate); las ratas invocadas no dan oro.", "Con la mitad de vida: Furia de la Plaga (+3 de Fuerza y otra Rata)."),
                    Summons = L("rata"), SummonBudget = 5, Moves = L("call_swarm", "rally", "royal_bite", "court_feast", "tail_whip", "plague_fury") },
                new EnemyProfile { Ai = "coloso", Tier = EnemyTier.Elite, HasPhase = true, Identity = "Golpe cargado", Title = "Carga un Martillo Pilón enorme: quítale vida mientras se concentra y quedará aturdido.",
                    Mechanics = L("Carga: tras Avivar la Caldera, si le quitas tanta vida como su Carga antes de que actúe, queda Aturdido y pierde el Martillo Pilón.", "Con la mitad de vida: Sobrecalentar (Carga 20, Martillo 28)."),
                    Moves = L("iron_step", "stoke", "pile_driver", "stunned", "overheat") },
                new EnemyProfile { Ai = "duelista", Tier = EnemyTier.Elite, HasPhase = true, Riposte = 6, Identity = "Desvío", Title = "Desvía el primer golpe tras ponerse en guardia y te responde. Abre con golpes pequeños.",
                    Mechanics = L("Desvío: anula por completo el próximo golpe que recibe; si fue durante una de tus cartas, al terminarla te responde con 6 de daño (Réplica).", "Con la mitad de vida: Segundo Aliento (2 Desvío y 2 de Fuerza)."),
                    StartStatuses = S((StatusKind.Parry, 1)), Moves = L("en_garde", "lunge", "feint", "fleche", "second_wind") },
                new EnemyProfile { Ai = "inquisidor", Tier = EnemyTier.Boss, HasPhase = true, Title = "Castiga jugar muchas cartas, confisca tu Fuerza y te arranca el escudo.",
                    Mechanics = L("Vigilancia: desde tu 4.ª carta de cada turno, cada carta que juegues le da 1 de Fuerza.", "Con la mitad de vida: Auto de Fe (castiga desde tu 3.ª carta)."),
                    StartStatuses = S((StatusKind.Vigil, 3)), Moves = L("edict", "confiscate", "verdict", "judgment", "expiation", "auto_de_fe", "pyre") },
                new EnemyProfile { Ai = "sierpe", Tier = EnemyTier.Boss, HasPhase = true, Title = "Invoca víboras y muda la piel para librarse de tu veneno y tus elementos.",
                    Mechanics = L("Muda: se quita todos sus perjuicios y elementos y los convierte en escudo.", "Invoca Víboras (no dan oro; como mucho 4 por combate).", "Con la mitad de vida: Piel Nueva (se libra de todo y se cura)."),
                    Summons = L("vibora"), SummonBudget = 4, Moves = L("brood", "hiss", "fangs", "constrict", "molt", "new_skin", "brood_frenzy", "death_bite", "tail") },
                new EnemyProfile { Ai = "sepulturero", Tier = EnemyTier.Boss, Title = "Protege a su hermana y la desentierra si cae.",
                    Mechanics = L("Si cae La Plañidera, cava su tumba y al turno siguiente la desentierra con la mitad de su vida (una vez por combate). Quítale 16 de vida mientras cava (Carga) para impedirlo."),
                    Spawns = L("planidera"), Moves = L("dig_in", "shovel", "requiem", "dig_grave", "exhume", "stunned", "grief", "shovel_heavy", "slab") },
                new EnemyProfile { Ai = "planidera", Tier = EnemyTier.Boss, Title = "Debilita al héroe y fortalece a su hermano.",
                    Mechanics = L("Si cae El Sepulturero, absorbe su alma: gana su Fuerza más 3 y se cura."),
                    Moves = L("lament", "wail", "requiem", "soul_drain", "banshee", "curse", "last_wail") },
            };
            return list.ToDictionary(p => p.Ai);
        }

        /// <summary>Profiles of the expansion enemies by ai.</summary>
        public static IReadOnlyDictionary<string, EnemyProfile> Profiles => profiles ??= BuildProfiles();
        /// <summary>The expansion profile, or null for the original enemies.</summary>
        public static EnemyProfile Profile(string ai) => ai != null && Profiles.TryGetValue(ai, out var p) ? p : null;
        public static bool IsExpansion(string ai) => ai != null && Profiles.ContainsKey(ai);

        public static IReadOnlyList<EncounterGroup> Groups => groups ??= Array.AsReadOnly(new[] {
            new EncounterGroup { Key = "plaga", Name = "La Plaga", Members = L("rata", "rata", "rata"), Synergy = "Tres Ratas de Cripta: cuantas más quedan vivas, más fuerte muerde cada una." },
            new EncounterGroup { Key = "carrona", Name = "Festín de Carroña", Members = L("madre_larva", "cuervo"), Synergy = "La Madre Larva te entretiene con su camada; si ella cae, el Cuervo se ceba con su cuerpo." },
            new EncounterGroup { Key = "pantano", Name = "Banquete del Pantano", Members = L("sapo", "babosa"), Synergy = "La Babosa corroe tu escudo mientras el Sapo se traga tus cartas." },
            new EncounterGroup { Key = "bandada", Name = "Bandada Hambrienta", Members = L("cuervo", "murcielago"), Synergy = "El Murciélago te desangra; si cae, el Cuervo se alimenta de él." },
            new EncounterGroup { Key = "fortin", Name = "Fortín de Pinchos", Members = L("escarabajo", "erizo"), Synergy = "El Escarabajo acumula escudo y el Erizo castiga cada carta que lo daña." },
            new EncounterGroup { Key = "cripta", Name = "Cripta Infestada", Members = L("gusano", "rata", "rata"), Synergy = "El Gusano te envenena mientras las ratas muerden en manada." },
        });

        /// <summary>Floor-1 boss fights of the expanded roster: the 3 originals, then the 3 new ones.</summary>
        public static IReadOnlyList<BossEncounter> BossEncounters => bossEncounters ??= Array.AsReadOnly(new[] {
            new BossEncounter { Key = "micelida", Name = "Reina Micélida", Members = L("micelida") },
            new BossEncounter { Key = "tejedora", Name = "La Tejedora", Members = L("tejedora") },
            new BossEncounter { Key = "caballero", Name = "Caballero Hueco", Members = L("caballero") },
            new BossEncounter { Key = "inquisidor", Name = "El Gran Inquisidor", Members = L("inquisidor") },
            new BossEncounter { Key = "sierpe", Name = "La Sierpe Mudapiel", Members = L("sierpe") },
            new BossEncounter { Key = "lapida", Name = "Los Hermanos Lápida", Members = L("sepulturero", "planidera"), Linked = true, HpFactor = 1.0,
                Synergy = "El Sepulturero protege a La Plañidera y la desentierra si cae; si cae él, ella absorbe su fuerza. Mátalos juntos." },
        });

        public static EncounterGroup Group(string key) => Groups.FirstOrDefault(g => g.Key == key) ?? throw new ArgumentException("Unknown group: " + key);
        public static BossEncounter BossFight(string key) => BossEncounters.FirstOrDefault(b => b.Key == key) ?? throw new ArgumentException("Unknown boss: " + key);

        // ------------------------------------------------------------ roster lists
        private static readonly string[] NewSoloOrder = { "madre_larva", "erizo", "sapo", "cuervo", "rata", "escarabajo", "cieno" };
        private static readonly string[] NewElites = { "rey_rata", "coloso", "duelista" };

        public static IReadOnlyList<string> EliteList(EnemyRoster roster) => Check(roster) == EnemyRoster.Original ? EliteOrder : EliteOrder.Concat(NewElites).ToList();
        public static IReadOnlyList<string> BossKeys(EnemyRoster roster) => Check(roster) == EnemyRoster.Original ? Floor1Bosses : BossEncounters.Select(b => b.Key).ToList();

        private static EnemyRoster Check(EnemyRoster roster) =>
            Enum.IsDefined(typeof(EnemyRoster), roster) ? roster : throw new ArgumentOutOfRangeException(nameof(roster));

        /// <summary>Pruebas encounter list: 1-11 original regulars, 12-17 original pairs, then 18-24 new regulars and 25-30 new groups.</summary>
        public static int EncounterTotal(EnemyRoster roster) => Check(roster) == EnemyRoster.Original ? EncounterCount : EncounterCount + NewSoloOrder.Length + Groups.Count;

        public static string EncounterLabel(int index, EnemyRoster roster)
        {
            if (Check(roster) == EnemyRoster.Original || index <= EncounterCount) return EncounterLabel(index);
            if (index > EncounterTotal(roster)) return "Al azar";
            var k = index - EncounterCount - 1;
            return k < NewSoloOrder.Length ? Definition(NewSoloOrder[k]).Name : "Grupo: " + Groups[k - NewSoloOrder.Length].Name;
        }

        public static List<EnemyState> CreateEncounter(int index, int floor, string roomId, EnemyRoster roster)
        {
            if (Check(roster) == EnemyRoster.Original || index <= EncounterCount) return CreateEncounter(index, floor, roomId);
            var k = index - EncounterCount - 1;
            if (k < NewSoloOrder.Length) return new List<EnemyState> { Create(NewSoloOrder[k], floor, roomId + "_e0") };
            return CreateGroup(Groups[k - NewSoloOrder.Length].Key, floor, roomId);
        }

        public static string EliteLabel(int index, EnemyRoster roster)
        {
            var list = EliteList(roster);
            return index >= 1 && index <= list.Count ? Definition(list[index - 1]).Name : "Al azar";
        }

        public static string BossLabel(int index, EnemyRoster roster)
        {
            if (Check(roster) == EnemyRoster.Original) return BossLabel(index);
            return index >= 1 && index <= BossEncounters.Count ? BossEncounters[index - 1].Name : "Por semilla";
        }

        /// <summary>enemy_roster.roll_encounter with the roster: the expanded pool only changes floor 1 (same rng calls).</summary>
        public static List<EnemyState> RollEncounter(PyRandom rng, int floor, string roomId, int forced, EnemyRoster roster)
        {
            if (Check(roster) == EnemyRoster.Original || floor != 1) return RollEncounter(rng, floor, roomId, forced);
            if (forced >= 1 && forced <= EncounterTotal(roster)) return CreateEncounter(forced, floor, roomId, roster);
            if (rng.Random() < PairChance(floor))
            {
                var keys = Pairs.Where(p => p.MinFloor <= floor).Select(p => p.Key).Concat(Groups.Where(g => g.MinFloor <= floor && floor <= g.MaxFloor).Select(g => g.Key))
                    .OrderBy(k => k, StringComparer.Ordinal).ToList();
                var key = rng.Choice(keys);
                var enemies = Pairs.Any(p => p.Key == key) ? CreatePair(key, floor, roomId) : CreateGroup(key, floor, roomId);
                rng.Random();   // the trio roll of the original (never succeeds on floor 1)
                return enemies;
            }
            var ais = Regulars.Select(d => d.Ai).Concat(Profiles.Values.Where(p => p.SoloPool && p.Tier == EnemyTier.Normal && p.MinFloor <= floor && floor <= p.MaxFloor).Select(p => p.Ai))
                .OrderBy(a => a, StringComparer.Ordinal).ToList();
            var ai = rng.Choice(ais);
            return new List<EnemyState> { Create(ai, floor, roomId + "_e0") };
        }

        public static List<EnemyState> RollElite(PyRandom rng, int floor, string roomId, int forced, EnemyRoster roster)
        {
            if (Check(roster) == EnemyRoster.Original || floor != 1) return RollElite(rng, floor, roomId, forced);
            var list = EliteList(roster);
            var ai = forced >= 1 && forced <= list.Count ? list[forced - 1] : rng.Choice(list.ToList());
            return new List<EnemyState> { Create(ai, floor, roomId + "_elite") };
        }

        // ------------------------------------------------------------ creation
        /// <summary>The enemy with its HP and starting statuses, without an intent yet.</summary>
        public static EnemyState CreateUnplanned(string ai, int floor, string id, double hpFactor = 1, int? exactHp = null)
        {
            var definition = Definition(ai);
            var hp = exactHp.HasValue ? Math.Max(1, exactHp.Value) : Math.Max(1, (int)(Scale(definition.BaseHp, floor) * hpFactor));
            var enemy = new EnemyState(id, definition, Math.Max(1, floor), hp);
            if (ai == "seta") enemy.Statuses.Add(StatusKind.Fuse, 3);
            if (ai == "caballero") enemy.Statuses.Add(StatusKind.Blades, 2);
            var profile = Profile(ai);
            if (profile != null)
            {
                foreach (var s in profile.StartStatuses) enemy.Statuses.Add(s.Kind, s.Amount);
                enemy.Riposte = Scale(profile.Riposte, floor);
            }
            return enemy;
        }

        /// <summary>A pair or trio of the expansion: no vengeance link, re-planned with the whole group.</summary>
        public static List<EnemyState> CreateGroup(string key, int floor, string prefix)
        {
            var group = Group(key);
            var enemies = new List<EnemyState>();
            for (var i = 0; i < group.Members.Count; i++)
            {
                var e = Create(group.Members[i], floor, prefix + "_e" + i, group.HpFactor);
                e.PairName = group.Name; enemies.Add(e);
            }
            foreach (var enemy in enemies) { enemy.Step = 0; enemy.Plan(new StatusSet(), enemies); }
            return enemies;
        }

        /// <summary>A floor boss fight; the original bosses keep their id "boss_f&lt;floor&gt;".</summary>
        public static List<EnemyState> CreateBoss(string key, int floor)
        {
            var fight = BossFight(key);
            if (!fight.Linked) return new List<EnemyState> { Create(fight.Members[0], floor, "boss_f" + floor, fight.HpFactor) };
            var enemies = new List<EnemyState>();
            for (var i = 0; i < fight.Members.Count; i++)
            {
                var e = Create(fight.Members[i], floor, "boss_f" + floor + "_e" + i, fight.HpFactor);
                e.PairName = fight.Name; enemies.Add(e);
            }
            enemies[0].PartnerId = enemies[1].Id; enemies[1].PartnerId = enemies[0].Id;
            foreach (var enemy in enemies) { enemy.Step = 0; enemy.Plan(new StatusSet(), enemies); }
            return enemies;
        }

        /// <summary>A summoned, split or revived enemy; the combat plans it (step 0) after it arrives.</summary>
        internal static EnemyState CreateSummon(string ai, int floor, string id, string summonerId, double hpFactor, bool paysGold, int? exactHp = null)
        {
            var enemy = CreateUnplanned(ai, floor, id, hpFactor, exactHp);
            enemy.Summoned = true; enemy.SummonerId = summonerId; enemy.PaysGold = paysGold;
            return enemy;
        }
    }
}
