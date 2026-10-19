using System;
using System.Linq;
using LastWish.Domain;
using NUnit.Framework;

namespace LastWish.Tests
{
    public sealed class RunTests
    {
        [SetUp] public void ResetTuning() => Tuning.Reset();
        [TearDown] public void RestoreTuning() => Tuning.Reset();

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
            }
        }
        [Test] public void EnteringARoomUnlocksItsNeighboursLikeThePythonMap()
        {
            var run = new RunState(HeroKind.Warrior, 42);
            Assert.That(run.EnterRoom(run.Map.BossId), Is.Null);
            Assert.That(run.EnterRoom("missing"), Is.Null);
            var entry = run.Map.Nodes.Single(n => n.Available);
            Assert.That(run.EnterRoom(entry.Id), Is.Not.Null); Assert.That(run.EnterRoom(entry.Id), Is.Null);
            Assert.That(run.Map.AvailableNodes().Select(n => n.Id), Is.EquivalentTo(entry.Connections));
        }
        [Test] public void PurgeKeepsLastCardAndPriceGrows()
        {
            var run = new RunState(HeroKind.Mage, 42);
            Assert.That(run.PurgePrice, Is.EqualTo(75));
            Assert.That(run.PurgeCard(0), Is.Not.Null); Assert.That(run.PurgePrice, Is.EqualTo(100));
            while (run.Deck.Count > 1) { run.GainGold(1000); Assert.That(run.PurgeCard(0), Is.Not.Null); }
            var gold = run.Gold; Assert.That(run.PurgeCard(0), Is.Null); Assert.That(run.Gold, Is.EqualTo(gold));
            Assert.That(run.PurgeBlockReason(), Does.Contain("última carta"));
        }
        [Test] public void UpgradeOnlyChangesChosenCardAndHonorsCap()
        {
            var run = new RunState(HeroKind.Warrior, 42);
            var original = run.Deck[0]; var other = run.Deck[1];
            Assert.That(run.UpgradePrice(original), Is.EqualTo(50));
            Assert.That(run.BuyUpgrade(0), Is.True);
            Assert.That(run.Deck[0].Damage.Value, Is.EqualTo(original.Damage.Value + 3));
            Assert.That(run.Deck[0].Name, Is.EqualTo(original.Name + "+"));
            Assert.That(run.Deck[1], Is.SameAs(other));
            var gold = run.Gold; Assert.That(run.BuyUpgrade(0), Is.False); Assert.That(run.Gold, Is.EqualTo(gold));
        }
        [TestCase(PullKind.Normal, 0, 80)] [TestCase(PullKind.Stellar, 0, 190)] [TestCase(PullKind.Normal, 1, 120)] [TestCase(PullKind.Stellar, 2, 430)]
        public void GachaPricesMatchPython(PullKind kind, int pulls, int expected) =>
            Assert.That(RunState.PullPrice(kind, pulls), Is.EqualTo(expected));
        [Test] public void AdvancingKeepsDeckAndHp()
        {
            var run = new RunState(HeroKind.Rogue, 42);
            var hp = run.Hp; var deck = run.Deck.ToArray();
            run.AdvanceFloor();
            Assert.That(run.Floor, Is.EqualTo(2)); Assert.That(run.Hp, Is.EqualTo(hp)); Assert.That(run.Deck, Is.EqualTo(deck));
            Assert.That(run.Map.Floor, Is.EqualTo(2));
        }
        [Test] public void InvalidValuesDoNotMutateRun()
        {
            var run = new RunState(HeroKind.Warrior, 42); var gold = run.Gold;
            Assert.That(run.GainGold(-1), Is.Zero);
            Assert.That(run.PurgeCard(-1), Is.Null); Assert.That(run.BuyUpgrade(int.MaxValue), Is.False);
            Assert.That(run.Spend(-1), Is.False); Assert.That(run.Gold, Is.EqualTo(gold));
        }
        [Test] public void StartingGoldAndHpFollowPruebas()
        {
            Tuning.Current.StartingGold = 77; Tuning.Current.ExtraMaxHp = 5;
            var run = new RunState(HeroKind.Warrior, 1);
            Assert.That(run.Gold, Is.EqualTo(77)); Assert.That(run.Hp, Is.EqualTo(105)); Assert.That(run.MaxHp, Is.EqualTo(105));
        }
        [Test] public void CheckpointRoundTripPreservesEconomyDeckRelicsAndMap()
        {
            var run = new RunState(HeroKind.Mage, 71);
            run.BuyUpgrade(0); run.AcquireRelic("iron_heart"); run.GainGold(91);
            var entry = run.Map.Nodes.Single(n => n.Available);
            run.EnterRoom(entry.Id);
            var restored = RunState.FromSave(run.CreateSave());
            Assert.That(restored.Gold, Is.EqualTo(run.Gold)); Assert.That(restored.Hp, Is.EqualTo(run.Hp));
            Assert.That(restored.MaxHp, Is.EqualTo(run.MaxHp)); Assert.That(restored.Deck[0].Upgraded, Is.True);
            Assert.That(restored.Deck[0].Damage.Value, Is.EqualTo(run.Deck[0].Damage.Value));
            Assert.That(restored.Map.Nodes.Select(n => n.Available), Is.EqualTo(run.Map.Nodes.Select(n => n.Available)));
            Assert.That(restored.Map.Nodes.Select(n => n.Visited), Is.EqualTo(run.Map.Nodes.Select(n => n.Visited)));
            Assert.That(restored.EnterRoom(entry.Id), Is.Null);
        }
        [Test] public void InvalidCheckpointIsRejected()
        {
            Assert.Throws<ArgumentException>(() => RunState.FromSave(new RunSave()));
            var run = new RunState(HeroKind.Rogue, 17); var save = run.CreateSave(); save.gold = -1;
            Assert.Throws<ArgumentException>(() => RunState.FromSave(save));
        }
        [TestCase(HeroKind.Warrior, 100, 2, 10)] [TestCase(HeroKind.Mage, 60, 4, 10)] [TestCase(HeroKind.Rogue, 70, 3, 9)]
        public void OriginalHeroesHaveTheirRealStatsAndStarterDecks(HeroKind kind, int hp, int mana, int count)
        {
            var run = new RunState(kind, 42);
            Assert.That(run.Hp, Is.EqualTo(hp)); Assert.That(run.Hero.MaxMana, Is.EqualTo(mana));
            Assert.That(run.Deck.Count, Is.EqualTo(count));
        }
        [Test] public void VictoryPaysGoldHealsAndCountsInterest()
        {
            var run = new RunState(HeroKind.Warrior, 42);
            run.AcquireRelic("blood_potion"); run.AcquireRelic("compound_interest");
            var enemies = run.GenerateEnemies(run.Map.Nodes[0].Id);
            var expected = run.CombatGold(enemies);
            var gold = run.Gold;
            Assert.That(run.ApplyCombatVictory(50, enemies), Is.EqualTo(expected));
            Assert.That(run.Hp, Is.EqualTo(58));
            Assert.That(run.Gold, Is.EqualTo(gold + expected + (gold + expected) / 10));
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
        [Test] public void InvinciblePruebasHeroNeverLosesHp()
        {
            var run = new RunState(HeroKind.Mage, 3);
            Tuning.Current.Invincible = true; Tuning.Current.ForcedEncounter = 5;
            var battle = run.CreateCombat(run.GenerateEnemies("x"));
            for (var turn = 0; turn < 6; turn++) battle.EndTurn();
            Assert.That(battle.Hp, Is.EqualTo(run.Hp));
        }
    }
}
