using System;
using System.Collections.Generic;
using System.Numerics;
using LastWish.Domain;

namespace LastWish.Presentation
{
    public sealed partial class CombatScene
    {
        public override void Draw(Canvas canvas)
        {
            var (dx, dy) = reactionShow.ShakeOffset();
            if (dx == 0 && dy == 0) { DrawFrame(canvas); return; }
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(0, 0, 0));   // a reaction hit: the screen shakes a little
            canvas.PushTranslate(dx, dy);
            DrawFrame(canvas);
            canvas.PopTransform();
        }

        private void DrawFrame(Canvas canvas)
        {
            backdrop.Draw(canvas);
            DrawTopBar(canvas);
            DrawBattlefield(canvas);
            logStrip.Draw(canvas, Art);
            DrawHandArea(canvas);
            DrawLeaving(canvas);
            DrawCastStage(canvas);
            bursts.Draw(canvas, Art);
            reactionShow.Draw(canvas, Art, Game.Data.Glossary);
            banners.Draw(canvas, Art, Game.Data.Glossary);
            fx.Draw(canvas);
            if (feedbackTime > 0) Txt.DrawAt(canvas, Fonts.Get(16), feedbackText, Anchor.Center, 640, HandAreaY - 48, new Rgba(245, 165, 125));
            DrawTargets(canvas);
            DrawElementPreview(canvas);
            DrawHeldCard(canvas);
            if (play.Active) DrawTargetingHint(canvas);

            if (overlay != null) overlay.Draw(canvas);
            else if (!play.Active && DrawLogDetail(canvas)) { }
            else if (!play.Active)
            {
                var tooltip = GetTooltip();
                if (tooltip != null)
                {
                    RectF? beside = null;
                    if (hoveredCard.HasValue && hoveredCard.Value < cardRects.Count) beside = cardRects[hoveredCard.Value];
                    if (!beside.HasValue && !hoveredDetail.HasValue)
                    {
                        if (hoveredEnemy.HasValue && hoveredEnemy.Value < enemyRects.Count) beside = enemyRects[hoveredEnemy.Value];
                        else if (hoveredPlayer && playerRect.HasValue) beside = playerRect.Value;
                    }
                    Tooltip.Draw(canvas, Game, tooltip, mouse, beside);
                }
            }
        }

        // ------------------------------------------------------------------ top bar
        private void DrawTopBar(Canvas canvas)
        {
            PixelUi.Topbar(canvas, Art, TopBarH);
            var t = idleTime;
            var shownRelics = new List<OwnedRelic>(5);
            for (var i = 0; i < Math.Min(5, relics.Count); i++) shownRelics.Add(relics[i]);
            Hud.DrawRelics(canvas, Art, shownRelics, 184, 10, hoveredRelic, fxTime, relicRects);
            PixelUi.Button(canvas, Art, relicCollectionRect, "Reliquias (" + relics.Count + ")", "bronze", PixelUi.StateOf(relicCollectionRect, mouse, mouseDown), "bag", null, t, 0, 14);
            PixelUi.Panel(canvas, Art, drawInfoRect);
            var count = Math.Min(state.CardsPerTurn, CombatState.HandMaxSize);
            float x = drawInfoRect.X + 12;
            var font = Fonts.Get(14);
            foreach (var (iconName, text) in new[] { ("draw", "Robo: " + count + "/turno"), ("hand_cards", "Mano máx.: " + CombatState.HandMaxSize) })
            {
                if (Art.HasIcon(iconName))
                {
                    var icon = Art.Icon(iconName); var r = Txt.Place(icon.W * 2, icon.H * 2, Anchor.MidLeft, x, drawInfoRect.CenterY);
                    canvas.Blit(icon, r.X, r.Y, 2); x += icon.W * 2 + 3;
                }
                var lr = Txt.OutlinedAt(canvas, font, text, Anchor.MidLeft, x, drawInfoRect.CenterY, Colors.Cream);
                x += lr.W + 16;
            }
            Hud.DrawTurnCounter(canvas, Art, state.Turn, Canvas.Width / 2 + 22, 34);
            var rect = new IRect(EndTurnX, EndTurnY, EndTurnW, EndTurnH);
            endTurnRect = Hud.DrawEndTurn(canvas, Art, rect, rect.Contains(mouse.x, mouse.y), mouseDown, enemyPhase <= 0, NothingToPlay, t);
        }

