using System;
using System.Collections.Generic;

namespace LastWish.Domain
{
    /// <summary>
    /// Which enemy content a run rolls. <see cref="Original"/> reproduces the Python game exactly (parity tests);
    /// <see cref="Expanded"/> adds the new floor-1 enemies, groups and bosses (only floor-1 enemy rolls differ).
    /// </summary>
    public enum EnemyRoster { Original, Expanded }

    public enum EnemyTier { Normal, Minion, Elite, Boss }

    /// <summary>Inspect-panel data and rules of an expansion enemy (the original enemies have none).</summary>
    public sealed class EnemyProfile
    {
        public string Ai { get; internal set; }
        /// <summary>Identity name (empty for bosses, like the originals).</summary>
        public string Identity { get; internal set; } = "";
        public string Title { get; internal set; } = "";
        /// <summary>Spanish lines of the inspect panel.</summary>
        public IReadOnlyList<string> Mechanics { get; internal set; } = Array.Empty<string>();
        public EnemyTier Tier { get; internal set; }
        /// <summary>Can be rolled alone by the random encounter pool.</summary>
        public bool SoloPool { get; internal set; }
        public int MinFloor { get; internal set; } = 1;
        public int MaxFloor { get; internal set; } = 1;
        /// <summary>One transition the first time it plans at half HP or less.</summary>
        public bool HasPhase { get; internal set; }
        public IReadOnlyList<StatusAmount> StartStatuses { get; internal set; } = Array.Empty<StatusAmount>();
        /// <summary>Réplica damage per parried hit (unscaled).</summary>
        public int Riposte { get; internal set; }
        /// <summary>Small enemies it adds with summon intents.</summary>
        public IReadOnlyList<string> Summons { get; internal set; } = Array.Empty<string>();
        /// <summary>Most minions it can summon in one fight (0 = it does not summon); afterwards it uses its alternative.</summary>
        public int SummonBudget { get; internal set; }
        /// <summary>Other enemies its intents can bring (split fragments, a revived partner): preloaded with the fight.</summary>
        public IReadOnlyList<string> Spawns { get; internal set; } = Array.Empty<string>();
        /// <summary>Every intent id it can plan.</summary>
        public IReadOnlyList<string> Moves { get; internal set; } = Array.Empty<string>();
    }

    /// <summary>A pair or trio of the expansion (no vengeance link).</summary>
    public sealed class EncounterGroup
    {
        public string Key { get; internal set; }
        public string Name { get; internal set; }
        public string Synergy { get; internal set; }
        public IReadOnlyList<string> Members { get; internal set; }
        public int MinFloor { get; internal set; } = 1;
        public int MaxFloor { get; internal set; } = 1;
        public double HpFactor { get; internal set; } = 0.8;
    }

    /// <summary>A boss fight: one boss, or two linked bosses.</summary>
    public sealed class BossEncounter
    {
        public string Key { get; internal set; }
        public string Name { get; internal set; }
        public string Synergy { get; internal set; } = "";
        public IReadOnlyList<string> Members { get; internal set; }
        public bool Linked { get; internal set; }
        public double HpFactor { get; internal set; } = 1.0;
    }

    public readonly struct SummonSpec
    {
        public string Ai { get; }
        public int Count { get; }
        public double HpFactor { get; }
        public bool PaysGold { get; }
        public SummonSpec(string ai, int count, double hpFactor, bool paysGold) { Ai = ai; Count = count; HpFactor = hpFactor; PaysGold = paysGold; }
    }

    /// <summary>Expansion effects of an intent; immutable after planning (shared with play previews).</summary>
    public sealed class IntentExtras
    {
        public static readonly IntentExtras None = new IntentExtras();
        public SummonSpec[] Summons { get; internal set; } = Array.Empty<SummonSpec>();
        /// <summary>Hero Strength it takes (int.MaxValue = all of it).</summary>
        public int StealStrength { get; internal set; }
        /// <summary>Strength it gains when the hero had none to steal.</summary>
        public int StrengthFallback { get; internal set; }
        /// <summary>Percent of the hero's block it takes before hitting.</summary>
        public int SiphonBlockPercent { get; internal set; }
        public bool Cleanse { get; internal set; }
        public int CleanseBlockPerStack { get; internal set; }
        public int CleanseBlockCap { get; internal set; }
        /// <summary>Cards it swallows from the discard pile.</summary>
        public int Swallow { get; internal set; }
        /// <summary>Own statuses set to an exact value.</summary>
        public StatusAmount[] SetSelf { get; internal set; } = Array.Empty<StatusAmount>();
        public int HealSelf { get; internal set; }
        /// <summary>The prepared blow: losing all the Carga before it acts stuns the enemy.</summary>
        public bool ConsumesFocus { get; internal set; }
        public string SplitInto { get; internal set; }
        public int SplitCount { get; internal set; }
        public string ReviveAi { get; internal set; }
        /// <summary>The value is recalculated after every card (Manada, Embestida Acorazada).</summary>
        public bool Dynamic { get; internal set; }
        public bool Any => this != None;
    }

    /// <summary>The on-screen cap: at most 3 enemies alive, and the mix must fit a formation.</summary>
    public static class EnemyField
    {
        public const int MaxAlive = 3;
        /// <summary>A large enemy (boss sheet) takes the room of two small ones.</summary>
        public static bool IsLarge(EnemyDefinition d) => d.Boss || d.Elite;
        /// <summary>Formations: 0 large + up to 3 small, 1 large + up to 2 small, 2 large alone.</summary>
        public static bool Fits(int large, int small) => small >= 0 && (large == 0 ? small <= 3 : large == 1 ? small <= 2 : large == 2 && small == 0);

        /// <summary>How many more enemies of that size fit, counting alive enemies and arrivals already queued this turn.</summary>
        public static int Free(IEnumerable<EnemyState> alive, IEnumerable<EnemyDefinition> queued, bool large)
        {
            int l = 0, s = 0;
            if (alive != null) foreach (var e in alive) if (e.Alive) { if (IsLarge(e.Definition)) l++; else s++; }
            if (queued != null) foreach (var d in queued) { if (IsLarge(d)) l++; else s++; }
            var n = 0;
            while (n < MaxAlive && (large ? Fits(l + n + 1, s) : Fits(l, s + n + 1))) n++;
            return n;
        }
    }

    /// <summary>What happened when an enemy died (returned swallowed cards, Carroña feeding).</summary>
    public sealed class EnemyDeathEvent
    {
        public string EnemyId { get; internal set; }
        public List<string> ReturnedCards { get; } = new List<string>();
        public List<(string feederId, int strength, int healed)> Carrion { get; } = new List<(string, int, int)>();
    }
}
