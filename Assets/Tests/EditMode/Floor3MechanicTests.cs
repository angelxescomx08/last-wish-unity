using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using NUnit.Framework;
using static LastWish.Tests.Floor1Kit;

namespace LastWish.Tests
{
    /// <summary>Combat rules of the floor-3 expansion: Pendón, Cólera, Perforante, Agotamiento, Congelación, Rebobinar,
    /// Inmortal, Cuerda, the summoners at the cap and the floor-3 rolls (trios included).</summary>
    public sealed class Floor3MechanicTests
    {
        private static EnemyState Three(string ai, string id = "e0") => EnemyCatalog.Create(ai, 3, id);
        private static CardDefinition[] Many(CardDefinition card, int n) => Enumerable.Repeat(card, n).ToArray();

        [Test] public void TheBannerInspiresItsAlliesWhileItLives()
        {
            var group = EnemyCatalog.CreateGroup("vanguardia", 3, "g");
            var state = Fight(group, Many(Huge(), 10));
            var (banner, imp) = (group[0], group[1]);
            Assert.That(imp.Statuses[StatusKind.Inspired], Is.EqualTo(2), "set from the first turn");
            Assert.That(banner.Statuses[StatusKind.Inspired], Is.Zero, "its own Pendón does not count");
            var plain = imp.Intent.Value + imp.Statuses[StatusKind.Strength];
            Assert.That(imp.HitDamage(state.HeroStatuses), Is.EqualTo(plain + 2));
            state.SelectEnemy(0); state.PlayCard(0);
            Assert.That(banner.Alive, Is.False);
            Assert.That(imp.Statuses[StatusKind.Inspired], Is.Zero, "the aura ends right after the card");
            Assert.That(imp.HitDamage(state.HeroStatuses), Is.EqualTo(plain));
        }

        [Test] public void RaisingTheBannerMakesTheAuraBigger()
        {
            var group = EnemyCatalog.CreateGroup("guardia", 3, "g");
            var state = Fight(group, Many(Wait(), 10));
            state.EndTurn();                                   // Asta Helada; next Alzar el Pendón
            state.EndTurn();
            Assert.That(group[0].Statuses[StatusKind.Banner], Is.EqualTo(3));
            Assert.That(group[1].Statuses[StatusKind.Inspired], Is.EqualTo(3));
        }

        [Test] public void AnAuraAlsoReachesOriginalEnemiesAndArrivals()
        {
            var commander = Three("comandante");
            var state = Fight(new[] { commander }, Many(Wait(), 10));
            state.EndTurn();                                   // Llamar a la Guardia
            var squire = state.Enemies.Single(e => e.Definition.Ai == "escudero");
            Assert.That(squire.Statuses[StatusKind.Inspired], Is.EqualTo(1), "a summoned Escudero is inspired as it arrives");
            Assert.That(squire.PaysGold, Is.False);
        }

        [Test] public void RageGivesStrengthPerWoundingHitUntilItsNextAction()
        {
            var barbarian = Three("barbaro");
            var state = Fight(new[] { barbarian }, Many(Hit(5), 12));
            state.SelectEnemy(0);
            state.PlayCard(0); state.PlayCard(0); state.PlayCard(0);
            Assert.That(barbarian.Statuses[StatusKind.Strength], Is.EqualTo(3));
            Assert.That(barbarian.RageStrength, Is.EqualTo(3));
            Assert.That(state.LastPlay.RageGains[barbarian.Id], Is.EqualTo(1));
            var expected = EnemyCatalog.Scale(10, 3) + 3;
            Assert.That(barbarian.HitDamage(state.HeroStatuses), Is.EqualTo(expected), "the next Hachazo carries it");
            state.EndTurn();
            var action = state.EnemyLog.Single();
            Assert.That(action.RageSpent, Is.EqualTo(3));
            Assert.That(barbarian.Statuses[StatusKind.Strength], Is.Zero, "spent after its action");
            Assert.That(barbarian.RageStrength, Is.Zero);
        }

