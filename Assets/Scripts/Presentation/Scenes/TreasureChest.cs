using System;
using System.Collections.Generic;

namespace LastWish.Presentation
{
    /// <summary>
    /// The chest of the treasure room, drawn by code in native pixels (shown ×2, docs/art-style.md), with the look of the
    /// map's treasure icon: dark oak planks, a rounded lid, two gold straps with rivets, a gold rim and a lock plate, iron feet.
    /// Four complete frames, each computed whole from one label map with one ink outline: closed, ajar (lid lifted 2 and 5
    /// pixels, gold light in the gap) and open (the lid stands back showing its inner face, lit from below by a heap of
    /// coins). The body never moves between frames. Two layers like every sprite: the opaque paint and the light of the
    /// treasure (the gap, the coins, the bottom of the inner lid) to add on top.
    /// </summary>
    public static class TreasureChest
    {
        public const int W = 88, H = 72, Cx = 44, Left = 12, Right = 76, Ground = 70;
        public const int BodyTop = 42, BodyBottom = 67, LidBase = 34, DomeH = 12, Mouth = 38, OpenLidTop = 14, BackLip = 36;
        public const int Frames = 4, Open = 3;
        /// <summary>The lock plate (native), where the closed chest glints.</summary>
        public const int LockX = 44, LockY = 47;

        private enum Zone { None, Lid, LidLip, Body, BodyRim, Band, Lock, Hasp, Keyhole, Foot, Gap, Inside, Coins, LidInner, LidEdge }

        private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
        private static float B(int x, int y) => (Bayer[y & 3, x & 3] + 0.5f) / 16f;
        private static uint Hash(int a, int b) { var h = (uint)(a * 73856093) ^ (uint)(b * 19349663); h ^= h >> 13; h *= 0x5bd1e995; return h ^ (h >> 15); }

        public static readonly Rgba Ink = new Rgba(7, 5, 12);
        // the map icon's ramps (Tools/VfxLab/map_icons.py WOOD / GOLD / IRON) with one more step at each end
        private static readonly Rgba[] Wood = { new Rgba(24, 12, 9), new Rgba(42, 22, 16), new Rgba(74, 42, 26), new Rgba(110, 67, 40), new Rgba(148, 96, 58), new Rgba(185, 133, 84) };
        private static readonly Rgba[] Gold = { new Rgba(59, 42, 20), new Rgba(94, 70, 38), new Rgba(138, 106, 58), new Rgba(217, 178, 106), new Rgba(244, 220, 156), new Rgba(255, 246, 220) };
        private static readonly Rgba[] Iron = { new Rgba(20, 18, 32), new Rgba(35, 32, 54), new Rgba(54, 50, 79), new Rgba(77, 72, 112) };
        private static readonly Rgba Warm = new Rgba(255, 190, 90);

        private static readonly int[] BandX = { 22, 61 };
        private const int BandW = 5;

        /// <summary>How far the lid is lifted in a closed or ajar frame.</summary>
        public static int Lift(int frame) => frame == 1 ? 2 : frame == 2 ? 5 : 0;

        /// <summary>Top of the closed (domed) lid at column <paramref name="x"/>, before lifting.</summary>
        private static float DomeTop(int x)
        {
            var u = (x + 0.5f - Cx) / ((Right - Left) / 2f);
            return LidBase - DomeH * (float)Math.Sqrt(Math.Max(0f, 1f - u * u));
        }

        /// <summary>Top of the coin heap in the open chest.</summary>
        private static float CoinTop(int x)
        {
            var u = (x + 0.5f - Cx) / ((Right - Left - 4) / 2f);
            var mound = (float)Math.Pow(Math.Max(0f, 1f - u * u), 0.6) * 7f;
            var bumps = (float)(Math.Sin(x * 1.9) * 0.6 + Math.Sin(x * 0.7 + 1.3) * 0.5);
            return BodyTop - 1 - mound - bumps;
        }

        private static bool InBand(int x) { foreach (var b in BandX) if (x >= b && x < b + BandW) return true; return false; }
        private static int BandCol(int x) { foreach (var b in BandX) if (x >= b && x < b + BandW) return x - b; return -1; }

