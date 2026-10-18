using System.Collections;
using System.IO;
using LastWish.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LastWish.Tests
{
    public sealed class BootstrapTests
    {
        [UnityTest]
        public IEnumerator LoadingMenuSelectionAndCombatWork()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            var deadline = Time.realtimeSinceStartup + 30;
            while ((GameBootstrap.Instance == null || !GameBootstrap.Instance.IsReady) && Time.realtimeSinceStartup < deadline)
                yield return null;
            var game = GameBootstrap.Instance;
            Assert.That(game, Is.Not.Null);
            Assert.That(game.IsReady, Is.True, "Startup must finish within 30 seconds");
            Assert.That(game.ScreenName, Is.EqualTo("Menu"));
            var results = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../TestResults"));
            Directory.CreateDirectory(results);
            CaptureUi(Path.Combine(results, "menu.png"));
            yield return null;
            GameObject.Find("PlayButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(game.ScreenName, Is.EqualTo("Selection"));
            game.StartBattle(LastWish.Domain.HeroKind.Warrior);
            Assert.That(game.ScreenName, Is.EqualTo("Battle"));
            Assert.That(game.Battle.Hand.Count, Is.EqualTo(5));
            var atlas = Resources.Load<Texture2D>("Game/Art/warriorAtlas");
            Assert.That(atlas.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(atlas.width, Is.EqualTo(6912), "Native atlas must not be resized by import");
            var previousMana = game.Battle.Mana;
            GameObject.Find("CardButton0").GetComponent<Button>().onClick.Invoke();
            Assert.That(game.IsBusy, Is.True);
            deadline = Time.realtimeSinceStartup + 5;
            while (game.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.IsBusy, Is.False);
            Assert.That(game.Battle.Mana, Is.LessThan(previousMana));
            Assert.That(game.Battle.Hand.Count, Is.EqualTo(4));
            var turn = game.Battle.Turn;
            GameObject.Find("EndTurnButton").GetComponent<Button>().onClick.Invoke();
            deadline = Time.realtimeSinceStartup + 5;
            while (game.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.Battle.Turn, Is.EqualTo(turn + 1));
            Assert.That(game.Battle.Hand.Count, Is.EqualTo(5));
            Assert.That(game.Battle.Mana, Is.EqualTo(game.Battle.Hero.MaxMana));
            CaptureUi(Path.Combine(results, "combat.png"));
            yield return null;
            GameObject.Find("MenuButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(game.ScreenName, Is.EqualTo("Menu"));
        }

        public static void CaptureUi(string path)
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var camera = Object.FindFirstObjectByType<Camera>();
            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            var previousMask = camera.cullingMask;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.cullingMask = ~0;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
                camera.cullingMask = previousMask;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                Object.Destroy(pixels);
                Object.Destroy(target);
            }
        }
    }
}
