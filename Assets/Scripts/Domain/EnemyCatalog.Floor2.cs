using System;
using System.Collections.Generic;
using System.Linq;

namespace LastWish.Domain
{
    /// <summary>
    /// The floor-2 expansion (hand-written): 15 enemies (6 normals, 3 minions, 3 elites, 3 bosses), 6 groups and 3 boss
    /// fights that replace the Señor de la Cripta on floor 2. Only <see cref="EnemyRoster.Expanded"/> rolls them, and only on floor 2.
    /// </summary>
    public static partial class EnemyCatalog
    {
        private static IReadOnlyList<EnemyDefinition> floor2Definitions;
        private static IReadOnlyList<EncounterGroup> floor2Groups;
        private static IReadOnlyList<BossEncounter> floor2BossEncounters;

        /// <summary>The 15 floor-2 enemies: 6 normals, 3 minions, 3 elites and 3 bosses.</summary>
        public static IReadOnlyList<EnemyDefinition> Floor2Definitions => floor2Definitions ??= Array.AsReadOnly(new[] {
            new EnemyDefinition("centinela", "Centinela Rúnico", "sentinel", 56),
            new EnemyDefinition("fanatico", "Fanático del Abismo", "zealot", 50),
            new EnemyDefinition("sombra", "Sombra Rencorosa", "shade", 38),
            new EnemyDefinition("sanguijuela", "Sanguijuela Gigante", "leech", 44),
            new EnemyDefinition("trol", "Trol de Cloaca", "troll", 56),
            new EnemyDefinition("ilusionista", "Ilusionista", "illusionist", 40),
            new EnemyDefinition("ilusion", "Ilusión", "illusion", 10),
            new EnemyDefinition("esqueleto", "Esqueleto Alzado", "skeleton", 16),
            new EnemyDefinition("esquirla", "Esquirla de Espejo", "shard", 12),
            new EnemyDefinition("paladin", "Paladín Caído", "paladin", 88, false, true),
            new EnemyDefinition("condesa", "Condesa Carmesí", "countess", 84, false, true),
            new EnemyDefinition("sacerdote", "Sumo Sacerdote del Abismo", "highpriest", 82, false, true),
            new EnemyDefinition("nigromante", "El Nigromante", "necromancer", 132, true),
            new EnemyDefinition("hidra", "La Hidra de la Cisterna", "hydra", 150, true),
            new EnemyDefinition("reina_espejos", "La Reina de los Espejos", "mirrorqueen", 130, true),
        });

        /// <summary>Spanish names of the floor-2 statuses (no collisions with cards, relics, identities or original intents).</summary>
        public static readonly IReadOnlyDictionary<StatusKind, string> Floor2StatusNames = new Dictionary<StatusKind, string> {
            [StatusKind.Ward] = "Amparo", [StatusKind.Ritual] = "Rito", [StatusKind.Grudge] = "Rencor", [StatusKind.Regen] = "Cicatrización",
            [StatusKind.Heads] = "Cabezas", [StatusKind.Reflect] = "Reflejo", [StatusKind.Thirst] = "Ansia de Sangre",
            [StatusKind.Bleed] = "Sangrado", [StatusKind.Necrosis] = "Necrosis",
        };
        /// <summary>The two hero debuffs of floor 2: real glossary terms, highlighted in intent texts.</summary>
        public static readonly IReadOnlyList<StatusKind> Floor2HeroDebuffs = Array.AsReadOnly(new[] { StatusKind.Bleed, StatusKind.Necrosis });