        [Test] public void HitsThatOnlyTakeBlockOrArePoisonDoNotEnrage()
        {
            var barbarian = Three("barbaro");
            barbarian.GainBlock(500);
            var state = Fight(new[] { barbarian }, Many(Hit(5), 6));
            state.SelectEnemy(0); state.PlayCard(0);
            Assert.That(barbarian.Statuses[StatusKind.Strength], Is.Zero, "the block took it");
            Assert.That(state.LastPlay.RageGains, Is.Empty);
            barbarian.Statuses.Add(StatusKind.Poison, 4);
            state.EndTurn();
            Assert.That(barbarian.Statuses[StatusKind.Strength], Is.Zero, "poison is not a hit");
        }

        [Test] public void PierceIgnoresTheHerosBlock()
        {
            var hunter = Three("cazador");
            var state = Fight(new[] { hunter }, Many(Guard(40), 10), invincible: false);
            state.PlayCard(0);
            Assert.That(state.Block, Is.GreaterThanOrEqualTo(40));
            var hp = state.Hp;
            state.EndTurn();                                   // Jabalina de Hielo
            var action = state.EnemyLog.Single();
            Assert.That(action.Hits.Single(), Is.EqualTo(EnemyCatalog.Scale(8, 3)));
            Assert.That(action.Pierced, Is.EqualTo(EnemyCatalog.Scale(8, 3)));
            Assert.That(hp - state.Hp, Is.EqualTo(EnemyCatalog.Scale(8, 3)));
            var other = Three("barbaro", "b");
            var guarded = Fight(new[] { other }, Many(Guard(40), 10), invincible: false);
            guarded.PlayCard(0); var before = guarded.Hp;
            guarded.EndTurn();
            Assert.That(guarded.Hp, Is.EqualTo(before), "a normal attack is blocked");
            Assert.That(guarded.EnemyLog.Single().Pierced, Is.Zero);
        }

        [Test] public void ExhaustionStartsTheNextTurnWithOneManaLess()
        {
            var mage = Three("arcanista");
            var state = Fight(new[] { mage }, Many(Wait(), 10));
            var full = state.ManaMaximum;
            state.EndTurn();                                   // Drenar Maná: Agotamiento 1
            Assert.That(state.HeroStatuses[StatusKind.Exhaustion], Is.EqualTo(1));
            Assert.That(state.Mana, Is.EqualTo(full - 1));
            Assert.That(state.ManaExhausted, Is.EqualTo(1));
            state.EndTurn();
            Assert.That(state.HeroStatuses[StatusKind.Exhaustion], Is.Zero);
            Assert.That(state.Mana, Is.EqualTo(full), "one turn only");
            Assert.That(state.ManaExhausted, Is.Zero);
        }

        [Test] public void FrostbiteCostsOneHpPerCardLeftInTheHand()
        {
            var wisp = Three("anima");
            var state = Fight(new[] { wisp }, Many(Guard(100), 20), invincible: false);
            state.EndTurn();                                   // Toque Gélido: Congelación 2
            Assert.That(state.HeroStatuses[StatusKind.Frostbite], Is.EqualTo(2));
            state.PlayCard(0); state.PlayCard(0);               // 3 cards left, lots of block
            var left = state.Hand.Count;
            Assert.That(left, Is.GreaterThan(0));
            var hp = state.Hp;
            state.EndTurn();                                   // Velo de Escarcha (no hit)
            Assert.That(state.FrostbiteLost, Is.EqualTo(left), "block does not stop it");
            Assert.That(hp - state.Hp, Is.EqualTo(left));
            while (state.Hand.Count > 0) state.PlayCard(0);
            state.EndTurn();
            Assert.That(state.FrostbiteLost, Is.Zero, "an empty hand loses nothing");
            Assert.That(state.HeroStatuses[StatusKind.Frostbite], Is.Zero, "two turns, then it thaws");
        }

