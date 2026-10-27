using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using NUnit.Framework;
using static LastWish.Tests.Floor1Kit;

namespace LastWish.Tests
{
    /// <summary>Combat rules of the floor-2 expansion: Amparo, Rito, Rencor, Sangrado, Cicatrización, bound Ilusiones, Cabezas,
    /// Reflejo, Ansia de Sangre, Necrosis, Espejo Maldito, Ofrenda Oscura, Cosecha de Almas and the floor-2 rolls.</summary>
    public sealed class Floor2MechanicTests
    {
        private static EnemyState Two(string ai, string id = "e0") => EnemyCatalog.Create(ai, 2, id);
        private static CardDefinition[] Many(CardDefinition card, int n) => Enumerable.Repeat(card, n).ToArray();

        [Test] public void WardTakesItsStacksFromEveryHitUntilTheNextEnemyTurn()
        {
            var sentinel = Two("centinela");
            var state = Fight(new[] { sentinel }, Many(Hit(10), 12));
            state.EndTurn();                                   // Mazazo; next: Runa de Amparo
            state.EndTurn();                                   // Runa de Amparo: Amparo 4, block 6
            Assert.That(sentinel.Statuses[StatusKind.Ward], Is.EqualTo(4));
            var block = sentinel.Block; var hp = sentinel.Hp;
            state.SelectEnemy(0); state.PlayCard(0);           // (10 + Ataque) - 4 reaches the block
            Assert.That(block - sentinel.Block + (hp - sentinel.Hp), Is.EqualTo(10 + state.AttackBonusNow - 4));
            var small = Two("centinela", "e1");
            var other = Fight(new[] { small }, Many(Hit(4), 6));
            small.Statuses.Add(StatusKind.Ward, 4 + other.AttackBonusNow);
            other.SelectEnemy(0); other.PlayCard(0);
            Assert.That(small.Hp, Is.EqualTo(small.MaxHp), "a hit no bigger than its Amparo does nothing");
            state.EndTurn();
            Assert.That(sentinel.Statuses[StatusKind.Ward], Is.Zero, "Amparo is lost at the start of its next turn");
        }

        [Test] public void RitualGivesStrengthAfterEachActionButNotTheTurnItIsGained()
        {
            var zealot = Two("fanatico");
            var state = Fight(new[] { zealot }, Many(Wait(), 10));
            state.EndTurn();                                   // Rito Oscuro
            Assert.That(zealot.Statuses[StatusKind.Ritual], Is.EqualTo(2));
            Assert.That(zealot.Statuses[StatusKind.Strength], Is.Zero);
            Assert.That(state.EnemyLog[0].RitualStrength, Is.Zero);
            state.EndTurn();                                   // Puñal Ritual, then +2
            Assert.That(zealot.Statuses[StatusKind.Strength], Is.EqualTo(2));
            Assert.That(state.EnemyLog[0].RitualStrength, Is.EqualTo(2));
            state.EndTurn();
            Assert.That(zealot.Statuses[StatusKind.Strength], Is.EqualTo(4));
        }

        [Test] public void GrudgeHitsTheHeroWhenItDiesAndBlockAbsorbsIt()
        {
            var shade = Two("sombra");
            var state = Fight(new[] { shade }, Many(Hit(1000), 6), invincible: false);
            var hp = state.Hp;
            state.SelectEnemy(0); state.PlayCard(0);
            Assert.That(state.Victory, Is.True);
            Assert.That(hp - state.Hp, Is.EqualTo(6));
            var ev = state.DeathEvents.Single();
            Assert.That((ev.GrudgeDamage, ev.GrudgeLost), Is.EqualTo((6, 6)));
            var guarded = Two("sombra", "e1");
            var deck = new[] { Guard(10), Hit(1000) };
            var other = Fight(new[] { guarded }, deck, invincible: false);
            var before = other.Hp;
            other.PlayCard(IndexOf(other, "guard"));
            other.SelectEnemy(0); other.PlayCard(IndexOf(other, "hit"));
            Assert.That(other.Hp, Is.EqualTo(before), "the block takes all the Rencor");
            Assert.That(other.DeathEvents.Single().GrudgeLost, Is.Zero);
        }