        // ------------------------------------------------------------------ battlefield
        private IRect BossRect(EnemyAnimator anim)
        {
            var height = Math.Max(120, anim.Sheet.Anchor.y - anim.Sheet.Top - 4);
            var top = Math.Max(TopBarH + 60, BossGround - height);
            return new IRect(BossCx - BossW / 2, top, BossW, BossGround - 6 - top);
        }

        private void DrawBattlefield(Canvas canvas)
        {
            canvas.Line(0, HandAreaY, Canvas.Width, HandAreaY, Colors.PanelBorder);
            enemyRects.Clear();
            var incoming = IncomingDamage();
            var lethal = incoming - state.Block >= state.Hp && state.Hp > 0;
            var t = idleTime;
            foreach (var (anim, slot) in gone)
            {
                var (gx, gy) = EnemySlots[Math.Min(slot, EnemySlots.Length - 1)];
                anim.Draw(canvas, Art, (gx + EntityWidget.EnemyW / 2, gy + EntityWidget.EnemyH + 6));
            }
            ReactionTargets();
            var g = Game.Data.Glossary;
            for (var i = 0; i < EnemyCount; i++)
            {
                var enemy = E(i);
                var slot = slotOf.TryGetValue(enemy.Id, out var s) ? s : i;
                if (slot >= EnemySlots.Length) break;
                var (ex, ey) = EnemySlots[slot];
                DrawEnemyFloor(canvas, i, enemy, ex, ey);
                (int, int)? shown = shownEnemies != null && i < shownEnemies.Count ? shownEnemies[i] : ((int, int)?)null;
                var aliveShown = shown.HasValue ? shown.Value.Item1 > 0 : enemy.Alive;
                enemyAnims.TryGetValue(i, out var anim);
                var hit = enemy.HitDamage(state.HeroStatuses);
                while (enemyHitboxes.Count <= i) enemyHitboxes.Add(new EntityHitboxes());
                var boxes = enemyHitboxes[i]; boxes.Clear();
                var attacking = lethal && enemy.Intent.Kind == IntentKind.Attack;
                IRect rect;
                if (anim != null && anim.Sheet.IsBoss)
                {
                    rect = BossRect(anim);
                    anim.Draw(canvas, Art, (BossCx, BossGround));
                    if (aliveShown) rect = EntityWidget.DrawEnemy(canvas, Art, g, enemy, rect.X, rect.Y, false, false, null, false, (rect.W, rect.H), hit, attacking, t, boxes, shown);
                }
                else if (anim != null)
                {
                    rect = new IRect(ex, ey, EntityWidget.EnemyW, EntityWidget.EnemyH);
                    anim.Draw(canvas, Art, (rect.CenterX, rect.Bottom + 6));
                    if (aliveShown) rect = EntityWidget.DrawEnemy(canvas, Art, g, enemy, ex, ey, false, false, null, false, null, hit, attacking, t, boxes, shown);
                }
                else rect = EntityWidget.DrawEnemy(canvas, Art, g, enemy, ex, ey, false, false, null, true, null, hit, attacking, t, boxes, shown);
                enemyRects.Add(rect);
                if (reactHints.TryGetValue(i, out var hint) && boxes.Element.HasValue && enemy.Alive)
                    ElementPreview.DrawReactHint(canvas, Art, g, boxes.Element.Value, hint, fxTime);
            }
            while (enemyHitboxes.Count > EnemyCount) enemyHitboxes.RemoveAt(enemyHitboxes.Count - 1);
            DrawPlayer(canvas);
            if (state.ActivePowers.Count > 0) DrawActivePowers(canvas);
        }

        private void DrawPlayer(Canvas canvas)
        {
            (float, float) center = (PlayerX + EntityWidget.PlayerW / 2f, PlayerY + EntityWidget.PlayerH / 2f);
            heroFx?.DrawShadow(canvas, Art, center);
            var loss = shownEnemies == null ? Math.Max(0, IncomingDamage() - state.Block) : 0;
            if (hasHeroSprites)
            {
                var frame = heroSheets.Frame(heroAction ?? "idle", heroAction != null ? heroActionTime : idleTime, 192, out var scale);
                int w = (int)(frame.W * scale), h = (int)(frame.H * scale);
                var r = IRect.AtCenter(PlayerX + EntityWidget.PlayerW / 2, PlayerY + EntityWidget.PlayerH / 2, w, h);
                canvas.Blit(frame, r.X, r.Y, scale);
            }
            playerRect = EntityWidget.DrawPlayer(canvas, Art, Game.Data.Glossary, state, PlayerX, PlayerY, null, !hasHeroSprites, loss, idleTime, playerHitboxes, shownPlayerBlock);
            heroFx?.Draw(canvas, Art, center);
        }

