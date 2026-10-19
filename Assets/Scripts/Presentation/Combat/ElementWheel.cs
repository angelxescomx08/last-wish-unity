using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>ui/element_wheel.py: the four elements on a diamond and the six reactions between them (R key / button).</summary>
    public sealed class ElementWheelOverlay : IOverlay
    {
        public static readonly IRect PanelRect = new IRect(150, 70, 980, 580);
        private static readonly (int x, int y) Center = (PanelRect.X + 230, PanelRect.Y + 300);
        private const int R = 150;
        private static readonly Rgba Text = new Rgba(236, 228, 214);
        /// <summary>REACTIONS in Python insertion order.</summary>
        private static readonly (ElementKind a, ElementKind b, ReactionKind r)[] Reactions =
        {
            (ElementKind.Fire, ElementKind.Water, ReactionKind.Steam), (ElementKind.Fire, ElementKind.Earth, ReactionKind.Magma),
            (ElementKind.Fire, ElementKind.Air, ReactionKind.Wildfire), (ElementKind.Water, ElementKind.Earth, ReactionKind.Mud),
            (ElementKind.Water, ElementKind.Air, ReactionKind.Freeze), (ElementKind.Earth, ElementKind.Air, ReactionKind.Sandstorm),
        };
        private static readonly Dictionary<ReactionKind, string> Short = new Dictionary<ReactionKind, string>
        {
            [ReactionKind.Steam] = "doble de las acumulaciones como daño", [ReactionKind.Magma] = "le quita el escudo · Vulnerable 2",
            [ReactionKind.Wildfire] = "Fuego a todos los enemigos", [ReactionKind.Mud] = "triple de las acumulaciones en escudo",
            [ReactionKind.Freeze] = "pierde su próxima acción", [ReactionKind.Sandstorm] = "Débil 1 a todos los enemigos",
        };
        private static readonly string[] Alone = { "pierde vida al final de su turno", "sus golpes hacen menos daño", "recibe más daño por golpe", "nada; sus reacciones van a todos" };

        public static (int x, int y) Corner(ElementKind e)
        {
            switch (e)
            {
                case ElementKind.Fire: return (Center.x, Center.y - R);
                case ElementKind.Water: return (Center.x + R, Center.y);
                case ElementKind.Air: return (Center.x, Center.y + R);
                default: return (Center.x - R, Center.y);
            }
        }

        public static (int x, int y) ReactionSpot(ElementKind e1, ElementKind e2)
        {
            // sorted by Spanish name ("Agua" < "Aire" < "Fuego" < "Tierra"); the midpoint is symmetric anyway.
            var (ax, ay) = Corner(e1); var (bx, by) = Corner(e2);
            var mx = MathX.FloorDiv(ax + bx, 2); var my = MathX.FloorDiv(ay + by, 2);
            bool Pair(ElementKind p, ElementKind q) => e1 == p && e2 == q || e1 == q && e2 == p;
            if (Pair(ElementKind.Fire, ElementKind.Air)) return (mx, my - 58);
            if (Pair(ElementKind.Water, ElementKind.Earth)) return (mx + 58, my);
            return (mx, my);
        }

        private readonly GameContext game;
        private (float x, float y) mouse; private bool down; private float t;
        private readonly IRect closeRect = new IRect(PanelRect.Right - 150, PanelRect.Bottom - 50, 130, 36);
        public bool Dismissed { get; private set; }
        public ElementWheelOverlay(GameContext game) { this.game = game; }

        public void HandleEvent(in InputEvent e)
        {
            if (e.Type == EventType.MouseMove) mouse = e.Pos;
            else if (e.Type == EventType.MouseDown && (e.Button == 1 || e.Button == 3))
            {
                down = true;
                if (closeRect.Contains(e.X, e.Y) || !PanelRect.Contains(e.X, e.Y) || e.Button == 3) Dismissed = true;
            }
            else if (e.Type == EventType.MouseUp) down = false;
            else if (e.Type == EventType.KeyDown && (e.Key == Key.Escape || e.Key == Key.R || e.Key == Key.Return)) Dismissed = true;
        }

        public void Update(float dt) => t += Math.Max(0f, dt);

        public void Draw(Canvas canvas)
        {
            var art = game.Art; var g = game.Data.Glossary; var fonts = game.Fonts;
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(6, 4, 10, 190));
            PixelUi.Panel(canvas, art, PanelRect);
            PixelUi.Ribbon(canvas, art, PanelRect.CenterX, PanelRect.Y + 4, "Elementos y reacciones", 16);
            foreach (var (a, b, _) in Reactions)
            {
                var (ax, ay) = Corner(a); var (bx, by) = Corner(b);
                canvas.Line(ax, ay, bx, by, new Rgba(74, 66, 92), 3);
            }
            foreach (var (a, b, r) in Reactions)
            {
                var (x, y) = ReactionSpot(a, b);
                canvas.Circle(x, y, 19, new Rgba(22, 16, 28));
                canvas.Circle(x, y, 19, ElementPreview.ReactionColor(g, r), 2);
                var iconName = ElementPreview.ReactionIcon(g, r);
                if (art.HasIcon(iconName)) { var ic = art.Icon(iconName); var ir = IRect.AtCenter(x, y, ic.W * 2, ic.H * 2); canvas.Blit(ic, ir.X, ir.Y, 2); }
            }
            foreach (var el in ElementPreview.Order)
            {
                var (x, y) = Corner(el);
                var pulse = 0.5 + 0.5 * Math.Sin(t * 2.4 + (int)el);
                var ink = ElementPreview.Ink(g, el);
                canvas.Circle(x, y, 36, new Rgba(22, 16, 28));
                canvas.Circle(x, y, 36, ink, 2 + (int)pulse);
                var iconName = ElementPreview.Icon(el);
                if (art.HasIcon(iconName)) { var ic = art.Icon(iconName); var ir = IRect.AtCenter(x, y, ic.W * 4, ic.H * 4); canvas.Blit(ic, ir.X, ir.Y, 4); }
                var offY = el == ElementKind.Fire ? -52 : el == ElementKind.Air ? 52 : 50;
                Txt.OutlinedAt(canvas, fonts.Get(16), ElementPreview.Name(el), Anchor.Center, x, y + offY, ink);
            }
            Txt.OutlinedAt(canvas, fonts.Get(14), "Dos elementos distintos en un enemigo = reacción", Anchor.Center, Center.x, PanelRect.Bottom - 34, new Rgba(176, 170, 160));

            int x0 = PanelRect.X + 470, yy = PanelRect.Y + 56;
            Txt.Outlined(canvas, fonts.Get(16), "Reacciones (con la suma de acumulaciones)", x0, yy, new Rgba(246, 204, 92)); yy += 32;
            var row = new List<ElementPreview.Part>(8);
            foreach (var (a, b, r) in Reactions)
            {
                row.Clear();
                row.Add(ElementPreview.Part.Ic(ElementPreview.ReactionIcon(g, r)));
                row.Add(ElementPreview.Part.T(CombatLog.ReactionName(g, r), ElementPreview.ReactionColor(g, r)));
                row.Add(ElementPreview.Part.T(" ", Text));
                row.Add(ElementPreview.Part.Ic(ElementPreview.Icon(a))); row.Add(ElementPreview.Part.T("+", Text)); row.Add(ElementPreview.Part.Ic(ElementPreview.Icon(b)));
                ElementPreview.DrawRow(canvas, art, row, x0, yy + 12);
                Txt.Outlined(canvas, fonts.Get(14), Short[r], x0 + 40, yy + 28, Text);
                yy += 46;
            }
            yy += 10;
            Txt.Outlined(canvas, fonts.Get(16), "Cada elemento solo", x0, yy, new Rgba(246, 204, 92)); yy += 24;
            foreach (var el in ElementPreview.Order)
            {
                row.Clear();
                row.Add(ElementPreview.Part.Ic(ElementPreview.Icon(el)));
                row.Add(ElementPreview.Part.T(ElementPreview.Name(el) + ":", ElementPreview.Ink(g, el)));
                row.Add(ElementPreview.Part.T(Alone[(int)el], Text));
                ElementPreview.DrawRow(canvas, art, row, x0, yy + 10, 14);
                yy += 26;
            }
            PixelUi.Button(canvas, art, closeRect, "Cerrar", "bronze", PixelUi.StateOf(closeRect, mouse, down), null, "R", game.Time, 0, 14);
        }
    }
}
