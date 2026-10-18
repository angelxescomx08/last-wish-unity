using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

namespace LastWish.Domain
{
    public enum ReactionKind { Steam, Magma, Wildfire, Mud, Freeze, Sandstorm }
    public readonly struct ReactionEvent
    {
        public string EnemyId { get; }
        public ReactionKind Kind { get; }
        public int Stacks { get; }
        public ReactionEvent(string enemyId, ReactionKind kind, int stacks) { EnemyId = enemyId; Kind = kind; Stacks = stacks; }
    }
    public sealed partial class CombatState
    {
        private readonly List<CardDefinition> draw = new List<CardDefinition>();
        private readonly List<CardDefinition> discard = new List<CardDefinition>();
        private readonly List<CardDefinition> hand = new List<CardDefinition>();
        private readonly List<CardDefinition> exhausted = new List<CardDefinition>();
        private readonly List<EnemyState> enemies;
        private readonly List<ReactionEvent> reactions = new List<ReactionEvent>();
        private readonly Random random;
        private readonly int handSize;
        private readonly bool legacy;
        private readonly int legacyDamage;
        private int cardsPlayed;
        public ReadOnlyCollection<CardDefinition> Hand { get; }
        public ReadOnlyCollection<EnemyState> Enemies { get; }
        public ReadOnlyCollection<ReactionEvent> Reactions { get; }
        public StatusSet HeroStatuses { get; } = new StatusSet();
        public HeroDefinition Hero { get; }
        public int Hp { get; private set; }
        public int Block { get; private set; }
        public int Mana { get; private set; }
        public int Strength { get; private set; }
        public int Turn { get; private set; } = 1;
        public int SelectedEnemy { get; private set; }
        public EnemyState Target => enemies[SelectedEnemy];
        public int EnemyHp => Target.Hp;
        public int EnemyMaxHp => Target.MaxHp;
        public string EnemyName => Target.Definition.Name;
        public int EnemyBlock => Target.Block;
        public int EnemyDamage => legacy ? legacyDamage + (Turn - 1) / 3 : Target.HitDamage(HeroStatuses);
        public bool EnemyIntendsBlock => legacy ? Turn % 3 == 0 : Target.Intent.Kind != IntentKind.Attack;
        public int Fire => Target.Statuses[StatusKind.Fire];
        public int DrawCount => draw.Count;
        public int DiscardCount => discard.Count;
        public int ExhaustedCount => exhausted.Count;
        public bool Defeat => Hp <= 0;
        public bool Victory => !Defeat && enemies.All(e => !e.Alive);
        public bool Finished => Defeat || Victory;

