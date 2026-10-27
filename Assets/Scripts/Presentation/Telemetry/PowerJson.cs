using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>
    /// Where finished and in-progress runs go (the runtime writes them to disk and uploads them in the
    /// background). Calls come from the game loop and must return at once.
    /// </summary>
    public interface ITelemetrySink
    {
        /// <summary>Application version stamped on every run.</summary>
        string GameVersion { get; }
        /// <summary>Anonymous id of this installation (separates players in the analytics), or null.</summary>
        string PlayerId { get; }
        /// <summary>The latest state of a run that is still on: replaces the previous checkpoint of the same run.</summary>
        void Checkpoint(string runId, string json);
        /// <summary>A finished run: queue it for upload and drop its checkpoint.</summary>
        void Submit(string runId, string json);
    }

    /// <summary>
    /// Compact JSON of a <see cref="RunRecord"/>: the schema public.ingest_run accepts (docs/telemetry.md).
    /// Built with one StringBuilder; a long run (dozens of fights) is a few tens of kilobytes.
    /// </summary>
    public static class PowerJson
    {
        public static string Write(RunRecord r)
        {
            var sb = new StringBuilder(4096 + 1024 * r.Combats.Count);
            sb.Append('{');
            Num(sb, "schema_version", RunRecord.SchemaVersion, true);
            Str(sb, "run_id", r.RunId);
            if (!string.IsNullOrEmpty(r.PlayerId)) Str(sb, "player_id", r.PlayerId);
            Str(sb, "game_version", r.GameVersion);
            Str(sb, "content_hash", r.ContentHash);
            if (!string.IsNullOrEmpty(r.StartedAt)) Str(sb, "started_at", r.StartedAt);
            Str(sb, "hero", Hero(r.Hero));
            Num(sb, "seed", r.Seed);
            Str(sb, "roster", r.Roster == EnemyRoster.Expanded ? "expanded" : "original");
            Key(sb, "tuned"); sb.Append(r.Tuned ? "true" : "false");
            Str(sb, "outcome", Outcome(r.Outcome));
            Num(sb, "floor", r.Floor);
            Num(sb, "rooms", r.Rooms);
            Num(sb, "gold", r.Gold);
            Num(sb, "hp", r.Hp);
            Num(sb, "max_hp", r.MaxHp);
            Key(sb, "played_seconds"); sb.Append(System.Math.Round(r.PlayedSeconds, 1).ToString("0.0", CultureInfo.InvariantCulture));

            Key(sb, "deck"); sb.Append('[');
            for (var i = 0; i < r.Deck.Count; i++)
            {
                var d = r.Deck[i];
                if (i > 0) sb.Append(',');
                sb.Append('{'); Str(sb, "id", d.Id, true); Key(sb, "upgraded"); sb.Append(d.Upgraded ? "true" : "false"); Str(sb, "chroma", Chroma(d.Chroma)); sb.Append('}');
            }
            sb.Append(']');

            Key(sb, "relics"); sb.Append('[');
            for (var i = 0; i < r.Relics.Count; i++)
            {
                var x = r.Relics[i];
                if (i > 0) sb.Append(',');
                sb.Append('{'); Str(sb, "id", x.Id, true); Str(sb, "chroma", Chroma(x.Chroma));
                if (x.Floor > 0) Num(sb, "floor", x.Floor);
                Str(sb, "source", x.Source); sb.Append('}');
            }
            sb.Append(']');

            Key(sb, "choices"); sb.Append('[');
            for (var i = 0; i < r.Choices.Count; i++)
            {
                var c = r.Choices[i];
                if (i > 0) sb.Append(',');
                sb.Append('{'); Num(sb, "floor", c.Floor, true); Str(sb, "kind", c.Relic ? "relic" : "card"); Str(sb, "source", c.Source);
                Key(sb, "offered"); Strings(sb, c.Offered); Key(sb, "picked"); Strings(sb, c.Picked); sb.Append('}');
            }
            sb.Append(']');

            Key(sb, "combats"); sb.Append('[');
            for (var i = 0; i < r.Combats.Count; i++)
            {
                var c = r.Combats[i];
                if (i > 0) sb.Append(',');
                sb.Append('{');
                Num(sb, "floor", c.Floor, true);
                Str(sb, "room", c.Room == RoomKind.Boss ? "boss" : c.Room == RoomKind.Elite ? "elite" : "combat");
                Str(sb, "encounter", c.Encounter);
                Key(sb, "enemies"); Strings(sb, c.Enemies);
                Num(sb, "enemy_hp", c.EnemyHp);
                Str(sb, "outcome", c.Outcome == CombatOutcome.Won ? "won" : c.Outcome == CombatOutcome.Lost ? "lost" : "abandoned");
                Num(sb, "turns", c.Turns);
                Num(sb, "hp_start", c.HpStart);
                Num(sb, "hp_end", c.HpEnd);
                Num(sb, "max_hp", c.MaxHp);
                Num(sb, "hp_lost", c.HpLost);
                Num(sb, "damage", c.Damage);
                Num(sb, "cards_played", c.CardsPlayed);
                Num(sb, "mana", c.Mana);
                Key(sb, "deck"); Strings(sb, c.Deck);
                Key(sb, "relics"); Strings(sb, c.Relics);
                Key(sb, "cards"); sb.Append('[');
                for (var k = 0; k < c.Cards.Count; k++)
                {
                    var u = c.Cards[k];
                    if (k > 0) sb.Append(',');
                    sb.Append('{'); Str(sb, "id", u.Id, true); Num(sb, "plays", u.Plays); Num(sb, "mana", u.Mana); Num(sb, "damage", u.Damage); Num(sb, "block", u.Block); sb.Append('}');
                }
                sb.Append("]}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public static string Hero(HeroKind hero) => hero == HeroKind.Mage ? "mage" : hero == HeroKind.Rogue ? "rogue" : "warrior";
        public static string Outcome(RunEnd? end) => end == RunEnd.Death ? "death" : end == RunEnd.Abandon ? "abandon" : end == RunEnd.Quit ? "quit" : "unfinished";
        private static string Chroma(ChromaKind chroma) => chroma == ChromaKind.Golden ? "golden" : chroma == ChromaKind.Diamond ? "diamond" : "none";

        private static void Key(StringBuilder sb, string key, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":");
        }
        private static void Num(StringBuilder sb, string key, long value, bool first = false) { Key(sb, key, first); sb.Append(value.ToString(CultureInfo.InvariantCulture)); }
        private static void Str(StringBuilder sb, string key, string value, bool first = false) { Key(sb, key, first); Quote(sb, value); }
        private static void Strings(StringBuilder sb, List<string> values)
        {
            sb.Append('[');
            for (var i = 0; i < values.Count; i++) { if (i > 0) sb.Append(','); Quote(sb, values[i]); }
            sb.Append(']');
        }
        private static void Quote(StringBuilder sb, string value)
        {
            if (value == null) { sb.Append("null"); return; }
            sb.Append('"');
            foreach (var ch in value)
            {
                if (ch == '"' || ch == '\\') sb.Append('\\').Append(ch);
                else if (ch < 32) sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                else sb.Append(ch);
            }
            sb.Append('"');
        }
    }
}
