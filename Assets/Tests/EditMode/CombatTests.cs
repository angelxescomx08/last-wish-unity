using System;
using System.Linq;
using System.Numerics;
using LastWish.Domain;
using NUnit.Framework;

namespace LastWish.Tests
{
    public sealed class CombatTests
    {
        private static CombatState Battle(params CardDefinition[] cards) =>
            new CombatState(HeroDefinition.For(HeroKind.Warrior), cards, 42);

        [Test] public void BigValueKeepsThousandDigitArithmeticExact()
        {
            var value = BigInteger.Pow(10, 1000);
            Assert.That(new BigValue(value).AddFlat(7).MultiplyBy(9).Value,
                Is.EqualTo((value + 7) * 9));
        }
        [Test] public void FlatBonusIsAppliedBeforeMultipliersRegardlessOfCallOrder() =>
            Assert.That(new BigValue(6).MultiplyBy(3).AddFlat(2).Value, Is.EqualTo(new BigInteger(24)));
        [TestCase(-1)] public void NegativeBaseIsRejected(int value) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new BigValue(value));
        [Test] public void ZeroMultiplierIsExact() =>
            Assert.That(new BigValue(99).MultiplyBy(0).Value, Is.EqualTo(BigInteger.Zero));
        [Test] public void OpeningHandHasFiveCardsAndFullMana()
        {
            var state = DemoBattle.Create(HeroKind.Warrior, 42);
            Assert.That(state.Hand.Count, Is.EqualTo(5));
            Assert.That(state.Mana, Is.EqualTo(state.Hero.MaxMana));
            Assert.That(state.DrawCount, Is.EqualTo(5));
        }
        [Test] public void SeedProducesTheSameOpeningHand()
        {
            Assert.That(DemoBattle.Create(HeroKind.Mage, 77).Hand.Select(c => c.Id),
                Is.EqualTo(DemoBattle.Create(HeroKind.Mage, 77).Hand.Select(c => c.Id)));
        }
        [Test] public void PlayingSpendsManaAndMovesCardToDiscard()
        {
            var state = Battle(new CardDefinition("hit", "Golpe", 1, 6, 0));
            Assert.That(state.PlayCard(0), Is.True);
            Assert.That(state.Mana, Is.EqualTo(2));
            Assert.That(state.Hand.Count, Is.Zero);
            Assert.That(state.DiscardCount, Is.EqualTo(1));
            Assert.That(state.EnemyHp, Is.EqualTo(36));
        }
        [Test] public void UnaffordableCardDoesNotMutateBattle()
        {
            var state = Battle(new CardDefinition("costly", "Costosa", 4, 9, 0));
            Assert.That(state.PlayCard(0), Is.False);
            Assert.That(state.Hand.Count, Is.EqualTo(1));
            Assert.That(state.Mana, Is.EqualTo(3));
            Assert.That(state.EnemyHp, Is.EqualTo(45));
        }
        [TestCase(-1)] [TestCase(1)] [TestCase(int.MaxValue)]
        public void InvalidHandIndexIsIgnored(int index) =>
            Assert.That(Battle(new CardDefinition("x", "Golpe", 1, 1, 0)).PlayCard(index), Is.False);
        [Test] public void BlockAbsorbsEnemyAttackBeforeHp()
        {
            var state = Battle(new CardDefinition("guard", "Defender", 1, 0, 10));
            state.PlayCard(0);
            state.EndTurn();
            Assert.That(state.Hp, Is.EqualTo(state.Hero.MaxHp));
            Assert.That(state.Block, Is.Zero);
            Assert.That(state.Mana, Is.EqualTo(3));
        }
        [Test] public void EndTurnRefillsHandByRecyclingDiscard()
        {
            var state = DemoBattle.Create(HeroKind.Rogue, 42);
            state.EndTurn();
            state.EndTurn();
            Assert.That(state.Hand.Count, Is.EqualTo(5));
            Assert.That(state.DrawCount + state.DiscardCount + state.Hand.Count, Is.EqualTo(10));
        }
        [Test] public void HugeHitWinsWithoutIntegerOverflow()
        {
            var state = Battle(new CardDefinition("huge", "Impacto", 1, BigInteger.Pow(10, 1000), 0));
            Assert.That(state.PlayCard(0), Is.True);
            Assert.That(state.Victory, Is.True);
            Assert.That(state.EnemyHp, Is.Zero);
            Assert.That(state.EndTurn(), Is.False);
        }
        [Test] public void DefeatPreventsFurtherActions()
        {
            var state = Battle(new CardDefinition("blank", "Espera", 0, 0, 0));
            for (var i = 0; i < 100 && !state.Finished; i++) state.EndTurn();
            Assert.That(state.Defeat, Is.True);
            Assert.That(state.Hp, Is.Zero);
            Assert.That(state.PlayCard(0), Is.False);
        }
        [Test] public void EmptyDeckIsRejected() =>
            Assert.Throws<ArgumentException>(() => Battle(Array.Empty<CardDefinition>()));
    }
}
