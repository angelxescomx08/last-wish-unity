using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LastWish.Domain;

namespace LastWish.Presentation
{
    public sealed partial class CombatScene
    {
        // ------------------------------------------------------------------ hand fan
        private static List<(int x, int y)> CardPositions(int count)
        {
            var list = new List<(int, int)>(count);
            if (count == 0) return list;
            var areaW = CardAreaX1 - CardAreaX0;
            var step = Math.Min(CardWidget.CardW + CardGap, (areaW - CardWidget.CardW) / (double)Math.Max(1, count - 1));
            var totalW = CardWidget.CardW + step * (count - 1);
            var startX = CardAreaX0 + (areaW - totalW) / 2;
            for (var i = 0; i < count; i++) list.Add((MathX.Round(startX + i * step), CardY));
            return list;
        }

        /// <summary>_hand_layout: a gentle fan; the hovered card grows, stands straight and rises; its neighbours slide away.</summary>
        private static Dictionary<int, CardPose> HandLayout(int count, int? hovered, int? held)
        {
            var indices = new List<int>();
            for (var i = 0; i < count; i++) if (i != held) indices.Add(i);
            var slots = CardPositions(indices.Count);
            var mid = (indices.Count - 1) / 2.0;
            int? hoveredK = hovered.HasValue && indices.Contains(hovered.Value) ? indices.IndexOf(hovered.Value) : (int?)null;
            var poses = new Dictionary<int, CardPose>();
            for (var k = 0; k < indices.Count; k++)
            {
                var i = indices[k]; var x = slots[k].x;
                var off = k - mid;
                double cx = x + CardWidget.CardW / 2.0;
                if (hoveredK.HasValue)
                {
                    var d = k - hoveredK.Value;
                    if (d == 0) { poses[i] = new CardPose((float)cx, 718 - CardWidget.CardH * HoverScale / 2, HoverScale, 0f); continue; }
                    if (Math.Abs(d) <= 2) cx += Math.Sign(d) * NeighbourPush / Math.Abs(d);
                }
                var angle = Math.Max(-MaxTilt, Math.Min(MaxTilt, -off * TiltStep));
                poses[i] = new CardPose((float)cx, (float)(HandBaseY + Math.Min(14.0, off * off * 1.2)), 1f, (float)angle);
            }
            return poses;
        }

        private static IRect PoseRect(CardPose pose)
        {
            var (w, h) = CardWidget.CardSize(pose.Scale);
            return new IRect(MathX.Round(pose.X) - w / 2, MathX.Round(pose.Y) - h / 2, w, h);
        }

        private static float? ShrinkFace(CardPose pose) => Math.Max(pose.Scale, pose.Face ?? 0f);

        private static void TweenFace(CardPose pose, float goalScale)
        {
            if (CardWidget.QuantizeScale(pose.Scale) == CardWidget.QuantizeScale(goalScale)) pose.Face = null;
            else pose.Face = Math.Max(goalScale, pose.Face ?? pose.Scale);
        }

        private int? HeldIndex()
        {
            var card = play.Active ? play.Card : null;
            return card.HasValue && card.Value < state.Hand.Count ? card : null;
        }

        private CardPose SpawnPose()
        {
            float x, y;
            if (drawPileRect.HasValue) { x = drawPileRect.Value.CenterX; y = drawPileRect.Value.CenterY; }
            else { x = DrawX + 40; y = DrawY + 40; }
            return new CardPose(x, y, 0.3f, 0f);
        }

        private HashSet<int> WaitingIndices()
        {
            var set = new HashSet<int>();
            for (var i = 0; i < state.Hand.Count; i++) if (deals.TryGetValue(state.Hand[i], out var d) && d.Waiting) set.Add(i);
            return set;
        }

        private HashSet<int> DealingIndices()
        {
            var set = new HashSet<int>();
            for (var i = 0; i < state.Hand.Count; i++) if (deals.ContainsKey(state.Hand[i])) set.Add(i);
            return set;
        }

        private Dictionary<int, CardPose> TargetPoses()
        {
            var held = HeldIndex();
            var hovered = held.HasValue ? null : hoveredCard;
            var waiting = WaitingIndices();
            var order = new List<int>();
            for (var i = 0; i < state.Hand.Count; i++) if (!waiting.Contains(i)) order.Add(i);
            var sub = HandLayout(order.Count, hovered.HasValue && order.Contains(hovered.Value) ? order.IndexOf(hovered.Value) : (int?)null,
                held.HasValue && order.Contains(held.Value) ? order.IndexOf(held.Value) : (int?)null);
            var poses = new Dictionary<int, CardPose>();
            foreach (var pair in sub) poses[order[pair.Key]] = pair.Value;
            foreach (var i in waiting) poses[i] = SpawnPose();
            if (held.HasValue)
            {
                if (play.Aiming || play.ByKey) poses[held.Value] = new CardPose(AimCenter.x, AimCenter.y, AimScale, 0f);
                else poses[held.Value] = new CardPose(play.Pointer.x, play.Pointer.y, 1f, 0f);
            }
            return poses;
        }

