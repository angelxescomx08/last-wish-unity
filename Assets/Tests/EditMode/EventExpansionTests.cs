using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using NUnit.Framework;

namespace LastWish.Tests
{
    public sealed class EventExpansionTests
    {
        [SetUp] public void Reset() => Tuning.Reset();
        [TearDown] public void Restore() => Tuning.Reset();

        [Test] public void AddsOneHundredEventsAfterTheOriginalFifty()
        {
            Assert.That(EventCatalog.All.Count, Is.EqualTo(150));
            Assert.That(EventCatalog.All[0].Id, Is.EqualTo("scattered_coins"));
            Assert.That(EventCatalog.All[49].Id, Is.EqualTo("heart_gate"));
            Assert.That(EventCatalog.All.Skip(50).Select(e => e.Id).Distinct().Count(), Is.EqualTo(100));
            Assert.That(EventCatalog.All.Skip(50).Count(e => e.Stages.Count > 1), Is.EqualTo(8));
        }

        [Test] public void EveryLinkedChapterHasAReachableMatchingFlagAndNoCycles()
        {
            foreach (var definition in EventCatalog.All)
            {
                if (definition.RequiredFlag.Length > 0)
                    Assert.That(EventCatalog.All.Any(p => p.Stages.SelectMany(s => s.Choices)
                        .Any(c => c.NextEventId == definition.Id && c.SetFlag == definition.RequiredFlag)), Is.True, definition.Id);
                CheckLinks(definition, new HashSet<string>());
                foreach (var stage in definition.Stages)
                {
                    Assert.That(stage.Choices.Count, Is.InRange(2, 3), definition.Id);
                    Assert.That(stage.Choices.Any(c => c.IsExit && c.GoldCost == 0 && c.HpCost == 0 && c.MaxHpCost == 0), Is.True);
                    foreach (var c in stage.Choices)
                        foreach (var effect in new[] { c.Success, c.Failure }.Where(e => e != null && e.Relic.Length > 0))
                            Assert.That(RunContent.RelicByTag(effect.Relic), Is.Not.Null, effect.Relic);
                }
            }
        }

        private static void CheckLinks(EventDefinition definition, HashSet<string> ancestors)
        {
            Assert.That(ancestors.Add(definition.Id), Is.True, "Cycle: " + definition.Id);
            foreach (var choice in definition.Stages.SelectMany(s => s.Choices).Where(c => c.NextEventId.Length > 0))
            {
                var next = EventCatalog.Find(choice.NextEventId);
                Assert.That(next, Is.Not.Null);
                Assert.That(next.RequiredFlag, Is.EqualTo(choice.SetFlag));
                CheckLinks(next, new HashSet<string>(ancestors));
            }
        }

        [Test] public void EveryDecisionPathAcrossAllStagesCanBePlayedWithoutBreakingRunState()
        {
            foreach (var definition in EventCatalog.All)
                foreach (var path in Paths(definition, 0))
                {
                    var run = new RunState(HeroKind.Warrior, 314);
                    Unlock(run, definition);
                    SetFloor(run, definition.MinFloor); run.GainGold(10000); run.Heal(int.MaxValue);
                    var session = run.OpenEvent("path_" + definition.Id, definition.Id);
                    foreach (var choice in path)
                    {
                        if (session.Completed) break; // A failed risk can end a path early.
                        Assert.That(session.Choose(choice), Is.True, definition.Id + ": " + session.Message);
                    }
                    Assert.That(session.Completed, Is.True, definition.Id);
                    Assert.That(run.Hp, Is.InRange(1, run.MaxHp), definition.Id);
                    Assert.That(run.Gold, Is.GreaterThanOrEqualTo(0), definition.Id);
                    Assert.That(run.Deck.Count, Is.GreaterThanOrEqualTo(1), definition.Id);
                    var gold = run.Gold;
                    Assert.That(session.Choose(0), Is.False); Assert.That(run.Gold, Is.EqualTo(gold));
                }
        }

        private static IEnumerable<int[]> Paths(EventDefinition definition, int stage)
        {
            for (var i = 0; i < definition.Stages[stage].Choices.Count; i++)
            {
                var next = definition.Stages[stage].Choices[i].NextStage;
                if (next < 0) yield return new[] { i };
                else foreach (var rest in Paths(definition, next)) yield return new[] { i }.Concat(rest).ToArray();
            }
        }

        private static void SetFloor(RunState run, int floor) => typeof(RunState).GetProperty("Floor").SetValue(run, floor);
        private static void Unlock(RunState run, EventDefinition definition)
        {
            if (definition.RequiredFlag.Length == 0 || run.EventFlags.Contains(definition.RequiredFlag)) return;
            var parent = EventCatalog.All.Single(p => p.Stages.SelectMany(s => s.Choices)
                .Any(c => c.NextEventId == definition.Id && c.SetFlag == definition.RequiredFlag));
            Unlock(run, parent); SetFloor(run, parent.MinFloor); run.GainGold(10000); run.Heal(int.MaxValue);
            var session = run.OpenEvent("unlock_" + parent.Id, parent.Id);
            var choice = parent.Stages[0].Choices.ToList().FindIndex(c => c.NextEventId == definition.Id);
            Assert.That(choice, Is.GreaterThanOrEqualTo(0)); Assert.That(session.Choose(choice), Is.True, parent.Id);
        }

        [TestCase(0, "clockwork_chick", "silent_bell")]
        [TestCase(1, "silent_bell", "clockwork_chick")]
        public void CopperEggBranchesAreMutuallyExclusive(int branch, string expected, string unavailable)
        {
            var run = new RunState(HeroKind.Warrior, 42); run.GainGold(500);
            run.OpenEvent("egg", "copper_egg").Choose(0);
            run.AdvanceFloor(); var middle = run.OpenEvent("incubator");
            Assert.That(middle.Definition.Id, Is.EqualTo("brass_incubator")); middle.Choose(branch);
            run.AdvanceFloor();
            Assert.That(run.OpenEvent("ending").Definition.Id, Is.EqualTo(expected));
            Assert.Throws<InvalidOperationException>(() => run.OpenEvent("other_ending", unavailable));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(5)] [TestCase(8)] [TestCase(12)]
        public void RandomSelectionRespectsFloorsAndDoesNotRevealLockedChapters(int floor)
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var run = new RunState(HeroKind.Mage, seed); SetFloor(run, floor);
                for (var room = 0; room < 12; room++)
                {
                    var session = run.OpenEvent("random_" + room);
                    Assert.That(floor, Is.InRange(session.Definition.MinFloor, session.Definition.MaxFloor));
                    Assert.That(session.Definition.RequiredFlag, Is.Empty);
                    session.Choose(session.Stage.Choices.Count - 1);
                }
            }
        }
    }
}
