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
        void StartMusic(float volume);
        void StopMusic();
        void SetMusicVolume(float volume);
        /// <summary>Milliseconds since start (pygame.time.get_ticks).</summary>
        long Ticks { get; }
    }

    /// <summary>infrastructure/audio.SoundPlayer: one shared instance; voice limit, repeat gaps, important cues wait.</summary>
    public sealed class SoundPlayer
    {
        private const int MaxVoices = 3, RepeatGap = 80;
        private static readonly Dictionary<string, int> Gaps = new Dictionary<string, int> { { "nav", 120 }, { "error", 300 } };
        private static readonly HashSet<string> Important = new HashSet<string> { "win", "purchase", "open_pack", "reward", "travel" };
        private readonly IAudioBackend backend;
        private readonly Dictionary<string, int> nextVariant = new Dictionary<string, int>();
        private readonly Dictionary<string, long> lastPlayed = new Dictionary<string, long>();
        private (string name, long at)? pending;
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
            if (now - pending.Value.at > 1000 || TryPlay(pending.Value.name, now)) pending = null;
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

        private void Play(string name)
        {
            if (backend == null) return;
            var now = backend.Ticks;
            if (!TryPlay(name, now) && Important.Contains(name))
                if (pending == null || name == "win") pending = (name, now);
        }

        private bool TryPlay(string name, long now)
        {
            var variants = backend.Variants(name);
            if (variants == 0 || SfxVolume == 0) return true;
            var gap = Gaps.TryGetValue(name, out var g) ? g : RepeatGap;
            if (now - (lastPlayed.TryGetValue(name, out var last) ? last : -gap) < gap) return true;
            if (backend.BusyVoices >= MaxVoices) return false;
            var index = nextVariant.TryGetValue(name, out var i) ? i : 0;
            if (!backend.Play(name, index % variants, SfxVolume)) return false;
            lastPlayed[name] = now; nextVariant[name] = (index + 1) % variants;
            return true;
        }
    }
}
