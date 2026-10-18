using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastWish.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LastWish.Tests
{
    public sealed class RunFlowTests
    {
        [UnityTest]
        public IEnumerator NewRunMapCombatRewardAndCheckpointWork()
        {
            var save = Path.Combine(UnityEngine.Application.persistentDataPath, "run-v1.json");
            var previous = File.Exists(save) ? File.ReadAllBytes(save) : null;
            var previousBackup = File.Exists(save + ".bak") ? File.ReadAllBytes(save + ".bak") : null;
            try
            {
                yield return SceneManager.LoadSceneAsync("Bootstrap");
                var deadline = Time.realtimeSinceStartup + 30;
                while ((GameBootstrap.Instance == null || !GameBootstrap.Instance.IsReady) && Time.realtimeSinceStartup < deadline) yield return null;
                var game = GameBootstrap.Instance;
                Assert.That(game.IsReady, Is.True);
                Click("PlayButton"); Click("MageButton");
                Assert.That(game.ScreenName, Is.EqualTo("Map"));
                game.Run.ForcedEncounter = 1;
                var checkpoint = RunPersistence.Load();
                Assert.That(checkpoint, Is.Not.Null); Assert.That(checkpoint.Seed, Is.EqualTo(game.Run.Seed));
                var frameTimes = new List<float>();
                for (var frame = 0; frame < 120; frame++) { yield return null; frameTimes.Add(Time.unscaledDeltaTime * 1000); }
                frameTimes.Sort();
                var results = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../TestResults"));
                Directory.CreateDirectory(results);
                var metrics = new FrameMetrics { frameCount = frameTimes.Count, medianMs = frameTimes[60], p95Ms = frameTimes[113],
                    averageMs = frameTimes.Average(), scene = "Map", environment = "Unity Editor PlayMode, automated graphics test; not a standalone benchmark" };
                File.WriteAllText(Path.Combine(results, "map-performance.json"), JsonUtility.ToJson(metrics, true));
                BootstrapTests.CaptureUi(Path.Combine(results, "map.png"));
                var room = game.Run.Map.Nodes.Single(n => n.Available);
                Click("Room_" + room.Id);
                Assert.That(game.ScreenName, Is.EqualTo("Battle"));
                var oldDeckCount = game.Run.Deck.Count;
                for (var action = 0; action < 100 && game.ScreenName == "Battle"; action++)
                {
                    deadline = Time.realtimeSinceStartup + 5;
                    while (game.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(game.IsBusy, Is.False);
                    if (game.ScreenName != "Battle") break;
                    var index = Enumerable.Range(0, game.Battle.Hand.Count).Where(i => game.Battle.CanPlay(i) && game.Battle.Hand[i].IsAttack).DefaultIfEmpty(-1).First();
                    if (index < 0) game.EndTurn(); else game.PlayCard(index);
                    yield return null;
                }
                deadline = Time.realtimeSinceStartup + 5;
                while (game.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(game.ScreenName, Is.EqualTo("Reward"));
                Click("RewardCard0");
                Assert.That(game.ScreenName, Is.EqualTo("Map"));
                Assert.That(game.Run.Deck.Count, Is.EqualTo(oldDeckCount + 1));
                Assert.That(game.Run.Map.Find(room.Id).Visited, Is.True);
                Assert.That(game.Run.Map.Nodes.Any(n => n.Available && !n.Visited), Is.True);
                Assert.That(RunPersistence.Load().Deck.Count, Is.EqualTo(game.Run.Deck.Count));
                Click("DeckButton"); Assert.That(game.ScreenName, Is.EqualTo("Inventory"));
                BootstrapTests.CaptureUi(Path.Combine(results, "inventory.png"));
                Click("LeaveRoomButton"); Assert.That(game.ScreenName, Is.EqualTo("Map"));
            }
            finally
            {
                if (previous != null) File.WriteAllBytes(save, previous); else if (File.Exists(save)) File.Delete(save);
                if (previousBackup != null) File.WriteAllBytes(save + ".bak", previousBackup); else if (File.Exists(save + ".bak")) File.Delete(save + ".bak");
            }
        }
        [UnityTest]
        public IEnumerator EveryNonCombatRoomCanBeUsedAndExited()
        {
            var save = Path.Combine(UnityEngine.Application.persistentDataPath, "run-v1.json");
            var previous = File.Exists(save) ? File.ReadAllBytes(save) : null;
            var backup = File.Exists(save + ".bak") ? File.ReadAllBytes(save + ".bak") : null;
            try
            {
                yield return SceneManager.LoadSceneAsync("Bootstrap");
                var deadline = Time.realtimeSinceStartup + 30;
                while ((GameBootstrap.Instance == null || !GameBootstrap.Instance.IsReady) && Time.realtimeSinceStartup < deadline) yield return null;
                var game = GameBootstrap.Instance; Assert.That(game.IsReady, Is.True);
                var kinds = new[] { LastWish.Domain.RoomKind.Shop, LastWish.Domain.RoomKind.Treasure, LastWish.Domain.RoomKind.Event,
                    LastWish.Domain.RoomKind.Warlock, LastWish.Domain.RoomKind.Purge, LastWish.Domain.RoomKind.Gacha };
                foreach (var kind in kinds)
                {
                    LastWish.Domain.RunState run = null; LastWish.Domain.MapRoom room = null;
                    for (var seed = 0; seed < 100 && room == null; seed++)
                    {
                        run = new LastWish.Domain.RunState(LastWish.Domain.HeroKind.Warrior, seed);
                        room = run.Map.Nodes.FirstOrDefault(n => n.Kind == kind);
                    }
                    Assert.That(room, Is.Not.Null);
                    var parent = run.Map.Nodes.First(n => n.Connections.Contains(room.Id));
                    run.RestoreProgress(parent.Id, new string[0]);
                    typeof(GameBootstrap).GetProperty("Run").SetValue(game, run);
                    game.EnterRoom(room.Id); yield return null;
                    var results = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../TestResults"));
                    BootstrapTests.CaptureUi(Path.Combine(results, kind.ToString().ToLowerInvariant() + ".png"));
                    var gold = run.Gold; var cards = run.Deck.Count;
                    switch (kind)
                    {
                        case LastWish.Domain.RoomKind.Shop:
                            Assert.That(Enumerable.Range(0, 3).Select(i => GameObject.Find("ShopRelic" + i).GetComponentInChildren<Text>().text).Distinct().Count(), Is.EqualTo(3));
                            Click("ShopPack0"); Assert.That(game.ScreenName, Is.EqualTo("Pack"));
                            Click("PackCard0"); Assert.That(run.Deck.Count, Is.EqualTo(cards + 1)); Assert.That(run.Gold, Is.LessThan(gold));
                            Click("LeaveRoomButton"); break;
                        case LastWish.Domain.RoomKind.Treasure:
                            Click("TakeRelicButton"); Assert.That(run.Relics.Count, Is.EqualTo(1)); break;
                        case LastWish.Domain.RoomKind.Event:
                            Click("EventGold"); Assert.That(run.Gold, Is.EqualTo(gold + 50)); break;
                        case LastWish.Domain.RoomKind.Warlock:
                            Click("DeckCard0"); Assert.That(run.Deck[0].Upgraded, Is.True); Assert.That(run.Gold, Is.EqualTo(gold - 50));
                            Click("LeaveRoomButton"); break;
                        case LastWish.Domain.RoomKind.Purge:
                            Click("DeckCard0"); Assert.That(run.Deck.Count, Is.EqualTo(cards - 1)); Assert.That(run.Gold, Is.EqualTo(gold - 75)); break;
                        case LastWish.Domain.RoomKind.Gacha:
                            Click("Pull0"); Assert.That(game.ScreenName, Is.EqualTo("Capsule"));
                            Click("AcceptRelic"); Assert.That(run.Relics.Count, Is.EqualTo(1)); Assert.That(run.Gold, Is.EqualTo(gold - 80));
                            Click("LeaveRoomButton"); break;
                    }
                    Assert.That(game.ScreenName, Is.EqualTo("Map"), kind.ToString());
                    Assert.That(run.RoomCompleted, Is.True);
                }
            }
            finally
            {
                if (previous != null) File.WriteAllBytes(save, previous); else if (File.Exists(save)) File.Delete(save);
                if (backup != null) File.WriteAllBytes(save + ".bak", backup); else if (File.Exists(save + ".bak")) File.Delete(save + ".bak");
            }
        }
        [Test] public void JsonCheckpointKeepsEveryEffectLayer()
        {
            var save = Path.Combine(UnityEngine.Application.persistentDataPath, "run-v1.json");
            var previous = File.Exists(save) ? File.ReadAllBytes(save) : null;
            var backup = File.Exists(save + ".bak") ? File.ReadAllBytes(save + ".bak") : null;
            try
            {
                var run = new LastWish.Domain.RunState(LastWish.Domain.HeroKind.Mage, 42);
                var card = LastWish.Domain.NumericCards.All.First(c => c.Effects.Any(e => e.Element.HasValue));
                foreach (var entry in LastWish.Domain.NumericCards.All) run.AddCard(entry);
                RunPersistence.Save(run);
                var restored = RunPersistence.Load(); Assert.That(restored, Is.Not.Null);
                Assert.That(restored.Deck[run.Deck.Count - LastWish.Domain.NumericCards.All.Count + LastWish.Domain.NumericCards.All.ToList().IndexOf(card)].Effects.Count, Is.EqualTo(card.Effects.Count));
                var originalCards = run.CreateSave().cards; var restoredCards = restored.CreateSave().cards;
                Assert.That(restoredCards.Length, Is.EqualTo(originalCards.Length));
                for (var i = 0; i < originalCards.Length; i++)
                    Assert.That(JsonUtility.ToJson(restoredCards[i]), Is.EqualTo(JsonUtility.ToJson(originalCards[i])), originalCards[i].id);
            }
            finally
            {
                if (previous != null) File.WriteAllBytes(save, previous); else if (File.Exists(save)) File.Delete(save);
                if (backup != null) File.WriteAllBytes(save + ".bak", backup); else if (File.Exists(save + ".bak")) File.Delete(save + ".bak");
            }
        }
        private static void Click(string name) => GameObject.Find(name).GetComponent<Button>().onClick.Invoke();
        [Serializable] private sealed class FrameMetrics
        {
            public string scene, environment;
            public int frameCount;
            public float medianMs, p95Ms, averageMs;
        }
    }
}
