using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>Shop interface (2026-11-04 redesign): every relic and pack fits its card (whole name, tags in one row over the
    /// price, every word of the rule text), prices never leave their button, the room's boxes never overlap, purchases charge
    /// once and animate (relic to the counter, pack rising before it opens) and every state draws (null renderer, real fonts).</summary>
    public sealed class ShopLayoutTests
    {
        private static Art art;
        private static NullRender render;
        private static GameData data;

        [SetUp] public void ResetTuning() => Tuning.Reset();
        [TearDown] public void RestoreTuning() => Tuning.Reset();

        private static GameContext Game()
        {
            if (art == null) { render = new NullRender(); art = new Art(render, new FileAssets()); art.LoadCore(); data = new GameData(new FileAssets()); }
            return new GameContext(new Canvas(render), art, new SoundPlayer(new SilentAudio(), .7f, 0f), new Preferences(), null) { Data = data };
        }

        private static void SetGold(RunState run, int gold) => typeof(RunState).GetProperty("Gold").SetValue(run, gold);
        private static RunState Run(int gold, HeroKind hero = HeroKind.Rogue, int seed = 5) { var run = new RunState(hero, seed); SetGold(run, gold); return run; }
        private static ShopScene Shop(RunState run, IList<PackOffer> packs = null, IList<RelicOffer> relics = null) => new ShopScene(Game(), run, packs, relics);

        private static bool Inside(RectF inner, RectF outer) => inner.X >= outer.X - 0.01f && inner.Y >= outer.Y - 0.01f && inner.Right <= outer.Right + 0.01f && inner.Bottom <= outer.Bottom + 0.01f;
        /// <summary>The letters of a text without its spaces (rich text splits punctuation into runs of its own).</summary>
        private static string Words(string text) => new string((text ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray());
        private static string Shown(ShopScene.OfferView v) => Words(string.Concat(v.Lines.SelectMany(l => l.Runs.Select(r => r.text))) + string.Concat(v.Plain));
        private static string Num(int n) => n.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");

        private static void AssertFits(ShopScene.OfferView v, string label)
        {
            var card = ((RectF)v.Rect).Inflate(-16, -16);
            Assert.That(Inside(v.Art, card), Is.True, label + ": art " + v.Art);
            Assert.That(Inside(v.Name, card), Is.True, label + ": name " + v.Name);
            Assert.That(Inside(v.Button, card), Is.True, label + ": button " + v.Button);
            Assert.That(v.Name.X, Is.GreaterThanOrEqualTo(v.Art.Right), label + ": name right of the art");
            Assert.That(v.Name.Bottom, Is.LessThanOrEqualTo(v.Tags.Min(t => t.rect.Y)), label + ": name over the tags");
            foreach (var t in v.Tags)
            {
                Assert.That(Inside(t.rect, card), Is.True, label + ": tag " + t.text);
                Assert.That(t.rect.Bottom, Is.LessThanOrEqualTo(v.Button.Y), label + ": tag " + t.text + " over the price");
                Assert.That(t.rect.Y, Is.EqualTo(v.Tags[0].rect.Y), label + ": tags in one row");
            }
            Assert.That(v.Text.Y, Is.GreaterThan(v.Button.Bottom), label + ": text under the price");
            var lines = v.Lines.Count + v.Plain.Count;
            Assert.That(lines, Is.InRange(1, 4), label);
            Assert.That(v.Text.Y + (lines - 1) * ShopScene.DescLine + 16, Is.LessThanOrEqualTo(v.Rect.Bottom - 8), label + ": text inside the card");
            Assert.That(v.Lines.All(l => l.Width <= v.Text.W), Is.True, label + ": lines inside the column");
        }

        [Test] public void EveryRelicFitsItsCardInEveryChroma()
        {
            var run = Run(200);
            foreach (var def in RunContent.Relics)
                foreach (var chroma in new[] { ChromaKind.None, ChromaKind.Golden, ChromaKind.Diamond })
                {
                    var shop = Shop(run, null, new[] { new RelicOffer(def, chroma), new RelicOffer(def, chroma), new RelicOffer(def, chroma) });
                    for (var i = 0; i < 3; i++)
                    {
                        var v = shop.RelicView(i); var label = def.Name + " " + chroma + " #" + i;
                        Assert.That(v.Title, Is.EqualTo(def.Name), label);
                        Assert.That(Shown(v), Is.EqualTo(Words(def.Description)), label + ": every word");
                        Assert.That(v.Tags.Count, Is.EqualTo(chroma == ChromaKind.None ? 1 : 2), label);
                        AssertFits(v, label);
                    }
                }
        }

        [Test] public void EveryPackFitsAndSaysHowManyCardsYouKeep()
        {
            foreach (var bag in new[] { false, true })
            {
                var run = Run(200);
                if (bag) run.AcquireRelic("bottomless_bag");
                foreach (var def in RunContent.Packs)
                    foreach (var chroma in new[] { ChromaKind.None, ChromaKind.Golden, ChromaKind.Diamond })
                    {
                        var shop = Shop(run, new[] { new PackOffer(def, chroma), new PackOffer(def, chroma), new PackOffer(def, chroma) });
                        var v = shop.PackView(1); var label = def.Name + " " + chroma + (bag ? " + bolsa" : "");
                        AssertFits(v, label);
                        StringAssert.Contains("cartas", Shown(v), label);
                        StringAssert.EndsWith(Words("te quedas con " + run.PackPicks(chroma) + "."), Shown(v), label);
                        Assert.That(v.BasePrice, Is.EqualTo(def.Cost), label);
                    }
            }
        }

        [Test] public void TheRoomBoxesNeverOverlapAndStayOnScreen()
        {
            var boxes = new List<(string name, RectF rect)> { ("relics", ShopScene.RelicCounter), ("pause", PauseMenu.PauseButton), ("exit", ShopScene.ExitButton) };
            for (var i = 0; i < 3; i++) { boxes.Add(("relic " + i, ShopScene.RelicRect(i))); boxes.Add(("pack " + i, ShopScene.PackRect(i))); }
            foreach (var (name, r) in boxes) Assert.That(Inside(r, new RectF(0, 0, Canvas.Width, Canvas.Height)), Is.True, name);
            for (var i = 0; i < boxes.Count; i++)
                for (var j = i + 1; j < boxes.Count; j++)
                    Assert.That(boxes[i].rect.Intersects(boxes[j].rect), Is.False, boxes[i].name + " / " + boxes[j].name);
            // the cards stand on their shelves, the awning is above the first row and the shelves are inside the stall
            var awningBottom = ShopScene.AwningY + ShopStall.AwningH * 2;
            Assert.That(awningBottom, Is.LessThanOrEqualTo(ShopScene.RelicRowY), "awning over the relics");
            Assert.That(ShopScene.AwningY, Is.GreaterThanOrEqualTo(96), "awning under the title band");
            Assert.That(ShopScene.ShelfY(ShopScene.RelicRowY) + ShopStall.PlankH * 2, Is.LessThanOrEqualTo(ShopScene.PackRowY), "first shelf over the packs");
            Assert.That(ShopScene.ShelfY(ShopScene.PackRowY) + ShopStall.PlankH * 2, Is.LessThanOrEqualTo(ShopScene.ExitButton.Y), "second shelf over the exit");
            Assert.That(ShopScene.RelicRect(0).X, Is.GreaterThan(ShopScene.StallX));
            Assert.That(ShopScene.RelicRect(2).Right, Is.LessThan(ShopScene.StallX + ShopScene.StallW));
        }

        [TestCase(0, 0, 0)] [TestCase(150, 150, 0)] [TestCase(75, 150, 0)] [TestCase(150, 150, 149)] [TestCase(150, 150, 150)]
        [TestCase(9999, 9999, 0)] [TestCase(123456, 150, 7)] [TestCase(1000000000, 1000000000, 0)] [TestCase(int.MaxValue, int.MaxValue, 0)]
        public void PricesNeverLeaveTheirButton(int price, int basePrice, int gold)
        {
            var shop = Shop(Run(gold));
            var room = shop.RelicView(0).Button.W - 20;
            var (label, size, shortBy, was, width) = shop.PriceLine(price, basePrice, gold, room);
            Assert.That(width, Is.LessThanOrEqualTo(room));
            if (price == 0) Assert.That(label, Is.EqualTo("¡Gratis!"));
            if (price > 0 && price <= 9999) StringAssert.StartsWith(Num(price), label);
            if (gold < price && price <= 9999) Assert.That(shortBy, Is.EqualTo("faltan " + Num(price - gold)));
            if (price < basePrice && basePrice <= 9999) Assert.That(was, Is.EqualTo(Num(basePrice)), "the old price struck through");
            if (price >= basePrice) Assert.That(was, Is.Null);
        }

        [Test] public void BuyingARelicChargesOnceAndFliesToTheCounter()
        {
            var run = Run(400); var shop = Shop(run);
            var game = Game(); var card = ShopScene.RelicRect(1); var price = shop.RelicPrice;
            shop.HandleEvent(new InputEvent(EventType.MouseDown, card.CenterX, card.CenterY, 1));
            Assert.That(run.Gold, Is.EqualTo(400 - price));
            Assert.That(run.OwnedRelics.Count, Is.EqualTo(1));
            Assert.That(shop.RelicSold(1), Is.True);
            shop.HandleEvent(new InputEvent(EventType.MouseDown, card.CenterX, card.CenterY, 1));
            Assert.That(run.Gold, Is.EqualTo(400 - price), "a sold relic is not charged again");
            Assert.That(run.OwnedRelics.Count, Is.EqualTo(1));
            for (var t = 0f; t < ShopScene.FlyT + 0.2f; t += 0.05f) { shop.Update(0.05f); game.Canvas.BeginFrame(); shop.Draw(game.Canvas); game.Canvas.EndFrame(); }
            Assert.That(shop.RelicView(1).Sold, Is.True);
        }

        [Test] public void APackRisesBeforeItOpensAndTheShopWaitsMeanwhile()
        {
            var run = Run(400); var shop = Shop(run);
            var card = ShopScene.PackRect(0); var price = shop.PackPrice(0);
            shop.HandleEvent(new InputEvent(EventType.MouseDown, card.CenterX, card.CenterY, 1));
            Assert.That(run.Gold, Is.EqualTo(400 - price));
            Assert.That(shop.PackSold(0), Is.True);
            Assert.That(shop.SelectedPack.HasValue, Is.False, "the pack rises first");
            Assert.That(shop.Busy, Is.True);
            var other = ShopScene.PackRect(1);
            shop.HandleEvent(new InputEvent(EventType.MouseDown, other.CenterX, other.CenterY, 1));
            shop.HandleEvent(new InputEvent(EventType.MouseDown, ShopScene.ExitButton.CenterX, ShopScene.ExitButton.CenterY, 1));
            Assert.That(shop.PackSold(1), Is.False, "no second purchase while the first pack rises");
            Assert.That(shop.Cleared, Is.False);
            for (var t = 0f; t < ShopScene.PackRiseT + 0.1f; t += 0.05f) shop.Update(0.05f);
            Assert.That(shop.SelectedPack, Is.EqualTo(shop.Packs[0].Definition.Theme));
            Assert.That(shop.Busy, Is.False);
        }

        [Test] public void TheSceneManagerOpensTheBoughtPack()
        {
            var game = Game(); var run = Run(400);
            var manager = new SceneManager(game, new MainMenuScene(game)); manager.StartRun(run);
            var shop = new ShopScene(game, run); manager.Push(shop);
            var card = ShopScene.PackRect(2);
            manager.HandleEvent(new InputEvent(EventType.MouseDown, card.CenterX, card.CenterY, 1));
            manager.Update(0.05f);
            Assert.That(manager.Top, Is.SameAs(shop));
            for (var t = 0f; t < ShopScene.PackRiseT + 0.2f; t += 0.05f) manager.Update(0.05f);
            Assert.That(manager.Top, Is.InstanceOf<PackOpeningScene>());
        }

        [Test] public void WithoutGoldNothingIsBoughtAndTheShortfallShows()
        {
            var run = Run(10); var shop = Shop(run);
            foreach (var r in new[] { ShopScene.RelicRect(0), ShopScene.PackRect(0) })
                shop.HandleEvent(new InputEvent(EventType.MouseDown, r.CenterX, r.CenterY, 1));
            Assert.That(run.Gold, Is.EqualTo(10));
            Assert.That(run.OwnedRelics.Count, Is.Zero);
            Assert.That(shop.RelicSold(0) || shop.PackSold(0), Is.False);
            Assert.That(shop.RelicView(0).Affordable, Is.False);
            var v = shop.RelicView(0);
            Assert.That(shop.PriceLine(v.Price, v.BasePrice, run.Gold, v.Button.W - 20).shortBy, Is.EqualTo("faltan " + (v.Price - 10)));
        }

        [Test] public void DiscountsShowTheOldPriceAndTheVoucherMakesOneRelicFree()
        {
            var run = Run(400); run.AcquireRelic("voucher"); run.AcquireRelic("member_card");
            var shop = Shop(run);
            var relic = shop.RelicView(0); var pack = shop.PackView(0);
            Assert.That(relic.Price, Is.Zero);
            Assert.That(shop.PriceLine(relic.Price, relic.BasePrice, run.Gold, relic.Button.W - 20).label, Is.EqualTo("¡Gratis!"));
            Assert.That(pack.Price, Is.LessThan(pack.BasePrice));
            Assert.That(shop.PriceLine(pack.Price, pack.BasePrice, run.Gold, pack.Button.W - 20).was, Is.EqualTo(pack.BasePrice.ToString()));
            var card = ShopScene.RelicRect(0);
            shop.HandleEvent(new InputEvent(EventType.MouseDown, card.CenterX, card.CenterY, 1));
            Assert.That(run.Gold, Is.EqualTo(400));
            Assert.That(shop.RelicView(1).Price, Is.GreaterThan(0), "the voucher is spent");
        }

        [Test] public void EveryStateDrawsWithHoverGoldenOffersAndFlights()
        {
            var game = Game();
            foreach (var (gold, seed, hero) in new[] { (0, 1, HeroKind.Warrior), (99999, 2, HeroKind.Mage), (300, 3, HeroKind.Rogue) })
            {
                var run = Run(gold, hero, seed);
                var golden = RunContent.Relics.Take(3).Select(d => new RelicOffer(d, ChromaKind.Golden)).ToList();
                var packs = RunContent.Packs.Take(3).Select((p, k) => new PackOffer(p, k == 1 ? ChromaKind.Diamond : ChromaKind.None)).ToList();
                foreach (var shop in new[] { Shop(run), Shop(run, packs, golden) })
                {
                    void Frames(int n) { for (var k = 0; k < n; k++) { shop.Update(0.05f); game.Canvas.BeginFrame(); shop.Draw(game.Canvas); game.Canvas.EndFrame(); } }
                    for (var i = 0; i < 3; i++)
                        foreach (var r in new[] { ShopScene.RelicRect(i), ShopScene.PackRect(i) })
                        { shop.HandleEvent(new InputEvent(EventType.MouseMove, r.CenterX, r.CenterY)); Frames(2); }
                    shop.HandleEvent(new InputEvent(EventType.MouseMove, ShopScene.ExitButton.CenterX, ShopScene.ExitButton.CenterY)); Frames(2);
                    for (var i = 0; i < 3; i++)
                    {
                        var r = ShopScene.RelicRect(i); shop.HandleEvent(new InputEvent(EventType.MouseDown, r.CenterX, r.CenterY, 1)); Frames(4);
                        var p = ShopScene.PackRect(i); shop.HandleEvent(new InputEvent(EventType.MouseDown, p.CenterX, p.CenterY, 1)); Frames(14);
                        shop.SelectedPack = null;
                    }
                    Frames(20);
                }
            }
        }

        [Test] public void TheStallArtFollowsTheStyleRules()
        {
            var awning = ShopStall.Awning(); var plank = ShopStall.Plank(); var cushion = ShopStall.Cushion();
            Assert.That(awning.Length, Is.EqualTo(ShopStall.AwningW * ShopStall.AwningH));
            Assert.That(plank.Length, Is.EqualTo(ShopStall.PlankW * ShopStall.PlankH));
            Assert.That(ShopStall.AwningW * 2, Is.EqualTo(ShopScene.StallW), "shown ×2 over the whole stall");
            // the rod is solid, the scallops leave gaps at the bottom between stripes, every pixel is opaque or empty
            for (var x = 0; x < ShopStall.AwningW; x++) Assert.That(awning[x].A, Is.EqualTo(255), "rod " + x);
            Assert.That(Enumerable.Range(0, ShopStall.AwningW).Count(x => awning[(ShopStall.AwningH - 1) * ShopStall.AwningW + x].A == 0), Is.GreaterThanOrEqualTo(ShopStall.AwningW / 3), "scallops");
            foreach (var px in awning.Concat(plank).Concat(cushion)) Assert.That(px.A == 0 || px.A == 255, Is.True);
            Assert.That(awning.Count(p => p.A == 255 && p.R == 7 && p.G == 5 && p.B == 12), Is.GreaterThan(ShopStall.AwningW), "one ink outline");
        }
    }
}
