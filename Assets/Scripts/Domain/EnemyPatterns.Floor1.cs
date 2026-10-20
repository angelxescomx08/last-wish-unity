using System;
using System.Collections.Generic;
using System.Linq;

namespace LastWish.Domain
{
    /// <summary>
    /// Intent patterns of the floor-1 expansion (hand-written). Every number and Spanish intent text of the new
    /// enemies lives here. Kinds follow one rule: Attack if it hits; else Summon if it adds enemies (summon, split,
    /// revive); else Block if it is a plain "gain block" move; else Debuff if it hurts the hero's statuses, deck,
    /// Strength or block; else Buff. Aturdido is Stun.
    /// </summary>
    public static partial class EnemyPatterns
    {
        private static readonly StatusAmount[] NoStatuses = Array.Empty<StatusAmount>();
        private static StatusAmount[] St(params (StatusKind kind, int amount)[] list)
        {
            var a = new StatusAmount[list.Length];
            for (var i = 0; i < list.Length; i++) a[i] = new StatusAmount(list[i].kind, list[i].amount);
            return a;
        }
        private static StatusCardSpawn[] Wounds(int count, string pile) => new[] { new StatusCardSpawn("status_herida", count, pile) };

        /// <summary>Builds an intent of the expansion; values (attack, block, ally block, heals) are given at floor 1 and scaled here.</summary>
        private static EnemyIntent Make(string id, string name, string description, int floor, int attack = 0, int hits = 1, int guard = 0, int block = 0,
            StatusAmount[] buffs = null, StatusAmount[] debuffs = null, StatusAmount[] allyBuffs = null, StatusCardSpawn[] cards = null,
            int allyBlock = 0, int healAllies = 0, IntentExtras extras = null)
        {
            var x = extras ?? IntentExtras.None;
            IntentKind kind; var value = 0;
            if (attack > 0) { kind = IntentKind.Attack; value = EnemyCatalog.Scale(attack, floor); }
            else if (x.Summons.Length > 0 || x.ReviveAi != null || x.SplitInto != null) kind = IntentKind.Summon;
            else if (guard > 0) { kind = IntentKind.Block; value = EnemyCatalog.Scale(guard, floor); }
            else if (debuffs != null && debuffs.Length > 0 || cards != null && cards.Length > 0 || x.StealStrength > 0 || x.SiphonBlockPercent > 0 || x.Swallow > 0) kind = IntentKind.Debuff;
            else kind = IntentKind.Buff;
            return new EnemyIntent(kind, value, hits, name, id, description, block: EnemyCatalog.Scale(block, floor), buffs: buffs, debuffs: debuffs,
                allyBuffs: allyBuffs, cards: cards, allyBlock: EnemyCatalog.Scale(allyBlock, floor), healAllies: EnemyCatalog.Scale(healAllies, floor)) { Extras = x };
        }

        /// <summary>The intent of an enemy whose Carga was broken: it loses its turn.</summary>
        public static EnemyIntent Stunned(int floor) => new EnemyIntent(IntentKind.Stun, 0, 1, "Aturdido", "stunned", "Está Aturdido: pierde su turno.");

        private static IntentExtras Summon(string ai, int count, double hpFactor) => new IntentExtras { Summons = new[] { new SummonSpec(ai, count, hpFactor, false) } };

        /// <summary>How many <paramref name="ai"/> the enemy can summon now (screen cap and its per-fight budget).</summary>
        private static int Room(EnemyState enemy, IReadOnlyList<EnemyState> enemies, string ai, int want)
        {
            var profile = EnemyCatalog.Profile(enemy.Definition.Ai);
            var budget = profile == null ? 0 : Math.Max(0, profile.SummonBudget - enemy.SummonsMade);
            var free = EnemyField.Free(enemies, null, EnemyField.IsLarge(EnemyCatalog.Definition(ai)));
            return Math.Max(0, Math.Min(want, Math.Min(free, budget)));
        }
        private static bool BudgetLeft(EnemyState enemy)
        {
            var profile = EnemyCatalog.Profile(enemy.Definition.Ai);
            return profile != null && enemy.SummonsMade < profile.SummonBudget;
        }

