using System;
using System.Collections.Generic;

namespace LastWish.Domain
{
    /// <summary>
    /// Intent patterns of the floor-3 expansion (hand-written). Every number and Spanish intent text of the floor-3 enemies
    /// lives here; values are given at floor 1 and scaled by <c>Make</c> (×1,3 on floor 3). Same kind rule as floor 1.
    /// Also the two auras of floor 3: Pendón (Inspiración for the allies) and Inmortal (El Rey Liche while its Filacteria lives).
    /// </summary>
    public static partial class EnemyPatterns
    {
        private static EnemyIntent Floor3Next(EnemyState enemy, StatusSet hero, IReadOnlyList<EnemyState> enemies)
        {
            var profile = EnemyCatalog.Profile(enemy.Definition.Ai);
            EnemyIntent intent = null;
            if (profile.HasPhase && !enemy.PhaseTwo && enemy.Hp * 2L <= enemy.MaxHp && enemy.Statuses[StatusKind.Focus] == 0)
            { enemy.PhaseTwo = true; intent = Floor3Phase(enemy, enemies); }
            if (intent == null) intent = Floor3Cycle(enemy, enemy.Step++, enemies);
            if (!HasAlly(enemy, enemies)) { intent.AllyBuffs = NoStatuses; intent.AllyBlock = 0; }
            return intent;
        }

        /// <summary>The one-time 50 % transition of the floor-3 elites and bosses (every one restarts its cycle).</summary>
        private static EnemyIntent Floor3Phase(EnemyState e, IReadOnlyList<EnemyState> enemies)
        {
            var f = e.Floor;
            var step = e.Step;
            e.Step = 0;
            switch (e.Definition.Ai)
            {
                case "comandante":
                {
                    var squire = Room(e, enemies, "escudero", 1) >= 1;
                    return Make("last_charge", "Última Carga", squire ? "Ordena la última carga: gana 2 de Fuerza y 1 de Pendón y llama a otro Escudero."
                        : "Ordena la última carga: gana 2 de Fuerza y 1 de Pendón.", f,
                        buffs: St((StatusKind.Strength, 2), (StatusKind.Banner, 1)), extras: squire ? Summon("escudero", 1, 1.0) : null);
                }
                case "wendigo":
                    return Make("endless_hunger", "Hambre Insaciable", "Su hambre no tiene fondo: se cura 15, gana 1 de Cólera y 2 de Fuerza.", f,
                        buffs: St((StatusKind.Rage, 1), (StatusKind.Strength, 2)), extras: new IntentExtras { HealSelf = EnemyCatalog.Scale(15, f) });
                case "cronomante":
                    return Make("time_loop", "Bucle Temporal", "Cierra un bucle temporal: recuperará toda la vida que le quites este turno y ganará 2 de Fuerza.", f,
                        buffs: St((StatusKind.Strength, 2)), extras: new IntentExtras { Rewind = true, Dynamic = true });
                case "liche":
                    if (!AnyAlive(e, enemies, "filacteria") && Room(e, enemies, "filacteria", 1) >= 1)
                        return Make("phylactery_rite", "Ritual de la Filacteria", "Forja una Filacteria nueva (vuelve a ser Inmortal mientras exista) y se cubre.", f,
                            block: 12, extras: Summon("filacteria", 1, 1.0));
                    return Make("lich_fury", "Furia del Liche", "La escarcha le corre por las venas: gana 3 de Fuerza y se cubre.", f, block: 12, buffs: St((StatusKind.Strength, 3)));
                case "relojero":
                {
                    var n = Room(e, enemies, "automata", 2);
                    var x = n > 0 ? Summon("automata", n, 1.0) : null;
                    return Make("zero_hour", "Hora Cero", n == 2 ? "Suena la hora cero: gana 2 de Fuerza y 2 de Cuerda y monta dos Autómatas."
                        : n == 1 ? "Suena la hora cero: gana 2 de Fuerza y 2 de Cuerda y monta un Autómata." : "Suena la hora cero: gana 2 de Fuerza y 2 de Cuerda.", f,
                        buffs: St((StatusKind.Strength, 2), (StatusKind.Spring, 2)), extras: x);
                }
                case "dragona":
                    return Make("glacial_fury", "Furia Glacial", "Se arranca la coraza de hielo: pierde el Blindaje y gana 2 de Cólera y 2 de Fuerza.", f,
                        buffs: St((StatusKind.Rage, 2), (StatusKind.Strength, 2)), extras: new IntentExtras { SetSelf = St((StatusKind.Shell, 0)) });
            }
            e.Step = step;   // no phase move: the cycle goes on where it was
            return null;
        }

