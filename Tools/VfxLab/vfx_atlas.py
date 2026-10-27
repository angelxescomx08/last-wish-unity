"""Particle atlas (8×8 tiles of 16×16, white shapes), trail ramp and light falloff for the VFX Lab.

Pixel particles keep a constant quad size (16 px = 1 world unit at PPU 16); their size over lifetime is
done with flipbook frames that shrink the shape, so every particle stays a crisp pixel sprite.
Tile index = row * 8 + column (Unity's Texture Sheet Animation counts from the top-left tile).
"""
import math

import numpy as np
from PIL import Image

from pixelkit import BAYER4

TILE = 16
COLS = ROWS = 8

# Row map (documented in docs/vfx-lab.md and mirrored in VfxAtlas.cs).
ROW_SPARK = 0      # shrink flipbook: 7px star -> 1px dot
ROW_MOTE = 1       # soft ember flipbook: 3x3 soft -> fading dot
ROW_SHAPES = 2     # 0 soft blob, 1 thin ring, 2 thick ring, 3 halo ring, 4 starburst, 5 long glint, 6 shard, 7 dot 3x3
ROW_IMPACT = 3     # impact flash flipbook
ROW_TWINKLE = 4    # twinkle flipbook (grow and shrink)
ROW_SMOKE = 5      # dithered puff flipbook (alpha material)
ROW_RUNES = 6      # 8 arcane glyphs
ROW_DASH = 7       # 0..7 soft dashes / streak heads


def _tile():
    return np.zeros((TILE, TILE), float)


def _plus(t, r, c=7, core=1.0, arm=1.0):
    t[c, c] = core
    for i in range(1, r + 1):
        v = arm * (1 - (i - 1) / (r + 0.5)) if r > 1 else arm
        t[c, c + i] = t[c, c - i] = t[c + i, c] = t[c - i, c] = max(t[c + i, c], v)
    return t


def _diag(t, r, c=7, v=0.6):
    for i in range(1, r + 1):
        for dx, dy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
            t[c + dy * i, c + dx * i] = max(t[c + dy * i, c + dx * i], v * (1 - (i - 1) / (r + 0.5)))
    return t


def _disc(t, r, c=7.5, soft=0.0, value=1.0):
    yy, xx = np.mgrid[0:TILE, 0:TILE]
    d = np.hypot(xx + 0.5 - c, yy + 0.5 - c)
    if soft <= 0:
        t[d <= r] = np.maximum(t[d <= r], value)
    else:
        a = np.clip((r - d) / soft, 0, 1) * value
        np.maximum(t, a, out=t)
    return t


def _ring(t, r, w=1.0, c=7.5, value=1.0, soft=0.0):
    yy, xx = np.mgrid[0:TILE, 0:TILE]
    d = np.hypot(xx + 0.5 - c, yy + 0.5 - c)
    if soft <= 0:
        m = np.abs(d - r) <= w / 2
        t[m] = np.maximum(t[m], value)
    else:
        a = np.clip(1 - np.abs(d - r) / soft, 0, 1) * value
        np.maximum(t, a, out=t)
    return t


def spark_row():
    tiles = []
    t = _plus(_tile(), 3, core=1, arm=1); _diag(t, 1, v=0.7); tiles.append(t)        # 7px star
    t = _plus(_tile(), 2, core=1, arm=1); _diag(t, 1, v=0.5); tiles.append(t)        # 5px star
    tiles.append(_plus(_tile(), 2, core=1, arm=0.85))                                 # 5px plus
    tiles.append(_plus(_tile(), 1, core=1, arm=0.9))                                  # 3px plus
    t = _tile(); t[7:9, 7:9] = 1.0; tiles.append(t)                                   # 2x2
    t = _tile(); t[7, 7] = 1.0; t[7, 8] = 0.6; tiles.append(t)                        # 1px + tail
    t = _tile(); t[7, 7] = 1.0; tiles.append(t)                                       # 1px
    t = _tile(); t[7, 7] = 0.55; tiles.append(t)                                      # dim 1px
    return tiles


