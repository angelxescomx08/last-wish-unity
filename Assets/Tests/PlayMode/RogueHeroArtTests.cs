using System.Collections;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using Key = LastWish.Presentation.Key;

namespace LastWish.Tests
{
    /// <summary>La Pícara's new sheet in a real fight: idle, attack at the blade's contact, guard and the enemy turn
    /// (captures in TestResults/rogue-hero-*.png to inspect by eye).</summary>
    public sealed class RogueHeroArtTests
    {
        private static int Playable(CombatState state, System.Func<CardDefinition, bool> what) =>
            Enumerable.Range(0, state.Hand.Count).FirstOrDefault(i => what(state.Hand[i]) && state.CanPlay(i));

        [UnityTest]
        public IEnumerator TheRogueFightsWithHerNewSheet()
        {
            yield return GameDriver.Boot();
            var manager = GameDriver.Manager;
            var sheets = GameDriver.Game.Context.Art.Hero("rogue");
            Assert.That(sheets.HasHalo, Is.True);
            var run = new RunState(HeroKind.Rogue, 4242);
            manager.StartRun(run);
            yield return GameDriver.Until(() => manager.Top is MapScene, 5, "map");
            ((MapScene)manager.Top).SelectedNode = run.Map.AvailableNodes().First(n => n.Kind == RoomKind.Combat);
            yield return GameDriver.Until(() => manager.Top is CombatScene, 5, "combat");
            var combat = (CombatScene)manager.Top; var state = combat.State;
            yield return GameDriver.Seconds(1.6f);
            for (var i = 0; i < 3; i++) { GameDriver.Capture("rogue-hero-idle-" + i); yield return GameDriver.Seconds(0.4f); }

            var attack = Playable(state, c => c.IsAttack);
            Assume.That(state.Hand[attack].IsAttack, "the opening hand has an attack");
            GameDriver.Key(Key.D1 + attack); GameDriver.Key(Key.Return);
            yield return GameDriver.Until(() => combat.HeroAction == "attack", 3, "an attack card plays the attack clip");
            yield return GameDriver.Seconds(sheets.StrikeSeconds * 0.5f);
            GameDriver.Capture("rogue-hero-attack-windup");
            yield return GameDriver.Seconds(sheets.StrikeSeconds * 0.5f);
            GameDriver.Capture("rogue-hero-attack-strike");
            yield return GameDriver.Seconds(0.15f);
            GameDriver.Capture("rogue-hero-attack-follow");
            yield return GameDriver.Until(() => !combat.PresentationBusy && combat.HeroAction == null, 6, "the attack finishes");

            var guard = Playable(state, c => !c.IsAttack);
            if (!state.Hand[guard].IsAttack && state.CanPlay(guard))
            {
                GameDriver.Key(Key.D1 + guard); GameDriver.Key(Key.Return);
                yield return GameDriver.Until(() => combat.HeroAction == "guard", 3, "a block card plays the guard clip");
                yield return GameDriver.Seconds(0.27f);
                GameDriver.Capture("rogue-hero-guard");
                yield return GameDriver.Until(() => !combat.PresentationBusy && combat.HeroAction == null, 6, "the guard finishes");
            }

            GameDriver.Key(Key.E);
            var end = Time.realtimeSinceStartup + 6;
            while (combat.HeroAction != "hurt" && Time.realtimeSinceStartup < end) yield return null;
            if (combat.HeroAction == "hurt") { yield return GameDriver.Seconds(0.05f); GameDriver.Capture("rogue-hero-hurt"); }
            yield return GameDriver.Seconds(1.0f);
            GameDriver.Capture("rogue-hero-after-enemy-turn");
        }
    }
}
