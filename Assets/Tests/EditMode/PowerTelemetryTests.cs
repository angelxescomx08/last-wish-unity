using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>Power telemetry (docs/telemetry.md): what the tracker records, the JSON the server accepts, and the scene manager wiring.</summary>
    public sealed class PowerTelemetryTests
    {
        [SetUp] public void ResetTuning() => Tuning.Reset();
        [TearDown] public void RestoreTuning() => Tuning.Reset();

        private const string RunId = "6f1d1c1e-8a2b-4c3d-9e4f-0a1b2c3d4e5f";
        private static RunState NewRun(long seed = 42) => new RunState(HeroKind.Warrior, seed, EnemyRoster.Expanded);

        /// <summary>Plays the first playable card until the fight ends, reporting each play like the combat screen.</summary>
        private static (int plays, long damage, long mana) Fight(PowerTracker tracker, CombatState state)
        {
            int plays = 0; long damage = 0, mana = 0;
            for (var turn = 0; turn < 200 && !state.Finished; turn++)
            {
                for (var guard = 0; guard < 50 && !state.Finished; guard++)
                {
                    var index = -1;
                    for (var i = 0; i < state.Hand.Count && index < 0; i++) if (state.CanPlay(i)) index = i;
                    if (index < 0) break;
                    var card = state.Hand[index]; var cost = state.CardCost(card); var block = state.Block;
                    var result = state.Play(index);
                    Assert.That(result.Success, Is.True);
                    tracker.CardPlayed(card, cost, result, state.Block - block);
                    plays++; mana += cost; damage += result.CastHits.Sum(h => h.Sum(x => (long)x));
                }
                if (!state.Finished) state.EndTurn();
            }
            Assert.That(state.Finished, Is.True, "the fight must end");
            return (plays, damage, mana);
        }

        // ------------------------------------------------------------------ domain

        [Test] public void AFightRecordsEveryPlayItsDamageAndHowItEnded()
        {
            var run = NewRun(); var tracker = new PowerTracker(run, RunId, "0.1.0");
            var enemies = run.GenerateEnemies("telemetry-room");
            var state = run.CreateCombat(enemies);
            tracker.BeginCombat(run, state, RoomKind.Combat, enemies);
            var combat = tracker.Combat;
            Assert.That(combat, Is.Not.Null);
            Assert.That(combat.Encounter, Is.EqualTo(string.Join("+", enemies.Select(e => e.Definition.Ai))));
            Assert.That(combat.Deck, Is.EqualTo(run.Deck.Select(c => c.Id)));
            Assert.That(combat.HpStart, Is.EqualTo(state.Hp));

            var (plays, damage, mana) = Fight(tracker, state);
            tracker.EndCombat(state, state.Victory ? CombatOutcome.Won : CombatOutcome.Lost);

            Assert.That(tracker.Combat, Is.Null);
            Assert.That(tracker.Record.Combats.Single(), Is.SameAs(combat));
            Assert.That(combat.Outcome, Is.EqualTo(state.Victory ? CombatOutcome.Won : CombatOutcome.Lost));
            Assert.That((combat.CardsPlayed, combat.Damage, combat.Mana), Is.EqualTo((plays, damage, mana)));
            Assert.That(combat.Cards.Sum(c => c.Plays), Is.EqualTo(plays), "every play belongs to one card");
            Assert.That(combat.Cards.Sum(c => c.Damage), Is.EqualTo(damage));
            Assert.That(combat.Cards.Select(c => c.Id), Is.Unique);
            Assert.That(damage, Is.GreaterThan(0));
            Assert.That((combat.Turns, combat.HpEnd, combat.HpLost), Is.EqualTo((state.Turn, Math.Max(0, state.Hp), state.HpLostCombat)));
        }

        [Test] public void ChoicesKeepWhatWasOfferedAndWhatWasTaken()
        {
            var run = NewRun(); var tracker = new PowerTracker(run, RunId);
            var offer = run.PickRewardCards("room-a").Select(c => c.Card).ToList();
            var reward = tracker.OfferCards(run, "reward", offer);
            tracker.Pick(reward, offer[1].Id);
            var skipped = tracker.OfferCards(run, "reward", run.PickRewardCards("room-b").Select(c => c.Card));
            var relics = run.PickTreasureRelics("room-c");
            var treasure = tracker.OfferRelics(run, "treasure", relics.Select(r => r.Definition));
            tracker.Pick(treasure, relics[0].Definition.Id);

            Assert.That(tracker.Record.Choices, Is.EqualTo(new[] { reward, skipped, treasure }));
            Assert.That(reward.Offered, Is.EqualTo(offer.Select(c => c.Id)));
            Assert.That(reward.Picked, Is.EqualTo(new[] { offer[1].Id }));
            Assert.That(skipped.Picked, Is.Empty, "a skipped reward takes nothing");
            Assert.That((treasure.Relic, treasure.Source, treasure.Floor), Is.EqualTo((true, "treasure", 1)));
            Assert.That(tracker.OfferCards(run, "reward", Array.Empty<CardDefinition>()), Is.Null, "an empty offer is not a choice");

            var reroll = run.PickPackCards(PackTheme.Epico, reroll: 1).Select(c => c.Card).ToList();
            tracker.ReplaceOffer(reward, reroll);
            Assert.That(reward.Offered, Is.EqualTo(reroll.Select(c => c.Id)));
        }

        [Test] public void RelicsRememberWhereTheyCameFrom()
        {
            var run = NewRun(); var tracker = new PowerTracker(run, RunId);
            var first = run.PickTreasureRelics("room-a")[0];
            run.AcquireRelic(first); tracker.RelicGained(run, first.Definition, "treasure");
            var other = RunContent.Relics.First(r => r.Id != first.Definition.Id && r.ClassId == "neutral");
            run.AcquireRelic(new RelicOffer(other, ChromaKind.Golden));      // given outside a tracked room
            var record = tracker.Checkpoint(run, 12.5);

            Assert.That(record.Relics.Select(r => (r.Id, r.Source, r.Floor)), Is.EqualTo(new[] { (first.Definition.Id, "treasure", 1), (other.Id, "unknown", 0) }));
            Assert.That(record.Relics[1].Chroma, Is.EqualTo(ChromaKind.Golden));
            Assert.That(record.PlayedSeconds, Is.EqualTo(12.5));
            Assert.That(tracker.Dirty, Is.False, "a checkpoint is a clean point");
        }

        [Test] public void FinishingClosesTheOpenFightAndFreezesTheRun()
        {
            var run = NewRun(); var tracker = new PowerTracker(run, RunId);
            var enemies = run.GenerateEnemies("room-a"); var state = run.CreateCombat(enemies);
            tracker.BeginCombat(run, state, RoomKind.Elite, enemies);
            var record = tracker.Finish(run, RunEnd.Death, 30, state);

            Assert.That((record.Outcome, record.Hp), Is.EqualTo(((RunEnd?)RunEnd.Death, 0)));
            Assert.That(record.Combats.Single().Outcome, Is.EqualTo(CombatOutcome.Lost), "dying ends the fight as lost");
            Assert.That(tracker.Finished, Is.True);
            tracker.RoomEntered(); tracker.BeginCombat(run, state, RoomKind.Combat, enemies);
            tracker.OfferCards(run, "reward", run.Deck);
            Assert.That((record.Rooms, record.Combats.Count, record.Choices.Count), Is.EqualTo((0, 1, 0)), "a finished run records nothing more");
            Assert.That(tracker.Finish(run, RunEnd.Quit, 99), Is.SameAs(record));
            Assert.That(record.Outcome, Is.EqualTo((RunEnd?)RunEnd.Death));

            var abandoned = new PowerTracker(run, RunId);
            abandoned.BeginCombat(run, state, RoomKind.Combat, enemies);
            Assert.That(abandoned.Finish(run, RunEnd.Abandon, 1).Combats.Single().Outcome, Is.EqualTo(CombatOutcome.Abandoned));
        }

        [Test] public void PruebasRunsAreMarkedSoTheAnalyticsLeaveThemOut()
        {
            Assert.That(PowerTracker.IsDefault(Tuning.Current), Is.True);
            Assert.That(new PowerTracker(NewRun(), RunId).Record.Tuned, Is.False);
            Tuning.Current.ChromaChances[Tuning.ChromaKey(ChromaKind.Golden, "card")] = Tuning.DefaultChromaChance(ChromaKind.Golden, "card");
            Assert.That(PowerTracker.IsDefault(Tuning.Current), Is.True, "writing the default value changes nothing");
            Tuning.Current.ExtraDamage = 5;
            Assert.That(new PowerTracker(NewRun(), RunId).Record.Tuned, Is.True);
            Tuning.Reset(); Tuning.Current.ChromaChances[Tuning.ChromaKey(ChromaKind.Golden, "relic")] = 1.0;
            Assert.That(PowerTracker.IsDefault(Tuning.Current), Is.False);
        }

        [Test] public void BossFightsAreKeyedByTheBossAndTheContentHashIsStable()
        {
            var run = NewRun();
            Assert.That(PowerTracker.EncounterKey(run, RoomKind.Boss, run.GenerateBoss()), Is.EqualTo(run.FloorBossKey()));
            Assert.That(PowerTracker.ContentHash, Does.Match("^[0-9a-f]{16}$"));
            Assert.That(PowerTracker.ContentHash, Is.EqualTo(PowerTracker.ContentHash));
            Assert.That(new PowerTracker(run, RunId).Record.ContentHash, Is.EqualTo(PowerTracker.ContentHash));
        }

        // ------------------------------------------------------------------ JSON

        [Test] public void TheJsonHasTheShapePublicIngestRunAccepts()
        {
            var culture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");     // decimal comma: the JSON must not care
                var run = NewRun(); var tracker = new PowerTracker(run, RunId, "0.1.0", "0d6c6a44-1111-4222-8333-944455556666", "2026-10-23T18:00:00Z");
                var enemies = run.GenerateEnemies("room-a"); var state = run.CreateCombat(enemies);
                tracker.RoomEntered(); tracker.BeginCombat(run, state, RoomKind.Combat, enemies); Fight(tracker, state);
                tracker.EndCombat(state, state.Victory ? CombatOutcome.Won : CombatOutcome.Lost);
                var offer = run.PickRewardCards("room-a").Select(c => c.Card).ToList();
                tracker.Pick(tracker.OfferCards(run, "reward", offer), offer[0].Id);
                tracker.Record.StartedAt = "quote \" backslash \\ newline \n end";
                var json = PowerJson.Write(tracker.Checkpoint(run, 75.26));

                var doc = Json.Obj(Json.Parse(json));
                Assert.That(doc.Get("schema_version"), Is.EqualTo(1L));
                Assert.That(doc.Get("run_id"), Is.EqualTo(RunId));
                Assert.That(doc.Get("started_at"), Is.EqualTo("quote \" backslash \\ newline \n end"), "strings are escaped");
                Assert.That(doc.Get("hero"), Is.EqualTo("warrior"));
                Assert.That(doc.Get("roster"), Is.EqualTo("expanded"));
                Assert.That(doc.Get("outcome"), Is.EqualTo("unfinished"));
                Assert.That(doc.Get("tuned"), Is.EqualTo(false));
                Assert.That(doc.Get("floor"), Is.EqualTo(1L));
                Assert.That(doc.Get("rooms"), Is.EqualTo(1L));
                Assert.That(doc.Get("seed"), Is.EqualTo(42L));
                Assert.That(doc.Get("played_seconds"), Is.EqualTo(75.3).Within(1e-9));
                Assert.That(doc.Get("content_hash"), Is.EqualTo(PowerTracker.ContentHash));
                Assert.That(Json.Arr(doc.Get("deck")).Count, Is.EqualTo(run.Deck.Count));
                var choice = Json.Obj(Json.Arr(doc.Get("choices")).Single());
                Assert.That(choice.Get("kind"), Is.EqualTo("card"));
                Assert.That(choice.Get("source"), Is.EqualTo("reward"));
                Assert.That(Json.Arr(choice.Get("picked")).Single(), Is.EqualTo(offer[0].Id));
                var combat = Json.Obj(Json.Arr(doc.Get("combats")).Single());
                Assert.That(combat.Get("room"), Is.EqualTo("combat"));
                Assert.That(combat.Get("outcome"), Is.EqualTo(state.Victory ? "won" : "lost"));
                var cards = Json.Arr(combat.Get("cards")).Select(Json.Obj).ToList();
                Assert.That(cards.Sum(c => (long)c.Get("plays")), Is.EqualTo((long)combat.Get("cards_played")));
                Assert.That(cards.Sum(c => (long)c.Get("damage")), Is.EqualTo((long)combat.Get("damage")));
                foreach (var key in new[] { "enemies", "deck", "relics" }) Assert.That(Json.Arr(combat.Get(key)), Is.Not.Null, key);
            }
            finally { Thread.CurrentThread.CurrentCulture = culture; }
        }

        // ------------------------------------------------------------------ scene manager

        private sealed class MemorySink : ITelemetrySink
        {
            public readonly List<string> Checkpoints = new List<string>(), Submitted = new List<string>();
            public string GameVersion => "test";
            public string PlayerId => null;
            public void Checkpoint(string runId, string json) => Checkpoints.Add(json);
            public void Submit(string runId, string json) => Submitted.Add(json);
        }

        private static (GameContext game, SceneManager manager, MemorySink sink) Game(bool telemetry = true)
        {
            var render = new NullRender(); var art = new Art(render, new FileAssets()); art.LoadCore();
            var sink = new MemorySink();
            var game = new GameContext(new Canvas(render), art, new SoundPlayer(null), new Preferences(), null) { Data = new GameData(new FileAssets()), Telemetry = telemetry ? sink : null };
            return (game, new SceneManager(game, new MainMenuScene(game, false, false)), sink);
        }

        private static void Press(SceneManager manager, Key key)
        {
            manager.HandleEvent(new InputEvent(EventType.KeyDown, key: key));
            manager.HandleEvent(new InputEvent(EventType.KeyUp, key: key));
        }

        [Test] public void TheSceneManagerTracksAFightAndSendsTheAbandonedRun()
        {
            var (game, manager, sink) = Game();
            var run = Enumerable.Range(0, 200).Select(s => NewRun(s)).First(r => r.Map.Nodes.Any(n => n.Available && n.Kind == RoomKind.Combat));
            manager.StartRun(run);
            Assert.That(game.Power, Is.Not.Null, "a run with a sink is tracked");
            ((MapScene)manager.Top).SelectedNode = run.Map.Nodes.First(n => n.Available && n.Kind == RoomKind.Combat);
            manager.Update(0.016f);
            Assert.That(manager.Top, Is.InstanceOf<CombatScene>());
            Assert.That(game.Power.Combat, Is.Not.Null, "entering the fight opens its record");

            var played = game.Power.Combat;
            var cardIndex = Enumerable.Range(0, ((CombatScene)manager.Top).State.Hand.Count).First(i => ((CombatScene)manager.Top).State.CanPlay(i));
            Press(manager, Key.D1 + cardIndex);
            Press(manager, Key.Return);
            Assert.That(played.CardsPlayed, Is.EqualTo(1), "the combat screen reports the play");

            Press(manager, Key.Escape);            // pause
            Press(manager, Key.Down);              // "Abandonar partida…"
            Press(manager, Key.Return);            // confirm screen
            Press(manager, Key.Down);
            Press(manager, Key.Return);            // "Abandonar y volver al menú"
            Assert.That(manager.Top, Is.InstanceOf<MainMenuScene>());
            Assert.That(game.Power, Is.Null);

            var doc = Json.Obj(Json.Parse(sink.Submitted.Single()));
            Assert.That(doc.Get("outcome"), Is.EqualTo("abandon"));
            Assert.That(doc.Get("rooms"), Is.EqualTo(1L));
            var combat = Json.Obj(Json.Arr(doc.Get("combats")).Single());
            Assert.That(combat.Get("outcome"), Is.EqualTo("abandoned"));
            Assert.That(combat.Get("cards_played"), Is.EqualTo(1L));
        }

        [Test] public void BackOnTheMapTheRunIsCheckpointedAndClosingTheGameSendsIt()
        {
            var (game, manager, sink) = Game();
            manager.StartRun(NewRun());
            manager.Update(0.016f);
            Assert.That(sink.Checkpoints, Is.Empty, "nothing new, nothing written");
            game.Power.RoomEntered();
            manager.Update(0.016f); manager.Update(0.016f);
            Assert.That(Json.Obj(Json.Parse(sink.Checkpoints.Single())).Get("outcome"), Is.EqualTo("unfinished"));
            manager.EndSession();
            Assert.That(Json.Obj(Json.Parse(sink.Submitted.Single())).Get("outcome"), Is.EqualTo("quit"));
            manager.EndSession();
            Assert.That(sink.Submitted, Has.Count.EqualTo(1), "a run is sent once");
        }

        [Test] public void StartingAnotherRunSendsTheOneLeftOpen()
        {
            var (game, manager, sink) = Game();
            manager.StartRun(NewRun(1));
            var first = game.Power;
            manager.StartRun(NewRun(2));
            Assert.That(game.Power, Is.Not.SameAs(first));
            Assert.That(first.Finished, Is.True);
            var doc = Json.Obj(Json.Parse(sink.Submitted.Single()));
            Assert.That(doc.Get("outcome"), Is.EqualTo("abandon"));
            Assert.That(doc.Get("seed"), Is.EqualTo(1L), "the old run keeps its own data");
        }

        [Test] public void WithoutASinkNothingIsTrackedAndATrackerErrorNeverBreaksTheGame()
        {
            var (game, manager, _) = Game(false);
            manager.StartRun(NewRun());
            Assert.That(game.Power, Is.Null);

            var (tracked, other, _) = Game();
            other.StartRun(NewRun());
            Assert.DoesNotThrow(() => tracked.Track(p => throw new InvalidOperationException("bug")));
            Assert.That(tracked.Power, Is.Null, "telemetry switches itself off for the run");
            Assert.DoesNotThrow(() => other.Update(0.016f));
        }
    }
}
