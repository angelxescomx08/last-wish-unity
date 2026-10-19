using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>One character an action touched, and what happened to it (combat_log.LogTarget).</summary>
    public sealed class LogTarget
    {
        public string Name, Portrait, EnemyId = "";
        public int Hp, Block, Healed;
        public bool Died, Aimed, Blocked;
        public (string name, int stacks)[] Statuses = Array.Empty<(string, int)>();
        public string[] Reactions = Array.Empty<string>();
    }

    /// <summary>combat_log.LogEntry.</summary>
    public sealed class LogEntry
    {
        public string Kind, Icon, Title; public int Turn;
        public string[] Lines = Array.Empty<string>();
        public int Value; public string ValueKind = "";
        public CardDefinition Card;
        public string Source = "", SourceName = "", Verb = "";
        public LogTarget[] Targets = Array.Empty<LogTarget>();
    }

    /// <summary>application/combat_log.py: Hearthstone-style history of one fight (capped, versioned).</summary>
    public sealed class CombatLog
    {
        public const int MaxEntries = 150;
        public const string Hero = "hero";
        private static readonly Dictionary<string, string> TypeIcon = new Dictionary<string, string> { ["ATTACK"] = "damage", ["SKILL"] = "block", ["POWER"] = "ritual", ["STATUS"] = "junk_card" };
        private static readonly Dictionary<string, string> ElementIcon = new Dictionary<string, string> { ["Fuego"] = "fire", ["Agua"] = "water", ["Tierra"] = "earth", ["Aire"] = "air" };

        public readonly int Capacity;
        private readonly List<LogEntry> entries = new List<LogEntry>();
        public int Version { get; private set; }
        public CombatLog(int maxEntries = MaxEntries) { Capacity = Math.Max(1, maxEntries); }
        public IReadOnlyList<LogEntry> Entries => entries;
        public int Count => entries.Count;

        public void Add(LogEntry entry)
        {
            entries.Add(entry);
            if (entries.Count > Capacity) entries.RemoveRange(0, entries.Count - Capacity);
            Version++;
        }

        public void Turn(int n) => Add(new LogEntry { Kind = "turn", Turn = n, Icon = "hourglass", Title = "Turno " + n });

        public void Enemy(int turn, string icon, string title, string detail, int hpLost = 0, string source = "", LogTarget[] targets = null)
        {
            var lines = new List<string>();
            foreach (var p in (detail ?? "").Split(new[] { " · " }, StringSplitOptions.None)) if (p.Length > 0) lines.Add(Sentence(p));
            Add(new LogEntry { Kind = "enemy", Turn = turn, Icon = icon, Title = title, Lines = lines.ToArray(), Value = hpLost, ValueKind = hpLost != 0 ? "loss" : "",
                Source = source, SourceName = source, Verb = icon, Targets = targets ?? Array.Empty<LogTarget>() });
        }

        public void Effect(int turn, string icon, string title, string[] lines, LogTarget[] targets = null)
            => Add(new LogEntry { Kind = "effect", Turn = turn, Icon = icon, Title = title, Lines = lines ?? Array.Empty<string>(), Verb = icon, Targets = targets ?? Array.Empty<LogTarget>() });

        /// <summary>"pierdes 3 de vida" → "Pierdes 3 de vida." (no extra dot after "!" or ".").</summary>
        public static string Sentence(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var i = text[0] == '¡' || text[0] == '¿' ? 1 : 0;
            if (i < text.Length) text = text.Substring(0, i) + char.ToUpperInvariant(text[i]) + text.Substring(i + 1);
            var last = text[text.Length - 1];
            return last == '.' || last == '!' || last == '?' ? text : text + ".";
        }

        private static string Plural(int n, string one, string many) => n + " " + (n == 1 ? one : many);

        /// <summary>elements.reaction_summary: what a reaction did, in a few words.</summary>
        public static string ReactionSummary(ReactionKind reaction, int stacks)
        {
            switch (reaction)
            {
                case ReactionKind.Steam: return 2 * stacks + " de daño";
                case ReactionKind.Magma: return "sin escudo y Vulnerable 2";
                case ReactionKind.Wildfire: return stacks + " de Fuego a todos";
                case ReactionKind.Mud: return "+" + 3 * stacks + " de escudo";
                case ReactionKind.Freeze: return "pierde su próxima acción";
                default: return "Débil 1 a todos";
            }
        }

        public static string ReactionName(Glossary g, ReactionKind k) => g.ReactionName.TryGetValue(k.ToString().ToUpperInvariant(), out var n) ? n : k.ToString();

        /// <summary>Each enemy's statuses before a play (for "statuses gained").</summary>
        public static int[] Snapshot(EnemyState e)
        {
            var kinds = Enum.GetValues(typeof(StatusKind)); var a = new int[kinds.Length];
            foreach (StatusKind k in kinds) a[(int)k] = e.Statuses[k];
            return a;
        }

        private static (string, int)[] StatusGain(Glossary g, int[] before, EnemyState after)
        {
            var list = new List<(string, int)>();
            foreach (var k in after.Statuses.Active)
            {
                var gain = after.Statuses[k] - (before != null ? before[(int)k] : 0);
                if (gain > 0) list.Add((g.StatusName.TryGetValue(k, out var n) ? n : k.ToString(), gain));
            }
            return list.ToArray();
        }

        /// <summary>hero_entry: the entry of a card you just played.</summary>
        public static LogEntry HeroEntry(GameContext game, int turn, CardDefinition card, PlayResult result, IReadOnlyList<string> enemyNames, IReadOnlyList<EnemyState> enemiesAfter,
            int blockGain, IReadOnlyList<ReactionEvent> reactions, IReadOnlyList<int[]> statusesBefore = null, int? target = null, string heroName = "Héroe")
        {
            var g = game.Data.Glossary; var info = game.Data.Card(card);
            var lost = new int[enemyNames.Count];
            foreach (var hits in result.CastHits)
                for (var i = 0; i < Math.Min(hits.Length, lost.Length); i++) lost[i] += Math.Max(0, hits[i]);
            var lines = new List<string>();
            for (var i = 0; i < lost.Length; i++)
                if (lost[i] > 0)
                {
                    var dead = i < enemiesAfter.Count && !enemiesAfter[i].Alive;
                    lines.Add(enemyNames[i] + ": −" + lost[i] + " de vida" + (dead ? " (muere)" : ""));
                }
            if (blockGain > 0) lines.Add("Ganaste " + blockGain + " de escudo");
            var drawn = 0; foreach (var d in result.CastDrawn) drawn += d;
            if (drawn > 0) lines.Add("Robaste " + Plural(drawn, "carta", "cartas"));
            foreach (var (element, stacks, all) in info.Chips ?? Array.Empty<(string, int, bool)>())
                lines.Add("Aplicaste " + stacks + " de " + element + " " + (all ? "a todos" : "al enemigo"));
            var namesById = new Dictionary<string, string>();
            foreach (var e in enemiesAfter) namesById[e.Id] = e.Definition.Name;
            foreach (var ev in reactions)
            {
                var who = namesById.TryGetValue(ev.EnemyId, out var n) ? n : "";
                lines.Add("¡" + ReactionName(g, ev.Kind) + "! en " + who + ": " + ReactionSummary(ev.Kind, ev.Stacks));
            }
            foreach (var (flag, label) in new[] { (result.Combo, "¡Combo!"), (result.Singular, "¡Singular!"), (result.Void, "¡Vacío!"), (result.Spoil, "¡Despojo!"),
                (result.Reaction, "¡Reacción!"), (result.Fury, "¡Furia!"), (result.Endure, "¡Aguante!") })
                if (flag) lines.Add(label);
            var title = "Jugaste " + card.Name + (result.Casts > 1 ? " x" + result.Casts : "");
            var badges = info.Badges ?? Array.Empty<string>();
            var icon = badges.Length > 0 && ElementIcon.TryGetValue(badges[0], out var ei) ? ei : TypeIcon.TryGetValue(info.Type ?? "", out var ti) ? ti : "cards";
            var dealt = 0; foreach (var l in lost) dealt += l;
            int value; string kind;
            if (dealt != 0) { value = dealt; kind = "damage"; } else if (blockGain != 0) { value = blockGain; kind = "block"; } else { value = 0; kind = ""; }

            var byEnemy = new Dictionary<string, List<string>>();
            foreach (var ev in reactions)
            {
                if (!byEnemy.TryGetValue(ev.EnemyId, out var list)) byEnemy[ev.EnemyId] = list = new List<string>();
                list.Add(ReactionName(g, ev.Kind));
            }
            var targets = new List<LogTarget>();
            for (var i = 0; i < enemyNames.Count; i++)
            {
                if (i >= enemiesAfter.Count) continue;
                var enemy = enemiesAfter[i];
                var gained = StatusGain(g, statusesBefore != null && i < statusesBefore.Count ? statusesBefore[i] : null, enemy);
                var reacted = byEnemy.TryGetValue(enemy.Id, out var rl) ? rl.ToArray() : Array.Empty<string>();
                if (lost[i] != 0 || gained.Length > 0 || reacted.Length > 0 || i == target)
                    targets.Add(new LogTarget { Name = enemyNames[i], Portrait = enemyNames[i], Hp = lost[i], Died = !enemy.Alive, Statuses = gained, Reactions = reacted, EnemyId = enemy.Id, Aimed = i == target });
            }
            if (blockGain > 0 || targets.Count == 0) targets.Add(new LogTarget { Name = heroName, Portrait = Hero, Block = Math.Max(0, blockGain) });
            var type = info.Type ?? "";
            var verb = type == "ATTACK" ? "attack_2" : type == "SKILL" ? (blockGain != 0 ? "block" : "buff") : "buff";
            if (type != "ATTACK")
                foreach (var t in targets) if (t.Portrait != Hero && (t.Statuses.Length > 0 || t.Reactions.Length > 0)) { verb = "debuff"; break; }
            return new LogEntry { Kind = "hero", Turn = turn, Icon = icon, Title = title, Lines = lines.Count > 0 ? lines.ToArray() : new[] { "Sin efecto visible" },
                Value = value, ValueKind = kind, Card = card, Source = Hero, SourceName = heroName, Verb = verb, Targets = targets.ToArray() };
        }
    }
}
