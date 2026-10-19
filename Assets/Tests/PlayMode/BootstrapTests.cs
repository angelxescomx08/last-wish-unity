using System.Collections;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using EventType = LastWish.Presentation.EventType;
using Key = LastWish.Presentation.Key;

namespace LastWish.Tests
{
    public sealed class BootstrapTests
    {
        [UnityTest]
        public IEnumerator LoadingMenuSelectionMapAndCombatWork()
        {
            yield return GameDriver.Boot();
            var manager = GameDriver.Manager;
            Assert.That(manager.Top, Is.InstanceOf<MainMenuScene>());
            Assert.That(GameDriver.Game.LastBatchCount, Is.LessThan(200), "The menu must stay a handful of batches");
            yield return GameDriver.Frames(3);
            GameDriver.Capture("menu");

            GameDriver.Key(Key.Return);                         // Jugar
            yield return GameDriver.Until(() => manager.Top is CharacterSelectScene, 5, "Jugar must open the character selection");
            GameDriver.Key(Key.Return);                         // La Guerrera
            yield return GameDriver.Until(() => manager.Top is MapScene, 5, "Confirming must start a run on the map");
            Assert.That(manager.Run, Is.Not.Null);
            GameDriver.Capture("map");

            var map = (MapScene)manager.Top;
            map.SelectedNode = manager.Run.Map.AvailableNodes().First(n => n.Kind == RoomKind.Combat);
            yield return GameDriver.Until(() => manager.Top is CombatScene, 5, "A combat room must open the fight");
            var combat = (CombatScene)manager.Top;
            var state = combat.State;
            yield return GameDriver.Seconds(1.5f);              // the opening hand is dealt
            Assert.That(state.Hand.Count, Is.EqualTo(5));
            GameDriver.Capture("combat");

            var mana = state.Mana;
            var index = Enumerable.Range(0, state.Hand.Count).First(i => state.CanPlay(i));
            GameDriver.Key(Key.D1 + index);
            GameDriver.Key(Key.Return);
            yield return GameDriver.Until(() => state.Mana < mana, 5, "Number key + Enter must play the card");
            Assert.That(state.Hand.Count, Is.EqualTo(4));

            var turn = state.Turn;
            yield return GameDriver.Until(() => !combat.PresentationBusy, 5, "The play must finish presenting");
            GameDriver.Key(Key.E);
            yield return GameDriver.Until(() => state.Turn == turn + 1, 5, "E must end the turn");
            Assert.That(state.Mana, Is.EqualTo(state.ManaMaximum));
            yield return GameDriver.Seconds(2.5f);
            GameDriver.Capture("combat-turn2");

            GameDriver.Key(Key.Escape);
            yield return GameDriver.Until(() => manager.Pause != null, 5, "Escape must open the pause menu");
            GameDriver.Capture("pause");
            GameDriver.Key(Key.Escape);
            yield return GameDriver.Until(() => manager.Pause == null, 5, "Escape must resume");
            GameDriver.Key(Key.C);
            yield return GameDriver.Until(() => manager.HeroSheet != null, 5, "C must open the hero sheet");
            yield return GameDriver.Seconds(1f);
            GameDriver.Capture("hero-sheet");
            GameDriver.Key(Key.Escape);
            yield return GameDriver.Until(() => manager.HeroSheet == null, 5, "Escape must close the hero sheet");
        }

        [UnityTest]
        public IEnumerator MouseDragPlaysACardOnTheEnemy()
        {
            yield return GameDriver.Boot();
            var manager = GameDriver.Manager;
            var run = new RunState(HeroKind.Warrior, 12345);
            manager.StartRun(run);
            yield return null;
            ((MapScene)manager.Top).SelectedNode = run.Map.AvailableNodes().First(n => n.Kind == RoomKind.Combat);
            yield return GameDriver.Until(() => manager.Top is CombatScene, 5, "combat");
            var state = ((CombatScene)manager.Top).State;
            yield return GameDriver.Seconds(1.5f);
            var hp = state.Enemies.Sum(e => e.Hp);
            var attack = Enumerable.Range(0, state.Hand.Count).FirstOrDefault(i => state.Hand[i].IsAttack && state.CanPlay(i));
            Assume.That(state.Hand[attack].IsAttack, "The opening hand has an attack");
            // hover, press, drag above the play line onto the enemy, release (Slay the Spire style)
            var scene = (CombatScene)manager.Top;
            GameDriver.Send(new InputEvent(EventType.MouseMove, 640, 640));
            yield return null;
            GameDriver.Key(Key.D1 + attack);                     // number key picks it up; aim with the mouse
            GameDriver.Send(new InputEvent(EventType.MouseMove, 830, 230));
            yield return GameDriver.Frames(2);
            GameDriver.Capture("aim");
            GameDriver.Send(new InputEvent(EventType.MouseDown, 830, 230, 1));
            GameDriver.Send(new InputEvent(EventType.MouseUp, 830, 230, 1));
            yield return GameDriver.Until(() => state.Enemies.Sum(e => e.Hp) < hp, 5, "Clicking the enemy must play the attack on it");
        }
    }
}