def mote_row():
    tiles = []
    t = _tile(); t[6:9, 6:9] = 0.55; t[7, 7] = 1.0; t[6, 7] = t[8, 7] = t[7, 6] = t[7, 8] = 0.85; tiles.append(t)
    t = _tile(); t[7, 7] = 1.0; t[6, 7] = t[8, 7] = t[7, 6] = t[7, 8] = 0.6; tiles.append(t)
    t = _tile(); t[7:9, 7:9] = 0.9; tiles.append(t)
    t = _tile(); t[7:9, 7:9] = 0.6; t[7, 7] = 1.0; tiles.append(t)
    t = _tile(); t[7, 7] = 1.0; tiles.append(t)
    t = _tile(); t[7, 7] = 0.75; tiles.append(t)
    t = _tile(); t[7, 7] = 0.5; tiles.append(t)
    t = _tile(); t[7, 7] = 0.3; tiles.append(t)
    return tiles


def shapes_row():
    tiles = []
    # 0 soft blob (quadratic falloff, used for glow halos; the shader posterizes and dithers it)
    yy, xx = np.mgrid[0:TILE, 0:TILE]
    d = np.hypot(xx + 0.5 - 8, yy + 0.5 - 8) / 8.0
    tiles.append(np.clip(1 - d, 0, 1) ** 2)
    tiles.append(_ring(_tile(), 6.5, 1.0, c=8))                                      # 1 thin ring
    tiles.append(_ring(_tile(), 6.0, 2.0, c=8))                                      # 2 thick ring
    tiles.append(_ring(_tile(), 6.0, c=8, soft=2.2))                                 # 3 soft halo ring
    t = _plus(_tile(), 7, core=1, arm=1); _diag(t, 4, v=0.8); _disc(t, 2.2, c=7.0, value=1.0); tiles.append(t)  # 4 starburst
    t = _plus(_tile(), 7, core=1, arm=1); _diag(t, 2, v=0.45); tiles.append(t)      # 5 long glint
    t = _tile()                                                                        # 6 shard (diamond 3x5)
    for y, row in enumerate(["..#..", ".###.", ".###.", "#####", ".###.", ".###.", "..#.."]):
        for x, ch in enumerate(row):
            if ch == '#':
                t[4 + y, 5 + x] = 1.0 if x == 2 or y == 3 else 0.75
    tiles.append(t)
    t = _tile(); t[6:9, 6:9] = 1.0; tiles.append(t)                                   # 7 solid 3x3
    return tiles


