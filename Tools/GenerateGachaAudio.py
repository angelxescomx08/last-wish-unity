"""Synthesize the gachapon sound effects of Last Wish (stdlib only, deterministic).

Run from the project root:
    python Tools/GenerateGachaAudio.py                  # write Assets/Resources/Game/Audio/gacha_*.wav (+ .meta)
    python Tools/GenerateGachaAudio.py --check          # verify the files on disk match the recipes
    python Tools/GenerateGachaAudio.py --preview DIR    # also mix whole pulls at the scene's timing

Every sound is built from code (FM bells, glockenspiel bars, filtered noise, damped modes,
additive brass and choir, a Freeverb-style hall). Nothing is recorded or sampled.

Direction (user, 2026-10-22: "brillante tipo gacha", more addictive), following how slot and
gacha audio is built: bell-like timbres and major keys for rewards, rising tones and risers for
anticipation, a hush before the reveal, celebrations whose length, loudness and richness grow
with the prize (short-short-long for small wins; timpani, brass, choir and a long shower of
coins and sparkles for the big ones), and soft steps for routine actions. Key: D major.

Levels are set by loudness (the loudest 50 ms window) against the existing effects:
nav -34 dB, card -31 dB, purchase -25.5 dB, reward -22 dB, win -21 dB.
"""
from __future__ import annotations

import argparse
import array
import hashlib
import math
from pathlib import Path
import random
import sys
import wave

RATE = 44100
TAU = math.tau
ROOT = Path(__file__).resolve().parents[1]
AUDIO = ROOT / 'Assets' / 'Resources' / 'Game' / 'Audio'
PEAK_CAP = 0.42

# Scene timing (GachaScene.cs): the churn must end on the KA-CHUNK.
CRANK_T, THUNK_AT = 1.9, 0.86
THUNK_TIME = CRANK_T * THUNK_AT


def note(name: str) -> float:
    """'D5' -> Hz (A4 = 440)."""
    steps = {'C': -9, 'C#': -8, 'D': -7, 'D#': -6, 'E': -5, 'F': -4, 'F#': -3, 'G': -2, 'G#': -1, 'A': 0, 'A#': 1, 'B': 2}
    pitch, octave = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((steps[pitch] + 12 * (octave - 4)) / 12)


# Bright 'open' notes with no third (D E G A B): they shine over the D minor theme without
# clashing with its F. Only the Legendaria resolves to a full D major chord (a Picardy third).
CRANK_NOTES = [note(n) for n in ('D6', 'E6', 'G6', 'A6', 'B6', 'D7')]
PENTA_HIGH = [note(n) for n in ('D6', 'E6', 'G6', 'A6', 'B6', 'D7', 'E7', 'A7')]


# ---------------------------------------------------------------- buffers and filters

def frames(seconds: float) -> int:
    return max(1, round(seconds * RATE))


class Track:
    def __init__(self, seconds: float) -> None:
        self.x = [0.0] * frames(seconds)

    def mix(self, at: float, samples, gain: float = 1.0) -> None:
        x = self.x
        start = round(at * RATE)
        end = min(len(x), start + len(samples))
        for i in range(max(0, start), end):
            x[i] += samples[i - start] * gain


def noise(rng: random.Random, seconds: float) -> list[float]:
    u = rng.uniform
    return [u(-1.0, 1.0) for _ in range(frames(seconds))]


def lowpass(x, cutoff: float) -> list[float]:
    a = 1 - math.exp(-TAU * cutoff / RATE)
    y = 0.0
    out = []
    for s in x:
        y += a * (s - y)
        out.append(y)
    return out


def highpass(x, cutoff: float) -> list[float]:
    a = 1 - math.exp(-TAU * cutoff / RATE)
    y = 0.0
    out = []
    for s in x:
        y += a * (s - y)
        out.append(s - y)
    return out


def bandpass(x, f0: float, q: float, f1: float | None = None, curve: float = 1.0) -> list[float]:
    """RBJ band-pass (0 dB peak); the centre may sweep f0 -> f1 (exponentially, shaped by curve)."""
    count = len(x)
    x1 = x2 = y1 = y2 = 0.0
    b0 = b2 = a1 = a2 = 0.0
    out = []
    for i, s in enumerate(x):
        if i % 16 == 0:
            u = (i / max(1, count - 1)) ** curve
            f = f0 if f1 is None else f0 * (f1 / f0) ** u
            w = TAU * min(f, RATE * 0.45) / RATE
            alpha = math.sin(w) / (2 * q)
            a0 = 1 + alpha
            b0, b2 = alpha / a0, -alpha / a0
            a1, a2 = -2 * math.cos(w) / a0, (1 - alpha) / a0
        y = b0 * s + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, s, y1, y
        out.append(y)
    return out


def shape(x, envelope) -> list[float]:
    return [s * envelope(i / RATE) for i, s in enumerate(x)]


def decay_env(attack: float, decay: float):
    def env(t: float) -> float:
        if t < attack:
            return math.sin(0.5 * math.pi * t / attack) ** 2
        return math.exp(-(t - attack) * decay)
    return env


def bell_env(seconds: float, peak_at: float, power: float = 2.0):
    def env(t: float) -> float:
        if t <= 0 or t >= seconds:
            return 0.0
        if t < peak_at:
            return math.sin(0.5 * math.pi * t / peak_at) ** power
        return math.cos(0.5 * math.pi * (t - peak_at) / (seconds - peak_at)) ** power
    return env