        private static bool HasAlly(EnemyState enemy, IReadOnlyList<EnemyState> enemies)
        {
            foreach (var ally in enemies) if (ally != enemy && ally.Alive) return true;
            return false;
        }

        private static EnemyState Partner(EnemyState enemy, IReadOnlyList<EnemyState> enemies)
        {
            if (string.IsNullOrEmpty(enemy.PartnerId)) return null;
            foreach (var other in enemies) if (other.Id == enemy.PartnerId) return other;
            return null;
        }

        /// <summary>The partner exists in the fight and is dead.</summary>
        private static bool PartnerFallen(EnemyState enemy, IReadOnlyList<EnemyState> enemies)
        {
            var partner = Partner(enemy, enemies);
            return partner != null && !partner.Alive;
        }

        // ------------------------------------------------------------------ planning
        private static EnemyIntent ExpansionNext(EnemyState enemy, StatusSet hero, IReadOnlyList<EnemyState> enemies)
        {
            var profile = EnemyCatalog.Profile(enemy.Definition.Ai);
            EnemyIntent intent = null;
            if (profile.HasPhase && !enemy.PhaseTwo && enemy.Hp * 2L <= enemy.MaxHp && enemy.Statuses[StatusKind.Focus] == 0)
            { enemy.PhaseTwo = true; intent = PhaseIntent(enemy, enemies); }
            if (intent == null) intent = SpecialIntent(enemy, enemies, false);
            if (intent == null) intent = CycleIntent(enemy, enemy.Step++, enemies);
            if (!HasAlly(enemy, enemies)) { intent.AllyBuffs = NoStatuses; intent.AllyBlock = 0; }
            return intent;
        }

        /// <summary>The one-time 50 % transition of the new elites and solo bosses.</summary>
        private static EnemyIntent PhaseIntent(EnemyState e, IReadOnlyList<EnemyState> enemies)
        {
            var f = e.Floor;
            switch (e.Definition.Ai)
            {
                case "rey_rata":
                    return Room(e, enemies, "rata", 1) >= 1
                        ? Make("plague_fury", "Furia de la Plaga", "Enfurece: gana 3 de Fuerza y llama a otra Rata.", f, buffs: St((StatusKind.Strength, 3)), extras: Summon("rata", 1, 0.8))
                        : Make("plague_fury", "Furia de la Plaga", "Enfurece: gana 3 de Fuerza.", f, buffs: St((StatusKind.Strength, 3)));
                case "coloso":
                    return Make("overheat", "Sobrecalentar", "Se sobrecalienta: gana 2 de Fuerza y se cubre. Su caldera arde más (Carga 20, Martillo 28).", f, block: 10, buffs: St((StatusKind.Strength, 2)));
                case "duelista":
                    return Make("second_wind", "Segundo Aliento", "Recupera el aliento: gana 2 Desvío y 2 de Fuerza.", f, buffs: St((StatusKind.Parry, 2), (StatusKind.Strength, 2)));
                case "inquisidor":
                    e.Step = 0;
                    return Make("auto_de_fe", "Auto de Fe", "Enciende la hoguera: ahora castiga desde tu 3.ª carta del turno, gana 2 de Fuerza y se cubre.", f, block: 12,
                        buffs: St((StatusKind.Strength, 2)), extras: new IntentExtras { SetSelf = St((StatusKind.Vigil, 2)) });
                case "sierpe":
                    e.Step = 0;
                    return Make("new_skin", "Piel Nueva", "Estrena piel: se libra de todo, se cura 15 y gana 2 de Fuerza.", f, buffs: St((StatusKind.Strength, 2)),
                        extras: new IntentExtras { Cleanse = true, HealSelf = EnemyCatalog.Scale(15, f) });
            }
            return null;
        }

