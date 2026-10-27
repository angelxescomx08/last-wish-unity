using System;
using System.Collections.Generic;
using System.IO;

namespace LastWish.Presentation
{
    /// <summary>
    /// An animated card illustration (Resources/Game/Cards/Anim, written by Tools/VfxLab/export_game_card_art.py):
    /// a looping grid of lit colour frames, a soft halo per frame drawn additively (the game has no bloom) and live
    /// twinkles and motes. <see cref="CardWidget"/> draws it in the art window behind a face composed with that window
    /// left transparent, so the rarity frame always stays on top; static faces show <see cref="Still"/>.
    /// </summary>
    public sealed class CardArtAnimation
    {
        /// <summary>Strength of the additive halo (1 = as exported).</summary>
        public const float HaloGain = 0.5f;
        private const int TwinkleSlots = 3, MoteSlots = 4;
        private static readonly Rgba DimTint = new Rgba(150, 150, 150);

        public string Id { get; }
        public int Frames { get; }
        public float Fps { get; }
        /// <summary>The frame shown where the card is not animated (cached faces: deal flight, pack reveal, burn…).</summary>
        public int Still { get; }
        public Rgba Spark { get; }
        public Rgba Mote { get; }
        /// <summary>Seconds added to the clock, from the id: copies animate in step and a card rebuilt every frame never jumps.</summary>
        public float Phase { get; }
        public float Duration => Frames / Fps;
        public Tex Sheet { get; }
        public Tex HaloSheet { get; }
        private readonly int columns, pad, cellW, cellH, frameW, frameH, haloCellW, haloCellH, haloW, haloH;
        private readonly uint seed;

        public CardArtAnimation(string id, Dictionary<string, object> meta, Dictionary<string, object> card, Func<string, Tex> load)
        {
            Id = id;
            Frames = Math.Max(1, Json.Int(card.Get("frames"), 1));
            Fps = (float)Math.Max(1, Json.Num(meta.Get("fps"), 12));
            Still = MathX.Clamp(Json.Int(card.Get("still")), 0, Frames - 1);
            Spark = Color(card.Get("spark"), new Rgba(150, 240, 255));
            Mote = Color(card.Get("mote"), new Rgba(90, 200, 255));
            columns = Math.Max(1, Json.Int(meta.Get("columns"), 1));
            pad = Math.Max(0, Json.Int(meta.Get("pad")));
            (cellW, cellH) = Pair(meta.Get("cell")); (frameW, frameH) = Pair(meta.Get("frame"));
            (haloCellW, haloCellH) = Pair(meta.Get("halo_cell")); (haloW, haloH) = Pair(meta.Get("halo_frame"));
            seed = StableHash(id);
            Phase = seed % 997 / 997f * Duration;
            Sheet = load(CardArtAnimations.Folder + id);
            HaloSheet = load(CardArtAnimations.Folder + id + "_halo");
        }

        private static (int, int) Pair(object o) { var a = Json.Ints(o); return a != null && a.Length == 2 ? (Math.Max(1, a[0]), Math.Max(1, a[1])) : (1, 1); }
        private static Rgba Color(object o, Rgba fallback) { var a = Json.Ints(o); return a != null && a.Length >= 3 ? new Rgba(a[0], a[1], a[2]) : fallback; }

        /// <summary>FNV-1a of the id (string.GetHashCode is not stable between runs).</summary>
        public static uint StableHash(string text)
        {
            unchecked
            {
                var h = 2166136261u;
                foreach (var c in text ?? "") { h ^= c; h *= 16777619u; }
                return h;
            }
        }

        /// <summary>The frame shown at <paramref name="seconds"/> (any value: negative, huge or NaN).</summary>
        public int FrameAt(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return 0;
            var f = Math.Floor(seconds * Fps);
            var i = (int)(f - Math.Floor(f / Frames) * Frames);
            return i < 0 || i >= Frames ? 0 : i;
        }

        /// <summary>Colour frame <paramref name="index"/> (wraps), without its gutter.</summary>
        public Sprite Frame(int index)
        {
            index = ((index % Frames) + Frames) % Frames;
            return new Sprite(Sheet, index % columns * cellW + pad, index / columns * cellH + pad, frameW, frameH);
        }

        /// <summary>Halo frame <paramref name="index"/> (wraps): the same picture at native size.</summary>
        public Sprite Halo(int index)
        {
            index = ((index % Frames) + Frames) % Frames;
            return new Sprite(HaloSheet, index % columns * haloCellW + pad, index / columns * haloCellH + pad, haloW, haloH);
        }

        /// <summary>
        /// The illustration at <paramref name="time"/> covering <paramref name="window"/> (the art window with the margin
        /// the frame hides), with its halo and particles inside <paramref name="zone"/> (the visible window). Nothing is
        /// clipped: the picture overflows the window by a pixel or two at most, always under the opaque frame.
        /// </summary>
        public void Draw(Canvas canvas, IRect window, IRect zone, double time, bool dim)
        {
            if (window.W <= 0 || window.H <= 0) return;
            var t = time + Phase;
            var index = FrameAt(t);
            var k = Math.Max(window.W / (double)frameW, window.H / (double)frameH);
            int bw = Math.Max(window.W, MathX.Round(frameW * k)), bh = Math.Max(window.H, MathX.Round(frameH * k));
            var dst = new RectF(window.X - (bw - window.W) / 2, window.Y - (bh - window.H) / 2, bw, bh);
            canvas.BlitRect(Frame(index), dst, dim ? DimTint : Rgba.White);
            // additive light ignores the canvas alpha and tint (pygame BLEND_RGB_ADD), so fade and dim it by hand
            var light = MathX.Clamp01(canvas.Alpha) * (dim ? 0.35f : 1f);
            if (light > 0.01f)
            {
                var g = MathX.Round(255 * HaloGain * light);
                canvas.BlitRect(Halo(index), dst, new Rgba(g, g, g), Blend.Add);
                var unit = Math.Max(1, MathX.Round(window.W / (double)(frameW / 2)));   // one pixel of the native art
                Particles(canvas, zone, t, light, unit);
            }
        }

