using System;
using System.Collections.Generic;
using System.Numerics;

namespace LastWish.Domain
{
    public enum StatusKind { Poison, Marked, Weak, Vulnerable, Frail, Entangled, Strength, Blades, Fuse, Fire, Water, Earth, Air, Frozen, Thorns, Shell, Carrion, Swallowed, Focus, Parry, Vigil }
    public enum ElementKind { Fire, Water, Earth, Air }
    public enum IntentKind { Unknown, Attack, Block, Buff, Debuff, Summon, Stun }

    public sealed class StatusSet
    {
        /// <summary>Number of status kinds (the arrays follow the enum, so the expansion statuses fit).</summary>
        public static readonly int KindCount = Enum.GetValues(typeof(StatusKind)).Length;
        private readonly int[] amounts = new int[KindCount];
        private readonly List<StatusKind> order = new List<StatusKind>(KindCount);
        public int this[StatusKind kind] => amounts[(int)kind];
        public void Add(StatusKind kind, int amount)
        {
            if (amount <= 0) return;
            if (amounts[(int)kind] == 0) { order.Remove(kind); order.Add(kind); }
            amounts[(int)kind] = (int)Math.Min(int.MaxValue, (long)amounts[(int)kind] + amount);
        }
        public void Remove(StatusKind kind) { amounts[(int)kind] = 0; order.Remove(kind); }
        /// <summary>Sets a status to an exact value (0 or less removes it).</summary>
        internal void Set(StatusKind kind, int amount)
        {
            if (amount <= 0) { Remove(kind); return; }
            if (amounts[(int)kind] == 0) { order.Remove(kind); order.Add(kind); }
            amounts[(int)kind] = amount;
        }
        /// <summary>Active statuses in the order they were gained (the original keeps a list).</summary>
        public IEnumerable<StatusKind> Active { get { foreach (var k in order) if (amounts[(int)k] > 0) yield return k; } }
        public int Count { get { var n = 0; foreach (var k in order) if (amounts[(int)k] > 0) n++; return n; } }
        public void CopyFrom(StatusSet other) { Array.Copy(other.amounts, amounts, amounts.Length); order.Clear(); order.AddRange(other.order); }
        public void Tick(StatusKind kind) { if (amounts[(int)kind] > 0) amounts[(int)kind]--; }
        public int DebuffKinds
        {
            get
            {
                var count = 0;
                for (var i = 0; i < (int)StatusKind.Thorns; i++)   // the expansion statuses are enemy buffs
                    if (amounts[i] > 0 && i != (int)StatusKind.Strength && i != (int)StatusKind.Blades && i != (int)StatusKind.Fuse) count++;
                return count;
            }
        }
    }
    public readonly struct StatusAmount
    {
        public StatusKind Kind { get; }
        public int Amount { get; }
        public StatusAmount(StatusKind kind, int amount) { Kind = kind; Amount = amount; }
    }
    public readonly struct StatusCardSpawn
    {
        public string Id { get; }
        public int Count { get; }
        public string Pile { get; }
        public StatusCardSpawn(string id, int count, string pile) { Id = id; Count = count; Pile = pile; }
    }
    public sealed class EnemyIntent
    {
        public IntentKind Kind { get; }
        public int Value { get; internal set; }
        public int Hits { get; internal set; }
        public string Name { get; }
        public string Id { get; }
        public string Description { get; }
        public int Block { get; }
        public StatusAmount[] Buffs { get; internal set; }
        public StatusAmount[] Debuffs { get; }
        public StatusAmount[] AllyBuffs { get; internal set; }
        public StatusCardSpawn[] Cards { get; }
        public bool Lifesteal { get; }
        public bool SelfDestruct { get; }
        public int AllyBlock { get; internal set; }
        public int HealAllies { get; }
        /// <summary>Expansion effects (summons, steal Strength, cleanse…); <see cref="IntentExtras.None"/> for the original enemies.</summary>
        public IntentExtras Extras { get; internal set; } = IntentExtras.None;
        public EnemyIntent(IntentKind kind, int value = 0, int hits = 1, string name = "", string id = "", string description = "",
            int block = 0, StatusAmount[] buffs = null, StatusAmount[] debuffs = null, StatusAmount[] allyBuffs = null,
            StatusCardSpawn[] cards = null, bool lifesteal = false, bool selfDestruct = false, int allyBlock = 0, int healAllies = 0)
        {
            Kind = kind; Value = value; Hits = hits; Name = name; Id = id; Description = description; Block = block;
            Buffs = buffs ?? Array.Empty<StatusAmount>(); Debuffs = debuffs ?? Array.Empty<StatusAmount>();
            AllyBuffs = allyBuffs ?? Array.Empty<StatusAmount>(); Cards = cards ?? Array.Empty<StatusCardSpawn>();
            Lifesteal = lifesteal; SelfDestruct = selfDestruct; AllyBlock = allyBlock; HealAllies = healAllies;
        }
    }
    public sealed class EnemyDefinition
    {
        public string Ai { get; }
        public string Name { get; }
        public string AssetId { get; }
        public int BaseHp { get; }
        public bool Boss { get; }
        public bool Elite { get; }
        public EnemyDefinition(string ai, string name, string assetId, int hp, bool boss = false, bool elite = false)
        { Ai = ai; Name = name; AssetId = assetId; BaseHp = hp; Boss = boss; Elite = elite; }
    }
    public sealed class EnemyState
    {
        public string Id { get; }
        public EnemyDefinition Definition { get; }
        public int Floor { get; }
        public int MaxHp { get; }
        public int Hp { get; private set; }
        public int Block { get; private set; }
        public bool Alive => Hp > 0;
        public StatusSet Statuses { get; } = new StatusSet();
        public EnemyIntent Intent { get; internal set; }
        public string PartnerId { get; internal set; }
        public string PairName { get; internal set; }
        public int Step { get; internal set; }
        public int Charges { get; internal set; }
        public bool Enraged { get; internal set; }
        public bool VengeanceUsed { get; internal set; }
        // ---- expansion state (zero / false for the original enemies)
        /// <summary>Added during the fight (summon, split or revival).</summary>
        public bool Summoned { get; internal set; }
        /// <summary>Counts for the victory gold (summoned minions do not).</summary>
        public bool PaysGold { get; internal set; } = true;
        public string SummonerId { get; internal set; }
        /// <summary>Damage dealt back for each hit parried during one of the hero's cards (Réplica).</summary>
        public int Riposte { get; internal set; }
        /// <summary>Minions this enemy has summoned in this fight (summoners have a budget).</summary>
        public int SummonsMade { get; internal set; }
        internal bool PhaseTwo, PartnerHandled, ReviveUsed, AwaitingExhume, GriefUsed, SplitDone, FocusBroken;
        internal int ParriedHits, DamagingHits;
        internal List<CardDefinition> Stomach = new List<CardDefinition>();
        /// <summary>Cards it swallowed (they go back to the discard pile when it dies).</summary>
        public IReadOnlyList<CardDefinition> SwallowedCards => Stomach;
        public EnemyState(string id, EnemyDefinition definition, int floor, int hp)
        { Id = id; Definition = definition; Floor = floor; MaxHp = Hp = hp; }
        /// <summary>An independent copy (play previews); the intent object is shared because intents are never mutated after planning.</summary>
        internal EnemyState Clone()
        {
            var copy = new EnemyState(Id, Definition, Floor, MaxHp) { Hp = Hp, Block = Block, Intent = Intent, PartnerId = PartnerId, PairName = PairName, Step = Step, Charges = Charges, Enraged = Enraged, VengeanceUsed = VengeanceUsed };
            copy.Statuses.CopyFrom(Statuses);
            copy.Summoned = Summoned; copy.PaysGold = PaysGold; copy.SummonerId = SummonerId; copy.Riposte = Riposte; copy.SummonsMade = SummonsMade;
            copy.PhaseTwo = PhaseTwo; copy.PartnerHandled = PartnerHandled; copy.ReviveUsed = ReviveUsed; copy.AwaitingExhume = AwaitingExhume;
            copy.GriefUsed = GriefUsed; copy.SplitDone = SplitDone; copy.FocusBroken = FocusBroken;
            copy.ParriedHits = ParriedHits; copy.DamagingHits = DamagingHits;
            copy.Stomach = new List<CardDefinition>(Stomach);
            return copy;
        }
        public void GainBlock(int amount) { if (Alive && amount > 0) Block = (int)Math.Min(int.MaxValue, (long)Block + amount); }
        public void ClearBlock() => Block = 0;
        /// <summary>Blindaje: keeps at most <paramref name="max"/> block (0 clears it like <see cref="ClearBlock"/>).</summary>
        public void KeepBlock(int max) => Block = Math.Min(Block, Math.Max(0, max));
        public int LoseHp(int amount)
        {
            var lost = Math.Min(Hp, Math.Max(0, amount)); Hp -= lost;
            if (lost > 0 && Statuses[StatusKind.Focus] > 0)
            {
                var focus = Math.Max(0, Statuses[StatusKind.Focus] - lost);
                Statuses.Set(StatusKind.Focus, focus);
                if (focus == 0) FocusBroken = true;
            }
            return lost;
        }
        public int Heal(int amount)
        { var gain = Alive ? Math.Min(MaxHp - Hp, Math.Max(0, amount)) : 0; Hp += gain; return gain; }
        public int Damage(BigInteger amount)
        {
            if (!Alive || amount <= 0) return 0;
            if (Statuses[StatusKind.Parry] > 0) { Statuses.Set(StatusKind.Parry, Statuses[StatusKind.Parry] - 1); ParriedHits++; return 0; }
            amount += (BigInteger)Statuses[StatusKind.Marked] + Statuses[StatusKind.Earth];
            var absorbed = (int)BigInteger.Min(amount, Block); Block -= absorbed; amount -= absorbed;
            var lost = LoseHp((int)BigInteger.Min(amount, Hp));
            if (absorbed > 0 || lost > 0) DamagingHits++;
            return lost;
        }
        public void Plan(StatusSet hero, IReadOnlyList<EnemyState> enemies) => Intent = EnemyPatterns.Next(this, hero, enemies);
        public int HitDamage(StatusSet hero)
        {
            var amount = Math.Max(0, (long)Intent.Value + Statuses[StatusKind.Strength] - Statuses[StatusKind.Water]);
            if (Statuses[StatusKind.Weak] > 0) amount = amount * 3 / 4;
            if (Statuses[StatusKind.Frozen] > 0) amount = Definition.Boss || Definition.Elite ? amount / 2 : 0;
            if (hero[StatusKind.Vulnerable] > 0) amount = amount * 3 / 2;
            return (int)Math.Min(int.MaxValue, amount);
        }
    }
    public static partial class EnemyPatterns
    {
        public static EnemyIntent Next(EnemyState enemy, StatusSet hero, IReadOnlyList<EnemyState> enemies)
        {
            if (EnemyCatalog.IsExpansion(enemy.Definition.Ai)) return ExpansionNext(enemy, hero, enemies);
            if (!enemy.VengeanceUsed && PartnerDead(enemy, enemies))
            { enemy.VengeanceUsed = true; return Vengeance(enemy.Floor); }
            var ai = enemy.Definition.Ai;
            if ((enemy.Definition.Boss || enemy.Definition.Elite) && !enemy.Enraged && enemy.Hp * 2L <= enemy.MaxHp)
            { enemy.Enraged = true; return Enrage(ai, enemy.Floor); }
            if (ai == "seta")
                return Normal(ai, enemy.Statuses[StatusKind.Fuse] <= 1 ? 2 : enemy.Statuses[StatusKind.Fuse] == 2 ? 1 : 0, enemy.Floor);
            var step = enemy.Step++;
            if (ai == "acolito" && step % 4 == 2)
                foreach (var ally in enemies) if (ally.Alive && ally.Hp < ally.MaxHp) return Mend(enemy.Floor);
            var intent = Normal(ai, step, enemy.Floor);
            if (ai == "tejedora" && intent.Id == "feast") intent.Hits = 1 + hero.DebuffKinds;
            if (ai == "caballero")
            {
                if (intent.Id == "blade_dance") intent.Hits = Math.Max(1, enemy.Statuses[StatusKind.Blades]);
                if (intent.Id == "summon" && enemy.Statuses[StatusKind.Blades] >= 6) intent.Buffs = Array.Empty<StatusAmount>();
            }
            if (ai == "minotauro" && intent.Id == "charge") intent.Value = EnemyCatalog.Scale(15 + 5 * enemy.Charges++, enemy.Floor);
            var hasAlly = false;
            foreach (var ally in enemies) if (ally != enemy && ally.Alive) { hasAlly = true; break; }
            if (!hasAlly) { intent.AllyBuffs = Array.Empty<StatusAmount>(); intent.AllyBlock = 0; }
            return intent;
        }
        private static bool PartnerDead(EnemyState enemy, IReadOnlyList<EnemyState> enemies)
        {
            if (string.IsNullOrEmpty(enemy.PartnerId)) return false;
            foreach (var other in enemies) if (other.Id == enemy.PartnerId) return !other.Alive;
            return true;
        }
        public static void ReactToDeaths(IReadOnlyList<EnemyState> enemies)
        {
            foreach (var enemy in enemies)
                if (!EnemyCatalog.IsExpansion(enemy.Definition.Ai) && enemy.Alive && !enemy.VengeanceUsed && PartnerDead(enemy, enemies))
                { enemy.VengeanceUsed = true; enemy.Intent = Vengeance(enemy.Floor); }
            ExpansionReact(enemies);
        }
    }
}
