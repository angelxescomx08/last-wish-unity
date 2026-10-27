using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>Gachapón sounds: the clips Tools/GenerateGachaAudio.py writes, the SoundPlayer rules they rely on,
    /// and the cues of a real GachaScene driven frame by frame (no Unity audio or rendering needed).</summary>
    public sealed class GachaSoundTests
    {
        private const float Dt = 1f / 60f;
        private const float Sfx = .7f;
        private static readonly Dictionary<string, int> GachaVariants = new Dictionary<string, int> {
            { "gacha_coin_toss", 3 }, { "gacha_coin_slot", 3 }, { "gacha_churn", 2 }, { "gacha_crank", 6 }, { "gacha_zap", 3 },
            { "gacha_tease", 2 }, { "gacha_thunk", 3 }, { "gacha_pop", 3 }, { "gacha_bounce", 2 }, { "gacha_hop", 2 },
            { "gacha_wobble", 5 }, { "gacha_open", 3 }, { "gacha_tier", 5 }, { "gacha_golden", 1 }, { "gacha_keep", 3 },
            { "gacha_stash", 3 }, { "gacha_dust", 3 } };
        /// <summary>Sounds of other screens the machine used before it had its own.</summary>
        private static readonly string[] Borrowed = { "nav", "card", "open_pack", "reward", "confirm", "purchase", "cancel" };
        private static readonly int[] RisingCrank = { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5 };

        [SetUp] public void ResetTuning() => Tuning.Reset();
        [TearDown] public void RestoreTuning() => Tuning.Reset();

        // ------------------------------------------------------------------ clips on disk

        [Test] public void EveryEffectHasItsClipsOnDisk()
        {
            foreach (var name in SoundPlayer.Effects) Assert.That(Clips.Variants(name), Is.GreaterThanOrEqualTo(1), name);
            Assert.That(SoundPlayer.Effects.Count(n => n.StartsWith("gacha_", StringComparison.Ordinal)), Is.EqualTo(GachaVariants.Count));
            foreach (var pair in GachaVariants)
            {
                Assert.That(SoundPlayer.Effects, Does.Contain(pair.Key));
                Assert.That(Clips.Variants(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            }
        }

        [Test] public void GachaClipsAreCleanMonoWithoutClicks()
        {
            foreach (var pair in GachaVariants)
                for (var v = 0; v < pair.Value; v++)
                {
                    var label = pair.Key + "_" + v; var wav = Clips.Read(pair.Key, v);
                    Assert.That((wav.Channels, wav.Bits, wav.Rate), Is.EqualTo((1, 16, 44100)), label);
                    Assert.That(wav.Samples.Length, Is.GreaterThan(2000), label);
                    Assert.That(wav.Samples[0], Is.EqualTo(0), label + " starts silent");
                    Assert.That(wav.Samples[wav.Samples.Length - 1], Is.EqualTo(0), label + " ends silent");
                    Assert.That(wav.Peak, Is.InRange(.03, .43), label + " level");
                    Assert.That(Math.Abs(wav.Mean), Is.LessThan(.002), label + " has no DC offset");
                    Assert.That(File.Exists(Clips.PathOf(label) + ".meta"), label + " has its .meta");
                }
        }

        [Test] public void AudioMetaGuidsAreUnique()
        {
            var guids = Directory.GetFiles(Clips.AudioDir, "*.meta")
                .Select(path => File.ReadAllLines(path).First(l => l.StartsWith("guid:", StringComparison.Ordinal))).ToList();
            Assert.That(guids.Count, Is.GreaterThan(GachaVariants.Values.Sum()));
            Assert.That(guids.Distinct().Count(), Is.EqualTo(guids.Count));
        }

        [Test] public void TheChurnEndsOnTheKaChunk()
        {
            for (var v = 0; v < GachaVariants["gacha_churn"]; v++)
                Assert.That(Clips.Read("gacha_churn", v).Seconds, Is.EqualTo(GachaScene.CrankT * GachaScene.ThunkAt).Within(.03));
        }

        [Test] public void RarerStingersAreLongerAndLouder()
        {
            var stingers = Enumerable.Range(0, 5).Select(v => Clips.Read("gacha_tier", v)).ToList();
            for (var v = 1; v < 5; v++)
            {
                Assert.That(stingers[v].Seconds, Is.GreaterThan(stingers[v - 1].Seconds), "tier " + (v + 1));
                Assert.That(stingers[v].Loudness, Is.GreaterThan(stingers[v - 1].Loudness), "tier " + (v + 1));
            }
            Assert.That(Clips.Read("gacha_thunk", 0).Loudness, Is.GreaterThan(Clips.Read("gacha_crank", 5).Loudness), "the KA-CHUNK is the loudest beat of the crank");
        }

        // ------------------------------------------------------------------ SoundPlayer rules

        [Test] public void SteppedEffectsPickTheirVariantInOrder()
        {
            Assert.That(Enumerable.Range(0, 12).Select(k => SoundPlayer.StepVariant(k, 12, 6)), Is.EqualTo(RisingCrank));
            Assert.That(Enumerable.Range(0, 5).Select(k => SoundPlayer.StepVariant(k, 5, 5)), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            Assert.That(Enumerable.Range(0, 5).Select(k => SoundPlayer.StepVariant(k, 5, 3)), Is.EqualTo(new[] { 0, 0, 1, 1, 2 }), "fewer clips: proportional");
            Assert.That(SoundPlayer.StepVariant(-3, 5, 5), Is.EqualTo(0));
            Assert.That(SoundPlayer.StepVariant(99, 5, 5), Is.EqualTo(4));
            Assert.That(SoundPlayer.StepVariant(3, 5, 1), Is.EqualTo(0));
            Assert.That(SoundPlayer.StepVariant(3, 5, 0), Is.EqualTo(0));
            Assert.That(SoundPlayer.StepVariant(3, 1, 5), Is.EqualTo(0));
            Assert.That(SoundPlayer.StepVariant(int.MaxValue - 1, int.MaxValue, 8), Is.EqualTo(7), "no overflow");
        }

        [Test] public void EachRarityPlaysItsOwnStinger()
        {
            var audio = new RecordingAudio(); var sound = new SoundPlayer(audio, Sfx);
            for (var rarity = 1; rarity <= 5; rarity++) { audio.Advance(3); sound.PlayGachaTier(rarity); }
            Assert.That(audio.Of("gacha_tier").Select(c => c.Variant), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            audio.Advance(3); sound.PlayGachaWobble(4); sound.PlayGachaBounce(1); sound.PlayGachaHop(true);
            Assert.That(audio.Played.Skip(5).Select(c => (c.Name, c.Variant)), Is.EqualTo(new[] { ("gacha_wobble", 4), ("gacha_bounce", 1), ("gacha_hop", 1) }));
        }

        [Test] public void GachaCuesLayerBeyondTheUsualThreeVoicesButMenusAndArcsDoNot()
        {
            var audio = new RecordingAudio(); var sound = new SoundPlayer(audio, Sfx);
            sound.PlayGachaChurn(); sound.PlayGachaCoinSlot(); sound.PlayGachaCoinToss();
            Assert.That(audio.BusyVoices, Is.EqualTo(3));
            sound.PlayNav(); sound.PlayGachaZap(true);
            sound.PlayGachaCrank(0, 12); sound.PlayGachaThunk(); sound.PlayGachaTease();
            Assert.That(audio.Names, Is.EqualTo(new[] { "gacha_churn", "gacha_coin_slot", "gacha_coin_toss", "gacha_crank", "gacha_thunk", "gacha_tease" }));
            sound.PlayGachaOpen();
            Assert.That(audio.Names.Count, Is.EqualTo(SoundPlayer.GachaVoices), "even the show stops at its own limit");
        }

        [Test] public void FastCrankTicksAllPlayButArcsAreSpaced()
        {
            var audio = new RecordingAudio(); var sound = new SoundPlayer(audio, Sfx);
            for (var k = 0; k < 12; k++) { sound.PlayGachaCrank(k, 12); audio.Advance(.05); }
            Assert.That(audio.Of("gacha_crank").Select(c => c.Variant), Is.EqualTo(RisingCrank));
            audio.Advance(1);
            for (var i = 0; i < 20; i++) { sound.PlayGachaZap(i % 2 == 0); audio.Advance(.05); }
            var zaps = audio.Of("gacha_zap").ToList();
            Assert.That(zaps.Count, Is.InRange(6, 8));
            for (var i = 1; i < zaps.Count; i++) Assert.That(zaps[i].At - zaps[i - 1].At, Is.GreaterThanOrEqualTo(.11 - 1e-6));
        }

        [Test] public void SmallArcsPlaySofterThanBigOnes()
        {
            var audio = new RecordingAudio(); var sound = new SoundPlayer(audio, .8f);
            sound.PlayGachaZap(false); audio.Advance(.2); sound.PlayGachaZap(true);
            Assert.That(audio.Of("gacha_zap").Select(c => c.Volume), Is.EqualTo(new[] { .8f * SoundPlayer.SmallZapGain, .8f }).Within(1e-5));
        }

        [Test] public void AStingerWaitsForAFreeVoiceInsteadOfBeingLost()
        {
            var audio = new RecordingAudio(); var sound = new SoundPlayer(audio, Sfx);
            sound.PlayGachaChurn(); sound.PlayGachaCoinSlot(); sound.PlayGachaCoinToss(); sound.PlayGachaThunk(); sound.PlayGachaTease(); sound.PlayGachaDust();
            sound.PlayGachaTier(5);
            Assert.That(audio.Of("gacha_tier"), Is.Empty);
            var firstFree = audio.Played.Min(c => audio.Duration(c.Name, c.Variant));
            Assert.That(firstFree, Is.LessThan(1.0), "a cue waits at most one second");
            audio.Advance(firstFree + .01); sound.Update();
            Assert.That(audio.Of("gacha_tier").Select(c => c.Variant), Is.EqualTo(new[] { 4 }), "it keeps its rarity while waiting");
        }

        [Test] public void StoppingTheChurnReachesTheBackend()
        {
            var audio = new RecordingAudio(); var sound = new SoundPlayer(audio, Sfx);
            sound.PlayGachaChurn(); sound.StopGachaChurn();
            Assert.That(audio.Stopped, Is.EqualTo(new[] { "gacha_churn" }));
            Assert.That(audio.BusyVoices, Is.Zero);
        }

        [Test] public void MissingClipsMuteAndNoBackendAreSilent()
        {
            var empty = new RecordingAudio(new Dictionary<string, double[]>()); var sound = new SoundPlayer(empty, Sfx);
            sound.PlayGachaTier(3); sound.PlayGachaCrank(5, 12); sound.PlayGachaZap(true); sound.StopGachaChurn(); sound.Update();
            Assert.That(empty.Played, Is.Empty);
            var audio = new RecordingAudio(); var muted = new SoundPlayer(audio, 0f);
            muted.PlayGachaTier(5); muted.PlayGachaThunk(); muted.Update();
            Assert.That(audio.Played, Is.Empty);
            var none = new SoundPlayer(null);
            Assert.DoesNotThrow(() => { none.PlayGachaTier(2); none.PlayGachaWobble(9); none.StopGachaChurn(); none.Update(); });
        }

        // ------------------------------------------------------------------ the show

        [Test] public void EveryBeatOfAPullHasItsOwnSound()
        {
            var tiers = new HashSet<int>(); var arcs = 0;
            var pulls = Enumerable.Range(1, 30).Select(s => (seed: s, kind: PullKind.Stellar)).Concat(Enumerable.Range(1, 12).Select(s => (seed: 100 + s, kind: PullKind.Normal)));
            foreach (var (seed, kind) in pulls)
            {
                var show = new HeadlessGacha(seed);
                Assert.That(show.Scene.StartPull(kind), Is.True);
                show.RunUntil(() => show.Scene.Phase == "reveal", 14f);
                var relic = show.Scene.Result.Relic; var tier = Math.Max(1, Math.Min(5, relic.Definition.Rarity));
                var isGolden = relic.Chroma != ChromaKind.None;
                tiers.Add(tier);
                arcs += show.Audio.Of("gacha_zap").Count();
                CheckShow(show.Audio, tier, isGolden, kind + " seed " + seed + " tier " + tier);
            }
            Assert.That(tiers, Is.SupersetOf(new[] { 1, 2, 3 }));
            Assert.That(tiers.Any(t => t >= 4), "an Épica or Legendaria was pulled (tease, big arcs)");
            Assert.That(arcs, Is.GreaterThan(0));
        }

        private static void CheckShow(RecordingAudio audio, int tier, bool golden, string label)
        {
            float At(string name) => audio.Of(name).Single().At;
            Assert.That(audio.Names.Intersect(Borrowed), Is.Empty, label + ": no borrowed sounds left");
            Assert.That(At("gacha_coin_toss"), Is.EqualTo(0f).Within(1e-5), label);
            Assert.That(At("gacha_coin_slot"), Is.EqualTo(GachaScene.CoinT * .72f).Within(Dt * 1.5), label + ": clink with the sparks");
            var churn = audio.Of("gacha_churn").Single(); var thunk = At("gacha_thunk");
            Assert.That(churn.At, Is.EqualTo(GachaScene.CoinT).Within(Dt * 2.5), label + ": churn with the crank");
            Assert.That(thunk - churn.At, Is.EqualTo(GachaScene.CrankT * GachaScene.ThunkAt).Within(Dt * 2.5), label + ": KA-CHUNK");
            Assert.That(churn.At + audio.Duration(churn.Name, churn.Variant) - thunk, Is.InRange(-.01, .06), label + ": the churn ends on the KA-CHUNK");
            var ticks = audio.Of("gacha_crank").ToList();
            Assert.That(ticks.Select(c => c.Variant), Is.EqualTo(RisingCrank), label + ": twelve ticks climbing");
            Assert.That(ticks.All(c => c.At >= churn.At - 1e-5 && c.At < thunk), label);
            var zaps = audio.Of("gacha_zap").ToList();
            Assert.That(zaps.All(z => z.At > churn.At && z.At <= thunk + 1e-5), label + ": arcs only while cranking");
            Assert.That(zaps.All(z => Math.Abs(z.Volume - Sfx * (tier >= 4 ? 1f : SoundPlayer.SmallZapGain)) < 1e-4), label + ": arc size");
            for (var i = 1; i < zaps.Count; i++) Assert.That(zaps[i].At - zaps[i - 1].At, Is.GreaterThanOrEqualTo(.11 - 1e-5), label);
            Assert.That(audio.Of("gacha_tease").Count(), Is.EqualTo(tier >= 4 ? 1 : 0), label + ": tease only for Épica+");
            if (tier >= 4) Assert.That(At("gacha_tease") - churn.At, Is.EqualTo(GachaScene.CrankT * .55f).Within(Dt * 2.5), label);
            var pop = At("gacha_pop");
            Assert.That(pop - thunk, Is.EqualTo(GachaScene.CrankT * (1 - GachaScene.ThunkAt)).Within(Dt * 2.5), label + ": out of the chute");
            var bounces = audio.Of("gacha_bounce").ToList();
            Assert.That(bounces.Select(c => c.Variant), Is.EqualTo(new[] { 0, 1 }), label + ": two bounces");
            Assert.That(bounces[0].At, Is.GreaterThan(pop), label);
            var hop = audio.Of("gacha_hop").Single();
            Assert.That(hop.Variant, Is.EqualTo(tier >= 2 ? 1 : 0), label + ": sparkle trail from Poco común");
            Assert.That(hop.At - pop, Is.EqualTo(GachaScene.FallT).Within(Dt * 2.5), label);
            var wobbles = audio.Of("gacha_wobble").ToList();
            Assert.That(wobbles.Select(c => c.Variant), Is.EqualTo(Enumerable.Range(0, tier)), label + ": one rising rock per tier");
            for (var k = 1; k < wobbles.Count; k++) Assert.That(wobbles[k].At - wobbles[k - 1].At, Is.EqualTo(GachaScene.ShakeT + .12f).Within(Dt * 1.5), label);
            var open = At("gacha_open");
            Assert.That(open, Is.GreaterThan(wobbles.Last().At), label);
            var stinger = audio.Of("gacha_tier").Single();
            Assert.That((stinger.At, stinger.Variant), Is.EqualTo((open, tier - 1)), label + ": the stinger of its rarity");
            Assert.That(audio.Of("gacha_golden").Select(c => c.At), Is.EqualTo(golden ? new[] { open } : new float[0]), label + ": golden shimmer");
        }

        [Test] public void KeepingFliesToTheCounterAndClicksIntoPlace()
        {
            var show = new HeadlessGacha(5);
            show.Scene.StartPull(PullKind.Normal); show.Scene.SkipToReveal(); show.Audio.Clear();
            Assert.That(show.Scene.Keep(), Is.True);
            Assert.That(show.Audio.Names, Is.EqualTo(new[] { "gacha_keep" }));
            show.RunUntil(() => show.Scene.Phase == "idle", 2f);
            Assert.That(show.Audio.Names, Is.EqualTo(new[] { "gacha_keep", "gacha_stash" }));
            Assert.That(show.Audio.Of("gacha_stash").Single().At, Is.EqualTo(GachaScene.CollectT).Within(Dt * 1.5));
        }

        [Test] public void DecliningCrumblesIntoDust()
        {
            var show = new HeadlessGacha(6);
            show.Scene.StartPull(PullKind.Normal); show.Scene.SkipToReveal(); show.Audio.Clear();
            Assert.That(show.Scene.Decline(), Is.True);
            show.RunUntil(() => show.Scene.Phase == "idle", 2f);
            Assert.That(show.Audio.Names, Is.EqualTo(new[] { "gacha_dust" }));
        }

        [Test] public void SkippingCutsTheChurnAndTheCrank()
        {
            var show = new HeadlessGacha(7);
            show.Scene.StartPull(PullKind.Stellar);
            show.RunFor(GachaScene.CoinT + .6f);
            Assert.That(show.Scene.Phase, Is.EqualTo("crank"));
            var ticks = show.Audio.Of("gacha_crank").Count();
            Assert.That(ticks, Is.InRange(2, 11));
            show.Scene.Advance();
            Assert.That(show.Audio.Stopped, Is.EqualTo(new[] { "gacha_churn" }));
            show.RunUntil(() => show.Scene.Phase == "reveal", 10f);
            Assert.That(show.Audio.Of("gacha_crank").Count(), Is.EqualTo(ticks));
            Assert.That(show.Audio.Names, Has.None.EqualTo("gacha_thunk").And.None.EqualTo("gacha_pop").And.None.EqualTo("gacha_hop"));
            var tier = show.Scene.Result.Rarity;
            Assert.That(show.Audio.Of("gacha_tier").Single().Variant, Is.EqualTo(tier - 1), "a skipped pull still sings its rarity");
        }

        [Test] public void ClickingTheCapsuleOpensItRightAway()
        {
            var show = new HeadlessGacha(8);
            show.Scene.StartPull(PullKind.Normal);
            show.RunUntil(() => show.Scene.Phase == "present", 5f);
            show.RunFor(.2f);
            show.Scene.HandleEvent(new InputEvent(EventType.MouseDown, 742, 292, 1));
            Assert.That(show.Scene.Phase, Is.EqualTo("open"));
            Assert.That(show.Audio.Names.Skip(show.Audio.Names.Count - 2), Is.EqualTo(new[] { "gacha_open", "gacha_tier" }));
        }

        [Test] public void HoveringAButtonTicksOnceAndOnlyWhenItCanBePressed()
        {
            var show = new HeadlessGacha(9);
            int Navs() => show.Audio.Of("nav").Count();
            show.Move(1106, 196); Assert.That(Navs(), Is.EqualTo(1), "Tirada normal");
            show.RunFor(.2f); show.Move(1110, 200); Assert.That(Navs(), Is.EqualTo(1), "same button");
            show.RunFor(.2f); show.Move(1106, 302); Assert.That(Navs(), Is.EqualTo(2), "Tirada estelar");
            show.RunFor(.2f); show.Move(10, 10); Assert.That(Navs(), Is.EqualTo(2), "nothing under the pointer");
            show.RunFor(.2f); show.Move(1175, 676); Assert.That(Navs(), Is.EqualTo(3), "Salir");
            show.Scene.StartPull(PullKind.Normal);
            show.RunFor(.2f); show.Move(1106, 196); show.RunFor(.2f); show.Move(1106, 302);
            Assert.That(Navs(), Is.EqualTo(3), "pull buttons are silent during the show");
        }

        [Test] public void WithoutGoldTheMachineBuzzesInsteadOfTakingACoin()
        {
            Tuning.Current.StartingGold = 0;
            var show = new HeadlessGacha(10);
            Assert.That(show.Scene.StartPull(PullKind.Normal), Is.False);
            Assert.That(show.Audio.Names, Is.EqualTo(new[] { "error" }));
        }

        [Test] public void AThousandRandomPullsNeverBreakTheMixer()
        {
            var show = new HeadlessGacha(11); show.Run.GainGold(1_000_000_000);
            var rng = new Random(4);
            for (var step = 0; step < 6000; step++)
            {
                var roll = rng.Next(100);
                if (roll < 3) show.Scene.StartPull(rng.Next(2) == 0 ? PullKind.Normal : PullKind.Stellar);
                else if (roll < 5) show.Scene.Advance();
                else if (roll < 6) show.Scene.Keep();
                else if (roll < 7) show.Scene.Decline();
                else if (roll < 9) show.Move(rng.Next(1280), rng.Next(720));
                show.Frames(1);
                Assert.That(show.Audio.BusyVoices, Is.LessThanOrEqualTo(RecordingAudio.Voices));
            }
            Assert.That(show.Scene.PullsMade, Is.GreaterThan(5));
            Assert.That(show.Audio.Played.All(c => c.Variant >= 0 && c.Variant < show.Audio.Variants(c.Name)));
            Assert.That(show.Audio.Names.Intersect(Borrowed.Where(n => n != "nav" && n != "cancel")), Is.Empty);
        }
    }

    /// <summary>A GachaScene with real assets and timing, a null renderer and a recording mixer.</summary>
    internal sealed class HeadlessGacha
    {
        private static Art art;
        private static NullRender render;
        public RecordingAudio Audio { get; }
        public SoundPlayer Sound { get; }
        public RunState Run { get; }
        public GachaScene Scene { get; }

        public HeadlessGacha(int seed)
        {
            if (art == null) { render = new NullRender(); art = new Art(render, new FileAssets()); art.LoadCore(); }
            Audio = new RecordingAudio();
            Sound = new SoundPlayer(Audio, .7f, 0f);
            var game = new GameContext(new Canvas(render), art, Sound, new Preferences(), null);
            Run = new RunState(HeroKind.Warrior, seed);
            Scene = new GachaScene(game, Run, seed);
        }

        public void Frames(int count)
        {
            for (var i = 0; i < count; i++) { Audio.Advance(1.0 / 60); Scene.Update(1f / 60f); Sound.Update(); }
        }
        public void RunFor(float seconds) => Frames((int)Math.Ceiling(seconds * 60 - 1e-4));
        public void RunUntil(Func<bool> done, float limit)
        {
            for (var frames = 0; !done(); frames++)
            {
                if (frames > limit * 60) Assert.Fail("Timed out in phase " + Scene.Phase);
                Frames(1);
            }
        }
        public void Move(float x, float y) => Scene.HandleEvent(new InputEvent(EventType.MouseMove, x, y));
    }

    /// <summary>Mixer that records what plays and when; eight voices with the real clip lengths.</summary>
    internal sealed class RecordingAudio : IAudioBackend
    {
        public const int Voices = 8;
        public sealed class Cue
        {
            public string Name; public int Variant; public float Volume; public float At;
            public override string ToString() => Name + "_" + Variant + " @" + At.ToString("0.000");
        }
        private readonly Dictionary<string, double[]> durations;
        private readonly List<(string name, double end)> voices = new List<(string, double)>();
        private double now;
        public List<Cue> Played { get; } = new List<Cue>();
        public List<string> Stopped { get; } = new List<string>();
        public List<string> Names => Played.Select(c => c.Name).ToList();

        public RecordingAudio(Dictionary<string, double[]> durations = null) { this.durations = durations ?? Clips.Durations(); }
        public void Advance(double seconds) => now += seconds;
        public void Clear() { Played.Clear(); Stopped.Clear(); }
        public IEnumerable<Cue> Of(string name) => Played.Where(c => c.Name == name);
        public double Duration(string name, int variant) => durations[name][variant];

        public long Ticks => (long)Math.Round(now * 1000);
        public int Variants(string name) => durations.TryGetValue(name, out var d) ? d.Length : 0;
        public int BusyVoices { get { voices.RemoveAll(v => v.end <= now + 1e-9); return voices.Count; } }
        public bool Play(string name, int variant, float volume)
        {
            if (BusyVoices >= Voices) return false;
            voices.Add((name, now + durations[name][variant]));
            Played.Add(new Cue { Name = name, Variant = variant, Volume = volume, At = (float)now });
            return true;
        }
        public void Stop(string name) { Stopped.Add(name); voices.RemoveAll(v => v.name == name); }
        public void StartMusic(float volume) { }
        public void StopMusic() { }
        public void SetMusicVolume(float volume) { }
    }

    /// <summary>The project's audio clips and images, read straight from Assets/Resources/Game.</summary>
    internal static class Clips
    {
        private static string root;
        public static string Root
        {
            get
            {
                if (root != null) return root;
                foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
                    for (var folder = new DirectoryInfo(start); folder != null; folder = folder.Parent)
                        if (Directory.Exists(Path.Combine(folder.FullName, "Assets/Resources/Game/Audio"))) return root = Path.Combine(folder.FullName, "Assets/Resources/Game");
                throw new DirectoryNotFoundException("Assets/Resources/Game");
            }
        }
        public static string AudioDir => Path.Combine(Root, "Audio");
        public static string PathOf(string clip) => Path.Combine(AudioDir, clip + ".wav");
        public static int Variants(string name) { var n = 0; while (n < 16 && File.Exists(PathOf(name + "_" + n))) n++; return n; }
        public static Wav Read(string name, int variant) => Wav.Load(PathOf(name + "_" + variant));

        private static Dictionary<string, double[]> durations;
        public static Dictionary<string, double[]> Durations()
        {
            if (durations != null) return durations;
            durations = new Dictionary<string, double[]>();
            foreach (var name in SoundPlayer.Effects)
                durations[name] = Enumerable.Range(0, Variants(name)).Select(v => Read(name, v).Seconds).ToArray();
            return durations;
        }

        /// <summary>Width and height from the PNG header (64×64 when the file is missing).</summary>
        public static (int w, int h) PngSize(string key)
        {
            var path = Path.Combine(Root, key + ".png");
            if (!File.Exists(path)) return (64, 64);
            var head = new byte[24];
            using (var file = File.OpenRead(path)) file.Read(head, 0, head.Length);
            int Big(int at) => head[at] << 24 | head[at + 1] << 16 | head[at + 2] << 8 | head[at + 3];
            return (Big(16), Big(20));
        }
    }

    internal sealed class Wav
    {
        public int Channels, Bits, Rate;
        public short[] Samples;
        public double Seconds => Samples.Length / (double)Rate;
        public double Peak => Samples.Max(s => Math.Abs((int)s)) / 32768.0;
        public double Mean => Samples.Average(s => (double)s) / 32768.0;
        /// <summary>RMS of the loudest 50 ms (the generator's level measure).</summary>
        public double Loudness
        {
            get
            {
                var window = Rate / 20; var best = 0.0;
                for (var start = 0; start < Math.Max(1, Samples.Length - window); start += 220)
                {
                    double sum = 0; var end = Math.Min(Samples.Length, start + window);
                    for (var i = start; i < end; i++) { var s = Samples[i] / 32768.0; sum += s * s; }
                    best = Math.Max(best, sum / (end - start));
                }
                return Math.Sqrt(best);
            }
        }

        public static Wav Load(string path)
        {
            var bytes = File.ReadAllBytes(path); var wav = new Wav();
            for (var at = 12; at + 8 <= bytes.Length;)
            {
                var id = System.Text.Encoding.ASCII.GetString(bytes, at, 4); var size = BitConverter.ToInt32(bytes, at + 4);
                if (id == "fmt ") { wav.Channels = BitConverter.ToInt16(bytes, at + 10); wav.Rate = BitConverter.ToInt32(bytes, at + 12); wav.Bits = BitConverter.ToInt16(bytes, at + 22); }
                else if (id == "data")
                {
                    wav.Samples = new short[size / 2];
                    Buffer.BlockCopy(bytes, at + 8, wav.Samples, 0, size / 2 * 2);
                    break;
                }
                at += 8 + size + (size & 1);
            }
            return wav;
        }
    }

    internal sealed class FileAssets : IAssetSource
    {
        public string ReadText(string key) => File.ReadAllText(Path.Combine(Clips.Root, key + ".json"));
    }

    internal sealed class NullRender : IRenderBackend
    {
        private int next = 1;
        public Tex WhiteTexture { get; } = new Tex(0, "white", 1, 1);
        public Tex LoadTexture(string key) { var (w, h) = Clips.PngSize(key); return new Tex(next++, key, w, h); }
        public Tex CreateTexture(string key, int width, int height, Rgba[] pixels, bool smooth = false) => new Tex(next++, key, width, height, smooth);
        public void BeginFrame() { }
        public void Quad(Tex tex, float u0, float v0, float u1, float v1, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, Rgba color, Blend blend) { }
        public void Triangle(float x0, float y0, float x1, float y1, float x2, float y2, Rgba color, Blend blend) { }
        public void TexTriangle(Tex tex, float x0, float y0, float u0, float v0, float x1, float y1, float u1, float v1, float x2, float y2, float u2, float v2, Rgba color, Blend blend) { }
        public Tex CreateTarget(string key, int width, int height) => new Tex(next++, key, width, height, false, true);
        public void ReleaseTarget(Tex target) { }
        public void BeginTarget(Tex target) { }
        public void EndTarget() { }
        public void SetClip(RectF? clip) { }
        public void EndFrame() { }
    }
}