        private static Rgba[] TrailOf(CardDefinition card) => card.Chroma != ChromaKind.None ? CardDeal.TrailGold : CardDeal.Trail;

        /// <summary>_fly_card: move a dealt card along its arc; false once it has landed.</summary>
        private bool FlyCard(CardDefinition key, CardPose pose, CardPose goal, Deal deal, float dt)
        {
            deal.Advance(dt);
            if (deal.Waiting)
            {
                pose.X = deal.Start.x; pose.Y = deal.Start.y; pose.Scale = CardDeal.StartScale; pose.Angle = CardDeal.StartAngle; pose.Face = goal.Scale;
                return true;
            }
            if (deal.Done)
            {
                deals.Remove(key);
                pose.X = goal.X; pose.Y = goal.Y; pose.Scale = goal.Scale; pose.Angle = goal.Angle; pose.Face = null;
                bursts.Burst(goal.X, goal.Y - 40, 8, TrailOf(key), (40f, 140f), null, (0.2f, 0.45f), (1.5f, 2.5f), ParticleStyle.Spark, 3.0f, 0f, 30f);
                return false;
            }
            var (x, y, scale, angle) = CardDeal.Pose(deal.Progress, deal.Start, (goal.X, goal.Y, goal.Scale, goal.Angle));
            if (dt > 0)
            {
                float vx = (x - pose.X) / dt, vy = (y - pose.Y) / dt;
                for (var k = 0; k < 2; k++)
                    bursts.Emit(x + rng.UniformF(-14, 14), y + rng.UniformF(-20, 20), -vx * 0.08f + rng.UniformF(-30, 30), -vy * 0.08f + rng.UniformF(-30, 30),
                        rng.UniformF(0.25f, 0.5f), TrailOf(key), rng.UniformF(1.5f, 3.0f), ParticleStyle.Square, 2.5f);
            }
            pose.X = x; pose.Y = y; pose.Scale = scale; pose.Angle = angle; pose.Face = goal.Scale;
            return true;
        }

        private readonly HashSet<CardDefinition> liveKeys = new HashSet<CardDefinition>();
        private readonly List<CardDefinition> staleKeys = new List<CardDefinition>();

        private void AdvanceCards(float dt)
        {
            var poses = TargetPoses();
            var held = HeldIndex();
            var k = 1f - (float)Math.Exp(-Ease * dt);
            var kHeld = 1f - (float)Math.Exp(-EaseHeld * dt);
            var fresh = 0;
            for (var i = 0; i < state.Hand.Count; i++)
            {
                var key = state.Hand[i];
                if (!motion.TryGetValue(key, out var pose))
                {
                    pose = motion[key] = SpawnPose();
                    float delay;
                    if (dealPlan.Count > 0) { delay = dealPlan[0]; dealPlan.RemoveAt(0); } else delay = fresh * CardDeal.DealStagger;
                    fresh++;
                    deals[key] = new Deal(delay, (pose.X, pose.Y));
                }
                var goal = poses[i];
                if (deals.TryGetValue(key, out var deal) && FlyCard(key, pose, goal, deal, dt)) continue;
                var f = i == held ? kHeld : k;
                pose.X += (goal.X - pose.X) * f;
                pose.Y += (goal.Y - pose.Y) * f;
                pose.Scale = goal.Scale;
                pose.Angle += (goal.Angle - pose.Angle) * f;
                TweenFace(pose, goal.Scale);
            }
            liveKeys.Clear(); foreach (var c in state.Hand) liveKeys.Add(c);
            staleKeys.Clear(); foreach (var key in motion.Keys) if (!liveKeys.Contains(key)) staleKeys.Add(key);
            foreach (var key in staleKeys) motion.Remove(key);
            staleKeys.Clear(); foreach (var key in deals.Keys) if (!liveKeys.Contains(key)) staleKeys.Add(key);
            foreach (var key in staleKeys) deals.Remove(key);
            float dx, dy;
            if (discPileRect.HasValue) { dx = discPileRect.Value.CenterX; dy = discPileRect.Value.CenterY; } else { dx = DiscardX + 40; dy = DiscardY + 40; }
            foreach (var ghost in leaving)
            {
                var p = ghost.Pose; ghost.Age += dt;
                p.X += (dx - p.X) * k; p.Y += (dy - p.Y) * k; p.Scale += (0.25f - p.Scale) * k; p.Angle += (0f - p.Angle) * k;
            }
            leaving.RemoveAll(g => g.Age >= LeaveSeconds);
        }