def reverb(x, room: float = 0.82, damp: float = 0.3, wet: float = 0.3, tail: float = 1.0) -> list[float]:
    """Mono Freeverb (8 damped combs + 4 all-passes): the hall that makes bells shine."""
    x = list(x) + [0.0] * frames(tail)
    feedback, damp1 = room * 0.28 + 0.7, damp * 0.4
    combs = [[0.0] * n for n in (1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617)]
    stores = [0.0] * 8
    index = [0] * 8
    passes = [[0.0] * n for n in (556, 441, 341, 225)]
    pindex = [0] * 4
    out = []
    for s in x:
        inp = s * 0.015
        acc = 0.0
        for k in range(8):
            buf = combs[k]
            i = index[k]
            o = buf[i]
            stores[k] = o * (1 - damp1) + stores[k] * damp1
            buf[i] = inp + stores[k] * feedback
            index[k] = i + 1 if i + 1 < len(buf) else 0
            acc += o
        for k in range(4):
            buf = passes[k]
            i = pindex[k]
            bo = buf[i]
            buf[i] = acc + bo * 0.5
            acc = bo - acc
            pindex[k] = i + 1 if i + 1 < len(buf) else 0
        out.append(s + acc * wet * 3.0)
    return out


# ---------------------------------------------------------------- instruments

def damped(seconds: float, freq: float, decay: float, phase: float = 0.0) -> list[float]:
    count = frames(seconds)
    if freq >= RATE * 0.45:
        return [0.0] * count
    w = TAU * freq / RATE
    r = math.exp(-decay / RATE)
    c = 2 * r * math.cos(w)
    rr = r * r
    y2 = math.sin(phase - w) / r
    y1 = math.sin(phase)
    out = [y1]
    for _ in range(count - 1):
        y = c * y1 - rr * y2
        out.append(y)
        y2, y1 = y1, y
    return out


def modes(seconds: float, f0: float, table) -> list[float]:
    out = [0.0] * frames(seconds)
    for ratio, amp, decay in table:
        for i, s in enumerate(damped(seconds, f0 * ratio, decay)):
            out[i] += amp * s
    attack = frames(0.0015)
    for i in range(min(attack, len(out))):
        out[i] *= math.sin(0.5 * math.pi * i / attack) ** 2
    return out


def glide(seconds: float, f0: float, f1: float, envelope, vibrato: tuple[float, float] = (0.0, 0.0),
          harmonics=((1, 1.0),)) -> list[float]:
    rate, depth = vibrato
    phase = 0.0
    out = []
    count = frames(seconds)
    for i in range(count):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, count - 1))
        if depth:
            f *= 1 + depth * math.sin(TAU * rate * t)
        phase += TAU * f / RATE
        out.append(envelope(t) * sum(a * math.sin(h * phase) for h, a in harmonics))
    return out


def fm_bell(freq: float, seconds: float = 1.2, index: float = 3.0, ratio: float = 3.5, decay: float = 3.0) -> list[float]:
    """DX-style bell: a sine phase-modulated at a non-integer ratio, the brightness fading first."""
    out = []
    wc, wm = TAU * freq / RATE, TAU * freq * ratio / RATE
    if freq * (1 + ratio * index * 0.5) >= RATE * 0.45:
        index = max(0.0, (RATE * 0.45 / freq - 1) / (ratio * 0.5))
    for i in range(frames(seconds)):
        t = i / RATE
        env = (math.sin(0.5 * math.pi * min(1.0, t / 0.002)) ** 2) * math.exp(-t * decay)
        mod = index * math.exp(-t * decay * 2.2)
        out.append(env * math.sin(wc * i + mod * math.sin(wm * i)))
    return out


def glock(freq: float, seconds: float = 0.6, decay: float = 7.0) -> list[float]:
    """Glockenspiel bar: the bright, sparkly reward bell."""
    return modes(seconds, freq, ((1.0, 1.0, decay), (2.76, 0.42, decay * 2.2), (5.40, 0.16, decay * 3.6), (8.93, 0.05, decay * 5.0)))


def celesta(freq: float, seconds: float = 0.8, decay: float = 4.5) -> list[float]:
    return modes(seconds, freq, ((1.0, 1.0, decay), (2.0, 0.22, decay * 1.6), (4.0, 0.12, decay * 2.6), (6.0, 0.04, decay * 3.4)))


def ping(freq: float, seconds: float = 0.18) -> list[float]:
    return modes(seconds, freq, ((1.0, 1.0, 20.0), (2.76, 0.3, 38.0), (5.4, 0.08, 60.0)))


def click(rng: random.Random, freq: float = 3000.0, q: float = 1.6, seconds: float = 0.008, decay: float = 900.0) -> list[float]:
    return shape(bandpass(noise(rng, seconds), freq, q), decay_env(0.0004, decay))


def plastic(rng: random.Random, freq: float, seconds: float = 0.07, weight: float = 1.0) -> list[float]:
    out = [0.0] * frames(seconds)
    tick = click(rng, freq * 2.2, 1.4, 0.006, 1100)
    shell = modes(seconds, freq, ((1.0, 1.0, 55.0), (2.31, 0.45, 80.0), (3.62, 0.20, 110.0)))
    body = shape(bandpass(noise(rng, seconds), freq * 1.1, 3.5), decay_env(0.0006, 140))
    for i in range(len(out)):
        out[i] = 0.55 * shell[i] * weight + 0.8 * body[i]
    for i, s in enumerate(tick):
        out[i] += 0.5 * s
    return out


def metal(rng: random.Random, freq: float, seconds: float = 0.35, damping: float = 1.0) -> list[float]:
    table = ((1.0, 1.0, 9 * damping), (1.594, 0.55, 14 * damping), (2.136, 0.36, 19 * damping),
             (2.296, 0.24, 23 * damping), (2.653, 0.16, 30 * damping))
    ring = modes(seconds, freq, table)
    tick = click(rng, freq * 1.6, 2.0, 0.005, 1300)
    for i, s in enumerate(tick):
        ring[i] += 0.6 * s
    return lowpass(ring, 9000)


