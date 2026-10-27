using System;
using System.Collections.Generic;

namespace LastWish.Presentation
{
    /// <summary>What the presentation needs from an audio backend.</summary>
    public interface IAudioBackend
    {
        /// <summary>Number of loaded variants of an effect (0 when missing).</summary>
        int Variants(string name);
        /// <summary>Voices currently playing effects.</summary>
        int BusyVoices { get; }
        bool Play(string name, int variant, float volume);
        /// <summary>Fade out (a few ms, no click) every voice playing this effect.</summary>
        void Stop(string name);
        void StartMusic(float volume);
        void StopMusic();
        void SetMusicVolume(float volume);
        /// <summary>Milliseconds since start (pygame.time.get_ticks).</summary>
        long Ticks { get; }
    }

    /// <summary>infrastructure/audio.SoundPlayer: one shared instance; voice limit, repeat gaps, important cues wait.
    /// Effects with ordered variants (a rising crank, one stinger per rarity) are picked by step instead of round-robin.</summary>
    public sealed class SoundPlayer
    {
        /// <summary>Every effect the backend loads: Game/Audio/&lt;name&gt;_0.wav, _1… up to the first missing variant.</summary>
        public static readonly string[] Effects = {
            "card", "attack", "block", "death", "end_turn", "hit", "win", "nav", "confirm", "cancel", "purchase", "open_pack", "reward", "error", "travel",
            "gacha_coin_toss", "gacha_coin_slot", "gacha_churn", "gacha_crank", "gacha_zap", "gacha_tease", "gacha_thunk", "gacha_pop",
            "gacha_bounce", "gacha_hop", "gacha_wobble", "gacha_open", "gacha_tier", "gacha_golden", "gacha_keep", "gacha_stash", "gacha_dust" };
        private const int MaxVoices = 3, RepeatGap = 80;
        /// <summary>The gachapón show layers a churn, ratchet ticks and stingers, so its cues may use more voices.</summary>
        public const int GachaVoices = 6;
        /// <summary>Gain of the small electric arcs (the big Épica+ arcs play at full gain).</summary>
        public const float SmallZapGain = .55f;
        private static readonly Dictionary<string, int> Gaps = new Dictionary<string, int> {
            { "nav", 120 }, { "error", 300 }, { "gacha_crank", 30 }, { "gacha_zap", 110 }, { "gacha_bounce", 60 } };
        private static readonly HashSet<string> Important = new HashSet<string> { "win", "purchase", "open_pack", "reward", "travel", "gacha_tier", "gacha_stash" };
        /// <summary>Garnish keeps the usual voice limit, so it never crowds out the main cues.</summary>
        private static readonly HashSet<string> Garnish = new HashSet<string> { "gacha_zap" };
        private readonly IAudioBackend backend;
        private readonly Dictionary<string, int> nextVariant = new Dictionary<string, int>();
        private readonly Dictionary<string, long> lastPlayed = new Dictionary<string, long>();
        private (string name, int variant, float gain, long at)? pending;
        private bool musicStarted;
        public float SfxVolume { get; private set; }
        public float MusicVolume { get; private set; }

        public SoundPlayer(IAudioBackend backend, float sfxVolume = .7f, float musicVolume = .35f)
        { this.backend = backend; SfxVolume = Volume(sfxVolume); MusicVolume = Volume(musicVolume); }

