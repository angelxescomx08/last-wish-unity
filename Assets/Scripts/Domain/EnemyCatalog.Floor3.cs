using System;
using System.Collections.Generic;
using System.Linq;

namespace LastWish.Domain
{
    /// <summary>
    /// The floor-3 expansion (hand-written): 15 enemies (6 normals, 3 minions, 3 elites, 3 bosses), 6 groups and 3 boss
    /// fights that replace the Señor de la Cripta on floor 3. Only <see cref="EnemyRoster.Expanded"/> rolls them, and only on floor 3.
    /// </summary>
    public static partial class EnemyCatalog
    {
        /// <summary>The last floor with expansion content (floors above roll the original enemies, like the Python game).</summary>
        public const int LastExpandedFloor = 3;

        private static IReadOnlyList<EnemyDefinition> floor3Definitions;
        private static IReadOnlyList<EncounterGroup> floor3Groups;
        private static IReadOnlyList<BossEncounter> floor3BossEncounters;

        /// <summary>The 15 floor-3 enemies: 6 normals, 3 minions, 3 elites and 3 bosses.</summary>
        public static IReadOnlyList<EnemyDefinition> Floor3Definitions => floor3Definitions ??= Array.AsReadOnly(new[] {
            new EnemyDefinition("abanderado", "Abanderado Helado", "bannerman", 46),
            new EnemyDefinition("barbaro", "Bárbaro de la Ventisca", "barbarian", 52),
            new EnemyDefinition("arcanista", "Arcanista del Vacío", "voidmage", 40),
            new EnemyDefinition("cazador", "Cazador Glacial", "frosthunter", 44),
            new EnemyDefinition("anima", "Ánima Helada", "frostwisp", 36),
            new EnemyDefinition("ensamblador", "Ensamblador", "tinkerer", 42),
            new EnemyDefinition("automata", "Autómata de Cuerda", "automaton", 14),
            new EnemyDefinition("escudero", "Escudero", "squire", 18),
            new EnemyDefinition("filacteria", "Filacteria", "phylactery", 40),
            new EnemyDefinition("comandante", "Comandante de la Ciudadela", "commander", 86, false, true),
            new EnemyDefinition("wendigo", "Wendigo", "wendigo", 92, false, true),
            new EnemyDefinition("cronomante", "Cronomante", "chronomancer", 88, false, true),
            new EnemyDefinition("liche", "El Rey Liche", "lichking", 130, true),
            new EnemyDefinition("relojero", "El Relojero", "clockmaker", 140, true),
            new EnemyDefinition("dragona", "La Dragona de Escarcha", "frostdragon", 148, true),
        });

        /// <summary>Spanish names of the floor-3 statuses (no collisions with cards, relics, identities, original intents or floors 1-2).</summary>
        public static readonly IReadOnlyDictionary<StatusKind, string> Floor3StatusNames = new Dictionary<StatusKind, string> {
            [StatusKind.Banner] = "Pendón", [StatusKind.Inspired] = "Inspiración", [StatusKind.Rage] = "Cólera", [StatusKind.Undying] = "Inmortal",
            [StatusKind.Spring] = "Cuerda", [StatusKind.Exhaustion] = "Agotamiento", [StatusKind.Frostbite] = "Congelación",
        };
        /// <summary>The two hero debuffs of floor 3: real glossary terms, highlighted in intent texts.</summary>
        public static readonly IReadOnlyList<StatusKind> Floor3HeroDebuffs = Array.AsReadOnly(new[] { StatusKind.Exhaustion, StatusKind.Frostbite });

