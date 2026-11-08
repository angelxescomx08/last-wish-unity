using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>Treasure room (2026-11-07 redesign): every relic fits its card in every chroma and with 1–4 relics on offer, the
    /// room's boxes never overlap, the chest opens and the relics rise before you can choose (a click only hurries the show),
    /// picking and taking work with the mouse and the keys, the scene manager gives the taken relic once, an empty chest only
    /// lets you continue, Pico de Minero / Ganzúa show their gold, every phase draws, and the chest art keeps the style rules.</summary>
    public sealed class TreasureLayoutTests
    {
        private static Art art;
        private static NullRender render;
        private static GameData data;

        [SetUp] public void ResetTuning() => Tuning.Reset();
        [TearDown] public void RestoreTuning() => Tuning.Reset();

        private static GameContext Game(bool withData = true)
        {
            if (art == null) { render = new NullRender(); art = new Art(render, new FileAssets()); art.LoadCore(); data = new GameData(new FileAssets()); }
            return new GameContext(new Canvas(render), art, new SoundPlayer(new SilentAudio(), .7f, 0f), new Preferences(), null) { Data = withData ? data : null };
        }

        private static RunState Run(int seed = 5) => new RunState(HeroKind.Rogue, seed);
        private static TreasureScene Room(RunState run, IEnumerable<RelicOffer> offers, bool withData = true) => new TreasureScene(Game(withData), run, offers.ToList());
        private static RelicOffer Offer(string id, ChromaKind chroma = ChromaKind.None) => new RelicOffer(RunContent.RelicById(id), chroma);
        private static List<RelicOffer> Three() => new List<RelicOffer> { Offer("r_vial"), Offer("r_clover", ChromaKind.Golden), Offer("r_interest") };

        private static bool Inside(RectF inner, RectF outer) => inner.X >= outer.X - 0.01f && inner.Y >= outer.Y - 0.01f && inner.Right <= outer.Right + 0.01f && inner.Bottom <= outer.Bottom + 0.01f;
        private static bool Overlap(RectF a, RectF b) => a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
        private static string Words(string text) => new string((text ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray());
        private static string Shown(RelicCardView v) => Words(string.Concat(v.Lines.SelectMany(l => l.Runs.Select(r => r.text))) + string.Concat(v.Plain));

        private static void Run(TreasureScene room, float seconds, float step = 0.05f) { for (var t = 0f; t < seconds; t += step) room.Update(step); }
        private static void Click(TreasureScene room, RectF r) => room.HandleEvent(new InputEvent(EventType.MouseDown, r.CenterX, r.CenterY, 1));
        private static void Move(TreasureScene room, RectF r) => room.HandleEvent(new InputEvent(EventType.MouseMove, r.CenterX, r.CenterY));
        private static void Press(TreasureScene room, Key key) => room.HandleEvent(new InputEvent(EventType.KeyDown, key: key));
        private static TreasureScene Ready(RunState run, List<RelicOffer> offers) { var room = Room(run, offers); room.SkipIntro(); return room; }

        private static void Draw(GameContext game, TreasureScene room, int frames = 1)
        {
            for (var k = 0; k < frames; k++) { room.Update(0.05f); game.Time += 0.05f; game.Canvas.BeginFrame(); room.Draw(game.Canvas); game.Canvas.EndFrame(); }
        }

        [Test] public void EveryRelicFitsItsCardInEveryChromaAndCount()
        {
            var run = Run();
            foreach (var def in RunContent.Relics)
                foreach (var chroma in new[] { ChromaKind.None, ChromaKind.Golden, ChromaKind.Diamond })
                    for (var n = 1; n <= 4; n++)
                    {
                        var lay = Room(run, Enumerable.Range(0, n).Select(_ => new RelicOffer(def, chroma))).Layout();
                        Assert.That(lay.Cards.Count, Is.EqualTo(n));
                        foreach (var v in lay.Cards)
                        {
                            var label = def.Name + " " + chroma + " ×" + n + " #" + v.Index;
                            var card = v.Rect.Inflate(-2 * TreasureScene.Inset, -2 * TreasureScene.Inset);
                            Assert.That(v.Title, Is.EqualTo(def.Name), label);
                            Assert.That(Shown(v), Is.EqualTo(Words(def.Description)), label + ": every word");
                            Assert.That(Inside(v.Niche, card), Is.True, label + ": niche");
                            Assert.That(Inside(v.Name, card), Is.True, label + ": whole name " + v.Name);
                            Assert.That(v.Name.Y, Is.GreaterThanOrEqualTo(v.Niche.Bottom), label + ": name under the niche");
                            Assert.That(v.Tags[0].text, Is.EqualTo(RunContent.RarityLabel(Math.Max(1, Math.Min(5, def.Rarity)))), label + ": rarity first");
                            if (chroma != ChromaKind.None) Assert.That(v.Tags.Any(tg => tg.text.StartsWith(ChromaFx.Style(chroma).Name, StringComparison.Ordinal)), Is.True, label + ": chroma tag");
                            foreach (var tg in v.Tags)
                            {
                                Assert.That(Inside(tg.rect, card), Is.True, label + ": tag " + tg.text);
                                Assert.That(tg.rect.Y, Is.EqualTo(v.Tags[0].rect.Y), label + ": tags in one row");
                                Assert.That(tg.rect.Y, Is.GreaterThanOrEqualTo(v.Name.Bottom), label + ": tags under the name");
                                Assert.That(tg.rect.Bottom, Is.LessThanOrEqualTo(v.Rect.Y + TreasureScene.RuleY - 4), label + ": tags over the rule");
                            }
                            for (var k = 1; k < v.Tags.Count; k++) Assert.That(v.Tags[k].rect.X, Is.GreaterThanOrEqualTo(v.Tags[k - 1].rect.Right), label + ": tags apart");
                            Assert.That(v.LineCount, Is.GreaterThanOrEqualTo(1), label);
                            Assert.That(v.Lines.All(l => l.Width <= v.Text.W), Is.True, label + ": lines inside the column");
                            Assert.That(v.Text.Y + (v.LineCount - 1) * TreasureScene.Line + 16, Is.LessThanOrEqualTo(v.Rect.Bottom - 8), label + ": text inside the card");
                        }
                    }
        }

        [Test] public void EveryCardOfARoomHasTheSameNameSizeAndHeight()
        {
            var lay = Room(Run(), Three()).Layout();
            Assert.That(lay.Cards.Select(c => c.NameSize).Distinct().Count(), Is.EqualTo(1));
            Assert.That(lay.Cards.Select(c => c.Rect.H).Distinct().Count(), Is.EqualTo(1));
            Assert.That(lay.Cards.Select(c => c.Rect.Y).Distinct().Single(), Is.EqualTo(TreasureScene.CardTop));
            Assert.That(Room(Run(), new[] { Offer("r_vial") }).Layout().Cards[0].NameSize, Is.EqualTo(20), "a short name gets the chunky title face");
        }

        [Test] public void TheRoomsBoxesNeverOverlap()
        {
            var longest = RunContent.Relics.OrderByDescending(r => r.Description.Length).ToList();
            var bonusRun = Run(); bonusRun.AcquireRelic("miner_pick"); bonusRun.AcquireRelic("lockpick");
            var pause = PauseMenu.PauseButton; var stats = new RectF(pause.Right + 6, pause.Y, pause.W, pause.H);
            var counter = (RectF)TreasureScene.RelicCounter; var chest = TreasureScene.ChestRect;
            for (var n = 0; n <= 4; n++)
                foreach (var run in new[] { Run(), bonusRun })
                {
                    var label = n + " relics" + (run == bonusRun ? " + bonus" : "");
                    var lay = Room(run, longest.Take(n).Select(r => new RelicOffer(r, ChromaKind.Golden))).Layout();
                    foreach (var c in lay.Cards)
                    {
                        Assert.That(c.Rect.X, Is.GreaterThanOrEqualTo(16), label); Assert.That(c.Rect.Right, Is.LessThanOrEqualTo(Canvas.Width - 16), label);
                        Assert.That(c.Rect.Y - 22, Is.GreaterThanOrEqualTo(96), label + ": room for the arrow under the ribbon");
                        Assert.That(c.Rect.Bottom, Is.LessThanOrEqualTo(chest.Y + TreasureChest.OpenLidTop * 2 - 16), label + ": cards over the open chest");
                        foreach (var o in lay.Cards) if (o != c) Assert.That(Overlap(c.Rect.Inflate(16, 16), o.Rect), Is.False, label + ": cards and their frames apart");
                        foreach (var r in new[] { pause, stats, counter }) Assert.That(Overlap(c.Rect, r), Is.False, label);
                    }
                    Assert.That(lay.Take.Y, Is.GreaterThanOrEqualTo(TreasureScene.ChestGround + 8), label + ": buttons under the chest");
                    Assert.That(lay.Take.Bottom, Is.LessThanOrEqualTo(Canvas.Height - 16), label);
                    Assert.That(lay.HasSkip, Is.EqualTo(n > 0), label);
                    if (lay.HasSkip) Assert.That(Overlap(lay.Take, lay.Skip), Is.False, label);
                    Assert.That(lay.HasBonus, Is.EqualTo(run == bonusRun), label);
                    if (lay.HasBonus)
                    {
                        Assert.That(Inside(lay.Bonus, new RectF(0, 0, Canvas.Width, Canvas.Height)), Is.True, label);
                        foreach (var r in new[] { chest, lay.Take, lay.Skip }.Concat(lay.Cards.Select(c => c.Rect))) Assert.That(Overlap(lay.Bonus, r), Is.False, label + ": bonus " + r);
                    }
                    if (n == 0) Assert.That(Overlap(lay.Empty, chest), Is.False, label);
                }
        }

        [Test] public void TheChestOpensAndTheRelicsRiseBeforeYouChoose()
        {
            var room = Room(Run(), Three());
            Assert.That(room.State, Is.EqualTo(TreasureScene.Phase.Intro));
            Assert.That(room.LidFrame, Is.EqualTo(0));
            var seen = new List<int>();
            for (var t = 0f; t < TreasureScene.OpenAt + 0.1f; t += 0.02f) { room.Update(0.02f); if (seen.Count == 0 || seen[seen.Count - 1] != room.LidFrame) seen.Add(room.LidFrame); }
            Assert.That(seen, Is.EqualTo(new[] { 0, 1, 2, TreasureChest.Open }), "closed, ajar twice, open");
            Assert.That(room.State, Is.EqualTo(TreasureScene.Phase.Intro));
            Run(room, room.IntroLength);
            Assert.That(room.State, Is.EqualTo(TreasureScene.Phase.Choosing));
            Assert.That(room.Cleared, Is.False);
        }

        [Test] public void AClickOrAKeyDuringTheShowOnlyHurriesIt()
        {
            var room = Room(Run(), Three());
            Click(room, room.TakeRect);
            Assert.That(room.State, Is.EqualTo(TreasureScene.Phase.Choosing));
            Assert.That(room.TookRelic, Is.False); Assert.That(room.Cleared, Is.False);
            Assert.That(room.LidFrame, Is.EqualTo(TreasureChest.Open));
            var other = Room(Run(), Three());
            Press(other, Key.Return);
            Assert.That(other.State, Is.EqualTo(TreasureScene.Phase.Choosing));
            Assert.That(other.TookRelic, Is.False);
        }

        [Test] public void ClickingACardPicksItAndTakeTakesThatOne()
        {
            var run = Run(); var before = run.OwnedRelics.Count; var offers = Three(); var room = Ready(run, offers);
            Assert.That(room.Selected, Is.EqualTo(0));
            Click(room, room.CardRect(2));
            Assert.That(room.Selected, Is.EqualTo(2));
            Assert.That(room.State, Is.EqualTo(TreasureScene.Phase.Choosing), "picking is not taking");
            Click(room, room.TakeRect);
            Assert.That(room.State, Is.EqualTo(TreasureScene.Phase.Taking));
            Assert.That(room.TookRelic, Is.True);
            Assert.That(room.ChosenRelic, Is.SameAs(offers[2]));
            Assert.That(room.Cleared, Is.False, "the relic flies first");
            Run(room, TreasureScene.TakeEndT + 0.1f);
            Assert.That(room.Cleared, Is.True);
            Assert.That(room.LidFrame, Is.EqualTo(0), "the lid closed behind it");
            Assert.That(run.OwnedRelics.Count, Is.EqualTo(before), "the scene manager gives the relic, not the room");
        }

        [Test] public void KeysPickTakeAndSkip()
        {
            var offers = Three(); var room = Ready(Run(), offers);
            Press(room, Key.D3); Assert.That(room.Selected, Is.EqualTo(2));
            Press(room, Key.Right); Assert.That(room.Selected, Is.EqualTo(0), "wraps");
            Press(room, Key.Left); Assert.That(room.Selected, Is.EqualTo(2));
            Press(room, Key.Tab); Assert.That(room.Selected, Is.EqualTo(0));
            Press(room, Key.D2); Assert.That(room.Selected, Is.EqualTo(1));
            Press(room, Key.D9); Assert.That(room.Selected, Is.EqualTo(1), "no fourth relic");
            Press(room, Key.Return);
            Assert.That(room.ChosenRelic, Is.SameAs(offers[1]));
            var other = Ready(Run(), Three());
            Press(other, Key.O);
            Assert.That(other.State, Is.EqualTo(TreasureScene.Phase.Skipping));
            Assert.That(other.ChosenRelic, Is.Null);
        }

        [Test] public void SkippingLeavesEveryRelicAndAClickWhileLeavingEndsAtOnce()
        {
            var room = Ready(Run(), Three());
            Click(room, room.SkipRect.Value);
            Assert.That(room.State, Is.EqualTo(TreasureScene.Phase.Skipping));
            Run(room, TreasureScene.SkipEndT + 0.1f);
            Assert.That(room.Cleared, Is.True); Assert.That(room.TookRelic, Is.False); Assert.That(room.ChosenRelic, Is.Null);
            var fast = Ready(Run(), Three());
            Click(fast, fast.TakeRect); fast.Update(0.05f);
            Click(fast, fast.TakeRect);
            Assert.That(fast.Cleared, Is.True); Assert.That(fast.TookRelic, Is.True);
        }

        [Test] public void TheSceneManagerGivesTheTakenRelicOnce()
        {
            foreach (var take in new[] { true, false })
            {
                var game = Game(); var run = Run(); var offers = Three();
                var manager = new SceneManager(game, new MainMenuScene(game)); manager.StartRun(run);
                var before = run.OwnedRelics.Count;
                var room = new TreasureScene(game, run, offers); manager.Push(room);
                manager.HandleEvent(new InputEvent(EventType.MouseDown, 640, 300, 1));       // hurry the show
                manager.HandleEvent(new InputEvent(EventType.MouseDown, room.CardRect(1).CenterX, room.CardRect(1).CenterY, 1));
                var button = take ? room.TakeRect : room.SkipRect.Value;
                manager.HandleEvent(new InputEvent(EventType.MouseDown, button.CenterX, button.CenterY, 1));
                for (var t = 0f; t < TreasureScene.TakeEndT + 0.3f; t += 0.05f) manager.Update(0.05f);
                Assert.That(manager.Top, Is.InstanceOf<MapScene>(), "the room closed");
                Assert.That(run.OwnedRelics.Count, Is.EqualTo(before + (take ? 1 : 0)));
                if (take) Assert.That(run.OwnedRelics.Count(r => r.Definition.Id == "r_clover"), Is.EqualTo(1));
            }
        }

        [Test] public void AnEmptyChestOnlyLetsYouContinue()
        {
            var game = Game(); var room = new TreasureScene(game, Run(), new List<RelicOffer>());
            var lay = room.Layout();
            Assert.That(lay.Cards, Is.Empty); Assert.That(lay.HasSkip, Is.False); Assert.That(room.SkipRect, Is.Null);
            Assert.That(lay.TakeLabel, Is.EqualTo("Continuar"));
            Assert.That(lay.Ribbon, Is.EqualTo("El cofre está vacío"));
            Draw(game, room, 30);
            Assert.That(room.State, Is.EqualTo(TreasureScene.Phase.Choosing));
            Press(room, Key.O); Assert.That(room.Cleared, Is.False, "nothing to skip");
            Press(room, Key.Return);
            Assert.That(room.Cleared, Is.True); Assert.That(room.TookRelic, Is.False); Assert.That(room.ChosenRelic, Is.Null);
            var nulls = new TreasureScene(game, Run(), null);
            Assert.That(nulls.Relics, Is.Empty);
        }

        [Test] public void TheRibbonSaysHowManyRelicsThereAre()
        {
            Assert.That(Room(Run(), new[] { Offer("r_vial") }).RibbonText, Is.EqualTo("Una reliquia te espera"));
            Assert.That(Room(Run(), Three()).RibbonText, Is.EqualTo("Elige una de las 3 reliquias"));
        }

        [Test] public void PicoDeMineroAndGanzuaShowTheGoldTheyGave()
        {
            Assert.That(Room(Run(), Three()).BonusGold, Is.EqualTo(0));
            var run = Run(); run.AcquireRelic("miner_pick");
            Assert.That(Room(run, Three()).BonusGold, Is.EqualTo(25));
            run.AcquireRelic("lockpick");
            var room = Room(run, Three());
            Assert.That(room.BonusGold, Is.EqualTo(45));
            Assert.That(room.BonusSources, Does.Contain("Pico de Minero").And.Contain("Ganzúa"));
            var golden = Run(); golden.AcquireRelic("miner_pick", ChromaKind.Golden);
            Assert.That(Room(golden, Three()).BonusGold, Is.EqualTo(50), "golden: x2, like the gold the room paid");
        }

        [Test] public void OnlyTooltipsThatAddSomethingShowAndNeverSayEstado()
        {
            var plain = RunContent.Relics.First(r => r.ClassId == "neutral" && Tooltip.Relic(Game(), r, ChromaKind.None).Panels.Count == 0);
            var room = Ready(Run(), new List<RelicOffer> { Offer("r_vial"), new RelicOffer(plain) });
            Assert.That(room.HoverTooltip(), Is.Null, "nothing hovered");
            Move(room, room.CardRect(0));
            var tip = room.HoverTooltip();
            Assert.That(tip, Is.Not.Null, "Veneno and the class are not on the card");
            Assert.That(tip.Lines.Any(l => l.StartsWith("Estado", StringComparison.Ordinal)), Is.False);
            Move(room, room.CardRect(1));
            Assert.That(room.HoverTooltip(), Is.Null, plain.Name + ": the card already says everything");
        }

        [Test] public void EveryPhaseDraws()
        {
            var longest = RunContent.Relics.OrderByDescending(r => r.Description.Length).ToList();
            foreach (var withData in new[] { true, false })
                for (var n = 0; n <= 4; n++)
                    foreach (var take in new[] { true, false })
                    {
                        var game = Game(withData); var run = Run(); run.AcquireRelic("miner_pick");
                        var offers = longest.Take(n).Select((r, i) => new RelicOffer(r, (ChromaKind)(i % 3))).ToList();
                        var room = new TreasureScene(game, run, offers);
                        Assert.DoesNotThrow(() =>
                        {
                            for (var k = 0; k < 40; k++) Draw(game, room);
                            for (var i = 0; i < n; i++) { Move(room, room.CardRect(i)); Draw(game, room, 2); Click(room, room.CardRect(i)); Draw(game, room); }
                            Move(room, room.TakeRect); Draw(game, room, 2);
                            if (take || n == 0) Click(room, room.TakeRect); else Click(room, room.SkipRect.Value);
                            for (var k = 0; k < 30; k++) Draw(game, room);
                        }, n + " relics, take " + take + ", data " + withData);
                        Assert.That(room.Cleared, Is.True);
                    }
        }

        [Test] public void TheChestArtKeepsTheStyleRules()
        {
            const int w = TreasureChest.W, h = TreasureChest.H;
            var frames = Enumerable.Range(0, TreasureChest.Frames).Select(TreasureChest.Paint).ToList();
            for (var f = 0; f < frames.Count; f++)
            {
                var (paint, light) = frames[f];
                bool Solid(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && paint[y * w + x].A > 0;
                foreach (var (x, y) in new[] { (0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1) }) Assert.That(Solid(x, y), Is.False, "frame " + f + ": transparent corner");
                for (var y = 0; y < h; y++)
                    for (var x = 0; x < w; x++)
                    {
                        var i = y * w + x;
                        if (!Solid(x, y)) { Assert.That(light[i].R + light[i].G + light[i].B, Is.EqualTo(0), "frame " + f + ": light only on paint"); continue; }
                        Assert.That(paint[i].A, Is.EqualTo(255), "frame " + f + ": opaque paint");
                        if (!Solid(x - 1, y) || !Solid(x + 1, y) || !Solid(x, y - 1) || !Solid(x, y + 1))
                            Assert.That(paint[i].Equals(TreasureChest.Ink), Is.True, "frame " + f + ": one ink outline at " + x + "," + y);
                        if (y > TreasureChest.Ground) Assert.Fail("frame " + f + ": nothing under the ground line");
                    }
                // one region
                var seen = new bool[w * h]; var stack = new Stack<int>(); var start = Array.FindIndex(paint, p => p.A > 0); stack.Push(start); seen[start] = true; var count = 0;
                while (stack.Count > 0)
                {
                    var i = stack.Pop(); count++; int x = i % w, y = i / w;
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        if (Solid(x + dx, y + dy) && !seen[(y + dy) * w + x + dx]) { seen[(y + dy) * w + x + dx] = true; stack.Push((y + dy) * w + x + dx); }
                }
                Assert.That(count, Is.EqualTo(paint.Count(p => p.A > 0)), "frame " + f + ": one silhouette");
                Assert.That(Solid(TreasureChest.Left + 2, TreasureChest.Ground - 1), Is.True, "frame " + f + ": feet on the ground");
            }
            // the body never moves; only the lid and the light change
            for (var f = 1; f < frames.Count; f++)
                for (var y = TreasureChest.BodyTop + 2; y < h; y++)
                    for (var x = 0; x < w; x++)
                        Assert.That(frames[f].paint[y * w + x], Is.EqualTo(frames[0].paint[y * w + x]), "frame " + f + " body at " + x + "," + y);
            int Lit(int f) => frames[f].light.Count(l => l.R + l.G + l.B > 0);
            Assert.That(Lit(0), Is.EqualTo(0), "closed: no light");
            Assert.That(Lit(1), Is.GreaterThan(0)); Assert.That(Lit(2), Is.GreaterThan(Lit(1)), "the gap widens");
            Assert.That(Lit(TreasureChest.Open), Is.GreaterThan(Lit(2)), "open: the coins shine");
            Assert.That(TreasureChest.Paint(-5).paint, Is.EqualTo(frames[0].paint)); Assert.That(TreasureChest.Paint(99).paint, Is.EqualTo(frames[TreasureChest.Open].paint));
        }
    }
}