        public CombatState(HeroDefinition hero, IEnumerable<CardDefinition> deck, int seed, int initialHp = -1,
            int enemyHp = 45, int enemyDamage = 8, string enemyName = "Espectro", int extraDraw = 0)
            : this(hero, deck, seed, initialHp, new[] { new EnemyState("demo", new EnemyDefinition("demo", enemyName, "wraith", enemyHp), 1, enemyHp) }, extraDraw)
        {
            if (enemyHp < 1 || enemyDamage < 0) throw new ArgumentOutOfRangeException();
            legacy = true; legacyDamage = enemyDamage;
            Target.Intent = new EnemyIntent(IntentKind.Attack, enemyDamage, name: "Ataque", id: "demo");
        }
        public CombatState(HeroDefinition hero, IEnumerable<CardDefinition> deck, int seed, int initialHp,
            IEnumerable<EnemyState> encounter, int extraDraw = 0)
        {
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));
            if (deck == null || encounter == null) throw new ArgumentNullException();
            foreach (var card in deck) draw.Add(card ?? throw new ArgumentException("Null card"));
            enemies = encounter.ToList();
            if (draw.Count == 0 || enemies.Count == 0 || enemies.Any(e => e == null || !e.Alive)) throw new ArgumentException("Empty deck or invalid encounter");
            if (initialHp < -1 || initialHp > hero.MaxHp || extraDraw < 0 || extraDraw > 100) throw new ArgumentOutOfRangeException();
            singularDeck = draw.Select(c => c.Id).Distinct().Count() == draw.Count;
            random = new Random(seed); handSize = 5 + extraDraw;
            turnStartAlive = enemies.Count;
            Hp = initialHp == -1 ? hero.MaxHp : initialHp; Mana = hero.MaxMana;
            Hand = hand.AsReadOnly(); Enemies = enemies.AsReadOnly(); Reactions = reactions.AsReadOnly();
            Shuffle(); DrawCards(handSize);
        }
        public bool SelectEnemy(int index)
        {
            if (Finished || index < 0 || index >= enemies.Count || !enemies[index].Alive) return false;
            SelectedEnemy = index; EffectVersion++; return true;
        }
        private void EnsureTarget()
        {
            if (Target.Alive) return;
            for (var i = 0; i < enemies.Count; i++) if (enemies[i].Alive) { SelectedEnemy = i; return; }
        }
        public bool CanPlay(int index) => !Finished && index >= 0 && index < hand.Count && CardCost(hand[index]) <= Mana
            && hand[index].Id != "status_moho" && hand[index].Id != "status_herida";
        public bool PlayCard(int index)
        {
            if (!CanPlay(index)) return false;
            var card = hand[index]; var target = SelectedEnemy; var endure = Block > 0;
            var mask = ActiveConditions(card); reactions.Clear();
            Mana -= CardCost(card); nextDiscount = 0; hand.RemoveAt(index);
            (card.Kind == CardKind.Power ? powers : card.Id == "status_espora" ? exhausted : discard).Add(card);
            var extraCombo = card.Effects != null && (mask & 1u << (int)EffectCondition.Combo) != 0 && card.Effects.Any(e => e.Condition == EffectCondition.Combo) && !doubleComboUsed ? doubleCombo : 0;
            if (extraCombo > 0) doubleComboUsed = true;
            var casts = 1 + (echoFifth > 0 && (cardsPlayed + 1) % 5 == 0 ? echoFifth : 0);
            var printedDamage = card.Damage.Value;
            if (card.Effects != null) { printedDamage = 0; foreach (var layer in card.Effects) if (Active(layer, mask)) printedDamage += layer.Damage; }
            var damageBonus = printedDamage > 0 ? nextDamage : 0; if (printedDamage > 0) nextDamage = 0;
            for (var cast = 0; cast < casts; cast++)
            {
                var before = executeWounded && card.IsAttack ? enemies.Select(e => e.Hp).ToArray() : null;
                if (!enemies[target].Alive) { EnsureTarget(); target = SelectedEnemy; }
                if (card.Effects != null)
                { ResolveNumeric(card, target, mask, damageBonus); for (var repeat = 0; repeat < extraCombo; repeat++) RepeatCombo(card, target); }
                else
                {
                    DamageFromCard(target, card.Damage.Value, damageBonus);
                    GainCardBlock((long)card.Block + (cardsPlayed > 0 ? card.ComboBlock : 0));
                    if (endure) GainStrength(card.EndureStrength);
                    GainMana(card.ManaGain);
                    if (card.FireStacks > 0) ApplyElement(target, ElementKind.Fire, card.FireStacks);
                    DrawCards(card.Draw);
                }
                if (card.IsAttack && poisonAttacks > 0)
                {
                    poisonAttacks--; var poisoned = enemies[target].Alive ? enemies[target] : enemies.FirstOrDefault(e => e.Alive);
                    if (poisoned != null) poisoned.Statuses.Add(StatusKind.Poison, 2);
                }
                if (before != null) for (var i = 0; i < enemies.Count; i++)
                    if (enemies[i].Hp < before[i] && enemies[i].Hp * 4L < enemies[i].MaxHp) enemies[i].LoseHp(enemies[i].Hp);
            }
            SettlePickpocket();
            cardsPlayed++; EffectVersion++;
            EnemyPatterns.ReactToDeaths(enemies); EnsureTarget(); return true;
        }
        public bool EndTurn()
        {
            if (Finished) return false;
            SettlePickpocket();
            AddBlock(endTurnBlock);
            nextDiscount = 0; nextDamage = 0; pickpocketGold = 0;
            foreach (var card in hand)
            {
                if (card.Id == "status_espora") HeroStatuses.Add(StatusKind.Poison, 3);
                (card.Id == "status_moho" ? exhausted : discard).Add(card);
            }
            hand.Clear();
            HeroStatuses.Tick(StatusKind.Weak); HeroStatuses.Tick(StatusKind.Vulnerable); HeroStatuses.Tick(StatusKind.Frail);
            foreach (var enemy in enemies) { enemy.ClearBlock(); enemy.Statuses.Remove(StatusKind.Marked); }
            if (legacy) ExecuteLegacy(); else foreach (var enemy in enemies) ExecuteEnemy(enemy);
            if (!legacy)
                foreach (var enemy in enemies) if (enemy.Alive) PlanEnemy(enemy);
            EnemyPatterns.ReactToDeaths(enemies);
            if (!retainBlock) Block = 0; retainBlock = false; catalyst = 0;
            cardsPlayed = 0; discardsThisTurn = 0; decoys = 0; counterDamage = 0; doubleComboUsed = false; turnStartAlive = enemies.Count(e => e.Alive); HpLostThisTurn = 0; EffectVersion++;
            if (!Finished)
            {
                Turn++; Mana = Hero.MaxMana; reactions.Clear();
                var poison = Math.Min(Hp, HeroStatuses[StatusKind.Poison]); Hp -= poison; OnHpLost(poison, true); HeroStatuses.Tick(StatusKind.Poison);
                var drawCount = Math.Max(0, handSize - HeroStatuses[StatusKind.Entangled]); HeroStatuses.Remove(StatusKind.Entangled);
                if (!Defeat) { TriggerPowers(); var extra = pendingDraws; pendingDraws = 0; DrawCards(drawCount + extra); AfterHandDrawn(); }
            }
            EnsureTarget(); return true;
        }
        private void ExecuteLegacy()
        {
            if (EnemyIntendsBlock) Target.GainBlock(8); else HitHero(EnemyDamage);
            if (!Defeat) Target.LoseHp(Fire);
        }
        private void ExecuteEnemy(EnemyState enemy)
        {
            if (!enemy.Alive) return;
            enemy.LoseHp(enemy.Statuses[StatusKind.Poison]); enemy.Statuses.Tick(StatusKind.Poison);
            if (!enemy.Alive) return;
            if (enemy.Statuses[StatusKind.Frozen] > 0 && !enemy.Definition.Boss && !enemy.Definition.Elite)
            { enemy.Statuses.Tick(StatusKind.Frozen); enemy.LoseHp(enemy.Statuses[StatusKind.Fire]); return; }
            var intent = enemy.Intent;
            var lost = 0L;
            if (intent.Kind == IntentKind.Attack)
                for (var hit = 0; hit < Math.Max(1, intent.Hits) && !Defeat && enemy.Alive; hit++) lost += EnemyHit(enemy, enemy.HitDamage(HeroStatuses));
            if (intent.Kind == IntentKind.Block) enemy.GainBlock(intent.Value);
            if (intent.Id == "" && intent.Kind == IntentKind.Debuff) HeroStatuses.Add(StatusKind.Weak, 2);
            if (intent.Lifesteal) enemy.Heal((int)Math.Min(int.MaxValue, lost));
            enemy.GainBlock(intent.Block);
            foreach (var buff in intent.Buffs) enemy.Statuses.Add(buff.Kind, buff.Amount);
            foreach (var debuff in intent.Debuffs) HeroStatuses.Add(debuff.Kind, debuff.Amount);
            foreach (var spawn in intent.Cards) SpawnStatusCards(spawn);
            foreach (var ally in enemies)
            {
                if (!ally.Alive) continue;
                if (ally != enemy)
                {
                    ally.GainBlock(intent.AllyBlock);
                    foreach (var buff in intent.AllyBuffs) ally.Statuses.Add(buff.Kind, buff.Amount);
                }
                ally.Heal(intent.HealAllies);
            }
            if (intent.SelfDestruct) enemy.LoseHp(enemy.Hp);
            enemy.Statuses.Tick(StatusKind.Weak); enemy.Statuses.Tick(StatusKind.Vulnerable); enemy.Statuses.Tick(StatusKind.Frozen);
            if (enemy.Definition.Ai == "seta" && enemy.Statuses[StatusKind.Fuse] > 1) enemy.Statuses.Tick(StatusKind.Fuse);
            enemy.LoseHp(enemy.Statuses[StatusKind.Fire]);
        }
        private int EnemyHit(EnemyState enemy, int damage)
        {
            if (decoys > 0)
            {
                var others = enemies.Where(e => e != enemy && e.Alive).ToArray();
                if (others.Length > 0) { decoys--; others[random.Next(others.Length)].Damage(damage); return 0; }
            }
            var fullyBlocked = damage > 0 && Block >= damage;
            var lost = HitHero(damage); if (fullyBlocked && counterDamage > 0) enemy.Damage(counterDamage);
            return lost;
        }
        private int HitHero(int damage)
        {
            var absorbed = Math.Min(Block, damage); Block -= absorbed;
            var lost = Math.Min(Hp, damage - absorbed); Hp -= lost; OnHpLost(lost, false); return lost;
        }
        private void PlanEnemy(EnemyState enemy)
        {
            if (enemy.Definition.Ai != "crypt") { enemy.Plan(HeroStatuses, enemies); return; }
            var roll = random.NextDouble();
            enemy.Intent = roll < .55 ? new EnemyIntent(IntentKind.Attack, random.Next(5, 16))
                : roll < .80 ? new EnemyIntent(IntentKind.Block, random.Next(4, 11))
                : new EnemyIntent(roll < .92 ? IntentKind.Buff : IntentKind.Debuff);
        }
        public bool ApplyElement(int index, ElementKind element, int stacks) => ApplyElement(index, element, stacks, true);
        private bool ApplyElement(int index, ElementKind element, int stacks, bool spread)
        {
            if (index < 0 || index >= enemies.Count || !enemies[index].Alive || stacks <= 0) return false;
            EffectVersion++;
            var enemy = enemies[index]; var incoming = (StatusKind)((int)StatusKind.Fire + (int)element);
            var previous = incoming;
            for (var i = 0; i < 4; i++) if (enemy.Statuses[(StatusKind)((int)StatusKind.Fire + i)] > 0) { previous = (StatusKind)((int)StatusKind.Fire + i); break; }
            if (previous == incoming) { enemy.Statuses.Add(incoming, stacks); return false; }
            var total = (int)Math.Min(int.MaxValue, (long)stacks + enemy.Statuses[previous]);
            for (var i = 0; i < 4; i++) enemy.Statuses.Remove((StatusKind)((int)StatusKind.Fire + i));
            var repeats = catalyst > 0 ? 2 : 1; if (catalyst > 0) catalyst--;
            for (var repeat = 0; repeat < repeats; repeat++) ResolveReaction(enemy, previous, incoming, total, spread);
            return true;
        }
        private void ResolveReaction(EnemyState enemy, StatusKind previous, StatusKind incoming, int total, bool spread)
        {
            var index = enemies.IndexOf(enemy);
            var a = Math.Min((int)previous, (int)incoming); var b = Math.Max((int)previous, (int)incoming);
            ReactionKind reaction;
            if (a == (int)StatusKind.Fire && b == (int)StatusKind.Water) { reaction = ReactionKind.Steam; foreach (var other in enemies) if (other == enemy || archmage && other.Alive) other.Damage((BigInteger)2 * total); }
            else if (a == (int)StatusKind.Fire && b == (int)StatusKind.Earth) { reaction = ReactionKind.Magma; foreach (var other in enemies) if (other == enemy || archmage && other.Alive) { other.ClearBlock(); other.Statuses.Add(StatusKind.Vulnerable, 2); } }
            else if (a == (int)StatusKind.Fire && b == (int)StatusKind.Air)
            {
                reaction = ReactionKind.Wildfire; enemy.Statuses.Add(StatusKind.Fire, total);
                if (spread) for (var i = 0; i < enemies.Count; i++) if (i != index) ApplyElement(i, ElementKind.Fire, total, false);
            }
            else if (a == (int)StatusKind.Water && b == (int)StatusKind.Earth) { reaction = ReactionKind.Mud; Block = (int)Math.Min(int.MaxValue, Block + 3L * total); }
            else if (a == (int)StatusKind.Water && b == (int)StatusKind.Air) { reaction = ReactionKind.Freeze; foreach (var other in enemies) if ((other == enemy || archmage && other.Alive) && other.Statuses[StatusKind.Frozen] == 0) other.Statuses.Add(StatusKind.Frozen, 1); }
            else { reaction = ReactionKind.Sandstorm; foreach (var other in enemies) if (other.Alive) other.Statuses.Add(StatusKind.Weak, 1); }
            AddBlock(reactionBlock); pendingDraws = Saturate((long)pendingDraws + reactionDraw);
            reactions.Add(new ReactionEvent(enemy.Id, reaction, total)); EnemyPatterns.ReactToDeaths(enemies);
        }
        public static CardDefinition StatusCard(string id)
        {
            switch (id)
            {
                case "status_espora": return new CardDefinition(id, "Espora", 1, 0, 0);
                case "status_moho": return new CardDefinition(id, "Moho", 0, 0, 0);
                case "status_herida": return new CardDefinition(id, "Herida", 0, 0, 0);
                default: throw new ArgumentException("Unknown status card");
            }
        }
        private void SpawnStatusCards(StatusCardSpawn spawn)
        {
            for (var i = 0; i < spawn.Count; i++)
            {
                var card = StatusCard(spawn.Id);
                if (spawn.Pile == "draw") draw.Insert(random.Next(draw.Count + 1), card);
                else if (spawn.Pile == "hand" && hand.Count < 10) hand.Add(card);
                else discard.Add(card);
            }
        }
        private void OnDraw(CardDefinition card) { if (card.Id == "status_moho") Mana = Math.Max(0, Mana - 1); }
        private void DrawCards(int count)
        {
            for (var i = 0; i < Math.Max(0, count) && hand.Count < 10; i++)
            {
                if (draw.Count == 0) { if (discard.Count == 0) break; draw.AddRange(discard); discard.Clear(); Shuffle(); }
                var index = draw.Count - 1; var card = draw[index]; draw.RemoveAt(index);
                if (card.Id == "t_daga_oculta") { HitRandom(4); HideDaggers(daggersOnDraw); }
                else { hand.Add(card); OnDraw(card); }
            }
        }
        private void Shuffle()
        {
            for (var i = draw.Count - 1; i > 0; i--) { var other = random.Next(i + 1); var card = draw[i]; draw[i] = draw[other]; draw[other] = card; }
        }
    }
}
