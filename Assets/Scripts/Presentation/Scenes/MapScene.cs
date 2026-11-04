using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>
    /// The floor map as the plan of the crypt seen from above (docs/art-style.md "Mapa"): the entrance comes up the stairs at
    /// the bottom, the boss waits in its hall at the top. The plan (<see cref="MapPlan"/>, <see cref="MapArt"/>) is one
    /// cached texture per floor; over it go, per room, its fog (rooms you cannot reach yet) or its rune circle (rooms you can
    /// enter), then the lights (a cyan pulse where you can go, the crimson hall, the torches of the rooms you went through),
    /// the room icons (<see cref="MapIcons"/>), the marker over the room you are in, and the legend. Open stories (a choice
    /// that "continuará") get a panel left of the plan and a scroll over every event room where their next chapter can appear.
    /// </summary>
    public sealed class MapScene : Scene
    {
        public static readonly Dictionary<RoomKind, string> Labels = new Dictionary<RoomKind, string>
        {
            [RoomKind.Combat] = "Combate", [RoomKind.Elite] = "Élite", [RoomKind.Boss] = "Jefe", [RoomKind.Treasure] = "Tesoro",
            [RoomKind.Shop] = "Tienda", [RoomKind.Event] = "Evento", [RoomKind.Warlock] = "Brujo", [RoomKind.Gacha] = "Gachapón",
            [RoomKind.Purge] = "Purga",
        };
        public static readonly Dictionary<RoomKind, string> Descriptions = new Dictionary<RoomKind, string>
        {
            [RoomKind.Combat] = "Enemigos. Si ganas: oro y una carta a elegir.",
            [RoomKind.Elite] = "Enemigos más duros. Si ganas: oro, una carta y una reliquia.",
            [RoomKind.Boss] = "El guardián del piso. Derrótalo para pasar al siguiente.",
            [RoomKind.Treasure] = "Un cofre: elige una de sus reliquias.",
            [RoomKind.Shop] = "Reliquias y sobres a cambio de oro.",
            [RoomKind.Event] = "Encuentros, tratos y sacrificios. Tus decisiones pueden abrir nuevas historias.",
            [RoomKind.Warlock] = "El Brujo mejora tus cartas a cambio de oro.",
            [RoomKind.Gacha] = "Paga, gira la manivela y abre una cápsula con una reliquia.",
            [RoomKind.Purge] = "Un altar donde quemar una carta de tu mazo.",
        };
        /// <summary>The order of the legend (only the kinds on this floor are listed).</summary>
        public static readonly RoomKind[] LegendOrder =
            { RoomKind.Combat, RoomKind.Elite, RoomKind.Event, RoomKind.Treasure, RoomKind.Shop, RoomKind.Warlock, RoomKind.Gacha, RoomKind.Purge, RoomKind.Boss };

        private static readonly Rgba Bg = new Rgba(8, 6, 14), BackLight = new Rgba(24, 14, 40), ReachLight = new Rgba(30, 150, 200),
            BossLight = new Rgba(120, 20, 20), TorchLight = new Rgba(200, 110, 40), MarkerLight = new Rgba(40, 140, 190),
            LegendTitle = new Rgba(252, 214, 112), LegendText = new Rgba(232, 220, 196),
            FoggedIcon = new Rgba(97, 97, 97), VisitedIcon = new Rgba(146, 144, 152);
        private static readonly Rgba[] Flame = { new Rgba(255, 230, 150), new Rgba(255, 160, 60), new Rgba(200, 70, 30) };
        private static readonly Rgba StoryInk = new Rgba(252, 214, 112), StoryDim = new Rgba(168, 158, 180), StoryLight = new Rgba(210, 150, 40);
        /// <summary>The scroll the event screen uses for the story ("La historia continuará").</summary>
        public const string StoryIcon = "spoil";
        /// <summary>The area under the header the plan is fitted in (screen px), and the kit frame around it.</summary>
        public const int AreaTop = 76, AreaW = 680, AreaH = 614, FramePad = 12;
        public const int LegendW = 236, LegendRow = 38, LegendRight = 36;
        private const int Px = MapPlan.Scale;

        private readonly RunState run;
        private readonly Dictionary<string, IRect> nodeRects = new Dictionary<string, IRect>();
        private readonly List<string> nodeOrder = new List<string>();
        private string hoveredId;
        private (float x, float y) mouse = (0, 0);
        private readonly IRect relicRect = new IRect(24, 16, 194, 36), deckRect = new IRect(1062, 16, 194, 36);
        private IOverlay overlay;
        // the plan of the floor on screen and its textures
        private RunMap planMap;
        private Tex planTex;
        private Tex[] fogTex, ringTex;
        private MapIcons icons;
        private bool iconsTried;

        public MapRoom SelectedNode { get; set; }
        public override (Anchor anchor, float x, float y)? GoldHudPos => (Anchor.TopRight, 1048, 11);
        public override bool HasOverlay => overlay != null;
        /// <summary>The plan of the floor on screen (null before the first frame or without a map).</summary>
        public MapPlan Plan { get; private set; }
        /// <summary>Screen position of the plan's top-left art pixel.</summary>
        public (float x, float y) Origin { get; private set; }

        public MapScene(GameContext game, RunState run) : base(game) { this.run = run; }

        /// <summary>Screen rect of a room's chamber (its hit area), or null if it is not on the plan.</summary>
        public IRect? RoomRect(string id)
        {
            EnsurePlan();
            return id != null && nodeRects.TryGetValue(id, out var r) ? r : (IRect?)null;
        }

        public override void HandleEvent(in InputEvent e)
        {
            if (overlay != null) { overlay.HandleEvent(e); return; }
            if (e.Type == EventType.MouseMove) { mouse = e.Pos; UpdateHover(e.X, e.Y); }
            else if (e.IsLeftDown) Click(e.X, e.Y);
        }

        public override void Update(float dt)
        {
            if (overlay != null && overlay.Dismissed) { overlay = null; Sound.PlayCancel(); }
            overlay?.Update(dt);
        }

        public override void Draw(Canvas canvas)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), Bg);
            storyKnown = false; storyNow = run.NextStory; storyKnown = true;
            EnsurePlan();
            if (Plan != null) DrawFloor(canvas);
            DrawHeader(canvas);
            if (Plan != null) { DrawLegend(canvas); DrawStories(canvas); DrawHoverTip(canvas); }
            Fonts.Get(11).DrawCentered(canvas, "Haz clic en una sala iluminada para avanzar", 640, 710, Colors.TextSecondary);
            overlay?.Draw(canvas);
        }

        // ---------------------------------------------------------------- the plan and its textures

        private void EnsurePlan()
        {
            var gm = run.Map;
            if (gm == null) { Plan = null; planMap = null; nodeRects.Clear(); nodeOrder.Clear(); return; }
            if (ReferenceEquals(gm, planMap) && Plan != null) return;
            planMap = gm;
            var cell = MapPlan.CellPx(gm.Rows, gm.Columns, AreaW, AreaH);
            var plan = MapPlan.Build(gm, cell);
            var sig = MapPlan.Signature(gm);
            var key = "map_" + sig.ToString("x8") + "_" + cell;
            Rgba[] pixels = null;
            Rgba[] Pixels() => pixels ?? (pixels = MapArt.FloorPlan(plan, unchecked((int)sig)));
            planTex = Art.Procedural(key + "_plan", plan.W, plan.H, Pixels);
            fogTex = new Tex[plan.Rooms.Count]; ringTex = new Tex[plan.Rooms.Count];
            foreach (var r in plan.Rooms)
            {
                var b = plan.Bounds[r.Index]; var index = r.Index;
                fogTex[index] = Art.Procedural(key + "_fog" + index, b.w, b.h, () => MapArt.Fog(plan, Pixels(), index));
                var f = MapArt.FloorArea(r);
                ringTex[index] = Art.Procedural("map_ring_" + f.w + "x" + f.h, f.w, f.h, () => MapArt.RuneRing(f.w, f.h));
            }
            Plan = plan;
            Origin = (Canvas.Width / 2 - plan.W * Px / 2, AreaTop + (AreaH - plan.H * Px) / 2);
            nodeRects.Clear(); nodeOrder.Clear();
            foreach (var r in plan.Rooms)
            {
                nodeRects[r.Node.Id] = new IRect((int)Origin.x + r.X * Px, (int)Origin.y + r.Y * Px, r.W * Px, r.H * Px);
                nodeOrder.Add(r.Node.Id);
            }
        }

        private MapIcons Icons()
        {
            if (iconsTried) return icons;
            iconsTried = true;
            try { icons = MapIcons.Load(Art); }
            catch (Exception) { icons = null; }   // without the atlas the rooms show their names instead
            return icons;
        }

        private RectF ArtRect(float x, float y, float w, float h) => new RectF(Origin.x + x * Px, Origin.y + y * Px, w * Px, h * Px);

        /// <summary>The middle of a room's floor on screen.</summary>
        private (float x, float y) CentreOf(MapPlan.Room r) => (Origin.x + r.Centre.x * Px + 1, Origin.y + r.Centre.y * Px + 1);

        /// <summary>A soft additive light, an ellipse of radii (<paramref name="rx"/>, <paramref name="ry"/>) screen px.</summary>
        private void Light(Canvas canvas, float cx, float cy, float rx, float ry, Rgba c, float k)
        {
            if (!(k > 0.01f)) return;
            canvas.BlitRect(Glows.Soft(Art), new RectF(cx - rx, cy - ry, rx * 2, ry * 2), new Rgba((int)(c.R * k), (int)(c.G * k), (int)(c.B * k)), Blend.Add);
        }

        private static bool Reachable(MapRoom n) => n.Available && !n.Visited;

        // ---------------------------------------------------------------- drawing

        private void DrawFloor(Canvas canvas)
        {
            var plan = Plan; var t = Game.Time;
            var pulse = 0.5f + 0.5f * (float)Math.Sin(t * Math.PI);
            float pw = plan.W * Px, ph = plan.H * Px;
            Light(canvas, Origin.x + pw / 2, Origin.y + ph / 2, pw * 0.8f, ph * 0.65f, BackLight, 1f);
            PixelUi.Panel(canvas, Art, new RectF(Origin.x - FramePad, Origin.y - FramePad, pw + 2 * FramePad, ph + 2 * FramePad));
            canvas.BlitRect(new Sprite(planTex), new RectF(Origin.x, Origin.y, pw, ph), Rgba.White);

            // what you know of the floor: fog over the rooms out of reach, a rune circle where you can go
            foreach (var r in plan.Rooms)
            {
                var n = r.Node;
                if (n.Visited) continue;
                if (n.Available)
                {
                    var f = MapArt.FloorArea(r);
                    canvas.BlitRect(new Sprite(ringTex[r.Index]), ArtRect(f.x, f.y, f.w, f.h), Rgba.White.WithAlpha(150 + (int)(105 * pulse)));
                }
                else
                {
                    var b = plan.Bounds[r.Index];
                    canvas.BlitRect(new Sprite(fogTex[r.Index]), ArtRect(b.x, b.y, b.w, b.h), Rgba.White);
                }
            }

            // lights
            foreach (var r in plan.Rooms)
            {
                var n = r.Node; var (cx, cy) = CentreOf(r);
                if (r.Boss) Light(canvas, cx, cy, r.W * 1.6f, r.H * 1.4f, BossLight, 0.55f + 0.25f * pulse);
                if (Reachable(n)) Light(canvas, cx, cy, r.W * 1.4f, r.H * 1.2f, ReachLight, 0.22f + 0.16f * pulse + (hoveredId == n.Id ? 0.3f : 0f));
                if (n.Visited) foreach (var torch in r.Torches) DrawTorch(canvas, torch.x, torch.y, t);
            }

            // the icons, dimmed in the fog and in the rooms you already went through
            var atlas = Icons();
            foreach (var r in plan.Rooms)
            {
                var n = r.Node; var (cx, cy) = CentreOf(r);
                var name = MapIcons.NameOf(n, r.Boss);
                var lift = hoveredId == n.Id && Reachable(n) ? 2 : 0;
                var tint = n.Visited ? VisitedIcon : n.Available ? Rgba.White : FoggedIcon;
                var icon = atlas?.Icon(name) ?? default;
                if (!icon.Valid)
                {
                    var label = Labels.TryGetValue(n.Kind, out var l) ? l : "?";
                    Txt.OutlinedAt(canvas, Fonts.Get(Math.Max(8, Math.Min(11, r.W * Px / 6))), label, Anchor.Center, cx, cy - lift, tint);
                    continue;
                }
                var dst = new RectF(cx - icon.W, cy - icon.H - lift, icon.W * Px, icon.H * Px);
                canvas.BlitRect(icon, dst, tint);
                if (Reachable(n))
                {
                    var k = (int)(255 * (0.55f + 0.45f * pulse));
                    canvas.BlitRect(atlas.HaloOf(name), dst, new Rgba(k, k, k), Blend.Add);
                }
            }

            // open stories: a scroll over every event room where the next chapter can appear
            if (NextStory() != null)
                foreach (var r in plan.Rooms)
                    if (ShowsStory(r.Node)) DrawStoryMarker(canvas, r, atlas, pulse, t);

            // you are here
            if (run.CurrentRoomId != null && plan.ById.TryGetValue(run.CurrentRoomId, out var cur))
            {
                var bob = (float)Math.Round(2 * Math.Sin(t * Math.PI));
                var marker = atlas?.Icon(MapIcons.Marker) ?? default;
                float mx = Origin.x + (cur.X + cur.W) * Px - 2, my = Origin.y + cur.Y * Px + 4 + bob;
                Light(canvas, mx, my, 18, 18, MarkerLight, 0.5f + 0.2f * pulse);
                if (marker.Valid) canvas.BlitRect(marker, new RectF(mx - marker.W, my - marker.H, marker.W * Px, marker.H * Px), Rgba.White);
                else canvas.Fill(new RectF(mx - 4, my - 6, 8, 8), MapArt.CyanR[2]);
            }
        }

        /// <summary>An event room the next chapter of an open story can appear in (any unvisited one: the first you enter).</summary>
        public bool ShowsStory(MapRoom n) => n != null && n.Kind == RoomKind.Event && !n.Visited && NextStory() != null;

        /// <summary>The run's next story, read once per frame (the map asks for it per room).</summary>
        private PendingStory NextStory() => storyKnown ? storyNow : run.NextStory;
        private PendingStory storyNow;
        private bool storyKnown;

        /// <summary>Screen centre of a room's story scroll: on the top-right corner of its icon.</summary>
        public (float x, float y) StoryMarkerAt(MapPlan.Room r)
        {
            var (cx, cy) = CentreOf(r);
            var icon = Icons()?.Icon(MapIcons.NameOf(r.Node, r.Boss)) ?? default;
            var lift = hoveredId == r.Node.Id && Reachable(r.Node) ? 2 : 0;
            return icon.Valid ? (cx + icon.W, cy - icon.H - lift) : (cx + 14, cy - 14);
        }

        private void DrawStoryMarker(Canvas canvas, MapPlan.Room r, MapIcons atlas, float pulse, float t)
        {
            var (x, y) = StoryMarkerAt(r);
            var reach = Reachable(r.Node);
            if (reach) y += (float)Math.Round(Math.Sin(t * Math.PI * 1.5));
            Light(canvas, x, y, 22, 20, StoryLight, reach ? 0.45f + 0.3f * pulse : r.Node.Available ? 0.3f : 0.12f);
            var scroll = Art.Icon(StoryIcon);
            var tint = r.Node.Available ? Rgba.White : FoggedIcon;
            if (scroll.Valid) canvas.BlitRect(scroll, new RectF(x - scroll.W, y - scroll.H, scroll.W * 2, scroll.H * 2), tint);
            else canvas.Fill(new RectF(x - 4, y - 4, 8, 8), StoryInk);
        }

        /// <summary>A lit sconce: a two-pixel flame that flickers over the unlit one in the plan, and its warm light.</summary>
        private void DrawTorch(Canvas canvas, int tx, int ty, float t)
        {
            float fx = Origin.x + tx * Px, fy = Origin.y + ty * Px;
            var flicker = MapArt.Hash((int)(t * 9), tx, ty);
            Light(canvas, fx + 1, fy - 1, 26, 22, TorchLight, 0.62f + 0.18f * flicker);
            canvas.Fill(new RectF(fx, fy - 2, Px, Px), Flame[0]);
            canvas.Fill(new RectF(fx, fy - 4, Px, Px), Flame[1]);
            if (flicker > 0.3f) canvas.Fill(new RectF(fx + (flicker > 0.85f ? Px : flicker < 0.4f ? -Px : 0), fy - 6, Px, Px), Flame[2]);
        }

        private void DrawHeader(Canvas canvas)
        {
            const int cx = 640;
            PixelUi.Topbar(canvas, Art, 68);
            PixelUi.Ribbon(canvas, Art, cx, 24, "PISO " + run.Floor, 18);
            var bar = new IRect(cx - 70, 44, 140, 14);
            var ratio = run.MaxHp > 0 ? run.Hp / (double)run.MaxHp : 0;
            canvas.Rect(bar, new Rgba(50, 14, 18), 0, 3);
            canvas.Rect(new RectF(bar.X, bar.Y, (int)(bar.W * ratio), bar.H), new Rgba(200, 46, 52), 0, 3);
            canvas.Rect(bar, new Rgba(14, 8, 12), 1, 3);
            var heart = Art.Icon("heal");
            if (heart.Valid) canvas.BlitRect(heart, RectF.CenteredInt(bar.X - 2, bar.CenterY, heart.W, heart.H), Rgba.White);
            Txt.OutlinedAt(canvas, Fonts.Get(11), run.Hp + "/" + run.MaxHp, Anchor.Center, bar.CenterX, bar.CenterY, Colors.Cream);
            PixelUi.Button(canvas, Art, relicRect, "Reliquias (" + run.OwnedRelics.Count + ")", icon: "bag", state: PixelUi.StateOf(relicRect, mouse, false), size: 15, t: Game.Time);
            PixelUi.Button(canvas, Art, deckRect, "Mazo (" + run.Deck.Count + ")", icon: "deck", state: PixelUi.StateOf(deckRect, mouse, false), size: 15, t: Game.Time);
        }

        /// <summary>The kinds of room on this floor, in <see cref="LegendOrder"/>.</summary>
        public List<RoomKind> LegendKinds()
        {
            var kinds = new List<RoomKind>();
            if (Plan == null) return kinds;
            foreach (var k in LegendOrder)
                foreach (var r in Plan.Rooms)
                    if ((r.Boss ? RoomKind.Boss : r.Node.Kind) == k) { kinds.Add(k); break; }
            return kinds;
        }

        private void DrawLegend(Canvas canvas)
        {
            var kinds = LegendKinds();
            if (kinds.Count == 0) return;
            var atlas = Icons();
            var x = Canvas.Width - LegendRight - LegendW; var y = (int)Origin.y;
            PixelUi.Panel(canvas, Art, new RectF(x, y, LegendW, 56 + LegendRow * kinds.Count));
            Txt.OutlinedAt(canvas, Fonts.Get(16), "Salas", Anchor.TopLeft, x + 20, y + 14, LegendTitle);
            for (var i = 0; i < kinds.Count; i++)
            {
                var yy = y + 46 + LegendRow * i;
                var icon = atlas?.Icon(kinds[i] == RoomKind.Boss ? MapIcons.Boss : kinds[i].ToString()) ?? default;
                if (icon.Valid) canvas.BlitRect(icon, new RectF(x + 22 + (20 - icon.W), yy + (20 - icon.H) - 2, icon.W * Px, icon.H * Px), Rgba.White);
                Txt.OutlinedAt(canvas, Fonts.Get(16), Labels[kinds[i]], Anchor.MidLeft, x + 70, yy + 17, LegendText);
            }
        }

        /// <summary>One open story in the panel: its rect and the lines it shows.</summary>
        public sealed class StoryRow
        {
            public PendingStory Story; public IRect Rect; public List<string> Name = new List<string>(); public string Chapter = "";
            public List<string> Where = new List<string>(); public bool Next;
        }

        public const int StoryTextX = 52, StoryLine = 18, StorySmallLine = 12, StoryGap = 12;

        /// <summary>
        /// The panel of open stories left of the plan (mirror of the legend), one row per story in the order they come; rows
        /// that would leave the plan area are summed up as "… y N más". Empty without open stories.
        /// </summary>
        public const int StoryTextW = LegendW - StoryTextX - 10;

        /// <summary>Where a story's next chapter waits: the next event room, after the one before it, or a later floor.</summary>
        public static string WhereText(PendingStory s, bool next) =>
            !s.Ready ? "Desde el piso " + s.MinFloor : next ? "En el próximo evento" : "Tras la historia anterior";

        public List<StoryRow> StoryRows(out IRect panel, out int hidden)
        {
            var rows = new List<StoryRow>(); hidden = 0;
            var stories = run.PendingStories;
            panel = default;
            if (stories.Count == 0 || Plan == null) return rows;
            int x = LegendRight, top = (int)Origin.y, inner = StoryTextW;
            var bottom = AreaTop + AreaH - 4;
            var name = Fonts.Get(16); var small = Fonts.Get(11);
            var next = run.NextStory;
            var y = top + 46;
            for (var i = 0; i < stories.Count; i++)
            {
                var s = stories[i];
                var row = new StoryRow { Story = s, Next = next != null && s.EventId == next.EventId };
                row.Name = TextFit.WrapLimited(name, s.StoryName, inner, 2);
                row.Chapter = "Capítulo " + s.Chapter + " de " + s.Chapters;
                row.Where = TextFit.WrapLimited(small, WhereText(s, row.Next), inner, 2);
                var h = row.Name.Count * StoryLine + StorySmallLine * (1 + row.Where.Count) + 4;
                if (y + h + 10 > bottom - (i < stories.Count - 1 ? StoryLine : 0)) { hidden = stories.Count - i; break; }
                row.Rect = new IRect(x + 8, y - 4, LegendW - 16, h + 6);
                rows.Add(row);
                y += h + StoryGap;
            }
            if (hidden > 0) y += StoryLine;
            panel = new IRect(x, top, LegendW, y - top + 4);
            return rows;
        }

        private void DrawStories(Canvas canvas)
        {
            var rows = StoryRows(out var panel, out var hidden);
            if (rows.Count == 0 && hidden == 0) return;
            var t = Game.Time; var pulse = 0.5f + 0.5f * (float)Math.Sin(t * Math.PI);
            PixelUi.Panel(canvas, Art, new RectF(panel.X, panel.Y, panel.W, panel.H));
            Txt.OutlinedAt(canvas, Fonts.Get(16), "Historias", Anchor.TopLeft, panel.X + 20, panel.Y + 14, LegendTitle);
            var scroll = Art.Icon(StoryIcon);
            var name = Fonts.Get(16); var small = Fonts.Get(11);
            foreach (var row in rows)
            {
                var ready = row.Story.Ready;
                int ix = panel.X + 32, iy = row.Rect.Y + 16;
                if (row.Next)
                {
                    // the chapter the next event room opens: a warm plate and a breathing light behind its scroll
                    PixelUi.PixelBox(canvas, new RectF(row.Rect.X, row.Rect.Y, row.Rect.W, row.Rect.H), new Rgba(210, 150, 40, 26), new Rgba(252, 214, 112, 90));
                    Light(canvas, ix, iy, 26, 22, StoryLight, 0.35f + 0.25f * pulse);
                }
                if (scroll.Valid) canvas.BlitRect(scroll, new RectF(ix - scroll.W, iy - scroll.H, scroll.W * 2, scroll.H * 2), ready ? Rgba.White : FoggedIcon);
                var x = panel.X + StoryTextX; var y = row.Rect.Y + 4;
                foreach (var line in row.Name) { Txt.OutlinedAt(canvas, name, line, Anchor.TopLeft, x, y, LegendText); y += StoryLine; }
                Txt.OutlinedAt(canvas, small, row.Chapter, Anchor.TopLeft, x, y + 1, StoryDim); y += StorySmallLine;
                foreach (var line in row.Where) { Txt.OutlinedAt(canvas, small, line, Anchor.TopLeft, x, y + 1, ready ? StoryInk : StoryDim); y += StorySmallLine; }
            }
            if (hidden > 0)
                Txt.OutlinedAt(canvas, small, "… y " + hidden + (hidden == 1 ? " historia más" : " historias más"), Anchor.BottomLeft, panel.X + StoryTextX, panel.Bottom - 10, StoryDim);
        }

        private void DrawHoverTip(Canvas canvas)
        {
            if (hoveredId == null || overlay != null) return;
            var node = run.Map?.Find(hoveredId);
            if (node == null) return;
            var boss = Plan != null && Plan.ById.TryGetValue(node.Id, out var room) && room.Boss;
            var kind = boss ? RoomKind.Boss : node.Kind;
            var content = new TooltipContent(Labels.TryGetValue(kind, out var l) ? l : "?") { Accent = Icons()?.Glow(MapIcons.NameOf(node, boss)) };
            if (Descriptions.TryGetValue(kind, out var d)) content.Lines.Add(d);
            if (ShowsStory(node))
            {
                var story = NextStory();
                content.Lines.Add("[[" + StoryIcon + "]]Aquí sigue tu historia: " + story.StoryName + " (capítulo " + story.Chapter + " de " + story.Chapters + ").");
            }
            content.Lines.Add(node.Visited ? "Ya pasaste por aquí." : node.Available ? "Haz clic para entrar." : "Aún no puedes llegar.");
            Tooltip.Draw(canvas, Game, content, mouse);
        }

        // ---------------------------------------------------------------- input

        private void UpdateHover(float x, float y)
        {
            EnsurePlan();
            var previous = hoveredId; hoveredId = null;
            var gm = run.Map; if (gm == null) return;
            foreach (var id in nodeOrder)
            {
                if (!nodeRects[id].Contains(x, y)) continue;
                hoveredId = id;
                var node = gm.Find(id);
                if (id != previous && node != null && Reachable(node)) Sound.PlayNav();
                return;
            }
        }

        private void Click(float x, float y)
        {
            if (SelectedNode != null) return;
            if (relicRect.Contains(x, y)) { overlay = new RelicViewer(Game, run.OwnedRelics); Sound.PlayConfirm(); return; }
            if (deckRect.Contains(x, y)) { overlay = new PileViewer(Game, "Tu mazo", run.Deck, CardBonus.ForRun(run)); Sound.PlayCard(); return; }
            EnsurePlan();
            var gm = run.Map; if (gm == null) return;
            foreach (var id in nodeOrder)
            {
                if (!nodeRects[id].Contains(x, y)) continue;
                var node = gm.Find(id);
                if (node != null && Reachable(node)) { Sound.PlayTravel(); SelectedNode = node; }
                return;
            }
        }
    }
}
