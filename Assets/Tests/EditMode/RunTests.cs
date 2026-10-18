using System;
using System.Linq;
using LastWish.Domain;
using NUnit.Framework;

namespace LastWish.Tests
{
    public sealed class RunTests
    {
        [TestCase(1, 7, 5)] [TestCase(3, 8, 5)] [TestCase(30, 12, 8)]
        public void MapDimensionsMatchPython(int floor, int rows, int cols)
        {
            var map = RunMap.Generate(42, floor);
            Assert.That(map.Rows, Is.EqualTo(rows)); Assert.That(map.Columns, Is.EqualTo(cols));
        }
        [Test] public void MapsAreRepeatableAndHaveOnlyOrthogonalEdges()
        {
            for (var seed = 0; seed < 100; seed++)
            {
                var map = RunMap.Generate(seed, 1);
                Assert.That(map.Nodes.Select(n => n.Id + n.Kind), Is.EqualTo(RunMap.Generate(seed, 1).Nodes.Select(n => n.Id + n.Kind)));
                foreach (var node in map.Nodes)
                    foreach (var next in node.Connections.Select(map.Find))
                        Assert.That(Math.Abs(node.Row - next.Row) + Math.Abs(node.Column - next.Column), Is.EqualTo(1));
                Assert.That(map.Nodes.Count(n => n.Available), Is.EqualTo(1));
                Assert.That(map.Nodes.Count(n => n.Kind == RoomKind.Boss), Is.EqualTo(1));
                Assert.That(map.Nodes.Count(n => n.Kind == RoomKind.Elite), Is.EqualTo(1));
                Assert.That(map.CanReachBoss(), Is.True);
            }
        }
        [Test] public void RoomCannotBeReenteredOrEnteredBeforeUnlocked()
        {
            var run = new RunState(HeroKind.Warrior, 42);
            Assert.That(run.Enter(run.Map.BossId), Is.False);
            Assert.That(run.Enter("missing"), Is.False);
            var entry = run.Map.Nodes.Single(n => n.Available);
            Assert.That(run.Enter(entry.Id), Is.True); Assert.That(run.Enter(entry.Id), Is.False);
            Assert.That(run.Map.Nodes.Any(n => n.Available && !n.Visited), Is.False, "Room must be completed first");
            run.CompleteRoom(); Assert.That(run.Map.Nodes.Any(n => n.Available && !n.Visited), Is.True);
        }
        [Test] public void BuyingIsAtomicAndSoldOffersCannotBeBoughtTwice()
        {
            var run = new RunState(HeroKind.Warrior, 42);
            var before = run.Gold;
            Assert.That(run.BuyCard("offer", new CardDefinition("x", "Golpe", 1, 6, 0), before + 1), Is.False);
            Assert.That(run.Gold, Is.EqualTo(before));
            Assert.That(run.BuyCard("offer", new CardDefinition("x", "Golpe", 1, 6, 0), 50), Is.True);
            Assert.That(run.BuyCard("offer", new CardDefinition("x", "Golpe", 1, 6, 0), 50), Is.False);
            Assert.That(run.Gold, Is.EqualTo(before - 50));
        }
        [Test] public void PurgeKeepsLastCardAndPriceGrows()
        {
            var run = new RunState(HeroKind.Mage, 42);
            Assert.That(run.PurgePrice, Is.EqualTo(75));
            Assert.That(run.Purge(0), Is.True); Assert.That(run.PurgePrice, Is.EqualTo(100));
            while (run.Deck.Count > 1) { run.GainGold(1000); Assert.That(run.Purge(0), Is.True); }
            var gold = run.Gold; Assert.That(run.Purge(0), Is.False); Assert.That(run.Gold, Is.EqualTo(gold));
        }
        [Test] public void UpgradeOnlyChangesChosenCardAndHonorsCap()
        {
            var run = new RunState(HeroKind.Warrior, 42);
            var original = run.Deck[0]; var other = run.Deck[1];
            Assert.That(run.Upgrade(0), Is.True);
            Assert.That(run.Deck[0].Damage.Value, Is.EqualTo(original.Damage.Value + 3));
            Assert.That(run.Deck[1], Is.SameAs(other));
            var gold = run.Gold; Assert.That(run.Upgrade(0), Is.False); Assert.That(run.Gold, Is.EqualTo(gold));
        }
        [TestCase(false, 0, 80)] [TestCase(true, 0, 190)] [TestCase(false, 1, 120)] [TestCase(true, 2, 430)]
        public void GachaPricesMatchPython(bool stellar, int pulls, int expected) =>
            Assert.That(RunState.PullPrice(stellar, pulls), Is.EqualTo(expected));
        [Test] public void BossCompletionAdvancesOnceAndKeepsDeckAndHp()
        {
            var run = new RunState(HeroKind.Rogue, 42);
            Assert.That(run.AdvanceFloor(), Is.False);
            run.RestoreProgress(run.Map.BossId, new string[0]);
            var hp = run.Hp; var deck = run.Deck.ToArray();
            Assert.That(run.AdvanceFloor(), Is.True); Assert.That(run.AdvanceFloor(), Is.False);
            Assert.That(run.Floor, Is.EqualTo(2)); Assert.That(run.Hp, Is.EqualTo(hp)); Assert.That(run.Deck, Is.EqualTo(deck));
        }
        [Test] public void InvalidValuesDoNotMutateRun()
        {
            var run = new RunState(HeroKind.Warrior, 42); var gold = run.Gold;
            Assert.Throws<ArgumentOutOfRangeException>(() => run.GainGold(-1));
            Assert.That(run.Purge(-1), Is.False); Assert.That(run.Upgrade(int.MaxValue), Is.False);
            Assert.That(run.BuyCard("negative", run.Deck[0], -1), Is.False); Assert.That(run.Gold, Is.EqualTo(gold));
        }
        [Test] public void CheckpointRoundTripPreservesEconomyDeckRelicsAndMap()
        {
            var run = new RunState(HeroKind.Mage, 71);
            run.Upgrade(0); run.AcquireRelic("iron"); run.GainGold(91);
            var entry = run.Map.Nodes.Single(n => n.Available);
            run.Enter(entry.Id); run.CompleteRoom();
            var restored = RunState.FromSave(run.CreateSave());
            Assert.That(restored.Gold, Is.EqualTo(run.Gold)); Assert.That(restored.Hp, Is.EqualTo(run.Hp));
            Assert.That(restored.MaxHp, Is.EqualTo(run.MaxHp)); Assert.That(restored.Deck[0].Upgraded, Is.True);
            Assert.That(restored.Deck[0].Damage.Value, Is.EqualTo(run.Deck[0].Damage.Value));
            Assert.That(restored.Map.Nodes.Select(n => n.Available), Is.EqualTo(run.Map.Nodes.Select(n => n.Available)));
            Assert.That(restored.Enter(entry.Id), Is.False);
        }
        [Test] public void InvalidCheckpointIsRejectedAndMidRoomCannotBeSaved()
        {
            Assert.Throws<ArgumentException>(() => RunState.FromSave(new RunSave()));
            var run = new RunState(HeroKind.Rogue, 17); var save = run.CreateSave(); save.gold = -1;
            Assert.Throws<ArgumentException>(() => RunState.FromSave(save));
            run.Enter(run.Map.Nodes.Single(n => n.Available).Id);
            Assert.Throws<InvalidOperationException>(() => run.CreateSave());
        }
        [TestCase(HeroKind.Warrior, 100, 2, 10)] [TestCase(HeroKind.Mage, 60, 4, 10)] [TestCase(HeroKind.Rogue, 70, 3, 9)]
        public void OriginalHeroesHaveTheirRealStatsAndStarterDecks(HeroKind kind, int hp, int mana, int count)
        {
            var run = new RunState(kind, 42);
            Assert.That(run.Hp, Is.EqualTo(hp)); Assert.That(run.Hero.MaxMana, Is.EqualTo(mana));
            Assert.That(run.Deck.Count, Is.EqualTo(count));
        }
        [Test] public void RogueComboNeedsAnEarlierCard()
        {
            var hero = HeroDefinition.OriginalFor(HeroKind.Rogue);
            var combo = RunContent.StarterDeck(HeroKind.Rogue).Last();
            var battle = new CombatState(hero, new[] { combo }, 42);
            battle.PlayCard(0); Assert.That(battle.Block, Is.Zero);
            battle = new CombatState(hero, new[] { combo, new CardDefinition("zero", "Espera", 0, 0, 0) }, 42);
            battle.PlayCard(battle.Hand.ToList().FindIndex(c => c.Id == "zero"));
            battle.PlayCard(0); Assert.That(battle.Block, Is.EqualTo(6));
        }
        [Test] public void WarriorEndureChecksBlockBeforePlayingTheCard()
        {
            var guard = RunContent.StarterDeck(HeroKind.Warrior).Last();
            var battle = new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior), new[] { guard, guard }, 42);
            battle.PlayCard(0); Assert.That(battle.Strength, Is.Zero);
            battle.PlayCard(0); Assert.That(battle.Strength, Is.EqualTo(1));
        }
        [Test] public void MageFirePersistsAndDamagesAfterEnemyActs()
        {
            var spark = RunContent.StarterDeck(HeroKind.Mage).Single(c => c.Id == "chispa_ignea_base");
            var battle = new CombatState(HeroDefinition.OriginalFor(HeroKind.Mage), new[] { spark }, 42);
            battle.PlayCard(0); Assert.That(battle.Fire, Is.EqualTo(2));
            var hp = battle.EnemyHp; battle.EndTurn(); Assert.That(battle.EnemyHp, Is.EqualTo(hp - 2));
            Assert.That(battle.Fire, Is.EqualTo(2));
        }
    }
}