        private static bool LockPlate(int x, int y)
        {
            if (x < 39 || x >= 49 || y < BodyTop + 1 || y >= 53) return false;
            if (y == 52 && (x == 39 || x == 48)) return false;          // rounded bottom corners
            return true;
        }

        private static bool Keyhole(int x, int y) => y == 46 && (x == 43 || x == 44) || y == 47 && x >= 42 && x <= 45 || y >= 48 && y <= 50 && (x == 43 || x == 44);

        private static Zone ZoneAt(int frame, int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return Zone.None;
            var inside = x >= Left && x < Right;
            // iron feet under the two front corners
            if (y >= BodyBottom && y < Ground && (x >= Left && x < Left + 9 || x >= Right - 9 && x < Right)) return Zone.Foot;
            if (!inside) return Zone.None;
            if (y >= BodyTop && y < BodyBottom)
            {
                if (LockPlate(x, y)) return Keyhole(x, y) ? Zone.Keyhole : Zone.Lock;
                if (y < BodyTop + 2) return Zone.BodyRim;
                return InBand(x) ? Zone.Band : Zone.Body;
            }
            if (frame == Open)
            {
                if (y >= BackLip && y < BodyTop) return y >= CoinTop(x) && x > Left && x < Right - 1 ? Zone.Coins : Zone.Inside;
                if (y >= CoinTop(x) && y < BodyTop && x > Left + 2 && x < Right - 3) return Zone.Coins;   // the heap rises over the back lip
                if (y >= OpenLidTop && y < BackLip)
                {
                    var cut = y == OpenLidTop ? 3 : y == OpenLidTop + 1 ? 1 : 0;           // rounded top corners
                    if (x < Left + cut || x >= Right - cut) return Zone.None;
                    if (y < OpenLidTop + 3) return Zone.LidLip;
                    if (x < Left + 2 || x >= Right - 2) return Zone.LidEdge;
                    return Zone.LidInner;
                }
                return Zone.None;
            }
            var lift = Lift(frame);
            if (y >= BodyTop - lift && y < BodyTop) return x > Left && x < Right - 1 ? Zone.Gap : Zone.None;
            var ly = y + lift;                                            // the lid in its own (unlifted) rows
            if (ly < BodyTop && ly >= DomeTop(x))
            {
                if (x >= 41 && x < 47 && ly >= Mouth && !(x >= 43 && x < 45 && ly >= Mouth + 1 && ly < BodyTop - 1)) return Zone.Hasp;
                if (ly >= BodyTop - 2) return Zone.LidLip;
                return InBand(x) ? Zone.Band : Zone.Lid;
            }
            return Zone.None;
        }

        private static Rgba Ramp(Rgba[] ramp, float tone) => ramp[Math.Max(0, Math.Min(ramp.Length - 1, (int)Math.Round(tone)))];

