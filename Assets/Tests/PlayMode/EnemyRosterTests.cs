using System.Collections;
using System.IO;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LastWish.Tests
{
    public sealed class EnemyRosterTests
    {
        [UnityTest]
        public IEnumerator AllNineteenEnemiesLoadTheirOwnOriginalAtlas()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            var deadline = Time.realtimeSinceStartup + 30;
            while ((GameBootstrap.Instance == null || !GameBootstrap.Instance.IsReady) && Time.realtimeSinceStartup < deadline) yield return null;
            var game = GameBootstrap.Instance; Assert.That(game.IsReady, Is.True);
            var results = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../TestResults"));
            foreach (var definition in EnemyCatalog.Definitions)
            {
                var run = new RunState(HeroKind.Mage, 42);
                var kind = definition.Boss ? RoomKind.Boss : definition.Elite ? RoomKind.Elite : RoomKind.Combat;
                var pool = EnemyCatalog.Definitions.Where(d => d.Boss == definition.Boss && d.Elite == definition.Elite).ToList();
                var forced = pool.IndexOf(definition) + 1;
                run.ForcedBoss = run.ForcedElite = run.ForcedEncounter = forced;
                var room = kind == RoomKind.Combat ? run.Map.Nodes.Single(n => n.Available) : run.Map.Nodes.First(n => n.Kind == kind);
                if (kind != RoomKind.Combat)
                {
                    var parent = run.Map.Nodes.First(n => n.Connections.Contains(room.Id));
                    run.RestoreProgress(parent.Id, new string[0]);
                }
                typeof(GameBootstrap).GetProperty("Run").SetValue(game, run);
                game.EnterRoom(room.Id);
                deadline = Time.realtimeSinceStartup + 30;
                while (game.ScreenName == "Loading" && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(game.ScreenName, Is.EqualTo("Battle"), definition.Ai);
                Assert.That(game.Battle.Enemies[0].Definition.Ai, Is.EqualTo(definition.Ai));
                var actor = GameObject.Find(definition.AssetId + "Actor").GetComponent<RawImage>();
                Assert.That(actor.texture.name, Is.EqualTo(definition.AssetId + "Atlas"));
                Assert.That(actor.texture.filterMode, Is.EqualTo(FilterMode.Point));
                if (definition.Boss || definition.Ai == "golem" || definition.Ai == "espectro")
                    BootstrapTests.CaptureUi(Path.Combine(results, "enemy-" + definition.Ai + ".png"));
            }
        }
        [UnityTest]
        public IEnumerator PairTargetsAndPooledHandControlsWork()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            var deadline = Time.realtimeSinceStartup + 30;
            while ((GameBootstrap.Instance == null || !GameBootstrap.Instance.IsReady) && Time.realtimeSinceStartup < deadline) yield return null;
            var game = GameBootstrap.Instance;
            var run = new RunState(HeroKind.Mage, 42) { ForcedEncounter = 12 };
            typeof(GameBootstrap).GetProperty("Run").SetValue(game, run);
            game.EnterRoom(run.Map.Nodes.Single(n => n.Available).Id);
            deadline = Time.realtimeSinceStartup + 30;
            while (game.ScreenName == "Loading" && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.ScreenName, Is.EqualTo("Battle")); Assert.That(game.Battle.Enemies.Count, Is.EqualTo(2));
            GameObject.Find("EnemyTarget1").GetComponent<Button>().onClick.Invoke();
            Assert.That(game.Battle.SelectedEnemy, Is.EqualTo(1));
            var first = GameObject.Find("CardButton0").GetInstanceID();
            game.PlayCard(0);
            deadline = Time.realtimeSinceStartup + 5;
            while (game.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.IsBusy, Is.False);
            Assert.That(GameObject.Find("CardButton0").GetInstanceID(), Is.EqualTo(first), "Hand controls must be reused after playing a card");
            BootstrapTests.CaptureUi(Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../TestResults/pair.png")));
        }
    }
}