        private void DrawActivePowers(Canvas canvas)
        {
            Fonts.Get(10).Draw(canvas, "PODERES ACTIVOS", 560, HandAreaY - 80, Colors.TextSecondary);
            for (var i = 0; i < state.ActivePowers.Count; i++)
                CardWidget.Draw(canvas, Game, state.ActivePowers[i], 560 + i * (CardWidget.CardW + 6), HandAreaY - 78, default);
        }

        // ------------------------------------------------------------------ hand area
        /// <summary>_hand_shade: black ramp darkening downwards; the top 26 rows are a warm glow (SRCALPHA lines overwrite).</summary>
        private Sprite HandShade(int h)
        {
            return new Sprite(Art.Procedural("combat_hand_shade_" + h, 1, h, () =>
            {
                var px = new Rgba[h];
                for (var y = 0; y < h; y++) px[y] = new Rgba(0, 0, 0, (int)(10 + 70 * Math.Pow(y / (double)Math.Max(1, h - 1), 1.5)));
                for (var y = 0; y < Math.Min(26, h); y++) px[y] = new Rgba(255, 190, 110, (int)(22 * (1 - y / 26.0)));
                return px;
            }));
        }

        private void DrawHandArea(Canvas canvas)
        {
            var bg = new IRect(0, HandAreaY, Canvas.Width, Canvas.Height - HandAreaY);
            canvas.Fill(bg, Colors.BgCardArea);
            canvas.BlitRect(HandShade(bg.H), bg, Rgba.White);
            PixelUi.Trim(canvas, Art, HandAreaY);
            orb.Draw(canvas, Art, ManaCx, ManaCy, state.Mana, state.ManaMaximum);

            var hand = state.Hand;
            var poses = TargetPoses();
            var held = HeldIndex();
            var dealing = DealingIndices();
            cardRects.Clear();
            for (var i = 0; i < hand.Count; i++) cardRects.Add(PoseRect(poses[i]));
            cardDrawOrder.Clear();
            for (var i = 0; i < hand.Count; i++) if (i != held && !dealing.Contains(i)) cardDrawOrder.Add(i);
            var hovered = held.HasValue ? null : hoveredCard;
            if (hovered.HasValue && cardDrawOrder.Remove(hovered.Value)) cardDrawOrder.Add(hovered.Value);
            foreach (var i in cardDrawOrder)
            {
                var card = hand[i];
                var pose = motion.TryGetValue(card, out var p) ? p : SpawnPose();
                CardWidget.DrawAt(canvas, Game, card, pose.X, pose.Y, HandShow(card), pose.Scale, pose.Angle,
                    i == hovered ? CardWidget.RarityColor(card.Rarity) : (Rgba?)null, pose.Face);
            }
            DrawDealt(canvas, dealing);
            var waiting = WaitingIndices().Count;
            drawPileRect = Hud.DrawPile(canvas, Art, true, state.DrawPile.Count + waiting, DrawX, DrawY, hoveredDrawPile);
            discPileRect = Hud.DrawPile(canvas, Art, false, state.DiscardPile.Count, DiscardX, DiscardY, hoveredDiscPile);
            if (ElementsRelevant())
                PixelUi.Button(canvas, Art, ElementsButtonRect, "", "bronze", PixelUi.StateOf(ElementsButtonRect, mouse, mouseDown), "reaction", "R", idleTime, 0, 14);
            PixelUi.Button(canvas, Art, handCollectionRect, "Mano " + (hand.Count - waiting) + "/" + CombatState.HandMaxSize, "bronze",
                PixelUi.StateOf(handCollectionRect, mouse, mouseDown), "hand_cards", null, idleTime, 0, 14);
        }

