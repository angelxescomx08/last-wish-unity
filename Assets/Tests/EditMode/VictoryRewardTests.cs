using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>Victory rewards by steps (2026-11-08 redesign): the tracker lists the right steps for normal fights, elites
    /// and bosses with the gold already counted; the gold intro and every arrival only hurry on a click or key; a card is
    /// picked first and added on a second click / Enter (or skipped), then the elite's relic is optional; the boss pack goes
    /// through the scene manager and comes back to the tracker; the scene manager applies exactly what was taken; empty
    /// offers only let you continue; everything fits the screen and every phase draws.</summary>
    public sealed class VictoryRewardTests
    {
        private static Art art;
        private static NullRender render;
        private static GameData data;

        [SetUp] public void ResetTuning() => Tuning.Reset();
        [TearDown] public void RestoreTuning() => Tuning.Reset();

        private static GameContext Game(bool withData = true)
        {
            if (art == null) { render = new NullRender(); art = new Art(render, new FileAssets()); art.LoadCore(); data = new GameData(new FileAssets()); }
            return new GameContext(new Canvas(render), art, new SoundPlayer(new SilentAudio(), .7f, 0f), new Preferences(), null) { Data = withData ? data : null };
        }

        private static RunState Run(HeroKind hero = HeroKind.Warrior, int seed = 5) => new RunState(hero, seed, EnemyRoster.Expanded);
        private static List<OfferedCard> Offers(int n, int skip = 0) => RunContent.Cards.Skip(skip).Take(n).Select(c => new OfferedCard(c.Copy())).ToList();
        private static RelicOffer Relic(string id = "r_vial", ChromaKind chroma = ChromaKind.None) => new RelicOffer(RunContent.RelicById(id), chroma);
        private static List<RelicOffer> BossRelics() => new List<RelicOffer> { Relic("r_vial"), Relic("r_clover", ChromaKind.Golden), Relic("r_interest") };

        private static CombatRewardScene Elite(GameContext g, RunState run, int cards = 3, RelicOffer relic = null) => new CombatRewardScene(g, run, 40, Offers(cards), relic ?? Relic(), elite: true);
        private static CombatRewardScene Normal(GameContext g, RunState run, int cards = 3) => new CombatRewardScene(g, run, 18, Offers(cards));

        private static void Tick(Scene s, float seconds, float step = 0.05f) { for (var t = 0f; t < seconds; t += step) s.Update(step); }
        private static void Click(Scene s, RectF r) => s.HandleEvent(new InputEvent(EventType.MouseDown, r.CenterX, r.CenterY, 1));
        private static void Move(Scene s, RectF r) => s.HandleEvent(new InputEvent(EventType.MouseMove, r.CenterX, r.CenterY));
        private static void Press(Scene s, Key key) => s.HandleEvent(new InputEvent(EventType.KeyDown, key: key));
        private static void Draw(GameContext g, Scene s, int frames = 1)
        {
            for (var k = 0; k < frames; k++) { s.Update(0.05f); g.Time += 0.05f; g.Canvas.BeginFrame(); s.Draw(g.Canvas); g.Canvas.EndFrame(); }
        }
        private static bool Overlap(RectF a, RectF b) => a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
        private static bool OnScreen(RectF r) => r.X >= 0 && r.Y >= 0 && r.Right <= Canvas.Width && r.Bottom <= Canvas.Height;

        // ------------------------------------------------------------------ steps

        [Test] public void EachFightListsItsSteps()
        {
            var g = Game(); var run = Run();
            Assert.That(Normal(g, run).Steps.Select(s => s.Kind), Is.EqualTo(new[] { RewardStepKind.Gold, RewardStepKind.Card }));
            Assert.That(Elite(g, run).Steps.Select(s => s.Kind), Is.EqualTo(new[] { RewardStepKind.Gold, RewardStepKind.Card, RewardStepKind.Relic }));
            var noRelic = new CombatRewardScene(g, run, 40, Offers(3), null, elite: true);
            Assert.That(noRelic.Steps.Select(s => s.Kind), Is.EqualTo(new[] { RewardStepKind.Gold, RewardStepKind.Card }), "an elite whose relic pool is empty");
            Assert.That(noRelic.Kind, Is.EqualTo(VictoryKind.Elite));
            var boss = new BossRewardScene(g, run, 99, BossRelics());
            Assert.That(boss.Steps.Select(s => s.Kind), Is.EqualTo(new[] { RewardStepKind.Gold, RewardStepKind.Pack, RewardStepKind.Relic }));
            Assert.That((Normal(g, run).Title, Elite(g, run).Title, boss.Title), Is.EqualTo(("¡Victoria!", "¡Élite derrotada!", "¡Jefe derrotado!")));
        }

        [Test] public void TheGoldIsCountedFromTheStartAndTheSecondStepIsCurrent()
        {
            var g = Game(); var run = Run();
            run.GainGold(10);      // sets LastInterest to whatever the run's relics give (none here)
            var scene = Elite(g, run);
            Assert.That(scene.Steps[0].State, Is.EqualTo(RewardStepState.Done));
            Assert.That(scene.Steps[0].Result, Is.EqualTo("+40 oro"));
            Assert.That(scene.Steps[1].State, Is.EqualTo(RewardStepState.Current));
            Assert.That(scene.Steps[2].State, Is.EqualTo(RewardStepState.Upcoming));
            Assert.That(scene.Current, Is.EqualTo(1));
            Assert.That(scene.RibbonText, Is.EqualTo("Paso 2 de 3 · Elige una carta"));
            Assert.That(VictoryRewardScene.GoldText(45, 4), Is.EqualTo("+45 oro +4 interés"));
            Assert.That(VictoryRewardScene.GoldText(0, 0), Is.EqualTo("+0 oro"));
        }

        [Test] public void ClicksAndKeysDuringTheIntroOnlyHurryIt()
        {
            var g = Game(); var scene = Elite(g, Run());
            Assert.That(scene.State, Is.EqualTo(VictoryRewardScene.Phase.Intro));
            var card = scene.Layout().Cards[0];
            Click(scene, card);
            Assert.That(scene.State, Is.EqualTo(VictoryRewardScene.Phase.Enter), "the first click skips the gold intro");
            Assert.That(scene.Selected, Is.EqualTo(-1));
            Press(scene, Key.Return);
            Assert.That(scene.State, Is.EqualTo(VictoryRewardScene.Phase.Choosing), "the next press ends the arrival");
            Assert.That(scene.Selected, Is.EqualTo(-1), "nothing was picked while hurrying");
            Assert.That(scene.Steps[1].State, Is.EqualTo(RewardStepState.Current));
            Assert.That(scene.Cleared, Is.False);
        }

        [Test] public void TheShowEndsOnItsOwnAndThenWaitsForYou()
        {
            var g = Game(); var scene = Elite(g, Run());
            Tick(scene, VictoryRewardScene.IntroT + VictoryRewardScene.EnterT + 3 * VictoryRewardScene.Stagger + 0.2f);
            Assert.That(scene.State, Is.EqualTo(VictoryRewardScene.Phase.Choosing));
            Tick(scene, 60f);
            Assert.That(scene.State, Is.EqualTo(VictoryRewardScene.Phase.Choosing), "a choice never times out");
            Assert.That(scene.Cleared, Is.False);
        }

        // ------------------------------------------------------------------ cards

        [Test] public void AClickPicksACardAndASecondClickAddsIt()
        {
            var g = Game(); var run = Run(); var scene = Elite(g, run); scene.SkipIntro();
            var deck = run.Deck.Count;
            Click(scene, scene.CardRects[1]);
            Assert.That(scene.Selected, Is.EqualTo(1));
            Assert.That(scene.PrimaryEnabled, Is.True);
            Assert.That(scene.PrimaryLabel, Is.EqualTo("Añadir al mazo"));
            Click(scene, scene.CardRects[2]);
            Assert.That(scene.Selected, Is.EqualTo(2), "another click changes the pick");
            Click(scene, scene.CardRects[2]);
            Assert.That(scene.State, Is.EqualTo(VictoryRewardScene.Phase.Taking));
            Assert.That(scene.ChosenCard, Is.SameAs(scene.Cards[2].Card));
            Tick(scene, VictoryRewardScene.FlyT + 0.05f);
            Assert.That(scene.ShownDeckCount, Is.EqualTo(deck + 1), "the deck plate counts the card when it lands");
            Tick(scene, VictoryRewardScene.TakeT);
            Assert.That(scene.Steps[1].State, Is.EqualTo(RewardStepState.Done));
            Assert.That(scene.Steps[1].Result, Is.EqualTo(scene.Cards[2].Card.Name));
            Assert.That(scene.CurrentStep.Kind, Is.EqualTo(RewardStepKind.Relic), "then the relic");
            Assert.That(scene.Cleared, Is.False);
        }

        [Test] public void TheGoldButtonIsOffUntilACardIsPicked()
        {
            var g = Game(); var scene = Normal(g, Run()); scene.SkipIntro();
            Assert.That(scene.PrimaryEnabled, Is.False);
            Assert.That(scene.PrimaryLabel, Is.EqualTo("Elige una carta"));
            Click(scene, scene.PrimaryRect);
            Assert.That(scene.State, Is.EqualTo(VictoryRewardScene.Phase.Choosing), "refused: nothing picked");
            Assert.That(scene.Steps[1].State, Is.EqualTo(RewardStepState.Current));
            Draw(g, scene);   // the refusal message draws
            Click(scene, scene.CardRects[0]); Click(scene, scene.PrimaryRect);
            Assert.That(scene.ChosenCard, Is.SameAs(scene.Cards[0].Card));
        }

        [Test] public void TheKeysPickAndAddACard()
        {
            var g = Game(); var scene = Normal(g, Run()); scene.SkipIntro();
            Press(scene, Key.D3); Assert.That(scene.Selected, Is.EqualTo(2));
            Press(scene, Key.Right); Assert.That(scene.Selected, Is.EqualTo(0), "→ wraps round");
            Press(scene, Key.Left); Assert.That(scene.Selected, Is.EqualTo(2));
            Press(scene, Key.Tab); Assert.That(scene.Selected, Is.EqualTo(0));
            Press(scene, Key.D9); Assert.That(scene.Selected, Is.EqualTo(0), "no ninth card");
            Press(scene, Key.Return);
            Assert.That(scene.ChosenCard, Is.SameAs(scene.Cards[0].Card));
            Tick(scene, VictoryRewardScene.TakeT + 0.05f);
            Assert.That(scene.Cleared, Is.True, "a normal fight ends after its card");
        }

        [Test] public void EnterWithNothingPickedPicksTheFirstCardFirst()
        {
            var g = Game(); var scene = Normal(g, Run()); scene.SkipIntro();
            Press(scene, Key.Return);
            Assert.That(scene.Selected, Is.EqualTo(0));
            Assert.That(scene.State, Is.EqualTo(VictoryRewardScene.Phase.Choosing), "the first Enter only picks");
        }

        [Test] public void SkippingTheCardsAddsNone()
        {
            var g = Game();
            foreach (var how in new[] { "button", "key" })
            {
                var scene = Elite(g, Run()); scene.SkipIntro();
                Click(scene, scene.CardRects[0]);
                if (how == "button") Click(scene, scene.SkipRect.Value); else Press(scene, Key.O);
                Assert.That(scene.State, Is.EqualTo(VictoryRewardScene.Phase.Skipping), how);
                Tick(scene, VictoryRewardScene.SkipT + 0.05f);
                Assert.That(scene.ChosenCard, Is.Null, how);
                Assert.That(scene.Steps[1].State, Is.EqualTo(RewardStepState.Skipped), how);
                Assert.That(scene.Steps[1].Result, Is.EqualTo("Omitida"), how);
                Assert.That(scene.CurrentStep.Kind, Is.EqualTo(RewardStepKind.Relic), how);
            }
        }

        [Test] public void AClickWhileTheCardFliesGoesStraightToTheNextStep()
        {
            var g = Game(); var run = Run(); var scene = Elite(g, run); scene.SkipIntro();
            Press(scene, Key.D1); Press(scene, Key.Return);
            Click(scene, new RectF(640, 400, 2, 2));
            Assert.That(scene.CurrentStep.Kind, Is.EqualTo(RewardStepKind.Relic));
            Assert.That(scene.ShownDeckCount, Is.EqualTo(run.Deck.Count + 1), "the card landed on the way");
        }

        // ------------------------------------------------------------------ the elite's relic

        private static CombatRewardScene AtRelic(GameContext g, RunState run, RelicOffer relic = null)
        {
            var scene = Elite(g, run, 3, relic); scene.SkipIntro();
            Press(scene, Key.O); Tick(scene, VictoryRewardScene.SkipT + 0.05f); scene.SkipIntro();
            Assert.That(scene.CurrentStep.Kind, Is.EqualTo(RewardStepKind.Relic));
            Assert.That(scene.State, Is.EqualTo(VictoryRewardScene.Phase.Choosing));
            return scene;
        }

        [Test] public void TheElitesRelicIsReadyToTakeAndOptional()
        {
            var g = Game(); var run = Run();
            var scene = AtRelic(g, run);
            Assert.That(scene.Selected, Is.EqualTo(0), "a single relic is already the choice");
            Assert.That(scene.PrimaryLabel, Is.EqualTo("Tomar reliquia"));
            Assert.That(scene.RibbonText, Is.EqualTo("Paso 3 de 3 · Tómala si quieres"));
            Press(scene, Key.Return);
            Assert.That(scene.TookRelic, Is.True);
            Tick(scene, VictoryRewardScene.FlyT + 0.05f);
            Assert.That(scene.ShownRelicCount, Is.EqualTo(run.OwnedRelics.Count + 1));
            Tick(scene, VictoryRewardScene.TakeT);
            Assert.That(scene.Cleared, Is.True);
            Assert.That(scene.Steps[2].Result, Is.EqualTo(scene.Relic.Definition.Name));

            var left = AtRelic(g, Run());
            Click(left, left.SkipRect.Value);
            Tick(left, VictoryRewardScene.SkipT + 0.05f);
            Assert.That(left.TookRelic, Is.False);
            Assert.That(left.Cleared, Is.True);
            Assert.That(left.Steps[2].State, Is.EqualTo(RewardStepState.Skipped));
        }

        [Test] public void ClickingTheRelicCardTakesIt()
        {
            var g = Game(); var scene = AtRelic(g, Run());
            Click(scene, scene.Layout().Relics[0].Rect);
            Assert.That(scene.TookRelic, Is.True);
        }

        // ------------------------------------------------------------------ the boss

        [Test] public void TheBossPackOpensAndComesBackToTheTracker()
        {
            var g = Game(); var run = Run();
            var scene = new BossRewardScene(g, run, 99, BossRelics()); scene.SkipIntro();
            Assert.That(scene.PrimaryLabel, Is.EqualTo("Abrir sobre"));
            Assert.That(scene.SkipRect, Is.Null, "the pack is always opened (its own screen lets you skip the card)");
            Click(scene, scene.Layout().Pack);
            Assert.That(scene.OpenPackRequested, Is.True);
            Assert.That(scene.State, Is.EqualTo(VictoryRewardScene.Phase.WaitingPack));
            var kept = RunContent.Cards.First(c => c.Rarity == 4);
            scene.PackDone(new List<CardDefinition> { kept });
            Assert.That(scene.Steps[1].State, Is.EqualTo(RewardStepState.Done));
            Assert.That(scene.Steps[1].Result, Is.EqualTo(kept.Name));
            Tick(scene, VictoryRewardScene.ReturnT + 0.05f); scene.SkipIntro();
            Assert.That(scene.CurrentStep.Kind, Is.EqualTo(RewardStepKind.Relic));
            Assert.That(scene.Selected, Is.EqualTo(-1), "three relics: nothing picked yet");
            Assert.That(scene.PrimaryEnabled, Is.False);
            Press(scene, Key.D2); Press(scene, Key.Return);
            Assert.That(scene.ChosenRelic, Is.SameAs(scene.RelicOffers[1]));
            Tick(scene, VictoryRewardScene.TakeT + 0.05f);
            Assert.That(scene.Cleared, Is.True);
        }

        [Test] public void APackClosedWithoutACardIsMarkedSkippedAndTheBossRelicCanBeLeft()
        {
            var g = Game(); var scene = new BossRewardScene(g, Run(), 99, BossRelics()); scene.SkipIntro();
            Press(scene, Key.Return);
            scene.PackDone(new List<CardDefinition>());
            Assert.That(scene.Steps[1].State, Is.EqualTo(RewardStepState.Skipped));
            Tick(scene, VictoryRewardScene.ReturnT + 0.05f); scene.SkipIntro();
            Press(scene, Key.O); Tick(scene, VictoryRewardScene.SkipT + 0.05f);
            Assert.That(scene.ChosenRelic, Is.Null);
            Assert.That(scene.Cleared, Is.True);
            scene.PackDone(new List<CardDefinition>());     // a stray call changes nothing
            Assert.That(scene.Steps[1].State, Is.EqualTo(RewardStepState.Skipped));
        }

        // ------------------------------------------------------------------ empty offers

        [Test] public void NothingToChooseOnlyLetsYouContinue()
        {
            var g = Game();
            var scene = new CombatRewardScene(g, Run(), 10, new List<OfferedCard>()); scene.SkipIntro();
            Assert.That(scene.PrimaryEnabled, Is.True);
            Assert.That(scene.PrimaryLabel, Is.EqualTo("Continuar"));
            Assert.That(scene.SkipRect, Is.Null);
            Draw(g, scene, 3);
            Press(scene, Key.Return);
            Assert.That(scene.Cleared, Is.True);
            Assert.That(scene.ChosenCard, Is.Null);

            var boss = new BossRewardScene(g, Run(), 10, new List<RelicOffer>()); boss.SkipIntro();
            Press(boss, Key.Return); boss.PackDone(null); Tick(boss, 1f); boss.SkipIntro();
            Assert.That(boss.PrimaryLabel, Is.EqualTo("Continuar"));
            Draw(g, boss, 3);
            Click(boss, boss.PrimaryRect);
            Assert.That(boss.Cleared, Is.True);
            Assert.That(boss.ChosenRelic, Is.Null);
        }

        // ------------------------------------------------------------------ the scene manager

        private static SceneManager Manager(GameContext g, RunState run)
        {
            var m = new SceneManager(g, new MainMenuScene(g, false, false));
            m.StartRun(run); m.Update(0.05f);
            return m;
        }

        private static void Run(SceneManager m, float seconds) { for (var t = 0f; t < seconds; t += 0.05f) m.Update(0.05f); }

        [Test] public void TheSceneManagerAddsOnlyWhatWasTaken()
        {
            var g = Game();
            foreach (var takeRelic in new[] { true, false })
            {
                var run = Run(); var m = Manager(g, run);
                var deck = run.Deck.Count; var relics = run.OwnedRelics.Count;
                var scene = Elite(g, run); m.Push(scene); scene.SkipIntro();
                Press(scene, Key.D2); Press(scene, Key.Return); Run(m, 1.2f); scene.SkipIntro();
                Press(scene, takeRelic ? Key.Return : Key.O); Run(m, 1.2f);
                Assert.That(m.Top, Is.Not.SameAs(scene), "the reward screen closed");
                Assert.That(run.Deck.Count, Is.EqualTo(deck + 1));
                Assert.That(run.Deck.Last().Id, Is.EqualTo(scene.Cards[1].Card.Id));
                Assert.That(run.OwnedRelics.Count, Is.EqualTo(relics + (takeRelic ? 1 : 0)), takeRelic ? "taken" : "left");
            }
        }

        [Test] public void TheSceneManagerOpensTheBossPackAndAdvancesTheFloorAfterTheRelic()
        {
            var g = Game(); var run = Run(); var m = Manager(g, run);
            var floor = run.Floor; var deck = run.Deck.Count; var relics = run.OwnedRelics.Count;
            var scene = new BossRewardScene(g, run, 99, run.PickBossRelics()); m.Push(scene); scene.SkipIntro();
            Press(scene, Key.Return); m.Update(0.05f);
            var pack = m.Top as PackOpeningScene;
            Assert.That(pack, Is.Not.Null, "the pack screen is on top");
            pack.SkipAnimation(); Run(m, 0.2f); pack.Choose(0); Run(m, 3f);
            Assert.That(m.Top, Is.SameAs(scene), "back to the rewards");
            Assert.That(run.Deck.Count, Is.EqualTo(deck + 1));
            Assert.That(scene.Steps[1].State, Is.EqualTo(RewardStepState.Done));
            Assert.That(scene.Steps[1].Result, Is.EqualTo(run.Deck.Last().Name));
            scene.SkipIntro();
            Press(scene, Key.D1); Press(scene, Key.Return); Run(m, 1.2f);
            Assert.That(run.OwnedRelics.Count, Is.EqualTo(relics + 1));
            Assert.That(run.Floor, Is.EqualTo(floor + 1));
            Assert.That(m.Top, Is.InstanceOf<MapScene>());
        }

        // ------------------------------------------------------------------ layout

        [Test] public void TheCardsAndTheirTagsFitForAnyNumberOfCards()
        {
            var g = Game();
            for (var n = 1; n <= 7; n++)
            {
                var scene = Normal(g, Run(), n); scene.SkipIntro();
                var lay = scene.Layout();
                Assert.That(lay.Cards.Count, Is.EqualTo(n));
                for (var i = 0; i < n; i++)
                {
                    var lifted = lay.Cards[i].Move(0, -20);
                    Assert.That(OnScreen(lifted), Is.True, n + " cards: card " + i);
                    Assert.That(lifted.Y, Is.GreaterThan(VictoryRewardScene.SubY + 10), "a picked card stays under the heading");
                    Assert.That(lay.RarityTags[i].Y, Is.GreaterThan(lay.Cards[i].Bottom), "the rarity under its card");
                    Assert.That(lay.RarityTags[i].Bottom, Is.LessThan(lay.Primary.Y - 20), "clear of the buttons");
                    if (i > 0) Assert.That(Overlap(lay.Cards[i - 1], lay.Cards[i]), Is.False);
                }
                Assert.That(Overlap(lay.Primary, lay.Secondary), Is.False);
                Assert.That(OnScreen(lay.Primary) && OnScreen(lay.Secondary), Is.True);
            }
        }

        [Test] public void EveryRelicFitsAboveTheButtons()
        {
            var g = Game();
            foreach (var def in RunContent.Relics)
                foreach (var chroma in new[] { ChromaKind.None, ChromaKind.Golden, ChromaKind.Diamond })
                    for (var n = 1; n <= 3; n++)
                    {
                        var offers = Enumerable.Range(0, n).Select(_ => new RelicOffer(def, chroma)).ToList();
                        var boss = new BossRewardScene(g, Run(), 1, offers); boss.SkipIntro();
                        Press(boss, Key.Return); boss.PackDone(null); Tick(boss, 1f); boss.SkipIntro();
                        var lay = boss.Layout();
                        Assert.That(lay.Relics.Count, Is.EqualTo(n));
                        foreach (var v in lay.Relics)
                        {
                            var label = def.Name + " " + chroma + " ×" + n;
                            Assert.That(v.Rect.Bottom, Is.LessThanOrEqualTo(lay.Primary.Y - 10), label + ": above the buttons");
                            Assert.That(v.Rect.Y - 18, Is.GreaterThan(VictoryRewardScene.SubY + 8), label + ": the arrow stays under the heading");
                            Assert.That(OnScreen(v.Rect), Is.True, label);
                            Assert.That(v.Lines.All(l => l.Width <= v.Text.W), Is.True, label + ": lines inside the card");
                        }
                    }
        }

        [Test] public void TheTrackerSitsUnderTheTitleWithoutOverlaps()
        {
            var g = Game();
            foreach (Scene s in new Scene[] { Normal(g, Run()), Elite(g, Run()), new BossRewardScene(g, Run(), 9, BossRelics()) })
            {
                var lay = ((VictoryRewardScene)s).Layout();
                for (var i = 0; i < lay.Pills.Count; i++)
                {
                    Assert.That(OnScreen(lay.Pills[i]), Is.True);
                    Assert.That(lay.Pills[i].Y, Is.GreaterThan(96), "under the title band");
                    Assert.That(lay.Pills[i].Bottom, Is.LessThan(VictoryRewardScene.HeadingY - 12), "over the step heading");
                    Assert.That(Overlap(lay.Pills[i], VictoryRewardScene.DeckCounter), Is.False);
                    if (i > 0) Assert.That(lay.Pills[i].X - lay.Pills[i - 1].Right, Is.GreaterThanOrEqualTo(VictoryRewardScene.LinkW - 1));
                }
                Assert.That(Overlap(VictoryRewardScene.DeckCounter, VictoryRewardScene.RelicCounter), Is.False);
                Assert.That(Overlap(VictoryRewardScene.DeckCounter, PauseMenu.PauseButton), Is.False);
            }
        }

        // ------------------------------------------------------------------ drawing

        [Test] public void EveryPhaseDrawsWithAndWithoutData()
        {
            foreach (var withData in new[] { true, false })
            {
                var g = Game(withData);
                var elite = Elite(g, Run(HeroKind.Rogue), 5, Relic("r_clover", ChromaKind.Golden));
                Draw(g, elite, 30);                                        // intro and arrival
                Move(elite, elite.CardRects[2]); Draw(g, elite, 2);       // hover with its tooltip
                Click(elite, elite.CardRects[2]); Draw(g, elite, 2);
                Move(elite, elite.Layout().Pills[0]); Draw(g, elite, 1);
                Press(elite, Key.Return); Draw(g, elite, 8);               // the card flies
                Move(elite, elite.Layout().Pills[1]); Draw(g, elite, 1);  // the done step shows its card
                Draw(g, elite, 20);
                Move(elite, elite.Layout().Relics[0].Rect); Draw(g, elite, 2);
                Press(elite, Key.Return); Draw(g, elite, 20);
                Assert.That(elite.Cleared, Is.True, withData ? "with data" : "without data");

                var boss = new BossRewardScene(g, Run(HeroKind.Mage), 120, BossRelics());
                Draw(g, boss, 30); Move(boss, boss.Layout().Pack); Draw(g, boss, 2);
                Press(boss, Key.Return); Draw(g, boss, 2);
                boss.PackDone(new List<CardDefinition> { RunContent.Cards[3] }); Draw(g, boss, 30);
                Move(boss, boss.Layout().Relics[1].Rect); Draw(g, boss, 2);
                Press(boss, Key.D2); Press(boss, Key.O); Draw(g, boss, 20);
                Assert.That(boss.Cleared, Is.True);
            }
        }

        [Test] public void AThousandRandomInputsNeverBreakTheFlow()
        {
            var g = Game(); var rng = new Random(7);
            var keys = new[] { Key.Return, Key.O, Key.D1, Key.D2, Key.D3, Key.Left, Key.Right, Key.Tab, Key.Space };
            for (var round = 0; round < 6; round++)
            {
                VictoryRewardScene scene = round % 3 == 0 ? Elite(g, Run(), 1 + round) : round % 3 == 1 ? (VictoryRewardScene)Normal(g, Run(), round) : new BossRewardScene(g, Run(), 5, BossRelics());
                for (var k = 0; k < 1000 && !scene.Cleared; k++)
                {
                    var x = (float)rng.NextDouble() * Canvas.Width; var y = (float)rng.NextDouble() * Canvas.Height;
                    switch (rng.Next(4))
                    {
                        case 0: scene.HandleEvent(new InputEvent(EventType.MouseMove, x, y)); break;
                        case 1: scene.HandleEvent(new InputEvent(EventType.MouseDown, x, y, 1)); break;
                        case 2: scene.HandleEvent(new InputEvent(EventType.KeyDown, key: keys[rng.Next(keys.Length)])); break;
                        default: scene.Update((float)rng.NextDouble() * 0.3f); break;
                    }
                    if (scene is BossRewardScene b && b.OpenPackRequested) { b.OpenPackRequested = false; b.PackDone(rng.Next(2) == 0 ? null : new List<CardDefinition> { RunContent.Cards[0] }); }
                    if (k % 50 == 0) Draw(g, scene);
                }
                Assert.That(scene.Steps.Count(s => s.State == RewardStepState.Current), Is.LessThanOrEqualTo(1));
                Assert.That(scene.Steps[0].State, Is.EqualTo(RewardStepState.Done));
                if (scene.Cleared) Assert.That(scene.Steps.All(s => s.State == RewardStepState.Done || s.State == RewardStepState.Skipped), Is.True);
            }
        }
    }
}
