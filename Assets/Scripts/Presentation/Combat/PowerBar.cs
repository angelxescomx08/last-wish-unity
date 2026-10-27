using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>The hero's active powers as badges under the statuses (Slay the Spire style): one badge per card with
    /// its art, a count for copies (a golden one counts twice), a pop when one is added and a flash when it acts.
    /// Hovering a badge shows <see cref="Tooltip.Power"/>.</summary>
    public sealed class PowerBar
    {
        public const int Size = 34, Step = 38;
        public const float PopT = .55f, FlashT = .6f;
        private static readonly Rgba Back = new Rgba(30, 14, 46), Rim = new Rgba(160, 96, 230), Ink = new Rgba(12, 8, 14);

        public readonly struct Badge
        {
            public readonly CardDefinition Card; public readonly int Copies; public readonly IRect Rect;
            public Badge(CardDefinition card, int copies, IRect rect) { Card = card; Copies = copies; Rect = rect; }
        }

        private readonly List<Badge> badges = new List<Badge>();
        private readonly Dictionary<string, int> known = new Dictionary<string, int>();
        private readonly Dictionary<string, float> pops = new Dictionary<string, float>(), flashes = new Dictionary<string, float>();
        private readonly Dictionary<string, int> gained = new Dictionary<string, int>();
        public IReadOnlyList<Badge> Badges => badges;

        /// <summary>Powers grouped by card in play order: the best chroma represents them; copies = casts (golden 2, diamond 3).</summary>
        public static List<(CardDefinition card, int copies)> Group(IReadOnlyList<CardDefinition> powers)
        {
            var order = new List<string>(); var shown = new Dictionary<string, CardDefinition>(); var copies = new Dictionary<string, int>();
            foreach (var power in powers ?? Array.Empty<CardDefinition>())
            {
                if (power == null) continue;
                if (!shown.TryGetValue(power.Id, out var best)) { order.Add(power.Id); shown[power.Id] = power; copies[power.Id] = 0; }
                else if (power.Chroma > best.Chroma) shown[power.Id] = power;
                copies[power.Id] = (int)Math.Min(int.MaxValue, (long)copies[power.Id] + power.CastCount);
            }
            return order.Select(id => (shown[id], copies[id])).ToList();
        }

        /// <summary>Lay the badges out from (x, y), wrapping at <paramref name="maxW"/>; a new power or a new copy pops.</summary>
        public void Sync(IReadOnlyList<CardDefinition> powers, int x, int y, int maxW)
        {
            badges.Clear();
            var perRow = Math.Max(1, maxW / Step); var i = 0;
            foreach (var (card, copies) in Group(powers))
            {
                badges.Add(new Badge(card, copies, new IRect(x + i % perRow * Step, y + i / perRow * Step, Size, Size))); i++;
                var before = known.TryGetValue(card.Id, out var b) ? b : 0;
                if (copies > before) { pops[card.Id] = PopT; gained[card.Id] = copies - before; }
                known[card.Id] = copies;
            }
        }

        /// <summary>Height the badges take below their first row (0 without powers).</summary>
        public int Height(int maxW) => badges.Count == 0 ? 0 : ((badges.Count - 1) / Math.Max(1, maxW / Step) + 1) * Step;

        public void Flash(string id) { if (id != null) flashes[id] = FlashT; }
        public void FlashWhere(Func<CardDefinition, bool> acts) { foreach (var b in badges) if (acts(b.Card)) Flash(b.Card.Id); }
        public float Pop(string id) => pops.TryGetValue(id, out var p) ? p / PopT : 0f;
        public float Glow(string id) => flashes.TryGetValue(id, out var f) ? f / FlashT : 0f;

        public void Update(float dt)
        {
            dt = float.IsNaN(dt) ? 0f : Math.Max(0f, Math.Min(.25f, dt));
            foreach (var key in pops.Keys.ToList()) { pops[key] -= dt; if (pops[key] <= 0) pops.Remove(key); }
            foreach (var key in flashes.Keys.ToList()) { flashes[key] -= dt; if (flashes[key] <= 0) flashes.Remove(key); }
        }

        public int? At(float x, float y)
        {
            for (var i = 0; i < badges.Count; i++) if (badges[i].Rect.Inflate(2, 2).Contains(x, y)) return i;
            return null;
        }

        public void Draw(Canvas canvas, Art art, int? hovered, float time)
        {
            if (badges.Count > 0)
                Txt.OutlinedAt(canvas, art.Fonts.Get(10), "PODERES", Anchor.MidRight, badges[0].Rect.X - 8, badges[0].Rect.CenterY, new Rgba(200, 160, 250), Ink);
            for (var i = 0; i < badges.Count; i++)
            {
                var b = badges[i]; var id = b.Card.Id;
                var pop = Pop(id); var flash = Glow(id); var hot = hovered == i;
                var grow = 1f + .45f * pop * pop + (hot ? .08f : 0f);
                int size = (int)Math.Round(Size * grow), cx = b.Rect.CenterX, cy = b.Rect.CenterY;
                var r = IRect.AtCenter(cx, cy, size, size);
                var chroma = b.Card.Chroma != ChromaKind.None ? ChromaFx.Style(b.Card.Chroma) : null;
                var rim = chroma?.Main ?? Rim;
                var light = Math.Max(pop, flash);
                if (light > 0 || hot) Glows.Draw(canvas, art, cx, cy, (int)(26 + 18 * light), Glows.Scaled(rim, .35 + .65 * light));
                canvas.Rect(r, Back, 0, 4);
                var inner = r.Inflate(-3, -3);
                if (art.HasCardArt(id))
                {
                    var s = art.CardArt(id);
                    var k = Math.Max(inner.W / (double)s.W, inner.H / (double)s.H);
                    int bw = Math.Max(inner.W, MathX.Round(s.W * k)), bh = Math.Max(inner.H, MathX.Round(s.H * k));
                    canvas.PushClip(new RectF(inner.X, inner.Y, inner.W, inner.H));
                    canvas.BlitRect(s, new RectF(inner.X - (bw - inner.W) / 2, inner.Y - (bh - inner.H) / 2, bw, bh), Rgba.White);
                    canvas.PopClip();
                }
                else if (art.HasIcon("ritual"))
                {
                    var icon = art.Icon("ritual"); var ir = IRect.AtCenter(cx, cy, icon.W * 2, icon.H * 2);
                    canvas.Blit(icon, ir.X, ir.Y, 2);
                }
                canvas.Rect(r, hot ? Glows.Scaled(rim, 1.35) : rim, 2, 4);
                if (flash > 0) canvas.Fill(new RectF(inner.X, inner.Y, inner.W, inner.H), Glows.Scaled(Rgba.White, .55 * flash), Blend.Add);
                if (b.Copies > 1)
                    Txt.OutlinedAt(canvas, art.Fonts.Get(13), "×" + b.Copies, Anchor.BottomRight, r.Right + 4, r.Bottom + 4, chroma?.Bright ?? new Rgba(236, 220, 255), Ink);
                if (pop > 0 && gained.TryGetValue(id, out var plus))
                {
                    var drop = (1f - pop) * 10f;
                    Txt.OutlinedAt(canvas, art.Fonts.Get(13), "+" + plus, Anchor.MidTop, cx, b.Rect.Bottom + 4 + drop, new Rgba(200, 255, 190).WithAlpha((int)(255 * Math.Min(1f, pop * 2))), Ink);
                }
            }
        }
    }
}
