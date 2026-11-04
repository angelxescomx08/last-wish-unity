using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>scenes/gacha_scene.py: pay gold, turn the crank, open a capsule, keep or decline a random relic.</summary>
    public sealed class GachaScene : Scene
    {
        private const int MachineX = 272, MachineY = 132, FloorY = 548, PanelX = 960, BtnW = 292, BtnH = 112;
        private const float StageX = 742f, StageY = 292f;
        private static readonly IRect BtnNormal = new IRect(PanelX, 136, BtnW, BtnH), BtnStellar = new IRect(PanelX, 258, BtnW, BtnH), Exit = new IRect(1102, 658, 150, 36);
        /// <summary>The relic counter in the top bar, left of the pause buttons: where kept relics fly.</summary>
        private static readonly IRect RelicPlate = new IRect(24, 16, 196, 36);
        private IRect btnKeep = new IRect(0, 0, 196, 48), btnDecline = new IRect(0, 0, 172, 36);
        private const int PurseX = 1200, PurseY = 35, RelicCounterX = 122, RelicCounterY = 34;
        public const float CoinT = 0.75f, CrankT = 1.9f, ThunkAt = 0.86f, FallT = 0.78f, HopT = 0.55f, DropT = FallT + HopT, ShakeT = 0.34f, PresentAuto = 6f, OpenT = 1.05f, CollectT = 0.55f, DiscardT = 0.6f;
        private static readonly Rgba[] Gold = { new Rgba(255, 250, 210), new Rgba(255, 214, 90), new Rgba(230, 150, 30), new Rgba(140, 80, 10) };
        private static readonly Rgba[] Dust = { new Rgba(150, 140, 140), new Rgba(110, 100, 104), new Rgba(80, 72, 78) };
        private static readonly Rgba White = Rgba.White;

        private static float Clamp01(float t) => t < 0 ? 0 : t > 1 ? 1 : t;
        private static float EaseOutCubic(float t) { t = Clamp01(t); return 1 - (1 - t) * (1 - t) * (1 - t); }
        private static float Smoothstep(float t) { t = Clamp01(t); return t * t * (3 - 2 * t); }
        private static float EaseOutBack(float t, float s = 1.9f) { t = Clamp01(t) - 1; return 1 + (s + 1) * t * t * t + s * t * t; }
        public static Rgba TierColor(int rarity) => CardWidget.RarityColor(rarity);
        public static Rgba[] TierPalette(int rarity) { var b = TierColor(rarity); return new[] { White, Glows.Scaled(b, 1.25), b, Glows.Scaled(b, 0.6) }; }
        public static string DescText(RelicOffer r) => r.Chroma == ChromaKind.None ? r.Definition.Description : r.Definition.Description + " " + ChromaFx.Style(r.Chroma).ShortNote + ".";

        private readonly RunState run;
        private readonly DungeonBackdrop backdrop;
        private readonly PyRandom rng;
        private readonly BurstParticles fx;
        private float time, t; private string phase = "idle";
        private PullResult result;
        private float shake, flash, feedbackT, sparkleClock, focus, pulse, jump, bulbFlash, counterPulse;
        private readonly HashSet<string> fired = new HashSet<string>();
        private string hoveredKey, feedback = "";
        private readonly List<(float x, float y, int ci, float ph)> pile;
        private readonly List<(List<(float x, float y)> pts, Rgba color, float age, float life)> bolts = new List<(List<(float, float)>, Rgba, float, float)>();
        private readonly List<(float x, float y, float age, float life, Rgba color, float rmax)> rings = new List<(float, float, float, float, Rgba, float)>();
        private readonly int scale; private readonly Dictionary<string, object> meta;
        public bool Cleared { get; set; }
        public int PullsMade { get; private set; }
        public string Phase => phase;
        public PullResult Result => result;
        public bool IsAnimating => phase != "idle" && phase != "reveal";
        public override (Anchor anchor, float x, float y)? GoldHudPos => (Anchor.TopRight, 1268, 12);
        /// <summary>The room's interface boxes (tests: nothing overlaps, everything is on screen).</summary>
        public static IRect PullButton(PullKind kind) => kind == PullKind.Stellar ? BtnStellar : BtnNormal;
        public static IRect ExitButton => Exit;
        public static IRect RelicCounter => RelicPlate;
        public static IRect OddsBox => new IRect(PanelX, OddsTop, BtnW, OddsH);
        /// <summary>The widest the reveal panel can be, centred on the stage.</summary>
        public static IRect RevealBounds => new IRect((int)StageX - RevealMaxW / 2, (int)StageY + 84, RevealMaxW, 0);
        /// <summary>Room for the price on a pull button (the button minus its edges).</summary>
        public const int PriceRoom = BtnW - 48;

        public GachaScene(GameContext game, RunState run, int seed = 3) : base(game)
        {
            this.run = run; rng = new PyRandom(seed); fx = new BurstParticles(900, seed);
            backdrop = new DungeonBackdrop(Art, seed + 11, 0.6f);
            meta = Art.Gacha; scale = Json.Int(meta.Get("scale"), 2);
            pile = MakePile();
        }

        private int Tier => result?.Relic != null ? Math.Max(1, Math.Min(5, result.Relic.Definition.Rarity)) : 1;
        private int TierIndex => Tier - 1;
        private void SetPhase(string name) { phase = name; t = 0; fired.Clear(); }
        private bool Once(string key, float at = 0f) { if (fired.Contains(key) || t < at) return false; fired.Add(key); return true; }

        private (int x, int y) Point(string name) { var p = Json.Ints(meta.Get(name)); return (p[0] * scale, p[1] * scale); }
        private IRect MetaRect(string name) { var r = Json.Ints(meta.Get(name)); return new IRect(r[0] * scale, r[1] * scale, (r[2] - r[0]) * scale, (r[3] - r[1]) * scale); }
        private (float x, float y) MachinePoint(string name) { var (px, py) = Point(name); return (MachineX + px, MachineY + py); }
        private (float x, float y) ChuteMouth() { var r = MetaRect("chute"); return (MachineX + r.CenterX, MachineY + r.CenterY); }
        private IRect Box => MetaRect("box").Move(MachineX, MachineY);

        private Sprite Cell(string file, int size, int index, int row = 0) => new Sprite(Art.Texture("Gacha/" + file), index * size, row * size, size, size);
        private Sprite Small(int i) => Cell("capsule_small", Json.Int(meta.Get("small")), i);
        private Sprite DropSprite(int tier) => Cell("capsule", Json.Int(meta.Get("drop")), tier);
        private Sprite Big(int tier, int part) => Cell("capsule_big", Json.Int(meta.Get("big")), part, tier);
        private Sprite Crank(int i) => Cell("crank", Json.Int(meta.Get("crank_size")), i);
        private Sprite Coin(int i) => Cell("coin", Json.Int(meta.Get("coin")), i);
        private Sprite Flap(int i) { var tex = Art.Texture("Gacha/flap"); var fw = tex.Width / 3; return new Sprite(tex, i * fw, 0, fw, tex.Height); }
        private void Blit2(Canvas canvas, Sprite s, float cx, float cy, float extra = 1f, float angle = 0, Rgba? tint = null, Blend blend = Blend.Alpha)
        {
            int w = (int)(s.W * scale * extra), h = (int)(s.H * scale * extra);
            var dst = RectF.CenteredInt((int)cx, (int)cy, w, h);
            if (angle != 0) canvas.PushRotateScale((int)cx, (int)cy, angle, 1f);
            canvas.BlitRect(s, dst, tint ?? Rgba.White, blend);
            if (angle != 0) canvas.PopTransform();
        }

        private List<(float, float, int, float)> MakePile()
        {
            var box = Box; var r = new PyRandom(17); var list = new List<(float, float, int, float)>();
            const float size = 32; const int rows = 4;
            for (var row = 0; row < rows; row++)
            {
                var n = 7 - row / 2;
                var y = box.Bottom - size * 0.5f - 2 - row * (size * 0.72f);
                for (var k = 0; k < n; k++)
                {
                    double x = box.X + size * 0.6 + k * (box.W - size * 1.2) / Math.Max(1, n - 1);
                    x += (row % 2 == 1 ? size * 0.36 : 0) + r.Uniform(-4, 4);
                    if (row == rows - 1 && r.Random() < 0.4) continue;
                    if (x > box.Right - size * 0.5) continue;
                    var yy = y + r.Uniform(-3, 3); var ci = r.RandRange(6); var ph = r.Uniform(0, MathX.Tau);
                    list.Add(((float)x, (float)yy, ci, (float)ph));
                }
            }
            return list;
        }

        // ------------------------------------------------------------------ input
        public override void HandleEvent(in InputEvent e)
        {
            if (e.Type == EventType.MouseMove)
            {
                var hit = Hit(e.X, e.Y);
                if (hit != null && hit != hoveredKey && (phase == "idle" || phase == "reveal")) Sound.PlayNav();
                hoveredKey = hit;
            }
            else if (e.IsLeftDown) Click(e.X, e.Y);
            else if (e.Type == EventType.KeyDown)
            {
                if (e.Key == Key.Space || e.Key == Key.Return || e.Key == Key.KeypadEnter) Advance();
                else if (e.Key == Key.R || e.Key == Key.Backspace || e.Key == Key.Delete) Decline();
                else if (e.Key == Key.D1) StartPull(PullKind.Normal);
                else if (e.Key == Key.D2) StartPull(PullKind.Stellar);
            }
        }

        private string Hit(float x, float y)
        {
            if (phase == "reveal") return btnKeep.Contains(x, y) ? "keep" : btnDecline.Contains(x, y) ? "decline" : null;
            if (BtnNormal.Contains(x, y)) return "normal";
            if (BtnStellar.Contains(x, y)) return "stellar";
            return Exit.Contains(x, y) ? "exit" : null;
        }

        private void Click(float x, float y)
        {
            var hit = Hit(x, y);
            if (phase == "idle")
            {
                if (hit == "normal") StartPull(PullKind.Normal);
                else if (hit == "stellar") StartPull(PullKind.Stellar);
                else if (hit == "exit") { Sound.PlayCancel(); Cleared = true; }
                return;
            }
            if (phase == "reveal") { if (hit == "keep") Keep(); else if (hit == "decline") Decline(); return; }
            Advance();
        }

        public bool StartPull(PullKind kind)
        {
            if (phase != "idle") return false;
            var r = run.Pull(kind);
            if (!r.Success) { feedback = r.Message; feedbackT = 2f; Sound.PlayError(); return false; }
            result = r; PullsMade++; Sound.PlayGachaCoinToss(); SetPhase("coin");
            return true;
        }

        public void Advance()
        {
            if (phase == "coin" || phase == "crank" || phase == "drop") { Sound.StopGachaChurn(); SetPhase("present"); t = 0.25f; focus = Math.Max(focus, 0.6f); }
            else if (phase == "present") Open();
            else if (phase == "open") SetPhase("reveal");
            else if (phase == "reveal") Keep();
        }

        public bool Keep()
        {
            if (phase != "reveal" || result == null) return false;
            run.AcceptPull(result); TrackPull(true); SetPhase("collect"); Sound.PlayGachaKeep();
            return true;
        }

        /// <summary>Power telemetry: each pull is a one-relic choice, kept or declined.</summary>
        private void TrackPull(bool kept)
        {
            var relic = result?.Relic?.Definition;
            if (relic == null) return;
            Game.Track(p =>
            {
                var choice = p.OfferRelics(run, "gacha", new[] { relic });
                if (kept) { p.Pick(choice, relic.Id); p.RelicGained(run, relic, "gacha"); }
            });
        }

        public bool Decline()
        {
            if (phase != "reveal" || result == null) return false;
            RunState.DeclinePull(result); TrackPull(false); SetPhase("discard");
            fx.Burst(StageX, StageY, 40, Dust, (60f, 240f), null, (0.4f, 0.9f), (3f, 6f), ParticleStyle.Square, 2.5f, -60f);
            fx.Burst(StageX, StageY, 2, new[] { new Rgba(90, 80, 90) }, (0f, 10f), null, (0.3f, 0.5f), (50f, 70f), ParticleStyle.Glow, 4f);
            Sound.PlayGachaDust();
            return true;
        }

        public void SkipToReveal() { var guard = 0; while (phase != "reveal" && phase != "idle" && guard++ < 10) Advance(); }

        // ------------------------------------------------------------------ simulation
        public override void Update(float dt)
        {
            dt = Math.Min(0.1f, Math.Max(0f, dt));
            time += dt; t += dt;
            backdrop.Update(dt);
            feedbackT = Math.Max(0f, feedbackT - dt); shake = Math.Max(0f, shake - dt * 30); flash = Math.Max(0f, flash - dt * 3.2f);
            pulse = Math.Max(0f, pulse - dt * 5f); counterPulse = Math.Max(0f, counterPulse - dt * 2.2f); jump = Math.Max(0f, jump - dt * 3.5f); bulbFlash = Math.Max(0f, bulbFlash - dt * 2f);
            float target = phase == "coin" ? 0.55f : phase == "crank" ? 1f : phase == "drop" ? 0.9f : phase == "present" ? 0.75f : phase == "open" ? 0.7f : phase == "reveal" ? 0.6f : 0f;
            focus += (target - focus) * Math.Min(1f, dt * 4f);
            for (var i = bolts.Count - 1; i >= 0; i--) { var b = bolts[i]; b.age += dt; if (b.age < b.life) bolts[i] = b; else bolts.RemoveAt(i); }
            for (var i = rings.Count - 1; i >= 0; i--) { var r = rings[i]; r.age += dt; if (r.age < r.life) rings[i] = r; else rings.RemoveAt(i); }
            switch (phase)
            {
                case "coin": UpdateCoin(); break;
                case "crank": UpdateCrank(); break;
                case "drop": UpdateDrop(); break;
                case "present": UpdatePresent(); break;
                case "open": if (t >= OpenT) SetPhase("reveal"); break;
                case "collect":
                    if (t >= CollectT) { fx.Burst(RelicCounterX, RelicCounterY, 16, TierPalette(Tier), (60f, 200f), null, (0.3f, 0.6f), (1.5f, 3f), ParticleStyle.Spark, 3f); Sound.PlayGachaStash(); counterPulse = 1f; SetPhase("idle"); }
                    break;
                case "discard": if (t >= DiscardT) SetPhase("idle"); break;
            }
            Ambient(dt);
            fx.Update(dt);
        }

        private void UpdateCoin()
        {
            if (Once("clink", CoinT * 0.72f))
            {
                var (x, y) = MachinePoint("slot");
                fx.Burst(x, y, 26, Gold, (80f, 300f), null, (0.2f, 0.5f), (1.5f, 2.5f), ParticleStyle.Spark, 4f);
                fx.Burst(x, y, 2, Gold, (0f, 10f), null, (0.25f, 0.35f), (34f, 44f), ParticleStyle.Glow, 4f);
                rings.Add((x, y, 0f, 0.4f, new Rgba(255, 214, 90), 46f));
                bulbFlash = 1f; pulse = 1f; Sound.PlayGachaCoinSlot();
            }
            if (t >= CoinT) SetPhase("crank");
        }
        private float CrankU => Clamp01(t / CrankT);

        private void UpdateCrank()
        {
            const int turns = 12;
            if (Once("churn")) Sound.PlayGachaChurn();
            for (var k = 0; k < turns; k++)
            {
                var at = CrankT * ThunkAt * (1 - (float)Math.Pow(1 - k / (double)turns, 1.35));
                if (Once("tick" + k, at))
                {
                    var (x, y) = MachinePoint("crank");
                    fx.Burst(x, y, 6 + k, Gold, (60f, 180f + 10 * k), null, (0.15f, 0.35f), (1f, 2.5f), ParticleStyle.Spark, 5f);
                    pulse = 0.6f + 0.4f * k / turns; Sound.PlayGachaCrank(k, turns);
                }
            }
            var tier = TierIndex; var box = Box; var u = CrankU;
            var chance = (0.25 + 0.6 * u) * (tier >= 3 ? 1.0 : 0.45);
            if (0.15f < u && u < ThunkAt && rng.Random() < chance) SpawnBolt(box, tier >= 3 ? TierColor(Tier) : new Rgba(200, 220, 255), tier >= 3);
            if (tier >= 3 && Once("tease", CrankT * 0.55f))
            { fx.Implode(box.CenterX, box.CenterY, 40, TierPalette(Tier), (120f, 200f), (0.35f, 0.55f)); flash = 0.25f; Sound.PlayGachaTease(); }
            if (Once("thunk", CrankT * ThunkAt))
            {
                jump = 1f; shake = 6 + 2 * tier; bulbFlash = 1f;
                var (cx, cy) = ChuteMouth();
                fx.Burst(cx, cy - 10, 30 + 10 * tier, TierPalette(Tier), (120f, 380f), ((float)(Math.PI * 1.05), (float)(Math.PI * 1.95)), (0.3f, 0.7f), (2f, 4f), ParticleStyle.Square, 2.5f, 300f);
                rings.Add((cx, cy, 0f, 0.45f, TierColor(Tier), 120f)); Sound.PlayGachaThunk();
            }
            if (t >= CrankT) SetPhase("drop");
        }

        private void SpawnBolt(IRect box, Rgba color, bool big)
        {
            var r = rng;
            (float, float) EdgePoint()
            {
                var side = r.RandRange(4);
                if (side == 0) return ((float)r.Uniform(box.X, box.Right), box.Y + 4);
                if (side == 1) return ((float)r.Uniform(box.X, box.Right), box.Bottom - 4);
                if (side == 2) return (box.X + 4, (float)r.Uniform(box.Y, box.Bottom));
                return (box.Right - 4, (float)r.Uniform(box.Y, box.Bottom));
            }
            var p0 = EdgePoint(); (float x, float y) p1; int n; float jitter, life;
            if (big) { var dx = r.Uniform(-30, 30); var dy = r.Uniform(-30, 30); p1 = ((float)(box.CenterX + dx), (float)(box.CenterY + dy)); n = 9; jitter = 14; life = 0.18f; }
            else { var ang = r.Uniform(0, MathX.Tau); p1 = (p0.Item1 + (float)Math.Cos(ang) * 26, p0.Item2 + (float)Math.Sin(ang) * 26); n = 4; jitter = 6; life = 0.12f; }
            var pts = new List<(float x, float y)> { p0 };
            for (var k = 1; k < n; k++)
            {
                var tt = k / (float)n; var jx = r.Uniform(-jitter, jitter); var jy = r.Uniform(-jitter, jitter);
                pts.Add((p0.Item1 + (p1.x - p0.Item1) * tt + (float)jx, p0.Item2 + (p1.y - p0.Item2) * tt + (float)jy));
            }
            pts.Add(p1);
            bolts.Add((pts, color, 0f, life));
            fx.Burst(p1.x, p1.y, big ? 5 : 2, new[] { White, color }, (40f, 160f), null, (0.1f, 0.25f), (1f, 2f), ParticleStyle.Spark, 5f);
            Sound.PlayGachaZap(big);
        }

        private (float x, float y, float grow) DropPos(float tt)
        {
            var (x0, y0) = ChuteMouth(); float floor = FloorY;
            if (tt < FallT)
            {
                var x = x0 + 70 * EaseOutCubic(tt / FallT);
                const double g = 2600.0;
                var t1 = Math.Sqrt(2 * (floor - y0) / g);
                if (tt < t1) return (x, (float)(y0 + 0.5 * g * tt * tt), 0f);
                var v = g * t1 * 0.42; var tb = tt - t1;
                for (var i = 0; i < 3; i++)
                {
                    var flight = 2 * v / g;
                    if (tb < flight) return (x, (float)(floor - (v * tb - 0.5 * g * tb * tb)), 0f);
                    tb -= flight; v *= 0.42;
                }
                return (x, floor, 0f);
            }
            var u = Clamp01((tt - FallT) / HopT);
            float sx = x0 + 70, sy = floor; var e = EaseOutCubic(u);
            return (sx + (StageX - sx) * e, sy + (StageY - sy) * e - 150 * (float)Math.Sin(Math.PI * u), e);
        }

        private void UpdateDrop()
        {
            var (x0, y0) = ChuteMouth(); var tier = TierIndex;
            if (Once("out"))
            {
                var pal = TierPalette(Tier);
                fx.Burst(x0, y0, 16, Dust, (30f, 140f), null, (0.3f, 0.6f), (2f, 4f), ParticleStyle.Square, 3f, -40f);
                fx.Burst(x0, y0, 24 + 8 * tier, pal, (160f, 420f), ((float)(Math.PI * 0.15), (float)(Math.PI * 0.85)), (0.2f, 0.45f), (1.5f, 2.5f), ParticleStyle.Spark, 3f);
                rings.Add((x0, y0, 0f, 0.5f, TierColor(Tier), 90f));
                flash = Math.Max(flash, 0.15f + 0.05f * tier);
                Sound.PlayGachaPop();
            }
            const double g = 2600.0;
            var t1 = Math.Sqrt(2 * (FloorY - y0) / g); var v = g * t1 * 0.42;
            var hits = new[] { t1, t1 + 2 * v / g };
            for (var k = 0; k < 2; k++)
                if (Once("bounce" + k, (float)hits[k]))
                {
                    var (x, _, _) = DropPos((float)hits[k]);
                    fx.Burst(x, FloorY + 10, 14 - 4 * k, Dust, (40f, 180f), ((float)Math.PI, (float)MathX.Tau), (0.3f, 0.6f), (2f, 4f), ParticleStyle.Square, 3f, 200f);
                    rings.Add((x, FloorY + 14, 0f, 0.3f, new Rgba(200, 190, 180), 50 - 15 * k)); Sound.PlayGachaBounce(k);
                }
            if (Once("hop", FallT)) Sound.PlayGachaHop(tier >= 1);
            if (t > FallT && tier >= 1)
            {
                var (x, y, _) = DropPos(t);
                for (var i = 0; i < 1 + tier; i++)
                {
                    var px = rng.Uniform(-12, 12); var py = rng.Uniform(-12, 12); var vx = rng.Uniform(-30, 30); var vy = rng.Uniform(-60, 0);
                    fx.Emit(x + (float)px, y + (float)py, (float)vx, (float)vy, 0.5f, TierPalette(Tier), 2.5f);
                }
            }
            if (t >= DropT) SetPhase("present");
        }

        private int Shakes => TierIndex + 1;

        private void UpdatePresent()
        {
            for (var k = 0; k < Shakes; k++)
                if (Once("shake" + k, 0.35f + k * (ShakeT + 0.12f)))
                { fx.Implode(StageX, StageY, 14 + 6 * k, TierPalette(Tier), (70f, 130f), (0.28f, 0.4f)); Sound.PlayGachaWobble(k); }
            if (t >= PresentAuto) Open();
        }

        private void Open()
        {
            SetPhase("open");
            var tier = TierIndex; var pal = TierPalette(Tier);
            flash = 0.75f + 0.08f * tier; shake = new[] { 0f, 0f, 2f, 7f, 11f }[tier];
            fx.Burst(StageX, StageY, 30 + 22 * tier, pal, (140f, 460f + 60 * tier), null, (0.4f, 1f), (2f, 4f), ParticleStyle.Square, 2.2f, 160f);
            fx.Burst(StageX, StageY, 16 + 8 * tier, pal, (200f, 520f), null, (0.2f, 0.5f), (1.5f, 2.5f), ParticleStyle.Spark, 3.5f);
            fx.Burst(StageX, StageY, 3, pal, (0f, 20f), null, (0.35f, 0.6f), (60f, 90f + 15 * tier), ParticleStyle.Glow, 4f);
            if (Tier == 5)
                for (var i = 0; i < 90; i++)
                {
                    var px = rng.Uniform(-260, 260); var py = rng.Uniform(140, 300); var vx = rng.Uniform(-40, 40); var vy = rng.Uniform(40, 160); var life = rng.Uniform(1.2, 2.2); var size = rng.Uniform(2.5, 4.5);
                    fx.Emit(StageX + (float)px, StageY - (float)py, (float)vx, (float)vy, (float)life, Gold, (float)size, ParticleStyle.Square, 0.6f, 120f);
                }
            Sound.PlayGachaOpen();
            Sound.PlayGachaTier(Tier);
            if (result?.Relic != null && result.Relic.Chroma != ChromaKind.None) Sound.PlayGachaGolden();
        }

        private void Ambient(float dt)
        {
            sparkleClock -= dt;
            if (sparkleClock > 0) return;
            sparkleClock = (float)rng.Uniform(0.08, 0.2);
            if (phase == "idle" || phase == "coin" || phase == "crank")
            {
                var box = Box;
                var x = rng.Uniform(box.X + 6, box.Right - 6); var y = rng.Uniform(box.Y + 6, box.Bottom - 6); var vy = rng.Uniform(-12, -4); var life = rng.Uniform(0.4, 0.8); var size = rng.Uniform(1.0, 2.0);
                fx.Emit((float)x, (float)y, 0, (float)vy, (float)life, new[] { White, new Rgba(220, 210, 255) }, (float)size);
            }
            else if ((phase == "present" || phase == "reveal" || phase == "open") && TierIndex >= 1)
            {
                var a = rng.Uniform(0, MathX.Tau); var d = rng.Uniform(50, 110); var vx = rng.Uniform(-8, 8); var vy = rng.Uniform(-60, -25); var life = rng.Uniform(0.8, 1.4); var size = rng.Uniform(1.5, 3.0);
                fx.Emit(StageX + (float)(Math.Cos(a) * d), StageY + (float)(Math.Sin(a) * d * 0.6) + 30, (float)vx, (float)vy, (float)life, TierPalette(Tier), (float)size, ParticleStyle.Square, 0.4f);
            }
        }

        // ------------------------------------------------------------------ procedural overlays
        private Sprite Vignette() => new Sprite(Art.Procedural("gacha_vignette", 160, 90, () =>
        {
            var px = new Rgba[160 * 90]; const double cx = 560 / 8.0, cy = 330 / 8.0;
            for (var y = 0; y < 90; y++)
                for (var x = 0; x < 160; x++)
                {
                    var d = Math.Sqrt(Math.Pow((x - cx) / (420 / 8.0), 2) + Math.Pow((y - cy) / (300 / 8.0), 2));
                    px[y * 160 + x] = new Rgba(4, 2, 8, (int)(255 * Math.Pow(MathX.Clamp01((d - 0.55) / 0.7), 1.3)));
                }
            return px;
        }, smooth: true));

        private Sprite Band() => new Sprite(Art.Procedural("gacha_band", 1, 92, () =>
        {
            var px = new Rgba[92];
            for (var y = 0; y < 92; y++) px[y] = new Rgba(6, 4, 12, (int)(210 * Math.Pow(1 - y / 92.0, 1.4)));
            return px;
        }));

        private Sprite Beam() => new Sprite(Art.Procedural("gacha_beam", 720, 720, () =>
        {
            const int L = 360; var px = new Rgba[720 * 720];
            for (var y = 0; y < 720; y++)
                for (var x = 0; x < 720; x++)
                {
                    double dx = x + 0.5 - L, dy = y + 0.5 - L; var a = Math.Atan2(dy, dx); Rgba c = Rgba.Black;
                    for (var i = 23; i >= 0; i--)
                    {
                        var k = i / 24.0; var spread = 0.26 * (1 - k * 0.4); var reach = L * (1 - k * 0.5);
                        if (Math.Abs(a) <= spread && dx <= reach * Math.Cos(spread))
                        { c = Glows.Scaled(new Rgba(150, 130, 95), Math.Pow(1 - k, 0.8)); break; }
                    }
                    px[y * 720 + x] = c;
                }
            return px;
        }, smooth: true));

        private Sprite Rays(int rarity) => new Sprite(Art.Procedural("gacha_rays", 640, 640, () =>
        {
            const int size = 640, r = 320, n = 12; var px = new Rgba[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    double dx = x + 0.5 - r, dy = y + 0.5 - r; var d = Math.Sqrt(dx * dx + dy * dy);
                    var seg = MathX.PyMod(MathX.PyMod(Math.Atan2(dy, dx), MathX.Tau), MathX.Tau / n);
                    var rr = (int)Math.Ceiling(d - 0.0001);
                    var glow = rr <= r ? Math.Pow(1.0 - Math.Max(1, rr) / (double)r, 2) : 0;
                    var v = seg <= MathX.Tau / n * 0.45 && d <= r ? (int)(255 * glow) : 0;
                    px[y * size + x] = new Rgba(v, v, v, 255);
                }
            return px;
        }, smooth: true));

        // ------------------------------------------------------------------ drawing
        public override void Draw(Canvas canvas)
        {
            backdrop.Draw(canvas);
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(8, 4, 14, 120));
            if (focus > 0.02f) canvas.BlitRect(Vignette(), new RectF(0, 0, Canvas.Width, Canvas.Height), Rgba.White.WithAlpha((int)(235 * Math.Min(1f, focus))));
            int ox = 0, oy = 0;
            if (shake > 0) { ox = (int)rng.Uniform(-shake, shake); oy = (int)rng.Uniform(-shake, shake); }
            DrawBeams(canvas, ox, oy); DrawMachine(canvas, ox, oy); DrawBolts(canvas, ox, oy); DrawRings(canvas, ox, oy); DrawPrize(canvas, ox, oy);
            fx.Draw(canvas, Art);
            DrawPanel(canvas); DrawRevealText(canvas);
            if (flash > 0) canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), Glows.Scaled(White, 0.8 * Math.Min(1f, flash)), Blend.Add);
        }

        private void DrawBeams(Canvas canvas, int ox, int oy)
        {
            if (phase != "crank" && phase != "drop") return;
            var k = phase == "crank" ? Clamp01(t / 0.4f) : Math.Max(0f, 1 - t / 0.5f);
            if (k <= 0.02f) return;
            float cx = MachineX + 128 + ox, cy = MachineY + 8 + oy;
            var tint = TierIndex >= 3 && CrankU > 0.5f ? TierColor(Tier) : new Rgba(255, 230, 170);
            foreach (var side in new[] { -1, 1 })
            {
                var ang = -90 + side * (35 + 28 * Math.Sin(time * 3.2 + side));
                canvas.BlitRotated(Beam(), cx, cy, (float)-ang, 1f, Glows.Scaled(tint, 0.9 * k), Blend.Add);
            }
        }

        private void DrawBolts(Canvas canvas, int ox, int oy)
        {
            foreach (var (pts, color, age, life) in bolts)
            {
                var k = 1 - age / life;
                var shifted = new List<(float x, float y)>(pts.Count);
                foreach (var (x, y) in pts) shifted.Add((x + ox, y + oy));
                canvas.Lines(shifted, false, Glows.Scaled(color, 0.55 * k + 0.2), 4);
                canvas.Lines(shifted, false, Glows.Scaled(White, 0.6 + 0.4 * k), 1);
                Glows.Draw(canvas, Art, (int)shifted[shifted.Count - 1].x, (int)shifted[shifted.Count - 1].y, 20, Glows.Scaled(color, 0.5 * k));
            }
        }

        private void DrawRings(Canvas canvas, int ox, int oy)
        {
            foreach (var (x, y, age, life, color, rmax) in rings)
            {
                var u = age / life; var r = (int)(6 + (rmax - 6) * EaseOutCubic(u)); var w = Math.Max(1, (int)(5 * (1 - u)));
                canvas.Ellipse(new RectF((int)(x - r + ox), (int)(y - r * 0.45f + oy), 2 * r, (int)(r * 0.9f)), Glows.Scaled(color, 1 - u * 0.7), w);
            }
        }

        private (int x, int y) MachineJitter()
        {
            if (phase == "crank")
            {
                var u = CrankU; var amp = 1.0 + 2.0 * u;
                return ((int)Math.Round(Math.Sin(t * (30 + 30 * u)) * amp, MidpointRounding.ToEven), (int)Math.Round(-Math.Abs(Math.Sin(t * (15 + 15 * u))) * amp, MidpointRounding.ToEven));
            }
            if (phase == "coin" && CoinT * 0.72f < t && t < CoinT * 0.9f) return (0, 1);
            return (0, 0);
        }

        private float Swirl()
        {
            if (phase != "crank") return 0f;
            var u = CrankU;
            if (u < 0.18f) return Smoothstep(u / 0.18f);
            if (u < ThunkAt - 0.1f) return 1f;
            return 1f - Smoothstep((u - (ThunkAt - 0.1f)) / (1f - ThunkAt + 0.1f));
        }

        private void DrawMachine(Canvas canvas, int ox, int oy)
        {
            var (jx, jy) = MachineJitter();
            int bx = MachineX + ox + jx, by = MachineY + oy + jy;
            Glows.Draw(canvas, Art, bx + 128, by + 200, 240, Glows.Scaled(new Rgba(90, 26, 64), 0.35 + 0.65 * focus));
            const int pad = 24;
            var machine = new Sprite(Art.Texture("Gacha/machine")); int mw = machine.W * scale, mh = machine.H * scale;
            int lw = mw + 2 * pad, lh = mh + 2 * pad;
            var swirl = Swirl();
            var zoom = 1f + 0.06f * swirl;
            var sx = zoom + 0.035f * pulse - 0.04f * jump; var sy = zoom - 0.03f * pulse + 0.06f * jump;
            var hop = (int)(-18 * Math.Sin(Math.PI * Math.Min(1f, jump)) * (jump > 0.5f ? 1 : 0.3));
            var scaled = Math.Abs(sx - 1) > 0.002f || Math.Abs(sy - 1) > 0.002f;
            int iw = scaled ? (int)(lw * sx) : lw, ih = scaled ? (int)(lh * sy) : lh;
            // the layer (machine + capsules + glass) is drawn with a transform that maps layer px to its scaled rect
            var left = bx + mw / 2 - iw / 2; var top = by + mh + pad + hop - ih;
            float kx = iw / (float)lw, ky = ih / (float)lh;
            canvas.PushTransform(Mat2D.Scale(kx, ky).Then(Mat2D.Translate(left, top)));
            const int mx = pad, my = pad;
            canvas.BlitRect(machine, new RectF(mx, my, mw, mh), Rgba.White);
            var box = MetaRect("box").Move(mx, my);
            canvas.PushClip(new RectF(left + box.X * kx, top + box.Y * ky, box.W * kx, box.H * ky));
            var spin = time * (2f + 9f * swirl);
            float cxv = box.CenterX, cyv = box.CenterY + 6;
            var n = pile.Count; var baseBox = Box;
            var order = new List<(double depth, float px, float py, int ci)>(n);
            for (var i = 0; i < n; i++)
            {
                var (x, y, ci, ph) = pile[i];
                var rx = x - baseBox.X + box.X; var ry = y - baseBox.Y + box.Y;
                var bob = (float)Math.Sin(time * 1.6 + ph) * 0.8f;
                if (swirl > 0)
                {
                    var ang = spin * (1.0 + 0.25 * (i % 3)) + ph * 2 + i * 0.7;
                    var rad = 26 + i * 37 % 64; var h = -46 + i / (double)Math.Max(1, n - 1) * 88;
                    var tx = cxv + Math.Cos(ang) * rad; var ty = cyv + h + Math.Sin(ang) * rad * 0.28;
                    order.Add((Math.Sin(ang), (float)(rx + (tx - rx) * swirl), (float)(ry + (ty - ry) * swirl), ci));
                }
                else order.Add((0.0, rx, ry - bob, ci));
            }
            order.Sort((a, b) => a.depth.CompareTo(b.depth));
            var smallColors = Json.Int(meta.Get("small_colors"), 6);
            foreach (var (_, px, py, ci) in order) Blit2(canvas, Small(ci % smallColors), (int)px, (int)py);
            var u = phase == "crank" ? CrankU : 0f;
            if (phase == "crank" && u > 0.5f)
            {
                var k = Clamp01((u - 0.5f) / (ThunkAt - 0.5f));
                float px = cxv, py = box.Y + 30 + (box.H - 20) * Smoothstep(k);
                Glows.Draw(canvas, Art, (int)px, (int)py, 40, Glows.Scaled(TierColor(Tier), 0.5 + 0.3 * Math.Sin(time * 20)));
                if (k < 1f) Blit2(canvas, DropSprite(TierIndex), (int)px, (int)py);
            }
            canvas.PopClip();
            var glass = new Sprite(Art.Texture("Gacha/glass"));
            canvas.BlitRect(glass, new RectF(mx, my, glass.W * scale, glass.H * scale), Rgba.White);
            DrawBulbs(canvas, mx, my);
            var frames = Json.Int(meta.Get("crank_frames"), 8); var fi = 0;
            if (phase == "crank") { var turns = 3.0 * Smoothstep(Math.Min(1f, u / ThunkAt)); fi = (int)(turns * frames) % frames; }
            var (ccx, ccy) = Point("crank");
            Blit2(canvas, Crank(fi), mx + ccx, my + ccy);
            if (phase == "coin" && t > CoinT * 0.65f || bulbFlash > 0.3f) { var (ssx, ssy) = Point("slot"); Glows.Draw(canvas, Art, mx + ssx, my + ssy, 26, new Rgba(255, 200, 80)); }
            var flap = phase == "drop" && t < 0.35f ? (t < 0.2f ? 2 : 1) : 0;
            var ch = MetaRect("chute"); var fs = Flap(flap);
            canvas.BlitRect(fs, new RectF(mx + ch.X, my + ch.Y, fs.W * scale, fs.H * scale), Rgba.White);
            canvas.PopTransform();
            if (phase == "coin") { var (sxp, syp) = Point("slot"); DrawCoin(canvas, bx + sxp, by + syp); }
        }

        private void DrawBulbs(Canvas canvas, int mx, int my)
        {
            var bulbs = Json.Arr(meta.Get("bulbs")); if (bulbs == null) return;
            var speed = 4.0; var color = new Rgba(255, 214, 120);
            if (phase == "crank") { speed = 16.0; if (TierIndex >= 3 && t > CrankT * 0.45f) color = TierColor(Tier); }
            else if ((phase == "present" || phase == "open" || phase == "reveal") && result != null) { color = TierColor(Tier); speed = 9.0; }
            if (phase == "crank") speed = 10.0 + 30.0 * CrankU;
            var lit = (int)(time * speed);
            for (var i = 0; i < bulbs.Count; i++)
            {
                var b = Json.Ints(bulbs[i]);
                var on = (i + lit) % 3 == 0 || phase == "open" || bulbFlash > 0.35f;
                int x = mx + b[0] * scale, y = my + b[1] * scale;
                if (on)
                {
                    canvas.Fill(new RectF(x - 3, y - 3, 6, 6), color); canvas.Fill(new RectF(x - 3, y - 3, 2, 2), Glows.Scaled(color, 1.3));
                    Glows.Draw(canvas, Art, x, y, 14, Glows.Scaled(color, 0.6));
                }
                else { canvas.Fill(new RectF(x - 3, y - 3, 6, 6), new Rgba(64, 30, 26)); canvas.Fill(new RectF(x - 3, y - 3, 2, 2), new Rgba(110, 70, 50)); }
            }
        }

        private void DrawCoin(Canvas canvas, float sx, float sy)
        {
            var u = Clamp01(t / (CoinT * 0.78f)); if (u >= 1) return;
            var x = PurseX + (sx - PurseX) * u; var y = PurseY + (sy - PurseY) * u - 160 * (float)Math.Sin(Math.PI * u);
            var frames = Json.Int(meta.Get("coin_frames"), 6);
            var img = Coin((int)(t * 30) % frames);
            Glows.Draw(canvas, Art, (int)x, (int)y, 30, new Rgba(120, 90, 20));
            canvas.BlitRect(img, RectF.CenteredInt((int)x, (int)y, img.W * scale * 2, img.H * scale * 2), Rgba.White);
            for (var i = 0; i < 2; i++) { var vx = rng.Uniform(-30, 30); var vy = rng.Uniform(-30, 30); var size = rng.Uniform(1.5, 3); fx.Emit(x, y, (float)vx, (float)vy, 0.4f, Gold, (float)size); }
        }

        private void RelicIcon(Canvas canvas, int size, float cx, float cy, Rgba tint)
        {
            var s = Art.Relic(result?.Relic?.Definition.Name);
            if (s.Valid) canvas.BlitRect(s, RectF.CenteredInt((int)cx, (int)cy, size, size), tint);
        }

        private void DrawPrize(Canvas canvas, int ox, int oy)
        {
            if (result == null || phase == "idle" || phase == "coin" || phase == "crank") return;
            var tier = TierIndex; var rarity = Tier;
            if (phase == "drop")
            {
                var (dx, dy, grow) = DropPos(t);
                var spin = t < FallT ? 0 : (int)(-360 * EaseOutCubic((t - FallT) / HopT));
                if (grow < 0.5f) Blit2(canvas, DropSprite(tier), (int)(dx + ox), (int)(dy + oy), 1f, spin);
                else Blit2(canvas, Big(tier, 0), (int)(dx + ox), (int)(dy + oy), 0.55f + 0.45f * (grow - 0.5f) / 0.5f, spin);
                return;
            }
            float x = StageX + ox, y = StageY + oy;
            if (phase == "present")
            {
                var floatY = (float)Math.Sin(t * 2.6) * 6; var pl = 0.5 + 0.5 * Math.Sin(t * 5);
                Glows.Draw(canvas, Art, (int)x, (int)y, 90 + 12 * tier, Glows.Scaled(TierColor(rarity), 0.35 + 0.25 * pl + 0.08 * tier));
                var angle = 0f;
                for (var k = 0; k < Shakes; k++)
                {
                    var at = 0.35f + k * (ShakeT + 0.12f);
                    if (at <= t && t < at + ShakeT) { var uu = (t - at) / ShakeT; angle = (float)(Math.Sin(uu * MathX.Tau * 2) * (10 + 4 * k) * (1 - uu)); }
                }
                Blit2(canvas, Big(tier, 0), (int)x, (int)(y + floatY), 1f, angle);
                if (t > 0.6f)
                {
                    canvas.PushAlpha(Math.Min(1f, (t - 0.6f) / 0.3f) * (float)(0.78 + 0.22 * Math.Sin(time * 4)));
                    var prompt = Text(canvas, "Haz clic para abrir", 16, Anchor.MidLeft, x - (TextWidth("Haz clic para abrir", 16) + 8 + PixelUi.KeycapWidth(Art, "Espacio")) / 2f, y + 94, Cream, true);
                    PixelUi.Keycap(canvas, Art, prompt.Right + 8 + PixelUi.KeycapWidth(Art, "Espacio") / 2f - 3, y + 94, "Espacio");
                    canvas.PopAlpha();
                }
                return;
            }
            var tOpen = phase == "open" ? t : OpenT + 1;
            if ((phase == "open" || phase == "reveal") && tier >= 2)
            {
                var kk = Math.Min(1.0, tOpen / 0.25); var level = Math.Min(3, (int)(kk * (2 + tier * 0.5)));
                canvas.BlitRotated(Rays(rarity), (int)x, (int)y, (float)MathX.PyMod(time * 20.0, 360), 1f, Glows.Scaled(Glows.Scaled(TierColor(rarity), 0.85), (level + 1) * 0.25), Blend.Add);
            }
            if (phase == "open")
            {
                var u = Clamp01(tOpen / 0.6f); var alpha = (int)(255 * (1 - u));
                canvas.PushAlpha(alpha / 255f);
                Blit2(canvas, Big(tier, 1), (int)(x - 60 * u), (int)(y - 20 - 140 * u + 120 * u * u), 1f, -40 * u);
                Blit2(canvas, Big(tier, 2), (int)(x + 50 * u), (int)(y + 20 + 60 * u + 200 * u * u), 1f, 30 * u);
                canvas.PopAlpha();
            }
            var pop = EaseOutBack((tOpen - 0.1f) / 0.5f);
            if (phase == "discard")
            {
                var u = Clamp01(t / DiscardT); pop = 1f - Smoothstep(u); y += 40 * u * u;
                if (pop > 0.02f) RelicIcon(canvas, Math.Max(4, (int)(96 * pop)), x, y, new Rgba(90, 90, 90, (int)(255 * (1 - u))));
                return;
            }
            if (phase == "collect")
            {
                var u = EaseOutCubic(t / CollectT);
                x = x + (RelicCounterX - x) * u; y = y + (RelicCounterY - y) * u - 80 * (float)Math.Sin(Math.PI * u);
                pop = 1f - 0.75f * u;
            }
            if (pop <= 0.02f) return;
            Glows.Draw(canvas, Art, (int)x, (int)y, 70 + 10 * tier, Glows.Scaled(TierColor(rarity), 0.55));
            var bobY = phase == "reveal" ? (float)Math.Sin(time * 2.2) * 4 : 0f;
            var size = (int)(96 * pop);
            if (size < 4) return;
            if (!Art.Relic(result.Relic?.Definition.Name).Valid)
            {
                canvas.Circle((int)x, (int)(y + bobY), size / 3, TierColor(rarity));
                canvas.Circle((int)(x - size / 9), (int)(y + bobY - size / 9), Math.Max(1, size / 12), White);
                return;
            }
            RelicIcon(canvas, size, x, y + bobY, Rgba.White);
        }

        // ------------------------------------------------------------------ interface pieces
        // Every label is outlined in ink (one font pixel thick) so it reads over the rays, the glass and the room;
        // panels and buttons are the HUD kit (iron panel, bronze and gold buttons); boxes, bars and gems are drawn
        // on the 2 px grain of the art.

        private static readonly (int dx, int dy)[] Ring = { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) };
        private static readonly Rgba Ink = Colors.Ink, Cream = Colors.Cream, Dim = new Rgba(186, 176, 160), Gilt = new Rgba(255, 214, 120),
            Lucky = new Rgba(140, 240, 120), Short = new Rgba(255, 140, 118), Pink = new Rgba(255, 196, 228);

        /// <summary>Outlined text placed like pygame get_rect(anchor=...); <paramref name="shadow"/> adds a drop shadow.</summary>
        private RectF Text(Canvas canvas, string text, int size, Anchor anchor, float x, float y, Rgba color, bool shadow = false, bool bold = false)
        {
            if (string.IsNullOrEmpty(text)) return new RectF(x, y, 0, 0);
            var font = bold ? Fonts.Bold(size) : Fonts.Get(size); var k = font.Scale;
            var r = Txt.Place(font.Width(text) + 2 * k, font.Height + 2 * k, anchor, x, y);
            float ox = (float)Math.Floor(r.X) + k, oy = (float)Math.Floor(r.Y) + k;
            if (shadow) foreach (var dx in new[] { -1, 0, 1 }) font.Draw(canvas, text, ox + dx * k, oy + 2 * k, Ink);
            foreach (var (dx, dy) in Ring) font.Draw(canvas, text, ox + dx * k, oy + dy * k, Ink);
            font.Draw(canvas, text, ox, oy, color);
            return r;
        }

        private int TextWidth(string text, int size) { var f = Fonts.Get(size); return f.Width(text) + 2 * f.Scale; }

        /// <summary>A box with notched 2 px corners, a 2 px edge and an optional light top row.</summary>
        private static void PixelBox(Canvas canvas, RectF r, Rgba fill, Rgba edge, Rgba? light = null)
        {
            r = new RectF((float)Math.Floor(r.X), (float)Math.Floor(r.Y), (float)Math.Floor(r.W), (float)Math.Floor(r.H));
            canvas.Fill(new RectF(r.X + 2, r.Y, r.W - 4, 2), edge); canvas.Fill(new RectF(r.X + 2, r.Bottom - 2, r.W - 4, 2), edge);
            canvas.Fill(new RectF(r.X, r.Y + 2, 2, r.H - 4), edge); canvas.Fill(new RectF(r.Right - 2, r.Y + 2, 2, r.H - 4), edge);
            canvas.Fill(new RectF(r.X + 2, r.Y + 2, r.W - 4, r.H - 4), fill);
            if (light.HasValue) canvas.Fill(new RectF(r.X + 2, r.Y + 2, r.W - 4, 2), light.Value);
        }

        /// <summary>A 2 px gold frame just outside <paramref name="r"/> (hovered cards).</summary>
        private static void Halo2(Canvas canvas, RectF r, Rgba color)
        {
            r = r.Inflate(8, 8);
            canvas.Fill(new RectF(r.X + 4, r.Y, r.W - 8, 2), color); canvas.Fill(new RectF(r.X + 4, r.Bottom - 2, r.W - 8, 2), color);
            canvas.Fill(new RectF(r.X, r.Y + 4, 2, r.H - 8), color); canvas.Fill(new RectF(r.Right - 2, r.Y + 4, 2, r.H - 8), color);
            canvas.Fill(new RectF(r.X + 2, r.Y + 2, 2, 2), color); canvas.Fill(new RectF(r.Right - 4, r.Y + 2, 2, 2), color);
            canvas.Fill(new RectF(r.X + 2, r.Bottom - 4, 2, 2), color); canvas.Fill(new RectF(r.Right - 4, r.Bottom - 4, 2, 2), color);
        }

        // 9×9 capsule (native px): tier-coloured top, white bottom, ink outline.
        private static readonly string[] CapsuleMap = { "..kkkkk..", ".kLLTTTk.", "kLTTTTTDk", "kTTTTTTDk", "kSSSSSSSk", "kWWWWWWGk", "kWWWWWGGk", ".kWWWGGk.", "..kkkkk.." };

        private (Rgba main, Rgba light)[] capsuleColors;
        /// <summary>The colours of the machine's capsules (gacha.json tier_colors / tier_light), read once.</summary>
        private (Rgba main, Rgba light) CapsuleColors(int rarity)
        {
            if (capsuleColors == null)
            {
                capsuleColors = new (Rgba, Rgba)[5];
                var tc = Json.Arr(meta.Get("tier_colors")); var tl = Json.Arr(meta.Get("tier_light"));
                for (var i = 0; i < 5; i++)
                {
                    var c = tc != null && i < tc.Count ? Json.Ints(tc[i]) : null; var l = tl != null && i < tl.Count ? Json.Ints(tl[i]) : null;
                    var main = c != null ? new Rgba(c[0], c[1], c[2]) : TierColor(i + 1);
                    capsuleColors[i] = (main, l != null ? new Rgba(l[0], l[1], l[2]) : Glows.Scaled(main, 1.3));
                }
            }
            return capsuleColors[Math.Max(1, Math.Min(5, rarity)) - 1];
        }

        /// <summary>The small tier capsule that marks a rarity (18×18 px, the colours of the machine's capsules).</summary>
        private void MiniCapsule(Canvas canvas, float cx, float cy, int rarity, float alpha = 1f)
        {
            var (main, light) = CapsuleColors(rarity);
            Rgba Of(char ch)
            {
                switch (ch)
                {
                    case 'k': return Ink;
                    case 'L': return light;
                    case 'T': return main;
                    case 'D': return Glows.Scaled(main, 0.72);
                    case 'S': return Glows.Scaled(main, 0.45);
                    case 'W': return new Rgba(232, 230, 238);
                    default: return new Rgba(170, 166, 180);
                }
            }
            var a = (int)(255 * alpha);
            float x0 = (float)Math.Floor(cx) - 9, y0 = (float)Math.Floor(cy) - 9;
            for (var y = 0; y < CapsuleMap.Length; y++)
                for (var x = 0; x < CapsuleMap[y].Length; x++)
                    if (CapsuleMap[y][x] != '.') canvas.Fill(new RectF(x0 + x * 2, y0 + y * 2, 2, 2), Of(CapsuleMap[y][x]).WithAlpha(a));
        }

        /// <summary>A 60×8 odds bar: ink frame, dark track in the tier colour, fill with a light top row.</summary>
        private static void OddsBar(Canvas canvas, float x, float y, double p, Rgba col)
        {
            canvas.Fill(new RectF(x, y, 60, 8), Ink);
            canvas.Fill(new RectF(x + 2, y + 2, 56, 4), Glows.Scaled(col, 0.28));
            if (p <= 0) return;
            var w = Math.Max(1, (int)Math.Round(28 * Math.Min(1.0, p))) * 2;
            canvas.Fill(new RectF(x + 2, y + 2, w, 4), col);
            canvas.Fill(new RectF(x + 2, y + 2, w, 2), Glows.Scaled(col, 1.3));
        }

        /// <summary>A rarity / chroma tag: pixel box in the colour, text in a light version of it.</summary>
        private float Chip(Canvas canvas, string text, float x, float cy, Rgba color, int capsule = 0)
        {
            var w = TextWidth(text, 13) + 16 + (capsule > 0 ? 20 : 0);
            var r = new RectF(x, (float)Math.Floor(cy) - 12, w, 24);
            PixelBox(canvas, r, Glows.Scaled(color, 0.22).WithAlpha(235), Glows.Scaled(color, 0.85));
            var tx = x + 8;
            if (capsule > 0) { MiniCapsule(canvas, tx + 8, cy, capsule); tx += 20; }
            Text(canvas, text, 13, Anchor.MidLeft, tx, cy - 1, Glows.Scaled(color, 1.35));
            return w;
        }

        private float ChipWidth(string text, bool capsule) => TextWidth(text, 13) + 16 + (capsule ? 20 : 0);

        // ------------------------------------------------------------------ reveal panel
        private const int RevealMaxW = 412, RevealMinW = 348;

        private static readonly (float u, float v, float ph)[] FrameStars = { (0f, 0f, 0f), (1f, 0.3f, 1.7f), (0.32f, 1f, 3.1f), (0f, 0.72f, 4.4f), (0.74f, 0f, 2.3f), (1f, 1f, 5.2f) };

        /// <summary>Four-point stars that come and go on the frame (never over the text).</summary>
        private static void FrameTwinkles(Canvas canvas, RectF r, Rgba color, float t)
        {
            foreach (var (u, v, ph) in FrameStars)
            {
                var k = Math.Sin(t * 2.4 + ph); if (k < 0.2) continue;
                var arm = 2 * (1 + (int)(k * 2.5));
                var cx = (float)Math.Floor((r.X + 1 + (r.W - 4) * u) / 2) * 2; var cy = (float)Math.Floor((r.Y + 1 + (r.H - 4) * v) / 2) * 2;
                canvas.Fill(new RectF(cx - arm, cy, 2 * arm + 2, 2), color); canvas.Fill(new RectF(cx, cy - arm, 2, 2 * arm + 2), color);
                canvas.Fill(new RectF(cx, cy, 2, 2), Rgba.White);
            }
        }

        private static string ChromaChip(ChromaKind chroma) => ChromaFx.Style(chroma).ShortNote.Replace(": ", " · ").Replace(" x", " ×");

        /// <summary>The reveal panel and its two buttons; also places <c>btnKeep</c> / <c>btnDecline</c>.</summary>
        private void DrawRevealText(Canvas canvas)
        {
            if (phase != "open" && phase != "reveal" || result?.Relic == null) return;
            var relic = result.Relic; var def = relic.Definition; var rarity = Math.Max(1, Math.Min(5, def.Rarity)); var col = TierColor(rarity);
            var k = phase == "reveal" ? 1f : Clamp01((t - 0.45f) / 0.4f);
            if (k <= 0) return;
            var layout = RevealLayout(relic, result.Duplicate);
            var (panel, title, titleSize, chips, rows, lines) = (layout.Panel, layout.Title, layout.TitleSize, layout.Chips, layout.Chips.Count, layout.Lines);
            canvas.PushAlpha(k);
            var rise = (int)Math.Round((1 - EaseOutCubic(k)) * 12) * 2;
            panel = panel.Move(0, rise);
            if (relic.Chroma != ChromaKind.None) ChromaFx.DrawHalo(canvas, Art, panel.CenterX, panel.CenterY, panel.W, panel.H, relic.Chroma, Game.Time, false, 8);
            PixelUi.Panel(canvas, Art, panel);
            // header band in the tier colour (dithered into the iron), then a thin rule
            var inner = panel.Inflate(-16, -16);
            var headerH = 14 + Fonts.Get(titleSize).Height + 8 + rows * 30;
            canvas.Fill(new RectF(inner.X, inner.Y, inner.W, headerH), Glows.Scaled(col, 0.3).WithAlpha(150));
            for (var x = (int)inner.X; x < inner.Right; x += 4) canvas.Fill(new RectF(x + ((x / 4) % 2) * 2, inner.Y + headerH, 2, 2), Glows.Scaled(col, 0.3).WithAlpha(150));
            canvas.Fill(new RectF(inner.X + 4, inner.Y + headerH + 4, inner.W - 8, 2), Glows.Scaled(col, 0.75));
            if (relic.Chroma != ChromaKind.None)
            {
                var st = ChromaFx.Style(relic.Chroma); var pulse = 0.5 + 0.5 * Math.Sin(Game.Time * 2.2);
                Halo2(canvas, panel.Inflate(-8, -8), Glows.Scaled(st.Main, 0.8 + 0.3 * pulse));
                FrameTwinkles(canvas, panel, st.Bright, Game.Time);
            }
            var y = inner.Y + 8;
            Text(canvas, title, titleSize, Anchor.MidTop, panel.CenterX, y, relic.Chroma != ChromaKind.None ? new Rgba(255, 226, 140) : Cream, true);
            y += Fonts.Get(titleSize).Height + 2 * Fonts.Get(titleSize).Scale + 6;
            foreach (var row in chips)
            {
                var total = row.Sum(c => ChipWidth(c.text, c.capsule > 0)) + 8 * (row.Count - 1);
                var x = (float)Math.Floor(panel.CenterX - total / 2);
                foreach (var c in row) x += Chip(canvas, c.text, x, y + 12, c.color, c.capsule) + 8;
                y += 30;
            }
            y = inner.Y + headerH + 14;
            foreach (var line in lines) { Text(canvas, line, 13, Anchor.MidTop, panel.CenterX, y, Colors.TextPrimary); y += 20; }
            canvas.PopAlpha();
            // buttons: keep (gold, Enter) and decline (bronze, R), with what declining costs under it
            var bottom = panel.Bottom - rise + 14;
            btnKeep = new IRect((int)panel.CenterX - 8 - 196, (int)bottom, 196, 48);
            btnDecline = new IRect((int)panel.CenterX + 8, (int)bottom + 6, 172, 36);
            if (phase != "reveal") return;
            if (hoveredKey == "keep") Halo2(canvas, ((RectF)btnKeep).Move(0, -1), new Rgba(255, 226, 140));
            if (hoveredKey == "decline") Halo2(canvas, ((RectF)btnDecline).Move(0, -1), new Rgba(220, 150, 140));
            PixelUi.Button(canvas, Art, btnKeep, "Quedármela", "gold", hoveredKey == "keep" ? ButtonState.Hover : ButtonState.Idle, null, "Enter", time, 0f, 16);
            PixelUi.Button(canvas, Art, btnDecline, "Rechazar", "bronze", hoveredKey == "decline" ? ButtonState.Hover : ButtonState.Idle, null, "R", time, 0f, 16);
            Text(canvas, "No se devuelve el oro", 13, Anchor.MidTop, btnDecline.CenterX, btnDecline.Bottom + 4, hoveredKey == "decline" ? Short : Dim);
        }

        /// <summary>Where the reveal panel goes and what it says (title, tags in one or two rows, description lines).</summary>
        public sealed class RevealCard
        {
            public RectF Panel; public string Title; public int TitleSize;
            public List<List<(string text, Rgba color, int capsule)>> Chips; public List<string> Lines;
        }

        /// <summary>Reveal layout of <paramref name="relic"/>: the chunky title when it fits (else the small face, else cut with "…"),
        /// tags for rarity, chroma and repeat, and the description wrapped to at most four lines.</summary>
        public RevealCard RevealLayout(RelicOffer relic, bool duplicate)
        {
            var def = relic.Definition; var rarity = Math.Max(1, Math.Min(5, def.Rarity));
            // title: the chunky face when it fits, else the small one, else cut with "…"
            var titleSize = 20; var name = def.Name;
            if (TextWidth(name, titleSize) > RevealMaxW - 40) titleSize = 16;
            name = TextFit.Fit(Fonts.Get(titleSize), name, RevealMaxW - 40 - 4);
            var tags = new List<(string text, Rgba color, int capsule)> { (RunContent.RarityLabel(rarity), TierColor(rarity), rarity) };
            if (relic.Chroma != ChromaKind.None) tags.Add((ChromaChip(relic.Chroma), ChromaFx.Style(relic.Chroma).Main, 0));
            if (duplicate) tags.Add(("¡Repetida!", new Rgba(240, 150, 70), 0));
            var tagsW = tags.Sum(c => ChipWidth(c.text, c.capsule > 0)) + 8 * (tags.Count - 1);
            var w = (int)Math.Max(RevealMinW, Math.Max(TextWidth(name, titleSize) + 48, tagsW + 40));
            w = Math.Min(RevealMaxW, (w + 1) / 2 * 2);
            var chips = new List<List<(string, Rgba, int)>>();
            if (tagsW <= w - 40) chips.Add(tags);
            else { chips.Add(tags.GetRange(0, 1)); chips.Add(tags.GetRange(1, tags.Count - 1)); }
            var lines = TextFit.WrapLimited(Fonts.Get(13), def.Description, w - 44, 4);
            var titleFont = Fonts.Get(titleSize);
            var headerH = 14 + titleFont.Height + 8 + chips.Count * 30;
            var h = 16 + headerH + 14 + lines.Count * 20 + 22;
            var panel = new RectF((float)Math.Floor(StageX - w / 2f), StageY + 84, w, h);
            return new RevealCard { Panel = panel, Title = name, TitleSize = titleSize, Chips = chips, Lines = lines };
        }

        // ------------------------------------------------------------------ room interface
        private void DrawPanel(Canvas canvas)
        {
            canvas.BlitRect(Band(), new RectF(0, 0, Canvas.Width, 92), Rgba.White);
            // title: glowing pink outlined letters over a crimson ribbon with the rule of the room
            Glows.Draw(canvas, Art, 640, 40, 120, new Rgba(70, 20, 60));
            Text(canvas, "Gachapón", 30, Anchor.Center, 640, 38, Pink, true, true);
            PixelUi.Ribbon(canvas, Art, 640, 78, "Cada tirada sube el precio de las siguientes", 13);
            DrawRelicCounter(canvas);
            var busy = phase != "idle";
            DrawPullCard(canvas, PullKind.Normal, BtnNormal, hoveredKey == "normal" && !busy, busy);
            DrawPullCard(canvas, PullKind.Stellar, BtnStellar, hoveredKey == "stellar" && !busy, busy);
            DrawOdds(canvas);
            var hov = hoveredKey == "exit" && !busy;
            if (hov) Halo2(canvas, ((RectF)Exit).Move(0, -1), new Rgba(220, 200, 160));
            PixelUi.Button(canvas, Art, Exit, "Salir", "bronze", busy ? ButtonState.Off : hov ? ButtonState.Hover : ButtonState.Idle, null, null, time, 0f, 16);
            if (feedbackT > 0)
            {
                var a = Math.Min(1f, feedbackT / 0.3f);
                canvas.PushAlpha(a);
                var w = TextWidth(feedback, 16) + 40;
                PixelUi.Panel(canvas, Art, RectF.CenteredInt(640, 684, w, 40));
                Text(canvas, feedback, 16, Anchor.Center, 640, 683, Short);
                canvas.PopAlpha();
            }
        }

        /// <summary>Where kept relics land: an iron plate with the bag and the number of relics.</summary>
        private void DrawRelicCounter(Canvas canvas)
        {
            var pulseK = Math.Max(0f, counterPulse);
            var plate = RelicPlate.Move(0, pulseK > 0.5f ? -2 : 0);
            if (pulseK > 0) Halo2(canvas, plate, Glows.Scaled(TierColor(Tier), 0.6 + 0.6 * pulseK));
            PixelUi.Panel(canvas, Art, plate);
            var bag = Art.Icon("bag");
            if (bag.Valid) canvas.BlitRect(bag, new RectF(plate.X + 10, plate.CenterY - bag.H, bag.W * 2, bag.H * 2), Rgba.White);
            Text(canvas, "Reliquias", 13, Anchor.MidLeft, plate.X + 44, plate.CenterY - 1, Dim);
            // the kept relic counts once it lands
            var count = run.OwnedRelics.Count - (phase == "collect" && result != null && result.Accepted ? 1 : 0);
            Text(canvas, count.ToString(), 20, Anchor.MidRight, plate.Right - 12, plate.CenterY - 1, Gilt);
        }

        private void DrawPullCard(Canvas canvas, PullKind kind, IRect rect, bool hovered, bool busy)
        {
            var d = RunState.PullDefinitions[(int)kind]; var price = run.GachaPrice(kind);
            var stellar = kind == PullKind.Stellar; var afford = run.CanPull(kind); var ok = afford && !busy;
            var card = new RectF(rect.X, rect.Y - (hovered && ok ? 2 : 0), rect.W, rect.H);
            if (hovered && ok) Halo2(canvas, card, stellar ? new Rgba(255, 214, 110) : new Rgba(220, 200, 160));
            PixelUi.Panel(canvas, Art, card);
            var inner = card.Inflate(-16, -16);
            canvas.Fill(inner, stellar ? new Rgba(120, 54, 176, hovered && ok ? 96 : 70) : new Rgba(70, 96, 150, hovered && ok ? 52 : 34));
            if (stellar)
                foreach (var (fx, fy, ph) in new[] { (0.30f, 0.20f, 0f), (0.86f, 0.62f, 2.2f), (0.55f, 0.40f, 4.1f), (0.94f, 0.18f, 1.2f), (0.18f, 0.70f, 3.3f) })
                {
                    var tw = 0.5 + 0.5 * Math.Sin(time * 2.6 + ph); if (tw < 0.35) continue;
                    float sx = (float)Math.Floor(inner.X + inner.W * fx / 2) * 2, sy = (float)Math.Floor(inner.Y + inner.H * fy * 0.5f / 2) * 2;
                    var sc = new Rgba(255, 230, 170, (int)(220 * tw));
                    canvas.Fill(new RectF(sx, sy - 2, 2, 6), sc); canvas.Fill(new RectF(sx - 2, sy, 6, 2), sc);
                }
            // capsule, name, key, note
            var bob = ok ? (int)Math.Round(Math.Sin(time * 2.4 + (stellar ? 1.7 : 0)) * 1.2) * 2 : 0;
            Blit2(canvas, DropSprite(stellar ? 4 : 0), card.X + 38, card.Y + 34 + bob, 1f, 0, ok ? (Rgba?)null : new Rgba(170, 170, 170));
            Text(canvas, d.Name, 16, Anchor.MidLeft, card.X + 68, card.Y + 23, !afford && !busy ? Dim : stellar ? new Rgba(255, 226, 150) : Cream, true);
            PixelUi.Keycap(canvas, Art, card.Right - 26, card.Y + 23, stellar ? "2" : "1");
            Text(canvas, d.Note, 13, Anchor.MidLeft, card.X + 68, card.Y + 45, Dim);
            // the price is the button
            var style = stellar ? "gold" : "bronze"; var bh = stellar ? 48 : 36;
            var btn = new RectF(card.X + 12, card.Bottom - 12 - 48 + (48 - bh) / 2, card.W - 24, bh);
            var state = !ok ? ButtonState.Off : hovered ? ButtonState.Hover : ButtonState.Idle;
            PixelUi.Button(canvas, Art, btn, "", style, state, null, null, time + (stellar ? 1.3f : 0f));
            var shown = btn.Move(0, state == ButtonState.Hover ? -1 : 0);
            var (label, font, shortBy, textW) = PriceLine(price, run.Gold, PriceRoom);
            var coin = Art.Icon("coin");
            var x = (float)Math.Floor(shown.CenterX - textW / 2f); var cy = shown.CenterY - 1;
            if (coin.Valid) { canvas.BlitRect(coin, new RectF(x, (float)Math.Floor(cy) - coin.H, coin.W * 2, coin.H * 2), state == ButtonState.Off ? new Rgba(150, 150, 150) : Rgba.White); x += 32; }
            var r = Text(canvas, label, font, Anchor.MidLeft, x, cy, state == ButtonState.Off ? new Rgba(196, 190, 184) : Cream);
            if (shortBy != null) Text(canvas, shortBy, 13, Anchor.MidLeft, r.Right + 10, cy, Short);
            if (stellar && ok)
            {
                var tt = MathX.PyMod(time * 0.6, 1.0); var pos = tt * 2 * (card.W + card.H); double px, py;
                if (pos < card.W) { px = card.X + pos; py = card.Y; }
                else if (pos < card.W + card.H) { px = card.Right; py = card.Y + pos - card.W; }
                else if (pos < 2 * card.W + card.H) { px = card.Right - (pos - card.W - card.H); py = card.Bottom; }
                else { px = card.X; py = card.Bottom - (pos - 2 * card.W - card.H); }
                Glows.Draw(canvas, Art, (int)px, (int)py, 16, new Rgba(255, 200, 90));
            }
            // while the machine runs the cards stay solid but sink into the dark
            if (busy) canvas.Fill(card.Inflate(-4, -4), new Rgba(8, 6, 14, 110));
        }

        /// <summary>What the price button says: "405 oro" in the chunky face (plus "faltan N" when short), shrinking
        /// to "405" and then to the small face so it never leaves a button <paramref name="maxW"/> wide.
        /// <c>width</c> includes the 32 px of the coin.</summary>
        public (string label, int size, string shortBy, int width) PriceLine(int price, int gold, int maxW)
        {
            var amount = price.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");
            var shortBy = gold >= price ? null : "faltan " + (price - gold).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");
            foreach (var (label, size, extra) in new[] { (amount + " oro", 20, shortBy), (amount, 20, shortBy), (amount, 16, shortBy), (amount, 16, null) })
            {
                var text = price == 0 ? "¡Gratis!" : label;
                var w = 32 + TextWidth(text, size) + (extra != null ? 10 + TextWidth(extra, 13) : 0);
                if (w <= maxW) return (text, size, extra, w);
            }
            var cut = TextFit.Fit(Fonts.Get(16), amount, maxW - 34);
            return (cut, 16, null, 32 + TextWidth(cut, 16));
        }

        private static string Pct(double p)
        {
            if (p <= 0) return "0%";
            return p < 0.1 ? Math.Round(p * 100, 1, MidpointRounding.ToEven).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%" : MathX.Round(p * 100) + "%";
        }

        private const int OddsTop = 382, OddsH = 264, ColNormal = 172, ColStellar = 242;

        private void DrawOdds(Canvas canvas)
        {
            var box = new RectF(PanelX, OddsTop, BtnW, OddsH);
            PixelUi.Panel(canvas, Art, box);
            int x0 = PanelX, top = OddsTop;
            Text(canvas, "Probabilidades", 16, Anchor.Center, box.CenterX, top + 21, Gilt, true);
            canvas.Fill(new RectF(x0 + 18, top + 36, BtnW - 36, 2), new Rgba(96, 80, 60));
            // luck (raises the high tiers) and the two columns
            var report = LuckReport.For(run); var lucky = report.Bonus > 0;
            var clover = Art.Icon("clover");
            if (clover.Valid) canvas.BlitRect(clover, new RectF(x0 + 14, top + 52 - clover.H, clover.W * 2, clover.H * 2), lucky ? Rgba.White : new Rgba(200, 200, 200));
            var luck = Text(canvas, "Suerte " + report.Luck, 13, Anchor.MidLeft, x0 + 44, top + 51, lucky ? Lucky : Dim);
            if (lucky) Text(canvas, "▲", 13, Anchor.MidLeft, luck.Right + 2, top + 51, Lucky);
            Text(canvas, "Normal", 13, Anchor.Center, x0 + ColNormal, top + 51, Cream);
            Text(canvas, "Estelar", 13, Anchor.Center, x0 + ColStellar, top + 51, new Rgba(255, 220, 140));
            var normal = run.GachaOdds(PullKind.Normal); var stellar = run.GachaOdds(PullKind.Stellar);
            var baseN = RunState.PullOdds(PullKind.Normal, report.Base); var baseS = RunState.PullOdds(PullKind.Stellar, report.Base);
            for (var r = 1; r <= 5; r++)
            {
                var y = top + 82 + (r - 1) * 32; var col = TierColor(r);
                if (r % 2 == 0) canvas.Fill(new RectF(x0 + 10, y - 15, BtnW - 20, 32), new Rgba(255, 255, 255, 10));
                MiniCapsule(canvas, x0 + 24, y + 1, r);
                Text(canvas, RunContent.RarityLabel(r), 13, Anchor.MidLeft, x0 + 38, y, Glows.Scaled(col, 1.2));
                foreach (var (cx, odds, b) in new[] { (x0 + ColNormal, normal, baseN), (x0 + ColStellar, stellar, baseS) })
                {
                    var p = odds.TryGetValue(r, out var v) ? v : 0; var bp = b.TryGetValue(r, out var bv) ? bv : 0;
                    var label = Text(canvas, Pct(p), 13, Anchor.Center, cx, y - 5, p > 0 ? Cream : new Rgba(120, 114, 108));
                    if (p > bp + 0.0005 && r >= 2) Text(canvas, "▲", 13, Anchor.MidLeft, label.Right, y - 5, Lucky);
                    OddsBar(canvas, cx - 30, y + 6, p, col);
                }
            }
            canvas.Fill(new RectF(x0 + 18, top + OddsH - 38, BtnW - 36, 2), new Rgba(70, 60, 52));
            Text(canvas, "Tiradas hechas: " + run.Pulls + "  ·  precio ×1.5", 13, Anchor.Center, box.CenterX, top + OddsH - 22, Dim);
        }
    }
}
