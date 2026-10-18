using System;
using LastWish.Domain;
using NUnit.Framework;
namespace LastWish.Tests
{
    public sealed class PowerTests
    {
        private static CardDefinition Card(string id, CardKind kind, params CardCommand[] commands) =>
            new CardDefinition(id, id, 0, 0, 0, effects: new[] { new EffectLayer(EffectCondition.Always, 0, commands: commands) }, kind: kind);
        private static CombatState Battle(params CardDefinition[] cards) => new CombatState(
            HeroDefinition.OriginalFor(HeroKind.Warrior), cards, 7, 100, enemyHp: 10000, enemyDamage: 0);
        private static int Index(CombatState state, string id)
        { for (var i = 0; i < state.Hand.Count; i++) if (state.Hand[i].Id == id) return i; throw new Exception(id); }
        [Test] public void OwnDamageCannotKillAndOverflowHealingBecomesBlock()
        {
            var state = Battle(Card("lose", CardKind.Skill, new CardCommand(CommandKind.LoseHp, 1000)),
                Card("heal", CardKind.Skill, new CardCommand(CommandKind.Heal, 110)));
            state.PlayCard(Index(state, "lose")); Assert.That(state.Hp, Is.EqualTo(1));
            state.PlayCard(Index(state, "heal")); Assert.That(state.Hp, Is.EqualTo(100)); Assert.That(state.Block, Is.EqualTo(11));
        }
        [Test] public void PowersStayOutOfDiscardAndTriggerOnSubsequentTurns()
        {
            var power = new CardDefinition("power", "power", 0, 0, 0, effects: new[] {
                new EffectLayer(EffectCondition.Always, 0, turnCommands: new[] { new CardCommand(CommandKind.Heal, 3) }) }, kind: CardKind.Power);
            var state = Battle(power, Card("guard", CardKind.Skill));
            state.PlayCard(Index(state, "power")); Assert.That(state.ActivePowers.Count, Is.EqualTo(1));
            Assert.That(state.DiscardCount, Is.Zero); state.EndTurn(); Assert.That(state.Block, Is.EqualTo(3));
        }
        [Test] public void HealthLossPowersReactPerHitAndSelfDamageCapApplies()
        {
            var state = Battle(Card("powers", CardKind.Power, new CardCommand(CommandKind.SecondSkin),
                new CardCommand(CommandKind.Berserker), new CardCommand(CommandKind.IronWill)),
                Card("lose", CardKind.Skill, new CardCommand(CommandKind.LoseHp, 8), new CardCommand(CommandKind.LoseHp, 4)));
            state.PlayCard(Index(state, "powers")); state.PlayCard(Index(state, "lose"));
            Assert.That(state.Hp, Is.EqualTo(98)); Assert.That(state.Strength, Is.EqualTo(2));
            Assert.That(state.Block, Is.EqualTo(6)); Assert.That(state.HpLostThisTurn, Is.EqualTo(2));
        }
        [Test] public void HiddenDaggersPlayOnDrawWithoutTakingAHandSlot()
        {
            var state = Battle(Card("hide", CardKind.Skill, new CardCommand(CommandKind.HideDaggers, 2)));
            state.PlayCard(0); Assert.That(state.DrawCount, Is.EqualTo(2));
            var hp = state.EnemyHp; state.EndTurn();
            Assert.That(state.EnemyHp, Is.EqualTo(hp - 8));
            Assert.That(state.Hand.Count, Is.EqualTo(1)); Assert.That(state.ExhaustedCount, Is.Zero);
        }
        [Test] public void RandomCardOutcomesRespectBoundsOverManySeeds()
        {
            for (var seed = 0; seed < 100; seed++)
            {
                var card = new CardDefinition("dice", "dice", 0, 0, 0, effects: new[] {
                    new EffectLayer(EffectCondition.Always, 0, commands: new[] { new CardCommand(CommandKind.RollDice) }) });
                var state = new CombatState(HeroDefinition.OriginalFor(HeroKind.Rogue), new[] { card }, seed);
                typeof(CombatState).GetProperty("Mana").SetValue(state, 0); state.PlayCard(0);
                Assert.That(state.Mana, Is.InRange(0, 3));
            }
        }
    }
}
