using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>scenes/combat_scene.CardPose: where a card is drawn (centre, size, tilt, face layout scale).</summary>
    public sealed class CardPose
    {
        public float X, Y, Scale = 1f, Angle; public float? Face;
        public CardPose(float x, float y, float scale = 1f, float angle = 0f, float? face = null) { X = x; Y = y; Scale = scale; Angle = angle; Face = face; }
        public CardPose Copy() => new CardPose(X, Y, Scale, Angle, Face);
    }

    /// <summary>
    /// scenes/combat_scene.py: the battle screen — hand fan with tweened motion and deals, card play (drag / click / keys),
    /// golden cast replays, animated enemies and hero, enemy turn, tooltips, history, inspect panel and Mage elements.
    /// Rules stay in <see cref="CombatState"/>; this only presents and forwards input.
    /// </summary>
    public sealed partial class CombatScene : Scene
    {
        // ------------------------------------------------------------------ layout (virtual 1280×720)
        private const int TopBarH = 68, HandAreaY = 465, CardY = 524, CardGap = 28, CardAreaX0 = 180, CardAreaX1 = 1100;
        private const int EnemyY = 125, PlayerX = 195, PlayerY = 90;
        private const int ManaCx = 68, ManaCy = 595, ManaR = 42;
        private const int DrawX = 1160, DrawY = 484, DiscardX = 1160, DiscardY = 596;
        private const int EndTurnX = 1074, EndTurnY = 9, EndTurnW = 196, EndTurnH = 48;
        private const float EnemyPhaseTime = 1.3f, DrawAfterCast = 0.28f, DrawAfterGoldenCast = 0.22f, DrawAfterEnemyTurn = 1.1f;

        private static readonly Dictionary<string, string> CardNamesEs = new Dictionary<string, string> { ["status_espora"] = "Espora", ["status_moho"] = "Moho" };
        private static readonly Rgba Toxic = new Rgba(170, 230, 70), HealText = new Rgba(120, 236, 120), VengeanceText = new Rgba(255, 90, 70), DebuffText = new Rgba(220, 120, 240);
        private const float VengeanceDelay = 1.0f, ReactionDelay = 0.35f;
        public const float LongPress = 0.5f;
        private static readonly Rgba TargetingColor = new Rgba(255, 190, 50);
        private const int PlayLineY = HandAreaY;
        private static readonly float HandBaseY = CardY + CardWidget.CardH / 2f - 12;
        private const float HoverScale = 1.3f, AimScale = 1.1f, MaxTilt = 10f, TiltStep = 2.5f, NeighbourPush = 46f, Ease = 16f, EaseHeld = 32f, LeaveSeconds = 0.3f;
        private static readonly (float x, float y) AimCenter = (640f, 500f), CastStage = (560f, 250f);
        private const float CastIntro = 0.35f, CastGap = 0.65f, CastOutro = 0.8f, KillHold = 0.7f, DeathHold = 0.35f;
        private static readonly Rgba[] Gold = { new Rgba(255, 250, 210), new Rgba(255, 214, 90), new Rgba(230, 150, 30), new Rgba(140, 80, 10) };
        private static readonly Rgba[] Gold2 = { Gold[0], Gold[1] }, Gold3 = { Gold[0], Gold[1], Gold[2] };
        private static readonly (int x, int y) EnemyHitPad = (40, 70);
        private static readonly Rgba Playable = new Rgba(255, 212, 60), Held = new Rgba(240, 232, 214);

        // ------------------------------------------------------------------ state
        private readonly CombatState state;
        private readonly IReadOnlyList<OwnedRelic> relics;
        private readonly DungeonBackdrop backdrop;
        private float idleTime;
        private string heroAction;           // attack | guard | hurt | cast | death
        private readonly string heroId;
        private readonly HeroSheets heroSheets;
        private readonly bool hasHeroSprites;
        private readonly float idleCycle;
        private float heroActionTime;
        public bool IsBoss { get; }
        public bool IsElite { get; }
        private bool victoryAcknowledged, deathAcknowledged;
        private readonly int initialEnemyCount;
        private readonly FxLayer fx;

        private (float x, float y) mouse;
        private readonly CardPlayInput play = new CardPlayInput(PlayLineY);
        private readonly Dictionary<CardDefinition, CardPose> motion = new Dictionary<CardDefinition, CardPose>();
        private readonly Dictionary<CardDefinition, Deal> deals = new Dictionary<CardDefinition, Deal>();
        private readonly List<float> dealPlan = new List<float>();
        private sealed class Leaving { public CardDefinition Card; public CardPose Pose; public float Age; public BigInteger Dmg; public long Blk; }
        private readonly List<Leaving> leaving = new List<Leaving>();
        private sealed class Sequence
        {
            public CardDefinition Card; public BigInteger Dmg; public long Blk; public PlayResult Result; public bool Attack; public string Action;
            public CardPose Start; public float T, Pulse; public int Fired; public int PrevBlock; public int[] PrevHp;
        }
        private Sequence sequence;
        private float holdTime;
        private List<(int hp, int block)> shownEnemies;
        private int? shownPlayerBlock;
        private readonly BurstParticles bursts = new BurstParticles(400, 11);
        private readonly Rng rng = new Rng(97);
        private float fxTime;

        /// <summary>Display index → index in <see cref="CombatState.Enemies"/>: the original drops the dead from its list after each enemy turn.</summary>
        private readonly List<int> view = new List<int>();
        private readonly Dictionary<string, EnemyAnimator> animById = new Dictionary<string, EnemyAnimator>();
        private readonly Dictionary<string, int> slotOf = new Dictionary<string, int>();
        private readonly EnemyFormation formation;
        private readonly List<(EnemyAnimator anim, int slot)> gone = new List<(EnemyAnimator, int)>();
        private readonly HashSet<string> avenging = new HashSet<string>();
        private readonly Dictionary<int, EnemyAnimator> enemyAnims = new Dictionary<int, EnemyAnimator>();
        private float? pendingHeroHurt;
        private string pendingHeroName = "hurt";
        private readonly HeroFx heroFx;

        private readonly List<IRect> cardRects = new List<IRect>();
        private readonly List<IRect> enemyRects = new List<IRect>();
        private readonly List<IRect> relicRects = new List<IRect>();
        private IRect? playerRect, endTurnRect, drawPileRect, discPileRect;
        private readonly IRect relicCollectionRect = new IRect(10, 16, 164, 36), handCollectionRect = new IRect(12, 668, 150, 36), drawInfoRect = new IRect(770, 12, 290, 42);
        public static readonly IRect PauseRect = new IRect(460, 16, 64, 36);
        public static readonly IRect ElementsButtonRect = new IRect(118, 618, 46, 36);
        private readonly ManaOrb orb = new ManaOrb();
        private float enemyPhase;
        private bool mouseDown;
        private readonly List<int> cardDrawOrder = new List<int>();

        private int? hoveredCard, hoveredEnemy, hoveredRelic;
        private bool hoveredPlayer, hoveredMana, hoveredDrawPile, hoveredDiscPile;
        private struct Detail { public bool Intent; public int Owner; public int J; }   // Owner -1 = the hero
        private Detail? hoveredDetail;
        private readonly List<EntityHitboxes> enemyHitboxes = new List<EntityHitboxes>();
        private readonly EntityHitboxes playerHitboxes = new EntityHitboxes();

        private IOverlay overlay;
        private string feedbackText = "";
        private float feedbackTime;
        private readonly ActionBanners banners = new ActionBanners();
        private readonly ReactionShow reactionShow;
        private readonly Dictionary<ReactionKind, int> reactionsExplained = new Dictionary<ReactionKind, int>();
        private int previewSig; private bool previewValid; private PlayPreview previewCached;
        private readonly Dictionary<int, ReactionKind> reactHints = new Dictionary<int, ReactionKind>();
        private readonly Dictionary<int, ReactionKind> reactDomain = new Dictionary<int, ReactionKind>();
        private ((float x, float y) pos, int who, float held)? longPress;   // who: -1 hero, i enemy
        private readonly CombatLog log = new CombatLog();
        private readonly CombatLogStrip logStrip = new CombatLogStrip();
        private readonly LogDetail logDetail = new LogDetail();
        private readonly Dictionary<string, Sprite?> portraits = new Dictionary<string, Sprite?>();

        public CombatScene(GameContext game, CombatState state, IReadOnlyList<OwnedRelic> relics, bool isBoss = false) : base(game)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.relics = relics ?? Array.Empty<OwnedRelic>();
            backdrop = new DungeonBackdrop(game.Art);
            var heroName = state.Hero.Name;
            heroId = Art.HeroIdFor(heroName) ?? "warrior";
            hasHeroSprites = Art.HeroIdFor(heroName) != null;
            heroSheets = game.Art.Hero(heroId);
            idleCycle = game.Art.Hero("warrior").Seconds("idle");
            IsBoss = isBoss;
            for (var i = 0; i < state.Enemies.Count; i++) { view.Add(i); if (state.Enemies[i].Definition.Elite) IsElite = true; }
            initialEnemyCount = state.Enemies.Count;
            fx = new FxLayer(game.Art, game.Fonts.Get(16));
            var large = 0; var bringsSmall = false;
            for (var i = 0; i < state.Enemies.Count; i++)
            {
                var enemy = state.Enemies[i];
                var sheet = game.Art.SheetForEnemy(enemy.Definition.Name);
                if (sheet != null) animById[enemy.Id] = new EnemyAnimator(sheet, 31 + 17 * i, 0.47f * i);
                if (sheet != null && sheet.IsBoss) large++;
                var profile = EnemyCatalog.Profile(enemy.Definition.Ai);
                if (profile == null) continue;
                foreach (var ai in profile.Summons.Concat(profile.Spawns))
                {
                    var spawned = EnemyCatalog.Definition(ai);
                    _ = game.Art.SheetForEnemy(spawned.Name);   // preload: the first arrival does not stall
                    if (!EnemyField.IsLarge(spawned)) bringsSmall = true;
                }
            }
            formation = new EnemyFormation(EnemyFormation.Choose(large, bringsSmall));
            for (var i = 0; i < state.Enemies.Count; i++)
            {
                var enemy = state.Enemies[i];
                if (formation.Shape == EnemyFormation.Kind.Line3) { slotOf[enemy.Id] = i; continue; }
                var isLarge = animById.TryGetValue(enemy.Id, out var a) && a.Sheet.IsBoss;
                var slot = formation.Pick(isLarge, slotOf.Values.ToList(), Array.Empty<int>());
                slotOf[enemy.Id] = slot >= 0 ? slot : formation.AnyOfSize(isLarge);
            }
            SyncEnemyAnims(null);
            var strike = hasHeroSprites ? heroSheets.StrikeSeconds : 0f;
            var castRelease = heroSheets.CastSeconds;
            var authoredWarrior = heroId == "warrior" && castRelease > 0;
            if (strike > 0)
                heroFx = new HeroFx(strike, 5, 260, heroSheets.StrikeOffset, heroSheets.CastOffset, castRelease > 0 ? castRelease : (float?)null, heroId == "mage",
                    authoredWarrior ? heroSheets.Seconds("guard") * 0.4f : (float?)null, authoredWarrior ? heroSheets.Seconds("death") * 0.6f : (float?)null);
            AnnouncePair();
            reactionShow = new ReactionShow(bursts, 17);
            log.Turn(state.Turn);
            logStrip.Sync(log);
        }

        public CombatState State => state;
        /// <summary>The enemies the original's list still holds (it drops the dead after each enemy turn): the victory gold counts these.</summary>
        public List<EnemyState> ListedEnemies() { var list = new List<EnemyState>(); for (var i = 0; i < EnemyCount; i++) list.Add(E(i)); return list; }
        public CombatLog Log => log;
        public int TurnReached => state.Turn;
        public string HeroAction => heroAction;
        public IReadOnlyDictionary<int, EnemyAnimator> EnemyAnimators => enemyAnims;
        public override (Anchor anchor, float x, float y)? GoldHudPos => (Anchor.TopLeft, 12, 476);
        public override RectF? PauseButtonRect => PauseRect;
        public override bool HasOverlay => overlay != null;
        /// <summary>A card is held (Escape cancels it before the pause menu opens).</summary>
        public bool HoldingCard => play.Active;
        public IOverlay Overlay => overlay;

        private EnemyState E(int i) => state.Enemies[view[i]];
        private int EnemyCount => view.Count;
        private int? ViewOf(int domainIndex) { var i = view.IndexOf(domainIndex); return i >= 0 ? i : (int?)null; }
        private int? ViewOfId(string id) { for (var i = 0; i < view.Count; i++) if (E(i).Id == id) return i; return null; }
        private int? Domain(int? viewIndex) => viewIndex.HasValue && viewIndex.Value >= 0 && viewIndex.Value < view.Count ? view[viewIndex.Value] : (int?)null;
        private CardInfo Info(CardDefinition card) => Game.Data.Card(card);
        private bool Unplayable(CardDefinition card) => Info(card).Unplayable;
        private bool Affordable(CardDefinition card) => state.CardCost(card) <= state.Mana;
        private float HeroSeconds(string name) => name != null ? heroSheets.Seconds(name) : 0f;

        // ------------------------------------------------------------------ outcome
        /// <summary>A multi-cast replay or a killing blow is still on screen (victory waits for it).</summary>
        public bool PresentationBusy => sequence != null || holdTime > 0;

        public bool DeathOccurred
        {
            get
            {
                if (PresentationBusy || pendingHeroHurt.HasValue) return false;
                if (heroAction == "death" && heroActionTime < HeroSeconds("death") + DeathHold) return false;
                return state.Hp <= 0;
            }
        }

        public bool CombatWon
        {
            get
            {
                if (victoryAcknowledged || PresentationBusy || EnemiesDying) return false;
                if (initialEnemyCount == 0) return false;
                for (var i = 0; i < EnemyCount; i++) if (E(i).Alive) return false;
                return true;
            }
        }

        public bool VictoryAcknowledged { get => victoryAcknowledged; set => victoryAcknowledged = value; }
        public bool DeathAcknowledged { get => deathAcknowledged; set => deathAcknowledged = value; }

        public bool EnemiesDying { get { foreach (var a in animById.Values) if (a.Dead && !a.DeathDone) return true; return false; } }

        // ------------------------------------------------------------------ protocol
        public override void HandleEvent(in InputEvent e)
        {
            if (overlay != null) { overlay.HandleEvent(e); return; }
            if (logStrip.HandleEvent(e)) { if (e.Type == EventType.MouseMove) mouse = e.Pos; return; }
            if (PresentationBusy && (e.Type == EventType.MouseDown || e.Type == EventType.KeyDown)) return;
            switch (e.Type)
            {
                case EventType.MouseMove:
                    mouse = e.Pos;
                    if (longPress.HasValue && Math.Abs(e.X - longPress.Value.pos.x) + Math.Abs(e.Y - longPress.Value.pos.y) > 10) longPress = null;
                    play.Move(e.Pos, EnemyAt(e.Pos));
                    if (play.Active) ClearHover(); else UpdateHover(e.Pos);
                    break;
                case EventType.MouseDown when e.Button == 1:
                    mouseDown = true;
                    if (!play.Active)
                    {
                        var who = InspectableAt(e.Pos);
                        longPress = who.HasValue ? (e.Pos, who.Value, 0f) : (((float, float), int, float)?)null;
                    }
                    HandleClick(e.Pos);
                    break;
                case EventType.MouseUp when e.Button == 1:
                    mouseDown = false; longPress = null;
                    HandleRelease(e.Pos);
                    break;
                case EventType.MouseDown when e.Button == 3:
                    {
                        var who = play.Active ? null : InspectableAt(e.Pos);
                        if (who.HasValue) OpenInspect(who.Value); else CancelSelection();
                        break;
                    }
                case EventType.KeyDown:
                    HandleKey(e.Key);
                    break;
            }
        }

        public override void Update(float dt)
        {
            var pdt = Math.Max(0f, dt);
            AdvanceHeroAnimation(pdt);
            (float, float) hero = (PlayerX + EntityWidget.PlayerW / 2f, PlayerY + EntityWidget.PlayerH / 2f - 8);
            foreach (var pair in enemyAnims)
                if (pair.Value.Style.Blades && pair.Key < EnemyCount) { pair.Value.Blades = E(pair.Key).Statuses[StatusKind.Blades]; pair.Value.Target = hero; }
            foreach (var anim in animById.Values) anim.Update(dt);
            gone.RemoveAll(g => !g.anim.Busy);
            AnnounceVengeance();
            heroFx?.Update(dt);
            if (pendingHeroHurt.HasValue)
            {
                pendingHeroHurt -= pdt;
                if (pendingHeroHurt <= 0) { pendingHeroHurt = null; PlayHeroAction(pendingHeroName); }
            }
            backdrop.Update(pdt);
            if (overlay != null) play.Cancel();
            if (overlay != null && overlay.Dismissed) { overlay = null; Sound.PlayCancel(); }
            feedbackTime = Math.Max(0f, feedbackTime - dt);
            banners.Update(dt);
            orb.Update(dt, state.Mana, state.ManaMaximum);
            enemyPhase = Math.Max(0f, enemyPhase - pdt);
            fx.Update(dt);
            fxTime += pdt;
            AdvanceCards(pdt);
            bursts.Update(dt);
            holdTime = Math.Max(0f, holdTime - pdt);
            AdvanceSequence(pdt);
            reactionShow.Update(dt);
            if (longPress.HasValue && overlay == null)
            {
                var lp = longPress.Value; lp.held += pdt; longPress = lp;
                if (lp.held >= LongPress) { longPress = null; OpenInspect(lp.who); }
            }
            logStrip.Sync(log);
            logStrip.Update(dt);
            overlay?.Update(dt);
        }

        // ------------------------------------------------------------------ hit tests and hover
        private int? EnemyAt((float x, float y) pos)
        {
            for (var i = 0; i < enemyRects.Count; i++)
                if (i < EnemyCount && E(i).Alive && enemyRects[i].Inflate(EnemyHitPad.x, EnemyHitPad.y).Contains(pos.x, pos.y)) return i;
            return null;
        }

        private int? CardAt((float x, float y) pos)
        {
            for (var k = cardDrawOrder.Count - 1; k >= 0; k--)
            {
                var i = cardDrawOrder[k];
                if (i < state.Hand.Count && i < cardRects.Count && cardRects[i].Contains(pos.x, pos.y)) return i;
            }
            return null;
        }

        private void ClearHover()
        {
            hoveredCard = hoveredEnemy = hoveredRelic = null;
            hoveredPlayer = hoveredMana = hoveredDrawPile = hoveredDiscPile = false;
            hoveredDetail = null;
        }

        private Detail? DetailAt((float x, float y) pos)
        {
            for (var i = 0; i < enemyHitboxes.Count; i++)
            {
                if (i < EnemyCount && !E(i).Alive) continue;
                var boxes = enemyHitboxes[i];
                if (boxes.Intent.HasValue && boxes.Intent.Value.Inflate(6, 6).Contains(pos.x, pos.y)) return new Detail { Intent = true, Owner = i };
                for (var j = 0; j < boxes.Statuses.Count; j++) if (boxes.Statuses[j].Rect.Contains(pos.x, pos.y)) return new Detail { Owner = i, J = j };
            }
            for (var j = 0; j < playerHitboxes.Statuses.Count; j++) if (playerHitboxes.Statuses[j].Rect.Contains(pos.x, pos.y)) return new Detail { Owner = -1, J = j };
            return null;
        }

        private void UpdateHover((float x, float y) pos)
        {
            var previous = hoveredCard;
            ClearHover();
            var card = CardAt(pos);
            if (card.HasValue) { hoveredCard = card; if (card != previous) Sound.PlayNav(); return; }
            hoveredDetail = DetailAt(pos);
            if (hoveredDetail.HasValue) return;
            for (var i = 0; i < enemyRects.Count; i++) if (enemyRects[i].Contains(pos.x, pos.y)) { hoveredEnemy = i; return; }
            for (var i = 0; i < relicRects.Count; i++) if (relicRects[i].Contains(pos.x, pos.y)) { hoveredRelic = i; return; }
            if (playerRect.HasValue && playerRect.Value.Contains(pos.x, pos.y)) { hoveredPlayer = true; return; }
            float mx = pos.x - ManaCx, my = pos.y - ManaCy;
            if (mx * mx + my * my <= ManaR * ManaR) { hoveredMana = true; return; }
            if (drawPileRect.HasValue && drawPileRect.Value.Contains(pos.x, pos.y)) { hoveredDrawPile = true; return; }
            if (discPileRect.HasValue && discPileRect.Value.Contains(pos.x, pos.y)) { hoveredDiscPile = true; return; }
        }

        // ------------------------------------------------------------------ input
        private void HandleClick((float x, float y) pos)
        {
            if (ElementsRelevant() && ElementsButtonRect.Contains(pos.x, pos.y)) { OpenElementWheel(); return; }
            var onRelic = relicCollectionRect.Contains(pos.x, pos.y);
            foreach (var r in relicRects) onRelic |= r.Contains(pos.x, pos.y);
            if (onRelic) { overlay = new RelicViewer(Game, relics); Sound.PlayConfirm(); return; }
            if (handCollectionRect.Contains(pos.x, pos.y)) { overlay = new PileViewer(Game, "Tu mano", state.Hand, CombatBonus()); Sound.PlayCard(); return; }
            if (drawPileRect.HasValue && drawPileRect.Value.Contains(pos.x, pos.y)) { overlay = new PileViewer(Game, "Pila de Robo", state.DrawPile, CombatBonus()); Sound.PlayCard(); return; }
            if (discPileRect.HasValue && discPileRect.Value.Contains(pos.x, pos.y)) { overlay = new PileViewer(Game, "Pila de Descarte", state.DiscardPile, CombatBonus()); Sound.PlayCard(); return; }
            if (endTurnRect.HasValue && endTurnRect.Value.Contains(pos.x, pos.y)) { if (enemyPhase <= 0) DoEndTurn(); return; }
            var cardIndex = CardAt(pos);
            if (cardIndex.HasValue) { PickCard(cardIndex.Value, pos); return; }
            if (play.Active) { Finish(play.Click(pos, EnemyAt(pos))); return; }
            CancelSelection();
        }

        /// <summary>combat_card_bonus: Ataque + Orbe de Fuego + Fuerza, Destreza.</summary>
        private CardBonus CombatBonus() => new CardBonus(state.Hero.AttackBonus + RelicTotal("fire_orb", 2) + state.Strength, state.Hero.Dexterity);

        private int RelicTotal(string tag, int amount)
        {
            long total = 0;
            foreach (var r in relics) if (r.Active && r.Definition.Tag == tag) total += (long)amount * r.Multiplier;
            return (int)Math.Min(int.MaxValue, total);
        }

        private void HandleRelease((float x, float y) pos)
        {
            if (play.Active && !play.Sticky) Finish(play.Release(pos, EnemyAt(pos)));
        }

        private List<int> AliveViews()
        {
            var alive = new List<int>();
            for (var i = 0; i < EnemyCount; i++) if (E(i).Alive) alive.Add(i);
            return alive;
        }

        private void HandleKey(Key key)
        {
            if (key == Key.Escape) CancelSelection();
            else if (key >= Key.D1 && key <= Key.D9)
            {
                var index = key - Key.D1;
                if (index < state.Hand.Count) PickCard(index, null);
            }
            else if (key == Key.Left || key == Key.Right || key == Key.Tab) play.CycleTarget(AliveViews(), key == Key.Left ? -1 : 1);
            else if (key == Key.Return || key == Key.KeypadEnter || key == Key.Space) { if (play.Active) Finish(play.Confirm()); }
            else if (key == Key.E && enemyPhase <= 0) DoEndTurn();
            else if (key == Key.I && !play.Active && InspectableAt(mouse).HasValue) OpenInspect(InspectableAt(mouse).Value);
            else if (key == Key.R && ElementsRelevant()) OpenElementWheel();
        }

        private static TargetKind KindOf(CardInfo info) => info.Target == "ENEMY" ? TargetKind.Enemy : info.Target == "ALL_ENEMIES" ? TargetKind.AllEnemies : TargetKind.Self;

        private void PickCard(int index, (float x, float y)? pos)
        {
            var card = state.Hand[index];
            if (Unplayable(card)) { ShowError("Esta carta no se puede jugar"); return; }
            if (!Affordable(card)) { orb.Error(); ShowError("Maná insuficiente"); return; }
            var kind = KindOf(Info(card));
            if (!pos.HasValue)
            {
                var alive = AliveViews();
                play.PickWithKey(index, kind, alive.Count > 0 ? alive[0] : (int?)null);
            }
            else play.Pick(index, kind, pos.Value);
            hoveredCard = null;
            Sound.PlayConfirm();
        }

        private void Finish(PlayRequest? request)
        {
            if (request.HasValue) DoPlayCard(request.Value.CardIndex, request.Value.TargetIndex);
        }

        public void CancelSelection()
        {
            if (play.Cancel()) Sound.PlayCancel();
        }

        private void ShowError(string message)
        {
            feedbackText = message; feedbackTime = 1.8f;
            Sound.PlayError();
        }

        /// <summary>Right click / long press / I: the inspect panel of an enemy (view index) or the hero (-1).</summary>
        private int? InspectableAt((float x, float y) pos)
        {
            var i = EnemyAt(pos);
            if (i.HasValue) return i;
            if (playerRect.HasValue && playerRect.Value.Contains(pos.x, pos.y)) return -1;
            return null;
        }

        public void OpenInspect(int who)
        {
            TooltipContent content; Sprite? portrait;
            if (who >= 0 && who < EnemyCount)
            {
                var enemy = E(who);
                content = Tooltip.Enemy(Game, enemy, enemy.HitDamage(state.HeroStatuses), state);
                portrait = Portrait(enemy.Definition.Name);
            }
            else if (who == -1) { content = Tooltip.Player(Game, state, IncomingDamage(), state.Hero.Luck); portrait = Portrait(CombatLog.Hero); }
            else return;
            play.Cancel();
            ClearHover();
            overlay = new InspectOverlay(Game, content, portrait);
            Sound.PlayConfirm();
        }

        public void OpenElementWheel()
        {
            play.Cancel();
            overlay = new ElementWheelOverlay(Game);
            Sound.PlayConfirm();
        }

        // ------------------------------------------------------------------ derived values
        public bool NothingToPlay
        {
            get { foreach (var c in state.Hand) if (!Unplayable(c) && Affordable(c)) return false; return true; }
        }

        /// <summary>Damage every living enemy's ATTACK intent will deal this enemy turn (before block).</summary>
        private int IncomingDamage()
        {
            long total = 0;
            for (var i = 0; i < EnemyCount; i++)
            {
                var e = E(i);
                if (e.Alive && e.Intent.Kind == IntentKind.Attack) total += (long)e.HitDamage(state.HeroStatuses) * Math.Max(1, e.Intent.Hits);
            }
            return (int)Math.Min(int.MaxValue, total);
        }

        private uint Ready(CardDefinition card, int? target = null) => state.ReadyMask(card, Domain(target));

        private BigInteger ReadyDamage(CardDefinition card) => CardTotals.Damage(card, Ready(card));

        /// <summary>_bonuses: what the hand adds to a card's printed damage / block (0 when it has none).</summary>
        private (BigInteger dmg, long blk) Bonuses(CardDefinition card)
        {
            var mask = Ready(card);
            var (dmg, blk) = state.CardBonuses(card);
            return (CardTotals.Damage(card, mask) > 0 ? dmg : 0, CardTotals.Block(card, mask) > 0 ? blk : 0);
        }

        private CardShow HandShow(CardDefinition card, int? target = null)
        {
            var (dmg, blk) = Bonuses(card);
            return new CardShow { Unaffordable = !Affordable(card), BonusDamage = dmg, BonusBlock = blk, Ready = Ready(card, target), Cost = state.CardCost(card) };
        }

        private bool ElementsRelevant()
        {
            bool Any(IEnumerable<CardDefinition> pile) { foreach (var c in pile) { var b = Info(c).Badges; if (b != null && b.Length > 0) return true; } return false; }
            if (Any(state.Hand) || Any(state.DrawPile) || Any(state.DiscardPile) || Any(state.ActivePowers) || Any(state.ExhaustPile)) return true;
            for (var i = 0; i < EnemyCount; i++) if (ElementPreview.Held(E(i)).HasValue) return true;
            return false;
        }

        private Sprite? Portrait(string key)
        {
            if (portraits.TryGetValue(key, out var hit)) return hit;
            Sprite? image = null;
            if (key == CombatLog.Hero) { if (hasHeroSprites) image = heroSheets.Frame("idle", 0f, 96, out _); }
            else
            {
                var sheet = Art.SheetForEnemy(key);
                if (sheet != null) image = sheet.Portrait();
            }
            portraits[key] = image;
            return image;
        }
    }
}