        [Test] public void GrudgeGrowsWhileTheShadeLives()
        {
            var shade = Two("sombra");
            var state = Fight(new[] { shade }, Many(Wait(), 10));
            state.EndTurn(); state.EndTurn();                   // Zarpa, Rencor Creciente
            Assert.That(shade.Statuses[StatusKind.Grudge], Is.EqualTo(10));
        }

        [Test] public void BleedingCostsOneHpPerCardThroughBlock()
        {
            var leech = Two("sanguijuela");
            var state = Fight(new[] { leech }, Many(Guard(5), 12), invincible: false);
            state.EndTurn();                                   // Ventosa: Sangrado 2
            Assert.That(state.HeroStatuses[StatusKind.Bleed], Is.EqualTo(2));
            var hp = state.Hp;
            state.PlayCard(0);
            Assert.That(state.Hp, Is.EqualTo(hp - 1), "block does not stop the bleeding");
            Assert.That(state.LastPlay.HeroHits.Single(), Is.EqualTo(("", 1, "bleed")));
            state.PlayCard(0);
            Assert.That(state.Hp, Is.EqualTo(hp - 2));
            state.EndTurn(); state.EndTurn();
            Assert.That(state.HeroStatuses[StatusKind.Bleed], Is.Zero, "two turns, then it stops");
        }

        [Test] public void DrainHealsTheLeechWithTheHpItTakes()
        {
            var leech = Two("sanguijuela");
            var state = Fight(new[] { leech }, Many(Wait(), 10), invincible: false);
            state.EndTurn();                                   // Ventosa
            leech.LoseHp(20);
            var hp = state.Hp; var leechHp = leech.Hp;
            state.EndTurn();                                   // Succión: 8
            var lost = hp - state.Hp;
            Assert.That(lost, Is.EqualTo(EnemyCatalog.Scale(7, 2)));
            Assert.That(leech.Hp, Is.EqualTo(leechHp + lost));
        }

        [Test] public void RegenHealsAtTheEndOfItsTurnUnlessItBurnsOrIsPoisoned()
        {
            var troll = Two("trol");
            var state = Fight(new[] { troll }, Many(Wait(), 10));
            troll.LoseHp(30);
            var hp = troll.Hp;
            state.EndTurn();
            Assert.That(troll.Hp, Is.EqualTo(hp + 3));
            Assert.That(state.EnemyLog[0].Regenerated, Is.EqualTo(3));
            troll.Statuses.Add(StatusKind.Poison, 5);
            hp = troll.Hp; state.EndTurn();
            Assert.That(troll.Hp, Is.EqualTo(hp - 5), "poisoned: no healing");
            troll.Statuses.Remove(StatusKind.Poison); troll.Statuses.Add(StatusKind.Fire, 2);
            hp = troll.Hp; state.EndTurn();
            Assert.That(troll.Hp, Is.EqualTo(hp - 2), "burning: no healing");
        }

        [Test] public void IllusionsVanishWithTheirSummonerAndPayNoGold()
        {
            var mage = Two("ilusionista");
            var state = Fight(new[] { mage }, Many(Huge(), 10));
            state.EndTurn();                                   // Desdoblarse
            var illusion = state.Enemies.Single(e => e.Definition.Ai == "ilusion");
            Assert.That(illusion.Bound, Is.True); Assert.That(illusion.PaysGold, Is.False); Assert.That(illusion.SummonerId, Is.EqualTo("e0"));
            state.SelectEnemy(0); state.PlayCard(0);
            Assert.That(mage.Alive, Is.False);
            Assert.That(illusion.Alive, Is.False, "it vanished");
            Assert.That(state.Victory, Is.True);
            Assert.That(state.DeathEvents.Single(d => d.EnemyId == illusion.Id).Dispelled, Is.True);
        }