        private static IEnumerable<EnemyProfile> BuildFloor2Profiles() => new[] {
            new EnemyProfile { Ai = "centinela", Tier = EnemyTier.Normal, SoloPool = true, MinFloor = 2, MaxFloor = 2, Identity = "Amparo",
                Title = "Ampara a su grupo: cada golpe hace menos daño. Los golpes grandes lo atraviesan mejor que muchos pequeños.",
                Mechanics = L("Amparo: tras Runa de Amparo, cada golpe que reciban él o sus aliados hace 4 de daño menos hasta su siguiente turno."),
                Moves = L("rune_maul", "ward_rune", "rune_burst") },
            new EnemyProfile { Ai = "fanatico", Tier = EnemyTier.Normal, SoloPool = true, MinFloor = 2, MaxFloor = 2, Identity = "Rito",
                Title = "Empieza con un rito y gana Fuerza cada turno. Mátalo rápido.",
                Mechanics = L("Rito: al terminar cada uno de sus turnos gana tanta Fuerza como Rito tenga."),
                Moves = L("dark_rite", "ritual_dagger", "blood_slash") },
            new EnemyProfile { Ai = "sombra", Tier = EnemyTier.Normal, SoloPool = true, MinFloor = 2, MaxFloor = 2, Identity = "Rencor",
                Title = "Al morir te golpea con todo su rencor. Mátala con escudo puesto, o antes de que crezca.",
                Mechanics = L("Rencor: al morir te inflige tanto daño como Rencor tenga (tu escudo lo absorbe)."),
                StartStatuses = S((StatusKind.Grudge, 6)), Moves = L("shadow_claw", "grudge_grow", "rend") },
            new EnemyProfile { Ai = "sanguijuela", Tier = EnemyTier.Normal, SoloPool = true, MinFloor = 2, MaxFloor = 2, Identity = "Sangrado",
                Title = "Te hace sangrar: cada carta que juegues te cuesta vida. Juega menos cartas y más fuertes.",
                Mechanics = L("Sangrado: mientras lo tengas, pierdes 1 de vida por cada carta que juegues (ignora el escudo)."),
                Moves = L("suction_cup", "drain", "clot") },
            new EnemyProfile { Ai = "trol", Tier = EnemyTier.Normal, SoloPool = true, MinFloor = 2, MaxFloor = 2, Identity = "Cicatrización",
                Title = "Se cura al final de cada turno. El Fuego y el Veneno le impiden cicatrizar.",
                Mechanics = L("Cicatrización: al terminar su turno se cura tanto como Cicatrización tenga, salvo si tiene Fuego o Veneno."),
                StartStatuses = S((StatusKind.Regen, 3)), Moves = L("club", "lick_wounds", "double_claw") },
            new EnemyProfile { Ai = "ilusionista", Tier = EnemyTier.Normal, SoloPool = true, MinFloor = 2, MaxFloor = 2, Identity = "Espejismo",
                Title = "Se desdobla en Ilusiones que desaparecen cuando ella cae. Ve a por la original.",
                Mechanics = L("Invoca Ilusiones (nunca hay más de 3 enemigos en pantalla; como mucho 4 por combate). No dan oro.", "Si muere la Ilusionista, sus Ilusiones se desvanecen."),
                Summons = L("ilusion"), SummonBudget = 4, Moves = L("mirror_split", "hypnotic_glow", "prism_ray", "blinding_flash") },
            new EnemyProfile { Ai = "ilusion", Tier = EnemyTier.Minion, MinFloor = 2, MaxFloor = 2, Identity = "Espejismo", BoundToSummoner = true,
                Title = "Un reflejo de la Ilusionista: parpadea para esquivar un golpe.",
                Mechanics = L("Esbirro invocado: no da oro.", "Se desvanece si muere la Ilusionista."), Moves = L("phantom_strike", "blink") },
            new EnemyProfile { Ai = "esqueleto", Tier = EnemyTier.Minion, MinFloor = 2, MaxFloor = 2, Identity = "Ejército",
                Title = "Hueso viejo al servicio del Nigromante.",
                Mechanics = L("Esbirro invocado: no da oro."), Moves = L("rusty_slash", "shield_bash") },
            new EnemyProfile { Ai = "esquirla", Tier = EnemyTier.Minion, MinFloor = 2, MaxFloor = 2, Identity = "Rencor",
                Title = "Un trozo de espejo: corta al romperse.",
                Mechanics = L("Esbirro invocado: no da oro.", "Rencor: al romperse te corta (tu escudo lo absorbe)."),
                StartStatuses = S((StatusKind.Grudge, 4)), Moves = L("cutting_glint", "refract") },
            new EnemyProfile { Ai = "paladin", Tier = EnemyTier.Elite, HasPhase = true, MinFloor = 2, MaxFloor = 2, Identity = "Juramento",
                Title = "Se protege con Amparo; al romper su juramento gana Fuerza cada turno. Guarda tu daño para entonces.",
                Mechanics = L("Amparo: tras Escudo de Fe, cada golpe que recibe hace 5 de daño menos hasta su siguiente turno.", "Con la mitad de vida: Juramento Roto (Rito 2 y 2 de Fuerza: crece cada turno)."),
                Moves = L("shield_of_faith", "judgment_hammer", "condemn", "oath_broken", "unholy_fury", "profane_roar") },
            new EnemyProfile { Ai = "condesa", Tier = EnemyTier.Elite, HasPhase = true, MinFloor = 2, MaxFloor = 2, Identity = "Vampiresa",
                Title = "Te hace sangrar y se alimenta de tu sangre: cada turno en que pierdas vida se hace más fuerte.",
                Mechanics = L("Ansia de Sangre: cada turno en que pierdas vida gana 1 de Fuerza (una vez por turno).", "Te deja Sangrado y se cura con lo que te quita.",
                    "Con la mitad de vida: Hambre Eterna (se cura, más Ansia de Sangre y llama a un Murciélago, que no da oro)."),
                StartStatuses = S((StatusKind.Thirst, 1)), Summons = L("murcielago"), SummonBudget = 2,
                Moves = L("crimson_kiss", "bleeding_cut", "mist_veil", "crimson_bite", "eternal_hunger", "call_colony") },
            new EnemyProfile { Ai = "sacerdote", Tier = EnemyTier.Elite, HasPhase = true, MinFloor = 2, MaxFloor = 2, Identity = "Ofrenda",
                Title = "Llama a sus fanáticos y los sacrifica para quedarse su Fuerza. Mátalos antes de la ofrenda.",
                Mechanics = L("Invoca Fanáticos del Abismo (no dan oro; como mucho 3 por combate).", "Ofrenda Oscura: sacrifica a su Fanático más fuerte, se queda su Fuerza más 2 y se cura.",
                    "Con la mitad de vida: Ascensión (gana Rito 2)."),
                Summons = L("fanatico"), SummonBudget = 3,
                Moves = L("abyss_call", "abyss_blessing", "ceremonial_dagger", "dark_offering", "abyss_punish", "abyss_flames", "ascension") },
            new EnemyProfile { Ai = "nigromante", Tier = EnemyTier.Boss, HasPhase = true, MinFloor = 2, MaxFloor = 2,
                Title = "Levanta esqueletos, te impide curarte y su Cosecha crece con cada hueso que rompes.",
                Mechanics = L("Invoca Esqueletos Alzados (no dan oro; como mucho 6 por combate).", "Cosecha de Almas: +3 de daño por cada esqueleto suyo destruido.",
                    "Necrosis: mientras la tengas no puedes curarte.", "Con la mitad de vida: Danza Macabra (más esqueletos y 2 de Fuerza)."),
                Summons = L("esqueleto"), SummonBudget = 6,
                Moves = L("raise_bones", "bone_order", "necrotic_touch", "soul_harvest", "bone_wall", "danse_macabre") },
            new EnemyProfile { Ai = "hidra", Tier = EnemyTier.Boss, MinFloor = 2, MaxFloor = 2,
                Title = "Muerde con cada cabeza y cicatriza. Córtale cabezas con golpes grandes y quémala o envenénala.",
                Mechanics = L("Cabezas: Mordisco Múltiple golpea una vez por cabeza.", "Un solo golpe que le quite 20 o más de vida le corta una cabeza (mínimo 2).",
                    "Cicatrización: se cura al terminar su turno, salvo si tiene Fuego o Veneno.", "Al bajar a 2/3 y a 1/3 de su vida le crecen 2 cabezas."),
                StartStatuses = S((StatusKind.Heads, 3), (StatusKind.Regen, 5)),
                Moves = L("many_bites", "acid_breath", "submerge", "new_heads", "hydra_fury") },
            new EnemyProfile { Ai = "reina_espejos", Tier = EnemyTier.Boss, HasPhase = true, MinFloor = 2, MaxFloor = 2,
                Title = "Te devuelve los golpes grandes y tus propios perjuicios. Sus esquirlas cortan al romperse.",
                Mechanics = L("Reflejo: cada golpe que le quite 15 o más de vida gasta 1 de Reflejo y te devuelve la mitad (tu escudo lo absorbe).",
                    "Espejo Maldito: te pasa su Veneno, Débil y Vulnerable y recupera 1 de Reflejo.",
                    "Invoca Esquirlas de Espejo (no dan oro; como mucho 6 por combate).", "Con la mitad de vida: Mil Reflejos (+2 de Reflejo)."),
                StartStatuses = S((StatusKind.Reflect, 2)), Summons = L("esquirla"), SummonBudget = 6,
                Moves = L("shatter", "prism_glow", "prism_cut", "cursed_mirror", "mirror_ray", "thousand_reflections") },
        };

