using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>Gachapón interface (2026-11-03 redesign): every relic fits its reveal panel, prices never leave their
    /// button, the room's boxes never overlap, and every phase of every pull draws (null renderer, real fonts).</summary>
    public sealed class GachaLayoutTests
    {
        private static Art art;
        private static NullRender render;

        [SetUp] public void ResetTuning() => Tuning.Reset();
        [TearDown] public void RestoreTuning() => Tuning.Reset();

        private static GachaScene Scene(out RunState run, out GameContext game, int seed = 3, HeroKind hero = HeroKind.Rogue)
        {
            if (art == null) { render = new NullRender(); art = new Art(render, new FileAssets()); art.LoadCore(); }
            game = new GameContext(new Canvas(render), art, new SoundPlayer(new SilentAudio(), .7f, 0f), new Preferences(), null);
            run = new RunState(hero, seed);
            return new GachaScene(game, run, seed);
        }

        private static GachaScene Scene() => Scene(out _, out _);

        private static string Words(string text) => string.Join(" ", (text ?? "").Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries));

        [Test] public void EveryRelicFitsItsRevealPanel()
        {
            var scene = Scene(); var bounds = GachaScene.RevealBounds; var column = GachaScene.PullButton(PullKind.Normal);
            var machineRight = 272 + 128 * 2;
            foreach (var def in RunContent.Relics)
                foreach (var chroma in new[] { ChromaKind.None, ChromaKind.Golden, ChromaKind.Diamond })
                    foreach (var duplicate in new[] { false, true })
                    {
                        var label = def.Name + " " + chroma + (duplicate ? " repetida" : "");
                        var card = scene.RevealLayout(new RelicOffer(def, chroma), duplicate);
                        Assert.That(card.Title, Is.EqualTo(def.Name), label + ": whole name");
                        Assert.That(card.TitleSize, Is.EqualTo(20), label + ": the chunky title face");
                        Assert.That(card.Lines.Count, Is.InRange(1, 4), label);
                        Assert.That(card.Lines.Any(l => l.EndsWith(TextFit.Ellipsis, StringComparison.Ordinal)), Is.False, label + ": nothing cut");
                        Assert.That(Words(string.Join(" ", card.Lines)), Is.EqualTo(Words(def.Description)), label + ": every word shown");
                        Assert.That(card.Chips.Sum(r => r.Count), Is.EqualTo(1 + (chroma != ChromaKind.None ? 1 : 0) + (duplicate ? 1 : 0)), label);
                        Assert.That(card.Panel.W, Is.LessThanOrEqualTo(bounds.W), label);
                        Assert.That(card.Panel.X, Is.GreaterThanOrEqualTo(machineRight), label + ": clear of the machine");
                        Assert.That(card.Panel.Right, Is.LessThan(column.X), label + ": clear of the pull buttons");
                        Assert.That(card.Panel.Bottom + 14 + 48 + 24, Is.LessThanOrEqualTo(Canvas.Height), label + ": buttons and note on screen");
                    }
        }

        [Test] public void TheRoomBoxesNeverOverlapAndStayOnScreen()
        {
            var boxes = new List<(string name, IRect rect)>
            {
                ("relics", GachaScene.RelicCounter), ("pause", ToI(PauseMenu.PauseButton)),
                ("normal", GachaScene.PullButton(PullKind.Normal)), ("stellar", GachaScene.PullButton(PullKind.Stellar)),
                ("odds", GachaScene.OddsBox), ("exit", GachaScene.ExitButton),
            };
            foreach (var (name, r) in boxes)
                Assert.That(r.X >= 0 && r.Y >= 0 && r.Right <= Canvas.Width && r.Bottom <= Canvas.Height, Is.True, name + " on screen");
            for (var i = 0; i < boxes.Count; i++)
                for (var j = i + 1; j < boxes.Count; j++)
                {
                    var (a, ra) = boxes[i]; var (b, rb) = boxes[j];
                    var overlap = ra.X < rb.Right && rb.X < ra.Right && ra.Y < rb.Bottom && rb.Y < ra.Bottom;
                    Assert.That(overlap, Is.False, a + " / " + b);
                }
            Assert.That(GachaScene.RevealBounds.Right, Is.LessThan(GachaScene.PullButton(PullKind.Normal).X), "widest reveal panel");
        }

        private static IRect ToI(RectF r) => new IRect((int)r.X, (int)r.Y, (int)r.W, (int)r.H);

        [TestCase(0, 0)] [TestCase(5, 0)] [TestCase(80, 80)] [TestCase(405, 120)] [TestCase(960, 834)] [TestCase(9999, 0)]
        [TestCase(123456, 99)] [TestCase(3030140, 37)] [TestCase(1000000000, 0)] [TestCase(int.MaxValue, 0)]
        public void PricesNeverLeaveTheirButton(int price, int gold)
        {
            var (label, size, shortBy, width) = Scene().PriceLine(price, gold, GachaScene.PriceRoom);
            Assert.That(width, Is.LessThanOrEqualTo(GachaScene.PriceRoom));
            Assert.That(label, Is.Not.Empty);
            if (price == 0) Assert.That(label, Is.EqualTo("¡Gratis!"));
            if (price > 0 && price < 100000)
            {
                Assert.That((label, size), Is.EqualTo((price.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ") + " oro", 20)), "the chunky price with its unit");
                Assert.That(shortBy, Is.EqualTo(gold >= price ? null : "faltan " + (price - gold).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ")));
            }
        }

        [Test] public void EveryPhaseOfEveryPullDraws()
        {
            var scene = Scene(out var run, out var game, 12);
            run.GainGold(1_000_000_000);
            var canvas = game.Canvas; var tiers = new HashSet<int>(); var phases = new HashSet<string>();
            void Frame(float dt = 1f / 30f) { game.Time += dt; scene.Update(dt); canvas.BeginFrame(); scene.Draw(canvas); canvas.EndFrame(); phases.Add(scene.Phase); }
            for (var pull = 0; pull < 24; pull++)
            {
                Assert.That(scene.StartPull(pull % 3 == 0 ? PullKind.Stellar : PullKind.Normal), Is.True);
                for (var f = 0; f < 600 && scene.Phase != "reveal"; f++) Frame();
                Assert.That(scene.Phase, Is.EqualTo("reveal"));
                tiers.Add(scene.Result.Rarity);
                scene.HandleEvent(new InputEvent(EventType.MouseMove, 700, 560)); Frame();
                scene.HandleEvent(new InputEvent(EventType.MouseMove, 840, 560)); Frame();
                if (pull % 2 == 0) scene.Keep(); else scene.Decline();
                for (var f = 0; f < 120 && scene.Phase != "idle"; f++) Frame();
                Assert.That(scene.Phase, Is.EqualTo("idle"));
                scene.HandleEvent(new InputEvent(EventType.MouseMove, 1106, 196 + pull % 2 * 106)); Frame();
            }
            Assert.That(phases, Is.SupersetOf(new[] { "coin", "crank", "drop", "present", "open", "reveal", "collect", "discard", "idle" }));
            Assert.That(tiers.Count, Is.GreaterThanOrEqualTo(3), "several rarities drawn");
        }

        [Test] public void APoorHeroSeesWhatIsMissingAndTheBuzzMessage()
        {
            Tuning.Current.StartingGold = 0;
            var scene = Scene(out var run, out var game);
            Assert.That(run.CanPull(PullKind.Normal), Is.False);
            var (_, _, shortBy, _) = scene.PriceLine(run.GachaPrice(PullKind.Normal), run.Gold, GachaScene.PriceRoom);
            Assert.That(shortBy, Is.EqualTo("faltan " + run.GachaPrice(PullKind.Normal)));
            Assert.That(scene.StartPull(PullKind.Normal), Is.False);
            for (var f = 0; f < 90; f++) { scene.Update(1f / 60f); game.Canvas.BeginFrame(); scene.Draw(game.Canvas); game.Canvas.EndFrame(); }
            Assert.That(scene.Phase, Is.EqualTo("idle"));
        }
    }

    internal sealed class SilentAudio : IAudioBackend
    {
        public int Variants(string name) => 1;
        public int BusyVoices => 0;
        public bool Play(string name, int variant, float volume) => true;
        public void Stop(string name) { }
        public void StartMusic(float volume) { }
        public void StopMusic() { }
        public void SetMusicVolume(float volume) { }
        public long Ticks => 0;
    }
}
