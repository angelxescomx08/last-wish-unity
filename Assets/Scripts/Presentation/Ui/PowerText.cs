using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>What an active power does right now, in Spanish with live numbers: its effect for the copies in play
    /// (a golden one counts twice), how copies stack, and its state this turn. Used by the power badges and the hero panel.</summary>
    public static class PowerText
    {
        private static string Times(long n) => n == 1 ? "1 vez" : n + " veces";
        private static string Cards(long n) => n == 1 ? "1 carta" : n + " cartas";

        private static readonly Dictionary<string, Func<int, CombatState, string>> Effects = new Dictionary<string, Func<int, CombatState, string>>
        {
            ["mg_piel_granito"] = (n, s) => "Cada reacción que provoques te da " + 4L * n + " de escudo.",
            ["wr_segunda_piel"] = (n, s) => "Cada vez que pierdes vida, ganas " + 3L * n + " de escudo.",
            ["r_reflejos"] = (n, s) => "+" + n + " de Destreza durante este combate: tus cartas dan más escudo.",
            ["r_danza_de_sombras"] = (n, s) => "Tus Combos se activan aunque no hayas jugado otra carta antes.",
            ["mg_ojo_huracan"] = (n, s) => "Cada reacción que provoques te hace robar " + Cards(n) + ".",
            ["wr_sangre_hirviente"] = (n, s) => n == 1 ? "Al inicio de tu turno pierdes 2 de vida y ganas 1 de Fuerza."
                : "Al inicio de tu turno, " + n + " veces: pierdes 2 de vida y ganas 1 de Fuerza.",
            ["wr_fortaleza_interior"] = (n, s) => "Cada vez que te curas, ganas " + 2L * n + " de escudo.",
            ["wr_regeneracion"] = (n, s) => "Al inicio de tu turno te curas " + 3L * n + ".",
            ["r_ritmo_letal"] = (n, s) => "La primera carta de cada turno cuesta " + n + " menos.",
            ["r_tormenta_de_acero"] = (n, s) => "Al inicio de cada turno inflige " + (s != null && s.SingularDeck ? 5L : 3L) * n + " de daño a todos los enemigos.",
            ["r_rapina"] = (n, s) => "Cada carta que descartes inflige " + 3L * n + " de daño a un enemigo al azar.",
            ["r_maestra_de_dagas"] = (n, s) => "Cada Daga Oculta que robes mete " + n + " más en tu pila de robo.",
            ["r_sombra_gemela"] = (n, s) => "La primera carta de cada turno que active su Combo lo activa " + Times(Multiplier(n)) + ".",
            ["r_fortuna_audaz"] = (n, s) => n == 1 ? "Al inicio de cada turno: 50 % roba 1 carta, 50 % gana 4 de escudo."
                : "Al inicio de cada turno, " + n + " tiradas: 50 % roba 1 carta, 50 % gana 4 de escudo.",
            ["r_nada_que_perder"] = (n, s) => n == 1 ? "Al inicio de cada turno, tras robar: descarta una carta al azar y roba 2."
                : "Al inicio de cada turno, tras robar, " + n + " veces: descarta una carta al azar y roba 2.",
            ["r_cadena_perfecta"] = (n, s) => "Cada quinta carta que juegues en un turno se lanza " + Times(n) + " más.",
            ["r_asesina"] = (n, s) => "Tus ataques rematan a los enemigos que queden por debajo del 25 % de vida.",
            ["mg_corazon_igneo"] = (n, s) => "Al inicio de tu turno, +" + n + " de Fuego a todos los enemigos.",
            ["mg_archimago"] = (n, s) => "Tus reacciones afectan a todos los enemigos.",
            ["wr_piel_acero"] = (n, s) => "Al final de tu turno ganas " + 6L * n + " de escudo.",
            ["wr_berserker"] = (n, s) => "Cada vez que pierdes vida, +" + n + " de Fuerza.",
            ["wr_voluntad_hierro"] = (n, s) => "Tus propios efectos no te quitan más de 1 de vida cada vez.",
        };

        /// <summary>On/off powers: more copies add nothing.</summary>
        public static readonly HashSet<string> NoStack = new HashSet<string> { "r_danza_de_sombras", "r_asesina", "mg_archimago", "wr_voluntad_hierro" };
        /// <summary>Each copy multiplies instead of adding.</summary>
        public static readonly HashSet<string> Multiplies = new HashSet<string> { "r_sombra_gemela" };
        /// <summary>Powers that act at the end of your turn.</summary>
        public static readonly HashSet<string> AtTurnEnd = new HashSet<string> { "wr_piel_acero" };

        public static bool Known(string id) => id != null && Effects.ContainsKey(id);
        public static long Multiplier(int copies) => 1L << Math.Min(Math.Max(0, copies), CombatState.MaxComboDoublings);
        /// <summary>The power acts by itself at the start of each of your turns.</summary>
        public static bool ActsAtTurnStart(CardDefinition card) => card?.Effects != null && card.Effects.Any(e => e.TurnCommands.Count > 0);

        /// <summary>The effect line with the numbers of <paramref name="copies"/> copies.</summary>
        public static string Effect(GameContext game, CardDefinition card, int copies, CombatState state)
        {
            var n = Math.Max(1, copies);
            if (Effects.TryGetValue(card.Id, out var line)) return line(n, state);
            var texts = game?.Data?.Card(card).Texts?.Where(t => !string.IsNullOrEmpty(t)).ToList() ?? new List<string>();
            var text = texts.Count > 0 ? string.Join(" ", texts.Select(t => char.ToUpperInvariant(t[0]) + t.Substring(1) + (t.EndsWith(".") ? "" : "."))) : "Efecto permanente.";
            return n > 1 ? text + " (×" + n + ")" : text;
        }

        /// <summary>How copies add up, or null when there is only one and nothing to say.</summary>
        public static string Stacking(CardDefinition card, int copies)
        {
            var golden = card.Chroma != ChromaKind.None ? " (una " + (card.Chroma == ChromaKind.Diamond ? "de diamante cuenta como 3" : "dorada cuenta como 2") + ")" : "";
            if (Multiplies.Contains(card.Id))
                return copies > 1 ? "Copias activas: " + copies + golden + ". Cada copia duplica: " + string.Join(" → ", Enumerable.Range(1, Math.Min(copies, 4)).Select(k => "×" + Multiplier(k))) + (copies > 4 ? " → …" : "") + "."
                    : "Cada copia más lo vuelve a duplicar (×2, ×4, ×8…).";
            if (copies <= 1) return null;
            if (NoStack.Contains(card.Id)) return "Copias activas: " + copies + golden + ". Más copias no suman efecto.";
            return "Copias activas: " + copies + golden + ". Cada copia suma su efecto.";
        }

        /// <summary>What it is doing this turn, or when it acts.</summary>
        public static string State(CardDefinition card, int copies, CombatState state)
        {
            switch (card.Id)
            {
                case "r_sombra_gemela":
                    if (state == null) return null;
                    return state.ComboDoublingUsed ? "Ya lo usaste este turno: vuelve en tu próximo turno."
                        : "Listo: tu próxima carta con Combo lo activará " + Times(state.ComboMultiplier) + ".";
                case "r_ritmo_letal":
                    if (state == null) return null;
                    return state.CardsPlayedThisTurn == 0 ? "Listo: tu próxima carta cuesta " + copies + " menos." : "Ya se aplicó este turno.";
                case "r_cadena_perfecta":
                    if (state == null) return null;
                    var played = state.CardsPlayedThisTurn; var next = (played / 5 + 1) * 5;
                    return "Cartas jugadas este turno: " + played + ". La carta número " + next + " se lanzará " + Times(copies) + " más.";
            }
            if (AtTurnEnd.Contains(card.Id)) return "Se activa al final de cada turno.";
            return ActsAtTurnStart(card) ? "Se activa al inicio de cada turno." : "Activo durante todo el combate.";
        }

        /// <summary>All the lines of a power, effect first.</summary>
        public static List<string> Lines(GameContext game, CardDefinition card, int copies, CombatState state)
        {
            var lines = new List<string> { Effect(game, card, copies, state) };
            var stacking = Stacking(card, copies); if (stacking != null) lines.Add(stacking);
            var now = State(card, copies, state); if (now != null) lines.Add("  " + now);
            return lines;
        }

        /// <summary>"Sombra Gemela ×2, Rapiña" for the quick look at the hero.</summary>
        public static string Summary(IReadOnlyList<CardDefinition> powers) =>
            string.Join(", ", PowerBar.Group(powers).Select(p => p.card.Name + (p.copies > 1 ? " ×" + p.copies : "")));
    }
}
