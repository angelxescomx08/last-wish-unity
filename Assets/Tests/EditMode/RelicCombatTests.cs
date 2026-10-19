using System.Linq;
using LastWish.Domain;
using NUnit.Framework;
namespace LastWish.Tests
{
    public sealed class RelicCombatTests
    {
        private static OwnedRelic Owned(string tag) => new OwnedRelic(RelicCatalog.All.Single(r=>r.Tag==tag));
        private static CombatState State(OwnedRelic[] relics, params CardDefinition[] cards)
        {
            var enemy=new EnemyState("e",new EnemyDefinition("fixture","Objetivo","wraith",10000),1,10000);
            typeof(EnemyState).GetProperty("Intent").SetValue(enemy,new EnemyIntent(IntentKind.Attack,0));
            return new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior),cards,42,100,new[]{enemy},relics:relics);
        }
        [Test] public void StartingManaAndExtraDrawApplyExactlyOnce()
        {
            var deck=Enumerable.Range(0,10).Select(i=>new CardDefinition("card"+i,"Carta",0,0,0)).ToArray();
            var state=State(new[]{Owned("combat_amulet"),Owned("energy_stone")},deck);
            Assert.That(state.ManaMaximum,Is.EqualTo(3));Assert.That(state.Mana,Is.EqualTo(3));Assert.That(state.Hand.Count,Is.EqualTo(6));
            state.EndTurn();Assert.That(state.ManaMaximum,Is.EqualTo(3));Assert.That(state.Hand.Count,Is.EqualTo(6));
        }
        [Test] public void FireOrbAddsToTheMainHitAndNotToExtraStrikes()
        {
            var card=new CardDefinition("hit","hit",0,5,0,effects:new[]{new EffectLayer(EffectCondition.Always,5,commands:new[]{new CardCommand(CommandKind.Strike,5)})});
            var state=State(new[]{Owned("fire_orb")},card);state.PlayCard(0);Assert.That(state.EnemyHp,Is.EqualTo(9984));
        }
        [Test] public void SpectralShieldIsSpentBeforeAnkhAcrossMultiHitAttacks()
        {
            var shield=Owned("spectral_shield");var ankh=Owned("ankh");
            var state=State(new[]{ankh,shield},new CardDefinition("a","Carta",0,0,0));
            typeof(EnemyState).GetProperty("Intent").SetValue(state.Enemies[0],new EnemyIntent(IntentKind.Attack,500,2));
            state.EndTurn();Assert.That(state.Hp,Is.EqualTo(100));Assert.That(shield.Active,Is.False);Assert.That(ankh.Active,Is.False);
        }
        [Test] public void EternalFountIncreasesMaximumAtEachNewTurn()
        {
            var state=State(new[]{Owned("eternal_fount")},new CardDefinition("a","Carta",0,0,0));
            Assert.That(state.ManaMaximum,Is.EqualTo(2));state.EndTurn();Assert.That(state.ManaMaximum,Is.EqualTo(3));
            state.EndTurn();Assert.That(state.ManaMaximum,Is.EqualTo(4));
        }
        [Test] public void PanaceaStopsEnemyDebuffs()
        {
            var state=State(new[]{Owned("panacea")},new CardDefinition("a","Carta",0,0,0));
            typeof(EnemyState).GetProperty("Intent").SetValue(state.Enemies[0],new EnemyIntent(IntentKind.Debuff,debuffs:new[]{new StatusAmount(StatusKind.Poison,5)}));
            state.EndTurn();Assert.That(state.HeroStatuses[StatusKind.Poison],Is.Zero);Assert.That(state.Hp,Is.EqualTo(100));
        }
    }
}