        /// <summary>The paint and the light of frame <paramref name="frame"/> (0 closed … 3 open).</summary>
        public static (Rgba[] paint, Rgba[] light) Paint(int frame)
        {
            frame = Math.Max(0, Math.Min(Frames - 1, frame));
            var paint = new Rgba[W * H]; var light = new Rgba[W * H];
            var zones = new Zone[W * H];
            for (var y = 0; y < H; y++) for (var x = 0; x < W; x++) zones[y * W + x] = ZoneAt(frame, x, y);
            Zone Z(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? Zone.None : zones[y * W + x];
            var lift = Lift(frame);
            for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                {
                    var i = y * W + x; var z = zones[i];
                    if (z == Zone.None) continue;
                    var across = (x - Left) / (float)(Right - Left);          // 0 left (lit) … 1 right
                    var d = B(x, y) - 0.5f;
                    switch (z)
                    {
                        case Zone.Body:
                        {
                            // three planks with a lit top row, a shaded bottom row and drifting grain
                            var ry = y - (BodyTop + 2); var plank = ry < 7 ? 0 : ry < 15 ? 1 : 2; var top = plank == 0 ? 0 : plank == 1 ? 7 : 15; var bottom = plank == 0 ? 6 : plank == 1 ? 14 : 22;
                            if (ry == 7 || ry == 15) { paint[i] = Wood[0]; break; }
                            var tone = 3.0f - across * 1.3f + (float)Math.Sin(x * 0.33 + plank * 2.1 + Math.Sin(x * 0.07) * 2.2) * 0.35f + d * 0.5f;
                            if (ry == top + (plank == 0 ? 0 : 1)) tone += 1f; else if (ry == bottom) tone -= 0.9f;
                            if (ry > top + 1 && ry < bottom - 1 && Hash(x / 3, plank) % 9 == 0 && Hash(x, y) % 3 == 0) tone -= 0.8f;   // a few knots
                            paint[i] = Ramp(Wood, Math.Max(1, tone));
                            break;
                        }
                        case Zone.Lid:
                        {
                            var ly = y + lift; var top = DomeTop(x);
                            if (ly == 30 || ly == 36) { paint[i] = Wood[1]; break; }
                            // the dome: lit on its upper left, the light rolling off towards the lip
                            var curve = 1f - (ly - top) / Math.Max(1f, BodyTop - 2 - top);
                            var tone = 2.6f - across * 1.4f + curve * 1.2f + d * 0.5f;
                            if (ly - top < 1.2f) tone += 0.7f;
                            paint[i] = Ramp(Wood, Math.Max(1, tone));
                            break;
                        }
                        case Zone.Band:
                        {
                            var c = BandCol(x); var ly = y + (y < BodyTop ? lift : 0);
                            var tone = c == 0 ? 4f : c == BandW - 1 ? 1.6f : 3f - c * 0.3f;
                            tone -= across * 0.6f; tone += d * 0.6f;
                            if (y < BodyTop && ly - DomeTop(x) < 1.2f) tone += 0.6f;
                            paint[i] = Ramp(Gold, tone);
                            var rivet = c == 2 && (ly % 6 == 3);
                            if (rivet) paint[i] = Gold[5];
                            else if (c == 2 && ly % 6 == 4) paint[i] = Gold[1];
                            break;
                        }
                        case Zone.LidLip:
                            if (frame == Open) paint[i] = y == OpenLidTop ? Gold[4] : y == OpenLidTop + 1 ? Gold[3] : Gold[1];
                            else paint[i] = y + lift == BodyTop - 2 ? Gold[4 - (across > 0.6f ? 1 : 0)] : Gold[2];
                            if (x % 8 == 4 && (frame == Open ? y == OpenLidTop + 1 : y + lift == BodyTop - 1)) paint[i] = Gold[5];
                            break;
                        case Zone.BodyRim:
                            paint[i] = y == BodyTop ? Gold[across > 0.7f ? 3 : 4] : Gold[2];
                            if (x % 8 == 0 && y == BodyTop + 1) paint[i] = Gold[5];
                            break;
                        case Zone.Lock:
                        {
                            var edgeL = !LockPlate(x - 1, y) || !LockPlate(x, y - 1); var edgeR = !LockPlate(x + 1, y) || !LockPlate(x, y + 1);
                            paint[i] = edgeL ? Gold[4] : edgeR ? Gold[1] : Ramp(Gold, 3f - (y - 44) * 0.08f + d * 0.8f);
                            if ((x == 40 || x == 47) && (y == 44 || y == 51)) paint[i] = Gold[5];
                            break;
                        }
                        case Zone.Keyhole: paint[i] = Ink; break;
                        case Zone.Hasp: paint[i] = Z(x - 1, y) != Zone.Hasp || Z(x, y - 1) != Zone.Hasp ? Gold[4] : Gold[2]; break;
                        case Zone.Foot: paint[i] = y == BodyBottom ? Iron[3] : Ramp(Iron, 2f - (y - BodyBottom) * 0.4f + d * 0.6f); break;
                        case Zone.Gap:
                        {
                            // light pouring out between the lid and the body
                            var k = 1f - Math.Abs(x + 0.5f - Cx) / ((Right - Left) / 2f);
                            paint[i] = k + d * 0.5f > 0.35f ? Gold[5] : Gold[4];
                            light[i] = Glows.Scaled(Warm, 0.55 + 0.45 * k);
                            break;
                        }
                        case Zone.Inside:
                            paint[i] = y == BackLip ? Ink : y == BackLip + 1 ? Wood[4] : Ramp(Wood, 1.6f - (y - BackLip - 2) * 0.25f + d * 0.6f);
                            if (y > BackLip + 1) light[i] = Glows.Scaled(Warm, 0.12);
                            break;
                        case Zone.Coins:
                        {
                            // a heap of coins: round tops picked out in light, the hollows darker, a few sparkles
                            var cx = (int)Math.Floor((x + (y % 2) * 1.5f) / 3f); var coin = Hash(cx, y / 2);
                            var tone = 3f + ((coin % 3) - 1) * 0.6f + (y - CoinTop(x) < 1.2f ? 1.2f : 0f) - across * 0.5f + d * 0.8f;
                            if ((x + y) % 3 == 0 && coin % 5 == 0) tone -= 1.2f;
                            paint[i] = Ramp(Gold, Math.Max(2, tone));
                            if (coin % 23 == 0 && (x + y) % 2 == 0) paint[i] = Gold[5];
                            var lvl = Math.Max(0, Math.Min(5, (int)Math.Round(tone)));
                            light[i] = Glows.Scaled(Warm, 0.18 + 0.1 * Math.Max(0, lvl - 2));
                            break;
                        }
                        case Zone.LidInner:
                        {
                            // the inside of the lid, lit from below by the coins
                            var ry = y - (OpenLidTop + 3);
                            if (ry == 6 || ry == 12) { paint[i] = Wood[0]; break; }
                            var tone = 0.9f + ry / 18f * 2.4f - Math.Abs(across - 0.5f) * 0.8f + d * 0.5f;
                            paint[i] = Ramp(Wood, tone);
                            if (ry >= 13) light[i] = Glows.Scaled(Warm, 0.06 + 0.03 * (ry - 13));
                            break;
                        }
                        case Zone.LidEdge: paint[i] = x == Left || x == Right - 1 ? Wood[3] : Wood[4]; break;
                    }
                }
            // one ink outline round the silhouette, and a seam between the lid and the light of the gap
            var outline = new List<int>();
            for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                {
                    var i = y * W + x;
                    if (zones[i] != Zone.None) continue;
                    if (Z(x - 1, y) != Zone.None || Z(x + 1, y) != Zone.None || Z(x, y - 1) != Zone.None || Z(x, y + 1) != Zone.None) outline.Add(i);
                }
            foreach (var i in outline) { paint[i] = Ink; light[i] = default; }
            return (paint, light);
        }

        /// <summary>The cached sprites of a frame: paint and light.</summary>
        public static (Sprite paint, Sprite light) Sprites(Art art, int frame)
        {
            frame = Math.Max(0, Math.Min(Frames - 1, frame));
            Rgba[] p = null, l = null;
            void Build() { if (p == null) (p, l) = Paint(frame); }
            var paint = new Sprite(art.Procedural("treasure_chest_" + frame, W, H, () => { Build(); return p; }));
            var light = new Sprite(art.Procedural("treasure_chest_light_" + frame, W, H, () => { Build(); return l; }));
            return (paint, light);
        }

        /// <summary>A soft column of light (white on black, drawn additively and tinted) rising from the open chest.</summary>
        public static Sprite Beam(Art art) => new Sprite(art.Procedural("treasure_beam", 32, 96, () =>
        {
            var px = new Rgba[32 * 96];
            for (var y = 0; y < 96; y++)
                for (var x = 0; x < 32; x++)
                {
                    var spread = 5.5 + (95 - y) / 95.0 * 6.5;               // wider as it rises
                    var v = Math.Exp(-Math.Pow((x - 15.5) / spread, 2)) * Math.Pow(y / 95.0, 1.6);
                    var c = (int)(255 * v);
                    px[y * 32 + x] = new Rgba(c, c, c, 255);
                }
            return px;
        }, smooth: true));
    }
}
