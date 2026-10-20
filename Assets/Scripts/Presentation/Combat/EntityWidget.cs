using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>One status badge on the battlefield (hover → its rule).</summary>
    public readonly struct StatusBadge
    {
        public readonly string Name; public readonly int Stacks; public readonly IRect Rect;
        public StatusBadge(string name, int stacks, IRect rect) { Name = name; Stacks = stacks; Rect = rect; }
    }

    /// <summary>The <c>hitboxes</c> dict of entity_widget.py: intent rect, element badge and status badges.</summary>
    public sealed class EntityHitboxes
    {
        public IRect? Intent, Element;
        public readonly List<StatusBadge> Statuses = new List<StatusBadge>(8);
        public void Clear() { Intent = null; Element = null; Statuses.Clear(); }
    }

    /// <summary>ui/entity_widget.py: enemies and the hero on the battlefield (HP bar, block, statuses, intents).</summary>
    public static class EntityWidget
    {
        private const int Corner = 6, HpH = 13;
        public const int StatusBadgeSize = 28, StatusStep = 31;
        public const int EnemyW = 128, EnemyH = 150, PlayerW = 155, PlayerH = 195, ElementBadgeR = 17;
        private static readonly Rgba OutlineInk = new Rgba(12, 8, 14);

        // ------------------------------------------------------------------ shared helpers
        private static RectF Outlined(Canvas canvas, FontFace font, string text, Anchor anchor, float x, float y, Rgba color, Rgba? outline = null)
            => Txt.OutlinedAt(canvas, font, text, anchor, x, y, color, outline ?? OutlineInk);

        private static void HpBar(Canvas canvas, Art art, IRect rect, int current, int maximum, int block = 0, int previewLoss = 0, float t = 0f)
        {
            var ratio = maximum > 0 ? Math.Max(0.0, Math.Min(1.0, current / (double)maximum)) : 0.0;
            var fillCol = ratio < 0.35 ? Colors.HpLow : Colors.HpFill;
            canvas.Rect(rect, Colors.HpEmpty, 0, 3);
            if (ratio > 0)
            {
                var fill = new IRect(rect.X, rect.Y, (int)(rect.W * ratio), rect.H);
                canvas.Rect(fill, fillCol, 0, 3);
                var loss = Math.Min(current, Math.Max(0, previewLoss));
                if (loss > 0 && maximum > 0)
                {
                    var after = (int)(rect.W * (long)(current - loss) / (double)maximum);
                    var seg = new IRect(rect.X + after, rect.Y, Math.Max(2, fill.W - after), rect.H);
                    var k = 0.5 + 0.5 * Math.Sin(t * 6.0);
                    canvas.Fill(seg, new Rgba((int)(160 + 90 * k), (int)(36 + 50 * k), (int)(40 + 40 * k)));
                    canvas.PushClip(seg);
                    var hatch = new Rgba(255, 214, 200, 150);
                    for (var sx = -seg.H; sx < seg.W; sx += 5)
                        canvas.Line(seg.X + sx, seg.Y + seg.H, seg.X + sx + seg.H, seg.Y, hatch);
                    canvas.PopClip();
                }
            }
            canvas.Rect(rect, block > 0 ? Colors.BlockColor : Colors.BorderDim, block > 0 ? 2 : 1, 3);
            Outlined(canvas, art.Fonts.Get(11), current + "/" + maximum, Anchor.Center, rect.CenterX, rect.CenterY, Colors.HpText);
        }

        private static IRect? BlockBadge(Canvas canvas, Art art, int cx, int cy, int block)
        {
            if (block <= 0) return null;
            var font = art.Fonts.Get(13);
            if (!art.HasIcon("block"))
            {
                const int radius = 14;
                canvas.Circle(cx, cy, radius, Colors.BlockBg);
                canvas.Circle(cx, cy, radius, Colors.BlockColor, 2);
                Txt.DrawAt(canvas, font, block.ToString(), Anchor.Center, cx, cy, Colors.BlockColor);
                return new IRect(cx - radius, cy - radius, radius * 2, radius * 2);
            }
            var icon = art.Icon("block");
            var rect = IRect.AtCenter(cx, cy, icon.W * 2, icon.H * 2);
            canvas.Blit(icon, rect.X, rect.Y, 2);
            Outlined(canvas, font, block.ToString(), Anchor.Center, cx, cy - 1, new Rgba(240, 248, 255), new Rgba(10, 26, 60));
            return rect;
        }

        public static IRect StatusRect(int index, int startX, int y, int maxW = 10_000)
        {
            var perRow = Math.Max(1, maxW / StatusStep);
            return new IRect(startX + (index % perRow) * StatusStep, y + (index / perRow) * StatusStep, StatusBadgeSize, StatusBadgeSize);
        }

        private static void StatusEffects(Canvas canvas, Art art, Glossary g, StatusSet set, StatusKind? skip, int startX, int y, int maxW, List<StatusBadge> output)
        {
            var font = art.Fonts.Get(13); var i = 0;
            foreach (var kind in set.Active)
            {
                if (skip.HasValue && kind == skip.Value) continue;
                var name = g.StatusName.TryGetValue(kind, out var n) ? n : kind.ToString();
                var stacks = set[kind]; var buff = g.IsBuffStatus(name);
                var badge = StatusRect(i++, startX, y, maxW);
                var iconName = g.StatusIconOf(name, buff);
                if (art.HasIcon(iconName)) canvas.Blit(art.Icon(iconName), badge.X, badge.Y, 2);
                else canvas.Rect(badge.Inflate(-6, -6), buff ? Colors.BuffColor : Colors.DebuffColor, 0, 3);
                var ink = buff ? new Rgba(160, 245, 170) : new Rgba(255, 240, 236);
                Outlined(canvas, font, stacks.ToString(), Anchor.BottomRight, badge.Right + 3, badge.Bottom + 3, ink);
                output?.Add(new StatusBadge(name, stacks, badge));
            }
        }

        // ------------------------------------------------------------------ intent
        private static readonly Dictionary<IntentKind, string> IntentShort = new Dictionary<IntentKind, string>
        { [IntentKind.Attack] = "ATQ", [IntentKind.Block] = "BLQ", [IntentKind.Buff] = "BUFF", [IntentKind.Debuff] = "DEB", [IntentKind.Unknown] = "???",
          [IntentKind.Summon] = "INV", [IntentKind.Stun] = "ATUR" };
        private static readonly Dictionary<IntentKind, Rgba> IntentColor = new Dictionary<IntentKind, Rgba>
        { [IntentKind.Attack] = Colors.IntentAttack, [IntentKind.Block] = Colors.IntentBlock, [IntentKind.Buff] = Colors.IntentBuff, [IntentKind.Debuff] = Colors.IntentDebuff, [IntentKind.Unknown] = Colors.IntentUnknown,
          [IntentKind.Summon] = Colors.IntentBuff, [IntentKind.Stun] = Colors.IntentUnknown };

        /// <summary>Total minions or fragments an intent brings.</summary>
        public static int SummonCount(EnemyIntent it) { var n = 0; foreach (var s in it.Extras.Summons) n += s.Count; return n; }

        private static bool Any<T>(T[] a) => a != null && a.Length > 0;

        public static string IntentLabel(EnemyIntent it, int? damage = null)
        {
            var label = IntentShort[it.Kind];
            var value = damage.HasValue && it.Kind == IntentKind.Attack ? damage.Value : it.Value;
            if (value > 0) label += " " + value;
            if (it.Hits > 1) label += "x" + it.Hits;
            var extras = new List<string>();
            if (it.Block > 0) extras.Add("BLQ");
            if (Any(it.Debuffs) && it.Kind != IntentKind.Debuff) extras.Add("DEB");
            if (Any(it.Cards)) extras.Add("CARTAS");
            if (Any(it.Buffs) && it.Kind != IntentKind.Buff) extras.Add("BUFF");
            foreach (var e in extras) label += " +" + e;
            return label;
        }

        public static string IntentNumber(EnemyIntent it, int? damage = null)
        {
            if (it.Kind == IntentKind.Attack) { var v = damage ?? it.Value; return it.Hits > 1 ? v + "×" + it.Hits : v.ToString(); }
            if (it.Kind == IntentKind.Block && it.Value > 0) return it.Value.ToString();
            if (it.Kind == IntentKind.Summon && SummonCount(it) > 1) return "×" + SummonCount(it);
            return "";
        }

        private static readonly List<string> extrasBuffer = new List<string>(8);
        public static List<string> IntentExtras(EnemyIntent it, List<string> output = null)
        {
            var extras = output ?? new List<string>(); extras.Clear();
            if (it.Block > 0) extras.Add("block");
            if (Any(it.Buffs) && it.Kind != IntentKind.Buff) extras.Add("status_buff");
            if (Any(it.Debuffs) && it.Kind != IntentKind.Debuff) extras.Add("status_debuff");
            if (Any(it.Cards)) extras.Add("junk_card");
            if (it.AllyBlock > 0 && !extras.Contains("block")) extras.Add("block");
            if (Any(it.AllyBuffs) && !extras.Contains("status_buff") && it.Kind != IntentKind.Buff) extras.Add("status_buff");
            if (it.Lifesteal || it.HealAllies > 0) extras.Add("heal");
            if (it.SelfDestruct) extras.Add("fuse");
            var x = it.Extras;
            if (x.StealStrength > 0) extras.Add("strength");
            if (x.SiphonBlockPercent > 0 && !extras.Contains("block")) extras.Add("block");
            if (x.Swallow > 0) extras.Add("cards");
            if (x.Cleanse && !extras.Contains("status_buff")) extras.Add("status_buff");
            if (x.ConsumesFocus) extras.Add("hourglass");
            return extras;
        }

        /// <summary>Radial glow of the lethal intent: concentric SRCALPHA circles (each overwrites), alpha ∝ (70 + 80k).</summary>
        private static Sprite LethalGlow(Art art, int size)
        {
            return new Sprite(art.Procedural("intent_lethal_glow_" + size, size, size, () =>
            {
                var px = new Rgba[size * size]; var half = size / 2;
                for (var r = half; r > 4; r -= 3)
                {
                    var a = (int)Math.Min(255, 255 * Math.Pow(1 - r / (size / 2.0), 1.2));
                    for (var y = 0; y < size; y++)
                        for (var x = 0; x < size; x++)
                        {
                            double dx = x + 0.5 - half, dy = y + 0.5 - half;
                            if (dx * dx + dy * dy <= r * r) px[y * size + x] = new Rgba(255, 255, 255, a);
                        }
                }
                return px;
            }));
        }

        /// <summary>draw_intent: icon + number + extras centred on <paramref name="cx"/>, bottom at <paramref name="bottom"/>.</summary>
        public static IRect DrawIntent(Canvas canvas, Art art, EnemyIntent it, int cx, int bottom, int? damage = null, bool lethal = false, float t = 0f)
        {
            var mainName = Tooltip.IntentIcon(it, damage);
            if (!art.HasIcon(mainName)) return IntentBubble(canvas, art, it, cx, bottom - 11, damage);
            var main = art.Icon(mainName); int mainW = main.W * 2, mainH = main.H * 2;
            var number = IntentNumber(it, damage);
            var attack = it.Kind == IntentKind.Attack;
            var ink = lethal ? new Rgba(255, 96, 84) : attack ? new Rgba(255, 240, 226) : new Rgba(214, 236, 255);
            var numFont = art.Fonts.Get(21);
            var numW = number.Length > 0 ? Txt.OutlinedSize(numFont, number).w : 0;
            var extras = IntentExtras(it, extrasBuffer);
            var width = mainW + numW;
            foreach (var e in extras) if (art.HasIcon(e)) width += art.Icon(e).W * 2;
            var height = mainH;
            var bob = MathX.Round(Math.Sin(t * 2.4) * 2);
            var rect = new IRect(cx - width / 2, bottom - height, width, height);

            var shade = IRect.AtCenter(rect.CenterX, rect.Bottom - 7, width + 18, 20);
            canvas.Ellipse(shade, new Rgba(6, 4, 10, 130));
            if (lethal)
            {
                var k = 0.5 + 0.5 * Math.Sin(t * 7.0);
                var size = mainW + 34;
                var glow = LethalGlow(art, size);
                var g = IRect.AtCenter(rect.X + mainW / 2, rect.CenterY + bob, size, size);
                canvas.Blit(glow, g.X, g.Y, 1f, new Rgba(255, 40, 30, (int)Math.Min(255, 70 + 80 * k)));
            }
            var x = rect.X;
            canvas.Blit(main, x, rect.Y + bob, 2);
            x += mainW;
            if (number.Length > 0) { Outlined(canvas, numFont, number, Anchor.MidLeft, x - 3, rect.Bottom - 11, ink); x += numW; }
            foreach (var e in extras)
            {
                if (!art.HasIcon(e)) continue;
                var s = art.Icon(e);
                canvas.Blit(s, x, rect.Bottom - s.H * 2, 2);
                x += s.W * 2;
            }
            if (lethal && art.HasIcon("lethal"))
            {
                var skull = art.Icon("lethal");
                var r = IRect.AtCenter(rect.X + 2, rect.Y + 3 + bob, skull.W, skull.H);
                canvas.Blit(skull, r.X, r.Y);
            }
            return rect;
        }

        private static IRect IntentBubble(Canvas canvas, Art art, EnemyIntent it, int cx, int cy, int? damage)
        {
            var col = IntentColor[it.Kind]; var label = IntentLabel(it, damage);
            var font = art.Fonts.Get(10);
            int w = Math.Max(56, font.Width(label) + 16), h = 22;
            var rect = new IRect(cx - w / 2, cy - h / 2, w, h);
            canvas.Rect(rect, Colors.BgPanel, 0, 4);
            canvas.Rect(rect, col, 1, 4);
            Txt.DrawAt(canvas, font, label, Anchor.Center, rect.CenterX, rect.CenterY, col);
            return rect;
        }

        // ------------------------------------------------------------------ enemy
        /// <summary>The element status an enemy holds (first in status order), or null.</summary>
        public static StatusKind? ElementOf(EnemyState enemy)
        {
            foreach (var k in enemy.Statuses.Active)
                if (k == StatusKind.Fire || k == StatusKind.Water || k == StatusKind.Earth || k == StatusKind.Air) return k;
            return null;
        }

        public static IRect DrawEnemy(Canvas canvas, Art art, Glossary g, EnemyState enemy, int x, int y, bool targeted = false, bool highlighted = false,
            Sprite? sprite = null, bool framed = true, (int w, int h)? size = null, int? intentDamage = null, bool lethal = false, float t = 0f, EntityHitboxes hitboxes = null,
            (int hp, int block)? shown = null)
        {
            var (w, h) = size ?? (EnemyW, EnemyH);
            var rect = new IRect(x, y, w, h);
            // shown: HP / block drawn during a golden replay (the original swaps them in for the draw)
            var hpNow = shown?.hp ?? enemy.Hp; var blockNow = shown?.block ?? enemy.Block;
            if (hpNow <= 0)
            {
                canvas.Rect(rect, new Rgba(30, 25, 25), 0, Corner);
                canvas.Rect(rect, new Rgba(60, 50, 50), 1, Corner);
                Txt.DrawAt(canvas, art.Fonts.Get(13), "DERROTADO", Anchor.Center, rect.CenterX, rect.CenterY, new Rgba(100, 80, 80));
                return rect;
            }
            var nameRect = Outlined(canvas, art.Fonts.Get(12), enemy.Definition.Name, Anchor.MidBottom, rect.CenterX, y - 2, Colors.TextAccent);
            var intentRect = DrawIntent(canvas, art, enemy.Intent, rect.CenterX, (int)nameRect.Y - 1, intentDamage, lethal, t);

            Rgba body, border; int bw;
            if (highlighted) { body = new Rgba(160, 70, 40); border = new Rgba(255, 160, 50); bw = 2; }
            else if (targeted) { body = Colors.EnemyAccent; border = new Rgba(255, 220, 60); bw = 2; }
            else { body = Colors.EnemyBody; border = Colors.EnemyAccent; bw = 1; }
            if (framed) canvas.Rect(rect, body, 0, Corner);
            if (sprite.HasValue && sprite.Value.Valid)
            {
                var s = sprite.Value; var sr = IRect.AtCenter(rect.CenterX, rect.CenterY, s.W, s.H);
                canvas.Blit(s, sr.X, sr.Y);
            }
            if (framed) canvas.Rect(rect, border, bw, Corner);
            if (highlighted)
            {
                const int sz = 8;
                foreach (var (cx, cy, dx, dy) in new[] { (rect.X, rect.Y, 1, 1), (rect.Right, rect.Y, -1, 1), (rect.X, rect.Bottom, 1, -1), (rect.Right, rect.Bottom, -1, -1) })
                {
                    canvas.Line(cx, cy, cx + dx * sz, cy, border, 2);
                    canvas.Line(cx, cy, cx, cy + dy * sz, border, 2);
                }
            }
            var hp = new IRect(x, rect.Bottom + 4, w, HpH);
            HpBar(canvas, art, hp, hpNow, enemy.MaxHp, blockNow);
            BlockBadge(canvas, art, hp.X, hp.CenterY, blockNow);

            var element = ElementOf(enemy);
            hitboxes?.Clear();
            if (element.HasValue)
            {
                var name = g.StatusName[element.Value]; var stacks = enemy.Statuses[element.Value];
                var badge = DrawElementBadge(canvas, art, g, name, stacks, hp.Right + 4, hp.CenterY, t);
                if (hitboxes != null) { hitboxes.Statuses.Add(new StatusBadge(name, stacks, badge)); hitboxes.Element = badge; }
            }
            StatusEffects(canvas, art, g, enemy.Statuses, element, x, rect.Bottom + 21, Math.Max(w, 124), hitboxes?.Statuses);
            if (hitboxes != null) hitboxes.Intent = intentRect;
            return rect;
        }

        private static Sprite ElementGlow(Art art)
        {
            const int r = ElementBadgeR, size = r * 4;
            return new Sprite(art.Procedural("element_badge_glow", size, size, () =>
            {
                var px = new Rgba[size * size];
                for (var k = 6; k > 0; k--)
                {
                    var rad = r + k * 2; var a = (int)(255 * k / 6.0);
                    for (var y = 0; y < size; y++)
                        for (var x = 0; x < size; x++)
                        {
                            double dx = x + 0.5 - r * 2, dy = y + 0.5 - r * 2;
                            if (dx * dx + dy * dy <= rad * rad) px[y * size + x] = new Rgba(255, 255, 255, a);
                        }
                }
                return px;
            }));
        }

        /// <summary>draw_element_badge: dark disc, pulsing rim in the element colour, its icon and the stacks.</summary>
        public static IRect DrawElementBadge(Canvas canvas, Art art, Glossary g, string name, int stacks, int cx, int cy, float t = 0f)
        {
            var ink = g.ElementColor.TryGetValue(name, out var c) ? c : Rgba.White;
            const int r = ElementBadgeR;
            var pulse = 0.5 + 0.5 * Math.Sin(t * 3.2);
            canvas.Blit(ElementGlow(art), cx - r * 2, cy - r * 2, 1f, ink.WithAlpha((int)(10 + 8 * pulse)));
            canvas.Circle(cx, cy, r, new Rgba(16, 12, 20));
            canvas.Circle(cx, cy, r, ink, 2);
            var iconName = g.StatusIconOf(name);
            if (art.HasIcon(iconName))
            {
                var icon = art.Icon(iconName); var ir = IRect.AtCenter(cx, cy - 1, icon.W * 2, icon.H * 2);
                canvas.Blit(icon, ir.X, ir.Y, 2);
            }
            Outlined(canvas, art.Fonts.Get(16), stacks.ToString(), Anchor.MidLeft, cx + r - 6, cy + r - 6, ink);
            return new IRect(cx - r, cy - r, r * 2, r * 2);
        }

        // ------------------------------------------------------------------ player
        public static IRect DrawPlayer(Canvas canvas, Art art, Glossary g, CombatState state, int x, int y, Sprite? sprite = null, bool framed = true,
            int incomingLoss = 0, float t = 0f, EntityHitboxes hitboxes = null, int? blockShown = null)
        {
            var block = blockShown ?? state.Block;
            var rect = new IRect(x, y, PlayerW, PlayerH);
            if (framed) canvas.Rect(rect, Colors.PlayerBody, 0, Corner);
            if (sprite.HasValue && sprite.Value.Valid)
            {
                var s = sprite.Value; var sr = IRect.AtCenter(rect.CenterX, rect.CenterY, s.W, s.H);
                canvas.Blit(s, sr.X, sr.Y);
            }
            if (framed) canvas.Rect(rect, Colors.PlayerAccent, 2, Corner);
            Outlined(canvas, art.Fonts.Get(12), state.Hero.Name, Anchor.MidBottom, rect.CenterX, y - 4, Colors.TextAccent);
            var hp = new IRect(x, rect.Bottom + 4, PlayerW, HpH + 2);
            HpBar(canvas, art, hp, state.Hp, state.Hero.MaxHp, block, incomingLoss, t);
            BlockBadge(canvas, art, hp.X, hp.CenterY, block);
            if (incomingLoss > 0) Outlined(canvas, art.Fonts.Get(14), "-" + incomingLoss, Anchor.MidLeft, hp.Right + 5, hp.CenterY, new Rgba(255, 110, 96));
            hitboxes?.Clear();
            StatusEffects(canvas, art, g, state.HeroStatuses, null, x, rect.Bottom + 23, PlayerW + 40, hitboxes?.Statuses);
            return rect;
        }
    }
}