        private CardPose HeldPose()
        {
            var held = HeldIndex();
            if (!held.HasValue) return null;
            return motion.TryGetValue(state.Hand[held.Value], out var p) ? p : TargetPoses()[held.Value];
        }

        // ------------------------------------------------------------------ playing a card
        private int[] HpView() { var a = new int[EnemyCount]; for (var i = 0; i < a.Length; i++) a[i] = E(i).Hp; return a; }
        private int[] ToView(int[] domain, int[] fallback = null)
        {
            if (domain == null) return fallback ?? Array.Empty<int>();
            var a = new int[EnemyCount];
            for (var i = 0; i < a.Length; i++) a[i] = view[i] < domain.Length ? domain[view[i]] : 0;
            return a;
        }

        private void DoPlayCard(int cardIdx, int? targetView)
        {
            var oldHp = HpView();
            var oldBlk = new int[EnemyCount]; for (var i = 0; i < oldBlk.Length; i++) oldBlk[i] = E(i).Block;
            var oldStatus = new List<int[]>(); for (var i = 0; i < EnemyCount; i++) oldStatus.Add(CombatLog.Snapshot(E(i)));
            var oldBlock = state.Block;
            var played = cardIdx >= 0 && cardIdx < state.Hand.Count ? state.Hand[cardIdx] : null;
            var isAttack = played != null && ReadyDamage(played) > 0;
            play.Cancel();
            CardPose playedPose = null;
            if (played != null) motion.TryGetValue(played, out playedPose);
            var bonuses = played != null ? Bonuses(played) : (BigInteger.Zero, 0L);
            var names = new List<string>(); var before = new List<EnemyState>();
            for (var i = 0; i < EnemyCount; i++) { names.Add(E(i).Definition.Name); before.Add(E(i)); }

            var result = state.Play(cardIdx, Domain(targetView));
            if (!result.Success) { ShowError(result.Message); return; }
            feedbackTime = 0f;
            hoveredCard = null;
            Sound.PlayCard();
            if (played != null) { motion.Remove(played); deals.Remove(played); }
            var blockGained = state.Block - oldBlock;
            var action = isAttack ? "attack" : blockGained > 0 ? "guard" : "cast";
            PlanDraws(result, action);
            if (result.Combo && playerRect.HasValue) fx.AddText(playerRect.Value.CenterX, playerRect.Value.Y - 10, "¡COMBO!", new Rgba(90, 235, 180), 1.1f);
            var shownCount = result.Combo ? 1 : 0;
            foreach (var (flag, label, color) in new[] { (result.Singular, "¡SINGULAR!", new Rgba(185, 140, 255)), (result.Void, "¡VACÍO!", new Rgba(90, 170, 255)),
                (result.Spoil, "¡DESPOJO!", new Rgba(240, 150, 60)), (result.Reaction, "¡REACCIÓN!", new Rgba(255, 130, 205)) })
                if (flag && playerRect.HasValue) { fx.AddText(playerRect.Value.CenterX, playerRect.Value.Y - 10 - 24 * shownCount, label, color, 1.1f); shownCount++; }
            AnnounceReactions(isAttack ? ReactionDelay : 0.1f);
            if (played != null)
            {
                var viewResult = ViewResult(result);
                log.Add(CombatLog.HeroEntry(Game, state.Turn, played, viewResult, names, before, blockGained, state.Reactions, oldStatus, targetView, state.Hero.Name));
            }

            if (result.Casts > 1 && played != null)
            {
                var start = playedPose != null ? new CardPose(playedPose.X, playedPose.Y, playedPose.Scale, playedPose.Angle) : new CardPose(640f, 600f, 1f, 0f);
                sequence = new Sequence { Card = played, Dmg = bonuses.Item1, Blk = bonuses.Item2, Result = result, Attack = isAttack, Action = action, Start = start, PrevBlock = oldBlock, PrevHp = (int[])oldHp.Clone() };
                shownEnemies = new List<(int, int)>(); for (var i = 0; i < oldHp.Length; i++) shownEnemies.Add((oldHp[i], oldBlk[i]));
                shownPlayerBlock = oldBlock;
                return;
            }
            if (played != null && playedPose != null)
                leaving.Add(new Leaving { Card = played, Pose = new CardPose(playedPose.X, playedPose.Y, playedPose.Scale, playedPose.Angle, ShrinkFace(playedPose)), Dmg = bonuses.Item1, Blk = bonuses.Item2 });
            PlayHeroAction(action);
            if (blockGained > 0 && playerRect.HasValue) { fx.AddBlockFlash(playerRect.Value, blockGained, heroFx == null); Sound.PlayBlock(); }
            var delay = heroAction == "attack" && heroFx != null ? heroSheets.StrikeSeconds : 0f;
            for (var i = 0; i < Math.Min(oldHp.Length, EnemyCount); i++)
            {
                var dmg = oldHp[i] - E(i).Hp;
                if (dmg > 0 && i < enemyRects.Count) EnemyHit(i, dmg, !E(i).Alive, true, delay);
            }
        }

