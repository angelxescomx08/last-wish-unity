using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>
    /// scenes/pack_opening_scene.py: intro → idle → charge → burst (tear, explosion, rays) → deal → reveal
    /// (flip per card, rarity bursts) → pick → outro. Any click / Space skips the animation.
    /// </summary>
    public sealed class PackOpeningScene : Scene
    {
        private static readonly Rgba Bg = new Rgba(8, 12, 20);
        private const int Gap = 20, CardY = 120, ScreenW = 1280, ScreenH = 720, PackH = 380, PackCx = 640, PackCy = 360;
        public const float IntroT = 0.55f, ChargeT = 0.8f, BurstT = 0.35f, DealEach = 0.45f, DealStagger = 0.09f, FlipT = 0.32f, FlipStagger = 0.28f, AnticipationT = 0.3f, OutroT = 0.55f, TearFraction = 0.13f;
        private static readonly Rgba[] Silver = { new Rgba(255, 255, 255), new Rgba(220, 225, 235), new Rgba(150, 155, 170) };
        private static readonly Rgba[] GoldPal = { new Rgba(255, 245, 190), new Rgba(255, 210, 80), new Rgba(215, 140, 25), new Rgba(140, 70, 10) };
        private static readonly Dictionary<string, (Rgba glow, Rgba[] palette, Rgba[] accent)> Themes = new Dictionary<string, (Rgba, Rgba[], Rgba[])>
        {
            ["acero"] = (new Rgba(200, 50, 40), new[] { new Rgba(255, 245, 230), new Rgba(255, 130, 95), new Rgba(205, 45, 40), new Rgba(95, 20, 20) }, Silver),
            ["escudo"] = (new Rgba(40, 170, 85), new[] { new Rgba(240, 255, 240), new Rgba(125, 240, 155), new Rgba(40, 165, 80), new Rgba(15, 65, 30) }, Silver),
            ["magia"] = (new Rgba(50, 120, 240), new[] { new Rgba(235, 250, 255), new Rgba(125, 205, 255), new Rgba(50, 115, 235), new Rgba(20, 40, 115) }, Silver),
            ["epico"] = (new Rgba(155, 60, 225), new[] { new Rgba(255, 245, 255), new Rgba(220, 150, 255), new Rgba(150, 60, 220), new Rgba(60, 20, 100) }, GoldPal),
        };
        public static readonly Rgba[][] RarityPalette =
        {
            null,
            new[] { new Rgba(255, 255, 255), new Rgba(205, 195, 180), new Rgba(120, 110, 100) },
            new[] { new Rgba(230, 255, 235), new Rgba(110, 220, 130), new Rgba(40, 120, 60) },
            new[] { new Rgba(230, 245, 255), new Rgba(110, 170, 255), new Rgba(40, 80, 180) },
            new[] { new Rgba(250, 235, 255), new Rgba(200, 120, 255), new Rgba(110, 40, 170) },
            new[] { new Rgba(255, 255, 230), new Rgba(255, 215, 90), new Rgba(240, 150, 30), new Rgba(150, 70, 10) },
        };
        private static readonly float[] SparkleRate = { 0, 0, 0, 3f, 6f, 11f };

        private static float Clamp01(float t) => t < 0 ? 0 : t > 1 ? 1 : t;
        public static float EaseOutCubic(float t) { t = Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
        public static float EaseOutBack(float t, float s = 1.7f) { t = Clamp01(t) - 1f; return t * t * ((s + 1f) * t + s) + 1f; }
        private static int Rarity(CardDefinition c) => Math.Max(1, Math.Min(5, c.Rarity));
        private static bool IsSpecial(OfferedCard c) => Rarity(c.Card) >= 3 || c.Card.Chroma != ChromaKind.None || c.Lucky;
        private static Rgba[] Slice(Rgba[] p, int n) { var r = new Rgba[Math.Min(n, p.Length)]; Array.Copy(p, r, r.Length); return r; }
        private static Rgba[] RevealPalette(CardDefinition c)
        {
            if (c.Chroma != ChromaKind.None) { var st = ChromaFx.Style(c.Chroma); return new[] { st.Bright, st.Main, st.Glow, st.Dark }; }
            return RarityPalette[Rarity(c)];
        }

        private readonly CardBonus bonus; private readonly LuckReport luck;
        private List<OfferedCard> cards;
        private readonly string packName;
        private readonly List<RectF> cardRects = new List<RectF>();
        private int? hovered; private RectF? skipRect, rerollRect;
        private (float x, float y) mouse;
        private readonly PyRandom rng;
        private readonly ChromaKind packChroma;
        private int picks; private readonly List<int> chosen = new List<int>();
        private Func<List<OfferedCard>> reroll;
        private readonly string theme; private readonly Rgba glowColor; private readonly Rgba[] palette, accent;
        private readonly PackTheme packTheme;
        private readonly int packW, tearY; private readonly int[] tearEdge;
        private readonly BurstParticles fx; private readonly ParticleSystem motes;
        private float shake, flash, time; private (int x, int y) shakeOff;
        private string phaseName = "intro"; private float t, tOpen, tOut; private bool packHover;
        private bool[] landed, flipped; private float[] dealAngle, flipStart, sparkleAcc; private float pickAt;
        private readonly float[] strip = new float[5]; private readonly float[] body = new float[2];

        public bool Cleared { get; set; }
        public CardDefinition ChosenCard { get; private set; }
        public List<CardDefinition> ChosenCards { get; } = new List<CardDefinition>();
        public override bool ShowGoldHud => true;

        public PackOpeningScene(GameContext game, List<OfferedCard> cards, string packName, PackTheme theme, int seed = 0, ChromaKind chroma = ChromaKind.None,
            CardBonus bonus = default, LuckReport luck = null, int? picks = null, Func<List<OfferedCard>> reroll = null) : base(game)
        {
            this.bonus = bonus; this.luck = luck; this.cards = cards; this.packName = packName; rng = new PyRandom(seed);
            packChroma = chroma;
            var keep = picks ?? (int)chroma + 1;
            this.picks = cards.Count > 0 ? Math.Max(1, Math.Min(keep, cards.Count)) : 1;
            this.reroll = reroll;
            packTheme = theme; this.theme = PackArt.ThemeKey(theme);
            if (!Themes.ContainsKey(this.theme)) this.theme = "acero";
            (glowColor, palette, accent) = Themes[this.theme];
            packW = PackArt.Size(Art, theme, PackH).w;
            tearY = Math.Max(10, (int)(PackH * TearFraction));
            tearEdge = TearEdge(packW, tearY);
            fx = new BurstParticles(900, seed);
            var dark = Glows.Scaled(glowColor, 0.35);
            motes = new ParticleSystem(new EmitterConfig
            {
                Rate = 14, Lifetime = (3f, 6f), Area = (0, 330, 640, 30), Vy = (-18f, -8f), Wobble = 6f,
                Colors = new[] { dark, Glows.Scaled(glowColor, 0.8), palette[1], dark }, Size = 1, Capacity = 90,
            }, 2, seed);
            motes.Prewarm(4f);
            landed = new bool[cards.Count]; flipped = new bool[cards.Count]; sparkleAcc = new float[cards.Count];
            dealAngle = new float[cards.Count];
            for (var i = 0; i < cards.Count; i++) dealAngle[i] = (float)rng.Uniform(-22.0, 22.0);
            flipStart = ScheduleFlips();
            pickAt = cards.Count > 0 ? flipStart[flipStart.Length - 1] + FlipT + 0.15f : BurstT;
        }

        private static int[] TearEdge(int w, int tearY, int seed = 7)
        {
            var r = new PyRandom(seed); var pts = new List<(int x, int y)>();
            var x = 0; var up = true;
            while (x < w + 14) { pts.Add((x, tearY + (up ? -5 : 5) + r.RandInt(-2, 2))); x += r.RandInt(8, 14); up = !up; }
            var ys = new int[Math.Max(0, w)]; var j = 0;
            for (var cx = 0; cx < w; cx++)
            {
                while (j + 1 < pts.Count - 1 && pts[j + 1].x <= cx) j++;
                var (x0, y0) = pts[j]; var (x1, y1) = pts[j + 1];
                var u = x1 != x0 ? (cx - x0) / (double)(x1 - x0) : 0.0;
                ys[cx] = (int)(y0 + (y1 - y0) * u);
            }
            return ys;
        }

        private float[] ScheduleFlips()
        {
            var dealEnd = BurstT + Math.Max(0, cards.Count - 1) * DealStagger + DealEach;
            var tt = dealEnd + 0.15f; var starts = new float[cards.Count];
            for (var i = 0; i < cards.Count; i++) { if (IsSpecial(cards[i])) tt += AnticipationT; starts[i] = tt; tt += FlipStagger; }
            return starts;
        }

        private (float x, float y) SlotCenter(int i)
        {
            var n = cards.Count; var totalW = n * CardWidget.CardW + Math.Max(0, n - 1) * Gap; var startX = ScreenW / 2 - totalW / 2;
            return (startX + i * (CardWidget.CardW + Gap) + CardWidget.CardW / 2f, CardY + CardWidget.CardH / 2f);
        }
        private IRect PackRect => IRect.AtCenter(PackCx, PackCy, packW, PackH);

        public string Phase
        {
            get
            {
                if (phaseName != "open") return phaseName;
                if (tOpen < BurstT) return "burst";
                foreach (var l in landed) if (!l) return "deal";
                return tOpen < pickAt ? "reveal" : "pick";
            }
        }
        public bool IsAnimating => Phase != "idle" && Phase != "pick";

        public override void HandleEvent(in InputEvent e)
        {
            if (Cleared) return;
            if (e.Type == EventType.MouseMove)
            {
                mouse = e.Pos;
                packHover = (Phase == "intro" || Phase == "idle") && PackRect.Contains(e.X, e.Y);
                if (Phase == "pick") UpdateHover(e.X, e.Y);
            }
            else if (e.IsLeftDown) { mouse = e.Pos; AdvanceOrClick(e.Pos); }
            else if (e.Type == EventType.KeyDown && (e.Key == Key.Space || e.Key == Key.Return || e.Key == Key.KeypadEnter))
            { if (Phase != "pick") AdvanceOrClick(null); }
        }

        private void AdvanceOrClick((float x, float y)? pos)
        {
            var phase = Phase;
            if (phase == "intro" || phase == "idle") StartCharge();
            else if (phase == "pick") { if (pos.HasValue) Click(pos.Value.x, pos.Value.y); }
            else if (phase != "outro") SkipAnimation();
        }

        public void StartCharge() { if (phaseName == "intro" || phaseName == "idle") { phaseName = "charge"; t = 0; Sound.PlayConfirm(); } }

        public void SkipAnimation()
        {
            if (phaseName == "intro" || phaseName == "idle" || phaseName == "charge") Open();
            if (phaseName != "open") return;
            tOpen = Math.Max(tOpen, pickAt);
            for (var i = 0; i < cards.Count; i++) { landed[i] = true; flipped[i] = true; }
            fx.Clear(); flash = 0; shake = 0;
        }

        public override void Update(float dt)
        {
            dt = Math.Min(0.1f, Math.Max(0f, dt));
            time += dt; motes.Update(dt); fx.Update(dt);
            flash = Math.Max(0f, flash - dt * 2.8f);
            shake *= (float)Math.Exp(-7.0 * dt);
            if (shake > 0.3f) { var a = shake; shakeOff = ((int)rng.Uniform(-a, a), (int)rng.Uniform(-a, a)); } else shakeOff = (0, 0);
            if (phaseName == "intro") { t += dt; if (t >= IntroT) { phaseName = "idle"; t = 0; } }
            else if (phaseName == "idle")
            {
                t += dt;
                if (rng.Random() < dt * 5)
                {
                    var r = PackRect;
                    var px = rng.Uniform(r.X, r.Right); var py = rng.Uniform(r.Y, r.Bottom); var vy = rng.Uniform(-40, -20); var life = rng.Uniform(0.6, 1.1); var size = rng.Uniform(4, 8);
                    fx.Emit((float)px, (float)py, 0, (float)vy, (float)life, Slice(palette, 3), (float)size, ParticleStyle.Glow);
                }
            }
            else if (phaseName == "charge")
            {
                t += dt; var u = t / ChargeT;
                shake = Math.Max(shake, 1f + 6f * u * u);
                var n = (int)(dt * (40 + 160 * u)) + (rng.Random() < 0.5 ? 1 : 0);
                fx.Implode(PackCx, PackCy, n, Slice(palette, 3), (150f, 280f), (0.25f, 0.45f), (1.5f, 3f));
                if (t >= ChargeT) Open();
            }
            else if (phaseName == "open") UpdateOpen(dt);
            else if (phaseName == "outro") { tOut += dt; if (tOut >= OutroT) Cleared = true; }
        }

        private void Open()
        {
            phaseName = "open"; tOpen = 0; flash = 1f; shake = 14f;
            Sound.PlayOpenPack();
            float cx = PackCx, cy = PackCy; float tearScreenY = PackRect.Y + tearY;
            fx.Burst(cx, cy, 110, palette, (120f, 620f), null, (0.5f, 1.2f), (3f, 7f), ParticleStyle.Square, 2.2f, 260f, 40f);
            fx.Burst(cx, tearScreenY, 70, Slice(palette, 3), (250f, 800f), null, (0.25f, 0.6f), (1.5f, 3f), ParticleStyle.Spark, 3f, 0f, 60f);
            fx.Burst(cx, cy, 26, Slice(palette, 3), (40f, 260f), null, (0.5f, 1f), (14f, 34f), ParticleStyle.Glow, 2.5f, 0f, 50f);
            fx.Burst(cx, tearScreenY, 45, accent, (200f, 520f), ((float)(-Math.PI * 0.95), (float)(-Math.PI * 0.05)), (0.9f, 1.6f), (3f, 5f), ParticleStyle.Square, 1.2f, 520f);
            strip[0] = 0; strip[1] = 0; strip[2] = (float)rng.Uniform(140, 220); strip[3] = -560f; strip[4] = 0;
            body[0] = 0; body[1] = -60f;
        }

        private void UpdateOpen(float dt)
        {
            tOpen += dt;
            strip[2] *= 1f - 0.4f * dt; strip[3] += 1500f * dt; strip[0] += strip[2] * dt; strip[1] += strip[3] * dt; strip[4] -= 300f * dt;
            body[1] += 900f * dt; body[0] += body[1] * dt;
            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                var start = BurstT + i * DealStagger;
                if (i == 0 && !landed[0] && tOpen - dt < start && start <= tOpen) Sound.PlayCard();
                if (!landed[i] && tOpen >= start + DealEach)
                {
                    landed[i] = true; var (x, y) = SlotCenter(i);
                    fx.Burst(x, y + CardWidget.CardH / 2f, 8, Silver, (40f, 140f), ((float)Math.PI, (float)MathX.Tau), (0.25f, 0.5f), (2f, 3f), ParticleStyle.Square, 4f);
                }
                var fs = flipStart[i];
                if (IsSpecial(card) && !flipped[i] && fs - AnticipationT <= tOpen && tOpen < fs && rng.Random() < dt * 30)
                {
                    var (x, y) = SlotCenter(i);
                    fx.Implode(x, y, 2, Slice(RevealPalette(card.Card), 3), (90f, 150f), (0.2f, 0.35f));
                }
                if (!flipped[i] && tOpen >= fs + FlipT * 0.5f) { flipped[i] = true; FlipBurst(i); }
            }
            if (Phase == "pick") UpdateSparkles(dt);
        }

        private void FlipBurst(int i)
        {
            var c = cards[i].Card; var v = Rarity(c); var pal = RarityPalette[v]; var (x, y) = SlotCenter(i);
            fx.Burst(x, y, 8 + 6 * v, pal, (80f, 200f + 50 * v), null, (0.35f, 0.8f), (2f, 4f), ParticleStyle.Square, 2.5f, 0f, 30f);
            fx.Burst(x, y, 4 * v, Slice(pal, 2), (200f, 380f + 40 * v), null, (0.2f, 0.45f), (1.5f, 2.5f), ParticleStyle.Spark, 3f, 0f, 20f);
            if (v >= 3) fx.Burst(x, y, 2 * v, Slice(pal, 3), (20f, 120f), null, (0.4f, 0.8f), (16f, 30f), ParticleStyle.Glow, 3f, 0f, 25f);
            if (v == 5)
            {
                fx.Burst(x, y - CardWidget.CardH / 2f, 55, GoldPal, (220f, 460f), ((float)(-Math.PI * 0.9), (float)(-Math.PI * 0.1)), (1f, 1.7f), (3f, 5f), ParticleStyle.Square, 1f, 560f);
                shake = Math.Max(shake, 7f); flash = Math.Max(flash, 0.35f);
            }
            if (c.Chroma != ChromaKind.None)
            {
                var gold = RevealPalette(c);
                fx.Burst(x, y - CardWidget.CardH / 2f, 60, gold, (220f, 480f), ((float)(-Math.PI * 0.95), (float)(-Math.PI * 0.05)), (1f, 1.8f), (3f, 5f), ParticleStyle.Square, 1f, 560f);
                fx.Burst(x, y, 12, Slice(gold, 3), (20f, 140f), null, (0.5f, 0.9f), (18f, 34f), ParticleStyle.Glow, 3f, 0f, 30f);
                shake = Math.Max(shake, 6f); flash = Math.Max(flash, 0.3f);
            }
            if (v >= 4 || c.Chroma != ChromaKind.None) Sound.PlayReward(); else Sound.PlayCard();
        }

        private void UpdateSparkles(float dt)
        {
            for (var i = 0; i < cards.Count; i++)
            {
                if (phaseName == "outro") break;
                var c = cards[i].Card;
                var rate = SparkleRate[Rarity(c)] + (hovered == i ? 4f : 0f) + (c.Chroma != ChromaKind.None ? 8f : 0f);
                if (rate == 0) continue;
                sparkleAcc[i] += rate * dt;
                var (x, y) = SlotCenter(i); var lift = hovered == i ? -20 : 0; var pal = RevealPalette(c);
                while (sparkleAcc[i] >= 1f)
                {
                    sparkleAcc[i] -= 1f;
                    var px = x + rng.Uniform(-CardWidget.CardW / 2.0, CardWidget.CardW / 2.0); var py = y + lift + rng.Uniform(-CardWidget.CardH / 2.0, CardWidget.CardH / 2.0);
                    var vx = rng.Uniform(-8, 8); var vy = rng.Uniform(-60, -25); var life = rng.Uniform(0.7, 1.3); var size = rng.Uniform(2, 3);
                    fx.Emit((float)px, (float)py, (float)vx, (float)vy, (float)life, Slice(pal, 3), (float)size);
                }
            }
        }

        // ------------------------------------------------------------------ drawing
        public override void Draw(Canvas canvas)
        {
            canvas.Fill(new RectF(0, 0, ScreenW, ScreenH), Bg);
            var (ox, oy) = shakeOff; var phase = Phase;
            motes.Draw(canvas);
            DrawBackgroundGlow(canvas, phase, ox, oy);
            if (phase == "intro" || phase == "idle" || phase == "charge") DrawClosedPack(canvas, phase, ox, oy);
            else { DrawRays(canvas, ox, oy); DrawPackPieces(canvas, ox, oy); DrawCards(canvas, phase, ox, oy); }
            fx.Draw(canvas, Art, ox, oy);
            if (flash > 0) canvas.Fill(new RectF(0, 0, ScreenW, ScreenH), new Rgba(255, 255, 255, (int)(255 * Math.Min(1f, flash))));
            DrawUi(canvas, phase);
            if (luck != null) LuckBadge.Draw(canvas, Game, Anchor.BottomLeft, 16, 704, luck);
        }

        private void DrawBackgroundGlow(Canvas canvas, string phase, int ox, int oy)
        {
            double k; int cx, cy, radius;
            if (phase == "intro" || phase == "idle" || phase == "charge")
            {
                k = 0.45 + 0.12 * Math.Sin(time * 2.4);
                if (phase == "idle" && packHover) k += 0.15;
                if (phase == "charge") k += 0.55 * (t / ChargeT);
                if (phase == "intro") k *= EaseOutCubic(t / IntroT);
                cx = PackCx; cy = PackCy; radius = 300;
            }
            else
            {
                k = phase != "outro" ? 0.4 : 0.4 * (1.0 - tOut / OutroT);
                cx = ScreenW / 2; cy = CardY + CardWidget.CardH / 2; radius = 420;
            }
            k = MathX.Round(k * 10) / 10.0;
            if (k <= 0) return;
            Glows.DrawAt(canvas, Art, cx - radius + ox, cy - radius + oy, radius, Glows.Scaled(glowColor, k));
        }

        /// <summary>The (gilded) pack art, or its white silhouette, into <paramref name="dst"/> (sub-rect in pack px).</summary>
        private void PackSprite(Canvas canvas, int sx, int sy, int sw, int sh, RectF dst, Rgba? silhouette = null)
        {
            var art = Art.PackArt(PackArt.ThemeKey(packTheme)); if (!art.Valid) return;
            float ku = art.W / (float)packW, kv = art.H / (float)PackH;
            var sub = art.Sub((int)(sx * ku), (int)(sy * kv), Math.Max(1, (int)Math.Round(sw * ku)), Math.Max(1, (int)Math.Round(sh * kv)));
            if (silhouette.HasValue) { canvas.BlitRect(sub, dst, silhouette.Value, Blend.Silhouette); return; }
            if (packChroma != ChromaKind.None)
            {
                var st = ChromaFx.Style(packChroma);
                canvas.BlitRect(sub, dst, st.FrameMult);
                canvas.BlitRect(sub, dst, Glows.Scaled(st.FrameAdd, canvas.Alpha), Blend.AddMask);
            }
            else canvas.BlitRect(sub, dst, Rgba.White);
        }

        private void DrawClosedPack(Canvas canvas, string phase, int ox, int oy)
        {
            int cx = PackCx, cy = PackCy; double angle = 0, scale = 1;
            if (phase == "intro") { var e = EaseOutBack(t / IntroT); cy = (int)(-PackH + (cy + PackH) * e); }
            else if (phase == "idle") { cy += (int)(Math.Sin(time * 2.0) * 6); angle = Math.Sin(time * 1.3) * 2.0; scale = packHover ? 1.04 : 1.0; }
            else { var u = t / ChargeT; angle = Math.Sin(time * 55.0) * (1.5 + 4.0 * u); scale = 1.0 + 0.08 * u; }
            var dst = RectF.CenteredInt(cx + ox, cy + oy, packW, PackH);
            canvas.PushRotateScale(cx + ox, cy + oy, (float)angle, (float)scale);
            PackSprite(canvas, 0, 0, packW, PackH, dst);
            if (packChroma != ChromaKind.None)
            {
                var art = Art.PackArt(PackArt.ThemeKey(packTheme));
                ChromaFx.Sheen(canvas, dst, packChroma, time, art);
                ChromaFx.DrawTwinkles(canvas, dst, packChroma, time, Math.Max(0.5f, packW / 90f));
            }
            if (phase == "charge") { var u = t / ChargeT; PackSprite(canvas, 0, 0, packW, PackH, dst, new Rgba(255, 255, 255, (int)(230 * u * u))); }
            canvas.PopTransform();
            if (packChroma != ChromaKind.None)
            {
                var w = (int)(packW * Math.Abs(scale)); var h = (int)(PackH * scale);
                ChromaFx.DrawMotes(canvas, Art, RectF.CenteredInt(cx + ox, cy + oy, w, h), packChroma, time, 18, 1.8f);
            }
        }

        private Sprite RaysSprite()
        {
            const int size = 760, r = size / 2;
            return new Sprite(Art.Procedural("pack_rays", size, size, () =>
            {
                var px = new Rgba[size * size];
                const int n = 14;
                for (var y = 0; y < size; y++)
                    for (var x = 0; x < size; x++)
                    {
                        double dx = x + 0.5 - r, dy = y + 0.5 - r;
                        var d = Math.Sqrt(dx * dx + dy * dy);
                        var a = MathX.PyMod(Math.Atan2(dy, dx), MathX.Tau);
                        var seg = MathX.PyMod(a, MathX.Tau / n);
                        var inRay = seg <= MathX.Tau / n * 0.42 && d <= r;
                        var rr = (int)Math.Ceiling(d - 0.0001);
                        var glow = rr <= r ? Math.Pow(1.0 - Math.Max(1, rr) / (double)r, 2) : 0;
                        var v = inRay ? (int)(255 * glow) : 0;
                        px[y * size + x] = new Rgba(v, v, v, 255);
                    }
                return px;
            }, smooth: true));
        }

        private void DrawRays(Canvas canvas, int ox, int oy)
        {
            double k;
            if (Phase == "outro") k = 0.5 * (1.0 - tOut / OutroT);
            else if (tOpen < 0.25f) k = tOpen / 0.25;
            else k = Math.Max(0.0, 1.0 - (tOpen - 0.25) / Math.Max(0.1, pickAt - 0.25)) * 0.8;
            var level = Math.Min(3, (int)(k * 4));
            if (k <= 0.05) return;
            var levelK = (level + 1) * 0.25;
            var col = Glows.Scaled(Glows.Scaled(glowColor, 0.9), levelK);
            int cx, cy; if (tOpen < BurstT + 0.4f) { cx = PackCx; cy = PackCy; } else { cx = ScreenW / 2; cy = CardY + CardWidget.CardH / 2; }
            canvas.BlitRotated(RaysSprite(), cx + ox, cy + oy, (float)MathX.PyMod(time * 18.0, 360), 1f, col, Blend.Add);
        }

        private void DrawPackPieces(Canvas canvas, int ox, int oy)
        {
            if (tOpen > 1.2f) return;
            var rect = PackRect; var top = tearY - 10; var seam = palette[0];
            var fade = Clamp01((tOpen - 0.35f) / 0.6f);
            if (fade < 1f)
            {
                var dy = tOpen > 0.1f ? body[0] : 0f;
                var y0 = rect.Y + top + (int)Math.Max(0f, dy) + oy;
                canvas.PushAlpha(1f - fade);
                DrawColumns(canvas, rect.X + ox, y0, top, PackH, true, seam);
                canvas.PopAlpha();
            }
            if (tOpen < 1.1f)
            {
                var stripH = tearY + 10;
                var c = (rect.CenterX + strip[0] + ox, rect.Y + stripH / 2f + strip[1] + oy);
                canvas.PushAlpha(1f - Clamp01((tOpen - 0.5f) / 0.6f));
                canvas.PushRotateScale((int)c.Item1, (int)c.Item2, strip[4], 1f);
                DrawColumns(canvas, (int)c.Item1 - packW / 2, (int)c.Item2 - stripH / 2, 0, stripH, false, seam);
                canvas.PopTransform(); canvas.PopAlpha();
            }
        }

        /// <summary>The torn strip (rows above the jagged edge) or body (below), column runs with the same edge height.</summary>
        private void DrawColumns(Canvas canvas, int x0, int y0, int srcTop, int srcBottom, bool isBody, Rgba seam)
        {
            var w = packW; var x = 0;
            while (x < w)
            {
                var ey = tearEdge[x]; var end = x + 1;
                while (end < w && tearEdge[end] == ey) end++;
                var run = end - x;
                if (isBody)
                {
                    var from = Math.Max(srcTop, ey);
                    if (srcBottom > from) PackSprite(canvas, x, from, run, srcBottom - from, new RectF(x0 + x, y0 + (from - srcTop), run, srcBottom - from));
                    if (ey - srcTop >= 0 && ey < srcBottom) canvas.Fill(new RectF(x0 + x, y0 + ey - srcTop, run, 1), seam);
                }
                else
                {
                    var to = Math.Min(srcBottom, ey);
                    if (to > 0) PackSprite(canvas, x, 0, run, to, new RectF(x0 + x, y0, run, to));
                    if (ey - 1 >= 0 && ey - 1 < srcBottom) canvas.Fill(new RectF(x0 + x, y0 + ey - 1, run, 1), seam);
                }
                x = end;
            }
        }

        private void BlitCard(Canvas canvas, Sprite img, CardDefinition card, bool face, float cx, float cy, float sx = 1, float sy = 1, float angle = 0, int alpha = 255)
        {
            var w = Math.Max(1, (int)(CardWidget.CardW * Math.Abs(sx))); var h = Math.Max(1, (int)(CardWidget.CardH * sy));
            var dst = RectF.CenteredInt((int)cx, (int)cy, w, h);
            if (angle != 0) canvas.PushRotateScale((int)cx, (int)cy, angle, 1f);
            if (alpha < 255) canvas.PushAlpha(Math.Max(0, alpha) / 255f);
            canvas.BlitRect(img, dst, Rgba.White);
            if (face && card.Chroma != ChromaKind.None && alpha >= 255)
            {
                ChromaFx.Sheen(canvas, dst, card.Chroma, time, img);
                ChromaFx.DrawTwinkles(canvas, dst, card.Chroma, time, Math.Abs(sx));
            }
            if (alpha < 255) canvas.PopAlpha();
            if (angle != 0) canvas.PopTransform();
        }

        private void Halo(Canvas canvas, OfferedCard offer, float cx, float cy, double k)
        {
            if (!IsSpecial(offer) || k <= 0) return;
            var c = offer.Card; var r = Rarity(c);
            k = MathX.Round(Math.Min(1.0, k) * 8) / 8.0;
            var col = c.Chroma != ChromaKind.None ? Glows.Scaled(ChromaFx.Style(c.Chroma).Glow, k * 0.6) : Glows.Scaled(RarityPalette[r][1], k * (0.35 + 0.12 * (r - 3)));
            Glows.DrawAt(canvas, Art, (int)cx - 125, (int)cy - 125, 125, col);
        }

        private Sprite FaceOf(CardDefinition c) => CardWidget.Face(Game, c, bonus.For(c));

        private void DrawCards(Canvas canvas, string phase, int ox, int oy)
        {
            if (phase == "pick") { DrawPick(canvas, ox, oy); return; }
            if (phase == "outro") { DrawOutro(canvas, ox, oy); return; }
            float px = PackCx, py = PackCy - 30;
            for (var i = 0; i < cards.Count; i++)
            {
                var offer = cards[i]; var start = BurstT + i * DealStagger;
                if (tOpen < start) continue;
                var (tx, ty) = SlotCenter(i);
                var u = EaseOutCubic((tOpen - start) / DealEach);
                var x = px + (tx - px) * u; var y = py + (ty - py) * u - 70 * (float)Math.Sin(Math.PI * u);
                var scale = 0.35f + 0.65f * u; var angle = dealAngle[i] * (1f - u);
                var fs = flipStart[i]; var f = Clamp01((tOpen - fs) / FlipT);
                var pulse = 1f + 0.12f * (float)Math.Sin(Math.PI * f);
                var sx = (float)Math.Abs(Math.Cos(Math.PI * f)) * scale * pulse;
                if (IsSpecial(offer))
                {
                    var pre = Clamp01((tOpen - (fs - AnticipationT)) / AnticipationT);
                    Halo(canvas, offer, x + ox, y + oy, f < 1f ? pre * 1.4 : 1.0);
                    if (f == 0f && pre > 0) x += (float)Math.Sin(time * 60.0) * 2.5f * pre;
                }
                var isFace = f >= 0.5f;
                BlitCard(canvas, isFace ? FaceOf(offer.Card) : Art.CardBack, offer.Card, isFace, x + ox, y + oy, sx, scale * pulse, angle);
            }
        }

        private void DrawPick(Canvas canvas, int ox, int oy)
        {
            var n = cards.Count; var totalW = n * CardWidget.CardW + Math.Max(0, n - 1) * Gap; var startX = ScreenW / 2 - totalW / 2;
            cardRects.Clear();
            for (var i = 0; i < n; i++)
            {
                var offer = cards[i]; var (cx, cy) = SlotCenter(i);
                var pulse = 0.85 + 0.15 * Math.Sin(time * 3.0 + i);
                Halo(canvas, offer, cx + ox, cy + oy - (hovered == i ? 20 : 0), pulse * (hovered == i ? 1.25 : 1.0));
                var x = startX + i * (CardWidget.CardW + Gap) + ox;
                if (offer.Lucky)
                {
                    var lift = hovered == i || chosen.Contains(i) ? -20 : 0;
                    CardFx.DrawLuckyBack(canvas, Art, new RectF(x, CardY + oy + lift, CardWidget.CardW, CardWidget.CardH), time);
                }
                var rect = CardWidget.Draw(canvas, Game, offer.Card, x, CardY + oy, bonus.For(offer.Card), chosen.Contains(i), hovered == i);
                if (offer.Lucky) CardFx.DrawLuckyFront(canvas, Game, rect, time);
                cardRects.Add(rect.Move(-ox, -oy));
            }
        }

        private void DrawOutro(Canvas canvas, int ox, int oy)
        {
            var u = EaseOutCubic(tOut / OutroT);
            for (var i = 0; i < cards.Count; i++)
            {
                if (chosen.Contains(i)) continue;
                var (cx, cy) = SlotCenter(i);
                BlitCard(canvas, FaceOf(cards[i].Card), cards[i].Card, true, cx + ox, cy + 60 * u + oy, 1, 1, 0, (int)(255 * (1f - u)));
            }
            var n = chosen.Count;
            for (var k = 0; k < n; k++)
            {
                var c = cards[chosen[k]].Card; var (sx, sy) = SlotCenter(chosen[k]);
                var tx = ScreenW / 2f + (k - (n - 1) / 2f) * (CardWidget.CardW * 1.35f + 30); var ty = ScreenH / 2f - 20;
                var x = sx + (tx - sx) * u; var y = sy - 20 + (ty - sy + 20) * u;
                var scale = 1f + 0.35f * u;
                var radius = (int)(150 * scale);
                Glows.DrawAt(canvas, Art, (int)x - radius + ox, (int)y - radius + oy, radius, Glows.Scaled(RevealPalette(c)[1], 0.55));
                BlitCard(canvas, FaceOf(c), c, true, x + ox, y + oy, scale, scale);
            }
        }

        private void DrawUi(Canvas canvas, string phase)
        {
            const int cx = 640;
            skipRect = null;
            if (phase == "intro" || phase == "idle" || phase == "charge")
            {
                Fonts.Get(24).DrawCentered(canvas, packName, cx, 50, Colors.TextAccent);
                if (phase == "idle")
                {
                    var a = (int)(150 + 105 * (0.5 + 0.5 * Math.Sin(time * 3.2)));
                    Fonts.Get(15).DrawCentered(canvas, "Haz clic en el sobre para abrirlo", cx, PackCy + PackH / 2 + 40, Colors.TextPrimary.WithAlpha(a));
                }
                return;
            }
            if (phase == "pick")
            {
                Fonts.Get(24).DrawCentered(canvas, "Abriendo: " + packName, cx, 50, Colors.TextAccent);
                var left = picks - chosen.Count;
                var text = picks == 1 ? "Elige una carta para añadir a tu mazo:" : "Sobre " + (packChroma == ChromaKind.Golden ? "dorado" : "especial") + ": elige " + picks + " cartas (te quedan " + left + ")";
                Fonts.Get(13).DrawCentered(canvas, text, cx, 90, Colors.TextPrimary);
                var f = Fonts.Get(14); var label = chosen.Count > 0 ? "Terminar" : "Omitir";
                int w = f.Width(label), h = f.Height, sx = cx - w / 2, sy = CardY + CardWidget.CardH + 40 - h / 2;
                var sr = new RectF(sx - 10, sy - 6, w + 20, h + 12); skipRect = sr;
                canvas.Rect(sr, Colors.BgPanel, 0, 5); canvas.Rect(sr, Colors.PanelBorder, 1, 5);
                f.Draw(canvas, label, sx, sy, Colors.TextSecondary);
                rerollRect = null;
                if (CanReroll)
                {
                    const string rl = "Volver a tirar (Dado Cargado)";
                    int rw = f.Width(rl), rx = cx - rw / 2, ry = CardY + CardWidget.CardH + 84 - h / 2;
                    var rr = new RectF(rx - 10, ry - 6, rw + 20, h + 12); rerollRect = rr;
                    canvas.Rect(rr, Colors.BgPanel, 0, 5); canvas.Rect(rr, Colors.TextAccent, 1, 5);
                    f.Draw(canvas, rl, rx, ry, Colors.TextAccent);
                }
                if (hovered.HasValue && hovered.Value < cards.Count)
                {
                    var c = cards[hovered.Value].Card; var s = bonus.For(c);
                    Tooltip.Draw(canvas, Game, Tooltip.Card(Game, c, s.BonusDamage, s.BonusBlock), mouse);
                }
            }
            else if (phase != "outro") Fonts.Get(12).DrawCentered(canvas, "Clic para saltar", cx, ScreenH - 28, Colors.TextSecondary);
        }

        private void UpdateHover(float x, float y)
        {
            var previous = hovered; hovered = null;
            for (var i = 0; i < cardRects.Count; i++) if (cardRects[i].Contains(x, y)) { hovered = i; if (i != previous) Sound.PlayNav(); return; }
        }

        private void Click(float x, float y)
        {
            if (Cleared || Phase != "pick") return;
            for (var i = 0; i < cardRects.Count; i++) if (cardRects[i].Contains(x, y)) { Choose(i); return; }
            if (rerollRect.HasValue && rerollRect.Value.Contains(x, y)) { Reroll(); return; }
            if (skipRect.HasValue && skipRect.Value.Contains(x, y)) { Sound.PlayCancel(); ChosenCard = ChosenCards.Count > 0 ? ChosenCards[0] : null; Cleared = true; }
        }

        public bool CanReroll => reroll != null && Phase == "pick" && chosen.Count == 0;

        public bool Reroll()
        {
            if (!CanReroll) return false;
            var next = reroll(); reroll = null;
            if (next == null || next.Count == 0) return false;
            Sound.PlayReward();
            cards = next; var n = cards.Count;
            landed = new bool[n]; flipped = new bool[n]; dealAngle = new float[n]; flipStart = new float[n]; sparkleAcc = new float[n];
            for (var i = 0; i < n; i++) { landed[i] = true; flipped[i] = true; }
            picks = Math.Max(1, Math.Min(picks, n)); hovered = null;
            for (var i = 0; i < n; i++) FlipBurst(i);
            return true;
        }

        public void Choose(int i)
        {
            if (Phase != "pick" || i < 0 || i >= cards.Count || chosen.Contains(i)) return;
            Sound.PlayReward();
            chosen.Add(i); ChosenCards.Add(cards[i].Card); ChosenCard = ChosenCards[0];
            if (chosen.Count >= picks) { hovered = null; phaseName = "outro"; tOut = 0; }
            var (x, y) = SlotCenter(i); var pal = RevealPalette(cards[i].Card);
            fx.Burst(x, y - 20, 50, pal, (120f, 420f), null, (0.4f, 0.9f), (2f, 5f), ParticleStyle.Square, 2.5f, 0f, 40f);
            fx.Burst(x, y - 20, 10, Slice(pal, 3), (20f, 120f), null, (0.4f, 0.7f), (18f, 30f), ParticleStyle.Glow, 3f, 0f, 30f);
        }
    }
}
