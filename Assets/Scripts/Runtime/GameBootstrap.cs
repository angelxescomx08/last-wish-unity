using System;
using System.Collections;
using LastWish.Application;
using LastWish.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

namespace LastWish.Presentation
{
    public sealed partial class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }
        public bool IsReady { get; private set; }
        public bool IsBusy { get; private set; }
        public CombatState Battle => session?.State;
        public string ScreenName { get; private set; }
        private UiFactory ui;
        private GameInput gameInput;
        private TooltipPresenter tooltip;
        private GameAssets assets;
        private RectTransform viewport;
        private RectTransform screen;
        private RectTransform loadingFill;
        private Text loadingLabel, loadingPercent, loadingTitle, fps, heroStats, heroDebuffs, turnStats, feedback;
        private Transform handRoot;
        private readonly System.Collections.Generic.List<CardView> cardViews = new System.Collections.Generic.List<CardView>();
        private CanvasGroup handGroup;
        private Button endTurn;
        private AtlasAnimation heroView;
        private AtlasDefinition heroAtlas;
        private BattleSession session;
        private HeroKind selectedHero = HeroKind.Warrior;
        private AudioSource music, effects;
        private bool showFps;
        private float frameTime;
        private int frames;
        private const int Seed = 42;

        private void Awake()
        {
            Instance = this;
            UnityEngine.Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            showFps = PlayerPrefs.GetInt("last_wish_show_fps", 1) == 1;
            ui = new UiFactory(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            assets = new GameAssets();
            gameInput = new GameInput(PlayCard, EndTurn, () => { if (!IsBusy) ShowMenu(); }, CycleEnemy);
            var cameraObject = new GameObject("GameCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.cullingMask = 0; camera.orthographic = true; camera.allowHDR = false; camera.allowMSAA = false;
            var canvas = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            viewport = ui.Rect(canvas.transform, "Viewport", 0, 0, 1280, 720);
            viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f);
            viewport.anchoredPosition = Vector2.zero;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            var overlay = ui.Rect(viewport, "PerformanceOverlay", 640, 360, 1280, 720);
            var overlayCanvas = overlay.gameObject.AddComponent<Canvas>(); overlayCanvas.overrideSorting = true; overlayCanvas.sortingOrder = 10;
            fps = ui.Label(overlay, "Fps", "", 1125, 18, 290, 30, 16, UiFactory.Gold);
            ShowLoading();
        }

        private IEnumerator Start()
        {
            yield return null; // Present loading feedback before requesting any large texture.
            var loader = assets.Load(UpdateLoading);
            while (true)
            {
                bool more = false; object next = null; Exception failure = null;
                try { more = loader.MoveNext(); if (more) next = loader.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null)
                {
                    loadingLabel.text = "No se pudo preparar la aventura.";
                    Debug.LogException(failure); yield break;
                }
                if (!more) break;
                yield return next;
            }
            ui.Font = assets.Font;
            Font.textureRebuilt += KeepFontPixels;
            music = gameObject.AddComponent<AudioSource>(); music.clip = assets.Get<AudioClip>("Audio/Music");
            music.loop = true; music.volume = .25f; music.Play();
            effects = gameObject.AddComponent<AudioSource>(); effects.volume = .65f;
            tooltip = new TooltipPresenter(ui, viewport);
            Run = RunPersistence.Load();
            ShowMenu();
            IsReady = true;
        }

        private void OnDestroy()
        {
            gameInput?.Dispose();
            Font.textureRebuilt -= KeepFontPixels;
            if (Instance == this) Instance = null;
        }
        private void KeepFontPixels(Font font)
        {
            if (font == ui.Font && font.material.mainTexture != null) font.material.mainTexture.filterMode = FilterMode.Point;
        }
        private void Update()
        {
            if (!IsReady) return;
            frameTime += Time.unscaledDeltaTime; frames++;
            if (frameTime >= .25f)
            {
                fps.text = showFps ? string.Format("{0:0} FPS  |  {1:0.0} ms", frames / frameTime, 1000 * frameTime / frames) : "";
                frameTime = 0; frames = 0;
            }

        }

        private void ClearScreen(string name)
        {
            tooltip?.Hide();
            if (screen != null) { screen.gameObject.SetActive(false); Destroy(screen.gameObject); }
            screen = ui.Rect(viewport, name, 640, 360, 1280, 720);
            ScreenName = name; gameInput.SetBattleActive(name == "Battle"); fps.transform.SetAsLastSibling();
        }
        private void ShowLoading()
        {
            ClearScreen("Loading");
            ui.Panel(screen, "Backdrop", 640, 360, 1280, 720, new Color32(12, 14, 24, 255));
            ui.Panel(screen, "PanelBorder", 640, 360, 684, 304, UiFactory.Gold);
            ui.Panel(screen, "Panel", 640, 360, 680, 300, new Color32(22, 24, 38, 255));
            loadingTitle = ui.Label(screen, "Title", "ÚLTIMO DESEO", 640, 280, 620, 90, 56, UiFactory.Gold);
            loadingLabel = ui.Label(screen, "Stage", "Preparando la aventura...", 640, 370, 620, 50, 24);
            ui.Panel(screen, "ProgressTrack", 640, 420, 464, 24, Color.black);
            loadingFill = ui.Panel(screen, "Progress", 410, 420, 0, 18, UiFactory.Gold).rectTransform;
            loadingFill.pivot = new Vector2(0, .5f);
            loadingPercent = ui.Label(screen, "ProgressText", "0 %", 640, 465, 200, 40, 24);
        }
        private void UpdateLoading(string label, float progress)
        {
            loadingLabel.text = label; loadingPercent.text = Mathf.RoundToInt(progress * 100) + " %";
            loadingFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 460 * progress);
            if (progress > 0)
            {
                loadingTitle.font = assets.Font; loadingLabel.font = assets.Font; loadingPercent.font = assets.Font;
            }
        }
        private void Backdrop()
        {
            ui.Art(screen, "Dungeon", assets.Get<Texture2D>("Art/DungeonRoom"), 640, 360, 1280, 720);
            ui.Panel(screen, "Shade", 640, 360, 1280, 720, new Color(0, 0, .04f, .27f));
        }
        private AtlasAnimation Actor(string id, float x, float y, float width)
        {
            var data = assets.Atlas(id);
            var texture = assets.Get<Texture2D>("Art/" + id + "Atlas");
            var image = ui.Art(screen, id + "Actor", texture, x, y, width, width * data.cellHeight / data.cellWidth);
            var animator = image.gameObject.AddComponent<AtlasAnimation>();
            animator.Initialize(image, texture, data); return animator;
        }
        private void Confirm() { effects.PlayOneShot(assets.Get<AudioClip>("Audio/Confirm")); }
        public void ShowMenu()
        {
            if (IsBusy) return;
            session = null; ClearScreen("Menu"); Backdrop();
            Actor("warrior", 225, 420, 288); Actor("mage", 1055, 420, 288);
            ui.Label(screen, "Title", "ÚLTIMO DESEO", 640, 125, 1000, 100, 64, UiFactory.Gold);
            ui.Label(screen, "Subtitle", "Cada carta decide tu destino.", 640, 205, 900, 50, 24);
            ui.Panel(screen, "MenuPanel", 640, 420, 410, 265, new Color(0.05f, .05f, .08f, .94f));
            ui.Button(screen, "PlayButton", "Jugar", 640, 340, 320, 48, ShowCharacterSelection);
            ui.Button(screen, "SettingsButton", "Ajustes", 640, 412, 320, 48, ShowSettings);
            ui.Button(screen, "QuitButton", "Salir", 640, 484, 320, 48, Quit);
            if (Run != null && !Run.IsDead)
                ui.Button(screen, "ContinueRunButton", "Continuar partida", 640, 570, 420, 44, () => { if (Run.RoomCompleted) ShowMap(); else ShowCurrentRoom(); });
            ui.Label(screen, "Hint", "Una aventura nueva. El mismo último deseo.", 640, 650, 1100, 40, 24);
        }
        public void ShowCharacterSelection()
        {
            Confirm(); ClearScreen("Selection"); Backdrop();
            ui.Label(screen, "Heading", "ELIGE TU DESTINO", 640, 90, 1100, 70, 48, UiFactory.Gold);
            var kinds = new[] { HeroKind.Warrior, HeroKind.Mage, HeroKind.Rogue };
            for (var i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i]; var hero = HeroDefinition.OriginalFor(kind); var x = 270 + i * 370;
                ui.Panel(screen, "HeroPanel", x, 365, 325, 420, new Color(.04f, .05f, .09f, .90f));
                ui.Label(screen, "HeroName", hero.Name, x, 205, 315, 50, 32, UiFactory.Gold);
                Actor(hero.AssetId, x, 350, 240);
                ui.Label(screen, "HeroStats", "Vida " + hero.MaxHp + "  |  Maná " + hero.MaxMana, x, 480, 310, 45, 24);
                ui.Button(screen, kind + "Button", "Elegir", x, 540, 255, 46, () => StartRun(kind));
            }
            ui.Button(screen, "BackButton", "Volver", 640, 650, 280, 44, ShowMenu);
        }
        private void ShowSettings()
        {
            Confirm(); ClearScreen("Settings"); Backdrop();
            ui.Label(screen, "Heading", "AJUSTES", 640, 170, 900, 80, 48, UiFactory.Gold);
            ui.Button(screen, "ToggleFpsButton", "Mostrar FPS: " + (showFps ? "Sí" : "No"), 640, 340, 500, 56, () =>
            { showFps = !showFps; PlayerPrefs.SetInt("last_wish_show_fps", showFps ? 1 : 0); PlayerPrefs.Save(); ShowSettings(); });
            ui.Button(screen, "BackButton", "Volver", 640, 500, 320, 48, ShowMenu);
        }
        public void StartBattle(HeroKind kind)
        {
            Run = null; Confirm(); selectedHero = kind; session = new BattleSession(kind, Seed);
            BuildBattle();
        }
        private void BuildBattle()
        {
            heroAtlas = assets.Atlas(session.State.Hero.AssetId);
            ClearScreen("Battle"); Backdrop();
            heroView = Actor(session.State.Hero.AssetId, 270, 345, 192);
            BuildEnemies();
            ui.Panel(screen, "TopBar", 640, 42, 1280, 84, new Color(.025f, .03f, .05f, .96f));
            turnStats = ui.Label(screen, "Turn", "", 475, 42, 700, 50, 24, UiFactory.Gold);
            ui.Button(screen, "MenuButton", "Menú", 80, 42, 120, 42, ShowMenu);
            heroStats = ui.Label(screen, "HeroStats", "", 280, 180, 500, 100, 22);
            heroDebuffs = ui.Label(screen, "HeroDebuffs", "", 270, 250, 490, 50, 14, UiFactory.Gold);
            feedback = ui.Label(screen, "Feedback", "Juega tus cartas y sobrevive.", 640, 430, 1150, 50, 24, UiFactory.Gold);
            ui.Panel(screen, "HandBackdrop", 640, 620, 1280, 200, new Color(.025f, .03f, .06f, .96f));
            cardViews.Clear();
            handRoot = ui.Rect(screen, "Hand", 550, 605, 1100, 220);
            handGroup = handRoot.gameObject.AddComponent<CanvasGroup>();
            endTurn = ui.Button(screen, "EndTurnButton", "Fin de turno", 1150, 595, 200, 54, EndTurn);
            ui.Label(screen, "Controls", "1-0: jugar carta   |   Espacio: fin de turno   |   Tab: objetivo   |   Esc: menú", 640, 701, 1200, 30, 16);
            RefreshBattle();
        }
        private void RefreshBattle()
        {
            var state = session.State;
            turnStats.text = "Turno " + state.Turn + "   |   Maná " + state.Mana + "/" + state.Hero.MaxMana + "   |   Robo " + state.DrawCount + "   Descarte " + state.DiscardCount;
            heroStats.text = state.Hero.Name + "\nVida " + state.Hp + "/" + state.Hero.MaxHp + "   Escudo " + state.Block + "\nFuerza " + state.Strength + " · Poderes " + state.ActivePowerCount;
            heroDebuffs.text = DescribeStatuses(state.HeroStatuses);
            RefreshEnemies();
            var spacing = Math.Min(190, 1000f / Math.Max(1, state.Hand.Count));
            for (var i = 0; i < state.Hand.Count; i++)
            {
                var index = i;
                if (i == cardViews.Count) cardViews.Add(new CardView(ui, handRoot, assets.Get<Texture2D>("Art/CardFrame"), index, () => PlayCard(index), tooltip));
                cardViews[i].Bind(state.Hand[i], state, i, spacing, session.CanPlay(i));
            }
            for (var i = state.Hand.Count; i < cardViews.Count; i++) cardViews[i].SetVisible(false);
            handGroup.interactable = !IsBusy; handGroup.blocksRaycasts = !IsBusy; endTurn.interactable = !IsBusy;
        }
        public void PlayCard(int index)
        {
            if (IsBusy || session == null || !session.CanPlay(index)) return;
            StartCoroutine(PlayCardAnimation(index));
        }
        private IEnumerator PlayCardAnimation(int index)
        {
            IsBusy = true; RefreshBattle();
            var card = session.State.Hand[index];
            var attacked = session.State.SelectedEnemy;
            heroView.Play(card.IsAttack ? "attack" : "guard");
            effects.PlayOneShot(assets.Get<AudioClip>("Audio/Card"));
            var contact = card.IsAttack ? heroAtlas.attackContactSeconds : .15f;
            yield return new WaitForSeconds(contact);
            session.PlayCard(index);
            if (card.IsAttack) { ShowEnemyHit(attacked); effects.PlayOneShot(assets.Get<AudioClip>("Audio/Hit")); }
            feedback.text = "Has jugado " + card.Name + ".";
            RefreshBattle();
            yield return new WaitForSeconds(.45f);
            IsBusy = false;
            if (session.State.Victory) ShowResult(true); else RefreshBattle();
        }
        public void EndTurn()
        {
            if (IsBusy || session == null || session.State.Finished) return;
            StartCoroutine(EndTurnAnimation());
        }
        private IEnumerator EndTurnAnimation()
        {
            IsBusy = true; RefreshBattle();
            var before = session.State.Hp;
            PlayEnemyIntents();
            yield return new WaitForSeconds(.3f);
            session.EndTurn();
            if (before != session.State.Hp) heroView.Play(session.State.Defeat ? "death" : "hurt", !session.State.Defeat);
            ShowEnemyDeaths();
            feedback.text = "Turno enemigo resuelto. Has recibido " + (before - session.State.Hp) + " de daño.";
            RefreshBattle();
            yield return new WaitForSeconds(.6f);
            IsBusy = false;
            if (session.State.Victory) ShowResult(true); else if (session.State.Defeat) ShowResult(false); else RefreshBattle();
        }
        private void ShowResult(bool victory)
        {
            if (Run != null && victory) { ShowRunReward(); return; }
            if (Run != null) { Run.ApplyCombatResult(0, 0); RunPersistence.Delete(); }
            ClearScreen("Result"); Backdrop();
            ui.Panel(screen, "ResultPanel", 640, 355, 760, 390, new Color(.035f, .04f, .07f, .95f));
            ui.Label(screen, "ResultHeading", victory ? "VICTORIA" : "ÚLTIMO SUSPIRO", 640, 250, 1000, 90, 56, UiFactory.Gold);
            ui.Label(screen, "ResultMessage", victory ? "La mazmorra guarda más secretos." : "Cada derrota deja una nueva oportunidad.", 640, 350, 1100, 60, 24);
            ui.Button(screen, "RetryButton", Run == null ? "Otra batalla" : "Nueva partida", 640, 440, 380, 50, () => { if (Run == null) StartBattle(selectedHero); else StartRun(selectedHero); });
            ui.Button(screen, "ResultMenuButton", "Menú principal", 640, 515, 380, 50, ShowMenu);
        }
        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }
    }
}