        /// <summary>States that replace the cycle: the Cieno's split and the paired bosses' reactions (design §5.13, §5.15).</summary>
        private static EnemyIntent SpecialIntent(EnemyState e, IReadOnlyList<EnemyState> enemies, bool reacting)
        {
            var f = e.Floor;
            switch (e.Definition.Ai)
            {
                case "cieno":
                    if (reacting || e.SplitDone || e.Hp * 2L > e.MaxHp) return null;
                    return Make("split", "Dividirse", "Se parte en dos Cienos Menores con la mitad de su vida cada uno.", f, extras: new IntentExtras { SplitInto = "cieno_menor", SplitCount = 2 });
                case "sepulturero":
                    if (!PartnerFallen(e, enemies) || reacting && e.PartnerHandled) return null;
                    if (e.AwaitingExhume) return Exhume(f);
                    if (!e.ReviveUsed)
                    {
                        e.AwaitingExhume = true; e.PartnerHandled = true;
                        return Make("dig_grave", "Cavar Tumba", "Cava la tumba de su hermana: al turno siguiente la desentierra. Quítale 16 de vida mientras cava y no podrá hacerlo.", f,
                            extras: new IntentExtras { SetSelf = St((StatusKind.Focus, 16)) });
                    }
                    if (!e.GriefUsed)
                    {
                        e.GriefUsed = true; e.PartnerHandled = true; e.Step = 0;
                        return Make("grief", "Duelo", "Llora a su hermana: gana 3 de Fuerza y se cubre.", f, block: 10, buffs: St((StatusKind.Strength, 3)));
                    }
                    return null;
                case "planidera":
                    if (!PartnerFallen(e, enemies) || e.PartnerHandled) return null;
                    e.PartnerHandled = true; e.Step = 0;
                    var strength = Partner(e, enemies).Statuses[StatusKind.Strength] + 3;
                    return Make("soul_drain", "Alma en Pena", "Absorbe el alma de su hermano: gana su Fuerza más 3, se cura 20 y se cubre.", f, block: 10,
                        buffs: St((StatusKind.Strength, strength)), extras: new IntentExtras { HealSelf = EnemyCatalog.Scale(20, f) });
            }
            return null;
        }

        private static EnemyIntent Exhume(int f) =>
            Make("exhume", "Exhumar", "Desentierra a La Plañidera con la mitad de su vida. Quítale 16 de vida antes de que actúe y quedará Aturdido.", f,
                extras: new IntentExtras { ReviveAi = "planidera", ConsumesFocus = true });

