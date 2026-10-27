using System;
using System.Linq;
using LastWish.Domain;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>Unity design changes on top of the exported original cards (CardDesign) and Sombra Gemela stacking.</summary>
    public sealed class CardDesignTests
    {
        [SetUp] public void ResetTuning() => Tuning.Reset();
        [TearDown] public void RestoreTuning() => Tuning.Reset();

        private static CardDefinition Run(string id) => RunContent.Cards.Single(c => c.Id == id);
        private static CombatState Fight(params CardDefinition[] deck) => new CombatState(
            HeroDefinition.OriginalFor(HeroKind.Rogue).WithBonuses(0, 10, 0), deck, 42, enemyHp: 100000, enemyDamage: 0);
        private static void Play(CombatState state, string id)
        {
            for (var i = 0; i < state.Hand.Count; i++) if (state.Hand[i].Id == id) { Assert.That(state.PlayCard(i), Is.True, id); return; }
            throw new InvalidOperationException(id + " is not in the hand");
        }
        private static CardDefinition Combo(string id = "combo") => new CardDefinition(id, id, 0, 3, 0,
            effects: new[] { new EffectLayer(EffectCondition.Always, 3), new EffectLayer(EffectCondition.Combo, 5) });
        private static CardDefinition Opener() => new CardDefinition("opener", "opener", 0, 0, 0, effects: new[] { new EffectLayer(EffectCondition.Always, 0) });

        // ------------------------------------------------------------------ one-shot powers are skills

        [Test] public void PoderOcultoAndConcentracionAreSkillsInTheGame()
        {
            foreach (var id in new[] { "ep_poder_oculto", "m_concentracion" })
            {
                Assert.That(Run(id).Kind, Is.EqualTo(CardKind.Skill), id);
                Assert.That(NumericCards.All.Single(c => c.Id == id).Kind, Is.EqualTo(CardKind.Power), id + ": the exported original is untouched");
            }
            Assert.That(RunContent.Cards.Count, Is.EqualTo(NumericCards.All.Count));
            Assert.That(RunContent.Cards.Where(c => !CardDesign.KindChanges.ContainsKey(c.Id)).All(c => NumericCards.All.Contains(c)), "other cards are the same objects");
        }

        [Test] public void PoderOcultoDrawsFourOnceAndGoesToTheDiscard()
        {
            var deck = new[] { Run("ep_poder_oculto").WithInnate() }.Concat(Enumerable.Range(0, 20).Select(i => new CardDefinition("filler" + i, "Relleno", 1, 1, 0))).ToArray();
            var state = Fight(deck);
            var hand = state.Hand.Count;
            Play(state, "ep_poder_oculto");
            Assert.That(state.Hand.Count, Is.EqualTo(hand - 1 + 4));
            Assert.That(state.ActivePowers, Is.Empty);
            Assert.That(state.DiscardPile.Select(c => c.Id), Does.Contain("ep_poder_oculto"));
        }

        [Test] public void GoldenSkillsStayGoldenAndKeepTheirNewType()
        {
            var golden = Run("ep_poder_oculto").WithChroma(ChromaKind.Golden);
            Assert.That((golden.Kind, golden.Chroma, golden.CastCount), Is.EqualTo((CardKind.Skill, ChromaKind.Golden, 2)));
            Assert.That(golden.Upgrade().Kind, Is.EqualTo(CardKind.Skill));
            Assert.That(CardDesign.Apply(golden), Is.SameAs(golden), "applying twice changes nothing");
        }

        [Test] public void EveryPowerInTheGameKeepsActingBetweenTurns()
        {
            var powers = RunContent.Cards.Where(c => c.Kind == CardKind.Power).ToList();
            Assert.That(powers.Count, Is.GreaterThan(15));
            foreach (var power in powers)
                Assert.That(power.Effects.Any(e => e.Commands.Count > 0 || e.TurnCommands.Count > 0), power.Id + " only has one-shot numbers: make it a skill");
        }

        // ------------------------------------------------------------------ Sombra Gemela multiplies

        /// <summary>Plays every Sombra Gemela, an opener, then a 3 + Combo 5 card: (times its Combo resolved, damage it dealt).</summary>
        private static (int times, long dealt) ComboTimes(int normalCopies, int goldenCopies, out CombatState state)
        {
            var twin = Run("r_sombra_gemela");
            var deck = Enumerable.Range(0, normalCopies).Select(_ => twin)
                .Concat(Enumerable.Range(0, goldenCopies).Select(_ => twin.WithChroma(ChromaKind.Golden)))
                .Concat(new[] { Opener(), Combo() }).ToArray();
            state = Fight(deck);
            while (state.Hand.Any(c => c.Id == "r_sombra_gemela")) Play(state, "r_sombra_gemela");
            Play(state, "opener");
            var hp = (long)state.EnemyHp;
            Play(state, "combo");
            return (state.LastPlay.ComboTimes, hp - state.EnemyHp);
        }

        [Test] public void EachCopyOfSombraGemelaDoublesTheCombo()
        {
            var (none, baseDamage) = ComboTimes(0, 0, out _);
            Assert.That(none, Is.EqualTo(1), "no Sombra Gemela");
            var (once, onceDamage) = ComboTimes(1, 0, out var one);
            Assert.That(once, Is.EqualTo(2), "one copy: twice");
            Assert.That(onceDamage - baseDamage, Is.EqualTo(5), "the Combo layer (5) once more");
            Assert.That((one.ComboDoublings, one.ComboMultiplier), Is.EqualTo((1, 2)));
            var (gold, goldDamage) = ComboTimes(0, 1, out var golden);
            Assert.That(gold, Is.EqualTo(4), "golden = two copies: 2 × 2");
            Assert.That(goldDamage - baseDamage, Is.EqualTo(15));
            Assert.That((golden.ComboDoublings, golden.ComboMultiplier, golden.ActivePowers.Count), Is.EqualTo((2, 4, 1)));
            Assert.That(ComboTimes(2, 0, out _).times, Is.EqualTo(4), "two normal copies");
            Assert.That(ComboTimes(1, 1, out _).times, Is.EqualTo(8), "a normal and a golden one: 2 × 2 × 2");
        }

        [Test] public void OnlyTheFirstComboCardOfEachTurnIsMultiplied()
        {
            var twin = Run("r_sombra_gemela").WithChroma(ChromaKind.Golden);
            var state = Fight(twin, Combo("a"), Combo("b"));
            Play(state, "r_sombra_gemela");
            Assert.That(state.ComboDoublingUsed, Is.False);
            Play(state, "a");
            Assert.That((state.LastPlay.ComboTimes, state.ComboDoublingUsed), Is.EqualTo((4, true)));
            Play(state, "b");
            Assert.That(state.LastPlay.ComboTimes, Is.EqualTo(1), "the second Combo card of the turn resolves once");
            state.EndTurn();
            Assert.That(state.ComboDoublingUsed, Is.False, "ready again next turn");
        }

        [Test] public void TheMultiplierStopsAtItsCapWithoutOverflow()
        {
            var state = Fight(Opener());
            typeof(CombatState).GetField("doubleCombo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(state, int.MaxValue);
            Assert.That(state.ComboMultiplier, Is.EqualTo(1 << CombatState.MaxComboDoublings));
            typeof(CombatState).GetField("doubleCombo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(state, CombatState.MaxComboDoublings + 1);
            Assert.That(state.ComboMultiplier, Is.EqualTo(1024));
        }

        [Test] public void APlayWithoutComboReportsOneTime()
        {
            var fresh = Fight(Combo());
            Play(fresh, "combo");
            Assert.That((fresh.LastPlay.Combo, fresh.LastPlay.ComboTimes), Is.EqualTo((false, 1)), "first card of the turn: no Combo");
        }
    }
}