        [Test] public void AnIllusionBlinksAHitAwayWithoutAnswering()
        {
            var mage = Two("ilusionista");
            var state = Fight(new[] { mage }, Many(Hit(3), 10), invincible: false);
            state.EndTurn(); state.EndTurn();                   // summon, then the Ilusión strikes and plans Parpadeo
            var illusion = state.Enemies.Single(e => e.Definition.Ai == "ilusion");
            state.EndTurn();                                   // Parpadeo: Desvío 1
            Assert.That(illusion.Statuses[StatusKind.Parry], Is.EqualTo(1));
            var hp = state.Hp; var ihp = illusion.Hp;
            state.SelectEnemy(state.Enemies.ToList().IndexOf(illusion)); state.PlayCard(0);
            Assert.That(illusion.Hp, Is.EqualTo(ihp));
            Assert.That(state.LastPlay.Parried[illusion.Id], Is.EqualTo(1));
            Assert.That(state.LastPlay.HeroHits.Where(h => h.kind == "riposte"), Is.Empty);
            Assert.That(state.Hp, Is.EqualTo(hp));
        }

        [Test] public void BigHitsCutTheHydrasHeadsAndTheBitesFollow()
        {
            var hydra = Two("hidra");
            var state = Fight(new[] { hydra }, new[] { Hit(25, "big"), Hit(25, "big"), Hit(25, "big"), Hit(10, "small") });
            Assert.That(hydra.Intent.Hits, Is.EqualTo(3));
            state.SelectEnemy(0); state.PlayCard(IndexOf(state, "big"));
            Assert.That(hydra.Statuses[StatusKind.Heads], Is.EqualTo(2));
            Assert.That(state.LastPlay.HeadsCut[hydra.Id], Is.EqualTo(1));
            Assert.That(hydra.Intent.Hits, Is.EqualTo(2), "Mordisco Múltiple bites once less right away");
            state.PlayCard(IndexOf(state, "big"));
            Assert.That(hydra.Statuses[StatusKind.Heads], Is.EqualTo(Floor2Rules.MinHeads), "never below 2");
            var other = Two("hidra", "h2");
            var calm = Fight(new[] { other }, Many(Hit(10), 4));
            calm.SelectEnemy(0); calm.PlayCard(0);
            Assert.That(other.Statuses[StatusKind.Heads], Is.EqualTo(3), "a small hit cuts nothing");
        }

        [Test] public void ReflectSendsHalfOfABigHitBackAndSpendsAStack()
        {
            var queen = Two("reina_espejos");
            var state = Fight(new[] { queen }, Many(Hit(30), 6), invincible: false);
            var hp = state.Hp; var back = (30 + state.AttackBonusNow) / 2;
            state.SelectEnemy(0); state.PlayCard(0);
            Assert.That(state.Hp, Is.EqualTo(hp - back));
            Assert.That(state.LastPlay.HeroHits.Single(), Is.EqualTo((queen.Id, back, "reflect")));
            Assert.That(queen.Statuses[StatusKind.Reflect], Is.EqualTo(1));
            state.PlayCard(0); state.PlayCard(0);
            Assert.That(queen.Statuses[StatusKind.Reflect], Is.Zero);
            var hpAfter = state.Hp;
            Assert.That(hp - hpAfter, Is.EqualTo(2 * back), "two stacks, two reflections");
            var small = Two("reina_espejos", "q2");
            var bonus = Fight(new[] { Two("trol", "t") }).AttackBonusNow;
            var other = Fight(new[] { small }, Many(Hit(Floor2Rules.ReflectFrom - 1 - bonus), 4), invincible: false);
            var before = other.Hp; other.SelectEnemy(0); other.PlayCard(0);
            Assert.That(other.Hp, Is.EqualTo(before), "a hit under 15 is not reflected");
            Assert.That(small.Statuses[StatusKind.Reflect], Is.EqualTo(2));
        }