        private static EnemyIntent CycleIntent(EnemyState e, int step, IReadOnlyList<EnemyState> enemies)
        {
            var f = e.Floor;
            switch (e.Definition.Ai)
            {
                case "madre_larva":
                    if (step % 3 == 1) return Make("spit", "Escupitajo Pegajoso", "Te escupe baba pegajosa: Débil 1 turno.", f, attack: 6, debuffs: St((StatusKind.Weak, 1)));
                    if (Room(e, enemies, "larva", 1) >= 1)
                        return Make("lay", "Puesta", "Pone una Larva (máximo 3 enemigos en pantalla) y se cubre.", f, block: 5, extras: Summon("larva", 1, 1.0));
                    return Make("feed", "Nutrir Camada", BudgetLeft(e) ? "No cabe más camada: da 2 de Fuerza a sus aliados y cura 4 de vida a todos." : "Ya no pone más huevos: da 2 de Fuerza a sus aliados y cura 4 de vida a todos.", f,
                        allyBuffs: St((StatusKind.Strength, 2)), healAllies: 4);
                case "erizo":
                    switch (step % 3)
                    {
                        case 0: return Make("roll", "Rodar", "Se hace una bola de huesos y rueda sobre ti.", f, attack: 7);
                        case 1: return Make("bristle", "Erizarse", "Eriza sus púas: gana 2 Pinchos y se cubre.", f, block: 6, buffs: St((StatusKind.Thorns, 2)));
                        default: return Make("quills", "Lluvia de Púas", "Te dispara tres púas.", f, attack: 3, hits: 3);
                    }
                case "sapo":
                    switch (step % 3)
                    {
                        case 0: return Make("tongue", "Lengüetazo", "Te azota con la lengua: Débil 1 turno.", f, attack: 5, debuffs: St((StatusKind.Weak, 1)));
                        case 1: return Make("swallow", "Tragar", "Se traga una carta al azar de tu descarte (vuelve a tu descarte cuando muera) y se cubre.", f, block: 6, extras: new IntentExtras { Swallow = 1 });
                        default: return Make("belly", "Panzazo", "Se deja caer con toda su barriga.", f, attack: 12);
                    }
                case "cuervo":
                    switch (step % 3)
                    {
                        case 0: return Make("peck", "Picoteo", "Dos picotazos rápidos.", f, attack: 3, hits: 2);
                        case 1: return Make("caw", "Graznido", "Un graznido que te deja Vulnerable 2 turnos.", f, debuffs: St((StatusKind.Vulnerable, 2)));
                        default: return Make("gouge", "Picotazo al Ojo", "Va directo a tus ojos.", f, attack: 9);
                    }
                case "rata":
                    if (step % 3 == 2) return Make("squeak", "Chillido", "Chilla: ella y sus aliados ganan 1 de Fuerza.", f, buffs: St((StatusKind.Strength, 1)), allyBuffs: St((StatusKind.Strength, 1)));
                    return PackBite(e, enemies);
                case "escarabajo":
                    switch (step % 3)
                    {
                        case 0: return Make("curl", "Enroscarse", "Se encierra en su caparazón.", f, guard: 8);
                        case 1: return Make("horn", "Cornada", "Te embiste con el cuerno.", f, attack: 8);
                        default: return Ram(e);
                    }
                case "cieno":
                    switch (step % 3)
                    {
                        case 0: return Make("engulf", "Engullir", "Te envuelve en su masa gelatinosa.", f, attack: 9);
                        case 1: return Make("sticky", "Baba Pegajosa", "Te pega con baba: robas 1 carta menos.", f, attack: 5, debuffs: St((StatusKind.Entangled, 1)));
                        default: return Make("gel", "Gelificarse", "Se endurece.", f, guard: 10);
                    }
                case "larva":
                    if (step % 3 == 2) return Make("harden", "Endurecerse", "Endurece su piel: gana 1 de Fuerza y se cubre.", f, block: 3, buffs: St((StatusKind.Strength, 1)));
                    return Make("gnaw", "Roer", "Te roe con sus mandíbulas diminutas.", f, attack: 3);
                case "cieno_menor":
                    return step % 2 == 0 ? Make("nibble", "Mordisco Viscoso", "Te muerde con su masa viscosa.", f, attack: 5)
                        : Make("glob", "Glóbulo", "Te lanza un glóbulo: Frágil 1 turno.", f, attack: 3, debuffs: St((StatusKind.Frail, 1)));
                case "vibora":
                    return step % 2 == 0 ? Make("venom_fang", "Colmillo Venenoso", "Muerde y te da 2 de Veneno.", f, attack: 3, debuffs: St((StatusKind.Poison, 2)))
                        : Make("lurk", "Acechar", "Se enrosca y acecha: gana 1 de Fuerza y se cubre.", f, block: 4, buffs: St((StatusKind.Strength, 1)));
                case "rey_rata":
                    switch (step % 4)
                    {
                        case 0:
                            var rats = Room(e, enemies, "rata", 2);
                            if (rats == 2) return Make("call_swarm", "Llamar a la Plaga", "Llama a dos Ratas de Cripta y se cubre.", f, block: 6, extras: Summon("rata", 2, 0.8));
                            if (rats == 1) return Make("call_swarm", "Llamar a la Plaga", "Llama a una Rata de Cripta y se cubre.", f, block: 6, extras: Summon("rata", 1, 0.8));
                            return Make("rally", "Arenga Real", BudgetLeft(e) ? "No caben más ratas: da 2 de Fuerza a su corte y se cubre." : "Ya no le quedan ratas que llamar: da 2 de Fuerza a su corte y se cubre.", f,
                                block: 6, allyBuffs: St((StatusKind.Strength, 2)));
                        case 1: return Make("royal_bite", "Mordisco Real", "Un mordisco con dientes de oro.", f, attack: 10);
                        case 2: return Make("court_feast", "Festín de la Corte", "Banquete de carroña: cura 5 a todos y da 1 de Fuerza a sus ratas.", f, healAllies: 5, allyBuffs: St((StatusKind.Strength, 1)));
                        default: return Make("tail_whip", "Latigazo de Colas", "Azota con su nudo de colas.", f, attack: 4, hits: 3);
                    }
                case "coloso":
                    var hot = e.PhaseTwo;
                    switch (step % 3)
                    {
                        case 0: return Make("iron_step", "Pisada de Hierro", "Avanza con un pisotón de hierro y se cubre.", f, attack: 7, block: 7);
                        case 1:
                            return hot
                                ? Make("stoke", "Avivar la Caldera", "Aviva su caldera al rojo: al turno siguiente descarga el Martillo Pilón. Quítale 20 de vida mientras se concentra y quedará Aturdido.", f, extras: new IntentExtras { SetSelf = St((StatusKind.Focus, 20)) })
                                : Make("stoke", "Avivar la Caldera", "Aviva su caldera: al turno siguiente descarga el Martillo Pilón. Quítale 16 de vida mientras se concentra y quedará Aturdido.", f, extras: new IntentExtras { SetSelf = St((StatusKind.Focus, 16)) });
                        default:
                            return hot
                                ? Make("pile_driver", "Martillo Pilón", "Descarga todo el peso de su martillo al rojo. Quítale 20 de vida antes de que actúe y quedará Aturdido.", f, attack: 28, extras: new IntentExtras { ConsumesFocus = true })
                                : Make("pile_driver", "Martillo Pilón", "Descarga todo el peso de su martillo. Quítale 16 de vida antes de que actúe y quedará Aturdido.", f, attack: 24, extras: new IntentExtras { ConsumesFocus = true });
                    }
                case "duelista":
                    switch (step % 4)
                    {
                        case 0: return Make("en_garde", "En Guardia", "Se pone en guardia: desviará el próximo golpe y te responderá con 6 de daño.", f, block: 5, buffs: St((StatusKind.Parry, 1)));
                        case 1: return Make("lunge", "Estocada", "Una estocada precisa.", f, attack: 11);
                        case 2: return Make("feint", "Finta y Réplica", "Dos fintas rápidas y vuelve a la guardia: gana 1 Desvío.", f, attack: 5, hits: 2, buffs: St((StatusKind.Parry, 1)));
                        default: return Make("fleche", "Flecha", "Se lanza a fondo con el estoque.", f, attack: 13);
                    }
                case "inquisidor":
                    if (!e.PhaseTwo)
                        switch (step % 5)
                        {
                            case 0: return Make("edict", "Edicto", "Lee un edicto: te deja Débil 2 turnos y se cubre.", f, block: 10, debuffs: St((StatusKind.Weak, 2)));
                            case 1: return Confiscate(f);
                            case 2: return Verdict(f);
                            case 3: return Judgment(f, 12);
                            default: return Make("expiation", "Expiación", "Te impone expiación: baraja 2 Heridas en tu pila de robo y se cubre.", f, block: 8, cards: Wounds(2, "draw"));
                        }
                    switch (step % 4)
                    {
                        case 0: return Verdict(f);
                        case 1: return Judgment(f, 14);
                        case 2: return Confiscate(f);
                        default: return Make("pyre", "Hoguera", "Una columna de fuego: 18 de daño y 1 Herida en tu descarte.", f, attack: 18, cards: Wounds(1, "discard"));
                    }
                case "sierpe":
                    if (!e.PhaseTwo)
                        switch (step % 4)
                        {
                            case 0:
                                return Room(e, enemies, "vibora", 1) >= 1
                                    ? Make("brood", "Llamar a la Nidada", "Llama a una Víbora de su nidada y se cubre.", f, block: 6, extras: Summon("vibora", 1, 1.0))
                                    : Hiss(e, f);
                            case 1: return Make("fangs", "Colmillos", "Muerde y te da 3 de Veneno.", f, attack: 7, debuffs: St((StatusKind.Poison, 3)));
                            case 2: return Make("constrict", "Constricción", "Te estruja: robas 1 carta menos.", f, attack: 10, debuffs: St((StatusKind.Entangled, 1)));
                            default: return Molt(f);
                        }
                    switch (step % 4)
                    {
                        case 0:
                            var vipers = Room(e, enemies, "vibora", 2);
                            if (vipers == 2) return Make("brood_frenzy", "Nidada Furiosa", "Llama a dos Víboras y se cubre.", f, block: 6, extras: Summon("vibora", 2, 1.0));
                            if (vipers == 1) return Make("brood_frenzy", "Nidada Furiosa", "Llama a una Víbora y se cubre.", f, block: 6, extras: Summon("vibora", 1, 1.0));
                            return Hiss(e, f);
                        case 1: return Make("death_bite", "Mordisco Letal", "Un mordisco letal: 14 de daño y 4 de Veneno.", f, attack: 14, debuffs: St((StatusKind.Poison, 4)));
                        case 2: return Molt(f);
                        default: return Make("tail", "Coletazo", "Tres coletazos.", f, attack: 4, hits: 3);
                    }
                case "sepulturero":
                    if (PartnerFallen(e, enemies))
                        return step % 2 == 0 ? Make("shovel_heavy", "Palazo Brutal", "Un palazo con todas sus fuerzas.", f, attack: 14)
                            : Make("slab", "Losa", "Te deja caer una losa: Frágil 2 turnos.", f, attack: 8, debuffs: St((StatusKind.Frail, 2)));
                    switch (step % 3)
                    {
                        case 0: return Make("dig_in", "Cavar Trinchera", "Se atrinchera y cubre a La Plañidera con 10 de escudo.", f, guard: 8, allyBlock: 10);
                        case 1: return Make("shovel", "Palazo", "Un palazo con la pala de enterrador.", f, attack: 9);
                        default: return Make("requiem", "Réquiem", "Golpea a la vez que su hermana.", f, attack: 4, hits: 2);
                    }
                case "planidera":
                    if (PartnerFallen(e, enemies))
                        switch (step % 3)
                        {
                            case 0: return Make("banshee", "Grito de Ultratumba", "Dos gritos que desgarran.", f, attack: 7, hits: 2);
                            case 1: return Make("curse", "Maldición", "Te maldice: Débil y Vulnerable 2 turnos.", f, debuffs: St((StatusKind.Weak, 2), (StatusKind.Vulnerable, 2)));
                            default: return Make("last_wail", "Llanto Final", "Un llanto final devastador.", f, attack: 16);
                        }
                    switch (step % 3)
                    {
                        case 0: return Make("lament", "Lamento", "Llora: te deja Débil y Frágil y da 1 de Fuerza a su hermano.", f, debuffs: St((StatusKind.Weak, 1), (StatusKind.Frail, 1)), allyBuffs: St((StatusKind.Strength, 1)));
                        case 1: return Make("wail", "Alarido", "Un alarido que te hiere dos veces y te deja 1 Herida en el descarte.", f, attack: 3, hits: 2, cards: Wounds(1, "discard"));
                        default: return Make("requiem", "Réquiem", "Golpea a la vez que su hermano.", f, attack: 4, hits: 2);
                    }
            }
            throw new ArgumentException("Unknown enemy: " + e.Definition.Ai);
        }