        private static EnemyIntent Floor3Cycle(EnemyState e, int step, IReadOnlyList<EnemyState> enemies)
        {
            var f = e.Floor;
            switch (e.Definition.Ai)
            {
                case "abanderado":
                    switch (step % 3)
                    {
                        case 0: return Make("pole_strike", "Asta Helada", "Un golpe con el asta del pendón.", f, attack: 8);
                        case 1: return Make("raise_banner", "Alzar el Pendón", "Alza el pendón: gana 1 de Pendón (sus aliados harán 1 de daño más por golpe) y se cubre.", f,
                            block: 8, buffs: St((StatusKind.Banner, 1)));
                        default: return Make("banner_charge", "Carga del Pendón", "Dos embestidas con el pendón por delante.", f, attack: 5, hits: 2);
                    }
                case "barbaro":
                    switch (step % 3)
                    {
                        case 0: return Make("axe_chop", "Hachazo", "Un hachazo brutal.", f, attack: 10);
                        case 1: return Make("war_cry", "Grito de Guerra", "Ruge de furia: gana 1 de Cólera y se cubre.", f, block: 6, buffs: St((StatusKind.Rage, 1)));
                        default: return Make("whirlwind", "Torbellino", "Gira con el hacha: tres tajos.", f, attack: 4, hits: 3);
                    }
                case "arcanista":
                    switch (step % 3)
                    {
                        case 0: return Make("mana_drain", "Drenar Maná", "Te drena el maná: Agotamiento 1 turno (empiezas tu turno con 1 de maná menos).", f,
                            attack: 6, debuffs: St((StatusKind.Exhaustion, 1)));
                        case 1: return Make("void_orb", "Orbe del Vacío", "Un orbe de energía del vacío.", f, attack: 11);
                        default: return Make("arcane_barrier", "Barrera Arcana", "Levanta una barrera arcana.", f, guard: 12);
                    }
                case "cazador":
                    switch (step % 3)
                    {
                        case 0: return Javelin(f, 8);
                        case 1: return Make("snow_stalk", "Acechar en la Nieve", "Se esconde en la nieve: gana 1 de Fuerza y se cubre.", f, block: 6, buffs: St((StatusKind.Strength, 1)));
                        default: return Make("bolt_volley", "Ráfaga de Virotes", "Tres virotes seguidos.", f, attack: 3, hits: 3);
                    }
                case "anima":
                    switch (step % 3)
                    {
                        case 0: return Make("chill_touch", "Toque Gélido", "Un toque helado: Congelación 2 turnos (al terminar tu turno pierdes 1 de vida por carta que te quede en la mano).", f,
                            attack: 6, debuffs: St((StatusKind.Frostbite, 2)));
                        case 1: return Make("frost_veil", "Velo de Escarcha", "Se envuelve en escarcha y te deja Débil 1 turno.", f, block: 10, debuffs: St((StatusKind.Weak, 1)));
                        default: return Make("icicles", "Carámbanos", "Te lanza dos carámbanos.", f, attack: 4, hits: 2);
                    }
                case "ensamblador":
                    switch (step % 3)
                    {
                        case 0:
                            if (Room(e, enemies, "automata", 1) >= 1)
                                return Make("assemble", "Ensamblar", "Monta un Autómata de Cuerda (máximo 3 enemigos en pantalla) y se cubre.", f, block: 6, extras: Summon("automata", 1, 1.0));
                            return Make("tune_up", "Ajustar Tuercas", (BudgetLeft(e) ? "No caben más autómatas" : "Ya no le quedan piezas") + ": da 2 de Fuerza a sus aliados y se cubre.", f,
                                block: 6, allyBuffs: St((StatusKind.Strength, 2)));
                        case 1: return Make("wrench", "Llave Inglesa", "Un golpe con la llave inglesa.", f, attack: 9);
                        default: return Make("spark", "Chispazo", "Un chispazo que te deja Frágil 1 turno.", f, attack: 5, debuffs: St((StatusKind.Frail, 1)));
                    }
                case "automata":
                    return step % 2 == 0 ? Make("bolt_shot", "Perno", "Dispara un perno que atraviesa tu escudo.", f, attack: 3, extras: new IntentExtras { Pierce = true })
                        : Make("wind_up", "Dar Cuerda", "Se da cuerda: gana 1 de Fuerza y se cubre.", f, block: 3, buffs: St((StatusKind.Strength, 1)));
                case "escudero":
                    return step % 2 == 0 ? Make("shield_up", "Escudo en Alto", "Alza el escudo: se cubre y da 4 de escudo a sus aliados.", f, guard: 5, allyBlock: 4)
                        : Make("spear_thrust", "Lanzada", "Una lanzada.", f, attack: 5);
                case "filacteria":
                    return step % 2 == 0 ? Make("cold_pulse", "Pulso Helado", "Late con frío: se cubre y da 10 de escudo a su señor.", f, guard: 6, allyBlock: 10)
                        : Make("life_beat", "Latido Vital", "Un latido de vida robada: cura 6 a todos.", f, healAllies: 6);
                case "comandante":
                {
                    var two = e.PhaseTwo;
                    switch (step % 4)
                    {
                        case 0:
                            if (Room(e, enemies, "escudero", 1) >= 1)
                                return Make("call_guard", "Llamar a la Guardia", "Llama a un Escudero (máximo 3 enemigos en pantalla) y se cubre.", f, block: 6, extras: Summon("escudero", 1, 1.0));
                            return Make("close_ranks", "Formación Cerrada", (BudgetLeft(e) ? "No caben más escuderos" : "Ya no le quedan escuderos") + ": se cubre y da 8 de escudo a sus aliados.", f,
                                guard: 10, allyBlock: 8);
                        case 1: return Make("command_thrust", "Estocada de Mando", "Una estocada de comandante.", f, attack: two ? 14 : 12);
                        case 2: return Make("bugle_call", "Toque de Corneta", "Toca la corneta: gana 1 de Pendón y se cubre.", f, block: 8, buffs: St((StatusKind.Banner, 1)));
                        default: return Make("piercing_lance", "Lanza Perforante", "Una lanzada que atraviesa tu escudo.", f, attack: two ? 12 : 10, extras: new IntentExtras { Pierce = true });
                    }
                }
                case "wendigo":
                {
                    var two = e.PhaseTwo;
                    switch (step % 3)
                    {
                        case 0: return Make("twin_claws", "Zarpazo Doble", "Dos zarpazos.", f, attack: two ? 6 : 5, hits: 2);
                        case 1: return Make("frozen_howl", "Aullido Gélido", "Un aullido helado: Congelación 2 turnos y Débil 1 turno. Se cubre.", f,
                            block: 8, debuffs: St((StatusKind.Frostbite, 2), (StatusKind.Weak, 1)));
                        default: return Make("devour_flesh", "Devorar", "Te muerde y se cura lo que te quite.", f, attack: two ? 13 : 11, lifesteal: true);
                    }
                }
                case "cronomante":
                {
                    var two = e.PhaseTwo;
                    switch (step % 4)
                    {
                        case 0: return Make("slow_time", "Ralentizar", "Ralentiza tu tiempo: Agotamiento 1 turno (empiezas tu turno con 1 de maná menos).", f,
                            attack: 8, debuffs: St((StatusKind.Exhaustion, 1)));
                        case 1: return Make("paradox", "Paradoja", "Tres golpes que llegan a la vez.", f, attack: two ? 6 : 5, hits: 3);
                        case 2: return Rewind(f);
                        default: return Make("future_strike", "Golpe Anticipado", "Un golpe que ya te había alcanzado antes de empezar.", f, attack: two ? 17 : 15);
                    }
                }
                case "liche":
                    if (!e.PhaseTwo)
                        switch (step % 4)
                        {
                            case 0: return Make("blizzard", "Ventisca", "Tres ráfagas heladas que te dejan Congelación 1 turno.", f, attack: 4, hits: 3, debuffs: St((StatusKind.Frostbite, 1)));
                            case 1: return FrostLance(f, 11);
                            case 2: return Make("ice_shroud", "Mortaja de Hielo", "Se envuelve en hielo y cubre a su Filacteria con 8 de escudo.", f, guard: 14, allyBlock: 8);
                            default: return VoidTouch(f, 8);
                        }
                    switch (step % 3)
                    {
                        case 0: return FrostLance(f, 13);
                        case 1: return Make("blizzard", "Ventisca", "Tres ráfagas heladas que te dejan Congelación 2 turnos.", f, attack: 5, hits: 3, debuffs: St((StatusKind.Frostbite, 2)));
                        default: return VoidTouch(f, 10);
                    }
                case "relojero":
                {
                    var two = e.PhaseTwo;
                    switch (step % 4)
                    {
                        case 0:
                            var n = Room(e, enemies, "automata", 2);
                            if (n > 0) return Make("build_automata", "Montar Autómatas", n == 2 ? "Monta dos Autómatas de Cuerda y se cubre." : "Monta un Autómata de Cuerda y se cubre.", f,
                                block: 8, extras: Summon("automata", n, 1.0));
                            return Make("oil_gears", "Engrasar Engranajes", (BudgetLeft(e) ? "No caben más autómatas" : "Ya no le quedan piezas") + ": da 2 de Fuerza a sus aliados y se cubre.", f,
                                block: 8, allyBuffs: St((StatusKind.Strength, 2)));
                        case 1: return Make("clock_hands", "Manecillas", "Dos tajos con las manecillas del reloj.", f, attack: two ? 7 : 6, hits: 2);
                        case 2: return Make("stop_time", "Detener el Tiempo", "Detiene tu tiempo: Agotamiento " + (two ? 2 : 1) + (two ? " turnos" : " turno") + " (empiezas tu turno con 1 de maná menos). Se cubre.", f,
                            block: 12, debuffs: St((StatusKind.Exhaustion, two ? 2 : 1)));
                        default: return Chime(e);
                    }
                }
                case "dragona":
                    if (!e.PhaseTwo)
                        switch (step % 4)
                        {
                            case 0: return FrostBreath(f, 4);
                            case 1: return PiercingClaw(f, 11);
                            case 2: return Make("fold_wings", "Plegar las Alas", "Se envuelve en sus alas de hielo.", f, guard: 18);
                            default: return Make("glacier_tail", "Coletazo Glaciar", "Un coletazo como un glaciar.", f, attack: 17);
                        }
                    switch (step % 3)
                    {
                        case 0: return FrostBreath(f, 5);
                        case 1: return PiercingClaw(f, 13);
                        default: return Make("glacier_tail", "Coletazo Glaciar", "Un coletazo como un glaciar.", f, attack: 19);
                    }
            }
            throw new ArgumentException("Unknown enemy: " + e.Definition.Ai);
        }