        [Test] public void RewindHealsWhatTheHeroTookThisTurn()
        {
            var crono = Three("cronomante");
            var state = Fight(new[] { crono }, Many(Hit(20), 12));
            state.EndTurn(); state.EndTurn();                   // Ralentizar, Paradoja; next Rebobinar
            Assert.That(crono.Intent.Id, Is.EqualTo("rewind"));
            Assert.That(crono.HpAtTurnStart, Is.EqualTo(crono.Hp));
            state.SelectEnemy(0); state.PlayCard(0); state.PlayCard(0);
            var taken = crono.HpAtTurnStart - crono.Hp;
            Assert.That(taken, Is.GreaterThan(0));
            state.EndTurn();
            var action = state.EnemyLog.Single();
            Assert.That(action.Rewound, Is.EqualTo(taken));
            Assert.That(crono.Hp, Is.EqualTo(crono.MaxHp), "back to where the turn began");
        }

        [Test] public void TheLichCannotDieWhileItsPhylacteryLives()
        {
            var party = EnemyCatalog.CreateBoss("liche", 3);
            var (lich, phylactery) = (party[0], party[1]);
            var state = Fight(party, Many(Huge(), 10));
            Assert.That(lich.Statuses[StatusKind.Undying], Is.EqualTo(1));
            state.SelectEnemy(0); state.PlayCard(0);
            Assert.That(lich.Alive, Is.True); Assert.That(lich.Hp, Is.EqualTo(1), "Inmortal");
            lich.Statuses.Add(StatusKind.Poison, 50);
            state.EndTurn();
            Assert.That(lich.Hp, Is.EqualTo(1), "poison cannot finish it either");
            state.SelectEnemy(1); state.PlayCard(0);
            Assert.That(phylactery.Alive, Is.False);
            Assert.That(lich.Statuses[StatusKind.Undying], Is.Zero, "mortal right after the card");
            state.SelectEnemy(0); state.PlayCard(0);
            Assert.That(state.Victory, Is.True);
        }

        [Test] public void TheLichForgesANewPhylacteryOnceAtHalfHp()
        {
            var party = EnemyCatalog.CreateBoss("liche", 3);
            var lich = party[0];
            var state = Fight(party, Many(Huge(), 10));
            state.SelectEnemy(1); state.PlayCard(0);            // the Filacteria breaks first
            lich.LoseHp(lich.Hp - lich.MaxHp / 2);
            state.EndTurn();                                   // acts, then plans the rite
            Assert.That(lich.Intent.Id, Is.EqualTo("phylactery_rite"));
            state.EndTurn();
            var fresh = state.Enemies.Single(e => e.Definition.Ai == "filacteria" && e.Alive);
            Assert.That(fresh.Summoned, Is.True); Assert.That(fresh.PaysGold, Is.False);
            Assert.That(lich.Statuses[StatusKind.Undying], Is.EqualTo(1), "Inmortal again");
        }

        [Test] public void TheClockmakersChimeHitsOncePerSpringAndAutomataWindItUp()
        {
            var clock = EnemyCatalog.CreateBoss("relojero", 3).Single();
            var state = Fight(new[] { clock }, Many(Wait(), 12), invincible: true);
            state.EndTurn();                                   // Montar Autómatas: two arrive
            var automata = state.Enemies.Where(e => e.Definition.Ai == "automata").ToList();
            Assert.That(automata.Count, Is.EqualTo(2));
            Assert.That(automata.All(a => a.SummonerId == clock.Id), Is.True);
            state.EndTurn();                                   // Manecillas; both automata act
            Assert.That(clock.Statuses[StatusKind.Spring], Is.EqualTo(4));
            Assert.That(state.EnemyLog.Count(a => a.WoundUp == clock.Id), Is.EqualTo(2));
            state.EndTurn();                                   // Detener el Tiempo; plans Campanada
            Assert.That(clock.Intent.Id, Is.EqualTo("chime"));
            Assert.That(clock.Intent.Hits, Is.EqualTo(6));
            state.EndTurn();
            var chime = state.EnemyLog.Single(a => a.MoveId == "chime");
            Assert.That(chime.Hits.Count, Is.EqualTo(6));
            Assert.That(clock.Statuses[StatusKind.Spring], Is.EqualTo(1 + 2), "back to 1, then the automata wound it up again");
        }

