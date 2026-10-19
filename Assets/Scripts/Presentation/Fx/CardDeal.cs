using System;

namespace LastWish.Presentation
{
    /// <summary>fx/card_deal.Deal: one card being dealt (wait before leaving the pile, then the flight).</summary>
    public sealed class Deal
    {
        public float Wait, T;
        public (float x, float y) Start;
        public Deal(float wait, (float x, float y) start) { Wait = wait; Start = start; }
        public bool Waiting => Wait > 0f;
        public float Progress => Waiting ? 0f : Math.Min(1f, T / CardDeal.DealSeconds);
        public bool Done => !Waiting && T >= CardDeal.DealSeconds;

        public void Advance(float dt)
        {
            dt = Math.Max(0f, dt);
            if (Wait > 0f)
            {
                var spill = dt - Wait;
                Wait = Math.Max(0f, Wait - dt);
                if (spill > 0f) T += spill;
            }
            else T += dt;
        }
    }

    /// <summary>fx/card_deal.py: arc out of the draw pile, flip in the air, settle with a small overshoot.</summary>
    public static class CardDeal
    {
        public const float DealSeconds = 0.5f, DealStagger = 0.12f, FlipEnd = 0.6f, StartScale = 0.34f, StartAngle = -28f, ArcHeight = 150f;
        public static readonly Rgba[] Trail = { new Rgba(235, 245, 255), new Rgba(170, 205, 255), new Rgba(110, 150, 230) };
        public static readonly Rgba[] TrailGold = { new Rgba(255, 250, 210), new Rgba(255, 214, 90), new Rgba(230, 150, 30) };

        public static double EaseOutCubic(double u) { u = Math.Max(0, Math.Min(1, u)); return 1 - Math.Pow(1 - u, 3); }
        public static double EaseOutBack(double u, double k = 1.6) { u = Math.Max(0, Math.Min(1, u)) - 1; return 1 + (k + 1) * u * u * u + k * u * u; }

        public static (float width, bool face) FlipWidth(float u)
        {
            var f = Math.Max(0.0, Math.Min(1.0, u / FlipEnd));
            return ((float)Math.Abs(Math.Cos(Math.PI * f)), f >= 0.5);
        }

        public static (float x, float y, float scale, float angle) Pose(float u, (float x, float y) start, (float x, float y, float scale, float angle) goal)
        {
            u = Math.Max(0f, Math.Min(1f, u));
            var e = EaseOutCubic(u);
            var (sx, sy) = start; var (gx, gy, gscale, gangle) = goal;
            var cx = (sx + gx) / 2.0; var cy = Math.Min(sy, gy) - ArcHeight;
            double a = (1 - e) * (1 - e), b = 2 * (1 - e) * e, c = e * e;
            return ((float)(a * sx + b * cx + c * gx), (float)(a * sy + b * cy + c * gy),
                (float)(StartScale + (gscale - StartScale) * EaseOutBack(u)), (float)(StartAngle * (1 - e) + gangle * e));
        }

        /// <summary>draw_dealt_card: back or face, squashed horizontally by the flip, rotated. Returns its centre and size.</summary>
        public static RectF Draw(Canvas canvas, Art art, Sprite face, (float x, float y, float scale, float angle) pose, float u)
        {
            var (x, y, scale, angle) = pose;
            var (width, showingFace) = FlipWidth(u);
            int w0 = face.W, h0 = face.H;
            var w = Math.Max(1, MathX.Round(w0 * scale * width)); var h = Math.Max(1, MathX.Round(h0 * scale));
            var src = showingFace ? face : art.CardBack;
            int cx = MathX.Round(x), cy = MathX.Round(y);
            if (Math.Abs(angle) > 0.5f)
            {
                canvas.PushTransform(Mat2D.RotateDeg(angle).Then(Mat2D.Translate(cx, cy)));
                canvas.BlitRect(src, new RectF(-w / 2f, -h / 2f, w, h), Rgba.White);
                canvas.PopTransform();
            }
            else canvas.BlitRect(src, new RectF(cx - w / 2, cy - h / 2, w, h), Rgba.White);
            return new RectF(cx - w / 2, cy - h / 2, w, h);
        }
    }
}
