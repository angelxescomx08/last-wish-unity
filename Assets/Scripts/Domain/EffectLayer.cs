using System;
using System.Numerics;

namespace LastWish.Domain
{
    public enum CardKind { Auto, Attack, Skill, Power, Status }
    public enum EffectCondition { Always, Combo, Singular, Void, Spoil, Reaction, Fury, Endure }
    public sealed class EffectLayer
    {
        public EffectCondition Condition { get; }
        public BigInteger Damage { get; }
        public int Block { get; }
        public int Draw { get; }
        public int Mana { get; }
        public ElementKind? Element { get; }
        public int Stacks { get; }
        public bool AllEnemies { get; }
        public System.Collections.ObjectModel.ReadOnlyCollection<CardCommand> Commands { get; }
        public System.Collections.ObjectModel.ReadOnlyCollection<CardCommand> TurnCommands { get; }
        public string Text { get; }
        public EffectLayer(EffectCondition condition, BigInteger damage, int block = 0, int draw = 0, int mana = 0,
            ElementKind? element = null, int stacks = 0, bool allEnemies = false, CardCommand[] commands = null, CardCommand[] turnCommands = null, string text = "")
        {
            if (!Enum.IsDefined(typeof(EffectCondition), condition) || damage < 0 || block < 0 || draw < 0 || mana < 0 || stacks < 0)
                throw new ArgumentOutOfRangeException();
            if (element.HasValue && !Enum.IsDefined(typeof(ElementKind), element.Value)) throw new ArgumentOutOfRangeException(nameof(element));
            Commands = Array.AsReadOnly(commands == null ? Array.Empty<CardCommand>() : (CardCommand[])commands.Clone());
            TurnCommands = Array.AsReadOnly(turnCommands == null ? Array.Empty<CardCommand>() : (CardCommand[])turnCommands.Clone());
            Text = text ?? "";
            Condition = condition; Damage = damage; Block = block; Draw = draw; Mana = mana; Element = element; Stacks = stacks; AllEnemies = allEnemies;
        }
        public EffectLayer UpgradeBase(int extraDraw) => new EffectLayer(Condition,
            Damage > 0 ? Damage + BigInteger.Max(3, Damage / 3) : Damage,
            Block > 0 ? (int)Math.Min(int.MaxValue, (long)Block + Math.Max(3, Block / 3)) : 0,
            (int)Math.Min(int.MaxValue, (long)Draw + extraDraw), Mana, Element, Stacks, AllEnemies, System.Linq.Enumerable.ToArray(Commands), System.Linq.Enumerable.ToArray(TurnCommands), Text);
    }
    public sealed partial class CombatState
    {
        private readonly bool singularDeck;
        private int discardsThisTurn;
        public int HpLostThisTurn { get; private set; }
        public int EffectVersion { get; private set; }
        private uint ActiveConditions(CardDefinition card)
        {
            var mask = 1u;
            if (cardsPlayed > 0 || comboAlways) mask |= 1u << (int)EffectCondition.Combo;
            if (singularDeck) mask |= 1u << (int)EffectCondition.Singular;
            if (Mana == CardCost(card)) mask |= 1u << (int)EffectCondition.Void;
            if (discardsThisTurn > 0) mask |= 1u << (int)EffectCondition.Spoil;
            if (HpLostThisTurn > 0) mask |= 1u << (int)EffectCondition.Fury;
            if (Block > 0) mask |= 1u << (int)EffectCondition.Endure;
            if (card.Effects != null)
                foreach (var effect in card.Effects)
                {
                    if (!effect.Element.HasValue || effect.Stacks == 0) continue;
                    for (var i = 0; i < enemies.Count; i++)
                    {
                        if (!enemies[i].Alive || (!effect.AllEnemies && i != SelectedEnemy)) continue;
                        for (var element = 0; element < 4; element++)
                            if (element != (int)effect.Element.Value && enemies[i].Statuses[(StatusKind)((int)StatusKind.Fire + element)] > 0)
                                mask |= 1u << (int)EffectCondition.Reaction;
                    }
                }
            return mask;
        }
        private static bool Active(EffectLayer effect, uint mask) => (mask & 1u << (int)effect.Condition) != 0;
        private void ResolveNumeric(CardDefinition card, int target, uint mask, int damageBonus = 0)
        {
            BigInteger damage = 0; long block = 0, mana = 0, draws = 0;
            foreach (var effect in card.Effects)
            {
                if (!Active(effect, mask)) continue;
                damage += effect.Damage; block = Math.Min(int.MaxValue, block + effect.Block);
                mana = Math.Min(int.MaxValue, mana + effect.Mana); draws = Math.Min(10, draws + effect.Draw);
            }
            DamageFromCard(target, damage, damageBonus);
            GainCardBlock(block);
            Mana = (int)Math.Min(Hero.MaxMana, Mana + mana);
            foreach (var effect in card.Effects)
            {
                if (!Active(effect, mask) || !effect.Element.HasValue || effect.Stacks == 0) continue;
                if (effect.AllEnemies) for (var i = 0; i < enemies.Count; i++) ApplyElement(i, effect.Element.Value, effect.Stacks);
                else ApplyElement(target, effect.Element.Value, effect.Stacks);
            }
            foreach (var effect in card.Effects) if (Active(effect, mask)) ExecuteCommands(effect.Commands, target);
            var totalDraws = (int)Math.Min(10, draws + pendingDraws); pendingDraws = 0; DrawCards(totalDraws);
        }
        private void DamageFromCard(int target, BigInteger damage, int damageBonus = 0)
        {
            LastHit = 0;
            if (damage <= 0 || !enemies[target].Alive) return;
            damage += Hero.AttackBonus + (BigInteger)Strength + damageBonus;
            if (HeroStatuses[StatusKind.Weak] > 0) damage = damage * 3 / 4;
            if (enemies[target].Statuses[StatusKind.Vulnerable] > 0) damage = damage * 3 / 2;
            LastHit = enemies[target].Damage(damage);
        }
        private void GainCardBlock(long amount)
        {
            if (amount <= 0) return;
            amount += Hero.Dexterity + bonusDexterity;
            if (HeroStatuses[StatusKind.Frail] > 0) amount = amount * 3 / 4;
            Block = (int)Math.Min(int.MaxValue, Block + amount);
        }
        public CardDefinition DiscardRandom()
        {
            if (hand.Count == 0) return null;
            return DiscardAt(random.Next(hand.Count));
        }
        public long PreviewBlock(CardDefinition card)
        {
            var block = (long)card.Block + (cardsPlayed > 0 ? card.ComboBlock : 0);
            if (card.Effects != null)
            {
                block = 0; var mask = ActiveConditions(card);
                foreach (var effect in card.Effects) if (Active(effect, mask)) block = Math.Min(int.MaxValue, block + effect.Block);
            }
            if (block <= 0) return 0;
            block += Hero.Dexterity + bonusDexterity;
            if (HeroStatuses[StatusKind.Frail] > 0) block = block * 3 / 4;
            return block;
        }
        public BigInteger PreviewDamage(CardDefinition card)
        {
            var mask = ActiveConditions(card); var damage = card.Damage.Value;
            if (card.Effects != null)
            { damage = 0; foreach (var effect in card.Effects) if (Active(effect, mask)) damage += effect.Damage; }
            if (damage <= 0) return 0;
            damage += Hero.AttackBonus + (BigInteger)Strength;
            if (HeroStatuses[StatusKind.Weak] > 0) damage = damage * 3 / 4;
            if (Target.Statuses[StatusKind.Vulnerable] > 0) damage = damage * 3 / 2;
            return damage + Target.Statuses[StatusKind.Earth] + Target.Statuses[StatusKind.Marked];
        }
    }
}
