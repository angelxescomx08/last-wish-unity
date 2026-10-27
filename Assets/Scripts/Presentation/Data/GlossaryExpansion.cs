using System;
using System.Text.RegularExpressions;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>
    /// Glossary entries of the floor-1, floor-2 and floor-3 expansions (Data/glossary.json is exported from the Python game and
    /// is not edited). The new enemy statuses only get status data (shown by the status panels of an enemy); "Aturdido",
    /// "Esbirro" and the hero debuffs of floors 2 and 3 ("Sangrado", "Necrosis", "Agotamiento", "Congelación") are real
    /// glossary terms, highlighted in texts.
    /// </summary>
    public static class GlossaryExpansion
    {
        private sealed class StatusEntry
        {
            public StatusKind Kind; public string Icon, Short, Rule; public bool Debuff;
        }

        private static readonly StatusEntry[] Statuses = {
            new StatusEntry { Kind = StatusKind.Thorns, Icon = "damage", Short = "te hace {n} por carta que lo dañe",
                Rule = "Cada carta tuya que le haga daño te inflige tantos puntos de daño como acumulaciones (una vez por carta). No es un ataque: tus reliquias de defensa no lo cambian." },
            new StatusEntry { Kind = StatusKind.Shell, Icon = "tower", Short = "conserva hasta {n} de escudo",
                Rule = "Al empezar su turno conserva hasta tantas acumulaciones de escudo en lugar de perderlo." },
            new StatusEntry { Kind = StatusKind.Carrion, Icon = "fury", Short = "+{n} de Fuerza si muere otro enemigo",
                Rule = "Cada vez que muere otro enemigo que no sea un esbirro gana tantas acumulaciones de Fuerza y se cura el triple." },
            new StatusEntry { Kind = StatusKind.Swallowed, Icon = "cards", Short = "{n} cartas tuyas tragadas",
                Rule = "Cartas que se ha tragado de tu mazo. Vuelven a tu descarte cuando muere." },
            new StatusEntry { Kind = StatusKind.Focus, Icon = "hourglass", Short = "quítale {n} de vida para aturdirlo",
                Rule = "Prepara un golpe. Si pierde tantos PV como acumulaciones antes de actuar, queda Aturdido y pierde su turno." },
            new StatusEntry { Kind = StatusKind.Parry, Icon = "endure", Short = "desvía {n} golpes",
                Rule = "Anula por completo el próximo golpe que recibe (una acumulación por golpe). Si fue durante una de tus cartas, al terminarla te devuelve su Réplica." },
            new StatusEntry { Kind = StatusKind.Vigil, Icon = "combo", Short = "tras {n} cartas en un turno, +1 de Fuerza por carta",
                Rule = "A partir de tu carta número acumulaciones + 1 de cada turno, cada carta que juegues le da 1 de Fuerza." },
        };

        private static readonly StatusEntry[] Floor2Statuses = {
            new StatusEntry { Kind = StatusKind.Ward, Icon = "defend", Short = "cada golpe le hace {n} menos",
                Rule = "Cada golpe que recibe hace tantos puntos de daño menos como acumulaciones. Se pierde al empezar su siguiente turno." },
            new StatusEntry { Kind = StatusKind.Ritual, Icon = "ritual", Short = "+{n} de Fuerza al terminar su turno",
                Rule = "Al terminar cada uno de sus turnos gana tanta Fuerza como acumulaciones (no el turno en que lo consigue)." },
            new StatusEntry { Kind = StatusKind.Grudge, Icon = "lethal", Short = "te hace {n} al morir",
                Rule = "Cuando muere te inflige tantos puntos de daño como acumulaciones. Tu escudo lo absorbe. No es un ataque." },
            new StatusEntry { Kind = StatusKind.Regen, Icon = "heal", Short = "se cura {n} al terminar su turno",
                Rule = "Al terminar su turno se cura tantos PV como acumulaciones, salvo si tiene Fuego o Veneno." },
            new StatusEntry { Kind = StatusKind.Heads, Icon = "attack_3", Short = "muerde {n} veces",
                Rule = "Mordisco Múltiple golpea una vez por cabeza. Un solo golpe que le quite 20 o más de vida le corta una (mínimo 2)." },
            new StatusEntry { Kind = StatusKind.Reflect, Icon = "void", Short = "devuelve {n} golpes grandes",
                Rule = "Cada golpe que le quite 15 o más de vida gasta 1 acumulación y te devuelve la mitad de esa vida al terminar tu carta. Tu escudo lo absorbe." },
            new StatusEntry { Kind = StatusKind.Thirst, Icon = "strength", Short = "+{n} de Fuerza si pierdes vida",
                Rule = "La primera vez de cada turno que pierdes vida (por cualquier causa) gana tanta Fuerza como acumulaciones." },
            new StatusEntry { Kind = StatusKind.Bleed, Icon = "fury", Debuff = true, Short = "pierdes 1 de vida por carta",
                Rule = "Mientras lo tengas, pierdes 1 de vida por cada carta que juegues (ignora el escudo). Baja 1 al final de tu turno." },
            new StatusEntry { Kind = StatusKind.Necrosis, Icon = "unplayable", Debuff = true, Short = "no puedes curarte",
                Rule = "Mientras la tengas, tus curaciones no hacen nada. Baja 1 al final de tu turno." },
        };

        private static readonly StatusEntry[] Floor3Statuses = {
            new StatusEntry { Kind = StatusKind.Banner, Icon = "helmet", Short = "sus aliados hacen +{n} por golpe",
                Rule = "Mientras viva, cada golpe de sus aliados hace tantos puntos de daño más como acumulaciones (Inspiración). Mátalo y se acaba." },
            new StatusEntry { Kind = StatusKind.Inspired, Icon = "status_buff", Short = "+{n} de daño por golpe",
                Rule = "Cada uno de sus golpes hace tantos puntos de daño más como acumulaciones. Se lo da el Pendón de un aliado mientras ese aliado viva." },
            new StatusEntry { Kind = StatusKind.Rage, Icon = "fury", Short = "+{n} de Fuerza por golpe que lo hiera",
                Rule = "Cada golpe que le quite vida le da tanta Fuerza como acumulaciones. Pierde esa Fuerza al terminar su siguiente acción. Pégale pocas veces y fuerte." },
            new StatusEntry { Kind = StatusKind.Undying, Icon = "ethereal", Short = "no baja de 1 de vida",
                Rule = "No puede bajar de 1 de vida mientras viva su Filacteria. Rómpela primero." },
            new StatusEntry { Kind = StatusKind.Spring, Icon = "gear", Short = "Campanada golpea {n} veces",
                Rule = "Campanada golpea una vez por acumulación y después le deja 1. Cada Autómata suyo que actúa le da 1 más." },
            new StatusEntry { Kind = StatusKind.Exhaustion, Icon = "mana", Debuff = true, Short = "1 de maná menos al empezar el turno",
                Rule = "Mientras lo tengas, empiezas cada turno con 1 de maná menos. Baja 1 al final de tu turno." },
            new StatusEntry { Kind = StatusKind.Frostbite, Icon = "frozen", Debuff = true, Short = "pierdes 1 de vida por carta en la mano",
                Rule = "Mientras la tengas, al terminar tu turno pierdes 1 de vida por cada carta que te quede en la mano (ignora el escudo). Baja 1 al final de tu turno." },
        };

        private static Regex WordPattern(string name) =>
            new Regex("(?<![\\wÁÉÍÓÚáéíóúñÑ])(" + Regex.Escape(name) + ")(?![\\wáéíóúñ])", RegexOptions.CultureInvariant);

        private static Term Register(Glossary g, StatusEntry s, string name)
        {
            g.StatusName[s.Kind] = name;
            g.StatusIcon[name] = s.Icon;
            g.StatusIsBuff[name] = !s.Debuff;
            g.StatusShort[name] = s.Short;
            g.StatusText[name] = s.Rule;
            var term = new Term { Name = name, Rule = s.Rule, Icon = s.Icon, Color = g.Keyword, Pattern = WordPattern(name), Lines = Array.Empty<string>() };
            g.StatusTerms[name] = term;
            return term;
        }

        public static void Apply(Glossary g)
        {
            foreach (var s in Statuses) Register(g, s, EnemyCatalog.ExpansionStatusNames[s.Kind]);
            foreach (var s in Floor2Statuses)
            {
                var term = Register(g, s, EnemyCatalog.Floor2StatusNames[s.Kind]);
                if (s.Debuff) g.AllTerms.Add(term);   // hero debuffs: highlighted in intent texts, with their panel
            }
            foreach (var s in Floor3Statuses)
            {
                var term = Register(g, s, EnemyCatalog.Floor3StatusNames[s.Kind]);
                if (s.Debuff) g.AllTerms.Add(term);
            }
            var stunned = EnemyCatalog.ExpansionTermNames[0]; var minion = EnemyCatalog.ExpansionTermNames[1];
            g.AllTerms.Add(new Term { Name = stunned, Rule = "Pierde su próximo turno.", Icon = "unplayable", Color = g.Keyword, Pattern = WordPattern(stunned), Lines = Array.Empty<string>() });
            g.AllTerms.Add(new Term { Name = minion, Rule = "Enemigo invocado durante el combate. No da oro. Nunca hay más de 3 enemigos en pantalla.",
                Icon = "ritual", Color = g.Keyword, Pattern = WordPattern(minion), Lines = Array.Empty<string>() });
        }
    }
}
