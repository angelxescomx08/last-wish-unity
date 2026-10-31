using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>
    /// The floor map as the plan of the crypt seen from above (docs/art-style.md), in art pixels (one art pixel =
    /// <see cref="Scale"/> screen pixels). Every room is a chamber carved in its cell of the grid, with a brick north wall
    /// and a flagstone floor; corridors are carved between connected rooms; the entrance has stairs coming up from the bottom
    /// edge; the boss has a great hall with pillars and a crimson rug that reaches into the top margin.
    /// </summary>
    public sealed class MapPlan
    {
        public const int Scale = 2;
        public const byte Rock = 0, Floor = 1, Corridor = 2, Stairs = 3, Hall = 4, Rug = 5;
        /// <summary>The cell sizes the plan uses (screen px): multiples of 8, so a cell is a whole number of 4x4 dither tiles.</summary>
        public const int MinCell = 40, MaxCell = 72;

        public sealed class Room
        {
            public MapRoom Node;
            public int Index;
            public int X, Y, W, H;               // the carved chamber (art px)
            public int FloorTop;                 // first floor row under its north wall
            public (int x, int y) Centre;        // middle of its floor (art px)
            public (int x, int y)[] Torches;     // sconces on its north wall
            public bool Boss, Start;
        }

        public RunMap Map { get; private set; }
        public int Rows { get; private set; }
        public int Cols { get; private set; }
        /// <summary>Art pixels per grid cell.</summary>
        public int S { get; private set; }
        public int Margin { get; private set; }
        public int W { get; private set; }
        public int H { get; private set; }
        public int Inset { get; private set; }
        public int WallH { get; private set; }
        public int CorridorW { get; private set; }
        public readonly List<Room> Rooms = new List<Room>();
        public readonly Dictionary<string, Room> ById = new Dictionary<string, Room>();
        /// <summary>Per art pixel: Rock, Floor, Corridor, Stairs, Hall or Rug.</summary>
        public byte[] Kind { get; private set; }
        /// <summary>Per art pixel: the room a carved pixel belongs to (a corridor belongs to its nearer end), -1 for rock.</summary>
        public short[] Owner { get; private set; }
        /// <summary>Per art pixel: carved, but inside a north wall face (the rock above is that close).</summary>
        public bool[] Wall { get; private set; }
        /// <summary>Per room: the box around every pixel it owns (its chamber, its halves of the corridors, its stairs).</summary>
        public (int x, int y, int w, int h)[] Bounds { get; private set; }

        /// <summary>The cell size (screen px) that fits the grid, its margins and the boss hall in the area under the header.</summary>
        public static int CellPx(int rows, int cols, int areaW, int areaH)
        {
            var fit = Math.Min(areaW / (cols + 0.75), areaH / (rows + 0.75));
            return Math.Max(MinCell, Math.Min(MaxCell, (int)(fit / 8) * 8));
        }

        public bool Carved(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && Kind[y * W + x] != Rock;
        public bool InWall(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && Wall[y * W + x];

        public static MapPlan Build(RunMap map, int cellPx)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            var p = new MapPlan { Map = map, Rows = Math.Max(1, map.Rows), Cols = Math.Max(1, map.Columns) };
            p.S = Math.Max(MinCell, cellPx) / Scale;
            p.Margin = Math.Max(5, p.S * 3 / 8);
            p.W = p.Cols * p.S + 2 * p.Margin; p.H = p.Rows * p.S + 2 * p.Margin;
            // a fifth of the cell, but never so much that a chamber gets narrower than its 16-px icon and a pixel each side
            p.Inset = Math.Max(3, Math.Min((int)Math.Round(p.S * 0.2), (p.S - 18) / 2));
            p.WallH = Math.Max(3, p.S / 8);
            p.CorridorW = Math.Max(6, (int)Math.Round(p.S * 0.26 / 2) * 2);
            p.Kind = new byte[p.W * p.H]; p.Owner = new short[p.W * p.H]; p.Wall = new bool[p.W * p.H];
            for (var i = 0; i < p.Owner.Length; i++) p.Owner[i] = -1;

            var occupied = new HashSet<(int, int)>();
            foreach (var n in map.Nodes) occupied.Add((n.Row, n.Column));
            foreach (var n in map.Nodes)
            {
                int cx0 = p.Margin + n.Column * p.S, cy0 = p.Margin + (p.Rows - 1 - n.Row) * p.S;
                var room = new Room { Node = n, Index = p.Rooms.Count, X = cx0 + p.Inset, Y = cy0 + p.Inset, W = p.S - 2 * p.Inset, H = p.S - 2 * p.Inset };
                room.Boss = n.Id == map.BossId; room.Start = n.Row == 0;
                if (room.Boss)
                {
                    // the great hall grows into the empty cells beside it and into the top margin
                    var grow = p.S * 2 / 5;
                    if (!occupied.Contains((n.Row, n.Column - 1)) && !occupied.Contains((n.Row, n.Column + 1))) { room.X -= grow; room.W += 2 * grow; }
                    var up = Math.Max(0, p.Margin - 3);
                    room.Y -= up; room.H += up;
                }
                room.FloorTop = room.Y + p.WallH;
                room.Centre = (room.X + room.W / 2, room.FloorTop + (room.Y + room.H - room.FloorTop) / 2);
                room.Torches = new[] { (room.X + 2, room.Y + 1), (room.X + room.W - 3, room.Y + 1) };
                p.Rooms.Add(room); p.ById[n.Id] = room;
            }
            // corridors first (each pixel belongs to its nearer end), then the chambers over them
            foreach (var a in p.Rooms)
                foreach (var id in a.Node.Connections)
                {
                    if (!p.ById.TryGetValue(id, out var b) || string.CompareOrdinal(a.Node.Id, id) > 0 && b.Node.Connections.Contains(a.Node.Id)) continue;
                    p.CarveCorridor(a, b);
                }
            foreach (var r in p.Rooms)
            {
                var k = r.Boss ? Hall : Floor;
                p.Fill(r.X, r.Y, r.W, r.H, k, r.Index);
                if (r.Start) p.Fill(r.Centre.x - p.CorridorW / 2, r.Y + r.H, p.CorridorW, p.H - (r.Y + r.H), Stairs, r.Index);
            }
            foreach (var r in p.Rooms)
            {
                if (!r.Boss) continue;
                // two pillars at the back of the hall, and a crimson rug from its door to the throne
                var pw = Math.Max(3, p.S / 7);
                foreach (var px in new[] { r.X + pw, r.X + r.W - 2 * pw })
                    for (var y = r.FloorTop + 2; y < r.FloorTop + 2 + pw; y++)
                        for (var x = px; x < px + pw; x++) p.Set(x, y, Rock, -1);
                var half = Math.Max(2, p.CorridorW / 2 - 1);
                for (var y = r.FloorTop + 3; y < r.Y + r.H; y++)
                    for (var x = r.Centre.x - half; x < r.Centre.x + half; x++)
                        if (p.Carved(x, y)) p.Kind[y * p.W + x] = Rug;
            }
            // a north wall face wherever rock is right above (rooms and east-west corridors; stairs and north-south
            // corridors open through it)
            for (var y = 0; y < p.H; y++)
                for (var x = 0; x < p.W; x++)
                {
                    var i = y * p.W + x;
                    if (p.Kind[i] == Rock || p.Kind[i] == Stairs) continue;
                    for (var k = 1; k <= p.WallH; k++) if (!p.Carved(x, y - k)) { p.Wall[i] = true; break; }
                }
            p.Bounds = new (int, int, int, int)[p.Rooms.Count];
            var box = new int[p.Rooms.Count * 4];
            for (var i = 0; i < p.Rooms.Count; i++) { box[i * 4] = int.MaxValue; box[i * 4 + 1] = int.MaxValue; box[i * 4 + 2] = -1; box[i * 4 + 3] = -1; }
            for (var y = 0; y < p.H; y++)
                for (var x = 0; x < p.W; x++)
                {
                    var o = p.Owner[y * p.W + x];
                    if (o < 0) continue;
                    var b = o * 4;
                    if (x < box[b]) box[b] = x;
                    if (y < box[b + 1]) box[b + 1] = y;
                    if (x > box[b + 2]) box[b + 2] = x;
                    if (y > box[b + 3]) box[b + 3] = y;
                }
            for (var i = 0; i < p.Rooms.Count; i++)
                p.Bounds[i] = (box[i * 4], box[i * 4 + 1], box[i * 4 + 2] - box[i * 4] + 1, box[i * 4 + 3] - box[i * 4 + 1] + 1);
            return p;
        }

        /// <summary>A stable hash of what the plan is built from (the grid, the rooms, their connections and the boss), for
        /// texture keys and the art seed: two maps with the same layout get the same plan.</summary>
        public static uint Signature(RunMap map)
        {
            if (map == null) return 0;
            unchecked
            {
                var h = 2166136261u;
                void Mix(string s) { foreach (var ch in s ?? "") { h ^= ch; h *= 16777619u; } h ^= 0xFF; h *= 16777619u; }
                Mix(map.Rows + "x" + map.Columns + "/" + map.Floor); Mix(map.BossId);
                foreach (var n in map.Nodes) { Mix(n.Id); Mix(n.Row + "," + n.Column); foreach (var c in n.Connections) Mix(c); }
                return h;
            }
        }

        private void Set(int x, int y, byte kind, int owner)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            var i = y * W + x; Kind[i] = kind; Owner[i] = (short)owner;
        }

        private void Fill(int x0, int y0, int w, int h, byte kind, int owner)
        {
            for (var y = y0; y < y0 + h; y++) for (var x = x0; x < x0 + w; x++) Set(x, y, kind, owner);
        }

        private void CarveCorridor(Room a, Room b)
        {
            int half = CorridorW / 2;
            if (a.Node.Row == b.Node.Row)
            {
                var (l, r) = a.Centre.x <= b.Centre.x ? (a, b) : (b, a);
                var cy = l.Boss ? r.Centre.y : l.Centre.y;
                for (var x = l.Centre.x; x <= r.Centre.x; x++)
                    for (var y = cy - half - WallH; y < cy + half; y++)
                        Set(x, y, Corridor, x - l.Centre.x <= r.Centre.x - x ? l.Index : r.Index);
            }
            else
            {
                var (t, bt) = a.Centre.y <= b.Centre.y ? (a, b) : (b, a);
                var cx = t.Boss ? bt.Centre.x : t.Centre.x;
                for (var y = t.Centre.y; y <= bt.Centre.y; y++)
                    for (var x = cx - half; x < cx + half; x++)
                        Set(x, y, Corridor, y - t.Centre.y <= bt.Centre.y - y ? t.Index : bt.Index);
            }
        }

        /// <summary>Screen position of an art pixel for a plan whose top-left corner is drawn at <paramref name="origin"/>.</summary>
        public (float x, float y) ToScreen((float x, float y) origin, float ax, float ay) => (origin.x + ax * Scale, origin.y + ay * Scale);
    }

    /// <summary>Paints a <see cref="MapPlan"/>: the plan itself (fixed for a floor) and what you know of it (fog, rune circles).</summary>
    public static class MapArt
    {
        private static Rgba Hex(string h) => new Rgba(Convert.ToInt32(h.Substring(1, 2), 16), Convert.ToInt32(h.Substring(3, 2), 16), Convert.ToInt32(h.Substring(5, 2), 16));
        private static Rgba[] Ramp(params string[] hs) { var r = new Rgba[hs.Length]; for (var i = 0; i < hs.Length; i++) r[i] = Hex(hs[i]); return r; }

        public static readonly Rgba Ink = Hex("#07050c"), Mortar = Hex("#0d0b16");
        public static readonly Rgba[] RockR = Ramp("#0a0912", "#100e1a", "#171425", "#1f1b31", "#29243f");
        public static readonly Rgba[] WallR = Ramp("#1b1729", "#262036", "#322b48", "#41395d", "#544a75");
        public static readonly Rgba[] FloorR = Ramp("#141120", "#1b1729", "#231e35", "#2d2742", "#383151");
        public static readonly Rgba[] RugR = Ramp("#2e0a14", "#4a1020", "#6c1828", "#8c2232");
        public static readonly Rgba[] GoldR = Ramp("#5e4626", "#8a6a3a", "#d9b26a");
        public static readonly Rgba[] CyanR = Ramp("#0d3550", "#137ea3", "#2fc4e6", "#8ff1ff");
        public static readonly Rgba Bone = Hex("#6b6478"), RimTop = Hex("#2b2540"), RimBottom = Hex("#120f1c");

        private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
        private static float B(int x, int y) => (Bayer[y & 3, x & 3] + 0.5f) / 16f;

        public static float Hash(int x, int y, int s)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
            }
        }

        private static float Noise(float x, float y, int s)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(x0, y0, s), b = Hash(x0 + 1, y0, s), c = Hash(x0, y0 + 1, s), d = Hash(x0 + 1, y0 + 1, s);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        private static Rgba Tone(Rgba[] ramp, float v, int x, int y, float amp = 0.55f)
        {
            var i = (int)Math.Floor(v * (ramp.Length - 1) + 0.5f + (B(x, y) - 0.5f) * amp);
            return ramp[i < 0 ? 0 : i >= ramp.Length ? ramp.Length - 1 : i];
        }

        /// <summary>The plan of the floor, opaque, <c>plan.W x plan.H</c> art pixels.</summary>
        public static Rgba[] FloorPlan(MapPlan plan, int seed)
        {
            int W = plan.W, H = plan.H, S = plan.S;
            var px = new Rgba[W * H];
            var t = Math.Max(5, S / 5);
            for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                {
                    var i = y * W + x; var k = plan.Kind[i];
                    Rgba c;
                    if (k == MapPlan.Rock)
                    {
                        var edge = plan.Carved(x - 1, y) || plan.Carved(x + 1, y) || plan.Carved(x, y - 1) || plan.Carved(x, y + 1);
                        if (edge) c = Ink;
                        else if (plan.InWall(x, y + 2) && !plan.InWall(x, y + 3)) c = Tone(RockR, 0.86f, x, y, 0.4f);   // the lit lip over a wall
                        else
                        {
                            var v = 0.30f + 0.30f * Noise(x / 7f, y / 7f, seed) + 0.14f * Noise(x / 2.5f, y / 2.5f, seed + 1) - 0.12f * ((float)x / W + (float)y / H);
                            c = Tone(RockR, v, x, y);
                            if (Hash(x, y, seed + 2) > 0.988f) c = RockR[4];
                        }
                    }
                    else if (plan.Wall[i]) c = WallPixel(plan, x, y, seed);
                    else if (k == MapPlan.Stairs)
                    {
                        var owner = plan.Rooms[plan.Owner[i]];
                        var d = y - (owner.Y + owner.H);
                        var depth = (float)d / Math.Max(1, H - (owner.Y + owner.H));
                        var step = d % 3;
                        c = step == 0 ? Tone(FloorR, 0.9f - 0.5f * depth, x, y, 0.4f) : step == 1 ? Tone(FloorR, 0.55f - 0.4f * depth, x, y) : FloorR[0];
                        if (x == owner.Centre.x - plan.CorridorW / 2 || x == owner.Centre.x + plan.CorridorW / 2 - 1) c = FloorR[0];
                    }
                    else if (k == MapPlan.Rug)
                    {
                        var r = plan.Rooms[plan.Owner[i]];
                        var half = Math.Max(2, plan.CorridorW / 2 - 1);
                        if (x == r.Centre.x - half || x == r.Centre.x + half - 1) c = GoldR[1];
                        else if ((y - r.FloorTop) % 6 == 0 && Math.Abs(x - r.Centre.x + 0.5f) < 1) c = GoldR[2];
                        else c = Tone(RugR, 0.62f - 0.25f * (float)(y - r.FloorTop) / Math.Max(1, r.Y + r.H - r.FloorTop) - 0.1f * Math.Abs(x - r.Centre.x) / half, x, y);
                    }
                    else c = FloorPixel(plan, x, y, t, seed);
                    px[i] = c;
                }
            // unlit sconces on every north wall (MapScene lights them in the rooms you visited)
            foreach (var r in plan.Rooms)
                foreach (var (sx, sy) in r.Torches)
                {
                    Put(px, plan, sx, sy, GoldR[0]); Put(px, plan, sx, sy + 1, GoldR[1]); Put(px, plan, sx, sy - 1, Ink);
                }
            // the slab: an ink border, light catching its top and left edges; the stairs break through at the bottom
            for (var x = 0; x < W; x++)
            {
                if (!plan.Carved(x, H - 1)) { px[(H - 1) * W + x] = Ink; if (!plan.Carved(x, H - 2)) px[(H - 2) * W + x] = RimBottom; }
                px[x] = Ink; if (!plan.Carved(x, 1)) px[W + x] = RimTop;
            }
            for (var y = 0; y < H; y++)
            {
                if (!plan.Carved(0, y)) px[y * W] = Ink;
                if (!plan.Carved(W - 1, y)) px[y * W + W - 1] = Ink;
                if (y > 0 && y < H - 1 && !plan.Carved(1, y)) px[y * W + 1] = RimTop;
                if (y > 0 && y < H - 1 && !plan.Carved(W - 2, y)) px[y * W + W - 2] = RimBottom;
            }
            return px;
        }

        private static void Put(Rgba[] px, MapPlan plan, int x, int y, Rgba c) { if (x >= 0 && y >= 0 && x < plan.W && y < plan.H) px[y * plan.W + x] = c; }

        private static Rgba WallPixel(MapPlan plan, int x, int y, int seed)
        {
            var k = 0; while (k < plan.WallH && plan.Carved(x, y - k - 1)) k++;   // rows of wall above this one
            if (k == 0) return Tone(WallR, 0.95f, x, y, 0.3f);                       // the top edge catches the light
            if (k >= plan.WallH - 1) return WallR[0];                               // its foot, in shadow
            var course = (k - 1) / 2;
            if ((k - 1) % 2 == 1) return Mortar;
            if ((x + course * 3) % 5 == 0) return Mortar;
            var brick = (x + course * 3) / 5;
            var lit = plan.Carved(x - 1, y) ? 0f : 0.15f;
            return Tone(WallR, 0.5f + 0.35f * (Hash(brick, course + y * 7, seed + 3) - 0.5f) + lit, x, y, 0.5f);
        }

        private static Rgba FloorPixel(MapPlan plan, int x, int y, int t, int seed)
        {
            var ty = y / t; var sx = x + (ty & 1) * (t / 2);
            var tx = sx / t; int lx = sx % t, ly = y % t;
            if (lx == 0 || ly == 0) return Mortar;
            var v = 0.42f + 0.32f * (Hash(tx, ty, seed + 5) - 0.5f) - 0.08f * (lx + ly) / (2f * t);
            if (lx == 1 && ly == 1) v += 0.3f;
            // ambient occlusion along the walls and rock (light from the upper left: the west and north sides are darker)
            if (!plan.Carved(x - 1, y) || plan.InWall(x, y - 1)) v -= 0.3f;
            else if (!plan.Carved(x - 2, y) || plan.InWall(x, y - 2)) v -= 0.15f;
            if (!plan.Carved(x + 1, y) || !plan.Carved(x, y + 1)) v -= 0.12f;
            var c = Tone(FloorR, v, x, y);
            var h = Hash(x, y, seed + 7);
            if (h > 0.993f) c = RockR[4];
            else if (h > 0.9895f && plan.Carved(x + 1, y) && !plan.InWall(x + 1, y)) c = Bone;
            return c;
        }

        /// <summary>How dark the rooms you cannot reach yet are (0 = as lit as the rest, 1 = black).</summary>
        public const float FogDepth = 0.58f;

        /// <summary>A room you cannot reach yet, sunk in shadow: the plan under every pixel it owns (its chamber and its
        /// halves of the corridors) darkened towards the ink, still readable; <c>plan.Bounds[room]</c> in size, transparent
        /// outside what it owns. <paramref name="floorPlan"/> comes from <see cref="FloorPlan"/>.</summary>
        public static Rgba[] Fog(MapPlan plan, Rgba[] floorPlan, int room)
        {
            var (bx, by, bw, bh) = plan.Bounds[room];
            var px = new Rgba[bw * bh];
            for (var y = 0; y < bh; y++)
                for (var x = 0; x < bw; x++)
                {
                    var i = (by + y) * plan.W + bx + x;
                    if (plan.Owner[i] != room) continue;
                    var c = floorPlan[i];
                    px[y * bw + x] = new Rgba((int)(c.R + (Ink.R - c.R) * FogDepth), (int)(c.G + (Ink.G - c.G) * FogDepth), (int)(c.B + (Ink.B - c.B) * FogDepth));
                }
            return px;
        }

        /// <summary>The rune circle on the floor of a room you can enter, for a floor <paramref name="w"/> x
        /// <paramref name="h"/> art pixels (the chamber under its north wall): a dotted cyan ring, squashed by the top-down
        /// view, and a fainter dashed ring inside it. Transparent elsewhere.</summary>
        public static Rgba[] RuneRing(int w, int h)
        {
            var px = new Rgba[Math.Max(0, w * h)];
            var radius = Math.Min(w, h) * 0.42f;
            int cx = w / 2, cy = h / 2;
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    float dx = x + 0.5f - cx, dy = (y + 0.5f - cy) * 1.25f, d = (float)Math.Sqrt(dx * dx + dy * dy);
                    var a = Math.Atan2(dy, dx);
                    if (Math.Abs(d - radius) < 0.55f) px[y * w + x] = ((int)Math.Floor((a + Math.PI) / (2 * Math.PI) * 28) % 4 == 0) ? CyanR[3] : CyanR[2];
                    else if (Math.Abs(d - (radius - 2.2f)) < 0.5f && ((int)Math.Floor((a + Math.PI) / (2 * Math.PI) * 20) & 1) == 0) px[y * w + x] = CyanR[1];
                }
            return px;
        }

        /// <summary>The floor area of a room (art px): its chamber under the north wall, where <see cref="RuneRing"/> goes.</summary>
        public static (int x, int y, int w, int h) FloorArea(MapPlan.Room r) => (r.X, r.FloorTop, r.W, r.Y + r.H - r.FloorTop);

        /// <summary>What you know of the floor in one layer the size of the plan (tests and previews; the game draws the
        /// same pieces one by one): <see cref="Fog"/> over every room you cannot reach yet, <see cref="RuneRing"/> in every
        /// room you can enter, transparent elsewhere.</summary>
        public static Rgba[] StateLayer(MapPlan plan, Rgba[] floorPlan)
        {
            var px = new Rgba[plan.W * plan.H];
            foreach (var r in plan.Rooms)
            {
                var n = r.Node;
                if (n.Visited) continue;
                if (n.Available)
                {
                    var (fx, fy, fw, fh) = FloorArea(r);
                    var ring = RuneRing(fw, fh);
                    for (var y = 0; y < fh; y++) for (var x = 0; x < fw; x++) if (ring[y * fw + x].A > 0) px[(fy + y) * plan.W + fx + x] = ring[y * fw + x];
                    continue;
                }
                var (bx, by, bw, bh) = plan.Bounds[r.Index];
                var fog = Fog(plan, floorPlan, r.Index);
                for (var y = 0; y < bh; y++) for (var x = 0; x < bw; x++) if (fog[y * bw + x].A > 0) px[(by + y) * plan.W + bx + x] = fog[y * bw + x];
            }
            return px;
        }
    }
}
