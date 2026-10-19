using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>Pack art helpers (card_assets.pack_art: the closed booster scaled to a height).</summary>
    public static class PackArt
    {
        public static string ThemeKey(PackTheme theme) => theme.ToString().ToLowerInvariant();
        public static (int w, int h) Size(Art art, PackTheme theme, int height)
        {
            var s = art.PackArt(ThemeKey(theme));
            return s.Valid ? (Math.Max(1, MathX.Round(s.W * height / (double)s.H)), height) : (0, 0);
        }
        /// <summary>Draw the pack centred at (cx, cy); golden packs are gilded and animated.</summary>
        public static RectF Draw(Canvas canvas, Art art, PackTheme theme, int height, float cx, float cy, ChromaKind chroma = ChromaKind.None, float t = 0, float alpha = 1f, float baseW = 110)
        {
            var s = art.PackArt(ThemeKey(theme)); if (!s.Valid) return default;
            var (w, h) = Size(art, theme, height);
            var dst = RectF.CenteredInt(cx, cy, w, h);
            if (chroma != ChromaKind.None) ChromaFx.DrawGilded(canvas, s, dst, chroma, t, baseW, alpha);
            else canvas.BlitRect(s, dst, Rgba.White.WithAlpha((int)(255 * alpha)));
            return dst;
        }
    }

    /// <summary>scenes/shop_scene.py: three single-stock relics and three single-stock packs.</summary>
    public sealed class ShopScene : Scene
    {
        private const int RelicCost = 150, TileW = 330, TileH = 190, Gap = 24;
        private readonly RunState run;
        private readonly List<PackOffer> packs; private readonly List<RelicOffer> relics;
        private readonly HashSet<int> soldPacks = new HashSet<int>(), soldRelics = new HashSet<int>();
        private int purchases;
        private readonly List<IRect> packRects = new List<IRect>(), relicRects = new List<IRect>();
        private (string kind, int i)? hovered;
        private IRect? exitRect;
        private string feedbackText = ""; private float feedbackTime; private Rgba feedbackColor = Colors.TextAccent;
        public PackTheme? SelectedPack { get; set; }
        public ChromaKind SelectedPackChroma { get; set; }
        public bool Cleared { get; set; }

        public ShopScene(GameContext game, RunState run) : base(game)
        {
            this.run = run;
            (packs, relics) = run.PickShopStock();
        }

        public override void HandleEvent(in InputEvent e)
        {
            if (e.Type == EventType.MouseMove) UpdateHover(e.X, e.Y);
            else if (e.IsLeftDown) Click(e.X, e.Y);
        }
        public override void Update(float dt) => feedbackTime = Math.Max(0f, feedbackTime - dt);

        private void Label(Canvas canvas, string text, float cx, float cy, int size, Rgba color) => Fonts.Get(size).DrawCentered(canvas, text, cx, cy, color);

        public override void Draw(Canvas canvas)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(10, 14, 18));
            const int cx = 640;
            Label(canvas, "Tienda del Viajero", cx, 45, 26, Colors.TextAccent);
            var startX = cx - (3 * TileW + 2 * Gap) / 2;
            relicRects.Clear(); packRects.Clear();
            Label(canvas, "Reliquias", cx, 120, 16, Colors.TextSecondary);
            for (var i = 0; i < relics.Count; i++)
            {
                var rect = new IRect(startX + i * (TileW + Gap), 140, TileW, TileH); relicRects.Add(rect);
                var r = relics[i]; var cost = Price(RelicCost, true);
                DrawTile(canvas, r.Definition.Name, r.Definition.Description, r.Chroma, false, cost, rect, soldRelics.Contains(i), hovered == ("Reliquias", i),
                    alpha => RelicViewer.DrawRelicIcon(canvas, Art, r.Definition.Name, 64, rect.X + 55, rect.Y + 90, alpha));
            }
            Label(canvas, "Sobres", cx, 365, 16, Colors.TextSecondary);
            for (var i = 0; i < packs.Count; i++)
            {
                var rect = new IRect(startX + i * (TileW + Gap), 385, TileW, TileH); packRects.Add(rect);
                var p = packs[i]; var cost = Price(p.Definition.Cost, false);
                DrawTile(canvas, p.Definition.Name, p.Definition.Description, p.Chroma, true, cost, rect, soldPacks.Contains(i), hovered == ("Sobres", i),
                    alpha => PackArt.Draw(canvas, Art, p.Definition.Theme, 150, rect.X + 55, rect.Y + 90, p.Chroma, Game.Time, alpha));
            }
            if (feedbackTime > 0) Label(canvas, feedbackText, cx, 601, 16, feedbackColor);
            LuckBadge.Draw(canvas, Game, Anchor.BottomLeft, 16, 704, LuckReport.For(run));
            var exit = new IRect(cx - 80, 625, 160, 42); exitRect = exit;
            canvas.Rect(exit, Colors.BgPanel, 0, 6);
            Label(canvas, "Salir", exit.CenterX, exit.CenterY, 16, Colors.TextPrimary);
        }

        private void DrawTile(Canvas canvas, string name, string description, ChromaKind chroma, bool isPack, int cost, IRect rect, bool sold, bool isHovered, Action<float> drawArt)
        {
            var available = !sold && run.Gold >= cost;
            canvas.Rect(rect, Colors.BgPanel, 0, 8);
            canvas.Rect(rect, isHovered && available ? Colors.TextAccent : Colors.PanelBorder, 2, 8);
            drawArt(sold ? 60 / 255f : 1f);
            var color = sold ? Colors.TextSecondary : Colors.TextPrimary;
            var textX = rect.X + 215;
            Label(canvas, ChromaFx.Title(name, chroma, isPack), rect.CenterX, rect.Y + 25, 16, color);
            if (chroma != ChromaKind.None)
            {
                var st = ChromaFx.Style(chroma);
                description += " " + (isPack ? st.PackNote : st.ShortNote + ".");
                if (!sold) ChromaFx.DrawBox(canvas, Art, rect, chroma, Game.Time, 8);
            }
            var font = Fonts.Get(13);
            var lines = RelicViewer.CardWrap(description, font, 205);
            var room = Math.Max(1, (rect.Bottom - 52 - (rect.Y + 65)) / 19 + 1);
            if (lines.Count > room) { lines = lines.GetRange(0, room); lines[lines.Count - 1] = TextFit.Fit(font, lines[lines.Count - 1] + "…", 205); }
            for (var i = 0; i < lines.Count; i++) Label(canvas, lines[i], textX, rect.Y + 65 + i * 19, 13, color);
            Label(canvas, sold ? "Agotado" : cost == 0 ? "¡Gratis!" : cost + " oro", rect.CenterX, rect.Bottom - 40, 16, sold ? Colors.TextSecondary : Colors.TextAccent);
            if (!sold && !available) Label(canvas, "Sin fondos", rect.CenterX, rect.Bottom - 18, 12, new Rgba(180, 90, 90));
        }

        private void UpdateHover(float x, float y)
        {
            var previous = hovered; hovered = null;
            foreach (var (kind, rects) in new[] { ("Sobres", packRects), ("Reliquias", relicRects) })
                for (var i = 0; i < rects.Count; i++)
                    if (rects[i].Contains(x, y)) { hovered = (kind, i); if (hovered != previous) Sound.PlayNav(); return; }
        }

        private int Price(int basePrice, bool relic) => run.ShopOfferPrice(basePrice, relic, purchases);

        private void Click(float x, float y)
        {
            if (SelectedPack.HasValue || Cleared) return;
            for (var i = 0; i < packRects.Count; i++)
            {
                if (!packRects[i].Contains(x, y)) continue;
                var pack = packs[i]; var price = Price(pack.Definition.Cost, false);
                if (!PurchaseAllowed(soldPacks.Contains(i), price)) return;
                run.Spend(price); purchases++; soldPacks.Add(i);
                SelectedPack = pack.Definition.Theme; SelectedPackChroma = pack.Chroma;
                Sound.PlayPurchase(); ShowFeedback(pack.Definition.Name + " comprado");
                return;
            }
            for (var i = 0; i < relicRects.Count; i++)
            {
                if (!relicRects[i].Contains(x, y)) continue;
                var price = Price(RelicCost, true);
                if (!PurchaseAllowed(soldRelics.Contains(i), price)) return;
                if (price == 0) run.UseVoucher();
                run.Spend(price); purchases++; soldRelics.Add(i);
                run.AcquireRelic(relics[i]);
                Sound.PlayPurchase(); ShowFeedback(relics[i].Definition.Name + " obtenida");
                return;
            }
            if (exitRect.HasValue && exitRect.Value.Contains(x, y)) { Cleared = true; Sound.PlayCancel(); }
        }

        private void ShowFeedback(string text, bool error = false)
        { feedbackText = text; feedbackTime = 2f; feedbackColor = error ? new Rgba(235, 130, 125) : Colors.TextAccent; }

        private bool PurchaseAllowed(bool sold, int cost)
        {
            string message;
            if (sold) message = "Este objeto ya está agotado";
            else if (run.Gold < cost) message = "Te faltan " + (cost - run.Gold) + " de oro";
            else return true;
            ShowFeedback(message, true); Sound.PlayError();
            return false;
        }
    }

    /// <summary>scenes/event_scene.py: narrative text and a gold pickup.</summary>
    public sealed class EventScene : Scene
    {
        private static readonly (string title, string body)[] Events =
        {
            ("Monedas Dispersas", "Encuentras monedas dispersadas por el suelo.\n¡Alguien debió pasar aquí con prisa!"),
            ("Cofre Olvidado", "Hay un pequeño cofre medio enterrado entre las piedras.\nNo tiene cerradura. ¡Qué suerte!"),
            ("Mercader Caído", "Un mercader caído ha perdido su bolsa de monedas.\nParece que ya no la necesita."),
            ("Trampa Fallida", "Una trampa dispara unas monedas al aire.\nAlgún ingeniero tuvo un día malo."),
        };
        private readonly string title; private readonly string[] bodyLines;
        private IRect? button;
        public int Gold { get; }
        public bool Cleared { get; set; }

        public EventScene(GameContext game, RunState run, int gold, string roomId) : base(game)
        {
            Gold = gold;
            // The original indexes by the process-salted hash(); a stable CRC keeps the same four texts.
            var e = Events[PyRandom.Crc32(roomId ?? "") % Events.Length];
            title = e.title; bodyLines = e.body.Split('\n');
        }

        public override void HandleEvent(in InputEvent e)
        {
            if (Cleared) return;
            if (e.IsLeftDown && button.HasValue && button.Value.Contains(e.X, e.Y)) { Sound.PlayReward(); Cleared = true; }
        }

        public override void Draw(Canvas canvas)
        {
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(10, 10, 20));
            const int cx = 640;
            Fonts.Get(24).DrawCentered(canvas, "Evento Misterioso", cx, 80, new Rgba(140, 80, 200));
            Fonts.Get(18).DrawCentered(canvas, title, cx, 140, Colors.TextAccent);
            var cy = 190;
            foreach (var line in bodyLines) { Fonts.Get(14).DrawCentered(canvas, line, cx, cy, Colors.TextPrimary); cy += 26; }
            Fonts.Get(18).DrawCentered(canvas, "Recompensa: +" + Gold + " de oro", cx, 300, new Rgba(220, 190, 50));
            var f = Fonts.Get(15); var w = f.Width("Recoger"); var h = f.Height;
            var bx = cx - w / 2; var by = 380 - h / 2;
            var rect = new IRect(bx - 20, by - 8, w + 40, h + 16); button = rect;
            canvas.Rect(rect, new Rgba(50, 120, 50), 0, 6); canvas.Rect(rect, new Rgba(60, 160, 60), 2, 6);
            f.Draw(canvas, "Recoger", bx, by, Colors.TextPrimary);
        }
    }
}
