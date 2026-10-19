using System.Linq;
using LastWish.Domain;
using NUnit.Framework;
namespace LastWish.Tests
{
    public sealed class ChromaTests
    {
        [Test] public void GoldenCardRepeatsItsWholeEffectAndKeepsPrintedNumbers()
        {
            var card=new CardDefinition("a","Carta",0,5,3).WithChroma(ChromaKind.Golden);
            var state=new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior),new[]{card},42,enemyHp:1000);
            state.PlayCard(0);Assert.That(state.EnemyHp,Is.EqualTo(982));Assert.That(state.Block,Is.EqualTo(14));Assert.That(card.Damage.Value.ToString(),Is.EqualTo("5"));
        }
        [Test] public void DiamondPowerTriggersItsTurnEffectThreeTimes()
        {
            var card=new CardDefinition("p","Poder",0,0,0,effects:new[]{new EffectLayer(EffectCondition.Always,0,turnCommands:new[]{new CardCommand(CommandKind.Strength,1)})},kind:CardKind.Power).WithChroma(ChromaKind.Diamond);
            var state=new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior),new[]{card},42,enemyHp:1000,enemyDamage:0);
            state.PlayCard(0);state.EndTurn();Assert.That(state.Strength,Is.EqualTo(3));
        }
        [Test] public void GoldenRelicHasTwoSeparateRevivalCharges()
        {
            var relic=new OwnedRelic(RelicCatalog.All.Single(r=>r.Tag=="spectral_shield"),ChromaKind.Golden);
            var enemy=new EnemyState("e",new EnemyDefinition("fixture","Objetivo","wraith",100),1,100);
            typeof(EnemyState).GetProperty("Intent").SetValue(enemy,new EnemyIntent(IntentKind.Attack,500,2));
            var state=new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior),new[]{new CardDefinition("a","Carta",0,0,0)},42,100,new[]{enemy},relics:new[]{relic});
            state.EndTurn();Assert.That(state.Hp,Is.EqualTo(1));Assert.That(relic.TimesTriggered,Is.EqualTo(2));Assert.That(relic.Active,Is.False);
        }
    }
}
