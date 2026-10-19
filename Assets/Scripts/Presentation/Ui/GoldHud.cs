using System;
using System.Collections.Generic;

namespace LastWish.Presentation
{
    /// <summary>ui/gold_hud.GoldHud: the shared gold counter of the run screens.</summary>
    public sealed class GoldHud
    {
        public static readonly Rgba Gold = new Rgba(255, 214, 90), GoldLight = new Rgba(255, 244, 190), Gain = new Rgba(130, 240, 120), Loss = new Rgba(255, 110, 100);
        private static readonly Rgba Plate = new Rgba(20, 15, 10, 225), Border = new Rgba(176, 128, 46);
        public static readonly (Anchor anchor, float x, float y) DefaultPos = (Anchor.TopRight, 1268, 12);
        private const float CountTime = 0.8f, SpinEvery = 4f;
        private const int MaxDeltas = 6;
        private sealed class Delta { public string Text; public Rgba Color; public float Age; public int Direction; }
        private readonly Art art;
        private int target;
        private double shown, rate;
        private float time, spin, flash;
        private Rgba flashColor = Gold;
        private readonly List<Delta> deltas = new List<Delta>();
        private readonly BurstParticles fx;
        private bool synced;
        public RectF Rect { get; private set; }
        private static readonly Rgba[] SparkPalette = { new Rgba(255, 244, 190), new Rgba(255, 214, 90), new Rgba(230, 150, 30) };
        private static readonly Rgba[] GoldPalette = { new Rgba(255, 214, 90) };

        public GoldHud(Art art, int seed = 9) { this.art = art; fx = new BurstParticles(90, seed); }

        public int Shown => MathX.Round(shown);
        public int Target => target;
        public bool Counting => Shown != target;

        public void Sync(int amount) { target = amount; shown = amount; deltas.Clear(); synced = true; }

        public void Note(string text) => Note(text, new Rgba(200, 160, 255));
        public void Note(string text, Rgba color)
        {
            deltas.Add(new Delta { Text = text, Color = color, Age = -0.25f, Direction = -2 });
            while (deltas.Count > MaxDeltas) deltas.RemoveAt(0);
        }

        private (float x, float y) CoinCenter => (Rect.X + 26, Rect.CenterY);

        public void Update(float dt, int amount)
        {
            dt = Math.Min(0.1f, Math.Max(0f, dt));
            time += dt;
            if (!synced) Sync(amount);
            if (amount != target)
            {
                var diff = (long)amount - target;
                target = amount;
                rate = Math.Max(40.0, Math.Abs(target - shown) / CountTime);
                var gain = diff > 0;
                deltas.Add(new Delta { Text = (gain ? "+" : "−") + Numbers.FormatGold(Math.Abs(diff)), Color = gain ? Gain : Loss, Age = 0, Direction = gain ? -1 : 1 });
                while (deltas.Count > MaxDeltas) deltas.RemoveAt(0);
                flash = 1f; flashColor = gain ? Gain : Loss; spin = 0.6f;
                if (gain && Rect.W > 0)
                {
                    var (cx, cy) = CoinCenter;
                    fx.Burst(cx, cy, 14, SparkPalette, (60, 180), null, (0.3f, 0.6f), (1.5f, 3f), ParticleStyle.Square, 3f, 120);
                    fx.Burst(cx, cy, 1, GoldPalette, (0, 5), null, (0.25f, 0.35f), (24, 30), ParticleStyle.Glow, 4f);
                }
            }
            if (shown != target)
            {
                var step = rate * dt;
                if (Math.Abs(target - shown) <= step) shown = target;
                else shown += target > shown ? step : -step;
            }
            if (time % SpinEvery < dt) spin = Math.Max(spin, 0.5f);
            spin = Math.Max(0f, spin - dt);
            flash = Math.Max(0f, flash - dt * 2.5f);
            foreach (var d in deltas) d.Age += dt;
            deltas.RemoveAll(d => d.Age >= 1.3f);
            fx.Update(dt);
        }

        private static readonly (int, int)[] Offsets = { (0, 2), (4, 2), (2, 0), (2, 4), (1, 1), (3, 3), (1, 3), (3, 1) };
        private (int w, int h) OutlinedSize(string text, int size) { var f = art.Fonts.Get(size); return (f.Width(text) + 4, f.Height + 4); }
        private void Outlined(Canvas canvas, string text, int size, Rgba color, float x, float y)
        {
            var f = art.Fonts.Get(size);
            foreach (var (dx, dy) in Offsets) f.Draw(canvas, text, x + dx, y + dy, new Rgba(24, 12, 4));
            f.Draw(canvas, text, x + 2, y + 2, color);
        }

        public RectF Draw(Canvas canvas, (Anchor anchor, float x, float y)? at = null)
        {
            var (anchor, px, py) = at ?? DefaultPos;
            var text = Numbers.FormatGold(Shown);
            var (nw, nh) = OutlinedSize(text, 26);
            var w = Math.Max(132, nw + 62); const int h = 46;
            var rect = Txt.Place(w, h, anchor, px, py);
            Rect = rect;
            canvas.Rect(rect, Plate, 0, 10);
            var border = Border;
            if (flash > 0) border = Rgba.Lerp(Border, flashColor, flash);
            canvas.Rect(rect, border, 2, 10);
            canvas.Line(rect.X + 10, rect.Y + 3, rect.Right - 10, rect.Y + 3, new Rgba(90, 66, 30));
            var (cx, cy) = CoinCenter;
            Glows.Draw(canvas, art, cx, cy, 26, new Rgba(60, 44, 10));
            var coinSize = Json.Int(art.Gacha.Get("coin")); var frames = Json.Int(art.Gacha.Get("coin_frames"));
            var k = spin > 0 ? (int)(time * 18) % frames : 0;
            var coin = new Sprite(art.Texture("Gacha/coin"), k * coinSize, 0, coinSize, coinSize);
            // gacha coin ×2 then ×3//2 → ×3 native.
            var scale = (coinSize * 2 * 3 / 2) / (float)coinSize;
            canvas.BlitRect(coin, RectF.CenteredInt(cx, cy + (int)(Math.Sin(time * 2) * 1), (int)(coinSize * scale), (int)(coinSize * scale)), Rgba.White);
            var numberRect = Txt.Place(nw, nh, Anchor.MidLeft, rect.X + 46, rect.CenterY);
            Outlined(canvas, text, 26, Counting ? GoldLight : Gold, numberRect.X, numberRect.Y);
            foreach (var d in deltas)
            {
                if (d.Age < 0) continue;
                var size = d.Direction != -2 ? 20 : 16;
                var (dw, dh) = OutlinedSize(d.Text, size);
                double y;
                if (d.Direction == -2) y = rect.Bottom + 56 - Math.Min(1.0, d.Age / 0.5) * 18;
                else if (d.Direction < 0) y = rect.Bottom + 34 - Math.Min(1.0, d.Age / 0.5) * 18;
                else y = rect.Bottom + 16 + d.Age * 22;
                var r = Txt.Place(dw, dh, Anchor.MidRight, rect.Right - 6, (int)y);
                canvas.PushAlpha((float)Math.Max(0.0, 1 - d.Age / 1.3));
                Outlined(canvas, d.Text, size, d.Color, r.X, r.Y);
                canvas.PopAlpha();
            }
            fx.Draw(canvas, art);
            return rect;
        }
    }
}