        /// <summary>The play's per-cast snapshots in display order (the original's list has no dead enemies from earlier turns).</summary>
        private PlayResult ViewResult(PlayResult result)
        {
            var r = new PlayResult { Success = result.Success, Message = result.Message, Combo = result.Combo, Singular = result.Singular, Void = result.Void, Spoil = result.Spoil,
                Reaction = result.Reaction, Fury = result.Fury, Endure = result.Endure, Casts = result.Casts };
            r.CastDrawn.AddRange(result.CastDrawn); r.CastPlayerBlock.AddRange(result.CastPlayerBlock);
            foreach (var a in result.CastHits) r.CastHits.Add(ToView(a));
            foreach (var a in result.CastEnemyHp) r.CastEnemyHp.Add(ToView(a));
            foreach (var a in result.CastEnemyBlock) r.CastEnemyBlock.Add(ToView(a));
            return r;
        }

        // ------------------------------------------------------------------ golden replay
        private void PlanDraws(PlayResult result, string action)
        {
            var plan = new List<float>();
            var drawn = result.CastDrawn.Count > 0 ? result.CastDrawn : new List<int> { 0 };
            for (var k = 0; k < drawn.Count; k++)
            {
                var at = result.Casts > 1 ? CastTime(k, action) + DrawAfterGoldenCast : DrawAfterCast;
                for (var j = 0; j < drawn[k]; j++) plan.Add(at + j * CardDeal.DealStagger);
            }
            dealPlan.Clear(); dealPlan.AddRange(plan);
        }

        private float CastTime(int k, string action = null)
        {
            if (action == null && sequence != null) action = sequence.Action;
            var duration = action != null ? HeroSeconds(action) : 0f;
            return CastIntro + k * Math.Max(CastGap, duration + 0.04f);
        }

        private void AdvanceSequence(float dt)
        {
            var seq = sequence; if (seq == null) return;
            seq.T += dt;
            seq.Pulse = Math.Max(0f, seq.Pulse - dt * 3f);
            var result = seq.Result;
            while (seq.Fired < result.Casts && seq.T >= CastTime(seq.Fired)) { FireCast(seq.Fired); seq.Fired++; }
            var outro = Math.Max(CastOutro, HeroSeconds(seq.Action) + 0.04f);
            if (seq.T >= CastTime(result.Casts - 1) + outro)
            {
                leaving.Add(new Leaving { Card = seq.Card, Pose = new CardPose(CastStage.x, CastStage.y, 0.95f, 0f, 0.95f + 0.14f), Dmg = seq.Dmg, Blk = seq.Blk });
                sequence = null; shownEnemies = null; shownPlayerBlock = null;
            }
        }

        private void FireCast(int k)
        {
            var seq = sequence; var result = seq.Result; var n = result.Casts;
            var (x, y) = CastStage;
            seq.Pulse = 1f;
            bursts.Burst(x, y, 26 + 14 * k, Gold, (120f, 420f), null, (0.4f, 0.9f), (2f, 5f), ParticleStyle.Square, 2.5f, 0f, 40f);
            bursts.Burst(x, y, 16, Gold2, (250f, 520f), null, (0.2f, 0.4f), (1.5f, 2.5f), ParticleStyle.Spark, 3.0f, 0f, 30f);
            bursts.Burst(x, y, 6, Gold3, (10f, 80f), null, (0.4f, 0.7f), (20f, 34f), ParticleStyle.Glow, 3.0f, 0f, 20f);
            fx.AddText(x, y - 120, "Lanzamiento " + (k + 1) + "/" + n, new Rgba(255, 214, 90), 0.9f);
            if (k > 0)
            {
                Sound.PlayCard();
                if (playerRect.HasValue) fx.AddText(playerRect.Value.CenterX, playerRect.Value.Y - 34, "¡x" + (k + 1) + "!", new Rgba(255, 210, 80), 1.0f);
            }
            var block = k < result.CastPlayerBlock.Count ? result.CastPlayerBlock[k] : seq.PrevBlock;
            var gained = block - seq.PrevBlock;
            seq.PrevBlock = block; shownPlayerBlock = block;
            PlayHeroAction(seq.Action);
            if (gained > 0 && playerRect.HasValue) { fx.AddBlockFlash(playerRect.Value, gained); Sound.PlayBlock(); }
            var delay = seq.Attack && heroFx != null ? heroSheets.StrikeSeconds : 0f;
            var hpAfter = k < result.CastEnemyHp.Count ? ToView(result.CastEnemyHp[k]) : Array.Empty<int>();
            var blkAfter = k < result.CastEnemyBlock.Count ? ToView(result.CastEnemyBlock[k]) : Array.Empty<int>();
            if (hpAfter.Length > 0 && blkAfter.Length > 0)
            {
                shownEnemies = new List<(int, int)>();
                for (var i = 0; i < Math.Min(hpAfter.Length, blkAfter.Length); i++) shownEnemies.Add((hpAfter[i], blkAfter[i]));
            }
            var hits = k < result.CastHits.Count ? ToView(result.CastHits[k]) : Array.Empty<int>();
            for (var i = 0; i < hits.Length; i++)
                if (hits[i] > 0 && i < enemyRects.Count)
                {
                    var killed = i < hpAfter.Length && hpAfter[i] <= 0 && 0 < seq.PrevHp[i];
                    EnemyHit(i, hits[i], killed, false, delay);
                }
            if (hpAfter.Length > 0) seq.PrevHp = hpAfter;
        }