        private static EnemyIntent Javelin(int f, int attack) =>
            Make("ice_javelin", "Jabalina de Hielo", "Una jabalina de hielo que atraviesa tu escudo.", f, attack: attack, extras: new IntentExtras { Pierce = true });
        private static EnemyIntent FrostLance(int f, int attack) =>
            Make("frost_lance", "Lanza Gélida", "Una lanza de hielo que atraviesa tu escudo.", f, attack: attack, extras: new IntentExtras { Pierce = true });
        private static EnemyIntent VoidTouch(int f, int attack) =>
            Make("void_touch", "Toque del Vacío", "Un toque del vacío: Agotamiento 1 turno (empiezas tu turno con 1 de maná menos).", f, attack: attack, debuffs: St((StatusKind.Exhaustion, 1)));
        private static EnemyIntent FrostBreath(int f, int attack) =>
            Make("frost_breath", "Aliento Gélido", "Tres bocanadas de aliento helado que te dejan Congelación 2 turnos.", f, attack: attack, hits: 3, debuffs: St((StatusKind.Frostbite, 2)));
        private static EnemyIntent PiercingClaw(int f, int attack) =>
            Make("piercing_claw", "Garra Perforante", "Un zarpazo que atraviesa tu escudo.", f, attack: attack, extras: new IntentExtras { Pierce = true });
        private static EnemyIntent Rewind(int f) =>
            Make("rewind", "Rebobinar", "Rebobina el tiempo: recuperará toda la vida que le quites este turno y se cubre.", f, block: 8, extras: new IntentExtras { Rewind = true, Dynamic = true });