        [Test] public void CursedMirrorSendsItsDebuffsToTheHero()
        {
            var queen = Two("reina_espejos");
            var state = Fight(new[] { queen }, Many(Wait(), 12));
            state.EndTurn(); state.EndTurn();                   // Fragmentar, Corte Prismático
            Assert.That(queen.Intent.Id, Is.EqualTo("cursed_mirror"));
            Assert.That(queen.Intent.Kind, Is.EqualTo(IntentKind.Debuff));
            queen.Statuses.Add(StatusKind.Poison, 6); queen.Statuses.Add(StatusKind.Weak, 2); queen.Statuses.Add(StatusKind.Vulnerable, 3); queen.Statuses.Add(StatusKind.Marked, 4);
            state.EndTurn();
            var action = state.EnemyLog.Single(a => a.MoveId == "cursed_mirror");
            Assert.That(action.DebuffsReturned, Is.EqualTo(5 + 2 + 3), "the poison already ticked once");
            Assert.That(queen.Statuses[StatusKind.Poison] + queen.Statuses[StatusKind.Weak] + queen.Statuses[StatusKind.Vulnerable] + queen.Statuses[StatusKind.Marked], Is.Zero);
            Assert.That(state.HeroStatuses[StatusKind.Vulnerable], Is.EqualTo(3));
            Assert.That(state.HeroStatuses[StatusKind.Weak], Is.EqualTo(2));
            Assert.That(state.HeroStatuses[StatusKind.Poison], Is.EqualTo(4), "5 moved, 1 ticked at the start of your turn");
            Assert.That(queen.Statuses[StatusKind.Reflect], Is.EqualTo(3), "it regains 1 Reflejo");
        }

        [Test] public void ThirstFeedsOncePerTurnWhenTheHeroLosesHp()
        {
            var countess = Two("condesa");
            var state = Fight(new[] { countess }, Many(Wait(), 12), invincible: false);
            state.PlayCard(0);
            Assert.That(countess.Statuses[StatusKind.Strength], Is.Zero, "no HP lost, no Strength");
            state.EndTurn();                                   // Beso Carmesí (enemy turn: does not count)
            Assert.That(countess.Statuses[StatusKind.Strength], Is.Zero);
            state.EndTurn();                                   // Corte Sangrante: Sangrado 2
            state.PlayCard(0);
            Assert.That(countess.Statuses[StatusKind.Strength], Is.EqualTo(1));
            Assert.That(state.ThirstFeeds.Single(), Is.EqualTo((countess.Id, 1)));
            state.PlayCard(0);
            Assert.That(countess.Statuses[StatusKind.Strength], Is.EqualTo(1), "once per turn");
            state.EndTurn();
            state.PlayCard(0);
            Assert.That(countess.Statuses[StatusKind.Strength], Is.EqualTo(2), "a new turn feeds again");
        }