        private static IEnumerable<EnemyProfile> BuildFloor3Profiles() => new[] {
            new EnemyProfile { Ai = "abanderado", Tier = EnemyTier.Normal, SoloPool = false, MinFloor = 3, MaxFloor = 3, Identity = "Pendón",
                Title = "Mientras viva, sus aliados golpean más fuerte. Mátalo primero.",
                Mechanics = L("Pendón: mientras viva, cada golpe de sus aliados hace tanto daño más como Pendón tenga (Inspiración).", "Solo sale acompañado."),
                StartStatuses = S((StatusKind.Banner, 2)), Moves = L("pole_strike", "raise_banner", "banner_charge") },
            new EnemyProfile { Ai = "barbaro", Tier = EnemyTier.Normal, SoloPool = true, MinFloor = 3, MaxFloor = 3, Identity = "Cólera",
                Title = "Cada golpe que le quita vida lo enfurece para su siguiente acción. Pégale pocas veces y fuerte.",
                Mechanics = L("Cólera: cada golpe que le quite vida le da tanta Fuerza como Cólera tenga; la pierde al terminar su siguiente acción."),
                StartStatuses = S((StatusKind.Rage, 1)), Moves = L("axe_chop", "war_cry", "whirlwind") },
            new EnemyProfile { Ai = "arcanista", Tier = EnemyTier.Normal, SoloPool = true, MinFloor = 3, MaxFloor = 3, Identity = "Agotamiento",
                Title = "Te drena el maná: con Agotamiento empiezas el turno con 1 de maná menos.",
                Mechanics = L("Agotamiento: mientras lo tengas, empiezas cada turno con 1 de maná menos. Baja 1 al final de tu turno."),
                Moves = L("mana_drain", "void_orb", "arcane_barrier") },
            new EnemyProfile { Ai = "cazador", Tier = EnemyTier.Normal, SoloPool = true, MinFloor = 3, MaxFloor = 3, Identity = "Perforante",
                Title = "Sus jabalinas atraviesan tu escudo: esos golpes no se paran, se aguantan.",
                Mechanics = L("Perforante: los golpes de Jabalina de Hielo ignoran tu escudo."),
                Moves = L("ice_javelin", "snow_stalk", "bolt_volley") },
            new EnemyProfile { Ai = "anima", Tier = EnemyTier.Normal, SoloPool = true, MinFloor = 3, MaxFloor = 3, Identity = "Congelación",
                Title = "Te congela: al terminar el turno pierdes vida por cada carta que te quede en la mano. Juega todo lo que puedas.",
                Mechanics = L("Congelación: mientras la tengas, al terminar tu turno pierdes 1 de vida por cada carta que te quede en la mano (ignora el escudo). Baja 1 al final de tu turno."),
                Moves = L("chill_touch", "frost_veil", "icicles") },
            new EnemyProfile { Ai = "ensamblador", Tier = EnemyTier.Normal, SoloPool = true, MinFloor = 3, MaxFloor = 3, Identity = "Taller",
                Title = "Monta Autómatas de Cuerda que disparan pernos perforantes. Si no caben más, los mejora.",
                Mechanics = L("Invoca Autómatas de Cuerda (nunca hay más de 3 enemigos en pantalla; como mucho 3 por combate). No dan oro."),
                Summons = L("automata"), SummonBudget = 3, Moves = L("assemble", "tune_up", "wrench", "spark") },
            new EnemyProfile { Ai = "automata", Tier = EnemyTier.Minion, MinFloor = 3, MaxFloor = 3, Identity = "Engranaje",
                Title = "Un muñeco de cuerda con un lanzapernos que atraviesa el escudo.",
                Mechanics = L("Esbirro invocado: no da oro.", "Engranaje: cada vez que actúa le da 1 de Cuerda al Relojero que lo montó."),
                Moves = L("bolt_shot", "wind_up") },
            new EnemyProfile { Ai = "escudero", Tier = EnemyTier.Minion, MinFloor = 3, MaxFloor = 3, Identity = "Escolta",
                Title = "Protege a su comandante con el escudo y lancea a su orden.",
                Mechanics = L("Esbirro invocado: no da oro."), Moves = L("shield_up", "spear_thrust") },
            new EnemyProfile { Ai = "filacteria", Tier = EnemyTier.Minion, MinFloor = 3, MaxFloor = 3, Identity = "Filacteria",
                Title = "Guarda el alma del Rey Liche: mientras exista, el Liche no puede morir.",
                Mechanics = L("Mientras viva, El Rey Liche no baja de 1 de vida (Inmortal).", "Cubre y cura a su señor."),
                Moves = L("cold_pulse", "life_beat") },
            new EnemyProfile { Ai = "comandante", Tier = EnemyTier.Elite, HasPhase = true, MinFloor = 3, MaxFloor = 3, Identity = "Mando",
                Title = "Llama a sus Escuderos y los inspira con su Pendón. Sin él, su guardia pega mucho menos.",
                Mechanics = L("Pendón: mientras viva, cada golpe de sus aliados hace tanto daño más como Pendón tenga.", "Invoca Escuderos (no dan oro; como mucho 3 por combate).",
                    "Lanza Perforante ignora tu escudo.", "Con la mitad de vida: Última Carga (+2 de Fuerza, +1 Pendón y otro Escudero)."),
                StartStatuses = S((StatusKind.Banner, 1)), Summons = L("escudero"), SummonBudget = 3,
                Moves = L("call_guard", "close_ranks", "command_thrust", "bugle_call", "piercing_lance", "last_charge") },
            new EnemyProfile { Ai = "wendigo", Tier = EnemyTier.Elite, HasPhase = true, MinFloor = 3, MaxFloor = 3, Identity = "Hambre",
                Title = "Te congela y se alimenta de ti; cada golpe que le quita vida lo enfurece.",
                Mechanics = L("Cólera: cada golpe que le quite vida le da tanta Fuerza como Cólera tenga, hasta el final de su siguiente acción.", "Devorar le cura lo que te quite.",
                    "Con la mitad de vida: Hambre Insaciable (se cura 15, +1 Cólera y +2 de Fuerza)."),
                StartStatuses = S((StatusKind.Rage, 1)), Moves = L("twin_claws", "frozen_howl", "devour_flesh", "endless_hunger") },
            new EnemyProfile { Ai = "cronomante", Tier = EnemyTier.Elite, HasPhase = true, MinFloor = 3, MaxFloor = 3, Identity = "Tiempo",
                Title = "Te ralentiza y rebobina el tiempo: el turno en que anuncia Rebobinar, recuperará toda la vida que le quites.",
                Mechanics = L("Agotamiento: empiezas tu turno con 1 de maná menos.", "Rebobinar: recupera toda la vida que perdió desde que empezó tu turno.",
                    "Con la mitad de vida: Bucle Temporal (rebobina y gana 2 de Fuerza)."),
                Moves = L("slow_time", "paradox", "rewind", "future_strike", "time_loop") },
            new EnemyProfile { Ai = "liche", Tier = EnemyTier.Boss, HasPhase = true, MinFloor = 3, MaxFloor = 3, UndyingWhile = "filacteria",
                Title = "No puede morir mientras exista su Filacteria. Rómpela, aguanta el frío y remátalo.",
                Mechanics = L("Inmortal: mientras viva su Filacteria no baja de 1 de vida.", "Congelación: pierdes 1 de vida por cada carta que te quede en la mano al terminar tu turno.",
                    "Lanza Gélida es Perforante (ignora tu escudo).", "Con la mitad de vida: si su Filacteria ya cayó, forja otra (una vez); si no, Furia del Liche (+3 de Fuerza)."),
                Summons = L("filacteria"), SummonBudget = 1,
                Moves = L("blizzard", "frost_lance", "ice_shroud", "void_touch", "phylactery_rite", "lich_fury") },
            new EnemyProfile { Ai = "relojero", Tier = EnemyTier.Boss, HasPhase = true, MinFloor = 3, MaxFloor = 3,
                Title = "Sus Autómatas le dan cuerda y la Campanada golpea una vez por Cuerda. Rómpelos antes de que suene.",
                Mechanics = L("Cuerda: Campanada golpea una vez por Cuerda; después vuelve a 1.", "Cada Autómata suyo que actúa le da 1 de Cuerda.",
                    "Invoca Autómatas de Cuerda (no dan oro; como mucho 4 por combate).", "Agotamiento: empiezas tu turno con 1 de maná menos.",
                    "Con la mitad de vida: Hora Cero (+2 de Fuerza, +2 Cuerda y más autómatas)."),
                StartStatuses = S((StatusKind.Spring, 2)), Summons = L("automata"), SummonBudget = 4,
                Moves = L("build_automata", "oil_gears", "clock_hands", "stop_time", "chime", "zero_hour") },
            new EnemyProfile { Ai = "dragona", Tier = EnemyTier.Boss, HasPhase = true, MinFloor = 3, MaxFloor = 3,
                Title = "Sus escamas conservan su escudo, sus garras atraviesan el tuyo y su aliento te congela. A la mitad se quita la coraza y se enfurece.",
                Mechanics = L("Blindaje: al empezar su turno conserva hasta 12 de escudo.", "Garra Perforante ignora tu escudo.", "Aliento Gélido te deja Congelación.",
                    "Con la mitad de vida: Furia Glacial (pierde el Blindaje y gana Cólera 2 y 2 de Fuerza)."),
                StartStatuses = S((StatusKind.Shell, 12)),
                Moves = L("frost_breath", "piercing_claw", "fold_wings", "glacier_tail", "glacial_fury") },
        };

