using System;
using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>El Mercader, the shopkeeper (2026-11-04): his sheet loads from the export of Tools/VfxLab/merchant.html with
    /// all six animations, he stands left of the stall, every line he can say fits its speech bubble, he speaks when you
    /// enter, buy, cannot pay, click something sold, empty the shop, hover an offer (sometimes) and after a quiet spell,
    /// never repeating himself too soon, and he moves: he tips his hat, bows, shakes his head, talks while the line is typed
    /// and flips a coin when the shop is quiet (null renderer, real fonts and assets).</summary>
    public sealed class MerchantTests
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

        private static RunState Run(int gold, int seed = 5) { var run = new RunState(HeroKind.Rogue, seed); typeof(RunState).GetProperty("Gold").SetValue(run, gold); return run; }
        private static ShopScene Shop(RunState run) => new ShopScene(Game(), run);
        private static void Click(ShopScene s, IRect r) => s.HandleEvent(new InputEvent(EventType.MouseDown, r.CenterX, r.CenterY, 1));
        private static bool Inside(RectF a, RectF b) => a.X >= b.X && a.Y >= b.Y && a.Right <= b.Right && a.Bottom <= b.Bottom;

        [Test] public void TheSheetLoadsAndPlacesHimLeftOfTheStall()
        {
            var shop = Shop(Run(100)); var sheet = shop.Merchant;
            Assert.That(sheet.Valid, Is.True, "Resources/Game/Shop/merchant.json + merchant_96.png");
            var expected = new Dictionary<string, (int frames, bool loop)> { ["idle"] = (24, true), ["talk"] = (12, true), ["greet"] = (14, false), ["thanks"] = (14, false), ["no"] = (14, false), ["coins"] = (24, false) };
            Assert.That(sheet.Clips.Keys, Is.EquivalentTo(expected.Keys));
            foreach (var pair in expected)
            {
                var clip = sheet.Clips[pair.Key];
                Assert.That((clip.Frames, clip.Loop), Is.EqualTo(pair.Value), pair.Key);
                Assert.That(clip.Total, Is.EqualTo(clip.Frames / 12f).Within(0.01f), pair.Key + " at 12 fps");
            }
            Assert.That(sheet.Clips["greet"].Total, Is.LessThan(MerchantSpeech.Hold), "a reaction is over before his line fades");
            Assert.That(sheet.Resolve("dance"), Is.EqualTo("idle"), "unknown animations fall back to idle");
            Assert.That(Clips.PngSize("Shop/merchant_96"), Is.EqualTo((sheet.Cell * sheet.Clips.Values.Max(c => c.Frames), sheet.Cell * sheet.Clips.Count)));
            Assert.That(Clips.PngSize("Shop/merchant_halo_96"), Is.EqualTo(Clips.PngSize("Shop/merchant_96")));
            var r = shop.MerchantRect;
            Assert.That(r.Y + sheet.GroundY * sheet.Scale, Is.EqualTo(ShopScene.MerchantGround), "feet on the ground line");
            Assert.That(r.X + sheet.Feet.x * sheet.Scale, Is.EqualTo(ShopScene.MerchantX).Within(1f));
            // the whole cell (so every pose, even the deepest bow) stays left of the stall, under the relic counter and over the luck badge
            Assert.That(r.Right, Is.LessThanOrEqualTo(ShopScene.StallX), "every pose fits left of the stall");
            Assert.That(r.X + 10 * sheet.Scale, Is.GreaterThan(0), "back of the lantern");
            Assert.That(r.Y + sheet.Top * sheet.Scale, Is.GreaterThan(ShopScene.RelicCounter.Bottom));
            Assert.That(ShopScene.MerchantGround + 8, Is.LessThan(654), "shadow over the luck badge");
            for (var i = 0; i < 3; i++) Assert.That(ShopScene.RelicRect(i).X, Is.GreaterThanOrEqualTo(ShopScene.StallX));
        }

        [Test] public void EveryLineFitsItsBubbleWithTheLongestNames()
        {
            var shop = Shop(Run(100));
            var longest = RunContent.Relics.Select(r => r.Name).Concat(RunContent.Packs.Select(p => p.Name)).OrderByDescending(n => art.Fonts.Get(13).Width(n)).First();
            var head = shop.MerchantRect.Y + shop.Merchant.Top * shop.Merchant.Scale;
            foreach (var pair in MerchantLines.All)
                foreach (var line in pair.Value)
                    foreach (var shortBy in new[] { 1, 999, 1234567 })
                    {
                        var text = MerchantLines.Fill(line, longest, shortBy); var label = pair.Key + ": " + text;
                        Assert.That(text.Contains("{"), Is.False, label + ": every placeholder filled");
                        var b = shop.Bubble(text);
                        Assert.That(b.Lines.Count, Is.InRange(1, 3), label);
                        Assert.That(string.Join(" ", b.Lines), Is.EqualTo(text), label + ": every word, in order");
                        Assert.That(b.Lines.All(l => art.Fonts.Get(13).Width(l) <= ShopScene.BubbleMaxW), Is.True, label);
                        Assert.That(b.Box.X, Is.GreaterThanOrEqualTo(16), label);
                        Assert.That(b.Box.Right, Is.LessThanOrEqualTo(ShopScene.StallX - 8), label + ": left of the stall");
                        Assert.That(b.Box.Bottom + 12, Is.LessThanOrEqualTo(head), label + ": over his hat");
                        Assert.That(b.Box.Y, Is.GreaterThan(ShopScene.RelicCounter.Bottom + 8), label + ": under the relic counter");
                        Assert.That(b.TailX, Is.InRange(b.Box.X + 8, b.Box.Right - 8), label);
                    }
        }

        [Test] public void EveryCueHasLinesAndNoneRefersToTheItemWithAGender()
        {
            foreach (MerchantCue cue in Enum.GetValues(typeof(MerchantCue))) Assert.That(MerchantLines.All[cue], Is.Not.Empty, cue.ToString());
            foreach (var line in MerchantLines.All.Values.SelectMany(l => l).Where(l => l.Contains("{item}")))
                foreach (var word in new[] { "este ", "esta ", "tuyo", "tuya", "el {item}", "la {item}" })
                    StringAssert.DoesNotContain(word, line.ToLowerInvariant(), line);
        }

        [Test] public void HeGreetsYouAndReactsToSalesRefusalsAndSoldOffers()
        {
            var run = Run(400); var shop = Shop(run); var sp = shop.Speech;
            Assert.That(sp.Cue, Is.EqualTo(MerchantCue.Enter));
            Assert.That(MerchantLines.All[MerchantCue.Enter], Has.Member(sp.Text));
            var golden = shop.Relics[0].Chroma != ChromaKind.None;
            Click(shop, ShopScene.RelicRect(0));
            Assert.That(sp.Cue, Is.EqualTo(golden ? MerchantCue.BuyGolden : MerchantCue.BuyRelic));
            Click(shop, ShopScene.RelicRect(0));
            Assert.That(sp.Cue, Is.EqualTo(MerchantCue.Sold), "clicking a sold offer");
            typeof(RunState).GetProperty("Gold").SetValue(run, 3);
            Click(shop, ShopScene.RelicRect(1));
            Assert.That(sp.Cue, Is.EqualTo(MerchantCue.Poor));
            if (sp.Text.Contains("Te faltan")) StringAssert.Contains((shop.RelicPrice - 3).ToString(), sp.Text);
        }

        [Test] public void EmptyingTheShopGetsAFarewellAndTheVoucherAThankYou()
        {
            var run = Run(99999); run.AcquireRelic("voucher"); var shop = Shop(run); var sp = shop.Speech;
            Click(shop, ShopScene.RelicRect(0));
            Assert.That(sp.Cue, Is.EqualTo(MerchantCue.BuyFree));
            Click(shop, ShopScene.RelicRect(1)); Click(shop, ShopScene.RelicRect(2));
            for (var i = 0; i < 3; i++)
            {
                Click(shop, ShopScene.PackRect(i));
                for (var t = 0f; t < ShopScene.PackRiseT + 0.1f; t += 0.05f) shop.Update(0.05f);
                shop.SelectedPack = null;
            }
            Assert.That(sp.Cue, Is.EqualTo(MerchantCue.SoldOut));
        }

        [Test] public void HoverCommentsAreOccasionalAndNeverRepeatTheSameOffer()
        {
            var sp = new MerchantSpeech(1); sp.Say(MerchantCue.Enter);
            Assert.That(sp.Hover("relic0", MerchantCue.HoverRelic, "X"), Is.False, "too soon after the greeting");
            var said = 0;
            for (var k = 0; k < 200; k++)
            {
                for (var t = 0; t < 60; t++) sp.Update(0.1f);
                if (sp.Hover("offer" + k, MerchantCue.HoverRelic, "X")) said++;
                Assert.That(sp.Hover("offer" + k, MerchantCue.HoverRelic, "X"), Is.False, "the same offer twice in a row");
            }
            Assert.That(said, Is.InRange(40, 160), "about " + MerchantSpeech.HoverChance * 100 + " % of the time");
        }

        [Test] public void HeChatsAfterAQuietSpellAndNeverRepeatsTheLastThreeLines()
        {
            var sp = new MerchantSpeech(7); sp.Say(MerchantCue.Enter);
            for (var t = 0; t < 2000; t++) sp.Update(0.1f);
            Assert.That(sp.Lines, Is.GreaterThan(5), "idle chatter over 200 s");
            var history = new List<string>();
            for (var k = 0; k < 300; k++) history.Add(sp.Say(MerchantCue.Idle));
            for (var k = 1; k < history.Count; k++)
                for (var j = Math.Max(0, k - 3); j < k; j++) Assert.That(history[k], Is.Not.EqualTo(history[j]), "line " + k);
        }

        [Test] public void TheLineIsTypedOutHeldAndFaded()
        {
            var sp = new MerchantSpeech(3); var text = sp.Say(MerchantCue.SoldOut);
            Assert.That(sp.Shown, Is.Zero); Assert.That(sp.Talking, Is.True);
            for (var t = 0f; t < text.Length / MerchantSpeech.CharsPerSecond + 0.05f; t += 0.05f) sp.Update(0.05f);
            Assert.That(sp.Shown, Is.EqualTo(text.Length)); Assert.That(sp.Talking, Is.False); Assert.That(sp.Alpha, Is.EqualTo(1f));
            for (var t = 0f; t < MerchantSpeech.Hold + MerchantSpeech.Fade + 0.1f; t += 0.05f) sp.Update(0.05f);
            Assert.That(sp.Visible, Is.False);
            sp.Update(1e9f); Assert.That(sp.Alpha, Is.InRange(0f, 1f));
        }

        [Test] public void EveryCueStartsTheRightReaction()
        {
            var want = new Dictionary<MerchantCue, string>
            {
                [MerchantCue.Enter] = "greet", [MerchantCue.BuyRelic] = "thanks", [MerchantCue.BuyPack] = "thanks", [MerchantCue.BuyGolden] = "thanks",
                [MerchantCue.BuyFree] = "thanks", [MerchantCue.SoldOut] = "thanks", [MerchantCue.Poor] = "no", [MerchantCue.Sold] = "no",
            };
            foreach (MerchantCue cue in Enum.GetValues(typeof(MerchantCue)))
                Assert.That(MerchantSpeech.ReactionOf(cue), Is.EqualTo(want.TryGetValue(cue, out var r) ? r : null), cue.ToString());
        }

        private static void Tick(ShopScene s, float seconds, float dt = 1 / 60f) { for (var t = 0f; t < seconds - 1e-4f; t += dt) s.Update(dt); }

        [Test] public void HeTipsHisHatBowsAndShakesHisHead()
        {
            var run = Run(400); var shop = Shop(run); var pose = shop.Pose; var clips = shop.Merchant.Clips;
            shop.Update(1 / 60f);
            Assert.That(pose.Anim, Is.EqualTo("greet"), "walking in");
            Tick(shop, clips["greet"].Total - 0.1f);
            Assert.That(pose.Anim, Is.EqualTo("greet"), "the hat tip plays to its end");
            Tick(shop, 0.2f);
            Assert.That(pose.Anim, Is.EqualTo(shop.Speech.Talking ? "talk" : "idle"));
            Click(shop, ShopScene.RelicRect(0)); shop.Update(1 / 60f);
            Assert.That(pose.Anim, Is.EqualTo("thanks"), "a sale");
            Assert.That(shop.Merchant.FrameAt(pose.Anim, pose.Elapsed), Is.LessThanOrEqualTo(1), "from the start");
            Tick(shop, 0.4f);
            Click(shop, ShopScene.RelicRect(0)); shop.Update(1 / 60f);
            Assert.That(pose.Anim, Is.EqualTo("no"), "clicking a sold offer interrupts the bow");
            Tick(shop, 0.3f);
            typeof(RunState).GetProperty("Gold").SetValue(run, 3);
            Click(shop, ShopScene.RelicRect(1)); shop.Update(1 / 60f);
            Assert.That(pose.Anim, Is.EqualTo("no")); Assert.That(pose.Elapsed, Is.LessThan(0.05f), "each refusal shakes his head again");
        }

        [Test] public void HeTalksWhileTheLineIsTypedAndFinishesTheGesture()
        {
            var sheet = Shop(Run(100)).Merchant; var sp = new MerchantSpeech(11); var pose = new MerchantPose(5);
            var talk = sheet.Clips["talk"];
            sp.Say(MerchantCue.Idle);
            const float dt = 1 / 60f;
            pose.Update(dt, sp, sheet); Assert.That(pose.Anim, Is.EqualTo("talk"), "idle chatter has no reaction: he just talks");
            var t = 0f;
            while (sp.Talking) { sp.Update(dt); pose.Update(dt, sp, sheet); t += dt; }
            var cycles = (float)Math.Ceiling(t / talk.Total);
            var switched = -1f;
            for (var k = 0; k < 200 && switched < 0; k++) { sp.Update(dt); pose.Update(dt, sp, sheet); t += dt; if (pose.Anim == "idle") switched = t; }
            Assert.That(switched, Is.GreaterThan(0), "back to idle");
            Assert.That(switched, Is.EqualTo(cycles * talk.Total).Within(2.5f * dt), "only at the end of a talking cycle (it ends on the rest pose)");
            Assert.That(sheet.FrameAt("idle", pose.Elapsed), Is.Zero, "and the idle starts on its first frame");
        }

        [Test] public void WhenTheShopIsQuietHeFlipsACoinAndALineInterruptsIt()
        {
            var sheet = Shop(Run(100)).Merchant; var sp = new MerchantSpeech(2); var pose = new MerchantPose(9);
            const float dt = 1 / 30f;
            var flips = 0; var prev = pose.Anim; var t = 0f; var lastFlip = -99f; var gaps = new List<float>();
            for (var k = 0; k < 30 * 120; k++)
            {
                sp.Update(dt); pose.Update(dt, sp, sheet); t += dt;
                if (pose.Anim == "coins" && prev != "coins") { flips++; if (lastFlip > 0) gaps.Add(t - lastFlip); lastFlip = t; }
                prev = pose.Anim;
            }
            Assert.That(flips, Is.InRange(4, 16), "a coin now and then over two minutes, between his idle chatter");
            Assert.That(gaps.Min(), Is.GreaterThanOrEqualTo(MerchantPose.FidgetMin), "never back to back");
            // a line while the coin is in the air: he answers at once
            for (var k = 0; k < 30 * 30 && pose.Anim != "coins"; k++) { sp.Update(dt); pose.Update(dt, sp, sheet); }
            Assert.That(pose.Anim, Is.EqualTo("coins"));
            sp.Say(MerchantCue.HoverRelic, "X"); pose.Update(dt, sp, sheet);
            Assert.That(pose.Anim, Is.EqualTo("talk"), "a comment without a reaction interrupts the coin with talking");
            sp.Say(MerchantCue.Poor, null, 10); pose.Update(dt, sp, sheet);
            Assert.That(pose.Anim, Is.EqualTo("no"), "a reaction interrupts the talking at once");
            sp.Say(MerchantCue.HoverPack, "X"); pose.Update(dt, sp, sheet);
            Assert.That(pose.Anim, Is.EqualTo("no"), "but a plain comment lets the reaction finish");
            for (var k = 0; k < 60; k++) { sp.Update(dt); pose.Update(dt, sp, sheet); }
            Assert.That(pose.Anim, Is.EqualTo(sp.Talking ? "talk" : "idle"));
        }

        [Test] public void TheShopDrawsHimAndHisBubbleInEveryState()
        {
            var game = Game();
            foreach (var gold in new[] { 0, 400, 99999 })
            {
                var shop = new ShopScene(game, Run(gold, gold + 1));
                for (var k = 0; k < 400; k++)
                {
                    if (k % 37 == 0) { var r = k % 2 == 0 ? ShopScene.RelicRect(k % 3) : ShopScene.PackRect(k % 3); shop.HandleEvent(new InputEvent(EventType.MouseMove, r.CenterX, r.CenterY)); }
                    if (k % 91 == 0) { Click(shop, ShopScene.RelicRect(k % 3)); shop.SelectedPack = null; }
                    shop.Update(0.05f); game.Canvas.BeginFrame(); shop.Draw(game.Canvas); game.Canvas.EndFrame();
                }
                Assert.That(shop.Speech.Lines, Is.GreaterThan(1));
                Assert.That(shop.Merchant.Clips.ContainsKey(shop.Pose.Anim), Is.True, shop.Pose.Anim);
            }
        }
    }
}