        private static EnemyIntent Confiscate(int f) =>
            Make("confiscate", "Confiscación", "Confisca toda tu Fuerza y se la queda (si no tienes, gana 2). Te deja Frágil 2 turnos.", f,
                debuffs: St((StatusKind.Frail, 2)), extras: new IntentExtras { StealStrength = int.MaxValue, StrengthFallback = 2 });
        private static EnemyIntent Verdict(int f) => Make("verdict", "Veredicto", "Tres golpes de maza; cada Fuerza que tenga suma a cada golpe.", f, attack: 5, hits: 3);
        private static EnemyIntent Judgment(int f, int attack) =>
            Make("judgment", "Juicio", "Te arranca la mitad de tu escudo, se lo queda y golpea.", f, attack: attack, extras: new IntentExtras { SiphonBlockPercent = 50 });
        private static EnemyIntent Molt(int f) =>
            Make("molt", "Muda", "Muda la piel: se libra de sus perjuicios y elementos y gana 2 de escudo por cada acumulación (máx. 30).", f,
                extras: new IntentExtras { Cleanse = true, CleanseBlockPerStack = 2, CleanseBlockCap = EnemyCatalog.Scale(30, f) });
        private static EnemyIntent Hiss(EnemyState e, int f) =>
            Make("hiss", "Siseo", BudgetLeft(e) ? "No caben más crías: da 2 de Fuerza a sus Víboras y se cubre." : "Su nidada se ha agotado: da 2 de Fuerza a sus Víboras y se cubre.", f,
                block: 6, allyBuffs: St((StatusKind.Strength, 2)));

