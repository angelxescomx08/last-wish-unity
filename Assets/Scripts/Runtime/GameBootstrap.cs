using System;
using System.Collections;
using LastWish.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using Canvas = LastWish.Presentation.Canvas;

namespace LastWish.Runtime
{
    /// <summary>
    /// main.py's loop on Unity: loading screen, then every frame input → SceneManager.update → draw the
    /// 1280×720 virtual canvas into an offscreen target (one mesh, a few draw calls) and present it
    /// aspect-fit and smoothly scaled with black bars, like infrastructure/viewport.py.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }
        public bool IsReady { get; private set; }
        public bool Failed { get; private set; }
        public SceneManager Manager { get; private set; }
        public GameContext Context { get; private set; }
        public string LoadingLabel { get; private set; } = "";
        public float LoadingProgress { get; private set; }
        public RenderTexture CanvasTarget => target;
        public int LastBatchCount => backend?.LastBatchCount ?? 0;
        /// <summary>Tests feed events here instead of (or as well as) the devices.</summary>
        public readonly System.Collections.Generic.Queue<InputEvent> Injected = new System.Collections.Generic.Queue<InputEvent>();

        private UnityRenderBackend backend;
        private Canvas canvas;
        private Art art;
        private RenderTexture target;
        private Camera view;
        private InputBridge input;
        private UnityAudio unityAudio;
        private LoadingScreen loading;
        private float scale = 1f; private int offX, offY, destW, destH, lastW, lastH;
        private float fpsTime; private int fpsFrames; private float fps;

