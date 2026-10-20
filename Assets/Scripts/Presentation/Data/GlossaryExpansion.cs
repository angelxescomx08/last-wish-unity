using System;
using System.Text.RegularExpressions;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>
    /// Glossary entries of the floor-1 expansion (Data/glossary.json is exported from the Python game and is not edited).
    /// The seven new statuses only get status data (shown by the status panels of an enemy); "Aturdido" and "Esbirro"
    /// are real glossary terms, highlighted in texts.
    /// </summary>
    public static class GlossaryExpansion
    {
        private sealed class StatusEntry
        {
            public StatusKind Kind; public string Icon, Short, Rule;
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

        private static Regex WordPattern(string name) =>
            new Regex("(?<![\\wÁÉÍÓÚáéíóúñÑ])(" + Regex.Escape(name) + ")(?![\\wáéíóúñ])", RegexOptions.CultureInvariant);

        public static void Apply(Glossary g)
        {
            foreach (var s in Statuses)
            {
                var name = EnemyCatalog.ExpansionStatusNames[s.Kind];
                g.StatusName[s.Kind] = name;
                g.StatusIcon[name] = s.Icon;
                g.StatusIsBuff[name] = true;
                g.StatusShort[name] = s.Short;
                g.StatusText[name] = s.Rule;
                g.StatusTerms[name] = new Term { Name = name, Rule = s.Rule, Icon = s.Icon, Color = g.Keyword, Pattern = WordPattern(name), Lines = Array.Empty<string>() };
            }
            var stunned = EnemyCatalog.ExpansionTermNames[0]; var minion = EnemyCatalog.ExpansionTermNames[1];
            g.AllTerms.Add(new Term { Name = stunned, Rule = "Pierde su próximo turno.", Icon = "unplayable", Color = g.Keyword, Pattern = WordPattern(stunned), Lines = Array.Empty<string>() });
            g.AllTerms.Add(new Term { Name = minion, Rule = "Enemigo invocado durante el combate. No da oro. Nunca hay más de 3 enemigos en pantalla.",
                Icon = "ritual", Color = g.Keyword, Pattern = WordPattern(minion), Lines = Array.Empty<string>() });
        }
    }
}