        private static EnemyIntent PackBite(EnemyState e, IReadOnlyList<EnemyState> enemies)
        {
            var others = 0;
            foreach (var other in enemies) if (other != e && other.Alive && other.Definition.Ai == "rata") others++;
            return Make("pack_bite", "Mordisco de Manada", "Muerde con la manada: +1 de daño por cada otra Rata viva.", e.Floor, attack: 2 + others, extras: new IntentExtras { Dynamic = true });
        }

        private static EnemyIntent Ram(EnemyState e)
        {
            var kept = Math.Min(e.Block, e.Statuses[StatusKind.Shell]);   // the block it keeps for its turn
            var intent = Make("ram", "Embestida Acorazada", "Te embiste con el caparazón: 4 de daño más la mitad de su escudo.", e.Floor, attack: 4, extras: new IntentExtras { Dynamic = true });
            intent.Value += kept / 2;
            return intent;
        }

        // ------------------------------------------------------------------ immediate reactions
        /// <summary>
        /// Runs after every card, reaction and enemy turn (and at the start of the hero's turn): paired-boss reactions,
        /// broken Carga and dynamic intents. Idempotent; never mutates an intent shared with a preview.
        /// </summary>
        public static void ExpansionReact(IReadOnlyList<EnemyState> enemies)
        {
            for (var i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!e.Alive || !EnemyCatalog.IsExpansion(e.Definition.Ai)) continue;
                if (e.Definition.Ai == "sepulturero" || e.Definition.Ai == "planidera")
                {
                    var replaced = SpecialIntent(e, enemies, true);
                    if (replaced != null) e.Intent = replaced;
                }
                Interrupt(e);
                Refresh(e, enemies);
            }
        }

