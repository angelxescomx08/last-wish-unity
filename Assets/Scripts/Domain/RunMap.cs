using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

namespace LastWish.Domain
{
    public enum RoomKind { Combat, Treasure, Shop, Event, Boss, Warlock, Gacha, Purge, Elite }

    public sealed class MapRoom
    {
        private readonly List<string> connections = new List<string>();
        public string Id { get; }
        public int Row { get; }
        public int Column { get; }
        public RoomKind Kind { get; internal set; }
        public bool Visited { get; internal set; }
        public bool Available { get; internal set; }
        public ReadOnlyCollection<string> Connections { get; }
        internal MapRoom(int floor, int row, int column)
        { Id = RunMap.NodeId(floor, row, column); Row = row; Column = column; Connections = connections.AsReadOnly(); }
        internal void Connect(string other) { if (!connections.Contains(other)) connections.Add(other); }
    }

    /// <summary>
    /// The floor map: a faithful port of <c>map_generator.generate_map</c>. Room placement depends on
    /// CPython's set iteration order of <c>(row, col)</c> tuples, which is emulated exactly so a seed
    /// produces the same floor as the original game.
    /// </summary>
    public sealed class RunMap
    {
        private static readonly BigInteger Prime1 = BigInteger.Parse("6364136223846793005");
        private static readonly BigInteger Prime2 = BigInteger.Parse("1442695040888963407");
        private static readonly BigInteger Mask64 = (BigInteger.One << 64) - 1;
        private readonly Dictionary<string, MapRoom> lookup;
        /// <summary>Rooms in the original dict order (CPython set order of the grid).</summary>
        public ReadOnlyCollection<MapRoom> Nodes { get; }
        public string BossId { get; }
        public int Floor { get; }
        public int Rows { get; }
        public int Columns { get; }
        private RunMap(List<MapRoom> nodes, int floor, int rows, int columns, string bossId)
        {
            Nodes = nodes.AsReadOnly();
            lookup = nodes.ToDictionary(n => n.Id); Floor = floor; Rows = rows; Columns = columns; BossId = bossId;
        }
        public static string NodeId(int floor, int row, int column) => "f" + floor + "_r" + row + "_c" + column;
        public MapRoom Find(string id) => id != null && lookup.TryGetValue(id, out var node) ? node : null;
        public IEnumerable<MapRoom> AvailableNodes() => Nodes.Where(n => n.Available && !n.Visited);

        /// <summary>GameMap.mark_visited: visit a room and unlock every connection not visited yet.</summary>
        public void MarkVisited(string id)
        {
            var node = Find(id);
            if (node == null) return;
            node.Visited = true; node.Available = false;
            foreach (var child in node.Connections) { var next = Find(child); if (next != null && !next.Visited) next.Available = true; }
        }

        public static int RowsForFloor(int floor) => (int)Math.Min(12, 7L + (floor - 1L) / 2);
        public static int ColumnsForFloor(int floor) => (int)Math.Min(8, 5L + (floor - 1L) / 3);
        public static int PathsForFloor(int floor) => (int)Math.Min(6, 3L + (floor - 1L) / 3);

