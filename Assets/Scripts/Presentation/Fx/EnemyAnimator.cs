using System;
using System.Collections.Generic;

namespace LastWish.Presentation
{
    /// <summary>fx/enemy_animator.EnemyFxStyle: particle colours and cue points of one animated enemy (screen px from the anchor).</summary>
    public sealed class EnemyFxStyle
    {
        public Rgba[] Wisp = EnemyAnimator.WispPal, Ecto = EnemyAnimator.EctoPal, Spark = EnemyAnimator.SparkPal, Shreds = EnemyAnimator.VioletPal;
        public Rgba Glow = new Rgba(40, 150, 140);
        public (float x, float y) Chest = (-4, -96), Eyes = (-6, -124), Body = (0, -84), Claw = (-118, -80);
        public int ShadowW = 150;
        public float AmbientRise = 1f;
        public bool AmbientHeavy, Blades;
    }

    /// <summary>fx/enemy_animator.EnemyAnimator: plays an enemy sheet by time, with its own pooled particles.</summary>
    public sealed class EnemyAnimator
    {
        private const float MaxDt = 0.1f;
        private const double Tau = Math.PI * 2;
        private static Rgba C(int r, int g, int b) => new Rgba(r, g, b);
        internal static readonly Rgba[] WispPal = { C(150, 255, 230), C(90, 210, 190), C(50, 140, 140), C(26, 70, 80) };
        internal static readonly Rgba[] EctoPal = { C(230, 255, 245), C(130, 235, 215), C(60, 170, 160), C(40, 31, 64) };
        internal static readonly Rgba[] SparkPal = { C(255, 255, 255), C(200, 255, 240), C(110, 220, 200) };
        internal static readonly Rgba[] VioletPal = { C(180, 160, 230), C(116, 98, 160), C(58, 45, 90) };
        private static readonly Rgba[] Tox = { C(236, 255, 160), C(170, 230, 70), C(110, 170, 50), C(60, 100, 30) };
        private static readonly Rgba[] Spore = { C(250, 255, 214), C(226, 236, 150), C(180, 196, 96), C(120, 130, 60) };
        private static readonly Rgba[] CapBits = { C(238, 148, 132), C(178, 52, 84), C(96, 22, 58) };
        private static readonly Rgba[] Ichor = { C(255, 214, 226), C(255, 110, 170), C(214, 54, 96), C(110, 20, 50) };
        private static readonly Rgba[] Chitin = { C(156, 140, 196), C(76, 60, 106), C(34, 26, 54) };
        private static readonly Rgba[] Silk = { C(250, 250, 255), C(220, 220, 236), C(170, 170, 192) };
        private static readonly Rgba[] EmberPal = { C(255, 250, 220), C(255, 214, 130), C(255, 132, 40), C(176, 58, 16) };
        private static readonly Rgba[] Steel = { C(235, 240, 250), C(196, 202, 214), C(134, 142, 160) };
        private static readonly Rgba[] Ash = { C(140, 130, 128), C(92, 86, 84), C(62, 56, 58) };
        private static readonly Rgba[] Acid = { C(236, 255, 170), C(170, 240, 80), C(96, 190, 50), C(40, 110, 30) };
        private static readonly Rgba[] Flesh = { C(250, 236, 214), C(214, 196, 170), C(150, 140, 128), C(86, 80, 90) };
        private static readonly Rgba[] Earth = { C(122, 96, 70), C(84, 62, 46), C(46, 34, 28) };
        private static readonly Rgba[] VioletGlow = { C(240, 220, 255), C(196, 150, 255), C(140, 86, 220), C(70, 40, 120) };
        private static readonly Rgba[] Blood = { C(255, 200, 200), C(230, 70, 80), C(160, 24, 44), C(80, 10, 26) };
        private static readonly Rgba[] Fur = { C(150, 112, 120), C(96, 66, 86), C(54, 36, 52) };
        private static readonly Rgba[] Bone = { C(246, 236, 214), C(210, 190, 160), C(140, 118, 100) };
        private static readonly Rgba[] TealCap = { C(150, 200, 196), C(70, 120, 124), C(34, 62, 70) };
        private static readonly Rgba[] Moss = { C(200, 250, 220), C(120, 220, 170), C(60, 160, 120), C(30, 90, 80) };
        private static readonly Rgba[] Stone = { C(220, 226, 232), C(150, 160, 172), C(96, 104, 120), C(56, 62, 76) };
        private static readonly Rgba[] MossBits = { C(150, 210, 90), C(90, 150, 60), C(50, 90, 40) };
        private static readonly Rgba[] Coins = { C(255, 250, 210), C(255, 214, 90), C(220, 150, 40), C(130, 80, 20) };
        private static readonly Rgba[] Wood = { C(176, 120, 74), C(118, 76, 46), C(66, 40, 26) };
        private static readonly Rgba[] AshFall = { C(255, 212, 118), C(250, 140, 40), C(200, 70, 18), C(78, 72, 72) };
        private static readonly Rgba[] RedBlood = { C(232, 70, 62), C(176, 26, 30), C(110, 12, 18), C(56, 6, 10) };
        private static readonly Rgba[] Hood = { C(138, 54, 50), C(106, 36, 38), C(78, 25, 29), C(34, 12, 16) };
        private static readonly Rgba[] SwampMist = { C(184, 214, 164), C(134, 174, 124), C(92, 130, 96), C(58, 86, 66) };
        private static readonly Rgba[] SwampGoo = { C(182, 246, 100), C(100, 194, 54), C(44, 122, 38), C(22, 64, 30) };
        private static readonly Rgba[] Hex = { C(236, 255, 196), C(182, 246, 100), C(210, 150, 255), C(150, 74, 204) };
        private static readonly Rgba[] Rags = { C(106, 124, 70), C(78, 98, 56), C(58, 76, 48), C(29, 41, 32) };
        private static readonly Rgba[] Grit = { C(176, 174, 169), C(128, 124, 124), C(92, 88, 92), C(60, 56, 62) };
        private static readonly Rgba[] Chips = { C(206, 204, 218), C(138, 135, 157), C(86, 83, 109), C(48, 45, 68) };
        private static readonly Rgba[] Amber = { C(255, 252, 228), C(255, 234, 164), C(255, 192, 74), C(226, 132, 28) };
        private static readonly Rgba[] Flakes = { C(222, 220, 214), C(176, 174, 169), C(124, 122, 119), C(98, 96, 94) };
        private static readonly Rgba[] SteamPal = { C(255, 255, 255), C(232, 242, 252), C(186, 204, 222), C(132, 150, 172) };
        private static readonly Rgba[] FurBlood = { C(200, 146, 100), C(166, 62, 52), C(94, 26, 30), C(36, 9, 13) };
        private static readonly Rgba[] Rage = { C(255, 255, 255), C(255, 170, 90), C(240, 80, 40), C(176, 30, 20) };
        private static readonly Rgba[] Dirt = { C(172, 156, 132), C(130, 112, 92), C(84, 53, 36), C(43, 25, 20) };
        private static readonly Rgba[] Venom = { C(214, 255, 160), C(122, 226, 72), C(44, 140, 52), C(18, 64, 34) };
        private static readonly Rgba[] IchorRed = { C(250, 222, 196), C(184, 92, 78), C(114, 38, 44), C(52, 15, 27) };
        private static readonly Rgba[] GoldPal = { C(255, 244, 198), C(255, 208, 104), C(212, 146, 42), C(146, 86, 22) };
        private static readonly Rgba[] Shell = { C(160, 70, 64), C(78, 23, 34), C(33, 11, 21), C(9, 5, 10) };
        private static Rgba[] Tail(Rgba[] p) { var r = new Rgba[p.Length - 1]; Array.Copy(p, 1, r, 0, r.Length); return r; }

