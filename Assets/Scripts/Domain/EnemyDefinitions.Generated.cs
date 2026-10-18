using System;
using System.Collections.Generic;
namespace LastWish.Domain
{
    public static partial class EnemyCatalog
    {
        public static readonly IReadOnlyList<EnemyDefinition> Definitions = Array.AsReadOnly(new[] {
            new EnemyDefinition("espectro", "Espectro", "wraith", 42, false, false),
            new EnemyDefinition("babosa", "Babosa Ácida", "slime", 46, false, false),
            new EnemyDefinition("gusano", "Gusano de Tumba", "worm", 52, false, false),
            new EnemyDefinition("ojo", "Ojo Vigilante", "eye", 34, false, false),
            new EnemyDefinition("craneo", "Cráneo Ígneo", "skull", 38, false, false),
            new EnemyDefinition("murcielago", "Murciélago Vampiro", "bat", 30, false, false),
            new EnemyDefinition("seta", "Seta Explosiva", "bomb", 28, false, false),
            new EnemyDefinition("golem", "Gólem de Musgo", "golem", 72, false, false),
            new EnemyDefinition("acolito", "Acólito de Ceniza", "acolyte", 40, false, false),
            new EnemyDefinition("diablillo", "Diablillo", "imp", 32, false, false),
            new EnemyDefinition("mimico", "Mímico", "mimic", 60, false, false),
            new EnemyDefinition("micelida", "Reina Micélida", "mycelid", 138, true, false),
            new EnemyDefinition("tejedora", "La Tejedora", "weaver", 126, true, false),
            new EnemyDefinition("caballero", "Caballero Hueco", "knight", 150, true, false),
            new EnemyDefinition("verdugo", "El Verdugo", "executioner", 92, false, true),
            new EnemyDefinition("bruja", "Bruja del Pantano", "hag", 80, false, true),
            new EnemyDefinition("gargola", "Gárgola", "gargoyle", 84, false, true),
            new EnemyDefinition("minotauro", "Minotauro", "minotaur", 100, false, true),
            new EnemyDefinition("escorpion", "Escorpión Rey", "scorpion", 88, false, true),
        });
    }
}