        // ------------------------------------------------------------------ hero animation
        private void PlayHeroAction(string name)
        {
            if (heroAction == "death") return;
            if (HeroSeconds(name) > 0)
            {
                heroAction = name; heroActionTime = 0f;
                heroFx?.Play(name);
            }
        }

        private void AdvanceHeroAnimation(float dt)
        {
            if (heroAction == null) { idleTime = (float)MathX.PyMod(idleTime + dt, idleCycle > 0 ? idleCycle : 1.6); return; }
            heroActionTime += dt;
            if (heroAction == "death") return;
            if (heroActionTime >= HeroSeconds(heroAction)) { heroAction = null; heroActionTime = 0f; idleTime = 0f; }
        }

        // ------------------------------------------------------------------ reactions, banners
        private void AnnounceReactions(float delay = 0f)
        {
            var g = Game.Data.Glossary;
            var perEnemy = new Dictionary<string, int>();
            foreach (var ev in state.Reactions)
            {
                var i = ViewOfId(ev.EnemyId);
                if (!i.HasValue || i.Value >= enemyRects.Count) continue;
                perEnemy.TryGetValue(ev.EnemyId, out var n); perEnemy[ev.EnemyId] = n + 1;
                var rect = enemyRects[i.Value];
                var at = delay + 0.55f * n;
                reactionShow.Add((rect.CenterX, rect.CenterY - 10), ev.Kind, ev.First, ev.Second, CombatLog.ReactionSummary(ev.Kind, ev.Stacks), at);
                reactionsExplained.TryGetValue(ev.Kind, out var seen);
                if (seen < 2)
                {
                    reactionsExplained[ev.Kind] = seen + 1;
                    var key = ElementPreview.ReactionKey(ev.Kind);
                    var text = g.ReactionText.TryGetValue(key, out var tx) ? tx : "";
                    var cut = text.IndexOf(": ", StringComparison.Ordinal);
                    var head = cut >= 0 ? text.Substring(0, cut) : text; var rest = cut >= 0 ? text.Substring(cut + 2) : text;
                    if (rest.Length > 0) rest = char.ToUpperInvariant(rest[0]) + rest.Substring(1);
                    banners.Add(ElementPreview.ReactionIcon(g, ev.Kind), CombatLog.ReactionName(g, ev.Kind) + ": " + head, rest, at + ReactionShow.Clash);
                }
                if (!E(i.Value).Alive) holdTime = Math.Max(holdTime, at + ReactionShow.Clash + 0.7f);
            }
        }

        private void AnnounceActions(IReadOnlyList<EnemyAction> actions, Dictionary<int, EnemyIntent> intents, Dictionary<int, string> names, Func<int, int?> toView)
        {
            banners.Clear();
            var k = 0;
            foreach (var action in actions)
            {
                var v = toView(action.Index);
                if (!v.HasValue || !intents.ContainsKey(v.Value)) continue;
                var (icon, title, detail) = ActionBanners.Describe(Game.Data.Glossary, names.TryGetValue(v.Value, out var nm) ? nm : "", intents[v.Value], action);
                banners.Add(icon, title, detail, 0.12f * k); k++;
            }
        }