def wood(freq: float, seconds: float = 0.12, decay: float = 30.0) -> list[float]:
    return modes(seconds, freq, ((1.0, 1.0, decay), (2.41, 0.18, decay * 1.7), (3.87, 0.035, decay * 2.4)))


def thump(seconds: float, f0: float, f1: float, decay: float) -> list[float]:
    return glide(seconds, f0, f1, decay_env(0.002, decay))


def timpani(rng: random.Random, freq: float, seconds: float = 1.4, decay: float = 3.2) -> list[float]:
    body = glide(seconds, freq * 1.08, freq, decay_env(0.004, decay), harmonics=((1, 1.0), (1.5, 0.35), (1.99, 0.25), (2.44, 0.12)))
    skin = shape(lowpass(noise(rng, 0.2), 900), decay_env(0.001, 35))
    for i, s in enumerate(skin):
        body[i] += 0.5 * s
    return body


def brass(freq: float, seconds: float, attack: float = 0.03, release: float = 0.25) -> list[float]:
    """Additive brass stab: the brightness opens with the attack ('blat') and settles."""
    out = []
    phases = [0.0] * 14
    for i in range(frames(seconds)):
        t = i / RATE
        env = min(1.0, t / attack) if t < attack else 1.0
        if t > seconds - release:
            env *= max(0.0, (seconds - t) / release)
        bright = 0.35 + 0.65 * math.exp(-t * 6) + 0.15 * min(1.0, t / attack)
        s = 0.0
        for k in range(1, 15):
            f = freq * k
            if f > 9000:
                break
            phases[k - 1] += TAU * f * (1 + 0.003 * math.sin(TAU * 5.5 * t)) / RATE
            s += math.sin(phases[k - 1]) / k * math.exp(-(k - 1) * (1.15 - bright))
        out.append(env * s)
    return out


def choir(freqs, seconds: float, attack: float = 0.35) -> list[float]:
    """'Aah' pad: detuned buzzy voices through vowel formants."""
    raw = [0.0] * frames(seconds)
    for f in freqs:
        for detune in (0.994, 1.0, 1.006):
            v = glide(seconds, f * detune, f * detune, lambda t: 1.0, (5.0, 0.004), ((1, 1.0), (2, 0.5), (3, 0.33), (4, 0.25), (5, 0.2), (6, 0.16)))
            for i, s in enumerate(v):
                raw[i] += s
    formed = [0.0] * len(raw)
    for centre, q, gain in ((700, 4.0, 1.0), (1150, 5.0, 0.6), (2700, 6.0, 0.25)):
        for i, s in enumerate(bandpass(raw, centre, q)):
            formed[i] += gain * s
    return shape(formed, bell_env(seconds, attack, 1.5))


def shower(rng: random.Random, seconds: float, density: float, notes, level: float = 1.0, coins: float = 0.5) -> list[float]:
    """Coins and sparkles raining down, thinning out: the 'you won' layer."""
    track = Track(seconds + 0.3)
    t = 0.0
    while True:
        u = t / seconds
        t += rng.expovariate(density * (1.0 - 0.85 * u))
        if t >= seconds:
            break
        fade = (1 - t / seconds) ** 1.2
        if rng.random() < coins:
            track.mix(t, metal(rng, rng.uniform(2600, 4200), 0.12, 3.5), level * 0.5 * fade * rng.uniform(0.4, 1.0))
        else:
            track.mix(t, ping(rng.choice(notes), 0.2), level * fade * rng.uniform(0.35, 1.0))
    return track.x


def sparkle_run(notes, step: float, seconds: float = 0.22, instrument=None) -> list[float]:
    instrument = instrument or (lambda f: ping(f, seconds))
    track = Track(step * len(notes) + seconds + 0.05)
    for k, f in enumerate(notes):
        track.mix(k * step, instrument(f), 1.0 - 0.3 * k / max(1, len(notes)))
    return track.x


def riser(rng: random.Random, seconds: float, f0: float, f1: float, chord=(1.0, 1.25, 1.5)) -> list[float]:
    """Tension: filtered noise opening up and a shimmering chord climbing."""
    air = bandpass(noise(rng, seconds), f0 * 2, 1.0, f1 * 4, 1.6)
    air = shape(air, lambda t: (t / seconds) ** 2.2)
    out = air
    for ratio in chord:
        tone = glide(seconds, f0 * ratio, f1 * ratio, lambda t: (t / seconds) ** 1.8, (11.0, 0.008), ((1, 1.0), (2, 0.3), (3, 0.12)))
        out = [a + 0.22 * b for a, b in zip(out, tone)]
    return out


# ---------------------------------------------------------------- the sounds

def coin_toss(rng, pitch, variant):
    """The coin flies off the purse with a bright, spinning 'ching'."""
    track = Track(0.6)
    track.mix(0.0, click(rng, 3200 * pitch, 1.4, 0.01, 700), 0.4)
    ring = metal(rng, 2350 * pitch, 0.55, 1.4)
    spin, phase = [], 0.0
    for i, s in enumerate(ring):
        phase += TAU * (10 + 20 * i / len(ring)) / RATE
        spin.append(s * (0.3 + 0.7 * abs(math.cos(phase))))
    track.mix(0.003, spin, 0.9)
    track.mix(0.02, sparkle_run([note('A6') * pitch, note('D7') * pitch], 0.06, 0.18), 0.35)
    air = shape(bandpass(noise(rng, 0.5), 1600 * pitch, 1.2, 4200 * pitch), bell_env(0.5, 0.35))
    track.mix(0.03, air, 0.12)
    return reverb(track.x, 0.6, 0.4, 0.18, 0.35)


