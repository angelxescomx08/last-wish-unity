using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>application/play_preview.EnemyPreview.</summary>
    public sealed class EnemyPreview
    {
        public int Index;
        public (ElementKind element, int stacks)? Before, After;
        public readonly List<ReactionEvent> Reactions = new List<ReactionEvent>();
        public int HpLoss; public bool Killed;
        public bool Changed => Reactions.Count > 0 || !Nullable.Equals(Before, After) || HpLoss > 0;
    }

    public sealed class PlayPreview
    {
        public readonly List<EnemyPreview> Enemies = new List<EnemyPreview>();
        public int BlockGain; public bool ReactionKeyword;
    }

    /// <summary>application/play_preview.py + ui/element_preview.py: what an aimed Mage card will do, and who would react.</summary>
    public static class ElementPreview
    {
        public static readonly ElementKind[] Order = { ElementKind.Fire, ElementKind.Water, ElementKind.Earth, ElementKind.Air };
        public static readonly string[] Names = { "Fuego", "Agua", "Tierra", "Aire" };
        private static readonly string[] Icons = { "fire", "water", "earth", "air" };
        private static readonly Rgba Bg = new Rgba(22, 16, 28), Text = new Rgba(236, 228, 214);
        private const int Pad = 10, RowH = 28;

        public static string Name(ElementKind e) => Names[(int)e];
        public static string Icon(ElementKind e) => Icons[(int)e];
        public static Rgba Ink(Glossary g, ElementKind e) => g.ElementColor.TryGetValue(Name(e), out var c) ? c : Rgba.White;
        public static string ReactionKey(ReactionKind r) => r.ToString().ToUpperInvariant();
        public static Rgba ReactionColor(Glossary g, ReactionKind r) => g.ReactionColor.TryGetValue(ReactionKey(r), out var c) ? c : Rgba.White;
        public static string ReactionIcon(Glossary g, ReactionKind r) => g.ReactionIcon.TryGetValue(ReactionKey(r), out var i) ? i : "reaction";
        public static StatusKind StatusOf(ElementKind e) => (StatusKind)((int)StatusKind.Fire + (int)e);
        public static bool TryElement(string name, out ElementKind e) { var i = Array.IndexOf(Names, name); e = (ElementKind)Math.Max(0, i); return i >= 0; }

        public static ReactionKind? ReactionFor(ElementKind a, ElementKind b)
        {
            if (a == b) return null;
            int lo = Math.Min((int)a, (int)b), hi = Math.Max((int)a, (int)b);
            if (lo == 0) return hi == 1 ? ReactionKind.Steam : hi == 2 ? ReactionKind.Magma : ReactionKind.Wildfire;
            if (lo == 1) return hi == 2 ? ReactionKind.Mud : ReactionKind.Freeze;
            return ReactionKind.Sandstorm;
        }

        public static (ElementKind, int)? Held(EnemyState enemy)
        {
            foreach (var k in enemy.Statuses.Active)
                if (k >= StatusKind.Fire && k <= StatusKind.Air) return ((ElementKind)((int)k - (int)StatusKind.Fire), enemy.Statuses[k]);
            return null;
        }

        /// <summary>preview_play: the play simulated on a copy of the combat (same random stream); null if it cannot be played.</summary>
        public static PlayPreview Preview(CombatState state, int cardIndex, int? target)
        {
            if (cardIndex < 0 || cardIndex >= state.Hand.Count) return null;
            var sim = state.Clone();
            var before = new List<(int hp, (ElementKind, int)? held)>();
            foreach (var e in sim.Enemies) before.Add((e.Hp, Held(e)));
            var block = sim.Block;
            var result = sim.Play(cardIndex, target);
            if (!result.Success) return null;
            var preview = new PlayPreview { BlockGain = Math.Max(0, sim.Block - block), ReactionKeyword = result.Reaction };
            for (var i = 0; i < sim.Enemies.Count && i < before.Count; i++)
            {
                var enemy = sim.Enemies[i]; var (hp, held) = before[i];
                var ep = new EnemyPreview { Index = i, Before = held, After = Held(enemy), HpLoss = Math.Max(0, hp - enemy.Hp), Killed = hp > 0 && !enemy.Alive };
                foreach (var ev in sim.Reactions) if (ev.EnemyId == enemy.Id) ep.Reactions.Add(ev);
                preview.Enemies.Add(ep);
            }
            return preview;
        }

        /// <summary>reacting_enemies: living enemies an element of the card would react with → the reaction (first element wins).</summary>
        public static void ReactingEnemies(GameContext game, CombatState state, CardDefinition card, Dictionary<int, ReactionKind> output)
        {
            output.Clear();
            var info = game.Data.Card(card);
            if (info.Chips == null) return;
            foreach (var (name, _, _) in info.Chips)
            {
                if (!TryElement(name, out var element)) continue;
                for (var i = 0; i < state.Enemies.Count; i++)
                {
                    if (output.ContainsKey(i)) continue;
                    var enemy = state.Enemies[i]; if (!enemy.Alive) continue;
                    var held = Held(enemy);
                    if (held.HasValue && held.Value.Item1 != element) output[i] = ReactionFor(held.Value.Item1, element).Value;
                }
            }
        }

        // ------------------------------------------------------------------ rows: "@icon" or (text, colour)
        public readonly struct Part
        {
            public readonly string Icon, Text; public readonly Rgba Color;
            private Part(string icon, string text, Rgba color) { Icon = icon; Text = text; Color = color; }
            public static Part Ic(string icon) => new Part(icon, null, default);
            public static Part T(string text, Rgba color) => new Part(null, text, color);
        }

        public static List<List<Part>> Rows(Glossary g, EnemyPreview ep, string keyword = "", bool compact = false)
        {
            var rows = new List<List<Part>>();
            if (compact)
            {
                for (var k = 0; k < Math.Min(2, ep.Reactions.Count); k++)
                {
                    var ev = ep.Reactions[k];
                    rows.Add(new List<Part> { Part.Ic(ReactionIcon(g, ev.Kind)), Part.T("¡" + CombatLog.ReactionName(g, ev.Kind).ToUpperInvariant() + "!", ReactionColor(g, ev.Kind)) });
                }
                if (ep.Reactions.Count == 0 && ep.After.HasValue && !Nullable.Equals(ep.After, ep.Before))
                {
                    var (el, n) = ep.After.Value;
                    rows.Add(new List<Part> { Part.Ic(Icon(el)), Part.T(Name(el) + " " + n, Ink(g, el)) });
                }
                if (rows.Count > 0 && ep.HpLoss > 0) rows.Add(new List<Part> { Part.T("−" + ep.HpLoss + " de vida", g.Damage) });
                return rows;
            }
            for (var k = 0; k < Math.Min(2, ep.Reactions.Count); k++)
            {
                var ev = ep.Reactions[k];
                var row = new List<Part>();
                var had = ep.Before.HasValue && ep.Before.Value.element == ev.First ? ep.Before.Value.stacks : Math.Max(0, ev.Stacks - 1);
                row.Add(Part.Ic(Icon(ev.First))); row.Add(Part.T(had.ToString(), Ink(g, ev.First))); row.Add(Part.T("+", Text));
                row.Add(Part.Ic(Icon(ev.Second))); row.Add(Part.T(Math.Max(0, ev.Stacks - had).ToString(), Ink(g, ev.Second))); row.Add(Part.T("=", Text));
                row.Add(Part.Ic(ReactionIcon(g, ev.Kind))); row.Add(Part.T("¡" + CombatLog.ReactionName(g, ev.Kind).ToUpperInvariant() + "!", ReactionColor(g, ev.Kind)));
                rows.Add(row);
                rows.Add(new List<Part> { Part.T(CombatLog.ReactionSummary(ev.Kind, ev.Stacks), Text) });
            }
            if (ep.Reactions.Count == 0 && ep.After.HasValue && !Nullable.Equals(ep.After, ep.Before))
            {
                var (el, n) = ep.After.Value;
                if (ep.Before.HasValue && ep.Before.Value.element == el) rows.Add(new List<Part> { Part.Ic(Icon(el)), Part.T(Name(el) + " " + ep.Before.Value.stacks + " → " + n, Ink(g, el)) });
                else rows.Add(new List<Part> { Part.Ic(Icon(el)), Part.T("+" + n + " de " + Name(el), Ink(g, el)) });
            }
            if (!string.IsNullOrEmpty(keyword)) rows.Add(new List<Part> { Part.Ic("reaction"), Part.T("Reacción: " + keyword, g.ReactionInk) });
            if (rows.Count > 0 && ep.HpLoss > 0) rows.Add(new List<Part> { Part.T("−" + ep.HpLoss + " de vida" + (ep.Killed ? "  ¡muere!" : ""), g.Damage) });
            return rows;
        }

        private static int RowWidth(Art art, List<Part> row, FontFace font)
        {
            var w = 0;
            foreach (var p in row)
                if (p.Icon != null) w += (art.HasIcon(p.Icon) ? art.Icon(p.Icon).W * 2 : 0) + 4;
                else w += font.Width(p.Text) + 6;
            return w;
        }

        /// <summary>draw_row: left to right, vertically centred on <paramref name="cy"/>; returns the end x.</summary>
        public static int DrawRow(Canvas canvas, Art art, List<Part> row, int x, int cy, int size = 16)
        {
            var font = art.Fonts.Get(size);
            foreach (var p in row)
            {
                if (p.Icon != null)
                {
                    if (!art.HasIcon(p.Icon)) continue;
                    var icon = art.Icon(p.Icon);
                    canvas.Blit(icon, x, cy - icon.H, 2);
                    x += icon.W * 2 + 4;
                }
                else
                {
                    var r = Txt.OutlinedAt(canvas, font, p.Text, Anchor.MidLeft, x, cy, p.Color);
                    x += (int)r.W + 6;
                }
            }
            return x;
        }

        public static IRect? DrawBubble(Canvas canvas, Art art, int anchorX, int anchorY, List<List<Part>> rows, Rgba accent, float t = 0f, int? bottomLimit = null)
        {
            if (rows.Count == 0) return null;
            var font = art.Fonts.Get(16);
            var w = 0; foreach (var r in rows) w = Math.Max(w, RowWidth(art, r, font)); w += 2 * Pad;
            var h = rows.Count * RowH + 2 * Pad - 6;
            var x = Math.Max(6, Math.Min(Canvas.Width - w - 6, anchorX - w / 2));
            var y = anchorY + 10;
            if (bottomLimit.HasValue && y + h > bottomLimit.Value) y = Math.Max(70, bottomLimit.Value - h);
            var rect = new IRect(x, y, w, h);
            var pulse = 0.5 + 0.5 * Math.Sin(t * 4.0);
            var k = 0.8 + 0.2 * pulse;
            var rim = new Rgba(Math.Min(255, (int)(accent.R * k)), Math.Min(255, (int)(accent.G * k)), Math.Min(255, (int)(accent.B * k)));
            canvas.Rect(rect, Bg, 0, 8);
            canvas.Rect(rect, rim, 2, 8);
            var px = Math.Max(rect.X + 12, Math.Min(rect.Right - 12, anchorX));
            canvas.Triangle(px - 8, rect.Y, px + 8, rect.Y, px, rect.Y - 9, rim);
            for (var i = 0; i < rows.Count; i++) DrawRow(canvas, art, rows[i], rect.X + Pad, rect.Y + Pad + i * RowH + RowH / 2 - 3);
            return rect;
        }

        public static void DrawReactHint(Canvas canvas, Art art, Glossary g, IRect badge, ReactionKind reaction, float t)
        {
            var color = ReactionColor(g, reaction);
            var pulse = 0.5 + 0.5 * Math.Sin(t * 6.0);
            canvas.Circle(badge.CenterX, badge.CenterY, badge.W / 2 + 3 + (int)(2 * pulse), color, 2);
            var iconName = ReactionIcon(g, reaction);
            if (art.HasIcon(iconName))
            {
                var icon = art.Icon(iconName); var bob = (int)(3 * Math.Sin(t * 5.0));
                var r = Txt.Place(icon.W * 2, icon.H * 2, Anchor.MidLeft, badge.Right + 4, badge.CenterY - 18 + bob);
                canvas.Blit(icon, r.X, r.Y, 2);
            }
        }

        /// <summary>draw_element_floor: the glow is blitted with BLEND_RGBA_ADD, which ignores its alpha, so the whole ellipse adds the colour.</summary>
        public static void DrawElementFloor(Canvas canvas, int feetX, int feetY, Rgba color, float t, int width = 150, bool strong = false)
        {
            var h = Math.Max(18, width / 4);
            canvas.Ellipse(IRect.AtCenter(feetX, feetY, width, h), color, 0, Blend.Add);
        }
    }
}