        [Test] public void NecrosisStopsEveryHeal()
        {
            var necro = Two("nigromante");
            var state = Fight(new[] { necro }, Many(Wait(), 12), invincible: false);
            state.EndTurn();                                   // Alzar Huesos
            Assert.That(necro.Intent.Id, Is.EqualTo("necrotic_touch"));
            state.EndTurn();                                   // Toque Necrótico: Necrosis 2
            Assert.That(state.HeroStatuses[StatusKind.Necrosis], Is.EqualTo(2));
            var hp = state.Hp;
            typeof(CombatState).GetMethod("HealHero", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(state, new object[] { 10L });
            Assert.That(state.Hp, Is.EqualTo(hp));
            Assert.That(state.Block, Is.Zero, "not even as block");
            state.EndTurn(); state.EndTurn();
            Assert.That(state.HeroStatuses[StatusKind.Necrosis], Is.Zero);
            var lower = state.Hp;
            typeof(CombatState).GetMethod("HealHero", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(state, new object[] { 5L });
            Assert.That(state.Hp, Is.EqualTo(System.Math.Min(state.Hero.MaxHp, lower + 5)));
        }

        [Test] public void SoulHarvestGrowsWithEveryFallenSkeleton()
        {
            var necro = Two("nigromante");
            var state = Fight(new[] { necro }, Many(Huge(), 12));
            state.EndTurn();                                   // two skeletons arrive
            var skeletons = state.Enemies.Where(e => e.Definition.Ai == "esqueleto").ToList();
            Assert.That(skeletons.Count, Is.EqualTo(2));
            state.EndTurn();                                   // Toque Necrótico; next: Cosecha de Almas
            Assert.That(necro.Intent.Id, Is.EqualTo("soul_harvest"));
            var base0 = necro.Intent.Value;
            Assert.That(base0, Is.EqualTo(EnemyCatalog.Scale(6, 2)));
            state.SelectEnemy(state.Enemies.ToList().IndexOf(skeletons[0])); state.PlayCard(0);
            Assert.That(necro.MinionsLost, Is.EqualTo(1));
            Assert.That(necro.Intent.Value, Is.EqualTo(EnemyCatalog.Scale(9, 2)), "updated right after the card");
            state.SelectEnemy(state.Enemies.ToList().IndexOf(skeletons[1])); state.PlayCard(0);
            Assert.That(necro.Intent.Value, Is.EqualTo(EnemyCatalog.Scale(12, 2)));
        }

        [Test] public void TheHighPriestSacrificesHisStrongestZealot()
        {
            var priest = Two("sacerdote");
            var state = Fight(new[] { priest }, Many(Wait(), 12));
            state.EndTurn();                                   // Llamada al Abismo
            var zealot = state.Enemies.Single(e => e.Definition.Ai == "fanatico");
            Assert.That(zealot.MaxHp, Is.EqualTo((int)(EnemyCatalog.Scale(50, 2) * 0.5)));
            Assert.That(zealot.PaysGold, Is.False);
            state.EndTurn();                                   // Daga Ceremonial; the zealot starts his rite
            Assert.That(priest.Intent.Id, Is.EqualTo("dark_offering"));
            zealot.Statuses.Add(StatusKind.Strength, 3);
            priest.LoseHp(20);
            var hp = priest.Hp;
            state.EndTurn();                                   // Ofrenda Oscura
            var action = state.EnemyLog.Single(a => a.MoveId == "dark_offering");
            Assert.That(zealot.Alive, Is.False);
            Assert.That(action.Devoured, Is.EqualTo(zealot.Id));
            Assert.That(action.DevouredStrength, Is.EqualTo(3 + 2));
            Assert.That(priest.Statuses[StatusKind.Strength], Is.EqualTo(5));
            Assert.That(priest.Hp, Is.EqualTo(hp + EnemyCatalog.Scale(12, 2)));
        }

        [Test] public void KillingTheZealotFirstTurnsTheOfferingIntoPunishment()
        {
            var priest = Two("sacerdote");
            var state = Fight(new[] { priest }, Many(Huge(), 12));
            state.EndTurn(); state.EndTurn();
            Assert.That(priest.Intent.Id, Is.EqualTo("dark_offering"));
            var zealot = state.Enemies.Single(e => e.Definition.Ai == "fanatico");
            state.SelectEnemy(state.Enemies.ToList().IndexOf(zealot)); state.PlayCard(0);
            Assert.That(priest.Intent.Id, Is.EqualTo("abyss_punish"), "no victim left: it changes on the spot");
            Assert.That(priest.Intent.Kind, Is.EqualTo(IntentKind.Attack));
        }

        [Test] public void TheCountessCallsBatsThatPayNoGold()
        {
            var countess = Two("condesa");
            var state = Fight(new[] { countess }, Many(Wait(), 12));
            countess.LoseHp(countess.Hp - countess.MaxHp / 2);
            state.EndTurn();                                   // acts, then plans Hambre Eterna
            Assert.That(countess.Intent.Id, Is.EqualTo("eternal_hunger"));
            Assert.That(countess.Intent.Extras.Summons.Single().Ai, Is.EqualTo("murcielago"));
            state.EndTurn();
            var bat = state.Enemies.Single(e => e.Definition.Ai == "murcielago");
            Assert.That(bat.PaysGold, Is.False); Assert.That(bat.Summoned, Is.True);
            Assert.That(countess.Statuses[StatusKind.Thirst], Is.EqualTo(2));
            Assert.That(bat.Intent, Is.Not.Null);
        }

        [Test] public void ShardsCutTheHeroWhenTheyBreak()
        {
            var queen = Two("reina_espejos");
            var state = Fight(new[] { queen }, Many(Hit(13), 12), invincible: false);
            state.EndTurn();                                   // two shards
            var shard = state.Enemies.First(e => e.Definition.Ai == "esquirla");
            Assert.That(shard.Statuses[StatusKind.Grudge], Is.EqualTo(4));
            var hp = state.Hp;
            state.SelectEnemy(state.Enemies.ToList().IndexOf(shard)); state.PlayCard(0);
            Assert.That(shard.Alive, Is.False);
            Assert.That(hp - state.Hp, Is.EqualTo(4));
        }

        [Test] public void FloorTwoRollsUseTheNewPoolsAndKeepFloorOne()
        {
            var seen = new HashSet<string>(); var bosses = new HashSet<string>();
            for (var seed = 1; seed <= 80; seed++)
            {
                var original = new RunState(HeroKind.Mage, seed); original.AdvanceFloor();
                var expanded = new RunState(HeroKind.Mage, seed, EnemyRoster.Expanded); expanded.AdvanceFloor();
                Assert.That(original.GenerateEnemies("r1").Concat(original.GenerateElite("r2")).All(e => !EnemyCatalog.IsExpansion(e.Definition.Ai)), Is.True);
                Assert.That(original.GenerateBoss().Single().Definition.Ai, Is.EqualTo("crypt"), "the original floor 2 keeps the Señor de la Cripta");
                foreach (var e in expanded.GenerateEnemies("r" + seed).Concat(expanded.GenerateElite("r2")))
                {
                    seen.Add(e.Definition.Ai);
                    Assert.That(EnemyCatalog.IsExpansion(e.Definition.Ai) && !EnemyCatalog.IsFloor2(e.Definition.Ai), Is.False, "no floor-1 expansion enemy on floor 2: " + e.Definition.Ai);
                }
                var boss = expanded.GenerateBoss();
                bosses.Add(expanded.FloorBossKey());
                Assert.That(EnemyCatalog.FloorBossKeys(2), Does.Contain(expanded.FloorBossKey()));
                Assert.That(boss.All(b => b.Definition.Boss), Is.True);
                var floor4 = new RunState(HeroKind.Mage, seed, EnemyRoster.Expanded); floor4.AdvanceFloor(); floor4.AdvanceFloor(); floor4.AdvanceFloor();
                var reference = new RunState(HeroKind.Mage, seed); reference.AdvanceFloor(); reference.AdvanceFloor(); reference.AdvanceFloor();
                Assert.That(floor4.GenerateEnemies("r1").Select(e => e.Definition.Ai), Is.EqualTo(reference.GenerateEnemies("r1").Select(e => e.Definition.Ai)), "floor 4 is the original");
                Assert.That(floor4.FloorBossKey(), Is.Null);
                var floor1 = new RunState(HeroKind.Mage, seed, EnemyRoster.Expanded);
                Assert.That(floor1.GenerateEnemies("r1").All(e => !EnemyCatalog.IsFloor2(e.Definition.Ai)), Is.True, "no floor-2 enemy on floor 1");
            }
            Assert.That(seen.Count(EnemyCatalog.IsFloor2), Is.GreaterThanOrEqualTo(5), string.Join(",", seen));
            Assert.That(bosses.Count, Is.EqualTo(3));
        }

        [Test] public void PruebasForcesFloorTwoContentOnFloorsOneAndTwo()
        {
            try
            {
                Tuning.Reset(); Tuning.Current.ForcedBoss = 8; Tuning.Current.ForcedElite = 10; Tuning.Current.ForcedEncounter = 33;
                var run = new RunState(HeroKind.Warrior, 5, EnemyRoster.Expanded);
                Assert.That(run.GenerateBoss().Single().Definition.Ai, Is.EqualTo("hidra"));
                Assert.That(run.GenerateElite("r").Single().Definition.Ai, Is.EqualTo("condesa"));
                Assert.That(run.GenerateEnemies("r").Single().Definition.Ai, Is.EqualTo("sombra"));
                run.AdvanceFloor();
                Assert.That(run.GenerateBoss().Single().Definition.Ai, Is.EqualTo("hidra"));
                Tuning.Current.ForcedBoss = 4;
                Assert.That(run.GenerateBoss().Single().Definition.Ai, Is.EqualTo("inquisidor"), "a floor-1 fight can be tested on floor 2 too");
                run.AdvanceFloor();
                Assert.That(run.GenerateBoss().Single().Definition.Ai, Is.EqualTo("inquisidor"), "and on floor 3");
                run.AdvanceFloor();
                Assert.That(run.GenerateBoss().Single().Definition.Ai, Is.EqualTo("crypt"), "floor 4 ignores the expansion indices");
            }
            finally { Tuning.Reset(); }
        }

        [Test] public void TheCapHoldsInLongFloorTwoFights()
        {
            foreach (var fight in new[] { EnemyCatalog.CreateBoss("nigromante", 2), EnemyCatalog.CreateBoss("reina_espejos", 2), new List<EnemyState> { Two("sacerdote") },
                new List<EnemyState> { Two("condesa") }, new List<EnemyState> { Two("ilusionista") }, EnemyCatalog.CreateGroup("teatro", 2, "g") })
            {
                var state = Fight(fight, Many(Wait(), 10));
                for (var t = 0; t < 30 && !state.Finished; t++)
                {
                    state.EndTurn();
                    var alive = state.Enemies.Where(e => e.Alive).ToList();
                    var large = alive.Count(e => EnemyField.IsLarge(e.Definition));
                    Assert.That(EnemyField.Fits(large, alive.Count - large), Is.True, fight[0].Definition.Ai + " turn " + t);
                    Assert.That(alive.All(e => e.Intent != null), Is.True);
                    Assert.That(state.Enemies.Select(e => e.Id).Distinct().Count(), Is.EqualTo(state.Enemies.Count));
                    foreach (var e in state.Enemies) if (e.Summoned && e.Alive) e.LoseHp(e.Hp);   // keep the summoners summoning
                }
                var summoner = state.Enemies[0];
                var budget = EnemyCatalog.Profile(summoner.Definition.Ai).SummonBudget;
                Assert.That(summoner.SummonsMade, Is.LessThanOrEqualTo(budget), summoner.Definition.Ai);
            }
        }

        [Test] public void PreviewsNeverTouchTheRealFight()
        {
            var queen = Two("reina_espejos");
            var state = Fight(new[] { queen }, Many(Hit(40), 6), invincible: false);
            var copy = state.Clone();
            copy.SelectEnemy(0); copy.PlayCard(0);
            Assert.That(queen.Statuses[StatusKind.Reflect], Is.EqualTo(2));
            Assert.That(queen.Hp, Is.EqualTo(queen.MaxHp));
            Assert.That(state.Hp, Is.EqualTo(100));
            Assert.That(copy.Hp, Is.EqualTo(100 - (40 + state.AttackBonusNow) / 2));
        }
    }
}