        private void AnnounceExtras(EnemyAction action, float at)
        {
            if (!playerRect.HasValue) return;
            var g = Game.Data.Glossary;
            float x = playerRect.Value.CenterX + 60, y = playerRect.Value.Y + 70;
            for (var j = 0; j < action.Debuffs.Count; j++)
            {
                var d = action.Debuffs[j];
                fx.AddText(x, y - 18 * j, (g.StatusName.TryGetValue(d.Kind, out var nm) ? nm : d.Kind.ToString()) + " " + d.Amount, DebuffText, 1.3f, at + 0.1f * j);
            }
            for (var j = 0; j < action.Cards.Count; j++)
            {
                var c = action.Cards[j];
                fx.AddText(x, y - 18 * (action.Debuffs.Count + j), "+" + c.Count + " " + (CardNamesEs.TryGetValue(c.Id, out var cn) ? cn : c.Id), Toxic, 1.4f, at + 0.15f);
            }
        }

        private void EnemyHit(int i, int dmg, bool killed, bool hold = true, float delay = 0f)
        {
            var rect = enemyRects[i];
            enemyAnims.TryGetValue(i, out var anim);
            fx.AddHitFlash(rect, dmg, 0, delay, anim == null);
            Sound.PlayAttack();
            if (killed)
            {
                if (anim != null) anim.Play("death", delay); else fx.AddDeathFlash(rect, delay);
                Sound.PlayDeath();
                if (hold) holdTime = KillHold + delay;
            }
            else anim?.Play("hurt", delay);
        }

        /// <summary>_sync_enemy_anims: display index → animator; animators of enemies that left the list finish in their old slot.</summary>
        private void SyncEnemyAnims(List<EnemyState> before)
        {
            var current = new HashSet<string>(); for (var i = 0; i < EnemyCount; i++) current.Add(E(i).Id);
            if (before != null)
                foreach (var enemy in before)
                {
                    if (current.Contains(enemy.Id) || !animById.TryGetValue(enemy.Id, out var anim)) continue;
                    if (anim.Busy) gone.Add((anim, slotOf.TryGetValue(enemy.Id, out var s) ? s : 0));
                    else animById.Remove(enemy.Id);
                }
            enemyAnims.Clear();
            for (var i = 0; i < EnemyCount; i++) if (animById.TryGetValue(E(i).Id, out var a)) enemyAnims[i] = a;
        }

        private void AnnounceVengeance()
        {
            for (var i = 0; i < EnemyCount; i++)
            {
                var enemy = E(i);
                if (!enemy.Alive || enemy.Intent.Id != "vengeance" || avenging.Contains(enemy.Id) || i >= enemyRects.Count) continue;
                avenging.Add(enemy.Id);
                var rect = enemyRects[i];
                fx.AddText(rect.CenterX, rect.Y - 46, "¡VENGANZA!", VengeanceText, 1.6f, VengeanceDelay);
                if (enemyAnims.TryGetValue(i, out var anim)) anim.Play("cast", VengeanceDelay);
            }
        }

        private void AnnouncePair()
        {
            for (var i = 0; i < EnemyCount; i++)
            {
                var e = E(i);
                if (e.Definition.Elite && Game.Data.Enemies.TryGetValue(e.Definition.Ai, out var d))
                    banners.Add("buff", "¡Élite! " + d.Name, d.Identity + ": " + d.Title, 0.4f);
            }
            var names = new HashSet<string>(); for (var i = 0; i < EnemyCount; i++) if (!string.IsNullOrEmpty(E(i).PairName)) names.Add(E(i).PairName);
            foreach (var pair in Game.Data.Pairs.Values)
                if (names.Contains(pair.name)) banners.Add("buff", "¡Pareja! " + pair.name, pair.synergy, 0.4f);
        }

