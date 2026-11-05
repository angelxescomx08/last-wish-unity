using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>El Brujo's room (redesigned 2026-11-04): El Brujo's sheet (Tools/VfxLab/warlock.html) loads with its six
    /// animations and the cast event, he and his cauldron stand left of the deck, every line he can say fits its bubble,
    /// every card of the game fits its tile and its confirmation panel, the price list follows the domain (and the Martillo
    /// del Herrero), picking, confirming, cancelling, refusing and the upgrade show work with the mouse and the keys, he
    /// reacts (greet, cast, no, ponder) and every state draws (null renderer, real fonts and assets).</summary>
    public sealed class WarlockLayoutTests
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
        private static RunState Run(int gold, HeroKind hero = HeroKind.Warrior, int seed = 5) { var run = new RunState(hero, seed); SetGold(run, gold); return run; }
        private static WarlockScene Room(RunState run) => new WarlockScene(Game(), run);
        private static void Click(WarlockScene s, RectF r) => s.HandleEvent(new InputEvent(EventType.MouseDown, r.CenterX, r.CenterY, 1));
        private static void Key_(WarlockScene s, Key k) => s.HandleEvent(new InputEvent(EventType.KeyDown, key: k));
        private static void Tick(WarlockScene s, float seconds, float dt = 1 / 60f) { for (var t = 0f; t < seconds - 1e-4f; t += dt) s.Update(dt); }
        private static bool Inside(RectF a, RectF b) => a.X >= b.X && a.Y >= b.Y && a.Right <= b.Right && a.Bottom <= b.Bottom;
        private static bool Overlap(RectF a, RectF b) => a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

        [Test] public void TheSheetLoadsAndPlacesHimAndHisCauldronLeftOfTheDeck()
        {
            var room = Room(Run(100)); var sheet = room.Npc;
            Assert.That(sheet.Valid, Is.True, "Resources/Game/Warlock/warlock.json + warlock_96.png");
            var expected = new Dictionary<string, (int frames, bool loop)> { ["idle"] = (24, true), ["talk"] = (12, true), ["greet"] = (14, false), ["cast"] = (18, false), ["no"] = (14, false), ["ponder"] = (24, false) };
            Assert.That(sheet.Clips.Keys, Is.EquivalentTo(expected.Keys));
            foreach (var pair in expected)
            {
                var clip = sheet.Clips[pair.Key];
                Assert.That((clip.Frames, clip.Loop), Is.EqualTo(pair.Value), pair.Key);
                Assert.That(clip.Total, Is.EqualTo(clip.Frames / 12f).Within(0.01f), pair.Key + " at 12 fps");
            }
            Assert.That(sheet.Events.ContainsKey("cast"), Is.True, "the frame where the flame bursts");
            Assert.That(sheet.Events["cast"].frame, Is.InRange(1, sheet.Clips["cast"].Frames - 2));
            Assert.That(room.ImpactAt, Is.LessThan(sheet.Clips["cast"].Total), "the bolt lands while he still holds the staff up");
            Assert.That(sheet.Anchors.ContainsKey("staff") && sheet.Anchors.ContainsKey("mouth"), Is.True);
            Assert.That(Clips.PngSize("Warlock/warlock_96"), Is.EqualTo((sheet.Cell * sheet.Clips.Values.Max(c => c.Frames), sheet.Cell * sheet.Clips.Count)));
            Assert.That(Clips.PngSize("Warlock/warlock_halo_96"), Is.EqualTo(Clips.PngSize("Warlock/warlock_96")));
            var r = room.NpcRect;
            Assert.That(r.Y + sheet.GroundY * sheet.Scale, Is.EqualTo(WarlockScene.NpcGround), "feet on the ground line");
            Assert.That(r.X + sheet.Feet.x * sheet.Scale, Is.EqualTo(WarlockScene.NpcX).Within(1f));
            Assert.That(r.Right, Is.LessThanOrEqualTo(WarlockScene.DeckPanel.X), "every pose, even the cast, left of the deck");
            Assert.That(r.X, Is.GreaterThanOrEqualTo(0));
            Assert.That(r.Y + sheet.Top * sheet.Scale, Is.GreaterThan(WarlockScene.Tariffs.Bottom + 22 + 60), "under the price list, with room for his bubble");
            var pot = WarlockCauldron.Sprite(art);
            Assert.That(WarlockScene.CauldronX + pot.W, Is.LessThanOrEqualTo(WarlockScene.DeckPanel.X - 8), "the cauldron fits beside him");
            Assert.That(WarlockScene.CauldronX - pot.W, Is.GreaterThan(r.X + 60 * sheet.Scale), "in front of him, not over him");
        }

        [Test] public void TheCauldronIsDrawnInTheArtStyle()
        {
            var px = WarlockCauldron.Pixels();
            Assert.That(px.Length, Is.EqualTo(WarlockCauldron.W * WarlockCauldron.H));
            Assert.That(px[0].A + px[WarlockCauldron.W - 1].A, Is.Zero, "transparent corners");
            var colours = px.Where(p => p.A > 0).Distinct().ToList();
            Assert.That(colours, Has.Member(new Rgba(7, 5, 12)), "one ink outline");
            Assert.That(colours.Count, Is.InRange(8, 20), "a small palette of ramps");
            Assert.That(colours.Any(c => c.G > c.R + 60), Is.True, "the green brew");
        }

        [Test] public void EveryLineFitsItsBubbleWithTheLongestCardName()
        {
            var room = Room(Run(100)); var font = art.Fonts.Get(13);
            var longest = RunContent.Cards.Select(c => c.Name).Concat(RunContent.Cards.Select(c => c.Upgrade().Name)).OrderByDescending(n => font.Width(n)).First();
            var head = room.NpcRect.Y + room.Npc.Top * room.Npc.Scale;
            foreach (var pair in WarlockLines.All)
                foreach (var line in pair.Value)
                    foreach (var shortBy in new[] { 1, 999, 1234567 })
                    {
                        var text = WarlockLines.Fill(line, longest, shortBy); var label = pair.Key + ": " + text;
                        Assert.That(text.Contains("{"), Is.False, label + ": every placeholder filled");
                        var b = room.Bubble(text);
                        Assert.That(b.Lines.Count, Is.InRange(1, 3), label);
                        Assert.That(string.Join(" ", b.Lines), Is.EqualTo(text), label + ": every word, in order");
                        Assert.That(b.Lines.All(l => font.Width(l) <= WarlockScene.BubbleMaxW), Is.True, label);
                        Assert.That(b.Box.X, Is.GreaterThanOrEqualTo(16), label);
                        Assert.That(b.Box.Right, Is.LessThanOrEqualTo(WarlockScene.DeckPanel.X - 8), label + ": left of the deck");
                        Assert.That(b.Box.Bottom + 12, Is.LessThanOrEqualTo(head), label + ": over his hood");
                        Assert.That(b.Box.Y, Is.GreaterThan(WarlockScene.Tariffs.Bottom + 22 + 8), label + ": under the price list (even with the hammer's note)");
                    }
        }

        [Test] public void EveryCueHasLinesAndTheRightReaction()
        {
            foreach (WarlockCue cue in Enum.GetValues(typeof(WarlockCue))) Assert.That(WarlockLines.All[cue], Is.Not.Empty, cue.ToString());
            var want = new Dictionary<WarlockCue, string> { [WarlockCue.Enter] = "greet", [WarlockCue.AllDone] = "greet", [WarlockCue.Upgrade] = "cast", [WarlockCue.Poor] = "no", [WarlockCue.Maxed] = "no" };
            foreach (WarlockCue cue in Enum.GetValues(typeof(WarlockCue)))
                Assert.That(WarlockSpeech.ReactionOf(cue), Is.EqualTo(want.TryGetValue(cue, out var r) ? r : null), cue.ToString());
        }

        [Test] public void TheBoxesOfTheRoomDoNotOverlap()
        {
            var boxes = new Dictionary<string, RectF>
            {
                ["counter"] = WarlockScene.Counter, ["pause"] = PauseMenu.PauseButton, ["tariffs"] = new RectF(WarlockScene.Tariffs.X, WarlockScene.Tariffs.Y, WarlockScene.Tariffs.W, WarlockScene.Tariffs.H + 22),
                ["deck"] = WarlockScene.DeckPanel, ["exit"] = WarlockScene.ExitButton, ["gold"] = new RectF(1060, 8, 212, 52),
            };
            foreach (var a in boxes) foreach (var b in boxes)
                    if (string.CompareOrdinal(a.Key, b.Key) < 0) Assert.That(Overlap(a.Value, b.Value), Is.False, a.Key + " / " + b.Key);
            foreach (var b in boxes.Values) Assert.That(Inside(b, new RectF(0, 0, Canvas.Width, Canvas.Height)), Is.True);
            Assert.That(Inside(WarlockScene.Viewport, WarlockScene.DeckPanel), Is.True);
            Assert.That(Inside(WarlockScene.Track, WarlockScene.DeckPanel), Is.True);
            Assert.That(Inside(WarlockScene.ToggleRect, WarlockScene.DeckPanel) && WarlockScene.ToggleRect.Bottom <= WarlockScene.Viewport.Y, Is.True, "the toggle sits in the header");
            Assert.That(WarlockScene.Viewport.W, Is.EqualTo(WarlockScene.Columns * WarlockScene.TileW));
            Assert.That(WarlockScene.Viewport.H, Is.EqualTo(2 * WarlockScene.TileH), "two whole rows at once");
            Assert.That(Inside(WarlockScene.ConfirmButton, WarlockScene.ConfirmPanel) && Inside(WarlockScene.CancelButton, WarlockScene.ConfirmPanel), Is.True);
            Assert.That(Overlap(WarlockScene.ConfirmButton, WarlockScene.CancelButton), Is.False);
            Assert.That(WarlockScene.ConfirmPanel.X, Is.GreaterThanOrEqualTo(WarlockScene.DeckPanel.X), "the panel never covers El Brujo");
        }

        [Test] public void EveryCardFitsItsTileAndItsConfirmationPanel()
        {
            var game = Game();
            foreach (var hero in new[] { HeroKind.Warrior, HeroKind.Mage, HeroKind.Rogue })
            {
                var run = Run(99999, hero);
                foreach (var c in RunContent.Cards) run.AddCard(c);
                var room = new WarlockScene(game, run);
                for (var i = 0; i < run.Deck.Count; i++)
                {
                    var card = run.Deck[i]; var label = hero + " " + card.Id;
                    var t = room.Tile(i);
                    Assert.That(t.Card.X, Is.GreaterThanOrEqualTo(t.Tile.X), label); Assert.That(t.Card.Right, Is.LessThanOrEqualTo(t.Tile.Right), label);
                    Assert.That(t.Button.Bottom, Is.LessThanOrEqualTo(t.Tile.Bottom), label + ": the button is inside its tile");
                    Assert.That(t.Width, Is.LessThanOrEqualTo(t.Button.W - 12), label + ": price fits its button");
                    if (!RunState.CanUpgrade(card)) continue;
                    Assert.That(t.Price, Is.EqualTo(run.UpgradePrice(card)), label);
                    room.Select(i);
                    var v = room.Confirmation();
                    Assert.That(art.Fonts.Bold(22).Width(v.Title), Is.LessThanOrEqualTo(WarlockScene.ConfirmPanel.W - 80), label);
                    Assert.That(Inside(v.Old, WarlockScene.ConfirmPanel) && Inside(v.New, WarlockScene.ConfirmPanel), Is.True, label + ": both cards in the panel");
                    Assert.That(Overlap(v.Old, v.New), Is.False, label);
                    Assert.That(v.Chips, Is.Not.Empty, label + ": the upgrade says what it changes");
                    foreach (var chip in v.Chips)
                    {
                        Assert.That(Inside(chip.rect, ((RectF)WarlockScene.ConfirmPanel).Inflate(-32, -32)), Is.True, label + ": " + chip.text);
                        Assert.That(chip.rect.Y, Is.GreaterThanOrEqualTo(v.Old.Bottom), label);
                        Assert.That(chip.rect.Bottom, Is.LessThanOrEqualTo(WarlockScene.ConfirmButton.Y - 8), label + ": above the buttons");
                        Assert.That(art.Fonts.Get(20).Width(chip.text) + 22, Is.LessThanOrEqualTo(chip.rect.W), label + ": " + chip.text);
                    }
                    Assert.That(v.ButtonWidth, Is.LessThanOrEqualTo(WarlockScene.ConfirmButton.W - 20), label + ": Mejorar + price + Enter fit");
                    Assert.That(v.Affordable, Is.True, label);
                    room.Cancel();
                }
            }
        }

        [Test] public void PricesFromZeroToHugeFitTheirButtons()
        {
            var room = Room(Run(100));
            foreach (var p in new[] { 0, 1, 50, 200, 9999, 123456, 99999999, int.MaxValue })
            {
                var (label, size, width) = room.PriceLine(p, CardWidget.CardW - 20);
                Assert.That(width, Is.LessThanOrEqualTo(CardWidget.CardW - 20), p.ToString());
                Assert.That(label.Length, Is.GreaterThan(0));
            }
        }

        [Test] public void ThePriceListFollowsTheDomainAndTheHammer()
        {
            var run = Run(100); var room = Room(run);
            var v = room.TariffList();
            Assert.That(v.Rows.Select(r => r.price), Is.EqualTo(new[] { 50, 75, 100, 150, 200 }));
            Assert.That(v.Note, Is.Null);
            foreach (var row in v.Rows) Assert.That(Inside(row.row, v.Box), Is.True, row.name);
            Assert.That(v.Rows.Select(r => r.name), Is.EqualTo(Enumerable.Range(1, 5).Select(RunContent.RarityLabel)));
            run.AcquireRelic("smith_hammer");
            v = room.TariffList();
            Assert.That(v.Rows.Select(r => r.price), Is.EqualTo(new[] { 25, 38, 50, 75, 100 }), "the Martillo del Herrero halves them (rounded up)");
            Assert.That(v.Rows.All(r => r.was == r.price * 2), Is.True);
            Assert.That(v.Note, Does.Contain("Martillo"));
            foreach (var row in v.Rows) Assert.That(Inside(row.row, v.Box), Is.True, row.name);
            Assert.That(v.Box.Bottom - 22, Is.GreaterThan(v.Rows.Last().row.Bottom), "room for the note");
            var card = run.Deck.First(RunState.CanUpgrade);
            Assert.That(room.Tile(run.Deck.ToList().IndexOf(card)).Price, Is.EqualTo(run.UpgradePriceFor(card.Rarity)));
        }

        [Test] public void PickingConfirmingAndTheShowUpgradeTheCardOnce()
        {
            var run = Run(400); var room = Room(run);
            Assert.That(room.Speech.Cue, Is.EqualTo(WarlockCue.Enter));
            var i = run.Deck.ToList().FindIndex(RunState.CanUpgrade); var card = run.Deck[i]; var price = run.UpgradePrice(card);
            room.Update(1 / 60f);
            Assert.That(room.Pose.Anim, Is.EqualTo("greet"), "a flame in his palm when you walk in");
            Click(room, room.CardRect(i));
            Assert.That(room.Current, Is.EqualTo(WarlockScene.Phase.Confirm)); Assert.That(room.HasOverlay, Is.True);
            Assert.That(room.SelectedIndex, Is.EqualTo(i)); Assert.That(room.Speech.Cue, Is.EqualTo(WarlockCue.Select));
            Assert.That(run.Gold, Is.EqualTo(400), "nothing is paid until you confirm");
            Click(room, WarlockScene.ConfirmButton);
            Assert.That(room.Current, Is.EqualTo(WarlockScene.Phase.Upgrading));
            Assert.That(run.Gold, Is.EqualTo(400 - price)); Assert.That(run.Deck[i].Upgraded, Is.True);
            Assert.That(run.Deck[i].Name, Is.EqualTo(card.Upgrade().Name));
            Assert.That(room.Speech.Cue, Is.EqualTo(WarlockCue.Upgrade));
            room.Update(1 / 60f);
            Assert.That(room.Pose.Anim, Is.EqualTo("cast"), "he raises the staff");
            Click(room, WarlockScene.ConfirmButton); Click(room, room.CardRect(i));
            Assert.That(room.Current, Is.EqualTo(WarlockScene.Phase.Upgrading), "the spell cannot be skipped before it lands");
            Assert.That(run.Gold, Is.EqualTo(400 - price), "and nothing is charged twice");
            Tick(room, room.ImpactAt + 0.05f);
            Assert.That(room.Current, Is.EqualTo(WarlockScene.Phase.Upgrading));
            Tick(room, WarlockScene.HoldT + 0.1f);
            Assert.That(room.Current, Is.EqualTo(WarlockScene.Phase.Browse), "the room comes back on its own");
            Assert.That(room.HasOverlay, Is.False);
            Click(room, room.CardRect(i));
            Assert.That(room.Current, Is.EqualTo(WarlockScene.Phase.Browse), "an upgraded card cannot be picked again");
            Assert.That(room.Speech.Cue, Is.EqualTo(WarlockCue.Maxed));
            room.Update(1 / 60f); Assert.That(room.Pose.Anim, Is.EqualTo("no"));
            Assert.That(run.Gold, Is.EqualTo(400 - price));
        }

        [Test] public void AfterTheSpellLandsAClickOrEnterSkipsTheRest()
        {
            var run = Run(400); var room = Room(run);
            var i = run.Deck.ToList().FindIndex(RunState.CanUpgrade);
            room.Select(i); Key_(room, Key.Return);
            Assert.That(room.Current, Is.EqualTo(WarlockScene.Phase.Upgrading), "Enter confirms");
            Tick(room, room.ImpactAt + 0.05f);
            Key_(room, Key.Return);
            Assert.That(room.Current, Is.EqualTo(WarlockScene.Phase.Browse));
            room.Select(run.Deck.ToList().FindIndex(RunState.CanUpgrade)); room.Confirm();
            Tick(room, room.ImpactAt + 0.05f);
            Click(room, new RectF(700, 400, 2, 2));
            Assert.That(room.Current, Is.EqualTo(WarlockScene.Phase.Browse));
        }

        [Test] public void CancellingWithTheButtonEscapeOrAClickOutsideChangesNothing()
        {
            var run = Run(400); var room = Room(run); var before = run.Deck.Select(c => c.Name).ToList();
            var i = run.Deck.ToList().FindIndex(RunState.CanUpgrade);
            foreach (var how in new[] { "button", "escape", "outside" })
            {
                room.Select(i);
                Assert.That(room.Current, Is.EqualTo(WarlockScene.Phase.Confirm), how);
                if (how == "button") Click(room, WarlockScene.CancelButton);
                else if (how == "escape") Key_(room, Key.Escape);
                else Click(room, new RectF(WarlockScene.ConfirmPanel.Right + 20, 300, 2, 2));
                Assert.That(room.Current, Is.EqualTo(WarlockScene.Phase.Browse), how);
                Assert.That(room.Speech.Cue, Is.EqualTo(WarlockCue.Cancel), how);
            }
            room.Select(i);
            Click(room, new RectF(120, 500, 2, 2));
            Assert.That(room.Current, Is.EqualTo(WarlockScene.Phase.Confirm), "clicking El Brujo's side does not close the panel");
            Assert.That(run.Gold, Is.EqualTo(400)); Assert.That(run.Deck.Select(c => c.Name), Is.EqualTo(before));
        }

        [Test] public void WithoutGoldHeShakesHisHeadAndTheCardStaysTheSame()
        {
            var run = Run(400); var room = Room(run);
            var i = run.Deck.ToList().FindIndex(RunState.CanUpgrade); var price = run.UpgradePrice(run.Deck[i]);
            SetGold(run, price - 1);
            room.Update(0.5f);
            room.Select(i);
            Assert.That(room.Confirmation().Affordable, Is.False);
            StringAssert.Contains("faltan 1 de oro", room.Confirmation().Note);
            Assert.That(room.Confirm(), Is.False);
            Assert.That(room.Current, Is.EqualTo(WarlockScene.Phase.Confirm), "the panel stays so you can cancel");
            Assert.That(room.Speech.Cue, Is.EqualTo(WarlockCue.Poor));
            if (room.Speech.Text.Contains("faltan")) StringAssert.Contains("1", room.Speech.Text);
            room.Update(1 / 60f); Assert.That(room.Pose.Anim, Is.EqualTo("no"));
            Assert.That(run.Gold, Is.EqualTo(price - 1)); Assert.That(run.Deck[i].Upgraded, Is.False);
            Assert.That(room.Tile(i).Affordable, Is.False);
            SetGold(run, price);
            Assert.That(room.Confirm(), Is.True, "exactly enough gold");
            Assert.That(run.Gold, Is.Zero);
        }

        [Test] public void UpgradingTheLastCardMakesHimSayGoodbye()
        {
            var run = Run(99999); var room = Room(run);
            while (run.Deck.Any(RunState.CanUpgrade))
            {
                room.Select(run.Deck.ToList().FindIndex(RunState.CanUpgrade)); Assert.That(room.Confirm(), Is.True);
                Tick(room, room.ImpactAt + WarlockScene.HoldT + 0.1f, 1 / 30f);
            }
            Assert.That(room.Speech.Cue, Is.EqualTo(WarlockCue.AllDone));
            Assert.That(run.Deck.All(c => c.Upgraded), Is.True);
            var again = Room(run);
            Assert.That(again.Speech.Cue, Is.EqualTo(WarlockCue.AllDone), "walking into the room with nothing left to upgrade");
        }

        [Test] public void TheToggleScrollingAndTheExitWork()
        {
            var run = Run(400);
            foreach (var c in RunContent.Cards.Take(40)) run.AddCard(c);
            var room = Room(run);
            Assert.That(room.ShowUpgraded, Is.False);
            Click(room, WarlockScene.ToggleRect); Assert.That(room.ShowUpgraded, Is.True);
            Key_(room, Key.V); Assert.That(room.ShowUpgraded, Is.False);
            Assert.That(room.MaxScroll, Is.GreaterThan(0));
            room.HandleEvent(new InputEvent(EventType.Wheel, 700, 400, wheel: -1));
            Assert.That(room.Scroll, Is.EqualTo(60), "the wheel scrolls");
            Key_(room, Key.End); Assert.That(room.Scroll, Is.EqualTo(room.MaxScroll));
            Key_(room, Key.PageDown); Assert.That(room.Scroll, Is.EqualTo(room.MaxScroll), "clamped at the end");
            Key_(room, Key.Home); Assert.That(room.Scroll, Is.Zero);
            Key_(room, Key.Up); Assert.That(room.Scroll, Is.Zero, "clamped at the start");
            Key_(room, Key.Down); Tick(room, 1f);
            Assert.That(room.Scroll, Is.EqualTo(60));
            // a card scrolled out of the viewport cannot be clicked through the header
            Key_(room, Key.End); Tick(room, 1f);
            var first = room.CardRect(0);
            Assert.That(first.Bottom, Is.LessThan(WarlockScene.Viewport.Y));
            var last = run.Deck.Count - 1; var lr = room.CardRect(last);
            Assert.That(lr.Bottom, Is.LessThanOrEqualTo(WarlockScene.Viewport.Bottom + 1), "the last card is reachable");
            if (RunState.CanUpgrade(run.Deck[last])) { Click(room, lr); Assert.That(room.SelectedIndex, Is.EqualTo(last)); room.Cancel(); }
            Assert.That(room.Cleared, Is.False);
            Click(room, WarlockScene.ExitButton);
            Assert.That(room.Cleared, Is.True);
        }

        [Test] public void TheSceneManagerOpensAndClosesTheRoom()
        {
            var game = Game(); var run = Run(400);
            var manager = new SceneManager(game, new MainMenuScene(game)); manager.StartRun(run);
            var room = new WarlockScene(game, run); manager.Push(room);
            Assert.That(PauseMenu.PauseButtonRect(room), Is.EqualTo(PauseMenu.PauseButton), "the pause button in its usual place");
            room.Select(run.Deck.ToList().FindIndex(RunState.CanUpgrade));
            Assert.That(PauseMenu.PauseButtonRect(room), Is.Null, "hidden while the panel is open");
            manager.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.Escape));
            Assert.That(room.Current, Is.EqualTo(WarlockScene.Phase.Browse), "Escape closes the panel first");
            Assert.That(manager.Pause, Is.Null);
            manager.HandleEvent(new InputEvent(EventType.MouseDown, WarlockScene.ExitButton.CenterX, WarlockScene.ExitButton.CenterY, 1));
            manager.Update(0.05f);
            Assert.That(manager.Top, Is.Not.SameAs(room));
        }

        [Test] public void HoverCommentsAreOccasional()
        {
            var run = Run(400); foreach (var c in RunContent.Cards.Where(c => c.Rarity >= 3).Take(5)) run.AddCard(c);
            var room = Room(run); var said = 0;
            for (var k = 0; k < 120; k++)
            {
                Tick(room, 6f, 0.1f);
                var lines = room.Speech.Lines;
                var r = room.CardRect(k % Math.Min(12, run.Deck.Count));
                room.HandleEvent(new InputEvent(EventType.MouseMove, r.CenterX, r.CenterY));
                if (room.Speech.Lines > lines && new[] { WarlockCue.HoverCard, WarlockCue.HoverRare, WarlockCue.HoverPoor, WarlockCue.HoverUpgraded }.Contains(room.Speech.Cue)) said++;
                room.HandleEvent(new InputEvent(EventType.MouseMove, 1180, 690));
            }
            Assert.That(said, Is.InRange(20, 100), "about " + WarlockSpeech.HoverChance * 100 + " % of the time");
        }

        [Test] public void WhenTheRoomIsQuietHeStrokesHisBeard()
        {
            var sheet = Room(Run(100)).Npc; var sp = new WarlockSpeech(4); var pose = new WarlockPose(8);
            const float dt = 1 / 30f; var ponders = 0; var prev = pose.Anim;
            for (var k = 0; k < 30 * 120; k++)
            {
                sp.Update(dt); pose.Update(dt, sp, sheet);
                if (pose.Anim == "ponder" && prev != "ponder") ponders++;
                prev = pose.Anim;
                Assert.That(sheet.Clips.ContainsKey(pose.Anim), Is.True, pose.Anim);
            }
            Assert.That(ponders, Is.InRange(4, 16));
            sp.Say(WarlockCue.Upgrade, "X"); pose.Update(dt, sp, sheet);
            Assert.That(pose.Anim, Is.EqualTo("cast"));
            Assert.That(pose.Elapsed, Is.Zero);
        }

        [Test] public void EveryStateDraws()
        {
            var game = Game();
            foreach (var gold in new[] { 0, 120, 99999 })
            {
                var run = Run(gold, HeroKind.Mage, gold + 3);
                foreach (var c in RunContent.Cards.Where(c => c.Rarity >= 2).Take(gold == 99999 ? 30 : 4)) run.AddCard(c);
                if (gold == 120) run.AcquireRelic("smith_hammer");
                var room = new WarlockScene(game, run);
                for (var k = 0; k < 500; k++)
                {
                    var i = k % run.Deck.Count;
                    if (k % 23 == 0) { var r = room.CardRect(i); room.HandleEvent(new InputEvent(EventType.MouseMove, r.CenterX, r.CenterY)); }
                    if (k % 61 == 0) room.Select(i);
                    if (k % 61 == 5) room.Confirm();
                    if (k % 61 == 40) room.Cancel();
                    if (k % 97 == 0) Key_(room, Key.V);
                    if (k % 71 == 0) Key_(room, k % 2 == 0 ? Key.PageDown : Key.Home);
                    room.Update(0.05f); game.Canvas.BeginFrame(); room.Draw(game.Canvas); game.Canvas.EndFrame();
                }
                Assert.That(room.Speech.Lines, Is.GreaterThan(1));
                Assert.That(room.Npc.Clips.ContainsKey(room.Pose.Anim), Is.True, room.Pose.Anim);
            }
        }
    }
}
