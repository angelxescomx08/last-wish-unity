using System;
using System.Linq;
using LastWish.Domain;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>Values the combat screen reads from the rules: previews, card identity, statuses shown as badges.</summary>
    public sealed class CombatViewTests
    {
        private static CombatState Fight(int seed, params string[] relicTags)
        {
            var hero = HeroDefinition.OriginalFor(HeroKind.Warrior);
            var deck = Enumerable.Range(0, 14).Select(i => i % 3 == 0 ? new CardDefinition("draw" + i, "Robo", 0, 2, 0, draw: 2) : new CardDefinition("hit" + i, "Golpe", 1, 6, 0)).ToList();
            var enemies = new[] { EnemyCatalog.Create("espectro", 1, "a"), EnemyCatalog.Create("golem", 1, "b") };
            var relics = relicTags.Select(t => new OwnedRelic(RunContent.RelicByTag(t))).ToList();
            return new CombatState(hero, deck, seed, -1, enemies, 0, relics);
        }

        private static string Snapshot(CombatState s) =>
            string.Join(",", s.Hand.Select(c => c.Id)) + "|" + string.Join(",", s.DrawPile.Select(c => c.Id)) + "|" + string.Join(",", s.Enemies.Select(e => e.Hp + "/" + e.Block + "/" + e.Intent.Id)) + "|" + s.Hp + "/" + s.Mana + "/" + s.Turn;

        [Test] public void SeededRandomRepeatsSystemRandom()
        {
            foreach (var seed in new[] { 0, 1, -7, 42, int.MinValue, int.MaxValue })
            {
                var a = new Random(seed); var b = new SeededRandom(seed);
                for (var i = 0; i < 2000; i++)
                {
                    Assert.That(b.Next(), Is.EqualTo(a.Next()));
                    Assert.That(b.Next(17), Is.EqualTo(a.Next(17)));
                    Assert.That(b.Next(-5, 9), Is.EqualTo(a.Next(-5, 9)));
                    Assert.That(b.NextDouble(), Is.EqualTo(a.NextDouble()));
                }
            }
        }

        [Test] public void APreviewUsesTheSameRandomStreamAndLeavesTheFightAlone()
        {
            var real = Fight(7);
            var before = Snapshot(real);
            var copy = real.Clone();
            Assert.That(copy.Play(0, 0).Success, Is.True);
            Assert.That(Snapshot(real), Is.EqualTo(before), "Playing on the copy must not touch the fight");
            Assert.That(real.Play(0, 0).Success, Is.True);
            Assert.That(Snapshot(copy), Is.EqualTo(Snapshot(real)), "The copy plays exactly like the real fight");
            copy.EndTurn(); real.EndTurn();
            Assert.That(Snapshot(copy), Is.EqualTo(Snapshot(real)), "…including the shuffles and draws after it");
        }

        [Test] public void EveryCardOfAFightIsItsOwnObject()
        {
            var card = new CardDefinition("same", "Golpe", 1, 6, 0);
            var state = new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior), Enumerable.Repeat(card, 8), 3, -1, new[] { EnemyCatalog.Create("espectro", 1, "a") });
            Assert.That(state.Hand.Distinct().Count(), Is.EqualTo(state.Hand.Count));
            Assert.That(state.Hand.All(c => !ReferenceEquals(c, card)));
        }

        [Test] public void HeroFuerzaIsAlsoAStatusBadge()
        {
            var state = Fight(5, "scar");
            Assert.That(state.Strength, Is.EqualTo(1));
            Assert.That(state.HeroStatuses[StatusKind.Strength], Is.EqualTo(1));
            Assert.That(state.HeroStatuses.Active, Has.Member(StatusKind.Strength));
        }

        [Test] public void BrokenChainMakesEnemyJunkFade()
        {
            foreach (var withChain in new[] { false, true })
            {
                var state = withChain ? Fight(9, "broken_chain") : Fight(9);
                var spawn = typeof(CombatState).GetMethod("SpawnStatusCards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                for (var i = state.Hand.Count - 1; i >= 0; i--) if (state.CanPlay(i)) state.Play(i, 0);
                spawn.Invoke(state, new object[] { new StatusCardSpawn("status_espora", 1, "hand") });
                spawn.Invoke(state, new object[] { new StatusCardSpawn("status_herida", 1, "hand") });
                state.EndTurn();
                Assert.That(state.ExhaustPile.Any(c => c.Id == "status_espora"), Is.EqualTo(withChain));
                Assert.That(state.ExhaustPile.Any(c => c.Id == "status_herida"), Is.False, "Herida never fades");
            }
        }
    }
}
