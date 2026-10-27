using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LastWish.VfxLab.Tests
{
    /// <summary>
    /// Plays the VFX Lab scene with a fixed 30 fps clock and saves captures of the final composite (pixel layer +
    /// bloom) to TestResults/vfxlab for visual review: card idle loop, hover, the cyan bolt frame by frame,
    /// the same moments with bloom off, and a stress test. Fails on any error or exception logged meanwhile.
    /// </summary>
    public sealed class VfxLabCaptureTests
    {
        private const string ScenePath = "Assets/VfxLab/Scenes/VfxLab.unity";
        private string results;
        private VfxLabDirector director;
        private PixelGlowCamera pixel;

        [UnityTest, Timeout(300000)]
        public IEnumerator CaptureVfxLab()
        {
            results = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults/vfxlab"));
            Directory.CreateDirectory(results);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only");
#endif
            Time.captureFramerate = 30;
            try
            {
                yield return null;
                yield return null;
                director = Object.FindAnyObjectByType<VfxLabDirector>();
                Assert.That(director, Is.Not.Null, "VfxLabDirector missing");
                pixel = director.pixelCamera;
                Assert.That(pixel, Is.Not.Null);
                Assert.That(pixel.PixelTarget, Is.Not.Null, "Pixel target not created");
                director.mouseHover = false;
                var log = new System.Text.StringBuilder();
                log.AppendLine("pixelTarget " + pixel.PixelTarget.width + "x" + pixel.PixelTarget.height + " " + pixel.PixelTarget.format);
                log.AppendLine("graphics " + SystemInfo.graphicsDeviceType + " hdr " + SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf));

                // 1) card idle loop (2 s at 15 fps)
                for (var i = 0; i < 30; i++) { yield return Frames(2); Capture($"cards_idle_{i:00}", 960, 540); }
                Capture("overview", 1920, 1080);

                // 2) hover
                director.cards[1].Hovered = true;
                yield return Frames(12);
                Capture("hover", 1920, 1080);
                director.cards[1].Hovered = false;
                yield return Frames(20);

                // 3) the cyan bolt, every frame for 1.8 s, with full-size stills at key moments
                var stills = new[] { 6, 11, 14, 17, 20, 23, 28, 36, 48 };
                director.PlayCard(0);
                var maxParticles = 0;
                for (var f = 0; f < 54; f++)
                {
                    yield return null;
                    Capture($"bolt_{f:00}", 960, 540);
                    if (System.Array.IndexOf(stills, f) >= 0) Capture($"bolt_still_{f:00}", 1920, 1080);
                    maxParticles = Mathf.Max(maxParticles, LiveParticles());
                }
                log.AppendLine("bolt max live particles " + maxParticles);
                yield return Frames(40);

                // 4) same moments with bloom off (pixel layer only)
                pixel.bloom = false;
                director.PlayCard(2);
                for (var f = 0; f < 30; f++)
                {
                    yield return null;
                    if (System.Array.IndexOf(stills, f) >= 0) Capture($"nobloom_{f:00}", 1920, 1080);
                }
                pixel.bloom = true;
                yield return Frames(40);

                // 5) legibility under load
                director.StartCoroutine(director.Stress());
                maxParticles = 0;
                for (var f = 0; f < 40; f++)
                {
                    yield return null;
                    maxParticles = Mathf.Max(maxParticles, LiveParticles());
                    if (f == 12 || f == 20 || f == 30) Capture($"stress_{f:00}", 1920, 1080);
                }
                log.AppendLine("stress max live particles " + maxParticles);
                File.WriteAllText(Path.Combine(results, "stats.txt"), log.ToString());
            }
            finally
            {
                Time.captureFramerate = 0;
            }
        }

        private static IEnumerator Frames(int n) { for (var i = 0; i < n; i++) yield return null; }

        private static int LiveParticles()
        {
            var n = 0;
            foreach (var s in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) n += s.particleCount;
            return n;
        }

        private void Capture(string name, int width, int height)
        {
            pixel.PixelCamera.Render();
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                pixel.CompositeTo(rt);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply(false);
                File.WriteAllBytes(Path.Combine(results, name + ".png"), tex.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(tex);
            }
        }
    }
}
