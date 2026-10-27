using System;
using System.Collections.Generic;

namespace LastWish.Domain
{
    /// <summary>
    /// Intent patterns of the floor-2 expansion (hand-written). Every number and Spanish intent text of the floor-2 enemies
    /// lives here; values are given at floor 1 and scaled by <c>Make</c> (×1,15 on floor 2). Same kind rule as floor 1.
    /// </summary>
    public static partial class EnemyPatterns
    {
        private static EnemyIntent Floor2Next(EnemyState enemy, StatusSet hero, IReadOnlyList<EnemyState> enemies)
        {
            var profile = EnemyCatalog.Profile(enemy.Definition.Ai);
            EnemyIntent intent = null;
            if (profile.HasPhase && !enemy.PhaseTwo && enemy.Hp * 2L <= enemy.MaxHp && enemy.Statuses[StatusKind.Focus] == 0)
            { enemy.PhaseTwo = true; intent = Floor2Phase(enemy, enemies); }
            if (intent == null) intent = Floor2Special(enemy);
            if (intent == null) intent = Floor2Cycle(enemy, enemy.Step++, enemies);
            if (!HasAlly(enemy, enemies)) { intent.AllyBuffs = NoStatuses; intent.AllyBlock = 0; }
            return intent;
        }

        /// <summary>The one-time 50 % transition of the floor-2 elites and of El Nigromante and La Reina de los Espejos.</summary>
        private static EnemyIntent Floor2Phase(EnemyState e, IReadOnlyList<EnemyState> enemies)
        {
            var f = e.Floor;
            switch (e.Definition.Ai)
            {
                case "paladin":
                    e.Step = 0;
                    return Make("oath_broken", "Juramento Roto", "Rompe su juramento: gana Rito 2 (2 de Fuerza al terminar cada uno de sus turnos) y 2 de Fuerza.", f,
                        buffs: St((StatusKind.Ritual, 2), (StatusKind.Strength, 2)));
                case "condesa":
                {
                    e.Step = 0;
                    var bat = Room(e, enemies, "murcielago", 1) >= 1;
                    var x = new IntentExtras { HealSelf = EnemyCatalog.Scale(15, f) };
                    if (bat) x.Summons = new[] { new SummonSpec("murcielago", 1, 0.6, false) };
                    return Make("eternal_hunger", "Hambre Eterna", bat ? "Su hambre no tiene fin: se cura 15, gana 1 de Ansia de Sangre y llama a un Murciélago Vampiro."
                        : "Su hambre no tiene fin: se cura 15 y gana 1 de Ansia de Sangre.", f, buffs: St((StatusKind.Thirst, 1)), extras: x);
                }
                case "sacerdote":
                    e.Step = 0;
                    return Make("ascension", "Ascensión", "Asciende: gana Rito 2 (2 de Fuerza al terminar cada uno de sus turnos) y se cubre.", f, block: 10,
                        buffs: St((StatusKind.Ritual, 2)));
                case "nigromante":
                {
                    e.Step = 0;
                    var n = Room(e, enemies, "esqueleto", 2);
                    return Make("danse_macabre", "Danza Macabra", n == 2 ? "Baila con los muertos: alza dos Esqueletos, gana 2 de Fuerza y se cubre."
                        : n == 1 ? "Baila con los muertos: alza un Esqueleto, gana 2 de Fuerza y se cubre." : "Baila con los muertos: gana 2 de Fuerza y se cubre.", f,
                        block: 12, buffs: St((StatusKind.Strength, 2)), extras: n > 0 ? Summon("esqueleto", n, 1.0) : null);
                }
                case "reina_espejos":
                {
                    e.Step = 0;
                    var shard = Room(e, enemies, "esquirla", 1) >= 1;
                    var x = shard ? Summon("esquirla", 1, 1.0) : null;
                    return Make("thousand_reflections", "Mil Reflejos", shard ? "Se multiplica en mil reflejos: gana 2 de Reflejo y 2 de Fuerza y desprende una Esquirla."
                        : "Se multiplica en mil reflejos: gana 2 de Reflejo y 2 de Fuerza.", f, buffs: St((StatusKind.Reflect, 2), (StatusKind.Strength, 2)), extras: x);
                }
            }
            return null;
        }