        // ------------------------------------------------------------------ live particles

        private static uint Hash(uint a, uint b, uint c)
        {
            unchecked
            {
                var h = a * 0x9E3779B1u ^ (b + 0x7F4A7C15u) * 0x85EBCA77u ^ (c + 0x165667B1u) * 0xC2B2AE3Du;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return h;
            }
        }
        private static float Unit(uint h, int shift) => ((h >> shift) & 1023) / 1023f;
        private static Rgba Light(Rgba c, float k) => new Rgba(MathX.Round(Math.Min(255, c.R * k)), MathX.Round(Math.Min(255, c.G * k)), MathX.Round(Math.Min(255, c.B * k)));

        /// <summary>Twinkling four-point stars and motes drifting up, deterministic in (id, slot, cycle).</summary>
        private void Particles(Canvas canvas, IRect zone, double t, float light, int unit)
        {
            var margin = 3 * unit;
            float x0 = zone.X + margin, y0 = zone.Y + margin, w = Math.Max(1, zone.W - 2 * margin), h = Math.Max(1, zone.H - 2 * margin);
            var white = Rgba.Lerp(Spark, Rgba.White, 0.65);
            for (var slot = 0; slot < TwinkleSlots; slot++)
            {
                var period = 1.25 + 0.4 * slot;
                var clock = t / period + slot * 0.37;
                var cycle = Math.Floor(clock); var u = clock - cycle;
                if (u >= 0.55) continue;                                     // dark part of the cycle
                var env = (float)Math.Pow(Math.Sin(Math.PI * u / 0.55), 1.5) * light;
                var r = Hash(seed, (uint)slot, (uint)(long)cycle);
                var x = (float)Math.Round(x0 + Unit(r, 0) * Math.Max(0, w - unit)); var y = (float)Math.Round(y0 + Unit(r, 10) * h * 0.75f);
                var arm = unit * (1 + MathX.Round(env * 2));
                var edge = Light(Spark, 0.75f * env);
                canvas.Fill(new RectF(x - arm, y, arm * 2 + unit, unit), edge, Blend.Add);
                canvas.Fill(new RectF(x, y - arm, unit, arm * 2 + unit), edge, Blend.Add);
                canvas.Fill(new RectF(x, y, unit, unit), Light(white, env), Blend.Add);
            }
            for (var slot = 0; slot < MoteSlots; slot++)
            {
                var period = 2.1 + 0.35 * slot;
                var clock = t / period + slot * 0.29;
                var cycle = Math.Floor(clock); var u = (float)(clock - cycle);
                var env = (float)Math.Sin(Math.PI * u) * light * 0.9f;
                var r = Hash(seed ^ 0xA5A5u, (uint)slot, (uint)(long)cycle);
                var sway = (float)Math.Sin((u * 2 + Unit(r, 20)) * Math.PI * 2) * 2 * unit;
                var x = (float)Math.Round(x0 + Unit(r, 0) * w + sway);
                var y = (float)Math.Round(y0 + h * (0.55f + 0.4f * Unit(r, 10)) - u * h * 0.5f);
                canvas.Fill(new RectF(x, y, unit, unit), Light(Mote, env), Blend.Add);
            }
        }
    }

    /// <summary>The animated illustrations listed in Cards/Anim/anim.json, loaded once per <see cref="Art"/>.</summary>
    public static class CardArtAnimations
    {
        public const string Folder = "Cards/Anim/";
        private static Art loadedFor;
        private static readonly Dictionary<string, CardArtAnimation> byId = new Dictionary<string, CardArtAnimation>();

        /// <summary>The animation of <paramref name="cardId"/>, or null when the card has a still illustration.</summary>
        public static CardArtAnimation For(Art art, string cardId)
        {
            if (art == null || cardId == null) return null;
            Load(art);
            return byId.TryGetValue(cardId, out var anim) ? anim : null;
        }

        public static IReadOnlyCollection<string> Ids(Art art) { Load(art); return byId.Keys; }

        private static void Load(Art art)
        {
            if (ReferenceEquals(art, loadedFor)) return;
            loadedFor = art; byId.Clear();
            Dictionary<string, object> meta;
            try { meta = Json.Obj(Json.Parse(art.ReadText(Folder + "anim"))); }
            catch (Exception e) when (e is InvalidOperationException || e is IOException) { return; }   // no animated art
            var cards = Json.Obj(meta.Get("cards"));
            if (cards == null) return;
            foreach (var pair in cards)
            {
                var card = Json.Obj(pair.Value);
                if (card != null) byId[pair.Key] = new CardArtAnimation(pair.Key, meta, card, art.Texture);
            }
        }
    }
}