        public static bool IsFloor2(string ai) => ai != null && Profiles.TryGetValue(ai, out var p) && p.MinFloor == 2;

        /// <summary>The floor-2 groups (vida ×0,8, no vengeance link).</summary>
        public static IReadOnlyList<EncounterGroup> Floor2Groups => floor2Groups ??= Array.AsReadOnly(new[] {
            new EncounterGroup { Key = "conclave", Name = "Cónclave Abisal", Members = L("fanatico", "centinela"), MinFloor = 2, MaxFloor = 2,
                Synergy = "El Centinela ampara al Fanático mientras su Rito lo hace más fuerte cada turno." },
            new EncounterGroup { Key = "sangre", Name = "Festín de Sangre", Members = L("sanguijuela", "murcielago"), MinFloor = 2, MaxFloor = 2,
                Synergy = "La Sanguijuela te hace sangrar con cada carta y el Murciélago se cura con tu sangre." },
            new EncounterGroup { Key = "teatro", Name = "Teatro de Sombras", Members = L("ilusionista", "sombra"), MinFloor = 2, MaxFloor = 2,
                Synergy = "Las Ilusiones te distraen mientras la Sombra acumula Rencor." },
            new EncounterGroup { Key = "cloaca", Name = "Cloaca Infecta", Members = L("trol", "babosa"), MinFloor = 2, MaxFloor = 2,
                Synergy = "La Babosa corroe tu escudo y el Trol cicatriza sus heridas: quémalo o envenénalo." },
            new EncounterGroup { Key = "coro", Name = "Coro de Lamentos", Members = L("sombra", "sombra"), MinFloor = 2, MaxFloor = 2,
                Synergy = "Dos Sombras Rencorosas: si las matas a la vez con poco escudo, su Rencor te hará pedazos." },
            new EncounterGroup { Key = "forja", Name = "Forja Rúnica", Members = L("centinela", "craneo"), MinFloor = 2, MaxFloor = 2,
                Synergy = "El Centinela ampara al Cráneo Ígneo, que no para de atacar." },
        });