        /// <summary>La Hidra: 2 more heads the first time it plans at 2/3 of its HP or less, and again at 1/3 (its cycle does not advance).</summary>
        private static EnemyIntent Floor2Special(EnemyState e)
        {
            if (e.Definition.Ai != "hidra") return null;
            var f = e.Floor;
            var grow = Math.Max(0, Math.Min(2, Floor2Rules.MaxHeads - e.Statuses[StatusKind.Heads]));
            if (e.Stage < 1 && e.Hp * 3L <= e.MaxHp * 2L)
            {
                e.Stage = 1;
                var buffs = grow > 0 ? St((StatusKind.Heads, grow), (StatusKind.Regen, 2)) : St((StatusKind.Regen, 2));
                return Make("new_heads", "Cabezas Nuevas", "Le brotan cabezas nuevas: +" + grow + " Cabezas y +2 de Cicatrización.", f, buffs: buffs);
            }
            if (e.Stage < 2 && e.Hp * 3L <= e.MaxHp)
            {
                e.Stage = 2;
                var buffs = grow > 0 ? St((StatusKind.Heads, grow), (StatusKind.Strength, 2)) : St((StatusKind.Strength, 2));
                return Make("hydra_fury", "Furia de la Hidra", "Enfurece: +" + grow + " Cabezas, gana 2 de Fuerza y se cubre.", f, block: 12, buffs: buffs);
            }
            return null;
        }