        [Test] public void AnAutomatonOfTheTinkererWindsNobodyUp()
        {
            var tinker = Three("ensamblador");
            var state = Fight(new[] { tinker }, Many(Wait(), 12));
            state.EndTurn(); state.EndTurn();
            var automaton = state.Enemies.Single(e => e.Definition.Ai == "automata");
            Assert.That(state.EnemyLog.All(a => a.WoundUp == null), Is.True);
            Assert.That(tinker.Statuses[StatusKind.Spring], Is.Zero);
            Assert.That(automaton.PaysGold, Is.False);
        }

        [Test] public void TheDragonKeepsItsBlockUntilItSheds()
        {
            var dragon = Three("dragona");
            var state = Fight(new[] { dragon }, Many(Wait(), 12));
            state.EndTurn(); state.EndTurn(); state.EndTurn();   // Aliento, Garra, Plegar las Alas
            var block = dragon.Block;
            Assert.That(block, Is.EqualTo(EnemyCatalog.Scale(18, 3)));
            state.EndTurn();                                   // keeps 12 of it during its next turn
            Assert.That(dragon.Block, Is.EqualTo(12));
            dragon.LoseHp(dragon.Hp - dragon.MaxHp / 2);
            state.EndTurn();                                   // plans Furia Glacial
            Assert.That(dragon.Intent.Id, Is.EqualTo("glacial_fury"));
            state.EndTurn();
            Assert.That(dragon.Statuses[StatusKind.Shell], Is.Zero);
            Assert.That(dragon.Statuses[StatusKind.Rage], Is.EqualTo(2));
        }

        [Test] public void FloorThreeRollsUseTheNewPoolsWithTrios()
        {
            var seen = new HashSet<string>(); var bosses = new HashSet<string>(); var trios = 0; var groupTrios = 0;
            for (var seed = 1; seed <= 120; seed++)
            {
                var run = new RunState(HeroKind.Mage, seed, EnemyRoster.Expanded); run.AdvanceFloor(); run.AdvanceFloor();
                foreach (var room in new[] { "r1", "r2", "r3" })
                {
                    var enemies = run.GenerateEnemies(room + seed);
                    foreach (var e in enemies)
                    {
                        seen.Add(e.Definition.Ai);
                        Assert.That(EnemyCatalog.IsExpansion(e.Definition.Ai) && !EnemyCatalog.IsFloor3(e.Definition.Ai), Is.False, "only floor-3 expansion enemies on floor 3: " + e.Definition.Ai);
                    }
                    Assert.That(enemies.Count, Is.LessThanOrEqualTo(EnemyField.MaxAlive));
                    if (enemies.Count == 3)
                    {
                        trios++;
                        Assert.That(enemies[2].Id.EndsWith("_e2"), Is.True);
                        Assert.That(enemies.Select(e => e.Definition.Ai).Distinct().Count() >= 2, Is.True);
                        if (EnemyCatalog.Floor3Groups.Any(g => g.Name == enemies[0].PairName)) groupTrios++;
                    }
                }
                foreach (var e in run.GenerateElite("e" + seed)) seen.Add(e.Definition.Ai);
                bosses.Add(run.FloorBossKey());
                Assert.That(EnemyCatalog.FloorBossKeys(3), Does.Contain(run.FloorBossKey()));
                var original = new RunState(HeroKind.Mage, seed); original.AdvanceFloor(); original.AdvanceFloor();
                Assert.That(original.GenerateEnemies("r1").All(e => !EnemyCatalog.IsExpansion(e.Definition.Ai)), Is.True, "the original roster never changes");
                Assert.That(original.GenerateBoss().Single().Definition.Ai, Is.EqualTo("crypt"));
            }
            Assert.That(seen.Count(EnemyCatalog.IsFloor3), Is.GreaterThanOrEqualTo(8), string.Join(",", seen));
            Assert.That(seen.Contains("abanderado"), Is.True, "the Abanderado comes in groups and trios");
            Assert.That(bosses.Count, Is.EqualTo(3));
            Assert.That(trios, Is.GreaterThan(10), "a quarter of the pair fights get a third enemy");
            Assert.That(groupTrios, Is.GreaterThan(0));
        }

