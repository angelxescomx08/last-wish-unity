using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>
    /// The golden / diamond replay on the combat screen (user, 2026-11-04: "la carta de veneno a todos los enemigos no se está
    /// aplicando en dorada dos veces"). The rules always cast the card twice; the screen used to replay only HP and block, so
    /// Veneno jumped to its final value with the first cast and the second cast seemed to do nothing. Now statuses and mana
    /// wait for their cast, and every cast floats what it added ("+5 Veneno") over each enemy.
    /// </summary>
    public sealed class GoldenReplayTests
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        [SetUp] public void ResetTuning() => Tuning.Reset();
        [TearDown] public void RestoreTuning() => Tuning.Reset();

        private static GameContext game;
        private static GameContext Game()
        {
            if (game != null) return game;
            var render = new NullRender(); var art = new Art(render, new FileAssets()); art.LoadCore();
            return game = new GameContext(new Canvas(render), art, new SoundPlayer(null), new Preferences(), null) { Data = new GameData(new FileAssets()) };
        }

        /// <summary>A fight with the most enemies a floor-1 combat room offers, the hand starting with <paramref name="card"/>.</summary>
        private static CombatScene Scene(CardDefinition card, int mana = 3)
        {
            var run = new RunState(HeroKind.Rogue, 42, EnemyRoster.Expanded);
            var enemies = run.Map.Nodes.Where(n => n.Kind == RoomKind.Combat).Select(n => run.GenerateEnemies(n.Id)).OrderByDescending(e => e.Count).First();
            var state = run.CreateCombat(enemies); state.Invincible = true;
            var scene = new CombatScene(Game(), state, run.OwnedRelics);
            Frames(scene, 40);
            ((List<CardDefinition>)typeof(CombatState).GetField("hand", F).GetValue(state)).Insert(0, card.Copy());
            typeof(CombatState).GetProperty("Mana").SetValue(state, mana);
            Frames(scene, 20);
            return scene;
        }

        private static void Draw(CombatScene scene) { var g = Game(); g.Canvas.BeginFrame(); scene.Draw(g.Canvas); g.Canvas.EndFrame(); }
        private static void Frames(CombatScene scene, int n, float dt = 1f / 60f) { for (var i = 0; i < n; i++) { scene.Update(dt); if (i % 10 == 0) Draw(scene); } }
        private static void Play(CombatScene scene, int index, int? target) => typeof(CombatScene).GetMethod("DoPlayCard", F).Invoke(scene, new object[] { index, target });

        /// <summary>The stacks of <paramref name="status"/> each enemy's badges show now (0 when it has no badge).</summary>
        private static int[] Shown(CombatScene scene, string status)
        {
            Draw(scene);
            var boxes = (List<EntityHitboxes>)typeof(CombatScene).GetField("enemyHitboxes", F).GetValue(scene);
            return boxes.Take(scene.State.Enemies.Count).Select(b => b.Statuses.Where(s => s.Name == status).Select(s => s.Stacks).DefaultIfEmpty(0).First()).ToArray();
        }

        /// <summary>The floating labels alive now ("+5 Veneno", "-6"…).</summary>
        private static List<string> Labels(CombatScene scene)
        {
            var fx = typeof(CombatScene).GetField("fx", F).GetValue(scene);
            var effects = (IEnumerable)fx.GetType().GetField("effects", F).GetValue(fx);
            var texts = new List<string>();
            foreach (var e in effects) { var t = (string)e.GetType().GetField("Text").GetValue(e); if (t != null) texts.Add(t); }
            return texts;
        }

        private static readonly float CastIntro = 0.35f;
        private static float CastStep(CombatScene scene) => Math.Max(0.65f, (float)typeof(CombatScene).GetMethod("HeroSeconds", F).Invoke(scene, new object[] { "cast" }) + 0.04f);

        [Test] public void GoldenPoisonStormShowsEachCastAddingItsPoison()
        {
            foreach (var chroma in new[] { ChromaKind.Golden, ChromaKind.Diamond })
            {
                var storm = RunContent.Cards.Single(c => c.Id == "ep_tormenta_veneno").WithChroma(chroma);
                var scene = Scene(storm);
                var n = scene.State.Enemies.Count; var casts = storm.CastCount;
                Assert.That(n, Is.GreaterThan(1), "the test wants a group");
                Play(scene, 0, 0);
                Assert.That(scene.State.Enemies.Select(e => e.Statuses[StatusKind.Poison]), Is.All.EqualTo(5 * casts), "the rules applied every cast");
                Assert.That(Shown(scene, "Veneno"), Is.All.EqualTo(0), chroma + ": before the first cast the badges still show no poison");
                var step = CastStep(scene); var t = 0f;
                for (var k = 0; k < casts; k++)
                {
                    var at = CastIntro + k * step + 0.05f;
                    while (t < at) { scene.Update(1f / 60f); t += 1f / 60f; }
                    Assert.That(Shown(scene, "Veneno"), Is.All.EqualTo(5 * (k + 1)), chroma + ": after cast " + (k + 1));
                    Assert.That(Labels(scene).Count(l => l == "+5 Veneno"), Is.GreaterThanOrEqualTo(n), chroma + ": cast " + (k + 1) + " floats its poison over every enemy");
                }
                if (casts == 2) Assert.That(Labels(scene).Count(l => l == "+5 Veneno"), Is.EqualTo(n * casts), "one label per enemy and cast");
                Frames(scene, 300);
                Assert.That(Shown(scene, "Veneno"), Is.All.EqualTo(5 * casts), chroma + ": the replay ends on the real numbers");
            }
        }

        [Test] public void TheManaOrbFillsCastByCast()
        {
            var card = new CardDefinition("m", "Maná", 1, 0, 0, manaGain: 1).WithChroma(ChromaKind.Golden);
            var scene = Scene(card, 1);
            int ShownMana() => (int)typeof(CombatScene).GetProperty("ShownMana", F).GetValue(scene);
            Play(scene, 0, 0);
            Assert.That(scene.State.Mana, Is.EqualTo(2));
            Assert.That(ShownMana(), Is.EqualTo(0), "paid, nothing cast yet");
            var step = CastStep(scene); var t = 0f;
            for (var k = 0; k < 2; k++)
            {
                while (t < CastIntro + k * step + 0.05f) { scene.Update(1f / 60f); t += 1f / 60f; }
                Assert.That(ShownMana(), Is.EqualTo(k + 1), "after cast " + (k + 1));
            }
            Frames(scene, 300);
            Assert.That(ShownMana(), Is.EqualTo(2));
        }

        [Test] public void ANormalCardAlsoSaysWhatItApplies()
        {
            var mark = RunContent.Cards.Single(c => c.Id == "r_marcar_objetivo");
            var scene = Scene(mark);
            Play(scene, 0, 0);
            Assert.That(Labels(scene), Has.Some.EqualTo("+3 Marcado"));
            Assert.That(Shown(scene, "Marcado")[0], Is.EqualTo(3), "no replay for a single cast: the badge shows it at once");
        }

        [Test] public void ReplayingEveryGoldenStatusCardDrawsEveryCast()
        {
            var cards = RunContent.Cards.Where(c => c.ClassId == "rogue" || c.ClassId == "neutral").Where(c => c.Effects != null
                && c.Effects.Any(l => l.Commands.Any(cmd => cmd.Kind == CommandKind.PoisonAll || cmd.Kind == CommandKind.Poison || cmd.Kind == CommandKind.Mark || cmd.Kind == CommandKind.WeakenAll))).ToList();
            Assert.That(cards.Count, Is.GreaterThanOrEqualTo(3));
            foreach (var card in cards)
            {
                var scene = Scene(card.WithChroma(ChromaKind.Diamond), 10);
                Play(scene, 0, 0);
                Assert.DoesNotThrow(() => Frames(scene, 300), card.Id);
                var names = Game().Data.Glossary.StatusName;
                foreach (var kind in new[] { StatusKind.Poison, StatusKind.Marked, StatusKind.Weak })
                    Assert.That(Shown(scene, names[kind]), Is.EqualTo(scene.State.Enemies.Select(e => e.Statuses[kind]).ToArray()), card.Id + " " + kind + ": the badges end on the real stacks");
            }
        }
    }
}
