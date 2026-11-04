using System.Collections;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;
using UnityEngine.TestTools;

using Key = LastWish.Presentation.Key;

namespace LastWish.Tests
{
    /// <summary>The warrior's basic cards (Espadazo, Bloqueo, Guardia Férrea) with their animated illustrations, in the
    /// real game: deck viewer and combat hand (captures in TestResults/warrior-*.png to inspect by eye).</summary>
    public sealed class WarriorCardArtTests
    {
        [UnityTest]
        public IEnumerator TheWarriorDeckAndHandShowTheAnimatedArt()
        {
            yield return GameDriver.Boot();
            var manager = GameDriver.Manager;
            var art = GameDriver.Game.Context.Art;
            Assert.That(CardArtAnimations.Ids(art), Is.SupersetOf(new[] { "espadazo_base", "bloqueo_base", "guardia_ferrea_base" }),
                "Cards/Anim/anim.json is in Resources");
            var run = new RunState(HeroKind.Warrior, 4242);
            manager.StartRun(run);
            yield return GameDriver.Until(() => manager.Top is MapScene, 5, "map");
            yield return GameDriver.Frames(3);

            GameDriver.Click(1159, 34);                            // "Mazo": the deck viewer draws cards with CardWidget.Draw
            yield return GameDriver.Seconds(0.6f);
            GameDriver.Capture("warrior-deck");
            yield return GameDriver.Seconds(0.33f);
            GameDriver.Capture("warrior-deck-later");
            GameDriver.Key(Key.Escape);
            yield return GameDriver.Frames(3);

            ((MapScene)manager.Top).SelectedNode = run.Map.AvailableNodes().First(n => n.Kind == RoomKind.Combat);
            yield return GameDriver.Until(() => manager.Top is CombatScene, 5, "combat");
            var state = ((CombatScene)manager.Top).State;
            yield return GameDriver.Seconds(1.6f);                  // the opening hand is dealt
            Assert.That(state.Hand.Count(c => CardArtAnimations.For(art, c.Id) != null), Is.EqualTo(state.Hand.Count),
                "the warrior's opening hand is all basic cards");
            var hand = state.Hand.Count;
            for (var i = 0; i < 24; i++)                            // one loop of the 12 fps illustrations
            {
                GameDriver.Capture("warrior-hand-" + i.ToString("00"));
                yield return GameDriver.Seconds(1f / 12f);
            }
            GameDriver.Key(Key.D3);                                 // pick up the third card: drawn bigger, upright
            yield return GameDriver.Seconds(0.5f);
            GameDriver.Capture("warrior-hand-held");
            yield return GameDriver.Seconds(0.4f);
            GameDriver.Capture("warrior-hand-held-later");
            GameDriver.Key(Key.Escape);
            yield return GameDriver.Frames(3);
            Assert.That(state.Hand.Count, Is.EqualTo(hand), "nothing was played");
        }
    }
}