def impact_row():
    tiles = []
    for i in range(8):
        k = i / 7.0
        t = _tile()
        if i == 0:
            _disc(t, 4.5, c=7.5, value=1.0)
            _plus(t, 7, core=1, arm=1)
            _diag(t, 5, v=0.9)
        elif i == 1:
            _disc(t, 3.5, c=7.5, value=1.0)
            _ring(t, 5.5, 1.2, c=8)
            _plus(t, 7, core=1, arm=0.9)
            _diag(t, 4, v=0.7)
        else:
            r = 4.0 + k * 3.4
            ring = _ring(_tile(), r, 1.0, c=8, value=1.0 - 0.6 * k)
            # break the ring apart as it expands
            ang = np.degrees(np.arctan2(np.mgrid[0:TILE, 0:TILE][0] + 0.5 - 8, np.mgrid[0:TILE, 0:TILE][1] + 0.5 - 8)) % 360
            keep = ((ang // (360 / (16 - i))) % 2 == 0) if i >= 4 else np.ones_like(ring, bool)
            t = np.maximum(t, np.where(keep, ring, 0))
            arm = max(0.0, 1.0 - k * 1.25)
            if arm > 0:
                _plus(t, max(1, 7 - i), core=arm, arm=arm)
            if i <= 3:
                _disc(t, max(0.8, 3.0 - i * 0.7), c=7.5, value=0.9)
        tiles.append(np.clip(t, 0, 1))
    return tiles


def twinkle_row():
    tiles = []
    for size in (0, 1, 2, 3, 4, 3, 2, 1):
        t = _tile()
        if size == 0:
            t[7, 7] = 0.8
        else:
            _plus(t, size + (1 if size >= 3 else 0), core=1, arm=1)
            if size >= 3:
                _diag(t, size - 2, v=0.55)
        tiles.append(t)
    return tiles


def smoke_row():
    tiles = []
    rng = np.random.default_rng(7)
    noise = rng.random((TILE, TILE))
    yy, xx = np.mgrid[0:TILE, 0:TILE]
    for i in range(8):
        k = i / 7.0
        r = 3.0 + 4.0 * k
        d = np.hypot(xx + 0.5 - 8, yy + 0.5 - 8 + k * 1.5)
        dens = np.clip(1 - d / r, 0, 1) * (1 - k * 0.85)
        bay = BAYER4[yy % 4, xx % 4]
        t = ((dens * 1.6 + noise * 0.25 - 0.25) > bay).astype(float) * 1.0
        tiles.append(t)
    return tiles


RUNES = [
    ["..#..", ".#.#.", "#...#", ".#.#.", "..#..", "..#..", ".###."],
    ["#...#", ".#.#.", "..#..", ".#.#.", "#...#", ".....", "#####"],
    ["####.", "#..#.", "####.", "#..#.", "#...#", ".....", "..#.."],
    ["..#..", "..#..", "#####", "..#..", ".#.#.", "#...#", "....."],
    [".###.", "#...#", "..##.", ".#...", "#####", ".....", "..#.."],
    ["#.#.#", "#.#.#", ".###.", "..#..", "..#..", ".###.", "....."],
    ["##.##", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "##.##"],
    ["..#..", ".###.", "#.#.#", "..#..", "#.#.#", ".###.", "..#.."],
]


def rune_row():
    tiles = []
    for rune in RUNES:
        t = _tile()
        for y, row in enumerate(rune):
            for x, ch in enumerate(row):
                if ch == '#':
                    t[4 + y, 5 + x] = 1.0
        tiles.append(t)
    return tiles


def dash_row():
    tiles = []
    for n in (1, 2, 3, 5, 7, 9, 11, 15):
        t = _tile()
        x0 = 8 - (n + 1) // 2
        for i in range(n):
            t[7, x0 + i] = 0.45 + 0.55 * (i + 1) / n
        tiles.append(t)
    return tiles


def build_atlas():
    rows = [spark_row(), mote_row(), shapes_row(), impact_row(), twinkle_row(), smoke_row(), rune_row(), dash_row()]
    a = np.zeros((ROWS * TILE, COLS * TILE), float)
    for r, tiles in enumerate(rows):
        for c, t in enumerate(tiles):
            a[r * TILE:(r + 1) * TILE, c * TILE:(c + 1) * TILE] = t
    rgba = np.zeros(a.shape + (4,), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = (np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)
    return Image.fromarray(rgba, 'RGBA')


def build_trail():
    """Ribbon texture for TrailRenderer / particle trails: bright centre line, soft edges (16x8)."""
    h, w = 8, 16
    rgba = np.zeros((h, w, 4), np.uint8)
    rgba[..., :3] = 255
    prof = [0.0, 0.25, 0.6, 1.0, 1.0, 0.6, 0.25, 0.0]
    for y in range(h):
        rgba[y, :, 3] = int(prof[y] * 255)
    return Image.fromarray(rgba, 'RGBA')


def build_light(size=64, bands=7):
    """Pixel-art light pool: radial falloff posterized into bands with Bayer dithering between them."""
    yy, xx = np.mgrid[0:size, 0:size]
    c = size / 2
    d = np.hypot(xx + 0.5 - c, yy + 0.5 - c) / c
    f = np.clip(1 - d, 0, 1) ** 1.6
    bay = BAYER4[yy % 4, xx % 4]
    q = np.floor(f * bands + bay) / bands
    rgba = np.zeros((size, size, 4), np.uint8)
    rgba[..., :3] = (np.clip(q, 0, 1)[..., None] * 255 + 0.5).astype(np.uint8)
    rgba[..., 3] = 255
    return Image.fromarray(rgba, 'RGBA')