        public static readonly Dictionary<string, EnemyFxStyle> Styles = new Dictionary<string, EnemyFxStyle>
        {
            ["wraith"] = new EnemyFxStyle(),
            ["mycelid"] = new EnemyFxStyle { Wisp = Spore, Ecto = Tox, Spark = Tox, Shreds = CapBits, Glow = C(60, 120, 30), Chest = (0, -126), Eyes = (0, -116), Body = (0, -96), Claw = (-292, -14), ShadowW = 210, AmbientRise = 0.55f },
            ["weaver"] = new EnemyFxStyle { Wisp = Silk, Ecto = Ichor, Spark = Ichor, Shreds = Chitin, Glow = C(120, 24, 70), Chest = (30, -96), Eyes = (-70, -92), Body = (10, -84), Claw = (-120, -64), ShadowW = 230, AmbientRise = 0.4f, AmbientHeavy = true },
            ["knight"] = new EnemyFxStyle { Wisp = EmberPal, Ecto = EmberPal, Spark = Steel, Shreds = Ash, Glow = C(150, 60, 16), Chest = (-6, -124), Eyes = (0, -178), Body = (0, -110), Claw = (-120, -100), ShadowW = 170, Blades = true },
            ["slime"] = new EnemyFxStyle { Wisp = Acid, Ecto = Acid, Spark = Acid, Shreds = Tail(Acid), Glow = C(50, 130, 30), Chest = (0, -60), Eyes = (-10, -72), Body = (0, -50), Claw = (-118, -40), ShadowW = 180, AmbientRise = 0.5f, AmbientHeavy = true },
            ["worm"] = new EnemyFxStyle { Wisp = Tox, Ecto = Flesh, Spark = Tox, Shreds = Earth, Glow = C(60, 100, 40), Chest = (-10, -110), Eyes = (-30, -128), Body = (0, -70), Claw = (-118, -96), ShadowW = 150, AmbientRise = 0.6f },
            ["eye"] = new EnemyFxStyle { Wisp = VioletGlow, Ecto = Ichor, Spark = VioletGlow, Shreds = Tail(Ichor), Glow = C(100, 40, 150), Chest = (0, -96), Eyes = (-12, -108), Body = (0, -92), Claw = (-170, -108), ShadowW = 90 },
            ["skull"] = new EnemyFxStyle { Wisp = EmberPal, Ecto = EmberPal, Spark = EmberPal, Shreds = Bone, Glow = C(170, 70, 20), Chest = (10, -92), Eyes = (0, -100), Body = (10, -92), Claw = (-150, -82), ShadowW = 110 },
            ["bat"] = new EnemyFxStyle { Wisp = Blood, Ecto = Blood, Spark = Blood, Shreds = Fur, Glow = C(110, 20, 40), Chest = (0, -94), Eyes = (-10, -108), Body = (0, -92), Claw = (-110, -90), ShadowW = 130, AmbientRise = 0.7f },
            ["bomb"] = new EnemyFxStyle { Wisp = EmberPal, Ecto = Flesh, Spark = EmberPal, Shreds = TealCap, Glow = C(160, 90, 20), Chest = (0, -84), Eyes = (0, -60), Body = (0, -70), Claw = (-100, -62), ShadowW = 100 },
            ["golem"] = new EnemyFxStyle { Wisp = Moss, Ecto = Stone, Spark = Stone, Shreds = MossBits, Glow = C(40, 120, 110), Chest = (-10, -110), Eyes = (-24, -140), Body = (-10, -90), Claw = (-150, -24), ShadowW = 190, AmbientRise = 0.5f, AmbientHeavy = true },
            ["acolyte"] = new EnemyFxStyle { Wisp = EmberPal, Ecto = EmberPal, Spark = EmberPal, Shreds = Ash, Glow = C(150, 70, 20), Chest = (-10, -110), Eyes = (-20, -134), Body = (-10, -90), Claw = (-104, -70), ShadowW = 130, AmbientHeavy = true },
            ["imp"] = new EnemyFxStyle { Wisp = EmberPal, Ecto = Blood, Spark = EmberPal, Shreds = Tail(Blood), Glow = C(160, 50, 20), Chest = (10, -84), Eyes = (0, -108), Body = (10, -80), Claw = (-112, -80), ShadowW = 100 },
            ["mimic"] = new EnemyFxStyle { Wisp = Coins, Ecto = Blood, Spark = Coins, Shreds = Wood, Glow = C(150, 110, 30), Chest = (0, -60), Eyes = (0, -72), Body = (0, -56), Claw = (-124, -40), ShadowW = 170, AmbientRise = 0.4f, AmbientHeavy = true },
            ["executioner"] = new EnemyFxStyle { Wisp = AshFall, Ecto = RedBlood, Spark = EmberPal, Shreds = Hood, Glow = C(150, 40, 20), Chest = (-10, -104), Eyes = (-21, -157), Body = (-14, -84), Claw = (-148, -36), ShadowW = 120, AmbientRise = 0.5f, AmbientHeavy = true },
            ["hag"] = new EnemyFxStyle { Wisp = SwampMist, Ecto = SwampGoo, Spark = Hex, Shreds = Rags, Glow = C(60, 130, 40), Chest = (-10, -88), Eyes = (-35, -119), Body = (-22, -68), Claw = (-268, -100), ShadowW = 150, AmbientRise = 0.5f },
            ["gargoyle"] = new EnemyFxStyle { Wisp = Grit, Ecto = Chips, Spark = Amber, Shreds = Flakes, Glow = C(150, 100, 30), Chest = (-12, -100), Eyes = (-54, -134), Body = (8, -96), Claw = (-138, -50), ShadowW = 150, AmbientRise = 0.5f, AmbientHeavy = true },
            ["minotaur"] = new EnemyFxStyle { Wisp = SteamPal, Ecto = FurBlood, Spark = Rage, Shreds = Dirt, Glow = C(150, 40, 20), Chest = (-24, -122), Eyes = (-57, -145), Body = (-10, -99), Claw = (-255, -111), ShadowW = 130, AmbientRise = 0.5f, AmbientHeavy = true },
            ["scorpion"] = new EnemyFxStyle { Wisp = Venom, Ecto = IchorRed, Spark = GoldPal, Shreds = Shell, Glow = C(50, 130, 40), Chest = (-64, -39), Eyes = (-66, -56), Body = (-4, -48), Claw = (-246, -37), ShadowW = 260, AmbientRise = 0.5f, AmbientHeavy = true },
        };
        public static EnemyFxStyle StyleFor(string sheetId) => sheetId != null && Styles.TryGetValue(sheetId, out var s) ? s : Styles["wraith"];

