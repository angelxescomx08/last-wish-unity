using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>ui/pixel_ui.ManaOrb: liquid cut at the mana level, splash on spend, glow on refill, shake on error.</summary>
    public sealed class ManaOrb
    {
        public const int Scale = 2, Size = 48 * Scale;
        private const float OrbC = 23.5f, OrbR = 18.5f;
        private static readonly Rgba[] LiquidSplash = { new Rgba(160, 214, 255), new Rgba(78, 156, 246), new Rgba(232, 248, 255) };
        private static readonly Rgba[] Motes = { new Rgba(232, 248, 255), new Rgba(160, 214, 255), new Rgba(120, 190, 255) };

        private float? level; private int? shown;
        public float T { get; private set; }
        private float flash, splash, shake;
        public (int x, int y) Center = (68, 595);
        private readonly BurstParticles particles;

        public ManaOrb(int seed = 3) { particles = new BurstParticles(160, seed); }
        public float Shake => shake;
        public int ParticleCount => particles.Count;

        public static float Target(int current, int maximum) => maximum > 0 ? Math.Max(0f, Math.Min(1f, current / (float)maximum)) : 0f;
        public void Error() => shake = 0.45f;

        public void Update(float dt, int current, int maximum)
        {
            dt = Math.Max(0f, Math.Min(dt, 0.1f));
            T += dt;
            var goal = Target(current, maximum);
            if (!level.HasValue) { level = goal; shown = current; }
            level += (goal - level.Value) * Math.Min(1f, dt * 7f);
            var (cx, cy) = Center;
            if (current < shown)
            {
                splash = 0.5f;
                var n = 6 + 3 * Math.Min(5, shown.Value - current);
                particles.Burst(cx, cy - 6, n, LiquidSplash, (60f, 170f), ((float)(Math.PI * 1.1), (float)(Math.PI * 1.9)), (0.35f, 0.7f), (2f, 4f), ParticleStyle.Square, 1f, 420f);
            }
            else if (current > shown)
            {
                flash = 0.7f;
                for (var k = 0; k < 14; k++)
                {
                    var a = k / 14.0 * MathX.Tau;
                    particles.Emit(cx + (float)Math.Cos(a) * 30, cy + (float)Math.Sin(a) * 30, (float)Math.Cos(a) * 15, -70 - 40 * (k % 3), 0.9f, Motes, 3, ParticleStyle.Glow, 1.5f);
                }
            }
            shown = current;
            flash = Math.Max(0f, flash - dt); splash = Math.Max(0f, splash - dt); shake = Math.Max(0f, shake - dt);
            if (current > 0 && particles.Count < 40 && (int)(T * 6) != (int)((T - dt) * 6))
                particles.Emit(cx + (float)(Math.Sin(T * 13) * 14), cy + 26, 0, -40, 0.7f, Motes, 2, ParticleStyle.Square);
            particles.Update(dt);
        }

        private void Compose(Canvas canvas, Art art, int ox, int oy)
        {
            var back = art.KitPiece("orb_back");
            if (back.Valid) canvas.Blit(back, ox, oy, Scale);
            var lvl = level ?? 0f;
            var liquid = art.KitPiece("orb_liquid_" + ((int)(T * 8) % 8));
            if (liquid.Valid && lvl > 0.001f)
            {
                var top = OrbC - OrbR;
                var surfaceY = top + (1 - lvl) * (OrbR * 2);
                var amp = lvl < 0.999f ? 1f + 1.5f * splash : 0f;
                var crest = new Rgba(220, 244, 255);
                for (var x = 4; x < 44; x++)
                {
                    var wave = surfaceY + Math.Sin(x * 0.45 + T * 3.2) * amp;
                    var y0 = Math.Max(0, MathX.Round(wave));
                    if (y0 >= 47) continue;
                    canvas.Blit(liquid.Sub(x, y0, 1, 48 - y0), ox + x * Scale, oy + y0 * Scale, Scale);
                    if (lvl < 0.999f && Math.Sqrt(Math.Pow(x + 0.5 - OrbC, 2) + Math.Pow(y0 + 0.5 - OrbC, 2)) < OrbR - 0.5)
                        canvas.Fill(new RectF(ox + x * Scale, oy + y0 * Scale, Scale, Scale), crest);
                }
            }
            var glass = art.KitPiece("orb_glass"); var frame = art.KitPiece("orb_frame");
            if (glass.Valid) canvas.Blit(glass, ox, oy, Scale);
            if (frame.Valid) canvas.Blit(frame, ox, oy, Scale);
        }

        public IRect Draw(Canvas canvas, Art art, int cx0, int cy0, int current, int maximum)
        {
            Center = (cx0, cy0);
            var dx = shake > 0 ? (int)(Math.Sin(T * 70) * 4 * (shake / 0.45f)) : 0;
            int cx = cx0 + dx, cy = cy0;
            if (!art.KitPiece("orb_frame").Valid)
            {
                canvas.Circle(cx, cy, 40, new Rgba(20, 30, 70));
                canvas.Circle(cx, cy, 40, new Rgba(120, 170, 255), 2);
            }
            else
            {
                // _glow_circle blitted with BLEND_RGB_ADD: pygame ignores the alpha, so the whole disc adds.
                if (flash > 0) PixelUi.GlowCircle(canvas, cx, cy, Size / 2 + 16, new Rgba(90, 170, 255));
                Compose(canvas, art, cx - Size / 2, cy - Size / 2);
                if (shake > 0) canvas.Circle(cx, cy, Size / 2 - 4, new Rgba(255, 70, 60), 3);
            }
            var numCol = current <= 0 || shake > 0 ? new Rgba(255, 140, 130) : Rgba.White;
            var outline = new Rgba(6, 14, 40);
            var bigF = art.Fonts.Get(34); var smallF = art.Fonts.Get(15);
            var big = current.ToString(); var small = "/" + maximum;
            var bw = Txt.OutlinedSize(bigF, big).w; var total = bw + Txt.OutlinedSize(smallF, small).w;
            Txt.OutlinedAt(canvas, bigF, big, Anchor.MidLeft, cx - total / 2, cy - 2, numCol, outline);
            Txt.OutlinedAt(canvas, smallF, small, Anchor.BottomLeft, cx - total / 2 + bw, cy + 12, new Rgba(190, 220, 255), outline);
            particles.Draw(canvas, art);
            return new IRect(cx - Size / 2, cy - Size / 2, Size, Size);
        }
    }

    /// <summary>ui/hud_widget.py: relic slots, piles, End Turn button and the turn ribbon.</summary>
    public static class Hud
    {
        public const int RelicSize = 48, RelicGap = 5, RelicIconSize = 36, PileW = 108, PileH = 88;

        public static void DrawRelics(Canvas canvas, Art art, IReadOnlyList<OwnedRelic> relics, int x, int y, int? hoveredIndex, float t, List<IRect> rects)
        {
            rects.Clear();
            for (var i = 0; i < relics.Count; i++)
            {
                var relic = relics[i];
                var rect = new IRect(x, y, RelicSize, RelicSize); rects.Add(rect);
                var hovered = i == hoveredIndex;
                var shown = hovered ? rect.Move(0, -2) : rect;
                if (art.HasKitPiece("panel")) art.DrawKitSlice(canvas, "panel", shown);
                else canvas.Rect(shown, Colors.RelicBg, 0, 6);
                var rim = CardWidget.RarityColor((int)relic.Definition.Rarity);
                canvas.Rect(shown.Inflate(-6, -6), hovered ? Colors.TextPrimary : rim, 1, 3);
                var icon = art.Relic(relic.Definition.Name);
                if (icon.Valid)
                {
                    canvas.BlitRect(icon, IRect.AtCenter(shown.CenterX, shown.CenterY, RelicIconSize, RelicIconSize), Rgba.White);
                    if (!relic.Active) canvas.Fill(shown.Inflate(-8, -8), new Rgba(0, 0, 0, 170));
                }
                else
                {
                    var name = relic.Definition.Name;
                    Txt.OutlinedAt(canvas, art.Fonts.Get(10), name.Substring(0, Math.Min(5, name.Length)), Anchor.Center, shown.CenterX, shown.CenterY - 4, Colors.TextAccent);
                    if (name.Length > 5) Txt.OutlinedAt(canvas, art.Fonts.Get(8), name.Substring(5, Math.Min(5, name.Length - 5)), Anchor.Center, shown.CenterX, shown.CenterY + 8, Colors.TextSecondary);
                }
                if (relic.Chroma != ChromaKind.None) ChromaFx.DrawBox(canvas, art, shown, relic.Chroma, t, 6);
                x += RelicSize + RelicGap;
            }
        }

        public static IRect DrawPile(Canvas canvas, Art art, bool draw, int count, int x, int y, bool hovered = false)
        {
            var rect = new IRect(x, y, PileW, PileH);
            var lift = hovered ? -3 : 0;
            var sprite = art.KitPiece(count == 0 ? "pile_empty" : draw ? "pile_draw" : "pile_discard");
            int bx, by;
            if (sprite.Valid)
            {
                int w = sprite.W * 2, h = sprite.H * 2;
                var pos = new IRect(rect.CenterX - w / 2, rect.Y + lift, w, h);
                canvas.Blit(sprite, pos.X, pos.Y, 2);
                if (hovered) canvas.Blit(sprite, pos.X, pos.Y, 2, new Rgba(40, 34, 20), Blend.AddMask);
                bx = pos.Right - 8; by = pos.Bottom - 10;
            }
            else
            {
                canvas.Rect(rect, draw ? new Rgba(25, 30, 45) : new Rgba(35, 20, 20), 0, 5);
                bx = rect.Right - 14; by = rect.Bottom - 16;
            }
            canvas.Circle(bx, by, 17, new Rgba(12, 8, 16));
            canvas.Circle(bx, by, 17, draw ? new Rgba(90, 140, 230) : new Rgba(220, 90, 80), 2);
            Txt.OutlinedAt(canvas, art.Fonts.Get(19), count.ToString(), Anchor.Center, bx, by, Rgba.White);
            var title = draw ? "Robo" : "Descarte";
            Txt.OutlinedAt(canvas, art.Fonts.Get(12), hovered ? title + " · Ver" : title, Anchor.MidTop, rect.CenterX - 10, rect.Bottom - 6, new Rgba(230, 220, 200));
            return rect;
        }

        public static IRect DrawEndTurn(Canvas canvas, Art art, IRect rect, bool hovered, bool pressed, bool enabled, bool ready, float t)
        {
            var state = !enabled ? ButtonState.Off : pressed && hovered ? ButtonState.Press : hovered ? ButtonState.Hover : ButtonState.Idle;
            var glow = ready && enabled ? 0.55f + 0.45f * (float)Math.Sin(t * 5.0) : 0f;
            PixelUi.Button(canvas, art, rect, enabled ? "TERMINAR TURNO" : "TURNO ENEMIGO", "gold", state, "hourglass", enabled ? "E" : null, t, glow, 14);
            return rect;
        }

        public static RectF DrawTurnCounter(Canvas canvas, Art art, int turn, int cx, int cy) => PixelUi.Ribbon(canvas, art, cx, cy, "TURNO " + turn, 16);
    }
}