        private void DrawDealt(Canvas canvas, HashSet<int> dealing)
        {
            var hand = state.Hand;
            var order = new List<int>(dealing); order.Sort();
            foreach (var i in order)
            {
                var card = hand[i];
                if (!deals.TryGetValue(card, out var deal) || !motion.TryGetValue(card, out var pose) || deal.Waiting) continue;
                var show = HandShow(card); show.Ready = 0;
                var u = deal.Progress;
                if (u < CardDeal.FlipEnd)
                    CardDeal.Draw(canvas, Art, CardWidget.Face(Game, card, show), (pose.X, pose.Y, pose.Scale, pose.Angle), u);
                else
                    CardWidget.DrawAt(canvas, Game, card, pose.X, pose.Y, show, pose.Scale, pose.Angle, null, pose.Face);
            }
        }

        private void DrawTargetingHint(Canvas canvas)
        {
            string text;
            if (play.Kind == TargetKind.Enemy)
                text = play.KeyboardAim ? "←/→ cambia de objetivo  ·  Enter para jugar  ·  ESC para cancelar"
                    : play.Sticky ? "Haz clic en un enemigo  ·  Clic derecho para cancelar" : "Suelta sobre un enemigo  ·  Vuelve a la mano para cancelar";
            else if (play.ByKey) text = "Enter para jugar  ·  ESC para cancelar";
            else if (play.Armed) text = !play.Sticky ? "Suelta para jugar la carta" : "Haz clic para jugar la carta";
            else text = "Arrástrala fuera de la mano para jugarla  ·  Clic derecho para cancelar";
            Txt.DrawAt(canvas, Fonts.Get(15), text, Anchor.Center, 640, TopBarH + 16, TargetingColor);
        }

        private void DrawLeaving(Canvas canvas)
        {
            foreach (var l in leaving)
                CardWidget.DrawAt(canvas, Game, l.Card, l.Pose.X, l.Pose.Y, CardShow.With(l.Dmg, l.Blk), l.Pose.Scale, l.Pose.Angle, null, l.Pose.Face);
        }

        private void DrawHeldCard(Canvas canvas)
        {
            var held = HeldIndex(); var pose = HeldPose();
            if (!held.HasValue || pose == null) return;
            var card = state.Hand[held.Value];
            var ready = play.Armed || play.Aiming && play.Target.HasValue;
            var show = HandShow(card, play.Target); show.Unaffordable = false;
            var rect = CardWidget.DrawAt(canvas, Game, card, pose.X, pose.Y, show, pose.Scale, 0f, ready ? Playable : Held, pose.Face);
            if (play.Aiming) Targeting.DrawArrow(canvas, Art, ((int)rect.X + (int)rect.W / 2, rect.Y + 6), ArrowEnd(), play.Target.HasValue, fxTime);
        }

        private (float x, float y) ArrowEnd()
        {
            var target = play.Target;
            if (play.KeyboardAim && target.HasValue && target.Value < enemyRects.Count) { var r = enemyRects[target.Value]; return (r.CenterX, r.CenterY); }
            return play.Pointer;
        }

        private void DrawTargets(Canvas canvas)
        {
            if (!play.Active) return;
            var t = fxTime;
            if (play.Kind == TargetKind.Enemy)
            {
                var target = play.Target;
                if (play.Aiming && target.HasValue && target.Value < enemyRects.Count) Targeting.DrawReticle(canvas, enemyRects[target.Value], Targeting.ReticleEnemy, t);
            }
            else if (play.Armed)
            {
                if (play.Kind == TargetKind.AllEnemies)
                {
                    for (var i = 0; i < Math.Min(EnemyCount, enemyRects.Count); i++) if (E(i).Alive) Targeting.DrawReticle(canvas, enemyRects[i], Targeting.ReticleEnemy, t);
                }
                else if (playerRect.HasValue) Targeting.DrawReticle(canvas, playerRect.Value, Targeting.ReticleSelf, t);
            }
        }

        private void DrawCastStage(Canvas canvas)
        {
            var seq = sequence; if (seq == null) return;
            var u = Math.Min(1f, seq.T / CastIntro); u = 1f - (1f - u) * (1f - u) * (1f - u);
            var start = seq.Start;
            var x = start.X + (CastStage.x - start.X) * u; var y = start.Y + (CastStage.y - start.Y) * u;
            var scale = start.Scale + (0.95f - start.Scale) * u + 0.14f * seq.Pulse;
            CardWidget.DrawAt(canvas, Game, seq.Card, x, y, CardShow.With(seq.Dmg, seq.Blk), scale, start.Angle * (1f - u), null, Math.Max(start.Scale, 0.95f + 0.14f));
        }

