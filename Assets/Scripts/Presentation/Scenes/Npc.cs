using System;
using System.Collections.Generic;
using System.Linq;

namespace LastWish.Presentation
{
    /// <summary>
    /// A room character's sheet (El Mercader, El Brujo: Tools/VfxLab/*.html → export_*_art.py → Resources/Game/…): one row
    /// per animation, 96 px cells shown ×2 with nearest filtering, the halo added on top, the named anchors the room uses
    /// (mouth for the speech bubble, a lantern or a staff's flame…), the events of the actions (a cast's release frame and
    /// point) and where the feet stand. Missing animations fall back to "idle", so a room works with only the rest pose.
    /// </summary>
    public sealed class NpcSheet
    {
        public sealed class Clip { public int Row, Frames; public bool Loop; public float[] Durations; public float Total; }
        public int Cell { get; private set; } = 96;
        public int Scale { get; private set; } = 2;
        public float HaloGain { get; private set; } = 0.55f;
        public (float x, float y) Mouth { get; private set; } = (60, 45);
        public int Top { get; private set; } = 23;
        public int GroundY { get; private set; } = 94;
        public (float x, float w) Feet { get; private set; } = (51, 18);
        public readonly Dictionary<string, Clip> Clips = new Dictionary<string, Clip>();
        public readonly Dictionary<string, (float x, float y)> Anchors = new Dictionary<string, (float x, float y)>();
        public readonly Dictionary<string, (int frame, (float x, float y) point)> Events = new Dictionary<string, (int, (float, float))>();
        private Tex sheet, halo;

        /// <summary>Load <paramref name="meta"/> (e.g. "Shop/merchant"); the sheet and halo textures come from its JSON.</summary>
        public static NpcSheet Load(Art art, string meta)
        {
            var s = new NpcSheet();
            try
            {
                var root = Json.Obj(Json.Parse(art.ReadText(meta)));
                s.Cell = Json.Int(root.Get("cell"), 96); s.Scale = Json.Int(root.Get("scale"), 2);
                foreach (var pair in Json.Obj(root.Get("animations")))
                {
                    var a = Json.Obj(pair.Value); var ds = Json.Ints(a.Get("durations_ms")) ?? new[] { 83 };
                    var clip = new Clip { Row = Json.Int(a.Get("row")), Frames = Math.Max(1, Json.Int(a.Get("frames"), 1)), Loop = a.Get("loop") is bool b && b, Durations = ds.Select(d => d / 1000f).ToArray() };
                    clip.Total = clip.Durations.Sum();
                    s.Clips[pair.Key] = clip;
                }
                var anchors = Json.Obj(root.Get("anchors"));
                if (anchors != null)
                    foreach (var pair in anchors)
                    {
                        var v = Json.Nums(pair.Value);
                        if (v != null && v.Length >= 2) s.Anchors[pair.Key] = ((float)v[0], (float)v[1]);
                    }
                if (s.Anchors.TryGetValue("mouth", out var mouth)) s.Mouth = mouth;
                s.Top = Json.Int(anchors?.Get("top"), s.Top);
                var events = Json.Obj(root.Get("events"));
                if (events != null)
                    foreach (var pair in events)
                    {
                        var e = Json.Obj(pair.Value); var p = Json.Nums(e?.Get("point"));
                        s.Events[pair.Key] = (Json.Int(e?.Get("frame")), p != null && p.Length >= 2 ? ((float)p[0], (float)p[1]) : (0f, 0f));
                    }
                var ground = Json.Obj(root.Get("ground"));
                s.GroundY = Json.Int(ground.Get("y"), s.GroundY);
                var idle = Json.Arr(Json.Obj(ground.Get("frames"))?.Get("idle"));
                var f0 = idle != null && idle.Count > 0 ? Json.Nums(idle[0]) : null; if (f0 != null) s.Feet = ((float)f0[0], (float)f0[1]);
                var h = Json.Obj(root.Get("halo")); if (h != null) s.HaloGain = (float)Json.Num(h.Get("gain"), 0.55);
                s.sheet = art.Texture(Json.Str(root.Get("sheet")) ?? meta + "_96");
                s.halo = art.Texture(Json.Str(h?.Get("sheet")) ?? meta + "_halo_96");
            }
            catch (Exception) { s.sheet = null; }
            return s;
        }