        [Test] public void FloorsOneAndTwoNeverMeetFloorThreeEnemies()
        {
            for (var seed = 1; seed <= 60; seed++)
            {
                var run = new RunState(HeroKind.Rogue, seed, EnemyRoster.Expanded);
                Assert.That(run.GenerateEnemies("a").Concat(run.GenerateElite("b")).Any(e => EnemyCatalog.IsFloor3(e.Definition.Ai)), Is.False);
                run.AdvanceFloor();
                Assert.That(run.GenerateEnemies("a").Concat(run.GenerateElite("b")).Any(e => EnemyCatalog.IsFloor3(e.Definition.Ai)), Is.False);
                Assert.That(run.GenerateEnemies("c").Count, Is.LessThanOrEqualTo(2), "no trios before floor 3");
            }
        }

        [Test] public void PruebasForcesFloorThreeContentOnEveryExpandedFloor()
        {
            try
            {
                Tuning.Reset(); Tuning.Current.ForcedBoss = 10; Tuning.Current.ForcedElite = 13; Tuning.Current.ForcedEncounter = 50;
                var run = new RunState(HeroKind.Warrior, 5, EnemyRoster.Expanded);
                Assert.That(run.GenerateBoss().Select(e => e.Definition.Ai), Is.EqualTo(new[] { "liche", "filacteria" }));
                Assert.That(run.GenerateElite("r").Single().Definition.Ai, Is.EqualTo("wendigo"));
                Assert.That(run.GenerateEnemies("r").Select(e => e.Definition.Ai), Is.EqualTo(new[] { "abanderado", "barbaro" }));
                run.AdvanceFloor(); run.AdvanceFloor();
                Assert.That(run.GenerateBoss().First().Definition.Ai, Is.EqualTo("liche"));
                Tuning.Current.ForcedBoss = 7;
                Assert.That(run.GenerateBoss().Single().Definition.Ai, Is.EqualTo("nigromante"), "a floor-2 fight can be tested on floor 3");
                run.AdvanceFloor();
                Assert.That(run.GenerateBoss().Single().Definition.Ai, Is.EqualTo("crypt"), "floor 4 ignores the expansion indices");
            }
            finally { Tuning.Reset(); }
        }

        [Test] public void TheCapHoldsInLongFloorThreeFights()
        {
            foreach (var fight in new[] { EnemyCatalog.CreateBoss("relojero", 3), EnemyCatalog.CreateBoss("liche", 3), new List<EnemyState> { Three("comandante") },
                new List<EnemyState> { Three("ensamblador") }, EnemyCatalog.CreateGroup("taller", 3, "g") })
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
                Assert.That(summoner.SummonsMade, Is.LessThanOrEqualTo(EnemyCatalog.Profile(summoner.Definition.Ai).SummonBudget), summoner.Definition.Ai);
            }
        }

        [Test] public void PreviewsNeverTouchTheRealFight()
        {
            var party = EnemyCatalog.CreateBoss("liche", 3);
            var state = Fight(party.Concat(new[] { Three("barbaro", "b") }).ToList(), Many(Hit(9), 10));
            var copy = state.Clone();
            copy.SelectEnemy(2); copy.PlayCard(0); copy.PlayCard(0);
            Assert.That(state.Enemies[2].Statuses[StatusKind.Strength], Is.Zero);
            Assert.That(state.Enemies[2].RageStrength, Is.Zero);
            Assert.That(copy.Enemies[2].RageStrength, Is.EqualTo(2));
        }
    }
}
