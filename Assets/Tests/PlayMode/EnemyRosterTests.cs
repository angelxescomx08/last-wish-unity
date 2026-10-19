using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using EventType = LastWish.Presentation.EventType;
using Key = LastWish.Presentation.Key;

namespace LastWish.Tests
{
    public sealed class EnemyRosterTests
    {
        private static IEnumerator Fight(List<EnemyState> enemies, bool boss, string capture, int turns)
        {
            var manager = GameDriver.Manager;
            var run = new RunState(HeroKind.Mage, 42);
            manager.StartRun(run);
            var state = run.CreateCombat(enemies);
            var scene = new CombatScene(GameDriver.Game.Context, state, run.OwnedRelics, boss);
            manager.Push(scene);
            yield return GameDriver.Seconds(1.2f);
            foreach (var enemy in state.Enemies)
            {
                var sheet = GameDriver.Game.Context.Art.SheetForEnemy(enemy.Definition.Name);
                if (enemy.Definition.Ai != "crypt") Assert.That(sheet, Is.Not.Null, enemy.Definition.Name + " must have its own animated sheet");
            }
            Assert.That(scene.EnemyAnimators.Count, Is.EqualTo(state.Enemies.Count(e => e.Definition.Ai != "crypt")));
            if (capture != null) GameDriver.Capture(capture);
            for (var t = 0; t < turns && !state.Finished; t++)
            {
                GameDriver.Key(Key.E);
                yield return GameDriver.Seconds(1.6f);
            }
        }

        [UnityTest]
        public IEnumerator EveryEnemyFightsWithItsOwnSheet()
        {
            yield return GameDriver.Boot();
            foreach (var definition in EnemyCatalog.Definitions)
            {
                var enemy = EnemyCatalog.Create(definition.Ai, 1, "e0");
                var capture = definition.Boss || definition.Elite || definition.Ai == "golem" || definition.Ai == "espectro" ? "enemy-" + definition.Ai : null;
                yield return Fight(new List<EnemyState> { enemy }, definition.Boss, capture, 2);
            }
        }

        [UnityTest]
        public IEnumerator EveryPairFightsSideBySide()
        {
            yield return GameDriver.Boot();
            foreach (var pair in EnemyCatalog.Pairs)
                yield return Fight(EnemyCatalog.CreatePair(pair.Key, 1, "p"), false, "pair-" + pair.Key, 3);
        }
    }
}