        // ------------------------------------------------------------------ tooltips
        private TooltipContent GetTooltip()
        {
            var detail = DetailTooltip();
            if (detail != null) return detail;
            if (hoveredCard.HasValue && hoveredCard.Value < state.Hand.Count)
            {
                var card = state.Hand[hoveredCard.Value];
                var (dmg, blk) = Bonuses(card);
                return Tooltip.Card(Game, card, dmg, blk, Ready(card));
            }
            if (hoveredEnemy.HasValue && hoveredEnemy.Value < EnemyCount)
            {
                var enemy = E(hoveredEnemy.Value);
                return Tooltip.EnemyQuick(Game, enemy, enemy.HitDamage(state.HeroStatuses));
            }
            if (hoveredRelic.HasValue && hoveredRelic.Value < relics.Count) return Tooltip.Relic(Game, relics[hoveredRelic.Value]);
            if (drawInfoRect.Contains(mouse.x, mouse.y))
                return new TooltipContent("Cartas por turno")
                {
                    Lines = new List<string> { "5 cartas base", "+" + (state.CardsPerTurn - 5) + " por reliquias", "El robo se limita al espacio libre en la mano", "y a las cartas disponibles en robo y descarte." },
                };
            if (hoveredPlayer) return Tooltip.PlayerQuick(Game, state, IncomingDamage());
            if (hoveredMana) return Tooltip.Mana(state.Mana, state.ManaMaximum);
            if (hoveredDrawPile) return Tooltip.Pile("ROBO", state.DrawPile.Count, true, state.SpyglassCards());
            if (hoveredDiscPile) return Tooltip.Pile("DESCARTE", state.DiscardPile.Count, false);
            return null;
        }

        private TooltipContent DetailTooltip()
        {
            if (!hoveredDetail.HasValue) return null;
            var d = hoveredDetail.Value;
            if (d.Intent && d.Owner < EnemyCount)
            {
                var enemy = E(d.Owner);
                return enemy.Alive ? Tooltip.Intent(Game, enemy, enemy.HitDamage(state.HeroStatuses), state) : null;
            }
            if (d.Intent) return null;
            if (d.Owner >= 0)
            {
                if (d.Owner >= enemyHitboxes.Count) return null;
                var shown = enemyHitboxes[d.Owner].Statuses;
                return d.J < shown.Count ? Tooltip.Status(Game, shown[d.J].Name, shown[d.J].Stacks) : null;
            }
            return d.J < playerHitboxes.Statuses.Count ? Tooltip.Status(Game, playerHitboxes.Statuses[d.J].Name, playerHitboxes.Statuses[d.J].Stacks, true) : null;
        }

        // ------------------------------------------------------------------ history detail
        private void DrawLogCard(Canvas canvas, CardDefinition card, int x, int y)
        {
            var (w, h) = CardWidget.CardSize(0.7f);
            canvas.Blit(CardWidget.Face(Game, card, default, w, h), x, y);
        }

        private bool DrawLogDetail(Canvas canvas)
        {
            var entry = logStrip.ShownEntry();
            if (entry == null) return false;
            var t = fxTime;
            foreach (var target in entry.Targets)
            {
                if (target.Portrait == CombatLog.Hero) { if (playerRect.HasValue) Targeting.DrawReticle(canvas, playerRect.Value, Targeting.ReticleSelf, t); continue; }
                for (var k = 0; k < EnemyCount; k++)
                    if (E(k).Id == target.EnemyId && E(k).Alive) { if (k < enemyRects.Count) Targeting.DrawReticle(canvas, enemyRects[k], new Rgba(255, 90, 70), t); break; }
            }
            if (entry.Kind == "enemy")
                for (var k = 0; k < EnemyCount; k++)
                    if (E(k).Definition.Name == entry.Source && E(k).Alive) { if (k < enemyRects.Count) Targeting.DrawReticle(canvas, enemyRects[k], Targeting.ReticleEnemy, t); break; }
            logDetail.Layout(Game, entry, CardWidget.CardSize(0.7f));
            var tile = logStrip.TileRect(entry) ?? logStrip.Rect;
            var x = logStrip.Rect.Right + 8;
            var y = Math.Max(TopBarH + 4, Math.Min(Canvas.Height - logDetail.H - 4, tile.CenterY - logDetail.H / 2));
            logDetail.Draw(canvas, Game, x, y, Portrait, DrawLogCard);
            return true;
        }

