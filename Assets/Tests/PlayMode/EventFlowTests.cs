using System.Collections;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;
using UnityEngine.TestTools;
using Key = LastWish.Presentation.Key;
using EventType = LastWish.Presentation.EventType;

namespace LastWish.Tests
{
    public sealed class EventFlowTests
    {
        [UnityTest] public IEnumerator EventRoomUsesChoicesAndReturnsToMapWithoutDoubleGold()
        {
            yield return GameDriver.Boot();
            GameDriver.Key(Key.Return);
            yield return GameDriver.Until(() => GameDriver.Manager.Top is CharacterSelectScene, 5, "character select");
            GameDriver.Key(Key.Return);
            yield return GameDriver.Until(() => GameDriver.Manager.Top is MapScene, 5, "map");
            var manager = GameDriver.Manager; var run = manager.Run;
            var node = run.Map.AvailableNodes().First();
            typeof(MapRoom).GetProperty("Kind").SetValue(node, RoomKind.Event);
            run.OpenEvent(node.Id, "scattered_coins");
            ((MapScene)manager.Top).SelectedNode = node;
            yield return GameDriver.Until(() => manager.Top is EventScene, 5, "event decision");
            var gold = run.Gold;
            GameDriver.Key(Key.D1);
            yield return GameDriver.Until(() => ((EventScene)manager.Top).Session.Completed, 5, "choice outcome");
            Assert.That(run.Gold, Is.EqualTo(gold + 22));
            yield return GameDriver.Frames(2);
            GameDriver.Key(Key.Return);
            yield return GameDriver.Until(() => manager.Top is MapScene, 5, "return to map");
            Assert.That(run.Gold, Is.EqualTo(gold + 22));
            Assert.That(run.EventHistory.Count, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator MerchantSupportsMouseAndMultipleStagesWithoutArtwork()
        {
            yield return GameDriver.Boot();
            var run = new RunState(HeroKind.Warrior, 77); run.GainGold(100);
            run.OpenEvent("bazaar", "masked_bazaar");
            var scene = new EventScene(GameDriver.Game.Context, run, "bazaar");
            scene.Draw(GameDriver.Game.Context.Canvas);
            scene.HandleEvent(new InputEvent(EventType.MouseDown, 150, 330, 1));
            Assert.That(scene.Session.StageIndex, Is.EqualTo(1));
            var afterEntry = run.Gold;
            scene.Draw(GameDriver.Game.Context.Canvas);
            scene.HandleEvent(new InputEvent(EventType.MouseDown, 150, 330, 1));
            Assert.That(scene.Session.Completed, Is.True);
            Assert.That(run.Gold, Is.EqualTo(afterEntry - 30));
            yield return GameDriver.Frames(1);
        }
        [UnityTest] public IEnumerator NewMarketSupportsThreeStagesAndSingleSettlement()
        {
            yield return GameDriver.Boot();
            var run = new RunState(HeroKind.Warrior, 91); run.GainGold(200);
            run.OpenEvent("market", "starling_market");
            var scene = new EventScene(GameDriver.Game.Context, run, "market");
            var gold = run.Gold; var maxHp = run.MaxHp; var hp = run.Hp;
            scene.Draw(GameDriver.Game.Context.Canvas);
            scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.D1));
            Assert.That(scene.Session.StageIndex, Is.EqualTo(1));
            scene.Draw(GameDriver.Game.Context.Canvas);
            scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.D1));
            Assert.That(scene.Session.StageIndex, Is.EqualTo(2));
            scene.Draw(GameDriver.Game.Context.Canvas);
            scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.D2));
            Assert.That(scene.Session.Completed, Is.True);
            Assert.That(run.Gold, Is.EqualTo(gold - 15));
            Assert.That(run.Hp, Is.EqualTo(hp - 8)); Assert.That(run.MaxHp, Is.EqualTo(maxHp + 4));
            Assert.That(run.EventHistory.Count, Is.EqualTo(3));
            scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.Return));
            scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.D2));
            Assert.That(run.Gold, Is.EqualTo(gold - 15));
            yield return GameDriver.Frames(1);
        }

        [UnityTest] public IEnumerator NewBranchingStoryRemembersTheChosenEndingAcrossFloors()
        {
            yield return GameDriver.Boot();
            var run = new RunState(HeroKind.Warrior, 92); run.GainGold(200);
            run.OpenEvent("egg", "copper_egg");
            var start = new EventScene(GameDriver.Game.Context, run, "egg");
            start.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.D1));
            run.AdvanceFloor();
            var middle = new EventScene(GameDriver.Game.Context, run, "incubator");
            Assert.That(middle.Session.Definition.Id, Is.EqualTo("brass_incubator"));
            middle.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.D2));
            run.AdvanceFloor();
            var ending = new EventScene(GameDriver.Game.Context, run, "bell");
            Assert.That(ending.Session.Definition.Id, Is.EqualTo("silent_bell"));
            Assert.Throws<System.InvalidOperationException>(() => run.OpenEvent("locked", "clockwork_chick"));
            var gold = run.Gold;
            ending.Draw(GameDriver.Game.Context.Canvas);
            ending.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.D1));
            Assert.That(run.Gold, Is.EqualTo(gold + 105));
            Assert.That(run.EventHistory.Count, Is.EqualTo(3));
            yield return GameDriver.Frames(1);
        }

    }
}