        public static readonly (float x, float y) BladeOrbit = (78f, 20f), BladeCenter = (0f, -118f), DefaultTarget = (-560f, -96f);
        public const float BladeFirst = 0.30f, BladeGap = 0.13f, BladeFlight = 0.22f, BladeReform = 0.45f;

        private sealed class BladeState { public string Mode; public float T, X0, Y0, Tx, Ty; }

        public readonly EnemySheet Sheet;
        public readonly EnemyFxStyle Style;
        private readonly Rng rng;
        private float idleTime, actionTime, wispClock, fxTime;
        private readonly List<(string name, float delay, int hits)> queue = new List<(string, float, int)>();
        private string final = "death";
        private (float x, float y)? anchor;
        private readonly HashSet<string> fired = new HashSet<string>();
        public readonly BurstParticles Particles;
        private int hits = 1;
        public int Blades;
        public (float x, float y)? Target;
        private float bladeAngle;
        private readonly List<BladeState> bladeState = new List<BladeState>();
        public string Action { get; private set; }
        public bool Dead { get; private set; }

        public EnemyAnimator(EnemySheet sheet, int seed = 0, float phase = 0f, int particles = 220)
        {
            Sheet = sheet; rng = new Rng(seed);
            idleTime = (float)MathX.PyMod(phase, Math.Max(1e-6, sheet.Seconds("idle")));
            wispClock = rng.UniformF(0f, 0.2f); fxTime = rng.UniformF(0f, 10f);
            Particles = new BurstParticles(particles, seed + 7);
            Style = StyleFor(sheet.Id);
            bladeAngle = rng.UniformF(0f, (float)Tau);
        }

