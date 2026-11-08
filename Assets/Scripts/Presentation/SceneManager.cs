using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>
    /// main.SceneManager: stack-based scene manager that drives every transition and owns the run,
    /// the pause menu, the hero sheet and the shared gold counter.
    /// </summary>
    public sealed class SceneManager
    {
        private readonly List<Scene> stack = new List<Scene>();
        private readonly GameContext game;
        public RunState Run { get; private set; }
        public bool QuitRequested { get; private set; }
        public Scene Top => stack[stack.Count - 1];
        public int Depth => stack.Count;
        public IReadOnlyList<Scene> Stack => stack;
        public PauseMenu Pause { get; private set; }
        public HeroSheetOverlay HeroSheet { get; private set; }
        public GoldHud GoldHud { get; }
        private (float x, float y) mouse = (-1, -1);
        private bool mouseDown;
        private float uiTime;
        private RunState goldRun;
        private int interestSeen;
        // power telemetry: the open choices of the screens on top and when the run started (game clock)
        private ChoiceRecord cardChoice, relicChoice;
        private float runStarted;

        public SceneManager(GameContext game, Scene initial)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            stack.Add(initial ?? throw new ArgumentNullException(nameof(initial)));
            GoldHud = new GoldHud(game.Art);
        }

        public void Push(Scene scene) => stack.Add(scene);
        public void Pop() { if (stack.Count > 1) stack.RemoveAt(stack.Count - 1); }
        private void PopAllExceptFirst() { while (stack.Count > 1) stack.RemoveAt(stack.Count - 1); }
        private SoundPlayer Sound => game.Sound;

        /// <summary>Start a run directly (tests and tools); the map is pushed on top of the current scene.</summary>
        public void StartRun(RunState run) { Run = run; BeginTracking(run); Push(new MapScene(game, run)); }

        private bool CanPause()
        {
            if (Run == null) return false;
            var top = Top;
            return !(top is MainMenuScene || top is CharacterSelectScene || top is SettingsScene || top is DevSettingsScene || top is DeathScene);
        }

        /// <summary>The hero sheet of the current run (live combat values when in a fight).</summary>
        public void OpenHeroSheet()
        {
            if (Run == null) return;
            CombatState state = null;
            if (Top is CombatScene combat) { state = combat.State; combat.CancelSelection(); }
            HeroSheet = new HeroSheetOverlay(game, HeroSheetData.For(game, Run, state));
            Sound.PlayConfirm();
        }

        // ------------------------------------------------------------------ loop
        public void HandleEvent(in InputEvent e)
        {
            if (e.Type == EventType.MouseMove) mouse = e.Pos;
            else if (e.Type == EventType.MouseDown && e.Button == 1) mouseDown = true;
            else if (e.Type == EventType.MouseUp && e.Button == 1) mouseDown = false;
            if (HeroSheet != null)
            {
                HeroSheet.HandleEvent(e);
                if (HeroSheet.Dismissed) { HeroSheet = null; Sound.PlayConfirm(); }
                return;
            }
            if (Pause != null)
            {
                Pause.HandleEvent(e);
                var action = Pause.Action;
                if (action == PauseAction.Abandon) { EndTracking(RunEnd.Abandon); Run = null; stack.Clear(); stack.Add(new MainMenuScene(game)); }
                if (action != PauseAction.None) { Pause = null; Sound.PlayConfirm(); }
                return;
            }
            var top = Top;
            var stats = PauseMenu.StatsButtonRect(top);
            var wantsSheet = e.Type == EventType.KeyDown && e.Key == Key.C && !e.Repeat && !top.HasOverlay
                || e.Type == EventType.MouseDown && e.Button == 1 && stats.HasValue && stats.Value.Contains(e.Pos);
            if (CanPause() && wantsSheet) { OpenHeroSheet(); return; }
            var escape = e.Type == EventType.KeyDown && e.Key == Key.Escape;
            var button = PauseMenu.PauseButtonRect(top);
            var clicked = e.Type == EventType.MouseDown && e.Button == 1 && button.HasValue && button.Value.Contains(e.Pos);
            if (CanPause() && (escape || clicked))
            {
                // Escape first dismisses an existing collection or held card.
                if (escape && (top.HasOverlay || top is CombatScene held && held.HoldingCard)) { top.HandleEvent(e); return; }
                if (escape && e.Repeat) return;
                if (top is CombatScene combat) combat.CancelSelection();
                Pause = new PauseMenu(game.Art);
                Sound.PlayConfirm();
                return;
            }
            top.HandleEvent(e);
        }

        public void Update(float dt)
        {
            Sound.Update();
            uiTime += dt;
            if (HeroSheet != null) { HeroSheet.Update(dt); return; }
            if (Pause != null) { Pause.Update(dt); return; }
            var top = Top;
            top.Update(dt);
            HandleTransitions(top);
            OpenDeckChoice();
            UpdateGold(dt);
            CheckpointRun();
        }

        private void OpenDeckChoice()
        {
            var run = Run;
            if (run == null || run.PendingChoices.Count == 0) return;
            var top = Top;
            if (top is DeckChoiceScene || top is CombatScene || top is PackOpeningScene || top is MainMenuScene || top is CharacterSelectScene || top is DeathScene) return;
            var choice = run.NextChoice();
            if (choice != null) Push(new DeckChoiceScene(game, run, choice.Kind, choice.Count));
        }

        private void UpdateGold(float dt)
        {
            var run = Run;
            if (run == null) { goldRun = null; return; }
            if (!ReferenceEquals(goldRun, run)) { goldRun = run; GoldHud.Sync(run.Gold); interestSeen = run.InterestEarned; }
            if (run.InterestEarned > interestSeen) { GoldHud.Note("+" + (run.InterestEarned - interestSeen) + " interés"); interestSeen = run.InterestEarned; }
            GoldHud.Update(dt, run.Gold);
        }

        public void Draw(Canvas canvas)
        {
            var top = Top;
            top.Draw(canvas);
            if (CanPause() && Run != null && top.ShowGoldHud) GoldHud.Draw(canvas, top.GoldHudPos);
            var button = CanPause() ? PauseMenu.PauseButtonRect(top) : null;
            if (button.HasValue)
            {
                PauseMenu.DrawPauseButton(canvas, game.Art, button.Value, mouse, mouseDown, uiTime);
                PauseMenu.DrawStatsButton(canvas, game.Art, PauseMenu.StatsButtonRect(top).Value, mouse, mouseDown, uiTime);
            }
            Pause?.Draw(canvas);
            HeroSheet?.Draw(canvas);
        }

        // ------------------------------------------------------------------ transitions
        private void HandleTransitions(Scene top)
        {
            switch (top)
            {
                case MainMenuScene s: TMainMenu(s); break;
                case CharacterSelectScene s: TCharSelect(s); break;
                case MapScene s: TMap(s); break;
                case CombatScene s: TCombat(s); break;
                case CombatRewardScene s: TCombatReward(s); break;
                case TreasureScene s: TTreasure(s); break;
                case ShopScene s: TShop(s); break;
                case PackOpeningScene s: TPack(s); break;
                case EventScene s: TEvent(s); break;
                case BossRewardScene s: TBossReward(s); break;
                case WarlockScene s: if (s.Cleared) { s.Cleared = false; Pop(); } break;
                case GachaScene s: if (s.Cleared) { s.Cleared = false; Pop(); } break;
                case PurgeScene s: if (s.Cleared) { s.Cleared = false; Pop(); } break;
                case DeckChoiceScene s: if (s.Cleared) { s.Cleared = false; Pop(); } break;
                case SettingsScene s: if (s.Cleared) { s.Cleared = false; game.Prefs.Save(game.Storage); Pop(); } break;
                case DevSettingsScene s: if (s.Cleared) { s.Cleared = false; DevSettings.Save(game.Storage); Pop(); } break;
                case DeathScene s: TDeath(s); break;
            }
        }

        private void TMainMenu(MainMenuScene scene)
        {
            var action = scene.RequestedAction;
            if (action == MenuAction.None) return;
            scene.RequestedAction = MenuAction.None;
            switch (action)
            {
                case MenuAction.Play: Push(new CharacterSelectScene(game)); break;
                case MenuAction.Settings: Push(new SettingsScene(game)); break;
                case MenuAction.Dev: Push(new DevSettingsScene(game)); break;
                case MenuAction.Exit: QuitRequested = true; break;
            }
        }

        private void TCharSelect(CharacterSelectScene scene)
        {
            if (scene.Confirmed)
            {
                scene.Confirmed = false;
                Run = new RunState(scene.SelectedCharacter, scene.Seed, EnemyRoster.Expanded);   // the menu plays the floor-1 expansion
                BeginTracking(Run);
                Pop();
                Push(new MapScene(game, Run));
            }
            else if (scene.BackToMenu) { scene.BackToMenu = false; Pop(); }
        }

        private void PushCombat(List<EnemyState> enemies, RoomKind room)
        {
            var run = Run; var state = run.CreateCombat(enemies);
            game.Track(p => p.BeginCombat(run, state, room, enemies));
            Push(new CombatScene(game, state, run.OwnedRelics, room == RoomKind.Boss));
        }

        private void TMap(MapScene scene)
        {
            var node = scene.SelectedNode;
            if (node == null) return;
            scene.SelectedNode = null;
            var run = Run;
            if (run.EnterRoom(node.Id) == null) return;
            game.Track(p => p.RoomEntered());
            switch (node.Kind)
            {
                case RoomKind.Combat when Tuning.Current.GachaRooms: Push(new GachaScene(game, run)); break;
                case RoomKind.Combat: PushCombat(run.GenerateEnemies(node.Id), RoomKind.Combat); break;
                case RoomKind.Elite: PushCombat(run.GenerateElite(node.Id), RoomKind.Elite); break;
                case RoomKind.Boss: PushCombat(run.GenerateBoss(), RoomKind.Boss); break;
                case RoomKind.Treasure:
                    var treasure = run.PickTreasureRelics(node.Id);
                    relicChoice = game.Track(p => p.OfferRelics(run, "treasure", treasure.Select(r => r.Definition)));
                    Push(new TreasureScene(game, run, treasure)); break;
                case RoomKind.Shop: Push(new ShopScene(game, run)); break;
                case RoomKind.Event: Push(new EventScene(game, run, node.Id)); break;
                case RoomKind.Warlock: Push(new WarlockScene(game, run)); break;
                case RoomKind.Gacha: Push(new GachaScene(game, run)); break;
                case RoomKind.Purge: Push(new PurgeScene(game, run)); break;
            }
        }

        private void TCombat(CombatScene scene)
        {
            var run = Run;
            if (scene.CombatWon && !scene.VictoryAcknowledged)
            {
                scene.VictoryAcknowledged = true;
                Sound.PlayWin();
                var state = scene.State;
                var enemies = scene.ListedEnemies();      // the original's list: dead from earlier turns already dropped
                var room = run.CurrentRoomId ?? "unknown";
                game.Track(p => p.EndCombat(state, CombatOutcome.Won));
                if (scene.IsBoss)
                {
                    var gold = run.ApplyCombatVictory(state.Hp, enemies, state.GoldEarned, state.HpLostCombat, state.Turn, boss: true);
                    var relics = run.PickBossRelics();
                    relicChoice = game.Track(p => p.OfferRelics(run, "boss", relics.Select(r => r.Definition)));
                    Push(new BossRewardScene(game, run, gold, relics));
                }
                else if (scene.IsElite)
                {
                    var gold = run.ApplyCombatVictory(state.Hp, enemies, state.GoldEarned, state.HpLostCombat, state.Turn, elite: true);
                    var relic = run.PickEliteRelic(room);
                    var cards = run.PickRewardCards(room, luckBonus: RunState.EliteCardLuck);
                    cardChoice = game.Track(p => p.OfferCards(run, "elite_reward", cards.Select(c => c.Card)));
                    Push(new CombatRewardScene(game, run, gold, cards, relic, elite: true));
                }
                else
                {
                    var gold = run.ApplyCombatVictory(state.Hp, enemies, state.GoldEarned, state.HpLostCombat, state.Turn);
                    var cards = run.PickRewardCards(room);
                    cardChoice = game.Track(p => p.OfferCards(run, "reward", cards.Select(c => c.Card)));
                    Push(new CombatRewardScene(game, run, gold, cards));
                }
            }
            else if (scene.DeathOccurred && !scene.DeathAcknowledged)
            {
                scene.DeathAcknowledged = true;
                Sound.PlayDeath();
                EndTracking(RunEnd.Death);
                Push(new DeathScene(game, scene.TurnReached));
            }
        }

        private void TCombatReward(CombatRewardScene scene)
        {
            if (!scene.Cleared) return;
            scene.Cleared = false;
            var run = Run;
            // the elite's relic is optional (2026-11-08): only a taken one is acquired
            if (scene.TookRelic && scene.Relic != null) { run.AcquireRelic(scene.Relic); game.Track(p => p.RelicGained(run, scene.Relic.Definition, "elite")); }
            if (scene.ChosenCard != null) { run.AddCard(scene.ChosenCard); var choice = cardChoice; game.Track(p => p.Pick(choice, scene.ChosenCard.Id)); }
            cardChoice = null;
            Pop(); Pop();
        }

        private void TTreasure(TreasureScene scene)
        {
            if (!scene.Cleared) return;
            scene.Cleared = false;
            if (scene.ChosenRelic != null) TakeRelic(scene.ChosenRelic, "treasure");
            relicChoice = null;
            Pop();
        }

        private Func<List<OfferedCard>> PackReroll(PackTheme theme)
        {
            var run = Run;
            if (run == null || !run.HasRelic("loaded_die")) return null;
            return () =>
            {
                var cards = run.PickPackCards(theme, reroll: 1);
                var choice = cardChoice; game.Track(p => p.ReplaceOffer(choice, cards.Select(c => c.Card)));
                return cards;
            };
        }

        private void TShop(ShopScene scene)
        {
            if (scene.SelectedPack.HasValue)
            {
                var theme = scene.SelectedPack.Value; var chroma = scene.SelectedPackChroma;
                scene.SelectedPack = null; scene.SelectedPackChroma = ChromaKind.None;
                var run = Run;
                var cards = run.PickPackCards(theme);
                cardChoice = game.Track(p => p.OfferCards(run, "pack", cards.Select(c => c.Card)));
                var name = ChromaFx.Title(RunContent.Pack(theme).Name, chroma, true);
                Push(new PackOpeningScene(game, cards, name, theme, run.Floor, chroma, CardBonus.ForRun(run), LuckReport.For(run), run.PackPicks(chroma), PackReroll(theme)));
            }
            else if (scene.Cleared) { scene.Cleared = false; Pop(); }
        }

        private void TPack(PackOpeningScene scene)
        {
            if (!scene.Cleared) return;
            scene.Cleared = false;
            var choice = cardChoice;
            foreach (var card in scene.ChosenCards) { Run.AddCard(card); game.Track(p => p.Pick(choice, card.Id)); }
            cardChoice = null;
            Pop();
            if (Top is BossRewardScene boss) boss.PackDone(scene.ChosenCards);
        }

        private void TEvent(EventScene scene)
        {
            if (!scene.Cleared) return;
            scene.Cleared = false;
            // The event session has already settled its chosen outcome.
            Pop();
        }

        private void TBossReward(BossRewardScene scene)
        {
            if (scene.OpenPackRequested)
            {
                scene.OpenPackRequested = false;
                var run = Run;
                var cards = run.PickPackCards(PackTheme.Epico);
                cardChoice = game.Track(p => p.OfferCards(run, "boss_pack", cards.Select(c => c.Card)));
                Push(new PackOpeningScene(game, cards, RunContent.Pack(PackTheme.Epico).Name, PackTheme.Epico, run.Floor, ChromaKind.None,
                    CardBonus.ForRun(run), LuckReport.For(run), null, PackReroll(PackTheme.Epico)));
            }
            else if (scene.Cleared)
            {
                scene.Cleared = false;
                if (scene.ChosenRelic != null) TakeRelic(scene.ChosenRelic, "boss");
                relicChoice = null;
                Pop(); Pop();
                Run.AdvanceFloor();
                Push(new MapScene(game, Run));
            }
        }

        private void TDeath(DeathScene scene)
        {
            var action = scene.RequestedAction;
            if (action == DeathAction.None) return;
            scene.RequestedAction = DeathAction.None;
            Run = null;
            if (action == DeathAction.NewGame) { PopAllExceptFirst(); Push(new CharacterSelectScene(game)); }
            else if (action == DeathAction.MainMenu) PopAllExceptFirst();
        }

        // ------------------------------------------------------------------ power telemetry (docs/telemetry.md)
        private void TakeRelic(RelicOffer relic, string source)
        {
            var run = Run; var choice = relicChoice;
            run.AcquireRelic(relic);
            game.Track(p => { p.Pick(choice, relic.Definition.Id); p.RelicGained(run, relic.Definition, source); });
        }

        private double PlayedSeconds => Math.Max(0.0, game.Time - runStarted);

        /// <summary>A new run starts its own tracker (only when the runtime set a telemetry sink).</summary>
        private void BeginTracking(RunState run)
        {
            EndTracking(RunEnd.Abandon, null);     // a tracker left open (tests, tools) keeps its last checkpoint
            game.Power = null; cardChoice = relicChoice = null;
            if (game.Telemetry == null || run == null) return;
            runStarted = game.Time;
            try
            {
                game.Power = new PowerTracker(run, Guid.NewGuid().ToString("D"), game.Telemetry.GameVersion, game.Telemetry.PlayerId,
                    DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            }
            catch (Exception) { game.Power = null; }
        }

        /// <summary>The run ended: close its tracker and hand the record to the sink. An open fight is closed with it.</summary>
        private void EndTracking(RunEnd end) => EndTracking(end, Run);

        private void EndTracking(RunEnd end, RunState run)
        {
            var power = game.Power;
            game.Power = null; cardChoice = relicChoice = null;
            if (power == null || power.Finished) return;
            try
            {
                var record = power.Finish(run, end, PlayedSeconds, (Top as CombatScene)?.State);
                game.Telemetry?.Submit(record.RunId, PowerJson.Write(record));
            }
            catch (Exception) { }
        }

        /// <summary>Back on the map after something was recorded: save the run so far (it survives closing the game).</summary>
        private void CheckpointRun()
        {
            var power = game.Power; var run = Run;
            if (power == null || !power.Dirty || run == null || !(Top is MapScene) || HeroSheet != null || Pause != null) return;
            try
            {
                var record = power.Checkpoint(run, PlayedSeconds);
                game.Telemetry?.Checkpoint(record.RunId, PowerJson.Write(record));
            }
            catch (Exception) { game.Power = null; }
        }

        /// <summary>The game is closing: a run still on is sent as "quit" (the runtime uploads it on the next start if needed).</summary>
        public void EndSession() => EndTracking(RunEnd.Quit);
    }
}