        private static float Volume(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : Math.Max(0f, Math.Min(1f, v));

        public void SetVolumes(float sfx, float music)
        {
            SfxVolume = Volume(sfx); MusicVolume = Volume(music);
            if (SfxVolume == 0) pending = null;
            backend?.SetMusicVolume(MusicVolume);
        }

        public void StartMusic() { if (musicStarted || backend == null) return; backend.StartMusic(MusicVolume); musicStarted = true; }
        public void StopMusic() { if (musicStarted) backend?.StopMusic(); musicStarted = false; pending = null; }

        public void Update()
        {
            if (pending == null || backend == null) return;
            var now = backend.Ticks;
            var (name, variant, gain, at) = pending.Value;
            if (now - at > 1000 || TryPlay(name, variant, gain, now)) pending = null;
        }

        public void PlayCard() => Play("card");
        public void PlayAttack() => Play("attack");
        public void PlayBlock() => Play("block");
        public void PlayDeath() => Play("death");
        public void PlayEndTurn() => Play("end_turn");
        public void PlayHit() => Play("hit");
        public void PlayWin() => Play("win");
        public void PlayNav() => Play("nav");
        public void PlayConfirm() => Play("confirm");
        public void PlayCancel() => Play("cancel");
        public void PlayPurchase() => Play("purchase");
        public void PlayOpenPack() => Play("open_pack");
        public void PlayReward() => Play("reward");
        public void PlayError() => Play("error");
        public void PlayTravel() => Play("travel");

        // Gachapón, in the order of the show (Tools/GenerateGachaAudio.py builds every clip).
        /// <summary>The coin leaves the purse spinning (paying for a pull).</summary>
        public void PlayGachaCoinToss() => Play("gacha_coin_toss");
        /// <summary>Clink into the slot, rattle down inside, the bulbs light up.</summary>
        public void PlayGachaCoinSlot() => Play("gacha_coin_slot");
        /// <summary>Capsules tumbling in the vortex with a riser that ends on the KA-CHUNK.</summary>
        public void PlayGachaChurn() => Play("gacha_churn");
        /// <summary>Skipping the show cuts the churn with a short fade.</summary>
        public void StopGachaChurn() => Stop("gacha_churn");
        /// <summary>One ratchet tooth; the pitch climbs with the crank.</summary>
        public void PlayGachaCrank(int tick, int ticks) => PlayStep("gacha_crank", tick, ticks);
        public void PlayGachaZap(bool big) => Play("gacha_zap", -1, big ? 1f : SmallZapGain);
        /// <summary>Épica and better: light is sucked into the glass.</summary>
        public void PlayGachaTease() => Play("gacha_tease");
        public void PlayGachaThunk() => Play("gacha_thunk");
        /// <summary>The capsule pushes the chute flap and plops out.</summary>
        public void PlayGachaPop() => Play("gacha_pop");
        /// <summary>First (0) and second (1) bounce on the floor.</summary>
        public void PlayGachaBounce(int bounce) => PlayStep("gacha_bounce", bounce, 2);
        /// <summary>The hop onto the stage; Poco común and better leave a trail of sparkles.</summary>
        public void PlayGachaHop(bool sparkle) => PlayStep("gacha_hop", sparkle ? 1 : 0, 2);
        /// <summary>Rock k of the capsule on the stage (one per tier step), each a note higher.</summary>
        public void PlayGachaWobble(int rock) => PlayStep("gacha_wobble", rock, 5);
        public void PlayGachaOpen() => Play("gacha_open");
        /// <summary>The reveal stinger of a rarity (1 Común … 5 Legendaria).</summary>
        public void PlayGachaTier(int rarity) => PlayStep("gacha_tier", rarity - 1, 5);
        public void PlayGachaGolden() => Play("gacha_golden");
        /// <summary>Quedármela: the relic flies to the relic counter.</summary>
        public void PlayGachaKeep() => Play("gacha_keep");
        /// <summary>The relic lands in the relic counter.</summary>
        public void PlayGachaStash() => Play("gacha_stash");
        /// <summary>Rechazar: the relic crumbles into dust.</summary>
        public void PlayGachaDust() => Play("gacha_dust");

        /// <summary>Variant for step <paramref name="step"/> of <paramref name="steps"/> when <paramref name="variants"/> clips exist (proportional, clamped).</summary>
        public static int StepVariant(int step, int steps, int variants)
        {
            if (variants <= 1 || steps <= 1) return 0;
            var s = Math.Max(0, Math.Min(steps - 1, step));
            return Math.Min(variants - 1, (int)((long)s * variants / steps));
        }

        /// <summary>Voices that may already be busy for this effect to start.</summary>
        public static int VoiceLimit(string name) =>
            name.StartsWith("gacha_", StringComparison.Ordinal) && !Garnish.Contains(name) ? GachaVoices : MaxVoices;

        private void PlayStep(string name, int step, int steps)
        {
            if (backend == null) return;
            Play(name, StepVariant(step, steps, backend.Variants(name)));
        }

        private void Play(string name, int variant = -1, float gain = 1f)
        {
            if (backend == null) return;
            var now = backend.Ticks;
            if (!TryPlay(name, variant, gain, now) && Important.Contains(name))
                if (pending == null || name == "win") pending = (name, variant, gain, now);
        }

        private void Stop(string name)
        {
            if (backend == null) return;
            backend.Stop(name);
            if (pending?.name == name) pending = null;
        }

        /// <summary>True means handled (including mute/cooldown); false means the mixer is busy.</summary>
        private bool TryPlay(string name, int variant, float gain, long now)
        {
            var variants = backend.Variants(name);
            if (variants == 0 || SfxVolume == 0 || !(gain > 0)) return true;
            var gap = Gaps.TryGetValue(name, out var g) ? g : RepeatGap;
            if (now - (lastPlayed.TryGetValue(name, out var last) ? last : -gap) < gap) return true;
            if (backend.BusyVoices >= VoiceLimit(name)) return false;
            var index = variant >= 0 ? Math.Min(variant, variants - 1) : (nextVariant.TryGetValue(name, out var i) ? i : 0) % variants;
            if (!backend.Play(name, index, Volume(SfxVolume * gain))) return false;
            lastPlayed[name] = now;
            if (variant < 0) nextVariant[name] = (index + 1) % variants;
            return true;
        }
    }
}