        public bool Valid => sheet != null && Clips.Count > 0;

        /// <summary>A named anchor in cell pixels (<paramref name="fallback"/> when the sheet has none).</summary>
        public (float x, float y) Anchor(string name, (float x, float y) fallback = default) => Anchors.TryGetValue(name, out var a) ? a : fallback;

        /// <summary>The clip that plays for <paramref name="mood"/> (idle when the sheet has no such animation).</summary>
        public string Resolve(string mood) => mood != null && Clips.ContainsKey(mood) ? mood : "idle";

        /// <summary>Seconds from the start of <paramref name="anim"/> to the start of frame <paramref name="frame"/>.</summary>
        public float TimeOf(string anim, int frame)
        {
            if (!Clips.TryGetValue(Resolve(anim), out var c)) return 0;
            var t = 0f; for (var i = 0; i < Math.Min(frame, c.Frames); i++) t += c.Durations[Math.Min(i, c.Durations.Length - 1)];
            return t;
        }

        public int FrameAt(string anim, float elapsed)
        {
            if (!Clips.TryGetValue(Resolve(anim), out var c) || c.Frames <= 1 || c.Total <= 0) return 0;
            var t = c.Loop ? (float)MathX.PyMod(elapsed, c.Total) : Math.Min(elapsed, c.Total - 0.0001f);
            for (var i = 0; i < c.Frames; i++) { if (t < c.Durations[Math.Min(i, c.Durations.Length - 1)]) return i; t -= c.Durations[Math.Min(i, c.Durations.Length - 1)]; }
            return c.Frames - 1;
        }

        /// <summary>The sprite's rectangle on screen when its feet stand at (<paramref name="x"/>, <paramref name="groundY"/>).</summary>
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

    /// <summary>
    /// What a room character says and when (pure state, ticked by the room): a line appears letter by letter, stays a few
    /// seconds and fades. Cues the room raises always speak; hovering something only sometimes (never two lines within
    /// <see cref="HoverGap"/> seconds, never twice for the same thing in a row); after a quiet spell he chats. The same line
    /// never comes back while it is one of the last three said. <typeparamref name="TCue"/> is the character's own enum.
    /// </summary>
    public class NpcSpeech<TCue> where TCue : struct
    {
        public const float CharsPerSecond = 40f, Hold = 4.2f, Fade = 0.3f, HoverGap = 5f, HoverChance = 0.45f;
        private readonly Rng rng;
        private readonly IReadOnlyDictionary<TCue, string[]> lines;
        private readonly Func<string, string, int, string> fill;
        private readonly TCue idleCue;
        private readonly List<string> recent = new List<string>();
        private string hoverKey;
        private float idleAfter;
        public string Text { get; private set; }
        public TCue Cue { get; private set; }
        public float Age { get; private set; }
        /// <summary>Seconds since the last line started.</summary>
        public float Quiet { get; private set; }
        public int Lines { get; private set; }

        protected NpcSpeech(int seed, IReadOnlyDictionary<TCue, string[]> lines, TCue idleCue, Func<string, string, int, string> fill)
        { rng = new Rng(seed); this.lines = lines; this.idleCue = idleCue; this.fill = fill; idleAfter = NextIdle(); }

        private float NextIdle() => (float)rng.Uniform(12, 19);
        public float ShowFor => Text == null ? 0 : Text.Length / CharsPerSecond + Hold;
        public bool Visible => Text != null && Age < ShowFor + Fade;
        public float Alpha => Text == null ? 0 : Age < ShowFor ? 1f : Math.Max(0f, 1f - (Age - ShowFor) / Fade);
        /// <summary>Letters shown so far (the line is typed out).</summary>
        public int Shown => Text == null ? 0 : Math.Min(Text.Length, (int)(Age * CharsPerSecond));
        public bool Talking => Text != null && Shown < Text.Length;