def coin_slot(rng, pitch, variant):
    """KA-CHING into the slot, a quick rattle down, and the machine powers up (all bulbs on)."""
    track = Track(0.6)
    track.mix(0.0, metal(rng, 2400 * pitch, 0.3, 2.2), 1.0)
    track.mix(0.0, wood(420 * pitch, 0.12, 26), 0.3)
    at, gap, level = 0.05, 0.03, 0.45
    for _ in range(3):
        track.mix(at, metal(rng, rng.uniform(1800, 2300) * pitch, 0.07, 4.5), level)
        at += gap
        gap *= 0.8
        level *= 0.7
    track.mix(0.13, metal(rng, 1100 * pitch, 0.2, 3.2), 0.45)
    track.mix(0.13, wood(240 * pitch, 0.2, 18), 0.5)
    power_up = sparkle_run([f * pitch for f in (note('D6'), note('E6'), note('A6'), note('D7'))], 0.045, 0.3, lambda f: glock(f, 0.3, 10))
    track.mix(0.02, power_up, 0.5)
    return reverb(track.x, 0.7, 0.35, 0.25, 0.4)


def crank(rng, pitch, step):
    """One ratchet tooth + a little bell climbing the D major scale with the crank (anticipation)."""
    track = Track(0.12)
    p = pitch * 2 ** (step * 1.5 / 12)
    track.mix(0.0, click(rng, 3400 * p, 2.6, 0.007, 1100), 1.0)
    track.mix(0.0, modes(0.06, 1500 * p, ((1.0, 1.0, 85), (2.3, 0.35, 120))), 0.35)
    track.mix(0.0, wood(230 * p, 0.06, 80), 0.35)
    track.mix(0.016, click(rng, 2600 * p, 2.4, 0.005, 1400), 0.4)
    bell = glock(CRANK_NOTES[min(len(CRANK_NOTES) - 1, step)] * pitch, 0.11, 30)
    track.mix(0.002, bell, 0.22 + 0.03 * step)
    return reverb(track.x, 0.4, 0.5, 0.1, 0.05)


def churn(rng, pitch, variant):
    """Capsules swirl faster and faster under a climbing major chord; a hush right before the KA-CHUNK."""
    seconds = THUNK_TIME + 0.02
    hush = 0.07
    track = Track(seconds)

    def swirl(t: float) -> float:
        u = t / CRANK_T
        return math.sin(0.5 * math.pi * u / 0.18) ** 2 if u < 0.18 else 1.0

    t = 0.02
    while t < seconds - hush:
        u = t / THUNK_TIME
        t += rng.expovariate(18 + 70 * u ** 1.4)
        if t >= seconds - hush:
            break
        level = rng.uniform(0.3, 1.0) * swirl(t) * (0.5 + 0.5 * u)
        track.mix(t, plastic(rng, rng.uniform(1000, 2600) * pitch, 0.045, 0.6), level * 0.8)
    rise = riser(rng, seconds, note('D4') * pitch, note('A4') * pitch * 1.06, (1.0, 1.335, 1.5, 2.0))
    track.mix(0.0, rise, 0.75)
    hum = glide(seconds, 70 * pitch, 110 * pitch, lambda tt: swirl(tt) * (0.4 + 0.6 * tt / seconds))
    track.mix(0.0, hum, 0.15)
    x = track.x
    cut, fade = frames(hush), frames(0.03)
    for i in range(1, cut + 1):
        x[-i] = 0.0
    for i in range(1, fade + 1):
        x[-cut - i] *= (i - 1) / fade
    return x


def zap(rng, pitch, variant):
    """A magic fizz crackling over the glass."""
    track = Track(0.14)
    t = 0.0
    for _ in range(rng.randint(5, 8)):
        t += rng.uniform(0.004, 0.014)
        if t > 0.09:
            break
        burst = shape(bandpass(noise(rng, 0.007), rng.uniform(3500, 6500) * pitch, 2.4), decay_env(0.0003, rng.uniform(600, 1200)))
        track.mix(t, burst, rng.uniform(0.4, 1.0))
    chirp = glide(0.08, rng.choice(PENTA_HIGH) * pitch, rng.choice(PENTA_HIGH) * pitch * 1.3, decay_env(0.003, 35))
    track.mix(0.004, chirp, 0.3)
    return lowpass(track.x, 9000)


def tease(rng, pitch, variant):
    """Épica and better: the light is sucked in with a rising choir - something big is coming."""
    seconds = 0.65
    peak = 0.5
    track = Track(seconds)

    def swell(t: float) -> float:
        return (t / peak) ** 3 if t < peak else math.exp(-(t - peak) * 30)

    air = shape(bandpass(noise(rng, seconds), 700 * pitch, 1.0, 5200 * pitch, 0.8), swell)
    track.mix(0.0, air, 0.8)
    voices = choir([note('D4') * pitch, note('A4') * pitch, note('D5') * pitch], seconds, peak)
    track.mix(0.0, shape(voices, swell), 0.35)
    track.mix(0.0, shape(glide(seconds, note('A5') * pitch, note('A6') * pitch, lambda t: 1.0, (15, 0.02)), swell), 0.12)
    track.mix(peak, thump(0.15, 140 * pitch, 70 * pitch, 26), 0.5)
    return reverb(track.x, 0.8, 0.3, 0.25, 0.6)


def thunk(rng, pitch, variant):
    """KA-CHUNK: the latch lets go and the capsule drops - a heavy, satisfying lock-in."""
    track = Track(0.5)
    track.mix(0.0, click(rng, 3600 * pitch, 1.8, 0.008, 900), 0.8)
    track.mix(0.0, metal(rng, 980 * pitch, 0.18, 3.0), 0.55)
    at = 0.04
    track.mix(at, thump(0.36, 160 * pitch, 48 * pitch, 11), 1.0)
    track.mix(at, wood(185 * pitch, 0.3, 16), 0.8)
    track.mix(at, plastic(rng, 980 * pitch, 0.09, 1.0), 0.6)
    track.mix(at, shape(lowpass(noise(rng, 0.2), 420), decay_env(0.002, 24)), 0.9)
    track.mix(at + 0.01, glock(note('D6') * pitch, 0.4, 8), 0.12)
    return reverb(track.x, 0.55, 0.5, 0.12, 0.3)


