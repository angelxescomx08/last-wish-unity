using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>Event room interface (2026-11-04 redesign): every stage and every result of the 150 events fits its boxes
    /// (title, story, choice cards, chips, result panel), nothing overlaps, every phase draws and the cards answer the mouse
    /// and the keys (null renderer, real fonts).</summary>
    public sealed class EventLayoutTests
    {
        private static Art art;
        private static NullRender render;
        private static GameData data;

        [SetUp] public void ResetTuning() => Tuning.Reset();
        [TearDown] public void RestoreTuning() => Tuning.Reset();

        private static GameContext Game()
        {
            if (art == null) { render = new NullRender(); art = new Art(render, new FileAssets()); art.LoadCore(); data = new GameData(new FileAssets()); }
            return new GameContext(new Canvas(render), art, new SoundPlayer(new SilentAudio(), .7f, 0f), new Preferences(), null) { Data = data };
        }

        private static void SetGold(RunState run, int gold) => typeof(RunState).GetProperty("Gold").SetValue(run, gold);
        private static void SetHp(RunState run, int hp) => typeof(RunState).GetProperty("Hp").SetValue(run, hp);

        /// <summary>A run where <paramref name="def"/> can open: its floor and its story flag reached through the real parent events.</summary>
        private static RunState RunFor(EventDefinition def, long seed = 42, HeroKind hero = HeroKind.Warrior)
        {
            var run = new RunState(hero, seed);
            Unlock(run, def);
            typeof(RunState).GetProperty("Floor").SetValue(run, def.MinFloor);
            return run;
        }

        /// <summary>The event room of <paramref name="def"/> (the session is opened first so the room shows that event).</summary>
        private static EventScene Open(RunState run, EventDefinition def)
        {
            run.OpenEvent("room", def.Id);
            return new EventScene(Game(), run, "room");
        }

        private static void Unlock(RunState run, EventDefinition def)
        {
            if (def.RequiredFlag.Length == 0 || run.EventFlags.Contains(def.RequiredFlag)) return;
            var parent = EventCatalog.All.First(e => e.Stages.SelectMany(s => s.Choices).Any(c => c.SetFlag == def.RequiredFlag));
            Unlock(run, parent);
            typeof(RunState).GetProperty("Floor").SetValue(run, parent.MinFloor);
            SetGold(run, 5000); run.Heal(int.MaxValue);
            var session = run.OpenEvent("unlock_" + parent.Id, parent.Id);
            session.Choose(parent.Stages[0].Choices.ToList().FindIndex(c => c.SetFlag == def.RequiredFlag));
        }

        /// <summary>One scene per reachable stage of <paramref name="def"/> (following the choices that lead on).</summary>
        private static IEnumerable<(EventScene scene, RunState run)> Stages(EventDefinition def, int gold = 5000, int hp = -1)
        {
            var paths = new Queue<List<int>>(); paths.Enqueue(new List<int>()); var seen = new HashSet<int>();
            while (paths.Count > 0)
            {
                var path = paths.Dequeue();
                var run = RunFor(def); SetGold(run, 5000);
                var scene = Open(run, def);
                Assert.That(scene.Session.Definition.Id, Is.EqualTo(def.Id));
                foreach (var pick in path) scene.Session.Choose(pick);
                if (scene.Session.Completed || !seen.Add(scene.Session.StageIndex)) continue;
                SetGold(run, gold); if (hp > 0) SetHp(run, Math.Min(hp, run.MaxHp));
                yield return (scene, run);
                var stage = scene.Session.Stage;
                for (var i = 0; i < stage.Choices.Count; i++)
                    if (stage.Choices[i].NextStage >= 0) paths.Enqueue(path.Concat(new[] { i }).ToList());
            }
        }

        private static string Words(IEnumerable<string> lines) => string.Join(" ", string.Join(" ", lines).Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        private static bool Inside(RectF inner, RectF outer) => inner.X >= outer.X - 0.01f && inner.Y >= outer.Y - 0.01f && inner.Right <= outer.Right + 0.01f && inner.Bottom <= outer.Bottom + 0.01f;
        private static readonly RectF Column = new RectF(EventScene.ColX, EventScene.AreaTop, EventScene.ColW, EventScene.AreaBottom - EventScene.AreaTop);

        private static void AssertChoices(EventScene scene, string label)
        {
            var lay = scene.Layout();
            Assert.That(lay.Title, Is.EqualTo(scene.Session.Definition.Name), label + ": whole title");
            Assert.That(lay.TitleRect.X, Is.GreaterThan(PauseMenu.PauseButton.Right + 64 + 8), label + ": title clear of the pause and hero buttons");
            Assert.That(lay.TitleRect.Right, Is.LessThan(1268 - 160), label + ": title clear of the gold counter");
            Assert.That(Words(lay.Body), Is.EqualTo(Words(new[] { scene.Session.Stage.Body })), label + ": every word of the story");
            Assert.That(Inside(lay.Story, Column), Is.True, label + ": story " + lay.Story);
            Assert.That(lay.Choices.Count, Is.EqualTo(scene.Session.Stage.Choices.Count), label);
            Assert.That(Inside(lay.Plate, new RectF(0, EventScene.AreaTop, Canvas.Width, EventScene.AreaBottom - EventScene.AreaTop)), Is.True, label + ": plate");
            var previous = lay.Story;
            foreach (var c in lay.Choices)
            {
                var name = label + " #" + (c.Index + 1);
                Assert.That(Inside(c.Rect, Column), Is.True, name + ": card on screen " + c.Rect);
                Assert.That(c.Rect.Y, Is.GreaterThanOrEqualTo(previous.Bottom), name + ": below the box before it");
                previous = c.Rect;
                var inner = c.Rect.Inflate(-24, -16);
                Assert.That(Inside(c.LabelRect, inner), Is.True, name + ": label inside");
                if (c.Cost.HasValue)
                {
                    Assert.That(Inside(c.CostRect, inner), Is.True, name + ": cost inside");
                    Assert.That(c.LabelRect.Right, Is.LessThan(c.CostRect.X - 8), name + ": label clear of the cost");
                }
                foreach (var p in c.Pieces) Assert.That(Inside(p.Rect, inner), Is.True, name + ": chip " + p.Chip.Text + " inside");
                for (var i = 0; i < c.Pieces.Count; i++)
                    for (var j = i + 1; j < c.Pieces.Count; j++)
                        Assert.That(c.Pieces[i].Rect.Intersects(c.Pieces[j].Rect), Is.False, name + ": chips " + c.Pieces[i].Chip.Text + " / " + c.Pieces[j].Chip.Text);
                if (c.Blocked) Assert.That(Inside(c.ReasonRect, inner) && c.Reason.Length > 0, Is.True, name + ": reason inside");
                if (c.Note.Count > 0) Assert.That(c.NoteY + c.Note.Count * EventScene.SmallLine, Is.LessThanOrEqualTo(c.Rect.Bottom - 8), name + ": note inside");
                Assert.That(c.Pieces.Count > 0 || c.Note.Count > 0 || c.Compact, Is.True, name + ": says what it does");
            }
        }

        [Test] public void EveryStageOfEveryEventFitsItsBoxes()
        {
            var stages = 0;
            foreach (var def in EventCatalog.All)
                foreach (var (scene, _) in Stages(def))
                { AssertChoices(scene, def.Id + " stage " + scene.Session.StageIndex); stages++; }
            Assert.That(stages, Is.EqualTo(EventCatalog.All.Sum(d => d.Stages.Count)), "every stage reached");
        }

        [Test] public void PoorAndHurtHeroesSeeWhyAChoiceIsBlocked()
        {
            var blocked = 0;
            foreach (var def in EventCatalog.All)
                foreach (var (scene, run) in Stages(def, 0, 1))
                {
                    AssertChoices(scene, def.Id + " poor");
                    foreach (var c in scene.Layout().Choices)
                    {
                        Assert.That(c.Blocked, Is.EqualTo(scene.Session.BlockReason(c.Index).Length > 0), def.Id);
                        if (!c.Blocked) continue;
                        blocked++;
                        var choice = scene.Session.Stage.Choices[c.Index];
                        if (choice.GoldCost > 0) Assert.That(c.Reason, Is.EqualTo("Te faltan " + choice.GoldCost + " de oro."), def.Id);
                    }
                    Assert.That(scene.Layout().Choices.Any(c => !c.Blocked), Is.True, def.Id + ": leaving is always possible");
                }
            Assert.That(blocked, Is.GreaterThan(100));
        }

        [Test] public void EveryResultFitsItsPanelAndListsWhatChanged()
        {
            foreach (var def in EventCatalog.All)
                for (var i = 0; i < def.Stages[0].Choices.Count; i++)
                {
                    var run = RunFor(def); SetGold(run, 5000);
                    var scene = Open(run, def);
                    var label = def.Id + " #" + (i + 1);
                    var gold = run.Gold; var hp = run.Hp;
                    scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.D1 + i));
                    if (!scene.Session.Completed) continue;
                    var lay = scene.Layout();
                    Assert.That(lay.Result, Is.True, label);
                    Assert.That(Inside(lay.ResultPanel, Column), Is.True, label + ": panel " + lay.ResultPanel);
                    Assert.That(Inside(lay.Continue, Column), Is.True, label + ": continue " + lay.Continue);
                    Assert.That(lay.Continue.Y, Is.GreaterThan(lay.ResultPanel.Bottom), label);
                    Assert.That(lay.ResultTitle, Is.EqualTo(def.Stages[0].Choices[i].Label), label + ": whole title");
                    Assert.That(Words(lay.ResultLines), Is.EqualTo(Words(new[] { scene.Session.LastEffect.Text })), label + ": every word of the outcome");
                    foreach (var p in lay.ResultPieces) Assert.That(Inside(p.Rect, lay.ResultPanel.Inflate(-24, -16)), Is.True, label + ": " + p.Chip.Text);
                    var texts = lay.ResultPieces.Select(p => p.Chip.Text).ToList();
                    if (run.Gold != gold) Assert.That(texts, Has.Some.EqualTo((run.Gold > gold ? "+" : "−") + Math.Abs(run.Gold - gold) + " oro"), label);
                    if (run.Hp != hp) Assert.That(texts, Has.Some.EndsWith(Math.Abs(run.Hp - hp) + " vida"), label);
                    if (def.Stages[0].Choices[i].SuccessPercent < 100) Assert.That(lay.Outcome.HasValue, Is.True, label + ": risky choices say how it went");
                }
        }

        [Test] public void EveryPhaseDrawsWithHoverAndPreviews()
        {
            var game = Game();
            foreach (var def in EventCatalog.All)
            {
                var run = RunFor(def); SetGold(run, 5000);
                var scene = Open(run, def);
                for (var step = 0; step < 4 && !scene.Session.Completed; step++)
                {
                    var lay = scene.Layout();
                    foreach (var c in lay.Choices)
                    {
                        scene.HandleEvent(new InputEvent(EventType.MouseMove, c.Rect.CenterX, c.Rect.CenterY));
                        foreach (var p in c.Pieces.Where(p => p.Chip.HasPreview))
                        {
                            scene.HandleEvent(new InputEvent(EventType.MouseMove, p.Rect.CenterX, p.Rect.CenterY));
                            Assert.That(scene.HoveredChip(lay).HasValue, Is.True, def.Id + ": " + p.Chip.Text);
                            if (p.Chip.Card != null)
                            {
                                var box = EventScene.PreviewBox(p.Chip, p.Rect);
                                Assert.That(Inside(box, new RectF(0, 0, Canvas.Width, Canvas.Height)), Is.True, def.Id + ": preview on screen");
                            }
                            game.Canvas.BeginFrame(); scene.Draw(game.Canvas); game.Canvas.EndFrame();
                        }
                    }
                    for (var t = 0; t < 6; t++) { scene.Update(0.08f); game.Canvas.BeginFrame(); scene.Draw(game.Canvas); game.Canvas.EndFrame(); }
                    var next = lay.Choices.FindIndex(c => scene.Session.Stage.Choices[c.Index].NextStage >= 0);
                    scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.D1 + (next >= 0 ? next : 0)));
                }
                for (var t = 0; t < 6; t++) { scene.Update(0.08f); game.Canvas.BeginFrame(); scene.Draw(game.Canvas); game.Canvas.EndFrame(); }
                Assert.That(scene.Session.Completed, Is.True, def.Id);
            }
        }

        [Test] public void ClickingACardChoosesItAndContinueLeaves()
        {
            var def = EventCatalog.Find("scattered_coins");
            var run = RunFor(def); SetGold(run, 10);
            var scene = Open(run, def);
            var card = scene.ChoiceRect(0);
            scene.HandleEvent(new InputEvent(EventType.MouseMove, card.CenterX, card.CenterY));
            scene.HandleEvent(new InputEvent(EventType.MouseDown, card.CenterX, card.CenterY, 1));
            Assert.That(scene.Session.Completed, Is.True);
            Assert.That(run.Gold, Is.EqualTo(32));
            Assert.That(scene.Cleared, Is.False);
            var button = scene.ContinueRect;
            scene.HandleEvent(new InputEvent(EventType.MouseDown, button.X - 4, button.Y - 4, 1));
            Assert.That(scene.Cleared, Is.False, "outside the button");
            scene.HandleEvent(new InputEvent(EventType.MouseDown, button.CenterX, button.CenterY, 1));
            Assert.That(scene.Cleared, Is.True);
        }

        [Test] public void ABlockedChoiceExplainsItselfAndChangesNothing()
        {
            var def = EventCatalog.Find("relic_peddler");
            var run = RunFor(def); SetGold(run, 30);
            var scene = Open(run, def);
            var card = scene.ChoiceRect(0);
            scene.HandleEvent(new InputEvent(EventType.MouseDown, card.CenterX, card.CenterY, 1));
            scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.Return));
            Assert.That(scene.Session.Completed, Is.False);
            Assert.That(run.Gold, Is.EqualTo(30));
            Assert.That(run.OwnedRelics.Count, Is.Zero);
            Assert.That(scene.Layout().Choices[0].Reason, Is.EqualTo("Te faltan 55 de oro."));
        }

        [Test] public void KeysMoveTheFocusAndConfirmTheFocusedChoice()
        {
            var def = EventCatalog.Find("forgotten_chest");
            var run = RunFor(def); SetGold(run, 0);
            var scene = Open(run, def);
            scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.Down));
            scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.Down));
            scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.Up));
            scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.Return));
            Assert.That(scene.Session.Completed, Is.True);
            Assert.That(scene.Session.LastChoice.Label, Is.EqualTo("Recoger la bolsa de al lado"));
            Assert.That(run.Gold, Is.EqualTo(12));
            scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.Return));
            Assert.That(scene.Cleared, Is.True);
        }

        [Test] public void MultiStageEventsShowWhatTheLastStepSettled()
        {
            var def = EventCatalog.Find("masked_bazaar");
            var run = RunFor(def); SetGold(run, 100);
            var scene = Open(run, def);
            Assert.That(scene.Layout().Previous, Is.Empty);
            scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.D1));
            Assert.That(scene.Session.StageIndex, Is.EqualTo(1));
            var lay = scene.Layout();
            Assert.That(Words(lay.Previous), Is.EqualTo(Words(new[] { def.Stages[0].Choices[0].Success.Text })));
            Assert.That(lay.Choices.Count, Is.EqualTo(3));
            AssertChoices(scene, "bazaar stage 2");
        }

        [Test] public void ChipsNameTheCardAndRelicAtStake()
        {
            var bazaar = EventCatalog.Find("masked_bazaar"); var scene = Open(RunFor(bazaar), bazaar);
            var upgrade = scene.EffectChips(new EventEffect("x", upgrade: true)).Single();
            Assert.That(upgrade.Card, Is.Not.Null); Assert.That(upgrade.After.Name, Is.EqualTo(upgrade.Card.Name + "+"));
            Assert.That(upgrade.Text, Is.EqualTo("Mejora " + upgrade.Card.Name));
            var relic = scene.EffectChips(new EventEffect("x", relic: "rabbit_foot")).Single();
            Assert.That(relic.Relic, Is.Not.Null); Assert.That(relic.Text, Is.EqualTo("Reliquia: " + relic.Relic.Name));
            var all = scene.EffectChips(new EventEffect("x", gold: 5, heal: 6, maxHp: 7, luck: 8, damage: 9, remove: true, card: true));
            Assert.That(all.Select(c => c.Text), Is.EqualTo(new[] { "+5 oro", "+6 vida", "+7 vida máx.", "+8 suerte", "hasta −9 vida", "Pierdes " + scene.EffectChips(new EventEffect("x", remove: true))[0].Card.Name, "Carta nueva" }));
            Assert.That(EventScene.CostChip(new EventChoice("a", new EventEffect("x"), goldCost: 1000000)).Value.Text, Is.EqualTo("−1000000 oro"));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(999)] [TestCase(int.MaxValue)]
        public void HugeAmountsStillFitTheCard(int gold)
        {
            var def = EventCatalog.Find("relic_peddler");
            var run = RunFor(def); SetGold(run, gold);
            var scene = Open(run, def);
            AssertChoices(scene, "gold " + gold);
        }
    }
}
