using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>ui/combat_log_widget.py: the history strip on the battlefield's left edge (newest on top, scroll, hover / tap).</summary>
    public sealed class CombatLogStrip
    {
        public static readonly IRect DefaultRect = new IRect(6, 74, 48, 384);
        public const int TileSize = 40, Step = 44, Sep = 18, Arrow = 14, DragStart = 6;
        public const float Fresh = 1.2f;
        private static readonly Dictionary<string, (Rgba fill, Rgba rim)> Style = new Dictionary<string, (Rgba, Rgba)>
        {
            ["hero"] = (new Rgba(36, 46, 74), new Rgba(226, 172, 58)),
            ["enemy"] = (new Rgba(70, 24, 30), new Rgba(220, 66, 58)),
            ["effect"] = (new Rgba(26, 40, 56), new Rgba(130, 190, 240)),
        };
        private static readonly Dictionary<string, Rgba> ValueInk = new Dictionary<string, Rgba>
        { ["damage"] = new Rgba(255, 150, 110), ["block"] = new Rgba(120, 190, 255), ["loss"] = new Rgba(255, 96, 86) };

        public static (Rgba fill, Rgba rim) StyleOf(string kind) => Style.TryGetValue(kind ?? "", out var s) ? s : Style["effect"];

        public readonly IRect Rect;
        public float Offset, Target;
        public LogEntry Pinned;
        private readonly List<(LogEntry entry, int y, int h)> layout = new List<(LogEntry, int, int)>();
        private int contentH, version = -1;
        private readonly Dictionary<LogEntry, float> fresh = new Dictionary<LogEntry, float>();
        private readonly List<LogEntry> freshDone = new List<LogEntry>();
        private (float y, float offset)? press;
        private bool dragging;
        public (float x, float y) Mouse;

        public CombatLogStrip(IRect? rect = null) { Rect = rect ?? DefaultRect; }
        public bool Dragging => dragging;

        public void Sync(CombatLog log)
        {
            if (log.Version == version) return;
            var oldH = contentH; var first = version < 0;
            version = log.Version;
            layout.Clear(); var y = 0;
            for (var i = log.Entries.Count - 1; i >= 0; i--)
            {
                var e = log.Entries[i]; var h = e.Kind == "turn" ? Sep : Step;
                layout.Add((e, y, h)); y += h;
            }
            contentH = y;
            var alive = new HashSet<LogEntry>(); foreach (var (e, _, _) in layout) alive.Add(e);
            if (!first) foreach (var (e, _, _) in layout) if (!fresh.ContainsKey(e)) fresh[e] = 0f;
            freshDone.Clear(); foreach (var k in fresh.Keys) if (!alive.Contains(k)) freshDone.Add(k);
            foreach (var k in freshDone) fresh.Remove(k);
            if (Target > 0 && !first) { var grow = contentH - oldH; Target += grow; Offset += grow; }
            Target = Clamp(Target); Offset = Clamp(Offset);
            if (Pinned != null && !alive.Contains(Pinned)) Pinned = null;
        }

        public int MaxOffset => Math.Max(0, contentH - Rect.H);
        private float Clamp(float v) => Math.Max(0f, Math.Min(MaxOffset, v));
        public void Scroll(float px) => Target = Clamp(Target + px);

        public void Update(float dt)
        {
            dt = Math.Max(0f, Math.Min(dt, 0.1f));
            Offset += (Target - Offset) * Math.Min(1f, dt * 14);
            if (Math.Abs(Target - Offset) < 0.5f) Offset = Target;
            freshDone.Clear();
            var keys = new List<LogEntry>(fresh.Keys);
            foreach (var k in keys) { var a = fresh[k] + dt; if (a > Fresh) fresh.Remove(k); else fresh[k] = a; }
        }

        private IRect UpArrow => new IRect(Rect.X, Rect.Y, Rect.W, Arrow);
        private IRect DownArrow => new IRect(Rect.X, Rect.Bottom - Arrow, Rect.W, Arrow);

        /// <summary>True when the strip used the event.</summary>
        public bool HandleEvent(InputEvent ev)
        {
            switch (ev.Type)
            {
                case EventType.MouseMove:
                    Mouse = ev.Pos;
                    if (press.HasValue)
                    {
                        var (y0, off0) = press.Value;
                        if (dragging || Math.Abs(ev.Pos.y - y0) >= DragStart) { dragging = true; Target = Offset = Clamp(off0 - (ev.Pos.y - y0)); }
                        return true;
                    }
                    return false;
                case EventType.Wheel:
                    if (Rect.Contains(Mouse.x, Mouse.y)) { Scroll(-ev.Wheel * Step); return true; }
                    return false;
                case EventType.MouseDown:
                    if (ev.Button == 4 || ev.Button == 5)
                    {
                        if (Rect.Contains(ev.Pos.x, ev.Pos.y)) { Scroll(ev.Button == 5 ? Step : -Step); return true; }
                        return false;
                    }
                    if (ev.Button != 1) return false;
                    Mouse = ev.Pos;
                    if (Rect.Contains(ev.Pos.x, ev.Pos.y)) { press = (ev.Pos.y, Offset); dragging = false; return true; }
                    Pinned = null;
                    return false;
                case EventType.MouseUp:
                    if (ev.Button != 1 || !press.HasValue) return false;
                    var dragged = dragging; press = null; dragging = false;
                    if (!dragged) Tap(ev.Pos);
                    return true;
            }
            return false;
        }

        private void Tap((float x, float y) pos)
        {
            if (Offset > 0 && UpArrow.Contains(pos.x, pos.y)) { Scroll(-2 * Step); return; }
            if (Offset < MaxOffset && DownArrow.Contains(pos.x, pos.y)) { Scroll(2 * Step); return; }
            var entry = EntryAt(pos);
            Pinned = entry == null || entry == Pinned ? null : entry;
        }

        public IRect? TileRect(LogEntry entry)
        {
            foreach (var (e, y, h) in layout)
                if (e == entry) return new IRect(Rect.X + (Rect.W - TileSize) / 2, Rect.Y + y - MathX.Round(Offset), TileSize, Math.Min(TileSize, h));
            return null;
        }

        public LogEntry EntryAt((float x, float y) pos)
        {
            if (!Rect.Contains(pos.x, pos.y)) return null;
            var y = pos.y - Rect.Y + Offset;
            foreach (var (e, ey, h) in layout) if (ey <= y && y < ey + h && e.Kind != "turn") return e;
            return null;
        }

        public LogEntry ShownEntry() => dragging ? null : Pinned ?? EntryAt(Mouse);

        public void Draw(Canvas canvas, Art art)
        {
            if (layout.Count == 0) return;
            canvas.Rect(Rect, new Rgba(10, 8, 14, 150), 0, 8);
            canvas.Rect(Rect, new Rgba(90, 80, 110, 160), 1, 8);
            canvas.PushClip(Rect.Inflate(-2, -2));
            var shown = ShownEntry();
            var top = Offset; var off = MathX.Round(Offset);
            foreach (var (entry, y, h) in layout)
            {
                if (y + h <= top || y >= top + Rect.H) continue;
                var ty = Rect.Y + y - off;
                if (entry.Kind == "turn") { DrawSeparator(canvas, art, entry, Rect.X, ty); continue; }
                var x = Rect.X + (Rect.W - TileSize) / 2;
                DrawTile(canvas, art, entry, x, ty);
                var isFresh = fresh.TryGetValue(entry, out var age);
                if (isFresh || entry == shown)
                {
                    var k = entry == shown ? 1f : Math.Max(0f, 1 - age / Fresh);
                    if (k > 0) canvas.Rect(new IRect(x - 1, ty - 1, TileSize + 2, TileSize + 2), entry == shown ? new Rgba(255, 236, 160) : new Rgba(255, 214, 90), k > 0.4f ? 2 : 1, 7);
                }
            }
            canvas.PopClip();
            foreach (var (arrow, show, up) in new[] { (UpArrow, Offset > 0.5f, true), (DownArrow, Offset < MaxOffset - 0.5f, false) })
            {
                if (!show) continue;
                int cx = arrow.CenterX, cy = arrow.CenterY;
                canvas.Rect(arrow, new Rgba(14, 10, 18), 0, 5);
                var gold = new Rgba(246, 204, 92);
                if (up) canvas.Triangle(cx - 7, cy + 3, cx + 7, cy + 3, cx, cy - 4, gold);
                else canvas.Triangle(cx - 7, cy - 3, cx + 7, cy - 3, cx, cy + 4, gold);
            }
        }

        private void DrawSeparator(Canvas canvas, Art art, LogEntry entry, int x, int y)
        {
            var mid = y + Sep / 2;
            canvas.Line(x + 2, mid, x + Rect.W - 3, mid, new Rgba(120, 100, 70));
            Txt.OutlinedAt(canvas, art.Fonts.Get(8), "T" + entry.Turn, Anchor.Center, x + Rect.W / 2, mid, new Rgba(246, 204, 92));
        }

        private static void DrawTile(Canvas canvas, Art art, LogEntry entry, int x, int y)
        {
            var (fill, rim) = StyleOf(entry.Kind);
            var r = new IRect(x, y, TileSize, TileSize);
            canvas.Rect(r, fill, 0, 6);
            canvas.Rect(r, rim, 2, 6);
            if (art.HasIcon(entry.Icon))
            {
                var icon = art.Icon(entry.Icon); var ir = IRect.AtCenter(x + TileSize / 2, y + TileSize / 2 - 2, icon.W * 2, icon.H * 2);
                canvas.Blit(icon, ir.X, ir.Y, 2);
            }
            if (entry.Value > 0)
            {
                var text = (entry.ValueKind == "loss" ? "−" : "") + entry.Value;
                Txt.OutlinedAt(canvas, art.Fonts.Get(8), text, Anchor.BottomRight, x + TileSize - 1, y + TileSize, ValueInk.TryGetValue(entry.ValueKind, out var ink) ? ink : new Rgba(236, 228, 214));
            }
        }

        public static TooltipContent EntryTooltip(LogEntry entry)
        {
            var who = entry.Kind == "hero" ? "Tú" : entry.Kind == "enemy" ? "Enemigo" : entry.Kind == "effect" ? "Efecto" : "";
            return new TooltipContent(entry.Title) { Lines = new List<string>(entry.Lines), Icon = entry.Icon, Subtitle = "Turno " + entry.Turn + "  ·  " + who, Accent = StyleOf(entry.Kind).rim };
        }
    }

    /// <summary>combat_log_widget.render_entry_detail: who acted → on whom, then the sentences. Laid out once per entry.</summary>
    public sealed class LogDetail
    {
        public const int Portrait = 64, TargetW = 84, MaxTargets = 4, Pad = 10, TextW = 300;
        private static readonly Rgba HeroRim = new Rgba(226, 172, 58), EnemyRim = new Rgba(220, 66, 58), Ink = new Rgba(236, 228, 214);

        public LogEntry Entry { get; private set; }
        public int W { get; private set; }
        public int H { get; private set; }
        private int rowH, more;
        private bool hasArrow;
        private (int w, int h) source;
        private readonly List<object> textLines = new List<object>();   // string title/sub handled separately, RichLine for body

        /// <summary>Lay out the panel for <paramref name="entry"/> (no-op when it is already this one).</summary>
        public void Layout(GameContext game, LogEntry entry, (int w, int h) cardSize)
        {
            if (Entry == entry) return;
            Entry = entry;
            source = (0, 0);
            if (entry.Card != null) source = cardSize;
            else if (!string.IsNullOrEmpty(entry.Source)) source = (TargetW, Portrait + 40);
            hasArrow = entry.Targets.Length > 0 && source.w > 0;
            var shownTargets = Math.Min(MaxTargets, entry.Targets.Length);
            more = entry.Targets.Length - shownTargets;
            var rowW = source.w + (hasArrow ? 52 : 0) + shownTargets * TargetW + (more > 0 ? 30 : 0);
            rowH = Math.Max(source.h, shownTargets > 0 ? Portrait + 40 : 0);
            W = Math.Max(TextW, rowW) + 2 * Pad;
            var g = game.Data.Glossary; var font = game.Fonts.Get(14);
            textLines.Clear();
            var textH = (Txt.OutlinedSize(game.Fonts.Get(16), entry.Title).h + 2) + (Txt.OutlinedSize(game.Fonts.Get(8), "T").h + 2);
            foreach (var line in entry.Lines)
                foreach (var rl in RichText.Lines(g, line, font, W - 2 * Pad)) { textLines.Add(rl); textH += rl.Height + 2; }
            H = Pad * 2 + rowH + (rowH > 0 ? 10 : 0) + textH;
        }

        private static void PortraitBox(Canvas canvas, Art art, Glossary g, int bx, int by, string name, Sprite? image, Rgba rim, LogTarget t = null)
        {
            var frame = new IRect(bx + (TargetW - Portrait) / 2, by + 4, Portrait, Portrait);
            canvas.Rect(frame, new Rgba(20, 16, 26), 0, 8);
            var died = t != null && t.Died; var aimed = t != null && t.Aimed;
            Sprite pic = image ?? default; float picScale = 1f;
            if (!pic.Valid)
            {
                var fallback = rim.Equals(HeroRim) ? "helmet" : "unknown";
                if (art.HasIcon(fallback)) { pic = art.Icon(fallback); picScale = 3f; }
            }
            if (pic.Valid)
            {
                float iw = pic.W * picScale, ih = pic.H * picScale;
                var k = Math.Min((Portrait - 6) / Math.Max(1f, iw), (Portrait - 6) / Math.Max(1f, ih));
                int pw = Math.Max(1, (int)(iw * k)), ph = Math.Max(1, (int)(ih * k));
                canvas.BlitRect(pic, IRect.AtCenter(frame.CenterX, frame.CenterY, pw, ph), died ? new Rgba(90, 90, 90) : Rgba.White);
            }
            canvas.Rect(frame, aimed ? new Rgba(255, 236, 160) : rim, aimed ? 3 : 2, 8);
            if (died && art.HasIcon("lethal"))
            {
                var skull = art.Icon("lethal"); var sr = IRect.AtCenter(frame.CenterX, frame.CenterY, skull.W * 2, skull.H * 2);
                canvas.Blit(skull, sr.X, sr.Y, 2);
            }
            if (t != null)
            {
                var splashed = false;
                foreach (var (value, ink, prefix) in new[] { (t.Hp, new Rgba(255, 90, 70), "−"), (t.Block, new Rgba(110, 180, 255), "+"), (t.Healed, new Rgba(120, 236, 120), "+") })
                {
                    if (value <= 0) continue;
                    var font = art.Fonts.Get(16); var text = prefix + value; var (nw, nh) = Txt.OutlinedSize(font, text);
                    var r = Math.Max(nw, nh) / 2 + 5; int cx = frame.Right - 4, cy = frame.Y + 10;
                    canvas.Circle(cx, cy, r, new Rgba(24, 12, 16));
                    canvas.Circle(cx, cy, r, ink, 2);
                    Txt.OutlinedAt(canvas, font, text, Anchor.Center, cx, cy, ink);
                    splashed = true; break;
                }
                if (!splashed && t.Blocked && art.HasIcon("block"))
                {
                    var s = art.Icon("block"); var sr = IRect.AtCenter(frame.Right - 4, frame.Y + 10, s.W * 2, s.H * 2);
                    canvas.Blit(s, sr.X, sr.Y, 2);
                }
                for (var k = 0; k < Math.Min(2, t.Reactions.Length); k++)
                {
                    string iconName = null;
                    if (g != null) foreach (var p in g.ReactionName) if (p.Value == t.Reactions[k]) { g.ReactionIcon.TryGetValue(p.Key, out iconName); break; }
                    if (iconName != null && art.HasIcon(iconName)) canvas.Blit(art.Icon(iconName), frame.X - 4, frame.Y - 2 + k * 24, 2);
                }
            }
            var small = art.Fonts.Get(8);
            Txt.OutlinedAt(canvas, small, TextFit.Fit(small, name, TargetW - 4), Anchor.Center, bx + TargetW / 2, frame.Bottom + 9, Ink);
            if (t != null)
            {
                var x = bx + 4;
                for (var k = 0; k < Math.Min(4, t.Statuses.Length); k++)
                {
                    var (sname, n) = t.Statuses[k];
                    var iconName = g != null ? g.StatusIconOf(sname) : null;
                    if (iconName == null || !art.HasIcon(iconName)) continue;
                    canvas.Blit(art.Icon(iconName), x, frame.Bottom + 18);
                    var stack = "+" + n;
                    Txt.Outlined(canvas, small, stack, x + 15, frame.Bottom + 19, Ink);
                    x += 15 + Txt.OutlinedSize(small, stack).w + 3;
                }
            }
        }

        public void Draw(Canvas canvas, GameContext game, int px, int py, Func<string, Sprite?> portrait, Action<Canvas, CardDefinition, int, int> drawCard)
        {
            var entry = Entry; if (entry == null) return;
            var art = game.Art; var (_, accent) = CombatLogStrip.StyleOf(entry.Kind);
            var panel = new IRect(px, py, W, H);
            canvas.Rect(panel, new Rgba(16, 12, 22, 240), 0, 10);
            canvas.Rect(panel, accent, 2, 10);
            int x = px + Pad, y = py + Pad;
            if (entry.Card != null && drawCard != null) { drawCard(canvas, entry.Card, x, y + (rowH - source.h) / 2); x += source.w; }
            else if (source.w > 0)
            {
                PortraitBox(canvas, art, game.Data.Glossary, x, y + (rowH - source.h) / 2, string.IsNullOrEmpty(entry.SourceName) ? entry.Source : entry.SourceName, portrait?.Invoke(entry.Source),
                    entry.Source == CombatLog.Hero ? HeroRim : EnemyRim);
                x += source.w;
            }
            if (hasArrow)
            {
                var cy = y + rowH / 2 - 8;
                canvas.Line(x + 4, cy, x + 46, cy, Ink, 3);
                canvas.Triangle(x + 46, cy - 7, x + 46, cy + 7, x + 52, cy, Ink);
                var verb = string.IsNullOrEmpty(entry.Verb) ? "attack_2" : entry.Verb;
                if (art.HasIcon(verb)) { var a = art.Icon(verb); var ar = IRect.AtCenter(x + 25, cy - 16, a.W * 2, a.H * 2); canvas.Blit(a, ar.X, ar.Y, 2); }
                x += 52;
            }
            for (var i = 0; i < Math.Min(MaxTargets, entry.Targets.Length); i++)
            {
                var t = entry.Targets[i];
                PortraitBox(canvas, art, game.Data.Glossary, x, y + (rowH - (Portrait + 40)) / 2, t.Name, portrait?.Invoke(t.Portrait), t.Portrait == CombatLog.Hero ? HeroRim : EnemyRim, t);
                x += TargetW;
            }
            if (more > 0) Txt.Outlined(canvas, art.Fonts.Get(16), "+" + more, x + 4, y + rowH / 2 - 8, Ink);
            y += rowH + (rowH > 0 ? 10 : 0);
            var titleF = art.Fonts.Get(16); var subF = art.Fonts.Get(8);
            Txt.Outlined(canvas, titleF, entry.Title, px + Pad, y, new Rgba(246, 204, 92)); y += Txt.OutlinedSize(titleF, entry.Title).h + 2;
            var who = entry.Kind == "hero" ? "Tú" : entry.Kind == "enemy" ? "Enemigo" : entry.Kind == "effect" ? "Efecto" : "";
            var sub = "Turno " + entry.Turn + "  ·  " + who;
            Txt.Outlined(canvas, subF, sub, px + Pad, y, new Rgba(176, 170, 160)); y += Txt.OutlinedSize(subF, sub).h + 2;
            foreach (var o in textLines) { var rl = (RichLine)o; rl.Draw(canvas, px + Pad, y); y += rl.Height + 2; }
        }
    }
}
