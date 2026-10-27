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
    /// <summary>The floor-2 expansion on screen in Unity: every new enemy, group and boss fight with its sheet, summoners at the cap,
    /// and captures in TestResults/ for visual review.</summary>
    public sealed class Floor2EnemyTests
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
        public IEnumerator EveryFloorTwoEnemyFightsWithItsOwnSheet()
        {
            yield return GameDriver.Boot();
            foreach (var d in EnemyCatalog.Floor2Definitions)
                yield return Fight(new List<EnemyState> { EnemyCatalog.Create(d.Ai, 2, "e0") }, d.Boss, "f2-enemy-" + d.Ai, 3);
        }

        [UnityTest]
        public IEnumerator EveryFloorTwoGroupFightsSideBySide()
        {
            yield return GameDriver.Boot();
            foreach (var g in EnemyCatalog.Floor2Groups)
                yield return Fight(EnemyCatalog.CreateGroup(g.Key, 2, "g"), false, "f2-group-" + g.Key, 3);
        }

        [UnityTest]
        public IEnumerator FloorTwoBossFights()
        {
            yield return GameDriver.Boot();
            foreach (var b in EnemyCatalog.Floor2BossEncounters)
                yield return Fight(EnemyCatalog.CreateBoss(b.Key, 2), true, "f2-boss-" + b.Key, 4, captureEnd: true);
        }

        [UnityTest]
        public IEnumerator FloorTwoSummonersFillTheFieldUpToTheCap()
        {
            yield return GameDriver.Boot();
            yield return Fight(new List<EnemyState> { EnemyCatalog.Create("ilusionista", 2, "e0") }, false, "f2-summon-illusions", 4, captureEnd: true);
            Assert.That(scene.State.Enemies.Count(e => e.Alive), Is.GreaterThanOrEqualTo(2));
            yield return Fight(EnemyCatalog.CreateBoss("nigromante", 2), true, "f2-summon-skeletons", 2, captureEnd: true);
            Assert.That(scene.State.Enemies.Count(e => e.Alive), Is.EqualTo(3), "El Nigromante and two skeletons");
        }
    }
}