        public bool DeathDone => Dead && actionTime >= Sheet.Seconds(final);
        public bool Busy => Dead ? !DeathDone : Action != null || queue.Count > 0;
        public float Seconds(string name) => Sheet.Seconds(name);
        public float StrikeTime() { var t = StrikeTimes("attack"); return t.Length > 0 ? t[0] : 0f; }

        public float[] StrikeTimes(string name, int hitCount = 1)
        {
            hitCount = Math.Max(1, hitCount);
            if (name == "command" && Style.Blades)
            {
                var r = new float[hitCount];
                for (var k = 0; k < hitCount; k++) r[k] = BladeFirst + k * BladeGap + BladeFlight;
                return r;
            }
            var b = new List<float>(Sheet.StrikeSeconds(name));
            if (b.Count == 0) return Array.Empty<float>();
            while (b.Count < hitCount) b.Add(b[b.Count - 1] + 0.12f);
            return b.GetRange(0, Math.Max(hitCount, 1)).ToArray();
        }

        public void Play(string name, float delay = 0f, int hitCount = 1)
        {
            if (Dead || !Sheet.Animations.ContainsKey(name) || name == "idle") return;
            if (delay > 0) { queue.Add((name, delay, hitCount)); return; }
            Start(name, hitCount);
        }

        private void Start(string name, int hitCount = 1)
        {
            if (Dead) return;
            if (name == "death" || Array.IndexOf(Sheet.Terminal, name) >= 0)
            {
                Dead = true; final = name; queue.Clear(); DropBlades();
            }
            Action = name; actionTime = 0f; fired.Clear(); hits = Math.Max(1, hitCount);
        }