        private static EnemyIntent Floor2Cycle(EnemyState e, int step, IReadOnlyList<EnemyState> enemies)
        {
            var f = e.Floor;
            switch (e.Definition.Ai)
            {
                case "centinela":
                    switch (step % 3)
                    {
                        case 0: return Make("rune_maul", "Mazazo Rúnico", "Un mazazo de piedra rúnica.", f, attack: 10);
                        case 1: return Make("ward_rune", "Runa de Amparo", "Graba una runa: cada golpe que reciban él y sus aliados hace 4 de daño menos hasta su siguiente turno. Se cubre.", f,
                            block: 6, buffs: St((StatusKind.Ward, 4)), allyBuffs: St((StatusKind.Ward, 4)));
                        default: return Make("rune_burst", "Descarga Rúnica", "Dos descargas de energía rúnica.", f, attack: 5, hits: 2);
                    }
                case "fanatico":
                    if (step == 0) return Make("dark_rite", "Rito Oscuro", "Inicia un rito oscuro: gana Rito 2 (2 de Fuerza al terminar cada uno de sus turnos) y se cubre.", f,
                        block: 6, buffs: St((StatusKind.Ritual, 2)));
                    return (step - 1) % 2 == 0 ? Make("ritual_dagger", "Puñal Ritual", "Una puñalada ritual.", f, attack: 7)
                        : Make("blood_slash", "Tajo de Sangre", "Dos tajos con el puñal.", f, attack: 3, hits: 2);
                case "sombra":
                    switch (step % 3)
                    {
                        case 0: return Make("shadow_claw", "Zarpa Umbría", "Una zarpa de sombra que te deja Débil 1 turno.", f, attack: 7, debuffs: St((StatusKind.Weak, 1)));
                        case 1: return Make("grudge_grow", "Rencor Creciente", "Se envuelve en sombras: gana 4 de Rencor y se cubre.", f, block: 8, buffs: St((StatusKind.Grudge, 4)));
                        default: return Make("rend", "Desgarro", "Dos desgarros.", f, attack: 4, hits: 2);
                    }
                case "sanguijuela":
                    switch (step % 3)
                    {
                        case 0: return Make("suction_cup", "Ventosa", "Se te pega: Sangrado 2 turnos (pierdes 1 de vida por cada carta que juegues).", f, attack: 6, debuffs: St((StatusKind.Bleed, 2)));
                        case 1: return Make("drain", "Succión", "Te chupa la sangre y se cura lo que te quite.", f, attack: 7, lifesteal: true);
                        default: return Make("clot", "Coágulo", "Se endurece como un coágulo.", f, guard: 10);
                    }
                case "trol":
                    switch (step % 3)
                    {
                        case 0: return Make("club", "Garrotazo", "Un garrotazo enorme.", f, attack: 11);
                        case 1: return Make("lick_wounds", "Lamer Heridas", "Se lame las heridas: +1 de Cicatrización y se cubre.", f, block: 8, buffs: St((StatusKind.Regen, 1)));
                        default: return Make("double_claw", "Zarpazo Doble", "Dos zarpazos.", f, attack: 5, hits: 2);
                    }
                case "ilusionista":
                    switch (step % 3)
                    {
                        case 0:
                            if (Room(e, enemies, "ilusion", 1) >= 1)
                                return Make("mirror_split", "Desdoblarse", "Se desdobla en una Ilusión (máximo 3 enemigos en pantalla) y se cubre.", f, block: 5, extras: Summon("ilusion", 1, 1.0));
                            return Make("hypnotic_glow", "Brillo Hipnótico", (BudgetLeft(e) ? "No caben más ilusiones" : "Ya no se desdobla más") + ": te deja Débil y Vulnerable 1 turno y se cubre.", f,
                                block: 5, debuffs: St((StatusKind.Weak, 1), (StatusKind.Vulnerable, 1)));
                        case 1: return Make("prism_ray", "Rayo Prismático", "Un rayo de luz prismática.", f, attack: 10);
                        default: return Make("blinding_flash", "Destello Cegador", "Un destello que te deja Débil 1 turno.", f, attack: 4, debuffs: St((StatusKind.Weak, 1)));
                    }
                case "ilusion":
                    return step % 2 == 0 ? Make("phantom_strike", "Golpe Ilusorio", "Un golpe que duele de verdad.", f, attack: 5)
                        : Make("blink", "Parpadeo", "Parpadea: el próximo golpe que reciba no le hará nada.", f, buffs: St((StatusKind.Parry, 1)));
                case "esqueleto":
                    return step % 2 == 0 ? Make("rusty_slash", "Tajo Oxidado", "Un tajo con una espada oxidada.", f, attack: 6)
                        : Make("shield_bash", "Golpe de Escudo", "Golpea con su escudo y se cubre.", f, attack: 3, block: 5);
                case "esquirla":
                    return step % 2 == 0 ? Make("cutting_glint", "Destello Cortante", "Un destello afilado.", f, attack: 5)
                        : Make("refract", "Refractar", "Refracta la luz: gana 1 de Fuerza y se cubre.", f, block: 3, buffs: St((StatusKind.Strength, 1)));
                case "paladin":
                    if (!e.PhaseTwo)
                        switch (step % 3)
                        {
                            case 0: return Make("shield_of_faith", "Escudo de Fe", "Alza su escudo: gana 5 de Amparo (cada golpe que reciba hace 5 de daño menos hasta su siguiente turno) y se cubre.", f,
                                block: 10, buffs: St((StatusKind.Ward, 5)));
                            case 1: return Make("judgment_hammer", "Martillo del Juicio", "Un martillazo sagrado.", f, attack: 14);
                            default: return Make("condemn", "Condena", "Dos golpes que te dejan Vulnerable 1 turno.", f, attack: 6, hits: 2, debuffs: St((StatusKind.Vulnerable, 1)));
                        }
                    switch (step % 3)
                    {
                        case 0: return Make("unholy_fury", "Furia Impía", "Tres golpes de furia impía.", f, attack: 5, hits: 3);
                        case 1: return Make("judgment_hammer", "Martillo del Juicio", "Un martillazo impío.", f, attack: 16);
                        default: return Make("profane_roar", "Grito Profano", "Un grito profano que te deja Débil 2 turnos. Se cubre.", f, block: 8, debuffs: St((StatusKind.Weak, 2)));
                    }
                case "condesa":
                    if (!e.PhaseTwo)
                        switch (step % 4)
                        {
                            case 0: return Make("crimson_kiss", "Beso Carmesí", "Un beso que te roba la vida: se cura lo que te quite.", f, attack: 8, lifesteal: true);
                            case 1: return Make("bleeding_cut", "Corte Sangrante", "Dos cortes que te dejan Sangrado 2 turnos.", f, attack: 4, hits: 2, debuffs: St((StatusKind.Bleed, 2)));
                            case 2: return MistVeil(f);
                            default: return Make("crimson_bite", "Dentellada Carmesí", "Una dentellada brutal.", f, attack: 12);
                        }
                    switch (step % 4)
                    {
                        case 0: return Make("crimson_kiss", "Beso Carmesí", "Un beso que te roba la vida: se cura lo que te quite.", f, attack: 9, lifesteal: true);
                        case 1: return Make("bleeding_cut", "Corte Sangrante", "Dos cortes que te dejan Sangrado 2 turnos.", f, attack: 5, hits: 2, debuffs: St((StatusKind.Bleed, 2)));
                        case 2:
                            return Room(e, enemies, "murcielago", 1) >= 1
                                ? Make("call_colony", "Llamar a la Colonia", "Llama a un Murciélago Vampiro (no da oro) y se cubre.", f, block: 8, extras: Summon("murcielago", 1, 0.6))
                                : MistVeil(f);
                        default: return Make("crimson_bite", "Dentellada Carmesí", "Una dentellada brutal.", f, attack: 14);
                    }
                case "sacerdote":
                    switch (step % 4)
                    {
                        case 0:
                            if (Room(e, enemies, "fanatico", 1) >= 1)
                                return Make("abyss_call", "Llamada al Abismo", "Llama a un Fanático del Abismo (no da oro) y se cubre.", f, block: 6, extras: Summon("fanatico", 1, 0.5));
                            return Make("abyss_blessing", "Bendición Abisal", (BudgetLeft(e) ? "No caben más fanáticos" : "Ya no le quedan fanáticos que llamar") + ": da 1 de Rito a sus aliados y se cubre.", f,
                                block: 6, allyBuffs: St((StatusKind.Ritual, 1)));
                        case 1: return Make("ceremonial_dagger", "Daga Ceremonial", "Una puñalada ceremonial.", f, attack: 10);
                        case 2: return Offering(e, enemies);
                        default: return Make("abyss_flames", "Llamas del Abismo", "Dos llamaradas que te dejan Débil 1 turno.", f, attack: 5, hits: 2, debuffs: St((StatusKind.Weak, 1)));
                    }
                case "nigromante":
                    if (!e.PhaseTwo)
                        switch (step % 4)
                        {
                            case 0: return RaiseBones(e, enemies);
                            case 1: return Make("necrotic_touch", "Toque Necrótico", "Un toque necrótico: Necrosis 2 turnos (no puedes curarte).", f, attack: 9, debuffs: St((StatusKind.Necrosis, 2)));
                            case 2: return SoulHarvest(e);
                            default: return Make("bone_wall", "Muro de Huesos", "Levanta un muro de huesos: se cubre y da 6 de escudo a sus esqueletos.", f, guard: 12, allyBlock: 6);
                        }
                    switch (step % 3)
                    {
                        case 0: return SoulHarvest(e);
                        case 1: return Make("necrotic_touch", "Toque Necrótico", "Un toque necrótico: Necrosis 3 turnos (no puedes curarte).", f, attack: 11, debuffs: St((StatusKind.Necrosis, 3)));
                        default: return RaiseBones(e, enemies);
                    }
                case "hidra":
                    switch (step % 3)
                    {
                        case 0: return ManyBites(e);
                        case 1: return Make("acid_breath", "Aliento Ácido", "Un aliento ácido que te deja Frágil 2 turnos.", f, attack: 8, debuffs: St((StatusKind.Frail, 2)));
                        default: return Make("submerge", "Sumergirse", "Se sumerge en la cisterna y se cubre.", f, guard: 14);
                    }
                case "reina_espejos":
                    if (!e.PhaseTwo)
                        switch (step % 4)
                        {
                            case 0: return Shatter(e, enemies);
                            case 1: return Make("prism_cut", "Corte Prismático", "Dos cortes de cristal.", f, attack: 6, hits: 2);
                            case 2: return CursedMirror(f);
                            default: return Make("mirror_ray", "Rayo Especular", "Un rayo de luz concentrada.", f, attack: 15);
                        }
                    switch (step % 4)
                    {
                        case 0: return Make("prism_cut", "Corte Prismático", "Dos cortes de cristal.", f, attack: 7, hits: 2);
                        case 1: return Shatter(e, enemies);
                        case 2: return Make("mirror_ray", "Rayo Especular", "Un rayo de luz concentrada.", f, attack: 18);
                        default: return CursedMirror(f);
                    }
            }
            throw new ArgumentException("Unknown enemy: " + e.Definition.Ai);
        }

