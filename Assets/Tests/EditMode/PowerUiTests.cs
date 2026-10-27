using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>Active powers on screen: badges with copies (PowerBar), what each one does now (PowerText / Tooltip.Power),
    /// and the combat screen wiring (hover, hero panels), driven headless with the real assets.</summary>
    public sealed class PowerUiTests
    {
        [SetUp] public void ResetTuning() => Tuning.Reset();
        [TearDown] public void RestoreTuning() => Tuning.Reset();

        private static CardDefinition Run(string id) => RunContent.Cards.Single(c => c.Id == id);
        private static GameContext game;
        private static GameContext Game()
        {
            if (game != null) return game;
            var render = new NullRender(); var art = new Art(render, new FileAssets()); art.LoadCore();
            return game = new GameContext(new Canvas(render), art, new SoundPlayer(null), new Preferences(), null) { Data = new GameData(new FileAssets()) };
        }
        private static CombatState Fight(params CardDefinition[] deck) => new CombatState(
            HeroDefinition.OriginalFor(HeroKind.Rogue).WithBonuses(0, 10, 0), deck, 7, -1, new[] { EnemyCatalog.Create("espectro", 1, "a") }, 0, null);
        private static void Play(CombatState state, string id)
        {
            var i = state.Hand.ToList().FindIndex(c => c.Id == id);
            Assert.That(i, Is.GreaterThanOrEqualTo(0), id);
            Assert.That(state.Play(i).Success, Is.True, id);
        }

        // ------------------------------------------------------------------ grouping and badges

        [Test] public void PowersGroupByCardAndCountEveryCopy()
        {
            var twin = Run("r_sombra_gemela"); var rapina = Run("r_rapina");
            var groups = PowerBar.Group(new[] { twin, rapina, twin.WithChroma(ChromaKind.Golden), twin.WithChroma(ChromaKind.Diamond) });
            Assert.That(groups.Select(g => (g.card.Id, g.copies)), Is.EqualTo(new[] { ("r_sombra_gemela", 1 + 2 + 3), ("r_rapina", 1) }));
            Assert.That(groups[0].card.Chroma, Is.EqualTo(ChromaKind.Diamond), "the best chroma represents the group");
            Assert.That(PowerBar.Group(Array.Empty<CardDefinition>()), Is.Empty);
            Assert.That(PowerBar.Group(null), Is.Empty);
            var many = PowerBar.Group(Enumerable.Repeat(twin.WithChroma(ChromaKind.Diamond), 1000).ToList());
            Assert.That(many.Single().copies, Is.EqualTo(3000));
        }

        [Test] public void BadgesPopWhenACopyArrivesFlashWhenTheyActAndCanBeHovered()
        {
            var bar = new PowerBar(); var twin = Run("r_sombra_gemela");
            bar.Sync(new[] { twin }, 100, 300, 200);
            Assert.That(bar.Pop("r_sombra_gemela"), Is.EqualTo(1f), "a new power pops");
            for (var i = 0; i < 10; i++) bar.Update(.1f);
            Assert.That(bar.Pop("r_sombra_gemela"), Is.Zero);
            bar.Sync(new[] { twin }, 100, 300, 200);
            Assert.That(bar.Pop("r_sombra_gemela"), Is.Zero, "nothing new, no pop");
            bar.Sync(new[] { twin, twin.WithChroma(ChromaKind.Golden) }, 100, 300, 200);
            Assert.That((bar.Badges.Single().Copies, bar.Pop("r_sombra_gemela")), Is.EqualTo((3, 1f)), "a golden copy adds two and pops");
            bar.FlashWhere(c => c.Id == "r_sombra_gemela");
            Assert.That(bar.Glow("r_sombra_gemela"), Is.EqualTo(1f));
            bar.Update(float.NaN); Assert.That(bar.Glow("r_sombra_gemela"), Is.EqualTo(1f), "a broken frame time changes nothing");
            for (var i = 0; i < 4; i++) bar.Update(1e9f);
            Assert.That(bar.Glow("r_sombra_gemela"), Is.Zero);
            var rect = bar.Badges[0].Rect;
            Assert.That(bar.At(rect.CenterX, rect.CenterY), Is.EqualTo(0));
            Assert.That(bar.At(rect.X - 20, rect.Y), Is.Null);
        }

        [Test] public void BadgesWrapIntoRows()
        {
            var bar = new PowerBar();
            var ids = new[] { "r_rapina", "r_sombra_gemela", "r_cadena_perfecta", "r_asesina", "r_maestra_de_dagas", "r_ritmo_letal", "r_fortuna_audaz" };
            bar.Sync(ids.Select(Run).ToList(), 195, 308, 195);
            var perRow = 195 / PowerBar.Step;
            Assert.That(bar.Badges.Count, Is.EqualTo(ids.Length));
            Assert.That(bar.Badges[perRow].Rect.Y, Is.EqualTo(308 + PowerBar.Step));
            Assert.That(bar.Height(195), Is.EqualTo(2 * PowerBar.Step));
            Assert.That(bar.Badges.All(b => b.Rect.X >= 195 && b.Rect.Right <= 195 + 195));
        }

        // ------------------------------------------------------------------ what each power says

        [Test] public void EveryPowerInTheGameHasALiveDescription()
        {
            var g = Game(); var state = Fight(new CardDefinition("x", "x", 1, 1, 0));
            var font = new BitmapFont("lw16", new Tex(1, "font", 512, 512), File.ReadAllText(Path.Combine(Clips.Root, "Fonts/lw16.json")));
            foreach (var power in RunContent.Cards.Where(c => c.Kind == CardKind.Power))
            {
                Assert.That(PowerText.Known(power.Id), power.Id + " needs a line in PowerText");
                for (var copies = 1; copies <= 3; copies++)
                {
                    var tip = Tooltip.Power(g, power, copies, state);
                    Assert.That(tip.Lines.Count, Is.GreaterThanOrEqualTo(2), power.Id);
                    Assert.That(tip.Subtitle, Does.StartWith("Poder"));
                    Assert.That(tip.Tag, Is.EqualTo(copies > 1 ? "×" + copies : ""));
                    foreach (var ch in tip.AllText().Where(c => c != '\n'))
                        Assert.That(font.Has(ch), power.Id + ": the pixel font has no '" + ch + "'");
                }
            }
        }

        [Test] public void NumbersFollowTheCopies()
        {
            var g = Game(); var state = Fight(new CardDefinition("x", "x", 1, 1, 0));
            Assert.That(PowerText.Effect(g, Run("r_rapina"), 2, state), Does.Contain("6 de daño"));
            Assert.That(PowerText.Effect(g, Run("wr_piel_acero"), 3, state), Does.Contain("18 de escudo"));
            Assert.That(PowerText.Effect(g, Run("r_sombra_gemela"), 1, state), Does.Contain("2 veces"));
            Assert.That(PowerText.Effect(g, Run("r_sombra_gemela"), 2, state), Does.Contain("4 veces"));
            Assert.That(PowerText.Effect(g, Run("r_sombra_gemela"), 3, state), Does.Contain("8 veces"));
            Assert.That(PowerText.Effect(g, Run("r_sombra_gemela"), int.MaxValue, state), Does.Contain((1L << CombatState.MaxComboDoublings) + " veces"));
            Assert.That(PowerText.Stacking(Run("r_asesina"), 2), Does.Contain("no suman"));
            Assert.That(PowerText.Stacking(Run("r_rapina"), 1), Is.Null);
            Assert.That(PowerText.Stacking(Run("r_rapina").WithChroma(ChromaKind.Golden), 2), Does.Contain("dorada cuenta como 2"));
            Assert.That(PowerText.Stacking(Run("r_sombra_gemela"), 1), Does.Contain("duplicar"));
            Assert.That(PowerText.Stacking(Run("r_sombra_gemela"), 2), Does.Contain("×2 → ×4"));
            var unknown = new CardDefinition("zz_new_power", "Nuevo", 1, 0, 0, kind: CardKind.Power);
            Assert.That(PowerText.Effect(g, unknown, 2, state), Does.Contain("×2"), "a new power without a line still says it is doubled");
        }

        [Test] public void TheStateLineFollowsTheTurn()
        {
            var twin = Run("r_sombra_gemela").WithChroma(ChromaKind.Golden).WithInnate();
            var combo = new CardDefinition("combo", "combo", 0, 3, 0, effects: new[] { new EffectLayer(EffectCondition.Always, 3), new EffectLayer(EffectCondition.Combo, 5) }).WithInnate();
            var state = Fight(twin, combo);
            Play(state, "r_sombra_gemela");
            Assert.That(PowerText.State(twin, 2, state), Does.Contain("Listo"));
            Assert.That(PowerText.State(twin, 2, state), Does.Contain("4 veces"));
            Play(state, "combo");
            Assert.That(PowerText.State(twin, 2, state), Does.Contain("Ya lo usaste"));
            Assert.That(PowerText.State(Run("r_cadena_perfecta"), 1, state), Does.Contain("Cartas jugadas este turno: 2"));
            Assert.That(PowerText.State(Run("r_cadena_perfecta"), 1, state), Does.Contain("número 5"));
            Assert.That(PowerText.State(Run("r_tormenta_de_acero"), 1, state), Does.Contain("inicio de cada turno"));
            Assert.That(PowerText.State(Run("wr_piel_acero"), 1, state), Does.Contain("final de cada turno"));
            Assert.That(PowerText.State(Run("r_rapina"), 1, state), Does.Contain("todo el combate"));
        }

        [Test] public void HeroPanelsListThePowers()
        {
            var g = Game();
            var state = Fight(Run("r_sombra_gemela").WithChroma(ChromaKind.Golden).WithInnate(), Run("r_rapina").WithInnate(), new CardDefinition("x", "x", 1, 1, 0));
            Play(state, "r_sombra_gemela"); Play(state, "r_rapina");
            var quick = Tooltip.PlayerQuick(g, state, 0);
            Assert.That(quick.Lines.Any(l => l.Contains("Poderes: Sombra Gemela ×2, Rapiña")));
            var full = Tooltip.Player(g, state, 0, 0);
            Assert.That(full.Panels.Select(p => p.Key).Where(k => k.StartsWith("power:")), Is.EqualTo(new[] { "power:r_sombra_gemela", "power:r_rapina" }));
            Assert.That(full.Panels.First(p => p.Key == "power:r_sombra_gemela").Tag, Is.EqualTo("×2"));
            var card = Tooltip.Card(g, Run("r_sombra_gemela"), 0);
            Assert.That(card.Lines.Any(l => l.Contains("Cada copia lo vuelve a duplicar")));
            Assert.That(card.Lines.Any(l => l.Contains("junto a tu héroe")));
        }

        [Test] public void FacesShowTheNewTypesAndTexts()
        {
            var g = Game();
            Assert.That(CardWidget.TypeOf(g, Run("ep_poder_oculto")), Is.EqualTo("SKILL"));
            Assert.That(CardWidget.TypeOf(g, Run("m_concentracion")), Is.EqualTo("SKILL"));
            Assert.That(g.Data.Card(Run("ep_poder_oculto")).PowerTurn, Is.False);
            Assert.That(CardWidget.TypeOf(g, Run("r_sombra_gemela")), Is.EqualTo("POWER"));
            Assert.That(g.Data.Card(Run("r_sombra_gemela")).Texts, Is.EqualTo(new[] { CardDesign.TextChanges["r_sombra_gemela"] }));
            var lines = CardWidget.AbilityLines(g, Run("ep_poder_oculto"), 0, 0, 0);
            Assert.That(lines, Is.EqualTo(new[] { "Roba 4 cartas." }), "no more 'Poder permanente'");
        }

        // ------------------------------------------------------------------ the combat screen

        [Test] public void TheCombatScreenShowsBadgesAndTheirTooltipOnHover()
        {
            var g = Game();
            var state = Fight(Run("r_sombra_gemela").WithChroma(ChromaKind.Golden).WithInnate(), Run("r_rapina").WithInnate(),
                new CardDefinition("a", "a", 1, 1, 0), new CardDefinition("b", "b", 1, 1, 0), new CardDefinition("c", "c", 1, 1, 0));
            Play(state, "r_sombra_gemela"); Play(state, "r_rapina");
            var scene = new CombatScene(g, state, Array.Empty<OwnedRelic>());
            for (var i = 0; i < 90; i++) scene.Update(1f / 60f);
            g.Canvas.BeginFrame(); scene.Draw(g.Canvas); g.Canvas.EndFrame();
            var bar = (PowerBar)typeof(CombatScene).GetField("powerBar", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(scene);
            Assert.That(bar.Badges.Select(b => (b.Card.Id, b.Copies)), Is.EqualTo(new[] { ("r_sombra_gemela", 2), ("r_rapina", 1) }));
            Assert.That(bar.Badges[0].Rect.Y, Is.GreaterThan(290), "under the hero's HP bar");
            var rect = bar.Badges[0].Rect;
            scene.HandleEvent(new InputEvent(EventType.MouseMove, rect.CenterX, rect.CenterY));
            var tip = (TooltipContent)typeof(CombatScene).GetMethod("GetTooltip", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(scene, null);
            Assert.That(tip.Title, Does.Contain("Sombra Gemela"));
            Assert.That(tip.Lines[0], Does.Contain("4 veces"));
            Assert.DoesNotThrow(() => { g.Canvas.BeginFrame(); scene.Draw(g.Canvas); g.Canvas.EndFrame(); }, "tooltip + card preview draw");
            scene.HandleEvent(new InputEvent(EventType.MouseMove, 5, 5));
            Assert.That(typeof(CombatScene).GetMethod("GetTooltip", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(scene, null), Is.Null);
        }

        [Test] public void APowerPlayedInCombatPopsOnItsBadge()
        {
            var g = Game();
            var state = Fight(Run("r_rapina").WithInnate(), new CardDefinition("a", "a", 1, 1, 0));
            var scene = new CombatScene(g, state, Array.Empty<OwnedRelic>());
            for (var i = 0; i < 60; i++) scene.Update(1f / 60f);
            g.Canvas.BeginFrame(); scene.Draw(g.Canvas); g.Canvas.EndFrame();
            var bar = (PowerBar)typeof(CombatScene).GetField("powerBar", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(scene);
            Assert.That(bar.Badges, Is.Empty);
            Play(state, "r_rapina");
            g.Canvas.BeginFrame(); scene.Draw(g.Canvas); g.Canvas.EndFrame();
            Assert.That((bar.Badges.Single().Card.Id, bar.Pop("r_rapina")), Is.EqualTo(("r_rapina", 1f)));
        }
    }
}
