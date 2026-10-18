using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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
        { Id = $"f{floor}_r{row}_c{column}"; Row = row; Column = column; Connections = connections.AsReadOnly(); }
        internal void Connect(MapRoom other) { if (!connections.Contains(other.Id)) connections.Add(other.Id); }
    }

    public sealed class RunMap
    {
        private readonly Dictionary<string, MapRoom> lookup;
        public ReadOnlyCollection<MapRoom> Nodes { get; }
        public string BossId { get; }
        public int Rows { get; }
        public int Columns { get; }
        private RunMap(IEnumerable<MapRoom> nodes, int rows, int columns, string bossId)
        {
            Nodes = nodes.OrderBy(n => n.Row).ThenBy(n => n.Column).ToList().AsReadOnly();
            lookup = Nodes.ToDictionary(n => n.Id); Rows = rows; Columns = columns; BossId = bossId;
        }
        public MapRoom Find(string id) => id != null && lookup.TryGetValue(id, out var node) ? node : null;
        public bool CanReachBoss(string excluded = null)
        {
            var start = Nodes.First(n => n.Row == 0).Id;
            var seen = new HashSet<string> { start }; var todo = new Stack<string>(); todo.Push(start);
            while (todo.Count > 0)
                foreach (var next in Find(todo.Pop()).Connections)
                    if (next != excluded && seen.Add(next)) todo.Push(next);
            return seen.Contains(BossId);
        }
        internal void Unlock(MapRoom node)
        { foreach (var id in node.Connections) if (!lookup[id].Visited) lookup[id].Available = true; }

        public static RunMap Generate(int seed, int floor)
        {
            if (floor < 1) throw new ArgumentOutOfRangeException(nameof(floor));
            var rows = (int)Math.Min(12, 7L + (floor - 1L) / 2);
            var columns = (int)Math.Min(8, 5L + (floor - 1L) / 3);
            var paths = (int)Math.Min(6, 3L + (floor - 1L) / 3);
            var random = new Random(unchecked(seed * 397 ^ floor * 7919));
            var grid = new Dictionary<(int, int), MapRoom>();
            MapRoom Node(int r, int c)
            {
                if (!grid.TryGetValue((r, c), out var node)) grid[(r, c)] = node = new MapRoom(floor, r, c);
                return node;
            }
            void Horizontal(int r, int a, int b) { Node(r, a).Connect(Node(r, b)); Node(r, b).Connect(Node(r, a)); }
            var center = columns / 2;
            for (var path = 0; path < paths; path++)
            {
                var col = center;
                for (var row = 0; row < rows - 1; row++)
                {
                    Node(row, col);
                    if (row < rows - 2)
                    {
                        var next = Math.Max(0, Math.Min(columns - 1, col + (row == 0 ? 0 : random.Next(-1, 2))));
                        if (next != col) Horizontal(row, col, next);
                        Node(row, next).Connect(Node(row + 1, next)); col = next;
                    }
                    else
                    {
                        while (col != center) { var next = col + Math.Sign(center - col); Horizontal(row, col, next); col = next; }
                        Node(row, col).Connect(Node(rows - 1, center));
                    }
                }
            }
            var boss = Node(rows - 1, center); boss.Kind = RoomKind.Boss; Node(0, center).Available = true;
            var map = new RunMap(grid.Values, rows, columns, boss.Id);
            MapRoom Pick(RoomKind kind, Func<MapRoom, bool> predicate)
            {
                var candidates = map.Nodes.Where(n => n.Kind == RoomKind.Combat && predicate(n)).ToArray();
                if (candidates.Length == 0) return null;
                var chosen = candidates[random.Next(candidates.Length)]; chosen.Kind = kind; return chosen;
            }
            bool Middle(MapRoom n) => n.Row > 0 && n.Row < rows - 2;
            var treasure = Pick(RoomKind.Treasure, Middle);
            var shop = Pick(RoomKind.Shop, n => Middle(n) && n.Row != treasure?.Row);
            for (var i = random.Next(1, 3); i > 0; i--) Pick(RoomKind.Event, Middle);
            var warlock = Pick(RoomKind.Warlock, Middle) ?? Pick(RoomKind.Warlock, n => n.Row > 0 && n.Row < rows - 1);
            var gacha = Pick(RoomKind.Gacha, n => Middle(n) && n.Row != shop?.Row) ?? Pick(RoomKind.Gacha, Middle);
            var purge = Pick(RoomKind.Purge, n => Middle(n) && n.Row != warlock?.Row) ?? Pick(RoomKind.Purge, Middle)
                ?? Pick(RoomKind.Purge, n => n.Row > 0 && n.Row < rows - 1);
            _ = Pick(RoomKind.Elite, n => n.Row >= 2 && n.Row < rows - 1 && map.CanReachBoss(n.Id))
                ?? Pick(RoomKind.Elite, n => n.Row >= 2 && n.Row < rows - 1)
                ?? Pick(RoomKind.Elite, n => n.Row > 0 && n.Row < rows - 1);
            return map;
        }
    }
}