        public void Update(float dt)
        {
            dt = Math.Min(MaxDt, Math.Max(0f, dt));
            fxTime += dt;
            for (var i = 0; i < queue.Count; i++)
            {
                var (n, d, h) = queue[i]; d -= dt;
                if (d <= 0) { queue.RemoveAt(i); i--; Start(n, h); if (Dead) break; }
                else queue[i] = (n, d, h);
            }
            if (Action != null)
            {
                actionTime += dt;
                Cues();
                if (!Dead && actionTime >= Sheet.Seconds(Action)) { Action = null; actionTime = 0f; idleTime = 0f; }
            }
            else idleTime = (float)MathX.PyMod(idleTime + dt, Math.Max(1e-6, Sheet.Seconds("idle")));
            Ambient(dt);
            if (Style.Blades) UpdateBlades(dt);
            Particles.Update(dt);
        }

        private bool Once(string key, float at)
        {
            if (fired.Contains(key) || actionTime < at) return false;
            fired.Add(key); return true;
        }

        private static readonly string[] StrikeKeys = { "strike0", "strike1", "strike2", "strike3", "strike4", "strike5", "strike6", "strike7" };

        private void Cues()
        {
            if (!anchor.HasValue) return;
            var (ax, ay) = anchor.Value;
            var p = Particles; var r = rng; var st = Style; var name = Action;
            var strikes = name != "hurt" && name != "death" && name != "cast" ? StrikeTimes(name, hits) : Array.Empty<float>();
            var terminal = Array.IndexOf(Sheet.Terminal, name) >= 0;
            if (!(name == "command" && st.Blades))
                for (var k = 0; k < strikes.Length; k++)
                {
                    if (!Once(k < StrikeKeys.Length ? StrikeKeys[k] : "strike" + k, strikes[k] - 0.05f)) continue;
                    float x = ax + st.Claw.x, y = ay + st.Claw.y;
                    p.Burst(x, y, 22, st.Spark, (160f, 420f), ((float)(Math.PI * 0.55), (float)(Math.PI * 1.25)), (0.18f, 0.4f), (1.5f, 2.5f), ParticleStyle.Spark, 4.0f);
                    p.Burst(x, y, 14, st.Wisp, (40f, 160f), null, (0.4f, 0.8f), (2f, 4f), ParticleStyle.Square, 3.0f, -60f, 18f);
                    p.Burst(x, y, 3, st.Wisp, (0f, 30f), null, (0.25f, 0.4f), (26f, 36f), ParticleStyle.Glow, 4.0f);
                }
            if (terminal && strikes.Length > 0 && Once("boom", strikes[0]))
            {
                float x = ax + st.Body.x, y = ay + st.Body.y;
                p.Burst(x, y, 60, st.Spark, (200f, 560f), null, (0.3f, 0.8f), (1.5f, 3f), ParticleStyle.Spark, 2.5f);
                p.Burst(x, y, 40, st.Wisp, (80f, 300f), null, (0.5f, 1.2f), (3f, 6f), ParticleStyle.Square, 2.0f, -40f, 20f);
                p.Burst(x, y, 24, Ash, (30f, 140f), null, (0.8f, 1.6f), (5f, 9f), ParticleStyle.Square, 1.5f, -70f, 24f);
                p.Burst(x, y, 4, st.Spark, (0f, 20f), null, (0.3f, 0.5f), (90f, 120f), ParticleStyle.Glow, 4.0f);
            }
            if (name == "hurt")
            {
                if (Once("splash", 0f))
                {
                    float x = ax + st.Body.x, y = ay + st.Body.y;
                    p.Burst(x, y, 26, st.Ecto, (90f, 280f), ((float)(-Math.PI * 0.45), (float)(Math.PI * 0.45)), (0.35f, 0.8f), (2f, 4f), ParticleStyle.Square, 2.2f, 380f, 10f);
                    p.Burst(x, y, 10, st.Shreds, (60f, 200f), null, (0.3f, 0.6f), (2f, 3f), ParticleStyle.Square, 2.5f, 200f, 14f);
                    p.Burst(x, y, 2, st.Ecto, (0f, 10f), null, (0.15f, 0.25f), (36f, 46f), ParticleStyle.Glow, 4.0f);
                }
            }
            else if (name == "cast" || name == "spores" || name == "web" || name == "command")
            {
                if (Once("charge", 0f)) p.Implode(ax + st.Chest.x, ay + st.Chest.y, 24, st.Wisp, (70f, 130f), (0.35f, 0.55f));
                if (name == "cast" && Once("rune", 0.3f))
                {
                    for (var i = 0; i < 16; i++)
                    {
                        var a = r.Uniform(0, Tau);
                        p.Emit(ax + (float)Math.Cos(a) * 52, ay - 2 + (float)Math.Sin(a) * 12, 0, r.UniformF(-90, -40), r.UniformF(0.6f, 1.1f), st.Wisp, r.UniformF(2, 3), ParticleStyle.Square, 1.0f);
                    }
                    p.Burst(ax + st.Chest.x, ay + st.Chest.y, 2, st.Wisp, (0f, 10f), null, (0.3f, 0.45f), (40f, 52f), ParticleStyle.Glow, 4.0f);
                }
            }
            else if (name == "death")
            {
                if (Once("pop", 0f))
                {
                    p.Burst(ax + st.Body.x, ay + st.Body.y, 30, st.Ecto, (120f, 320f), null, (0.4f, 0.9f), (2f, 4f), ParticleStyle.Square, 2.0f, 300f, 12f);
                    p.Burst(ax + st.Eyes.x, ay + st.Eyes.y, 3, st.Ecto, (0f, 10f), null, (0.3f, 0.5f), (40f, 56f), ParticleStyle.Glow, 4.0f);
                }
                var total = Sheet.Seconds("death");
                var u = actionTime / Math.Max(1e-6f, total);
                if (0.2f < u && u < 0.95f)
                {
                    var front = ay - 8 - (u - 0.2f) / 0.75f * 170;
                    for (var i = 0; i < 3; i++)
                        p.Emit(ax + r.UniformF(-44, 48), front + r.UniformF(-8, 8), r.UniformF(-25, 25), r.UniformF(-140, -60), r.UniformF(0.5f, 1.1f), st.Wisp, r.UniformF(2, 4), ParticleStyle.Square, 1.2f);
                }
                if (Once("soul", total * 0.8f))
                {
                    float x = ax + st.Eyes.x, y = ay + st.Eyes.y;
                    p.Burst(x, y, 24, st.Spark, (100f, 300f), null, (0.3f, 0.7f), (1.5f, 2.5f), ParticleStyle.Spark, 3.0f);
                    p.Burst(x, y, 6, st.Wisp, (10f, 60f), ((float)(-Math.PI * 0.7), (float)(-Math.PI * 0.3)), (0.8f, 1.4f), (14f, 24f), ParticleStyle.Glow, 0.5f, -90f);
                }
            }
        }