def pop(rng, pitch, variant):
    """The capsule plops out through the flap with a bright little 'pop' and a sparkle."""
    track = Track(0.45)
    track.mix(0.0, shape(bandpass(noise(rng, 0.04), 650 * pitch, 2.2), decay_env(0.001, 90)), 0.6)
    track.mix(0.006, plastic(rng, 560 * pitch, 0.12, 1.4), 1.0)
    track.mix(0.006, thump(0.07, 900 * pitch, 380 * pitch, 40), 0.4)
    track.mix(0.03, sparkle_run([note('A6') * pitch, note('D7') * pitch, note('E7') * pitch], 0.04, 0.18), 0.4)
    track.mix(0.2, shape(bandpass(noise(rng, 0.03), 700 * pitch, 2.4), decay_env(0.001, 120)), 0.3)
    track.mix(0.2, wood(250 * pitch, 0.08, 50), 0.22)
    return reverb(track.x, 0.6, 0.4, 0.15, 0.3)


def bounce(rng, pitch, bounce_index):
    """Plastic on stone with a playful bell; the second bounce higher and softer."""
    p = pitch * (1.0, 1.122)[bounce_index]
    track = Track(0.3)
    track.mix(0.0, plastic(rng, 660 * p, 0.13, 1.2), 1.0)
    track.mix(0.0, thump(0.1, 125 * p, 82 * p, 30), 0.4)
    track.mix(0.024, plastic(rng, 1700 * p, 0.04, 0.5), 0.2)
    track.mix(0.002, glock((note('E6'), note('A6'))[bounce_index] * pitch, 0.25, 12), 0.25)
    return reverb(track.x, 0.5, 0.45, 0.12, 0.2)


def hop(rng, pitch, sparkle):
    """Whoosh up to the stage; Poco común and better climb a glittering run."""
    track = Track(0.62)
    whoosh = lowpass(bandpass(noise(rng, 0.5), 380 * pitch, 1.3, 1700 * pitch, 0.7), 3600)
    track.mix(0.0, shape(whoosh, bell_env(0.5, 0.24)), 0.75)
    if sparkle:
        notes = [f * pitch for f in (note('A5'), note('D6'), note('E6'), note('G6'), note('A6'), note('D7'), note('E7'))]
        track.mix(0.02, sparkle_run(notes, 0.05, 0.22, lambda f: glock(f, 0.22, 14)), 0.55)
    return reverb(track.x, 0.7, 0.35, 0.22, 0.45)


def wobble(rng, pitch, step):
    """The capsule rocks once per tier: knock-knock and a bell climbing D E G A D' - each rock brighter."""
    track = Track(0.5)
    notes = (note('D5'), note('E5'), note('G5'), note('A5'), note('D6'))
    p = pitch * 2 ** (step * 2 / 12)
    track.mix(0.0, plastic(rng, 900 * p, 0.07, 0.9), 0.65)
    track.mix(0.11, plastic(rng, 1000 * p, 0.07, 0.9), 0.55)
    bell = fm_bell(notes[step] * pitch, 0.45, 2.0 + 0.4 * step, 3.5, 5.5)
    track.mix(0.004, bell, 0.6 + 0.08 * step)
    if step >= 2:
        track.mix(0.02, glock(notes[step] * 2 * pitch, 0.35, 9), 0.25 + 0.05 * step)
        swell = shape(highpass(noise(rng, 0.35), 4500), bell_env(0.35, 0.25, 2.0))
        track.mix(0.0, swell, 0.04 * step)
    return reverb(track.x, 0.75, 0.3, 0.25, 0.45)


def open_capsule(rng, pitch, variant):
    """POP! The halves fly apart in a burst of sparkles and a puff of air."""
    track = Track(0.8)
    track.mix(0.0, click(rng, 3600 * pitch, 2.0, 0.008, 800), 1.0)
    track.mix(0.0, thump(0.08, 1300 * pitch, 460 * pitch, 30), 0.8)
    track.mix(0.0, plastic(rng, 820 * pitch, 0.1, 1.1), 0.6)
    track.mix(0.0, thump(0.14, 120 * pitch, 66 * pitch, 22), 0.55)
    track.mix(0.004, shape(highpass(lowpass(noise(rng, 0.4), 6000), 900), decay_env(0.004, 9)), 0.3)
    track.mix(0.01, shower(rng, 0.45, 60, PENTA_HIGH, 0.35, 0.25), 1.0)
    return reverb(track.x, 0.8, 0.3, 0.28, 0.7)