        private static EnemyIntent MistVeil(int f) =>
            Make("mist_veil", "Velo de Niebla", "Se envuelve en niebla: se cubre y te deja Débil 1 turno.", f, guard: 12, debuffs: St((StatusKind.Weak, 1)));

        private static EnemyIntent CursedMirror(int f) =>
            Make("cursed_mirror", "Espejo Maldito", "Te devuelve su Veneno, Débil y Vulnerable, recupera 1 de Reflejo y se cubre.", f, block: 10,
                buffs: St((StatusKind.Reflect, 1)), extras: new IntentExtras { ReturnDebuffs = true });

        private static EnemyIntent Shatter(EnemyState e, IReadOnlyList<EnemyState> enemies)
        {
            var n = Room(e, enemies, "esquirla", 2);
            if (n > 0) return Make("shatter", "Fragmentar", n == 2 ? "Desprende dos Esquirlas de Espejo y se cubre." : "Desprende una Esquirla de Espejo y se cubre.", e.Floor,
                block: 6, extras: Summon("esquirla", n, 1.0));
            return Make("prism_glow", "Brillo Prismático", (BudgetLeft(e) ? "No caben más esquirlas" : "Ya no le quedan esquirlas") + ": da 2 de Fuerza a sus aliados y se cubre.", e.Floor,
                block: 6, allyBuffs: St((StatusKind.Strength, 2)));
        }

