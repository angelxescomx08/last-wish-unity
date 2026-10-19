using System;
using System.Collections.Generic;
using System.Numerics;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>Totals of a card for a set of ready keyword layers (Card.total_* with the keyword flags).</summary>
    public static class CardTotals
    {
        public static uint Bit(EffectCondition c) => 1u << (int)c;
        private static bool Active(EffectLayer layer, uint mask) => layer.Condition == EffectCondition.Always || (mask & Bit(layer.Condition)) != 0;

        public static bool HasLayer(CardDefinition card, EffectCondition c)
        {
            if (card.Effects != null) foreach (var e in card.Effects) if (e.Condition == c) return true;
            return c == EffectCondition.Combo && card.ComboBlock > 0 || c == EffectCondition.Endure && card.EndureStrength > 0;
        }
        /// <summary>The flags the card can use (``combo and bool(card.combo_effects())``…).</summary>
        public static uint Normalize(CardDefinition card, uint mask)
        {
            uint m = 0;
            for (var c = EffectCondition.Combo; c <= EffectCondition.Endure; c++)
                if ((mask & Bit(c)) != 0 && HasLayer(card, c)) m |= Bit(c);
            return m;
        }
        public static BigInteger Damage(CardDefinition card, uint mask = 0)
        {
            if (card.Effects == null) return card.Damage.Value;
            BigInteger d = 0; foreach (var l in card.Effects) if (Active(l, mask)) d += l.Damage; return d;
        }
        public static long Block(CardDefinition card, uint mask = 0)
        {
            if (card.Effects == null) return card.Block + ((mask & Bit(EffectCondition.Combo)) != 0 ? card.ComboBlock : 0);
            long b = 0; foreach (var l in card.Effects) if (Active(l, mask)) b += l.Block; return b;
        }
        public static int Draw(CardDefinition card, uint mask = 0)
        {
            if (card.Effects == null) return card.Draw;
            var d = 0; foreach (var l in card.Effects) if (Active(l, mask)) d += l.Draw; return d;
        }
        public static int Mana(CardDefinition card, uint mask = 0)
        {
            if (card.Effects == null) return card.ManaGain;
            var d = 0; foreach (var l in card.Effects) if (Active(l, mask)) d += l.Mana; return d;
        }
    }

    /// <summary>What a card shows besides its own data: the draw_card keyword arguments.</summary>
    public struct CardShow
    {
        public bool Unaffordable;
        public BigInteger BonusDamage;
        public long BonusBlock;
        /// <summary>Ready keyword layers (bits of <see cref="EffectCondition"/>).</summary>
        public uint Ready;
        public int? Cost;
        public static CardShow With(BigInteger bonusDamage, long bonusBlock) => new CardShow { BonusDamage = bonusDamage, BonusBlock = bonusBlock };
    }

    /// <summary>
    /// ui/card_widget.py: rarity frame + illustration + dynamic text. The static face is composed once
    /// into an offscreen surface keyed by every value it shows (LRU), then drawn as one quad — moved,
    /// scaled and tilted for free on the GPU; chroma sheen, auras and ready effects animate on top.
    /// </summary>
    public static class CardWidget
    {
        public const int CardW = 140, CardH = 194;
        public static readonly Rgba[] RarityColors = { new Rgba(170, 160, 150), new Rgba(170, 160, 150), new Rgba(80, 200, 110), new Rgba(80, 150, 240), new Rgba(180, 90, 240), new Rgba(245, 175, 40) };
        public static Rgba RarityColor(int rarity) => RarityColors[Math.Max(1, Math.Min(5, rarity))];

        private static readonly Dictionary<string, (Rgba dark, Rgba light)> TypeBackdrop = new Dictionary<string, (Rgba, Rgba)>
        {
            ["ATTACK"] = (new Rgba(70, 18, 22), new Rgba(150, 44, 40)), ["SKILL"] = (new Rgba(16, 30, 66), new Rgba(44, 90, 150)),
            ["POWER"] = (new Rgba(40, 16, 64), new Rgba(110, 56, 160)), ["STATUS"] = (new Rgba(26, 36, 14), new Rgba(82, 110, 34)),
        };
        private static readonly Rgba InkDark = new Rgba(34, 24, 18), InkLight = new Rgba(236, 230, 214), ManaInk = new Rgba(200, 230, 255),
            AtkInk = new Rgba(255, 150, 120), DefInk = new Rgba(150, 210, 255), BonusInk = new Rgba(140, 240, 140), Hole = new Rgba(18, 16, 24);
        public static readonly Dictionary<string, string> ElementIcon = new Dictionary<string, string> { ["Fuego"] = "fire", ["Agua"] = "water", ["Tierra"] = "earth", ["Aire"] = "air" };
        private static readonly Dictionary<string, (Rgba dark, Rgba mid, Rgba motif)> ElementBackdrop = new Dictionary<string, (Rgba, Rgba, Rgba)>
        {
            ["Fuego"] = (new Rgba(70, 16, 12), new Rgba(150, 44, 22), new Rgba(250, 150, 60)),
            ["Agua"] = (new Rgba(12, 30, 72), new Rgba(32, 82, 152), new Rgba(120, 190, 250)),
            ["Tierra"] = (new Rgba(42, 28, 16), new Rgba(98, 68, 38), new Rgba(40, 24, 14)),
            ["Aire"] = (new Rgba(24, 52, 62), new Rgba(62, 122, 134), new Rgba(190, 240, 245)),
        };
        private static readonly Dictionary<string, string> TypeIcon = new Dictionary<string, string> { ["ATTACK"] = "damage", ["SKILL"] = "block", ["POWER"] = "ritual" };

        // ------------------------------------------------------------------ zones
        private struct Zone { public double X0, Y0, X1, Y1, Cx, Cy, R; }
        private static Dictionary<string, Zone> zones;
        private static readonly Dictionary<int, Zone> artZones = new Dictionary<int, Zone>();

        private static Zone ReadZone(Dictionary<string, object> z)
        {
            double N(string k) => z.ContainsKey(k) ? Json.Num(z[k]) : 0;
            return new Zone { X0 = N("x0"), Y0 = N("y0"), X1 = N("x1"), Y1 = N("y1"), Cx = N("cx"), Cy = N("cy"), R = N("r") };
        }
        private static Zone Z(Art art, string name)
        {
            if (zones == null)
            {
                zones = new Dictionary<string, Zone>();
                foreach (var pair in Json.Obj(art.CardLayout.Get("zones"))) zones[pair.Key] = ReadZone(Json.Obj(pair.Value));
            }
            return zones[name];
        }
        private static Zone ArtZone(Art art, int rarity)
        {
            if (!artZones.TryGetValue(rarity, out var z))
            {
                var (x0, y0, x1, y1) = art.CardArtZone(rarity);
                artZones[rarity] = z = new Zone { X0 = x0, Y0 = y0, X1 = x1, Y1 = y1 };
            }
            return z;
        }
        private static IRect ZRect(Zone z, int w, int h) =>
            new IRect(MathX.Round(z.X0 * w), MathX.Round(z.Y0 * h), MathX.Round((z.X1 - z.X0) * w), MathX.Round((z.Y1 - z.Y0) * h));
        private static (int cx, int cy, int r) ZCircle(Zone z, int w, int h) => (MathX.Round(z.Cx * w), MathX.Round(z.Cy * h), Math.Max(2, MathX.Round(z.R * w)));

        // ------------------------------------------------------------------ text
        private static string FitText(string text, FontFace font, int maxW)
        {
            if (font.Width(text) <= maxW) return text;
            while (text.Length > 1 && font.Width(text + "…") > maxW) text = text.Substring(0, text.Length - 1);
            return text + "…";
        }

        public static string TypeOf(GameContext g, CardDefinition card)
        {
            var info = g.Data?.Cards.TryGetValue(card.Id, out var i) == true ? i : null;
            if (info != null) return info.Type;
            switch (card.Kind) { case CardKind.Power: return "POWER"; case CardKind.Status: return "STATUS"; case CardKind.Attack: return "ATTACK"; default: return "SKILL"; }
        }

        private static readonly (string key, EffectCondition cond, string active, string label)[] KeywordLines =
        {
            ("combo", EffectCondition.Combo, "¡Combo activo!", "Combo"), ("singular", EffectCondition.Singular, "¡Singular activo!", "Singular"),
            ("void", EffectCondition.Void, "¡Vacío activo!", "Vacío"), ("spoil", EffectCondition.Spoil, "¡Despojo activo!", "Despojo"),
            ("reaction", EffectCondition.Reaction, "¡Reacción!", "Reacción"), ("fury", EffectCondition.Fury, "¡Furia activa!", "Furia"),
            ("endure", EffectCondition.Endure, "¡Aguante activo!", "Aguante"),
        };

        /// <summary>_ability_lines: Spanish effect lines from the card data (effective values), at most 4.</summary>
        public static List<string> AbilityLines(GameContext g, CardDefinition card, BigInteger damage, long block, uint mask)
        {
            var lines = new List<string>();
            var info = g.Data?.Card(card);
            var type = TypeOf(g, card);
            var draw = CardTotals.Draw(card, mask); var mana = CardTotals.Mana(card, mask);
            if (type == "ATTACK" && damage > 0) lines.Add("Inflige " + Numbers.FormatInt(damage) + " de daño.");
            if (block > 0) lines.Add("Gana " + Numbers.FormatInt(block) + " de escudo.");
            if (draw > 0) lines.Add("Roba " + draw + " carta" + (draw > 1 ? "s" : "") + ".");
            if (mana > 0) lines.Add("+" + mana + " de maná.");
            var anyText = false;
            if (info?.Texts != null)
                foreach (var text in info.Texts)
                {
                    if (string.IsNullOrEmpty(text)) continue;
                    anyText = true;
                    lines.Add(char.ToUpperInvariant(text[0]) + text.Substring(1) + (text.EndsWith(".") ? "" : "."));
                }
            if (type == "POWER" && !anyText) lines.Add("Poder permanente.");
            foreach (var (key, cond, active, label) in KeywordLines)
            {
                if (info == null || !info.KeywordText.TryGetValue(key, out var kt)) continue;
                lines.Add((mask & CardTotals.Bit(cond)) != 0 ? active : label + ": " + kt + ".");
            }
            if (lines.Count > 4) lines.RemoveRange(4, lines.Count - 4);
            return lines;
        }

        // ------------------------------------------------------------------ face cache
        private struct FaceKey : IEquatable<FaceKey>
        {
            public string Id, Name; public int Rarity, ShownCost, PrintedCost, W, H; public BigInteger Damage; public long Block;
            public bool BoostD, BoostB, Affordable, Upgraded; public uint Mask; public ChromaKind Chroma;
            public bool Equals(FaceKey o) => Id == o.Id && Name == o.Name && Rarity == o.Rarity && ShownCost == o.ShownCost && PrintedCost == o.PrintedCost && W == o.W && H == o.H
                && Damage == o.Damage && Block == o.Block && BoostD == o.BoostD && BoostB == o.BoostB && Affordable == o.Affordable && Upgraded == o.Upgraded && Mask == o.Mask && Chroma == o.Chroma;
            public override bool Equals(object obj) => obj is FaceKey o && Equals(o);
            public override int GetHashCode()
            {
                unchecked
                {
                    var h = Id.GetHashCode() * 31 + Name.GetHashCode();
                    h = h * 31 + ShownCost; h = h * 31 + W; h = h * 31 + H * 7 + Rarity; h = h * 31 + Damage.GetHashCode(); h = h * 31 + Block.GetHashCode();
                    h = h * 31 + (int)Mask; h = h * 31 + (int)Chroma; h = h * 31 + (Affordable ? 1 : 0) + (BoostD ? 2 : 0) + (BoostB ? 4 : 0) + (Upgraded ? 8 : 0);
                    return h;
                }
            }
        }
        private sealed class FaceEntry { public FaceKey Key; public Tex Texture; public int LastFrame; public LinkedListNode<FaceEntry> Node; }
        private const int CacheMax = 96;
        private static readonly Dictionary<FaceKey, FaceEntry> faces = new Dictionary<FaceKey, FaceEntry>();
        private static readonly LinkedList<FaceEntry> lru = new LinkedList<FaceEntry>();
        private static readonly Dictionary<(int, int), Stack<Tex>> spareTargets = new Dictionary<(int, int), Stack<Tex>>();
        private static int targetSerial;

        public static int CachedFaces => faces.Count;

        private static Tex RentTarget(Canvas canvas, int w, int h)
        {
            if (spareTargets.TryGetValue((w, h), out var pool) && pool.Count > 0) return pool.Pop();
            return canvas.Backend.CreateTarget("card_face_" + targetSerial++, w, h);
        }
        private static void Evict(Canvas canvas)
        {
            var node = lru.First;
            while (faces.Count > CacheMax && node != null)
            {
                var next = node.Next;
                var e = node.Value;
                if (e.LastFrame != canvas.FrameIndex) // never recycle a surface already drawn this frame
                {
                    lru.Remove(node); faces.Remove(e.Key);
                    var size = (e.Texture.Width, e.Texture.Height);
                    if (!spareTargets.TryGetValue(size, out var pool)) spareTargets[size] = pool = new Stack<Tex>();
                    if (pool.Count < 8) pool.Push(e.Texture); else canvas.Backend.ReleaseTarget(e.Texture);
                }
                node = next;
            }
        }

        /// <summary>render_card_surface: the static face at (w, h), composed once and cached.</summary>
        public static Sprite Face(GameContext g, CardDefinition card, in CardShow show, int w = CardW, int h = CardH)
        {
            var canvas = g.Canvas;
            var mask = CardTotals.Normalize(card, show.Ready);
            var rawDamage = CardTotals.Damage(card, mask); var rawBlock = CardTotals.Block(card, mask);
            var damage = rawDamage > 0 ? rawDamage + show.BonusDamage : BigInteger.Zero;
            var block = rawBlock > 0 ? rawBlock + show.BonusBlock : 0;
            var comboDmg = mask != 0 && rawDamage > CardTotals.Damage(card);
            var comboBlk = mask != 0 && rawBlock > CardTotals.Block(card);
            var key = new FaceKey
            {
                Id = card.Id, Name = card.Name, Rarity = card.Rarity, ShownCost = show.Cost ?? card.Cost, PrintedCost = card.Cost, W = w, H = h, Damage = damage, Block = block,
                BoostD = show.BonusDamage > 0 || comboDmg, BoostB = show.BonusBlock > 0 || comboBlk, Affordable = !show.Unaffordable, Upgraded = card.Upgraded, Mask = mask, Chroma = card.Chroma,
            };
            if (faces.TryGetValue(key, out var hit))
            {
                hit.LastFrame = canvas.FrameIndex;
                lru.Remove(hit.Node); lru.AddLast(hit.Node);
                return new Sprite(hit.Texture);
            }
            var tex = RentTarget(canvas, w, h);
            canvas.BeginTarget(tex);
            Compose(g, card, key, mask);
            canvas.EndTarget();
            var entry = new FaceEntry { Key = key, Texture = tex, LastFrame = canvas.FrameIndex };
            entry.Node = lru.AddLast(entry);
            faces[key] = entry;
            if (faces.Count > CacheMax) Evict(canvas);
            return new Sprite(tex);
        }

        private static void Compose(GameContext g, CardDefinition card, FaceKey key, uint mask)
        {
            var canvas = g.Canvas; var art = g.Art; var fonts = g.Fonts; var data = g.Data;
            int w = key.W, h = key.H;
            var info = data?.Card(card);
            var type = TypeOf(g, card);
            if (!key.Affordable) canvas.PushTint(new Rgba(150, 150, 150));

            // 1. illustration window over a type backdrop
            var artRect = ZRect(ArtZone(art, card.Rarity), w, h).Inflate(4, 4);
            DrawTypeBackdrop(canvas, type, artRect);
            var ownArt = art.HasCardArt(card.Id);
            var badges = info?.Badges ?? Array.Empty<string>();
            if (badges.Length > 0 && !ownArt) DrawElementArt(canvas, art, badges, type, artRect);
            else DrawIllustration(canvas, art, card.Id, type, artRect);

            // 2. dark discs behind the see-through circles
            foreach (var name in new[] { "mana", "attack", "block" })
            {
                var (cx, cy, r) = ZCircle(Z(art, name), w, h);
                canvas.Circle(cx, cy, r + 1, Hole);
            }

            // 3. rarity frame (gilded for chromas)
            var frameKey = Json.Str(Json.Obj(Json.Obj(art.CardLayout.Get("frames")).Get(Art.RarityKey(card.Rarity))).Get("file"));
            if (card.Chroma != ChromaKind.None) frameKey += "_" + ChromaFx.Suffix(card.Chroma);
            canvas.BlitRect(new Sprite(art.Texture(frameKey)), new RectF(0, 0, w, h), Rgba.White);
            DrawElementChips(canvas, g, info, artRect.Inflate(-4, -4));

            // 4. text
            {
                var (cx, cy, r) = ZCircle(Z(art, "mana"), w, h);
                var manaCol = key.Affordable ? ManaInk : new Rgba(235, 80, 80);
                if (key.Affordable && key.ShownCost < card.Cost) manaCol = new Rgba(110, 235, 130);
                var font = fonts.Get(Math.Max(10, MathX.Round(r * 2.1)));
                var text = key.ShownCost.ToString();
                var tw = font.Width(text) + 2; var th = font.Height + 2;
                Txt.OutlinedCross(canvas, font, text, cx - tw / 2, cy - th / 2, manaCol);
            }
            {
                var nameRect = ZRect(Z(art, "name"), w, h);
                var size = Math.Max(8, MathX.Round(nameRect.H * 0.78));
                var font = fonts.Bold(size);
                while (size > 7 && font.Width(card.Name) > nameRect.W - 4) { size--; font = fonts.Bold(size); }
                if (font.Width(card.Name) > nameRect.W - 4) font = fonts.Tiny(8 * Math.Max(1, nameRect.H / 13));
                var text = FitText(card.Name, font, nameRect.W - 4);
                var tw = font.Width(text);
                font.Draw(canvas, text, nameRect.CenterX - tw / 2, nameRect.CenterY - font.Height / 2, InkDark);
            }
            var textRect = ZRect(Z(art, "text"), w, h);
            if (card.Chroma != ChromaKind.None)
            {
                var st = ChromaFx.Style(card.Chroma);
                var casts = card.CastCount;
                var label = st.Name.ToUpperInvariant() + (casts > 1 ? "  x" + casts : "");
                var font = fonts.Get(Math.Max(7, MathX.Round(h * 0.048)));
                var pw = font.Width(label) + 2; var ph = font.Height + 2;
                Txt.OutlinedCross(canvas, font, label, textRect.CenterX - pw / 2, textRect.Bottom - ph, st.Bright, st.Dark);
                textRect = new IRect(textRect.X, textRect.Y, textRect.W, Math.Max(1, textRect.H - ph));
            }
            var lines = AbilityLines(g, card, key.Damage, key.Block, mask);
            if (lines.Count > 0 && data != null)
            {
                var font = fonts.Bold(Math.Max(8, MathX.Round(h * 0.052)));
                var wrapped = new List<RichLine>();
                foreach (var raw in lines) wrapped.AddRange(RichText.Lines(data.Glossary, raw, font, textRect.W, InkLight));
                var lineH = font.LineSize;
                var maxLines = Math.Max(1, textRect.H / lineH);
                var truncated = wrapped.Count > maxLines;
                if (truncated) wrapped.RemoveRange(maxLines, wrapped.Count - maxLines);
                var y = textRect.CenterY - wrapped.Count * lineH / 2;
                for (var i = 0; i < wrapped.Count; i++)
                {
                    var line = wrapped[i];
                    if (truncated && i == wrapped.Count - 1)
                    {
                        var dotsW = font.Width("…");
                        var keep = Math.Min(line.Width, textRect.W - dotsW);
                        var jw = keep + dotsW; var x = textRect.CenterX - jw / 2;
                        canvas.PushClip(new RectF(x, y, keep, line.Height));
                        line.Draw(canvas, x, y);
                        canvas.PopClip();
                        font.Draw(canvas, "…", x + keep, y, InkLight);
                    }
                    else line.Draw(canvas, textRect.CenterX - line.Width / 2, y);
                    y += lineH;
                }
            }
            foreach (var (zone, isDamage) in new[] { ("attack", true), ("block", false) })
            {
                var value = isDamage ? key.Damage : new BigInteger(key.Block);
                if (value <= 0) continue;
                var (cx, cy, r) = ZCircle(Z(art, zone), w, h);
                var txt = Numbers.FormatInt(value);
                var size = Math.Max(8, MathX.Round(r * (txt.Length <= 2 ? 2.0 : txt.Length == 3 ? 1.5 : 1.2)));
                var font = fonts.Bold(size);
                var boosted = isDamage ? key.BoostD : key.BoostB;
                var tw = font.Width(txt) + 2; var th = font.Height + 2;
                Txt.OutlinedCross(canvas, font, txt, cx - tw / 2, cy - th / 2, boosted ? BonusInk : isDamage ? AtkInk : DefInk);
            }
            if (!key.Affordable) canvas.PopTint();
        }

        private static void DrawTypeBackdrop(Canvas canvas, string type, IRect r)
        {
            var (dark, light) = TypeBackdrop.TryGetValue(type, out var c) ? c : (new Rgba(30, 30, 40), new Rgba(70, 70, 90));
            const int bands = 6;
            for (var i = 0; i < bands; i++)
            {
                var t = i / (double)(bands - 1);
                var k = 1 - Math.Abs(t - 0.45) * 1.6;
                var col = new Rgba(MathX.Round(dark.R + (light.R - dark.R) * k), MathX.Round(dark.G + (light.G - dark.G) * k), MathX.Round(dark.B + (light.B - dark.B) * k));
                var y0 = MathX.Round(i * r.H / (double)bands);
                canvas.Fill(new RectF(r.X, r.Y + y0, r.W, MathX.Round((i + 1) * r.H / (double)bands) - y0), col);
            }
        }

        private static void DrawIllustration(Canvas canvas, Art art, string cardId, string type, IRect r)
        {
            if (art.HasCardArt(cardId))
            {
                var s = art.CardArt(cardId);
                var k = Math.Max(r.W / (double)s.W, r.H / (double)s.H);
                int bw = Math.Max(r.W, MathX.Round(s.W * k)), bh = Math.Max(r.H, MathX.Round(s.H * k));
                int ox = (bw - r.W) / 2, oy = (bh - r.H) / 2;
                canvas.PushClip(new RectF(r.X, r.Y, r.W, r.H));
                canvas.BlitRect(s, new RectF(r.X - ox, r.Y - oy, bw, bh), Rgba.White);
                canvas.PopClip();
                return;
            }
            var icon = art.CardPlaceholder(type);
            if (!icon.Valid) return;
            var scale = Math.Max(1, Math.Min(r.W / icon.W, r.H / icon.H));
            int iw = icon.W * scale, ih = icon.H * scale;
            canvas.BlitRect(icon, new RectF(r.CenterX - iw / 2, r.CenterY - ih / 2, iw, ih), Rgba.White);
        }

        private static void DrawIcon(Canvas canvas, Art art, string name, int scale, int cx, int cy)
        {
            var s = art.Icon(name); if (!s.Valid) return;
            int w = s.W * scale, h = s.H * scale;
            canvas.BlitRect(s, new RectF(cx - w / 2, cy - h / 2, w, h), Rgba.White);
        }

        private static void DrawElementArt(Canvas canvas, Art art, string[] names, string type, IRect r)
        {
            var key = "elbg_" + string.Join("_", names) + "_" + r.W + "x" + r.H;
            var tex = art.Procedural(key, r.W, r.H, () => ElementBackdropPixels(names, r.W, r.H));
            canvas.BlitRect(new Sprite(tex), r, Rgba.White);
            var big = Math.Max(1, r.H / 22);
            int cx = r.CenterX, cy = r.Y + MathX.Round(r.H * 0.36);
            if (names.Length == 1) DrawIcon(canvas, art, ElementIcon[names[0]], big, cx, cy);
            else if (names.Length == 2)
            {
                DrawIcon(canvas, art, ElementIcon[names[0]], big, cx - r.W / 8, cy);
                DrawIcon(canvas, art, ElementIcon[names[1]], Math.Max(1, big - 1), cx + r.W / 4, cy - r.H / 8);
            }
            else
            {
                var small = Math.Max(1, big - 1); var step = r.W / (names.Length + 1);
                for (var k = 0; k < names.Length; k++) DrawIcon(canvas, art, ElementIcon[names[k]], small, r.X + step * (k + 1), cy + (k % 2 == 1 ? 6 : -6));
            }
            var kind = art.Icon(TypeIcon.TryGetValue(type, out var ti) ? ti : "ritual");
            if (kind.Valid) canvas.Blit(kind, r.X + 5, r.Y + 5);
        }

        /// <summary>_element_backdrop: banded gradient per element (diagonal split) + its motif (seeded like the original).</summary>
        private static Rgba[] ElementBackdropPixels(string[] names, int w, int h)
        {
            var px = new Rgba[w * h];
            var n = names.Length; const int bands = 6;
            void FillRect(int x, int y, int fw, int fh, Rgba c)
            {
                for (var yy = Math.Max(0, y); yy < Math.Min(h, y + fh); yy++)
                    for (var xx = Math.Max(0, x); xx < Math.Min(w, x + fw); xx++) px[yy * w + xx] = c;
            }
            for (var y = 0; y < h; y++)
            {
                var band = Math.Min(bands - 1, y * bands / Math.Max(1, h));
                var k = 1 - Math.Abs(band / (double)(bands - 1) - 0.5) * 1.4;
                for (var part = 0; part < n; part++)
                {
                    var (dark, mid, _) = ElementBackdrop[names[part]];
                    var col = new Rgba(MathX.Round(dark.R + (mid.R - dark.R) * k), MathX.Round(dark.G + (mid.G - dark.G) * k), MathX.Round(dark.B + (mid.B - dark.B) * k));
                    var x0 = Math.Max(0, MathX.Round((2.0 * part / n - y / (double)Math.Max(1, h)) * w));
                    var x1 = part < n - 1 ? Math.Min(w, MathX.Round((2.0 * (part + 1) / n - y / (double)Math.Max(1, h)) * w)) : w;
                    if (x1 > x0) FillRect(x0, y, x1 - x0, 1, col);
                }
            }
            for (var part = 0; part < n; part++)
            {
                var name = names[part];
                var rng = new PyRandom(17 + part);
                var motif = ElementBackdrop[name].motif;
                var count = Math.Max(8, w * h / 260 / n);
                for (var i = 0; i < count; i++)
                {
                    int x = rng.RandRange(w), y = rng.RandRange(h);
                    if ((int)((x / (double)w + y / (double)h) / 2 * n) != part && n > 1) continue;
                    if (name == "Fuego") FillRect(x, y, 2, 2, motif);
                    else if (name == "Agua") { for (var d = 0; d < 7; d++) FillRect(x + d, y + (d == 2 || d == 3 ? 1 : 0), 1, 1, motif); }
                    else if (name == "Tierra") FillRect(x, y, 3, 2, motif);
                    else FillRect(x, y, 9, 1, motif);
                }
            }
            return px;
        }

        private static void DrawElementChips(Canvas canvas, GameContext g, CardInfo info, IRect artRect)
        {
            if (info?.Chips == null || info.Chips.Length == 0) return;
            var scale = artRect.H >= 70 ? 2 : 1;
            var sizes = new List<(int w, int h)>();
            foreach (var chip in info.Chips) sizes.Add(ChipSize(g, chip, scale));
            var total = 0; foreach (var s in sizes) total += s.w; total += 3 * (sizes.Count - 1);
            var x = artRect.CenterX - total / 2;
            for (var i = 0; i < info.Chips.Length; i++)
            {
                DrawChip(canvas, g, info.Chips[i], scale, x, artRect.Bottom - sizes[i].h - 1, sizes[i]);
                x += sizes[i].w + 3;
            }
        }

        private static (int w, int h) ChipSize(GameContext g, (string element, int stacks, bool all) chip, int scale)
        {
            var icon = g.Art.Icon(ElementIcon[chip.element]);
            var iw = icon.Valid ? icon.W * scale : 0;
            var numFont = g.Fonts.Get(scale >= 2 ? 16 : 8);
            int nw = numFont.Width("+" + chip.stacks) + 2, nh = numFont.Height + 2;
            var ew = chip.all && scale >= 2 ? g.Fonts.Get(8).Width("todos") + 2 : 0;
            return (iw + nw + (ew > 0 ? ew + 2 : 0) + 8, Math.Max(iw, nh) + 4);
        }

        private static void DrawChip(Canvas canvas, GameContext g, (string element, int stacks, bool all) chip, int scale, int x, int y, (int w, int h) size)
        {
            var ink = g.Data.Glossary.ElementColor.TryGetValue(chip.element, out var c) ? c : Rgba.White;
            var rect = new RectF(x, y, size.w, size.h);
            canvas.Rect(rect, new Rgba(14, 10, 18, 235), 0, 6);
            canvas.Rect(rect, ink, 2, 6);
            var icon = g.Art.Icon(ElementIcon[chip.element]);
            var px = x + 3;
            if (icon.Valid)
            {
                int iw = icon.W * scale, ih = icon.H * scale;
                canvas.BlitRect(icon, new RectF(px, y + (size.h - ih) / 2, iw, ih), Rgba.White);
                px += iw + 1;
            }
            var numFont = g.Fonts.Get(scale >= 2 ? 16 : 8);
            var num = "+" + chip.stacks; var nh = numFont.Height + 2;
            Txt.OutlinedCross(canvas, numFont, num, px, y + (size.h - nh) / 2, ink);
            px += numFont.Width(num) + 2 + 2;
            if (chip.all && scale >= 2)
            {
                var f8 = g.Fonts.Get(8); var eh = f8.Height + 2;
                Txt.OutlinedCross(canvas, f8, "todos", px, y + (size.h - eh) / 2 + 1, ink);
            }
        }

        // ------------------------------------------------------------------ public drawing
        /// <summary>Ready keyword layers of a card for these flags (card_fx.ready_keywords).</summary>
        public static uint ReadyMask(CardDefinition card, uint flags) => CardTotals.Normalize(card, flags);

        /// <summary>draw_card: at (x, y), lifted 20 px when hovered or selected; returns its rect.</summary>
        public static RectF Draw(Canvas canvas, GameContext g, CardDefinition card, float x, float y, in CardShow show, bool selected = false, bool hovered = false)
        {
            var lift = selected || hovered ? -20 : 0;
            var rect = new IRect((int)x, (int)y + lift, CardW, CardH);
            var body = Face(g, card, show);
            var t = g.Time;
            var ready = ReadyMask(card, show.Ready);
            if (ready != 0) CardFx.DrawReadyBack(canvas, g.Art, card.Rarity, rect.CenterX, rect.CenterY, CardW, CardH, ready, t, 0);
            if (card.Chroma != ChromaKind.None) ChromaFx.DrawCardAura(canvas, g.Art, card.Rarity, rect.CenterX, rect.CenterY, CardW, CardH, 0, ChromaFx.Style(card.Chroma).Glow, t);
            canvas.Blit(body, rect.X, rect.Y);
            if (card.Chroma != ChromaKind.None)
            {
                AnimateFace(canvas, body, rect, card.Chroma, t, 1f);
                ChromaFx.DrawMotes(canvas, g.Art, rect, card.Chroma, t);
            }
            if (ready != 0) CardFx.DrawReadyFront(canvas, g, rect.CenterX, rect.CenterY, CardW, CardH, ready, t, 0);
            if (selected || hovered)
                canvas.Rect(new IRect(rect.X - 2, rect.Y - 2, rect.W + 4, rect.H + 4), selected ? Colors.CardSelected : RarityColor(card.Rarity), 2, 10);
            return rect;
        }

        /// <summary>animate_card_face: sheen masked by the face and twinkles, over the blitted face.</summary>
        private static void AnimateFace(Canvas canvas, Sprite body, IRect rect, ChromaKind chroma, float t, float scale)
        {
            ChromaFx.Sheen(canvas, rect, chroma, t, body);
            ChromaFx.DrawTwinkles(canvas, rect, chroma, t, scale);
        }

        public const float ScaleStep = 0.05f;
        public static float QuantizeScale(float scale) => Math.Max(ScaleStep, MathX.Round(scale / ScaleStep) * ScaleStep);
        public static (int w, int h) CardSize(float scale) { var q = QuantizeScale(scale); return (MathX.Round(CardW * q), MathX.Round(CardH * q)); }

        /// <summary>draw_card_at: centred, scaled (5 % steps) and tilted; ``faceScale`` lays the face out once at
        /// that scale and resizes it smoothly while the card grows or shrinks.</summary>
        public static RectF DrawAt(Canvas canvas, GameContext g, CardDefinition card, float centerX, float centerY, in CardShow show,
            float scale = 1f, float angle = 0f, Rgba? outline = null, float? faceScale = null)
        {
            var (w, h) = CardSize(scale);
            var (fw, fh) = faceScale.HasValue ? CardSize(faceScale.Value) : (w, h);
            var body = Face(g, card, show, fw, fh);
            var tilt = MathX.Round(angle);
            int cx = MathX.Round(centerX), cy = MathX.Round(centerY);
            var t = g.Time;
            var ready = ReadyMask(card, show.Ready);
            if (ready != 0) CardFx.DrawReadyBack(canvas, g.Art, card.Rarity, cx, cy, w, h, ready, t, tilt);
            var rect = new IRect(cx - w / 2, cy - h / 2, w, h);
            if (card.Chroma != ChromaKind.None) ChromaFx.DrawCardAura(canvas, g.Art, card.Rarity, cx, cy, w, h, tilt, ChromaFx.Style(card.Chroma).Glow, t);
            if (tilt != 0) canvas.PushRotateScale(cx, cy, tilt, 1f);
            canvas.BlitRect(body, rect, Rgba.White);
            if (card.Chroma != ChromaKind.None) AnimateFace(canvas, body, rect, card.Chroma, t, Math.Max(0.5f, w / (float)CardW));
            if (tilt != 0) canvas.PopTransform();
            if (card.Chroma != ChromaKind.None) ChromaFx.DrawMotes(canvas, g.Art, rect, card.Chroma, t, 14, w / (float)CardW);
            if (outline.HasValue && tilt == 0) canvas.Rect(new IRect(rect.X - 3, rect.Y - 3, rect.W + 6, rect.H + 6), outline.Value, 3, 12);
            if (ready != 0) CardFx.DrawReadyFront(canvas, g, cx, cy, w, h, ready, t, tilt);
            return rect;
        }

        /// <summary>The face-down crystal back (card_back) at <paramref name="dst"/>.</summary>
        public static void DrawBack(Canvas canvas, Art art, RectF dst, Rgba? tint = null) => canvas.BlitRect(art.CardBack, dst, tint ?? Rgba.White);
    }
}
