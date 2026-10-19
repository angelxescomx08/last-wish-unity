using System;
using System.Collections;
using System.IO;
using LastWish.Presentation;
using LastWish.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

using EventType = LastWish.Presentation.EventType;
using Key = LastWish.Presentation.Key;

namespace LastWish.Tests
{
    /// <summary>Loads the Bootstrap scene and drives the ported game with pygame-like events.</summary>
    public static class GameDriver
    {
        public static string Results
        {
            get
            {
                var path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../TestResults"));
                Directory.CreateDirectory(path);
                return path;
            }
        }

        public static IEnumerator Boot()
        {
            yield return UnitySceneManager.LoadSceneAsync("Bootstrap");
            var deadline = Time.realtimeSinceStartup + 30;
            while ((GameBootstrap.Instance == null || !GameBootstrap.Instance.IsReady) && !(GameBootstrap.Instance?.Failed ?? false) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(GameBootstrap.Instance, Is.Not.Null);
            Assert.That(GameBootstrap.Instance.Failed, Is.False, "Startup failed: " + GameBootstrap.Instance.LoadingLabel);
            Assert.That(GameBootstrap.Instance.IsReady, Is.True, "Startup must finish within 30 seconds");
        }

        public static GameBootstrap Game => GameBootstrap.Instance;
        public static Presentation.SceneManager Manager => Game.Manager;

        public static void Send(InputEvent e) => Game.Injected.Enqueue(e);
        public static void Key(Key key) { Send(new InputEvent(EventType.KeyDown, key: key)); Send(new InputEvent(EventType.KeyUp, key: key)); }
        public static void Click(float x, float y)
        {
            Send(new InputEvent(EventType.MouseMove, x, y));
            Send(new InputEvent(EventType.MouseDown, x, y, 1));
            Send(new InputEvent(EventType.MouseUp, x, y, 1));
        }

        public static IEnumerator Frames(int count) { for (var i = 0; i < count; i++) yield return null; }
        public static IEnumerator Seconds(float seconds) { var end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }

        public static IEnumerator Until(Func<bool> condition, float timeout, string what)
        {
            var end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(condition(), Is.True, what);
        }

        /// <summary>Saves the 1280×720 virtual canvas (what the player sees inside the letterbox).</summary>
        public static void Capture(string name)
        {
            var rt = Game.CanvasTarget;
            var previous = RenderTexture.active;
            var pixels = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(Results, name + ".png"), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(pixels); }
        }
    }
}