        // ------------------------------------------------------------------ floating swords
        private (float x, float y, float depth) BladeHome(int k, int n)
        {
            var (ax, ay) = anchor ?? (0f, 0f);
            var a = bladeAngle + k * Tau / Math.Max(1, n);
            var bob = 4 * Math.Sin(fxTime * 2.2 + k * 1.7);
            return ((float)(ax + BladeCenter.x + Math.Cos(a) * BladeOrbit.x), (float)(ay + BladeCenter.y + Math.Sin(a) * BladeOrbit.y + bob), (float)Math.Sin(a));
        }

        private void UpdateBlades(float dt)
        {
            var n = Dead ? 0 : Math.Max(0, Blades);
            bladeAngle = (float)MathX.PyMod(bladeAngle + dt * 0.9, Tau);
            while (bladeState.Count < n)
            {
                bladeState.Add(new BladeState { Mode = "form" });
                if (anchor.HasValue)
                {
                    var (x, y, _) = BladeHome(bladeState.Count - 1, n);
                    Particles.Implode(x, y, 16, EmberPal, (40f, 80f), (0.3f, 0.45f));
                }
            }
            if (!Dead && bladeState.Count > n) bladeState.RemoveRange(n, bladeState.Count - n);
            foreach (var b in bladeState)
            {
                b.T += dt;
                if (b.Mode == "form" && b.T > 0.35f) { b.Mode = "orbit"; b.T = 0f; }
                else if (b.Mode == "fly" && b.T >= BladeFlight)
                {
                    b.Mode = "gone"; b.T = 0f;
                    Particles.Burst(b.Tx, b.Ty, 18, Steel, (160f, 380f), ((float)(Math.PI * 0.6), (float)(Math.PI * 1.4)), (0.15f, 0.35f), (1.5f, 2.5f), ParticleStyle.Spark, 4.0f);
                    Particles.Burst(b.Tx, b.Ty, 2, EmberPal, (0f, 20f), null, (0.2f, 0.3f), (22f, 30f), ParticleStyle.Glow, 4.0f);
                }
                else if (b.Mode == "gone" && b.T >= BladeReform) { b.Mode = "form"; b.T = 0f; }
            }
            if (Action == "command" && n > 0 && anchor.HasValue)
            {
                var (ax, ay) = anchor.Value;
                var (tx, ty) = Target ?? (ax + DefaultTarget.x, ay + DefaultTarget.y);
                for (var h = 0; h < hits; h++)
                {
                    if (!Once("blade" + h, BladeFirst + h * BladeGap)) continue;
                    var k = h % n;
                    var (x, y, _) = BladeHome(k, n);
                    float jx = rng.UniformF(-14, 14), jy = rng.UniformF(-22, 22);
                    var s = bladeState[k]; s.Mode = "fly"; s.T = 0f; s.X0 = x; s.Y0 = y; s.Tx = tx + jx; s.Ty = ty + jy;
                }
            }
        }