        /// <summary>Campanada: one hit per Cuerda (at least 1); afterwards Cuerda goes back to 1. Recalculated before it acts.</summary>
        private static EnemyIntent Chime(EnemyState e)
        {
            var spring = Math.Max(1, e.Statuses[StatusKind.Spring]);
            return Make("chime", "Campanada", "Da la hora: un golpe por cada Cuerda que tenga. Después le queda 1 de Cuerda.", e.Floor, attack: 4, hits: spring,
                extras: new IntentExtras { Dynamic = true, SetSelf = St((StatusKind.Spring, 1)) });
        }

        private static bool AnyAlive(EnemyState self, IReadOnlyList<EnemyState> enemies, string ai)
        {
            foreach (var other in enemies) if (other != self && other.Alive && other.Definition.Ai == ai) return true;
            return false;
        }

        /// <summary>
        /// Pendón and Inmortal, recomputed from who is alive: each enemy's Inspiración is the Pendón of the other living enemies;
        /// El Rey Liche is Inmortal while a Filacteria lives. A no-op when no enemy has those statuses or that profile.
        /// </summary>
        public static void Floor3Auras(IReadOnlyList<EnemyState> enemies)
        {
            long banners = 0;
            foreach (var e in enemies) if (e.Alive) banners += e.Statuses[StatusKind.Banner];
            foreach (var e in enemies)
            {
                var own = e.Alive ? e.Statuses[StatusKind.Banner] : 0;
                var inspired = e.Alive ? (int)Math.Min(int.MaxValue, banners - own) : 0;
                if (e.Statuses[StatusKind.Inspired] != inspired) e.Statuses.Set(StatusKind.Inspired, inspired);
                var anchor = EnemyCatalog.Profile(e.Definition.Ai)?.UndyingWhile;
                if (anchor == null) continue;
                var keepers = 0;
                if (e.Alive) foreach (var other in enemies) if (other != e && other.Alive && other.Definition.Ai == anchor) keepers++;
                if (e.Statuses[StatusKind.Undying] != keepers) e.Statuses.Set(StatusKind.Undying, keepers);
            }
        }

        /// <summary>Floor-3 dynamic intents: Campanada follows the Cuerda.</summary>
        private static void Floor3Refresh(EnemyState e, IReadOnlyList<EnemyState> enemies)
        {
            if (e.Intent == null || !e.Intent.Extras.Dynamic) return;
            if (e.Intent.Id == "chime") { var fresh = Chime(e); if (fresh.Hits != e.Intent.Hits) e.Intent = fresh; }
        }
    }
}