def tier(rng, pitch, tier_index):
    """The reveal fanfare, one per rarity: the rarer, the higher, richer, louder and longer."""
    lead = 0.05
    D4, FS4, A4, D5, E5, FS5, A5, D6, E6, FS6, G6, A6, D7 = (note(n) for n in ('D4', 'F#4', 'A4', 'D5', 'E5', 'F#5', 'A5', 'D6', 'E6', 'F#6', 'G6', 'A6', 'D7'))
    if tier_index == 0:
        # Común: ding-ding (short-short), a pleasant small win.
        track = Track(0.7)
        track.mix(lead, glock(A5, 0.5, 6), 0.8)
        track.mix(lead + 0.09, glock(D6, 0.55, 5.5), 1.0)
        return reverb(track.x, 0.7, 0.35, 0.25, 0.7)
    if tier_index == 1:
        # Poco común: short-short-long up the major triad.
        track = Track(0.9)
        for k, (f, level) in enumerate(((D6, 0.7), (E6, 0.8))):
            track.mix(lead + 0.08 * k, glock(f, 0.4, 7), level)
        track.mix(lead + 0.17, glock(A6, 0.75, 4.2), 1.0)
        track.mix(lead + 0.17, fm_bell(A5, 0.75, 1.5, 2.0, 4.0), 0.35)
        track.mix(lead + 0.2, shower(rng, 0.35, 30, PENTA_HIGH, 0.25, 0.0), 1.0)
        return reverb(track.x, 0.78, 0.3, 0.28, 0.9)
    if tier_index == 2:
        # Rara: bright arpeggio blooming into a chord, with a short coin shower.
        track = Track(1.2)
        for k, f in enumerate((D5, E5, A5, D6)):
            track.mix(lead + 0.07 * k, glock(f * 2, 0.45, 6), 0.55 + 0.1 * k)
        bloom = lead + 0.28
        for f, level in ((D6, 0.8), (E6, 0.6), (A6, 0.6)):
            track.mix(bloom, fm_bell(f, 1.0, 2.2, 3.5, 3.2), level * 0.6)
        track.mix(bloom, celesta(D5, 0.9, 3.5), 0.4)
        track.mix(bloom, shower(rng, 0.6, 45, PENTA_HIGH, 0.4, 0.4), 1.0)
        return reverb(track.x, 0.82, 0.28, 0.3, 1.1)
    if tier_index == 3:
        # Épica: a boom, a choir swells under a fast climbing arpeggio, a wide purple bloom.
        track = Track(1.7)
        track.mix(lead, timpani(rng, note('D2') * 2, 1.2, 3.5), 0.7)
        track.mix(lead, choir([D4, A4, D5, E5], 1.5, 0.3), 0.18)
        for k, f in enumerate((D5, E5, A5, D6, E6, A6)):
            track.mix(lead + 0.05 * k, glock(f * 2 if k < 3 else f, 0.4, 7), 0.45 + 0.06 * k)
        bloom = lead + 0.32
        for f, level in ((D6, 0.8), (E6, 0.5), (G6, 0.45), (A6, 0.6), (D7, 0.4)):
            track.mix(bloom, fm_bell(f, 1.3, 2.5, 3.5, 2.6), level * 0.55)
        track.mix(bloom, shower(rng, 1.0, 55, PENTA_HIGH, 0.45, 0.45), 1.0)
        return reverb(track.x, 0.86, 0.25, 0.32, 1.4)
    # Legendaria: timpani roll + boom, a brass fanfare (short-short-LONG), a huge bell chord,
    # the choir, and a long rain of coins and sparkles.
    track = Track(3.0)
    for k in range(6):
        track.mix(lead + 0.035 * k, timpani(rng, note('A2') * 2, 0.25, 9), 0.18 + 0.05 * k)
    boom = lead + 0.24
    track.mix(boom, timpani(rng, note('D2') * 2, 1.8, 2.4), 0.9)
    for at, f, length in ((lead + 0.0, A4, 0.11), (lead + 0.12, A4, 0.11), (boom, D5, 1.1)):
        for chord in ((1.0, 0.55), (1.26, 0.4), (1.5, 0.45)):
            track.mix(at, brass(f * chord[0], length, 0.02, 0.35 if length > 0.5 else 0.05), chord[1] * 0.16)
    track.mix(boom, choir([D4, FS4, A4, D5], 2.2, 0.4), 0.22)
    for f, level in ((D6, 0.9), (FS6, 0.7), (A6, 0.7), (D7, 0.5)):
        track.mix(boom, fm_bell(f, 2.0, 2.8, 3.5, 1.8), level * 0.5)
    track.mix(boom, sparkle_run([D6, E6, FS6, A6, note('B6'), D7, note('E7'), note('F#7')], 0.04, 0.25, lambda f: glock(f, 0.25, 10)), 0.5)
    track.mix(boom + 0.1, shower(rng, 2.2, 70, PENTA_HIGH, 0.5, 0.5), 1.0)
    return reverb(track.x, 0.88, 0.22, 0.32, 1.6)


def golden(rng, pitch, variant):
    """A golden relic: a glittering 'shiiing' just after the reveal."""
    delay = 0.24
    track = Track(1.4)
    for k, f in enumerate((note('A5'), note('D6'), note('E6'), note('A6'), note('D7'))):
        track.mix(delay - 0.15 + 0.03 * k, ping(f * pitch, 0.2), 0.3)
    ring = [0.0] * frames(1.0)
    for f, level in ((note('D7'), 1.0), (note('A7'), 0.5), (note('E7'), 0.6)):
        for i, s in enumerate(fm_bell(f * pitch, 1.0, 1.5, 1.41, 2.8)):
            ring[i] += level * s
    ring = [s * (0.7 + 0.3 * math.sin(TAU * 12 * i / RATE)) for i, s in enumerate(ring)]
    track.mix(delay, ring, 0.5)
    track.mix(delay, shape(highpass(noise(rng, 0.8), 5500), decay_env(0.02, 4.0)), 0.07)
    track.mix(delay, shower(rng, 0.7, 50, PENTA_HIGH, 0.35, 0.6), 0.8)
    return reverb(track.x, 0.84, 0.25, 0.3, 0.9)


def keep(rng, pitch, variant):
    """Quedármela: the relic flies up with a rising glittering run."""
    track = Track(0.6)
    whoosh = lowpass(bandpass(noise(rng, 0.5), 520 * pitch, 1.4, 2400 * pitch, 0.8), 3600)
    track.mix(0.0, shape(whoosh, bell_env(0.5, 0.3)), 0.55)
    notes = [f * pitch for f in (note('D5'), note('E5'), note('A5'), note('D6'), note('E6'), note('A6'))]
    track.mix(0.02, sparkle_run(notes, 0.055, 0.22, lambda f: glock(f, 0.22, 12)), 0.6)
    return reverb(track.x, 0.72, 0.32, 0.22, 0.45)