        /// <summary>Floor-2 boss fights of the expanded roster (they replace the Señor de la Cripta).</summary>
        public static IReadOnlyList<BossEncounter> Floor2BossEncounters => floor2BossEncounters ??= Array.AsReadOnly(new[] {
            new BossEncounter { Key = "nigromante", Name = "El Nigromante", Members = L("nigromante") },
            new BossEncounter { Key = "hidra", Name = "La Hidra de la Cisterna", Members = L("hidra") },
            new BossEncounter { Key = "reina_espejos", Name = "La Reina de los Espejos", Members = L("reina_espejos") },
        });

        /// <summary>Every expansion group (floor 1, then floor 2, then floor 3).</summary>
        public static IEnumerable<EncounterGroup> AllGroups => Groups.Concat(Floor2Groups).Concat(Floor3Groups);
        /// <summary>Every expansion boss fight (floor 1, then floor 2, then floor 3).</summary>
        public static IEnumerable<BossEncounter> AllBossEncounters => BossEncounters.Concat(Floor2BossEncounters).Concat(Floor3BossEncounters);

        private static readonly string[] Floor2SoloOrder = { "centinela", "fanatico", "sombra", "sanguijuela", "trol", "ilusionista" };
        private static readonly string[] Floor2Elites = { "paladin", "condesa", "sacerdote" };

        /// <summary>The boss fights one floor of the expanded roster picks from by seed (empty above <see cref="LastExpandedFloor"/>).</summary>
        public static IReadOnlyList<string> FloorBossKeys(int floor) =>
            floor == 1 ? BossEncounters.Select(b => b.Key).ToList() : floor == 2 ? Floor2BossEncounters.Select(b => b.Key).ToList()
            : floor == 3 ? Floor3BossEncounters.Select(b => b.Key).ToList() : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>The elites one floor of the expanded roster picks from: the 5 originals and that floor's new ones.</summary>
        public static IReadOnlyList<string> FloorElites(int floor) =>
            floor == 1 ? EliteOrder.Concat(NewElites).ToList() : floor == 2 ? EliteOrder.Concat(Floor2Elites).ToList()
            : floor == 3 ? EliteOrder.Concat(Floor3Elites).ToList() : (IReadOnlyList<string>)EliteOrder;
    }

    /// <summary>Per-hit rules and numbers of the floor-2 statuses.</summary>
    public static class Floor2Rules
    {
        /// <summary>A single hit that takes this much HP from La Hidra cuts one of its heads.</summary>
        public const int HeadCut = 20;
        public const int MinHeads = 2, MaxHeads = 8;
        /// <summary>A single hit that takes this much HP from an enemy with Reflejo spends one stack and comes back halved.</summary>
        public const int ReflectFrom = 15;

        /// <summary>After a hit took HP: Reflejo owes half of it to the hero, a big hit cuts a head.</summary>
        internal static void AfterHit(EnemyState enemy, int lost)
        {
            var reflect = enemy.Statuses[StatusKind.Reflect];
            if (reflect > 0 && lost >= ReflectFrom)
            {
                enemy.Statuses.Set(StatusKind.Reflect, reflect - 1);
                enemy.ReflectPending = (int)Math.Min(int.MaxValue, (long)enemy.ReflectPending + lost / 2);
            }
            var heads = enemy.Statuses[StatusKind.Heads];
            if (heads > MinHeads && lost >= HeadCut) { enemy.Statuses.Set(StatusKind.Heads, heads - 1); enemy.HeadsCut++; }
        }
    }
}
