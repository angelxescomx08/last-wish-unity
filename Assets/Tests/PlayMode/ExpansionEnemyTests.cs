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
    /// <summary>The floor-1 expansion on screen: every new enemy, group and boss fight with its sheet, summons at the cap,
    /// the revived Plañidera, and formations whose HUDs never overlap.</summary>
    public sealed class ExpansionEnemyTests
    {
        private static CombatScene scene;

        private static IEnumerator Fight(List<EnemyState> enemies, bool boss, string capture, int turns, bool captureEnd = false)
        {
            var manager = GameDriver.Manager;
            var run = new RunState(HeroKind.Warrior, 42, EnemyRoster.Expanded);
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
        public IEnumerator EveryNewEnemyFightsWithItsOwnSheet()
        {
            yield return GameDriver.Boot();
            foreach (var d in EnemyCatalog.ExpansionDefinitions)
                yield return Fight(new List<EnemyState> { EnemyCatalog.Create(d.Ai, 1, "e0") }, d.Boss, "x-enemy-" + d.Ai, 3);
        }

        [UnityTest]
        public IEnumerator EveryNewGroupFightsSideBySide()
        {
            yield return GameDriver.Boot();
            foreach (var g in EnemyCatalog.Groups)
                yield return Fight(EnemyCatalog.CreateGroup(g.Key, 1, "g"), false, "x-group-" + g.Key, 3);
        }

        [UnityTest]
        public IEnumerator NewBossFightsIncludingThePair()
        {
            yield return GameDriver.Boot();
            foreach (var key in new[] { "inquisidor", "sierpe", "lapida" })
                yield return Fight(EnemyCatalog.CreateBoss(key, 1), true, "x-boss-" + key, 4, captureEnd: true);
        }

        [UnityTest]
        public IEnumerator SummonersFillTheFieldUpToTheCap()
        {
            yield return GameDriver.Boot();
            yield return Fight(new List<EnemyState> { EnemyCatalog.Create("madre_larva", 1, "e0") }, false, "x-summon-larvae", 4, captureEnd: true);
            Assert.That(scene.State.Enemies.Count(e => e.Alive), Is.EqualTo(3));
            yield return Fight(new List<EnemyState> { EnemyCatalog.Create("rey_rata", 1, "e0") }, false, "x-summon-ratking", 2, captureEnd: true);
            Assert.That(scene.State.Enemies.Count(e => e.Alive), Is.EqualTo(3), "Rey Rata and two rats");
            yield return Fight(new List<EnemyState> { EnemyCatalog.Create("cieno", 1, "e0") }, false, null, 0);
            scene.State.Enemies[0].LoseHp(30);
            GameDriver.Key(Key.E); yield return GameDriver.Seconds(1.8f);
            GameDriver.Key(Key.E); yield return GameDriver.Seconds(2.2f);
            Assert.That(scene.State.Enemies.Count(e => e.Alive && e.Definition.Ai == "cieno_menor"), Is.EqualTo(2));
            GameDriver.Capture("x-summon-split");
        }

        [UnityTest]
        public IEnumerator TheGravediggerBringsHisSisterBack()
        {
            yield return GameDriver.Boot();
            yield return Fight(EnemyCatalog.CreateBoss("lapida", 1), true, null, 0);
            var state = scene.State;
            state.Enemies[1].LoseHp(state.Enemies[1].Hp);
            for (var t = 0; t < 3; t++) { GameDriver.Key(Key.E); yield return GameDriver.Seconds(2.2f); }
            Assert.That(state.Enemies.Count, Is.EqualTo(3));
            Assert.That(state.Enemies[2].Alive, Is.True);
            Assert.That(scene.EnemyAnimators.Count, Is.EqualTo(2));
            GameDriver.Capture("x-boss-lapida-revived");
        }

        [Test]
        public void FormationsKeepHudsApartAndClearOfTheCardInPlay()
        {
            const int left = 14, right = 35;          // badges stick out of the rects (block left, element right)
            const int cardRight = 636;               // the card in play spans x 484-636
            foreach (EnemyFormation.Kind kind in System.Enum.GetValues(typeof(EnemyFormation.Kind)))
            {
                var spans = EnemyFormation.PlacesOf(kind).Select(p => p.Large ? (p.X - p.W / 2 - left, p.X + p.W / 2 + right) : (p.X - left, p.X + 128 + right))
                    .OrderBy(s => s.Item1).ToList();
                for (var i = 1; i < spans.Count; i++) Assert.That(spans[i].Item1, Is.GreaterThanOrEqualTo(spans[i - 1].Item2), kind + " overlap");
                if (kind == EnemyFormation.Kind.Line3 || kind == EnemyFormation.Kind.Boss1) continue;   // original coordinates, unchanged
                Assert.That(spans.Last().Item2, Is.LessThanOrEqualTo(1280), kind + " off screen");
                Assert.That(spans.First().Item1 + left, Is.GreaterThan(cardRight), kind + " under the card in play");
            }
        }
    }
}
