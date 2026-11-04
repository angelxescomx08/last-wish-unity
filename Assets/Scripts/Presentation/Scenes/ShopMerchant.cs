using System;
using System.Collections.Generic;
using System.Linq;

namespace LastWish.Presentation
{
    /// <summary>What the shopkeeper reacts to.</summary>
    public enum MerchantCue { Enter, Idle, HoverRelic, HoverPack, HoverGolden, HoverPoor, BuyRelic, BuyPack, BuyGolden, BuyFree, Poor, Sold, SoldOut }

    /// <summary>The shopkeeper's lines (on screen, so in Spanish). <c>{item}</c> is the offer's name, <c>{short}</c> the gold
    /// that is missing. Lines never refer to the item with a gendered word, so any relic or pack name fits.</summary>
    public static class MerchantLines
    {
        public static readonly IReadOnlyDictionary<MerchantCue, string[]> All = new Dictionary<MerchantCue, string[]>
        {
            [MerchantCue.Enter] = new[]
            {
                "¡Bienvenido, viajero! Todo lo que ves está a la venta.", "Ah, un cliente. Las criptas son malas para el negocio.",
                "Pasa, pasa. Mis precios son casi honrados.", "Mira sin miedo: mirar todavía es gratis.",
                "¿Sigues vivo? Entonces puedes comprar algo.",
            },
            [MerchantCue.Idle] = new[]
            {
                "Este farol no se apaga ni con la lluvia de la cripta.", "Llevo tres pisos cargando esta mochila. Compra algo y aligérame.",
                "Los muertos no compran nada. Tú sí, ¿verdad?", "Una vez le vendí una espada a un esqueleto. Bueno... se la llevó.",
                "No regateo. Bueno, casi nunca.", "¿Te decides o le pongo precio a tu indecisión?",
            },
            [MerchantCue.HoverRelic] = new[]
            {
                "¿{item}? Tienes buen ojo.", "Esa la saqué de un cofre que respiraba.", "Tiene historia. Y precio.",
                "Con eso las criptas te temerán un poco más.",
            },
            [MerchantCue.HoverPack] = new[] { "Cinco cartas y una sorpresa.", "Los sobres nunca mienten. Casi nunca.", "¿{item}? Ábrelo y elige con calma." },
            [MerchantCue.HoverGolden] = new[] { "¡Esa brilla! Efecto doble, placer doble.", "Oro de verdad. Lo mordí para comprobarlo." },
            [MerchantCue.HoverPoor] = new[] { "Te faltan {short} monedas. Puedo esperar.", "Mirar es gratis. Comprar ya es otra historia." },
            [MerchantCue.BuyRelic] = new[]
            {
                "¡Trato hecho! Cuídala bien.", "¡{item}! No acepto devoluciones.", "Excelente elección. Mi bolsa te lo agradece.",
                "Que te dé suerte allá abajo.",
            },
            [MerchantCue.BuyPack] = new[] { "¡Ábrelo! A ver qué te toca.", "Que salga algo bueno...", "Cinco cartas, un destino." },
            [MerchantCue.BuyGolden] = new[] { "¡La dorada! Esa la voy a extrañar.", "Brilla más en tus manos que en las mías." },
            [MerchantCue.BuyFree] = new[] { "Con ese vale... un trato es un trato.", "Gratis. Me duele, pero cumplo mi palabra." },
            [MerchantCue.Poor] = new[]
            {
                "Sin oro no hay trato, amigo.", "Te faltan {short} de oro. Vuelve con la bolsa llena.", "Mi generosidad tiene un límite: el precio.",
            },
            [MerchantCue.Sold] = new[] { "Ya lo vendí. Ni yo tengo dos.", "Agotado. Haber madrugado." },
            [MerchantCue.SoldOut] = new[] { "¡Me dejaste sin nada! Hasta la próxima, viajero." },
        };

