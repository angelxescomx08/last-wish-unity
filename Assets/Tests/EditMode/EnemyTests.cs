using System.Linq;
using System.Numerics;
using LastWish.Domain;
using NUnit.Framework;

namespace LastWish.Tests
{
    public sealed class EnemyTests
    {
        [Test] public void WraithHasTwoClawsThenWail()
        {
            var enemy = EnemyCatalog.Create("espectro", 1, "a");
            Assert.That(enemy.Intent.Id, Is.EqualTo("claw")); Assert.That(enemy.Intent.Value, Is.EqualTo(11));
            enemy.Plan(new StatusSet(), new[] { enemy }); Assert.That(enemy.Intent.Id, Is.EqualTo("claw"));
            enemy.Plan(new StatusSet(), new[] { enemy }); Assert.That(enemy.Intent.Id, Is.EqualTo("wail"));
            Assert.That(enemy.Intent.Debuffs.Single(s => s.Kind == StatusKind.Weak).Amount, Is.EqualTo(2));
        }
        [Test] public void TargetSelectionOnlyDamagesTheChosenEnemy()
        {
            var enemies = new[] { EnemyCatalog.Create("espectro", 1, "a"), EnemyCatalog.Create("babosa", 1, "b") };
            var battle = new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior), new[] { new CardDefinition("hit", "Golpe", 0, 6, 0) }, 42, 100, enemies);
            Assert.That(battle.SelectEnemy(2), Is.False); Assert.That(battle.SelectEnemy(-1), Is.False);
            Assert.That(battle.SelectEnemy(1), Is.True); battle.PlayCard(0);
            Assert.That(enemies[0].Hp, Is.EqualTo(42)); Assert.That(enemies[1].Hp, Is.EqualTo(36));
            Assert.That(battle.Victory, Is.False);
        }
        [Test] public void PartnerDeathImmediatelyReplacesIntentWithVengeanceOnce()
        {
            var enemies = EnemyCatalog.CreatePair("culto", 1, "pair");
            var battle = new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior), new[] { new CardDefinition("huge", "Golpe", 0, BigInteger.Pow(10, 1000), 0) }, 42, 100, enemies);
            battle.SelectEnemy(0); battle.PlayCard(0);
            Assert.That(battle.Victory, Is.False); Assert.That(enemies[1].Intent.Id, Is.EqualTo("vengeance"));
            Assert.That(battle.SelectedEnemy, Is.EqualTo(1));
            battle.EndTurn(); Assert.That(enemies[1].Statuses[StatusKind.Strength], Is.EqualTo(3));
            Assert.That(enemies[1].Intent.Id, Is.Not.EqualTo("vengeance"));
        }
        [Test] public void AllEnemiesMustBeDefeatedForVictory()
        {
            var enemies = new[] { EnemyCatalog.Create("espectro", 1, "a"), EnemyCatalog.Create("babosa", 1, "b") };
            var hit = new CardDefinition("huge", "Golpe", 0, BigInteger.Pow(10, 1000), 0);
            var battle = new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior), new[] { hit, hit }, 42, 100, enemies);
            battle.PlayCard(0); Assert.That(battle.Victory, Is.False);
            battle.PlayCard(0); Assert.That(battle.Victory, Is.True);
        }
        [Test] public void SteamConsumesElementsAndHitsThroughTheUsualBlockRules()
        {
            var enemy = EnemyCatalog.Create("espectro", 1, "a");
            var battle = new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior), new[] { new CardDefinition("wait", "Espera", 0, 0, 0) }, 42, 100, new[] { enemy });
            battle.ApplyElement(0, ElementKind.Fire, 3); battle.ApplyElement(0, ElementKind.Water, 2);
            Assert.That(enemy.Hp, Is.EqualTo(32)); Assert.That(enemy.Statuses[StatusKind.Fire], Is.Zero);
            Assert.That(enemy.Statuses[StatusKind.Water], Is.Zero);
        }
        [Test] public void FrozenRegularEnemyLosesItsAction()
        {
            var enemy = EnemyCatalog.Create("espectro", 1, "a");
            var battle = new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior), new[] { new CardDefinition("wait", "Espera", 0, 0, 0) }, 42, 100, new[] { enemy });
            battle.ApplyElement(0, ElementKind.Water, 2); battle.ApplyElement(0, ElementKind.Air, 1);
            battle.EndTurn(); Assert.That(battle.Hp, Is.EqualTo(100));
            Assert.That(enemy.Statuses[StatusKind.Frozen], Is.Zero);
        }
        [Test] public void StatusCardsAreTemporaryAndMoldDrainsManaWhenDrawn()
        {
            var battle = new CombatState(HeroDefinition.OriginalFor(HeroKind.Mage), new[] { CombatState.StatusCard("status_moho") }, 42);
            Assert.That(battle.Mana, Is.EqualTo(3)); Assert.That(battle.CanPlay(0), Is.False);
            battle.EndTurn(); Assert.That(battle.Hand.Count, Is.Zero); Assert.That(battle.ExhaustedCount, Is.EqualTo(1));
        }
        [TestCase(ElementKind.Fire, ElementKind.Water, ReactionKind.Steam)]
        [TestCase(ElementKind.Fire, ElementKind.Earth, ReactionKind.Magma)]
        [TestCase(ElementKind.Fire, ElementKind.Air, ReactionKind.Wildfire)]
        [TestCase(ElementKind.Water, ElementKind.Earth, ReactionKind.Mud)]
        [TestCase(ElementKind.Water, ElementKind.Air, ReactionKind.Freeze)]
        [TestCase(ElementKind.Earth, ElementKind.Air, ReactionKind.Sandstorm)]
        public void AllSixElementPairsMatchTheOriginalReactionTable(ElementKind first, ElementKind second, ReactionKind expected)
        {
            var enemy = EnemyCatalog.Create("espectro", 1, "a");
            var battle = new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior), new[] { new CardDefinition("wait", "Espera", 0, 0, 0) }, 42, 100, new[] { enemy });
            battle.ApplyElement(0, first, 3); Assert.That(battle.ApplyElement(0, second, 2), Is.True);
            Assert.That(battle.Reactions.Last().Kind, Is.EqualTo(expected));
            if (expected == ReactionKind.Mud) Assert.That(battle.Block, Is.EqualTo(15));
            if (expected == ReactionKind.Wildfire) Assert.That(enemy.Statuses[StatusKind.Fire], Is.EqualTo(5));
            if (expected == ReactionKind.Sandstorm) Assert.That(enemy.Statuses[StatusKind.Weak], Is.EqualTo(1));
            if (expected == ReactionKind.Magma) Assert.That(enemy.Statuses[StatusKind.Vulnerable], Is.EqualTo(2));
        }
    }
}