def stash(rng, pitch, variant):
    """The relic lands in the counter: click into place and a big, warm 'item get' chord."""
    track = Track(0.9)
    track.mix(0.0, metal(rng, 1700 * pitch, 0.12, 3.8), 0.5)
    track.mix(0.0, wood(420 * pitch, 0.12, 32), 0.5)
    for f, level in ((note('D6'), 1.0), (note('E6'), 0.5), (note('A6'), 0.7)):
        track.mix(0.012, fm_bell(f * pitch, 0.8, 2.0, 3.5, 3.6), level * 0.55)
    track.mix(0.012, celesta(note('D5') * pitch, 0.7, 4.0), 0.4)
    track.mix(0.08, sparkle_run([note('A6') * pitch, note('D7') * pitch], 0.05, 0.18), 0.25)
    return reverb(track.x, 0.8, 0.3, 0.26, 0.8)


def dust(rng, pitch, variant):
    """Rechazar: the relic softens into dust - a gentle falling sparkle, never a failure buzz."""
    track = Track(0.85)
    for at in (0.0, 0.02):
        track.mix(at, click(rng, 1800 * pitch, 2.0, 0.01, 500), 0.35)
    t = 0.01
    while t < 0.6:
        t += rng.expovariate(200 * math.exp(-t * 4.0) + 18)
        centre = 3600 * math.exp(-t * 2.2) + 700
        grain = shape(bandpass(noise(rng, 0.006), rng.uniform(0.7, 1.3) * centre * pitch, 2.5), decay_env(0.0004, 900))
        track.mix(t, grain, rng.uniform(0.25, 0.7) * math.exp(-t * 3.0))
    track.mix(0.01, shape(bandpass(noise(rng, 0.6), 900 * pitch, 0.8, 320 * pitch), decay_env(0.035, 5.5)), 0.35)
    falling = [f * pitch for f in (note('A6'), note('E6'), note('D6'), note('A5'))]
    track.mix(0.03, sparkle_run(falling, 0.07, 0.2), 0.22)
    return reverb(track.x, 0.7, 0.4, 0.2, 0.5)


# name: (generator, variants, loudness dB of the loudest 50 ms, per-variant offset dB, pitched round-robin)
CUES = {
    'gacha_coin_toss': (coin_toss, 3, -27.0, 0.0, True),
    'gacha_coin_slot': (coin_slot, 3, -25.5, 0.0, True),
    'gacha_crank': (crank, 6, -31.0, 0.6, False),
    'gacha_churn': (churn, 2, -30.0, 0.0, True),
    'gacha_zap': (zap, 3, -34.0, 0.0, True),
    'gacha_tease': (tease, 2, -25.0, 0.0, True),
    'gacha_thunk': (thunk, 3, -21.5, 0.0, True),
    'gacha_pop': (pop, 3, -25.5, 0.0, True),
    'gacha_bounce': (bounce, 2, -27.0, -2.5, False),
    'gacha_hop': (hop, 2, -29.0, 2.0, False),
    'gacha_wobble': (wobble, 5, -28.0, 0.8, False),
    'gacha_open': (open_capsule, 3, -22.0, 0.0, True),
    'gacha_tier': (tier, 5, -24.0, 1.5, False),
    'gacha_golden': (golden, 1, -23.0, 0.0, True),
    'gacha_keep': (keep, 3, -26.0, 0.0, True),
    'gacha_stash': (stash, 3, -22.5, 0.0, True),
    'gacha_dust': (dust, 3, -29.0, 0.0, True),
}


# ---------------------------------------------------------------- mastering and files

def loudness(x) -> float:
    window = frames(0.05)
    best = 0.0
    for start in range(0, max(1, len(x) - window), 220):
        chunk = x[start:start + window]
        best = max(best, sum(s * s for s in chunk) / len(chunk))
    return math.sqrt(best)


def trim_tail(x, floor: float = 6e-4) -> list[float]:
    """Drop the silent end of a reverb tail (keeps 20 ms after the last audible sample)."""
    peak = max(abs(s) for s in x) or 1.0
    last = len(x) - 1
    while last > 0 and abs(x[last]) < floor * peak:
        last -= 1
    return x[:min(len(x), last + frames(0.02))]


def master(x, target_db: float, keep_length: bool = False) -> list[float]:
    x = highpass(x, 28)
    if not keep_length:
        x = trim_tail(x)
    count = len(x)
    rise, fall = frames(0.0012), frames(0.015)
    for i in range(min(rise, count)):
        x[i] *= math.sin(0.5 * math.pi * i / rise) ** 2
    for i in range(1, min(fall, count) + 1):
        x[-i] *= math.sin(0.5 * math.pi * (i - 1) / fall) ** 2
    gain = 10 ** (target_db / 20) / max(1e-9, loudness(x))
    peak = max(abs(s) for s in x) * gain
    if peak > PEAK_CAP:
        gain *= PEAK_CAP / peak
    x = [s * gain for s in x]
    x[0] = x[-1] = 0.0
    return x


def synthesize(name: str, variant: int) -> list[float]:
    generator, variants, target, step_db, pitched = CUES[name]
    rng = random.Random(f'last-wish:gachapon:v2:{name}:{variant}')
    pitch = (0.97, 1.0, 1.03)[variant % 3] if pitched and variants > 1 else 1.0
    raw = generator(rng, pitch, variant)
    return master(raw, target + step_db * variant, keep_length=name == 'gacha_churn')


def pcm(samples) -> bytes:
    data = array.array('h', (round(max(-1.0, min(1.0, s)) * 32767) for s in samples))
    if sys.byteorder != 'little':
        data.byteswap()
    return data.tobytes()


def write_wav(path: Path, samples) -> None:
    with wave.open(str(path), 'wb') as output:
        output.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        output.writeframes(pcm(samples))