        // ------------------------------------------------------------------ Mage elements
        private (int? index, CardDefinition card) ConsideredCard()
        {
            var held = HeldIndex();
            var idx = held ?? hoveredCard;
            if (!idx.HasValue || idx.Value < 0 || idx.Value >= state.Hand.Count) return (null, null);
            return (idx, state.Hand[idx.Value]);
        }

        private static bool HasBadges(CardInfo info) => info.Badges != null && info.Badges.Length > 0;

        private void ReactionTargets()
        {
            reactHints.Clear();
            var (_, card) = ConsideredCard();
            if (card == null || !HasBadges(Info(card))) return;
            ElementPreview.ReactingEnemies(Game, state, card, reactDomain);
            foreach (var pair in reactDomain) { var v = ViewOf(pair.Key); if (v.HasValue) reactHints[v.Value] = pair.Value; }
        }

        private void DrawEnemyFloor(Canvas canvas, int i, EnemyState enemy, int ex, int ey)
        {
            var held = enemy.Alive ? ElementPreview.Held(enemy) : null;
            var hasHint = reactHints.TryGetValue(i, out var hint);
            if (!held.HasValue && !hasHint) return;
            enemyAnims.TryGetValue(i, out var anim);
            int fx0, fy0, width;
            if (anim != null && anim.Sheet.IsBoss) { fx0 = BossCx; fy0 = BossGround; width = 220; }
            else { fx0 = ex + EntityWidget.EnemyW / 2; fy0 = ey + EntityWidget.EnemyH + (anim != null ? 6 : 0); width = 150; }
            var g = Game.Data.Glossary;
            var color = hasHint ? ElementPreview.ReactionColor(g, hint) : ElementPreview.Ink(g, held.Value.Item1);
            ElementPreview.DrawElementFloor(canvas, fx0, fy0, color, fxTime, width, hasHint);
        }

        private PlayPreview ElementPreviewFor(int index, int? targetView)
        {
            unchecked
            {
                var h = 17;
                void Mix(int v) => h = h * 31 + v;
                Mix(index); Mix(targetView ?? -1); Mix(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(state.Hand[index]));
                Mix(state.Mana); Mix(state.CardsPlayedThisTurn); Mix(state.Turn); Mix(state.EffectVersion);
                for (var i = 0; i < EnemyCount; i++)
                {
                    var e = E(i); Mix(e.Hp); Mix(e.Block);
                    foreach (var k in e.Statuses.Active) { Mix((int)k); Mix(e.Statuses[k]); }
                }
                if (!previewValid || previewSig != h) { previewSig = h; previewValid = true; previewCached = ElementPreview.Preview(state, index, Domain(targetView)); }
            }
            return previewCached;
        }

        private void DrawElementPreview(Canvas canvas)
        {
            if (!play.Active) return;
            var (index, card) = ConsideredCard();
            if (card == null || !HasBadges(Info(card))) return;
            var targets = new List<int>(); int? chosen;
            if (play.Kind == TargetKind.Enemy && play.Aiming && play.Target.HasValue) { targets.Add(play.Target.Value); chosen = play.Target; }
            else if (play.Kind == TargetKind.AllEnemies && play.Armed) { for (var i = 0; i < EnemyCount; i++) targets.Add(i); chosen = null; }
            else return;
            var preview = ElementPreviewFor(index.Value, chosen);
            if (preview == null) return;
            var g = Game.Data.Glossary;
            var info = Info(card);
            var keyword = preview.ReactionKeyword && info.KeywordText.TryGetValue("reaction", out var kt) ? kt : "";
            foreach (var i in targets)
            {
                if (i >= EnemyCount || i >= enemyRects.Count) continue;
                var di = view[i];
                if (di >= preview.Enemies.Count) continue;
                var ep = preview.Enemies[di];
                var rows = chosen.HasValue ? ElementPreview.Rows(g, ep, keyword) : ElementPreview.Rows(g, ep, compact: true);
                if (rows.Count == 0) continue;
                var rect = enemyRects[i];
                var accent = ep.Reactions.Count > 0 ? ElementPreview.ReactionColor(g, ep.Reactions[0].Kind) : ep.After.HasValue ? ElementPreview.Ink(g, ep.After.Value.element) : new Rgba(200, 190, 220);
                ElementPreview.DrawBubble(canvas, Art, rect.CenterX, rect.Bottom + 46, rows, accent, fxTime, HandAreaY - 4);
            }
        }
    }
}
