// Generated from the Python pattern functions; see Tools/ExportEnemyPatterns.py.
namespace LastWish.Domain
{
    public static partial class EnemyPatterns
    {
        private static EnemyIntent Normal(string ai, int step, int floor)
        {
            switch (ai)
            {
                case "espectro":
                    switch (step % 3)
                    {
                        case 0: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(11, floor), 1, "Zarpazo Espectral", "claw", "Te desgarra con sus garras.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(11, floor), 1, "Zarpazo Espectral", "claw", "Te desgarra con sus garras.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Debuff, 0, 1, "Lamento", "wail", "Su lamento te deja Débil 2 turnos y Frágil 1 turno.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Weak, 2), new StatusAmount(StatusKind.Frail, 1) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "babosa":
                    switch (step % 3)
                    {
                        case 0: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(5, floor), 1, "Escupitajo Ácido", "spit", "Te escupe ácido: Frágil 2 turnos (tu escudo rinde menos).", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Frail, 2) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(10, floor), 1, "Golpe Viscoso", "slam", "Se deja caer sobre ti.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Debuff, 0, 1, "Corroer", "corrode", "Corroe tu armadura: Vulnerable 2 turnos. Se cubre un poco.", block: EnemyCatalog.Scale(5, floor), buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Vulnerable, 2) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "gusano":
                    switch (step % 3)
                    {
                        case 0: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(6, floor), 1, "Mordida Pútrida", "bite", "Muerde y te da 3 de Veneno.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Poison, 3) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Block, EnemyCatalog.Scale(12, floor), 1, "Enterrarse", "burrow", "Se mete bajo tierra y gana mucho escudo.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Debuff, 0, 1, "Bilis", "bile", "Te vomita bilis: 5 de Veneno.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Poison, 5) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "ojo":
                    switch (step % 3)
                    {
                        case 0: return new EnemyIntent(IntentKind.Debuff, 0, 1, "Mirada Fija", "stare", "Te clava la mirada: Vulnerable 2 turnos y Débil 1 turno.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Vulnerable, 2), new StatusAmount(StatusKind.Weak, 1) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(8, floor), 1, "Rayo Ocular", "beam", "Dispara un rayo por la pupila.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Debuff, 0, 1, "Pavor", "dread", "Te paraliza de miedo: Enredado (robas 1 carta menos) y Frágil.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Entangled, 1), new StatusAmount(StatusKind.Frail, 1) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "craneo":
                    switch (step % 3)
                    {
                        case 0: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(7, floor), 1, "Llamarada", "flame", "Escupe fuego. Cada Fuerza que gana suma a este golpe.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(7, floor), 1, "Llamarada", "flame", "Escupe fuego. Cada Fuerza que gana suma a este golpe.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Buff, 0, 1, "Avivar", "stoke", "Aviva su fuego: gana 3 de Fuerza para siempre.", block: 0, buffs: new[] { new StatusAmount(StatusKind.Strength, 3) }, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "murcielago":
                    switch (step % 3)
                    {
                        case 0: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(7, floor), 1, "Mordisco Vampírico", "bite", "Muerde y se cura tanta vida como te quite.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: true, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(3, floor), 3, "Picado", "dive", "Cae en picado: tres mordiscos rápidos.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Debuff, 0, 1, "Chillido", "screech", "Un chillido ensordecedor: Débil 2 turnos.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Weak, 2) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "seta":
                    switch (step % 3)
                    {
                        case 0: return new EnemyIntent(IntentKind.Debuff, 0, 1, "Nube de Esporas", "puff", "Suelta esporas: 2 de Veneno.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Poison, 2) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Block, EnemyCatalog.Scale(8, floor), 1, "Hincharse", "swell", "Se hincha y se cubre. La próxima vez, explota.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(24, floor), 1, "¡Explosión!", "explode", "Explota con un golpe enorme y muere. ¡Mátala antes!", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: true, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "golem":
                    switch (step % 3)
                    {
                        case 0: return new EnemyIntent(IntentKind.Block, EnemyCatalog.Scale(10, floor), 1, "Muralla", "wall", "Levanta un muro: escudo para él y para su compañero.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: EnemyCatalog.Scale(8, floor), healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(13, floor), 1, "Puñetazo de Roca", "punch", "Un puñetazo lento y demoledor.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Buff, 0, 1, "Endurecer", "harden", "Endurece la piedra: escudo y 2 de Fuerza.", block: EnemyCatalog.Scale(12, floor), buffs: new[] { new StatusAmount(StatusKind.Strength, 2) }, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "acolito":
                    switch (step % 4)
                    {
                        case 0: return new EnemyIntent(IntentKind.Buff, 0, 1, "Bendición de Ceniza", "bless", "Da 2 de Fuerza a todos sus aliados y a sí mismo.", block: 0, buffs: new[] { new StatusAmount(StatusKind.Strength, 2) }, debuffs: null, allyBuffs: new[] { new StatusAmount(StatusKind.Strength, 2) }, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(6, floor), 1, "Incensario", "censer", "Te golpea con el incensario ardiente.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Debuff, 0, 1, "Penitencia", "penance", "Te impone penitencia: Débil y Frágil 1 turno.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Weak, 1), new StatusAmount(StatusKind.Frail, 1) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 3: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(6, floor), 1, "Incensario", "censer", "Te golpea con el incensario ardiente.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "diablillo":
                    switch (step % 4)
                    {
                        case 0: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(3, floor), 3, "Zarpazos", "claws", "Tres zarpazos seguidos.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(11, floor), 1, "Bola de Fuego", "fireball", "Te lanza una bola de fuego: Vulnerable 1 turno.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Vulnerable, 1) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(3, floor), 3, "Zarpazos", "claws", "Tres zarpazos seguidos.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 3: return new EnemyIntent(IntentKind.Buff, 0, 1, "Burla", "taunt", "Se ríe de ti y gana 2 de Fuerza.", block: 0, buffs: new[] { new StatusAmount(StatusKind.Strength, 2) }, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "mimico":
                    switch (step % 4)
                    {
                        case 0: return new EnemyIntent(IntentKind.Block, EnemyCatalog.Scale(15, floor), 1, "Fingir", "feign", "Se hace pasar por un cofre: gana mucho escudo. Lo próximo es una dentellada.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(18, floor), 1, "Dentellada", "chomp", "Abre la tapa y muerde con todo.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(4, floor), 3, "Lengüetazo", "lick", "Tres latigazos con la lengua: Débil 1 turno.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Weak, 1) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 3: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(12, floor), 1, "Dentellada", "chomp", "Otra dentellada.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "micelida":
                    switch (step % 3)
                    {
                        case 0: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(6, floor), 1, "Lluvia de Esporas", "spores", "Ataca y baraja 2 Esporas en tu pila de robo.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: new[] { new StatusCardSpawn("status_espora", 2, "draw") }, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(10, floor), 1, "Raíces Estranguladoras", "roots", "Ataca y te da 3 de Veneno.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Poison, 3) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Block, EnemyCatalog.Scale(12, floor), 1, "Brote de Moho", "mold", "Se cubre y baraja 2 Moho en tu pila de robo.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: new[] { new StatusCardSpawn("status_moho", 2, "draw") }, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "tejedora":
                    switch (step % 4)
                    {
                        case 0: return new EnemyIntent(IntentKind.Debuff, 0, 1, "Hilos Pegajosos", "web", "Te deja Débil 2 turnos y Enredado (robas 1 carta menos).", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Weak, 2), new StatusAmount(StatusKind.Entangled, 1) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(9, floor), 1, "Colmillo", "fang", "Muerde y te deja Vulnerable 2 turnos.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Vulnerable, 2) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Block, EnemyCatalog.Scale(10, floor), 1, "Capullo de Seda", "cocoon", "Se envuelve en seda y te deja Frágil 2 turnos.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Frail, 2) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 3: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(5, floor), 5, "Banquete", "feast", "Muerde una vez más por cada debuff distinto que tengas.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "caballero":
                    switch (step % 6)
                    {
                        case 0: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(4, floor), 2, "Danza de Espadas", "blade_dance", "Golpea una vez por cada espada flotante.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Buff, 0, 1, "Llamar al Acero", "summon", "Invoca otra espada y se cubre.", block: EnemyCatalog.Scale(8, floor), buffs: new[] { new StatusAmount(StatusKind.Blades, 1) }, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(8, floor), 2, "Estocada Doble", "double", "Dos estocadas rápidas.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 3: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(4, floor), 2, "Danza de Espadas", "blade_dance", "Golpea una vez por cada espada flotante.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 4: return new EnemyIntent(IntentKind.Buff, 0, 1, "Llamar al Acero", "summon", "Invoca otra espada y se cubre.", block: EnemyCatalog.Scale(8, floor), buffs: new[] { new StatusAmount(StatusKind.Blades, 1) }, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 5: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(17, floor), 1, "Tajo del Verdugo", "execute", "Un único tajo devastador.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "verdugo":
                    switch (step % 4)
                    {
                        case 0: return new EnemyIntent(IntentKind.Debuff, 0, 1, "Sentencia", "sentence", "Te señala con el hacha: Vulnerable 2 turnos. Se cubre.", block: EnemyCatalog.Scale(8, floor), buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Vulnerable, 2) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(14, floor), 1, "Hachazo", "chop", "Un hachazo pesado.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Buff, 0, 1, "Afilar", "sharpen", "Afila el hacha: gana 2 de Fuerza y escudo.", block: EnemyCatalog.Scale(10, floor), buffs: new[] { new StatusAmount(StatusKind.Strength, 2) }, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 3: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(21, floor), 1, "Decapitar", "behead", "Levanta el hacha y descarga un golpe enorme.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "bruja":
                    switch (step % 4)
                    {
                        case 0: return new EnemyIntent(IntentKind.Debuff, 0, 1, "Maleficio", "hex", "Te maldice: Débil 2 turnos y Frágil 2 turnos.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Weak, 2), new StatusAmount(StatusKind.Frail, 2) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(9, floor), 1, "Rayo de Ciénaga", "bolt", "Un rayo de fango verde: te da 3 de Veneno.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Poison, 3) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Block, EnemyCatalog.Scale(10, floor), 1, "Caldero Burbujeante", "cauldron", "Remueve el caldero: se cubre y baraja 2 Moho en tu pila de robo.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: new[] { new StatusCardSpawn("status_moho", 2, "draw") }, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 3: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(4, floor), 3, "Muñeco Vudú", "voodoo", "Clava tres alfileres en un muñeco con tu cara.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "gargola":
                    switch (step % 3)
                    {
                        case 0: return new EnemyIntent(IntentKind.Block, EnemyCatalog.Scale(18, floor), 1, "Petrificar", "petrify", "Se convierte en piedra: gana muchísimo escudo.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(5, floor), 3, "Picado", "dive", "Cae en picado: tres zarpazos seguidos.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(11, floor), 1, "Zarpazo de Piedra", "rend", "Un zarpazo de piedra: te deja Frágil 2 turnos.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Frail, 2) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "minotauro":
                    switch (step % 3)
                    {
                        case 0: return new EnemyIntent(IntentKind.Block, EnemyCatalog.Scale(12, floor), 1, "Escarbar", "paw", "Escarba el suelo y resopla: se cubre. Lo próximo es una Embestida.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(15, floor), 1, "Embestida", "charge", "Embiste con los cuernos. Cada Embestida hace 5 más que la anterior.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(9, floor), 1, "Pisotón", "stomp", "Un pisotón que hace temblar el suelo: Enredado (robas 1 carta menos).", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Entangled, 1) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
                case "escorpion":
                    switch (step % 4)
                    {
                        case 0: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(7, floor), 1, "Aguijonazo", "sting", "Te clava el aguijón: 4 de Veneno.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Poison, 4) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 1: return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(5, floor), 2, "Pinzas", "pinch", "Dos pinzazos: te deja Débil 1 turno.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Weak, 1) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 2: return new EnemyIntent(IntentKind.Block, EnemyCatalog.Scale(14, floor), 1, "Caparazón", "shell", "Se encoge bajo su caparazón: gana mucho escudo.", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                        case 3: return new EnemyIntent(IntentKind.Debuff, 0, 1, "Toxina", "toxin", "Rocía veneno: 6 de Veneno.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Poison, 6) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                    }
                    break;
            }
            return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(18, floor), name: "Golpe de la Cripta", id: "crypt");
        }
        private static EnemyIntent Enrage(string ai, int floor)
        {
            switch (ai)
            {
                case "micelida": return new EnemyIntent(IntentKind.Debuff, 0, 1, "Floración Pútrida", "bloom", "Te da 4 de Veneno y pone 2 Esporas en tu mano.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Poison, EnemyCatalog.Scale(4, floor)) }, allyBuffs: null, cards: new[] { new StatusCardSpawn("status_espora", 2, "hand") }, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                case "tejedora": return new EnemyIntent(IntentKind.Debuff, 0, 1, "Madre de la Camada", "brood", "Te aplica Débil, Frágil, Vulnerable y Enredado a la vez.", block: 0, buffs: null, debuffs: new[] { new StatusAmount(StatusKind.Weak, 1), new StatusAmount(StatusKind.Frail, 1), new StatusAmount(StatusKind.Vulnerable, 1), new StatusAmount(StatusKind.Entangled, 1) }, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                case "caballero": return new EnemyIntent(IntentKind.Buff, 0, 1, "Furia Hueca", "fury", "Gana 2 de Fuerza: cada golpe hace +2.", block: EnemyCatalog.Scale(6, floor), buffs: new[] { new StatusAmount(StatusKind.Strength, 2) }, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                case "verdugo": return new EnemyIntent(IntentKind.Buff, 0, 1, "Sed de Sangre", "bloodlust", "Huele la sangre: gana 3 de Fuerza para siempre.", block: 0, buffs: new[] { new StatusAmount(StatusKind.Strength, 3) }, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                case "bruja": return new EnemyIntent(IntentKind.Buff, 0, 1, "Brebaje Prohibido", "potion", "Bebe un brebaje prohibido: se cura 15 y gana 2 de Fuerza.", block: 0, buffs: new[] { new StatusAmount(StatusKind.Strength, 2) }, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: EnemyCatalog.Scale(15, floor));
                case "gargola": return new EnemyIntent(IntentKind.Buff, 0, 1, "Despertar", "awaken", "Despierta del todo: gana 3 de Fuerza y escudo.", block: EnemyCatalog.Scale(10, floor), buffs: new[] { new StatusAmount(StatusKind.Strength, 3) }, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                case "minotauro": return new EnemyIntent(IntentKind.Buff, 0, 1, "Furia Taurina", "rage", "Brama de furia: gana 3 de Fuerza para siempre.", block: 0, buffs: new[] { new StatusAmount(StatusKind.Strength, 3) }, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
                case "escorpion": return new EnemyIntent(IntentKind.Buff, 0, 1, "Frenesí", "frenzy", "Entra en frenesí: gana 2 de Fuerza y escudo.", block: EnemyCatalog.Scale(8, floor), buffs: new[] { new StatusAmount(StatusKind.Strength, 2) }, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: 0);
            }
            return Normal(ai, 0, floor);
        }
        private static EnemyIntent Mend(int floor) => new EnemyIntent(IntentKind.Buff, 0, 1, "Plegaria", "mend", "Reza: cura 8 de vida a cada aliado (y a sí mismo).", block: 0, buffs: null, debuffs: null, allyBuffs: null, cards: null, lifesteal: false, selfDestruct: false, allyBlock: 0, healAllies: EnemyCatalog.Scale(8, floor));
        private static EnemyIntent Vengeance(int floor) => new EnemyIntent(IntentKind.Buff, name: "Venganza", id: "vengeance", block: EnemyCatalog.Scale(6, floor), buffs: new[] { new StatusAmount(StatusKind.Strength, 3) });
    }
}