def read_wav(path: Path) -> array.array:
    with wave.open(str(path), 'rb') as source:
        if source.getnchannels() != 1 or source.getsampwidth() != 2 or source.getframerate() != RATE:
            raise ValueError(f'{path.name}: expected mono 16-bit {RATE} Hz')
        data = array.array('h', source.readframes(source.getnframes()))
    if sys.byteorder != 'little':
        data.byteswap()
    return data


META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 0
    quality: 0.7
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write_meta(path: Path) -> None:
    meta = path.with_name(path.name + '.meta')
    if meta.exists():
        return
    guid = hashlib.md5(f'last-wish/gachapon-audio/{path.name}'.encode()).hexdigest()
    meta.write_text(META.format(guid=guid), encoding='utf-8', newline='\n')


def files():
    for name, (_, variants, *_rest) in CUES.items():
        for variant in range(variants):
            yield name, variant, AUDIO / f'{name}_{variant}.wav'


def check() -> int:
    problems = 0
    for name, variant, path in files():
        if not path.exists() or not path.with_name(path.name + '.meta').exists():
            print(f'missing: {path.name} (or its .meta)')
            problems += 1
            continue
        expected = array.array('h')
        expected.frombytes(pcm(synthesize(name, variant)))
        actual = read_wav(path)
        if len(actual) != len(expected) or max(abs(a - b) for a, b in zip(actual, expected)) > 2:
            print(f'differs: {path.name}')
            problems += 1
    print('ok' if problems == 0 else f'{problems} problem(s)')
    return 1 if problems else 0


# ---------------------------------------------------------------- previews (whole pulls at the scene's timing)

def preview(directory: Path) -> None:
    """Mix pulls exactly as GachaScene schedules them, to listen without the editor."""
    cache: dict[tuple[str, int], list[float]] = {}

    def cue(name: str, variant: int = 0) -> list[float]:
        if (name, variant) not in cache:
            cache[(name, variant)] = synthesize(name, variant)
        return cache[(name, variant)]

    directory.mkdir(parents=True, exist_ok=True)
    g, chute_y, floor_y = 2600.0, 132 + 171 * 2, 548
    t1 = math.sqrt(2 * (floor_y - chute_y) / g)
    bounces = (t1, t1 + 2 * (g * t1 * 0.42) / g)
    for label, rarity, golden_relic, decline in (('comun', 1, False, False), ('poco-comun', 2, False, False), ('rara', 3, False, False),
                                                 ('epica-rechazada', 4, False, True), ('legendaria-dorada', 5, True, False)):
        track = Track(16.0)
        rng = random.Random(label)
        rr = {}

        def play(at, name, variant=None, gain=1.0):
            variants = CUES[name][1]
            if variant is None:
                variant = rr.get(name, 0)
                rr[name] = (variant + 1) % variants
            track.mix(at, cue(name, variant), gain)

        play(0.0, 'gacha_coin_toss')
        play(0.75 * 0.72, 'gacha_coin_slot')
        crank_at = 0.75
        play(crank_at, 'gacha_churn')
        for k in range(12):
            at = THUNK_TIME * (1 - (1 - k / 12) ** 1.35)
            play(crank_at + at, 'gacha_crank', k * 6 // 12)
        last_zap = -1.0
        for frame in range(int(CRANK_T * 60)):
            t = frame / 60
            u = t / CRANK_T
            big = rarity >= 4
            chance = (0.25 + 0.6 * u) * (1.0 if big else 0.45)
            if 0.15 < u < THUNK_AT and rng.random() < chance and t - last_zap > 0.11:
                play(crank_at + t, 'gacha_zap', gain=1.0 if big else 0.55)
                last_zap = t
        if rarity >= 4:
            play(crank_at + CRANK_T * 0.55, 'gacha_tease')
        play(crank_at + THUNK_TIME, 'gacha_thunk')
        drop_at = crank_at + CRANK_T
        play(drop_at, 'gacha_pop')
        for k, at in enumerate(bounces):
            play(drop_at + at, 'gacha_bounce', k)
        play(drop_at + 0.78, 'gacha_hop', 1 if rarity >= 2 else 0)
        present_at = drop_at + 0.78 + 0.55
        for k in range(rarity):
            play(present_at + 0.35 + k * 0.46, 'gacha_wobble', k)
        open_at = present_at + 0.35 + rarity * 0.46 + 0.5
        play(open_at, 'gacha_open')
        play(open_at, 'gacha_tier', rarity - 1)
        if golden_relic:
            play(open_at, 'gacha_golden')
        decide_at = open_at + 2.6
        if decline:
            play(decide_at, 'gacha_dust')
            end = decide_at + 1.0
        else:
            play(decide_at, 'gacha_keep')
            play(decide_at + 0.55, 'gacha_stash')
            end = decide_at + 1.6
        mix = track.x[:frames(end)]
        loud = max(abs(s) for s in mix)
        gain = min(1.6, 0.9 / loud)
        write_wav(directory / f'gacha-preview-{label}.wav', [s * gain for s in mix])
        print(f'preview {label}: {end:.1f}s, peak {loud * gain:.2f}')


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--check', action='store_true', help='verify the files instead of writing them')
    parser.add_argument('--preview', type=Path, help='also write whole-pull previews into this folder')
    args = parser.parse_args()
    if args.check:
        return check()
    AUDIO.mkdir(parents=True, exist_ok=True)
    for name, variant, path in files():
        samples = synthesize(name, variant)
        write_wav(path, samples)
        write_meta(path)
        if variant == CUES[name][1] - 1:
            print(f'{name}: {CUES[name][1]} variant(s), {len(samples) / RATE:.2f}s, '
                  f'loudness {20 * math.log10(loudness(samples)):.1f} dB, peak {max(abs(s) for s in samples):.3f}', flush=True)
    if args.preview:
        preview(args.preview)
    return 0


if __name__ == '__main__':
    sys.exit(main())