        public static RunMap Generate(long seed, int floor)
        {
            if (floor < 1) throw new ArgumentOutOfRangeException(nameof(floor));
            var floorSeed = (seed * Prime1 + floor * Prime2) & Mask64;
            var rng = new PyRandom(floorSeed);
            var rows = RowsForFloor(floor); var maxCols = ColumnsForFloor(floor); var paths = PathsForFloor(floor);
            var bossRow = rows - 1; var bossCol = maxCols / 2; var startCol = maxCols / 2;
            var grid = new TupleSet();
            var vertical = new Dictionary<(int, int), HashSet<(int, int)>>();
            var horizontal = new Dictionary<(int, int), HashSet<(int, int)>>();
            void AddH((int, int) a, (int, int) b)
            {
                grid.Add(a); grid.Add(b);
                if (!horizontal.TryGetValue(a, out var sa)) horizontal[a] = sa = new HashSet<(int, int)>(); sa.Add(b);
                if (!horizontal.TryGetValue(b, out var sb)) horizontal[b] = sb = new HashSet<(int, int)>(); sb.Add(a);
            }
            void AddV((int, int) a, (int, int) b)
            {
                grid.Add(a); grid.Add(b);
                if (!vertical.TryGetValue(a, out var sa)) vertical[a] = sa = new HashSet<(int, int)>(); sa.Add(b);
            }
            var steps = new[] { -1, 0, 1 };
            for (var p = 0; p < paths; p++)
            {
                var col = startCol;
                for (var row = 0; row < rows - 1; row++)
                {
                    grid.Add((row, col));
                    if (row < rows - 2)
                    {
                        var step = row == 0 ? 0 : rng.Choice(steps);
                        var next = Math.Max(0, Math.Min(maxCols - 1, col + step));
                        if (next != col) AddH((row, col), (row, next));
                        AddV((row, next), (row + 1, next)); col = next;
                    }
                    else
                    {
                        var cur = col;
                        while (cur != bossCol) { var nxt = cur + (bossCol > cur ? 1 : -1); AddH((row, cur), (row, nxt)); cur = nxt; }
                        AddV((row, bossCol), (bossRow, bossCol));
                    }
                }
            }
            grid.Add((bossRow, bossCol));

            var nodes = new List<MapRoom>();
            var byPos = new Dictionary<(int, int), MapRoom>();
            foreach (var pos in grid.InOrder())
            {
                var node = new MapRoom(floor, pos.Item1, pos.Item2)
                { Kind = pos == (bossRow, bossCol) ? RoomKind.Boss : RoomKind.Combat, Available = pos == (0, startCol) };
                if (vertical.TryGetValue(pos, out var v)) foreach (var t in v) node.Connect(NodeId(floor, t.Item1, t.Item2));
                if (horizontal.TryGetValue(pos, out var h)) foreach (var t in h) node.Connect(NodeId(floor, t.Item1, t.Item2));
                nodes.Add(node); byPos[pos] = node;
            }
            var map = new RunMap(nodes, floor, rows, maxCols, NodeId(floor, bossRow, bossCol));

            var midRows = Enumerable.Range(1, Math.Max(0, rows - 3)).ToList();
            var pool = nodes.Where(n => n.Kind == RoomKind.Combat && midRows.Contains(n.Row)).ToList();
            MapRoom PickFromRows(ICollection<int> targetRows)
            {
                var candidates = pool.Where(n => targetRows.Contains(n.Row) && n.Kind == RoomKind.Combat).ToList();
                if (candidates.Count == 0) return null;
                var chosen = rng.Choice(candidates); pool.Remove(chosen); return chosen;
            }
            List<MapRoom> SortedFallback() => nodes.Where(n => n.Kind == RoomKind.Combat && n.Row > 0 && n.Row < rows - 1)
                .OrderBy(n => n.Id, StringComparer.Ordinal).ToList();

            var treasure = PickFromRows(midRows);
            if (treasure != null) treasure.Kind = RoomKind.Treasure;
            var treasureRow = treasure?.Row ?? -1;
            var shop = PickFromRows(midRows.Where(r => r != treasureRow).ToList());
            if (shop != null) shop.Kind = RoomKind.Shop;
            var events = rng.Choice(new[] { 1, 2 });
            for (var i = 0; i < events; i++) { var e = PickFromRows(midRows); if (e != null) e.Kind = RoomKind.Event; }
            var warlock = PickFromRows(midRows);
            if (warlock == null) { var fallback = SortedFallback(); warlock = fallback.Count > 0 ? rng.Choice(fallback) : null; }
            if (warlock != null) warlock.Kind = RoomKind.Warlock;
            var shopRow = shop?.Row ?? -1;
            var gacha = PickFromRows(midRows.Where(r => r != shopRow).ToList()) ?? PickFromRows(midRows);
            if (gacha != null) gacha.Kind = RoomKind.Gacha;
            var warlockRow = warlock?.Row ?? -1;
            var purge = PickFromRows(midRows.Where(r => r != warlockRow).ToList()) ?? PickFromRows(midRows);
            if (purge == null) { var fallback = SortedFallback(); purge = fallback.Count > 0 ? rng.Choice(fallback) : null; }
            if (purge != null) purge.Kind = RoomKind.Purge;

            var startId = NodeId(floor, 0, startCol);
            bool Avoidable(string excluded)
            {
                var seen = new HashSet<string> { startId }; var todo = new Stack<string>(); todo.Push(startId);
                while (todo.Count > 0) foreach (var next in map.Find(todo.Pop()).Connections) if (next != excluded && seen.Add(next)) todo.Push(next);
                return seen.Contains(map.BossId);
            }
            var lateRows = Enumerable.Range(2, Math.Max(0, rows - 3)).ToList();
            var allMiddle = Enumerable.Range(1, Math.Max(0, rows - 2)).ToList();
            MapRoom elite = null;
            foreach (var rowsOk in new[] { lateRows, midRows, allMiddle })
            {
                foreach (var needAvoid in new[] { true, false })
                {
                    var options = nodes.Where(n => n.Kind == RoomKind.Combat && rowsOk.Contains(n.Row) && (!needAvoid || Avoidable(n.Id)))
                        .OrderBy(n => n.Id, StringComparer.Ordinal).ToList();
                    if (options.Count > 0) { elite = rng.Choice(options); break; }
                }
                if (elite != null) break;
            }
            if (elite != null) elite.Kind = RoomKind.Elite;
            return map;
        }

