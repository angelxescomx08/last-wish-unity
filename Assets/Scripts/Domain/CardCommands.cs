using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LastWish.Domain
{
    public enum CommandKind
    {
        LoseHp, Heal, Strength, Mana, Lifesteal, Strike, Area, HealArea, ShieldBash,
        Retaliation, LivingArmor, DoubleBlock, Transfusion, Execution, BoilingBlood,
        InnerFortress, SecondSkin, SteelSkin, Berserker, IronWill, RetainBlock,
        FanFlames, Combustion, CombustionAll, FireHeart, RawArea, RawHit, Avalanche,
        GraniteSkin, HurricaneEye, Catalyst, Archmage, Convergence, WeakenAll,
        HandDamage, ManaBlock, DiscardBlock, Poison, DrawPileArea, ManaPerEnemy,
        DiscardAttackArea, CheapHandHit, PoisonAll, RefundKill, NextDiscount,
        FirstDiscount, NextDamage, ComboAlways, Dexterity, DrawTypes, TurnBlades,
        BreakGuard, Flurry, EmptyPockets, SleightOfHand, Mark, DiscardRandom, RandomHit, TwoRandomHits,
        HideDaggers, LuckyCut, Decoy, Counter, Pickpocket, PoisonBlade, RollDice, Scavenge,
        DaggerMaster, TwinShadow, BoldFortune, NothingToLose, PerfectChain, Assassin, ThousandCuts
    }
    public readonly struct CardCommand
    {
        public CommandKind Kind { get; }
        public int Amount { get; }
        public int Times { get; }
        public CardCommand(CommandKind kind, int amount = 0, int times = 1)
        {
            if (!Enum.IsDefined(typeof(CommandKind), kind) || amount < 0 || times < 1 || times > 100)
                throw new ArgumentOutOfRangeException();
            Kind = kind; Amount = amount; Times = times;
        }
    }
    public sealed partial class CombatState
    {
        private readonly List<CardDefinition> powers = new List<CardDefinition>();
        public int ActivePowerCount => powers.Count;
        public IReadOnlyList<CardDefinition> ActivePowers => powers.AsReadOnly();
        public int LastHit { get; private set; }
        private int lastAreaHits, blockOnHeal, blockOnLoss, strengthOnLoss, endTurnBlock;
        private int pendingDraws, bonusDexterity, nextDiscount, firstDiscount, nextDamage;
        private int reactionBlock, reactionDraw, catalyst;
        public int GoldEarned { get; private set; }
        private int decoys, counterDamage, pickpocketGold, poisonAttacks, discardDamage, daggersOnDraw, doubleCombo, echoFifth, rummages, turnStartAlive;
        private bool doubleComboUsed, executeWounded;
        private bool selfDamageCap, retainBlock, comboAlways, archmage;
        private static int Saturate(long value) => (int)Math.Min(int.MaxValue, Math.Max(0, value));
        private void AddBlock(long amount) => Block = Saturate(Block + amount);
        private void GainEffectBlock(long amount) => AddBlock(HeroStatuses[StatusKind.Frail] > 0 ? amount * 3 / 4 : amount);
        private void GainMana(long amount) => Mana = (int)Math.Min(Hero.MaxMana, Mana + amount);
        private void GainStrength(int amount) => Strength = Saturate((long)Strength + amount);
        private void OnHpLost(int lost, bool ownTurn)
        { if (lost <= 0) return; if (ownTurn) HpLostThisTurn = Saturate((long)HpLostThisTurn + lost); AddBlock(blockOnLoss); GainStrength(strengthOnLoss); }
        private void LoseOwnHp(int amount)
        {
            var lost = Math.Max(0, Math.Min(selfDamageCap ? Math.Min(1, amount) : amount, Hp - 1));
            Hp -= lost; OnHpLost(lost, true);
        }
        private void HealHero(long amount)
        {
            if (amount <= 0) return;
            var healed = Math.Min(amount, Hero.MaxHp - Hp); Hp += (int)healed;
            AddBlock(amount - healed + blockOnHeal);
        }
        private void HeroHit(int target, long amount)
        {
            if (amount <= 0 || !enemies[target].Alive) return;
            BigInteger damage = (BigInteger)amount + Strength;
            if (HeroStatuses[StatusKind.Weak] > 0) damage = damage * 3 / 4;
            if (enemies[target].Statuses[StatusKind.Vulnerable] > 0) damage = damage * 3 / 2;
            enemies[target].Damage(damage);
        }
        private void RawArea(long amount) { foreach (var enemy in enemies) if (enemy.Alive) enemy.Damage(amount); }
        public int CardCost(CardDefinition card) => Math.Max(0, card.Cost - nextDiscount - (cardsPlayed == 0 ? firstDiscount : 0));
        private void TriggerPowers()
        {
            foreach (var power in powers)
                if (power.Effects != null) foreach (var layer in power.Effects)
                { ExecuteCommands(layer.TurnCommands, SelectedEnemy); var count = pendingDraws; pendingDraws = 0; DrawCards(count); }
        }
        private void ExecuteCommands(IReadOnlyList<CardCommand> commands, int target)
        {
            var enemy = enemies[target];
            foreach (var command in commands)
            {
                var n = command.Amount;
                switch (command.Kind)
                {
                    case CommandKind.LoseHp: LoseOwnHp(n); break;
                    case CommandKind.Heal: HealHero(n); break;
                    case CommandKind.Strength: GainStrength(n); break;
                    case CommandKind.Mana: GainMana(n); break;
                    case CommandKind.Lifesteal: HealHero(LastHit / Math.Max(1, n)); break;
                    case CommandKind.Strike: for (var i = 0; i < command.Times; i++) HeroHit(target, n); break;
                    case CommandKind.Area:
                        lastAreaHits = 0; for (var i = 0; i < enemies.Count; i++) if (enemies[i].Alive) { HeroHit(i, n); lastAreaHits++; } break;
                    case CommandKind.HealArea: HealHero((long)n * lastAreaHits); break;
                    case CommandKind.ShieldBash: HeroHit(target, Block); break;
                    case CommandKind.Retaliation: HeroHit(target, 2L * HpLostThisTurn); break;
                    case CommandKind.LivingArmor: GainEffectBlock(Math.Min(30, (Hero.MaxHp - Hp) / 2)); break;
                    case CommandKind.DoubleBlock: AddBlock(Block); break;
                    case CommandKind.Transfusion: var previous = Block; Block = 0; HealHero(previous); break;
                    case CommandKind.Execution: if (LastHit > 0 && !enemy.Alive) HealHero(10); break;
                    case CommandKind.BoilingBlood: LoseOwnHp(2); GainStrength(1); break;
                    case CommandKind.InnerFortress: blockOnHeal = Saturate((long)blockOnHeal + 2); break;
                    case CommandKind.SecondSkin: blockOnLoss = Saturate((long)blockOnLoss + 3); break;
                    case CommandKind.SteelSkin: endTurnBlock = Saturate((long)endTurnBlock + 6); break;
                    case CommandKind.Berserker: strengthOnLoss = Saturate((long)strengthOnLoss + 1); break;
                    case CommandKind.IronWill: selfDamageCap = true; break;
                    case CommandKind.RetainBlock: retainBlock = true; break;
                    case CommandKind.FanFlames: if (enemy.Alive) enemy.Statuses.Add(StatusKind.Fire, enemy.Statuses[StatusKind.Fire]); break;
                    case CommandKind.Combustion: Combust(enemy); break;
                    case CommandKind.CombustionAll: foreach (var other in enemies) Combust(other); break;
                    case CommandKind.FireHeart: for (var i = 0; i < enemies.Count; i++) ApplyElement(i, ElementKind.Fire, 1); break;
                    case CommandKind.RawArea: RawArea(n); break;
                    case CommandKind.RawHit: enemy.Damage(n); break;
                    case CommandKind.Avalanche: enemy.Damage((BigInteger)4 * enemy.Statuses[StatusKind.Earth]); break;
                    case CommandKind.GraniteSkin: reactionBlock = Saturate((long)reactionBlock + 4); break;
                    case CommandKind.HurricaneEye: reactionDraw = Saturate((long)reactionDraw + 1); break;
                    case CommandKind.Catalyst: catalyst = Saturate((long)catalyst + 1); break;
                    case CommandKind.Archmage: archmage = true; break;
                    case CommandKind.Convergence: for (var i = 0; i < 4; i++) ApplyElement(target, (ElementKind)i, 2); break;
                    case CommandKind.WeakenAll: foreach (var other in enemies) if (other.Alive) other.Statuses.Add(StatusKind.Weak, 1); break;
                    case CommandKind.HandDamage: enemy.Damage(hand.Count * 2L); break;
                    case CommandKind.ManaBlock: AddBlock(Mana * 5L); break;
                    case CommandKind.DiscardBlock: AddBlock(discard.Count * 2L); break;
                    case CommandKind.Poison: if (enemy.Alive) enemy.Statuses.Add(StatusKind.Poison, 3); break;
                    case CommandKind.DrawPileArea: RawArea(draw.Count); break;
                    case CommandKind.ManaPerEnemy: GainMana(enemies.Count(e => e.Alive)); break;
                    case CommandKind.DiscardAttackArea: RawArea(discard.Count(c => c.IsAttack) * 5L); break;
                    case CommandKind.CheapHandHit: if (hand.All(c => c.Cost <= 1)) enemy.Damage(40); break;
                    case CommandKind.PoisonAll: foreach (var other in enemies) if (other.Alive) other.Statuses.Add(StatusKind.Poison, 5); break;
                    case CommandKind.RefundKill: if (!enemy.Alive) GainMana(1); break;
                    case CommandKind.NextDiscount: nextDiscount = Saturate((long)nextDiscount + 1); break;
                    case CommandKind.FirstDiscount: firstDiscount = Saturate((long)firstDiscount + 1); break;
                    case CommandKind.NextDamage: nextDamage = Saturate((long)nextDamage + 6); break;
                    case CommandKind.ComboAlways: comboAlways = true; break;
                    case CommandKind.Dexterity: bonusDexterity = Saturate((long)bonusDexterity + 1); break;
                    case CommandKind.DrawTypes:
                        foreach (var kind in new[] { CardKind.Power, CardKind.Attack, CardKind.Skill })
                            for (var i = draw.Count - 1; i >= 0 && hand.Count < 10; i--) if (draw[i].Kind == kind) { hand.Add(draw[i]); draw.RemoveAt(i); break; } break;
                    case CommandKind.TurnBlades: RawArea(singularDeck ? 5 : 3); break;
                    case CommandKind.BreakGuard: if (enemy.Alive) enemy.ClearBlock(); break;
                    case CommandKind.Flurry: enemy.Damage(2L * (cardsPlayed + 1)); break;
                    case CommandKind.EmptyPockets:
                        pendingDraws += hand.Count; while (hand.Count > 0) DiscardAt(hand.Count - 1); break;
                    case CommandKind.SleightOfHand:
                        for (var i = discard.Count - 1; i >= 0 && hand.Count < 10; i--) if (discard[i].Id != "r_juego_de_manos") { hand.Add(discard[i]); discard.RemoveAt(i); break; } break;
                    case CommandKind.Mark: if (enemy.Alive) enemy.Statuses.Add(StatusKind.Marked, 3); break;
                    case CommandKind.DiscardRandom: DiscardRandom(); break;
                    case CommandKind.RandomHit: HitRandom(n); break;
                    case CommandKind.TwoRandomHits:
                        var candidates = enemies.Where(e => e.Alive).ToList();
                        for (var i = 0; i < 2 && candidates.Count > 0; i++) { var chosen = random.Next(candidates.Count); candidates[chosen].Damage(10); candidates.RemoveAt(chosen); } break;
                    case CommandKind.HideDaggers: HideDaggers(n); break;
                    case CommandKind.LuckyCut: if (enemy.Alive && random.NextDouble() < Math.Min(.75, .10 + .025 * Hero.Luck)) enemy.Damage(6); break;
                    case CommandKind.Decoy: decoys++; break;
                    case CommandKind.Counter: counterDamage = Saturate((long)counterDamage + 3); break;
                    case CommandKind.Pickpocket: pickpocketGold = Saturate((long)pickpocketGold + 10); break;
                    case CommandKind.PoisonBlade: poisonAttacks = Saturate((long)poisonAttacks + 3); break;
                    case CommandKind.RollDice:
                        var rolls = Math.Min(4, 1 + Hero.Luck / 20 + (random.NextDouble() < Hero.Luck % 20 / 20.0 ? 1 : 0));
                        var best = 0; for (var i = 0; i < rolls; i++) best = Math.Max(best, random.Next(4)); GainMana(best); break;
                    case CommandKind.Scavenge: discardDamage = Saturate((long)discardDamage + 3); break;
                    case CommandKind.DaggerMaster: daggersOnDraw++; break;
                    case CommandKind.TwinShadow: doubleCombo++; break;
                    case CommandKind.BoldFortune: if (random.NextDouble() < .5) pendingDraws++; else AddBlock(4); break;
                    case CommandKind.NothingToLose: rummages++; break;
                    case CommandKind.PerfectChain: echoFifth++; break;
                    case CommandKind.Assassin: executeWounded = true; break;
                    case CommandKind.ThousandCuts: for (var i = 0; i < discard.Count; i++) HitRandom(3); break;
                    default: throw new InvalidOperationException("Unhandled card command");
                }
            }
        }
        private void HitRandom(int amount)
        { var alive = enemies.Where(e => e.Alive).ToArray(); if (alive.Length > 0) alive[random.Next(alive.Length)].Damage(amount); }
        private void HideDaggers(int count)
        { for (var i = 0; i < count; i++) draw.Insert(random.Next(draw.Count + 1), new CardDefinition("t_daga_oculta", "Daga Oculta", 0, 0, 0)); }
        private CardDefinition DiscardAt(int index)
        {
            var card = hand[index]; hand.RemoveAt(index); discard.Add(card); discardsThisTurn++;
            if (discardDamage > 0) HitRandom(discardDamage); EffectVersion++; return card;
        }
        private void SettlePickpocket()
        { if (pickpocketGold > 0 && enemies.Count(e => e.Alive) < turnStartAlive) { GoldEarned = Saturate((long)GoldEarned + pickpocketGold); pickpocketGold = 0; } }
        private void AfterHandDrawn()
        { for (var i = 0; i < rummages; i++) if (DiscardRandom() != null) DrawCards(2); }
        private void RepeatCombo(CardDefinition card, int target)
        {
            BigInteger damage = 0; long block = 0, mana = 0; var draws = 0;
            foreach (var layer in card.Effects) if (layer.Condition == EffectCondition.Combo)
            { damage += layer.Damage; block += layer.Block; mana += layer.Mana; draws += layer.Draw; }
            enemies[target].Damage(damage); GainEffectBlock(block); GainMana(mana);
            foreach (var layer in card.Effects) if (layer.Condition == EffectCondition.Combo)
            {
                if (layer.Element.HasValue && layer.Stacks > 0)
                { if (layer.AllEnemies) for (var i = 0; i < enemies.Count; i++) ApplyElement(i, layer.Element.Value, layer.Stacks); else ApplyElement(target, layer.Element.Value, layer.Stacks); }
            }
            foreach (var layer in card.Effects) if (layer.Condition == EffectCondition.Combo) ExecuteCommands(layer.Commands, target);
            var total = Saturate((long)draws + pendingDraws); pendingDraws = 0; DrawCards(total);
        }
        private static void Combust(EnemyState enemy)
        { if (!enemy.Alive) return; var fire = enemy.Statuses[StatusKind.Fire]; if (fire <= 0) return; enemy.Statuses.Remove(StatusKind.Fire); enemy.Damage(3L * fire); }
    }
}