        private void Awake()
        {
            Instance = this;
            UnityEngine.Application.targetFrameRate = 60;      // clock.tick(60)
            QualitySettings.vSyncCount = 0;
            var cameraObject = new GameObject("GameCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            view = cameraObject.GetComponent<Camera>();
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = Color.black;
            view.cullingMask = 0; view.orthographic = true; view.allowHDR = false; view.allowMSAA = false;
            target = new RenderTexture(Canvas.Width, Canvas.Height, 0, RenderTextureFormat.ARGB32)
            { name = "LastWish virtual canvas", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, useMipMap = false, hideFlags = HideFlags.HideAndDontSave };
            target.Create();
            backend = new UnityRenderBackend { MainTarget = target };
            canvas = new Canvas(backend);
            input = new InputBridge();
        }

        private IEnumerator Start()
        {
            // Fonts and the small atlases first: the loading screen is drawn with them.
            backend.Calibrate(canvas);
            var source = new ResourceAssets();
            art = new Art(backend, source);
            try { art.LoadCore(); }
            catch (Exception error) { Failed = true; Debug.LogException(error); yield break; }
            loading = new LoadingScreen(art.Fonts);
            var storage = new FileStorage();
            Report("Abriendo las puertas...", 0f);
            yield return null;
            var prefs = Preferences.Load(storage);
            DevSettings.Load(storage);
            Report("Preparando las cartas...", .05f);
            yield return null;
            Exception failure = null;
            try
            {
                for (var rarity = 1; rarity <= 5; rarity++) art.CardFrame(rarity);
                var back = art.CardBack;
            }
            catch (Exception error) { failure = error; }
            if (failure != null) { Fail(failure); yield break; }
            var heroes = new[] { "warrior", "mage", "rogue" };
            for (var i = 0; i < heroes.Length; i++)
            {
                Report("Despertando a los héroes...", .25f + .5f * i / heroes.Length);
                yield return null;
                try { var sheets = art.Hero(heroes[i]); sheets.Frame("idle", 0f, 192, out _); sheets.Frame("idle", 0f, 96, out _); }
                catch (Exception error) { failure = error; }
                if (failure != null) { Fail(failure); yield break; }
            }
            Report("Afinando los sonidos...", .80f);
            yield return null;
            unityAudio = new UnityAudio(gameObject);
            unityAudio.LoadEffects();
            var sound = new SoundPlayer(unityAudio, prefs.SfxVolume, prefs.MusicVolume);
            Report("Preparando la mazmorra...", .90f);
            yield return null;
            try
            {
                Context = new GameContext(canvas, art, sound, prefs, storage) { Data = new GameData(source), Quit = Quit };
                Manager = new SceneManager(Context, new MainMenuScene(Context, false, false));
                canvas.BeginFrame(); Manager.Draw(canvas); canvas.EndFrame(); Graphics.ExecuteCommandBuffer(backend.Commands);
            }
            catch (Exception error) { failure = error; }
            if (failure != null) { Fail(failure); yield break; }
            Report("Encendiendo las antorchas...", .97f);
            yield return null;
            sound.StartMusic();
            Report("Todo listo", 1f);
            yield return null;
            IsReady = true;
        }

        private void Fail(Exception error)
        {
            Failed = true;
            Debug.LogException(error);
            Report("No se pudo preparar la aventura.", LoadingProgress);
        }

        /// <summary>LoadingScreen.present: draw the panel now (the next yield shows it).</summary>
        private void Report(string label, float progress)
        {
            LoadingLabel = label; LoadingProgress = progress;
            canvas.BeginFrame(); loading.Draw(canvas, label, progress); canvas.EndFrame();
            Graphics.ExecuteCommandBuffer(backend.Commands);
        }

        private void Update()
        {
            UpdateViewport();
            if (!IsReady || Manager == null) return;
            var dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            unityAudio?.Tick(dt);
            fpsTime += Time.unscaledDeltaTime; fpsFrames++;
            if (fpsTime >= 0.5f) { fps = fpsFrames / fpsTime; fpsTime = 0f; fpsFrames = 0; }
            Context.Fps = fps;
            Context.Time += dt;
            foreach (var e in input.Poll(ToVirtual)) Manager.HandleEvent(e);
            while (Injected.Count > 0) Manager.HandleEvent(Injected.Dequeue());
            Manager.Update(dt);
            if (Manager.QuitRequested) { Quit(); return; }
            canvas.BeginFrame();
            Manager.Draw(canvas);
            if (Context.Prefs.ShowFps)
            {
                var font = art.Fonts.Get(16); var text = "FPS: " + MathX.Round(fps);
                font.Draw(canvas, text, Canvas.Width - font.Width(text) - 8, 8, new Rgba(255, 220, 50));
            }
            canvas.EndFrame();
            Graphics.ExecuteCommandBuffer(backend.Commands);
        }

        /// <summary>viewport._update: aspect-fit scale and centred offsets (whole pixels).</summary>
        private void UpdateViewport()
        {
            int w = Math.Max(Screen.width, 1), h = Math.Max(Screen.height, 1);
            if (w != lastW || h != lastH)
            {
                lastW = w; lastH = h;
                scale = Math.Min(w / (float)Canvas.Width, h / (float)Canvas.Height);
                destW = (int)(Canvas.Width * scale); destH = (int)(Canvas.Height * scale);
                offX = (w - destW) / 2; offY = (h - destH) / 2;
            }
            target.filterMode = destW == Canvas.Width && destH == Canvas.Height ? FilterMode.Point : FilterMode.Bilinear;
        }

        /// <summary>viewport.present: the canvas aspect-fit into the window (black bars from the camera clear).
        /// GUI.DrawTexture handles the render-texture orientation of every graphics API (D3D, Metal, GL, Vulkan).</summary>
        private void OnGUI()
        {
            if (UnityEngine.Event.current.type != UnityEngine.EventType.Repaint || destW <= 0) return;
            GUI.DrawTexture(new Rect(offX, offY, destW, destH), target, ScaleMode.StretchToFill, false);
        }

        /// <summary>viewport.to_virtual: screen pixels (Unity: bottom-left origin) → whole canvas pixels.</summary>
        private (float x, float y) ToVirtual(float px, float py)
        {
            var top = Screen.height - py;
            if (scale <= 0) return ((int)px, (int)top);
            return ((int)((px - offX) / scale), (int)((top - offY) / scale));
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            Context?.Sound.StopMusic();
            input?.Dispose();
            backend?.Dispose();
            if (target != null) { target.Release(); Destroy(target); }
            if (Instance == this) Instance = null;
        }
    }
}
