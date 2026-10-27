using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>The floor-2 expansion on the combat screen, driven headless with the real assets: every enemy, group and boss
    /// fight with its own animated sheet and style, turns played, every enemy hovered and inspected, statuses in the glossary,
    /// and the Spanish texts of the new intents and actions.</summary>
    public sealed class Floor2PresentationTests
    {
        [SetUp] public void ResetTuning() => Tuning.Reset();
        [TearDown] public void RestoreTuning() => Tuning.Reset();

        private static GameContext game;
        private static GameContext Game()
        {
            if (game != null) return game;
            var render = new NullRender(); var art = new Art(render, new FileAssets()); art.LoadCore();
            return game = new GameContext(new Canvas(render), art, new SoundPlayer(null), new Preferences(), null) { Data = new GameData(new FileAssets()) };
        }

        private static CombatScene Scene(List<EnemyState> enemies, bool boss, HeroKind hero = HeroKind.Mage)
        {
            var run = new RunState(hero, 42, EnemyRoster.Expanded);
            var state = run.CreateCombat(enemies); state.Invincible = true;
            return new CombatScene(Game(), state, run.OwnedRelics, boss);
        }

        private static void Frames(CombatScene scene, int n)
        {
            var g = Game();
            for (var i = 0; i < n; i++)
            {
                scene.Update(1f / 30f);
                if (i % 10 == 0) { g.Canvas.BeginFrame(); scene.Draw(g.Canvas); g.Canvas.EndFrame(); }
            }
        }

        private static List<IRect> Rects(CombatScene scene) =>
            (List<IRect>)typeof(CombatScene).GetField("enemyRects", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(scene);
        private static TooltipContent Tip(CombatScene scene) =>
            (TooltipContent)typeof(CombatScene).GetMethod("GetTooltip", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(scene, null);

        /// <summary>Hovers every enemy (quick look), right-clicks it (inspect panel) and closes the panel, drawing each step.</summary>
        private static void LookAtEveryEnemy(CombatScene scene, string where)
        {
            var g = Game();
            foreach (var r in Rects(scene).ToList())
            {
                scene.HandleEvent(new InputEvent(EventType.MouseMove, r.CenterX, r.CenterY));
                Assert.DoesNotThrow(() => { g.Canvas.BeginFrame(); scene.Draw(g.Canvas); g.Canvas.EndFrame(); }, where + " quick look");
                scene.HandleEvent(new InputEvent(EventType.MouseDown, r.CenterX, r.CenterY, 3));
                scene.HandleEvent(new InputEvent(EventType.MouseUp, r.CenterX, r.CenterY, 3));
                Assert.DoesNotThrow(() => { g.Canvas.BeginFrame(); scene.Draw(g.Canvas); g.Canvas.EndFrame(); }, where + " inspect panel");
                scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.Escape));
            }
            scene.HandleEvent(new InputEvent(EventType.MouseMove, 5, 5));
        }

        private static void Play(List<EnemyState> enemies, bool boss, int turns, string where)
        {
            var scene = Scene(enemies, boss);
            var state = scene.State;
            Frames(scene, 40);
            foreach (var e in state.Enemies)
            {
                var sheet = Game().Art.SheetForEnemy(e.Definition.Name);
                Assert.That(sheet, Is.Not.Null, e.Definition.Name + " needs its own animated sheet");
                Assert.That(EnemyAnimator.Styles.ContainsKey(sheet.Id), Is.True, sheet.Id + " needs a particle style");
                Assert.That(sheet.IsBoss, Is.EqualTo(EnemyField.IsLarge(e.Definition)), e.Definition.Name + " size");
            }
            Assert.That(scene.EnemyAnimators.Count, Is.EqualTo(state.Enemies.Count), where);
            LookAtEveryEnemy(scene, where);
            for (var t = 0; t < turns && !state.Finished; t++)
            {
                scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.E));
                Frames(scene, 120);
                Assert.That(state.Enemies.Count(e => e.Alive), Is.LessThanOrEqualTo(EnemyField.MaxAlive), where);
                Assert.That(scene.EnemyAnimators.Count, Is.EqualTo(scene.ListedEnemies().Count), where + ": every arrival gets an animator");
            }
            LookAtEveryEnemy(scene, where + " after " + turns + " turns");
        }

        [Test] public void EveryFloorTwoEnemyFightsWithItsOwnSheet()
        {
            foreach (var d in EnemyCatalog.Floor2Definitions)
                Play(new List<EnemyState> { EnemyCatalog.Create(d.Ai, 2, "e0") }, d.Boss, 4, d.Ai);
        }

        [Test] public void EveryFloorTwoGroupAndBossFightRunsOnScreen()
        {
            foreach (var g in EnemyCatalog.Floor2Groups) Play(EnemyCatalog.CreateGroup(g.Key, 2, "g"), false, 3, g.Key);
            foreach (var b in EnemyCatalog.Floor2BossEncounters) Play(EnemyCatalog.CreateBoss(b.Key, 2), true, 6, b.Key);
        }

        [Test] public void CardsAgainstTheNewMechanicsShowTheirEffects()
        {
            // Reflejo, cut heads, Rencor and a vanished Ilusión on screen: hits on the hero, floating texts and history entries
            foreach (var ai in new[] { "reina_espejos", "hidra", "sombra", "ilusionista" })
            {
                var scene = Scene(new List<EnemyState> { EnemyCatalog.Create(ai, 2, "e0") }, EnemyCatalog.Definition(ai).Boss);
                var state = scene.State;
                Frames(scene, 30);
                if (ai == "ilusionista") { scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.E)); Frames(scene, 120); }
                state.SelectEnemy(0);
                for (var k = 0; k < 3 && !state.Finished; k++)
                {
                    var i = Enumerable.Range(0, state.Hand.Count).Where(j => state.CanPlay(j)).DefaultIfEmpty(-1).First();
                    if (i < 0) break;
                    Assert.DoesNotThrow(() => state.Play(i, 0), ai);
                    Frames(scene, 45);
                }
                Assert.DoesNotThrow(() => Frames(scene, 60), ai);
            }
        }

        [Test] public void FloorTwoStatusesAreInTheGlossaryWithIcons()
        {
            var g = Game().Data.Glossary; var art = Game().Art;
            foreach (var pair in EnemyCatalog.Floor2StatusNames)
            {
                Assert.That(g.StatusName[pair.Key], Is.EqualTo(pair.Value));
                Assert.That(g.StatusTerms.ContainsKey(pair.Value), Is.True, pair.Value);
                Assert.That(art.HasIcon(g.StatusIconOf(pair.Value)), Is.True, pair.Value + " icon");
                Assert.That(g.StatusShortText(pair.Value, 3), Does.Not.Contain("{n}"));
                var debuff = EnemyCatalog.Floor2HeroDebuffs.Contains(pair.Key);
                Assert.That(g.IsBuffStatus(pair.Value), Is.EqualTo(!debuff), pair.Value);
                Assert.That(g.AllTerms.Any(t => t.Name == pair.Value), Is.EqualTo(debuff), pair.Value + " highlighted only if it is a hero debuff");
            }
            Assert.That(g.TermsIn(new[] { "Se te pega: Sangrado 2 turnos." }).Select(t => t.Name), Does.Contain("Sangrado"));
            Assert.That(g.TermsIn(new[] { "Un toque necrótico: Necrosis 2 turnos." }).Select(t => t.Name), Does.Contain("Necrosis"));
        }

        [Test] public void TooltipsAndBannersDescribeTheNewIntentsInSpanish()
        {
            var ctx = Game(); var g = ctx.Data.Glossary;
            var priest = EnemyCatalog.Create("sacerdote", 2, "p");
            var state = new RunState(HeroKind.Warrior, 3, EnemyRoster.Expanded).CreateCombat(new[] { priest });
            state.Invincible = true;
            state.EndTurn(); state.EndTurn();
            Assert.That(priest.Intent.Id, Is.EqualTo("dark_offering"));
            var lines = Tooltip.IntentLines(ctx, priest, null, state);
            Assert.That(lines.Any(l => l.Contains("Sacrificará a su esbirro más fuerte (Fanático del Abismo)")), Is.True, string.Join(" | ", lines));
            var queen = EnemyCatalog.Create("reina_espejos", 2, "q");
            queen.Plan(new StatusSet(), new[] { queen }); queen.Plan(new StatusSet(), new[] { queen });
            Assert.That(queen.Intent.Id, Is.EqualTo("cursed_mirror"));
            Assert.That(Tooltip.IntentLines(ctx, queen, null, null).Any(l => l.Contains("Te pasará su Veneno, Débil y Vulnerable")), Is.True);
            Assert.That(EntityWidget.IntentExtras(queen.Intent), Does.Contain("status_debuff"));
            var action = new EnemyAction { MoveId = "dark_offering", Devoured = "x", DevouredStrength = 5, RitualStrength = 2, Regenerated = 4, DebuffsReturned = 3 };
            var (_, title, detail) = ActionBanners.Describe(g, "Sumo Sacerdote del Abismo", priest.Intent, action);
            Assert.That(title, Is.EqualTo("Sumo Sacerdote del Abismo usa Ofrenda Oscura"));
            foreach (var part in new[] { "sacrifica a un esbirro y gana 5 de Fuerza", "su Rito le da 2 de Fuerza", "cicatriza 4 de vida", "te devuelve sus perjuicios" })
                Assert.That(detail, Does.Contain(part));
            var leech = EnemyCatalog.Create("sanguijuela", 2, "l");
            var tip = Tooltip.Intent(ctx, leech, null, null);
            Assert.That(tip.Panels.Any(p => p.Title == "Sangrado"), Is.True, "the glossary panel explains Sangrado");
            var panel = Tooltip.StatusPanel(ctx, "Sangrado", 2, true);
            Assert.That(panel.Lines.Any(l => l.Contains("Dura 2 turnos")), Is.True);
            Assert.That(Tooltip.PairLine(EnemyCatalog.CreateGroup("teatro", 2, "g")[0]), Is.EqualTo("  Grupo: Teatro de Sombras."));
        }

        [Test] public void PruebasListsTheFloorTwoContent()
        {
            var labels = Enumerable.Range(0, EnemyCatalog.BossKeys(EnemyRoster.Expanded).Count + 1).Select(i => EnemyCatalog.BossLabel(i, EnemyRoster.Expanded)).ToList();
            Assert.That(labels[9], Is.EqualTo("La Reina de los Espejos (piso 2)"));
            var data = new Dictionary<string, object> { { "forced_boss", 9L }, { "forced_elite", 11L }, { "forced_encounter", 42L } };
            DevSettings.Apply(data);
            Assert.That((Tuning.Current.ForcedBoss, Tuning.Current.ForcedElite, Tuning.Current.ForcedEncounter), Is.EqualTo((9, 11, 42)), "saved values are not clamped away");
        }
    }
}
