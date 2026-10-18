using System.Linq;
using LastWish.Domain;
using NUnit.Framework;

namespace LastWish.Tests
{
    public sealed class EffectTests
    {
        private static CombatState Battle(CardDefinition card, params CardDefinition[] prefix)
        {
            var deck = prefix.Concat(new[] { card }).ToArray();
            return new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior), deck, 42, 100,
                new[] { EnemyCatalog.Create("espectro", 1, "a") });
        }
        [Test] public void ComboDamageAggregatesBeforeAddingHeroBonusOnce()
        {
            var card = new CardDefinition("combo", "Tajo", 1, 6, 0, effects: new[] {
                new EffectLayer(EffectCondition.Always, 6), new EffectLayer(EffectCondition.Combo, 5) });
            var battle = Battle(card, new CardDefinition("prefix", "Espera", 0, 0, 0));
            battle.PlayCard(battle.Hand.ToList().FindIndex(c => c.Id == "prefix")); battle.PlayCard(0);
            Assert.That(battle.EnemyHp, Is.EqualTo(27));
        }
        [Test] public void EndureBlockReceivesDexterityOnceForTheWholeCard()
        {
            var card = new CardDefinition("endure", "Muro", 1, 0, 5, effects: new[] {
                new EffectLayer(EffectCondition.Always, 0, 5), new EffectLayer(EffectCondition.Endure, 0, 5) });
            var battle = Battle(card, new CardDefinition("prefix", "Defensa", 0, 0, 1));
            battle.PlayCard(battle.Hand.ToList().FindIndex(c => c.Id == "prefix")); battle.PlayCard(0);
            Assert.That(battle.Block, Is.EqualTo(19));
        }
        [Test] public void VoidUsesManaAfterCostBeforeManaGain()
        {
            var card = new CardDefinition("void", "Vacío", 2, 0, 0, effects: new[] {
                new EffectLayer(EffectCondition.Always, 0, mana: 1), new EffectLayer(EffectCondition.Void, 12) });
            var battle = Battle(card); battle.PlayCard(0);
            Assert.That(battle.EnemyHp, Is.EqualTo(26)); Assert.That(battle.Mana, Is.EqualTo(1));
        }
        [Test] public void SingularIsDisabledByRepeatedStartingIds()
        {
            var card = new CardDefinition("singular", "Singular", 0, 0, 0, effects: new[] { new EffectLayer(EffectCondition.Singular, 9) });
            var unique = Battle(card); unique.PlayCard(0); Assert.That(unique.EnemyHp, Is.EqualTo(29));
            var repeated = Battle(card, card); repeated.PlayCard(0); Assert.That(repeated.EnemyHp, Is.EqualTo(42));
        }
        [Test] public void ReactionBonusIsDeterminedBeforeElementsAreConsumed()
        {
            var card = new CardDefinition("reaction", "Vapor", 1, 0, 0, effects: new[] {
                new EffectLayer(EffectCondition.Always, 0, element: ElementKind.Water, stacks: 3), new EffectLayer(EffectCondition.Reaction, 4) });
            var battle = Battle(card); battle.ApplyElement(0, ElementKind.Fire, 2); battle.PlayCard(0);
            Assert.That(battle.EnemyHp, Is.EqualTo(24)); Assert.That(battle.Reactions.Last().Kind, Is.EqualTo(ReactionKind.Steam));
        }
        [Test] public void EffectLayersSurviveCheckpointSerialization()
        {
            var run = new RunState(HeroKind.Mage, 42);
            run.AddCard(new CardDefinition("element", "Agua", 1, 0, 0, effects: new[] { new EffectLayer(EffectCondition.Always, 0, element: ElementKind.Water, stacks: 3) }));
            var restored = RunState.FromSave(run.CreateSave());
            Assert.That(restored.Deck.Last().Effects.Single().Element, Is.EqualTo(ElementKind.Water));
            Assert.That(restored.Deck.Last().Effects.Single().Stacks, Is.EqualTo(3));
        }
        [Test] public void UpgradingOnlyChangesTheBaseLayer()
        {
            var card = new CardDefinition("combo", "Tajo", 1, 6, 0, effects: new[] {
                new EffectLayer(EffectCondition.Always, 6), new EffectLayer(EffectCondition.Combo, 5) });
            var upgraded = card.Upgrade();
            Assert.That(upgraded.Effects[0].Damage, Is.EqualTo(new System.Numerics.BigInteger(9)));
            Assert.That(upgraded.Effects[1].Damage, Is.EqualTo(new System.Numerics.BigInteger(5)));
            Assert.That(card.Effects[0].Damage, Is.EqualTo(new System.Numerics.BigInteger(6)));
        }
    }
}
