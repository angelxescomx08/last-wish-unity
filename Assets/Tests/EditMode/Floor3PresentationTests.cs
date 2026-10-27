using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>The floor-3 expansion on the combat screen, driven headless with the real assets: every enemy, group and boss
    /// fight with its own animated sheet and style, turns played, every enemy hovered and inspected, statuses in the glossary,
    /// and the Spanish texts of the new intents and actions.</summary>
    public sealed class Floor3PresentationTests
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

        private static CombatScene Scene(List<EnemyState> enemies, bool boss, HeroKind hero = HeroKind.Mage, bool invincible = true)
        {
            var run = new RunState(hero, 42, EnemyRoster.Expanded);
            var state = run.CreateCombat(enemies); state.Invincible = invincible;
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

        [Test] public void EveryFloorThreeEnemyFightsWithItsOwnSheet()
        {
            foreach (var d in EnemyCatalog.Floor3Definitions)
                Play(new List<EnemyState> { EnemyCatalog.Create(d.Ai, 3, "e0") }, d.Boss, 4, d.Ai);
        }

        [Test] public void EveryFloorThreeGroupAndBossFightRunsOnScreen()
        {
            foreach (var g in EnemyCatalog.Floor3Groups) Play(EnemyCatalog.CreateGroup(g.Key, 3, "g"), false, 3, g.Key);
            foreach (var b in EnemyCatalog.Floor3BossEncounters) Play(EnemyCatalog.CreateBoss(b.Key, 3), true, 6, b.Key);
        }

        [Test] public void CardsAndTurnsAgainstTheNewMechanicsShowTheirEffects()
        {
            // Cólera after each card, Congelación and Agotamiento at the turn change, Rebobinar and the Cuerda of the Autómatas
            foreach (var (ai, boss) in new[] { ("barbaro", false), ("anima", false), ("arcanista", false), ("cronomante", false), ("relojero", true) })
            {
                var enemies = boss ? EnemyCatalog.CreateBoss(ai, 3) : new List<EnemyState> { EnemyCatalog.Create(ai, 3, "e0") };
                var scene = Scene(enemies, boss, HeroKind.Rogue, invincible: false);
                var state = scene.State;
                Frames(scene, 30);
                for (var turn = 0; turn < 4 && !state.Finished; turn++)
                {
                    state.SelectEnemy(0);
                    var i = Enumerable.Range(0, state.Hand.Count).Where(j => state.CanPlay(j)).DefaultIfEmpty(-1).First();
                    if (i >= 0) { var at = i; Assert.DoesNotThrow(() => state.Play(at, 0), ai); Frames(scene, 45); }
                    scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.E));
                    Assert.DoesNotThrow(() => Frames(scene, 120), ai + " turn " + turn);
                }
            }
        }

        [Test] public void FloorThreeStatusesAreInTheGlossaryWithIcons()
        {
            var g = Game().Data.Glossary; var art = Game().Art;
            foreach (var pair in EnemyCatalog.Floor3StatusNames)
            {
                Assert.That(g.StatusName[pair.Key], Is.EqualTo(pair.Value));
                Assert.That(g.StatusTerms.ContainsKey(pair.Value), Is.True, pair.Value);
                Assert.That(art.HasIcon(g.StatusIconOf(pair.Value)), Is.True, pair.Value + " icon");
                Assert.That(g.StatusShortText(pair.Value, 3), Does.Not.Contain("{n}"));
                var debuff = EnemyCatalog.Floor3HeroDebuffs.Contains(pair.Key);
                Assert.That(g.IsBuffStatus(pair.Value), Is.EqualTo(!debuff), pair.Value);
                Assert.That(g.AllTerms.Any(t => t.Name == pair.Value), Is.EqualTo(debuff), pair.Value + " highlighted only if it is a hero debuff");
            }
            Assert.That(g.TermsIn(new[] { "Te drena el maná: Agotamiento 1 turno." }).Select(t => t.Name), Does.Contain("Agotamiento"));
            Assert.That(g.TermsIn(new[] { "Un toque helado: Congelación 2 turnos." }).Select(t => t.Name), Does.Contain("Congelación"));
        }

        [Test] public void TooltipsAndBannersDescribeTheNewIntentsInSpanish()
        {
            var ctx = Game(); var g = ctx.Data.Glossary;
            var hunter = EnemyCatalog.Create("cazador", 3, "h");
            var state = Floor1Kit.Fight(new[] { hunter }, Enumerable.Repeat(Floor1Kit.Guard(30), 8));
            state.PlayCard(0);
            Assert.That(state.Block, Is.GreaterThanOrEqualTo(30));
            var lines = Tooltip.IntentLines(ctx, hunter, hunter.HitDamage(state.HeroStatuses), state);
            Assert.That(lines.Any(l => l.Contains("Perforante: atraviesa tu escudo.")), Is.True, string.Join(" | ", lines));
            Assert.That(lines.Any(l => l.Contains("Tu escudo no lo para: perderías " + hunter.HitDamage(state.HeroStatuses) + " de vida.")), Is.True, string.Join(" | ", lines));
            Assert.That(Tooltip.LossFrom(state, hunter.HitDamage(state.HeroStatuses)), Is.EqualTo(hunter.HitDamage(state.HeroStatuses)), "the block does not count");
            Assert.That(EntityWidget.IntentExtras(hunter.Intent), Does.Contain("damage"));

            var group = EnemyCatalog.CreateGroup("vanguardia", 3, "g");
            var fight = new RunState(HeroKind.Mage, 3, EnemyRoster.Expanded).CreateCombat(group);
            var imp = group[1];
            var impLines = Tooltip.IntentLines(ctx, imp, imp.HitDamage(fight.HeroStatuses), fight);
            Assert.That(impLines.Any(l => l.Contains("+2 Inspiración")), Is.True, string.Join(" | ", impLines));

            var crono = EnemyCatalog.Create("cronomante", 3, "c");
            var timeFight = new RunState(HeroKind.Mage, 3, EnemyRoster.Expanded).CreateCombat(new[] { crono });
            timeFight.Invincible = true;
            timeFight.EndTurn(); timeFight.EndTurn();
            Assert.That(crono.Intent.Id, Is.EqualTo("rewind"));
            crono.LoseHp(9);
            var rewindLines = Tooltip.IntentLines(ctx, crono, null, timeFight);
            Assert.That(rewindLines.Any(l => l.Contains("Rebobinará: recuperará toda la vida que le quites este turno (ahora 9).")), Is.True, string.Join(" | ", rewindLines));
            Assert.That(EntityWidget.IntentExtras(crono.Intent), Does.Contain("hourglass"));

            var dragon = EnemyCatalog.Create("dragona", 3, "d");
            dragon.LoseHp(dragon.Hp - dragon.MaxHp / 2); dragon.Plan(new StatusSet(), new[] { dragon });
            Assert.That(Tooltip.IntentLines(ctx, dragon, null, null).Any(l => l.Contains("Perderá su Blindaje.")), Is.True);

            var action = new EnemyAction { MoveId = "ice_javelin", Pierced = 10, Rewound = 48, Healed = 48, RageSpent = 3, WoundUp = "boss_f3" };
            var (_, title, detail) = ActionBanners.Describe(g, "Cazador Glacial", hunter.Intent, action);
            Assert.That(title, Is.EqualTo("Cazador Glacial usa Jabalina de Hielo"));
            foreach (var part in new[] { "atraviesa tu escudo (10)", "rebobina el tiempo: recupera 48 de vida", "descarga 3 de Fuerza de su Cólera", "da cuerda al Relojero" })
                Assert.That(detail, Does.Contain(part));

            var wisp = EnemyCatalog.Create("anima", 3, "w");
            var tip = Tooltip.Intent(ctx, wisp, null, null);
            Assert.That(tip.Panels.Any(p => p.Title == "Congelación"), Is.True, "the glossary panel explains Congelación");
            var panel = Tooltip.StatusPanel(ctx, "Agotamiento", 2, true);
            Assert.That(panel.Lines.Any(l => l.Contains("Dura 2 turnos")), Is.True);
            Assert.That(Tooltip.PairLine(EnemyCatalog.CreateGroup("tormenta", 3, "g")[0]), Is.EqualTo("  Grupo: Tormenta Blanca."));
        }

        [Test] public void PruebasListsTheFloorThreeContent()
        {
            var labels = Enumerable.Range(0, EnemyCatalog.BossKeys(EnemyRoster.Expanded).Count + 1).Select(i => EnemyCatalog.BossLabel(i, EnemyRoster.Expanded)).ToList();
            Assert.That(labels.Last(), Is.EqualTo("La Dragona de Escarcha (piso 3)"));
            var data = new Dictionary<string, object> { { "forced_boss", 12L }, { "forced_elite", 14L }, { "forced_encounter", 54L } };
            DevSettings.Apply(data);
            Assert.That((Tuning.Current.ForcedBoss, Tuning.Current.ForcedElite, Tuning.Current.ForcedEncounter), Is.EqualTo((12, 14, 54)), "saved values are not clamped away");
        }
    }
}