        public static string Fill(string line, string item, int shortBy) =>
            line.Replace("{item}", item ?? "Eso").Replace("{short}", shortBy.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " "));
    }

    /// <summary>
    /// What the shopkeeper says and when (pure state, ticked by the shop): a line appears letter by letter, stays a few
    /// seconds and fades. Purchases, refusals and greetings always speak; hovering an offer only sometimes (never two lines
    /// within <see cref="HoverGap"/> seconds, never twice for the same offer in a row); after a quiet spell he chats.
    /// The same line never comes back while it is one of the last three said.
    /// </summary>
    public sealed class MerchantSpeech
    {
        public const float CharsPerSecond = 40f, Hold = 4.2f, Fade = 0.3f, HoverGap = 5f, HoverChance = 0.45f;
        private readonly Rng rng;
        private readonly List<string> recent = new List<string>();
        private string hoverKey;
        private float idleAfter;
        public string Text { get; private set; }
        public MerchantCue Cue { get; private set; }
        public float Age { get; private set; }
        /// <summary>Seconds since the last line started.</summary>
        public float Quiet { get; private set; }
        public int Lines { get; private set; }

        public MerchantSpeech(int seed) { rng = new Rng(seed); idleAfter = NextIdle(); }

        private float NextIdle() => (float)rng.Uniform(12, 19);
        public float ShowFor => Text == null ? 0 : Text.Length / CharsPerSecond + Hold;
        public bool Visible => Text != null && Age < ShowFor + Fade;
        public float Alpha => Text == null ? 0 : Age < ShowFor ? 1f : Math.Max(0f, 1f - (Age - ShowFor) / Fade);
        /// <summary>Letters shown so far (the line is typed out).</summary>
        public int Shown => Text == null ? 0 : Math.Min(Text.Length, (int)(Age * CharsPerSecond));
        public bool Talking => Text != null && Shown < Text.Length;
        /// <summary>The animation a line starts with: he tips his hat when you walk in, bows after a sale (and when you empty
        /// the shop), shakes his head at a refusal; other lines have none (he just talks).</summary>
        public static string ReactionOf(MerchantCue cue)
        {
            switch (cue)
            {
                case MerchantCue.Enter: return "greet";
                case MerchantCue.BuyRelic: case MerchantCue.BuyPack: case MerchantCue.BuyGolden: case MerchantCue.BuyFree: case MerchantCue.SoldOut: return "thanks";
                case MerchantCue.Poor: case MerchantCue.Sold: return "no";
                default: return null;
            }
        }

        /// <summary>Say a line of <paramref name="cue"/> (always, replacing what he was saying).</summary>
        public string Say(MerchantCue cue, string item = null, int shortBy = 0)
        {
            var lines = MerchantLines.All[cue];
            var fresh = lines.Where(l => !recent.Contains(l)).ToList();
            var pool = fresh.Count > 0 ? fresh : lines.ToList();
            var line = pool[Math.Min(pool.Count - 1, (int)(rng.Random() * pool.Count))];
            recent.Add(line); if (recent.Count > 3) recent.RemoveAt(0);
            Text = MerchantLines.Fill(line, item, shortBy); Cue = cue; Age = 0; Quiet = 0; Lines++;
            idleAfter = NextIdle();
            return Text;
        }

        /// <summary>Maybe comment on the offer under the mouse (<paramref name="key"/> identifies it).</summary>
        public bool Hover(string key, MerchantCue cue, string item, int shortBy = 0)
        {
            if (key == hoverKey) return false;
            hoverKey = key;
            if (Quiet < HoverGap || rng.Random() > HoverChance) return false;
            Say(cue, item, shortBy);
            return true;
        }

        public void Update(float dt)
        {
            dt = Math.Max(0f, Math.Min(0.1f, dt));
            Age += dt; Quiet += dt;
            if (!Visible && Quiet >= idleAfter) Say(MerchantCue.Idle);
        }
    }

    /// <summary>
    /// Which animation of El Mercader plays (pure state, ticked by the shop with his speech). Each new line starts its
    /// reaction once (greet, thanks, no), which plays to its end unless another line interrupts it; while a line is being
    /// typed he talks, and the talk loop finishes its cycle before he goes back to idle; when he has been idle a while he
    /// flips a coin. Every clip starts and ends on the idle's first frame, so switching never jumps.
    /// </summary>
    public sealed class MerchantPose
    {
        public const float FidgetMin = 7f, FidgetMax = 12f;
        private readonly Rng rng;
        private int line = -1;
        private float idleFor, fidgetAfter;
        public string Anim { get; private set; } = "idle";
        public float Elapsed { get; private set; }

        public MerchantPose(int seed) { rng = new Rng(seed); fidgetAfter = NextFidget(); }

        private float NextFidget() => (float)rng.Uniform(FidgetMin, FidgetMax);

        private void Play(string anim) { Anim = anim; Elapsed = 0; idleFor = 0; }

        public void Update(float dt, MerchantSpeech speech, MerchantSheet sheet)
        {
            dt = Math.Max(0f, Math.Min(0.1f, dt));
            Elapsed += dt;
            var want = speech.Talking && sheet.Clips.ContainsKey("talk") ? "talk" : "idle";
            if (speech.Lines != line)
            {
                line = speech.Lines;
                var reaction = MerchantSpeech.ReactionOf(speech.Cue);
                if (reaction != null && sheet.Clips.ContainsKey(reaction)) { Play(reaction); return; }
                if (Anim == "coins") { Play(want); return; }       // a new line ends the fidget
            }
            if (!sheet.Clips.TryGetValue(Anim, out var clip)) { Play("idle"); return; }
            if (!clip.Loop)
            {
                if (Elapsed < clip.Total) return;                   // reactions and the coin play to their end
                Play(want); return;
            }
            if (Anim == want)
            {
                if (Anim == "idle" && sheet.Clips.ContainsKey("coins") && (idleFor += dt) >= fidgetAfter) { Play("coins"); fidgetAfter = NextFidget(); }
                return;
            }
            if (Anim == "talk")
            {
                // finish the talking cycle (it ends where the idle begins)
                if (Math.Floor(Elapsed / clip.Total) > Math.Floor((Elapsed - dt) / clip.Total) || clip.Total <= 0) { var over = clip.Total > 0 ? (float)MathX.PyMod(Elapsed, clip.Total) : 0f; Play(want); Elapsed = Math.Min(over, dt); }
                return;
            }
            Play(want);
        }
    }

    /// <summary>
    /// El Mercader's sheet (Tools/VfxLab/merchant.html → export_merchant_art.py → Resources/Game/Shop): one row per animation,
    /// 96 px cells shown ×2 with nearest filtering, the halo added on top, the anchors of the speech bubble and the lantern.
    /// Missing animations fall back to "idle", so the shop works with only the approved pose.
    /// </summary>
    public sealed class MerchantSheet
    {
        public sealed class Clip { public int Row, Frames; public bool Loop; public float[] Durations; public float Total; }
        public int Cell { get; private set; } = 96;
        public int Scale { get; private set; } = 2;
        public float HaloGain { get; private set; } = 0.55f;
        public (float x, float y) Mouth { get; private set; } = (60, 45);
        public (float x, float y) Lantern { get; private set; } = (18, 61);
        public int Top { get; private set; } = 23;
        public int GroundY { get; private set; } = 94;
        public (float x, float w) Feet { get; private set; } = (51, 18);
        public readonly Dictionary<string, Clip> Clips = new Dictionary<string, Clip>();
        private Tex sheet, halo;

        public static MerchantSheet Load(Art art)
        {
            var s = new MerchantSheet();
            try
            {
                var meta = Json.Obj(Json.Parse(art.ReadText("Shop/merchant")));
                s.Cell = Json.Int(meta.Get("cell"), 96); s.Scale = Json.Int(meta.Get("scale"), 2);
                foreach (var pair in Json.Obj(meta.Get("animations")))
                {
                    var a = Json.Obj(pair.Value); var ds = Json.Ints(a.Get("durations_ms")) ?? new[] { 83 };
                    var clip = new Clip { Row = Json.Int(a.Get("row")), Frames = Math.Max(1, Json.Int(a.Get("frames"), 1)), Loop = a.Get("loop") is bool b && b, Durations = ds.Select(d => d / 1000f).ToArray() };
                    clip.Total = clip.Durations.Sum();
                    s.Clips[pair.Key] = clip;
                }
                var anchors = Json.Obj(meta.Get("anchors"));
                var mouth = Json.Nums(anchors.Get("mouth")); if (mouth != null) s.Mouth = ((float)mouth[0], (float)mouth[1]);
                var lantern = Json.Nums(anchors.Get("lantern")); if (lantern != null) s.Lantern = ((float)lantern[0], (float)lantern[1]);
                s.Top = Json.Int(anchors.Get("top"), s.Top);
                var ground = Json.Obj(meta.Get("ground"));
                s.GroundY = Json.Int(ground.Get("y"), s.GroundY);
                var idle = Json.Arr(Json.Obj(ground.Get("frames"))?.Get("idle"));
                var f0 = idle != null && idle.Count > 0 ? Json.Nums(idle[0]) : null; if (f0 != null) s.Feet = ((float)f0[0], (float)f0[1]);
                var h = Json.Obj(meta.Get("halo")); if (h != null) s.HaloGain = (float)Json.Num(h.Get("gain"), 0.55);
                s.sheet = art.Texture(Json.Str(meta.Get("sheet")) ?? "Shop/merchant_96");
                s.halo = art.Texture(Json.Str(h?.Get("sheet")) ?? "Shop/merchant_halo_96");
            }
            catch (Exception) { s.sheet = null; }
            return s;
        }

        public bool Valid => sheet != null && Clips.Count > 0;

        /// <summary>The clip that plays for <paramref name="mood"/> (idle when the sheet has no such animation).</summary>
        public string Resolve(string mood) => mood != null && Clips.ContainsKey(mood) ? mood : "idle";

        public int FrameAt(string anim, float elapsed)
        {
            if (!Clips.TryGetValue(Resolve(anim), out var c) || c.Frames <= 1 || c.Total <= 0) return 0;
            var t = c.Loop ? (float)MathX.PyMod(elapsed, c.Total) : Math.Min(elapsed, c.Total - 0.0001f);
            for (var i = 0; i < c.Frames; i++) { if (t < c.Durations[Math.Min(i, c.Durations.Length - 1)]) return i; t -= c.Durations[Math.Min(i, c.Durations.Length - 1)]; }
            return c.Frames - 1;
        }

        /// <summary>The sprite's rectangle on screen when his feet stand at (<paramref name="x"/>, <paramref name="groundY"/>).</summary>
        public RectF Rect(float x, float groundY) => new RectF(x - Feet.x * Scale, groundY - GroundY * Scale, Cell * Scale, Cell * Scale);
        public (float x, float y) ToScreen(RectF rect, (float x, float y) p) => (rect.X + p.x * Scale, rect.Y + p.y * Scale);

        public void Draw(Canvas canvas, string anim, float elapsed, RectF rect, float haloK = 1f)
        {
            if (!Valid) return;
            var name = Resolve(anim); var c = Clips[name]; var f = FrameAt(name, elapsed);
            var src = new Sprite(sheet, f * Cell, c.Row * Cell, Cell, Cell);
            canvas.BlitRect(src, rect, Rgba.White);
            if (halo == null) return;
            var v = MathX.Round(255 * MathX.Clamp01(canvas.Alpha) * HaloGain * haloK);
            if (v > 2) canvas.BlitRect(new Sprite(halo, f * Cell, c.Row * Cell, Cell, Cell), rect, new Rgba(v, v, v), Blend.Add);
        }
    }
}