        /// <summary>Right before the enemy acts (after its poison): a Carga broken now still stuns it.</summary>
        public static void BeforeAct(EnemyState enemy, IReadOnlyList<EnemyState> enemies)
        {
            if (!enemy.Alive || !EnemyCatalog.IsExpansion(enemy.Definition.Ai)) return;
            Interrupt(enemy);
            Refresh(enemy, enemies);
        }

        private static void Interrupt(EnemyState e)
        {
            if (!e.FocusBroken) return;
            e.FocusBroken = false;
            if (e.Intent == null || !e.Intent.Extras.ConsumesFocus) return;
            e.Intent = Stunned(e.Floor);
            e.Statuses.Remove(StatusKind.Focus);
            if (e.Definition.Ai == "sepulturero") { e.ReviveUsed = true; e.AwaitingExhume = false; }
        }

        private static void Refresh(EnemyState e, IReadOnlyList<EnemyState> enemies)
        {
            if (e.Intent == null || !e.Intent.Extras.Dynamic) return;
            var fresh = e.Definition.Ai == "rata" ? PackBite(e, enemies) : e.Definition.Ai == "escarabajo" ? Ram(e) : null;
            if (fresh != null && fresh.Value != e.Intent.Value) e.Intent = fresh;
        }
    }
}
