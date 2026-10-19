using System;
using LastWish.Domain;
using NUnit.Framework;
namespace LastWish.Tests
{
    public sealed class CardInteractionTests
    {
        private static CardDefinition Rule(string id, params CardCommand[] commands) => new CardDefinition(id,id,0,0,0,
            effects: new[] { new EffectLayer(EffectCondition.Always, 0, commands: commands) });
        private static CombatState State(params CardDefinition[] cards) => new CombatState(
            HeroDefinition.OriginalFor(HeroKind.Warrior).WithBonuses(0,8,0), cards,42, enemyHp:1000, enemyDamage:0);
        private static void Play(CombatState state, string id)
        { for(var i=0;i<state.Hand.Count;i++) if(state.Hand[i].Id==id) { Assert.That(state.PlayCard(i),Is.True,id); return; } throw new Exception(id); }
        [Test] public void FifthCardEchoReusesTheNextDamageBonusOnBothCasts()
        {
            var hit = new CardDefinition("hit","hit",0,5,0, effects:new[]{new EffectLayer(EffectCondition.Always,5)});
            var state=State(Rule("echo",new CardCommand(CommandKind.PerfectChain)),Rule("boost",new CardCommand(CommandKind.NextDamage)),Rule("a"),Rule("b"),hit);
            Play(state,"echo");Play(state,"boost");Play(state,"a");Play(state,"b");Play(state,"hit");
            Assert.That(state.EnemyHp,Is.EqualTo(970));
        }
        [Test] public void EchoedSharpeningAccumulatesBothBonusesForTheFollowingAttack()
        {
            var state=State(Rule("echo",new CardCommand(CommandKind.PerfectChain)),Rule("a"),Rule("b"),Rule("c"),Rule("boost",new CardCommand(CommandKind.NextDamage)));
            Play(state,"echo");Play(state,"a");Play(state,"b");Play(state,"c");Play(state,"boost");
            var bonus=(int)typeof(CombatState).GetField("nextDamage",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(state);
            Assert.That(bonus,Is.EqualTo(12));
        }
        [Test] public void TwinShadowRepeatsOnlyThePrintedComboLayer()
        {
            var combo = new CardDefinition("combo","combo",0,3,0,effects:new[]{new EffectLayer(EffectCondition.Always,3),new EffectLayer(EffectCondition.Combo,5)});
            var state=State(Rule("twin",new CardCommand(CommandKind.TwinShadow)),combo);
            Play(state,"twin");Play(state,"combo");Assert.That(state.EnemyHp,Is.EqualTo(983));
        }
        [Test] public void PoisonChargesApplyAfterAnAttackAndExecutionPaysPendingGold()
        {
            var state=State(Rule("powers",new CardCommand(CommandKind.PoisonBlade),new CardCommand(CommandKind.Pickpocket),new CardCommand(CommandKind.Assassin)),
                new CardDefinition("hit","hit",0,800,0));
            Play(state,"powers");Play(state,"hit");Assert.That(state.Victory,Is.True);Assert.That(state.GoldEarned,Is.EqualTo(10));
        }
        [Test] public void DecoyRedirectsOneHitToAnotherEnemy()
        {
            var encounter=EnemyCatalog.CreateEncounter(12,1,"fixture");
            var state=new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior),new[]{Rule("decoy",new CardCommand(CommandKind.Decoy))},42,100,encounter);
            foreach(var enemy in state.Enemies) typeof(EnemyState).GetProperty("Intent").SetValue(enemy,new EnemyIntent(IntentKind.Attack,6));
            var hp=state.Enemies[1].Hp;Play(state,"decoy");state.EndTurn();
            Assert.That(state.Enemies[1].Hp,Is.EqualTo(hp-6));Assert.That(state.Hp,Is.EqualTo(94));
        }
        [Test] public void CounterDealsDamageOnlyWhenAHittingEnemyIsFullyBlocked()
        {
            var card=new CardDefinition("counter","counter",0,0,20,effects:new[]{new EffectLayer(EffectCondition.Always,0,20,commands:new[]{new CardCommand(CommandKind.Counter)})});
            var enemy=new EnemyState("e",new EnemyDefinition("fixture","Objetivo","wraith",100),1,100);
            typeof(EnemyState).GetProperty("Intent").SetValue(enemy,new EnemyIntent(IntentKind.Attack,6,2));
            var state=new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior),new[]{card},42,100,new[]{enemy});
            state.PlayCard(0);state.EndTurn();Assert.That(state.Hp,Is.EqualTo(100));Assert.That(enemy.Hp,Is.EqualTo(94));
        }
    }
}