        private void DropBlades()
        {
            if (!anchor.HasValue) { bladeState.Clear(); return; }
            var n = bladeState.Count;
            for (var k = 0; k < n; k++)
            {
                var (x, y, _) = BladeHome(k, n);
                Particles.Burst(x, y, 10, EmberPal, (40f, 160f), null, (0.3f, 0.7f), (2f, 3f), ParticleStyle.Square, 2.0f, 200f);
            }
            bladeState.Clear();
        }

        private Sprite BladeFrame(double angle)
        {
            var count = Math.Max(1, Sheet.BladeRotations);
            var k = ((MathX.Round(MathX.PyMod(angle, Tau) / Tau * count) % count) + count) % count;
            return Sheet.Blade(k);
        }

        private void DrawBlades(Canvas canvas, bool front)
        {
            if (Sheet.BladeTexture == null || bladeState.Count == 0) return;
            var n = bladeState.Count; var sc = Sheet.Scale; var size = Sheet.BladeSize * sc;
            for (var k = 0; k < n; k++)
            {
                var b = bladeState[k];
                if (b.Mode == "gone") continue;
                var (x, y, depth) = BladeHome(k, n);
                Sprite img; var alpha = 255;
                if (b.Mode == "fly")
                {
                    if (!front) continue;
                    img = BladeFrame(Math.Atan2(b.Ty - b.Y0, b.Tx - b.X0));
                    foreach (var (lag, ga) in new[] { (0.09f, 70), (0.045f, 130) })
                    {
                        var u0 = (float)Math.Pow(Math.Max(0f, Math.Min(1f, (b.T - lag) / BladeFlight)), 1.6);
                        var gr = IRect.AtCenter((int)(b.X0 + (b.Tx - b.X0) * u0), (int)(b.Y0 + (b.Ty - b.Y0) * u0), size, size);
                        canvas.Blit(img, gr.X, gr.Y, sc, Rgba.White.WithAlpha(ga));
                    }
                    var u = (float)Math.Pow(Math.Min(1f, b.T / BladeFlight), 1.6);
                    x = b.X0 + (b.Tx - b.X0) * u; y = b.Y0 + (b.Ty - b.Y0) * u;
                }
                else
                {
                    if ((depth >= 0) != front) continue;
                    img = BladeFrame(Math.PI / 2 + 0.25 * Math.Cos(bladeAngle + k));
                    if (b.Mode == "form") alpha = (int)(255 * Math.Min(1f, b.T / 0.35f));
                }
                var r = IRect.AtCenter((int)x, (int)y, size, size);
                canvas.Blit(img, r.X, r.Y, sc, Rgba.White.WithAlpha(alpha));
            }
        }