        // ------------------------------------------------------------------ history of the enemy turn
        private void LogEnemyTurn(int turn, Dictionary<int, EnemyIntent> intents, Dictionary<int, string> names, HashSet<int> frozen, List<EnemyState> before, Func<int, int?> toView)
        {
            var g = Game.Data.Glossary; var hero = state.Hero.Name;
            string Nm(StatusKind k) => g.StatusName.TryGetValue(k, out var n) ? n : k.ToString();
            foreach (var action in state.EnemyLog)
            {
                var v = toView(action.Index);
                if (!v.HasValue || !intents.TryGetValue(v.Value, out var intent)) continue;
                var name = names.TryGetValue(v.Value, out var nm) ? nm : "";
                var (icon, title, detail) = ActionBanners.Describe(g, name, intent, action);
                var actor = v.Value < before.Count ? before[v.Value] : null;
                var targets = new List<LogTarget>();
                var lost = action.Hits.Sum();
                if (action.Hits.Count > 0 || action.Debuffs.Count > 0 || action.Cards.Count > 0)
                    targets.Add(new LogTarget { Name = hero, Portrait = CombatLog.Hero, Hp = lost, Statuses = action.Debuffs.Select(d => (Nm(d.Kind), d.Amount)).ToArray(), Blocked = action.Hits.Count > 0 && lost == 0 });
                var allyBuffs = intent.AllyBuffs ?? Array.Empty<StatusAmount>();
                if (intent.AllyBlock > 0 || allyBuffs.Length > 0)
                    foreach (var ally in before)
                        if (ally != actor && ally.Hp > 0)
                            targets.Add(new LogTarget { Name = ally.Definition.Name, Portrait = ally.Definition.Name, Block = intent.AllyBlock, Statuses = allyBuffs.Select(b => (Nm(b.Kind), b.Amount)).ToArray(), EnemyId = ally.Id });
                var ownBlock = intent.Block + (intent.Kind == IntentKind.Block ? intent.Value : 0);
                var buffs = intent.Buffs ?? Array.Empty<StatusAmount>();
                if (actor != null && (ownBlock > 0 || buffs.Length > 0 || action.Healed != 0 || action.Exploded) && !targets.Any(t => t.EnemyId == actor.Id))
                    targets.Add(new LogTarget { Name = name, Portrait = name, Block = ownBlock, Healed = action.Healed, Statuses = buffs.Select(b => (Nm(b.Kind), b.Amount)).ToArray(), Died = action.Exploded, EnemyId = actor.Id });
                log.Enemy(turn, icon, title, detail, lost, name, targets.ToArray());
            }
            var frozenSorted = frozen.ToList(); frozenSorted.Sort();
            foreach (var i in frozenSorted)
            {
                var e = i < before.Count ? before[i] : null;
                log.Effect(turn, "frozen", (names.TryGetValue(i, out var nm) ? nm : "") + " está Congelado", new[] { "Pierde su turno." },
                    e != null ? new[] { new LogTarget { Name = e.Definition.Name, Portrait = e.Definition.Name, EnemyId = e.Id } } : null);
            }
            foreach (var ev in state.Reactions)
            {
                EnemyState e = null; foreach (var x in state.Enemies) if (x.Id == ev.EnemyId) { e = x; break; }
                var rn = CombatLog.ReactionName(g, ev.Kind);
                log.Effect(state.Turn, ElementPreview.ReactionIcon(g, ev.Kind), "¡" + rn + "!", new[] { "En " + (e != null ? e.Definition.Name : "") + ": " + CombatLog.ReactionSummary(ev.Kind, ev.Stacks) + "." },
                    e != null ? new[] { new LogTarget { Name = e.Definition.Name, Portrait = e.Definition.Name, Reactions = new[] { rn }, EnemyId = e.Id, Died = !e.Alive } } : null);
            }
            log.Turn(state.Turn);
        }

