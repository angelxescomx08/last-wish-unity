using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;
using UnityEngine.TestTools;

using Key = LastWish.Presentation.Key;

namespace LastWish.Tests
{
    /// <summary>The floor-3 expansion on screen in Unity: every new enemy, group and boss fight with its sheet, summoners at the cap,
    /// and captures in TestResults/ for visual review.</summary>
    public sealed class Floor3EnemyTests
    {
        private static CombatScene scene;

        private static IEnumerator Fight(List<EnemyState> enemies, bool boss, string capture, int turns, bool captureEnd = false)
        {
            var manager = GameDriver.Manager;
            var run = new RunState(HeroKind.Rogue, 42, EnemyRoster.Expanded);
            manager.StartRun(run);
            var state = run.CreateCombat(enemies);
            state.Invincible = true;
            scene = new CombatScene(GameDriver.Game.Context, state, run.OwnedRelics, boss);
            manager.Push(scene);
            yield return GameDriver.Seconds(1.2f);
            foreach (var enemy in state.Enemies)
            {
                var sheet = GameDriver.Game.Context.Art.SheetForEnemy(enemy.Definition.Name);
                Assert.That(sheet, Is.Not.Null, enemy.Definition.Name + " must have its own animated sheet");
                Assert.That(EnemyAnimator.Styles.ContainsKey(sheet.Id), Is.True, sheet.Id + " needs a particle style");
                Assert.That(sheet.IsBoss, Is.EqualTo(EnemyField.IsLarge(enemy.Definition)), enemy.Definition.Name + " size");
            }
            Assert.That(scene.EnemyAnimators.Count, Is.EqualTo(state.Enemies.Count));
            if (capture != null && !captureEnd) GameDriver.Capture(capture);
            for (var t = 0; t < turns && !state.Finished; t++)
            {
                GameDriver.Key(Key.E);
                yield return GameDriver.Seconds(1.8f);
                Assert.That(state.Enemies.Count(e => e.Alive), Is.LessThanOrEqualTo(EnemyField.MaxAlive));
                Assert.That(scene.EnemyAnimators.Count, Is.EqualTo(scene.ListedEnemies().Count), "every arrival gets an animator");
            }
            if (capture != null && captureEnd) GameDriver.Capture(capture);
        }

        [UnityTest]
        public IEnumerator EveryFloorThreeEnemyFightsWithItsOwnSheet()
        {
            yield return GameDriver.Boot();
            foreach (var d in EnemyCatalog.Floor3Definitions)
                yield return Fight(new List<EnemyState> { EnemyCatalog.Create(d.Ai, 3, "e0") }, d.Boss, "f3-enemy-" + d.Ai, 3);
        }

        [UnityTest]
        public IEnumerator EveryFloorThreeGroupFightsSideBySide()
        {
            yield return GameDriver.Boot();
            foreach (var g in EnemyCatalog.Floor3Groups)
                yield return Fight(EnemyCatalog.CreateGroup(g.Key, 3, "g"), false, "f3-group-" + g.Key, 3);
        }

        [UnityTest]
        public IEnumerator FloorThreeBossFights()
        {
            yield return GameDriver.Boot();
            foreach (var b in EnemyCatalog.Floor3BossEncounters)
                yield return Fight(EnemyCatalog.CreateBoss(b.Key, 3), true, "f3-boss-" + b.Key, 4, captureEnd: true);
        }

        [UnityTest]
        public IEnumerator FloorThreeSummonersFillTheFieldUpToTheCap()
        {
            yield return GameDriver.Boot();
            yield return Fight(new List<EnemyState> { EnemyCatalog.Create("ensamblador", 3, "e0") }, false, "f3-summon-automata", 4, captureEnd: true);
            Assert.That(scene.State.Enemies.Count(e => e.Alive), Is.GreaterThanOrEqualTo(2));
            yield return Fight(EnemyCatalog.CreateBoss("relojero", 3), true, "f3-summon-clockwork", 2, captureEnd: true);
            Assert.That(scene.State.Enemies.Count(e => e.Alive), Is.EqualTo(3), "El Relojero and two Autómatas");
            Assert.That(scene.State.Enemies[0].Statuses[StatusKind.Spring], Is.GreaterThan(2), "the Autómatas wound it up");
            yield return Fight(EnemyCatalog.CreateBoss("liche", 3), true, "f3-boss-lich-and-phylactery", 1);
            Assert.That(scene.State.Enemies[0].Statuses[StatusKind.Undying], Is.EqualTo(1));
        }
    }
}