        private void Ambient(float dt)
        {
            if (!anchor.HasValue || Dead) return;
            wispClock -= dt;
            if (wispClock > 0) return;
            wispClock += rng.UniformF(0.12f, 0.26f);
            var (ax, ay) = anchor.Value; var r = rng; var st = Style; var k = st.AmbientRise;
            var x = ax + r.UniformF(-st.ShadowW * 0.3f, st.ShadowW * 0.32f);
            if (st.AmbientHeavy)
                Particles.Emit(x, ay - r.UniformF(60, 130), r.UniformF(-6, 6), r.UniformF(4, 14), r.UniformF(1.2f, 2.0f), st.Wisp, r.UniformF(1.0f, 2.0f), ParticleStyle.Square, 0.3f);
            else if (r.Random() < 0.55)
                Particles.Emit(x, ay - r.UniformF(8, 30), r.UniformF(-8, 8), r.UniformF(-50, -22) * k, r.UniformF(1.0f, 1.8f) / Math.Max(0.5f, k), st.Wisp, r.UniformF(1.5f, 3.0f), ParticleStyle.Square, 0.3f);
            else
                Particles.Emit(x, ay - r.UniformF(10, 40), r.UniformF(-6, 6), r.UniformF(-30, -14) * k, r.UniformF(1.2f, 2.0f), st.Wisp, r.UniformF(6, 11), ParticleStyle.Glow, 0.2f);
        }

        // ------------------------------------------------------------------ drawing
        public Sprite CurrentFrame() => Action != null ? Sheet.Frame(Action, actionTime) : Sheet.Frame("idle", idleTime);

        /// <summary>_shadow(w, 26): concentric SRCALPHA ellipses (each overwrites).</summary>
        private static Sprite ShadowSprite(Art art, int w, int h)
        {
            return new Sprite(art.Procedural("enemy_shadow_" + w + "x" + h, w, h, () =>
            {
                var px = new Rgba[w * h];
                for (var i = 6; i > 0; i--)
                {
                    var kk = i / 6.0; var a = (int)(26 * (1.2 - kk) + 10);
                    double ew = w * kk, eh = h * kk, cx = w / 2.0, cy = h / 2.0;
                    for (var y = 0; y < h; y++)
                        for (var x = 0; x < w; x++)
                        {
                            double dx = (x + 0.5 - cx) / (ew / 2), dy = (y + 0.5 - cy) / (eh / 2);
                            if (dx * dx + dy * dy <= 1) px[y * w + x] = new Rgba(0, 0, 0, a);
                        }
                }
                return px;
            }));
        }

        public void Draw(Canvas canvas, Art art, (float x, float y) anchorPoint)
        {
            anchor = anchorPoint;
            int ax = (int)anchorPoint.x, ay = (int)anchorPoint.y;
            var fade = 1f;
            if (Dead) fade = Math.Max(0f, 1f - actionTime / Math.Max(1e-6f, Sheet.Seconds(final)));
            var st = Style;
            if (fade > 0)
            {
                var sw = st.ShadowW;
                canvas.Blit(ShadowSprite(art, sw, 26), ax - sw / 2, ay - 13, 1f, Rgba.White.WithAlpha((int)(255 * fade)));
                var pulse = 0.5 + 0.5 * Math.Sin(fxTime * 2.4);
                var boost = Action != null && Action != "hurt" && Action != "death" ? 2 : 0;
                var level = Math.Max(1, Math.Min(6, (int)((2 + 2 * pulse + boost) * fade + 0.5)));
                var gw = sw + 40;
                var kk = level / 6.0;
                var col = new Rgba((int)(st.Glow.R * kk), (int)(st.Glow.G * kk), (int)(st.Glow.B * kk));
                canvas.BlitRect(Glows.Soft(art), new RectF(ax - gw / 2, ay - 22, gw, 44), col, Blend.Add);
            }
            if (st.Blades) DrawBlades(canvas, false);
            if (!DeathDone)
            {
                var frame = CurrentFrame(); var (w, h) = Sheet.Anchor;
                canvas.Blit(frame, ax - w, ay - h, Sheet.Scale);
            }
            if (st.Blades) DrawBlades(canvas, true);
            Particles.Draw(canvas, art);
        }
    }
}
