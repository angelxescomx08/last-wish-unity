using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>scenes/map_scene.py: crossword-style floor map (start at the bottom, boss at the top).</summary>
    public sealed class MapScene : Scene
    {
        private static readonly Rgba Bg = new Rgba(8, 10, 14), GridEmpty = new Rgba(18, 22, 28), GridBorder = new Rgba(30, 36, 46),
            CorridorIdle = new Rgba(28, 34, 42), CorridorLive = new Rgba(60, 74, 90), FillVisited = new Rgba(30, 40, 30),
            BorderAvail = new Rgba(255, 230, 80), BorderHover = new Rgba(255, 255, 160), BorderLocked = new Rgba(36, 44, 54), BorderVisit = new Rgba(55, 80, 55),
            LabelAvail = new Rgba(235, 235, 235), LabelLocked = new Rgba(55, 58, 68), LabelVisit = new Rgba(65, 95, 65);
        public static readonly Dictionary<RoomKind, Rgba> FillAvail = new Dictionary<RoomKind, Rgba>
        {
            [RoomKind.Combat] = new Rgba(155, 45, 45), [RoomKind.Treasure] = new Rgba(185, 145, 25), [RoomKind.Shop] = new Rgba(35, 110, 185),
            [RoomKind.Event] = new Rgba(110, 50, 170), [RoomKind.Boss] = new Rgba(200, 25, 25), [RoomKind.Warlock] = new Rgba(30, 140, 110),
            [RoomKind.Gacha] = new Rgba(205, 70, 150), [RoomKind.Purge] = new Rgba(215, 105, 35), [RoomKind.Elite] = new Rgba(170, 28, 70),
        };
        public static readonly Dictionary<RoomKind, string> Labels = new Dictionary<RoomKind, string>
        {
            [RoomKind.Combat] = "Combate", [RoomKind.Treasure] = "Tesoro", [RoomKind.Shop] = "Tienda", [RoomKind.Event] = "Evento", [RoomKind.Boss] = "JEFE",
            [RoomKind.Warlock] = "Brujo", [RoomKind.Gacha] = "Gacha", [RoomKind.Purge] = "Purga", [RoomKind.Elite] = "ÉLITE",
        };
        private static Rgba Locked(Rgba c) => new Rgba(Math.Max(c.R - 130, 14), Math.Max(c.G - 130, 14), Math.Max(c.B - 130, 14));
        private const int HeaderH = 72, FooterH = 44, PadX = 90, MaxCell = 82, MinCell = 36;

        private readonly RunState run;
        private readonly Dictionary<string, IRect> nodeRects = new Dictionary<string, IRect>();
        private readonly List<string> nodeOrder = new List<string>();
        private string hoveredId;
        private (float x, float y) mouse = (0, 0);
        private readonly IRect relicRect = new IRect(24, 16, 194, 36), deckRect = new IRect(1062, 16, 194, 36);
        private IOverlay overlay;
        public MapRoom SelectedNode { get; set; }
        public override (Anchor anchor, float x, float y)? GoldHudPos => (Anchor.TopRight, 1048, 11);
        public override bool HasOverlay => overlay != null;

        public MapScene(GameContext game, RunState run) : base(game) { this.run = run; }

        private static int CellPx(int rows, int cols)
        {
            var usableW = Canvas.Width - PadX * 2; var usableH = Canvas.Height - HeaderH - FooterH - 20;
            return Math.Max(MinCell, Math.Min(MaxCell, Math.Min(usableW / cols, usableH / rows)));
        }
        private static (int left, int top) Origin(int rows, int cols, int cell) =>
            ((Canvas.Width - cols * cell) / 2, HeaderH + (Canvas.Height - HeaderH - FooterH - rows * cell) / 2);
        private static (int x, int y) Centre(MapRoom n, (int left, int top) org, int cell, int rows) =>
            (org.left + n.Column * cell + cell / 2, org.top + (rows - 1 - n.Row) * cell + cell / 2);

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
            DrawHeader(canvas);
            var gm = run.Map;
            if (gm != null)
            {
                var cell = CellPx(gm.Rows, gm.Columns); var org = Origin(gm.Rows, gm.Columns, cell);
                DrawGrid(canvas, gm, cell, org);
                DrawCorridors(canvas, gm, cell, org);
                DrawNodes(canvas, gm, cell, org);
                DrawHoverTip(canvas, gm);
            }
            Fonts.Get(11).DrawCentered(canvas, "Haz clic en una habitación disponible para avanzar", 640, 710, Colors.TextSecondary);
            overlay?.Draw(canvas);
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

        private void DrawGrid(Canvas canvas, RunMap gm, int cell, (int left, int top) org)
        {
            var occupied = new HashSet<(int, int)>();
            foreach (var n in gm.Nodes) occupied.Add((n.Row, n.Column));
            for (var r = 0; r < gm.Rows; r++)
                for (var c = 0; c < gm.Columns; c++)
                {
                    var x = org.left + c * cell; var y = org.top + (gm.Rows - 1 - r) * cell;
                    if (!occupied.Contains((r, c))) canvas.Fill(new RectF(x, y, cell, cell), GridEmpty);
                    canvas.Rect(new RectF(x, y, cell, cell), GridBorder, 1);
                }
        }

        private void DrawCorridors(Canvas canvas, RunMap gm, int cell, (int left, int top) org)
        {
            var width = Math.Max(8, cell / 4);
            var drawn = new HashSet<(string, string)>();
            foreach (var node in gm.Nodes)
            {
                var (x1, y1) = Centre(node, org, cell, gm.Rows);
                var live = node.Visited || node.Available;
                foreach (var cid in node.Connections)
                {
                    var child = gm.Find(cid); if (child == null) continue;
                    var key = string.CompareOrdinal(node.Id, cid) < 0 ? (node.Id, cid) : (cid, node.Id);
                    if (!drawn.Add(key)) continue;
                    var (x2, y2) = Centre(child, org, cell, gm.Rows);
                    canvas.Line(x1, y1, x2, y2, live || child.Visited || child.Available ? CorridorLive : CorridorIdle, width);
                }
            }
        }

        private void DrawNodes(Canvas canvas, RunMap gm, int cell, (int left, int top) org)
        {
            nodeRects.Clear(); nodeOrder.Clear();
            var inset = Math.Max(4, cell / 9); var ns = cell - inset * 2;
            var fontSize = Math.Max(8, Math.Min(11, cell / 7));
            foreach (var node in gm.Nodes)
            {
                var (cx, cy) = Centre(node, org, cell, gm.Rows);
                var rect = new IRect(cx - ns / 2, cy - ns / 2, ns, ns);
                nodeRects[node.Id] = rect; nodeOrder.Add(node.Id);
                var available = node.Available && !node.Visited; var hovered = hoveredId == node.Id;
                Rgba fill, border; int bw;
                if (node.Visited) { fill = FillVisited; border = BorderVisit; bw = 1; }
                else if (available) { fill = FillAvail.TryGetValue(node.Kind, out var f) ? f : new Rgba(100, 100, 100); border = hovered ? BorderHover : BorderAvail; bw = 2; }
                else { fill = FillAvail.TryGetValue(node.Kind, out var f) ? Locked(f) : new Rgba(20, 20, 25); border = BorderLocked; bw = 1; }
                canvas.Rect(rect, fill, 0, 5);
                canvas.Rect(rect, border, bw, 5);
                // pygame.draw writes the colour without blending: the "alpha 80" glow ring is opaque.
                if (available && !hovered) canvas.Rect(new IRect(rect.X - 2, rect.Y - 2, rect.W + 4, rect.H + 4), border, 1, 6);
                var labelCol = node.Visited ? LabelVisit : available ? LabelAvail : LabelLocked;
                var text = node.Visited ? "✓" : Labels.TryGetValue(node.Kind, out var l) ? l : "?";
                Fonts.Get(fontSize).DrawCentered(canvas, text, cx, cy, labelCol);
                if (node.Row == 0 && available) Fonts.Get(Math.Max(7, fontSize - 2)).DrawCentered(canvas, "INICIO", cx, rect.Bottom + 6, new Rgba(200, 200, 100));
            }
        }

        private void DrawHoverTip(Canvas canvas, RunMap gm)
        {
            if (hoveredId == null) return;
            var node = gm.Find(hoveredId);
            if (node == null || !(node.Available && !node.Visited)) return;
            var label = Labels.TryGetValue(node.Kind, out var l) ? l : "?";
            var font = Fonts.Get(13); var w = font.Width(label); var h = font.Height;
            var tx = Math.Min((int)mouse.x + 14, Canvas.Width - w - 10); var ty = Math.Max((int)mouse.y - 32, 4);
            var bg = new RectF(tx - 5, ty - 4, w + 10, h + 8);
            canvas.Rect(bg, Colors.BgPanel, 0, 4); canvas.Rect(bg, Colors.PanelBorder, 1, 4);
            font.Draw(canvas, label, tx, ty, Colors.TextAccent);
        }

        private void UpdateHover(float x, float y)
        {
            var previous = hoveredId; hoveredId = null;
            var gm = run.Map; if (gm == null) return;
            foreach (var id in nodeOrder)
            {
                if (!nodeRects[id].Contains(x, y)) continue;
                var node = gm.Find(id);
                if (node != null && node.Available && !node.Visited)
                {
                    hoveredId = id;
                    if (id != previous) Sound.PlayNav();
                }
                return;
            }
        }

        private void Click(float x, float y)
        {
            if (SelectedNode != null) return;
            if (relicRect.Contains(x, y)) { overlay = new RelicViewer(Game, run.OwnedRelics); Sound.PlayConfirm(); return; }
            if (deckRect.Contains(x, y)) { overlay = new PileViewer(Game, "Tu mazo", run.Deck, CardBonus.ForRun(run)); Sound.PlayCard(); return; }
            var gm = run.Map; if (gm == null) return;
            foreach (var id in nodeOrder)
            {
                if (!nodeRects[id].Contains(x, y)) continue;
                var node = gm.Find(id);
                if (node != null && node.Available && !node.Visited) { Sound.PlayTravel(); SelectedNode = node; }
                return;
            }
        }
    }
}