        // ------------------------------------------------------------------ end of turn
        private void DoEndTurn()
        {
            var oldHp = state.Hp;
            var before = new List<EnemyState>(); for (var i = 0; i < EnemyCount; i++) before.Add(E(i));
            var oldEnemyHps = before.Select(e => e.Hp).ToArray();
            var intents = new Dictionary<int, EnemyIntent>(); var names = new Dictionary<int, string>();
            for (var i = 0; i < before.Count; i++) { if (before[i].Alive) intents[i] = before[i].Intent; names[i] = before[i].Definition.Name; }
            var domainToView = new Dictionary<int, int>(); for (var i = 0; i < view.Count; i++) domainToView[view[i]] = i;
            int? ToBefore(int domainIndex) => domainToView.TryGetValue(domainIndex, out var v) ? v : (int?)null;

            play.Cancel();
            Sound.PlayEndTurn();
            enemyPhase = EnemyPhaseTime;
            foreach (var card in state.Hand)
                if (motion.TryGetValue(card, out var pose) && !deals.ContainsKey(card))
                {
                    var (d, b) = Bonuses(card);
                    leaving.Add(new Leaving { Card = card, Pose = new CardPose(pose.X, pose.Y, pose.Scale, pose.Angle, ShrinkFace(pose)), Dmg = d, Blk = b });
                }
            var frozen = new HashSet<int>();
            for (var i = 0; i < before.Count; i++)
            {
                var e = before[i];
                if (e.Alive && e.Statuses[StatusKind.Frozen] > 0 && !(e.Definition.Boss || e.Definition.Elite)) frozen.Add(i);
            }
            state.EndTurn();
            // the original drops the dead from its enemy list after the enemies act
            view.Clear(); for (var i = 0; i < state.Enemies.Count; i++) if (state.Enemies[i].Alive) view.Add(i);
            var g = Game.Data.Glossary;
            foreach (var i in frozen)
                if (i < enemyRects.Count)
                {
                    var rect = enemyRects[i];
                    fx.AddText(rect.CenterX, rect.CenterY, "¡Congelado!", g.IceInk, 1.4f, 0.3f, "frozen");
                }
            AnnounceReactions(1.4f);
            LogEnemyTurn(state.Turn - 1, intents, names, frozen, before, ToBefore);
            motion.Clear(); deals.Clear();
            dealPlan.Clear(); for (var j = 0; j < state.Hand.Count; j++) dealPlan.Add(DrawAfterEnemyTurn + j * CardDeal.DealStagger);
            AnnounceActions(state.EnemyLog, intents, names, ToBefore);

            // animated enemies act out their move; each hit lands on its strike
            float? strike = null;
            var hitTimes = new List<(float t, int lost)>();
            var k = 0;
            var animsBefore = new Dictionary<int, EnemyAnimator>(enemyAnims);
            foreach (var action in state.EnemyLog)
            {
                var vi = ToBefore(action.Index);
                if (!vi.HasValue || !animsBefore.TryGetValue(vi.Value, out var anim) || !intents.ContainsKey(vi.Value)) continue;
                var delay = 0.14f * k; k++;
                var fallback = intents[vi.Value].Kind == IntentKind.Attack ? "attack" : "cast";
                var name = anim.Sheet.AnimationForMove(action.MoveId, fallback);
                if (action.Exploded && Array.IndexOf(anim.Sheet.Terminal, name) < 0) name = "death";
                var hits = Math.Max(1, action.Hits.Count);
                anim.Play(name, delay, hits);
                var times = anim.StrikeTimes(name, hits).Select(t => delay + t).ToArray();
                if (action.Hits.Count > 0 && times.Length > 0)
                {
                    strike = strike.HasValue ? Math.Min(strike.Value, times[0]) : times[0];
                    for (var j = 0; j < action.Hits.Count; j++) hitTimes.Add((times[Math.Min(j, times.Length - 1)], action.Hits[j]));
                }
                AnnounceExtras(action, times.Length > 0 ? times[times.Length - 1] : delay + 0.5f);
            }
            foreach (var action in state.EnemyLog)
            {
                var vi = ToBefore(action.Index);
                if (!vi.HasValue || !animsBefore.ContainsKey(vi.Value)) AnnounceExtras(action, 0.2f);
            }
            var exploded = new HashSet<int>();
            foreach (var a in state.EnemyLog) if (a.Exploded) { var vi = ToBefore(a.Index); if (vi.HasValue) exploded.Add(vi.Value); }
            // _enemy_hit still works on the old indices here: rects and animators are re-keyed by SyncEnemyAnims below
            for (var i = 0; i < before.Count; i++)
            {
                if (i >= enemyRects.Count) continue;
                var old = oldEnemyHps[i]; var enemy = before[i];
                if (old - enemy.Hp > 0 && !exploded.Contains(i)) EnemyHit(i, old - enemy.Hp, !enemy.Alive);
                else if (enemy.Hp > old) { var rect = enemyRects[i]; fx.AddText(rect.CenterX, rect.Y + 20, "+" + (enemy.Hp - old), HealText, 1.2f, 0.55f); }
            }
            foreach (var i in exploded)
                if (i < enemyRects.Count) { Sound.PlayDeath(); holdTime = Math.Max(holdTime, 1.2f); }
            SyncEnemyAnims(before);

            var dmg = oldHp - state.Hp;
            var wait = strike ?? 0f;
            var reaction = state.Hp > 0 ? "hurt" : "death";
            if (dmg > 0)
            {
                if (wait > 0) { pendingHeroHurt = wait; pendingHeroName = reaction; }
                else PlayHeroAction(reaction);
            }
            if (dmg > 0 && playerRect.HasValue)
            {
                var pr = playerRect.Value;
                var shown = hitTimes.Where(h => h.lost > 0).ToList();
                var shownSum = shown.Sum(h => h.lost);
                if (shown.Count > 1 || shown.Count > 0 && shownSum < dmg)
                {
                    var spread = shown.Count;
                    for (var j = 0; j < shown.Count; j++) fx.AddHitFlash(pr, shown[j].lost, (j - (spread - 1) / 2f) * 18, shown[j].t, false);
                    var rest = dmg - shownSum;
                    if (rest > 0)
                    {
                        var last = shown.Count > 0 ? shown.Max(h => h.t) : 0f;
                        fx.AddHitFlash(pr, rest, 0, last + 0.25f, false);
                        fx.AddText(pr.CenterX + 60, pr.Y + 50, "Veneno", Toxic, 1f, last + 0.25f);
                    }
                }
                else fx.AddHitFlash(pr, dmg, 0, wait, heroFx == null);
                Sound.PlayHit();
            }
        }
    }
}