        /// <summary>Say a line of <paramref name="cue"/> (always, replacing what he was saying).</summary>
        public string Say(TCue cue, string item = null, int shortBy = 0)
        {
            var all = lines[cue];
            var fresh = all.Where(l => !recent.Contains(l)).ToList();
            var pool = fresh.Count > 0 ? fresh : all.ToList();
            var line = pool[Math.Min(pool.Count - 1, (int)(rng.Random() * pool.Count))];
            recent.Add(line); if (recent.Count > 3) recent.RemoveAt(0);
            Text = fill(line, item, shortBy); Cue = cue; Age = 0; Quiet = 0; Lines++;
            idleAfter = NextIdle();
            return Text;
        }

        /// <summary>Maybe comment on what is under the mouse (<paramref name="key"/> identifies it).</summary>
        public bool Hover(string key, TCue cue, string item, int shortBy = 0)
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
            if (!Visible && Quiet >= idleAfter) Say(idleCue);
        }
    }

    /// <summary>
    /// Which animation of a room character plays (pure state, ticked by the room with his speech). Each new line starts its
    /// reaction once, which plays to its end unless another line interrupts it; while a line is being typed he talks, and the
    /// talk loop finishes its cycle before he goes back to idle; when he has been idle a while he plays his fidget. Every clip
    /// starts and ends on the idle's first frame, so switching never jumps. <see cref="Play"/> starts an action the room asks
    /// for (the warlock's cast) the same way.
    /// </summary>
    public class NpcPose
    {
        public const float FidgetMin = 7f, FidgetMax = 12f;
        private readonly Rng rng;
        private readonly string fidget;
        private int line = -1;
        private float idleFor, fidgetAfter;
        public string Anim { get; private set; } = "idle";
        public float Elapsed { get; private set; }

        public NpcPose(int seed, string fidget) { rng = new Rng(seed); this.fidget = fidget; fidgetAfter = NextFidget(); }

        private float NextFidget() => (float)rng.Uniform(FidgetMin, FidgetMax);

        /// <summary>Start <paramref name="anim"/> from its first frame.</summary>
        public void Play(string anim) { Anim = anim; Elapsed = 0; idleFor = 0; }

