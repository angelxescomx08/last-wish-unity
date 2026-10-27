using System.Collections.Generic;

namespace LastWish.Domain
{
    /// <summary>How a run ended (telemetry). A run that is still on is "unfinished".</summary>
    public enum RunEnd { Death, Abandon, Quit }

    /// <summary>How a fight ended (telemetry): abandoned = the run was left in the middle of it.</summary>
    public enum CombatOutcome { Won, Lost, Abandoned }

    /// <summary>One card in one fight: plays, mana paid, HP the plays removed from enemies and block they gave.</summary>
    public sealed class CardUse
    {
        public string Id = "";
        public int Plays;
        public long Mana, Damage, Block;
    }

    /// <summary>One fight of the run: who, how it went, and what the hero brought (deck and relics at the start).</summary>
    public sealed class CombatRecord
    {
        public int Floor;
        public RoomKind Room;
        /// <summary>The boss fight key (e.g. "caballero") or the enemy kinds joined with "+".</summary>
        public string Encounter = "";
        public readonly List<string> Enemies = new List<string>();
        public int EnemyHp, Turns, HpStart, HpEnd, MaxHp, HpLost, CardsPlayed;
        /// <summary>HP removed from enemies by card plays (poison and fire ticks of the enemy turn are not counted).</summary>
        public long Damage;
        public long Mana;
        /// <summary>Null while the fight is still on.</summary>
        public CombatOutcome? Outcome;
        public readonly List<string> Deck = new List<string>();
        public readonly List<string> Relics = new List<string>();
        public readonly List<CardUse> Cards = new List<CardUse>();
    }

    /// <summary>Cards or relics the game offered and what the player took (none = skipped).</summary>
    public sealed class ChoiceRecord
    {
        public int Floor;
        public bool Relic;
        public string Source = "";
        public readonly List<string> Offered = new List<string>();
        public readonly List<string> Picked = new List<string>();
    }

    /// <summary>A relic the hero owns at the end, with where it was obtained.</summary>
    public sealed class RelicRecord
    {
        public string Id = "";
        public ChromaKind Chroma;
        /// <summary>0 when the game gave it outside the tracked rooms.</summary>
        public int Floor;
        public string Source = "unknown";
    }

    /// <summary>A card of the deck at the end (or at the last checkpoint).</summary>
    public sealed class DeckEntry
    {
        public string Id = "";
        public bool Upgraded;
        public ChromaKind Chroma;
    }

    /// <summary>
    /// Everything the power telemetry stores about one run (schema 1, docs/telemetry.md). Plain data: the
    /// presentation serialises it and the runtime ships it; the rules never read it.
    /// </summary>
    public sealed class RunRecord
    {
        public const int SchemaVersion = 1;
        public string RunId = "", PlayerId, GameVersion = "dev", ContentHash = "", StartedAt;
        public HeroKind Hero;
        public long Seed;
        public EnemyRoster Roster;
        /// <summary>The Pruebas screen changed the rules: the analytics leave the run out.</summary>
        public bool Tuned;
        /// <summary>Null while the run is on ("unfinished").</summary>
        public RunEnd? Outcome;
        public int Floor = 1, Rooms, Gold, Hp, MaxHp;
        public double PlayedSeconds;
        public readonly List<DeckEntry> Deck = new List<DeckEntry>();
        public readonly List<RelicRecord> Relics = new List<RelicRecord>();
        public readonly List<ChoiceRecord> Choices = new List<ChoiceRecord>();
        public readonly List<CombatRecord> Combats = new List<CombatRecord>();
    }
}