        /// <summary>Restores visited/available flags from a checkpoint.</summary>
        internal void Restore(IEnumerable<string> visited, IEnumerable<string> available)
        {
            foreach (var node in Nodes) { node.Visited = false; node.Available = false; }
            foreach (var id in visited) { var node = Find(id) ?? throw new ArgumentException("Unknown visited room"); node.Visited = true; }
            foreach (var id in available) { var node = Find(id) ?? throw new ArgumentException("Unknown available room"); node.Available = !node.Visited; }
        }

        /// <summary>Insertion-order emulation of a CPython <c>set</c> of 2-int tuples (3.8+ xxHash tuple hash).</summary>
        private sealed class TupleSet
        {
            private const int LinearProbes = 9;
            private (int, int)[] keys = new (int, int)[8];
            private ulong[] hashes = new ulong[8];
            private bool[] used = new bool[8];
            private int fill;

            private static ulong Hash((int, int) t)
            {
                const ulong P1 = 11400714785074694791UL, P2 = 14029467366897019727UL, P5 = 2870177450012600261UL;
                unchecked
                {
                    var acc = P5;
                    foreach (var item in new[] { t.Item1, t.Item2 })
                    {
                        var lane = (ulong)(long)(item == -1 ? -2 : item);
                        acc += lane * P2; acc = (acc << 31) | (acc >> 33); acc *= P1;
                    }
                    acc += 2UL ^ (P5 ^ 3527539UL);
                    return acc == ulong.MaxValue ? 1546275796UL : acc;
                }
            }

            public void Add((int, int) key)
            {
                var hash = Hash(key); var mask = (ulong)keys.Length - 1;
                var i = hash & mask; var perturb = hash;
                while (true)
                {
                    var probes = i + LinearProbes <= mask ? LinearProbes : 0;
                    var j = i;
                    for (var n = 0; n <= probes; n++, j++)
                    {
                        if (!used[j]) { used[j] = true; keys[j] = key; hashes[j] = hash; fill++; if ((ulong)fill * 5 >= mask * 3) Resize(fill * 4); return; }
                        if (hashes[j] == hash && keys[j].Equals(key)) return;
                    }
                    perturb >>= 5;
                    i = (i * 5 + 1 + perturb) & mask;
                }
            }

            private void Resize(int minUsed)
            {
                var size = 8; while (size <= minUsed) size <<= 1;
                var oldKeys = keys; var oldHashes = hashes; var oldUsed = used;
                keys = new (int, int)[size]; hashes = new ulong[size]; used = new bool[size];
                var mask = (ulong)size - 1;
                for (var e = 0; e < oldKeys.Length; e++)
                {
                    if (!oldUsed[e]) continue;
                    var hash = oldHashes[e]; var perturb = hash; var i = hash & mask;
                    while (true)
                    {
                        if (!used[i]) break;
                        var placed = false;
                        if (i + LinearProbes <= mask)
                            for (var j = 1; j <= LinearProbes; j++) if (!used[i + (ulong)j]) { i += (ulong)j; placed = true; break; }
                        if (placed) break;
                        perturb >>= 5; i = (i * 5 + 1 + perturb) & mask;
                    }
                    used[i] = true; keys[i] = oldKeys[e]; hashes[i] = hash;
                }
            }

            public IEnumerable<(int, int)> InOrder() { for (var i = 0; i < keys.Length; i++) if (used[i]) yield return keys[i]; }
        }
    }
}
