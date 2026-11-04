using System;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    public sealed class EventTests
    {
        [SetUp] public void Reset() => Tuning.Reset();
        [TearDown] public void Restore() => Tuning.Reset();
        private static RunState Run(long seed = 42) => new RunState(HeroKind.Warrior, seed);

        [Test] public void CatalogHasOneHundredFiftyUniqueEventsWithPendingArtAndExitChoices()
        {
            Assert.That(EventCatalog.All.Count, Is.EqualTo(150));
            Assert.That(EventCatalog.All.Select(e => e.Id).Distinct().Count(), Is.EqualTo(150));
            foreach (var e in EventCatalog.All)
            {
                Assert.That(e.ArtStatus, Is.EqualTo("Pendiente"));
                Assert.That(e.ArtKey, Is.Empty);
                Assert.That(e.Rarity, Is.InRange(1, 5));
                foreach (var stage in e.Stages)
                    Assert.That(stage.Choices.Any(c => c.IsExit), Is.True, e.Id);
                foreach (var c in e.Stages.SelectMany(s => s.Choices))
                    if (c.NextEventId.Length > 0) Assert.That(EventCatalog.Find(c.NextEventId), Is.Not.Null);
            }
        }

        [Test] public void SameSeedAndRoomReturnSameEventAndSessionWithoutRerolls()
        {
            var a = Run(); var b = Run();
            var first = a.OpenEvent("room");
            Assert.That(first.Definition.Id, Is.EqualTo(b.OpenEvent("room").Definition.Id));
            Assert.That(a.OpenEvent("room"), Is.SameAs(first));
            first.Choose(first.Stage.Choices.Count - 1);
            Assert.That(a.OpenEvent("room"), Is.SameAs(first));
            Assert.That(first.Choose(0), Is.False);
        }

        [Test] public void PurchasesAreAtomicAndCannotBeRepeated()
        {
            var run = Run(); run.Spend(run.Gold);
            var session = run.OpenEvent("sale", "bottle_vendor");
            var hp = run.Hp;
            Assert.That(session.Choose(0), Is.False);
            Assert.That(run.Hp, Is.EqualTo(hp)); Assert.That(session.Completed, Is.False);
            run.GainGold(100);
            Assert.That(session.Choose(0), Is.True);
            Assert.That(run.Gold, Is.EqualTo(70));
            Assert.That(session.Choose(0), Is.False); Assert.That(run.Gold, Is.EqualTo(70));
        }

        [Test] public void SacrificeCannotKillAndDoesNotPartiallyPay()
        {
            var run = Run(); var session = run.OpenEvent("blood", "blood_altar");
            run.ApplyCombatVictory(8, Array.Empty<EnemyState>());
            var gold = run.Gold;
            Assert.That(session.Choose(0), Is.False);
            Assert.That(run.Hp, Is.EqualTo(8)); Assert.That(run.Gold, Is.EqualTo(gold));
            run.Heal(1); Assert.That(session.Choose(0), Is.True);
            Assert.That(run.Hp, Is.EqualTo(1)); Assert.That(run.Gold, Is.EqualTo(gold + 55));
        }

        [Test] public void ChainOnlyUnlocksOnAcceptedChoiceAndWaitsForItsFloor()
        {
            var run = Run();
            Assert.Throws<InvalidOperationException>(() => run.OpenEvent("locked", "lantern_ferryman"));
            var first = run.OpenEvent("start", "lost_lantern"); first.Choose(0);
            Assert.That(run.EventFlags.Contains("lantern_found"), Is.True);
            Assert.Throws<InvalidOperationException>(() => run.OpenEvent("early", "lantern_ferryman"));
            run.AdvanceFloor();
            var next = run.OpenEvent("river");
            Assert.That(next.Definition.Id, Is.EqualTo("lantern_ferryman"));
            next.Choose(next.Stage.Choices.Count - 1);
            Assert.That(run.OpenEvent("later").Definition.Id, Is.Not.EqualTo("lantern_ferryman"));
            Assert.That(run.EventHistory.Count, Is.EqualTo(2));
            Assert.That(Run().EventFlags, Is.Empty);
        }

        [Test] public void MultiStageMerchantChargesOnceAndHasDifferentChoicesAfterwards()
        {
            var run = Run(); var session = run.OpenEvent("tent", "masked_bazaar");
            var gold = run.Gold; Assert.That(session.Choose(0), Is.True);
            Assert.That(session.Completed, Is.False); Assert.That(session.StageIndex, Is.EqualTo(1));
            Assert.That(run.Gold, Is.EqualTo(gold - 20));
            Assert.That(session.Choose(0), Is.True); Assert.That(session.Completed, Is.True);
            Assert.That(run.Gold, Is.EqualTo(gold - 50));
        }

        [Test] public void RandomOutcomesAreDeterministicAndInvalidInputDoesNotConsumeThem()
        {
            var a = Run(); var b = Run();
            var x = a.OpenEvent("well", "whispering_well"); var y = b.OpenEvent("well", "whispering_well");
            Assert.That(x.Choose(-1), Is.False); Assert.That(x.Choose(99), Is.False);
            x.Choose(0); y.Choose(0);
            Assert.That(a.Gold, Is.EqualTo(b.Gold)); Assert.That(a.Hp, Is.EqualTo(b.Hp));
            Assert.That(x.Message, Is.EqualTo(y.Message));
        }

        [Test] public void GoldRewardsUseExistingInterestMechanic()
        {
            var run = Run(); run.AcquireRelic("compound_interest");
            var gold = run.Gold; var session = run.OpenEvent("coins", "scattered_coins");
            session.Choose(0);
            Assert.That(run.Gold, Is.EqualTo(gold + 22 + (gold + 22) / 10));
        }

        [Test] public void ChoicesCannotEmptyDeckOrSellOwnedRelicsAgain()
        {
            var run = Run(); run.AdvanceFloor(); var altar = run.OpenEvent("forget", "forgetting_pool");
            while (run.Deck.Count > 1) { run.GainGold(10000); run.PurgeCard(0); }
            Assert.That(altar.Choose(0), Is.False); Assert.That(run.Deck.Count, Is.EqualTo(1));
            run.AcquireRelic("rabbit_foot");
            var merchant = run.OpenEvent("seller", "relic_peddler");
            Assert.That(merchant.Choose(0), Is.False);
        }

        [Test] public void PermanentVitalitySacrificeSurvivesOtherMaxHpRewards()
        {
            var run = Run(); run.AdvanceFloor();
            var max = run.MaxHp; var surgeon = run.OpenEvent("surgeon", "red_surgeon");
            Assert.That(surgeon.Choose(0), Is.True); Assert.That(run.MaxHp, Is.EqualTo(max - 5));
            run.AcquireRelic("iron_heart");
            Assert.That(run.MaxHp, Is.EqualTo(max + 10));
            Assert.That(run.Hp, Is.LessThanOrEqualTo(run.MaxHp));
        }

        [Test] public void HeroSheetAndCombatIncludeEventVitalityAndLuckChanges()
        {
            var render = new NullRender(); var art = new Art(render, new FileAssets()); art.LoadCore();
            var game = new GameContext(new Canvas(render), art, new SoundPlayer(null), new Preferences(), null)
                { Data = new GameData(new FileAssets()) };
            var run = Run();
            run.OpenEvent("spring", "root_spring").Choose(0);
            run.OpenEvent("luck", "spore_garden").Choose(0);
            run.AdvanceFloor(); run.OpenEvent("surgeon", "red_surgeon").Choose(0);
            var sheet = HeroSheetData.For(game, run);
            Assert.That(sheet.Stat("hp").Value, Is.EqualTo(run.MaxHp));
            Assert.That(sheet.Stat("luck").Value, Is.EqualTo(run.Luck));
            Assert.That(sheet.Stat("hp").Sources.Sum(s => s.Amount) + run.Hero.MaxHp, Is.EqualTo(run.MaxHp));
            Assert.That(sheet.Stat("luck").Sources.Sum(s => s.Amount) + run.Hero.Luck, Is.EqualTo(run.Luck));
            var combat = run.CreateCombat(run.GenerateEnemies("fight"));
            Assert.That(combat.Hero.MaxHp, Is.EqualTo(run.MaxHp));
            Assert.That(combat.Hero.Luck, Is.EqualTo(run.Luck));
        }

        [Test] public void AnOpenStoryNamesItsNextChapterAndWaitsForItsFloor()
        {
            var run = Run();
            Assert.That(run.PendingStories, Is.Empty); Assert.That(run.NextStory, Is.Null);
            run.OpenEvent("start", "lost_lantern").Choose(0);
            var story = run.PendingStories.Single();
            Assert.That(story.EventId, Is.EqualTo("lantern_ferryman"));
            Assert.That(story.StoryName, Is.EqualTo("La linterna extraviada"));
            Assert.That(story.FromName, Is.EqualTo("La linterna extraviada"));
            Assert.That((story.Chapter, story.Chapters, story.MinFloor), Is.EqualTo((2, 3, 2)));
            Assert.That(story.Ready, Is.False); Assert.That(run.NextStory, Is.Null);
            run.AdvanceFloor();
            Assert.That(run.NextStory.EventId, Is.EqualTo("lantern_ferryman"));
            var next = run.OpenEvent("river");
            Assert.That(next.Definition.Id, Is.EqualTo("lantern_ferryman"));
            Assert.That(run.PendingStories, Is.Empty, "the chapter opened: nothing waits any more");
        }

        [Test] public void DecliningAStoryLeavesNothingPendingAndTheFirstReadyChapterComesFirst()
        {
            var run = Run();
            var lantern = run.OpenEvent("lantern", "lost_lantern");
            lantern.Choose(lantern.Stage.Choices.Count - 1);
            Assert.That(run.PendingStories, Is.Empty);
            run.AdvanceFloor();
            StoryKit.Follow(run, "violin", "hollow_violin");
            StoryKit.Follow(run, "ember", "caged_ember");
            Assert.That(run.PendingStories.Select(s => s.EventId), Is.EqualTo(new[] { "string_mender", "ember_nursery" }));
            Assert.That(run.PendingStories.Select(s => s.Ready), Is.EqualTo(new[] { true, false }));
            Assert.That(run.NextStory.EventId, Is.EqualTo("string_mender"));
            Assert.That(run.OpenEvent("next").Definition.Id, Is.EqualTo("string_mender"), "the map's marker tells the truth");
        }

        [Test] public void EveryLinkedEventIsALaterChapterOfAStoryThatCanOpen()
        {
            foreach (var e in EventCatalog.All)
            {
                var first = EventCatalog.Find(EventStories.FirstChapter(e.Id));
                Assert.That(first.RequiredFlag, Is.Empty, e.Id + ": a story starts with an event anyone can meet");
                Assert.That(EventStories.Chapters(e.Id), Is.GreaterThanOrEqualTo(EventStories.Chapter(e.Id)), e.Id);
                Assert.That(EventStories.Chapter(e.Id) > 1, Is.EqualTo(e.RequiredFlag.Length > 0), e.Id + ": chapters 2+ are the flagged events");
                foreach (var c in e.Stages.SelectMany(s => s.Choices).Where(c => c.NextEventId.Length > 0))
                    Assert.That(c.SetFlag, Is.EqualTo(EventCatalog.Find(c.NextEventId).RequiredFlag), e.Id + " -> " + c.NextEventId + ": the choice must unlock the chapter it promises");
            }
            Assert.That(EventStories.Chapter("lantern_ferryman"), Is.EqualTo(2));
            Assert.That(EventStories.Chapters("lost_lantern"), Is.EqualTo(3));
            Assert.That(EventStories.Chapter("glass_sanctum"), Is.EqualTo(4));
        }

        [Test] public void DecliningChainDoesNotUnlockAndOldFloorSessionsCannotCharge()
        {
            var run = Run(); var lantern = run.OpenEvent("lantern", "lost_lantern");
            lantern.Choose(lantern.Stage.Choices.Count - 1);
            var stale = run.OpenEvent("merchant", "bottle_vendor");
            var gold = run.Gold; run.AdvanceFloor();
            Assert.That(stale.Choose(0), Is.False); Assert.That(run.Gold, Is.EqualTo(gold));
            Assert.Throws<InvalidOperationException>(() => run.OpenEvent("locked", "lantern_ferryman"));
        }

        [Test] public void EveryEligibleChoiceOnEveryFloorCanBeResolvedAndAlwaysAllowsLeaving()
        {
            foreach (var definition in EventCatalog.All)
            {
                for (var choice = 0; choice < definition.Stages[0].Choices.Count; choice++)
                {
                    var run = Run();
                    for (var floor = 1; floor < definition.MinFloor; floor++) run.AdvanceFloor();
                    // Complete each prerequisite through its real event choice, on a separate room.
                    Unlock(run, definition);
                    run.GainGold(10000);
                    var session = run.OpenEvent(definition.Id, definition.Id);
                    session.Choose(choice);
                    while (!session.Completed) session.Choose(session.Stage.Choices.Count - 1);
                    Assert.That(run.Hp, Is.InRange(1, run.MaxHp), definition.Id);
                    Assert.That(run.Deck.Count, Is.GreaterThanOrEqualTo(1), definition.Id);
                }
            }
        }

        private static void Unlock(RunState run, EventDefinition definition)
        {
            if (definition.RequiredFlag.Length == 0 || run.EventFlags.Contains(definition.RequiredFlag)) return;
            var parent = EventCatalog.All.First(e => e.Stages.SelectMany(s => s.Choices).Any(c => c.SetFlag == definition.RequiredFlag));
            Unlock(run, parent);
            // The test may already be on a later floor: replay the predecessor on its supported floor.
            typeof(RunState).GetProperty("Floor").SetValue(run, parent.MinFloor);
            run.GainGold(1000); run.Heal(int.MaxValue);
            var session = run.OpenEvent("unlock_" + parent.Id, parent.Id);
            session.Choose(parent.Stages[0].Choices.ToList().FindIndex(c => c.SetFlag == definition.RequiredFlag));
            typeof(RunState).GetProperty("Floor").SetValue(run, definition.MinFloor);
        }
    }
}