        public static bool IsFloor3(string ai) => ai != null && Profiles.TryGetValue(ai, out var p) && p.MinFloor == 3;

        /// <summary>The floor-3 groups (vida ×0,8, no vengeance link). Like the original pairs of floor 3, a quarter of them get a third enemy.</summary>
        public static IReadOnlyList<EncounterGroup> Floor3Groups => floor3Groups ??= Array.AsReadOnly(new[] {
            new EncounterGroup { Key = "guardia", Name = "Guardia de la Ciudadela", Members = L("abanderado", "cazador"), MinFloor = 3, MaxFloor = 3,
                Synergy = "El Pendón del Abanderado se suma a las jabalinas del Cazador, que atraviesan tu escudo." },
            new EncounterGroup { Key = "horda", Name = "Horda de la Ventisca", Members = L("abanderado", "barbaro"), MinFloor = 3, MaxFloor = 3,
                Synergy = "El Abanderado inspira al Bárbaro, y cada golpe pequeño que le das al Bárbaro lo enfurece más." },
            new EncounterGroup { Key = "taller", Name = "Taller del Vacío", Members = L("ensamblador", "golem"), MinFloor = 3, MaxFloor = 3,
                Synergy = "El Gólem cubre al Ensamblador mientras sus Autómatas te disparan." },
            new EncounterGroup { Key = "tormenta", Name = "Tormenta Blanca", Members = L("anima", "arcanista"), MinFloor = 3, MaxFloor = 3,
                Synergy = "Con menos maná te quedan más cartas en la mano, y la Congelación te las cobra." },
            new EncounterGroup { Key = "patrulla", Name = "Patrulla Helada", Members = L("cazador", "ojo"), MinFloor = 3, MaxFloor = 3,
                Synergy = "El Ojo te deja Vulnerable y la jabalina del Cazador atraviesa tu escudo." },
            new EncounterGroup { Key = "vanguardia", Name = "Vanguardia Infernal", Members = L("abanderado", "diablillo"), MinFloor = 3, MaxFloor = 3,
                Synergy = "El Diablillo no para de atacar y cada uno de sus golpes lleva el Pendón del Abanderado." },
        });

