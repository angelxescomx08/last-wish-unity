using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>The floor map as the plan of the crypt (Map/MapArt.cs, Map/MapIcons.cs, Scenes/MapScene.cs): the plan's
    /// geometry and palette, the fog and the rune circles, the room icons, and what the scene draws and where it takes clicks;
    /// headless, no captures.</summary>
    public sealed class MapArtTests
    {
        private sealed class Recorder : IRenderBackend
        {
            public readonly List<(string tex, Blend blend, Rgba color, float x0, float y0, float x1, float y1)> Quads = new List<(string, Blend, Rgba, float, float, float, float)>();
            public readonly Dictionary<string, (int w, int h, Rgba[] px)> Created = new Dictionary<string, (int, int, Rgba[])>();
            private int next = 1;
            public Tex WhiteTexture { get; } = new Tex(0, "white", 1, 1);
            public Tex LoadTexture(string key) { var (w, h) = Clips.PngSize(key); return new Tex(next++, key, w, h); }
            public Tex CreateTexture(string key, int width, int height, Rgba[] pixels, bool smooth = false) { Created[key] = (width, height, pixels); return new Tex(next++, key, width, height, smooth); }
            public void BeginFrame() { }
            public void Quad(Tex tex, float u0, float v0, float u1, float v1, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, Rgba color, Blend blend)
                => Quads.Add((tex?.Key, blend, color, Math.Min(Math.Min(x0, x1), Math.Min(x2, x3)), Math.Min(Math.Min(y0, y1), Math.Min(y2, y3)),
                    Math.Max(Math.Max(x0, x1), Math.Max(x2, x3)), Math.Max(Math.Max(y0, y1), Math.Max(y2, y3))));
            public void Triangle(float x0, float y0, float x1, float y1, float x2, float y2, Rgba color, Blend blend) { }
            public void TexTriangle(Tex tex, float x0, float y0, float u0, float v0, float x1, float y1, float u1, float v1, float x2, float y2, float u2, float v2, Rgba color, Blend blend) { }
            public Tex CreateTarget(string key, int width, int height) => new Tex(next++, key, width, height, false, true);
            public void ReleaseTarget(Tex target) { }
            public void BeginTarget(Tex target) { }
            public void EndTarget() { }
            public void SetClip(RectF? clip) { }
            public void EndFrame() { }
            public IEnumerable<(string tex, Blend blend, Rgba color, float x0, float y0, float x1, float y1)> With(Func<string, bool> key) => Quads.Where(q => q.tex != null && key(q.tex));
        }

        /// <summary>Seeds and floors from the smallest grid (7x5, the biggest cells) to the biggest (12x8, the smallest).</summary>
        private static readonly (long seed, int floor)[] Floors = { (1, 1), (4, 1), (42, 2), (7, 3), (9, 7), (9, 13) };

        private static RunState Run(long seed, int floor, int steps)
        {
            var run = new RunState(HeroKind.Warrior, seed);
            for (var f = 1; f < floor; f++) run.AdvanceFloor();
            for (var s = 0; s < steps; s++)
            {
                var next = run.Map.AvailableNodes().OrderByDescending(n => n.Row).ThenBy(n => n.Id).FirstOrDefault();
                if (next == null) break;
                run.EnterRoom(next.Id);
            }
            return run;
        }

        private static int CellFor(RunMap map) => MapPlan.CellPx(map.Rows, map.Columns, MapScene.AreaW, MapScene.AreaH);
        private static MapPlan PlanFor(RunMap map) => MapPlan.Build(map, CellFor(map));

        private static (GameContext game, Recorder render) Context()
        {
            var render = new Recorder(); var art = new Art(render, new FileAssets()); art.LoadCore();
            return (new GameContext(new Canvas(render), art, new SoundPlayer(null), new Preferences(), null) { Data = new GameData(new FileAssets()), Time = 0.4f }, render);
        }

        private static bool Unexplored(MapRoom n) => !n.Visited && !n.Available;

        // ---------------------------------------------------------------- the plan

        [Test] public void ThePlanFitsUnderTheHeaderInWholeDitherTiles()
        {
            foreach (var (seed, floor) in Floors)
            {
                var map = Run(seed, floor, 0).Map; var cell = CellFor(map); var plan = PlanFor(map);
                Assert.That(cell % 8, Is.EqualTo(0), "a cell is a whole number of 4x4 dither tiles at x2");
                Assert.That(cell, Is.InRange(MapPlan.MinCell, MapPlan.MaxCell));
                Assert.That(plan.W * MapPlan.Scale, Is.LessThanOrEqualTo(MapScene.AreaW), $"floor {floor} fits across");
                Assert.That(plan.H * MapPlan.Scale, Is.LessThanOrEqualTo(MapScene.AreaH), $"floor {floor} fits under the header");
                Assert.That(plan.Rooms.Count, Is.EqualTo(map.Nodes.Count));
                foreach (var r in plan.Rooms)
                {
                    Assert.That(r.X >= 1 && r.Y >= 1 && r.X + r.W <= plan.W - 1 && r.Y + r.H <= plan.H - 1, Is.True, $"{r.Node.Id} inside the slab");
                    Assert.That(r.W - 2, Is.GreaterThanOrEqualTo(16), $"{r.Node.Id}: its 16-px icon fits in the chamber");
                }
            }
        }

        [Test] public void EveryRoomIsCarvedAndReachableFromTheStairs()
        {
            foreach (var (seed, floor) in Floors)
            {
                var plan = PlanFor(Run(seed, floor, 0).Map);
                var seen = new bool[plan.W * plan.H]; var queue = new Queue<(int x, int y)>();
                for (var x = 0; x < plan.W; x++)
                    if (plan.Kind[(plan.H - 1) * plan.W + x] == MapPlan.Stairs) { seen[(plan.H - 1) * plan.W + x] = true; queue.Enqueue((x, plan.H - 1)); }
                Assert.That(queue.Count, Is.GreaterThan(0), "stairs come up from the bottom edge");
                while (queue.Count > 0)
                {
                    var (x, y) = queue.Dequeue();
                    foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                        if (plan.Carved(nx, ny) && !seen[ny * plan.W + nx]) { seen[ny * plan.W + nx] = true; queue.Enqueue((nx, ny)); }
                }
                foreach (var r in plan.Rooms)
                {
                    var c = r.Centre.y * plan.W + r.Centre.x;
                    Assert.That(plan.Kind[c], Is.Not.EqualTo(MapPlan.Rock), $"floor {floor}: {r.Node.Id} is carved");
                    Assert.That(plan.Owner[c], Is.EqualTo(r.Index), $"floor {floor}: the middle of {r.Node.Id} is its own");
                    Assert.That(seen[c], Is.True, $"floor {floor}: {r.Node.Id} can be walked to from the stairs");
                }
            }
        }

        [Test] public void CorridorsJoinExactlyTheConnectedRooms()
        {
            foreach (var (seed, floor) in Floors)
            {
                var map = Run(seed, floor, 0).Map; var plan = PlanFor(map);
                foreach (var a in plan.Rooms)
                    foreach (var b in plan.Rooms)
                    {
                        if (a.Boss || b.Boss) continue;
                        var n = a.Node; var m = b.Node;
                        int x, y;
                        if (n.Row == m.Row && m.Column == n.Column + 1) { x = (a.X + a.W + b.X) / 2; y = a.Centre.y; }
                        else if (n.Column == m.Column && m.Row == n.Row + 1) { x = a.Centre.x; y = (b.Y + b.H + a.Y) / 2; }
                        else continue;
                        var linked = n.Connections.Contains(m.Id) || m.Connections.Contains(n.Id);
                        Assert.That(plan.Kind[y * plan.W + x] == MapPlan.Corridor, Is.EqualTo(linked), $"floor {floor}: {n.Id} - {m.Id}");
                    }
            }
        }

        [Test] public void ThePlanIsPaintedInTheCryptPaletteWithOneInkOutline()
        {
            var palette = new HashSet<Rgba>(MapArt.RockR.Concat(MapArt.WallR).Concat(MapArt.FloorR).Concat(MapArt.RugR).Concat(MapArt.GoldR)
                .Concat(new[] { MapArt.Ink, MapArt.Mortar, MapArt.Bone, MapArt.RimTop, MapArt.RimBottom }));
            foreach (var (seed, floor) in Floors)
            {
                var plan = PlanFor(Run(seed, floor, 0).Map);
                var px = MapArt.FloorPlan(plan, 1234);
                Assert.That(px.Length, Is.EqualTo(plan.W * plan.H));
                Assert.That(px.All(p => p.A == 255), Is.True, "opaque");
                Assert.That(px.Where(p => !palette.Contains(p)).Distinct().Take(3), Is.Empty, $"floor {floor}: only the crypt ramps");
                for (var y = 2; y < plan.H - 2; y++)
                    for (var x = 2; x < plan.W - 2; x++)
                        if (!plan.Carved(x, y) && (plan.Carved(x - 1, y) || plan.Carved(x + 1, y) || plan.Carved(x, y - 1) || plan.Carved(x, y + 1)))
                            Assert.That(px[y * plan.W + x], Is.EqualTo(MapArt.Ink), $"floor {floor}: the rock around ({x},{y}) is outlined in ink");
                Assert.That(MapArt.FloorPlan(plan, 1234), Is.EqualTo(px), "the same seed paints the same plan");
            }
        }

        [Test] public void TheSignatureFollowsTheLayout()
        {
            Assert.That(MapPlan.Signature(Run(4, 1, 0).Map), Is.EqualTo(MapPlan.Signature(Run(4, 1, 3).Map)), "visiting rooms does not change the plan");
            Assert.That(MapPlan.Signature(Run(4, 1, 0).Map), Is.Not.EqualTo(MapPlan.Signature(Run(4, 2, 0).Map)));
            Assert.That(MapPlan.Signature(Run(4, 1, 0).Map), Is.Not.EqualTo(MapPlan.Signature(Run(5, 1, 0).Map)));
        }

        // ---------------------------------------------------------------- what you know of it

        [Test] public void FogCoversTheRoomsOutOfReachAndRunesTheOnesYouCanEnter()
        {
            var cyan = new HashSet<Rgba>(MapArt.CyanR);
            foreach (var (seed, floor) in Floors)
                foreach (var steps in new[] { 0, 3 })
                {
                    var map = Run(seed, floor, steps).Map; var plan = PlanFor(map);
                    var basePx = MapArt.FloorPlan(plan, 99); var state = MapArt.StateLayer(plan, basePx);
                    var rings = new int[plan.Rooms.Count];
                    for (var i = 0; i < state.Length; i++)
                    {
                        var o = plan.Owner[i]; var p = state[i];
                        if (o < 0) { Assert.That(p.A, Is.EqualTo(0), "nothing over the rock"); continue; }
                        var n = plan.Rooms[o].Node;
                        if (n.Visited) Assert.That(p.A, Is.EqualTo(0), $"{n.Id} was visited: clear");
                        else if (n.Available) { if (p.A > 0) { Assert.That(cyan.Contains(p), Is.True, $"{n.Id}: only the cyan runes"); rings[o]++; } }
                        else
                        {
                            var c = basePx[i];
                            Assert.That(p.A, Is.EqualTo(255), $"{n.Id} is out of reach: fogged");
                            Assert.That(p.R + p.G + p.B, Is.LessThan(c.R + c.G + c.B + 1).And.GreaterThan(0), $"{n.Id}: darker, but still there");
                        }
                    }
                    foreach (var r in plan.Rooms)
                        if (r.Node.Available && !r.Node.Visited) Assert.That(rings[r.Index], Is.GreaterThan(20), $"floor {floor}: a rune circle in {r.Node.Id}");
                    Assert.That(plan.Rooms.Any(r => Unexplored(r.Node)), Is.True, "some of the floor is still in the dark");
                }
        }

        [Test] public void TheRuneRingIsCyanCentredAndInsideTheFloor()
        {
            foreach (var (w, h) in new[] { (16, 13), (22, 19), (50, 30) })
            {
                var px = MapArt.RuneRing(w, h);
                var lit = Enumerable.Range(0, w * h).Where(i => px[i].A > 0).ToList();
                Assert.That(lit.Count, Is.GreaterThan(12));
                Assert.That(lit.All(i => MapArt.CyanR.Contains(px[i])), Is.True);
                int minX = lit.Min(i => i % w), maxX = lit.Max(i => i % w), minY = lit.Min(i => i / w), maxY = lit.Max(i => i / w);
                Assert.That(minX > 0 && minY > 0 && maxX < w - 1 && maxY < h - 1, Is.True, $"{w}x{h}: clear of the walls");
                Assert.That(Math.Abs(minX + maxX - (w - 1)), Is.LessThanOrEqualTo(2), "centred across");
                Assert.That(maxX - minX, Is.GreaterThan(maxY - minY), "squashed by the view from above");
            }
        }

        // ---------------------------------------------------------------- icons

        [Test] public void EveryRoomKindHasAnIconAndAGlow()
        {
            var (game, render) = Context();
            var icons = MapIcons.Load(game.Art);
            var (sw, sh) = (icons.Sheet.Width, icons.Sheet.Height);
            Assert.That((icons.Halo.Width, icons.Halo.Height), Is.EqualTo((sw, sh)), "the halo lines up with the sheet");
            foreach (var kind in (RoomKind[])Enum.GetValues(typeof(RoomKind)))
                Assert.That(icons.Has(kind == RoomKind.Boss ? MapIcons.Boss : kind.ToString()), Is.True, kind.ToString());
            Assert.That(icons.Has(MapIcons.Marker), Is.True);
            foreach (var name in icons.Names)
            {
                var s = icons.Icon(name);
                Assert.That(s.Valid && s.X >= 0 && s.Y >= 0 && s.X + s.W <= sw && s.Y + s.H <= sh, Is.True, name + " inside the sheet");
                Assert.That(s.W, Is.InRange(8, 20)); Assert.That(s.H, Is.InRange(8, 20));
                var g = icons.Glow(name); Assert.That(g.R + g.G + g.B, Is.GreaterThan(200), name + " glows");
            }
            Assert.That(MapScene.Labels.Keys.Count, Is.EqualTo(Enum.GetValues(typeof(RoomKind)).Length), "a Spanish name for every room");
            Assert.That(MapScene.Descriptions.Keys.Count, Is.EqualTo(Enum.GetValues(typeof(RoomKind)).Length));
        }

        // ---------------------------------------------------------------- the scene

        [Test] public void TheSceneDrawsThePlanTheFogTheRunesAndTheIcons()
        {
            foreach (var (seed, floor) in Floors)
            {
                var (game, render) = Context();
                var run = Run(seed, floor, 3);
                var scene = new MapScene(game, run);
                scene.Draw(game.Canvas);
                var plan = scene.Plan; var o = scene.Origin;
                var drawn = render.With(k => k.EndsWith("_plan")).Single();
                Assert.That((drawn.x0, drawn.y0, drawn.x1, drawn.y1), Is.EqualTo((o.x, o.y, o.x + plan.W * 2, o.y + plan.H * 2)), "the plan at x2");
                Assert.That(o.x, Is.EqualTo(640 - plan.W), "centred");
                Assert.That(o.y, Is.GreaterThanOrEqualTo(MapScene.AreaTop));
                var rooms = plan.Rooms.Select(r => r.Node).ToList();
                Assert.That(render.With(k => k.Contains("_fog")).Count(), Is.EqualTo(rooms.Count(Unexplored)), "fog over the rooms out of reach");
                Assert.That(render.With(k => k.StartsWith("map_ring_")).Count(), Is.EqualTo(rooms.Count(n => n.Available && !n.Visited)), "runes where you can go");
                var legend = scene.LegendKinds();
                Assert.That(legend, Is.Unique.And.Not.Empty);
                Assert.That(render.With(k => k == MapIcons.SheetKey).Count(), Is.EqualTo(rooms.Count + legend.Count + (run.CurrentRoomId != null ? 1 : 0)), "an icon per room, per legend row and the marker");
                Assert.That(render.With(k => k == MapIcons.HaloKey).All(q => q.blend == Blend.Add), Is.True);
                Assert.That(render.With(k => k == MapIcons.HaloKey).Count(), Is.EqualTo(rooms.Count(n => n.Available && !n.Visited)), "the rooms you can enter glow");
                var visited = rooms.Count(n => n.Visited);
                Assert.That(render.Quads.Count(q => q.tex == "white" && q.color.Equals(new Rgba(255, 230, 150))), Is.EqualTo(visited * 2), "two torches lit per room you went through");
            }
        }

        [Test] public void OnlyTheRoomsYouCanReachTakeAClick()
        {
            var (game, _) = Context();
            var run = Run(4, 1, 2);
            foreach (var node in run.Map.Nodes)
            {
                var scene = new MapScene(game, run);
                scene.Draw(game.Canvas);
                var rect = scene.RoomRect(node.Id);
                Assert.That(rect.HasValue, Is.True, node.Id);
                scene.HandleEvent(new InputEvent(EventType.MouseMove, rect.Value.CenterX, rect.Value.CenterY));
                scene.Draw(game.Canvas);   // with its tooltip
                scene.HandleEvent(new InputEvent(EventType.MouseDown, rect.Value.CenterX, rect.Value.CenterY, 1));
                Assert.That(scene.SelectedNode, Is.EqualTo(node.Available && !node.Visited ? node : null), node.Id);
            }
            var empty = new MapScene(game, run);
            empty.Draw(game.Canvas);
            empty.HandleEvent(new InputEvent(EventType.MouseDown, 8, 700, 1));
            Assert.That(empty.SelectedNode, Is.Null, "the rock takes no click");
        }

        [Test] public void ThePlanIsPaintedOncePerFloor()
        {
            var (game, render) = Context();
            var run = Run(7, 3, 2);
            var scene = new MapScene(game, run);
            scene.Draw(game.Canvas);
            var made = render.Created.Keys.Count(k => k.StartsWith("map_"));
            Assert.That(render.Created.Keys.Count(k => k.EndsWith("_plan")), Is.EqualTo(1));
            Assert.That(render.Created.Keys.Count(k => k.Contains("_fog")), Is.EqualTo(scene.Plan.Rooms.Count), "one fog piece per room");
            scene.Draw(game.Canvas);
            run.EnterRoom(run.Map.AvailableNodes().First().Id);
            scene.Draw(game.Canvas);
            new MapScene(game, run).Draw(game.Canvas);
            Assert.That(render.Created.Keys.Count(k => k.StartsWith("map_")), Is.EqualTo(made), "moving on and coming back reuse the textures");
            run.AdvanceFloor();
            scene.Draw(game.Canvas);
            Assert.That(render.Created.Keys.Count(k => k.EndsWith("_plan")), Is.EqualTo(2), "a new floor, a new plan");
        }
    }
}
