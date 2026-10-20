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
        /// <summary>The element the enemy held and the one that arrived (the two icons of the reaction show).</summary>
        public ElementKind First { get; }
        public ElementKind Second { get; }
        public ReactionEvent(string enemyId, ReactionKind kind, int stacks, ElementKind first = ElementKind.Fire, ElementKind second = ElementKind.Water)
        { EnemyId = enemyId; Kind = kind; Stacks = stacks; First = first; Second = second; }
    }
    public sealed partial class CombatState
    {
        private List<CardDefinition> draw = new List<CardDefinition>();
        private List<CardDefinition> discard = new List<CardDefinition>();
        private List<CardDefinition> hand = new List<CardDefinition>();
        private List<CardDefinition> exhausted = new List<CardDefinition>();
        private List<EnemyState> enemies;
        private List<ReactionEvent> reactions = new List<ReactionEvent>();
        /// <summary>Status cards made ethereal by Cadena Rota (they fade at the end of the turn).</summary>
        private HashSet<CardDefinition> fading = new HashSet<CardDefinition>();
        private SeededRandom random;
        private readonly int handSize;
        private readonly bool legacy;
        private readonly int legacyDamage;
        private int cardsPlayed;
        public ReadOnlyCollection<CardDefinition> Hand { get; private set; }
        public ReadOnlyCollection<EnemyState> Enemies { get; private set; }
        public ReadOnlyCollection<ReactionEvent> Reactions { get; private set; }
        public StatusSet HeroStatuses { get; private set; } = new StatusSet();
        public HeroDefinition Hero { get; }
        /// <summary>Pruebas: enemy attacks, poison and own effects never lower the hero's HP.</summary>
        public bool Invincible { get; set; }
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
            IEnumerable<EnemyState> encounter, int extraDraw = 0, IEnumerable<OwnedRelic> relics = null)
        {
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));
            BindRelics(relics); manaMaximum = Saturate((long)hero.MaxMana + RelicPower("combat_amulet"));
            if (deck == null || encounter == null) throw new ArgumentNullException();
            foreach (var card in deck) draw.Add((card ?? throw new ArgumentException("Null card")).Copy());   // like copy.copy: one object per card in this fight
            enemies = encounter.ToList();
            if (draw.Count == 0 || enemies.Count == 0 || enemies.Any(e => e == null || !e.Alive)) throw new ArgumentException("Empty deck or invalid encounter");
            if (initialHp < -1 || initialHp > hero.MaxHp || extraDraw < 0 || extraDraw > 100) throw new ArgumentOutOfRangeException();
            singularDeck = draw.Select(c => c.Id).Distinct().Count() == draw.Count;
            random = new SeededRandom(seed); handSize = 5 + extraDraw + RelicPower("broken_totem") + RelicPower("energy_stone");
            turnStartAlive = enemies.Count;
            Hp = initialHp == -1 ? hero.MaxHp : initialHp; Mana = ManaMaximum;
            Hand = hand.AsReadOnly(); Enemies = enemies.AsReadOnly(); Reactions = reactions.AsReadOnly();
            Shuffle(); CombatStartRelics(); DrawCards(handSize + RelicPower("silent_boots", 2)); OpeningHandRelics(); AfterHandDrawn();
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
            var snapshot = ExpansionSnapshot();
            var card = hand[index]; var target = SelectedEnemy; var endure = Block > 0;
            var mask = ActiveConditions(card); resolvingCard = card; resolvingMask = mask; var paidCost = CardCost(card); reactions.Clear();
            Mana -= CardCost(card); nextDiscount = 0; hand.RemoveAt(index);
            (card.Kind == CardKind.Power ? powers : card.Id == "status_espora" ? exhausted : discard).Add(card);
            var extraCombo = card.Effects != null && (mask & 1u << (int)EffectCondition.Combo) != 0 && card.Effects.Any(e => e.Condition == EffectCondition.Combo) && !doubleComboUsed ? doubleCombo : 0;
            if (extraCombo > 0) doubleComboUsed = true;
            var casts = card.CastCount + (echoFifth > 0 && (cardsPlayed + 1) % 5 == 0 ? echoFifth : 0);
            var printedDamage = card.Damage.Value;
            if (card.Effects != null) { printedDamage = 0; foreach (var layer in card.Effects) if (Active(layer, mask)) printedDamage += layer.Damage; }
            var damageBonus = printedDamage > 0 ? nextDamage : 0; if (printedDamage > 0) nextDamage = 0;
            var result = lastPlay = new PlayResult { Success = true, Casts = casts };
            result.Combo = KeywordResolved(EffectCondition.Combo); result.Singular = KeywordResolved(EffectCondition.Singular); result.Void = KeywordResolved(EffectCondition.Void);
            result.Spoil = KeywordResolved(EffectCondition.Spoil); result.Reaction = KeywordResolved(EffectCondition.Reaction); result.Fury = KeywordResolved(EffectCondition.Fury); result.Endure = KeywordResolved(EffectCondition.Endure);
            for (var cast = 0; cast < casts; cast++)
            {
                var castHpBefore = enemies.Select(e => e.Hp).ToArray(); var castDrawnBefore = drawnToHand;
                var before = executeWounded && card.IsAttack ? enemies.Select(e => e.Hp).ToArray() : null;
                if (!enemies[target].Alive) { EnsureTarget(); target = SelectedEnemy; }
                if (card.Effects != null)
                { ResolveNumeric(card, target, mask, damageBonus); for (var repeat = 0; repeat < extraCombo; repeat++) RepeatCondition(card, target, EffectCondition.Combo);
                    if (KeywordResolved(EffectCondition.Spoil)) for (var repeat=0;repeat<RelicPower("split_dagger");repeat++) RepeatCondition(card,target,EffectCondition.Spoil); }
                else
                {
                    if (card.Id == "t_daga_oculta") DaggerHit();
                    DamageFromCard(target, card.Damage.Value, damageBonus);
                    GainCardBlock((long)card.Block + (cardsPlayed > 0 ? card.ComboBlock : 0));
                    if (endure) GainStrength(card.EndureStrength);
                    GainMana(card.ManaGain);
                    if (card.FireStacks > 0) ApplyElement(target, ElementKind.Fire, card.FireStacks);
                    DrawCards(card.Draw);
                }
                if (card.IsAttack && (poisonAttacks > 0 || attacksPlayed == 0 && HasRelic("poison_vial")))
                {
                    var poisonStacks = attacksPlayed == 0 ? RelicPower("poison_vial") : 0;
                    if (poisonAttacks > 0) { poisonAttacks--; poisonStacks += 2; }
                    var poisoned = enemies[target].Alive ? enemies[target] : enemies.FirstOrDefault(e => e.Alive);
                    PoisonEnemy(poisoned, poisonStacks);
                }
                if (before != null) for (var i = 0; i < enemies.Count; i++)
                    if (enemies[i].Hp < before[i] && enemies[i].Hp * 4L < enemies[i].MaxHp) enemies[i].LoseHp(enemies[i].Hp);
                RecordCast(result, castHpBefore, castDrawnBefore);
            }
            var tailHp = enemies.Select(e => e.Hp).ToArray(); var tailDrawn = drawnToHand;
            AfterPlayRelics(target, paidCost);
            cardsPlayed++; EffectVersion++; AfterCompletedPlay();
            AfterCardExpansion(result, snapshot);
            EnemyPatterns.ReactToDeaths(enemies); HandleEnemyDeaths(); EnsureTarget();
            MergeTail(result, tailHp, tailDrawn);
            return true;
        }
        public bool EndTurn()
        {
            if (Finished) return false;
            enemyLog.Clear();
            SettlePickpocket();
            AddBlock(endTurnBlock); EndTurnRelics();
            nextDiscount = 0; nextDamage = 0; pickpocketGold = 0;
            foreach (var card in hand)
            {
                if (card.Id == "status_espora" && !HasRelic("antidote")) HeroStatuses.Add(StatusKind.Poison, 3);
                (card.Id == "status_moho" || fading.Contains(card) ? exhausted : discard).Add(card);
            }
            hand.Clear();
            HeroStatuses.Tick(StatusKind.Weak); HeroStatuses.Tick(StatusKind.Vulnerable); HeroStatuses.Tick(StatusKind.Frail);
            foreach (var enemy in enemies) { enemy.KeepBlock(enemy.Statuses[StatusKind.Shell]); if (ClearMarked(enemy)) enemy.Statuses.Remove(StatusKind.Marked); }
            hitLastEnemyTurn = false; deathEvents.Clear();
            if (legacy) ExecuteLegacy(); else for (var i = 0; i < enemies.Count; i++) ExecuteEnemy(enemies[i]);
            AppendQueuedSummons();
            if (!legacy)
                foreach (var enemy in enemies) if (enemy.Alive) PlanEnemy(enemy);
            EnemyPatterns.ReactToDeaths(enemies); HandleEnemyDeaths(); CheckRelicDeaths(); SpreadPoison();
            if (!retainBlock && !(Turn == 1 && HasRelic("anchor"))) Block = HasRelic("worn_roundel") ? Block / 2 : 0; retainBlock = false; catalyst = 0;
            cardsPlayed = 0; discardsThisTurn = 0; decoys = 0; counterDamage = 0; doubleComboUsed = false; turnStartAlive = enemies.Count(e => e.Alive); HpLostThisTurn = 0; EffectVersion++; ResetRelicTurn();
            {
                Turn++; manaMaximum = Saturate((long)manaMaximum + RelicPower("eternal_fount")); Mana = ManaMaximum; reactions.Clear(); var relicDraw = StartTurnRelics();
                var poison = Invincible ? 0 : Math.Min(Hp, HeroStatuses[StatusKind.Poison]); Hp -= poison; OnHpLost(poison, true); Revive(); HeroStatuses.Tick(StatusKind.Poison);
                var drawCount = Math.Max(0, handSize - HeroStatuses[StatusKind.Entangled]); HeroStatuses.Remove(StatusKind.Entangled);
                { TriggerPowers(); var extra = pendingDraws; pendingDraws = 0; DrawCards(drawCount + extra + relicDraw); AfterHandDrawn(); }
            }
            EnemyPatterns.ExpansionReact(enemies); HandleEnemyDeaths();   // deaths and broken Carga at the start of the hero's turn
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
            EnemyPatterns.BeforeAct(enemy, enemies);
            var intent = enemy.Intent;
            var x = intent.Extras;
            var lost = 0L;
            var action = new EnemyAction { Index = enemies.IndexOf(enemy), MoveId = intent.Id };
            enemyLog.Add(action);
            if (intent.Kind == IntentKind.Stun) action.Stunned = true;
            else ActOut(enemy, intent, x, action, ref lost);
            enemy.Statuses.Tick(StatusKind.Weak); enemy.Statuses.Tick(StatusKind.Vulnerable); enemy.Statuses.Tick(StatusKind.Frozen);
            if (enemy.Definition.Ai == "seta" && enemy.Statuses[StatusKind.Fuse] > 1) enemy.Statuses.Tick(StatusKind.Fuse);
            enemy.LoseHp(enemy.Statuses[StatusKind.Fire]);
        }
        private void ActOut(EnemyState enemy, EnemyIntent intent, IntentExtras x, EnemyAction action, ref long lost)
        {
            if (x.Any) BeforeHitsExpansion(enemy, x, action);
            if (intent.Kind == IntentKind.Attack)
                for (var hit = 0; hit < Math.Max(1, intent.Hits) && !Defeat && enemy.Alive; hit++) { var l = EnemyHit(enemy, enemy.HitDamage(HeroStatuses)); lost += l; action.Hits.Add(l); }
            if (intent.Kind == IntentKind.Block) enemy.GainBlock(intent.Value);
            if (intent.Id == "" && intent.Kind == IntentKind.Debuff) if (!BlocksDebuff(StatusKind.Weak)) { HeroStatuses.Add(StatusKind.Weak, 2); action.Debuffs.Add(new StatusAmount(StatusKind.Weak, 2)); }
            if (intent.Lifesteal) action.Healed += enemy.Heal((int)Math.Min(int.MaxValue, lost));
            enemy.GainBlock(intent.Block);
            foreach (var buff in intent.Buffs) enemy.Statuses.Add(buff.Kind, buff.Amount);
            foreach (var debuff in intent.Debuffs) if (!BlocksDebuff(debuff.Kind)) { HeroStatuses.Add(debuff.Kind, debuff.Amount); action.Debuffs.Add(debuff); }
            foreach (var spawn in intent.Cards) { SpawnStatusCards(spawn); action.Cards.Add(spawn); }
            foreach (var ally in enemies)
            {
                if (!ally.Alive) continue;
                if (ally != enemy)
                {
                    ally.GainBlock(intent.AllyBlock);
                    foreach (var buff in intent.AllyBuffs) ally.Statuses.Add(buff.Kind, buff.Amount);
                }
                var healed = ally.Heal(intent.HealAllies);
                if (ally == enemy) action.Healed += healed;
            }
            if (x.Any) AfterEffectsExpansion(enemy, x, action);
            if (intent.SelfDestruct) { enemy.LoseHp(enemy.Hp); action.Exploded = true; }
            if (x.SplitInto != null) Split(enemy, x, action);
        }
        private int EnemyHit(EnemyState enemy, int damage)
        {
            if (decoys > 0)
            {
                var others = enemies.Where(e => e != enemy && e.Alive).ToArray();
                if (others.Length > 0) { decoys--; others[random.Next(others.Length)].Damage(damage); return 0; }
            }
            damage = RelicIncomingDamage(damage, enemy);
            var fullyBlocked = damage > 0 && Block >= damage;
            var lost = HitHero(damage); if (fullyBlocked && counterDamage > 0) enemy.Damage(counterDamage);
            return lost;
        }
        private int HitHero(int damage)
        {
            var absorbed = Math.Min(Block, damage); Block -= absorbed;
            var lost = Invincible ? 0 : Math.Min(Hp, damage - absorbed); Hp -= lost; OnHpLost(lost, false); if (lost > 0) hitLastEnemyTurn = true; Revive(); return lost;
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
            if (!lensUsed && HasRelic("prismatic_lens")) { lensUsed = true; repeats++; }
            for (var repeat = 0; repeat < repeats; repeat++) ResolveReaction(enemy, previous, incoming, total, spread);
            return true;
        }
        private void ResolveReaction(EnemyState enemy, StatusKind previous, StatusKind incoming, int total, bool spread)
        {
            var index = enemies.IndexOf(enemy);
            var a = Math.Min((int)previous, (int)incoming); var b = Math.Max((int)previous, (int)incoming);
            ReactionKind reaction;
            if (a == (int)StatusKind.Fire && b == (int)StatusKind.Water) { reaction = ReactionKind.Steam; foreach (var other in enemies) if (other == enemy || archmage && other.Alive) { other.Damage((BigInteger)2 * total); other.Statuses.Add(StatusKind.Weak, RelicPower("cauldron")); } }
            else if (a == (int)StatusKind.Fire && b == (int)StatusKind.Earth) { reaction = ReactionKind.Magma; foreach (var other in enemies) if (other == enemy || archmage && other.Alive) { other.ClearBlock(); other.Statuses.Add(StatusKind.Vulnerable, 2); other.Damage(RelicPower("volcano_ember", 5)); } }
            else if (a == (int)StatusKind.Fire && b == (int)StatusKind.Air)
            {
                reaction = ReactionKind.Wildfire; enemy.Statuses.Add(StatusKind.Fire, total);
                if (spread) for (var i = 0; i < enemies.Count; i++) if (i != index) ApplyElement(i, ElementKind.Fire, Saturate((long)total + RelicPower("lava_stone")), false);
            }
            else if (a == (int)StatusKind.Water && b == (int)StatusKind.Earth) { reaction = ReactionKind.Mud; Block = (int)Math.Min(int.MaxValue, Block + 3L * total); }
            else if (a == (int)StatusKind.Water && b == (int)StatusKind.Air) { reaction = ReactionKind.Freeze; foreach (var other in enemies) if ((other == enemy || archmage && other.Alive) && other.Statuses[StatusKind.Frozen] == 0) other.Statuses.Add(StatusKind.Frozen, 1 + (other.Definition.Boss || other.Definition.Elite ? 0 : RelicPower("frost_crystal"))); }
            else { reaction = ReactionKind.Sandstorm; foreach (var other in enemies) if (other.Alive) other.Statuses.Add(StatusKind.Weak, 1 + RelicPower("witch_broom")); }
            HitRandom(RelicPower("storm_orb", 3));
            AddBlock(reactionBlock); pendingDraws = Saturate((long)pendingDraws + reactionDraw);
            reactions.Add(new ReactionEvent(enemy.Id, reaction, total, (ElementKind)((int)previous - (int)StatusKind.Fire), (ElementKind)((int)incoming - (int)StatusKind.Fire))); EnemyPatterns.ReactToDeaths(enemies);
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
                if (spawn.Id != "status_herida" && HasRelic("broken_chain")) fading.Add(card);   // Cadena Rota: enemy junk is ethereal
                if (spawn.Pile == "draw") draw.Insert(random.Next(draw.Count + 1), card);
                else if (spawn.Pile == "hand" && hand.Count < 10) hand.Add(card);
                else discard.Add(card);
            }
        }
        private void OnDraw(CardDefinition card) { if (card.Id == "status_moho") Mana = Math.Max(0, Mana - 1); }
        private void DrawCards(int count)
        {
            var previousPending = pendingDraws;
            for (var i = 0; i < Math.Max(0, count) && hand.Count < 10; i++)
            {
                if (draw.Count == 0) { if (discard.Count == 0) break; draw.AddRange(discard); discard.Clear(); Shuffle(); pendingDraws += OnReshuffle(); }
                var index = draw.Count - 1; var card = draw[index]; draw.RemoveAt(index);
                if (card.PlayOnDraw) { for (var cast = 0; cast < card.CastCount; cast++) DaggerHit(); }
                else { hand.Add(card); drawnToHand++; OnDraw(card); }
            }
            var extra = pendingDraws - previousPending; if (extra > 0) { pendingDraws = previousPending; DrawCards(extra); }
        }
        private void Shuffle()
        {
            for (var i = draw.Count - 1; i > 0; i--) { var other = random.Next(i + 1); var card = draw[i]; draw[i] = draw[other]; draw[other] = card; }
        }
    }
}