        /// <summary>One tick: <paramref name="lines"/> counts the lines said so far, <paramref name="reaction"/> is the
        /// animation the latest line starts (or null), <paramref name="talking"/> whether it is still being typed.</summary>
        public void Update(float dt, int lines, string reaction, bool talking, NpcSheet sheet)
        {
            dt = Math.Max(0f, Math.Min(0.1f, dt));
            Elapsed += dt;
            var want = talking && sheet.Clips.ContainsKey("talk") ? "talk" : "idle";
            if (lines != line)
            {
                line = lines;
                if (reaction != null && sheet.Clips.ContainsKey(reaction)) { Play(reaction); return; }
                if (Anim == fidget) { Play(want); return; }        // a new line ends the fidget
            }
            if (!sheet.Clips.TryGetValue(Anim, out var clip)) { Play("idle"); return; }
            if (!clip.Loop)
            {
                if (Elapsed < clip.Total) return;                   // reactions and the fidget play to their end
                Play(want); return;
            }
            if (Anim == want)
            {
                if (Anim == "idle" && fidget != null && sheet.Clips.ContainsKey(fidget) && (idleFor += dt) >= fidgetAfter) { Play(fidget); fidgetAfter = NextFidget(); }
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
    /// The paper speech bubble of a room character: crème paper with an ink edge, a tail leaning towards his face, the line
    /// typed out letter by letter (shared by El Mercader and El Brujo).
    /// </summary>
    public static class SpeechBubble
    {
        public const int Line = 20;
        public sealed class View { public RectF Box; public List<string> Lines = new List<string>(); public (float x, float y) Tip; public float TailX; }
        private static readonly Rgba Paper = new Rgba(236, 226, 200), PaperLight = new Rgba(250, 244, 226), PaperShade = new Rgba(196, 182, 150), Words = new Rgba(52, 36, 24);

        /// <summary>Lay out <paramref name="text"/> (13 px, at most <paramref name="maxW"/> wide) over a head whose top is at
        /// <paramref name="headTop"/>, the tail pointing at <paramref name="mouth"/>, the box kept between
        /// <paramref name="minX"/> and <paramref name="maxX"/>.</summary>
        public static View Layout(FontFace font, string text, int maxW, (float x, float y) mouth, float headTop, float minX, float maxX)
        {
            var b = new View();
            if (string.IsNullOrEmpty(text)) return b;
            b.Lines = TextFit.Wrap(font, text, maxW);
            var w = b.Lines.Max(l => font.Width(l)) + 28; var h = b.Lines.Count * Line + 20;
            var (mx, _) = mouth;
            var bottom = headTop - 16;
            var cx = Math.Max(minX + w / 2f, Math.Min(maxX - w / 2f, mx - 6));
            b.Box = new RectF((float)Math.Floor((cx - w / 2f) / 2) * 2, (float)Math.Floor((bottom - h) / 2) * 2, (float)Math.Ceiling(w / 2f) * 2, (float)Math.Ceiling(h / 2f) * 2);
            b.TailX = Math.Max(b.Box.X + 18, Math.Min(b.Box.Right - 18, mx - 14));
            b.Tip = ((float)Math.Floor((mx - 4) / 2) * 2, b.Box.Bottom + 14);
            return b;
        }

        /// <summary>Draw <paramref name="b"/> with the first <paramref name="shown"/> letters, popping in during the first
        /// moments (<paramref name="age"/>) and faded by <paramref name="alpha"/>.</summary>
        public static void Draw(Canvas canvas, FontFace font, View b, int shown, float age, float alpha)
        {
            if (b.Lines.Count == 0 || alpha <= 0) return;
            var pop = Math.Min(1f, age / 0.12f);
            canvas.PushAlpha(alpha);
            var box = b.Box.Move(0, (float)Math.Round((1 - pop) * 3) * 2);
            // tail first (the box covers its root): rows of 2 px leaning from the box towards his face, ink round paper
            var drop = box.Y - b.Box.Y; var rows = (int)((b.Tip.y + drop - box.Bottom) / 2) + 1;
            for (var k = 0; k < rows; k++)
            {
                var u = k / (float)Math.Max(1, rows - 1); var y = box.Bottom - 2 + k * 2;
                var mid = b.TailX + (b.Tip.x - b.TailX) * u; var half = 7f * (1 - u) + 1;
                float x0 = (float)Math.Floor((mid - half) / 2) * 2, x1 = (float)Math.Ceiling((mid + half) / 2) * 2;
                canvas.Fill(new RectF(x0 - 2, y, x1 - x0 + 4, k == rows - 1 ? 4 : 2), Colors.Ink);
                if (x1 - x0 >= 2 && k < rows - 1) canvas.Fill(new RectF(x0, y, x1 - x0, 2), k < rows - 2 ? Paper : PaperShade);
            }
            PixelUi.PixelBox(canvas, box, Paper, Colors.Ink, PaperLight);
            canvas.Fill(new RectF(box.X + 2, box.Bottom - 4, box.W - 4, 2), PaperShade);
            var y0 = box.Y + 10;
            foreach (var line in b.Lines)
            {
                if (shown <= 0) break;
                var part = line.Length <= shown ? line : line.Substring(0, shown);
                font.Draw(canvas, part, box.X + 14, y0, Words);
                shown -= line.Length + 1; y0 += Line;
            }
            canvas.PopAlpha();
        }
    }
}
