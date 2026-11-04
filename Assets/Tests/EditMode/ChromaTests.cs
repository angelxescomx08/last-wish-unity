using System.Collections.Generic;
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
        /// <summary>A fight against three sturdy enemies with a hand of <paramref name="card"/> and mana to spare.</summary>
        private static CombatState Fight(CardDefinition card)
        {
            var enemies = Enumerable.Range(0, 3).Select(i => new EnemyState("e" + i, new EnemyDefinition("demo", "Objetivo " + i, "wraith", 1000000), 1, 1000000)).ToList();
            return new CombatState(HeroDefinition.For(HeroKind.Rogue).WithBonuses(0, 20, 900), Enumerable.Repeat(card, 10), 7, -1, enemies);
        }

        [Test] public void GoldenAndDiamondPoisonStormPoisonEveryEnemyOncePerCast()
        {
            var storm = RunContent.Cards.Single(c => c.Id == "ep_tormenta_veneno");
            foreach (var (chroma, stacks) in new[] { (ChromaKind.None, 5), (ChromaKind.Golden, 10), (ChromaKind.Diamond, 15) })
            {
                var state = Fight(storm.WithChroma(chroma));
                var result = state.Play(0, 0);
                Assert.That(result.Success, Is.True, chroma.ToString());
                Assert.That(state.Enemies.Select(e => e.Statuses[StatusKind.Poison]), Is.All.EqualTo(stacks), chroma.ToString());
                // what the golden replay shows: every cast adds its 5
                Assert.That(result.CastEnemyStatuses.Count, Is.EqualTo(result.Casts), chroma.ToString());
                for (var k = 0; k < result.Casts; k++)
                    Assert.That(result.CastEnemyStatuses[k].Select(s => s[StatusKind.Poison]), Is.All.EqualTo(5 * (k + 1)), chroma + " cast " + (k + 1));
            }
        }

        [Test] public void EachCastRecordsTheManaAndHeroStatusesItLeaves()
        {
            var mana = new CardDefinition("m", "Maná", 1, 0, 0, manaGain: 1).WithChroma(ChromaKind.Golden);
            var state = new CombatState(HeroDefinition.For(HeroKind.Rogue), Enumerable.Repeat(mana, 10), 7, enemyHp: 1000);
            typeof(CombatState).GetProperty("Mana").SetValue(state, 2);
            var result = state.Play(0);
            Assert.That(result.CastMana, Is.EqualTo(new[] { 2, 3 }));
            var fury = new CardDefinition("s", "Fuerza", 0, 0, 0, effects: new[] { new EffectLayer(EffectCondition.Always, 0, commands: new[] { new CardCommand(CommandKind.Strength, 2) }) }).WithChroma(ChromaKind.Diamond);
            var strong = new CombatState(HeroDefinition.For(HeroKind.Warrior), Enumerable.Repeat(fury, 10), 7, enemyHp: 1000);
            var played = strong.Play(0);
            Assert.That(played.CastHeroStatuses.Select(s => s[StatusKind.Strength]), Is.EqualTo(new[] { 2, 4, 6 }));
        }

        /// <summary>
        /// Every card resolves its statuses and elements once per cast: summed over the enemies (random targets included) and on
        /// the hero, a golden copy gives twice and a diamond copy three times what the normal card gives, and the last cast's
        /// snapshot is the fight as it ends (the replay finishes on the real numbers).
        /// </summary>
        [Test] public void EveryGoldenOrDiamondCardAddsItsStatusesOncePerCast()
        {
            var cards = RunContent.Cards.ToList();
            foreach (var hero in new[] { HeroKind.Warrior, HeroKind.Mage, HeroKind.Rogue })
                foreach (var starter in RunContent.StarterDeck(hero)) if (cards.All(c => c.Id != starter.Id)) cards.Add(starter);
            Assert.That(cards.Count, Is.GreaterThan(150));
            foreach (var card in cards)
            {
                var totals = new List<long[]>();
                foreach (var chroma in new[] { ChromaKind.None, ChromaKind.Golden, ChromaKind.Diamond })
                {
                    var state = Fight(card.WithChroma(chroma));
                    var result = state.Play(0, 0);
                    Assert.That(result.Success, Is.True, card.Id + " " + chroma + ": " + result.Message);
                    var sums = new long[StatusSet.KindCount * 2];
                    for (var k = 0; k < StatusSet.KindCount; k++)
                    {
                        foreach (var e in state.Enemies) sums[k] += e.Statuses[(StatusKind)k];
                        sums[StatusSet.KindCount + k] = state.HeroStatuses[(StatusKind)k];
                    }
                    totals.Add(sums);
                    var last = result.CastEnemyStatuses.Count - 1;
                    Assert.That(last, Is.EqualTo(result.Casts - 1), card.Id);
                    for (var i = 0; i < state.Enemies.Count; i++)
                        for (var k = 0; k < StatusSet.KindCount; k++)
                            Assert.That(result.CastEnemyStatuses[last][i][(StatusKind)k], Is.EqualTo(state.Enemies[i].Statuses[(StatusKind)k]), card.Id + " " + chroma + " last snapshot");
                    Assert.That(result.CastMana[last], Is.EqualTo(state.Mana), card.Id + " " + chroma + " mana");
                }
                for (var k = 0; k < totals[0].Length; k++)
                {
                    var what = card.Id + " " + (k < StatusSet.KindCount ? "enemies " + (StatusKind)k : "hero " + (StatusKind)(k - StatusSet.KindCount));
                    Assert.That(totals[1][k], Is.EqualTo(2 * totals[0][k]), what + " (dorada)");
                    Assert.That(totals[2][k], Is.EqualTo(3 * totals[0][k]), what + " (diamante)");
                }
            }
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