        private static EnemyIntent RaiseBones(EnemyState e, IReadOnlyList<EnemyState> enemies)
        {
            var n = Room(e, enemies, "esqueleto", 2);
            if (n > 0) return Make("raise_bones", "Alzar Huesos", n == 2 ? "Alza dos Esqueletos y se cubre." : "Alza un Esqueleto y se cubre.", e.Floor,
                block: 8, extras: Summon("esqueleto", n, 1.0));
            return Make("bone_order", "Orden de Hueso", (BudgetLeft(e) ? "No caben más esqueletos" : "Su ejército está agotado") + ": da 2 de Fuerza a sus aliados y se cubre.", e.Floor,
                block: 8, allyBuffs: St((StatusKind.Strength, 2)));
        }

        /// <summary>Cosecha de Almas: 6 (8 after the 50 %) + 3 per minion of its that died; recalculated after every card.</summary>
        private static EnemyIntent SoulHarvest(EnemyState e)
        {
            var lost = e.MinionsLost;
            return Make("soul_harvest", "Cosecha de Almas", "Cosecha las almas de su ejército: +3 de daño por cada esqueleto suyo destruido (" + lost + ").", e.Floor,
                attack: (e.PhaseTwo ? 8 : 6) + 3 * Math.Min(lost, 1000), extras: new IntentExtras { Dynamic = true });
        }

        /// <summary>Mordisco Múltiple: one bite per head; recalculated after every card (heads can be cut).</summary>
        private static EnemyIntent ManyBites(EnemyState e) =>
            Make("many_bites", "Mordisco Múltiple", "Muerde con cada cabeza: un golpe por cabeza.", e.Floor, attack: 4, hits: Math.Max(1, e.Statuses[StatusKind.Heads]),
                extras: new IntentExtras { Dynamic = true });

        private static EnemyIntent Offering(EnemyState e, IReadOnlyList<EnemyState> enemies) =>
            DevourVictim(e, enemies) != null
                ? Make("dark_offering", "Ofrenda Oscura", "Sacrifica a su Fanático más fuerte: se queda su Fuerza más 2 y se cura 12. Mátalo antes y no podrá.", e.Floor,
                    extras: new IntentExtras { Devour = true, DevourHeal = EnemyCatalog.Scale(12, e.Floor), DevourStrength = 2, Dynamic = true })
                : Punish(e.Floor);

        private static EnemyIntent Punish(int f) => Make("abyss_punish", "Castigo Abisal", "Sin nadie a quien sacrificar, te castiga él mismo.", f, attack: 13);

        /// <summary>The living minion an Ofrenda Oscura takes: the one it summoned with the most Strength (first in the list on ties).</summary>
        public static EnemyState DevourVictim(EnemyState devourer, IReadOnlyList<EnemyState> enemies)
        {
            EnemyState best = null;
            foreach (var other in enemies)
                if (other != devourer && other.Alive && other.SummonerId == devourer.Id && (best == null || other.Statuses[StatusKind.Strength] > best.Statuses[StatusKind.Strength]))
                    best = other;
            return best;
        }

        /// <summary>Refreshes the floor-2 dynamic intents of every living enemy (after deaths were counted).</summary>
        public static void Floor2React(IReadOnlyList<EnemyState> enemies)
        {
            for (var i = 0; i < enemies.Count; i++) if (enemies[i].Alive) Floor2Refresh(enemies[i], enemies);
        }

        /// <summary>Floor-2 dynamic intents: bites per head, Cosecha per lost minion, an Ofrenda without a victim becomes Castigo Abisal.</summary>
        private static void Floor2Refresh(EnemyState e, IReadOnlyList<EnemyState> enemies)
        {
            if (e.Intent == null || !e.Intent.Extras.Dynamic) return;
            switch (e.Intent.Id)
            {
                case "many_bites": { var fresh = ManyBites(e); if (fresh.Hits != e.Intent.Hits) e.Intent = fresh; break; }
                case "soul_harvest": { var fresh = SoulHarvest(e); if (fresh.Value != e.Intent.Value) e.Intent = fresh; break; }
                case "dark_offering": if (DevourVictim(e, enemies) == null) e.Intent = Punish(e.Floor); break;
            }
        }
    }
}