        /// <summary>Floor-3 boss fights of the expanded roster (they replace the Señor de la Cripta). El Rey Liche starts with its Filacteria.</summary>
        public static IReadOnlyList<BossEncounter> Floor3BossEncounters => floor3BossEncounters ??= Array.AsReadOnly(new[] {
            new BossEncounter { Key = "liche", Name = "El Rey Liche", Members = L("liche", "filacteria"),
                Synergy = "La Filacteria guarda el alma del Rey Liche: mientras exista, él no baja de 1 de vida." },
            new BossEncounter { Key = "relojero", Name = "El Relojero", Members = L("relojero") },
            new BossEncounter { Key = "dragona", Name = "La Dragona de Escarcha", Members = L("dragona") },
        });

        private static readonly string[] Floor3SoloOrder = { "abanderado", "barbaro", "arcanista", "cazador", "anima", "ensamblador" };
        private static readonly string[] Floor3Elites = { "comandante", "wendigo", "cronomante" };

        /// <summary>The floor each expansion boss fight belongs to (1-3).</summary>
        public static int BossFightFloor(string key) =>
            BossEncounters.Any(b => b.Key == key) ? 1 : Floor2BossEncounters.Any(b => b.Key == key) ? 2 : Floor3BossEncounters.Any(b => b.Key == key) ? 3 : 0;

        /// <summary>The floor an expansion elite belongs to (0 for the original elites).</summary>
        public static int EliteFloor(string ai) => NewElites.Contains(ai) ? 1 : Floor2Elites.Contains(ai) ? 2 : Floor3Elites.Contains(ai) ? 3 : 0;

        /// <summary>Trio roll of floor 3: the third enemy comes from the original regulars and the floor's normals (not a member).</summary>
        private static List<string> TrioPool(int floor, IEnumerable<string> members)
        {
            var taken = new HashSet<string>(members);
            return Regulars.Select(d => d.Ai).Concat(Profiles.Values.Where(p => p.Tier == EnemyTier.Normal && p.MinFloor <= floor && floor <= p.MaxFloor).Select(p => p.Ai))
                .Where(a => !taken.Contains(a)).Distinct().OrderBy(a => a, StringComparer.Ordinal).ToList();
        }
    }

    /// <summary>Per-hit rules and numbers of the floor-3 statuses.</summary>
    public static class Floor3Rules
    {
        /// <summary>After a hit took HP: Cólera gives Strength until the enemy's next action.</summary>
        internal static void AfterHit(EnemyState enemy)
        {
            var rage = enemy.Statuses[StatusKind.Rage];
            if (rage <= 0) return;
            enemy.Statuses.Add(StatusKind.Strength, rage);
            enemy.RageStrength = (int)Math.Min(int.MaxValue, (long)enemy.RageStrength + rage);
            enemy.RageGained = (int)Math.Min(int.MaxValue, (long)enemy.RageGained + rage);
        }
    }
}
