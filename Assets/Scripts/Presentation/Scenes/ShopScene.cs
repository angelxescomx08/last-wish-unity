using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// The traveller's shop (interface redesigned 2026-11-04): the dungeon room behind a warm focus, a title band, a striped
    /// awning and two wooden shelves drawn by code (<see cref="ShopStall"/>), with three relics and three packs on them.
    /// Every offer is an iron card: the relic in a lit niche (or the pack art), its name, rarity / theme and chroma tags,
    /// the price as a kit button and the rule text colour-coded underneath. Each offer sells once; a bought relic flies to the
    /// relic counter, a bought pack rises and opens. Prices, stock and rules are the domain's (<see cref="RunState"/>).
    /// </summary>
    public sealed class ShopScene : Scene
    {
        // ------------------------------------------------------------------ layout
        public const int RelicCost = 150, CardW = 312, CardH = 212, Gap = 16, RowX = 272, RelicRowY = 152, PackRowY = 402;
        public const int StallX = 256, StallW = 1000, AwningY = 98, Niche = 76, NicheH = 88, Inset = 16, ButtonH = 36, DescTop = 122, DescLine = 20;
        public const float FlyT = 0.6f, PackRiseT = 0.5f;
        public static readonly IRect ExitButton = new IRect(1102, 658, 150, 36), RelicCounter = new IRect(24, 16, 196, 36);
        /// <summary>Where El Mercader stands: his feet, left of the stall, over the luck badge.</summary>
        public const int MerchantX = 150, MerchantGround = 636, BubbleMaxW = 200, BubbleLine = 20;
        public static IRect RelicRect(int i) => new IRect(RowX + i * (CardW + Gap), RelicRowY, CardW, CardH);
        public static IRect PackRect(int i) => new IRect(RowX + i * (CardW + Gap), PackRowY, CardW, CardH);
        /// <summary>The top of the shelf under a row (the cards stand on it).</summary>
        public static int ShelfY(int rowY) => rowY + CardH - 2;

        private static readonly Rgba Ink = Colors.Ink, Cream = Colors.Cream, Dim = new Rgba(186, 176, 160), Gilt = new Rgba(255, 214, 120),
            Short = new Rgba(255, 140, 118), Warm = new Rgba(255, 196, 120);

        /// <summary>One offer's card: what it shows and where (tests: every relic and pack fits).</summary>
        public sealed class OfferView
        {
            public IRect Rect; public RectF Art, Name, Button; public string Title; public List<(string text, Rgba color, int gem, RectF rect)> Tags = new List<(string, Rgba, int, RectF)>();
            public List<RichLine> Lines = new List<RichLine>(); public List<string> Plain = new List<string>(); public RectF Text;
            public int Price, BasePrice; public bool Sold, Affordable, Golden;
        }

        private readonly RunState run;
        private readonly List<PackOffer> packs; private readonly List<RelicOffer> relics;
        private readonly HashSet<int> soldPacks = new HashSet<int>(), soldRelics = new HashSet<int>();
        private int purchases;
        private ChoiceRecord relicChoice;     // power telemetry: the three relics on sale
        private readonly DungeonBackdrop backdrop;
        private readonly NpcSheet merchant;
        private readonly MerchantSpeech speech;
        private readonly MerchantPose pose;
        private readonly BurstParticles fx = new BurstParticles(500, 29);
        private (string kind, int i)? hovered;
        private bool exitHovered;
        private float time, feedbackTime, counterPulse;
        private string feedbackText = ""; private Rgba feedbackColor = Gilt;
        // a relic flying to the counter, a pack rising to be opened
        private int flyingRelic = -1, risingPack = -1; private float flyT, riseT;
        public PackTheme? SelectedPack { get; set; }
        public ChromaKind SelectedPackChroma { get; set; }
        public bool Cleared { get; set; }
        public bool Busy => risingPack >= 0;

        public ShopScene(GameContext game, RunState run) : this(game, run, null, null) { }

        /// <summary>A shop with a given stock (tests and tools); null = the room's own stock.</summary>
        public ShopScene(GameContext game, RunState run, IList<PackOffer> packStock, IList<RelicOffer> relicStock) : base(game)
        {
            this.run = run;
            (packs, relics) = run.PickShopStock();
            if (packStock != null) packs = packStock.ToList();
            if (relicStock != null) relics = relicStock.ToList();
            relicChoice = game.Track(p => p.OfferRelics(run, "shop", relics.Select(r => r.Definition)));
            backdrop = new DungeonBackdrop(Art, 41, 0.6f);
            merchant = MerchantSheet.Load(Art);
            var seed = (int)(run.Seed % 100003) * 31 + run.Floor * 7 + (run.CurrentRoomId ?? "").Length;
            speech = new MerchantSpeech(seed); pose = new MerchantPose(seed + 1);
            speech.Say(MerchantCue.Enter);
        }

        public MerchantSpeech Speech => speech;
        public NpcSheet Merchant => merchant;
        public MerchantPose Pose => pose;

        public IReadOnlyList<RelicOffer> Relics => relics;
        public IReadOnlyList<PackOffer> Packs => packs;
        public bool RelicSold(int i) => soldRelics.Contains(i);
        public bool PackSold(int i) => soldPacks.Contains(i);

        // ------------------------------------------------------------------ input
        public override void HandleEvent(in InputEvent e)
        {
            if (e.Type == EventType.MouseMove) UpdateHover(e.X, e.Y);
            else if (e.IsLeftDown) Click(e.X, e.Y);
        }

        private void UpdateHover(float x, float y)
        {
            var previous = hovered; var wasExit = exitHovered; hovered = null; exitHovered = ExitButton.Contains(x, y);
            for (var i = 0; i < relics.Count; i++) if (RelicRect(i).Contains(x, y)) hovered = ("relic", i);
            for (var i = 0; i < packs.Count; i++) if (PackRect(i).Contains(x, y)) hovered = ("pack", i);
            if (hovered.HasValue && hovered != previous || exitHovered && !wasExit) Sound.PlayNav();
            if (hovered.HasValue && hovered != previous && !Busy) Comment(hovered.Value.kind, hovered.Value.i);
        }

        /// <summary>The shopkeeper may say something about the offer under the mouse.</summary>
        private void Comment(string kind, int i)
        {
            var relic = kind == "relic"; var v = relic ? RelicView(i) : PackView(i);
            if (v.Sold) return;
            var cue = !v.Affordable ? MerchantCue.HoverPoor : v.Golden ? MerchantCue.HoverGolden : relic ? MerchantCue.HoverRelic : MerchantCue.HoverPack;
            speech.Hover(kind + i, cue, v.Title, Math.Max(0, v.Price - run.Gold));
        }

        private bool AllSold => soldRelics.Count >= relics.Count && soldPacks.Count >= packs.Count;

        private int Price(int basePrice, bool relic) => run.ShopOfferPrice(basePrice, relic, purchases);
        public int RelicPrice => Price(RelicCost, true);
        public int PackPrice(int i) => Price(packs[i].Definition.Cost, false);

        private void Click(float x, float y)
        {
            if (SelectedPack.HasValue || Cleared || Busy) return;
            for (var i = 0; i < packs.Count; i++)
            {
                if (!PackRect(i).Contains(x, y)) continue;
                var pack = packs[i]; var price = PackPrice(i);
                if (!PurchaseAllowed(soldPacks.Contains(i), price)) { Shake(PackRect(i)); return; }
                run.Spend(price); purchases++; soldPacks.Add(i);
                risingPack = i; riseT = 0;
                Sound.PlayPurchase(); ShowFeedback(pack.Definition.Name + " comprado");
                speech.Say(AllSold ? MerchantCue.SoldOut : pack.Chroma != ChromaKind.None ? MerchantCue.BuyGolden : MerchantCue.BuyPack, pack.Definition.Name);
                Coins(PackRect(i));
                return;
            }
            for (var i = 0; i < relics.Count; i++)
            {
                if (!RelicRect(i).Contains(x, y)) continue;
                var price = RelicPrice;
                if (!PurchaseAllowed(soldRelics.Contains(i), price)) { Shake(RelicRect(i)); return; }
                if (price == 0) run.UseVoucher();
                run.Spend(price); purchases++; soldRelics.Add(i);
                run.AcquireRelic(relics[i]);
                var bought = relics[i].Definition;
                Game.Track(p => { p.Pick(relicChoice, bought.Id); p.RelicGained(run, bought, "shop"); });
                flyingRelic = i; flyT = 0;
                Sound.PlayPurchase(); ShowFeedback(bought.Name + " obtenida");
                speech.Say(AllSold ? MerchantCue.SoldOut : price == 0 ? MerchantCue.BuyFree : relics[i].Chroma != ChromaKind.None ? MerchantCue.BuyGolden : MerchantCue.BuyRelic, bought.Name);
                Coins(RelicRect(i));
                return;
            }
            if (ExitButton.Contains(x, y)) { Cleared = true; Sound.PlayCancel(); }
        }

        private (float x, float y, float t)? shakeAt;
        private void Shake(IRect r) => shakeAt = (r.X, r.Y, 0.35f);

        private void Coins(IRect r)
        {
            var (bx, by) = (r.X + r.W - 120f, r.Y + Inset + 52 + ButtonH / 2f);
            fx.Burst(bx, by, 22, new[] { Rgba.White, Gilt, new Rgba(230, 150, 30) }, (80f, 260f), ((float)Math.PI * 1.1f, (float)Math.PI * 1.9f), (0.35f, 0.7f), (2f, 3.5f), ParticleStyle.Square, 2.4f, 380f);
        }

        private void ShowFeedback(string text, bool error = false)
        { feedbackText = text; feedbackTime = 2f; feedbackColor = error ? Short : Gilt; }

        private bool PurchaseAllowed(bool sold, int cost)
        {
            string message;
            if (sold) { message = "Este objeto ya está agotado"; speech.Say(MerchantCue.Sold); }
            else if (run.Gold < cost) { message = "Te faltan " + (cost - run.Gold) + " de oro"; speech.Say(MerchantCue.Poor, null, cost - run.Gold); }
            else return true;
            ShowFeedback(message, true); Sound.PlayError();
            return false;
        }

        // ------------------------------------------------------------------ simulation
        public override void Update(float dt)
        {
            dt = Math.Min(0.1f, Math.Max(0f, dt));
            time += dt; feedbackTime = Math.Max(0f, feedbackTime - dt); counterPulse = Math.Max(0f, counterPulse - dt * 2.2f);
            if (shakeAt.HasValue) { var s = shakeAt.Value; s.t -= dt; shakeAt = s.t > 0 ? s : ((float, float, float)?)null; }
            backdrop.Update(dt);
            speech.Update(dt);
            pose.Update(dt, speech, merchant);
            if (flyingRelic >= 0)
            {
                flyT += dt;
                if (flyT >= FlyT)
                {
                    fx.Burst(RelicCounter.X + 22, RelicCounter.CenterY, 18, Palette(relics[flyingRelic].Definition.Rarity), (60f, 200f), null, (0.3f, 0.6f), (1.5f, 3f), ParticleStyle.Spark, 3f);
                    counterPulse = 1f; flyingRelic = -1;
                }
            }
            if (risingPack >= 0)
            {
                riseT += dt;
                if (riseT >= PackRiseT)
                {
                    var pack = packs[risingPack]; risingPack = -1;
                    SelectedPack = pack.Definition.Theme; SelectedPackChroma = pack.Chroma;
                }
            }
            fx.Update(dt);
        }

        private static Rgba[] Palette(int rarity) { var c = CardWidget.RarityColor(rarity); return new[] { Rgba.White, Glows.Scaled(c, 1.3), c }; }

        // ------------------------------------------------------------------ offers
        private static readonly Dictionary<PackTheme, (string tag, string noun, Rgba color)> Themes = new Dictionary<PackTheme, (string, string, Rgba)>
        {
            [PackTheme.Acero] = ("Ataque", "de ataque", new Rgba(236, 104, 92)),
            [PackTheme.Escudo] = ("Defensa", "defensivas", new Rgba(100, 172, 255)),
            [PackTheme.Magia] = ("Magia", "de magia", new Rgba(190, 128, 255)),
            [PackTheme.Epico] = ("Épico", "poderosas", new Rgba(255, 200, 90)),
        };

        /// <summary>"Dorada ×2" / "Diamante ×3": short enough to share a row with the rarity tag.</summary>
        public static string ChromaTag(ChromaKind chroma) => ChromaFx.Style(chroma).Name + " ×" + ((int)chroma + 1);

        /// <summary>The card of relic <paramref name="i"/>: niche, name, tags, price button and rule text.</summary>
        public OfferView RelicView(int i)
        {
            var offer = relics[i]; var def = offer.Definition; var r = RelicRect(i);
            var v = new OfferView { Rect = r, Title = def.Name, Price = RelicPrice, BasePrice = RelicCost, Sold = soldRelics.Contains(i), Golden = offer.Chroma != ChromaKind.None };
            v.Affordable = run.Gold >= v.Price;
            v.Art = new RectF(r.X + Inset, r.Y + Inset, Niche, NicheH);
            var rx = r.X + Inset + Niche + 12; var rw = r.Right - Inset - rx;
            v.Name = Txt.Place(PixelUi.InkTextWidth(Art, def.Name, 16, true), Fonts.Bold(16).Height + 2, Anchor.MidLeft, rx, r.Y + Inset + 10);
            var rarity = Math.Max(1, Math.Min(5, def.Rarity));
            var tags = new List<(string, Rgba, int)> { (RunContent.RarityLabel(rarity), CardWidget.RarityColor(rarity), rarity) };
            if (offer.Chroma != ChromaKind.None)
            {
                // "Dorada ×2" when it shares the row with the rarity, else just "Dorada"
                var full = ChromaTag(offer.Chroma); var st = ChromaFx.Style(offer.Chroma);
                tags.Add((TagWidth(tags[0].Item1, rarity) + 6 + TagWidth(full, 0) <= rw ? full : st.Name, st.Main, 0));
            }
            PlaceTags(v, tags, rx, r.Y + Inset + 24, rw);
            v.Button = new RectF(rx, r.Y + Inset + NicheH - ButtonH, rw, ButtonH);
            v.Text = new RectF(r.X + Inset, r.Y + DescTop, r.W - 2 * Inset, r.Bottom - 10 - (r.Y + DescTop));
            Describe(v, def.Description);
            return v;
        }

        /// <summary>The card of pack <paramref name="i"/>: art, name, theme and chroma tags, price button and what it gives.</summary>
        public OfferView PackView(int i)
        {
            var offer = packs[i]; var def = offer.Definition; var r = PackRect(i); var theme = Themes[def.Theme];
            var v = new OfferView { Rect = r, Title = def.Name, Price = PackPrice(i), BasePrice = def.Cost, Sold = soldPacks.Contains(i), Golden = offer.Chroma != ChromaKind.None };
            v.Affordable = run.Gold >= v.Price;
            var (aw, ah) = PackArt.Size(Art, def.Theme, CardH - 2 * Inset - 4);
            v.Art = new RectF(r.X + Inset + 2, r.Y + Inset + 2, Math.Max(aw, 96), ah);
            var rx = (int)v.Art.Right + 12; var rw = r.Right - Inset - rx;
            v.Name = Txt.Place(PixelUi.InkTextWidth(Art, def.Name, 16, true), Fonts.Bold(16).Height + 2, Anchor.MidLeft, rx, r.Y + Inset + 10);
            var tags = new List<(string, Rgba, int)> { (theme.tag, theme.color, 0) };
            if (offer.Chroma != ChromaKind.None) tags.Add((ChromaFx.Style(offer.Chroma).NameMasc, ChromaFx.Style(offer.Chroma).Main, 0));
            PlaceTags(v, tags, rx, r.Y + Inset + 24, rw);
            v.Button = new RectF(rx, r.Y + Inset + NicheH - ButtonH, rw, ButtonH);
            v.Text = new RectF(rx, r.Y + DescTop, rw, r.Bottom - 10 - (r.Y + DescTop));
            var picks = run.PackPicks(offer.Chroma);
            Describe(v, "Abre " + RunState.PackSize + " cartas " + theme.noun + " y te quedas con " + picks + ".");
            return v;
        }

        private int TagWidth(string text, int gem) => PixelUi.InkTextWidth(Art, text, 13) + 16 + (gem > 0 ? 12 : 0);

        private void PlaceTags(OfferView v, List<(string text, Rgba color, int gem)> tags, float x, float y, float width)
        {
            float cx = x;
            foreach (var (text, color, gem) in tags)
            {
                var w = TagWidth(text, gem);
                if (cx > x && cx + w > x + width) { cx = x; y += 28; }
                v.Tags.Add((text, color, gem, new RectF(cx, y, Math.Min(w, width), 24)));
                cx += w + 6;
            }
        }

        private void Describe(OfferView v, string text)
        {
            var font = Fonts.Get(13);
            if (Game.Data != null) v.Lines = RichText.Lines(Game.Data.Glossary, text, font, (int)v.Text.W, Cream).ToList();
            else v.Plain = TextFit.Wrap(font, text, (int)v.Text.W);
        }

        /// <summary>What the price button says: "150 oro" in the chunky face (+ "faltan N" when short of gold), shrinking so it
        /// never leaves the button; the undiscounted price, struck through, when a relic lowered it.</summary>
        public (string label, int size, string shortBy, string was, int width) PriceLine(int price, int basePrice, int gold, float room)
        {
            string Num(int n) => n.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");
            var shortBy = gold >= price ? null : "faltan " + Num(price - gold);
            var was = price < basePrice ? Num(basePrice) : null;
            foreach (var (label, size, extra, old) in new[] { (Num(price) + " oro", 20, shortBy, was), (Num(price), 20, shortBy, was), (Num(price), 20, shortBy, (string)null), (Num(price), 16, shortBy, (string)null), (Num(price), 16, (string)null, (string)null) })
            {
                var text = price == 0 ? "¡Gratis!" : label;
                var w = (price == 0 ? 0 : 32) + PixelUi.InkTextWidth(Art, text, size) + (extra != null ? 8 + PixelUi.InkTextWidth(Art, extra, 13) : 0) + (old != null ? PixelUi.InkTextWidth(Art, old, 13) + 8 : 0);
                if (w <= room) return (text, size, extra, old, w);
            }
            var cut = TextFit.Fit(Fonts.Get(16), Num(price), (int)room - 34);
            return (cut, 16, null, null, 32 + PixelUi.InkTextWidth(Art, cut, 16));
        }

        // ------------------------------------------------------------------ drawing
        public override void Draw(Canvas canvas)
        {
            backdrop.Draw(canvas);
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(12, 6, 4, 140));
            canvas.BlitRect(Vignette(), new RectF(0, 0, Canvas.Width, Canvas.Height), Rgba.White);
            DrawBand(canvas);
            DrawRelicCounter(canvas);
            DrawStall(canvas);
            DrawMerchant(canvas);
            for (var i = 0; i < relics.Count; i++) DrawOffer(canvas, RelicView(i), "relic", i);
            for (var i = 0; i < packs.Count; i++) DrawOffer(canvas, PackView(i), "pack", i);
            DrawBubble(canvas);
            DrawFlights(canvas);
            fx.Draw(canvas, Art);
            LuckBadge.Draw(canvas, Game, Anchor.BottomLeft, 16, 704, LuckReport.For(run));
            if (exitHovered && !Busy) PixelUi.Frame2(canvas, ((RectF)ExitButton).Move(0, -1), new Rgba(220, 200, 160));
            PixelUi.Button(canvas, Art, ExitButton, "Salir", "bronze", Busy ? ButtonState.Off : exitHovered ? ButtonState.Hover : ButtonState.Idle, null, null, time, 0f, 16);
            if (feedbackTime > 0)
            {
                canvas.PushAlpha(Math.Min(1f, feedbackTime / 0.3f));
                var w = PixelUi.InkTextWidth(Art, feedbackText, 16) + 48;
                var box = RectF.CenteredInt(640, 676, w, 40);
                PixelUi.Panel(canvas, Art, box);
                PixelUi.InkText(canvas, Art, feedbackText, 16, Anchor.Center, box.CenterX, box.CenterY - 1, feedbackColor);
                canvas.PopAlpha();
            }
        }

        private Sprite Vignette() => new Sprite(Art.Procedural("shop_vignette", 160, 90, () =>
        {
            var px = new Rgba[160 * 90];
            for (var y = 0; y < 90; y++)
                for (var x = 0; x < 160; x++)
                {
                    var d = Math.Sqrt(Math.Pow((x - 80) / 96.0, 2) + Math.Pow((y - 50) / 62.0, 2));
                    px[y * 160 + x] = new Rgba(8, 4, 2, (int)(225 * Math.Pow(MathX.Clamp01((d - 0.5) / 0.6), 1.4)));
                }
            return px;
        }, smooth: true));

        private Sprite Band() => new Sprite(Art.Procedural("shop_band", 1, 96, () =>
        {
            var px = new Rgba[96];
            for (var y = 0; y < 96; y++) px[y] = new Rgba(10, 6, 4, (int)(220 * Math.Pow(1 - y / 96.0, 1.4)));
            return px;
        }));

        private void DrawBand(Canvas canvas)
        {
            canvas.BlitRect(Band(), new RectF(0, 0, Canvas.Width, 96), Rgba.White);
            Glows.Draw(canvas, Art, 640, 40, 150, new Rgba(80, 52, 18));
            PixelUi.InkText(canvas, Art, "Tienda del Viajero", 30, Anchor.Center, 640, 38, Gilt, true, true);
            PixelUi.Ribbon(canvas, Art, 640, 80, "Cada objeto se vende una sola vez", 13);
        }

        /// <summary>Where bought relics land: an iron plate with the bag and the number of relics.</summary>
        private void DrawRelicCounter(Canvas canvas)
        {
            var plate = (RectF)RelicCounter;
            if (counterPulse > 0) { plate = plate.Move(0, counterPulse > 0.5f ? -2 : 0); PixelUi.Frame2(canvas, plate, Glows.Scaled(Gilt, 0.6 + 0.6 * counterPulse)); }
            PixelUi.Panel(canvas, Art, plate);
            var bag = Art.Icon("bag");
            if (bag.Valid) canvas.BlitRect(bag, new RectF(plate.X + 10, plate.CenterY - bag.H, bag.W * 2, bag.H * 2), Rgba.White);
            PixelUi.InkText(canvas, Art, "Reliquias", 13, Anchor.MidLeft, plate.X + 44, plate.CenterY - 1, Dim);
            var count = run.OwnedRelics.Count - (flyingRelic >= 0 ? 1 : 0);
            PixelUi.InkText(canvas, Art, count.ToString(), 20, Anchor.MidRight, plate.Right - 12, plate.CenterY - 1, Gilt);
        }

        public RectF MerchantRect => merchant.Rect(MerchantX, MerchantGround);

        /// <summary>El Mercader by his stall: warm floor light and contact shadow, the sprite and its halo, the lantern's glow.</summary>
        private void DrawMerchant(Canvas canvas)
        {
            if (!merchant.Valid) return;
            var r = MerchantRect; var flick = 0.85f + 0.15f * (float)Math.Sin(time * 7.3) * (float)Math.Sin(time * 3.1 + 1);
            var (lx, ly) = merchant.ToScreen(r, merchant.Anchor("lantern", (18, 61)));
            Glows.Draw(canvas, Art, lx, ly, 70, Glows.Scaled(new Rgba(90, 52, 18), flick));
            GroundShadow.Draw(canvas, Art, r.X + merchant.Feet.x * merchant.Scale, MerchantGround, merchant.Feet.w * merchant.Scale * 2.4f, merchant.Scale, 1f, new Rgba(52, 34, 16));
            merchant.Draw(canvas, pose.Anim, pose.Elapsed, r, flick);
            Glows.Draw(canvas, Art, lx, ly, 26, Glows.Scaled(new Rgba(255, 170, 70), 0.35f * flick));
        }

        /// <summary>The speech bubble: where it goes and the lines of the current sentence (tests: every line fits).</summary>
        public SpeechBubble.View Bubble(string text)
        {
            var r = MerchantRect;
            return SpeechBubble.Layout(Fonts.Get(13), text, BubbleMaxW, merchant.ToScreen(r, merchant.Mouth), r.Y + merchant.Top * merchant.Scale, 16, StallX - 12);
        }

        private void DrawBubble(Canvas canvas)
        {
            if (!speech.Visible) return;
            SpeechBubble.Draw(canvas, Fonts.Get(13), Bubble(speech.Text), speech.Shown, speech.Age, speech.Alpha);
        }

        private void DrawStall(Canvas canvas)
        {
            // warm light from the stall lamps on the wall behind the shelves
            Glows.Draw(canvas, Art, 640, 300, 420, new Rgba(58, 34, 12));
            var awning = ShopStall.AwningSprite(Art);
            canvas.Fill(new RectF(StallX + 8, AwningY + awning.H * 2 - 6, StallW - 16, 10), new Rgba(6, 3, 2, 90));
            canvas.BlitRect(awning, new RectF(StallX, AwningY, awning.W * 2, awning.H * 2), Rgba.White);
            var plank = ShopStall.PlankSprite(Art);
            foreach (var row in new[] { RelicRowY, PackRowY })
            {
                var y = ShelfY(row);
                canvas.Fill(new RectF(StallX + 6, y + 20, StallW - 12, 10), new Rgba(6, 3, 2, 110));
                canvas.BlitRect(plank, new RectF(StallX, y, plank.W * 2, plank.H * 2), Rgba.White);
            }
        }

        private void DrawOffer(Canvas canvas, OfferView v, string kind, int i)
        {
            var isHovered = hovered == (kind, i) && !Busy && !v.Sold;
            var ok = !v.Sold && v.Affordable;
            var dx = shakeAt.HasValue && shakeAt.Value.x == v.Rect.X && shakeAt.Value.y == v.Rect.Y ? (float)Math.Round(Math.Sin(shakeAt.Value.t * 60) * 3 * shakeAt.Value.t / 0.35f) * 2 : 0f;
            var lift = isHovered && ok ? -2 : 0;
            var r = ((RectF)v.Rect).Move(dx, lift);
            RectF M(RectF q) => q.Move(dx, lift);
            ChromaKind chroma = kind == "relic" ? relics[i].Chroma : packs[i].Chroma;
            if (chroma != ChromaKind.None && !v.Sold) ChromaFx.DrawHalo(canvas, Art, r.CenterX, r.CenterY, r.W, r.H, chroma, Game.Time, false, 8);
            if (isHovered) PixelUi.Frame2(canvas, r, ok ? new Rgba(255, 222, 140) : new Rgba(200, 140, 120));
            PixelUi.Panel(canvas, Art, r);
            canvas.Fill(r.Inflate(-16, -16), isHovered && ok ? new Rgba(120, 80, 30, 46) : new Rgba(60, 40, 20, 26));
            // the item: a relic on a velvet cushion in a lit niche, or the pack art
            if (kind == "relic") DrawNiche(canvas, M(v.Art), relics[i], v.Sold || flyingRelic == i);
            else if (risingPack != i) PackArt.Draw(canvas, Art, packs[i].Definition.Theme, (int)v.Art.H, M(v.Art).CenterX, M(v.Art).CenterY, packs[i].Chroma, Game.Time, v.Sold ? 0.35f : 1f, v.Art.W);
            // name and tags
            PixelUi.InkText(canvas, Art, v.Title, 16, Anchor.MidLeft, M(v.Name).X, M(v.Name).CenterY - 1, v.Sold ? Dim : chroma != ChromaKind.None ? new Rgba(255, 226, 140) : Cream, false, true);
            foreach (var (text, color, gem, rect) in v.Tags)
            {
                var t = M(rect);
                PixelUi.PixelBox(canvas, t, Glows.Scaled(color, 0.2).WithAlpha(235), Glows.Scaled(color, 0.82), Glows.Scaled(color, 0.34).WithAlpha(235));
                var tx = t.X + 8;
                if (gem > 0) { Gem(canvas, tx + 3, t.CenterY, color); tx += 12; }
                PixelUi.InkText(canvas, Art, text, 13, Anchor.MidLeft, tx, t.CenterY - 1, Glows.Scaled(color, 1.32));
            }
            // rule text, colour-coded
            DitherRule(canvas, r.X + Inset, r.Y + DescTop - 8, r.W - 2 * Inset, new Rgba(110, 88, 60));
            var y = M(v.Text).Y;
            foreach (var line in v.Lines) { line.Draw(canvas, M(v.Text).X, y, v.Sold ? 0.45f : 1f); y += DescLine; }
            foreach (var line in v.Plain) { Fonts.Get(13).Draw(canvas, line, M(v.Text).X, y, v.Sold ? Dim : Cream); y += DescLine; }
            if (chroma != ChromaKind.None && !v.Sold)
            {
                var st = ChromaFx.Style(chroma); var pulse = 0.5 + 0.5 * Math.Sin(Game.Time * 2.2);
                PixelUi.Frame2(canvas, r.Inflate(-8, -8), Glows.Scaled(st.Main, 0.75 + 0.3 * pulse));
                FrameTwinkles(canvas, r, st.Bright, Game.Time + i * 1.3f);
            }
            // the price is the button
            if (v.Sold)
            {
                canvas.Fill(r.Inflate(-8, -8), new Rgba(8, 6, 12, 120));
                PixelUi.Ribbon(canvas, Art, M(v.Button).CenterX, M(v.Button).CenterY, "¡Vendido!", 16);
                return;
            }
            var b = M(v.Button);
            var state = !ok || Busy ? ButtonState.Off : isHovered ? ButtonState.Hover : ButtonState.Idle;
            PixelUi.Button(canvas, Art, b, "", chroma != ChromaKind.None ? "gold" : "bronze", state, null, null, time + i * 0.7f);
            var shown = b.Move(0, state == ButtonState.Hover ? -1 : 0);
            var (label, size, shortBy, was, width) = PriceLine(v.Price, v.BasePrice, run.Gold, b.W - 20);
            var x = (float)Math.Floor(shown.CenterX - width / 2f); var cy = shown.CenterY - 1;
            if (was != null)
            {
                var wr = PixelUi.InkText(canvas, Art, was, 13, Anchor.MidLeft, x, cy, new Rgba(170, 150, 130));
                canvas.Fill(new RectF(wr.X + 2, (float)Math.Floor(wr.CenterY), wr.W - 4, 2), Short);
                x = wr.Right + 8;
            }
            var coin = Art.Icon("coin");
            if (coin.Valid && v.Price > 0) { canvas.BlitRect(coin, new RectF(x, (float)Math.Floor(cy) - coin.H, coin.W * 2, coin.H * 2), state == ButtonState.Off ? new Rgba(150, 150, 150) : Rgba.White); x += 32; }
            var pr = PixelUi.InkText(canvas, Art, label, size, Anchor.MidLeft, x, cy, state == ButtonState.Off ? new Rgba(196, 190, 184) : Cream);
            if (shortBy != null) PixelUi.InkText(canvas, Art, shortBy, 13, Anchor.MidLeft, pr.Right + 8, cy, Short);
        }

        private static readonly (float u, float v, float ph)[] FrameStars = { (0f, 0f, 0f), (1f, 0.3f, 1.7f), (0.32f, 1f, 3.1f), (0f, 0.72f, 4.4f), (0.74f, 0f, 2.3f), (1f, 1f, 5.2f) };

        /// <summary>Four-point stars that come and go on the frame of golden offers (never over the text).</summary>
        private static void FrameTwinkles(Canvas canvas, RectF r, Rgba color, float t)
        {
            foreach (var (u, v, ph) in FrameStars)
            {
                var k = Math.Sin(t * 2.4 + ph); if (k < 0.2) continue;
                var arm = 2 * (1 + (int)(k * 2.5));
                var cx = (float)Math.Floor((r.X + 1 + (r.W - 4) * u) / 2) * 2; var cy = (float)Math.Floor((r.Y + 1 + (r.H - 4) * v) / 2) * 2;
                canvas.Fill(new RectF(cx - arm, cy, 2 * arm + 2, 2), color); canvas.Fill(new RectF(cx, cy - arm, 2, 2 * arm + 2), color);
                canvas.Fill(new RectF(cx, cy, 2, 2), Rgba.White);
            }
        }

        private static void DitherRule(Canvas canvas, float x, float y, float w, Rgba color)
        {
            for (var px = (int)x; px < x + w - 1; px += 4) canvas.Fill(new RectF(px, (float)Math.Floor(y / 2) * 2, 2, 2), color);
        }

        private static void Gem(Canvas canvas, float cx, float cy, Rgba c)
        {
            float x = (float)Math.Floor(cx / 2) * 2 - 4, y = (float)Math.Floor(cy / 2) * 2 - 4;
            canvas.Fill(new RectF(x + 2, y, 4, 8), Ink); canvas.Fill(new RectF(x, y + 2, 8, 4), Ink);
            canvas.Fill(new RectF(x + 2, y + 2, 4, 4), c); canvas.Fill(new RectF(x + 2, y + 2, 2, 2), Glows.Scaled(c, 1.5));
        }

        /// <summary>The lit niche of a relic: dark box, rarity-coloured light, a velvet cushion, the relic bobbing on it.</summary>
        private void DrawNiche(Canvas canvas, RectF n, RelicOffer offer, bool empty)
        {
            var rarity = Math.Max(1, Math.Min(5, offer.Definition.Rarity)); var rc = CardWidget.RarityColor(rarity);
            PixelUi.PixelBox(canvas, n, new Rgba(14, 10, 18), Ink, new Rgba(34, 26, 40));
            canvas.PushClip(n.Inflate(-4, -4));
            Glows.Draw(canvas, Art, n.CenterX, n.CenterY + 4, 46, Glows.Scaled(rc, empty ? 0.12 : 0.38 + 0.08 * Math.Sin(time * 2.0)));
            canvas.PopClip();
            var cushion = ShopStall.CushionSprite(Art);
            canvas.BlitRect(cushion, new RectF((float)Math.Floor(n.CenterX - cushion.W), n.Bottom - 6 - cushion.H * 2, cushion.W * 2, cushion.H * 2), empty ? new Rgba(120, 120, 120) : Rgba.White);
            if (empty) return;
            var bob = (float)Math.Round(Math.Sin(time * 1.8 + n.X * 0.01) * 1.2) * 2;
            RelicViewer.DrawRelicIcon(canvas, Art, offer.Definition.Name, 64, n.CenterX, n.CenterY - 6 + bob);
            if (offer.Chroma != ChromaKind.None) ChromaFx.Sheen(canvas, n.Inflate(-4, -4), offer.Chroma, Game.Time);
        }

        private static float Ease(float t) { t = t < 0 ? 0 : t > 1 ? 1 : t; return 1 - (1 - t) * (1 - t) * (1 - t); }

        /// <summary>A bought relic flying to the counter; a bought pack rising to the middle of the screen before it opens.</summary>
        private void DrawFlights(Canvas canvas)
        {
            if (flyingRelic >= 0)
            {
                var from = RelicView(flyingRelic).Art; var u = Ease(flyT / FlyT);
                float x0 = from.CenterX, y0 = from.CenterY - 6, x1 = RelicCounter.X + 22, y1 = RelicCounter.CenterY;
                var x = x0 + (x1 - x0) * u; var y = y0 + (y1 - y0) * u - 90 * (float)Math.Sin(Math.PI * u);
                var size = (int)Math.Round((64 - 34 * u) / 2) * 2;
                Glows.Draw(canvas, Art, x, y, 40, Glows.Scaled(CardWidget.RarityColor(relics[flyingRelic].Definition.Rarity), 0.6));
                RelicViewer.DrawRelicIcon(canvas, Art, relics[flyingRelic].Definition.Name, size, x, y);
            }
            if (risingPack >= 0)
            {
                var v = PackView(risingPack); var u = Ease(riseT / PackRiseT);
                var x = v.Art.CenterX + (640 - v.Art.CenterX) * u; var y = v.Art.CenterY + (380 - v.Art.CenterY) * u;
                var h = (int)(v.Art.H * (1 + 0.45f * u));
                canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(6, 3, 2, (int)(150 * u)));
                Glows.Draw(canvas, Art, x, y, 120 + 60 * u, Glows.Scaled(Themes[packs[risingPack].Definition.Theme].color, 0.35 + 0.35 * u));
                PackArt.Draw(canvas, Art, packs[risingPack].Definition.Theme, h, x, y, packs[risingPack].Chroma, Game.Time, 1f, v.Art.W);
            }
        }
    }

    /// <summary>
    /// The stall of the shop, drawn by code in native pixels (shown ×2, docs/art-style.md): a crimson and bone striped awning
    /// on a wooden rod with a scalloped, gold-hemmed edge; oak shelves with a lit top face, grain, nails and iron brackets; and
    /// the small velvet cushion the relics rest on. Light from the upper left, Bayer dither, one ink outline.
    /// </summary>
    public static class ShopStall
    {
        public const int AwningW = ShopScene.StallW / 2, AwningH = 22, PlankW = ShopScene.StallW / 2, PlankH = 14, Stripe = 16;
        private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
        private static float B(int x, int y) => (Bayer[y & 3, x & 3] + 0.5f) / 16f;
        private static readonly Rgba Ink = new Rgba(7, 5, 12);
        private static readonly Rgba[] Crimson = { new Rgba(48, 12, 20), new Rgba(82, 20, 30), new Rgba(120, 32, 42), new Rgba(160, 50, 54), new Rgba(196, 78, 72) };
        private static readonly Rgba[] Bone = { new Rgba(86, 74, 60), new Rgba(130, 116, 92), new Rgba(176, 160, 126), new Rgba(214, 198, 160), new Rgba(236, 224, 192) };
        private static readonly Rgba[] Wood = { new Rgba(40, 24, 14), new Rgba(70, 42, 24), new Rgba(104, 66, 36), new Rgba(140, 94, 54), new Rgba(178, 128, 76), new Rgba(210, 164, 106) };
        private static readonly Rgba[] Iron = { new Rgba(36, 34, 46), new Rgba(64, 62, 78), new Rgba(98, 96, 116) };
        private static readonly Rgba Hem = new Rgba(216, 168, 74), HemDark = new Rgba(150, 104, 40);

        public static Sprite AwningSprite(Art art) => new Sprite(art.Procedural("shop_awning", AwningW, AwningH, Awning));
        public static Sprite PlankSprite(Art art) => new Sprite(art.Procedural("shop_plank", PlankW, PlankH, Plank));
        public static Sprite CushionSprite(Art art) => new Sprite(art.Procedural("shop_cushion", 30, 8, Cushion));

        private static bool InCloth(int x, int y)
        {
            if (x < 0 || x >= AwningW || y < 4 || y >= AwningH) return false;
            if (y < 14) return true;
            var cx = (x / Stripe) * Stripe + Stripe / 2f - 0.5f; var dx = x - cx; var dy = y - 13.5f;
            return dx * dx + dy * dy <= 7.6f * 7.6f;
        }

        /// <summary>The awning: rod (rows 0–3) and striped cloth with scallops (rows 4–21).</summary>
        public static Rgba[] Awning()
        {
            var px = new Rgba[AwningW * AwningH];
            for (var y = 0; y < AwningH; y++)
                for (var x = 0; x < AwningW; x++)
                {
                    var i = y * AwningW + x;
                    if (y < 4)
                    {
                        px[i] = y == 0 || y == 3 ? Ink : y == 1 ? Wood[4] : Wood[2];
                        if (y == 1 && (x % 40 == 0 || x % 40 == 1)) px[i] = Wood[5];
                        continue;
                    }
                    if (!InCloth(x, y)) continue;
                    var s = x / Stripe; var sx = x % Stripe; var ramp = s % 2 == 0 ? Crimson : Bone;
                    var tone = 3f;
                    if (sx < 2) tone += 1; else if (sx >= Stripe - 2) tone -= 1;
                    if (y < 7) tone -= (7 - y) * 0.45f;                 // shadow under the rod
                    if (y > 15) tone -= 0.6f;
                    tone += (B(x, y) - 0.5f) * 0.9f;
                    px[i] = ramp[Math.Max(0, Math.Min(4, (int)Math.Round(tone)))];
                }
            // gold hem along the scallops, then one ink outline under the cloth
            var outline = new List<int>(); var hem = new List<int>();
            for (var y = 4; y < AwningH; y++)
                for (var x = 0; x < AwningW; x++)
                {
                    if (!InCloth(x, y)) { if (InCloth(x, y - 1) || InCloth(x - 1, y) || InCloth(x + 1, y)) outline.Add(y * AwningW + x); continue; }
                    if (y >= 12 && (!InCloth(x, y + 1) || !InCloth(x, y + 2))) hem.Add(y * AwningW + x);
                }
            foreach (var i in hem) px[i] = (i % AwningW) % Stripe < Stripe / 2 ? Hem : HemDark;
            foreach (var i in outline) px[i] = Ink;
            for (var y = 4; y < AwningH; y++) { if (InCloth(0, y)) px[y * AwningW] = Ink; if (InCloth(AwningW - 1, y)) px[y * AwningW + AwningW - 1] = Ink; }
            return px;
        }

        private static bool Bracket(int x, int y)
        {
            if (y < 10) return false;
            foreach (var bx in new[] { 22, PlankW - 30 })
            {
                var lx = x - bx; if (lx < 0 || lx >= 8) continue;
                return lx < 8 - (y - 10) * 2;
            }
            return false;
        }

        /// <summary>A shelf: lit top face, grained front, nails, iron brackets under it.</summary>
        public static Rgba[] Plank()
        {
            var px = new Rgba[PlankW * PlankH];
            for (var y = 0; y < PlankH; y++)
                for (var x = 0; x < PlankW; x++)
                {
                    var i = y * PlankW + x;
                    if (y >= 10) { if (Bracket(x, y)) px[i] = Bracket(x, y - 1) && Bracket(x - 1, y) && Bracket(x + 1, y) ? Iron[(x + y) % 3 == 0 ? 2 : 1] : Ink; continue; }
                    if (y == 0 || y == 9 || x == 0 || x == PlankW - 1) { px[i] = Ink; continue; }
                    if (y == 1) { px[i] = Wood[5]; continue; }
                    if (y <= 3) { px[i] = Wood[4]; continue; }
                    if (y == 4) { px[i] = Wood[1]; continue; }
                    // the front: grain lines that drift along the board
                    var grain = Math.Sin(x * 0.11 + y * 1.7 + Math.Sin(x * 0.023) * 3);
                    var tone = 2.6 + grain * 0.6 + (B(x, y) - 0.5) * 0.8 - (y - 5) * 0.15;
                    px[i] = Wood[Math.Max(1, Math.Min(3, (int)Math.Round(tone)))];
                    if (x % 68 == 34 && y == 6) px[i] = Bone[4];
                    if (x % 68 == 35 && y == 6 || x % 68 == 34 && y == 7) px[i] = Wood[0];
                }
            return px;
        }

        /// <summary>The velvet cushion under a relic (30×8 native).</summary>
        public static Rgba[] Cushion()
        {
            const int w = 30, h = 8; var px = new Rgba[w * h];
            bool In(int x, int y) { var dx = (x + 0.5f - w / 2f) / (w / 2f); var dy = (y + 0.5f - h / 2f) / (h / 2f); return dx * dx + dy * dy <= 1f; }
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    if (!In(x, y)) continue;
                    var edge = !In(x - 1, y) || !In(x + 1, y) || !In(x, y - 1) || !In(x, y + 1);
                    var tone = 3 - y * 0.45f + (x < w / 2 ? 0.4f : -0.2f) + (B(x, y) - 0.5f) * 0.7f;
                    px[y * w + x] = edge ? Ink : Crimson[Math.Max(0, Math.Min(4, (int)Math.Round(tone)))];
                    if (!edge && y == 2 && x > 8 && x < 14) px[y * w + x] = Crimson[4];
                }
            return px;
        }
    }
}
